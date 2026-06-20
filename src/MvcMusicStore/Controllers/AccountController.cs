using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILoyaltyService _loyalty;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILoyaltyService loyalty)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _loyalty = loyalty;
        }

        private async Task MigrateShoppingCartAsync(string userName)
        {
            var storeDb = HttpContext.RequestServices.GetRequiredService<MusicStoreEntities>();

            var cart = ShoppingCart.GetCart(storeDb, HttpContext);
            await cart.MigrateCartAsync(userName);

            // Merge any session wishlist into the signed-in account, mirroring the cart.
            var wishlist = Wishlist.GetWishlist(storeDb, HttpContext);
            await wishlist.MigrateWishlistAsync(userName);

            await storeDb.SaveChangesAsync();

            HttpContext.Session.SetString(ShoppingCart.CartSessionKey, userName);
            HttpContext.Session.SetString(Wishlist.WishlistSessionKey, userName);
        }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    model.UserName!, model.Password!, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    await MigrateShoppingCartAsync(model.UserName!);
                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    ModelState.AddModelError("", "Invalid username or password.");
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public IActionResult Register([FromQuery(Name = "ref")] string? referralCode = null)
        {
            return View(new RegisterViewModel { ReferralCode = referralCode });
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    EmailMarketingOptIn = model.SubscribeToNewsletter,
                    AbandonedCartOptIn = true,
                    UnsubscribeToken = Guid.NewGuid().ToString("N"),
                };
                var result = await _userManager.CreateAsync(user, model.Password!);
                if (result.Succeeded)
                {
                    // Attribute the referral (if any) and ensure the new user has their own code.
                    await _loyalty.RegisterReferralAsync(user, model.ReferralCode);
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    await MigrateShoppingCartAsync(user.UserName!);
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    AddErrors(result);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // POST: /Account/Disassociate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disassociate(string loginProvider, string providerKey)
        {
            ManageMessageId? message = null;
            var userId = _userManager.GetUserId(User);
            IdentityResult result = await _userManager.RemoveLoginAsync(
                (await _userManager.FindByIdAsync(userId!))!,
                loginProvider, providerKey);

            if (result.Succeeded)
            {
                message = ManageMessageId.RemoveLoginSuccess;
            }
            else
            {
                message = ManageMessageId.Error;
            }
            return RedirectToAction("Manage", new { Message = message });
        }

        //
        // GET: /Account/Manage
        public async Task<IActionResult> Manage(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                : message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                : message == ManageMessageId.Error ? "An error has occurred."
                : "";
            ViewBag.HasLocalPassword = await HasPasswordAsync();
            ViewBag.ReturnUrl = Url.Action("Manage");
            return View();
        }

        //
        // POST: /Account/Manage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(ManageUserViewModel model)
        {
            bool hasPassword = await HasPasswordAsync();
            ViewBag.HasLocalPassword = hasPassword;
            ViewBag.ReturnUrl = Url.Action("Manage");
            if (hasPassword)
            {
                if (ModelState.IsValid)
                {
                    var user = await _userManager.GetUserAsync(User);
                    IdentityResult result = await _userManager.ChangePasswordAsync(
                        user!, model.OldPassword!, model.NewPassword!);
                    if (result.Succeeded)
                    {
                        await _signInManager.RefreshSignInAsync(user!);
                        return RedirectToAction("Manage", new { Message = ManageMessageId.ChangePasswordSuccess });
                    }
                    else
                    {
                        AddErrors(result);
                    }
                }
            }
            else
            {
                // User does not have a password so remove any validation errors caused by a missing OldPassword field
                var state = ModelState["OldPassword"];
                if (state != null)
                {
                    state.Errors.Clear();
                }

                if (ModelState.IsValid)
                {
                    var user = await _userManager.GetUserAsync(User);
                    IdentityResult result = await _userManager.AddPasswordAsync(user!, model.NewPassword!);
                    if (result.Succeeded)
                    {
                        await _signInManager.RefreshSignInAsync(user!);
                        return RedirectToAction("Manage", new { Message = ManageMessageId.SetPasswordSuccess });
                    }
                    else
                    {
                        AddErrors(result);
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/Rewards
        public async Task<IActionResult> Rewards()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var referralCode = await _loyalty.EnsureReferralCodeAsync(user);
            var tier = _loyalty.GetTier(user.LifetimeSpend);
            var nextTier = _loyalty.GetNextTier(user.LifetimeSpend);
            var options = _loyalty.Options;
            var pointsPerDollarRedeemed = options.PointsPerDollarRedeemed > 0 ? options.PointsPerDollarRedeemed : 1;

            var model = new RewardsViewModel
            {
                Points = user.LoyaltyPoints,
                LifetimeSpend = user.LifetimeSpend,
                LifetimePointsEarned = user.LifetimePointsEarned,
                PointsDollarValue = (decimal)user.LoyaltyPoints / pointsPerDollarRedeemed,
                TierName = tier.Name,
                TierMultiplier = tier.EarnMultiplier,
                ReferralCode = referralCode,
                ReferralLink = Url.Action("Register", "Account", new { @ref = referralCode }, Request.Scheme) ?? string.Empty,
                WasReferred = !string.IsNullOrWhiteSpace(user.ReferredByCode),
                ReferrerRewardPoints = options.ReferrerRewardPoints,
                RefereeRewardPoints = options.RefereeRewardPoints,
                PointsPerDollar = options.PointsPerDollar,
                PointsPerDollarRedeemed = options.PointsPerDollarRedeemed,
            };

            if (nextTier != null)
            {
                model.NextTierName = nextTier.Name;
                model.NextTierMultiplier = nextTier.EarnMultiplier;
                model.SpendToNextTier = Math.Max(0m, nextTier.MinimumLifetimeSpend - user.LifetimeSpend);

                var span = nextTier.MinimumLifetimeSpend - tier.MinimumLifetimeSpend;
                var progressed = user.LifetimeSpend - tier.MinimumLifetimeSpend;
                model.NextTierProgressPercent = span > 0
                    ? (int)Math.Clamp(progressed / span * 100m, 0m, 100m)
                    : 0;
            }

            return View(model);
        }

        //
        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            // Request a redirect to the external login provider
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        //
        // GET: /Account/ExternalLoginCallback
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToAction("Login");
            }

            // Sign in the user with this external login provider if the user already has a login
            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false);
            if (result.Succeeded)
            {
                return RedirectToLocal(returnUrl);
            }
            else
            {
                // If the user does not have an account, prompt to create one
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.LoginProvider = info.LoginProvider;
                return View("ExternalLoginConfirmation",
                    new ExternalLoginConfirmationViewModel { UserName = info.Principal?.Identity?.Name ?? "" });
            }
        }

        //
        // POST: /Account/LinkLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LinkLogin(string provider)
        {
            var redirectUrl = Url.Action("LinkLoginCallback", "Account");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(
                provider, redirectUrl, _userManager.GetUserId(User));
            return Challenge(properties, provider);
        }

        //
        // GET: /Account/LinkLoginCallback
        public async Task<IActionResult> LinkLoginCallback()
        {
            var userId = _userManager.GetUserId(User);
            var info = await _signInManager.GetExternalLoginInfoAsync(userId);
            if (info == null)
            {
                return RedirectToAction("Manage", new { Message = ManageMessageId.Error });
            }
            var user = await _userManager.FindByIdAsync(userId!);
            var result = await _userManager.AddLoginAsync(user!, info);
            if (result.Succeeded)
            {
                return RedirectToAction("Manage");
            }
            return RedirectToAction("Manage", new { Message = ManageMessageId.Error });
        }

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(
            ExternalLoginConfirmationViewModel model, string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Manage");
            }

            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                var info = await _signInManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }
                var user = new ApplicationUser { UserName = model.UserName };
                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return RedirectToLocal(returnUrl);
                    }
                }
                AddErrors(result);
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogOff()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        //
        // GET: /Account/ExternalLoginFailure
        [AllowAnonymous]
        public IActionResult ExternalLoginFailure()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> RemoveAccountList()
        {
            var user = await _userManager.GetUserAsync(User);
            var linkedAccounts = await _userManager.GetLoginsAsync(user!);
            ViewBag.ShowRemoveButton = await HasPasswordAsync() || linkedAccounts.Count > 1;
            return PartialView("_RemoveAccountPartial", linkedAccounts);
        }

        //
        // GET: /Account/EmailPreferences
        public async Task<IActionResult> EmailPreferences()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            return View(new EmailPreferencesViewModel
            {
                Email = user.Email,
                EmailMarketingOptIn = user.EmailMarketingOptIn,
                AbandonedCartOptIn = user.AbandonedCartOptIn,
            });
        }

        //
        // POST: /Account/EmailPreferences
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmailPreferences(EmailPreferencesViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            user.Email = model.Email;
            user.EmailMarketingOptIn = model.EmailMarketingOptIn;
            user.AbandonedCartOptIn = model.AbandonedCartOptIn;
            if (string.IsNullOrWhiteSpace(user.UnsubscribeToken))
            {
                user.UnsubscribeToken = Guid.NewGuid().ToString("N");
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                ViewBag.StatusMessage = "Your email preferences have been saved.";
            }
            else
            {
                AddErrors(result);
            }

            return View(model);
        }

        //
        // GET: /Account/Unsubscribe
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Unsubscribe(string? token, string? type)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Success = false;
                return View();
            }

            // Cosmos can't translate AnyAsync/First-with-predicate cleanly; materialize a single match.
            var matches = await _userManager.Users
                .Where(u => u.UnsubscribeToken == token)
                .Take(1)
                .ToListAsync();
            var user = matches.FirstOrDefault();

            if (user == null)
            {
                ViewBag.Success = false;
                return View();
            }

            if (string.Equals(type, "marketing", StringComparison.OrdinalIgnoreCase))
            {
                user.EmailMarketingOptIn = false;
            }
            else if (string.Equals(type, "cart", StringComparison.OrdinalIgnoreCase))
            {
                user.AbandonedCartOptIn = false;
            }
            else
            {
                user.EmailMarketingOptIn = false;
                user.AbandonedCartOptIn = false;
            }

            await _userManager.UpdateAsync(user);

            ViewBag.Success = true;
            ViewBag.UnsubscribeType = type;
            return View();
        }

        #region Helpers
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
        }

        private async Task<bool> HasPasswordAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                return await _userManager.HasPasswordAsync(user);
            }
            return false;
        }

        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            Error
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }
        #endregion
    }
}