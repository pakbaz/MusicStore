using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
        private readonly BlobServiceClient blobServiceClient;
        private readonly StorageOptions storageOptions;
        private readonly ILogger<StoreManagerController> logger;

        private const int PageSize = 20;

        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".webp"
        };

        private static readonly HashSet<string> AllowedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3",
            ".m4a",
            ".aac",
            ".ogg",
            ".oga",
            ".wav"
        };

        private static readonly Dictionary<string, string> AudioContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".mp3"] = "audio/mpeg",
            [".m4a"] = "audio/mp4",
            [".aac"] = "audio/aac",
            [".ogg"] = "audio/ogg",
            [".oga"] = "audio/ogg",
            [".wav"] = "audio/wav"
        };

        private const long MaxPreviewAudioBytes = 10 * 1024 * 1024;

        public StoreManagerController(
            MusicStoreEntities storeDb,
            IAlbumArtworkService albumArtworkService,
            IThumbnailCacheService thumbnailCacheService,
            IWebHostEnvironment environment,
            BlobServiceClient blobServiceClient,
            IOptions<StorageOptions> storageOptions,
            ILogger<StoreManagerController> logger)
        {
            db = storeDb;
            this.albumArtworkService = albumArtworkService;
            this.thumbnailCacheService = thumbnailCacheService;
            this.environment = environment;
            this.blobServiceClient = blobServiceClient;
            this.storageOptions = storageOptions.Value;
            this.logger = logger;
        }

        //
        // GET: /StoreManager/

        public async Task<IActionResult> Index(int page = 1)
        {
            if (page < 1)
            {
                page = 1;
            }

            var totalResults = await db.Albums.CountAsync();
            var totalPages = (int)Math.Ceiling(totalResults / (double)PageSize);
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            // Order and page in the query so the admin list does not materialize the whole
            // Albums container. Cosmos default indexing supports single-property ORDER BY.
            var albums = await db.Albums
                .OrderBy(a => a.Price)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
            albums.PopulateNavigation();

            var viewModel = new StoreManagerIndexViewModel
            {
                Albums = albums,
                Page = page,
                PageSize = PageSize,
                TotalResults = totalResults
            };

            return View(viewModel);
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
        public async Task<IActionResult> Create(Album album, IFormFile? thumbnailFile, IFormFile? previewAudioFile, CancellationToken cancellationToken)
        {
            ValidateThumbnailFile(thumbnailFile);
            ValidatePreviewAudioFile(previewAudioFile);

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

                if (previewAudioFile is { Length: > 0 })
                {
                    album.PreviewUrl = await SavePreviewAudioAsync(previewAudioFile, cancellationToken);
                }

                album.PreviewDurationSeconds = NormalizePreviewDuration(album.PreviewDurationSeconds);

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
        public async Task<IActionResult> Edit(int id, Album album, IFormFile? thumbnailFile, IFormFile? previewAudioFile, CancellationToken cancellationToken)
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
            ValidatePreviewAudioFile(previewAudioFile);

            if (ModelState.IsValid)
            {
                existingAlbum.GenreId = album.GenreId;
                existingAlbum.ArtistId = album.ArtistId;
                existingAlbum.Title = album.Title;
                existingAlbum.Price = album.Price;
                existingAlbum.AlbumArtUrl = album.AlbumArtUrl;
                existingAlbum.PreviewDurationSeconds = NormalizePreviewDuration(album.PreviewDurationSeconds);

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

                if (previewAudioFile is { Length: > 0 })
                {
                    existingAlbum.PreviewUrl = await SavePreviewAudioAsync(previewAudioFile, cancellationToken);
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

        private static int NormalizePreviewDuration(int durationSeconds)
        {
            return durationSeconds <= 0
                ? Album.DefaultPreviewDurationSeconds
                : Math.Clamp(durationSeconds, 5, 60);
        }

        private static string ResolveAudioExtension(IFormFile audioFile)
        {
            var extension = Path.GetExtension(audioFile.FileName);
            return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
        }

        private void ValidatePreviewAudioFile(IFormFile? previewAudioFile)
        {
            if (previewAudioFile is null || previewAudioFile.Length == 0)
            {
                return;
            }

            if (previewAudioFile.Length > MaxPreviewAudioBytes)
            {
                ModelState.AddModelError(nameof(previewAudioFile), "Preview audio must be 10 MB or smaller.");
            }

            var extension = ResolveAudioExtension(previewAudioFile);
            if (!AllowedAudioExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(previewAudioFile), "Upload a valid audio file (.mp3, .m4a, .aac, .ogg, .wav).");
            }

            if (!previewAudioFile.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(previewAudioFile), "Upload a valid audio content type.");
            }
        }

        private async Task<string> SavePreviewAudioAsync(IFormFile previewAudioFile, CancellationToken cancellationToken)
        {
            var extension = ResolveAudioExtension(previewAudioFile);
            var contentType = AudioContentTypes.TryGetValue(extension, out var resolved) ? resolved : "audio/mpeg";

            var containerClient = blobServiceClient.GetBlobContainerClient(storageOptions.MusicContainer);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            var blobName = $"{Guid.NewGuid():N}{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            await using var stream = previewAudioFile.OpenReadStream();
            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
                cancellationToken);

            return "/media/music/" + blobName;
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
