using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly MusicStoreEntities storeDB;

        public OrdersController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        //
        // GET: /Orders/

        public async Task<IActionResult> Index()
        {
            var username = User.Identity!.Name;
            var orders = await storeDB.Orders.Where(o => o.Username == username).ToListAsync();
            return View(orders.OrderByDescending(o => o.OrderDate).ToList());
        }

        //
        // GET: /Orders/Details/5

        public async Task<IActionResult> Details(int id)
        {
            var username = User.Identity!.Name;
            var order = (await storeDB.Orders
                .Where(o => o.OrderId == id && o.Username == username)
                .Take(1)
                .ToListAsync()).FirstOrDefault();

            if (order == null)
            {
                return NotFound();
            }

            var details = order.OrderDetails ?? new List<OrderDetail>();
            var titles = await ResolveAlbumTitlesAsync(details.Select(d => d.AlbumId));

            var vm = new OrderHistoryDetailsViewModel
            {
                Order = order,
                Items = details.Select(d => new OrderLineItemViewModel
                {
                    AlbumId = d.AlbumId,
                    Title = titles.TryGetValue(d.AlbumId, out var t) ? t : $"Album #{d.AlbumId}",
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                }).ToList(),
            };

            return View(vm);
        }

        private async Task<Dictionary<int, string>> ResolveAlbumTitlesAsync(IEnumerable<int> albumIds)
        {
            var ids = albumIds.Distinct().ToHashSet();
            if (ids.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var albums = await storeDB.Albums.ToListAsync();
            return albums
                .Where(a => ids.Contains(a.AlbumId))
                .ToDictionary(a => a.AlbumId, a => a.Title ?? $"Album #{a.AlbumId}");
        }
    }
}
