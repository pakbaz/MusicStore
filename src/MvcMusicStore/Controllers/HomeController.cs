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
        private const int SectionSize = 6;

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
            // Each homepage section is a small, bounded query so the page never materializes the
            // whole Albums container or scans every Order to rank popularity.
            var featuredAlbums = await storeDB.Albums
                .Where(a => a.IsFeatured)
                .OrderBy(a => a.Title)
                .Take(SectionSize)
                .ToListAsync();

            if (featuredAlbums.Count < SectionSize)
            {
                var fillCount = SectionSize - featuredAlbums.Count;
                var fillAlbums = await storeDB.Albums
                    .Where(a => !a.IsFeatured)
                    .OrderByDescending(a => a.AlbumId)
                    .Take(fillCount)
                    .ToListAsync();
                featuredAlbums.AddRange(fillAlbums);
            }
            featuredAlbums.PopulateNavigation();

            var trendingAlbums = await storeDB.Albums
                .OrderByDescending(a => a.Popularity)
                .Take(SectionSize)
                .ToListAsync();
            trendingAlbums.PopulateNavigation();

            var featuredIds = featuredAlbums.Select(a => a.AlbumId).ToList();
            var curatedAlbums = await storeDB.Albums
                .Where(a => !featuredIds.Contains(a.AlbumId))
                .OrderByDescending(a => a.AlbumId)
                .Take(SectionSize)
                .ToListAsync();
            curatedAlbums.PopulateNavigation();

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
    }
}
