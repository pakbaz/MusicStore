using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MvcMusicStore.Models;

namespace MvcMusicStore.ViewModels
{
    public class BuyGiftCardViewModel
    {
        public static readonly decimal[] PresetAmounts = { 10m, 25m, 50m, 100m };

        public const decimal MinimumAmount = 5m;
        public const decimal MaximumAmount = 500m;

        [DisplayName("Amount")]
        public decimal Amount { get; set; } = 25m;

        [DisplayName("Custom amount")]
        [Range(typeof(decimal), "5", "500", ErrorMessage = "Custom amount must be between $5 and $500.")]
        public decimal? CustomAmount { get; set; }

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

        // The custom amount wins when provided; otherwise the selected preset is used.
        public decimal EffectiveAmount => CustomAmount is > 0 ? CustomAmount.Value : Amount;
    }

    public class MyGiftCardsViewModel
    {
        public List<GiftCard> PurchasedCards { get; set; } = new();

        public string? LookupCode { get; set; }

        public GiftCard? LookupResult { get; set; }

        public string? LookupError { get; set; }
    }
}
