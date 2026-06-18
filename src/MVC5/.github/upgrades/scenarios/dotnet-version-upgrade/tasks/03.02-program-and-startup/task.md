# 03.02-program-and-startup: Create Program.cs replacing Global.asax + OWIN Startup

# 03.02 - Create Program.cs

## Objective
Create Program.cs as the ASP.NET Core entry point, replacing Global.asax.cs, Startup.cs (OWIN), App_Start/Startup.Auth.cs, App_Start/Startup.App.cs, App_Start/RouteConfig.cs, App_Start/BundleConfig.cs, App_Start/FilterConfig.cs.

## Steps
1. Create Program.cs with WebApplication.CreateBuilder pattern
2. Wire up: AddControllersWithViews, AddDbContext for both contexts, AddIdentity, cookie auth, UseSession, static files, routing, default route
3. Include admin user seeding from Startup.App.cs
4. Delete legacy startup files

**Done when**: Program.cs compiles; all startup logic migrated.
