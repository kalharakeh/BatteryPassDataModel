using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round three, 5: a version that carries a change that was waiting is logged in the name of who set it off (the feed,
/// a cluster admin, a token, the team) and says "published with the changes that were waiting".
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WaitingChangeAuditLineTests(MongoFixture mongo)
{
    private const string Cycles = "numberOfFullChargingAndDischargingCycles";

    [Fact]
    public async Task APublicationAfterALiveValue_ShouldNameTheSenderAndSayItCarriesTheWaitingChanges()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync("model-test", true, false, true);
        await rig.Store.SetValuesAsync<BatteryPassWeb.Dpp.Store.BatteryPack>(rig.PackId, new Dictionary<string, BatteryPassWeb.Dpp.Values.AttributeValue>(), [Cycles]);
        Assert.Null((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem);
        await rig.Store.SetModelAnswersAsync("model-test", true, true, true);
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["batteryMass"] = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 } }, "form"));

        await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { [Cycles] = 14 }, "feed"));

        var entry = await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", PublicationService.PublishedEvent))
            .SortByDescending(item => item["_id"]).FirstAsync();
        Assert.Equal($"Version 2 of pack {rig.PackId} published with the changes that were waiting", entry["message"].AsString);
        Assert.Equal("bms-feed", entry["actor"].AsString);
        Assert.True(entry["metadata"]["withWaitingChanges"].AsBoolean);
    }

    [Fact]
    public async Task AnOrdinaryPublication_ShouldReadAsBefore()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var entry = await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", PublicationService.PublishedEvent))
            .SortByDescending(item => item["_id"]).FirstAsync();

        Assert.Equal($"Version 1 of pack {rig.PackId} published", entry["message"].AsString);
        Assert.False(entry["metadata"]["withWaitingChanges"].AsBoolean);
    }
}
