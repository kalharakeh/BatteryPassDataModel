# DPP V3 Step 6a-1: Tap Targets of at Least 24 px, and No Start Without a Database Name — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The first part of step 6, go-live readiness (design 14). It fixes the small tap targets that the R6b phone pass (3 Nov 2026) found and step 5's review found again (step 5 wrap-up, "Open follow-ups"):
- **`.bp-text-button`** (the header's "Change", "Suggest the fade", "Cancel the new passport…", "Remove its picture…") gets a box at least 24 px tall (WCAG 2.5.8). It was 19 px.
- **The header facts' links** (Batch, Economic operator on the team's battery page) get a box at least 24 px tall. They were 21 px.
- **The header's "Change" button keeps the facts row level.** Its 24 px box overhangs its label's line instead of growing it, so the Cluster value doesn't sit 5 px lower than the Batch and operator values.

- **The app refuses to start in Production without `MONGODB_DB`** (the managing session, 4 Nov 2026). Without it the app falls back to `battery_pass_demo`, the live app's database (design, "Where it runs"), so a deployment that forgets it would write the new app's data there. Development keeps today's fallback. A name given on purpose, `battery_pass_demo` included, still starts: the design keeps "reset the old database to match the new app" open as the no-extra-cost path.

The print style is the other half of 6a in the step 6 split. It waits for the product owner's answers (P1–P5) and gets its own plan.

**Architecture:**
- **Task 1, CSS only:** three rules in `web/wwwroot/css/bp-product-data.css`, and one new test class, `BatteryPassWeb.Tests/Look/TapTargetTests.cs` (3 tests), in the style of `PhoneWidthWrapTests`. No view and no `site.css` change.
- **Task 2:** `DeploymentSecurityConfiguration.MissingDatabaseName(isProduction, mongoDb)` returns why the app can't start, or null. `Program.cs` calls it right after `WebApplication.CreateBuilder` and throws `InvalidOperationException` with that message. It reads the environment variable itself, not the configuration, because `appsettings.json` names `battery_pass_demo` too. 8 new test cases in `DeploymentSecurityConfigurationTests`, which keeps its place in the test project.

**Tech Stack:** .NET 10, ASP.NET Core MVC, xUnit 2.9. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, section 14 (step 6). The finding is in the managing session's notes: `r6b-phone-pass.md` ("Minor, not changed: small tap targets") and `step5-wrap-up-draft.md` ("Open follow-ups").

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at 7717908 ("docs(dpp): step 5 wrap-up…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas or any database.
- **The owner's launcher must keep setting `MONGODB_DB`**, as it does today (design, "Where it runs"). After task 2, an app started in Production without it stops at once with the message below.
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Each task runs only its own tests. The whole suite runs once**, in the final checks. Never run two whole suites at once.
  - If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started, by their process IDs, never by name. Then run it again.
- **Baseline:** the whole suite shows `Passed:  2517` (step 5's result). After this plan: 2528.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6a-test.patch`). Then run `git apply ../<name>` from the worktree root. Both blocks were checked with `git apply --check` on the worktree at 7717908.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**
- **The old app is never touched:**
  - keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`;
  - never touch the running app on Elastic Beanstalk or its database `battery_pass_demo`.

## Found while prototyping

**Setup:**
- a scratch clone at 7717908, with my own app and a throwaway mongod, after a demo reset;
- Edge at 1280×900, and as a touch phone at 375×812;
- the pages: the team's and the cluster admin's battery pages of DEMO-C7M-0001, the detail page and the public summary.

**The rule:** a control under 24 px still passes WCAG 2.5.8 if a 24 px circle on its centre touches no other target ("spaced"). A link inside a sentence is exempt.

| Control | Before | After |
|---|---|---|
| Team page, 375: "Change" (cluster) | 43×19, **FAIL** (near "Mark as invalid or for deletion…") | 24 px tall, passes |
| Team page, 375: Batch link "2026 · Demo plant North" | 181×21, **FAIL** (near "Public page") | 24 px tall, passes |
| Team page, 375: Economic operator link | 244×21, **FAIL** (near the toolbar) | 24 px tall, passes |
| The same three at 1280 | small but spaced | 24 px tall |
| The breadcrumbs ("Batteries", "Registry"), 22 px; "More details", 21 px; the model link in the label, 15 px; the detail page's jump links ("Power ↓"), 19 px; "Accident information", 20 px | small but spaced, at both widths | unchanged (they pass) |

- **No overlaps added.** No two targets' boxes overlap after the change that didn't overlap before.
  - At 375 the check lists overlaps between the facts and the phone's action bar ("More", "Public page", …). They show with the old stylesheet too.
  - They come from the fixed bar and its closed menu, not from this change. Step 6c's role-matrix check looks at them with `elementFromPoint`.
- **The header stays level.** The facts' first text lines sit at 359, 359 and 360 px on the team page at 1280, and at 371 and 371 on the cluster admin's. Without the third rule, the Cluster value sat 5 px lower.
- **The header is 1 px shorter than before** at 1280 (305 px against 306), and the same height at 375.
  - Shots, in the managing session's notes: `step6a-shots/6a-taps-header-1280-before-after.png` and `…-375-before-after.png`.
  - The "before" shots were taken with the old stylesheet served, not with the new rules switched off.
- The script is `step6a-shots/taps6a.mjs`.
- **Where else the rules apply:**
  - The three other text buttons stand alone, not inside a sentence, so the inline-flex box changes no line breaks:
    - "Suggest the fade" (`_StatusDialog.cshtml`) is an item of `.bp-suggest`, which is already a flex row;
    - "Cancel the new passport (it was a mistake)" (`_StatusCard.cshtml`) and "Remove its picture and show the family's" (`Family.cshtml`) are alone in their forms.
  - `.bp-bh-facts dd a` matches only the team's header (Batch, Economic operator). The cluster admin's header and the frozen live values (`_LiveValuesCard.cshtml`) use `.bp-bh-facts` with no links.
- **Replayed on a clean clone:** both patches apply from this plan's text; 3 red, then green.
  - The clean clone also shows the two known `ApplicationAuditCoverageTests` failures: a clean checkout gives `site.css` CRLF line endings.
  - They pass in the worktree.

### Task 1: Tap targets of at least 24 px

**Files:**
- Create: `BatteryPassWeb.Tests/Look/TapTargetTests.cs`
- Modify: `web/wwwroot/css/bp-product-data.css` (three rules)

- [ ] **Step 1: Write the failing tests**

Save as `../6a-test.patch` and run `git apply ../6a-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Look/TapTargetTests.cs b/BatteryPassWeb.Tests/Look/TapTargetTests.cs
new file mode 100644
index 0000000..25e33e7
--- /dev/null
+++ b/BatteryPassWeb.Tests/Look/TapTargetTests.cs
@@ -0,0 +1,27 @@
+using BatteryPassWeb.Tests.Dpp;
+
+namespace BatteryPassWeb.Tests.Look;
+
+/// <summary>
+/// Step 6a, tap targets (WCAG 2.5.8, at least 24 px): on the team's battery page at 375 px the header's "Change" button
+/// (19 px) and the Batch and Economic operator links (21 px) sat too close to each other and to the toolbar to pass the
+/// rule's spacing exception (R6b phone pass, step 5's review). The text buttons and the header facts' links get a box at
+/// least 24 px tall, in bp-product-data.css (site.css unchanged).
+/// </summary>
+public sealed class TapTargetTests
+{
+    private static string Css() =>
+        File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")).Replace("\r\n", "\n");
+
+    [Fact]
+    public void TheTextButtons_ShouldBeAtLeast24PxTall() =>
+        Assert.Contains(".bp-text-button {\n  display: inline-flex;\n  align-items: center;\n  min-height: 24px;", Css(), StringComparison.Ordinal);
+
+    [Fact]
+    public void TheHeaderFactsLinks_ShouldBeAtLeast24PxTall() =>
+        Assert.Contains(".bp-bh-facts dd a {\n  display: inline-flex;\n  align-items: center;\n  min-height: 24px;\n}", Css(), StringComparison.Ordinal);
+
+    [Fact]
+    public void TheHeadersChangeButton_ShouldNotPushTheClusterValueDown() =>
+        Assert.Contains(".bp-bh-facts dt .bp-text-button {\n  margin-block: -4px;", Css(), StringComparison.Ordinal);
+}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental` then `dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~TapTargetTests"`
Expected: `Failed!  - Failed:     3, Passed:     0`.

- [ ] **Step 3: Add the three rules**

Save as `../6a-css.patch` and run `git apply ../6a-css.patch`:

```diff
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 141ed2c..399b084 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -1755,7 +1755,11 @@ main .bp-field-marker.is-published {
   gap: 6px;
 }
 
+/* At least 24 px tall, for WCAG 2.5.8 (step 6a). */
 .bp-text-button {
+  display: inline-flex;
+  align-items: center;
+  min-height: 24px;
   margin-left: 8px;
   padding: 0;
   border: 0;
@@ -3064,7 +3068,16 @@ main .bp-field-marker.is-published {
   white-space: nowrap;
 }
 
+/* The header facts' links (Batch, Economic operator) are at least 24 px tall too (step 6a). */
+.bp-bh-facts dd a {
+  display: inline-flex;
+  align-items: center;
+  min-height: 24px;
+}
+
+/* Its 24 px box overhangs its label's line instead of growing it, so the Cluster value stays level with the others. */
 .bp-bh-facts dt .bp-text-button {
+  margin-block: -4px;
   margin-left: 6px;
   font-size: 0.78rem;
 }
```

- [ ] **Step 4: Run the tests and the classes that read these rules**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~TapTargetTests|FullyQualifiedName~Look.|FullyQualifiedName~BatteryPageLayoutTests|FullyQualifiedName~EarlierPassportPageTests|FullyQualifiedName~ApplicationAuditCoverageTests|FullyQualifiedName~Step5ReviewTests|FullyQualifiedName~StatusWindowScrollTests"`
Expected: `Passed!  - Failed:     0, Passed:   124`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/TapTargetTests.cs web/wwwroot/css/bp-product-data.css
git commit -m "fix(dpp): text buttons and the header facts' links are at least 24 px tall (WCAG 2.5.8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: No start in Production without `MONGODB_DB`

**Files:**
- Modify: `BatteryPassWeb.Tests/DeploymentSecurityConfigurationTests.cs` (3 tests, 8 cases)
- Modify: `web/Configuration/DeploymentSecurityConfiguration.cs`
- Modify: `web/Program.cs`

**Found while prototyping** (the app built from the scratch clone, started with each setting; a throwaway mongod):

| Environment | `MONGODB_DB` | Result |
|---|---|---|
| Production | not set | stops at once: `Unhandled exception. System.InvalidOperationException: MONGODB_DB isn't set. …` |
| Production | set | `Now listening on …`, `Hosting environment: Production` |
| Development | not set | `Now listening on …`, `Hosting environment: Development` (today's fallback) |

No test boots the app; five test classes read `Program.cs` as text, and they pass with the change.

- [ ] **Step 1: Write the failing tests**

Save as `../6a-db-test.patch` and run `git apply ../6a-db-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/DeploymentSecurityConfigurationTests.cs b/BatteryPassWeb.Tests/DeploymentSecurityConfigurationTests.cs
index 7f09572..443edaa 100644
--- a/BatteryPassWeb.Tests/DeploymentSecurityConfigurationTests.cs
+++ b/BatteryPassWeb.Tests/DeploymentSecurityConfigurationTests.cs
@@ -23,4 +23,38 @@ public sealed class DeploymentSecurityConfigurationTests
     {
         Assert.False(DeploymentSecurityConfiguration.ParseRequireHttpsRedirection(value));
     }
+
+    // Step 6a: without MONGODB_DB the app falls back to battery_pass_demo, the live app's database ("Where it runs").
+    // In Production the name must be given; Development keeps today's fallback.
+    [Theory]
+    [InlineData(null)]
+    [InlineData("")]
+    [InlineData("   ")]
+    public void ProductionWithoutADatabaseName_ShouldRefuseToStart(string? mongoDb)
+    {
+        var message = DeploymentSecurityConfiguration.MissingDatabaseName(isProduction: true, mongoDb);
+
+        Assert.NotNull(message);
+        Assert.Contains("MONGODB_DB", message, StringComparison.Ordinal);
+        Assert.Contains("battery_pass_demo", message, StringComparison.Ordinal);
+    }
+
+    [Theory]
+    [InlineData(true, "battery_pass_v3")]
+    [InlineData(true, "battery_pass_demo")]
+    [InlineData(false, null)]
+    [InlineData(false, "")]
+    public void AGivenDatabaseName_OrDevelopment_ShouldStart(bool isProduction, string? mongoDb) =>
+        Assert.Null(DeploymentSecurityConfiguration.MissingDatabaseName(isProduction, mongoDb));
+
+    [Fact]
+    public void TheAppsStart_ShouldCheckTheDatabaseName()
+    {
+        var program = File.ReadAllText(BatteryPassWeb.Tests.Dpp.DppTestFiles.RepoFile("web", "Program.cs")).Replace("\r\n", "\n");
+
+        Assert.Contains(
+            "if (DeploymentSecurityConfiguration.MissingDatabaseName(builder.Environment.IsProduction(), Environment.GetEnvironmentVariable(\"MONGODB_DB\")) is { } missingDatabase)\n{\n    throw new InvalidOperationException(missingDatabase);\n}",
+            program,
+            StringComparison.Ordinal);
+    }
 }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`
Expected: the build fails with `error CS0117: 'DeploymentSecurityConfiguration' does not contain a definition for 'MissingDatabaseName'`.

- [ ] **Step 3: Add the check**

Save as `../6a-db-code.patch` and run `git apply ../6a-db-code.patch`:

```diff
diff --git a/web/Configuration/DeploymentSecurityConfiguration.cs b/web/Configuration/DeploymentSecurityConfiguration.cs
index a91c42e..340edbc 100644
--- a/web/Configuration/DeploymentSecurityConfiguration.cs
+++ b/web/Configuration/DeploymentSecurityConfiguration.cs
@@ -14,4 +14,13 @@ public static class DeploymentSecurityConfiguration
             && !normalized.Equals("0", StringComparison.OrdinalIgnoreCase)
             && !normalized.Equals("no", StringComparison.OrdinalIgnoreCase);
     }
+
+    /// <summary>
+    /// Why the app can't start, or null. Without MONGODB_DB the app falls back to battery_pass_demo, the live app's
+    /// database (design, "Where it runs"), so in Production the name must be given; Development keeps the fallback.
+    /// </summary>
+    public static string? MissingDatabaseName(bool isProduction, string? mongoDb) =>
+        isProduction && string.IsNullOrWhiteSpace(mongoDb)
+            ? "MONGODB_DB isn't set. In Production the app needs the database's name, so it never falls back to battery_pass_demo, the live app's database. Set MONGODB_DB to the database this app should use."
+            : null;
 }
diff --git a/web/Program.cs b/web/Program.cs
index c3d2c68..0561efb 100644
--- a/web/Program.cs
+++ b/web/Program.cs
@@ -20,6 +20,11 @@ DotEnvLoader.Load(Path.Combine(currentDirectory, "web", ".env.local"));
 
 var builder = WebApplication.CreateBuilder(args);
 
+if (DeploymentSecurityConfiguration.MissingDatabaseName(builder.Environment.IsProduction(), Environment.GetEnvironmentVariable("MONGODB_DB")) is { } missingDatabase)
+{
+    throw new InvalidOperationException(missingDatabase);
+}
+
 var enableRateLimiting = ParseFeatureFlag(
     EnvironmentOrConfig(builder.Configuration, "ENABLE_RATE_LIMITING", "EnableRateLimiting"),
     BatteryPassOptions.DefaultEnableRateLimiting);
```

- [ ] **Step 4: Run the tests and the classes that read `Program.cs`**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~DeploymentSecurityConfigurationTests|FullyQualifiedName~ApplicationAuditCoverageTests|FullyQualifiedName~AuthServiceDemoFallbackTests|FullyQualifiedName~DppServiceWiringTests|FullyQualifiedName~AdminFeedbackImplementationTests|FullyQualifiedName~DemoCatalogueTests"`
Expected: `Passed!  - Failed:     0, Passed:    45`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/DeploymentSecurityConfigurationTests.cs web/Configuration/DeploymentSecurityConfiguration.cs web/Program.cs
git commit -m "fix(dpp): in Production the app refuses to start without MONGODB_DB, never falling back to battery_pass_demo" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 3: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2528, Skipped:     0, Total:  2528`. That is the 2517 from before plus 11 new tests: 3 in task 1 and 8 in task 2.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 7717908 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

These are not steps of this plan. The managing session brings them to the product owner:

1. Restart the app with the usual launcher and sign in with a team account. Open Batteries, then DEMO-C7M-0001.
2. The header looks as before. "Change" next to Cluster and the Batch and Economic operator links are a little easier to hit. The Cluster value sits level with the others.
3. On a phone, or with the browser narrowed to a phone's width, open "More details" in the header and try each control:
   - "Change" opens the cluster window;
   - the Batch and Economic operator links open their pages;
   - none of these taps hits the toolbar instead.
4. Nothing else changes for the owner: the launcher already sets `MONGODB_DB`. If it ever didn't, the app would now stop at once with the message "MONGODB_DB isn't set…" instead of opening the live app's database.

## Next

- Step 6a's print style: its own plan, after the owner's answers P1–P5.
- Then 6b, the help pages and test pack.
