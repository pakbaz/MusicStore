namespace MvcMusicStore.Services;

public sealed class AlbumMetadataEnrichmentOptions
{
    public const string SectionName = "AlbumMetadataEnrichment";

    public bool Enabled { get; set; } = true;
    public int StartupDelaySeconds { get; set; } = 5;
    public int IntervalHours { get; set; } = 24;
    public int BatchSize { get; set; } = 25;
    public int RequestDelayMilliseconds { get; set; } = 1200;
}
