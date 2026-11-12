using System.Security.Claims;
using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 67: the release page can release test IDs (they contain TEST, are never real battery IDs, aren't counted, and
/// go when the demo data is cleared). A test ID can be used on demo or real product data, and makes a demo battery.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class TestIdReleaseTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;

    private static async Task<(DemoRig Rig, BatteryIdPool Pool)> SeededAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(Who);
        return (rig, rig.Services.GetRequiredService<BatteryIdPool>());
    }

    private static BatteryIdsController Controller(DemoRig rig, bool test)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, Who), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)], "test")),
        };
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Form = new FormCollection(test ? new Dictionary<string, StringValues> { ["isTest"] = "true" } : []);
        return new BatteryIdsController(rig.Services.GetRequiredService<BatteryIdPool>(), rig.Store)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private static Dictionary<string, JsonNode?> Values(string serial)
    {
        var values = DemoCatalogue.Load().Packs.First(pack => pack.Publish).Values.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone(), StringComparer.Ordinal);
        values["batterySerialNumber"] = serial;
        return values;
    }

    [Fact]
    public async Task TheTick_ShouldReleaseTestIds_ThatAreNotCounted()
    {
        var (rig, pool) = await SeededAsync(mongo);
        using var _ = rig;
        var counts = await pool.RealCountsAsync();
        var controller = Controller(rig, test: true);

        Assert.IsType<RedirectResult>(await controller.ReleaseIds("Try-out", null, [], [], [], "TRY-0001\nTRY-0002", CancellationToken.None));

        var released = await pool.GetAsync("TRY-0001");
        Assert.True(released!.IsDemo);
        Assert.Contains("-TEST-", released.Identifier, StringComparison.Ordinal);
        Assert.Equal(counts, await pool.RealCountsAsync());
        Assert.Equal("Released 2 test battery IDs. They contain TEST, aren't counted, and go when the demo data is reset or cleared.", controller.TempData[ProductDataController.StatusKey]);
    }

    [Fact]
    public async Task WithoutTheTick_ARelease_ShouldStayReal()
    {
        var (rig, pool) = await SeededAsync(mongo);
        using var _ = rig;

        await Controller(rig, test: false).ReleaseIds("PO 1", null, [], [], [], "VPC-12221", CancellationToken.None);

        Assert.False((await pool.GetAsync("VPC-12221"))!.IsDemo);
    }

    [Fact]
    public async Task ASerialNumber_ShouldNotBeReleasedTwice_AcrossTheTwoKinds()
    {
        var (rig, pool) = await SeededAsync(mongo);
        using var _ = rig;
        await pool.ReleaseAsync(Who, new ReleaseRequest("PO 1", null, [], "VPC-12221"));
        await pool.ReleaseAsync(Who, new ReleaseRequest("Try-out", null, [], "TRY-0001", IsDemo: true));

        var asTest = await pool.ReleaseAsync(Who, new ReleaseRequest("Try-out 2", null, [], "VPC-12221", IsDemo: true));
        var asReal = await pool.ReleaseAsync(Who, new ReleaseRequest("PO 2", null, [], "TRY-0001"));

        Assert.False(asTest.Done);
        Assert.StartsWith("already released", Assert.Single(asTest.Problems).Reason, StringComparison.Ordinal);
        Assert.False(asReal.Done);
        Assert.StartsWith("already released", Assert.Single(asReal.Problems).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestIds_ShouldMakeDemoBatteries_OnDemoAndOnRealProductData_ThatPublish_AndGoWithTheDemoData()
    {
        var (rig, pool) = await SeededAsync(mongo);
        using var _ = rig;
        var pages = rig.Services.GetRequiredService<ProductDataPages>();
        var compact = (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact");
        var (familyId, _) = await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");
        var demoOperator = (await rig.Store.ListAsync<EconomicOperator>()).Single(item => item.Name == "Scania Industrial Batteries (demo)");
        var (operatorId, _) = await pages.CopyOperatorAsRealAsync(Who, demoOperator.Id, "Scania Industrial Batteries");
        var realBatch = (await rig.Store.ListBatchesAsync((await rig.Store.ListModelsAsync(familyId!)).Single(model => model.Name == "Compact 7M").Id)).Single();
        var demoBatch = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single().BatchId;
        await Controller(rig, test: true).ReleaseIds("Try-out", null, [], [], [], "TRY-0001\nTRY-0002", CancellationToken.None);
        var counts = await pool.RealCountsAsync();
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        var publication = rig.Services.GetRequiredService<PublicationService>();

        var onDemo = await creation.CreateAsync(ChangeSender.Team(Who), new NewPack(demoBatch, demoOperator.Id, null, Values("TRY-0001"), FromPool: true));
        var onReal = await creation.CreateAsync(ChangeSender.Team(Who), new NewPack(realBatch.Id, operatorId!, null, Values("TRY-0002"), FromPool: true));

        Assert.Null(onDemo.Problem);
        Assert.Null(onReal.Problem);
        foreach (var pack in new[] { onDemo.Pack!, onReal.Pack! })
        {
            Assert.True(pack.IsDemo); // a demo battery in every sense: marked, shown as a test ID, outside the counts
            Assert.Contains("-TEST-", pack.Values["uniqueProductIdentifier"].Value.GetValue<string>(), StringComparison.Ordinal);
            var published = await publication.PublishAsync(ChangeSender.Team(Who), pack.Id);
            Assert.True(published.Version is not null, published.Problem ?? string.Join("; ", published.Report?.Blocking ?? []));
        }

        Assert.Equal(counts, await pool.RealCountsAsync());

        var cleared = await rig.Demo.ClearAsync(Who);
        Assert.Empty(cleared.Notes);
        Assert.Null(await pool.GetAsync("TRY-0001"));
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(onReal.Pack!.Id)); // its battery on real product data goes too
        Assert.NotNull(await rig.Store.GetAsync<ProductionBatch>(realBatch.Id)); // the real product data stays
    }

    [Fact]
    public async Task ResettingTheDemoData_ShouldAlsoRemoveTestIds_AndTheirBatteriesOnRealProductData()
    {
        var (rig, pool) = await SeededAsync(mongo);
        using var _ = rig;
        var pages = rig.Services.GetRequiredService<ProductDataPages>();
        var compact = (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact");
        var (familyId, _) = await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");
        var demoOperator = (await rig.Store.ListAsync<EconomicOperator>()).Single(item => item.Name == "Scania Industrial Batteries (demo)");
        var (operatorId, _) = await pages.CopyOperatorAsRealAsync(Who, demoOperator.Id, "Scania Industrial Batteries");
        var realBatch = (await rig.Store.ListBatchesAsync((await rig.Store.ListModelsAsync(familyId!)).Single(model => model.Name == "Compact 7M").Id)).Single();
        await pool.ReleaseAsync(Who, new ReleaseRequest("Try-out", null, [], "TRY-0001", IsDemo: true));
        var made = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), new NewPack(realBatch.Id, operatorId!, null, Values("TRY-0001"), FromPool: true));

        var reset = await rig.Demo.ResetAsync(Who);

        Assert.Empty(reset.Notes);
        Assert.Null(await pool.GetAsync("TRY-0001"));
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(made.Pack!.Id));
        Assert.NotNull(await rig.Store.GetAsync<ProductionBatch>(realBatch.Id));
    }

    [Fact]
    public void TheReleaseForm_ShouldHaveTheTick_WithWhatItMeans()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Index.cshtml"));

        Assert.Contains("<input type=\"checkbox\" name=\"isTest\" value=\"true\" checked=\"@Model.Form.IsTest\" />", view, StringComparison.Ordinal);
        Assert.Contains("Test IDs (for trying things out; they contain TEST and are never real battery IDs)", view, StringComparison.Ordinal);
        Assert.Contains("They aren't counted in the numbers above, and Reset demo data and Clear demo data remove them with their batteries.", view, StringComparison.Ordinal);
    }

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
