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

The product owner then approved nine changes from the review (items 1–9) and three more (items 10–12):

8. **A Batteries tab for cluster admins** (task 8), first in their tabs, opening the Registry, which already lists their cluster's batteries.
9. **A confirm window before every removal that can't be undone** (task 9): a draft battery, a family, a model, a batch, a site, an operator. The app's own window (`_ConfirmModal`), as the clusters, users and API tokens already used; its code moves to `bp-confirm.js` so every page can load it.
10. **The team's list copies the full battery ID and says when** (task 10): the copy button beside the short battery ID, and "Version 3 · updated 2 Nov 2026, 14:05" under the Passport badge, as the Registry shows.
11. **Passport pages open in the same tab everywhere** (task 11): the battery pages' Public page and Detail page links, the Versions IDs, the cluster admin's link and the accident readers' link; files and the QR code still open a new tab.
12. **One name: "Public page"** (task 12), never "public summary"; "Back to the public page".
13. **The carbon footprint study's link in words** (task 13): "Carbon footprint study", its address on hover.
14. **The home page's sample is a published demo battery** (task 14): "Open a sample battery passport" opens one when the demo data is there; nothing shows when it isn't.
15. **The audit log's filter drops the old model's entries** (task 15): "Old passports", "Old batteries" and "Old passport files" are gone; their entries still show under "All".
16. **The cluster admin's History** (task 16): a read-only History section on their battery page, covering only the time the battery was in their cluster (a move from another cluster or a hand-over starts it; one away ends it). It never shows more than their battery page already shows: no other cluster's name or users (a move reads "Another cluster"), no Authorities values, no values before a change, no reasons for a mark, and the team, market surveillance and battery systems by role, not by name. The latest 50 lines.
17. **A window opened from More closes the menu** (task 17): the menu closes when its window opens, focus comes back to "More" when the window closes, and Escape closes an open menu.
18. **The Live values tab whenever there is a live value** (task 18): the tab was missing for two reasons. It showed only when the battery had a reading in the last 7 days (so every battery lost it a week after its last reading, the demo's a week after a reset); and only an active passport was given the battery's live values, so a passport marked invalid or for deletion (DEMO-C7M-0003, DEMO-CORE-0003) had none at all. A marked passport now keeps the battery's last live values, frozen by the mark (all of them are for the purchaser and up, so nothing new is public; the API's passport for a marked battery shows them too); an archived or replaced passport still has none. The tab shows to any reader allowed to see a live value whenever the battery has one: the latest value of each with its date, however old; a chart when the week has 2 or more readings; "No reading in the last 7 days" otherwise.
19. **Passports of this battery** (task 19): a battery placed on the market again lists its passports oldest first (1 → 2 → 3, the newest marked), each with its battery ID, status, from–to dates and a link. The team sees all of them, with each one's battery page and history; the detail page shows the list to the team and market surveillance, in its Passport tab; the cluster admin sees public facts only (each passport's public page, never another cluster's name, users or purchaser data); public readers keep "replaces an earlier passport". A battery's history lists its other passports' histories.
20. **A battery's third passport** (task 20): a battery placed on the market twice couldn't get its third passport ("another battery already has this serial number"), because only a passport's direct neighbours could share its serial number. Every passport of the chain may now; an unrelated battery still may not.
21. **The demo shows a chain of three** (task 21): DEMO-C13M-0001's new passport is remanufactured in turn and gets a third one (warranty P5Y). The demo has 10 batteries, 7 of them published.

**Architecture:**
- `ValueWords.Lines` (task 1) reads the code inside the schema's small object for #2, #20 and #22 and says it as the badges do (`StatusBadge.Battery`, `DppStatuses.Words`, `ValueWords.CategoryWords`). `SavePipeline.NoLongerChanges`, `PublicationService.NoNewVersion` and the Status section use `DppStatuses.Words`.
- `bp-status.css` (task 2) gives the hero's buttons a gap for their icons; `bp-product-data.css` (task 3) drops the console tables' minimum inside `.bp-bpanel`.
- `BatteryPages.Matches` and `ListOnePerBatteryAsync` (task 4) take whether clusters may be searched; `RegistryController` passes `RegistryList.ShowsClusters(reader)`, the rule that already hides the Cluster column.
- `ClusterBatteryPages.ManageHrefAsync` (task 6) returns the team's battery page for the team. `AdminController.AuditLog` (task 7) sets `AuditBattery` when the search is a battery's pack ID, and `AdminController.Passports` redirects.
- `_ClusterAdminTabs` (task 8) gains the Batteries tab. `bp-confirm.js` (task 9) holds the confirm window's code, moved from `bp-admin-pages.js`; a removal button carries `data-confirm-action` and its words.
- `ValueWords.LinkWords` (task 13) names a single link value (#36). `HomeController.Index` (task 14) asks `BatteryPages.ExampleBatteryIdAsync` for the sample.
- `ClusterHistory` (task 16, new, in `Dpp/Audit`) reads a battery's audit timeline (`ApplicationAuditService.ListEntityTimelineAsync`), walks the cluster moves back from the current cluster to find the cluster's own time, keeps only the events it can describe, and writes each line with the actor by role and no before-values. `ClusterBatteryPages` adds it to the cluster admin's page.
- `bp-record-editor.js` (task 17) closes a `details.bp-more` menu when a window opens from it and on Escape.
- `LiveTrends.ForTab` (task 18) builds the Live values tab (`LiveTab`) from the reader's latest live values and the week's readings; `PassportReader` gives the battery's live values to its newest passport, active or marked (`IsNewest`), not only an active one.
- `PassportChains` (task 19, new) lists a battery's passports through `SuccessorOf` and `Succession`; `ChainView` decides what each reader sees (`ChainReader.Team`, `Detail`, `ClusterAdmin`); `_PassportChain` shows it. `DppStore.ChainPackIdsAsync` (task 20) gives the whole chain, which `SavePipeline` and `PackCreationService` let share the serial number.
- `DemoSuccessor.Then` (task 21) places a demo battery's new passport on the market again in turn.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** the whole-page review of the built R5 at 1280 px (2–3 Nov 2026; as the team, a cluster admin, market surveillance and the public), with a side-by-side of the old app's views (tag `app-v1.2-before-dpp-v3`); the findings are in the managing session's notes; then the product owner's approval of items 1–12 (3 Nov 2026). Decisions 36 and 37 stand. What anyone may see changes only where the owner chose: the Registry's search stops telling readers who may not see clusters which batteries a customer has (task 4); a cluster admin sees their own time of a battery's history (task 16) and the public facts of its other passports (task 19); market surveillance sees a battery's passports on the detail page (task 19). Nothing new is public, and #22 stays out of anything public.

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
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2365`. After this plan: 2424. The suite takes about three minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../r6-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole; a removed file has its `git rm`.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.** The new styles go to `bp-product-data.css` (battery pages and lists) and `bp-status.css` (every page).
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## What happens to the old tests

- `ValueWordsTests` reads #20 and #22 in words (task 1).
- `OneRowPerBatteryTests` passes whether clusters may be searched (task 4).
- `AdminFeedbackFollowupTests` and `BatteryRouteAndRegistryTests` find the one "Open the current version" link, not the icon; `AdminFeedbackFollowupTests` also reads the lists' icons from `_RowActions` (task 5).
- `HandOverPagesTests` reads "open its battery page from its row below" (task 6).
- `ClusterAdminPagesTests` reads the Batteries tab first (task 8).
- `DemoCardTests` and `ClustersTabTests` read the confirm window's code from `bp-confirm.js` (task 9).
- `AccidentSectionTests` reads the accident readers' link without a new tab (task 11).
- `AdminFeedbackFollowupTests`, `ModelPictureTests` and `HeroButtonIconsTests` read "Public page" and "Back to the public page" (task 12).
- `PassportControllerTests` checks the home page has no fixed sample passport ID (task 14).
- `BatteryPageLayoutTests` and `BatterySectionsTests` count the cluster admin's History section (task 16).
- `LiveChartsTests` builds the tab through `LiveTrends.ForTab` (task 18).
- `BatteryHistoryLinkTests` checks a battery with one passport lists no others (task 19).
- `DemoSeedTests`, `DemoClearTests` and `DemoCardTests` count 10 batteries, 7 published, 12 versions and the chain of three (task 21).
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

### Task 8: A Batteries tab for cluster admins

The cluster admin's tabs start with Batteries, which opens the Registry: their cluster's batteries.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/ClusterAdminBatteriesTabTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs`
- Modify: `web/Views/Shared/_ClusterAdminTabs.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task8-tests.patch` (`BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs`). Save the block as `../r6-task8-tests.patch`, outside the worktree, and run `git apply ../r6-task8-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs b/BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs
index 81bc455..a2a6baa 100644
--- a/BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs
+++ b/BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs
@@ -17,7 +17,7 @@ public sealed class ClusterAdminPagesTests
     public void Heading_ShouldNameTheClustersAndTheTabs()
     {
         Assert.Contains("<h1>Cluster admin</h1>", Tabs, StringComparison.Ordinal);
-        Assert.Contains("<p>Users and API tokens for @(string.IsNullOrWhiteSpace(scope) ? \"the clusters you run\" : scope).</p>", Tabs, StringComparison.Ordinal);
+        Assert.Contains("<p>Batteries, users and API tokens for @(string.IsNullOrWhiteSpace(scope) ? \"the clusters you run\" : scope).</p>", Tabs, StringComparison.Ordinal); // a Batteries tab since R6
         Assert.Contains("href=\"/cluster-admin/users\">Users</a>", Tabs, StringComparison.Ordinal);
         Assert.Contains("href=\"/cluster-admin/api-tokens\">API tokens</a>", Tabs, StringComparison.Ordinal);
         foreach (var page in new[] { Users, Tokens })
```

`BatteryPassWeb.Tests/Look/ClusterAdminBatteriesTabTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 1 (3 Nov 2026): cluster admins had no tab for their batteries (the old app had
/// "Managed passports"); a Batteries tab, first, opens the Registry, where their batteries are (decision 30).
/// </summary>
public sealed class ClusterAdminBatteriesTabTests
{
    [Fact]
    public void TheClusterAdminTabs_ShouldStartWithBatteries_OpeningTheRegistry()
    {
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_ClusterAdminTabs.cshtml"));

        Assert.Contains("<a class=\"@TabClass(\"batteries\")\" href=\"/registry\">Batteries</a>", tabs, StringComparison.Ordinal);
        Assert.Contains("<option value=\"/registry\">Batteries</option>", tabs, StringComparison.Ordinal); // the phone's section picker too
        Assert.True(tabs.IndexOf(">Batteries</a>", StringComparison.Ordinal) < tabs.IndexOf(">Users</a>", StringComparison.Ordinal));
        Assert.Contains("Batteries, users and API tokens for", tabs, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~ClusterAdminBatteriesTabTests | FullyQualifiedName~ClusterAdminPagesTests"`
Expected: `Failed:     2, Passed:     4` (failing: `ClusterAdminBatteriesTabTests.TheClusterAdminTabs_ShouldStartWithBatteries_OpeningTheRegistry`, `ClusterAdminPagesTests.Heading_ShouldNameTheClustersAndTheTabs`).

- [ ] **Step 3: Write the code**

Patch `r6-task8-code.patch` (`web/Views/Shared/_ClusterAdminTabs.cshtml`). Save the block as `../r6-task8-code.patch`, outside the worktree, and run `git apply ../r6-task8-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Shared/_ClusterAdminTabs.cshtml b/web/Views/Shared/_ClusterAdminTabs.cshtml
index 1b73add..6b6214a 100644
--- a/web/Views/Shared/_ClusterAdminTabs.cshtml
+++ b/web/Views/Shared/_ClusterAdminTabs.cshtml
@@ -9,12 +9,13 @@
 
 <div class="bp-admin-heading">
     <h1>Cluster admin</h1>
-    <p>Users and API tokens for @(string.IsNullOrWhiteSpace(scope) ? "the clusters you run" : scope).</p>
+    <p>Batteries, users and API tokens for @(string.IsNullOrWhiteSpace(scope) ? "the clusters you run" : scope).</p>
 </div>
 
 <form class="bp-admin-mobile-tab-form mt-4" aria-label="Cluster administration section">
     <label for="bp-cluster-admin-mobile-tab-select">Cluster admin section</label>
     <select id="bp-cluster-admin-mobile-tab-select" data-admin-mobile-tab-select>
+        <option value="/registry">Batteries</option>
         <option value="/cluster-admin/users" selected="@IsSelected("users")">Users</option>
         <option value="/cluster-admin/api-tokens" selected="@IsSelected("api-tokens")">API tokens</option>
     </select>
@@ -22,6 +23,8 @@
 
 <nav class="bp-local-admin-nav" aria-label="Local admin tabs">
     <div class="bp-tab-row">
+        @* The cluster's batteries are in the Registry (decision 30); the tab leads there, as the old app's own tab did (R6). *@
+        <a class="@TabClass("batteries")" href="/registry">Batteries</a>
         <a class="@TabClass("users")" href="/cluster-admin/users">Users</a>
         <a class="@TabClass("api-tokens")" href="/cluster-admin/api-tokens">API tokens</a>
     </div>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~ClusterAdminBatteriesTabTests | FullyQualifiedName~ClusterAdminPagesTests"`
Expected: `Passed!  - Failed:     0, Passed:     6`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/ClusterAdminBatteriesTabTests.cs BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs web/Views/Shared/_ClusterAdminTabs.cshtml
git commit -m "feat(dpp): a Batteries tab for cluster admins, opening the Registry" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: A confirm window before every removal that can't be undone

Removing a draft battery, a family, a model, a batch, a site or an operator first asks in the app's own window. Its code moves to `bp-confirm.js`, which every page with a removal loads.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Look/ClustersTabTests.cs`
- Test (create): `BatteryPassWeb.Tests/Look/ConfirmRemovalsTests.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/ClusterAdmin/ApiTokens.cshtml`
- Modify: `web/Views/ClusterAdmin/Users.cshtml`
- Modify: `web/Views/ProductData/Batch.cshtml`
- Modify: `web/Views/ProductData/Family.cshtml`
- Modify: `web/Views/ProductData/SitesAndOperators.cshtml`
- Modify: `web/Views/ProductData/_RecordEditor.cshtml`
- Modify: `web/wwwroot/js/bp-admin-pages.js`
- Create: `web/wwwroot/js/bp-confirm.js`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task9-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoCardTests.cs`, `BatteryPassWeb.Tests/Look/ClustersTabTests.cs`). Save the block as `../r6-task9-tests.patch`, outside the worktree, and run `git apply ../r6-task9-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
index c1b2584..7ae4411 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
@@ -145,7 +145,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
     {
         var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
         var card = page[page.IndexOf("data-demo-data-card", StringComparison.Ordinal)..page.IndexOf("@if (selectedTab == \"users\")", StringComparison.Ordinal)];
-        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-admin-pages.js"));
+        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-confirm.js")); // the confirm window's own script since R6
 
         Assert.Equal(2, card.Split("data-confirm-busy=\"").Length - 1);
         Assert.Contains("data-confirm-busy=\"Resetting the demo data… This takes a little over a minute. Keep this page open; it reloads when done.\"", card, StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Look/ClustersTabTests.cs b/BatteryPassWeb.Tests/Look/ClustersTabTests.cs
index eb6e9ee..35833e3 100644
--- a/BatteryPassWeb.Tests/Look/ClustersTabTests.cs
+++ b/BatteryPassWeb.Tests/Look/ClustersTabTests.cs
@@ -87,7 +87,8 @@ public sealed class ClustersTabTests(MongoFixture mongo)
         Assert.DoesNotContain("data-confirm-modal hidden", page, StringComparison.Ordinal);
         Assert.Contains("document.querySelectorAll('[data-open-dialog]')", script, StringComparison.Ordinal);
         Assert.Contains("document.querySelectorAll('dialog[data-open-on-load=\"true\"]')", script, StringComparison.Ordinal);
-        Assert.Contains("document.querySelectorAll('[data-confirm-action]')", script, StringComparison.Ordinal);
+        Assert.Contains("document.querySelectorAll('[data-confirm-action]')", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-confirm.js")), StringComparison.Ordinal); // its own script since R6
+        Assert.Contains("<script src=\"~/js/bp-confirm.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
         Assert.Contains("data-confirm-modal hidden", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_ConfirmModal.cshtml")), StringComparison.Ordinal);
     }
 
```

`BatteryPassWeb.Tests/Look/ConfirmRemovalsTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 2 (3 Nov 2026): removing a draft battery, a family, a model, a batch, a site or an
/// operator can't be undone, and happened on one click once the fold was open; each now asks first in the app's
/// "Please confirm" window, as deleting a cluster, an account or a token already did. The window's script stands on its
/// own (bp-confirm.js), so these pages don't take the admin pages' other behaviour.
/// </summary>
public sealed class ConfirmRemovalsTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Theory]
    [InlineData("Batteries", "Battery.cshtml", "Remove draft battery</button>")]
    [InlineData("ProductData", "Family.cshtml", "Remove battery family</button>")]
    [InlineData("ProductData", "_RecordEditor.cshtml", "Remove @kindWord</button>")]
    public void EachRemoval_ShouldAskFirst(string folder, string view, string button)
    {
        var page = Read("web", "Views", folder, view);
        var tag = page[page.LastIndexOf("<button", page.IndexOf(button, StringComparison.Ordinal), StringComparison.Ordinal)..page.IndexOf(button, StringComparison.Ordinal)];

        Assert.Contains("data-confirm-action data-confirm-title=", tag, StringComparison.Ordinal);
        Assert.Contains("It can't be undone.", tag, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Batteries", "Battery.cshtml")]
    [InlineData("ProductData", "Family.cshtml")]
    [InlineData("ProductData", "Batch.cshtml")]
    [InlineData("ProductData", "SitesAndOperators.cshtml")]
    public void EveryPageWithARemoval_ShouldHaveTheWindowAndItsScript(string folder, string view)
    {
        var page = Read("web", "Views", folder, view);

        Assert.Contains("@await Html.PartialAsync(\"_ConfirmModal\")", page, StringComparison.Ordinal);
        Assert.Contains("<script src=\"~/js/bp-confirm.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("bp-admin-pages.js", page, StringComparison.Ordinal); // its dialogs would open twice beside the page's own
    }

    [Fact]
    public void TheWindowsScript_ShouldStandOnItsOwn_AndFindOnlyTheSharedWindow()
    {
        var confirm = Read("web", "wwwroot", "js", "bp-confirm.js");
        var admin = Read("web", "wwwroot", "js", "bp-admin-pages.js");

        Assert.Contains("document.querySelector('.bp-confirm-modal[data-confirm-modal]')", confirm, StringComparison.Ordinal); // not the editor's "Keep editing" window
        Assert.DoesNotContain("data-confirm-action", admin, StringComparison.Ordinal);
        foreach (var page in new[] { Read("web", "Views", "Admin", "Clusters.cshtml"), Read("web", "Views", "ClusterAdmin", "Users.cshtml"), Read("web", "Views", "ClusterAdmin", "ApiTokens.cshtml") })
        {
            Assert.Contains("<script src=\"~/js/bp-confirm.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoCardTests | FullyQualifiedName~ClustersTabTests | FullyQualifiedName~ConfirmRemovalsTests"`
Expected: `Failed:    10, Passed:    13` (failing: `DemoCardTests.TheDemoButtons_ShouldSayTheyAreWorkingOnceConfirmed`, `ClustersTabTests.ThePage_ShouldUseTheSharedScriptAndConfirmWindow`, `ConfirmRemovalsTests.TheWindowsScript_ShouldStandOnItsOwn_AndFindOnlyTheSharedWindow`).

- [ ] **Step 3: Write the code**

Patch `r6-task9-code.patch` (`web/Views/Admin/Clusters.cshtml`, `web/Views/Batteries/Battery.cshtml`, `web/Views/ClusterAdmin/ApiTokens.cshtml`, `web/Views/ClusterAdmin/Users.cshtml`, `web/Views/ProductData/Batch.cshtml`, `web/Views/ProductData/Family.cshtml`, `web/Views/ProductData/SitesAndOperators.cshtml`, `web/Views/ProductData/_RecordEditor.cshtml`, `web/wwwroot/js/bp-admin-pages.js`). Save the block as `../r6-task9-code.patch`, outside the worktree, and run `git apply ../r6-task9-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
index 023ff06..c9fbdf7 100644
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -562,4 +562,5 @@
 
 @section Scripts {
     <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
+    <script src="~/js/bp-confirm.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index c180a32..486cf1a 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -334,7 +334,7 @@
                             <form method="post" action="@Model.Removal.Action" class="d-flex flex-wrap align-items-center gap-3 mt-2">
                                 @Html.AntiForgeryToken()
                                 <span>It's a draft, so nothing has been published. Removing it deletes it with its accident reports; this can't be undone.</span>
-                                <button type="submit" class="bp-danger-button bp-button-compact text-nowrap">Remove draft battery</button>
+                                <button type="submit" class="bp-danger-button bp-button-compact text-nowrap" data-confirm-action data-confirm-title="Remove draft battery" data-confirm-message="Remove this draft battery and its values? It can't be undone." data-confirm-submit="Remove">Remove draft battery</button>
                             </form>
                         }
                     </details>
@@ -420,10 +420,13 @@
     {
         @await Html.PartialAsync("~/Views/Batteries/_MarkDialog.cshtml", markable)
     }
+    @* Asks before a removal that can't be undone (redesign R6). *@
+    @await Html.PartialAsync("_ConfirmModal")
 </main>
 
 @section Scripts {
     <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
+    <script src="~/js/bp-confirm.js" asp-append-version="true"></script>
     <script src="~/js/bp-status-change.js" asp-append-version="true"></script>
     <script src="~/js/bp-battery-page.js" asp-append-version="true"></script>
     <script src="~/js/bp-picture-trim.js" asp-append-version="true"></script>
diff --git a/web/Views/ClusterAdmin/ApiTokens.cshtml b/web/Views/ClusterAdmin/ApiTokens.cshtml
index c27ef47..e5fa4ca 100644
--- a/web/Views/ClusterAdmin/ApiTokens.cshtml
+++ b/web/Views/ClusterAdmin/ApiTokens.cshtml
@@ -68,4 +68,5 @@
 
 @section Scripts {
     <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
+    <script src="~/js/bp-confirm.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/ClusterAdmin/Users.cshtml b/web/Views/ClusterAdmin/Users.cshtml
index ff1d5bd..b0772ee 100644
--- a/web/Views/ClusterAdmin/Users.cshtml
+++ b/web/Views/ClusterAdmin/Users.cshtml
@@ -256,4 +256,5 @@
 
 @section Scripts {
     <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
+    <script src="~/js/bp-confirm.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/ProductData/Batch.cshtml b/web/Views/ProductData/Batch.cshtml
index a29c377..b72ba09 100644
--- a/web/Views/ProductData/Batch.cshtml
+++ b/web/Views/ProductData/Batch.cshtml
@@ -53,8 +53,11 @@
 
     @await Html.PartialAsync("_Messages")
     @await Html.PartialAsync("_RecordEditor", Model.Editor)
+    @* Asks before a removal that can't be undone (redesign R6). *@
+    @await Html.PartialAsync("_ConfirmModal")
 </main>
 
 @section Scripts {
     <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
+    <script src="~/js/bp-confirm.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/ProductData/Family.cshtml b/web/Views/ProductData/Family.cshtml
index 524b328..3222b71 100644
--- a/web/Views/ProductData/Family.cshtml
+++ b/web/Views/ProductData/Family.cshtml
@@ -272,12 +272,15 @@
             <form method="post" action="@Model.Removal.Action" class="d-flex flex-wrap align-items-center gap-3 mt-2">
                 @Html.AntiForgeryToken()
                 <span>It has no models. Removing it deletes its name and picture; this can't be undone.</span>
-                <button type="submit" class="bp-danger-button bp-button-compact text-nowrap">Remove battery family</button>
+                <button type="submit" class="bp-danger-button bp-button-compact text-nowrap" data-confirm-action data-confirm-title="Remove battery family" data-confirm-message="Remove this battery family, its name and picture? It can't be undone." data-confirm-submit="Remove">Remove battery family</button>
             </form>
         }
     </details>
+    @* Asks before a removal that can't be undone (redesign R6). *@
+    @await Html.PartialAsync("_ConfirmModal")
 </main>
 
 @section Scripts {
     <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
+    <script src="~/js/bp-confirm.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/ProductData/SitesAndOperators.cshtml b/web/Views/ProductData/SitesAndOperators.cshtml
index c6acc01..b761d37 100644
--- a/web/Views/ProductData/SitesAndOperators.cshtml
+++ b/web/Views/ProductData/SitesAndOperators.cshtml
@@ -100,8 +100,11 @@
     {
         <div class="bp-card mt-4">Choose a site or an operator above to edit its passport values.</div>
     }
+    @* Asks before a removal that can't be undone (redesign R6). *@
+    @await Html.PartialAsync("_ConfirmModal")
 </main>
 
 @section Scripts {
     <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
+    <script src="~/js/bp-confirm.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/ProductData/_RecordEditor.cshtml b/web/Views/ProductData/_RecordEditor.cshtml
index 33cb24f..4e8767c 100644
--- a/web/Views/ProductData/_RecordEditor.cshtml
+++ b/web/Views/ProductData/_RecordEditor.cshtml
@@ -219,7 +219,7 @@
         <form method="post" action="@Model.Removal.Action" class="d-flex flex-wrap align-items-center gap-3 mt-2">
             @Html.AntiForgeryToken()
             <span>Nothing uses it yet. Removing it deletes its values@(Model.Kind is TargetKind.Model or TargetKind.Batch ? " and uploaded documents" : string.Empty); this can't be undone.</span>
-            <button type="submit" class="bp-danger-button bp-button-compact text-nowrap">Remove @kindWord</button>
+            <button type="submit" class="bp-danger-button bp-button-compact text-nowrap" data-confirm-action data-confirm-title="Remove @kindWord" data-confirm-message="Remove this @kindWord and its values? It can't be undone." data-confirm-submit="Remove">Remove @kindWord</button>
         </form>
     }
 </details>
diff --git a/web/wwwroot/js/bp-admin-pages.js b/web/wwwroot/js/bp-admin-pages.js
index 2dccbbb..9f5e97d 100644
--- a/web/wwwroot/js/bp-admin-pages.js
+++ b/web/wwwroot/js/bp-admin-pages.js
@@ -56,57 +56,5 @@
         }
     }
 
-    // A button with data-confirm-action asks first, in the shared "Please confirm" window (_ConfirmModal).
-    const confirmModal = document.querySelector('[data-confirm-modal]');
-    const confirmTitle = document.getElementById('bp-confirm-title');
-    const confirmMessage = confirmModal?.querySelector('[data-confirm-message]');
-    const confirmSubmit = confirmModal?.querySelector('[data-confirm-submit]');
-    let pendingForm = null;
-    let pendingButton = null;
-    document.querySelectorAll('[data-confirm-action]').forEach((button) => {
-        button.addEventListener('click', (event) => {
-            const form = button.closest('form');
-            if (!form || !confirmModal || !confirmTitle || !confirmMessage || !confirmSubmit) {
-                return;
-            }
-
-            event.preventDefault();
-            pendingForm = form;
-            pendingButton = button;
-            confirmTitle.textContent = button.dataset.confirmTitle || 'Confirm action';
-            confirmMessage.textContent = button.dataset.confirmMessage || 'Continue with this action?';
-            confirmSubmit.textContent = button.dataset.confirmSubmit || 'Confirm';
-            confirmModal.hidden = false;
-            confirmSubmit.focus();
-        });
-    });
-
-    confirmModal?.querySelectorAll('[data-confirm-cancel]').forEach((button) => {
-        button.addEventListener('click', () => {
-            if (confirmSubmit?.disabled) {
-                return; // already sent: the page reloads when it's done
-            }
-
-            confirmModal.hidden = true;
-            pendingForm = null;
-        });
-    });
-
-    confirmSubmit?.addEventListener('click', () => {
-        if (!pendingForm) {
-            return;
-        }
-
-        // A long action (the demo data) says it's working and can't be sent twice (redesign R1, owner's point 1).
-        const busy = pendingButton?.dataset.confirmBusy;
-        if (busy) {
-            confirmMessage.textContent = busy;
-            confirmSubmit.textContent = 'Working…';
-            confirmSubmit.disabled = true;
-            confirmModal.querySelectorAll('button[data-confirm-cancel]').forEach((button) => { button.disabled = true; });
-            confirmModal.querySelector('[role="dialog"]')?.setAttribute('aria-busy', 'true');
-        }
-
-        pendingForm.submit();
-    });
+    // "Please confirm" before a risky action is in bp-confirm.js (redesign R6).
 })();
```

`web/wwwroot/js/bp-confirm.js`:

```javascript
// "Please confirm" before a risky action (step 3e; on its own since redesign R6 so the battery and product data pages
// can use it without the admin pages' other behaviour): a button with data-confirm-action asks first, in the shared
// window (_ConfirmModal). Without JavaScript the button sends its form at once, as before.
(function () {
    const confirmModal = document.querySelector('.bp-confirm-modal[data-confirm-modal]');
    const confirmTitle = document.getElementById('bp-confirm-title');
    const confirmMessage = confirmModal?.querySelector('[data-confirm-message]');
    const confirmSubmit = confirmModal?.querySelector('[data-confirm-submit]');
    let pendingForm = null;
    let pendingButton = null;
    document.querySelectorAll('[data-confirm-action]').forEach((button) => {
        button.addEventListener('click', (event) => {
            const form = button.closest('form');
            if (!form || !confirmModal || !confirmTitle || !confirmMessage || !confirmSubmit) {
                return;
            }

            event.preventDefault();
            pendingForm = form;
            pendingButton = button;
            confirmTitle.textContent = button.dataset.confirmTitle || 'Confirm action';
            confirmMessage.textContent = button.dataset.confirmMessage || 'Continue with this action?';
            confirmSubmit.textContent = button.dataset.confirmSubmit || 'Confirm';
            confirmModal.hidden = false;
            confirmSubmit.focus();
        });
    });

    confirmModal?.querySelectorAll('[data-confirm-cancel]').forEach((button) => {
        button.addEventListener('click', () => {
            if (confirmSubmit?.disabled) {
                return; // already sent: the page reloads when it's done
            }

            confirmModal.hidden = true;
            pendingForm = null;
        });
    });

    confirmSubmit?.addEventListener('click', () => {
        if (!pendingForm) {
            return;
        }

        // A long action (the demo data) says it's working and can't be sent twice (redesign R1, owner's point 1).
        const busy = pendingButton?.dataset.confirmBusy;
        if (busy) {
            confirmMessage.textContent = busy;
            confirmSubmit.textContent = 'Working…';
            confirmSubmit.disabled = true;
            confirmModal.querySelectorAll('button[data-confirm-cancel]').forEach((button) => { button.disabled = true; });
            confirmModal.querySelector('[role="dialog"]')?.setAttribute('aria-busy', 'true');
        }

        pendingForm.submit();
    });
})();
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoCardTests | FullyQualifiedName~ClustersTabTests | FullyQualifiedName~ConfirmRemovalsTests"`
Expected: `Passed!  - Failed:     0, Passed:    23`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCardTests.cs BatteryPassWeb.Tests/Look/ClustersTabTests.cs BatteryPassWeb.Tests/Look/ConfirmRemovalsTests.cs web/Views/Admin/Clusters.cshtml web/Views/Batteries/Battery.cshtml web/Views/ClusterAdmin/ApiTokens.cshtml web/Views/ClusterAdmin/Users.cshtml web/Views/ProductData/Batch.cshtml web/Views/ProductData/Family.cshtml web/Views/ProductData/SitesAndOperators.cshtml web/Views/ProductData/_RecordEditor.cshtml web/wwwroot/js/bp-admin-pages.js web/wwwroot/js/bp-confirm.js
git commit -m "feat(dpp): a confirm window before every removal that can't be undone" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: The team's list copies the full battery ID and says when

Each row of the team's list has the copy button for the full battery ID and "Version … · updated …" under the Passport badge, as on the Registry.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/TeamListRowTests.cs`
- Modify: `web/Views/Batteries/Batteries.cshtml`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/TeamListRowTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 3 (3 Nov 2026): the team's list lost the old list's copy button for the full battery
/// ID and its "updated" date; both come back, the date as on the Registry ("Version 3 · updated 2 Nov 2026, 19:50").
/// </summary>
public sealed class TeamListRowTests
{
    [Fact]
    public void EachRow_ShouldCopyTheFullBatteryId_AndSayWhenItWasUpdated()
    {
        var list = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Batteries.cshtml"));
        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));

        Assert.Contains("@BatteryPages.ShortId(row.Identifier)</small>@await Html.PartialAsync(\"_CopyIdButton\", row.Identifier)</span>", list, StringComparison.Ordinal);
        Assert.Contains("<small class=\"d-block text-secondary mt-1\">Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>", list, StringComparison.Ordinal);
        Assert.Contains("Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>", registry, StringComparison.Ordinal); // the same words
        Assert.DoesNotContain("· version {version.Number}", list, StringComparison.Ordinal); // said once, under the badges
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~TeamListRowTests"`
Expected: `Failed:     1, Passed:     0` (failing: `TeamListRowTests.EachRow_ShouldCopyTheFullBatteryId_AndSayWhenItWasUpdated`).

- [ ] **Step 3: Write the code**

Patch `r6-task10-code.patch` (`web/Views/Batteries/Batteries.cshtml`). Save the block as `../r6-task10-code.patch`, outside the worktree, and run `git apply ../r6-task10-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Batteries/Batteries.cshtml b/web/Views/Batteries/Batteries.cshtml
index 6f028c7..d84182d 100644
--- a/web/Views/Batteries/Batteries.cshtml
+++ b/web/Views/Batteries/Batteries.cshtml
@@ -64,7 +64,8 @@
                             <tr>
                                 <td data-label="Serial number">
                                     <a href="@BatteryPages.BatteryHref(row.PackId)" class="bp-battery-link">@row.SerialNumber</a>
-                                    <small class="bp-battery-id" title="Battery ID (#6): @row.Identifier">@BatteryPages.ShortId(row.Identifier)</small>
+                                    @* The full battery ID to copy, as the old list had (redesign R6, the owner's choice 3). *@
+                                    <span class="bp-id-copy-row"><small class="bp-battery-id" title="Battery ID (#6): @row.Identifier">@BatteryPages.ShortId(row.Identifier)</small>@await Html.PartialAsync("_CopyIdButton", row.Identifier)</span>
                                 </td>
                                 <td data-label="Model">@row.ModelName <small class="bp-batteries-muted">@row.FamilyName</small></td>
                                 <td data-label="Batch">@row.BatchLabel</td>
@@ -85,8 +86,13 @@
                                         {
                                             @await Html.PartialAsync("_StatusBadge", battery)
                                         }
-                                        @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(row.IsDraft ? null : row.StatusName).With((row.Active ?? row.Latest) is { } version ? $"· version {version.Number}" : null))
+                                        @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(row.IsDraft ? null : row.StatusName))
                                     </div>
+                                    @* The version and when it was last updated, as on the Registry (redesign R6, the owner's choice 3). *@
+                                    @if ((row.Active ?? row.Latest) is { } shown)
+                                    {
+                                        <small class="d-block text-secondary mt-1">Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>
+                                    }
                                 </td>
                                 <td class="bp-action-cell" data-label="Actions">
                                     @* Icons named on hover, like the old app's row actions (redesign R5, owner's item 1). *@
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~TeamListRowTests"`
Expected: `Passed!  - Failed:     0, Passed:     1`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/TeamListRowTests.cs web/Views/Batteries/Batteries.cshtml
git commit -m "fix(dpp): the team's list copies the full battery ID and says when each passport was updated" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Passport pages open in the same tab

Every link to a passport page opens it in the same tab; files and the QR code still open a new one.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs`
- Test (create): `BatteryPassWeb.Tests/Look/PassportLinksSameTabTests.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/Batteries/_AccidentsCard.cshtml`
- Modify: `web/Views/Batteries/_VersionsCard.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task11-tests.patch` (`BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs`). Save the block as `../r6-task11-tests.patch`, outside the worktree, and run `git apply ../r6-task11-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs b/BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs
index 3afc220..832bb70 100644
--- a/BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs
@@ -37,7 +37,7 @@ public sealed class AccidentSectionTests(MongoFixture mongo)
 
         Assert.Contains("See and add this battery's accident reports here.", card, StringComparison.Ordinal);
         Assert.True(list > 0 && upload > list && readers > upload);
-        Assert.Contains("<p class=\"bp-accident-readers\"><a href=\"@ReaderPages.AccidentsHref(Model.Pack.Id)\" target=\"_blank\" rel=\"noreferrer\">", card, StringComparison.Ordinal);
+        Assert.Contains("<p class=\"bp-accident-readers\"><a href=\"@ReaderPages.AccidentsHref(Model.Pack.Id)\">", card, StringComparison.Ordinal);
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Look/PassportLinksSameTabTests.cs`:

```csharp
using System.Text.RegularExpressions;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 4 (3 Nov 2026): passport pages opened in a new tab from the battery pages but in the
/// same tab from the lists; they open in the same tab everywhere now. Files (documents, the QR picture, the API's data)
/// still open in a new tab, as decided on 29 Oct.
/// </summary>
public sealed class PassportLinksSameTabTests
{
    [Theory]
    [InlineData("Batteries", "Battery.cshtml")]
    [InlineData("ClusterAdmin", "Battery.cshtml")]
    [InlineData("Batteries", "_VersionsCard.cshtml")]
    [InlineData("Batteries", "_AccidentsCard.cshtml")]
    [InlineData("Shared", "_RowActions.cshtml")]
    public void APassportPagesLink_ShouldOpenInTheSameTab(string folder, string view)
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", folder, view));
        var passportLinks = Regex.Matches(page, "<a [^>]*href=\"(/dpp/[^\"]*|@ReaderPages\\.AccidentsHref[^\"]*|@publicPage|@detail)\"[^>]*>").Select(match => match.Value).ToArray();

        Assert.All(passportLinks, link => Assert.DoesNotContain("target=\"_blank\"", link, StringComparison.Ordinal));
    }

    [Fact]
    public void Files_ShouldStillOpenInANewTab()
    {
        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var accidents = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_AccidentsCard.cshtml"));

        Assert.Contains("href=\"/qr/@batteryKey/svg\" target=\"_blank\" rel=\"noreferrer\"", battery, StringComparison.Ordinal);
        Assert.Contains("<a href=\"@ReaderPages.DocumentHref(report.Id)\" target=\"_blank\" rel=\"noreferrer\">", accidents, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AccidentSectionTests | FullyQualifiedName~PassportLinksSameTabTests"`
Expected: `Failed:     5, Passed:     4` (failing: `AccidentSectionTests.TheSection_ShouldPutTheReportsAndTheUploadFirst_AndTheReadersPageLast`).

- [ ] **Step 3: Write the code**

Patch `r6-task11-code.patch` (`web/Views/Batteries/Battery.cshtml`, `web/Views/Batteries/_AccidentsCard.cshtml`, `web/Views/Batteries/_VersionsCard.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`). Save the block as `../r6-task11-code.patch`, outside the worktree, and run `git apply ../r6-task11-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 486cf1a..4d198c8 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -190,8 +190,8 @@
             <nav class="bp-bh-open" aria-label="This battery elsewhere">
                 @if (publication.IsPublished)
                 {
-                    <a class="bp-ilink" href="/dpp/@batteryKey" target="_blank" rel="noreferrer" title="The public summary, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
-                    <a class="bp-ilink" href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer" title="Everything its readers see, by who they are"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
+                    <a class="bp-ilink" href="/dpp/@batteryKey" title="The public summary, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
+                    <a class="bp-ilink" href="/dpp/@batteryKey/detail" title="Everything its readers see, by who they are"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
                 }
                 <a class="bp-ilink" href="/qr/@batteryKey/svg" target="_blank" rel="noreferrer" title="@(publication.IsPublished ? "Opens the public summary" : "Opens the public summary once the passport is published")"><svg class="bp-i" aria-hidden="true"><use href="#i-qr" /></svg>QR code</a>
                 <a class="bp-ilink" href="/qr/@batteryKey/download" title="Save the QR code as a picture"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Save QR</a>
diff --git a/web/Views/Batteries/_AccidentsCard.cshtml b/web/Views/Batteries/_AccidentsCard.cshtml
index 7ed27d4..f5b9f78 100644
--- a/web/Views/Batteries/_AccidentsCard.cshtml
+++ b/web/Views/Batteries/_AccidentsCard.cshtml
@@ -68,5 +68,5 @@
         </div>
     </form>
     }
-    <p class="bp-accident-readers"><a href="@ReaderPages.AccidentsHref(Model.Pack.Id)" target="_blank" rel="noreferrer"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>How readers see it</a> <span class="bp-batteries-muted">· the accident page the passport links to</span></p>
+    <p class="bp-accident-readers"><a href="@ReaderPages.AccidentsHref(Model.Pack.Id)"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>How readers see it</a> <span class="bp-batteries-muted">· the accident page the passport links to</span></p>
 </section>
diff --git a/web/Views/Batteries/_VersionsCard.cshtml b/web/Views/Batteries/_VersionsCard.cshtml
index 9e11f77..64c4e62 100644
--- a/web/Views/Batteries/_VersionsCard.cshtml
+++ b/web/Views/Batteries/_VersionsCard.cshtml
@@ -28,7 +28,7 @@
                     {
                         <tr>
                             <td data-label="Version">@row.Version.Number</td>
-                            <td data-label="Passport ID"><span class="bp-id-copy-row"><a class="bp-id-value" href="/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail" target="_blank" rel="noreferrer" title="@row.Version.DigitalProductPassportId">@BatteryPages.ShortId(row.Version.DigitalProductPassportId)</a>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
+                            <td data-label="Passport ID"><span class="bp-id-copy-row"><a class="bp-id-value" href="/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail" title="@row.Version.DigitalProductPassportId">@BatteryPages.ShortId(row.Version.DigitalProductPassportId)</a>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
                             <td data-label="Status"><div class="bp-badges is-compact">@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(row.Version.DppStatus))</div></td>
                             <td data-label="Published">@ValueWords.Time(row.Version.PublishedAtUtc) <small class="bp-batteries-muted d-block">by @row.Version.PublishedBy</small></td>
                             <td data-label="Signature">
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 40de0cb..7b19c88 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -48,7 +48,7 @@
                 </div>
                 @if (Model.Status?.Predecessor is { } replaced)
                 {
-                    <p class="bp-lineage"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>New passport of @replaced.SerialNumber. Its <a href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer">detail page</a> links the earlier one.</p>
+                    <p class="bp-lineage"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>New passport of @replaced.SerialNumber. Its <a href="/dpp/@batteryKey/detail">detail page</a> links the earlier one.</p>
                 }
             </div>
             <div class="bp-bh-facts-row">
@@ -99,8 +99,8 @@
                 <button type="button" class="bp-secondary-button bp-with-icon" data-open-dialog="handover-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-handover" /></svg>Hand over…</button>
             }
             <nav class="bp-bh-open" aria-label="This battery elsewhere">
-                <a class="bp-ilink" href="/dpp/@batteryKey" target="_blank" rel="noreferrer" title="The public summary, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
-                <a class="bp-ilink" href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer" title="Everything your cluster sees"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
+                <a class="bp-ilink" href="/dpp/@batteryKey" title="The public summary, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
+                <a class="bp-ilink" href="/dpp/@batteryKey/detail" title="Everything your cluster sees"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
                 <a class="bp-ilink" href="@ReaderPages.AccidentsHref(Model.Pack.Id)" title="The battery's accident reports, as readers see them"><svg class="bp-i" aria-hidden="true"><use href="#i-accident" /></svg>Accident page</a>
             </nav>
         </div>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AccidentSectionTests | FullyQualifiedName~PassportLinksSameTabTests"`
Expected: `Passed!  - Failed:     0, Passed:     9`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/AccidentSectionTests.cs BatteryPassWeb.Tests/Look/PassportLinksSameTabTests.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_AccidentsCard.cshtml web/Views/Batteries/_VersionsCard.cshtml web/Views/ClusterAdmin/Battery.cshtml
git commit -m "fix(dpp): passport pages open in the same tab everywhere; files still in a new one" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: One name: "Public page"

"Public page" and "Back to the public page" everywhere, never "public summary".

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs`
- Test (create): `BatteryPassWeb.Tests/Look/PublicPageNameTests.cs`
- Modify: `web/Controllers/ProductDataController.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/Batteries/_ModulesCard.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`
- Modify: `web/Views/Help/Guide.cshtml`
- Modify: `web/Views/Passport/_PassportHero.cshtml`
- Modify: `web/Views/ProductData/Family.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task12-tests.patch` (`BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs`, `BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs`). Save the block as `../r6-task12-tests.patch`, outside the worktree, and run `git apply ../r6-task12-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
index 20cfe98..cff6c23 100644
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -25,7 +25,7 @@ public sealed class AdminFeedbackFollowupTests
         var combined = string.Join(Environment.NewLine, registry, hero, rowActions);
         Assert.Contains("bp-report-action", combined);
         Assert.Contains("View more about this passport", hero);
-        Assert.Contains("Back to summary", hero);
+        Assert.Contains("Back to the public page", hero);
         Assert.Contains("bp-report-action-icon", combined);
         Assert.Contains(".bp-report-action::after", css);
         Assert.Contains("top: calc(-100% -", css);
@@ -41,7 +41,7 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains("bp-report-title-block", hero);
         Assert.Contains("bp-report-hero-cta", hero);
         Assert.Contains("View more about this passport", hero);
-        Assert.Contains("Back to summary", hero);
+        Assert.Contains("Back to the public page", hero);
         Assert.DoesNotContain("bp-report-header-actions", hero);
         Assert.DoesNotContain("bp-report-action-strip", hero);
         Assert.DoesNotContain("bp-report-action-label", hero);
@@ -68,7 +68,7 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains("bp-report-hero-layout", hero);
         Assert.Contains("bp-report-hero-media", hero);
         Assert.Contains("View more about this passport", hero);
-        Assert.Contains("Back to summary", hero);
+        Assert.Contains("Back to the public page", hero);
         Assert.Contains("bp-report-hero-qr", hero);
         Assert.Contains(".bp-report-hero-card", css);
         Assert.Contains(".bp-report-wide-section", css);
diff --git a/BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs b/BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs
index fd3b259..dfbe1ed 100644
--- a/BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs
@@ -74,7 +74,7 @@ public sealed class ModelPictureTests(MongoFixture mongo)
         var removed = Assert.IsType<RedirectResult>(await controller.RemoveModelPicture("model-test", CancellationToken.None));
 
         Assert.Equal($"{ProductDataPages.FamilyHref("family-test", "model-test")}#modules", saved.Url);
-        Assert.Equal("Picture saved. Its batteries' public summary shows it instead of the family's.", savedMessage);
+        Assert.Equal("Picture saved. Its batteries' public page shows it instead of the family's.", savedMessage);
         Assert.Equal($"{ProductDataPages.FamilyHref("family-test", "model-test")}#modules", removed.Url);
         Assert.Equal("Picture removed. Its batteries show the family's picture again.", controller.TempData[ProductDataController.StatusKey]);
         Assert.Contains("action=\"/admin/models/@selected.Id/picture\"", page, StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs b/BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs
index b11b5d6..533c5b7 100644
--- a/BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs
+++ b/BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs
@@ -16,7 +16,7 @@ public sealed class HeroButtonIconsTests
 
         Assert.DoesNotContain("&lt;-", hero, StringComparison.Ordinal);
         Assert.DoesNotContain("-&gt;", hero, StringComparison.Ordinal);
-        Assert.Contains("<use href=\"#i-back\" /></svg>Back to summary</a>", hero, StringComparison.Ordinal);
+        Assert.Contains("<use href=\"#i-back\" /></svg>Back to the public page</a>", hero, StringComparison.Ordinal);
         Assert.Contains("View more about this passport<svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-arrow\" /></svg></a>", hero, StringComparison.Ordinal);
         Assert.Contains("Open the battery page<svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-arrow\" /></svg></a>", hero, StringComparison.Ordinal); // one name since R6 T6
         Assert.Contains(".bp-report-hero-cta { gap: 8px; }", css, StringComparison.Ordinal);
```

`BatteryPassWeb.Tests/Look/PublicPageNameTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 5 (3 Nov 2026): the public page had two names ("Public page" on the icons, "summary"
/// on the detail page's button and in help texts); it is "the public page" in every visible word. Code names stay.
/// </summary>
public sealed class PublicPageNameTests
{
    private static readonly string[] Views =
    [
        "Batteries/Battery.cshtml", "Batteries/_ModulesCard.cshtml", "ClusterAdmin/Battery.cshtml", "Help/Guide.cshtml",
        "Passport/_PassportHero.cshtml", "ProductData/Family.cshtml", "Shared/_RowActions.cshtml",
    ];

    [Fact]
    public void ThePublicPage_ShouldHaveOneName()
    {
        foreach (var view in Views)
        {
            var visible = string.Join("\n", File.ReadAllLines(DppTestFiles.RepoFile(["web", "Views", .. view.Split('/')]))
                .Where(line => !line.TrimStart().StartsWith("@*", StringComparison.Ordinal)));
            Assert.DoesNotContain("public summary", visible, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Back to summary", visible, StringComparison.Ordinal);
        }

        Assert.Contains("<use href=\"#i-back\" /></svg>Back to the public page</a>", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml")), StringComparison.Ordinal);
        Assert.Contains("Its batteries' public page shows it", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "ProductDataController.cs")), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminFeedbackFollowupTests | FullyQualifiedName~ModelPictureTests | FullyQualifiedName~HeroButtonIconsTests | FullyQualifiedName~PublicPageNameTests"`
Expected: `Failed:     6, Passed:     9` (failing: `AdminFeedbackFollowupTests.PassportReportHero_ShouldUseApprovedSplitHeaderDesign`, `AdminFeedbackFollowupTests.ReportActions_ShouldUseConsistentIconButtonsWithDesignedTooltips`, `AdminFeedbackFollowupTests.ReportActions_ShouldUseHeroCtaWithoutOldHeaderRailLabels`, `ModelPictureTests.TheFamilyPage_ShouldSaveAndRemoveAModelsPictureInItsSection`, `HeroButtonIconsTests.TheHerosButtons_ShouldUseTheArrowIcons_NotTypedArrows`, `PublicPageNameTests.ThePublicPage_ShouldHaveOneName`).

- [ ] **Step 3: Write the code**

Patch `r6-task12-code.patch` (`web/Controllers/ProductDataController.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/Batteries/_ModulesCard.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/Views/Help/Guide.cshtml`, `web/Views/Passport/_PassportHero.cshtml`, `web/Views/ProductData/Family.cshtml`). Save the block as `../r6-task12-code.patch`, outside the worktree, and run `git apply ../r6-task12-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/ProductDataController.cs b/web/Controllers/ProductDataController.cs
index c1b509e..ef9751c 100644
--- a/web/Controllers/ProductDataController.cs
+++ b/web/Controllers/ProductDataController.cs
@@ -148,7 +148,7 @@ public sealed class ProductDataController(ProductDataPages pages, RecordEditor e
             ? "choose a PNG or JPEG picture first"
             : await pages.SetModelPictureAsync(Who, modelId, picture.FileName, await ReadAsync(picture, cancellationToken), cancellationToken);
         TempData[problem is null ? StatusKey : ErrorKey] = problem is null
-            ? "Picture saved. Its batteries' public summary shows it instead of the family's."
+            ? "Picture saved. Its batteries' public page shows it instead of the family's."
             : $"The picture was not saved: {problem}.";
         return Redirect($"{ProductDataPages.FamilyHref(model.FamilyId, modelId)}#modules");
     }
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 4d198c8..0b6b359 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -190,10 +190,10 @@
             <nav class="bp-bh-open" aria-label="This battery elsewhere">
                 @if (publication.IsPublished)
                 {
-                    <a class="bp-ilink" href="/dpp/@batteryKey" title="The public summary, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
+                    <a class="bp-ilink" href="/dpp/@batteryKey" title="The public page, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
                     <a class="bp-ilink" href="/dpp/@batteryKey/detail" title="Everything its readers see, by who they are"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
                 }
-                <a class="bp-ilink" href="/qr/@batteryKey/svg" target="_blank" rel="noreferrer" title="@(publication.IsPublished ? "Opens the public summary" : "Opens the public summary once the passport is published")"><svg class="bp-i" aria-hidden="true"><use href="#i-qr" /></svg>QR code</a>
+                <a class="bp-ilink" href="/qr/@batteryKey/svg" target="_blank" rel="noreferrer" title="@(publication.IsPublished ? "Opens the public page" : "Opens the public page once the passport is published")"><svg class="bp-i" aria-hidden="true"><use href="#i-qr" /></svg>QR code</a>
                 <a class="bp-ilink" href="/qr/@batteryKey/download" title="Save the QR code as a picture"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Save QR</a>
                 <a class="bp-ilink" href="/admin/audit?q=@Uri.EscapeDataString(Model.Pack.Id)" title="Every change to this battery, in the audit log"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>History</a>
             </nav>
diff --git a/web/Views/Batteries/_ModulesCard.cshtml b/web/Views/Batteries/_ModulesCard.cshtml
index d5a7bc6..452815a 100644
--- a/web/Views/Batteries/_ModulesCard.cshtml
+++ b/web/Views/Batteries/_ModulesCard.cshtml
@@ -7,7 +7,7 @@
     <p class="mb-0">
         @if (Model.ForTeam)
         {
-            <text>The public summary shows how many modules the battery has, with the model's
+            <text>The public page shows how many modules the battery has, with the model's
             <a href="@Model.ModuleDescriptionHref">module description</a>@(Model.HasModuleDescription ? string.Empty : " (not typed yet)").</text>
         }
         else
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 7b19c88..bb73939 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -99,7 +99,7 @@
                 <button type="button" class="bp-secondary-button bp-with-icon" data-open-dialog="handover-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-handover" /></svg>Hand over…</button>
             }
             <nav class="bp-bh-open" aria-label="This battery elsewhere">
-                <a class="bp-ilink" href="/dpp/@batteryKey" title="The public summary, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
+                <a class="bp-ilink" href="/dpp/@batteryKey" title="The public page, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
                 <a class="bp-ilink" href="/dpp/@batteryKey/detail" title="Everything your cluster sees"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
                 <a class="bp-ilink" href="@ReaderPages.AccidentsHref(Model.Pack.Id)" title="The battery's accident reports, as readers see them"><svg class="bp-i" aria-hidden="true"><use href="#i-accident" /></svg>Accident page</a>
             </nav>
diff --git a/web/Views/Help/Guide.cshtml b/web/Views/Help/Guide.cshtml
index a9d76ee..5284082 100644
--- a/web/Views/Help/Guide.cshtml
+++ b/web/Views/Help/Guide.cshtml
@@ -26,7 +26,7 @@
                 <h2>What each reader sees</h2>
                 <dl class="bp-word-list">
                     <dt>Everyone</dt>
-                    <dd>The public summary: the public values, the document links and the family's picture.</dd>
+                    <dd>The public page: the public values, the document links and the family's picture.</dd>
                     <dt>The battery's cluster, market surveillance</dt>
                     <dd>Also the purchaser values, such as state of charge, temperature, battery status and accident reports.</dd>
                     <dt>Repair, recycling, treatment, Commission</dt>
@@ -89,7 +89,7 @@
                         <li>The model, batch, site and operator are complete.</li>
                         <li>The battery's own values and first live values are in.</li>
                         <li>Nothing blocks publication, and the battery says "Published" with the latest version.</li>
-                        <li>The QR code opens the public summary.</li>
+                        <li>The QR code opens the public page.</li>
                     </ul>
                 </section>
             }
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index 56249a9..a2b68f2 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -122,7 +122,7 @@
             <div class="bp-hero-ctas">
                 @if (detail)
                 {
-                    <a class="bp-report-hero-cta" href="@PassportPages.SummaryHref(Model.Address)"><svg class="bp-i" aria-hidden="true"><use href="#i-back" /></svg>Back to summary</a>
+                    <a class="bp-report-hero-cta" href="@PassportPages.SummaryHref(Model.Address)"><svg class="bp-i" aria-hidden="true"><use href="#i-back" /></svg>Back to the public page</a>
                     @if (ViewData["ManageHref"] is string manageHref)
                     {
                         <a class="bp-report-hero-cta" href="@manageHref">Open the battery page<svg class="bp-i" aria-hidden="true"><use href="#i-arrow" /></svg></a>
diff --git a/web/Views/ProductData/Family.cshtml b/web/Views/ProductData/Family.cshtml
index 3222b71..8f66265 100644
--- a/web/Views/ProductData/Family.cshtml
+++ b/web/Views/ProductData/Family.cshtml
@@ -46,7 +46,7 @@
                 </form>
                 <form method="post" action="/admin/families/@familyId/picture" enctype="multipart/form-data" class="bp-family-identity-field" data-upload-row>
                     @Html.AntiForgeryToken()
-                    <span>Picture, shown on the public summary (PNG or JPEG)</span>
+                    <span>Picture, shown on the public page (PNG or JPEG)</span>
                     <div class="d-flex gap-2">
                         <input type="text" readonly value="@(hasPicture ? "A picture is set" : "No picture yet")" aria-label="Current picture" data-upload-name />
                         <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap flex-shrink-0 mb-0">
@@ -148,7 +148,7 @@
                 </div>
                 <div class="d-grid gap-2">
                     <span><strong>Picture of @selected.Name</strong> <em class="bp-field-marker">Not passport data</em></span>
-                    <small class="bp-field-help">@(selected.PictureFileId is null ? "It shows the family's picture. Give it its own when it looks different, for example a double stack." : "Its own picture: its batteries' public summary shows it instead of the family's.")</small>
+                    <small class="bp-field-help">@(selected.PictureFileId is null ? "It shows the family's picture. Give it its own when it looks different, for example a double stack." : "Its own picture: its batteries' public page shows it instead of the family's.")</small>
                     <div class="bp-attr-file">
                         <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                             Choose picture
@@ -171,7 +171,7 @@
                 <label for="module-description">Module description of @selected.Name <em class="bp-field-marker">Not passport data</em></label>
                 <textarea id="module-description" name="description" rows="2" maxlength="@ModuleList.MaxDescriptionLength" placeholder="e.g. 8 lithium-ion modules, 2 strings of 4">@selected.ModuleDescription</textarea>
                 <div class="bp-live-save">
-                    <small class="bp-field-help">Shown on the public summary of this model's batteries, with each battery's module count. Each battery's modules are listed on its own page.</small>
+                    <small class="bp-field-help">Shown on the public page of this model's batteries, with each battery's module count. Each battery's modules are listed on its own page.</small>
                     <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap">Save description</button>
                 </div>
             </form>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminFeedbackFollowupTests | FullyQualifiedName~ModelPictureTests | FullyQualifiedName~HeroButtonIconsTests | FullyQualifiedName~PublicPageNameTests"`
Expected: `Passed!  - Failed:     0, Passed:    15`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs BatteryPassWeb.Tests/Look/HeroButtonIconsTests.cs BatteryPassWeb.Tests/Look/PublicPageNameTests.cs web/Controllers/ProductDataController.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_ModulesCard.cshtml web/Views/ClusterAdmin/Battery.cshtml web/Views/Help/Guide.cshtml web/Views/Passport/_PassportHero.cshtml web/Views/ProductData/Family.cshtml
git commit -m "fix(dpp): the public page has one name, "Public page"" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: The carbon footprint study's link in words

The study link (#36) reads "Carbon footprint study", with its address on hover.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/StudyLinkWordsTests.cs`
- Modify: `web/Dpp/Pages/ValueWords.cs`
- Modify: `web/Views/Passport/_PassportTabs.cshtml`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/StudyLinkWordsTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's choice 6 (3 Nov 2026): the carbon footprint study (#36) showed its web address as the link's
/// words; it reads "Carbon footprint study", the address on hover and in the link itself.
/// </summary>
public sealed class StudyLinkWordsTests
{
    [Fact]
    public void TheStudysLink_ShouldReadAsWords_AndLeadToTheAddress()
    {
        const string Address = "https://www.example.com/demo/compact-7m/carbon-footprint-study";

        var line = Assert.Single(ValueWords.Lines(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(36)), JsonValue.Create(Address)));

        Assert.Equal(new ValueLine(null, "Carbon footprint study", Address), line);
    }

    [Fact]
    public void ALinksAddress_ShouldShowOnHover() =>
        Assert.Contains("<a href=\"@part.Href\" target=\"_blank\" rel=\"noreferrer\" title=\"@part.Href\">@part.Text</a>",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml")), StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StudyLinkWordsTests"`
Expected: `Failed:     2, Passed:     0` (failing: `StudyLinkWordsTests.ALinksAddress_ShouldShowOnHover`, `StudyLinkWordsTests.TheStudysLink_ShouldReadAsWords_AndLeadToTheAddress`).

- [ ] **Step 3: Write the code**

Patch `r6-task13-code.patch` (`web/Dpp/Pages/ValueWords.cs`, `web/Views/Passport/_PassportTabs.cshtml`). Save the block as `../r6-task13-code.patch`, outside the worktree, and run `git apply ../r6-task13-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/ValueWords.cs b/web/Dpp/Pages/ValueWords.cs
index c93dbdd..cd7f9c3 100644
--- a/web/Dpp/Pages/ValueWords.cs
+++ b/web/Dpp/Pages/ValueWords.cs
@@ -21,6 +21,9 @@ public static class ValueWords
 {
     private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");
 
+    /// <summary>What a web link is called instead of its address (redesign R6, the owner's choice 6).</summary>
+    private static readonly Dictionary<int, string> LinkWords = new() { [36] = "Carbon footprint study" };
+
     /// <summary>The battery category (#20) in words: "industrial/non-stationary battery" reads "Industrial, non-stationary battery".</summary>
     public static string CategoryWords(string code)
     {
@@ -93,7 +96,8 @@ public static class ValueWords
             case FormKind.Single:
                 var only = form.Inputs[0];
                 var single = Part(only, only.Path.Length == 0 ? value : value[only.Path]);
-                return single is null ? [] : [new ValueLine(null, single, only.Kind == InputKind.Url ? single : null)];
+                // A web link reads as what it is when the passport has words for it (#36, redesign R6); its address shows on hover.
+                return single is null ? [] : [new ValueLine(null, only.Kind == InputKind.Url && LinkWords.GetValueOrDefault(form.Attribute.Number) is { } words ? words : single, only.Kind == InputKind.Url ? single : null)];
             default:
                 return form.Inputs
                     .Select(input => (input.Label, Text: Part(input, value[input.Path])))
diff --git a/web/Views/Passport/_PassportTabs.cshtml b/web/Views/Passport/_PassportTabs.cshtml
index 611ac44..6a406cb 100644
--- a/web/Views/Passport/_PassportTabs.cshtml
+++ b/web/Views/Passport/_PassportTabs.cshtml
@@ -106,7 +106,7 @@
                                             }
                                             @if (part.Href is not null)
                                             {
-                                                <a href="@part.Href" target="_blank" rel="noreferrer">@part.Text</a>
+                                                <a href="@part.Href" target="_blank" rel="noreferrer" title="@part.Href">@part.Text</a>
                                             }
                                             else
                                             {
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StudyLinkWordsTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/StudyLinkWordsTests.cs web/Dpp/Pages/ValueWords.cs web/Views/Passport/_PassportTabs.cshtml
git commit -m "fix(dpp): the carbon footprint study's link reads as words, its address on hover" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: The home page's sample is a published demo battery

"Open a sample battery passport" opens a published demo battery while the demo data is there, and doesn't show otherwise.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Demo/HomeSampleLinkTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs`
- Modify: `web/Controllers/HomeController.cs`
- Modify: `web/Views/Home/Index.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task14-tests.patch` (`BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs`). Save the block as `../r6-task14-tests.patch`, outside the worktree, and run `git apply ../r6-task14-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs b/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs
index ce4f69d..fa9a9e5 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs
@@ -197,7 +197,7 @@ public sealed class PassportControllerTests(MongoFixture mongo)
     {
         var home = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Home", "Index.cshtml"));
 
-        Assert.DoesNotContain("sample", home, StringComparison.OrdinalIgnoreCase);
+        Assert.DoesNotContain("SamplePassportId", home, StringComparison.Ordinal); // not the old app's sample; R6 links a published demo battery instead
         Assert.DoesNotContain("DID", home, StringComparison.Ordinal);
         Assert.Contains("placeholder=\"Serial number, battery ID or passport ID\"", home, StringComparison.Ordinal);
     }
```

`BatteryPassWeb.Tests/Demo/HomeSampleLinkTests.cs`:

```csharp
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Redesign R6, the owner's choice 7 (3 Nov 2026): the old home page had a "Search sample battery" link; the home page
/// links a published demo battery again, "Open a sample battery passport", only while demo data exists, never a real one.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class HomeSampleLinkTests(MongoFixture mongo)
{
    private static async Task<object?> SampleAsync(DemoRig rig)
    {
        var controller = new HomeController(rig.Services.GetRequiredService<PassportPages>());
        var view = Assert.IsType<ViewResult>(await controller.Index(null, null, rig.Services.GetRequiredService<BatteryPages>(), CancellationToken.None));
        return view.ViewData[HomeController.SampleKey];
    }

    [Fact]
    public async Task TheHomePage_ShouldOfferAPublishedDemoBattery_OnlyWhileThereIsOne()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var before = await SampleAsync(rig);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var after = await SampleAsync(rig);

        Assert.Equal(string.Empty, before);
        Assert.StartsWith("urn:", Assert.IsType<string>(after), StringComparison.Ordinal); // a demo battery's ID
        Assert.Equal(await rig.Services.GetRequiredService<BatteryPages>().ExampleBatteryIdAsync(), after); // the API help's sample
    }

    [Fact]
    public void TheHomePage_ShouldLinkTheSample() =>
        Assert.Contains("<a href=\"/dpp/@Uri.EscapeDataString(sample)\">Open a sample battery passport</a>",
            File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Home", "Index.cshtml")), StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HomeSampleLinkTests | FullyQualifiedName~PassportControllerTests"`
Expected: the build fails with CS0117, CS1501 (for example `error CS0117 'HomeController' does not contain a definition for 'SampleKey'`).

- [ ] **Step 3: Write the code**

Patch `r6-task14-code.patch` (`web/Controllers/HomeController.cs`, `web/Views/Home/Index.cshtml`). Save the block as `../r6-task14-code.patch`, outside the worktree, and run `git apply ../r6-task14-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/HomeController.cs b/web/Controllers/HomeController.cs
index 3874e4e..1da96ab 100644
--- a/web/Controllers/HomeController.cs
+++ b/web/Controllers/HomeController.cs
@@ -13,11 +13,16 @@ public class HomeController(PassportPages pages) : Controller
     /// <summary>Address parts that aren't an ID: the passport pages' prefix and suffixes, and today's old ones.</summary>
     private static readonly HashSet<string> RouteWords = new(StringComparer.OrdinalIgnoreCase) { "dpp", "detail", "summary", "latest" };
 
+    /// <summary>Where the home page finds the sample battery's ID: a published demo battery, or none (redesign R6).</summary>
+    public const string SampleKey = "SampleBatteryId";
+
     [HttpGet("")]
-    public IActionResult Index([FromQuery] string? q, [FromQuery] string? notFound)
+    public async Task<IActionResult> Index([FromQuery] string? q, [FromQuery] string? notFound, [FromServices] BatteryPages batteries, CancellationToken cancellationToken)
     {
         ViewData["SearchQuery"] = q?.Trim() ?? string.Empty;
         ViewData["SearchNotFound"] = string.Equals(notFound, "1", StringComparison.OrdinalIgnoreCase);
+        // A sample to open, as the old app had: a published demo battery, never a real one (as the API help, 30 Oct 2026).
+        ViewData[SampleKey] = await batteries.ExampleBatteryIdAsync(cancellationToken);
         return View();
     }
 
diff --git a/web/Views/Home/Index.cshtml b/web/Views/Home/Index.cshtml
index 7b4d819..fb94fad 100644
--- a/web/Views/Home/Index.cshtml
+++ b/web/Views/Home/Index.cshtml
@@ -33,6 +33,11 @@
                 Enter a serial number, battery ID or passport ID, or use the QR icon inside the field before searching.
             </p>
         </form>
+        @if (ViewData[BatteryPassWeb.Controllers.HomeController.SampleKey] is string { Length: > 0 } sample)
+        {
+            @* A published demo battery to try, while demo data exists (redesign R6, the owner's choice 7). *@
+            <p class="bp-landing-sample mt-3 mb-0"><a href="/dpp/@Uri.EscapeDataString(sample)">Open a sample battery passport</a></p>
+        }
     </div>
 
     <div class="bp-qr-modal" data-qr-modal hidden>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HomeSampleLinkTests | FullyQualifiedName~PassportControllerTests"`
Expected: `Passed!  - Failed:     0, Passed:    23`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/HomeSampleLinkTests.cs BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs web/Controllers/HomeController.cs web/Views/Home/Index.cshtml
git commit -m "fix(dpp): the home page links a published demo battery as the sample" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: The audit log's filter drops the old model's entries

"Old passports", "Old batteries" and "Old passport files" leave the filter; their entries still show under "All".

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/AuditFilterOldEntriesTests.cs`
- Modify: `web/Views/Admin/AuditLog.cshtml`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/AuditFilterOldEntriesTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's choice 8 (3 Nov 2026): the audit log's filter still offered "Old passports", "Old batteries"
/// and "Old passport files", leftovers of the old model whose data was reset (decision 1); they're gone.
/// </summary>
public sealed class AuditFilterOldEntriesTests
{
    [Fact]
    public void TheFilter_ShouldNotOfferTheOldModelsEntries()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "AuditLog.cshtml"));

        Assert.DoesNotContain("\"Old passports\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Old batteries\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Old passport files\"", view, StringComparison.Ordinal);
        Assert.Contains("@Option(\"dpp-pack\", \"Batteries\", filter.EntityType)", view, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AuditFilterOldEntriesTests"`
Expected: `Failed:     1, Passed:     0` (failing: `AuditFilterOldEntriesTests.TheFilter_ShouldNotOfferTheOldModelsEntries`).

- [ ] **Step 3: Write the code**

Patch `r6-task15-code.patch` (`web/Views/Admin/AuditLog.cshtml`). Save the block as `../r6-task15-code.patch`, outside the worktree, and run `git apply ../r6-task15-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Admin/AuditLog.cshtml b/web/Views/Admin/AuditLog.cshtml
index c0d5ffa..fe9426e 100644
--- a/web/Views/Admin/AuditLog.cshtml
+++ b/web/Views/Admin/AuditLog.cshtml
@@ -58,8 +58,7 @@
                     @Option("cluster", "Clusters", filter.EntityType)
                     @Option("apiToken", "API tokens", filter.EntityType)
                     @Option("account", "Accounts", filter.EntityType)
-                    @Option("passport", "Old passports", filter.EntityType)
-                    @Option("battery", "Old batteries", filter.EntityType)
+                    @* The old model's entity types are gone: its data was reset (decision 1; redesign R6). *@
                 </select>
             </label>
             <label>
@@ -88,9 +87,6 @@
                             @Option("dpp", "Product data and passports", filter.Category)
                             @Option("security", "Sign-ins, accounts and tokens", filter.Category)
                             @Option("admin", "Clusters and other admin", filter.Category)
-                            @Option("passport", "Old passports", filter.Category)
-                            @Option("battery", "Old batteries", filter.Category)
-                            @Option("file", "Old passport files", filter.Category)
                         </select>
                     </label>
                     <label>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AuditFilterOldEntriesTests"`
Expected: `Passed!  - Failed:     0, Passed:     1`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/AuditFilterOldEntriesTests.cs web/Views/Admin/AuditLog.cshtml
git commit -m "fix(dpp): the audit log's filter drops the old model's entries" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: The cluster admin's History

A read-only History section on the cluster admin's battery page, for the time the battery was in their cluster only, and never more than their battery page already shows: another cluster reads "Another cluster", people outside the cluster by role, no Authorities values, no before-values, no reasons for a mark. A battery moved from North to South shows South's admin only South's time.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/ClusterHistoryTests.cs`
- Create: `web/Dpp/Audit/ClusterHistory.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Modify: `web/Services/ApplicationAuditService.cs`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`
- Create: `web/Views/ClusterAdmin/_HistoryCard.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task16-tests.patch` (`BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs`, `BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs`). Save the block as `../r6-task16-tests.patch`, outside the worktree, and run `git apply ../r6-task16-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs b/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs
index 74f7cac..af6839a 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs
@@ -14,7 +14,7 @@ public sealed class BatteryPageLayoutTests
         foreach (var (page, ids) in new[]
         {
             (View("Batteries", "Battery.cshtml"), new[] { "overview", "values", "status", "live", "accidents", "modules", "versions" }),
-            (View("ClusterAdmin", "Battery.cshtml"), new[] { "overview", "status", "live", "accidents", "handover", "modules", "versions" }),
+            (View("ClusterAdmin", "Battery.cshtml"), new[] { "overview", "status", "live", "accidents", "handover", "modules", "versions", "history" }),
         })
         {
             var start = page.IndexOf("<div class=\"bp-bpanel\">", StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs b/BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs
index 1c75213..9f5fb6f 100644
--- a/BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs
@@ -69,6 +69,6 @@ public sealed class BatterySectionsTests(MongoFixture mongo)
 
         var page = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId))!;
 
-        Assert.Equal(["overview", "status", "live", "accidents", "handover", "modules", "versions"], page.Sections.Select(section => section.Id));
+        Assert.Equal(["overview", "status", "live", "accidents", "handover", "modules", "versions", "history"], page.Sections.Select(section => section.Id));
     }
 }
```

`BatteryPassWeb.Tests/Dpp/ClusterHistoryTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's choice 9 (3 Nov 2026) with the managing session's privacy rules: the cluster admin's battery
/// page gets a read-only History of what happened while the battery was in their cluster, and its moves in and out. It
/// never shows more than their page does: no other cluster's name or ID, no team member's name, no value they may not
/// read (the authorities'), no mark reason. One battery moves from Customer North to Customer South to prove it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterHistoryTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";
    private const string SouthAdmin = "south-admin@example.com";
    private const string Reason = "Demo: declared values don't match the test report";

    private static ClusterHistory History(PublicationRig rig) =>
        new(new ApplicationAuditService(rig.Context), new AuditLines(rig.Store, new DocumentStore(rig.Context), AttributeFormsTests.Forms, rig.Context));

    private static string Text(IReadOnlyList<AuditLine> lines) => string.Join("\n", lines.Select(line =>
        string.Concat(line.Sentence.Select(word => word.Text)) + (line.Change is null ? string.Empty : $" · {line.Change}")
        + string.Concat(line.Values.Select(row => $" | {row.Name}: {row.Before} -> {row.After}"))));

    private static async Task SaveAsync(PublicationRig rig, ChangeSender sender, string property, JsonNode value) =>
        Assert.True((await rig.Pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { [property] = value }, "form"))).Saved);

    [Fact]
    public async Task ABatteryMovedBetweenTwoClusters_ShouldShowEachClusterOnlyItsOwnTime()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var north = ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North);
        var south = ChangeSender.ClusterAdmin(SouthAdmin, BatteryTesting.South);
        var team = PublicationTesting.Team;

        await SaveAsync(rig, north, "stateOfChargeSoC", new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = 71 });
        await SaveAsync(rig, team, "thirdPartyServiceProvider", "Authorities-only provider name"); // #13, the authorities' value
        var handOvers = HandOverTests.Service(rig);
        Assert.Null((await handOvers.AskAsync(north, rig.PackId, BatteryTesting.South, confirm: true)).Problem);
        var waiting = (await rig.Store.FindWaitingHandOverAsync(rig.PackId))!;
        Assert.Null(await handOvers.AnswerAsync(south, waiting.Id, accept: true));
        await SaveAsync(rig, south, "stateOfChargeSoC", new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = 42 });
        var marks = PassportMarkTests.Service(rig);
        Assert.Null(await marks.MarkInvalidAsync(team, rig.PackId, Reason));
        Assert.Null(await marks.UndoAsync(team, rig.PackId));

        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var forNorth = Text(await History(rig).ForAsync(pack, new HashSet<string> { BatteryTesting.North }));
        var forSouth = Text(await History(rig).ForAsync(pack, new HashSet<string> { BatteryTesting.South }));

        // North: its own change and the hand-over out; nothing after, nothing of South's.
        Assert.Contains(NorthAdmin, forNorth, StringComparison.Ordinal);
        Assert.Contains("71", forNorth, StringComparison.Ordinal);
        Assert.Contains("asked another cluster to accept the battery", forNorth, StringComparison.Ordinal);
        Assert.Contains("Another cluster accepted the battery's hand-over: it moved to another cluster", forNorth, StringComparison.Ordinal);
        Assert.DoesNotContain("42", forNorth, StringComparison.Ordinal);
        Assert.DoesNotContain("marked the passport invalid", forNorth, StringComparison.Ordinal);

        // South: the hand-over in, its own change, the mark and its undoing as facts; nothing before, nothing of North's.
        Assert.Contains("Another cluster asked your cluster to accept the battery", forSouth, StringComparison.Ordinal);
        Assert.Contains($"{SouthAdmin} accepted the battery's hand-over to your cluster", forSouth, StringComparison.Ordinal);
        Assert.Contains("42", forSouth, StringComparison.Ordinal);
        Assert.Contains("The manufacturer's team marked the passport invalid", forSouth, StringComparison.Ordinal);
        Assert.Contains("The manufacturer's team undid the passport's mark", forSouth, StringComparison.Ordinal);
        Assert.DoesNotContain("71", forSouth, StringComparison.Ordinal);

        foreach (var (history, other, otherAdmin) in new[] { (forNorth, BatteryTesting.South, SouthAdmin), (forSouth, BatteryTesting.North, NorthAdmin) })
        {
            Assert.DoesNotContain(other, history, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Customer North", history, StringComparison.Ordinal);
            Assert.DoesNotContain("Customer South", history, StringComparison.Ordinal);
            Assert.DoesNotContain(otherAdmin, history, StringComparison.Ordinal);
            Assert.DoesNotContain(team.Who, history, StringComparison.Ordinal); // the team by role, never by name
            Assert.DoesNotContain(Reason, history, StringComparison.Ordinal); // a mark's reason is the team's and market surveillance's
            Assert.DoesNotContain("Authorities-only provider name", history, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Others_ShouldBeNamedByTheirRole()
    {
        Assert.Equal("a@north", ClusterHistory.Who("a@north", nameof(SenderKind.ClusterAdmin)));
        Assert.Equal("Your battery system", ClusterHistory.Who("token north", nameof(SenderKind.BatterySystemFeed)));
        Assert.Equal("The manufacturer's team", ClusterHistory.Who("team@example.com", nameof(SenderKind.Team)));
        Assert.Equal("Market surveillance", ClusterHistory.Who("msa@example.com", nameof(SenderKind.MarketSurveillance)));
        Assert.Equal("The app", ClusterHistory.Who("system", string.Empty));
    }

    [Fact]
    public void TheClusterAdminsPage_ShouldHaveAHistorySection()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "_HistoryCard.cshtml"));

        Assert.Contains("@await Html.PartialAsync(\"~/Views/ClusterAdmin/_HistoryCard.cshtml\", Model.History)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("line.Facts", card, StringComparison.Ordinal); // never the team's details
        Assert.DoesNotContain("line.Technical", card, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPageLayoutTests | FullyQualifiedName~BatterySectionsTests | FullyQualifiedName~ClusterHistoryTests"`
Expected: the build fails with CS0246 (for example `error CS0246 The type or namespace name 'ClusterHistory' could not be found`).

- [ ] **Step 3: Write the code**

Patch `r6-task16-code.patch` (`web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Services/ApplicationAuditService.cs`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r6-task16-code.patch`, outside the worktree, and run `git apply ../r6-task16-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index 3163889..f909db4 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -60,6 +60,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<UploadService>();
         services.AddSingleton<PassportPages>();
         services.AddSingleton<AuditLines>();
+        services.AddSingleton<ClusterHistory>();
         return services;
     }
 }
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index 85b2e18..7a37857 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -32,6 +32,9 @@ public sealed record ClusterBatteryPage(
     /// <summary>The "Hand over" card (step 4d): the waiting hand-over, or the window to ask for one.</summary>
     public HandOverCardView HandOver { get; init; } = new(string.Empty, string.Empty, null, null);
 
+    /// <summary>What happened while the battery was in this cluster, as its admin may read it (redesign R6), newest first.</summary>
+    public IReadOnlyList<BatteryPassWeb.Dpp.Audit.AuditLine> History { get; init; } = [];
+
     /// <summary>The team page's cards, without what only the team does (step 4a).</summary>
     public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false);
 
@@ -54,6 +57,7 @@ public sealed record ClusterBatteryPage(
             sections.Add(new("handover", "Hand over", "handover", HandOver.Waiting is null ? "To another cluster" : "Waiting for their answer"));
             sections.Add(new("modules", "Modules", "modules", Count: Pack.Modules.Count));
             sections.Add(new("versions", "Versions", "versions", Count: Versions.Count));
+            sections.Add(new("history", "History", "history", "While it was your cluster's")); // redesign R6
             return sections;
         }
     }
@@ -74,7 +78,8 @@ public sealed class ClusterBatteryPages(
     DocumentStore documentStore,
     ClusterRepository clusters,
     StatusPages status,
-    LiveTrends? trends = null)
+    LiveTrends? trends = null,
+    BatteryPassWeb.Dpp.Audit.ClusterHistory? history = null)
 {
     public static string BatteryHref(string packId) => $"/cluster-admin/batteries/{Uri.EscapeDataString(packId)}";
 
@@ -142,6 +147,7 @@ public sealed class ClusterBatteryPages(
             Status = await status.CardAsync(sources, active: true, href, forTeam: false, cancellationToken: cancellationToken),
             Tiles = trends is null ? [] : await trends.TilesAsync(sources.Pack, sources.Model, cancellationToken),
             HandOver = new HandOverCardView($"{href}/handover", serial, waiting, waiting is null ? null : names.GetValueOrDefault(waiting.ToClusterId) ?? waiting.ToClusterId),
+            History = history is null ? [] : await history.ForAsync(pack, sender.ClusterIds, cancellationToken),
         };
     }
 
diff --git a/web/Services/ApplicationAuditService.cs b/web/Services/ApplicationAuditService.cs
index ad2a7b8..df3df92 100644
--- a/web/Services/ApplicationAuditService.cs
+++ b/web/Services/ApplicationAuditService.cs
@@ -177,6 +177,22 @@ public sealed class ApplicationAuditService
             .ToListAsync(cancellationToken);
     }
 
+    /// <summary>Every event of one record, newest first: a battery's whole timeline (redesign R6, the cluster admin's History).</summary>
+    public async Task<IReadOnlyList<BsonDocument>> ListEntityTimelineAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
+    {
+        if (GetApplicationAuditEventsCollection() is not { } collection)
+        {
+            return [];
+        }
+
+        var filter = Builders<BsonDocument>.Filter;
+        return await collection
+            .Find(filter.Eq("entityType", entityType) & filter.Eq("entityId", entityId))
+            .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
+            .Limit(5000)
+            .ToListAsync(cancellationToken);
+    }
+
     public async Task<IReadOnlyList<BsonDocument>> ListApplicationAuditEventsAsync(
         ApplicationAuditFilter filter,
         CancellationToken cancellationToken = default)
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index bb73939..9cb463a 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -140,6 +140,9 @@
             <div data-section="versions" hidden="@Hidden("versions")">
                 @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
             </div>
+            <div data-section="history" hidden="@Hidden("history")">
+                @await Html.PartialAsync("~/Views/ClusterAdmin/_HistoryCard.cshtml", Model.History)
+            </div>
         </div>
     </div>
 
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 0460435..5c393a5 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3548,3 +3548,31 @@ main .bp-field-marker.is-published {
 .bp-bpanel .bp-console-table td[data-label="Published"] {
   white-space: normal;
 }
+
+/* The cluster admin's History (redesign R6): one line per event, its time first. */
+.bp-cluster-history {
+  display: grid;
+  gap: 10px;
+  margin: 16px 0 0;
+  padding: 0;
+  list-style: none;
+}
+
+.bp-cluster-history li {
+  display: grid;
+  grid-template-columns: 150px minmax(0, 1fr);
+  gap: 4px 14px;
+  padding-top: 10px;
+  border-top: 1px solid var(--bp-border);
+}
+
+.bp-cluster-history-when {
+  color: var(--bp-muted);
+  font-size: 0.85rem;
+}
+
+@media (max-width: 575.98px) {
+  .bp-cluster-history li {
+    grid-template-columns: 1fr;
+  }
+}
```

`web/Dpp/Audit/ClusterHistory.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Audit;

/// <summary>
/// The cluster admin's read-only History of a battery (redesign R6, the owner's choice 9; the managing session's privacy
/// rules). It shows only what happened while the battery was in their cluster, and its moves in and out; never more than
/// their battery page shows them: no other cluster's name, no team member's name, no value they may not read, no mark
/// reason. The log doesn't store an actor's cluster, so the battery's cluster at each moment is worked out from its
/// cluster changes and accepted hand-overs, back from the cluster it is in now.
/// </summary>
public sealed class ClusterHistory(ApplicationAuditService audit, AuditLines lines)
{
    /// <summary>The most lines shown, newest first.</summary>
    public const int Shown = 50;

    /// <summary>Events whose own line says nothing about another cluster or the team's internals.</summary>
    private static readonly HashSet<string> Described =
    [
        SavePipeline.AuditEventType,
        StatusChangeService.ChangedEvent,
        StatusChangeService.NoteAddedEvent,
        DocumentService.UploadedEvent,
        PublicationService.PublishedEvent,
        PublicationService.InactiveEvent,
        BatteryPages.ModulesSetEvent,
        PackCreationService.AuditEventType,
    ];

    /// <summary>Who did it, as a cluster admin may read it: their own cluster's people by name, everyone else by role.</summary>
    public static string Who(string actor, string role) => role switch
    {
        nameof(SenderKind.ClusterAdmin) => actor,
        nameof(SenderKind.BatterySystemFeed) => "Your battery system",
        nameof(SenderKind.Team) => "The manufacturer's team",
        nameof(SenderKind.MarketSurveillance) => "Market surveillance",
        _ => "The app",
    };

    public async Task<IReadOnlyList<AuditLine>> ForAsync(BatteryPack pack, IReadOnlySet<string> clusterIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(clusterIds);
        var events = await audit.ListEntityTimelineAsync("dpp-pack", pack.Id, cancellationToken);
        var picked = Pick(events, pack.ClusterId, clusterIds).Take(Shown).ToArray();
        var described = new Queue<AuditLine>(await lines.DescribeAsync(picked.Where(item => item.Own is null).Select(item => item.Event).ToArray(), cancellationToken));
        return picked.Select(item => item.Own ?? AfterOnly(described.Dequeue())).ToArray(); // the described lines come back in order
    }

    /// <summary>
    /// A line with the new values only: a value before a change may date from before the battery was theirs, e.g. the
    /// first change after a hand-over in, so "before" is never shown.
    /// </summary>
    public static AuditLine AfterOnly(AuditLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var change = line.Change is { } words && words.LastIndexOf(" → ", StringComparison.Ordinal) is var arrow and >= 0 ? words[(arrow + 3)..] : line.Change;
        return line with { Change = change, Values = line.Values.Select(row => row with { Before = string.Empty }).ToArray() };
    }

    /// <summary>
    /// The events a cluster admin of <paramref name="mine"/> may see, newest first, each a cleaned copy (the team's audit
    /// log reads the same documents) or a line of its own for a move, a hand-over or a mark.
    /// </summary>
    public static IEnumerable<(BsonDocument Event, AuditLine? Own)> Pick(IReadOnlyList<BsonDocument> newestFirst, string? clusterNow, IReadOnlySet<string> mine)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        ArgumentNullException.ThrowIfNull(mine);
        bool Mine(string? cluster) => cluster is not null && mine.Contains(cluster);
        var cluster = clusterNow; // the battery's cluster at the moment of the event being read, going back in time
        foreach (var original in newestFirst)
        {
            var type = BsonHelpers.GetString(original, "eventType");
            var metadata = original.TryGetValue("metadata", out var value) && value.IsBsonDocument ? value.AsBsonDocument : new BsonDocument();
            var from = metadata.TryGetValue("from", out var f) && f.IsString ? f.AsString : null;
            var to = metadata.TryGetValue("to", out var t) && t.IsString ? t.AsString : null;
            var role = BsonHelpers.GetString(original, "actorRole");
            var who = Who(BsonHelpers.GetString(original, "actor"), role);
            var accepted = metadata.TryGetValue("accepted", out var a) && a.IsBoolean && a.AsBoolean;

            // A hand-over is asked and cancelled by the sending cluster's admin and answered by the receiving one's: when
            // that side isn't theirs, the actor is "Another cluster", never its name or its people.
            string Side(bool theirSideIsMine) => role == nameof(SenderKind.ClusterAdmin) && !theirSideIsMine ? "Another cluster" : who;

            if (type == BatteryPages.ClusterChangedEvent || (type == HandOverService.AnsweredEvent && accepted))
            {
                if (Mine(from) || Mine(to))
                {
                    var words = type == BatteryPages.ClusterChangedEvent
                        ? Mine(to) ? " moved the battery to your cluster" : " moved the battery to another cluster"
                        : Mine(to) ? " accepted the battery's hand-over to your cluster" : " accepted the battery's hand-over: it moved to another cluster";
                    yield return (original, Line(original, type == BatteryPages.ClusterChangedEvent ? who : Side(Mine(to)), words));
                }

                cluster = from;
                continue;
            }

            if (type is HandOverService.AskedEvent or HandOverService.CancelledEvent or HandOverService.AnsweredEvent)
            {
                if (Mine(from) || Mine(to))
                {
                    var words = type switch
                    {
                        HandOverService.AskedEvent => Mine(from) ? " asked another cluster to accept the battery" : " asked your cluster to accept the battery",
                        HandOverService.CancelledEvent => " cancelled the battery's hand-over",
                        _ => " declined the battery's hand-over",
                    };
                    yield return (original, Line(original, Side(type == HandOverService.AnsweredEvent ? Mine(to) : Mine(from)), words));
                }

                continue;
            }

            if (!Mine(cluster))
            {
                continue;
            }

            if (type is PassportMarks.InvalidEvent or PassportMarks.DeletionEvent or PassportMarks.UndoneEvent)
            {
                // The fact and the day only: a mark's reason is the team's and market surveillance's (decision 34).
                var words = type switch
                {
                    PassportMarks.InvalidEvent => " marked the passport invalid",
                    PassportMarks.DeletionEvent => " marked the passport for deletion",
                    _ => " undid the passport's mark: it is active again",
                };
                yield return (original, Line(original, who, words));
                continue;
            }

            if (!Described.Contains(type))
            {
                continue; // e.g. a refused publish or upload: the team's own working
            }

            var copy = original.DeepClone().AsBsonDocument;
            copy["actor"] = who; // no "@": the line builder looks up no one's name
            copy["actorRole"] = string.Empty;
            if (type == SavePipeline.AuditEventType)
            {
                var keys = metadata.TryGetValue("keys", out var k) && k.IsBsonArray ? k.AsBsonArray : [];
                var readable = new BsonArray(keys.OfType<BsonDocument>().Where(key => Readable(BsonHelpers.GetString(key, "key"))));
                if (readable.Count == 0)
                {
                    continue; // only values they may not read changed
                }

                copy["metadata"]["keys"] = readable;
            }

            yield return (copy, null);
        }
    }

    /// <summary>A value a cluster admin may read: public, purchaser or shared data; not the authorities' (#7, #12, #13, #29).</summary>
    private static bool Readable(string property) =>
        AttributeCatalog.All.FirstOrDefault(attribute => attribute.Property == property) is { } attribute && attribute.Audience != Audience.Authorities;

    private static AuditLine Line(BsonDocument original, string who, string words)
    {
        var when = DateTimeOffset.TryParse(BsonHelpers.GetString(original, "createdAt"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : (DateTimeOffset?)null;
        return new AuditLine(BsonHelpers.GetString(original, "eventId") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N"), BsonHelpers.GetString(original, "eventType"), when, AuditIcon.Other, false,
            [new AuditWord(who, Strong: true), new AuditWord(words)], null, [], [], [], string.Empty);
    }
}
```

`web/Views/ClusterAdmin/_HistoryCard.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Audit
@using BatteryPassWeb.Dpp.Pages
@model IReadOnlyList<AuditLine>
@* The cluster admin's History (redesign R6, the owner's choice 9): what happened while the battery was in their cluster,
   and its moves in and out, newest first. Read-only; the sentence, its values and its time only, never the team's
   technical details. *@
<section class="bp-card bp-console-form-card bp-battery-card" id="history">
    <span class="bp-summary-title">History <em class="bp-field-marker">Read only</em></span>
    <h2>@(Model.Count == 0 ? "Nothing yet" : Model.Count >= ClusterHistory.Shown ? $"The latest {ClusterHistory.Shown} events" : Words.Count(Model.Count, "event"))</h2>
    <p class="mb-0">What happened to this battery while it was in your cluster, and when it moved in or out. Others are named by their role: the manufacturer's team, market surveillance, another cluster.</p>
    @if (Model.Count > 0)
    {
        <ol class="bp-cluster-history">
            @foreach (var line in Model)
            {
                <li>
                    <span class="bp-cluster-history-when">@(line.When is { } when ? ValueWords.Time(when.UtcDateTime) : "—")</span>
                    <span>
                        @foreach (var word in line.Sentence)
                        {
                            if (word.Strong)
                            {
                                <strong>@word.Text</strong>
                            }
                            else
                            {
                                @word.Text
                            }
                        }
                        @if (line.Change is { } change)
                        {
                            <span class="bp-batteries-muted"> · @change</span>
                        }
                        @if (line.Values.Count > 0)
                        {
                            <span class="d-block small text-secondary">@string.Join("; ", line.Values.Select(row => $"{row.Name}: {row.After}"))</span>
                        }
                    </span>
                </li>
            }
        </ol>
    }
</section>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPageLayoutTests | FullyQualifiedName~BatterySectionsTests | FullyQualifiedName~ClusterHistoryTests"`
Expected: `Passed!  - Failed:     0, Passed:    26`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs BatteryPassWeb.Tests/Dpp/BatterySectionsTests.cs BatteryPassWeb.Tests/Dpp/ClusterHistoryTests.cs web/Dpp/Audit/ClusterHistory.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Services/ApplicationAuditService.cs web/Views/ClusterAdmin/Battery.cshtml web/Views/ClusterAdmin/_HistoryCard.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): the cluster admin's read-only History, while the battery was theirs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 17: A window opened from More closes the menu

Opening a window from a More menu closes the menu; closing the window brings focus back to "More"; Escape closes an open menu.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/MoreMenuClosesTests.cs`
- Modify: `web/wwwroot/js/bp-record-editor.js`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/MoreMenuClosesTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6, the owner's item 10 (a bug found testing round 2): More → "Mark as invalid or for deletion…" → Cancel
/// left the More menu open. A window opened from the menu now closes it, focus comes back to "More" when the window
/// closes (Cancel or Escape), and Escape closes an open menu too. Only the More menu: the header's "More details" fold
/// also holds a window's button (the cluster's "Change") and stays as it is.
/// </summary>
public sealed class MoreMenuClosesTests
{
    [Fact]
    public void AWindowOpenedFromMore_ShouldCloseTheMenu_AndGiveFocusBack()
    {
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-record-editor.js"));

        Assert.Contains("const menu = button.closest('details.bp-more');", script, StringComparison.Ordinal);
        Assert.Contains("menu.open = false;", script, StringComparison.Ordinal);
        Assert.Contains("dialog?.addEventListener('close', () => menu.querySelector(':scope > summary')?.focus(), { once: true });", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('details.bp-more[open]').forEach((menu) => {", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMoreMenusWindows_ShouldOpenThroughTheSharedOpener()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var menu = page[page.IndexOf("<details class=\"bp-more\">", StringComparison.Ordinal)..page.IndexOf("</details>", page.IndexOf("<details class=\"bp-more\">", StringComparison.Ordinal), StringComparison.Ordinal)];

        Assert.Contains("data-open-dialog=\"mark-dialog\"", menu, StringComparison.Ordinal);
        Assert.Contains("<script src=\"~/js/bp-record-editor.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~MoreMenuClosesTests"`
Expected: `Failed:     1, Passed:     1` (failing: `MoreMenuClosesTests.AWindowOpenedFromMore_ShouldCloseTheMenu_AndGiveFocusBack`).

- [ ] **Step 3: Write the code**

Patch `r6-task17-code.patch` (`web/wwwroot/js/bp-record-editor.js`). Save the block as `../r6-task17-code.patch`, outside the worktree, and run `git apply ../r6-task17-code.patch` from the worktree root:

```diff
diff --git a/web/wwwroot/js/bp-record-editor.js b/web/wwwroot/js/bp-record-editor.js
index c8cf8c3..98807e1 100644
--- a/web/wwwroot/js/bp-record-editor.js
+++ b/web/wwwroot/js/bp-record-editor.js
@@ -71,7 +71,27 @@
     // its Cancel button (formmethod="dialog") closes it without sending anything.
     document.querySelectorAll('[data-open-dialog]').forEach((button) => {
         const dialog = document.getElementById(button.dataset.openDialog);
-        button.addEventListener('click', () => dialog?.showModal());
+        button.addEventListener('click', () => {
+            // Opened from the More menu: the menu closes, and focus comes back to "More" when the window does (R6).
+            const menu = button.closest('details.bp-more');
+            if (menu) {
+                menu.open = false;
+                dialog?.addEventListener('close', () => menu.querySelector(':scope > summary')?.focus(), { once: true });
+            }
+
+            dialog?.showModal();
+        });
+    });
+    // Escape closes an open More menu as it closes a window, and leaves focus on "More" (redesign R6).
+    document.addEventListener('keydown', (event) => {
+        if (event.key !== 'Escape') {
+            return;
+        }
+
+        document.querySelectorAll('details.bp-more[open]').forEach((menu) => {
+            menu.open = false;
+            menu.querySelector(':scope > summary')?.focus();
+        });
     });
     // A dialog marked data-open-on-load="true" opens at once, e.g. the "Hand over" window once the cluster is named (step
     // 4d). (Razor writes data- attributes even when false, so the value says it.)
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~MoreMenuClosesTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/MoreMenuClosesTests.cs web/wwwroot/js/bp-record-editor.js
git commit -m "fix(dpp): a window opened from More closes the menu; focus comes back; Escape closes the menu" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 18: The Live values tab whenever there is a live value

The tab was missing for two reasons: it showed only with a reading in the last 7 days, and a passport marked invalid or for deletion was given no live values at all (only an active version was). It now shows whenever the reader may see a live value the battery has: the latest of each with its date, a chart when the week has 2 or more readings, and "No reading in the last 7 days" otherwise. A marked passport keeps the battery's last live values, frozen by the mark; an archived or replaced one still has none.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/MarkedPassportLiveValuesTests.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Dpp/Assembly/PassportStaticCore.cs`
- Modify: `web/Dpp/Pages/LiveTrends.cs`
- Modify: `web/Dpp/Publication/PassportReader.cs`
- Modify: `web/Views/Passport/_LiveHistory.cshtml`
- Modify: `web/Views/Passport/_PassportTabs.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task18-tests.patch` (`BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs`). Save the block as `../r6-task18-tests.patch`, outside the worktree, and run `git apply ../r6-task18-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs b/BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs
index 4436c5b..fcd066b 100644
--- a/BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs
@@ -77,8 +77,8 @@ public sealed class LiveChartsTests
         Assert.DoesNotContain("bp-passport-module-list", detail, StringComparison.Ordinal);
         Assert.DoesNotContain("<partial name=\"_LiveHistory\"", detail, StringComparison.Ordinal);
         Assert.Contains("<partial name=\"_PassportTabs\" model=\"Model\" view-data=\"ViewData\" />", detail, StringComparison.Ordinal);
-        Assert.Contains("<summary>All readings (@Model.Count)</summary>", live, StringComparison.Ordinal);
-        Assert.Contains("var charts = LiveTrends.Charts(Model, Clock.GetUtcNow().UtcDateTime, Reading);", live, StringComparison.Ordinal);
+        Assert.Contains("<summary>All readings (@Model.History.Count)</summary>", live, StringComparison.Ordinal); // the tab's own model since R6
+        Assert.Contains("var charts = LiveTrends.ForTab(Model.Latest, Model.History, Clock.GetUtcNow().UtcDateTime, Reading);", live, StringComparison.Ordinal);
         Assert.Contains("a[href^=\"#tab-\"]", Read("wwwroot", "js", "bp-passport-links.js"), StringComparison.Ordinal);
     }
 }
```

`BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's item 11 and the managing session's rule: the Live values tab was left out whenever the last
/// 7 days had no readings (demo batteries without a week of readings, or a demo reset days ago). It now shows to any
/// reader allowed to see a live value whenever the battery has one: the latest value of each with its date, even older
/// than 7 days; the charts where the week has readings; a calm note when the week had none.
/// </summary>
public sealed class LiveTabRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static ShownValue Latest(int number, string text, DateTime at) =>
        new(AttributeCatalog.ByNumber(number), [new ValueLine(null, text)], new ValueOrigin(at, "feed", Measured: true));

    private static LiveHistoryEntry Reading(int hoursAgo, decimal percent) =>
        new(AttributeCatalog.ByNumber(63), new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = percent }, Now.AddHours(-hoursAgo), Now.AddHours(-hoursAgo), "feed");

    [Fact]
    public void AValueWithNoReadingThisWeek_ShouldShowItsLatestValueAndDate()
    {
        var old = Now.AddDays(-12);

        var tile = Assert.Single(LiveTrends.ForTab([Latest(63, "61 %", old)], [], Now, _ => string.Empty));

        Assert.Equal("61 %", tile.Shown);
        Assert.Equal(0, tile.Readings);
        Assert.Null(tile.Line(300, 70));
        Assert.Equal($"Measured {ValueWords.Time(old)} · {PassportPages.SourceWord("feed")}", tile.Note);
    }

    [Fact]
    public void AValueWithAWeekOfReadings_ShouldShowItsLine_AndTheLatestValue()
    {
        var tile = Assert.Single(LiveTrends.ForTab([Latest(63, "61 %", Now)], [Reading(0, 61), Reading(48, 80), Reading(400, 20)], Now, _ => string.Empty));

        Assert.Equal("61 %", tile.Shown);
        Assert.Equal(2, tile.Readings); // the reading 400 hours ago is outside the week
        Assert.NotNull(tile.Line(300, 70));
    }

    [Fact]
    public void OnlyLiveValues_ShouldBeTiles() =>
        Assert.Empty(LiveTrends.ForTab([Latest(21, "320 kg", Now)], [], Now, _ => string.Empty)); // #21 battery mass is fixed

    [Fact]
    public void TheTab_ShouldShowWheneverTheReaderMaySeeALiveValue_OnTheLatestPassport()
    {
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml"));
        var live = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_LiveHistory.cshtml"));
        var controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "PassportController.cs"));

        Assert.Contains("var live = Model.Current is null && liveValues.Length > 0", tabs, StringComparison.Ordinal);
        Assert.Contains("Model.Audiences.SelectMany(section => section.Values).Where(value => value.Attribute.Change == BatteryPassWeb.Dpp.Catalog.ChangeRule.Live)", tabs, StringComparison.Ordinal); // only what this reader may see
        Assert.Contains("0 => \"No reading in the last 7 days\"", live, StringComparison.Ordinal);
        Assert.Contains("if (liveHistory is not null && page.Current is null)", controller, StringComparison.Ordinal);
    }
}
```

`BatteryPassWeb.Tests/Dpp/MarkedPassportLiveValuesTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's item 11, the second cause: a passport marked invalid or for deletion is still the battery's
/// newest passport, but only an active version was given the battery's live values, so a marked passport had none and no
/// Live values tab (DEMO-C7M-0003, DEMO-CORE-0003). It keeps the battery's last live values now, frozen by the mark. An
/// older version of the same passport (archived) or a passport replaced by a newer one (inactive) still has none.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkedPassportLiveValuesTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    private static bool HasStateOfCharge(JsonObject passport) =>
        passport.ToJsonString().Contains("\"stateOfChargeSoC\"", StringComparison.Ordinal);

    [Fact]
    public async Task AnInvalidPassport_ShouldKeepTheBatterysLastLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.True((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 40 }, "form"))).Saved);
        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(Team, rig.PackId, "Wrong carbon footprint figures"));
        var version = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;

        var passport = (await new PassportReader(rig.Store).VersionAsync(version.DigitalProductPassportId))!;

        Assert.Equal(DppStatuses.Invalid, version.DppStatus);
        Assert.True(HasStateOfCharge(passport.Passport));
    }

    [Fact]
    public async Task APassportMarkedForDeletion_ShouldKeepTheBatterysLastLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
        Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(Team, rig.PackId));
        var versions = await rig.Store.ListVersionsAsync(rig.PackId);
        var latest = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;

        var reader = new PassportReader(rig.Store);

        Assert.Equal(DppStatuses.MarkedForDeletion, latest.DppStatus);
        Assert.True(HasStateOfCharge((await reader.VersionAsync(latest.DigitalProductPassportId))!.Passport));
        Assert.All(versions.Where(version => version.DppStatus == DppStatuses.Archived), archived => Assert.False(HasStateOfCharge(reader.VersionAsync(archived.DigitalProductPassportId).GetAwaiter().GetResult()!.Passport)));
    }

    [Fact]
    public async Task AReplacedPassport_ShouldStillHaveNoLiveValues()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;

        var passport = (await new PassportReader(rig.Store).VersionAsync(old.DigitalProductPassportId))!;

        Assert.Equal(DppStatuses.Inactive, old.DppStatus);
        Assert.False(HasStateOfCharge(passport.Passport));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~LiveChartsTests | FullyQualifiedName~LiveTabRuleTests | FullyQualifiedName~MarkedPassportLiveValuesTests"`
Expected: the build fails with CS0117 (for example `error CS0117 'LiveTrends' does not contain a definition for 'ForTab'`).

- [ ] **Step 3: Write the code**

Patch `r6-task18-code.patch` (`web/Controllers/PassportController.cs`, `web/Dpp/Assembly/PassportStaticCore.cs`, `web/Dpp/Pages/LiveTrends.cs`, `web/Dpp/Publication/PassportReader.cs`, `web/Views/Passport/_LiveHistory.cshtml`, `web/Views/Passport/_PassportTabs.cshtml`). Save the block as `../r6-task18-code.patch`, outside the worktree, and run `git apply ../r6-task18-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/PassportController.cs b/web/Controllers/PassportController.cs
index 8e3daa5..c70b0da 100644
--- a/web/Controllers/PassportController.cs
+++ b/web/Controllers/PassportController.cs
@@ -70,7 +70,7 @@ public sealed class PassportController(DppReaders readers, PassportPages pages)
 
         // The current version's live values over the last week, for those who may see them (decision 30), from the same
         // change log and the same rule as the API's history.
-        if (liveHistory is not null && page.IsCurrent)
+        if (liveHistory is not null && page.Current is null) // the battery's latest passport, active or not (redesign R6)
         {
             ViewData["LiveHistory"] = await liveHistory.HistoryAsync(reader, page.Version.PackId, BatteryApi.MaxHistoryHours, cancellationToken);
         }
diff --git a/web/Dpp/Assembly/PassportStaticCore.cs b/web/Dpp/Assembly/PassportStaticCore.cs
index 31b5907..9d88646 100644
--- a/web/Dpp/Assembly/PassportStaticCore.cs
+++ b/web/Dpp/Assembly/PassportStaticCore.cs
@@ -39,9 +39,10 @@ public static class PassportStaticCore
     }
 
     /// <summary>
-    /// The passport readers see: the signed core with the version's #2 and #4 and, for the active version, the pack's
-    /// live values. Archived versions get no live values, because live values aren't versioned; so an archived passport
-    /// lacks the required live set and doesn't pass the schema's required lists. That is expected.
+    /// The passport readers see: the signed core with the version's #2 and #4 and, for the battery's newest passport (active,
+    /// or frozen by a mark), the pack's live values. Archived and inactive versions get no live values, because live values
+    /// aren't versioned; so such a passport lacks the required live set and doesn't pass the schema's required lists. That
+    /// is expected.
     /// </summary>
     public static JsonObject Compose(JsonNode core, string dppStatus, DateTime lastUpdatedUtc, IReadOnlyDictionary<string, AttributeValue>? liveValues)
     {
diff --git a/web/Dpp/Pages/LiveTrends.cs b/web/Dpp/Pages/LiveTrends.cs
index 0108115..1baf52a 100644
--- a/web/Dpp/Pages/LiveTrends.cs
+++ b/web/Dpp/Pages/LiveTrends.cs
@@ -9,6 +9,9 @@ using MongoDB.Bson;
 
 namespace BatteryPassWeb.Dpp.Pages;
 
+/// <summary>The detail page's Live values tab: the reader's latest live values and the last week's readings (redesign R6).</summary>
+public sealed record LiveTab(IReadOnlyList<ShownValue> Latest, IReadOnlyList<LiveHistoryEntry> History);
+
 /// <summary>One reading of a tile's trend line: when it was measured, and the value.</summary>
 public sealed record TrendPoint(DateTime AtUtc, decimal Value);
 
@@ -146,6 +149,42 @@ public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock
             .ToArray();
     }
 
+    /// <summary>
+    /// The detail page's Live values tab (redesign R6, the managing session's rule): one tile per live value the reader
+    /// may see (<paramref name="latest"/>, from their passport tabs), each with its latest reading and its date, even when
+    /// that is older than the last 7 days, and the week's line when the week had readings (<paramref name="history"/>).
+    /// </summary>
+    public static IReadOnlyList<LiveTile> ForTab(IReadOnlyList<ShownValue> latest, IReadOnlyList<LiveHistoryEntry> history, DateTime nowUtc, Func<LiveHistoryEntry, string> words)
+    {
+        ArgumentNullException.ThrowIfNull(latest);
+        ArgumentNullException.ThrowIfNull(history);
+        var week = Charts(history.Where(entry => entry.MeasuredAtUtc >= nowUtc.AddHours(-Hours)).ToArray(), nowUtc, words)
+            .ToDictionary(chart => chart.Property, StringComparer.Ordinal);
+        var order = AttributeCatalog.All.Select((attribute, index) => (attribute.Number, index)).ToDictionary(pair => pair.Number, pair => pair.index);
+        return latest
+            .Where(value => value.Attribute.Change == ChangeRule.Live)
+            .OrderBy(value => order.GetValueOrDefault(value.Attribute.Number, int.MaxValue))
+            .Select(value =>
+            {
+                var unit = value.Attribute.Unit ?? string.Empty;
+                var name = value.Attribute.IsExtension ? $"{value.Attribute.Label} {value.Attribute.Name}" : value.Attribute.Name;
+                var note = value.Origin is { } origin
+                    ? $"{(origin.Measured ? "Measured" : "Entered")} {ValueWords.Time(origin.GeneratedAtUtc)} · {PassportPages.SourceWord(origin.Source)}"
+                    : "When it was measured isn't known";
+                var shown = string.Join(", ", value.Lines.Select(line => line.Text));
+                return week.TryGetValue(value.Attribute.Property, out var chart)
+                    ? chart with { Value = null, Note = note, Latest = shown }
+                    : new LiveTile(unit.Length == 0 ? name : $"{name} ({unit})", value.Attribute.Property, null, unit, note, [])
+                    {
+                        Latest = shown,
+                        Readings = 0,
+                        FromUtc = nowUtc.AddHours(-Hours),
+                        ToUtc = nowUtc,
+                    };
+            })
+            .ToArray();
+    }
+
     public async Task<IReadOnlyList<LiveTile>> TilesAsync(BatteryPack pack, BatteryModel model, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(pack);
diff --git a/web/Dpp/Publication/PassportReader.cs b/web/Dpp/Publication/PassportReader.cs
index baa64c5..58d3165 100644
--- a/web/Dpp/Publication/PassportReader.cs
+++ b/web/Dpp/Publication/PassportReader.cs
@@ -40,9 +40,17 @@ public sealed class PassportReader(DppStore store)
 
     private async Task<PublishedPassport> ComposeAsync(PassportVersion version, CancellationToken cancellationToken)
     {
-        var liveValues = version.DppStatus == DppStatuses.Active
+        var liveValues = IsNewest(version.DppStatus)
             ? (await store.GetAsync<BatteryPack>(version.PackId, cancellationToken))?.Values
             : null;
         return new PublishedPassport(version, PassportStaticCore.Compose(version.StaticCore, version.DppStatus, version.LastUpdatedUtc, liveValues));
     }
+
+    /// <summary>
+    /// The battery's newest passport carries its live values: the active one, or one a mark has frozen (invalid, marked
+    /// for deletion), which keeps the last values it had (redesign R6). An older version of the same passport (archived)
+    /// and a passport a newer one replaced (inactive) have none.
+    /// </summary>
+    private static bool IsNewest(string status) =>
+        status is DppStatuses.Active or DppStatuses.Invalid or DppStatuses.MarkedForDeletion;
 }
diff --git a/web/Views/Passport/_LiveHistory.cshtml b/web/Views/Passport/_LiveHistory.cshtml
index ac8a8e6..5d93751 100644
--- a/web/Views/Passport/_LiveHistory.cshtml
+++ b/web/Views/Passport/_LiveHistory.cshtml
@@ -3,7 +3,7 @@
 @using BatteryPassWeb.Dpp.Pages
 @inject AttributeForms Forms
 @inject TimeProvider Clock
-@model IReadOnlyList<LiveHistoryEntry>
+@model LiveTab
 @{
     // The live values of the last week (decision 30), as this reader may see them: one small chart per value (redesign
     // R5, the owner's item 7), then every reading, newest first, folded. The detail page's "Live values" tab (item 8).
@@ -11,10 +11,10 @@
     const int Width = 300;
     const int Height = 70;
     string Reading(LiveHistoryEntry entry) => string.Join(", ", ValueWords.Lines(Forms.For(entry.Attribute), entry.Value).Select(line => line.Text));
-    var charts = LiveTrends.Charts(Model, Clock.GetUtcNow().UtcDateTime, Reading);
+    var charts = LiveTrends.ForTab(Model.Latest, Model.History, Clock.GetUtcNow().UtcDateTime, Reading);
 }
 <h2 class="h5 mb-2">The last 7 days</h2>
-<p class="bp-help-text">Each value now, and how it went over the last week. Every reading, with when it was measured and how it arrived, is under the charts.</p>
+<p class="bp-help-text">Each value's latest reading and when it was taken, and how it went over the last week when the battery sent readings. Every reading of the week, with when it was measured and how it arrived, is under the charts.</p>
 <div class="bp-live-charts">
     @foreach (var chart in charts)
     {
@@ -35,20 +35,22 @@
             }
             else
             {
-                <span class="bp-live-chart-flat">@(chart.Readings == 1 ? "One reading this week" : $"{Words.Count(chart.Readings, "reading")} this week")</span>
+                <span class="bp-live-chart-flat">@(chart.Readings switch { 0 => "No reading in the last 7 days", 1 => "One reading this week", _ => $"{Words.Count(chart.Readings, "reading")} this week" })</span>
             }
             <small>@chart.Note</small>
         </figure>
     }
 </div>
+@if (Model.History.Count > 0)
+{
 <details class="bp-live-all">
-    <summary>All readings (@Model.Count)</summary>
-    <p class="text-secondary small mt-2">Newest first.@(Model.Count > Shown ? $" The latest {Shown} of {Model.Count}." : string.Empty)</p>
+    <summary>All readings (@Model.History.Count)</summary>
+    <p class="text-secondary small mt-2">Newest first.@(Model.History.Count > Shown ? $" The latest {Shown} of {Model.History.Count}." : string.Empty)</p>
     <div class="bp-table-scroll">
         <table class="table align-middle bp-console-table bp-mobile-card-table mb-0">
             <thead><tr><th>Measured</th><th>Value</th><th>Reading</th><th>Came from</th></tr></thead>
             <tbody>
-                @foreach (var entry in Model.Take(Shown))
+                @foreach (var entry in Model.History.Take(Shown))
                 {
                     <tr>
                         <td data-label="Measured">@ValueWords.Time(entry.MeasuredAtUtc)</td>
@@ -61,3 +63,4 @@
         </table>
     </div>
 </details>
+}
diff --git a/web/Views/Passport/_PassportTabs.cshtml b/web/Views/Passport/_PassportTabs.cshtml
index 6a406cb..857bb79 100644
--- a/web/Views/Passport/_PassportTabs.cshtml
+++ b/web/Views/Passport/_PassportTabs.cshtml
@@ -8,7 +8,12 @@
         .ToArray();
     // Live values and Modules are tabs too (redesign R5, the owner's item 8), after the audiences' tabs, each only for a
     // reader it has something for: the live history is cut for the reader, the modules are null for those who may not see them.
-    var live = Model.IsCurrent && ViewData["LiveHistory"] is IReadOnlyList<BatteryPassWeb.Dpp.Api.LiveHistoryEntry> { Count: > 0 } history ? history : null;
+    // Live values: whenever this reader may see one on the battery's latest passport (redesign R6), with the last week's
+    // readings when there are any; an older version leads to the current one instead.
+    var liveValues = Model.Audiences.SelectMany(section => section.Values).Where(value => value.Attribute.Change == BatteryPassWeb.Dpp.Catalog.ChangeRule.Live).ToArray();
+    var live = Model.Current is null && liveValues.Length > 0
+        ? new LiveTab(liveValues, ViewData["LiveHistory"] as IReadOnlyList<BatteryPassWeb.Dpp.Api.LiveHistoryEntry> ?? [])
+        : null;
     var modules = Model.Modules is { Count: > 0 } listed ? listed : null;
     var extra = new List<(string Key, string Tab)>();
     if (live is not null)
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~LiveChartsTests | FullyQualifiedName~LiveTabRuleTests | FullyQualifiedName~MarkedPassportLiveValuesTests"`
Expected: `Passed!  - Failed:     0, Passed:    11`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs BatteryPassWeb.Tests/Dpp/MarkedPassportLiveValuesTests.cs web/Controllers/PassportController.cs web/Dpp/Assembly/PassportStaticCore.cs web/Dpp/Pages/LiveTrends.cs web/Dpp/Publication/PassportReader.cs web/Views/Passport/_LiveHistory.cshtml web/Views/Passport/_PassportTabs.cshtml
git commit -m "fix(dpp): the Live values tab shows whenever the reader may see a live value, with its latest date" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 19: Passports of this battery

A battery placed on the market again lists its passports oldest first, the newest marked, each with its battery ID, status, dates and a link: in full for the team (battery page and history), on the detail page's Passport tab for the team and market surveillance, public facts only for the cluster admin. A battery's history lists its other passports' histories. Public readers keep "replaces an earlier passport".

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs`
- Test (create): `BatteryPassWeb.Tests/Demo/ChainHistoryLinksTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/PassportChainTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Create: `web/Dpp/Pages/PassportChains.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`
- Modify: `web/Views/Admin/AuditLog.cshtml`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`
- Modify: `web/Views/Passport/_PassportTabs.cshtml`
- Create: `web/Views/Shared/_PassportChain.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task19-tests.patch` (`BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs`). Save the block as `../r6-task19-tests.patch`, outside the worktree, and run `git apply ../r6-task19-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs b/BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs
index cc88a34..2ba7cbd 100644
--- a/BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs
+++ b/BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs
@@ -30,7 +30,8 @@ public sealed class BatteryHistoryLinkTests(MongoFixture mongo)
         var battery = Assert.IsType<AuditBattery>(history.ViewData[AdminController.AuditBatteryKey]);
         var whole = Assert.IsType<ViewResult>(await DemoCardTests.Controller(rig).AuditLog("Repaired", null, null, null, null, null, null, null, lines, CancellationToken.None));
 
-        Assert.Equal(new AuditBattery(serial, BatteryPages.BatteryHref(pack.Id)), battery);
+        Assert.Equal((serial, BatteryPages.BatteryHref(pack.Id)), (battery.SerialNumber, battery.Href));
+        Assert.Empty(battery.OtherPassports ?? []); // one passport only; a battery placed on the market again links its others (R6 T19)
         Assert.False(whole.ViewData.ContainsKey(AdminController.AuditBatteryKey)); // an ordinary search names no battery
     }
 
```

`BatteryPassWeb.Tests/Demo/ChainHistoryLinksTests.cs`:

```csharp
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Mvc;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Redesign R6, the owner's item 12: a battery's History page links its other passports' histories when it was placed
/// on the market again (the demo DEMO-C13M-0001).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ChainHistoryLinksTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheNewestPassportsHistory_ShouldLinkTheEarlierOnes()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var packs = (await rig.Store.ListAsync<BatteryPack>()).Where(pack => pack.Values["batterySerialNumber"].Value!.GetValue<string>() == "DEMO-C13M-0001").ToArray();
        var newest = packs.Single(pack => pack.SuccessorOf is not null && packs.All(other => other.SuccessorOf != pack.Id));
        var lines = rig.Services.GetRequiredService<AuditLines>();

        var view = Assert.IsType<ViewResult>(await DemoCardTests.Controller(rig).AuditLog(newest.Id, null, null, null, null, null, null, null, lines, CancellationToken.None));
        var battery = Assert.IsType<AuditBattery>(view.ViewData[AdminController.AuditBatteryKey]);

        Assert.Equal(packs.Length - 1, battery.OtherPassports!.Count);
        Assert.Equal("Passport 1", battery.OtherPassports[0].Label);
        Assert.Equal($"/admin/audit?q={Uri.EscapeDataString(newest.SuccessorOf!)}", battery.OtherPassports[^1].Href);
    }
}
```

`BatteryPassWeb.Tests/Dpp/PassportChainTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's item 12 ("Team + MSA full, admin limited"): a battery placed on the market again shows
/// "Passports of this battery", oldest first, each with its battery ID, status, from–to dates and a link. The team sees
/// every passport and its history; the detail page shows it to the team and market surveillance; the current cluster
/// admin sees public facts only; public readers keep "replaces an earlier passport".
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportChainTests(MongoFixture mongo)
{
    [Fact]
    public async Task ABatteryPlacedOnTheMarketAgain_ShouldListItsPassportsInOrder()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var successor = (await rig.Store.GetAsync<BatteryPack>(old.Succession!.SuccessorPackId!))!;

        var fromNew = await PassportChains.ForAsync(rig.Store, successor);
        var fromOld = await PassportChains.ForAsync(rig.Store, old);

        Assert.Equal([rig.PackId, successor.Id], fromNew.Select(link => link.PackId));
        Assert.Equal([1, 2], fromNew.Select(link => link.Number));
        Assert.Equal((DppStatuses.Inactive, false, false), (fromNew[0].Status, fromNew[0].IsThis, fromNew[0].IsNewest));
        Assert.NotNull(fromNew[0].ToUtc); // replaced: it has an end date
        Assert.Equal((DppStatuses.Active, true, true), (fromNew[1].Status, fromNew[1].IsThis, fromNew[1].IsNewest));
        Assert.Null(fromNew[1].ToUtc);
        Assert.True(fromOld[0].IsThis);
        var single = await PublicationTesting.PublishedRigAsync(mongo);
        Assert.Empty(await PassportChains.ForAsync(single.Store, (await single.Store.GetAsync<BatteryPack>(single.PackId))!)); // one passport: no list
    }

    [Fact]
    public async Task ADraftNewPassport_ShouldShowOnlyToTheTeam()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var (successorId, _) = await SuccessionTests.Service(rig).CreateSuccessorAsync(PublicationTesting.Team, rig.PackId, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.OperatorId);
        var links = await PassportChains.ForAsync(rig.Store, (await rig.Store.GetAsync<BatteryPack>(successorId!))!);

        Assert.Equal(2, new ChainView(links, ChainReader.Team).Shown.Count);
        Assert.Single(new ChainView(links, ChainReader.ClusterAdmin).Shown); // a draft has no public facts
        Assert.Single(new ChainView(links, ChainReader.Detail).Shown);
    }

    [Fact]
    public async Task TheDetailPage_ShouldShowTheChainToTheTeamAndMarketSurveillanceOnly()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var newId = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;
        var id = (await rig.Store.GetAsync<BatteryPack>(newId))!.Values["uniqueProductIdentifier"].Value!.GetValue<string>();
        var pages = PassportPagesTests.Pages(rig);

        var team = (await pages.DetailAsync(Reader.With("team@example.com", ReaderRole.Team), id))!;
        var surveillance = (await pages.DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), id))!;
        var member = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), id))!;
        var anyone = (await pages.SummaryAsync(Reader.Anonymous, id))!;

        Assert.Equal(2, team.Chain.Count);
        Assert.Equal(2, surveillance.Chain.Count);
        Assert.Empty(member.Chain);
        Assert.Empty(anyone.Chain); // public readers keep "replaces an earlier passport"
    }

    [Fact]
    public void EachReader_ShouldGetItsOwnLinks_AndAClusterAdminOnlyPublicFacts()
    {
        var chain = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_PassportChain.cshtml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var clusterPart = chain[chain.IndexOf("default:", StringComparison.Ordinal)..chain.IndexOf("}\n                    </span>", StringComparison.Ordinal)];

        Assert.Contains("<a href=\"@PassportPages.SummaryHref(link.Identifier)\">Public page</a>", clusterPart, StringComparison.Ordinal);
        Assert.DoesNotContain("audit", clusterPart, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryHref", clusterPart, StringComparison.Ordinal);
        Assert.DoesNotContain("Cluster", typeof(ChainLink).GetProperties().Select(property => property.Name).Aggregate((a, b) => a + b), StringComparison.Ordinal); // a link knows no cluster at all
        Assert.Contains("@await Html.PartialAsync(\"_PassportChain\", new ChainView(Model.Chain, ChainReader.ClusterAdmin))", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml")), StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_PassportChain\", new ChainView(Model.Chain, ChainReader.Team))", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml")), StringComparison.Ordinal);
        Assert.Contains("new ChainView(Model.Chain, ChainReader.Detail)", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml")), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryHistoryLinkTests | FullyQualifiedName~ChainHistoryLinksTests | FullyQualifiedName~PassportChainTests"`
Expected: the build fails with CS0103, CS0246, CS1061 (for example `error CS0103 The name 'ChainReader' does not exist in the current context`).

- [ ] **Step 3: Write the code**

Patch `r6-task19-code.patch` (`web/Controllers/AdminController.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Views/Admin/AuditLog.cshtml`, `web/Views/Batteries/Battery.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/Views/Passport/_PassportTabs.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r6-task19-code.patch`, outside the worktree, and run `git apply ../r6-task19-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
index 6cff0fa..7b47486 100644
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -75,9 +75,15 @@ public class AdminController : Controller
         // Opened from a battery's History link (q is its pack ID): say whose history it is, and lead back (redesign R6).
         if (!string.IsNullOrWhiteSpace(q) && await _dppStore.GetAsync<BatteryPack>(q.Trim(), cancellationToken) is { } pack)
         {
+            // Its other passports' histories, when it was placed on the market again (redesign R6, the owner's item 12).
+            var others = (await BatteryPassWeb.Dpp.Pages.PassportChains.ForAsync(_dppStore, pack, cancellationToken))
+                .Where(link => !link.IsThis)
+                .Select(link => new AuditBatteryLink($"Passport {link.Number}", $"/admin/audit?q={Uri.EscapeDataString(link.PackId)}"))
+                .ToArray();
             ViewData[AuditBatteryKey] = new AuditBattery(
                 pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? pack.Id,
-                BatteryPassWeb.Dpp.Pages.BatteryPages.BatteryHref(pack.Id));
+                BatteryPassWeb.Dpp.Pages.BatteryPages.BatteryHref(pack.Id),
+                others);
         }
 
         // 50 events at a time; one more is read to know whether "Show 50 more" has anything to show.
@@ -1050,4 +1056,7 @@ public class AdminController : Controller
 }
 
 /// <summary>The battery whose history the audit log shows: its serial number and its page (redesign R6).</summary>
-public sealed record AuditBattery(string SerialNumber, string Href);
+public sealed record AuditBattery(string SerialNumber, string Href, IReadOnlyList<AuditBatteryLink>? OtherPassports = null);
+
+/// <summary>Another passport of the same battery, and its history (redesign R6).</summary>
+public sealed record AuditBatteryLink(string Label, string Href);
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 643d341..d2eb604 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -124,6 +124,9 @@ public sealed record BatteryPage(
     /// <summary>The overview's tiles: state of charge, cycles, temperature and capacity fade, with their last 7 days (redesign R4).</summary>
     public IReadOnlyList<LiveTile> Tiles { get; init; } = [];
 
+    /// <summary>The battery's passports, oldest first, when it was placed on the market again (redesign R6); empty otherwise.</summary>
+    public IReadOnlyList<ChainLink> Chain { get; init; } = [];
+
     /// <summary>The team's "Passport status" card (mock-up S5, step 4c-2): invalid, marked for deletion, and their undo.</summary>
     public PassportMarkView Marks => new(
         Publication.Active is not null ? DppStatuses.Active : Publication.Latest?.DppStatus,
@@ -477,6 +480,7 @@ public sealed class BatteryPages(
         {
             Status = await status.CardAsync(sources, active is not null, href, forTeam: true, cancellationToken: cancellationToken),
             Tiles = trends is null ? [] : await trends.TilesAsync(sources.Pack, sources.Model, cancellationToken),
+            Chain = await PassportChains.ForAsync(store, sources.Pack, cancellationToken),
             HandOverTo = await store.FindWaitingHandOverAsync(packId, cancellationToken) is { } waiting
                 ? clusterChoices.FirstOrDefault(cluster => string.Equals(cluster.Id, waiting.ToClusterId, StringComparison.OrdinalIgnoreCase))?.Name ?? waiting.ToClusterId
                 : null,
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index 7a37857..f71cafd 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -29,6 +29,9 @@ public sealed record ClusterBatteryPage(
     /// <summary>The overview's tiles, as on the team's page (redesign R4).</summary>
     public IReadOnlyList<LiveTile> Tiles { get; init; } = [];
 
+    /// <summary>The battery's passports, oldest first, when it was placed on the market again (redesign R6); empty otherwise.</summary>
+    public IReadOnlyList<ChainLink> Chain { get; init; } = [];
+
     /// <summary>The "Hand over" card (step 4d): the waiting hand-over, or the window to ask for one.</summary>
     public HandOverCardView HandOver { get; init; } = new(string.Empty, string.Empty, null, null);
 
@@ -146,6 +149,7 @@ public sealed class ClusterBatteryPages(
         {
             Status = await status.CardAsync(sources, active: true, href, forTeam: false, cancellationToken: cancellationToken),
             Tiles = trends is null ? [] : await trends.TilesAsync(sources.Pack, sources.Model, cancellationToken),
+            Chain = await PassportChains.ForAsync(store, pack, cancellationToken),
             HandOver = new HandOverCardView($"{href}/handover", serial, waiting, waiting is null ? null : names.GetValueOrDefault(waiting.ToClusterId) ?? waiting.ToClusterId),
             History = history is null ? [] : await history.ForAsync(pack, sender.ClusterIds, cancellationToken),
         };
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index 31dde22..6f241a7 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -83,6 +83,12 @@ public sealed record PassportPage(
     /// <summary>The key figures card (redesign R5): public values only, the same on the summary and the detail page.</summary>
     public KeyFigures? KeyFigures { get; init; }
 
+    /// <summary>
+    /// The battery's passports, oldest first, on the detail page for the team and market surveillance (redesign R6, the
+    /// owner's item 12); empty for everyone else, who read "replaces an earlier passport" as before.
+    /// </summary>
+    public IReadOnlyList<ChainLink> Chain { get; init; } = [];
+
     /// <summary>Why the passport is invalid and who said so, for the team and market surveillance only (decision 34); null otherwise.</summary>
     public string? MarkReason { get; init; }
 
@@ -301,6 +307,9 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
             recycled)
         {
             KeyFigures = KeyFigures.From(number => Find(everyone, AttributeCatalog.ByNumber(number)), Words, carbon, recycled),
+            Chain = detail && (who.Roles.Contains(ReaderRole.Team) || who.Roles.Contains(ReaderRole.MarketSurveillance))
+                ? await PassportChains.ForAsync(store, sources.Pack, cancellationToken)
+                : [],
             ModuleCount = sources.Pack.Modules.Count,
             ModuleDescription = sources.Model.ModuleDescription,
             Modules = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Modules : null,
diff --git a/web/Views/Admin/AuditLog.cshtml b/web/Views/Admin/AuditLog.cshtml
index fe9426e..b468140 100644
--- a/web/Views/Admin/AuditLog.cshtml
+++ b/web/Views/Admin/AuditLog.cshtml
@@ -36,7 +36,14 @@
     @if (ViewData[BatteryPassWeb.Controllers.AdminController.AuditBatteryKey] is BatteryPassWeb.Controllers.AuditBattery battery)
     {
         @* Opened from a battery's History link: whose history this is, and the ways back (redesign R6). *@
-        <p class="bp-audit-battery mt-4 mb-0">History of battery <strong>@battery.SerialNumber</strong> · <a href="@battery.Href">Back to the battery page</a> · <a href="/admin/audit">The whole audit log</a></p>
+        <p class="bp-audit-battery mt-4 mb-0">History of battery <strong>@battery.SerialNumber</strong> · <a href="@battery.Href">Back to the battery page</a> · <a href="/admin/audit">The whole audit log</a>@if (battery.OtherPassports is { Count: > 0 } others)
+            {
+                <text> · Its other passports' histories: </text>
+                @for (var index = 0; index < others.Count; index++)
+                {
+                    <a href="@others[index].Href">@others[index].Label</a>@(index < others.Count - 1 ? ", " : null)
+                }
+            }</p>
     }
     <section class="bp-card bp-console-form-card bp-product-editor-section mt-4">
         <form method="get" class="bp-auditlog-filters">
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 0b6b359..115792e 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -365,6 +365,7 @@
                 @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
             </div>
             <div data-section="versions" hidden="@Hidden("versions")">
+                @await Html.PartialAsync("_PassportChain", new ChainView(Model.Chain, ChainReader.Team))
                 @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
             </div>
         </div>
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 9cb463a..1fa37e7 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -138,6 +138,7 @@
                 @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
             </div>
             <div data-section="versions" hidden="@Hidden("versions")">
+                @await Html.PartialAsync("_PassportChain", new ChainView(Model.Chain, ChainReader.ClusterAdmin))
                 @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
             </div>
             <div data-section="history" hidden="@Hidden("history")">
diff --git a/web/Views/Passport/_PassportTabs.cshtml b/web/Views/Passport/_PassportTabs.cshtml
index 857bb79..e9a9472 100644
--- a/web/Views/Passport/_PassportTabs.cshtml
+++ b/web/Views/Passport/_PassportTabs.cshtml
@@ -128,6 +128,11 @@
                         }
                     </dl>
                 }
+                @if (tab.Key == BatteryPassWeb.Dpp.Catalog.AttributeDefinition.HeaderSection && Model.Chain.Count > 1)
+                {
+                    @* The battery's passports, for the team and market surveillance (redesign R6, the owner's item 12). *@
+                    <div class="mt-4">@await Html.PartialAsync("_PassportChain", new ChainView(Model.Chain, ChainReader.Detail))</div>
+                }
                 @if (tab.Key == "purchaser" && Model.Repairs is { } repairs)
                 {
                     <h3 class="h6 mt-3 mb-0">Repair history</h3>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 5c393a5..ee64750 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3576,3 +3576,59 @@ main .bp-field-marker.is-published {
     grid-template-columns: 1fr;
   }
 }
+
+/* "Passports of this battery" (redesign R6): one line per passport, oldest first, this one marked. */
+.bp-passport-chain {
+  display: grid;
+  gap: 8px;
+  margin: 14px 0 0;
+  padding: 0;
+  list-style: none;
+}
+
+.bp-passport-chain li {
+  display: grid;
+  grid-template-columns: 32px minmax(0, 1fr) auto;
+  align-items: center;
+  gap: 6px 14px;
+  padding: 10px 12px;
+  border: 1px solid var(--bp-border);
+  border-radius: 10px;
+}
+
+.bp-passport-chain li.is-this {
+  border-color: var(--bp-primary);
+}
+
+.bp-passport-chain-number {
+  display: grid;
+  place-items: center;
+  width: 28px;
+  height: 28px;
+  border-radius: 50%;
+  background: rgba(42, 110, 207, 0.1);
+  color: var(--bp-primary);
+  font-weight: 700;
+}
+
+.bp-passport-chain-main {
+  display: grid;
+  gap: 4px;
+  min-width: 0;
+}
+
+.bp-passport-chain-links {
+  display: flex;
+  flex-wrap: wrap;
+  gap: 12px;
+}
+
+@media (max-width: 575.98px) {
+  .bp-passport-chain li {
+    grid-template-columns: 32px minmax(0, 1fr);
+  }
+
+  .bp-passport-chain-links {
+    grid-column: 2;
+  }
+}
```

`web/Dpp/Pages/PassportChains.cs`:

```csharp
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One passport of a battery placed on the market again (design 7.5): its place in the chain, its battery ID (#6), its
/// passport status (#2), when it was published and when a newer one replaced it.
/// </summary>
public sealed record ChainLink(int Number, string PackId, string Identifier, string Status, DateTime? FromUtc, DateTime? ToUtc, bool IsThis, bool IsNewest)
{
    /// <summary>Whether it was ever published; a draft has no public facts.</summary>
    public bool Published => FromUtc is not null;
}

/// <summary>Who reads the chain, which sets what each line holds and where it leads (redesign R6, the owner's item 12).</summary>
public enum ChainReader
{
    /// <summary>The team's battery page: every passport, its battery page and its history.</summary>
    Team,

    /// <summary>The detail page, for the team and market surveillance: every published passport, its detail page.</summary>
    Detail,

    /// <summary>
    /// The current cluster admin: public facts only (battery ID, passport status, dates, the public page), because an
    /// earlier passport may have belonged to another customer's cluster.
    /// </summary>
    ClusterAdmin,
}

/// <summary>A battery's chain of passports, as one reader sees it (redesign R6, the owner's item 12).</summary>
public sealed record ChainView(IReadOnlyList<ChainLink> Links, ChainReader Reader)
{
    /// <summary>The lines this reader sees: a cluster admin and the detail page only published passports.</summary>
    public IReadOnlyList<ChainLink> Shown => Reader == ChainReader.Team ? Links : Links.Where(link => link.Published).ToArray();
}

/// <summary>
/// "Passports of this battery" (redesign R6, the owner's item 12, "Team + MSA full, admin limited"): a battery placed on
/// the market again has a pack per passport with the same serial number, each new pack naming the one it replaces
/// (<see cref="BatteryPack.SuccessorOf"/>). The chain runs from the first passport to the newest.
/// </summary>
public static class PassportChains
{
    /// <summary>The battery's passports in order, oldest first; empty when it has only this one.</summary>
    public static async Task<IReadOnlyList<ChainLink>> ForAsync(DppStore store, BatteryPack pack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(pack);
        var serial = BatteryPages.Text(pack, "batterySerialNumber");
        var packs = serial is null ? [pack] : await store.FindPacksBySerialNumberAsync(serial, cancellationToken);
        var byId = packs.Append(pack).GroupBy(each => each.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var first = pack;
        var seen = new HashSet<string>(StringComparer.Ordinal) { pack.Id };
        while (first.SuccessorOf is { } earlier && byId.TryGetValue(earlier, out var previous) && seen.Add(previous.Id))
        {
            first = previous;
        }

        var chain = new List<BatteryPack> { first };
        while (byId.Values.FirstOrDefault(each => each.SuccessorOf == chain[^1].Id && !chain.Contains(each)) is { } next)
        {
            chain.Add(next);
        }

        if (chain.Count < 2)
        {
            return [];
        }

        var links = new List<ChainLink>();
        for (var index = 0; index < chain.Count; index++)
        {
            var each = chain[index];
            var versions = await store.ListVersionsAsync(each.Id, cancellationToken);
            var latest = versions.MaxBy(version => version.Number);
            var status = latest?.DppStatus ?? "draft";
            links.Add(new ChainLink(
                index + 1,
                each.Id,
                BatteryPages.Text(each, "uniqueProductIdentifier") ?? string.Empty,
                status,
                versions.Count == 0 ? null : versions.Min(version => version.PublishedAtUtc),
                status == DppStatuses.Inactive ? latest!.LastUpdatedUtc : null,
                each.Id == pack.Id,
                index == chain.Count - 1));
        }

        return links;
    }
}
```

`web/Views/Shared/_PassportChain.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model ChainView
@* "Passports of this battery" (redesign R6, the owner's item 12): every passport of a battery placed on the market again,
   oldest first. The team sees all of them and their histories; the detail page (team, market surveillance) the
   published ones; a cluster admin only public facts, since an earlier passport may have been another customer's. *@
@if (Model.Shown.Count > 1)
{
    <section class="bp-card bp-console-form-card bp-battery-card mb-4" id="passport-chain">
        <span class="bp-summary-title">Passports of this battery</span>
        <h2>@Words.Count(Model.Shown.Count, "passport")</h2>
        <p class="mb-0">The battery was placed on the market again, and each time got a new passport.@(Model.Reader == ChainReader.ClusterAdmin ? " Only public facts are shown: an earlier passport may have belonged to another customer." : null)</p>
        <ol class="bp-passport-chain">
            @foreach (var link in Model.Shown)
            {
                <li class="@(link.IsThis ? "is-this" : null)">
                    <span class="bp-passport-chain-number">@link.Number</span>
                    <span class="bp-passport-chain-main">
                        <span class="bp-id-copy-row"><span class="bp-id-value" title="@link.Identifier">@BatteryPages.ShortId(link.Identifier)</span>@await Html.PartialAsync("_CopyIdButton", link.Identifier)</span>
                        <span class="bp-badges is-compact">
                            @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(link.Published ? link.Status : null))
                            @if (link.IsNewest)
                            {
                                <span class="bp-chip">Newest</span>
                            }
                            @if (link.IsThis)
                            {
                                <span class="bp-chip">This one</span>
                            }
                        </span>
                        <small class="d-block text-secondary">@(link.FromUtc is { } from ? $"From {ValueWords.Time(from)}" : "Not published")@(link.ToUtc is { } to ? $" to {ValueWords.Time(to)}" : link.Published ? " to now" : null)</small>
                    </span>
                    <span class="bp-passport-chain-links">
                        @switch (Model.Reader)
                        {
                            case ChainReader.Team:
                                @if (!link.IsThis)
                                {
                                    <a href="@BatteryPages.BatteryHref(link.PackId)">Battery page</a>
                                }
                                <a href="/admin/audit?q=@Uri.EscapeDataString(link.PackId)">History</a>
                                break;
                            case ChainReader.Detail:
                                @if (!link.IsThis)
                                {
                                    <a href="@PassportPages.DetailHref(link.Identifier)">Detail page</a>
                                }
                                break;
                            default:
                                <a href="@PassportPages.SummaryHref(link.Identifier)">Public page</a>
                                break;
                        }
                    </span>
                </li>
            }
        </ol>
    </section>
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryHistoryLinkTests | FullyQualifiedName~ChainHistoryLinksTests | FullyQualifiedName~PassportChainTests"`
Expected: `Passed!  - Failed:     0, Passed:     8`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/BatteryHistoryLinkTests.cs BatteryPassWeb.Tests/Demo/ChainHistoryLinksTests.cs BatteryPassWeb.Tests/Dpp/PassportChainTests.cs web/Controllers/AdminController.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Dpp/Pages/PassportChains.cs web/Dpp/Pages/PassportPages.cs web/Views/Admin/AuditLog.cshtml web/Views/Batteries/Battery.cshtml web/Views/ClusterAdmin/Battery.cshtml web/Views/Passport/_PassportTabs.cshtml web/Views/Shared/_PassportChain.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): Passports of this battery for the team, market surveillance and, public facts only, the cluster admin" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 20: A battery's third passport

A battery placed on the market twice couldn't get a third passport: only a passport's direct neighbours could share its serial number. The whole chain may now; an unrelated battery still may not.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/ThirdPassportTests.cs`
- Modify: `web/Dpp/Editing/PackCreationService.cs`
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Modify: `web/Dpp/Store/DppStore.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/ThirdPassportTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (found building the owner's item 12): a battery placed on the market a second time couldn't get its third
/// passport ("another battery already has this serial number"), because only a passport's direct neighbours could share
/// its serial number. Every passport of the chain may share it; an unrelated battery still may not.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ThirdPassportTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    [Fact]
    public async Task ABatteryPlacedOnTheMarketTwice_ShouldGetItsThirdPassport()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var second = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;
        Assert.Null((await StatusChangeTests.Service(rig).ChangeAsync(Team, second, new StatusChange(StatusEvent.Remanufactured, await StatusChangeTests.CurrentAsync(rig)))).Problem);

        var (third, problem) = await SuccessionTests.Service(rig).CreateSuccessorAsync(Team, second, (await rig.Store.GetAsync<BatteryPack>(second))!.OperatorId);

        Assert.Null(problem);
        Assert.NotNull(third);
        Assert.Equal([third!, second, rig.PackId], (await rig.Store.ChainPackIdsAsync(third)).ToArray());
        Assert.Equal([rig.PackId, second, third], (await rig.Store.ChainPackIdsAsync(rig.PackId)).ToArray());
    }

    [Fact]
    public async Task AnUnrelatedBattery_ShouldStillNotTakeTheSerialNumber()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var serial = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["batterySerialNumber"].Value!.GetValue<string>();
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var other = await BatteryTesting.Creation(rig).CreateAsync(Team, new NewPack(pack.BatchId, pack.OperatorId, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }));

        Assert.Null(other.Pack);
        Assert.Contains(other.Keys, key => key.Message == SavePipeline.SerialNumberTaken);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~ThirdPassportTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'DppStore' does not contain a definition for 'ChainPackIdsAsync'`).

- [ ] **Step 3: Write the code**

Patch `r6-task20-code.patch` (`web/Dpp/Editing/PackCreationService.cs`, `web/Dpp/Editing/SavePipeline.cs`, `web/Dpp/Store/DppStore.cs`). Save the block as `../r6-task20-code.patch`, outside the worktree, and run `git apply ../r6-task20-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Editing/PackCreationService.cs b/web/Dpp/Editing/PackCreationService.cs
index ef0582d..adcfeaa 100644
--- a/web/Dpp/Editing/PackCreationService.cs
+++ b/web/Dpp/Editing/PackCreationService.cs
@@ -69,10 +69,11 @@ public sealed class PackCreationService(
             return new PackCreationResult(null, evaluation.Results, "the battery serial number (#11) is required");
         }
 
-        // A battery's new linked passport keeps its serial number (design 7.5 and 8.2): the old pack may share it.
+        // A battery's new linked passport keeps its serial number (design 7.5 and 8.2): its earlier passports may share it,
+        // all of them, not only the one it replaces (redesign R6: a battery placed on the market twice).
         if (await store.SerialNumberTakenAsync(
             evaluation.Accepted["batterySerialNumber"].GetValue<string>(),
-            request.PredecessorPackId is null ? [] : [request.PredecessorPackId],
+            request.PredecessorPackId is null ? [] : await store.ChainPackIdsAsync(request.PredecessorPackId, cancellationToken),
             cancellationToken))
         {
             var keys = evaluation.Results
diff --git a/web/Dpp/Editing/SavePipeline.cs b/web/Dpp/Editing/SavePipeline.cs
index b3b98fc..17c5148 100644
--- a/web/Dpp/Editing/SavePipeline.cs
+++ b/web/Dpp/Editing/SavePipeline.cs
@@ -261,12 +261,9 @@ public sealed class SavePipeline(
         return new Evaluation(results, accepted);
     }
 
-    /// <summary>The pack and the passports linked to it (its predecessor and successor), which share its serial number.</summary>
-    private async Task<IReadOnlyCollection<string>> LinkedPacksAsync(string packId, CancellationToken cancellationToken)
-    {
-        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
-        return new[] { packId, pack?.SuccessorOf, pack?.Succession?.SuccessorPackId }.OfType<string>().ToArray();
-    }
+    /// <summary>The pack and every earlier and later passport of the same battery, which share its serial number (R6).</summary>
+    private Task<IReadOnlyCollection<string>> LinkedPacksAsync(string packId, CancellationToken cancellationToken) =>
+        store.ChainPackIdsAsync(packId, cancellationToken);
 
     private async Task<bool> UsedByPublishedPassportAsync(TargetKind target, string id, CancellationToken cancellationToken)
     {
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index 69af43b..a1284a9 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -179,6 +179,26 @@ public sealed class DppStore
     }
 
     /// <summary>All packs with this serial number (#11). Serial numbers are not unique.</summary>
+    /// <summary>
+    /// The pack and every earlier and later passport of the same battery (design 7.5): they share its serial number. A
+    /// battery placed on the market twice has three, not just a pack's direct neighbours (redesign R6).
+    /// </summary>
+    public async Task<IReadOnlyCollection<string>> ChainPackIdsAsync(string packId, CancellationToken cancellationToken = default)
+    {
+        var ids = new List<string>();
+        for (var at = (string?)packId; at is not null && !ids.Contains(at); at = (await GetAsync<BatteryPack>(at, cancellationToken))?.SuccessorOf)
+        {
+            ids.Add(at);
+        }
+
+        for (var at = (await GetAsync<BatteryPack>(packId, cancellationToken))?.Succession?.SuccessorPackId; at is not null && !ids.Contains(at); at = (await GetAsync<BatteryPack>(at, cancellationToken))?.Succession?.SuccessorPackId)
+        {
+            ids.Add(at);
+        }
+
+        return ids;
+    }
+
     public async Task<IReadOnlyList<BatteryPack>> FindPacksBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default)
     {
         if (_database is null || string.IsNullOrWhiteSpace(serialNumber))
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~ThirdPassportTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/ThirdPassportTests.cs web/Dpp/Editing/PackCreationService.cs web/Dpp/Editing/SavePipeline.cs web/Dpp/Store/DppStore.cs
git commit -m "fix(dpp): a battery placed on the market twice gets its third passport: the whole chain shares the serial number" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 21: The demo shows a chain of three passports

DEMO-C13M-0001's new passport is remanufactured in turn and gets a third one, so the demo shows "Passports of this battery" with three.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`
- Modify: `web/Dpp/Demo/DemoCatalogue.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Dpp/Demo/demo-catalogue.json`

- [ ] **Step 1: Write the failing tests**

Patch `r6-task21-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoCardTests.cs`, `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`, `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`). Save the block as `../r6-task21-tests.patch`, outside the worktree, and run `git apply ../r6-task21-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
index 7ae4411..c30f648 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
@@ -51,7 +51,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
         var result = await controller.ResetDemoData();
 
         Assert.Equal("/admin/clusters?tab=clusters", Assert.IsType<RedirectResult>(result).Url);
-        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 9 batteries, 6 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
+        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 10 batteries, 7 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
         var tokens = JsonSerializer.Deserialize<List<DemoToken>>((string)controller.TempData["GeneratedCredentials"]!)!;
         Assert.Equal(7, tokens.Count);
         Assert.All(tokens, token => Assert.False(string.IsNullOrWhiteSpace(token.Value)));
@@ -66,7 +66,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
 
         await controller.ClearDemoData();
 
-        Assert.StartsWith("Demo data cleared: 9 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
+        Assert.StartsWith("Demo data cleared: 10 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
         Assert.Null(controller.TempData["GeneratedCredentials"]);
         Assert.Equal(0, await rig.Count(DppStore.Packs));
     }
diff --git a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
index 653ea76..df3f751 100644
--- a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
@@ -29,7 +29,7 @@ public sealed class DemoClearTests(MongoFixture mongo)
         }
 
         Assert.Empty(result.Notes);
-        Assert.Contains("9 batteries removed", result.Done);
+        Assert.Contains("10 batteries removed", result.Done);
         Assert.Contains("12 accounts removed", result.Done);
     }
 
@@ -44,7 +44,7 @@ public sealed class DemoClearTests(MongoFixture mongo)
 
         Assert.Equal(before + 1, await rig.Count("applicationAuditEvents"));
         var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.ClearedEvent))).Single();
-        Assert.Equal(9, entry["metadata"]["batteries"].ToInt32());
+        Assert.Equal(10, entry["metadata"]["batteries"].ToInt32());
     }
 
     [Fact]
@@ -97,12 +97,12 @@ public sealed class DemoClearTests(MongoFixture mongo)
         var result = await rig.Demo.ResetAsync(DemoRig.Who);
 
         var packs = (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).ToList();
-        Assert.Equal(9, packs.Count);
+        Assert.Equal(10, packs.Count);
         Assert.Empty(packs.Intersect(firstPacks));
-        Assert.Equal(10, await rig.Count(DppStore.Versions));
+        Assert.Equal(12, await rig.Count(DppStore.Versions));
         Assert.Equal(7, result.Tokens.Count);
         Assert.Empty(result.Tokens.Select(token => token.Value).Intersect(first.Tokens.Select(token => token.Value)));
-        Assert.Contains("9 batteries removed", result.Done);
-        Assert.Contains("9 batteries, 6 of them published", result.Done);
+        Assert.Contains("10 batteries removed", result.Done);
+        Assert.Contains("10 batteries, 7 of them published", result.Done);
     }
 }
diff --git a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
index 0870e8b..72b28f1 100644
--- a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
@@ -45,8 +45,8 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.Equal(2, await rig.Count(DppStore.Sites));
         Assert.Equal(2, await rig.Count(DppStore.Operators));
         Assert.Equal(3, await rig.Count(DppStore.Batches));
-        Assert.Equal(9, await rig.Count(DppStore.Packs));
-        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "9 batteries, 6 of them published", "12 accounts and 7 API tokens"], result.Done);
+        Assert.Equal(10, await rig.Count(DppStore.Packs));
+        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "10 batteries, 7 of them published", "12 accounts and 7 API tokens"], result.Done);
     }
 
     [Fact]
@@ -57,11 +57,11 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         await rig.Demo.SeedAsync(DemoRig.Who);
 
         var versions = await rig.All(DppStore.Versions);
-        Assert.Equal(10, versions.Count); // C7M-0001: 3 (a new version, then its repair); C13M-0001: 2 (repurposed) and its new passport 1; CORE-0003: 2 (waste); CORE-0001 and C7M-0003: 1
+        Assert.Equal(12, versions.Count); // C7M-0001: 3 (a new version, then its repair); C13M-0001: 2 (repurposed), its new passport 2 (remanufactured) and its third 1 (R6); CORE-0003: 2 (waste); CORE-0001 and C7M-0003: 1
         Assert.Equal(3, versions.Count(version => version["DppStatus"].AsString == "active")); // C13M-0001's old passport is inactive, C7M-0003's invalid, CORE-0003's marked for deletion
         Assert.Equal(1, versions.Count(version => version["DppStatus"].AsString == "invalid"));
         Assert.Equal(1, versions.Count(version => version["DppStatus"].AsString == "marked-for-deletion"));
-        Assert.Equal(1, versions.Count(version => version["DppStatus"].AsString == "inactive"));
+        Assert.Equal(2, versions.Count(version => version["DppStatus"].AsString == "inactive")); // C13M-0001's first two passports (R6: a chain of three)
         Assert.All(versions, version => Assert.True(version.Contains("Proof")));
         var firstPack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single()["_id"].AsString;
         Assert.Equal([1, 2, 3], versions.Where(version => version["PackId"] == firstPack).Select(version => version["Number"].ToInt32()).Order());
@@ -82,11 +82,15 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.Equal(6, repaired["Values"]["capacityFade"]["Value"]["percentageValue"].ToInt32());
         Assert.Equal("waste", waste["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
         Assert.Empty(waste["Repairs"].AsBsonArray);
-        Assert.Equal(3, await rig.Count("applicationAuditEvents", Eq("eventType", BatteryPassWeb.Dpp.Editing.StatusChangeService.ChangedEvent)));
+        Assert.Equal(4, await rig.Count("applicationAuditEvents", Eq("eventType", BatteryPassWeb.Dpp.Editing.StatusChangeService.ChangedEvent))); // R6: C13M-0001's second passport is remanufactured too
         var repurposed = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C13M-0001"))).ToList();
-        Assert.Equal(2, repurposed.Count); // the old battery and its new passport, same serial number
-        var newOne = repurposed.Single(pack => pack.Contains("SuccessorOf") && !pack["SuccessorOf"].IsBsonNull);
-        Assert.Equal("repurposed", newOne["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
+        Assert.Equal(3, repurposed.Count); // the old battery and its two newer passports, same serial number (R6: a chain of three)
+        var first = repurposed.Single(pack => !pack.Contains("SuccessorOf") || pack["SuccessorOf"].IsBsonNull);
+        var newOne = repurposed.Single(pack => pack.Contains("SuccessorOf") && pack["SuccessorOf"] == first["_id"]);
+        var third = repurposed.Single(pack => pack.Contains("SuccessorOf") && pack["SuccessorOf"] == newOne["_id"]);
+        Assert.Equal("P5Y", third["Values"]["warrantyPeriod"]["Value"].AsString);
+        Assert.Equal("repurposed", first["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
+        Assert.Equal("remanufactured", newOne["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString); // R6: placed on the market again in turn
         Assert.Equal("P3Y", newOne["Values"]["warrantyPeriod"]["Value"].AsString);
         Assert.True(newOne["IsDemo"].AsBoolean);
         var invalid = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0003"))).Single();
@@ -118,7 +122,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         await rig.Demo.SeedAsync(DemoRig.Who);
 
         var packs = await rig.All(DppStore.Packs);
-        Assert.Equal([0, 0, 0, 7, 7, 10, 10, 13, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
+        Assert.Equal([0, 0, 0, 7, 7, 10, 10, 13, 13, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
         Assert.Equal(2, await rig.Count(DocumentStore.Records, Eq("Property", "informationOnAccidents")));
         Assert.Equal(12, await rig.Count(DocumentStore.Records, Eq("OwnerKind", "Model")));
         Assert.Equal(3, await rig.Count(DocumentStore.Records, Eq("Property", "resultsOfTestReportsProvingCompliance")));
@@ -195,7 +199,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 
         Assert.Equal([DemoDataService.NoPassword], result.Notes);
         Assert.Equal(0, await rig.Count("users"));
-        Assert.Equal(9, await rig.Count(DppStore.Packs));
+        Assert.Equal(10, await rig.Count(DppStore.Packs));
     }
 
     [Fact]
@@ -244,7 +248,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         var again = await rig.Demo.SeedAsync(DemoRig.Who);
 
         Assert.Equal(["The demo data is already there: reset it or clear it first."], again.Notes);
-        Assert.Equal(9, await rig.Count(DppStore.Packs));
+        Assert.Equal(10, await rig.Count(DppStore.Packs));
     }
 
     [Fact]
@@ -267,6 +271,6 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 
         var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.SeededEvent))).Single();
         Assert.Equal(DemoRig.Who, entry["actor"].AsString);
-        Assert.Equal((9, 6, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
+        Assert.Equal((10, 7, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
     }
 }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoCardTests | FullyQualifiedName~DemoClearTests | FullyQualifiedName~DemoSeedTests"`
Expected: `Failed:    12, Passed:    18` (failing: `DemoCardTests.ClearingFromTheCard_ShouldClearAndSayWhatWasRemoved`, `DemoCardTests.ResettingFromTheCard_ShouldSeedAndShowEveryNewTokenOnce`, `DemoClearTests.Clearing_ShouldKeepTheAuditLogAndLogItself`, `DemoClearTests.Clearing_ShouldRemoveEveryDemoRecordWithItsVersionsDocumentsAndFiles`, `DemoClearTests.Resetting_ShouldClearThenSeedWithNewBatteries`, `DemoSeedTests.SeedingAgain_ShouldAskToResetOrClearFirst`, `DemoSeedTests.TheBatteries_ShouldHaveModulesAccidentReportsAndDocuments`, `DemoSeedTests.ThePublishedBatteries_ShouldHaveSignedVersionsAndTheSecondOneAVersionTwo`, `DemoSeedTests.TheSeed_ShouldBeLoggedOnceWithTheCountsButNoTokenValues`, `DemoSeedTests.TheSeed_ShouldMakeTheFamiliesModelsAndBatteries`, `DemoSeedTests.TheStatusExamples_ShouldShowEveryStatusAndMark`, `DemoSeedTests.WithoutTheSetting_ShouldSeedEverythingButTheAccounts`).

- [ ] **Step 3: Write the code**

Patch `r6-task21-code.patch` (`web/Dpp/Demo/DemoCatalogue.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/Demo/demo-catalogue.json`). Save the block as `../r6-task21-code.patch`, outside the worktree, and run `git apply ../r6-task21-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Demo/DemoCatalogue.cs b/web/Dpp/Demo/DemoCatalogue.cs
index 7cbb184..805a74f 100644
--- a/web/Dpp/Demo/DemoCatalogue.cs
+++ b/web/Dpp/Demo/DemoCatalogue.cs
@@ -26,8 +26,11 @@ public sealed record DemoBatch(string Key, string Model, string Site, int Year,
 /// </summary>
 public sealed record DemoStatus(string What, JsonObject? Restated = null, int DaysAgo = 0, string? Repair = null, DemoSuccessor? Successor = null);
 
-/// <summary>Scania's new passport of a demo battery placed on the market again (step 4c-1): made, given a warranty, published.</summary>
-public sealed record DemoSuccessor(string Warranty);
+/// <summary>
+/// Scania's new passport of a demo battery placed on the market again (step 4c-1): made, given a warranty, published;
+/// with <paramref name="Then"/>, placed on the market again in turn, for a chain of passports (redesign R6).
+/// </summary>
+public sealed record DemoSuccessor(string Warranty, DemoStatus? Then = null);
 
 /// <summary>
 /// A demo battery's passport marked invalid, by the demo market surveillance account with this reason, or marked for
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index 0d6f776..0f0d3bd 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -429,9 +429,23 @@ public sealed class DemoDataService(
             await ChangeStatusAsync(team, packId, demo, status, cancellationToken);
             if (status.Successor is { } next)
             {
-                await MakeSuccessorAsync(team, packId, demo, next, cancellationToken);
+                var newest = await MakeSuccessorAsync(team, packId, demo, next, cancellationToken);
                 successors++;
                 published++;
+
+                // Placed on the market again in turn: a chain of passports (redesign R6, the owner's item 12).
+                for (var then = next.Then; then is not null; then = then.Successor?.Then)
+                {
+                    await ChangeStatusAsync(team, newest, demo, then, cancellationToken);
+                    if (then.Successor is not { } again)
+                    {
+                        break;
+                    }
+
+                    newest = await MakeSuccessorAsync(team, newest, demo, again, cancellationToken);
+                    successors++;
+                    published++;
+                }
             }
         }
 
@@ -554,7 +568,7 @@ public sealed class DemoDataService(
     /// Scania's new passport of a demo battery placed on the market again (step 4c-1), the way the team makes it: made
     /// from the old battery with its operator, given a warranty, published, which makes the old passport inactive.
     /// </summary>
-    private async Task MakeSuccessorAsync(ChangeSender team, string packId, DemoPack demo, DemoSuccessor next, CancellationToken cancellationToken)
+    private async Task<string> MakeSuccessorAsync(ChangeSender team, string packId, DemoPack demo, DemoSuccessor next, CancellationToken cancellationToken)
     {
         var old = await store.GetAsync<BatteryPack>(packId, cancellationToken) ?? throw new InvalidOperationException($"The demo battery {demo.Serial} is missing.");
         var (successorId, problem) = await successions.CreateSuccessorAsync(team, packId, old.OperatorId, cancellationToken);
@@ -570,6 +584,8 @@ public sealed class DemoDataService(
         {
             throw new InvalidOperationException($"The demo battery {demo.Serial}'s new passport couldn't be published: {result.Problem ?? string.Join("; ", result.Report?.Blocking ?? [])}.");
         }
+
+        return successorId;
     }
 
     /// <summary>
diff --git a/web/Dpp/Demo/demo-catalogue.json b/web/Dpp/Demo/demo-catalogue.json
index a6390c4..593b074 100644
--- a/web/Dpp/Demo/demo-catalogue.json
+++ b/web/Dpp/Demo/demo-catalogue.json
@@ -981,7 +981,18 @@
           "expectedLifetimeInCalendarYears": 6
         },
         "successor": {
-          "warranty": "P3Y"
+          "warranty": "P3Y",
+          "then": {
+            "what": "Remanufactured",
+            "restated": {
+              "capacityFade": 3,
+              "powerFade": 2,
+              "expectedLifetimeInCalendarYears": 9
+            },
+            "successor": {
+              "warranty": "P5Y"
+            }
+          }
         }
       }
     },
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoCardTests | FullyQualifiedName~DemoClearTests | FullyQualifiedName~DemoSeedTests"`
Expected: `Passed!  - Failed:     0, Passed:    30`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCardTests.cs BatteryPassWeb.Tests/Demo/DemoClearTests.cs BatteryPassWeb.Tests/Demo/DemoSeedTests.cs web/Dpp/Demo/DemoCatalogue.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/Demo/demo-catalogue.json
git commit -m "feat(dpp): the demo battery DEMO-C13M-0001 has a chain of three passports" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 22: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2424, Skipped:     0, Total:  2424` (59 more than the 2365 before).

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
8. **Clusters → Demo data → Reset** (the demo now has 10 batteries, 7 published), then **as the demo North admin:** the Batteries tab opens the Registry; a battery page ends with its History, with nobody outside the cluster named.
9. **As the team, a draft battery → Remove:** the confirm window; Cancel keeps it. The same on a family, model, batch, site and operator.
10. **The team's list:** each row has the copy button and "Version … · updated …".
11. **DEMO-C7M-0001's battery page → Public page:** it opens in the same tab and says "Public page"; More → a window: the menu closes, Escape too.
12. **DEMO-C7M-0003 and DEMO-CORE-0003's detail pages:** a Live values tab with each latest value and its date.
13. **DEMO-C13M-0001's battery page:** "Passports of this battery" lists 1 → 2 → 3, the third marked newest; History lists the other passports' histories.
14. **The home page:** "Open a sample battery passport" opens a published demo battery.

## Next

Step 5 resumes. The owner's open choices from the review (in the managing session's notes) may add their own small plan.
