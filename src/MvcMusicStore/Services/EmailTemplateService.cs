using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services;

/// <summary>A single line item rendered in a receipt or cart summary.</summary>
public sealed record EmailLineItem(string Title, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

/// <summary>The subject and both bodies produced for a templated email.</summary>
public sealed record RenderedEmail(string Subject, string HtmlBody, string PlainTextBody);

/// <summary>Builds brand-consistent HTML and plain-text bodies for store emails.</summary>
public sealed class EmailTemplateService
{
    private readonly EmailOptions options;

    public EmailTemplateService(IOptions<EmailOptions> options)
    {
        this.options = options.Value;
    }

    public RenderedEmail OrderConfirmation(Order order, IReadOnlyList<EmailLineItem> lines, string? storeUrl)
    {
        var name = string.IsNullOrWhiteSpace(order.FirstName) ? "there" : order.FirstName!;
        var subject = $"Your MVC Music Store order #{order.OrderId}";

        var html = new StringBuilder();
        html.Append($"<p style=\"margin:0 0 16px\">Hi {Encode(name)}, thanks for your order! We've received it and your confirmation number is <strong>#{order.OrderId}</strong>.</p>");
        html.Append(LineItemsTable(lines, order.Total));
        html.Append(AddressBlock(order));
        if (!string.IsNullOrWhiteSpace(storeUrl))
        {
            html.Append(Button("Continue shopping", storeUrl!));
        }

        var text = new StringBuilder();
        text.AppendLine($"Hi {name}, thanks for your order!");
        text.AppendLine($"Confirmation number: #{order.OrderId}");
        text.AppendLine($"Order date: {order.OrderDate:g}");
        text.AppendLine();
        AppendLineItemsText(text, lines, order.Total);
        if (!string.IsNullOrWhiteSpace(storeUrl))
        {
            text.AppendLine();
            text.AppendLine($"Continue shopping: {storeUrl}");
        }

        return new RenderedEmail(subject, Wrap("Order confirmed", html.ToString(), footerHtml: null), text.ToString());
    }

    public RenderedEmail AbandonedCart(
        string displayName,
        IReadOnlyList<EmailLineItem> lines,
        decimal cartTotal,
        string cartUrl,
        string? incentiveCode,
        string unsubscribeUrl)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "there" : displayName;
        var subject = "You left something in your cart";

        var html = new StringBuilder();
        html.Append($"<p style=\"margin:0 0 16px\">Hi {Encode(name)}, your cart is still waiting for you. Here's what you picked out:</p>");
        html.Append(LineItemsTable(lines, cartTotal));
        if (!string.IsNullOrWhiteSpace(incentiveCode))
        {
            html.Append(
                "<div style=\"margin:20px 0;padding:16px;border:1px dashed #6b46c1;border-radius:8px;background:#f5f3ff;text-align:center\">" +
                "<div style=\"font-size:13px;color:#4c1d95;text-transform:uppercase;letter-spacing:.05em\">Here's a little something</div>" +
                $"<div style=\"font-size:22px;font-weight:700;color:#4c1d95;margin-top:4px\">{Encode(incentiveCode!)}</div>" +
                "<div style=\"font-size:13px;color:#4c1d95;margin-top:4px\">Apply this code at checkout.</div>" +
                "</div>");
        }

        html.Append(Button("Return to your cart", cartUrl));

        var text = new StringBuilder();
        text.AppendLine($"Hi {name}, your cart is still waiting for you.");
        text.AppendLine();
        AppendLineItemsText(text, lines, cartTotal);
        if (!string.IsNullOrWhiteSpace(incentiveCode))
        {
            text.AppendLine();
            text.AppendLine($"Use code {incentiveCode} at checkout.");
        }

        text.AppendLine();
        text.AppendLine($"Return to your cart: {cartUrl}");

        var footer =
            $"You're receiving this because you have items in your cart at MVC Music Store. " +
            $"<a href=\"{Encode(unsubscribeUrl)}\" style=\"color:#9ca3af\">Unsubscribe from cart reminders</a>.";
        var textFooter = $"Unsubscribe from cart reminders: {unsubscribeUrl}";
        text.AppendLine();
        text.AppendLine(textFooter);

        return new RenderedEmail(subject, Wrap("Your cart is waiting", html.ToString(), footer), text.ToString());
    }

    private static string LineItemsTable(IReadOnlyList<EmailLineItem> lines, decimal total)
    {
        var sb = new StringBuilder();
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;margin:8px 0 16px\">");
        sb.Append("<thead><tr>" +
            "<th align=\"left\" style=\"padding:8px 0;border-bottom:2px solid #e5e7eb;font-size:13px;color:#6b7280\">Item</th>" +
            "<th align=\"center\" style=\"padding:8px 0;border-bottom:2px solid #e5e7eb;font-size:13px;color:#6b7280\">Qty</th>" +
            "<th align=\"right\" style=\"padding:8px 0;border-bottom:2px solid #e5e7eb;font-size:13px;color:#6b7280\">Price</th>" +
            "</tr></thead><tbody>");

        foreach (var line in lines)
        {
            sb.Append("<tr>" +
                $"<td style=\"padding:10px 0;border-bottom:1px solid #f3f4f6\">{Encode(line.Title)}</td>" +
                $"<td align=\"center\" style=\"padding:10px 0;border-bottom:1px solid #f3f4f6\">{line.Quantity}</td>" +
                $"<td align=\"right\" style=\"padding:10px 0;border-bottom:1px solid #f3f4f6\">{Money(line.LineTotal)}</td>" +
                "</tr>");
        }

        sb.Append("</tbody><tfoot><tr>" +
            "<td colspan=\"2\" align=\"right\" style=\"padding:12px 0;font-weight:700\">Total</td>" +
            $"<td align=\"right\" style=\"padding:12px 0;font-weight:700\">{Money(total)}</td>" +
            "</tr></tfoot></table>");
        return sb.ToString();
    }

    private static void AppendLineItemsText(StringBuilder text, IReadOnlyList<EmailLineItem> lines, decimal total)
    {
        foreach (var line in lines)
        {
            text.AppendLine($"- {line.Title} x{line.Quantity}  {Money(line.LineTotal)}");
        }

        text.AppendLine($"Total: {Money(total)}");
    }

    private static string AddressBlock(Order order)
    {
        var hasAddress = !string.IsNullOrWhiteSpace(order.Address);
        if (!hasAddress)
        {
            return string.Empty;
        }

        var lines = new[]
        {
            $"{order.FirstName} {order.LastName}".Trim(),
            order.Address,
            $"{order.City}, {order.State} {order.PostalCode}".Trim(',', ' '),
            order.Country,
        };

        var rows = string.Join("<br>", lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => Encode(l!)));

        return
            "<div style=\"margin:16px 0;font-size:14px;color:#374151\">" +
            "<div style=\"font-size:13px;color:#6b7280;text-transform:uppercase;letter-spacing:.05em;margin-bottom:4px\">Shipping to</div>" +
            $"{rows}</div>";
    }

    private static string Button(string label, string url) =>
        "<div style=\"margin:24px 0\">" +
        $"<a href=\"{Encode(url)}\" style=\"display:inline-block;background:#6b46c1;color:#ffffff;text-decoration:none;" +
        "padding:12px 24px;border-radius:8px;font-weight:600\">" +
        $"{Encode(label)}</a></div>";

    private string Wrap(string heading, string innerHtml, string? footerHtml)
    {
        var storeName = Encode(options.FromName);
        var footer = string.IsNullOrWhiteSpace(footerHtml)
            ? $"You're receiving this email from {storeName}."
            : footerHtml;

        return
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head>" +
            "<body style=\"margin:0;padding:0;background:#f3f4f6;font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#111827\">" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f3f4f6;padding:24px 0\"><tr><td align=\"center\">" +
            "<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:600px;max-width:92%;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,.08)\">" +
            "<tr><td style=\"background:#1f1147;padding:20px 28px\">" +
            $"<div style=\"color:#ffffff;font-size:18px;font-weight:700\">{storeName}</div>" +
            $"<div style=\"color:#c4b5fd;font-size:14px;margin-top:2px\">{Encode(heading)}</div>" +
            "</td></tr>" +
            $"<tr><td style=\"padding:28px\">{innerHtml}</td></tr>" +
            $"<tr><td style=\"padding:20px 28px;background:#f9fafb;border-top:1px solid #f3f4f6;font-size:12px;color:#9ca3af\">{footer}</td></tr>" +
            "</table></td></tr></table></body></html>";
    }

    private static string Money(decimal value) =>
        "$" + value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
