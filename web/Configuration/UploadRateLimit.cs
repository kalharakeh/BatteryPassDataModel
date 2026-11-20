using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The limit on file uploads (FU4): a signed-in person has an allowance of their own, so people behind one office address
/// don't share one; a caller who isn't signed in is counted by address. It needs the person to be known when the limiter
/// picks the partition, so <c>UseRateLimiter</c> runs after <c>UseAuthentication</c> (the order in <c>Program.cs</c>).
/// </summary>
public static class UploadRateLimit
{
    /// <summary>The signed-in person's name, hashed; or the caller's address when nobody is signed in.</summary>
    public static string PartitionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.User.Identity?.IsAuthenticated == true && PersonOf(context.User) is { Length: > 0 } person)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(person));
            return $"upload-user:{WebEncoders.Base64UrlEncode(hash)}";
        }

        return $"upload-ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    /// <summary>
    /// The person's e-mail address, in small letters: the sign-in's name identifier. The name claim is the display name,
    /// which two people may share, so it is only the last resort.
    /// </summary>
    private static string PersonOf(ClaimsPrincipal user) =>
        (user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Adds the <see cref="SecurityRateLimitPolicyNames.FileUpload"/> policy.</summary>
    public static void Add(RateLimiterOptions options, int permitLimit, int windowSeconds)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(SecurityRateLimitPolicyNames.FileUpload, context =>
            RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }
}
