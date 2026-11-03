using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 2 (3 Nov 2026): removing a draft battery, a family, a model, a batch, a site or an
/// operator can't be undone, and happened on one click once the fold was open; each now asks first in the app's
/// "Please confirm" window, as deleting a cluster, an account or a token already did. The window's script stands on its
/// own (bp-confirm.js), so these pages don't take the admin pages' other behaviour.
/// </summary>
public sealed class ConfirmRemovalsTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Theory]
    [InlineData("Batteries", "Battery.cshtml", "Remove draft battery</button>")]
    [InlineData("ProductData", "Family.cshtml", "Remove battery family</button>")]
    [InlineData("ProductData", "_RecordEditor.cshtml", "Remove @kindWord</button>")]
    public void EachRemoval_ShouldAskFirst(string folder, string view, string button)
    {
        var page = Read("web", "Views", folder, view);
        var tag = page[page.LastIndexOf("<button", page.IndexOf(button, StringComparison.Ordinal), StringComparison.Ordinal)..page.IndexOf(button, StringComparison.Ordinal)];

        Assert.Contains("data-confirm-action data-confirm-title=", tag, StringComparison.Ordinal);
        Assert.Contains("It can't be undone.", tag, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Batteries", "Battery.cshtml")]
    [InlineData("ProductData", "Family.cshtml")]
    [InlineData("ProductData", "Batch.cshtml")]
    [InlineData("ProductData", "SitesAndOperators.cshtml")]
    public void EveryPageWithARemoval_ShouldHaveTheWindowAndItsScript(string folder, string view)
    {
        var page = Read("web", "Views", folder, view);

        Assert.Contains("@await Html.PartialAsync(\"_ConfirmModal\")", page, StringComparison.Ordinal);
        Assert.Contains("<script src=\"~/js/bp-confirm.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("bp-admin-pages.js", page, StringComparison.Ordinal); // its dialogs would open twice beside the page's own
    }

    [Fact]
    public void TheWindowsScript_ShouldStandOnItsOwn_AndFindOnlyTheSharedWindow()
    {
        var confirm = Read("web", "wwwroot", "js", "bp-confirm.js");
        var admin = Read("web", "wwwroot", "js", "bp-admin-pages.js");

        Assert.Contains("document.querySelector('.bp-confirm-modal[data-confirm-modal]')", confirm, StringComparison.Ordinal); // not the editor's "Keep editing" window
        Assert.DoesNotContain("data-confirm-action", admin, StringComparison.Ordinal);
        foreach (var page in new[] { Read("web", "Views", "Admin", "Clusters.cshtml"), Read("web", "Views", "ClusterAdmin", "Users.cshtml"), Read("web", "Views", "ClusterAdmin", "ApiTokens.cshtml") })
        {
            Assert.Contains("<script src=\"~/js/bp-confirm.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
        }
    }
}
