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
            return View();
        }

        //
        // POST: /Checkout/AddressAndPayment

        [HttpPost]
        public IActionResult AddressAndPayment(Order order)
        {
            if (!ModelState.IsValid)
                return View(order);

            try
            {
                // Check promo code from form
                var promoCode = Request.Form["PromoCode"].ToString();
                if (!string.Equals(promoCode, PromoCode, StringComparison.OrdinalIgnoreCase))
                {
                    return View(order);
                }

                order.Username = User.Identity!.Name!;
                order.OrderDate = DateTime.Now;

                //Add the Order
                storeDB.Orders.Add(order);

                //Process the order
                var cart = ShoppingCart.GetCart(storeDB, HttpContext);
                cart.CreateOrder(order);

                // Save all changes
                storeDB.SaveChanges();

                return RedirectToAction("Complete",
                    new { id = order.OrderId });
            }
            catch
            {
                //Invalid - redisplay with errors
                return View(order);
            }
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
            else
            {
                return View("Error");
            }
        }
    }
}