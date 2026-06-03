using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Services;

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendPasswordResetAsync(string recipientEmail, string resetUrl, CancellationToken cancellationToken = default);
}

public sealed class PowerAutomateEmailSender : IEmailSender
{
    private readonly BatteryPassOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PowerAutomateEmailSender> _logger;

    public PowerAutomateEmailSender(
        IOptions<BatteryPassOptions> options,
        HttpClient httpClient,
        ILogger<PowerAutomateEmailSender> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.PowerAutomateResetWebhookUrl)
        && Uri.TryCreate(_options.PowerAutomateResetWebhookUrl, UriKind.Absolute, out _);

    public async Task SendPasswordResetAsync(string recipientEmail, string resetUrl, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("Password reset email was not sent because Power Automate webhook is not configured.");
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.PowerAutomateResetWebhookUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(_options.PowerAutomateResetWebhookSecret))
        {
            request.Headers.TryAddWithoutValidation("x-battery-pass-secret", _options.PowerAutomateResetWebhookSecret);
        }

        var payload = JsonSerializer.Serialize(new
        {
            email = recipientEmail,
            resetUrl,
            appName = string.IsNullOrWhiteSpace(_options.PasswordResetAppName)
                ? "Battery Pass"
                : _options.PasswordResetAppName
        });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Power Automate password reset webhook returned {StatusCode}: {ResponseBody}",
                (int)response.StatusCode,
                responseBody);
            throw new InvalidOperationException($"Power Automate password reset webhook failed with HTTP {(int)response.StatusCode}.");
        }
    }
}
