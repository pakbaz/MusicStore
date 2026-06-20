using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.ViewModels
{
    /// <summary>
    /// Aggregated rating data for a single album, computed in memory from the Reviews container
    /// (Cosmos cannot translate GROUP BY aggregates, mirroring the existing popularity calculation).
    /// </summary>
    public readonly record struct ReviewStats(double AverageRating, int ReviewCount)
    {
        public static readonly ReviewStats Empty = new(0d, 0);
    }

    public class StarRatingViewModel
    {
        public double Average { get; set; }
        public int Count { get; set; }

        /// <summary>Render the count label and "no ratings" messaging when false.</summary>
        public bool ShowCount { get; set; } = true;

        public StarRatingViewModel() { }

        public StarRatingViewModel(ReviewStats stats)
        {
            Average = stats.AverageRating;
            Count = stats.ReviewCount;
        }

        public int FilledStars => (int)Math.Round(Average, MidpointRounding.AwayFromZero);
    }

    public class ReviewListItemViewModel
    {
        public int ReviewId { get; set; }
        public int AlbumId { get; set; }
        public string Author { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Body { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool WasEdited { get; set; }
        public bool IsReported { get; set; }
    }

    public class CreateReviewViewModel
    {
        public int AlbumId { get; set; }

        [Range(1, 5, ErrorMessage = "Choose a rating from 1 to 5 stars.")]
        public int Rating { get; set; }

        [DisplayName("Your review")]
        [StringLength(2000)]
        [DataType(DataType.MultilineText)]
        public string? Body { get; set; }
    }

    /// <summary>
    /// Everything the album details page needs to render the ratings summary, the paginated review
    /// list, and the contextual review form / gating message.
    /// </summary>
    public class AlbumReviewsViewModel
    {
        public int AlbumId { get; set; }
        public string AlbumTitle { get; set; } = string.Empty;

        public StarRatingViewModel Summary { get; set; } = new();

        public List<ReviewListItemViewModel> Reviews { get; set; } = new();

        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public bool HasReviews => Summary.Count > 0;

        /// <summary>Visitor is signed in (so we know whether to prompt sign-in vs purchase).</summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>Signed-in visitor has an order containing this album.</summary>
        public bool HasPurchased { get; set; }

        /// <summary>Signed-in visitor can submit or edit a review (authenticated + purchased).</summary>
        public bool CanReview => IsAuthenticated && HasPurchased;

        /// <summary>The signed-in visitor's existing review, if they have already left one.</summary>
        public ReviewListItemViewModel? MyReview { get; set; }

        public CreateReviewViewModel Form { get; set; } = new();
    }

    public static class ReviewModerationFilters
    {
        public const string All = "all";
        public const string Reported = "reported";
        public const string Hidden = "hidden";

        public static string Normalize(string? filter)
        {
            return filter switch
            {
                Reported => Reported,
                Hidden => Hidden,
                _ => All
            };
        }
    }

    public class ReviewModerationItemViewModel
    {
        public int ReviewId { get; set; }
        public int AlbumId { get; set; }
        public string AlbumTitle { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Body { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public bool IsHidden { get; set; }
        public bool IsReported { get; set; }
        public string? ReportReason { get; set; }
    }

    public class ReviewModerationViewModel
    {
        public string Filter { get; set; } = ReviewModerationFilters.All;
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalResults { get; set; }
        public int ReportedCount { get; set; }
        public List<ReviewModerationItemViewModel> Reviews { get; set; } = new();
    }
}
