# Task 03.04-controllers — Progress Details

## Changes Made
- `Controllers/AccountController.cs`: Full rewrite. Replaced OWIN-based auth with ASP.NET Core Identity `SignInManager<ApplicationUser>` and `UserManager<ApplicationUser>` injected via DI. Removed OWIN `IAuthenticationManager`, `ChallengeResult` (HttpUnauthorizedResult), `GetOwinContext()`. Updated all Identity API calls to ASP.NET Core versions.
- `Controllers/HomeController.cs`: Updated namespace (System.Web.Mvc → Microsoft.AspNetCore.Mvc). Added DI constructor for MusicStoreEntities. Changed ActionResult → IActionResult.
- `Controllers/StoreController.cs`: Updated namespace. Added DI constructor. Changed ActionResult → IActionResult. Removed ChildActionOnly GenreMenu (moved to ViewComponent in 03.05).
- `Controllers/CheckoutController.cs`: Updated namespace. Added DI constructor. Replaced `FormCollection values` with `Order order` model binding. Removed `TryUpdateModel`. Added `Request.Form["PromoCode"]` for promo code check.
- `Controllers/StoreManagerController.cs`: Updated namespace. Added DI constructor. Replaced `HttpNotFound()` with `NotFound()`. Fixed `System.Data.Entity.EntityState` → `Microsoft.EntityFrameworkCore.EntityState`. Replaced `SelectList` with fully qualified `Microsoft.AspNetCore.Mvc.Rendering.SelectList`.
- `Controllers/ShoppingCartController.cs`: Updated namespace. Added DI constructor. Removed `ChildActionOnly` CartSummary (moved to ViewComponent in 03.05). Changed `HttpContext` usage to pass directly to ShoppingCart.GetCart.
