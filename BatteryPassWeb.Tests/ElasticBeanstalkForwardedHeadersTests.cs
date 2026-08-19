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
        Assert.Equal(2, options.ForwardLimit);
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
        Assert.Contains(
            "ElasticBeanstalkForwardedHeaders.Configure(options, trustedProxyNetworks, trustedProxyHopLimit)",
            program);
        Assert.Contains("TRUSTED_PROXY_NETWORKS=10.0.0.0/20,10.0.16.0/20", envExample);
        Assert.Contains("TRUSTED_PROXY_HOP_LIMIT=2", envExample);
    }

    [Fact]
    public void Configure_ShouldUnwindBothElasticBeanstalkProxyHopsByDefault()
    {
        var options = new ForwardedHeadersOptions();

        ElasticBeanstalkForwardedHeaders.Configure(options, "10.0.0.0/20,10.0.16.0/20");

        // The load balancer and nginx each append to X-Forwarded-For. Unwinding only
        // one hop leaves RemoteIpAddress pointing at the load balancer, which collapses
        // per-IP rate limiting into a single bucket shared by every client.
        Assert.Equal(2, options.ForwardLimit);
        Assert.Equal(2, ElasticBeanstalkForwardedHeaders.DefaultForwardLimit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseForwardLimit_ShouldFallBackToTheElasticBeanstalkTopology(string? configured)
    {
        Assert.Equal(2, ElasticBeanstalkForwardedHeaders.ParseForwardLimit(configured));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("3", 3)]
    [InlineData(" 2 ", 2)]
    public void ParseForwardLimit_ShouldHonourAnOverrideSoTopologyChangesNeedNoCodeChange(
        string configured,
        int expected)
    {
        Assert.Equal(expected, ElasticBeanstalkForwardedHeaders.ParseForwardLimit(configured));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("two")]
    public void ParseForwardLimit_ShouldRejectValuesThatWouldSilentlyDisableForwarding(string configured)
    {
        Assert.Throws<FormatException>(() => ElasticBeanstalkForwardedHeaders.ParseForwardLimit(configured));
    }

    [Fact]
    public void ShouldRestoreHttpsScheme_ShouldRepairTheSchemeNginxFlattened()
    {
        // Exactly what production sends: the load balancer's port survives, the proto
        // has been rewritten to http by nginx.
        Assert.True(ElasticBeanstalkForwardedHeaders.ShouldRestoreHttpsScheme("443", "http"));
    }

    [Fact]
    public void ShouldRestoreHttpsScheme_ShouldLeaveGenuinePlainHttpAlone()
    {
        // A real HTTP request arrives on port 80. Rewriting this would tell the app
        // every request was secure and disable the redirect entirely.
        Assert.False(ElasticBeanstalkForwardedHeaders.ShouldRestoreHttpsScheme("80", "http"));
    }

    [Theory]
    [InlineData("443", "https")]
    [InlineData("443", "HTTPS")]
    public void ShouldRestoreHttpsScheme_ShouldDoNothingWhenTheSchemeSurvived(
        string forwardedPort,
        string forwardedProto)
    {
        // If a future platform version stops rewriting the header, this must become a
        // no-op rather than fighting a correct value.
        Assert.False(ElasticBeanstalkForwardedHeaders.ShouldRestoreHttpsScheme(forwardedPort, forwardedProto));
    }

    [Theory]
    [InlineData(null, "http")]
    [InlineData("", "http")]
    [InlineData("443", null)]
    [InlineData("443", "")]
    [InlineData("not-a-port", "http")]
    public void ShouldRestoreHttpsScheme_ShouldRequireBothSignalsBeforeActing(
        string? forwardedPort,
        string? forwardedProto)
    {
        Assert.False(ElasticBeanstalkForwardedHeaders.ShouldRestoreHttpsScheme(forwardedPort, forwardedProto));
    }

    [Fact]
    public void ShouldRestoreHttpsScheme_ShouldReadTheHopClosestToTheApplication()
    {
        // Proxies append, so the last value is the one added by the nearest hop.
        Assert.True(ElasticBeanstalkForwardedHeaders.ShouldRestoreHttpsScheme("80, 443", "https, http"));
        Assert.False(ElasticBeanstalkForwardedHeaders.ShouldRestoreHttpsScheme("443, 80", "https, http"));
    }

    [Fact]
    public void Program_ShouldRestoreTheSchemeBeforeForwardedHeadersAreApplied()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        var restoreIndex = program.IndexOf("ShouldRestoreHttpsScheme", StringComparison.Ordinal);
        var forwardedIndex = program.IndexOf("app.UseForwardedHeaders()", StringComparison.Ordinal);

        Assert.True(restoreIndex >= 0, "The X-Forwarded-Proto repair is not wired into Program.cs.");

        // Repairing the header afterwards would be too late - the middleware has
        // already applied the wrong scheme and stripped the headers it consumed.
        Assert.True(
            restoreIndex < forwardedIndex,
            "The scheme must be repaired before UseForwardedHeaders, so the corrected value still goes through the trusted-proxy checks.");
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
