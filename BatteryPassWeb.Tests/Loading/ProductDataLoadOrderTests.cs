using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, round two, the order of saving: the load first makes and fills every new record, which no reader sees,
/// and only then changes the records that were already there. A stop while it makes the new records leaves nothing; a
/// stop after that keeps what it saved, says so, and a second load of the same workbook finishes the rest.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadOrderTests(MongoFixture mongo)
{
    private static string Mass(BatteryModel model) => model.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString();

    private static FilledWorkbook Changed() => Core()
        .SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800s", 455)
        .SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core700", 440)
        .SetNumber(BatchSheet, "batch|Year", "Batch of Core700", 2027);

    private async Task<LoadRig> LoadedAsync()
    {
        var rig = await EmptyAsync(mongo);
        Assert.True((await rig.LoadAsync(Core())).Loaded);
        return rig;
    }

    [Fact]
    public async Task AStopWhileMakingTheNewRecords_ShouldChangeNothingThatWasThere()
    {
        var rig = await LoadedAsync();
        rig.Loader.StepHook = step => step == "batch" ? throw new InvalidOperationException("the database went away") : Task.CompletedTask;

        var result = await rig.LoadAsync(Changed());

        Assert.False(result.Loaded);
        Assert.EndsWith("The records it had made were removed again. Nothing was saved. Load again when the fault is gone.", result.Message, StringComparison.Ordinal);
        Assert.Equal("470", Mass((await rig.Store.ListAsync<BatteryModel>()).Single(model => model.Name == "Core800s")));
        Assert.Equal(3, (await rig.Store.ListAsync<ProductionBatch>()).Count);
    }

    [Fact]
    public async Task AStopWhileChangingWhatWasThere_ShouldKeepWhatItSaved_AndASecondLoadFinishesIt()
    {
        var rig = await LoadedAsync();
        var models = 0;
        rig.Loader.StepHook = step => step == "model" && ++models == 1 ? throw new InvalidOperationException("the database went away") : Task.CompletedTask;

        var stopped = await rig.LoadAsync(Changed());

        Assert.False(stopped.Loaded);
        Assert.Equal("The load stopped part way: an error stopped it (InvalidOperationException). The 1 new record it made stays, complete. It had started to save changes to: battery model Core800s. Load again to finish; nothing has to be undone by hand.", stopped.Message);
        Assert.Equal(4, (await rig.Store.ListAsync<ProductionBatch>()).Count); // the new 2027 batch was made first
        Assert.Equal("455", Mass((await rig.Store.ListAsync<BatteryModel>()).Single(model => model.Name == "Core800s"))); // saved when it stopped
        Assert.Equal("470", Mass((await rig.Store.ListAsync<BatteryModel>()).Single(model => model.Name == "Core700")));

        rig.Loader.StepHook = null;
        var again = await rig.LoadAsync(Changed());

        Assert.True(again.Loaded, again.Message);
        Assert.Equal("Product data loaded: 1 record changed, of 1 battery family, 3 battery models, 3 batches, 1 manufacturing site and 1 economic operator.", again.Message);
        Assert.Equal("440", Mass((await rig.Store.ListAsync<BatteryModel>()).Single(model => model.Name == "Core700")));
        Assert.Equal(4, (await rig.Store.ListAsync<ProductionBatch>()).Count);
        Assert.False((await rig.CheckAsync(Changed())).HasChanges);
    }
}
