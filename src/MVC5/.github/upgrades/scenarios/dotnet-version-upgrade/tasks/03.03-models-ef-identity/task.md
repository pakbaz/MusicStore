# 03.03-models-ef-identity: Migrate models: EF Core DbContext, ASP.NET Core Identity, SampleData seeding

# 03.03 - Migrate models

## Objective
Migrate all model files from EF6 / ASP.NET Identity 1.x to EF Core / ASP.NET Core Identity.

## Files
- IdentityModels.cs: Update namespaces to Microsoft.AspNetCore.Identity.EntityFrameworkCore
- MusicStoreEntities.cs: EF Core DbContext with DI constructor
- SampleData.cs: Replace DropCreateDatabaseIfModelChanges with EF Core HasData or seed method
- ShoppingCart.cs: Replace HttpContextBase with HttpContext
- Order.cs: Fix Bind attribute namespace
- Album.cs, Artist.cs, Genre.cs, Cart.cs, OrderDetail.cs: Remove System.Web usings

**Done when**: All models compile against net10.0 / EF Core.
