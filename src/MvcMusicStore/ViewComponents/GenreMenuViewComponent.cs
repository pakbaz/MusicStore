using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Cosmos DB does not support cross-container Include/aggregation, so the genre menu
            // loads the (small) set of genres and orders them in memory instead.
            var genres = await _db.Genres.ToListAsync();

            var topGenres = genres
                .OrderBy(g => g.Name)
                .Take(9)
                .ToList();

            return View(topGenres);
        }
    }
}
