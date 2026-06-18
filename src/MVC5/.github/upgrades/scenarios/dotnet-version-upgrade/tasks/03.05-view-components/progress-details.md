# Task 03.05-view-components — Progress Details

## Changes Made
- `ViewComponents/GenreMenuViewComponent.cs`: Created. Replaces `StoreController.GenreMenu()` (ChildActionOnly). Uses DI for MusicStoreEntities. Returns the top 9 genres ordered by order count.
- `ViewComponents/CartSummaryViewComponent.cs`: Created. Replaces `ShoppingCartController.CartSummary()` (ChildActionOnly). Uses DI for MusicStoreEntities. Uses ShoppingCart to get cart items.
- `Views/Shared/Components/GenreMenu/Default.cshtml`: Created. Moved from `Views/Store/GenreMenu.cshtml` (the partial view stays in place for reference; layout updated in 03.06).
- `Views/Shared/Components/CartSummary/Default.cshtml`: Created. Moved from `Views/ShoppingCart/CartSummary.cshtml`.
