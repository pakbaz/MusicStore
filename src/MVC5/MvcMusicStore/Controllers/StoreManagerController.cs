using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;

namespace MvcMusicStore.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class StoreManagerController : Controller
    {
        private readonly MusicStoreEntities db;

        public StoreManagerController(MusicStoreEntities storeDb)
        {
            db = storeDb;
        }

        //
        // GET: /StoreManager/

        public IActionResult Index()
        {
            var albums = db.Albums.Include(a => a.Genre).Include(a => a.Artist)
                .OrderBy(a => a.Price);
            return View(albums.ToList());
        }

        //
        // GET: /StoreManager/Details/5

        public IActionResult Details(int id = 0)
        {
            Album? album = db.Albums.Find(id);
            if (album == null)
            {
                return NotFound();
            }
            return View(album);
        }

        //
        // GET: /StoreManager/Create

        public IActionResult Create()
        {
            ViewBag.GenreId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Genres, "GenreId", "Name");
            ViewBag.ArtistId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Artists, "ArtistId", "Name");
            return View();
        }

        //
        // POST: /StoreManager/Create

        [HttpPost]
        public IActionResult Create(Album album)
        {
            if (ModelState.IsValid)
            {
                db.Albums.Add(album);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.GenreId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Genres, "GenreId", "Name", album.GenreId);
            ViewBag.ArtistId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Artists, "ArtistId", "Name", album.ArtistId);
            return View(album);
        }

        //
        // GET: /StoreManager/Edit/5

        public IActionResult Edit(int id = 0)
        {
            Album? album = db.Albums.Find(id);
            if (album == null)
            {
                return NotFound();
            }
            ViewBag.GenreId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Genres, "GenreId", "Name", album.GenreId);
            ViewBag.ArtistId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Artists, "ArtistId", "Name", album.ArtistId);
            return View(album);
        }

        //
        // POST: /StoreManager/Edit/5

        [HttpPost]
        public IActionResult Edit(Album album)
        {
            if (ModelState.IsValid)
            {
                db.Entry(album).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.GenreId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Genres, "GenreId", "Name", album.GenreId);
            ViewBag.ArtistId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(db.Artists, "ArtistId", "Name", album.ArtistId);
            return View(album);
        }

        //
        // GET: /StoreManager/Delete/5

        public IActionResult Delete(int id = 0)
        {
            Album? album = db.Albums.Find(id);
            if (album == null)
            {
                return NotFound();
            }
            return View(album);
        }

        //
        // POST: /StoreManager/Delete/5

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            Album? album = db.Albums.Find(id);
            if (album != null)
            {
                db.Albums.Remove(album);
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}