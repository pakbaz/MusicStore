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
        private readonly IOrderEmailSender emailSender;
        private readonly ILogger<CheckoutController> logger;

        public CheckoutController(
            MusicStoreEntities storeDb,
            IOrderEmailSender emailSender,
            ILogger<CheckoutController> logger)
        {
            storeDB = storeDb;
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

            return View(new Order());
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public async Task<IActionResult> AddressAndPayment(Order order)
        {
            // Promo codes are optional and informational only for now; preserve any entered
            // value when redisplaying the form. Real validation/discounts are tracked separately.
            ViewBag.PromoCode = Request.Form["PromoCode"].ToString();

            if (!ModelState.IsValid)
                return View(order);

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            if (await cart.GetCountAsync() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            order.OrderId = await storeDB.NextOrderIdAsync();
            order.Username = User.Identity!.Name!;
            order.OrderDate = DateTime.Now;
            order.Status = "Paid";

            // Process the order (builds embedded order details and empties the cart)
            await cart.CreateOrderAsync(order);

            // Add the order
            storeDB.Orders.Add(order);

            // Save all changes
            await storeDB.SaveChangesAsync();

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

            if (order != null)
            {
                return View(order);
            }

            ViewBag.ErrorMessage = "We couldn't find that order for your account. Please review your recent orders or try checkout again.";
            return View("Error");
        }
    }
}