# Task 03.07-delete-legacy — Progress Details

## Files Deleted
- `Global.asax` — replaced by Program.cs
- `Global.asax.cs` — replaced by Program.cs
- `Startup.cs` — OWIN startup, replaced by Program.cs
- `Web.config` — replaced by appsettings.json
- `Web.Debug.config` — Web.config transform, not needed
- `Web.Release.config` — Web.config transform, not needed
- `Properties/AssemblyInfo.cs` — SDK-style auto-generates assembly info
- `App_Start/` (entire directory) — all App_Start files replaced by Program.cs:
  - `Startup.Auth.cs`, `Startup.App.cs`, `RouteConfig.cs`, `BundleConfig.cs`, `FilterConfig.cs`
