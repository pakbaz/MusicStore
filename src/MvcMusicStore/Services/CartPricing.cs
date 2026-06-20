using System;
using System.Collections.Generic;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// A single priced cart line: original unit price plus the sale-adjusted (effective) price.
    /// </summary>
    public class CartLine
    {
        public int RecordId { get; init; }
        public int AlbumId { get; init; }
        public string Title { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal EffectiveUnitPrice { get; init; }
        public string? SaleName { get; init; }
        public DateTime? SaleEndsUtc { get; init; }

        public bool IsOnSale => EffectiveUnitPrice < UnitPrice;
        public decimal LineSubtotal => EffectiveUnitPrice * Quantity;
        public decimal OriginalLineSubtotal => UnitPrice * Quantity;
    }

    /// <summary>
    /// Fully priced cart: sale-adjusted line items, subtotal, optional coupon discount, and total.
    /// </summary>
    public class CartPricing
    {
        public List<CartLine> Lines { get; init; } = new();

        /// <summary>Sum of sale-adjusted line subtotals (rounded).</summary>
        public decimal Subtotal { get; init; }

        /// <summary>Sum of original (pre-sale) line subtotals (rounded).</summary>
        public decimal OriginalSubtotal { get; init; }

        public string? AppliedCode { get; init; }
        public decimal DiscountAmount { get; init; }
        public string? DiscountMessage { get; init; }

        /// <summary>The tracked discount-code entity, when a valid coupon is applied.</summary>
        public DiscountCode? AppliedDiscountCode { get; init; }

        public decimal Total { get; init; }

        public decimal SaleSavings => Math.Max(0m, OriginalSubtotal - Subtotal);
        public bool HasSaleSavings => SaleSavings > 0m;
        public bool DiscountApplied => !string.IsNullOrEmpty(AppliedCode) && DiscountAmount > 0m;
        public bool HasAnySavings => HasSaleSavings || DiscountApplied;
    }
}
