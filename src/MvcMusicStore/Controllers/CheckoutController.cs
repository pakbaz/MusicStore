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
        private readonly IPromotionService promotions;

        public CheckoutController(MusicStoreEntities storeDb, IPromotionService promotions)
        {
            storeDB = storeDb;
            this.promotions = promotions;
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

            ViewBag.Pricing = await promotions.PriceCartAsync(cartItems, GetSessionDiscountCode());
            return View(new Order());
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public async Task<IActionResult> AddressAndPayment(Order order)
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var cartItems = await cart.GetCartItemsAsync();
            if (cartItems.Count == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            // Re-price against live sales + the code held in session (it may have expired since the cart).
            var pricing = await promotions.PriceCartAsync(cartItems, GetSessionDiscountCode());

            if (!ModelState.IsValid)
            {
                ViewBag.Pricing = pricing;
                return View(order);
            }

            order.OrderId = await storeDB.NextOrderIdAsync();
            order.Username = User.Identity!.Name!;
            order.OrderDate = DateTime.Now;

            // Builds embedded order details at the charged (sale-adjusted) prices, records the
            // discount, sets the total, and empties the cart.
            await cart.CreateOrderAsync(order, pricing);

            storeDB.Orders.Add(order);

            // Redeem the coupon (same tracked DbContext, persisted in the SaveChanges below).
            if (pricing.AppliedDiscountCode is { } redeemedCode)
            {
                redeemedCode.TimesUsed += 1;
            }

            await storeDB.SaveChangesAsync();

            ClearSessionDiscountCode();

            return RedirectToAction("Complete", new { id = order.OrderId });
        }

        //
        // GET: /Checkout/Complete

        public async Task<IActionResult> Complete(int id)
        {
            // Validate customer owns this order. Cosmos cannot translate AnyAsync() (EXISTS subquery),
            // so materialize a single matching id with Take(1) instead.
            IQueryable<Order> orders = storeDB.Orders;
            var matchingOrder = await orders
                .Where(o => o.OrderId == id && o.Username == User.Identity!.Name)
                .Select(o => o.OrderId)
                .Take(1)
                .ToListAsync();
            bool isValid = matchingOrder.Count != 0;

            if (isValid)
            {
                return View(id);
            }

            ViewBag.ErrorMessage = "We couldn't find that order for your account. Please review your recent orders or try checkout again.";
            return View("Error");
        }

        private string? GetSessionDiscountCode() =>
            HttpContext.Session.GetString(ShoppingCart.DiscountCodeSessionKey);

        private void ClearSessionDiscountCode() =>
            HttpContext.Session.Remove(ShoppingCart.DiscountCodeSessionKey);
    }
}
