using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round four, 2: every set of the marker writes a fresh token; a clear removes the marker only while it still holds the
/// token the clearing attempt read before its data, so a change marked meanwhile stays marked. The first time is kept.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkerClearTests(MongoFixture mongo)
{
    [Fact]
    public async Task AClear_ShouldNotRemoveAMarkerSetAfterTheAttemptStarted()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var first = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], first);
        var seen = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingToken; // the attempt starts

        await rig.Store.MarkChangesWaitingAsync([rig.PackId], first.AddHours(1)); // a change held back meanwhile

        Assert.False(await rig.Store.ClearChangesWaitingAsync(rig.PackId, seen));
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal(first, stored.ChangesWaitingSinceUtc); // the first time is kept
        Assert.NotEqual(seen, stored.ChangesWaitingToken);

        Assert.True(await rig.Store.ClearChangesWaitingAsync(rig.PackId, stored.ChangesWaitingToken));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
    }
}
