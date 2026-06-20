using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MvcMusicStore.Models
{
    public partial class Wishlist
    {
        MusicStoreEntities _db;
        string WishlistId { get; set; }

        public Wishlist(MusicStoreEntities db)
        {
            _db = db;
            WishlistId = string.Empty;
        }

        public const string WishlistSessionKey = "WishlistId";

        public static Wishlist GetWishlist(MusicStoreEntities db, HttpContext context)
        {
            var wishlist = new Wishlist(db);
            wishlist.WishlistId = wishlist.GetWishlistId(context);
            return wishlist;
        }

        // A wishlist has set semantics: an album is either saved or it isn't.
        // Adding an album that is already saved is a no-op.
        public async Task<bool> AddToWishlistAsync(Album album)
        {
            var existing = await _db.WishlistItems.SingleOrDefaultAsync(
                w => w.WishlistId == WishlistId
                && w.AlbumId == album.AlbumId);

            if (existing != null)
            {
                return false;
            }

            _db.WishlistItems.Add(new WishlistItem
            {
                RecordId = await _db.NextWishlistRecordIdAsync(),
                AlbumId = album.AlbumId,
                WishlistId = WishlistId,
                DateCreated = DateTime.Now,
                AlbumTitle = album.Title,
                AlbumPrice = album.Price,
                AlbumArtUrl = album.GetDisplayThumbnailUrl()
            });

            return true;
        }

        public async Task<bool> RemoveFromWishlistAsync(int id)
        {
            var item = await _db.WishlistItems.SingleOrDefaultAsync(
                w => w.WishlistId == WishlistId
                && w.RecordId == id);

            if (item == null)
            {
                return false;
            }

            _db.WishlistItems.Remove(item);
            return true;
        }

        public async Task<bool> RemoveAlbumAsync(int albumId)
        {
            var items = await _db.WishlistItems
                .Where(w => w.WishlistId == WishlistId && w.AlbumId == albumId)
                .ToListAsync();

            if (items.Count == 0)
            {
                return false;
            }

            foreach (var item in items)
            {
                _db.WishlistItems.Remove(item);
            }

            return true;
        }

        public async Task<List<WishlistItem>> GetWishlistItemsAsync()
        {
            var items = await _db.WishlistItems
                .Where(w => w.WishlistId == WishlistId)
                .ToListAsync();
            items.PopulateAlbum();
            return items;
        }

        public async Task<int> GetCountAsync()
        {
            return await _db.WishlistItems.CountAsync(w => w.WishlistId == WishlistId);
        }

        public async Task<bool> ContainsAlbumAsync(int albumId)
        {
            // Cosmos cannot translate AnyAsync() (EXISTS subquery), so materialize a single
            // matching id with Take(1) instead.
            var match = await _db.WishlistItems
                .Where(w => w.WishlistId == WishlistId && w.AlbumId == albumId)
                .Select(w => w.AlbumId)
                .Take(1)
                .ToListAsync();
            return match.Count != 0;
        }

        public async Task<List<int>> GetAlbumIdsAsync()
        {
            return await _db.WishlistItems
                .Where(w => w.WishlistId == WishlistId)
                .Select(w => w.AlbumId)
                .ToListAsync();
        }

        // We're using HttpContext to allow access to session.
        public string GetWishlistId(HttpContext context)
        {
            if (context.Session.GetString(WishlistSessionKey) == null)
            {
                if (!string.IsNullOrWhiteSpace(context.User?.Identity?.Name))
                {
                    context.Session.SetString(WishlistSessionKey, context.User.Identity.Name);
                }
                else
                {
                    Guid tempWishlistId = Guid.NewGuid();
                    context.Session.SetString(WishlistSessionKey, tempWishlistId.ToString());
                }
            }

            return context.Session.GetString(WishlistSessionKey)!;
        }

        // When a user logs in, migrate their session wishlist to be associated with
        // their username, de-duplicating against albums already saved to the account.
        public async Task MigrateWishlistAsync(string userName)
        {
            if (string.Equals(WishlistId, userName, StringComparison.Ordinal))
            {
                return;
            }

            var sessionItems = await _db.WishlistItems
                .Where(w => w.WishlistId == WishlistId)
                .ToListAsync();

            if (sessionItems.Count == 0)
            {
                return;
            }

            var existingAlbumIds = (await _db.WishlistItems
                .Where(w => w.WishlistId == userName)
                .Select(w => w.AlbumId)
                .ToListAsync())
                .ToHashSet();

            foreach (var item in sessionItems)
            {
                if (existingAlbumIds.Contains(item.AlbumId))
                {
                    // Album is already saved under the account; drop the duplicate.
                    _db.WishlistItems.Remove(item);
                }
                else
                {
                    item.WishlistId = userName;
                    existingAlbumIds.Add(item.AlbumId);
                }
            }
        }
    }
}
