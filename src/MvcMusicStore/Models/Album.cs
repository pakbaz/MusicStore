using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.Models
{
    public class Album {
        public const string DefaultPlaceholderThumbnailUrl = "~/Images/placeholder.svg";

        public static bool IsPlaceholderThumbnailUrl(string? thumbnailUrl)
        {
            if (string.IsNullOrWhiteSpace(thumbnailUrl))
            {
                return true;
            }

            return string.Equals(thumbnailUrl, DefaultPlaceholderThumbnailUrl, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(thumbnailUrl, "~/Images/placeholder.png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(thumbnailUrl, "/Images/placeholder.svg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(thumbnailUrl, "/Images/placeholder.png", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeThumbnailUrl(string? thumbnailUrl)
        {
            if (string.IsNullOrWhiteSpace(thumbnailUrl))
            {
                return DefaultPlaceholderThumbnailUrl;
            }

            return thumbnailUrl.StartsWith("http://coverartarchive.org/", StringComparison.OrdinalIgnoreCase)
                ? "https://coverartarchive.org/" + thumbnailUrl["http://coverartarchive.org/".Length..]
                : thumbnailUrl;
        }

        [ScaffoldColumn(false)]

        public int AlbumId { get; set; }

        public int GenreId { get; set; }

        [StringLength(120)]
        public string? GenreName { get; set; }

        public int ArtistId { get; set; }

        [StringLength(160)]
        public string? ArtistName { get; set; }

        [Required]
        [StringLength(160, MinimumLength = 2)]
        public string? Title { get; set; }

        [Required]
        [Range(0.01, 100.00)]

        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [DisplayName("Album Art URL")]
        [StringLength(1024)]
        public string? AlbumArtUrl { get; set; }

        [DisplayName("Audio URL")]
        [StringLength(1024)]
        public string? AudioUrl { get; set; }

        [DisplayName("Preview Audio URL")]
        [StringLength(1024)]
        public string? PreviewUrl { get; set; }

        [DisplayName("Preview Length (seconds)")]
        [Range(5, 60)]
        public int PreviewDurationSeconds { get; set; } = DefaultPreviewDurationSeconds;

        public const int DefaultPreviewDurationSeconds = 30;

        public bool HasPreview => !string.IsNullOrWhiteSpace(PreviewUrl);

        [DisplayName("Metadata Thumbnail URL")]
        [StringLength(1024)]
        public string? MetadataThumbnailUrl { get; set; }

        [DisplayName("Uploaded Thumbnail URL")]
        [StringLength(1024)]
        public string? UploadedThumbnailUrl { get; set; }

        public string GetDisplayThumbnailUrl()
        {
            if (!IsPlaceholderThumbnailUrl(UploadedThumbnailUrl))
            {
                return NormalizeThumbnailUrl(UploadedThumbnailUrl);
            }

            if (!IsPlaceholderThumbnailUrl(MetadataThumbnailUrl))
            {
                return NormalizeThumbnailUrl(MetadataThumbnailUrl);
            }

            if (!IsPlaceholderThumbnailUrl(AlbumArtUrl))
            {
                return NormalizeThumbnailUrl(AlbumArtUrl);
            }

            return DefaultPlaceholderThumbnailUrl;
        }

        [DisplayName("Featured Release")]
        public bool IsFeatured { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ReleaseDate { get; set; }

        [DisplayName("Available")]
        public bool IsAvailable { get; set; } = true;

        // Denormalized cumulative units sold, maintained at checkout so the catalog can sort by
        // popularity without scanning the Orders container on every request.
        [ScaffoldColumn(false)]
        public int Popularity { get; set; }

        public virtual Genre? Genre { get; set; }
        public virtual Artist? Artist { get; set; }
        public virtual List<OrderDetail>? OrderDetails { get; set; }
    }
}