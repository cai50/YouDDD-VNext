namespace Listening.Main.WebAPI.Controllers.Albums.ViewModels;
public record AlbumVM(Guid Id, MultilingualString Name, Guid CategoryId, Uri? CoverUrl)
{
    public static AlbumVM? Create(Album? a)
    {
        if (a == null)
        {
            return null;
        }
        return new AlbumVM(a.Id, a.Name, a.CategoryId, a.CoverUrl);
    }

    public static AlbumVM[] Create(Album[] items)
    {
        return items.Select(e => Create(e)!).ToArray();
    }
}
