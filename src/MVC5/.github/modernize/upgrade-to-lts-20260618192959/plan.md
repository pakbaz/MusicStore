# .NET Upgrade Plan: MvcMusicStore

## Summary

Upgrade the **MvcMusicStore** project to be fully aligned with **.NET 10 LTS** (`net10.0`).

| | Value |
|---|---|
| **Source .NET Version** | net10.0 (TargetFramework), but NuGet packages pinned to 9.0.0 |
| **Target .NET Version** | net10.0 (latest LTS) |
| **Project** | MvcMusicStore (`src/MVC5/MvcMusicStore/MvcMusicStore.csproj`) |

## Current State

The project's `<TargetFramework>` is already set to `net10.0`, however the NuGet packages are pinned to version **9.0.0**:

- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` → 9.0.0
- `Microsoft.EntityFrameworkCore.SqlServer` → 9.0.0
- `Microsoft.EntityFrameworkCore.Tools` → 9.0.0

These packages must be upgraded to their **10.x** releases to be consistent with the target framework and to take advantage of .NET 10 LTS improvements, security fixes, and API enhancements.

## Upgrade Scope

1. Update all `Microsoft.AspNetCore.*` and `Microsoft.EntityFrameworkCore.*` NuGet packages from 9.0.x → 10.0.x
2. Resolve any API breaking changes or deprecations introduced between EF Core 9 and EF Core 10
3. Verify the project builds successfully after package updates
4. Ensure all existing unit tests pass after the upgrade

## Projects in Solution

| Project | Path | Current TFM | Target TFM |
|---------|------|-------------|------------|
| MvcMusicStore | `src/MVC5/MvcMusicStore/MvcMusicStore.csproj` | net10.0 | net10.0 |
