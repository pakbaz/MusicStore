namespace MvcMusicStore.ViewModels
{
    public class ShoppingCartRemoveViewModel
    {
        public string? Message      { get; set; }
        public decimal CartTotal    { get; set; }
        public int CartCount        { get; set; }
        public int ItemCount        { get; set; }
        public decimal ItemSubtotal { get; set; }
        public string? CartSummary  { get; set; }
        public int DeleteId         { get; set; }

        public decimal Subtotal      { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? DiscountCode  { get; set; }
        public bool DiscountApplied  { get; set; }
    }
}