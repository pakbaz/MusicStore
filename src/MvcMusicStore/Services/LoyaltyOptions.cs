using System.Collections.Generic;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// Configuration for the loyalty rewards and referral program. Bound from the "Loyalty"
    /// configuration section so the economy can be tuned without code changes.
    /// </summary>
    public class LoyaltyOptions
    {
        public const string SectionName = "Loyalty";

        // Base points earned per dollar of order subtotal (before the tier multiplier).
        public int PointsPerDollar { get; set; } = 10;

        // Points required to redeem one dollar of discount at checkout.
        public int PointsPerDollarRedeemed { get; set; } = 100;

        // Points may only be redeemed in multiples of this value.
        public int RedemptionIncrement { get; set; } = 100;

        // Reward granted to the referrer after the referred customer's first purchase.
        public int ReferrerRewardPoints { get; set; } = 500;

        // Reward granted to the new customer after their first purchase via a referral.
        public int RefereeRewardPoints { get; set; } = 250;

        // Number of characters in a generated referral code.
        public int ReferralCodeLength { get; set; } = 8;

        public List<LoyaltyTierOptions> Tiers { get; set; } = new()
        {
            new LoyaltyTierOptions { Name = "Bronze", MinimumLifetimeSpend = 0m, EarnMultiplier = 1.0m },
            new LoyaltyTierOptions { Name = "Silver", MinimumLifetimeSpend = 50m, EarnMultiplier = 1.25m },
            new LoyaltyTierOptions { Name = "Gold", MinimumLifetimeSpend = 150m, EarnMultiplier = 1.5m },
            new LoyaltyTierOptions { Name = "Platinum", MinimumLifetimeSpend = 300m, EarnMultiplier = 2.0m },
        };
    }

    /// <summary>
    /// A single loyalty tier. Higher lifetime spend unlocks a higher earn multiplier.
    /// </summary>
    public class LoyaltyTierOptions
    {
        public string Name { get; set; } = "Bronze";
        public decimal MinimumLifetimeSpend { get; set; }
        public decimal EarnMultiplier { get; set; } = 1.0m;
    }
}
