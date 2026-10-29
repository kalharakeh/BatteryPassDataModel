using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 28: the four extreme-temperature times E1–E4, live values from the BMS added beyond the V3 file
/// (Polarion STD-800, DIN DKE 99100 6.7.7.5–6.7.7.8). Not in the v2.0.1 schema, which allows extra properties.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class AttributeExtensionTests(MongoFixture mongo)
{
    private static readonly OfficialSchemas Schemas = OfficialSchemas.Load();

    [Fact]
    public void TheExtensions_ShouldBeE1ToE4KeptLikeTemperature()
    {
        var temperature = AttributeCatalog.ByNumber(87);

        Assert.Equal(["E1", "E2", "E3", "E4"], AttributeCatalog.Extensions.Select(attribute => attribute.Label));
        Assert.Equal(
            ["timeSpentInExtremeTemperaturesAboveBoundary", "timeSpentInExtremeTemperaturesBelowBoundary",
             "timeSpentChargingDuringExtremeTemperaturesAboveBoundary", "timeSpentChargingDuringExtremeTemperaturesBelowBoundary"],
            AttributeCatalog.Extensions.Select(attribute => attribute.Property));
        Assert.All(AttributeCatalog.Extensions, attribute =>
        {
            Assert.True(attribute.IsExtension);
            Assert.Equal((temperature.Section, StorageLocation.Pack, ChangeRule.Live, temperature.Audience, temperature.Editors),
                (attribute.Section, attribute.Storage, attribute.Change, attribute.Audience, attribute.Editors));
            Assert.Equal(("s", Requirement.Conditional, false), (attribute.Unit, attribute.Requirement, attribute.MandatoryFromFeb2027));
        });
        Assert.Equal(73, AttributeCatalog.FileAttributes.Count);
        Assert.Equal(AttributeCatalog.FileAttributes.Concat(AttributeCatalog.Extensions), AttributeCatalog.All);
        Assert.Equal("#87", temperature.Label);
        Assert.False(temperature.IsExtension);
    }

    [Fact]
    public void TheExtensions_ShouldNotBeInTheOfficialSchema()
    {
        // When BatteryPass-Ready adds them, this fails: the extensions should then become schema attributes.
        Assert.All(AttributeCatalog.Extensions, attribute => Assert.Null(Schemas.FindProperty(attribute.Section, attribute.Property)));
    }

    [Fact]
    public void AnExtension_ShouldBeEnteredAsWholeSeconds()
    {
        var form = AttributeFormsTests.Forms.For(AttributeCatalog.Extensions[0]);

        Assert.Equal(FormKind.Single, form.Kind);
        var input = Assert.Single(form.Inputs);
        Assert.Equal((string.Empty, InputKind.Integer, "seconds", false), (input.Path, input.Kind, input.Unit, input.Required));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("3600", true)]
    [InlineData("-1", false)]
    [InlineData("1.5", false)]
    [InlineData("\"3600\"", false)]
    [InlineData("""{"value":3600}""", false)]
    public void AnExtensionValue_ShouldBeAWholeNumberOfSecondsNotBelowZero(string json, bool valid)
    {
        var issues = new AttributeValueValidator(Schemas).Validate(AttributeCatalog.Extensions[0], JsonNode.Parse(json));

        Assert.Equal(valid, issues.Count == 0);
        Assert.Equal(JsonValue.Create(3600)!.ToJsonString(), new AttributeValueNormalizer(Schemas).Normalize(AttributeCatalog.Extensions[0], JsonValue.Create(3600))!.ToJsonString());
    }

    [Fact]
    public async Task ExtensionValues_ShouldBeSavedLiveAndPublishedWithThePassportOutsideTheSignedCore()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(
            TargetKind.Pack,
            rig.PackId,
            new Dictionary<string, JsonNode?>
            {
                ["timeSpentInExtremeTemperaturesAboveBoundary"] = JsonValue.Create(120),
                ["timeSpentChargingDuringExtremeTemperaturesAboveBoundary"] = JsonValue.Create(0),
            },
            "form"));

        Assert.True(result.Saved, string.Join("; ", result.Keys.Select(key => key.Message)));
        var passport = PassportAssembler.Assemble((await rig.Store.LoadSourcesAsync(rig.PackId))!, rig.Seed.Header);
        Assert.Equal(120, passport["batteryPassportData"]!["performanceAndDurability"]!["timeSpentInExtremeTemperaturesAboveBoundary"]!.GetValue<int>());
        Assert.Empty(Schemas.CheckPassport(passport, feb2027Variant: false));
        Assert.Empty(Schemas.CheckPassport(passport, feb2027Variant: true));
        Assert.All(AttributeCatalog.Extensions, attribute => Assert.Contains(attribute, PassportStaticCore.NotSigned));
    }

    [Fact]
    public async Task APassportWithoutThem_ShouldStillPublish()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.Null(result.Problem);
        Assert.DoesNotContain(result.Report!.Warnings, warning => warning.StartsWith('E'));
    }
}
