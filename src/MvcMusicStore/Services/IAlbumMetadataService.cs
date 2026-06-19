namespace MvcMusicStore.Services;

public interface IAlbumMetadataService
{
    Task<AlbumMetadataResult?> TryGetMetadataAsync(string artistName, string albumTitle, CancellationToken cancellationToken = default);
}
