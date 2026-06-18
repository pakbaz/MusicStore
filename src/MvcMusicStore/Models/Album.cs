using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.Models
{
    public class Album {
        public const string DefaultPlaceholderThumbnailUrl = "~/Images/placeholder.png";

        [ScaffoldColumn(false)]

        public int AlbumId { get; set; }

        public int GenreId { get; set; }

        public int ArtistId { get; set; }

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

        [DisplayName("Metadata Thumbnail URL")]
        [StringLength(1024)]
        public string? MetadataThumbnailUrl { get; set; }

        [DisplayName("Uploaded Thumbnail URL")]
        [StringLength(1024)]
        public string? UploadedThumbnailUrl { get; set; }

        public string GetDisplayThumbnailUrl()
        {
            if (!string.IsNullOrWhiteSpace(UploadedThumbnailUrl))
            {
                return UploadedThumbnailUrl;
            }

            if (!string.IsNullOrWhiteSpace(MetadataThumbnailUrl))
            {
                return MetadataThumbnailUrl;
            }

            if (!string.IsNullOrWhiteSpace(AlbumArtUrl))
            {
                return AlbumArtUrl;
            }
            return DefaultPlaceholderThumbnailUrl;
        }

        [DisplayName("Featured Release")]
        public bool IsFeatured { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ReleaseDate { get; set; }

        [DisplayName("Available")]
        public bool IsAvailable { get; set; } = true;

        public virtual Genre? Genre { get; set; }
        public virtual Artist? Artist { get; set; }
        public virtual List<OrderDetail>? OrderDetails { get; set; }
    }
}