using System;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.Models
{
    public class WishlistItem
    {
        [Key]
        public int RecordId { get; set; }
        public string? WishlistId { get; set; }
        public int AlbumId { get; set; }

        public string? AlbumTitle { get; set; }
        public decimal AlbumPrice { get; set; }
        public string? AlbumArtUrl { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime DateCreated { get; set; }

        public virtual Album? Album { get; set; }
    }
}
