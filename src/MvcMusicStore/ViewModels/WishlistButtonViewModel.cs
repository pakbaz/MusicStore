namespace MvcMusicStore.ViewModels
{
    public class WishlistButtonViewModel
    {
        public int AlbumId { get; set; }
        public bool IsSaved { get; set; }
        public string ReturnUrl { get; set; } = "/";
        public bool Large { get; set; }
    }
}
