using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// Central promotions engine: computes sale-adjusted album pricing, validates and prices
    /// discount codes, and surfaces the featured "Deal of the Day".
    /// </summary>
    public interface IPromotionService
    {
        Task<IReadOnlyList<Sale>> GetActiveSalesAsync(DateTime? nowUtc = null, CancellationToken cancellationToken = default);

        AlbumPricing GetPricing(Album album, IReadOnlyList<Sale> activeSales);

        Task<AlbumPricing> GetPricingAsync(Album album, CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<int, AlbumPricing>> GetPricingLookupAsync(
            IEnumerable<Album> albums, CancellationToken cancellationToken = default);

        Task<DealOfTheDay?> GetFeaturedDealAsync(CancellationToken cancellationToken = default);

        Task<DiscountValidationResult> ValidateCodeAsync(
            string? code, decimal saleAdjustedSubtotal, CancellationToken cancellationToken = default);

        Task<CartPricing> PriceCartAsync(
            IEnumerable<Cart> cartItems, string? code, CancellationToken cancellationToken = default);
    }
}
