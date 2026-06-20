using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// Encapsulates the loyalty rewards and referral rules: tier resolution, point earning,
    /// redemption math, referral-code lifecycle, and applying loyalty changes at checkout.
    /// </summary>
    public class LoyaltyService : ILoyaltyService
    {
        // Excludes visually ambiguous characters (0/O, 1/I) so codes are easy to share.
        private static readonly char[] CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly LoyaltyOptions _options;

        public LoyaltyService(UserManager<ApplicationUser> userManager, IOptions<LoyaltyOptions> options)
        {
            _userManager = userManager;
            _options = options.Value;
        }

        public LoyaltyOptions Options => _options;

        public LoyaltyTier GetTier(decimal lifetimeSpend)
        {
            var tiers = OrderedTiers();
            var current = tiers[0];
            foreach (var tier in tiers)
            {
                if (lifetimeSpend >= tier.MinimumLifetimeSpend)
                {
                    current = tier;
                }
                else
                {
                    break;
                }
            }

            return current;
        }

        public LoyaltyTier? GetNextTier(decimal lifetimeSpend)
        {
            return OrderedTiers().FirstOrDefault(tier => tier.MinimumLifetimeSpend > lifetimeSpend);
        }

        public int CalculateEarnedPoints(decimal amount, LoyaltyTier tier)
        {
            if (amount <= 0)
            {
                return 0;
            }

            var points = amount * _options.PointsPerDollar * tier.EarnMultiplier;
            return (int)Math.Floor(points);
        }

        public int MaxRedeemablePoints(int balance, decimal subtotal)
        {
            return ComputeRedemption(balance, balance, subtotal).PointsApplied;
        }

        public RedemptionResult ComputeRedemption(int requestedPoints, int balance, decimal subtotal)
        {
            var increment = Math.Max(1, _options.RedemptionIncrement);
            var pointsPerDollar = Math.Max(1, _options.PointsPerDollarRedeemed);

            if (requestedPoints <= 0 || balance <= 0 || subtotal <= 0)
            {
                return new RedemptionResult(0, 0m);
            }

            // Round the request down to a whole increment.
            var usable = requestedPoints / increment * increment;

            // Cap to the (incremented) available balance.
            var balanceCap = balance / increment * increment;
            usable = Math.Min(usable, balanceCap);

            // Cap to the discount value of the order subtotal (whole dollars).
            var maxDiscountDollars = (int)Math.Floor(subtotal);
            var subtotalCap = maxDiscountDollars * pointsPerDollar;
            usable = Math.Min(usable, subtotalCap);

            if (usable <= 0)
            {
                return new RedemptionResult(0, 0m);
            }

            var discount = (decimal)usable / pointsPerDollar;
            return new RedemptionResult(usable, discount);
        }

        public async Task<string> EnsureReferralCodeAsync(ApplicationUser user)
        {
            if (string.IsNullOrWhiteSpace(user.ReferralCode))
            {
                user.ReferralCode = await GenerateUniqueCodeAsync();
                await _userManager.UpdateAsync(user);
            }

            return user.ReferralCode!;
        }

        public async Task<ApplicationUser?> FindByReferralCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var normalized = Normalize(code);

            // Cosmos cannot translate AnyAsync()/FirstOrDefaultAsync (EXISTS subquery), so
            // materialize a single matching record with Take(1) instead.
            var matches = await _userManager.Users
                .Where(u => u.ReferralCode == normalized)
                .Take(1)
                .ToListAsync();

            return matches.FirstOrDefault();
        }

        public async Task RegisterReferralAsync(ApplicationUser newUser, string? referredByCode)
        {
            if (!string.IsNullOrWhiteSpace(referredByCode))
            {
                var normalized = Normalize(referredByCode);
                var referrer = await FindByReferralCodeAsync(normalized);
                if (referrer != null && referrer.Id != newUser.Id)
                {
                    newUser.ReferredByCode = normalized;
                }
            }

            if (string.IsNullOrWhiteSpace(newUser.ReferralCode))
            {
                newUser.ReferralCode = await GenerateUniqueCodeAsync();
            }

            await _userManager.UpdateAsync(newUser);
        }

        public async Task<PurchaseLoyaltyResult> ApplyPurchaseAsync(ApplicationUser user, decimal subtotal, int pointsRedeemed)
        {
            pointsRedeemed = Math.Clamp(pointsRedeemed, 0, user.LoyaltyPoints);

            // Earn at the tier the customer held when the purchase was made.
            var earningTier = GetTier(user.LifetimeSpend);
            var earned = CalculateEarnedPoints(subtotal, earningTier);

            user.LoyaltyPoints = user.LoyaltyPoints - pointsRedeemed + earned;
            user.LifetimePointsEarned += earned;
            user.LifetimeSpend += subtotal;

            // Resolve a referral reward (granted exactly once, on the referred customer's first purchase).
            ApplicationUser? referrer = null;
            var referralBonus = 0;
            if (!user.HasMadePurchase && !user.ReferralRewardGranted && !string.IsNullOrWhiteSpace(user.ReferredByCode))
            {
                var candidate = await FindByReferralCodeAsync(user.ReferredByCode!);
                if (candidate != null && candidate.Id != user.Id)
                {
                    referrer = candidate;
                    referralBonus = _options.RefereeRewardPoints;
                    user.LoyaltyPoints += referralBonus;
                    user.LifetimePointsEarned += referralBonus;
                    user.ReferralRewardGranted = true;
                }
            }

            user.HasMadePurchase = true;
            if (string.IsNullOrWhiteSpace(user.ReferralCode))
            {
                user.ReferralCode = await GenerateUniqueCodeAsync();
            }

            // Persist the customer's own changes first as a single Identity save. The referral grant
            // flag is stored atomically with the referee bonus, so a failure paying the referrer can
            // never double-grant on a retry. Bail out without paying the referrer if this save fails.
            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                return new PurchaseLoyaltyResult
                {
                    Persisted = false,
                    PointsRedeemed = pointsRedeemed,
                    PointsEarned = earned,
                    ReferralBonusPoints = 0,
                    NewBalance = user.LoyaltyPoints,
                    Tier = GetTier(user.LifetimeSpend),
                };
            }

            // Pay the referrer only after the customer's grant flag is durably stored.
            if (referrer != null)
            {
                referrer.LoyaltyPoints += _options.ReferrerRewardPoints;
                referrer.LifetimePointsEarned += _options.ReferrerRewardPoints;
                await _userManager.UpdateAsync(referrer);
            }

            return new PurchaseLoyaltyResult
            {
                Persisted = true,
                PointsRedeemed = pointsRedeemed,
                PointsEarned = earned,
                ReferralBonusPoints = referralBonus,
                NewBalance = user.LoyaltyPoints,
                Tier = GetTier(user.LifetimeSpend),
            };
        }

        private List<LoyaltyTier> OrderedTiers()
        {
            var tiers = _options.Tiers
                .OrderBy(t => t.MinimumLifetimeSpend)
                .Select(t => new LoyaltyTier(t.Name, t.MinimumLifetimeSpend, t.EarnMultiplier))
                .ToList();

            if (tiers.Count == 0)
            {
                tiers.Add(new LoyaltyTier("Bronze", 0m, 1.0m));
            }

            return tiers;
        }

        private async Task<string> GenerateUniqueCodeAsync()
        {
            string code;
            do
            {
                code = GenerateCode();
            }
            while (await ReferralCodeExistsAsync(code));

            return code;
        }

        private async Task<bool> ReferralCodeExistsAsync(string code)
        {
            var matches = await _userManager.Users
                .Where(u => u.ReferralCode == code)
                .Select(u => u.Id)
                .Take(1)
                .ToListAsync();

            return matches.Count > 0;
        }

        private string GenerateCode()
        {
            var length = Math.Clamp(_options.ReferralCodeLength, 4, 32);
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }

            return new string(chars);
        }

        private static string Normalize(string code) => code.Trim().ToUpperInvariant();
    }
}
