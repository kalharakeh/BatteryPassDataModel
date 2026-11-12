using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round two, D: the warranty (#19) is 1 to 100 years or 1 to 1200 months, checked in the one validator so the page,
/// uploads and the API agree; 0 and other units are refused; a stored value too large to read never breaks a page.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WarrantyLimitsTests(MongoFixture mongo)
{
    private static readonly AttributeForm Warranty = AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(19));

    [Theory]
    [InlineData("99999999999", "Y")]
    [InlineData("P99999999999Y", "Y")]
    [InlineData("0", "Y")]
    [InlineData("8", "X")]
    [InlineData("101", "Y")]
    public async Task AWarrantyOutsideTheLimits_ShouldBeRefusedOnADraft(string amount, string unit)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var value = FormValues.Read(Warranty, name => name == "v.warrantyPeriod" ? amount : name == "v.warrantyPeriod.unit" ? unit : null);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["warrantyPeriod"] = value }, "form"));

        var key = Assert.Single(result.Keys);
        Assert.Equal(KeyOutcome.Rejected, key.Outcome);
        Assert.Equal("The warranty must be a whole number of years (1 to 100) or months (1 to 1200).", RefusalWording.Describe(Warranty, key.Message));
    }

    [Fact]
    public async Task AnUploadedWarrantyOutsideTheLimits_ShouldBeRefusedToo()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var report = await UploadServiceTests.Run(rig, PublicationTesting.Team, UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "warrantyPeriod": "P0Y" }""")));

        Assert.Equal(KeyOutcome.Rejected, Assert.Single(Assert.Single(report.Batteries).Values).Outcome);
    }

    [Fact]
    public async Task TheOfficialCombinedForm_ShouldBeAcceptedFromAFile_AndShowAsMonths()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var report = await UploadServiceTests.Run(rig, PublicationTesting.Team, UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "warrantyPeriod": "P3Y6M" }""")));

        Assert.Equal(KeyOutcome.Saved, Assert.Single(Assert.Single(report.Batteries).Values).Outcome); // round three, 3
        Assert.Equal(("42", "M"), FormValues.DurationParts("P3Y6M"));
    }

    [Fact]
    public async Task AStoredValueTooLargeToRead_ShouldShowAsWrittenAndNotBreakThePage()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>
        {
            ["warrantyPeriod"] = new() { Value = JsonValue.Create("P99999999999Y"), GeneratedAtUtc = DateTime.UtcNow, Source = "form" },
        });

        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        var field = page.Editor.Sections.SelectMany(section => section.Fields).Single(item => item.Attribute.Number == 19);

        Assert.Equal("P99999999999Y", field.Text("v.warrantyPeriod"));
        Assert.Equal("P99999999999Y", Assert.Single(ValueWords.Lines(Warranty, JsonValue.Create("P99999999999Y"))).Text);
        Assert.Equal(("P1Y99999999999M", "Y"), FormValues.DurationParts("P1Y99999999999M"));
    }

    [Fact]
    public void EmptyingTheNumber_ShouldStillBeNoValue()
    {
        Assert.Null(FormValues.Read(Warranty, name => name == "v.warrantyPeriod.unit" ? "Y" : string.Empty));
    }
}
