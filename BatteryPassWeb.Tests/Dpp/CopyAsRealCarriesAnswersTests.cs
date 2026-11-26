using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Where the Monday package (decision 66) meets the second fix wave (R01): a model copied as real product data comes with
/// the demo model's three answers. Without them every battery of the copy would wait for the questions before it could
/// be published, although the copy holds every value the answers ask for.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class CopyAsRealCarriesAnswersTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;

    [Fact]
    public async Task ACopiedModel_ShouldComeWithTheDemoModelsThreeAnswers()
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        using var _ = rig;
        await rig.Demo.SeedAsync(Who);
        var compact = (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact");
        var demoModels = await rig.Store.ListModelsAsync(compact.Id);
        Assert.All(demoModels, model => Assert.NotNull(model.MadeForCyclingUse)); // the demo models are answered

        var (familyId, problem) = await rig.Services.GetRequiredService<ProductDataPages>().CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");

        Assert.Null(problem);
        var copies = await rig.Store.ListModelsAsync(familyId!);
        Assert.Equal(demoModels.Count, copies.Count);
        foreach (var source in demoModels)
        {
            var copy = copies.Single(model => model.Name == source.Name);
            Assert.False(copy.IsDemo);
            Assert.Equal(source.HasCommercialWarranty, copy.HasCommercialWarranty);
            Assert.Equal(source.MadeForCyclingUse, copy.MadeForCyclingUse);
            Assert.Equal(source.CadmiumOrLeadAboveLimit, copy.CadmiumOrLeadAboveLimit);
        }
    }
}
