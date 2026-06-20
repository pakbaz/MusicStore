using System.Collections.Generic;
using MvcMusicStore.Models;

namespace MvcMusicStore.ViewModels
{
    public class ShoppingCartViewModel
    {
        public List<Cart>? CartItems { get; set; }
        public decimal CartTotal    { get; set; }
        public int CartCount        { get; set; }
        public List<Album> Recommendations { get; set; } = new();
        public List<Bundle> SuggestedBundles { get; set; } = new();
        public string? Message      { get; set; }
    }
}