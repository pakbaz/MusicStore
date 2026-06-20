using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MvcMusicStore.Models;

namespace MvcMusicStore.ViewModels
{
    public class SendGiftViewModel
    {
        public int AlbumId { get; set; }

        public string? AlbumTitle { get; set; }

        public string? ArtistName { get; set; }

        public string? AlbumArtUrl { get; set; }

        [DataType(DataType.Currency)]
        public decimal AlbumPrice { get; set; }

        [Required]
        [DisplayName("Recipient email")]
        [EmailAddress(ErrorMessage = "Enter a valid recipient email address.")]
        [StringLength(256)]
        public string? RecipientEmail { get; set; }

        [DisplayName("Recipient name (optional)")]
        [StringLength(160)]
        public string? RecipientName { get; set; }

        [DisplayName("Your name (optional)")]
        [StringLength(160)]
        public string? SenderName { get; set; }

        [DisplayName("Personal message (optional)")]
        [StringLength(500)]
        [DataType(DataType.MultilineText)]
        public string? Message { get; set; }
    }

    public class RedeemGiftViewModel
    {
        public AlbumGift? Gift { get; set; }

        public string Token { get; set; } = string.Empty;

        public bool IsSignedIn { get; set; }
    }

    public class GiftRedeemedViewModel
    {
        public int OrderId { get; set; }

        public string? AlbumTitle { get; set; }

        public string? SenderName { get; set; }
    }
}
