using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace MvcMusicStore.Models
{
    public class Bundle
    {
        [ScaffoldColumn(false)]
        public int BundleId { get; set; }

        [Required]
        [StringLength(160, MinimumLength = 2)]
        public string? Title { get; set; }

        [StringLength(1024)]
        public string? Description { get; set; }

        [Required]
        [Range(0.01, 1000.00)]
        [DataType(DataType.Currency)]
        [DisplayName("Bundle Price")]
        public decimal BundlePrice { get; set; }

        [DisplayName("Active")]
        public bool IsActive { get; set; } = true;

        [DataType(DataType.DateTime)]
        public DateTime DateCreated { get; set; }

        public List<BundleItem> Items { get; set; } = new();

        [NotMapped]
        public decimal RegularPrice => Items?.Sum(item => item.AlbumPrice) ?? 0m;

        [NotMapped]
        public decimal Savings => Math.Max(0m, RegularPrice - BundlePrice);

        [NotMapped]
        public int SavingsPercent =>
            RegularPrice > 0m ? (int)Math.Round((Savings / RegularPrice) * 100m, MidpointRounding.AwayFromZero) : 0;

        [NotMapped]
        public int AlbumCount => Items?.Count ?? 0;

        public string GetDisplayThumbnailUrl()
        {
            var first = Items?.FirstOrDefault(item => !Album.IsPlaceholderThumbnailUrl(item.AlbumArtUrl))
                ?? Items?.FirstOrDefault();
            return Album.NormalizeThumbnailUrl(first?.AlbumArtUrl);
        }
    }

    public class BundleItem
    {
        public int AlbumId { get; set; }

        [StringLength(160)]
        public string? AlbumTitle { get; set; }

        public decimal AlbumPrice { get; set; }

        [StringLength(1024)]
        public string? AlbumArtUrl { get; set; }
    }
}
