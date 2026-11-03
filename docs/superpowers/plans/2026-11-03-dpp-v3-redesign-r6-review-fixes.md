# DPP V3 Redesign R6: What the Review of the Built Pages Found — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Small, clear fixes from walking every page of the built R5 at 1280 px as each role, and from comparing it with the old app. Each task ends with a working app:

1. **The schema's codes in words** (task 1): the battery status (#22) reads "Re-used", not "re-used", like its badge; the category (#20) reads "Industrial, non-stationary battery"; a sentence naming a passport status says "marked for deletion", not "marked-for-deletion" (the Status section, and the refusals of a save or a new version). The passport and the API keep the schema's codes, as for #2 in R5.
2. **The passport hero's buttons with icons** (task 2): "‹ Back to summary", "View more about this passport ›" and the battery-page link carry the app's arrow icons instead of typed "<-" and "->".
3. **The battery page's tables fit** (task 3): at 1280 px the Modules and Versions tables were 174–210 px wider than the panel beside the side menu (`site.css` gives console tables a 1080 px minimum), hiding their right-hand columns behind a sideways scroll. The panel's tables drop the minimum; Versions shortens the passport ID in the middle (full on hover and in its copy button), shows the status as the Passport badge like the other lists, and lets "Published … by …" wrap.
4. **The Registry's search keeps clusters private** (task 4): it found batteries by their cluster's ID or name for every reader, so market surveillance, the Commission, notified bodies and the legitimate-interest roles could type a customer's name and learn which batteries are theirs, though the Cluster column is hidden from them. Only readers who may see clusters (the team, the cluster's members) find batteries by cluster now; everyone still finds a battery by its serial number, battery ID or passport ID. The old app refused it the same way ("Cluster search is not available for this role").
5. **The Registry's search says what it found** (task 5), "3 batteries match “DEMO-C7M”. Show all", like the team's list; an older passport version links its current version once (the line under the badges), not twice.
6. **One name for the battery page** (task 6): "Battery page" on the list icons, "Open the battery page" on the detail page (was "Manage this battery"), "open its battery page from its row below" after a hand-over (was "manage it from its row"). The team, which had no way back from the detail page, gets the same link to its own battery page.
7. **A battery's history says whose it is** (task 7): the History link opened the whole audit log searched by an internal ID; it now says "History of battery DEMO-C7M-0001 · Back to the battery page · The whole audit log". The old app's "Manage passports" address (`/admin/passports`) leads to the batteries instead of "page not found", as the cluster admins' one already did.

**Architecture:**
- `ValueWords.Lines` (task 1) reads the code inside the schema's small object for #2, #20 and #22 and says it as the badges do (`StatusBadge.Battery`, `DppStatuses.Words`, `ValueWords.CategoryWords`). `SavePipeline.NoLongerChanges`, `PublicationService.NoNewVersion` and the Status section use `DppStatuses.Words`.
- `bp-status.css` (task 2) gives the hero's buttons a gap for their icons; `bp-product-data.css` (task 3) drops the console tables' minimum inside `.bp-bpanel`.
- `BatteryPages.Matches` and `ListOnePerBatteryAsync` (task 4) take whether clusters may be searched; `RegistryController` passes `RegistryList.ShowsClusters(reader)`, the rule that already hides the Cluster column.
- `ClusterBatteryPages.ManageHrefAsync` (task 6) returns the team's battery page for the team. `AdminController.AuditLog` (task 7) sets `AuditBattery` when the search is a battery's pack ID, and `AdminController.Passports` redirects.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** the whole-page review of the built R5 at 1280 px (2–3 Nov 2026; as the team, a cluster admin, market surveillance and the public), with a side-by-side of the old app's views (tag `app-v1.2-before-dpp-v3`); the findings are in the managing session's notes. Decisions 36 and 37 stand; nothing here changes what anyone may see, except that the Registry's search stops telling readers who may not see clusters which batteries a customer has.

**Learning-mode contribution spot:** `ValueWords.CategoryWords` (task 1), how a category code reads; its test says what it must do.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 10b1e0d (R1–R5 and E1, as built). Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas** or `battery_pass_v3`. The tests use a throwaway MongoDB (EphemeralMongo).
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Each task runs only its own tests** (the filter in its steps); **the whole suite runs once**, in the final checks. Never run two whole suites at once.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started (by their process IDs, never by name) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2365`. After this plan: 2383. The suite takes about three minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../r6-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole; a removed file has its `git rm`.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.** The new styles go to `bp-product-data.css` (battery pages and lists) and `bp-status.css` (every page).
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## What happens to the old tests

- `ValueWordsTests` reads #20 and #22 in words (task 1).
- `OneRowPerBatteryTests` passes whether clusters may be searched (task 4).
- `AdminFeedbackFollowupTests` and `BatteryRouteAndRegistryTests` find the one "Open the current version" link, not the icon; `AdminFeedbackFollowupTests` also reads the lists' icons from `_RowActions` (task 5).
- `HandOverPagesTests` reads "open its battery page from its row below" (task 6).
- Nothing is removed.

---

### Task 1: The schema's codes in words

The battery status (#22) and the category (#20) read in words wherever a page shows them, as the badges say them; a sentence naming a passport status says it in words. The passport and the API keep the codes.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/StoredCodesInWordsTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs`
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Modify: `web/Dpp/Pages/StatusPages.cs`
- Modify: `web/Dpp/Pages/ValueWords.cs`
- Modify: `web/Dpp/Publication/PublicationService.cs`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs`). Save the block as `../r6-task1-tests.patch`, outside the worktree, and run `git apply ../r6-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs b/BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs
index 9d46277..663ad86 100644
--- a/BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs
@@ -13,8 +13,8 @@ public sealed class ValueWordsTests
     [InlineData(17, "\"2026-09\"", "September 2026")]
     [InlineData(19, "\"P8Y\"", "8 years")]
     [InlineData(19, "\"P1Y6M\"", "1 year 6 months")]
-    [InlineData(20, """{"batteryCategoryValue":"industrial/non-stationary battery"}""", "industrial/non-stationary battery")]
-    [InlineData(22, """{"batteryStatusValues":"original"}""", "original")]
+    [InlineData(20, """{"batteryCategoryValue":"industrial/non-stationary battery"}""", "Industrial, non-stationary battery")] // in words (R6)
+    [InlineData(22, """{"batteryStatusValues":"original"}""", "Original")] // the badge's words (R6)
     [InlineData(81, "12", "12 years")]
     [InlineData(82, "4000", "4,000")]
     [InlineData(63, """{"percentUnit":"%","percentageValue":76}""", "76 %")]
```

`BatteryPassWeb.Tests/Dpp/StoredCodesInWordsTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (the review of the built R5): a code from the schema's lists shows in words wherever a page shows it, as
/// the badges say it: the battery status (#22, "Re-used", not "re-used"), the category (#20) and, as since R5, the
/// passport status (#2); a sentence naming a passport status says "marked for deletion", not "marked-for-deletion".
/// </summary>
public sealed class StoredCodesInWordsTests
{
    private static string Shown(int number, JsonNode value) =>
        Assert.Single(ValueWords.Lines(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(number)), value)).Text;

    [Theory]
    [InlineData("original", "Original")]
    [InlineData("re-used", "Re-used")]
    [InlineData("waste", "Waste")]
    public void TheBatteryStatus_ShouldShowInTheBadgesWords(string code, string words) =>
        Assert.Equal(words, Shown(22, new JsonObject { ["batteryStatusValues"] = code })); // the schema's shape

    [Fact]
    public void TheCategory_ShouldReadAsWords() =>
        Assert.Equal("Industrial, non-stationary battery", Shown(20, new JsonObject { ["batteryCategoryValue"] = "industrial/non-stationary battery" }));

    [Fact]
    public void ThePassportStatus_ShouldStillShowInTheBadgesWords() =>
        Assert.Equal("Marked for deletion", Shown(2, new JsonObject { ["dppStatusValue"] = DppStatuses.MarkedForDeletion }));

    [Fact]
    public void ASentenceNamingAPassportStatus_ShouldSayItInWords()
    {
        Assert.Equal("the passport is marked for deletion; nothing on it changes", SavePipeline.NoLongerChanges(DppStatuses.MarkedForDeletion));
        Assert.Equal("the passport is marked for deletion, so it gets no new version", PublicationService.NoNewVersion(DppStatuses.MarkedForDeletion));
        Assert.Contains("The passport is {DppStatuses.Words(passportStatus!).ToLowerInvariant()}: nothing on it changes any more.",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Pages", "StatusPages.cs")), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StoredCodesInWordsTests | FullyQualifiedName~ValueWordsTests"`
Expected: `Failed:     7, Passed:    20` (failing: `StoredCodesInWordsTests.ASentenceNamingAPassportStatus_ShouldSayItInWords`, `StoredCodesInWordsTests.TheCategory_ShouldReadAsWords`).

- [ ] **Step 3: Write the code**

Patch `r6-task1-code.patch` (`web/Dpp/Editing/SavePipeline.cs`, `web/Dpp/Pages/StatusPages.cs`, `web/Dpp/Pages/ValueWords.cs`, `web/Dpp/Publication/PublicationService.cs`). Save the block as `../r6-task1-code.patch`, outside the worktree, and run `git apply ../r6-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Editing/SavePipeline.cs b/web/Dpp/Editing/SavePipeline.cs
index 6d8affb..b3b98fc 100644
--- a/web/Dpp/Editing/SavePipeline.cs
+++ b/web/Dpp/Editing/SavePipeline.cs
@@ -203,7 +203,7 @@ public sealed class SavePipeline(
     }
 
     /// <summary>Why a value of a passport that is no longer active is refused.</summary>
-    public static string NoLongerChanges(string status) => $"the passport is {status}; nothing on it changes";
+    public static string NoLongerChanges(string status) => $"the passport is {DppStatuses.Words(status).ToLowerInvariant()}; nothing on it changes";
 
     /// <summary>Why a pack's fixed value (#11, #17, #19) is refused after publication.</summary>
     public const string FixedOncePublished = "fixed once the passport is published";
diff --git a/web/Dpp/Pages/StatusPages.cs b/web/Dpp/Pages/StatusPages.cs
index c764f9d..19a0ea7 100644
--- a/web/Dpp/Pages/StatusPages.cs
+++ b/web/Dpp/Pages/StatusPages.cs
@@ -119,7 +119,7 @@ public sealed class StatusPages(DppStore store, RecordEditor editor, StatusChang
         var status = StatusChangeService.CurrentStatus(pack) ?? "original";
         var passportStatus = await store.PassportStatusAsync(pack.Id, cancellationToken);
         var successionView = await SuccessionAsync(pack, href, forTeam, cancellationToken);
-        var whyNot = DppStatuses.IsNoLongerActive(passportStatus) ? $"The passport is {passportStatus}: nothing on it changes any more."
+        var whyNot = DppStatuses.IsNoLongerActive(passportStatus) ? $"The passport is {DppStatuses.Words(passportStatus!).ToLowerInvariant()}: nothing on it changes any more."
             : !active ? "The status changes once the passport is published."
             : status == "waste" && !forTeam ? "The battery is waste, which is final."
             : successionView is not null && !forTeam ? "The battery is waiting for its new passport."
diff --git a/web/Dpp/Pages/ValueWords.cs b/web/Dpp/Pages/ValueWords.cs
index 0e59351..c93dbdd 100644
--- a/web/Dpp/Pages/ValueWords.cs
+++ b/web/Dpp/Pages/ValueWords.cs
@@ -21,6 +21,22 @@ public static class ValueWords
 {
     private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");
 
+    /// <summary>The battery category (#20) in words: "industrial/non-stationary battery" reads "Industrial, non-stationary battery".</summary>
+    public static string CategoryWords(string code)
+    {
+        ArgumentNullException.ThrowIfNull(code);
+        var words = code.Trim().Replace("/", ", ", StringComparison.Ordinal);
+        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
+    }
+
+    /// <summary>A coded value: the text itself, or the one text in the schema's small object around it.</summary>
+    private static string? Code(JsonNode? value) => value switch
+    {
+        JsonValue text when text.TryGetValue<string>(out var code) => code,
+        JsonObject holder => holder.Select(pair => pair.Value).OfType<JsonValue>().Select(inner => inner.TryGetValue<string>(out var code) ? code : null).FirstOrDefault(code => code is not null),
+        _ => null,
+    };
+
     public static IReadOnlyList<ValueLine> Lines(AttributeForm form, JsonNode? value)
     {
         ArgumentNullException.ThrowIfNull(form);
@@ -34,11 +50,17 @@ public static class ValueWords
             return [new ValueLine(null, Time(updated))];
         }
 
-        // The passport status (#2) in the badge's words ("Active"); the passport keeps the schema's value (redesign R5).
-        // The schema holds it as { "dppStatusValue": "active" }.
-        if (form.Attribute.Number == 2 && (value is JsonObject holder ? holder["dppStatusValue"] : value) is JsonValue status && status.TryGetValue<string>(out var stored))
+        // A code from the schema's lists in words: the passport status (#2, "Active"), the battery category (#20) and the
+        // battery status (#22, "Re-used"), as the badges say them (redesign R5 and R6). The passport keeps the schema's
+        // code; the schema holds each in a small object, e.g. { "dppStatusValue": "active" }.
+        if (form.Attribute.Number is 2 or 20 or 22 && Code(value) is { } code)
         {
-            return [new ValueLine(null, BatteryPassWeb.Dpp.Store.DppStatuses.Words(stored))];
+            return [new ValueLine(null, form.Attribute.Number switch
+            {
+                2 => BatteryPassWeb.Dpp.Store.DppStatuses.Words(code),
+                22 => StatusBadge.Battery(code)?.Words ?? code,
+                _ => CategoryWords(code),
+            })];
         }
 
         switch (form.Kind)
diff --git a/web/Dpp/Publication/PublicationService.cs b/web/Dpp/Publication/PublicationService.cs
index bb58123..266ea38 100644
--- a/web/Dpp/Publication/PublicationService.cs
+++ b/web/Dpp/Publication/PublicationService.cs
@@ -32,7 +32,7 @@ public sealed class PublicationService(
     public const string InactiveEvent = "dpp.passport-inactive";
 
     /// <summary>Why a passport that is no longer active gets no new version.</summary>
-    public static string NoNewVersion(string status) => $"the passport is {status}, so it gets no new version";
+    public static string NoNewVersion(string status) => $"the passport is {DppStatuses.Words(status).ToLowerInvariant()}, so it gets no new version";
 
     /// <summary>
     /// The checks the pack's passport would meet if it were published now, without publishing anything (the battery
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StoredCodesInWordsTests | FullyQualifiedName~ValueWordsTests"`
Expected: `Passed!  - Failed:     0, Passed:    27`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/StoredCodesInWordsTests.cs BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs web/Dpp/Editing/SavePipeline.cs web/Dpp/Pages/StatusPages.cs web/Dpp/Pages/ValueWords.cs web/Dpp/Publication/PublicationService.cs
git commit -m "fix(dpp): the schema's codes in words: battery status, category, passport status in sentences" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The passport hero's buttons with icons

"Back to summary", "View more about this passport" and the battery-page link carry the app's arrow icons instead of typed arrows.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs`
- Modify: `web/Views/Passport/_PassportHero.cshtml`
- Modify: `web/wwwroot/css/bp-status.css`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6 (the review of the built R5): the passport hero's buttons said "&lt;- Back to summary" and "View more about
/// this passport -&gt;" with typed arrows; they carry the app's arrow icons instead, like every other button.
/// </summary>
public sealed class HeroButtonIconsTests
{
    [Fact]
    public void TheHerosButtons_ShouldUseTheArrowIcons_NotTypedArrows()
    {
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-status.css"));

        Assert.DoesNotContain("&lt;-", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("-&gt;", hero, StringComparison.Ordinal);
        Assert.Contains("<use href=\"#i-back\" /></svg>Back to summary</a>", hero, StringComparison.Ordinal);
        Assert.Contains("View more about this passport<svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-arrow\" /></svg></a>", hero, StringComparison.Ordinal);
        Assert.Contains("Manage this battery<svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-arrow\" /></svg></a>", hero, StringComparison.Ordinal);
        Assert.Contains(".bp-report-hero-cta { gap: 8px; }", css, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HeroButtonIconsTests"`
Expected: `Failed:     1, Passed:     0` (failing: `HeroButtonIconsTests.TheHerosButtons_ShouldUseTheArrowIcons_NotTypedArrows`).

- [ ] **Step 3: Write the code**

Patch `r6-task2-code.patch` (`web/Views/Passport/_PassportHero.cshtml`, `web/wwwroot/css/bp-status.css`). Save the block as `../r6-task2-code.patch`, outside the worktree, and run `git apply ../r6-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index 123dfa3..f66a5ee 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -134,15 +134,15 @@
             <div class="bp-hero-ctas">
                 @if (detail)
                 {
-                    <a class="bp-report-hero-cta" href="@PassportPages.SummaryHref(Model.Address)">&lt;- Back to summary</a>
+                    <a class="bp-report-hero-cta" href="@PassportPages.SummaryHref(Model.Address)"><svg class="bp-i" aria-hidden="true"><use href="#i-back" /></svg>Back to summary</a>
                     @if (ViewData["ManageHref"] is string manageHref)
                     {
-                        <a class="bp-report-hero-cta" href="@manageHref">Manage this battery -&gt;</a>
+                        <a class="bp-report-hero-cta" href="@manageHref">Manage this battery<svg class="bp-i" aria-hidden="true"><use href="#i-arrow" /></svg></a>
                     }
                 }
                 else if (signedIn && Model.ReaderSeesMore)
                 {
-                    <a class="bp-report-hero-cta" href="@detailHref">View more about this passport -&gt;</a>
+                    <a class="bp-report-hero-cta" href="@detailHref">View more about this passport<svg class="bp-i" aria-hidden="true"><use href="#i-arrow" /></svg></a>
                 }
                 else if (signedIn)
                 {
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index 16b6895..8445da6 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -505,3 +505,7 @@ th .bp-status-help-words {
 .bp-live-chart small { color: var(--bp-muted); }
 .bp-live-all { margin-top: 16px; }
 .bp-live-all > summary { cursor: pointer; color: var(--bp-primary); font-weight: 600; }
+
+/* The passport hero's buttons carry the app's arrow icons, not typed arrows (redesign R6). */
+.bp-report-hero-cta { gap: 8px; }
+.bp-report-hero-cta .bp-i { width: 18px; height: 18px; flex: none; }
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HeroButtonIconsTests"`
Expected: `Passed!  - Failed:     0, Passed:     1`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs web/Views/Passport/_PassportHero.cshtml web/wwwroot/css/bp-status.css
git commit -m "fix(dpp): the passport hero's buttons with the app's arrow icons" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The battery page's tables fit their panel

At 1280 px the Modules and Versions tables were wider than the panel beside the side menu. The panel's tables drop the console minimum; Versions shortens the passport ID, shows the Passport badge and lets the publisher wrap.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/BatteryPanelTablesTests.cs`
- Modify: `web/Views/Batteries/_VersionsCard.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/BatteryPanelTablesTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6 (the review of the built R5): on the battery pages the Modules and Versions tables were 174–210 px wider
/// than the panel beside the side menu at 1280 px (site.css gives console tables a 1080 px minimum), so their right-hand
/// columns hid behind a sideways scroll. The panel's tables drop that minimum, Versions shortens the passport ID in the
/// middle (full on hover and in its copy button) and shows the status as the Passport badge, like the other lists.
/// </summary>
public sealed class BatteryPanelTablesTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void ThePanelsTables_ShouldNotKeepTheConsoleMinimumWidth() =>
        Assert.Contains(".bp-bpanel .bp-console-table {\n  min-width: 0;\n}\n\n.bp-bpanel .bp-console-table td[data-label=\"Published\"] {\n  white-space: normal;\n}", Read("web", "wwwroot", "css", "bp-product-data.css"), StringComparison.Ordinal);

    [Fact]
    public void Versions_ShouldShortenThePassportIdAndShowTheBadge()
    {
        var versions = Read("web", "Views", "Batteries", "_VersionsCard.cshtml");

        Assert.Contains("title=\"@row.Version.DigitalProductPassportId\">@BatteryPages.ShortId(row.Version.DigitalProductPassportId)</a>@await Html.PartialAsync(\"_CopyIdButton\", row.Version.DigitalProductPassportId)", versions, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_StatusBadge\", StatusBadge.Passport(row.Version.DppStatus))", versions, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPanelTablesTests"`
Expected: `Failed:     2, Passed:     0` (failing: `BatteryPanelTablesTests.ThePanelsTables_ShouldNotKeepTheConsoleMinimumWidth`, `BatteryPanelTablesTests.Versions_ShouldShortenThePassportIdAndShowTheBadge`).

- [ ] **Step 3: Write the code**

Patch `r6-task3-code.patch` (`web/Views/Batteries/_VersionsCard.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r6-task3-code.patch`, outside the worktree, and run `git apply ../r6-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Batteries/_VersionsCard.cshtml b/web/Views/Batteries/_VersionsCard.cshtml
index 7fdc432..9e11f77 100644
--- a/web/Views/Batteries/_VersionsCard.cshtml
+++ b/web/Views/Batteries/_VersionsCard.cshtml
@@ -28,8 +28,8 @@
                     {
                         <tr>
                             <td data-label="Version">@row.Version.Number</td>
-                            <td data-label="Passport ID"><span class="bp-id-copy-row"><a class="bp-id-value" href="/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail" target="_blank" rel="noreferrer">@row.Version.DigitalProductPassportId</a>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
-                            <td data-label="Status">@DppStatuses.Words(row.Version.DppStatus)</td>
+                            <td data-label="Passport ID"><span class="bp-id-copy-row"><a class="bp-id-value" href="/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail" target="_blank" rel="noreferrer" title="@row.Version.DigitalProductPassportId">@BatteryPages.ShortId(row.Version.DigitalProductPassportId)</a>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
+                            <td data-label="Status"><div class="bp-badges is-compact">@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(row.Version.DppStatus))</div></td>
                             <td data-label="Published">@ValueWords.Time(row.Version.PublishedAtUtc) <small class="bp-batteries-muted d-block">by @row.Version.PublishedBy</small></td>
                             <td data-label="Signature">
                                 @if (row.SignatureHolds)
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 700a34f..0460435 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3539,3 +3539,12 @@ main .bp-field-marker.is-published {
   color: var(--bp-primary);
 }
 
+/* The battery page's tables fit the panel beside the side menu (redesign R6): site.css gives console tables a
+   1080 px minimum, wider than the panel at 1280 px, which hid the right-hand columns behind a sideways scroll. */
+.bp-bpanel .bp-console-table {
+  min-width: 0;
+}
+
+.bp-bpanel .bp-console-table td[data-label="Published"] {
+  white-space: normal;
+}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPanelTablesTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/BatteryPanelTablesTests.cs web/Views/Batteries/_VersionsCard.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "fix(dpp): the battery page's tables fit their panel; Versions shows the status badge" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The Registry's search keeps clusters private

Only readers who may see clusters find batteries by a cluster's ID or name; the others find them by serial number, battery ID or passport ID, as the old app allowed.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/RegistryClusterSearchTests.cs`
- Modify: `web/Controllers/BatteriesController.cs`
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task4-tests.patch` (`BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs`). Save the block as `../r6-task4-tests.patch`, outside the worktree, and run `git apply ../r6-task4-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs b/BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs
index bf76aac..a59d66e 100644
--- a/BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs
@@ -26,9 +26,9 @@ public sealed class OneRowPerBatteryTests(MongoFixture mongo)
         var every = await pages.ListAsync(null);
         var oldRow = every.Rows.Single(row => row.PackId == rig.PackId);
 
-        var list = await pages.ListOnePerBatteryAsync(null, _ => true);
-        var byOldId = await pages.ListOnePerBatteryAsync(oldRow.Identifier, _ => true);
-        var byOldPassport = await pages.ListOnePerBatteryAsync(oldRow.Latest!.DigitalProductPassportId, _ => true);
+        var list = await pages.ListOnePerBatteryAsync(null, _ => true, matchClusters: true);
+        var byOldId = await pages.ListOnePerBatteryAsync(oldRow.Identifier, _ => true, matchClusters: true);
+        var byOldPassport = await pages.ListOnePerBatteryAsync(oldRow.Latest!.DigitalProductPassportId, _ => true, matchClusters: true);
 
         Assert.Equal(2, every.Rows.Count(row => row.SerialNumber == oldRow.SerialNumber)); // the API's list: a row per battery ID
         Assert.Equal(successorId, Assert.Single(list.Rows, row => row.SerialNumber == oldRow.SerialNumber).PackId);
@@ -46,8 +46,8 @@ public sealed class OneRowPerBatteryTests(MongoFixture mongo)
         var pages = BatteryTesting.Pages(rig);
         var surveillance = Reader.With("msa@example.com", ReaderRole.MarketSurveillance);
 
-        var team = await pages.ListOnePerBatteryAsync(null, _ => true);
-        var registry = await pages.ListOnePerBatteryAsync(null, row => RegistryList.MayList(surveillance, row));
+        var team = await pages.ListOnePerBatteryAsync(null, _ => true, matchClusters: true);
+        var registry = await pages.ListOnePerBatteryAsync(null, row => RegistryList.MayList(surveillance, row), matchClusters: false);
 
         Assert.Contains(team.Rows, row => row.PackId == successorId);
         Assert.DoesNotContain(team.Rows, row => row.PackId == rig.PackId);
@@ -84,8 +84,8 @@ public sealed class OneRowPerBatteryTests(MongoFixture mongo)
     [Fact]
     public void TheTeamsListAndTheRegistry_ShouldListOneRowPerBattery_AndTheApiEveryPack()
     {
-        Assert.Contains("pages.ListOnePerBatteryAsync(q, _ => true, cancellationToken)", View("Controllers", "BatteriesController.cs"), StringComparison.Ordinal);
-        Assert.Contains("batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), cancellationToken)", View("Controllers", "RegistryController.cs"), StringComparison.Ordinal);
+        Assert.Contains("pages.ListOnePerBatteryAsync(q, _ => true, matchClusters: true, cancellationToken)", View("Controllers", "BatteriesController.cs"), StringComparison.Ordinal);
+        Assert.Contains("batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), RegistryList.ShowsClusters(reader), cancellationToken)", View("Controllers", "RegistryController.cs"), StringComparison.Ordinal);
         Assert.Contains("await batteries.ListAsync(q, cancellationToken)", View("Controllers", "PassportsApiController.cs"), StringComparison.Ordinal);
         Assert.Contains("await batteries.ListAsync(null, cancellationToken)", View("Dpp", "Api", "PassportApi.cs"), StringComparison.Ordinal);
     }
```

`BatteryPassWeb.Tests/Dpp/RegistryClusterSearchTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (the review of the built R5): the Registry's search found batteries by their cluster's ID or name for every
/// reader, so market surveillance or a recycler could type a customer's name and learn which batteries are theirs, though
/// the Cluster column is hidden from them. Only readers who may see clusters (the team, the cluster's members) find
/// batteries by cluster now; the old app refused it the same way ("Cluster search is not available for this role").
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RegistryClusterSearchTests(MongoFixture mongo)
{
    [Fact]
    public async Task AReaderWhoMayNotSeeClusters_ShouldNotFindBatteriesByACluster()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);
        var surveillance = Reader.With("msa@example.com", ReaderRole.MarketSurveillance);
        var member = Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North);

        var bySurveillance = await pages.ListOnePerBatteryAsync(BatteryTesting.North, row => RegistryList.MayList(surveillance, row), RegistryList.ShowsClusters(surveillance));
        var byMember = await pages.ListOnePerBatteryAsync(BatteryTesting.North, row => RegistryList.MayList(member, row), RegistryList.ShowsClusters(member));
        var bySerial = await pages.ListOnePerBatteryAsync("VPC8C05", row => RegistryList.MayList(surveillance, row), RegistryList.ShowsClusters(surveillance));

        Assert.False(RegistryList.ShowsClusters(surveillance));
        Assert.Empty(bySurveillance.Rows);
        Assert.Single(byMember.Rows); // the cluster's own members still find their batteries by it
        Assert.Single(bySerial.Rows); // and everyone still finds a battery by its serial number
    }

    [Fact]
    public void TheRegistry_ShouldSearchClustersOnlyForThoseWhoSeeThem() =>
        Assert.Contains("batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), RegistryList.ShowsClusters(reader), cancellationToken)",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "RegistryController.cs")), StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~OneRowPerBatteryTests | FullyQualifiedName~RegistryClusterSearchTests"`
Expected: the build fails with CS1503, CS1739 (for example `error CS1503 Argument 3: cannot convert from 'bool' to 'System.Threading.CancellationToken'`).

- [ ] **Step 3: Write the code**

Patch `r6-task4-code.patch` (`web/Controllers/BatteriesController.cs`, `web/Controllers/RegistryController.cs`, `web/Dpp/Pages/BatteryPages.cs`). Save the block as `../r6-task4-code.patch`, outside the worktree, and run `git apply ../r6-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/BatteriesController.cs b/web/Controllers/BatteriesController.cs
index 76169bd..20c2b68 100644
--- a/web/Controllers/BatteriesController.cs
+++ b/web/Controllers/BatteriesController.cs
@@ -31,7 +31,7 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
 
     [HttpGet("")]
     public async Task<IActionResult> Batteries([FromQuery] string? q, CancellationToken cancellationToken) =>
-        View(await pages.ListOnePerBatteryAsync(q, _ => true, cancellationToken)); // one row per battery (redesign R5)
+        View(await pages.ListOnePerBatteryAsync(q, _ => true, matchClusters: true, cancellationToken)); // one row per battery (redesign R5)
 
     [HttpGet("new")]
     public async Task<IActionResult> NewBattery([FromQuery] string? batch, CancellationToken cancellationToken) =>
diff --git a/web/Controllers/RegistryController.cs b/web/Controllers/RegistryController.cs
index 73e0583..72386f6 100644
--- a/web/Controllers/RegistryController.cs
+++ b/web/Controllers/RegistryController.cs
@@ -21,7 +21,8 @@ public sealed class RegistryController(DppReaders readers, BatteryPages batterie
         var reader = await readers.ForAsync(User, cancellationToken);
         var editor = await readers.EditorForAsync(User, cancellationToken);
         // One row per battery, the newest passport this reader may see (redesign R5, owner's item 3).
-        var list = await batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), cancellationToken);
+        // A cluster is the team's and its members' business: only they find batteries by it (redesign R6).
+        var list = await batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), RegistryList.ShowsClusters(reader), cancellationToken);
         return View(RegistryList.Build(reader, list, editor) with
         {
             Waiting = editor is null ? [] : await handOvers.WaitingForAsync(editor, cancellationToken),
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 577eab4..643d341 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -239,14 +239,15 @@ public sealed class BatteryPages(
     /// Searching for an earlier passport's IDs finds the battery's row; its page links the earlier passport.
     /// </summary>
     /// <param name="mayList">The rows this reader may see (every row for the team; the Registry's rule for the others).</param>
-    public async Task<BatteriesPage> ListOnePerBatteryAsync(string? query, Func<BatteryRow, bool> mayList, CancellationToken cancellationToken = default)
+    /// <param name="matchClusters">Whether the search finds batteries by their cluster: only for readers who may see clusters (R6).</param>
+    public async Task<BatteriesPage> ListOnePerBatteryAsync(string? query, Func<BatteryRow, bool> mayList, bool matchClusters, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(mayList);
         var (rows, versions) = await RowsAsync(cancellationToken);
         var batteries = OnePerBattery(rows.Where(mayList).ToArray());
         var byPack = rows.ToDictionary(row => row.PackId, StringComparer.Ordinal);
         var wanted = query?.Trim() ?? string.Empty;
-        var shown = Sorted(batteries.Where(row => wanted.Length == 0 || Earlier(row, byPack).Prepend(row).Any(each => Matches(each, wanted, versions[each.PackId]))));
+        var shown = Sorted(batteries.Where(row => wanted.Length == 0 || Earlier(row, byPack).Prepend(row).Any(each => Matches(each, wanted, versions[each.PackId], matchClusters))));
         return new BatteriesPage(shown, wanted, batteries.Count);
     }
 
@@ -332,15 +333,17 @@ public sealed class BatteryPages(
     }
 
     /// <summary>True when the search text is part of the battery's passport IDs, battery ID, serial number or cluster.</summary>
-    public static bool Matches(BatteryRow row, string query, IEnumerable<PassportVersion> versions)
+    /// <param name="clusters">Whether the cluster counts: false for a reader who may not see clusters (redesign R6), or a
+    /// search by a customer's name would tell them which batteries are that customer's.</param>
+    public static bool Matches(BatteryRow row, string query, IEnumerable<PassportVersion> versions, bool clusters = true)
     {
         ArgumentNullException.ThrowIfNull(row);
         ArgumentNullException.ThrowIfNull(versions);
         bool Has(string? text) => text is not null && text.Contains(query, StringComparison.OrdinalIgnoreCase);
         return Has(row.SerialNumber)
             || Has(row.Identifier)
-            || Has(row.ClusterId)
-            || (row.ClusterId is not null && Has(row.ClusterLabel))
+            || (clusters && Has(row.ClusterId))
+            || (clusters && row.ClusterId is not null && Has(row.ClusterLabel))
             || versions.Any(version => Has(version.DigitalProductPassportId));
     }
 
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~OneRowPerBatteryTests | FullyQualifiedName~RegistryClusterSearchTests"`
Expected: `Passed!  - Failed:     0, Passed:     7`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/OneRowPerBatteryTests.cs BatteryPassWeb.Tests/Dpp/RegistryClusterSearchTests.cs web/Controllers/BatteriesController.cs web/Controllers/RegistryController.cs web/Dpp/Pages/BatteryPages.cs
git commit -m "fix(dpp): the Registry's search finds batteries by cluster only for readers who may see clusters" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The Registry's search says what it found

The Registry says how many batteries match the search and offers "Show all"; an older passport version links its current version once.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs`
- Modify: `web/Views/Passport/_PassportHero.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task5-tests.patch` (`BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`). Save the block as `../r6-task5-tests.patch`, outside the worktree, and run `git apply ../r6-task5-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
index f6b77a1..20cfe98 100644
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -21,7 +21,8 @@ public sealed class AdminFeedbackFollowupTests
         var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        var combined = string.Join(Environment.NewLine, registry, hero);
+        var rowActions = File.ReadAllText(RepoFile("web", "Views", "Shared", "_RowActions.cshtml")); // the lists' icons since R5
+        var combined = string.Join(Environment.NewLine, registry, hero, rowActions);
         Assert.Contains("bp-report-action", combined);
         Assert.Contains("View more about this passport", hero);
         Assert.Contains("Back to summary", hero);
@@ -46,7 +47,7 @@ public sealed class AdminFeedbackFollowupTests
         Assert.DoesNotContain("bp-report-action-label", hero);
         Assert.DoesNotContain(">Latest</span>", hero);
         Assert.DoesNotContain(">Detailed report</span>", hero);
-        Assert.Contains("bp-latest-passport-action-icon", hero);
+        Assert.Contains(">Open the current version</a>", hero); // one link, under the badges (R6)
         Assert.Contains(".bp-report-title-block", css);
         Assert.Contains(".bp-report-hero-cta", css);
         Assert.Contains("font-weight: 400;", css);
diff --git a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
index 9a76cbf..aad707e 100644
--- a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
+++ b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
@@ -38,8 +38,8 @@ public sealed class BatteryRouteAndRegistryTests
 
         Assert.Contains("<h1>@Model.Text(11)</h1>", hero);
         Assert.DoesNotContain(">Open latest passport<", hero);
-        Assert.Contains("aria-label=\"Open the current version\"", hero);
-        Assert.Contains("bp-latest-passport-action-icon", hero);
+        Assert.Contains("An older version of this passport. <a href=", hero); // one link to the current version (R6)
+        Assert.DoesNotContain("bp-latest-passport-action-icon", hero);
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs`:

```csharp
namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (the review of the built R5): the Registry's search said only "N batteries." with no way back to the whole
/// list; it now says what it matched and offers "Show all", like the team's list. An older passport version linked its
/// current version twice (an icon by the badges and a line under them); the line stays.
/// </summary>
public sealed class RegistrySearchWordsTests
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void TheRegistrysSearch_ShouldSayWhatItMatched_AndOfferShowAll()
    {
        var registry = View("Registry", "Index.cshtml");

        Assert.Contains("$\"{Words.Count(Model.Rows.Count, \"battery\", \"batteries\")} match “{Model.Query}”.\"", registry, StringComparison.Ordinal);
        Assert.Contains("$\"No battery you may open matches “{Model.Query}”.\"", registry, StringComparison.Ordinal);
        Assert.Equal(2, registry.Split("<a href=\"/registry\">Show all</a>").Length - 1);
    }

    [Fact]
    public void AnOlderVersion_ShouldLinkItsCurrentVersionOnce()
    {
        var hero = View("Passport", "_PassportHero.cshtml");

        Assert.Single(hero.Split(">Open the current version</a>").Skip(1));
        Assert.DoesNotContain("aria-label=\"Open the current version\"", hero, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminFeedbackFollowupTests | FullyQualifiedName~BatteryRouteAndRegistryTests | FullyQualifiedName~RegistrySearchWordsTests"`
Expected: `Failed:     3, Passed:    12` (failing: `BatteryRouteAndRegistryTests.PassportReports_ShouldUseSerialHeaderAndIconLatestAction`, `RegistrySearchWordsTests.AnOlderVersion_ShouldLinkItsCurrentVersionOnce`, `RegistrySearchWordsTests.TheRegistrysSearch_ShouldSayWhatItMatched_AndOfferShowAll`).

- [ ] **Step 3: Write the code**

Patch `r6-task5-code.patch` (`web/Views/Passport/_PassportHero.cshtml`, `web/Views/Registry/Index.cshtml`). Save the block as `../r6-task5-code.patch`, outside the worktree, and run `git apply ../r6-task5-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index f66a5ee..f46ed0a 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -41,19 +41,7 @@
                     {
                         <a class="bp-chip" href="@lineageHref"><svg class="bp-i" aria-hidden="true"><use href="#i-@(lineage.Pill == "Active" ? "history" : "arrow")" /></svg>@lineage.LinkText</a>
                     }
-                    @if (!Model.IsCurrent)
-                    {
-                        @if (Model.Current is not null)
-                        {
-                            <a href="@(detail ? PassportPages.DetailHref(Model.Identifier) : PassportPages.SummaryHref(Model.Identifier))" class="bp-report-action" aria-label="Open the current version" title="Open the current version" data-tooltip="Open the current version" data-action-icon="latest">
-                                <svg class="bp-report-action-icon bp-latest-passport-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                    <path d="M12 5a7 7 0 1 1-6.32 4" />
-                                    <path d="M5 5v4h4" />
-                                    <path d="M12 8v4l3 2" />
-                                </svg>
-                            </a>
-                        }
-                    }
+                    @* An older version links the current one once, in the line under the badges (redesign R6). *@
                     @if (Model.IsTest)
                     {
                         @* A tag, not a banner (owner's point 7); a tap or a hover explains it. *@
diff --git a/web/Views/Registry/Index.cshtml b/web/Views/Registry/Index.cshtml
index 336dab7..4c7e918 100644
--- a/web/Views/Registry/Index.cshtml
+++ b/web/Views/Registry/Index.cshtml
@@ -54,11 +54,18 @@
     <section class="bp-card bp-console-form-card bp-product-editor-section mt-4">
         @if (Model.Rows.Count == 0)
         {
-            <p class="mb-0">@(string.IsNullOrWhiteSpace(Model.Query) ? "No batteries to show." : "No battery you may open matches this search.")</p>
+            @* A search says what it looked for and how to see every battery again, like the team's list (redesign R6). *@
+            <p class="mb-0">@(string.IsNullOrWhiteSpace(Model.Query) ? "No batteries to show." : $"No battery you may open matches “{Model.Query}”.")@if (!string.IsNullOrWhiteSpace(Model.Query))
+                {
+                    <text> <a href="/registry">Show all</a></text>
+                }</p>
         }
         else
         {
-            <p class="text-secondary">@Words.Count(Model.Rows.Count, "battery", "batteries").</p>
+            <p class="text-secondary">@(string.IsNullOrWhiteSpace(Model.Query) ? $"{Words.Count(Model.Rows.Count, "battery", "batteries")}." : $"{Words.Count(Model.Rows.Count, "battery", "batteries")} match “{Model.Query}”.")@if (!string.IsNullOrWhiteSpace(Model.Query))
+                {
+                    <text> <a href="/registry">Show all</a></text>
+                }</p>
             <div class="bp-table-scroll">
                 <table class="table align-middle bp-console-table bp-mobile-card-table mb-0">
                     <thead>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminFeedbackFollowupTests | FullyQualifiedName~BatteryRouteAndRegistryTests | FullyQualifiedName~RegistrySearchWordsTests"`
Expected: `Passed!  - Failed:     0, Passed:    15`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs web/Views/Passport/_PassportHero.cshtml web/Views/Registry/Index.cshtml
git commit -m "fix(dpp): the Registry's search says what it matched and offers Show all; one link to the current version" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: One name for the battery page, and the team's way back

"Open the battery page" on the detail page and "open its battery page" after a hand-over, like the list icons' "Battery page"; the team gets the link to its own battery page.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/BatteryPageNameTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs`
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Modify: `web/Views/Passport/_PassportHero.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task6-tests.patch` (`BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`, `BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs`). Save the block as `../r6-task6-tests.patch`, outside the worktree, and run `git apply ../r6-task6-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs b/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
index baddce3..814fad0 100644
--- a/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
@@ -85,7 +85,7 @@ public sealed class HandOverPagesTests(MongoFixture mongo)
         var accepted = Assert.IsType<RedirectResult>(await receiver.Answer(asked.HandOver!.Id, accept: true, HandOverTests.Service(rig), CancellationToken.None));
 
         Assert.Equal("/registry", accepted.Url);
-        Assert.Equal("Accepted. The battery is now your cluster's; manage it from its row below.", receiver.TempData[ProductDataController.StatusKey]);
+        Assert.Equal("Accepted. The battery is now your cluster's; open its battery page from its row below.", receiver.TempData[ProductDataController.StatusKey]);
         var after = (RegistryPage)((ViewResult)await receiver.Index(null, HandOverTests.Service(rig), CancellationToken.None)).Model!;
         Assert.Empty(after.Waiting);
         Assert.NotNull(after.ManageHref(Assert.Single(after.Rows)));
diff --git a/BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs b/BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs
index b7508d2..b11b5d6 100644
--- a/BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs
+++ b/BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs
@@ -18,7 +18,7 @@ public sealed class HeroButtonIconsTests
         Assert.DoesNotContain("-&gt;", hero, StringComparison.Ordinal);
         Assert.Contains("<use href=\"#i-back\" /></svg>Back to summary</a>", hero, StringComparison.Ordinal);
         Assert.Contains("View more about this passport<svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-arrow\" /></svg></a>", hero, StringComparison.Ordinal);
-        Assert.Contains("Manage this battery<svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-arrow\" /></svg></a>", hero, StringComparison.Ordinal);
+        Assert.Contains("Open the battery page<svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-arrow\" /></svg></a>", hero, StringComparison.Ordinal); // one name since R6 T6
         Assert.Contains(".bp-report-hero-cta { gap: 8px; }", css, StringComparison.Ordinal);
     }
 }
```

`BatteryPassWeb.Tests/Dpp/BatteryPageNameTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (the review of the built R5): the battery page had three names ("Battery page" on the list icons, "Manage
/// this battery" on the detail page, "manage it from its row" after a hand-over); it is "the battery page" everywhere.
/// The team, which had no way back from the detail page, gets the same link to its own battery page.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryPageNameTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheDetailPage_ShouldLeadTheTeamToItsBatteryPage_AndTheClusterAdminToTheirs()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = ClusterBatteryPagesTests.Pages(rig);

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), await pages.ManageHrefAsync(ChangeSender.Team("team@example.com"), rig.PackId));
        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), await pages.ManageHrefAsync(ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId));
        Assert.Null(await pages.ManageHrefAsync(ChangeSender.Team("team@example.com"), "no-such-pack"));
        Assert.Null(await pages.ManageHrefAsync(null, rig.PackId));
    }

    [Fact]
    public void TheBatteryPage_ShouldHaveOneName()
    {
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "RegistryController.cs"));

        Assert.Contains(">Open the battery page<svg", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("Manage this battery", hero, StringComparison.Ordinal);
        Assert.Contains("open its battery page from its row below.", registry, StringComparison.Ordinal);
        Assert.Contains("data-tooltip=\"Battery page\"", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_RowActions.cshtml")), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPageNameTests | FullyQualifiedName~HandOverPagesTests | FullyQualifiedName~HeroButtonIconsTests"`
Expected: `Failed:     4, Passed:     7` (failing: `BatteryPageNameTests.TheBatteryPage_ShouldHaveOneName`, `BatteryPageNameTests.TheDetailPage_ShouldLeadTheTeamToItsBatteryPage_AndTheClusterAdminToTheirs`, `HandOverPagesTests.TheReceiversRegistry_ShouldShowItWaitingAndAcceptIt`, `HeroButtonIconsTests.TheHerosButtons_ShouldUseTheArrowIcons_NotTypedArrows`).

- [ ] **Step 3: Write the code**

Patch `r6-task6-code.patch` (`web/Controllers/RegistryController.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Views/Passport/_PassportHero.cshtml`). Save the block as `../r6-task6-code.patch`, outside the worktree, and run `git apply ../r6-task6-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/RegistryController.cs b/web/Controllers/RegistryController.cs
index 72386f6..b3fa23a 100644
--- a/web/Controllers/RegistryController.cs
+++ b/web/Controllers/RegistryController.cs
@@ -39,7 +39,7 @@ public sealed class RegistryController(DppReaders readers, BatteryPages batterie
         if (problem is null)
         {
             TempData[ProductDataController.StatusKey] = accept
-                ? "Accepted. The battery is now your cluster's; manage it from its row below."
+                ? "Accepted. The battery is now your cluster's; open its battery page from its row below."
                 : "Declined. The battery stays with the sending cluster.";
         }
         else
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index 0e734d3..85b2e18 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -78,12 +78,17 @@ public sealed class ClusterBatteryPages(
 {
     public static string BatteryHref(string packId) => $"/cluster-admin/batteries/{Uri.EscapeDataString(packId)}";
 
-    /// <summary>Where the detail page finds the "Manage this battery" link, for the battery's cluster admins only.</summary>
+    /// <summary>Where the detail page finds its "Open the battery page" link, for those who may change the battery.</summary>
     public const string ManageHrefKey = "ManageHref";
 
-    /// <summary>The battery's page, when the sender may manage it; null otherwise.</summary>
+    /// <summary>
+    /// The battery's page for this sender: the team's battery page for the team (redesign R6: the team had no way back
+    /// from the detail page), the cluster admin's for the battery's cluster admins; null for anyone else.
+    /// </summary>
     public async Task<string?> ManageHrefAsync(ChangeSender? sender, string packId, CancellationToken cancellationToken = default) =>
-        sender is not null && await ManagedAsync(sender, packId, cancellationToken) is not null ? BatteryHref(packId) : null;
+        sender is null ? null
+        : sender.Kind == SenderKind.Team ? (await store.GetAsync<BatteryPack>(packId, cancellationToken) is null ? null : BatteryPages.BatteryHref(packId))
+        : await ManagedAsync(sender, packId, cancellationToken) is not null ? BatteryHref(packId) : null;
 
     /// <summary>The battery's page; with <paramref name="live"/>, the live form as typed after a refused save. Null when the sender may not manage it.</summary>
     public async Task<ClusterBatteryPage?> BatteryAsync(ChangeSender sender, string packId, EditorState? live = null, CancellationToken cancellationToken = default)
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index f46ed0a..56249a9 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -125,7 +125,7 @@
                     <a class="bp-report-hero-cta" href="@PassportPages.SummaryHref(Model.Address)"><svg class="bp-i" aria-hidden="true"><use href="#i-back" /></svg>Back to summary</a>
                     @if (ViewData["ManageHref"] is string manageHref)
                     {
-                        <a class="bp-report-hero-cta" href="@manageHref">Manage this battery<svg class="bp-i" aria-hidden="true"><use href="#i-arrow" /></svg></a>
+                        <a class="bp-report-hero-cta" href="@manageHref">Open the battery page<svg class="bp-i" aria-hidden="true"><use href="#i-arrow" /></svg></a>
                     }
                 }
                 else if (signedIn && Model.ReaderSeesMore)
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPageNameTests | FullyQualifiedName~HandOverPagesTests | FullyQualifiedName~HeroButtonIconsTests"`
Expected: `Passed!  - Failed:     0, Passed:    11`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteryPageNameTests.cs BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs web/Controllers/RegistryController.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Views/Passport/_PassportHero.cshtml
git commit -m "fix(dpp): one name for the battery page, and the team's way back to it from the detail page" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: A battery's history says whose it is

Opened from a battery's History link, the audit log says "History of battery …" and leads back; the old "Manage passports" address leads to the batteries.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Views/Admin/AuditLog.cshtml`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs`:

```csharp
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Redesign R6 (the review of the built R5, the comparison with the old app): a battery's History link opened the whole
/// audit log searched by an internal ID, with nothing saying whose history it was; it now says "History of battery …" and
/// leads back to the battery page. The old app's "Manage passports" address (/admin/passports) leads to the batteries.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryHistoryLinkTests(MongoFixture mongo)
{
    [Fact]
    public async Task ABatterysHistory_ShouldSayWhoseItIs_AndLeadBack()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pack = (await rig.Store.ListAsync<BatteryPack>()).First();
        var serial = pack.Values["batterySerialNumber"].Value!.GetValue<string>();
        var controller = DemoCardTests.Controller(rig);
        var lines = rig.Services.GetRequiredService<AuditLines>();

        var history = Assert.IsType<ViewResult>(await controller.AuditLog(pack.Id, null, null, null, null, null, null, null, lines, CancellationToken.None));
        var battery = Assert.IsType<AuditBattery>(history.ViewData[AdminController.AuditBatteryKey]);
        var whole = Assert.IsType<ViewResult>(await DemoCardTests.Controller(rig).AuditLog("Repaired", null, null, null, null, null, null, null, lines, CancellationToken.None));

        Assert.Equal(new AuditBattery(serial, BatteryPages.BatteryHref(pack.Id)), battery);
        Assert.False(whole.ViewData.ContainsKey(AdminController.AuditBatteryKey)); // an ordinary search names no battery
    }

    [Fact]
    public async Task TheOldManagePassportsAddress_ShouldLeadToTheBatteries()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        Assert.Equal("/admin/batteries", Assert.IsType<RedirectResult>(DemoCardTests.Controller(rig).Passports()).Url);
    }

    [Fact]
    public void TheAuditLog_ShouldNameTheBatteryAndTheWaysBack() =>
        Assert.Contains("History of battery <strong>@battery.SerialNumber</strong> · <a href=\"@battery.Href\">Back to the battery page</a> · <a href=\"/admin/audit\">The whole audit log</a>",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "AuditLog.cshtml")), StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryHistoryLinkTests"`
Expected: the build fails with CS0117, CS0246, CS1061 (for example `error CS0117 'AdminController' does not contain a definition for 'AuditBatteryKey'`).

- [ ] **Step 3: Write the code**

Patch `r6-task7-code.patch` (`web/Controllers/AdminController.cs`, `web/Views/Admin/AuditLog.cshtml`). Save the block as `../r6-task7-code.patch`, outside the worktree, and run `git apply ../r6-task7-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
index 99bac3b..6cff0fa 100644
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -52,6 +52,13 @@ public class AdminController : Controller
     [HttpGet("help")]
     public IActionResult Help() => Redirect("/help/team");
 
+    /// <summary>Where the audit log finds the battery whose history it shows (redesign R6).</summary>
+    public const string AuditBatteryKey = "AuditBattery";
+
+    /// <summary>The old app's "Manage passports" address leads to the batteries (redesign R6; the cluster admins' one already does).</summary>
+    [HttpGet("passports")]
+    public IActionResult Passports() => Redirect("/admin/batteries");
+
     [HttpGet("audit")]
     public async Task<IActionResult> AuditLog(
         [FromQuery] string? q,
@@ -65,6 +72,14 @@ public class AdminController : Controller
         [FromServices] AuditLines auditLines,
         CancellationToken cancellationToken)
     {
+        // Opened from a battery's History link (q is its pack ID): say whose history it is, and lead back (redesign R6).
+        if (!string.IsNullOrWhiteSpace(q) && await _dppStore.GetAsync<BatteryPack>(q.Trim(), cancellationToken) is { } pack)
+        {
+            ViewData[AuditBatteryKey] = new AuditBattery(
+                pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? pack.Id,
+                BatteryPassWeb.Dpp.Pages.BatteryPages.BatteryHref(pack.Id));
+        }
+
         // 50 events at a time; one more is read to know whether "Show 50 more" has anything to show.
         const int PageSize = 50;
         var shown = Math.Clamp(show ?? PageSize, PageSize, 1000);
@@ -1033,3 +1048,6 @@ public class AdminController : Controller
     }
 
 }
+
+/// <summary>The battery whose history the audit log shows: its serial number and its page (redesign R6).</summary>
+public sealed record AuditBattery(string SerialNumber, string Href);
diff --git a/web/Views/Admin/AuditLog.cshtml b/web/Views/Admin/AuditLog.cshtml
index 95aaf36..c0d5ffa 100644
--- a/web/Views/Admin/AuditLog.cshtml
+++ b/web/Views/Admin/AuditLog.cshtml
@@ -33,6 +33,11 @@
         <symbol id="audit-i-chevron" viewBox="0 0 12 12"><path d="M4.5 2.5L8 6l-3.5 3.5" /></symbol>
     </svg>
 
+    @if (ViewData[BatteryPassWeb.Controllers.AdminController.AuditBatteryKey] is BatteryPassWeb.Controllers.AuditBattery battery)
+    {
+        @* Opened from a battery's History link: whose history this is, and the ways back (redesign R6). *@
+        <p class="bp-audit-battery mt-4 mb-0">History of battery <strong>@battery.SerialNumber</strong> · <a href="@battery.Href">Back to the battery page</a> · <a href="/admin/audit">The whole audit log</a></p>
+    }
     <section class="bp-card bp-console-form-card bp-product-editor-section mt-4">
         <form method="get" class="bp-auditlog-filters">
             <label class="bp-auditlog-search">
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryHistoryLinkTests"`
Expected: `Passed!  - Failed:     0, Passed:     3`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs web/Controllers/AdminController.cs web/Views/Admin/AuditLog.cshtml
git commit -m "fix(dpp): a battery's history says whose it is and leads back; the old Manage passports address leads on" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2383, Skipped:     0, Total:  2383` (18 more than the 2365 before).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 10b1e0d -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

1. **Restart the app with the usual launcher.**
2. **DEMO-C7M-0001's detail page:** the Purchaser tab says "Battery status: Re-used"; the hero says "Category: Industrial, non-stationary battery"; its buttons have arrow icons, and the team's says "Open the battery page".
3. **DEMO-C7M-0001 → Versions and Modules:** every column in view at 1280 px; the status as the Passport badge.
4. **DEMO-CORE-0003 → Status & repairs:** "The passport is marked for deletion: nothing on it changes any more."
5. **As the demo market surveillance account:** the Registry finds nothing for "Demo North", and finds DEMO-C7M-0001 by its serial number. **As the demo North admin:** "Demo North" still finds its batteries.
6. **The Registry, searched for "DEMO-C7M":** "3 batteries match “DEMO-C7M”. Show all".
7. **DEMO-C7M-0001 → History:** "History of battery DEMO-C7M-0001 · Back to the battery page · The whole audit log".

## Next

Step 5 resumes. The owner's open choices from the review (in the managing session's notes) may add their own small plan.
