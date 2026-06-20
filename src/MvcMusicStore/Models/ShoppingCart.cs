using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Services;

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

        // Session slot holding the discount code a shopper applied in the cart, carried into checkout.
        public const string DiscountCodeSessionKey = "DiscountCode";

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
            return cartItems.Sum(item => item.Count * item.AlbumPrice);
        }

        /// <summary>
        /// Builds the order from a pre-computed <see cref="CartPricing"/> (sale-adjusted unit
        /// prices plus any applied discount code) and empties the cart.
        /// </summary>
        public async Task<int> CreateOrderAsync(Order order, CartPricing pricing)
        {
            order.OrderDetails ??= new List<OrderDetail>();

            // Load the catalog albums backing the cart so each line can capture the album's
            // current title, art, and audio (download) URL as a stable snapshot. Albums are
            // fetched one id at a time, matching the Cosmos-safe query patterns used elsewhere.
            var albumLookup = new Dictionary<int, Album>();
            foreach (var albumId in pricing.Lines.Select(l => l.AlbumId).Distinct())
            {
                var album = await _db.Albums.FirstOrDefaultAsync(a => a.AlbumId == albumId);
                if (album != null)
                {
                    albumLookup[albumId] = album;
                }
            }

            var orderDetailId = 1;

            // Persist the sale-adjusted unit price each line was actually charged at, plus a
            // catalog snapshot (title, art, audio) so order history and receipts stay stable.
            foreach (var line in pricing.Lines)
            {
                albumLookup.TryGetValue(line.AlbumId, out var album);

                order.OrderDetails.Add(new OrderDetail
                {
                    OrderDetailId = orderDetailId++,
                    AlbumId = line.AlbumId,
                    OrderId = order.OrderId,
                    UnitPrice = line.EffectiveUnitPrice,
                    Quantity = line.Quantity,
                    AlbumTitle = album?.Title ?? line.Title,
                    AlbumArtUrl = album?.GetDisplayThumbnailUrl(),
                    AudioUrl = album?.AudioUrl,
                });
            }

            order.Subtotal = pricing.Subtotal;
            order.DiscountCode = pricing.AppliedCode;
            order.DiscountAmount = pricing.DiscountAmount;
            order.Total = pricing.Total;

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
