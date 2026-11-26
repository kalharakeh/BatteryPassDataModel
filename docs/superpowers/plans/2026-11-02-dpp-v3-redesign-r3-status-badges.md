# DPP V3 Redesign R3: The Two Statuses as Badges — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The third part of the battery pages' redesign (decision 36). Point 9: the battery status (#22) and the passport status (#2) looked alike and were mixed up.
- **Two badges** that never look alike, on every page that shows a battery:
  - a square **Battery** tag (what happened to the battery);
  - a round **Passport** pill (whether this passport counts).
  - Each status has its own colour and icon (decision 36 lets the badges have their own colours).
- **A "?"** beside the badges explains every status, in a small window.
- **#22 is purchaser data:** the Battery tag shows only to the battery's cluster, market surveillance and the team. It never shows on the public summary.
- **Earlier and new passports** (point 10a and the owner's answer to Q4): searching DEMO-C13M-0001 still opens the new passport. Today's banner "Active. This passport replaces an earlier one…" becomes a calm "Earlier passport" chip beside the passport badge, with one plain line. The earlier passport has the mirror image: a "New passport" chip. Invalid and marked passports keep a red line.
  - The line names what happened only to readers allowed #22: "Repurposed on 2 Nov 2026. Its earlier passport is inactive and stays readable."
  - Everyone else, and the public summary for everyone, gets the facts: "Replaces an earlier passport of this battery (since 2 Nov 2026)" or "This battery continues on a new passport since 2 Nov 2026". This changes step 4c-1's public notice.
- **A test passport** (point 7): the full-width yellow banner becomes a small amber "Test passport" tag beside the passport badge. A tap or a hover explains it, and the detail page's identifiers say it in words.
- **The Registry** (points 11 and 12):
  - Rows show both badges and a labelled **Open** button where the small "Manage" link was.
  - "Waiting for you" stands out (an accent edge, a bell, Accept first), and the Registry entry in the top bar shows how many hand-overs wait.
  - The team's battery list shows the badges too.
- **The public hero** (the owner's item 10, 2 Nov 2026), on the public summary and the detail page:
  - the picture moves inside the card, top right, at its own shape, about 160 px tall (100 px beside the name on a phone);
  - the facts use the card's full width (4 columns);
  - "View more" sits in the card, under the facts;
  - the facts leave out "Passport status": the Passport badge shows #2 to everyone, so nothing is hidden, and the detail page's passport data sections keep it as today;
  - nothing else changes: no change to the battery pages, the Registry or `site.css`.

**Architecture:**
- **`Dpp/Pages/StatusBadge.cs`** maps a status to its kind, colour class, icon and words.
- **Shared partials:**
  - `Views/Shared/_Icons.cshtml`: one inline SVG sprite, in the layout.
  - `_StatusBadge.cshtml`: one badge.
  - `_StatusHelp.cshtml`: the "?" and its explainer, a `popover` with no script, written once per page.
- **`wwwroot/css/bp-status.css`**, loaded by the layout (`site.css` is unchanged): badges, icons, chips, notice lines, the waiting card and the top-bar count.
- **The passport pages:**
  - `PassportPage.BatteryStatus` is read only from the detail page's audience sections.
  - `PassportNotice.Calm` marks an earlier or new passport, with dated words. `NoticeAsync` names the battery status only for a detail-page reader allowed #22 (`AudienceRules.MaySee`).
  - `_PassportHero` draws the badges, the chip and the line, or the red line.
  - `Views/Shared/_TestTag.cshtml` replaces the banner: a tag whose explanation (a popover) is written once per page; R4 reuses it for "Test ID".
  - `_PassportHero` gets one more class, `bp-hero-compact`. Its picture sits in the card before the facts, its buttons after them, and the empty `aside` is gone. Every rule for it in `bp-status.css` starts with `.bp-hero-compact`, so no other page changes (task 4).
- **The lists:**
  - `BatteryRow.BatteryStatus` holds the battery status.
  - `RegistryPage.BatteryStatusShown` holds the batteries whose #22 the reader may see (`AudienceRules.MaySeePurchaserData`).
  - `HandOverService.WaitingCountAsync` gives `_WaitingCount`, the top bar's count for a cluster admin.
- **A guard test:** no view may put `@code` right after a plain word. Razor reads `Registry@await` as an e-mail address and prints it as text.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** the owner's 17 points and the UX review (in the managing session's notes); the approved mock-up `docs/mockups/dpp-v3-battery-page-redesign.html` (owner's choice of 2 Nov 2026: layout B); decision 36 in `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-decisions.md`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 4f65caa. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas** or `battery_pass_v3`. The tests use a throwaway MongoDB (EphemeralMongo).
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. Whole-suite runs also take `--blame-hang --blame-hang-timeout 10m`.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started (by their process IDs, never by name) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2273`. After this plan: 2303. The suite takes about two to four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../r1-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.** The new styles go to `bp-product-data.css` (battery pages) and the new `bp-status.css` (every page, R3).
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## What happens to the old tests

Nothing is removed. Three assertions follow the new markup:
- `BatteryRouteAndRegistryTests`: the hero's status is a badge (task 2).
- `PassportControllerTests`: the hero's test notice is a tag (task 2).
- `SuccessionPagesTests`: the notices are calm and dated; the public ones without the #22 word (task 2).
- `PassportMarkPagesTests`: the Registry row's status is a badge (task 3).
- `AdminFeedbackFollowupTests`: the hero shows the passport status as its badge, not as a fact row (task 4).

---

### Task 1: The two statuses as badges, with their icons and the explainer

The shared pieces every page uses: the badge model, the icon sprite and the stylesheet (both in the layout), one badge as a partial, and the "?" with its explainer. A guard test keeps views from putting code right after a plain word, which Razor prints as text.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/StatusBadgeTests.cs`
- Create: `web/Dpp/Pages/StatusBadge.cs`
- Create: `web/Views/Shared/_Icons.cshtml`
- Modify: `web/Views/Shared/_Layout.cshtml`
- Create: `web/Views/Shared/_StatusBadge.cshtml`
- Create: `web/Views/Shared/_StatusHelp.cshtml`
- Create: `web/wwwroot/css/bp-status.css`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/StatusBadgeTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusBadgeTests"`
Expected: the build fails with CS0103, CS0246 (for example `error CS0103 The name 'StatusBadge' does not exist in the current context`).

- [ ] **Step 3: Write the code**

Patch `r3-task1-code.patch` (`web/Views/Shared/_Layout.cshtml`). Save the block as `../r3-task1-code.patch`, outside the worktree, and run `git apply ../r3-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Shared/_Layout.cshtml b/web/Views/Shared/_Layout.cshtml
index 67f233a..ac1dae4 100644
--- a/web/Views/Shared/_Layout.cshtml
+++ b/web/Views/Shared/_Layout.cshtml
@@ -60,8 +60,10 @@
     <title>@title - Scania Battery Passport Viewer</title>
     <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" />
     <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
+    <link rel="stylesheet" href="~/css/bp-status.css" asp-append-version="true" />
 </head>
 <body>
+    @await Html.PartialAsync("_Icons")
     <div class="bp-shell">
         <header class="bp-header bp-scania-header">
             <div class="bp-header-top-row">
```

`web/Dpp/Pages/StatusBadge.cs`:

```csharp
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One of a battery's two statuses as a badge (redesign R3, decision 36), so they never look alike: the battery status
/// (#22, what happened to the battery) is a square "Battery" tag, the passport status (#2, whether this passport counts)
/// a round "Passport" pill. Each status has its own colour and icon, drawn by Views/Shared/_StatusBadge.cshtml.
/// </summary>
/// <param name="Kind">"battery" or "passport".</param>
/// <param name="Css">The colour class, e.g. "is-reused" or "is-invalid".</param>
/// <param name="Icon">The icon's id in Views/Shared/_Icons.cshtml, without "i-".</param>
/// <param name="Words">The status in words: "Re-used", "Marked for deletion".</param>
public sealed record StatusBadge(string Kind, string Css, string Icon, string Words)
{
    public const string BatteryKind = "battery";
    public const string PassportKind = "passport";

    /// <summary>The battery status (#22) as a badge; null when the battery has none (or the reader may not see it).</summary>
    public static StatusBadge? Battery(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        null or "" or "-" => null,
        "original" => new(BatteryKind, "is-original", "star", "Original"),
        "re-used" => new(BatteryKind, "is-reused", "reuse", "Re-used"),
        "repurposed" => new(BatteryKind, "is-repurposed", "repurpose", "Repurposed"),
        "remanufactured" => new(BatteryKind, "is-remanufactured", "wrench", "Remanufactured"),
        "waste" => new(BatteryKind, "is-waste", "waste", "Waste"),
        var other => new(BatteryKind, "is-original", "battery", char.ToUpperInvariant(other[0]) + other[1..]),
    };

    /// <summary>The passport status (#2) as a badge; a passport never published is a draft.</summary>
    public static StatusBadge Passport(string? dppStatus) => dppStatus switch
    {
        null or "" or "draft" => new(PassportKind, "is-draft", "passport", "Draft"),
        DppStatuses.Active => new(PassportKind, "is-active", "check", "Active"),
        DppStatuses.Archived => new(PassportKind, "is-archived", "archive", "Archived"),
        DppStatuses.Inactive => new(PassportKind, "is-inactive", "pause", "Inactive"),
        DppStatuses.Invalid => new(PassportKind, "is-invalid", "alert", "Invalid"),
        DppStatuses.MarkedForDeletion => new(PassportKind, "is-marked", "trash", "Marked for deletion"),
        var other => new(PassportKind, "is-inactive", "passport", DppStatuses.Words(other)),
    };

    /// <summary>The same badge with more words after the status, e.g. "· version 3".</summary>
    public string? Detail { get; init; }

    public StatusBadge With(string? detail) => this with { Detail = detail };
}
```

`web/Views/Shared/_Icons.cshtml`:

```cshtml
@* The app's icons (redesign R3): one inline SVG sprite in the layout; a page draws one with
   <svg class="bp-i" aria-hidden="true"><use href="#i-name" /></svg>. Stroke drawings in the text colour. *@
<svg width="0" height="0" style="position:absolute" aria-hidden="true" focusable="false">
    <symbol id="i-globe" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9" /><path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18" /></symbol>
    <symbol id="i-doc" viewBox="0 0 24 24"><path d="M7 3h7l5 5v13H7z" /><path d="M14 3v5h5M10 13h6M10 17h6" /></symbol>
    <symbol id="i-qr" viewBox="0 0 24 24"><rect x="4" y="4" width="6" height="6" rx="1" /><rect x="14" y="4" width="6" height="6" rx="1" /><rect x="4" y="14" width="6" height="6" rx="1" /><path d="M14 14h2v2h-2zM18 18h2v2h-2zM14 19h1M19 14h1" /></symbol>
    <symbol id="i-download" viewBox="0 0 24 24"><path d="M12 4v11M7 10l5 5 5-5M5 20h14" /></symbol>
    <symbol id="i-history" viewBox="0 0 24 24"><path d="M4 12a8 8 0 1 0 2.3-5.6" /><path d="M4 4v4h4" /><path d="M12 8v4l3 2" /></symbol>
    <symbol id="i-more" viewBox="0 0 24 24"><circle cx="5" cy="12" r="1.3" /><circle cx="12" cy="12" r="1.3" /><circle cx="19" cy="12" r="1.3" /></symbol>
    <symbol id="i-battery" viewBox="0 0 24 24"><rect x="3" y="7" width="16" height="10" rx="2" /><path d="M21 10v4M7 10v4M11 10v4" /></symbol>
    <symbol id="i-passport" viewBox="0 0 24 24"><rect x="5" y="3" width="14" height="18" rx="2" /><circle cx="12" cy="10" r="3" /><path d="M9 16h6" /></symbol>
    <symbol id="i-check" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9" /><path d="M8 12.5l2.5 2.5L16 9.5" /></symbol>
    <symbol id="i-pause" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9" /><path d="M10 9v6M14 9v6" /></symbol>
    <symbol id="i-archive" viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="5" rx="1" /><path d="M5 9v10h14V9M10 13h4" /></symbol>
    <symbol id="i-alert" viewBox="0 0 24 24"><path d="M12 3l9.5 17h-19z" /><path d="M12 10v4M12 17v.5" /></symbol>
    <symbol id="i-trash" viewBox="0 0 24 24"><path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v6M14 11v6" /></symbol>
    <symbol id="i-star" viewBox="0 0 24 24"><path d="M12 3l2.6 5.6 6 .7-4.5 4.1 1.2 6L12 16.5l-5.3 2.9 1.2-6-4.5-4.1 6-.7z" /></symbol>
    <symbol id="i-reuse" viewBox="0 0 24 24"><path d="M4 12a8 8 0 0 1 13.7-5.7L20 8" /><path d="M20 4v4h-4" /><path d="M20 12a8 8 0 0 1-13.7 5.7L4 16" /><path d="M4 20v-4h4" /></symbol>
    <symbol id="i-repurpose" viewBox="0 0 24 24"><path d="M3 7h4l10 10h4M3 17h4l3-3M14 10l3-3h4" /><path d="M18 4l3 3-3 3M18 14l3 3-3 3" /></symbol>
    <symbol id="i-wrench" viewBox="0 0 24 24"><path d="M14.7 6.3a4 4 0 0 0-5.4 5.2L3 17.8V21h3.2l6.3-6.3a4 4 0 0 0 5.2-5.4l-2.6 2.6-2.4-.5-.5-2.4z" /></symbol>
    <symbol id="i-waste" viewBox="0 0 24 24"><path d="M5 7h14l-1.5 13h-11z" /><path d="M9 4h6M3 7h18" /><path d="M10 11l2 3 2-3" /></symbol>
    <symbol id="i-swap" viewBox="0 0 24 24"><path d="M7 4L3 8l4 4M3 8h14M17 12l4 4-4 4M21 16H7" /></symbol>
    <symbol id="i-note" viewBox="0 0 24 24"><path d="M5 4h14v11l-5 5H5z" /><path d="M14 20v-5h5M9 9h6M9 13h3" /></symbol>
    <symbol id="i-upload" viewBox="0 0 24 24"><path d="M12 20V9M7 14l5-5 5 5M5 4h14" /></symbol>
    <symbol id="i-help" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9" /><path d="M9.5 9.5a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .8-1 1.5v.7M12 17v.5" /></symbol>
    <symbol id="i-arrow" viewBox="0 0 24 24"><path d="M5 12h14M13 6l6 6-6 6" /></symbol>
    <symbol id="i-bell" viewBox="0 0 24 24"><path d="M6 16v-5a6 6 0 1 1 12 0v5l2 2H4z" /><path d="M10 21h4" /></symbol>
    <symbol id="i-handover" viewBox="0 0 24 24"><path d="M4 9h13l-3-3M20 15H7l3 3" /></symbol>
    <symbol id="i-live" viewBox="0 0 24 24"><path d="M3 12h4l2-5 4 10 2-5h6" /></symbol>
    <symbol id="i-modules" viewBox="0 0 24 24"><rect x="4" y="4" width="7" height="7" rx="1" /><rect x="13" y="4" width="7" height="7" rx="1" /><rect x="4" y="13" width="7" height="7" rx="1" /><rect x="13" y="13" width="7" height="7" rx="1" /></symbol>
    <symbol id="i-versions" viewBox="0 0 24 24"><path d="M12 3l9 5-9 5-9-5z" /><path d="M3 13l9 5 9-5" /></symbol>
    <symbol id="i-accident" viewBox="0 0 24 24"><path d="M8 3h8l5 5v8l-5 5H8l-5-5V8z" /><path d="M12 8v5M12 16v.5" /></symbol>
    <symbol id="i-overview" viewBox="0 0 24 24"><rect x="3" y="3" width="8" height="8" rx="1" /><rect x="13" y="3" width="8" height="5" rx="1" /><rect x="13" y="10" width="8" height="11" rx="1" /><rect x="3" y="13" width="8" height="8" rx="1" /></symbol>
    <symbol id="i-values" viewBox="0 0 24 24"><path d="M9 6h11M9 12h11M9 18h11M4 6h1M4 12h1M4 18h1" /></symbol>
    <symbol id="i-shield" viewBox="0 0 24 24"><path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z" /><path d="M12 8v5M12 16v.5" /></symbol>
    <symbol id="i-pencil" viewBox="0 0 24 24"><path d="M4 20h4L19 9l-4-4L4 16z" /><path d="M13 7l4 4" /></symbol>
    <symbol id="i-publish" viewBox="0 0 24 24"><path d="M12 15V4M7 9l5-5 5 5" /><path d="M5 14v6h14v-6" /></symbol>
</svg>
```

`web/Views/Shared/_StatusBadge.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model StatusBadge
@* One status as a badge (redesign R3, decision 36): a square Battery tag (#22) or a round Passport pill (#2). *@
<span class="bp-badge is-@Model.Kind @Model.Css"><svg class="bp-i" aria-hidden="true"><use href="#i-@Model.Icon" /></svg><span class="bp-badge-kind">@(Model.Kind == StatusBadge.BatteryKind ? "Battery" : "Passport")</span>@Model.Words
    @if (Model.Detail is { } detail)
    {
        <small>@detail</small>
    }
</span>
```

`web/Views/Shared/_StatusHelp.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@using BatteryPassWeb.Dpp.Store
@* "What do the two statuses mean?" (redesign R3, owner's point 9): the ? button beside the badges opens this explainer.
   A popover needs no script; the explainer is written once per page, however many buttons point at it. *@
<button type="button" class="bp-status-help-button" popovertarget="status-help" aria-label="What do the two statuses mean?" title="What do the two statuses mean?"><svg class="bp-i" aria-hidden="true"><use href="#i-help" /></svg></button>
@if (!Context.Items.ContainsKey("status-help"))
{
    Context.Items["status-help"] = true;
    <div id="status-help" class="bp-status-help" popover>
        <h2>Two statuses</h2>
        <p class="mb-0"><strong>Battery</strong> (square tag) says what has happened to the battery. <strong>Passport</strong> (round pill) says whether this passport is the one that counts.</p>
        <dl>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("original"))</dt><dd>As made; never repaired or reused.</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("re-used"))</dt><dd>Back in use for its first purpose: after a repair (same passport), or prepared for re-use and placed on the market again (new passport).</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("repurposed"))</dt><dd>Now used for a different purpose. It gets a new passport.</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("remanufactured"))</dt><dd>Rebuilt to work as new. It gets a new passport.</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("waste"))</dt><dd>Taken out of use for recycling. Final; the team can then mark the passport for deletion.</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Active))</dt><dd>The passport that counts now.</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Archived))</dt><dd>An older version of this passport; a newer one counts.</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Inactive))</dt><dd>Replaced by the battery's new passport after it was placed on the market again.</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Invalid))</dt><dd>Its data shouldn't be relied on, for example after a market surveillance finding. Only the team undoes it.</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.MarkedForDeletion))</dt><dd>The battery is waste and gone; the passport will be deleted.</dd>
            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(null))</dt><dd>Not published yet; only the team sees it.</dd>
        </dl>
        <div class="d-flex justify-content-end"><button type="button" class="bp-secondary-button" popovertarget="status-help" popovertargetaction="hide">Close</button></div>
    </div>
}
```

`web/wwwroot/css/bp-status.css`:

```css
/* ==== The two statuses, on every page (redesign R3, decision 36) ====================================================
   Loaded by the layout, so the public pages, the Registry and the battery pages draw the same badges. The battery status
   (#22, what happened to the battery) is a square "Battery" tag; the passport status (#2, whether this passport counts)
   a round "Passport" pill. Each status has its own colour and icon (Views/Shared/_Icons.cshtml), so they never look
   alike. site.css is not changed. */

/* Icons: one inline SVG sprite; each icon is a stroke drawing in the text colour. */
.bp-i {
  width: 18px;
  height: 18px;
  flex: none;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.8;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.bp-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 28px;
  padding: 2px 10px 2px 7px;
  border: 1px solid;
  font-size: 0.86rem;
  font-weight: 650;
  line-height: 1.25;
  white-space: nowrap;
}

.bp-badge .bp-i {
  width: 16px;
  height: 16px;
}

.bp-badge-kind {
  margin-right: 2px;
  font-size: 0.66rem;
  font-weight: 750;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  opacity: 0.72;
}

.bp-badge small {
  font-weight: 500;
  opacity: 0.85;
}

.bp-badge.is-battery {
  border-radius: 6px;
}

.bp-badge.is-passport {
  border-radius: 999px;
}

/* Battery status #22: what happened to the battery. Calm colours, one per status. */
.bp-badge.is-original { border-color: #cbd5e1; background: #f8fafc; color: #334155; }
.bp-badge.is-reused { border-color: #99f6e4; background: #f0fdfa; color: #0f5e57; }
.bp-badge.is-repurposed { border-color: #ddd6fe; background: #f5f3ff; color: #5b21b6; }
.bp-badge.is-remanufactured { border-color: #bfdbfe; background: #eff6ff; color: #1e40af; }
.bp-badge.is-waste { border-color: #a8a29e; background: #f5f5f4; color: #44403c; }

/* Passport status #2: green while it counts, grey once retired, red when something is wrong. */
.bp-badge.is-active { border-color: #bbf7d0; background: #f0fdf4; color: #166534; }
.bp-badge.is-draft { border-style: dashed; border-color: #94a3b8; background: transparent; color: #475569; }
.bp-badge.is-inactive,
.bp-badge.is-archived { border-color: #cbd5e1; background: #f1f5f9; color: #475569; }
.bp-badge.is-invalid { border-color: #fca5a5; background: #fef2f2; color: #991b1b; }
.bp-badge.is-marked { border-style: dashed; border-color: #f87171; background: #fff; color: #991b1b; }

.bp-badges {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.bp-badges.is-compact .bp-badge {
  min-height: 24px;
  font-size: 0.78rem;
}

/* "What do the two statuses mean?": a small round button opening the explainer (a popover, no script needed). */
.bp-status-help-button {
  display: inline-grid;
  place-items: center;
  width: 30px;
  height: 30px;
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: none;
  color: var(--bp-muted);
}

.bp-status-help-button:hover,
.bp-status-help-button:focus-visible {
  background: rgba(42, 110, 207, 0.08);
  color: var(--bp-primary);
}

.bp-status-help {
  width: min(100% - 32px, 560px);
  max-height: calc(100% - 48px);
  padding: 20px;
  border: 1px solid var(--bp-border);
  border-radius: 12px;
  background: var(--bp-surface);
  color: var(--bp-text);
  box-shadow: 0 22px 44px rgba(15, 23, 42, 0.24);
}

.bp-status-help::backdrop {
  background: rgba(15, 23, 42, 0.45);
}

.bp-status-help h2 {
  margin: 0 0 6px;
  font-size: 1.2rem;
}

.bp-status-help dl {
  display: grid;
  grid-template-columns: max-content minmax(0, 1fr);
  gap: 8px 14px;
  align-items: center;
  margin: 12px 0 16px;
}

.bp-status-help dd {
  margin: 0;
  font-size: 0.9rem;
}

/* An earlier or newer passport: a calm chip beside the passport badge and one plain line, never a banner (Q4). */
.bp-chip {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  min-height: 26px;
  padding: 1px 10px 1px 7px;
  border: 1px solid var(--bp-border);
  border-radius: 999px;
  background: var(--bp-surface);
  color: var(--bp-primary);
  font-size: 0.8rem;
  font-weight: 600;
  text-decoration: none;
  white-space: nowrap;
}

.bp-chip .bp-i {
  width: 15px;
  height: 15px;
}

.bp-chip:hover {
  border-color: var(--bp-primary);
}

.bp-lineage {
  display: flex;
  align-items: center;
  gap: 6px;
  margin: 6px 0 0;
  color: var(--bp-muted);
  font-size: 0.88rem;
}

.bp-lineage .bp-i {
  width: 15px;
  height: 15px;
}

/* A notice that matters (invalid, marked for deletion): one slim line with its action in reach. */
.bp-status-notice {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px 12px;
  margin: 0;
  padding: 8px 10px 8px 12px;
  border: 1px solid #fecaca;
  border-radius: 10px;
  background: #fef2f2;
  color: #7f1d1d;
  font-size: 0.92rem;
}

.bp-status-notice > span {
  flex: 1 1 260px;
  min-width: 0;
}

.bp-status-notice.is-quiet {
  border-color: #e2e8f0;
  background: #f8fafc;
  color: #334155;
}

/* The Registry's waiting card stands out (owner's point 12), and the top bar counts what waits. */
.bp-wait {
  border-left: 5px solid var(--bp-primary);
  background: linear-gradient(90deg, rgba(42, 110, 207, 0.07), transparent 60%), var(--bp-surface);
}

.bp-wait-head {
  display: flex;
  align-items: center;
  gap: 12px;
}

.bp-wait-bell {
  display: inline-grid;
  flex: none;
  place-items: center;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: var(--bp-primary);
  color: #fff;
}

.bp-wait-bell .bp-i {
  width: 22px;
  height: 22px;
}

.bp-nav-count {
  margin-left: 6px;
  padding: 0 7px;
  border-radius: 999px;
  background: #f59e0b;
  color: #1f2937;
  font-size: 0.72rem;
  font-weight: 700;
}

.bp-with-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

/* ---- Dark ---------------------------------------------------------------------------------------------------------- */
[data-theme="dark"] .bp-badge { background: transparent; }
[data-theme="dark"] .bp-badge.is-original { border-color: #64748b; color: #cbd5e1; }
[data-theme="dark"] .bp-badge.is-reused { border-color: #2dd4bf; color: #5eead4; }
[data-theme="dark"] .bp-badge.is-repurposed { border-color: #a78bfa; color: #c4b5fd; }
[data-theme="dark"] .bp-badge.is-remanufactured { border-color: #60a5fa; color: #93c5fd; }
[data-theme="dark"] .bp-badge.is-waste { border-color: #a8a29e; color: #d6d3d1; }
[data-theme="dark"] .bp-badge.is-active { border-color: #22c55e; color: #86efac; }
[data-theme="dark"] .bp-badge.is-draft,
[data-theme="dark"] .bp-badge.is-inactive,
[data-theme="dark"] .bp-badge.is-archived { border-color: #64748b; color: #cbd5e1; }
[data-theme="dark"] .bp-badge.is-invalid,
[data-theme="dark"] .bp-badge.is-marked { border-color: #f87171; color: #fca5a5; }
[data-theme="dark"] .bp-status-notice { border-color: #7f1d1d; background: rgba(127, 29, 29, 0.25); color: #fecaca; }
[data-theme="dark"] .bp-status-notice.is-quiet { border-color: var(--bp-border); background: transparent; color: var(--bp-text); }
[data-theme="dark"] .bp-status-help { background: #101f35; }
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusBadgeTests"`
Expected: `Passed!  - Failed:     0, Passed:    20`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2293`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Look/StatusBadgeTests.cs web/Dpp/Pages/StatusBadge.cs web/Views/Shared/_Icons.cshtml web/Views/Shared/_Layout.cshtml web/Views/Shared/_StatusBadge.cshtml web/Views/Shared/_StatusHelp.cshtml web/wwwroot/css/bp-status.css
git commit -m "feat(dpp): the battery and passport statuses as two badges, with an explainer" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The passport pages: both badges, and an earlier or new passport calmly

The public summary and the detail page show the passport's badge for everyone and the battery's for its readers only (#22 is purchaser data). An earlier or new passport is a chip beside the badge and one dated line, instead of a banner above the page. Only readers allowed #22 read what happened ("Repurposed on …"); everyone else reads the facts without the status word. A test passport wears a small amber tag that explains itself on a tap, instead of the yellow banner. Invalid and marked passports keep a red line, with the reason for the team and market surveillance.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/StatusBadgePagesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`
- Modify: `web/Views/Passport/_PassportHero.cshtml`
- Create: `web/Views/Shared/_TestTag.cshtml`
- Modify: `web/wwwroot/css/bp-status.css`

- [ ] **Step 1: Write the failing tests**

Patch `r3-task2-tests.patch` (`BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`, `BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs`). Save the block as `../r3-task2-tests.patch`, outside the worktree, and run `git apply ../r3-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
index 32aecf0..0f442ac 100644
--- a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
+++ b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
@@ -13,8 +13,8 @@ public sealed class BatteryRouteAndRegistryTests
 
         Assert.Contains("Battery ID", hero);
         Assert.Contains("Passport ID", hero);
-        Assert.Contains(">@Model.StatusWords<", hero); // archived, inactive, … (step 4c-1)
-        Assert.Contains(">Active<", hero);
+        // Active, archived, inactive, … as a badge (step 4c-1, redesign R3).
+        Assert.Contains("StatusBadge.Passport(Model.IsCurrent ? BatteryPassWeb.Dpp.Store.DppStatuses.Active : Model.Version.DppStatus)", hero);
         Assert.Contains("Battery model", hero);
         Assert.DoesNotContain("Battery version", hero);
     }
diff --git a/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs b/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs
index 7dde8c3..ce4f69d 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs
@@ -178,7 +178,7 @@ public sealed class PassportControllerTests(MongoFixture mongo)
 
         Assert.DoesNotContain("Cluster", hero, StringComparison.Ordinal);
         Assert.DoesNotContain("Battery status", hero, StringComparison.Ordinal);
-        Assert.Contains("Test passport: this battery's IDs start with TEST-.", hero, StringComparison.Ordinal);
+        Assert.Contains("@await Html.PartialAsync(\"_TestTag\", \"Test passport\")", hero, StringComparison.Ordinal); // a tag since redesign R3
         Assert.Contains("Model.PictureHref", hero, StringComparison.Ordinal);
         Assert.Contains("Open the current version", hero, StringComparison.Ordinal);
         Assert.Contains("Model.Audiences", tabs, StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs b/BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs
index fc76f9f..7fe34cd 100644
--- a/BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs
@@ -108,9 +108,10 @@ public sealed class SuccessionPagesTests(MongoFixture mongo)
         var oldSummary = (await pages.SummaryAsync(Reader.Anonymous, oldId))!;
         var newDetail = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), newId))!;
 
+        var day = ValueWords.Day(old.Succession.StartedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
         Assert.Equal("Inactive", oldSummary.StatusWords);
-        Assert.Equal(new PassportNotice("Inactive", "This battery was repurposed and has a new passport.", PassportPages.SummaryHref(newId), "Open the new passport"), oldSummary.Notice);
-        Assert.Equal(new PassportNotice("Active", "This passport replaces an earlier one of the same battery.", PassportPages.DetailHref(oldId), "Open the earlier passport"), newDetail.Notice);
+        Assert.Equal(new PassportNotice("Inactive", $"This battery continues on a new passport since {day}.", PassportPages.SummaryHref(newId), "New passport") { Calm = true }, oldSummary.Notice); // no #22 word for anyone
+        Assert.Equal(new PassportNotice("Active", $"Repurposed on {day}. Its earlier passport is inactive and stays readable.", PassportPages.DetailHref(oldId), "Earlier passport") { Calm = true }, newDetail.Notice);
     }
 
     [Fact]
@@ -118,11 +119,13 @@ public sealed class SuccessionPagesTests(MongoFixture mongo)
     {
         var rig = await new SuccessionTests(mongo).RepurposedAsync();
         await SuccessionTests.Service(rig).SaveOtherOperatorAsync(PublicationTesting.Team, rig.PackId, new OtherOperator(null, "Example Remanufacturing AB", "https://example.com/p/1"), link: true);
-        var oldId = Text((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!, "uniqueProductIdentifier")!;
+        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
+        var oldId = Text(old, "uniqueProductIdentifier")!;
+        var day = ValueWords.Day(old.Succession!.StartedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
 
         var summary = (await PassportPagesTests.Pages(rig).SummaryAsync(Reader.Anonymous, oldId))!;
 
-        Assert.Equal(new PassportNotice("Inactive", "This battery was repurposed by Example Remanufacturing AB, which issued its new passport.", "https://example.com/p/1", "https://example.com/p/1"), summary.Notice);
+        Assert.Equal(new PassportNotice("Inactive", $"This battery continues on a new passport issued by Example Remanufacturing AB since {day}.", "https://example.com/p/1", "New passport") { Calm = true }, summary.Notice);
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Dpp/StatusBadgePagesTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R3 on the passport pages: the passport's status for everyone, the battery's (#22, purchaser data) only for
/// its readers, and an earlier or newer passport as a calm chip and line (the owner's answer to Q4).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class StatusBadgePagesTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheBatteryStatus_ShouldBeShownOnlyToItsReaders()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var id = PassportPagesTests.Identifier(rig);
        var pages = PassportPagesTests.Pages(rig);

        var anyone = (await pages.SummaryAsync(Reader.Anonymous, id))!;
        var itsCluster = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), id))!;
        var surveillance = (await pages.DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), id))!;
        var recycler = (await pages.DetailAsync(Reader.With("recycler@example.com", ReaderRole.LiRecycling), id))!;

        Assert.Null(StatusBadge.Battery(anyone.BatteryStatus));
        Assert.Equal("Original", StatusBadge.Battery(itsCluster.BatteryStatus)!.Words);
        Assert.Equal("Original", StatusBadge.Battery(surveillance.BatteryStatus)!.Words);
        Assert.Null(StatusBadge.Battery(recycler.BatteryStatus));
    }

    [Fact]
    public async Task AnyoneWhoMayNotSeeTheBatteryStatus_ShouldNotReadItInTheNoticesEither()
    {
        // #22 is purchaser data, and "repurposed" is its value: the public reads the facts without it (manager, 2 Nov 2026).
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryPack>(rig.PackId))!;
        var successor = (await rig.Store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryPack>(old.Succession!.SuccessorPackId!))!;
        var pages = PassportPagesTests.Pages(rig);
        string Id(BatteryPassWeb.Dpp.Store.BatteryPack pack) => pack.Values["uniqueProductIdentifier"].Value!.ToString();
        var day = ValueWords.Day(old.Succession.StartedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        string[] statusWords = ["original", "re-used", "repurposed", "remanufactured", "waste"];

        var notices = new[]
        {
            (await pages.SummaryAsync(Reader.Anonymous, Id(old)))!.Notice!,
            (await pages.SummaryAsync(Reader.Anonymous, Id(successor)))!.Notice!,
            (await pages.SummaryAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), Id(successor)))!.Notice!, // the summary is public for everyone
            (await pages.DetailAsync(Reader.With("recycler@example.com", ReaderRole.LiRecycling), Id(successor)))!.Notice!,
        };
        var itsCluster = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), Id(successor)))!.Notice!;

        Assert.All(notices, notice => Assert.DoesNotContain(statusWords, word => notice.Text.Contains(word, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal($"Replaces an earlier passport of this battery (since {day}).", notices[1].Text);
        Assert.Equal($"Repurposed on {day}. Its earlier passport is inactive and stays readable.", itsCluster.Text);
    }

    [Fact]
    public void ATestPassport_ShouldWearATagThatExplainsItselfOnATap()
    {
        // A tag beside the badges, not a full-width banner (owner's point 7); a tap or a hover explains it (manager, 2 Nov 2026).
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var tag = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_TestTag.cshtml"));

        Assert.Contains("@await Html.PartialAsync(\"_TestTag\", \"Test passport\")", hero, StringComparison.Ordinal);
        Assert.Contains("<p class=\"bp-test-note\">Test identifiers:", hero, StringComparison.Ordinal); // the detail page says it in words
        Assert.DoesNotContain("<p class=\"bp-warning\">Test passport", hero, StringComparison.Ordinal);
        Assert.Contains("<button type=\"button\" class=\"bp-test-tag\" popovertarget=\"test-help\"", tag, StringComparison.Ordinal);
        Assert.Contains("<div id=\"test-help\" class=\"bp-status-help bp-test-help\" popover>", tag, StringComparison.Ordinal);
        Assert.Contains("@if (!Context.Items.ContainsKey(\"test-help\"))", tag, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHero_ShouldShowBothBadgesAndFoldItsNoticeIntoThem()
    {
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.Contains("@if (StatusBadge.Battery(Model.BatteryStatus) is { } battery)", hero, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_StatusHelp\")", hero, StringComparison.Ordinal);
        Assert.Contains("@if (Model.Notice is { Calm: true, Href: { } lineageHref } lineage)", hero, StringComparison.Ordinal);
        Assert.Contains("<a class=\"bp-chip\" href=\"@lineageHref\">", hero, StringComparison.Ordinal);
        Assert.Contains("<p class=\"bp-lineage\" data-passport-notice>", hero, StringComparison.Ordinal);
        Assert.Contains("<p class=\"bp-status-notice mt-2\" data-passport-notice>", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("<p class=\"bp-warning\" data-passport-notice>", hero, StringComparison.Ordinal); // no banner above the page (point 7)
        Assert.DoesNotContain("bp-status-pill", hero, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryRouteAndRegistryTests | FullyQualifiedName~PassportControllerTests | FullyQualifiedName~StatusBadgePagesTests | FullyQualifiedName~SuccessionPagesTests"`
Expected: the build fails with CS0117, CS1061 (for example `error CS0117 'PassportNotice' does not contain a definition for 'Calm'`).

- [ ] **Step 3: Write the code**

Patch `r3-task2-code.patch` (`web/Dpp/Pages/PassportPages.cs`, `web/Views/Passport/_PassportHero.cshtml`, `web/wwwroot/css/bp-status.css`). Save the block as `../r3-task2-code.patch`, outside the worktree, and run `git apply ../r3-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index 2584c0c..ba40484 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -56,6 +56,12 @@ public sealed record PassportPage(
     /// <summary>The first line of a public value in words, or "-".</summary>
     public string Text(int number) => Value(number)?.Lines.FirstOrDefault()?.Text ?? "-";
 
+    /// <summary>
+    /// The battery status (#22) in words for the badge (redesign R3); null unless the reader may see it. It is purchaser
+    /// data, so it comes only from the detail page's audience sections, never from the public ones.
+    /// </summary>
+    public string? BatteryStatus => Audiences.SelectMany(section => section.Values).FirstOrDefault(value => value.Attribute.Number == 22)?.Lines.FirstOrDefault()?.Text;
+
     /// <summary>How many modules the battery has (decision 31): app data, public, not part of the signed passport.</summary>
     public int ModuleCount { get; init; }
 
@@ -88,7 +94,14 @@ public sealed record PassportPage(
 /// One line at the top of both passport pages when the passport isn't simply active, or replaces an earlier one (mock-up
 /// S7, step 4c): what it says, and where it leads (a passport page, or another operator's address).
 /// </summary>
-public sealed record PassportNotice(string Pill, string Text, string? Href, string? LinkText);
+public sealed record PassportNotice(string Pill, string Text, string? Href, string? LinkText)
+{
+    /// <summary>
+    /// An earlier or newer passport of the same battery: drawn calmly, as a small chip beside the passport badge and one
+    /// plain line (redesign R3, owner's answer to Q4), not as a warning.
+    /// </summary>
+    public bool Calm { get; init; }
+}
 
 /// <summary>Where a published passport is: its version, and the battery's ID (#6) and serial number (#11).</summary>
 public sealed record PassportLocation(PassportVersion Version, string Identifier, string SerialNumber)
@@ -285,7 +298,7 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
             ModuleDescription = sources.Model.ModuleDescription,
             Modules = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Modules : null,
             Repairs = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Repairs.AsEnumerable().Reverse().ToArray() : null,
-            Notice = await NoticeAsync(version, sources.Pack, detail, cancellationToken),
+            Notice = await NoticeAsync(version, sources.Pack, detail, detail && AudienceRules.MaySee(who, AttributeCatalog.ByNumber(22), clusters), cancellationToken),
             MarkReason = detail && sources.Pack.Mark is { Reason: { } reason } mark && version.DppStatus == mark.Status
                 && (who.Roles.Contains(ReaderRole.Team) || who.Roles.Contains(ReaderRole.MarketSurveillance))
                 ? $"{reason} (marked by {(mark.ByRole == "MarketSurveillance" ? "market surveillance" : "the team")}, {mark.By})"
@@ -296,9 +309,11 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
 
     /// <summary>
     /// The S7 line: an inactive passport leads to the battery's new one (Scania's page, or another operator's address);
-    /// a passport that replaces an earlier one leads back to it (design 7.5, step 4c-1). Null for any other passport.
+    /// a passport that replaces an earlier one leads back to it (design 7.5, step 4c-1), both calmly and dated (redesign
+    /// R3). Only a reader who may see the battery status (#22, purchaser data) reads what happened ("Repurposed on …");
+    /// everyone else reads the facts without it. Invalid and marked passports keep a warning. Null for any other passport.
     /// </summary>
-    private async Task<PassportNotice?> NoticeAsync(PassportVersion version, BatteryPack pack, bool detail, CancellationToken cancellationToken)
+    private async Task<PassportNotice?> NoticeAsync(PassportVersion version, BatteryPack pack, bool detail, bool showsStatus, CancellationToken cancellationToken)
     {
         string Href(string identifier) => detail ? DetailHref(identifier) : SummaryHref(identifier);
         if (version.DppStatus == DppStatuses.Invalid)
@@ -313,35 +328,50 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
 
         if (version.DppStatus == DppStatuses.Inactive && pack.Succession is { } succession)
         {
+            var what = Happened(succession);
+            var since = Day(succession.StartedAtUtc);
             if (succession.SuccessorPackId is { } successorId && await store.GetAsync<BatteryPack>(successorId, cancellationToken) is { } successor
                 && PackText(successor, "uniqueProductIdentifier") is { } newIdentifier)
             {
-                return new PassportNotice("Inactive", $"This battery was {succession.Status} and has a new passport.", Href(newIdentifier), "Open the new passport");
+                var text = showsStatus ? $"{what}; the battery continues on its new passport." : $"This battery continues on a new passport since {since}.";
+                return new PassportNotice("Inactive", text, Href(newIdentifier), "New passport") { Calm = true };
             }
 
             if (succession.OtherPassport is { } other)
             {
                 var link = Uri.TryCreate(other, UriKind.Absolute, out var address) && address.Scheme is "https" or "http" ? other : null;
-                return new PassportNotice(
-                    "Inactive",
-                    $"This battery was {succession.Status} by {succession.OtherOperatorInfo ?? succession.OtherOperatorId}, which issued its new passport{(link is null ? $": {other}" : string.Empty)}.",
-                    link,
-                    link);
+                var issuer = succession.OtherOperatorInfo ?? succession.OtherOperatorId;
+                var tail = link is null ? $": {other}" : string.Empty;
+                var text = showsStatus
+                    ? $"{what} by {issuer}, which issued its new passport{tail}."
+                    : $"This battery continues on a new passport issued by {issuer} since {since}{tail}.";
+                return new PassportNotice("Inactive", text, link, link is null ? null : "New passport") { Calm = true };
             }
 
-            return new PassportNotice("Inactive", $"This battery was {succession.Status} and has a new passport.", null, null);
+            return new PassportNotice("Inactive", showsStatus ? $"{what}; the battery has a new passport." : $"This battery has a new passport since {since}.", null, null) { Calm = true };
         }
 
         if (version.DppStatus == DppStatuses.Active && pack.SuccessorOf is { } predecessorId
             && await store.GetAsync<BatteryPack>(predecessorId, cancellationToken) is { } predecessor
             && PackText(predecessor, "uniqueProductIdentifier") is { } oldIdentifier)
         {
-            return new PassportNotice("Active", "This passport replaces an earlier one of the same battery.", Href(oldIdentifier), "Open the earlier passport");
+            var text = predecessor.Succession is not { } earlier
+                ? "This passport replaces an earlier one of the same battery."
+                : showsStatus
+                    ? $"{Happened(earlier)}. Its earlier passport is inactive and stays readable."
+                    : $"Replaces an earlier passport of this battery (since {Day(earlier.StartedAtUtc)}).";
+            return new PassportNotice("Active", text, Href(oldIdentifier), "Earlier passport") { Calm = true };
         }
 
         return null;
     }
 
+    /// <summary>What happened to the battery and when, in words: "Repurposed on 2 Nov 2026". Only for readers of #22.</summary>
+    private static string Happened(Succession succession) =>
+        $"{StatusBadge.Battery(succession.Status)?.Words ?? succession.Status} on {Day(succession.StartedAtUtc)}";
+
+    private static string Day(DateTime utc) => ValueWords.Day(utc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
+
     /// <summary>The attributes' values in the cut passport, in catalogue order; the detail page adds each value's origin.</summary>
     private IReadOnlyList<ShownValue> Shown(JsonObject passport, PassportSources sources, bool detail, IEnumerable<AttributeDefinition> attributes)
     {
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index 48948b9..64c2356 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -8,24 +8,6 @@
     var detailHref = PassportPages.DetailHref(Model.Address);
     var signIn = $"/login?next={Uri.EscapeDataString(detailHref)}";
 }
-@if (Model.IsTest)
-{
-    <p class="bp-warning">Test passport: this battery's IDs start with TEST-. Real ones come once the identifier scheme is chosen.</p>
-}
-@if (Model.Notice is { } notice)
-{
-    <p class="bp-warning" data-passport-notice>
-        <strong>@notice.Pill.</strong> @notice.Text
-        @if (notice.Href is not null)
-        {
-            <a href="@notice.Href">@notice.LinkText</a>
-        }
-        @if (Model.MarkReason is { } reason)
-        {
-            <span class="d-block mt-1">Reason, seen only by the manufacturer's team and market surveillance: @reason</span>
-        }
-    </p>
-}
 @if (Model.MayMarkInvalid)
 {
     <details class="bp-warning" data-mark-invalid>
@@ -46,14 +28,19 @@
                 <span class="bp-summary-title">Battery</span>
                 <h1>@Model.Text(11)</h1>
                 <p class="text-secondary">Model: @Model.Text(10)</p>
-                <div class="d-flex flex-wrap gap-2 align-items-center mt-3">
-                    @if (Model.IsCurrent)
+                @* The two statuses as badges (redesign R3); the battery's only for readers who may see it (#22 is purchaser data). *@
+                <div class="bp-badges mt-3" data-passport-badges>
+                    @if (StatusBadge.Battery(Model.BatteryStatus) is { } battery)
                     {
-                        <span class="bp-status-pill is-active">Active</span>
+                        @await Html.PartialAsync("_StatusBadge", battery)
                     }
-                    else
+                    @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(Model.IsCurrent ? BatteryPassWeb.Dpp.Store.DppStatuses.Active : Model.Version.DppStatus))
+                    @if (Model.Notice is { Calm: true, Href: { } lineageHref } lineage)
+                    {
+                        <a class="bp-chip" href="@lineageHref"><svg class="bp-i" aria-hidden="true"><use href="#i-@(lineage.Pill == "Active" ? "history" : "arrow")" /></svg>@lineage.LinkText</a>
+                    }
+                    @if (!Model.IsCurrent)
                     {
-                        <span class="bp-status-pill is-inactive">@Model.StatusWords</span>
                         @if (Model.Current is not null)
                         {
                             <a href="@(detail ? PassportPages.DetailHref(Model.Identifier) : PassportPages.SummaryHref(Model.Identifier))" class="bp-report-action" aria-label="Open the current version" title="Open the current version" data-tooltip="Open the current version" data-action-icon="latest">
@@ -65,7 +52,30 @@
                             </a>
                         }
                     }
+                    @if (Model.IsTest)
+                    {
+                        @* A tag, not a banner (owner's point 7); a tap or a hover explains it. *@
+                        @await Html.PartialAsync("_TestTag", "Test passport")
+                    }
+                    @await Html.PartialAsync("_StatusHelp")
                 </div>
+                @if (Model.Notice is { Calm: true } calm)
+                {
+                    <p class="bp-lineage" data-passport-notice><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>@calm.Text</p>
+                }
+                else if (Model.Notice is { } warning)
+                {
+                    <p class="bp-status-notice mt-2" data-passport-notice>
+                        <svg class="bp-i" aria-hidden="true"><use href="#i-alert" /></svg>
+                        <span>
+                            <strong>@warning.Pill.</strong> @warning.Text
+                            @if (Model.MarkReason is { } reason)
+                            {
+                                <span class="d-block mt-1">Reason, seen only by the manufacturer's team and market surveillance: @reason</span>
+                            }
+                        </span>
+                    </p>
+                }
                 @if (!Model.IsCurrent && Model.Current is not null)
                 {
                     <p class="text-secondary small mt-2 mb-0">An older version of this passport. <a href="@(detail ? PassportPages.DetailHref(Model.Identifier) : PassportPages.SummaryHref(Model.Identifier))">Open the current version</a></p>
@@ -76,6 +86,10 @@
                 <span class="bp-summary-title">Identifiers</span>
                 <p class="bp-passport-id"><span class="bp-id-label">Passport ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Version.DigitalProductPassportId</span>@await Html.PartialAsync("_CopyIdButton", Model.Version.DigitalProductPassportId)</span></p>
                 <p class="bp-passport-id"><span class="bp-id-label">Battery ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Identifier</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></p>
+                @if (Model.IsTest && detail)
+                {
+                    <p class="bp-test-note">Test identifiers: these IDs start with TEST-, so this isn't a real passport yet. Real ones come once the identifier scheme is chosen.</p>
+                }
             </section>
 
             <section class="bp-summary-id-panel bp-summary-qr-panel bp-report-identity-panel bp-report-identity-panel--qr">
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index 96c3aa1..4e910f3 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -246,6 +246,37 @@
   gap: 8px;
 }
 
+/* A test passport or battery: a small amber tag that explains itself on a tap or a hover (owner's point 7). */
+.bp-test-tag {
+  display: inline-flex;
+  align-items: center;
+  gap: 4px;
+  min-height: 24px;
+  padding: 1px 8px;
+  border: 1px solid #fcd34d;
+  border-radius: 999px;
+  background: #fffbeb;
+  color: #92400e;
+  font-size: 0.74rem;
+  font-weight: 650;
+  cursor: help;
+}
+
+.bp-test-tag .bp-i {
+  width: 14px;
+  height: 14px;
+}
+
+.bp-test-help {
+  width: min(100% - 32px, 440px);
+}
+
+.bp-test-note {
+  margin: 4px 0 0;
+  color: #92400e;
+  font-size: 0.85rem;
+}
+
 /* ---- Dark ---------------------------------------------------------------------------------------------------------- */
 [data-theme="dark"] .bp-badge { background: transparent; }
 [data-theme="dark"] .bp-badge.is-original { border-color: #64748b; color: #cbd5e1; }
@@ -262,3 +293,5 @@
 [data-theme="dark"] .bp-status-notice { border-color: #7f1d1d; background: rgba(127, 29, 29, 0.25); color: #fecaca; }
 [data-theme="dark"] .bp-status-notice.is-quiet { border-color: var(--bp-border); background: transparent; color: var(--bp-text); }
 [data-theme="dark"] .bp-status-help { background: #101f35; }
+[data-theme="dark"] .bp-test-tag { border-color: #b45309; background: transparent; color: #fcd34d; }
+[data-theme="dark"] .bp-test-note { color: #fcd34d; }
```

`web/Views/Shared/_TestTag.cshtml`:

```cshtml
@model string
@* A test passport's or battery's tag (redesign R3 and R4; owner's point 7): small, amber, and explained on a tap or a
   hover, instead of a banner across the page. The explanation is written once per page, however many tags point at it. *@
<button type="button" class="bp-test-tag" popovertarget="test-help" title="IDs that start with TEST- are test identifiers">@Model<svg class="bp-i" aria-hidden="true"><use href="#i-help" /></svg></button>
@if (!Context.Items.ContainsKey("test-help"))
{
    Context.Items["test-help"] = true;
    <div id="test-help" class="bp-status-help bp-test-help" popover>
        <h2>A test passport</h2>
        <p>This battery's IDs start with <strong>TEST-</strong>: it isn't a real passport yet. Real IDs come once the identifier scheme is chosen.</p>
        <div class="d-flex justify-content-end"><button type="button" class="bp-secondary-button" popovertarget="test-help" popovertargetaction="hide">Close</button></div>
    </div>
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryRouteAndRegistryTests | FullyQualifiedName~PassportControllerTests | FullyQualifiedName~StatusBadgePagesTests | FullyQualifiedName~SuccessionPagesTests"`
Expected: `Passed!  - Failed:     0, Passed:    36`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2297`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs BatteryPassWeb.Tests/Dpp/StatusBadgePagesTests.cs BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs web/Dpp/Pages/PassportPages.cs web/Views/Passport/_PassportHero.cshtml web/Views/Shared/_TestTag.cshtml web/wwwroot/css/bp-status.css
git commit -m "feat(dpp): the passport pages show both statuses and link earlier and new passports calmly" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The Registry and the battery list: badges, an Open button, what waits

Every row shows both badges (the battery's only for its readers). A cluster admin's row has a labelled Open button instead of the small "Manage" link. "Waiting for you" stands out, with Accept first, and the Registry entry in the top bar counts the hand-overs waiting.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs`
- Modify: `web/Dpp/Editing/HandOverService.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/RegistryList.cs`
- Modify: `web/Views/Batteries/Batteries.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `web/Views/Shared/_Layout.cshtml`
- Create: `web/Views/Shared/_WaitingCount.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r3-task3-tests.patch` (`BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`). Save the block as `../r3-task3-tests.patch`, outside the worktree, and run `git apply ../r3-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
index 5a24f1b..cb791f5 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
@@ -147,7 +147,7 @@ public sealed class PassportMarkPagesTests(MongoFixture mongo)
         Assert.Contains("~/Views/Batteries/_PassportStatusCard.cshtml\", marks", battery, StringComparison.Ordinal);
         Assert.Contains("Model.MayMarkInvalid", hero, StringComparison.Ordinal);
         Assert.Contains("Model.MarkReason is { } reason", hero, StringComparison.Ordinal);
-        Assert.Contains("DppStatuses.Words(shown.DppStatus)", registry, StringComparison.Ordinal);
+        Assert.Contains("StatusBadge.Passport(shown.DppStatus)", registry, StringComparison.Ordinal); // a badge since redesign R3
     }
 
     private static PassportController Controller(PublicationRig rig, string email, string role)
```

`BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R3 on the lists: both statuses on every row (the battery's only for its readers), an Open button where the
/// cluster admin's small "Manage" link was (owner's point 11), and "Waiting for you" standing out (point 12).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RegistryBadgesTests(MongoFixture mongo)
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts]));

    [Fact]
    public async Task EachRow_ShouldCarryItsBatteryStatusForItsReadersOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var list = await BatteryTesting.Pages(rig).ListAsync(null);

        var itsCluster = RegistryList.Build(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), list);
        var surveillance = RegistryList.Build(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), list);
        var recycler = RegistryList.Build(Reader.With("recycler@example.com", ReaderRole.LiRecycling), list);

        Assert.Equal("original", list.Rows.Single(row => row.PackId == rig.PackId).BatteryStatus);
        Assert.Contains(rig.PackId, itsCluster.BatteryStatusShown);
        Assert.Contains(rig.PackId, surveillance.BatteryStatusShown);
        Assert.Single(recycler.Rows); // listed, as every published battery is for them (decision 30) …
        Assert.Empty(recycler.BatteryStatusShown); // … but #22 is purchaser data
    }

    [Fact]
    public async Task TheTopBar_ShouldCountTheHandOversWaitingForAClusterAdmin()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = HandOverTests.Service(rig);
        var southAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

        Assert.Equal(0, await service.WaitingCountAsync(southAdmin));
        await service.AskAsync(ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Equal(1, await service.WaitingCountAsync(southAdmin));
        Assert.Equal(0, await service.WaitingCountAsync(ChangeSender.Team("team@example.com")));
        Assert.Equal(2, View("Shared", "_Layout.cshtml").Split("Open Registry<partial name=\"_WaitingCount\" /></a>").Length - 1);
        Assert.Contains("<span class=\"bp-nav-count\"", View("Shared", "_WaitingCount.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheRegistry_ShouldShowBothBadgesAnOpenButtonAndAWaitingCardThatStandsOut()
    {
        var registry = View("Registry", "Index.cshtml");
        var waiting = registry[registry.IndexOf("id=\"waiting\"", StringComparison.Ordinal)..registry.IndexOf("</section>", StringComparison.Ordinal)];

        Assert.Contains("bp-wait mt-4\" id=\"waiting\"", registry, StringComparison.Ordinal);
        Assert.Contains("<use href=\"#i-bell\" />", waiting, StringComparison.Ordinal);
        Assert.True(waiting.IndexOf("value=\"true\"", StringComparison.Ordinal) < waiting.IndexOf("value=\"false\"", StringComparison.Ordinal)); // Accept first
        Assert.Contains("@if (Model.BatteryStatusShown.Contains(row.PackId) && StatusBadge.Battery(row.BatteryStatus) is { } battery)", registry, StringComparison.Ordinal);
        Assert.Contains("StatusBadge.Passport(shown.DppStatus)", registry, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Open the battery page of @row.SerialNumber\">Open<svg", registry, StringComparison.Ordinal);
        Assert.DoesNotContain(">Manage</a>", registry, StringComparison.Ordinal);
        Assert.Contains("StatusBadge.Battery(row.BatteryStatus)", View("Batteries", "Batteries.cshtml"), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkPagesTests | FullyQualifiedName~RegistryBadgesTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'BatteryRow' does not contain a definition for 'BatteryStatus'`).

- [ ] **Step 3: Write the code**

Patch `r3-task3-code.patch` (`web/Dpp/Editing/HandOverService.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/RegistryList.cs`, `web/Views/Batteries/Batteries.cshtml`, `web/Views/Registry/Index.cshtml`, `web/Views/Shared/_Layout.cshtml`). Save the block as `../r3-task3-code.patch`, outside the worktree, and run `git apply ../r3-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Editing/HandOverService.cs b/web/Dpp/Editing/HandOverService.cs
index 7a9b658..8f49038 100644
--- a/web/Dpp/Editing/HandOverService.cs
+++ b/web/Dpp/Editing/HandOverService.cs
@@ -166,6 +166,15 @@ public sealed class HandOverService(DppStore store, ClusterRepository clusters,
         return null;
     }
 
+    /// <summary>How many hand-overs wait for the sender's answer, for the Registry's entry in the top bar (redesign R3).</summary>
+    public async Task<int> WaitingCountAsync(ChangeSender sender, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(sender);
+        return sender.Kind != SenderKind.ClusterAdmin || sender.ClusterIds.Count == 0
+            ? 0
+            : (await store.ListWaitingHandOversToAsync(sender.ClusterIds, cancellationToken)).Count;
+    }
+
     /// <summary>The hand-overs waiting for the sender's answer: to a cluster they are admin of.</summary>
     public async Task<IReadOnlyList<WaitingHandOver>> WaitingForAsync(ChangeSender sender, CancellationToken cancellationToken = default)
     {
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 6752db1..a98f7a7 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -33,6 +33,9 @@ public sealed record BatteryRow(
 
     /// <summary>The passport's DPP status (#2), or "draft".</summary>
     public string StatusName => (Active ?? Latest)?.DppStatus ?? "draft";
+
+    /// <summary>The battery status (#22), shown as a badge only to its readers (redesign R3); null before it has one.</summary>
+    public string? BatteryStatus { get; init; }
 }
 
 public sealed record BatteriesPage(IReadOnlyList<BatteryRow> Rows, string Query, int Total);
@@ -195,6 +198,7 @@ public sealed class BatteryPages(
                 active)
             {
                 Latest = versions[pack.Id].MaxBy(version => version.Number),
+                BatteryStatus = StatusChangeService.CurrentStatus(pack),
             });
         }
 
diff --git a/web/Dpp/Pages/RegistryList.cs b/web/Dpp/Pages/RegistryList.cs
index 1c1b66b..2d70e2a 100644
--- a/web/Dpp/Pages/RegistryList.cs
+++ b/web/Dpp/Pages/RegistryList.cs
@@ -13,6 +13,9 @@ public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query,
     /// <summary>The hand-overs waiting for the signed-in cluster admin's answer (step 4d, mock-up S1).</summary>
     public IReadOnlyList<WaitingHandOver> Waiting { get; init; } = [];
 
+    /// <summary>The batteries whose status (#22, purchaser data) the reader may see: the team, market surveillance, their cluster's (redesign R3).</summary>
+    public IReadOnlySet<string> BatteryStatusShown { get; init; } = new HashSet<string>(StringComparer.Ordinal);
+
     /// <summary>The cluster admin's page of a battery of a cluster they are admin of; null otherwise.</summary>
     public string? ManageHref(BatteryRow row)
     {
@@ -70,6 +73,10 @@ public static class RegistryList
             Scope(reader))
         {
             ManagedClusterIds = editor is { Kind: SenderKind.ClusterAdmin } ? editor.ClusterIds.ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal),
+            BatteryStatusShown = batteries.Rows
+                .Where(row => AudienceRules.MaySeePurchaserData(reader, row.ClusterId is { } cluster ? [cluster] : []))
+                .Select(row => row.PackId)
+                .ToHashSet(StringComparer.Ordinal),
         };
     }
 }
diff --git a/web/Views/Batteries/Batteries.cshtml b/web/Views/Batteries/Batteries.cshtml
index 52de502..da1c1a2 100644
--- a/web/Views/Batteries/Batteries.cshtml
+++ b/web/Views/Batteries/Batteries.cshtml
@@ -54,7 +54,7 @@
                             <th>Model</th>
                             <th>Batch</th>
                             <th>Cluster</th>
-                            <th>Passport</th>
+                            <th>Status</th>
                         </tr>
                     </thead>
                     <tbody>
@@ -77,19 +77,15 @@
                                         @row.ClusterLabel
                                     }
                                 </td>
-                                <td data-label="Passport">
-                                    @if (row.Active is { } active)
-                                    {
-                                        <span class="bp-field-marker is-published">Published · version @active.Number</span>
-                                    }
-                                    else if (row.Latest is { } last)
-                                    {
-                                        <span class="bp-field-marker">@DppStatuses.Words(last.DppStatus) · version @last.Number</span>
-                                    }
-                                    else
-                                    {
-                                        <span class="bp-field-marker">Draft</span>
-                                    }
+                                <td data-label="Status">
+                                    @* Both statuses as badges, as everywhere (redesign R3). *@
+                                    <div class="bp-badges is-compact">
+                                        @if (StatusBadge.Battery(row.BatteryStatus) is { } battery)
+                                        {
+                                            @await Html.PartialAsync("_StatusBadge", battery)
+                                        }
+                                        @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(row.IsDraft ? null : row.StatusName).With((row.Active ?? row.Latest) is { } version ? $"· version {version.Number}" : null))
+                                    </div>
                                 </td>
                             </tr>
                         }
diff --git a/web/Views/Registry/Index.cshtml b/web/Views/Registry/Index.cshtml
index e688c98..517354e 100644
--- a/web/Views/Registry/Index.cshtml
+++ b/web/Views/Registry/Index.cshtml
@@ -15,10 +15,16 @@
     @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
     @if (Model.Waiting.Count > 0)
     {
-        <section class="bp-card bp-console-form-card bp-product-editor-section mt-4" id="waiting">
-            <span class="bp-summary-title">Waiting for you</span>
-            <h2 class="h5 mb-1">@Words.Count(Model.Waiting.Count, "battery", "batteries") handed over to your cluster</h2>
-            <p class="bp-help-text mb-2">Accept it to see its purchaser data and manage it. Until then it stays with the sending cluster.</p>
+        @* Waiting for you stands out from the list (redesign R3, owner's point 12). *@
+        <section class="bp-card bp-console-form-card bp-product-editor-section bp-wait mt-4" id="waiting">
+            <div class="bp-wait-head">
+                <span class="bp-wait-bell"><svg class="bp-i" aria-hidden="true"><use href="#i-bell" /></svg></span>
+                <div>
+                    <span class="bp-summary-title">Waiting for you</span>
+                    <h2 class="h5 mb-0">@Words.Count(Model.Waiting.Count, "battery", "batteries") handed over to your cluster</h2>
+                </div>
+            </div>
+            <p class="bp-help-text mt-2 mb-2">Accept it to see its purchaser data and manage it. Until then it stays with the sending cluster.</p>
             @foreach (var waiting in Model.Waiting)
             {
                 <div class="bp-handover-row">
@@ -29,8 +35,8 @@
                     <form method="post" action="/registry/handovers/@Uri.EscapeDataString(waiting.HandOver.Id)">
                         @Html.AntiForgeryToken()
                         <div class="bp-dialog-actions">
-                            <button type="submit" name="accept" value="false" class="bp-secondary-button">Decline</button>
                             <button type="submit" name="accept" value="true" class="bp-primary-button">Accept</button>
+                            <button type="submit" name="accept" value="false" class="bp-secondary-button">Decline</button>
                         </div>
                     </form>
                 </div>
@@ -64,10 +70,10 @@
                             {
                                 <th>Cluster</th>
                             }
-                            <th>Passport</th>
+                            <th>Status</th>
                             @if (Model.ManagedClusterIds.Count > 0)
                             {
-                                <th><span class="visually-hidden">Manage</span></th>
+                                <th><span class="visually-hidden">Open</span></th>
                             }
                         </tr>
                     </thead>
@@ -86,20 +92,23 @@
                                     <td data-label="Cluster">@row.ClusterLabel</td>
                                 }
                                 @{ var shown = (row.Active ?? row.Latest)!; }
-                                <td data-label="Passport">
-                                    Version @shown.Number
-                                    @if (row.Active is null)
-                                    {
-                                        <span class="bp-status-pill is-inactive">@BatteryPassWeb.Dpp.Store.DppStatuses.Words(shown.DppStatus)</span>
-                                    }
-                                    <small class="d-block text-secondary">updated @ValueWords.Time(shown.LastUpdatedUtc)</small>
+                                <td data-label="Status">
+                                    <div class="bp-badges is-compact">
+                                        @if (Model.BatteryStatusShown.Contains(row.PackId) && StatusBadge.Battery(row.BatteryStatus) is { } battery)
+                                        {
+                                            @await Html.PartialAsync("_StatusBadge", battery)
+                                        }
+                                        @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(shown.DppStatus))
+                                    </div>
+                                    <small class="d-block text-secondary mt-1">Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>
                                 </td>
                                 @if (Model.ManagedClusterIds.Count > 0)
                                 {
                                     <td data-label="">
                                         @if (Model.ManageHref(row) is { } manage)
                                         {
-                                            <a class="bp-text-button" href="@manage">Manage</a>
+                                            @* A labelled button where a small "Manage" link was (owner's point 11). *@
+                                            <a class="bp-secondary-button bp-button-compact bp-with-icon" href="@manage" aria-label="Open the battery page of @row.SerialNumber">Open<svg class="bp-i" aria-hidden="true"><use href="#i-arrow" /></svg></a>
                                         }
                                     </td>
                                 }
diff --git a/web/Views/Shared/_Layout.cshtml b/web/Views/Shared/_Layout.cshtml
index ac1dae4..cf64302 100644
--- a/web/Views/Shared/_Layout.cshtml
+++ b/web/Views/Shared/_Layout.cshtml
@@ -91,7 +91,7 @@
                                         <p>@customerName / @customerRole</p>
                                         <p>@(clusterNames.Count > 0 ? string.Join(", ", clusterNames) : "No cluster")</p>
                                     </div>
-                                    <a href="/registry">Open Registry</a>
+                                    <a href="/registry">Open Registry<partial name="_WaitingCount" /></a>
                                     @if (showAdminLink)
                                     {
                                         <a href="/admin">Admin</a>
@@ -108,7 +108,7 @@
                         <div class="bp-header-primary-links">
                         @if (isAuthenticated)
                         {
-                            <a href="/registry">Open Registry</a>
+                            <a href="/registry">Open Registry<partial name="_WaitingCount" /></a>
                         }
 
                         @if (showAdminLink)
```

`web/Views/Shared/_WaitingCount.cshtml`:

```cshtml
@inject BatteryPassWeb.Dpp.Access.DppReaders Readers
@inject BatteryPassWeb.Dpp.Editing.HandOverService HandOvers
@* The Registry's entry in the top bar counts the hand-overs waiting for a cluster admin (redesign R3, owner's point 12). *@
@{
    if (Context.Items["waiting-count"] is not int count)
    {
        var editor = User.IsInRole("clusterAdmin") ? await Readers.EditorForAsync(User) : null;
        count = editor is null ? 0 : await HandOvers.WaitingCountAsync(editor);
        Context.Items["waiting-count"] = count;
    }
}
@if (count > 0)
{
    <span class="bp-nav-count" title="@BatteryPassWeb.Dpp.Pages.Words.Count(count, "hand-over") waiting for you">@count</span>
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkPagesTests | FullyQualifiedName~RegistryBadgesTests"`
Expected: `Passed!  - Failed:     0, Passed:    11`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2300`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs web/Dpp/Editing/HandOverService.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/RegistryList.cs web/Views/Batteries/Batteries.cshtml web/Views/Registry/Index.cshtml web/Views/Shared/_Layout.cshtml web/Views/Shared/_WaitingCount.cshtml
git commit -m "feat(dpp): the lists show both statuses, an Open button, and what waits for you" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The public hero: the picture inside the card, the facts at full width

The owner's item 10. On the public summary and the detail page, the picture moves from its own column into the hero card, top right, at its own shape and about 160 px tall. The facts below use the card's full width, and "View more" sits in the card under them. On a phone the picture is about 100 px, beside the battery's name. The facts leave out "Passport status", because the Passport badge above shows #2 to everyone; the detail page's passport data sections keep it. Only `_PassportHero` and rules scoped to its new `bp-hero-compact` class change; the other pages and `site.css` stay as they are. One older assertion follows: `AdminFeedbackFollowupTests` now finds the passport status as the badge.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (create): `BatteryPassWeb.Tests/Look/PassportHeroTests.cs`
- Modify: `web/Views/Passport/_PassportHero.cshtml`
- Modify: `web/wwwroot/css/bp-status.css`

- [ ] **Step 1: Write the failing tests**

Patch `r3-task4-tests.patch` (`BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`). Save the block as `../r3-task4-tests.patch`, outside the worktree, and run `git apply ../r3-task4-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
index de7ee60..5dff8bd 100644
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -11,7 +11,7 @@ public sealed class AdminFeedbackFollowupTests
         var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
 
         Assert.DoesNotContain("bp-pill bp-pill-muted", hero);
-        Assert.Contains("<div><dt>Passport status</dt><dd>@Model.Text(2)</dd></div>", hero);
+        Assert.Contains("@await Html.PartialAsync(\"_StatusBadge\", StatusBadge.Passport(", hero); // the badge shows it (redesign R3)
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Look/PassportHeroTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// The owner's item 10 (redesign R3): on the public summary and the detail page, the picture sits inside the hero card,
/// top right, the facts use the card's full width, and "View more" is in the card. Contained to the hero's own markup
/// and rules scoped to it; site.css is unchanged.
/// </summary>
public sealed class PassportHeroTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void ThePicture_ShouldSitInsideTheCardWithTheButtonsUnderTheFacts()
    {
        var hero = Read("web", "Views", "Passport", "_PassportHero.cshtml");
        var card = hero[hero.IndexOf("<article class=\"bp-report-hero-info", StringComparison.Ordinal)..hero.IndexOf("</article>", StringComparison.Ordinal)];

        Assert.Contains("<section class=\"bp-report-hero-card bp-hero-compact\">", hero, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-report-hero-media bp-hero-picture\">", card, StringComparison.Ordinal);
        Assert.True(card.IndexOf("bp-hero-picture", StringComparison.Ordinal) < card.IndexOf("<dl class=\"bp-field-grid", StringComparison.Ordinal));
        Assert.True(card.IndexOf("</dl>", StringComparison.Ordinal) < card.IndexOf("<div class=\"bp-hero-ctas\">", StringComparison.Ordinal));
        Assert.Contains("View more about this passport", card, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside", hero, StringComparison.Ordinal); // no empty picture column beside the card
    }

    [Fact]
    public void TheFacts_ShouldNotRepeatThePassportStatusTheBadgeShows()
    {
        // The Passport badge shows #2 to everyone, so the facts leave it out (owner, 2 Nov 2026); nothing is hidden.
        var hero = Read("web", "Views", "Passport", "_PassportHero.cshtml");
        var facts = hero[hero.IndexOf("<dl class=\"bp-field-grid", StringComparison.Ordinal)..hero.IndexOf("</dl>", StringComparison.Ordinal)];

        Assert.DoesNotContain("Passport status", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("Model.Text(2)", facts, StringComparison.Ordinal);
        Assert.Contains("StatusBadge.Passport(", hero[hero.IndexOf("data-passport-badges", StringComparison.Ordinal)..hero.IndexOf("<dl class=\"bp-field-grid", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public void TheLayout_ShouldBeScopedToTheHeroAndGiveTheFactsTheFullWidth()
    {
        var css = Read("web", "wwwroot", "css", "bp-status.css");
        var hero = css[css.IndexOf(".bp-hero-compact .bp-report-hero-layout {", StringComparison.Ordinal)..css.IndexOf("/* ---- Dark", StringComparison.Ordinal)];

        Assert.Contains("\"battery ids qr picture\"\n    \"fields fields fields fields\"\n    \"cta cta cta cta\";", hero, StringComparison.Ordinal);
        Assert.Contains("    \"battery picture\"\n      \"ids ids\"", hero, StringComparison.Ordinal); // a phone: small, beside the name
        Assert.Contains("  height: 160px;\n  max-width: 220px;", hero, StringComparison.Ordinal);
        Assert.All(hero.Split('\n').Where(line => line.TrimEnd().EndsWith('{') && !line.TrimStart().StartsWith('@')), rule =>
            Assert.Contains(".bp-hero-compact", rule, StringComparison.Ordinal)); // every rule is the hero's own
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminFeedbackFollowupTests | FullyQualifiedName~PassportHeroTests"`
Expected: `Failed:     3, Passed:     9` (failing: `PassportHeroTests.TheFacts_ShouldNotRepeatThePassportStatusTheBadgeShows`, `PassportHeroTests.TheLayout_ShouldBeScopedToTheHeroAndGiveTheFactsTheFullWidth`, `PassportHeroTests.ThePicture_ShouldSitInsideTheCardWithTheButtonsUnderTheFacts`).

- [ ] **Step 3: Write the code**

Patch `r3-task4-code.patch` (`web/Views/Passport/_PassportHero.cshtml`, `web/wwwroot/css/bp-status.css`). Save the block as `../r3-task4-code.patch`, outside the worktree, and run `git apply ../r3-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index 64c2356..51c7e46 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -21,7 +21,9 @@
         </form>
     </details>
 }
-<section class="bp-report-hero-card">
+@* The picture sits inside the card, top right, and the facts use the card's full width (owner's item 10, redesign R3).
+   The facts leave out the passport status: the Passport badge shows #2 to everyone (owner, 2 Nov 2026). *@
+<section class="bp-report-hero-card bp-hero-compact">
     <div class="bp-report-hero-layout">
         <article class="bp-report-hero-info bp-summary-identity-grid bp-report-identity-grid">
             <section class="bp-summary-id-panel bp-report-identity-panel bp-report-identity-panel--battery bp-report-header bp-report-title-block">
@@ -99,6 +101,15 @@
                 </a>
             </section>
 
+            @if (Model.PictureHref is not null)
+            {
+                <div class="bp-report-hero-media bp-hero-picture">
+                    <div class="bp-image-wrap">
+                        <img src="@Model.PictureHref" alt="Picture of the @Model.FamilyName battery family" class="bp-battery-image" />
+                    </div>
+                </div>
+            }
+
             <dl class="bp-field-grid bp-report-hero-fields bp-summary-fields-panel">
                 <div><dt>Battery family</dt><dd>@(Model.FamilyName ?? "-")</dd></div>
                 <div><dt>Battery serial number</dt><dd>@Model.Text(11)</dd></div>
@@ -111,7 +122,6 @@
                     }</dd></div>
                 }
                 <div><dt>Category</dt><dd>@Model.Text(20)</dd></div>
-                <div><dt>Passport status</dt><dd>@Model.Text(2)</dd></div>
                 <div><dt>Weight</dt><dd>@Model.Text(21)</dd></div>
                 <div><dt>Manufactured date</dt><dd>@Model.Text(17)</dd></div>
                 <div><dt>Manufactured by</dt><dd>@manufacturer</dd></div>
@@ -121,35 +131,29 @@
                     <div><dt>Signature</dt><dd>@(Model.SignatureHolds ? "Verified" : "Doesn't match the signed passport")</dd></div>
                 }
             </dl>
-        </article>
 
-        <aside class="bp-report-hero-media">
-            @if (Model.PictureHref is not null)
-            {
-                <div class="bp-image-wrap">
-                    <img src="@Model.PictureHref" alt="Picture of the @Model.FamilyName battery family" class="bp-battery-image" />
-                </div>
-            }
-            @if (detail)
-            {
-                <a class="bp-report-hero-cta" href="@PassportPages.SummaryHref(Model.Address)">&lt;- Back to summary</a>
-                @if (ViewData["ManageHref"] is string manageHref)
+            <div class="bp-hero-ctas">
+                @if (detail)
+                {
+                    <a class="bp-report-hero-cta" href="@PassportPages.SummaryHref(Model.Address)">&lt;- Back to summary</a>
+                    @if (ViewData["ManageHref"] is string manageHref)
+                    {
+                        <a class="bp-report-hero-cta" href="@manageHref">Manage this battery -&gt;</a>
+                    }
+                }
+                else if (signedIn && Model.ReaderSeesMore)
                 {
-                    <a class="bp-report-hero-cta" href="@manageHref">Manage this battery -&gt;</a>
+                    <a class="bp-report-hero-cta" href="@detailHref">View more about this passport -&gt;</a>
                 }
-            }
-            else if (signedIn && Model.ReaderSeesMore)
-            {
-                <a class="bp-report-hero-cta" href="@detailHref">View more about this passport -&gt;</a>
-            }
-            else if (signedIn)
-            {
-                <a class="bp-report-hero-cta bp-report-hero-cta-warning" href="@(signIn)&switchAccount=true">Your account sees the public data only. Sign in with another account to see more.</a>
-            }
-            else
-            {
-                <a class="bp-report-hero-cta bp-report-hero-cta-warning" href="@signIn">Sign in to see more about this passport.</a>
-            }
-        </aside>
+                else if (signedIn)
+                {
+                    <a class="bp-report-hero-cta bp-report-hero-cta-warning" href="@(signIn)&switchAccount=true">Your account sees the public data only. Sign in with another account to see more.</a>
+                }
+                else
+                {
+                    <a class="bp-report-hero-cta bp-report-hero-cta-warning" href="@signIn">Sign in to see more about this passport.</a>
+                }
+            </div>
+        </article>
     </div>
 </section>
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index 4e910f3..ed985a0 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -277,6 +277,109 @@
   font-size: 0.85rem;
 }
 
+/* The public hero (owner's item 10): the picture inside the card, top right, at its own shape and the height of the
+   top block; the facts at the card's full width; "View more" in the card. Scoped to .bp-hero-compact, so site.css and
+   every other page stay as they are. The battery pages' header picture follows the same sizes (bp-product-data.css). */
+.bp-hero-compact .bp-report-hero-layout {
+  grid-template-columns: minmax(0, 1fr);
+}
+
+.bp-hero-compact .bp-report-hero-info.bp-summary-identity-grid {
+  grid-template-columns: minmax(220px, 1fr) minmax(260px, 1.2fr) minmax(150px, 190px) auto;
+  grid-template-areas:
+    "battery ids qr picture"
+    "fields fields fields fields"
+    "cta cta cta cta";
+}
+
+.bp-hero-compact .bp-report-identity-panel--battery { grid-area: battery; }
+.bp-hero-compact .bp-report-identity-panel--identifiers { grid-area: ids; }
+.bp-hero-compact .bp-report-identity-panel--qr { grid-area: qr; }
+.bp-hero-compact .bp-hero-picture { grid-area: picture; }
+.bp-hero-compact .bp-summary-fields-panel { grid-area: fields; }
+.bp-hero-compact .bp-hero-ctas { grid-area: cta; }
+
+.bp-hero-compact .bp-hero-picture {
+  min-height: 0;
+  display: grid;
+  place-items: center;
+  padding: 10px;
+  border: 1px solid var(--bp-border);
+  border-radius: 8px;
+  background: #f3f6fa;
+}
+
+.bp-hero-compact .bp-hero-picture .bp-image-wrap {
+  width: auto;
+  height: auto;
+  border: 0;
+  background: transparent;
+  box-shadow: none;
+}
+
+.bp-hero-compact .bp-hero-picture .bp-battery-image {
+  width: auto;
+  height: 160px;
+  max-width: 220px;
+  max-height: none;
+  aspect-ratio: auto;
+  object-fit: contain;
+  filter: none;
+}
+
+.bp-hero-compact .bp-hero-ctas {
+  display: flex;
+  flex-wrap: wrap;
+  justify-content: flex-end;
+  gap: 10px;
+}
+
+.bp-hero-compact .bp-hero-ctas .bp-report-hero-cta {
+  width: auto;
+}
+
+@media (max-width: 1180px) {
+  .bp-hero-compact .bp-report-hero-info.bp-summary-identity-grid {
+    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) auto;
+    grid-template-areas:
+      "battery battery picture"
+      "ids qr qr"
+      "fields fields fields"
+      "cta cta cta";
+  }
+}
+
+@media (max-width: 760px) {
+  .bp-hero-compact .bp-report-hero-info.bp-summary-identity-grid {
+    grid-template-columns: minmax(0, 1fr) auto;
+    grid-template-areas:
+      "battery picture"
+      "ids ids"
+      "qr qr"
+      "fields fields"
+      "cta cta";
+  }
+
+  /* On a phone the picture sits small beside the battery's name, never on a row of its own. */
+  .bp-hero-compact .bp-hero-picture {
+    align-self: start;
+    padding: 6px;
+  }
+
+  .bp-hero-compact .bp-hero-picture .bp-battery-image {
+    height: 100px;
+    max-width: 120px;
+  }
+
+  .bp-hero-compact .bp-hero-ctas {
+    justify-content: stretch;
+  }
+
+  .bp-hero-compact .bp-hero-ctas .bp-report-hero-cta {
+    width: 100%;
+  }
+}
+
 /* ---- Dark ---------------------------------------------------------------------------------------------------------- */
 [data-theme="dark"] .bp-badge { background: transparent; }
 [data-theme="dark"] .bp-badge.is-original { border-color: #64748b; color: #cbd5e1; }
@@ -295,3 +398,4 @@
 [data-theme="dark"] .bp-status-help { background: #101f35; }
 [data-theme="dark"] .bp-test-tag { border-color: #b45309; background: transparent; color: #fcd34d; }
 [data-theme="dark"] .bp-test-note { color: #fcd34d; }
+[data-theme="dark"] .bp-hero-compact .bp-hero-picture { background: #e9eef5; }
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminFeedbackFollowupTests | FullyQualifiedName~PassportHeroTests"`
Expected: `Passed!  - Failed:     0, Passed:    12`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2303`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/Look/PassportHeroTests.cs web/Views/Passport/_PassportHero.cshtml web/wwwroot/css/bp-status.css
git commit -m "feat(dpp): the public hero shows the picture inside the card and the facts at full width" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2303, Skipped:     0, Total:  2303`. From the 2273 before: 2293 after Task 1, 2297 after Task 2, 2300 after Task 3, 2303 after Task 4.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 4f65caa -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

1. **Restart the app with the usual launcher** and press "Reset demo data".
2. **The Registry as the demo North admin:** the top bar's Registry entry shows 1, and "Waiting for you" stands out at the top. Each row shows a square Battery tag and a round Passport pill, and has an Open button.
3. **Search DEMO-C13M-0001 while signed out:** only the Passport pill (Active), the "Earlier passport" chip, the amber "Test passport" tag (a tap explains it) and the line "Replaces an earlier passport of this battery (since …)". No status word. The "?" explains both statuses.
4. **DEMO-C7M-0003:** a red "Invalid" pill and a red line.
5. **DEMO-C13M-0001 on the PC and the phone:** the double-stack picture sits inside the hero card, top right (beside the name on the phone). The facts run the card's full width, with "View more" under them. The 7M shows its tall single pack the same way.

## Next

Plan R4 (the side-menu battery pages), then step 5c is reworked onto the new page.
