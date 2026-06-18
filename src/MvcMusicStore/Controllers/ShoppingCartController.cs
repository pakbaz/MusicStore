using System.Linq;
using Microsoft.AspNetCore.Mvc;
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

        public IActionResult Index()
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);

            // Set up our ViewModel
            var viewModel = new ShoppingCartViewModel
            {
                CartItems = cart.GetCartItems(),
                CartTotal = cart.GetTotal()
            };

            ViewBag.CartMessage = TempData["CartMessage"];

            // Return the view
            return View(viewModel);
        }

        //
        // GET: /ShoppingCart/AddToCart/5

        public IActionResult AddToCart(int id)
        {

            // Retrieve the album from the database
            var addedAlbum = storeDB.Albums
                .Single(album => album.AlbumId == id);

            // Add it to the shopping cart
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);

            cart.AddToCart(addedAlbum);

            storeDB.SaveChanges();

            // Go back to the main store page for more shopping
            return RedirectToAction("Index");
        }

        //
        // AJAX: /ShoppingCart/RemoveFromCart/5

        [HttpPost]
        public IActionResult RemoveFromCart(int id)
        {
            // Retrieve the current user's shopping cart
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);

            var cartItem = cart.GetCartItems().SingleOrDefault(item => item.RecordId == id);
            if (cartItem == null)
            {
                return NotFound("Cart item not found.");
            }

            var albumName = cartItem.Album?.Title ?? "This album";

            // Remove from cart
            int itemCount = cart.RemoveFromCart(id);

            storeDB.SaveChanges();

            var itemSubtotal = itemCount > 0
                ? (cartItem.Album?.Price ?? decimal.Zero) * itemCount
                : decimal.Zero;

            var message = itemCount > 0
                ? $"1 copy of {albumName} has been removed from your shopping cart."
                : $"{albumName} has been removed from your shopping cart.";

            return Json(BuildCartResponse(cart, id, itemCount, itemSubtotal, message));
        }

        [HttpPost]
        public IActionResult UpdateCart(int id, int count)
        {
            if (count < 0)
            {
                return BadRequest("Quantity cannot be negative.");
            }

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var cartItem = cart.GetCartItems().SingleOrDefault(item => item.RecordId == id);

            if (cartItem == null)
            {
                return NotFound("Cart item not found.");
            }

            var albumName = cartItem.Album?.Title ?? "This album";
            var unitPrice = cartItem.Album?.Price ?? decimal.Zero;
            var itemCount = cart.UpdateCartItemCount(id, count);

            storeDB.SaveChanges();

            var itemSubtotal = unitPrice * itemCount;
            var message = itemCount == 0
                ? $"{albumName} has been removed from your shopping cart."
                : $"{albumName} quantity has been updated to {itemCount}.";

            return Json(BuildCartResponse(cart, id, itemCount, itemSubtotal, message));
        }

        private ShoppingCartRemoveViewModel BuildCartResponse(ShoppingCart cart, int id, int itemCount, decimal itemSubtotal, string message)
        {
            var cartItems = cart.GetCartItems()
                .Where(item => item.Album != null)
                .OrderBy(item => item.Album!.Title)
                .ToList();

            var cartSummary = string.Join("\n", cartItems.Select(item => $"{item.Album!.Title} x{item.Count}"));

            return new ShoppingCartRemoveViewModel
            {
                Message = message,
                CartTotal = cart.GetTotal(),
                CartCount = cart.GetCount(),
                ItemCount = itemCount,
                ItemSubtotal = itemSubtotal,
                CartSummary = cartSummary,
                DeleteId = id
            };
        }
    }
}
