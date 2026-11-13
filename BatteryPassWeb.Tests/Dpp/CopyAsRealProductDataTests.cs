using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 66: the team makes real product data out of the demo data in one step ("Copy as real product data" on a demo
/// family, "Copy as real" on a demo site or operator). The copies carry no demo mark, have their own IDs and their own
/// files, and stay when the demo data is cleared or reset; the demo data is left as it is. Decision 45's rule stays.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class CopyAsRealProductDataTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;

    private static async Task<(DemoRig Rig, ProductDataPages Pages, BatteryFamily Compact)> SeededAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(Who);
        var compact = (await rig.Store.ListAsync<BatteryFamily>()).Single(family => family.Name == "Compact");
        return (rig, rig.Services.GetRequiredService<ProductDataPages>(), compact);
    }

    private static string Json<T>(IEnumerable<T> records) => string.Join("\n", records.Select(record => record.ToBsonDocument().ToJson()));

    private static string Texts(IReadOnlyDictionary<string, BatteryPassWeb.Dpp.Values.AttributeValue> values) =>
        string.Join("\n", values.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key}={entry.Value.Value.ToJsonString()}"));

    [Fact]
    public async Task CopyingADemoFamily_ShouldMakeARealFamilyWithEveryModelBatchDocumentAndPicture()
    {
        var (rig, pages, compact) = await SeededAsync(mongo);
        using var _ = rig;
        var files = rig.Services.GetRequiredService<DocumentStore>();
        var demoModels = await rig.Store.ListModelsAsync(compact.Id);

        var (familyId, problem) = await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");

        Assert.Null(problem);
        var family = (await rig.Store.GetAsync<BatteryFamily>(familyId!))!;
        Assert.False(family.IsDemo);
        Assert.Equal("Compact S", family.Name);
        Assert.NotEqual(compact.PictureFileId, family.PictureFileId);
        Assert.Equal(await files.DownloadFileAsync(compact.PictureFileId!), await files.DownloadFileAsync(family.PictureFileId!));

        var models = await rig.Store.ListModelsAsync(familyId!);
        Assert.Equal(demoModels.Select(model => model.Name).Order(), models.Select(model => model.Name).Order());
        foreach (var model in models)
        {
            var demo = demoModels.Single(item => item.Name == model.Name);
            Assert.False(model.IsDemo);
            Assert.NotEqual(demo.Id, model.Id);
            Assert.Equal(demo.ModuleDescription, model.ModuleDescription);
            Assert.Equal(demo.Values.Keys.Order(), model.Values.Keys.Order());
            var demoDocuments = await rig.All(DocumentStore.Records, Builders<BsonDocument>.Filter.Eq("OwnerId", demo.Id));
            var documents = await rig.All(DocumentStore.Records, Builders<BsonDocument>.Filter.Eq("OwnerId", model.Id));
            Assert.Equal(4, documents.Count);
            Assert.Equal(demoDocuments.Select(document => document["Property"].AsString).Order(), documents.Select(document => document["Property"].AsString).Order());
            foreach (var document in documents)
            {
                Assert.DoesNotContain(document["FileId"], demoDocuments.Select(item => item["FileId"]));
                var url = model.Values[document["Property"].AsString].Value["url"]!.GetValue<string>();
                Assert.EndsWith($"/dpp/documents/{document["_id"].AsString}", url, StringComparison.Ordinal); // the copy links its own file
                Assert.NotEmpty(await files.DownloadFileAsync(document["FileId"].AsString));
            }

            foreach (var (key, value) in demo.Values.Where(entry => !DppLinks.RestrictedDocumentProperties.Contains(entry.Key)))
            {
                Assert.True(JsonNode.DeepEquals(value.Value, model.Values[key].Value), $"{model.Name}: {key} differs");
            }

            var batches = await rig.Store.ListBatchesAsync(model.Id);
            var demoBatches = await rig.Store.ListBatchesAsync(demo.Id);
            Assert.Equal(demoBatches.Count, batches.Count);
            Assert.All(batches, batch => Assert.False(batch.IsDemo));
            Assert.All(batches, batch => Assert.Single(rig.All(DocumentStore.Records, Builders<BsonDocument>.Filter.Eq("OwnerId", batch.Id)).Result));
        }

        // The demo family is left exactly as it was.
        Assert.True((await rig.Store.GetAsync<BatteryFamily>(compact.Id))!.IsDemo);
        Assert.Equal(Json(demoModels), Json(await rig.Store.ListModelsAsync(compact.Id)));
    }

    [Fact]
    public async Task TheCopy_ShouldUseARealSiteNamedWithoutDemo_MadeOnceAndReused()
    {
        var (rig, pages, compact) = await SeededAsync(mongo);
        using var _ = rig;

        var (first, _) = await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");
        var (second, _) = await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact T");

        var sites = await rig.Store.ListAsync<ManufacturingSite>();
        var real = Assert.Single(sites, site => !site.IsDemo);
        Assert.Equal("Plant North", real.Name);
        var demoSite = sites.Single(site => site.Name == "Demo plant North");
        Assert.Equal(demoSite.Values.Keys.Order(), real.Values.Keys.Order());
        foreach (var familyId in new[] { first!, second! })
        {
            foreach (var model in await rig.Store.ListModelsAsync(familyId))
            {
                Assert.All(await rig.Store.ListBatchesAsync(model.Id), batch => Assert.Equal(real.Id, batch.SiteId));
            }
        }
    }

    [Theory]
    [InlineData("Demo plant North", "Plant North")]
    [InlineData("Scania Industrial Batteries (demo)", "Scania Industrial Batteries")]
    [InlineData("Demo Fleet Services AB", "Fleet Services AB")]
    [InlineData("Compact", "Compact (real)")]
    public void TheRealName_ShouldLeaveOutTheDemoWord(string demo, string real) =>
        Assert.Equal(real, ProductDataPages.RealCopyName(demo));

    [Fact]
    public async Task ACopy_ShouldBeRefused_ForRealDataOrATakenName_AndMakeNothing()
    {
        var (rig, pages, compact) = await SeededAsync(mongo);
        using var _ = rig;
        var before = await rig.Count(DppStore.Families);

        Assert.Contains("another battery family is already called core", (await pages.CopyFamilyAsRealAsync(Who, compact.Id, "core")).Problem);
        Assert.NotNull((await pages.CopyFamilyAsRealAsync(Who, compact.Id, "  ")).Problem);
        var (realId, _) = await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");
        Assert.Equal("it is already real product data", (await pages.CopyFamilyAsRealAsync(Who, realId!, "Compact T")).Problem);
        var realOperator = await pages.CreateOperatorAsync(Who, "Real operator");
        Assert.Equal("it is already real product data", (await pages.CopyOperatorAsRealAsync(Who, realOperator, "Another")).Problem);

        Assert.Equal(before + 1, await rig.Count(DppStore.Families));
    }

    [Fact]
    public async Task EachCopy_ShouldWriteOneAuditEntryNamingWhatWasMade()
    {
        var (rig, pages, compact) = await SeededAsync(mongo);
        using var _ = rig;
        var operatorId = (await rig.Store.ListAsync<EconomicOperator>()).Single(item => item.Name == "Scania Industrial Batteries (demo)").Id;
        var before = await rig.Count("applicationAuditEvents");

        await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");
        await pages.CopyOperatorAsRealAsync(Who, operatorId, "Scania Industrial Batteries");

        var entries = (await rig.All("applicationAuditEvents")).Skip((int)before).ToList();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Equal(ProductDataPages.CreatedEvent, entry["eventType"].AsString));
        Assert.Equal("Battery family Compact S created as a copy of Compact (demo data), with 2 models, 2 batches and the site Plant North", entries[0]["message"].AsString);
        Assert.Equal("Economic operator Scania Industrial Batteries created as a copy of Scania Industrial Batteries (demo) (demo data)", entries[1]["message"].AsString);
    }

    [Fact]
    public async Task SitesAndOperators_ShouldBeCopiedAsReal_WithTheirValues()
    {
        var (rig, pages, _) = await SeededAsync(mongo);
        using var __ = rig;
        var demoOperator = (await rig.Store.ListAsync<EconomicOperator>()).Single(item => item.Name == "Demo Fleet Services AB");
        var demoSite = (await rig.Store.ListAsync<ManufacturingSite>()).Single(item => item.Name == "Demo plant South");

        var (operatorId, operatorProblem) = await pages.CopyOperatorAsRealAsync(Who, demoOperator.Id, "Fleet Services AB");
        var (siteId, siteProblem) = await pages.CopySiteAsRealAsync(Who, demoSite.Id, "Plant South");

        Assert.Null(operatorProblem);
        Assert.Null(siteProblem);
        var copiedOperator = (await rig.Store.GetAsync<EconomicOperator>(operatorId!))!;
        var copiedSite = (await rig.Store.GetAsync<ManufacturingSite>(siteId!))!;
        Assert.False(copiedOperator.IsDemo);
        Assert.False(copiedSite.IsDemo);
        Assert.Equal(Texts(demoOperator.Values), Texts(copiedOperator.Values));
        Assert.Equal(Texts(demoSite.Values), Texts(copiedSite.Values));
        Assert.True((await rig.Store.GetAsync<EconomicOperator>(demoOperator.Id))!.IsDemo);
        Assert.Contains("another economic operator is already called fleet services ab", (await pages.CopyOperatorAsRealAsync(Who, demoOperator.Id, "fleet services ab")).Problem);
    }

    [Fact]
    public async Task TheSitesAndOperatorsPage_ShouldOfferCopyAsReal_OnlyForTheChosenDemoRecord()
    {
        var (rig, pages, _) = await SeededAsync(mongo);
        using var __ = rig;
        var demoOperator = (await rig.Store.ListAsync<EconomicOperator>()).Single(item => item.Name == "Scania Industrial Batteries (demo)");
        var (realId, _) = await pages.CopyOperatorAsRealAsync(Who, demoOperator.Id, "Scania Industrial Batteries");

        var offered = (await pages.SitesAndOperatorsAsync(null, demoOperator.Id)).RealCopy;

        Assert.Equal($"/admin/operators/{demoOperator.Id}/copy-as-real", offered!.Action);
        Assert.Equal("Scania Industrial Batteries", offered.SuggestedName);
        Assert.Null((await pages.SitesAndOperatorsAsync(null, realId)).RealCopy);
        Assert.Null((await pages.SitesAndOperatorsAsync(null, null)).RealCopy);
    }

    [Fact]
    public async Task ClearingTheDemoData_ShouldLeaveTheCopiesWhole_AndKeepNothingOfTheDemo()
    {
        var (rig, pages, compact) = await SeededAsync(mongo);
        using var _ = rig;
        var (familyId, _) = await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");
        var demoOperator = (await rig.Store.ListAsync<EconomicOperator>()).Single(item => item.Name == "Scania Industrial Batteries (demo)");
        await pages.CopyOperatorAsRealAsync(Who, demoOperator.Id, "Scania Industrial Batteries");
        var copied = Json(await rig.Store.ListModelsAsync(familyId!));

        var cleared = await rig.Demo.ClearAsync(Who);

        Assert.Empty(cleared.Notes); // nothing real still uses demo data, so nothing demo is kept
        Assert.Equal(0, await rig.Count(DppStore.Families, Builders<BsonDocument>.Filter.Eq("IsDemo", true)));
        Assert.Equal(copied, Json(await rig.Store.ListModelsAsync(familyId!)));
        var files = rig.Services.GetRequiredService<DocumentStore>();
        foreach (var document in await rig.All(DocumentStore.Records))
        {
            Assert.NotEmpty(await files.DownloadFileAsync(document["FileId"].AsString));
        }

        Assert.NotEmpty(await files.DownloadFileAsync((await rig.Store.GetAsync<BatteryFamily>(familyId!))!.PictureFileId!));
        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Batches, DppStore.Sites, DppStore.Operators })
        {
            Assert.True(await rig.Count(collection) > 0, $"{collection} lost the copies");
        }
    }

    [Fact]
    public async Task AfterAReset_ARealIdShouldMakeAndPublishABatteryOnTheCopy_OutsideTheDemo()
    {
        var (rig, pages, compact) = await SeededAsync(mongo);
        using var _ = rig;
        var (familyId, _) = await pages.CopyFamilyAsRealAsync(Who, compact.Id, "Compact S");
        var demoOperator = (await rig.Store.ListAsync<EconomicOperator>()).Single(item => item.Name == "Scania Industrial Batteries (demo)");
        var (operatorId, _) = await pages.CopyOperatorAsRealAsync(Who, demoOperator.Id, "Scania Industrial Batteries");

        var reset = await rig.Demo.ResetAsync(Who);
        Assert.Empty(reset.Notes);
        Assert.Contains((await rig.Store.ListAsync<BatteryFamily>()), family => family.IsDemo && family.Name == "Compact"); // rebuilt next to the copy
        Assert.NotNull(await rig.Store.GetAsync<BatteryFamily>(familyId!));

        var model = (await rig.Store.ListModelsAsync(familyId!)).Single(item => item.Name == "Compact 7M");
        var batch = (await rig.Store.ListBatchesAsync(model.Id)).Single();
        var pool = rig.Services.GetRequiredService<BatteryIdPool>();
        Assert.True((await pool.ReleaseAsync(Who, new ReleaseRequest("PO 1", null, [], "VPC-12221"))).Done);
        var values = DemoCatalogue.Load().Packs.First(pack => pack.Publish).Values.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone(), StringComparer.Ordinal);
        values["batterySerialNumber"] = "VPC-12221";

        var created = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), new NewPack(batch.Id, operatorId!, null, values, "form", FromPool: true));
        Assert.Null(created.Problem);
        await ExampleValuesTests.ReplaceExamplesAsync(rig, created.Pack!.Id); // the demo's example values first (decision 70)
        var published = await rig.Services.GetRequiredService<PublicationService>().PublishAsync(ChangeSender.Team(Who), created.Pack!.Id);

        Assert.Null(published.Problem);
        Assert.NotNull(published.Version);
        Assert.False(created.Pack.IsDemo);
        Assert.DoesNotContain("TEST", created.Pack.Values["uniqueProductIdentifier"].Value.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(await rig.Services.GetRequiredService<DemoBoundary>().InsideAsync(created.Pack)); // demo accounts and tokens don't reach it

        // The same real ID is still refused on the rebuilt demo batch.
        var demoBatch = (await rig.All(DppStore.Batches, Builders<BsonDocument>.Filter.Eq("IsDemo", true))).First()["_id"].AsString;
        await pool.ReleaseAsync(Who, new ReleaseRequest("PO 2", null, [], "VPC-12222"));
        values["batterySerialNumber"] = "VPC-12222";
        var refused = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), new NewPack(demoBatch, operatorId!, null, values, "form", FromPool: true));
        Assert.Null(refused.Pack);

        Assert.Empty((await rig.Demo.ClearAsync(Who)).Notes); // and clearing still works with a real battery on the copy
    }
}
