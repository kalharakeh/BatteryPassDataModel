using System.Text;
using Microsoft.AspNetCore.Http;

namespace BatteryPassWeb.Services;

/// <summary>
/// The token an external API request carries: HTTP Basic with the token as user name ("token:") or, for older clients,
/// the raw token after "Basic ".
/// </summary>
public static class ExternalApiCredentials
{
    public static string Token(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = request.Headers.Authorization.FirstOrDefault();
        const string prefix = "Basic ";
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var raw = authorization[prefix.Length..].Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        return TryDecodeBasic(raw, out var token) ? token : raw;
    }

    private static bool TryDecodeBasic(string raw, out string token)
    {
        token = string.Empty;
        try
        {
            var bytes = Convert.FromBase64String(raw);
            if (bytes.Length == 0 || bytes.Length > 512)
            {
                return false;
            }

            var decoded = Encoding.UTF8.GetString(bytes);
            if (decoded.Length == 0 || decoded.Any(character => character is < ' ' or > '~'))
            {
                return false;
            }

            var separator = decoded.IndexOf(':');
            var candidate = separator <= 0 ? string.Empty : decoded[..separator].Trim();
            if (candidate.Length == 0)
            {
                return false;
            }

            token = candidate;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
