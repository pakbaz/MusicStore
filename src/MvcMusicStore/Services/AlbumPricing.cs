using System;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// The effective price of an album after any active sale has been applied.
    /// </summary>
    public class AlbumPricing
    {
        public int AlbumId { get; init; }
        public decimal OriginalPrice { get; init; }
        public decimal EffectivePrice { get; init; }
        public string? SaleName { get; init; }
        public DateTime? SaleEndsUtc { get; init; }

        public bool IsOnSale => EffectivePrice < OriginalPrice;

        public decimal AmountOff => Math.Max(0m, OriginalPrice - EffectivePrice);

        public int? PercentOff =>
            IsOnSale && OriginalPrice > 0m
                ? (int)Math.Round((1m - (EffectivePrice / OriginalPrice)) * 100m, MidpointRounding.AwayFromZero)
                : null;

        public static AlbumPricing NoSale(int albumId, decimal price) => new()
        {
            AlbumId = albumId,
            OriginalPrice = price,
            EffectivePrice = price
        };
    }
}
