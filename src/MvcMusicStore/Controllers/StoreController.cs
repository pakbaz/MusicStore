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

        public async Task<IActionResult> Index(string? search, string? genre, decimal? minPrice, decimal? maxPrice, string? availability, string? sort)
        {
            var normalizedSort = CatalogSortOptions.Normalize(sort);
            var normalizedAvailability = CatalogAvailabilityOptions.Normalize(availability);

            var albums = await storeDB.Albums.ToListAsync();
            var salesByAlbum = await GetSalesByAlbumAsync();
            var reviewStats = await GetReviewStatsAsync();

            IEnumerable<CatalogAlbumItemViewModel> items = albums.Select(album => new CatalogAlbumItemViewModel
            {
                AlbumId = album.AlbumId,
                Title = album.Title ?? string.Empty,
                ArtistName = album.ArtistName ?? string.Empty,
                GenreName = album.GenreName ?? string.Empty,
                Price = album.Price,
                AlbumArtUrl = album.GetDisplayThumbnailUrl(),
                ReleaseDate = album.ReleaseDate,
                IsAvailable = album.IsAvailable,
                Popularity = salesByAlbum.TryGetValue(album.AlbumId, out var sold) ? sold : 0,
                AverageRating = reviewStats.TryGetValue(album.AlbumId, out var stats) ? stats.AverageRating : 0d,
                ReviewCount = reviewStats.TryGetValue(album.AlbumId, out var ratingStats) ? ratingStats.ReviewCount : 0,
                PreviewUrl = album.PreviewUrl,
                PreviewDurationSeconds = album.PreviewDurationSeconds
            });

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                items = items.Where(album =>
                    Contains(album.Title, term) ||
                    Contains(album.ArtistName, term) ||
                    Contains(album.GenreName, term));
            }

            if (!string.IsNullOrWhiteSpace(genre))
            {
                items = items.Where(album => string.Equals(album.GenreName, genre, StringComparison.OrdinalIgnoreCase));
            }

            if (minPrice.HasValue)
            {
                items = items.Where(album => album.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                items = items.Where(album => album.Price <= maxPrice.Value);
            }

            if (normalizedAvailability == CatalogAvailabilityOptions.Available)
            {
                items = items.Where(album => album.IsAvailable);
            }
            else if (normalizedAvailability == CatalogAvailabilityOptions.Unavailable)
            {
                items = items.Where(album => !album.IsAvailable);
            }

            items = normalizedSort switch
            {
                CatalogSortOptions.RatingDesc => items
                    .OrderByDescending(album => album.AverageRating)
                    .ThenByDescending(album => album.ReviewCount)
                    .ThenBy(album => album.Title),
                CatalogSortOptions.ReleaseDateDesc => items
                    .OrderByDescending(album => album.ReleaseDate ?? DateTime.MinValue)
                    .ThenBy(album => album.Title),
                CatalogSortOptions.ReleaseDateAsc => items
                    .OrderBy(album => album.ReleaseDate ?? DateTime.MaxValue)
                    .ThenBy(album => album.Title),
                CatalogSortOptions.PriceAsc => items
                    .OrderBy(album => album.Price)
                    .ThenBy(album => album.Title),
                CatalogSortOptions.PriceDesc => items
                    .OrderByDescending(album => album.Price)
                    .ThenBy(album => album.Title),
                _ => items
                    .OrderByDescending(album => album.Popularity)
                    .ThenBy(album => album.Title)
            };

            var albumList = items.ToList();
            foreach (var album in albumList)
            {
                album.AlbumArtUrl = Album.NormalizeThumbnailUrl(album.AlbumArtUrl);
            }

            var genreNames = albums
                .Select(album => album.GenreName ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
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
                TotalResults = albumList.Count
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

        public async Task<IActionResult> Details(int id, int reviewsPage = 1)
        {
            var albums = await storeDB.Albums.ToListAsync();
            var album = albums.FirstOrDefault(a => a.AlbumId == id);

            if (album == null)
            {
                return NotFound();
            }

            album.PopulateNavigation();

            var relatedByGenre = albums
                .Where(a => a.AlbumId != id && a.GenreId == album.GenreId)
                .OrderBy(a => a.Title)
                .Take(4)
                .ToList();
            relatedByGenre.PopulateNavigation();

            var moreFromArtist = albums
                .Where(a => a.AlbumId != id && a.ArtistId == album.ArtistId)
                .OrderBy(a => a.Title)
                .Take(4)
                .ToList();
            moreFromArtist.PopulateNavigation();

            var reviews = await BuildAlbumReviewsAsync(album, reviewsPage);

            var viewModel = new AlbumDetailsViewModel
            {
                Album = album,
                RelatedByGenre = relatedByGenre,
                MoreFromArtist = moreFromArtist,
                Reviews = reviews
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

            var albums = await storeDB.Albums.ToListAsync();

            var artistAlbums = albums
                .Where(a => a.ArtistId == id)
                .OrderBy(a => a.Title)
                .ToList();
            artistAlbums.PopulateNavigation();

            var artistAlbumIds = artistAlbums.Select(a => a.AlbumId).ToHashSet();
            var genreIds = artistAlbums.Select(a => a.GenreId).Distinct().ToHashSet();

            var relatedAlbums = albums
                .Where(a => !artistAlbumIds.Contains(a.AlbumId) && genreIds.Contains(a.GenreId))
                .OrderByDescending(a => a.IsFeatured)
                .ThenBy(a => a.Title)
                .Take(6)
                .ToList();
            relatedAlbums.PopulateNavigation();

            var viewModel = new ArtistDetailsViewModel
            {
                Artist = artist,
                Albums = artistAlbums,
                RelatedAlbums = relatedAlbums
            };

            return View(viewModel);
        }

        private async Task<Dictionary<int, int>> GetSalesByAlbumAsync()
        {
            var orders = await storeDB.Orders.ToListAsync();
            return orders
                .SelectMany(order => order.OrderDetails ?? new List<OrderDetail>())
                .GroupBy(detail => detail.AlbumId)
                .ToDictionary(group => group.Key, group => group.Sum(detail => detail.Quantity));
        }

        // Cosmos cannot translate GROUP BY aggregates, so visible reviews are materialized and
        // averaged in memory (matching the popularity calculation above).
        private async Task<Dictionary<int, ReviewStats>> GetReviewStatsAsync()
        {
            var reviews = await storeDB.Reviews
                .Where(review => !review.IsHidden)
                .ToListAsync();

            return reviews
                .GroupBy(review => review.AlbumId)
                .ToDictionary(
                    group => group.Key,
                    group => new ReviewStats(group.Average(review => review.Rating), group.Count()));
        }

        private async Task<AlbumReviewsViewModel> BuildAlbumReviewsAsync(Album album, int reviewsPage)
        {
            const int pageSize = 5;

            var albumReviews = await storeDB.Reviews
                .Where(review => review.AlbumId == album.AlbumId && !review.IsHidden)
                .ToListAsync();

            var ordered = albumReviews
                .OrderByDescending(review => review.UpdatedDate ?? review.CreatedDate)
                .ToList();

            var count = ordered.Count;
            var average = count == 0 ? 0d : ordered.Average(review => review.Rating);
            var totalPages = count == 0 ? 1 : (int)Math.Ceiling(count / (double)pageSize);
            var page = Math.Clamp(reviewsPage, 1, totalPages);

            var pageItems = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToListItem)
                .ToList();

            var username = User.Identity?.Name;
            var isAuthenticated = User.Identity?.IsAuthenticated == true;

            var myReview = string.IsNullOrWhiteSpace(username)
                ? null
                : albumReviews.FirstOrDefault(review =>
                    string.Equals(review.Username, username, StringComparison.OrdinalIgnoreCase));

            var hasPurchased = isAuthenticated &&
                await storeDB.HasPurchasedAlbumAsync(username, album.AlbumId);

            return new AlbumReviewsViewModel
            {
                AlbumId = album.AlbumId,
                AlbumTitle = album.Title ?? string.Empty,
                Summary = new StarRatingViewModel { Average = average, Count = count },
                Reviews = pageItems,
                Page = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                IsAuthenticated = isAuthenticated,
                HasPurchased = hasPurchased,
                MyReview = myReview is null ? null : ToListItem(myReview),
                Form = new CreateReviewViewModel
                {
                    AlbumId = album.AlbumId,
                    Rating = myReview?.Rating ?? 0,
                    Body = myReview?.Body
                }
            };
        }

        private static ReviewListItemViewModel ToListItem(Review review) => new()
        {
            ReviewId = review.ReviewId,
            AlbumId = review.AlbumId,
            Author = string.IsNullOrWhiteSpace(review.Username) ? "Anonymous" : review.Username!,
            Rating = review.Rating,
            Body = review.Body,
            CreatedDate = review.CreatedDate,
            WasEdited = review.UpdatedDate.HasValue,
            IsReported = review.IsReported
        };

        private static bool Contains(string source, string term)
        {
            return source.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
