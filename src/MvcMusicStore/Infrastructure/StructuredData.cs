using System.Globalization;
using System.Text.Json;
using MvcMusicStore.Models;

namespace MvcMusicStore.Infrastructure
{
    /// <summary>
    /// Builds schema.org JSON-LD documents for embedding in <c>&lt;script type="application/ld+json"&gt;</c>
    /// blocks. Serialized with the default <see cref="JsonSerializer"/> encoder, which escapes
    /// <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c> so untrusted catalog text cannot break out of the script tag.
    /// </summary>
    public static class StructuredData
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = false
        };

        /// <summary>
        /// Serializes <paramref name="value"/> and wraps it in a ready-to-embed
        /// <c>&lt;script type="application/ld+json"&gt;</c> element. The wrapper is built here (rather than
        /// as literal markup in the view) so Razor does not HTML-encode the <c>+</c> in the MIME type.
        /// The default JSON encoder escapes <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c>, so the body is safe
        /// to embed verbatim via <c>@Html.Raw</c>.
        /// </summary>
        private static string BuildJsonLdScript(object value) =>
            "<script type=\"application/ld+json\">" +
            JsonSerializer.Serialize(value, Options) +
            "</script>";

        /// <summary>
        /// A combined <c>Product</c> + <c>MusicAlbum</c> node with a price/availability offer.
        /// No <c>aggregateRating</c> is emitted because the catalog has no review data, and fabricated
        /// ratings fail Rich Results validation and violate search-engine policy.
        /// </summary>
        public static string MusicAlbum(
            Album album,
            string url,
            string imageUrl,
            string? genreName,
            string? artistName,
            string artistUrl)
        {
            var node = new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = new[] { "Product", "MusicAlbum" },
                ["name"] = album.Title,
                ["url"] = url
            };

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                node["image"] = imageUrl;
            }

            if (!string.IsNullOrWhiteSpace(genreName))
            {
                node["genre"] = genreName;
            }

            if (album.ReleaseDate.HasValue)
            {
                node["datePublished"] = album.ReleaseDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(artistName))
            {
                node["byArtist"] = new Dictionary<string, object?>
                {
                    ["@type"] = "MusicGroup",
                    ["name"] = artistName,
                    ["url"] = artistUrl
                };
            }

            node["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["price"] = album.Price.ToString("0.00", CultureInfo.InvariantCulture),
                ["priceCurrency"] = "USD",
                ["availability"] = album.IsAvailable
                    ? "https://schema.org/InStock"
                    : "https://schema.org/OutOfStock",
                ["url"] = url
            };

            return BuildJsonLdScript(node);
        }

        public static string MusicGroup(
            string? name,
            string url,
            string? imageUrl,
            IEnumerable<(string Name, string Url, string? Image)> albums)
        {
            var albumNodes = albums
                .Select(a =>
                {
                    var albumNode = new Dictionary<string, object?>
                    {
                        ["@type"] = "MusicAlbum",
                        ["name"] = a.Name,
                        ["url"] = a.Url
                    };
                    if (!string.IsNullOrWhiteSpace(a.Image))
                    {
                        albumNode["image"] = a.Image;
                    }

                    return albumNode;
                })
                .ToList();

            var node = new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "MusicGroup",
                ["name"] = name,
                ["url"] = url
            };

            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                node["image"] = imageUrl;
            }

            if (albumNodes.Count > 0)
            {
                node["album"] = albumNodes;
            }

            return BuildJsonLdScript(node);
        }

        /// <summary>A <c>WebSite</c> (with catalog search action) + <c>Organization</c> graph for the home page.</summary>
        public static string HomeGraph(string siteUrl, string searchUrlTemplate, string logoUrl)
        {
            var website = new Dictionary<string, object?>
            {
                ["@type"] = "WebSite",
                ["name"] = Seo.SiteName,
                ["url"] = siteUrl,
                ["potentialAction"] = new Dictionary<string, object?>
                {
                    ["@type"] = "SearchAction",
                    ["target"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "EntryPoint",
                        ["urlTemplate"] = searchUrlTemplate
                    },
                    ["query-input"] = "required name=search_term_string"
                }
            };

            var organization = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = Seo.SiteName,
                ["url"] = siteUrl,
                ["logo"] = logoUrl
            };

            var graph = new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@graph"] = new object[] { website, organization }
            };

            return BuildJsonLdScript(graph);
        }
    }
}
