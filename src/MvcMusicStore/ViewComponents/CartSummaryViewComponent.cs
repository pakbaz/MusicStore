using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.ViewComponents
{
    public class CartSummaryViewComponent : ViewComponent
    {
        private readonly MusicStoreEntities _db;

        public CartSummaryViewComponent(MusicStoreEntities db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var cart = ShoppingCart.GetCart(_db, HttpContext);

            var cartItems = (await cart.GetCartItemsAsync())
                .OrderBy(item => item.DisplayTitle)
                .ToList();

            var viewModel = new CartSummaryViewModel
            {
                CartCount = cartItems.Sum(item => item.Count),
                CartSummary = string.Join("\n", cartItems.Select(item => $"{item.DisplayTitle} x{item.Count}"))
            };

            return View(viewModel);
        }
    }
}
