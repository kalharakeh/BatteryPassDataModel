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

    [Theory]
    [InlineData("Batteries", "_AccidentsCard.cshtml", "a PDF, PNG or JPEG", "@AppOptions.Value.MaxUploadBytes")]
    [InlineData("Batteries", "_ModulesCard.cshtml", "a JSON file", "@BatteryPassWeb.Controllers.BatteriesController.MaxExportBytes")]
    [InlineData("ProductData", "_EditorField.cshtml", "a PDF, PNG or JPEG", "@Options.Value.MaxUploadBytes")]
    [InlineData("ProductData", "Family.cshtml", "a PNG or JPEG", "@AppOptions.Value.MaxUploadBytes")]
    public void EveryUpload_ShouldSayWhichKindAndHowLargeAFileItTakes(string folder, string view, string kind, string limit)
    {
        // Point 13: every upload is checked as the right kind of file for it, with clear feedback.
        var page = Read("web", "Views", folder, view);

        Assert.Contains($"data-kind-words=\"{kind}\"", page, StringComparison.Ordinal);
        Assert.Contains($"data-max-bytes=\"{limit}\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAccidentAndModuleUploads_ShouldShowTheChosenFile()
    {
        // The script finds the chosen-file line through the form's data-upload-row; these two forms had none (point 13).
        foreach (var view in new[] { "_AccidentsCard.cshtml", "_ModulesCard.cshtml" })
        {
            var page = Read("web", "Views", "Batteries", view);
            var upload = page.IndexOf("enctype=\"multipart/form-data\"", StringComparison.Ordinal);
            var start = page.LastIndexOf("<form", upload, StringComparison.Ordinal);
            var openingTag = page[start..page.IndexOf('>', upload)];
            var form = page[start..page.IndexOf("</form>", upload, StringComparison.Ordinal)];

            Assert.Contains("data-upload-row", openingTag, StringComparison.Ordinal);
            Assert.Contains("data-upload-chosen", form, StringComparison.Ordinal);
            Assert.Contains("data-upload-button", form, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheUploadScript_ShouldRefuseAFileOfTheWrongKindOrSizeBeforeSendingIt()
    {
        var script = Read("web", "wwwroot", "js", "bp-record-editor.js");

        Assert.Contains("function uploadProblem(picked, input)", script, StringComparison.Ordinal);
        Assert.Contains("input.dataset.kindWords", script, StringComparison.Ordinal);
        Assert.Contains("input.dataset.maxBytes", script, StringComparison.Ordinal);
        Assert.Contains("'Ready to upload: ' + picked.name + ' · ' + sizeWords(picked.size)", script, StringComparison.Ordinal);
        Assert.Contains("button.disabled = !picked || problem !== null;", script, StringComparison.Ordinal);
        Assert.Contains("[data-upload-chosen].is-wrong {", Css, StringComparison.Ordinal);
        Assert.Contains("[data-upload-chosen].is-good {", Css, StringComparison.Ordinal);
    }
}
