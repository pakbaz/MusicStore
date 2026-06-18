# .NET Version Upgrade: MvcMusicStore to .NET 10

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0

## Source Control
- **Source Branch**: pakbaz/miniature-winner
- **Working Branch**: dotnet-version-upgrade-net10
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)

## Upgrade Options
**Source**: .github/upgrades/scenarios/dotnet-version-upgrade/upgrade-options.md

### Strategy
- Upgrade Strategy: All-at-Once

### Project Structure
- Project Approach: In-place rewrite

### Compatibility
- System.Web Adapters: Direct Migration to ASP.NET Core APIs

### Modernization
- Configuration Migration: Auto-migrate to .NET Core Configuration
- Entity Framework: Migrate to EF Core
- Nullable Reference Types: Enable

## Strategy
**Selected**: All-at-Once
**Rationale**: Single project; framework-migration rules mandate All-at-Once for single .NET Framework projects.

### Execution Constraints
- Single atomic upgrade — all project changes in one pass
- SDK-style conversion is a separate task from TFM/API upgrade
- Validate full solution build after upgrade (0 errors, 0 warnings in modified files)
- EF Core migration done simultaneously with .NET upgrade
- No System.Web Adapters — direct API migration

## Decisions
- Upgrading from .NET Framework 4.8 to .NET 10.0 (LTS)
- Full migration: SDK-style project, ASP.NET Core MVC, ASP.NET Core Identity, EF Core, appsettings.json
- No new unit tests to be generated
- Success criteria: passBuild=true, passUnitTests=true
