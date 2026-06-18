using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MvcMusicStore.Controllers
{
    public class StoreController : Controller
    {
        private readonly MusicStoreEntities storeDB;

        public StoreController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        //
        // GET: /Store/

        public IActionResult Index(string? search, string? genre, decimal? minPrice, decimal? maxPrice, string? availability, string? sort)
        {
            var normalizedSort = CatalogSortOptions.Normalize(sort);
            var normalizedAvailability = CatalogAvailabilityOptions.Normalize(availability);

            IQueryable<CatalogAlbumItemViewModel> query = storeDB.Albums
                .Select(album => new CatalogAlbumItemViewModel
                {
                    AlbumId = album.AlbumId,
                    Title = album.Title ?? string.Empty,
                    ArtistName = album.Artist!.Name ?? string.Empty,
                    GenreName = album.Genre!.Name ?? string.Empty,
                    Price = album.Price,
                    AlbumArtUrl = string.IsNullOrEmpty(album.AlbumArtUrl) ? "~/Images/placeholder.png" : album.AlbumArtUrl,
                    ReleaseDate = album.ReleaseDate,
                    IsAvailable = album.IsAvailable,
                    Popularity = album.OrderDetails!.Sum(orderDetail => (int?)orderDetail.Quantity) ?? 0
                });

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(album =>
                    album.Title.Contains(term) ||
                    album.ArtistName.Contains(term) ||
                    album.GenreName.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(genre))
            {
                query = query.Where(album => album.GenreName == genre);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(album => album.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(album => album.Price <= maxPrice.Value);
            }

            if (normalizedAvailability == CatalogAvailabilityOptions.Available)
            {
                query = query.Where(album => album.IsAvailable);
            }
            else if (normalizedAvailability == CatalogAvailabilityOptions.Unavailable)
            {
                query = query.Where(album => !album.IsAvailable);
            }

            query = normalizedSort switch
            {
                CatalogSortOptions.ReleaseDateDesc => query
                    .OrderByDescending(album => album.ReleaseDate ?? DateTime.MinValue)
                    .ThenBy(album => album.Title),
                CatalogSortOptions.ReleaseDateAsc => query
                    .OrderBy(album => album.ReleaseDate ?? DateTime.MaxValue)
                    .ThenBy(album => album.Title),
                CatalogSortOptions.PriceAsc => query
                    .OrderBy(album => album.Price)
                    .ThenBy(album => album.Title),
                CatalogSortOptions.PriceDesc => query
                    .OrderByDescending(album => album.Price)
                    .ThenBy(album => album.Title),
                _ => query
                    .OrderByDescending(album => album.Popularity)
                    .ThenBy(album => album.Title)
            };

            var catalogModel = new CatalogIndexViewModel
            {
                Search = search,
                Genre = genre,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Availability = normalizedAvailability,
                Sort = normalizedSort,
                Genres = storeDB.Genres
                    .OrderBy(item => item.Name)
                    .Select(item => item.Name ?? string.Empty)
                    .ToList(),
                Albums = query.ToList()
            };
            catalogModel.TotalResults = catalogModel.Albums.Count;

            return View(catalogModel);
        }


        //
        // GET: /Store/Browse?genre=Disco

        public IActionResult Browse(string? genre)
        {
            if (string.IsNullOrWhiteSpace(genre))
            {
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index), new { genre });
        }

        public IActionResult Details(int id)
        {
            var album = storeDB.Albums
                .Include(item => item.Genre)
                .Include(item => item.Artist)
                .SingleOrDefault(item => item.AlbumId == id);

            if (album == null)
            {
                return NotFound();
            }

            return View(album);
        }
    }
}