using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services;

/// <summary>
/// High-level orchestration for store emails: resolves data, enforces consent, builds links,
/// renders templates, and dispatches via <see cref="IEmailSender"/>. All failures are logged and
/// swallowed so email never breaks checkout or the reminder worker.
/// </summary>
public sealed class StoreEmailService
{
    private readonly IEmailSender sender;
    private readonly EmailTemplateService templates;
    private readonly MusicStoreEntities db;
    private readonly EmailOptions emailOptions;
    private readonly AbandonedCartOptions cartOptions;
    private readonly ILogger<StoreEmailService> logger;

    public StoreEmailService(
        IEmailSender sender,
        EmailTemplateService templates,
        MusicStoreEntities db,
        IOptions<EmailOptions> emailOptions,
        IOptions<AbandonedCartOptions> cartOptions,
        ILogger<StoreEmailService> logger)
    {
        this.sender = sender;
        this.templates = templates;
        this.db = db;
        this.emailOptions = emailOptions.Value;
        this.cartOptions = cartOptions.Value;
        this.logger = logger;
    }

    /// <summary>Sends a transactional order receipt. Always allowed (no marketing consent required).</summary>
    public async Task SendOrderConfirmationAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (!emailOptions.Enabled)
        {
            logger.LogDebug("Email disabled; skipping order confirmation for #{OrderId}.", order.OrderId);
            return;
        }

        if (string.IsNullOrWhiteSpace(order.Email))
        {
            logger.LogInformation("Order #{OrderId} has no email address; skipping confirmation.", order.OrderId);
            return;
        }

        try
        {
            var lines = await ResolveReceiptLinesAsync(order, cancellationToken);
            var rendered = templates.OrderConfirmation(order, lines, BuildUrl("Store"));
            await sender.SendAsync(
                new OutgoingEmail
                {
                    ToAddress = order.Email!,
                    ToName = $"{order.FirstName} {order.LastName}".Trim(),
                    Subject = rendered.Subject,
                    HtmlBody = rendered.HtmlBody,
                    PlainTextBody = rendered.PlainTextBody,
                },
                cancellationToken);

            logger.LogInformation("Sent order confirmation for #{OrderId} to {Email}.", order.OrderId, order.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send order confirmation for #{OrderId}.", order.OrderId);
        }
    }

    /// <summary>
    /// Sends an abandoned-cart reminder if the user has opted in and has an email address.
    /// Returns true when an email was dispatched.
    /// </summary>
    public async Task<bool> SendAbandonedCartReminderAsync(
        ApplicationUser user,
        IReadOnlyList<Cart> items,
        CancellationToken cancellationToken = default)
    {
        if (!emailOptions.Enabled)
        {
            logger.LogDebug("Email disabled; skipping abandoned-cart reminder for {User}.", user.UserName);
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.Email) || !user.AbandonedCartOptIn || items.Count == 0)
        {
            return false;
        }

        try
        {
            var lines = items
                .Select(item => new EmailLineItem(item.AlbumTitle ?? $"Album #{item.AlbumId}", item.Count, item.AlbumPrice))
                .ToList();
            var total = lines.Sum(line => line.LineTotal);

            var unsubscribeUrl = BuildUnsubscribeUrl(user.UnsubscribeToken, "cart");
            var rendered = templates.AbandonedCart(
                user.UserName ?? "there",
                lines,
                total,
                BuildUrl("ShoppingCart") ?? "ShoppingCart",
                string.IsNullOrWhiteSpace(cartOptions.IncentiveCode) ? null : cartOptions.IncentiveCode,
                unsubscribeUrl);

            await sender.SendAsync(
                new OutgoingEmail
                {
                    ToAddress = user.Email!,
                    ToName = user.UserName,
                    Subject = rendered.Subject,
                    HtmlBody = rendered.HtmlBody,
                    PlainTextBody = rendered.PlainTextBody,
                },
                cancellationToken);

            logger.LogInformation("Sent abandoned-cart reminder to {Email}.", user.Email);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send abandoned-cart reminder to {Email}.", user.Email);
            return false;
        }
    }

    private async Task<IReadOnlyList<EmailLineItem>> ResolveReceiptLinesAsync(Order order, CancellationToken cancellationToken)
    {
        var details = order.OrderDetails ?? new List<OrderDetail>();
        var titles = new Dictionary<int, string>();

        foreach (var albumId in details.Select(d => d.AlbumId).Distinct())
        {
            // Point reads keep this Cosmos-friendly instead of materializing the whole catalog.
            var album = await db.Albums.FirstOrDefaultAsync(a => a.AlbumId == albumId, cancellationToken);
            if (album?.Title is { Length: > 0 } title)
            {
                titles[albumId] = title;
            }
        }

        return details
            .Select(d => new EmailLineItem(
                titles.TryGetValue(d.AlbumId, out var title) ? title : $"Album #{d.AlbumId}",
                d.Quantity,
                d.UnitPrice))
            .ToList();
    }

    private string BuildUnsubscribeUrl(string? token, string type)
    {
        var relative = $"Account/Unsubscribe?token={Uri.EscapeDataString(token ?? string.Empty)}&type={type}";
        return BuildUrl(relative) ?? relative;
    }

    private string? BuildUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(emailOptions.BaseUrl))
        {
            return null;
        }

        return $"{emailOptions.BaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }
}
