using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class BundleManagerController : Controller
    {
        private readonly MusicStoreEntities db;

        public BundleManagerController(MusicStoreEntities storeDb)
        {
            db = storeDb;
        }

        // GET: /BundleManager/
        public async Task<IActionResult> Index()
        {
            var bundles = await db.Bundles.ToListAsync();
            return View(bundles.OrderByDescending(b => b.DateCreated).ToList());
        }

        // GET: /BundleManager/Details/5
        public async Task<IActionResult> Details(int id = 0)
        {
            var bundle = await db.Bundles.SingleOrDefaultAsync(b => b.BundleId == id);
            if (bundle == null)
            {
                return NotFound();
            }

            return View(bundle);
        }

        // GET: /BundleManager/Create
        public async Task<IActionResult> Create()
        {
            var model = new BundleEditViewModel { IsActive = true };
            await PopulateAlbumChoicesAsync(model);
            return View(model);
        }

        // POST: /BundleManager/Create
        [HttpPost]
        public async Task<IActionResult> Create(BundleEditViewModel model, CancellationToken cancellationToken)
        {
            ValidateSelection(model);

            if (ModelState.IsValid)
            {
                var bundle = new Bundle
                {
                    BundleId = await db.NextBundleIdAsync(cancellationToken),
                    Title = model.Title,
                    Description = model.Description,
                    BundlePrice = model.BundlePrice,
                    IsActive = model.IsActive,
                    DateCreated = DateTime.UtcNow,
                    Items = await BuildBundleItemsAsync(model.SelectedAlbumIds, cancellationToken)
                };

                db.Bundles.Add(bundle);
                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction("Index");
            }

            await PopulateAlbumChoicesAsync(model);
            return View(model);
        }

        // GET: /BundleManager/Edit/5
        public async Task<IActionResult> Edit(int id = 0)
        {
            var bundle = await db.Bundles.SingleOrDefaultAsync(b => b.BundleId == id);
            if (bundle == null)
            {
                return NotFound();
            }

            var model = new BundleEditViewModel
            {
                BundleId = bundle.BundleId,
                Title = bundle.Title,
                Description = bundle.Description,
                BundlePrice = bundle.BundlePrice,
                IsActive = bundle.IsActive,
                SelectedAlbumIds = bundle.Items.Select(item => item.AlbumId).ToList()
            };
            await PopulateAlbumChoicesAsync(model);
            return View(model);
        }

        // POST: /BundleManager/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, BundleEditViewModel model, CancellationToken cancellationToken)
        {
            if (id != model.BundleId)
            {
                return BadRequest();
            }

            var bundle = await db.Bundles.SingleOrDefaultAsync(b => b.BundleId == id);
            if (bundle == null)
            {
                return NotFound();
            }

            ValidateSelection(model);

            if (ModelState.IsValid)
            {
                bundle.Title = model.Title;
                bundle.Description = model.Description;
                bundle.BundlePrice = model.BundlePrice;
                bundle.IsActive = model.IsActive;
                bundle.Items = await BuildBundleItemsAsync(model.SelectedAlbumIds, cancellationToken);

                await db.SaveChangesAsync(cancellationToken);
                return RedirectToAction("Index");
            }

            await PopulateAlbumChoicesAsync(model);
            return View(model);
        }

        // GET: /BundleManager/Delete/5
        public async Task<IActionResult> Delete(int id = 0)
        {
            var bundle = await db.Bundles.SingleOrDefaultAsync(b => b.BundleId == id);
            if (bundle == null)
            {
                return NotFound();
            }

            return View(bundle);
        }

        // POST: /BundleManager/Delete/5
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
        {
            var bundle = await db.Bundles.SingleOrDefaultAsync(b => b.BundleId == id, cancellationToken);
            if (bundle != null)
            {
                db.Bundles.Remove(bundle);
                await db.SaveChangesAsync(cancellationToken);
            }

            return RedirectToAction("Index");
        }

        private void ValidateSelection(BundleEditViewModel model)
        {
            var selected = (model.SelectedAlbumIds ?? new List<int>()).Distinct().ToList();
            model.SelectedAlbumIds = selected;

            if (selected.Count < 2)
            {
                ModelState.AddModelError(nameof(model.SelectedAlbumIds), "Select at least two albums for a bundle.");
            }
        }

        private async Task<List<BundleItem>> BuildBundleItemsAsync(IEnumerable<int> albumIds, CancellationToken cancellationToken)
        {
            var ids = albumIds.Distinct().ToList();
            var albums = await db.Albums.ToListAsync(cancellationToken);

            return albums
                .Where(album => ids.Contains(album.AlbumId))
                .Select(album => new BundleItem
                {
                    AlbumId = album.AlbumId,
                    AlbumTitle = album.Title,
                    AlbumPrice = album.Price,
                    AlbumArtUrl = album.GetDisplayThumbnailUrl()
                })
                .ToList();
        }

        private async Task PopulateAlbumChoicesAsync(BundleEditViewModel model)
        {
            var albums = await db.Albums.ToListAsync();
            model.AlbumChoices = albums
                .OrderBy(album => album.Title)
                .Select(album => new AlbumChoice
                {
                    AlbumId = album.AlbumId,
                    Title = album.Title ?? string.Empty,
                    ArtistName = album.ArtistName ?? string.Empty,
                    Price = album.Price
                })
                .ToList();
        }
    }
}
