using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.RegistryToken;
using Microsoft.AspNetCore.Http;
using static BatteryPassWeb.Tests.Dpp.RegistryTokenTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (decision 7, G10): a submission of 100 batteries makes about 200 reads, and one refused read fails the whole
/// submission. So a read with a valid EU registry token doesn't count against the public read limit. A read with any other
/// token still counts, so a junk token gets nobody round the limit; anonymous reads keep today's limit.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RegistryTokenReadLimitTests(MongoFixture mongo) : IDisposable
{
    private const int Limit = 3;
    private readonly ECDsa _key = NewKey();

    public void Dispose() => _key.Dispose();

    private string ValidToken(Action<JsonObject>? claims = null)
    {
        var body = Claims(DateTimeOffset.UtcNow.AddSeconds(-2));
        claims?.Invoke(body);
        return Token(_key, Header(), body);
    }

    [Fact]
    public async Task ValidRegistryReads_ShouldNotCount_AndAnonymousReadsKeepTheirLimit()
    {
        await using var site = await RegistryTokenHttpTests.StartAsync(mongo, _key, readLimit: Limit);

        var registry = new List<HttpStatusCode>();
        for (var i = 0; i < Limit * 5; i++)
        {
            using var answer = await site.GetAsync(i % 2 == 0 ? $"/dpp/{site.Id}" : $"/dpp/{site.Id}.json", "application/json", $"Bearer {ValidToken()}");
            registry.Add(answer.StatusCode);
        }

        var anonymous = new List<HttpStatusCode>();
        for (var i = 0; i <= Limit; i++)
        {
            using var answer = await site.GetAsync($"/dpp/{site.Id}.json", "application/json");
            anonymous.Add(answer.StatusCode);
        }

        Assert.All(registry, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], anonymous);
        using var stillRegistry = await site.GetAsync($"/dpp/{site.Id}", "application/json", $"Bearer {ValidToken()}");
        Assert.Equal(HttpStatusCode.OK, stillRegistry.StatusCode); // the registry still reads once the address's allowance is gone
    }

    [Theory]
    [InlineData("junk")]
    [InlineData("expired")]
    [InlineData("another organisation")]
    [InlineData("wrong scope")]
    [InlineData("basic")]
    public async Task ATokenThatIsntValidAndAuthorised_ShouldStillCount(string kind)
    {
        await using var site = await RegistryTokenHttpTests.StartAsync(mongo, _key, readLimit: Limit);
        string Authorization() => kind switch
        {
            "junk" => $"Bearer {Guid.NewGuid():N}",
            "expired" => $"Bearer {Token(_key, Header(), Claims(DateTimeOffset.UtcNow.AddMinutes(-5)))}",
            "another organisation" => $"Bearer {ValidToken(claims => claims["resource_requestor_identifiers"]![0]!["value"] = "ORG-SOMEONE-ELSE")}",
            "wrong scope" => $"Bearer {ValidToken(claims => claims["scope"] = "dpp:registry:write")}",
            _ => "Basic dXNlcjpwYXNz",
        };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i <= Limit; i++)
        {
            using var answer = await site.GetAsync($"/dpp/{site.Id}.json", "application/json", Authorization());
            statuses.Add(answer.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.All(statuses.Take(Limit), status => Assert.True(status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, $"{status}"));
        using var anonymous = await site.GetAsync($"/dpp/{site.Id}.json", "application/json");
        Assert.Equal(HttpStatusCode.TooManyRequests, anonymous.StatusCode); // the same address's allowance
    }

    [Fact]
    public void ThePartition_ShouldLetOnlyAnAuthorisedTokenThrough()
    {
        static DefaultHttpContext Request(RegistryTokenResult? outcome)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            context.Request.Headers.Authorization = "Bearer x";
            if (outcome is not null)
            {
                context.Features.Set(outcome);
            }

            return context;
        }

        Assert.True(PublicDataRateLimit.Exempt(Request(new RegistryTokenResult(RegistryTokenVerdict.Authorised, "authorised", Organisation))));
        Assert.False(PublicDataRateLimit.Exempt(Request(null)));
        Assert.False(PublicDataRateLimit.Exempt(Request(RegistryTokenResult.Invalid("signature"))));
        Assert.False(PublicDataRateLimit.Exempt(Request(new RegistryTokenResult(RegistryTokenVerdict.RequestorRefused, "requestor-other"))));
        Assert.False(PublicDataRateLimit.Exempt(Request(new RegistryTokenResult(RegistryTokenVerdict.InsufficientScope, "scope"))));
    }
}
