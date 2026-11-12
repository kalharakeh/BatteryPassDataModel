# DPP V3 Step 6d-2b: Missing ID Settings Refused, and the Battery IDs Page in Tabs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A part of its own between 6d-2 and 6d-3, from the owner's trial of 6d-2 (9 Nov 2026; the screens approved the same day: "okay approved"). Two problems, and two small faults found by the 6d-3 role check:

1. **Missing ID settings no longer fail silently.** Without `DPP_ID_AGENCY`, `DPP_ID_COMPANY` or `DPP_ID_BASE_URL` (or with one not valid), the app issued test IDs for real releases. Now:
   - the Battery IDs page shows a red notice: "The battery ID settings aren't set, so batteries would get test IDs instead of their real battery IDs.", naming each one not set or not valid;
   - "Release new IDs" and the release form are disabled, and a release is refused on the server with the same reason;
   - creating a battery from a released real ID (on the page or in the API) is refused with the same reason, and the ID stays available;
   - the demo data still works without the settings: its release, its batteries and its spares.
2. **The Battery IDs page is easier to find your way in** (the owner's layout):
   - **At the top:** a summary of the real IDs (Available · Used · Published · Voided; demo IDs aren't counted) and a primary "Release new IDs" button.
   - **Three tabs:**
     - **Releases**, which opens first: a row per release with its reference, model, date and counts; per row "Open", "Export to Excel" (that release only) and "Void unused…". The demo release is tagged "Demo".
     - **All IDs:** search and filters (status, release, model), and the filtered "Export to Excel". "Void…" is in a small ⋯ menu per row, not a red link. Demo IDs are hidden unless "Show demo IDs" is ticked.
     - **Release new IDs:** the form on its own, with the refusal list when a release is refused.
   - **A page per release**, `/admin/battery-ids/releases/{id}`: its reference, model, date and who released it, "Export to Excel" at the top, "Void unused…", and its IDs with their status.
   - **After a successful release**, the app goes to that release's page: "Released N battery IDs. Export them to Excel for the labels.", and a line on what comes next.
3. **The two faults from the role check:**
   - on a phone, a battery's full web-address ID ran off the right edge of its details; it now wraps inside the screen;
   - the passport pages and the API didn't find a battery by its web-address ID when it came escaped in the address (`%2F` for `/`); they now decode it once.

**Architecture:**
- **The settings check, in `web/Dpp/Identifiers/DunsIdentifierScheme.cs` (`IdentifierForms`):**
  - `MissingSettings(BatteryPassOptions)` lists `DPP_ID_AGENCY`, `DPP_ID_COMPANY` and `DPP_ID_BASE_URL` when not set or not valid (by the same rules as `IsConfigured`);
  - `SettingsMissing(missing)` is the one reason, used by the release, the create and the page.
- **`BatteryIdPool`:**
  - takes the options (an optional constructor parameter; DI passes them) and gives `MissingSettings`;
  - `ReleaseAsync` refuses a real release while any is missing; a demo release still goes in;
  - `ReleaseSummary.IsDemo`; `ReleasedIdFilter.Demo`: `ListAsync` leaves demo IDs out unless asked for, or unless one release is asked for;
  - new `ReleaseAsync(releaseId)` (one release's summary) and `RealCountsAsync()` (the summary's counts, real IDs only).
- **`PackCreationService`:** creating from a released real ID is refused with the same reason while a setting is missing; a demo ID still works. The API's create goes through it.
- **The pages:**
  - `BatteryIdsController`:
    - `BatteryIdsTabs` (`releases`, `ids`, `new`); `BatteryIdsPage` carries the tab, the counts, the missing settings, the releases, the form, the refusal and the tab's IDs;
    - `GET /admin/battery-ids?tab=…` (All IDs' filters and `demo`), `GET releases/{id}` (`ReleasePage`; 404 for an unknown one);
    - a successful release redirects to its page with `?released=true`; a refused one shows the Release new IDs tab with a 422;
    - the voids come back to the page they were made from (`returnTo`, only under `/admin/battery-ids`);
    - the export takes one release, or All IDs' filters; its file name carries the release's reference.
  - Views:
    - `BatteryIds/Index.cshtml`, rewritten: the red notice, the summary, the tabs;
    - new `Release.cshtml` (a release's page), `_IdRows.cshtml` (the ID rows with their ⋯ menu) and `_VoidDialogs.cshtml` (the two void windows, shared by both pages);
    - `css/bp-battery-ids.css`: the summary, tabs, row menu, release head, notice, and their phone and dark-theme rules; `js/bp-battery-ids.js` closes the row menu when its "Void…" opens the window.
- **The two faults:**
  - `css/bp-status.css`: `.bp-field-grid dd` may shrink and wraps anywhere;
  - `PassportPages.FindAsync` and `BatteryApi.FindAsync` decode the ID once (`Uri.UnescapeDataString`).
- **`site.css` is not changed.**
- **Tests:**
  - new `BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs`, covering:
    - a real release refused while the settings are missing, naming them, with the demo data seeded anyway;
    - a battery from a released real ID refused (the ID stays available), and one from a demo spare made;
    - the page opening on Releases with the notice, and its counts of real IDs only;
    - All IDs hiding demo IDs unless asked for;
    - a release landing on its page, with its IDs and its export's file name; an unknown release a 404;
    - a refused release staying on its tab with its reasons and the typed list;
    - the views: the notice, the disabled form, "Show demo IDs", the row menu without red links, the release page's export and void;
  - changed: `BatteryIdPoolTests` (the demo spares are asked for with `Demo: true`) and `DunsIdentifierTests` (the escaped web-address lookup on the pages and in the API, and the wrap on a phone).

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** the managing session's message of 9 Nov 2026 (the owner's two problems and the layout), and the approved screens in the notes folder's `step6d2b-shots/`.

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at abd0ff7 (plan 6d-2's commit, "feat(dpp): battery IDs released in advance…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas. The tests use throwaway MongoDBs (EphemeralMongo, already downloaded).
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own tests. The whole suite runs once**, in the final checks. Never run two whole suites at once.
- **Baseline:** the whole suite shows `Passed:  2602` (plan 6d-2's result). After this plan: 2611.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6d2b-test.patch`). Then run `git apply ../<name>` from the worktree root. New files are given inside the patches.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**

## Found while prototyping

- **The screens** were checked by eye at 1280 and 375 before the owner saw them, on an app with the settings and on one without. A script measured that no page scrolls sideways at either width.
  - The first red notice used the quiet status line and read too pale. It is now a red box like the refusal list's.
  - The disabled form's "Check and release" button looked live. It is now dimmed.
- **Why the release page:** the owner's trial showed the export of a new release was lost in a long list. Landing on the release puts its export first.
- **Demo IDs** stay out of the summary and All IDs by default, so the counts are the real stock. A release's own page and its export always show all of its IDs, demo or not.
- **The settings are checked when used**, not at start-up: the app keeps running for the demo data and every other page, and the page says what's missing.
- **The two faults** were found by the 6d-3 role check at 375 and in the API. They are fixed here so 6d-3 is the Help and the test pack only.

**Measured on a clean clone (abd0ff7):**
- the tests first fail to build: `ReleasedIdFilter` has no `Demo`, and `BatteryIdsTabs`, `ReleasePage` and `BatteryIdsPage.Tab` don't exist yet;
- with the code, Task 1's own classes pass: 65 tests across the 3 classes this plan adds or changes;
- the whole suite: `Failed: 2, Passed: 2609, Total: 2611` (the clean clone's two known `ApplicationAuditCoverageTests` failures, `site.css` with CRLF line endings, pass in the worktree).

### Task 1: Missing ID settings refused, and the Battery IDs page in tabs

**Files:**
- Create (tests): `BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs`
- Create (app): `web/Views/BatteryIds/Release.cshtml`, `web/Views/BatteryIds/_IdRows.cshtml`, `web/Views/BatteryIds/_VoidDialogs.cshtml`
- Modify (tests): `BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs`, `BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs`
- Modify (app): `web/Controllers/BatteryIdsController.cs`, `web/Dpp/Api/BatteryApi.cs`, `web/Dpp/Editing/PackCreationService.cs`, `web/Dpp/Identifiers/BatteryIdPool.cs`, `web/Dpp/Identifiers/DunsIdentifierScheme.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Views/BatteryIds/Index.cshtml`, `web/wwwroot/css/bp-battery-ids.css`, `web/wwwroot/css/bp-status.css`, `web/wwwroot/js/bp-battery-ids.js`

- [ ] **Step 1: Write the failing tests**

Save as `../6d2b-test.patch` and run `git apply ../6d2b-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs b/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs
index 6b38519..ced59d4 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs
@@ -271,7 +271,7 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
         await pool.ReleaseAsync(Who, List("PO real", "REAL-0001"));
 
         var demo = (await pool.ReleasesAsync()).Single(release => release.Reference == DemoDataService.DemoReference);
-        var spares = await pool.ListAsync(new ReleasedIdFilter(Query: "DEMOSPARE"));
+        var spares = await pool.ListAsync(new ReleasedIdFilter(Query: "DEMOSPARE", Demo: true));
         await rig.Demo.ClearAsync(Who);
 
         Assert.Equal(DemoDataService.SpareSerials.Length, demo.Count(ReleasedIdStatuses.Available));
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs b/BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs
new file mode 100644
index 0000000..1d9ca80
--- /dev/null
+++ b/BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs
@@ -0,0 +1,190 @@
+using System.Security.Claims;
+using System.Text.Json.Nodes;
+using BatteryPassWeb.Controllers;
+using BatteryPassWeb.Dpp.Demo;
+using BatteryPassWeb.Dpp.Editing;
+using BatteryPassWeb.Dpp.Identifiers;
+using BatteryPassWeb.Dpp.Store;
+using BatteryPassWeb.Services;
+using BatteryPassWeb.Tests.Demo;
+using Microsoft.AspNetCore.Http;
+using Microsoft.AspNetCore.Mvc;
+using Microsoft.AspNetCore.Mvc.ViewFeatures;
+using Microsoft.Extensions.DependencyInjection;
+
+namespace BatteryPassWeb.Tests.Dpp;
+
+/// <summary>
+/// Step 6d-2b, after the owner's trial of 6d-2 (9 Nov 2026): missing ID settings no longer fail silently (no real
+/// release, no battery from a real released ID, a red notice on the page; demo data still works), and the "Battery IDs"
+/// page is three tabs (Releases first, All IDs, Release new IDs) with a page per release that a new release lands on.
+/// </summary>
+[Collection(MongoCollection.Name)]
+public sealed class BatteryIdSettingsAndPagesTests(MongoFixture mongo)
+{
+    private const string Who = DemoRig.Who;
+    private static readonly string[] AllThree = ["DPP_ID_AGENCY", "DPP_ID_COMPANY", "DPP_ID_BASE_URL"];
+
+    private static async Task<(DemoRig Rig, BatteryIdPool Pool, BatteryPack C7)> SeededAsync(MongoFixture mongo, bool dunsIds)
+    {
+        var rig = await DemoRig.StartAsync(mongo, dunsIds: dunsIds);
+        await rig.Demo.SeedAsync(Who);
+        var c7 = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single();
+        return (rig, rig.Services.GetRequiredService<BatteryIdPool>(), c7);
+    }
+
+    private static NewPack NewBattery(BatteryPack like, string serial) =>
+        new(like.BatchId, like.OperatorId, like.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, FromPool: true);
+
+    private static BatteryIdsController Controller(DemoRig rig)
+    {
+        var http = new DefaultHttpContext
+        {
+            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, Who), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)], "test")),
+        };
+        return new BatteryIdsController(rig.Services.GetRequiredService<BatteryIdPool>(), rig.Store)
+        {
+            ControllerContext = new ControllerContext { HttpContext = http },
+            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
+        };
+    }
+
+    [Fact]
+    public async Task MissingSettings_ShouldRefuseARealRelease_NamingThem_WhileTheDemoDataStillWorks()
+    {
+        var (rig, pool, _) = await SeededAsync(mongo, dunsIds: false);
+        using var __ = rig;
+
+        var refused = await pool.ReleaseAsync(Who, new ReleaseRequest("PO 1", null, [], "REAL-0001"));
+
+        Assert.Equal(AllThree, pool.MissingSettings);
+        Assert.False(refused.Done);
+        Assert.Equal(IdentifierForms.SettingsMissing(AllThree), refused.Problem);
+        Assert.Contains("aren't set, so batteries would get test IDs", refused.Problem, StringComparison.Ordinal);
+        Assert.Single(await pool.ReleasesAsync(), release => release.IsDemo && release.Reference == DemoDataService.DemoReference); // the demo seeded anyway
+        Assert.Empty(await pool.ListAsync(new ReleasedIdFilter(Query: "REAL")));
+    }
+
+    [Fact]
+    public async Task MissingSettings_ShouldRefuseABatteryFromARealReleasedId_ButNotFromADemoSpare()
+    {
+        var (rig, pool, c7) = await SeededAsync(mongo, dunsIds: false);
+        using var __ = rig;
+        // An ID released while the settings were set, before they went missing.
+        await rig.Store.ReleasedIds.InsertOneAsync(new ReleasedBatteryId
+        {
+            Id = "OLD0001",
+            SerialNumber = "OLD-0001",
+            Identifier = "https://www.sib-scania-eubp.eu/25S/UN778474481OLD0001",
+            ReleaseId = "old-release",
+            Reference = "PO old",
+            Status = ReleasedIdStatuses.Available,
+            ReleasedBy = Who,
+            ReleasedAtUtc = DateTime.UtcNow,
+        });
+        var creation = rig.Services.GetRequiredService<PackCreationService>();
+
+        var real = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "OLD-0001"));
+        var demo = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, DemoDataService.SpareSerials[0]));
+
+        Assert.Equal(IdentifierForms.SettingsMissing(AllThree), real.Problem);
+        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("OLD-0001"))!.Status); // nothing was taken
+        Assert.Null(demo.Problem);
+        Assert.NotNull(demo.Pack);
+    }
+
+    [Fact]
+    public async Task ThePage_ShouldOpenOnReleases_AndShowTheNotice_WhenTheSettingsAreMissing()
+    {
+        var (rig, _, _) = await SeededAsync(mongo, dunsIds: false);
+        using var __ = rig;
+
+        var page = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await Controller(rig).Index(null, null, null, null, null, false, CancellationToken.None)).Model);
+
+        Assert.Equal(BatteryIdsTabs.Releases, page.Tab);
+        Assert.Equal(AllThree, page.MissingSettings);
+        Assert.Contains(page.Releases, release => release.IsDemo);
+        Assert.All(page.Counts.Values, count => Assert.Equal(0, count)); // the summary counts real IDs only
+    }
+
+    [Fact]
+    public async Task AllIds_ShouldHideDemoIds_UnlessAskedFor_AndTheSummaryCountRealIdsOnly()
+    {
+        var (rig, pool, _) = await SeededAsync(mongo, dunsIds: true);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, new ReleaseRequest("PO 2", null, [], "REAL-0001\nREAL-0002"));
+        var controller = Controller(rig);
+
+        var real = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(BatteryIdsTabs.Ids, null, null, null, null, false, CancellationToken.None)).Model);
+        var withDemo = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(BatteryIdsTabs.Ids, null, null, null, null, true, CancellationToken.None)).Model);
+
+        Assert.Equal(["REAL-0001", "REAL-0002"], real.Items.Select(item => item.SerialNumber).Order());
+        Assert.Contains(withDemo.Items, item => item.IsDemo);
+        Assert.Empty(real.MissingSettings);
+        Assert.Equal(2, real.Counts.GetValueOrDefault(ReleasedIdStatuses.Available)); // the demo spares aren't counted
+        Assert.Equal(0, real.Counts.GetValueOrDefault(ReleasedIdStatuses.Published));
+    }
+
+    [Fact]
+    public async Task ARelease_ShouldLandOnItsOwnPage_WithItsIdsAndItsExport()
+    {
+        var (rig, pool, _) = await SeededAsync(mongo, dunsIds: true);
+        using var __ = rig;
+        var controller = Controller(rig);
+
+        var done = Assert.IsType<RedirectResult>(await controller.ReleaseIds("PO 7, line 1", null, ["LBL-0001"], ["LBL-0003"], ["3"], null, CancellationToken.None));
+        var release = (await pool.ReleasesAsync()).Single(summary => summary.Reference == "PO 7, line 1");
+        var page = Assert.IsType<ReleasePage>(Assert.IsType<ViewResult>(await controller.Release(release.ReleaseId, true, CancellationToken.None)).Model);
+        var export = Assert.IsType<FileContentResult>(await controller.Export(null, release.ReleaseId, null, null, false, CancellationToken.None));
+
+        Assert.Equal($"/admin/battery-ids/releases/{Uri.EscapeDataString(release.ReleaseId)}?released=true", done.Url);
+        Assert.Equal("Released 3 battery IDs. Export them to Excel for the labels.", controller.TempData[ProductDataController.StatusKey]);
+        Assert.True(page.JustReleased);
+        Assert.Equal(["LBL-0001", "LBL-0002", "LBL-0003"], page.Items.Select(item => item.SerialNumber).Order());
+        Assert.StartsWith("battery-ids-PO-7--line-1-", export.FileDownloadName, StringComparison.Ordinal);
+        Assert.IsType<NotFoundResult>(await controller.Release("no-such-release", false, CancellationToken.None));
+    }
+
+    [Fact]
+    public async Task ARefusedRelease_ShouldStayOnTheReleaseTab_WithItsReasons()
+    {
+        var (rig, _, _) = await SeededAsync(mongo, dunsIds: true);
+        using var __ = rig;
+        var controller = Controller(rig);
+
+        var view = Assert.IsType<ViewResult>(await controller.ReleaseIds("PO 8", null, [], [], [], "TEST-0001\n---", CancellationToken.None));
+
+        Assert.Equal(nameof(BatteryIdsController.Index), view.ViewName);
+        Assert.Equal(StatusCodes.Status422UnprocessableEntity, controller.Response.StatusCode);
+        var page = Assert.IsType<BatteryIdsPage>(view.Model);
+        Assert.Equal(BatteryIdsTabs.New, page.Tab);
+        Assert.Equal(2, page.Refusal!.Problems.Count);
+        Assert.Equal("TEST-0001\n---", page.Form.List);
+    }
+
+    [Fact]
+    public void TheViews_ShouldBeTheOwnersLayout()
+    {
+        var index = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Index.cshtml"));
+        var rows = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "_IdRows.cshtml"));
+        var release = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Release.cshtml"));
+
+        Assert.Contains("data-id-settings-missing", index, StringComparison.Ordinal);
+        Assert.Contains("<fieldset disabled=\"@blocked\" class=\"bp-release-fields\">", index, StringComparison.Ordinal); // no release while blocked
+        Assert.Contains("Show demo IDs", index, StringComparison.Ordinal);
+        Assert.Contains("Release new IDs", index, StringComparison.Ordinal);
+        Assert.Contains("<details class=\"bp-row-menu\">", rows, StringComparison.Ordinal); // Void… in a small menu,
+        Assert.DoesNotContain("is-danger", rows, StringComparison.Ordinal); // not a red link on every row
+        Assert.Contains("Export to Excel", release, StringComparison.Ordinal);
+        Assert.Contains("data-void-unused", release, StringComparison.Ordinal);
+    }
+
+    private sealed class NoTempDataStorage : ITempDataProvider
+    {
+        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
+
+        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
+        {
+        }
+    }
+}
diff --git a/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs b/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
index da6a52e..7490b5a 100644
--- a/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
@@ -181,6 +181,30 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
         Assert.Contains("\"25S\"", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "HomeController.cs")), StringComparison.Ordinal); // a scanned address is searched by its item
     }
 
+    [Fact]
+    public async Task AWebAddressIdInAnAddress_ShouldFindItsBattery_OnThePagesAndInTheApi()
+    {
+        // Found by the role check (6d-3): an API client puts the battery ID in the address escaped, and the route keeps its
+        // slashes encoded (%2F), so the lookups decode it once.
+        using var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
+        await rig.Demo.SeedAsync(DemoRig.Who);
+        var encoded = Uri.EscapeDataString($"{Base}/25S/UN778474481TESTDEMOC7M0001").Replace("%3A", ":", StringComparison.Ordinal);
+        var encodedPassport = Uri.EscapeDataString((await rig.Services.GetRequiredService<PassportPages>().FindAsync("DEMO-C7M-0001"))!.DigitalProductPassportId);
+
+        var page = await rig.Services.GetRequiredService<PassportPages>().FindAsync(encoded);
+        var byPassport = await rig.Services.GetRequiredService<PassportPages>().FindAsync(encodedPassport);
+        var write = await rig.Services.GetRequiredService<BatteryPassWeb.Dpp.Api.BatteryApi>().FindAsync(ChangeSender.Team(DemoRig.Who), encoded);
+
+        Assert.Contains("%2F", encoded, StringComparison.Ordinal);
+        Assert.NotNull(page);
+        Assert.Equal(page.Id, byPassport!.Id);
+        Assert.Equal(page.PackId, write.Pack!.Id);
+    }
+
+    [Fact]
+    public void ALongIdValue_ShouldWrapOnAPhone_RatherThanScrollThePage() =>
+        Assert.Contains(".bp-field-grid dd {\n  min-width: 0;\n  overflow-wrap: anywhere;\n}", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-status.css")).Replace("\r\n", "\n"), StringComparison.Ordinal);
+
     [Fact]
     public async Task TheDemoData_ShouldGetTestIds_AndSuccessorsRNumbers()
     {
```

- [ ] **Step 2: See them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`
Expected: the build fails: `ReleasedIdFilter` has no `Demo`, and `BatteryIdsTabs`, `ReleasePage` and `BatteryIdsPage.Tab` don't exist yet.

- [ ] **Step 3: Write the code**

Save as `../6d2b-app.patch` and run `git apply ../6d2b-app.patch`:

```diff
diff --git a/web/Controllers/BatteryIdsController.cs b/web/Controllers/BatteryIdsController.cs
index fd2f155..2cb8534 100644
--- a/web/Controllers/BatteryIdsController.cs
+++ b/web/Controllers/BatteryIdsController.cs
@@ -12,26 +12,47 @@ public sealed record ReleaseForm(string? Reference, string? ModelId, IReadOnlyLi
     public static readonly ReleaseForm Empty = new(null, null, [new IdRange(null, null, null)], null);
 }
 
-/// <summary>The "Battery IDs" page: the release form, its refusals, the releases and the released IDs.</summary>
+/// <summary>The "Battery IDs" page's tabs (6d-2b): the releases (first), all IDs, and the release form.</summary>
+public static class BatteryIdsTabs
+{
+    public const string Releases = "releases";
+    public const string Ids = "ids";
+    public const string New = "new";
+
+    public static string Of(string? tab) => tab is Ids or New ? tab : Releases;
+}
+
+/// <summary>
+/// What every tab of the "Battery IDs" page shows: the summary of the real IDs and the settings' state, then the chosen
+/// tab's content.
+/// </summary>
 public sealed record BatteryIdsPage(
+    string Tab,
+    IReadOnlyDictionary<string, int> Counts,
+    IReadOnlyList<string> MissingSettings,
+    IReadOnlyList<ReleaseSummary> Releases,
+    IReadOnlyDictionary<string, string> Models,
     ReleaseForm Form,
     ReleaseResult? Refusal,
-    IReadOnlyList<ReleaseSummary> Releases,
     IReadOnlyList<ReleasedBatteryId> Items,
-    ReleasedIdFilter Filter,
-    IReadOnlyDictionary<string, string> Models,
-    IReadOnlyDictionary<string, string> Batteries,
-    int Total)
+    int Total,
+    ReleasedIdFilter Filter)
 {
-    /// <summary>The most rows the page lists; the export has them all.</summary>
+    /// <summary>The most rows a list shows; the export has them all.</summary>
     public const int MaxRows = 200;
 
     public string ModelName(string? modelId) => modelId is null ? "Any model" : Models.GetValueOrDefault(modelId, modelId);
 }
 
+/// <summary>One release's page (6d-2b): what it is, its export, and its IDs.</summary>
+public sealed record ReleasePage(ReleaseSummary Release, IReadOnlyList<ReleasedBatteryId> Items, IReadOnlyDictionary<string, string> Models, bool JustReleased)
+{
+    public string ModelName(string? modelId) => modelId is null ? "Any model" : Models.GetValueOrDefault(modelId, modelId);
+}
+
 /// <summary>
-/// The team's "Battery IDs" page (step 6d, the owner, 6 Nov 2026): serial numbers released in advance, each with its
-/// battery ID, so labels can be printed before the batteries are made. Team only.
+/// The team's "Battery IDs" page (step 6d; reorganised in 6d-2b after the owner's trial): serial numbers released in
+/// advance, each with its battery ID, so labels can be printed before the batteries are made. Team only.
 /// </summary>
 [Authorize(Policy = "AdminOnly")]
 [Route("admin/battery-ids")]
@@ -40,12 +61,31 @@ public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : C
     public const string ReleaseDialog = "void-unused-dialog";
 
     [HttpGet("")]
-    public async Task<IActionResult> Index([FromQuery] string? status, [FromQuery] string? release, [FromQuery] string? model, [FromQuery] string? q, CancellationToken cancellationToken) =>
-        View(await PageAsync(ReleaseForm.Empty, null, new ReleasedIdFilter(status, release, model, q), cancellationToken));
+    public async Task<IActionResult> Index(
+        [FromQuery] string? tab,
+        [FromQuery] string? status,
+        [FromQuery] string? release,
+        [FromQuery] string? model,
+        [FromQuery] string? q,
+        [FromQuery] bool demo,
+        CancellationToken cancellationToken) =>
+        View(await PageAsync(BatteryIdsTabs.Of(tab), ReleaseForm.Empty, null, new ReleasedIdFilter(status, release, model, q, demo), cancellationToken));
+
+    [HttpGet("releases/{releaseId}")]
+    public async Task<IActionResult> Release(string releaseId, [FromQuery] bool released, CancellationToken cancellationToken)
+    {
+        if (await pool.ReleaseAsync(releaseId, cancellationToken) is not { } summary)
+        {
+            return NotFound();
+        }
+
+        var items = await pool.ListAsync(new ReleasedIdFilter(Reference: releaseId), cancellationToken);
+        return View(new ReleasePage(summary, items, await ModelsAsync(cancellationToken), released));
+    }
 
     [HttpPost("")]
     [ValidateAntiForgeryToken]
-    public async Task<IActionResult> Release(
+    public async Task<IActionResult> ReleaseIds(
         [FromForm] string? reference,
         [FromForm] string? modelId,
         [FromForm] string[]? start,
@@ -61,18 +101,19 @@ public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : C
         var result = await pool.ReleaseAsync(Who, new ReleaseRequest(reference, modelId, ranges, list), cancellationToken);
         if (result.Done)
         {
-            TempData[ProductDataController.StatusKey] = $"Released {result.Released.ToString(CultureInfo.InvariantCulture)} battery {(result.Released == 1 ? "ID" : "IDs")} ({reference?.Trim()}). They are listed below; export them for the labels.";
-            return Redirect($"/admin/battery-ids?release={Uri.EscapeDataString(result.ReleaseId!)}");
+            // Straight to the new release, its export at the top, for the labels (the owner, 6d-2b).
+            TempData[ProductDataController.StatusKey] = $"Released {result.Released.ToString(CultureInfo.InvariantCulture)} battery {(result.Released == 1 ? "ID" : "IDs")}. Export them to Excel for the labels.";
+            return Redirect($"/admin/battery-ids/releases/{Uri.EscapeDataString(result.ReleaseId!)}?released=true");
         }
 
         if (result.Problems.Count == 0)
         {
             ViewData[ProductDataController.ErrorKey] = $"Nothing was released: {result.Problem}."; // a listed refusal says it in its own box
         }
+
         var form = new ReleaseForm(reference, modelId, ranges.Length == 0 ? ReleaseForm.Empty.Ranges : ranges, list);
-        var page = await PageAsync(form, result, new ReleasedIdFilter(), cancellationToken);
         Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
-        return View(nameof(Index), page);
+        return View(nameof(Index), await PageAsync(BatteryIdsTabs.New, form, result, new ReleasedIdFilter(), cancellationToken));
     }
 
     [HttpPost("void")]
@@ -93,30 +134,44 @@ public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : C
         return Redirect(Back(returnTo));
     }
 
+    /// <summary>The IDs as an Excel file: one release's (<paramref name="release"/>), or All IDs as filtered.</summary>
     [HttpGet("export")]
-    public async Task<IActionResult> Export([FromQuery] string? status, [FromQuery] string? release, [FromQuery] string? model, [FromQuery] string? q, CancellationToken cancellationToken)
+    public async Task<IActionResult> Export(
+        [FromQuery] string? status,
+        [FromQuery] string? release,
+        [FromQuery] string? model,
+        [FromQuery] string? q,
+        [FromQuery] bool demo,
+        CancellationToken cancellationToken)
     {
-        var items = await pool.ListAsync(new ReleasedIdFilter(status, release, model, q), cancellationToken);
-        var models = await ModelsAsync(cancellationToken);
-        return File(BatteryIdPool.Export(items, models), XlsxWriter.ContentType, $"battery-ids-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
+        var items = await pool.ListAsync(new ReleasedIdFilter(status, release, model, q, demo), cancellationToken);
+        var name = release is { Length: > 0 } && await pool.ReleaseAsync(release, cancellationToken) is { } summary ? FileWord(summary.Reference) : "all";
+        return File(BatteryIdPool.Export(items, await ModelsAsync(cancellationToken)), XlsxWriter.ContentType, $"battery-ids-{name}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
     }
 
-    private async Task<BatteryIdsPage> PageAsync(ReleaseForm form, ReleaseResult? refusal, ReleasedIdFilter filter, CancellationToken cancellationToken)
+    private async Task<BatteryIdsPage> PageAsync(string tab, ReleaseForm form, ReleaseResult? refusal, ReleasedIdFilter filter, CancellationToken cancellationToken)
     {
-        var all = await pool.ListAsync(filter, cancellationToken);
-        var items = all.Take(BatteryIdsPage.MaxRows).ToArray();
-        var batteries = new Dictionary<string, string>(StringComparer.Ordinal);
-        foreach (var packId in items.Select(item => item.PackId).OfType<string>().Distinct())
-        {
-            batteries[packId] = Dpp.Pages.BatteryPages.BatteryHref(packId);
-        }
-
-        return new BatteryIdsPage(form, refusal, await pool.ReleasesAsync(cancellationToken), items, filter, await ModelsAsync(cancellationToken), batteries, all.Count);
+        var all = tab == BatteryIdsTabs.Ids ? await pool.ListAsync(filter, cancellationToken) : [];
+        return new BatteryIdsPage(
+            tab,
+            await pool.RealCountsAsync(cancellationToken),
+            pool.MissingSettings,
+            await pool.ReleasesAsync(cancellationToken),
+            await ModelsAsync(cancellationToken),
+            form,
+            refusal,
+            all.Take(BatteryIdsPage.MaxRows).ToArray(),
+            all.Count,
+            filter);
     }
 
     private async Task<IReadOnlyDictionary<string, string>> ModelsAsync(CancellationToken cancellationToken) =>
         (await store.ListAsync<BatteryModel>(cancellationToken)).OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(model => model.Id, model => model.Name, StringComparer.Ordinal);
 
+    /// <summary>A reference as part of a file name: letters, digits and dashes.</summary>
+    private static string FileWord(string reference) =>
+        new string(reference.Select(letter => char.IsAsciiLetterOrDigit(letter) ? letter : '-').ToArray()).Trim('-') is { Length: > 0 } word ? word : "release";
+
     private void Message(string? status, string? error)
     {
         if (status is not null)
diff --git a/web/Dpp/Api/BatteryApi.cs b/web/Dpp/Api/BatteryApi.cs
index 44094d6..d20c02a 100644
--- a/web/Dpp/Api/BatteryApi.cs
+++ b/web/Dpp/Api/BatteryApi.cs
@@ -87,7 +87,7 @@ public sealed class BatteryApi(
     public async Task<WriteTarget> FindAsync(ChangeSender sender, string? id, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(sender);
-        var text = id?.Trim() ?? string.Empty;
+        var text = Uri.UnescapeDataString(id?.Trim() ?? string.Empty); // a web-form ID's slashes arrive encoded (%2F) in an address (6d)
         if (text.Length == 0)
         {
             return new WriteTarget(null, WriteRefusal.NotFound);
diff --git a/web/Dpp/Editing/PackCreationService.cs b/web/Dpp/Editing/PackCreationService.cs
index fd7bda5..a0b0cb1 100644
--- a/web/Dpp/Editing/PackCreationService.cs
+++ b/web/Dpp/Editing/PackCreationService.cs
@@ -112,6 +112,12 @@ public sealed class PackCreationService(
         // makes its own from its serial number (R1, R2, …), as does a battery made without the pool (tests, scripts).
         if (request.FromPool && request.PredecessorPackId is null && pool is not null)
         {
+            // Never a real released ID while the ID settings are missing: the battery would keep a test ID (6d-2b).
+            if (pool.MissingSettings is { Count: > 0 } missing && await pool.GetAsync(evaluation.Accepted["batterySerialNumber"].GetValue<string>(), cancellationToken) is { IsDemo: false })
+            {
+                return Failed(IdentifierForms.SettingsMissing(missing));
+            }
+
             var model = (await store.GetAsync<ProductionBatch>(request.BatchId, cancellationToken))!.ModelId;
             if (await pool.PickAsync(evaluation.Accepted["batterySerialNumber"].GetValue<string>(), pack.Id, model, cancellationToken) is not { } picked)
             {
diff --git a/web/Dpp/Identifiers/BatteryIdPool.cs b/web/Dpp/Identifiers/BatteryIdPool.cs
index acafc90..12ee730 100644
--- a/web/Dpp/Identifiers/BatteryIdPool.cs
+++ b/web/Dpp/Identifiers/BatteryIdPool.cs
@@ -26,23 +26,37 @@ public sealed record ReleaseResult(int Released, string? ReleaseId, IReadOnlyLis
 }
 
 /// <summary>One release, as the list shows it.</summary>
-public sealed record ReleaseSummary(string ReleaseId, string Reference, string? ModelId, string ReleasedBy, DateTime ReleasedAtUtc, IReadOnlyDictionary<string, int> Counts)
+public sealed record ReleaseSummary(string ReleaseId, string Reference, string? ModelId, string ReleasedBy, DateTime ReleasedAtUtc, IReadOnlyDictionary<string, int> Counts, bool IsDemo = false)
 {
     public int Total => Counts.Values.Sum();
 
     public int Count(string status) => Counts.GetValueOrDefault(status);
 }
 
-/// <summary>Which released IDs the list shows.</summary>
-public sealed record ReleasedIdFilter(string? Status = null, string? Reference = null, string? ModelId = null, string? Query = null);
+/// <summary>
+/// Which released IDs the list shows. Demo IDs only when <see cref="Demo"/> is asked (6d-2b), or when the list is one
+/// release's (<see cref="Reference"/>), which shows all of its own.
+/// </summary>
+public sealed record ReleasedIdFilter(string? Status = null, string? Reference = null, string? ModelId = null, string? Query = null, bool Demo = false);
 
 /// <summary>
 /// The battery IDs the team releases in advance (step 6d, the owner, 6 Nov 2026). A release is checked whole and is
 /// all-or-nothing; each serial number gets its #6 at release, fixed for life. A battery is created by picking an available
 /// one; a draft may swap it for another; a voided one is never reused.
 /// </summary>
-public sealed partial class BatteryIdPool(DppStore store, IIdentifierScheme identifiers, ApplicationAuditService audit, TimeProvider clock)
+public sealed partial class BatteryIdPool(
+    DppStore store,
+    IIdentifierScheme identifiers,
+    ApplicationAuditService audit,
+    TimeProvider clock,
+    Microsoft.Extensions.Options.IOptions<Configuration.BatteryPassOptions>? options = null)
 {
+    /// <summary>
+    /// The ID settings that are missing (6d-2b). Real IDs are neither released nor used while any is: they would be test
+    /// IDs. Demo IDs still work, as the demo data does without the settings. Empty for a pool made without options.
+    /// </summary>
+    public IReadOnlyList<string> MissingSettings => options is null ? [] : IdentifierForms.MissingSettings(options.Value);
+
     public const string ReleasedEvent = "dpp.ids-released";
     public const string VoidedEvent = "dpp.ids-voided";
     public const string SwappedEvent = "dpp.id-swapped";
@@ -71,6 +85,11 @@ public sealed partial class BatteryIdPool(DppStore store, IIdentifierScheme iden
             return Refused($"the reference is longer than {MaxReference} characters");
         }
 
+        if (!request.IsDemo && MissingSettings is { Count: > 0 } missing)
+        {
+            return Refused(IdentifierForms.SettingsMissing(missing));
+        }
+
         if (request.ModelId is { Length: > 0 } modelId && await store.GetAsync<BatteryModel>(modelId, cancellationToken) is null)
         {
             return Refused("that model doesn't exist");
@@ -182,12 +201,23 @@ public sealed partial class BatteryIdPool(DppStore store, IIdentifierScheme iden
             {
                 var first = group.First();
                 return new ReleaseSummary(group.Key, first.Reference, first.ModelId, first.ReleasedBy, first.ReleasedAtUtc,
-                    group.GroupBy(item => item.Status).ToDictionary(status => status.Key, status => status.Count(), StringComparer.Ordinal));
+                    group.GroupBy(item => item.Status).ToDictionary(status => status.Key, status => status.Count(), StringComparer.Ordinal), first.IsDemo);
             })
             .OrderByDescending(release => release.ReleasedAtUtc)
             .ToArray();
     }
 
+    /// <summary>One release, or null.</summary>
+    public async Task<ReleaseSummary?> ReleaseAsync(string releaseId, CancellationToken cancellationToken = default) =>
+        (await ReleasesAsync(cancellationToken)).FirstOrDefault(release => release.ReleaseId == releaseId);
+
+    /// <summary>How many real (not demo) IDs are in each status, for the page's summary (6d-2b).</summary>
+    public async Task<IReadOnlyDictionary<string, int>> RealCountsAsync(CancellationToken cancellationToken = default)
+    {
+        var real = await store.ReleasedIds.Find(item => !item.IsDemo).Project(item => item.Status).ToListAsync(cancellationToken);
+        return ReleasedIdStatuses.All.ToDictionary(status => status, status => real.Count(item => item == status), StringComparer.Ordinal);
+    }
+
     /// <summary>The released IDs the filter keeps, newest release first, then by serial number.</summary>
     public async Task<IReadOnlyList<ReleasedBatteryId>> ListAsync(ReleasedIdFilter filter, CancellationToken cancellationToken = default)
     {
@@ -202,6 +232,10 @@ public sealed partial class BatteryIdPool(DppStore store, IIdentifierScheme iden
         {
             where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.ReleaseId, filter.Reference);
         }
+        else if (!filter.Demo)
+        {
+            where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.IsDemo, false);
+        }
 
         if (!string.IsNullOrEmpty(filter.ModelId))
         {
diff --git a/web/Dpp/Identifiers/DunsIdentifierScheme.cs b/web/Dpp/Identifiers/DunsIdentifierScheme.cs
index 602e765..c2cce40 100644
--- a/web/Dpp/Identifiers/DunsIdentifierScheme.cs
+++ b/web/Dpp/Identifiers/DunsIdentifierScheme.cs
@@ -103,6 +103,36 @@ public static partial class IdentifierForms
 
     public const string TestMarker = "TEST";
 
+    /// <summary>
+    /// The ID settings that aren't set or aren't valid (6d-2b): DPP_ID_AGENCY (two or three capital letters),
+    /// DPP_ID_COMPANY (nine digits) and DPP_ID_BASE_URL (an https address). Empty when the scheme is complete.
+    /// </summary>
+    public static IReadOnlyList<string> MissingSettings(BatteryPassOptions options)
+    {
+        ArgumentNullException.ThrowIfNull(options);
+        var missing = new List<string>();
+        if (!AgencyCode().IsMatch(options.IdAgency ?? string.Empty))
+        {
+            missing.Add("DPP_ID_AGENCY");
+        }
+
+        if (!CompanyCode().IsMatch(options.IdCompany ?? string.Empty))
+        {
+            missing.Add("DPP_ID_COMPANY");
+        }
+
+        if (!Uri.TryCreate(options.IdBaseUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
+        {
+            missing.Add("DPP_ID_BASE_URL");
+        }
+
+        return missing;
+    }
+
+    /// <summary>Why real battery IDs can't be released or used while the settings are missing (6d-2b).</summary>
+    public static string SettingsMissing(IReadOnlyList<string> missing) =>
+        $"the battery ID settings aren't set, so batteries would get test IDs instead of their real battery IDs. Not set or not valid: {string.Join(", ", missing)}. Whoever runs the app sets them and restarts it";
+
     /// <summary>True when the settings give a full scheme; otherwise the app issues test IDs.</summary>
     public static bool IsConfigured(BatteryPassOptions options) =>
         AgencyCode().IsMatch(options.IdAgency) && CompanyCode().IsMatch(options.IdCompany) && Uri.TryCreate(options.IdBaseUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index 4a42605..7d66ab6 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -187,7 +187,8 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
     /// </summary>
     public async Task<PassportVersion?> FindAsync(string? id, CancellationToken cancellationToken = default)
     {
-        var text = id?.Trim() ?? string.Empty;
+        // A web-form ID in an address arrives with its slashes still encoded (%2F): decode it once (6d).
+        var text = Uri.UnescapeDataString(id?.Trim() ?? string.Empty);
         if (text.Length == 0)
         {
             return null;
diff --git a/web/Views/BatteryIds/Index.cshtml b/web/Views/BatteryIds/Index.cshtml
index 2c0241f..0edb76f 100644
--- a/web/Views/BatteryIds/Index.cshtml
+++ b/web/Views/BatteryIds/Index.cshtml
@@ -2,13 +2,18 @@
 @using BatteryPassWeb.Dpp.Identifiers
 @using BatteryPassWeb.Dpp.Pages
 @model BatteryIdsPage
+@* The "Battery IDs" page (6d-2b, the owner's layout): the real IDs' summary and "Release new IDs" at the top, then three
+   tabs: Releases (first), All IDs, Release new IDs. A release has its own page. *@
 @{
     ViewData["Title"] = "Battery IDs";
     var filter = Model.Filter;
     var here = Context.Request.Path + Context.Request.QueryString;
-    var exportQuery = Context.Request.QueryString.HasValue ? Context.Request.QueryString.Value : string.Empty;
-    var filtered = !string.IsNullOrEmpty(filter.Status) || !string.IsNullOrEmpty(filter.Reference) || !string.IsNullOrEmpty(filter.ModelId) || !string.IsNullOrEmpty(filter.Query);
     var problems = Model.Refusal?.Problems ?? [];
+    var filtered = !string.IsNullOrEmpty(filter.Status) || !string.IsNullOrEmpty(filter.Reference) || !string.IsNullOrEmpty(filter.ModelId) || !string.IsNullOrEmpty(filter.Query) || filter.Demo;
+    var exportQuery = string.Join("&", new[] { ("status", filter.Status), ("release", filter.Reference), ("model", filter.ModelId), ("q", filter.Query), ("demo", filter.Demo ? "true" : null) }
+        .Where(pair => !string.IsNullOrEmpty(pair.Item2)).Select(pair => $"{pair.Item1}={Uri.EscapeDataString(pair.Item2!)}"));
+    var blocked = Model.MissingSettings.Count > 0;
+    string TabClass(string tab) => Model.Tab == tab ? "bp-subtab is-active" : "bp-subtab";
 }
 
 <main class="bp-page bp-admin-page">
@@ -17,247 +22,221 @@
     @await Html.PartialAsync("_AdminTabs", "battery-ids")
     @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
 
-    <section class="bp-card bp-product-editor-section bp-console-form-card mt-4" id="release">
-        <div class="bp-product-section-heading">
+    @if (blocked)
+    {
+        <div class="bp-settings-missing mt-3" role="alert" data-id-settings-missing>
+            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 2 20h20L12 3z" /><path d="M12 10v4.5M12 17v.5" /></svg>
             <div>
-                <h2>Release battery IDs</h2>
-                <p class="bp-help-text mb-0">Reserve serial numbers before the batteries are made, so their labels can be printed. Each gets its battery ID (#6) now, fixed for life. A battery is then created by picking one.</p>
+                <h2>The battery ID settings aren't set, so batteries would get test IDs instead of their real battery IDs.</h2>
+                <p>Not set or not valid: <strong>@string.Join(", ", Model.MissingSettings)</strong>. Until whoever runs the app sets them and restarts it, nothing can be released and no battery can be created from a released ID. The demo data still works.</p>
             </div>
         </div>
+    }
 
-        @if (problems.Count > 0)
-        {
-            <div class="bp-release-problems" role="alert">
-                <h3>Fix these, then release again</h3>
-                <p>@Words.Count(problems.Count, "serial number or range", "serial numbers or ranges") can't be released, so none was. The form keeps what you typed.</p>
+    <section class="bp-card bp-console-form-card bp-ids-summary mt-4">
+        <div>
+            <h2>Battery IDs</h2>
+            <p class="bp-help-text mb-0">Serial numbers released before the batteries are made, each with its battery ID, so labels can be printed. Demo IDs aren't counted.</p>
+        </div>
+        <dl class="bp-ids-counts">
+            @foreach (var status in ReleasedIdStatuses.All)
+            {
+                <div class="is-@status"><dt>@ReleasedIdStatuses.Words(status)</dt><dd>@Model.Counts.GetValueOrDefault(status)</dd></div>
+            }
+        </dl>
+        <a class="bp-primary-button bp-ids-release-button" href="/admin/battery-ids?tab=new" aria-disabled="@(blocked ? "true" : null)">Release new IDs</a>
+    </section>
+
+    <nav class="bp-subtabs mt-4" aria-label="Battery IDs">
+        <a class="@TabClass(BatteryIdsTabs.Releases)" href="/admin/battery-ids" aria-current="@(Model.Tab == BatteryIdsTabs.Releases ? "page" : null)">Releases <span class="bp-subtab-count">@Model.Releases.Count</span></a>
+        <a class="@TabClass(BatteryIdsTabs.Ids)" href="/admin/battery-ids?tab=ids" aria-current="@(Model.Tab == BatteryIdsTabs.Ids ? "page" : null)">All IDs</a>
+        <a class="@TabClass(BatteryIdsTabs.New)" href="/admin/battery-ids?tab=new" aria-current="@(Model.Tab == BatteryIdsTabs.New ? "page" : null)">Release new IDs</a>
+    </nav>
+
+    @if (Model.Tab == BatteryIdsTabs.Releases)
+    {
+        <section class="bp-card bp-console-form-card bp-ids-tab" id="releases">
+            @if (Model.Releases.Count == 0)
+            {
+                <p class="mb-0">No battery IDs released yet. <a href="/admin/battery-ids?tab=new">Release new IDs</a>.</p>
+            }
+            else
+            {
                 <div class="bp-table-scroll">
-                    <table class="table align-middle mb-0 bp-console-table bp-problems-table">
-                        <thead><tr><th>Serial number or range</th><th>Why not</th></tr></thead>
+                    <table class="table align-middle mb-0 bp-console-table bp-releases-table">
+                        <thead><tr><th>Reference</th><th>Model</th><th>Released</th><th>IDs</th><th><span class="visually-hidden">Actions</span></th></tr></thead>
                         <tbody>
-                            @foreach (var problem in problems)
+                            @foreach (var release in Model.Releases)
                             {
-                                <tr><td data-label="Serial number or range"><code>@problem.What</code></td><td data-label="Why not">@problem.Reason</td></tr>
+                                var open = $"/admin/battery-ids/releases/{Uri.EscapeDataString(release.ReleaseId)}";
+                                <tr>
+                                    <td data-label="Reference"><a href="@open" class="bp-release-name">@release.Reference</a>@if (release.IsDemo) {<span class="bp-id-demo">Demo</span>}</td>
+                                    <td data-label="Model">@Model.ModelName(release.ModelId)</td>
+                                    <td data-label="Released">@ValueWords.Time(release.ReleasedAtUtc)<small class="bp-batteries-muted d-block">by @release.ReleasedBy</small></td>
+                                    <td data-label="IDs">
+                                        <span class="bp-release-counts">
+                                            <strong>@release.Total</strong>
+                                            @foreach (var status in ReleasedIdStatuses.All.Where(status => release.Count(status) > 0))
+                                            {
+                                                <span class="bp-id-status is-@status">@release.Count(status) @ReleasedIdStatuses.Words(status).ToLowerInvariant()</span>
+                                            }
+                                        </span>
+                                    </td>
+                                    <td data-label="Actions" class="bp-release-row-actions">
+                                        <a href="@open">Open</a>
+                                        <a href="/admin/battery-ids/export?release=@Uri.EscapeDataString(release.ReleaseId)"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Export to Excel</a>
+                                        @if (release.Count(ReleasedIdStatuses.Available) > 0)
+                                        {
+                                            <button type="button" class="bp-text-button" data-void-unused data-release-id="@release.ReleaseId" data-release-reference="@release.Reference" data-release-available="@release.Count(ReleasedIdStatuses.Available)">Void unused…</button>
+                                        }
+                                    </td>
+                                </tr>
                             }
                         </tbody>
                     </table>
                 </div>
-            </div>
-        }
-
-        <form method="post" action="/admin/battery-ids" class="bp-release-form">
-            @Html.AntiForgeryToken()
-            <div class="bp-release-grid">
-                <label>
-                    <span>Reference</span>
-                    <input type="text" name="reference" value="@Model.Form.Reference" maxlength="@BatteryIdPool.MaxReference" placeholder="e.g. PO 4512, line 2" required />
-                    <small class="bp-field-help">Shown in the list and the export.</small>
+            }
+        </section>
+    }
+    else if (Model.Tab == BatteryIdsTabs.Ids)
+    {
+        <section class="bp-card bp-console-form-card bp-ids-tab" id="released-ids">
+            <form method="get" action="/admin/battery-ids" class="bp-release-filter">
+                <input type="hidden" name="tab" value="ids" />
+                <label class="bp-release-search"><span>Serial number</span><input type="search" name="q" value="@filter.Query" placeholder="Part of one" /></label>
+                <label><span>Status</span>
+                    <select name="status">
+                        <option value="">All</option>
+                        @foreach (var status in ReleasedIdStatuses.All)
+                        {
+                            <option value="@status" selected="@(status == filter.Status)">@ReleasedIdStatuses.Words(status)</option>
+                        }
+                    </select>
+                </label>
+                <label><span>Release</span>
+                    <select name="release">
+                        <option value="">All</option>
+                        @foreach (var release in Model.Releases.Where(release => !release.IsDemo || filter.Demo))
+                        {
+                            <option value="@release.ReleaseId" selected="@(release.ReleaseId == filter.Reference)">@release.Reference</option>
+                        }
+                    </select>
                 </label>
-                <label>
-                    <span>Model <span class="bp-attr-optional">(optional)</span></span>
-                    <select name="modelId">
-                        <option value="">Any model</option>
+                <label><span>Model</span>
+                    <select name="model">
+                        <option value="">All</option>
+                        <option value="any" selected="@(filter.ModelId == "any")">Any model</option>
                         @foreach (var (id, name) in Model.Models)
                         {
-                            <option value="@id" selected="@(id == Model.Form.ModelId)">@name</option>
+                            <option value="@id" selected="@(id == filter.ModelId)">@name</option>
                         }
                     </select>
-                    <small class="bp-field-help">A model's IDs are offered first when creating a battery of it, and only for it.</small>
                 </label>
+                <label class="bp-release-demo"><input type="checkbox" name="demo" value="true" checked="@filter.Demo" /><span>Show demo IDs</span></label>
+                <div class="bp-release-filter-actions">
+                    <button type="submit" class="bp-secondary-button bp-button-compact">Show</button>
+                    @if (filtered)
+                    {
+                        <a href="/admin/battery-ids?tab=ids">Clear</a>
+                    }
+                </div>
+            </form>
+            <div class="bp-ids-list-head">
+                <p class="bp-help-text mb-0">@Words.Count(Model.Total, "ID")@(filtered ? " match" : null)@(Model.Items.Count < Model.Total ? $"; the first {Model.Items.Count} are shown" : null).</p>
+                <a class="bp-secondary-button bp-button-compact text-nowrap" href="/admin/battery-ids/export@(exportQuery.Length > 0 ? "?" + exportQuery : null)"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Export to Excel</a>
+            </div>
+            @if (Model.Items.Count == 0)
+            {
+                <p class="mb-0 mt-3">@(filtered ? "No released ID matches." : "No battery IDs released yet.")</p>
+            }
+            else
+            {
+                @await Html.PartialAsync("_IdRows", (Model.Items, (Func<string?, string>)Model.ModelName, true))
+            }
+        </section>
+    }
+    else
+    {
+        <section class="bp-card bp-product-editor-section bp-console-form-card bp-ids-tab" id="release">
+            <div class="bp-product-section-heading">
+                <div>
+                    <h2>Release new IDs</h2>
+                    <p class="bp-help-text mb-0">Each serial number gets its battery ID (#6) now, fixed for life. A battery is then created by picking one. After releasing, you land on the release's page to export it for the labels.</p>
+                </div>
             </div>
 
-            <fieldset class="bp-release-ranges" data-ranges>
-                <legend>Ranges</legend>
-                <p class="bp-field-help">The start and end share the letters before the number and its number of digits. The quantity checks the count.</p>
-                @foreach (var range in Model.Form.Ranges)
-                {
-                    <div class="bp-release-range" data-range-row>
-                        <label><span>Start</span><input type="text" name="start" value="@range.Start" placeholder="VPC-12221" autocomplete="off" /></label>
-                        <label><span>End</span><input type="text" name="end" value="@range.End" placeholder="VPC-12230" autocomplete="off" /></label>
-                        <label><span>Quantity</span><input type="text" name="quantity" value="@range.Quantity" placeholder="10" inputmode="numeric" autocomplete="off" /></label>
-                        <button type="button" class="bp-icon-button" data-remove-range aria-label="Remove this range" title="Remove this range"><svg class="bp-i" aria-hidden="true"><use href="#i-trash" /></svg></button>
+            @if (problems.Count > 0)
+            {
+                <div class="bp-release-problems" role="alert">
+                    <h3>Fix these, then release again</h3>
+                    <p>@Words.Count(problems.Count, "serial number or range", "serial numbers or ranges") can't be released, so none was. The form keeps what you typed.</p>
+                    <div class="bp-table-scroll">
+                        <table class="table align-middle mb-0 bp-console-table bp-problems-table">
+                            <thead><tr><th>Serial number or range</th><th>Why not</th></tr></thead>
+                            <tbody>
+                                @foreach (var problem in problems)
+                                {
+                                    <tr><td data-label="Serial number or range"><code>@problem.What</code></td><td data-label="Why not">@problem.Reason</td></tr>
+                                }
+                            </tbody>
+                        </table>
                     </div>
-                }
-                <button type="button" class="bp-text-button" data-add-range>+ Add a range</button>
-            </fieldset>
-
-            <label class="bp-release-list">
-                <span>Individual serial numbers <span class="bp-attr-optional">(optional)</span></span>
-                <textarea name="list" rows="4" placeholder="One per line, e.g.&#10;VPC-12301&#10;VPC-12305">@Model.Form.List</textarea>
-                <small class="bp-field-help">Pasted from a list, one per line. Use them with ranges or on their own.</small>
-            </label>
+                </div>
+            }
 
-            <div class="bp-release-actions">
-                <small class="bp-field-help">All or nothing: if any serial number can't be released, none is, and each reason is listed.</small>
-                <button type="submit" class="bp-primary-button">Check and release</button>
-            </div>
-        </form>
-    </section>
+            <form method="post" action="/admin/battery-ids" class="bp-release-form">
+                @Html.AntiForgeryToken()
+                <fieldset disabled="@blocked" class="bp-release-fields">
+                    <div class="bp-release-grid">
+                        <label>
+                            <span>Reference</span>
+                            <input type="text" name="reference" value="@Model.Form.Reference" maxlength="@BatteryIdPool.MaxReference" placeholder="e.g. PO 4512, line 2" required />
+                            <small class="bp-field-help">Shown in the lists and the export.</small>
+                        </label>
+                        <label>
+                            <span>Model <span class="bp-attr-optional">(optional)</span></span>
+                            <select name="modelId">
+                                <option value="">Any model</option>
+                                @foreach (var (id, name) in Model.Models)
+                                {
+                                    <option value="@id" selected="@(id == Model.Form.ModelId)">@name</option>
+                                }
+                            </select>
+                            <small class="bp-field-help">A model's IDs are offered first when creating a battery of it, and only for it.</small>
+                        </label>
+                    </div>
 
-    <section class="bp-card bp-product-editor-section bp-console-form-card mt-4" id="releases">
-        <div class="bp-product-section-heading">
-            <div>
-                <h2>Releases</h2>
-                <p class="bp-help-text mb-0">Each release with how many of its IDs are available, used by a draft, published or voided.</p>
-            </div>
-        </div>
-        @if (Model.Releases.Count == 0)
-        {
-            <p class="mb-0">No battery IDs released yet.</p>
-        }
-        else
-        {
-            <div class="bp-table-scroll">
-                <table class="table align-middle mb-0 bp-console-table bp-releases-table">
-                    <thead><tr><th>Reference</th><th>Model</th><th>Released</th><th>IDs</th><th><span class="visually-hidden">Actions</span></th></tr></thead>
-                    <tbody>
-                        @foreach (var release in Model.Releases)
+                    <fieldset class="bp-release-ranges" data-ranges>
+                        <legend>Ranges</legend>
+                        <p class="bp-field-help">The start and end share the letters before the number and its number of digits. The quantity checks the count.</p>
+                        @foreach (var range in Model.Form.Ranges)
                         {
-                            <tr class="@(release.ReleaseId == filter.Reference ? "table-active" : null)">
-                                <td data-label="Reference"><strong>@release.Reference</strong></td>
-                                <td data-label="Model">@Model.ModelName(release.ModelId)</td>
-                                <td data-label="Released">@ValueWords.Time(release.ReleasedAtUtc)<small class="bp-batteries-muted d-block">by @release.ReleasedBy</small></td>
-                                <td data-label="IDs">
-                                    <span class="bp-release-counts">
-                                        <strong>@release.Total</strong>
-                                        @foreach (var status in ReleasedIdStatuses.All.Where(status => release.Count(status) > 0))
-                                        {
-                                            <span class="bp-id-status is-@status">@release.Count(status) @ReleasedIdStatuses.Words(status).ToLowerInvariant()</span>
-                                        }
-                                    </span>
-                                </td>
-                                <td data-label="Actions" class="bp-release-row-actions">
-                                    <a href="/admin/battery-ids?release=@Uri.EscapeDataString(release.ReleaseId)#released-ids">Show its IDs</a>
-                                    @if (release.Count(ReleasedIdStatuses.Available) > 0)
-                                    {
-                                        <button type="button" class="bp-text-button is-danger" data-void-unused data-release-id="@release.ReleaseId" data-release-reference="@release.Reference" data-release-available="@release.Count(ReleasedIdStatuses.Available)">Void unused…</button>
-                                    }
-                                </td>
-                            </tr>
+                            <div class="bp-release-range" data-range-row>
+                                <label><span>Start</span><input type="text" name="start" value="@range.Start" placeholder="VPC-12221" autocomplete="off" /></label>
+                                <label><span>End</span><input type="text" name="end" value="@range.End" placeholder="VPC-12230" autocomplete="off" /></label>
+                                <label><span>Quantity</span><input type="text" name="quantity" value="@range.Quantity" placeholder="10" inputmode="numeric" autocomplete="off" /></label>
+                                <button type="button" class="bp-icon-button" data-remove-range aria-label="Remove this range" title="Remove this range"><svg class="bp-i" aria-hidden="true"><use href="#i-trash" /></svg></button>
+                            </div>
                         }
-                    </tbody>
-                </table>
-            </div>
-        }
-    </section>
+                        <button type="button" class="bp-text-button" data-add-range>+ Add a range</button>
+                    </fieldset>
 
-    <section class="bp-card bp-product-editor-section bp-console-form-card mt-4" id="released-ids">
-        <div class="bp-product-section-heading">
-            <div>
-                <h2>Released IDs</h2>
-                <p class="bp-help-text mb-0">@Words.Count(Model.Total, "ID")@(filtered ? " match" : null)@(Model.Items.Count < Model.Total ? $"; the first {Model.Items.Count} are shown, the export has them all" : null). The export has a row for each, with its QR code's full address for the label printer.</p>
-            </div>
-            <a class="bp-secondary-button bp-button-compact text-nowrap" href="/admin/battery-ids/export@(exportQuery)"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Export to Excel</a>
-        </div>
-        <form method="get" action="/admin/battery-ids#released-ids" class="bp-release-filter">
-            <label><span>Status</span>
-                <select name="status">
-                    <option value="">All</option>
-                    @foreach (var status in ReleasedIdStatuses.All)
-                    {
-                        <option value="@status" selected="@(status == filter.Status)">@ReleasedIdStatuses.Words(status)</option>
-                    }
-                </select>
-            </label>
-            <label><span>Release</span>
-                <select name="release">
-                    <option value="">All</option>
-                    @foreach (var release in Model.Releases)
-                    {
-                        <option value="@release.ReleaseId" selected="@(release.ReleaseId == filter.Reference)">@release.Reference</option>
-                    }
-                </select>
-            </label>
-            <label><span>Model</span>
-                <select name="model">
-                    <option value="">All</option>
-                    <option value="any" selected="@(filter.ModelId == "any")">Any model</option>
-                    @foreach (var (id, name) in Model.Models)
-                    {
-                        <option value="@id" selected="@(id == filter.ModelId)">@name</option>
-                    }
-                </select>
-            </label>
-            <label class="bp-release-search"><span>Serial number</span><input type="search" name="q" value="@filter.Query" placeholder="Part of one" /></label>
-            <div class="bp-release-filter-actions">
-                <button type="submit" class="bp-secondary-button bp-button-compact">Show</button>
-                @if (filtered)
-                {
-                    <a href="/admin/battery-ids#released-ids">Clear</a>
-                }
-            </div>
-        </form>
-        @if (Model.Items.Count == 0)
-        {
-            <p class="mb-0 mt-3">@(filtered ? "No released ID matches." : "No battery IDs released yet.")</p>
-        }
-        else
-        {
-            <div class="bp-table-scroll mt-3">
-                <table class="table align-middle mb-0 bp-console-table bp-released-table">
-                    <thead><tr><th>Serial number</th><th>Battery ID</th><th>Model</th><th>Reference</th><th>Status</th><th><span class="visually-hidden">Actions</span></th></tr></thead>
-                    <tbody>
-                        @foreach (var item in Model.Items)
-                        {
-                            <tr>
-                                <td data-label="Serial number"><strong>@item.SerialNumber</strong></td>
-                                <td data-label="Battery ID">@await Html.PartialAsync("_IdValue", item.Identifier)</td>
-                                <td data-label="Model">@Model.ModelName(item.ModelId)</td>
-                                <td data-label="Reference">@item.Reference<small class="bp-batteries-muted d-block">@ValueWords.Day(item.ReleasedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))</small></td>
-                                <td data-label="Status">
-                                    <span class="bp-id-status is-@item.Status">@ReleasedIdStatuses.Words(item.Status)</span>
-                                    @if (item.Status == ReleasedIdStatuses.Voided)
-                                    {
-                                        <small class="bp-batteries-muted d-block">@item.VoidReason</small>
-                                    }
-                                </td>
-                                <td data-label="Actions" class="bp-release-row-actions">
-                                    @if (item.PackId is { } packId)
-                                    {
-                                        <a href="@BatteryPages.BatteryHref(packId)">Open battery</a>
-                                    }
-                                    else if (item.Status == ReleasedIdStatuses.Available)
-                                    {
-                                        <button type="button" class="bp-text-button is-danger" data-void-id data-serial="@item.SerialNumber">Void…</button>
-                                    }
-                                </td>
-                            </tr>
-                        }
-                    </tbody>
-                </table>
-            </div>
-        }
-    </section>
+                    <label class="bp-release-list">
+                        <span>Individual serial numbers <span class="bp-attr-optional">(optional)</span></span>
+                        <textarea name="list" rows="4" placeholder="One per line, e.g.&#10;VPC-12301&#10;VPC-12305">@Model.Form.List</textarea>
+                        <small class="bp-field-help">Pasted from a list, one per line. Use them with ranges or on their own.</small>
+                    </label>
 
-    <div class="bp-console-form-card bp-dialog-host">
-        <dialog id="void-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="void-dialog-title">
-            <form method="post" action="/admin/battery-ids/void" class="bp-dialog-form">
-                @Html.AntiForgeryToken()
-                <input type="hidden" name="serialNumber" data-void-serial-field />
-                <input type="hidden" name="returnTo" value="@here" />
-                <h2 id="void-dialog-title">Void battery ID <span data-void-serial-name></span></h2>
-                <p class="bp-help-text">A voided ID is never used again, and stays listed with its reason. Only an available ID can be voided.</p>
-                <label><span>Reason</span><input type="text" name="reason" maxlength="@BatteryIdPool.MaxReason" placeholder="e.g. Label damaged" required /></label>
-                <div class="bp-dialog-actions">
-                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
-                    <button type="submit" class="bp-danger-button">Void the ID</button>
-                </div>
-            </form>
-        </dialog>
-        <dialog id="@BatteryIdsController.ReleaseDialog" class="bp-modal-panel bp-dialog" aria-labelledby="void-unused-title">
-            <form method="post" action="/admin/battery-ids/void-unused" class="bp-dialog-form">
-                @Html.AntiForgeryToken()
-                <input type="hidden" name="releaseId" data-void-release-field />
-                <input type="hidden" name="returnTo" value="@here" />
-                <h2 id="void-unused-title">Void the unused IDs of <span data-void-release-name></span></h2>
-                <p class="bp-help-text">Its <strong data-void-release-count></strong> available IDs are voided and never used again. IDs a battery already has stay as they are.</p>
-                <label><span>Reason</span><input type="text" name="reason" maxlength="@BatteryIdPool.MaxReason" placeholder="e.g. Order cancelled" required /></label>
-                <div class="bp-dialog-actions">
-                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
-                    <button type="submit" class="bp-danger-button">Void the unused IDs</button>
-                </div>
+                    <div class="bp-release-actions">
+                        <small class="bp-field-help">All or nothing: if any serial number can't be released, none is, and each reason is listed.</small>
+                        <button type="submit" class="bp-primary-button">Check and release</button>
+                    </div>
+                </fieldset>
             </form>
-        </dialog>
-    </div>
+        </section>
+    }
+
+    @await Html.PartialAsync("_VoidDialogs", here)
 </main>
 
 @section Scripts {
diff --git a/web/Views/BatteryIds/Release.cshtml b/web/Views/BatteryIds/Release.cshtml
new file mode 100644
index 0000000..0d35b08
--- /dev/null
+++ b/web/Views/BatteryIds/Release.cshtml
@@ -0,0 +1,59 @@
+@using BatteryPassWeb.Controllers
+@using BatteryPassWeb.Dpp.Identifiers
+@using BatteryPassWeb.Dpp.Pages
+@model ReleasePage
+@* One release's page (6d-2b): where a release lands, with its export at the top for the labels. *@
+@{
+    var release = Model.Release;
+    ViewData["Title"] = release.Reference;
+    var here = Context.Request.Path.ToString();
+    var export = $"/admin/battery-ids/export?release={Uri.EscapeDataString(release.ReleaseId)}";
+}
+
+<main class="bp-page bp-admin-page">
+    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
+    <link rel="stylesheet" href="~/css/bp-battery-ids.css" asp-append-version="true" />
+    <nav class="bp-breadcrumb" aria-label="Breadcrumb">
+        <a href="/admin/battery-ids">Battery IDs</a>
+        <span aria-current="page">@release.Reference</span>
+    </nav>
+    @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
+
+    <section class="bp-card bp-console-form-card bp-release-head mt-3">
+        <div class="bp-release-head-text">
+            <span class="bp-summary-title">Release@(release.IsDemo ? " · demo" : null)</span>
+            <h1>@release.Reference</h1>
+            <p class="mb-0">@Model.ModelName(release.ModelId) · released @ValueWords.Time(release.ReleasedAtUtc) by @release.ReleasedBy</p>
+            <span class="bp-release-counts mt-2">
+                <strong>@Words.Count(release.Total, "ID")</strong>
+                @foreach (var status in ReleasedIdStatuses.All.Where(status => release.Count(status) > 0))
+                {
+                    <span class="bp-id-status is-@status">@release.Count(status) @ReleasedIdStatuses.Words(status).ToLowerInvariant()</span>
+                }
+            </span>
+        </div>
+        <div class="bp-release-head-actions">
+            <a class="bp-primary-button" href="@export"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Export to Excel</a>
+            @if (release.Count(ReleasedIdStatuses.Available) > 0)
+            {
+                <button type="button" class="bp-secondary-button" data-void-unused data-release-id="@release.ReleaseId" data-release-reference="@release.Reference" data-release-available="@release.Count(ReleasedIdStatuses.Available)">Void unused…</button>
+            }
+        </div>
+        @if (Model.JustReleased)
+        {
+            <p class="bp-help-text bp-release-next mb-0">Next: export the IDs for the label printer, then create each battery from its serial number on <a href="/admin/batteries/new">Create battery</a>.</p>
+        }
+    </section>
+
+    <section class="bp-card bp-console-form-card mt-4">
+        <h2 class="h5">Its IDs</h2>
+        @await Html.PartialAsync("_IdRows", (Model.Items, (Func<string?, string>)Model.ModelName, false))
+    </section>
+
+    @await Html.PartialAsync("_VoidDialogs", here)
+</main>
+
+@section Scripts {
+    <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
+    <script src="~/js/bp-battery-ids.js" asp-append-version="true"></script>
+}
diff --git a/web/Views/BatteryIds/_IdRows.cshtml b/web/Views/BatteryIds/_IdRows.cshtml
new file mode 100644
index 0000000..64f1f4f
--- /dev/null
+++ b/web/Views/BatteryIds/_IdRows.cshtml
@@ -0,0 +1,43 @@
+@using BatteryPassWeb.Dpp.Identifiers
+@using BatteryPassWeb.Dpp.Pages
+@model (IReadOnlyList<ReleasedBatteryId> Items, Func<string?, string> ModelName, bool ShowReference)
+@* Released IDs as rows (6d-2b): serial number, battery ID, model, reference, status, and a small menu per row; voiding is
+   in the menu, not a red link on every row. A battery's ID opens its battery instead. *@
+<div class="bp-table-scroll">
+    <table class="table align-middle mb-0 bp-console-table bp-released-table">
+        <thead><tr><th>Serial number</th><th>Battery ID</th><th>Model</th><th>@(Model.ShowReference ? "Reference" : "Released")</th><th>Status</th><th><span class="visually-hidden">Actions</span></th></tr></thead>
+        <tbody>
+            @foreach (var item in Model.Items)
+            {
+                <tr>
+                    <td data-label="Serial number"><strong>@item.SerialNumber</strong>@if (item.IsDemo) {<span class="bp-id-demo">Demo</span>}</td>
+                    <td data-label="Battery ID">@await Html.PartialAsync("_IdValue", item.Identifier)</td>
+                    <td data-label="Model">@Model.ModelName(item.ModelId)</td>
+                    <td data-label="@(Model.ShowReference ? "Reference" : "Released")">@(Model.ShowReference ? item.Reference : ValueWords.Day(item.ReleasedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)))</td>
+                    <td data-label="Status">
+                        <span class="bp-id-status is-@item.Status">@ReleasedIdStatuses.Words(item.Status)</span>
+                        @if (item.Status == ReleasedIdStatuses.Voided)
+                        {
+                            <small class="bp-batteries-muted d-block">@item.VoidReason</small>
+                        }
+                    </td>
+                    <td data-label="Actions" class="bp-release-row-actions">
+                        @if (item.PackId is { } packId)
+                        {
+                            <a href="@BatteryPages.BatteryHref(packId)">Open battery</a>
+                        }
+                        else if (item.Status == ReleasedIdStatuses.Available)
+                        {
+                            <details class="bp-row-menu">
+                                <summary aria-label="More for @item.SerialNumber" title="More"><svg class="bp-i" aria-hidden="true"><use href="#i-more" /></svg></summary>
+                                <div class="bp-row-menu-list">
+                                    <button type="button" data-void-id data-serial="@item.SerialNumber">Void…<small>Never used again</small></button>
+                                </div>
+                            </details>
+                        }
+                    </td>
+                </tr>
+            }
+        </tbody>
+    </table>
+</div>
diff --git a/web/Views/BatteryIds/_VoidDialogs.cshtml b/web/Views/BatteryIds/_VoidDialogs.cshtml
new file mode 100644
index 0000000..aaba583
--- /dev/null
+++ b/web/Views/BatteryIds/_VoidDialogs.cshtml
@@ -0,0 +1,34 @@
+@using BatteryPassWeb.Controllers
+@using BatteryPassWeb.Dpp.Identifiers
+@model string
+@* The two void windows (6d-2b), on the tabs and on a release's page; they come back to the page they were opened from. *@
+<div class="bp-console-form-card bp-dialog-host">
+    <dialog id="void-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="void-dialog-title">
+        <form method="post" action="/admin/battery-ids/void" class="bp-dialog-form">
+            @Html.AntiForgeryToken()
+            <input type="hidden" name="serialNumber" data-void-serial-field />
+            <input type="hidden" name="returnTo" value="@Model" />
+            <h2 id="void-dialog-title">Void battery ID <span data-void-serial-name></span></h2>
+            <p class="bp-help-text">A voided ID is never used again, and stays listed with its reason. Only an available ID can be voided.</p>
+            <label><span>Reason</span><input type="text" name="reason" maxlength="@BatteryIdPool.MaxReason" placeholder="e.g. Label damaged" required /></label>
+            <div class="bp-dialog-actions">
+                <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
+                <button type="submit" class="bp-danger-button">Void the ID</button>
+            </div>
+        </form>
+    </dialog>
+    <dialog id="@BatteryIdsController.ReleaseDialog" class="bp-modal-panel bp-dialog" aria-labelledby="void-unused-title">
+        <form method="post" action="/admin/battery-ids/void-unused" class="bp-dialog-form">
+            @Html.AntiForgeryToken()
+            <input type="hidden" name="releaseId" data-void-release-field />
+            <input type="hidden" name="returnTo" value="@Model" />
+            <h2 id="void-unused-title">Void the unused IDs of <span data-void-release-name></span></h2>
+            <p class="bp-help-text">Its <strong data-void-release-count></strong> available IDs are voided and never used again. IDs a battery already has stay as they are.</p>
+            <label><span>Reason</span><input type="text" name="reason" maxlength="@BatteryIdPool.MaxReason" placeholder="e.g. Order cancelled" required /></label>
+            <div class="bp-dialog-actions">
+                <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
+                <button type="submit" class="bp-danger-button">Void the unused IDs</button>
+            </div>
+        </form>
+    </dialog>
+</div>
diff --git a/web/wwwroot/css/bp-battery-ids.css b/web/wwwroot/css/bp-battery-ids.css
index dc2bcca..68c9fd7 100644
--- a/web/wwwroot/css/bp-battery-ids.css
+++ b/web/wwwroot/css/bp-battery-ids.css
@@ -322,3 +322,243 @@
     overflow-wrap: anywhere;
   }
 }
+
+/* 6d-2b, the owner's layout: the summary with "Release new IDs", the three tabs, the per-row menu and a release's page. */
+.bp-ids-summary {
+  display: grid;
+  grid-template-columns: minmax(0, 1fr) auto auto;
+  align-items: center;
+  gap: 20px;
+}
+
+.bp-ids-summary h2 {
+  margin: 0 0 2px;
+  font-size: 1.25rem;
+}
+
+.bp-ids-counts {
+  display: flex;
+  gap: 8px;
+  margin: 0;
+}
+
+.bp-ids-counts > div {
+  min-width: 84px;
+  padding: 8px 12px;
+  border: 1px solid var(--bp-border);
+  border-radius: 10px;
+  text-align: center;
+}
+
+.bp-ids-counts dt {
+  font-size: 0.74rem;
+  font-weight: 700;
+  letter-spacing: 0.03em;
+  text-transform: uppercase;
+  color: var(--bp-muted);
+}
+
+.bp-ids-counts dd {
+  margin: 0;
+  font-size: 1.35rem;
+  font-weight: 700;
+  color: var(--bp-text);
+}
+
+.bp-ids-counts .is-available dd { color: #17663f; }
+.bp-ids-counts .is-voided dd { color: #6b7280; }
+.bp-ids-release-button[aria-disabled="true"] { opacity: 0.55; pointer-events: none; }
+
+.bp-subtabs {
+  display: flex;
+  gap: 4px;
+  border-bottom: 1px solid var(--bp-border);
+}
+
+.bp-subtab {
+  display: inline-flex;
+  align-items: center;
+  gap: 6px;
+  margin-bottom: -1px;
+  padding: 10px 14px;
+  border-bottom: 3px solid transparent;
+  color: var(--bp-muted);
+  font-weight: 600;
+  text-decoration: none;
+}
+
+.bp-subtab:hover { color: var(--bp-text); }
+
+.bp-subtab.is-active {
+  border-bottom-color: var(--bp-primary);
+  color: var(--bp-text);
+}
+
+.bp-subtab-count {
+  padding: 0 7px;
+  border-radius: 999px;
+  background: var(--bp-border);
+  font-size: 0.78rem;
+}
+
+.bp-ids-tab {
+  border-top-left-radius: 0;
+  border-top-right-radius: 0;
+}
+
+.bp-release-name { font-weight: 700; }
+
+.bp-id-demo {
+  display: inline-flex;
+  margin-left: 8px;
+  padding: 0 7px;
+  border: 1px solid #f0c36d;
+  border-radius: 999px;
+  background: #fff8e6;
+  color: #7a4b00;
+  font-size: 0.72rem;
+  font-weight: 700;
+  vertical-align: middle;
+}
+
+.bp-release-row-actions a {
+  display: inline-flex;
+  align-items: center;
+  gap: 4px;
+}
+
+.bp-release-demo {
+  display: flex !important;
+  align-items: center;
+  gap: 6px;
+  align-self: end;
+  padding-bottom: 8px;
+  white-space: nowrap;
+}
+
+.bp-release-filter {
+  grid-template-columns: minmax(0, 1fr) repeat(3, minmax(0, 9rem)) auto auto;
+}
+
+.bp-ids-list-head {
+  display: flex;
+  align-items: center;
+  justify-content: space-between;
+  gap: 12px;
+  margin: 16px 0 8px;
+}
+
+.bp-release-fields {
+  display: grid;
+  gap: 18px;
+  min-width: 0;
+  margin: 0;
+  padding: 0;
+  border: 0;
+}
+
+.bp-row-menu { position: relative; display: inline-block; }
+.bp-row-menu > summary { list-style: none; cursor: pointer; display: inline-flex; padding: 4px 8px; border: 1px solid var(--bp-border); border-radius: 8px; color: var(--bp-muted); }
+.bp-row-menu > summary::-webkit-details-marker { display: none; }
+.bp-row-menu-list {
+  position: absolute;
+  right: 0;
+  z-index: 5;
+  display: grid;
+  min-width: 190px;
+  margin-top: 4px;
+  padding: 6px;
+  border: 1px solid var(--bp-border);
+  border-radius: 10px;
+  background: var(--bp-surface);
+  box-shadow: 0 10px 28px rgba(10, 40, 80, 0.14);
+  text-align: left;
+  white-space: normal;
+}
+
+.bp-row-menu-list button {
+  display: block;
+  width: 100%;
+  padding: 8px 10px;
+  border: 0;
+  border-radius: 8px;
+  background: none;
+  color: var(--bp-text);
+  font: inherit;
+  font-weight: 600;
+  text-align: left;
+  cursor: pointer;
+}
+
+.bp-row-menu-list button:hover { background: rgba(42, 110, 207, 0.08); }
+.bp-row-menu-list small { display: block; color: var(--bp-muted); font-size: 0.78rem; font-weight: 400; }
+
+.bp-release-head {
+  display: grid;
+  grid-template-columns: minmax(0, 1fr) auto;
+  gap: 16px 24px;
+  align-items: start;
+}
+
+.bp-release-head h1 { margin: 2px 0 4px; font-size: 1.6rem; }
+.bp-release-head-actions { display: flex; flex-wrap: wrap; gap: 10px; justify-content: flex-end; }
+.bp-release-head-actions .bp-primary-button { display: inline-flex; align-items: center; gap: 6px; }
+.bp-release-next { grid-column: 1 / -1; }
+
+[data-theme="dark"] .bp-id-demo { border-color: rgba(240, 195, 109, 0.5); background: rgba(240, 195, 109, 0.12); color: #f5d38f; }
+[data-theme="dark"] .bp-ids-counts .is-available dd { color: #86efac; }
+
+@media (max-width: 767px) {
+  .bp-ids-summary { grid-template-columns: minmax(0, 1fr); }
+  .bp-ids-counts { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
+  .bp-ids-release-button { justify-self: start; }
+  .bp-subtabs { overflow-x: auto; }
+  .bp-subtab { padding: 10px 10px; white-space: nowrap; }
+  .bp-release-filter { grid-template-columns: minmax(0, 1fr); }
+  .bp-release-demo { padding-bottom: 0; }
+  .bp-ids-list-head { flex-wrap: wrap; }
+  .bp-release-head { grid-template-columns: minmax(0, 1fr); }
+  .bp-release-head-actions { justify-content: flex-start; }
+  .bp-releases-table td:nth-child(5) { display: flex !important; flex-wrap: wrap; gap: 6px 16px; }
+  .bp-releases-table td:nth-child(5) > * + * { margin-left: 0 !important; }
+  .bp-row-menu-list { right: auto; left: 0; }
+}
+
+/* The missing-settings notice (6d-2b): a red box, as the refusals', not a quiet status line. */
+.bp-settings-missing {
+  display: flex;
+  gap: 12px;
+  align-items: flex-start;
+  padding: 14px 16px;
+  border: 1px solid #e08080;
+  border-left-width: 5px;
+  border-radius: 12px;
+  background: #fff1f1;
+  color: #5c1010;
+}
+
+.bp-settings-missing svg {
+  flex: none;
+  width: 24px;
+  height: 24px;
+  fill: none;
+  stroke: #b42318;
+  stroke-width: 2;
+  stroke-linecap: round;
+  stroke-linejoin: round;
+}
+
+.bp-settings-missing h2 {
+  margin: 0 0 4px;
+  font-size: 1rem;
+  font-weight: 700;
+  color: #a12020;
+}
+
+.bp-settings-missing p { margin: 0; }
+
+.bp-release-fields:disabled .bp-primary-button { opacity: 0.5; cursor: not-allowed; }
+
+[data-theme="dark"] .bp-settings-missing { border-color: rgba(248, 113, 113, 0.6); background: rgba(127, 29, 29, 0.3); color: #fde2e2; }
+[data-theme="dark"] .bp-settings-missing h2 { color: #fca5a5; }
+[data-theme="dark"] .bp-settings-missing svg { stroke: #fca5a5; }
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index f41f5a1..46f67cf 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -562,3 +562,10 @@ th .bp-status-help-words {
   white-space: normal;
   overflow-wrap: anywhere;
 }
+
+/* Step 6d: a value can be a long web address with no spaces (a passport ID, https://www.sib-scania-eubp.eu/dpp/…/v4), so
+   a field's value may break anywhere rather than push a phone's page sideways. */
+.bp-field-grid dd {
+  min-width: 0;
+  overflow-wrap: anywhere;
+}
diff --git a/web/wwwroot/js/bp-battery-ids.js b/web/wwwroot/js/bp-battery-ids.js
index 11c0f78..7d28aa6 100644
--- a/web/wwwroot/js/bp-battery-ids.js
+++ b/web/wwwroot/js/bp-battery-ids.js
@@ -31,6 +31,7 @@
         button.addEventListener('click', () => {
             voidDialog.querySelector('[data-void-serial-field]').value = button.dataset.serial;
             voidDialog.querySelector('[data-void-serial-name]').textContent = button.dataset.serial;
+            button.closest('details')?.removeAttribute('open');
             voidDialog.showModal();
         });
     });
```

- [ ] **Step 4: See the tests pass**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~BatteryIdSettingsAndPagesTests|FullyQualifiedName~BatteryIdPoolTests|FullyQualifiedName~DunsIdentifierTests"`
Expected: `Passed!  - Failed:     0, Passed:    65`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs web/Views/BatteryIds/Release.cshtml web/Views/BatteryIds/_IdRows.cshtml web/Views/BatteryIds/_VoidDialogs.cshtml web/Controllers/BatteryIdsController.cs web/Dpp/Api/BatteryApi.cs web/Dpp/Editing/PackCreationService.cs web/Dpp/Identifiers/BatteryIdPool.cs web/Dpp/Identifiers/DunsIdentifierScheme.cs web/Dpp/Pages/PassportPages.cs web/Views/BatteryIds/Index.cshtml web/wwwroot/css/bp-battery-ids.css web/wwwroot/css/bp-status.css web/wwwroot/js/bp-battery-ids.js
git commit -m "feat(dpp): missing battery ID settings refused with a red notice; the Battery IDs page in tabs with a page per release" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2611, Skipped:     0, Total:  2611`. That is the 2602 from before plus 9 new test cases.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat db7bd45 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

These are not steps of this plan. The managing session brings them to the product owner, on the v3 app after "Reset demo data":

1. Started **with** the three ID settings: Admin → Battery IDs opens on Releases, with the summary and "Release new IDs" at the top.
2. Release new IDs: VPC-12221 to VPC-12230 (10) for PO 4512, line 2. The app lands on that release's page; "Export to Excel" there gives those 10 only.
3. All IDs: search VPC-122; ⋯ on a row → Void… asks for a reason. Tick "Show demo IDs" to see the demo ones too.
4. Started **without** one of the settings: the red notice names it, "Release new IDs" is disabled, and creating a battery from VPC-12222 is refused with the same reason. A demo spare (DEMO-SPARE-0001) still makes a battery.

## Next

Plan 6d-3: the team's Help for the release flow (the tabs and the release page), the test pack and the scenario matrix, and the role check re-run.
