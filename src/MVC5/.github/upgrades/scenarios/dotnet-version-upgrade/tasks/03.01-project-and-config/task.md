# 03.01-project-and-config: Update csproj to net10.0 with ASP.NET Core packages + create appsettings.json

# 03.01 - Update project file and configuration

## Objective
Update MvcMusicStore.csproj to target net10.0 with the correct ASP.NET Core / EF Core packages. Create appsettings.json and appsettings.Development.json migrating settings from Web.config.

## Steps
1. Set TargetFramework to net10.0
2. Add PackageReferences: Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Tools, Microsoft.AspNetCore.Identity.EntityFrameworkCore
3. Create appsettings.json with connection strings and app settings from Web.config
4. Create appsettings.Development.json

**Done when**: csproj targets net10.0 with correct packages; appsettings.json exists with migrated settings.
