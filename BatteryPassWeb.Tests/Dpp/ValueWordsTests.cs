using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Passport values in words on the public summary and the detail page (design 12).</summary>
public sealed class ValueWordsTests
{
    [Theory]
    [InlineData(21, """{"gramKilogramUnit":"kg","gramKilogramValue":512.5}""", "512.5 kg")]
    [InlineData(17, "\"2026-09\"", "September 2026")]
    [InlineData(19, "\"P8Y\"", "8 years")]
    [InlineData(19, "\"P1Y6M\"", "1 year 6 months")]
    [InlineData(20, """{"batteryCategoryValue":"industrial/non-stationary battery"}""", "industrial/non-stationary battery")]
    [InlineData(22, """{"batteryStatusValues":"original"}""", "original")]
    [InlineData(81, "12", "12 years")]
    [InlineData(82, "4000", "4,000")]
    [InlineData(63, """{"percentUnit":"%","percentageValue":76}""", "76 %")]
    [InlineData(1001, "3600", "3,600 minutes")]
    [InlineData(11, "\"VPC8C05-6788\"", "VPC8C05-6788")]
    public void ASingleValue_ShouldReadAsOneLine(int number, string json, string expected)
    {
        var line = Assert.Single(Lines(number, json));

        Assert.Null(line.Label);
        Assert.Equal(expected, line.Text);
        Assert.Null(line.Href);
    }

    [Fact]
    public void TheLastUpdate_ShouldReadAsATimeInTheAppsTimeZone()
    {
        var line = Assert.Single(Lines(4, "\"2026-10-28T09:30:00Z\""));

        Assert.Equal(ValueWords.Time(new DateTime(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc)), line.Text);
    }

    [Fact]
    public void AGroupedValue_ShouldGiveEachPartItsLabel()
    {
        var lines = Lines(66, """{"voltValue":650.0,"voltUnit":"V","temperatureRangeLowerBoundaryValue":-20,"temperatureRangeLowerBoundaryUnit":"°C","temperatureRangeUpperBoundaryValue":40,"temperatureRangeUpperBoundaryUnit":"°C"}""");

        Assert.Equal(
            [("Voltage", "650 V"), ("Test temperature from", "-20 °C"), ("Test temperature to", "40 °C")],
            lines.Select(line => (line.Label, line.Text)));
    }

    [Fact]
    public void AList_ShouldReadOneItemPerLine()
    {
        var lines = Lines(42, """["Cobalt","Lithium","Natural graphite"]""");

        Assert.Equal(["Cobalt", "Lithium", "Natural graphite"], lines.Select(line => line.Text));
    }

    [Fact]
    public void AListOfObjects_ShouldMarkWhereEachItemStarts()
    {
        var lines = Lines(44, """[{"chemicalName":"A","identifier":"1-2-3","hazardPhrases":["H301","H314"]},{"chemicalName":"B"}]""");

        Assert.Equal(
            [("Chemical name", "A", true), ("Identifier", "1-2-3", false), ("Hazard phrases", "H301, H314", false), ("Chemical name", "B", true)],
            lines.Select(line => (line.Label!, line.Text, line.StartsRow)));
    }

    [Theory]
    [InlineData(28, "EU declaration", "https://www.example.com/doc.pdf")]
    [InlineData(45, "Dismantling manual", "https://passport.example.com/dpp/documents/doc-1")]
    [InlineData(92, "Accident information", "https://passport.example.com/dpp/packs/pack-1/accidents")]
    public void ADocument_ShouldReadAsItsTitleLinkingItsAddress(int number, string title, string url)
    {
        var line = Assert.Single(Lines(number, $$"""{"contentType":"application/pdf","url":"{{url}}","language":"en-GB","resourceTitle":"{{title}}"}"""));

        Assert.Equal(title, line.Text);
        Assert.Equal(url, line.Href);
    }

    [Fact]
    public void AWebAddress_ShouldLinkItself()
    {
        var line = Assert.Single(Lines(36, "\"https://www.example.com/study\""));

        Assert.Equal("https://www.example.com/study", line.Href);
    }

    [Fact]
    public void NoValue_ShouldHaveNoLines()
    {
        Assert.Empty(ValueWords.Lines(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(21)), null));
    }

    [Fact]
    public void EveryValueOfTheSeedPassport_ShouldHaveWords()
    {
        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);

        foreach (var attribute in AttributeCatalog.All)
        {
            JsonNode? value = passport;
            foreach (var name in attribute.Path.Split('.'))
            {
                value = value?[name];
            }

            if (value is not null)
            {
                Assert.True(ValueWords.Lines(AttributeFormsTests.Forms.For(attribute), value).Count > 0, attribute.Label);
            }
        }
    }

    private static IReadOnlyList<ValueLine> Lines(int number, string json) =>
        ValueWords.Lines(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(number)), JsonNode.Parse(json));
}
