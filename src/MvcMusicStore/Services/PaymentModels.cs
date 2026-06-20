using MvcMusicStore.Models;

namespace MvcMusicStore.Services;

/// <summary>
/// A created hosted-payment session the shopper is redirected to.
/// </summary>
public record PaymentCheckoutSession(string Id, string Url);

/// <summary>
/// Normalized status of a payment session, decoupled from any specific provider.
/// </summary>
public record PaymentSessionStatus(PaymentStatus Status, string? PaymentIntentId);

/// <summary>
/// Outcome of verifying and parsing an incoming provider webhook.
/// </summary>
public record PaymentWebhookResult(
    bool Handled,
    string? EventType = null,
    int? OrderId = null,
    string? SessionId = null,
    string? PaymentIntentId = null,
    PaymentStatus? Status = null);

/// <summary>
/// Result of a refund attempt.
/// </summary>
public record PaymentRefundResult(bool Success, string? Error = null);
