using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;

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
        private readonly IPaymentService paymentService;

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
            ILogger<StoreManagerController> logger,
            IPaymentService paymentService)
        {
            db = storeDb;
            this.albumArtworkService = albumArtworkService;
            this.thumbnailCacheService = thumbnailCacheService;
            this.environment = environment;
            this.logger = logger;
            this.paymentService = paymentService;
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

        //
        // GET: /StoreManager/Orders

        public async Task<IActionResult> Orders()
        {
            var orders = await db.Orders.ToListAsync();
            return View(orders.OrderByDescending(o => o.OrderDate).ToList());
        }

        //
        // POST: /StoreManager/RefundOrder

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefundOrder(int id)
        {
            var order = (await db.Orders.Where(o => o.OrderId == id).Take(1).ToListAsync()).FirstOrDefault();
            if (order == null)
            {
                return NotFound();
            }

            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                TempData["OrderError"] = $"Order {id} can't be refunded because it isn't in a paid state.";
                return RedirectToAction(nameof(Orders));
            }

            // Only call the provider for real (Stripe) payments; promo/FREE orders are reversed locally.
            if (string.Equals(order.PaymentProvider, "Stripe", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(order.PaymentIntentId))
            {
                var result = await paymentService.RefundAsync(order.PaymentIntentId);
                if (!result.Success)
                {
                    TempData["OrderError"] = $"Refund failed for order {id}: {result.Error}";
                    return RedirectToAction(nameof(Orders));
                }
            }

            order.PaymentStatus = PaymentStatus.Refunded;
            await db.SaveChangesAsync();

            TempData["OrderMessage"] = $"Order {id} has been refunded.";
            return RedirectToAction(nameof(Orders));
        }
    }
}
