using System.Net.Http.Headers;
using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace MvcMusicStore.Services;

/// <summary>
/// Downloads remote album artwork and stores it in Azure Blob Storage (thumbnails container),
/// returning the absolute blob URL. Replaces the previous local-disk cache.
/// </summary>
public sealed class BlobThumbnailCacheService : IThumbnailCacheService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/gif",
        "image/jpeg",
        "image/png",
        "image/svg+xml",
        "image/webp"
    };

    private readonly HttpClient httpClient;
    private readonly BlobServiceClient blobServiceClient;
    private readonly IOptions<StorageOptions> options;
    private readonly IOptions<ThumbnailCacheOptions> cacheOptions;
    private readonly ILogger<BlobThumbnailCacheService> logger;

    public BlobThumbnailCacheService(
        HttpClient httpClient,
        BlobServiceClient blobServiceClient,
        IOptions<StorageOptions> options,
        IOptions<ThumbnailCacheOptions> cacheOptions,
        ILogger<BlobThumbnailCacheService> logger)
    {
        this.httpClient = httpClient;
        this.blobServiceClient = blobServiceClient;
        this.options = options;
        this.cacheOptions = cacheOptions;
        this.logger = logger;

        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MvcMusicStore", "1.0"));
        }
    }

    public async Task<string?> TryCacheThumbnailAsync(string? thumbnailUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out var sourceUri) ||
            (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
        {
            return thumbnailUrl;
        }

        // Already stored in our own blob container - nothing to do.
        var containerClient = blobServiceClient.GetBlobContainerClient(options.Value.ThumbnailsContainer);
        if (sourceUri.Host.Equals(containerClient.Uri.Host, StringComparison.OrdinalIgnoreCase) &&
            sourceUri.AbsolutePath.Contains("/" + options.Value.ThumbnailsContainer + "/", StringComparison.OrdinalIgnoreCase))
        {
            return thumbnailUrl;
        }

        var normalizedUrl = NormalizeSourceUrl(thumbnailUrl);
        var cacheKey = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedUrl))).ToLowerInvariant();

        try
        {
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            using var response = await httpClient.GetAsync(normalizedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Unable to cache thumbnail {ThumbnailUrl}. Remote server returned {StatusCode}.", normalizedUrl, response.StatusCode);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var extension = ResolveExtension(contentType, sourceUri);
            if (extension is null)
            {
                logger.LogWarning("Unable to cache thumbnail {ThumbnailUrl}. Unsupported content type {ContentType}.", normalizedUrl, contentType);
                return null;
            }

            var blobName = cacheKey + extension;
            var blobClient = containerClient.GetBlobClient(blobName);

            if (await blobClient.ExistsAsync(cancellationToken))
            {
                return BuildProxyUrl(blobName);
            }

            var maxBytes = Math.Max(1, cacheOptions.Value.MaxBytes);
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            long totalBytes = 0;
            while (true)
            {
                var bytesRead = await input.ReadAsync(chunk, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;
                if (totalBytes > maxBytes)
                {
                    logger.LogWarning("Unable to cache thumbnail {ThumbnailUrl}. File exceeded {MaxBytes} bytes.", normalizedUrl, maxBytes);
                    return null;
                }

                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
            }

            buffer.Position = 0;
            var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
            await blobClient.UploadAsync(
                buffer,
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = resolvedContentType } },
                cancellationToken);

            return BuildProxyUrl(blobName);
        }
        catch (RequestFailedException ex)
        {
            logger.LogWarning(ex, "Unable to store thumbnail {ThumbnailUrl} in blob storage.", normalizedUrl);
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Unable to cache thumbnail {ThumbnailUrl}.", normalizedUrl);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Timed out caching thumbnail {ThumbnailUrl}.", normalizedUrl);
            return null;
        }
    }

    private static string BuildProxyUrl(string blobName)
    {
        return "/media/thumbnails/" + blobName;
    }

    private static string NormalizeSourceUrl(string thumbnailUrl)
    {
        return thumbnailUrl.StartsWith("http://coverartarchive.org/", StringComparison.OrdinalIgnoreCase)
            ? "https://coverartarchive.org/" + thumbnailUrl["http://coverartarchive.org/".Length..]
            : thumbnailUrl;
    }

    private static string? ResolveExtension(string? contentType, Uri sourceUri)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && !AllowedContentTypes.Contains(contentType))
        {
            return null;
        }

        return contentType?.ToLowerInvariant() switch
        {
            "image/gif" => ".gif",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/svg+xml" => ".svg",
            "image/webp" => ".webp",
            _ => ResolveExtensionFromPath(sourceUri.AbsolutePath)
        };
    }

    private static string ResolveExtensionFromPath(string absolutePath)
    {
        var extension = Path.GetExtension(absolutePath).ToLowerInvariant();
        return extension switch
        {
            ".gif" => ".gif",
            ".jpeg" => ".jpg",
            ".jpg" => ".jpg",
            ".png" => ".png",
            ".svg" => ".svg",
            ".webp" => ".webp",
            _ => ".jpg"
        };
    }
}
