using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.Models
{
    public class AlbumGift
    {
        public int AlbumGiftId { get; set; }

        [Required]
        [StringLength(64)]
        public string Token { get; set; } = string.Empty;

        public int AlbumId { get; set; }

        [StringLength(160)]
        public string? AlbumTitle { get; set; }

        [DataType(DataType.Currency)]
        public decimal AlbumPrice { get; set; }

        [StringLength(1024)]
        public string? AlbumArtUrl { get; set; }

        [StringLength(256)]
        public string? SenderUsername { get; set; }

        [StringLength(160)]
        public string? SenderName { get; set; }

        [StringLength(256)]
        public string? RecipientEmail { get; set; }

        [StringLength(160)]
        public string? RecipientName { get; set; }

        [StringLength(500)]
        public string? Message { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; }

        public bool IsRedeemed { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? RedeemedDate { get; set; }

        [StringLength(256)]
        public string? RedeemedByUsername { get; set; }

        public int? RedeemedOrderId { get; set; }
    }
}
