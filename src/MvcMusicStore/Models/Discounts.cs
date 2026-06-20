using System;

namespace MvcMusicStore.Models
{
    /// <summary>
    /// Shared discount math used by both per-item sales and cart-level discount codes.
    /// All results are clamped to non-negative values and rounded to two decimal places.
    /// </summary>
    public static class Discounts
    {
        public static decimal Apply(decimal price, DiscountType type, decimal value)
        {
            if (price <= 0m)
            {
                return 0m;
            }

            decimal discounted = type switch
            {
                DiscountType.Percentage => price - (price * (Clamp(value, 0m, 100m) / 100m)),
                DiscountType.FixedAmount => price - Math.Max(0m, value),
                _ => price
            };

            return Round(Math.Max(0m, discounted));
        }

        public static decimal AmountOff(decimal price, DiscountType type, decimal value) =>
            Round(Math.Max(0m, price - Apply(price, type, value)));

        public static decimal Round(decimal amount) =>
            Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        private static decimal Clamp(decimal value, decimal min, decimal max) =>
            value < min ? min : value > max ? max : value;
    }
}
