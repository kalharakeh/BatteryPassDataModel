using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, G23 (decision 94): the EU's format requires ten end-of-life texts (wasteBatteryPreventionManagementInfo)
/// where the file has three document links. They are one group on the model, E11 to E20, texts in the passport's
/// language; #54 to #56, #27 and #44 stay as they are.
/// </summary>
public sealed class EndOfLifeTextsTests
{
    /// <summary>The EU's ten required texts, in the EU's order (pub-batteries.schema.json, wasteBatteryPreventionManagementInfo).</summary>
    private static readonly string[] EuNames =
    [
        "endUserRoleInWastePrevention", "endUserRoleInSeparateCollection", "separateCollectionPoints", "takeBackCollectionPoints",
        "preparationForReuseFacilities", "preparationForRepurposingFacilities", "wasteBatteryTreatmentFacilities",
        "wasteBatterySafetyInstructions", "batteryLabelSymbolMeaning", "batterySubstancesImpact",
    ];

    private static IReadOnlyList<AttributeDefinition> Texts => EuNames.Select(name => AttributeCatalog.FindByProperty(name)!).ToArray();

    [Fact]
    public void TheTenTexts_ShouldBeRequiredPublicTextsOfTheModel_UnderTheEusNames()
    {
        Assert.Equal(Enumerable.Range(11, 10).Select(number => $"E{number}"), Texts.Select(text => text.Label));
        Assert.All(Texts, text =>
        {
            Assert.Equal((StorageLocation.Model, Audience.Public, "circularityAndResourceEfficiency"), (text.Storage, text.Audience, text.Section));
            Assert.Equal(Need.Required, PassportNeeds.NeedOf(text, ModelAnswers.NotAnswered));
            Assert.Equal(InputKind.LanguageText, Assert.Single(AttributeFormsTests.Forms.For(text).Inputs).Kind);
            Assert.False(string.IsNullOrWhiteSpace(Assert.Single(AttributeFormsTests.Forms.For(text).Inputs).Hint)); // keyed off the catalogue (round two, L9)
            Assert.StartsWith("End of life: ", text.Name, StringComparison.Ordinal);
        });

        // The links and texts the file has stay.
        Assert.All(new[] { 27, 44, 54, 55, 56 }, number => Assert.False(AttributeCatalog.ByNumber(number).IsExtension));
    }

    [Fact]
    public void ATextMayHoldALink()
    {
        var value = JsonNode.Parse("""{"en-GB":"Collection points: https://www.sib-scania-eubp.eu/battery/collection-points"}""");

        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(Texts[2], value));
        Assert.Equal("Collection points: https://www.sib-scania-eubp.eu/battery/collection-points", Assert.Single(ValueWords.Lines(AttributeFormsTests.Forms.For(Texts[2]), value)).Text);
    }

    [Fact]
    public void APassportWithoutOneOfTheTexts_ShouldNotPublish_NamingIt()
    {
        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        var circularity = passport["batteryPassportData"]!["circularityAndResourceEfficiency"]!.AsObject();
        Assert.All(EuNames, name => Assert.NotNull(PassportLanguage.TextOf(circularity[name])));
        Assert.True(new PublicationChecks(DppStoreTesting.Schemas).Check(passport, DppTestSeed.Answers).CanPublish);

        circularity.Remove("takeBackCollectionPoints");

        var line = Assert.Single(new PublicationChecks(DppStoreTesting.Schemas).Check(passport, DppTestSeed.Answers).Blocking);
        Assert.Equal("E14 End of life: take-back points is missing", line);
        Assert.Equal("E14", PublishWords.Attribute(line)?.Label);
    }

    [Fact]
    public void AModelWithoutTheTexts_ShouldCountTenMissing()
    {
        var model = DppTestSeed.Load().Sources.Model;
        foreach (var name in EuNames)
        {
            model.Values.Remove(name);
        }

        Assert.Equal(10, RecordEditor.CountMissing(TargetKind.Model, model.Values, DppTestSeed.Answers));
    }
}
