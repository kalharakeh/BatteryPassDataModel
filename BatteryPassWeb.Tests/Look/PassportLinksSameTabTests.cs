using System.Text.RegularExpressions;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 4 (3 Nov 2026): passport pages opened in a new tab from the battery pages but in the
/// same tab from the lists; they open in the same tab everywhere now. Files (documents, the QR picture, the API's data)
/// still open in a new tab, as decided on 29 Oct.
/// </summary>
public sealed class PassportLinksSameTabTests
{
    [Theory]
    [InlineData("Batteries", "Battery.cshtml")]
    [InlineData("ClusterAdmin", "Battery.cshtml")]
    [InlineData("Batteries", "_VersionsCard.cshtml")]
    [InlineData("Batteries", "_AccidentsCard.cshtml")]
    [InlineData("Shared", "_RowActions.cshtml")]
    public void APassportPagesLink_ShouldOpenInTheSameTab(string folder, string view)
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", folder, view));
        var passportLinks = Regex.Matches(page, "<a [^>]*href=\"(/dpp/[^\"]*|@ReaderPages\\.AccidentsHref[^\"]*|@publicPage|@detail)\"[^>]*>").Select(match => match.Value).ToArray();

        Assert.All(passportLinks, link => Assert.DoesNotContain("target=\"_blank\"", link, StringComparison.Ordinal));
    }

    [Fact]
    public void Files_ShouldStillOpenInANewTab()
    {
        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var accidents = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_AccidentsCard.cshtml"));

        Assert.Contains("href=\"@PassportPages.QrHref(Model.Identifier)\" target=\"_blank\" rel=\"noreferrer\"", battery, StringComparison.Ordinal);
        Assert.Contains("<a href=\"@ReaderPages.DocumentHref(report.Id)\" target=\"_blank\" rel=\"noreferrer\">", accidents, StringComparison.Ordinal);
    }
}
