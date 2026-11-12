using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Redesign R6, the owner's choice 7 (3 Nov 2026): the old home page had a "Search sample battery" link; the home page
/// links a published demo battery again, "Open a sample battery passport", only while demo data exists, never a real one.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class HomeSampleLinkTests(MongoFixture mongo)
{
    private static async Task<object?> SampleAsync(DemoRig rig)
    {
        var controller = new HomeController(rig.Services.GetRequiredService<PassportPages>());
        var view = Assert.IsType<ViewResult>(await controller.Index(null, null, rig.Services.GetRequiredService<BatteryPages>(), CancellationToken.None));
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
        Assert.Equal(await rig.Services.GetRequiredService<BatteryPages>().ExampleBatteryIdAsync(), after); // the API help's sample
    }

    [Fact]
    public void TheHomePage_ShouldLinkTheSample() =>
        Assert.Contains("<a href=\"/dpp/@Uri.EscapeDataString(sample)\">Open a sample battery passport</a>",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Home", "Index.cshtml")), StringComparison.Ordinal);
}
