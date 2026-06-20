using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.ViewComponents
{
    public class WishlistSummaryViewComponent : ViewComponent
    {
        private readonly MusicStoreEntities _db;

        public WishlistSummaryViewComponent(MusicStoreEntities db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var wishlist = Wishlist.GetWishlist(_db, HttpContext);

            var viewModel = new WishlistSummaryViewModel
            {
                WishlistCount = await wishlist.GetCountAsync()
            };

            return View(viewModel);
        }
    }
}
