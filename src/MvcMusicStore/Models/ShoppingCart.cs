using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MvcMusicStore.Models
{
    public partial class ShoppingCart
    {
        MusicStoreEntities _db;
        string ShoppingCartId { get; set; }

        public ShoppingCart(MusicStoreEntities db)
        {
            _db = db;
            ShoppingCartId = string.Empty;
        }

        public const string CartSessionKey = "CartId";

        public static ShoppingCart GetCart(MusicStoreEntities db, HttpContext context)
        {
            var cart = new ShoppingCart(db);
            cart.ShoppingCartId = cart.GetCartId(context);
            return cart;
        }

        public async Task AddToCartAsync(Album album)
        {
            // Get the matching cart and album instances
            var cartItem = await _db.Carts.SingleOrDefaultAsync(
                c => c.CartId == ShoppingCartId
                && c.AlbumId == album.AlbumId);

            if (cartItem == null)
            {
                // Create a new cart item if no cart item exists
                cartItem = new Cart
                {
                    RecordId = await _db.NextCartRecordIdAsync(),
                    AlbumId = album.AlbumId,
                    CartId = ShoppingCartId,
                    Count = 1,
                    DateCreated = DateTime.Now,
                    AlbumTitle = album.Title,
                    AlbumPrice = album.Price,
                    AlbumArtUrl = album.GetDisplayThumbnailUrl()
                };

                _db.Carts.Add(cartItem);
            }
            else
            {
                // If the item does exist in the cart, then add one to the quantity
                cartItem.Count++;
            }
        }

        public async Task AddBundleToCartAsync(Bundle bundle)
        {
            var cartItem = await _db.Carts.SingleOrDefaultAsync(
                c => c.CartId == ShoppingCartId
                && c.BundleId == bundle.BundleId);

            if (cartItem == null)
            {
                cartItem = new Cart
                {
                    RecordId = await _db.NextCartRecordIdAsync(),
                    CartId = ShoppingCartId,
                    AlbumId = 0,
                    Count = 1,
                    DateCreated = DateTime.Now,
                    BundleId = bundle.BundleId,
                    BundleTitle = bundle.Title,
                    BundlePrice = bundle.BundlePrice,
                    BundleItems = DistributeBundlePricing(bundle),
                    AlbumArtUrl = bundle.GetDisplayThumbnailUrl()
                };

                _db.Carts.Add(cartItem);
            }
            else
            {
                cartItem.Count++;
            }
        }

        // Spreads the bundle's discounted price across its member albums (proportional to each
        // album's regular price) so the per-album unit prices sum exactly to the bundle price.
        // Those unit prices are what we record as OrderDetails at checkout.
        private static List<CartBundleItem> DistributeBundlePricing(Bundle bundle)
        {
            var items = bundle.Items ?? new List<BundleItem>();
            var result = new List<CartBundleItem>();
            if (items.Count == 0)
            {
                return result;
            }

            var regularTotal = items.Sum(item => item.AlbumPrice);
            var bundlePrice = bundle.BundlePrice;
            decimal allocated = 0m;

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                decimal unitPrice;

                if (index == items.Count - 1)
                {
                    // Last member absorbs the rounding remainder so the line sums to BundlePrice.
                    unitPrice = bundlePrice - allocated;
                }
                else if (regularTotal > 0m)
                {
                    unitPrice = Math.Round(item.AlbumPrice / regularTotal * bundlePrice, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    unitPrice = Math.Round(bundlePrice / items.Count, 2, MidpointRounding.AwayFromZero);
                }

                if (unitPrice < 0m)
                {
                    unitPrice = 0m;
                }

                allocated += unitPrice;
                result.Add(new CartBundleItem
                {
                    AlbumId = item.AlbumId,
                    AlbumTitle = item.AlbumTitle,
                    UnitPrice = unitPrice
                });
            }

            return result;
        }

        public async Task<int> RemoveFromCartAsync(int id)
        {
            // Get the cart
            var cartItem = await _db.Carts.SingleOrDefaultAsync(
                cart => cart.CartId == ShoppingCartId
                && cart.RecordId == id);

            int itemCount = 0;

            if (cartItem != null)
            {
                if (cartItem.Count > 1)
                {
                    cartItem.Count--;
                    itemCount = cartItem.Count;
                }
                else
                {
                    _db.Carts.Remove(cartItem);
                }
            }

            return itemCount;
        }

        public async Task<int> UpdateCartItemCountAsync(int id, int count)
        {
            var cartItem = await _db.Carts.SingleOrDefaultAsync(
                cart => cart.CartId == ShoppingCartId
                && cart.RecordId == id);

            if (cartItem == null)
            {
                return 0;
            }

            if (count <= 0)
            {
                _db.Carts.Remove(cartItem);
                return 0;
            }

            cartItem.Count = count;
            return cartItem.Count;
        }

        public async Task EmptyCartAsync()
        {
            var cartItems = await _db.Carts.Where(cart => cart.CartId == ShoppingCartId).ToListAsync();

            foreach (var cartItem in cartItems)
            {
                _db.Carts.Remove(cartItem);
            }
        }

        public async Task<List<Cart>> GetCartItemsAsync()
        {
            var cartItems = await _db.Carts.Where(cart => cart.CartId == ShoppingCartId).ToListAsync();
            cartItems.PopulateAlbum();
            return cartItems;
        }

        public async Task<int> GetCountAsync()
        {
            var cartItems = await _db.Carts.Where(cart => cart.CartId == ShoppingCartId).ToListAsync();
            return cartItems.Sum(item => item.Count);
        }

        public async Task<decimal> GetTotalAsync()
        {
            var cartItems = await _db.Carts.Where(cart => cart.CartId == ShoppingCartId).ToListAsync();
            return cartItems.Sum(item => item.Count * item.LineUnitPrice);
        }

        public async Task<int> CreateOrderAsync(Order order)
        {
            decimal orderTotal = 0;

            var cartItems = await GetCartItemsAsync();
            order.OrderDetails ??= new List<OrderDetail>();

            // Load the catalog albums backing the cart so each line can capture the album's
            // current title, art, and audio (download) URL as a stable snapshot. Albums are
            // fetched one id at a time, matching the Cosmos-safe query patterns used elsewhere.
            var albumLookup = new Dictionary<int, Album>();
            var albumIds = cartItems
                .Where(i => !i.IsBundle)
                .Select(i => i.AlbumId)
                .Concat(cartItems.SelectMany(i => (i.BundleItems ?? new List<CartBundleItem>()).Select(m => m.AlbumId)))
                .Distinct();
            foreach (var albumId in albumIds)
            {
                var album = await _db.Albums.FirstOrDefaultAsync(a => a.AlbumId == albumId);
                if (album != null)
                {
                    albumLookup[albumId] = album;
                }
            }

            var orderDetailId = 1;

            // Iterate over the items in the cart, adding the order details for each
            foreach (var item in cartItems)
            {
                if (item.IsBundle)
                {
                    // Expand the bundle into one order detail per member album at the distributed
                    // (discounted) unit price, so the order total matches the discounted cart line.
                    foreach (var member in item.BundleItems ?? new List<CartBundleItem>())
                    {
                        albumLookup.TryGetValue(member.AlbumId, out var memberAlbum);

                        order.OrderDetails.Add(new OrderDetail
                        {
                            OrderDetailId = orderDetailId++,
                            AlbumId = member.AlbumId,
                            OrderId = order.OrderId,
                            UnitPrice = member.UnitPrice,
                            Quantity = item.Count,
                            AlbumTitle = memberAlbum?.Title ?? member.AlbumTitle,
                            AlbumArtUrl = memberAlbum?.GetDisplayThumbnailUrl(),
                            AudioUrl = memberAlbum?.AudioUrl,
                        });

                        orderTotal += member.UnitPrice * item.Count;
                    }

                    continue;
                }

                albumLookup.TryGetValue(item.AlbumId, out var album);

                var orderDetail = new OrderDetail
                {
                    OrderDetailId = orderDetailId++,
                    AlbumId = item.AlbumId,
                    OrderId = order.OrderId,
                    UnitPrice = item.AlbumPrice,
                    Quantity = item.Count,
                    AlbumTitle = album?.Title ?? item.AlbumTitle,
                    AlbumArtUrl = album?.GetDisplayThumbnailUrl() ?? item.AlbumArtUrl,
                    AudioUrl = album?.AudioUrl,
                };

                // Set the order total of the shopping cart
                orderTotal += (item.Count * item.AlbumPrice);

                order.OrderDetails.Add(orderDetail);

                // Maintain the denormalized popularity counter so the catalog can sort by
                // popularity without scanning the Orders container on every request. Reuse the
                // album already loaded for this line (a tracked entity) so the increment is
                // persisted by the caller's SaveChangesAsync without an extra query.
                if (album != null)
                {
                    album.Popularity += item.Count;
                }
            }

            // Set the order's total to the orderTotal count
            order.Total = orderTotal;

            // Empty the shopping cart
            await EmptyCartAsync();

            // Return the OrderId as the confirmation number
            return order.OrderId;
        }

        // We're using HttpContext to allow access to session.
        public string GetCartId(HttpContext context)
        {
            if (context.Session.GetString(CartSessionKey) == null)
            {
                if (!string.IsNullOrWhiteSpace(context.User?.Identity?.Name))
                {
                    context.Session.SetString(CartSessionKey, context.User.Identity.Name);
                }
                else
                {
                    // Generate a new random GUID using System.Guid class
                    Guid tempCartId = Guid.NewGuid();

                    // Store tempCartId in session
                    context.Session.SetString(CartSessionKey, tempCartId.ToString());
                }
            }

            return context.Session.GetString(CartSessionKey)!;
        }

        // When a user has logged in, migrate their shopping cart to
        // be associated with their username
        public async Task MigrateCartAsync(string userName)
        {
            var shoppingCart = await _db.Carts.Where(c => c.CartId == ShoppingCartId).ToListAsync();

            foreach (Cart item in shoppingCart)
            {
                item.CartId = userName;
            }
        }
    }
}
