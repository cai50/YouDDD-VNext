using FluentValidation;
using System.IO;

namespace Listening.Admin.WebAPI.Episodes;

public record EpisodeImportFromFoldersRequest(Guid AlbumId, string AudioDir, string SubtitleDir,
    Dictionary<string, string> ChineseNames);

public class EpisodeImportFromFoldersRequestValidator : AbstractValidator<EpisodeImportFromFoldersRequest>
{
    public EpisodeImportFromFoldersRequestValidator(ListeningDbContext dbCtx)
    {
        RuleFor(x => x.AlbumId)
            .MustAsync((albumId, ct) => dbCtx.Query<Album>().AnyAsync(a => a.Id == albumId, ct))
            .WithMessage(x => $"AlbumId={x.AlbumId}不存在");

        RuleFor(x => x.AudioDir)
            .NotEmpty()
            .Must(Directory.Exists)
            .WithMessage(x => $"音频目录不存在：{x.AudioDir}");

        RuleFor(x => x.SubtitleDir)
            .NotEmpty()
            .Must(Directory.Exists)
            .WithMessage(x => $"字幕目录不存在：{x.SubtitleDir}");

        RuleFor(x => x.ChineseNames).NotNull();
    }
}