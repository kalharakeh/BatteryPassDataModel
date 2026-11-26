using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, G14 (decision 94, the owner's answer O4): the EU's format sends the model identifier only with a batch
/// number, so a batch gets a label: the model's serial prefix, the site's country and the year, such as
/// VPC8C05-SWE-2026, or the team's own. No two batches of a model share one.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatchLabelTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    private static AttributeDefinition Label => AttributeCatalog.FindByProperty(AttributeCatalog.BatchNumber)!;

    private static ManufacturingSite Site(string? country) => new()
    {
        Id = "site-label",
        Name = "Plant",
        Values = country is null ? [] : new() { [AttributeCatalog.ManufacturingCountry] = new AttributeValue { Value = JsonValue.Create(country), GeneratedAtUtc = DppStoreTesting.Now.UtcDateTime, Source = "form" } },
    };

    private static BatteryModel Model(string? format) => new() { Id = "model-label", FamilyId = "family-test", Name = "Core800", SerialFormat = format };

    [Fact]
    public void TheBatchLabel_ShouldBeARequiredPublicValueOfTheBatch()
    {
        Assert.Equal(("E8", StorageLocation.Batch, Audience.Public, "identifiersAndProductData"), (Label.Label, Label.Storage, Label.Audience, Label.Section));
        Assert.Equal(Need.Required, PassportNeeds.NeedOf(Label, ModelAnswers.NotAnswered));
    }

    [Theory]
    [InlineData("VPC8C05-#####", "SWE", "VPC8C05-SWE-2026")]
    [InlineData("VPC8C05s-#####", "SWE", "VPC8C05s-SWE-2026")]
    [InlineData("VP-C7C05-####-A", "FIN", "VPC7C05-FIN-2026")]
    [InlineData(null, "SWE", null)]
    [InlineData("#####", "SWE", null)]
    [InlineData("VPC8C05-#####", null, null)]
    public void TheAppsLabel_ShouldBeTheSerialPrefixTheCountryAndTheYear(string? format, string? country, string? expected)
    {
        Assert.Equal(expected, BatchLabels.Default(Model(format), Site(country), 2026));
    }

    [Fact]
    public async Task ANewBatch_ShouldGetTheAppsLabel_AndASecondBatchWithTheSameLabelNone()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.InsertAsync(Model("VPC8C05-#####"));
        var pages = PageTesting.Pages(rig);

        var (first, _) = await pages.CreateBatchAsync(Who, "model-label", 2027, "site-test");
        Assert.Equal("VPC8C05-SWE-2027", BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(first!))!));

        // A second Swedish site of the same model and year would make the same label: that batch gets none, and the team types one.
        await rig.Store.InsertAsync(new ManufacturingSite { Id = "site-other", Name = "Other plant", Values = Site("SWE").Values });
        var (second, _) = await pages.CreateBatchAsync(Who, "model-label", 2027, "site-other");
        Assert.Null(BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(second!))!));
    }

    [Fact]
    public async Task ALabelAnotherBatchOfTheModelHas_ShouldBeRefused_AndTheBatchsOwnAccepted()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var (other, _) = await pages.CreateBatchAsync(Who, "model-test", 2027, "site-test");

        var taken = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Batch, other!, new Dictionary<string, JsonNode?> { [AttributeCatalog.BatchNumber] = JsonValue.Create("exb-swe-2026 ") }, "form"));
        var own = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Batch, other!, new Dictionary<string, JsonNode?> { [AttributeCatalog.BatchNumber] = JsonValue.Create("EXB-SWE-2027") }, "form"));

        Assert.Equal((KeyOutcome.Rejected, BatchLabels.Taken), (taken.Keys[0].Outcome, taken.Keys[0].Message));
        Assert.Equal(KeyOutcome.Saved, own.Keys[0].Outcome);
        Assert.Equal("EXB-SWE-2027", BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(other!))!));
    }

    [Fact]
    public async Task ABatchWithoutALabel_ShouldGetTheAppsLabel_WhenItsSitesCountryIsSaved()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.InsertAsync(Model("VPC8C05-#####"));
        await rig.Store.InsertAsync(new ManufacturingSite { Id = "site-label", Name = "New plant" });
        var (batchId, _) = await PageTesting.Pages(rig).CreateBatchAsync(Who, "model-label", 2026, "site-label");
        Assert.Null(BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(batchId!))!)); // no country yet

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Site, "site-label", new Dictionary<string, JsonNode?> { [AttributeCatalog.ManufacturingCountry] = JsonValue.Create("SWE") }, "form"));

        Assert.Equal("VPC8C05-SWE-2026", BatchLabels.Of((await rig.Store.GetAsync<ProductionBatch>(batchId!))!));
    }

    [Fact]
    public void APassportWithoutABatchLabel_ShouldNotPublish()
    {
        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        var ident = passport["batteryPassportData"]!["identifiersAndProductData"]!.AsObject();
        Assert.Equal("EXB-SWE-2026", ident[AttributeCatalog.BatchNumber]!.GetValue<string>());

        ident.Remove(AttributeCatalog.BatchNumber);

        Assert.Contains("E8 Batch label is missing", new PublicationChecks(DppStoreTesting.Schemas).Check(passport, DppTestSeed.Answers).Blocking);
    }
}
