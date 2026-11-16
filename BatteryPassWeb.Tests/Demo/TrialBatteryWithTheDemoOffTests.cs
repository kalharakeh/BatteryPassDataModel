using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Decision 82, the owner's path: the database holds real product data only, the demo data is cleared and the demo is
/// switched off (<c>ENABLE_DEMO_DATA=false</c>). A team admin releases test IDs, makes a test battery on a real batch of
/// a model that has a serial number format, publishes it, and a visitor opens its public page. Nothing on the way needs
/// demo data.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class TrialBatteryWithTheDemoOffTests(MongoFixture mongo)
{
    private static readonly string[] DppCollections = [DppStore.Families, DppStore.Models, DppStore.Batches, DppStore.Sites, DppStore.Operators, DppStore.Packs, DppStore.BatteryIds, DppStore.Versions];

    [Fact]
    public async Task ATeamAdmin_ShouldReleaseTestIds_MakeATestBatteryOnARealBatch_PublishIt_AndAVisitorOpensIt()
    {
        // Real product data, made while the demo was still there (as the owner's was); then the demo is cleared and switched off.
        using var on = await DemoRig.StartAsync(mongo, dunsIds: true);
        await on.Demo.SeedAsync(Who);
        var productData = on.Services.GetRequiredService<ProductDataPages>();
        var compact = (await on.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact");
        var (familyId, familyProblem) = await productData.CopyFamilyAsRealAsync(Who, compact.Id, "Kern");
        var (operatorId, operatorProblem) = await productData.CopyOperatorAsRealAsync(Who, (await on.Store.ListAsync<EconomicOperator>()).First(item => item.IsDemo).Id, "Scania Industrial Batteries");
        Assert.Null(familyProblem ?? operatorProblem);
        await on.Demo.ClearAsync(Who);
        using var off = DemoRig.Over(on, demoData: false);
        Assert.False(off.Demo.Enabled);
        Assert.False(await off.Demo.HasDemoDataAsync());
        foreach (var collection in DppCollections)
        {
            Assert.Equal(0, await off.Count(collection, Builders<BsonDocument>.Filter.Eq("IsDemo", true))); // no demo data at all
        }

        // The model has a serial number format, as the owner's Core800s.
        var model = (await off.Store.ListModelsAsync(familyId!))[0];
        var batch = (await off.Store.ListBatchesAsync(model.Id)).First();
        var saved = await off.Services.GetRequiredService<ProductDataPages>().SetSerialFormatAsync(Who, model, "VPC8C05s-#####", confirmed: false, off.Services.GetRequiredService<IIdentifierScheme>());
        Assert.True(saved.Saved, saved.Problem);

        // Test IDs, released with the demo off: free of the format, marked TEST.
        var pool = off.Services.GetRequiredService<BatteryIdPool>();
        var released = await pool.ReleaseAsync(Who, new ReleaseRequest("Trial run", null, [], "TRY-0001\nvpc8c05s-90002", IsDemo: true));
        Assert.True(released.Done, released.Problem);

        // A test battery on the real batch, published at once.
        var values = DemoCatalogue.Load().Packs.First(pack => pack.Publish).Values.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone(), StringComparer.Ordinal);
        values["batterySerialNumber"] = "TRY-0001";
        var created = await off.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), new NewPack(batch.Id, operatorId!, null, values, FromPool: true));
        Assert.True(created.Pack is not null, created.Problem);
        var published = await off.Services.GetRequiredService<PublicationService>().PublishAsync(ChangeSender.Team(Who), created.Pack!.Id);
        Assert.True(published.Version is not null, string.Join(" | ", published.Report?.Blocking ?? [published.Problem ?? string.Empty]));

        // A visitor opens its public page, by the ID typed in any letters.
        foreach (var id in new[] { "UN778474481-TEST-TRY-0001", "un778474481-test-try-0001" })
        {
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()), RequestServices = off.Services };
            var controller = new PassportController(off.Services.GetRequiredService<DppReaders>(), off.Services.GetRequiredService<PassportPages>())
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                TempData = new TempDataDictionary(http, new NoTempDataKept()),
            };
            var shown = Assert.IsType<ViewResult>(await controller.Summary(id, CancellationToken.None, pool));
            var page = Assert.IsType<PassportPage>(shown.Model);
            Assert.Equal(("Summary", "TRY-0001", true), (shown.ViewName, page.Text(11), page.IsTest));
            Assert.True(page.SignatureHolds);
        }

        // Nothing along the way needed, or made, demo product data: the only marked records are the trial's own.
        Assert.Equal(new TestDataOutsideTheDemo(2, 1), await off.Demo.TestDataOutsideTheDemoAsync());
        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Batches, DppStore.Sites, DppStore.Operators })
        {
            Assert.Equal(0, await off.Count(collection, Builders<BsonDocument>.Filter.Eq("IsDemo", true)));
        }

        // And "Clear demo data", still on offer, takes the trial away again and leaves the real product data.
        await off.Demo.ClearAsync(Who);
        Assert.Null(await off.Store.GetAsync<BatteryPack>(created.Pack.Id));
        Assert.Empty(await pool.ListAsync(new ReleasedIdFilter(Demo: true, Voided: true)));
        Assert.NotNull(await off.Store.GetAsync<BatteryModel>(model.Id));
    }
}
