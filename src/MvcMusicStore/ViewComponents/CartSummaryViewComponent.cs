using System.Linq;
using Microsoft.AspNetCore.Mvc;
using MvcMusicStore.Models;

namespace MvcMusicStore.ViewComponents
{
    public class CartSummaryViewComponent : ViewComponent
    {
        private readonly MusicStoreEntities _db;

        public CartSummaryViewComponent(MusicStoreEntities db)
        {
            _db = db;
        }

        public IViewComponentResult Invoke()
        {
            var cart = ShoppingCart.GetCart(_db, HttpContext);

            var cartItems = cart.GetCartItems()
                .Select(a => a.Album!.Title)
                .OrderBy(x => x);

            ViewBag.CartCount = cartItems.Count();
            ViewBag.CartSummary = string.Join("\n", cartItems.Distinct());

            return View();
        }
    }
}
