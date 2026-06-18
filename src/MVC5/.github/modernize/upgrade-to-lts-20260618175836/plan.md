# .NET Upgrade Plan: MvcMusicStore

## Summary

Upgrade **MvcMusicStore** from **.NET Framework 4.8** to **.NET 10.0** (latest LTS).

## Source Version

- Framework: .NET Framework 4.8
- Project style: Legacy (non-SDK-style) `.csproj`
- Web framework: ASP.NET MVC 5 (`System.Web.Mvc`)
- ORM: Entity Framework 6
- Auth: ASP.NET Identity 1.0 + OWIN middleware

## Target Version

- Framework: **.NET 10.0** (`net10.0`)
- Web framework: **ASP.NET Core MVC**

## Projects in Solution

| Project | Path | Current TFM |
|---------|------|-------------|
| MvcMusicStore | `MvcMusicStore/MvcMusicStore.csproj` | `net48` (.NET Framework 4.8) |

## Upgrade Scope

This upgrade requires a full migration from .NET Framework to modern .NET, including:

1. **SDK-style project file conversion** — replace the legacy `<Project ToolsVersion="...">` format with the modern SDK-style `<Project Sdk="Microsoft.NET.Sdk.Web">` format.
2. **Target framework moniker update** — change `<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>` to `<TargetFramework>net10.0</TargetFramework>`.
3. **ASP.NET MVC 5 → ASP.NET Core MVC migration** — replace `System.Web`-based APIs (controllers, filters, routing, bundling, HTTP context) with ASP.NET Core equivalents.
4. **OWIN/Katana → ASP.NET Core middleware** — replace Startup.Auth.cs / `IAppBuilder`-based pipeline with `WebApplication`/`IMiddleware` pipeline.
5. **ASP.NET Identity 1.x → ASP.NET Core Identity** — upgrade identity models, user manager, and authentication cookies.
6. **Entity Framework 6 → EF Core** — update DbContext, model configurations, migrations, and SQL Server provider.
7. **NuGet package updates** — replace all `.NET Framework`-only packages with their .NET-compatible equivalents (or remove obsolete ones).
8. **Configuration migration** — replace `Web.config` / `Web.Debug.config` / `Web.Release.config` with `appsettings.json` and environment-specific overrides.
9. **Build and API compatibility fixes** — resolve any breaking changes surfaced during compilation.

## Task List

- `001-upgrade-dotnet-to-net10`: Upgrade MvcMusicStore from .NET Framework 4.8 to .NET 10.0
