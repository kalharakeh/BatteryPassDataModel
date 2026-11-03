namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R4 (owner's choice B, mock-up H and F1): both battery pages as a header and a side menu showing one section
/// at a time, their windows outside the sections, and the header carrying the notices and the one Undo.
/// </summary>
public sealed class BatteryPageLayoutTests
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void BothPages_ShouldShowOneSectionAtATimeWithTheirWindowsOutsideTheSections()
    {
        foreach (var (page, ids) in new[]
        {
            (View("Batteries", "Battery.cshtml"), new[] { "overview", "values", "status", "live", "accidents", "modules", "versions" }),
            (View("ClusterAdmin", "Battery.cshtml"), new[] { "overview", "status", "live", "accidents", "handover", "modules", "versions" }),
        })
        {
            var start = page.IndexOf("<div class=\"bp-bpanel\">", StringComparison.Ordinal);
            var panel = page[start..page.IndexOf("@* The windows", start, StringComparison.Ordinal)];

            Assert.Contains("@await Html.PartialAsync(\"~/Views/Batteries/_BatteryNav.cshtml\", new BatteryNav(sections, chosen, ", page, StringComparison.Ordinal);
            Assert.All(ids, id => Assert.Contains($"<div data-section=\"{id}\" hidden=\"@Hidden(\"{id}\")\"", panel, StringComparison.Ordinal));
            Assert.DoesNotContain("<dialog", panel, StringComparison.Ordinal); // a window in a hidden section can't open
            Assert.Contains("@await Html.PartialAsync(\"~/Views/Batteries/_StatusDialog.cshtml\", statusWindows)", page, StringComparison.Ordinal);
            Assert.Contains("data-open-dialog=\"status-dialog\"", page[..page.IndexOf("</header>", StringComparison.Ordinal)], StringComparison.Ordinal);
            Assert.Contains("data-open-dialog=\"note-dialog\"", page[..page.IndexOf("</header>", StringComparison.Ordinal)], StringComparison.Ordinal);
            Assert.Contains("<script src=\"~/js/bp-battery-page.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
            Assert.Contains("<script src=\"~/js/bp-picture-trim.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
            Assert.Contains("@await Html.PartialAsync(\"_StatusHelp\")", page, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("<dialog", View("Batteries", "_StatusCard.cshtml"), StringComparison.Ordinal);
        // Out of the card, the windows keep its form styles through an invisible host (else the radio's oval came back).
        Assert.Contains("<div class=\"bp-console-form-card bp-dialog-host\">", View("Batteries", "_StatusDialog.cshtml"), StringComparison.Ordinal);
        Assert.Contains(".bp-console-form-card.bp-dialog-host {\n  display: contents;\n}", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")).Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void TheTeamsHeader_ShouldCarryTheNoticesAndTheOneUndo()
    {
        var page = View("Batteries", "Battery.cshtml");
        var header = page[page.IndexOf("<header class=\"bp-bh\">", StringComparison.Ordinal)..page.IndexOf("</header>", StringComparison.Ordinal)];

        Assert.Contains("<form method=\"post\" action=\"@Model.Marks.Action/undo\" class=\"bp-status-notice\" id=\"mark-notice\">", header, StringComparison.Ordinal); // point 17
        Assert.Single(page.Split("/undo\"").Skip(1)); // one Undo, in the header (the Passport status section is gone, R5)
        Assert.Contains("@await Html.PartialAsync(\"_TestTag\", \"Test ID\")", header, StringComparison.Ordinal); // point 7: a tag that explains itself on a tap
        Assert.DoesNotContain("Test identifiers: this battery's IDs start with TEST-", page, StringComparison.Ordinal);
        Assert.Contains(">Earlier passport</a>", header, StringComparison.Ordinal);
        Assert.Contains(">New passport</a>", header, StringComparison.Ordinal); // point 6
        Assert.Contains("action=\"@href/handover/cancel\" class=\"bp-status-notice is-quiet\"", header, StringComparison.Ordinal);
        foreach (var link in new[] { ">Public page</a>", ">Detail page</a>", ">QR code</a>", ">Save QR</a>", ">History</a>" })
        {
            Assert.Contains(link, header, StringComparison.Ordinal); // point 4: labelled icon links
        }

        Assert.DoesNotContain("Open the new passport", View("Batteries", "_StatusCard.cshtml"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Batteries")]
    [InlineData("ClusterAdmin")]
    public void BothHeaders_ShouldBeShortWithThePictureTheFactsOnOneLineAndOneToolbarRow(string folder)
    {
        // The owner's improvements 1-3 and 7-9 (2 Nov 2026).
        var page = View(folder, "Battery.cshtml");
        var header = page[page.IndexOf("<header class=\"bp-bh\">", StringComparison.Ordinal)..page.IndexOf("</header>", StringComparison.Ordinal)];
        var bar = header[header.IndexOf("<div class=\"bp-bh-bar\">", StringComparison.Ordinal)..];
        var nav = View("Batteries", "_BatteryNav.cshtml");

        Assert.Contains("<main class=\"bp-page bp-admin-page bp-battery-page\">", page, StringComparison.Ordinal);
        Assert.Contains("@if (Model.PictureHref is { } picture)", header, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-bh-picture\"><img src=\"@picture\"", header, StringComparison.Ordinal); // a real picture, a column beside the text
        // The box fits the picture's visible shape: no minimum width, and the script cuts transparent margins off (owner, 2 Nov 2026).
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var box = css[css.IndexOf(".bp-bh-picture {\n", StringComparison.Ordinal)..];
        Assert.DoesNotContain("min-width", box[..box.IndexOf('}')], StringComparison.Ordinal);
        var trim = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-picture-trim.js"));
        Assert.Contains("document.querySelector('.bp-bh-picture img, .bp-hero-picture img')", trim, StringComparison.Ordinal);
        Assert.Contains("image.dataset.trimmed = 'true';", trim, StringComparison.Ordinal);
        Assert.True(header.IndexOf("bp-bh-picture", StringComparison.Ordinal) < header.IndexOf("<div class=\"bp-bh-id\">", StringComparison.Ordinal)
            && header.IndexOf("<div class=\"bp-bh-id\">", StringComparison.Ordinal) < header.IndexOf("<div class=\"bp-bh-facts-row\">", StringComparison.Ordinal));
        Assert.Contains("<span class=\"bp-id-value\" title=\"@Model.Identifier\">@BatteryPages.ShortId(Model.Identifier)</span>@await Html.PartialAsync(\"_CopyIdButton\", Model.Identifier)", header, StringComparison.Ordinal);
        Assert.Contains("<details class=\"bp-bh-details\" open data-fold-on-phone>", header, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-bh-actions\" data-action-bar>", bar, StringComparison.Ordinal);
        Assert.Contains("<nav class=\"bp-bh-open\"", bar, StringComparison.Ordinal);
        Assert.Contains("<select name=\"section\" data-section-picker>", nav, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePhone_ShouldGetAPickerABottomBarAndFoldedFacts()
    {
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var phone = css[css.LastIndexOf("@media (max-width: 767.98px) {", StringComparison.Ordinal)..];
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-battery-page.js"));

        Assert.Contains("  .bp-bh-actions {\n    position: fixed;", phone, StringComparison.Ordinal);
        Assert.Contains("  .bp-battery-page {\n    padding-bottom: 84px;\n  }", phone, StringComparison.Ordinal);
        Assert.Contains("  .bp-bnav {\n    display: none;\n  }", phone, StringComparison.Ordinal);
        Assert.Contains("  .bp-bpicker {\n    display: block;\n  }", phone, StringComparison.Ordinal);
        Assert.Contains("picker?.addEventListener('change'", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('details[data-fold-on-phone]')", script, StringComparison.Ordinal);
        Assert.Contains(".bp-bnav .bp-edit-nav-button.is-active {\n  border-left-color: var(--bp-primary);", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("urn:x-battery-pass:TEST-47c52ad4c32542e5b51b3b3326034fc1", "urn:x-battery-pass:TEST-…26034fc1")]
    [InlineData("urn:x-battery-pass:TEST-1", "urn:x-battery-pass:TEST-1")]
    public void ALongBatteryId_ShouldBeShortenedInTheMiddle(string id, string shown)
    {
        Assert.Equal(shown, BatteryPassWeb.Dpp.Pages.BatteryPages.ShortId(id));
    }

    [Fact]
    public void TheClusterAdminsHeader_ShouldCarryTheTestTagAndTheWaitingHandOver()
    {
        var page = View("ClusterAdmin", "Battery.cshtml");
        var header = page[page.IndexOf("<header class=\"bp-bh\">", StringComparison.Ordinal)..page.IndexOf("</header>", StringComparison.Ordinal)];

        Assert.Contains("@await Html.PartialAsync(\"_TestTag\", \"Test ID\")", header, StringComparison.Ordinal);
        Assert.Contains("<form method=\"post\" action=\"@Model.HandOver.Action/cancel\" class=\"bp-status-notice is-quiet\">", header, StringComparison.Ordinal);
        Assert.Contains("New passport of @replaced.SerialNumber", header, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMissingValues_ShouldBeListedOnceInTheOverview()
    {
        // Found in the review: the publication card and the values editor both counted them (manager, 2 Nov 2026).
        var editor = View("ProductData", "_RecordEditor.cshtml");

        Assert.Contains("@if (Model.Missing > 0 && Model.Kind == TargetKind.Pack)", editor, StringComparison.Ordinal);
        Assert.Contains("the overview lists every value the passport still needs", editor, StringComparison.Ordinal);
        Assert.Contains("@foreach (var group in publication.Missing)", View("Batteries", "Battery.cshtml"), StringComparison.Ordinal);
    }
}
