namespace MvcMusicStore.ViewModels
{
    public class AiMusicGenerationResultViewModel
    {
        public required int AlbumId { get; init; }

        public required string Genre { get; init; }

        public required string StyleDirection { get; init; }

        public required string Mood { get; init; }

        public required string Instrumentation { get; init; }

        public int TempoBpm { get; init; }

        public required string Title { get; init; }

        public required string ArtistName { get; init; }

        public required decimal SuggestedPrice { get; init; }

        public required string OriginalityStatement { get; init; }

        public string? AudioUrl { get; init; }

        public int DurationSeconds { get; init; }
    }
}
