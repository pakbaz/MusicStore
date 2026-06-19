using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

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
                    AlbumArtUrl = !string.IsNullOrEmpty(album.UploadedThumbnailUrl) && album.UploadedThumbnailUrl != Album.DefaultPlaceholderThumbnailUrl && album.UploadedThumbnailUrl != "~/Images/placeholder.png" && album.UploadedThumbnailUrl != "/Images/placeholder.png"
                        ? album.UploadedThumbnailUrl
                        : !string.IsNullOrEmpty(album.MetadataThumbnailUrl) && album.MetadataThumbnailUrl != Album.DefaultPlaceholderThumbnailUrl && album.MetadataThumbnailUrl != "~/Images/placeholder.png" && album.MetadataThumbnailUrl != "/Images/placeholder.png"
                            ? album.MetadataThumbnailUrl
                            : !string.IsNullOrEmpty(album.AlbumArtUrl) && album.AlbumArtUrl != Album.DefaultPlaceholderThumbnailUrl && album.AlbumArtUrl != "~/Images/placeholder.png" && album.AlbumArtUrl != "/Images/placeholder.png"
                                ? album.AlbumArtUrl
                                : Album.DefaultPlaceholderThumbnailUrl,
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

            foreach (var album in catalogModel.Albums)
            {
                album.AlbumArtUrl = Album.NormalizeThumbnailUrl(album.AlbumArtUrl);
            }

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
                .Include(a => a.Genre)
                .Include(a => a.Artist)
                .SingleOrDefault(a => a.AlbumId == id);

            if (album == null)
            {
                return NotFound();
            }

            var relatedByGenre = storeDB.Albums
                .Include(a => a.Artist)
                .Where(a => a.AlbumId != id && a.GenreId == album.GenreId)
                .OrderBy(a => a.Title)
                .Take(4)
                .ToList();

            var moreFromArtist = storeDB.Albums
                .Include(a => a.Genre)
                .Where(a => a.AlbumId != id && a.ArtistId == album.ArtistId)
                .OrderBy(a => a.Title)
                .Take(4)
                .ToList();

            var viewModel = new AlbumDetailsViewModel
            {
                Album = album,
                RelatedByGenre = relatedByGenre,
                MoreFromArtist = moreFromArtist
            };

            return View(viewModel);
        }

        public IActionResult Artist(int id)
        {
            var artist = storeDB.Artists.SingleOrDefault(a => a.ArtistId == id);

            if (artist == null)
            {
                return NotFound();
            }

            var artistAlbums = storeDB.Albums
                .Include(a => a.Genre)
                .Where(a => a.ArtistId == id)
                .OrderBy(a => a.Title)
                .ToList();

            var artistAlbumIds = artistAlbums.Select(a => a.AlbumId).ToHashSet();
            var genreIds = artistAlbums.Select(a => a.GenreId).Distinct().ToList();

            var relatedAlbums = storeDB.Albums
                .Include(a => a.Artist)
                .Where(a => !artistAlbumIds.Contains(a.AlbumId) && genreIds.Contains(a.GenreId))
                .OrderByDescending(a => a.IsFeatured)
                .ThenBy(a => a.Title)
                .Take(6)
                .ToList();

            var viewModel = new ArtistDetailsViewModel
            {
                Artist = artist,
                Albums = artistAlbums,
                RelatedAlbums = relatedAlbums
            };

            return View(viewModel);
        }
    }
}