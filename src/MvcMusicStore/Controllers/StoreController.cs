using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;
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
        private readonly IRecommendationService recommendationService;
        private readonly IBundleService bundleService;

        public StoreController(
            MusicStoreEntities storeDb,
            IRecommendationService recommendationService,
            IBundleService bundleService)
        {
            storeDB = storeDb;
            this.recommendationService = recommendationService;
            this.bundleService = bundleService;
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

            // Visible review aggregates are computed in memory because Cosmos cannot translate GROUP BY.
            var reviewStats = await GetReviewStatsAsync();

            List<CatalogAlbumItemViewModel> albumList;

            if (normalizedSort == CatalogSortOptions.RatingDesc)
            {
                // Ratings live in a separate Cosmos container and cannot drive the Albums ORDER BY,
                // so the "Top rated" sort materializes the filtered albums and orders them in memory.
                albumList = (await filtered
                        .Select(album => new CatalogRow
                        {
                            AlbumId = album.AlbumId,
                            Title = album.Title,
                            ArtistName = album.ArtistName,
                            GenreName = album.GenreName,
                            Price = album.Price,
                            ReleaseDate = album.ReleaseDate,
                            IsAvailable = album.IsAvailable,
                            Popularity = album.Popularity,
                            PreviewUrl = album.PreviewUrl,
                            PreviewDurationSeconds = album.PreviewDurationSeconds,
                            UploadedThumbnailUrl = album.UploadedThumbnailUrl,
                            MetadataThumbnailUrl = album.MetadataThumbnailUrl,
                            AlbumArtUrl = album.AlbumArtUrl
                        })
                        .ToListAsync())
                    .Select(row => ToCatalogItem(row, reviewStats))
                    .OrderByDescending(item => item.AverageRating)
                    .ThenByDescending(item => item.ReviewCount)
                    .ThenBy(item => item.Title)
                    .Skip((page - 1) * CatalogPageSize)
                    .Take(CatalogPageSize)
                    .ToList();
            }
            else
            {
                // Cosmos default indexing can only serve single-property ORDER BY, so sort on one key.
                IQueryable<Album> ordered = normalizedSort switch
                {
                    CatalogSortOptions.ReleaseDateDesc => filtered.OrderByDescending(album => album.ReleaseDate),
                    CatalogSortOptions.ReleaseDateAsc => filtered.OrderBy(album => album.ReleaseDate),
                    CatalogSortOptions.PriceAsc => filtered.OrderBy(album => album.Price),
                    CatalogSortOptions.PriceDesc => filtered.OrderByDescending(album => album.Price),
                    _ => filtered.OrderByDescending(album => album.Popularity)
                };

                albumList = (await ordered
                        .Skip((page - 1) * CatalogPageSize)
                        .Take(CatalogPageSize)
                        .Select(album => new CatalogRow
                        {
                            AlbumId = album.AlbumId,
                            Title = album.Title,
                            ArtistName = album.ArtistName,
                            GenreName = album.GenreName,
                            Price = album.Price,
                            ReleaseDate = album.ReleaseDate,
                            IsAvailable = album.IsAvailable,
                            Popularity = album.Popularity,
                            PreviewUrl = album.PreviewUrl,
                            PreviewDurationSeconds = album.PreviewDurationSeconds,
                            UploadedThumbnailUrl = album.UploadedThumbnailUrl,
                            MetadataThumbnailUrl = album.MetadataThumbnailUrl,
                            AlbumArtUrl = album.AlbumArtUrl
                        })
                        .ToListAsync())
                    .Select(row => ToCatalogItem(row, reviewStats))
                    .ToList();
            }

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

        public async Task<IActionResult> Details(int id, int reviewsPage = 1)
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

            var alsoBought = (await recommendationService.GetAlsoBoughtAsync(id)).ToList();
            var bundles = (await bundleService.GetBundlesContainingAlbumAsync(id)).ToList();
            var reviews = await BuildAlbumReviewsAsync(album, reviewsPage);

            var viewModel = new AlbumDetailsViewModel
            {
                Album = album,
                RelatedByGenre = relatedByGenre,
                MoreFromArtist = moreFromArtist,
                AlsoBought = alsoBought,
                Bundles = bundles,
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

        private static CatalogAlbumItemViewModel ToCatalogItem(CatalogRow row, IReadOnlyDictionary<int, ReviewStats> reviewStats)
        {
            var hasStats = reviewStats.TryGetValue(row.AlbumId, out var stats);
            return new CatalogAlbumItemViewModel
            {
                AlbumId = row.AlbumId,
                Title = row.Title ?? string.Empty,
                ArtistName = row.ArtistName ?? string.Empty,
                GenreName = row.GenreName ?? string.Empty,
                Price = row.Price,
                ReleaseDate = row.ReleaseDate,
                IsAvailable = row.IsAvailable,
                Popularity = row.Popularity,
                AverageRating = hasStats ? stats.AverageRating : 0d,
                ReviewCount = hasStats ? stats.ReviewCount : 0,
                PreviewUrl = row.PreviewUrl,
                PreviewDurationSeconds = row.PreviewDurationSeconds,
                AlbumArtUrl = new Album
                {
                    UploadedThumbnailUrl = row.UploadedThumbnailUrl,
                    MetadataThumbnailUrl = row.MetadataThumbnailUrl,
                    AlbumArtUrl = row.AlbumArtUrl
                }.GetDisplayThumbnailUrl()
            };
        }

        private sealed class CatalogRow
        {
            public int AlbumId { get; set; }
            public string? Title { get; set; }
            public string? ArtistName { get; set; }
            public string? GenreName { get; set; }
            public decimal Price { get; set; }
            public DateTime? ReleaseDate { get; set; }
            public bool IsAvailable { get; set; }
            public int Popularity { get; set; }
            public string? PreviewUrl { get; set; }
            public int PreviewDurationSeconds { get; set; }
            public string? UploadedThumbnailUrl { get; set; }
            public string? MetadataThumbnailUrl { get; set; }
            public string? AlbumArtUrl { get; set; }
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
    }
}
