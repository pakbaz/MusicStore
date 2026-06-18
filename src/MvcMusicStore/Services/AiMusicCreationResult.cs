namespace MvcMusicStore.Services
{
    public class AiMusicCreationResult
    {
        public required string Title { get; init; }

        public required string ArtistName { get; init; }

        public required string AlbumArtUrl { get; init; }

        public required string OriginalityStatement { get; init; }

        public decimal SuggestedPrice { get; init; }
    }
}
