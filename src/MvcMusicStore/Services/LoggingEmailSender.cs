using System.Text;
using Microsoft.Extensions.Options;

namespace MvcMusicStore.Services;

/// <summary>
/// Development/no-credential sender. Logs every message and, when <see cref="EmailOptions.LogDirectory"/>
/// is configured, persists the rendered HTML so emails can be inspected locally.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly EmailOptions options;
    private readonly ILogger<LoggingEmailSender> logger;

    public LoggingEmailSender(IOptions<EmailOptions> options, ILogger<LoggingEmailSender> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task SendAsync(OutgoingEmail message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[Email:Log] From=\"{From}\" To={To} <{ToName}> Subject=\"{Subject}\"",
            options.FromAddress,
            message.ToAddress,
            message.ToName,
            message.Subject);

        if (string.IsNullOrWhiteSpace(options.LogDirectory))
        {
            logger.LogDebug("[Email:Log] Plain text body:\n{Body}", message.PlainTextBody);
            return;
        }

        try
        {
            Directory.CreateDirectory(options.LogDirectory);
            var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Sanitize(message.Subject)}.html";
            var path = Path.Combine(options.LogDirectory, fileName);
            await File.WriteAllTextAsync(path, message.HtmlBody, cancellationToken);
            logger.LogInformation("[Email:Log] Wrote rendered email to {Path}", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "[Email:Log] Failed to persist email to '{Directory}'.", options.LogDirectory);
        }
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? "email" : slug[..Math.Min(slug.Length, 60)];
    }
}
