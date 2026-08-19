using System.Text;

namespace BatteryPassWeb.Configuration;

/// <summary>
/// The path the Elastic Beanstalk load balancer polls to decide whether an
/// instance is in service.
///
/// It must stay exempt from the HTTPS redirect: the check arrives over plain HTTP
/// and expects a 200, so redirecting it would mark every target unhealthy.
/// Changing <see cref="Path"/> requires updating the environment's
/// <c>aws:elasticbeanstalk:environment:process:default</c> / <c>HealthCheckPath</c>
/// setting to match, or the load balancer will poll a path that no longer exists.
///
/// Because this path is never redirected, it doubles as the only reliable way to
/// see what the application makes of an incoming request while a redirect loop is
/// in progress. That reporting is off unless <see cref="DiagnosticsEnvironmentVariable"/>
/// is set, since it exposes internal addresses.
/// </summary>
public static class BatteryPassHealthCheck
{
    public const string Path = "/health";

    public const string ResponseBody = "ok";

    public const string DiagnosticsEnvironmentVariable = "HEALTH_DIAGNOSTICS";

    /// <summary>
    /// Key under which the raw proxy headers are stashed before
    /// <c>UseForwardedHeaders</c> consumes them. Without a snapshot taken first, the
    /// headers read as absent by the time anything downstream can report on them,
    /// which makes it impossible to tell "never sent" apart from "sent and applied".
    /// </summary>
    public const string RawForwardedHeadersKey = "BatteryPass.RawForwardedHeaders";

    public static bool DiagnosticsEnabled(string? configuredValue) =>
        string.Equals(configuredValue?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reports what the application concluded about the request against the raw
    /// headers it was given, so a mismatch between the two is immediately visible.
    /// </summary>
    public static string BuildDiagnostics(
        string scheme,
        bool isHttps,
        string? forwardedProto,
        string? forwardedFor,
        string? remoteIpAddress,
        IEnumerable<KeyValuePair<string, string>>? rawProxyHeaders = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine(ResponseBody);
        builder.AppendLine($"scheme={scheme}");
        builder.AppendLine($"isHttps={isHttps}");
        builder.AppendLine($"x-forwarded-proto={Describe(forwardedProto)}");
        builder.AppendLine($"x-forwarded-for={Describe(forwardedFor)}");
        builder.AppendLine($"remote-ip={Describe(remoteIpAddress)}");

        builder.AppendLine("--- as received, before forwarded headers were applied ---");
        var raw = rawProxyHeaders?.ToList();
        if (raw is null || raw.Count == 0)
        {
            builder.AppendLine("(no proxy headers reached the application)");
            return builder.ToString();
        }

        foreach (var header in raw.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"{header.Key.ToLowerInvariant()}={Describe(header.Value)}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// The headers a reverse proxy uses to describe the original request. Captured
    /// verbatim so an overwritten value is distinguishable from a missing one.
    /// </summary>
    public static bool IsProxyHeader(string headerName) =>
        headerName.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("X-Real-IP", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("Forwarded", StringComparison.OrdinalIgnoreCase);

    private static string Describe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(absent)" : value;
}
