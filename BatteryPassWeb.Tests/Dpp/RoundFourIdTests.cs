using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S1 round four. (1) No key is given on a start where the unique item_key index doesn't exist: the fill could give both
/// batteries of a colliding pair the same key. (2) A new passport's R-number sees a taken number by its key, a dash
/// anywhere: a released ID or a removed passport written <c>…-R-1</c>, <c>…-R1-0</c> or <c>…-r1</c>.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RoundFourIdTests(MongoFixture mongo)
{
    private static BsonDocument OldPack(string id, BatteryPack like, string item, string? key = null)
    {
        var pack = new BsonDocument
        {
            ["_id"] = id, ["BatchId"] = like.BatchId, ["OperatorId"] = like.OperatorId, ["IsDemo"] = false,
            ["Values"] = new BsonDocument { ["uniqueProductIdentifier"] = new BsonDocument { ["Value"] = $"{Base}/25S/{item}", ["GeneratedAtUtc"] = DateTime.UtcNow, ["Source"] = "system" } },
        };
        if (key is not null)
        {
            pack["ItemKey"] = key;
        }

        return pack;
    }

    private static async Task DropKeyIndexesAsync(IMongoCollection<BsonDocument> packs)
    {
        foreach (var name in new[] { DppStore.ItemKeyIndex, DppStore.ItemKeyFindIndex })
        {
            try { await packs.Indexes.DropOneAsync(name); } catch (MongoCommandException) { /* not there */ }
        }
    }

    private static async Task<string?> KeyOf(IMongoCollection<BsonDocument> packs, string id) =>
        (await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync()).GetValue("ItemKey", BsonNull.Value) is { IsString: true } key ? key.AsString : null;

    [Fact]
    public async Task KeysThatAlreadyCollide_ShouldStopTheFill_AndBeNamed()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var packs = rig.Database.GetCollection<BsonDocument>(DppStore.Packs);
        await DropKeyIndexesAsync(packs);
        // An earlier build wrote the same key twice (no index then); and an old pack still has none.
        await packs.InsertOneAsync(OldPack("pack-pair-a", c7, "UN778474481-VBC-4-R1", "UN778474481VBC4R1"));
        await packs.InsertOneAsync(OldPack("pack-pair-b", c7, "UN778474481-VBC-4R1", "UN778474481VBC4R1"));
        await packs.InsertOneAsync(OldPack("pack-keyless", c7, "UN778474481-VBC-5"));

        var named = await rig.Store.EnsureIndexesAsync();

        Assert.Null(await KeyOf(packs, "pack-keyless")); // no key given without the index
        Assert.DoesNotContain(await packs.Indexes.List().ToListAsync(), index => index["name"] == DppStore.ItemKeyIndex);
        var pair = Assert.Single(named);
        Assert.Equal("UN778474481VBC4R1", pair.ItemKey);
        Assert.Equal(["pack-pair-a", "pack-pair-b"], new[] { pair.PackId, pair.HeldBy }.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnIndexBuildThatFailsOnce_ShouldGiveNoKeys_AndTheNextStartGivesThemAll()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var packs = rig.Database.GetCollection<BsonDocument>(DppStore.Packs);
        await DropKeyIndexesAsync(packs);
        await packs.UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, Builders<BsonDocument>.Update.Unset("ItemKey"));
        await packs.InsertOneAsync(OldPack("pack-old-1", c7, "UN778474481-VBC-6"));
        // Something in the way: an index of that name that isn't unique, so the build fails (as a passing error would).
        await packs.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("ItemKey"), new CreateIndexOptions { Name = DppStore.ItemKeyIndex }));

        Assert.Empty(await rig.Store.EnsureIndexesAsync()); // the start goes on
        Assert.Null(await KeyOf(packs, "pack-old-1"));

        await packs.Indexes.DropOneAsync(DppStore.ItemKeyIndex); // the cause is gone
        await rig.Store.EnsureIndexesAsync();
        Assert.Equal("UN778474481VBC6", await KeyOf(packs, "pack-old-1"));
        Assert.Equal(0, await packs.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ItemKey", BsonNull.Value) & Builders<BsonDocument>.Filter.Exists("Values.uniqueProductIdentifier.Value")));
    }

    [Theory]
    [InlineData("VBC-1-R-1", 1)]
    [InlineData("VBC-1-r1", 1)]
    [InlineData("VBC-1-R1-0", 10)]
    public async Task AReleasedId_WithADashAnywhereInItsRNumber_ShouldBeSkippedByANewPassport(string serial, int number)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var serials = new List<string> { "VBC-1", serial };
        serials.AddRange(Enumerable.Range(1, number - 1).Select(n => $"VBC-1-R{n}")); // the numbers below it taken too
        Assert.True((await pool.ReleaseAsync(Who, List("PO", [.. serials]))).Done);
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        var old = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VBC-1"))).Pack!;

        var next = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(old, "VBC-1") with { FromPool = false, PredecessorPackId = old.Id });

        Assert.True(next.Pack is not null, next.Problem);
        Assert.EndsWith($"/25S/UN778474481-VBC-1-R{number + 1}", Text(next.Pack!, "uniqueProductIdentifier"), StringComparison.Ordinal);
        Assert.Contains($"UN778474481VBC1R{number}", await rig.Store.TakenItemKeysAsync("UN778474481-VBC-1"));
    }

    [Theory]
    [InlineData("UN778474481-VBC-1-R-1")]
    [InlineData("UN778474481-vbc-1-r1")]
    [InlineData("UN778474481-VBC1R-1")]
    public async Task ARemovedPassport_WithADashAnywhereInItsRNumber_ShouldNotStopANewPassportForever(string removedItem)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO", "VBC-1"));
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        var old = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VBC-1"))).Pack!;
        await rig.Store.AddRemovedAsync(new RemovedPassport { Id = "pack-removed", BatteryId = $"{Base}/25S/{removedItem}", Ids = [$"{Base}/25S/{removedItem}", removedItem], RemovedAtUtc = DateTime.UtcNow, RemovedBy = Who });

        var next = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(old, "VBC-1") with { FromPool = false, PredecessorPackId = old.Id });

        Assert.True(next.Pack is not null, next.Problem); // not "removed" on every try
        Assert.EndsWith("/25S/UN778474481-VBC-1-R2", Text(next.Pack!, "uniqueProductIdentifier"), StringComparison.Ordinal);
    }
}
