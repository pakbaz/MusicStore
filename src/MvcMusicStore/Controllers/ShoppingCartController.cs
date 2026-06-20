using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly MusicStoreEntities storeDB;
        private readonly IPromotionService promotions;

        public ShoppingCartController(MusicStoreEntities storeDb, IPromotionService promotions)
        {
            storeDB = storeDb;
            this.promotions = promotions;
        }

        //
        // GET: /ShoppingCart/

        public async Task<IActionResult> Index()
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var cartItems = await cart.GetCartItemsAsync();
            var pricing = await promotions.PriceCartAsync(cartItems, GetSessionDiscountCode());

            // A stored code that has since expired or hit its limit is dropped silently here.
            if (!string.IsNullOrEmpty(GetSessionDiscountCode()) && !pricing.DiscountApplied)
            {
                ClearSessionDiscountCode();
            }

            var viewModel = new ShoppingCartViewModel
            {
                CartItems = cartItems,
                Pricing = pricing,
                CartTotal = pricing.Total,
                DiscountMessage = TempData["DiscountMessage"] as string
            };

            ViewBag.CartMessage = TempData["CartMessage"];

            return View(viewModel);
        }

        //
        // GET: /ShoppingCart/AddToCart/5

        public async Task<IActionResult> AddToCart(int id)
        {
            // Retrieve the album from the database
            var addedAlbum = await storeDB.Albums
                .SingleAsync(album => album.AlbumId == id);

            // Add it to the shopping cart
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);

            await cart.AddToCartAsync(addedAlbum);

            await storeDB.SaveChangesAsync();

            // Go back to the main store page for more shopping
            return RedirectToAction("Index");
        }

        //
        // POST: /ShoppingCart/ApplyDiscount

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyDiscount(string? code)
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var pricing = await promotions.PriceCartAsync(await cart.GetCartItemsAsync(), code);

            if (pricing.DiscountApplied)
            {
                SetSessionDiscountCode(pricing.AppliedCode!);
                TempData["DiscountMessage"] = pricing.DiscountMessage;
            }
            else
            {
                ClearSessionDiscountCode();
                TempData["DiscountMessage"] = pricing.DiscountMessage
                    ?? "That discount code could not be applied.";
            }

            return RedirectToAction("Index");
        }

        //
        // POST: /ShoppingCart/RemoveDiscount

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveDiscount()
        {
            ClearSessionDiscountCode();
            TempData["DiscountMessage"] = "Discount code removed.";
            return RedirectToAction("Index");
        }

        //
        // AJAX: /ShoppingCart/RemoveFromCart/5

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            // Retrieve the current user's shopping cart
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);

            var cartItem = (await cart.GetCartItemsAsync()).SingleOrDefault(item => item.RecordId == id);
            if (cartItem == null)
            {
                return NotFound("Cart item not found.");
            }

            var albumName = cartItem.Album?.Title ?? "This album";

            // Remove from cart
            int itemCount = await cart.RemoveFromCartAsync(id);

            await storeDB.SaveChangesAsync();

            var message = itemCount > 0
                ? $"1 copy of {albumName} has been removed from your shopping cart."
                : $"{albumName} has been removed from your shopping cart.";

            return Json(await BuildCartResponseAsync(cart, id, message));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCart(int id, int count)
        {
            if (count < 0)
            {
                return BadRequest("Quantity cannot be negative.");
            }

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var cartItem = (await cart.GetCartItemsAsync()).SingleOrDefault(item => item.RecordId == id);

            if (cartItem == null)
            {
                return NotFound("Cart item not found.");
            }

            var albumName = cartItem.Album?.Title ?? "This album";
            var itemCount = await cart.UpdateCartItemCountAsync(id, count);

            await storeDB.SaveChangesAsync();

            var message = itemCount == 0
                ? $"{albumName} has been removed from your shopping cart."
                : $"{albumName} quantity has been updated to {itemCount}.";

            return Json(await BuildCartResponseAsync(cart, id, message));
        }

        private async Task<ShoppingCartRemoveViewModel> BuildCartResponseAsync(ShoppingCart cart, int id, string message)
        {
            var cartItems = await cart.GetCartItemsAsync();
            var pricing = await promotions.PriceCartAsync(cartItems, GetSessionDiscountCode());

            // Drop a now-invalid stored code so totals stay honest.
            if (!string.IsNullOrEmpty(GetSessionDiscountCode()) && !pricing.DiscountApplied)
            {
                ClearSessionDiscountCode();
            }

            var line = pricing.Lines.FirstOrDefault(l => l.RecordId == id);
            var itemCount = line?.Quantity ?? 0;
            var itemSubtotal = line?.LineSubtotal ?? decimal.Zero;

            var summaryLines = pricing.Lines
                .OrderBy(l => l.Title)
                .Select(l => $"{l.Title} x{l.Quantity}");
            var cartSummary = string.Join("\n", summaryLines);

            return new ShoppingCartRemoveViewModel
            {
                Message = message,
                CartTotal = pricing.Total,
                CartCount = pricing.Lines.Sum(l => l.Quantity),
                ItemCount = itemCount,
                ItemSubtotal = itemSubtotal,
                CartSummary = cartSummary,
                DeleteId = id,
                Subtotal = pricing.Subtotal,
                DiscountAmount = pricing.DiscountAmount,
                DiscountCode = pricing.AppliedCode,
                DiscountApplied = pricing.DiscountApplied
            };
        }

        private string? GetSessionDiscountCode() =>
            HttpContext.Session.GetString(ShoppingCart.DiscountCodeSessionKey);

        private void SetSessionDiscountCode(string code) =>
            HttpContext.Session.SetString(ShoppingCart.DiscountCodeSessionKey, code);

        private void ClearSessionDiscountCode() =>
            HttpContext.Session.Remove(ShoppingCart.DiscountCodeSessionKey);
    }
}
