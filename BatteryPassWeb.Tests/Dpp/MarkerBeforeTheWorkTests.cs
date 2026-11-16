using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round four, 1: a saved change marks every published passport it reaches before any of them is published, in one
/// write; each marker goes when its passport is done. A run broken half way leaves the rest marked: the safe side.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkerBeforeTheWorkTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task ARunBrokenHalfWay_ShouldLeaveEveryPassportWithoutItsNewVersionMarked_AndGoOnWithTheOthers()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var packs = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Packs);
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var pack = await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)).SingleAsync();
        var version = await versions.Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SingleAsync();
        string[] ids = ["pack-break-1", "pack-break-2", "pack-break-3"];
        foreach (var id in ids)
        {
            var copy = pack.DeepClone().AsBsonDocument;
            copy["_id"] = id;
            copy["Values"]["batterySerialNumber"]["Value"] = id.ToUpperInvariant();
            copy["Values"]["uniqueProductIdentifier"]["Value"] = $"urn:test:{id}";
            copy["ItemKey"] = BatteryPassWeb.Dpp.Identifiers.IdentifierForms.ItemKey($"urn:test:{id}"); // its own ID's key (S1 round two)
            await packs.InsertOneAsync(copy);
            var versionCopy = version.DeepClone().AsBsonDocument;
            versionCopy["_id"] = $"version-{id}";
            versionCopy["PackId"] = id;
            versionCopy["DigitalProductPassportId"] = $"urn:test:{id}:v1";
            await versions.InsertOneAsync(versionCopy);
        }

        // One battery's stored ID is broken, so publishing it throws half way through the run.
        await packs.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", "pack-break-2"), Builders<BsonDocument>.Update.Set("Values.uniqueProductIdentifier.Value", new BsonDocument("broken", true)));
        var mass = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 };

        // Round five: one battery's failure never stops the others; it stays marked and is logged.
        var saved = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["batteryMass"] = mass }, "form"));
        Assert.True(saved.Saved);
        var failed = await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", BatteryPassWeb.Dpp.Publication.PublicationService.FailedEvent)).ToListAsync();
        Assert.Equal("pack-break-2", Assert.Single(failed)["entityId"].AsString);

        Assert.Equal("513", (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString()); // saved
        var left = 0;
        foreach (var id in ids.Prepend(rig.PackId))
        {
            var stored = (await rig.Store.GetAsync<BatteryPack>(id))!;
            if ((await rig.Store.GetActiveVersionAsync(id))!.Number == 1)
            {
                left++;
                Assert.NotNull(stored.ChangesWaitingSinceUtc); // no new version yet: marked
            }
            else
            {
                Assert.Null(stored.ChangesWaitingSinceUtc); // done: no marker
            }
        }

        Assert.Equal(1, left); // only the broken one: the others were published after it
        var page = await BatteryTesting.Pages(rig).TeamListAsync(null);
        Assert.Equal(left, page.Waiting.Count);
    }

    [Fact]
    public async Task ARunThatFinishes_ShouldLeaveNoMarker()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var mass = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 };

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["batteryMass"] = mass }, "form"));

        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }
}
