# DPP V3 Step 6d-4: Voided Battery IDs Hidden by Default — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A small request from the product owner (9 Nov 2026), who already approved the behaviour: the Battery IDs page hides voided IDs by default, with a **"Show voided"** tick box like "Show demo IDs".
- **All IDs:** voided IDs are hidden unless "Show voided" is ticked, or the Status filter is set to Voided. Its export follows what is shown. The count line says how many voided IDs are hidden, and an empty list says when only voided IDs match.
- **Releases:** a release whose IDs are **all** voided is hidden unless "Show voided" is ticked. A partly voided release stays, with its counts. The tab says how many releases are hidden.
- **A release's own page and its export:** unchanged, the full record with every ID and its status.
- **The summary counts:** unchanged; Voided still shows there.

**Architecture:**
- `web/Dpp/Identifiers/BatteryIdPool.cs`:
  - `ReleasedIdFilter` gets `Voided` and `ShowsVoided` (`Voided`, or the status Voided); `ListAsync` leaves voided IDs out unless `ShowsVoided`;
  - `ReleaseSummary.AllVoided`. `ReleasesAsync` still returns every release, so `ReleaseAsync` still finds one whose IDs are all voided.
- `web/Controllers/BatteryIdsController.cs`:
  - `Index` and `Export` take `voided`;
  - `PageAsync` leaves out the all-voided releases unless `ShowsVoided`, and counts what it hides (`HiddenReleases`, `HiddenVoidedIds`, new on `BatteryIdsPage`). The All IDs Release filter follows the same list;
  - a release's page asks for every ID (`Voided: true`).
- **A release filter on All IDs doesn't bring voided IDs back**; only a release's own page and its export are the full record. Both export links of a release (its row on Releases, and its page) add `&voided=true`.
- `web/Views/BatteryIds/Index.cshtml`: the box on both tabs (a small form at the top of Releases; one more box in All IDs' filter); the notes; the export query.
- `web/Views/BatteryIds/Release.cshtml`: its export asks for every ID.
- `web/wwwroot/css/bp-battery-ids.css`: one more column in All IDs' filter row, and the Releases tab's small form. **`site.css` is not changed.**
- The Help's Battery IDs part, the test pack's check and the per-role script's step 14 describe the box.
- **Tests:**
  - new `BatteryPassWeb.Tests/Dpp/BatteryIdsVoidedTests.cs` (All IDs, its export, Releases, a release's page, the views);
  - `BatteryIdSettingsAndPagesTests` calls `Index` and `Export` with the new argument;
  - `BatteryIdsHelpTests` asks for the new words in the Help, the test pack and the script.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB, xUnit 2.9. Nothing new is downloaded.

**The shots** (no approval round: the owner approved the behaviour): `step6d4-shots/` in the managing session's notes, Releases and All IDs, unticked and ticked, at 1280 and 375. They were taken on a scratch app at a314d41 plus this plan, with the three ID settings, after "Reset demo data", with two real releases: "PO 4512, line 2" (ten IDs, VPC-12230 voided) and "PO 4519" (three IDs, all voided with "Void unused…"). Nothing scrolls sideways at either width.

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at a314d41 ("docs(dpp): the cut-over runbook's battery ID settings and checks; decision 38…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -3`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas. The tests use their own throwaway MongoDB.
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own classes. The whole suite runs once**, in the final checks. Never run two whole suites at once.
- **Baseline:** the whole suite shows `Passed:  2615` (plan 6d-3's result). After this plan: 2620.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6d4-test.patch`). Then run `git apply ../<name>` from the worktree root. New files are given inside the patches.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.

**Measured on a clean clone (a314d41):**
- the tests first fail: the build fails with CS1501 (`Index` and `Export` take no `voided`) and CS1061 (no `HiddenVoidedIds`, `HiddenReleases` or `ReleaseSummary.AllVoided`);
- with the changes, Task 1's own classes pass: 16 tests in its three classes;
- the whole suite: 2618 of 2620 passed (the clean clone's two known `ApplicationAuditCoverageTests` failures, `site.css` with CRLF line endings, pass in the worktree).

### Task 1: Voided IDs hidden by default

**Files:**
- Create: `BatteryPassWeb.Tests/Dpp/BatteryIdsVoidedTests.cs`
- Modify (tests): `BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs`, `BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs`
- Modify (app and documents): `docs/end-user-testing-guide.md`, `docs/qa-test-pack.md`, `web/Controllers/BatteryIdsController.cs`, `web/Dpp/Identifiers/BatteryIdPool.cs`, `web/Views/BatteryIds/Index.cshtml`, `web/Views/BatteryIds/Release.cshtml`, `web/Views/Help/Guide.cshtml`, `web/wwwroot/css/bp-battery-ids.css`

- [ ] **Step 1: Write the failing tests**

Save as `../6d4-test.patch` and run `git apply ../6d4-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs b/BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs
index 1d9ca80..37fbd48 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs
@@ -99,7 +99,7 @@ public sealed class BatteryIdSettingsAndPagesTests(MongoFixture mongo)
         var (rig, _, _) = await SeededAsync(mongo, dunsIds: false);
         using var __ = rig;
 
-        var page = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await Controller(rig).Index(null, null, null, null, null, false, CancellationToken.None)).Model);
+        var page = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await Controller(rig).Index(null, null, null, null, null, false, false, CancellationToken.None)).Model);
 
         Assert.Equal(BatteryIdsTabs.Releases, page.Tab);
         Assert.Equal(AllThree, page.MissingSettings);
@@ -115,8 +115,8 @@ public sealed class BatteryIdSettingsAndPagesTests(MongoFixture mongo)
         await pool.ReleaseAsync(Who, new ReleaseRequest("PO 2", null, [], "REAL-0001\nREAL-0002"));
         var controller = Controller(rig);
 
-        var real = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(BatteryIdsTabs.Ids, null, null, null, null, false, CancellationToken.None)).Model);
-        var withDemo = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(BatteryIdsTabs.Ids, null, null, null, null, true, CancellationToken.None)).Model);
+        var real = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(BatteryIdsTabs.Ids, null, null, null, null, false, false, CancellationToken.None)).Model);
+        var withDemo = Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(BatteryIdsTabs.Ids, null, null, null, null, true, false, CancellationToken.None)).Model);
 
         Assert.Equal(["REAL-0001", "REAL-0002"], real.Items.Select(item => item.SerialNumber).Order());
         Assert.Contains(withDemo.Items, item => item.IsDemo);
@@ -135,7 +135,7 @@ public sealed class BatteryIdSettingsAndPagesTests(MongoFixture mongo)
         var done = Assert.IsType<RedirectResult>(await controller.ReleaseIds("PO 7, line 1", null, ["LBL-0001"], ["LBL-0003"], ["3"], null, CancellationToken.None));
         var release = (await pool.ReleasesAsync()).Single(summary => summary.Reference == "PO 7, line 1");
         var page = Assert.IsType<ReleasePage>(Assert.IsType<ViewResult>(await controller.Release(release.ReleaseId, true, CancellationToken.None)).Model);
-        var export = Assert.IsType<FileContentResult>(await controller.Export(null, release.ReleaseId, null, null, false, CancellationToken.None));
+        var export = Assert.IsType<FileContentResult>(await controller.Export(null, release.ReleaseId, null, null, false, false, CancellationToken.None));
 
         Assert.Equal($"/admin/battery-ids/releases/{Uri.EscapeDataString(release.ReleaseId)}?released=true", done.Url);
         Assert.Equal("Released 3 battery IDs. Export them to Excel for the labels.", controller.TempData[ProductDataController.StatusKey]);
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryIdsVoidedTests.cs b/BatteryPassWeb.Tests/Dpp/BatteryIdsVoidedTests.cs
new file mode 100644
index 0000000..855159d
--- /dev/null
+++ b/BatteryPassWeb.Tests/Dpp/BatteryIdsVoidedTests.cs
@@ -0,0 +1,163 @@
+using System.IO.Compression;
+using System.Security.Claims;
+using BatteryPassWeb.Controllers;
+using BatteryPassWeb.Dpp.Identifiers;
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
+/// Step 6d-4 (the owner, 9 Nov 2026): voided IDs are hidden by default, with a "Show voided" tick box like "Show demo
+/// IDs". All IDs hides them unless it is ticked or the status asked for is Voided, and its export follows. Releases hides a
+/// release whose IDs are all voided; a partly voided one stays with its counts. A release's own page and its export are
+/// the full record, and the summary counts are unchanged.
+/// </summary>
+[Collection(MongoCollection.Name)]
+public sealed class BatteryIdsVoidedTests(MongoFixture mongo)
+{
+    private const string Who = DemoRig.Who;
+
+    /// <summary>"PO keep" (KEEP-0001 to 0003, KEEP-0002 voided) and "PO gone" (GONE-0001 and 0002, both voided).</summary>
+    private static async Task<(DemoRig Rig, BatteryIdPool Pool, string Keep, string Gone)> SeededAsync(MongoFixture mongo)
+    {
+        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
+        await rig.Demo.SeedAsync(Who);
+        var pool = rig.Services.GetRequiredService<BatteryIdPool>();
+        var keep = await pool.ReleaseAsync(Who, new ReleaseRequest("PO keep", null, [], "KEEP-0001\nKEEP-0002\nKEEP-0003"));
+        var gone = await pool.ReleaseAsync(Who, new ReleaseRequest("PO gone", null, [], "GONE-0001\nGONE-0002"));
+        Assert.Null(await pool.VoidAsync(Who, "KEEP-0002", "Label damaged"));
+        Assert.Equal((2L, (string?)null), await pool.VoidUnusedAsync(Who, gone.ReleaseId!, "Order cancelled"));
+        return (rig, pool, keep.ReleaseId!, gone.ReleaseId!);
+    }
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
+    private static async Task<BatteryIdsPage> PageAsync(BatteryIdsController controller, string? tab, string? status = null, string? release = null, bool voided = false) =>
+        Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(tab, status, release, null, null, false, voided, CancellationToken.None)).Model);
+
+    private static string[] Serials(IEnumerable<ReleasedBatteryId> items) => items.Select(item => item.SerialNumber).Order(StringComparer.Ordinal).ToArray();
+
+    private static string Sheet(IActionResult result)
+    {
+        using var zip = new ZipArchive(new MemoryStream(Assert.IsType<FileContentResult>(result).FileContents));
+        return new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
+    }
+
+    [Fact]
+    public async Task AllIds_ShouldHideVoidedIds_UnlessShowVoidedIsTicked_OrTheStatusAskedForIsVoided()
+    {
+        var (rig, _, keep, _) = await SeededAsync(mongo);
+        using var __ = rig;
+        var controller = Controller(rig);
+
+        var hidden = await PageAsync(controller, BatteryIdsTabs.Ids);
+        var shown = await PageAsync(controller, BatteryIdsTabs.Ids, voided: true);
+        var asked = await PageAsync(controller, BatteryIdsTabs.Ids, status: ReleasedIdStatuses.Voided);
+        var oneRelease = await PageAsync(controller, BatteryIdsTabs.Ids, release: keep);
+
+        Assert.Equal(["KEEP-0001", "KEEP-0003"], Serials(hidden.Items));
+        Assert.Equal(3, hidden.HiddenVoidedIds); // the list says how many it hides
+        Assert.Equal(["GONE-0001", "GONE-0002", "KEEP-0001", "KEEP-0002", "KEEP-0003"], Serials(shown.Items));
+        Assert.Equal(0, shown.HiddenVoidedIds);
+        Assert.Equal(["GONE-0001", "GONE-0002", "KEEP-0002"], Serials(asked.Items));
+        Assert.Equal(["KEEP-0001", "KEEP-0003"], Serials(oneRelease.Items)); // a release filter doesn't bring them back
+        Assert.Equal(3, hidden.Counts.GetValueOrDefault(ReleasedIdStatuses.Voided)); // the summary still counts them
+    }
+
+    [Fact]
+    public async Task AllIdsExport_ShouldFollowWhatIsShown_WhileAReleasesExportHasEveryId()
+    {
+        var (rig, _, _, gone) = await SeededAsync(mongo);
+        using var __ = rig;
+        var controller = Controller(rig);
+
+        var hidden = Sheet(await controller.Export(null, null, null, null, false, false, CancellationToken.None));
+        var shown = Sheet(await controller.Export(null, null, null, null, false, true, CancellationToken.None));
+        var asked = Sheet(await controller.Export(ReleasedIdStatuses.Voided, null, null, null, false, false, CancellationToken.None));
+        var release = Sheet(await controller.Export(null, gone, null, null, false, true, CancellationToken.None));
+
+        Assert.Contains("KEEP-0001", hidden, StringComparison.Ordinal);
+        Assert.DoesNotContain("KEEP-0002", hidden, StringComparison.Ordinal);
+        Assert.DoesNotContain("GONE-0001", hidden, StringComparison.Ordinal);
+        Assert.Contains("KEEP-0002", shown, StringComparison.Ordinal);
+        Assert.Contains("GONE-0001", shown, StringComparison.Ordinal);
+        Assert.Contains("GONE-0002", asked, StringComparison.Ordinal);
+        Assert.DoesNotContain("KEEP-0001", asked, StringComparison.Ordinal);
+        Assert.Contains("GONE-0001", release, StringComparison.Ordinal);
+        Assert.Contains("GONE-0002", release, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public async Task Releases_ShouldHideAReleaseWithOnlyVoidedIds_UnlessShowVoidedIsTicked_AndKeepAPartlyVoidedOne()
+    {
+        var (rig, _, keep, gone) = await SeededAsync(mongo);
+        using var __ = rig;
+        var controller = Controller(rig);
+
+        var hidden = await PageAsync(controller, null);
+        var shown = await PageAsync(controller, null, voided: true);
+
+        Assert.DoesNotContain(hidden.Releases, release => release.ReleaseId == gone);
+        Assert.Equal(1, hidden.HiddenReleases);
+        var partly = Assert.Single(hidden.Releases, release => release.ReleaseId == keep);
+        Assert.Equal(1, partly.Count(ReleasedIdStatuses.Voided)); // with its counts
+        Assert.Equal(2, partly.Count(ReleasedIdStatuses.Available));
+        Assert.False(partly.AllVoided);
+        Assert.True(Assert.Single(shown.Releases, release => release.ReleaseId == gone).AllVoided);
+        Assert.Equal(0, shown.HiddenReleases);
+        Assert.Contains(hidden.Releases, release => release.IsDemo);
+    }
+
+    [Fact]
+    public async Task AReleasesOwnPage_ShouldListEveryIdWithItsStatus_EvenWhenAllAreVoided()
+    {
+        var (rig, pool, keep, gone) = await SeededAsync(mongo);
+        using var __ = rig;
+        var controller = Controller(rig);
+
+        var allVoided = Assert.IsType<ReleasePage>(Assert.IsType<ViewResult>(await controller.Release(gone, false, CancellationToken.None)).Model);
+        var partly = Assert.IsType<ReleasePage>(Assert.IsType<ViewResult>(await controller.Release(keep, false, CancellationToken.None)).Model);
+
+        Assert.Equal(["GONE-0001", "GONE-0002"], Serials(allVoided.Items));
+        Assert.All(allVoided.Items, item => Assert.Equal(ReleasedIdStatuses.Voided, item.Status));
+        Assert.Equal(["KEEP-0001", "KEEP-0002", "KEEP-0003"], Serials(partly.Items));
+        Assert.NotNull(await pool.ReleaseAsync(gone)); // still found by its own address
+    }
+
+    [Fact]
+    public void TheViews_ShouldOfferShowVoided_AndAReleasesExportShouldAskForEveryId()
+    {
+        var index = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Index.cshtml"));
+        var release = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Release.cshtml"));
+
+        Assert.Equal(2, index.Split("<input type=\"checkbox\" name=\"voided\" value=\"true\" checked=\"@filter.Voided\" /><span>Show voided</span>").Length - 1); // on Releases and All IDs
+        Assert.Contains("(\"voided\", filter.Voided ? \"true\" : null)", index, StringComparison.Ordinal); // All IDs' export follows the box
+        Assert.Contains("/admin/battery-ids/export?release=@Uri.EscapeDataString(release.ReleaseId)&voided=true", index, StringComparison.Ordinal);
+        Assert.Contains("/admin/battery-ids/export?release={Uri.EscapeDataString(release.ReleaseId)}&voided=true", release, StringComparison.Ordinal);
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
diff --git a/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs b/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs
index 3261c52..ddb9e6f 100644
--- a/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs
+++ b/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs
@@ -27,7 +27,7 @@ public sealed class BatteryIdsHelpTests
 
         Assert.True(Guide.IndexOf("@if (isTeam)", StringComparison.Ordinal) < Guide.IndexOf("id=\"battery-ids\"", StringComparison.Ordinal)); // the team's only
         Assert.Contains("<a href=\"@Part(\"battery-ids\")\">Battery IDs</a>", Read("web", "Views", "Shared", "_HelpContents.cshtml"), StringComparison.Ordinal);
-        foreach (var words in new[] { "href=\"/admin/battery-ids\"", "all or nothing", "the app opens the release's page", "Releases", "All IDs", "Show demo IDs", "⋯ menu", "Export to Excel", "Available, Used", "Voided", "Void unused…", "Change its battery ID…", "Removing a draft gives its ID back", "R1, R2", "The battery ID settings aren't set", "DPP_ID_BASE_URL", "The demo data works without them" })
+        foreach (var words in new[] { "href=\"/admin/battery-ids\"", "all or nothing", "the app opens the release's page", "Releases", "All IDs", "Show demo IDs", "A release whose IDs are all voided shows only with \"Show voided\" ticked", "voided IDs only with \"Show voided\" ticked or the status Voided chosen", "⋯ menu", "Export to Excel", "Available, Used", "Voided", "Void unused…", "Change its battery ID…", "Removing a draft gives its ID back", "R1, R2", "The battery ID settings aren't set", "DPP_ID_BASE_URL", "The demo data works without them" })
         {
             Assert.Contains(words, part, StringComparison.Ordinal);
         }
@@ -52,7 +52,7 @@ public sealed class BatteryIdsHelpTests
 
         Assert.Contains("| Mark as invalid | Battery IDs tab |", pack, StringComparison.Ordinal); // a column for every reader
         Assert.Equal(11, pack.Split('\n').Count(line => line.StartsWith('|') && (line.EndsWith("| – (Not allowed) |", StringComparison.Ordinal) || line.EndsWith("| – (sign-in) |", StringComparison.Ordinal) || line.EndsWith("| ✓ (and undo) | ✓ |", StringComparison.Ordinal))));
-        foreach (var check in new[] { "Battery IDs: a range releases", "lists every reason and releases nothing", "Create battery picks a released serial number", "opens the release's page", "The Battery IDs page opens on Releases", "\"Show demo IDs\" ticked", "red notice naming it", "\"Change its battery ID…\" on a draft", "\"Not published yet\"" })
+        foreach (var check in new[] { "Battery IDs: a range releases", "lists every reason and releases nothing", "Create battery picks a released serial number", "opens the release's page", "The Battery IDs page opens on Releases", "\"Show demo IDs\" ticked", "voided IDs only with \"Show voided\" ticked", "a release whose IDs are all voided only with it ticked", "red notice naming it", "\"Change its battery ID…\" on a draft", "\"Not published yet\"" })
         {
             Assert.Contains(check, pack, StringComparison.Ordinal);
         }
@@ -61,6 +61,7 @@ public sealed class BatteryIdsHelpTests
         Assert.Contains("Battery IDs tab: release VPC-12221 to VPC-12230 with \"Release new IDs\"", script, StringComparison.Ordinal);
         Assert.Contains("the release's page opens", script, StringComparison.Ordinal);
         Assert.Contains("in its row's ⋯ menu, Void…", script, StringComparison.Ordinal);
+        Assert.Contains("it leaves the list until \"Show voided\" is ticked", script, StringComparison.Ordinal);
         Assert.Contains("\"batterySerialNumber\": \"DEMO-SPARE-0001\"", script, StringComparison.Ordinal);
         Assert.Contains("DEMO-SPARE-0001 to DEMO-SPARE-0004", Read("docs", "sample-cluster-test-accounts.md"), StringComparison.Ordinal);
     }
```

- [ ] **Step 2: See them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`
Expected: the build fails with CS1501 (`Index` and `Export` take no `voided`) and CS1061 (no `HiddenVoidedIds`, `HiddenReleases` or `ReleaseSummary.AllVoided`).

- [ ] **Step 3: Write the change**

Save as `../6d4-app.patch` and run `git apply ../6d4-app.patch`:

```diff
diff --git a/docs/end-user-testing-guide.md b/docs/end-user-testing-guide.md
index bb54782..85617b6 100644
--- a/docs/end-user-testing-guide.md
+++ b/docs/end-user-testing-guide.md
@@ -125,8 +125,8 @@ Each step says what to do, then **Expected:** what should happen.
    **Expected:** the picker offers Compact 7M's IDs first, then any model's, then the demo spares; the draft shows `UN778474481VPC12221` and its QR code; the ID is Used.
 13. On that draft, More: Change its battery ID…, VPC-12222.
    **Expected:** the draft now has VPC-12222 and its ID; VPC-12221 is Available again.
-14. All IDs: search VPC-12230; in its row's ⋯ menu, Void…, with a reason. Then tick "Show demo IDs".
-   **Expected:** it is Voided with the reason, and the create picker no longer offers it; the demo IDs show only once ticked.
+14. All IDs: search VPC-12230; in its row's ⋯ menu, Void…, with a reason. Then tick "Show voided", and then "Show demo IDs".
+   **Expected:** it leaves the list until "Show voided" is ticked, then shows as Voided with the reason; the create picker no longer offers it; the demo IDs show only once ticked.
 15. Signed out, open `/25S/UN778474481VPC12223`, then `/25S/UN778474481VPC12230`.
    **Expected:** "Not published yet", then "Not available".
 16. Clusters tab: Demo data, Clear demo data.
diff --git a/docs/qa-test-pack.md b/docs/qa-test-pack.md
index 605d7cd..14ef896 100644
--- a/docs/qa-test-pack.md
+++ b/docs/qa-test-pack.md
@@ -101,7 +101,7 @@ Scanning a released ID whose passport isn't published yet shows "Not published y
 - [ ] Every row of the scenario matrix holds, for every reader.
 - [ ] The two badges show on every passport page with the right status. The Battery badge never shows to the public.
 - [ ] Battery IDs: a range releases and opens the release's page with every ID Available; its "Export to Excel" opens with that release's IDs, one row per ID and its QR address.
-- [ ] The Battery IDs page opens on Releases, with the real IDs' counts at the top; All IDs shows demo IDs only with "Show demo IDs" ticked; Void… is in a row's ⋯ menu.
+- [ ] The Battery IDs page opens on Releases, with the real IDs' counts at the top; All IDs shows demo IDs only with "Show demo IDs" ticked, and voided IDs only with "Show voided" ticked (or the status Voided chosen); Releases shows a release whose IDs are all voided only with it ticked, and its own page lists every ID; Void… is in a row's ⋯ menu.
 - [ ] A release with a wrong count, a repeated or already released serial number lists every reason and releases nothing.
 - [ ] Create battery picks a released serial number (no typing); the draft shows its battery ID and QR code at once; the ID turns Used.
 - [ ] "Change its battery ID…" on a draft takes another available ID and gives the old one back; a voided ID is never offered again.
diff --git a/web/Controllers/BatteryIdsController.cs b/web/Controllers/BatteryIdsController.cs
index 2cb8534..e9b0f09 100644
--- a/web/Controllers/BatteryIdsController.cs
+++ b/web/Controllers/BatteryIdsController.cs
@@ -24,7 +24,8 @@ public static class BatteryIdsTabs
 
 /// <summary>
 /// What every tab of the "Battery IDs" page shows: the summary of the real IDs and the settings' state, then the chosen
-/// tab's content.
+/// tab's content. Unless "Show voided" is ticked (6d-4), <see cref="Releases"/> leaves out the releases whose IDs are all
+/// voided (<see cref="HiddenReleases"/>), and All IDs its voided IDs (<see cref="HiddenVoidedIds"/>).
 /// </summary>
 public sealed record BatteryIdsPage(
     string Tab,
@@ -36,7 +37,9 @@ public sealed record BatteryIdsPage(
     ReleaseResult? Refusal,
     IReadOnlyList<ReleasedBatteryId> Items,
     int Total,
-    ReleasedIdFilter Filter)
+    ReleasedIdFilter Filter,
+    int HiddenVoidedIds = 0,
+    int HiddenReleases = 0)
 {
     /// <summary>The most rows a list shows; the export has them all.</summary>
     public const int MaxRows = 200;
@@ -68,8 +71,9 @@ public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : C
         [FromQuery] string? model,
         [FromQuery] string? q,
         [FromQuery] bool demo,
+        [FromQuery] bool voided,
         CancellationToken cancellationToken) =>
-        View(await PageAsync(BatteryIdsTabs.Of(tab), ReleaseForm.Empty, null, new ReleasedIdFilter(status, release, model, q, demo), cancellationToken));
+        View(await PageAsync(BatteryIdsTabs.Of(tab), ReleaseForm.Empty, null, new ReleasedIdFilter(status, release, model, q, demo, voided), cancellationToken));
 
     [HttpGet("releases/{releaseId}")]
     public async Task<IActionResult> Release(string releaseId, [FromQuery] bool released, CancellationToken cancellationToken)
@@ -79,7 +83,7 @@ public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : C
             return NotFound();
         }
 
-        var items = await pool.ListAsync(new ReleasedIdFilter(Reference: releaseId), cancellationToken);
+        var items = await pool.ListAsync(new ReleasedIdFilter(Reference: releaseId, Voided: true), cancellationToken); // the full record
         return View(new ReleasePage(summary, items, await ModelsAsync(cancellationToken), released));
     }
 
@@ -134,7 +138,10 @@ public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : C
         return Redirect(Back(returnTo));
     }
 
-    /// <summary>The IDs as an Excel file: one release's (<paramref name="release"/>), or All IDs as filtered.</summary>
+    /// <summary>
+    /// The IDs as an Excel file: one release's (<paramref name="release"/>, with <paramref name="voided"/> for all of
+    /// them), or All IDs as shown.
+    /// </summary>
     [HttpGet("export")]
     public async Task<IActionResult> Export(
         [FromQuery] string? status,
@@ -142,9 +149,10 @@ public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : C
         [FromQuery] string? model,
         [FromQuery] string? q,
         [FromQuery] bool demo,
+        [FromQuery] bool voided,
         CancellationToken cancellationToken)
     {
-        var items = await pool.ListAsync(new ReleasedIdFilter(status, release, model, q, demo), cancellationToken);
+        var items = await pool.ListAsync(new ReleasedIdFilter(status, release, model, q, demo, voided), cancellationToken);
         var name = release is { Length: > 0 } && await pool.ReleaseAsync(release, cancellationToken) is { } summary ? FileWord(summary.Reference) : "all";
         return File(BatteryIdPool.Export(items, await ModelsAsync(cancellationToken)), XlsxWriter.ContentType, $"battery-ids-{name}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
     }
@@ -152,17 +160,22 @@ public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : C
     private async Task<BatteryIdsPage> PageAsync(string tab, ReleaseForm form, ReleaseResult? refusal, ReleasedIdFilter filter, CancellationToken cancellationToken)
     {
         var all = tab == BatteryIdsTabs.Ids ? await pool.ListAsync(filter, cancellationToken) : [];
+        var hiddenIds = tab == BatteryIdsTabs.Ids && !filter.ShowsVoided ? (await pool.ListAsync(filter with { Voided = true }, cancellationToken)).Count - all.Count : 0;
+        var releases = await pool.ReleasesAsync(cancellationToken);
+        var shown = filter.ShowsVoided ? releases : releases.Where(release => !release.AllVoided).ToArray();
         return new BatteryIdsPage(
             tab,
             await pool.RealCountsAsync(cancellationToken),
             pool.MissingSettings,
-            await pool.ReleasesAsync(cancellationToken),
+            shown,
             await ModelsAsync(cancellationToken),
             form,
             refusal,
             all.Take(BatteryIdsPage.MaxRows).ToArray(),
             all.Count,
-            filter);
+            filter,
+            hiddenIds,
+            releases.Count - shown.Count);
     }
 
     private async Task<IReadOnlyDictionary<string, string>> ModelsAsync(CancellationToken cancellationToken) =>
diff --git a/web/Dpp/Identifiers/BatteryIdPool.cs b/web/Dpp/Identifiers/BatteryIdPool.cs
index 12ee730..e594708 100644
--- a/web/Dpp/Identifiers/BatteryIdPool.cs
+++ b/web/Dpp/Identifiers/BatteryIdPool.cs
@@ -31,13 +31,21 @@ public sealed record ReleaseSummary(string ReleaseId, string Reference, string?
     public int Total => Counts.Values.Sum();
 
     public int Count(string status) => Counts.GetValueOrDefault(status);
+
+    /// <summary>Every ID of the release is voided; the Releases tab hides it unless "Show voided" is ticked (6d-4).</summary>
+    public bool AllVoided => Total > 0 && Count(ReleasedIdStatuses.Voided) == Total;
 }
 
 /// <summary>
 /// Which released IDs the list shows. Demo IDs only when <see cref="Demo"/> is asked (6d-2b), or when the list is one
-/// release's (<see cref="Reference"/>), which shows all of its own.
+/// release's (<see cref="Reference"/>), which shows all of its own. Voided IDs only when <see cref="Voided"/> is asked or
+/// the status asked for is Voided (6d-4); a release's own page and export ask for them, as the full record.
 /// </summary>
-public sealed record ReleasedIdFilter(string? Status = null, string? Reference = null, string? ModelId = null, string? Query = null, bool Demo = false);
+public sealed record ReleasedIdFilter(string? Status = null, string? Reference = null, string? ModelId = null, string? Query = null, bool Demo = false, bool Voided = false)
+{
+    /// <summary>Whether the list keeps voided IDs: "Show voided" ticked, or the status filter set to Voided.</summary>
+    public bool ShowsVoided => Voided || Status == ReleasedIdStatuses.Voided;
+}
 
 /// <summary>
 /// The battery IDs the team releases in advance (step 6d, the owner, 6 Nov 2026). A release is checked whole and is
@@ -228,6 +236,11 @@ public sealed partial class BatteryIdPool(
             where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, filter.Status);
         }
 
+        if (!filter.ShowsVoided)
+        {
+            where &= Builders<ReleasedBatteryId>.Filter.Ne(item => item.Status, ReleasedIdStatuses.Voided);
+        }
+
         if (!string.IsNullOrEmpty(filter.Reference))
         {
             where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.ReleaseId, filter.Reference);
diff --git a/web/Views/BatteryIds/Index.cshtml b/web/Views/BatteryIds/Index.cshtml
index 0edb76f..21ccfa8 100644
--- a/web/Views/BatteryIds/Index.cshtml
+++ b/web/Views/BatteryIds/Index.cshtml
@@ -3,14 +3,15 @@
 @using BatteryPassWeb.Dpp.Pages
 @model BatteryIdsPage
 @* The "Battery IDs" page (6d-2b, the owner's layout): the real IDs' summary and "Release new IDs" at the top, then three
-   tabs: Releases (first), All IDs, Release new IDs. A release has its own page. *@
+   tabs: Releases (first), All IDs, Release new IDs. A release has its own page. Voided IDs, and releases with only voided
+   IDs, show with "Show voided" ticked (6d-4). *@
 @{
     ViewData["Title"] = "Battery IDs";
     var filter = Model.Filter;
     var here = Context.Request.Path + Context.Request.QueryString;
     var problems = Model.Refusal?.Problems ?? [];
-    var filtered = !string.IsNullOrEmpty(filter.Status) || !string.IsNullOrEmpty(filter.Reference) || !string.IsNullOrEmpty(filter.ModelId) || !string.IsNullOrEmpty(filter.Query) || filter.Demo;
-    var exportQuery = string.Join("&", new[] { ("status", filter.Status), ("release", filter.Reference), ("model", filter.ModelId), ("q", filter.Query), ("demo", filter.Demo ? "true" : null) }
+    var filtered = !string.IsNullOrEmpty(filter.Status) || !string.IsNullOrEmpty(filter.Reference) || !string.IsNullOrEmpty(filter.ModelId) || !string.IsNullOrEmpty(filter.Query) || filter.Demo || filter.Voided;
+    var exportQuery = string.Join("&", new[] { ("status", filter.Status), ("release", filter.Reference), ("model", filter.ModelId), ("q", filter.Query), ("demo", filter.Demo ? "true" : null), ("voided", filter.Voided ? "true" : null) }
         .Where(pair => !string.IsNullOrEmpty(pair.Item2)).Select(pair => $"{pair.Item1}={Uri.EscapeDataString(pair.Item2!)}"));
     var blocked = Model.MissingSettings.Count > 0;
     string TabClass(string tab) => Model.Tab == tab ? "bp-subtab is-active" : "bp-subtab";
@@ -56,11 +57,23 @@
     @if (Model.Tab == BatteryIdsTabs.Releases)
     {
         <section class="bp-card bp-console-form-card bp-ids-tab" id="releases">
-            @if (Model.Releases.Count == 0)
+            @if (Model.Releases.Count == 0 && Model.HiddenReleases == 0)
             {
                 <p class="mb-0">No battery IDs released yet. <a href="/admin/battery-ids?tab=new">Release new IDs</a>.</p>
             }
             else
+            {
+                <form method="get" action="/admin/battery-ids" class="bp-releases-voided">
+                    <label class="bp-release-demo"><input type="checkbox" name="voided" value="true" checked="@filter.Voided" /><span>Show voided</span></label>
+                    <button type="submit" class="bp-secondary-button bp-button-compact">Show</button>
+                    @if (Model.HiddenReleases > 0)
+                    {
+                        <span class="bp-help-text">@Words.Count(Model.HiddenReleases, "release") with only voided IDs @Words.Is(Model.HiddenReleases) hidden.</span>
+                    }
+                </form>
+            }
+
+            @if (Model.Releases.Count > 0)
             {
                 <div class="bp-table-scroll">
                     <table class="table align-middle mb-0 bp-console-table bp-releases-table">
@@ -84,7 +97,7 @@
                                     </td>
                                     <td data-label="Actions" class="bp-release-row-actions">
                                         <a href="@open">Open</a>
-                                        <a href="/admin/battery-ids/export?release=@Uri.EscapeDataString(release.ReleaseId)"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Export to Excel</a>
+                                        <a href="/admin/battery-ids/export?release=@Uri.EscapeDataString(release.ReleaseId)&voided=true"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Export to Excel</a>
                                         @if (release.Count(ReleasedIdStatuses.Available) > 0)
                                         {
                                             <button type="button" class="bp-text-button" data-void-unused data-release-id="@release.ReleaseId" data-release-reference="@release.Reference" data-release-available="@release.Count(ReleasedIdStatuses.Available)">Void unused…</button>
@@ -133,6 +146,7 @@
                     </select>
                 </label>
                 <label class="bp-release-demo"><input type="checkbox" name="demo" value="true" checked="@filter.Demo" /><span>Show demo IDs</span></label>
+                <label class="bp-release-demo"><input type="checkbox" name="voided" value="true" checked="@filter.Voided" /><span>Show voided</span></label>
                 <div class="bp-release-filter-actions">
                     <button type="submit" class="bp-secondary-button bp-button-compact">Show</button>
                     @if (filtered)
@@ -142,12 +156,12 @@
                 </div>
             </form>
             <div class="bp-ids-list-head">
-                <p class="bp-help-text mb-0">@Words.Count(Model.Total, "ID")@(filtered ? " match" : null)@(Model.Items.Count < Model.Total ? $"; the first {Model.Items.Count} are shown" : null).</p>
+                <p class="bp-help-text mb-0">@Words.Count(Model.Total, "ID")@(filtered ? " match" : null)@(Model.Items.Count < Model.Total ? $"; the first {Model.Items.Count} are shown" : null)@(Model.HiddenVoidedIds > 0 ? $"; {Words.Count(Model.HiddenVoidedIds, "voided ID")} {Words.Is(Model.HiddenVoidedIds)} hidden" : null).</p>
                 <a class="bp-secondary-button bp-button-compact text-nowrap" href="/admin/battery-ids/export@(exportQuery.Length > 0 ? "?" + exportQuery : null)"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Export to Excel</a>
             </div>
             @if (Model.Items.Count == 0)
             {
-                <p class="mb-0 mt-3">@(filtered ? "No released ID matches." : "No battery IDs released yet.")</p>
+                <p class="mb-0 mt-3">@(Model.HiddenVoidedIds > 0 ? "Only voided IDs match. Tick \"Show voided\" to see them." : filtered ? "No released ID matches." : "No battery IDs released yet.")</p>
             }
             else
             {
diff --git a/web/Views/BatteryIds/Release.cshtml b/web/Views/BatteryIds/Release.cshtml
index 0d35b08..902c7ac 100644
--- a/web/Views/BatteryIds/Release.cshtml
+++ b/web/Views/BatteryIds/Release.cshtml
@@ -2,12 +2,13 @@
 @using BatteryPassWeb.Dpp.Identifiers
 @using BatteryPassWeb.Dpp.Pages
 @model ReleasePage
-@* One release's page (6d-2b): where a release lands, with its export at the top for the labels. *@
+@* One release's page (6d-2b): where a release lands, with its export at the top for the labels. It is the full record:
+   every ID with its status, voided ones too (6d-4). *@
 @{
     var release = Model.Release;
     ViewData["Title"] = release.Reference;
     var here = Context.Request.Path.ToString();
-    var export = $"/admin/battery-ids/export?release={Uri.EscapeDataString(release.ReleaseId)}";
+    var export = $"/admin/battery-ids/export?release={Uri.EscapeDataString(release.ReleaseId)}&voided=true";
 }
 
 <main class="bp-page bp-admin-page">
diff --git a/web/Views/Help/Guide.cshtml b/web/Views/Help/Guide.cshtml
index 2d36406..c77af2d 100644
--- a/web/Views/Help/Guide.cshtml
+++ b/web/Views/Help/Guide.cshtml
@@ -172,11 +172,11 @@
                         <dt>Release new IDs</dt>
                         <dd>Give a reference, such as the purchase order, and optionally the model. Add one or more ranges (start, end and quantity) or paste serial numbers, one per line. It is all or nothing: if one can't be released, none is, and each reason is listed. Once released, the app opens the release's page.</dd>
                         <dt>Releases</dt>
-                        <dd>The first tab: one row per release, with its reference, model, date and counts. "Open" shows the release's page: its IDs, "Export to Excel" at the top and "Void unused…". The demo data's release is tagged Demo.</dd>
+                        <dd>The first tab: one row per release, with its reference, model, date and counts. "Open" shows the release's page: its IDs, "Export to Excel" at the top and "Void unused…". The demo data's release is tagged Demo. A release whose IDs are all voided shows only with "Show voided" ticked; its page still lists every ID.</dd>
                         <dt>All IDs</dt>
-                        <dd>Every released ID, searched by serial number and filtered by status, release and model. Demo IDs show only with "Show demo IDs" ticked.</dd>
+                        <dd>Every released ID, searched by serial number and filtered by status, release and model. Demo IDs show only with "Show demo IDs" ticked, and voided IDs only with "Show voided" ticked or the status Voided chosen.</dd>
                         <dt>Export to Excel</dt>
-                        <dd>One row per ID, with the QR code's full address for the label printer or a mail merge. On a release's page or row, that release's IDs; on All IDs, the filtered ones.</dd>
+                        <dd>One row per ID, with the QR code's full address for the label printer or a mail merge. On a release's page or row, all of that release's IDs; on All IDs, the ones shown.</dd>
                         <dt>Statuses</dt>
                         <dd>Available, Used (a draft has it), Published (fixed for life) and Voided (never used again).</dd>
                         <dt>Void…</dt>
diff --git a/web/wwwroot/css/bp-battery-ids.css b/web/wwwroot/css/bp-battery-ids.css
index 68c9fd7..22e9135 100644
--- a/web/wwwroot/css/bp-battery-ids.css
+++ b/web/wwwroot/css/bp-battery-ids.css
@@ -437,9 +437,19 @@
 }
 
 .bp-release-filter {
-  grid-template-columns: minmax(0, 1fr) repeat(3, minmax(0, 9rem)) auto auto;
+  grid-template-columns: minmax(0, 1fr) repeat(3, minmax(0, 9rem)) auto auto auto;
 }
 
+.bp-releases-voided {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 8px 12px;
+  margin-bottom: 12px;
+}
+
+.bp-releases-voided .bp-release-demo { padding-bottom: 0; }
+
 .bp-ids-list-head {
   display: flex;
   align-items: center;
```

- [ ] **Step 4: See the tests pass**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~BatteryIdsVoidedTests|FullyQualifiedName~BatteryIdSettingsAndPagesTests|FullyQualifiedName~BatteryIdsHelpTests"`
Expected: `Passed!  - Failed:     0, Passed:    16`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteryIdsVoidedTests.cs BatteryPassWeb.Tests/Dpp/BatteryIdSettingsAndPagesTests.cs BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs docs/end-user-testing-guide.md docs/qa-test-pack.md web/Controllers/BatteryIdsController.cs web/Dpp/Identifiers/BatteryIdPool.cs web/Views/BatteryIds/Index.cshtml web/Views/BatteryIds/Release.cshtml web/Views/Help/Guide.cshtml web/wwwroot/css/bp-battery-ids.css
git commit -m "feat(dpp): voided battery IDs hidden by default, with a Show voided box on Releases and All IDs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2620, Skipped:     0, Total:  2620`. That is the 2615 from before plus 5 new test cases.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat a314d41 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.
