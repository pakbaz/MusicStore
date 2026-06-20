using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class StoreManagerController : Controller
    {
        private readonly MusicStoreEntities db;
        private readonly IAlbumArtworkService albumArtworkService;
        private readonly IThumbnailCacheService thumbnailCacheService;
        private readonly IWebHostEnvironment environment;
        private readonly ILogger<StoreManagerController> logger;

        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".webp"
        };

        public StoreManagerController(
            MusicStoreEntities storeDb,
            IAlbumArtworkService albumArtworkService,
            IThumbnailCacheService thumbnailCacheService,
            IWebHostEnvironment environment,
            ILogger<StoreManagerController> logger)
        {
            db = storeDb;
            this.albumArtworkService = albumArtworkService;
            this.thumbnailCacheService = thumbnailCacheService;
            this.environment = environment;
            this.logger = logger;
        }

        //
        // GET: /StoreManager/

        public async Task<IActionResult> Index()
        {
            var albums = await db.Albums.ToListAsync();
            albums.PopulateNavigation();
            return View(albums.OrderBy(a => a.Price).ToList());
        }

        //
        // GET: /StoreManager/Details/5

        public async Task<IActionResult> Details(int id = 0)
        {
            Album? album = await db.Albums.SingleOrDefaultAsync(a => a.AlbumId == id);
            if (album == null)
            {
                return NotFound();
            }
            album.PopulateNavigation();
            return View(album);
        }

        //
        // GET: /StoreManager/Create

        public async Task<IActionResult> Create()
        {
            await PopulateSelectListsAsync();
            return View();
        }

        //
        // POST: /StoreManager/Create

        [HttpPost]
        public async Task<IActionResult> Create(Album album, IFormFile? thumbnailFile, CancellationToken cancellationToken)
        {
            ValidateThumbnailFile(thumbnailFile);

            if (ModelState.IsValid)
            {
                if (thumbnailFile is { Length: > 0 })
                {
                    album.UploadedThumbnailUrl = await SaveUploadedThumbnailAsync(thumbnailFile, cancellationToken);
                }
                else if (Album.IsPlaceholderThumbnailUrl(album.AlbumArtUrl))
                {
                    album.MetadataThumbnailUrl = await TryFetchMetadataThumbnailAsync(album.ArtistId, album.Title, cancellationToken);
                }

                album.AlbumId = await db.NextAlbumIdAsync(cancellationToken);
                await ApplyDenormalizedNamesAsync(album, cancellationToken);

                db.Albums.Add(album);
                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction("Index");
            }

            await PopulateSelectListsAsync(album.GenreId, album.ArtistId);
            return View(album);
        }

        //
        // GET: /StoreManager/Edit/5

        public async Task<IActionResult> Edit(int id = 0)
        {
            Album? album = await db.Albums.SingleOrDefaultAsync(a => a.AlbumId == id);
            if (album == null)
            {
                return NotFound();
            }
            await PopulateSelectListsAsync(album.GenreId, album.ArtistId);
            return View(album);
        }

        //
        // POST: /StoreManager/Edit/5

        [HttpPost]
        public async Task<IActionResult> Edit(int id, Album album, IFormFile? thumbnailFile, CancellationToken cancellationToken)
        {
            if (id != album.AlbumId)
            {
                return BadRequest();
            }

            var existingAlbum = await db.Albums.SingleOrDefaultAsync(a => a.AlbumId == id);
            if (existingAlbum == null)
            {
                return NotFound();
            }

            ValidateThumbnailFile(thumbnailFile);

            if (ModelState.IsValid)
            {
                existingAlbum.GenreId = album.GenreId;
                existingAlbum.ArtistId = album.ArtistId;
                existingAlbum.Title = album.Title;
                existingAlbum.Price = album.Price;
                existingAlbum.AlbumArtUrl = album.AlbumArtUrl;

                if (thumbnailFile is { Length: > 0 })
                {
                    existingAlbum.UploadedThumbnailUrl = await SaveUploadedThumbnailAsync(thumbnailFile, cancellationToken);
                }
                else if (string.IsNullOrWhiteSpace(existingAlbum.UploadedThumbnailUrl) &&
                         string.IsNullOrWhiteSpace(existingAlbum.MetadataThumbnailUrl) &&
                         Album.IsPlaceholderThumbnailUrl(existingAlbum.AlbumArtUrl))
                {
                    existingAlbum.MetadataThumbnailUrl = await TryFetchMetadataThumbnailAsync(existingAlbum.ArtistId, existingAlbum.Title, cancellationToken);
                }

                await ApplyDenormalizedNamesAsync(existingAlbum, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction("Index");
            }

            await PopulateSelectListsAsync(album.GenreId, album.ArtistId);
            return View(album);
        }

        //
        // GET: /StoreManager/Delete/5

        public async Task<IActionResult> Delete(int id = 0)
        {
            Album? album = await db.Albums.SingleOrDefaultAsync(a => a.AlbumId == id);
            if (album == null)
            {
                return NotFound();
            }
            album.PopulateNavigation();
            return View(album);
        }

        //
        // POST: /StoreManager/Delete/5

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
        {
            Album? album = await db.Albums.SingleOrDefaultAsync(a => a.AlbumId == id, cancellationToken);
            if (album != null)
            {
                db.Albums.Remove(album);
                await db.SaveChangesAsync(cancellationToken);
            }
            return RedirectToAction("Index");
        }

        //
        // GET: /StoreManager/Reviews

        public async Task<IActionResult> Reviews(string? filter, int page = 1, CancellationToken cancellationToken = default)
        {
            const int pageSize = 20;
            var normalizedFilter = ReviewModerationFilters.Normalize(filter);

            var reviews = await db.Reviews.ToListAsync(cancellationToken);
            var reportedCount = reviews.Count(r => r.IsReported && !r.IsHidden);

            IEnumerable<Review> filtered = normalizedFilter switch
            {
                ReviewModerationFilters.Reported => reviews.Where(r => r.IsReported),
                ReviewModerationFilters.Hidden => reviews.Where(r => r.IsHidden),
                _ => reviews
            };

            var ordered = filtered
                // Surface reported, still-visible reviews first so admins can act on them quickly.
                .OrderByDescending(r => r.IsReported && !r.IsHidden)
                .ThenByDescending(r => r.UpdatedDate ?? r.CreatedDate)
                .ToList();

            var totalResults = ordered.Count;
            var totalPages = totalResults == 0 ? 1 : (int)Math.Ceiling(totalResults / (double)pageSize);
            var currentPage = Math.Clamp(page, 1, totalPages);

            var items = ordered
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReviewModerationItemViewModel
                {
                    ReviewId = r.ReviewId,
                    AlbumId = r.AlbumId,
                    AlbumTitle = string.IsNullOrWhiteSpace(r.AlbumTitle) ? $"Album #{r.AlbumId}" : r.AlbumTitle!,
                    Author = string.IsNullOrWhiteSpace(r.Username) ? "Anonymous" : r.Username!,
                    Rating = r.Rating,
                    Body = r.Body,
                    CreatedDate = r.CreatedDate,
                    UpdatedDate = r.UpdatedDate,
                    IsHidden = r.IsHidden,
                    IsReported = r.IsReported,
                    ReportReason = r.ReportReason
                })
                .ToList();

            return View(new ReviewModerationViewModel
            {
                Filter = normalizedFilter,
                Page = currentPage,
                TotalPages = totalPages,
                TotalResults = totalResults,
                ReportedCount = reportedCount,
                Reviews = items
            });
        }

        //
        // POST: /StoreManager/HideReview/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HideReview(int id, string? filter, int page = 1, CancellationToken cancellationToken = default)
        {
            await SetReviewVisibilityAsync(id, hidden: true, cancellationToken);
            return RedirectToAction(nameof(Reviews), new { filter, page });
        }

        //
        // POST: /StoreManager/UnhideReview/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnhideReview(int id, string? filter, int page = 1, CancellationToken cancellationToken = default)
        {
            await SetReviewVisibilityAsync(id, hidden: false, cancellationToken);
            return RedirectToAction(nameof(Reviews), new { filter, page });
        }

        //
        // POST: /StoreManager/DeleteReview/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReview(int id, string? filter, int page = 1, CancellationToken cancellationToken = default)
        {
            var review = await db.Reviews.SingleOrDefaultAsync(r => r.ReviewId == id, cancellationToken);
            if (review != null)
            {
                db.Reviews.Remove(review);
                await db.SaveChangesAsync(cancellationToken);
            }
            return RedirectToAction(nameof(Reviews), new { filter, page });
        }

        private async Task SetReviewVisibilityAsync(int id, bool hidden, CancellationToken cancellationToken)
        {
            var review = await db.Reviews.SingleOrDefaultAsync(r => r.ReviewId == id, cancellationToken);
            if (review == null)
            {
                return;
            }

            review.IsHidden = hidden;
            if (!hidden)
            {
                // Clearing the report keeps an un-hidden review out of the "needs attention" queue.
                review.IsReported = false;
                review.ReportReason = null;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        private async Task PopulateSelectListsAsync(int? genreId = null, int? artistId = null)
        {
            var genres = (await db.Genres.ToListAsync()).OrderBy(g => g.Name).ToList();
            var artists = (await db.Artists.ToListAsync()).OrderBy(a => a.Name).ToList();
            ViewBag.GenreId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(genres, "GenreId", "Name", genreId);
            ViewBag.ArtistId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(artists, "ArtistId", "Name", artistId);
        }

        private async Task ApplyDenormalizedNamesAsync(Album album, CancellationToken cancellationToken)
        {
            var genre = await db.Genres.SingleOrDefaultAsync(g => g.GenreId == album.GenreId, cancellationToken);
            var artist = await db.Artists.SingleOrDefaultAsync(a => a.ArtistId == album.ArtistId, cancellationToken);
            album.GenreName = genre?.Name;
            album.ArtistName = artist?.Name;
        }

        private static string ResolveImageExtension(IFormFile thumbnailFile)
        {
            var extension = Path.GetExtension(thumbnailFile.FileName);
            return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
        }

        private void ValidateThumbnailFile(IFormFile? thumbnailFile)
        {
            if (thumbnailFile is null || thumbnailFile.Length == 0)
            {
                return;
            }

            var extension = ResolveImageExtension(thumbnailFile);
            if (!AllowedImageExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(thumbnailFile), "Upload a valid image file (.png, .jpg, .jpeg, .gif, .webp).");
            }

            if (!thumbnailFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(thumbnailFile), "Upload a valid image content type.");
            }
        }

        private async Task<string> SaveUploadedThumbnailAsync(IFormFile thumbnailFile, CancellationToken cancellationToken)
        {
            var extension = ResolveImageExtension(thumbnailFile);
            var uploadsDirectory = Path.Combine(environment.ContentRootPath, "Images", "Uploads");
            Directory.CreateDirectory(uploadsDirectory);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var outputPath = Path.Combine(uploadsDirectory, fileName);

            await using var outputStream = System.IO.File.Create(outputPath);
            await thumbnailFile.CopyToAsync(outputStream, cancellationToken);

            return $"~/Images/Uploads/{fileName}";
        }

        private async Task<string?> TryFetchMetadataThumbnailAsync(int artistId, string? albumTitle, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(albumTitle))
            {
                return null;
            }

            var artistName = await db.Artists
                .Where(artist => artist.ArtistId == artistId)
                .Select(artist => artist.Name)
                .SingleOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(artistName))
            {
                return null;
            }

            try
            {
                var thumbnailUrl = await albumArtworkService.TryGetThumbnailUrlAsync(artistName, albumTitle, cancellationToken);
                return await thumbnailCacheService.TryCacheThumbnailAsync(thumbnailUrl, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Unable to fetch metadata artwork for album '{AlbumTitle}' by '{ArtistName}'.", albumTitle, artistName);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                logger.LogWarning(ex, "Timed out while fetching metadata artwork for album '{AlbumTitle}' by '{ArtistName}'.", albumTitle, artistName);
                return null;
            }
        }
    }
}
