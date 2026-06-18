# .NET Version Upgrade Plan — MvcMusicStore

## Overview

**Target**: MvcMusicStore — .NET Framework 4.8 → .NET 10.0 (ASP.NET Core MVC)
**Scope**: Single project, in-place full framework migration. Includes SDK-style conversion, ASP.NET Core MVC, ASP.NET Core Identity, EF Core, and appsettings.json.

### Selected Strategy
**All-at-Once** — Single project upgraded in one atomic pass.
**Rationale**: 1 project on net48, small web surface (6 controllers, ~29 views), no dependency graph to manage.

---

## Tasks

### 01-prerequisites: Verify upgrade prerequisites

Confirm that the .NET 10 SDK is installed and available, and that the solution is on the correct working branch. Verify the current project state: confirm the project is a legacy non-SDK-style csproj targeting net48 with packages.config. Document any global.json constraints that might interfere with the upgrade.

**Done when**: .NET 10 SDK is confirmed installed; current branch is verified; no global.json conflicts.

---

### 02-sdk-style-conversion: Convert project to SDK-style format

Convert `MvcMusicStore/MvcMusicStore.csproj` from legacy Visual Studio web application format (non-SDK-style, `ToolsVersion="12.0"`, `ProjectTypeGuids`, `packages.config`) to modern SDK-style format. This conversion stays on the current TFM (net48) and handles the structural change only.

Key concerns:
- Remove `packages.config` — migrate all package references to `<PackageReference>` elements in the csproj
- Remove legacy `<Import>` directives (`Microsoft.WebApplication.targets`, `NuGet.targets`, `Microsoft.CSharp.targets`)
- Remove `<ProjectTypeGuids>`, `<ProductVersion>`, `<SchemaVersion>` and other legacy properties
- Remove explicit `<Compile>`, `<Content>`, `<None>` item groups (SDK-style auto-discovers files)
- Keep assembly references that are framework GAC assemblies for now (they'll be cleaned in the next task)
- The converted project should still target net48 and build after conversion

**Done when**: `MvcMusicStore.csproj` is SDK-style format; `packages.config` is deleted; project still targets net48; `dotnet build` succeeds on the current TFM (or errors are only from incompatible package references that will be fixed in the next task).

---

### 03-migrate-to-aspnet-core: Migrate project to ASP.NET Core / net10.0

The core migration task: update the project from net48 ASP.NET MVC 5 to net10.0 ASP.NET Core MVC. This is a full framework migration touching all layers of the project.

**Project file changes**:
- Set `<TargetFramework>net10.0</TargetFramework>`
- Replace all legacy package references with net10.0-compatible packages:
  - `Microsoft.AspNetCore.App` (framework reference)
  - `Microsoft.EntityFrameworkCore.SqlServer` (EF Core)
  - `Microsoft.EntityFrameworkCore.Tools` (EF migrations)
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- Remove: all OWIN packages, System.Web.Mvc, System.Web.Optimization, WebGrease, Antlr, Microsoft.Web.Infrastructure

**Infrastructure** (create new, delete old):
- Create `Program.cs` — entry point replacing `Global.asax.cs` and OWIN `Startup.cs`. Wire up: `AddControllersWithViews()`, `AddDbContext<MusicStoreEntities>()`, `AddDbContext<ApplicationDbContext>()`, `AddIdentity<ApplicationUser, IdentityRole>()`, `AddAuthentication()` cookie auth, `UseSession()`, static files, routing.
- Create `appsettings.json` — migrate connection strings and app settings from `Web.config`
- Create `appsettings.Development.json` — development overrides
- Delete: `Global.asax`, `Global.asax.cs`, `Startup.cs` (OWIN), `App_Start/Startup.Auth.cs`, `App_Start/Startup.App.cs`, `App_Start/BundleConfig.cs`, `App_Start/FilterConfig.cs`, `App_Start/RouteConfig.cs`, `Web.config`, `Web.Debug.config`, `Web.Release.config`

**Models**:
- `IdentityModels.cs`: Update `IdentityUser` / `IdentityDbContext` to `Microsoft.AspNetCore.Identity.EntityFrameworkCore` namespaces
- `MusicStoreEntities.cs`: Migrate `DbContext` from `System.Data.Entity` to `Microsoft.EntityFrameworkCore`; add constructor accepting `DbContextOptions<MusicStoreEntities>` for DI
- `SampleData.cs`: Replace `DropCreateDatabaseIfModelChanges<MusicStoreEntities>` (EF6) with EF Core seed data using `HasData()` in `OnModelCreating` or a seeding method called from `Program.cs`
- `ShoppingCart.cs`: Replace `HttpContextBase` with `HttpContext` (ASP.NET Core); replace `Session[key]` with `HttpContext.Session.GetString(key)` / `SetString(key, value)`; inject `IHttpContextAccessor` or pass `HttpContext` directly
- `Order.cs`: Change `using System.Web.Mvc` → `using Microsoft.AspNetCore.Mvc` for `[Bind]`
- `Artist.cs`, `Genre.cs`, `Cart.cs`, `OrderDetail.cs`, `Album.cs`: Remove `System.Web` usings

**Controllers** (all controllers):
- Replace `using System.Web.Mvc` → `using Microsoft.AspNetCore.Mvc`
- `AccountController.cs`: Full rewrite using ASP.NET Core Identity `SignInManager<ApplicationUser>` and `UserManager<ApplicationUser>` injected via DI; replace `IAuthenticationManager` (OWIN) with `SignInManager`; replace `HttpContext.GetOwinContext().Authentication` with `SignInManager`; update `ChallengeResult` to use ASP.NET Core `ChallengeResult`; replace `User.Identity.GetUserId()` with `UserManager.GetUserId(User)`
- `CheckoutController.cs`: Replace `FormCollection` with model binding (`[FromForm] Order order`); replace `TryUpdateModel(order)` with direct parameter; update `HttpContext` session access
- `StoreManagerController.cs`: Replace `HttpNotFound()` with `NotFound()`; fix `using System.Data.Entity` → `using Microsoft.EntityFrameworkCore`
- `ShoppingCartController.cs`: Replace `ChildActionOnly` (remove attribute — not supported in ASP.NET Core). `CartSummary()` and `StoreController.GenreMenu()` need to be converted to View Components.
- `HomeController.cs`: Namespace only
- `StoreController.cs`: Same `ChildActionOnly` → View Component for `GenreMenu()`

**View Components** (replacing `ChildActionOnly`):
- Create `ViewComponents/GenreMenuViewComponent.cs` — replaces `StoreController.GenreMenu()`
- Create `ViewComponents/CartSummaryViewComponent.cs` — replaces `ShoppingCartController.CartSummary()`

**Views**:
- Create `Views/_ViewImports.cshtml` — add `@using Microsoft.AspNetCore.Identity`, `@using MvcMusicStore`, `@using MvcMusicStore.Models`, `@using MvcMusicStore.ViewModels`, `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`
- `Views/Shared/_Layout.cshtml`: Replace `@Scripts.Render(...)` and `@Styles.Render(...)` with direct `<link>` and `<script>` tags; replace `@Html.Action("GenreMenu", "Store")` and `@Html.Action("CartSummary", "ShoppingCart")` with `@await Component.InvokeAsync("GenreMenu")` / `@await Component.InvokeAsync("CartSummary")`; replace `@Html.Partial("_LoginPartial")` with `@await Html.PartialAsync("_LoginPartial")`
- `Views/Shared/_LoginPartial.cshtml`: Update `@using Microsoft.AspNet.Identity` → `@inject Microsoft.AspNetCore.Identity.UserManager<MvcMusicStore.Models.ApplicationUser> UserManager`; update `User.Identity.GetUserName()` → `User.Identity.Name`
- Delete `Views/Web.config`
- All views with `@Scripts.Render(...)` or `@Styles.Render(...)` — replace with direct tags or remove
- Account views: Update external login partial, ensure no `Microsoft.AspNet.Identity` usings remain

**Done when**: `dotnet build` succeeds with 0 errors and 0 warnings in the project; all `System.Web` references removed; all OWIN references removed; the project targets net10.0.

---

### 04-final-validation: Build, test, and validate

Run a complete final validation. Build the full solution, execute any existing unit tests, confirm no regressions. Document any remaining deferred items or known limitations.

If unit test projects exist: run all tests and confirm they pass. If no test projects exist (as is the case here based on the solution structure), confirm the build is clean.

**Done when**: `dotnet build` exits with 0 errors and 0 warnings; `dotnet run` starts the application without startup errors; solution is in a clean, committable state.
