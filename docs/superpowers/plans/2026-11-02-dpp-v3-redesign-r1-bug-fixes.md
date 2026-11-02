# DPP V3 Redesign R1: The Bugs on the Battery Pages — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The first of four parts of the battery pages' redesign (decision 36). It fixes the bugs the product owner found while trying step 4, before the layout changes:
- **Point 1:** "Reset demo data" says it's working, at once and in words, and always finishes. Closing the tab no longer stops it halfway. It is also about twice as fast on Atlas: the seed's batteries are made side by side. With a local relay adding 45 ms each way, a reset took 138 s before and 76 s after, so the text says "a little over a minute".
- **Points 2 and 3:** the "Change the battery status" window outlines the chosen option instead of drawing an odd oval around its radio. Its buttons stay at its bottom edge while the re-stated values scroll, so "Save and publish a new version" never needs scrolling.
- **Point 13:** every upload in the app (accident reports, the module list, documents, the family picture) shows the chosen file's name and size at once. A file of the wrong kind or over the size limit is refused in words before anything is sent; the server still checks the content.
- **Point 10b:** an invalid passport is found by its serial number (DEMO-C7M-0003), and its page says it's invalid, instead of "No published battery passport was found".
- **Found in the review:** a battery whose passport is no longer active (invalid, marked for deletion, inactive) says why nothing on it changes, instead of "Its passport is a draft", and its values editor offers no Save.

**Architecture:**
- **Demo reset** (task 1): the two buttons carry `data-confirm-busy`. `bp-admin-pages.js` shows that text in the confirm window and disables its buttons once confirmed. `AdminController.ResetDemoData`/`ClearDemoData` take no cancellation token and pass `CancellationToken.None`.
- **Status window** (task 2): CSS only, in `bp-product-data.css`. The general focus ring leaves radios out. A radio keeps its size (`align-self: start`), has no ring, and the whole option shows keyboard focus (`.bp-choice:has(input:focus-visible)`). `[data-status-dialog] .bp-dialog-actions` is sticky at the window's bottom.
- **Uploads** (task 3): the accident and module forms get the `data-upload-row` the script looks for. Every file input names its kind in words (`data-kind-words`) and its limit (`data-max-bytes`). `bp-record-editor.js` gains `sizeWords` and `uploadProblem`.
- **Search** (task 4): `PassportPages.FindAsync` falls back to the latest published version when no active version has the serial number (still exactly one battery).
- **Frozen passport** (task 5): `BatteryCards.FrozenAs` (the DPP status of a passport that is no longer active) gives the reason in words. `RecordEditorView.FrozenNote` makes the editor read-only (a disabled `fieldset`, no save bar). `BatteryPage.Editor` is the editor as the page shows it.
- **A faster seed** (task 6): the seed's about 1,200 database calls ran one after another, each waiting for Atlas's round trip.
  - `DemoDataService.SeedPackAsync` holds one battery's steps, in order; `SeedAsync` runs the 8 batteries side by side (`Task.WhenAll`).
  - The accounts are made alongside the product data.
  - Every save still goes through the save rules, and the counts and notes are the same.

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
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2255`. After this plan: 2268. The suite takes about two to four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../r1-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.** The new styles go to `bp-product-data.css` (battery pages) and the new `bp-status.css` (every page, R3).
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## What happens to the old tests

Nothing is removed. `DemoCardTests` calls the two demo actions without a cancellation token (task 1).

## A part to write yourself (optional)

The product owner chose "you write it" before; the plan gives the code. If they want to try one, the natural spot is `uploadProblem` in `web/wwwroot/js/bp-record-editor.js` (task 3): how a chosen file is matched against the input's `accept` list ("application/pdf,image/png" or ".json") and its size limit, and what the refusal says. `TheUploadScript_ShouldRefuseAFileOfTheWrongKindOrSizeBeforeSendingIt` names what it needs.

---

### Task 1: The demo reset says it's working and always finishes

Point 1: the reset took about two minutes on Atlas with only the browser's loading tab. The confirm window now shows "Resetting the demo data… This takes about 2 minutes" and disables its buttons once confirmed. The two actions no longer take the request's cancellation token, so closing the tab can't stop the seed halfway.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/wwwroot/js/bp-admin-pages.js`

- [ ] **Step 1: Write the failing tests**

Patch `r1-task1-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoCardTests.cs`). Save the block as `../r1-task1-tests.patch`, outside the worktree, and run `git apply ../r1-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
index 5680709..7d85575 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
@@ -48,7 +48,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
         using var rig = await DemoRig.StartAsync(mongo);
         var controller = Controller(rig);
 
-        var result = await controller.ResetDemoData(CancellationToken.None);
+        var result = await controller.ResetDemoData();
 
         Assert.Equal("/admin/clusters?tab=clusters", Assert.IsType<RedirectResult>(result).Url);
         Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 9 batteries, 6 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
@@ -64,7 +64,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
         await rig.Demo.SeedAsync(DemoRig.Who);
         var controller = Controller(rig);
 
-        await controller.ClearDemoData(CancellationToken.None);
+        await controller.ClearDemoData();
 
         Assert.StartsWith("Demo data cleared: 9 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
         Assert.Null(controller.TempData["GeneratedCredentials"]);
@@ -80,7 +80,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
             MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("clusterId", "demo-north"), MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set("isDemo", true));
         var controller = Controller(rig);
 
-        await controller.ResetDemoData(CancellationToken.None);
+        await controller.ResetDemoData();
 
         Assert.Equal(DppLinks.NotConfigured, controller.TempData["ErrorMessage"]);
         Assert.Equal(1, await rig.Count("clusters"));
@@ -130,6 +130,31 @@ public sealed class DemoCardTests(MongoFixture mongo)
         Assert.Contains("data-generated-token-copy", window, StringComparison.Ordinal);
     }
 
+    [Fact]
+    public void ResetAndClear_ShouldFinishEvenWhenTheTabIsClosed()
+    {
+        // Closing the tab cancels the request; the demo data must not stop halfway (owner's point 1, redesign R1).
+        foreach (var action in new[] { nameof(AdminController.ResetDemoData), nameof(AdminController.ClearDemoData) })
+        {
+            Assert.DoesNotContain(typeof(AdminController).GetMethod(action)!.GetParameters(), parameter => parameter.ParameterType == typeof(CancellationToken));
+        }
+    }
+
+    [Fact]
+    public void TheDemoButtons_ShouldSayTheyAreWorkingOnceConfirmed()
+    {
+        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
+        var card = page[page.IndexOf("data-demo-data-card", StringComparison.Ordinal)..page.IndexOf("@if (selectedTab == \"users\")", StringComparison.Ordinal)];
+        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-admin-pages.js"));
+
+        Assert.Equal(2, card.Split("data-confirm-busy=\"").Length - 1);
+        Assert.Contains("data-confirm-busy=\"Resetting the demo data… This takes about 2 minutes. Keep this page open; it reloads when done.\"", card, StringComparison.Ordinal);
+        Assert.Contains("data-confirm-busy=\"Clearing the demo data… This takes about a minute. Keep this page open; it reloads when done.\"", card, StringComparison.Ordinal);
+        Assert.Contains("const busy = pendingButton?.dataset.confirmBusy;", script, StringComparison.Ordinal);
+        Assert.Contains("confirmSubmit.disabled = true;", script, StringComparison.Ordinal);
+        Assert.Contains("if (confirmSubmit?.disabled)", script, StringComparison.Ordinal);
+    }
+
     private sealed class NoTempDataStorage : ITempDataProvider
     {
         public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoCardTests"`
Expected: the build fails with CS7036 (for example `error CS7036 There is no argument given that corresponds to the required parameter 'cancellationToken' of 'AdminController.ClearDemoData(CancellationToken)'`).

- [ ] **Step 3: Write the code**

Patch `r1-task1-code.patch` (`web/Controllers/AdminController.cs`, `web/Views/Admin/Clusters.cshtml`, `web/wwwroot/js/bp-admin-pages.js`). Save the block as `../r1-task1-code.patch`, outside the worktree, and run `git apply ../r1-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
index 6290cec..99bac3b 100644
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -956,17 +956,20 @@ public class AdminController : Controller
         return string.IsNullOrWhiteSpace(actor) ? "admin" : actor;
     }
 
-    /// <summary>Removes the demo data and makes it again (decision 32); the new tokens are shown once.</summary>
+    /// <summary>
+    /// Removes the demo data and makes it again (decision 32); the new tokens are shown once. It takes no cancellation
+    /// token: closing the tab must not stop it halfway (redesign R1).
+    /// </summary>
     [HttpPost("demo-data/reset")]
     [ValidateAntiForgeryToken]
-    public Task<IActionResult> ResetDemoData(CancellationToken cancellationToken) =>
-        DemoDataAsync("Demo data reset", () => _demoData.ResetAsync(CurrentActor(), cancellationToken));
+    public Task<IActionResult> ResetDemoData() =>
+        DemoDataAsync("Demo data reset", () => _demoData.ResetAsync(CurrentActor(), CancellationToken.None));
 
-    /// <summary>Removes the demo data and keeps the real data (decision 32).</summary>
+    /// <summary>Removes the demo data and keeps the real data (decision 32); like the reset, it always finishes.</summary>
     [HttpPost("demo-data/clear")]
     [ValidateAntiForgeryToken]
-    public Task<IActionResult> ClearDemoData(CancellationToken cancellationToken) =>
-        DemoDataAsync("Demo data cleared", () => _demoData.ClearAsync(CurrentActor(), cancellationToken));
+    public Task<IActionResult> ClearDemoData() =>
+        DemoDataAsync("Demo data cleared", () => _demoData.ClearAsync(CurrentActor(), CancellationToken.None));
 
     private async Task<IActionResult> DemoDataAsync(string heading, Func<Task<DemoResult>> run)
     {
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
index d4b798d..f146e17 100644
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -228,11 +228,11 @@
             <div class="d-flex flex-wrap gap-2">
                 <form method="post" action="/admin/demo-data/reset">
                     @Html.AntiForgeryToken()
-                    <button type="submit" class="bp-secondary-button" data-confirm-action data-confirm-title="Reset demo data" data-confirm-message="Remove the demo data and make it again? Real data stays as it is. The new API tokens are shown once." data-confirm-submit="Reset demo data">Reset demo data</button>
+                    <button type="submit" class="bp-secondary-button" data-confirm-action data-confirm-title="Reset demo data" data-confirm-message="Remove the demo data and make it again? Real data stays as it is. The new API tokens are shown once." data-confirm-submit="Reset demo data" data-confirm-busy="Resetting the demo data… This takes about 2 minutes. Keep this page open; it reloads when done.">Reset demo data</button>
                 </form>
                 <form method="post" action="/admin/demo-data/clear">
                     @Html.AntiForgeryToken()
-                    <button type="submit" class="bp-danger-button" data-confirm-action data-confirm-title="Clear demo data" data-confirm-message="Remove the demo data? Real data stays as it is, and a demo record that real data uses is kept." data-confirm-submit="Clear demo data">Clear demo data</button>
+                    <button type="submit" class="bp-danger-button" data-confirm-action data-confirm-title="Clear demo data" data-confirm-message="Remove the demo data? Real data stays as it is, and a demo record that real data uses is kept." data-confirm-submit="Clear demo data" data-confirm-busy="Clearing the demo data… This takes about a minute. Keep this page open; it reloads when done.">Clear demo data</button>
                 </form>
             </div>
         </section>
diff --git a/web/wwwroot/js/bp-admin-pages.js b/web/wwwroot/js/bp-admin-pages.js
index 55843f1..2dccbbb 100644
--- a/web/wwwroot/js/bp-admin-pages.js
+++ b/web/wwwroot/js/bp-admin-pages.js
@@ -62,6 +62,7 @@
     const confirmMessage = confirmModal?.querySelector('[data-confirm-message]');
     const confirmSubmit = confirmModal?.querySelector('[data-confirm-submit]');
     let pendingForm = null;
+    let pendingButton = null;
     document.querySelectorAll('[data-confirm-action]').forEach((button) => {
         button.addEventListener('click', (event) => {
             const form = button.closest('form');
@@ -71,6 +72,7 @@
 
             event.preventDefault();
             pendingForm = form;
+            pendingButton = button;
             confirmTitle.textContent = button.dataset.confirmTitle || 'Confirm action';
             confirmMessage.textContent = button.dataset.confirmMessage || 'Continue with this action?';
             confirmSubmit.textContent = button.dataset.confirmSubmit || 'Confirm';
@@ -81,14 +83,30 @@
 
     confirmModal?.querySelectorAll('[data-confirm-cancel]').forEach((button) => {
         button.addEventListener('click', () => {
+            if (confirmSubmit?.disabled) {
+                return; // already sent: the page reloads when it's done
+            }
+
             confirmModal.hidden = true;
             pendingForm = null;
         });
     });
 
     confirmSubmit?.addEventListener('click', () => {
-        if (pendingForm) {
-            pendingForm.submit();
+        if (!pendingForm) {
+            return;
         }
+
+        // A long action (the demo data) says it's working and can't be sent twice (redesign R1, owner's point 1).
+        const busy = pendingButton?.dataset.confirmBusy;
+        if (busy) {
+            confirmMessage.textContent = busy;
+            confirmSubmit.textContent = 'Working…';
+            confirmSubmit.disabled = true;
+            confirmModal.querySelectorAll('button[data-confirm-cancel]').forEach((button) => { button.disabled = true; });
+            confirmModal.querySelector('[role="dialog"]')?.setAttribute('aria-busy', 'true');
+        }
+
+        pendingForm.submit();
     });
 })();
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoCardTests"`
Expected: `Passed!  - Failed:     0, Passed:     8`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2257`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCardTests.cs web/Controllers/AdminController.cs web/Views/Admin/Clusters.cshtml web/wwwroot/js/bp-admin-pages.js
git commit -m "fix(dpp): the demo reset says it's working and always finishes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The status window: the chosen option outlined, its buttons in view

Points 2 and 3. The "odd oval that fades" was the general focus ring stretched over the radio, gone when the focus moved. Radios now keep their size and have no ring; the chosen option is outlined, and keyboard focus outlines the option. The window's buttons stay at its bottom edge while its values scroll.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/RedesignFixesTests.cs`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/RedesignFixesTests.cs`:

```csharp
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
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~RedesignFixesTests"`
Expected: `Failed:     2, Passed:     0` (failing: `RedesignFixesTests.TheStatusWindow_ShouldKeepItsButtonsInViewWhileItsValuesScroll`, `RedesignFixesTests.TheStatusWindow_ShouldOutlineTheChosenOptionInsteadOfARingAroundItsRadio`).

- [ ] **Step 3: Write the code**

Patch `r1-task2-code.patch` (`web/wwwroot/css/bp-product-data.css`). Save the block as `../r1-task2-code.patch`, outside the worktree, and run `git apply ../r1-task2-code.patch` from the worktree root:

```diff
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 1e86d12..fc0ad97 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -80,8 +80,8 @@ main :is(input, textarea)::placeholder {
   font-weight: 400;
 }
 
-/* A visible focus ring on every field. */
-main :is(input:not([type="checkbox"]):not([type="file"]), select, textarea):focus {
+/* A visible focus ring on every field. Radios have their own (their option is outlined), see .bp-choice. */
+main :is(input:not([type="checkbox"]):not([type="radio"]):not([type="file"]), select, textarea):focus {
   border-color: var(--bp-primary);
   box-shadow: 0 0 0 3px rgba(42, 110, 207, 0.25);
   outline: none;
@@ -2395,11 +2395,24 @@ main .bp-field-marker.is-published {
 
 .bp-console-form-card .bp-choice input[type="radio"] {
   grid-row: span 2;
+  align-self: start;
   width: auto;
   min-height: 0;
   margin-top: 4px;
   padding: 0;
   box-shadow: none;
+  accent-color: var(--bp-primary);
+}
+
+/* No ring around the radio itself (it was stretched into an oval, redesign R1): the whole option shows the focus. */
+.bp-console-form-card .bp-choice input[type="radio"]:focus {
+  box-shadow: none;
+  outline: none;
+}
+
+.bp-choice:has(input:focus-visible) {
+  outline: 2px solid var(--bp-primary);
+  outline-offset: 2px;
 }
 
 .bp-choice small {
@@ -2434,6 +2447,21 @@ main .bp-field-marker.is-published {
   min-width: 0;
 }
 
+/* The status window's buttons stay at its bottom edge while its values scroll (redesign R1, owner's point 3). */
+[data-status-dialog] .bp-dialog-actions {
+  position: sticky;
+  bottom: 0;
+  z-index: 1;
+  margin: 0 -20px -20px;
+  padding: 12px 20px 16px;
+  border-top: 1px solid var(--bp-border);
+  background: #fff;
+}
+
+[data-theme="dark"] [data-status-dialog] .bp-dialog-actions {
+  background: #101f35;
+}
+
 /* The Help: one place, a contents list on the left with only the reader's parts. */
 .bp-help-layout {
   display: grid;
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~RedesignFixesTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2259`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Look/RedesignFixesTests.cs web/wwwroot/css/bp-product-data.css
git commit -m "fix(dpp): the status window outlines the chosen option and keeps its buttons in view" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Every upload says what was chosen, and refuses the wrong file at once

Point 13. The accident and module forms lacked the row marker the script looks for, so nothing showed that a file was taken. Every file input now names its kind and size limit. The script shows "Ready to upload: name · size", or why the file can't be sent, and the button waits for a file that may be. The server still checks each file by its content.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Look/RedesignFixesTests.cs`
- Modify: `web/Views/Batteries/_AccidentsCard.cshtml`
- Modify: `web/Views/Batteries/_ModulesCard.cshtml`
- Modify: `web/Views/ProductData/Family.cshtml`
- Modify: `web/Views/ProductData/_EditorField.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`
- Modify: `web/wwwroot/js/bp-record-editor.js`

- [ ] **Step 1: Write the failing tests**

Patch `r1-task3-tests.patch` (`BatteryPassWeb.Tests/Look/RedesignFixesTests.cs`). Save the block as `../r1-task3-tests.patch`, outside the worktree, and run `git apply ../r1-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Look/RedesignFixesTests.cs b/BatteryPassWeb.Tests/Look/RedesignFixesTests.cs
index 8222bea..d64fdeb 100644
--- a/BatteryPassWeb.Tests/Look/RedesignFixesTests.cs
+++ b/BatteryPassWeb.Tests/Look/RedesignFixesTests.cs
@@ -36,4 +36,50 @@ public sealed class RedesignFixesTests
         Assert.Contains("bottom: 0;", actions, StringComparison.Ordinal);
         Assert.Contains("background: #101f35;", Block(Css, "[data-theme=\"dark\"] [data-status-dialog] .bp-dialog-actions {"), StringComparison.Ordinal);
     }
+
+    [Theory]
+    [InlineData("Batteries", "_AccidentsCard.cshtml", "a PDF, PNG or JPEG", "@AppOptions.Value.MaxUploadBytes")]
+    [InlineData("Batteries", "_ModulesCard.cshtml", "a JSON file", "@BatteryPassWeb.Controllers.BatteriesController.MaxExportBytes")]
+    [InlineData("ProductData", "_EditorField.cshtml", "a PDF, PNG or JPEG", "@Options.Value.MaxUploadBytes")]
+    [InlineData("ProductData", "Family.cshtml", "a PNG or JPEG", "@AppOptions.Value.MaxUploadBytes")]
+    public void EveryUpload_ShouldSayWhichKindAndHowLargeAFileItTakes(string folder, string view, string kind, string limit)
+    {
+        // Point 13: every upload is checked as the right kind of file for it, with clear feedback.
+        var page = Read("web", "Views", folder, view);
+
+        Assert.Contains($"data-kind-words=\"{kind}\"", page, StringComparison.Ordinal);
+        Assert.Contains($"data-max-bytes=\"{limit}\"", page, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void TheAccidentAndModuleUploads_ShouldShowTheChosenFile()
+    {
+        // The script finds the chosen-file line through the form's data-upload-row; these two forms had none (point 13).
+        foreach (var view in new[] { "_AccidentsCard.cshtml", "_ModulesCard.cshtml" })
+        {
+            var page = Read("web", "Views", "Batteries", view);
+            var upload = page.IndexOf("enctype=\"multipart/form-data\"", StringComparison.Ordinal);
+            var start = page.LastIndexOf("<form", upload, StringComparison.Ordinal);
+            var openingTag = page[start..page.IndexOf('>', upload)];
+            var form = page[start..page.IndexOf("</form>", upload, StringComparison.Ordinal)];
+
+            Assert.Contains("data-upload-row", openingTag, StringComparison.Ordinal);
+            Assert.Contains("data-upload-chosen", form, StringComparison.Ordinal);
+            Assert.Contains("data-upload-button", form, StringComparison.Ordinal);
+        }
+    }
+
+    [Fact]
+    public void TheUploadScript_ShouldRefuseAFileOfTheWrongKindOrSizeBeforeSendingIt()
+    {
+        var script = Read("web", "wwwroot", "js", "bp-record-editor.js");
+
+        Assert.Contains("function uploadProblem(picked, input)", script, StringComparison.Ordinal);
+        Assert.Contains("input.dataset.kindWords", script, StringComparison.Ordinal);
+        Assert.Contains("input.dataset.maxBytes", script, StringComparison.Ordinal);
+        Assert.Contains("'Ready to upload: ' + picked.name + ' · ' + sizeWords(picked.size)", script, StringComparison.Ordinal);
+        Assert.Contains("button.disabled = !picked || problem !== null;", script, StringComparison.Ordinal);
+        Assert.Contains("[data-upload-chosen].is-wrong {", Css, StringComparison.Ordinal);
+        Assert.Contains("[data-upload-chosen].is-good {", Css, StringComparison.Ordinal);
+    }
 }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~RedesignFixesTests"`
Expected: `Failed:     6, Passed:     2` (failing: `RedesignFixesTests.TheAccidentAndModuleUploads_ShouldShowTheChosenFile`, `RedesignFixesTests.TheUploadScript_ShouldRefuseAFileOfTheWrongKindOrSizeBeforeSendingIt`).

- [ ] **Step 3: Write the code**

Patch `r1-task3-code.patch` (`web/Views/Batteries/_AccidentsCard.cshtml`, `web/Views/Batteries/_ModulesCard.cshtml`, `web/Views/ProductData/Family.cshtml`, `web/Views/ProductData/_EditorField.cshtml`, `web/wwwroot/css/bp-product-data.css`, `web/wwwroot/js/bp-record-editor.js`). Save the block as `../r1-task3-code.patch`, outside the worktree, and run `git apply ../r1-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Batteries/_AccidentsCard.cshtml b/web/Views/Batteries/_AccidentsCard.cshtml
index 037e7f3..f36afb0 100644
--- a/web/Views/Batteries/_AccidentsCard.cshtml
+++ b/web/Views/Batteries/_AccidentsCard.cshtml
@@ -1,4 +1,5 @@
 @using BatteryPassWeb.Dpp.Pages
+@inject Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> AppOptions
 @model BatteryCards
 @* The accident reports card (#92): on the team's battery page and on the cluster admin's (step 4a). *@
 <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="accidents">
@@ -24,12 +25,12 @@
     }
     else
     {
-    <form method="post" action="@Model.Href/accidents" enctype="multipart/form-data" class="bp-attr-upload bp-accident-upload">
+    <form method="post" action="@Model.Href/accidents" enctype="multipart/form-data" class="bp-attr-upload bp-accident-upload" data-upload-row>
         @Html.AntiForgeryToken()
         <div class="bp-attr-file">
             <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                 Choose file
-                <input type="file" name="file" class="bp-document-hidden-file" accept="application/pdf,image/png,image/jpeg" data-upload-file />
+                <input type="file" name="file" class="bp-document-hidden-file" accept="application/pdf,image/png,image/jpeg" data-kind-words="a PDF, PNG or JPEG" data-max-bytes="@AppOptions.Value.MaxUploadBytes" data-upload-file />
             </label>
             <span class="bp-help-text" data-upload-chosen>No file chosen</span>
         </div>
@@ -50,7 +51,7 @@
             </label>
         </div>
         <div class="bp-live-save">
-            <small class="bp-field-help">PDF, PNG or JPEG, checked by its content.</small>
+            <small class="bp-field-help">PDF, PNG or JPEG, up to @BatteryCards.Size(AppOptions.Value.MaxUploadBytes), checked by its content.</small>
             <button type="submit" class="bp-secondary-button" data-upload-button>Add report</button>
         </div>
     </form>
diff --git a/web/Views/Batteries/_ModulesCard.cshtml b/web/Views/Batteries/_ModulesCard.cshtml
index 5cd2f66..d5a7bc6 100644
--- a/web/Views/Batteries/_ModulesCard.cshtml
+++ b/web/Views/Batteries/_ModulesCard.cshtml
@@ -37,12 +37,12 @@
     }
     @if (Model.ForTeam)
     {
-        <form method="post" action="@Model.Href/modules/from-export" enctype="multipart/form-data" class="bp-attr-upload bp-module-upload">
+        <form method="post" action="@Model.Href/modules/from-export" enctype="multipart/form-data" class="bp-attr-upload bp-module-upload" data-upload-row>
             @Html.AntiForgeryToken()
             <div class="bp-attr-file">
                 <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                     Choose file
-                    <input type="file" name="file" class="bp-document-hidden-file" accept="application/json,.json" data-upload-file />
+                    <input type="file" name="file" class="bp-document-hidden-file" accept="application/json,.json" data-kind-words="a JSON file" data-max-bytes="@BatteryPassWeb.Controllers.BatteriesController.MaxExportBytes" data-upload-file />
                 </label>
                 <span class="bp-help-text" data-upload-chosen>No file chosen</span>
             </div>
diff --git a/web/Views/ProductData/Family.cshtml b/web/Views/ProductData/Family.cshtml
index b406593..8951b21 100644
--- a/web/Views/ProductData/Family.cshtml
+++ b/web/Views/ProductData/Family.cshtml
@@ -1,5 +1,6 @@
 @using BatteryPassWeb.Dpp.Editing
 @using BatteryPassWeb.Dpp.Pages
+@inject Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> AppOptions
 @model FamilyPage
 @{
     var selected = Model.SelectedModel;
@@ -50,7 +51,7 @@
                         <input type="text" readonly value="@(hasPicture ? "A picture is set" : "No picture yet")" aria-label="Current picture" data-upload-name />
                         <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap flex-shrink-0 mb-0">
                             Choose picture
-                            <input type="file" name="picture" class="bp-document-hidden-file" accept="image/png,image/jpeg" data-upload-file />
+                            <input type="file" name="picture" class="bp-document-hidden-file" accept="image/png,image/jpeg" data-kind-words="a PNG or JPEG" data-max-bytes="@AppOptions.Value.MaxUploadBytes" data-upload-file />
                         </label>
                         <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap" data-upload-button>Save picture</button>
                     </div>
diff --git a/web/Views/ProductData/_EditorField.cshtml b/web/Views/ProductData/_EditorField.cshtml
index 4fc04b9..8edb94a 100644
--- a/web/Views/ProductData/_EditorField.cshtml
+++ b/web/Views/ProductData/_EditorField.cshtml
@@ -188,7 +188,7 @@
                     <div class="bp-attr-file">
                         <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                             Choose file
-                            <input type="file" name="file" class="bp-document-hidden-file" accept="application/pdf,image/png,image/jpeg" form="@formId" data-upload-file />
+                            <input type="file" name="file" class="bp-document-hidden-file" accept="application/pdf,image/png,image/jpeg" form="@formId" data-kind-words="a PDF, PNG or JPEG" data-max-bytes="@Options.Value.MaxUploadBytes" data-upload-file />
                         </label>
                         <span class="bp-help-text" data-upload-chosen>No file chosen</span>
                         <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap" form="@formId" data-upload-button>Upload</button>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index fc0ad97..1ff74a5 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -2447,6 +2447,21 @@ main .bp-field-marker.is-published {
   min-width: 0;
 }
 
+/* A chosen file: ready (green) or why it can't be sent (red), before anything is uploaded (redesign R1). */
+[data-upload-chosen].is-good {
+  color: #166534;
+  font-weight: 600;
+}
+
+[data-upload-chosen].is-wrong {
+  color: var(--bp-danger);
+  font-weight: 600;
+}
+
+[data-theme="dark"] [data-upload-chosen].is-good {
+  color: #86efac;
+}
+
 /* The status window's buttons stay at its bottom edge while its values scroll (redesign R1, owner's point 3). */
 [data-status-dialog] .bp-dialog-actions {
   position: sticky;
diff --git a/web/wwwroot/js/bp-record-editor.js b/web/wwwroot/js/bp-record-editor.js
index df034de..c8cf8c3 100644
--- a/web/wwwroot/js/bp-record-editor.js
+++ b/web/wwwroot/js/bp-record-editor.js
@@ -2,22 +2,57 @@
 // "Discard unsaved edits?", the warning before leaving with unsaved edits, and the republish question.
 // Without JavaScript every section stays visible and the forms still work.
 (function () {
-    // A chosen file shows its name; its Upload button waits for one.
+    // A file size in plain words, like the pages' own (BatteryCards.Size).
+    function sizeWords(bytes) {
+        if (bytes < 1024) {
+            return bytes + ' bytes';
+        }
+
+        return bytes < 1048576 ? Math.round(bytes / 1024) + ' KB' : (bytes / 1048576).toFixed(1).replace(/\.0$/, '') + ' MB';
+    }
+
+    // Why a chosen file can't be sent, in words, or null when it may (redesign R1, owner's point 13): its kind against
+    // the input's accept list ("application/pdf,image/png" or ".json"), then its size against data-max-bytes. The server
+    // still checks the file's content; this only says it at once, before a long upload.
+    function uploadProblem(picked, input) {
+        const kinds = (input.accept || '').split(',').map((kind) => kind.trim().toLowerCase()).filter((kind) => kind.length > 0);
+        const name = picked.name.toLowerCase();
+        const type = (picked.type || '').toLowerCase();
+        const fits = kinds.length === 0 || kinds.some((kind) => kind.startsWith('.')
+            ? name.endsWith(kind)
+            : kind.endsWith('/*') ? type.startsWith(kind.slice(0, -1)) : type === kind);
+        if (!fits) {
+            return picked.name + ' isn’t ' + (input.dataset.kindWords || 'the right kind of file') + '. Choose another file.';
+        }
+
+        const max = Number(input.dataset.maxBytes || 0);
+        if (max > 0 && picked.size > max) {
+            return picked.name + ' is ' + sizeWords(picked.size) + '; the most is ' + sizeWords(max) + '. Choose a smaller file.';
+        }
+
+        return null;
+    }
+
+    // A chosen file shows its name and size, or why it can't be sent; its Upload button waits for a file that may be.
     document.querySelectorAll('[data-upload-file]').forEach((file) => {
         const row = file.closest('[data-upload-row], .bp-attr-row');
         const chosen = row?.querySelector('[data-upload-chosen]');
         const name = row?.querySelector('[data-upload-name]');
         const button = row?.querySelector('[data-upload-button]');
         const update = () => {
-            const picked = file.files && file.files.length > 0 ? file.files[0].name : null;
+            const picked = file.files && file.files.length > 0 ? file.files[0] : null;
+            const problem = picked ? uploadProblem(picked, file) : null;
+            const text = !picked ? 'No file chosen' : problem ?? 'Ready to upload: ' + picked.name + ' · ' + sizeWords(picked.size);
             if (button) {
-                button.disabled = !picked;
+                button.disabled = !picked || problem !== null;
             }
 
             if (chosen) {
-                chosen.textContent = picked ? 'Ready to upload: ' + picked : 'No file chosen';
+                chosen.textContent = text;
+                chosen.classList.toggle('is-good', picked !== null && problem === null);
+                chosen.classList.toggle('is-wrong', problem !== null);
             } else if (name && picked) {
-                name.value = 'Ready to upload: ' + picked;
+                name.value = text;
             }
         };
         file.addEventListener('change', update);
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~RedesignFixesTests"`
Expected: `Passed!  - Failed:     0, Passed:     8`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2265`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Look/RedesignFixesTests.cs web/Views/Batteries/_AccidentsCard.cshtml web/Views/Batteries/_ModulesCard.cshtml web/Views/ProductData/Family.cshtml web/Views/ProductData/_EditorField.cshtml web/wwwroot/css/bp-product-data.css web/wwwroot/js/bp-record-editor.js
git commit -m "fix(dpp): every upload says what was chosen and refuses the wrong kind or size at once" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: An invalid passport is found by its serial number

Point 10b. Marking a passport invalid changes its only version, so no active version was left for the search by serial number. When none is active, the latest published version counts (still only when exactly one battery has that serial number), and its page says it is invalid.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`

- [ ] **Step 1: Write the failing tests**

Patch `r1-task4-tests.patch` (`BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`). Save the block as `../r1-task4-tests.patch`, outside the worktree, and run `git apply ../r1-task4-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
index a197206..a66550a 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
@@ -69,6 +69,22 @@ public sealed class PassportMarkPagesTests(MongoFixture mongo)
         Assert.False(surveillance.MayMarkInvalid); // already invalid
     }
 
+    [Fact]
+    public async Task AnInvalidPassport_ShouldStillBeFoundByItsSerialNumber()
+    {
+        // Point 10b: marking it invalid leaves no active version, and the search said "No published battery passport was found".
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("msa@example.com"), rig.PackId, Reason);
+        var pages = PassportPagesTests.Pages(rig);
+
+        var found = await pages.FindAsync("VPC8C05-6788");
+        var located = (await pages.LocateAsync("VPC8C05-6788"))!;
+
+        Assert.Equal(DppStatuses.Invalid, found!.DppStatus);
+        Assert.Equal(PassportPagesTests.Identifier(rig), located.Identifier);
+        Assert.Equal("Invalid", (await pages.SummaryAsync(Reader.Anonymous, located.Address))!.Notice!.Pill);
+    }
+
     [Fact]
     public async Task MarketSurveillance_ShouldMarkInvalidFromTheDetailPageAndNobodyElse()
     {
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkPagesTests"`
Expected: `Failed:     1, Passed:     6` (failing: `PassportMarkPagesTests.AnInvalidPassport_ShouldStillBeFoundByItsSerialNumber`).

- [ ] **Step 3: Write the code**

Patch `r1-task4-code.patch` (`web/Dpp/Pages/PassportPages.cs`). Save the block as `../r1-task4-code.patch`, outside the worktree, and run `git apply ../r1-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index 44cb28b..23e8089 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -160,7 +160,8 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
 
     /// <summary>
     /// The published version a battery ID (#6), passport ID (#5) or serial number (#11) names; null when there is none.
-    /// A serial number counts only when exactly one published battery has it.
+    /// A serial number counts only when exactly one published battery has it: the one with an active passport, else (an
+    /// invalid or marked passport, redesign R1) the one whose latest version is published.
     /// </summary>
     public async Task<PassportVersion?> FindAsync(string? id, CancellationToken cancellationToken = default)
     {
@@ -181,8 +182,9 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
             return await store.GetActiveVersionAsync(pack.Id, cancellationToken) ?? await store.GetLatestVersionAsync(pack.Id, cancellationToken);
         }
 
+        var candidates = await store.FindPacksBySerialNumberAsync(text, cancellationToken);
         var published = new List<PassportVersion>();
-        foreach (var candidate in await store.FindPacksBySerialNumberAsync(text, cancellationToken))
+        foreach (var candidate in candidates)
         {
             if (await store.GetActiveVersionAsync(candidate.Id, cancellationToken) is { } active)
             {
@@ -190,6 +192,18 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
             }
         }
 
+        if (published.Count == 0)
+        {
+            // Marking a passport invalid leaves it no active version; it stays findable and says so (owner's point 10b).
+            foreach (var candidate in candidates)
+            {
+                if (await store.GetLatestVersionAsync(candidate.Id, cancellationToken) is { } latest)
+                {
+                    published.Add(latest);
+                }
+            }
+        }
+
         return published.Count == 1 ? published[0] : null;
     }
 
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkPagesTests"`
Expected: `Passed!  - Failed:     0, Passed:     7`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2266`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs web/Dpp/Pages/PassportPages.cs
git commit -m "fix(dpp): an invalid passport is found by its serial number" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: A frozen passport says why nothing changes

Found in the review. An invalid, marked or inactive battery's values editor said "Its passport is a draft" and offered Save, which the save pipeline then refused. Its live values and accident cards said the data "continue on its new passport", even when there is none. Each now says why, in words that fit the status, and the values editor is read-only.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`
- Modify: `web/Dpp/Pages/BatteryCards.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/ProductDataPages.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/ProductData/_RecordEditor.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `r1-task5-tests.patch` (`BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`). Save the block as `../r1-task5-tests.patch`, outside the worktree, and run `git apply ../r1-task5-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
index a66550a..5a24f1b 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
@@ -69,6 +69,24 @@ public sealed class PassportMarkPagesTests(MongoFixture mongo)
         Assert.False(surveillance.MayMarkInvalid); // already invalid
     }
 
+    [Fact]
+    public async Task AnInvalidBattery_ShouldSayWhyNothingChangesInsteadOfCallingItADraft()
+    {
+        // Found in the redesign review: its values editor said "Its passport is a draft" and offered Save.
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("msa@example.com"), rig.PackId, Reason);
+        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
+        var editor = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "_RecordEditor.cshtml"));
+        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
+
+        Assert.Equal("The passport is invalid: nothing on it changes until your team undoes it.", page.Cards.FrozenNote);
+        Assert.Equal(page.Cards.FrozenNote, page.Editor.FrozenNote);
+        Assert.Null(page.Values.FrozenNote);
+        Assert.Contains("<fieldset class=\"bp-frozen-fields\" disabled=\"@(Model.FrozenNote is not null)\">", editor, StringComparison.Ordinal);
+        Assert.Contains("@if (Model.FrozenNote is null)", editor, StringComparison.Ordinal);
+        Assert.Contains("@await Html.PartialAsync(\"~/Views/ProductData/_RecordEditor.cshtml\", Model.Editor)", battery, StringComparison.Ordinal);
+    }
+
     [Fact]
     public async Task AnInvalidPassport_ShouldStillBeFoundByItsSerialNumber()
     {
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkPagesTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'BatteryPage' does not contain a definition for 'Editor'`).

- [ ] **Step 3: Write the code**

Patch `r1-task5-code.patch` (`web/Dpp/Pages/BatteryCards.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ProductDataPages.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/ProductData/_RecordEditor.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r1-task5-code.patch`, outside the worktree, and run `git apply ../r1-task5-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/BatteryCards.cs b/web/Dpp/Pages/BatteryCards.cs
index 7c1c14e..fe35eb4 100644
--- a/web/Dpp/Pages/BatteryCards.cs
+++ b/web/Dpp/Pages/BatteryCards.cs
@@ -10,6 +10,8 @@ namespace BatteryPassWeb.Dpp.Pages;
 /// </summary>
 /// <param name="ForTeam">The team's page: it edits the module list and links the model's module description.</param>
 /// <param name="ModuleDescriptionHref">Where the team types the model's module description; null on the cluster admin's page.</param>
+/// <param name="FrozenAs">The DPP status (#2) of a passport that is no longer active (inactive, invalid or marked for
+/// deletion); null while it is active or a draft.</param>
 public sealed record BatteryCards(
     BatteryPack Pack,
     string Href,
@@ -20,10 +22,19 @@ public sealed record BatteryCards(
     bool ForTeam,
     string? ModuleDescriptionHref = null,
     bool HasModuleDescription = false,
-    bool Frozen = false)
+    string? FrozenAs = null)
 {
-    /// <summary>Why the live values and accident reports take nothing more (an inactive passport, step 4c); null otherwise.</summary>
-    public string? FrozenNote => Frozen ? "The passport is no longer active: nothing on it changes any more. The battery's data continue on its new passport." : null;
+    /// <summary>Whether nothing on the passport changes any more (step 4c).</summary>
+    public bool Frozen => FrozenAs is not null;
+
+    /// <summary>Why nothing on the passport changes, in words that fit its status (redesign R1); null while it can change.</summary>
+    public string? FrozenNote => FrozenAs switch
+    {
+        null => null,
+        DppStatuses.Invalid => $"The passport is invalid: nothing on it changes until {Team} undoes it.",
+        DppStatuses.MarkedForDeletion => $"The passport is marked for deletion: nothing on it changes until {Team} undoes it.",
+        _ => "The passport is no longer active: nothing on it changes any more. The battery's data continue on its new passport.",
+    };
 
     /// <summary>Who the team is, in the reader's words.</summary>
     public string Team => ForTeam ? "your team" : "the team";
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 1342aaa..6752db1 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -133,7 +133,10 @@ public sealed record BatteryPage(
         ForTeam: true,
         $"{ProductDataPages.FamilyHref(Sources.Model.FamilyId, Sources.Model.Id)}#modules",
         !string.IsNullOrWhiteSpace(Sources.Model.ModuleDescription),
-        Frozen: Publication.NoLongerActive);
+        FrozenAs: Publication.NoLongerActive ? Publication.Latest!.DppStatus : null);
+
+    /// <summary>The passport values editor as the page shows it: read-only, with the reason, once nothing changes (redesign R1).</summary>
+    public RecordEditorView Editor => Cards.FrozenNote is { } note ? Values with { FrozenNote = note } : Values;
 }
 
 /// <summary>
diff --git a/web/Dpp/Pages/ProductDataPages.cs b/web/Dpp/Pages/ProductDataPages.cs
index 3a2d0e9..1786367 100644
--- a/web/Dpp/Pages/ProductDataPages.cs
+++ b/web/Dpp/Pages/ProductDataPages.cs
@@ -42,6 +42,9 @@ public sealed record RecordEditorView(
     /// <summary>Shown to the team (the default); false on the cluster admin's battery page (step 4a), which says "the team".</summary>
     public bool ForTeam { get; init; } = true;
 
+    /// <summary>Why nothing can be saved (a battery whose passport is no longer active, redesign R1); null when it can.</summary>
+    public string? FrozenNote { get; init; }
+
     public string UploadAction(string property) => $"{SaveAction}/documents/{Uri.EscapeDataString(property)}";
 
     public string KindLabel => Kind switch
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 45d8930..c036d92 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -286,7 +286,7 @@
     }
 
     <div id="values">
-        @await Html.PartialAsync("~/Views/ProductData/_RecordEditor.cshtml", Model.Values)
+        @await Html.PartialAsync("~/Views/ProductData/_RecordEditor.cshtml", Model.Editor)
     </div>
     @if (publication.IsPublished)
     {
diff --git a/web/Views/ProductData/_RecordEditor.cshtml b/web/Views/ProductData/_RecordEditor.cshtml
index 2a4a8ff..b180ddf 100644
--- a/web/Views/ProductData/_RecordEditor.cshtml
+++ b/web/Views/ProductData/_RecordEditor.cshtml
@@ -28,7 +28,9 @@
         TargetKind.Pack => "Only this battery has these values; the others come from its model, batch, site and operator.",
         _ => null,
     };
-    var publishedNote = Model.Kind == TargetKind.Pack
+    var publishedNote = Model.FrozenNote is { } frozen
+        ? frozen
+        : Model.Kind == TargetKind.Pack
         ? Model.PublishedPassports > 0
             ? "Its passport is published: saving a change publishes a new version, and you're asked first."
             : "Its passport is a draft: saving changes the draft only."
@@ -132,6 +134,7 @@
         }
 
         <div class="bp-edit-form">
+            <fieldset class="bp-frozen-fields" disabled="@(Model.FrozenNote is not null)">
             @foreach (var group in Model.Sections)
             {
                 <section id="@group.Id" class="bp-card bp-edit-section bp-console-form-card@(group.Missing == 0 ? " has-no-missing" : string.Empty)" data-section-title="@group.Title">
@@ -146,6 +149,10 @@
                 </section>
             }
 
+            </fieldset>
+
+            @if (Model.FrozenNote is null)
+            {
             <div class="bp-product-editor-savebar">
                 <span class="bp-savebar-text" data-save-status>Only the values you change are saved.</span>
                 <div class="bp-savebar-actions">
@@ -153,6 +160,7 @@
                     <button type="submit" class="bp-primary-button">Save @kindWord</button>
                 </div>
             </div>
+            }
         </div>
 
         @if (Model.Confirm.Count > 0)
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 1ff74a5..a342a30 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -2447,6 +2447,11 @@ main .bp-field-marker.is-published {
   min-width: 0;
 }
 
+/* A frozen passport's values: shown, not editable (redesign R1). The fieldset only disables its fields. */
+.bp-frozen-fields {
+  display: contents;
+}
+
 /* A chosen file: ready (green) or why it can't be sent (red), before anything is uploaded (redesign R1). */
 [data-upload-chosen].is-good {
   color: #166534;
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkPagesTests"`
Expected: `Passed!  - Failed:     0, Passed:     8`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2267`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs web/Dpp/Pages/BatteryCards.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/ProductDataPages.cs web/Views/Batteries/Battery.cshtml web/Views/ProductData/_RecordEditor.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "fix(dpp): a frozen passport says why nothing on it changes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The demo seed makes the batteries side by side

Point 1, the time itself. On Atlas each of the seed's about 1,200 database calls waits for its round trip. The batteries don't depend on each other, nor the accounts on the product data, so they now run side by side. Each battery's own steps stay in order, and every save still goes through the save rules. Measured with a local relay adding 45 ms each way, a reset went from 138 s to 76 s. The confirm window now says "a little over a minute", and "about 20 seconds" for clearing.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r1-task6-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoCardTests.cs`). Save the block as `../r1-task6-tests.patch`, outside the worktree, and run `git apply ../r1-task6-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
index 7d85575..c1b2584 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
@@ -148,13 +148,26 @@ public sealed class DemoCardTests(MongoFixture mongo)
         var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-admin-pages.js"));
 
         Assert.Equal(2, card.Split("data-confirm-busy=\"").Length - 1);
-        Assert.Contains("data-confirm-busy=\"Resetting the demo data… This takes about 2 minutes. Keep this page open; it reloads when done.\"", card, StringComparison.Ordinal);
-        Assert.Contains("data-confirm-busy=\"Clearing the demo data… This takes about a minute. Keep this page open; it reloads when done.\"", card, StringComparison.Ordinal);
+        Assert.Contains("data-confirm-busy=\"Resetting the demo data… This takes a little over a minute. Keep this page open; it reloads when done.\"", card, StringComparison.Ordinal);
+        Assert.Contains("data-confirm-busy=\"Clearing the demo data… This takes about 20 seconds. Keep this page open; it reloads when done.\"", card, StringComparison.Ordinal);
         Assert.Contains("const busy = pendingButton?.dataset.confirmBusy;", script, StringComparison.Ordinal);
         Assert.Contains("confirmSubmit.disabled = true;", script, StringComparison.Ordinal);
         Assert.Contains("if (confirmSubmit?.disabled)", script, StringComparison.Ordinal);
     }
 
+    [Fact]
+    public void TheSeed_ShouldRunTheBatteriesSideBySide()
+    {
+        // On Atlas each database call waits for its round trip (about 1,200 calls: 138 s before this change and 76 s after it, with 45 ms each way). The batteries
+        // don't depend on each other, nor do the accounts on the product data, so they run side by side; each battery's
+        // own steps stay in order and every save still goes through the save rules (redesign R1, point 1).
+        var service = File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Demo", "DemoDataService.cs"));
+
+        Assert.Contains("var accountsSeeded = SeedAccountsAsync(catalogue, accountNotes, cancellationToken);", service, StringComparison.Ordinal);
+        Assert.Contains("var outcomes = await Task.WhenAll(catalogue.Packs.Select(demo => SeedPackAsync(", service, StringComparison.Ordinal);
+        Assert.Contains("private async Task<PackOutcome> SeedPackAsync(", service, StringComparison.Ordinal);
+    }
+
     private sealed class NoTempDataStorage : ITempDataProvider
     {
         public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoCardTests"`
Expected: `Failed:     2, Passed:     7` (failing: `DemoCardTests.TheDemoButtons_ShouldSayTheyAreWorkingOnceConfirmed`, `DemoCardTests.TheSeed_ShouldRunTheBatteriesSideBySide`).

- [ ] **Step 3: Write the code**

Patch `r1-task6-code.patch` (`web/Dpp/Demo/DemoDataService.cs`, `web/Views/Admin/Clusters.cshtml`). Save the block as `../r1-task6-code.patch`, outside the worktree, and run `git apply ../r1-task6-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index a7919f1..5726149 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -109,7 +109,9 @@ public sealed class DemoDataService(
             await MarkAccountRecordAsync("clusters", Builders<BsonDocument>.Filter.Eq("clusterId", cluster.Id), cancellationToken);
         }
 
-        var accounts = await SeedAccountsAsync(catalogue, notes, cancellationToken);
+        // The accounts don't depend on the product data, so they are made side by side with it (redesign R1).
+        var accountNotes = new List<string>();
+        var accountsSeeded = SeedAccountsAsync(catalogue, accountNotes, cancellationToken);
 
         var families = new Dictionary<string, string>(StringComparer.Ordinal);
         foreach (var family in catalogue.Families)
@@ -155,71 +157,16 @@ public sealed class DemoDataService(
             batches[batch.Key] = id;
         }
 
-        var published = 0;
-        var successors = 0;
-        foreach (var demo in catalogue.Packs)
-        {
-            var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form"), cancellationToken);
-            if (created.Pack is null)
-            {
-                var rejected = created.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key} ({key.Message})");
-                notes.Add($"Battery {demo.Serial} wasn't made: {created.Problem} {string.Join("; ", rejected)}".TrimEnd() + ".");
-                continue;
-            }
-
-            var packId = created.Pack.Id;
-            await MarkAsync(DppStore.Packs, packId, cancellationToken);
-            if (demo.Modules > 0)
-            {
-                await batteries.ImportModulesAsync(who, packId, ModuleExport(demo.Serial, demo.Modules), cancellationToken);
-            }
+        var accounts = await accountsSeeded;
+        notes.AddRange(accountNotes);
 
-            if (demo.Publish)
-            {
-                var result = await publication.PublishAsync(team, packId, cancellationToken);
-                if (result.Version is null)
-                {
-                    throw new InvalidOperationException($"The demo battery {demo.Serial} couldn't be published: {result.Problem ?? string.Join("; ", result.Report?.Blocking ?? [])}.");
-                }
-
-                published++;
-                if (demo.History)
-                {
-                    await WriteHistoryAsync(team, packId, demo, cancellationToken);
-                }
-
-                if (demo.NewVersion is not null)
-                {
-                    await SaveAsync(team, TargetKind.Pack, packId, demo.NewVersion, cancellationToken);
-                }
-            }
-
-            if (demo.Accident is not null)
-            {
-                await documents.AddAccidentReportAsync(team, packId, new DocumentUpload("accident-report.pdf", DemoPdf.Make("Accident report", demo.Accident), demo.Accident, "en-GB"), cancellationToken);
-            }
-
-            if (demo.Status is { } status && demo.Publish)
-            {
-                await ChangeStatusAsync(team, packId, demo, status, cancellationToken);
-                if (status.Successor is { } next)
-                {
-                    await MakeSuccessorAsync(team, packId, demo, next, cancellationToken);
-                    successors++;
-                    published++;
-                }
-            }
-
-            if (demo.Mark is { } mark && demo.Publish)
-            {
-                await MarkAsync(team, packId, demo, mark, catalogue, cancellationToken);
-            }
-
-            if (demo.HandOverTo is { } receiver && demo.Publish)
-            {
-                await HandOverAsync(packId, demo, receiver, catalogue, cancellationToken);
-            }
-        }
+        // The batteries don't depend on each other: on a remote database each call waits for its round trip, so they are
+        // made side by side, which about halves the reset (redesign R1, point 1). Each battery's own steps stay in order,
+        // and every save still goes through the save rules.
+        var outcomes = await Task.WhenAll(catalogue.Packs.Select(demo => SeedPackAsync(who, team, demo, batches, operators, catalogue, cancellationToken)));
+        var published = outcomes.Sum(outcome => outcome.Published);
+        var successors = outcomes.Sum(outcome => outcome.Successors);
+        notes.AddRange(outcomes.SelectMany(outcome => outcome.Notes));
 
         demoTokens.AddRange(await SeedTokensAsync(who, catalogue, cancellationToken));
 
@@ -409,6 +356,85 @@ public sealed class DemoDataService(
     }
 
     /// <summary>Seven days of live values every six hours, oldest first, through the API's own write path.</summary>
+    /// <summary>One demo battery's steps, in order: made, its modules, published with its history, its accident report, its
+    /// status change and new passport, its mark and its hand-over. Batteries are seeded side by side (redesign R1).</summary>
+    private async Task<PackOutcome> SeedPackAsync(
+        string who,
+        ChangeSender team,
+        DemoPack demo,
+        IReadOnlyDictionary<string, string> batches,
+        IReadOnlyDictionary<string, string> operators,
+        DemoCatalogue catalogue,
+        CancellationToken cancellationToken)
+    {
+        var published = 0;
+        var successors = 0;
+        var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form"), cancellationToken);
+        if (created.Pack is null)
+        {
+            var rejected = created.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key} ({key.Message})");
+            return new PackOutcome(0, 0, [$"Battery {demo.Serial} wasn't made: {created.Problem} {string.Join("; ", rejected)}".TrimEnd() + "."]);
+        }
+
+        var packId = created.Pack.Id;
+        await MarkAsync(DppStore.Packs, packId, cancellationToken);
+        if (demo.Modules > 0)
+        {
+            await batteries.ImportModulesAsync(who, packId, ModuleExport(demo.Serial, demo.Modules), cancellationToken);
+        }
+
+        if (demo.Publish)
+        {
+            var result = await publication.PublishAsync(team, packId, cancellationToken);
+            if (result.Version is null)
+            {
+                throw new InvalidOperationException($"The demo battery {demo.Serial} couldn't be published: {result.Problem ?? string.Join("; ", result.Report?.Blocking ?? [])}.");
+            }
+
+            published++;
+            if (demo.History)
+            {
+                await WriteHistoryAsync(team, packId, demo, cancellationToken);
+            }
+
+            if (demo.NewVersion is not null)
+            {
+                await SaveAsync(team, TargetKind.Pack, packId, demo.NewVersion, cancellationToken);
+            }
+        }
+
+        if (demo.Accident is not null)
+        {
+            await documents.AddAccidentReportAsync(team, packId, new DocumentUpload("accident-report.pdf", DemoPdf.Make("Accident report", demo.Accident), demo.Accident, "en-GB"), cancellationToken);
+        }
+
+        if (demo.Status is { } status && demo.Publish)
+        {
+            await ChangeStatusAsync(team, packId, demo, status, cancellationToken);
+            if (status.Successor is { } next)
+            {
+                await MakeSuccessorAsync(team, packId, demo, next, cancellationToken);
+                successors++;
+                published++;
+            }
+        }
+
+        if (demo.Mark is { } mark && demo.Publish)
+        {
+            await MarkAsync(team, packId, demo, mark, catalogue, cancellationToken);
+        }
+
+        if (demo.HandOverTo is { } receiver && demo.Publish)
+        {
+            await HandOverAsync(packId, demo, receiver, catalogue, cancellationToken);
+        }
+
+        return new PackOutcome(published, successors, []);
+    }
+
+    /// <summary>What seeding one battery made: published passports, new passports, and notes.</summary>
+    private sealed record PackOutcome(int Published, int Successors, IReadOnlyList<string> Notes);
+
     private async Task WriteHistoryAsync(ChangeSender team, string packId, DemoPack demo, CancellationToken cancellationToken)
     {
         var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken) ?? throw new InvalidOperationException($"The demo battery {demo.Serial} is missing.");
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
index f146e17..023ff06 100644
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -228,11 +228,11 @@
             <div class="d-flex flex-wrap gap-2">
                 <form method="post" action="/admin/demo-data/reset">
                     @Html.AntiForgeryToken()
-                    <button type="submit" class="bp-secondary-button" data-confirm-action data-confirm-title="Reset demo data" data-confirm-message="Remove the demo data and make it again? Real data stays as it is. The new API tokens are shown once." data-confirm-submit="Reset demo data" data-confirm-busy="Resetting the demo data… This takes about 2 minutes. Keep this page open; it reloads when done.">Reset demo data</button>
+                    <button type="submit" class="bp-secondary-button" data-confirm-action data-confirm-title="Reset demo data" data-confirm-message="Remove the demo data and make it again? Real data stays as it is. The new API tokens are shown once." data-confirm-submit="Reset demo data" data-confirm-busy="Resetting the demo data… This takes a little over a minute. Keep this page open; it reloads when done.">Reset demo data</button>
                 </form>
                 <form method="post" action="/admin/demo-data/clear">
                     @Html.AntiForgeryToken()
-                    <button type="submit" class="bp-danger-button" data-confirm-action data-confirm-title="Clear demo data" data-confirm-message="Remove the demo data? Real data stays as it is, and a demo record that real data uses is kept." data-confirm-submit="Clear demo data" data-confirm-busy="Clearing the demo data… This takes about a minute. Keep this page open; it reloads when done.">Clear demo data</button>
+                    <button type="submit" class="bp-danger-button" data-confirm-action data-confirm-title="Clear demo data" data-confirm-message="Remove the demo data? Real data stays as it is, and a demo record that real data uses is kept." data-confirm-submit="Clear demo data" data-confirm-busy="Clearing the demo data… This takes about 20 seconds. Keep this page open; it reloads when done.">Clear demo data</button>
                 </form>
             </div>
         </section>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoCardTests"`
Expected: `Passed!  - Failed:     0, Passed:     9`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2268`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCardTests.cs web/Dpp/Demo/DemoDataService.cs web/Views/Admin/Clusters.cshtml
git commit -m "perf(dpp): the demo seed makes the batteries side by side" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2268, Skipped:     0, Total:  2268`. From the 2255 before: 2257 after Task 1, 2259 after Task 2, 2265 after Task 3, 2266 after Task 4, 2267 after Task 5, 2268 after Task 6.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 4f65caa -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. The managing session brings these to the product owner:

1. **Restart the app with the usual launcher**, sign in with a team account and press "Reset demo data" at the end of the Clusters tab. The window says it's resetting and takes a little over a minute; the page reloads when done.
2. **DEMO-C7M-0001's page** (Batteries):
   - "Change status": no oval. The chosen option is outlined; on "Next", the save button stays at the window's bottom.
   - In "Accident reports", choose a PDF: "Ready to upload: … · … KB". Choose a ZIP: it says it isn't a PDF, PNG or JPEG.
3. **Search DEMO-C7M-0003** on the start page: its passport opens and says it was marked invalid. Its team page says why nothing changes, with no Save.

## Next

Plan R2 (a picture per battery model), then R3 (the two statuses as badges) and R4 (the side-menu battery pages).
