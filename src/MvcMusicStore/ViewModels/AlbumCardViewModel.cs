using System;
using System.Collections.Generic;
using MvcMusicStore.Models;
using MvcMusicStore.Services;

namespace MvcMusicStore.ViewModels
{
    /// <summary>
    /// Sale-aware data for a single album card. Used by the shared <c>_AlbumCard</c> partial so
    /// homepage and store grids render slashed prices and sale badges consistently.
    /// </summary>
    public class AlbumCardViewModel
    {
        public int AlbumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public decimal OriginalPrice { get; set; }
        public decimal EffectivePrice { get; set; }
        public string? SaleName { get; set; }

        public bool IsOnSale => EffectivePrice < OriginalPrice;

        public static AlbumCardViewModel FromAlbum(Album album, AlbumPricing? pricing = null, string? subtitle = null)
        {
            var price = pricing ?? AlbumPricing.NoSale(album.AlbumId, album.Price);

            return new AlbumCardViewModel
            {
                AlbumId = album.AlbumId,
                Title = album.Title ?? string.Empty,
                Subtitle = subtitle,
                ThumbnailUrl = album.GetDisplayThumbnailUrl(),
                OriginalPrice = price.OriginalPrice,
                EffectivePrice = price.EffectivePrice,
                SaleName = price.SaleName
            };
        }

        public static AlbumCardViewModel FromAlbum(
            Album album,
            IReadOnlyDictionary<int, AlbumPricing> pricingLookup,
            string? subtitle = null)
        {
            pricingLookup.TryGetValue(album.AlbumId, out var pricing);
            return FromAlbum(album, pricing, subtitle);
        }
    }
}
