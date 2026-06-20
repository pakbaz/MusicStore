using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;

namespace MvcMusicStore.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class SalesController : Controller
    {
        private readonly MusicStoreEntities db;

        public SalesController(MusicStoreEntities storeDb)
        {
            db = storeDb;
        }

        //
        // GET: /Sales/

        public async Task<IActionResult> Index()
        {
            var sales = await db.Sales.ToListAsync();
            return View(sales.OrderByDescending(s => s.IsFeatured).ThenBy(s => s.Name).ToList());
        }

        //
        // GET: /Sales/Create

        public async Task<IActionResult> Create()
        {
            var sale = new Sale
            {
                StartDateUtc = DateTime.UtcNow,
                EndDateUtc = DateTime.UtcNow.AddDays(1)
            };
            await PopulateAlbumsAsync(sale.AlbumIds);
            return View(sale);
        }

        //
        // POST: /Sales/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Sale sale, CancellationToken cancellationToken)
        {
            ValidateSale(sale);

            if (ModelState.IsValid)
            {
                sale.SaleId = await db.NextSaleIdAsync(cancellationToken);
                await EnforceSingleFeaturedAsync(sale, cancellationToken);
                db.Sales.Add(sale);
                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction(nameof(Index));
            }

            await PopulateAlbumsAsync(sale.AlbumIds);
            return View(sale);
        }

        //
        // GET: /Sales/Edit/5

        public async Task<IActionResult> Edit(int id)
        {
            var sale = await db.Sales.SingleOrDefaultAsync(s => s.SaleId == id);
            if (sale == null)
            {
                return NotFound();
            }

            await PopulateAlbumsAsync(sale.AlbumIds);
            return View(sale);
        }

        //
        // POST: /Sales/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Sale sale, CancellationToken cancellationToken)
        {
            if (id != sale.SaleId)
            {
                return BadRequest();
            }

            var existing = await db.Sales.SingleOrDefaultAsync(s => s.SaleId == id);
            if (existing == null)
            {
                return NotFound();
            }

            ValidateSale(sale);

            if (ModelState.IsValid)
            {
                existing.Name = sale.Name;
                existing.DiscountType = sale.DiscountType;
                existing.Value = sale.Value;
                existing.StartDateUtc = sale.StartDateUtc;
                existing.EndDateUtc = sale.EndDateUtc;
                existing.IsActive = sale.IsActive;
                existing.IsFeatured = sale.IsFeatured;
                existing.AppliesToEntireStore = sale.AppliesToEntireStore;
                existing.AlbumIds = sale.AppliesToEntireStore ? new List<int>() : sale.AlbumIds;

                await EnforceSingleFeaturedAsync(existing, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction(nameof(Index));
            }

            await PopulateAlbumsAsync(sale.AlbumIds);
            return View(sale);
        }

        //
        // GET: /Sales/Delete/5

        public async Task<IActionResult> Delete(int id)
        {
            var sale = await db.Sales.SingleOrDefaultAsync(s => s.SaleId == id);
            if (sale == null)
            {
                return NotFound();
            }

            return View(sale);
        }

        //
        // POST: /Sales/Delete/5

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
        {
            var sale = await db.Sales.SingleOrDefaultAsync(s => s.SaleId == id, cancellationToken);
            if (sale != null)
            {
                db.Sales.Remove(sale);
                await db.SaveChangesAsync(cancellationToken);
            }

            return RedirectToAction(nameof(Index));
        }

        private void ValidateSale(Sale sale)
        {
            if (sale.DiscountType == DiscountType.Percentage && sale.Value > 100m)
            {
                ModelState.AddModelError(nameof(Sale.Value), "Percentage discounts cannot exceed 100%.");
            }

            if (sale.EndDateUtc < sale.StartDateUtc)
            {
                ModelState.AddModelError(nameof(Sale.EndDateUtc), "The end date must be on or after the start date.");
            }

            if (sale.AppliesToEntireStore)
            {
                sale.AlbumIds = new List<int>();
            }
            else if (sale.AlbumIds.Count == 0)
            {
                ModelState.AddModelError(nameof(Sale.AlbumIds),
                    "Select at least one album, or mark the sale as storewide.");
            }
        }

        // Only one sale may be the featured "Deal of the Day"; unset the flag on any others.
        private async Task EnforceSingleFeaturedAsync(Sale sale, CancellationToken cancellationToken)
        {
            if (!sale.IsFeatured)
            {
                return;
            }

            var others = await db.Sales.ToListAsync(cancellationToken);
            foreach (var other in others.Where(s => s.SaleId != sale.SaleId && s.IsFeatured))
            {
                other.IsFeatured = false;
            }
        }

        private async Task PopulateAlbumsAsync(IEnumerable<int>? selectedAlbumIds = null)
        {
            var albums = await db.Albums.ToListAsync();
            var ordered = albums
                .OrderBy(a => a.Title)
                .Select(a => new { a.AlbumId, Label = $"{a.Title} — {a.ArtistName}" })
                .ToList();

            ViewBag.AlbumOptions = new MultiSelectList(ordered, "AlbumId", "Label", selectedAlbumIds);
        }
    }
}
