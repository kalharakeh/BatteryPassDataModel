using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class ProductDataPagesTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    [Fact]
    public async Task Families_ShouldCountModelsBatchesAndBatteries()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var row = Assert.Single((await PageTesting.Pages(rig).FamiliesAsync()).Families);

        Assert.Equal(("family-test", 1, 1, 1, 1), (row.Family.Id, row.Models, row.Batches, row.Packs, row.Published));
    }

    [Fact]
    public async Task ANewModel_ShouldShowWhatItStillNeedsByFeb2027()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var modelId = (await pages.CreateModelAsync(Who, "family-test", "C7M-2"))!;

        var family = Assert.Single((await pages.FamiliesAsync()).Families);
        var page = (await pages.FamilyAsync("family-test", modelId))!;

        Assert.Equal(27, family.Missing); // the seeded model and batch are complete; #20 is filled in on creation
        var chips = page.Models.ToDictionary(chip => chip.Model.Id, chip => chip.Missing);
        Assert.Equal((0, 27), (chips["model-test"], chips[modelId]));
        Assert.Equal(27, page.ModelEditor!.Missing);
    }

    [Fact]
    public async Task ANewModel_ShouldStartWithTheValueTheSchemaAllowsOnlyOneWay()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var modelId = (await PageTesting.Pages(rig).CreateModelAsync(Who, "family-test", "C7M-3"))!;

        var stored = (await rig.Store.GetAsync<BatteryModel>(modelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.batteryMass"] = "512";
        var post = PageTesting.Editor(rig).Read(TargetKind.Model, stored, PageTesting.Field(form));

        var category = Assert.Single(stored); // #20 battery category has no input: the schema allows one value
        Assert.Equal(("batteryCategory", "industrial/non-stationary battery"), (category.Key, category.Value.Value["batteryCategoryValue"]!.GetValue<string>()));
        Assert.Equal((Who, "form"), (category.Value.Who, category.Value.Source));
        Assert.Equal(["batteryMass"], post.Changed.Keys); // so typing one value saves one value
    }

    [Fact]
    public async Task Family_ShouldOpenItsFirstModelWithItsBatchesAndEditor()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var page = (await PageTesting.Pages(rig).FamilyAsync("family-test", modelId: null))!;

        Assert.Equal("model-test", page.SelectedModel?.Id);
        var batch = Assert.Single(page.Batches);
        Assert.Equal(("Example plant", 16, 16, 1, 0), (batch.SiteName, batch.Filled, batch.Total, batch.Packs, batch.Published));
        Assert.Equal((TargetKind.Model, 37, 37), (page.ModelEditor!.Kind, page.ModelEditor.Filled, page.ModelEditor.Total));
        Assert.Equal("/admin/models/model-test", page.ModelEditor.SaveAction);
        Assert.Null(await PageTesting.Pages(rig).FamilyAsync("no-such-family", null));
    }

    [Fact]
    public async Task AQuestionToConfirm_ShouldReachTheEditor()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.ratedCapacity"] = "210";
        var post = editor.Read(TargetKind.Model, stored, PageTesting.Field(form));
        var save = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, "model-test", post, confirmed: false);

        var page = (await PageTesting.Pages(rig).FamilyAsync("family-test", "model-test", new EditorState(post, save)))!;

        Assert.Equal("VPC8C05-6788", Assert.Single(page.ModelEditor!.Confirm).SerialNumber);
        Assert.Equal(1, page.ModelEditor.PublishedPassports);
        Assert.Equal("210", page.ModelEditor.Sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Number == 57).Text("v.ratedCapacity"));
    }

    [Fact]
    public async Task NewBatches_ShouldBeOnePerYearAndSite()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        Assert.Contains("already has a 2026 batch", (await pages.CreateBatchAsync(Who, "model-test", 2026, "site-test")).Problem);
        Assert.Contains("calendar year", (await pages.CreateBatchAsync(Who, "model-test", 26, "site-test")).Problem);
        Assert.Contains("manufacturing site", (await pages.CreateBatchAsync(Who, "model-test", 2027, "no-such-site")).Problem);
        Assert.Contains("model was not found", (await pages.CreateBatchAsync(Who, "no-such-model", 2027, "site-test")).Problem);

        var (batchId, problem) = await pages.CreateBatchAsync(Who, "model-test", 2027, "site-test");

        Assert.Null(problem);
        Assert.Equal(2027, (await rig.Store.GetAsync<ProductionBatch>(batchId!))!.Year);
    }

    [Fact]
    public async Task NewRecords_ShouldBeCreatedAndLogged()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        var familyId = await pages.CreateFamilyAsync(Who, "  Compact 7M ");
        var modelId = await pages.CreateModelAsync(Who, familyId, "C7M-1");
        var siteId = await pages.CreateSiteAsync(Who, "Plant 2");
        var operatorId = await pages.CreateOperatorAsync(Who, "Operator 2");

        Assert.Equal("Compact 7M", (await rig.Store.GetAsync<BatteryFamily>(familyId))!.Name);
        Assert.Equal(familyId, (await rig.Store.GetAsync<BatteryModel>(modelId!))!.FamilyId);
        Assert.NotNull(await rig.Store.GetAsync<ManufacturingSite>(siteId));
        Assert.NotNull(await rig.Store.GetAsync<EconomicOperator>(operatorId));
        Assert.Null(await pages.CreateModelAsync(Who, "no-such-family", "X"));
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Equal(4, events.Count(e => e["eventType"].AsString == ProductDataPages.CreatedEvent));
    }

    [Fact]
    public async Task Rename_ShouldOnlyActOnANewName()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        Assert.False(await pages.RenameAsync<ManufacturingSite>(Who, "site-test", "Example plant", " Example plant "));
        Assert.False(await pages.RenameAsync<ManufacturingSite>(Who, "site-test", "Example plant", "  "));
        Assert.True(await pages.RenameAsync<ManufacturingSite>(Who, "site-test", "Example plant", "Plant 1"));

        Assert.Equal("Plant 1", (await rig.Store.GetAsync<ManufacturingSite>("site-test"))!.Name);
    }

    [Fact]
    public async Task FamilyPicture_ShouldBeAPngOrJpeg()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        Assert.Contains("PNG or JPEG", await pages.SetFamilyPictureAsync(Who, "family-test", "manual.pdf", DocumentTesting.Pdf()));
        Assert.Contains("PNG or JPEG", await pages.SetFamilyPictureAsync(Who, "family-test", "empty.png", []));
        Assert.Contains("not found", await pages.SetFamilyPictureAsync(Who, "no-such-family", "f.png", DocumentTesting.Png()));
        Assert.Null(await pages.FamilyPictureAsync("family-test"));

        Assert.Null(await pages.SetFamilyPictureAsync(Who, "family-test", "C:\\pictures\\family.png", DocumentTesting.Png()));

        var picture = (await pages.FamilyPictureAsync("family-test"))!.Value;
        Assert.Equal(DocumentTesting.Png(), picture.Content);
        Assert.Equal(FileTypes.Png, picture.ContentType);
    }

    [Fact]
    public async Task SitesAndOperators_ShouldOpenTheChosenRecord()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        var none = await pages.SitesAndOperatorsAsync(null, null);
        var site = await pages.SitesAndOperatorsAsync("site-test", null);
        var economicOperator = await pages.SitesAndOperatorsAsync(null, "operator-test");

        Assert.Null(none.Editor);
        Assert.Equal((1, 3, 3), (Assert.Single(none.Sites).Batches, none.Sites[0].Filled, none.Sites[0].Total));
        Assert.Equal(1, Assert.Single(none.Operators).Packs);
        Assert.Equal((TargetKind.Site, "/admin/sites/site-test"), (site.Editor!.Kind, site.Editor.SaveAction));
        Assert.Equal((TargetKind.Operator, 2), (economicOperator.Editor!.Kind, economicOperator.Editor.Total));
    }
}
