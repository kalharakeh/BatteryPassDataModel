using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using NetIPNetwork = System.Net.IPNetwork;

namespace BatteryPassWeb.Configuration;

public static class ElasticBeanstalkForwardedHeaders
{
    private static readonly (string Network, int PrefixLength)[] DefaultTrustedPrivateNetworks =
    [
        ("10.0.0.0", 8),
        ("172.16.0.0", 12),
        ("192.168.0.0", 16)
    ];

    public static void Configure(ForwardedHeadersOptions options)
    {
        Configure(options, Environment.GetEnvironmentVariable("TRUSTED_PROXY_NETWORKS"));
    }

    public static void Configure(ForwardedHeadersOptions options, string? trustedProxyNetworks)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

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
