using System.Collections.Generic;
using MvcMusicStore.Models;
using MvcMusicStore.Services;

namespace MvcMusicStore.ViewModels
{
    public class ShoppingCartViewModel
    {
        public List<Cart>? CartItems { get; set; }

        /// <summary>Final amount payable (sale-adjusted subtotal minus any discount code).</summary>
        public decimal CartTotal    { get; set; }

        /// <summary>Sale-adjusted line pricing plus any applied discount code.</summary>
        public CartPricing? Pricing { get; set; }

        /// <summary>Status message from the most recent apply/remove discount action.</summary>
        public string? DiscountMessage { get; set; }
    }
}