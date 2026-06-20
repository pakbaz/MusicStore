using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MvcMusicStore.Models;

namespace MvcMusicStore.ViewComponents
{
    public class LoyaltySummaryViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public LoyaltySummaryViewComponent(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (HttpContext.User?.Identity?.IsAuthenticated != true)
            {
                return Content(string.Empty);
            }

            var user = await _userManager.GetUserAsync(HttpContext.User);
            return View(user?.LoyaltyPoints ?? 0);
        }
    }
}
