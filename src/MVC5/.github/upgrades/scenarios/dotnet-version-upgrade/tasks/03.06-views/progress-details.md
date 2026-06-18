# Task 03.06-views — Progress Details

## Changes Made
- `Views/_ViewImports.cshtml`: Created with namespace imports and tag helper registration.
- `Views/Shared/_Layout.cshtml`: Replaced `@Scripts.Render`/`@Styles.Render` with direct `<link>`/`<script>` tags. Replaced `@Html.Action("GenreMenu", "Store")` and `@Html.Action("CartSummary", "ShoppingCart")` with `@await Component.InvokeAsync(...)`. Replaced `@Html.Partial("_LoginPartial")` with `@await Html.PartialAsync(...)`.
- `Views/Shared/_LoginPartial.cshtml`: Replaced `@using Microsoft.AspNet.Identity` and `User.Identity.GetUserName()` with `@inject UserManager` and `User.Identity.Name`. Updated logout form to use asp-* tag helpers.
- `Views/Account/Manage.cshtml`: Removed `Microsoft.AspNet.Identity` using; updated `@Html.Partial` to `@await Html.PartialAsync`; removed `@Html.Action("RemoveAccountList")`.
- `Views/Account/_ChangePasswordPartial.cshtml`: Replaced `User.Identity.GetUserName()` with `User.Identity?.Name`.
- `Views/Account/_ExternalLoginsListPartial.cshtml`: Replaced OWIN external auth types with ASP.NET Core `IAuthenticationSchemeProvider`.
- `Views/Account/_RemoveAccountPartial.cshtml`: Updated model type from `UserLoginInfo` to `AuthenticationToken`.
- `Views/Account/Login.cshtml`, `Register.cshtml`, `ExternalLoginConfirmation.cshtml`: Replaced `@Scripts.Render` with direct script tags.
- `Views/StoreManager/Edit.cshtml`, `Create.cshtml`: Replaced `@Scripts.Render` with direct script tags.
- `Views/Checkout/AddressAndPayment.cshtml`: Replaced `@Scripts.Render` with direct script tags.
- `Views/Web.config`: Deleted.
