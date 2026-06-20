using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class StoreController : Controller
    {
        private const int CatalogPageSize = 12;
        private const int RelatedAlbumCount = 4;
        private const int RelatedArtistCandidatePoolSize = 24;
        private const int RelatedArtistAlbumCount = 6;

        private readonly MusicStoreEntities storeDB;

        public StoreController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        //
        // GET: /Store/

        public async Task<IActionResult> Index(string? search, string? genre, decimal? minPrice, decimal? maxPrice, string? availability, string? sort, int page = 1)
        {
            var normalizedSort = CatalogSortOptions.Normalize(sort);
            var normalizedAvailability = CatalogAvailabilityOptions.Normalize(availability);

            if (page < 1)
            {
                page = 1;
            }

            // Build the catalog query so Cosmos performs filtering, sorting, and paging server-side
            // instead of materializing the whole Albums container on every request.
            IQueryable<Album> filtered = storeDB.Albums;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                filtered = filtered.Where(album =>
                    (album.Title != null && album.Title.ToLower().Contains(term)) ||
                    (album.ArtistName != null && album.ArtistName.ToLower().Contains(term)) ||
                    (album.GenreName != null && album.GenreName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(genre))
            {
                filtered = filtered.Where(album => album.GenreName == genre);
            }

            if (minPrice.HasValue)
            {
                filtered = filtered.Where(album => album.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                filtered = filtered.Where(album => album.Price <= maxPrice.Value);
            }

            if (normalizedAvailability == CatalogAvailabilityOptions.Available)
            {
                filtered = filtered.Where(album => album.IsAvailable);
            }
            else if (normalizedAvailability == CatalogAvailabilityOptions.Unavailable)
            {
                filtered = filtered.Where(album => !album.IsAvailable);
            }

            var totalResults = await filtered.CountAsync();

            var totalPages = (int)Math.Ceiling(totalResults / (double)CatalogPageSize);
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            // Cosmos default indexing can only serve single-property ORDER BY, so sort on one key.
            IQueryable<Album> ordered = normalizedSort switch
            {
                CatalogSortOptions.ReleaseDateDesc => filtered.OrderByDescending(album => album.ReleaseDate),
                CatalogSortOptions.ReleaseDateAsc => filtered.OrderBy(album => album.ReleaseDate),
                CatalogSortOptions.PriceAsc => filtered.OrderBy(album => album.Price),
                CatalogSortOptions.PriceDesc => filtered.OrderByDescending(album => album.Price),
                _ => filtered.OrderByDescending(album => album.Popularity)
            };

            var pageItems = await ordered
                .Skip((page - 1) * CatalogPageSize)
                .Take(CatalogPageSize)
                .Select(album => new
                {
                    album.AlbumId,
                    album.Title,
                    album.ArtistName,
                    album.GenreName,
                    album.Price,
                    album.ReleaseDate,
                    album.IsAvailable,
                    album.Popularity,
                    album.UploadedThumbnailUrl,
                    album.MetadataThumbnailUrl,
                    album.AlbumArtUrl
                })
                .ToListAsync();

            var albumList = pageItems.Select(album => new CatalogAlbumItemViewModel
            {
                AlbumId = album.AlbumId,
                Title = album.Title ?? string.Empty,
                ArtistName = album.ArtistName ?? string.Empty,
                GenreName = album.GenreName ?? string.Empty,
                Price = album.Price,
                ReleaseDate = album.ReleaseDate,
                IsAvailable = album.IsAvailable,
                Popularity = album.Popularity,
                AlbumArtUrl = new Album
                {
                    UploadedThumbnailUrl = album.UploadedThumbnailUrl,
                    MetadataThumbnailUrl = album.MetadataThumbnailUrl,
                    AlbumArtUrl = album.AlbumArtUrl
                }.GetDisplayThumbnailUrl()
            }).ToList();

            // Genre filter options come from the small Genres container rather than a distinct scan
            // over the entire Albums container.
            var genreNames = (await storeDB.Genres
                    .Select(g => g.Name)
                    .ToListAsync())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            var catalogModel = new CatalogIndexViewModel
            {
                Search = search,
                Genre = genre,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Availability = normalizedAvailability,
                Sort = normalizedSort,
                Genres = genreNames,
                Albums = albumList,
                TotalResults = totalResults,
                Page = page,
                PageSize = CatalogPageSize
            };

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

        public async Task<IActionResult> Details(int id)
        {
            var album = await storeDB.Albums.SingleOrDefaultAsync(a => a.AlbumId == id);

            if (album == null)
            {
                return NotFound();
            }

            album.PopulateNavigation();

            var relatedByGenre = await storeDB.Albums
                .Where(a => a.AlbumId != id && a.GenreId == album.GenreId)
                .OrderBy(a => a.Title)
                .Take(RelatedAlbumCount)
                .ToListAsync();
            relatedByGenre.PopulateNavigation();

            var moreFromArtist = await storeDB.Albums
                .Where(a => a.AlbumId != id && a.ArtistId == album.ArtistId)
                .OrderBy(a => a.Title)
                .Take(RelatedAlbumCount)
                .ToListAsync();
            moreFromArtist.PopulateNavigation();

            var viewModel = new AlbumDetailsViewModel
            {
                Album = album,
                RelatedByGenre = relatedByGenre,
                MoreFromArtist = moreFromArtist
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Artist(int id)
        {
            var artist = await storeDB.Artists.FirstOrDefaultAsync(a => a.ArtistId == id);

            if (artist == null)
            {
                return NotFound();
            }

            var artistAlbums = await storeDB.Albums
                .Where(a => a.ArtistId == id)
                .OrderBy(a => a.Title)
                .ToListAsync();
            artistAlbums.PopulateNavigation();

            var artistAlbumIds = artistAlbums.Select(a => a.AlbumId).ToHashSet();
            var genreIds = artistAlbums.Select(a => a.GenreId).Distinct().ToList();

            var relatedAlbums = new List<Album>();
            if (genreIds.Count > 0)
            {
                // Pull a bounded candidate pool ordered by a single key, then finish the
                // featured-first / title ordering in memory to stay within Cosmos' single-key ORDER BY.
                var candidates = await storeDB.Albums
                    .Where(a => a.ArtistId != id && genreIds.Contains(a.GenreId))
                    .OrderByDescending(a => a.IsFeatured)
                    .Take(RelatedArtistCandidatePoolSize)
                    .ToListAsync();

                relatedAlbums = candidates
                    .Where(a => !artistAlbumIds.Contains(a.AlbumId))
                    .OrderByDescending(a => a.IsFeatured)
                    .ThenBy(a => a.Title)
                    .Take(RelatedArtistAlbumCount)
                    .ToList();
                relatedAlbums.PopulateNavigation();
            }

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
