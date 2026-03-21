using FileService.SDK.NETCore;
using Listening.Admin.WebAPI.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using Zack.EventBus;
using Zack.JWT;
using System.Linq;
using Listening.Domain.ValueObjects;

namespace Listening.Admin.WebAPI.Episodes;
[Route("[controller]/[action]")]
[ApiController]
[Authorize(Roles = "Admin")]
[UnitOfWork(typeof(ListeningDbContext))]
public class EpisodeController : ControllerBase
{
    private IListeningRepository repository;
    private readonly ListeningDbContext dbContext;
    private readonly EncodingEpisodeHelper encodingEpisodeHelper;
    private readonly IEventBus eventBus;
    private readonly ListeningDomainService domainService;

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IOptionsSnapshot<JWTOptions> jwtOptions;
    private readonly ITokenService tokenService;
    private readonly IOptionsSnapshot<FileServiceOptions> optionFileService;

    public EpisodeController(ListeningDbContext dbContext,
            EncodingEpisodeHelper encodingEpisodeHelper,
            IEventBus eventBus, ListeningDomainService domainService,
            IListeningRepository repository,
            IHttpClientFactory httpClientFactory,
            IOptionsSnapshot<JWTOptions> jwtOptions,
            ITokenService tokenService,
            IOptionsSnapshot<FileServiceOptions> optionFileService)
    {
        this.dbContext = dbContext;
        this.encodingEpisodeHelper = encodingEpisodeHelper;
        this.eventBus = eventBus;
        this.domainService = domainService;
        this.repository = repository;
        this.httpClientFactory = httpClientFactory;
        this.jwtOptions = jwtOptions;
        this.tokenService = tokenService;
        this.optionFileService = optionFileService;
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Add(EpisodeAddRequest req)
    {
        //如果上传的是m4a，不用转码，直接存到数据库
        if (req.AudioUrl.ToString().EndsWith("m4a", StringComparison.OrdinalIgnoreCase))
        {

            Episode episode = await domainService.AddEpisodeAsync(req.Name, req.AlbumId,
                req.AudioUrl, req.DurationInSecond, req.SubtitleType, req.Subtitle);

            dbContext.Add(episode);
            return episode.Id;
        }
        else
        {
            //非m4a文件需要先转码，为了避免非法数据污染业务数据，增加业务逻辑麻烦，按照DDD的原则，不完整的Episode不能插入数据库
            //先临时插入Redis，转码完成再插入数据库
            Guid episodeId = Guid.NewGuid();
            EncodingEpisodeInfo encodingEpisode = new EncodingEpisodeInfo(episodeId, req.Name, req.AlbumId, req.DurationInSecond, req.Subtitle, req.SubtitleType, "Created");
            await encodingEpisodeHelper.AddEncodingEpisodeAsync(episodeId, encodingEpisode);

            //通知转码
            eventBus.Publish("MediaEncoding.Created", new { MediaId = episodeId, MediaUrl = req.AudioUrl, OutputFormat = "m4a", SourceSystem = "Listening" });//启动转码
            return episodeId;
        }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<EpisodeImportFromFoldersResponse>> ImportFromFolders(
        EpisodeImportFromFoldersRequest req, CancellationToken cancellationToken = default)
    {
        Uri urlRoot = optionFileService.Value.UrlRoot;
        FileServiceClient fileService = new FileServiceClient(httpClientFactory,
            urlRoot, jwtOptions.Value, tokenService);

        Dictionary<string, FileInfo> subtitleFiles = new DirectoryInfo(req.SubtitleDir)
            .GetFiles("*.ass", SearchOption.TopDirectoryOnly)
            .GroupBy(f => Path.GetFileNameWithoutExtension(f.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        FileInfo[] audioFiles = new DirectoryInfo(req.AudioDir)
            .GetFiles("*.m4a", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<string> importedFiles = new List<string>();
        List<string> skippedMessages = new List<string>();
        var existingEpisodes = await repository.GetEpisodesByAlbumIdAsync(req.AlbumId);
        HashSet<string> existingEnglishNames = existingEpisodes
            .Select(e => e.Name.English)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<Episode> newEpisodes = new List<Episode>();

        foreach (FileInfo audioFile in audioFiles)
        {
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(audioFile.Name);
            if (existingEnglishNames.Contains(fileNameWithoutExt))
            {
                skippedMessages.Add($"跳过{audioFile.Name}：文件已导入");
                continue;
            }
            if (!subtitleFiles.TryGetValue(fileNameWithoutExt, out FileInfo? subtitleFile))
            {
                skippedMessages.Add($"跳过{audioFile.Name}：没有找到同名.ass字幕");
                continue;
            }
            if (!req.ChineseNames.TryGetValue(fileNameWithoutExt, out string? chineseName)
                || string.IsNullOrWhiteSpace(chineseName))
            {
                skippedMessages.Add($"跳过{audioFile.Name}：没有提供中文名");
                continue;
            }

            string subtitle = await System.IO.File.ReadAllTextAsync(subtitleFile.FullName, cancellationToken);
            double durationInSecond = GetDurationInSecondFromAss(subtitle);
            if (durationInSecond <= 0)
            {
                skippedMessages.Add($"跳过{audioFile.Name}：无法从.ass字幕解析时长");
                continue;
            }

            try
            {
                Uri audioUrl = await fileService.UploadAsync(audioFile, cancellationToken);
                MultilingualString name = new MultilingualString(chineseName, fileNameWithoutExt);

                Episode episode = await domainService.AddEpisodeAsync(name, req.AlbumId,
                    audioUrl, durationInSecond, "ass", subtitle);

                dbContext.Add(episode);
                newEpisodes.Add(episode);
                importedFiles.Add(audioFile.Name);
                existingEnglishNames.Add(fileNameWithoutExt);
            }
            catch (Exception ex)
            {
                skippedMessages.Add($"跳过{audioFile.Name}：{ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var episodesForSort = existingEpisodes.Concat(newEpisodes)
            .OrderBy(e => GetEpisodeOrderFromEnglishName(e.Name.English))
            .ThenBy(e => e.SequenceNumber)
            .ToArray();
        for (int i = 0; i < episodesForSort.Length; i++)
        {
            episodesForSort[i].ChangeSequenceNumber(i + 1);
        }

        return new EpisodeImportFromFoldersResponse(
            importedFiles.Count,
            skippedMessages.Count,
            importedFiles.ToArray(),
            skippedMessages.ToArray());
    }

    [HttpPut]
    [Route("{id}")]
    public async Task<ActionResult> Update([RequiredGuid] Guid id, EpisodeUpdateRequest request)
    {
        var episode = await repository.GetEpisodeByIdAsync(id);
        if (episode == null)
        {
            return NotFound("id没找到");
        }
        episode.ChangeName(request.Name);
        episode.ChangeSubtitle(request.SubtitleType, request.Subtitle);
        return Ok();
    }

    [HttpDelete]
    [Route("{id}")]
    public async Task<ActionResult> DeleteById([RequiredGuid] Guid id)
    {
        var album = await repository.GetEpisodeByIdAsync(id);
        if (album == null)
        {
            return NotFound($"没有Id={id}的Episode");
        }
        album.SoftDelete();//软删除
        return Ok();
    }

    [HttpGet]
    [Route("{id}")]
    public async Task<ActionResult<Episode>> FindById([RequiredGuid] Guid id)
    {
        var episode = await repository.GetEpisodeByIdAsync(id);
        if (episode == null)
        {
            return NotFound($"没有Id={id}的Episode");
        }
        return episode;
    }

    [HttpGet]
    [Route("{albumId}")]
    public Task<Episode[]> FindByAlbumId([RequiredGuid] Guid albumId)
    {
        return repository.GetEpisodesByAlbumIdAsync(albumId);
    }

    [HttpGet]
    [Route("{albumId}")]
    public async Task<ActionResult<EncodingEpisodeInfo[]>> FindEncodingEpisodesByAlbumId([RequiredGuid] Guid albumId)
    {
        List<EncodingEpisodeInfo> list = new List<EncodingEpisodeInfo>();
        var episodeIds = await encodingEpisodeHelper.GetEncodingEpisodeIdsAsync(albumId);
        foreach (Guid episodeId in episodeIds)
        {
            var encodingEpisode = await encodingEpisodeHelper.GetEncodingEpisodeAsync(episodeId);
            if (!encodingEpisode.Status.EqualsIgnoreCase("Completed"))
            {
                list.Add(encodingEpisode);
            }
        }
        return list.ToArray();
    }

    [HttpPut]
    [Route("{id}")]
    public async Task<ActionResult> Hide([RequiredGuid] Guid id)
    {
        var episode = await repository.GetEpisodeByIdAsync(id);
        if (episode == null)
        {
            return NotFound($"没有Id={id}的Category");
        }
        episode.Hide();
        return Ok();
    }

    [HttpPut]
    [Route("{id}")]
    public async Task<ActionResult> Show([RequiredGuid] Guid id)
    {
        var episode = await repository.GetEpisodeByIdAsync(id);
        if (episode == null)
        {
            return NotFound($"没有Id={id}的Category");
        }
        episode.Show();
        return Ok();
    }

    [HttpPut]
    [Route("{albumId}")]
    public async Task<ActionResult> Sort([RequiredGuid] Guid albumId, EpisodesSortRequest req)
    {
        await domainService.SortEpisodesAsync(albumId, req.SortedEpisodeIds);
        return Ok();
    }

    [HttpPost]
    [Route("RefreshSubtitles/{albumId}")]
    [AllowAnonymous]
    public async Task<ActionResult> RefreshSubtitles([RequiredGuid] Guid albumId)
    {
        var episodes = await repository.GetEpisodesByAlbumIdAsync(albumId);
        foreach (var episode in episodes)
        {
            var (enSubs, zhSubs) = episode.ParseSubtitle();
            var en = enSubs ?? Enumerable.Empty<Sentence>();
            var zh = zhSubs ?? Enumerable.Empty<Sentence>();
            var sentences = en.Concat(zh).ToArray();

            eventBus.Publish("ListeningEpisode.Updated", new { Id = episode.Id, episode.Name, Sentences = sentences, episode.AlbumId, episode.Subtitle, episode.SubtitleType });
        }
        return Ok();
    }

    private static double GetDurationInSecondFromAss(string subtitle)
    {
        Regex regex = new Regex(
            @"^Dialogue:\s*\d+,(?<start>\d+:\d{2}:\d{2}\.\d{2,3}),(?<end>\d+:\d{2}:\d{2}\.\d{2,3}),",
            RegexOptions.Multiline);

        TimeSpan maxEndTime = TimeSpan.Zero;
        MatchCollection matches = regex.Matches(subtitle);
        foreach (Match match in matches)
        {
            string endText = match.Groups["end"].Value;
            if (TryParseAssTime(endText, out TimeSpan endTime) && endTime > maxEndTime)
            {
                maxEndTime = endTime;
            }
        }
        return maxEndTime.TotalSeconds;
    }

    private static bool TryParseAssTime(string value, out TimeSpan result)
    {
        string[] formats = new[]
        {
            @"h\:mm\:ss\.ff",
            @"h\:mm\:ss\.fff"
        };
        return TimeSpan.TryParseExact(value, formats, CultureInfo.InvariantCulture, out result);
    }

    private static int GetEpisodeOrderFromEnglishName(string englishName)
    {
        var matches = Regex.Matches(englishName, @"E(?<num>\d{1,3})", RegexOptions.IgnoreCase);
        if (matches.Count == 0)
        {
            return int.MaxValue;
        }
        var lastMatch = matches[^1];
        return int.Parse(lastMatch.Groups["num"].Value);
    }
}