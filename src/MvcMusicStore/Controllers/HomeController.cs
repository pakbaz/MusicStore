using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly MusicStoreEntities storeDB;

        public HomeController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        //
        // GET: /Home/

        public IActionResult Index()
        {
            const int sectionSize = 6;
            var featuredAlbums = GetFeaturedAlbums(sectionSize);
            var trendingAlbums = GetTopSellingAlbums(sectionSize);
            var curatedAlbums = GetNewReleases(sectionSize, featuredAlbums.Select(a => a.AlbumId));

            var viewModel = new HomeIndexViewModel
            {
                FeaturedReleases = featuredAlbums,
                TrendingReleases = trendingAlbums,
                CuratedReleases = curatedAlbums
            };

            return View(viewModel);
        }

        private List<Album> GetFeaturedAlbums(int count)
        {
            var featuredAlbums = storeDB.Albums
                .Include(a => a.Artist)
                .Where(a => a.IsFeatured)
                .OrderBy(a => a.Title)
                .Take(count)
                .ToList();

            if (featuredAlbums.Count >= count)
            {
                return featuredAlbums;
            }

            var featuredIds = featuredAlbums.Select(a => a.AlbumId).ToHashSet();
            var fallbackAlbums = storeDB.Albums
                .Include(a => a.Artist)
                .Where(a => !featuredIds.Contains(a.AlbumId))
                .OrderByDescending(a => a.AlbumId)
                .Take(count - featuredAlbums.Count)
                .ToList();

            featuredAlbums.AddRange(fallbackAlbums);
            return featuredAlbums;
        }

        private List<Album> GetTopSellingAlbums(int count)
        {
            var topSellingAlbums = storeDB.OrderDetails
                .GroupBy(od => od.AlbumId)
                .Select(g => new { AlbumId = g.Key, TotalSold = g.Sum(od => od.Quantity) })
                .OrderByDescending(x => x.TotalSold)
                .Take(count)
                .Join(
                    storeDB.Albums.Include(a => a.Artist),
                    x => x.AlbumId,
                    a => a.AlbumId,
                    (x, a) => a)
                .ToList();

            if (topSellingAlbums.Count >= count)
            {
                return topSellingAlbums;
            }

            var topSellingIds = topSellingAlbums.Select(a => a.AlbumId).ToHashSet();
            var fallbackAlbums = storeDB.Albums
                .Include(a => a.Artist)
                .Where(a => !topSellingIds.Contains(a.AlbumId))
                .OrderByDescending(a => a.AlbumId)
                .Take(count - topSellingAlbums.Count)
                .ToList();

            topSellingAlbums.AddRange(fallbackAlbums);
            return topSellingAlbums;
        }

        private List<Album> GetNewReleases(int count, IEnumerable<int> excludeAlbumIds)
        {
            var excludedIds = excludeAlbumIds.ToHashSet();

            return storeDB.Albums
                .Include(a => a.Artist)
                .Where(a => !excludedIds.Contains(a.AlbumId))
                .OrderByDescending(a => a.AlbumId)
                .Take(count)
                .ToList();
        }
    }
}