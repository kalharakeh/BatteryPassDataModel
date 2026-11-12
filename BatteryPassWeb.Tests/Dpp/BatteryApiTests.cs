using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The API's writing side (step 3c-2): creating, publishing and the live values, on the pages' services.</summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryApiTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = ChangeSender.Team("api-token:team");
    private static readonly ChangeSender NorthFeed = ChangeSender.Feed("api-token:north", BatteryTesting.North);
    private static readonly ChangeSender SouthFeed = ChangeSender.Feed("api-token:south", BatteryTesting.South);

    [Fact]
    public void TheLiveValues_ShouldBeTheCataloguesLiveSetThatABatterySystemFeeds()
    {
        Assert.Equal(["#63", "#83", "#87", "E1", "E2", "E3", "E4"], BatteryApi.LiveSet.Select(attribute => attribute.Label));
        Assert.Equal("#63, #83, #87, E1, E2, E3 and E4", BatteryApi.LiveSetWords);
    }

    [Fact]
    public void AToken_ShouldWriteAsTheTeamOrAsABatterySystemOfItsClusters()
    {
        var global = new ExternalApiTokenContext { TokenId = "t-1", GlobalAccess = true };
        var cluster = new ExternalApiTokenContext { TokenId = "t-2", ClusterIds = [" Cluster-Test "] };

        var team = DppReaders.SenderFor(global);
        var feed = DppReaders.SenderFor(cluster);

        Assert.Equal(SenderKind.Team, team.Kind);
        Assert.Equal(DppReaders.For(global).Who, team.Who);
        Assert.Equal(SenderKind.BatterySystemFeed, feed.Kind);
        Assert.Equal(DppReaders.For(cluster).Who, feed.Who);
        Assert.Contains(BatteryTesting.North, feed.ClusterIds);
        Assert.Contains("CLUSTER-TEST", feed.ClusterIds);
    }

    [Fact]
    public async Task ABatteryToWrite_ShouldBeFoundForItsOwnClusterAndADraftForTheTeamOnly()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var api = Api(rig);

        Assert.Equal(WriteRefusal.None, (await api.FindAsync(Team, Serial(rig))).Refusal);
        Assert.Equal(WriteRefusal.NotFound, (await api.FindAsync(NorthFeed, Serial(rig))).Refusal);

        await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        foreach (var id in new[] { PassportPagesTests.Identifier(rig), version.DigitalProductPassportId, Serial(rig) })
        {
            var found = await api.FindAsync(NorthFeed, id);
            Assert.Equal(rig.PackId, found.Pack?.Id);
            Assert.Equal(PassportPagesTests.Identifier(rig), found.Identifier);
        }

        Assert.Equal(WriteRefusal.NotYours, (await api.FindAsync(SouthFeed, Serial(rig))).Refusal);
        Assert.Equal(WriteRefusal.NotFound, (await api.FindAsync(Team, "no-such-battery")).Refusal);
    }

    [Fact]
    public async Task LiveValues_ShouldBeSavedInPlaceWithTheirMeasurementTime()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var measured = rig.Clock.Now.UtcDateTime.AddMinutes(-10);
        rig.Clock.Now = rig.Clock.Now.AddMinutes(1);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var result = await Api(rig).WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 64), ("temperatureInformation", 23), ("timeSpentInExtremeTemperaturesAboveBoundary", 120), ("batteryStatus", "waste"), ("ipAddress", "10.0.0.1")), measured, preview: false);

        Assert.True(result.Saved);
        Assert.Equal(
            ["stateOfChargeSoC:Saved", "temperatureInformation:Saved", "timeSpentInExtremeTemperaturesAboveBoundary:Saved", "batteryStatus:Rejected", "ipAddress:Ignored"],
            result.Keys.Select(key => $"{key.Key}:{key.Outcome}"));
        Assert.Equal("#22 is not a live value; this call takes #63, #83, #87, E1, E2, E3 and E4", result.Keys[3].Message);
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        Assert.Equal(64, stored["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(measured, stored["stateOfChargeSoC"].GeneratedAtUtc);
        Assert.Equal(BatteryApi.FeedSource, stored["stateOfChargeSoC"].Source);
        Assert.Equal(NorthFeed.Who, stored["stateOfChargeSoC"].Who);
        var versions = await rig.Store.ListVersionsAsync(rig.PackId);
        Assert.Single(versions);
        Assert.Equal(rig.Clock.Now.UtcDateTime, versions[0].LastUpdatedUtc);
    }

    [Fact]
    public async Task ABatterySystemOfAnotherCluster_ShouldChangeNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var result = await Api(rig).WriteLiveValuesAsync(SouthFeed, pack, Values(("stateOfChargeSoC", 10)), null, preview: false);

        Assert.False(result.Saved);
        Assert.Equal(KeyOutcome.Rejected, result.Keys.Single().Outcome);
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task APreview_ShouldReportWithoutSaving()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var result = await Api(rig).WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 10), ("temperatureInformation", "")), null, preview: true);

        Assert.False(result.Saved);
        Assert.Equal([KeyOutcome.Saved, KeyOutcome.KeptEmpty], result.Keys.Select(key => key.Outcome));
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task TheChangeLog_ShouldKeepEachSavedValuesMeasurementTime()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var measured = new DateTime(2026, 10, 28, 8, 15, 0, DateTimeKind.Utc);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        await Api(rig).WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 64)), measured, preview: false);

        var change = await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", SavePipeline.AuditEventType)).SortByDescending(document => document["createdAt"]).FirstAsync();
        var key = change["metadata"]["keys"][0].AsBsonDocument;
        Assert.Equal("stateOfChargeSoC", key["key"].AsString);
        Assert.Equal("2026-10-28T08:15:00.0000000Z", key["generatedAt"].AsString);
    }

    [Fact]
    public async Task TheHistory_ShouldListEachSavedLiveValueNewestFirstForThoseWhoMaySeeIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var api = Api(rig);
        var now = rig.Clock.Now.UtcDateTime;
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        await api.WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 70), ("temperatureInformation", 21)), now.AddHours(-30), preview: false);
        await api.WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 64)), now.AddHours(-2), preview: false);
        await api.WriteLiveValuesAsync(NorthFeed, pack, Values(("timeSpentInExtremeTemperaturesAboveBoundary", 120)), now.AddHours(-1), preview: false);

        var own = await api.HistoryAsync(Reader.With("north", ReaderRole.ClusterUser, BatteryTesting.North), rig.PackId, 24);
        var week = await api.HistoryAsync(Reader.With("team", ReaderRole.Team), rig.PackId, BatteryApi.MaxHistoryHours);
        var other = await api.HistoryAsync(Reader.With("south", ReaderRole.ClusterUser, BatteryTesting.South), rig.PackId, 24);

        Assert.Equal(["E1", "#63"], own.Select(entry => entry.Attribute.Label));
        Assert.Equal([now.AddHours(-1), now.AddHours(-2)], own.Select(entry => entry.MeasuredAtUtc));
        Assert.Equal(64, own[1].Value["percentageValue"]!.GetValue<int>());
        Assert.Equal(AttributeCatalog.Extensions[0].Unit, own[0].Attribute.Unit);
        Assert.All(own, entry => Assert.Equal(BatteryApi.FeedSource, entry.Source));
        Assert.Equal(["E1", "#63", "#63", "#87"], week.Select(entry => entry.Attribute.Label));
        Assert.Empty(other);
    }

    [Fact]
    public async Task AMeasurementTime_ShouldNotLieAheadOfTheAppsClock()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var now = rig.Clock.Now.UtcDateTime;

        Assert.False(Api(rig).InTheFuture(now.Add(BatteryApi.ClockTolerance)));
        Assert.True(Api(rig).InTheFuture(now.Add(BatteryApi.ClockTolerance).AddSeconds(1)));
    }

    [Fact]
    public async Task TheTeam_ShouldCreateADraftBatteryAndPublishIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var api = Api(rig);
        var choices = await api.ChoicesAsync();
        var batch = choices.Batches.Single();
        var economicOperator = choices.Operators.Single();

        var created = await api.CreateAsync(Team, batch.BatchId, economicOperator.Id, " Cluster-South ", Values(("batterySerialNumber", "VPC9-4001"), ("manufacturingDate", "2026-09")));

        Assert.Null(created.Problem);
        var pack = created.Pack!;
        Assert.Equal(BatteryTesting.South, pack.ClusterId);
        Assert.Equal(BatteryApi.ApiSource, pack.Values["batterySerialNumber"].Source);
        Assert.Equal("API", PassportPages.SourceWord(BatteryApi.ApiSource));
        Assert.Null(await rig.Store.GetActiveVersionAsync(pack.Id));

        var copied = await CopySeedValuesAsync(rig, pack.Id);
        var published = await api.PublishAsync(Team, copied);
        Assert.Null(published.Problem);
        Assert.Equal(1, published.Version!.Number);
    }

    [Theory]
    [InlineData("feed", null, "only the team creates batteries")]
    [InlineData("team", "no-such-cluster", "cluster no-such-cluster doesn't exist; name a registered cluster or none")]
    [InlineData("team-without-batch", null, "batchId and operatorId are required; GET batches and GET operators list them")]
    public async Task ABatteryThatCannotBeCreated_ShouldSayWhy(string who, string? cluster, string problem)
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var batch = who == "team-without-batch" ? null : rig.Seed.Sources.Batch.Id;

        var created = await Api(rig).CreateAsync(who == "feed" ? NorthFeed : Team, batch, rig.Seed.Sources.Operator.Id, cluster, Values(("batterySerialNumber", "VPC9-4002")));

        Assert.Null(created.Pack);
        Assert.Equal(problem, created.Problem);
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldPublish()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var result = await Api(rig).PublishAsync(NorthFeed, pack);

        Assert.Null(result.Version);
        Assert.Equal("only the team publishes passports", result.Problem);
    }

    internal static BatteryApi Api(PublicationRig rig) => new(
        rig.Store,
        rig.Pipeline,
        BatteryTesting.Creation(rig),
        rig.Publication,
        BatteryTesting.Pages(rig),
        new ClusterRepository(rig.Context),
        new ApplicationAuditService(rig.Context),
        rig.Clock);

    internal static Dictionary<string, JsonNode?> Values(params (string Key, object? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value is null ? null : JsonValue.Create(value.Value) as JsonNode, StringComparer.Ordinal);

    private static string Serial(PublicationRig rig) => rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value!.GetValue<string>();

    /// <summary>The seed pack's own values on a new pack, so it can be published; returns the pack as stored.</summary>
    private static async Task<BatteryPack> CopySeedValuesAsync(PublicationRig rig, string packId)
    {
        var values = rig.Seed.Sources.Pack.Values
            .Where(entry => entry.Key is not ("uniqueProductIdentifier" or "batteryStatus" or "informationOnAccidents" or "batterySerialNumber" or "manufacturingDate"))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        await rig.Store.SetValuesAsync<BatteryPack>(packId, values);
        return (await rig.Store.GetAsync<BatteryPack>(packId))!;
    }
}
