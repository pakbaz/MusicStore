using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    [Authorize]
    public class ReviewsController : Controller
    {
        private readonly MusicStoreEntities db;

        public ReviewsController(MusicStoreEntities storeDb)
        {
            db = storeDb;
        }

        //
        // POST: /Reviews/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateReviewViewModel model, CancellationToken cancellationToken)
        {
            var album = await db.Albums.SingleOrDefaultAsync(a => a.AlbumId == model.AlbumId, cancellationToken);
            if (album == null)
            {
                return NotFound();
            }

            var username = User.Identity!.Name!;

            // Verified-purchaser gate: reviews are limited to shoppers who bought the album.
            if (!await db.HasPurchasedAlbumAsync(username, model.AlbumId, cancellationToken))
            {
                TempData["ReviewError"] = "Only verified purchasers can review this album.";
                return RedirectToReviews(model.AlbumId);
            }

            if (model.Rating < Review.MinRating || model.Rating > Review.MaxRating)
            {
                TempData["ReviewError"] = "Choose a star rating from 1 to 5 before submitting your review.";
                return RedirectToReviews(model.AlbumId);
            }

            var body = string.IsNullOrWhiteSpace(model.Body) ? null : model.Body.Trim();

            // One review per shopper per album: re-submitting edits the existing review so the
            // average rating stays accurate.
            var albumReviews = await db.Reviews
                .Where(r => r.AlbumId == model.AlbumId)
                .ToListAsync(cancellationToken);
            var existing = albumReviews.FirstOrDefault(r =>
                string.Equals(r.Username, username, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Rating = model.Rating;
                existing.Body = body;
                existing.AlbumTitle = album.Title;
                existing.UpdatedDate = DateTime.UtcNow;
                TempData["ReviewMessage"] = "Your review was updated.";
            }
            else
            {
                db.Reviews.Add(new Review
                {
                    ReviewId = await db.NextReviewIdAsync(cancellationToken),
                    AlbumId = model.AlbumId,
                    AlbumTitle = album.Title,
                    Username = username,
                    Rating = model.Rating,
                    Body = body,
                    CreatedDate = DateTime.UtcNow
                });
                TempData["ReviewMessage"] = "Thanks! Your review was posted.";
            }

            await db.SaveChangesAsync(cancellationToken);
            return RedirectToReviews(model.AlbumId);
        }

        //
        // POST: /Reviews/Report

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report(int id, int albumId, string? reason, CancellationToken cancellationToken)
        {
            var review = await db.Reviews.SingleOrDefaultAsync(r => r.ReviewId == id, cancellationToken);
            if (review == null)
            {
                return NotFound();
            }

            if (!review.IsReported)
            {
                review.IsReported = true;
                review.ReportReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
                await db.SaveChangesAsync(cancellationToken);
            }

            TempData["ReviewMessage"] = "Thanks for flagging this review. Our team will take a look.";
            return RedirectToReviews(albumId);
        }

        private IActionResult RedirectToReviews(int albumId) =>
            RedirectToAction("Details", "Store", new { id = albumId }, "reviews");
    }
}
