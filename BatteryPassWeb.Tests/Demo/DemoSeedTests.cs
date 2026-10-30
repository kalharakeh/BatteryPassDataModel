using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3d-3: the demo data shows the app's features, made through the app's own paths (decision 32).</summary>
[Collection(MongoCollection.Name)]
public sealed class DemoSeedTests(MongoFixture mongo)
{
    private static FilterDefinition<BsonDocument> Eq(string field, BsonValue value) => Builders<BsonDocument>.Filter.Eq(field, value);

    [Fact]
    public async Task TheSeed_ShouldMakeTheFamiliesModelsAndBatteries()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Empty(result.Notes);
        Assert.Equal(["Compact", "Core"], (await rig.All(DppStore.Families)).Select(family => family["Name"].AsString).Order());
        Assert.All(await rig.All(DppStore.Families), family => Assert.Equal("image/png", family["PictureContentType"].AsString));
        Assert.Equal(["Compact 13M", "Compact 7M", "Core"], (await rig.All(DppStore.Models)).Select(model => model["Name"].AsString).Order());
        Assert.All(await rig.All(DppStore.Models), model => Assert.False(string.IsNullOrWhiteSpace(model["ModuleDescription"].AsString)));
        Assert.Equal(2, await rig.Count(DppStore.Sites));
        Assert.Equal(2, await rig.Count(DppStore.Operators));
        Assert.Equal(3, await rig.Count(DppStore.Batches));
        Assert.Equal(6, await rig.Count(DppStore.Packs));
        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "6 batteries, 3 of them published", "12 accounts and 7 API tokens"], result.Done);
    }

    [Fact]
    public async Task ThePublishedBatteries_ShouldHaveSignedVersionsAndTheSecondOneAVersionTwo()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var versions = await rig.All(DppStore.Versions);
        Assert.Equal(4, versions.Count);
        Assert.Equal(3, versions.Count(version => version["DppStatus"].AsString == "active"));
        Assert.All(versions, version => Assert.True(version.Contains("Proof")));
        var firstPack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single()["_id"].AsString;
        Assert.Equal([1, 2], versions.Where(version => version["PackId"] == firstPack).Select(version => version["Number"].ToInt32()).Order());
        Assert.All(await rig.All(DppStore.Packs), pack => Assert.StartsWith("urn:x-battery-pass:TEST-", pack["Values"]["uniqueProductIdentifier"]["Value"].AsString, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheDrafts_ShouldShowAReadyOneAndTwoWithMissingValues()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var drafts = (await rig.All(DppStore.Packs)).Where(pack => pack["Values"]["batterySerialNumber"]["Value"].AsString.EndsWith("0002", StringComparison.Ordinal)).ToList();

        Assert.Equal(3, drafts.Count);
        Assert.Equal(2, drafts.Count(pack => !pack["Values"].AsBsonDocument.Contains("manufacturingDate")));
        Assert.Equal(0, await rig.Count(DppStore.Versions, Builders<BsonDocument>.Filter.In("PackId", drafts.Select(pack => pack["_id"]))));
    }

    [Fact]
    public async Task TheBatteries_ShouldHaveModulesAccidentReportsAndDocuments()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var packs = await rig.All(DppStore.Packs);
        Assert.Equal([0, 0, 0, 7, 10, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
        Assert.Equal(2, await rig.Count(DocumentStore.Records, Eq("Property", "informationOnAccidents")));
        Assert.Equal(12, await rig.Count(DocumentStore.Records, Eq("OwnerKind", "Model")));
        Assert.Equal(3, await rig.Count(DocumentStore.Records, Eq("Property", "resultsOfTestReportsProvingCompliance")));
        Assert.Equal(12 + 3 + 2 + 2, await rig.Count($"{DocumentStore.Bucket}.files"));
    }

    [Fact]
    public async Task ThePublishedBatteries_ShouldHaveSevenDaysOfLiveValues()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var changes = await rig.All("applicationAuditEvents", Builders<BsonDocument>.Filter.And(Eq("eventType", "dpp.change"), Eq("metadata.keys.key", "stateOfChargeSoC")));
        var times = changes.Select(change => DateTime.Parse(change["metadata"]["keys"].AsBsonArray.First(key => key["key"] == "stateOfChargeSoC")["generatedAt"].AsString, null, System.Globalization.DateTimeStyles.RoundtripKind)).ToList();
        Assert.True(changes.Count >= 3 * 28, $"{changes.Count} live-value saves");
        Assert.InRange((DateTime.UtcNow - times.Min()).TotalHours, 160, 168);
    }

    [Fact]
    public async Task EveryRecordItMakes_ShouldBeMarkedAsDemoData()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs })
        {
            Assert.All(await rig.All(collection), record => Assert.True(record["IsDemo"].AsBoolean, $"{collection} {record["_id"]}"));
        }

        foreach (var collection in new[] { "users", "clusterMemberships", "clusters", "apiTokens" })
        {
            Assert.All(await rig.All(collection), record => Assert.True(record["isDemo"].AsBoolean, collection));
        }
    }

    [Fact]
    public async Task TheAccounts_ShouldCoverEveryRoleButTheTeamAdminAndUseTheSetting()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var users = await rig.All("users");
        Assert.Equal(12, users.Count);
        Assert.DoesNotContain(users, user => user["roles"].AsBsonArray.Contains("admin"));
        Assert.All(users, user => Assert.True(BCrypt.Net.BCrypt.Verify(DemoRig.Password, user["passwordHash"].AsString)));
        Assert.Equal(["clusterAdmin", "clusterAdmin", "clusterAdmin", "member", "member", "member"], (await rig.All("clusterMemberships")).Select(membership => membership["role"].AsString).Order());
    }

    [Fact]
    public async Task WithoutTheSetting_ShouldSeedEverythingButTheAccounts()
    {
        using var rig = await DemoRig.StartAsync(mongo, password: null);

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Equal([DemoDataService.NoPassword], result.Notes);
        Assert.Equal(0, await rig.Count("users"));
        Assert.Equal(6, await rig.Count(DppStore.Packs));
    }

    [Fact]
    public async Task TheTokens_ShouldBeShownOnceAndStoredOnlyAsHashes()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Equal(7, result.Tokens.Count);
        Assert.Equal(["Full access", "Read", "Read", "Read", "Read and write live values", "Read and write live values", "Read and write live values"], result.Tokens.Select(token => token.Access).Order());
        var stored = (await rig.All("apiTokens")).Select(token => token.ToJson()).ToList();
        var audit = (await rig.All("applicationAuditEvents")).Select(entry => entry.ToJson()).ToList();
        foreach (var token in result.Tokens)
        {
            Assert.DoesNotContain(stored, text => text.Contains(token.Value, StringComparison.Ordinal));
            Assert.DoesNotContain(audit, text => text.Contains(token.Value, StringComparison.Ordinal));
        }

        Assert.Single(await rig.All("apiTokens", Eq("globalAccess", true)));
    }

    [Fact]
    public async Task RealRecordsWithTheDemoNames_ShouldBeLeftAlone()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var clusters = rig.Services.GetRequiredService<ClusterRepository>();
        await clusters.UpsertClusterAsync("demo-north", "Real North");
        await clusters.UpsertUserAsync("demo.msa@example.test", "Real MSA", ["marketSurveillanceAuthority"], "real-hash");

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Contains("Cluster demo-north was already there as real data: the demo batteries use it, and it stays real.", result.Notes);
        Assert.Contains("Account demo.msa@example.test was already there as real data: it stays as it is.", result.Notes);
        var north = (await rig.All("clusters", Eq("clusterId", "demo-north"))).Single();
        Assert.Equal(("Real North", false), (north["name"].AsString, north.GetValue("isDemo", false).AsBoolean));
        Assert.Equal("real-hash", (await rig.All("users", Eq("email", "demo.msa@example.test"))).Single()["passwordHash"].AsString);
    }

    [Fact]
    public async Task SeedingAgain_ShouldAskToResetOrClearFirst()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var again = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Equal(["The demo data is already there: reset it or clear it first."], again.Notes);
        Assert.Equal(6, await rig.Count(DppStore.Packs));
    }

    [Fact]
    public async Task WithoutTheAppsAddress_ShouldMakeNothing()
    {
        using var rig = await DemoRig.StartAsync(mongo, appBaseUrl: null);

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Equal([DppLinks.NotConfigured], result.Notes);
        Assert.Equal(0, await rig.Count(DppStore.Families));
    }

    [Fact]
    public async Task TheSeed_ShouldBeLoggedOnceWithTheCountsButNoTokenValues()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.SeededEvent))).Single();
        Assert.Equal(DemoRig.Who, entry["actor"].AsString);
        Assert.Equal((6, 3, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
    }
}
