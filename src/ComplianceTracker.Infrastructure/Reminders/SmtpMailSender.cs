using System.Net;
using System.Net.Mail;
using ComplianceTracker.Application.Reminders;
using Microsoft.Extensions.Options;

namespace ComplianceTracker.Infrastructure.Reminders
{
    // US-183: the mail server (Mail:*). Any SMTP server works; the password is a secret, set outside the repository
    // (an environment variable such as Mail__Password). Without Mail:Host nothing is sent (R4).
    public sealed class MailOptions
    {
        public const string Section = "Mail";

        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string? From { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
    }

    // .NET's own SMTP client, so no extra package is needed
    public class SmtpMailSender : IMailSender
    {
        private readonly IOptionsMonitor<MailOptions> _options;

        public SmtpMailSender(IOptionsMonitor<MailOptions> options)
        {
            _options = options;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.CurrentValue.Host) && !string.IsNullOrWhiteSpace(_options.CurrentValue.From);

        public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            var options = _options.CurrentValue;
            using var client = new SmtpClient(options.Host, options.Port) { EnableSsl = options.EnableSsl };
            if (!string.IsNullOrWhiteSpace(options.UserName))
            {
                client.Credentials = new NetworkCredential(options.UserName, options.Password);
            }

            using var message = new MailMessage(options.From!, to, subject, body);
            await client.SendMailAsync(message, cancellationToken);
        }
    }
}
