using Microsoft.AspNetCore.Mvc;
using MvcMusicStore.Models;

namespace MvcMusicStore.Infrastructure
{
    /// <summary>
    /// <see cref="IUrlHelper"/> extensions for clean, slug-based album/artist URLs and for resolving
    /// content paths to absolute URLs (needed for canonical links, Open Graph images, and the sitemap).
    /// Slug links are generated via named routes so generation is deterministic regardless of the
    /// catch-all default route.
    /// </summary>
    public static class SeoUrlExtensions
    {
        public static string AlbumUrl(this IUrlHelper url, int albumId, string? title, bool absolute = false)
        {
            var values = new { id = albumId, slug = Seo.Slug(title) };
            if (!absolute)
            {
                return url.RouteUrl("album", values) ?? string.Empty;
            }

            var request = url.ActionContext.HttpContext.Request;
            return url.RouteUrl("album", values, request.Scheme, request.Host.Value) ?? string.Empty;
        }

        public static string ArtistUrl(this IUrlHelper url, int artistId, string? name, bool absolute = false)
        {
            var values = new { id = artistId, slug = Seo.Slug(name) };
            if (!absolute)
            {
                return url.RouteUrl("artist", values) ?? string.Empty;
            }

            var request = url.ActionContext.HttpContext.Request;
            return url.RouteUrl("artist", values, request.Scheme, request.Host.Value) ?? string.Empty;
        }

        /// <summary>Resolves a content path (<c>~/…</c>, <c>/…</c>, or absolute http(s)) to an absolute URL.</summary>
        public static string AbsoluteContent(this IUrlHelper url, string? contentPath)
        {
            if (string.IsNullOrWhiteSpace(contentPath))
            {
                return string.Empty;
            }

            var normalized = Album.NormalizeThumbnailUrl(contentPath);
            if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            var request = url.ActionContext.HttpContext.Request;
            var resolved = url.Content(normalized);
            return resolved.StartsWith('/')
                ? $"{request.Scheme}://{request.Host}{resolved}"
                : resolved;
        }

        public static string AbsoluteAlbumImage(this IUrlHelper url, Album album)
            => url.AbsoluteContent(Seo.AlbumSocialImagePath(album));

        /// <summary>The absolute URL of the current request, without the query string.</summary>
        public static string CanonicalUrl(this IUrlHelper url)
        {
            var request = url.ActionContext.HttpContext.Request;
            return $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
        }
    }
}
