namespace MvcMusicStore.Services;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>
    /// Stripe secret API key (sk_test_... / sk_live_...). Supply via user-secrets, environment
    /// variables, or Key Vault — never commit it.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Stripe publishable key (pk_test_... / pk_live_...). Not required for hosted Checkout, kept
    /// for completeness / future client-side use.
    /// </summary>
    public string? PublishableKey { get; set; }

    /// <summary>
    /// Webhook signing secret (whsec_...) used to verify incoming Stripe webhook events.
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// ISO currency code used for Checkout sessions.
    /// </summary>
    public string Currency { get; set; } = "usd";
}
