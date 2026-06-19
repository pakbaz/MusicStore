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
                .Where(item => item.Album != null)
                .OrderBy(item => item.Album!.Title)
                .ToList();

            var viewModel = new CartSummaryViewModel
            {
                CartCount = cartItems.Sum(item => item.Count),
                CartSummary = string.Join("\n", cartItems.Select(item => $"{item.Album!.Title} x{item.Count}"))
            };

            return View(viewModel);
        }
    }
}
