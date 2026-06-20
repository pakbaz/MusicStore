using System;
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
        private readonly IGiftCardService giftCards;
        const string PromoCode = "FREE";

        public CheckoutController(MusicStoreEntities storeDb, IGiftCardService giftCardService)
        {
            storeDB = storeDb;
            giftCards = giftCardService;
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

            return View(new Order());
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public async Task<IActionResult> AddressAndPayment(Order order)
        {
            var promoCode = Request.Form["PromoCode"].ToString();
            var giftCardCode = Request.Form["GiftCardCode"].ToString();
            ViewBag.PromoCode = promoCode;
            ViewBag.GiftCardCode = giftCardCode;

            if (!ModelState.IsValid)
                return View(order);

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            if (await cart.GetCountAsync() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            var cartTotal = await cart.GetTotalAsync();

            // Resolve an optional gift card. The gift card is a payment method that pays the order
            // total down; the FREE promo is a discount that clears any remainder.
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

                giftApplied = Math.Min(giftCard.Balance, cartTotal);
            }

            var remainingDue = cartTotal - giftApplied;
            var promoValid = string.Equals(promoCode, PromoCode, StringComparison.OrdinalIgnoreCase);

            // An order can be placed only when the remainder after the gift card is fully covered,
            // either by the gift card itself or by the FREE promo discount.
            if (remainingDue > 0 && !promoValid)
            {
                ModelState.AddModelError("PromoCode", "Enter promo code FREE or a gift card that covers your order total to place your order.");
                return View(order);
            }

            order.OrderId = await storeDB.NextOrderIdAsync();
            order.Username = User.Identity!.Name!;
            order.OrderDate = DateTime.Now;

            // Process the order (builds embedded order details, sets Total, and empties the cart)
            await cart.CreateOrderAsync(order);

            // Apply the gift card, recording a redemption transaction on the tracked card.
            if (giftCard != null && giftApplied > 0)
            {
                var applied = giftCards.Redeem(giftCard, order.Total, order.OrderId, order.Username);
                order.GiftCardCode = giftCard.Code;
                order.GiftCardAmountApplied = applied;
            }

            order.AmountDue = promoValid ? 0m : Math.Max(0m, order.Total - order.GiftCardAmountApplied);

            // Add the order
            storeDB.Orders.Add(order);

            // Save all changes (new order + any gift-card balance update)
            await storeDB.SaveChangesAsync();

            return RedirectToAction("Complete", new { id = order.OrderId });
        }

        //
        // GET: /Checkout/Complete

        public async Task<IActionResult> Complete(int id)
        {
            // Validate customer owns this order. Cosmos cannot translate AnyAsync() (EXISTS subquery),
            // so materialize the matching order with Take(1) instead.
            var orders = await storeDB.Orders
                .Where(o => o.OrderId == id && o.Username == User.Identity!.Name)
                .Take(1)
                .ToListAsync();

            var order = orders.FirstOrDefault();
            if (order != null)
            {
                return View(order);
            }

            ViewBag.ErrorMessage = "We couldn't find that order for your account. Please review your recent orders or try checkout again.";
            return View("Error");
        }
    }
}