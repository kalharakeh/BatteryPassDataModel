using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, G16 (decision 94): #42 is chosen from the EU's 34 critical raw materials and kept as the EU's addresses,
/// shown by the EU's labels. A name kept from before is matched where the meaning is plain; one that isn't stays, shows
/// as not on the list and is missing a choice. The BatteryPass-Ready passport keeps #42 as names. A model with cadmium or
/// lead above the limit says which symbol it shows (E9, the owner's answer O13).
/// </summary>
public sealed class CriticalRawMaterialsTests
{
    private const string Raw = EuCodeLists.RawMaterialBase;
    private const string Symbol = EuCodeLists.ChemicalSymbolBase;

    private static AttributeDefinition Materials => AttributeCatalog.ByNumber(42);

    private static AttributeDefinition Symbols => AttributeCatalog.FindByProperty(AttributeCatalog.ChemicalSymbol)!;

    private static AttributeForm MaterialsForm => AttributeFormsTests.Forms.For(Materials);

    private static JsonArray List(params string[] entries) => new(entries.Select(entry => (JsonNode)JsonValue.Create(entry)).ToArray());

    [Fact]
    public void TheEusList_ShouldHold34Materials_FoundByAddressCodeLabelOrAPlainOtherName()
    {
        Assert.Equal(34, EuCodeLists.RawMaterials.Count);
        Assert.Equal("Nickel – battery grade", EuCodeLists.RawMaterials[Raw + "nickelBatteryGrade"]);
        Assert.Equal(Raw + "lithium", EuCodeLists.FindRawMaterial("Lithium"));
        Assert.Equal(Raw + "lithium", EuCodeLists.FindRawMaterial("lithium"));
        Assert.Equal(Raw + "lithium", EuCodeLists.FindRawMaterial(Raw + "lithium"));
        Assert.Equal(Raw + "graphite", EuCodeLists.FindRawMaterial("Natural graphite"));
        Assert.Equal(Raw + "nickelBatteryGrade", EuCodeLists.FindRawMaterial(" nickel "));
        Assert.Equal(Raw + "bauxiteAluminaAluminium", EuCodeLists.FindRawMaterial("Aluminium"));
        Assert.Null(EuCodeLists.FindRawMaterial("Rare earth elements")); // heavy or light: not plain
        Assert.Null(EuCodeLists.FindRawMaterial("Unobtainium"));
    }

    [Fact]
    public void Materials_ShouldBeTickedFromTheEusList_ByTheEusLabels()
    {
        var input = Assert.Single(MaterialsForm.Inputs);

        Assert.Equal((FormKind.Single, InputKind.Choices, 34), (MaterialsForm.Kind, input.Kind, input.Options.Count));
        Assert.Equal("Graphite", input.Words(Raw + "graphite"));
        Assert.Equal("v.criticalRawMaterials", MaterialsForm.FieldName(input)); // the workbook's key stays model|criticalRawMaterials
    }

    [Fact]
    public void TickedOrTypedMaterials_ShouldBeKeptAsTheEusAddresses_InTheListsOrder_AndOthersAsTyped()
    {
        Assert.Equal(List(Raw + "cobalt", Raw + "lithium"), FormValues.Read(MaterialsForm, _ => $"{Raw}lithium\n{Raw}cobalt"), JsonNode.DeepEquals);
        Assert.Equal(
            List(Raw + "cobalt", Raw + "graphite", Raw + "nickelBatteryGrade", "Unobtainium"),
            FormValues.Read(MaterialsForm, _ => "Nickel\nUnobtainium\nNatural graphite\nCobalt"),
            JsonNode.DeepEquals);
    }

    [Fact]
    public void NamesKeptFromBefore_ShouldShowTicked_AndOneNotOnTheListAsItsOwnBox()
    {
        var shown = FormValues.Show(MaterialsForm, List("Lithium", "Natural graphite", "Unobtainium"));

        Assert.Equal($"{Raw}lithium\n{Raw}graphite\nUnobtainium", Assert.Single(shown).Value); // one entry per line (round two, L3)
    }

    [Fact]
    public void ANameNotOnTheList_ShouldBeMissingAChoice_AndBlockPublishing_WhileNamesThatMatchDont()
    {
        Assert.Null(PassportNeeds.Lacking(Materials, List("Cobalt", "Lithium", "Natural graphite", "Nickel")));
        Assert.Null(PassportNeeds.Lacking(Materials, List(Raw + "cobalt")));
        Assert.Equal("a choice from the EU's list: \"Unobtainium\" isn't on it", PassportNeeds.Lacking(Materials, List("Lithium", "Unobtainium")));

        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        passport["batteryPassportData"]!["batteryMaterialsAndComposition"]!["criticalRawMaterials"] = List("Lithium", "Unobtainium");

        Assert.Contains("#42 Critical raw materials is missing (a choice from the EU's list: \"Unobtainium\" isn't on it)", new PublicationChecks(DppStoreTesting.Schemas).Check(passport, DppTestSeed.Answers).Blocking);
    }

    [Fact]
    public void ThePassport_ShouldKeepMaterialsAsNames_TheEusLabelForAnAddress_AndANameKeptFromBeforeAsItWas()
    {
        Assert.Equal(List("Cobalt", "Nickel – battery grade"), PassportAssembler.RawMaterialWords(List(Raw + "cobalt", Raw + "nickelBatteryGrade")), JsonNode.DeepEquals);
        Assert.Equal(List("Cobalt", "Natural graphite", "Nickel"), PassportAssembler.RawMaterialWords(List("Cobalt", "Natural graphite", "Nickel")), JsonNode.DeepEquals);

        // On the pages a name kept from before reads exactly as stored (round two, M1: as it was signed).
        var lines = ValueWords.Lines(MaterialsForm, List(Raw + "cobalt", "Natural graphite", "Unobtainium"));
        Assert.Equal(["Cobalt", "Natural graphite", "Unobtainium"], lines.Select(line => line.Text));
    }

    [Fact]
    public void TheSymbol_ShouldBeNeededOnlyWhenCadmiumOrLeadIsAboveTheLimit_AndBeCadmiumLeadOrBoth()
    {
        Assert.Equal(("E9", Audience.Public), (Symbols.Label, Symbols.Audience));
        Assert.Equal(ModelQuestion.CadmiumOrLeadAboveLimit, ModelQuestions.QuestionOf(Symbols));
        Assert.Equal(Need.Required, PassportNeeds.NeedOf(Symbols, ModelAnswers.NotAnswered with { CadmiumOrLeadAboveLimit = true }));
        Assert.Equal(Need.NotNeeded, PassportNeeds.NeedOf(Symbols, ModelAnswers.NotAnswered with { CadmiumOrLeadAboveLimit = false }));

        var form = AttributeFormsTests.Forms.For(Symbols);
        Assert.Equal(List(Symbol + "cadmiumSymbol", Symbol + "leadSymbol"), FormValues.Read(form, _ => "Lead\nCadmium"), JsonNode.DeepEquals);

        var validator = new AttributeValueValidator(DppStoreTesting.Schemas);
        Assert.Empty(validator.Validate(Symbols, List(Symbol + "leadSymbol")));
        Assert.NotEmpty(validator.Validate(Symbols, List(Symbol + "noneSymbol")));
        Assert.NotEmpty(validator.Validate(Symbols, List(Symbol + "leadSymbol", Symbol + "leadSymbol")));
        Assert.NotEmpty(validator.Validate(Symbols, new JsonArray()));
        Assert.Equal(List(Symbol + "leadSymbol"), new AttributeValueNormalizer(DppStoreTesting.Schemas).Normalize(Symbols, JsonValue.Create("lead")), JsonNode.DeepEquals);
        Assert.Equal(["Lead (Pb)"], ValueWords.Lines(form, List(Symbol + "leadSymbol")).Select(line => line.Text));
    }
}
