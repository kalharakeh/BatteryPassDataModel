using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S1 round two, the one rule: two batteries never have IDs whose item parts are the same once dashes and case are
/// ignored (<see cref="IdentifierForms.ItemKey"/>: <c>UN778474481VBC1R1</c>). It holds at release, at creation from the
/// pool, at a swap and for a new passport's <c>-R&lt;n&gt;</c>, and the database refuses the second one (a unique index on
/// the stored key) when two requests arrive together. The scenario: battery <c>VBC-1</c> has a new passport
/// <c>…-VBC-1-R1</c>, and a released serial <c>VBC-1-r1</c>, <c>VBC-1R1</c> or <c>VBC1-R1</c> would give another
/// battery the same ID.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class OneBatteryIdRuleTests(MongoFixture mongo)
{
    private static PackCreationService Creation(DemoRig rig) => rig.Services.GetRequiredService<PackCreationService>();

    private static async Task<BatteryPack> CreateAsync(DemoRig rig, BatteryPack like, string serial)
    {
        var created = await Creation(rig).CreateAsync(ChangeSender.Team(Who), NewBattery(like, serial));
        Assert.True(created.Pack is not null, created.Problem + string.Join("; ", created.Keys.Select(key => key.Message)));
        return created.Pack!;
    }

    /// <summary>Battery VBC-1's new passport: the same battery, the next ID (…-VBC-1-R1).</summary>
    private static Task<PackCreationResult> NewPassportAsync(DemoRig rig, BatteryPack old) =>
        Creation(rig).CreateAsync(ChangeSender.Team(Who), NewBattery(old, "VBC-1") with { FromPool = false, PredecessorPackId = old.Id });

    private static string Id(BatteryPack pack) => Text(pack, "uniqueProductIdentifier")!;

    [Theory]
    [InlineData("VBC-1-r1")]
    [InlineData("VBC-1R1")]
    [InlineData("VBC1-R1")]
    [InlineData("vbc-1-r1")]
    public async Task ASerialReleasedAfterTheNewPassport_ShouldBeRefused_AtRelease(string serial)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO 1", "VBC-1"));
        var old = await CreateAsync(rig, c7, "VBC-1");
        var next = (await NewPassportAsync(rig, old)).Pack!;
        Assert.EndsWith("/25S/UN778474481-VBC-1-R1", Id(next), StringComparison.Ordinal);

        var result = await pool.ReleaseAsync(Who, List("PO 2", serial));

        Assert.False(result.Done);
        Assert.Equal(DunsIdentifierScheme.SameIdTaken($"UN778474481-{serial}", "VBC-1"), Assert.Single(result.Problems).Reason);
    }

    [Theory]
    [InlineData("VBC-1-r1")]
    [InlineData("VBC-1R1")]
    [InlineData("VBC1-R1")]
    public async Task ASerialReleasedBeforeTheNewPassport_ShouldKeepItsId_AndTheNewPassportTakesTheNextNumber(string serial)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO 1", "VBC-1", serial));
        var old = await CreateAsync(rig, c7, "VBC-1");

        var next = (await NewPassportAsync(rig, old)).Pack!;
        var made = await CreateAsync(rig, c7, serial); // its label may be printed: it stays usable

        Assert.EndsWith("/25S/UN778474481-VBC-1-R2", Id(next), StringComparison.Ordinal);
        Assert.EndsWith($"/25S/UN778474481-{serial}", Id(made), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("VBC-1-r1")]
    [InlineData("VBC-1R1")]
    [InlineData("VBC1-R1")]
    public async Task AReleaseFromBeforeThisChange_ThatCollides_ShouldNotMakeABattery_ByCreationOrBySwap(string serial)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO 1", "VBC-1", "VBC-77"));
        var old = await CreateAsync(rig, c7, "VBC-1");
        var next = (await NewPassportAsync(rig, old)).Pack!; // …-VBC-1-R1 is now taken
        var draft = await CreateAsync(rig, c7, "VBC-77");

        // Released before the release checked battery IDs (as a site's pool may hold), so the release check never saw it.
        await rig.Store.ReleasedIds.InsertOneAsync(new ReleasedBatteryId
        {
            Id = IdentifierForms.Clean(serial), SerialNumber = serial, Identifier = $"{Base}/25S/UN778474481-{serial}",
            ReleaseId = "release-old", Reference = "PO old", Status = ReleasedIdStatuses.Available, ReleasedBy = Who, ReleasedAtUtc = DateTime.UtcNow,
        });

        var created = await Creation(rig).CreateAsync(ChangeSender.Team(Who), NewBattery(c7, serial));
        var swapped = await pool.SwapAsync(Who, draft.Id, serial);

        var refusal = DunsIdentifierScheme.SameIdTaken($"UN778474481-{serial}", "VBC-1");
        Assert.Null(created.Pack);
        Assert.Equal(refusal, created.Problem);
        Assert.Equal(refusal, swapped);
        Assert.Equal((ReleasedIdStatuses.Available, (string?)null), ((await pool.GetAsync(serial))!.Status, (await pool.GetAsync(serial))!.PackId)); // nothing taken
        Assert.EndsWith("/25S/UN778474481-VBC-77", Id((await rig.Store.GetAsync<BatteryPack>(draft.Id))!), StringComparison.Ordinal);
        Assert.Single(await rig.Store.ListAsync<BatteryPack>(), pack => IdentifierForms.ItemKey(Id(pack)) == "UN778474481VBC1R1");
        Assert.Equal(next.Id, (await rig.Store.FindPackByItemIgnoringDashesAsync($"UN778474481-{serial}"))!.Id); // the one battery with that item
    }

    [Theory]
    [InlineData("VBC-1-r1")]
    [InlineData("VBC-1R1")]
    [InlineData("VBC1-R1")]
    public async Task ANewPassport_ShouldSkipAnRNumberWhoseIdAnotherBatteryOrAReleaseHas(string serial)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO 1", "VBC-1", serial, "VBC-1-R2"));
        var old = await CreateAsync(rig, c7, "VBC-1");
        await CreateAsync(rig, c7, serial); // another battery has …-VBC-1-R1 apart from capitals and dashes

        var next = await NewPassportAsync(rig, old);

        // R1 is the other battery's, R2 is released (its label may be printed): the new passport takes R3.
        Assert.True(next.Pack is not null, next.Problem);
        Assert.EndsWith("/25S/UN778474481-VBC-1-R3", Id(next.Pack!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDatabase_ShouldRefuseTheSecondOfTwoBatteriesWithTheSameItem_EvenAtTheSameMoment()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        BatteryPack Pack(string id, string item)
        {
            var pack = new BatteryPack { Id = id, BatchId = c7.BatchId, OperatorId = c7.OperatorId };
            pack.Values["uniqueProductIdentifier"] = new AttributeValue { Value = JsonValue.Create($"{Base}/25S/{item}"), GeneratedAtUtc = DateTime.UtcNow, Source = "system" };
            return pack;
        }

        for (var round = 0; round < 10; round++)
        {
            var both = await Task.WhenAll(
                Task.Run(async () => { try { await rig.Store.InsertAsync(Pack($"pack-a{round}", $"UN778474481-VBC-{round}-R1")); return true; } catch (MongoWriteException exception) when (DppStore.IsDuplicateItem(exception)) { return false; } }),
                Task.Run(async () => { try { await rig.Store.InsertAsync(Pack($"pack-b{round}", $"UN778474481-vbc-{round}r1")); return true; } catch (MongoWriteException exception) when (DppStore.IsDuplicateItem(exception)) { return false; } }));
            Assert.Single(both, inserted => inserted);
        }

        // A swap's write is held to it too: the key goes with the ID in the same update.
        var first = Pack("pack-x", "UN778474481-VBC-90");
        var second = Pack("pack-y", "UN778474481-VBC-91");
        await rig.Store.InsertAsync(first);
        await rig.Store.InsertAsync(second);
        var moved = new Dictionary<string, AttributeValue>(StringComparer.Ordinal) { ["uniqueProductIdentifier"] = new() { Value = JsonValue.Create($"{Base}/25S/UN778474481-vbc90"), GeneratedAtUtc = DateTime.UtcNow, Source = "system" } };
        var exception = await Assert.ThrowsAsync<MongoWriteException>(() => rig.Store.SetValuesAsync<BatteryPack>(second.Id, moved));
        Assert.True(DppStore.IsDuplicateItem(exception));
        Assert.Equal("UN778474481VBC91", (await rig.Store.GetAsync<BatteryPack>(second.Id))!.ItemKey);
    }

    [Fact]
    public async Task TwoRequestsAtOnce_ANewPassportAndABatteryFromTheSameItem_ShouldLeaveOneBatteryWithIt()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        for (var round = 0; round < 6; round++)
        {
            var serial = $"VBC-{round}";
            var twin = $"VBC{round}-r1";
            await pool.ReleaseAsync(Who, List($"PO {round}", serial, twin));
            var old = await CreateAsync(rig, c7, serial);

            var both = await Task.WhenAll(
                Task.Run(() => Creation(rig).CreateAsync(ChangeSender.Team(Who), NewBattery(old, serial) with { FromPool = false, PredecessorPackId = old.Id })),
                Task.Run(() => Creation(rig).CreateAsync(ChangeSender.Team(Who), NewBattery(c7, twin))));

            // Never two batteries with the same item, and never a 500: a refusal in words, or the next R-number.
            Assert.All(both, result => Assert.True(result.Pack is not null || result.Problem is not null));
            var keys = (await rig.Store.ListAsync<BatteryPack>()).Select(pack => IdentifierForms.ItemKey(Id(pack))).Where(key => key.StartsWith($"UN778474481VBC{round}", StringComparison.Ordinal)).ToArray();
            Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public async Task EveryBattery_ShouldHaveItsItemKeyStored_AndOldPacksGetItAtStart_WithoutFailingOnOldCollisions()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO 1", "VBC-5"));
        var made = await CreateAsync(rig, c7, "VBC-5");
        Assert.Equal("UN778474481VBC5", (await rig.Store.GetAsync<BatteryPack>(made.Id))!.ItemKey);

        // Packs from before this change have no key, and two of them may already collide (VBC-6-R1 and VBC-6R1).
        var packs = rig.Database.GetCollection<BsonDocument>(DppStore.Packs);
        await packs.Indexes.DropOneAsync("item_key");
        await packs.UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, Builders<BsonDocument>.Update.Unset("ItemKey"));
        foreach (var (id, item) in new[] { ("pack-old-1", "UN778474481-VBC-6-R1"), ("pack-old-2", "UN778474481-VBC-6R1"), ("pack-old-3", "UN778474481VPC12221") })
        {
            await packs.InsertOneAsync(new BsonDocument
            {
                ["_id"] = id, ["BatchId"] = c7.BatchId, ["OperatorId"] = c7.OperatorId, ["IsDemo"] = false,
                ["Values"] = new BsonDocument { ["uniqueProductIdentifier"] = new BsonDocument { ["Value"] = $"{Base}/25S/{item}", ["GeneratedAtUtc"] = DateTime.UtcNow, ["Source"] = "system" } },
            });
        }

        var collisions = await rig.Store.EnsureIndexesAsync();

        Assert.Equal("UN778474481VBC5", (await rig.Store.GetAsync<BatteryPack>(made.Id))!.ItemKey);
        Assert.Equal("UN778474481VPC12221", (await rig.Store.GetAsync<BatteryPack>("pack-old-3"))!.ItemKey); // before 6d-5: no dash
        Assert.Equal(["pack-old-2"], collisions.Select(collision => collision.PackId)); // the later one is named, the first keeps the key
        Assert.Equal("UN778474481VBC6R1", (await rig.Store.GetAsync<BatteryPack>("pack-old-1"))!.ItemKey);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>("pack-old-2"))!.ItemKey);
        Assert.Contains(await packs.Indexes.List().ToListAsync(), index => index["name"] == "item_key" && index["unique"].AsBoolean);
        var again = await rig.Store.EnsureIndexesAsync(); // a second start names it again, and changes nothing
        Assert.Equal(["pack-old-2"], again.Select(collision => collision.PackId));
        Assert.Equal(("UN778474481VBC6R1", "pack-old-1"), (again[0].ItemKey, again[0].HeldBy));

        // The one without a key is still found by the rule, so nothing new can take its item either.
        await pool.ReleaseAsync(Who, List("PO 2", "VBC-6-r1"));
        Assert.Contains("the same, apart from capitals and dashes", Assert.Single((await pool.ReleaseAsync(Who, List("PO 3", "VBC6r1"))).Problems).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARemovedPassportsId_ShouldNotComeBack_InOtherCapitalsOrDashes()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await rig.Store.AddRemovedAsync(new RemovedPassport { Id = "pack-gone", BatteryId = $"{Base}/25S/UN778474481-VBC-8-R1", Ids = ["pack-gone", $"{Base}/25S/UN778474481-VBC-8-R1", "UN778474481-VBC-8-R1"], RemovedAtUtc = DateTime.UtcNow, RemovedBy = Who });

        foreach (var serial in new[] { "VBC-8-r1", "VBC8R1", "vbc-8r1" })
        {
            Assert.Equal(BatteryIdPool.RemovedId, Assert.Single((await pool.ReleaseAsync(Who, List("PO", serial))).Problems).Reason);
            Assert.NotNull(await rig.Store.FindRemovedAsync($"UN778474481-{serial}"));
        }

        Assert.Null(await rig.Store.FindRemovedAsync("UN778474481-VBC-8-R2"));
    }
}
