using Microsoft.AspNetCore.Mvc;
using MvcMusicStore.Services;

namespace MvcMusicStore.Controllers
{
    public class BundlesController : Controller
    {
        private readonly IBundleService bundleService;

        public BundlesController(IBundleService bundleService)
        {
            this.bundleService = bundleService;
        }

        // GET: /Bundles/
        public async Task<IActionResult> Index()
        {
            var bundles = await bundleService.GetActiveBundlesAsync();
            return View(bundles);
        }

        // GET: /Bundles/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var bundle = await bundleService.GetBundleAsync(id);
            if (bundle == null || !bundle.IsActive)
            {
                return NotFound();
            }

            return View(bundle);
        }
    }
}
