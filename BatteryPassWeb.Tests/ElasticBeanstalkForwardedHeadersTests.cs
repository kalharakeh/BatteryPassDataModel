using BatteryPassWeb.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace BatteryPassWeb.Tests;

public sealed class ElasticBeanstalkForwardedHeadersTests
{
    [Fact]
    public void Configure_ShouldTrustForwardedForAndProtoFromLoadBalancer()
    {
        var options = new ForwardedHeadersOptions();

        ElasticBeanstalkForwardedHeaders.Configure(options);

        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
        Assert.Equal(1, options.ForwardLimit);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Empty(options.KnownProxies);
    }
}
