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
    }
}