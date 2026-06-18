using MvcMusicStore.Models;

namespace MvcMusicStore.ViewModels
{
    public class AlbumDetailsViewModel
    {
        public required Album Album { get; set; }
        public List<Album> RelatedByGenre { get; set; } = new();
        public List<Album> MoreFromArtist { get; set; } = new();
    }
}
