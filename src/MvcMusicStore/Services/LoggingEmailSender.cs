using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MvcMusicStore.Services
{
    // Simulated email delivery for the sample. Real SMTP is intentionally out of scope:
    // messages are written to the application log so gift-card codes and redeem links are
    // observable, and the calling pages also surface the code/link directly to the user.
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var plainText = StripHtml(htmlMessage);
            _logger.LogInformation(
                "Simulated email -> To: {ToEmail} | Subject: {Subject}\n{Body}",
                toEmail,
                subject,
                plainText);

            return Task.CompletedTask;
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var withoutTags = Regex.Replace(html, "<.*?>", " ");
            return Regex.Replace(withoutTags, "\\s+", " ").Trim();
        }
    }
}
