using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    public interface IRecommendationService
    {
        /// <summary>
        /// "Customers also bought" for a single album: ranked by co-purchase frequency from order
        /// history, falling back to same-genre then same-artist albums when order data is thin.
        /// </summary>
        Task<IReadOnlyList<Album>> GetAlsoBoughtAsync(int albumId, int count = 4, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cross-sell suggestions for the current cart: aggregate co-purchase across every cart
        /// album, excluding items already in the cart, with a popularity/genre fallback.
        /// </summary>
        Task<IReadOnlyList<Album>> GetCartCrossSellAsync(IEnumerable<int> cartAlbumIds, int count = 4, CancellationToken cancellationToken = default);
    }
}
