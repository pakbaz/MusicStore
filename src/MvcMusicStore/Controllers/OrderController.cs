using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;

namespace MvcMusicStore.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly MusicStoreEntities storeDB;

        public OrderController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        //
        // GET: /Order/  ("My Orders" history)

        public async Task<IActionResult> Index()
        {
            var username = User.Identity!.Name;

            var orders = await storeDB.Orders
                .Where(o => o.Username == username)
                .ToListAsync();

            // Sort newest-first in memory; the Cosmos provider's ORDER BY translation is
            // avoided deliberately to keep the query simple and resilient.
            var history = orders
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return View(history);
        }

        //
        // GET: /Order/Details/5

        public async Task<IActionResult> Details(int id)
        {
            var username = User.Identity!.Name;

            // Ownership is enforced directly in the query: a customer can only ever load an
            // order whose Username matches their own. A non-existent or someone else's order
            // returns the same friendly message without revealing whether it exists.
            var order = await storeDB.Orders
                .Where(o => o.OrderId == id && o.Username == username)
                .Take(1)
                .ToListAsync();

            if (order.Count == 0)
            {
                ViewBag.ErrorMessage = "We couldn't find that order for your account. Please review your recent orders.";
                return View("Error");
            }

            return View(order[0]);
        }
    }
}
