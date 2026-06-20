using System.Threading.Tasks;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    /// <summary>A loyalty tier resolved from configuration.</summary>
    public record LoyaltyTier(string Name, decimal MinimumLifetimeSpend, decimal EarnMultiplier);

    /// <summary>The outcome of validating a redemption request.</summary>
    public record RedemptionResult(int PointsApplied, decimal Discount);

    /// <summary>The loyalty changes produced by completing a purchase.</summary>
    public class PurchaseLoyaltyResult
    {
        public int PointsRedeemed { get; set; }
        public decimal Discount { get; set; }
        public int PointsEarned { get; set; }
        public int ReferralBonusPoints { get; set; }
        public int NewBalance { get; set; }
        public LoyaltyTier Tier { get; set; } = new("Bronze", 0m, 1.0m);

        /// <summary>False when the customer's loyalty changes could not be saved (e.g. a concurrent update).</summary>
        public bool Persisted { get; set; } = true;
    }

    public interface ILoyaltyService
    {
        LoyaltyOptions Options { get; }

        LoyaltyTier GetTier(decimal lifetimeSpend);
        LoyaltyTier? GetNextTier(decimal lifetimeSpend);
        int CalculateEarnedPoints(decimal amount, LoyaltyTier tier);

        int MaxRedeemablePoints(int balance, decimal subtotal);
        RedemptionResult ComputeRedemption(int requestedPoints, int balance, decimal subtotal);

        Task<string> EnsureReferralCodeAsync(ApplicationUser user);
        Task<ApplicationUser?> FindByReferralCodeAsync(string code);
        Task RegisterReferralAsync(ApplicationUser newUser, string? referredByCode);
        Task<PurchaseLoyaltyResult> ApplyPurchaseAsync(ApplicationUser user, decimal subtotal, int pointsRedeemed);
    }
}
