using System.Net;
using System.Net.Mail;
using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Services;

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendPasswordResetAsync(string recipientEmail, string resetUrl, CancellationToken cancellationToken = default);
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly BatteryPassOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<BatteryPassOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.EmailSmtpHost)
        && _options.EmailSmtpPort > 0
        && !string.IsNullOrWhiteSpace(_options.EmailSmtpUsername)
        && !string.IsNullOrWhiteSpace(_options.EmailSmtpPassword)
        && !string.IsNullOrWhiteSpace(FromEmail);

    public async Task SendPasswordResetAsync(string recipientEmail, string resetUrl, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("Password reset email was not sent because SMTP is not configured.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(FromEmail, _options.EmailFromName),
            Subject = "Reset your Battery Pass password",
            Body = BuildPasswordResetBody(resetUrl),
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipientEmail));

        using var client = new SmtpClient(_options.EmailSmtpHost, _options.EmailSmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_options.EmailSmtpUsername, _options.EmailSmtpPassword)
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    private string FromEmail =>
        string.IsNullOrWhiteSpace(_options.EmailFromEmail)
            ? _options.EmailSmtpUsername
            : _options.EmailFromEmail;

    private static string BuildPasswordResetBody(string resetUrl) =>
        "A password reset was requested for your Battery Pass account.\n\n"
        + $"Reset password: {resetUrl}\n\n"
        + "This link expires in 60 minutes. If you did not request this, you can ignore this email.";
}
