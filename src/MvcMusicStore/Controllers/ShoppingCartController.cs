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

            // Get the name of the album to display confirmation
            string albumName = storeDB.Carts
                .Single(item => item.RecordId == id).Album!.Title!;

            // Remove from cart
            int itemCount = cart.RemoveFromCart(id);

            storeDB.SaveChanges();

            string removed = (itemCount > 0) ? " 1 copy of " : string.Empty;

            // Display the confirmation message

            var results = new ShoppingCartRemoveViewModel
            {
                Message = removed + albumName +
                    " has been removed from your shopping cart.",
                CartTotal = cart.GetTotal(),
                CartCount = cart.GetCount(),
                ItemCount = itemCount,
                DeleteId = id
            };

            return Json(results);
        }
    }
}
