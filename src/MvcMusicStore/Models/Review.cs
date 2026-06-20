using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.Models
{
    public class Review
    {
        public const int MinRating = 1;
        public const int MaxRating = 5;

        [ScaffoldColumn(false)]
        public int ReviewId { get; set; }

        public int AlbumId { get; set; }

        [StringLength(160)]
        public string? AlbumTitle { get; set; }

        [StringLength(256)]
        public string? Username { get; set; }

        [Range(MinRating, MaxRating, ErrorMessage = "Choose a rating from 1 to 5 stars.")]
        public int Rating { get; set; }

        [DisplayName("Review")]
        [StringLength(2000)]
        [DataType(DataType.MultilineText)]
        public string? Body { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? UpdatedDate { get; set; }

        [DisplayName("Hidden")]
        public bool IsHidden { get; set; }

        [DisplayName("Reported")]
        public bool IsReported { get; set; }

        [StringLength(500)]
        public string? ReportReason { get; set; }

        public bool HasBody => !string.IsNullOrWhiteSpace(Body);
    }
}
