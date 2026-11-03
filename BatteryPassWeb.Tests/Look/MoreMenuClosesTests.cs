using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's item 10 (a bug found testing round 2): More → "Mark as invalid or for deletion…" → Cancel
/// left the More menu open. A window opened from the menu now closes it, focus comes back to "More" when the window
/// closes (Cancel or Escape), and Escape closes an open menu too. Only the More menu: the header's "More details" fold
/// also holds a window's button (the cluster's "Change") and stays as it is.
/// </summary>
public sealed class MoreMenuClosesTests
{
    [Fact]
    public void AWindowOpenedFromMore_ShouldCloseTheMenu_AndGiveFocusBack()
    {
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-record-editor.js"));

        Assert.Contains("const menu = button.closest('details.bp-more');", script, StringComparison.Ordinal);
        Assert.Contains("menu.open = false;", script, StringComparison.Ordinal);
        Assert.Contains("dialog?.addEventListener('close', () => menu.querySelector(':scope > summary')?.focus(), { once: true });", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('details.bp-more[open]').forEach((menu) => {", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMoreMenusWindows_ShouldOpenThroughTheSharedOpener()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var menu = page[page.IndexOf("<details class=\"bp-more\">", StringComparison.Ordinal)..page.IndexOf("</details>", page.IndexOf("<details class=\"bp-more\">", StringComparison.Ordinal), StringComparison.Ordinal)];

        Assert.Contains("data-open-dialog=\"mark-dialog\"", menu, StringComparison.Ordinal);
        Assert.Contains("<script src=\"~/js/bp-record-editor.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
    }
}
