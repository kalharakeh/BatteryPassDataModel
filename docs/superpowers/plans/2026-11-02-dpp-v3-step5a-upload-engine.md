# DPP V3 Step 5a: The Upload Engine, the Upload Log and the API's Upload Endpoint — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The first part of step 5, "Live data and uploads" (design 8; decisions 8, 17 and 28). One file can carry values for many batteries.
- **The native upload format** (design 8.2): `{"format": "dpp-v3-upload/1", "batteries": [...]}`, each battery named by its battery ID (#6) or serial number (#11), with an optional `measuredAt` and its `values` by the schema's property names.
- **The upload engine:** every battery's values go through the one save pipeline (design 6.2), which decides what this sender may change. A battery that can't be changed is refused without blocking the others.
- **The upload log:** each upload that isn't a preview is kept in a new collection, `dpp_uploads` (who, when, page or API, file, format, and what happened to each battery and value), and named in the audit log.
- **The API's upload endpoint:** `POST api/external/v1/uploads`, with `?preview=true` to check without saving. Cluster tokens stay live-values-only (J-10).
- **The step 4 wrap-up's follow-up:** hand-overs compare normalised cluster IDs.

Step 5's other parts follow: 5b the battery system's export (mapping BMS2_ACO) on the same endpoint, 5c the upload page with its preview, 5d status changes and repairs by upload.

**Architecture:**
- **`Dpp/Uploads/UploadFile.cs`:** the file's entries in one shape whatever the format (`UploadEntry`: how the file names the battery, when it was measured, its values, or why the entry can't be used), and the adapter interface `IUploadAdapter`. `NativeUploadFormat` reads the app's own format; 5b adds the export's adapter beside it.
- **`Dpp/Uploads/UploadService.cs`:** `RunAsync(sender, file, channel, fileName, preview)`:
  1. The first adapter that reads the file turns it into entries; a file no adapter reads, or with no batteries or more than 500, is refused as a whole.
  2. Each battery is found as the API's writes find it (`BatteryApi.FindAsync`: battery ID, passport ID or serial number; drafts only for the team; a serial number picks the passport that isn't inactive).
  3. A battery is refused as a whole when it isn't found, belongs to another cluster, is named twice in the file, has a passport that is no longer active, or a `measuredAt` in the future.
  4. Otherwise its values go through `SavePipeline.SaveAsync`, with the source "feed" for a battery system's token and "upload" otherwise. Each value is judged on its own (J-12).
  5. Unless it is a preview, the upload is logged (`UploadRecord` in `dpp_uploads`) and the audit log gets one `dpp.upload` entry, shown as "… uploaded values for 3 batteries".
- **Who may change what is the pipeline's rule, unchanged.** A battery system's token changes live values only; the team also changes a battery's own values where its pages allow it (#11, #17 and #19 while a draft, #13). The battery status and its re-stated values are still refused ("only through the status change") until 5d.
- **`DppApiController.Upload`:** a write token; 200 when a battery had a value saved (or would have, in a preview), 422 when none did, 400 when the file can't be read. Up to 2 MB.
- **`HandOverService`** normalises the stored cluster IDs before comparing them, as `ClusterBatteryPages` does.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 6.2, 8.1 and 8.2, and section 14's step-5 note. Decisions record: decisions 8, 29 and 35. The step 4 wrap-up: `docs/superpowers/notes/2026-11-02-dpp-v3-step4-wrap-up.md`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at dfacf85 (the step 4 wrap-up), plus the commit that adds this plan. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo). Once this plan runs, the app's start (its `EnsureIndexesAsync`) makes the new collection `dpp_uploads` and its index `at` in `battery_pass_v3` the first time the product owner starts it; it stays empty until something is uploaded. The reset tool knows it as new test data.
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. Whole-suite runs also take `--blame-hang --blame-hang-timeout 10m`.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop it (and any `dotnet` and `testhost` it left) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2233`. After this plan: 2255. The suite takes about two to four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../5a-task2-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (2 Nov 2026, through the managing session)

Decision 35 records step 5's answers; this plan's commit adds it. The ones 5a builds on:

1. **Q1: step 5 in four parts**, each ending with a working app: 5a the upload engine, the upload log and the API's upload endpoint (with the hand-over follow-up); 5b the battery system's export; 5c the upload page; 5d status changes and repairs by upload, and the demo example.
2. **Q6: the upload log is a new collection, `dpp_uploads`:** one entry per upload (who, when, page or API, file name, format, and per battery what happened to each value). Previews aren't logged. The reset tool treats it as test data; clearing the demo data removes the demo uploads, which 5d's demo example brings. The single-battery live values call stays audit-only, as today.
3. **Q7: who may send what is the save pipeline's rule.** The team (a global token here, the page in 5c) may also send a battery's own values; a cluster token sends as its battery system, live values only (J-10). Status changes by upload come with 5d, on the page only, by a person: the API refuses them, even from a global token.
4. **Q10: a battery that can't be changed is refused on its own;** the other batteries of the file are saved. A battery the file names twice is refused both times.
5. **Q11: one upload takes up to 500 batteries and 2 MB.**

## Found while prototyping, and what this plan does

- **The live values call already finds batteries the way an upload needs** (`BatteryApi.FindAsync`: battery ID, passport ID or serial number; drafts for the team only; a serial number picks the passport that isn't inactive). The upload uses it, so both calls answer the same way.
- **A passport that is no longer active** (inactive, invalid, marked for deletion) is refused as a whole, with the pipeline's own words ("the passport is invalid; nothing on it changes"), rather than value by value.
- **The upload's audit entry** reads "… uploaded values for 3 batteries", with how many were saved, instead of the log's generic line.
- **The help page's workbench** fills an upload's body with the battery ID typed above it, so "Send an upload of many batteries" works as it is.

## What happens to the old tests

Nothing is removed.
- **`HandOverTests`** gains one test (Task 1).
- **`ResetCollectionsTests`** knows the new collection `dpp_uploads` (Task 2).
- **`DppApiWritingTests`** lists the new endpoint with its rate limit, and its token helper is shared with the new tests (Task 3).
- **`DppApiControllerTests`** builds the controller with the upload service (Task 3).
- **`HelpWorkbenchLayoutTests`** expects the two new workbench calls (Task 3).

## A part to write yourself (optional)

The product owner chose "you write it" before; the plan gives the code. If they want to try one, the natural spot is `UploadService.RunAsync` in Task 2: when a battery is refused as a whole rather than value by value. `ABatteryThatCannotBeChanged_ShouldBeRefusedWithoutBlockingTheOthers` and `ABatteryNamedTwice_ShouldBeRefusedBothTimes` list it.

---

### Task 1: Hand-overs compare cluster IDs the way the pages do

The step 4 wrap-up's follow-up: `HandOverService` compared the battery's stored cluster ID with the sender's clusters as it was stored, unlike `ClusterBatteryPages`. It now normalises both sides (`ClusterRepository.NormalizeClusterId`), so a cluster ID stored with capitals or spaces still belongs to its cluster admins, and the hand-over records the normalised ID.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/HandOverTests.cs`
- Modify: `web/Dpp/Editing/HandOverService.cs`

- [ ] **Step 1: Write the failing tests**

Patch `5a-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/HandOverTests.cs`). Save the block as `../5a-task1-tests.patch`, outside the worktree, and run `git apply ../5a-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/HandOverTests.cs b/BatteryPassWeb.Tests/Dpp/HandOverTests.cs
index 3c72b71..3262b38 100644
--- a/BatteryPassWeb.Tests/Dpp/HandOverTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/HandOverTests.cs
@@ -151,6 +151,20 @@ public sealed class HandOverTests(MongoFixture mongo)
         Assert.Equal((HandOverStates.Cancelled, "team@example.com"), (cancelled.State, cancelled.AnsweredBy));
     }
 
+    [Fact]
+    public async Task AClusterIdStoredUnnormalised_ShouldStillBeTheSendersAndMoveOnAccept()
+    {
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        await rig.Store.SetPackClusterAsync(rig.PackId, $" {BatteryTesting.North.ToUpperInvariant()} ");
+
+        var asked = await Service(rig).AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
+        Assert.Null(asked.Problem);
+        Assert.Equal(BatteryTesting.North, asked.HandOver!.FromClusterId);
+        Assert.Null(await Service(rig).AnswerAsync(SouthAdmin, asked.HandOver.Id, accept: true));
+
+        Assert.Equal(BatteryTesting.South, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
+    }
+
     internal static HandOverService Service(PublicationRig rig) =>
         new(rig.Store, new ClusterRepository(rig.Context), new ApplicationAuditService(rig.Context), rig.Clock);
 
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HandOverTests"`
Expected: `Failed:     1, Passed:    11` (failing: `HandOverTests.AClusterIdStoredUnnormalised_ShouldStillBeTheSendersAndMoveOnAccept`).

- [ ] **Step 3: Write the code**

Patch `5a-task1-code.patch` (`web/Dpp/Editing/HandOverService.cs`). Save the block as `../5a-task1-code.patch`, outside the worktree, and run `git apply ../5a-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Editing/HandOverService.cs b/web/Dpp/Editing/HandOverService.cs
index 8354110..7a9b658 100644
--- a/web/Dpp/Editing/HandOverService.cs
+++ b/web/Dpp/Editing/HandOverService.cs
@@ -50,7 +50,9 @@ public sealed class HandOverService(DppStore store, ClusterRepository clusters,
             return Refused(NoBattery);
         }
 
-        if (sender.Kind != SenderKind.ClusterAdmin || pack.ClusterId is not { } from || !sender.ClusterIds.Contains(from))
+        // Cluster IDs are compared normalised, as ClusterBatteryPages does (the step 4 wrap-up's follow-up).
+        if (sender.Kind != SenderKind.ClusterAdmin || pack.ClusterId is not { } stored || ClusterRepository.NormalizeClusterId(stored) is not { } from
+            || !sender.ClusterIds.Contains(from))
         {
             return Refused(NotYours);
         }
@@ -106,13 +108,14 @@ public sealed class HandOverService(DppStore store, ClusterRepository clusters,
             return NotWaiting;
         }
 
-        if (sender.Kind != SenderKind.ClusterAdmin || !sender.ClusterIds.Contains(handOver.ToClusterId))
+        if (sender.Kind != SenderKind.ClusterAdmin || !sender.ClusterIds.Contains(ClusterRepository.NormalizeClusterId(handOver.ToClusterId)))
         {
             return NotTheReceiver;
         }
 
         var pack = await store.GetAsync<BatteryPack>(handOver.PackId, cancellationToken);
-        if (handOver.State != HandOverStates.Waiting || pack?.ClusterId != handOver.FromClusterId)
+        if (handOver.State != HandOverStates.Waiting || pack?.ClusterId is not { } now
+            || ClusterRepository.NormalizeClusterId(now) != ClusterRepository.NormalizeClusterId(handOver.FromClusterId))
         {
             return NotWaiting;
         }
@@ -147,7 +150,7 @@ public sealed class HandOverService(DppStore store, ClusterRepository clusters,
         }
 
         var mayCancel = sender.Kind == SenderKind.Team
-            || (sender.Kind == SenderKind.ClusterAdmin && sender.ClusterIds.Contains(handOver.FromClusterId));
+            || (sender.Kind == SenderKind.ClusterAdmin && sender.ClusterIds.Contains(ClusterRepository.NormalizeClusterId(handOver.FromClusterId)));
         if (!mayCancel)
         {
             return NotYours;
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HandOverTests"`
Expected: `Passed!  - Failed:     0, Passed:    12`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2234`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/HandOverTests.cs web/Dpp/Editing/HandOverService.cs
git commit -m "fix(dpp): hand-overs compare normalised cluster IDs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The upload engine and the upload log

One file of many batteries in the native format (design 8.2), read by its adapter, each battery found as the API finds it and its values sent through the one save pipeline, which decides what this sender may change. A battery that can't be changed (not found, another cluster's, named twice, a passport no longer active, a measurement time in the future) is refused without blocking the others. Each upload that isn't a preview is logged in the new collection `dpp_uploads` and in the audit log.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs`
- Modify: `tools/V3Reset/ResetCollections.cs`
- Modify: `web/Dpp/Audit/AuditLines.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Store/DppStore.cs`
- Create: `web/Dpp/Uploads/UploadFile.cs`
- Create: `web/Dpp/Uploads/UploadService.cs`

- [ ] **Step 1: Write the failing tests**

Patch `5a-task2-tests.patch` (`BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs`). Save the block as `../5a-task2-tests.patch`, outside the worktree, and run `git apply ../5a-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs b/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
index e05ff1b..d179306 100644
--- a/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
+++ b/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
@@ -16,7 +16,7 @@ public sealed class ResetCollectionsTests
         var named = Directory.GetFiles(web, "*.cs", SearchOption.AllDirectories)
             .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
             .SelectMany(file => Regex.Matches(File.ReadAllText(file), "GetCollection<[^>]*>\\(\"([^\"]+)\"\\)").Select(match => match.Groups[1].Value));
-        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };
+        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };
 
         foreach (var collection in named.Concat(dpp).Distinct())
         {
@@ -27,7 +27,7 @@ public sealed class ResetCollectionsTests
     [Fact]
     public void TheNewModelsCollections_ShouldBeEmptiedAsTestData()
     {
-        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
+        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
         {
             Assert.Equal(CollectionFate.NewTestData, ResetCollections.FateOf(collection));
         }
```

`BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Uploads in the native format (design 8.1 and 8.2, decision 8, step 5a): each battery found as the API finds it, its
/// values through the one save pipeline, a battery that can't be changed refused without blocking the others, and every
/// upload that isn't a preview logged.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class UploadServiceTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthFeed = ChangeSender.Feed("north-feed", BatteryTesting.North);

    [Fact]
    public async Task ATeamUpload_ShouldSaveEachValueThroughThePipelineAndBeLogged()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, PublicationTesting.Team, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 55, "temperatureInformation": 21, "batteryMass": 500, "colour": "red" }""", "2026-10-29T07:00:00Z")));

        Assert.Null(report.Problem);
        var battery = Assert.Single(report.Batteries);
        Assert.Equal((UploadService.Saved, rig.PackId, PassportPagesTests.Identifier(rig)), (battery.Outcome, battery.PackId, battery.BatteryId));
        Assert.Equal([KeyOutcome.Saved, KeyOutcome.Saved, KeyOutcome.Rejected, KeyOutcome.Ignored], battery.Values.Select(value => value.Outcome));
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"];
        Assert.Equal((55, "upload", new DateTime(2026, 10, 29, 7, 0, 0, DateTimeKind.Utc)), (stored.Value!["percentageValue"]!.GetValue<int>(), stored.Source, stored.GeneratedAtUtc));

        var logged = Assert.Single(await rig.Store.ListUploadsAsync(10));
        Assert.Equal(report.UploadId, logged.Id);
        Assert.Equal(("team@example.com", "Team", "page", "batteries.json", NativeUploadFormat.Format), (logged.Who, logged.SentAs, logged.Channel, logged.FileName, logged.Format));
        var loggedBattery = Assert.Single(logged.Batteries);
        Assert.Equal((1, Serial(rig), UploadService.Saved), (loggedBattery.Entry, loggedBattery.NamedAs, loggedBattery.Outcome));
        Assert.Equal(["Saved", "Saved", "Rejected", "Ignored"], loggedBattery.Values.Select(value => value.Outcome));
        Assert.Equal("Upload of 1 battery: 1 saved, 0 with nothing saved, 0 refused", (await LastAsync(rig, UploadService.UploadedEvent))["message"].AsString);
    }

    [Fact]
    public async Task TheAuditLog_ShouldSayWhoUploadedHowManyBatteries()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await Run(rig, PublicationTesting.Team, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 55 }"""), Entry("no-such-battery", """{ "stateOfChargeSoC": 1 }""")));

        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var lines = await new AuditLines(rig.Store, new DocumentStore(rig.Context), AttributeFormsTests.Forms, rig.Context).DescribeAsync(events);

        var line = Assert.Single(lines, line => line.EventType == UploadService.UploadedEvent);
        Assert.Equal("team@example.com uploaded values for 2 batteries", line.Text);
        Assert.Equal("1 saved, 0 with nothing saved, 1 refused", line.Change);
    }

    [Fact]
    public async Task ABatterySystem_ShouldSaveOnlyLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, NorthFeed, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 40, "manufacturingDate": "2026-01", "batteryStatus": "waste" }""")));

        var battery = Assert.Single(report.Batteries);
        Assert.Equal(UploadService.Saved, battery.Outcome);
        Assert.Equal([KeyOutcome.Saved, KeyOutcome.Rejected, KeyOutcome.Rejected], battery.Values.Select(value => value.Outcome));
        Assert.Equal("feed", (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Source);
        Assert.Equal("BatterySystemFeed", Assert.Single(await rig.Store.ListUploadsAsync(10)).SentAs);
    }

    [Fact]
    public async Task TheTeam_ShouldSetABatterysOwnValuesWhileItIsADraft()
    {
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var published = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var onDraft = await Run(draft, PublicationTesting.Team, File(Entry(Serial(draft), """{ "manufacturingDate": "2026-08" }""")));
        var onPublished = await Run(published, PublicationTesting.Team, File(Entry(Serial(published), """{ "manufacturingDate": "2026-08" }""")));

        Assert.Equal(UploadService.Saved, Assert.Single(onDraft.Batteries).Outcome);
        Assert.Equal("2026-08", (await draft.Store.GetAsync<BatteryPack>(draft.PackId))!.Values["manufacturingDate"].Value!.GetValue<string>());
        var refused = Assert.Single(onPublished.Batteries);
        Assert.Equal(UploadService.NothingSaved, refused.Outcome);
        Assert.Equal(SavePipeline.FixedOncePublished, Assert.Single(refused.Values).Message);
    }

    [Fact]
    public async Task ABatteryThatCannotBeChanged_ShouldBeRefusedWithoutBlockingTheOthers()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var southAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

        var report = await Run(rig, PublicationTesting.Team, File(
            Entry("no-such-battery", """{ "stateOfChargeSoC": 10 }"""),
            """{ "batterySerialNumber": "x" }""",
            Entry(Serial(rig), """{ "stateOfChargeSoC": 11 }""", "2026-10-29T09:00:00Z"),
            """{ "values": { "stateOfChargeSoC": 12 } }""",
            Entry(PassportPagesTests.Identifier(rig), """{ "stateOfChargeSoC": 13 }""")));
        var notYours = await Run(rig, southAdmin, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 14 }""")));

        Assert.Equal(
            [UploadService.NotFound, "the entry has no values (\"values\": {...})", UploadService.InTheFuture, "name the battery by uniqueProductIdentifier or batterySerialNumber", null],
            report.Batteries.Select(battery => battery.Problem));
        Assert.Equal([1, 2, 3, 4, 5], report.Batteries.Select(battery => battery.Entry));
        Assert.Equal(UploadService.Saved, report.Batteries[^1].Outcome);
        Assert.Equal(UploadService.NotYours, Assert.Single(notYours.Batteries).Problem);
        Assert.Equal(13, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task ABatteryNamedTwice_ShouldBeRefusedBothTimes()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, PublicationTesting.Team, File(
            Entry(Serial(rig), """{ "stateOfChargeSoC": 20 }"""),
            Entry(PassportPagesTests.Identifier(rig), """{ "stateOfChargeSoC": 21 }""")));

        Assert.All(report.Batteries, battery => Assert.Equal((UploadService.Refused, UploadService.NamedTwice), (battery.Outcome, battery.Problem)));
        Assert.False(report.AnySaved);
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task APassportThatIsNoLongerActive_ShouldRefuseTheUpload()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures"));

        var report = await Run(rig, PublicationTesting.Team, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 30 }""")));

        var battery = Assert.Single(report.Batteries);
        Assert.Equal((UploadService.Refused, SavePipeline.NoLongerChanges(DppStatuses.Invalid)), (battery.Outcome, battery.Problem));
    }

    [Fact]
    public async Task APreview_ShouldSaveAndLogNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, NorthFeed, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 12 }""")), preview: true);

        Assert.True(report.Preview);
        Assert.True(report.AnySaved);
        Assert.Null(report.UploadId);
        Assert.Empty(await rig.Store.ListUploadsAsync(10));
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("[]", UploadService.NotAnUpload)]
    [InlineData("""{ "batteries": [] }""", UploadService.NotAnUpload)]
    [InlineData("""{ "format": "dpp-v9" }""", "the format \"dpp-v9\" isn't one this app reads; use \"dpp-v3-upload/1\"")]
    [InlineData("""{ "format": 5 }""", "the format 5 isn't one this app reads; use \"dpp-v3-upload/1\"")]
    [InlineData("""{ "format": "dpp-v3-upload/1", "batteries": [] }""", "the file lists no batteries (\"batteries\": [...])")]
    public async Task AFileThatIsNotAnUpload_ShouldSayWhyAndLogNothing(string file, string problem)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, PublicationTesting.Team, file);

        Assert.Equal(problem, report.Problem);
        Assert.Empty(report.Batteries);
        Assert.Empty(await rig.Store.ListUploadsAsync(10));
    }

    [Fact]
    public async Task TooManyBatteries_ShouldBeRefusedAsAWhole()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var entries = Enumerable.Range(0, UploadService.MaxBatteries + 1).Select(index => Entry($"B-{index}", """{ "stateOfChargeSoC": 1 }""")).ToArray();

        var report = await Run(rig, PublicationTesting.Team, File(entries));

        Assert.Equal($"the file lists {UploadService.MaxBatteries + 1} batteries; one upload takes at most {UploadService.MaxBatteries}", report.Problem);
    }

    [Fact]
    public void AFilesBytes_ShouldBeReadWithOrWithoutAByteOrderMark()
    {
        var json = Encoding.UTF8.GetBytes("""{ "format": "dpp-v3-upload/1" }""");

        Assert.Equal(NativeUploadFormat.Format, UploadService.Parse([0xEF, 0xBB, 0xBF, .. json])!["format"]!.GetValue<string>());
        Assert.Equal(NativeUploadFormat.Format, UploadService.Parse(json)!["format"]!.GetValue<string>());
        Assert.Null(UploadService.Parse("not json"u8));
    }

    internal static UploadService Service(PublicationRig rig) =>
        new(rig.Store, rig.Pipeline, BatteryApiTests.Api(rig), new ApplicationAuditService(rig.Context), rig.Clock, [new NativeUploadFormat()]);

    internal static Task<UploadReport> Run(PublicationRig rig, ChangeSender sender, string file, bool preview = false) =>
        Service(rig).RunAsync(sender, JsonNode.Parse(file), "page", "batteries.json", preview);

    internal static string File(params string[] entries) => $$"""{ "format": "dpp-v3-upload/1", "batteries": [ {{string.Join(", ", entries)}} ] }""";

    internal static string Entry(string serialOrId, string values, string? measuredAt = null) =>
        $$"""{ "batterySerialNumber": "{{serialOrId}}", {{(measuredAt is null ? string.Empty : $"\"measuredAt\": \"{measuredAt}\", ")}}"values": {{values}} }""";

    internal static string Serial(PublicationRig rig) => rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value!.GetValue<string>();

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~UploadServiceTests | FullyQualifiedName~ResetCollectionsTests"`
Expected: the build fails with CS0103, CS0234, CS0246 (for example `error CS0103 The name 'UploadService' does not exist in the current context`).

- [ ] **Step 3: Write the code**

Patch `5a-task2-code.patch` (`tools/V3Reset/ResetCollections.cs`, `web/Dpp/Audit/AuditLines.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Store/DppStore.cs`). Save the block as `../5a-task2-code.patch`, outside the worktree, and run `git apply ../5a-task2-code.patch` from the worktree root:

```diff
diff --git a/tools/V3Reset/ResetCollections.cs b/tools/V3Reset/ResetCollections.cs
index 88f36e3..f3deedf 100644
--- a/tools/V3Reset/ResetCollections.cs
+++ b/tools/V3Reset/ResetCollections.cs
@@ -41,6 +41,7 @@ public static class ResetCollections
         ["dpp_packs"] = CollectionFate.NewTestData,
         ["dpp_passport_versions"] = CollectionFate.NewTestData,
         ["dpp_transfers"] = CollectionFate.NewTestData,
+        ["dpp_uploads"] = CollectionFate.NewTestData,
         ["dpp_documents"] = CollectionFate.NewTestData,
         ["dpp_files.files"] = CollectionFate.NewTestData,
         ["dpp_files.chunks"] = CollectionFate.NewTestData,
diff --git a/web/Dpp/Audit/AuditLines.cs b/web/Dpp/Audit/AuditLines.cs
index 56063d4..27f6669 100644
--- a/web/Dpp/Audit/AuditLines.cs
+++ b/web/Dpp/Audit/AuditLines.cs
@@ -7,6 +7,7 @@ using BatteryPassWeb.Dpp.Forms;
 using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Publication;
 using BatteryPassWeb.Dpp.Store;
+using BatteryPassWeb.Dpp.Uploads;
 using BatteryPassWeb.Services;
 using MongoDB.Bson;
 using MongoDB.Bson.IO;
@@ -214,6 +215,13 @@ public sealed class AuditLines(DppStore store, DocumentStore documents, Attribut
                 words.Add(new(" created battery "));
                 words.Add(new(record?.Name ?? entityId, Strong: true));
                 break;
+            case UploadService.UploadedEvent:
+                icon = AuditIcon.Upload;
+                var counts = message.Split(": ", 2);
+                words.Add(new(" uploaded values for "));
+                words.Add(new(counts[0].Replace("Upload of ", string.Empty, StringComparison.Ordinal), Strong: true));
+                change = counts.Length == 2 ? counts[1] : null;
+                break;
             case PublicationService.PublishedEvent:
                 icon = AuditIcon.Publish;
                 words.Add(new(" published battery "));
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index b9fac9d..3d9cd89 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -10,6 +10,7 @@ using BatteryPassWeb.Dpp.Publication;
 using BatteryPassWeb.Dpp.Schemas;
 using BatteryPassWeb.Dpp.Signing;
 using BatteryPassWeb.Dpp.Store;
+using BatteryPassWeb.Dpp.Uploads;
 using BatteryPassWeb.Dpp.Values;
 using Microsoft.Extensions.DependencyInjection.Extensions;
 
@@ -54,6 +55,8 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<ReaderPages>();
         services.AddSingleton<PassportApi>();
         services.AddSingleton<BatteryApi>();
+        services.AddSingleton<IUploadAdapter, NativeUploadFormat>();
+        services.AddSingleton<UploadService>();
         services.AddSingleton<PassportPages>();
         services.AddSingleton<AuditLines>();
         return services;
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index 88f9dea..ca2bd2f 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -221,6 +221,73 @@ public sealed class HandOver : IDppRecord
     public DateTime? AnsweredAtUtc { get; set; }
 }
 
+/// <summary>
+/// One upload (design 6.2 step 8 and 8.1, step 5a): a file of many batteries sent on the upload page or to the API's
+/// upload endpoint, with what happened to each battery and each value. App data, not passport data. Previews aren't
+/// logged: they change nothing.
+/// </summary>
+public sealed class UploadRecord : IDppRecord
+{
+    [BsonId]
+    public required string Id { get; init; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public required DateTime AtUtc { get; init; }
+
+    /// <summary>The person's e-mail or the token's name.</summary>
+    public required string Who { get; init; }
+
+    /// <summary>The sender's kind (<c>SenderKind</c>): Team, ClusterAdmin or BatterySystemFeed.</summary>
+    public required string SentAs { get; init; }
+
+    /// <summary>The clusters a cluster admin or a battery system's token sent for; empty for the team.</summary>
+    public List<string> ClusterIds { get; init; } = [];
+
+    /// <summary>"page" or "api".</summary>
+    public required string Channel { get; init; }
+
+    public string? FileName { get; init; }
+
+    /// <summary>The file's format, e.g. "dpp-v3-upload/1".</summary>
+    public required string Format { get; init; }
+
+    public List<UploadedBattery> Batteries { get; init; } = [];
+
+    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
+    public bool IsDemo { get; set; }
+}
+
+/// <summary>What happened to one battery of an upload: found or not, refused or not, and each value's outcome.</summary>
+public sealed class UploadedBattery
+{
+    /// <summary>The battery's place in the file, from 1.</summary>
+    public int Entry { get; init; }
+
+    /// <summary>The battery ID or serial number the file names it by.</summary>
+    public required string NamedAs { get; init; }
+
+    /// <summary>The battery found; null when none was.</summary>
+    public string? PackId { get; init; }
+
+    /// <summary>"saved", "nothing saved" or "refused".</summary>
+    public required string Outcome { get; init; }
+
+    /// <summary>Why the battery was refused as a whole; null when it wasn't.</summary>
+    public string? Problem { get; init; }
+
+    public List<UploadedValue> Values { get; init; } = [];
+}
+
+/// <summary>One value of an upload: its key, its outcome ("Saved", "KeptEmpty", "Ignored", "Rejected") and why.</summary>
+public sealed class UploadedValue
+{
+    public required string Key { get; init; }
+
+    public required string Outcome { get; init; }
+
+    public string? Message { get; init; }
+}
+
 /// <summary>One repair of a pack (design 7.9): when, what was repaired, and who recorded it when.</summary>
 public sealed class RepairEntry
 {
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index 35359be..d1ba179 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -21,6 +21,7 @@ public sealed class DppStore
     public const string Packs = "dpp_packs";
     public const string Versions = "dpp_passport_versions";
     public const string HandOvers = "dpp_transfers";
+    public const string Uploads = "dpp_uploads";
 
     private const string UniqueProductIdentifierField = "Values.uniqueProductIdentifier.Value";
     private const string SerialNumberField = "Values.batterySerialNumber.Value";
@@ -35,6 +36,7 @@ public sealed class DppStore
         [typeof(BatteryPack)] = Packs,
         [typeof(PassportVersion)] = Versions,
         [typeof(HandOver)] = HandOvers,
+        [typeof(UploadRecord)] = Uploads,
     };
 
     private readonly IMongoDatabase? _database;
@@ -104,6 +106,30 @@ public sealed class DppStore
                 Builders<HandOver>.IndexKeys.Ascending(handOver => handOver.ToClusterId).Ascending(handOver => handOver.State),
                 new CreateIndexOptions { Name = "to_state" }),
         ], cancellationToken);
+        await Collection<UploadRecord>().Indexes.CreateOneAsync(
+            new CreateIndexModel<UploadRecord>(Builders<UploadRecord>.IndexKeys.Descending(upload => upload.AtUtc), new CreateIndexOptions { Name = "at" }),
+            cancellationToken: cancellationToken);
+    }
+
+    /// <summary>Logs an upload (step 5a).</summary>
+    public async Task AddUploadAsync(UploadRecord upload, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(upload);
+        if (_database is not null)
+        {
+            await Collection<UploadRecord>().InsertOneAsync(upload, cancellationToken: cancellationToken);
+        }
+    }
+
+    /// <summary>The latest uploads, newest first.</summary>
+    public async Task<IReadOnlyList<UploadRecord>> ListUploadsAsync(int limit, CancellationToken cancellationToken = default)
+    {
+        if (_database is null)
+        {
+            return [];
+        }
+
+        return await Collection<UploadRecord>().Find(Builders<UploadRecord>.Filter.Empty).SortByDescending(upload => upload.AtUtc).Limit(limit).ToListAsync(cancellationToken);
     }
 
     public async Task<T?> GetAsync<T>(string id, CancellationToken cancellationToken = default)
```

`web/Dpp/Uploads/UploadFile.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>
/// One battery's entry in an upload, whatever the file's format: how the file names the battery, when its values were
/// measured, and the values by the schema's property names (design 8.2).
/// </summary>
/// <param name="Number">The entry's place in the file, from 1.</param>
/// <param name="NamedAs">The battery ID (#6) or serial number (#11) the file names the battery by.</param>
/// <param name="MeasuredAtUtc">When every value of the entry was generated; the upload time when null.</param>
/// <param name="Problem">Why the entry can't be used as it is; null when it can.</param>
public sealed record UploadEntry(
    int Number,
    string NamedAs,
    DateTime? MeasuredAtUtc,
    IReadOnlyDictionary<string, JsonNode?> Values,
    string? Problem = null);

/// <summary>A file read by its format's adapter: the format, its entries, or why it can't be read at all.</summary>
public sealed record UploadDocument(string? Format, IReadOnlyList<UploadEntry> Entries, string? Problem = null)
{
    public static UploadDocument Unreadable(string problem, string? format = null) => new(format, [], problem);
}

/// <summary>
/// Turns one file format into upload entries (design 8.1): the native format here, the battery system's export in
/// step 5b. Every entry then goes through the same save pipeline.
/// </summary>
public interface IUploadAdapter
{
    /// <summary>True when the file is in this adapter's format.</summary>
    bool Reads(JsonObject file);

    UploadDocument Read(JsonObject file);
}

/// <summary>
/// The app's own upload format (design 8.2): <c>{"format": "dpp-v3-upload/1", "batteries": [...]}</c>, each battery named
/// by <c>uniqueProductIdentifier</c> or <c>batterySerialNumber</c>, with an optional <c>measuredAt</c> and its
/// <c>values</c> by the schema's property names.
/// </summary>
public sealed class NativeUploadFormat : IUploadAdapter
{
    public const string Format = "dpp-v3-upload/1";

    public bool Reads(JsonObject file) => file["format"] is JsonValue;

    public UploadDocument Read(JsonObject file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file["format"] is not JsonValue value || !value.TryGetValue<string>(out var format) || format != Format)
        {
            return UploadDocument.Unreadable($"the format {file["format"]?.ToJsonString()} isn't one this app reads; use \"{Format}\"");
        }

        if (file["batteries"] is not JsonArray batteries || batteries.Count == 0)
        {
            return UploadDocument.Unreadable("the file lists no batteries (\"batteries\": [...])", Format);
        }

        if (batteries.Count > UploadService.MaxBatteries)
        {
            return UploadDocument.Unreadable($"the file lists {batteries.Count} batteries; one upload takes at most {UploadService.MaxBatteries}", Format);
        }

        return new UploadDocument(Format, batteries.Select((battery, index) => Entry(index + 1, battery)).ToArray());
    }

    private static UploadEntry Entry(int number, JsonNode? battery)
    {
        if (battery is not JsonObject entry)
        {
            return Broken(number, string.Empty, "the entry isn't a JSON object");
        }

        var namedAs = Text(entry["uniqueProductIdentifier"]) ?? Text(entry["batterySerialNumber"]);
        if (namedAs is null)
        {
            return Broken(number, string.Empty, "name the battery by uniqueProductIdentifier or batterySerialNumber");
        }

        DateTime? measuredAt = null;
        if (entry["measuredAt"] is { } moment)
        {
            if (Text(moment) is not { } stamp
                || !DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return Broken(number, namedAs, "measuredAt isn't a time, such as 2026-10-27T10:02:59Z");
            }

            measuredAt = parsed.UtcDateTime;
        }

        if (entry["values"] is not JsonObject values || values.Count == 0)
        {
            return Broken(number, namedAs, "the entry has no values (\"values\": {...})");
        }

        return new UploadEntry(
            number,
            namedAs,
            measuredAt,
            values.ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal));
    }

    private static UploadEntry Broken(int number, string namedAs, string problem) =>
        new(number, namedAs, null, new Dictionary<string, JsonNode?>(), problem);

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Trim() is { Length: > 0 } text ? text : null;
}
```

`web/Dpp/Uploads/UploadService.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>What happened to one battery of an upload.</summary>
/// <param name="Outcome">One of <see cref="UploadService.Saved"/>, <see cref="UploadService.NothingSaved"/> or <see cref="UploadService.Refused"/>.</param>
/// <param name="Problem">Why the battery was refused as a whole; null when it wasn't.</param>
public sealed record UploadedBatteryReport(
    int Entry,
    string NamedAs,
    string? BatteryId,
    string? PackId,
    string Outcome,
    string? Problem,
    IReadOnlyList<KeyResult> Values);

/// <summary>An upload's result: per battery what happened, or why the file couldn't be read at all.</summary>
/// <param name="UploadId">The log entry; null for a preview or an unreadable file, which aren't logged.</param>
public sealed record UploadReport(string? Format, bool Preview, IReadOnlyList<UploadedBatteryReport> Batteries, string? Problem, string? UploadId)
{
    /// <summary>True when at least one battery had a value saved (or, in a preview, would have).</summary>
    public bool AnySaved => Batteries.Any(battery => battery.Outcome == UploadService.Saved);
}

/// <summary>
/// Uploads (design 8.1 and 8.2, decision 8, step 5a): a file of many batteries, read by its format's adapter, each
/// battery found as the API finds it and its values sent through the one save pipeline (design 6.2), which decides what
/// this sender may change. A battery's refused value doesn't block its other values (J-12); a battery that can't be
/// changed doesn't block the others. Each upload that isn't a preview is logged (<see cref="UploadRecord"/>).
/// </summary>
public sealed class UploadService(
    DppStore store,
    SavePipeline pipeline,
    BatteryApi batteries,
    ApplicationAuditService audit,
    TimeProvider clock,
    IEnumerable<IUploadAdapter> adapters)
{
    public const int MaxBatteries = 500;
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string UploadedEvent = "dpp.upload";

    public const string Saved = "saved";
    public const string NothingSaved = "nothing saved";
    public const string Refused = "refused";

    public const string NotAnUpload = "the file isn't an upload this app reads: give \"format\": \"dpp-v3-upload/1\" and the batteries";
    public const string NotFound = "no battery you may change has this ID";
    public const string NotYours = "this battery belongs to another cluster";
    public const string Ambiguous = "more than one battery has this serial number; use the battery ID";
    public const string NamedTwice = "the file names this battery more than once";
    public const string InTheFuture = "measuredAt lies in the future";

    private readonly IUploadAdapter[] _adapters = adapters.ToArray();

    /// <summary>Reads a file's bytes as JSON (a leading byte order mark is fine); null when it isn't JSON.</summary>
    public static JsonNode? Parse(ReadOnlySpan<byte> file)
    {
        try
        {
            return JsonNode.Parse(file.StartsWith("﻿"u8) ? file[3..] : file);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <param name="channel">"page" or "api".</param>
    public async Task<UploadReport> RunAsync(ChangeSender sender, JsonNode? file, string channel, string? fileName, bool preview, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (file is not JsonObject root)
        {
            return new UploadReport(null, preview, [], NotAnUpload, null);
        }

        var document = _adapters.FirstOrDefault(adapter => adapter.Reads(root))?.Read(root) ?? UploadDocument.Unreadable(NotAnUpload);
        if (document.Problem is not null)
        {
            return new UploadReport(document.Format, preview, [], document.Problem, null);
        }

        // Find every battery first, so a battery the file names twice is refused both times.
        var found = new List<(UploadEntry Entry, WriteTarget? Target, string? Problem)>();
        foreach (var entry in document.Entries)
        {
            found.Add(await FindAsync(sender, entry, cancellationToken));
        }

        var twice = found.Where(item => item.Target?.Pack is not null)
            .GroupBy(item => item.Target!.Pack!.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var reports = new List<UploadedBatteryReport>();
        foreach (var (entry, target, problem) in found)
        {
            var pack = target?.Pack;
            if (problem is not null || pack is null || twice.Contains(pack.Id))
            {
                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target?.Identifier, pack?.Id, Refused, problem ?? NamedTwice, []));
                continue;
            }

            if (await store.PassportStatusAsync(pack.Id, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
            {
                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, SavePipeline.NoLongerChanges(status), []));
                continue;
            }

            var source = sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload";
            var result = await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, entry.Values, source, entry.MeasuredAtUtc), preview, cancellationToken);
            var outcome = result.Keys.Any(key => key.Outcome == KeyOutcome.Saved) ? Saved : NothingSaved;
            reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, result.Keys));
        }

        string? uploadId = null;
        if (!preview)
        {
            uploadId = await LogAsync(sender, channel, fileName, document.Format!, reports, cancellationToken);
        }

        return new UploadReport(document.Format, preview, reports, null, uploadId);
    }

    private async Task<(UploadEntry Entry, WriteTarget? Target, string? Problem)> FindAsync(ChangeSender sender, UploadEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Problem is not null)
        {
            return (entry, null, entry.Problem);
        }

        if (entry.MeasuredAtUtc is { } measuredAt && batteries.InTheFuture(measuredAt))
        {
            return (entry, null, InTheFuture);
        }

        var target = await batteries.FindAsync(sender, entry.NamedAs, cancellationToken);
        return target.Refusal switch
        {
            WriteRefusal.None => (entry, target, null),
            WriteRefusal.NotYours => (entry, null, NotYours),
            WriteRefusal.Ambiguous => (entry, null, Ambiguous),
            _ => (entry, null, NotFound),
        };
    }

    private async Task<string> LogAsync(ChangeSender sender, string channel, string? fileName, string format, IReadOnlyList<UploadedBatteryReport> reports, CancellationToken cancellationToken)
    {
        var upload = new UploadRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            AtUtc = clock.GetUtcNow().UtcDateTime,
            Who = sender.Who,
            SentAs = sender.Kind.ToString(),
            ClusterIds = sender.ClusterIds.Order(StringComparer.Ordinal).ToList(),
            Channel = channel,
            FileName = fileName,
            Format = format,
            Batteries = reports.Select(report => new UploadedBattery
            {
                Entry = report.Entry,
                NamedAs = report.NamedAs,
                PackId = report.PackId,
                Outcome = report.Outcome,
                Problem = report.Problem,
                Values = report.Values.Select(value => new UploadedValue
                {
                    Key = value.Key,
                    Outcome = value.Outcome.ToString(),
                    Message = string.IsNullOrEmpty(value.Message) ? null : value.Message,
                }).ToList(),
            }).ToList(),
        };
        await store.AddUploadAsync(upload, cancellationToken);

        int Count(string outcome) => reports.Count(report => report.Outcome == outcome);
        await audit.AppendApplicationAuditEventAsync(
            UploadedEvent,
            sender.Who,
            sender.Kind.ToString(),
            sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload",
            $"Upload of {reports.Count} {(reports.Count == 1 ? "battery" : "batteries")}: {Count(Saved)} saved, {Count(NothingSaved)} with nothing saved, {Count(Refused)} refused",
            new BsonDocument { ["upload"] = upload.Id, ["format"] = format, ["channel"] = channel },
            entityType: "dpp-upload",
            entityId: upload.Id,
            cancellationToken: cancellationToken);
        return upload.Id;
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~UploadServiceTests | FullyQualifiedName~ResetCollectionsTests"`
Expected: `Passed!  - Failed:     0, Passed:    28`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2249`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs tools/V3Reset/ResetCollections.cs web/Dpp/Audit/AuditLines.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs web/Dpp/Uploads/UploadFile.cs web/Dpp/Uploads/UploadService.cs
git commit -m "feat(dpp): the upload engine and the upload log" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The API's upload endpoint and its help

`POST api/external/v1/uploads` takes the upload file as its body, with `?preview=true` to check without saving. A cluster token sends as its battery system, so only live values are saved (J-10); a global token sends as the team. The help page's API reference and workbench describe it.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/DppApiUploadTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs`
- Test (modify): `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`
- Modify: `web/Controllers/DppApiController.cs`
- Modify: `web/Views/Help/Index.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `5a-task3-tests.patch` (`BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs`, `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`). Save the block as `../5a-task3-tests.patch`, outside the worktree, and run `git apply ../5a-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs b/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs
index 449c251..6262462 100644
--- a/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs
@@ -232,7 +232,7 @@ public sealed class DppApiControllerTests(MongoFixture mongo)
             http.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
         }
 
-        return new DppApiController(Repository(rig), new ClusterRepository(rig.Context), PassportApiTests.Api(rig), BatteryApiTests.Api(rig))
+        return new DppApiController(Repository(rig), new ClusterRepository(rig.Context), PassportApiTests.Api(rig), BatteryApiTests.Api(rig), UploadServiceTests.Service(rig))
         {
             ControllerContext = new ControllerContext { HttpContext = http },
         };
diff --git a/BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs b/BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs
index baf3e90..97c469a 100644
--- a/BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs
@@ -30,6 +30,7 @@ public sealed class DppApiWritingTests(MongoFixture mongo)
                 ["batteries"] = SecurityRateLimitPolicyNames.ExternalApiWrite,
                 ["batteries/{id}/live-values"] = SecurityRateLimitPolicyNames.ExternalApiWrite,
                 ["batteries/{id}/publish"] = SecurityRateLimitPolicyNames.ExternalApiLifecycle,
+                ["uploads"] = SecurityRateLimitPolicyNames.ExternalApiWrite,
             },
             posts);
         Assert.Null(type.Assembly.GetType("BatteryPassWeb.Controllers.ExternalApiController"));
@@ -226,7 +227,7 @@ public sealed class DppApiWritingTests(MongoFixture mongo)
     }
 
     /// <summary>Tokens of each access mode for the seed pack's cluster (north), another cluster (south) and the team (global).</summary>
-    private static async Task<Dictionary<string, string>> TokensAsync(PublicationRig rig)
+    internal static async Task<Dictionary<string, string>> TokensAsync(PublicationRig rig)
     {
         var repository = DppApiControllerTests.Repository(rig);
         var tokens = new Dictionary<string, string>();
diff --git a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
index d3d7614..26bf38d 100644
--- a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
+++ b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
@@ -184,6 +184,8 @@ public sealed class HelpWorkbenchLayoutTests
             "writeLiveValues",
             "previewLiveValues",
             "readLiveHistory24h",
+            "sendUpload",
+            "previewUpload",
             "listBatches",
             "listOperators",
             "createBattery",
```

`BatteryPassWeb.Tests/Dpp/DppApiUploadTests.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The external API's upload endpoint (design 8.1, step 5a), called with real tokens of each kind.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppApiUploadTests(MongoFixture mongo)
{
    [Fact]
    public async Task AClusterToken_ShouldUploadItsBatteriesLiveValuesOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var data = Data(
            await Upload(rig, tokens["north-write"], UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 33, "warrantyPeriod": "P9Y" }"""))),
            StatusCodes.Status200OK);

        Assert.True(data.GetProperty("saved").GetBoolean());
        Assert.Equal(NativeUploadFormat.Format, data.GetProperty("format").GetString());
        var battery = Assert.Single(data.GetProperty("batteries").EnumerateArray());
        Assert.Equal((1, PassportPagesTests.Identifier(rig), "saved"), (battery.GetProperty("entry").GetInt32(), battery.GetProperty("batteryId").GetString(), battery.GetProperty("outcome").GetString()));
        Assert.Equal(["saved", "rejected"], battery.GetProperty("values").EnumerateArray().Select(value => value.GetProperty("outcome").GetString()));
        var logged = Assert.Single(await rig.Store.ListUploadsAsync(10));
        Assert.Equal((data.GetProperty("uploadId").GetString(), "api", "BatterySystemFeed", (string?)null), (logged.Id, logged.Channel, logged.SentAs, logged.FileName));
        Assert.Equal([BatteryTesting.North], logged.ClusterIds);
    }

    [Theory]
    [InlineData("north-read", StatusCodes.Status403Forbidden)]
    [InlineData(null, StatusCodes.Status401Unauthorized)]
    public async Task ATokenWithoutWriteAccess_ShouldUploadNothing(string? token, int status)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await Upload(rig, token is null ? null : tokens[token], UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 33 }"""))));

        Assert.Equal(status, answer.StatusCode);
        Assert.Empty(await rig.Store.ListUploadsAsync(10));
    }

    [Fact]
    public async Task NothingSaved_ShouldBeUnprocessableAndAnUnreadableFileABadRequest()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var refused = Assert.IsType<ObjectResult>(await Upload(rig, tokens["south-write"], UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 33 }"""))));
        var unreadable = Assert.IsType<ObjectResult>(await Upload(rig, tokens["global-write"], """{ "format": "dpp-v9" }"""));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.StatusCode);
        Assert.Equal(UploadService.NotYours, Assert.Single(DppApiControllerTests.Json(refused).GetProperty("data").GetProperty("batteries").EnumerateArray()).GetProperty("problem").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, unreadable.StatusCode);
        Assert.Equal("The file can't be read: the format \"dpp-v9\" isn't one this app reads; use \"dpp-v3-upload/1\".", DppApiControllerTests.Json(unreadable).GetProperty("message").GetString());
    }

    [Fact]
    public async Task APreview_ShouldSaveNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var data = Data(await Upload(rig, tokens["global-write"], UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 33 }""")), preview: true), StatusCodes.Status200OK);

        Assert.True(data.GetProperty("preview").GetBoolean());
        Assert.False(data.GetProperty("saved").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("uploadId").ValueKind);
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public void TheEndpoint_ShouldTakeAtMostTwoMegabytes()
    {
        var limit = typeof(DppApiController).GetMethod(nameof(DppApiController.Upload))!.GetCustomAttribute<RequestSizeLimitAttribute>()!;
        var bytes = typeof(RequestSizeLimitAttribute).GetField("_bytes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(limit);

        Assert.Equal(2L * 1024 * 1024, bytes);
    }

    private static Task<IActionResult> Upload(PublicationRig rig, string? token, string body, bool preview = false) =>
        DppApiControllerTests.Controller(rig, token).Upload(JsonDocument.Parse(body).RootElement.Clone(), preview, CancellationToken.None);

    private static JsonElement Data(IActionResult result, int status)
    {
        var answer = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, answer.StatusCode);
        return DppApiControllerTests.Json(answer).GetProperty("data");
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DppApiUploadTests | FullyQualifiedName~DppApiWritingTests | FullyQualifiedName~DppApiControllerTests | FullyQualifiedName~HelpWorkbenchLayoutTests"`
Expected: the build fails with CS0117, CS1061, CS1729 (for example `error CS0117 'DppApiController' does not contain a definition for 'Upload'`).

- [ ] **Step 3: Write the code**

Patch `5a-task3-code.patch` (`web/Controllers/DppApiController.cs`, `web/Views/Help/Index.cshtml`). Save the block as `../5a-task3-code.patch`, outside the worktree, and run `git apply ../5a-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/DppApiController.cs b/web/Controllers/DppApiController.cs
index 3641ff3..2a2ca37 100644
--- a/web/Controllers/DppApiController.cs
+++ b/web/Controllers/DppApiController.cs
@@ -8,6 +8,7 @@ using BatteryPassWeb.Dpp.Catalog;
 using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Publication;
+using BatteryPassWeb.Dpp.Uploads;
 using BatteryPassWeb.Services;
 using Microsoft.AspNetCore.Http;
 using Microsoft.AspNetCore.Mvc;
@@ -23,7 +24,7 @@ namespace BatteryPassWeb.Controllers;
 /// </summary>
 [ApiController]
 [Route("api/external/v1")]
-public sealed class DppApiController(ExternalApiRepository tokens, ClusterRepository clusters, PassportApi api, BatteryApi writing) : ControllerBase
+public sealed class DppApiController(ExternalApiRepository tokens, ClusterRepository clusters, PassportApi api, BatteryApi writing, UploadService uploads) : ControllerBase
 {
     public const string NoPassport = "No published battery passport has this ID.";
     public const string NoBattery = "No battery this token may change has this ID.";
@@ -335,6 +336,48 @@ public sealed class DppApiController(ExternalApiRepository tokens, ClusterReposi
             });
     }
 
+    /// <summary>
+    /// An upload of many batteries (design 8.1, step 5a): the native format, through the one save pipeline. A cluster
+    /// token sends as its battery system, so only live values are saved (J-10); a global token sends as the team.
+    /// </summary>
+    [HttpPost("uploads")]
+    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)]
+    [RequestSizeLimit(UploadService.MaxBytes)]
+    public async Task<IActionResult> Upload([FromBody] JsonElement body, [FromQuery] bool preview, CancellationToken cancellationToken)
+    {
+        var (token, error) = await TokenAsync(ExternalTokenRequirement.Write, cancellationToken);
+        if (token is null)
+        {
+            return error!;
+        }
+
+        var report = await uploads.RunAsync(DppReaders.SenderFor(token), JsonNode.Parse(body.GetRawText()), "api", null, preview, cancellationToken);
+        if (report.Problem is not null)
+        {
+            return Envelope(StatusCodes.Status400BadRequest, $"The file can't be read: {report.Problem}.");
+        }
+
+        return Envelope(
+            report.AnySaved ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity,
+            !report.AnySaved ? "No value was saved; see each battery." : preview ? "Preview only: nothing was saved." : "Upload saved.",
+            new
+            {
+                uploadId = report.UploadId,
+                format = report.Format,
+                preview,
+                saved = !preview && report.AnySaved,
+                batteries = report.Batteries.Select(battery => new
+                {
+                    entry = battery.Entry,
+                    namedAs = battery.NamedAs,
+                    batteryId = battery.BatteryId,
+                    outcome = battery.Outcome,
+                    problem = battery.Problem,
+                    values = Keys(battery.Values),
+                }).ToArray(),
+            });
+    }
+
     [HttpGet("batches")]
     [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
     public async Task<IActionResult> Batches(CancellationToken cancellationToken)
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
index 0648896..5bb28d2 100644
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -192,6 +192,17 @@
                                 </button>
                             </td>
                         </tr>
+                        <tr class="bp-help-doc-item">
+                            <td><span class="bp-help-method">POST</span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/uploads</code><small>An upload of many batteries</small></span></td>
+                            <td><span class="bp-help-access-pill">Read-write</span></td>
+                            <td>Send one file with values for many batteries; each battery is saved or refused on its own.</td>
+                            <td class="bp-action-cell">
+                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-uploads" aria-expanded="false" aria-controls="api-detail-uploads" aria-label="Show upload endpoint details" title="Show upload endpoint details" data-tooltip="Details">
+                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
+                                </button>
+                            </td>
+                        </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">GET</span></td>
                             <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/passports</code><small>Passport versions</small></span></td>
@@ -340,7 +351,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                         <p>The battery system's call. <code>values</code> holds the live values by key; a bare number is enough, and the app adds the unit. <code>measuredAt</code> is when they were measured (ISO 8601 with its offset); without it, the time of the call is used. A cluster token changes the published batteries of its own clusters; a team token any battery, drafts included. Each value is checked on its own: one refused value doesn't stop the others, and an empty value never overwrites. Add <code>?preview=true</code> to check without saving.</p>
                         <p>The live values:</p>
                         <ul>@foreach (var live in BatteryPassWeb.Dpp.Api.BatteryApi.LiveSet) { <li><code>@live.Property</code> (@live.Label, @live.Name@(live.Unit is null ? "" : $", in {live.Unit}"))</li> }</ul>
-                        <p>Any other attribute is refused here: static values change on the team's pages.</p>
+                        <p>Any other attribute is refused here: static values change on the team's pages or by upload.</p>
                     </div>
                     <pre class="bp-help-pre"><code>POST @Model.BasePath/batteries/{id}/live-values
 {
@@ -358,6 +369,29 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                     <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/{id}/live-values/history?hours=24</code></pre>
                 </article>
 
+                <article id="api-detail-uploads" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
+                    <div>
+                        <span class="bp-summary-title">Uploads</span>
+                        <h3>Send values for many batteries in one file.</h3>
+                        <p>The body is the upload file. Name each battery by <code>uniqueProductIdentifier</code> (its battery ID) or <code>batterySerialNumber</code>; <code>measuredAt</code> is when its values were measured, and <code>values</code> holds them by key, as on the live values call. A cluster token sends as its battery system, so only the live values of its own clusters' published batteries are saved; a team token may also set a battery's own values where the team's pages allow it. Each battery is saved or refused on its own, and each value is checked on its own. A battery the file names twice, or whose passport is no longer active, is refused. Up to @BatteryPassWeb.Dpp.Uploads.UploadService.MaxBatteries batteries and 2 MB per file. Add <code>?preview=true</code> to check without saving.</p>
+                    </div>
+                    <pre class="bp-help-pre"><code>POST @Model.BasePath/uploads
+{
+  "format": "dpp-v3-upload/1",
+  "batteries": [
+    {
+      "batterySerialNumber": "VPC8C05-6788",
+      "measuredAt": "2026-10-27T10:02:59Z",
+      "values": {
+        "stateOfChargeSoC": 76,
+        "numberOfFullChargingAndDischargingCycles": 412,
+        "temperatureInformation": 23
+      }
+    }
+  ]
+}</code></pre>
+                </article>
+
                 <article id="api-detail-passport-list" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
                     <div><span class="bp-summary-title">Passport versions</span><h3>List the battery's published versions.</h3><p>Newest first, each with its passport ID, version number, status (active or archived), when it was published and last updated, and whether its signature still holds.</p></div>
                     <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/{id}/passports</code></pre>
@@ -403,6 +437,8 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                                 <option value="writeLiveValues">Send live values</option>
                                 <option value="previewLiveValues">Check live values without saving</option>
                                 <option value="readLiveHistory24h">Read live value history 24h</option>
+                                <option value="sendUpload">Send an upload of many batteries</option>
+                                <option value="previewUpload">Check an upload without saving</option>
                             </optgroup>
                             <optgroup label="Team token: create and publish">
                                 <option value="listBatches">List batches</option>
@@ -608,6 +644,20 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 }
             }, null, 2);
 
+            const uploadBody = JSON.stringify({
+                format: 'dpp-v3-upload/1',
+                batteries: [
+                    {
+                        uniqueProductIdentifier: '{batteryId}',
+                        measuredAt: new Date().toISOString().replace(/\.\d{3}Z$/, 'Z'),
+                        values: {
+                            stateOfChargeSoC: 76,
+                            temperatureInformation: 23
+                        }
+                    }
+                ]
+            }, null, 2);
+
             const templates = {
                 readFull: {
                     method: 'GET',
@@ -669,6 +719,16 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                     pathTemplate: `${basePath}/batteries/{batteryId}/live-values/history?hours=24`,
                     body: ''
                 },
+                sendUpload: {
+                    method: 'POST',
+                    pathTemplate: `${basePath}/uploads`,
+                    body: uploadBody
+                },
+                previewUpload: {
+                    method: 'POST',
+                    pathTemplate: `${basePath}/uploads?preview=true`,
+                    body: uploadBody
+                },
                 listBatches: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batches`,
@@ -941,7 +1001,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
 
                 methodEl.value = template.method;
                 pathEl.value = resolveTemplatePath(template.pathTemplate, getWorkbenchContext());
-                bodyEl.value = template.body;
+                bodyEl.value = resolveTemplatePath(template.body, getWorkbenchContext());
                 syncRawRequestFromControls();
                 showCallParts(templateKey);
             };
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DppApiUploadTests | FullyQualifiedName~DppApiWritingTests | FullyQualifiedName~DppApiControllerTests | FullyQualifiedName~HelpWorkbenchLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:    57`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2255`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs BatteryPassWeb.Tests/Dpp/DppApiUploadTests.cs BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs web/Controllers/DppApiController.cs web/Views/Help/Index.cshtml
git commit -m "feat(dpp): the API's upload endpoint and its help" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2255, Skipped:     0, Total:  2255`. From the 2233 before: 2234 after Task 1, 2249 after Task 2, 2255 after Task 3.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat dfacf85 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. 5a has no new screen; the upload page comes with 5c. The managing session brings these to the product owner:

1. **Restart the app with the usual launcher.** Its first start makes the empty collection `dpp_uploads`.
2. **The help page** (Help in the top bar, then "API reference and workbench"): the new row "`/uploads` — An upload of many batteries", with its details. In the request workbench, "Send an upload of many batteries" fills in a file for the battery ID typed above it.
3. **Try it in the workbench:** make a read-write token for demo-north on the API tokens tab (its value is shown once), paste it in the workbench, and type the battery ID of DEMO-C7M-0001. "Check an upload without saving" answers per battery and per value, and saves nothing. "Send an upload of many batteries" saves the live values; the battery's page shows them "from the battery system". Delete the token afterwards.
4. **The audit log:** one line per upload, "… uploaded values for 1 battery", with how many were saved, beside the usual change lines.

## Next

5b: the battery system's export (mapping BMS2_ACO) on the same endpoint. Then the mock-up for the whole step, 5c (the upload page) and 5d (status changes and repairs by upload, and the demo example).
