using System.Text.RegularExpressions;

namespace MvcMusicStore.Services;

/// <summary>Low-level transport that delivers a fully-rendered email.</summary>
public interface IEmailSender
{
    Task SendAsync(OutgoingEmail message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience overload for callers that only have a recipient address, subject, and HTML body
    /// (for example the gift-card and gifting flows). Derives a plain-text alternative from the HTML
    /// and delegates to <see cref="SendAsync"/>.
    /// </summary>
    Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken cancellationToken = default)
        => SendAsync(
            new OutgoingEmail
            {
                ToAddress = toEmail,
                Subject = subject,
                HtmlBody = htmlMessage,
                PlainTextBody = HtmlToPlainText(htmlMessage),
            },
            cancellationToken);

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutTags = Regex.Replace(html, "<.*?>", " ");
        return Regex.Replace(withoutTags, "\\s+", " ").Trim();
    }
}
