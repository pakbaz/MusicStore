using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;

namespace MvcMusicStore.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly MusicStoreEntities storeDB;
        const string PromoCode = "FREE";

        public CheckoutController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
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

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            if (await cart.GetCountAsync() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            order.OrderId = await storeDB.NextOrderIdAsync();
            order.Username = User.Identity!.Name!;
            order.OrderDate = DateTime.Now;

            // Process the order (builds embedded order details and empties the cart)
            await cart.CreateOrderAsync(order);

            // Add the order
            storeDB.Orders.Add(order);

            // Save all changes
            await storeDB.SaveChangesAsync();

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
    }
}