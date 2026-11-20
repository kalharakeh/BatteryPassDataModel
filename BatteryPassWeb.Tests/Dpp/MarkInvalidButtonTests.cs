using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// U11: for market surveillance, "Mark as invalid…" is a button inside the Purchaser card (the approved mock-up S6), not a
/// strip above the first card. It opens the window the team has: the reason is required, then a confirm step names the
/// battery and says that only the team can undo it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkInvalidButtonTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task MarketSurveillance_ShouldGetTheTeamsWindowNamingTheBattery()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var id = PassportPagesTests.Identifier(rig);

        var page = (await PassportPagesTests.Pages(rig).DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), id))!;
        var team = (await PassportPagesTests.Pages(rig).DetailAsync(Reader.With("team", ReaderRole.Team), id))!;

        var window = page.Marking!;
        Assert.Equal($"{PassportPages.DetailHref(page.Address)}/invalid", window.Action);
        Assert.Equal(page.Text(11), window.BatteryName);
        Assert.True(window.BySurveillance);
        Assert.Null(team.Marking); // the team uses its battery page
    }

    [Fact]
    public void TheButton_ShouldSitInThePurchaserCard_NotInAStripAboveThePage()
    {
        var hero = Read("Views", "Passport", "_PassportHero.cshtml");
        var tabs = Read("Views", "Passport", "_PassportTabs.cshtml");

        Assert.DoesNotContain("<details class=\"bp-warning\" data-mark-invalid>", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary>Mark as invalid…</summary>", hero, StringComparison.Ordinal);
        Assert.Contains("Model.Marking is { } marking", hero, StringComparison.Ordinal);
        Assert.Contains("~/Views/Batteries/_MarkDialog.cshtml", hero, StringComparison.Ordinal); // the window the team has
        var purchaser = tabs[tabs.IndexOf("tab.Key == \"purchaser\" && Model.Marking is not null", StringComparison.Ordinal)..];
        Assert.Contains("<button type=\"button\" class=\"bp-secondary-button\" data-passport-dialog=\"mark-dialog\">Mark as invalid…</button>", purchaser[..700], StringComparison.Ordinal);
    }

    [Fact]
    public void TheWindow_ShouldNeedAReasonThenConfirmNamingTheBatteryAndWhoUndoes()
    {
        var window = Read("Views", "Batteries", "_MarkDialog.cshtml");

        Assert.Contains("name=\"reason\"", window, StringComparison.Ordinal);
        Assert.Contains(" required", window[window.IndexOf("name=\"reason\"", StringComparison.Ordinal)..][..200], StringComparison.Ordinal);
        Assert.Contains("data-step=\"2\"", window, StringComparison.Ordinal); // the confirm step
        Assert.Contains("Model.BatteryName", window, StringComparison.Ordinal);
        Assert.Contains("Only the team can undo it.", window, StringComparison.Ordinal);
        Assert.Contains("Model.BySurveillance", window, StringComparison.Ordinal); // no "for deletion" choice for surveillance
    }
}
