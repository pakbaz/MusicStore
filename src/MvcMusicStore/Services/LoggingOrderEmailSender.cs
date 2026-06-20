using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services;

/// <summary>
/// Default <see cref="IOrderEmailSender"/> that renders the order confirmation receipt and
/// writes it to the application log. The app ships without a mail server, so this keeps the
/// checkout flow self-contained while leaving a clear extension seam: swap this registration
/// for an SMTP-backed sender to deliver real email without changing any callers.
/// </summary>
public class LoggingOrderEmailSender : IOrderEmailSender
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly EmailOptions _options;
    private readonly ILogger<LoggingOrderEmailSender> _logger;

    public LoggingOrderEmailSender(IOptions<EmailOptions> options, ILogger<LoggingOrderEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendOrderConfirmationAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var recipient = string.IsNullOrWhiteSpace(order.Email) ? order.Username : order.Email;
        var subject = $"Your MVC Music Store order #{order.OrderId} is confirmed";
        var body = BuildPlainTextReceipt(order);

        _logger.LogInformation(
            "Order confirmation email queued.\nFrom: {FromName} <{FromAddress}>\nTo: {Recipient}\nSubject: {Subject}\n{Body}",
            _options.FromName,
            _options.FromAddress,
            recipient,
            subject,
            body);

        return Task.CompletedTask;
    }

    private static string BuildPlainTextReceipt(Order order)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Thanks for your order, {order.FirstName} {order.LastName}!");
        sb.AppendLine();
        sb.AppendLine($"Order number: {order.OrderId}");
        sb.AppendLine($"Order date:   {order.OrderDate.ToString("f", DisplayCulture)}");
        sb.AppendLine($"Status:       {order.PaymentStatus}");
        sb.AppendLine();
        sb.AppendLine("Items");
        sb.AppendLine("-----");

        foreach (var detail in order.OrderDetails ?? new List<OrderDetail>())
        {
            var title = string.IsNullOrWhiteSpace(detail.AlbumTitle) ? $"Album #{detail.AlbumId}" : detail.AlbumTitle;
            var lineTotal = detail.UnitPrice * detail.Quantity;
            sb.AppendLine($"{title} x{detail.Quantity} - {lineTotal.ToString("C", DisplayCulture)}");
            if (!string.IsNullOrWhiteSpace(detail.AudioUrl))
            {
                sb.AppendLine($"  Download: {detail.AudioUrl}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Total: {order.Total.ToString("C", DisplayCulture)}");
        sb.AppendLine();
        sb.AppendLine("Billed to");
        sb.AppendLine("---------");
        sb.AppendLine($"{order.FirstName} {order.LastName}");
        sb.AppendLine(order.Address);
        sb.AppendLine($"{order.City}, {order.State} {order.PostalCode}");
        sb.AppendLine(order.Country);

        return sb.ToString();
    }
}
