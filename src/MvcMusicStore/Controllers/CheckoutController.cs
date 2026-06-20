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
    // Guest checkout is allowed: anonymous shoppers can complete a purchase with just an email.
    // Authenticated users keep the existing behavior (order tied to their account).
    [AllowAnonymous]
    public class CheckoutController : Controller
    {
        private readonly MusicStoreEntities storeDB;
        private readonly IPaymentService paymentService;
        private readonly IGiftCardService giftCards;
        private readonly ILoyaltyService loyalty;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly StoreEmailService storeEmail;
        private readonly ILogger<CheckoutController> logger;
        const string PromoCode = "FREE";

        public CheckoutController(
            MusicStoreEntities storeDb,
            IPaymentService paymentService,
            IGiftCardService giftCardService,
            ILoyaltyService loyaltyService,
            UserManager<ApplicationUser> userManager,
            StoreEmailService storeEmail,
            ILogger<CheckoutController> logger)
        {
            storeDB = storeDb;
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
            if (await cart.GetCountAsync() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            ViewBag.PaymentConfigured = paymentService.IsConfigured;
            var user = await userManager.GetUserAsync(User);
            PopulateLoyaltyViewBag(user, await cart.GetTotalAsync());

            return View(new Order());
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public async Task<IActionResult> AddressAndPayment(Order order)
        {
            var promoCode = Request.Form["PromoCode"].ToString().Trim();
            var giftCardCode = Request.Form["GiftCardCode"].ToString().Trim();
            ViewBag.PromoCode = promoCode;
            ViewBag.GiftCardCode = giftCardCode;
            ViewBag.PaymentConfigured = paymentService.IsConfigured;

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var user = await userManager.GetUserAsync(User);
            var cartTotal = await cart.GetTotalAsync();
            PopulateLoyaltyViewBag(user, cartTotal);

            if (!ModelState.IsValid)
                return View(order);

            if (await cart.GetCountAsync() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            var isFreePromo = string.Equals(promoCode, PromoCode, StringComparison.OrdinalIgnoreCase);

            // A non-empty promo code that isn't the valid one is rejected so shoppers get feedback.
            // An empty promo code means "pay with loyalty points, a gift card, and/or card".
            if (!string.IsNullOrWhiteSpace(promoCode) && !isFreePromo)
            {
                ModelState.AddModelError("PromoCode", "That promo code isn't valid. Leave it blank to pay by card.");
                return View(order);
            }

            // Loyalty redemption is computed first; it discounts the amount owed before any gift
            // card or card charge. The discount is recorded on the order and the points balance is
            // only moved once payment is captured.
            var requestedPoints = ParseRequestedPoints();
            var redemption = loyalty.ComputeRedemption(requestedPoints, user?.LoyaltyPoints ?? 0, cartTotal);
            var discountedTotal = cartTotal - redemption.Discount;

            // Resolve an optional gift card against the post-loyalty total. The FREE promo makes the
            // whole order free, so a gift card is ignored in that case to preserve its balance.
            GiftCard? giftCard = null;
            decimal giftApplied = 0m;
            if (!isFreePromo && !string.IsNullOrWhiteSpace(giftCardCode))
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
            order.OrderDate = DateTime.Now;

            string? guestToken = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                order.Username = User.Identity!.Name!;
            }
            else
            {
                // Guest order: stamp a random token so the confirmation and download links remain
                // accessible (including from the emailed link) without requiring an account.
                guestToken = Guid.NewGuid().ToString("N");
                order.GuestAccessToken = guestToken;
            }

            var tier = loyalty.GetTier(user?.LifetimeSpend ?? 0m);

            // Path 1 — no card payment needed: FREE, a gift card, or loyalty points cover the total.
            if (isFreePromo || remainingDue <= 0m)
            {
                await cart.CreateOrderAsync(order);
                ApplyLoyaltyToOrder(order, redemption, user, tier, cartTotal);

                if (isFreePromo)
                {
                    order.PaymentProvider = "Promo (FREE)";
                }
                else if (giftCard != null && giftApplied > 0m)
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
                await storeDB.SaveChangesAsync();

                if (user != null)
                {
                    var loyaltyResult = await loyalty.ApplyPurchaseAsync(user, cartTotal, redemption.PointsApplied);
                    TempData["ReferralBonus"] = loyaltyResult.ReferralBonusPoints;
                }

                await storeEmail.SendOrderConfirmationAsync(order);

                return RedirectToAction("Complete", new { id = order.OrderId, token = guestToken });
            }

            // Path 2 — a balance remains and must be paid by card via Stripe Checkout.
            if (!paymentService.IsConfigured)
            {
                ModelState.AddModelError(string.Empty,
                    "Online card payment isn't configured yet. Enter promo code FREE, or use loyalty points / a gift card that cover your total.");
                return View(order);
            }

            var items = await cart.GetCartItemsAsync();

            // Persist a pending order so the address and line items survive the redirect to Stripe.
            // The cart is intentionally NOT emptied, the gift card is NOT redeemed, and loyalty
            // points are NOT moved until the payment is captured.
            await cart.BuildOrderAsync(order);
            ApplyLoyaltyToOrder(order, redemption, user, tier, cartTotal);

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
                var completeUrl = Url.Action("Complete", "Checkout", new { id = order.OrderId, token = guestToken }, Request.Scheme)!;
                var successUrl = completeUrl
                    + (completeUrl.Contains('?') ? "&" : "?")
                    + "session_id={CHECKOUT_SESSION_ID}";
                var cancelUrl = Url.Action("Cancel", "Checkout", new { id = order.OrderId }, Request.Scheme)!;

                // Stripe charges the post-loyalty, post-gift-card remainder.
                var discountAmount = redemption.Discount + giftApplied;
                var session = await paymentService.CreateCheckoutSessionAsync(order, items, successUrl, cancelUrl, discountAmount);

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
                    "We couldn't start the payment. Your cart is unchanged — please try again.");
                return View(order);
            }
        }

        //
        // GET: /Checkout/Complete/5  (Stripe success_url, includes session_id)

        public async Task<IActionResult> Complete(int id, [FromQuery(Name = "session_id")] string? sessionId, string? token)
        {
            // Authenticated owners are matched by username; guests by the access token issued at
            // checkout (and embedded in the confirmation link/email).
            var order = await FindOrderAsync(id);
            if (order == null || !IsOrderAccessible(order, token))
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
            ViewBag.Downloads = await BuildDownloadsAsync(order);

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

        // Authorizes access to an order's confirmation page: the authenticated owner (username
        // match) or anyone presenting the guest access token issued when the order was placed.
        private bool IsOrderAccessible(Order order, string? token)
        {
            if (User.Identity?.IsAuthenticated == true &&
                string.Equals(order.Username, User.Identity!.Name, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.IsNullOrEmpty(token) &&
                   !string.IsNullOrEmpty(order.GuestAccessToken) &&
                   string.Equals(order.GuestAccessToken, token, StringComparison.Ordinal);
        }

        // Builds digital download links (title -> absolute URL) for the order's albums that carry
        // audio. An album with an AudioUrl is a digital item the buyer can download immediately.
        private async Task<List<KeyValuePair<string, string>>> BuildDownloadsAsync(Order order)
        {
            var downloads = new List<KeyValuePair<string, string>>();
            foreach (var detail in order.OrderDetails ?? new List<OrderDetail>())
            {
                var album = (await storeDB.Albums
                    .Where(a => a.AlbumId == detail.AlbumId)
                    .Take(1)
                    .ToListAsync()).FirstOrDefault();
                if (album != null && !string.IsNullOrWhiteSpace(album.AudioUrl))
                {
                    downloads.Add(new KeyValuePair<string, string>(
                        album.Title ?? $"Album #{detail.AlbumId}",
                        ToAbsoluteUrl(album.AudioUrl!)));
                }
            }

            return downloads;
        }

        private string ToAbsoluteUrl(string url)
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            var path = url.StartsWith('/') ? url : "/" + url;
            return $"{Request.Scheme}://{Request.Host}{path}";
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
    }
}
