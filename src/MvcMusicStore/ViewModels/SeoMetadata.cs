namespace MvcMusicStore.ViewModels
{
    /// <summary>
    /// Per-page SEO and social metadata. Views populate <c>ViewData["Seo"]</c> with an instance
    /// and the shared <c>_SeoMeta</c> head partial renders title, description, canonical, Open Graph
    /// and Twitter Card tags from it (falling back to site-wide defaults when unset).
    /// </summary>
    public class SeoMetadata
    {
        /// <summary>Page title without the site-name suffix.</summary>
        public string? Title { get; set; }

        public string? Description { get; set; }

        /// <summary>Absolute canonical URL. Defaults to the current request URL when null.</summary>
        public string? CanonicalUrl { get; set; }

        /// <summary>Absolute image URL used for Open Graph / Twitter Card previews.</summary>
        public string? ImageUrl { get; set; }

        public string? ImageAlt { get; set; }

        /// <summary>Open Graph object type (e.g. <c>website</c>, <c>product</c>, <c>profile</c>).</summary>
        public string OgType { get; set; } = "website";

        public decimal? Price { get; set; }

        public string? Currency { get; set; }

        /// <summary>schema.org availability URL used for the <c>product:availability</c> tag.</summary>
        public string? Availability { get; set; }
    }
}
