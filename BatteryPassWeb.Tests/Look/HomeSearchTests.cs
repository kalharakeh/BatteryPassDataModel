using System.Net;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

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

    /// <summary>
    /// Review 2, point 1: signed in, the Registry's matching and "Type more" only for the batteries the reader may list in
    /// the Registry; every other published battery only by the visitor's rule (serial number from its start).
    /// </summary>
    [Fact]
    public async Task ACustomersUser_ShouldFindOtherCustomersBatteriesOnlyByTheVisitorsRule()
    {
        await using var site = await SiteAsync(mongo);

        var (_, byAddress, _) = await site.FollowAsync("/search?q=dpp", "demo.north.user@example.test");
        var (_, byPrefix, _) = await site.FollowAsync("/search?q=demo-c13", "demo.north.user@example.test");
        var (_, inside, _) = await site.FollowAsync("/search?q=c13m", "demo.north.user@example.test");

        Assert.Equal(["DEMO-C7M-0001"], OfferedSerials(byAddress)); // their own customer's only
        Assert.DoesNotContain("Type more", byAddress, StringComparison.Ordinal);
        Assert.Equal(["DEMO-C13M-0001"], OfferedSerials(byPrefix)); // a south battery, as a visitor would get it
        Assert.Empty(OfferedSerials(inside)); // not from the start of the serial number
    }

    [Fact]
    public async Task ADemoAccount_ShouldNotFindARealBatteryByItsAddress()
    {
        await using var site = await SiteAsync(mongo);
        var real = await MakeOneBatteryRealAsync(site, "DEMO-C13M-0001");

        var (_, demo, _) = await site.FollowAsync("/search?q=dpp", "demo.msa@example.test");
        await site.Services.GetRequiredService<BatteryPassWeb.Services.ClusterRepository>()
            .UpsertUserAsync("real.msa@example.test", "Rita Real", [BatteryPassWeb.Services.AccessControlService.RoleMarketSurveillanceAuthority], BCrypt.Net.BCrypt.HashPassword("real-msa-password-1", 4));
        var (_, realMsa, _) = await site.FollowAsync("/search?q=dpp", "real.msa@example.test");
        await ToDoPageViewTests.AddTeamAsync(site);
        var (_, team, _) = await site.FollowAsync("/search?q=dpp", ToDoPageViewTests.Team);

        Assert.DoesNotContain(real, OfferedSerials(demo)); // R03: outside the demo a demo reader is a visitor
        Assert.NotEmpty(OfferedSerials(demo));
        Assert.Equal(3, OfferedSerials(realMsa).Length); // market surveillance lists every published battery (three are active)
        Assert.Contains(real, OfferedSerials(realMsa));
        Assert.Equal(3, OfferedSerials(team).Length);
    }

    /// <summary>Marks one demo battery as real (outside the demo), as a real battery on the same site would be.</summary>
    private static async Task<string> MakeOneBatteryRealAsync(ShellSite site, string serial)
    {
        var row = (await site.Services.GetRequiredService<BatteryPassWeb.Dpp.Pages.BatteryPages>().TeamListAsync(null)).Rows.Single(each => each.SerialNumber == serial);
        await site.Services.GetRequiredService<BatteryPassWeb.Services.MongoContext>().Database!
            .GetCollection<MongoDB.Bson.BsonDocument>(BatteryPassWeb.Dpp.Store.DppStore.Packs)
            .UpdateOneAsync(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", row.PackId), Builders<MongoDB.Bson.BsonDocument>.Update.Set("IsDemo", false));
        return serial;
    }

    /// <summary>Review 2, point 7 (decision 80): capitals, small letters and dashes don't tell serial numbers apart, in the prefix too.</summary>
    [Theory]
    [InlineData("democ7m", "DEMO-C7M-0001")]
    [InlineData("demo-c-7-m", "DEMO-C7M-0001")]
    [InlineData("---a", null)]
    [InlineData("d-e", null)]
    public async Task AVisitorsPrefix_ShouldCompareSerialNumbersByTheirLettersAndDigits(string typed, string? offered)
    {
        await using var site = await SiteAsync(mongo);

        var (_, page, _) = await site.FollowAsync($"/search?q={Uri.EscapeDataString(typed)}");

        if (offered is null)
        {
            Assert.Empty(OfferedSerials(page)); // fewer than three letters and digits
        }
        else
        {
            Assert.Contains(offered, OfferedSerials(page));
        }
    }

    /// <summary>Review 2, point 5: a failed search reads the passports once, not once for the exact steps and again for the near matches.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("demo.msa@example.test")]
    public async Task AFailedSearch_ShouldReadTheFleetOnce(string? email)
    {
        var context = mongo.NewContext();
        await using var site = await ShellSite.StartAsync(context);
        await site.Services.GetRequiredService<DemoDataService>().ResetAsync(DemoRig.Who);
        var database = context.Database!;
        await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("profile", 2));

        var (status, _, _) = await site.GetAsync("/search?q=NOPE-0000", email);

        await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("profile", 0));
        var profile = await database.GetCollection<MongoDB.Bson.BsonDocument>("system.profile").Find(MongoDB.Driver.FilterDefinition<MongoDB.Bson.BsonDocument>.Empty).ToListAsync();
        var fullReads = profile.Count(entry =>
            entry.GetValue("ns", string.Empty).AsString.EndsWith("." + BatteryPassWeb.Dpp.Store.DppStore.Versions, StringComparison.Ordinal)
            && entry.GetValue("command", new MongoDB.Bson.BsonDocument()).AsBsonDocument is var command
            && command.Contains("find")
            && (!command.Contains("filter") || command["filter"].AsBsonDocument.ElementCount == 0));
        Assert.Equal(HttpStatusCode.Redirect, status);
        Assert.Equal(1, fullReads);
    }

    [Fact]
    public void TheSearch_ShouldUseTheRegistrysMatching_ForSignedInReaders()
    {
        var home = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "HomeController.cs"));

        Assert.Contains("batteries.ListOnePerBatteryAsync(", home, StringComparison.Ordinal);
        Assert.DoesNotContain(".Contains(query", home, StringComparison.Ordinal);
    }
}
