using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round five: a slow publisher's archive step can't archive a faster publisher's newer version. Replayed through the
/// store in the order the review found: S inserts n+1; F reads n+1 active, inserts n+2 and archives; then S archives.
/// The battery must end with n+2 active, never with no active version.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ArchiveOrderTests(MongoFixture mongo)
{
    [Fact]
    public async Task ASlowPublishersArchive_ShouldLeaveTheFasterOnesNewerVersionActive()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var packId = seed.Sources.Pack.Id;
        await store.InsertAsync(Version(seed, 1)); // version n, active

        var slow = Version(seed, 2);
        await store.InsertAsync(slow); // S inserts n+1 and is held up before its archive step

        Assert.Equal(2, (await store.GetActiveVersionAsync(packId))!.Number); // F reads n+1 as active
        var fast = Version(seed, 3);
        await store.InsertAsync(fast); // F inserts n+2
        await store.ArchiveOtherVersionsAsync(packId, fast.Id, fast.Number); // F archives n and n+1

        await store.ArchiveOtherVersionsAsync(packId, slow.Id, slow.Number); // then S's archive step runs

        var active = await store.GetActiveVersionAsync(packId);
        Assert.Equal(3, active?.Number);
        Assert.Equal([DppStatuses.Archived, DppStatuses.Archived, DppStatuses.Active], (await store.ListVersionsAsync(packId)).OrderBy(v => v.Number).Select(v => v.DppStatus));
    }

    private static PassportVersion Version(SeedPassport seed, int number)
    {
        var core = PassportStaticCore.Extract(PassportAssembler.Assemble(seed.Sources, seed.Header));
        return new PassportVersion
        {
            Id = $"version-{Guid.NewGuid():N}",
            PackId = seed.Sources.Pack.Id,
            Number = number,
            DigitalProductPassportId = $"urn:x-battery-pass:TEST-DPP-{Guid.NewGuid():N}",
            DppStatus = DppStatuses.Active,
            PublishedAtUtc = DppStoreTesting.Now.UtcDateTime,
            LastUpdatedUtc = DppStoreTesting.Now.UtcDateTime,
            PublishedBy = "team@example.com",
            StaticCore = core,
            Proof = new PassportSigner(SigningTesting.Keys).Sign(core, DppStoreTesting.Now.UtcDateTime),
        };
    }
}
