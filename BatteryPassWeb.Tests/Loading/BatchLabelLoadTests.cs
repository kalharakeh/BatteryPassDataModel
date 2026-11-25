using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package R1, G14: a new batch's label that another batch of the same model already has is a problem the check names,
/// at its cell, not a load that stops part way.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatchLabelLoadTests(MongoFixture mongo)
{
    [Fact]
    public async Task ANewBatchWithALabelTheModelsOtherBatchHas_ShouldBeACheckProblem_NotAStopPartWay()
    {
        var rig = await EmptyAsync(mongo);
        var first = await rig.LoadAsync(Core().Set(BatchSheet, "batch|batchNumber", FirstBatch, "CORE800-2026"));
        Assert.True(first.Loaded, first.Message);

        var nextYear = Core()
            .Set(BatchSheet, "batch|Year", FirstBatch, "2027")
            .Set(BatchSheet, "batch|batchNumber", FirstBatch, "core800-2026");
        var check = await rig.CheckAsync(nextYear);

        var problem = Assert.Single(check.Texts(), text => text.Contains(BatchLabels.Taken, StringComparison.Ordinal));
        Assert.Contains("core800-2026", problem, StringComparison.Ordinal);
        Assert.False(check.CanLoad);

        var fine = await rig.CheckAsync(Core().Set(BatchSheet, "batch|Year", FirstBatch, "2027").Set(BatchSheet, "batch|batchNumber", FirstBatch, "CORE800-2027"));
        Assert.DoesNotContain(fine.Texts(), text => text.Contains(BatchLabels.Taken, StringComparison.Ordinal));
        Assert.Equal(3, (await rig.Store.ListAsync<ProductionBatch>()).Count);
    }
}
