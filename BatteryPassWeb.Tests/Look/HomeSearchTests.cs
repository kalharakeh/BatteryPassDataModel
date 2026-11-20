using System.Net;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Finding U02: the home search ignores case and offers near matches among the published passports only. Review 1,
/// point 1 (the manager's decision): near matches come only from a search, never from a query flag; a visitor's match the
/// serial number from its start, from three characters, at most three; signed-in readers keep the Registry's matching;
/// the search, and the home page with a query, are rate limited as the public reads are.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed partial class HomeSearchTests(MongoFixture mongo)
{
    private static async Task<ShellSite> SiteAsync(MongoFixture mongo, int? readLimit = null)
    {
        var site = await ShellSite.StartAsync(mongo.NewContext(), readLimit: readLimit);
        await site.Services.GetRequiredService<DemoDataService>().ResetAsync(DemoRig.Who);
        return site;
    }

    [GeneratedRegex("class=\"bp-near-match\" href=\"[^\"]*\">([^<]+)</a>")]
    private static partial Regex Offered();

    private static string[] OfferedSerials(string page) => Offered().Matches(page).Select(match => match.Groups[1].Value).ToArray();

    [Theory]
    [InlineData("demo-c7m-0001")]
    [InlineData("Demo-C7M-0001")]
    [InlineData("  demo-c7m-0001 ")]
    public async Task ASerialNumberInOtherCapitals_ShouldOpenItsPassport(string typed)
    {
        await using var site = await SiteAsync(mongo);
        var (_, _, exact) = await site.GetAsync("/search?q=DEMO-C7M-0001");

        var (status, _, location) = await site.GetAsync($"/search?q={Uri.EscapeDataString(typed)}");

        Assert.Equal(HttpStatusCode.Redirect, status);
        Assert.StartsWith("/dpp/", exact, StringComparison.Ordinal);
        Assert.Equal(exact, location);
    }

    [Fact]
    public async Task PartOfANumber_ShouldOfferTheNearMatches_ToASignedInReader()
    {
        await using var site = await SiteAsync(mongo);

        var (_, page, _) = await site.FollowAsync("/search?q=c7m", "demo.msa@example.test");

        Assert.Contains("data-near-matches", page, StringComparison.Ordinal);
        Assert.Contains("DEMO-C7M-0001", OfferedSerials(page));
        Assert.DoesNotContain("DEMO-C7M-0002", OfferedSerials(page)); // a draft is never offered
    }

    [Fact]
    public async Task AVisitor_ShouldBeOfferedSerialNumbersThatStartWithTheText_AtMostThree()
    {
        await using var site = await SiteAsync(mongo);

        var (_, start, _) = await site.FollowAsync("/search?q=demo-c");
        var (_, middle, _) = await site.FollowAsync("/search?q=c7m");

        Assert.InRange(OfferedSerials(start).Length, 1, 3);
        Assert.All(OfferedSerials(start), serial => Assert.StartsWith("DEMO-C", serial, StringComparison.Ordinal));
        Assert.DoesNotContain("Type more", start, StringComparison.Ordinal);
        Assert.Empty(OfferedSerials(middle)); // not from the start of the serial number
    }

    [Theory]
    [InlineData("dpp")]
    [InlineData("htt")]
    [InlineData("/v1")]
    [InlineData("sib-scania")]
    [InlineData("UN778474481")]
    [InlineData("25S")]
    public async Task AVisitor_ShouldNotFindTheFleetThroughTheAddresses(string typed)
    {
        await using var site = await SiteAsync(mongo);

        var (_, page, _) = await site.FollowAsync($"/search?q={Uri.EscapeDataString(typed)}");

        Assert.Empty(OfferedSerials(page));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("demo.msa@example.test")]
    public async Task TheQueryFlagAlone_ShouldOfferNothing(string? email)
    {
        await using var site = await SiteAsync(mongo);

        var (_, page, _) = await site.GetAsync("/?q=DEMO&notFound=1", email);

        Assert.DoesNotContain("data-near-matches", page, StringComparison.Ordinal);
        Assert.Contains("No published battery passport was found for", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoCharacters_ShouldOfferNothing()
    {
        await using var site = await SiteAsync(mongo);

        var (_, page, _) = await site.FollowAsync("/search?q=DE");

        Assert.DoesNotContain("data-near-matches", page, StringComparison.Ordinal);
        Assert.Contains("No published battery passport was found for", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSearch_AndTheHomePageWithAQuery_ShouldBeRateLimited()
    {
        await using var site = await SiteAsync(mongo, readLimit: 3);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await site.GetAsync("/search?q=NOPE-0000")).Status);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await site.GetAsync("/search?q=NOPE-0000")).Status);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await site.GetAsync("/?q=NOPE-0000&notFound=1")).Status);
        Assert.Equal(HttpStatusCode.OK, (await site.GetAsync("/")).Status); // the home page itself stays open
    }

    [Fact]
    public void TheSearch_ShouldUseTheRegistrysMatching_ForSignedInReaders()
    {
        var home = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "HomeController.cs"));

        Assert.Contains("batteries.ListOnePerBatteryAsync(", home, StringComparison.Ordinal);
        Assert.DoesNotContain(".Contains(query", home, StringComparison.Ordinal);
    }
}
