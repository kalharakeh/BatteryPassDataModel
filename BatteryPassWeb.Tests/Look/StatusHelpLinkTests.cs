using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R5, item 5: the owner couldn't find the status explainer. Beside the badges it now says "What do these
/// mean?" in words, and the lists' Status heading has it too, as a small ringed "?".
/// </summary>
public sealed class StatusHelpLinkTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void TheExplainer_ShouldBeOpenedByALinkThatSaysWhatItIs()
    {
        var help = Read("web", "Views", "Shared", "_StatusHelp.cshtml");
        var css = Read("web", "wwwroot", "css", "bp-status.css");

        Assert.Contains("<use href=\"#i-help\" /></svg><span class=\"bp-status-help-words\">What do these mean?</span></button>", help, StringComparison.Ordinal);
        Assert.Contains("  color: var(--bp-primary);\n  font: inherit;\n  font-size: 0.82rem;\n  font-weight: 600;\n  text-decoration: underline;", css, StringComparison.Ordinal);
        Assert.Contains("th .bp-status-help-button {", css, StringComparison.Ordinal);
        Assert.Contains("th .bp-status-help-words {", css, StringComparison.Ordinal);
    }

    [Fact]
    public void BothLists_ShouldExplainTheStatusesInTheirHeading()
    {
        // Both lists are one shared list since R7 (the owner's choice "One shared list").
        Assert.Contains("<th>Status @await Html.PartialAsync(\"_StatusHelp\")</th>", Read("web", "Views", "Shared", "_BatteryList.cshtml"), StringComparison.Ordinal);
        foreach (var view in new[] { Read("web", "Views", "Batteries", "Batteries.cshtml"), Read("web", "Views", "Registry", "Index.cshtml") })
        {
            Assert.Contains("@await Html.PartialAsync(\"_BatteryList\", new BatteryListView(", view, StringComparison.Ordinal);
        }
    }
}
