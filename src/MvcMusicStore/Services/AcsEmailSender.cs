using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace MvcMusicStore.Services;

/// <summary>Production sender backed by Azure Communication Services Email.</summary>
public sealed class AcsEmailSender : IEmailSender
{
    private readonly EmailClient client;
    private readonly EmailOptions options;
    private readonly ILogger<AcsEmailSender> logger;

    public AcsEmailSender(EmailClient client, IOptions<EmailOptions> options, ILogger<AcsEmailSender> logger)
    {
        this.client = client;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task SendAsync(OutgoingEmail message, CancellationToken cancellationToken = default)
    {
        var content = new EmailContent(message.Subject)
        {
            PlainText = message.PlainTextBody,
            Html = message.HtmlBody,
        };

        var recipients = new EmailRecipients(new List<EmailAddress>
        {
            new(message.ToAddress, message.ToName ?? message.ToAddress),
        });

        var email = new EmailMessage(options.FromAddress, recipients, content);

        EmailSendOperation operation = await client.SendAsync(WaitUntil.Started, email, cancellationToken);
        logger.LogInformation(
            "[Email:Acs] Queued message {OperationId} to {To} (\"{Subject}\").",
            operation.Id,
            message.ToAddress,
            message.Subject);
    }
}
