using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class StoreController : Controller
    {
        private readonly MusicStoreEntities storeDB;

        public StoreController(MusicStoreEntities storeDb)
        {
            storeDB = storeDb;
        }

        //
        // GET: /Store/

        public IActionResult Index()
        {
            var genres = storeDB.Genres.ToList();

            return View(genres);
        }


        //
        // GET: /Store/Browse?genre=Disco

        public IActionResult Browse(string genre)
        {
            // Retrieve Genre genre and its Associated associated Albums albums from database
            var genreModel = storeDB.Genres.Include("Albums")
                .Single(g => g.Name == genre);

            return View(genreModel);
        }

        public IActionResult Details(int id)
        {
            var album = storeDB.Albums
                .Include(a => a.Genre)
                .Include(a => a.Artist)
                .SingleOrDefault(a => a.AlbumId == id);

            if (album == null)
            {
                return NotFound();
            }

            var relatedByGenre = storeDB.Albums
                .Include(a => a.Artist)
                .Where(a => a.AlbumId != id && a.GenreId == album.GenreId)
                .OrderBy(a => a.Title)
                .Take(4)
                .ToList();

            var moreFromArtist = storeDB.Albums
                .Include(a => a.Genre)
                .Where(a => a.AlbumId != id && a.ArtistId == album.ArtistId)
                .OrderBy(a => a.Title)
                .Take(4)
                .ToList();

            var viewModel = new AlbumDetailsViewModel
            {
                Album = album,
                RelatedByGenre = relatedByGenre,
                MoreFromArtist = moreFromArtist
            };

            return View(viewModel);
        }

        public IActionResult Artist(int id)
        {
            var artist = storeDB.Artists.SingleOrDefault(a => a.ArtistId == id);

            if (artist == null)
            {
                return NotFound();
            }

            var artistAlbums = storeDB.Albums
                .Include(a => a.Genre)
                .Where(a => a.ArtistId == id)
                .OrderBy(a => a.Title)
                .ToList();

            var artistAlbumIds = artistAlbums.Select(a => a.AlbumId).ToHashSet();
            var genreIds = artistAlbums.Select(a => a.GenreId).Distinct().ToList();

            var relatedAlbums = storeDB.Albums
                .Include(a => a.Artist)
                .Where(a => !artistAlbumIds.Contains(a.AlbumId) && genreIds.Contains(a.GenreId))
                .OrderByDescending(a => a.IsFeatured)
                .ThenBy(a => a.Title)
                .Take(6)
                .ToList();

            var viewModel = new ArtistDetailsViewModel
            {
                Artist = artist,
                Albums = artistAlbums,
                RelatedAlbums = relatedAlbums
            };

            return View(viewModel);
        }
    }
}