# Upgrade Options — MvcMusicStore

Assessment: 1 project (MvcMusicStore, net48), full ASP.NET MVC 5 / System.Web / OWIN / EF6 / Identity 1.x stack, 6 controllers, ~29 views

## Strategy

### Upgrade Strategy
Single project detected — All-at-Once is mandated by the framework-migration planning rules for single .NET Framework projects.

| Value | Description |
|-------|-------------|
| **All-at-Once** (selected) | Upgrade the single project in one atomic pass. No dependency graph to manage. |

## Project Structure

### Project Approach
MvcMusicStore is a small web project (6 controllers, well under 10k LOC). In-place rewrite is appropriate.

| Value | Description |
|-------|-------------|
| **In-place rewrite** (selected) | Replace the Framework web project entirely in one pass. Appropriate for small project with acceptable downtime window. |
| Side-by-side | Create new ASP.NET Core project alongside old one; migrate incrementally. Not needed for this small project. |

## Compatibility

### System.Web Adapters
In-place approach confirmed; direct API migration is cleaner for a small project.

| Value | Description |
|-------|-------------|
| **Direct Migration to ASP.NET Core APIs** (selected) | Replace all System.Web usage with native ASP.NET Core equivalents immediately. Cleaner result, no adapter layer to remove later. |
| Use System.Web Adapters | Add Microsoft.AspNetCore.SystemWebAdapters shims. Not needed for this small in-place migration. |

## Modernization

### Entity Framework
User requirements explicitly mandate migration from EF6 to EF Core. Data layer is small (2 DbContexts, 7 entity types), no EDMX or complex EF6 features.

| Value | Description |
|-------|-------------|
| **Migrate to EF Core** (selected) | Migrate from EntityFramework 6 to Microsoft.EntityFrameworkCore simultaneously with .NET upgrade. Appropriate given small data layer and user requirement. |
| Keep EF6 | Run EF6 6.3+ on .NET Core. Not appropriate here given user requirement. |

### Configuration Migration
Standard web.config with few appSettings and two connection strings. Auto-migrate is straightforward.

| Value | Description |
|-------|-------------|
| **Auto-migrate to .NET Core Configuration** (selected) | Convert web.config settings to appsettings.json and migrate code to IConfiguration. |

### Nullable Reference Types
Target is net10.0; enabling nullable reference types is best practice.

| Value | Description |
|-------|-------------|
| **Enable** (selected) | Enable nullable reference types for the upgraded project. |
| Disable | Skip for now. Not recommended for new net10.0 code. |
