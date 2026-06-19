namespace MvcMusicStore.Services;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Azurite / account connection string. When set it takes precedence (used for local development).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Blob service endpoint (e.g. https://account.blob.core.windows.net). Used with managed identity in Azure.
    /// </summary>
    public string? BlobEndpoint { get; set; }

    public string ThumbnailsContainer { get; set; } = "thumbnails";

    public string MusicContainer { get; set; } = "music";
}
