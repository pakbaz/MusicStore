using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.ViewComponents
{
    public class WishlistButtonViewComponent : ViewComponent
    {
        private const string SavedAlbumIdsKey = "__WishlistSavedAlbumIds";

        private readonly MusicStoreEntities _db;

        public WishlistButtonViewComponent(MusicStoreEntities db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync(int albumId, string? returnUrl = null, bool large = false)
        {
            var savedAlbumIds = await GetSavedAlbumIdsAsync();

            var viewModel = new WishlistButtonViewModel
            {
                AlbumId = albumId,
                IsSaved = savedAlbumIds.Contains(albumId),
                ReturnUrl = string.IsNullOrEmpty(returnUrl)
                    ? Request.Path + Request.QueryString
                    : returnUrl,
                Large = large
            };

            return View(viewModel);
        }

        // The button is rendered once per album card, so the saved album ids are
        // loaded a single time per request and cached to avoid N store queries.
        private async Task<HashSet<int>> GetSavedAlbumIdsAsync()
        {
            if (HttpContext.Items[SavedAlbumIdsKey] is HashSet<int> cached)
            {
                return cached;
            }

            var wishlist = Wishlist.GetWishlist(_db, HttpContext);
            var ids = (await wishlist.GetAlbumIdsAsync()).ToHashSet();
            HttpContext.Items[SavedAlbumIdsKey] = ids;
            return ids;
        }
    }
}
