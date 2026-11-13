using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// A name is taken only by a record of the same kind, real or demo. The owner's real family is called "Core", as one of
/// the demo's families is: real product data may carry a name the demo data uses, and the lists mark the demo ones.
/// Two real records still can't share a name.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RealAndDemoNamesTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;

    [Fact]
    public async Task ARealFamily_ShouldTakeANameTheDemoUses_ButNotAnotherRealFamilysName()
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        using var _ = rig;
        await rig.Demo.SeedAsync(Who);
        var pages = rig.Services.GetRequiredService<ProductDataPages>();
        var demoCore = (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Core");
        Assert.True(demoCore.IsDemo);

        // a new real family, and a copy, may be called "Core" next to the demo's
        Assert.Null(await pages.NameTakenAsync(RecordKind.Family, "Core"));
        var (realId, problem) = await pages.CopyFamilyAsRealAsync(Who, demoCore.Id, "Core");
        Assert.Null(problem);
        var real = (await rig.Store.GetAsync<BatteryFamily>(realId!))!;
        Assert.False(real.IsDemo);
        Assert.Equal("Core", real.Name);

        // from then on the name is taken for real families, whatever its capitals
        Assert.Equal("another battery family is already called core", await pages.NameTakenAsync(RecordKind.Family, "core"));
        // the real family keeps its own name when it is saved again, and can't take another real family's
        Assert.Null(await pages.NameTakenAsync(RecordKind.Family, "Core", exceptId: real.Id));
        var (otherId, _) = await pages.CopyFamilyAsRealAsync(Who, demoCore.Id, "Core Two");
        Assert.NotNull(await pages.NameTakenAsync(RecordKind.Family, "Core Two", exceptId: real.Id));
        Assert.NotNull(otherId);

        // the demo family, renamed, is compared with the demo's names only
        var demoCompact = (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact" && family.IsDemo);
        Assert.NotNull(await pages.NameTakenAsync(RecordKind.Family, "Core", exceptId: demoCompact.Id));
        Assert.Null(await pages.NameTakenAsync(RecordKind.Family, "Core Two", exceptId: demoCompact.Id));
    }
}
