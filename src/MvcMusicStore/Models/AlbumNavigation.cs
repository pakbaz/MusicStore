using System.Collections.Generic;

namespace MvcMusicStore.Models
{
    /// <summary>
    /// With Cosmos DB the catalog is denormalized and navigation properties are not loaded by EF.
    /// These helpers rebuild lightweight Genre/Artist stubs from the denormalized columns so the
    /// existing Razor views (which read <c>album.Genre.Name</c> / <c>album.Artist.Name</c>) keep working.
    /// </summary>
    public static class AlbumNavigation
    {
        public static Album PopulateNavigation(this Album album)
        {
            if (album is null)
            {
                return album!;
            }

            album.Genre ??= new Genre { GenreId = album.GenreId, Name = album.GenreName };
            album.Artist ??= new Artist { ArtistId = album.ArtistId, Name = album.ArtistName };
            return album;
        }

        public static IEnumerable<Album> PopulateNavigation(this IEnumerable<Album> albums)
        {
            foreach (var album in albums)
            {
                album.PopulateNavigation();
            }

            return albums;
        }

        public static Cart PopulateAlbum(this Cart cart)
        {
            if (cart is null)
            {
                return cart!;
            }

            cart.Album ??= new Album
            {
                AlbumId = cart.AlbumId,
                Title = cart.AlbumTitle,
                Price = cart.AlbumPrice,
                AlbumArtUrl = cart.AlbumArtUrl
            };
            return cart;
        }

        public static IEnumerable<Cart> PopulateAlbum(this IEnumerable<Cart> carts)
        {
            foreach (var cart in carts)
            {
                cart.PopulateAlbum();
            }

            return carts;
        }
    }
}
