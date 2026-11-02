using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R3 (decision 36, owner's point 9): the battery status (#22) and the passport status (#2) as two badges that
/// never look alike, each status with its own colour and icon, explained by the "?" beside them.
/// </summary>
public sealed partial class StatusBadgeTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    [Theory]
    [InlineData("original", "is-original", "star", "Original")]
    [InlineData("re-used", "is-reused", "reuse", "Re-used")]
    [InlineData("Re-used", "is-reused", "reuse", "Re-used")]
    [InlineData("repurposed", "is-repurposed", "repurpose", "Repurposed")]
    [InlineData("remanufactured", "is-remanufactured", "wrench", "Remanufactured")]
    [InlineData("waste", "is-waste", "waste", "Waste")]
    public void EachBatteryStatus_ShouldHaveItsOwnSquareTag(string status, string css, string icon, string words)
    {
        Assert.Equal(new StatusBadge(StatusBadge.BatteryKind, css, icon, words), StatusBadge.Battery(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-")]
    public void NoBatteryStatus_ShouldShowNoTag(string? status)
    {
        // "-" is what a passport page shows for a value the reader may not see: #22 is purchaser data.
        Assert.Null(StatusBadge.Battery(status));
    }

    [Theory]
    [InlineData(DppStatuses.Active, "is-active", "check", "Active")]
    [InlineData(DppStatuses.Archived, "is-archived", "archive", "Archived")]
    [InlineData(DppStatuses.Inactive, "is-inactive", "pause", "Inactive")]
    [InlineData(DppStatuses.Invalid, "is-invalid", "alert", "Invalid")]
    [InlineData(DppStatuses.MarkedForDeletion, "is-marked", "trash", "Marked for deletion")]
    [InlineData(null, "is-draft", "passport", "Draft")]
    [InlineData("draft", "is-draft", "passport", "Draft")]
    public void EachPassportStatus_ShouldHaveItsOwnRoundPill(string? status, string css, string icon, string words)
    {
        Assert.Equal(new StatusBadge(StatusBadge.PassportKind, css, icon, words), StatusBadge.Passport(status));
    }

    [Fact]
    public void EveryPage_ShouldLoadTheBadgesAndTheirIcons()
    {
        var layout = Read("web", "Views", "Shared", "_Layout.cshtml");
        var icons = IconIds().Matches(Read("web", "Views", "Shared", "_Icons.cshtml")).Select(match => match.Groups[1].Value).ToHashSet();
        var used = new[] { "original", "re-used", "repurposed", "remanufactured", "waste", "unknown" }.Select(status => StatusBadge.Battery(status)!.Icon)
            .Concat(new[] { DppStatuses.Active, DppStatuses.Archived, DppStatuses.Inactive, DppStatuses.Invalid, DppStatuses.MarkedForDeletion, "draft", "other" }.Select(status => StatusBadge.Passport(status).Icon));

        Assert.Contains("<link rel=\"stylesheet\" href=\"~/css/bp-status.css\" asp-append-version=\"true\" />", layout, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_Icons\")", layout, StringComparison.Ordinal);
        Assert.All(used, icon => Assert.Contains(icon, icons));
        Assert.All(new[] { "help", "history", "arrow", "bell" }, icon => Assert.Contains(icon, icons));
    }

    [Fact]
    public void TheBadges_ShouldTellTheirKindByShapeAndWord()
    {
        var badge = Read("web", "Views", "Shared", "_StatusBadge.cshtml");
        var css = Read("web", "wwwroot", "css", "bp-status.css").Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("<span class=\"bp-badge is-@Model.Kind @Model.Css\">", badge, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-badge-kind\">@(Model.Kind == StatusBadge.BatteryKind ? \"Battery\" : \"Passport\")</span>", badge, StringComparison.Ordinal);
        Assert.Contains(".bp-badge.is-battery {\n  border-radius: 6px;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".bp-badge.is-passport {\n  border-radius: 999px;\n}", css, StringComparison.Ordinal);
        foreach (var cssClass in new[] { "is-original", "is-reused", "is-repurposed", "is-remanufactured", "is-waste", "is-active", "is-draft", "is-inactive", "is-archived", "is-invalid", "is-marked" })
        {
            Assert.Contains($".bp-badge.{cssClass}", css, StringComparison.Ordinal);
            Assert.Contains($"[data-theme=\"dark\"] .bp-badge.{cssClass}", css, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheQuestionMark_ShouldExplainEveryStatusOncePerPage()
    {
        var help = Read("web", "Views", "Shared", "_StatusHelp.cshtml");

        Assert.Contains("popovertarget=\"status-help\" aria-label=\"What do the two statuses mean?\"", help, StringComparison.Ordinal);
        Assert.Contains("<div id=\"status-help\" class=\"bp-status-help\" popover>", help, StringComparison.Ordinal);
        Assert.Contains("@if (!Context.Items.ContainsKey(\"status-help\"))", help, StringComparison.Ordinal);
        Assert.Equal(5, help.Split("StatusBadge.Battery(").Length - 1);
        Assert.Equal(6, help.Split("StatusBadge.Passport(").Length - 1);
    }

    [Fact]
    public void NoView_ShouldPutCodeRightAfterALetter()
    {
        // Razor reads text such as "Registry@await" as an e-mail address and prints the code as text (found in R3).
        var folder = Path.GetDirectoryName(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "Shared", "_Layout.cshtml")))!;
        var views = Directory.GetFiles(folder, "*.cshtml", SearchOption.AllDirectories);
        var stuck = views.SelectMany(view => File.ReadLines(view).Where(line => CodeAfterALetter().IsMatch(line)).Select(line => $"{Path.GetFileName(view)}: {line.Trim()}"));

        Assert.Empty(stuck);
    }

    [GeneratedRegex("<symbol id=\"i-([a-z-]+)\"")]
    private static partial Regex IconIds();

    // A plain word (not the end of code such as @Model.PublicBaseUrl, which Razor reads right) touching @code.
    [GeneratedRegex(@"(?<![@.\w])\w+@(if|await|Model|Html|foreach)\b")]
    private static partial Regex CodeAfterALetter();
}
