using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    public class BundleService : IBundleService
    {
        private readonly MusicStoreEntities _db;

        public BundleService(MusicStoreEntities db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Bundle>> GetActiveBundlesAsync(CancellationToken cancellationToken = default)
        {
            var bundles = await _db.Bundles.ToListAsync(cancellationToken);
            return bundles
                .Where(bundle => bundle.IsActive && bundle.Items.Count > 0)
                .OrderByDescending(bundle => bundle.Savings)
                .ThenBy(bundle => bundle.Title)
                .ToList();
        }

        public async Task<Bundle?> GetBundleAsync(int bundleId, CancellationToken cancellationToken = default)
        {
            var bundles = await _db.Bundles.ToListAsync(cancellationToken);
            return bundles.FirstOrDefault(bundle => bundle.BundleId == bundleId);
        }

        public async Task<IReadOnlyList<Bundle>> GetBundlesContainingAlbumAsync(int albumId, CancellationToken cancellationToken = default)
        {
            var bundles = await GetActiveBundlesAsync(cancellationToken);
            return bundles
                .Where(bundle => bundle.Items.Any(item => item.AlbumId == albumId))
                .ToList();
        }

        public async Task<IReadOnlyList<Bundle>> GetBundlesForCartAsync(IEnumerable<int> cartAlbumIds, CancellationToken cancellationToken = default)
        {
            var ids = (cartAlbumIds ?? Enumerable.Empty<int>()).Distinct().ToHashSet();
            var bundles = await GetActiveBundlesAsync(cancellationToken);

            if (ids.Count == 0)
            {
                return bundles;
            }

            return bundles
                .OrderByDescending(bundle => bundle.Items.Any(item => ids.Contains(item.AlbumId)))
                .ThenByDescending(bundle => bundle.Savings)
                .ThenBy(bundle => bundle.Title)
                .ToList();
        }
    }
}
