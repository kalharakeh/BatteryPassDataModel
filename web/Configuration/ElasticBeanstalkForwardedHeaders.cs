using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using NetIPNetwork = System.Net.IPNetwork;

namespace BatteryPassWeb.Configuration;

public static class ElasticBeanstalkForwardedHeaders
{
    /// <summary>
    /// Elastic Beanstalk places two proxies in front of the application: the load
    /// balancer, and then nginx on the instance. Each one appends to X-Forwarded-For,
    /// so both hops have to be unwound before <c>RemoteIpAddress</c> is the real
    /// client rather than an internal address.
    ///
    /// Unwinding only one hop leaves every request looking like it came from the load
    /// balancer, which silently collapses per-IP rate limiting into a single shared
    /// bucket for all users.
    ///
    /// This is safe against spoofing because the middleware stops unwinding at the
    /// first address outside <see cref="ForwardedHeadersOptions.KnownIPNetworks"/> and
    /// <see cref="ForwardedHeadersOptions.KnownProxies"/>. A client that sends its own
    /// X-Forwarded-For gets that value pushed further down the chain by the real
    /// proxies, beyond the hop limit, so it is never read.
    /// </summary>
    public const int DefaultForwardLimit = 2;

    private static readonly (string Network, int PrefixLength)[] DefaultTrustedPrivateNetworks =
    [
        ("10.0.0.0", 8),
        ("172.16.0.0", 12),
        ("192.168.0.0", 16)
    ];

    public static void Configure(ForwardedHeadersOptions options)
    {
        Configure(
            options,
            Environment.GetEnvironmentVariable("TRUSTED_PROXY_NETWORKS"),
            Environment.GetEnvironmentVariable("TRUSTED_PROXY_HOP_LIMIT"));
    }

    public static void Configure(ForwardedHeadersOptions options, string? trustedProxyNetworks)
    {
        Configure(options, trustedProxyNetworks, trustedProxyHopLimit: null);
    }

    public static void Configure(
        ForwardedHeadersOptions options,
        string? trustedProxyNetworks,
        string? trustedProxyHopLimit)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = ParseForwardLimit(trustedProxyHopLimit);

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);

        var configuredNetworks = ParseTrustedProxyNetworks(trustedProxyNetworks).ToList();
        var networks = configuredNetworks.Count > 0
            ? configuredNetworks
            : DefaultTrustedPrivateNetworks.Select(network => NetIPNetwork.Parse($"{network.Network}/{network.PrefixLength}"));

        foreach (var network in networks)
        {
            options.KnownIPNetworks.Add(network);
        }
    }

    /// <summary>
    /// nginx on Elastic Beanstalk rewrites <c>X-Forwarded-Proto</c> to its own scheme,
    /// which is always <c>http</c> because TLS terminates at the load balancer. The
    /// load balancer's <c>X-Forwarded-Port</c> passes through untouched, so it is the
    /// only surviving evidence of how the client actually connected.
    ///
    /// Repairing the header rather than setting the scheme directly is deliberate: the
    /// corrected value then goes through <c>UseForwardedHeaders</c> and is subject to
    /// the same <see cref="ForwardedHeadersOptions.KnownProxies"/> checks as everything
    /// else, so this cannot be used to fake HTTPS from an untrusted source.
    /// </summary>
    public static bool ShouldRestoreHttpsScheme(string? forwardedPort, string? forwardedProto)
    {
        // Only step in when the scheme has been flattened to http. If a future platform
        // version stops rewriting the header, this quietly does nothing.
        if (!string.Equals(LastValue(forwardedProto), "http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(
            LastValue(forwardedPort),
            HttpsRedirectionDefaults.PublicHttpsPort.ToString(),
            StringComparison.Ordinal);
    }

    private static string? LastValue(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        // Proxies append rather than replace, so the value added by the hop closest to
        // the application is the last one.
        var parts = headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts[^1];
    }

    public static int ParseForwardLimit(string? trustedProxyHopLimit)
    {
        if (string.IsNullOrWhiteSpace(trustedProxyHopLimit))
        {
            return DefaultForwardLimit;
        }

        if (!int.TryParse(trustedProxyHopLimit.Trim(), out var parsed) || parsed < 1)
        {
            throw new FormatException(
                $"TRUSTED_PROXY_HOP_LIMIT must be a positive integer. Received: '{trustedProxyHopLimit}'.");
        }

        return parsed;
    }

    private static IEnumerable<NetIPNetwork> ParseTrustedProxyNetworks(string? trustedProxyNetworks)
    {
        if (string.IsNullOrWhiteSpace(trustedProxyNetworks))
        {
            yield break;
        }

        var entries = trustedProxyNetworks.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            if (!NetIPNetwork.TryParse(entry, out var network))
            {
                throw new FormatException($"TRUSTED_PROXY_NETWORKS contains an invalid CIDR entry: '{entry}'.");
            }

            yield return network;
        }
    }
}
