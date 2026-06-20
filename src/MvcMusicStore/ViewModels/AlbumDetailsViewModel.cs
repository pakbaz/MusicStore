using MvcMusicStore.Models;
using MvcMusicStore.Services;

namespace MvcMusicStore.ViewModels
{
    public class AlbumDetailsViewModel
    {
        public required Album Album { get; set; }
        public AlbumPricing? Pricing { get; set; }
        public List<Album> RelatedByGenre { get; set; } = new();
        public List<Album> MoreFromArtist { get; set; } = new();
        public IReadOnlyDictionary<int, AlbumPricing> RelatedPricing { get; set; } =
            new Dictionary<int, AlbumPricing>();
    }
}
