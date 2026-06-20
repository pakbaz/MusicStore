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
        private readonly IRecommendationService recommendationService;
        private readonly IBundleService bundleService;

        public ShoppingCartController(
            MusicStoreEntities storeDb,
            IPromotionService promotions,
            IRecommendationService recommendationService,
            IBundleService bundleService)
        {
            storeDB = storeDb;
            this.promotions = promotions;
            this.recommendationService = recommendationService;
            this.bundleService = bundleService;
        }

        // GET: /ShoppingCart/
        public async Task<IActionResult> Index()
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var viewModel = await BuildCartViewModelAsync(cart);
            ViewBag.CartMessage = TempData["CartMessage"];
            viewModel.DiscountMessage = TempData["DiscountMessage"] as string;
            return View(viewModel);
        }

        // GET: /ShoppingCart/AddToCart/5
        public async Task<IActionResult> AddToCart(int id)
        {
            var addedAlbum = await storeDB.Albums.SingleAsync(album => album.AlbumId == id);

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            await cart.AddToCartAsync(addedAlbum);
            await storeDB.SaveChangesAsync();

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

        // GET: /ShoppingCart/AddBundleToCart/5
        public async Task<IActionResult> AddBundleToCart(int id)
        {
            var bundle = await bundleService.GetBundleAsync(id);
            if (bundle == null || !bundle.IsActive)
            {
                return NotFound();
            }

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            await cart.AddBundleToCartAsync(bundle);
            await storeDB.SaveChangesAsync();

            TempData["CartMessage"] = $"Added the \u201c{bundle.Title}\u201d bundle to your cart.";
            return RedirectToAction("Index");
        }

        // POST: /ShoppingCart/AddToCartAjax
        [HttpPost]
        public async Task<IActionResult> AddToCartAjax(int id)
        {
            var album = await storeDB.Albums.SingleOrDefaultAsync(a => a.AlbumId == id);
            if (album == null)
            {
                return NotFound("Album not found.");
            }

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            await cart.AddToCartAsync(album);
            await storeDB.SaveChangesAsync();

            var viewModel = await BuildCartViewModelAsync(cart, $"{album.Title} has been added to your cart.");
            return PartialView("_CartContents", viewModel);
        }

        // POST: /ShoppingCart/AddBundleToCartAjax
        [HttpPost]
        public async Task<IActionResult> AddBundleToCartAjax(int id)
        {
            var bundle = await bundleService.GetBundleAsync(id);
            if (bundle == null || !bundle.IsActive)
            {
                return NotFound("Bundle not found.");
            }

            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            await cart.AddBundleToCartAsync(bundle);
            await storeDB.SaveChangesAsync();

            var viewModel = await BuildCartViewModelAsync(cart, $"Added the \u201c{bundle.Title}\u201d bundle to your cart.");
            return PartialView("_CartContents", viewModel);
        }

        // POST: /ShoppingCart/RemoveFromCart/5
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            var cart = ShoppingCart.GetCart(storeDB, HttpContext);
            var cartItem = (await cart.GetCartItemsAsync()).SingleOrDefault(item => item.RecordId == id);
            if (cartItem == null)
            {
                return NotFound("Cart item not found.");
            }

            var itemName = cartItem.DisplayTitle;
            var itemCount = await cart.RemoveFromCartAsync(id);
            await storeDB.SaveChangesAsync();

            var message = itemCount > 0
                ? $"1 copy of {itemName} has been removed from your shopping cart."
                : $"{itemName} has been removed from your shopping cart.";

            var viewModel = await BuildCartViewModelAsync(cart, message);
            return PartialView("_CartContents", viewModel);
        }

        // POST: /ShoppingCart/UpdateCart
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

            var itemName = cartItem.DisplayTitle;
            var itemCount = await cart.UpdateCartItemCountAsync(id, count);
            await storeDB.SaveChangesAsync();

            var message = itemCount == 0
                ? $"{itemName} has been removed from your shopping cart."
                : $"{itemName} quantity has been updated to {itemCount}.";

            var viewModel = await BuildCartViewModelAsync(cart, message);
            return PartialView("_CartContents", viewModel);
        }

        private async Task<ShoppingCartViewModel> BuildCartViewModelAsync(ShoppingCart cart, string? message = null)
        {
            var items = await cart.GetCartItemsAsync();

            var pricing = await promotions.PriceCartAsync(items, GetSessionDiscountCode());

            // Drop a now-invalid stored code so the sale-adjusted totals stay honest.
            if (!string.IsNullOrEmpty(GetSessionDiscountCode()) && !pricing.DiscountApplied)
            {
                ClearSessionDiscountCode();
            }

            var albumIds = items
                .Where(item => !item.IsBundle)
                .Select(item => item.AlbumId)
                .Concat(items
                    .Where(item => item.IsBundle)
                    .SelectMany(item => (item.BundleItems ?? new List<CartBundleItem>())
                        .Select(bundleItem => bundleItem.AlbumId)))
                .Distinct()
                .ToList();

            var recommendations = (await recommendationService.GetCartCrossSellAsync(albumIds)).ToList();

            var bundleIdsInCart = items
                .Where(item => item.IsBundle && item.BundleId.HasValue)
                .Select(item => item.BundleId!.Value)
                .ToHashSet();

            var suggestedBundles = (await bundleService.GetBundlesForCartAsync(albumIds))
                .Where(bundle => !bundleIdsInCart.Contains(bundle.BundleId))
                .Take(3)
                .ToList();

            return new ShoppingCartViewModel
            {
                CartItems = items,
                Pricing = pricing,
                CartTotal = pricing.Total,
                CartCount = await cart.GetCountAsync(),
                Recommendations = recommendations,
                SuggestedBundles = suggestedBundles,
                Message = message
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
