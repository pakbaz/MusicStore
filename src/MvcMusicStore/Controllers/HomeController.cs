using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using MvcMusicStore.Models;

namespace MvcMusicStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly MusicStoreEntities storeDB;

        public HomeController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        //
        // GET: /Home/

        public IActionResult Index()
        {
            // Get most popular albums
            var albums = GetTopSellingAlbums(6);

            return View(albums);
        }


        private List<Album> GetTopSellingAlbums(int count)
        {
            return storeDB.OrderDetails
                .GroupBy(od => od.AlbumId)
                .Select(g => new { AlbumId = g.Key, TotalSold = g.Sum(od => od.Quantity) })
                .OrderByDescending(x => x.TotalSold)
                .Take(count)
                .Join(storeDB.Albums, x => x.AlbumId, a => a.AlbumId, (x, a) => a)
                .ToList();
        }
    }
}