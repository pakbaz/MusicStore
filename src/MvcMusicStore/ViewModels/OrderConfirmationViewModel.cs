using MvcMusicStore.Models;

namespace MvcMusicStore.ViewModels
{
    public class OrderConfirmationViewModel
    {
        public int OrderId { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string? PaymentProvider { get; set; }
        public decimal Total { get; set; }
    }
}
