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
        private readonly IPromotionService promotions;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILoyaltyService _loyalty;
        private readonly IGiftCardService giftCards;
        private readonly IOrderEmailSender emailSender;
        private readonly ILogger<CheckoutController> logger;

        public CheckoutController(
            MusicStoreEntities storeDb,
            IPromotionService promotions,
            UserManager<ApplicationUser> userManager,
            ILoyaltyService loyalty,
            IGiftCardService giftCardService,
            IOrderEmailSender emailSender,
            ILogger<CheckoutController> logger)
        {
            storeDB = storeDb;
            this.promotions = promotions;
            _userManager = userManager;
            _loyalty = loyalty;
            giftCards = giftCardService;
            this.emailSender = emailSender;
            this.logger = logger;
        }

        //
        // GET: /Checkout/

        public async Task<IActionResult> AddressAndPayment()
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var cartItems = await cart.GetCartItemsAsync();
            if (cartItems.Count == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            // Price against live sales + the code held in session so the summary and the loyalty
            // projection both reflect what the customer will actually pay.
            var pricing = await promotions.PriceCartAsync(cartItems, GetSessionDiscountCode());
            ViewBag.Pricing = pricing;

            var user = await _userManager.GetUserAsync(User);
            await PopulateLoyaltyViewBagAsync(user, pricing.Total);

            return View(new Order());
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public async Task<IActionResult> AddressAndPayment(Order order)
        {
            var giftCardCode = Request.Form["GiftCardCode"].ToString();
            ViewBag.GiftCardCode = giftCardCode;

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var cartItems = await cart.GetCartItemsAsync();

            // Re-price against live sales + the code held in session (it may have expired since the cart).
            var pricing = await promotions.PriceCartAsync(cartItems, GetSessionDiscountCode());
            ViewBag.Pricing = pricing;

            var user = await _userManager.GetUserAsync(User);
            await PopulateLoyaltyViewBagAsync(user, pricing.Total);

            if (!ModelState.IsValid)
            {
                return View(order);
            }

            if (cartItems.Count == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            // Resolve an optional gift card. A gift card is a payment method that pays the order
            // total down; any remainder is simply recorded as the amount due.
            GiftCard? giftCard = null;
            if (!string.IsNullOrWhiteSpace(giftCardCode))
            {
                giftCard = await giftCards.GetActiveByCodeAsync(giftCardCode);
                if (giftCard == null || giftCard.Balance <= 0)
                {
                    ModelState.AddModelError("GiftCardCode", "That gift card code is not valid or has no remaining balance.");
                    return View(order);
                }
            }

            order.OrderId = await storeDB.NextOrderIdAsync();
            order.Username = User.Identity!.Name!;
            order.OrderDate = DateTime.Now;
            order.Status = "Paid";

            // Builds embedded order details at the charged (sale-adjusted) prices, records the
            // discount code, sets Subtotal/DiscountAmount/Total, and empties the cart.
            await cart.CreateOrderAsync(order, pricing);

            // Apply any loyalty points the customer chose to redeem against the sale- and
            // coupon-adjusted order total.
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

            // Apply the gift card against the post-loyalty order total, recording a redemption
            // transaction on the tracked card.
            if (giftCard != null)
            {
                var applied = giftCards.Redeem(giftCard, order.Total, order.OrderId, order.Username);
                order.GiftCardCode = giftCard.Code;
                order.GiftCardAmountApplied = applied;
            }

            order.AmountDue = Math.Max(0m, order.Total - order.GiftCardAmountApplied);

            storeDB.Orders.Add(order);

            // Redeem the coupon (same tracked DbContext, persisted in the SaveChanges below).
            if (pricing.AppliedDiscountCode is { } redeemedCode)
            {
                redeemedCode.TimesUsed += 1;
            }

            // Persist the order first. The loyalty balance lives in a separate Cosmos context with no
            // shared transaction, so saving the order before crediting points ensures a customer can
            // never have points deducted without a recorded order.
            await storeDB.SaveChangesAsync();

            ClearSessionDiscountCode();

            // Accrue points / update tier / pay out any referral reward on first purchase.
            if (user != null)
            {
                var loyaltyResult = await _loyalty.ApplyPurchaseAsync(user, subtotal, redemption.PointsApplied);
                TempData["ReferralBonus"] = loyaltyResult.ReferralBonusPoints;
            }

            // Send the confirmation receipt. A delivery failure must never fail a placed order.
            try
            {
                await emailSender.SendOrderConfirmationAsync(order);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send confirmation email for order {OrderId}.", order.OrderId);
            }

            return RedirectToAction("Complete", new { id = order.OrderId });
        }

        //
        // GET: /Checkout/Complete

        public async Task<IActionResult> Complete(int id)
        {
            // Validate customer owns this order. Cosmos cannot translate AnyAsync() (EXISTS subquery),
            // so materialize a single matching order with Take(1) instead. Owned OrderDetails are
            // part of the same document, so they load with the order for the confirmation summary.
            var matchingOrders = await storeDB.Orders
                .Where(o => o.OrderId == id && o.Username == User.Identity!.Name)
                .Take(1)
                .ToListAsync();
            var order = matchingOrders.FirstOrDefault();

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

            return View(order);
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

        private string? GetSessionDiscountCode() =>
            HttpContext.Session.GetString(ShoppingCart.DiscountCodeSessionKey);

        private void ClearSessionDiscountCode() =>
            HttpContext.Session.Remove(ShoppingCart.DiscountCodeSessionKey);
    }
}
