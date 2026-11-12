using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class PassportVersionStoreTests(MongoFixture mongo)
{
    [Fact]
    public async Task Version_ShouldRoundTripAndStillVerify()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var version = Version(seed, number: 1, status: DppStatuses.Active);

        await store.InsertAsync(version);
        var stored = await store.GetVersionByPassportIdAsync(version.DigitalProductPassportId);

        Assert.NotNull(stored);
        Assert.True(PassportSigner.Verify(stored.StaticCore, stored.Proof)); // decimals come back in a form that hashes the same
        Assert.Equal(version.Proof.Hash, stored.Proof.Hash);
        Assert.Equal(version.PublishedAtUtc, stored.PublishedAtUtc);
    }

    [Fact]
    public async Task ActiveVersion_ShouldBeTheNewestActiveOne()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        Assert.Equal(0, await store.GetLatestVersionNumberAsync(seed.Sources.Pack.Id));
        await store.InsertAsync(Version(seed, 1, DppStatuses.Archived));
        await store.InsertAsync(Version(seed, 2, DppStatuses.Active));

        var active = await store.GetActiveVersionAsync(seed.Sources.Pack.Id);

        Assert.Equal(2, active?.Number);
        Assert.Equal(2, await store.GetLatestVersionNumberAsync(seed.Sources.Pack.Id));
        Assert.Equal([1, 2], (await store.ListVersionsAsync(seed.Sources.Pack.Id)).Select(v => v.Number));
    }

    [Fact]
    public async Task PassportIdAndVersionNumber_ShouldBeUnique()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var first = Version(seed, 1, DppStatuses.Active);
        await store.InsertAsync(first);

        var samePassportId = Version(seed, 2, DppStatuses.Active, first.DigitalProductPassportId);
        var sameNumber = Version(seed, 1, DppStatuses.Active);

        Assert.Equal(ServerErrorCategory.DuplicateKey, (await Assert.ThrowsAsync<MongoWriteException>(() => store.InsertAsync(samePassportId))).WriteError.Category);
        Assert.Equal(ServerErrorCategory.DuplicateKey, (await Assert.ThrowsAsync<MongoWriteException>(() => store.InsertAsync(sameNumber))).WriteError.Category);
    }

    [Fact]
    public async Task ArchiveOthers_ShouldKeepOnlyTheGivenVersionActive()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var older = Version(seed, 1, DppStatuses.Active);
        var newer = Version(seed, 2, DppStatuses.Active);
        await store.InsertAsync(older);
        await store.InsertAsync(newer);

        await store.ArchiveOtherVersionsAsync(seed.Sources.Pack.Id, newer.Id);

        var versions = await store.ListVersionsAsync(seed.Sources.Pack.Id);
        Assert.Equal([DppStatuses.Archived, DppStatuses.Active], versions.Select(v => v.DppStatus));
    }

    [Fact]
    public async Task LastUpdated_ShouldBeSettable()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var version = Version(seed, 1, DppStatuses.Active);
        await store.InsertAsync(version);
        var later = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        await store.SetVersionLastUpdatedAsync(version.Id, later);

        Assert.Equal(later, (await store.GetActiveVersionAsync(seed.Sources.Pack.Id))?.LastUpdatedUtc);
    }

    [Fact]
    public async Task PacksUsingARecord_ShouldBeFoundByModelSiteBatchAndOperator()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var packId = seed.Sources.Pack.Id;

        Assert.Equal([packId], await store.FindPackIdsUsingAsync(modelId: seed.Sources.Model.Id));
        Assert.Equal([packId], await store.FindPackIdsUsingAsync(siteId: seed.Sources.Site.Id));
        Assert.Equal([packId], await store.FindPackIdsUsingAsync(batchId: seed.Sources.Batch.Id));
        Assert.Equal([packId], await store.FindPackIdsUsingAsync(operatorId: seed.Sources.Operator.Id));
        Assert.Empty(await store.FindPackIdsUsingAsync(modelId: "no-such-model"));
    }

    private static PassportVersion Version(SeedPassport seed, int number, string status, string? passportId = null)
    {
        var core = PassportStaticCore.Extract(PassportAssembler.Assemble(seed.Sources, seed.Header));
        return new PassportVersion
        {
            Id = $"version-{Guid.NewGuid():N}",
            PackId = seed.Sources.Pack.Id,
            Number = number,
            DigitalProductPassportId = passportId ?? $"urn:x-battery-pass:TEST-DPP-{Guid.NewGuid():N}",
            DppStatus = status,
            PublishedAtUtc = DppStoreTesting.Now.UtcDateTime,
            LastUpdatedUtc = DppStoreTesting.Now.UtcDateTime,
            PublishedBy = "team@example.com",
            StaticCore = core,
            Proof = new PassportSigner(SigningTesting.Keys).Sign(core, DppStoreTesting.Now.UtcDateTime),
        };
    }
}
