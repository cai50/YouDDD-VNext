
using Listening.Domain.ValueObjects;

namespace Listening.Main.WebAPI.Controllers.Episodes.ViewModels;
public record EpisodeVM(Guid Id, MultilingualString Name, Guid AlbumId, Uri AudioUrl, double DurationInSecond, IEnumerable<SentenceVM>? zhSentences
    , IEnumerable<SentenceVM>? enSentences)
{
    public static EpisodeVM? Create(Episode? e, bool loadSubtitle)
    {
        if (e == null)
        {
            return null;
        }
        List<SentenceVM> zhSentenceVMs = new();
        List<SentenceVM> enSentenceVMs = new();
        if (loadSubtitle)
        {
            var sentences = e.ParseSubtitle();
            foreach (Sentence s in sentences.Item1)
            {
                SentenceVM vm = new SentenceVM(s.StartTime.TotalSeconds, s.EndTime.TotalSeconds, s.Value);
                zhSentenceVMs.Add(vm);
            }
            foreach (Sentence s in sentences.Item2)
            {
                SentenceVM vm = new SentenceVM(s.StartTime.TotalSeconds, s.EndTime.TotalSeconds, s.Value);
                enSentenceVMs.Add(vm);
            }
        }
        return new EpisodeVM(e.Id, e.Name, e.AlbumId, e.AudioUrl, e.DurationInSecond, zhSentenceVMs, enSentenceVMs);
    }

    public static EpisodeVM[] Create(Episode[] items, bool loadSubtitle)
    {
        return items.Select(e => Create(e, loadSubtitle)!).ToArray();
    }
}
