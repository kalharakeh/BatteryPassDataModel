using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, G20 (decision 94): the EU's format requires the temperature range at which the minimum, nominal and
/// maximum voltage (#64 to #66) were measured. The file's schema leaves it optional; the app now asks for it wherever the
/// voltage is given. A voltage without it is kept and shown, and counted as missing: a save never refuses it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class VoltageTemperaturesTests(MongoFixture mongo)
{
    private const string Lacking = "the test temperatures";

    private static JsonObject Voltage(bool withTemperatures) => withTemperatures
        ? new JsonObject { ["voltValue"] = 520.0, ["voltUnit"] = "V", ["temperatureRangeLowerBoundaryValue"] = -20, ["temperatureRangeLowerBoundaryUnit"] = "°C", ["temperatureRangeUpperBoundaryValue"] = 45, ["temperatureRangeUpperBoundaryUnit"] = "°C" }
        : new JsonObject { ["voltValue"] = 520.0, ["voltUnit"] = "V" };

    [Theory]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(66)]
    public void AVoltage_ShouldAskForItsTestTemperatures(int number)
    {
        var attribute = AttributeCatalog.ByNumber(number);
        var form = AttributeFormsTests.Forms.For(attribute);

        Assert.All(form.Inputs, input => Assert.True(input.Required, input.Path));
        Assert.Equal(Lacking, PassportNeeds.Lacking(attribute, Voltage(withTemperatures: false)));
        Assert.Null(PassportNeeds.Lacking(attribute, Voltage(withTemperatures: true)));
        Assert.True(PassportNeeds.IsMissing(attribute, Voltage(withTemperatures: false), ModelAnswers.NotAnswered));
    }

    [Fact]
    public void ThePowerCapability_ShouldKeepItsTestTemperaturesOptional()
    {
        var power = AttributeCatalog.ByNumber(67);

        Assert.Contains(AttributeFormsTests.Forms.For(power).Inputs, input => input.Path.StartsWith("temperatureRange", StringComparison.Ordinal) && !input.Required);
        Assert.Null(PassportNeeds.Lacking(power, new JsonObject { ["wattValueAt80PercentStateOfCharge"] = 1, ["wattUnit"] = "W" }));
    }

    [Fact]
    public void APassportWithAVoltageWithoutTemperatures_ShouldNotPublish_NamingWhatIsMissing()
    {
        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        passport["batteryPassportData"]!["performanceAndDurability"]!["minimumVoltage"] = Voltage(withTemperatures: false);

        var report = new PublicationChecks(DppStoreTesting.Schemas).Check(passport, DppTestSeed.Answers);

        var line = Assert.Single(report.Blocking);
        Assert.Equal("#64 Minimum voltage is missing (the test temperatures)", line);
        Assert.Equal("#64 Minimum voltage (the test temperatures)", PublishWords.Item(line));
        Assert.Equal(64, PublishWords.Attribute(line)?.Number);
    }

    [Fact]
    public void TheModelsEditor_ShouldMarkAndCountAVoltageWithoutTemperatures()
    {
        var model = DppTestSeed.Load().Sources.Model;
        var before = RecordEditor.CountMissing(TargetKind.Model, model.Values, DppTestSeed.Answers);
        model.Values["minimumVoltage"] = new AttributeValue { Value = Voltage(withTemperatures: false), GeneratedAtUtc = DppTestSeed.GeneratedAtUtc, Source = "seed" };

        Assert.Equal(before + 1, RecordEditor.CountMissing(TargetKind.Model, model.Values, DppTestSeed.Answers));
        var field = new EditorField(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(64)), new Dictionary<string, string>(), 0, null, null, HasValue: true)
        {
            Answers = DppTestSeed.Answers,
            Lacking = Lacking,
        };
        Assert.True(field.IsMissing);
        Assert.Equal("Missing the test temperatures", field.MissingWords);
    }

    [Fact]
    public async Task APublishedBattery_ShouldStayPublished_AndGetNoNewVersionUntilTheTemperaturesAreThere()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        // A model saved before the rule: its minimum voltage has no temperatures. A save never refuses it.
        var saved = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["minimumVoltage"] = Voltage(withTemperatures: false) }, "form"));
        Assert.Equal(KeyOutcome.Saved, Assert.Single(saved.Keys).Outcome);

        var active = await rig.Store.GetActiveVersionAsync(rig.PackId);
        Assert.Equal(1, active!.Number);
        var waiting = await rig.Publication.WaitingAsync(rig.PackId);
        Assert.Contains("#64 Minimum voltage is missing (the test temperatures)", waiting!.Blocking);

        var filled = Voltage(withTemperatures: true);
        filled["temperatureRangeLowerBoundaryValue"] = -10;
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["minimumVoltage"] = filled }, "form"));

        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number);
    }
}
