using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services;

public sealed class AlbumMetadataEnrichmentWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<AlbumMetadataEnrichmentOptions> options;
    private readonly ILogger<AlbumMetadataEnrichmentWorker> logger;

    public AlbumMetadataEnrichmentWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AlbumMetadataEnrichmentOptions> options,
        ILogger<AlbumMetadataEnrichmentWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            logger.LogInformation("Album metadata enrichment worker is disabled.");
            return;
        }

        await DelaySafelyAsync(TimeSpan.FromSeconds(Math.Max(0, currentOptions.StartupDelaySeconds)), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            currentOptions = options.Value;
            try
            {
                await EnrichCatalogPassAsync(currentOptions, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a transient dependency failure (e.g. a Cosmos 403/throttle) escape the
                // worker — that would crash the host. Log and retry on the next interval instead.
                logger.LogError(ex, "Album metadata enrichment pass failed; will retry next interval.");
            }

            var interval = TimeSpan.FromHours(Math.Max(1, currentOptions.IntervalHours));
            await DelaySafelyAsync(interval, stoppingToken);
        }
    }

    private async Task EnrichCatalogPassAsync(AlbumMetadataEnrichmentOptions currentOptions, CancellationToken cancellationToken)
    {
        var attemptedAlbumIds = new HashSet<int>();
        while (!cancellationToken.IsCancellationRequested)
        {
            var processedCount = await EnrichBatchAsync(currentOptions, attemptedAlbumIds, cancellationToken);
            if (processedCount == 0)
            {
                return;
            }
        }
    }

    private async Task<int> EnrichBatchAsync(
        AlbumMetadataEnrichmentOptions currentOptions,
        HashSet<int> attemptedAlbumIds,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicStoreEntities>();
        var metadataService = scope.ServiceProvider.GetRequiredService<IAlbumMetadataService>();
        var thumbnailCache = scope.ServiceProvider.GetRequiredService<IThumbnailCacheService>();

        var batchSize = Math.Clamp(currentOptions.BatchSize, 1, 100);
        var allAlbums = await db.Albums.ToListAsync(cancellationToken);
        var albums = allAlbums
            .Where(album => !attemptedAlbumIds.Contains(album.AlbumId) &&
                !string.IsNullOrWhiteSpace(album.ArtistName) &&
                NeedsEnrichment(album))
            .OrderByDescending(album => IsRemoteThumbnailUrl(album.MetadataThumbnailUrl))
            .ThenBy(album => album.AlbumId)
            .Take(batchSize)
            .ToList();

        if (albums.Count == 0)
        {
            logger.LogDebug("No albums require metadata enrichment.");
            return 0;
        }

        foreach (var album in albums)
        {
            attemptedAlbumIds.Add(album.AlbumId);
        }

        var updatedCount = 0;
        foreach (var album in albums)
        {
            if (string.IsNullOrWhiteSpace(album.Title) || string.IsNullOrWhiteSpace(album.ArtistName))
            {
                continue;
            }

            var existingRemoteThumbnailUrl = IsRemoteThumbnailUrl(album.MetadataThumbnailUrl)
                ? album.MetadataThumbnailUrl
                : null;

            AlbumMetadataResult? metadata = null;
            if (!album.ReleaseDate.HasValue || ShouldApplyMetadataThumbnail(album))
            {
                try
                {
                    metadata = await metadataService.TryGetMetadataAsync(album.ArtistName!, album.Title, cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "Unable to enrich metadata for album '{AlbumTitle}' by '{ArtistName}'.", album.Title, album.ArtistName);
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Timed out enriching metadata for album '{AlbumTitle}' by '{ArtistName}'.", album.Title, album.ArtistName);
                }
            }

            var changed = false;
            if (!album.ReleaseDate.HasValue && metadata?.ReleaseDate.HasValue == true)
            {
                album.ReleaseDate = metadata.ReleaseDate;
                changed = true;
            }

            var sourceThumbnailUrl = !string.IsNullOrWhiteSpace(metadata?.ThumbnailUrl)
                ? metadata.ThumbnailUrl
                : existingRemoteThumbnailUrl;

            if ((ShouldApplyMetadataThumbnail(album) || IsRemoteThumbnailUrl(album.MetadataThumbnailUrl)) &&
                !string.IsNullOrWhiteSpace(sourceThumbnailUrl))
            {
                var cachedThumbnailUrl = await thumbnailCache.TryCacheThumbnailAsync(sourceThumbnailUrl, cancellationToken);
                if (!string.IsNullOrWhiteSpace(cachedThumbnailUrl))
                {
                    album.MetadataThumbnailUrl = cachedThumbnailUrl;
                    changed = true;
                }
            }

            if (changed)
            {
                updatedCount++;
                await db.SaveChangesAsync(cancellationToken);
            }

            await DelaySafelyAsync(TimeSpan.FromMilliseconds(Math.Max(0, currentOptions.RequestDelayMilliseconds)), cancellationToken);
        }

        if (updatedCount > 0)
        {
            logger.LogInformation("Enriched metadata for {AlbumCount} album(s).", updatedCount);
        }

        return albums.Count;
    }

    private static bool NeedsEnrichment(Album album)
    {
        return !album.ReleaseDate.HasValue ||
               IsRemoteThumbnailUrl(album.MetadataThumbnailUrl) ||
               ShouldApplyMetadataThumbnail(album);
    }

    private static bool IsRemoteThumbnailUrl(string? thumbnailUrl)
    {
        return !string.IsNullOrWhiteSpace(thumbnailUrl) &&
               (thumbnailUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                thumbnailUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldApplyMetadataThumbnail(Album album)
    {
        return string.IsNullOrWhiteSpace(album.UploadedThumbnailUrl) &&
               string.IsNullOrWhiteSpace(album.MetadataThumbnailUrl) &&
               (string.IsNullOrWhiteSpace(album.AlbumArtUrl) ||
                album.AlbumArtUrl == Album.DefaultPlaceholderThumbnailUrl ||
                album.AlbumArtUrl == "~/Images/placeholder.png" ||
                album.AlbumArtUrl == "/Images/placeholder.png");
    }

    private static async Task DelaySafelyAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // Shutdown requested.
        }
    }
}
