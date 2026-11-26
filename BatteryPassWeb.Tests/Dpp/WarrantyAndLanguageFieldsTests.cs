using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// W05: the warranty (#19) is typed as a number with years or months, and stored in the official form (P8Y); a stored
/// value shows in the new field. Document and accident languages are chosen by name; the stored code doesn't change.
/// </summary>
public sealed class WarrantyAndLanguageFieldsTests
{
    private static readonly AttributeForm Warranty = AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(19));
    private const string Field = "v.warrantyPeriod";
    private const string Unit = "v.warrantyPeriod.unit";

    [Fact]
    public void TheWarranty_ShouldBeANumberWithYearsOrMonths()
    {
        Assert.Equal(InputKind.Duration, Assert.Single(Warranty.Inputs).Kind);
    }

    [Theory]
    [InlineData("8", "Y", "P8Y")]
    [InlineData("8", "M", "P8M")]
    [InlineData(" 10 ", null, "P10Y")]
    [InlineData("P3Y6M", "Y", "P3Y6M")] // typed the official way: kept as written
    public void ANumberAndAUnit_ShouldBeStoredInTheOfficialForm(string amount, string? unit, string expected)
    {
        var value = FormValues.Read(Warranty, name => name == Field ? amount : name == Unit ? unit : null);

        Assert.Equal(expected, value!.GetValue<string>());
    }

    [Fact]
    public void AnEmptyNumber_ShouldBeNoValueWhateverTheUnit()
    {
        Assert.Null(FormValues.Read(Warranty, name => name == Unit ? "M" : string.Empty));
    }

    [Theory]
    [InlineData("P8Y", "8", "Y")]
    [InlineData("P6M", "6", "M")]
    [InlineData("P1Y6M", "18", "M")]
    [InlineData("P30D", "P30D", "Y")]
    public void AStoredWarranty_ShouldShowInTheNewField(string stored, string amount, string unit)
    {
        var texts = FormValues.Show(Warranty, JsonValue.Create(stored));

        Assert.Equal((amount, unit), (texts[Field], texts[Unit]));
    }

    [Fact]
    public void AnUntouchedWarranty_ShouldNotCountAsAChange_AndEmptyingItShouldRemoveIt()
    {
        var editor = new RecordEditor(AttributeFormsTests.Forms, null!, null!, null!);
        var stored = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
        {
            ["warrantyPeriod"] = new() { Value = JsonValue.Create("P8Y"), GeneratedAtUtc = DateTime.UtcNow, Source = "form" },
        };
        var attributes = new[] { AttributeCatalog.ByNumber(19) };
        var stamp = editor.Stamp(attributes, stored, null);

        var untouched = editor.Read(attributes, stored, name => name == Field ? "8" : name == Unit ? "Y" : null, stamp);
        var months = editor.Read(attributes, stored, name => name == Field ? "8" : name == Unit ? "M" : null, stamp);
        var emptied = editor.Read(attributes, stored, name => name == Field ? string.Empty : name == Unit ? "Y" : null, stamp);

        Assert.Empty(untouched.Changed);
        Assert.Equal("P8M", months.Changed["warrantyPeriod"].GetValue<string>());
        Assert.Contains("warrantyPeriod", emptied.Removals);
    }

    [Fact]
    public void ThePublicPages_ShouldSayTheWarrantyInWords()
    {
        var line = Assert.Single(ValueWords.Lines(Warranty, JsonValue.Create("P8Y")));

        Assert.Equal("8 years", line.Text);
    }

    [Fact]
    public void EveryLanguageOfTheSchema_ShouldHaveAName()
    {
        var codes = AttributeFormsTests.Forms.Languages;

        Assert.Equal(43, codes.Count);
        Assert.All(codes, code => Assert.NotEqual(code, LanguageNames.Of(code)));
        Assert.Equal("English (United Kingdom)", LanguageNames.Of("en-GB"));
        Assert.Equal("Bulgarian (Bulgaria)", LanguageNames.Of(LanguageNames.ByName(codes)[0]));
        Assert.Equal(codes.Order(), LanguageNames.ByName(codes).Order()); // the same codes, ordered by name
    }
}
