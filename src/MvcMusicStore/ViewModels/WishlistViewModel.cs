using System.Collections.Generic;
using MvcMusicStore.Models;

namespace MvcMusicStore.ViewModels
{
    public class WishlistViewModel
    {
        public List<WishlistItem> WishlistItems { get; set; } = new();
    }
}
