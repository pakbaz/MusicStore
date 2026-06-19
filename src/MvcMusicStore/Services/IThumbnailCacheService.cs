namespace MvcMusicStore.Services;

public interface IThumbnailCacheService
{
    Task<string?> TryCacheThumbnailAsync(string? thumbnailUrl, CancellationToken cancellationToken = default);
}
