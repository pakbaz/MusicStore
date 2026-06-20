using System;
using System.IO;
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
        private readonly IPaymentService paymentService;
        private readonly IGiftCardService giftCards;
        private readonly ILoyaltyService loyalty;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly StoreEmailService storeEmail;
        private readonly ILogger<CheckoutController> logger;

        public CheckoutController(
            MusicStoreEntities storeDb,
            IPromotionService promotions,
            IPaymentService paymentService,
            IGiftCardService giftCardService,
            ILoyaltyService loyaltyService,
            UserManager<ApplicationUser> userManager,
            StoreEmailService storeEmail,
            ILogger<CheckoutController> logger)
        {
            storeDB = storeDb;
            this.promotions = promotions;
            this.paymentService = paymentService;
            giftCards = giftCardService;
            loyalty = loyaltyService;
            this.userManager = userManager;
            this.storeEmail = storeEmail;
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
            ViewBag.PaymentConfigured = paymentService.IsConfigured;

            var user = await userManager.GetUserAsync(User);
            PopulateLoyaltyViewBag(user, pricing.Total);

            return View(new Order());
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public async Task<IActionResult> AddressAndPayment(Order order)
        {
            var giftCardCode = Request.Form["GiftCardCode"].ToString().Trim();
            ViewBag.GiftCardCode = giftCardCode;
            ViewBag.PaymentConfigured = paymentService.IsConfigured;

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var cartItems = await cart.GetCartItemsAsync();

            // Re-price against live sales + the code held in session (it may have expired since the cart).
            var pricing = await promotions.PriceCartAsync(cartItems, GetSessionDiscountCode());
            ViewBag.Pricing = pricing;

            var user = await userManager.GetUserAsync(User);
            PopulateLoyaltyViewBag(user, pricing.Total);

            if (!ModelState.IsValid)
                return View(order);

            if (cartItems.Count == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            // Loyalty redemption is computed first against the sale- and coupon-adjusted total; it
            // discounts the amount owed before any gift card or card charge. The discount is
            // recorded on the order and the points balance is only moved once payment is captured.
            var requestedPoints = ParseRequestedPoints();
            var redemption = loyalty.ComputeRedemption(requestedPoints, user?.LoyaltyPoints ?? 0, pricing.Total);
            var discountedTotal = pricing.Total - redemption.Discount;

            // Resolve an optional gift card against the post-loyalty total. A gift card is a payment
            // method that pays the order total down; any remainder is paid by card via Stripe.
            GiftCard? giftCard = null;
            decimal giftApplied = 0m;
            if (!string.IsNullOrWhiteSpace(giftCardCode))
            {
                giftCard = await giftCards.GetActiveByCodeAsync(giftCardCode);
                if (giftCard == null || giftCard.Balance <= 0)
                {
                    ModelState.AddModelError("GiftCardCode", "That gift card code is not valid or has no remaining balance.");
                    return View(order);
                }
                giftApplied = Math.Min(giftCard.Balance, discountedTotal);
            }

            var remainingDue = discountedTotal - giftApplied;

            order.OrderId = await storeDB.NextOrderIdAsync();
            order.Username = User.Identity!.Name!;
            order.OrderDate = DateTime.Now;

            var tier = loyalty.GetTier(user?.LifetimeSpend ?? 0m);

            // Path 1 - no card payment needed: a gift card and/or loyalty points cover the total.
            if (remainingDue <= 0m)
            {
                // Builds embedded order details at sale-adjusted prices, records the discount code,
                // sets Subtotal/DiscountAmount/Total, and empties the cart.
                await cart.CreateOrderAsync(order, pricing);
                ApplyLoyaltyToOrder(order, redemption, user, tier, pricing.Total);

                if (giftCard != null && giftApplied > 0m)
                {
                    var applied = giftCards.Redeem(giftCard, order.Total, order.OrderId, order.Username);
                    order.GiftCardCode = giftCard.Code;
                    order.GiftCardAmountApplied = applied;
                    order.PaymentProvider = "Gift card";
                }
                else
                {
                    order.PaymentProvider = order.LoyaltyDiscount > 0m ? "Loyalty points" : "No charge";
                }

                order.AmountDue = 0m;
                order.PaymentStatus = PaymentStatus.Paid;
                order.PaidDate = DateTime.Now;

                storeDB.Orders.Add(order);

                // Redeem the coupon now (tracked DbContext, persisted by the SaveChanges below).
                if (pricing.AppliedDiscountCode is { } redeemedCode)
                {
                    redeemedCode.TimesUsed += 1;
                }

                await storeDB.SaveChangesAsync();

                ClearSessionDiscountCode();

                if (user != null)
                {
                    var loyaltyResult = await loyalty.ApplyPurchaseAsync(user, pricing.Total, redemption.PointsApplied);
                    TempData["ReferralBonus"] = loyaltyResult.ReferralBonusPoints;
                }

                // Send the confirmation receipt. A delivery failure must never fail a placed order.
                try
                {
                    await storeEmail.SendOrderConfirmationAsync(order);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send confirmation email for order {OrderId}.", order.OrderId);
                }

                return RedirectToAction("Complete", new { id = order.OrderId });
            }

            // Path 2 - a balance remains and must be paid by card via Stripe Checkout.
            if (!paymentService.IsConfigured)
            {
                ModelState.AddModelError(string.Empty,
                    "Online card payment isn't configured yet. Use loyalty points and/or a gift card that cover your total.");
                return View(order);
            }

            // Persist a pending order so the address and line items survive the redirect to Stripe.
            // The cart is intentionally NOT emptied, the gift card is NOT redeemed, the coupon is
            // NOT marked used, and loyalty points are NOT moved until the payment is captured.
            await cart.BuildOrderAsync(order, pricing);
            ApplyLoyaltyToOrder(order, redemption, user, tier, pricing.Total);

            order.PaymentProvider = "Stripe";
            order.PaymentStatus = PaymentStatus.Pending;
            order.AmountDue = remainingDue;
            if (giftCard != null && giftApplied > 0m)
            {
                order.GiftCardCode = giftCard.Code;
                order.GiftCardAmountApplied = giftApplied;
            }

            storeDB.Orders.Add(order);
            await storeDB.SaveChangesAsync();

            try
            {
                var completeUrl = Url.Action("Complete", "Checkout", new { id = order.OrderId }, Request.Scheme)!;
                var successUrl = completeUrl
                    + (completeUrl.Contains('?') ? "&" : "?")
                    + "session_id={CHECKOUT_SESSION_ID}";
                var cancelUrl = Url.Action("Cancel", "Checkout", new { id = order.OrderId }, Request.Scheme)!;

                // Stripe charges the post-loyalty, post-gift-card remainder.
                var discountAmount = redemption.Discount + giftApplied;
                var session = await paymentService.CreateCheckoutSessionAsync(order, cartItems, successUrl, cancelUrl, discountAmount);

                order.PaymentReference = session.Id;
                await storeDB.SaveChangesAsync();

                return Redirect(session.Url);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create a Stripe checkout session for order {OrderId}.", order.OrderId);
                order.PaymentStatus = PaymentStatus.Failed;
                await storeDB.SaveChangesAsync();

                ModelState.AddModelError(string.Empty,
                    "We couldn't start the payment. Your cart is unchanged - please try again.");
                return View(order);
            }
        }

        //
        // GET: /Checkout/Complete/5  (Stripe success_url, includes session_id)

        public async Task<IActionResult> Complete(int id, [FromQuery(Name = "session_id")] string? sessionId)
        {
            var order = await FindOrderAsync(id, User.Identity!.Name);
            if (order == null)
            {
                ViewBag.ErrorMessage = "We couldn't find that order for your account. Please review your recent orders or try checkout again.";
                return View("Error");
            }

            int referralBonus = 0;

            // Verify and finalize a pending Stripe order from the success redirect. This is
            // idempotent with the webhook — whichever arrives first wins.
            if (order.PaymentStatus == PaymentStatus.Pending && !string.IsNullOrWhiteSpace(sessionId))
            {
                try
                {
                    var status = await paymentService.GetSessionStatusAsync(sessionId);
                    if (status.Status == PaymentStatus.Paid)
                    {
                        order.PaymentStatus = PaymentStatus.Paid;
                        order.PaymentIntentId = status.PaymentIntentId;
                        order.PaymentReference = sessionId;
                        order.PaidDate = DateTime.Now;

                        await RedeemRecordedGiftCardAsync(order);
                        await RedeemRecordedCouponAsync(order);
                        ClearSessionDiscountCode();

                        // Finalize: clear the cart now that payment is captured.
                        var cart = ShoppingCart.GetCart(storeDB, HttpContext);
                        await cart.EmptyCartAsync();

                        await storeDB.SaveChangesAsync();

                        referralBonus = await ApplyRecordedLoyaltyAsync(order);
                        await storeEmail.SendOrderConfirmationAsync(order);
                    }
                    else if (status.Status is PaymentStatus.Cancelled or PaymentStatus.Failed)
                    {
                        order.PaymentStatus = status.Status;
                        await storeDB.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to verify Stripe session {SessionId} for order {OrderId}.", sessionId, order.OrderId);
                }
            }

            var viewer = await userManager.GetUserAsync(User);
            var viewerTier = loyalty.GetTier(viewer?.LifetimeSpend ?? 0m);
            ViewBag.PointsEarned = order.LoyaltyPointsEarned;
            ViewBag.PointsRedeemed = order.LoyaltyPointsRedeemed;
            ViewBag.LoyaltyDiscount = order.LoyaltyDiscount;
            ViewBag.OrderTotal = order.Total;
            ViewBag.NewBalance = viewer?.LoyaltyPoints ?? 0;
            ViewBag.TierName = viewerTier.Name;
            ViewBag.ReferralBonus = referralBonus != 0 ? referralBonus : Convert.ToInt32(TempData["ReferralBonus"] ?? 0);

            return View(order);
        }

        //
        // GET: /Checkout/Cancel/5  (Stripe cancel_url)

        public async Task<IActionResult> Cancel(int id)
        {
            var order = await FindOrderAsync(id, User.Identity!.Name);
            if (order != null && order.PaymentStatus == PaymentStatus.Pending)
            {
                order.PaymentStatus = PaymentStatus.Cancelled;
                await storeDB.SaveChangesAsync();
            }

            TempData["CartMessage"] =
                "Your payment was cancelled, so we kept everything in your cart. You can try checking out again whenever you're ready.";
            return RedirectToAction("Index", "ShoppingCart");
        }

        //
        // POST: /Checkout/Webhook  (Stripe server-to-server callback)

        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Webhook()
        {
            string json;
            using (var reader = new StreamReader(Request.Body))
            {
                json = await reader.ReadToEndAsync();
            }

            var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
            var result = paymentService.HandleWebhook(json, signature);

            if (!result.Handled || result.Status is null)
            {
                return Ok();
            }

            Order? order = null;
            if (result.OrderId.HasValue)
            {
                order = await FindOrderAsync(result.OrderId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(result.PaymentIntentId))
            {
                order = (await storeDB.Orders
                    .Where(o => o.PaymentIntentId == result.PaymentIntentId)
                    .Take(1)
                    .ToListAsync()).FirstOrDefault();
            }

            if (order == null)
            {
                logger.LogWarning("Stripe webhook {EventType} did not match any order.", result.EventType);
                return Ok();
            }

            switch (result.Status.Value)
            {
                case PaymentStatus.Paid when order.PaymentStatus != PaymentStatus.Paid:
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.PaidDate = DateTime.Now;
                    if (!string.IsNullOrWhiteSpace(result.PaymentIntentId))
                        order.PaymentIntentId = result.PaymentIntentId;
                    if (!string.IsNullOrWhiteSpace(result.SessionId))
                        order.PaymentReference = result.SessionId;
                    await RedeemRecordedGiftCardAsync(order);
                    await RedeemRecordedCouponAsync(order);
                    if (!string.IsNullOrWhiteSpace(order.Username))
                        await new ShoppingCart(storeDB).EmptyCartForUserAsync(order.Username);
                    await storeDB.SaveChangesAsync();
                    await ApplyRecordedLoyaltyAsync(order);
                    await storeEmail.SendOrderConfirmationAsync(order);
                    break;

                case PaymentStatus.Failed when order.PaymentStatus == PaymentStatus.Pending:
                case PaymentStatus.Cancelled when order.PaymentStatus == PaymentStatus.Pending:
                    order.PaymentStatus = result.Status.Value;
                    await storeDB.SaveChangesAsync();
                    break;

                case PaymentStatus.Refunded when order.PaymentStatus != PaymentStatus.Refunded:
                    order.PaymentStatus = PaymentStatus.Refunded;
                    await storeDB.SaveChangesAsync();
                    break;
            }

            return Ok();
        }

        private async Task<Order?> FindOrderAsync(int id, string? username = null)
        {
            IQueryable<Order> query = storeDB.Orders.Where(o => o.OrderId == id);
            if (username != null)
            {
                query = query.Where(o => o.Username == username);
            }

            // Cosmos can't translate AnyAsync/FirstOrDefaultAsync predicates here; materialize Take(1).
            return (await query.Take(1).ToListAsync()).FirstOrDefault();
        }

        // Records the loyalty redemption/discount and earned points on the order. The points
        // balance itself is only moved by ApplyPurchaseAsync once the order is paid.
        private void ApplyLoyaltyToOrder(Order order, RedemptionResult redemption, ApplicationUser? user, LoyaltyTier tier, decimal subtotal)
        {
            order.LoyaltyPointsRedeemed = redemption.PointsApplied;
            order.LoyaltyDiscount = redemption.Discount;
            order.Total = subtotal - redemption.Discount;
            if (user != null)
            {
                order.LoyaltyPointsEarned = loyalty.CalculateEarnedPoints(subtotal, tier);
            }
        }

        // Credits earned points and deducts redeemed points once an order's payment is captured.
        // Runs inside the single Pending -> Paid transition so the balance moves exactly once.
        private async Task<int> ApplyRecordedLoyaltyAsync(Order order)
        {
            if (string.IsNullOrWhiteSpace(order.Username))
                return 0;

            var user = await userManager.FindByNameAsync(order.Username);
            if (user == null)
                return 0;

            var subtotal = order.Total + order.LoyaltyDiscount;
            var result = await loyalty.ApplyPurchaseAsync(user, subtotal, order.LoyaltyPointsRedeemed);
            return result.ReferralBonusPoints;
        }

        // Redeems the gift card recorded on a pending order once its card payment is captured.
        // Runs as part of the single Pending -> Paid transition so the card is charged down once.
        private async Task RedeemRecordedGiftCardAsync(Order order)
        {
            if (string.IsNullOrWhiteSpace(order.GiftCardCode) || order.GiftCardAmountApplied <= 0m)
                return;

            var card = await giftCards.GetActiveByCodeAsync(order.GiftCardCode);
            if (card == null)
            {
                logger.LogWarning("Gift card {Code} for order {OrderId} could not be found at capture.", order.GiftCardCode, order.OrderId);
                return;
            }

            var applied = giftCards.Redeem(card, order.GiftCardAmountApplied, order.OrderId, order.Username);
            order.GiftCardAmountApplied = applied;
        }

        // Redeems the discount code recorded on a pending order once its card payment is captured,
        // so a coupon's usage count only advances for orders that are actually paid.
        private async Task RedeemRecordedCouponAsync(Order order)
        {
            if (string.IsNullOrWhiteSpace(order.DiscountCode))
                return;

            var normalized = order.DiscountCode.Trim().ToUpperInvariant();

            // Cosmos can't translate server-side filters reliably here; load the codes then match.
            var codes = await storeDB.DiscountCodes.ToListAsync();
            var code = codes.FirstOrDefault(c => string.Equals(c.Code, normalized, StringComparison.OrdinalIgnoreCase));
            if (code != null)
            {
                code.TimesUsed += 1;
            }
        }

        private int ParseRequestedPoints()
        {
            var raw = Request.Form["PointsToRedeem"].ToString();
            return int.TryParse(raw, out var value) && value > 0 ? value : 0;
        }

        private void PopulateLoyaltyViewBag(ApplicationUser? user, decimal subtotal)
        {
            var points = user?.LoyaltyPoints ?? 0;
            var tier = loyalty.GetTier(user?.LifetimeSpend ?? 0m);

            ViewBag.LoyaltyPoints = points;
            ViewBag.LoyaltyTierName = tier.Name;
            ViewBag.LoyaltyMultiplier = tier.EarnMultiplier;
            ViewBag.MaxRedeemablePoints = loyalty.MaxRedeemablePoints(points, subtotal);
            ViewBag.PointsPerDollarRedeemed = loyalty.Options.PointsPerDollarRedeemed;
            ViewBag.RedemptionIncrement = loyalty.Options.RedemptionIncrement;
            ViewBag.PointsPerDollar = loyalty.Options.PointsPerDollar;
            ViewBag.CartSubtotal = subtotal;
            ViewBag.ProjectedEarn = loyalty.CalculateEarnedPoints(subtotal, tier);
        }

        private string? GetSessionDiscountCode() =>
            HttpContext.Session.GetString(ShoppingCart.DiscountCodeSessionKey);

        private void ClearSessionDiscountCode() =>
            HttpContext.Session.Remove(ShoppingCart.DiscountCodeSessionKey);
    }
}
