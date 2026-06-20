using System.Collections.Generic;
using MvcMusicStore.Models;
using MvcMusicStore.Services;

namespace MvcMusicStore.ViewModels
{
    public class HomeIndexViewModel
    {
        public List<Album> FeaturedReleases { get; set; } = new();
        public List<Album> TrendingReleases { get; set; } = new();
        public List<Album> CuratedReleases { get; set; } = new();

        public DealOfTheDay? DealOfTheDay { get; set; }
        public IReadOnlyDictionary<int, AlbumPricing> Pricing { get; set; } =
            new Dictionary<int, AlbumPricing>();
    }
}
