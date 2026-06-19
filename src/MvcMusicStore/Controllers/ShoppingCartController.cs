using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly MusicStoreEntities storeDB;

        public ShoppingCartController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        //
        // GET: /ShoppingCart/

        public async Task<IActionResult> Index()
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);

            // Set up our ViewModel
            var viewModel = new ShoppingCartViewModel
            {
                CartItems = await cart.GetCartItemsAsync(),
                CartTotal = await cart.GetTotalAsync()
            };

            ViewBag.CartMessage = TempData["CartMessage"];

            // Return the view
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

            var itemSubtotal = itemCount > 0
                ? (cartItem.Album?.Price ?? decimal.Zero) * itemCount
                : decimal.Zero;

            var message = itemCount > 0
                ? $"1 copy of {albumName} has been removed from your shopping cart."
                : $"{albumName} has been removed from your shopping cart.";

            return Json(await BuildCartResponseAsync(cart, id, itemCount, itemSubtotal, message));
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
            var unitPrice = cartItem.Album?.Price ?? decimal.Zero;
            var itemCount = await cart.UpdateCartItemCountAsync(id, count);

            await storeDB.SaveChangesAsync();

            var itemSubtotal = unitPrice * itemCount;
            var message = itemCount == 0
                ? $"{albumName} has been removed from your shopping cart."
                : $"{albumName} quantity has been updated to {itemCount}.";

            return Json(await BuildCartResponseAsync(cart, id, itemCount, itemSubtotal, message));
        }

        private async Task<ShoppingCartRemoveViewModel> BuildCartResponseAsync(ShoppingCart cart, int id, int itemCount, decimal itemSubtotal, string message)
        {
            var cartItems = (await cart.GetCartItemsAsync())
                .Where(item => item.Album != null)
                .OrderBy(item => item.Album!.Title)
                .ToList();

            var cartSummary = string.Join("\n", cartItems.Select(item => $"{item.Album!.Title} x{item.Count}"));

            return new ShoppingCartRemoveViewModel
            {
                Message = message,
                CartTotal = await cart.GetTotalAsync(),
                CartCount = await cart.GetCountAsync(),
                ItemCount = itemCount,
                ItemSubtotal = itemSubtotal,
                CartSummary = cartSummary,
                DeleteId = id
            };
        }
    }
}
