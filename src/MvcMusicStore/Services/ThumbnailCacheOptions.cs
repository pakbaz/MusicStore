namespace MvcMusicStore.Services;

public sealed class ThumbnailCacheOptions
{
    public const string SectionName = "ThumbnailCache";

    public string RelativeDirectory { get; set; } = "Images/MetadataCache";
    public long MaxBytes { get; set; } = 5 * 1024 * 1024;
}
