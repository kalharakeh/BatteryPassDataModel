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
/// Package R1, G21 (decision 94): the EU's format gives the idle (not-in-use) temperature range together with the test
/// it comes from, a text in a language. #88 and #89 keep the bounds; E10 is the text, required with them.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class StorageTestTextTests(MongoFixture mongo)
{
    private static AttributeDefinition Test => AttributeCatalog.FindByProperty(AttributeCatalog.NotInUseTemperatureRangeTest)!;

    [Fact]
    public void TheStorageTest_ShouldBeARequiredPublicTextOfTheModel_BesideTheIdleRange()
    {
        Assert.Equal(("E10", StorageLocation.Model, Audience.Public), (Test.Label, Test.Storage, Test.Audience));
        Assert.Equal(AttributeCatalog.ByNumber(88).Section, Test.Section);
        Assert.Equal(Need.Required, PassportNeeds.NeedOf(Test, ModelAnswers.NotAnswered));
        Assert.Contains(Test, RecordEditor.AttributesOf(TargetKind.Model));

        var input = Assert.Single(AttributeFormsTests.Forms.For(Test).Inputs);
        Assert.Equal((InputKind.LanguageText, true), (input.Kind, input.Required));
    }

    [Theory]
    [InlineData("""{"en-GB":"Storage test to IEC 62660-1"}""", true)]
    [InlineData("""{"en":"Storage test"}""", false)]
    [InlineData("\"Storage test\"", false)]
    public void TheStorageTest_ShouldBeKeptAsALanguageMap(string json, bool valid)
    {
        Assert.Equal(valid, new AttributeValueValidator(DppStoreTesting.Schemas).Validate(Test, JsonNode.Parse(json)).Count == 0);
    }

    [Fact]
    public async Task AStorageTestSentAsPlainText_ShouldBeSavedInThePassportsLanguage()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var saved = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { [AttributeCatalog.NotInUseTemperatureRangeTest] = JsonValue.Create("Storage at -30 °C to 55 °C for 30 days") }, "upload"));

        Assert.Equal(KeyOutcome.Saved, Assert.Single(saved.Keys).Outcome);
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;
        Assert.Equal("""{"en-GB":"Storage at -30 °C to 55 °C for 30 days"}""", model.Values[AttributeCatalog.NotInUseTemperatureRangeTest].Value.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    [Fact]
    public void APassportWithoutTheStorageTest_ShouldNotPublish_AndShowTheTextWhenItHasOne()
    {
        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        var performance = passport["batteryPassportData"]!["performanceAndDurability"]!.AsObject();
        Assert.Equal("Storage test to IEC 62660-1 between -30 °C and 55 °C", PassportLanguage.TextOf(performance[AttributeCatalog.NotInUseTemperatureRangeTest]));
        Assert.Equal("Storage test to IEC 62660-1 between -30 °C and 55 °C", Assert.Single(ValueWords.Lines(AttributeFormsTests.Forms.For(Test), performance[AttributeCatalog.NotInUseTemperatureRangeTest])).Text);

        performance.Remove(AttributeCatalog.NotInUseTemperatureRangeTest);

        Assert.Contains("E10 Test of the idle temperature range is missing", new PublicationChecks(DppStoreTesting.Schemas).Check(passport, DppTestSeed.Answers).Blocking);
    }
}
