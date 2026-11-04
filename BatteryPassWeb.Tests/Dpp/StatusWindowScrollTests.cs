namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The owner's second step-5 test (4 Nov 2026, Edge at 150 % scaling): the "Change status" window hid its last choice
/// ("Waste") under its Cancel/Next bar and had nothing to scroll. The bar was sticky with a negative bottom margin, so the
/// window was 20 px shorter than its content and the overlap didn't count as overflow. Now each step has a head, a body
/// that alone scrolls, and its buttons below the body, never over it.
/// </summary>
public sealed class StatusWindowScrollTests
{
    private static string Css() => File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")).Replace("\r\n", "\n");

    [Fact]
    public void EachStep_ShouldHaveAHeadABodyThatScrollsAndItsButtonsBelowTheBody()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_StatusDialog.cshtml")).Replace("\r\n", "\n");
        var window = view[view.IndexOf("<dialog id=\"status-dialog\"", StringComparison.Ordinal)..view.IndexOf("</dialog>", StringComparison.Ordinal)];

        foreach (var step in new[] { "<div data-status-step=\"1\">", "<div data-status-step=\"2\">" })
        {
            var start = window.IndexOf(step, StringComparison.Ordinal);
            var end = window.IndexOf("<div class=\"bp-dialog-actions\">", start, StringComparison.Ordinal);
            var head = window.IndexOf("<div class=\"bp-dialog-head\">", start, StringComparison.Ordinal);
            var body = window.IndexOf("<div class=\"bp-dialog-body\">", start, StringComparison.Ordinal);
            Assert.True(start >= 0 && head > start && body > head && end > body, step);
        }

        Assert.True(window.IndexOf("class=\"bp-choice-list\"", StringComparison.Ordinal) > window.IndexOf("<div class=\"bp-dialog-body\">", StringComparison.Ordinal));
        Assert.True(window.IndexOf("class=\"bp-attr-list\"", StringComparison.Ordinal) > window.LastIndexOf("<div class=\"bp-dialog-body\">", StringComparison.Ordinal));
    }

    [Fact]
    public void OnlyTheBodyScrolls_AndTheButtonsNeverCoverIt()
    {
        var css = Css();

        Assert.Contains("[data-status-dialog][open] {\n  display: flex;\n  flex-direction: column;\n  padding: 0;\n  overflow: hidden;\n}", css, StringComparison.Ordinal);
        Assert.Contains("[data-status-dialog] .bp-dialog-body {\n  display: grid;\n  flex: 1 1 auto;\n  gap: 14px;\n  min-height: 0;\n  overflow-y: auto;\n  overscroll-behavior: contain;", css, StringComparison.Ordinal);
        Assert.Contains("[data-status-dialog].is-step-2 [data-status-step=\"2\"] {\n  display: flex;\n}", css, StringComparison.Ordinal);
        var actions = css[css.IndexOf("[data-status-dialog] .bp-dialog-actions {", StringComparison.Ordinal)..];
        actions = actions[..actions.IndexOf('}')];
        Assert.DoesNotContain("sticky", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("-20px", actions, StringComparison.Ordinal);
    }
}
