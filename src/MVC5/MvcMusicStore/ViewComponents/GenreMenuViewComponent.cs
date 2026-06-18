using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MvcMusicStore.Models;

namespace MvcMusicStore.ViewComponents
{
    public class GenreMenuViewComponent : ViewComponent
    {
        private readonly MusicStoreEntities _db;

        public GenreMenuViewComponent(MusicStoreEntities db)
        {
            _db = db;
        }

        public IViewComponentResult Invoke()
        {
            var genres = _db.Genres
                .OrderByDescending(
                    g => g.Albums!.Sum(
                    a => a.OrderDetails!.Sum(
                    od => od.Quantity)))
                .Take(9)
                .ToList();

            return View(genres);
        }
    }
}
