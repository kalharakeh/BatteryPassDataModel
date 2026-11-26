using System.Net;
using BatteryPassWeb.Configuration;
using Microsoft.AspNetCore.Http;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// C3 round two, point 5: the four public data routes are counted by the caller's address only, so a different
/// Authorization header on each request doesn't give a fresh allowance.
/// </summary>
public sealed class PublicDataRateLimitTests
{
    [Fact]
    public void ThePartition_ShouldBeTheCallersAddressOnly()
    {
        static HttpContext Request(string address, string? authorization)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse(address);
            if (authorization is not null)
            {
                context.Request.Headers.Authorization = authorization;
            }

            return context;
        }

        var plain = PublicDataRateLimit.PartitionKey(Request("203.0.113.7", null));

        Assert.Equal(plain, PublicDataRateLimit.PartitionKey(Request("203.0.113.7", "Basic " + Guid.NewGuid())));
        Assert.Equal(plain, PublicDataRateLimit.PartitionKey(Request("203.0.113.7", "Bearer " + Guid.NewGuid())));
        Assert.NotEqual(plain, PublicDataRateLimit.PartitionKey(Request("203.0.113.8", null)));
    }

    [Fact]
    public void TheAppsStart_ShouldAddThePolicy_WithTheReadLimit()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("PublicDataRateLimit.Add(options, externalApiReadRateLimitPerWindow, rateLimitWindowSeconds);", program, StringComparison.Ordinal);
    }
}
