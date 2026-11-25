using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, round two (review M2): a save that would make published batteries lack a value they didn't lack before
/// (a voltage without its test temperatures, a #42 name off the EU's list) is refused, as removing a value they need is
/// refused. A value already lacking stays accepted, so the team can fill values one at a time.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class LackingRefusalTests(MongoFixture mongo)
{
    private static JsonObject Voltage(double volts, bool withTemperatures) => withTemperatures
        ? new JsonObject { ["voltValue"] = volts, ["voltUnit"] = "V", ["temperatureRangeLowerBoundaryValue"] = -20, ["temperatureRangeLowerBoundaryUnit"] = "°C", ["temperatureRangeUpperBoundaryValue"] = 45, ["temperatureRangeUpperBoundaryUnit"] = "°C" }
        : new JsonObject { ["voltValue"] = volts, ["voltUnit"] = "V" };

    private static ChangeSet ModelChange(string key, JsonNode value) =>
        new(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { [key] = value }, "form");

    [Fact]
    public async Task ClearingAVoltagesTemperaturesOnAPublishedModel_ShouldBeRefused_NamingWhatWouldBeMissing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, ModelChange("minimumVoltage", Voltage(520, withTemperatures: false)));

        var key = Assert.Single(result.Keys);
        Assert.Equal(KeyOutcome.Rejected, key.Outcome);
        Assert.Contains("the test temperatures", key.Message, StringComparison.Ordinal);
        Assert.Contains("the test temperatures", RefusalWording.Describe(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(64)), key.Message), StringComparison.Ordinal);
        var stored = (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values["minimumVoltage"].Value;
        Assert.NotNull(stored["temperatureRangeLowerBoundaryValue"]);
        Assert.Null(await rig.Publication.WaitingAsync(rig.PackId)); // nothing held back
    }

    [Fact]
    public async Task ANameOffTheListOnAPublishedModel_ShouldBeRefused()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, ModelChange(AttributeCatalog.CriticalRawMaterials, new JsonArray("Lithium", "Unobtainium")));

        var key = Assert.Single(result.Keys);
        Assert.Equal(KeyOutcome.Rejected, key.Outcome);
        Assert.Contains("Unobtainium", key.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AValueAlreadyLacking_ShouldStayAccepted_SoValuesCanBeFilledOneAtATime()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        // A model saved before the rule: its minimum voltage has no temperatures (as data from v3.8 can be).
        await rig.Store.SetValuesAsync<BatteryModel>("model-test", new Dictionary<string, AttributeValue>
        {
            ["minimumVoltage"] = new() { Value = Voltage(520, withTemperatures: false), GeneratedAtUtc = DppTestSeed.GeneratedAtUtc, Source = "form" },
        });

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, ModelChange("minimumVoltage", Voltage(525, withTemperatures: false)));

        Assert.Equal(KeyOutcome.Saved, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task TheSameSaveOnADraftsModel_ShouldBeAccepted()
    {
        var rig = await PublicationTesting.RigAsync(mongo); // nothing published

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, ModelChange("minimumVoltage", Voltage(520, withTemperatures: false)));

        Assert.Equal(KeyOutcome.Saved, Assert.Single(result.Keys).Outcome);
    }
}
