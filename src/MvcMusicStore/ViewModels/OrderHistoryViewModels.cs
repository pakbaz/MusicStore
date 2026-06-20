using System.Collections.Generic;
using MvcMusicStore.Models;

namespace MvcMusicStore.ViewModels
{
    public class OrderHistoryDetailsViewModel
    {
        public Order Order { get; set; } = null!;
        public List<OrderLineItemViewModel> Items { get; set; } = new();
    }

    public class OrderLineItemViewModel
    {
        public int AlbumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
