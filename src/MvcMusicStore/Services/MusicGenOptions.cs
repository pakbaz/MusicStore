namespace MvcMusicStore.Services;

public class MusicGenOptions
{
    public const string SectionName = "MusicGen";

    /// <summary>
    /// Base URL of the ACE-Step music generation service (separate container app).
    /// </summary>
    public string? BaseUrl { get; set; }

    public int DefaultDurationSeconds { get; set; } = 15;

    public int TimeoutSeconds { get; set; } = 600;
}
