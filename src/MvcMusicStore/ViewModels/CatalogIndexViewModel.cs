using System;
using System.Collections.Generic;

namespace MvcMusicStore.ViewModels
{
    public static class CatalogSortOptions
    {
        public const string PopularityDesc = "popularity_desc";
        public const string ReleaseDateDesc = "release_desc";
        public const string ReleaseDateAsc = "release_asc";
        public const string PriceAsc = "price_asc";
        public const string PriceDesc = "price_desc";

        public static string Normalize(string? sort)
        {
            return sort switch
            {
                ReleaseDateDesc => ReleaseDateDesc,
                ReleaseDateAsc => ReleaseDateAsc,
                PriceAsc => PriceAsc,
                PriceDesc => PriceDesc,
                _ => PopularityDesc
            };
        }
    }

    public static class CatalogAvailabilityOptions
    {
        public const string All = "all";
        public const string Available = "available";
        public const string Unavailable = "unavailable";

        public static string Normalize(string? availability)
        {
            return availability switch
            {
                Available => Available,
                Unavailable => Unavailable,
                _ => All
            };
        }
    }

    public class CatalogAlbumItemViewModel
    {
        public int AlbumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string GenreName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string AlbumArtUrl { get; set; } = "~/Images/placeholder.svg";
        public DateTime? ReleaseDate { get; set; }
        public bool IsAvailable { get; set; }
        public int Popularity { get; set; }
        public string? PreviewUrl { get; set; }
        public int PreviewDurationSeconds { get; set; }
        public bool HasPreview => !string.IsNullOrWhiteSpace(PreviewUrl);
    }

    public class CatalogIndexViewModel
    {
        public string? Search { get; set; }
        public string? Genre { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string Availability { get; set; } = CatalogAvailabilityOptions.All;
        public string Sort { get; set; } = CatalogSortOptions.PopularityDesc;
        public int TotalResults { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalResults / (double)PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public List<string> Genres { get; set; } = [];
        public List<CatalogAlbumItemViewModel> Albums { get; set; } = [];
    }
}
