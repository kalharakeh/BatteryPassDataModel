using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Redesign R1: the bugs the owner found on the battery pages (points 2, 3 and 13).</summary>
public sealed class RedesignFixesTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string Css => Read("web", "wwwroot", "css", "bp-product-data.css");

    private static string Block(string text, string start)
    {
        var at = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(at >= 0, $"Missing: {start}");
        return text[at..text.IndexOf('}', at)];
    }

    [Fact]
    public void TheStatusWindow_ShouldOutlineTheChosenOptionInsteadOfARingAroundItsRadio()
    {
        // The general focus ring stretched over the radio made "an odd oval that fades" (point 2).
        Assert.Contains("main :is(input:not([type=\"checkbox\"]):not([type=\"radio\"]):not([type=\"file\"]), select, textarea):focus {", Css, StringComparison.Ordinal);
        Assert.Contains("align-self: start;", Block(Css, ".bp-console-form-card .bp-choice input[type=\"radio\"] {"), StringComparison.Ordinal);
        Assert.Contains("box-shadow: none;", Block(Css, ".bp-console-form-card .bp-choice input[type=\"radio\"]:focus {"), StringComparison.Ordinal);
        Assert.Contains("outline: 2px solid var(--bp-primary);", Block(Css, ".bp-choice:has(input:focus-visible) {"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheStatusWindow_ShouldKeepItsButtonsInViewWhileItsValuesScroll()
    {
        // "Save and publish a new version" sat below every re-stated value (point 3).
        var actions = Block(Css, "[data-status-dialog] .bp-dialog-actions {");

        Assert.Contains("position: sticky;", actions, StringComparison.Ordinal);
        Assert.Contains("bottom: 0;", actions, StringComparison.Ordinal);
        Assert.Contains("background: #101f35;", Block(Css, "[data-theme=\"dark\"] [data-status-dialog] .bp-dialog-actions {"), StringComparison.Ordinal);
    }
}
