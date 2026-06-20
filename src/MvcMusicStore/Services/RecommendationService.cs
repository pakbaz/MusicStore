using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly MusicStoreEntities _db;

        public RecommendationService(MusicStoreEntities db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Album>> GetAlsoBoughtAsync(int albumId, int count = 4, CancellationToken cancellationToken = default)
        {
            var albums = await _db.Albums.ToListAsync(cancellationToken);
            var byId = albums.ToDictionary(a => a.AlbumId);

            if (!byId.TryGetValue(albumId, out var seed))
            {
                return Array.Empty<Album>();
            }

            var orders = await _db.Orders.ToListAsync(cancellationToken);
            var scores = BuildCoPurchaseScores(orders, new HashSet<int> { albumId });

            var result = new List<Album>();
            var used = new HashSet<int> { albumId };

            foreach (var id in scores.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => kv.Key))
            {
                if (!byId.TryGetValue(id, out var album))
                {
                    continue;
                }

                if (used.Add(id))
                {
                    result.Add(album);
                }

                if (result.Count >= count)
                {
                    break;
                }
            }

            if (result.Count < count)
            {
                AppendFallback(result, used, albums, seed, count);
            }

            foreach (var album in result)
            {
                album.PopulateNavigation();
            }

            return result;
        }

        public async Task<IReadOnlyList<Album>> GetCartCrossSellAsync(IEnumerable<int> cartAlbumIds, int count = 4, CancellationToken cancellationToken = default)
        {
            var cartSet = (cartAlbumIds ?? Enumerable.Empty<int>()).Distinct().ToHashSet();
            var albums = await _db.Albums.ToListAsync(cancellationToken);
            var byId = albums.ToDictionary(a => a.AlbumId);

            var orders = await _db.Orders.ToListAsync(cancellationToken);
            var scores = BuildCoPurchaseScores(orders, cartSet);

            var result = new List<Album>();
            var used = new HashSet<int>(cartSet);

            foreach (var id in scores.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Select(kv => kv.Key))
            {
                if (!byId.TryGetValue(id, out var album))
                {
                    continue;
                }

                if (used.Add(id))
                {
                    result.Add(album);
                }

                if (result.Count >= count)
                {
                    break;
                }
            }

            if (result.Count < count)
            {
                var cartGenres = cartSet
                    .Where(byId.ContainsKey)
                    .Select(id => byId[id].GenreId)
                    .ToHashSet();
                var sales = BuildSalesByAlbum(orders);

                var fallback = albums
                    .Where(a => !used.Contains(a.AlbumId))
                    .OrderByDescending(a => cartGenres.Contains(a.GenreId))
                    .ThenByDescending(a => sales.TryGetValue(a.AlbumId, out var sold) ? sold : 0)
                    .ThenByDescending(a => a.IsFeatured)
                    .ThenBy(a => a.Title);

                foreach (var album in fallback)
                {
                    if (used.Add(album.AlbumId))
                    {
                        result.Add(album);
                    }

                    if (result.Count >= count)
                    {
                        break;
                    }
                }
            }

            foreach (var album in result)
            {
                album.PopulateNavigation();
            }

            return result;
        }

        private static Dictionary<int, int> BuildCoPurchaseScores(IReadOnlyList<Order> orders, HashSet<int> seeds)
        {
            var scores = new Dictionary<int, int>();
            if (seeds.Count == 0)
            {
                return scores;
            }

            foreach (var order in orders)
            {
                var details = order.OrderDetails;
                if (details is null || details.Count == 0)
                {
                    continue;
                }

                var ids = details.Select(d => d.AlbumId).Distinct().ToList();
                if (!ids.Any(seeds.Contains))
                {
                    continue;
                }

                foreach (var id in ids)
                {
                    if (seeds.Contains(id))
                    {
                        continue;
                    }

                    scores[id] = scores.TryGetValue(id, out var current) ? current + 1 : 1;
                }
            }

            return scores;
        }

        private static Dictionary<int, int> BuildSalesByAlbum(IReadOnlyList<Order> orders)
        {
            return orders
                .SelectMany(order => order.OrderDetails ?? new List<OrderDetail>())
                .GroupBy(detail => detail.AlbumId)
                .ToDictionary(group => group.Key, group => group.Sum(detail => detail.Quantity));
        }

        private static void AppendFallback(List<Album> result, HashSet<int> used, List<Album> albums, Album seed, int count)
        {
            var sameGenre = albums
                .Where(a => !used.Contains(a.AlbumId) && a.GenreId == seed.GenreId)
                .OrderByDescending(a => a.IsFeatured)
                .ThenBy(a => a.Title);

            foreach (var album in sameGenre)
            {
                if (used.Add(album.AlbumId))
                {
                    result.Add(album);
                }

                if (result.Count >= count)
                {
                    return;
                }
            }

            var sameArtist = albums
                .Where(a => !used.Contains(a.AlbumId) && a.ArtistId == seed.ArtistId)
                .OrderBy(a => a.Title);

            foreach (var album in sameArtist)
            {
                if (used.Add(album.AlbumId))
                {
                    result.Add(album);
                }

                if (result.Count >= count)
                {
                    return;
                }
            }
        }
    }
}
