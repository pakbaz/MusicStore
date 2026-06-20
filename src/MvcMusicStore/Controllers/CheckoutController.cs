using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;

namespace MvcMusicStore.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly MusicStoreEntities storeDB;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILoyaltyService _loyalty;
        const string PromoCode = "FREE";

        public CheckoutController(
            MusicStoreEntities storeDb,
            UserManager<ApplicationUser> userManager,
            ILoyaltyService loyalty)
        {
            storeDB = storeDb;
            _userManager = userManager;
            _loyalty = loyalty;
        }

        //
        // GET: /Checkout/

        public async Task<IActionResult> AddressAndPayment()
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            if (await cart.GetCountAsync() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            var user = await _userManager.GetUserAsync(User);
            await PopulateLoyaltyViewBagAsync(user, await cart.GetTotalAsync());

            return View(new Order());
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public async Task<IActionResult> AddressAndPayment(Order order)
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var user = await _userManager.GetUserAsync(User);
            await PopulateLoyaltyViewBagAsync(user, await cart.GetTotalAsync());

            if (!ModelState.IsValid)
                return View(order);

            // Check promo code from form
            var promoCode = Request.Form["PromoCode"].ToString();
            if (!string.Equals(promoCode, PromoCode, StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.PromoCode = promoCode;
                ModelState.AddModelError("PromoCode", "Please enter the valid promo code to place your order.");
                return View(order);
            }

            if (await cart.GetCountAsync() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            order.OrderId = await storeDB.NextOrderIdAsync();
            order.Username = User.Identity!.Name!;
            order.OrderDate = DateTime.Now;

            // Process the order (builds embedded order details, sets Total, and empties the cart)
            await cart.CreateOrderAsync(order);

            // Apply any loyalty points the customer chose to redeem as a discount on the order total.
            var subtotal = order.Total;
            var requestedPoints = ParseRequestedPoints();
            var redemption = _loyalty.ComputeRedemption(requestedPoints, user?.LoyaltyPoints ?? 0, subtotal);

            order.LoyaltyPointsRedeemed = redemption.PointsApplied;
            order.LoyaltyDiscount = redemption.Discount;
            order.Total = subtotal - redemption.Discount;

            // Points are earned at the tier the customer holds before this purchase; record them on
            // the order so the recorded value matches what ApplyPurchaseAsync credits below.
            if (user != null)
            {
                order.LoyaltyPointsEarned = _loyalty.CalculateEarnedPoints(subtotal, _loyalty.GetTier(user.LifetimeSpend));
            }

            // Persist the order first. The loyalty balance lives in a separate Cosmos context with no
            // shared transaction, so saving the order before charging points ensures a customer can
            // never have points deducted without a recorded order.
            storeDB.Orders.Add(order);
            await storeDB.SaveChangesAsync();

            // Accrue points / update tier / pay out any referral reward on first purchase.
            if (user != null)
            {
                var loyaltyResult = await _loyalty.ApplyPurchaseAsync(user, subtotal, redemption.PointsApplied);
                TempData["ReferralBonus"] = loyaltyResult.ReferralBonusPoints;
            }

            return RedirectToAction("Complete", new { id = order.OrderId });
        }

        //
        // GET: /Checkout/Complete

        public async Task<IActionResult> Complete(int id)
        {
            // Validate the customer owns this order. Cosmos cannot translate AnyAsync() (EXISTS
            // subquery), so materialize a single matching record with Take(1) instead.
            var order = (await storeDB.Orders
                .Where(o => o.OrderId == id && o.Username == User.Identity!.Name)
                .Take(1)
                .ToListAsync())
                .FirstOrDefault();

            if (order == null)
            {
                ViewBag.ErrorMessage = "We couldn't find that order for your account. Please review your recent orders or try checkout again.";
                return View("Error");
            }

            var user = await _userManager.GetUserAsync(User);
            var tier = _loyalty.GetTier(user?.LifetimeSpend ?? 0m);

            ViewBag.PointsEarned = order.LoyaltyPointsEarned;
            ViewBag.PointsRedeemed = order.LoyaltyPointsRedeemed;
            ViewBag.LoyaltyDiscount = order.LoyaltyDiscount;
            ViewBag.OrderTotal = order.Total;
            ViewBag.NewBalance = user?.LoyaltyPoints ?? 0;
            ViewBag.TierName = tier.Name;
            ViewBag.ReferralBonus = Convert.ToInt32(TempData["ReferralBonus"] ?? 0);

            return View(id);
        }

        private int ParseRequestedPoints()
        {
            var raw = Request.Form["PointsToRedeem"].ToString();
            return int.TryParse(raw, out var value) && value > 0 ? value : 0;
        }

        private Task PopulateLoyaltyViewBagAsync(ApplicationUser? user, decimal subtotal)
        {
            var points = user?.LoyaltyPoints ?? 0;
            var tier = _loyalty.GetTier(user?.LifetimeSpend ?? 0m);

            ViewBag.LoyaltyPoints = points;
            ViewBag.LoyaltyTierName = tier.Name;
            ViewBag.LoyaltyMultiplier = tier.EarnMultiplier;
            ViewBag.MaxRedeemablePoints = _loyalty.MaxRedeemablePoints(points, subtotal);
            ViewBag.PointsPerDollarRedeemed = _loyalty.Options.PointsPerDollarRedeemed;
            ViewBag.RedemptionIncrement = _loyalty.Options.RedemptionIncrement;
            ViewBag.PointsPerDollar = _loyalty.Options.PointsPerDollar;
            ViewBag.CartSubtotal = subtotal;
            ViewBag.ProjectedEarn = _loyalty.CalculateEarnedPoints(subtotal, tier);

            return Task.CompletedTask;
        }
    }
}