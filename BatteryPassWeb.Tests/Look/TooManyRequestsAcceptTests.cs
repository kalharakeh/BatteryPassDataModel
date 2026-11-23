using System.Net;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// R3 (decision 6): a request without an Accept header, or with "*/*", now gets a passport's data. Over a limit the JSON
/// error stays with the passport addresses; the search and other pages keep their sentence whatever the Accept header.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class TooManyRequestsAcceptTests(MongoFixture mongo)
{
    [Theory]
    [InlineData(null)]
    [InlineData("*/*")]
    public async Task APersonSearchingWithoutNamingThePage_ShouldStillGetASentence(string? accept)
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext(), readLimit: 1);

        await site.GetAsync("/search?q=NOPE-0000", accept: accept);
        var (status, body, _) = await site.GetAsync("/search?q=NOPE-0000", accept: accept);

        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.Equal(BatteryPassWeb.Configuration.TooManyRequests.SearchWords, body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("*/*")]
    [InlineData("application/ld+json")]
    public async Task APassportsAddressWithoutNamingThePage_ShouldGetTheJsonErrorOverTheLimit(string? accept)
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext(), readLimit: 1);

        await site.GetAsync("/dpp/NO-SUCH-ID", accept: accept);
        var (status, body, _) = await site.GetAsync("/dpp/NO-SUCH-ID", accept: accept);

        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.Equal("{\"error\":\"Too many requests.\"}", body);
    }
}
