using BatteryPassWeb.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace BatteryPassWeb.Tests;

public sealed class ElasticBeanstalkForwardedHeadersTests
{
    [Fact]
    public void Configure_ShouldTrustForwardedForAndProtoOnlyFromKnownProxyNetworks()
    {
        var options = new ForwardedHeadersOptions();

        ElasticBeanstalkForwardedHeaders.Configure(options, "10.0.0.0/20,10.0.16.0/20");

        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
        Assert.Equal(1, options.ForwardLimit);
        Assert.Contains(options.KnownIPNetworks, network => network.ToString() == "10.0.0.0/20");
        Assert.Contains(options.KnownIPNetworks, network => network.ToString() == "10.0.16.0/20");
        Assert.Contains(IPAddress.Loopback, options.KnownProxies);
        Assert.Contains(IPAddress.IPv6Loopback, options.KnownProxies);
    }

    [Fact]
    public void Configure_ShouldDefaultToPrivateNetworksInsteadOfTrustingEveryProxy()
    {
        var options = new ForwardedHeadersOptions();

        ElasticBeanstalkForwardedHeaders.Configure(options, trustedProxyNetworks: null);

        Assert.NotEmpty(options.KnownIPNetworks);
        Assert.NotEmpty(options.KnownProxies);
        Assert.Contains(options.KnownIPNetworks, network => network.ToString() == "10.0.0.0/8");
        Assert.Contains(options.KnownIPNetworks, network => network.ToString() == "172.16.0.0/12");
        Assert.Contains(options.KnownIPNetworks, network => network.ToString() == "192.168.0.0/16");
    }

    [Fact]
    public void ProgramAndEnvExample_ShouldWireTrustedProxyNetworksConfiguration()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
        var envExample = File.ReadAllText(RepoFile("web", ".env.example"));

        Assert.Contains("TRUSTED_PROXY_NETWORKS", program);
        Assert.Contains("ElasticBeanstalkForwardedHeaders.Configure(options, trustedProxyNetworks)", program);
        Assert.Contains("TRUSTED_PROXY_NETWORKS=10.0.0.0/20,10.0.16.0/20", envExample);
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
