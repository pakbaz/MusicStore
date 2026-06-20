namespace MvcMusicStore.ViewModels
{
    public class AudioPreviewViewModel
    {
        public required string PreviewUrl { get; init; }

        public int DurationSeconds { get; init; } = Models.Album.DefaultPreviewDurationSeconds;

        public string Title { get; init; } = string.Empty;

        // "detail" for the album page, "card" for the compact catalog/card variant.
        public string Variant { get; init; } = "detail";
    }
}
