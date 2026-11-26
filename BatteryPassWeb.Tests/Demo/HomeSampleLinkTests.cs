using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Redesign R6, the owner's choice 7 (3 Nov 2026): the old home page had a "Search sample battery" link; the home page
/// links a published demo battery again, "Open a sample battery passport", only while demo data exists, never a real one.
/// Finding U06: the sample is DEMO-C7M-0001, the easiest passport to read, and there is none while the demo is switched off.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class HomeSampleLinkTests(MongoFixture mongo)
{
    private static async Task<object?> SampleAsync(DemoRig rig, bool demoOn = true)
    {
        var controller = new HomeController(rig.Services.GetRequiredService<PassportPages>());
        var view = Assert.IsType<ViewResult>(await controller.Index(null, null, rig.Services.GetRequiredService<BatteryPages>(), CancellationToken.None, new BatteryPassWeb.Dpp.Demo.DemoDataSwitch(demoOn)));
        return view.ViewData[HomeController.SampleKey];
    }

    [Fact]
    public async Task TheHomePage_ShouldOfferAPublishedDemoBattery_OnlyWhileThereIsOne()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var before = await SampleAsync(rig);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var after = await SampleAsync(rig);

        Assert.Equal(string.Empty, before);
        Assert.StartsWith("urn:", Assert.IsType<string>(after), StringComparison.Ordinal); // a demo battery's ID
        var c7m = (await rig.Services.GetRequiredService<BatteryPages>().TeamListAsync(null)).Rows.Single(row => row.SerialNumber == "DEMO-C7M-0001");
        Assert.Equal(c7m.Identifier, after); // the easiest passport to read (U06)
        Assert.Equal(string.Empty, await SampleAsync(rig, demoOn: false)); // none while the demo is switched off
    }

    [Fact]
    public void TheHomePage_ShouldLinkTheSample() =>
        Assert.Contains("<a href=\"/dpp/@Uri.EscapeDataString(sample)\">Open a sample battery passport</a>",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Home", "Index.cshtml")), StringComparison.Ordinal);
}
