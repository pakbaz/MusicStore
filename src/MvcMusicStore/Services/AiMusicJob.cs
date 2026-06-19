namespace MvcMusicStore.Services
{
    public enum AiMusicJobStatus
    {
        Pending,
        Running,
        Succeeded,
        Failed
    }

    /// <summary>
    /// Tracks the lifecycle of a single asynchronous AI music generation request so the
    /// browser can poll for progress instead of holding a multi-minute HTTP request open.
    /// </summary>
    public class AiMusicJob
    {
        public required string Id { get; init; }

        public AiMusicJobStatus Status { get; set; } = AiMusicJobStatus.Pending;

        public string Prompt { get; init; } = string.Empty;

        public int DurationSeconds { get; init; }

        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? CompletedAt { get; set; }

        public string? Error { get; set; }

        public int? AlbumId { get; set; }

        public string? Title { get; set; }

        public string? ArtistName { get; set; }

        public string? Genre { get; set; }

        public string? AudioUrl { get; set; }

        public int ResultDurationSeconds { get; set; }

        public decimal SuggestedPrice { get; set; }

        public string? OriginalityStatement { get; set; }
    }
}
