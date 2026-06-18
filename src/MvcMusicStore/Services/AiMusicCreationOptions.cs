namespace MvcMusicStore.Services
{
    public class AiMusicCreationOptions
    {
        public const string SectionName = "AiMusicCreation";

        public decimal DefaultPrice { get; set; } = 8.99M;

        public string PlaceholderAlbumArtUrl { get; set; } = "~/Images/placeholder.png";
    }
}
