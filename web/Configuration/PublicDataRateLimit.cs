using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The read limit of a passport's public data (findings R07; C3 round two, point 5). These routes take no token, so they
/// are counted by the caller's address only: an Authorization header, which they ignore, can't give a caller a fresh
/// allowance. The allowance per window is the external API's read limit, counted apart from it.
/// </summary>
public static class PublicDataRateLimit
{
    /// <summary>The caller's address as the limiter's partition; nothing from the request's headers.</summary>
    public static string PartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"public-data-ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    /// <summary>Adds the <see cref="SecurityRateLimitPolicyNames.PublicDataRead"/> policy.</summary>
    public static void Add(RateLimiterOptions options, int permitLimit, int windowSeconds)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(SecurityRateLimitPolicyNames.PublicDataRead, context =>
            RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }
}
