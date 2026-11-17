using System.Diagnostics;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit.Abstractions;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S1 round three, point 2: the start-up fill of the item key. The unique index comes first; each key is written with one
/// guarded update (the pack's _id, no key yet, and the ID still the one read); a duplicate the database reports leaves the
/// pack without a key and names both packs, never a pack against itself; the start never fails; a later start reads only
/// what the index finds without a key.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ItemKeyFillTests(MongoFixture mongo, ITestOutputHelper output)
{
    private static BsonDocument OldPack(string id, string batchId, string operatorId, string item) => new()
    {
        ["_id"] = id, ["BatchId"] = batchId, ["OperatorId"] = operatorId, ["IsDemo"] = false,
        ["Values"] = new BsonDocument { ["uniqueProductIdentifier"] = new BsonDocument { ["Value"] = $"{Base}/25S/{item}", ["GeneratedAtUtc"] = DateTime.UtcNow, ["Source"] = "system" } },
    };

    /// <summary>A database as a build before round two left it: packs without a key, and no item key indexes.</summary>
    private static async Task<(DemoRig Rig, IMongoCollection<BsonDocument> Packs, BatteryPack Like)> OldDatabaseAsync(MongoFixture mongo)
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        var packs = rig.Database.GetCollection<BsonDocument>(DppStore.Packs);
        foreach (var name in new[] { DppStore.ItemKeyIndex, DppStore.ItemKeyFindIndex })
        {
            try { await packs.Indexes.DropOneAsync(name); } catch (MongoCommandException) { /* not there */ }
        }

        await packs.UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, Builders<BsonDocument>.Update.Unset("ItemKey"));
        return (rig, packs, c7);
    }

    private static async Task<string?> KeyOf(IMongoCollection<BsonDocument> packs, string id) =>
        (await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync()).GetValue("ItemKey", BsonNull.Value) is { IsString: true } key ? key.AsString : null;

    [Fact]
    public async Task TwoStartsAtOnce_ShouldGiveEveryPackItsKey_NameARealPairOnly_AndNeverFail()
    {
        var (rig, packs, like) = await OldDatabaseAsync(mongo);
        using var __ = rig;
        await packs.InsertManyAsync(Enumerable.Range(0, 300).Select(n => OldPack($"pack-old-{n:000}", like.BatchId, like.OperatorId, $"UN778474481-OLD-{n:000}")));
        await packs.InsertOneAsync(OldPack("pack-old-pair-a", like.BatchId, like.OperatorId, "UN778474481-VBC-6-R1"));
        await packs.InsertOneAsync(OldPack("pack-old-pair-b", like.BatchId, like.OperatorId, "UN778474481-VBC-6R1"));
        var other = new DppStore(rig.Services.GetService(typeof(BatteryPassWeb.Services.MongoContext)) as BatteryPassWeb.Services.MongoContext ?? throw new InvalidOperationException());

        var both = await Task.WhenAll(Task.Run(() => rig.Store.EnsureIndexesAsync()), Task.Run(() => other.EnsureIndexesAsync()));

        for (var n = 0; n < 300; n++)
        {
            Assert.Equal($"UN778474481OLD{n:000}", await KeyOf(packs, $"pack-old-{n:000}"));
        }

        Assert.Equal("UN778474481VBC6R1", await KeyOf(packs, "pack-old-pair-a")); // the first in _id order
        Assert.Null(await KeyOf(packs, "pack-old-pair-b"));
        var named = both.SelectMany(collisions => collisions).ToArray();
        Assert.NotEmpty(named);
        Assert.All(named, collision => Assert.Equal(("pack-old-pair-b", "pack-old-pair-a", "UN778474481VBC6R1"), (collision.PackId, collision.HeldBy, collision.ItemKey)));
        Assert.All(named, collision => Assert.NotEqual(collision.PackId, collision.HeldBy)); // never a pack against itself
    }

    [Fact]
    public async Task ASwapBetweenTheReadAndTheWrite_ShouldKeepTheSwapsKey()
    {
        var (rig, packs, like) = await OldDatabaseAsync(mongo);
        using var __ = rig;
        await rig.Store.EnsureIndexesAsync();
        await packs.InsertOneAsync(OldPack("pack-swapped", like.BatchId, like.OperatorId, "UN778474481-OLD-1"));
        var read = $"{Base}/25S/UN778474481-OLD-1";

        // Meanwhile the draft swapped to another ID: its key came with it.
        await rig.Store.SetValuesAsync<BatteryPack>("pack-swapped", new Dictionary<string, BatteryPassWeb.Dpp.Values.AttributeValue>(StringComparer.Ordinal)
        {
            ["uniqueProductIdentifier"] = new() { Value = System.Text.Json.Nodes.JsonValue.Create($"{Base}/25S/UN778474481-NEW-2"), GeneratedAtUtc = DateTime.UtcNow, Source = "system" },
        });
        var written = await rig.Store.FillItemKeyAsync("pack-swapped", read);

        Assert.Equal(ItemKeyFill.Changed, written.Outcome);
        Assert.Equal("UN778474481NEW2", await KeyOf(packs, "pack-swapped"));
    }

    [Fact]
    public async Task AnInsertWithTheSameKeyBetweenTheReadAndTheWrite_ShouldLeaveTheOldPackKeyless_AndNameBoth()
    {
        var (rig, packs, like) = await OldDatabaseAsync(mongo);
        using var __ = rig;
        await rig.Store.EnsureIndexesAsync();
        await packs.InsertOneAsync(OldPack("pack-old-x", like.BatchId, like.OperatorId, "UN778474481-VBC-7-R1"));
        var read = $"{Base}/25S/UN778474481-VBC-7-R1";
        await packs.InsertOneAsync(OldPack("pack-new-y", like.BatchId, like.OperatorId, "UN778474481-VBC7R1").Add("ItemKey", "UN778474481VBC7R1"));

        var written = await rig.Store.FillItemKeyAsync("pack-old-x", read);

        Assert.Equal(ItemKeyFill.Held, written.Outcome);
        Assert.Equal(new ItemKeyCollision("pack-old-x", "UN778474481VBC7R1", "pack-new-y"), written.Collision);
        Assert.Null(await KeyOf(packs, "pack-old-x"));
        Assert.Equal(["pack-old-x"], (await rig.Store.EnsureIndexesAsync()).Select(collision => collision.PackId)); // and the start goes on
    }

    [Fact]
    public async Task ALaterStart_ShouldReadOnlyThePacksTheIndexFindsWithoutAKey()
    {
        var (rig, packs, like) = await OldDatabaseAsync(mongo);
        using var __ = rig;
        await packs.InsertManyAsync(Enumerable.Range(0, 2000).Select(n => OldPack($"pack-many-{n:0000}", like.BatchId, like.OperatorId, $"UN778474481-MANY-{n:0000}")));

        var first = Stopwatch.StartNew();
        await rig.Store.EnsureIndexesAsync();
        first.Stop();
        var later = Stopwatch.StartNew();
        await rig.Store.EnsureIndexesAsync();
        later.Stop();
        output.WriteLine($"first start with 2,000 packs without a key: {first.ElapsedMilliseconds} ms; a later start: {later.ElapsedMilliseconds} ms");

        Assert.Equal(0, await packs.CountDocumentsAsync(Builders<BsonDocument>.Filter.Exists("ItemKey", false) & Builders<BsonDocument>.Filter.Exists("Values.uniqueProductIdentifier.Value")));
        var plan = await rig.Store.ExplainKeylessQueryAsync();
        Assert.Contains("IXSCAN", plan, StringComparison.Ordinal); // the index answers "which packs have no key"
        Assert.DoesNotContain("COLLSCAN", plan, StringComparison.Ordinal);
        Assert.True(first.Elapsed < TimeSpan.FromSeconds(60), $"{first.Elapsed}");
    }
}
