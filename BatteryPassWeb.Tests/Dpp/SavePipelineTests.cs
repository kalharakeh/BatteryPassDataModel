using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class SavePipelineTests(MongoFixture mongo)
{
    private const string PackId = "pack-VPC8C05-6788";
    private static readonly ChangeSender Team = ChangeSender.Team("team@example.com");

    [Fact]
    public async Task Team_ShouldSaveAValidValueWithWhenSourceAndWho()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange(("thirdPartyServiceProvider", "\"New Hosting AB\"")));

        Assert.True(result.Saved);
        Assert.Equal(KeyOutcome.Saved, Assert.Single(result.Keys).Outcome);
        var stored = (await store.GetAsync<BatteryPack>(PackId))!.Values["thirdPartyServiceProvider"];
        Assert.Equal("New Hosting AB", stored.Value.GetValue<string>());
        Assert.Equal(DppStoreTesting.Now.UtcDateTime, stored.GeneratedAtUtc);
        Assert.Equal("form", stored.Source);
        Assert.Equal("team@example.com", stored.Who);
    }

    [Fact]
    public async Task FeedValue_ShouldBeStoredInTheSchemaFormWithItsMeasurementTime()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var measured = new DateTime(2026, 10, 27, 10, 2, 59, DateTimeKind.Utc);
        var change = new ChangeSet(TargetKind.Pack, PackId, Values(("stateOfChargeSoC", "55")), "feed", measured);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), change);

        Assert.True(result.Saved);
        var stored = (await store.GetAsync<BatteryPack>(PackId))!.Values["stateOfChargeSoC"];
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"percentageValue":55,"percentUnit":"%"}"""), stored.Value));
        Assert.Equal(measured, stored.GeneratedAtUtc);
        Assert.Equal("feed", stored.Source);
    }

    [Fact]
    public async Task Preview_ShouldReportWithoutSaving()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange(("thirdPartyServiceProvider", "\"Preview AB\"")), preview: true);

        Assert.False(result.Saved);
        Assert.Equal(KeyOutcome.Saved, Assert.Single(result.Keys).Outcome);
        Assert.Equal("Example Passport Hosting AB", (await store.GetAsync<BatteryPack>(PackId))!.Values["thirdPartyServiceProvider"].Value.GetValue<string>());
    }

    [Theory]
    [InlineData("remainingCapacity", "{}", KeyOutcome.Ignored, "not in the attribute list")] // schema property left out of V3
    [InlineData("totalChargedEnergyKwh", "69", KeyOutcome.Ignored, "not in the attribute list")]
    [InlineData("ratedCapacity", "200", KeyOutcome.Rejected, "stored on the model")]
    [InlineData("batteryStatus", "\"waste\"", KeyOutcome.Rejected, "status change")]
    [InlineData("capacityFade", "5", KeyOutcome.Rejected, "status change")]
    [InlineData("uniqueProductIdentifier", "\"urn:other\"", KeyOutcome.Rejected, "set by the app")]
    [InlineData("dppSchemaVersion", "\"x\"", KeyOutcome.Rejected, "set by the app")]
    [InlineData("stateOfChargeSoC", "120", KeyOutcome.Rejected, "maximum")]
    [InlineData("manufacturingDate", "\"2026-10-14\"", KeyOutcome.Rejected, "decision 18")]
    public async Task Key_ShouldBeIgnoredOrRejectedWithTheReason(string key, string json, KeyOutcome expected, string reason)
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange((key, json), ("thirdPartyServiceProvider", "\"Still Saved AB\"")));

        var keyResult = result.Keys.Single(k => k.Key == key);
        Assert.Equal(expected, keyResult.Outcome);
        Assert.Contains(reason, keyResult.Message, StringComparison.Ordinal);
        Assert.True(result.Saved); // the valid key in the same change set is still saved (J-12)
        var pack = (await store.GetAsync<BatteryPack>(PackId))!;
        Assert.Equal(seed.Sources.Pack.Values.ContainsKey(key), pack.Values.ContainsKey(key));
        if (seed.Sources.Pack.Values.TryGetValue(key, out var original))
        {
            Assert.True(JsonNode.DeepEquals(original.Value, pack.Values[key].Value));
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"  \"")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task EmptyValue_ShouldKeepTheStoredValue(string json)
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange(("thirdPartyServiceProvider", json)));

        Assert.False(result.Saved);
        Assert.Equal(KeyOutcome.KeptEmpty, Assert.Single(result.Keys).Outcome);
        Assert.Equal("Example Passport Hosting AB", (await store.GetAsync<BatteryPack>(PackId))!.Values["thirdPartyServiceProvider"].Value.GetValue<string>());
    }

    [Theory]
    [InlineData("cluster-test", "temperatureInformation", true)]
    [InlineData("another-cluster", "temperatureInformation", false)]
    [InlineData("cluster-test", "batterySerialNumber", false)]
    public async Task ClusterAdmin_ShouldChangeOnlyPurchaserDataOfTheirOwnBatteries(string adminCluster, string key, bool saved)
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var value = key == "temperatureInformation" ? "31" : "\"X-1\"";

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(ChangeSender.ClusterAdmin("admin@example.com", adminCluster), PackChange((key, value)));

        Assert.Equal(saved, result.Saved);
        Assert.Equal(saved ? KeyOutcome.Saved : KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task Feed_ShouldNotChangeAccidentInformation()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var accident = """{"contentType":"text/html","url":"https://example.com/accident"}""";

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), PackChange(("informationOnAccidents", accident)));

        Assert.Equal(KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task Team_ShouldSetAModelsStartingValueForARestatedAttribute()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var change = new ChangeSet(TargetKind.Model, seed.Sources.Model.Id, Values(("capacityFade", "1")), "form");

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, change);

        Assert.True(result.Saved);
        Assert.Equal(1, (await store.GetAsync<BatteryModel>(seed.Sources.Model.Id))!.Values["capacityFade"].Value["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task MissingRecord_ShouldBeReportedAsNotFound()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, new ChangeSet(TargetKind.Batch, "no-such-batch", Values(("carbonFootprintPerformanceClass", "\"Class A\"")), "form"));

        Assert.False(result.Found);
        Assert.False(result.Saved);
    }

    [Fact]
    public async Task Save_ShouldBeLoggedWithTheResultPerKey()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange(("thirdPartyServiceProvider", "\"Logged AB\""), ("ratedCapacity", "200")));

        var events = await new ApplicationAuditService(context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var logged = Assert.Single(events, e => e["eventType"].AsString == SavePipeline.AuditEventType);
        Assert.Equal(PackId, logged["entityId"].AsString);
        Assert.Equal("team@example.com", logged["actor"].AsString);
        Assert.Equal(2, logged["metadata"]["keys"].AsBsonArray.Count);
    }

    private static ChangeSet PackChange(params (string Key, string Json)[] values) => new(TargetKind.Pack, PackId, Values(values), "form");

    private static Dictionary<string, JsonNode?> Values(params (string Key, string Json)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonNode.Parse(v.Json), StringComparer.Ordinal);
}
