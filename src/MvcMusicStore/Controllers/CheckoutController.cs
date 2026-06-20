using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
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
        private readonly IPaymentService paymentService;
        private readonly IGiftCardService giftCards;
        private readonly IOrderEmailSender emailSender;
        private readonly ILogger<CheckoutController> logger;
        const string PromoCode = "FREE";

        public CheckoutController(
            MusicStoreEntities storeDb,
            IPaymentService paymentService,
            IGiftCardService giftCardService,
            IOrderEmailSender emailSender,
            ILogger<CheckoutController> logger)
        {
            storeDB = storeDb;
            this.paymentService = paymentService;
            giftCards = giftCardService;
            this.emailSender = emailSender;
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
            return View(new Order());
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public async Task<IActionResult> AddressAndPayment(Order order)
        {
            ViewBag.PaymentConfigured = paymentService.IsConfigured;

            var promoCode = Request.Form["PromoCode"].ToString().Trim();
            var giftCardCode = Request.Form["GiftCardCode"].ToString().Trim();
            ViewBag.PromoCode = promoCode;
            ViewBag.GiftCardCode = giftCardCode;

            if (!ModelState.IsValid)
                return View(order);

            var isFreePromo = string.Equals(promoCode, PromoCode, StringComparison.OrdinalIgnoreCase);

            // A non-empty promo code that isn't the valid one is rejected so shoppers get feedback.
            // An empty promo code means "pay with a gift card and/or card" and falls through.
            if (!string.IsNullOrWhiteSpace(promoCode) && !isFreePromo)
            {
                ModelState.AddModelError("PromoCode", "That promo code isn't valid. Leave it blank to pay by card.");
                return View(order);
            }

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            if (await cart.GetCountAsync() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            var cartTotal = await cart.GetTotalAsync();

            // Resolve an optional gift card. A gift card pays the order total down; the FREE promo
            // and/or a card payment cover any remainder. FREE makes the whole order free, so a gift
            // card is ignored in that case to preserve its balance.
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

                giftApplied = Math.Min(giftCard.Balance, cartTotal);
            }

            var remainingDue = cartTotal - giftApplied;

            order.OrderId = await storeDB.NextOrderIdAsync();
            order.Username = User.Identity!.Name!;
            order.OrderDate = DateTime.Now;

            // Path 1 — no payment provider needed: the FREE promo or a gift card covers the order.
            if (isFreePromo || remainingDue <= 0m)
            {
                await cart.CreateOrderAsync(order);

                if (isFreePromo)
                {
                    order.PaymentProvider = "Promo (FREE)";
                }
                else
                {
                    // Gift card covers the full total — redeem it now and record it on the order.
                    var applied = giftCards.Redeem(giftCard!, order.Total, order.OrderId, order.Username);
                    order.GiftCardCode = giftCard!.Code;
                    order.GiftCardAmountApplied = applied;
                    order.PaymentProvider = "Gift card";
                }

                order.AmountDue = 0m;
                order.PaymentStatus = PaymentStatus.Paid;
                order.PaidDate = DateTime.Now;

                storeDB.Orders.Add(order);
                await storeDB.SaveChangesAsync();

                await SendReceiptSafelyAsync(order);

                return RedirectToAction("Complete", new { id = order.OrderId });
            }

            // Path 2 — a balance remains and must be paid by card via Stripe Checkout.
            if (!paymentService.IsConfigured)
            {
                ModelState.AddModelError(string.Empty,
                    "Online card payment isn't configured yet. Enter promo code FREE, or use a gift card that covers your total.");
                return View(order);
            }

            var items = await cart.GetCartItemsAsync();

            // Persist a pending order so the address and line items survive the redirect to Stripe.
            // The cart is intentionally NOT emptied, and any gift card is NOT redeemed, until capture.
            await cart.BuildOrderAsync(order);
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

                var session = await paymentService.CreateCheckoutSessionAsync(order, items, successUrl, cancelUrl, giftApplied);

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

        public async Task<IActionResult> Complete(int id, [FromQuery(Name = "session_id")] string? sessionId)
        {
            var order = await FindOrderAsync(id, User.Identity!.Name);
            if (order == null)
            {
                ViewBag.ErrorMessage = "We couldn't find that order for your account. Please review your recent orders or try checkout again.";
                return View("Error");
            }

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

                        await SendReceiptSafelyAsync(order);
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
                    await SendReceiptSafelyAsync(order);
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

        // Sends the confirmation receipt once an order is paid. A delivery failure must never
        // surface to the shopper or roll back a captured payment, so failures are only logged.
        private async Task SendReceiptSafelyAsync(Order order)
        {
            try
            {
                await emailSender.SendOrderConfirmationAsync(order);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send confirmation email for order {OrderId}.", order.OrderId);
            }
        }
    }
}
