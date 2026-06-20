using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MvcMusicStore.Models
{
    public class Cart
    {
        [Key]
        public int RecordId  { get; set; }
        public string? CartId { get; set; }
        public int AlbumId   { get; set; }
        public int Count     { get; set; }

        public string? AlbumTitle { get; set; }
        public decimal AlbumPrice { get; set; }
        public string? AlbumArtUrl { get; set; }

        // Bundle lines: a single cart row can represent a discounted bundle instead of one album.
        public int? BundleId { get; set; }
        public string? BundleTitle { get; set; }
        public decimal BundlePrice { get; set; }
        public List<CartBundleItem>? BundleItems { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime DateCreated { get; set; }

        public virtual Album? Album  { get; set; }

        [NotMapped]
        public bool IsBundle => BundleId.HasValue;

        [NotMapped]
        public decimal LineUnitPrice => IsBundle ? BundlePrice : AlbumPrice;

        [NotMapped]
        public decimal LineSubtotal => LineUnitPrice * Count;

        [NotMapped]
        public string DisplayTitle => IsBundle
            ? (BundleTitle ?? "Bundle")
            : (AlbumTitle ?? Album?.Title ?? "Album");
    }

    public class CartBundleItem
    {
        public int AlbumId { get; set; }
        public string? AlbumTitle { get; set; }
        public decimal UnitPrice { get; set; }
    }
}