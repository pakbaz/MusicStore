using System.Globalization;
using System.Text;
using MvcMusicStore.Models;

namespace MvcMusicStore.Infrastructure
{
    /// <summary>
    /// Pure SEO helpers: slug generation, text truncation, meta-description builders, and selection
    /// of a social-preview image path. URL-to-absolute resolution lives in <see cref="SeoUrlExtensions"/>.
    /// </summary>
    public static class Seo
    {
        public const string SiteName = "MVC Music Store";

        public const string DefaultDescription =
            "MVC Music Store is a sample online music shop — browse and buy albums across genres, " +
            "preview tracks, and generate original AI music.";

        /// <summary>Site-wide social/OG image. A real promotional PNG (SVGs make poor previews).</summary>
        public const string DefaultSocialImagePath = "~/Images/home-showcase.png";

        public const string LogoPath = "~/Images/logo.png";

        /// <summary>Builds a lowercase, hyphenated, ASCII slug suitable for clean URLs.</summary>
        public static string Slug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "untitled";
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (ch < 128 && char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToLowerInvariant(ch));
                }
                else if (char.IsWhiteSpace(ch) || ch is '-' or '_' or '/' or '.' or '&' or '+')
                {
                    builder.Append('-');
                }
            }

            var slug = builder.ToString();
            while (slug.Contains("--", StringComparison.Ordinal))
            {
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            }

            slug = slug.Trim('-');
            return slug.Length == 0 ? "untitled" : slug;
        }

        /// <summary>Truncates text to <paramref name="maxLength"/>, preferring a word boundary.</summary>
        public static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            if (value.Length <= maxLength)
            {
                return value;
            }

            var cut = value[..maxLength];
            var lastSpace = cut.LastIndexOf(' ');
            if (lastSpace > maxLength * 0.6)
            {
                cut = cut[..lastSpace];
            }

            return cut.TrimEnd() + "…";
        }

        /// <summary>Returns the album's display artwork, falling back to the site image for SVG placeholders.</summary>
        public static string AlbumSocialImagePath(Album album)
        {
            var image = album.GetDisplayThumbnailUrl();
            if (string.IsNullOrWhiteSpace(image) || image.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultSocialImagePath;
            }

            return image;
        }

        public static string AlbumDescription(Album album)
        {
            var artist = album.Artist?.Name ?? album.ArtistName;
            var genre = album.Genre?.Name ?? album.GenreName;

            var lead = $"Buy “{album.Title}”";
            if (!string.IsNullOrWhiteSpace(artist))
            {
                lead += $" by {artist}";
            }

            lead += $" on {SiteName} for {album.Price.ToString("C", CultureInfo.CurrentCulture)}.";

            var tail = string.IsNullOrWhiteSpace(genre)
                ? " Preview tracks and add it to your cart."
                : $" {genre} album — preview tracks and add it to your cart.";

            return Truncate(lead + tail, 300);
        }

        public static string ArtistDescription(string? name, int albumCount, IEnumerable<string?> genres)
        {
            var safeName = string.IsNullOrWhiteSpace(name) ? "this artist" : name;

            var genreList = genres
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Select(g => g!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            var genreText = genreList.Count > 0 ? $" across {string.Join(", ", genreList)}" : string.Empty;
            var albumText = albumCount == 1 ? "1 album" : $"{albumCount} albums";

            return Truncate(
                $"Explore {albumText} by {safeName}{genreText} on {SiteName}. " +
                "Discover the discography, preview tracks, and add favorites to your cart.",
                300);
        }
    }
}
