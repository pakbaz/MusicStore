using MvcMusicStore.Models;

namespace MvcMusicStore.ViewModels
{
    public class HomeIndexViewModel
    {
        public List<Album> FeaturedReleases { get; set; } = new();
        public List<Album> TrendingReleases { get; set; } = new();
        public List<Album> CuratedReleases { get; set; } = new();
    }
}
