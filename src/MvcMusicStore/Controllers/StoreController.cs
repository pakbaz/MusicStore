using MvcMusicStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            var album = storeDB.Albums.Find(id);

            return View(album);
        }
    }
}