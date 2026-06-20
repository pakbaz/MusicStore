using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.Models
{
    /// <summary>
    /// A time-boxed markdown applied to specific albums (or the entire store). The single sale
    /// flagged <see cref="IsFeatured"/> drives the homepage "Deal of the Day" countdown.
    /// </summary>
    public class Sale
    {
        [ScaffoldColumn(false)]
        public int SaleId { get; set; }

        [Required]
        [StringLength(120, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [DisplayName("Discount Type")]
        public DiscountType DiscountType { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public decimal Value { get; set; }

        [Required]
        [DisplayName("Starts (UTC)")]
        [DataType(DataType.DateTime)]
        public DateTime StartDateUtc { get; set; }

        [Required]
        [DisplayName("Ends (UTC)")]
        [DataType(DataType.DateTime)]
        public DateTime EndDateUtc { get; set; }

        [DisplayName("Active")]
        public bool IsActive { get; set; } = true;

        [DisplayName("Featured (Deal of the Day)")]
        public bool IsFeatured { get; set; }

        [DisplayName("Applies to entire store")]
        public bool AppliesToEntireStore { get; set; }

        [DisplayName("Album IDs")]
        public List<int> AlbumIds { get; set; } = new();

        public bool IsActiveAt(DateTime nowUtc) =>
            IsActive && StartDateUtc <= nowUtc && nowUtc <= EndDateUtc;

        public bool AppliesTo(int albumId) =>
            AppliesToEntireStore || AlbumIds.Contains(albumId);
    }
}
