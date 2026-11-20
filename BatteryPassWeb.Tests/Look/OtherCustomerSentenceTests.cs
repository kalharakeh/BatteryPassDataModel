using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Finding W10, review 1 point 2: "This battery belongs to another customer" only when the battery has a customer and the
/// reader belongs to another one; anyone else signed in who sees only the public data gets a neutral sentence.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class OtherCustomerSentenceTests(MongoFixture mongo)
{
    private const string Another = "This battery belongs to another customer; you see its public data.";
    private const string Neutral = "You see the public data of this battery.";

    private static async Task<(ShellSite Site, string SouthSummary)> SiteAsync(MongoFixture mongo)
    {
        var site = await ShellSite.StartAsync(mongo.NewContext());
        await site.Services.GetRequiredService<DemoDataService>().ResetAsync(DemoRig.Who);
        var south = (await site.Services.GetRequiredService<BatteryPages>().TeamListAsync(null)).Rows.First(row => row.ClusterId == "demo-south" && row.Active is not null);
        return (site, PassportPages.SummaryHref(south.Identifier));
    }

    [Fact]
    public async Task AnotherCustomersPerson_ShouldBeToldItIsAnotherCustomers()
    {
        var (site, south) = await SiteAsync(mongo);
        await using var _ = site;

        var (_, page, _) = await site.GetAsync(south, "demo.north.user@example.test");

        Assert.Contains(Another, page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAccountWithNoCustomer_ShouldGetTheNeutralSentence()
    {
        var (site, south) = await SiteAsync(mongo);
        await using var _ = site;
        await site.Services.GetRequiredService<ClusterRepository>()
            .UpsertUserAsync("no.customer@example.test", "Nora None", [AccessControlService.RoleNormalUser], BCrypt.Net.BCrypt.HashPassword("no-customer-password-1", 4));

        var (_, page, _) = await site.GetAsync(south, "no.customer@example.test");

        Assert.DoesNotContain(Another, page, StringComparison.Ordinal);
        Assert.Contains(Neutral, page, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHero_ShouldChooseBySomethingTheReaderAndTheBatteryBothHave()
    {
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.Contains("else if (signedIn && Model.AnotherCustomers)", hero, StringComparison.Ordinal);
        Assert.Contains(Neutral, hero, StringComparison.Ordinal);
    }
}
