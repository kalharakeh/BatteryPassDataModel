# DPP V3 Redesign R4: The Side-Menu Battery Pages — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The last part of the battery pages' redesign (decision 36), the layout the product owner chose on 2 Nov 2026: **B, a side menu showing one section at a time**, like the old app. It applies to the team's battery page and the cluster admin's.

Before, at phone width the team's page was 6,400–9,600 px tall: nine stacked cards and six Save buttons. After, about 2,300 px; it opens on the overview, and every other section is one tap away.
- **A header** (mock-up H):
  - The model, the serial number and both status badges. Links (point 4) become labelled icons: Public page, Detail page, QR code, Save QR, History.
  - The facts sit in a compact list, with the test-ID notice as a small amber "Test ID" tag that explains itself on a tap (point 7; R3's tag).
  - The actions are labelled buttons: Publish passport, Change status, and Add a note (point 5); the risky ones wait under "More".
  - The notices are one line each:
    - invalid or marked, with the reason and **the one Undo** (point 17);
    - "New passport" or "Earlier passport" as a calm chip and line (point 6);
    - a waiting hand-over with "Cancel it".
- **The side menu**, with a hint or a count per entry:
  - The team's page: Overview · Passport values · Status & repairs · Live values · Accident reports · Modules · Versions · Passport status.
  - The cluster admin's page: Overview · Status & repairs · Live values · Accident reports · Hand over · Modules · Versions.
  - The overview is today's Publication card, whole (what's missing with "Fill in", the file checks, Publish), with the last status, live values and accidents. The missing values are listed only there: the values editor marks its fields and points to the overview instead of counting them again.
- **The cluster admin's header** has the same lines: a waiting hand-over with the one "Cancel the hand-over", and "New passport of …" on a successor. Their page opens only while the passport is active (step 4a), so an invalid or inactive battery shows on the Registry row and the detail page instead.
- **A save returns to its own section** with its message, without JavaScript too. Every card and action of today stays; each Save sits under what it saves.
- **The owner's nine improvements** (2 Nov 2026, after the prototype's first screenshots), recorded with decision 36:
  - the model's picture (R2's picture, else the family's) as a real picture, not a thumbnail: a left column from the label down to the facts row, about 160 px tall at its own shape, with the text top-aligned beside it; about 100 px beside the name on a phone. These are the same sizes as R3's public hero. The box fits the picture's visible shape: `bp-battery-page.js` cuts a picture's transparent margins off (the demo pictures are 4:3 canvases with transparent sides), so the tall 7M gets a narrow box; without JavaScript the picture shows as it is;
  - the facts on one line each, the battery ID shortened in the middle (full in its copy button and tooltip);
  - one toolbar row: the actions on the left, the labelled icon links on the right;
  - an overview with tiles for state of charge, cycles, temperature and capacity fade, each with a small 7-day trend line drawn from the change log (inline SVG, no library), then "Nothing needs you" or what needs doing, once (task 3);
  - a slim side menu with a coloured edge on the open section, staying in view;
  - clearer words ("Documents: 6, all unchanged since they were uploaded");
  - on a phone: a section picker instead of the stacked menu, Change status and Add a note in a bar at the bottom (the page leaves room for it), and the less used facts folded behind "More details".

**Architecture:**
- **`Dpp/Pages/BatterySections.cs`** (task 1):
  - `BatterySection` (id, title, icon, hint, count) and `BatteryNav`.
  - `BatterySections.Chosen` picks the open section: `?section=`, else the card a form just sent (the controllers' `CardKey`, read with `Peek`), else the values after a values save, else the overview.
  - `BatteryPage.Sections` and `ClusterBatteryPage.Sections` list each page's menu. No controller changes.
- **The pages** (task 2):
  - Both views are rebuilt as header + `_BatteryNav` + one `<div data-section="…" hidden>` per existing card partial; each card keeps its id and its messages.
  - The windows sit at page level (a window inside a hidden section can't open): `_StatusDialog` holds "Change status" and the new "Add a note" window, taken out of `_StatusCard`. An invisible host (`bp-console-form-card bp-dialog-host`, `display: contents`) keeps the card's form styles, so R1's radio fix still applies. The cluster, publish and hand-over windows stay where they work.
  - One form per action: the Undo is only in the header's red line, and `_PassportStatusCard` points to it. The cluster admin's "Cancel the hand-over" is only in its header line, and `_HandOverCard` points to it.
  - `wwwroot/js/bp-battery-page.js` switches sections without a reload, keeps `?section=` in the address, and follows a `#link` into a hidden section.
- **CSS** (`bp-product-data.css`): the header, the menu (`.bp-edit-nav`, the old app's menu style the values editor already uses), the overview, and phones (the menu becomes two columns above the section).
- **For step 5c:** the "Upload a file" card becomes one more `BatterySection` and one more wrapper, after "Live values".

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
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2303`. After this plan: 2328. The suite takes about two to four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../r1-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.** The new styles go to `bp-product-data.css` (battery pages) and the new `bp-status.css` (every page, R3).
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## What happens to the old tests

Nothing is removed. The cards keep their ids, forms and messages. One assertion follows the move: `HandOverPagesTests` finds "Cancel the hand-over" on the cluster admin's page, not in the card (task 2).

---

### Task 1: The side menu's sections, and which one is open

Each battery page lists its sections in the order of the work, with a hint or a count. The server picks the open one, so a save comes back to its own section with its message: `?section=` if asked, else the card a form just sent (the publication, cluster and removal cards live in the overview), else the values after a values save, else the overview.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Create: `web/Dpp/Pages/BatterySections.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R4 (owner's choice B): the battery pages show one section at a time from a side menu, like the old app. The
/// server picks the open section, so a save comes back to its own section with its message.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatterySectionsTests(MongoFixture mongo)
{
    private static readonly IReadOnlyList<BatterySection> Menu =
    [
        new("overview", "Overview", "overview"), new("values", "Passport values", "values"), new("status", "Status & repairs", "swap"),
        new("live", "Live values", "live"), new("modules", "Modules", "modules"),
    ];

    [Theory]
    [InlineData("live", null, false, "live")] // asked for
    [InlineData("nonsense", null, false, "overview")] // unknown: the overview
    [InlineData(null, "status", false, "status")] // a status change or a note was just sent
    [InlineData(null, "module-list", false, "modules")] // "Type or correct the list"
    [InlineData(null, "publication", false, "overview")] // publishing, the cluster and removing live in the overview
    [InlineData(null, "cluster", false, "overview")]
    [InlineData(null, "passport-status", false, "overview")] // not on this menu
    [InlineData(null, null, true, "values")] // the values editor was just saved
    [InlineData("status", "live", true, "status")] // asking wins
    [InlineData(null, null, false, "overview")]
    public void TheOpenSection_ShouldBeAskedForElseTheOneJustSavedElseTheOverview(string? asked, string? card, bool valuesMessage, string expected)
    {
        Assert.Equal(expected, BatterySections.Chosen(Menu, asked, card, valuesMessage));
    }

    [Fact]
    public async Task TheTeamsMenu_ShouldListEveryCardInTheOrderOfTheWork()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var draft = await PublicationTesting.RigAsync(mongo);

        var published = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Sections;
        var unpublished = (await BatteryTesting.Pages(draft).BatteryAsync(draft.PackId))!.Sections;

        Assert.Equal(["overview", "values", "status", "live", "accidents", "modules", "versions", "passport-status"], published.Select(section => section.Id));
        Assert.Equal("Nothing needs you", published[0].Hint);
        Assert.Equal("Original", published.Single(section => section.Id == "status").Hint);
        Assert.Equal(1, published.Single(section => section.Id == "versions").Count);
        Assert.Equal(["overview", "values", "status", "live", "accidents", "modules", "versions"], unpublished.Select(section => section.Id)); // no passport status before publishing
        Assert.EndsWith("to do", unpublished[0].Hint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInvalidPassport_ShouldSaySoInTheMenu()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("msa@example.com"), rig.PackId, "Demo: wrong carbon footprint figures");

        var sections = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Sections;

        Assert.Equal("Invalid", sections.Single(section => section.Id == "passport-status").Hint);
    }

    [Fact]
    public async Task TheClusterAdminsMenu_ShouldHaveTheHandOverAndNoTeamSections()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId))!;

        Assert.Equal(["overview", "status", "live", "accidents", "handover", "modules", "versions"], page.Sections.Select(section => section.Id));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatterySectionsTests"`
Expected: the build fails with CS0246 (for example `error CS0246 The type or namespace name 'BatterySection' could not be found`).

- [ ] **Step 3: Write the code**

Patch `r4-task1-code.patch` (`web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`). Save the block as `../r4-task1-code.patch`, outside the worktree, and run `git apply ../r4-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index a98f7a7..fb7ece6 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -140,6 +140,37 @@ public sealed record BatteryPage(
 
     /// <summary>The passport values editor as the page shows it: read-only, with the reason, once nothing changes (redesign R1).</summary>
     public RecordEditorView Editor => Cards.FrozenNote is { } note ? Values with { FrozenNote = note } : Values;
+
+    /// <summary>What the overview asks the team to do: the missing values by group, the problems, and publishing once ready.</summary>
+    public int NeedsYou => Publication.Missing.Count + Publication.Problems.Count + (!Publication.IsPublished && Publication.Ready ? 1 : 0);
+
+    /// <summary>The page's side menu (redesign R4, option B): every card, in the order of the work, with a hint or a count.</summary>
+    public IReadOnlyList<BatterySection> Sections
+    {
+        get
+        {
+            var sections = new List<BatterySection>
+            {
+                new(BatterySections.Overview, "Overview", "overview", NeedsYou == 0 ? "Nothing needs you" : $"{Words.Count(NeedsYou, "thing")} to do"),
+                new("values", "Passport values", "values", Editor.Missing > 0 ? $"{Editor.Missing} missing" : "All filled in"),
+            };
+            if (Status is { } status)
+            {
+                sections.Add(new("status", "Status & repairs", "swap", BatterySections.StatusHint(status)));
+            }
+
+            sections.Add(new("live", "Live values", "live", BatterySections.LiveHint(Pack, Live)));
+            sections.Add(new("accidents", "Accident reports", "accident", Count: AccidentReports.Count));
+            sections.Add(new("modules", "Modules", "modules", Count: Pack.Modules.Count));
+            sections.Add(new("versions", "Versions", "versions", Count: Versions.Count));
+            if (Marks.Active || Marks.Marked)
+            {
+                sections.Add(new("passport-status", "Passport status", "shield", Marks.Marked ? DppStatuses.Words(Marks.Status!) : "Mark invalid or for deletion"));
+            }
+
+            return sections;
+        }
+    }
 }
 
 /// <summary>
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index 80de20b..bee0612 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -31,6 +31,26 @@ public sealed record ClusterBatteryPage(
 
     /// <summary>The team page's cards, without what only the team does (step 4a).</summary>
     public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false);
+
+    /// <summary>The page's side menu (redesign R4, option B), like the team's without what only the team does.</summary>
+    public IReadOnlyList<BatterySection> Sections
+    {
+        get
+        {
+            var sections = new List<BatterySection> { new(BatterySections.Overview, "Overview", "overview", HandOver.Waiting is null ? null : "A hand-over is waiting") };
+            if (Status is { } status)
+            {
+                sections.Add(new("status", "Status & repairs", "swap", BatterySections.StatusHint(status)));
+            }
+
+            sections.Add(new("live", "Live values", "live", BatterySections.LiveHint(Pack, Live)));
+            sections.Add(new("accidents", "Accident reports", "accident", Count: AccidentReports.Count));
+            sections.Add(new("handover", "Hand over", "handover", HandOver.Waiting is null ? "To another cluster" : "Waiting for their answer"));
+            sections.Add(new("modules", "Modules", "modules", Count: Pack.Modules.Count));
+            sections.Add(new("versions", "Versions", "versions", Count: Versions.Count));
+            return sections;
+        }
+    }
 }
 
 /// <summary>The "Hand over" card (mock-up S2 and S8): where its forms go, and the hand-over waiting with its receiver's name.</summary>
```

`web/Dpp/Pages/BatterySections.cs`:

```csharp
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>One entry of a battery page's side menu (redesign R4, option B): one card of the page, shown alone.</summary>
/// <param name="Id">The card's id, also its anchor and the "section" query value: "live", "status", …</param>
/// <param name="Icon">The icon's id in Views/Shared/_Icons.cshtml, without "i-".</param>
/// <param name="Hint">One line under the title, e.g. "Nothing needs you"; null when a count says it.</param>
/// <param name="Count">A number beside the title, e.g. how many accident reports; null for none.</param>
public sealed record BatterySection(string Id, string Title, string Icon, string? Hint = null, int? Count = null);

/// <summary>The side menu as a page draws it: its sections, the open one, and the page they belong to.</summary>
public sealed record BatteryNav(IReadOnlyList<BatterySection> Sections, string Chosen, string Href);

/// <summary>
/// Which section of a battery page is open (redesign R4). One at a time, like the old app's side menu. The server
/// chooses, so a save comes back to its own section with its message, with or without JavaScript.
/// </summary>
public static class BatterySections
{
    public const string Overview = "overview";

    /// <summary>
    /// The open section: the one asked for (?section=), else the one whose card a form just sent (the controllers'
    /// card key), else the passport values after a values save, else the first (the overview).
    /// </summary>
    public static string Chosen(IReadOnlyList<BatterySection> sections, string? asked, string? card, bool valuesMessage)
    {
        ArgumentNullException.ThrowIfNull(sections);
        bool Has(string? id) => id is not null && sections.Any(section => section.Id == id);
        if (Has(asked))
        {
            return asked!;
        }

        if (card is not null && Has(Of(card)))
        {
            return Of(card);
        }

        return valuesMessage && Has("values") ? "values" : sections[0].Id;
    }

    /// <summary>The section a card's messages belong to: the publication, cluster and removal cards live in the overview.</summary>
    public static string Of(string card) => card switch
    {
        "publication" or "remove" or "cluster" => Overview,
        "module-list" => "modules",
        _ => card,
    };

    /// <summary>The status section's hint: the status, and how many repairs.</summary>
    public static string StatusHint(StatusCardView status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var repairs = status.Repairs.Count(repair => !repair.IsCorrection);
        return repairs == 0 ? status.StatusWords : $"{status.StatusWords} · {Words.Count(repairs, "repair")}";
    }

    /// <summary>The live values section's hint: when the last one was measured, or that none was yet.</summary>
    public static string LiveHint(BatteryPack pack, RecordEditorView live)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(live);
        var last = live.Sections.SelectMany(section => section.Fields)
            .Select(field => pack.Values.GetValueOrDefault(field.Attribute.Property)?.GeneratedAtUtc)
            .Max();
        return last is { } at ? $"Last {ValueWords.Time(at)}" : "Nothing measured yet";
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatterySectionsTests"`
Expected: `Passed!  - Failed:     0, Passed:    13`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2316`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/BatterySections.cs web/Dpp/Pages/ClusterBatteryPages.cs
git commit -m "feat(dpp): the battery pages' sections and the one that is open" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Both battery pages: the header and the side menu

The team's and the cluster admin's battery pages become a header and a side menu showing one section at a time. Every card and form of today stays, in its section. The windows sit at page level; the notices and the one Undo are in the header.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Create: `web/Views/Batteries/_BatteryNav.cshtml`
- Modify: `web/Views/Batteries/_PassportStatusCard.cshtml`
- Modify: `web/Views/Batteries/_StatusCard.cshtml`
- Create: `web/Views/Batteries/_StatusDialog.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`
- Modify: `web/Views/ClusterAdmin/_HandOverCard.cshtml`
- Modify: `web/Views/ProductData/_RecordEditor.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`
- Create: `web/wwwroot/js/bp-battery-page.js`

- [ ] **Step 1: Write the failing tests**

Patch `r4-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`). Save the block as `../r4-task2-tests.patch`, outside the worktree, and run `git apply ../r4-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs b/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
index 66f3b24..f65161b 100644
--- a/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
@@ -153,7 +153,8 @@ public sealed class HandOverPagesTests(MongoFixture mongo)
         Assert.Contains("name=\"checkedId\"", card, StringComparison.Ordinal);
         Assert.Contains("TempData[ClusterBatteriesController.HandOverToKey] as string", card, StringComparison.Ordinal); // read once, so the window opens only right after "Find the cluster"
         Assert.DoesNotContain("TempData.Peek", card, StringComparison.Ordinal);
-        Assert.Contains("Cancel the hand-over", card, StringComparison.Ordinal);
+        Assert.Contains("Cancel the hand-over</button>", cluster, StringComparison.Ordinal); // one Cancel, on the header's line (redesign R4)
+        Assert.DoesNotContain("/cancel", card, StringComparison.Ordinal);
         Assert.Contains("foreach (var waiting in Model.Waiting)", registry, StringComparison.Ordinal);
         Assert.Contains("bp-handover-row", registry, StringComparison.Ordinal);
         Assert.Contains("Model.HandOverTo is { } receiver", battery, StringComparison.Ordinal);
```

`BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs`:

```csharp
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
            (View("Batteries", "Battery.cshtml"), new[] { "overview", "values", "status", "live", "accidents", "modules", "versions", "passport-status" }),
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
        Assert.DoesNotContain("/undo", View("Batteries", "_PassportStatusCard.cshtml"), StringComparison.Ordinal); // one Undo, in the header
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
        var trim = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-battery-page.js"));
        Assert.Contains("document.querySelector('.bp-bh-picture img')", trim, StringComparison.Ordinal);
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
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPageLayoutTests | FullyQualifiedName~HandOverPagesTests"`
Expected: the build fails with CS0117 (for example `error CS0117 'BatteryPages' does not contain a definition for 'ShortId'`).

- [ ] **Step 3: Write the code**

Patch `r4-task2-code.patch` (`web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/Batteries/_PassportStatusCard.cshtml`, `web/Views/Batteries/_StatusCard.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/Views/ClusterAdmin/_HandOverCard.cshtml`, `web/Views/ProductData/_RecordEditor.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r4-task2-code.patch`, outside the worktree, and run `git apply ../r4-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index fb7ece6..1969ecc 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -144,6 +144,9 @@ public sealed record BatteryPage(
     /// <summary>What the overview asks the team to do: the missing values by group, the problems, and publishing once ready.</summary>
     public int NeedsYou => Publication.Missing.Count + Publication.Problems.Count + (!Publication.IsPublished && Publication.Ready ? 1 : 0);
 
+    /// <summary>The header's thumbnail: the model's own picture, else its family's; null when neither has one (redesign R4).</summary>
+    public string? PictureHref => BatteryPages.PictureHref(Sources.Model, Family);
+
     /// <summary>The page's side menu (redesign R4, option B): every card, in the order of the work, with a hint or a count.</summary>
     public IReadOnlyList<BatterySection> Sections
     {
@@ -193,6 +196,22 @@ public sealed class BatteryPages(
     public const string ClusterChangedEvent = "dpp.pack-cluster-changed";
     public const string ModulesSetEvent = "dpp.pack-modules-set";
 
+    /// <summary>A battery's picture: its model's own (redesign R2), else its family's (decision 20); null when neither has one.</summary>
+    public static string? PictureHref(BatteryModel model, BatteryFamily? family)
+    {
+        ArgumentNullException.ThrowIfNull(model);
+        return model.PictureFileId is not null
+            ? $"/dpp/models/{Uri.EscapeDataString(model.Id)}/picture"
+            : family?.PictureFileId is null ? null : $"/dpp/families/{Uri.EscapeDataString(family.Id)}/picture";
+    }
+
+    /// <summary>A long ID shortened in the middle for a header ("urn:x-battery-pass:TES…3c043"); the copy button keeps it whole.</summary>
+    public static string ShortId(string id)
+    {
+        ArgumentNullException.ThrowIfNull(id);
+        return id.Length <= 36 ? id : $"{id[..24]}…{id[^8..]}";
+    }
+
     public static string BatteryHref(string packId) => $"/admin/batteries/{Uri.EscapeDataString(packId)}";
 
     /// <summary>
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index bee0612..bd883b2 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -32,6 +32,9 @@ public sealed record ClusterBatteryPage(
     /// <summary>The team page's cards, without what only the team does (step 4a).</summary>
     public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false);
 
+    /// <summary>The header's thumbnail, as on the team's page.</summary>
+    public string? PictureHref => BatteryPages.PictureHref(Sources.Model, Family);
+
     /// <summary>The page's side menu (redesign R4, option B), like the team's without what only the team does.</summary>
     public IReadOnlyList<BatterySection> Sections
     {
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index c036d92..687df3f 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -34,6 +34,15 @@
     var card = (TempData[BatteriesController.CardKey] ?? ViewData[BatteriesController.CardKey]) as string;
     var testIds = Identifiers.IsTestIdentifier(Model.Identifier) || Model.Versions.Any(row => Identifiers.IsTestIdentifier(row.Version.DigitalProductPassportId));
     var filesChanged = publication.Files.Count(file => !file.Unchanged);
+    var batteryKey = Uri.EscapeDataString(Model.Identifier);
+
+    // The side menu (redesign R4): the open section comes from ?section=, the card a form just sent, or a values save.
+    var valuesMessage = TempData.Peek(ProductDataController.EditorStatusKey) is not null || TempData.Peek(ProductDataController.EditorErrorKey) is not null
+        || TempData.Peek(ProductDataController.EditorInfoKey) is not null || ViewData[ProductDataController.EditorErrorKey] is not null;
+    var sections = Model.Sections;
+    var chosen = BatterySections.Chosen(sections, Context.Request.Query["section"], card, valuesMessage);
+    bool Hidden(string id) => id != chosen;
+    var passportBadge = StatusBadge.Passport(Model.Marks.Status).With((publication.Active ?? publication.Latest) is { } shown ? $"· version {shown.Number}" : "· only your team sees it");
 
     async Task CardMessagesAsync(string name)
     {
@@ -41,106 +50,335 @@
     }
 }
 
-<main class="bp-page bp-admin-page">
+<main class="bp-page bp-admin-page bp-battery-page">
     <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
     <nav class="bp-breadcrumb" aria-label="Breadcrumb">
         <a href="/admin/batteries">Batteries</a>
         <span aria-current="page">@Model.SerialNumber</span>
     </nav>
 
-    <header class="bp-page-heading bp-battery-heading">
-        <span class="bp-summary-title">Battery</span>
-        <div class="bp-battery-title">
-            <h1>@Model.SerialNumber</h1>
-            @if (publication.Active is { } current)
+    @* The header (redesign R4, mock-up H): the model's picture, who the battery is, its two statuses, its facts, and one
+       toolbar row with what to do and where to open it. On a phone the less used facts fold behind "More details" and the
+       actions sit in a bar at the bottom of the screen. *@
+    <header class="bp-bh">
+        @* The picture is a real picture, a column from the label down to the facts, with the text beside it
+           (owner's feedback, 2 Nov 2026); the actions and links stay below, across the header. *@
+        <div class="bp-bh-main@(Model.PictureHref is null ? " no-picture" : string.Empty)">
+            @if (Model.PictureHref is { } picture)
             {
-                <span class="bp-field-marker is-published">Published · version @current.Number</span>
+                <div class="bp-bh-picture"><img src="@picture" alt="Picture of the @sources.Model.Name battery model" /></div>
             }
-            else if (publication.Latest is { } last)
+            <div class="bp-bh-id">
+                <span class="bp-summary-title">Battery · <a href="@ProductDataPages.FamilyHref(sources.Model.FamilyId, sources.Model.Id)">@sources.Model.Name</a> of @Model.Family.Name</span>
+                <h1>@Model.SerialNumber</h1>
+                <div class="bp-badges">
+                    @if (StatusBadge.Battery(Model.Status?.Status) is { } battery)
+                    {
+                        @await Html.PartialAsync("_StatusBadge", battery)
+                    }
+                    @await Html.PartialAsync("_StatusBadge", passportBadge)
+                    @if (testIds)
+                    {
+                        @await Html.PartialAsync("_TestTag", "Test ID")
+                    }
+                    @if (Model.Status?.Predecessor is { } earlier)
+                    {
+                        <a class="bp-chip" href="@earlier.Href"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>Earlier passport</a>
+                    }
+                    @if (Model.Status?.Succession is { Linked: true } next && (next.SuccessorHref ?? next.OtherPassport) is { } newPassport)
+                    {
+                        <a class="bp-chip" href="@newPassport"><svg class="bp-i" aria-hidden="true"><use href="#i-arrow" /></svg>New passport</a>
+                    }
+                    @await Html.PartialAsync("_StatusHelp")
+                </div>
+                @if (Model.Status?.Predecessor is { } replaced)
+                {
+                    <p class="bp-lineage"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>New passport of @replaced.SerialNumber, whose passport @(replaced.Inactive ? "is now inactive" : "becomes inactive when this one is published").</p>
+                }
+                @if (Model.Status?.Succession is { Linked: true } linked && Model.Pack.Succession is { } succession)
+                {
+                    <p class="bp-lineage"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>@(StatusBadge.Battery(succession.Status)?.Words ?? succession.Status) on @ValueWords.Day(succession.StartedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))@(linked.OtherPassport is { } other ? $" by {linked.OtherOperatorInfo ?? linked.OtherOperatorId}, which issued its new passport: {other}" : "; the battery continues on its new passport").</p>
+                }
+            </div>
+            <div class="bp-bh-facts-row">
+                <dl class="bp-bh-facts">
+                    <div class="bp-bh-fact-id">
+                        <dt>Battery ID <span class="bp-attr-number">#6</span></dt>
+                        <dd><span class="bp-id-copy-row"><span class="bp-id-value" title="@Model.Identifier">@BatteryPages.ShortId(Model.Identifier)</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></dd>
+                    </div>
+                </dl>
+                <details class="bp-bh-details" open data-fold-on-phone>
+                    <summary>More details</summary>
+                    <dl class="bp-bh-facts">
+                        <div><dt>Batch</dt><dd title="@Model.BatchLabel"><a href="/admin/batches/@Uri.EscapeDataString(sources.Batch.Id)">@Model.BatchLabel</a></dd></div>
+                        <div><dt>Economic operator</dt><dd title="@sources.Operator.Name"><a href="@ProductDataPages.SitesHref("operator", sources.Operator.Id)">@sources.Operator.Name</a></dd></div>
+                        <div><dt>Cluster <button type="button" class="bp-text-button" data-open-dialog="cluster-dialog">Change</button></dt><dd class="@(Model.Pack.ClusterId is null ? "bp-batteries-muted" : null)">@Model.ClusterLabel</dd></div>
+                        @if (publication.IsPublished)
+                        {
+                            <div><dt>Made <span class="bp-attr-number">#17</span> · warranty <span class="bp-attr-number">#19</span></dt><dd>@Month(Value("manufacturingDate")) · @Period(Value("warrantyPeriod"))</dd></div>
+                        }
+                    </dl>
+                </details>
+            </div>
+        </div>
+
+        @if (Model.Marks.Marked)
+        {
+            @* Invalid or marked for deletion: the reason and the one Undo, in reach (owner's points 7 and 17). *@
+            <form method="post" action="@Model.Marks.Action/undo" class="bp-status-notice" id="mark-notice">
+                @Html.AntiForgeryToken()
+                <svg class="bp-i" aria-hidden="true"><use href="#i-shield" /></svg>
+                <span>
+                    <strong>@DppStatuses.Words(Model.Marks.Status!) since @(Model.Marks.Mark is { } mark ? ValueWords.Time(mark.AtUtc) : "—").</strong>
+                    @if (Model.Marks.Mark is { } why)
+                    {
+                        <text>Marked by @(why.ByRole == "MarketSurveillance" ? "market surveillance" : "the team") (@why.By)@(why.Reason is null ? "." : $": “{why.Reason}”.")</text>
+                    }
+                    Nothing on the passport changes until it's undone.
+                </span>
+                <button type="submit" class="bp-secondary-button bp-button-compact">Undo: make it active again</button>
+            </form>
+        }
+        @if (Model.HandOverTo is { } receiver)
+        {
+            <form method="post" action="@href/handover/cancel" class="bp-status-notice is-quiet">
+                @Html.AntiForgeryToken()
+                <svg class="bp-i" aria-hidden="true"><use href="#i-handover" /></svg>
+                <span>Hand-over to <strong>@receiver</strong> waiting for their answer.</span>
+                <button type="submit" class="bp-secondary-button bp-button-compact">Cancel it</button>
+            </form>
+        }
+        @{ await CardMessagesAsync("cluster"); }
+
+        @* One toolbar row: what to do on the left, where to open the battery on the right (owner's improvement 3). *@
+        <div class="bp-bh-bar">
+            @if ((!publication.IsPublished && publication.Ready) || Model.Status is { CanChange: true } || Model.Status is { MayAddNote: true })
             {
-                <span class="bp-field-marker">@DppStatuses.Words(last.DppStatus) · version @last.Number</span>
+                <div class="bp-bh-actions" data-action-bar>
+                    @if (!publication.IsPublished && publication.Ready)
+                    {
+                        <button type="button" class="bp-primary-button bp-with-icon" data-open-dialog="publish-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-publish" /></svg>Publish passport</button>
+                    }
+                    @if (Model.Status is { CanChange: true })
+                    {
+                        <button type="button" class="bp-primary-button bp-with-icon" data-open-dialog="status-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-swap" /></svg>Change status</button>
+                    }
+                    @if (Model.Status is { MayAddNote: true })
+                    {
+                        <button type="button" class="bp-secondary-button bp-with-icon" data-open-dialog="note-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-note" /></svg>Add a note</button>
+                    }
+                </div>
             }
-            else
+            @if ((sections.Any(section => section.Id == "passport-status") && !Model.Marks.Marked) || !publication.IsPublished)
             {
-                <span class="bp-field-marker">Draft · only your team sees it</span>
+                <details class="bp-more">
+                    <summary class="bp-secondary-button bp-with-icon"><svg class="bp-i" aria-hidden="true"><use href="#i-more" /></svg>More</summary>
+                    <div class="bp-more-list">
+                        @if (sections.Any(section => section.Id == "passport-status") && !Model.Marks.Marked)
+                        {
+                            <a href="@href?section=passport-status" class="is-danger"><svg class="bp-i" aria-hidden="true"><use href="#i-alert" /></svg><span>Mark as invalid or for deletion…<small>Freezes the passport; every page says so</small></span></a>
+                        }
+                        @if (!publication.IsPublished)
+                        {
+                            <a href="@href?section=overview#remove" class="is-danger"><svg class="bp-i" aria-hidden="true"><use href="#i-trash" /></svg><span>Remove this draft battery…<small>Only while nothing is published</small></span></a>
+                        }
+                    </div>
+                </details>
             }
+            <nav class="bp-bh-open" aria-label="This battery elsewhere">
+                @if (publication.IsPublished)
+                {
+                    <a class="bp-ilink" href="/dpp/@batteryKey" target="_blank" rel="noreferrer" title="The public summary, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
+                    <a class="bp-ilink" href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer" title="Everything its readers see, by who they are"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
+                }
+                <a class="bp-ilink" href="/qr/@batteryKey/svg" target="_blank" rel="noreferrer" title="@(publication.IsPublished ? "Opens the public summary" : "Opens the public summary once the passport is published")"><svg class="bp-i" aria-hidden="true"><use href="#i-qr" /></svg>QR code</a>
+                <a class="bp-ilink" href="/qr/@batteryKey/download" title="Save the QR code as a picture"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Save QR</a>
+                <a class="bp-ilink" href="/admin/audit?q=@Uri.EscapeDataString(Model.Pack.Id)" title="Every change to this battery, in the audit log"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>History</a>
+            </nav>
         </div>
-        <p class="mb-0">
-            Model <a href="@ProductDataPages.FamilyHref(sources.Model.FamilyId, sources.Model.Id)">@sources.Model.Name</a> of @Model.Family.Name,
-            made at @sources.Site.Name in @sources.Batch.Year.
-        </p>
-        @{
-            var batteryKey = Uri.EscapeDataString(Model.Identifier);
-        }
-        <nav class="bp-battery-links" aria-label="This battery elsewhere">
-            @if (publication.IsPublished)
-            {
-                <a href="/dpp/@batteryKey" target="_blank" rel="noreferrer">Public summary</a>
-                <a href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer">Detail page</a>
-            }
-            <a href="/qr/@batteryKey/svg" target="_blank" rel="noreferrer">QR code</a>
-            <a href="/qr/@batteryKey/download">Download QR code</a>
-            <a href="/admin/audit?q=@Uri.EscapeDataString(Model.Pack.Id)">Audit entries</a>
-            @if (!publication.IsPublished)
+    </header>
+
+    @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
+
+    <div class="bp-bpage">
+        @await Html.PartialAsync("~/Views/Batteries/_BatteryNav.cshtml", new BatteryNav(sections, chosen, href))
+        <div class="bp-bpanel">
+            @* Overview: today's Publication card, whole, as what needs you first. *@
+            <div data-section="overview" hidden="@Hidden("overview")">
+                <section class="bp-card bp-console-form-card bp-battery-card" id="publication">
+                    <div class="bp-battery-card-head">
+                        <div>
+                            <span class="bp-summary-title">Overview</span>
+                            @if (publication.Active is { } active)
+                            {
+                                <h2>Published since @When(active.PublishedAtUtc)</h2>
+                                <p class="mb-0">Version @active.Number is live. A change to its values publishes a new version, and you're asked first; live values and accident reports update it in place.</p>
+                            }
+                            else if (publication.Latest is { } last)
+                            {
+                                <h2>@DppStatuses.Words(last.DppStatus) since @When(last.LastUpdatedUtc)</h2>
+                                <p class="mb-0">Version @last.Number is its last. Nothing on this passport changes any more; it stays readable by its passport and battery IDs.</p>
+                            }
+                            else if (publication.Ready)
+                            {
+                                <h2>Ready to publish</h2>
+                                <p class="mb-0">Every value the passport needs by February 2027 is there. Publishing makes version 1, signs it and shows it to its readers.</p>
+                            }
+                            else
+                            {
+                                <h2>Not ready to publish yet</h2>
+                                <p class="mb-0"><span class="bp-missing-note">@Words.Count(publication.MissingCount, "value") missing</span> that the passport needs by February 2027.</p>
+                            }
+                        </div>
+                        @if (!publication.IsPublished && publication.Ready)
+                        {
+                            <button type="button" class="bp-primary-button text-nowrap" data-open-dialog="publish-dialog">Publish passport</button>
+                        }
+                    </div>
+                    @{ await CardMessagesAsync("publication"); }
+
+                    @if (Model.NeedsYou == 0 && publication.IsPublished)
+                    {
+                        <ul class="bp-todo"><li class="is-none">Nothing needs you.</li></ul>
+                    }
+                    @if (publication.Missing.Count > 0)
+                    {
+                        <ul class="bp-missing-groups">
+                            @foreach (var group in publication.Missing)
+                            {
+                                <li>
+                                    <div class="bp-missing-group-head">
+                                        <strong>@group.Record</strong>
+                                        <a href="@group.Href">Fill in</a>
+                                    </div>
+                                    <span>@string.Join(", ", group.Attributes.Select(attribute => $"{attribute.Label} {attribute.Name}"))</span>
+                                </li>
+                            }
+                        </ul>
+                    }
+                    @if (publication.Problems.Count > 0)
+                    {
+                        <ul class="bp-missing-groups">
+                            @foreach (var problem in publication.Problems)
+                            {
+                                <li><span class="text-danger">@problem</span></li>
+                            }
+                        </ul>
+                    }
+
+                    <dl class="bp-overview">
+                        @if (Model.Status is { } status)
+                        {
+                            <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-swap" /></svg>Battery status</dt><dd>@status.StatusWords<small>@(status.Since is { } since ? $"since {ValueWords.Time(since)}{(status.AfterRepair ? ", after a repair" : string.Empty)}" : "since it was made")</small></dd></div>
+                        }
+                        <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-live" /></svg>Live values</dt><dd>@BatterySections.LiveHint(Model.Pack, Model.Live)<small>Measured on the battery</small></dd></div>
+                        <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-accident" /></svg>Accident reports</dt><dd>@(Model.AccidentReports.Count == 0 ? "None reported" : Words.Count(Model.AccidentReports.Count, "report"))</dd></div>
+                    </dl>
+
+                    <div class="bp-battery-checks">
+                        @if (publication.NeededLater.Count > 0)
+                        {
+                            <details>
+                                <summary>@Words.Count(publication.NeededLater.Count, "value") needed only after February 2027</summary>
+                                <p class="mb-0">@string.Join(", ", publication.NeededLater.Select(attribute => $"{attribute.Label} {attribute.Name}"))</p>
+                            </details>
+                        }
+                        @if (publication.Files.Count == 0)
+                        {
+                            <p class="mb-0 bp-batteries-muted">No uploaded files yet.</p>
+                        }
+                        else
+                        {
+                            <details open="@(filesChanged > 0)">
+                                <summary>
+                                    @if (filesChanged == 0)
+                                    {
+                                        <span class="bp-done-note">Uploaded files: @publication.Files.Count of @publication.Files.Count unchanged</span>
+                                    }
+                                    else
+                                    {
+                                        <span class="bp-missing-note">Uploaded files: @filesChanged changed since upload</span>
+                                    }
+                                </summary>
+                                <ul class="bp-file-checks">
+                                    @foreach (var file in publication.Files)
+                                    {
+                                        <li>
+                                            <span>@file.Document.FileName · @file.Owner</span>
+                                            @if (file.Unchanged)
+                                            {
+                                                <span class="bp-done-note">Unchanged</span>
+                                            }
+                                            else
+                                            {
+                                                <span class="bp-missing-note">Changed or missing: its SHA-256 no longer matches</span>
+                                            }
+                                        </li>
+                                    }
+                                </ul>
+                                <small class="bp-field-help">Each file is checked against the SHA-256 fingerprint taken when it was uploaded.</small>
+                            </details>
+                        }
+                    </div>
+                </section>
+
+                @if (!publication.IsPublished)
+                {
+                    <details class="bp-card bp-remove-record mt-4" id="remove" open="@(card == "remove")">
+                        <summary>Remove this battery</summary>
+                        @{ await CardMessagesAsync("remove"); }
+                        @if (Model.Removal.Blocked is { } blocked)
+                        {
+                            <p class="mb-0 mt-2">It can't be removed: @blocked</p>
+                        }
+                        else
+                        {
+                            <form method="post" action="@Model.Removal.Action" class="d-flex flex-wrap align-items-center gap-3 mt-2">
+                                @Html.AntiForgeryToken()
+                                <span>It's a draft, so nothing has been published. Removing it deletes it with its accident reports; this can't be undone.</span>
+                                <button type="submit" class="bp-danger-button bp-button-compact text-nowrap">Remove draft battery</button>
+                            </form>
+                        }
+                    </details>
+                }
+            </div>
+
+            <div data-section="values" hidden="@Hidden("values")" id="values">
+                @await Html.PartialAsync("~/Views/ProductData/_RecordEditor.cshtml", Model.Editor)
+                @if (publication.IsPublished)
+                {
+                    <p class="bp-battery-fixed-note">The serial number, manufacturing date and warranty are fixed now that the passport is published; they're shown at the top.</p>
+                }
+            </div>
+
+            @if (Model.Status is { } statusCard)
             {
-                <span class="bp-batteries-muted">The QR code opens the public summary once the passport is published.</span>
+                <div data-section="status" hidden="@Hidden("status")">
+                    @await Html.PartialAsync("~/Views/Batteries/_StatusCard.cshtml", statusCard)
+                </div>
             }
-        </nav>
-        <dl class="bp-battery-facts">
-            <div class="bp-battery-fact-wide">
-                <dt>Battery ID <span class="bp-attr-number">#6</span></dt>
-                <dd><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Identifier</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></dd>
+            <div data-section="live" hidden="@Hidden("live")">
+                @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
             </div>
-            <div>
-                <dt>Batch</dt>
-                <dd><a href="/admin/batches/@Uri.EscapeDataString(sources.Batch.Id)">@Model.BatchLabel</a></dd>
+            <div data-section="accidents" hidden="@Hidden("accidents")">
+                @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
             </div>
-            <div>
-                <dt>Economic operator</dt>
-                <dd><a href="@ProductDataPages.SitesHref("operator", sources.Operator.Id)">@sources.Operator.Name</a></dd>
+            <div data-section="modules" hidden="@Hidden("modules")">
+                @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
             </div>
-            <div>
-                <dt>Cluster</dt>
-                <dd>
-                    <span class="@(Model.Pack.ClusterId is null ? "bp-batteries-muted" : null)">@Model.ClusterLabel</span>
-                    <button type="button" class="bp-text-button" data-open-dialog="cluster-dialog">Change</button>
-                    @if (Model.HandOverTo is { } receiver)
-                    {
-                        <form method="post" action="@href/handover/cancel" class="d-block">
-                            @Html.AntiForgeryToken()
-                            <small class="bp-field-help">Hand-over to @receiver waiting for their answer.</small>
-                            <button type="submit" class="bp-text-button">Cancel it</button>
-                        </form>
-                    }
-                </dd>
+            <div data-section="versions" hidden="@Hidden("versions")">
+                @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
             </div>
-            @if (publication.IsPublished)
+            @if (Model.Marks is { } marks && (marks.Active || marks.Marked))
             {
-                <div>
-                    <dt>Serial number <span class="bp-attr-number">#11</span></dt>
-                    <dd>@Value("batterySerialNumber")</dd>
-                </div>
-                <div>
-                    <dt>Manufacturing date <span class="bp-attr-number">#17</span></dt>
-                    <dd>@Month(Value("manufacturingDate"))</dd>
-                </div>
-                <div>
-                    <dt>Warranty <span class="bp-attr-number">#19</span></dt>
-                    <dd>@Period(Value("warrantyPeriod"))</dd>
+                <div data-section="passport-status" hidden="@Hidden("passport-status")">
+                    @await Html.PartialAsync("~/Views/Batteries/_PassportStatusCard.cshtml", marks)
                 </div>
             }
-        </dl>
-        @{ await CardMessagesAsync("cluster"); }
-    </header>
-
-    @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
-    @if (testIds)
-    {
-        <p class="alert alert-secondary bp-status-line is-info mt-3" role="note">
-            <span>Test identifiers: this battery's IDs start with TEST-. Real ones come once the identifier scheme is chosen (design 10).</span>
-        </p>
-    }
+        </div>
+    </div>
 
+    @* The windows, at page level: one inside a hidden section can't open. *@
     <dialog id="cluster-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="cluster-dialog-title">
         <form method="post" action="@href/cluster">
             @Html.AntiForgeryToken()
@@ -163,110 +401,6 @@
         </form>
     </dialog>
 
-    <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="publication">
-        <div class="bp-battery-card-head">
-            <div>
-                <span class="bp-summary-title">Publication</span>
-                @if (publication.Active is { } active)
-                {
-                    <h2>Published since @When(active.PublishedAtUtc)</h2>
-                    <p class="mb-0">Version @active.Number is live. A change to its values publishes a new version, and you're asked first; live values and accident reports update it in place.</p>
-                }
-                else if (publication.Latest is { } last)
-                {
-                    <h2>@DppStatuses.Words(last.DppStatus) since @When(last.LastUpdatedUtc)</h2>
-                    <p class="mb-0">Version @last.Number is its last. Nothing on this passport changes any more; it stays readable by its passport and battery IDs.</p>
-                }
-                else if (publication.Ready)
-                {
-                    <h2>Ready to publish</h2>
-                    <p class="mb-0">Every value the passport needs by February 2027 is there. Publishing makes version 1, signs it and shows it to its readers.</p>
-                }
-                else
-                {
-                    <h2>Not ready to publish yet</h2>
-                    <p class="mb-0"><span class="bp-missing-note">@Words.Count(publication.MissingCount, "value") missing</span> that the passport needs by February 2027.</p>
-                }
-            </div>
-            @if (!publication.IsPublished && publication.Ready)
-            {
-                <button type="button" class="bp-primary-button text-nowrap" data-open-dialog="publish-dialog">Publish passport</button>
-            }
-        </div>
-        @{ await CardMessagesAsync("publication"); }
-
-        @if (publication.Missing.Count > 0)
-        {
-            <ul class="bp-missing-groups">
-                @foreach (var group in publication.Missing)
-                {
-                    <li>
-                        <div class="bp-missing-group-head">
-                            <strong>@group.Record</strong>
-                            <a href="@group.Href">Fill in</a>
-                        </div>
-                        <span>@string.Join(", ", group.Attributes.Select(attribute => $"{attribute.Label} {attribute.Name}"))</span>
-                    </li>
-                }
-            </ul>
-        }
-        @if (publication.Problems.Count > 0)
-        {
-            <ul class="bp-missing-groups">
-                @foreach (var problem in publication.Problems)
-                {
-                    <li><span class="text-danger">@problem</span></li>
-                }
-            </ul>
-        }
-
-        <div class="bp-battery-checks">
-            @if (publication.NeededLater.Count > 0)
-            {
-                <details>
-                    <summary>@Words.Count(publication.NeededLater.Count, "value") needed only after February 2027</summary>
-                    <p class="mb-0">@string.Join(", ", publication.NeededLater.Select(attribute => $"{attribute.Label} {attribute.Name}"))</p>
-                </details>
-            }
-            @if (publication.Files.Count == 0)
-            {
-                <p class="mb-0 bp-batteries-muted">No uploaded files yet.</p>
-            }
-            else
-            {
-                <details open="@(filesChanged > 0)">
-                    <summary>
-                        @if (filesChanged == 0)
-                        {
-                            <span class="bp-done-note">Uploaded files: @publication.Files.Count of @publication.Files.Count unchanged</span>
-                        }
-                        else
-                        {
-                            <span class="bp-missing-note">Uploaded files: @filesChanged changed since upload</span>
-                        }
-                    </summary>
-                    <ul class="bp-file-checks">
-                        @foreach (var file in publication.Files)
-                        {
-                            <li>
-                                <span>@file.Document.FileName · @file.Owner</span>
-                                @if (file.Unchanged)
-                                {
-                                    <span class="bp-done-note">Unchanged</span>
-                                }
-                                else
-                                {
-                                    <span class="bp-missing-note">Changed or missing: its SHA-256 no longer matches</span>
-                                }
-                            </li>
-                        }
-                    </ul>
-                    <small class="bp-field-help">Each file is checked against the SHA-256 fingerprint taken when it was uploaded.</small>
-                </details>
-            }
-        </div>
-    </section>
-
     @if (!publication.IsPublished && publication.Ready)
     {
         <dialog id="publish-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="publish-dialog-title">
@@ -285,50 +419,14 @@
         </dialog>
     }
 
-    <div id="values">
-        @await Html.PartialAsync("~/Views/ProductData/_RecordEditor.cshtml", Model.Editor)
-    </div>
-    @if (publication.IsPublished)
-    {
-        <p class="bp-battery-fixed-note">The serial number, manufacturing date and warranty are fixed now that the passport is published; they're shown at the top.</p>
-    }
-
-    @if (Model.Status is { } status)
-    {
-        @await Html.PartialAsync("~/Views/Batteries/_StatusCard.cshtml", status)
-    }
-    @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
-    @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
-    @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
-    @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
-
-    @if (Model.Marks is { } marks && (marks.Active || marks.Marked))
+    @if (Model.Status is { } statusWindows)
     {
-        @await Html.PartialAsync("~/Views/Batteries/_PassportStatusCard.cshtml", marks)
-    }
-
-    @if (!publication.IsPublished)
-    {
-    <details class="bp-card bp-remove-record mt-4" id="remove" open="@(card == "remove")">
-        <summary>Remove this battery</summary>
-        @{ await CardMessagesAsync("remove"); }
-        @if (Model.Removal.Blocked is { } blocked)
-        {
-            <p class="mb-0 mt-2">It can't be removed: @blocked</p>
-        }
-        else
-        {
-            <form method="post" action="@Model.Removal.Action" class="d-flex flex-wrap align-items-center gap-3 mt-2">
-                @Html.AntiForgeryToken()
-                <span>It's a draft, so nothing has been published. Removing it deletes it with its accident reports; this can't be undone.</span>
-                <button type="submit" class="bp-danger-button bp-button-compact text-nowrap">Remove draft battery</button>
-            </form>
-        }
-    </details>
+        @await Html.PartialAsync("~/Views/Batteries/_StatusDialog.cshtml", statusWindows)
     }
 </main>
 
 @section Scripts {
     <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
     <script src="~/js/bp-status-change.js" asp-append-version="true"></script>
+    <script src="~/js/bp-battery-page.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/Batteries/_PassportStatusCard.cshtml b/web/Views/Batteries/_PassportStatusCard.cshtml
index 7a1a6ae..553d918 100644
--- a/web/Views/Batteries/_PassportStatusCard.cshtml
+++ b/web/Views/Batteries/_PassportStatusCard.cshtml
@@ -1,24 +1,22 @@
 @using BatteryPassWeb.Dpp.Pages
 @using BatteryPassWeb.Dpp.Store
 @model PassportMarkView
-@* The team's "Passport status" card (step 4c-2, mock-up S5): mark the passport invalid or for deletion, or undo it. *@
-<details class="bp-card bp-danger-zone mt-4" id="passport-status" open="@(Model.Marked)">
-    <summary><strong>Passport status</strong> <span class="text-secondary">· @DppStatuses.Words(Model.Status ?? "draft").ToLowerInvariant()</span></summary>
+@* The team's "Passport status" section (step 4c-2, mock-up S5; redesign R4): mark the passport invalid or for deletion.
+   Once marked, the header's red line carries the reason and the one Undo button (owner's point 17). *@
+<section class="bp-card bp-console-form-card bp-danger-zone" id="passport-status">
+    <span class="bp-summary-title">Passport status <span class="bp-attr-number">#2</span></span>
+    <div class="bp-battery-status mb-2">@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(Model.Status))</div>
     @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "passport-status")
     @if (Model.Marked)
     {
-        <p class="mt-2 mb-2">
+        <p class="mb-1">
             <strong>@DppStatuses.Words(Model.Status!) since @(Model.Mark is { } mark ? ValueWords.Time(mark.AtUtc) : "—").</strong>
             @if (Model.Mark is { } why)
             {
                 <span>Marked by @(why.ByRole == "MarketSurveillance" ? "market surveillance" : "the team") (@why.By)@(why.Reason is null ? "." : $": {why.Reason}")</span>
             }
         </p>
-        <form method="post" action="@Model.Action/undo">
-            @Html.AntiForgeryToken()
-            <div class="bp-danger-actions"><button type="submit" class="bp-secondary-button">Undo: make the passport active again</button></div>
-            <small class="bp-field-help d-block">The reason stays in the audit log.</small>
-        </form>
+        <p class="bp-help-text mb-0">To undo it, use <strong>Undo</strong> on the red line at the top of the page. The reason stays in the audit log.</p>
     }
     else
     {
@@ -41,4 +39,4 @@
             </div>
         </form>
     }
-</details>
+</section>
diff --git a/web/Views/Batteries/_StatusCard.cshtml b/web/Views/Batteries/_StatusCard.cshtml
index cc1c82c..2f929af 100644
--- a/web/Views/Batteries/_StatusCard.cshtml
+++ b/web/Views/Batteries/_StatusCard.cshtml
@@ -1,13 +1,16 @@
-@using BatteryPassWeb.Dpp.Editing
 @using BatteryPassWeb.Dpp.Pages
 @model StatusCardView
-@* The battery status card and its "Change status" window (step 4b, mock-up S2 and S3), on both battery pages. *@
-<section class="bp-card bp-console-form-card bp-battery-card mt-4" id="status">
+@* The battery status card (step 4b, mock-up S2), on both battery pages: the "Status & repairs" section of the side menu
+   (redesign R4). Its windows are at page level (_StatusDialog); an earlier or new passport shows in the header. *@
+<section class="bp-card bp-console-form-card bp-battery-card" id="status">
     <div class="bp-battery-card-head">
         <div>
             <span class="bp-summary-title">Battery status <span class="bp-attr-number">#22</span></span>
             <div class="bp-battery-status">
-                <strong>@Model.StatusWords</strong>
+                @if (StatusBadge.Battery(Model.Status) is { } badge)
+                {
+                    @await Html.PartialAsync("_StatusBadge", badge)
+                }
                 @if (Model.Since is { } since)
                 {
                     <span class="text-secondary">since @ValueWords.Time(since)@(Model.AfterRepair ? ", after a repair" : string.Empty)</span>
@@ -15,29 +18,22 @@
             </div>
             <p class="mb-0">@(Model.WhyNot ?? (Model.Status == "waste" ? "The battery is waste, which is final. Mistyped values can still be corrected." : "A status change also re-states the fades and the expected lifetime, and publishes a new version of this passport."))</p>
         </div>
-        @if (Model.CanChange)
-        {
-            <button type="button" class="bp-primary-button" data-open-dialog="status-dialog">Change status</button>
-        }
+        <div class="d-flex flex-wrap gap-2">
+            @if (Model.MayAddNote)
+            {
+                <button type="button" class="bp-secondary-button bp-with-icon" data-open-dialog="note-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-note" /></svg>Add a note</button>
+            }
+            @if (Model.CanChange)
+            {
+                <button type="button" class="bp-primary-button bp-with-icon" data-open-dialog="status-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-swap" /></svg>Change status</button>
+            }
+        </div>
     </div>
     @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "status")
-    @if (Model.Predecessor is { } earlier)
-    {
-        <p class="bp-passport-notice mt-3"><strong>New passport of an earlier battery.</strong><span>It replaces @earlier.SerialNumber, @(earlier.Inactive ? "whose passport is now inactive." : "which becomes inactive when this one is published.")</span><a href="@earlier.Href">Open the earlier battery</a></p>
-    }
-    @if (Model.Succession is { } next)
+    @if (Model.Succession is { Linked: false } next)
     {
         <div class="mt-3 pt-3 border-top" id="successor">
-            @if (next.Linked)
-            {
-                <p class="mb-0"><strong>This passport is inactive.</strong> The battery continues on its new passport@(next.OtherPassport is { } other ? $", issued by {next.OtherOperatorInfo ?? next.OtherOperatorId}: {other}" : ".")
-                    @if (next.SuccessorHref is { } newPage)
-                    {
-                        <a href="@newPage">Open the new passport</a>
-                    }
-                </p>
-            }
-            else if (next.SuccessorMade)
+            @if (next.SuccessorMade)
             {
                 <p class="mb-2"><strong>The new passport is @(next.SuccessorPublished ? "published" : "a draft").</strong> This passport becomes inactive once it is published.
                     <a href="@next.SuccessorHref">Open the new battery</a>
@@ -77,7 +73,7 @@
                     </div>
                 </form>
             }
-            @if (Model.ForTeam && !next.Linked && !next.SuccessorPublished)
+            @if (Model.ForTeam && !next.SuccessorPublished)
             {
                 <form method="post" action="@next.Action/cancel" class="mt-3">
                     @Html.AntiForgeryToken()
@@ -105,64 +101,4 @@
         </ul>
     }
     <small class="bp-field-help d-block mt-2">Seen by the battery's cluster, market surveillance and @(Model.ForTeam ? "your team" : "the team"), with the status.</small>
-    @if (Model.MayAddNote)
-    {
-        <details class="bp-module-edit mt-2">
-            <summary>Add a note to the history</summary>
-            <form method="post" action="@Model.NoteAction" class="bp-dialog-form mt-2">
-                @Html.AntiForgeryToken()
-                <label><span>Entries are never changed or removed. To correct one, add a note; it shows as "Correction".</span><input type="text" name="note" maxlength="@StatusChangeService.MaxDescription" placeholder="e.g. The repair of 20 Oct was on module 5, not module 4." /></label>
-                <div class="bp-live-save"><span></span><button type="submit" class="bp-secondary-button">Add note</button></div>
-            </form>
-        </details>
-    }
-
-    @if (Model.CanChange)
-    {
-        <dialog id="status-dialog" class="bp-modal-panel bp-dialog bp-dialog-wide" aria-labelledby="status-dialog-title" data-status-dialog>
-            <form method="post" action="@Model.Restated.SaveAction" class="bp-dialog-form">
-                @Html.AntiForgeryToken()
-                <div data-status-step="1">
-                    <h2 id="status-dialog-title">Change the battery status</h2>
-                    <p class="bp-help-text">Now: <strong>@Model.Status</strong>. What happened to the battery?</p>
-                    <div class="bp-choice-list">
-                        @foreach (var (what, label, help) in Model.Choices)
-                        {
-                            <label class="bp-choice"><input type="radio" name="what" value="@what" checked="@(what == Model.Choices[0].What)" data-status-choice="@label" /><strong>@label</strong><small>@help</small></label>
-                        }
-                    </div>
-                    <div class="bp-dialog-actions">
-                        <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
-                        <button type="button" class="bp-primary-button" data-status-next>Next</button>
-                    </div>
-                </div>
-                <div data-status-step="2">
-                    <h2><span data-status-chosen>@Model.Choices[0].Label</span>: re-state the values</h2>
-                    <p class="bp-help-text">Check each value; they start as the battery's current ones.</p>
-                    <div data-status-repair>
-                        <label><span>Day of the repair</span><input type="date" name="repairDate" value="@Model.Today" max="@Model.Today" /></label>
-                        <label><span>What was repaired</span><input type="text" name="repairDescription" maxlength="@StatusChangeService.MaxDescription" placeholder="e.g. Module 4 replaced after a coolant leak" /></label>
-                    </div>
-                    <div class="bp-attr-list">
-                        @foreach (var field in Model.Restated.Sections.SelectMany(section => section.Fields))
-                        {
-                            @await Html.PartialAsync("~/Views/ProductData/_EditorField.cshtml", new EditorFieldView(Model.Restated, field))
-                            @if (field.Attribute.Number == 59 && Model.RatedCapacityAh is { } rated)
-                            {
-                                <div class="bp-suggest" data-fade-suggest data-rated="@rated.ToString(System.Globalization.CultureInfo.InvariantCulture)">
-                                    <label><span>Measured remaining capacity (Ah)</span><input type="text" inputmode="decimal" data-fade-remaining /></label>
-                                    <button type="button" class="bp-text-button" data-fade-button>Suggest the fade</button>
-                                    <small class="bp-field-help" data-fade-note>1 − remaining / @ValueWords.Number(rated) Ah rated. The measured capacity isn't stored.</small>
-                                </div>
-                            }
-                        }
-                    </div>
-                    <div class="bp-dialog-actions">
-                        <button type="button" class="bp-secondary-button" data-status-back>Back</button>
-                        <button type="submit" class="bp-primary-button">Save and publish a new version</button>
-                    </div>
-                </div>
-            </form>
-        </dialog>
-    }
 </section>
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 15a887e..7dca582 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -1,66 +1,160 @@
+@using BatteryPassWeb.Controllers
+@using BatteryPassWeb.Dpp.Identifiers
 @using BatteryPassWeb.Dpp.Pages
+@inject IIdentifierScheme Identifiers
 @model ClusterBatteryPage
-@* The cluster admin's battery page (step 4a, mock-up S2): the team page's shared cards, without what only the team does. *@
+@* The cluster admin's battery page (step 4a, mock-up S2), in the team page's layout (redesign R4, mock-up F1): the same
+   header and side menu, without what only the team does. *@
 @{
     ViewData["Title"] = $"Battery {Model.SerialNumber}";
     var active = Model.Versions.Select(row => row.Version).FirstOrDefault(version => version.DppStatus == BatteryPassWeb.Dpp.Store.DppStatuses.Active);
     var batteryKey = Uri.EscapeDataString(Model.Identifier);
+    var card = (TempData.Peek(BatteriesController.CardKey) ?? ViewData[BatteriesController.CardKey]) as string;
+    var sections = Model.Sections;
+    var chosen = BatterySections.Chosen(sections, Context.Request.Query["section"], card, valuesMessage: false);
+    bool Hidden(string id) => id != chosen;
+    var testIds = Identifiers.IsTestIdentifier(Model.Identifier);
 }
 
-<main class="bp-page bp-admin-page">
+<main class="bp-page bp-admin-page bp-battery-page">
     <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
     <nav class="bp-breadcrumb" aria-label="Breadcrumb">
         <a href="/registry">Registry</a>
         <span aria-current="page">@Model.SerialNumber</span>
     </nav>
 
-    <header class="bp-page-heading bp-battery-heading">
-        <span class="bp-summary-title">Battery</span>
-        <div class="bp-battery-title">
-            <h1>@Model.SerialNumber</h1>
-            @if (active is not null)
+    <header class="bp-bh">
+        @* The picture is a real picture, a column from the label down to the facts, with the text beside it
+           (owner's feedback, 2 Nov 2026); the actions and links stay below, across the header. *@
+        <div class="bp-bh-main@(Model.PictureHref is null ? " no-picture" : string.Empty)">
+            @if (Model.PictureHref is { } picture)
             {
-                <span class="bp-field-marker is-published">Published · version @active.Number</span>
+                <div class="bp-bh-picture"><img src="@picture" alt="Picture of the @Model.Sources.Model.Name battery model" /></div>
             }
+            <div class="bp-bh-id">
+                <span class="bp-summary-title">Battery · @Model.Sources.Model.Name of @Model.Family.Name</span>
+                <h1>@Model.SerialNumber</h1>
+                <div class="bp-badges">
+                    @if (StatusBadge.Battery(Model.Status?.Status) is { } battery)
+                    {
+                        @await Html.PartialAsync("_StatusBadge", battery)
+                    }
+                    @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(active?.DppStatus).With(active is null ? null : $"· version {active.Number}"))
+                    @if (testIds)
+                    {
+                        @await Html.PartialAsync("_TestTag", "Test ID")
+                    }
+                    @await Html.PartialAsync("_StatusHelp")
+                </div>
+                @if (Model.Status?.Predecessor is { } replaced)
+                {
+                    <p class="bp-lineage"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>New passport of @replaced.SerialNumber. Its <a href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer">detail page</a> links the earlier one.</p>
+                }
+            </div>
+            <div class="bp-bh-facts-row">
+                <dl class="bp-bh-facts">
+                    <div class="bp-bh-fact-id">
+                        <dt>Battery ID <span class="bp-attr-number">#6</span></dt>
+                        <dd><span class="bp-id-copy-row"><span class="bp-id-value" title="@Model.Identifier">@BatteryPages.ShortId(Model.Identifier)</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></dd>
+                    </div>
+                </dl>
+                <details class="bp-bh-details" open data-fold-on-phone>
+                    <summary>More details</summary>
+                    <dl class="bp-bh-facts">
+                        <div><dt>Batch</dt><dd title="@Model.BatchLabel">@Model.BatchLabel</dd></div>
+                        <div><dt>Cluster</dt><dd title="@Model.ClusterLabel">@Model.ClusterLabel</dd></div>
+                    </dl>
+                </details>
+            </div>
+        </div>
+
+        @* The page opens only while the passport is active (step 4a): an invalid or inactive battery shows on the Registry
+           row and the detail page instead. A waiting hand-over is one line here, with its one Cancel (redesign R4). *@
+        @if (Model.HandOver.Waiting is { } waiting)
+        {
+            <form method="post" action="@Model.HandOver.Action/cancel" class="bp-status-notice is-quiet">
+                @Html.AntiForgeryToken()
+                <svg class="bp-i" aria-hidden="true"><use href="#i-handover" /></svg>
+                <span>Hand-over to <strong>@Model.HandOver.ReceiverName</strong> waiting for their answer, asked @ValueWords.Time(waiting.AskedAtUtc).</span>
+                <button type="submit" class="bp-secondary-button bp-button-compact">Cancel the hand-over</button>
+            </form>
+        }
+        <div class="bp-bh-bar">
+            @if (Model.Status is { CanChange: true } || Model.Status is { MayAddNote: true })
+            {
+                <div class="bp-bh-actions" data-action-bar>
+                    @if (Model.Status is { CanChange: true })
+                    {
+                        <button type="button" class="bp-primary-button bp-with-icon" data-open-dialog="status-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-swap" /></svg>Change status</button>
+                    }
+                    @if (Model.Status is { MayAddNote: true })
+                    {
+                        <button type="button" class="bp-secondary-button bp-with-icon" data-open-dialog="note-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-note" /></svg>Add a note</button>
+                    }
+                </div>
+            }
+            <a class="bp-secondary-button bp-with-icon" href="@Model.Href?section=handover"><svg class="bp-i" aria-hidden="true"><use href="#i-handover" /></svg>Hand over…</a>
+            <nav class="bp-bh-open" aria-label="This battery elsewhere">
+                <a class="bp-ilink" href="/dpp/@batteryKey" target="_blank" rel="noreferrer" title="The public summary, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
+                <a class="bp-ilink" href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer" title="Everything your cluster sees"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
+                <a class="bp-ilink" href="@ReaderPages.AccidentsHref(Model.Pack.Id)" title="The battery's accident reports, as readers see them"><svg class="bp-i" aria-hidden="true"><use href="#i-accident" /></svg>Accident page</a>
+            </nav>
         </div>
-        <p class="mb-0">Model @Model.Sources.Model.Name of @Model.Family.Name, made at @Model.Sources.Site.Name in @Model.Sources.Batch.Year.</p>
-        <nav class="bp-battery-links" aria-label="This battery elsewhere">
-            <a href="/dpp/@batteryKey" target="_blank" rel="noreferrer">Public summary</a>
-            <a href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer">Detail page</a>
-            <a href="@ReaderPages.AccidentsHref(Model.Pack.Id)">Accident page</a>
-        </nav>
-        <dl class="bp-battery-facts">
-            <div class="bp-battery-fact-wide">
-                <dt>Battery ID <span class="bp-attr-number">#6</span></dt>
-                <dd><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Identifier</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></dd>
+    </header>
+
+    @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
+
+    <div class="bp-bpage">
+        @await Html.PartialAsync("~/Views/Batteries/_BatteryNav.cshtml", new BatteryNav(sections, chosen, Model.Href))
+        <div class="bp-bpanel">
+            <div data-section="overview" hidden="@Hidden("overview")">
+                <section class="bp-card bp-console-form-card bp-battery-card" id="overview">
+                    <span class="bp-summary-title">Overview</span>
+                    <h2>@(active is null ? "Not published" : $"Published · version {active.Number}")</h2>
+                    <p class="mb-0">Your cluster's battery. Change its status after a repair or when it's used again, keep its live values and accident reports, or hand it over to another customer. The team keeps its passport values.</p>
+                    <dl class="bp-overview">
+                        @if (Model.Status is { } status)
+                        {
+                            <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-swap" /></svg>Battery status</dt><dd>@status.StatusWords<small>@(status.Since is { } since ? $"since {ValueWords.Time(since)}{(status.AfterRepair ? ", after a repair" : string.Empty)}" : "since it was made")</small></dd></div>
+                        }
+                        <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-live" /></svg>Live values</dt><dd>@BatterySections.LiveHint(Model.Pack, Model.Live)<small>Measured on the battery</small></dd></div>
+                        <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-accident" /></svg>Accident reports</dt><dd>@(Model.AccidentReports.Count == 0 ? "None reported" : Words.Count(Model.AccidentReports.Count, "report"))</dd></div>
+                    </dl>
+                </section>
             </div>
-            <div>
-                <dt>Batch</dt>
-                <dd>@Model.BatchLabel</dd>
+            @if (Model.Status is { } statusCard)
+            {
+                <div data-section="status" hidden="@Hidden("status")">
+                    @await Html.PartialAsync("~/Views/Batteries/_StatusCard.cshtml", statusCard)
+                </div>
+            }
+            <div data-section="live" hidden="@Hidden("live")">
+                @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
             </div>
-            <div>
-                <dt>Cluster</dt>
-                <dd>@Model.ClusterLabel</dd>
+            <div data-section="accidents" hidden="@Hidden("accidents")">
+                @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
             </div>
-            <div>
-                <dt>Serial number <span class="bp-attr-number">#11</span></dt>
-                <dd>@Model.SerialNumber</dd>
+            <div data-section="handover" hidden="@Hidden("handover")">
+                @await Html.PartialAsync("~/Views/ClusterAdmin/_HandOverCard.cshtml", Model.HandOver)
             </div>
-        </dl>
-    </header>
+            <div data-section="modules" hidden="@Hidden("modules")">
+                @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
+            </div>
+            <div data-section="versions" hidden="@Hidden("versions")">
+                @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
+            </div>
+        </div>
+    </div>
 
-    @if (Model.Status is { } status)
+    @* The windows, at page level: one inside a hidden section can't open. *@
+    @if (Model.Status is { } statusWindows)
     {
-        @await Html.PartialAsync("~/Views/Batteries/_StatusCard.cshtml", status)
+        @await Html.PartialAsync("~/Views/Batteries/_StatusDialog.cshtml", statusWindows)
     }
-    @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
-    @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
-    @await Html.PartialAsync("~/Views/ClusterAdmin/_HandOverCard.cshtml", Model.HandOver)
-    @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
-    @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
 </main>
 
 @section Scripts {
     <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
     <script src="~/js/bp-status-change.js" asp-append-version="true"></script>
+    <script src="~/js/bp-battery-page.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/ClusterAdmin/_HandOverCard.cshtml b/web/Views/ClusterAdmin/_HandOverCard.cshtml
index 35aa426..8640afa 100644
--- a/web/Views/ClusterAdmin/_HandOverCard.cshtml
+++ b/web/Views/ClusterAdmin/_HandOverCard.cshtml
@@ -15,11 +15,8 @@
         <span class="bp-summary-title">Hand over <em class="bp-field-marker">Not passport data</em></span>
         <h2>Waiting for @Model.ReceiverName</h2>
         <p class="mb-2">Asked @ValueWords.Time(waiting.AskedAtUtc). The battery stays with your cluster until they accept.</p>
+        <p class="bp-help-text mb-0">To cancel it, use <strong>Cancel the hand-over</strong> on the line at the top of the page.</p>
         @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "handover")
-        <form method="post" action="@Model.Action/cancel">
-            @Html.AntiForgeryToken()
-            <div class="bp-dialog-actions"><button type="submit" class="bp-secondary-button">Cancel the hand-over</button></div>
-        </form>
     }
     else
     {
diff --git a/web/Views/ProductData/_RecordEditor.cshtml b/web/Views/ProductData/_RecordEditor.cshtml
index b180ddf..33cb24f 100644
--- a/web/Views/ProductData/_RecordEditor.cshtml
+++ b/web/Views/ProductData/_RecordEditor.cshtml
@@ -49,7 +49,13 @@
                 <p class="bp-editor-scope">@scope</p>
             }
             <p class="bp-editor-summary">
-                @if (Model.Missing > 0)
+                @if (Model.Missing > 0 && Model.Kind == TargetKind.Pack)
+                {
+                    @* A battery's page lists every missing value once, in its overview (redesign R4). *@
+                    <span class="bp-missing-dot" aria-hidden="true"></span>
+                    <span>The missing values are marked below; the overview lists every value the passport still needs.</span>
+                }
+                else if (Model.Missing > 0)
                 {
                     <span class="bp-missing-dot" aria-hidden="true"></span>
                     <span><strong>@Words.Count(Model.Missing, "value")</strong> the passport needs by Feb 2027 @Words.Is(Model.Missing) missing.</span>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 0588ba5..83a42bf 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -2871,3 +2871,548 @@ main .bp-field-marker.is-published {
 .bp-handover-row .bp-dialog-actions {
   margin: 0;
 }
+
+/* ==== The battery pages' layout (redesign R4, option B, mock-up H and F1; the owner's improvements of 2 Nov 2026) =====
+   A short header: the model's picture, the battery and its two statuses, its facts, and one toolbar row with what to do
+   and where to open it. Then a slim side menu that shows one section at a time, like the old app's. On a phone: a
+   section picker, the facts folded, and the actions in a bar at the bottom. Badges, chips and notice lines come from
+   bp-status.css. */
+.bp-bh {
+  position: relative;
+  display: grid;
+  gap: 12px;
+  margin-top: 8px;
+  padding: 16px 20px;
+  border: 1px solid var(--bp-border);
+  border-radius: 14px;
+  background: var(--bp-surface);
+}
+
+/* The picture, a column from the label down to the facts; the text top-aligned beside it. It is sized by its height
+   at its own shape (the 7M is tall, the 13M wide), the same as the public hero's picture (bp-status.css). */
+.bp-bh-main {
+  display: grid;
+  grid-template-columns: auto minmax(0, 1fr);
+  grid-template-areas:
+    "picture id"
+    "picture facts";
+  gap: 10px 22px;
+  align-items: start;
+}
+
+.bp-bh-main.no-picture {
+  grid-template-columns: minmax(0, 1fr);
+  grid-template-areas:
+    "id"
+    "facts";
+}
+
+.bp-bh-main > .bp-bh-id {
+  grid-area: id;
+}
+
+.bp-bh-main > .bp-bh-facts-row {
+  grid-area: facts;
+}
+
+.bp-bh-picture {
+  grid-area: picture;
+  align-self: stretch;
+  display: grid;
+  place-items: center;
+  padding: 10px;
+  border: 1px solid var(--bp-border);
+  border-radius: 12px;
+  background: #f3f6fa;
+}
+
+/* The box fits the picture's visible shape: bp-battery-page.js cuts transparent margins off, hiding it meanwhile. */
+.bp-bh-picture img.is-trimming {
+  opacity: 0;
+}
+
+.bp-bh-picture img {
+  display: block;
+  width: auto;
+  height: 160px;
+  max-width: 220px;
+  object-fit: contain;
+}
+
+.bp-bh-id {
+  min-width: 0;
+}
+
+.bp-bh h1 {
+  margin: 2px 0 6px;
+  font-size: 1.6rem;
+  overflow-wrap: anywhere;
+}
+
+/* The facts: one line each, the long battery ID shortened in the middle (its copy button and tooltip keep it whole). */
+.bp-bh-facts-row {
+  display: grid;
+  grid-template-columns: max-content minmax(0, 1fr);
+  gap: 10px 22px;
+  padding-top: 10px;
+  border-top: 1px solid var(--bp-border);
+}
+
+.bp-bh-facts {
+  display: grid;
+  grid-template-columns: repeat(auto-fit, minmax(165px, 1fr));
+  gap: 8px 22px;
+  margin: 0;
+}
+
+.bp-bh-facts div {
+  min-width: 0;
+}
+
+.bp-bh-facts dt {
+  color: var(--bp-muted);
+  font-size: 0.74rem;
+  font-weight: 600;
+}
+
+.bp-bh-facts dd {
+  margin: 0;
+  overflow: hidden;
+  font-weight: 600;
+  text-overflow: ellipsis;
+  white-space: nowrap;
+}
+
+.bp-bh-facts dt .bp-text-button {
+  margin-left: 6px;
+  font-size: 0.78rem;
+}
+
+/* The folded facts: each at its own width on one line, flowing to a next row only when they don't fit. */
+.bp-bh-details .bp-bh-facts {
+  display: flex;
+  flex-wrap: wrap;
+  gap: 8px 28px;
+}
+
+.bp-bh-details .bp-bh-facts dd {
+  overflow: visible;
+}
+
+.bp-bh-fact-id {
+  grid-column: 1 / -1;
+}
+
+.bp-bh-fact-id dd {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 4px 8px;
+  overflow: visible;
+}
+
+.bp-bh-fact-id .bp-id-copy-row {
+  display: inline-flex;
+  min-width: 0;
+  max-width: 100%;
+}
+
+.bp-bh-fact-id .bp-id-value {
+  overflow: hidden;
+  text-overflow: ellipsis;
+  white-space: nowrap;
+}
+
+.bp-bh-details > summary {
+  display: none;
+}
+
+/* One toolbar row: the actions on the left, the labelled icon links on the right (owner's improvement 3). */
+.bp-bh-bar {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 8px;
+  padding-top: 10px;
+  border-top: 1px solid var(--bp-border);
+}
+
+.bp-bh-actions {
+  display: flex;
+  flex-wrap: wrap;
+  gap: 8px;
+}
+
+.bp-bh-open {
+  display: flex;
+  flex-wrap: wrap;
+  gap: 2px;
+  margin-left: auto;
+}
+
+.bp-ilink {
+  display: inline-flex;
+  align-items: center;
+  gap: 6px;
+  padding: 7px 9px;
+  border-radius: 9px;
+  color: var(--bp-text);
+  font-size: 0.84rem;
+  font-weight: 600;
+  line-height: 1.2;
+  text-decoration: none;
+}
+
+.bp-ilink .bp-i {
+  width: 18px;
+  height: 18px;
+  color: var(--bp-primary);
+}
+
+.bp-ilink:hover,
+.bp-ilink:focus-visible {
+  background: rgba(42, 110, 207, 0.08);
+  color: var(--bp-primary);
+}
+
+/* "More": the risky actions, in a small menu. */
+.bp-more {
+  position: relative;
+}
+
+.bp-more > summary {
+  list-style: none;
+  cursor: pointer;
+}
+
+.bp-more > summary::-webkit-details-marker {
+  display: none;
+}
+
+.bp-more-list {
+  position: absolute;
+  left: 0;
+  z-index: 5;
+  display: grid;
+  min-width: 270px;
+  margin-top: 6px;
+  padding: 6px;
+  border: 1px solid var(--bp-border);
+  border-radius: 12px;
+  background: var(--bp-surface);
+  box-shadow: 0 10px 28px rgba(10, 40, 80, 0.14);
+}
+
+.bp-more-list a {
+  display: flex;
+  align-items: center;
+  gap: 10px;
+  padding: 9px 10px;
+  border-radius: 8px;
+  color: var(--bp-text);
+  text-decoration: none;
+}
+
+.bp-more-list a:hover {
+  background: rgba(42, 110, 207, 0.08);
+}
+
+.bp-more-list .is-danger {
+  color: var(--bp-danger);
+}
+
+.bp-more-list small {
+  display: block;
+  color: var(--bp-muted);
+  font-size: 0.76rem;
+}
+
+/* The windows at page level keep the card's form styles; the host itself draws nothing. */
+.bp-console-form-card.bp-dialog-host {
+  display: contents;
+}
+
+/* The slim side menu: a coloured edge on the open section, in view while the page scrolls (owner's improvement 5). */
+.bp-bpage {
+  display: grid;
+  grid-template-columns: 230px minmax(0, 1fr);
+  gap: 22px;
+  align-items: start;
+  margin-top: 18px;
+}
+
+.bp-bnav {
+  position: sticky;
+  top: 12px;
+  gap: 2px;
+}
+
+.bp-bnav .bp-edit-nav-button {
+  display: grid;
+  grid-template-columns: 20px minmax(0, 1fr) auto;
+  align-items: center;
+  gap: 1px 10px;
+  padding: 8px 10px;
+  border: 0;
+  border-left: 3px solid transparent;
+  border-radius: 0 8px 8px 0;
+  background: transparent;
+  text-decoration: none;
+}
+
+.bp-bnav .bp-edit-nav-button:hover {
+  background: rgba(42, 110, 207, 0.06);
+}
+
+.bp-bnav .bp-edit-nav-button.is-active {
+  border-left-color: var(--bp-primary);
+  background: rgba(42, 110, 207, 0.08);
+  color: var(--bp-primary);
+}
+
+.bp-bnav .bp-edit-nav-button .bp-i {
+  color: var(--bp-primary);
+}
+
+.bp-bnav .bp-edit-nav-button small {
+  grid-column: 2 / -1;
+  color: var(--bp-muted);
+  font-size: 0.74rem;
+  font-weight: 500;
+}
+
+.bp-count {
+  padding: 0 7px;
+  border-radius: 999px;
+  background: rgba(110, 130, 159, 0.16);
+  color: var(--bp-text);
+  font-size: 0.72rem;
+}
+
+.bp-bpicker {
+  display: none;
+}
+
+.bp-bpanel > [data-section] > .bp-card:first-child {
+  margin-top: 0 !important;
+}
+
+/* The overview's "Nothing needs you" or what needs doing. */
+.bp-todo {
+  display: grid;
+  gap: 6px;
+  margin: 14px 0 0;
+  padding: 0;
+  list-style: none;
+}
+
+.bp-todo li {
+  padding: 8px 12px;
+  border-left: 3px solid #f59e0b;
+  border-radius: 0 8px 8px 0;
+  background: rgba(245, 158, 11, 0.08);
+}
+
+.bp-todo li.is-none {
+  border-left-color: #22c55e;
+  background: rgba(34, 197, 94, 0.07);
+}
+
+.bp-overview {
+  display: grid;
+  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
+  gap: 12px;
+  margin: 14px 0;
+}
+
+.bp-overview > div {
+  padding: 12px 14px;
+  border: 1px solid var(--bp-border);
+  border-radius: 12px;
+}
+
+.bp-overview dt {
+  display: flex;
+  align-items: center;
+  gap: 8px;
+  color: var(--bp-muted);
+  font-size: 0.76rem;
+  font-weight: 650;
+  letter-spacing: 0.04em;
+  text-transform: uppercase;
+}
+
+.bp-overview dd {
+  margin: 6px 0 0;
+}
+
+.bp-overview dd small {
+  display: block;
+  color: var(--bp-muted);
+}
+
+@media (max-width: 767.98px) {
+  .bp-bh {
+    padding: 14px;
+  }
+
+  .bp-bh h1 {
+    font-size: 1.35rem;
+  }
+
+  .bp-bh-facts-row {
+    grid-template-columns: minmax(0, 1fr);
+  }
+
+  /* On a phone the picture is about 100 px tall beside the name; the facts take the full width below. */
+  .bp-bh-main {
+    grid-template-areas:
+      "picture id"
+      "facts facts";
+    gap: 10px 14px;
+  }
+
+  .bp-bh-main.no-picture {
+    grid-template-areas:
+      "id"
+      "facts";
+  }
+
+  .bp-bh-picture {
+    align-self: start;
+    min-width: 0;
+    padding: 6px;
+  }
+
+  .bp-bh-picture img {
+    height: 100px;
+    max-width: 110px;
+  }
+
+  .bp-bh-facts {
+    grid-template-columns: repeat(2, minmax(0, 1fr));
+  }
+
+  /* The less used facts fold behind "More details" (owner's improvement 9). */
+  .bp-bh-details > summary {
+    display: inline-block;
+    color: var(--bp-primary);
+    font-size: 0.88rem;
+    font-weight: 600;
+    cursor: pointer;
+  }
+
+  .bp-bh-details[open] > summary {
+    margin-bottom: 8px;
+  }
+
+  /* "More" beside the five links, so the header stays short. */
+  .bp-bh-bar {
+    display: grid;
+    grid-template-columns: auto minmax(0, 1fr);
+    gap: 8px;
+  }
+
+  .bp-bh-bar .bp-more > summary {
+    min-height: 0;
+    padding: 6px 10px;
+    font-size: 0.85rem;
+  }
+
+  .bp-bh-open {
+    display: grid;
+    grid-template-columns: repeat(5, minmax(0, 1fr));
+    margin-left: 0;
+  }
+
+  .bp-ilink {
+    flex-direction: column;
+    gap: 3px;
+    padding: 6px 2px;
+    font-size: 0.7rem;
+    text-align: center;
+  }
+
+  /* The actions in a bar at the bottom of the screen; the page leaves room for it (owner's improvement 8). */
+  .bp-bh-actions {
+    position: fixed;
+    right: 0;
+    bottom: 0;
+    left: 0;
+    z-index: 20;
+    display: grid;
+    grid-auto-columns: minmax(0, 1fr);
+    grid-auto-flow: column;
+    gap: 8px;
+    padding: 10px 14px calc(10px + env(safe-area-inset-bottom));
+    border-top: 1px solid var(--bp-border);
+    background: var(--bp-surface);
+    box-shadow: 0 -6px 18px rgba(10, 40, 80, 0.08);
+  }
+
+  .bp-battery-page {
+    padding-bottom: 84px;
+  }
+
+  .bp-more {
+    position: static;
+  }
+
+  .bp-more-list {
+    left: 14px;
+    right: 14px;
+    min-width: 0;
+  }
+
+  /* A section picker at the top instead of the stacked menu (owner's improvement 7). */
+  .bp-bpage {
+    grid-template-columns: minmax(0, 1fr);
+    gap: 12px;
+    margin-top: 14px;
+  }
+
+  .bp-bnav {
+    display: none;
+  }
+
+  .bp-bpicker {
+    display: block;
+  }
+
+  .bp-bpicker label {
+    display: flex;
+    align-items: center;
+    gap: 10px;
+    margin: 0;
+    font-weight: 600;
+  }
+
+  .bp-bpicker select {
+    flex: 1;
+    min-height: 42px;
+    padding: 8px 10px;
+    border: 1px solid var(--bp-border);
+    border-radius: 10px;
+    background: var(--bp-surface);
+    color: var(--bp-text);
+    font: inherit;
+    font-weight: 600;
+  }
+}
+
+[data-theme="dark"] .bp-bh-picture {
+  background: #e9eef5;
+}
+
+[data-theme="dark"] .bp-bnav .bp-edit-nav-button {
+  border-color: transparent;
+  background: transparent;
+  text-decoration: none;
+}
+
+[data-theme="dark"] .bp-bnav .bp-edit-nav-button.is-active {
+  border-left-color: var(--bp-primary);
+  background: rgba(122, 182, 255, 0.1);
+  color: var(--bp-primary);
+}
+
```

`web/Views/Batteries/_BatteryNav.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model BatteryNav
@* The battery page's side menu (redesign R4, option B, like the old app's): one section at a time. Each entry is a link,
   so it works without JavaScript; bp-battery-page.js switches sections without a reload. *@
@* On a phone a picker at the top chooses the section instead of the stacked menu (owner's improvement 7); without
   JavaScript its button shows the chosen one. *@
<form method="get" action="@Model.Href" class="bp-bpicker">
    <label>
        <span>Section</span>
        <select name="section" data-section-picker>
            @foreach (var entry in Model.Sections)
            {
                <option value="@entry.Id" selected="@(entry.Id == Model.Chosen)">@entry.Title@(entry.Count is { } count ? $" ({count})" : string.Empty)</option>
            }
        </select>
    </label>
    <noscript><button type="submit" class="bp-secondary-button bp-button-compact">Show</button></noscript>
</form>
<nav class="bp-edit-nav bp-bnav" aria-label="Battery sections" data-battery-nav>
    @foreach (var entry in Model.Sections)
    {
        var open = entry.Id == Model.Chosen;
        <a href="@Model.Href?section=@entry.Id" class="bp-edit-nav-button@(open ? " is-active" : string.Empty)" aria-current="@(open ? "page" : null)" data-section-link="@entry.Id">
            <svg class="bp-i" aria-hidden="true"><use href="#i-@entry.Icon" /></svg>
            <span class="bp-edit-nav-label">@entry.Title</span>
            @if (entry.Count is { } count)
            {
                <span class="bp-count">@count</span>
            }
            @if (entry.Hint is { } hint)
            {
                <small>@hint</small>
            }
        </a>
    }
</nav>
```

`web/Views/Batteries/_StatusDialog.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Editing
@using BatteryPassWeb.Dpp.Pages
@model StatusCardView
@* The "Change status" window (step 4b, mock-up S3) and the "Add a note" window, at page level on both battery pages:
   a window inside a hidden section of the side menu can't open (redesign R4). Its buttons stay in view (R1). *@
@* The host keeps the card's form styles for the windows (the radios without their oval, the fields' look) and draws nothing itself. *@
<div class="bp-console-form-card bp-dialog-host">
@if (Model.CanChange)
{
    <dialog id="status-dialog" class="bp-modal-panel bp-dialog bp-dialog-wide" aria-labelledby="status-dialog-title" data-status-dialog>
        <form method="post" action="@Model.Restated.SaveAction" class="bp-dialog-form">
            @Html.AntiForgeryToken()
            <div data-status-step="1">
                <h2 id="status-dialog-title">Change the battery status</h2>
                <p class="bp-help-text">Now: <strong>@Model.Status</strong>. What happened to the battery?</p>
                <div class="bp-choice-list">
                    @foreach (var (what, label, help) in Model.Choices)
                    {
                        <label class="bp-choice"><input type="radio" name="what" value="@what" checked="@(what == Model.Choices[0].What)" data-status-choice="@label" /><strong>@label</strong><small>@help</small></label>
                    }
                </div>
                <div class="bp-dialog-actions">
                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
                    <button type="button" class="bp-primary-button" data-status-next>Next</button>
                </div>
            </div>
            <div data-status-step="2">
                <h2><span data-status-chosen>@Model.Choices[0].Label</span>: re-state the values</h2>
                <p class="bp-help-text">Check each value; they start as the battery's current ones.</p>
                <div data-status-repair>
                    <label><span>Day of the repair</span><input type="date" name="repairDate" value="@Model.Today" max="@Model.Today" /></label>
                    <label><span>What was repaired</span><input type="text" name="repairDescription" maxlength="@StatusChangeService.MaxDescription" placeholder="e.g. Module 4 replaced after a coolant leak" /></label>
                </div>
                <div class="bp-attr-list">
                    @foreach (var field in Model.Restated.Sections.SelectMany(section => section.Fields))
                    {
                        @await Html.PartialAsync("~/Views/ProductData/_EditorField.cshtml", new EditorFieldView(Model.Restated, field))
                        @if (field.Attribute.Number == 59 && Model.RatedCapacityAh is { } rated)
                        {
                            <div class="bp-suggest" data-fade-suggest data-rated="@rated.ToString(System.Globalization.CultureInfo.InvariantCulture)">
                                <label><span>Measured remaining capacity (Ah)</span><input type="text" inputmode="decimal" data-fade-remaining /></label>
                                <button type="button" class="bp-text-button" data-fade-button>Suggest the fade</button>
                                <small class="bp-field-help" data-fade-note>1 − remaining / @ValueWords.Number(rated) Ah rated. The measured capacity isn't stored.</small>
                            </div>
                        }
                    }
                </div>
                <div class="bp-dialog-actions">
                    <button type="button" class="bp-secondary-button" data-status-back>Back</button>
                    <button type="submit" class="bp-primary-button">Save and publish a new version</button>
                </div>
            </div>
        </form>
    </dialog>
}
@if (Model.MayAddNote)
{
    <dialog id="note-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="note-dialog-title">
        <form method="post" action="@Model.NoteAction" class="bp-dialog-form">
            @Html.AntiForgeryToken()
            <h2 id="note-dialog-title">Add a note to the history</h2>
            <label><span>Entries are never changed or removed. To correct one, add a note; it shows as "Correction".</span><input type="text" name="note" maxlength="@StatusChangeService.MaxDescription" placeholder="e.g. The repair of 20 Oct was on module 5, not module 4." required /></label>
            <div class="bp-dialog-actions">
                <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
                <button type="submit" class="bp-primary-button">Add note</button>
            </div>
        </form>
    </dialog>
}
</div>
```

`web/wwwroot/js/bp-battery-page.js`:

```javascript
// The battery pages' side menu (redesign R4, option B): one section at a time. The server opens the right one, also
// after a save; this switches sections without a reload, keeps the address so a reload stays put, and follows a
// #link into a hidden section (e.g. "Fill in" or a form's return to #live). Without JavaScript the menu's links work.
(function () {
    const nav = document.querySelector('[data-battery-nav]');
    if (!nav) {
        return;
    }

    const sections = Array.from(document.querySelectorAll('[data-section]'));
    const links = Array.from(nav.querySelectorAll('[data-section-link]'));
    const picker = document.querySelector('[data-section-picker]');

    function show(id) {
        if (!sections.some((section) => section.dataset.section === id)) {
            return false;
        }

        sections.forEach((section) => { section.hidden = section.dataset.section !== id; });
        links.forEach((link) => {
            const open = link.dataset.sectionLink === id;
            link.classList.toggle('is-active', open);
            if (open) {
                link.setAttribute('aria-current', 'page');
            } else {
                link.removeAttribute('aria-current');
            }
        });
        if (picker) {
            picker.value = id;
        }

        return true;
    }

    // The phone's section picker switches like the menu (owner's improvement 7).
    picker?.addEventListener('change', () => {
        if (show(picker.value)) {
            const url = new URL(window.location.href);
            url.searchParams.set('section', picker.value);
            url.hash = '';
            history.replaceState(null, '', url);
        }
    });

    // On a phone the less used facts start folded behind "More details" (owner's improvement 9).
    if (window.matchMedia('(max-width: 767.98px)').matches) {
        document.querySelectorAll('details[data-fold-on-phone]').forEach((details) => { details.open = false; });
    }

    links.forEach((link) => {
        link.addEventListener('click', (event) => {
            if (event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
                return; // a new tab or window: let the browser open the link
            }

            event.preventDefault();
            if (show(link.dataset.sectionLink)) {
                history.replaceState(null, '', link.getAttribute('href'));
                const panel = document.querySelector('.bp-bpanel');
                if (panel && panel.getBoundingClientRect().top < 0) {
                    panel.scrollIntoView({ block: 'start' }); // on a phone the menu sits above the section
                }
            }
        });
    });

    function follow() {
        const id = decodeURIComponent(location.hash.slice(1));
        const target = id ? document.getElementById(id) : null;
        const section = target?.closest('[data-section]');
        if (section && section.hidden && show(section.dataset.section)) {
            target.scrollIntoView({ block: 'start' });
        }
    }

    window.addEventListener('hashchange', follow);
    follow();
})();

// The header's picture fits its visible shape (owner, 2 Nov 2026): a picture's transparent margins are cut off, so a
// tall battery like the 7M gets a narrow box. A picture without transparent margins, a JPEG, or no JavaScript shows the
// picture as it is. The picture is hidden only while it is being cut, and data-trimmed marks it done.
(function () {
    const image = document.querySelector('.bp-bh-picture img');
    if (!image) {
        return;
    }

    function done() {
        image.classList.remove('is-trimming');
        image.dataset.trimmed = 'true';
    }

    // The smallest box around the pixels that aren't transparent, or null when there is nothing to cut.
    function visibleBox(width, height, pixels) {
        let left = width;
        let right = -1;
        let top = height;
        let bottom = -1;
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                if (pixels[(y * width + x) * 4 + 3] > 8) {
                    left = Math.min(left, x);
                    right = Math.max(right, x);
                    top = Math.min(top, y);
                    bottom = Math.max(bottom, y);
                }
            }
        }
        if (right < 0 || (left === 0 && top === 0 && right === width - 1 && bottom === height - 1)) {
            return null;
        }
        return { left, top, width: right - left + 1, height: bottom - top + 1 };
    }

    function trim() {
        let cut = null;
        try {
            const width = image.naturalWidth;
            const height = image.naturalHeight;
            if (width && height) {
                const canvas = document.createElement('canvas');
                canvas.width = width;
                canvas.height = height;
                const context = canvas.getContext('2d', { willReadFrequently: true });
                context.drawImage(image, 0, 0);
                const box = visibleBox(width, height, context.getImageData(0, 0, width, height).data);
                if (box) {
                    const out = document.createElement('canvas');
                    out.width = box.width;
                    out.height = box.height;
                    out.getContext('2d').drawImage(canvas, box.left, box.top, box.width, box.height, 0, 0, box.width, box.height);
                    cut = out.toDataURL('image/png');
                }
            }
        } catch {
            cut = null; // a picture the page may not read shows as it is
        }
        if (!cut) {
            done();
            return;
        }
        image.addEventListener('load', done, { once: true });
        image.addEventListener('error', done, { once: true });
        image.src = cut;
    }

    image.classList.add('is-trimming');
    if (image.complete) {
        trim();
    } else {
        image.addEventListener('load', trim, { once: true });
        image.addEventListener('error', done, { once: true });
    }
})();
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPageLayoutTests | FullyQualifiedName~HandOverPagesTests"`
Expected: `Passed!  - Failed:     0, Passed:    17`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2325`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_BatteryNav.cshtml web/Views/Batteries/_PassportStatusCard.cshtml web/Views/Batteries/_StatusCard.cshtml web/Views/Batteries/_StatusDialog.cshtml web/Views/ClusterAdmin/Battery.cshtml web/Views/ClusterAdmin/_HandOverCard.cshtml web/Views/ProductData/_RecordEditor.cshtml web/wwwroot/css/bp-product-data.css web/wwwroot/js/bp-battery-page.js
git commit -m "feat(dpp): the battery pages as a header and a side menu (layout B)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The overview: the live values with their last 7 days

The owner's improvement 4. The overview opens with four tiles: state of charge, full cycles, temperature and capacity fade. Each shows its value now and a small line of its last 7 days, read from the change log by measurement time, like the detail page's live history. Capacity fade changes only at a status change, so its tile says so. Below the tiles comes "Nothing needs you" or what needs doing, once; the battery status tile is gone, because the header's badge says it. The documents line reads "Documents: 6, all unchanged since they were uploaded". The cluster admin's overview has the same tiles.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/LiveTrendsTests.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Create: `web/Dpp/Pages/LiveTrends.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Create: `web/Views/Batteries/_LiveTiles.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/LiveTrendsTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R4, the owner's improvement 4: the battery overview's tiles (state of charge, cycles, temperature, capacity
/// fade), each with a small line of its last 7 days, read from the change log like the live history.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class LiveTrendsTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task EachTile_ShouldShowItsValueAndItsLast7Days()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var api = BatteryApiTests.Api(rig);
        var now = rig.Clock.GetUtcNow().UtcDateTime;
        foreach (var (hoursAgo, level) in new[] { (200, 10), (48, 40), (24, 55), (1, 61) })
        {
            var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
            var written = await api.WriteLiveValuesAsync(PublicationTesting.Team, pack, new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["stateOfChargeSoC"] = level }, now.AddHours(-hoursAgo), preview: false);
            Assert.True(written.Saved, $"{hoursAgo}h: {string.Join("; ", written.Keys.Select(key => $"{key.Key}:{key.Outcome} {key.Message}"))}");
        }

        var sources = (await rig.Store.LoadSourcesAsync(rig.PackId))!;
        var tiles = await new LiveTrends(new ApplicationAuditService(rig.Context), rig.Clock).TilesAsync(sources.Pack, sources.Model);
        var charge = tiles.Single(tile => tile.Property == "stateOfChargeSoC");
        var fade = tiles.Single(tile => tile.Property == "capacityFade");

        Assert.Equal(["State of charge", "Full cycles", "Temperature", "Capacity fade"], tiles.Select(tile => tile.Title));
        Assert.Equal("61 %", charge.Shown);
        Assert.Equal([40m, 55m, 61m], charge.Points.Select(point => point.Value)); // the reading of 200 hours ago is older than 7 days
        Assert.Equal("From 40 to 61 over the last 7 days", charge.TrendWords);
        Assert.StartsWith("0,", charge.Line(140, 36), StringComparison.Ordinal);
        Assert.Equal("Re-stated at each status change", fade.Note);
        Assert.NotNull(fade.Value); // the model's value until the battery re-states it
        Assert.Null(fade.Line(140, 36));
        Assert.Equal("No change in the last 7 days", fade.TrendWords);
    }

    [Fact]
    public void TheLine_ShouldRunFromTheOldestOnTheLeftWithTheHighestOnTop()
    {
        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var tile = new LiveTile("State of charge", "stateOfChargeSoC", 80m, "%", "Measured …", [new(start, 20m), new(start.AddHours(12), 80m), new(start.AddHours(24), 50m)]);

        Assert.Equal("0,34 70,2 140,18", tile.Line(140, 36));
        Assert.Equal("0,18 140,18", new LiveTile("Full cycles", "x", 5m, "", "", [new(start, 5m), new(start.AddHours(1), 5m)]).Line(140, 36)); // flat in the middle
    }

    [Fact]
    public void TheOverview_ShouldShowTheTilesThenWhatNeedsDoingOnceInClearWords()
    {
        var team = Read("web", "Views", "Batteries", "Battery.cshtml");
        var overview = team[team.IndexOf("<div data-section=\"overview\"", StringComparison.Ordinal)..team.IndexOf("<div data-section=\"values\"", StringComparison.Ordinal)];
        var tiles = Read("web", "Views", "Batteries", "_LiveTiles.cshtml");

        Assert.Contains("@await Html.PartialAsync(\"~/Views/Batteries/_LiveTiles.cshtml\", Model.Tiles)", overview, StringComparison.Ordinal);
        Assert.True(overview.IndexOf("_LiveTiles", StringComparison.Ordinal) < overview.IndexOf("Nothing needs you.", StringComparison.Ordinal));
        Assert.DoesNotContain("<dt><svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-swap\" /></svg>Battery status</dt>", overview, StringComparison.Ordinal); // the header's badge says it
        Assert.Contains("Documents: @publication.Files.Count, all unchanged since they were uploaded", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("Uploaded files:", overview, StringComparison.Ordinal);
        Assert.Contains("<svg class=\"bp-spark\" viewBox=\"0 0 140 36\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"@tile.TrendWords\"><polyline points=\"@line\" /></svg>", tiles, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"~/Views/Batteries/_LiveTiles.cshtml\", Model.Tiles)", Read("web", "Views", "ClusterAdmin", "Battery.cshtml"), StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<LiveTrends>();", Read("web", "Dpp", "DppServiceCollectionExtensions.cs"), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~LiveTrendsTests"`
Expected: the build fails with CS0246 (for example `error CS0246 The type or namespace name 'LiveTile' could not be found`).

- [ ] **Step 3: Write the code**

Patch `r4-task3-code.patch` (`web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r4-task3-code.patch`, outside the worktree, and run `git apply ../r4-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index 3d9cd89..3163889 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -49,6 +49,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<RecordEditor>();
         services.AddSingleton<ProductDataPages>();
         services.AddSingleton<StatusPages>();
+        services.AddSingleton<LiveTrends>();
         services.AddSingleton<BatteryPages>();
         services.AddSingleton<ClusterBatteryPages>();
         services.AddSingleton<DppReaders>();
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 1969ecc..2d192ff 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -118,6 +118,9 @@ public sealed record BatteryPage(
     /// <summary>The receiving cluster's name while a hand-over waits for its answer (step 4d); null otherwise.</summary>
     public string? HandOverTo { get; init; }
 
+    /// <summary>The overview's tiles: state of charge, cycles, temperature and capacity fade, with their last 7 days (redesign R4).</summary>
+    public IReadOnlyList<LiveTile> Tiles { get; init; } = [];
+
     /// <summary>The team's "Passport status" card (mock-up S5, step 4c-2): invalid, marked for deletion, and their undo.</summary>
     public PassportMarkView Marks => new(
         Publication.Active is not null ? DppStatuses.Active : Publication.Latest?.DppStatus,
@@ -191,7 +194,8 @@ public sealed class BatteryPages(
     DppLinks links,
     ClusterRepository clusters,
     ApplicationAuditService audit,
-    StatusPages status)
+    StatusPages status,
+    LiveTrends? trends = null)
 {
     public const string ClusterChangedEvent = "dpp.pack-cluster-changed";
     public const string ModulesSetEvent = "dpp.pack-modules-set";
@@ -420,6 +424,7 @@ public sealed class BatteryPages(
             new RemovalView($"{href}/remove", versions.Count > 0 ? "its passport is published, so it stays with its versions." : null))
         {
             Status = await status.CardAsync(sources, active is not null, href, forTeam: true, cancellationToken: cancellationToken),
+            Tiles = trends is null ? [] : await trends.TilesAsync(sources.Pack, sources.Model, cancellationToken),
             HandOverTo = await store.FindWaitingHandOverAsync(packId, cancellationToken) is { } waiting
                 ? clusterChoices.FirstOrDefault(cluster => string.Equals(cluster.Id, waiting.ToClusterId, StringComparison.OrdinalIgnoreCase))?.Name ?? waiting.ToClusterId
                 : null,
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index bd883b2..0e734d3 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -26,6 +26,9 @@ public sealed record ClusterBatteryPage(
     /// <summary>The battery status card (step 4b), shared with the team's page.</summary>
     public StatusCardView? Status { get; init; }
 
+    /// <summary>The overview's tiles, as on the team's page (redesign R4).</summary>
+    public IReadOnlyList<LiveTile> Tiles { get; init; } = [];
+
     /// <summary>The "Hand over" card (step 4d): the waiting hand-over, or the window to ask for one.</summary>
     public HandOverCardView HandOver { get; init; } = new(string.Empty, string.Empty, null, null);
 
@@ -70,7 +73,8 @@ public sealed class ClusterBatteryPages(
     DocumentService documents,
     DocumentStore documentStore,
     ClusterRepository clusters,
-    StatusPages status)
+    StatusPages status,
+    LiveTrends? trends = null)
 {
     public static string BatteryHref(string packId) => $"/cluster-admin/batteries/{Uri.EscapeDataString(packId)}";
 
@@ -131,6 +135,7 @@ public sealed class ClusterBatteryPages(
             await documentStore.ListAsync(TargetKind.Pack, pack.Id, DocumentService.Accidents, cancellationToken))
         {
             Status = await status.CardAsync(sources, active: true, href, forTeam: false, cancellationToken: cancellationToken),
+            Tiles = trends is null ? [] : await trends.TilesAsync(sources.Pack, sources.Model, cancellationToken),
             HandOver = new HandOverCardView($"{href}/handover", serial, waiting, waiting is null ? null : names.GetValueOrDefault(waiting.ToClusterId) ?? waiting.ToClusterId),
         };
     }
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 687df3f..1488330 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -203,7 +203,8 @@
     <div class="bp-bpage">
         @await Html.PartialAsync("~/Views/Batteries/_BatteryNav.cshtml", new BatteryNav(sections, chosen, href))
         <div class="bp-bpanel">
-            @* Overview: today's Publication card, whole, as what needs you first. *@
+            @* The overview (redesign R4, the owner's improvement 4): where the passport stands, the live values with their last
+               7 days, and what needs doing, once. Today's Publication card, whole, with clearer words. *@
             <div data-section="overview" hidden="@Hidden("overview")">
                 <section class="bp-card bp-console-form-card bp-battery-card" id="publication">
                     <div class="bp-battery-card-head">
@@ -211,13 +212,13 @@
                             <span class="bp-summary-title">Overview</span>
                             @if (publication.Active is { } active)
                             {
-                                <h2>Published since @When(active.PublishedAtUtc)</h2>
-                                <p class="mb-0">Version @active.Number is live. A change to its values publishes a new version, and you're asked first; live values and accident reports update it in place.</p>
+                                <h2>Published · version @active.Number</h2>
+                                <p class="mb-0">Public since @When(active.PublishedAtUtc). Changing one of its values publishes version @(active.Number + 1), and you're asked first; live values and accident reports change it in place.</p>
                             }
                             else if (publication.Latest is { } last)
                             {
-                                <h2>@DppStatuses.Words(last.DppStatus) since @When(last.LastUpdatedUtc)</h2>
-                                <p class="mb-0">Version @last.Number is its last. Nothing on this passport changes any more; it stays readable by its passport and battery IDs.</p>
+                                <h2>@DppStatuses.Words(last.DppStatus) · version @last.Number</h2>
+                                <p class="mb-0">Since @When(last.LastUpdatedUtc). Nothing on this passport changes any more; it stays readable by its passport and battery IDs.</p>
                             }
                             else if (publication.Ready)
                             {
@@ -237,12 +238,15 @@
                     </div>
                     @{ await CardMessagesAsync("publication"); }
 
+                    @await Html.PartialAsync("~/Views/Batteries/_LiveTiles.cshtml", Model.Tiles)
+
                     @if (Model.NeedsYou == 0 && publication.IsPublished)
                     {
                         <ul class="bp-todo"><li class="is-none">Nothing needs you.</li></ul>
                     }
                     @if (publication.Missing.Count > 0)
                     {
+                        <h3 class="h6 mt-3 mb-0">What the passport still needs</h3>
                         <ul class="bp-missing-groups">
                             @foreach (var group in publication.Missing)
                             {
@@ -266,15 +270,6 @@
                         </ul>
                     }
 
-                    <dl class="bp-overview">
-                        @if (Model.Status is { } status)
-                        {
-                            <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-swap" /></svg>Battery status</dt><dd>@status.StatusWords<small>@(status.Since is { } since ? $"since {ValueWords.Time(since)}{(status.AfterRepair ? ", after a repair" : string.Empty)}" : "since it was made")</small></dd></div>
-                        }
-                        <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-live" /></svg>Live values</dt><dd>@BatterySections.LiveHint(Model.Pack, Model.Live)<small>Measured on the battery</small></dd></div>
-                        <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-accident" /></svg>Accident reports</dt><dd>@(Model.AccidentReports.Count == 0 ? "None reported" : Words.Count(Model.AccidentReports.Count, "report"))</dd></div>
-                    </dl>
-
                     <div class="bp-battery-checks">
                         @if (publication.NeededLater.Count > 0)
                         {
@@ -285,7 +280,7 @@
                         }
                         @if (publication.Files.Count == 0)
                         {
-                            <p class="mb-0 bp-batteries-muted">No uploaded files yet.</p>
+                            <p class="mb-0 bp-batteries-muted">No documents uploaded yet.</p>
                         }
                         else
                         {
@@ -293,11 +288,11 @@
                                 <summary>
                                     @if (filesChanged == 0)
                                     {
-                                        <span class="bp-done-note">Uploaded files: @publication.Files.Count of @publication.Files.Count unchanged</span>
+                                        <span class="bp-done-note">Documents: @publication.Files.Count, all unchanged since they were uploaded</span>
                                     }
                                     else
                                     {
-                                        <span class="bp-missing-note">Uploaded files: @filesChanged changed since upload</span>
+                                        <span class="bp-missing-note">Documents: @filesChanged of @publication.Files.Count changed since they were uploaded</span>
                                     }
                                 </summary>
                                 <ul class="bp-file-checks">
@@ -316,7 +311,7 @@
                                         </li>
                                     }
                                 </ul>
-                                <small class="bp-field-help">Each file is checked against the SHA-256 fingerprint taken when it was uploaded.</small>
+                                <small class="bp-field-help">Each document is checked against the SHA-256 fingerprint taken when it was uploaded.</small>
                             </details>
                         }
                     </div>
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 7dca582..4bc707c 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -112,14 +112,7 @@
                     <span class="bp-summary-title">Overview</span>
                     <h2>@(active is null ? "Not published" : $"Published · version {active.Number}")</h2>
                     <p class="mb-0">Your cluster's battery. Change its status after a repair or when it's used again, keep its live values and accident reports, or hand it over to another customer. The team keeps its passport values.</p>
-                    <dl class="bp-overview">
-                        @if (Model.Status is { } status)
-                        {
-                            <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-swap" /></svg>Battery status</dt><dd>@status.StatusWords<small>@(status.Since is { } since ? $"since {ValueWords.Time(since)}{(status.AfterRepair ? ", after a repair" : string.Empty)}" : "since it was made")</small></dd></div>
-                        }
-                        <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-live" /></svg>Live values</dt><dd>@BatterySections.LiveHint(Model.Pack, Model.Live)<small>Measured on the battery</small></dd></div>
-                        <div><dt><svg class="bp-i" aria-hidden="true"><use href="#i-accident" /></svg>Accident reports</dt><dd>@(Model.AccidentReports.Count == 0 ? "None reported" : Words.Count(Model.AccidentReports.Count, "report"))</dd></div>
-                    </dl>
+                    @await Html.PartialAsync("~/Views/Batteries/_LiveTiles.cshtml", Model.Tiles)
                 </section>
             </div>
             @if (Model.Status is { } statusCard)
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 83a42bf..1f79050 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3197,6 +3197,63 @@ main .bp-field-marker.is-published {
   margin-top: 0 !important;
 }
 
+/* The overview's tiles: each live value with a small line of its last 7 days (owner's improvement 4). */
+.bp-tiles {
+  display: grid;
+  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
+  gap: 12px;
+  margin: 16px 0 4px;
+}
+
+.bp-tile {
+  display: grid;
+  gap: 2px;
+  padding: 12px 14px;
+  border: 1px solid var(--bp-border);
+  border-radius: 12px;
+}
+
+.bp-tile-title {
+  color: var(--bp-muted);
+  font-size: 0.74rem;
+  font-weight: 650;
+  letter-spacing: 0.04em;
+  text-transform: uppercase;
+}
+
+.bp-tile-value {
+  font-size: 1.35rem;
+  font-weight: 700;
+}
+
+.bp-spark {
+  width: 100%;
+  height: 36px;
+  margin: 2px 0;
+}
+
+.bp-spark polyline {
+  fill: none;
+  stroke: var(--bp-primary);
+  stroke-width: 2;
+  stroke-linejoin: round;
+  stroke-linecap: round;
+  vector-effect: non-scaling-stroke;
+}
+
+.bp-tile-flat {
+  display: flex;
+  align-items: center;
+  min-height: 36px;
+  color: var(--bp-muted);
+  font-size: 0.8rem;
+}
+
+.bp-tile small {
+  color: var(--bp-muted);
+  font-size: 0.76rem;
+}
+
 /* The overview's "Nothing needs you" or what needs doing. */
 .bp-todo {
   display: grid;
```

`web/Dpp/Pages/LiveTrends.cs`:

```csharp
using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>One reading of a tile's trend line: when it was measured, and the value.</summary>
public sealed record TrendPoint(DateTime AtUtc, decimal Value);

/// <summary>
/// One of the battery overview's tiles (redesign R4, the owner's improvement 4): the value now, and how it went over the
/// last 7 days, drawn as a small line.
/// </summary>
/// <param name="Note">What the small print says under the value: when it was measured, or how the value changes.</param>
public sealed record LiveTile(string Title, string Property, decimal? Value, string Unit, string Note, IReadOnlyList<TrendPoint> Points)
{
    /// <summary>The value with its unit, or a dash before there is one.</summary>
    public string Shown => Value is { } value ? $"{ValueWords.Number(value)}{(Unit.Length == 0 ? string.Empty : $" {Unit}")}" : "—";

    /// <summary>The trend in words, for readers who can't see the line.</summary>
    public string TrendWords => Points.Count < 2
        ? "No change in the last 7 days"
        : $"From {ValueWords.Number(Points[0].Value)} to {ValueWords.Number(Points[^1].Value)} over the last 7 days";

    /// <summary>
    /// The line's points in a <paramref name="width"/> × <paramref name="height"/> box, oldest on the left and the highest
    /// value at the top; null with fewer than two readings, when there is no line to draw.
    /// </summary>
    public string? Line(int width, int height)
    {
        if (Points.Count < 2)
        {
            return null;
        }

        var first = Points[0].AtUtc.Ticks;
        var span = Math.Max(1, Points[^1].AtUtc.Ticks - first);
        var low = Points.Min(point => point.Value);
        var high = Points.Max(point => point.Value);
        var range = high - low;
        return string.Join(" ", Points.Select(point =>
        {
            var x = (double)(point.AtUtc.Ticks - first) / span * width;
            var y = range == 0 ? height / 2.0 : height - 2 - (double)((point.Value - low) / range) * (height - 4);
            return $"{x.ToString("0.#", CultureInfo.InvariantCulture)},{y.ToString("0.#", CultureInfo.InvariantCulture)}";
        }));
    }
}

/// <summary>
/// The battery overview's tiles: state of charge, full cycles, temperature and capacity fade, each with its last 7 days
/// read from the change log, which keeps every saved value with its measurement time (like the live history, decision 30).
/// They are shown on the team's and the cluster admin's battery pages, whose readers may see these values.
/// </summary>
public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock)
{
    public const int Hours = 168;

    private static readonly (int Number, string Title, string Unit)[] Tiles =
    [
        (63, "State of charge", "%"),
        (83, "Full cycles", ""),
        (87, "Temperature", "°C"),
        (59, "Capacity fade", "%"),
    ];

    public async Task<IReadOnlyList<LiveTile>> TilesAsync(BatteryPack pack, BatteryModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(model);
        var sinceUtc = clock.GetUtcNow().UtcDateTime.AddHours(-Hours);
        var wanted = Tiles.Select(tile => AttributeCatalog.ByNumber(tile.Number).Property).ToHashSet(StringComparer.Ordinal);
        var points = new Dictionary<string, List<TrendPoint>>(StringComparer.Ordinal);
        foreach (var change in await audit.ListEntityEventsAsync(SavePipeline.AuditEventType, "dpp-pack", pack.Id, sinceUtc, cancellationToken))
        {
            var savedAt = Moment(BsonHelpers.GetString(change, "createdAt")) ?? sinceUtc;
            var keys = change.GetValue("metadata", new BsonDocument()).AsBsonDocument.GetValue("keys", new BsonArray()).AsBsonArray;
            foreach (var key in keys.OfType<BsonDocument>())
            {
                var property = key.GetValue("key", string.Empty).ToString()!;
                if (key.GetValue("outcome", string.Empty) != KeyOutcome.Saved.ToString() || !wanted.Contains(property)
                    || key.GetValue("value", BsonNull.Value) is not BsonString text || Number(JsonNode.Parse(text.Value)) is not { } value)
                {
                    continue;
                }

                var measuredAt = Moment(key.GetValue("generatedAt", BsonNull.Value) is BsonString generated ? generated.Value : null) ?? savedAt;
                if (measuredAt >= sinceUtc)
                {
                    (points.TryGetValue(property, out var list) ? list : points[property] = []).Add(new TrendPoint(measuredAt, value));
                }
            }
        }

        return Tiles.Select(tile =>
        {
            var property = AttributeCatalog.ByNumber(tile.Number).Property;
            var stored = pack.Values.GetValueOrDefault(property) ?? model.Values.GetValueOrDefault(property);
            var note = tile.Number == 59
                ? "Re-stated at each status change"
                : stored is null ? "Not measured yet" : $"Measured {ValueWords.Time(stored.GeneratedAtUtc)}";
            var line = points.GetValueOrDefault(property)?.OrderBy(point => point.AtUtc).ToArray() ?? [];
            return new LiveTile(tile.Title, property, Number(stored?.Value), tile.Unit, note, line);
        }).ToArray();
    }

    /// <summary>
    /// A value's number: a plain number (#83, cycles), or the number inside the schema's small object, such as
    /// { "percentUnit": "%", "percentageValue": 76 } (#63, #59) or { "degreeCelsiusUnit": "°C", "celsiusValue": 23 } (#87).
    /// </summary>
    private static decimal? Number(JsonNode? node) => node switch
    {
        // Whatever .NET type holds it (int, double, decimal), a JSON number reads the same through its text.
        JsonValue value when value.GetValueKind() == System.Text.Json.JsonValueKind.Number => decimal.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture),
        JsonValue text when text.TryGetValue<string>(out var words) && decimal.TryParse(words, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
        JsonObject inner => inner.Select(pair => Number(pair.Value)).FirstOrDefault(number => number is not null),
        _ => null,
    };

    private static DateTime? Moment(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment) ? moment.UtcDateTime : null;
}
```

`web/Views/Batteries/_LiveTiles.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model IReadOnlyList<LiveTile>
@* The overview's tiles (redesign R4, the owner's improvement 4): each value now, with a small line of its last 7 days. *@
@if (Model.Count > 0)
{
    <div class="bp-tiles">
        @foreach (var tile in Model)
        {
            <div class="bp-tile">
                <span class="bp-tile-title">@tile.Title</span>
                <strong class="bp-tile-value">@tile.Shown</strong>
                @if (tile.Line(140, 36) is { } line)
                {
                    <svg class="bp-spark" viewBox="0 0 140 36" preserveAspectRatio="none" role="img" aria-label="@tile.TrendWords"><polyline points="@line" /></svg>
                }
                else
                {
                    <span class="bp-tile-flat">@tile.TrendWords</span>
                }
                <small>@tile.Note</small>
            </div>
        }
    </div>
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~LiveTrendsTests"`
Expected: `Passed!  - Failed:     0, Passed:     3`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2328`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/LiveTrendsTests.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Dpp/Pages/LiveTrends.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_LiveTiles.cshtml web/Views/ClusterAdmin/Battery.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): the battery overview shows the live values with their last 7 days" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2328, Skipped:     0, Total:  2328`. From the 2303 before: 2316 after Task 1, 2325 after Task 2, 2328 after Task 3.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 4f65caa -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

1. **Restart the app with the usual launcher** and press "Reset demo data".
2. **DEMO-C7M-0001 (Batteries), on the phone and the PC:**
   - The header shows the model's picture as a column on the left, from the label down to the facts, then the Battery and Passport badges and the five labelled links. Change status, Add a note and More sit under the facts.
   - The page opens on the overview. Tapping "Live values" shows only that section; saving one comes back to it with "Saved".
   - "Add a note" opens its window from the header; the note shows in "Status & repairs".
3. **DEMO-C7M-0003:** a red line at the top with the reason and "Undo: make it active again".
4. **DEMO-C13M-0001** (the earlier passport): a "New passport" chip and one line, no banner.
5. **As the demo North admin:** Registry → Open on DEMO-C7M-0001 shows the same layout, with "Hand over" in the menu.
6. **As the demo Fleet admin:** DEMO-CORE-0001's page has the line "Hand-over to Demo North Operations waiting for their answer", with "Cancel the hand-over".

## Next

Step 5 resumes. Plan 5b is unchanged by the redesign. Plan 5c's "Upload a file" card is reworked onto the new page as one more section after "Live values", before it is committed.
