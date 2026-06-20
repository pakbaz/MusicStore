using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly MusicStoreEntities storeDB;
        private readonly IPromotionService promotions;

        public HomeController(MusicStoreEntities storeDb, IPromotionService promotions)
        {
            storeDB = storeDb;
            this.promotions = promotions;
        }

        //
        // GET: /Home/

        public async Task<IActionResult> Index()
        {
            const int sectionSize = 6;

            var albums = await storeDB.Albums.ToListAsync();
            albums.PopulateNavigation();
            var salesByAlbum = await GetSalesByAlbumAsync();

            var featuredAlbums = GetFeaturedAlbums(albums, sectionSize);
            var trendingAlbums = GetTopSellingAlbums(albums, salesByAlbum, sectionSize);
            var featuredIds = featuredAlbums.Select(a => a.AlbumId).ToHashSet();
            var curatedAlbums = GetNewReleases(albums, sectionSize, featuredIds);

            var shownAlbums = featuredAlbums
                .Concat(trendingAlbums)
                .Concat(curatedAlbums)
                .GroupBy(a => a.AlbumId)
                .Select(g => g.First());
            var pricing = await promotions.GetPricingLookupAsync(shownAlbums);
            var deal = await promotions.GetFeaturedDealAsync();

            var viewModel = new HomeIndexViewModel
            {
                FeaturedReleases = featuredAlbums,
                TrendingReleases = trendingAlbums,
                CuratedReleases = curatedAlbums,
                Pricing = pricing,
                DealOfTheDay = deal
            };

            return View(viewModel);
        }

        private static List<Album> GetFeaturedAlbums(List<Album> albums, int count)
        {
            var featuredAlbums = albums
                .Where(a => a.IsFeatured)
                .OrderBy(a => a.Title)
                .Take(count)
                .ToList();

            if (featuredAlbums.Count >= count)
            {
                return featuredAlbums;
            }

            var featuredIds = featuredAlbums.Select(a => a.AlbumId).ToHashSet();
            featuredAlbums.AddRange(albums
                .Where(a => !featuredIds.Contains(a.AlbumId))
                .OrderByDescending(a => a.AlbumId)
                .Take(count - featuredAlbums.Count));

            return featuredAlbums;
        }

        private static List<Album> GetTopSellingAlbums(List<Album> albums, IReadOnlyDictionary<int, int> salesByAlbum, int count)
        {
            return albums
                .OrderByDescending(a => salesByAlbum.TryGetValue(a.AlbumId, out var sold) ? sold : 0)
                .ThenByDescending(a => a.AlbumId)
                .Take(count)
                .ToList();
        }

        private static List<Album> GetNewReleases(List<Album> albums, int count, HashSet<int> excludeAlbumIds)
        {
            return albums
                .Where(a => !excludeAlbumIds.Contains(a.AlbumId))
                .OrderByDescending(a => a.AlbumId)
                .Take(count)
                .ToList();
        }

        private async Task<Dictionary<int, int>> GetSalesByAlbumAsync()
        {
            var orders = await storeDB.Orders.ToListAsync();
            return orders
                .SelectMany(order => order.OrderDetails ?? new List<OrderDetail>())
                .GroupBy(detail => detail.AlbumId)
                .ToDictionary(group => group.Key, group => group.Sum(detail => detail.Quantity));
        }
    }
}
