using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class DppStoreTests(MongoFixture mongo)
{
    [Fact]
    public async Task StoredSeed_ShouldLoadBackIntoTheSamePassport()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var sources = await store.LoadSourcesAsync(seed.Sources.Pack.Id);

        Assert.NotNull(sources);
        var passport = PassportAssembler.Assemble(sources, seed.Header);
        Assert.True(JsonNode.DeepEquals(PassportAssembler.Assemble(seed.Sources, seed.Header), passport));
        Assert.Empty(DppStoreTesting.Schemas.CheckPassport(passport, feb2027Variant: false));
    }

    [Fact]
    public async Task EnsureIndexes_ShouldBeSafeToRunAgain()
    {
        var (store, _, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        await store.EnsureIndexesAsync();
        await store.EnsureIndexesAsync();
    }

    [Fact]
    public async Task UniqueProductIdentifier_ShouldBeUnique()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var copy = CopyOfSeedPack(seed, "pack-copy");

        var error = await Assert.ThrowsAsync<MongoWriteException>(() => store.InsertAsync(copy));

        Assert.Equal(ServerErrorCategory.DuplicateKey, error.WriteError.Category);
    }

    [Fact]
    public async Task Batch_ShouldBeUniquePerModelSiteAndYear()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var batch = seed.Sources.Batch;
        var same = new ProductionBatch { Id = "batch-again", ModelId = batch.ModelId, SiteId = batch.SiteId, Year = batch.Year };

        var error = await Assert.ThrowsAsync<MongoWriteException>(() => store.InsertAsync(same));

        Assert.Equal(ServerErrorCategory.DuplicateKey, error.WriteError.Category);
        await store.InsertAsync(new ProductionBatch { Id = "batch-next-year", ModelId = batch.ModelId, SiteId = batch.SiteId, Year = batch.Year + 1 });
    }

    [Fact]
    public async Task SerialNumber_ShouldFindEveryPackWithIt()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var second = CopyOfSeedPack(seed, "pack-second");
        second.Values["uniqueProductIdentifier"] = DppStoreTesting.Value(JsonValue.Create("urn:x-battery-pass:TEST-second")!);
        await store.InsertAsync(second);

        var packs = await store.FindPacksBySerialNumberAsync("VPC8C05-6788");

        Assert.Equal(["pack-VPC8C05-6788", "pack-second"], packs.Select(p => p.Id).Order(StringComparer.Ordinal));
        Assert.Empty(await store.FindPacksBySerialNumberAsync("no-such-serial"));
    }

    [Fact]
    public async Task UniqueProductIdentifier_ShouldFindThePack()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var pack = await store.FindPackByUniqueProductIdentifierAsync("urn:test:battery:VPC8C05-6788");

        Assert.Equal(seed.Sources.Pack.Id, pack?.Id);
        Assert.Null(await store.FindPackByUniqueProductIdentifierAsync("urn:test:battery:none"));
    }

    [Fact]
    public async Task SetValues_ShouldChangeOnlyTheGivenValues()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var packId = seed.Sources.Pack.Id;

        var changed = await store.SetValuesAsync<BatteryPack>(packId, new Dictionary<string, AttributeValue>
        {
            ["stateOfChargeSoC"] = DppStoreTesting.Value(JsonNode.Parse("""{"percentUnit":"%","percentageValue":55}""")!),
        });

        Assert.True(changed);
        var pack = await store.GetAsync<BatteryPack>(packId);
        Assert.Equal(55, pack!.Values["stateOfChargeSoC"].Value["percentageValue"]!.GetValue<int>());
        Assert.Equal("VPC8C05-6788", pack.Values["batterySerialNumber"].Value.GetValue<string>());
        Assert.Equal(seed.Sources.Pack.Values.Count, pack.Values.Count);
        Assert.False(await store.SetValuesAsync<BatteryPack>("no-such-pack", new Dictionary<string, AttributeValue>
        {
            ["stateOfChargeSoC"] = DppStoreTesting.Value(JsonNode.Parse("""{"percentUnit":"%","percentageValue":1}""")!),
        }));
    }

    [Fact]
    public async Task MissingRecords_ShouldReadAsNothing()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var orphan = CopyOfSeedPack(seed, "pack-orphan");
        orphan.Values.Remove("uniqueProductIdentifier");
        var orphanWithMissingBatch = new BatteryPack { Id = orphan.Id, BatchId = "no-such-batch", OperatorId = orphan.OperatorId, Values = orphan.Values };
        await store.InsertAsync(orphanWithMissingBatch);

        Assert.Null(await store.GetAsync<BatteryModel>("no-such-model"));
        Assert.Null(await store.LoadSourcesAsync("no-such-pack"));
        Assert.Null(await store.LoadSourcesAsync("pack-orphan"));
    }

    [Fact]
    public async Task WithoutADatabase_ReadsShouldReturnNothingAndWritesShouldThrow()
    {
        var store = new DppStore(new MongoContext(Options.Create(new BatteryPassOptions())));

        Assert.False(store.IsAvailable);
        await store.EnsureIndexesAsync();
        Assert.Null(await store.GetAsync<BatteryPack>("pack-1"));
        Assert.Empty(await store.FindPacksBySerialNumberAsync("VPC8C05-6788"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InsertAsync(new BatteryFamily { Id = "f", Name = "F" }));
    }

    private static BatteryPack CopyOfSeedPack(SeedPassport seed, string id) => new()
    {
        Id = id,
        BatchId = seed.Sources.Pack.BatchId,
        OperatorId = seed.Sources.Pack.OperatorId,
        ClusterId = seed.Sources.Pack.ClusterId,
        Values = new Dictionary<string, AttributeValue>(seed.Sources.Pack.Values, StringComparer.Ordinal),
    };
}
