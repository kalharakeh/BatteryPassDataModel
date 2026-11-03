using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Mvc;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Redesign R6, the owner's item 12: a battery's History page links its other passports' histories when it was placed
/// on the market again (the demo DEMO-C13M-0001).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ChainHistoryLinksTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheNewestPassportsHistory_ShouldLinkTheEarlierOnes()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var packs = (await rig.Store.ListAsync<BatteryPack>()).Where(pack => pack.Values["batterySerialNumber"].Value!.GetValue<string>() == "DEMO-C13M-0001").ToArray();
        var newest = packs.Single(pack => pack.SuccessorOf is not null && packs.All(other => other.SuccessorOf != pack.Id));
        var lines = rig.Services.GetRequiredService<AuditLines>();

        var view = Assert.IsType<ViewResult>(await DemoCardTests.Controller(rig).AuditLog(newest.Id, null, null, null, null, null, null, null, lines, CancellationToken.None));
        var battery = Assert.IsType<AuditBattery>(view.ViewData[AdminController.AuditBatteryKey]);

        Assert.Equal(packs.Length - 1, battery.OtherPassports!.Count);
        Assert.Equal("Passport 1", battery.OtherPassports[0].Label);
        Assert.Equal($"/admin/audit?q={Uri.EscapeDataString(newest.SuccessorOf!)}", battery.OtherPassports[^1].Href);
    }
}
