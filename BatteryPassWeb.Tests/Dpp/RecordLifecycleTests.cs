using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Removing records made by mistake (only while nothing uses them), starting a model from a copy, and refusing a name
/// another record has.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RecordLifecycleTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    [Fact]
    public async Task RecordsInUse_ShouldSayWhyTheyCantBeRemoved()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        Assert.Equal("It still has 1 battery model. Remove them first.", await pages.WhyNotRemovableAsync(RecordKind.Family, "family-test"));
        Assert.Equal("1 battery uses it.", await pages.WhyNotRemovableAsync(RecordKind.Model, "model-test"));
        Assert.Equal("1 battery belongs to it.", await pages.WhyNotRemovableAsync(RecordKind.Batch, "batch-test-2026"));
        Assert.Equal("1 batch was made there.", await pages.WhyNotRemovableAsync(RecordKind.Site, "site-test"));
        Assert.Equal("1 battery names it as economic operator.", await pages.WhyNotRemovableAsync(RecordKind.Operator, "operator-test"));

        var (removed, problem, _) = await pages.RemoveAsync(Who, RecordKind.Model, "model-test");
        Assert.False(removed);
        Assert.Equal("1 battery uses it.", problem);
        Assert.NotNull(await rig.Store.GetAsync<BatteryModel>("model-test"));
    }

    [Fact]
    public async Task UnusedRecords_ShouldBeRemovedWithTheirDocumentsAndLogged()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var familyId = await pages.CreateFamilyAsync(Who, "Mistake family");
        var modelId = (await pages.CreateModelAsync(Who, familyId, "Mistake model"))!;
        var (batchId, _) = await pages.CreateBatchAsync(Who, modelId, 2026, "site-test");
        await PageTesting.Documents(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, modelId, "dismantlingInformation", new DocumentUpload("manual.pdf", DocumentTesting.Pdf()));

        Assert.Equal("It still has 1 batch. Remove them first.", await pages.WhyNotRemovableAsync(RecordKind.Model, modelId));
        Assert.True((await pages.RemoveAsync(Who, RecordKind.Batch, batchId!)).Removed);
        Assert.True((await pages.RemoveAsync(Who, RecordKind.Model, modelId)).Removed);
        Assert.True((await pages.RemoveAsync(Who, RecordKind.Family, familyId)).Removed);

        Assert.Null(await rig.Store.GetAsync<ProductionBatch>(batchId!));
        Assert.Null(await rig.Store.GetAsync<BatteryModel>(modelId));
        Assert.Null(await rig.Store.GetAsync<BatteryFamily>(familyId));
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Model, modelId, "dismantlingInformation"));
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Equal(3, events.Count(e => e["eventType"].AsString == ProductDataPages.RemovedEvent));
    }

    [Fact]
    public async Task ACopiedModel_ShouldStartWithTheTypedValuesButNotTheDocuments()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var source = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;

        var copyId = await pages.CreateModelAsync(Who, "family-test", "Model test 2", "model-test");

        var copy = (await rig.Store.GetAsync<BatteryModel>(copyId!))!;
        var uploads = RecordEditor.AttributesOf(TargetKind.Model)
            .Where(attribute => AttributeFormsTests.Forms.For(attribute).Kind == BatteryPassWeb.Dpp.Forms.FormKind.Upload)
            .Select(attribute => attribute.Property)
            .ToHashSet();
        Assert.Equal(source.Values.Keys.Where(key => !uploads.Contains(key)).Order(), copy.Values.Keys.Order());
        Assert.True(JsonNode.DeepEquals(source.Values["batteryMass"].Value, copy.Values["batteryMass"].Value));
        Assert.All(copy.Values.Values, value => Assert.Equal((Who, "form"), (value.Who, value.Source)));
    }

    [Fact]
    public async Task ANameAnotherRecordHas_ShouldBeRefused()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;
        var site = (await rig.Store.GetAsync<ManufacturingSite>("site-test"))!;

        Assert.NotNull(await pages.NameTakenAsync(RecordKind.Model, model.Name.ToUpperInvariant(), "family-test"));
        Assert.Null(await pages.NameTakenAsync(RecordKind.Model, model.Name, "another-family"));
        Assert.Null(await pages.NameTakenAsync(RecordKind.Model, model.Name, "family-test", exceptId: "model-test"));
        Assert.NotNull(await pages.NameTakenAsync(RecordKind.Site, $" {site.Name} "));
        Assert.Null(await pages.NameTakenAsync(RecordKind.Operator, "A new operator"));
    }
}
