using System.Security.Cryptography;
using System.Text;

namespace BatteryPassWeb.Services;

public sealed class BatteryIdService
{
    public const int IdLength = 70;
    private const string DevelopmentFallbackSecret = "battery-pass-local-development-id-secret";
    private readonly byte[] _secret;

    public BatteryIdService(string secret, bool allowMissingSecret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            if (!allowMissingSecret)
            {
                throw new InvalidOperationException("ID_GENERATION_SECRET is required for Battery ID and Passport ID generation.");
            }

            secret = DevelopmentFallbackSecret;
        }

        _secret = Encoding.UTF8.GetBytes(secret.Trim());
    }

    public string CreateBatteryId(string batteryFamily, string batterySerialNumber)
    {
        var normalizedFamily = NormalizeIdentityPart(batteryFamily);
        var normalizedSerial = NormalizeIdentityPart(batterySerialNumber);
        if (string.IsNullOrWhiteSpace(normalizedFamily) || string.IsNullOrWhiteSpace(normalizedSerial))
        {
            throw new ArgumentException("Battery Family and Battery serial number are required.");
        }

        return CreateOpaqueId($"battery|{normalizedFamily}|{normalizedSerial}");
    }

    public string CreatePassportId(string batteryId, string batteryModel, DateTimeOffset snapshotCreatedAt)
    {
        var normalizedBatteryId = NormalizeIdentityPart(batteryId);
        var normalizedBatteryModel = NormalizeIdentityPart(batteryModel);
        if (string.IsNullOrWhiteSpace(normalizedBatteryId) || string.IsNullOrWhiteSpace(normalizedBatteryModel))
        {
            throw new ArgumentException("Battery ID and Battery Model are required.");
        }

        return CreateOpaqueId($"passport|{snapshotCreatedAt.UtcDateTime:O}|{normalizedBatteryId}|{normalizedBatteryModel}");
    }

    public static string NormalizeIdentityPart(string value)
    {
        return string.Join(
            " ",
            (value ?? string.Empty).Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private string CreateOpaqueId(string payload)
    {
        using var hmac = new HMACSHA512(_secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var encoded = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        while (encoded.Length < IdLength)
        {
            var extra = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{payload}|{encoded.Length}"));
            encoded += Convert.ToBase64String(extra)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        return encoded[..IdLength];
    }
}
