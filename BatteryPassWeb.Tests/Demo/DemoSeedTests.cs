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
    public async Task TheCompact13M_ShouldShowItsOwnPictureAndThe7MItsFamilys()
    {
        // Owner's point 8: the 13M is the double stack, so it gets back the old demo's compact13.png (redesign R2).
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);
        var models = (await rig.All(DppStore.Models)).ToDictionary(model => model["Name"].AsString);

        Assert.Equal("image/png", models["Compact 13M"]["PictureContentType"].AsString);
        Assert.True(models["Compact 7M"].GetValue("PictureFileId", BsonNull.Value).IsBsonNull);
        Assert.True(models["Core"].GetValue("PictureFileId", BsonNull.Value).IsBsonNull);
        Assert.Equal(700941, DemoCatalogue.Picture("compact13.png").Length);
    }

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
        Assert.Equal(10, await rig.Count(DppStore.Packs));
        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "10 batteries, 7 of them published", "12 accounts and 7 API tokens"], result.Done);
    }

    [Fact]
    public async Task ThePublishedBatteries_ShouldHaveSignedVersionsAndTheSecondOneAVersionTwo()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var versions = await rig.All(DppStore.Versions);
        Assert.Equal(12, versions.Count); // C7M-0001: 3 (a new version, then its repair); C13M-0001: 2 (repurposed), its new passport 2 (remanufactured) and its third 1 (R6); CORE-0003: 2 (waste); CORE-0001 and C7M-0003: 1
        Assert.Equal(3, versions.Count(version => version["DppStatus"].AsString == "active")); // C13M-0001's old passport is inactive, C7M-0003's invalid, CORE-0003's marked for deletion
        Assert.Equal(1, versions.Count(version => version["DppStatus"].AsString == "invalid"));
        Assert.Equal(1, versions.Count(version => version["DppStatus"].AsString == "marked-for-deletion"));
        Assert.Equal(2, versions.Count(version => version["DppStatus"].AsString == "inactive")); // C13M-0001's first two passports (R6: a chain of three)
        Assert.All(versions, version => Assert.True(version.Contains("Proof")));
        var firstPack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single()["_id"].AsString;
        Assert.Equal([1, 2, 3], versions.Where(version => version["PackId"] == firstPack).Select(version => version["Number"].ToInt32()).Order());
        Assert.All(await rig.All(DppStore.Packs), pack => Assert.StartsWith("urn:x-battery-pass:TEST-", pack["Values"]["uniqueProductIdentifier"]["Value"].AsString, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheStatusExamples_ShouldShowEveryStatusAndMark()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var repaired = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single();
        var waste = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-CORE-0003"))).Single();
        Assert.Equal("re-used", repaired["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
        Assert.Equal("Module 4 replaced after a coolant leak.", repaired["Repairs"].AsBsonArray.Single()["Description"].AsString);
        Assert.Equal(6, repaired["Values"]["capacityFade"]["Value"]["percentageValue"].ToInt32());
        Assert.Equal("waste", waste["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
        Assert.Empty(waste["Repairs"].AsBsonArray);
        Assert.Equal(4, await rig.Count("applicationAuditEvents", Eq("eventType", BatteryPassWeb.Dpp.Editing.StatusChangeService.ChangedEvent))); // R6: C13M-0001's second passport is remanufactured too
        var repurposed = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C13M-0001"))).ToList();
        Assert.Equal(3, repurposed.Count); // the old battery and its two newer passports, same serial number (R6: a chain of three)
        var first = repurposed.Single(pack => !pack.Contains("SuccessorOf") || pack["SuccessorOf"].IsBsonNull);
        var newOne = repurposed.Single(pack => pack.Contains("SuccessorOf") && pack["SuccessorOf"] == first["_id"]);
        var third = repurposed.Single(pack => pack.Contains("SuccessorOf") && pack["SuccessorOf"] == newOne["_id"]);
        Assert.Equal("P5Y", third["Values"]["warrantyPeriod"]["Value"].AsString);
        Assert.Equal("repurposed", first["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
        Assert.Equal("remanufactured", newOne["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString); // R6: placed on the market again in turn
        Assert.Equal("P3Y", newOne["Values"]["warrantyPeriod"]["Value"].AsString);
        Assert.True(newOne["IsDemo"].AsBoolean);
        var invalid = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0003"))).Single();
        Assert.Equal("invalid", invalid["Mark"]["Status"].AsString);
        Assert.Equal("Demo: wrong carbon footprint figures", invalid["Mark"]["Reason"].AsString);
        Assert.Equal("demo.msa@example.test", invalid["Mark"]["By"].AsString);
        Assert.Equal("MarketSurveillance", invalid["Mark"]["ByRole"].AsString);
        Assert.Equal("marked-for-deletion", waste["Mark"]["Status"].AsString);
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
        Assert.Equal([0, 0, 0, 7, 7, 10, 10, 13, 13, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
        Assert.Equal(2, await rig.Count(DocumentStore.Records, Eq("Property", "informationOnAccidents")));
        Assert.Equal(12, await rig.Count(DocumentStore.Records, Eq("OwnerKind", "Model")));
        Assert.Equal(3, await rig.Count(DocumentStore.Records, Eq("Property", "resultsOfTestReportsProvingCompliance")));
        Assert.Equal(12 + 3 + 2 + 2 + 1, await rig.Count($"{DocumentStore.Bucket}.files")); // + the Compact 13M picture (redesign R2)
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
    public async Task TheHandOverExample_ShouldWaitForDemoNorthsAnswer()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var pack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-CORE-0001"))).Single();
        var waiting = (await rig.All(DppStore.HandOvers)).Single();
        Assert.Equal(pack["_id"].AsString, waiting["PackId"].AsString);
        Assert.Equal(
            ("demo-fleet", "demo-north", "waiting", DemoDataService.SeedActor),
            (waiting["FromClusterId"].AsString, waiting["ToClusterId"].AsString, waiting["State"].AsString, waiting["AskedBy"].AsString));
        Assert.Equal("demo-fleet", pack["ClusterId"].AsString); // it stays with the sender until demo-north accepts
    }

    [Fact]
    public async Task TheUploadExample_ShouldShowAnExportAndAFileOnTheCompact7M()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var pack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single();
        var uploads = (await rig.All(DppStore.Uploads)).OrderBy(upload => upload["AtUtc"].ToUniversalTime()).ToList();
        Assert.Equal(
            [("api", "BatterySystemFeed", "BMS2_ACO 01.32.07", "saved"), ("page", "ClusterAdmin", "dpp-v3-upload/1", "saved")],
            uploads.Select(upload => (upload["Channel"].AsString, upload["SentAs"].AsString, upload["Format"].AsString, upload["Batteries"][0]["Outcome"].AsString)));
        Assert.All(uploads, upload => Assert.Equal(pack["_id"].AsString, upload["Batteries"].AsBsonArray.Single()["PackId"].AsString));
        Assert.Equal(71, pack["Values"]["stateOfChargeSoC"]["Value"]["percentageValue"].ToInt32()); // the file came last
        Assert.Equal(7, pack["Modules"].AsBsonArray.Count); // the export names the seed's modules: none added
    }

    [Fact]
    public async Task EveryRecordItMakes_ShouldBeMarkedAsDemoData()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Uploads })
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
        Assert.Equal(10, await rig.Count(DppStore.Packs));
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

        // A real cluster with the demo's ID stops the seeding before anything changes (finding R03, point 7).
        Assert.Equal(["Nothing was changed: the demo's cluster demo-north is there as a real cluster, so the demo can't use it. Remove that cluster, then reset the demo data again."], result.Notes);
        Assert.Equal(0, await rig.Count(DppStore.Packs));
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
        Assert.Equal(10, await rig.Count(DppStore.Packs));
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
        Assert.Equal((10, 7, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
    }
}
