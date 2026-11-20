# DPP V3 Step 6c: The Role-Matrix Check's Fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the six faults that step 6c's role-matrix check found, and a seventh found while checking the fixes (5 Nov 2026, at dc488fa; the managing session's notes, `step6c-findings.md`). The check found 887 PASS and 78 FAIL across every reader of design 5.2, every page, window and API call, at 1280, 375 and 150 %. The reader matrix itself holds; the 78 FAILs are these six faults:

1. **Another cluster's admin opening a battery page gets the browser's bare 404.** They should get the app's "Not allowed" page, as a cluster user does. Unknown battery IDs get it too, so whether a battery exists stays unsaid.
2. **The team's "Change" (Cluster, in the battery page header) has its bottom 3 px covered by the Cluster value.** Plan 6a-1 gave it a 24 px box overhanging its line, and the value painted after it sits on top. It is now drawn above.
3. **The audit log names an API token as `api-token:<id>`, and a battery as `pack pack-<id>`.** Now a token reads "Battery system" or "API token", as a battery's Earlier uploads already do. An earlier passport made inactive reads "… set the passport of battery DEMO-C13M-0001 inactive".
4. **On a phone, the lists' "What do these mean?" sat in the hidden heading row**: invisible, still focusable. On phones only it now shows beside "N batteries." on the Registry and the Batteries list, and the hidden copy takes no focus. Desktop is unchanged. The product owner chose "Beside the count" (5 Nov 2026).
5. **On a phone, the team's battery page scrolled sideways for a long Passport badge** ("Draft · only your team", "Marked for deletion · version 2"). The badge now wraps.
6. **A battery placed on the market again, whose newest passport is then marked invalid or for deletion, couldn't be found by its serial number** ("Not available", 404). The serial-number lookup now skips the inactive earlier passports, as decision 34 says.
   - The API's and the uploads' lookup by serial number (`BatteryApi.FindAsync`) already skipped them. A test now keeps the two in step.
7. **Found while checking fault 4: on a phone, the explainer window's words were squeezed** beside the widest badge, about a word per line. On a phone the list now stacks, each badge above its meaning.

Also, as a docs line: design 7.9 now says what step 4b built. A repair that changes a passport value publishes a new version; one that changes none keeps the version, and only the repair history grows.

**Architecture:**
- **`ClusterBatteriesController.Battery`** returns `Forbid()` instead of `NotFound()`. The cookie scheme sends the reader to `/not-allowed`.
- **`PassportPages.FindAsync`:** when no passport with the serial number is active, the fallback takes the latest version of each battery with that serial number, *except inactive ones*.
- **`AuditLines.Describe`:** an `api-token:` actor is named "Battery system" (sent as `BatterySystemFeed`) or "API token". `PublicationService.InactiveEvent` gets its own sentence, with the battery's serial number from the log's record names and the reason as the change.
- **`bp-product-data.css`:** `.bp-bh-facts dt .bp-text-button` gets `position: relative; z-index: 1`.
- **`bp-status.css`:**
  - the count row (`.bp-batteries-count-row`);
  - its explainer, shown at `max-width: 767px` only, the breakpoint where `site.css` hides the table heading;
  - the heading's copy `display: none` there;
  - every badge wraps there (it does only when it must);
  - in its own block at the end of the file, after the list's own rule: the statuses list stacks on a phone.
- **`_BatteryList.cshtml`:** the count and `_StatusHelp` in one row. A popover can't sit inside the count's `<p>`.
- **`site.css` is not changed.**
- **`docs/superpowers/specs/…-design.md`**, 7.9: the repair's version rule, as built.
- **Tests:**
  - new: `BatteryPassWeb.Tests/Dpp/RoleMatrixFixesTests.cs` (7 tests, 8 cases). Faults 3 and 6 are checked on the real demo data: its battery system's token uploads, and DEMO-C13M-0001's three passports, marked invalid and marked for deletion. A further test checks that the API's and uploads' lookup finds the same battery.
  - changed: `ClusterBatteriesControllerTests.AnotherClustersAdmin_ShouldFindNothing` becomes `…ShouldBeSentToNotAllowed`, with `ForbidResult`, also for an unknown ID.
  - changed: `TapTargetTests.TheHeadersChangeButton_…` follows the rule's new first lines.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 5.2, 7.5–7.8 and 14 (step 6). Decisions 33, 34 and 37. The findings: the managing session's notes, `step6c-findings.md`, with the check's script and logs in `step6c/`.

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at dc488fa (plan 6b-1's last commit, "feat(dpp): the Help for every reader…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas. The tests use a throwaway MongoDB (EphemeralMongo).
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own tests. The whole suite runs once**, in the final checks. Never run two whole suites at once.
- **Baseline:** the whole suite shows `Passed:  2534` (plan 6b-1's result). After this plan: 2542.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6c-test.patch`). Then run `git apply ../<name>` from the worktree root. New files are given inside the patches.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**

## Found while prototyping

**Setup:**
- a scratch clone of dc488fa with my own app and a throwaway mongod, after "Reset demo data";
- the fixes checked again with the check's own script (`step6c/walk6c.mjs`), phases 1 (pages) and 4 (data changes), in Edge.

**The recheck:**

- **Pages (phase 1), every reader at 1280 and 375:** 714 PASS, 0 FAIL. Before the fixes: 638 PASS, 76 FAIL.
  - Another cluster's admin gets 302 to `/not-allowed`.
  - The team's "Change" is on top over its whole box.
  - The audit log has no raw IDs.
  - At 375 the Registry and the Batteries list show "What do these mean?" beside the count and hide the heading's copy.
  - No battery page scrolls sideways, including the Versions section of a marked-for-deletion battery, which a header-only first try still missed.
- **Data changes (phase 4):** 17 PASS, 0 FAIL. Market surveillance marks DEMO-C13M-0001 invalid, and a visitor searching its serial number sees it as invalid, without the reason.
- **Fault 7** showed on the 375 shot of the explainer opened from the count: the words beside the widest badge, about one per line. With the phone rule, each badge sits above its meaning. That rule has to come after the list's own `.bp-status-meanings` rule in the file, which overrides it otherwise.
- **Shots** (the managing session's notes, `step6c/`): `fault4-registry-375.png`, `fault4-explainer-open-375.png` and `fault4-registry-1280-unchanged.png`.

**Measured on a clean clone of dc488fa:**
- the tests fail first: 9 (every new case except the API's lookup, which was already right, and the 2 changed tests);
- then they pass with the fixes: 445 tests across the 60 classes that read or exercise the changed files;
- the clean clone shows them as 443 plus the two known `ApplicationAuditCoverageTests` failures (a clean checkout gives `site.css` CRLF line endings). They pass in the worktree.

### Task 1: The six fixes

**Files:**
- Create: `BatteryPassWeb.Tests/Dpp/RoleMatrixFixesTests.cs`
- Modify: `BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs`, `BatteryPassWeb.Tests/Look/TapTargetTests.cs`
- Modify: `web/Controllers/ClusterBatteriesController.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Dpp/Audit/AuditLines.cs`
- Modify: `web/wwwroot/css/bp-product-data.css`, `web/wwwroot/css/bp-status.css`, `web/Views/Shared/_BatteryList.cshtml`
- Modify: `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (7.9, one line)

- [ ] **Step 1: Write the failing tests**

Save as `../6c-test.patch` and run `git apply ../6c-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs b/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs
index 801b437..f5bb13a 100644
--- a/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs
@@ -61,11 +61,14 @@ public sealed class ClusterBatteriesControllerTests(MongoFixture mongo)
     }
 
     [Fact]
-    public async Task AnotherClustersAdmin_ShouldFindNothing()
+    public async Task AnotherClustersAdmin_ShouldBeSentToNotAllowed()
     {
+        // Step 6c: the app's "Not allowed" page, as a cluster user gets, never the browser's bare 404; and whether the
+        // battery exists stays unsaid.
         var rig = await RigAsync();
 
-        Assert.IsType<NotFoundResult>(await Controller(rig, SouthAdmin, "clusterAdmin").Battery(rig.PackId, CancellationToken.None));
+        Assert.IsType<ForbidResult>(await Controller(rig, SouthAdmin, "clusterAdmin").Battery(rig.PackId, CancellationToken.None));
+        Assert.IsType<ForbidResult>(await Controller(rig, SouthAdmin, "clusterAdmin").Battery("pack-that-does-not-exist", CancellationToken.None));
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/Dpp/RoleMatrixFixesTests.cs b/BatteryPassWeb.Tests/Dpp/RoleMatrixFixesTests.cs
new file mode 100644
index 0000000..1006254
--- /dev/null
+++ b/BatteryPassWeb.Tests/Dpp/RoleMatrixFixesTests.cs
@@ -0,0 +1,108 @@
+using System.Text.RegularExpressions;
+using BatteryPassWeb.Dpp.Audit;
+using BatteryPassWeb.Dpp.Pages;
+using BatteryPassWeb.Dpp.Store;
+using BatteryPassWeb.Services;
+using BatteryPassWeb.Tests.Demo;
+using Microsoft.Extensions.DependencyInjection;
+
+namespace BatteryPassWeb.Tests.Dpp;
+
+/// <summary>
+/// Step 6c, the role-matrix check (5 Nov 2026): the faults it found, on the demo data. A battery placed on the market
+/// again stays findable by its serial number when its newest passport is marked invalid (6); the audit log names a token
+/// and a battery in words (3); and the page fixes 2, 4 and 5 in the stylesheets and the lists. Fault 1 (another cluster's
+/// admin is sent to "Not allowed") is in ClusterBatteriesControllerTests.
+/// </summary>
+[Collection(MongoCollection.Name)]
+public sealed partial class RoleMatrixFixesTests(MongoFixture mongo)
+{
+    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n");
+
+    [Theory]
+    [InlineData(DppStatuses.Invalid)]
+    [InlineData(DppStatuses.MarkedForDeletion)]
+    public async Task ABatteryWithANewPassport_ShouldStayFindableBySerialNumber_WhenItsNewestIsMarked(string mark)
+    {
+        using var rig = await DemoRig.StartAsync(mongo);
+        await rig.Demo.SeedAsync(DemoRig.Who);
+        var pages = rig.Services.GetRequiredService<PassportPages>();
+
+        // DEMO-C13M-0001: three passports, the two older inactive. Mark the newest, as market surveillance or the team would.
+        var newest = await pages.FindAsync("DEMO-C13M-0001");
+        Assert.NotNull(newest);
+        Assert.Equal(DppStatuses.Active, newest.DppStatus);
+        await rig.Store.SetVersionStatusAsync(newest.Id, mark, DateTime.UtcNow);
+
+        var found = await pages.FindAsync("DEMO-C13M-0001");
+
+        Assert.NotNull(found);
+        Assert.Equal(newest.Id, found.Id);
+        Assert.Equal(mark, found.DppStatus);
+    }
+
+    [Fact]
+    public async Task TheApiAndUploads_ShouldFindTheSameBatteryBySerialNumber_WhenItsNewestIsMarked()
+    {
+        // The API's and the uploads' lookup (BatteryApi.FindAsync) already skips inactive passports (decision 34); this
+        // keeps the pages and it in step.
+        using var rig = await DemoRig.StartAsync(mongo);
+        await rig.Demo.SeedAsync(DemoRig.Who);
+        var newest = (await rig.Services.GetRequiredService<PassportPages>().FindAsync("DEMO-C13M-0001"))!;
+        await rig.Store.SetVersionStatusAsync(newest.Id, DppStatuses.Invalid, DateTime.UtcNow);
+
+        var target = await rig.Services.GetRequiredService<BatteryPassWeb.Dpp.Api.BatteryApi>().FindAsync(BatteryPassWeb.Dpp.Editing.ChangeSender.Team(DemoRig.Who), "DEMO-C13M-0001");
+
+        Assert.Equal(newest.PackId, target.Pack?.Id);
+    }
+
+    [Fact]
+    public async Task TheAuditLog_ShouldNameATokenAndABatteryInWords()
+    {
+        using var rig = await DemoRig.StartAsync(mongo);
+        await rig.Demo.SeedAsync(DemoRig.Who);
+        var events = await rig.Services.GetRequiredService<ApplicationAuditService>().ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
+        var lines = await rig.Services.GetRequiredService<AuditLines>().DescribeAsync(events);
+
+        // The demo data has a battery system's uploads (sent with a token) and DEMO-C13M-0001's earlier passports made inactive.
+        Assert.Contains(lines, line => line.EventType == BatteryPassWeb.Dpp.Uploads.UploadService.UploadedEvent && line.Text.StartsWith("Battery system ", StringComparison.Ordinal));
+        var inactive = lines.Where(line => line.EventType == BatteryPassWeb.Dpp.Publication.PublicationService.InactiveEvent).ToArray();
+        Assert.NotEmpty(inactive);
+        Assert.All(inactive, line => Assert.Contains(" set the passport of battery DEMO-C13M-0001 inactive", line.Text, StringComparison.Ordinal));
+        Assert.All(lines, line => Assert.DoesNotMatch(RawId(), line.Text));
+    }
+
+    [Fact]
+    public void TheHeadersChangeButton_ShouldBeDrawnAboveTheClusterValue() =>
+        Assert.Contains(".bp-bh-facts dt .bp-text-button {\n  position: relative;\n  z-index: 1;\n  margin-block: -4px;", Read("web", "wwwroot", "css", "bp-product-data.css"), StringComparison.Ordinal);
+
+    [Fact]
+    public void OnAPhone_TheListsExplainer_ShouldSitBesideTheCount()
+    {
+        var list = Read("web", "Views", "Shared", "_BatteryList.cshtml");
+        var css = Read("web", "wwwroot", "css", "bp-status.css");
+
+        Assert.Contains("<div class=\"bp-batteries-count-row\">", list, StringComparison.Ordinal);
+        Assert.True(list.IndexOf("@await Html.PartialAsync(\"_StatusHelp\")", StringComparison.Ordinal) < list.IndexOf("<table", StringComparison.Ordinal));
+        Assert.Contains("<th>Status @await Html.PartialAsync(\"_StatusHelp\")</th>", list, StringComparison.Ordinal);
+        Assert.Contains(".bp-batteries-count-row > .bp-status-help-button {\n  display: none;\n}", css, StringComparison.Ordinal);
+        Assert.Contains("@media (max-width: 767px) {\n  .bp-batteries-count-row > .bp-status-help-button {\n    display: inline-flex;\n  }\n\n  .bp-mobile-card-table thead .bp-status-help-button {\n    display: none;\n  }", css, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void OnAPhone_TheBadges_ShouldWrap() =>
+        Assert.Contains("  .bp-mobile-card-table thead .bp-status-help-button {\n    display: none;\n  }\n\n  /* A long badge (\"Passport Marked for deletion · version 2\") wraps instead of widening the page (step 6c). */\n  .bp-badge {\n    max-width: 100%;\n    flex-wrap: wrap;\n    white-space: normal;\n  }\n}", Read("web", "wwwroot", "css", "bp-status.css"), StringComparison.Ordinal);
+
+    [Fact]
+    public void OnAPhone_TheStatusesInWords_ShouldStack()
+    {
+        // Found while checking fault 4: at 375 the explainer's words were squeezed beside the widest badge.
+        var css = Read("web", "wwwroot", "css", "bp-status.css");
+        var stack = css.LastIndexOf("@media (max-width: 767px) {\n  .bp-status-meanings {\n    grid-template-columns: minmax(0, 1fr);", StringComparison.Ordinal);
+
+        Assert.True(stack > css.IndexOf(".bp-status-meanings {\n  display: grid;", StringComparison.Ordinal), "the phone rule comes after the list's own rule");
+    }
+
+    [GeneratedRegex(@"api-token:|\bpack-[0-9a-f]{6,}")]
+    private static partial Regex RawId();
+}
diff --git a/BatteryPassWeb.Tests/Look/TapTargetTests.cs b/BatteryPassWeb.Tests/Look/TapTargetTests.cs
index 25e33e7..9f3dac9 100644
--- a/BatteryPassWeb.Tests/Look/TapTargetTests.cs
+++ b/BatteryPassWeb.Tests/Look/TapTargetTests.cs
@@ -23,5 +23,5 @@ public sealed class TapTargetTests
 
     [Fact]
     public void TheHeadersChangeButton_ShouldNotPushTheClusterValueDown() =>
-        Assert.Contains(".bp-bh-facts dt .bp-text-button {\n  margin-block: -4px;", Css(), StringComparison.Ordinal);
+        Assert.Contains(".bp-bh-facts dt .bp-text-button {\n  position: relative;\n  z-index: 1;\n  margin-block: -4px;", Css(), StringComparison.Ordinal); // drawn above the value it overhangs (step 6c)
 }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental` then `dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~RoleMatrixFixesTests|FullyQualifiedName~ClusterBatteriesControllerTests|FullyQualifiedName~TapTargetTests"`
Expected: `Failed!  - Failed:     9, Passed:    18`. That is every new case except `TheApiAndUploads_ShouldFindTheSameBatteryBySerialNumber_WhenItsNewestIsMarked` (that lookup was already right), plus `AnotherClustersAdmin_ShouldBeSentToNotAllowed` and `TheHeadersChangeButton_ShouldNotPushTheClusterValueDown`.

- [ ] **Step 3: The fixes**

Save as `../6c-code.patch` and run `git apply ../6c-code.patch`:

```diff
diff --git a/docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md b/docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md
index 2bd2a79..42e99ae 100644
--- a/docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md
+++ b/docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md
@@ -260,7 +260,7 @@ The file's DIN note on #22 asks for a repair history and for a repair to be reco
 - The team or the battery's cluster admins record a repair through the status change (7.4), with "repair" as the reason. An upload can do the same (8.2).
 - The repair adds a "repaired" entry to the pack's repair history: date, what was repaired, who recorded it. The history is shown with the battery status, to the same readers (5.2). The schema has no field for it, so it isn't in the passport JSON.
 - A battery with status "original" becomes "re-used". A battery that is already "re-used", "remanufactured" or "repurposed" keeps its status (J-15).
-- Like any status change, a repair asks for the re-stated values (pre-filled) and publishes a new version of the same passport.
+- Like any status change, a repair asks for the re-stated values (pre-filled). A repair that changes a passport value (the status, or a re-stated value) publishes a new version of the same passport; one that changes none keeps the version, and only the repair history grows (as built in step 4b; wording corrected in step 6c).
 
 ## 8. Live data and uploads
 
diff --git a/web/Controllers/ClusterBatteriesController.cs b/web/Controllers/ClusterBatteriesController.cs
index 5cbe2c9..a556935 100644
--- a/web/Controllers/ClusterBatteriesController.cs
+++ b/web/Controllers/ClusterBatteriesController.cs
@@ -35,7 +35,9 @@ public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppRea
         }
 
         var page = sender is null ? null : await WithUploadAsync(await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken), cancellationToken);
-        return page is null ? NotFound() : View(PageView, page);
+        // A battery this admin may not manage, or none: the app's "Not allowed" page, as a cluster user gets, without
+        // saying whether it exists (step 6c; the site has no page for a bare 404).
+        return page is null ? Forbid() : View(PageView, page);
     }
 
     [HttpPost("{packId}/live")]
diff --git a/web/Dpp/Audit/AuditLines.cs b/web/Dpp/Audit/AuditLines.cs
index 5442e44..f744ae1 100644
--- a/web/Dpp/Audit/AuditLines.cs
+++ b/web/Dpp/Audit/AuditLines.cs
@@ -107,8 +107,11 @@ public sealed class AuditLines(DppStore store, DocumentStore documents, Attribut
     {
         var eventType = BsonHelpers.GetString(auditEvent, "eventType");
         var email = BsonHelpers.GetString(auditEvent, "actor");
-        var actor = people.GetValueOrDefault(email) ?? (email.Length > 0 ? email : "The app");
         var role = FirstNonEmpty(BsonHelpers.GetString(auditEvent, "actorRole"), BsonHelpers.GetString(auditEvent, "actorType"));
+        // A token is named by what it is, never by its ID, as in a battery's earlier uploads (step 6c).
+        var actor = email.StartsWith("api-token:", StringComparison.Ordinal)
+            ? (role == nameof(SenderKind.BatterySystemFeed) ? "Battery system" : "API token")
+            : people.GetValueOrDefault(email) ?? (email.Length > 0 ? email : "The app");
         var source = BsonHelpers.GetString(auditEvent, "source");
         var entityType = BsonHelpers.GetString(auditEvent, "entityType");
         var entityId = BsonHelpers.GetString(auditEvent, "entityId");
@@ -222,6 +225,14 @@ public sealed class AuditLines(DppStore store, DocumentStore documents, Attribut
                 words.Add(new(counts[0].Replace("Upload of ", string.Empty, StringComparison.Ordinal), Strong: true));
                 change = counts.Length == 2 ? counts[1] : null;
                 break;
+            case PublicationService.InactiveEvent:
+                // An earlier passport replaced by the battery's new one (step 4c), named by the battery (step 6c).
+                icon = AuditIcon.Publish;
+                words.Add(new(" set the passport of battery "));
+                words.Add(new(record?.Name ?? entityId, Strong: true));
+                words.Add(new(" inactive"));
+                change = metadata.TryGetValue("why", out var why) && why.IsString ? why.AsString : null;
+                break;
             case PublicationService.PublishedEvent:
                 icon = AuditIcon.Publish;
                 words.Add(new(" published battery "));
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index 6f241a7..b0487c8 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -217,9 +217,11 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
         if (published.Count == 0)
         {
             // Marking a passport invalid leaves it no active version; it stays findable and says so (owner's point 10b).
+            // A battery placed on the market again has older passports with the same serial number, made inactive; the
+            // one that isn't inactive is the battery's passport (decision 34; step 6c).
             foreach (var candidate in candidates)
             {
-                if (await store.GetLatestVersionAsync(candidate.Id, cancellationToken) is { } latest)
+                if (await store.GetLatestVersionAsync(candidate.Id, cancellationToken) is { DppStatus: not DppStatuses.Inactive } latest)
                 {
                     published.Add(latest);
                 }
diff --git a/web/Views/Shared/_BatteryList.cshtml b/web/Views/Shared/_BatteryList.cshtml
index e61ffd4..74c6d01 100644
--- a/web/Views/Shared/_BatteryList.cshtml
+++ b/web/Views/Shared/_BatteryList.cshtml
@@ -25,10 +25,14 @@
     }
     else
     {
-        <p class="bp-batteries-count">@(Model.Searched ? $"{Words.Count(Model.Rows.Count, "battery", "batteries")} match “{Model.Query}”." : $"{Words.Count(Model.Rows.Count, "battery", "batteries")}.")@if (Model.Searched)
-            {
-                <text> <a href="@Model.Action">Show all</a></text>
-            }</p>
+        <div class="bp-batteries-count-row">
+            <p class="bp-batteries-count">@(Model.Searched ? $"{Words.Count(Model.Rows.Count, "battery", "batteries")} match “{Model.Query}”." : $"{Words.Count(Model.Rows.Count, "battery", "batteries")}.")@if (Model.Searched)
+                {
+                    <text> <a href="@Model.Action">Show all</a></text>
+                }</p>
+            @* On a phone the Status heading is hidden, so its explainer shows here (step 6c, the owner's choice). *@
+            @await Html.PartialAsync("_StatusHelp")
+        </div>
         <div class="bp-table-scroll">
             <table class="table align-middle bp-console-table bp-batteries-table bp-mobile-card-table bp-one-line-list">
                 <thead>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 399b084..d10a4f0 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3077,6 +3077,8 @@ main .bp-field-marker.is-published {
 
 /* Its 24 px box overhangs its label's line instead of growing it, so the Cluster value stays level with the others. */
 .bp-bh-facts dt .bp-text-button {
+  position: relative;
+  z-index: 1;
   margin-block: -4px;
   margin-left: 6px;
   font-size: 0.78rem;
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index f8dd5fe..ee2ec5b 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -163,6 +163,36 @@ th .bp-status-help-words {
   font-size: 1.2rem;
 }
 
+/* The lists' explainer: in the Status heading on a desktop; on a phone, where the heading row is hidden, beside the
+   count instead, and the hidden heading's copy takes no focus (step 6c, the owner's choice). */
+.bp-batteries-count-row {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 4px 10px;
+}
+
+.bp-batteries-count-row > .bp-status-help-button {
+  display: none;
+}
+
+@media (max-width: 767px) {
+  .bp-batteries-count-row > .bp-status-help-button {
+    display: inline-flex;
+  }
+
+  .bp-mobile-card-table thead .bp-status-help-button {
+    display: none;
+  }
+
+  /* A long badge ("Passport Marked for deletion · version 2") wraps instead of widening the page (step 6c). */
+  .bp-badge {
+    max-width: 100%;
+    flex-wrap: wrap;
+    white-space: normal;
+  }
+}
+
 /* The statuses in words: in the badges' explainer window and in the Help's "Statuses" part (step 6b-1). */
 .bp-status-meanings {
   display: grid;
@@ -510,3 +540,16 @@ th .bp-status-help-words {
 /* The passport hero's buttons carry the app's arrow icons, not typed arrows (redesign R6). */
 .bp-report-hero-cta { gap: 8px; }
 .bp-report-hero-cta .bp-i { width: 18px; height: 18px; flex: none; }
+
+/* On a phone the statuses in words stack, each badge above its meaning, so the words get the window's width (step 6c).
+   After the list's own rule, which this overrides. */
+@media (max-width: 767px) {
+  .bp-status-meanings {
+    grid-template-columns: minmax(0, 1fr);
+    gap: 4px 0;
+  }
+
+  .bp-status-meanings dd {
+    margin-bottom: 8px;
+  }
+}
```

- [ ] **Step 4: Run the tests and the classes that read or exercise these files**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~AdminDenseConsoleLayoutTests|FullyQualifiedName~AdminHeadingTests|FullyQualifiedName~ApplicationAuditCoverageTests|FullyQualifiedName~AuditLinesTests|FullyQualifiedName~AuditLogPageTests|FullyQualifiedName~BatteriesControllerTests|FullyQualifiedName~BatteryApiTests|FullyQualifiedName~BatteryAuditLinesTests|FullyQualifiedName~BatteryHistoryLinkTests|FullyQualifiedName~BatteryPageLayoutTests|FullyQualifiedName~BatteryPanelTablesTests|FullyQualifiedName~ChainHistoryLinksTests|FullyQualifiedName~ClusterBatteriesControllerTests|FullyQualifiedName~ClusterHistoryTests|FullyQualifiedName~DppApiControllerTests|FullyQualifiedName~DppApiUploadTests|FullyQualifiedName~DppApiWritingTests|FullyQualifiedName~HandOverPagesTests|FullyQualifiedName~HelpPerRoleTests|FullyQualifiedName~HeroButtonIconsTests|FullyQualifiedName~HeroChainChipsTests|FullyQualifiedName~HistoryReadsWellTests|FullyQualifiedName~HomeSampleLinkTests|FullyQualifiedName~KeyFiguresTests|FullyQualifiedName~LiveChartsTests|FullyQualifiedName~LiveTabRuleTests|FullyQualifiedName~MarkWindowTests|FullyQualifiedName~MarkedBatteryClusterTests|FullyQualifiedName~ModelPictureTests|FullyQualifiedName~ModuleDataTests|FullyQualifiedName~OneLineListTests|FullyQualifiedName~PassportApiTests|FullyQualifiedName~PassportChainTests|FullyQualifiedName~PassportControllerTests|FullyQualifiedName~PassportHeroTests|FullyQualifiedName~PassportLinksSameTabTests|FullyQualifiedName~PassportMarkPagesTests|FullyQualifiedName~PassportPagesTests|FullyQualifiedName~PrintStyleTests|FullyQualifiedName~QrWorkflowTests|FullyQualifiedName~RedesignFixesTests|FullyQualifiedName~RegistryBadgesTests|FullyQualifiedName~RegistryListTests|FullyQualifiedName~RegistrySearchWordsTests|FullyQualifiedName~ReviewFixesTests|FullyQualifiedName~RoleMatrixFixesTests|FullyQualifiedName~RowActionsTests|FullyQualifiedName~SharedBatteryListTests|FullyQualifiedName~StatusBadgePagesTests|FullyQualifiedName~StatusBadgeTests|FullyQualifiedName~StatusHelpLinkTests|FullyQualifiedName~StatusPagesTests|FullyQualifiedName~StatusWindowScrollTests|FullyQualifiedName~Step5OwnerTestFixesTests|FullyQualifiedName~Step5ReviewTests|FullyQualifiedName~SuccessionPagesTests|FullyQualifiedName~SuccessionTests|FullyQualifiedName~TapTargetTests|FullyQualifiedName~TeamListRowTests|FullyQualifiedName~UploadServiceTests"`
Expected: `Passed!  - Failed:     0, Passed:   445`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/RoleMatrixFixesTests.cs BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs BatteryPassWeb.Tests/Look/TapTargetTests.cs web/Controllers/ClusterBatteriesController.cs web/Dpp/Pages/PassportPages.cs web/Dpp/Audit/AuditLines.cs web/wwwroot/css/bp-product-data.css web/wwwroot/css/bp-status.css web/Views/Shared/_BatteryList.cshtml docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md
git commit -m "fix(dpp): the role-matrix check's faults: Not allowed for another cluster, findable invalid passports, the audit log in words, and phone fixes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2542, Skipped:     0, Total:  2542`. That is the 2534 from before plus 8 new test cases.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat dc488fa -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

These are not steps of this plan. The managing session brings them to the product owner:

1. On a phone (or the browser narrowed to a phone's width), the Registry shows "What do these mean?" beside "N batteries.". It opens the explainer, each badge above its meaning.
2. As `demo.south.admin@example.test`, a demo-north battery page's address leads to "Not allowed".
3. As market surveillance, mark DEMO-C13M-0001 invalid; signed out, searching DEMO-C13M-0001 shows it as invalid. (The team then undoes it.)
4. The team's audit log reads "Battery system uploaded values for…" and "… set the passport of battery DEMO-C13M-0001 inactive".

## Next

Step 6d, the identifier scheme (DUNS), planned once step 6c is accepted.
