using Microsoft.EntityFrameworkCore;

namespace MvcMusicStore.Models
{
    public class MusicStoreEntities : DbContext
    {
        public MusicStoreEntities(DbContextOptions<MusicStoreEntities> options)
            : base(options)
        {
        }

        public DbSet<Album>     Albums { get; set; }
        public DbSet<Genre>     Genres { get; set; }
        public DbSet<Artist>    Artists { get; set; }
        public DbSet<Cart>      Carts { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<Order>     Orders { get; set; }

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
                b.OwnsMany(o => o.OrderDetails, d =>
                {
                    d.Ignore(x => x.Album);
                    d.Ignore(x => x.Order);
                });
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

        public async Task<int> NextAlbumIdAsync(CancellationToken cancellationToken = default)
        {
            var ids = await Albums.Select(a => a.AlbumId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }

        public async Task<int> NextGenreIdAsync(CancellationToken cancellationToken = default)
        {
            var ids = await Genres.Select(g => g.GenreId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }

        public async Task<int> NextArtistIdAsync(CancellationToken cancellationToken = default)
        {
            var ids = await Artists.Select(a => a.ArtistId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }

        public async Task<int> NextOrderIdAsync(CancellationToken cancellationToken = default)
        {
            var ids = await Orders.Select(o => o.OrderId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }

        public async Task<int> NextCartRecordIdAsync(CancellationToken cancellationToken = default)
        {
            var ids = await Carts.Select(c => c.RecordId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }

        public async Task<int> NextWishlistRecordIdAsync(CancellationToken cancellationToken = default)
        {
            var ids = await WishlistItems.Select(w => w.RecordId).ToListAsync(cancellationToken);
            return ids.Count == 0 ? 1 : ids.Max() + 1;
        }
    }
}