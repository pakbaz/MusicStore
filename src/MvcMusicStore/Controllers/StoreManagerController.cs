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
            var albums = db.Albums.Include(a => a.Genre).Include(a => a.Artist)
                .OrderBy(a => a.Price);
            return View(await albums.ToListAsync());
        }

        //
        // GET: /StoreManager/Details/5

        public async Task<IActionResult> Details(int id = 0)
        {
            Album? album = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Genre)
                .SingleOrDefaultAsync(a => a.AlbumId == id);
            if (album == null)
            {
                return NotFound();
            }
            return View(album);
        }

        //
        // GET: /StoreManager/Create

        public IActionResult Create()
        {
            ViewBag.GenreId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Genres, "GenreId", "Name");
            ViewBag.ArtistId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Artists, "ArtistId", "Name");
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

                db.Albums.Add(album);
                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction("Index");
            }

            ViewBag.GenreId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Genres, "GenreId", "Name", album.GenreId);
            ViewBag.ArtistId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Artists, "ArtistId", "Name", album.ArtistId);
            return View(album);
        }

        //
        // GET: /StoreManager/Edit/5

        public async Task<IActionResult> Edit(int id = 0)
        {
            Album? album = await db.Albums.FindAsync(id);
            if (album == null)
            {
                return NotFound();
            }
            ViewBag.GenreId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Genres, "GenreId", "Name", album.GenreId);
            ViewBag.ArtistId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Artists, "ArtistId", "Name", album.ArtistId);
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

            var existingAlbum = await db.Albums.FindAsync(id);
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

                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction("Index");
            }

            ViewBag.GenreId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Genres, "GenreId", "Name", album.GenreId);
            ViewBag.ArtistId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Artists, "ArtistId", "Name", album.ArtistId);
            return View(album);
        }

        //
        // GET: /StoreManager/Delete/5

        public async Task<IActionResult> Delete(int id = 0)
        {
            Album? album = await db.Albums
                .Include(a => a.Artist)
                .Include(a => a.Genre)
                .SingleOrDefaultAsync(a => a.AlbumId == id);
            if (album == null)
            {
                return NotFound();
            }
            return View(album);
        }

        //
        // POST: /StoreManager/Delete/5

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
        {
            Album? album = await db.Albums.FindAsync(id);
            if (album != null)
            {
                db.Albums.Remove(album);
                await db.SaveChangesAsync(cancellationToken);
            }
            return RedirectToAction("Index");
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