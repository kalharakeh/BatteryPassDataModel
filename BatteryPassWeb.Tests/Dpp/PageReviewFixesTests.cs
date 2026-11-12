using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, the whole-page review at 1280 px (manager's request): one place per action, the header's (the Status
/// section and the overview repeated Change status, Add a note and Publish passport), and a clearer label for the model
/// and its family ("Compact 7M · Compact family", not "Compact 7M of Compact"). The manager's answers: the passport status
/// in the badge's words wherever it shows (b), and the cluster admin's "Hand over…" opening its window directly (c).
/// </summary>
public sealed class PageReviewFixesTests
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void EachAction_ShouldBeInTheHeaderOnly()
    {
        var team = View("Batteries", "Battery.cshtml");
        var header = team[..team.IndexOf("</header>", StringComparison.Ordinal)];
        var card = View("Batteries", "_StatusCard.cshtml");

        foreach (var window in new[] { "publish-dialog", "status-dialog", "note-dialog" })
        {
            Assert.Single(team.Split($"data-open-dialog=\"{window}\"").Skip(1));
            Assert.Contains($"data-open-dialog=\"{window}\"", header, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("data-open-dialog", card, StringComparison.Ordinal);
        Assert.Contains("among the page's actions.", card, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("active", "Active")]
    [InlineData("marked-for-deletion", "Marked for deletion")]
    [InlineData("inactive", "Inactive")]
    public void ThePassportStatus_ShouldShowInTheBadgesWords(string stored, string shown)
    {
        var lines = ValueWords.Lines(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(2)), new JsonObject { ["dppStatusValue"] = stored }); // the schema's shape

        Assert.Equal(shown, Assert.Single(lines).Text);
    }

    [Fact]
    public void TheClusterAdminsHandOver_ShouldOpenItsWindowFromTheHeader_WithNoSecondButton()
    {
        var page = View("ClusterAdmin", "Battery.cshtml");
        var header = page[..page.IndexOf("</header>", StringComparison.Ordinal)];
        var card = View("ClusterAdmin", "_HandOverCard.cshtml");

        Assert.Contains("data-open-dialog=\"handover-dialog\"><svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-handover\" /></svg>Hand over…</button>", header, StringComparison.Ordinal);
        Assert.DoesNotContain("section=handover", page, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"~/Views/ClusterAdmin/_HandOverDialog.cshtml\", Model.HandOver)", page[page.IndexOf("</header>", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.DoesNotContain("<button", card, StringComparison.Ordinal);
        Assert.DoesNotContain("<dialog", card, StringComparison.Ordinal);
        Assert.Contains("Waiting for @Model.ReceiverName", card, StringComparison.Ordinal); // the section keeps the waiting state
    }

    [Fact]
    public void TheLabel_ShouldNameTheModelAndItsFamily()
    {
        Assert.Contains("@sources.Model.Name</a> · @Model.Family.Name family</span>", View("Batteries", "Battery.cshtml"), StringComparison.Ordinal);
        Assert.Contains("Battery · @Model.Sources.Model.Name · @Model.Family.Name family</span>", View("ClusterAdmin", "Battery.cshtml"), StringComparison.Ordinal);
    }
}
