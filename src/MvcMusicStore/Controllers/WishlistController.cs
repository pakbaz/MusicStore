using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class WishlistController : Controller
    {
        private readonly MusicStoreEntities storeDB;

        public WishlistController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        //
        // GET: /Wishlist/

        public async Task<IActionResult> Index()
        {
            var wishlist = Wishlist.GetWishlist(storeDB, HttpContext);

            var viewModel = new WishlistViewModel
            {
                WishlistItems = (await wishlist.GetWishlistItemsAsync())
                    .OrderByDescending(item => item.DateCreated)
                    .ToList()
            };

            ViewBag.WishlistMessage = TempData["WishlistMessage"];

            return View(viewModel);
        }

        //
        // POST: /Wishlist/ToggleSave
        // Used by the "Save for later" toggle on catalog cards and album details.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSave(int albumId, string? returnUrl = null)
        {
            var album = await storeDB.Albums.SingleOrDefaultAsync(a => a.AlbumId == albumId);

            if (album == null)
            {
                return NotFound();
            }

            var wishlist = Wishlist.GetWishlist(storeDB, HttpContext);

            if (await wishlist.ContainsAlbumAsync(albumId))
            {
                await wishlist.RemoveAlbumAsync(albumId);
                TempData["WishlistMessage"] = $"{album.Title} was removed from your wishlist.";
            }
            else
            {
                await wishlist.AddToWishlistAsync(album);
                TempData["WishlistMessage"] = $"{album.Title} was saved to your wishlist.";
            }

            await storeDB.SaveChangesAsync();

            return RedirectToReturnUrl(returnUrl);
        }

        //
        // AJAX: POST /Wishlist/RemoveFromWishlist/5

        [HttpPost]
        public async Task<IActionResult> RemoveFromWishlist(int id)
        {
            var wishlist = Wishlist.GetWishlist(storeDB, HttpContext);

            var item = (await wishlist.GetWishlistItemsAsync())
                .SingleOrDefault(i => i.RecordId == id);

            if (item == null)
            {
                return NotFound("Wishlist item not found.");
            }

            var albumName = item.Album?.Title ?? "This album";

            await wishlist.RemoveFromWishlistAsync(id);
            await storeDB.SaveChangesAsync();

            return Json(await BuildResponseAsync(
                wishlist, id, $"{albumName} has been removed from your wishlist."));
        }

        //
        // AJAX: POST /Wishlist/MoveToCart/5

        [HttpPost]
        public async Task<IActionResult> MoveToCart(int id)
        {
            var wishlist = Wishlist.GetWishlist(storeDB, HttpContext);

            var item = (await wishlist.GetWishlistItemsAsync())
                .SingleOrDefault(i => i.RecordId == id);

            if (item == null)
            {
                return NotFound("Wishlist item not found.");
            }

            var album = await storeDB.Albums.SingleOrDefaultAsync(a => a.AlbumId == item.AlbumId)
                ?? item.Album;

            var albumName = album?.Title ?? "This album";

            if (album != null)
            {
                var cart = ShoppingCart.GetCart(storeDB, HttpContext);
                await cart.AddToCartAsync(album);
            }

            await wishlist.RemoveFromWishlistAsync(id);
            await storeDB.SaveChangesAsync();

            return Json(await BuildResponseAsync(
                wishlist, id, $"{albumName} has been moved to your shopping cart."));
        }

        private async Task<WishlistActionViewModel> BuildResponseAsync(Wishlist wishlist, int id, string message)
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var cartItems = (await cart.GetCartItemsAsync())
                .Where(item => item.Album != null)
                .OrderBy(item => item.Album!.Title)
                .ToList();

            return new WishlistActionViewModel
            {
                Message = message,
                WishlistCount = await wishlist.GetCountAsync(),
                CartCount = cartItems.Sum(item => item.Count),
                CartSummary = string.Join("\n", cartItems.Select(item => $"{item.Album!.Title} x{item.Count}")),
                ItemId = id
            };
        }

        private IActionResult RedirectToReturnUrl(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Store");
        }
    }
}
