using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, round two, point 5: the load saves a model's answers before its values, so the check judges a removal
/// by the answers in the workbook, not by those stored now. A workbook that answers yes and removes the value that yes
/// asks for, on a model with published passports, is a problem line and stops nothing part way.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadAnswersTests(MongoFixture mongo)
{
    private const string Cycles = "model|expectedLifetimeNumberOfChargeDischargeCycles";

    /// <summary>The workbook for the seed battery's real records, its model answering the cycling question as given.</summary>
    private static FilledWorkbook ForTheSeedModel(string cycling) => FilledWorkbook.Empty()
        .Set(FamilySheet, "family|Name", "Core", "Test battery family")
        .Set(ModelSheet, "model|Name", "Core800", "EXB-NMC-100")
        .Clear(ModelSheet, "model|Name", "Core800s")
        .Clear(ModelSheet, "model|Name", "Core700")
        .Set(ModelSheet, "model|MadeForCyclingUse", "Core800", cycling)
        .Set(ModelSheet, Cycles, "Core800", "none");

    private async Task<LoadRig> PublishedAsync(bool cyclingStored)
    {
        var rig = Around(await PublicationTesting.PublishedRigAsync(mongo));
        await rig.Store.SetModelAnswersAsync("model-test", true, cyclingStored, true);
        return rig;
    }

    [Fact]
    public async Task YesAndNoneForTheValueYesAsksFor_ShouldBeAProblem_NotAStopPartWay()
    {
        var rig = await PublishedAsync(cyclingStored: false);

        var result = await rig.LoadAsync(ForTheSeedModel("Yes"), []);

        Assert.False(result.Loaded);
        SameLines(
            ["Models, Expected lifetime: number of charge-discharge cycles, Core800: The published passports need this value with the answers in this workbook, so it can't be removed. Change it instead, or answer the question as before"],
            result.Check.Texts());
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;
        Assert.False(model.MadeForCyclingUse); // nothing saved
        Assert.True(model.Values.ContainsKey("expectedLifetimeNumberOfChargeDischargeCycles"));
        Assert.Empty(await rig.AuditAsync(BatteryPassWeb.Dpp.Loading.ProductDataLoader.StoppedEvent));
    }

    [Fact]
    public async Task NoAndNoneForTheValueYesAskedFor_ShouldLoad_AsTheNewAnswerNoLongerNeedsIt()
    {
        var rig = await PublishedAsync(cyclingStored: true);

        var result = await rig.LoadAsync(ForTheSeedModel("No"), []);

        Assert.True(result.Loaded, result.Message);
        Assert.Empty(result.Check.Texts());
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;
        Assert.False(model.MadeForCyclingUse);
        Assert.False(model.Values.ContainsKey("expectedLifetimeNumberOfChargeDischargeCycles"));
    }

    [Fact]
    public async Task YesKeptAndNone_ShouldStillBeRefusedInTheFormsOwnWords()
    {
        var rig = await PublishedAsync(cyclingStored: true);

        var check = await rig.CheckAsync(ForTheSeedModel("Yes"), []);

        SameLines(["Models, Expected lifetime: number of charge-discharge cycles, Core800: The published passports need this value, so it can't be removed. Change it instead"], check.Texts());
    }
}
