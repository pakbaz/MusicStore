using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MvcMusicStore.Services;

public sealed class MusicBrainzAlbumArtworkService : IAlbumArtworkService, IAlbumMetadataService
{
    private const string MusicBrainzApiBaseUrl = "https://musicbrainz.org/ws/2/";
    private const string CoverArtArchiveBaseUrl = "https://coverartarchive.org/";

    private readonly HttpClient httpClient;
    private readonly ILogger<MusicBrainzAlbumArtworkService> logger;

    public MusicBrainzAlbumArtworkService(HttpClient httpClient, ILogger<MusicBrainzAlbumArtworkService> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;

        if (httpClient.BaseAddress is null)
        {
            httpClient.BaseAddress = new Uri(MusicBrainzApiBaseUrl);
        }

        if (!httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MvcMusicStore", "1.0"));
        }

        if (!httpClient.DefaultRequestHeaders.Accept.Any())
        {
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public async Task<string?> TryGetThumbnailUrlAsync(string artistName, string albumTitle, CancellationToken cancellationToken = default)
    {
        var metadata = await TryGetMetadataAsync(artistName, albumTitle, cancellationToken);
        return metadata?.ThumbnailUrl;
    }

    public async Task<AlbumMetadataResult?> TryGetMetadataAsync(string artistName, string albumTitle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artistName) || string.IsNullOrWhiteSpace(albumTitle))
        {
            return null;
        }

        var release = await TryGetReleaseAsync(artistName, albumTitle, cancellationToken);
        if (release is null)
        {
            return null;
        }

        var thumbnailUrl = await TryGetCoverArtThumbnailAsync(release.ReleaseId, cancellationToken);
        return new AlbumMetadataResult(thumbnailUrl, release.ReleaseDate);
    }

    private async Task<MusicBrainzRelease?> TryGetReleaseAsync(string artistName, string albumTitle, CancellationToken cancellationToken)
    {
        var query = $"release:{albumTitle} AND artist:{artistName}";
        var url = $"release?query={Uri.EscapeDataString(query)}&fmt=json&limit=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("releases", out var releases) || releases.GetArrayLength() == 0)
        {
            return null;
        }

        var firstRelease = releases[0];
        if (!firstRelease.TryGetProperty("id", out var idElement) || string.IsNullOrWhiteSpace(idElement.GetString()))
        {
            return null;
        }

        DateTime? releaseDate = null;
        if (firstRelease.TryGetProperty("date", out var dateElement))
        {
            releaseDate = ParseMusicBrainzDate(dateElement.GetString());
        }

        return new MusicBrainzRelease(idElement.GetString()!, releaseDate);
    }

    private async Task<string?> TryGetCoverArtThumbnailAsync(string releaseId, CancellationToken cancellationToken)
    {
        var coverArtUrl = $"{CoverArtArchiveBaseUrl}release/{releaseId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, coverArtUrl);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogDebug("No Cover Art Archive image found for MusicBrainz release {ReleaseId}.", releaseId);
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("images", out var images) || images.GetArrayLength() == 0)
        {
            return null;
        }

        var firstImage = images[0];
        if (firstImage.TryGetProperty("thumbnails", out var thumbnails))
        {
            if (thumbnails.TryGetProperty("small", out var smallThumbnail) && !string.IsNullOrWhiteSpace(smallThumbnail.GetString()))
            {
                return NormalizeArtworkUrl(smallThumbnail.GetString()!);
            }

            if (thumbnails.TryGetProperty("large", out var largeThumbnail) && !string.IsNullOrWhiteSpace(largeThumbnail.GetString()))
            {
                return NormalizeArtworkUrl(largeThumbnail.GetString()!);
            }
        }

        if (firstImage.TryGetProperty("image", out var fullImage) && !string.IsNullOrWhiteSpace(fullImage.GetString()))
        {
            return NormalizeArtworkUrl(fullImage.GetString()!);
        }

        return null;
    }

    private static string NormalizeArtworkUrl(string artworkUrl)
    {
        return artworkUrl.StartsWith("http://coverartarchive.org/", StringComparison.OrdinalIgnoreCase)
            ? "https://coverartarchive.org/" + artworkUrl["http://coverartarchive.org/".Length..]
            : artworkUrl;
    }

    private static DateTime? ParseMusicBrainzDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var format in new[] { "yyyy-MM-dd", "yyyy-MM", "yyyy" })
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate;
            }
        }

        return null;
    }

    private sealed record MusicBrainzRelease(string ReleaseId, DateTime? ReleaseDate);
}
