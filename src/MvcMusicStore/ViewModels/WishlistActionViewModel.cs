namespace MvcMusicStore.ViewModels
{
    public class WishlistActionViewModel
    {
        public string? Message       { get; set; }
        public int WishlistCount     { get; set; }
        public int CartCount         { get; set; }
        public string? CartSummary   { get; set; }
        public int ItemId            { get; set; }
    }
}
