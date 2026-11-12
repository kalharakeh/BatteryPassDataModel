# DPP V3 Redesign R6b: A Read-Only Page for Marked Batteries — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two small changes from the page review of the built R6. Each task ends with a working app:

1. **A marked battery's page for its cluster admin, read-only** (task 1). A battery whose passport is marked invalid or for deletion had no page for its cluster admin: the Registry row had no pencil and `/cluster-admin/batteries/{id}` answered "not found", because the page needed an active passport. So the cluster admin couldn't see the battery's History, its passports or why it was frozen. The owning cluster admin now gets the page, read-only:
   - the mark's fact and date under the header ("Invalid since 3 Nov 2026, 13:35. Nothing on the passport changes until the team undoes the mark, so this page is to read only."), never its reason (decision 34);
   - the passport badge and the Overview say "Invalid · version 1" (or "Marked for deletion · version 2");
   - the side menu: Overview, Status & repairs, Live values, Modules, Versions, History. No Accident reports section (its reports stay on the Accident page, linked in the header) and no Hand over;
   - no forms, no windows, no Change status, Add a note or Hand over buttons, no upload, no More menu;
   - Live values lists the last values with when and how each was measured, to read; this frozen card is the team's too, so the team's page of a marked or inactive battery shows them as well, and its card no longer says "type them here";
   - the detail page's "Open the battery page" leads there, for the cluster admin and the team.
   Every change to the battery is still refused (live values, accident reports, status, notes, another operator, hand-over). Only the cluster's own battery; another cluster's admin and a passport a newer one replaced (inactive) still get nothing. The History and the passports list keep their R6 rules.
2. **No passport chips beside the passports list** (task 2, the owner's rule: no duplicates). On the team's battery page, "Passports of this battery" lists every passport of a battery placed on the market again; the hero's "Earlier passport" and "New passport" chips led to the same passports, so they show only when there is no list. The public page has no list and keeps its "Earlier passport" link.

**Architecture:**
- `DppStatuses.IsMarked` (task 1): invalid or marked for deletion, the battery's newest passport frozen by a mark.
- `ClusterBatteryPages.ReadableAsync` (task 1): the cluster's own battery whose newest passport is active, or marked (with the mark). The page (`BatteryAsync`) and its links (`ManageHrefAsync`) use it; every change still goes through `ManagedAsync`, which now asks `ReadableAsync` for an active passport only. `ClusterBatteryPage` gets `FrozenAs`, `MarkedSince` and `ReadOnly`; its cards are frozen then and its side menu drops Accident reports and Hand over. `RegistryList.ManageHref` and `PassportController.Detail` offer the page for a marked passport too.
- `_LiveValuesCard` (task 1): a frozen card lists the last values (`data-frozen-values`).
- `Batteries/Battery.cshtml` (task 2): the chips only while `Model.Chain` is empty.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** the page review of the built R6 (3 Nov 2026, gaps 5 and 6) and the product owner's answer: "Yes, read-only page" — "give cluster admins a read-only battery page for batteries marked invalid or for deletion (mark notice, Status, Live values, Modules, Versions, History; no forms, no hand-over)". Gap 6 is the managing session's call under the owner's rule of no duplicates. Decision 34 stands: a mark's reason stays the team's and market surveillance's.

**Learning-mode contribution spot:** `ClusterBatteryPages.ReadableAsync` (task 1), which passport statuses a cluster admin may read; its tests say what it must do.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at a78cd9b (R1–R6 as built, with the History fix a78cd9b). Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas** or `battery_pass_v3`. The tests use a throwaway MongoDB (EphemeralMongo).
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Each task runs only its own tests** (the filter in its steps); **the whole suite runs once**, in the final checks. Never run two whole suites at once.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started (by their process IDs, never by name) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2429`. After this plan: 2438. The suite takes about three minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../r6b-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole; a removed file has its `git rm`.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.** The new styles go to `bp-product-data.css` (battery pages and lists) and `bp-status.css` (every page).
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## What happens to the old tests

- `PassportMarkPagesTests` (task 1): the Registry now gives an invalid battery's cluster admin its page (it asserted no link: "nothing on it changes, so there is nothing to manage"); the test is renamed `TheRegistry_ShouldKeepAnInvalidBatteryWithItsStatus_AndItsReadOnlyPage`.
- `ClusterBatteriesControllerTests` (task 1) gains a test that a marked battery opens read-only and refuses every post.
- Nothing is removed.

---

### Task 1: A marked battery's page for its cluster admin, read-only

A battery whose passport is marked invalid or for deletion opens for its cluster admin, read-only: the mark's fact and date, Status, Live values, Modules, Versions and History; no forms, windows or hand-over. Every change is still refused; another cluster's admin and a replaced passport still get nothing.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/ReadOnlyClusterPageTests.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Modify: `web/Dpp/Pages/RegistryList.cs`
- Modify: `web/Dpp/Store/PassportVersion.cs`
- Modify: `web/Views/Batteries/_LiveValuesCard.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r6b-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`). Save the block as `../r6b-task1-tests.patch`, outside the worktree, and run `git apply ../r6b-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs b/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs
index c8a8d30..45c3f19 100644
--- a/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs
@@ -176,6 +176,28 @@ public sealed class ClusterBatteriesControllerTests(MongoFixture mongo)
         return Assert.IsType<ViewResult>(await controller.Detail(PassportPagesTests.Identifier(rig), CancellationToken.None, manage: ClusterBatteryPagesTests.Pages(rig)));
     }
 
+    [Fact]
+    public async Task AMarkedBattery_ShouldOpenReadOnly_AndRefuseEveryPost()
+    {
+        // Redesign R6b: the page opens read-only for a passport marked invalid; every change is still refused.
+        var rig = await RigAsync();
+        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Demo: wrong figures"));
+        var live = await LiveFormAsync(rig, "temperatureInformation", "21");
+
+        var page = Assert.IsType<ClusterBatteryPage>(Assert.IsType<ViewResult>(await Controller(rig, NorthAdmin, "clusterAdmin").Battery(rig.PackId, CancellationToken.None)).Model);
+        Assert.True(page.ReadOnly);
+        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), (await DetailAsync(rig, NorthAdmin, "clusterAdmin")).ViewData[ClusterBatteryPages.ManageHrefKey]); // the detail page leads to it
+        Assert.Null((await DetailAsync(rig, SouthAdmin, "clusterAdmin")).ViewData[ClusterBatteryPages.ManageHrefKey]);
+        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin", live).SaveLiveValues(rig.PackId, CancellationToken.None));
+        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin").AddAccidentReport(rig.PackId, Upload("report.pdf", "%PDF-1.4 report"u8.ToArray()), "Report", "en", CancellationToken.None));
+        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin").ChangeStatus(rig.PackId, "Repaired", "2026-11-03", "A repair.", CancellationToken.None));
+        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin").AddHistoryNote(rig.PackId, "A note.", CancellationToken.None));
+        Assert.IsType<NotFoundResult>(await Controller(rig, NorthAdmin, "clusterAdmin").OtherOperator(rig.PackId, "Other", "Somewhere", null, false, CancellationToken.None));
+        await Controller(rig, NorthAdmin, "clusterAdmin").HandOver(rig.PackId, BatteryTesting.South, BatteryTesting.South, HandOverTests.Service(rig), CancellationToken.None);
+        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
+        Assert.NotEqual(21, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["temperatureInformation"].Value!["celsiusValue"]!.GetValue<int>());
+    }
+
     private async Task<PublicationRig> RigAsync()
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
diff --git a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
index 8054cb4..d14c531 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
@@ -122,7 +122,7 @@ public sealed class PassportMarkPagesTests(MongoFixture mongo)
     }
 
     [Fact]
-    public async Task TheRegistry_ShouldKeepAnInvalidBatteryWithItsStatusButNoManageLink()
+    public async Task TheRegistry_ShouldKeepAnInvalidBatteryWithItsStatus_AndItsReadOnlyPage()
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
         await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, Reason);
@@ -134,7 +134,8 @@ public sealed class PassportMarkPagesTests(MongoFixture mongo)
 
         var row = Assert.Single(surveillance.Rows);
         Assert.Equal(DppStatuses.Invalid, row.StatusName);
-        Assert.Null(admin.ManageHref(Assert.Single(admin.Rows))); // nothing on it changes, so there is nothing to manage
+        // Nothing on it changes, but its cluster admin reads its page, read-only (redesign R6b, the owner's choice).
+        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), admin.ManageHref(Assert.Single(admin.Rows)));
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Dpp/ReadOnlyClusterPageTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6b, the owner's choice (3 Nov 2026, "Yes, read-only page"): a battery whose passport is marked invalid or
/// for deletion had no page for its cluster admin (only an active passport did), so they couldn't see its History, its
/// passports or why it was frozen. Its cluster admin now gets the page read-only: the mark, Status, Live values,
/// Modules, Versions and History; no forms, no hand-over, no accident upload. Every change is still refused. Another
/// cluster's admin, and an archived or replaced passport, still get nothing.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ReadOnlyClusterPageTests(MongoFixture mongo)
{
    private const string Reason = "Demo: declared values don't match the test report";
    private static readonly ChangeSender Team = PublicationTesting.Team;
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);
    private static readonly ChangeSender SouthAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

    private async Task<PublicationRig> MarkedAsync(bool forDeletion)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        if (forDeletion)
        {
            await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
            Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(Team, rig.PackId));
        }
        else
        {
            Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(Team, rig.PackId, Reason));
        }

        return rig;
    }

    [Theory]
    [InlineData(false, DppStatuses.Invalid)]
    [InlineData(true, DppStatuses.MarkedForDeletion)]
    public async Task AMarkedBattery_ShouldOpenReadOnlyForItsClustersAdmin(bool forDeletion, string status)
    {
        var rig = await MarkedAsync(forDeletion);
        var pages = ClusterBatteryPagesTests.Pages(rig);

        var page = await pages.BatteryAsync(NorthAdmin, rig.PackId);

        Assert.NotNull(page);
        Assert.Equal(status, page.FrozenAs);
        Assert.True(page.ReadOnly);
        Assert.Equal(["overview", "status", "live", "modules", "versions", "history"], page.Sections.Select(section => section.Id));
        Assert.True(page.Cards.Frozen);
        Assert.False(page.Status!.CanChange);
        Assert.False(page.Status.MayAddNote);
        Assert.NotNull(page.MarkedSince);
        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), await pages.ManageHrefAsync(NorthAdmin, rig.PackId));
        Assert.Null(await pages.BatteryAsync(SouthAdmin, rig.PackId)); // only the cluster's own battery
        Assert.Null(await pages.ManageHrefAsync(SouthAdmin, rig.PackId));
    }

    [Fact]
    public async Task AnActiveBattery_ShouldStayEditable()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(NorthAdmin, rig.PackId))!;

        Assert.False(page.ReadOnly);
        Assert.Null(page.MarkedSince);
        Assert.Contains("handover", page.Sections.Select(section => section.Id));
        Assert.Contains("accidents", page.Sections.Select(section => section.Id));
    }

    [Fact]
    public async Task AMarkedBattery_ShouldRefuseEveryChange()
    {
        var rig = await MarkedAsync(forDeletion: false);
        var pages = ClusterBatteryPagesTests.Pages(rig);
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("stateOfChargeSoC")] = "42";

        Assert.Null(await pages.SaveLiveAsync(NorthAdmin, rig.PackId, PageTesting.Field(form), PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null), null));
        Assert.Null(await pages.AddAccidentReportAsync(NorthAdmin, rig.PackId, new DocumentUpload("report.pdf", "%PDF-1.4 report"u8.ToArray(), "Report", "en")));
        Assert.Null(await pages.ChangeStatusAsync(NorthAdmin, rig.PackId, nameof(StatusEvent.Repaired), _ => null, "2026-11-03", "A repair."));
        Assert.Null(await pages.AddHistoryNoteAsync(NorthAdmin, rig.PackId, "A note."));
        Assert.Null(await pages.OtherOperatorAsync(NorthAdmin, rig.PackId, new OtherOperator("Other operator", "Somewhere", null), link: false));
        Assert.NotNull((await HandOverTests.Service(rig).AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true)).Problem);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
        Assert.Equal(stored["stateOfChargeSoC"].Value!.ToJsonString(), (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!.ToJsonString());
    }

    [Fact]
    public async Task AReplacedPassport_ShouldStillGetNoPage()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        await rig.Store.SetPackClusterAsync(rig.PackId, BatteryTesting.North);

        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
        Assert.Null(await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(NorthAdmin, rig.PackId));
    }

    [Fact]
    public async Task TheRegistry_ShouldGiveAMarkedBatterysAdminItsPage()
    {
        var rig = await MarkedAsync(forDeletion: false);
        var batteries = await BatteryTesting.Pages(rig).ListAsync(null);

        var north = RegistryList.Build(Reader.With("admin", ReaderRole.ClusterAdmin, BatteryTesting.North), batteries, NorthAdmin);
        var south = RegistryList.Build(Reader.With("admin", ReaderRole.ClusterAdmin, BatteryTesting.South), batteries, SouthAdmin);

        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), north.ManageHref(Assert.Single(north.Rows)));
        Assert.All(south.Rows, row => Assert.Null(south.ManageHref(row)));
    }

    [Fact]
    public void TheReadOnlyPage_ShouldOfferNothingThatChangesTheBattery()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var live = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_LiveValuesCard.cshtml"));

        // The mark's fact and date, without the reason (decision 34: the team's and market surveillance's).
        Assert.Contains("id=\"mark-notice\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Reason", view, StringComparison.Ordinal);
        // No hand-over button or windows on a read-only page.
        Assert.Contains("@if (!Model.ReadOnly && Model.HandOver.Waiting is null)\n            {\n                @* Opens the window directly", view, StringComparison.Ordinal);
        Assert.Contains("@if (!Model.ReadOnly && Model.Status is { } statusWindows)", view, StringComparison.Ordinal);
        Assert.Contains("@if (!Model.ReadOnly && Model.HandOver.Waiting is null)\n    {\n        <div class=\"bp-console-form-card bp-dialog-host\">", view, StringComparison.Ordinal);
        // A frozen passport's live values still show, read-only, with when each was measured.
        Assert.Contains("data-frozen-values", live, StringComparison.Ordinal);
        Assert.Contains("@if (Model.Frozen)", live, StringComparison.Ordinal); // no "type them here" on a frozen passport
        Assert.Contains("The last values measured on the battery, to read: nothing on the passport changes any more.", live, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~ClusterBatteriesControllerTests | FullyQualifiedName~PassportMarkPagesTests | FullyQualifiedName~ReadOnlyClusterPageTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'ClusterBatteryPage' does not contain a definition for 'FrozenAs'`).

- [ ] **Step 3: Write the code**

Patch `r6b-task1-code.patch` (`web/Controllers/PassportController.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Dpp/Pages/RegistryList.cs`, `web/Dpp/Store/PassportVersion.cs`, `web/Views/Batteries/_LiveValuesCard.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`). Save the block as `../r6b-task1-code.patch`, outside the worktree, and run `git apply ../r6b-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/PassportController.cs b/web/Controllers/PassportController.cs
index c70b0da..62e5cfa 100644
--- a/web/Controllers/PassportController.cs
+++ b/web/Controllers/PassportController.cs
@@ -75,8 +75,8 @@ public sealed class PassportController(DppReaders readers, PassportPages pages)
             ViewData["LiveHistory"] = await liveHistory.HistoryAsync(reader, page.Version.PackId, BatteryApi.MaxHistoryHours, cancellationToken);
         }
 
-        // The battery's cluster admins manage it from here too (step 4a).
-        if (manage is not null && page.IsCurrent)
+        // The battery's cluster admins manage it from here too (step 4a); read it while its passport is marked (R6b).
+        if (manage is not null && (page.IsCurrent || BatteryPassWeb.Dpp.Store.DppStatuses.IsMarked(page.Version.DppStatus)))
         {
             ViewData[ClusterBatteryPages.ManageHrefKey] = await manage.ManageHrefAsync(await readers.EditorForAsync(User, cancellationToken), page.Version.PackId, cancellationToken);
         }
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index f71cafd..221b784 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -38,8 +38,17 @@ public sealed record ClusterBatteryPage(
     /// <summary>What happened while the battery was in this cluster, as its admin may read it (redesign R6), newest first.</summary>
     public IReadOnlyList<BatteryPassWeb.Dpp.Audit.AuditLine> History { get; init; } = [];
 
-    /// <summary>The team page's cards, without what only the team does (step 4a).</summary>
-    public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false);
+    /// <summary>The mark that froze the passport (invalid, marked for deletion); null while it is active (redesign R6b).</summary>
+    public string? FrozenAs { get; init; }
+
+    /// <summary>When the passport was marked; its reason is the team's and market surveillance's (decision 34).</summary>
+    public DateTime? MarkedSince { get; init; }
+
+    /// <summary>A marked passport's page: to read only, nothing on it changes (redesign R6b, the owner's choice).</summary>
+    public bool ReadOnly => FrozenAs is not null;
+
+    /// <summary>The team page's cards, without what only the team does (step 4a); frozen while the passport is marked.</summary>
+    public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false, FrozenAs: FrozenAs);
 
     /// <summary>The header's thumbnail, as on the team's page.</summary>
     public string? PictureHref => BatteryPages.PictureHref(Sources.Model, Family);
@@ -56,8 +65,12 @@ public sealed record ClusterBatteryPage(
             }
 
             sections.Add(new("live", "Live values", "live", BatterySections.LiveHint(Pack, Live)));
-            sections.Add(new("accidents", "Accident reports", "accident", Count: AccidentReports.Count));
-            sections.Add(new("handover", "Hand over", "handover", HandOver.Waiting is null ? "To another cluster" : "Waiting for their answer"));
+            if (!ReadOnly) // a marked passport takes no report and can't be handed over; its reports stay on the Accident page
+            {
+                sections.Add(new("accidents", "Accident reports", "accident", Count: AccidentReports.Count));
+                sections.Add(new("handover", "Hand over", "handover", HandOver.Waiting is null ? "To another cluster" : "Waiting for their answer"));
+            }
+
             sections.Add(new("modules", "Modules", "modules", Count: Pack.Modules.Count));
             sections.Add(new("versions", "Versions", "versions", Count: Versions.Count));
             sections.Add(new("history", "History", "history", "While it was your cluster's")); // redesign R6
@@ -73,6 +86,7 @@ public sealed record HandOverCardView(string Action, string SerialNumber, HandOv
 /// The cluster admin's battery page (design 6.1 and 12, step 4a). A cluster admin manages only a battery of a cluster
 /// they are admin of, and only once its passport is published and active: a draft is the team's (J-6). Every change
 /// goes through the save pipeline as the cluster admin, which allows the live values and accident reports (design 6.1).
+/// A passport marked invalid or for deletion opens read-only, every change still refused (redesign R6b).
 /// </summary>
 public sealed class ClusterBatteryPages(
     DppStore store,
@@ -96,12 +110,12 @@ public sealed class ClusterBatteryPages(
     public async Task<string?> ManageHrefAsync(ChangeSender? sender, string packId, CancellationToken cancellationToken = default) =>
         sender is null ? null
         : sender.Kind == SenderKind.Team ? (await store.GetAsync<BatteryPack>(packId, cancellationToken) is null ? null : BatteryPages.BatteryHref(packId))
-        : await ManagedAsync(sender, packId, cancellationToken) is not null ? BatteryHref(packId) : null;
+        : await ReadableAsync(sender, packId, cancellationToken) is not null ? BatteryHref(packId) : null;
 
     /// <summary>The battery's page; with <paramref name="live"/>, the live form as typed after a refused save. Null when the sender may not manage it.</summary>
     public async Task<ClusterBatteryPage?> BatteryAsync(ChangeSender sender, string packId, EditorState? live = null, CancellationToken cancellationToken = default)
     {
-        if (await ManagedAsync(sender, packId, cancellationToken) is not { } pack)
+        if (await ReadableAsync(sender, packId, cancellationToken) is not ({ } pack, var frozenAs))
         {
             return null;
         }
@@ -147,11 +161,13 @@ public sealed class ClusterBatteryPages(
             versions.OrderByDescending(version => version.Number).Select(version => new VersionRow(version, PassportReader.Verify(version))).ToArray(),
             await documentStore.ListAsync(TargetKind.Pack, pack.Id, DocumentService.Accidents, cancellationToken))
         {
-            Status = await status.CardAsync(sources, active: true, href, forTeam: false, cancellationToken: cancellationToken),
+            Status = await status.CardAsync(sources, active: frozenAs is null, href, forTeam: false, cancellationToken: cancellationToken),
             Tiles = trends is null ? [] : await trends.TilesAsync(sources.Pack, sources.Model, cancellationToken),
             Chain = await PassportChains.ForAsync(store, pack, cancellationToken),
             HandOver = new HandOverCardView($"{href}/handover", serial, waiting, waiting is null ? null : names.GetValueOrDefault(waiting.ToClusterId) ?? waiting.ToClusterId),
             History = history is null ? [] : await history.ForAsync(pack, sender.ClusterIds, cancellationToken),
+            FrozenAs = frozenAs,
+            MarkedSince = frozenAs is null ? null : pack.Mark?.AtUtc,
         };
     }
 
@@ -211,7 +227,15 @@ public sealed class ClusterBatteryPages(
     }
 
     /// <summary>The pack, when it belongs to a cluster the sender is admin of and its passport is published and active.</summary>
-    private async Task<BatteryPack?> ManagedAsync(ChangeSender sender, string packId, CancellationToken cancellationToken)
+    /// <summary>A battery of the sender's cluster whose passport is active: the one the cluster admin may change.</summary>
+    private async Task<BatteryPack?> ManagedAsync(ChangeSender sender, string packId, CancellationToken cancellationToken) =>
+        await ReadableAsync(sender, packId, cancellationToken) is (var pack, null) ? pack : null;
+
+    /// <summary>
+    /// A battery of the sender's cluster whose newest passport is active, or marked invalid or for deletion; the mark, or
+    /// null while it is active. A draft, or a passport a newer one replaced, gives nothing (redesign R6b).
+    /// </summary>
+    private async Task<(BatteryPack Pack, string? FrozenAs)?> ReadableAsync(ChangeSender sender, string packId, CancellationToken cancellationToken)
     {
         ArgumentNullException.ThrowIfNull(sender);
         if (sender.Kind != SenderKind.ClusterAdmin
@@ -221,6 +245,9 @@ public sealed class ClusterBatteryPages(
             return null;
         }
 
-        return await store.GetActiveVersionAsync(pack.Id, cancellationToken) is null ? null : pack;
+        var status = await store.PassportStatusAsync(pack.Id, cancellationToken);
+        return status == DppStatuses.Active ? (pack, null)
+            : DppStatuses.IsMarked(status) ? (pack, status)
+            : null;
     }
 }
diff --git a/web/Dpp/Pages/RegistryList.cs b/web/Dpp/Pages/RegistryList.cs
index 4dcc4e0..79e9cfb 100644
--- a/web/Dpp/Pages/RegistryList.cs
+++ b/web/Dpp/Pages/RegistryList.cs
@@ -16,11 +16,16 @@ public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query,
     /// <summary>The batteries whose status (#22, purchaser data) the reader may see: the team, market surveillance, their cluster's (redesign R3).</summary>
     public IReadOnlySet<string> BatteryStatusShown { get; init; } = new HashSet<string>(StringComparer.Ordinal);
 
-    /// <summary>The cluster admin's page of a battery of a cluster they are admin of; null otherwise.</summary>
+    /// <summary>
+    /// The cluster admin's page of a battery of a cluster they are admin of, while its passport is active or marked (read
+    /// only then, redesign R6b); null otherwise.
+    /// </summary>
     public string? ManageHref(BatteryRow row)
     {
         ArgumentNullException.ThrowIfNull(row);
-        return row.Active is not null && row.ClusterId is { } cluster && ManagedClusterIds.Contains(cluster) ? ClusterBatteryPages.BatteryHref(row.PackId) : null;
+        return (row.Active is not null || DppStatuses.IsMarked(row.Latest?.DppStatus)) && row.ClusterId is { } cluster && ManagedClusterIds.Contains(cluster)
+            ? ClusterBatteryPages.BatteryHref(row.PackId)
+            : null;
     }
 
     /// <summary>True for the team: its rows open the team's battery page and its history (redesign R5).</summary>
diff --git a/web/Dpp/Store/PassportVersion.cs b/web/Dpp/Store/PassportVersion.cs
index 77c9b3e..092a4b3 100644
--- a/web/Dpp/Store/PassportVersion.cs
+++ b/web/Dpp/Store/PassportVersion.cs
@@ -23,6 +23,9 @@ public static class DppStatuses
     /// <summary>A passport no longer active (step 4c): it takes no changes and gets no new version.</summary>
     public static bool IsNoLongerActive(string? status) => status is Inactive or Invalid or MarkedForDeletion;
 
+    /// <summary>Frozen by a mark (step 4c-2): still the battery's newest passport, unlike an inactive one (redesign R6b).</summary>
+    public static bool IsMarked(string? status) => status is Invalid or MarkedForDeletion;
+
     /// <summary>A DPP status in words, for pills and tables: "Active", "Marked for deletion".</summary>
     public static string Words(string status) => status switch
     {
diff --git a/web/Views/Batteries/_LiveValuesCard.cshtml b/web/Views/Batteries/_LiveValuesCard.cshtml
index 3fb268e..643534b 100644
--- a/web/Views/Batteries/_LiveValuesCard.cshtml
+++ b/web/Views/Batteries/_LiveValuesCard.cshtml
@@ -6,7 +6,11 @@
     <span class="bp-summary-title">Live values</span>
     <h2>Measured on the battery</h2>
     <p class="mb-0">
-        @if (Model.ForTeam)
+        @if (Model.Frozen)
+        {
+            <text>The last values measured on the battery, to read: nothing on the passport changes any more.</text>
+        }
+        else if (Model.ForTeam)
         {
             <text>Its battery system sends these; type them here before it does, for example the end-of-line values, or to
             correct one. They change the passport in place, without a new version.</text>
@@ -21,6 +25,19 @@
     @if (Model.FrozenNote is { } frozen)
     {
         <p class="bp-help-text mt-2 mb-0" data-frozen>@frozen</p>
+        @* The last values still show, to read (redesign R6b): what the battery last measured, and when. *@
+        <dl class="bp-bh-facts bp-frozen-values mt-3" data-frozen-values>
+            @foreach (var field in Model.Live.Sections.SelectMany(section => section.Fields))
+            {
+                @if (Model.Pack.Values.GetValueOrDefault(field.Attribute.Property) is { Value: not null } stored)
+                {
+                    <div>
+                        <dt>@field.Attribute.Name <span class="bp-attr-number">@field.Attribute.Label</span></dt>
+                        <dd>@string.Join(", ", ValueWords.Lines(field.Form, stored.Value).Select(line => line.Text)) <small class="d-block text-secondary">@ValueWords.Time(stored.GeneratedAtUtc), @BatteryCards.Source(stored.Source)</small></dd>
+                    </div>
+                }
+            }
+        </dl>
     }
     else
     {
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 1fa37e7..82e9561 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -8,6 +8,8 @@
 @{
     ViewData["Title"] = $"Battery {Model.SerialNumber}";
     var active = Model.Versions.Select(row => row.Version).FirstOrDefault(version => version.DppStatus == BatteryPassWeb.Dpp.Store.DppStatuses.Active);
+    // A marked passport's newest version (redesign R6b): the page is read-only then.
+    var shown = active ?? (Model.ReadOnly ? Model.Versions.Select(row => row.Version).FirstOrDefault() : null);
     var batteryKey = Uri.EscapeDataString(Model.Identifier);
     var card = (TempData.Peek(BatteriesController.CardKey) ?? ViewData[BatteriesController.CardKey]) as string;
     var sections = Model.Sections;
@@ -39,7 +41,7 @@
                     {
                         @await Html.PartialAsync("_StatusBadge", battery)
                     }
-                    @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(active?.DppStatus).With(active is null ? null : $"· version {active.Number}"))
+                    @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(shown?.DppStatus).With(shown is null ? null : $"· version {shown.Number}"))
                     @if (testIds)
                     {
                         @await Html.PartialAsync("_TestTag", "Test ID")
@@ -68,9 +70,20 @@
             </div>
         </div>
 
-        @* The page opens only while the passport is active (step 4a): an invalid or inactive battery shows on the Registry
+        @* The page opens while the passport is active (step 4a), and read-only while it is marked invalid or for deletion
+           (redesign R6b): the mark's fact and date, not its reason (decision 34). An inactive battery shows on the Registry
            row and the detail page instead. A waiting hand-over is one line here, with its one Cancel (redesign R4). *@
-        @if (Model.HandOver.Waiting is { } waiting)
+        @if (Model.FrozenAs is { } frozen)
+        {
+            <p class="bp-status-notice" id="mark-notice">
+                <svg class="bp-i" aria-hidden="true"><use href="#i-shield" /></svg>
+                <span>
+                    <strong>@BatteryPassWeb.Dpp.Store.DppStatuses.Words(frozen) since @(Model.MarkedSince is { } since ? ValueWords.Time(since) : "—").</strong>
+                    Nothing on the passport changes until the team undoes the mark, so this page is to read only.
+                </span>
+            </p>
+        }
+        @if (!Model.ReadOnly && Model.HandOver.Waiting is { } waiting)
         {
             <form method="post" action="@Model.HandOver.Action/cancel" class="bp-status-notice is-quiet">
                 @Html.AntiForgeryToken()
@@ -93,7 +106,7 @@
                     }
                 </div>
             }
-            @if (Model.HandOver.Waiting is null)
+            @if (!Model.ReadOnly && Model.HandOver.Waiting is null)
             {
                 @* Opens the window directly, like marking a passport (redesign R5, the owner's answer c). *@
                 <button type="button" class="bp-secondary-button bp-with-icon" data-open-dialog="handover-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-handover" /></svg>Hand over…</button>
@@ -114,8 +127,16 @@
             <div data-section="overview" hidden="@Hidden("overview")">
                 <section class="bp-card bp-console-form-card bp-battery-card" id="overview">
                     <span class="bp-summary-title">Overview</span>
-                    <h2>@(active is null ? "Not published" : $"Published · version {active.Number}")</h2>
-                    <p class="mb-0">Your cluster's battery. Change its status after a repair or when it's used again, keep its live values and accident reports, or hand it over to another customer. The team keeps its passport values.</p>
+                    @if (Model.FrozenAs is { } frozenAs && shown is not null)
+                    {
+                        <h2>@BatteryPassWeb.Dpp.Store.DppStatuses.Words(frozenAs) · version @shown.Number</h2>
+                        <p class="mb-0">Your cluster's battery, to read only: its passport is @BatteryPassWeb.Dpp.Store.DppStatuses.Words(frozenAs).ToLowerInvariant(), so nothing on it changes until the team undoes the mark. Its status, live values, modules, versions and history stay here; its accident reports are on the Accident page.</p>
+                    }
+                    else
+                    {
+                        <h2>@(active is null ? "Not published" : $"Published · version {active.Number}")</h2>
+                        <p class="mb-0">Your cluster's battery. Change its status after a repair or when it's used again, keep its live values and accident reports, or hand it over to another customer. The team keeps its passport values.</p>
+                    }
                     @await Html.PartialAsync("~/Views/Batteries/_LiveTiles.cshtml", Model.Tiles)
                 </section>
             </div>
@@ -128,12 +149,15 @@
             <div data-section="live" hidden="@Hidden("live")">
                 @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
             </div>
-            <div data-section="accidents" hidden="@Hidden("accidents")">
-                @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
-            </div>
-            <div data-section="handover" hidden="@Hidden("handover")">
-                @await Html.PartialAsync("~/Views/ClusterAdmin/_HandOverCard.cshtml", Model.HandOver)
-            </div>
+            @if (!Model.ReadOnly)
+            {
+                <div data-section="accidents" hidden="@Hidden("accidents")">
+                    @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
+                </div>
+                <div data-section="handover" hidden="@Hidden("handover")">
+                    @await Html.PartialAsync("~/Views/ClusterAdmin/_HandOverCard.cshtml", Model.HandOver)
+                </div>
+            }
             <div data-section="modules" hidden="@Hidden("modules")">
                 @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
             </div>
@@ -148,11 +172,11 @@
     </div>
 
     @* The windows, at page level: one inside a hidden section can't open. *@
-    @if (Model.Status is { } statusWindows)
+    @if (!Model.ReadOnly && Model.Status is { } statusWindows)
     {
         @await Html.PartialAsync("~/Views/Batteries/_StatusDialog.cshtml", statusWindows)
     }
-    @if (Model.HandOver.Waiting is null)
+    @if (!Model.ReadOnly && Model.HandOver.Waiting is null)
     {
         <div class="bp-console-form-card bp-dialog-host">
             @await Html.PartialAsync("~/Views/ClusterAdmin/_HandOverDialog.cshtml", Model.HandOver)
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~ClusterBatteriesControllerTests | FullyQualifiedName~PassportMarkPagesTests | FullyQualifiedName~ReadOnlyClusterPageTests"`
Expected: `Passed!  - Failed:     0, Passed:    28`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs BatteryPassWeb.Tests/Dpp/ReadOnlyClusterPageTests.cs web/Controllers/PassportController.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Dpp/Pages/RegistryList.cs web/Dpp/Store/PassportVersion.cs web/Views/Batteries/_LiveValuesCard.cshtml web/Views/ClusterAdmin/Battery.cshtml
git commit -m "feat(dpp): a marked battery's page for its cluster admin, read-only" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: No passport chips beside the passports list

The team's hero offers "Earlier passport" and "New passport" only when "Passports of this battery" isn't shown; the public page keeps its link.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/HeroChainChipsTests.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/HeroChainChipsTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6b (the owner's rule: no duplicates): on the team's battery page, "Passports of this battery" lists every
/// passport of a battery placed on the market again, so the hero's "Earlier passport" and "New passport" chips, which
/// led to the same passports, show only while there is no list. The public page has no list and keeps its link.
/// </summary>
public sealed class HeroChainChipsTests
{
    [Fact]
    public void TheTeamsHero_ShouldOfferThePassportChipsOnlyWithoutTheList()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var pages = File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Pages", "PassportPages.cs"));

        Assert.Contains("@if (Model.Chain.Count == 0 && Model.Status?.Predecessor is { } earlier)", page, StringComparison.Ordinal);
        Assert.Contains("@if (Model.Chain.Count == 0 && Model.Status?.Succession is { Linked: true } next && (next.SuccessorHref ?? next.OtherPassport) is { } newPassport)", page, StringComparison.Ordinal);
        Assert.Contains("\"Earlier passport\") { Calm = true }", pages, StringComparison.Ordinal); // the public page keeps it
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HeroChainChipsTests"`
Expected: `Failed:     1, Passed:     0` (failing: `HeroChainChipsTests.TheTeamsHero_ShouldOfferThePassportChipsOnlyWithoutTheList`).

- [ ] **Step 3: Write the code**

Patch `r6b-task2-code.patch` (`web/Views/Batteries/Battery.cshtml`). Save the block as `../r6b-task2-code.patch`, outside the worktree, and run `git apply ../r6b-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 059ca85..de12e1c 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -81,11 +81,11 @@
                     {
                         @await Html.PartialAsync("_TestTag", "Test ID")
                     }
-                    @if (Model.Status?.Predecessor is { } earlier)
+                    @if (Model.Chain.Count == 0 && Model.Status?.Predecessor is { } earlier)
                     {
                         <a class="bp-chip" href="@earlier.Href"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>Earlier passport</a>
                     }
-                    @if (Model.Status?.Succession is { Linked: true } next && (next.SuccessorHref ?? next.OtherPassport) is { } newPassport)
+                    @if (Model.Chain.Count == 0 && Model.Status?.Succession is { Linked: true } next && (next.SuccessorHref ?? next.OtherPassport) is { } newPassport)
                     {
                         <a class="bp-chip" href="@newPassport"><svg class="bp-i" aria-hidden="true"><use href="#i-arrow" /></svg>New passport</a>
                     }
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HeroChainChipsTests"`
Expected: `Passed!  - Failed:     0, Passed:     1`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/HeroChainChipsTests.cs web/Views/Batteries/Battery.cshtml
git commit -m "fix(dpp): the team's hero offers the passport chips only without the passports list" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2438, Skipped:     0, Total:  2438` (9 more than the 2429 before).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat a78cd9b -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

1. **Restart the app with the usual launcher**, then **Clusters → Demo data → Reset demo data**.
2. **As demo.north.admin@example.test:** Registry → DEMO-C7M-0003: its row has the pencil (Battery page). Open it:
   "Invalid since …" under the header; the Passport badge "Invalid · version 1"; the side menu Overview, Status &
   repairs, Live values, Modules, Versions, History; no Change status, Add a note or Hand over. Live values lists the
   last values with their dates. History lists what happened while the battery was the cluster's.
3. **As demo.fleet.admin@example.test:** the same for DEMO-CORE-0003, "Marked for deletion since …".
4. **As demo.south.admin@example.test:** Registry → DEMO-C7M-0003: no pencil.
5. **As the team:** DEMO-C13M-0001's battery page: no "Earlier passport" chip in the header; Versions lists the 3
   passports. The public page of DEMO-C13M-0001 still has "Earlier passport".

## Next

Step 5 resumes.
