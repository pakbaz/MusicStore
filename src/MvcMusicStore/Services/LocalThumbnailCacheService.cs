using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace MvcMusicStore.Services;

public sealed class LocalThumbnailCacheService : IThumbnailCacheService
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
    private readonly IWebHostEnvironment environment;
    private readonly IOptions<ThumbnailCacheOptions> options;
    private readonly ILogger<LocalThumbnailCacheService> logger;

    public LocalThumbnailCacheService(
        HttpClient httpClient,
        IWebHostEnvironment environment,
        IOptions<ThumbnailCacheOptions> options,
        ILogger<LocalThumbnailCacheService> logger)
    {
        this.httpClient = httpClient;
        this.environment = environment;
        this.options = options;
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

        var normalizedUrl = NormalizeSourceUrl(thumbnailUrl);
        var relativeDirectory = NormalizeRelativeDirectory(options.Value.RelativeDirectory);
        var cacheDirectory = Path.Combine(GetWebRootPath(), relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(cacheDirectory);

        var cacheKey = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedUrl))).ToLowerInvariant();
        var existingFile = Directory.EnumerateFiles(cacheDirectory, cacheKey + ".*").FirstOrDefault();
        if (existingFile is not null)
        {
            return ToAppRelativePath(relativeDirectory, Path.GetFileName(existingFile));
        }

        try
        {
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

            var maxBytes = Math.Max(1, options.Value.MaxBytes);
            var outputPath = Path.Combine(cacheDirectory, cacheKey + extension);
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(outputPath);

            var buffer = new byte[81920];
            long totalBytes = 0;
            while (true)
            {
                var bytesRead = await input.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;
                if (totalBytes > maxBytes)
                {
                    output.Close();
                    File.Delete(outputPath);
                    logger.LogWarning("Unable to cache thumbnail {ThumbnailUrl}. File exceeded {MaxBytes} bytes.", normalizedUrl, maxBytes);
                    return null;
                }

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            return ToAppRelativePath(relativeDirectory, Path.GetFileName(outputPath));
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

    private string GetWebRootPath()
    {
        return string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;
    }

    private static string NormalizeSourceUrl(string thumbnailUrl)
    {
        return thumbnailUrl.StartsWith("http://coverartarchive.org/", StringComparison.OrdinalIgnoreCase)
            ? "https://coverartarchive.org/" + thumbnailUrl["http://coverartarchive.org/".Length..]
            : thumbnailUrl;
    }

    private static string NormalizeRelativeDirectory(string? relativeDirectory)
    {
        var normalized = string.IsNullOrWhiteSpace(relativeDirectory)
            ? "Images/MetadataCache"
            : relativeDirectory.Trim().Replace('\\', '/').Trim('/');

        return normalized.Contains("..", StringComparison.Ordinal)
            ? "Images/MetadataCache"
            : normalized;
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

    private static string ToAppRelativePath(string relativeDirectory, string fileName)
    {
        return "~/" + relativeDirectory.Trim('/').Replace('\\', '/') + "/" + fileName;
    }
}
