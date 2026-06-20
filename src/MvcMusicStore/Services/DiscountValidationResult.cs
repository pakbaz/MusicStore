using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// Outcome of validating a discount code against a cart subtotal.
    /// </summary>
    public class DiscountValidationResult
    {
        public bool IsValid { get; init; }
        public string Message { get; init; } = string.Empty;
        public decimal DiscountAmount { get; init; }
        public string? Code { get; init; }
        public DiscountCode? AppliedCode { get; init; }

        public static DiscountValidationResult Invalid(string message) => new()
        {
            IsValid = false,
            Message = message
        };
    }
}
