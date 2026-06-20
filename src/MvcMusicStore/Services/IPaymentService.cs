using MvcMusicStore.Models;

namespace MvcMusicStore.Services;

/// <summary>
/// Abstraction over a payment provider (implemented with Stripe Checkout). Keeps provider SDK
/// types out of controllers so the provider can be swapped without touching the checkout flow.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// True when the provider has the credentials it needs to process payments.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Creates a hosted checkout session for the supplied order/cart and returns the redirect URL.
    /// </summary>
    Task<PaymentCheckoutSession> CreateCheckoutSessionAsync(
        Order order,
        IReadOnlyList<Cart> items,
        string successUrl,
        string cancelUrl,
        decimal giftCardAmount = 0m,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up the current status of a previously created checkout session.
    /// </summary>
    Task<PaymentSessionStatus> GetSessionStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the signature of and parses an incoming webhook payload into a normalized result.
    /// </summary>
    PaymentWebhookResult HandleWebhook(string requestBody, string? signatureHeader);

    /// <summary>
    /// Refunds a captured payment by its payment-intent id.
    /// </summary>
    Task<PaymentRefundResult> RefundAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default);
}
