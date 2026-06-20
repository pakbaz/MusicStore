using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.Models
{
    /// <summary>
    /// A coupon/discount code applied at the cart level. Supports percentage or fixed-amount
    /// discounts with an optional minimum spend, active window, and total usage limit.
    /// </summary>
    public class DiscountCode
    {
        [ScaffoldColumn(false)]
        public int DiscountCodeId { get; set; }

        [Required]
        [StringLength(40, MinimumLength = 2)]
        [RegularExpression(@"[A-Za-z0-9_-]+",
            ErrorMessage = "Use letters, numbers, hyphens, or underscores only.")]
        public string Code { get; set; } = string.Empty;

        [StringLength(160)]
        public string? Description { get; set; }

        [Required]
        [DisplayName("Discount Type")]
        public DiscountType DiscountType { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public decimal Value { get; set; }

        [DisplayName("Minimum Spend")]
        [Range(0, 1000000)]
        public decimal? MinimumSpend { get; set; }

        [DisplayName("Starts (UTC)")]
        [DataType(DataType.DateTime)]
        public DateTime? StartDateUtc { get; set; }

        [DisplayName("Expires (UTC)")]
        [DataType(DataType.DateTime)]
        public DateTime? EndDateUtc { get; set; }

        [DisplayName("Usage Limit")]
        [Range(1, 1000000)]
        public int? UsageLimit { get; set; }

        [DisplayName("Times Used")]
        [ScaffoldColumn(false)]
        public int TimesUsed { get; set; }

        [DisplayName("Active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Normalizes a user-entered code for storage and comparison.</summary>
        public static string Normalize(string? code) =>
            (code ?? string.Empty).Trim().ToUpperInvariant();

        public bool HasRemainingUses => UsageLimit is null || TimesUsed < UsageLimit.Value;

        public bool IsRedeemableAt(DateTime nowUtc) =>
            IsActive
            && HasRemainingUses
            && (StartDateUtc is null || StartDateUtc.Value <= nowUtc)
            && (EndDateUtc is null || nowUtc <= EndDateUtc.Value);
    }
}
