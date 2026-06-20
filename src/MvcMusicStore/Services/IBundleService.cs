using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    public interface IBundleService
    {
        Task<IReadOnlyList<Bundle>> GetActiveBundlesAsync(CancellationToken cancellationToken = default);

        Task<Bundle?> GetBundleAsync(int bundleId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Bundle>> GetBundlesContainingAlbumAsync(int albumId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Bundle>> GetBundlesForCartAsync(IEnumerable<int> cartAlbumIds, CancellationToken cancellationToken = default);
    }
}
