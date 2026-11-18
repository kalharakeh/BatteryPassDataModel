using System.Net;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Finding U02: the home search ignores case and offers near matches, with the Registry's own matching (the same code),
/// among the published passports only; a visitor gets at most a few, and only for three characters or more.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class HomeSearchTests(MongoFixture mongo)
{
    private static async Task<ShellSite> SiteAsync(MongoFixture mongo)
    {
        var site = await ShellSite.StartAsync(mongo.NewContext());
        await site.Services.GetRequiredService<DemoDataService>().ResetAsync(DemoRig.Who);
        return site;
    }

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
    public async Task PartOfANumber_ShouldOfferTheNearMatches()
    {
        await using var site = await SiteAsync(mongo);

        var (status, _, location) = await site.GetAsync("/search?q=c7m");
        Assert.Equal(HttpStatusCode.Redirect, status);
        var (_, page, _) = await site.GetAsync(location!);

        Assert.Contains("data-near-matches", page, StringComparison.Ordinal);
        Assert.Contains(">DEMO-C7M-0001</a>", page, StringComparison.Ordinal);
        Assert.Contains("href=\"/dpp/", page, StringComparison.Ordinal);
        Assert.DoesNotContain(">DEMO-C7M-0002</a>", page, StringComparison.Ordinal); // a draft is never offered
    }

    [Fact]
    public async Task AVisitor_ShouldGetAtMostThreeNearMatches()
    {
        await using var site = await SiteAsync(mongo);

        var active = (await site.Services.GetRequiredService<BatteryPassWeb.Dpp.Pages.BatteryPages>().TeamListAsync(null)).Rows.Count(row => row.Active is not null);
        var (_, page, _) = await site.GetAsync("/?q=DEMO&notFound=1");

        var offered = page.Split("class=\"bp-near-match\"").Length - 1;
        Assert.Equal(Math.Min(3, active), offered);
        Assert.Equal(active > 3, page.Contains("Type more of the number to see the rest.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TwoCharacters_ShouldOfferNothing()
    {
        await using var site = await SiteAsync(mongo);

        var (_, page, _) = await site.GetAsync("/?q=DE&notFound=1");

        Assert.DoesNotContain("data-near-matches", page, StringComparison.Ordinal);
        Assert.Contains("No published battery passport was found for", page, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSearch_ShouldUseTheRegistrysMatching()
    {
        var home = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "HomeController.cs"));

        Assert.Contains("batteries.ListOnePerBatteryAsync(", home, StringComparison.Ordinal);
        Assert.DoesNotContain(".Contains(query", home, StringComparison.Ordinal);
    }
}
