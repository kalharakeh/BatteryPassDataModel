# DPP V3 Redesign R5: The Owner's Second Round — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The product owner tested R1–R4 and answered eight points (decision 37); a whole-page review at 1280 px found three more. This plan does them all, each task ending with a working app:

1. **The battery lists' rows end in icons** (owner: "4 icons"), like the old app's row actions: the battery page (pencil), the public page (globe) and the detail page (document) once published, and the history in the audit log (clock) for the team. Each names itself on hover. They replace R3's "Open" button on the Registry; the team's list had none. So the icons stay in view at 1280 px, the lists shorten the battery ID in the middle (full on hover), and the batch and cluster may wrap.
2. **One row per battery** (owner: "One row, newest"). A battery placed on the market again has a pack per passport with the same serial number (DEMO-C13M-0001). The team's list, the Registry (the cluster admins' list) and their search show one row per battery, the newest passport the reader may see. Clicking opens it, and its page keeps the "Earlier passport" chip; searching for an earlier passport's IDs finds the battery's row. The public lookup by serial number and the API's reads and writes by serial number already pick the passport that isn't inactive; the API's lists (`GET /api/passports`, a cluster's batteries) keep one row per battery ID.
3. **"Mark as invalid or for deletion…" opens a window** from More (owner: "open a window and also fix the font and layout"), like Change status: choose invalid or for deletion, give the reason, confirm. The separate "Passport status" section is gone: once marked, the header's red line carries the reason and the one Undo, and the message shows there. A deletion now keeps its reason too. More's menu takes the page's font (its items are buttons now), has room for its words, and its entries look like the side menu's.
4. **The status explainer says what it is.** The owner couldn't find the small "?" after the badges. It is now a calm "What do these mean?" link with its icon on both battery pages and the public hero, and a small ringed "?" in the lists' Status heading.
5. **The accident reports section is the place to see and add them.** The reports and "Add a report" come first, the readers' accident page last, as a small "How readers see it" link. On a frozen passport it says adding is off and why, and links the new passport when there is one.
6. **Key figures** (task 6; the owner chose the recommended set R of `docs/mockups/dpp-v3-key-figures.html`: "yes your recomended"). The public summary's "Summary report" card (power, the carbon pie, recycled content) becomes "Key figures": eight public values in three groups, as tiles. *Using it:* capacity and voltage (#57, #66, #64, #65), power (#67, #70, the stored watts shown in kW), expected lifetime (#82, #85, warranty #19), round-trip efficiency (#71, #72). *Environment:* the carbon footprint and its class (#30, #35) with one bar split by the four life-cycle stages instead of the pie (#31–#34), recycled content per material (#49–#52) and the renewable share (#53). *Safety:* chemistry and critical raw materials (#41, #42), safe idle temperatures and the fire extinguishing agent (#88, #89, #26). Public attributes only, read from the passport as everyone sees it, so the card is the same for every reader; nothing is worked out. The same card shows on the detail page between the hero and the data sections, where each title links down to its full entry with its date and source (owner: "yes add links"): the link opens the entry's tab and any folded part, scrolls to it and marks it. The summary's card has no links.
7. **The detail page's live values as small charts** (task 7; owner: "why is the last 7 days list very long? … can we make them a plot or graph"). One chart per live value the reader may see (the same values as before): the latest value large, the last 7 days as a line on a day axis, its low and high and how many readings, units in the title. A value with one reading says "One reading this week". Every reading stays, newest first, folded under the charts as "All readings (N)".
8. **Live values and Modules are tabs** (task 7; owner: "modules and live values should be in their each own tab as well"), after the audiences' tabs, each only for a reader it has something for. The cards under the tabs are gone; a `#tab-live` link opens its tab.
9. **What the page review found** (task 8): each action has one place, the header ("Change status" and "Add a note" were repeated on the Status section, "Publish passport" in the overview), and the label names the model and its family clearly ("Compact 7M · Compact family", not "Compact 7M of Compact"). With the managing session's answers: **the passport status (#2) shows in the badge's words** wherever a page shows it ("Active", not "active"; the passport and the API keep the schema's value), and **the cluster admin's "Hand over…" opens its window directly** from the header, like marking a passport: the window moves to page level (`_HandOverDialog`), and the Hand over section keeps the explanation and the waiting state without a second button.

**Architecture:**
- `Dpp/Pages/RowActions.cs` and `Views/Shared/_RowActions.cshtml` (task 1) draw a row's icons with the old app's `.bp-report-action` styles, which `site.css` still has (tooltips and dark mode included). `RegistryPage.Actions` gives the team its own battery page and history, a cluster admin theirs.
- `BatteryPages.ListOnePerBatteryAsync(query, mayList)` (task 2) is the pages' list: first the rows this reader may see, then `BatteryPages.OnePerBattery` drops each row another of them replaces (`BatteryRow.SuccessorOf`, from `BatteryPack.SuccessorOf`). A newer pack the reader may not see (a draft, for the Registry) leaves the one they see in place. `ListAsync` stays as it is for the API.
- `BatteriesController.Mark` (task 3) takes `what` (invalid or deletion) and `reason` from `_MarkDialog`; it replaces the two mark actions. `PassportMarks.MarkForDeletionAsync` takes an optional reason. The messages use the card name `mark`, which opens the overview.
- `_StatusHelp` (task 4) keeps its one popover per page; only the button changes, and `th .bp-status-help-*` rules make it a ringed "?" in a table heading.
- `BatteryCards.NewPassportHref` (task 5) is the new passport of an inactive one.
- `Dpp/Pages/KeyFigures.cs` (task 6) builds the tiles from the public passport (`PassportPage.KeyFigures`, set for both pages); `_KeyFigures`, `_KeyFigure` and `_KeyFigureLabel` draw them; `_PassportTabs` gives every entry the id `value-<number>`, and `wwwroot/js/bp-passport-links.js` opens a link's tab and marks the entry. Six icons join `_Icons`; the styles go to `bp-status.css`.
- `LiveTrends.Charts` (task 7) makes the detail page's charts from the reader's live history with R4's `LiveTile` and its `Line` (now on a fixed 7-day axis, with `Days`, `Low` and `High`); `_LiveHistory` draws them as inline SVG. `_PassportTabs` adds the Live values and Modules tabs (the Detail view passes it its `ViewData`).

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** the owner's round-2 answers after testing R1–R4 (2 Nov 2026, in the managing session's notes); the mock-ups `docs/mockups/dpp-v3-battery-page-redesign.html` (R1–R4) and `docs/mockups/dpp-v3-key-figures.html` (task 6); decision 37 in `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-decisions.md`.

**Learning-mode contribution spot:** `BatteryPages.OnePerBattery` (task 2), the rule for which of a battery's rows a list keeps. The tests in `OneRowPerBatteryTests` say what it must do, including along a chain of passports and while the new passport is a draft.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at eb3105c (R1–R4 and the public hero's picture trim). Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas** or `battery_pass_v3`. The tests use a throwaway MongoDB (EphemeralMongo).
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Each task runs only its own tests** (the filter in its steps); **the whole suite runs once**, in the final checks. Never run two whole suites at once.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started (by their process IDs, never by name) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2329`. After this plan: 2364. The suite takes about three minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../r5-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole; a removed file has its `git rm`.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.** The new styles go to `bp-product-data.css` (battery pages and lists) and `bp-status.css` (every page).
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## What happens to the old tests

- `RegistryBadgesTests` and `RegistryListTests` find the row's icons (`Model.Actions(row)`) instead of the Open button and its `Model.ManageHref(row)` (task 1).
- `BatteryPageLayoutTests`, `BatterySectionsTests` and `PassportMarkPagesTests` follow the Passport status section into the window and the header (task 3): the menu has seven entries, the mark posts to `/mark` with `what`, and its message comes back to the page itself, not `#passport-status`.
- `AdminFeedbackFollowupTests` finds the report card's styles in `_KeyFigures` instead of `Summary.cshtml` (task 6).
- `DppApiControllerTests.PassportsController` becomes `internal`, so `OneRowPerBatteryTests` can check that the API's lists keep both rows (task 2).
- `_PassportStatusCard.cshtml` is removed (task 3); nothing else is. The detail page's Modules and live-values cards move into tabs (task 7).
- `PassportPagesTests` reads #2 as "Active" and "Archived", and `HandOverPagesTests` finds the hand-over window in `_HandOverDialog` (task 8).

---

### Task 1: The lists' rows end in icons named on hover

The owner's item 1. Each row of the team's list and of the Registry ends in the old app's icon actions: the battery page (pencil) for whoever may change it, the public and detail pages (globe, document) once published, and the history (clock) for the team. They replace R3's Open button. The battery ID under the serial number is shortened in the middle (full on hover), and the batch and cluster may wrap, so the icons stay in view at 1280 px.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/RegistryListTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/RowActionsTests.cs`
- Modify: `web/Dpp/Pages/RegistryList.cs`
- Create: `web/Dpp/Pages/RowActions.cs`
- Modify: `web/Views/Batteries/Batteries.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`
- Create: `web/Views/Shared/_RowActions.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `r5-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs`, `BatteryPassWeb.Tests/Dpp/RegistryListTests.cs`). Save the block as `../r5-task1-tests.patch`, outside the worktree, and run `git apply ../r5-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs b/BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs
index 4622709..b7233b2 100644
--- a/BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs
@@ -57,7 +57,7 @@ public sealed class RegistryBadgesTests(MongoFixture mongo)
         Assert.True(waiting.IndexOf("value=\"true\"", StringComparison.Ordinal) < waiting.IndexOf("value=\"false\"", StringComparison.Ordinal)); // Accept first
         Assert.Contains("@if (Model.BatteryStatusShown.Contains(row.PackId) && StatusBadge.Battery(row.BatteryStatus) is { } battery)", registry, StringComparison.Ordinal);
         Assert.Contains("StatusBadge.Passport(shown.DppStatus)", registry, StringComparison.Ordinal);
-        Assert.Contains("aria-label=\"Open the battery page of @row.SerialNumber\">Open<svg", registry, StringComparison.Ordinal);
+        Assert.Contains("_RowActions", registry, StringComparison.Ordinal); // the Open button became the row's icons (R5)
         Assert.DoesNotContain(">Manage</a>", registry, StringComparison.Ordinal);
         Assert.Contains("StatusBadge.Battery(row.BatteryStatus)", View("Batteries", "Batteries.cshtml"), StringComparison.Ordinal);
     }
diff --git a/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs b/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs
index 327199d..d0ab9c7 100644
--- a/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs
@@ -53,7 +53,7 @@ public sealed class RegistryListTests
 
         Assert.Contains("/dpp/@Uri.EscapeDataString(row.Identifier)/detail", view);
         Assert.DoesNotContain("_BatteryTable", view); // the old model's table is gone from the Registry
-        Assert.Contains("Model.ManageHref(row)", view); // step 4a: the cluster admin's "Manage" link
+        Assert.Contains("Model.Actions(row)", view); // step 4a's link to the cluster admin's page, among the row's icons since R5
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Dpp/RowActionsTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, owner's item 1: the battery lists' rows end in icons named on hover, like the old app's row actions:
/// the battery page (pencil), the public and detail pages (globe, document) once published, the history (clock) for the
/// team. They replace R3's Open button.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RowActionsTests(MongoFixture mongo)
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts]));

    private async Task<(BatteryRow Row, string Key)> PublishedRowAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var row = (await BatteryTesting.Pages(rig).ListAsync(null)).Rows.Single(candidate => candidate.PackId == rig.PackId);
        return (row, Uri.EscapeDataString(row.Identifier));
    }

    [Fact]
    public async Task APublishedRow_ShouldOpenItsPublicAndDetailPages_AndTheTeamItsHistory()
    {
        var (row, key) = await PublishedRowAsync();

        var team = RowActions.For(row, BatteryPages.BatteryHref(row.PackId), team: true);
        var reader = RowActions.For(row, null, team: false);

        Assert.Equal(BatteryPages.BatteryHref(row.PackId), team.Manage);
        Assert.Equal($"/dpp/{key}", team.Public);
        Assert.Equal($"/dpp/{key}/detail", team.Detail);
        Assert.Equal($"/admin/audit?q={Uri.EscapeDataString(row.PackId)}", team.History);
        Assert.Null(reader.Manage);
        Assert.Null(reader.History); // the audit log is the team's
        Assert.NotNull(reader.Public);
    }

    [Fact]
    public async Task ADraftRow_ShouldHaveNoPublicOrDetailPage()
    {
        var (row, _) = await PublishedRowAsync();

        var draft = RowActions.For(row with { Active = null, Latest = null }, BatteryPages.BatteryHref(row.PackId), team: true);

        Assert.Null(draft.Public);
        Assert.Null(draft.Detail);
        Assert.NotNull(draft.Manage);
    }

    [Fact]
    public async Task TheRegistry_ShouldOpenTheTeamsBatteryPageForTheTeam_AndTheClusterAdminsForItsAdmin()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var list = await BatteryTesting.Pages(rig).ListAsync(null);
        var row = list.Rows.Single(candidate => candidate.PackId == rig.PackId);

        var team = RegistryList.Build(Reader.With("team@example.com", ReaderRole.Team), list).Actions(row);
        var admin = RegistryList.Build(Reader.With("north-admin@example.com", ReaderRole.ClusterAdmin, BatteryTesting.North), list,
            ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North)).Actions(row);
        var surveillance = RegistryList.Build(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), list).Actions(row);

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), team.Manage);
        Assert.NotNull(team.History);
        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), admin.Manage);
        Assert.Null(admin.History);
        Assert.Null(surveillance.Manage);
        Assert.NotNull(surveillance.Detail);
    }

    [Fact]
    public void BothLists_ShouldEndEachRowInTheIcons_NamedOnHover()
    {
        var icons = View("Shared", "_RowActions.cshtml");
        var registry = View("Registry", "Index.cshtml");
        var team = View("Batteries", "Batteries.cshtml");

        foreach (var (icon, name) in new[] { ("i-pencil", "Battery page"), ("i-globe", "Public page"), ("i-doc", "Detail page"), ("i-history", "History") })
        {
            Assert.Contains($"data-tooltip=\"{name}\"><svg class=\"bp-report-action-icon\" aria-hidden=\"true\"><use href=\"#{icon}\" />", icons, StringComparison.Ordinal);
        }

        Assert.Contains("@await Html.PartialAsync(\"_RowActions\", Model.Actions(row))", registry, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_RowActions\", RowActions.For(row, BatteryPages.BatteryHref(row.PackId), team: true))", team, StringComparison.Ordinal);
        Assert.DoesNotContain(">Open<svg", registry, StringComparison.Ordinal); // R3's Open button is gone
        // The icons stay in view at 1280 px: the battery ID shortened in the middle, the batch and cluster wrapping.
        Assert.Contains("@BatteryPages.ShortId(row.Identifier)</small>", team, StringComparison.Ordinal);
        Assert.Contains("@BatteryPages.ShortId(row.Identifier)</small>", registry, StringComparison.Ordinal);
        Assert.Contains(".bp-console-table:has(.bp-actions-column) td:is([data-label=\"Batch\"], [data-label=\"Cluster\"]) {", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")), StringComparison.Ordinal);
        Assert.Equal(2, team.Split("<span class=\"visually-hidden\">Actions</span>").Length - 1 + registry.Split("<span class=\"visually-hidden\">Actions</span>").Length - 1);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~RegistryBadgesTests | FullyQualifiedName~RegistryListTests | FullyQualifiedName~RowActionsTests"`
Expected: the build fails with CS0103, CS1061 (for example `error CS0103 The name 'RowActions' does not exist in the current context`).

- [ ] **Step 3: Write the code**

Patch `r5-task1-code.patch` (`web/Dpp/Pages/RegistryList.cs`, `web/Views/Batteries/Batteries.cshtml`, `web/Views/Registry/Index.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r5-task1-code.patch`, outside the worktree, and run `git apply ../r5-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/RegistryList.cs b/web/Dpp/Pages/RegistryList.cs
index 2d70e2a..4dcc4e0 100644
--- a/web/Dpp/Pages/RegistryList.cs
+++ b/web/Dpp/Pages/RegistryList.cs
@@ -22,6 +22,16 @@ public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query,
         ArgumentNullException.ThrowIfNull(row);
         return row.Active is not null && row.ClusterId is { } cluster && ManagedClusterIds.Contains(cluster) ? ClusterBatteryPages.BatteryHref(row.PackId) : null;
     }
+
+    /// <summary>True for the team: its rows open the team's battery page and its history (redesign R5).</summary>
+    public bool Team { get; init; }
+
+    /// <summary>A row's icon actions (redesign R5, owner's item 1): the team's or the cluster admin's battery page, the public and detail pages, and for the team the history.</summary>
+    public RowActions Actions(BatteryRow row)
+    {
+        ArgumentNullException.ThrowIfNull(row);
+        return RowActions.For(row, Team ? BatteryPages.BatteryHref(row.PackId) : ManageHref(row), Team);
+    }
 }
 
 /// <summary>
@@ -72,6 +82,7 @@ public static class RegistryList
             ShowsClusters(reader),
             Scope(reader))
         {
+            Team = reader.Roles.Contains(ReaderRole.Team),
             ManagedClusterIds = editor is { Kind: SenderKind.ClusterAdmin } ? editor.ClusterIds.ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal),
             BatteryStatusShown = batteries.Rows
                 .Where(row => AudienceRules.MaySeePurchaserData(reader, row.ClusterId is { } cluster ? [cluster] : []))
diff --git a/web/Views/Batteries/Batteries.cshtml b/web/Views/Batteries/Batteries.cshtml
index da1c1a2..2c02d97 100644
--- a/web/Views/Batteries/Batteries.cshtml
+++ b/web/Views/Batteries/Batteries.cshtml
@@ -55,6 +55,7 @@
                             <th>Batch</th>
                             <th>Cluster</th>
                             <th>Status</th>
+                            <th class="bp-actions-column"><span class="visually-hidden">Actions</span></th>
                         </tr>
                     </thead>
                     <tbody>
@@ -63,7 +64,7 @@
                             <tr>
                                 <td data-label="Serial number">
                                     <a href="@BatteryPages.BatteryHref(row.PackId)" class="bp-battery-link">@row.SerialNumber</a>
-                                    <small class="bp-battery-id" title="Battery ID (#6)">@row.Identifier</small>
+                                    <small class="bp-battery-id" title="Battery ID (#6): @row.Identifier">@BatteryPages.ShortId(row.Identifier)</small>
                                 </td>
                                 <td data-label="Model">@row.ModelName <small class="bp-batteries-muted">@row.FamilyName</small></td>
                                 <td data-label="Batch">@row.BatchLabel</td>
@@ -87,6 +88,10 @@
                                         @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(row.IsDraft ? null : row.StatusName).With((row.Active ?? row.Latest) is { } version ? $"· version {version.Number}" : null))
                                     </div>
                                 </td>
+                                <td class="bp-action-cell" data-label="Actions">
+                                    @* Icons named on hover, like the old app's row actions (redesign R5, owner's item 1). *@
+                                    @await Html.PartialAsync("_RowActions", RowActions.For(row, BatteryPages.BatteryHref(row.PackId), team: true))
+                                </td>
                             </tr>
                         }
                     </tbody>
diff --git a/web/Views/Registry/Index.cshtml b/web/Views/Registry/Index.cshtml
index 517354e..8a27862 100644
--- a/web/Views/Registry/Index.cshtml
+++ b/web/Views/Registry/Index.cshtml
@@ -71,10 +71,7 @@
                                 <th>Cluster</th>
                             }
                             <th>Status</th>
-                            @if (Model.ManagedClusterIds.Count > 0)
-                            {
-                                <th><span class="visually-hidden">Open</span></th>
-                            }
+                            <th class="bp-actions-column"><span class="visually-hidden">Actions</span></th>
                         </tr>
                     </thead>
                     <tbody>
@@ -83,7 +80,7 @@
                             <tr>
                                 <td data-label="Serial number">
                                     <a href="/dpp/@Uri.EscapeDataString(row.Identifier)/detail"><strong>@row.SerialNumber</strong></a>
-                                    <small class="d-block text-secondary">@row.Identifier</small>
+                                    <small class="d-block text-secondary" title="Battery ID (#6): @row.Identifier">@BatteryPages.ShortId(row.Identifier)</small>
                                 </td>
                                 <td data-label="Model">@row.ModelName <small class="d-block text-secondary">@row.FamilyName</small></td>
                                 <td data-label="Batch">@row.BatchLabel</td>
@@ -102,16 +99,10 @@
                                     </div>
                                     <small class="d-block text-secondary mt-1">Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>
                                 </td>
-                                @if (Model.ManagedClusterIds.Count > 0)
-                                {
-                                    <td data-label="">
-                                        @if (Model.ManageHref(row) is { } manage)
-                                        {
-                                            @* A labelled button where a small "Manage" link was (owner's point 11). *@
-                                            <a class="bp-secondary-button bp-button-compact bp-with-icon" href="@manage" aria-label="Open the battery page of @row.SerialNumber">Open<svg class="bp-i" aria-hidden="true"><use href="#i-arrow" /></svg></a>
-                                        }
-                                    </td>
-                                }
+                                <td class="bp-action-cell" data-label="Actions">
+                                    @* Icons named on hover, like the old app's row actions (redesign R5, owner's item 1). *@
+                                    @await Html.PartialAsync("_RowActions", Model.Actions(row))
+                                </td>
                             </tr>
                         }
                     </tbody>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 1f79050..86e558e 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -1685,6 +1685,11 @@ dialog.bp-dialog::backdrop {
   overflow-wrap: anywhere;
 }
 
+/* The battery lists keep their row icons in view at 1280 px (redesign R5): the batch and the cluster wrap instead. */
+.bp-console-table:has(.bp-actions-column) td:is([data-label="Batch"], [data-label="Cluster"]) {
+  white-space: normal;
+}
+
 .bp-battery-link {
   font-weight: 650;
   text-decoration: none;
```

`web/Dpp/Pages/RowActions.cs`:

```csharp
namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// A battery list row's actions as icons with their names on hover, like the old app's row actions (redesign R5, owner's
/// item 1): the battery page (pencil) for whoever may change the battery, the public and detail pages (globe, document)
/// once the passport is published, and the battery's history in the audit log (clock) for the team, whose log it is.
/// </summary>
public sealed record RowActions(string Serial, string? Manage, string? Public, string? Detail, string? History)
{
    /// <param name="manage">The battery page this reader opens, or null when they may not change the battery.</param>
    /// <param name="team">True for the team: only it reads the audit log.</param>
    public static RowActions For(BatteryRow row, string? manage, bool team)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = Uri.EscapeDataString(row.Identifier);
        var published = !row.IsDraft && row.Identifier.Length > 0;
        return new RowActions(
            row.SerialNumber,
            manage,
            published ? $"/dpp/{key}" : null,
            published ? $"/dpp/{key}/detail" : null,
            team ? $"/admin/audit?q={Uri.EscapeDataString(row.PackId)}" : null);
    }
}
```

`web/Views/Shared/_RowActions.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model RowActions
@* A battery row's actions as icons, each named on hover (redesign R5, owner's item 1; the old app's row actions). *@
<div class="bp-icon-action-row">
    @if (Model.Manage is { } manage)
    {
        <a class="bp-report-action" href="@manage" aria-label="Open the battery page of @Model.Serial" data-tooltip="Battery page"><svg class="bp-report-action-icon" aria-hidden="true"><use href="#i-pencil" /></svg></a>
    }
    @if (Model.Public is { } publicPage)
    {
        <a class="bp-report-action" href="@publicPage" aria-label="Public page of @Model.Serial" data-tooltip="Public page"><svg class="bp-report-action-icon" aria-hidden="true"><use href="#i-globe" /></svg></a>
    }
    @if (Model.Detail is { } detail)
    {
        <a class="bp-report-action" href="@detail" aria-label="Detail page of @Model.Serial" data-tooltip="Detail page"><svg class="bp-report-action-icon" aria-hidden="true"><use href="#i-doc" /></svg></a>
    }
    @if (Model.History is { } history)
    {
        <a class="bp-report-action" href="@history" aria-label="History of @Model.Serial" data-tooltip="History"><svg class="bp-report-action-icon" aria-hidden="true"><use href="#i-history" /></svg></a>
    }
</div>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~RegistryBadgesTests | FullyQualifiedName~RegistryListTests | FullyQualifiedName~RowActionsTests"`
Expected: `Passed!  - Failed:     0, Passed:    16`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs BatteryPassWeb.Tests/Dpp/RegistryListTests.cs BatteryPassWeb.Tests/Dpp/RowActionsTests.cs web/Dpp/Pages/RegistryList.cs web/Dpp/Pages/RowActions.cs web/Views/Batteries/Batteries.cshtml web/Views/Registry/Index.cshtml web/Views/Shared/_RowActions.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): the battery lists' rows end in icons named on hover" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: One row per battery, the newest passport

The owner's item 3. The team's list and the Registry list each battery once, as its newest passport the reader may see; a search for an earlier passport's IDs finds the battery's row. The API's lists keep a row per battery ID.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs`
- Modify: `web/Controllers/BatteriesController.cs`
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`

- [ ] **Step 1: Write the failing tests**

Patch `r5-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs`). Save the block as `../r5-task2-tests.patch`, outside the worktree, and run `git apply ../r5-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs b/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs
index 6262462..bbc45af 100644
--- a/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs
@@ -238,7 +238,7 @@ public sealed class DppApiControllerTests(MongoFixture mongo)
         };
     }
 
-    private static PassportsApiController PassportsController(PublicationRig rig) => new(
+    internal static PassportsApiController PassportsController(PublicationRig rig) => new(
         new DppReaders(new ClusterRepository(rig.Context)),
         BatteryTesting.Pages(rig),
         PassportApiTests.Api(rig),
```

`BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs`:

```csharp
using System.Text.Json;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, owner's item 3 ("One row, newest"): a battery placed on the market again has a pack per passport with the
/// same serial number. The team's list, the Registry and their search show one row per battery, the newest passport its
/// reader may see; the API's lists keep a row per battery ID.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class OneRowPerBatteryTests(MongoFixture mongo)
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", ..parts]));

    [Fact]
    public async Task TheTeamsList_ShouldShowTheNewPassportOnly_AndFindItByTheEarlierOnesIds()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var successorId = old.Succession!.SuccessorPackId!;
        var pages = BatteryTesting.Pages(rig);
        var every = await pages.ListAsync(null);
        var oldRow = every.Rows.Single(row => row.PackId == rig.PackId);

        var list = await pages.ListOnePerBatteryAsync(null, _ => true);
        var byOldId = await pages.ListOnePerBatteryAsync(oldRow.Identifier, _ => true);
        var byOldPassport = await pages.ListOnePerBatteryAsync(oldRow.Latest!.DigitalProductPassportId, _ => true);

        Assert.Equal(2, every.Rows.Count(row => row.SerialNumber == oldRow.SerialNumber)); // the API's list: a row per battery ID
        Assert.Equal(successorId, Assert.Single(list.Rows, row => row.SerialNumber == oldRow.SerialNumber).PackId);
        Assert.Equal(every.Total - 1, list.Total); // counts batteries, not packs
        Assert.Equal(successorId, Assert.Single(byOldId.Rows).PackId);
        Assert.Equal(successorId, Assert.Single(byOldPassport.Rows).PackId);
    }

    [Fact]
    public async Task WhileTheNewPassportIsADraft_TheRegistryShouldKeepTheActiveOne_AndTheTeamSeeTheDraft()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var (successorId, problem) = await SuccessionTests.Service(rig).CreateSuccessorAsync(PublicationTesting.Team, rig.PackId, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.OperatorId);
        Assert.Null(problem);
        var pages = BatteryTesting.Pages(rig);
        var surveillance = Reader.With("msa@example.com", ReaderRole.MarketSurveillance);

        var team = await pages.ListOnePerBatteryAsync(null, _ => true);
        var registry = await pages.ListOnePerBatteryAsync(null, row => RegistryList.MayList(surveillance, row));

        Assert.Contains(team.Rows, row => row.PackId == successorId);
        Assert.DoesNotContain(team.Rows, row => row.PackId == rig.PackId);
        Assert.Equal(rig.PackId, Assert.Single(registry.Rows).PackId); // the draft isn't the Registry's, so the active one stays
    }

    [Fact]
    public async Task TheApisLists_ShouldStillReturnEveryBatteryId()
    {
        // The manager's condition for item 3: GET /api/passports and a cluster's batteries keep a row per battery ID.
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var serial = (await BatteryTesting.Pages(rig).ListAsync(null)).Rows.Single(row => row.PackId == rig.PackId).SerialNumber;

        var listed = Assert.IsType<OkObjectResult>(await DppApiControllerTests.PassportsController(rig).List(null, CancellationToken.None));
        var passports = JsonSerializer.SerializeToElement(listed.Value).GetProperty("passports").EnumerateArray();
        var cluster = await PassportApiTests.Api(rig).BatteriesAsync(Reader.With("team@example.com", ReaderRole.Team), old.ClusterId!);

        Assert.Equal(2, passports.Count(passport => passport.GetProperty("serialNumber").GetString() == serial));
        Assert.Equal(2, cluster.Count(row => row.SerialNumber == serial));
    }

    [Fact]
    public void OnePerBattery_ShouldDropEveryRowAnotherRowReplaces_AlongAChain()
    {
        BatteryRow Row(string pack, string? successorOf) =>
            new(pack, "SN-1", $"urn:{pack}", "Compact", "Compact 7M", "2026", null, "No cluster yet", null) { SuccessorOf = successorOf };

        var rows = BatteryPages.OnePerBattery([Row("a", null), Row("b", "a"), Row("c", "b"), Row("other", null)]);

        Assert.Equal(["c", "other"], rows.Select(row => row.PackId));
    }

    [Fact]
    public void TheTeamsListAndTheRegistry_ShouldListOneRowPerBattery_AndTheApiEveryPack()
    {
        Assert.Contains("pages.ListOnePerBatteryAsync(q, _ => true, cancellationToken)", View("Controllers", "BatteriesController.cs"), StringComparison.Ordinal);
        Assert.Contains("batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), cancellationToken)", View("Controllers", "RegistryController.cs"), StringComparison.Ordinal);
        Assert.Contains("await batteries.ListAsync(q, cancellationToken)", View("Controllers", "PassportsApiController.cs"), StringComparison.Ordinal);
        Assert.Contains("await batteries.ListAsync(null, cancellationToken)", View("Dpp", "Api", "PassportApi.cs"), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DppApiControllerTests | FullyQualifiedName~OneRowPerBatteryTests"`
Expected: the build fails with CS0117, CS1061 (for example `error CS0117 'BatteryPages' does not contain a definition for 'OnePerBattery'`).

- [ ] **Step 3: Write the code**

Patch `r5-task2-code.patch` (`web/Controllers/BatteriesController.cs`, `web/Controllers/RegistryController.cs`, `web/Dpp/Pages/BatteryPages.cs`). Save the block as `../r5-task2-code.patch`, outside the worktree, and run `git apply ../r5-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/BatteriesController.cs b/web/Controllers/BatteriesController.cs
index 68518f4..32fff38 100644
--- a/web/Controllers/BatteriesController.cs
+++ b/web/Controllers/BatteriesController.cs
@@ -31,7 +31,7 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
 
     [HttpGet("")]
     public async Task<IActionResult> Batteries([FromQuery] string? q, CancellationToken cancellationToken) =>
-        View(await pages.ListAsync(q, cancellationToken));
+        View(await pages.ListOnePerBatteryAsync(q, _ => true, cancellationToken)); // one row per battery (redesign R5)
 
     [HttpGet("new")]
     public async Task<IActionResult> NewBattery([FromQuery] string? batch, CancellationToken cancellationToken) =>
diff --git a/web/Controllers/RegistryController.cs b/web/Controllers/RegistryController.cs
index 91b9ce9..73e0583 100644
--- a/web/Controllers/RegistryController.cs
+++ b/web/Controllers/RegistryController.cs
@@ -20,7 +20,9 @@ public sealed class RegistryController(DppReaders readers, BatteryPages batterie
     {
         var reader = await readers.ForAsync(User, cancellationToken);
         var editor = await readers.EditorForAsync(User, cancellationToken);
-        return View(RegistryList.Build(reader, await batteries.ListAsync(q, cancellationToken), editor) with
+        // One row per battery, the newest passport this reader may see (redesign R5, owner's item 3).
+        var list = await batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), cancellationToken);
+        return View(RegistryList.Build(reader, list, editor) with
         {
             Waiting = editor is null ? [] : await handOvers.WaitingForAsync(editor, cancellationToken),
         });
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 2d192ff..e0eb286 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -36,6 +36,9 @@ public sealed record BatteryRow(
 
     /// <summary>The battery status (#22), shown as a badge only to its readers (redesign R3); null before it has one.</summary>
     public string? BatteryStatus { get; init; }
+
+    /// <summary>The pack whose passport this one replaces (design 7.5); null for a battery placed on the market new.</summary>
+    public string? SuccessorOf { get; init; }
 }
 
 public sealed record BatteriesPage(IReadOnlyList<BatteryRow> Rows, string Query, int Total);
@@ -222,7 +225,60 @@ public sealed class BatteryPages(
     /// Every battery, newest model first by name, filtered by <paramref name="query"/>: a passport ID (#5, any version), a
     /// battery ID (#6), a serial number (#11) or a cluster's ID or name, ignoring case; part of one is enough.
     /// </summary>
+    /// <summary>Every battery pack, each with its own row: the API's lists, which name every battery ID (redesign R5 leaves them as they are).</summary>
     public async Task<BatteriesPage> ListAsync(string? query, CancellationToken cancellationToken = default)
+    {
+        var (rows, versions) = await RowsAsync(cancellationToken);
+        var wanted = query?.Trim() ?? string.Empty;
+        var shown = Sorted(rows.Where(row => wanted.Length == 0 || Matches(row, wanted, versions[row.PackId])));
+        return new BatteriesPage(shown, wanted, rows.Count);
+    }
+
+    /// <summary>
+    /// The batteries a reader's list shows, one row each (redesign R5, owner's item 3): a battery placed on the market
+    /// again has a pack per passport with the same serial number, and only the newest one the reader may see is listed.
+    /// Searching for an earlier passport's IDs finds the battery's row; its page links the earlier passport.
+    /// </summary>
+    /// <param name="mayList">The rows this reader may see (every row for the team; the Registry's rule for the others).</param>
+    public async Task<BatteriesPage> ListOnePerBatteryAsync(string? query, Func<BatteryRow, bool> mayList, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(mayList);
+        var (rows, versions) = await RowsAsync(cancellationToken);
+        var batteries = OnePerBattery(rows.Where(mayList).ToArray());
+        var byPack = rows.ToDictionary(row => row.PackId, StringComparer.Ordinal);
+        var wanted = query?.Trim() ?? string.Empty;
+        var shown = Sorted(batteries.Where(row => wanted.Length == 0 || Earlier(row, byPack).Prepend(row).Any(each => Matches(each, wanted, versions[each.PackId]))));
+        return new BatteriesPage(shown, wanted, batteries.Count);
+    }
+
+    /// <summary>
+    /// The rows left when each battery keeps only its newest pack among them: a row goes when another of the rows replaces
+    /// it. A newer pack the reader may not see (a draft, for the Registry) leaves the one they see in place.
+    /// </summary>
+    public static IReadOnlyList<BatteryRow> OnePerBattery(IReadOnlyList<BatteryRow> rows)
+    {
+        ArgumentNullException.ThrowIfNull(rows);
+        var replaced = rows.Select(row => row.SuccessorOf).OfType<string>().ToHashSet(StringComparer.Ordinal);
+        return rows.Where(row => !replaced.Contains(row.PackId)).ToArray();
+    }
+
+    /// <summary>The packs a row's pack replaces, newest first (a chain, guarded against loops).</summary>
+    private static IEnumerable<BatteryRow> Earlier(BatteryRow row, IReadOnlyDictionary<string, BatteryRow> byPack)
+    {
+        var seen = new HashSet<string>(StringComparer.Ordinal) { row.PackId };
+        for (var at = row.SuccessorOf; at is not null && seen.Add(at) && byPack.TryGetValue(at, out var earlier); at = earlier.SuccessorOf)
+        {
+            yield return earlier;
+        }
+    }
+
+    private static BatteryRow[] Sorted(IEnumerable<BatteryRow> rows) => rows
+        .OrderBy(row => row.FamilyName, StringComparer.OrdinalIgnoreCase)
+        .ThenBy(row => row.ModelName, StringComparer.OrdinalIgnoreCase)
+        .ThenBy(row => row.SerialNumber, StringComparer.OrdinalIgnoreCase)
+        .ToArray();
+
+    private async Task<(List<BatteryRow> Rows, ILookup<string, PassportVersion> Versions)> RowsAsync(CancellationToken cancellationToken)
     {
         var packs = await store.ListAsync<BatteryPack>(cancellationToken);
         var batches = (await store.ListAsync<ProductionBatch>(cancellationToken)).ToDictionary(batch => batch.Id, StringComparer.Ordinal);
@@ -253,17 +309,11 @@ public sealed class BatteryPages(
             {
                 Latest = versions[pack.Id].MaxBy(version => version.Number),
                 BatteryStatus = StatusChangeService.CurrentStatus(pack),
+                SuccessorOf = pack.SuccessorOf,
             });
         }
 
-        var wanted = query?.Trim() ?? string.Empty;
-        var shown = rows
-            .Where(row => wanted.Length == 0 || Matches(row, wanted, versions[row.PackId]))
-            .OrderBy(row => row.FamilyName, StringComparer.OrdinalIgnoreCase)
-            .ThenBy(row => row.ModelName, StringComparer.OrdinalIgnoreCase)
-            .ThenBy(row => row.SerialNumber, StringComparer.OrdinalIgnoreCase)
-            .ToArray();
-        return new BatteriesPage(shown, wanted, rows.Count);
+        return (rows, versions);
     }
 
     /// <summary>The API help's example battery: a published demo battery's ID, or none. The help is open to everyone, so it
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DppApiControllerTests | FullyQualifiedName~OneRowPerBatteryTests"`
Expected: `Passed!  - Failed:     0, Passed:    21`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs web/Controllers/BatteriesController.cs web/Controllers/RegistryController.cs web/Dpp/Pages/BatteryPages.cs
git commit -m "feat(dpp): one row per battery in the lists, the newest passport" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Marking opens a window from More

The owner's item 2. "Mark as invalid or for deletion…" opens a window at page level, like Change status: choose, give the reason, confirm. The Passport status section and its card are gone; the header's red line already has the reason and the one Undo, and the mark's message shows there. More's menu takes the page's font and has room for its words.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/MarkWindowTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`
- Modify: `web/Controllers/BatteriesController.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Dpp/Editing/PassportMarks.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/BatterySections.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Create: `web/Views/Batteries/_MarkDialog.cshtml`
- Delete: `web/Views/Batteries/_PassportStatusCard.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `r5-task3-tests.patch` (`BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs`, `BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs`, `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`). Save the block as `../r5-task3-tests.patch`, outside the worktree, and run `git apply ../r5-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs b/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs
index 95c2261..74f7cac 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs
@@ -13,7 +13,7 @@ public sealed class BatteryPageLayoutTests
     {
         foreach (var (page, ids) in new[]
         {
-            (View("Batteries", "Battery.cshtml"), new[] { "overview", "values", "status", "live", "accidents", "modules", "versions", "passport-status" }),
+            (View("Batteries", "Battery.cshtml"), new[] { "overview", "values", "status", "live", "accidents", "modules", "versions" }),
             (View("ClusterAdmin", "Battery.cshtml"), new[] { "overview", "status", "live", "accidents", "handover", "modules", "versions" }),
         })
         {
@@ -44,7 +44,7 @@ public sealed class BatteryPageLayoutTests
         var header = page[page.IndexOf("<header class=\"bp-bh\">", StringComparison.Ordinal)..page.IndexOf("</header>", StringComparison.Ordinal)];
 
         Assert.Contains("<form method=\"post\" action=\"@Model.Marks.Action/undo\" class=\"bp-status-notice\" id=\"mark-notice\">", header, StringComparison.Ordinal); // point 17
-        Assert.DoesNotContain("/undo", View("Batteries", "_PassportStatusCard.cshtml"), StringComparison.Ordinal); // one Undo, in the header
+        Assert.Single(page.Split("/undo\"").Skip(1)); // one Undo, in the header (the Passport status section is gone, R5)
         Assert.Contains("@await Html.PartialAsync(\"_TestTag\", \"Test ID\")", header, StringComparison.Ordinal); // point 7: a tag that explains itself on a tap
         Assert.DoesNotContain("Test identifiers: this battery's IDs start with TEST-", page, StringComparison.Ordinal);
         Assert.Contains(">Earlier passport</a>", header, StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs b/BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs
index 6e1e3fd..1c75213 100644
--- a/BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs
@@ -24,6 +24,7 @@ public sealed class BatterySectionsTests(MongoFixture mongo)
     [InlineData(null, "publication", false, "overview")] // publishing, the cluster and removing live in the overview
     [InlineData(null, "cluster", false, "overview")]
     [InlineData(null, "passport-status", false, "overview")] // not on this menu
+    [InlineData(null, "mark", false, "overview")] // marking says so in the header (R5)
     [InlineData(null, null, true, "values")] // the values editor was just saved
     [InlineData("status", "live", true, "status")] // asking wins
     [InlineData(null, null, false, "overview")]
@@ -41,23 +42,24 @@ public sealed class BatterySectionsTests(MongoFixture mongo)
         var published = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Sections;
         var unpublished = (await BatteryTesting.Pages(draft).BatteryAsync(draft.PackId))!.Sections;
 
-        Assert.Equal(["overview", "values", "status", "live", "accidents", "modules", "versions", "passport-status"], published.Select(section => section.Id));
+        Assert.Equal(["overview", "values", "status", "live", "accidents", "modules", "versions"], published.Select(section => section.Id)); // no Passport status section (R5)
         Assert.Equal("Nothing needs you", published[0].Hint);
         Assert.Equal("Original", published.Single(section => section.Id == "status").Hint);
         Assert.Equal(1, published.Single(section => section.Id == "versions").Count);
-        Assert.Equal(["overview", "values", "status", "live", "accidents", "modules", "versions"], unpublished.Select(section => section.Id)); // no passport status before publishing
+        Assert.Equal(["overview", "values", "status", "live", "accidents", "modules", "versions"], unpublished.Select(section => section.Id));
         Assert.EndsWith("to do", unpublished[0].Hint, StringComparison.Ordinal);
     }
 
     [Fact]
-    public async Task AnInvalidPassport_ShouldSaySoInTheMenu()
+    public async Task AnInvalidPassport_ShouldSaySoInTheHeader_NotInASection()
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
         await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("msa@example.com"), rig.PackId, "Demo: wrong carbon footprint figures");
 
         var sections = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Sections;
 
-        Assert.Equal("Invalid", sections.Single(section => section.Id == "passport-status").Hint);
+        Assert.DoesNotContain(sections, section => section.Id == "passport-status"); // the header's red line says it (R5)
+        Assert.True((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks.Marked);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
index cb791f5..8054cb4 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
@@ -18,17 +18,17 @@ public sealed class PassportMarkPagesTests(MongoFixture mongo)
     private const string Reason = "Demo: wrong carbon footprint figures";
 
     [Fact]
-    public async Task TheTeamsPage_ShouldMarkInvalidAndUndoItFromThePassportStatusCard()
+    public async Task TheTeamsPage_ShouldMarkInvalidFromTheWindowAndUndoItInTheHeader()
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
         var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());
 
         Assert.True((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks.Active);
-        var marked = Assert.IsType<RedirectResult>(await controller.MarkInvalid(rig.PackId, Reason, CancellationToken.None));
+        var marked = Assert.IsType<RedirectResult>(await controller.Mark(rig.PackId, "invalid", Reason, CancellationToken.None));
         var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks;
         await controller.UndoMark(rig.PackId, CancellationToken.None);
 
-        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#passport-status", marked.Url);
+        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), marked.Url); // the message shows in the header (R5)
         Assert.True(card.Marked);
         Assert.Equal(DppStatuses.Invalid, card.Status);
         Assert.Equal(Reason, card.Mark!.Reason);
@@ -42,7 +42,7 @@ public sealed class PassportMarkPagesTests(MongoFixture mongo)
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
         var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());
 
-        await controller.MarkForDeletion(rig.PackId, CancellationToken.None);
+        await controller.Mark(rig.PackId, "deletion", "Recycled", CancellationToken.None);
 
         Assert.Equal($"Nothing changed: {PassportMarks.OnlyWaste}.", controller.TempData[BatteriesController.CardErrorKey]);
         Assert.False((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks.Waste);
@@ -144,7 +144,7 @@ public sealed class PassportMarkPagesTests(MongoFixture mongo)
         var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));
 
-        Assert.Contains("~/Views/Batteries/_PassportStatusCard.cshtml\", marks", battery, StringComparison.Ordinal);
+        Assert.Contains("~/Views/Batteries/_MarkDialog.cshtml\", markable", battery, StringComparison.Ordinal); // a window since R5
         Assert.Contains("Model.MayMarkInvalid", hero, StringComparison.Ordinal);
         Assert.Contains("Model.MarkReason is { } reason", hero, StringComparison.Ordinal);
         Assert.Contains("StatusBadge.Passport(shown.DppStatus)", registry, StringComparison.Ordinal); // a badge since redesign R3
```

`BatteryPassWeb.Tests/Dpp/MarkWindowTests.cs`:

```csharp
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, owner's item 2: "Mark as invalid or for deletion…" under More opens a window at page level, like Change
/// status (choose, give the reason, confirm); the separate Passport status section is gone, and More's menu has the
/// page's font and room for its words.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkWindowTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task AWasteBattery_ShouldBeMarkedForDeletionWithItsReason()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.Null((await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)))).Problem);
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        var marked = Assert.IsType<RedirectResult>(await controller.Mark(rig.PackId, "deletion", "  Recycled at the north plant  ", CancellationToken.None));
        var view = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks;

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), marked.Url);
        Assert.Equal(DppStatuses.MarkedForDeletion, view.Status);
        Assert.Equal("Recycled at the north plant", view.Mark!.Reason);
        Assert.Equal("The passport is marked for deletion. It stays readable until it is deleted.", controller.TempData[BatteriesController.CardStatusKey]);
    }

    [Fact]
    public async Task AnInvalidMarkWithoutAReason_ShouldChangeNothingAndSaySoInTheHeader()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        await controller.Mark(rig.PackId, "invalid", " ", CancellationToken.None);

        Assert.Equal($"Nothing changed: {PassportMarks.NeedsReason}.", controller.TempData[BatteriesController.CardErrorKey]);
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public void More_ShouldOpenTheMarkWindow_WhichAsksWhatAndWhy()
    {
        var page = Read("Views", "Batteries", "Battery.cshtml");
        var header = page[page.IndexOf("<header class=\"bp-bh\">", StringComparison.Ordinal)..page.IndexOf("</header>", StringComparison.Ordinal)];
        var window = Read("Views", "Batteries", "_MarkDialog.cshtml");

        Assert.Contains("<button type=\"button\" class=\"is-danger\" data-open-dialog=\"mark-dialog\">", header, StringComparison.Ordinal);
        Assert.Contains("@{ await CardMessagesAsync(\"mark\"); }", header, StringComparison.Ordinal);
        Assert.DoesNotContain("passport-status", page, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"))!, "_PassportStatusCard.cshtml")));
        Assert.Contains("<dialog id=\"mark-dialog\" class=\"bp-modal-panel bp-dialog\"", window, StringComparison.Ordinal);
        Assert.Contains("<form method=\"post\" action=\"@Model.Action\" class=\"bp-dialog-form\">", window, StringComparison.Ordinal);
        Assert.Contains("name=\"what\" value=\"invalid\" checked", window, StringComparison.Ordinal);
        Assert.Contains("name=\"what\" value=\"deletion\" disabled=\"@(!Model.Waste)\"", window, StringComparison.Ordinal);
        Assert.Contains("name=\"reason\"", window, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-console-form-card bp-dialog-host\">", window, StringComparison.Ordinal); // the radios keep the card's look
    }

    [Fact]
    public void MoresMenu_ShouldUseThePagesFontAndRoomForItsWords()
    {
        var css = Read("wwwroot", "css", "bp-product-data.css");

        Assert.Contains(".bp-more-list a,\n.bp-more-list button {", css, StringComparison.Ordinal);
        Assert.Contains("  font: inherit;\n  font-size: 0.92rem;\n  font-weight: 600;", css, StringComparison.Ordinal);
        Assert.Contains("  width: max-content;\n  min-width: 300px;", css, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPageLayoutTests | FullyQualifiedName~BatterySectionsTests | FullyQualifiedName~MarkWindowTests | FullyQualifiedName~PassportMarkPagesTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'BatteriesController' does not contain a definition for 'Mark'`).

- [ ] **Step 3: Write the code**

Delete `web/Views/Batteries/_PassportStatusCard.cshtml` (nothing uses it any more):

```bash
git rm -q web/Views/Batteries/_PassportStatusCard.cshtml
```

Patch `r5-task3-code.patch` (`web/Controllers/BatteriesController.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/Editing/PassportMarks.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/BatterySections.cs`, `web/Views/Batteries/Battery.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r5-task3-code.patch`, outside the worktree, and run `git apply ../r5-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/BatteriesController.cs b/web/Controllers/BatteriesController.cs
index 32fff38..76169bd 100644
--- a/web/Controllers/BatteriesController.cs
+++ b/web/Controllers/BatteriesController.cs
@@ -220,17 +220,16 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
         return Redirect($"{BatteryPages.BatteryHref(packId)}#status");
     }
 
-    /// <summary>The team marks the passport invalid, with a reason (design 7.7).</summary>
-    [HttpPost("{packId}/mark/invalid")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> MarkInvalid(string packId, [FromForm] string? reason, CancellationToken cancellationToken) =>
-        MarkResult(packId, await marks.MarkInvalidAsync(ChangeSender.Team(Who), packId, reason, cancellationToken), "The passport is marked invalid. Every page says so; only the team undoes it.");
-
-    /// <summary>The team marks a waste battery's passport for deletion (design 7.6).</summary>
-    [HttpPost("{packId}/mark/deletion")]
+    /// <summary>
+    /// The team marks the passport invalid (design 7.7) or, for a waste battery, for deletion (design 7.6), with a reason,
+    /// from the "Mark…" window that "More" opens (redesign R5, owner's item 2).
+    /// </summary>
+    [HttpPost("{packId}/mark")]
     [ValidateAntiForgeryToken]
-    public async Task<IActionResult> MarkForDeletion(string packId, CancellationToken cancellationToken) =>
-        MarkResult(packId, await marks.MarkForDeletionAsync(ChangeSender.Team(Who), packId, cancellationToken), "The passport is marked for deletion. It stays readable until it is deleted.");
+    public async Task<IActionResult> Mark(string packId, [FromForm] string? what, [FromForm] string? reason, CancellationToken cancellationToken) =>
+        what == "deletion"
+            ? MarkResult(packId, await marks.MarkForDeletionAsync(ChangeSender.Team(Who), packId, reason, cancellationToken), "The passport is marked for deletion. It stays readable until it is deleted.")
+            : MarkResult(packId, await marks.MarkInvalidAsync(ChangeSender.Team(Who), packId, reason, cancellationToken), "The passport is marked invalid. Every page says so; only the team undoes it.");
 
     /// <summary>The team undoes "invalid" or "marked for deletion" (J-8).</summary>
     [HttpPost("{packId}/mark/undo")]
@@ -240,8 +239,9 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
 
     private RedirectResult MarkResult(string packId, string? problem, string done)
     {
-        Card("passport-status", problem is null ? done : null, problem is null ? null : $"Nothing changed: {problem}.");
-        return Redirect($"{BatteryPages.BatteryHref(packId)}#passport-status");
+        // The message shows in the header, by the red line that says what the passport is (redesign R5).
+        Card("mark", problem is null ? done : null, problem is null ? null : $"Nothing changed: {problem}.");
+        return Redirect(BatteryPages.BatteryHref(packId));
     }
 
     [HttpPost("{packId}/remove")]
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index cf56ea1..0d6f776 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -586,7 +586,7 @@ public sealed class DemoDataService(
         }
         else if (mark.Deletion)
         {
-            problem = await marks.MarkForDeletionAsync(team, packId, cancellationToken);
+            problem = await marks.MarkForDeletionAsync(team, packId, cancellationToken: cancellationToken);
         }
 
         if (problem is not null)
diff --git a/web/Dpp/Editing/PassportMarks.cs b/web/Dpp/Editing/PassportMarks.cs
index 91f23f7..c47fe1d 100644
--- a/web/Dpp/Editing/PassportMarks.cs
+++ b/web/Dpp/Editing/PassportMarks.cs
@@ -45,7 +45,8 @@ public sealed class PassportMarks(DppStore store, ApplicationAuditService audit,
         return await MarkAsync(sender, packId, DppStatuses.Invalid, why.Length > MaxReason ? why[..MaxReason] : why, cancellationToken);
     }
 
-    public async Task<string?> MarkForDeletionAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
+    /// <param name="reason">Why, as the team gives it in the "Mark…" window (redesign R5); optional.</param>
+    public async Task<string?> MarkForDeletionAsync(ChangeSender sender, string packId, string? reason = null, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(sender);
         if (sender.Kind != SenderKind.Team)
@@ -59,7 +60,8 @@ public sealed class PassportMarks(DppStore store, ApplicationAuditService audit,
             return OnlyWaste;
         }
 
-        return await MarkAsync(sender, packId, DppStatuses.MarkedForDeletion, null, cancellationToken);
+        var why = reason?.Trim() ?? string.Empty;
+        return await MarkAsync(sender, packId, DppStatuses.MarkedForDeletion, why.Length == 0 ? null : why.Length > MaxReason ? why[..MaxReason] : why, cancellationToken);
     }
 
     /// <summary>The team puts an invalid or marked-for-deletion passport back to active (J-8).</summary>
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index e0eb286..3e789f2 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -172,11 +172,9 @@ public sealed record BatteryPage(
             sections.Add(new("accidents", "Accident reports", "accident", Count: AccidentReports.Count));
             sections.Add(new("modules", "Modules", "modules", Count: Pack.Modules.Count));
             sections.Add(new("versions", "Versions", "versions", Count: Versions.Count));
-            if (Marks.Active || Marks.Marked)
-            {
-                sections.Add(new("passport-status", "Passport status", "shield", Marks.Marked ? DppStatuses.Words(Marks.Status!) : "Mark invalid or for deletion"));
-            }
 
+            // No "Passport status" section (redesign R5, owner's item 2): marking opens a window from "More", and a marked
+            // passport's reason and Undo are on the header's red line.
             return sections;
         }
     }
diff --git a/web/Dpp/Pages/BatterySections.cs b/web/Dpp/Pages/BatterySections.cs
index 666b2df..079e6a4 100644
--- a/web/Dpp/Pages/BatterySections.cs
+++ b/web/Dpp/Pages/BatterySections.cs
@@ -44,7 +44,7 @@ public static class BatterySections
     /// <summary>The section a card's messages belong to: the publication, cluster and removal cards live in the overview.</summary>
     public static string Of(string card) => card switch
     {
-        "publication" or "remove" or "cluster" => Overview,
+        "publication" or "remove" or "cluster" or "mark" => Overview,
         "module-list" => "modules",
         _ => card,
     };
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 92bbe4f..629aef7 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -139,6 +139,7 @@
                 <button type="submit" class="bp-secondary-button bp-button-compact">Undo: make it active again</button>
             </form>
         }
+        @{ await CardMessagesAsync("mark"); }
         @if (Model.HandOverTo is { } receiver)
         {
             <form method="post" action="@href/handover/cancel" class="bp-status-notice is-quiet">
@@ -169,14 +170,15 @@
                     }
                 </div>
             }
-            @if ((sections.Any(section => section.Id == "passport-status") && !Model.Marks.Marked) || !publication.IsPublished)
+            @if (Model.Marks is { Active: true, Marked: false } || !publication.IsPublished)
             {
                 <details class="bp-more">
                     <summary class="bp-secondary-button bp-with-icon"><svg class="bp-i" aria-hidden="true"><use href="#i-more" /></svg>More</summary>
                     <div class="bp-more-list">
-                        @if (sections.Any(section => section.Id == "passport-status") && !Model.Marks.Marked)
+                        @if (Model.Marks is { Active: true, Marked: false })
                         {
-                            <a href="@href?section=passport-status" class="is-danger"><svg class="bp-i" aria-hidden="true"><use href="#i-alert" /></svg><span>Mark as invalid or for deletion…<small>Freezes the passport; every page says so</small></span></a>
+                            @* Opens a window, like Change status (redesign R5, owner's item 2). *@
+                            <button type="button" class="is-danger" data-open-dialog="mark-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-alert" /></svg><span>Mark as invalid or for deletion…<small>Freezes the passport; every page says so</small></span></button>
                         }
                         @if (!publication.IsPublished)
                         {
@@ -364,12 +366,6 @@
             <div data-section="versions" hidden="@Hidden("versions")">
                 @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
             </div>
-            @if (Model.Marks is { } marks && (marks.Active || marks.Marked))
-            {
-                <div data-section="passport-status" hidden="@Hidden("passport-status")">
-                    @await Html.PartialAsync("~/Views/Batteries/_PassportStatusCard.cshtml", marks)
-                </div>
-            }
         </div>
     </div>
 
@@ -418,6 +414,11 @@
     {
         @await Html.PartialAsync("~/Views/Batteries/_StatusDialog.cshtml", statusWindows)
     }
+
+    @if (Model.Marks is { Active: true, Marked: false } markable)
+    {
+        @await Html.PartialAsync("~/Views/Batteries/_MarkDialog.cshtml", markable)
+    }
 </main>
 
 @section Scripts {
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 86e558e..471fd85 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3099,7 +3099,9 @@ main .bp-field-marker.is-published {
   left: 0;
   z-index: 5;
   display: grid;
-  min-width: 270px;
+  width: max-content;
+  min-width: 300px;
+  max-width: min(380px, calc(100vw - 28px));
   margin-top: 6px;
   padding: 6px;
   border: 1px solid var(--bp-border);
@@ -3108,17 +3110,34 @@ main .bp-field-marker.is-published {
   box-shadow: 0 10px 28px rgba(10, 40, 80, 0.14);
 }
 
-.bp-more-list a {
+/* Links and buttons alike, in the page's font (a button otherwise takes the browser's) and sized like the side menu's
+   entries: the name, and a quieter line under it (redesign R5, owner's item 2). */
+.bp-more-list a,
+.bp-more-list button {
   display: flex;
-  align-items: center;
+  align-items: flex-start;
   gap: 10px;
+  width: 100%;
   padding: 9px 10px;
+  border: 0;
   border-radius: 8px;
+  background: none;
   color: var(--bp-text);
+  font: inherit;
+  font-size: 0.92rem;
+  font-weight: 600;
+  line-height: 1.3;
+  text-align: left;
   text-decoration: none;
+  cursor: pointer;
 }
 
-.bp-more-list a:hover {
+.bp-more-list .bp-i {
+  margin-top: 1px;
+}
+
+.bp-more-list a:hover,
+.bp-more-list button:hover {
   background: rgba(42, 110, 207, 0.08);
 }
 
@@ -3128,8 +3147,11 @@ main .bp-field-marker.is-published {
 
 .bp-more-list small {
   display: block;
+  margin-top: 2px;
   color: var(--bp-muted);
-  font-size: 0.76rem;
+  font-size: 0.78rem;
+  font-weight: 400;
+  line-height: 1.35;
 }
 
 /* The windows at page level keep the card's form styles; the host itself draws nothing. */
@@ -3423,7 +3445,9 @@ main .bp-field-marker.is-published {
   .bp-more-list {
     left: 14px;
     right: 14px;
+    width: auto;
     min-width: 0;
+    max-width: none;
   }
 
   /* A section picker at the top instead of the stacked menu (owner's improvement 7). */
```

`web/Views/Batteries/_MarkDialog.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Editing
@using BatteryPassWeb.Dpp.Pages
@model PassportMarkView
@* "Mark as invalid or for deletion…" from the header's "More" (redesign R5, owner's item 2): a window at page level, like
   Change status. Choose, give the reason, confirm. Once marked, the header's red line carries the reason and the one Undo. *@
<div class="bp-console-form-card bp-dialog-host">
    <dialog id="mark-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="mark-dialog-title">
        <form method="post" action="@Model.Action" class="bp-dialog-form">
            @Html.AntiForgeryToken()
            <h2 id="mark-dialog-title">Mark the passport</h2>
            <p class="bp-help-text">Every page then says so, and nothing on the passport changes until the team undoes it.</p>
            <div class="bp-choice-list">
                <label class="bp-choice"><input type="radio" name="what" value="invalid" checked /><strong>Invalid</strong><small>For example after a market surveillance finding.</small></label>
                <label class="bp-choice"><input type="radio" name="what" value="deletion" disabled="@(!Model.Waste)" /><strong>For deletion</strong><small>@(Model.Waste ? "After recycling or export. The passport stays readable until it is deleted." : "Only for a battery whose status is waste.")</small></label>
            </div>
            <label><span>Reason (seen only by your team and market surveillance)</span><input type="text" name="reason" maxlength="@PassportMarks.MaxReason" placeholder="e.g. Declared values don't match the test report" required /></label>
            <div class="bp-dialog-actions">
                <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
                <button type="submit" class="bp-danger-button">Mark the passport</button>
            </div>
        </form>
    </dialog>
</div>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPageLayoutTests | FullyQualifiedName~BatterySectionsTests | FullyQualifiedName~MarkWindowTests | FullyQualifiedName~PassportMarkPagesTests"`
Expected: `Passed!  - Failed:     0, Passed:    35`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs BatteryPassWeb.Tests/Dpp/MarkWindowTests.cs BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs web/Controllers/BatteriesController.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/Editing/PassportMarks.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/BatterySections.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_MarkDialog.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): marking a passport opens a window from More" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The status explainer says what it is

Item 5. The "?" after the badges becomes a calm "What do these mean?" link with its icon, on both battery pages and the public hero. The lists' Status heading gets the same explainer as a small ringed "?".

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs`
- Modify: `web/Views/Batteries/Batteries.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `web/Views/Shared/_StatusHelp.cshtml`
- Modify: `web/wwwroot/css/bp-status.css`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs`:

```csharp
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
        foreach (var view in new[] { Read("web", "Views", "Batteries", "Batteries.cshtml"), Read("web", "Views", "Registry", "Index.cshtml") })
        {
            Assert.Contains("<th>Status @await Html.PartialAsync(\"_StatusHelp\")</th>", view, StringComparison.Ordinal);
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusHelpLinkTests"`
Expected: `Failed:     2, Passed:     0` (failing: `StatusHelpLinkTests.BothLists_ShouldExplainTheStatusesInTheirHeading`, `StatusHelpLinkTests.TheExplainer_ShouldBeOpenedByALinkThatSaysWhatItIs`).

- [ ] **Step 3: Write the code**

Patch `r5-task4-code.patch` (`web/Views/Batteries/Batteries.cshtml`, `web/Views/Registry/Index.cshtml`, `web/Views/Shared/_StatusHelp.cshtml`, `web/wwwroot/css/bp-status.css`). Save the block as `../r5-task4-code.patch`, outside the worktree, and run `git apply ../r5-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Batteries/Batteries.cshtml b/web/Views/Batteries/Batteries.cshtml
index 2c02d97..6f028c7 100644
--- a/web/Views/Batteries/Batteries.cshtml
+++ b/web/Views/Batteries/Batteries.cshtml
@@ -54,7 +54,7 @@
                             <th>Model</th>
                             <th>Batch</th>
                             <th>Cluster</th>
-                            <th>Status</th>
+                            <th>Status @await Html.PartialAsync("_StatusHelp")</th>
                             <th class="bp-actions-column"><span class="visually-hidden">Actions</span></th>
                         </tr>
                     </thead>
diff --git a/web/Views/Registry/Index.cshtml b/web/Views/Registry/Index.cshtml
index 8a27862..336dab7 100644
--- a/web/Views/Registry/Index.cshtml
+++ b/web/Views/Registry/Index.cshtml
@@ -70,7 +70,7 @@
                             {
                                 <th>Cluster</th>
                             }
-                            <th>Status</th>
+                            <th>Status @await Html.PartialAsync("_StatusHelp")</th>
                             <th class="bp-actions-column"><span class="visually-hidden">Actions</span></th>
                         </tr>
                     </thead>
diff --git a/web/Views/Shared/_StatusHelp.cshtml b/web/Views/Shared/_StatusHelp.cshtml
index 71b67b6..87b0d8d 100644
--- a/web/Views/Shared/_StatusHelp.cshtml
+++ b/web/Views/Shared/_StatusHelp.cshtml
@@ -1,8 +1,9 @@
 @using BatteryPassWeb.Dpp.Pages
 @using BatteryPassWeb.Dpp.Store
-@* "What do the two statuses mean?" (redesign R3, owner's point 9): the ? button beside the badges opens this explainer.
+@* "What do the two statuses mean?" (redesign R3, owner's point 9): the link beside the badges opens this explainer; it
+   says so in words, since a bare "?" went unseen (R5, item 5). In a list's Status heading it is a small ringed "?".
    A popover needs no script; the explainer is written once per page, however many buttons point at it. *@
-<button type="button" class="bp-status-help-button" popovertarget="status-help" aria-label="What do the two statuses mean?" title="What do the two statuses mean?"><svg class="bp-i" aria-hidden="true"><use href="#i-help" /></svg></button>
+<button type="button" class="bp-status-help-button" popovertarget="status-help" aria-label="What do the two statuses mean?"><svg class="bp-i" aria-hidden="true"><use href="#i-help" /></svg><span class="bp-status-help-words">What do these mean?</span></button>
 @if (!Context.Items.ContainsKey("status-help"))
 {
     Context.Items["status-help"] = true;
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index a33eb67..1cf7d5e 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -84,22 +84,63 @@
 }
 
 /* "What do the two statuses mean?": a small round button opening the explainer (a popover, no script needed). */
+/* "What do these mean?": a calm text link with its icon beside the badges, easy to spot (redesign R5, item 5). */
 .bp-status-help-button {
-  display: inline-grid;
-  place-items: center;
-  width: 30px;
-  height: 30px;
-  padding: 0;
+  display: inline-flex;
+  align-items: center;
+  gap: 5px;
+  min-height: 30px;
+  padding: 2px 9px 2px 5px;
   border: 0;
-  border-radius: 50%;
+  border-radius: 999px;
   background: none;
-  color: var(--bp-muted);
+  color: var(--bp-primary);
+  font: inherit;
+  font-size: 0.82rem;
+  font-weight: 600;
+  text-decoration: underline;
+  text-decoration-color: rgba(42, 110, 207, 0.35);
+  text-underline-offset: 3px;
+  cursor: pointer;
+}
+
+.bp-status-help-button .bp-i {
+  width: 17px;
+  height: 17px;
 }
 
 .bp-status-help-button:hover,
 .bp-status-help-button:focus-visible {
   background: rgba(42, 110, 207, 0.08);
-  color: var(--bp-primary);
+  text-decoration-color: currentColor;
+}
+
+/* In a list's Status heading: the "?" alone, in a small ring; its words stay for screen readers. */
+th .bp-status-help-button {
+  justify-content: center;
+  width: 22px;
+  height: 22px;
+  min-height: 0;
+  margin-left: 4px;
+  padding: 0;
+  border: 1px solid var(--bp-border);
+  color: var(--bp-muted);
+  text-decoration: none;
+  vertical-align: -5px;
+}
+
+th .bp-status-help-button .bp-i {
+  width: 14px;
+  height: 14px;
+}
+
+th .bp-status-help-words {
+  position: absolute;
+  width: 1px;
+  height: 1px;
+  overflow: hidden;
+  clip-path: inset(50%);
+  white-space: nowrap;
 }
 
 .bp-status-help {
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusHelpLinkTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs web/Views/Batteries/Batteries.cshtml web/Views/Registry/Index.cshtml web/Views/Shared/_StatusHelp.cshtml web/wwwroot/css/bp-status.css
git commit -m "feat(dpp): the status explainer says what it is, also in the lists" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The accident reports section: see and add them here

Item 6. The section reads as the place to see and add reports: the list and "Add a report" first, the readers' accident page last as a small "How readers see it" link. A frozen passport says adding is off and why, and links the new passport when there is one.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs`
- Modify: `web/Dpp/Pages/BatteryCards.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Views/Batteries/_AccidentsCard.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, item 6: the accident reports section is the place to see and add them: the list and the upload first,
/// the readers' accident page as a small "How readers see it" link last; a frozen passport says why adding is off and
/// points to the new passport.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class AccidentSectionTests(MongoFixture mongo)
{
    private static string Card() => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_AccidentsCard.cshtml")).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task AnInactivePassport_ShouldPointToTheNewPassport()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var successorId = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;

        var old = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Cards;
        var current = (await BatteryTesting.Pages(rig).BatteryAsync(successorId))!.Cards;

        Assert.True(old.Frozen);
        Assert.Equal(BatteryPassWeb.Dpp.Pages.BatteryPages.BatteryHref(successorId), old.NewPassportHref);
        Assert.False(current.Frozen);
        Assert.Null(current.NewPassportHref);
    }

    [Fact]
    public void TheSection_ShouldPutTheReportsAndTheUploadFirst_AndTheReadersPageLast()
    {
        var card = Card();
        var list = card.IndexOf("<ul class=\"bp-file-checks\">", StringComparison.Ordinal);
        var upload = card.IndexOf("<h3 class=\"bp-subheading\">Add a report</h3>", StringComparison.Ordinal);
        var readers = card.IndexOf("How readers see it</a>", StringComparison.Ordinal);

        Assert.Contains("See and add this battery's accident reports here.", card, StringComparison.Ordinal);
        Assert.True(list > 0 && upload > list && readers > upload);
        Assert.Contains("<p class=\"bp-accident-readers\"><a href=\"@ReaderPages.AccidentsHref(Model.Pack.Id)\" target=\"_blank\" rel=\"noreferrer\">", card, StringComparison.Ordinal);
    }

    [Fact]
    public void AFrozenPassport_ShouldSayWhyAddingIsOff_AndLinkTheNewPassport()
    {
        var card = Card();

        Assert.Contains("<strong>Adding reports is off on this passport.</strong> @frozen", card, StringComparison.Ordinal);
        Assert.Contains("@if (Model.NewPassportHref is { } next)", card, StringComparison.Ordinal);
        Assert.Contains("<a href=\"@next\">Add them on the new passport</a>", card, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AccidentSectionTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'BatteryCards' does not contain a definition for 'NewPassportHref'`).

- [ ] **Step 3: Write the code**

Patch `r5-task5-code.patch` (`web/Dpp/Pages/BatteryCards.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Views/Batteries/_AccidentsCard.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r5-task5-code.patch`, outside the worktree, and run `git apply ../r5-task5-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/BatteryCards.cs b/web/Dpp/Pages/BatteryCards.cs
index fe35eb4..aafc5f3 100644
--- a/web/Dpp/Pages/BatteryCards.cs
+++ b/web/Dpp/Pages/BatteryCards.cs
@@ -12,6 +12,7 @@ namespace BatteryPassWeb.Dpp.Pages;
 /// <param name="ModuleDescriptionHref">Where the team types the model's module description; null on the cluster admin's page.</param>
 /// <param name="FrozenAs">The DPP status (#2) of a passport that is no longer active (inactive, invalid or marked for
 /// deletion); null while it is active or a draft.</param>
+/// <param name="NewPassportHref">An inactive passport's new passport, where the battery's reports go now (redesign R5); null otherwise.</param>
 public sealed record BatteryCards(
     BatteryPack Pack,
     string Href,
@@ -22,7 +23,8 @@ public sealed record BatteryCards(
     bool ForTeam,
     string? ModuleDescriptionHref = null,
     bool HasModuleDescription = false,
-    string? FrozenAs = null)
+    string? FrozenAs = null,
+    string? NewPassportHref = null)
 {
     /// <summary>Whether nothing on the passport changes any more (step 4c).</summary>
     public bool Frozen => FrozenAs is not null;
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 3e789f2..577eab4 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -142,7 +142,8 @@ public sealed record BatteryPage(
         ForTeam: true,
         $"{ProductDataPages.FamilyHref(Sources.Model.FamilyId, Sources.Model.Id)}#modules",
         !string.IsNullOrWhiteSpace(Sources.Model.ModuleDescription),
-        FrozenAs: Publication.NoLongerActive ? Publication.Latest!.DppStatus : null);
+        FrozenAs: Publication.NoLongerActive ? Publication.Latest!.DppStatus : null,
+        NewPassportHref: Status?.Succession is { Linked: true } next ? next.SuccessorHref ?? next.OtherPassport : null);
 
     /// <summary>The passport values editor as the page shows it: read-only, with the reason, once nothing changes (redesign R1).</summary>
     public RecordEditorView Editor => Cards.FrozenNote is { } note ? Values with { FrozenNote = note } : Values;
diff --git a/web/Views/Batteries/_AccidentsCard.cshtml b/web/Views/Batteries/_AccidentsCard.cshtml
index f36afb0..7ed27d4 100644
--- a/web/Views/Batteries/_AccidentsCard.cshtml
+++ b/web/Views/Batteries/_AccidentsCard.cshtml
@@ -1,11 +1,12 @@
 @using BatteryPassWeb.Dpp.Pages
 @inject Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> AppOptions
 @model BatteryCards
-@* The accident reports card (#92): on the team's battery page and on the cluster admin's (step 4a). *@
+@* The accident reports card (#92): on the team's battery page and on the cluster admin's (step 4a). The place to see and
+   add the reports: the list and the upload first, how the passport's readers see them last (redesign R5, item 6). *@
 <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="accidents">
     <span class="bp-summary-title">Accident reports <span class="bp-attr-number">#92</span></span>
     <h2>@(Model.AccidentReports.Count == 0 ? "No accidents reported" : Words.Count(Model.AccidentReports.Count, "accident report"))</h2>
-    <p class="mb-0">The passport links to the battery's <a href="@ReaderPages.AccidentsHref(Model.Pack.Id)">accident page</a>, which lists these reports or says none were reported. Seen only by the battery's cluster, market surveillance and @Model.Team.</p>
+    <p class="mb-0">See and add this battery's accident reports here. They're seen only by the battery's cluster, market surveillance and @Model.Team.</p>
     @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "accidents")
     @if (Model.AccidentReports.Count > 0)
     {
@@ -21,12 +22,23 @@
     }
     @if (Model.FrozenNote is { } frozen)
     {
-        <p class="bp-help-text mt-2 mb-0" data-frozen>@frozen</p>
+        @* Why adding is off, and where the battery's reports go now (R5, item 6). *@
+        <div class="bp-accident-frozen" data-frozen>
+            <svg class="bp-i" aria-hidden="true"><use href="#i-pause" /></svg>
+            <p class="mb-0">
+                <strong>Adding reports is off on this passport.</strong> @frozen
+                @if (Model.NewPassportHref is { } next)
+                {
+                    <a href="@next">Add them on the new passport</a>
+                }
+            </p>
+        </div>
     }
     else
     {
     <form method="post" action="@Model.Href/accidents" enctype="multipart/form-data" class="bp-attr-upload bp-accident-upload" data-upload-row>
         @Html.AntiForgeryToken()
+        <h3 class="bp-subheading">Add a report</h3>
         <div class="bp-attr-file">
             <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                 Choose file
@@ -56,4 +68,5 @@
         </div>
     </form>
     }
+    <p class="bp-accident-readers"><a href="@ReaderPages.AccidentsHref(Model.Pack.Id)" target="_blank" rel="noreferrer"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>How readers see it</a> <span class="bp-batteries-muted">· the accident page the passport links to</span></p>
 </section>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 471fd85..700a34f 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -1900,6 +1900,43 @@ main .bp-field-marker.is-published {
   margin-top: 16px;
 }
 
+/* A frozen passport: why adding is off, and the way to the new passport (redesign R5, item 6). */
+.bp-accident-frozen {
+  display: flex;
+  align-items: flex-start;
+  gap: 10px;
+  margin-top: 14px;
+  padding: 10px 12px;
+  border: 1px solid var(--bp-border);
+  border-radius: 10px;
+  background: rgba(100, 116, 139, 0.06);
+}
+
+.bp-accident-frozen .bp-i {
+  margin-top: 2px;
+  color: var(--bp-muted);
+}
+
+/* How the passport's readers see the reports: a small link, after the work. */
+.bp-accident-readers {
+  margin: 16px 0 0;
+  padding-top: 12px;
+  border-top: 1px solid var(--bp-border);
+  font-size: 0.85rem;
+}
+
+.bp-accident-readers a {
+  display: inline-flex;
+  align-items: center;
+  gap: 6px;
+  font-weight: 600;
+}
+
+.bp-accident-readers .bp-i {
+  width: 16px;
+  height: 16px;
+}
+
 /* A new battery: where it comes from, then its own values. */
 .bp-new-battery-part + .bp-new-battery-part {
   margin-top: 22px;
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AccidentSectionTests"`
Expected: `Passed!  - Failed:     0, Passed:     3`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs web/Dpp/Pages/BatteryCards.cs web/Dpp/Pages/BatteryPages.cs web/Views/Batteries/_AccidentsCard.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): the accident reports section is where they are seen and added" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Key figures on the summary and the detail page

The owner's item 4, set R of the mock-up. The summary's report card becomes "Key figures": eight public values in three groups (using it, environment, safety), the carbon pie as one bar. The same card sits on the detail page between the hero and the data sections, its titles linking down to their full entries.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/KeyFiguresTests.cs`
- Create: `web/Dpp/Pages/KeyFigures.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/Views/Passport/Summary.cshtml`
- Create: `web/Views/Passport/_KeyFigure.cshtml`
- Create: `web/Views/Passport/_KeyFigureLabel.cshtml`
- Create: `web/Views/Passport/_KeyFigures.cshtml`
- Modify: `web/Views/Passport/_PassportTabs.cshtml`
- Modify: `web/Views/Shared/_Icons.cshtml`
- Modify: `web/wwwroot/css/bp-status.css`
- Create: `web/wwwroot/js/bp-passport-links.js`

- [ ] **Step 1: Write the failing tests**

Patch `r5-task6-tests.patch` (`BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`). Save the block as `../r5-task6-tests.patch`, outside the worktree, and run `git apply ../r5-task6-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
index 5dff8bd..f6b77a1 100644
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -62,7 +62,7 @@ public sealed class AdminFeedbackFollowupTests
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.Contains("bp-report-hero-card", hero);
-        Assert.Contains("bp-report-wide-section", summary);
+        Assert.Contains("bp-report-wide-section", File.ReadAllText(RepoFile("web", "Views", "Passport", "_KeyFigures.cshtml"))); // the report card is Key figures since R5
         Assert.Contains("bp-report-wide-section", tabs);
         Assert.Contains("bp-report-hero-layout", hero);
         Assert.Contains("bp-report-hero-media", hero);
@@ -109,12 +109,13 @@ public sealed class AdminFeedbackFollowupTests
     }
 
     [Fact]
-    public void ReportCharts_ShouldUseTableLegendsAndOriginalPowerHeading()
+    public void ReportCharts_ShouldBeTheKeyFiguresCard()
     {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
+        // The "Summary report" card became "Key figures" (redesign R5); its legend is a list beside one bar.
+        var card = File.ReadAllText(RepoFile("web", "Views", "Passport", "_KeyFigures.cshtml"));
 
-        Assert.Contains("<h3 class=\"bp-subheading\">Original power capability</h3>", summary);
-        Assert.Contains("bp-chart-value-legend", summary);
+        Assert.Contains(">Key figures</span>", card);
+        Assert.Contains("bp-kf-legend", card);
     }
 
     [Fact]
@@ -129,7 +130,7 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains("bp-report-identity-panel--identifiers", hero);
         Assert.Contains("bp-report-identity-panel--qr", hero);
         Assert.DoesNotContain("bp-report-media-code-row", hero);
-        Assert.Contains("CultureInfo.InvariantCulture", summary);
+        Assert.Contains("CultureInfo.InvariantCulture", File.ReadAllText(RepoFile("web", "Views", "Passport", "_KeyFigures.cshtml"))); // the percent widths, in Key figures since R5
         Assert.Contains("grid-template-columns: minmax(220px, 1fr) minmax(280px, 1.2fr) minmax(160px, 220px);", css);
         Assert.Contains(".bp-report-identity-panel--qr", css);
         Assert.Contains("@media (max-width: 1180px)", css);
```

`BatteryPassWeb.Tests/Dpp/KeyFiguresTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, owner's item 4 (decision 37): "Key figures" replaces the summary's "Summary report" card and shows on
/// the detail page under the hero too. Public values only, the same card for every reader; nothing is worked out.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class KeyFiguresTests(MongoFixture mongo)
{
    private static string View(string name) => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", name)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static readonly Dictionary<int, JsonNode?> Compact7M = new()
    {
        [57] = JsonNode.Parse("""{"amperehourMiliamperehourValue":140}"""),
        [64] = JsonNode.Parse("""{"voltValue":320}"""),
        [65] = JsonNode.Parse("""{"voltValue":460}"""),
        [66] = JsonNode.Parse("""{"voltValue":400}"""),
        [67] = JsonNode.Parse("""{"wattValueAt80PercentStateOfCharge":171000,"wattValueAt20PercentStateOfCharge":126000}"""),
        [70] = JsonNode.Parse("""{"wattValue":180000}"""),
        [82] = JsonValue.Create(3000),
        [85] = JsonNode.Parse("""{"amperePerAmpereHourValue":0.5}"""),
        [71] = JsonNode.Parse("""{"percentageValue":93}"""),
        [72] = JsonNode.Parse("""{"percentageValue":90}"""),
        [30] = JsonNode.Parse("""{"kgCO2EquivalentPerKilowattHourValue":64}"""),
        [35] = JsonValue.Create("Class B"),
        [53] = JsonNode.Parse("""{"percentageValue":38}"""),
        [42] = JsonNode.Parse("""["Cobalt","Lithium"]"""),
        [88] = JsonNode.Parse("""{"celsiusValue":-20}"""),
        [89] = JsonNode.Parse("""{"celsiusValue":45}"""),
        [26] = JsonNode.Parse("""{"agentFireClass":"B","extinguishingAgent":"Water"}"""),
    };

    private static string? Words(int number) => number switch { 19 => "8 years", 41 => "Li-ion NMC", _ => null };

    [Fact]
    public void TheFigures_ShouldReadTheFilesValues_InWordsAReaderKnows()
    {
        var figures = KeyFigures.From(number => Compact7M.GetValueOrDefault(number), Words, [], [new ChartPart("Nickel", 30, "%")]);

        Assert.Equal(["Capacity and voltage", "Power", "Expected lifetime", "Round-trip efficiency"], figures.Using.Select(figure => figure.Label));
        Assert.Equal(new KeyFigure("battery", "Capacity and voltage", "140 Ah", "at 400 V", "Works between 320 and 460 V", 57), figures.Using[0]);
        Assert.Equal(new KeyFigure("bolt", "Power", "171 kW", "at 80 % charge", "126 kW at 20 % charge · at most 180 kW", 67), figures.Using[1]); // the stored W, in kW
        Assert.Equal(new KeyFigure("reuse", "Expected lifetime", "3,000", "cycles", "Tested at 0.5 C · warranty 8 years", 82), figures.Using[2]);
        Assert.Equal("90 % at half its cycle life", figures.Using[3].Under);
        Assert.Equal(("64", "Class B"), (figures.Carbon!.Value, figures.CarbonClass));
        Assert.Equal(38m, figures.Renewable);
        Assert.Equal(("value-71", "value-30", 49), (figures.Using[3].Anchor, figures.Carbon.Anchor, figures.RecycledNumber)); // where the detail page's links land
        Assert.Equal(new KeyFigure("flask", "Chemistry", "Li-ion NMC", null, "Critical raw materials: cobalt, lithium", 41), figures.Safety[0]);
        Assert.Equal(new KeyFigure("thermo", "Storage and fire", "−20 to 45 °C", "when idle", "In a fire: water (fire class B)", 88), figures.Safety[1]);
    }

    [Fact]
    public void AFigureWithoutItsValue_ShouldNotShow()
    {
        var figures = KeyFigures.From(number => number == 57 ? Compact7M[57] : null, _ => null, [], []);

        Assert.Equal(new KeyFigure("battery", "Capacity and voltage", "140 Ah", null, null, 57), Assert.Single(figures.Using));
        Assert.Null(figures.Carbon);
        Assert.Empty(figures.Safety);
        Assert.True(figures.Any);
        Assert.False(KeyFigures.From(_ => null, _ => null, [], []).Any);
    }

    [Fact]
    public async Task TheCard_ShouldBeTheSameForEveryReader_OnBothPages()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = PassportPagesTests.Pages(rig);
        var id = PassportPagesTests.Identifier(rig);

        var summary = (await pages.SummaryAsync(Reader.Anonymous, id))!.KeyFigures!;
        var purchaser = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), id))!.KeyFigures!;

        Assert.True(summary.Any);
        Assert.Equal(summary.Using, purchaser.Using);
        Assert.Equal(summary.Safety, purchaser.Safety);
        Assert.Equal(summary.Carbon, purchaser.Carbon);
        Assert.Equal(summary.Recycled, purchaser.Recycled);
        Assert.DoesNotContain(summary.Using.Concat(summary.Safety), figure => figure.Label.Contains("charge", StringComparison.OrdinalIgnoreCase) && figure.Label != "Power"); // no state of charge (#63, purchaser data)
    }

    [Fact]
    public async Task EveryFiguresLink_ShouldLandOnAnEntryOfTheDetailPage()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var page = (await PassportPagesTests.Pages(rig).DetailAsync(Reader.With("team@example.com", ReaderRole.Team), PassportPagesTests.Identifier(rig)))!;
        var figures = page.KeyFigures!;
        var entries = page.Sections.Concat(page.Audiences).SelectMany(section => section.Values).Select(value => KeyFigures.Anchor(value.Attribute.Number)).ToHashSet();

        var anchors = figures.Using.Concat(figures.Safety).Append(figures.Carbon).OfType<KeyFigure>().Select(figure => figure.Anchor).Append(KeyFigures.Anchor(figures.RecycledNumber)).ToArray();

        Assert.NotEmpty(anchors);
        Assert.All(anchors, anchor => Assert.Contains(anchor, entries));
    }

    [Fact]
    public void OnTheDetailPage_EachTitleShouldLinkDown_AndTheLinkOpenItsTab()
    {
        var label = View("_KeyFigureLabel.cshtml");
        var tabs = View("_PassportTabs.cshtml");
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-passport-links.js"));

        Assert.Contains("@if (ViewData[\"Detail\"] is true)", label, StringComparison.Ordinal); // the summary's card has no links
        Assert.Contains("<a href=\"#@Model.Anchor\" data-key-figure-link>", label, StringComparison.Ordinal);
        Assert.Contains("<div id=\"@KeyFigures.Anchor(value.Attribute.Number)\">", tabs, StringComparison.Ordinal);
        Assert.Contains("<script src=\"~/js/bp-passport-links.js\" asp-append-version=\"true\"></script>", View("Detail.cshtml"), StringComparison.Ordinal);
        Assert.Contains("window.bootstrap.Tab.getOrCreateInstance(button).show();", script, StringComparison.Ordinal);
        Assert.Contains("folded.open = true;", script, StringComparison.Ordinal);
        Assert.Contains("entry.classList.add('is-landed');", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BothPages_ShouldShowTheCard_AndTheOldSummaryReportIsGone()
    {
        var summary = View("Summary.cshtml");
        var detail = View("Detail.cshtml");
        var card = View("_KeyFigures.cshtml");

        Assert.Contains("<partial name=\"_KeyFigures\" model=\"keyFigures\" />", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Summary report", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("bp-pie", summary, StringComparison.Ordinal);
        Assert.True(detail.IndexOf("_PassportHero", StringComparison.Ordinal) < detail.IndexOf("_KeyFigures", StringComparison.Ordinal)
            && detail.IndexOf("_KeyFigures", StringComparison.Ordinal) < detail.IndexOf("_PassportTabs", StringComparison.Ordinal)); // under the hero
        Assert.Contains("<span class=\"bp-summary-title\" id=\"key-figures-title\">Key figures</span>", card, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-kf-stack\" role=\"img\"", card, StringComparison.Ordinal); // one bar instead of the pie
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminFeedbackFollowupTests | FullyQualifiedName~KeyFiguresTests"`
Expected: the build fails with CS0103, CS0246, CS1061 (for example `error CS0103 The name 'KeyFigures' does not exist in the current context`).

- [ ] **Step 3: Write the code**

Patch `r5-task6-code.patch` (`web/Dpp/Pages/PassportPages.cs`, `web/Views/Passport/Detail.cshtml`, `web/Views/Passport/Summary.cshtml`, `web/Views/Passport/_PassportTabs.cshtml`, `web/Views/Shared/_Icons.cshtml`, `web/wwwroot/css/bp-status.css`). Save the block as `../r5-task6-code.patch`, outside the worktree, and run `git apply ../r5-task6-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index ba40484..31dde22 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -80,6 +80,9 @@ public sealed record PassportPage(
     /// <summary>The line about an inactive passport or one that replaces an earlier passport (S7); null otherwise.</summary>
     public PassportNotice? Notice { get; init; }
 
+    /// <summary>The key figures card (redesign R5): public values only, the same on the summary and the detail page.</summary>
+    public KeyFigures? KeyFigures { get; init; }
+
     /// <summary>Why the passport is invalid and who said so, for the team and market surveillance only (decision 34); null otherwise.</summary>
     public string? MarkReason { get; init; }
 
@@ -277,6 +280,9 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
                 .ToArray()
             : [];
 
+        var carbon = Chart(everyone, CarbonStages, "kgCO2EquivalentPerKilowattHourValue", "kgCO2-eq/kWh");
+        var recycled = Chart(everyone, RecycledShares, "percentageValue", "%");
+        string? Words(int number) => sections.SelectMany(section => section.Values).FirstOrDefault(value => value.Attribute.Number == number)?.Lines.FirstOrDefault()?.Text;
         return new PassportPage(
             version,
             current,
@@ -291,9 +297,10 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
             AttributeCatalog.All.Any(a => a.Audience != Audience.Public && AudienceRules.MaySee(who, a, clusters)),
             sections,
             audiences,
-            Chart(everyone, CarbonStages, "kgCO2EquivalentPerKilowattHourValue", "kgCO2-eq/kWh"),
-            Chart(everyone, RecycledShares, "percentageValue", "%"))
+            carbon,
+            recycled)
         {
+            KeyFigures = KeyFigures.From(number => Find(everyone, AttributeCatalog.ByNumber(number)), Words, carbon, recycled),
             ModuleCount = sources.Pack.Modules.Count,
             ModuleDescription = sources.Model.ModuleDescription,
             Modules = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Modules : null,
diff --git a/web/Views/Passport/Detail.cshtml b/web/Views/Passport/Detail.cshtml
index c4fe9a4..0aaa104 100644
--- a/web/Views/Passport/Detail.cshtml
+++ b/web/Views/Passport/Detail.cshtml
@@ -12,6 +12,11 @@
         <p class="bp-warning" role="status">@marked</p>
     }
     <partial name="_PassportHero" model="Model" view-data="ViewData" />
+    @* The same key figures as the summary, under the hero (redesign R5). *@
+    @if (Model.KeyFigures is { } keyFigures)
+    {
+        <partial name="_KeyFigures" model="keyFigures" />
+    }
     <partial name="_PassportTabs" model="Model" />
     @if (Model.Modules is { Count: > 0 } modules)
     {
@@ -41,3 +46,7 @@
         <partial name="_LiveHistory" model="history" />
     }
 </main>
+
+@section Scripts {
+    <script src="~/js/bp-passport-links.js" asp-append-version="true"></script>
+}
diff --git a/web/Views/Passport/Summary.cshtml b/web/Views/Passport/Summary.cshtml
index 23802c3..94b1e45 100644
--- a/web/Views/Passport/Summary.cshtml
+++ b/web/Views/Passport/Summary.cshtml
@@ -1,98 +1,17 @@
-@using System.Globalization
 @using BatteryPassWeb.Dpp.Pages
 @model PassportPage
 @{
     // The public summary (design 12): no sign-in, opened from the battery's QR code. Public data only.
     ViewData["Title"] = $"Battery passport · {Model.Text(11)}";
     ViewData["Detail"] = false;
-    var colours = new[] { "#22577a", "#38a3a5", "#f2a541", "#57cc99" };
-    var carbonTotal = Model.Carbon.Sum(part => part.Value);
-    var power = Model.Value(67);
-
-    string Pie()
-    {
-        var start = 0m;
-        var slices = new List<string>();
-        for (var index = 0; index < Model.Carbon.Count; index++)
-        {
-            var end = carbonTotal == 0 ? 0 : start + Model.Carbon[index].Value / carbonTotal * 100;
-            slices.Add($"{colours[index]} {start.ToString("0.##", CultureInfo.InvariantCulture)}% {end.ToString("0.##", CultureInfo.InvariantCulture)}%");
-            start = end;
-        }
-
-        return carbonTotal == 0 ? "conic-gradient(#d1d5db 0% 100%)" : $"conic-gradient({string.Join(", ", slices)})";
-    }
 }
 <main class="bp-page">
     <partial name="_PassportHero" model="Model" view-data="ViewData" />
 
-    @if (power is not null || Model.Carbon.Count > 0 || Model.Recycled.Count > 0)
+    @* Key figures (redesign R5, owner's item 4): the old report card, renamed and widened. *@
+    @if (Model.KeyFigures is { } keyFigures)
     {
-        // One row on wide screens: power, carbon footprint and recycled content side by side; stacked on narrow ones.
-        <section class="bp-card bp-report-wide-section">
-            <span class="bp-summary-title">Summary report</span>
-            <div class="row g-4 mt-1">
-                @if (power is not null)
-                {
-                    <div class="col-12 col-md-6 col-xl-3 bp-summary-report-row bp-summary-report-row--power">
-                        <h3 class="bp-subheading">Original power capability</h3>
-                        <div class="bp-metric-grid mt-2" style="grid-template-columns: 1fr;">
-                            @foreach (var line in power.Lines)
-                            {
-                                <div class="bp-metric"><p>@(line.Label ?? "Power")</p><strong>@line.Text</strong></div>
-                            }
-                        </div>
-                    </div>
-                }
-                @if (Model.Carbon.Count > 0)
-                {
-                    <div class="col-12 col-md-6 col-xl-5 bp-summary-report-row bp-summary-report-row--carbon">
-                        <h3 class="bp-subheading">Carbon footprint</h3>
-                        @if (Model.Value(30) is { } perUnit)
-                        {
-                            <p class="text-secondary small mb-2">@perUnit.Lines[0].Text per functional unit</p>
-                        }
-                        <div class="bp-pie-row">
-                            <div class="bp-pie" style="width:140px;height:140px;background:@Pie();"></div>
-                            <table class="bp-chart-value-legend" style="flex:1 1 200px;width:auto;min-width:0;" aria-label="Carbon footprint per life-cycle stage">
-                                <tbody>
-                                    @for (var index = 0; index < Model.Carbon.Count; index++)
-                                    {
-                                        <tr>
-                                            <td><span class="bp-dot" style="background:@colours[index]"></span>@Model.Carbon[index].Label</td>
-                                            <td>@ValueWords.Number(Model.Carbon[index].Value) @Model.Carbon[index].Unit</td>
-                                        </tr>
-                                    }
-                                </tbody>
-                            </table>
-                        </div>
-                    </div>
-                }
-                @if (Model.Recycled.Count > 0)
-                {
-                    <div class="col-12 col-xl-4 bp-summary-report-row bp-summary-report-row--recycled">
-                        <h3 class="bp-subheading">Recycled content share</h3>
-                        <div class="row g-2 mt-1">
-                            @foreach (var share in Model.Recycled)
-                            {
-                                var percent = Math.Clamp(share.Value, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);
-                                <div class="col-6 col-sm-3 col-xl-6">
-                                    <div class="bp-donut-card d-flex align-items-center gap-2">
-                                        <div class="bp-donut flex-shrink-0 m-0" style="width:48px;height:48px;background:conic-gradient(#08a348 0 @(percent)%, #d1d5db @(percent)% 100%);" aria-hidden="true">
-                                            <div class="bp-donut-hole" style="width:26px;height:26px;"></div>
-                                        </div>
-                                        <div class="lh-sm">
-                                            <span class="d-block small text-secondary">@share.Label</span>
-                                            <strong>@ValueWords.Number(share.Value) %</strong>
-                                        </div>
-                                    </div>
-                                </div>
-                            }
-                        </div>
-                    </div>
-                }
-            </div>
-        </section>
+        <partial name="_KeyFigures" model="keyFigures" />
     }
 
     <partial name="_PassportTabs" model="Model" />
diff --git a/web/Views/Passport/_PassportTabs.cshtml b/web/Views/Passport/_PassportTabs.cshtml
index 9b5c661..393a914 100644
--- a/web/Views/Passport/_PassportTabs.cshtml
+++ b/web/Views/Passport/_PassportTabs.cshtml
@@ -45,7 +45,8 @@
                         @foreach (var value in tab.Values)
                         {
                             var single = value.Lines.Count == 1 && value.Lines[0].Label is null;
-                            <div>
+                            @* Each entry has an id, where the key figures' links land (redesign R5). *@
+                            <div id="@KeyFigures.Anchor(value.Attribute.Number)">
                                 <dt>@(value.Attribute.IsExtension ? $"{value.Attribute.Label} {value.Attribute.Name}" : value.Attribute.Name)</dt>
                                 <dd>
                                     @for (var line = 0; line < value.Lines.Count; line++)
diff --git a/web/Views/Shared/_Icons.cshtml b/web/Views/Shared/_Icons.cshtml
index a337285..738c5ec 100644
--- a/web/Views/Shared/_Icons.cshtml
+++ b/web/Views/Shared/_Icons.cshtml
@@ -35,4 +35,10 @@
     <symbol id="i-shield" viewBox="0 0 24 24"><path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z" /><path d="M12 8v5M12 16v.5" /></symbol>
     <symbol id="i-pencil" viewBox="0 0 24 24"><path d="M4 20h4L19 9l-4-4L4 16z" /><path d="M13 7l4 4" /></symbol>
     <symbol id="i-publish" viewBox="0 0 24 24"><path d="M12 15V4M7 9l5-5 5 5" /><path d="M5 14v6h14v-6" /></symbol>
+    <symbol id="i-bolt" viewBox="0 0 24 24"><path d="M13 3L5 14h6l-1 7 8-11h-6z" /></symbol>
+    <symbol id="i-gauge" viewBox="0 0 24 24"><path d="M4 16a8 8 0 1 1 16 0" /><path d="M12 16l4-5" /><path d="M4 20h16" /></symbol>
+    <symbol id="i-leaf" viewBox="0 0 24 24"><path d="M5 19c0-8 5-13 15-14-1 10-6 15-14 15" /><path d="M5 19l8-8" /></symbol>
+    <symbol id="i-recycle" viewBox="0 0 24 24"><path d="M7 19H4l3-5M17 19h3l-3-5M12 4l-2 3.5M12 4l2 3.5" /><path d="M8.5 9.5L6 14M15.5 9.5L18 14M9 19h6" /></symbol>
+    <symbol id="i-flask" viewBox="0 0 24 24"><path d="M9 3h6M10 3v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3" /><path d="M7.5 15h9" /></symbol>
+    <symbol id="i-thermo" viewBox="0 0 24 24"><path d="M10 14V5a2 2 0 1 1 4 0v9a4 4 0 1 1-4 0z" /><path d="M12 9v7" /></symbol>
 </svg>
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index 1cf7d5e..f51e65c 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -445,3 +445,47 @@ th .bp-status-help-words {
 [data-theme="dark"] .bp-test-tag { border-color: #b45309; background: transparent; color: #fcd34d; }
 [data-theme="dark"] .bp-test-note { color: #fcd34d; }
 [data-theme="dark"] .bp-hero-compact .bp-hero-picture { background: #e9eef5; }
+
+/* ==== Key figures (redesign R5, owner's item 4, decision 37) =========================================================
+   The public values a reader wants first, as tiles, on the summary and on the detail page under the hero. */
+.bp-kf-head { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 4px 16px; }
+.bp-kf-head p { margin: 0; color: var(--bp-muted); font-size: 0.88rem; }
+.bp-kf-group { margin: 16px 0 8px; color: var(--bp-muted); font-size: 0.72rem; font-weight: 750; letter-spacing: 0.06em; text-transform: uppercase; }
+.bp-kf-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; }
+.bp-kf { min-width: 0; padding: 14px 16px; border: 1px solid var(--bp-border); border-radius: 12px; background: var(--bp-surface); }
+.bp-kf.is-wide { grid-column: span 2; }
+.bp-kf-label { display: flex; align-items: center; gap: 8px; color: var(--bp-muted); font-size: 0.8rem; font-weight: 650; }
+.bp-kf-label .bp-i { width: 18px; height: 18px; color: var(--bp-primary); }
+.bp-kf-value { margin-top: 6px; font-size: 1.45rem; font-weight: 700; line-height: 1.2; overflow-wrap: anywhere; }
+.bp-kf-value small { color: var(--bp-muted); font-size: 0.9rem; font-weight: 600; }
+.bp-kf-sub { margin: 2px 0 0; color: var(--bp-muted); font-size: 0.84rem; line-height: 1.35; }
+.bp-kf-class { display: inline-block; margin-left: 8px; padding: 1px 9px; border: 1px solid #86efac; border-radius: 999px; background: #f0fdf4; color: #166534; font-size: 0.8rem; font-weight: 700; vertical-align: 4px; }
+.bp-kf-stack { display: flex; height: 12px; margin-top: 10px; overflow: hidden; border-radius: 999px; background: #e2e8f0; }
+.bp-kf-stack span { display: block; height: 100%; }
+.bp-kf-legend { display: flex; flex-wrap: wrap; gap: 4px 14px; margin: 8px 0 0; padding: 0; list-style: none; color: var(--bp-muted); font-size: 0.8rem; }
+.bp-kf-legend i { display: inline-block; width: 9px; height: 9px; margin-right: 5px; border-radius: 2px; }
+.bp-kf-legend b { color: var(--bp-text); font-weight: 650; }
+.bp-kf-bars { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: 6px 10px; margin-top: 10px; font-size: 0.84rem; }
+.bp-kf-track { height: 8px; overflow: hidden; border-radius: 999px; background: #e2e8f0; }
+.bp-kf-track span { display: block; height: 100%; border-radius: 999px; background: #08a348; }
+.bp-kf-bars b { font-weight: 650; text-align: right; }
+
+/* On the detail page a figure's title leads down to its full entry, which is marked for a moment where it lands. */
+.bp-kf-label a { display: inline-flex; align-items: center; gap: 8px; color: inherit; text-decoration: none; }
+.bp-kf-label a:hover, .bp-kf-label a:focus-visible { color: var(--bp-primary); text-decoration: underline; text-underline-offset: 3px; }
+.bp-kf-down { color: var(--bp-primary); font-weight: 700; }
+.bp-field-grid > div { scroll-margin-top: 96px; }
+.bp-field-grid > div.is-landed { border-radius: 8px; background: rgba(42, 110, 207, 0.08); box-shadow: 0 0 0 2px rgba(42, 110, 207, 0.55); }
+
+@media (max-width: 991.98px) {
+  .bp-kf-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
+}
+
+@media (max-width: 575.98px) {
+  .bp-kf { padding: 12px; }
+  .bp-kf-value { font-size: 1.25rem; }
+}
+
+[data-theme="dark"] .bp-kf-stack,
+[data-theme="dark"] .bp-kf-track { background: #334155; }
+[data-theme="dark"] .bp-kf-class { border-color: #166534; background: #052e16; color: #86efac; }
```

`web/Dpp/Pages/KeyFigures.cs`:

```csharp
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One key figure: what it is, its value, the value's small words, a line under it, and the attribute whose full entry it
/// links to on the detail page.
/// </summary>
public sealed record KeyFigure(string Icon, string Label, string Value, string? Small = null, string? Under = null, int Number = 0)
{
    /// <summary>The full entry's anchor in the detail page's data sections.</summary>
    public string Anchor => KeyFigures.Anchor(Number);
}

/// <summary>
/// "Key figures" (redesign R5, the owner's item 4, decision 37): the public values a reader wants first, as tiles, on the
/// public summary and on the detail page under the hero. They replace the summary's "Summary report" card (power, carbon
/// pie, recycled content). Public attributes only, read from the passport as everyone sees it, so the card is the same
/// for every reader on both pages. Nothing is worked out: power is the stored watts shown in kW. A tile shows only when
/// its main value is there.
/// </summary>
public sealed record KeyFigures(
    IReadOnlyList<KeyFigure> Using,
    KeyFigure? Carbon,
    string? CarbonClass,
    IReadOnlyList<ChartPart> CarbonStages,
    IReadOnlyList<ChartPart> Recycled,
    decimal? Renewable,
    IReadOnlyList<KeyFigure> Safety)
{
    /// <summary>The carbon stages' colours, in the order of <see cref="CarbonStages"/> (the old pie's).</summary>
    public static IReadOnlyList<string> StageColours { get; } = ["#22577a", "#38a3a5", "#f2a541", "#57cc99"];

    /// <summary>A value's entry in the detail page's data sections (its id there).</summary>
    public static string Anchor(int number) => $"value-{number}";

    /// <summary>Where the recycled content tile links: the first share the passport has (#49 to #52), else the renewable share.</summary>
    public int RecycledNumber => Recycled.Count > 0 ? RecycledFirst : 53;

    private int RecycledFirst { get; init; } = 49;

    /// <summary>Whether there is anything to show.</summary>
    public bool Any => Using.Count > 0 || Carbon is not null || Recycled.Count > 0 || Renewable is not null || Safety.Count > 0;

    /// <param name="find">A public attribute's value in the passport, by its number.</param>
    /// <param name="words">A public attribute's first line in words, by its number (e.g. #19's "8 years").</param>
    public static KeyFigures From(Func<int, JsonNode?> find, Func<int, string?> words, IReadOnlyList<ChartPart> carbonStages, IReadOnlyList<ChartPart> recycled)
    {
        ArgumentNullException.ThrowIfNull(find);
        ArgumentNullException.ThrowIfNull(words);
        decimal? Number(int attribute, string? name = null) => ValueWords.Decimal(name is null ? find(attribute) : find(attribute)?[name]);
        string N(decimal value) => ValueWords.Number(value);
        string Watts(decimal watts) => watts >= 1000 ? $"{N(Math.Round(watts / 1000, 1))} kW" : $"{N(watts)} W";
        string? Join(params string?[] parts) => parts.Any(part => part is not null) ? string.Join(" · ", parts.OfType<string>()) : null;

        var used = new List<KeyFigure>();
        if (Number(57, "amperehourMiliamperehourValue") is { } capacity)
        {
            var nominal = Number(66, "voltValue");
            var (low, high) = (Number(64, "voltValue"), Number(65, "voltValue"));
            used.Add(new("battery", "Capacity and voltage", $"{N(capacity)} Ah", nominal is { } volts ? $"at {N(volts)} V" : null,
                low is { } min && high is { } max ? $"Works between {N(min)} and {N(max)} V" : null, 57));
        }

        if (Number(67, "wattValueAt80PercentStateOfCharge") is { } at80)
        {
            var at20 = Number(67, "wattValueAt20PercentStateOfCharge");
            var most = Number(70, "wattValue");
            used.Add(new("bolt", "Power", Watts(at80), "at 80 % charge",
                Join(at20 is { } low ? $"{Watts(low)} at 20 % charge" : null, most is { } max ? $"at most {Watts(max)}" : null), 67));
        }

        if (Number(82) is { } cycles)
        {
            var rate = Number(85, "amperePerAmpereHourValue");
            used.Add(new("reuse", "Expected lifetime", N(cycles), "cycles",
                Join(rate is { } c ? $"Tested at {N(c)} C" : null, words(19) is { } warranty ? $"warranty {warranty}" : null), 82));
        }

        if (Number(71, "percentageValue") is { } efficiency)
        {
            used.Add(new("gauge", "Round-trip efficiency", $"{N(efficiency)} %", null,
                Number(72, "percentageValue") is { } half ? $"{N(half)} % at half its cycle life" : null, 71));
        }

        var carbon = Number(30, "kgCO2EquivalentPerKilowattHourValue") is { } perKwh
            ? new KeyFigure("leaf", "Carbon footprint", N(perKwh), "kg CO₂e per kWh", Number: 30)
            : null;

        var safety = new List<KeyFigure>();
        if (words(41) is { } chemistry)
        {
            var critical = find(42) is JsonArray materials ? materials.Select(material => material?.GetValue<string>()).OfType<string>().ToArray() : [];
            safety.Add(new("flask", "Chemistry", chemistry, null,
                critical.Length > 0 ? $"Critical raw materials: {string.Join(", ", critical.Select(material => material.ToLowerInvariant()))}" : null, 41));
        }

        if (Number(88, "celsiusValue") is { } idleLow && Number(89, "celsiusValue") is { } idleHigh)
        {
            var agent = find(26) is JsonArray agents ? agents.FirstOrDefault() : find(26);
            var fire = agent?["extinguishingAgent"]?.GetValue<string>();
            var fireClass = agent?["agentFireClass"]?.GetValue<string>();
            safety.Add(new("thermo", "Storage and fire", $"{N(idleLow).Replace("-", "−", StringComparison.Ordinal)} to {N(idleHigh)} °C", "when idle",
                fire is null ? null : $"In a fire: {fire.ToLowerInvariant()}{(fireClass is null ? null : $" (fire class {fireClass})")}", 88));
        }

        var carbonClass = find(35) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        var firstShare = new[] { 49, 50, 51, 52 }.FirstOrDefault(number => Number(number, "percentageValue") is not null, 49);
        return new KeyFigures(used, carbon, carbonClass, carbonStages, recycled, Number(53, "percentageValue"), safety) { RecycledFirst = firstShare };
    }
}
```

`web/Views/Passport/_KeyFigure.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model KeyFigure
@* One key figure's tile (redesign R5): what it is, its value, and a line under it. *@
<div class="bp-kf@(ViewData["Wide"] is true ? " is-wide" : null)">
    @await Html.PartialAsync("_KeyFigureLabel", (Model.Icon, Model.Label, Model.Anchor))
    <div class="bp-kf-value">@Model.Value@if (Model.Small is { } small)
        {
            <small> @small</small>
        }</div>
    @if (Model.Under is { } under)
    {
        <p class="bp-kf-sub">@under</p>
    }
</div>
```

`web/Views/Passport/_KeyFigureLabel.cshtml`:

```cshtml
@model (string Icon, string Label, string Anchor)
@* A key figure's title: on the detail page a link down to its full entry (redesign R5); on the summary plain words. *@
<div class="bp-kf-label">
    @if (ViewData["Detail"] is true)
    {
        <a href="#@Model.Anchor" data-key-figure-link><svg class="bp-i" aria-hidden="true"><use href="#i-@Model.Icon" /></svg>@Model.Label <span class="bp-kf-down" aria-hidden="true">↓</span></a>
    }
    else
    {
        <svg class="bp-i" aria-hidden="true"><use href="#i-@Model.Icon" /></svg>@Model.Label
    }
</div>
```

`web/Views/Passport/_KeyFigures.cshtml`:

```cshtml
@using System.Globalization
@using BatteryPassWeb.Dpp.Pages
@model KeyFigures
@* "Key figures" (redesign R5, owner's item 4, decision 37; mock-up docs/mockups/dpp-v3-key-figures.html): the public
   values a reader wants first, on the summary and on the detail page under the hero. The same card for every reader;
   on the detail page each figure's title links down to its full entry, with its date and source. *@
@if (Model.Any)
{
    <section class="bp-card bp-report-wide-section bp-kf-card" aria-labelledby="key-figures-title">
        <div class="bp-kf-head">
            <span class="bp-summary-title" id="key-figures-title">Key figures</span>
            <p>From this battery's passport. @(ViewData["Detail"] is true ? "Each title leads to its full entry below." : "Every value is in the sections below.")</p>
        </div>
        @if (Model.Using.Count > 0)
        {
            <p class="bp-kf-group">Using it</p>
            <div class="bp-kf-grid">
                @foreach (var figure in Model.Using)
                {
                    @await Html.PartialAsync("_KeyFigure", figure)
                }
            </div>
        }
        @if (Model.Carbon is not null || Model.Recycled.Count > 0 || Model.Renewable is not null)
        {
            <p class="bp-kf-group">Environment</p>
            <div class="bp-kf-grid">
                @if (Model.Carbon is { } carbon)
                {
                    var total = Model.CarbonStages.Sum(stage => stage.Value);
                    <div class="bp-kf is-wide">
                        @await Html.PartialAsync("_KeyFigureLabel", (carbon.Icon, carbon.Label, carbon.Anchor))
                        <div class="bp-kf-value">@carbon.Value <small>@carbon.Small</small>@if (Model.CarbonClass is { } carbonClass)
                            {
                                <span class="bp-kf-class">@carbonClass</span>
                            }</div>
                        @if (total > 0)
                        {
                            @* One bar split by the life-cycle stages, instead of the old pie. *@
                            <div class="bp-kf-stack" role="img" aria-label="@string.Join(", ", Model.CarbonStages.Select(stage => $"{stage.Label} {ValueWords.Number(stage.Value)}"))">
                                @for (var index = 0; index < Model.CarbonStages.Count; index++)
                                {
                                    <span style="width:@((Model.CarbonStages[index].Value / total * 100).ToString("0.##", CultureInfo.InvariantCulture))%;background:@KeyFigures.StageColours[index]"></span>
                                }
                            </div>
                            <ul class="bp-kf-legend">
                                @for (var index = 0; index < Model.CarbonStages.Count; index++)
                                {
                                    <li><i style="background:@KeyFigures.StageColours[index]"></i>@Model.CarbonStages[index].Label <b>@ValueWords.Number(Model.CarbonStages[index].Value)</b></li>
                                }
                            </ul>
                        }
                    </div>
                }
                @if (Model.Recycled.Count > 0 || Model.Renewable is not null)
                {
                    <div class="bp-kf is-wide">
                        @await Html.PartialAsync("_KeyFigureLabel", ("recycle", "Recycled content", KeyFigures.Anchor(Model.RecycledNumber)))
                        @if (Model.Recycled.Count > 0)
                        {
                            <div class="bp-kf-bars">
                                @foreach (var share in Model.Recycled)
                                {
                                    <span>@share.Label</span><span class="bp-kf-track"><span style="width:@(Math.Clamp(share.Value, 0, 100).ToString("0.##", CultureInfo.InvariantCulture))%"></span></span><b>@ValueWords.Number(share.Value) %</b>
                                }
                            </div>
                        }
                        @if (Model.Renewable is { } renewable)
                        {
                            <p class="bp-kf-sub mt-2">Renewable content <strong>@ValueWords.Number(renewable) %</strong></p>
                        }
                    </div>
                }
            </div>
        }
        @if (Model.Safety.Count > 0)
        {
            <p class="bp-kf-group">Safety</p>
            <div class="bp-kf-grid">
                @foreach (var figure in Model.Safety)
                {
                    @await Html.PartialAsync("_KeyFigure", figure, new ViewDataDictionary(ViewData) { ["Wide"] = true })
                }
            </div>
        }
    </section>
}
```

`web/wwwroot/js/bp-passport-links.js`:

```javascript
// The detail page's key figures link down to their full entries (redesign R5, owner's item 4): a link to #value-<n>
// opens the tab the entry sits in and any folded part around it, scrolls to it and marks it for a moment. Without
// JavaScript the link still works when the entry's tab is the open one.
(function () {
    function land(hash) {
        if (!/^#value-\d+$/.test(hash)) { return; }
        const entry = document.querySelector(hash);
        if (!entry) { return; }
        const pane = entry.closest('.tab-pane');
        if (pane && !pane.classList.contains('active')) {
            const button = document.querySelector(`[data-bs-target="#${pane.id}"]`);
            if (button && window.bootstrap) {
                window.bootstrap.Tab.getOrCreateInstance(button).show();
            } else if (button) {
                button.click();
            }
        }
        for (let folded = entry.closest('details:not([open])'); folded; folded = folded.parentElement?.closest('details:not([open])')) {
            folded.open = true;
        }
        document.querySelectorAll('.bp-field-grid > .is-landed').forEach((other) => other.classList.remove('is-landed'));
        entry.classList.add('is-landed');
        // After the tab's fade, so the entry has its place.
        window.setTimeout(() => entry.scrollIntoView({ behavior: 'smooth', block: 'center' }), 160);
    }

    document.addEventListener('click', (event) => {
        const link = event.target.closest('a[data-key-figure-link]');
        if (!link) { return; }
        event.preventDefault();
        history.replaceState(null, '', link.hash);
        land(link.hash);
    });
    window.addEventListener('hashchange', () => land(location.hash));
    land(location.hash);
})();
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminFeedbackFollowupTests | FullyQualifiedName~KeyFiguresTests"`
Expected: `Passed!  - Failed:     0, Passed:    15`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/Dpp/KeyFiguresTests.cs web/Dpp/Pages/KeyFigures.cs web/Dpp/Pages/PassportPages.cs web/Views/Passport/Detail.cshtml web/Views/Passport/Summary.cshtml web/Views/Passport/_KeyFigure.cshtml web/Views/Passport/_KeyFigureLabel.cshtml web/Views/Passport/_KeyFigures.cshtml web/Views/Passport/_PassportTabs.cshtml web/Views/Shared/_Icons.cshtml web/wwwroot/css/bp-status.css web/wwwroot/js/bp-passport-links.js
git commit -m "feat(dpp): key figures on the summary and the detail page" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Live values as charts; Live values and Modules as tabs

The owner's items 7 and 8. The detail page's "last 7 days" becomes one small chart per value, every reading folded under them; Live values and Modules become tabs of their own, each only for a reader it has something for.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs`
- Modify: `web/Dpp/Pages/LiveTrends.cs`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/Views/Passport/_LiveHistory.cshtml`
- Modify: `web/Views/Passport/_PassportTabs.cshtml`
- Modify: `web/wwwroot/css/bp-status.css`
- Modify: `web/wwwroot/js/bp-passport-links.js`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, the owner's items 7 and 8: on the detail page the last 7 days of live values are small charts (the latest
/// value, a line on a day axis, its low and high), with every reading folded under them; Live values and Modules are
/// tabs of their own, each only for a reader it has something for.
/// </summary>
public sealed class LiveChartsTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 19, 50, 0, DateTimeKind.Utc);

    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static LiveHistoryEntry Entry(int number, JsonNode value, int hoursAgo, string source = "feed") =>
        new(AttributeCatalog.ByNumber(number), value, Now.AddHours(-hoursAgo), Now.AddHours(-hoursAgo), source);

    private static JsonNode Percent(decimal value) => new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = value };

    [Fact]
    public void EachValue_ShouldBeOneChart_WithItsLatestReadingLowHighAndDays()
    {
        var history = new[]
        {
            Entry(63, Percent(61), 0, "form"), Entry(83, JsonValue.Create(411), 2), Entry(63, Percent(85), 24), Entry(63, Percent(25), 72),
        };

        var charts = LiveTrends.Charts(history, Now, entry => entry.Value.ToJsonString());

        Assert.Equal(["State of Charge (SoC) (%)", "Number of full charging and discharging cycles"], charts.Select(chart => chart.Title)); // the catalogue's order, units in the title
        var charge = charts[0];
        Assert.Equal("61 %", charge.Shown);
        Assert.Equal((25m, 85m, 3), (charge.Low!.Value, charge.High!.Value, charge.Readings));
        Assert.Equal($"Measured {ValueWords.Time(Now)} · {PassportPages.SourceWord("form")}", charge.Note);
        Assert.NotNull(charge.Line(300, 70));
        Assert.Equal(7, charge.Days(300).Count); // the midnights of the last 7 days
        Assert.Equal("27 Oct", charge.Days(300)[0].Label);
        Assert.Null(charts[1].Line(300, 70)); // one reading: no line, "One reading this week"
        Assert.Equal(1, charts[1].Readings);
    }

    [Fact]
    public void TheLine_ShouldSpanTheWholeWeek()
    {
        var chart = LiveTrends.Charts([Entry(63, Percent(50), 84), Entry(63, Percent(60), 0)], Now, _ => string.Empty)[0];

        var points = chart.Line(300, 70)!.Split(' ');

        Assert.Equal("150", points[0].Split(',')[0]); // half a week ago sits half way across
        Assert.Equal("300", points[1].Split(',')[0]);
    }

    [Fact]
    public void AValueThatIsNotANumber_ShouldShowItsLatestReadingInWords()
    {
        var report = JsonValue.Create("https://example.com/accidents");
        var chart = LiveTrends.Charts([Entry(92, report!, 3)], Now, _ => "Accident page")[0];

        Assert.Equal("Accident page", chart.Shown);
        Assert.Null(chart.Line(300, 70));
    }

    [Fact]
    public void LiveValuesAndModules_ShouldBeTabs_NotCardsUnderThem()
    {
        var tabs = Read("Views", "Passport", "_PassportTabs.cshtml");
        var detail = Read("Views", "Passport", "Detail.cshtml");
        var live = Read("Views", "Passport", "_LiveHistory.cshtml");

        Assert.Contains("<div class=\"tab-pane fade@(buttons[0].Key == \"live\" ? \" show active\" : string.Empty)\" id=\"tab-live\" role=\"tabpanel\">", tabs, StringComparison.Ordinal);
        Assert.Contains("id=\"tab-modules\" role=\"tabpanel\">", tabs, StringComparison.Ordinal);
        Assert.Contains("var modules = Model.Modules is { Count: > 0 } listed ? listed : null;", tabs, StringComparison.Ordinal); // only for its readers
        Assert.DoesNotContain("bp-passport-module-list", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("<partial name=\"_LiveHistory\"", detail, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_PassportTabs\" model=\"Model\" view-data=\"ViewData\" />", detail, StringComparison.Ordinal);
        Assert.Contains("<summary>All readings (@Model.Count)</summary>", live, StringComparison.Ordinal);
        Assert.Contains("var charts = LiveTrends.Charts(Model, Clock.GetUtcNow().UtcDateTime, Reading);", live, StringComparison.Ordinal);
        Assert.Contains("a[href^=\"#tab-\"]", Read("wwwroot", "js", "bp-passport-links.js"), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~LiveChartsTests"`
Expected: the build fails with CS0117 (for example `error CS0117 'LiveTrends' does not contain a definition for 'Charts'`).

- [ ] **Step 3: Write the code**

Patch `r5-task7-code.patch` (`web/Dpp/Pages/LiveTrends.cs`, `web/Views/Passport/Detail.cshtml`, `web/Views/Passport/_LiveHistory.cshtml`, `web/Views/Passport/_PassportTabs.cshtml`, `web/wwwroot/css/bp-status.css`, `web/wwwroot/js/bp-passport-links.js`). Save the block as `../r5-task7-code.patch`, outside the worktree, and run `git apply ../r5-task7-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/LiveTrends.cs b/web/Dpp/Pages/LiveTrends.cs
index bf2f35c..0108115 100644
--- a/web/Dpp/Pages/LiveTrends.cs
+++ b/web/Dpp/Pages/LiveTrends.cs
@@ -1,5 +1,6 @@
 using System.Globalization;
 using System.Text.Json.Nodes;
+using BatteryPassWeb.Dpp.Api;
 using BatteryPassWeb.Dpp.Catalog;
 using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Store;
@@ -18,8 +19,42 @@ public sealed record TrendPoint(DateTime AtUtc, decimal Value);
 /// <param name="Note">What the small print says under the value: when it was measured, or how the value changes.</param>
 public sealed record LiveTile(string Title, string Property, decimal? Value, string Unit, string Note, IReadOnlyList<TrendPoint> Points)
 {
-    /// <summary>The value with its unit, or a dash before there is one.</summary>
-    public string Shown => Value is { } value ? $"{ValueWords.Number(value)}{(Unit.Length == 0 ? string.Empty : $" {Unit}")}" : "—";
+    /// <summary>The value with its unit; the latest reading in words for a value that isn't a number; a dash before there is one.</summary>
+    public string Shown => Value is { } value ? $"{ValueWords.Number(value)}{(Unit.Length == 0 ? string.Empty : $" {Unit}")}" : Latest ?? "—";
+
+    /// <summary>The latest reading in words (the detail page's charts, redesign R5); null on the overview.</summary>
+    public string? Latest { get; init; }
+
+    /// <summary>How many readings the last 7 days had (the detail page's charts).</summary>
+    public int Readings { get; init; }
+
+    /// <summary>The time axis's ends, the last 7 days (the detail page's charts); null: the readings' own first and last.</summary>
+    public DateTime? FromUtc { get; init; }
+
+    public DateTime? ToUtc { get; init; }
+
+    /// <summary>The lowest and highest reading of the last 7 days; null without a number.</summary>
+    public decimal? Low => Points.Count > 0 ? Points.Min(point => point.Value) : null;
+
+    public decimal? High => Points.Count > 0 ? Points.Max(point => point.Value) : null;
+
+    /// <summary>The axis's days: where each day starts across the box, and its label ("2 Nov" for the first, then the day).</summary>
+    public IReadOnlyList<(double X, string Label)> Days(int width)
+    {
+        if (FromUtc is not { } from || ToUtc is not { } to || to <= from)
+        {
+            return [];
+        }
+
+        var days = new List<(double X, string Label)>();
+        for (var day = from.Date.AddDays(1); day < to; day = day.AddDays(1))
+        {
+            var x = (double)(day.Ticks - from.Ticks) / (to.Ticks - from.Ticks) * width;
+            days.Add((x, days.Count == 0 ? day.ToString("d MMM", CultureInfo.InvariantCulture) : day.Day.ToString(CultureInfo.InvariantCulture)));
+        }
+
+        return days;
+    }
 
     /// <summary>The trend in words, for readers who can't see the line.</summary>
     public string TrendWords => Points.Count < 2
@@ -37,8 +72,8 @@ public sealed record LiveTile(string Title, string Property, decimal? Value, str
             return null;
         }
 
-        var first = Points[0].AtUtc.Ticks;
-        var span = Math.Max(1, Points[^1].AtUtc.Ticks - first);
+        var first = (FromUtc ?? Points[0].AtUtc).Ticks;
+        var span = Math.Max(1, (ToUtc ?? Points[^1].AtUtc).Ticks - first);
         var low = Points.Min(point => point.Value);
         var high = Points.Max(point => point.Value);
         var range = high - low;
@@ -68,6 +103,49 @@ public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock
         (59, "Capacity fade", "%"),
     ];
 
+    /// <summary>
+    /// The detail page's live values as charts (redesign R5, the owner's item 7): one per value in the reader's live
+    /// history (so who sees what is unchanged), in the catalogue's order, each with the last 7 days on a fixed time axis.
+    /// A value that isn't a number (a document) shows its latest reading in words.
+    /// </summary>
+    /// <param name="words">A reading in words, as the readings table shows it.</param>
+    public static IReadOnlyList<LiveTile> Charts(IReadOnlyList<LiveHistoryEntry> history, DateTime nowUtc, Func<LiveHistoryEntry, string> words)
+    {
+        ArgumentNullException.ThrowIfNull(history);
+        ArgumentNullException.ThrowIfNull(words);
+        var order = AttributeCatalog.All.Select((attribute, index) => (attribute.Number, index)).ToDictionary(pair => pair.Number, pair => pair.index);
+        return history
+            .GroupBy(entry => entry.Attribute.Number)
+            .OrderBy(group => order.GetValueOrDefault(group.Key, int.MaxValue))
+            .Select(group =>
+            {
+                var attribute = group.First().Attribute;
+                var latest = group.MaxBy(entry => entry.MeasuredAtUtc)!;
+                var unit = attribute.Unit ?? string.Empty;
+                var name = attribute.IsExtension ? $"{attribute.Label} {attribute.Name}" : attribute.Name;
+                var points = group
+                    .Select(entry => (entry.MeasuredAtUtc, Value: Number(entry.Value)))
+                    .Where(point => point.Value is not null)
+                    .Select(point => new TrendPoint(point.MeasuredAtUtc, point.Value!.Value))
+                    .OrderBy(point => point.AtUtc)
+                    .ToArray();
+                return new LiveTile(
+                    unit.Length == 0 ? name : $"{name} ({unit})",
+                    attribute.Property,
+                    Number(latest.Value),
+                    unit,
+                    $"Measured {ValueWords.Time(latest.MeasuredAtUtc)} · {PassportPages.SourceWord(latest.Source)}",
+                    points)
+                {
+                    Latest = words(latest),
+                    Readings = group.Count(),
+                    FromUtc = nowUtc.AddHours(-Hours),
+                    ToUtc = nowUtc,
+                };
+            })
+            .ToArray();
+    }
+
     public async Task<IReadOnlyList<LiveTile>> TilesAsync(BatteryPack pack, BatteryModel model, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(pack);
diff --git a/web/Views/Passport/Detail.cshtml b/web/Views/Passport/Detail.cshtml
index 0aaa104..5dcb39c 100644
--- a/web/Views/Passport/Detail.cshtml
+++ b/web/Views/Passport/Detail.cshtml
@@ -17,34 +17,8 @@
     {
         <partial name="_KeyFigures" model="keyFigures" />
     }
-    <partial name="_PassportTabs" model="Model" />
-    @if (Model.Modules is { Count: > 0 } modules)
-    {
-        <section class="bp-card mt-4 bp-passport-module-list" aria-labelledby="modules-title">
-            <span class="bp-summary-title">Modules</span>
-            <h2 id="modules-title" class="h5">@Words.Count(modules.Count, "module")</h2>
-            <p class="text-secondary">Seen only by the battery's cluster, market surveillance and the manufacturer's team.</p>
-            <div class="bp-table-scroll">
-                <table class="table align-middle bp-console-table bp-mobile-card-table mb-0">
-                    <thead><tr><th>Position</th><th>Serial number</th><th>Connectivity ID</th></tr></thead>
-                    <tbody>
-                        @foreach (var module in modules)
-                        {
-                            <tr>
-                                <td data-label="Position">@module.Position</td>
-                                <td data-label="Serial number">@(module.SerialNumber ?? "-")</td>
-                                <td data-label="Connectivity ID">@(module.ConnectivityId ?? "-")</td>
-                            </tr>
-                        }
-                    </tbody>
-                </table>
-            </div>
-        </section>
-    }
-    @if (ViewData["LiveHistory"] is IReadOnlyList<BatteryPassWeb.Dpp.Api.LiveHistoryEntry> { Count: > 0 } history)
-    {
-        <partial name="_LiveHistory" model="history" />
-    }
+    @* The data, Live values and Modules included, as tabs (redesign R5, the owner's item 8). *@
+    <partial name="_PassportTabs" model="Model" view-data="ViewData" />
 </main>
 
 @section Scripts {
diff --git a/web/Views/Passport/_LiveHistory.cshtml b/web/Views/Passport/_LiveHistory.cshtml
index 6e5548e..ac8a8e6 100644
--- a/web/Views/Passport/_LiveHistory.cshtml
+++ b/web/Views/Passport/_LiveHistory.cshtml
@@ -2,16 +2,48 @@
 @using BatteryPassWeb.Dpp.Forms
 @using BatteryPassWeb.Dpp.Pages
 @inject AttributeForms Forms
+@inject TimeProvider Clock
 @model IReadOnlyList<LiveHistoryEntry>
 @{
-    // The live values of the last week, newest measurement first: what the battery system or a person sent, as this
-    // reader may see it (decision 30).
+    // The live values of the last week (decision 30), as this reader may see them: one small chart per value (redesign
+    // R5, the owner's item 7), then every reading, newest first, folded. The detail page's "Live values" tab (item 8).
     const int Shown = 100;
+    const int Width = 300;
+    const int Height = 70;
+    string Reading(LiveHistoryEntry entry) => string.Join(", ", ValueWords.Lines(Forms.For(entry.Attribute), entry.Value).Select(line => line.Text));
+    var charts = LiveTrends.Charts(Model, Clock.GetUtcNow().UtcDateTime, Reading);
 }
-<section class="bp-card mt-4 bp-live-history" aria-labelledby="live-history-title">
-    <span class="bp-summary-title">Live values</span>
-    <h2 id="live-history-title" class="h5">The last 7 days</h2>
-    <p class="text-secondary">Newest first. Each value with when it was measured and how it arrived.@(Model.Count > Shown ? $" The latest {Shown} of {Model.Count}." : string.Empty)</p>
+<h2 class="h5 mb-2">The last 7 days</h2>
+<p class="bp-help-text">Each value now, and how it went over the last week. Every reading, with when it was measured and how it arrived, is under the charts.</p>
+<div class="bp-live-charts">
+    @foreach (var chart in charts)
+    {
+        <figure class="bp-live-chart">
+            <figcaption>@chart.Title</figcaption>
+            <strong class="bp-live-chart-value">@chart.Shown</strong>
+            @if (chart.Line(Width, Height) is { } line)
+            {
+                <svg class="bp-live-chart-plot" viewBox="0 -4 @Width @(Height + 22)" role="img" aria-label="@chart.TrendWords">
+                    @foreach (var (x, label) in chart.Days(Width))
+                    {
+                        <line class="bp-live-chart-day" x1="@x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)" x2="@x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)" y1="0" y2="@Height" />
+                        @:<text class="bp-live-chart-label" x="@x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)" y="@(Height + 14)">@label</text>
+                    }
+                    <polyline points="@line" />
+                </svg>
+                <small class="bp-live-chart-range">Low @ValueWords.Number(chart.Low!.Value) · high @ValueWords.Number(chart.High!.Value)@(chart.Unit.Length == 0 ? null : $" {chart.Unit}") · @Words.Count(chart.Readings, "reading")</small>
+            }
+            else
+            {
+                <span class="bp-live-chart-flat">@(chart.Readings == 1 ? "One reading this week" : $"{Words.Count(chart.Readings, "reading")} this week")</span>
+            }
+            <small>@chart.Note</small>
+        </figure>
+    }
+</div>
+<details class="bp-live-all">
+    <summary>All readings (@Model.Count)</summary>
+    <p class="text-secondary small mt-2">Newest first.@(Model.Count > Shown ? $" The latest {Shown} of {Model.Count}." : string.Empty)</p>
     <div class="bp-table-scroll">
         <table class="table align-middle bp-console-table bp-mobile-card-table mb-0">
             <thead><tr><th>Measured</th><th>Value</th><th>Reading</th><th>Came from</th></tr></thead>
@@ -21,11 +53,11 @@
                     <tr>
                         <td data-label="Measured">@ValueWords.Time(entry.MeasuredAtUtc)</td>
                         <td data-label="Value">@entry.Attribute.Label @entry.Attribute.Name</td>
-                        <td data-label="Reading">@string.Join(", ", ValueWords.Lines(Forms.For(entry.Attribute), entry.Value).Select(line => line.Text))</td>
+                        <td data-label="Reading">@Reading(entry)</td>
                         <td data-label="Came from">@PassportPages.SourceWord(entry.Source)</td>
                     </tr>
                 }
             </tbody>
         </table>
     </div>
-</section>
+</details>
diff --git a/web/Views/Passport/_PassportTabs.cshtml b/web/Views/Passport/_PassportTabs.cshtml
index 393a914..611ac44 100644
--- a/web/Views/Passport/_PassportTabs.cshtml
+++ b/web/Views/Passport/_PassportTabs.cshtml
@@ -6,29 +6,76 @@
     var tabs = Model.Audiences.Select(section => (Section: section, Restricted: true))
         .Concat(Model.Sections.Select(section => (Section: section, Restricted: false)))
         .ToArray();
+    // Live values and Modules are tabs too (redesign R5, the owner's item 8), after the audiences' tabs, each only for a
+    // reader it has something for: the live history is cut for the reader, the modules are null for those who may not see them.
+    var live = Model.IsCurrent && ViewData["LiveHistory"] is IReadOnlyList<BatteryPassWeb.Dpp.Api.LiveHistoryEntry> { Count: > 0 } history ? history : null;
+    var modules = Model.Modules is { Count: > 0 } listed ? listed : null;
+    var extra = new List<(string Key, string Tab)>();
+    if (live is not null)
+    {
+        extra.Add(("live", "Live values"));
+    }
+
+    if (modules is not null)
+    {
+        extra.Add(("modules", "Modules"));
+    }
+
+    var buttons = tabs.Take(Model.Audiences.Count).Select(tab => (tab.Section.Key, tab.Section.Tab, Restricted: true))
+        .Concat(extra.Select(tab => (tab.Key, tab.Tab, Restricted: true)))
+        .Concat(tabs.Skip(Model.Audiences.Count).Select(tab => (tab.Section.Key, tab.Section.Tab, Restricted: false)))
+        .ToArray();
 }
 <section class="bp-card bp-report-wide-section">
     <ul class="nav nav-tabs bp-tabs" id="passport-tabs" role="tablist" style="flex-wrap:wrap;overflow-x:visible">
-        @for (var index = 0; index < tabs.Length; index++)
+        @for (var index = 0; index < buttons.Length; index++)
         {
-            var (tab, restricted) = tabs[index];
+            var (key, label, restricted) = buttons[index];
             <li class="nav-item" role="presentation">
-                <button class="nav-link@(index == 0 ? " active" : string.Empty)" data-bs-toggle="tab" data-bs-target="#tab-@tab.Key" type="button" role="tab" aria-selected="@(index == 0 ? "true" : "false")">
+                <button class="nav-link@(index == 0 ? " active" : string.Empty)" data-bs-toggle="tab" data-bs-target="#tab-@key" type="button" role="tab" aria-selected="@(index == 0 ? "true" : "false")">
                     @if (restricted)
                     {
                         <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true" style="vertical-align:-2px;margin-right:4px"><rect x="3.5" y="7" width="9" height="6.5" rx="1.5" /><path d="M5.5 7V5.5a2.5 2.5 0 0 1 5 0V7" /></svg>
                     }
-                    @tab.Tab
+                    @label
                 </button>
             </li>
         }
     </ul>
 
     <div class="tab-content pt-4">
+        @if (live is not null)
+        {
+            <div class="tab-pane fade@(buttons[0].Key == "live" ? " show active" : string.Empty)" id="tab-live" role="tabpanel">
+                <partial name="_LiveHistory" model="live" />
+            </div>
+        }
+        @if (modules is not null)
+        {
+            <div class="tab-pane fade@(buttons[0].Key == "modules" ? " show active" : string.Empty)" id="tab-modules" role="tabpanel">
+                <h2 class="h5 mb-2">@Words.Count(modules.Count, "module")</h2>
+                <p class="bp-help-text">Seen only by the battery's cluster, market surveillance and the manufacturer's team.</p>
+                <div class="bp-table-scroll">
+                    <table class="table align-middle bp-console-table bp-mobile-card-table mb-0">
+                        <thead><tr><th>Position</th><th>Serial number</th><th>Connectivity ID</th></tr></thead>
+                        <tbody>
+                            @foreach (var module in modules)
+                            {
+                                <tr>
+                                    <td data-label="Position">@module.Position</td>
+                                    <td data-label="Serial number">@(module.SerialNumber ?? "-")</td>
+                                    <td data-label="Connectivity ID">@(module.ConnectivityId ?? "-")</td>
+                                </tr>
+                            }
+                        </tbody>
+                    </table>
+                </div>
+            </div>
+        }
         @for (var index = 0; index < tabs.Length; index++)
         {
             var (tab, _) = tabs[index];
-            <div class="tab-pane fade@(index == 0 ? " show active" : string.Empty)" id="tab-@tab.Key" role="tabpanel">
+            <div class="tab-pane fade@(buttons[0].Key == tab.Key ? " show active" : string.Empty)" id="tab-@tab.Key" role="tabpanel">
                 <h2 class="h5 mb-2">@tab.Title</h2>
                 <p class="bp-help-text">@tab.Note</p>
                 @if (tab.Key == "purchaser" && Model.Current is not null)
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index f51e65c..16b6895 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -489,3 +489,19 @@ th .bp-status-help-words {
 [data-theme="dark"] .bp-kf-stack,
 [data-theme="dark"] .bp-kf-track { background: #334155; }
 [data-theme="dark"] .bp-kf-class { border-color: #166534; background: #052e16; color: #86efac; }
+
+/* ==== The detail page's live values as charts (redesign R5, the owner's item 7) ======================================
+   One small chart per value: the latest large, the last 7 days on a day axis, its low and high; every reading folded. */
+.bp-live-charts { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 12px; }
+.bp-live-chart { display: grid; gap: 4px; min-width: 0; margin: 0; padding: 14px 16px; border: 1px solid var(--bp-border); border-radius: 12px; background: var(--bp-surface); }
+.bp-live-chart figcaption { color: var(--bp-muted); font-size: 0.8rem; font-weight: 650; }
+.bp-live-chart-value { font-size: 1.35rem; line-height: 1.2; overflow-wrap: anywhere; }
+.bp-live-chart-plot { width: 100%; height: auto; overflow: visible; }
+.bp-live-chart-plot polyline { fill: none; stroke: var(--bp-primary); stroke-width: 2; stroke-linejoin: round; vector-effect: non-scaling-stroke; }
+.bp-live-chart-day { stroke: var(--bp-border); stroke-width: 1; vector-effect: non-scaling-stroke; }
+.bp-live-chart-label { fill: var(--bp-muted); font-size: 11px; text-anchor: middle; }
+.bp-live-chart-range { color: var(--bp-text); font-size: 0.8rem; }
+.bp-live-chart-flat { color: var(--bp-muted); font-size: 0.85rem; }
+.bp-live-chart small { color: var(--bp-muted); }
+.bp-live-all { margin-top: 16px; }
+.bp-live-all > summary { cursor: pointer; color: var(--bp-primary); font-weight: 600; }
diff --git a/web/wwwroot/js/bp-passport-links.js b/web/wwwroot/js/bp-passport-links.js
index d862109..0151e95 100644
--- a/web/wwwroot/js/bp-passport-links.js
+++ b/web/wwwroot/js/bp-passport-links.js
@@ -1,12 +1,13 @@
-// The detail page's key figures link down to their full entries (redesign R5, owner's item 4): a link to #value-<n>
-// opens the tab the entry sits in and any folded part around it, scrolls to it and marks it for a moment. Without
-// JavaScript the link still works when the entry's tab is the open one.
+// The detail page's links into its tabs (redesign R5, the owner's items 4 and 8): a link to #value-<n> opens the tab the
+// entry sits in and any folded part around it, scrolls to it and marks it; a link to #tab-<name> (e.g. #tab-live) opens
+// that tab. Without JavaScript the link still works when its tab is the open one.
 (function () {
     function land(hash) {
-        if (!/^#value-\d+$/.test(hash)) { return; }
+        if (!/^#(value-\d+|tab-[\w-]+)$/.test(hash)) { return; }
         const entry = document.querySelector(hash);
         if (!entry) { return; }
         const pane = entry.closest('.tab-pane');
+        const whole = entry === pane;
         if (pane && !pane.classList.contains('active')) {
             const button = document.querySelector(`[data-bs-target="#${pane.id}"]`);
             if (button && window.bootstrap) {
@@ -18,6 +19,10 @@
         for (let folded = entry.closest('details:not([open])'); folded; folded = folded.parentElement?.closest('details:not([open])')) {
             folded.open = true;
         }
+        if (whole) {
+            window.setTimeout(() => document.querySelector('#passport-tabs').scrollIntoView({ behavior: 'smooth', block: 'start' }), 160);
+            return;
+        }
         document.querySelectorAll('.bp-field-grid > .is-landed').forEach((other) => other.classList.remove('is-landed'));
         entry.classList.add('is-landed');
         // After the tab's fade, so the entry has its place.
@@ -25,7 +30,7 @@
     }
 
     document.addEventListener('click', (event) => {
-        const link = event.target.closest('a[data-key-figure-link]');
+        const link = event.target.closest('a[data-key-figure-link], a[href^="#tab-"]');
         if (!link) { return; }
         event.preventDefault();
         history.replaceState(null, '', link.hash);
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~LiveChartsTests"`
Expected: `Passed!  - Failed:     0, Passed:     4`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs web/Dpp/Pages/LiveTrends.cs web/Views/Passport/Detail.cshtml web/Views/Passport/_LiveHistory.cshtml web/Views/Passport/_PassportTabs.cshtml web/wwwroot/css/bp-status.css web/wwwroot/js/bp-passport-links.js
git commit -m "feat(dpp): the detail page's live values as charts, and Live values and Modules as tabs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: What the page review found: one place per action, clearer words

The whole-page review at 1280 px. Change status, Add a note and Publish passport are only among the header's actions (the Status section and the overview say so instead of repeating them), and the label reads "Compact 7M · Compact family". The passport status shows in the badge's words ("Active"), and the cluster admin's "Hand over…" opens its window from the header, with no second button in the section.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/PageReviewFixesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs`
- Modify: `web/Dpp/Pages/ValueWords.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/Batteries/_StatusCard.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`
- Modify: `web/Views/ClusterAdmin/_HandOverCard.cshtml`
- Create: `web/Views/ClusterAdmin/_HandOverDialog.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r5-task8-tests.patch` (`BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`, `BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs`). Save the block as `../r5-task8-tests.patch`, outside the worktree, and run `git apply ../r5-task8-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs b/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
index f65161b..baddce3 100644
--- a/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
@@ -143,16 +143,17 @@ public sealed class HandOverPagesTests(MongoFixture mongo)
     {
         var cluster = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));
         var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "_HandOverCard.cshtml"));
+        var window = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "_HandOverDialog.cshtml")); // at page level since R5
         var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));
         var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
         var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-record-editor.js"));
         var styles = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css"));
 
         Assert.Contains("~/Views/ClusterAdmin/_HandOverCard.cshtml\", Model.HandOver", cluster, StringComparison.Ordinal);
-        Assert.Contains("data-open-on-load=\"@(found ? \"true\" : \"false\")\"", card, StringComparison.Ordinal);
-        Assert.Contains("name=\"checkedId\"", card, StringComparison.Ordinal);
-        Assert.Contains("TempData[ClusterBatteriesController.HandOverToKey] as string", card, StringComparison.Ordinal); // read once, so the window opens only right after "Find the cluster"
-        Assert.DoesNotContain("TempData.Peek", card, StringComparison.Ordinal);
+        Assert.Contains("data-open-on-load=\"@(found ? \"true\" : \"false\")\"", window, StringComparison.Ordinal);
+        Assert.Contains("name=\"checkedId\"", window, StringComparison.Ordinal);
+        Assert.Contains("TempData[ClusterBatteriesController.HandOverToKey] as string", window, StringComparison.Ordinal); // read once, so the window opens only right after "Find the cluster"
+        Assert.DoesNotContain("TempData.Peek", window, StringComparison.Ordinal);
         Assert.Contains("Cancel the hand-over</button>", cluster, StringComparison.Ordinal); // one Cancel, on the header's line (redesign R4)
         Assert.DoesNotContain("/cancel", card, StringComparison.Ordinal);
         Assert.Contains("foreach (var waiting in Model.Waiting)", registry, StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs b/BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs
index 126bd48..2f1933c 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs
@@ -65,7 +65,7 @@ public sealed class PassportPagesTests(MongoFixture mongo)
             page.Sections.Select(section => section.Tab));
         Assert.Null(page.Value(22));
         Assert.Equal("VPC8C05-6788", page.Text(11));
-        Assert.Equal("active", page.Text(2));
+        Assert.Equal("Active", page.Text(2)); // the badge's words; the passport keeps "active" (R5)
     }
 
     [Fact]
@@ -183,7 +183,7 @@ public sealed class PassportPagesTests(MongoFixture mongo)
         Assert.Equal(second.Id, page.Current?.Id);
         Assert.Equal(first.DigitalProductPassportId, page.Address);
         Assert.Equal(first.DigitalProductPassportId, (await Pages(rig).LocateAsync(first.DigitalProductPassportId))!.Address);
-        Assert.Equal("archived", page.Text(2));
+        Assert.Equal("Archived", page.Text(2)); // the badge's words (R5)
         Assert.Equal("512.5 kg", page.Text(21));
         Assert.Null(page.Value(21)!.Origin);
         var purchaser = page.Audiences.Single(section => section.Key == "purchaser").Values.Select(value => value.Attribute.Number).ToArray();
```

`BatteryPassWeb.Tests/Dpp/PageReviewFixesTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HandOverPagesTests | FullyQualifiedName~PageReviewFixesTests | FullyQualifiedName~PassportPagesTests"`
Expected: `Failed:     9, Passed:    21` (failing: `HandOverPagesTests.ThePages_ShouldDrawTheCardTheWindowAndTheWaitingRows`, `PageReviewFixesTests.EachAction_ShouldBeInTheHeaderOnly`, `PageReviewFixesTests.TheClusterAdminsHandOver_ShouldOpenItsWindowFromTheHeader_WithNoSecondButton`, `PageReviewFixesTests.TheLabel_ShouldNameTheModelAndItsFamily`, `PassportPagesTests.AnArchivedVersion_ShouldShowItsOwnDataAndLinkToTheCurrentOne`, `PassportPagesTests.TheSummary_ShouldShowEveryPublicValueAndNothingElseWhoeverReads`).

- [ ] **Step 3: Write the code**

Patch `r5-task8-code.patch` (`web/Dpp/Pages/ValueWords.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/Batteries/_StatusCard.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/Views/ClusterAdmin/_HandOverCard.cshtml`). Save the block as `../r5-task8-code.patch`, outside the worktree, and run `git apply ../r5-task8-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/ValueWords.cs b/web/Dpp/Pages/ValueWords.cs
index 82e7cda..0e59351 100644
--- a/web/Dpp/Pages/ValueWords.cs
+++ b/web/Dpp/Pages/ValueWords.cs
@@ -34,6 +34,13 @@ public static class ValueWords
             return [new ValueLine(null, Time(updated))];
         }
 
+        // The passport status (#2) in the badge's words ("Active"); the passport keeps the schema's value (redesign R5).
+        // The schema holds it as { "dppStatusValue": "active" }.
+        if (form.Attribute.Number == 2 && (value is JsonObject holder ? holder["dppStatusValue"] : value) is JsonValue status && status.TryGetValue<string>(out var stored))
+        {
+            return [new ValueLine(null, BatteryPassWeb.Dpp.Store.DppStatuses.Words(stored))];
+        }
+
         switch (form.Kind)
         {
             case FormKind.Link:
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 629aef7..c180a32 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -69,7 +69,7 @@
                 <div class="bp-bh-picture"><img src="@picture" alt="Picture of the @sources.Model.Name battery model" /></div>
             }
             <div class="bp-bh-id">
-                <span class="bp-summary-title">Battery · <a href="@ProductDataPages.FamilyHref(sources.Model.FamilyId, sources.Model.Id)">@sources.Model.Name</a> of @Model.Family.Name</span>
+                <span class="bp-summary-title">Battery · <a href="@ProductDataPages.FamilyHref(sources.Model.FamilyId, sources.Model.Id)">@sources.Model.Name</a> · @Model.Family.Name family</span>
                 <h1>@Model.SerialNumber</h1>
                 <div class="bp-badges">
                     @if (StatusBadge.Battery(Model.Status?.Status) is { } battery)
@@ -233,9 +233,10 @@
                                 <p class="mb-0"><span class="bp-missing-note">@Words.Count(publication.MissingCount, "value") missing</span> that the passport needs by February 2027.</p>
                             }
                         </div>
+                        @* One Publish button, the header's (redesign R5's page review). *@
                         @if (!publication.IsPublished && publication.Ready)
                         {
-                            <button type="button" class="bp-primary-button text-nowrap" data-open-dialog="publish-dialog">Publish passport</button>
+                            <p class="bp-help-text mb-0">Publish it with <strong>Publish passport</strong> among the page's actions.</p>
                         }
                     </div>
                     @{ await CardMessagesAsync("publication"); }
diff --git a/web/Views/Batteries/_StatusCard.cshtml b/web/Views/Batteries/_StatusCard.cshtml
index 2f929af..f88c904 100644
--- a/web/Views/Batteries/_StatusCard.cshtml
+++ b/web/Views/Batteries/_StatusCard.cshtml
@@ -18,16 +18,11 @@
             </div>
             <p class="mb-0">@(Model.WhyNot ?? (Model.Status == "waste" ? "The battery is waste, which is final. Mistyped values can still be corrected." : "A status change also re-states the fades and the expected lifetime, and publishes a new version of this passport."))</p>
         </div>
-        <div class="d-flex flex-wrap gap-2">
-            @if (Model.MayAddNote)
-            {
-                <button type="button" class="bp-secondary-button bp-with-icon" data-open-dialog="note-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-note" /></svg>Add a note</button>
-            }
-            @if (Model.CanChange)
-            {
-                <button type="button" class="bp-primary-button bp-with-icon" data-open-dialog="status-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-swap" /></svg>Change status</button>
-            }
-        </div>
+        @* Change status and Add a note are the header's actions only (redesign R5's page review: one place per action). *@
+        @if (Model.CanChange || Model.MayAddNote)
+        {
+            <p class="bp-help-text mb-0">@(Model.CanChange && Model.MayAddNote ? "Change status and Add a note are" : Model.CanChange ? "Change status is" : "Add a note is") among the page's actions.</p>
+        }
     </div>
     @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "status")
     @if (Model.Succession is { Linked: false } next)
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 44f4c4b..40de0cb 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -32,7 +32,7 @@
                 <div class="bp-bh-picture"><img src="@picture" alt="Picture of the @Model.Sources.Model.Name battery model" /></div>
             }
             <div class="bp-bh-id">
-                <span class="bp-summary-title">Battery · @Model.Sources.Model.Name of @Model.Family.Name</span>
+                <span class="bp-summary-title">Battery · @Model.Sources.Model.Name · @Model.Family.Name family</span>
                 <h1>@Model.SerialNumber</h1>
                 <div class="bp-badges">
                     @if (StatusBadge.Battery(Model.Status?.Status) is { } battery)
@@ -93,7 +93,11 @@
                     }
                 </div>
             }
-            <a class="bp-secondary-button bp-with-icon" href="@Model.Href?section=handover"><svg class="bp-i" aria-hidden="true"><use href="#i-handover" /></svg>Hand over…</a>
+            @if (Model.HandOver.Waiting is null)
+            {
+                @* Opens the window directly, like marking a passport (redesign R5, the owner's answer c). *@
+                <button type="button" class="bp-secondary-button bp-with-icon" data-open-dialog="handover-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-handover" /></svg>Hand over…</button>
+            }
             <nav class="bp-bh-open" aria-label="This battery elsewhere">
                 <a class="bp-ilink" href="/dpp/@batteryKey" target="_blank" rel="noreferrer" title="The public summary, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
                 <a class="bp-ilink" href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer" title="Everything your cluster sees"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
@@ -144,6 +148,12 @@
     {
         @await Html.PartialAsync("~/Views/Batteries/_StatusDialog.cshtml", statusWindows)
     }
+    @if (Model.HandOver.Waiting is null)
+    {
+        <div class="bp-console-form-card bp-dialog-host">
+            @await Html.PartialAsync("~/Views/ClusterAdmin/_HandOverDialog.cshtml", Model.HandOver)
+        </div>
+    }
 </main>
 
 @section Scripts {
diff --git a/web/Views/ClusterAdmin/_HandOverCard.cshtml b/web/Views/ClusterAdmin/_HandOverCard.cshtml
index 8640afa..f6d5e4e 100644
--- a/web/Views/ClusterAdmin/_HandOverCard.cshtml
+++ b/web/Views/ClusterAdmin/_HandOverCard.cshtml
@@ -1,14 +1,7 @@
-@using BatteryPassWeb.Controllers
 @using BatteryPassWeb.Dpp.Pages
 @model HandOverCardView
-@* The "Hand over" card and window (step 4d, mock-up S2 and S8): the sender names the receiving cluster by its ID, sees its
-   name, and asks; while it waits, the battery stays here and the sender can cancel. *@
-@{
-    // Read (not peeked), so they are gone on the next visit: the window opens only right after "Find the cluster".
-    var typed = TempData[ClusterBatteriesController.HandOverToKey] as string;
-    var name = TempData[ClusterBatteriesController.HandOverNameKey] as string;
-    var found = typed is not null && name is not null && Model.Waiting is null;
-}
+@* The "Hand over" card (step 4d, mock-up S2): what a hand-over does, or the one waiting. The window is at page level
+   (_HandOverDialog), opened by the header's "Hand over…" (redesign R5, the owner's answer c), so no second button here. *@
 <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="handover">
     @if (Model.Waiting is { } waiting)
     {
@@ -25,26 +18,9 @@
                 <span class="bp-summary-title">Hand over <em class="bp-field-marker">Not passport data</em></span>
                 <h2>Sold or moved to another customer?</h2>
                 <p class="mb-0">Hand the battery over to their cluster. It moves once their cluster admin accepts; your cluster then no longer sees its purchaser data. The passport doesn't change.</p>
+                <p class="bp-help-text mt-2 mb-0">Start it with <strong>Hand over…</strong> among the page's actions.</p>
             </div>
-            <button type="button" class="bp-secondary-button" data-open-dialog="handover-dialog">Hand over…</button>
         </div>
         @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "handover")
-        <dialog id="handover-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="handover-dialog-title" data-open-on-load="@(found ? "true" : "false")">
-            <form method="post" action="@Model.Action" class="bp-dialog-form">
-                @Html.AntiForgeryToken()
-                <h2 id="handover-dialog-title">Hand @Model.SerialNumber over</h2>
-                <p class="bp-help-text">Ask the receiving customer for their cluster ID.</p>
-                <label><span>Receiving cluster ID</span><input type="text" name="toClusterId" value="@typed" autocomplete="off" required /></label>
-                @if (found)
-                {
-                    <input type="hidden" name="checkedId" value="@typed" />
-                    <p class="bp-done-note mb-0">@name</p>
-                }
-                <div class="bp-dialog-actions">
-                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
-                    <button type="submit" class="bp-primary-button">@(found ? "Ask them to accept" : "Find the cluster")</button>
-                </div>
-            </form>
-        </dialog>
     }
 </section>
```

`web/Views/ClusterAdmin/_HandOverDialog.cshtml`:

```cshtml
@using BatteryPassWeb.Controllers
@using BatteryPassWeb.Dpp.Pages
@model HandOverCardView
@* The "Hand over…" window (step 4d, mock-up S8), at page level so the header's "Hand over…" opens it from any section
   (redesign R5, the owner's answer c): the sender names the receiving cluster by its ID, sees its name, and asks. *@
@{
    // Read (not peeked), so they are gone on the next visit: the window opens only right after "Find the cluster".
    var typed = TempData[ClusterBatteriesController.HandOverToKey] as string;
    var name = TempData[ClusterBatteriesController.HandOverNameKey] as string;
    var found = typed is not null && name is not null && Model.Waiting is null;
}
<dialog id="handover-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="handover-dialog-title" data-open-on-load="@(found ? "true" : "false")">
    <form method="post" action="@Model.Action" class="bp-dialog-form">
        @Html.AntiForgeryToken()
        <h2 id="handover-dialog-title">Hand @Model.SerialNumber over</h2>
        <p class="bp-help-text">Ask the receiving customer for their cluster ID.</p>
        <label><span>Receiving cluster ID</span><input type="text" name="toClusterId" value="@typed" autocomplete="off" required /></label>
        @if (found)
        {
            <input type="hidden" name="checkedId" value="@typed" />
            <p class="bp-done-note mb-0">@name</p>
        }
        <div class="bp-dialog-actions">
            <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
            <button type="submit" class="bp-primary-button">@(found ? "Ask them to accept" : "Find the cluster")</button>
        </div>
    </form>
</dialog>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HandOverPagesTests | FullyQualifiedName~PageReviewFixesTests | FullyQualifiedName~PassportPagesTests"`
Expected: `Passed!  - Failed:     0, Passed:    30`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs BatteryPassWeb.Tests/Dpp/PageReviewFixesTests.cs BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs web/Dpp/Pages/ValueWords.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_StatusCard.cshtml web/Views/ClusterAdmin/Battery.cshtml web/Views/ClusterAdmin/_HandOverCard.cshtml web/Views/ClusterAdmin/_HandOverDialog.cshtml
git commit -m "fix(dpp): one place per action on the battery pages, and the model named with its family" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2364, Skipped:     0, Total:  2364` (35 more than the 2329 before).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat eb3105c -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

1. **Restart the app with the usual launcher** and press "Reset demo data".
2. **Batteries (the team's list), at 1280 px:** each row ends in up to four icons; hovering one names it. DEMO-C13M-0001 is listed once; its pencil opens the newest passport, whose header has the "Earlier passport" chip. The Status heading has a small "?" that opens the explainer.
3. **DEMO-C7M-0001:** "What do these mean?" beside the badges. More → "Mark as invalid or for deletion…" opens a window; Cancel closes it. The side menu has no "Passport status".
4. **DEMO-C7M-0001 → Accident reports:** the report, then "Add a report", then "How readers see it". **DEMO-C13M-0001's earlier passport → Accident reports:** "Adding reports is off on this passport" and "Add them on the new passport".
5. **As the demo North admin:** the Registry rows end in the icons, without History.
6. **DEMO-C7M-0001's public page:** "Key figures" with eight tiles in three groups and no links. **Its detail page:** the same card under the hero; "Capacity and voltage ↓" opens the Performance tab and marks "Rated capacity" with its date and source. The Live values tab shows seven small charts and "All readings" folded; the Modules tab the seven modules.
7. **DEMO-C7M-0001 → Status & repairs:** no second Change status or Add a note; the label reads "Compact 7M · Compact family". Its detail page's Passport tab says "DPP Status: Active". **As the demo North admin:** "Hand over…" in the header opens the window from any section.

## Next

Step 5 resumes, as after R4.
