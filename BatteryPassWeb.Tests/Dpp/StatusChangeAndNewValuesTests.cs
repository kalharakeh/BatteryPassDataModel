using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, round two (review L6 and L10): C1's rule as it stands. A published battery whose model still lacks one of
/// the EU's new values (data from v3.8) gets no status change until it is filled in, and the refusal names the value.
/// Signed off by the manager: the go-live step loads the filled workbook before any status change.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class StatusChangeAndNewValuesTests(MongoFixture mongo)
{
    [Fact]
    public async Task AStatusChange_ShouldBeRefusedWhileTheModelLacksANewValue_AndGoThroughOnceItIsFilled()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var test = AttributeCatalog.FindByProperty(AttributeCatalog.NotInUseTemperatureRangeTest)!;
        var kept = (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values[test.Property];
        await rig.Store.SetValuesAsync<BatteryModel>("model-test", new Dictionary<string, AttributeValue>(), [test.Property]); // as v3.8 left it

        var refused = await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig));

        Assert.Null(refused.Version);
        Assert.Contains("E10 Test of the idle temperature range", refused.Problem, StringComparison.Ordinal);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);

        await rig.Store.SetValuesAsync<BatteryModel>("model-test", new Dictionary<string, AttributeValue> { [test.Property] = kept });
        var done = await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig));

        Assert.Null(done.Problem);
        Assert.Equal(2, done.Version!.Number);
    }

    private static async Task<StatusChange> RepairAsync(PublicationRig rig)
    {
        var values = await StatusChangeTests.CurrentAsync(rig);
        values["capacityFade"] = JsonValue.Create(6m);
        return new StatusChange(StatusEvent.Repaired, values, "2026-10-20", "Module 4 replaced after a coolant leak.");
    }
}
