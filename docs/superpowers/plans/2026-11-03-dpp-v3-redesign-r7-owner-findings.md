# DPP V3 Redesign R7: What the Owner Found on Their Own Batteries — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The owner's 4 findings from testing their own batteries, as approved in the prototype, and one gap the page review found (task 5). Each task ends with a working app:

1. **Live values show the latest week with readings** (task 1, "live values have no plots anymore, is that intended?"). The charts needed 2 or more readings in the last 7 days; the owner's batteries had none this week, so no line showed. Now, when a battery has no reading in the last 7 days, its charts show the 7 days ending at its last reading:
   - the detail page's Live values tab is titled "Last week of readings: 17–24 Oct" (not "The last 7 days") and says "The battery has sent no reading since 24 Oct 2026, 18:21, so the charts show its last week of readings."; each chart's axis runs over that week; "All readings" lists it; a value with no reading that week says "No reading that week";
   - the battery pages' overview lines (team and cluster admin) use the same week, with one line above them: "No reading in the last 7 days: the lines show the last week of readings, 17–24 Oct.";
   - a battery that reports is unchanged ("The last 7 days"); a battery with no readings at all still shows no chart.
   The last reading's time comes from the battery's stored live values (the accident reports' page, #92, isn't a reading), so there is no history scan; one indexed query loads that week, with the same rule of who sees what.
2. **An earlier passport says what it is** (task 2, "why do the old batteries passports cards in the versions have each battery page link on them?", kept but made self-explanatory). In "Passports of this battery" (team), an earlier passport's link reads "Passport 1's page (read only)" (on hover: "This passport's battery page as it was when it was replaced; nothing on it changes"), the newest one's "Newest passport's page", and the intro adds "An earlier passport's page shows it as it was when it was replaced, to read only." That page says, in one line under its badges: "Passport 1 of 3, an earlier passport, read only: nothing on it changes. Open the newest passport's page". Two things on it still invited edits, and the app saved them: the header's cluster "Change" and the module list (upload and typed). Neither is offered now ("The module list stays as it was on this earlier passport; change it on the newest one."), and the app refuses both ("it is an earlier passport, kept as it was when it was replaced; change the newest passport instead"). Marked passports are unchanged.
3. **One shared battery list** (task 3, "open registry and batteries page have the same content but behave differently", the choice "One shared list"). Both pages draw `_BatteryList`: the same search ("Serial number, battery ID, passport ID or cluster"; without "or cluster" for readers who may not search clusters, R6), count line ("3 batteries match “DEMO-C7M”. Show all"), columns (Serial number with its copy icon, Model, Batch, Cluster for those who see clusters, Status, Updated "v3 · 3 Nov", in full on hover) and row icons. A serial number opens the battery page for the team (on the Registry too, which opened the detail page) and the battery's cluster admins, the detail page for everyone else. Each page keeps its rows and rules: the team's Batteries lists drafts and has Create battery; the Registry lists published batteries, a cluster admin only their cluster's; #22 where it was shown; the Registry's hand-over box.
4. **Every list row on one line at 1280 px** (task 4, "Copy icon only"). The two statuses sit side by side, told apart by their shape (a square battery tag, a round passport pill), their "Battery"/"Passport" words kept for screen readers; a serial number ends in "…" after 165 px (the demo's are about 140 px; any serial fits) and a model, batch or cluster name after 180 px, each whole on hover; the row icons are 32 px; the cells a little narrower; the last icon's name opens to its left. A phone keeps its cards. `site.css` is unchanged.
5. **A marked battery's cluster reads it as when it was active** (task 5, the page review's gap, the owner's "Yes, fix it"). A battery's cluster counted only while its passport was active, so once the passport was marked invalid or for deletion, its own cluster admin and members got "Not available" on its accident page (the read-only page's "Accident page" link, R6b, and #92 on the detail page) and lost its purchaser data on the detail page, though the team kept them. The cluster now counts while the battery's newest passport is marked, as its live values already did (`PassportReader`, R6): the owning cluster reads the battery exactly as when it was active. A replaced passport, another cluster and the public stay as they were.

**Architecture:**
- `LiveTrends.WeekEnd`, `WeekTitle`, `WeekDates` (task 1) pick and name the week; `ForTab` takes the week's end; `TilesAsync` finds the last reading in the pack's stored live values; `LiveTile.Period` names the week in its words. `BatteryApi.LatestWeekAsync` loads the week ending at the last reading (`ReadAsync`, shared with `HistoryAsync`); `PassportController.Detail` gives it the last reading from the page's live values; `LiveTab` carries the week's end to `_LiveHistory`.
- `BatteryPages.EarlierPassport`, `PublicationView.Replaced` and `BatteryCards.Replaced` (task 2); `SetClusterAsync` and `SaveModulesAsync` refuse an inactive passport.
- `BatteryListView` and `_BatteryList` (task 3); `RegistryPage.OpenHref`. The two pages pass their own rows, links and rules.
- `bp-product-data.css` (task 4): the list's one-line rules, scoped to `.bp-one-line-list` from 768 px up.
- `DppStore.FindPublishedPackIdsUsingAsync` (task 5), behind `FindPublishedClusterIdsUsingAsync` and `HasPublishedPackUsingAsync`: an invalid or marked-for-deletion version counts like an active one (only the newest version of a pack can carry either).

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** the product owner's 4 findings after testing their own batteries (3 Nov 2026), their choices ("Latest week with data", "keep as now but make it more intuitive and self-explanatory", "Copy icon only", "One shared list"), the approved prototype (shots in the managing session's notes, `redesign-pictures/r7-prototype/`), and the managing session's addition: a serial-number cap so a long real serial can't make the list scroll sideways. Decisions 30, 34, 36 and 37 stand. Who sees what changes only in task 5, as the owner chose: a marked battery's own cluster reads it as when it was active.

**Learning-mode contribution spot:** `LiveTrends.WeekEnd` (task 1), when a battery counts as still reporting; its tests say what it must do.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at adc6fff (R1–R6b as built, with the phone-width wrap adc6fff). Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas** or `battery_pass_v3`. The tests use a throwaway MongoDB (EphemeralMongo).
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Each task runs only its own tests** (the filter in its steps); **the whole suite runs once**, in the final checks. Never run two whole suites at once.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started (by their process IDs, never by name) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2440`. After this plan: 2459. The suite takes about three minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../r7-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole; a removed file has its `git rm`.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.** The new styles go to `bp-product-data.css` (battery pages and lists) and `bp-status.css` (every page).
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## What happens to the old tests

- `LiveChartsTests` and `LiveTabRuleTests` (task 1) read the tab's new call and its "that week" words.
- `StatusHelpLinkTests`, `RegistrySearchWordsTests`, `TeamListRowTests`, `RegistryListTests`, `AdminDenseConsoleLayoutTests`, `RowActionsTests`, `RegistryBadgesTests`, `PassportMarkPagesTests` and `BatteriesControllerTests` (task 3) read the shared list (`_BatteryList`) where they read the two pages' own tables: the same rules, one place.
- Nothing is removed.

---

### Task 1: Live values show the latest week with readings

When a battery has no reading in the last 7 days, the detail page's charts and the overview lines show the 7 days ending at its last reading, titled with that week's dates. The last reading comes from the stored values; one query loads that week; who sees what is unchanged.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/LatestWeekTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Dpp/Api/BatteryApi.cs`
- Modify: `web/Dpp/Pages/LiveTrends.cs`
- Modify: `web/Views/Batteries/_LiveTiles.cshtml`
- Modify: `web/Views/Passport/_LiveHistory.cshtml`
- Modify: `web/Views/Passport/_PassportTabs.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `r7-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs`, `BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs`). Save the block as `../r7-task1-tests.patch`, outside the worktree, and run `git apply ../r7-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs b/BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs
index fcd066b..2852106 100644
--- a/BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs
@@ -78,7 +78,7 @@ public sealed class LiveChartsTests
         Assert.DoesNotContain("<partial name=\"_LiveHistory\"", detail, StringComparison.Ordinal);
         Assert.Contains("<partial name=\"_PassportTabs\" model=\"Model\" view-data=\"ViewData\" />", detail, StringComparison.Ordinal);
         Assert.Contains("<summary>All readings (@Model.History.Count)</summary>", live, StringComparison.Ordinal); // the tab's own model since R6
-        Assert.Contains("var charts = LiveTrends.ForTab(Model.Latest, Model.History, Clock.GetUtcNow().UtcDateTime, Reading);", live, StringComparison.Ordinal);
+        Assert.Contains("var charts = LiveTrends.ForTab(Model.Latest, Model.History, now, Reading, end);", live, StringComparison.Ordinal); // the latest week with readings (R7)
         Assert.Contains("a[href^=\"#tab-\"]", Read("wwwroot", "js", "bp-passport-links.js"), StringComparison.Ordinal);
     }
 }
diff --git a/BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs b/BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs
index f1683e1..bde2139 100644
--- a/BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs
@@ -57,7 +57,7 @@ public sealed class LiveTabRuleTests
 
         Assert.Contains("var live = Model.Current is null && liveValues.Length > 0", tabs, StringComparison.Ordinal);
         Assert.Contains("Model.Audiences.SelectMany(section => section.Values).Where(value => value.Attribute.Change == BatteryPassWeb.Dpp.Catalog.ChangeRule.Live)", tabs, StringComparison.Ordinal); // only what this reader may see
-        Assert.Contains("0 => \"No reading in the last 7 days\"", live, StringComparison.Ordinal);
+        Assert.Contains("0 => older ? \"No reading that week\" : \"No reading in the last 7 days\"", live, StringComparison.Ordinal); // R7: an older week says "that week"
         Assert.Contains("if (liveHistory is not null && page.Current is null)", controller, StringComparison.Ordinal);
     }
 }
```

`BatteryPassWeb.Tests/Dpp/LatestWeekTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R7, the owner's item 1 ("live values have no plots anymore") and choice "Latest week with data": a battery
/// with no reading in the last 7 days showed no line. Its charts and overview lines now show the 7 days ending at its
/// last reading, titled with that week's dates; a battery that reports keeps "The last 7 days". The last reading's time
/// comes from the battery's stored values (no history scan), and one query loads that week, with the same rule of who
/// sees what.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class LatestWeekTests(MongoFixture mongo)
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void TheWeek_ShouldEndNow_UnlessTheLastReadingIsOlderThan7Days()
    {
        var older = Now.AddDays(-10);

        Assert.Equal(Now, LiveTrends.WeekEnd(Now.AddDays(-2), Now));
        Assert.Equal(Now, LiveTrends.WeekEnd(null, Now));
        Assert.Equal(older, LiveTrends.WeekEnd(older, Now));
        Assert.Equal("The last 7 days", LiveTrends.WeekTitle(Now, Now));
        Assert.Equal("Last week of readings: 17–24 Oct", LiveTrends.WeekTitle(older, Now));
        Assert.Equal("28 Oct – 4 Nov", LiveTrends.WeekDates(new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void TheTab_ShouldDrawTheOlderWeek_OnItsOwnAxis()
    {
        var end = Now.AddDays(-10);
        var soc = AttributeCatalog.ByNumber(63);
        LiveHistoryEntry Reading(int hoursBefore, decimal percent) =>
            new(soc, new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = percent }, end.AddHours(-hoursBefore), end.AddHours(-hoursBefore), "feed");
        var latest = new ShownValue(soc, [new ValueLine(null, "61 %")], new ValueOrigin(end, "feed", Measured: true));

        var tile = Assert.Single(LiveTrends.ForTab([latest], [Reading(0, 61), Reading(48, 80), Reading(200, 20)], Now, _ => string.Empty, end));

        Assert.Equal(2, tile.Readings); // the reading 200 hours before the end is outside that week
        Assert.Equal((end.AddHours(-LiveTrends.Hours), end), (tile.FromUtc!.Value, tile.ToUtc!.Value));
        Assert.Equal("17–24 Oct", tile.Period);
        Assert.Equal("From 80 to 61 over 17–24 Oct", tile.TrendWords);
        Assert.NotNull(tile.Line(300, 70));
    }

    [Fact]
    public async Task TheOverviewLines_ShouldShowTheLastWeekOfReadings()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var api = BatteryApiTests.Api(rig);
        var now = rig.Clock.GetUtcNow().UtcDateTime;
        foreach (var (daysAgo, level) in new[] { (12, 30), (11, 45), (10, 61) })
        {
            var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
            Assert.True((await api.WriteLiveValuesAsync(PublicationTesting.Team, pack, new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["stateOfChargeSoC"] = level }, now.AddDays(-daysAgo), preview: false)).Saved);
        }

        // The battery stopped reporting 10 days ago: every live value it holds was last measured then.
        foreach (var property in new[] { "numberOfFullChargingAndDischargingCycles", "temperatureInformation" })
        {
            await rig.Context.Database!.GetCollection<MongoDB.Bson.BsonDocument>(DppStore.Packs).UpdateOneAsync(
                MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", rig.PackId),
                MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set($"Values.{property}.GeneratedAtUtc", now.AddDays(-10)));
        }

        var sources = (await rig.Store.LoadSourcesAsync(rig.PackId))!;
        var charge = (await new LiveTrends(new ApplicationAuditService(rig.Context), rig.Clock).TilesAsync(sources.Pack, sources.Model)).Single(tile => tile.Property == "stateOfChargeSoC");

        Assert.Equal([30m, 45m, 61m], charge.Points.Select(point => point.Value));
        Assert.Equal(LiveTrends.WeekDates(now.AddDays(-10)), charge.Period);
        Assert.Equal($"From 30 to 61 over {LiveTrends.WeekDates(now.AddDays(-10))}", charge.TrendWords);
    }

    [Fact]
    public async Task TheDetailPagesWeek_ShouldEndAtTheLastReading_ForThoseWhoMaySeeIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var api = BatteryApiTests.Api(rig);
        var now = rig.Clock.GetUtcNow().UtcDateTime;
        foreach (var hoursAgo in new[] { 300, 260, 240 })
        {
            var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
            Assert.True((await api.WriteLiveValuesAsync(PublicationTesting.Team, pack, new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["stateOfChargeSoC"] = 50 }, now.AddHours(-hoursAgo), preview: false)).Saved);
        }

        var last = now.AddHours(-240);
        var (week, end) = await api.LatestWeekAsync(Reader.With("team", ReaderRole.Team), rig.PackId, last);
        var (recent, recentEnd) = await api.LatestWeekAsync(Reader.With("team", ReaderRole.Team), rig.PackId, now.AddHours(-1));
        var (other, _) = await api.LatestWeekAsync(Reader.With("south", ReaderRole.ClusterUser, BatteryTesting.South), rig.PackId, last);

        Assert.Equal(last, end);
        Assert.Equal([last, now.AddHours(-260), now.AddHours(-300)], week.Where(entry => entry.Attribute.Property == "stateOfChargeSoC").Select(entry => entry.MeasuredAtUtc));
        Assert.Equal(now, recentEnd);
        Assert.DoesNotContain(recent, entry => entry.Attribute.Property == "stateOfChargeSoC"); // the last 7 days had none
        Assert.Empty(other); // another cluster's user sees no purchaser values, as before
    }

    [Fact]
    public void ThePages_ShouldNameTheWeek_AndFindTheLastReadingFromTheStoredValues()
    {
        var tab = Read("web", "Views", "Passport", "_LiveHistory.cshtml");
        var tiles = Read("web", "Views", "Batteries", "_LiveTiles.cshtml");
        var controller = Read("web", "Controllers", "PassportController.cs");

        Assert.Contains("<h2 class=\"h5 mb-2\">@LiveTrends.WeekTitle(end, now)</h2>", tab, StringComparison.Ordinal);
        Assert.Contains("so the charts show its last week of readings.", tab, StringComparison.Ordinal);
        Assert.Contains("No reading in the last 7 days: the lines show the last week of readings, @Model[0].Period.", tiles, StringComparison.Ordinal);
        Assert.Contains("await liveHistory.LatestWeekAsync(reader, page.Version.PackId, lastReading, cancellationToken);", controller, StringComparison.Ordinal);
        Assert.Contains("value.Attribute.Property != BatteryPassWeb.Dpp.Documents.DocumentService.Accidents", controller, StringComparison.Ordinal); // the accident reports' page isn't a reading
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~LatestWeekTests | FullyQualifiedName~LiveChartsTests | FullyQualifiedName~LiveTabRuleTests"`
Expected: the build fails with CS0117, CS1061, CS1501, CS8130, CS8183 (for example `error CS0117 'LiveTrends' does not contain a definition for 'WeekDates'`).

- [ ] **Step 3: Write the code**

Patch `r7-task1-code.patch` (`web/Controllers/PassportController.cs`, `web/Dpp/Api/BatteryApi.cs`, `web/Dpp/Pages/LiveTrends.cs`, `web/Views/Batteries/_LiveTiles.cshtml`, `web/Views/Passport/_LiveHistory.cshtml`, `web/Views/Passport/_PassportTabs.cshtml`). Save the block as `../r7-task1-code.patch`, outside the worktree, and run `git apply ../r7-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/PassportController.cs b/web/Controllers/PassportController.cs
index 62e5cfa..8e9a579 100644
--- a/web/Controllers/PassportController.cs
+++ b/web/Controllers/PassportController.cs
@@ -72,7 +72,14 @@ public sealed class PassportController(DppReaders readers, PassportPages pages)
         // change log and the same rule as the API's history.
         if (liveHistory is not null && page.Current is null) // the battery's latest passport, active or not (redesign R6)
         {
-            ViewData["LiveHistory"] = await liveHistory.HistoryAsync(reader, page.Version.PackId, BatteryApi.MaxHistoryHours, cancellationToken);
+            // The latest week with readings (R7): the last 7 days, or the week ending at the battery's last reading.
+            var lastReading = page.Audiences.SelectMany(section => section.Values)
+                .Where(value => value.Attribute.Change == BatteryPassWeb.Dpp.Catalog.ChangeRule.Live && value.Attribute.Property != BatteryPassWeb.Dpp.Documents.DocumentService.Accidents && value.Origin is not null) // the accident reports' page isn't a reading
+                .Select(value => (DateTime?)value.Origin!.GeneratedAtUtc)
+                .Max();
+            var (week, weekEnd) = await liveHistory.LatestWeekAsync(reader, page.Version.PackId, lastReading, cancellationToken);
+            ViewData["LiveHistory"] = week;
+            ViewData["LiveWeekEnd"] = weekEnd;
         }
 
         // The battery's cluster admins manage it from here too (step 4a); read it while its passport is marked (R6b).
diff --git a/web/Dpp/Api/BatteryApi.cs b/web/Dpp/Api/BatteryApi.cs
index aa425cb..c442a15 100644
--- a/web/Dpp/Api/BatteryApi.cs
+++ b/web/Dpp/Api/BatteryApi.cs
@@ -179,12 +179,28 @@ public sealed class BatteryApi(
     /// log, which keeps every saved value with its measurement time; an entry written before 3c-2 has none, and its save
     /// time stands in.
     /// </summary>
-    public async Task<IReadOnlyList<LiveHistoryEntry>> HistoryAsync(Reader reader, string packId, int hours, CancellationToken cancellationToken = default)
+    public Task<IReadOnlyList<LiveHistoryEntry>> HistoryAsync(Reader reader, string packId, int hours, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(reader);
         ArgumentOutOfRangeException.ThrowIfLessThan(hours, 1);
         ArgumentOutOfRangeException.ThrowIfGreaterThan(hours, MaxHistoryHours);
-        var sinceUtc = clock.GetUtcNow().UtcDateTime.AddHours(-hours);
+        return ReadAsync(reader, packId, clock.GetUtcNow().UtcDateTime.AddHours(-hours), null, cancellationToken);
+    }
+
+    /// <summary>
+    /// The detail page's week of live values (redesign R7, the owner's choice "Latest week with data"): the last 7 days
+    /// while the battery reported in them; otherwise the 7 days ending at <paramref name="lastReadingUtc"/>, its last
+    /// reading (from its stored values: no history scan). One query from the week's start; the same rule of who sees what.
+    /// </summary>
+    public async Task<(IReadOnlyList<LiveHistoryEntry> Week, DateTime EndUtc)> LatestWeekAsync(Reader reader, string packId, DateTime? lastReadingUtc, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(reader);
+        var endUtc = BatteryPassWeb.Dpp.Pages.LiveTrends.WeekEnd(lastReadingUtc, clock.GetUtcNow().UtcDateTime);
+        return (await ReadAsync(reader, packId, endUtc.AddHours(-MaxHistoryHours), endUtc, cancellationToken), endUtc);
+    }
+
+    private async Task<IReadOnlyList<LiveHistoryEntry>> ReadAsync(Reader reader, string packId, DateTime sinceUtc, DateTime? untilUtc, CancellationToken cancellationToken)
+    {
         var batteryClusters = await store.FindPublishedClusterIdsUsingAsync(packId: packId, cancellationToken: cancellationToken);
         var visible = LiveSet.Where(attribute => AudienceRules.MaySee(reader, attribute, batteryClusters)).ToDictionary(attribute => attribute.Property, StringComparer.Ordinal);
         if (visible.Count == 0)
@@ -209,7 +225,7 @@ public sealed class BatteryApi(
                 }
 
                 var measuredAt = Moment(key.GetValue("generatedAt", BsonNull.Value) is BsonString generated ? generated.Value : null) ?? savedAt;
-                if (measuredAt >= sinceUtc)
+                if (measuredAt >= sinceUtc && (untilUtc is null || measuredAt <= untilUtc))
                 {
                     entries.Add(new LiveHistoryEntry(attribute, value, measuredAt, savedAt, change.GetValue("source", string.Empty).ToString()!));
                 }
diff --git a/web/Dpp/Pages/LiveTrends.cs b/web/Dpp/Pages/LiveTrends.cs
index 1baf52a..a59e3b7 100644
--- a/web/Dpp/Pages/LiveTrends.cs
+++ b/web/Dpp/Pages/LiveTrends.cs
@@ -10,7 +10,8 @@ using MongoDB.Bson;
 namespace BatteryPassWeb.Dpp.Pages;
 
 /// <summary>The detail page's Live values tab: the reader's latest live values and the last week's readings (redesign R6).</summary>
-public sealed record LiveTab(IReadOnlyList<ShownValue> Latest, IReadOnlyList<LiveHistoryEntry> History);
+/// <param name="WeekEndUtc">The end of the week the charts show: now, or the battery's last reading when that is older (R7).</param>
+public sealed record LiveTab(IReadOnlyList<ShownValue> Latest, IReadOnlyList<LiveHistoryEntry> History, DateTime? WeekEndUtc = null);
 
 /// <summary>One reading of a tile's trend line: when it was measured, and the value.</summary>
 public sealed record TrendPoint(DateTime AtUtc, decimal Value);
@@ -59,10 +60,13 @@ public sealed record LiveTile(string Title, string Property, decimal? Value, str
         return days;
     }
 
+    /// <summary>The week the line covers, in words: "the last 7 days", or an older week's dates, "20–27 Oct" (R7).</summary>
+    public string Period { get; init; } = LiveTrends.LastSevenDays;
+
     /// <summary>The trend in words, for readers who can't see the line.</summary>
     public string TrendWords => Points.Count < 2
-        ? "No change in the last 7 days"
-        : $"From {ValueWords.Number(Points[0].Value)} to {ValueWords.Number(Points[^1].Value)} over the last 7 days";
+        ? $"No change in {Period}"
+        : $"From {ValueWords.Number(Points[0].Value)} to {ValueWords.Number(Points[^1].Value)} over {Period}";
 
     /// <summary>
     /// The line's points in a <paramref name="width"/> × <paramref name="height"/> box, oldest on the left and the highest
@@ -98,6 +102,33 @@ public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock
 {
     public const int Hours = 168;
 
+    public const string LastSevenDays = "the last 7 days";
+
+    /// <summary>
+    /// The end of the week the charts show (redesign R7, the owner's choice "Latest week with data"): now while the battery
+    /// reported in the last 7 days; otherwise its last reading, so a battery that stopped reporting still shows its last week.
+    /// </summary>
+    public static DateTime WeekEnd(DateTime? lastReadingUtc, DateTime nowUtc) =>
+        lastReadingUtc is { } last && last < nowUtc.AddHours(-Hours) ? last : nowUtc;
+
+    /// <summary>Whether the week ending at <paramref name="endUtc"/> is an older one than the last 7 days.</summary>
+    public static bool IsOlderWeek(DateTime endUtc, DateTime nowUtc) => endUtc < nowUtc.AddHours(-Hours);
+
+    /// <summary>The week's dates, "20–27 Oct", or "28 Oct – 3 Nov" across two months.</summary>
+    public static string WeekDates(DateTime endUtc)
+    {
+        var from = endUtc.AddHours(-Hours);
+        return from.Month == endUtc.Month && from.Year == endUtc.Year
+            ? $"{from.Day}–{endUtc.ToString("d MMM", CultureInfo.InvariantCulture)}"
+            : $"{from.ToString("d MMM", CultureInfo.InvariantCulture)} – {endUtc.ToString("d MMM", CultureInfo.InvariantCulture)}";
+    }
+
+    /// <summary>The charts' heading: "The last 7 days", or "Last week of readings: 20–27 Oct".</summary>
+    public static string WeekTitle(DateTime endUtc, DateTime nowUtc) =>
+        IsOlderWeek(endUtc, nowUtc) ? $"Last week of readings: {WeekDates(endUtc)}" : "The last 7 days";
+
+    private static string Period(DateTime endUtc, DateTime nowUtc) => IsOlderWeek(endUtc, nowUtc) ? WeekDates(endUtc) : LastSevenDays;
+
     private static readonly (int Number, string Title, string Unit)[] Tiles =
     [
         (63, "State of charge", "%"),
@@ -154,11 +185,14 @@ public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock
     /// may see (<paramref name="latest"/>, from their passport tabs), each with its latest reading and its date, even when
     /// that is older than the last 7 days, and the week's line when the week had readings (<paramref name="history"/>).
     /// </summary>
-    public static IReadOnlyList<LiveTile> ForTab(IReadOnlyList<ShownValue> latest, IReadOnlyList<LiveHistoryEntry> history, DateTime nowUtc, Func<LiveHistoryEntry, string> words)
+    /// <param name="weekEndUtc">The week's end (<see cref="WeekEnd"/>); now when null (redesign R7).</param>
+    public static IReadOnlyList<LiveTile> ForTab(IReadOnlyList<ShownValue> latest, IReadOnlyList<LiveHistoryEntry> history, DateTime nowUtc, Func<LiveHistoryEntry, string> words, DateTime? weekEndUtc = null)
     {
         ArgumentNullException.ThrowIfNull(latest);
         ArgumentNullException.ThrowIfNull(history);
-        var week = Charts(history.Where(entry => entry.MeasuredAtUtc >= nowUtc.AddHours(-Hours)).ToArray(), nowUtc, words)
+        var endUtc = weekEndUtc ?? nowUtc;
+        var period = Period(endUtc, nowUtc);
+        var week = Charts(history.Where(entry => entry.MeasuredAtUtc >= endUtc.AddHours(-Hours) && entry.MeasuredAtUtc <= endUtc).ToArray(), endUtc, words)
             .ToDictionary(chart => chart.Property, StringComparer.Ordinal);
         var order = AttributeCatalog.All.Select((attribute, index) => (attribute.Number, index)).ToDictionary(pair => pair.Number, pair => pair.index);
         return latest
@@ -173,13 +207,14 @@ public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock
                     : "When it was measured isn't known";
                 var shown = string.Join(", ", value.Lines.Select(line => line.Text));
                 return week.TryGetValue(value.Attribute.Property, out var chart)
-                    ? chart with { Value = null, Note = note, Latest = shown }
+                    ? chart with { Value = null, Note = note, Latest = shown, Period = period }
                     : new LiveTile(unit.Length == 0 ? name : $"{name} ({unit})", value.Attribute.Property, null, unit, note, [])
                     {
                         Latest = shown,
                         Readings = 0,
-                        FromUtc = nowUtc.AddHours(-Hours),
-                        ToUtc = nowUtc,
+                        FromUtc = endUtc.AddHours(-Hours),
+                        ToUtc = endUtc,
+                        Period = period,
                     };
             })
             .ToArray();
@@ -189,8 +224,16 @@ public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock
     {
         ArgumentNullException.ThrowIfNull(pack);
         ArgumentNullException.ThrowIfNull(model);
-        var sinceUtc = clock.GetUtcNow().UtcDateTime.AddHours(-Hours);
+        // The latest week with readings (R7): the stored live values say when the battery last reported, so no history scan.
+        var nowUtc = clock.GetUtcNow().UtcDateTime;
         var wanted = Tiles.Select(tile => AttributeCatalog.ByNumber(tile.Number).Property).ToHashSet(StringComparer.Ordinal);
+        var lastReading = wanted
+            .Where(property => AttributeCatalog.FindByProperty(property)?.Change == ChangeRule.Live)
+            .Select(property => pack.Values.GetValueOrDefault(property)?.GeneratedAtUtc)
+            .Max();
+        var endUtc = WeekEnd(lastReading, nowUtc);
+        var period = Period(endUtc, nowUtc);
+        var sinceUtc = endUtc.AddHours(-Hours);
         var points = new Dictionary<string, List<TrendPoint>>(StringComparer.Ordinal);
         foreach (var change in await audit.ListEntityEventsAsync(SavePipeline.AuditEventType, "dpp-pack", pack.Id, sinceUtc, cancellationToken))
         {
@@ -206,7 +249,7 @@ public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock
                 }
 
                 var measuredAt = Moment(key.GetValue("generatedAt", BsonNull.Value) is BsonString generated ? generated.Value : null) ?? savedAt;
-                if (measuredAt >= sinceUtc)
+                if (measuredAt >= sinceUtc && measuredAt <= endUtc)
                 {
                     (points.TryGetValue(property, out var list) ? list : points[property] = []).Add(new TrendPoint(measuredAt, value));
                 }
@@ -221,7 +264,7 @@ public sealed class LiveTrends(ApplicationAuditService audit, TimeProvider clock
                 ? "Re-stated at each status change"
                 : stored is null ? "Not measured yet" : $"Measured {ValueWords.Time(stored.GeneratedAtUtc)}";
             var line = points.GetValueOrDefault(property)?.OrderBy(point => point.AtUtc).ToArray() ?? [];
-            return new LiveTile(tile.Title, property, Number(stored?.Value), tile.Unit, note, line);
+            return new LiveTile(tile.Title, property, Number(stored?.Value), tile.Unit, note, line) { Period = period };
         }).ToArray();
     }
 
diff --git a/web/Views/Batteries/_LiveTiles.cshtml b/web/Views/Batteries/_LiveTiles.cshtml
index c75a4ff..22177f1 100644
--- a/web/Views/Batteries/_LiveTiles.cshtml
+++ b/web/Views/Batteries/_LiveTiles.cshtml
@@ -1,8 +1,13 @@
 @using BatteryPassWeb.Dpp.Pages
 @model IReadOnlyList<LiveTile>
-@* The overview's tiles (redesign R4, the owner's improvement 4): each value now, with a small line of its last 7 days. *@
+@* The overview's tiles (redesign R4, the owner's improvement 4): each value now, with a small line of its last 7 days,
+   or of its last week of readings when the battery stopped reporting (R7). *@
 @if (Model.Count > 0)
 {
+    @if (Model[0].Period != LiveTrends.LastSevenDays)
+    {
+        <p class="bp-help-text mb-2">No reading in the last 7 days: the lines show the last week of readings, @Model[0].Period.</p>
+    }
     <div class="bp-tiles">
         @foreach (var tile in Model)
         {
diff --git a/web/Views/Passport/_LiveHistory.cshtml b/web/Views/Passport/_LiveHistory.cshtml
index 5d93751..8f2bfec 100644
--- a/web/Views/Passport/_LiveHistory.cshtml
+++ b/web/Views/Passport/_LiveHistory.cshtml
@@ -11,10 +11,21 @@
     const int Width = 300;
     const int Height = 70;
     string Reading(LiveHistoryEntry entry) => string.Join(", ", ValueWords.Lines(Forms.For(entry.Attribute), entry.Value).Select(line => line.Text));
-    var charts = LiveTrends.ForTab(Model.Latest, Model.History, Clock.GetUtcNow().UtcDateTime, Reading);
+    var now = Clock.GetUtcNow().UtcDateTime;
+    var end = Model.WeekEndUtc ?? now;
+    var older = LiveTrends.IsOlderWeek(end, now); // the battery stopped reporting: its last week of readings (R7)
+    var charts = LiveTrends.ForTab(Model.Latest, Model.History, now, Reading, end);
+    var week = older ? "that week" : "this week";
+}
+<h2 class="h5 mb-2">@LiveTrends.WeekTitle(end, now)</h2>
+@if (older)
+{
+    <p class="bp-help-text">The battery has sent no reading since @ValueWords.Time(end), so the charts show its last week of readings. Every reading of that week, with when it was measured and how it arrived, is under the charts.</p>
+}
+else
+{
+    <p class="bp-help-text">Each value's latest reading and when it was taken, and how it went over the last week when the battery sent readings. Every reading of the week, with when it was measured and how it arrived, is under the charts.</p>
 }
-<h2 class="h5 mb-2">The last 7 days</h2>
-<p class="bp-help-text">Each value's latest reading and when it was taken, and how it went over the last week when the battery sent readings. Every reading of the week, with when it was measured and how it arrived, is under the charts.</p>
 <div class="bp-live-charts">
     @foreach (var chart in charts)
     {
@@ -35,7 +46,7 @@
             }
             else
             {
-                <span class="bp-live-chart-flat">@(chart.Readings switch { 0 => "No reading in the last 7 days", 1 => "One reading this week", _ => $"{Words.Count(chart.Readings, "reading")} this week" })</span>
+                <span class="bp-live-chart-flat">@(chart.Readings switch { 0 => older ? "No reading that week" : "No reading in the last 7 days", 1 => $"One reading {week}", _ => $"{Words.Count(chart.Readings, "reading")} {week}" })</span>
             }
             <small>@chart.Note</small>
         </figure>
diff --git a/web/Views/Passport/_PassportTabs.cshtml b/web/Views/Passport/_PassportTabs.cshtml
index e9a9472..c67e574 100644
--- a/web/Views/Passport/_PassportTabs.cshtml
+++ b/web/Views/Passport/_PassportTabs.cshtml
@@ -12,7 +12,7 @@
     // readings when there are any; an older version leads to the current one instead.
     var liveValues = Model.Audiences.SelectMany(section => section.Values).Where(value => value.Attribute.Change == BatteryPassWeb.Dpp.Catalog.ChangeRule.Live).ToArray();
     var live = Model.Current is null && liveValues.Length > 0
-        ? new LiveTab(liveValues, ViewData["LiveHistory"] as IReadOnlyList<BatteryPassWeb.Dpp.Api.LiveHistoryEntry> ?? [])
+        ? new LiveTab(liveValues, ViewData["LiveHistory"] as IReadOnlyList<BatteryPassWeb.Dpp.Api.LiveHistoryEntry> ?? [], ViewData["LiveWeekEnd"] as DateTime?)
         : null;
     var modules = Model.Modules is { Count: > 0 } listed ? listed : null;
     var extra = new List<(string Key, string Tab)>();
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~LatestWeekTests | FullyQualifiedName~LiveChartsTests | FullyQualifiedName~LiveTabRuleTests"`
Expected: `Passed!  - Failed:     0, Passed:    13`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/LatestWeekTests.cs BatteryPassWeb.Tests/Dpp/LiveChartsTests.cs BatteryPassWeb.Tests/Dpp/LiveTabRuleTests.cs web/Controllers/PassportController.cs web/Dpp/Api/BatteryApi.cs web/Dpp/Pages/LiveTrends.cs web/Views/Batteries/_LiveTiles.cshtml web/Views/Passport/_LiveHistory.cshtml web/Views/Passport/_PassportTabs.cshtml
git commit -m "feat(dpp): live values show the latest week with readings when the last 7 days had none" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: An earlier passport says what it is

The passports list names an earlier passport's link "Passport 1's page (read only)"; that page says in one line that it is an earlier passport, read only, with the way to the newest. Its cluster and module list are no longer offered, and the app refuses both.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/EarlierPassportPageTests.cs`
- Modify: `web/Dpp/Pages/BatteryCards.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/Batteries/_ModulesCard.cshtml`
- Modify: `web/Views/Shared/_PassportChain.cshtml`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/EarlierPassportPageTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R7, the owner's item 2 ("why do the old batteries passports cards in the versions have each battery page link
/// on them?", kept but made self-explanatory): the passports list says what an earlier passport's link leads to, and that
/// page says in one line that it is an earlier passport, read only, with the way to the newest. Nothing on it invites an
/// edit any more: its cluster and modules took changes (the page offered both and the app saved them); now neither is
/// offered, and the app refuses both.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class EarlierPassportPageTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task AnEarlierPassport_ShouldRefuseACluster_AndAModuleList_WhileTheNewestTakesThem()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var pages = BatteryTesting.Pages(rig);
        var newest = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;
        var before = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        Assert.Equal(BatteryPages.EarlierPassport, await pages.SetClusterAsync("team@example.com", rig.PackId, BatteryTesting.South));
        Assert.Equal(BatteryPages.EarlierPassport, await pages.SetModulesAsync("team@example.com", rig.PackId, "1; MOD-1; CAN-1"));
        var after = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal((before.ClusterId, before.Modules.Count), (after.ClusterId, after.Modules.Count));
        Assert.Null(await pages.SetModulesAsync("team@example.com", newest, "1; MOD-1; CAN-1"));
    }

    [Fact]
    public async Task AnEarlierPassportsPage_ShouldKnowItIsReplaced()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var newest = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;

        var earlier = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        var current = (await BatteryTesting.Pages(rig).BatteryAsync(newest))!;

        Assert.True(earlier.Publication.Replaced);
        Assert.True(earlier.Cards.Replaced);
        Assert.False(current.Publication.Replaced);
        Assert.Equal([false, true], earlier.Chain.Select(link => link.IsNewest));
    }

    [Fact]
    public void TheLinksAndThePage_ShouldSayWhatTheyAre_AndOfferNoEdit()
    {
        var chain = Read("web", "Views", "Shared", "_PassportChain.cshtml");
        var page = Read("web", "Views", "Batteries", "Battery.cshtml");
        var modules = Read("web", "Views", "Batteries", "_ModulesCard.cshtml");

        Assert.Contains("@(link.IsNewest ? \"Newest passport's page\" : $\"Passport {link.Number}'s page (read only)\")", chain, StringComparison.Ordinal);
        Assert.Contains("An earlier passport's page shows it as it was when it was replaced, to read only.", chain, StringComparison.Ordinal);
        Assert.Contains("Passport @thisPassport.Number of @Model.Chain.Count, an earlier passport, read only: nothing on it changes. <a href=\"@BatteryPages.BatteryHref(newestPassport.PackId)\">Open the newest passport's page</a>", page, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Cluster @if (!publication.Replaced) {<button type=\"button\" class=\"bp-text-button\" data-open-dialog=\"cluster-dialog\">Change</button>}</dt>", page, StringComparison.Ordinal);
        Assert.Contains("@if (Model.ForTeam && Model.Replaced)", modules, StringComparison.Ordinal);
        Assert.Contains("The module list stays as it was on this earlier passport; change it on the newest one.", modules, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~EarlierPassportPageTests"`
Expected: the build fails with CS0117, CS1061 (for example `error CS0117 'BatteryPages' does not contain a definition for 'EarlierPassport'`).

- [ ] **Step 3: Write the code**

Patch `r7-task2-code.patch` (`web/Dpp/Pages/BatteryCards.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/Batteries/_ModulesCard.cshtml`, `web/Views/Shared/_PassportChain.cshtml`). Save the block as `../r7-task2-code.patch`, outside the worktree, and run `git apply ../r7-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/BatteryCards.cs b/web/Dpp/Pages/BatteryCards.cs
index aafc5f3..390eea1 100644
--- a/web/Dpp/Pages/BatteryCards.cs
+++ b/web/Dpp/Pages/BatteryCards.cs
@@ -29,6 +29,9 @@ public sealed record BatteryCards(
     /// <summary>Whether nothing on the passport changes any more (step 4c).</summary>
     public bool Frozen => FrozenAs is not null;
 
+    /// <summary>An earlier passport, replaced by a newer one: kept as it was, its modules too (redesign R7).</summary>
+    public bool Replaced => FrozenAs == DppStatuses.Inactive;
+
     /// <summary>Why nothing on the passport changes, in words that fit its status (redesign R1); null while it can change.</summary>
     public string? FrozenNote => FrozenAs switch
     {
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index d2eb604..df1e13b 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -80,6 +80,9 @@ public sealed record PublicationView(
     /// <summary>Published once and no longer active: inactive, invalid or marked for deletion (step 4c).</summary>
     public bool NoLongerActive => Active is null && Latest is not null;
 
+    /// <summary>An earlier passport, replaced by a newer one (redesign R7): kept as it was.</summary>
+    public bool Replaced => Active is null && Latest?.DppStatus == DppStatuses.Inactive;
+
     public bool Ready => Missing.Count == 0 && Problems.Count == 0;
 
     public int MissingCount => Missing.Sum(group => group.Attributes.Count);
@@ -528,6 +531,11 @@ public sealed class BatteryPages(
             return "the battery was not found";
         }
 
+        if (await store.PassportStatusAsync(packId, cancellationToken) == DppStatuses.Inactive)
+        {
+            return EarlierPassport;
+        }
+
         var wanted = string.IsNullOrWhiteSpace(clusterId) ? null : clusterId.Trim();
         if (string.Equals(wanted, pack.ClusterId, StringComparison.OrdinalIgnoreCase))
         {
@@ -566,6 +574,9 @@ public sealed class BatteryPages(
     }
 
     /// <summary>The battery's module list as the team typed it (decision 31). Null when it was saved.</summary>
+    /// <summary>Why an earlier passport takes no change (redesign R7): it is kept as it was when it was replaced.</summary>
+    public const string EarlierPassport = "it is an earlier passport, kept as it was when it was replaced; change the newest passport instead";
+
     public Task<string?> SetModulesAsync(string who, string packId, string? text, CancellationToken cancellationToken = default) =>
         SaveModulesAsync(who, packId, _ => ModuleList.FromText(text), "typed", cancellationToken);
 
@@ -592,6 +603,11 @@ public sealed class BatteryPages(
             return "the battery was not found";
         }
 
+        if (await store.PassportStatusAsync(packId, cancellationToken) == DppStatuses.Inactive)
+        {
+            return EarlierPassport;
+        }
+
         var serial = Text(pack, "batterySerialNumber");
         var (modules, problem) = read(serial);
         if (problem is not null)
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 9023651..f9e505e 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -91,11 +91,17 @@
                     }
                     @await Html.PartialAsync("_StatusHelp")
                 </div>
-                @if (Model.Status?.Predecessor is { } replaced)
+                @* An earlier passport of a battery placed on the market again: one line says so, with the way to the newest
+                   (redesign R7, the owner's item 2); it replaces the two lines below. *@
+                @if (Model.Chain.FirstOrDefault(link => link.IsThis) is { IsNewest: false } thisPassport && Model.Chain.FirstOrDefault(link => link.IsNewest) is { } newestPassport)
+                {
+                    <p class="bp-lineage" id="earlier-passport"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>Passport @thisPassport.Number of @Model.Chain.Count, an earlier passport, read only: nothing on it changes. <a href="@BatteryPages.BatteryHref(newestPassport.PackId)">Open the newest passport's page</a></p>
+                }
+                else if (Model.Status?.Predecessor is { } replaced)
                 {
                     <p class="bp-lineage"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>New passport of @replaced.SerialNumber, whose passport @(replaced.Inactive ? "is now inactive" : "becomes inactive when this one is published").</p>
                 }
-                @if (Model.Status?.Succession is { Linked: true } linked && Model.Pack.Succession is { } succession)
+                @if (Model.Chain.FirstOrDefault(link => link.IsThis) is not { IsNewest: false } && Model.Status?.Succession is { Linked: true } linked && Model.Pack.Succession is { } succession)
                 {
                     <p class="bp-lineage"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>@(StatusBadge.Battery(succession.Status)?.Words ?? succession.Status) on @ValueWords.Day(succession.StartedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))@(linked.OtherPassport is { } other ? $" by {linked.OtherOperatorInfo ?? linked.OtherOperatorId}, which issued its new passport: {other}" : "; the battery continues on its new passport").</p>
                 }
@@ -112,7 +118,7 @@
                     <dl class="bp-bh-facts">
                         <div><dt>Batch</dt><dd class="text-wrap" title="@Model.BatchLabel"><a href="/admin/batches/@Uri.EscapeDataString(sources.Batch.Id)">@Model.BatchLabel</a></dd></div>
                         <div><dt>Economic operator</dt><dd class="text-wrap" title="@sources.Operator.Name"><a href="@ProductDataPages.SitesHref("operator", sources.Operator.Id)">@sources.Operator.Name</a></dd></div>
-                        <div><dt>Cluster <button type="button" class="bp-text-button" data-open-dialog="cluster-dialog">Change</button></dt><dd class="text-wrap@(Model.Pack.ClusterId is null ? " bp-batteries-muted" : null)">@Model.ClusterLabel</dd></div>
+                        <div><dt>Cluster @if (!publication.Replaced) {<button type="button" class="bp-text-button" data-open-dialog="cluster-dialog">Change</button>}</dt><dd class="text-wrap@(Model.Pack.ClusterId is null ? " bp-batteries-muted" : null)">@Model.ClusterLabel</dd></div>
                         @if (publication.IsPublished)
                         {
                             <div><dt>Made <span class="bp-attr-number">#17</span> · warranty <span class="bp-attr-number">#19</span></dt><dd>@Month(Value("manufacturingDate")) · @Period(Value("warrantyPeriod"))</dd></div>
diff --git a/web/Views/Batteries/_ModulesCard.cshtml b/web/Views/Batteries/_ModulesCard.cshtml
index 452815a..4329d90 100644
--- a/web/Views/Batteries/_ModulesCard.cshtml
+++ b/web/Views/Batteries/_ModulesCard.cshtml
@@ -35,7 +35,11 @@
             </table>
         </div>
     }
-    @if (Model.ForTeam)
+    @if (Model.ForTeam && Model.Replaced)
+    {
+        <p class="bp-help-text mt-2 mb-0" data-frozen>The module list stays as it was on this earlier passport; change it on the newest one.</p>
+    }
+    else if (Model.ForTeam)
     {
         <form method="post" action="@Model.Href/modules/from-export" enctype="multipart/form-data" class="bp-attr-upload bp-module-upload" data-upload-row>
             @Html.AntiForgeryToken()
diff --git a/web/Views/Shared/_PassportChain.cshtml b/web/Views/Shared/_PassportChain.cshtml
index 2558a53..aeb3b86 100644
--- a/web/Views/Shared/_PassportChain.cshtml
+++ b/web/Views/Shared/_PassportChain.cshtml
@@ -8,7 +8,7 @@
     <section class="bp-card bp-console-form-card bp-battery-card mb-4" id="passport-chain">
         <span class="bp-summary-title">Passports of this battery</span>
         <h2>@Words.Count(Model.Shown.Count, "passport")</h2>
-        <p class="mb-0">The battery was placed on the market again, and each time got a new passport.@(Model.Reader == ChainReader.ClusterAdmin ? " Only public facts are shown: an earlier passport may have belonged to another customer." : null)</p>
+        <p class="mb-0">The battery was placed on the market again, and each time got a new passport.@(Model.Reader switch { ChainReader.ClusterAdmin => " Only public facts are shown: an earlier passport may have belonged to another customer.", ChainReader.Team => " An earlier passport's page shows it as it was when it was replaced, to read only.", _ => null })</p>
         <ol class="bp-passport-chain">
             @foreach (var link in Model.Shown)
             {
@@ -33,11 +33,12 @@
                         @switch (Model.Reader)
                         {
                             case ChainReader.Team:
+                                @* What each link leads to, in its words (redesign R7, the owner's item 2). *@
                                 @if (!link.IsThis)
                                 {
-                                    <a href="@BatteryPages.BatteryHref(link.PackId)">Battery page</a>
+                                    <a href="@BatteryPages.BatteryHref(link.PackId)" title="@(link.IsNewest ? "The newest passport's battery page" : "This passport's battery page as it was when it was replaced; nothing on it changes")">@(link.IsNewest ? "Newest passport's page" : $"Passport {link.Number}'s page (read only)")</a>
                                 }
-                                <a href="/admin/audit?q=@Uri.EscapeDataString(link.PackId)">History</a>
+                                <a href="/admin/audit?q=@Uri.EscapeDataString(link.PackId)" title="Everything that happened to passport @link.Number">History</a>
                                 break;
                             case ChainReader.Detail:
                                 @if (!link.IsThis)
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~EarlierPassportPageTests"`
Expected: `Passed!  - Failed:     0, Passed:     3`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/EarlierPassportPageTests.cs web/Dpp/Pages/BatteryCards.cs web/Dpp/Pages/BatteryPages.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_ModulesCard.cshtml web/Views/Shared/_PassportChain.cshtml
git commit -m "fix(dpp): an earlier passport's links and page say what they are; nothing on it invites an edit" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: One shared battery list

The team's Batteries and the Registry draw one list: the same search, columns and row icons. A serial number opens the battery page for the team and its cluster admins, the detail page for others. Each page keeps its own rows and rules.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/RegistryListTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/RowActionsTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/SharedBatteryListTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Look/TeamListRowTests.cs`
- Create: `web/Dpp/Pages/BatteryListView.cs`
- Modify: `web/Dpp/Pages/RegistryList.cs`
- Modify: `web/Views/Batteries/Batteries.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`
- Create: `web/Views/Shared/_BatteryList.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `r7-task3-tests.patch` (`BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`, `BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`, `BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs`, `BatteryPassWeb.Tests/Dpp/RegistryListTests.cs`, `BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs`, `BatteryPassWeb.Tests/Dpp/RowActionsTests.cs`, `BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs`, `BatteryPassWeb.Tests/Look/TeamListRowTests.cs`). Save the block as `../r7-task3-tests.patch`, outside the worktree, and run `git apply ../r7-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
index 6f8116a..59ef6b1 100644
--- a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
@@ -20,11 +20,12 @@ public sealed class AdminDenseConsoleLayoutTests
     public void Registry_ShouldUseDenseConsoleTableAndToolbar()
     {
         var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
+        var list = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryList.cshtml")); // the shared list since R7
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.Contains("bp-registry-page", registry);
-        Assert.Contains("bp-console-toolbar", registry);
-        Assert.Contains("bp-console-table", registry); // the Registry's own table on the new model (decision 30)
+        Assert.Contains("bp-console-toolbar", list);
+        Assert.Contains("bp-console-table", list); // the Registry's own table on the new model (decision 30)
         Assert.Contains(".bp-console-table", css);
         Assert.Contains(".bp-battery-id-cell", css);
         Assert.Contains("white-space: nowrap", css);
diff --git a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
index 54f9b89..a5e18cf 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
@@ -178,7 +178,7 @@ public sealed class BatteriesControllerTests(MongoFixture mongo)
 
         Assert.All(new[] { list, battery, create }, view => Assert.Contains("~/css/bp-product-data.css", view, StringComparison.Ordinal));
         Assert.Contains("PartialAsync(\"_AdminTabs\", \"batteries\")", list, StringComparison.Ordinal);
-        Assert.Contains("Passport ID, battery ID, serial or cluster", list, StringComparison.Ordinal);
+        Assert.Contains("\"Serial number, battery ID, passport ID or cluster\"", File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Pages", "BatteryListView.cs")), StringComparison.Ordinal); // the shared list's search since R7
         foreach (var card in new[] { "publication", "values", "live", "accidents", "versions", "remove" })
         {
             Assert.Contains($"id=\"{card}\"", battery, StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
index d14c531..815ebaf 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs
@@ -143,12 +143,12 @@ public sealed class PassportMarkPagesTests(MongoFixture mongo)
     {
         var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
         var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
-        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));
+        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_BatteryList.cshtml")); // the Registry's list since R7
 
         Assert.Contains("~/Views/Batteries/_MarkDialog.cshtml\", markable", battery, StringComparison.Ordinal); // a window since R5
         Assert.Contains("Model.MayMarkInvalid", hero, StringComparison.Ordinal);
         Assert.Contains("Model.MarkReason is { } reason", hero, StringComparison.Ordinal);
-        Assert.Contains("StatusBadge.Passport(shown.DppStatus)", registry, StringComparison.Ordinal); // a badge since redesign R3
+        Assert.Contains("StatusBadge.Passport(row.IsDraft ? null : (row.Active ?? row.Latest)!.DppStatus)", registry, StringComparison.Ordinal); // a badge since redesign R3
     }
 
     private static PassportController Controller(PublicationRig rig, string email, string role)
diff --git a/BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs b/BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs
index b7233b2..b688d4d 100644
--- a/BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs
@@ -55,10 +55,12 @@ public sealed class RegistryBadgesTests(MongoFixture mongo)
         Assert.Contains("bp-wait mt-4\" id=\"waiting\"", registry, StringComparison.Ordinal);
         Assert.Contains("<use href=\"#i-bell\" />", waiting, StringComparison.Ordinal);
         Assert.True(waiting.IndexOf("value=\"true\"", StringComparison.Ordinal) < waiting.IndexOf("value=\"false\"", StringComparison.Ordinal)); // Accept first
-        Assert.Contains("@if (Model.BatteryStatusShown.Contains(row.PackId) && StatusBadge.Battery(row.BatteryStatus) is { } battery)", registry, StringComparison.Ordinal);
-        Assert.Contains("StatusBadge.Passport(shown.DppStatus)", registry, StringComparison.Ordinal);
-        Assert.Contains("_RowActions", registry, StringComparison.Ordinal); // the Open button became the row's icons (R5)
-        Assert.DoesNotContain(">Manage</a>", registry, StringComparison.Ordinal);
-        Assert.Contains("StatusBadge.Battery(row.BatteryStatus)", View("Batteries", "Batteries.cshtml"), StringComparison.Ordinal);
+        // The shared list (R7) draws both badges; the Registry says whose battery status (#22) the reader may see.
+        var list = View("Shared", "_BatteryList.cshtml");
+        Assert.Contains("row => Model.BatteryStatusShown.Contains(row.PackId)", registry, StringComparison.Ordinal);
+        Assert.Contains("@if (Model.ShowsBatteryStatus(row) && StatusBadge.Battery(row.BatteryStatus) is { } battery)", list, StringComparison.Ordinal);
+        Assert.Contains("StatusBadge.Passport(row.IsDraft ? null : (row.Active ?? row.Latest)!.DppStatus)", list, StringComparison.Ordinal);
+        Assert.Contains("_RowActions", list, StringComparison.Ordinal); // the Open button became the row's icons (R5)
+        Assert.DoesNotContain(">Manage</a>", list, StringComparison.Ordinal);
     }
 }
diff --git a/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs b/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs
index d0ab9c7..ea58e4f 100644
--- a/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs
@@ -51,9 +51,11 @@ public sealed class RegistryListTests
     {
         var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));
 
-        Assert.Contains("/dpp/@Uri.EscapeDataString(row.Identifier)/detail", view);
+        // Since R7 a serial number opens the battery page for the team and its cluster admins, the detail page for the
+        // others (RegistryPage.OpenHref, tested in SharedBatteryListTests).
+        Assert.Contains("Model.OpenHref, Model.Actions", view);
         Assert.DoesNotContain("_BatteryTable", view); // the old model's table is gone from the Registry
-        Assert.Contains("Model.Actions(row)", view); // step 4a's link to the cluster admin's page, among the row's icons since R5
+        Assert.Contains("<a href=\"@Model.OpenHref(row)\" class=\"bp-battery-link", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_BatteryList.cshtml")));
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs b/BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs
index 0cad039..5dd5752 100644
--- a/BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs
@@ -12,11 +12,12 @@ public sealed class RegistrySearchWordsTests
     [Fact]
     public void TheRegistrysSearch_ShouldSayWhatItMatched_AndOfferShowAll()
     {
-        var registry = View("Registry", "Index.cshtml");
+        var registry = View("Shared", "_BatteryList.cshtml"); // the Registry's list is the shared list since R7
 
         Assert.Contains("$\"{Words.Count(Model.Rows.Count, \"battery\", \"batteries\")} match “{Model.Query}”.\"", registry, StringComparison.Ordinal);
-        Assert.Contains("$\"No battery you may open matches “{Model.Query}”.\"", registry, StringComparison.Ordinal);
-        Assert.Equal(2, registry.Split("<a href=\"/registry\">Show all</a>").Length - 1);
+        Assert.Contains("$\"No battery matches “{Model.Query}”.\"", registry, StringComparison.Ordinal);
+        Assert.Equal(2, registry.Split("<a href=\"@Model.Action\">Show all</a>").Length - 1);
+        Assert.Contains("new BatteryListView(\"/registry\", Model.Query", View("Registry", "Index.cshtml"), StringComparison.Ordinal);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/Dpp/RowActionsTests.cs b/BatteryPassWeb.Tests/Dpp/RowActionsTests.cs
index 11b581c..f7877b7 100644
--- a/BatteryPassWeb.Tests/Dpp/RowActionsTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/RowActionsTests.cs
@@ -82,13 +82,13 @@ public sealed class RowActionsTests(MongoFixture mongo)
             Assert.Contains($"data-tooltip=\"{name}\"><svg class=\"bp-report-action-icon\" aria-hidden=\"true\"><use href=\"#{icon}\" />", icons, StringComparison.Ordinal);
         }
 
-        Assert.Contains("@await Html.PartialAsync(\"_RowActions\", Model.Actions(row))", registry, StringComparison.Ordinal);
-        Assert.Contains("@await Html.PartialAsync(\"_RowActions\", RowActions.For(row, BatteryPages.BatteryHref(row.PackId), team: true))", team, StringComparison.Ordinal);
-        Assert.DoesNotContain(">Open<svg", registry, StringComparison.Ordinal); // R3's Open button is gone
-        // The icons stay in view at 1280 px: the battery ID shortened in the middle, the batch and cluster wrapping.
-        Assert.Contains("@BatteryPages.ShortId(row.Identifier)</small>", team, StringComparison.Ordinal);
-        Assert.Contains("@BatteryPages.ShortId(row.Identifier)</small>", registry, StringComparison.Ordinal);
+        // One shared list since R7: each page brings its own icons, the list draws them at the end of each row.
+        var list = View("Shared", "_BatteryList.cshtml");
+        Assert.Contains("@await Html.PartialAsync(\"_RowActions\", Model.Actions(row))", list, StringComparison.Ordinal);
+        Assert.Contains("Model.OpenHref, Model.Actions,", registry, StringComparison.Ordinal);
+        Assert.Contains("row => RowActions.For(row, BatteryPages.BatteryHref(row.PackId), team: true)", team, StringComparison.Ordinal);
+        Assert.DoesNotContain(">Open<svg", list, StringComparison.Ordinal); // R3's Open button is gone
         Assert.Contains(".bp-console-table:has(.bp-actions-column) td:is([data-label=\"Batch\"], [data-label=\"Cluster\"]) {", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css")), StringComparison.Ordinal);
-        Assert.Equal(2, team.Split("<span class=\"visually-hidden\">Actions</span>").Length - 1 + registry.Split("<span class=\"visually-hidden\">Actions</span>").Length - 1);
+        Assert.Equal(1, list.Split("<span class=\"visually-hidden\">Actions</span>").Length - 1);
     }
 }
diff --git a/BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs b/BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs
index 50af8d1..6cbdabf 100644
--- a/BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs
+++ b/BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs
@@ -25,9 +25,11 @@ public sealed class StatusHelpLinkTests
     [Fact]
     public void BothLists_ShouldExplainTheStatusesInTheirHeading()
     {
+        // Both lists are one shared list since R7 (the owner's choice "One shared list").
+        Assert.Contains("<th>Status @await Html.PartialAsync(\"_StatusHelp\")</th>", Read("web", "Views", "Shared", "_BatteryList.cshtml"), StringComparison.Ordinal);
         foreach (var view in new[] { Read("web", "Views", "Batteries", "Batteries.cshtml"), Read("web", "Views", "Registry", "Index.cshtml") })
         {
-            Assert.Contains("<th>Status @await Html.PartialAsync(\"_StatusHelp\")</th>", view, StringComparison.Ordinal);
+            Assert.Contains("@await Html.PartialAsync(\"_BatteryList\", new BatteryListView(", view, StringComparison.Ordinal);
         }
     }
 }
diff --git a/BatteryPassWeb.Tests/Look/TeamListRowTests.cs b/BatteryPassWeb.Tests/Look/TeamListRowTests.cs
index a48775a..3d30f11 100644
--- a/BatteryPassWeb.Tests/Look/TeamListRowTests.cs
+++ b/BatteryPassWeb.Tests/Look/TeamListRowTests.cs
@@ -11,12 +11,12 @@ public sealed class TeamListRowTests
     [Fact]
     public void EachRow_ShouldCopyTheFullBatteryId_AndSayWhenItWasUpdated()
     {
-        var list = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Batteries.cshtml"));
-        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));
+        // R7 (the owner's choice "Copy icon only"): the shared list keeps the copy button and says the version and update
+        // in its own short column, "v2 · 3 Nov", in full on hover.
+        var list = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_BatteryList.cshtml"));
 
-        Assert.Contains("@BatteryPages.ShortId(row.Identifier)</small>@await Html.PartialAsync(\"_CopyIdButton\", row.Identifier)</span>", list, StringComparison.Ordinal);
-        Assert.Contains("<small class=\"d-block text-secondary mt-1\">Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>", list, StringComparison.Ordinal);
-        Assert.Contains("Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>", registry, StringComparison.Ordinal); // the same words
-        Assert.DoesNotContain("· version {version.Number}", list, StringComparison.Ordinal); // said once, under the badges
+        Assert.Contains("@row.SerialNumber</a>@await Html.PartialAsync(\"_CopyIdButton\", row.Identifier)</span>", list, StringComparison.Ordinal);
+        Assert.Contains("<td data-label=\"Updated\" title=\"@BatteryListView.UpdatedInFull(row)\">@(BatteryListView.Updated(row) ?? \"—\")</td>", list, StringComparison.Ordinal);
+        Assert.DoesNotContain("· version {version.Number}", list, StringComparison.Ordinal);
     }
 }
```

`BatteryPassWeb.Tests/Dpp/SharedBatteryListTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R7, the owner's item 4 ("open registry and batteries page have the same content but behave differently")
/// and choice "One shared list": both pages draw one list, with the same search, columns and row icons. A serial number
/// opens the battery page for the team and its cluster admins, the detail page for everyone else. Each page keeps its own
/// rows and rules: who is listed, who sees clusters and the battery status (#22), the Registry's hand-overs.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SharedBatteryListTests(MongoFixture mongo)
{
    private static string View(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task ASerialNumber_ShouldOpenTheBatteryPage_ForTheTeamAndItsClusterAdmins_AndTheDetailPageForOthers()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var batteries = await BatteryTesting.Pages(rig).ListAsync(null);
        var row = Assert.Single(batteries.Rows);

        var team = RegistryList.Build(Reader.With("team", ReaderRole.Team), batteries, ChangeSender.Team("team"));
        var admin = RegistryList.Build(Reader.With("admin", ReaderRole.ClusterAdmin, BatteryTesting.North), batteries, ChangeSender.ClusterAdmin("admin", BatteryTesting.North));
        var member = RegistryList.Build(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), batteries);
        var surveillance = RegistryList.Build(Reader.With("msa", ReaderRole.MarketSurveillance), batteries);

        Assert.Equal(BatteryPages.BatteryHref(row.PackId), team.OpenHref(row));
        Assert.Equal(ClusterBatteryPages.BatteryHref(row.PackId), admin.OpenHref(row));
        Assert.Equal(PassportPages.DetailHref(row.Identifier), member.OpenHref(row));
        Assert.Equal(PassportPages.DetailHref(row.Identifier), surveillance.OpenHref(row));
    }

    [Fact]
    public async Task TheUpdatedColumn_ShouldSayTheVersionAndDayShort_AndInFullOnHover()
    {
        var published = Assert.Single((await BatteryTesting.Pages(await PublicationTesting.PublishedRigAsync(mongo)).ListAsync(null)).Rows);
        var draft = Assert.Single((await BatteryTesting.Pages(await PublicationTesting.RigAsync(mongo)).ListAsync(null)).Rows);
        var shown = (published.Active ?? published.Latest)!;

        Assert.Equal($"v{shown.Number} · {shown.LastUpdatedUtc.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture)}", BatteryListView.Updated(published));
        Assert.Equal($"Version {shown.Number} · updated {ValueWords.Time(shown.LastUpdatedUtc)}", BatteryListView.UpdatedInFull(published));
        Assert.Null(BatteryListView.Updated(draft));
    }

    [Fact]
    public void TheSearch_ShouldMentionClustersOnlyToThoseWhoMaySearchThem()
    {
        static BatteryListView List(bool clusters) => new("/registry", string.Empty, [], clusters, _ => string.Empty, _ => null!, _ => false);

        Assert.Equal("Serial number, battery ID, passport ID or cluster", List(clusters: true).Placeholder);
        Assert.Equal("Serial number, battery ID or passport ID", List(clusters: false).Placeholder); // R6: no cluster search for them
    }

    [Fact]
    public void BothPages_ShouldDrawTheSharedList_WithTheirOwnRules()
    {
        var team = View("Batteries", "Batteries.cshtml");
        var registry = View("Registry", "Index.cshtml");
        var list = View("Shared", "_BatteryList.cshtml");

        Assert.Contains("new BatteryListView(\"/admin/batteries\", Model.Query, Model.Rows, ShowClusters: true,", team, StringComparison.Ordinal);
        Assert.Contains("CreateHref = \"/admin/batteries/new\",", team, StringComparison.Ordinal);
        Assert.Contains("new BatteryListView(\"/registry\", Model.Query, Model.Rows, Model.ShowClusters,\n        Model.OpenHref, Model.Actions, row => Model.BatteryStatusShown.Contains(row.PackId))", registry, StringComparison.Ordinal);
        Assert.True(registry.IndexOf("id=\"waiting\"", StringComparison.Ordinal) < registry.IndexOf("_BatteryList", StringComparison.Ordinal)); // the hand-overs stay first
        Assert.DoesNotContain("<table", team, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", registry, StringComparison.Ordinal);
        Assert.Contains("<th>Serial number</th>\n                        <th>Model</th>\n                        <th>Batch</th>", list, StringComparison.Ordinal);
        Assert.Contains("<th>Updated</th>", list, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"@Model.Placeholder\"", list, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminDenseConsoleLayoutTests | FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~PassportMarkPagesTests | FullyQualifiedName~RegistryBadgesTests | FullyQualifiedName~RegistryListTests | FullyQualifiedName~RegistrySearchWordsTests | FullyQualifiedName~RowActionsTests | FullyQualifiedName~SharedBatteryListTests | FullyQualifiedName~StatusHelpLinkTests | FullyQualifiedName~TeamListRowTests"`
Expected: the build fails with CS0103, CS0246, CS1061 (for example `error CS0103 The name 'BatteryListView' does not exist in the current context`).

- [ ] **Step 3: Write the code**

Patch `r7-task3-code.patch` (`web/Dpp/Pages/RegistryList.cs`, `web/Views/Batteries/Batteries.cshtml`, `web/Views/Registry/Index.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r7-task3-code.patch`, outside the worktree, and run `git apply ../r7-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/RegistryList.cs b/web/Dpp/Pages/RegistryList.cs
index 79e9cfb..f0f66e4 100644
--- a/web/Dpp/Pages/RegistryList.cs
+++ b/web/Dpp/Pages/RegistryList.cs
@@ -28,6 +28,16 @@ public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query,
             : null;
     }
 
+    /// <summary>
+    /// Where a battery's serial number leads (redesign R7): its battery page for the team and its cluster admins, as the
+    /// pencil does; its detail page for everyone else.
+    /// </summary>
+    public string OpenHref(BatteryRow row)
+    {
+        ArgumentNullException.ThrowIfNull(row);
+        return (Team ? BatteryPages.BatteryHref(row.PackId) : ManageHref(row)) ?? PassportPages.DetailHref(row.Identifier);
+    }
+
     /// <summary>True for the team: its rows open the team's battery page and its history (redesign R5).</summary>
     public bool Team { get; init; }
 
diff --git a/web/Views/Batteries/Batteries.cshtml b/web/Views/Batteries/Batteries.cshtml
index d84182d..0d30fd0 100644
--- a/web/Views/Batteries/Batteries.cshtml
+++ b/web/Views/Batteries/Batteries.cshtml
@@ -18,91 +18,13 @@
                 One passport per battery. A new battery is a draft, seen only by your team, until its passport is published.
             </p>
         </div>
-        <div class="bp-batteries-tools mt-3">
-            <form method="get" action="/admin/batteries" class="bp-batteries-search" role="search">
-                <label class="visually-hidden" for="battery-search">Search batteries</label>
-                <input id="battery-search" type="search" name="q" value="@Model.Query" placeholder="Passport ID, battery ID, serial or cluster" />
-                <button type="submit" class="bp-secondary-button">Search</button>
-                @if (Model.Query.Length > 0)
-                {
-                    <a href="/admin/batteries" class="bp-batteries-clear">Show all</a>
-                }
-            </form>
-            <a href="/admin/batteries/new" class="bp-primary-button text-nowrap">Create battery</a>
-        </div>
     </section>
 
-    @if (Model.Total == 0)
-    {
-        <div class="bp-card mt-4">No batteries yet. Use Create battery to add the first; it starts as a draft.</div>
-    }
-    else if (Model.Rows.Count == 0)
+    @* The shared list (redesign R7, the owner's choice "One shared list"): every battery, drafts too, and Create. *@
+    @await Html.PartialAsync("_BatteryList", new BatteryListView("/admin/batteries", Model.Query, Model.Rows, ShowClusters: true,
+        row => BatteryPages.BatteryHref(row.PackId), row => RowActions.For(row, BatteryPages.BatteryHref(row.PackId), team: true), _ => true)
     {
-        <div class="bp-card mt-4">No battery matches “@Model.Query”. Search by passport ID, battery ID, serial number or cluster, or <a href="/admin/batteries">show all</a>.</div>
-    }
-    else
-    {
-        <section class="bp-card bp-table-card bp-batteries-card mt-4">
-            <p class="bp-batteries-count">
-                @(Model.Query.Length > 0 ? $"{Words.Count(Model.Rows.Count, "battery", "batteries")} of {Model.Total} match “{Model.Query}”." : $"{Words.Count(Model.Total, "battery", "batteries")}.")
-            </p>
-            <div class="bp-table-scroll">
-                <table class="table align-middle bp-console-table bp-batteries-table bp-mobile-card-table">
-                    <thead>
-                        <tr>
-                            <th>Serial number</th>
-                            <th>Model</th>
-                            <th>Batch</th>
-                            <th>Cluster</th>
-                            <th>Status @await Html.PartialAsync("_StatusHelp")</th>
-                            <th class="bp-actions-column"><span class="visually-hidden">Actions</span></th>
-                        </tr>
-                    </thead>
-                    <tbody>
-                        @foreach (var row in Model.Rows)
-                        {
-                            <tr>
-                                <td data-label="Serial number">
-                                    <a href="@BatteryPages.BatteryHref(row.PackId)" class="bp-battery-link">@row.SerialNumber</a>
-                                    @* The full battery ID to copy, as the old list had (redesign R6, the owner's choice 3). *@
-                                    <span class="bp-id-copy-row"><small class="bp-battery-id" title="Battery ID (#6): @row.Identifier">@BatteryPages.ShortId(row.Identifier)</small>@await Html.PartialAsync("_CopyIdButton", row.Identifier)</span>
-                                </td>
-                                <td data-label="Model">@row.ModelName <small class="bp-batteries-muted">@row.FamilyName</small></td>
-                                <td data-label="Batch">@row.BatchLabel</td>
-                                <td data-label="Cluster">
-                                    @if (row.ClusterId is null)
-                                    {
-                                        <span class="bp-batteries-muted">No cluster yet</span>
-                                    }
-                                    else
-                                    {
-                                        @row.ClusterLabel
-                                    }
-                                </td>
-                                <td data-label="Status">
-                                    @* Both statuses as badges, as everywhere (redesign R3). *@
-                                    <div class="bp-badges is-compact">
-                                        @if (StatusBadge.Battery(row.BatteryStatus) is { } battery)
-                                        {
-                                            @await Html.PartialAsync("_StatusBadge", battery)
-                                        }
-                                        @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(row.IsDraft ? null : row.StatusName))
-                                    </div>
-                                    @* The version and when it was last updated, as on the Registry (redesign R6, the owner's choice 3). *@
-                                    @if ((row.Active ?? row.Latest) is { } shown)
-                                    {
-                                        <small class="d-block text-secondary mt-1">Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>
-                                    }
-                                </td>
-                                <td class="bp-action-cell" data-label="Actions">
-                                    @* Icons named on hover, like the old app's row actions (redesign R5, owner's item 1). *@
-                                    @await Html.PartialAsync("_RowActions", RowActions.For(row, BatteryPages.BatteryHref(row.PackId), team: true))
-                                </td>
-                            </tr>
-                        }
-                    </tbody>
-                </table>
-            </div>
-        </section>
-    }
+        CreateHref = "/admin/batteries/new",
+        Empty = "No batteries yet. Use Create battery to add the first; it starts as a draft.",
+    })
 </main>
diff --git a/web/Views/Registry/Index.cshtml b/web/Views/Registry/Index.cshtml
index 4c7e918..8a803c8 100644
--- a/web/Views/Registry/Index.cshtml
+++ b/web/Views/Registry/Index.cshtml
@@ -44,77 +44,11 @@
         </section>
     }
 
-    <section class="bp-console-toolbar bp-battery-table-toolbar">
-        <form action="/registry" class="bp-search-form" role="search">
-            <input name="q" value="@Model.Query" placeholder="Serial number, battery ID or passport ID" aria-label="Search the registry" />
-            <button type="submit">Search</button>
-        </form>
-    </section>
-
-    <section class="bp-card bp-console-form-card bp-product-editor-section mt-4">
-        @if (Model.Rows.Count == 0)
-        {
-            @* A search says what it looked for and how to see every battery again, like the team's list (redesign R6). *@
-            <p class="mb-0">@(string.IsNullOrWhiteSpace(Model.Query) ? "No batteries to show." : $"No battery you may open matches “{Model.Query}”.")@if (!string.IsNullOrWhiteSpace(Model.Query))
-                {
-                    <text> <a href="/registry">Show all</a></text>
-                }</p>
-        }
-        else
-        {
-            <p class="text-secondary">@(string.IsNullOrWhiteSpace(Model.Query) ? $"{Words.Count(Model.Rows.Count, "battery", "batteries")}." : $"{Words.Count(Model.Rows.Count, "battery", "batteries")} match “{Model.Query}”.")@if (!string.IsNullOrWhiteSpace(Model.Query))
-                {
-                    <text> <a href="/registry">Show all</a></text>
-                }</p>
-            <div class="bp-table-scroll">
-                <table class="table align-middle bp-console-table bp-mobile-card-table mb-0">
-                    <thead>
-                        <tr>
-                            <th>Serial number</th>
-                            <th>Model</th>
-                            <th>Batch</th>
-                            @if (Model.ShowClusters)
-                            {
-                                <th>Cluster</th>
-                            }
-                            <th>Status @await Html.PartialAsync("_StatusHelp")</th>
-                            <th class="bp-actions-column"><span class="visually-hidden">Actions</span></th>
-                        </tr>
-                    </thead>
-                    <tbody>
-                        @foreach (var row in Model.Rows)
-                        {
-                            <tr>
-                                <td data-label="Serial number">
-                                    <a href="/dpp/@Uri.EscapeDataString(row.Identifier)/detail"><strong>@row.SerialNumber</strong></a>
-                                    <small class="d-block text-secondary" title="Battery ID (#6): @row.Identifier">@BatteryPages.ShortId(row.Identifier)</small>
-                                </td>
-                                <td data-label="Model">@row.ModelName <small class="d-block text-secondary">@row.FamilyName</small></td>
-                                <td data-label="Batch">@row.BatchLabel</td>
-                                @if (Model.ShowClusters)
-                                {
-                                    <td data-label="Cluster">@row.ClusterLabel</td>
-                                }
-                                @{ var shown = (row.Active ?? row.Latest)!; }
-                                <td data-label="Status">
-                                    <div class="bp-badges is-compact">
-                                        @if (Model.BatteryStatusShown.Contains(row.PackId) && StatusBadge.Battery(row.BatteryStatus) is { } battery)
-                                        {
-                                            @await Html.PartialAsync("_StatusBadge", battery)
-                                        }
-                                        @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(shown.DppStatus))
-                                    </div>
-                                    <small class="d-block text-secondary mt-1">Version @shown.Number · updated @ValueWords.Time(shown.LastUpdatedUtc)</small>
-                                </td>
-                                <td class="bp-action-cell" data-label="Actions">
-                                    @* Icons named on hover, like the old app's row actions (redesign R5, owner's item 1). *@
-                                    @await Html.PartialAsync("_RowActions", Model.Actions(row))
-                                </td>
-                            </tr>
-                        }
-                    </tbody>
-                </table>
-            </div>
-        }
-    </section>
+    @* The shared list (redesign R7, the owner's choice "One shared list"): the same search, columns and icons as the
+       team's Batteries page; each reader's rows, clusters and battery statuses as before. *@
+    @await Html.PartialAsync("_BatteryList", new BatteryListView("/registry", Model.Query, Model.Rows, Model.ShowClusters,
+        Model.OpenHref, Model.Actions, row => Model.BatteryStatusShown.Contains(row.PackId))
+    {
+        Empty = Model.Scope,
+    })
 </main>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index ee64750..e8d4a5f 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3632,3 +3632,14 @@ main .bp-field-marker.is-published {
     grid-column: 2;
   }
 }
+
+/* ==== One battery list (redesign R7, the owner's item 4) ============================================================ */
+
+/* The search and, for the team, Create battery: one row above the list on both pages. */
+.bp-battery-list-tools {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  justify-content: space-between;
+  gap: 12px;
+}
```

`web/Dpp/Pages/BatteryListView.cs`:

```csharp
using System.Globalization;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One battery list for the team's Batteries page and the Registry (redesign R7, the owner's choice "One shared list"):
/// the same search, columns and row icons; each page brings its own rows, rules and links. Who sees which batteries,
/// clusters and battery statuses (#22) stays each page's rule, unchanged.
/// </summary>
/// <param name="Action">Where the search goes: "/admin/batteries" or "/registry".</param>
/// <param name="ShowClusters">Whether the reader may see clusters (and search by them, R6).</param>
/// <param name="OpenHref">Where a battery's serial number leads: its battery page for the team and its cluster admins, else its detail page.</param>
/// <param name="ShowsBatteryStatus">Whether the reader may see this battery's status (#22, purchaser data).</param>
public sealed record BatteryListView(
    string Action,
    string Query,
    IReadOnlyList<BatteryRow> Rows,
    bool ShowClusters,
    Func<BatteryRow, string> OpenHref,
    Func<BatteryRow, RowActions> Actions,
    Func<BatteryRow, bool> ShowsBatteryStatus)
{
    /// <summary>The team's "Create battery" button beside the search; null on the Registry.</summary>
    public string? CreateHref { get; init; }

    /// <summary>What an empty list says when nothing was searched.</summary>
    public string Empty { get; init; } = "No batteries to show.";

    /// <summary>What the search finds by: a cluster only for readers who may see clusters (R6).</summary>
    public string Placeholder => ShowClusters ? "Serial number, battery ID, passport ID or cluster" : "Serial number, battery ID or passport ID";

    public bool Searched => !string.IsNullOrWhiteSpace(Query);

    /// <summary>The version and its last update, short: "v2 · 3 Nov"; null for a draft.</summary>
    public static string? Updated(BatteryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return (row.Active ?? row.Latest) is { } shown ? $"v{shown.Number} · {shown.LastUpdatedUtc.ToString("d MMM", CultureInfo.InvariantCulture)}" : null;
    }

    /// <summary>The same in full, on hover: "Version 2 · updated 3 Nov 2026, 15:36".</summary>
    public static string? UpdatedInFull(BatteryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return (row.Active ?? row.Latest) is { } shown ? $"Version {shown.Number} · updated {ValueWords.Time(shown.LastUpdatedUtc)}" : null;
    }
}
```

`web/Views/Shared/_BatteryList.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model BatteryListView
@* One battery list for the team's Batteries page and the Registry (redesign R7, the owner's choice 4, "One shared
   list"): the same search, columns and row icons; cards on a phone. The battery ID is behind the copy icon (copied in
   full, shown on hover). *@
<section class="bp-console-toolbar bp-battery-list-tools mt-4">
    <form method="get" action="@Model.Action" class="bp-batteries-search" role="search">
        <label class="visually-hidden" for="battery-search">Search batteries</label>
        <input id="battery-search" type="search" name="q" value="@Model.Query" placeholder="@Model.Placeholder" />
        <button type="submit" class="bp-secondary-button">Search</button>
    </form>
    @if (Model.CreateHref is { } create)
    {
        <a href="@create" class="bp-primary-button text-nowrap">Create battery</a>
    }
</section>

<section class="bp-card bp-table-card bp-batteries-card mt-3">
    @if (Model.Rows.Count == 0)
    {
        <p class="bp-batteries-count">@(Model.Searched ? $"No battery matches “{Model.Query}”." : Model.Empty)@if (Model.Searched)
            {
                <text> <a href="@Model.Action">Show all</a></text>
            }</p>
    }
    else
    {
        <p class="bp-batteries-count">@(Model.Searched ? $"{Words.Count(Model.Rows.Count, "battery", "batteries")} match “{Model.Query}”." : $"{Words.Count(Model.Rows.Count, "battery", "batteries")}.")@if (Model.Searched)
            {
                <text> <a href="@Model.Action">Show all</a></text>
            }</p>
        <div class="bp-table-scroll">
            <table class="table align-middle bp-console-table bp-batteries-table bp-mobile-card-table">
                <thead>
                    <tr>
                        <th>Serial number</th>
                        <th>Model</th>
                        <th>Batch</th>
                        @if (Model.ShowClusters)
                        {
                            <th>Cluster</th>
                        }
                        <th>Status @await Html.PartialAsync("_StatusHelp")</th>
                        <th>Updated</th>
                        <th class="bp-actions-column"><span class="visually-hidden">Actions</span></th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var row in Model.Rows)
                    {
                        <tr>
                            <td data-label="Serial number">
                                <span class="bp-id-copy-row" title="Battery ID (#6): @row.Identifier"><a href="@Model.OpenHref(row)" class="bp-battery-link">@row.SerialNumber</a>@await Html.PartialAsync("_CopyIdButton", row.Identifier)</span>
                            </td>
                            <td data-label="Model" title="@row.FamilyName family">@row.ModelName</td>
                            <td data-label="Batch">@row.BatchLabel</td>
                            @if (Model.ShowClusters)
                            {
                                <td data-label="Cluster">
                                    @if (row.ClusterId is null)
                                    {
                                        <span class="bp-batteries-muted">No cluster yet</span>
                                    }
                                    else
                                    {
                                        @row.ClusterLabel
                                    }
                                </td>
                            }
                            <td data-label="Status">
                                <div class="bp-badges is-compact">
                                    @if (Model.ShowsBatteryStatus(row) && StatusBadge.Battery(row.BatteryStatus) is { } battery)
                                    {
                                        @await Html.PartialAsync("_StatusBadge", battery)
                                    }
                                    @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(row.IsDraft ? null : (row.Active ?? row.Latest)!.DppStatus))
                                </div>
                            </td>
                            <td data-label="Updated" title="@BatteryListView.UpdatedInFull(row)">@(BatteryListView.Updated(row) ?? "—")</td>
                            <td class="bp-action-cell" data-label="Actions">
                                @await Html.PartialAsync("_RowActions", Model.Actions(row))
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</section>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~AdminDenseConsoleLayoutTests | FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~PassportMarkPagesTests | FullyQualifiedName~RegistryBadgesTests | FullyQualifiedName~RegistryListTests | FullyQualifiedName~RegistrySearchWordsTests | FullyQualifiedName~RowActionsTests | FullyQualifiedName~SharedBatteryListTests | FullyQualifiedName~StatusHelpLinkTests | FullyQualifiedName~TeamListRowTests"`
Expected: `Passed!  - Failed:     0, Passed:    66`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs BatteryPassWeb.Tests/Dpp/RegistryBadgesTests.cs BatteryPassWeb.Tests/Dpp/RegistryListTests.cs BatteryPassWeb.Tests/Dpp/RegistrySearchWordsTests.cs BatteryPassWeb.Tests/Dpp/RowActionsTests.cs BatteryPassWeb.Tests/Dpp/SharedBatteryListTests.cs BatteryPassWeb.Tests/Look/StatusHelpLinkTests.cs BatteryPassWeb.Tests/Look/TeamListRowTests.cs web/Dpp/Pages/BatteryListView.cs web/Dpp/Pages/RegistryList.cs web/Views/Batteries/Batteries.cshtml web/Views/Registry/Index.cshtml web/Views/Shared/_BatteryList.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): one shared battery list for the team's Batteries and the Registry" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Every list row on one line at 1280 px

The list's rows fit one line at 1280 px: the statuses side by side, long names ending in "…" with the whole on hover, 32 px icons, narrower cells. A phone keeps its cards.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Look/OneLineListTests.cs`
- Modify: `web/Views/Shared/_BatteryList.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Look/OneLineListTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R7, the owner's item 3 ("can we fit each battery row on the list on one line?") and choice "Copy icon only":
/// at 1280 px every row of the shared list is one line. Its two statuses sit side by side, told apart by their shape (a
/// square battery tag, a round passport pill) and named for screen readers; a long serial number (165 px), model, batch or
/// cluster (180 px) ends in "…" with the whole text on hover; the row icons are 32 px; the cells are a little narrower than
/// other console tables (site.css sets 14 px with !important, unchanged); the last icon's name opens to its left. A phone
/// keeps its cards.
/// </summary>
public sealed class OneLineListTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void TheList_ShouldClipLongTextWithTheWholeOfItOnHover()
    {
        var list = Read("web", "Views", "Shared", "_BatteryList.cshtml");

        Assert.Contains("bp-mobile-card-table bp-one-line-list\">", list, StringComparison.Ordinal);
        Assert.Contains("<a href=\"@Model.OpenHref(row)\" class=\"bp-battery-link bp-one-line-clip is-serial\" title=\"@row.SerialNumber · battery ID @row.Identifier\">@row.SerialNumber</a>", list, StringComparison.Ordinal);
        Assert.Contains("<td data-label=\"Model\"><span class=\"bp-one-line-clip\" title=\"@row.ModelName · @row.FamilyName family\">@row.ModelName</span></td>", list, StringComparison.Ordinal);
        Assert.Contains("<td data-label=\"Batch\"><span class=\"bp-one-line-clip\" title=\"@row.BatchLabel\">@row.BatchLabel</span></td>", list, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-one-line-clip\" title=\"@row.ClusterLabel\">@row.ClusterLabel</span>", list, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStyles_ShouldKeepEachRowOnOneLine_FromTabletWidthUp()
    {
        var css = Read("web", "wwwroot", "css", "bp-product-data.css");
        var oneLine = css[css.IndexOf("/* Every row on one line from tablet width up", StringComparison.Ordinal)..];

        Assert.StartsWith("/* Every row on one line from tablet width up", oneLine, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 768px) {", oneLine, StringComparison.Ordinal);
        Assert.Contains("    padding-inline: 7px !important;", oneLine, StringComparison.Ordinal);
        Assert.Contains("  .bp-console-table.bp-one-line-list td,\n  .bp-console-table.bp-one-line-list td:is([data-label=\"Batch\"], [data-label=\"Cluster\"]) {\n    white-space: nowrap;", oneLine, StringComparison.Ordinal);
        Assert.Contains("  .bp-one-line-list .bp-one-line-clip {\n    display: inline-block;\n    max-width: 180px;", oneLine, StringComparison.Ordinal);
        // Any serial number fits at 1280 px: 165 px (the demo's are about 140) with 32 px icons, still easy to tap.
        Assert.Contains("  .bp-one-line-list .bp-one-line-clip.is-serial {\n    max-width: 165px;", oneLine, StringComparison.Ordinal);
        Assert.Contains("  .bp-one-line-list .bp-report-action {\n    inline-size: 2rem;\n    block-size: 2rem;\n    min-inline-size: 2rem;\n    min-block-size: 2rem;", oneLine, StringComparison.Ordinal);
        Assert.Contains("  .bp-one-line-list .bp-badge-kind {\n    position: absolute;", oneLine, StringComparison.Ordinal); // named for screen readers
        Assert.Contains(".bp-one-line-list .bp-icon-action-row > .bp-report-action:last-child::after {\n  right: 0;\n  left: auto;", oneLine, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~OneLineListTests"`
Expected: `Failed:     2, Passed:     0` (failing: `OneLineListTests.TheList_ShouldClipLongTextWithTheWholeOfItOnHover`, `OneLineListTests.TheStyles_ShouldKeepEachRowOnOneLine_FromTabletWidthUp`).

- [ ] **Step 3: Write the code**

Patch `r7-task4-code.patch` (`web/Views/Shared/_BatteryList.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r7-task4-code.patch`, outside the worktree, and run `git apply ../r7-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Shared/_BatteryList.cshtml b/web/Views/Shared/_BatteryList.cshtml
index a6a49b8..e61ffd4 100644
--- a/web/Views/Shared/_BatteryList.cshtml
+++ b/web/Views/Shared/_BatteryList.cshtml
@@ -1,8 +1,8 @@
 @using BatteryPassWeb.Dpp.Pages
 @model BatteryListView
-@* One battery list for the team's Batteries page and the Registry (redesign R7, the owner's choice 4, "One shared
-   list"): the same search, columns and row icons; cards on a phone. The battery ID is behind the copy icon (copied in
-   full, shown on hover). *@
+@* One battery list for the team's Batteries page and the Registry (redesign R7, the owner's choices 3 and 4): the same
+   search, columns and row icons, every row on one line at 1280 px; cards on a phone. The battery ID is behind the copy
+   icon (copied in full) and on the serial number's hover; long text ends in "…", whole on hover. *@
 <section class="bp-console-toolbar bp-battery-list-tools mt-4">
     <form method="get" action="@Model.Action" class="bp-batteries-search" role="search">
         <label class="visually-hidden" for="battery-search">Search batteries</label>
@@ -30,7 +30,7 @@
                 <text> <a href="@Model.Action">Show all</a></text>
             }</p>
         <div class="bp-table-scroll">
-            <table class="table align-middle bp-console-table bp-batteries-table bp-mobile-card-table">
+            <table class="table align-middle bp-console-table bp-batteries-table bp-mobile-card-table bp-one-line-list">
                 <thead>
                     <tr>
                         <th>Serial number</th>
@@ -50,10 +50,10 @@
                     {
                         <tr>
                             <td data-label="Serial number">
-                                <span class="bp-id-copy-row" title="Battery ID (#6): @row.Identifier"><a href="@Model.OpenHref(row)" class="bp-battery-link">@row.SerialNumber</a>@await Html.PartialAsync("_CopyIdButton", row.Identifier)</span>
+                                <span class="bp-id-copy-row"><a href="@Model.OpenHref(row)" class="bp-battery-link bp-one-line-clip is-serial" title="@row.SerialNumber · battery ID @row.Identifier">@row.SerialNumber</a>@await Html.PartialAsync("_CopyIdButton", row.Identifier)</span>
                             </td>
-                            <td data-label="Model" title="@row.FamilyName family">@row.ModelName</td>
-                            <td data-label="Batch">@row.BatchLabel</td>
+                            <td data-label="Model"><span class="bp-one-line-clip" title="@row.ModelName · @row.FamilyName family">@row.ModelName</span></td>
+                            <td data-label="Batch"><span class="bp-one-line-clip" title="@row.BatchLabel">@row.BatchLabel</span></td>
                             @if (Model.ShowClusters)
                             {
                                 <td data-label="Cluster">
@@ -63,7 +63,7 @@
                                     }
                                     else
                                     {
-                                        @row.ClusterLabel
+                                        <span class="bp-one-line-clip" title="@row.ClusterLabel">@row.ClusterLabel</span>
                                     }
                                 </td>
                             }
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index e8d4a5f..b5496ec 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3643,3 +3643,76 @@ main .bp-field-marker.is-published {
   justify-content: space-between;
   gap: 12px;
 }
+
+/* The serial number with its copy icon. */
+.bp-one-line-list .bp-id-copy-row {
+  display: inline-flex;
+  align-items: center;
+  gap: 4px;
+}
+
+/* Every row on one line from tablet width up (redesign R7, the owner's item 3); a phone keeps its cards. The two statuses
+   side by side, told apart by their shape (a square battery tag, a round passport pill) and named for screen readers; a
+   long serial number, model, batch or cluster name ends in "…" with the whole of it on hover. */
+@media (min-width: 768px) {
+  /* site.css pads every console cell 14 px with !important; this list needs the room. */
+  .bp-console-table.bp-one-line-list :is(th, td) {
+    padding-inline: 7px !important;
+  }
+
+  .bp-one-line-list .bp-icon-action-row {
+    gap: 4px;
+  }
+
+  /* 32 px row icons (37.6 px elsewhere), still well above the 24 px a finger needs. */
+  .bp-one-line-list .bp-report-action {
+    inline-size: 2rem;
+    block-size: 2rem;
+    min-inline-size: 2rem;
+    min-block-size: 2rem;
+  }
+
+  .bp-console-table.bp-one-line-list td,
+  .bp-console-table.bp-one-line-list td:is([data-label="Batch"], [data-label="Cluster"]) {
+    white-space: nowrap;
+  }
+
+  /* A cell's max-width isn't kept by a table's automatic layout, so the clip is on the text itself. */
+  .bp-one-line-list .bp-one-line-clip {
+    display: inline-block;
+    max-width: 180px;
+    overflow: hidden;
+    text-overflow: ellipsis;
+    vertical-align: bottom;
+  }
+
+  /* Any serial number fits at 1280 px: about 17 characters show, the demo's whole, a longer one ends in "…". */
+  .bp-one-line-list .bp-one-line-clip.is-serial {
+    max-width: 165px;
+  }
+
+  .bp-one-line-list .bp-badges {
+    flex-wrap: nowrap;
+    gap: 6px;
+  }
+
+  .bp-one-line-list .bp-badge-kind {
+    position: absolute;
+    width: 1px;
+    height: 1px;
+    overflow: hidden;
+    clip-path: inset(50%);
+    white-space: nowrap;
+  }
+}
+
+/* The last row icon's name opens to its left, so it stays inside the list (centred, it made the list scroll sideways). */
+.bp-one-line-list .bp-icon-action-row > .bp-report-action:last-child::after {
+  right: 0;
+  left: auto;
+  transform: translateY(4px);
+}
+
+.bp-one-line-list .bp-icon-action-row > .bp-report-action:last-child:is(:hover, :focus-visible)::after {
+  transform: translateY(0);
+}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~OneLineListTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/OneLineListTests.cs web/Views/Shared/_BatteryList.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "fix(dpp): every row of the battery list on one line at 1280 px" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: A marked battery's cluster reads it as when it was active

A battery's cluster also counts while its newest passport is marked invalid or for deletion, so its cluster admin and members open its accident page and see its purchaser data, as when it was active. A replaced passport, another cluster and the public stay as they were.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/MarkedBatteryClusterTests.cs`
- Modify: `web/Dpp/Store/DppStore.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/MarkedBatteryClusterTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R7, task 5 (the page review's gap, the owner's "Yes, fix it"): a battery's cluster counted only while its
/// passport was active, so once the passport was marked invalid or for deletion its own cluster lost the accident page
/// ("Not available") and the purchaser data, though the team kept them. The cluster now counts while the battery's newest
/// passport is marked, as live values already do (PassportReader): the owning cluster reads it exactly as when it was
/// active. A replaced passport, another cluster and the public stay as they were.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkedBatteryClusterTests(MongoFixture mongo)
{
    private static readonly Reader NorthAdmin = Reader.With("north-admin", ReaderRole.ClusterAdmin, BatteryTesting.North);
    private static readonly Reader NorthUser = Reader.With("north-user", ReaderRole.ClusterUser, BatteryTesting.North);
    private static readonly Reader SouthAdmin = Reader.With("south-admin", ReaderRole.ClusterAdmin, BatteryTesting.South);

    private async Task<PublicationRig> MarkedAsync(bool forDeletion)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        if (forDeletion)
        {
            await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
            Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(PublicationTesting.Team, rig.PackId));
        }
        else
        {
            Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Demo: wrong figures"));
        }

        return rig;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AMarkedBatterysCluster_ShouldOpenItsAccidentPage_AsWhenItWasActive(bool forDeletion)
    {
        var rig = await MarkedAsync(forDeletion);
        var documents = PageTesting.Documents(rig);

        Assert.NotNull(await documents.AccidentReportsAsync(NorthAdmin, rig.PackId));
        Assert.NotNull(await documents.AccidentReportsAsync(NorthUser, rig.PackId));
        Assert.Equal([BatteryTesting.North], await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId));
    }

    [Fact]
    public async Task AnotherCluster_AndThePublic_ShouldStillNotOpenIt()
    {
        var rig = await MarkedAsync(forDeletion: false);
        var documents = PageTesting.Documents(rig);

        Assert.Null(await documents.AccidentReportsAsync(SouthAdmin, rig.PackId));
        Assert.Null(await documents.AccidentReportsAsync(Reader.Anonymous, rig.PackId)); // nothing new becomes public
    }

    [Fact]
    public async Task AMarkedBatterysDetailPage_ShouldShowItsClusterThePurchaserData_AndThePublicNoMore()
    {
        var rig = await MarkedAsync(forDeletion: false);
        var id = (await rig.Store.GetLatestVersionAsync(rig.PackId))!.DigitalProductPassportId;
        var pages = PassportPagesTests.Pages(rig);

        var member = (await pages.DetailAsync(NorthUser, id))!;
        var anyone = (await pages.SummaryAsync(Reader.Anonymous, id))!;

        Assert.Contains(member.Audiences, section => section.Key == "purchaser");
        Assert.Empty(anyone.Audiences);
    }

    [Fact]
    public async Task AReplacedPassport_ShouldStillNotCountForItsCluster()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        await rig.Store.SetPackClusterAsync(rig.PackId, BatteryTesting.North);

        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId));
        Assert.Null(await PageTesting.Documents(rig).AccidentReportsAsync(NorthAdmin, rig.PackId));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~MarkedBatteryClusterTests"`
Expected: `Failed:     3, Passed:     2` (failing: `MarkedBatteryClusterTests.AMarkedBatterysDetailPage_ShouldShowItsClusterThePurchaserData_AndThePublicNoMore`).

- [ ] **Step 3: Write the code**

Patch `r7-task5-code.patch` (`web/Dpp/Store/DppStore.cs`). Save the block as `../r7-task5-code.patch`, outside the worktree, and run `git apply ../r7-task5-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index a1284a9..65b483e 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -532,8 +532,12 @@ public sealed class DppStore
             return [];
         }
 
+        // The battery's newest passport counts while it is active, and while a mark freezes it (invalid, marked for
+        // deletion): its cluster reads it as when it was active (redesign R7, like PassportReader's live values). An
+        // archived version or a passport a newer one replaced (inactive) doesn't.
         return await Collection<PassportVersion>()
-            .Find(version => packIds.Contains(version.PackId) && version.DppStatus == DppStatuses.Active)
+            .Find(version => packIds.Contains(version.PackId)
+                && (version.DppStatus == DppStatuses.Active || version.DppStatus == DppStatuses.Invalid || version.DppStatus == DppStatuses.MarkedForDeletion))
             .Project(version => version.PackId)
             .ToListAsync(cancellationToken);
     }
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~MarkedBatteryClusterTests"`
Expected: `Passed!  - Failed:     0, Passed:     5`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/MarkedBatteryClusterTests.cs web/Dpp/Store/DppStore.cs
git commit -m "fix(dpp): a marked battery's cluster reads it as when it was active" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2459, Skipped:     0, Total:  2459` (19 more than the 2440 before).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat adc6fff -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

1. **Restart the app with the usual launcher**, then **Clusters → Demo data → Reset demo data**.
2. **As the team, Batteries:** one line per battery; the serial's copy icon; "Updated" says e.g. "v3 · 3 Nov". **Registry:**
   the same list, published batteries only; a serial opens the battery page.
3. **DEMO-C13M-0001 → Versions:** "Passport 1's page (read only)" and "Passport 2's page (read only)"; open passport 1:
   "Passport 1 of 3, an earlier passport, read only: …"; no cluster "Change"; Modules has no forms.
4. **A battery whose last reading is older than 7 days** (one of the owner's own): its detail page's Live values tab
   says "Last week of readings: …" with lines; its battery page's overview says the same above the tiles. A demo battery
   reports now, so its tab still says "The last 7 days" (a demo reset seeds a fresh week).
5. **As market surveillance, Registry:** no Cluster column; a serial opens the detail page.
6. **As demo.north.admin@example.test, DEMO-C7M-0003 (invalid) → its battery page → "Accident page":** the accident
   information page opens (it said "Not available"); as demo.south.admin@example.test there is no pencil, and the
   address alone gives "Not available".

## Next

Step 5 resumes.
