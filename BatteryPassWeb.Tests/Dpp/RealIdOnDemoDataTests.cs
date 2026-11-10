using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// U16: a real battery ID can't meet demo product data. Making a battery with a real ID on a demo batch, model or
/// operator is refused (the form and the API make it through the same service), and the same check blocks publishing
/// for a battery made before the rule. A demo ID on real product data stays allowed.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RealIdOnDemoDataTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;

    private static async Task<(DemoRig Rig, BatteryPack Demo)> SeededAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(Who);
        return (rig, (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single());
    }

    private static NewPack Request(BatteryPack like, string serial, bool fromPool) =>
        new(like.BatchId, like.OperatorId, like.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, FromPool: fromPool);

    [Fact]
    public async Task ARealReleasedId_ShouldBeRefusedOnADemoBatch_AndStayAvailable()
    {
        var (rig, demo) = await SeededAsync(mongo);
        using var _ = rig;
        var pool = rig.Services.GetRequiredService<BatteryIdPool>();
        await pool.ReleaseAsync(Who, new ReleaseRequest("PO 1", null, [], "VPC-12221"));

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), Request(demo, "VPC-12221", fromPool: true));

        Assert.Null(result.Pack);
        Assert.Equal(DemoDataRule.Message, result.Problem);
        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("VPC-12221"))!.Status);
        Assert.Empty(await rig.Store.FindPacksBySerialNumberAsync("VPC-12221"));
    }

    [Fact]
    public async Task ARealIdMadeFromATypedSerialNumber_ShouldBeRefusedOnADemoBatch()
    {
        var (rig, demo) = await SeededAsync(mongo);
        using var _ = rig;

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), Request(demo, "REAL-0001", fromPool: false));

        Assert.Null(result.Pack);
        Assert.Equal(DemoDataRule.Message, result.Problem);
    }

    [Fact]
    public async Task ADemoId_ShouldStillBeAllowedOnDemoData()
    {
        var (rig, demo) = await SeededAsync(mongo);
        using var _ = rig;

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), Request(demo, "DEMO-C7M-0099", fromPool: false) with { IsDemo = true });

        Assert.Null(result.Problem);
        Assert.True(result.Pack!.IsDemo);
    }

    [Theory]
    [InlineData(true, false, false, "real", true)]
    [InlineData(false, true, false, "real", true)]
    [InlineData(false, false, true, "real", true)]
    [InlineData(false, false, false, "real", false)] // real data takes a real ID
    [InlineData(true, true, true, "demo", false)] // a demo ID may sit on demo data
    [InlineData(false, false, false, "demo", false)] // and on real data
    public void TheRule_ShouldRefuseOnlyARealIdOnDemoData(bool demoBatch, bool demoModel, bool demoOperator, string id, bool conflict)
    {
        var pack = new BatteryPack { Id = "pack-1", BatchId = "b", OperatorId = "o", IsDemo = id == "demo" };
        pack.Values["uniqueProductIdentifier"] = new AttributeValue
        {
            Value = JsonValue.Create(id == "demo" ? "https://www.sib-scania-eubp.eu/25S/UN778474481-TEST-0001" : "https://www.sib-scania-eubp.eu/25S/UN778474481-VPC-0001")!,
            GeneratedAtUtc = DateTime.UtcNow,
            Source = "system",
        };
        var sources = new PassportSources(
            pack,
            new ProductionBatch { Id = "b", ModelId = "m", SiteId = "s", Year = 2026, IsDemo = demoBatch },
            new BatteryModel { Id = "m", Name = "M", FamilyId = "f", IsDemo = demoModel },
            new ManufacturingSite { Id = "s", Name = "S" },
            new EconomicOperator { Id = "o", Name = "O", IsDemo = demoOperator });

        Assert.Equal(conflict, DemoDataRule.Conflicts(sources));
    }

    [Fact]
    public async Task ABatteryWithARealIdOnDemoData_ShouldNotBePublished_AndTheCheckSaysWhy()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetValuesAsync<BatteryPack>(
            rig.PackId,
            new Dictionary<string, AttributeValue>
            {
                ["uniqueProductIdentifier"] = new() { Value = JsonValue.Create("https://www.sib-scania-eubp.eu/25S/UN778474481-VPC-0001")!, GeneratedAtUtc = rig.Clock.Now.UtcDateTime, Source = "system" },
            });
        var batches = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Batches);
        await batches.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", rig.Seed.Sources.Batch.Id), Builders<BsonDocument>.Update.Set("IsDemo", true));

        var check = await rig.Publication.CheckAsync(rig.PackId);
        var published = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.Contains(DemoDataRule.Message, check!.Blocking);
        Assert.Null(published.Version);
        Assert.Contains(DemoDataRule.Message, published.Report!.Blocking);
        Assert.Equal(0, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));

        await batches.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", rig.Seed.Sources.Batch.Id), Builders<BsonDocument>.Update.Set("IsDemo", false));
        Assert.Null((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem); // real data takes a real ID
    }
}
