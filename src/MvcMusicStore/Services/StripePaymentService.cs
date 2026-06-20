using Microsoft.Extensions.Options;
using MvcMusicStore.Models;
using Stripe;
using Stripe.Checkout;

namespace MvcMusicStore.Services;

/// <summary>
/// Stripe Checkout (hosted redirect) implementation of <see cref="IPaymentService"/>.
/// </summary>
public class StripePaymentService : IPaymentService
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly IStripeClient? _client;

    public StripePaymentService(IOptions<StripeOptions> options, ILogger<StripePaymentService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            _client = new StripeClient(_options.SecretKey);
        }
    }

    public bool IsConfigured => _client is not null;

    public async Task<PaymentCheckoutSession> CreateCheckoutSessionAsync(
        Order order,
        IReadOnlyList<Cart> items,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var currency = string.IsNullOrWhiteSpace(_options.Currency) ? "usd" : _options.Currency.ToLowerInvariant();

        var lineItems = items.Select(item => new SessionLineItemOptions
        {
            Quantity = item.Count,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = currency,
                // Stripe expects the amount in the smallest currency unit (e.g. cents).
                UnitAmountDecimal = item.AlbumPrice * 100m,
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = string.IsNullOrWhiteSpace(item.AlbumTitle) ? $"Album #{item.AlbumId}" : item.AlbumTitle,
                },
            },
        }).ToList();

        var createOptions = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            LineItems = lineItems,
            ClientReferenceId = order.OrderId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = order.OrderId.ToString(),
                ["username"] = order.Username ?? string.Empty,
            },
        };

        if (!string.IsNullOrWhiteSpace(order.Email))
        {
            createOptions.CustomerEmail = order.Email;
        }

        var service = new SessionService(_client);
        var session = await service.CreateAsync(createOptions, cancellationToken: cancellationToken);

        return new PaymentCheckoutSession(session.Id, session.Url);
    }

    public async Task<PaymentSessionStatus> GetSessionStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var service = new SessionService(_client);
        var session = await service.GetAsync(sessionId, cancellationToken: cancellationToken);

        return new PaymentSessionStatus(MapSessionStatus(session), session.PaymentIntentId);
    }

    public PaymentWebhookResult HandleWebhook(string requestBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            _logger.LogWarning("Stripe webhook received but no webhook secret is configured; ignoring.");
            return new PaymentWebhookResult(false);
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("Stripe webhook received without a signature header; ignoring.");
            return new PaymentWebhookResult(false);
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(requestBody, signatureHeader, _options.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            return new PaymentWebhookResult(false);
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
            {
                var session = stripeEvent.Data.Object as Session;
                var status = session is { PaymentStatus: "paid" or "no_payment_required" }
                    ? PaymentStatus.Paid
                    : PaymentStatus.Pending;
                return BuildSessionResult(stripeEvent.Type, session, status);
            }

            case EventTypes.CheckoutSessionAsyncPaymentSucceeded:
                return BuildSessionResult(stripeEvent.Type, stripeEvent.Data.Object as Session, PaymentStatus.Paid);

            case EventTypes.CheckoutSessionAsyncPaymentFailed:
                return BuildSessionResult(stripeEvent.Type, stripeEvent.Data.Object as Session, PaymentStatus.Failed);

            case EventTypes.CheckoutSessionExpired:
                return BuildSessionResult(stripeEvent.Type, stripeEvent.Data.Object as Session, PaymentStatus.Cancelled);

            case EventTypes.ChargeRefunded:
            {
                var charge = stripeEvent.Data.Object as Charge;
                return new PaymentWebhookResult(
                    Handled: true,
                    EventType: stripeEvent.Type,
                    PaymentIntentId: charge?.PaymentIntentId,
                    Status: PaymentStatus.Refunded);
            }

            default:
                _logger.LogInformation("Unhandled Stripe webhook event type {EventType}.", stripeEvent.Type);
                return new PaymentWebhookResult(false, stripeEvent.Type);
        }
    }

    public async Task<PaymentRefundResult> RefundAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return new PaymentRefundResult(false, "This order has no payment to refund.");
        }

        try
        {
            var service = new RefundService(_client);
            await service.CreateAsync(
                new RefundCreateOptions { PaymentIntent = paymentIntentId },
                cancellationToken: cancellationToken);
            return new PaymentRefundResult(true);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe refund failed for payment intent {PaymentIntentId}.", paymentIntentId);
            return new PaymentRefundResult(false, ex.StripeError?.Message ?? ex.Message);
        }
    }

    private static PaymentWebhookResult BuildSessionResult(string eventType, Session? session, PaymentStatus status)
    {
        int? orderId = null;
        if (session?.Metadata is not null
            && session.Metadata.TryGetValue("orderId", out var raw)
            && int.TryParse(raw, out var parsed))
        {
            orderId = parsed;
        }

        return new PaymentWebhookResult(
            Handled: true,
            EventType: eventType,
            OrderId: orderId,
            SessionId: session?.Id,
            PaymentIntentId: session?.PaymentIntentId,
            Status: status);
    }

    private static PaymentStatus MapSessionStatus(Session session)
    {
        if (session.PaymentStatus is "paid" or "no_payment_required")
        {
            return PaymentStatus.Paid;
        }

        return session.Status == "expired" ? PaymentStatus.Cancelled : PaymentStatus.Pending;
    }

    private void EnsureConfigured()
    {
        if (_client is null)
        {
            throw new InvalidOperationException("Stripe is not configured. Set Stripe:SecretKey to enable payments.");
        }
    }
}
