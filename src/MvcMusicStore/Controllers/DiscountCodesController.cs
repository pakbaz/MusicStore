using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;

namespace MvcMusicStore.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class DiscountCodesController : Controller
    {
        private readonly MusicStoreEntities db;

        public DiscountCodesController(MusicStoreEntities storeDb)
        {
            db = storeDb;
        }

        //
        // GET: /DiscountCodes/

        public async Task<IActionResult> Index()
        {
            var codes = await db.DiscountCodes.ToListAsync();
            return View(codes.OrderBy(c => c.Code).ToList());
        }

        //
        // GET: /DiscountCodes/Create

        public IActionResult Create()
        {
            return View(new DiscountCode());
        }

        //
        // POST: /DiscountCodes/Create

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DiscountCode discountCode, CancellationToken cancellationToken)
        {
            discountCode.Code = DiscountCode.Normalize(discountCode.Code);
            ValidateDiscount(discountCode);
            await ValidateUniqueCodeAsync(discountCode, cancellationToken);

            if (ModelState.IsValid)
            {
                discountCode.DiscountCodeId = await db.NextDiscountCodeIdAsync(cancellationToken);
                discountCode.TimesUsed = 0;
                db.DiscountCodes.Add(discountCode);
                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction(nameof(Index));
            }

            return View(discountCode);
        }

        //
        // GET: /DiscountCodes/Edit/5

        public async Task<IActionResult> Edit(int id)
        {
            var discountCode = await db.DiscountCodes.SingleOrDefaultAsync(c => c.DiscountCodeId == id);
            if (discountCode == null)
            {
                return NotFound();
            }

            return View(discountCode);
        }

        //
        // POST: /DiscountCodes/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DiscountCode discountCode, CancellationToken cancellationToken)
        {
            if (id != discountCode.DiscountCodeId)
            {
                return BadRequest();
            }

            var existing = await db.DiscountCodes.SingleOrDefaultAsync(c => c.DiscountCodeId == id);
            if (existing == null)
            {
                return NotFound();
            }

            discountCode.Code = DiscountCode.Normalize(discountCode.Code);
            ValidateDiscount(discountCode);
            await ValidateUniqueCodeAsync(discountCode, cancellationToken);

            if (ModelState.IsValid)
            {
                existing.Code = discountCode.Code;
                existing.Description = discountCode.Description;
                existing.DiscountType = discountCode.DiscountType;
                existing.Value = discountCode.Value;
                existing.MinimumSpend = discountCode.MinimumSpend;
                existing.StartDateUtc = discountCode.StartDateUtc;
                existing.EndDateUtc = discountCode.EndDateUtc;
                existing.UsageLimit = discountCode.UsageLimit;
                existing.IsActive = discountCode.IsActive;

                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction(nameof(Index));
            }

            return View(discountCode);
        }

        //
        // GET: /DiscountCodes/Delete/5

        public async Task<IActionResult> Delete(int id)
        {
            var discountCode = await db.DiscountCodes.SingleOrDefaultAsync(c => c.DiscountCodeId == id);
            if (discountCode == null)
            {
                return NotFound();
            }

            return View(discountCode);
        }

        //
        // POST: /DiscountCodes/Delete/5

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
        {
            var discountCode = await db.DiscountCodes.SingleOrDefaultAsync(c => c.DiscountCodeId == id, cancellationToken);
            if (discountCode != null)
            {
                db.DiscountCodes.Remove(discountCode);
                await db.SaveChangesAsync(cancellationToken);
            }

            return RedirectToAction(nameof(Index));
        }

        private void ValidateDiscount(DiscountCode discountCode)
        {
            if (discountCode.DiscountType == DiscountType.Percentage && discountCode.Value > 100m)
            {
                ModelState.AddModelError(nameof(DiscountCode.Value), "Percentage discounts cannot exceed 100%.");
            }

            if (discountCode.StartDateUtc.HasValue && discountCode.EndDateUtc.HasValue
                && discountCode.EndDateUtc.Value < discountCode.StartDateUtc.Value)
            {
                ModelState.AddModelError(nameof(DiscountCode.EndDateUtc), "The expiry date must be on or after the start date.");
            }
        }

        // Cosmos cannot translate AnyAsync()/EXISTS, so load codes and compare in memory.
        private async Task ValidateUniqueCodeAsync(DiscountCode discountCode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(discountCode.Code))
            {
                return;
            }

            var codes = await db.DiscountCodes.ToListAsync(cancellationToken);
            bool duplicate = codes.Any(c =>
                c.DiscountCodeId != discountCode.DiscountCodeId
                && string.Equals(c.Code, discountCode.Code, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                ModelState.AddModelError(nameof(DiscountCode.Code), "A discount code with that name already exists.");
            }
        }
    }
}
