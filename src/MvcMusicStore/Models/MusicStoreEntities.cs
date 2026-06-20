using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Services;

namespace MvcMusicStore.Models
{
    public class MusicStoreEntities : DbContext
    {
        private const string AlbumCounter = "Albums";
        private const string GenreCounter = "Genres";
        private const string ArtistCounter = "Artists";
        private const string OrderCounter = "Orders";
        private const string CartCounter = "Carts";
        private const string DiscountCodeCounter = "DiscountCodes";
        private const string SaleCounter = "Sales";
        private const string WishlistCounter = "Wishlists";
        private const string BundleCounter = "Bundles";

        private readonly CosmosCatalogOptions _catalogOptions;
        private CosmosSequenceGenerator? _sequences;

        public MusicStoreEntities(DbContextOptions<MusicStoreEntities> options, CosmosCatalogOptions catalogOptions)
            : base(options)
        {
            _catalogOptions = catalogOptions;
        }

        // Atomic, collision-free id generator backed by the dedicated Cosmos "Counters" container.
        // Built lazily from the provider's own CosmosClient so counter writes stay isolated from the
        // EF change tracker (allocating an id never persists pending catalog/order changes early).
        private CosmosSequenceGenerator Sequences =>
            _sequences ??= new CosmosSequenceGenerator(
                Database.GetCosmosClient(),
                _catalogOptions.DatabaseName,
                _catalogOptions.CountersContainerName);

        public DbSet<Album>     Albums { get; set; }
        public DbSet<Genre>     Genres { get; set; }
        public DbSet<Artist>    Artists { get; set; }
        public DbSet<Cart>      Carts { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<Order>     Orders { get; set; }
        public DbSet<DiscountCode> DiscountCodes { get; set; }
        public DbSet<Sale>      Sales { get; set; }
        public DbSet<Bundle>    Bundles { get; set; }
        public DbSet<Review>    Reviews { get; set; }
        public DbSet<GiftCard>  GiftCards { get; set; }
        public DbSet<AlbumGift> Gifts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Album>(b =>
            {
                b.ToContainer("Albums");
                b.HasKey(a => a.AlbumId);
                b.Ignore(a => a.Genre);
                b.Ignore(a => a.Artist);
                b.Ignore(a => a.OrderDetails);
            });

            modelBuilder.Entity<Genre>(b =>
            {
                b.ToContainer("Genres");
                b.HasKey(g => g.GenreId);
                b.Ignore(g => g.Albums);
            });

            modelBuilder.Entity<Artist>(b =>
            {
                b.ToContainer("Artists");
                b.HasKey(a => a.ArtistId);
            });

            modelBuilder.Entity<Cart>(b =>
            {
                b.ToContainer("Carts");
                b.HasKey(c => c.RecordId);
                b.Ignore(c => c.Album);
                b.OwnsMany(c => c.BundleItems);
            });

            modelBuilder.Entity<Bundle>(b =>
            {
                b.ToContainer("Bundles");
                b.HasKey(x => x.BundleId);
                b.OwnsMany(x => x.Items);
            });

            modelBuilder.Entity<WishlistItem>(b =>
            {
                b.ToContainer("Wishlists");
                b.HasKey(w => w.RecordId);
                b.Ignore(w => w.Album);
            });

            modelBuilder.Entity<Order>(b =>
            {
                b.ToContainer("Orders");
                b.HasKey(o => o.OrderId);
                b.Property(o => o.PaymentStatus).HasConversion<string>();
                b.OwnsMany(o => o.OrderDetails, d =>
                {
                    d.Ignore(x => x.Album);
                    d.Ignore(x => x.Order);
                });
            });

            modelBuilder.Entity<DiscountCode>(b =>
            {
                b.ToContainer("DiscountCodes");
                b.HasKey(c => c.DiscountCodeId);
            });

            modelBuilder.Entity<Review>(b =>
            {
                b.ToContainer("Reviews");
                b.HasKey(r => r.ReviewId);
            });

            modelBuilder.Entity<GiftCard>(b =>
            {
                b.ToContainer("GiftCards");
                b.HasKey(g => g.GiftCardId);
                b.OwnsMany(g => g.Transactions);
            });

            modelBuilder.Entity<AlbumGift>(b =>
            {
                b.ToContainer("Gifts");
                b.HasKey(g => g.AlbumGiftId);
            });

            modelBuilder.Entity<Sale>(b =>
            {
                b.ToContainer("Sales");
                b.HasKey(s => s.SaleId);
            });

            // The Azure Cosmos DB provider does not support index definitions; strip any conventional indexes.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var index in entityType.GetIndexes().ToList())
                {
                    entityType.RemoveIndex(index);
                }
            }
        }

        public Task<int> NextAlbumIdAsync(CancellationToken cancellationToken = default)
            => Sequences.NextAsync(AlbumCounter, MaxAlbumIdAsync, cancellationToken);

        public Task<int> NextGenreIdAsync(CancellationToken cancellationToken = default)
            => Sequences.NextAsync(GenreCounter, MaxGenreIdAsync, cancellationToken);

        public Task<int> NextArtistIdAsync(CancellationToken cancellationToken = default)
            => Sequences.NextAsync(ArtistCounter, MaxArtistIdAsync, cancellationToken);

        public Task<int> NextOrderIdAsync(CancellationToken cancellationToken = default)
            => Sequences.NextAsync(OrderCounter, MaxOrderIdAsync, cancellationToken);

        public Task<int> NextCartRecordIdAsync(CancellationToken cancellationToken = default)
            => Sequences.NextAsync(CartCounter, MaxCartRecordIdAsync, cancellationToken);

        public Task<int> NextDiscountCodeIdAsync(CancellationToken cancellationToken = default)
            => Sequences.NextAsync(DiscountCodeCounter, MaxDiscountCodeIdAsync, cancellationToken);

        public Task<int> NextSaleIdAsync(CancellationToken cancellationToken = default)
            => Sequences.NextAsync(SaleCounter, MaxSaleIdAsync, cancellationToken);

        public Task<int> NextWishlistRecordIdAsync(CancellationToken cancellationToken = default)
            => Sequences.NextAsync(WishlistCounter, MaxWishlistRecordIdAsync, cancellationToken);

        public Task<int> NextBundleIdAsync(CancellationToken cancellationToken = default)
            => Sequences.NextAsync(BundleCounter, MaxBundleIdAsync, cancellationToken);

        // Initializes every id counter from the current max in a single pass. Intended to run at
        // startup (after seeding) so the one-time MAX scan stays off the request/insert path; once
        // the counters exist this is just cheap point reads.
        public async Task EnsureSequencesInitializedAsync(CancellationToken cancellationToken = default)
        {
            await Sequences.EnsureInitializedAsync(AlbumCounter, MaxAlbumIdAsync, cancellationToken);
            await Sequences.EnsureInitializedAsync(GenreCounter, MaxGenreIdAsync, cancellationToken);
            await Sequences.EnsureInitializedAsync(ArtistCounter, MaxArtistIdAsync, cancellationToken);
            await Sequences.EnsureInitializedAsync(OrderCounter, MaxOrderIdAsync, cancellationToken);
            await Sequences.EnsureInitializedAsync(CartCounter, MaxCartRecordIdAsync, cancellationToken);
            await Sequences.EnsureInitializedAsync(DiscountCodeCounter, MaxDiscountCodeIdAsync, cancellationToken);
            await Sequences.EnsureInitializedAsync(SaleCounter, MaxSaleIdAsync, cancellationToken);
            await Sequences.EnsureInitializedAsync(WishlistCounter, MaxWishlistRecordIdAsync, cancellationToken);
            await Sequences.EnsureInitializedAsync(BundleCounter, MaxBundleIdAsync, cancellationToken);
        }

        private async Task<int> MaxAlbumIdAsync(CancellationToken cancellationToken)
        {
            var ids = await Albums.Select(a => a.AlbumId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 0 : ids.Max();
        }

        private async Task<int> MaxGenreIdAsync(CancellationToken cancellationToken)
        {
            var ids = await Genres.Select(g => g.GenreId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 0 : ids.Max();
        }

        private async Task<int> MaxArtistIdAsync(CancellationToken cancellationToken)
        {
            var ids = await Artists.Select(a => a.ArtistId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 0 : ids.Max();
        }

        private async Task<int> MaxOrderIdAsync(CancellationToken cancellationToken)
        {
            var ids = await Orders.Select(o => o.OrderId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 0 : ids.Max();
        }

        private async Task<int> MaxCartRecordIdAsync(CancellationToken cancellationToken)
        {
            var ids = await Carts.Select(c => c.RecordId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 0 : ids.Max();
        }

        private async Task<int> MaxDiscountCodeIdAsync(CancellationToken cancellationToken)
        {
            var ids = await DiscountCodes.Select(c => c.DiscountCodeId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 0 : ids.Max();
        }

        private async Task<int> MaxSaleIdAsync(CancellationToken cancellationToken)
        {
            var ids = await Sales.Select(s => s.SaleId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 0 : ids.Max();
        }

        private async Task<int> MaxWishlistRecordIdAsync(CancellationToken cancellationToken)
        {
            var ids = await WishlistItems.Select(w => w.RecordId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 0 : ids.Max();
        }

        private async Task<int> MaxBundleIdAsync(CancellationToken cancellationToken)
        {
            var ids = await Bundles.Select(b => b.BundleId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 0 : ids.Max();
        }

        public async Task<int> NextGiftCardIdAsync(CancellationToken cancellationToken = default)
        {
            var ids = await GiftCards.Select(g => g.GiftCardId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }

        public async Task<int> NextAlbumGiftIdAsync(CancellationToken cancellationToken = default)
        {
            var ids = await Gifts.Select(g => g.AlbumGiftId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }

        public async Task<int> NextReviewIdAsync(CancellationToken cancellationToken = default)
        {
            var ids = await Reviews.Select(r => r.ReviewId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }
    }
}