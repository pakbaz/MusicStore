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

        public async Task<int> CreateOrderAsync(Order order)
        {
            decimal orderTotal = 0;

            var cartItems = await GetCartItemsAsync();
            order.OrderDetails ??= new List<OrderDetail>();

            var orderDetailId = 1;

            // Iterate over the items in the cart, adding the order details for each
            foreach (var item in cartItems)
            {
                var orderDetail = new OrderDetail
                {
                    OrderDetailId = orderDetailId++,
                    AlbumId = item.AlbumId,
                    AlbumTitle = item.AlbumTitle,
                    OrderId = order.OrderId,
                    UnitPrice = item.AlbumPrice,
                    Quantity = item.Count,
                };

                // Set the order total of the shopping cart
                orderTotal += (item.Count * item.AlbumPrice);

                order.OrderDetails.Add(orderDetail);
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
