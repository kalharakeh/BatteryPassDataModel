using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding W12: a measurement time that lies ahead of the clock (five minutes allowed) is refused for every writer: the
/// team's and the cluster admin's forms through the one save path, as uploads and the API already did.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MeasuredAtFutureTests(MongoFixture mongo)
{
    private static ChangeSet Live(PublicationRig rig, DateTime? measured) =>
        new(TargetKind.Pack, rig.PackId, BatteryApiTests.Values(("stateOfChargeSoC", 64)), "form", measured);

    [Fact]
    public void TheRule_ShouldAllowFiveMinutesForClocks()
    {
        var now = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(MeasuredAtRule.InTheFuture(now.AddMinutes(5), now));
        Assert.True(MeasuredAtRule.InTheFuture(now.AddMinutes(5).AddSeconds(1), now));
        Assert.False(MeasuredAtRule.InTheFuture(now.AddYears(-1), now));
    }

    [Fact]
    public async Task ASaveWithATimeNextYear_ShouldBeRefusedAndStoreNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var before = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>();

        foreach (var preview in new[] { true, false })
        {
            var result = await rig.Pipeline.SaveAsync(ChangeSender.Team("team@example.test"), Live(rig, rig.Clock.Now.UtcDateTime.AddYears(1)), preview);

            Assert.False(result.Saved);
            var key = Assert.Single(result.Keys);
            Assert.Equal(KeyOutcome.Rejected, key.Outcome);
            Assert.Equal(MeasuredAtRule.Words, key.Message);
        }

        Assert.Equal(before, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task ASaveWithATimeAMinuteAhead_ShouldBeSaved()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(ChangeSender.Team("team@example.test"), Live(rig, rig.Clock.Now.UtcDateTime.AddMinutes(1)), preview: false);

        Assert.True(result.Saved);
    }

    [Fact]
    public async Task ASaveWithNoTime_ShouldBeSavedAsBefore()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        Assert.True((await rig.Pipeline.SaveAsync(ChangeSender.Team("team@example.test"), Live(rig, null), preview: false)).Saved);
    }
}
