# 03.06-views: Update Razor views for ASP.NET Core

# 03.06 - Update views

## Objective
Update all Razor views for ASP.NET Core compatibility.

## Steps
1. Create Views/_ViewImports.cshtml with tag helper imports and namespace usings
2. Update _Layout.cshtml: replace Scripts.Render/Styles.Render with direct tags, replace Html.Action with Component.InvokeAsync
3. Update _LoginPartial.cshtml: fix Identity namespace, User.Identity.Name
4. Update all views with @Scripts.Render to use direct script tags
5. Delete Views/Web.config

**Done when**: All views compile; no System.Web.Optimization references remain.
