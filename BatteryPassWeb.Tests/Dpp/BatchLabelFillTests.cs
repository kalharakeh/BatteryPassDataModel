using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, round two (review L1): the label the app makes for a batch (E8) is written through the save path, audited
/// and marked as the app's. It is made when the site's country or the model's serial number format is saved, whichever
/// comes last. A label once there, typed or made, is never changed by the app; one another batch of the model has is
/// skipped with a note.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatchLabelFillTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    private static AttributeValue Country(string code) => new() { Value = JsonValue.Create(code), GeneratedAtUtc = DppStoreTesting.Now.UtcDateTime, Source = "form" };

    private static async Task<(PublicationRig Rig, BatteryPassWeb.Dpp.Pages.ProductDataPages Pages)> RigAsync(MongoFixture mongo, string? format)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.InsertAsync(new BatteryModel { Id = "model-fill", FamilyId = "family-test", Name = "Core800", SerialFormat = format });
        return (rig, PageTesting.Pages(rig));
    }

    [Fact]
    public async Task TheAppsLabel_ShouldBeSavedAsTheApps_AndAudited()
    {
        var (rig, pages) = await RigAsync(mongo, "VPC8C05-#####");

        var (batchId, _) = await pages.CreateBatchAsync(Who, "model-fill", 2027, "site-test");

        var stored = (await rig.Store.GetAsync<ProductionBatch>(batchId!))!.Values[AttributeCatalog.BatchNumber];
        Assert.Equal(("VPC8C05-SWE-2027", "system"), (stored.Value.GetValue<string>(), stored.Source));
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Contains(events, e => e["eventType"].AsString == SavePipeline.AuditEventType && e["entityId"].AsString == batchId && e["source"].AsString == "system");
    }

    [Fact]
    public async Task TheCountryFirstAndTheFormatSecond_ShouldStillMakeTheLabel()
    {
        var (rig, pages) = await RigAsync(mongo, format: null);
        var (batchId, _) = await pages.CreateBatchAsync(Who, "model-fill", 2027, "site-test"); // the site has its country, the model no format
        Assert.Null(BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(batchId!))!));

        var save = await pages.SetSerialFormatAsync(Who, (await rig.Store.GetAsync<BatteryModel>("model-fill"))!, "VPC8C05-#####", confirmed: true, new TestIdentifierScheme());

        Assert.Equal("VPC8C05-SWE-2027", BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(batchId!))!));
        Assert.Contains(save.Labels, note => note.Contains("VPC8C05-SWE-2027", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ATypedLabel_ShouldBeLeftAlone_WhenTheCountryIsSaved()
    {
        var (rig, pages) = await RigAsync(mongo, "VPC8C05-#####");
        await rig.Store.InsertAsync(new ManufacturingSite { Id = "site-fill", Name = "New plant" });
        var (batchId, _) = await pages.CreateBatchAsync(Who, "model-fill", 2026, "site-fill");
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Batch, batchId!, new Dictionary<string, JsonNode?> { [AttributeCatalog.BatchNumber] = JsonValue.Create("MY-OWN-LABEL") }, "form"));

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Site, "site-fill", new Dictionary<string, JsonNode?> { [AttributeCatalog.ManufacturingCountry] = JsonValue.Create("FIN") }, "form"));

        Assert.Equal("MY-OWN-LABEL", BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(batchId!))!));
    }

    [Fact]
    public async Task ALabelAnotherBatchOfTheModelHas_ShouldBeSkippedWithANote()
    {
        var (rig, pages) = await RigAsync(mongo, format: null);
        await rig.Store.InsertAsync(new ManufacturingSite { Id = "site-other", Name = "Other plant", Values = new() { [AttributeCatalog.ManufacturingCountry] = Country("SWE") } });
        var (first, _) = await pages.CreateBatchAsync(Who, "model-fill", 2027, "site-test");
        var (second, _) = await pages.CreateBatchAsync(Who, "model-fill", 2027, "site-other");

        var save = await pages.SetSerialFormatAsync(Who, (await rig.Store.GetAsync<BatteryModel>("model-fill"))!, "VPC8C05-#####", confirmed: true, new TestIdentifierScheme());

        var labels = new[] { BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(first!))!), BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(second!))!) };
        Assert.Equal(1, labels.Count(label => label == "VPC8C05-SWE-2027"));
        Assert.Equal(1, labels.Count(label => label is null));
        Assert.Contains(save.Labels, note => note.Contains("got no label", StringComparison.Ordinal) && note.Contains("VPC8C05-SWE-2027", StringComparison.Ordinal));
    }
}
