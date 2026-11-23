using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
/// Package R1, G11 (decision 94): the EU's battery format names the manufacturing place as a city and/or region and a
/// country in three letters (ISO 3166 alpha-3). The site keeps #16's free text and gets the three values beside it.
/// </summary>
public sealed class ManufacturingPlaceTests
{
    private static readonly AttributeValueValidator Validator = new(DppStoreTesting.Schemas);

    private static AttributeDefinition City => AttributeCatalog.FindByProperty(AttributeCatalog.ManufacturingCity)!;

    private static AttributeDefinition Region => AttributeCatalog.FindByProperty(AttributeCatalog.ManufacturingRegion)!;

    private static AttributeDefinition Country => AttributeCatalog.FindByProperty(AttributeCatalog.ManufacturingCountry)!;

    [Fact]
    public void TheManufacturingPlace_ShouldBeACityARegionAndACountryOnTheSite_PublicBesideTheFreeText()
    {
        Assert.Equal(("E5", "E6", "E7"), (City.Label, Region.Label, Country.Label));
        Assert.All(new[] { City, Region, Country }, attribute =>
        {
            Assert.True(attribute.IsExtension);
            Assert.Equal((StorageLocation.Site, Audience.Public, "identifiersAndProductData"), (attribute.Storage, attribute.Audience, attribute.Section));
            Assert.Contains(attribute, RecordEditor.AttributesOf(TargetKind.Site));
        });

        // The EU requires the country and a city or a region; the app asks for the city and offers the region.
        Assert.Equal(Need.Required, PassportNeeds.NeedOf(City, ModelAnswers.NotAnswered));
        Assert.Equal(Need.Required, PassportNeeds.NeedOf(Country, ModelAnswers.NotAnswered));
        Assert.Equal(Need.Optional, PassportNeeds.NeedOf(Region, ModelAnswers.NotAnswered));
        Assert.NotNull(AttributeCatalog.FindByProperty("manufacturingPlace")); // #16 stays as it is
    }

    [Fact]
    public void TheCountryList_ShouldHoldEveryIso3166CountryByItsThreeLetters()
    {
        Assert.Equal(249, Countries.Codes.Count);
        Assert.All(Countries.Codes, code => Assert.Matches("^[A-Z]{3}$", code));
        Assert.Equal("Sweden", Countries.Names["SWE"]);
        Assert.Equal(("DEU", "FIN", "NOR", "GBR"), (Countries.Find("germany"), Countries.Find("Finland"), Countries.Find("nor"), Countries.Find("United Kingdom")));
        Assert.Null(Countries.Find("Scandinavia"));
    }

    [Fact]
    public void TheCountry_ShouldBeChosenFromTheListByName_AndKeptAsItsCode()
    {
        var form = AttributeFormsTests.Forms.For(Country);

        var input = Assert.Single(form.Inputs);
        Assert.Equal((FormKind.Single, InputKind.Select, true), (form.Kind, input.Kind, input.Required));
        Assert.Equal(Countries.Codes, input.Options);
        Assert.Equal("Sweden", input.Words("SWE"));

        // Typed as a name (the workbook) or chosen as a code (the page), it is kept as the code.
        Assert.Equal("\"SWE\"", FormValues.Read(form, _ => "Sweden")!.ToJsonString());
        Assert.Equal("\"SWE\"", FormValues.Read(form, _ => "SWE")!.ToJsonString());

        // The pages say it as the name.
        Assert.Equal("Sweden", Assert.Single(ValueWords.Lines(form, JsonValue.Create("SWE"))).Text);
    }

    [Theory]
    [InlineData("\"SWE\"", true)]
    [InlineData("\"swe\"", false)]
    [InlineData("\"Sweden\"", false)]
    [InlineData("\"XYZ\"", false)]
    [InlineData("752", false)]
    public void ACountry_ShouldBeACodeOfTheList(string json, bool valid)
    {
        Assert.Equal(valid, Validator.Validate(Country, JsonNode.Parse(json)).Count == 0);
    }

    [Theory]
    [InlineData("\"Södertälje\"", true)]
    [InlineData("\"   \"", false)]
    [InlineData("\"two\\nlines\"", false)]
    [InlineData("12", false)]
    public void ACity_ShouldBeOneLineOfText(string json, bool valid)
    {
        Assert.Equal(valid, Validator.Validate(City, JsonNode.Parse(json)).Count == 0);
        Assert.Equal(InputKind.Text, Assert.Single(AttributeFormsTests.Forms.For(City).Inputs).Kind);
    }

    [Fact]
    public void APassportWithoutTheCityOrTheCountry_ShouldNotPublish_AndOneWithoutTheRegionShould()
    {
        var checks = new PublicationChecks(DppStoreTesting.Schemas);
        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        var ident = passport["batteryPassportData"]!["identifiersAndProductData"]!.AsObject();
        Assert.Equal(("Stockholm", "SWE"), (ident["manufacturingCity"]!.GetValue<string>(), ident["manufacturingCountry"]!.GetValue<string>()));

        ident.Remove("manufacturingRegion");
        Assert.True(checks.Check(passport, DppTestSeed.Answers).CanPublish);

        ident.Remove("manufacturingCity");
        ident.Remove("manufacturingCountry");
        var report = checks.Check(passport, DppTestSeed.Answers);
        Assert.False(report.CanPublish);
        Assert.Contains("E5 Manufacturing city is missing", report.Blocking);
        Assert.Contains("E7 Manufacturing country is missing", report.Blocking);
    }

    [Fact]
    public void ASiteWithoutTheNewValues_ShouldCountThemAsMissing()
    {
        var site = DppTestSeed.Load().Sources.Site;
        var before = RecordEditor.CountMissing(TargetKind.Site, site.Values, ModelAnswers.NotAnswered);
        site.Values.Remove("manufacturingCity");
        site.Values.Remove("manufacturingCountry");
        site.Values.Remove("manufacturingRegion");

        Assert.Equal(before + 2, RecordEditor.CountMissing(TargetKind.Site, site.Values, ModelAnswers.NotAnswered));
        Assert.Matches(new Regex("^E[0-9]+$"), City.Label);
    }
}
