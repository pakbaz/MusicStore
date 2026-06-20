using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    public class PromotionService : IPromotionService
    {
        private readonly MusicStoreEntities _db;

        public PromotionService(MusicStoreEntities db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Sale>> GetActiveSalesAsync(
            DateTime? nowUtc = null, CancellationToken cancellationToken = default)
        {
            var now = nowUtc ?? DateTime.UtcNow;

            // Cosmos cannot translate the date-window comparison reliably, so load then filter.
            var sales = await _db.Sales.ToListAsync(cancellationToken);
            return sales.Where(s => s.IsActiveAt(now)).ToList();
        }

        public AlbumPricing GetPricing(Album album, IReadOnlyList<Sale> activeSales) =>
            BestPricing(album.AlbumId, album.Price, activeSales);

        public async Task<AlbumPricing> GetPricingAsync(Album album, CancellationToken cancellationToken = default)
        {
            var activeSales = await GetActiveSalesAsync(cancellationToken: cancellationToken);
            return GetPricing(album, activeSales);
        }

        public async Task<IReadOnlyDictionary<int, AlbumPricing>> GetPricingLookupAsync(
            IEnumerable<Album> albums, CancellationToken cancellationToken = default)
        {
            var activeSales = await GetActiveSalesAsync(cancellationToken: cancellationToken);
            var lookup = new Dictionary<int, AlbumPricing>();

            foreach (var album in albums)
            {
                lookup[album.AlbumId] = BestPricing(album.AlbumId, album.Price, activeSales);
            }

            return lookup;
        }

        public async Task<DealOfTheDay?> GetFeaturedDealAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var activeSales = await GetActiveSalesAsync(now, cancellationToken);

            var featured = activeSales
                .Where(s => s.IsFeatured)
                .OrderBy(s => s.EndDateUtc)
                .FirstOrDefault();

            if (featured is null)
            {
                return null;
            }

            var albums = await _db.Albums.ToListAsync(cancellationToken);

            // Prefer an explicitly targeted album; fall back to the priciest album for a storewide deal.
            Album? album = featured.AlbumIds
                .Select(id => albums.FirstOrDefault(a => a.AlbumId == id))
                .FirstOrDefault(a => a is not null);

            if (album is null && featured.AppliesToEntireStore)
            {
                album = albums.OrderByDescending(a => a.Price).FirstOrDefault();
            }

            if (album is null)
            {
                return null;
            }

            album.PopulateNavigation();
            var pricing = BestPricing(album.AlbumId, album.Price, new[] { featured });

            return new DealOfTheDay
            {
                Sale = featured,
                Album = album,
                Pricing = pricing
            };
        }

        public async Task<DiscountValidationResult> ValidateCodeAsync(
            string? code, decimal saleAdjustedSubtotal, CancellationToken cancellationToken = default)
        {
            var normalized = DiscountCode.Normalize(code);
            if (string.IsNullOrEmpty(normalized))
            {
                return DiscountValidationResult.Invalid("Enter a discount code.");
            }

            var codes = await _db.DiscountCodes.ToListAsync(cancellationToken);
            var entity = codes.FirstOrDefault(c => DiscountCode.Normalize(c.Code) == normalized);
            var now = DateTime.UtcNow;

            if (entity is null)
            {
                return DiscountValidationResult.Invalid($"\u201c{normalized}\u201d is not a valid discount code.");
            }

            if (!entity.IsActive)
            {
                return DiscountValidationResult.Invalid($"Code \u201c{normalized}\u201d is no longer active.");
            }

            if (entity.StartDateUtc is { } start && start > now)
            {
                return DiscountValidationResult.Invalid($"Code \u201c{normalized}\u201d is not active yet.");
            }

            if (entity.EndDateUtc is { } end && now > end)
            {
                return DiscountValidationResult.Invalid($"Code \u201c{normalized}\u201d has expired.");
            }

            if (!entity.HasRemainingUses)
            {
                return DiscountValidationResult.Invalid($"Code \u201c{normalized}\u201d has reached its usage limit.");
            }

            if (entity.MinimumSpend is { } minimum && saleAdjustedSubtotal < minimum)
            {
                return DiscountValidationResult.Invalid(
                    $"Code \u201c{normalized}\u201d requires a {minimum:C} minimum spend.");
            }

            var amount = Math.Min(saleAdjustedSubtotal,
                Discounts.AmountOff(saleAdjustedSubtotal, entity.DiscountType, entity.Value));

            if (amount <= 0m)
            {
                return DiscountValidationResult.Invalid("This code doesn't reduce your current total.");
            }

            return new DiscountValidationResult
            {
                IsValid = true,
                Code = normalized,
                AppliedCode = entity,
                DiscountAmount = Discounts.Round(amount),
                Message = entity.DiscountType == DiscountType.Percentage
                    ? $"Code \u201c{normalized}\u201d applied \u2014 {entity.Value:0.##}% off."
                    : $"Code \u201c{normalized}\u201d applied \u2014 {entity.Value:C} off."
            };
        }

        public async Task<CartPricing> PriceCartAsync(
            IEnumerable<Cart> cartItems, string? code, CancellationToken cancellationToken = default)
        {
            var activeSales = await GetActiveSalesAsync(cancellationToken: cancellationToken);
            var items = cartItems.ToList();

            var lines = new List<CartLine>();
            decimal subtotal = 0m;
            decimal originalSubtotal = 0m;

            foreach (var item in items)
            {
                var pricing = BestPricing(item.AlbumId, item.AlbumPrice, activeSales);
                var line = new CartLine
                {
                    RecordId = item.RecordId,
                    AlbumId = item.AlbumId,
                    Title = item.AlbumTitle ?? string.Empty,
                    Quantity = item.Count,
                    UnitPrice = item.AlbumPrice,
                    EffectiveUnitPrice = pricing.EffectivePrice,
                    SaleName = pricing.SaleName,
                    SaleEndsUtc = pricing.SaleEndsUtc
                };

                lines.Add(line);
                subtotal += line.LineSubtotal;
                originalSubtotal += line.OriginalLineSubtotal;
            }

            subtotal = Discounts.Round(subtotal);
            originalSubtotal = Discounts.Round(originalSubtotal);

            string? appliedCode = null;
            DiscountCode? appliedEntity = null;
            decimal discountAmount = 0m;
            string? message = null;

            if (!string.IsNullOrWhiteSpace(code))
            {
                var result = await ValidateCodeAsync(code, subtotal, cancellationToken);
                message = result.Message;
                if (result.IsValid)
                {
                    appliedCode = result.Code;
                    appliedEntity = result.AppliedCode;
                    discountAmount = result.DiscountAmount;
                }
            }

            var total = Discounts.Round(Math.Max(0m, subtotal - discountAmount));

            return new CartPricing
            {
                Lines = lines,
                Subtotal = subtotal,
                OriginalSubtotal = originalSubtotal,
                AppliedCode = appliedCode,
                AppliedDiscountCode = appliedEntity,
                DiscountAmount = discountAmount,
                DiscountMessage = message,
                Total = total
            };
        }

        private static AlbumPricing BestPricing(int albumId, decimal price, IReadOnlyList<Sale> activeSales)
        {
            var best = AlbumPricing.NoSale(albumId, price);

            foreach (var sale in activeSales)
            {
                if (!sale.AppliesTo(albumId))
                {
                    continue;
                }

                var effective = Discounts.Apply(price, sale.DiscountType, sale.Value);
                if (effective < best.EffectivePrice)
                {
                    best = new AlbumPricing
                    {
                        AlbumId = albumId,
                        OriginalPrice = price,
                        EffectivePrice = effective,
                        SaleName = sale.Name,
                        SaleEndsUtc = sale.EndDateUtc
                    };
                }
            }

            return best;
        }
    }
}
