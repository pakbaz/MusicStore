namespace MvcMusicStore.ViewModels
{
    public class RewardsViewModel
    {
        public int Points { get; set; }
        public decimal LifetimeSpend { get; set; }
        public int LifetimePointsEarned { get; set; }

        // Redeemable dollar value of the current points balance.
        public decimal PointsDollarValue { get; set; }

        public string TierName { get; set; } = "Bronze";
        public decimal TierMultiplier { get; set; } = 1.0m;

        public string? NextTierName { get; set; }
        public decimal NextTierMultiplier { get; set; }
        public decimal SpendToNextTier { get; set; }
        public int NextTierProgressPercent { get; set; }

        public string ReferralCode { get; set; } = string.Empty;
        public string ReferralLink { get; set; } = string.Empty;
        public bool WasReferred { get; set; }

        public int ReferrerRewardPoints { get; set; }
        public int RefereeRewardPoints { get; set; }
        public int PointsPerDollar { get; set; }
        public int PointsPerDollarRedeemed { get; set; }
    }
}
