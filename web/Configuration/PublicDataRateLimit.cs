using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The read limit of a passport's public data (findings R07; C3 round two, point 5). Counted by the caller's address only:
/// an Authorization header can't give a caller a fresh allowance. The allowance per window is the external API's read
/// limit, counted apart from it. One exception (R3, decision 7): a read with a valid EU registry token for this site's
/// organisation doesn't count, so a registration of many batteries isn't refused halfway. Any other token still counts.
/// </summary>
public static class PublicDataRateLimit
{
    /// <summary>The caller's address as the limiter's partition; nothing from the request's headers.</summary>
    public static string PartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"public-data-ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    /// <summary>
    /// Whether the read is the EU registry's with a valid token, as the token check found it before the limit
    /// (<see cref="BatteryPassWeb.Dpp.RegistryToken.RegistryTokenAuthentication.UseRegistryToken"/>). A 401 or 403 token counts.
    /// </summary>
    public static bool Exempt(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return BatteryPassWeb.Dpp.RegistryToken.RegistryTokenRequest.Outcome(context) is { Authorised: true };
    }

    /// <summary>Adds the <see cref="SecurityRateLimitPolicyNames.PublicDataRead"/> policy.</summary>
    public static void Add(RateLimiterOptions options, int permitLimit, int windowSeconds)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(SecurityRateLimitPolicyNames.PublicDataRead, context =>
            Exempt(context)
                ? RateLimitPartition.GetNoLimiter("eu-registry")
                : RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
    }
}
