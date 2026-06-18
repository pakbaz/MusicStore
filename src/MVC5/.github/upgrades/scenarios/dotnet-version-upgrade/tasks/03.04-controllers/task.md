# 03.04-controllers: Migrate all controllers to ASP.NET Core MVC

# 03.04 - Migrate controllers

## Objective
Update all 6 controllers from System.Web.Mvc to Microsoft.AspNetCore.Mvc.

## Files
- AccountController.cs: Full rewrite using SignInManager/UserManager DI
- CheckoutController.cs: Fix FormCollection, TryUpdateModel, HttpContext session
- StoreManagerController.cs: Fix HttpNotFound->NotFound, EntityState namespace
- ShoppingCartController.cs: Fix CartSummary (remove ChildActionOnly)
- StoreController.cs: Fix GenreMenu (remove ChildActionOnly)
- HomeController.cs: Namespace update only

**Done when**: All controllers compile; no System.Web.Mvc references remain.
