using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Redesign R6 (the review of the built R5, the comparison with the old app): a battery's History link opened the whole
/// audit log searched by an internal ID, with nothing saying whose history it was; it now says "History of battery …" and
/// leads back to the battery page. The old app's "Manage passports" address (/admin/passports) leads to the batteries.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryHistoryLinkTests(MongoFixture mongo)
{
    [Fact]
    public async Task ABatterysHistory_ShouldSayWhoseItIs_AndLeadBack()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pack = (await rig.Store.ListAsync<BatteryPack>()).First();
        var serial = pack.Values["batterySerialNumber"].Value!.GetValue<string>();
        var controller = DemoCardTests.Controller(rig);
        var lines = rig.Services.GetRequiredService<AuditLines>();

        var history = Assert.IsType<ViewResult>(await controller.AuditLog(pack.Id, null, null, null, null, null, null, null, lines, CancellationToken.None));
        var battery = Assert.IsType<AuditBattery>(history.ViewData[AdminController.AuditBatteryKey]);
        var whole = Assert.IsType<ViewResult>(await DemoCardTests.Controller(rig).AuditLog("Repaired", null, null, null, null, null, null, null, lines, CancellationToken.None));

        Assert.Equal((serial, BatteryPages.BatteryHref(pack.Id)), (battery.SerialNumber, battery.Href));
        Assert.Empty(battery.OtherPassports ?? []); // one passport only; a battery placed on the market again links its others (R6 T19)
        Assert.False(whole.ViewData.ContainsKey(AdminController.AuditBatteryKey)); // an ordinary search names no battery
    }

    [Fact]
    public async Task TheOldManagePassportsAddress_ShouldLeadToTheBatteries()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        Assert.Equal("/admin/batteries", Assert.IsType<RedirectResult>(DemoCardTests.Controller(rig).Passports()).Url);
    }

    [Fact]
    public void TheAuditLog_ShouldNameTheBatteryAndTheWaysBack() =>
        Assert.Contains("History of battery <strong>@battery.SerialNumber</strong> · <a href=\"@battery.Href\">Back to the battery page</a> · <a href=\"/admin/audit\">The whole audit log</a>",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "AuditLog.cshtml")), StringComparison.Ordinal);
}
