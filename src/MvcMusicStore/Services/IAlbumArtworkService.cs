namespace MvcMusicStore.Services;

public interface IAlbumArtworkService
{
    Task<string?> TryGetThumbnailUrlAsync(string artistName, string albumTitle, CancellationToken cancellationToken = default);
}
