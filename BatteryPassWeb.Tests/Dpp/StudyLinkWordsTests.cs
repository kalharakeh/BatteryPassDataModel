using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's choice 6 (3 Nov 2026): the carbon footprint study (#36) showed its web address as the link's
/// words; it reads "Carbon footprint study", the address on hover and in the link itself.
/// </summary>
public sealed class StudyLinkWordsTests
{
    [Fact]
    public void TheStudysLink_ShouldReadAsWords_AndLeadToTheAddress()
    {
        const string Address = "https://www.example.com/demo/compact-7m/carbon-footprint-study";

        var line = Assert.Single(ValueWords.Lines(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(36)), JsonValue.Create(Address)));

        Assert.Equal(new ValueLine(null, "Carbon footprint study", Address), line);
    }

    [Fact]
    public void ALinksAddress_ShouldShowOnHover() =>
        Assert.Contains("<a href=\"@part.Href\" target=\"_blank\" rel=\"noreferrer\" title=\"@part.Href\">@part.Text</a>",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml")), StringComparison.Ordinal);
}
