using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public IActionResult AddressAndPayment()
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            if (cart.GetCount() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            return View(new Order());
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public IActionResult AddressAndPayment(Order order)
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
            if (cart.GetCount() == 0)
            {
                TempData["CartMessage"] = "Your cart is empty. Add music before checking out.";
                return RedirectToAction("Index", "ShoppingCart");
            }

            order.Username = User.Identity!.Name!;
            order.OrderDate = DateTime.Now;

            // Add the order
            storeDB.Orders.Add(order);

            // Process the order
            cart.CreateOrder(order);

            // Save all changes
            storeDB.SaveChanges();

            return RedirectToAction("Complete", new { id = order.OrderId });
        }

        //
        // GET: /Checkout/Complete

        public IActionResult Complete(int id)
        {
            // Validate customer owns this order
            bool isValid = storeDB.Orders.Any(
                o => o.OrderId == id &&
                o.Username == User.Identity!.Name);

            if (isValid)
            {
                return View(id);
            }

            ViewBag.ErrorMessage = "We couldn't find that order for your account. Please review your recent orders or try checkout again.";
            return View("Error");
        }
    }
}