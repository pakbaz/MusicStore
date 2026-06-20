using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MvcMusicStore.Models
{
    /// <summary>
    /// Helpers for tying reviews back to order history. Cosmos cannot translate <c>AnyAsync()</c>
    /// (EXISTS subquery) so callers materialize orders and evaluate embedded order details in memory,
    /// matching the existing sales/popularity calculation in <see cref="MvcMusicStore.Controllers.StoreController"/>.
    /// </summary>
    public static class PurchaseQueries
    {
        public static async Task<bool> HasPurchasedAlbumAsync(
            this MusicStoreEntities db,
            string? username,
            int albumId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            var orders = await db.Orders
                .Where(o => o.Username == username)
                .ToListAsync(cancellationToken);

            return orders
                .SelectMany(order => order.OrderDetails ?? new List<OrderDetail>())
                .Any(detail => detail.AlbumId == albumId);
        }
    }
}
