namespace MvcMusicStore.Models
{
    public class OrderDetail
    {
        public int OrderDetailId { get; set; }
        public int OrderId { get; set; }
        public int AlbumId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Denormalized album snapshot captured at checkout so the order stays a stable
        // historical record even if the album is later edited or removed. AudioUrl drives
        // the per-item digital download link.
        public string? AlbumTitle { get; set; }
        public string? AlbumArtUrl { get; set; }
        public string? AudioUrl { get; set; }

        public virtual Album? Album { get; set; }
        public virtual Order? Order { get; set; }
    }
}