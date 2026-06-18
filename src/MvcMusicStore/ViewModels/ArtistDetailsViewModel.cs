using MvcMusicStore.Models;

namespace MvcMusicStore.ViewModels
{
    public class ArtistDetailsViewModel
    {
        public required Artist Artist { get; set; }
        public List<Album> Albums { get; set; } = new();
        public List<Album> RelatedAlbums { get; set; } = new();
    }
}
