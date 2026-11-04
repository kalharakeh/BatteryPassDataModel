# DPP V3 Step 5d: Status Changes and Repairs by Upload, the Help's Sample Files and the Demo Uploads — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The last part of step 5 (decisions 8 and 35, Q20–Q25):
- **A status change or a repair by upload,** in the app's own format, on the battery's page only ("Upload a file", plan 5c), by the team or the battery's cluster admin. It goes through the status change of the battery's page (4b): the same rules, the same new version, the same line in History and the audit log.
- **The check shows one status line,** e.g. "Repaired on 20 Oct 2026: “Cell module 2 replaced.” The status becomes re-used, with a new version.", and each re-stated value the file leaves out as "Kept: the battery's current value". A refused status change saves nothing of that battery and says why.
- **The API refuses a battery with a status change or a repair:** "status changes are made on the battery's page".
- **Earlier uploads** say "Status changed" with the line, and name a battery system's uploads "Battery system", never by its token's ID.
- **The help page** gets an "Upload a file" part for the team and cluster admins, with two sample files for the demo battery DEMO-C7M-0001 (an upload file with a repair, and a battery system export), downloads for them only.
- **The demo data** gives DEMO-C7M-0001 two uploads (an export from demo-north's battery system and a file from its cluster admin), removed with the demo data.

Shots approved on 4 Nov 2026 ("Approve"): the check with a repair line, a refused status change, the help's two downloads, DEMO-C7M-0001's Earlier uploads after a demo reset, and the check at 375 px.

**Architecture:**
- **`NativeUploadFormat`** (task 1) reads `batteryStatus` (a bare word or the schema's `batteryStatusValues`) and `"reason": "repair"` with `repairDescription` and `repairDate` into `UploadEntry.Status` (`UploadStatus`: what, the repair's day and description), and takes `batteryStatus` out of the entry's values.
- **`UploadService`** (task 1, now given `StatusChangeService`):
  - An entry with a status: on the API, the battery is refused (`StatusOnThePage`).
  - On the page, `StatusAsync` builds the re-stated values (the file's, else the battery's current ones, "kept") and runs `StatusChangeService.ChangeAsync` as a preview for the check, or for real on "Save" (source "upload"). A refusal refuses the battery.
  - Otherwise `StatusWords` makes the line, and the file's other values are saved after it.
  - The log keeps `UploadedBattery.Status` and `UploadedValue.Kept`.
- **`StatusChangeService.ChangeAsync` and `SavePipeline.SaveStatusChangeAsync`** (task 1) take `preview` and `source` (defaults: false, "form"), so the page's status window is unchanged.
- **`UploadPages`** and **`_UploadCard.cshtml`** (task 2) show the status line in the check, after saving and in Earlier uploads ("Status changed"), the kept values in grey, and "Battery system" for a battery system's uploads.
- **`UploadSamples`** (task 3) makes the two sample files and the demo uploads' files. **`HelpController.Sample`** (`GET /help/samples/{name}`, policy `ClusterAdminOrAdmin`) serves them; the guide's new part links them; the API's help says a status change is refused.
- **`DemoDataService`** (task 3) sends the two demo uploads through `UploadService` after the demo tokens (`SeedUploadsAsync`, for the catalogue's batteries with `"uploads": true`), marks them as demo data (`DppStore.SetUploadDemoAsync`), and clears them and the demo batteries' kept checks.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 7.4, 7.9 and 8.2 (amended by this plan's commit) and section 14's step-5 note. Decisions record: decisions 8 and 35. Plans 5a–5c: `docs/superpowers/plans/2026-11-02-dpp-v3-step5a-upload-engine.md`, `docs/superpowers/plans/2026-11-02-dpp-v3-step5b-battery-system-export.md`, `docs/superpowers/plans/2026-11-02-dpp-v3-step5c-upload-card.md`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 86dc2a7 (the end of plan 5c's run: "feat(dpp): the Upload a file section on both battery pages", on 0b16c6f and dfa18a8), plus the commit that adds this plan. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo). No new collection or index. After this plan, the next "Reset demo data" adds DEMO-C7M-0001's two demo uploads to `dpp_uploads` in `battery_pass_v3`.
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Each task runs only its own tests** (the filter in its steps); **the whole suite runs once**, in the final checks. Never run two whole suites at once.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started (by their process IDs, never by name) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2481` (plan 5c's result). After this plan: 2499. The suite takes about four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../5d-task2-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (3 and 4 Nov 2026, through the managing session)

Decision 35 holds step 5's answers; this plan's commit adds 5d's and amends design 8.2. The ones 5d builds on:

1. **Q20: status changes and repairs use the app's own upload format** (design 8.2). `batteryStatus` among the values asks for waste, re-used, remanufactured or repurposed; `"reason": "repair"` with a `repairDescription` (and a `repairDate`, else the day of `measuredAt`) asks for a repair. "original" can't be set by upload, a repair can't also name a status, and "repair" is the only reason an entry gives.
2. **Q21: the check shows one status line,** e.g. "Repaired on 20 Oct 2026: “Cell module 2 replaced.” The status becomes re-used, with a new version.", above the values. A re-stated value the file leaves out is the battery's current one (Q8), shown in grey as "Kept: the battery's current value".
3. **Q22: the status change is saved first, through the status change of the battery's page** (4b: the same rules, the same new version, the same audit line, with the source "upload"), then the file's live values. A refused status change saves nothing of that battery.
4. **Q23: the API refuses a battery with a status change or a repair** ("status changes are made on the battery's page"), even from a global token. This changes 5a: `batteryStatus` in an API file was one refused value and the rest was saved; now the whole battery is refused. The API's help says so.
5. **Q24: the help page's two sample files** (an upload file with live values and a repair, and a battery system export, both for the demo battery DEMO-C7M-0001) are downloads for the signed-in team and cluster admins only, in a new "Upload a file" part of the guide.
6. **Q25: DEMO-C7M-0001 gets two demo uploads** when the demo data is seeded: an export from demo-north's battery system two hours before, and a file of live values from its cluster admin half an hour before. Both are demo data, and clearing the demo data removes them.
7. **Approved with the shots on 4 Nov ("Approve"):** a repair sets the status to re-used, as on the page; the kept values; a refused status change refuses the whole battery; the API's refusal; a battery system's uploads named "Battery system" in Earlier uploads, never by its token's ID; the History and audit line is the page's own ("Battery DEMO-C7M-0001 repaired: … Status re-used.").

## Found while prototyping, and what this plan does

- **The check runs the status change as a preview,** with every rule of the page's status change (who may change it, waste is final, a repair needs a description and a day not in the future), so the check says why before anything is saved.
- **The status change and the file's live values are two saves,** in that order (Q22): the status change makes the new version with the re-stated values; the live values then change it in place. A re-stated value in the file goes with the status change, not twice.
- **The upload log keeps the status line** (`UploadedBattery.Status`) and which re-stated values were kept (`UploadedValue.Kept`), so Earlier uploads shows "Status changed" with the line. Uploads logged before this plan show neither.
- **A repair description ends with one full stop** in the line, whether the sender wrote one or not (found by the walk: "fault.”." before).
- **The demo uploads go through the one upload engine** after the demo tokens (the export with demo-north's read-write token's ID as its battery system), not written by hand. They add about 30 database operations to the seed and 2 to the clearing, roughly 2 to 3 seconds on Atlas.
- **Clearing the demo data removes the kept upload checks of the demo batteries in one delete,** not one per battery (about 0.5 s less on Atlas than the prototype's).
- **Browser checks** (20, on a throwaway app after a demo reset, at 1280 and 375 px, light and dark): the two demo uploads in DEMO-C7M-0001's Earlier uploads (team and cluster admin); the help's two downloads for the team and a cluster admin, not for a cluster user, the repair role or a visitor signed out; the help's upload file checked (the repair line, kept values) and saved (one line, a new version, "Status changed"); the status section, the audit log and the cluster admin's History; a repair without a description refused, without "Save"; no sideways scrolling.

## What happens to the old tests

Nothing is removed.
- **`UploadServiceTests`** builds the upload service with the status change (Task 1). `ABatterySystem_ShouldSaveOnlyLiveValues` no longer sends `batteryStatus`: a battery system's file with a status change is now refused as a whole (Q23), which `StatusUploadTests.TheApi_ShouldRefuseABatteryWithAStatusChange` covers.
- **`DemoSeedTests`** also expects the demo uploads to be marked as demo data, and **`DemoClearTests`** expects the demo uploads and the demo batteries' kept checks to be gone after clearing (Task 3).

## A part to write yourself (optional)

The product owner chose "you write it" before; the plan gives the code. If they want to try one, the natural spot is `UploadService.StatusWords` in Task 1: the status change in words for the check and Earlier uploads. `TheRepairLine_ShouldEndTheDescriptionWithOneFullStop` lists it.

---

### Task 1: Status changes and repairs in the upload format, through the status change

The rules before the page. An entry of the app's format asks for a status change with `batteryStatus` among its values (waste, re-used, remanufactured or repurposed) or for a repair with `"reason": "repair"`, a `repairDescription` and a `repairDate` (else the day of `measuredAt`) (Q20). On the battery's page the upload service runs it through the page's status change, as a preview for the check and for real on "Save" (source "upload"), with the file's re-stated values or the battery's current ones ("kept", Q8); a refusal saves nothing of the battery, and otherwise the file's live values follow (Q22). The API refuses the battery (Q23). The upload log keeps the status line and the kept values.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs`
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Modify: `web/Dpp/Editing/StatusChangeService.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Uploads/UploadFile.cs`
- Modify: `web/Dpp/Uploads/UploadService.cs`

- [ ] **Step 1: Write the failing tests**

Patch `5d-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs`). Save the block as `../5d-task1-tests.patch`, outside the worktree, and run `git apply ../5d-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs b/BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs
index 2db37fc..d7750b4 100644
--- a/BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs
@@ -63,11 +63,11 @@ public sealed class UploadServiceTests(MongoFixture mongo)
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
 
-        var report = await Run(rig, NorthFeed, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 40, "manufacturingDate": "2026-01", "batteryStatus": "waste" }""")));
+        var report = await Run(rig, NorthFeed, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 40, "manufacturingDate": "2026-01" }""")));
 
         var battery = Assert.Single(report.Batteries);
         Assert.Equal(UploadService.Saved, battery.Outcome);
-        Assert.Equal([KeyOutcome.Saved, KeyOutcome.Rejected, KeyOutcome.Rejected], battery.Values.Select(value => value.Outcome));
+        Assert.Equal([KeyOutcome.Saved, KeyOutcome.Rejected], battery.Values.Select(value => value.Outcome));
         Assert.Equal("feed", (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Source);
         Assert.Equal("BatterySystemFeed", Assert.Single(await rig.Store.ListUploadsAsync(10)).SentAs);
     }
@@ -190,7 +190,7 @@ public sealed class UploadServiceTests(MongoFixture mongo)
     }
 
     internal static UploadService Service(PublicationRig rig) =>
-        new(rig.Store, rig.Pipeline, BatteryApiTests.Api(rig), new ApplicationAuditService(rig.Context), rig.Clock, [new NativeUploadFormat(), new BmsExportFormat()]);
+        new(rig.Store, rig.Pipeline, BatteryApiTests.Api(rig), new ApplicationAuditService(rig.Context), rig.Clock, StatusChangeTests.Service(rig), [new NativeUploadFormat(), new BmsExportFormat()]);
 
     internal static Task<UploadReport> Run(PublicationRig rig, ChangeSender sender, string file, bool preview = false) =>
         Service(rig).RunAsync(sender, JsonNode.Parse(file), "page", "batteries.json", preview);
```

`BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Status changes and repairs by upload (design 8.2, decision 35 Q20–Q25; step 5d): read from the app's format, made
/// through the status change on the battery's page only, shown in the check with the kept values and refused by the API.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class StatusUploadTests(MongoFixture mongo)
{
    private static readonly NativeUploadFormat Format = new();

    [Theory]
    [InlineData("""{ "batterySerialNumber": "B", "reason": "repair", "repairDescription": "Fan replaced", "repairDate": "2026-10-30", "values": {} }""", StatusEvent.Repaired, "2026-10-30", null)]
    [InlineData("""{ "batterySerialNumber": "B", "measuredAt": "2026-10-28T09:00:00Z", "reason": "repair", "repairDescription": "Fan replaced" }""", StatusEvent.Repaired, "2026-10-28", null)]
    [InlineData("""{ "batterySerialNumber": "B", "values": { "batteryStatus": "waste" } }""", StatusEvent.Waste, null, null)]
    [InlineData("""{ "batterySerialNumber": "B", "values": { "batteryStatus": { "batteryStatusValues": "re-used" } } }""", StatusEvent.PlacedOnMarketAgain, null, null)]
    [InlineData("""{ "batterySerialNumber": "B", "values": { "batteryStatus": "original" } }""", null, null, "batteryStatus \"original\" can't be set by upload; it takes waste, re-used, remanufactured or repurposed, or a repair")]
    [InlineData("""{ "batterySerialNumber": "B", "reason": "repair", "repairDescription": "x", "values": { "batteryStatus": "waste" } }""", null, null, "a repair sets the battery status itself; leave batteryStatus out")]
    [InlineData("""{ "batterySerialNumber": "B", "reason": "upgrade", "values": { "stateOfChargeSoC": 5 } }""", null, null, "the only reason an entry gives is \"repair\"")]
    public void TheFormat_ShouldReadAStatusChangeOrARepair(string entry, StatusEvent? what, string? day, string? problem)
    {
        var read = Assert.Single(Format.Read(JsonNode.Parse($$"""{ "format": "dpp-v3-upload/1", "batteries": [ {{entry}} ] }""")!.AsObject()).Entries);

        Assert.Equal(problem, read.Problem);
        Assert.Equal(what, read.Status?.What);
        Assert.Equal(day, read.Status?.RepairDate);
        Assert.DoesNotContain("batteryStatus", read.Values.Keys);
    }

    [Theory]
    [InlineData("Fan replaced", "Repaired on 20 Oct 2026: “Fan replaced.” The status becomes re-used, with a new version.")]
    [InlineData("Fan replaced. ", "Repaired on 20 Oct 2026: “Fan replaced.” The status becomes re-used, with a new version.")]
    [InlineData("Fan replaced?", "Repaired on 20 Oct 2026: “Fan replaced?” The status becomes re-used, with a new version.")]
    public void TheRepairLine_ShouldEndTheDescriptionWithOneFullStop(string description, string line) =>
        Assert.Equal(line, UploadService.StatusWords(new UploadStatus(StatusEvent.Repaired, "2026-10-20", description), "2026-10-20", "re-used", done: false));

    [Fact]
    public async Task ARepairOnTheBatterysPage_ShouldBeCheckedThenSavedThroughTheStatusChange()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        var file = Repair(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 41, "capacityFade": 7 }""");

        var check = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", file);
        var battery = Assert.Single(check.Batteries);

        Assert.Null(check.Problem);
        Assert.Null(battery.Problem);
        Assert.Equal("Repaired on 20 Oct 2026: “Cell module 2 replaced.” The status becomes re-used, with a new version.", battery.Status);
        Assert.Contains("capacityFade", battery.Sent!.Keys);
        Assert.DoesNotContain("capacityFade", battery.Kept!);
        Assert.Contains("powerFade", battery.Kept!); // left out of the file: the battery's current value (Q8)
        Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId)); // a check changes nothing

        var saved = (await service.SaveCheckedAsync(PublicationTesting.Team, rig.PackId, check.CheckId))!;
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        Assert.Equal("Repaired on 20 Oct 2026: “Cell module 2 replaced.” The status became re-used, with a new version.", Assert.Single(saved.Batteries).Status);
        Assert.Equal("re-used", StatusChangeService.CurrentStatus(pack));
        Assert.Equal(("2026-10-20", "Cell module 2 replaced."), (pack.Repairs[^1].Date, pack.Repairs[^1].Description));
        Assert.Equal(7, pack.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(41, pack.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>()); // then the live values (Q22)
        Assert.Equal(2, (await rig.Store.ListVersionsAsync(rig.PackId)).Count);
        Assert.Equal("upload", (await LastAsync(rig, StatusChangeService.ChangedEvent))["source"].AsString);
        var logged = Assert.Single(Assert.Single(await rig.Store.ListUploadsAsync(10)).Batteries);
        Assert.Equal(saved.Batteries[0].Status, logged.Status);
        Assert.True(Assert.Single(logged.Values, value => value.Key == "powerFade").Kept);
    }

    [Fact]
    public async Task ARefusedStatusChange_ShouldSaveNothingOfTheBattery()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);

        var check = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", RepairWithoutWords(UploadServiceTests.Serial(rig)));
        var battery = Assert.Single(check.Batteries);

        Assert.Equal((UploadService.Refused, StatusChangeService.RepairNeedsDescription), (battery.Outcome, battery.Problem));
        Assert.Null(battery.Status);
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task TheApi_ShouldRefuseABatteryWithAStatusChange()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await UploadServiceTests.Service(rig).RunAsync(PublicationTesting.Team, JsonNode.Parse(Encoding.UTF8.GetString(Repair(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 41 }"""))), "api", null, preview: false);

        Assert.Equal((UploadService.Refused, UploadService.StatusOnThePage), (Assert.Single(report.Batteries).Outcome, report.Batteries[0].Problem));
        Assert.Equal("original", StatusChangeService.CurrentStatus((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!));
    }

    private static byte[] Repair(string serial, string values) => Encoding.UTF8.GetBytes(UploadServiceTests.File(
        $$"""{ "batterySerialNumber": "{{serial}}", "reason": "repair", "repairDescription": "Cell module 2 replaced.", "repairDate": "2026-10-20", "values": {{values}} }"""));

    private static byte[] RepairWithoutWords(string serial) => Encoding.UTF8.GetBytes(UploadServiceTests.File(
        $$"""{ "batterySerialNumber": "{{serial}}", "reason": "repair", "values": { "stateOfChargeSoC": 41 } }"""));

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusUploadTests | FullyQualifiedName~UploadServiceTests | FullyQualifiedName~UploadCheckTests | FullyQualifiedName~BmsExportUploadTests | FullyQualifiedName~DppApiControllerTests | FullyQualifiedName~StatusChangeTests | FullyQualifiedName~StatusPagesTests | FullyQualifiedName~SavePipelineTests | FullyQualifiedName~DppServiceWiringTests"`
Expected: the build fails with CS0117, CS0246, CS1061, CS1729 (for example `error CS0117 'UploadService' does not contain a definition for 'StatusOnThePage'`).

- [ ] **Step 3: Write the code**

Patch `5d-task1-code.patch` (`web/Dpp/Editing/SavePipeline.cs`, `web/Dpp/Editing/StatusChangeService.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Uploads/UploadFile.cs`, `web/Dpp/Uploads/UploadService.cs`). Save the block as `../5d-task1-code.patch`, outside the worktree, and run `git apply ../5d-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Editing/SavePipeline.cs b/web/Dpp/Editing/SavePipeline.cs
index 64fd819..ea1733c 100644
--- a/web/Dpp/Editing/SavePipeline.cs
+++ b/web/Dpp/Editing/SavePipeline.cs
@@ -81,8 +81,10 @@ public sealed class SavePipeline(
     /// or nothing: one refused or empty key saves none. Only <see cref="StatusChangeService"/> calls it, after its own
     /// checks of the transition.
     /// </summary>
-    public Task<SaveResult> SaveStatusChangeAsync(ChangeSender sender, string packId, IReadOnlyDictionary<string, JsonNode?> values, CancellationToken cancellationToken = default) =>
-        SaveCoreAsync(sender, new ChangeSet(TargetKind.Pack, packId, values, "form"), preview: false, statusChange: true, cancellationToken);
+    /// <param name="preview">True for an upload's check (step 5d): every key is judged, nothing is saved.</param>
+    /// <param name="source">"form", or "upload" for a status change in an uploaded file (step 5d).</param>
+    public Task<SaveResult> SaveStatusChangeAsync(ChangeSender sender, string packId, IReadOnlyDictionary<string, JsonNode?> values, CancellationToken cancellationToken = default, bool preview = false, string source = "form") =>
+        SaveCoreAsync(sender, new ChangeSet(TargetKind.Pack, packId, values, source), preview, statusChange: true, cancellationToken);
 
     private async Task<SaveResult> SaveCoreAsync(ChangeSender sender, ChangeSet changes, bool preview, bool statusChange, CancellationToken cancellationToken)
     {
diff --git a/web/Dpp/Editing/StatusChangeService.cs b/web/Dpp/Editing/StatusChangeService.cs
index 2693371..77b06c8 100644
--- a/web/Dpp/Editing/StatusChangeService.cs
+++ b/web/Dpp/Editing/StatusChangeService.cs
@@ -132,7 +132,9 @@ public sealed class StatusChangeService(
         return pack.Values.GetValueOrDefault(StatusProperty)?.Value?["batteryStatusValues"]?.GetValue<string>();
     }
 
-    public async Task<StatusChangeResult> ChangeAsync(ChangeSender sender, string packId, StatusChange change, CancellationToken cancellationToken = default)
+    /// <param name="preview">True for an upload's check (step 5d): every rule is checked and nothing changes.</param>
+    /// <param name="source">"form", or "upload" for a status change in an uploaded file (step 5d).</param>
+    public async Task<StatusChangeResult> ChangeAsync(ChangeSender sender, string packId, StatusChange change, CancellationToken cancellationToken = default, bool preview = false, string source = "form")
     {
         ArgumentNullException.ThrowIfNull(sender);
         ArgumentNullException.ThrowIfNull(change);
@@ -222,7 +224,14 @@ public sealed class StatusChangeService(
             values[attribute.Property] = change.Restated.GetValueOrDefault(attribute.Property)?.DeepClone();
         }
 
-        var saved = await pipeline.SaveStatusChangeAsync(sender, packId, values, cancellationToken);
+        var saved = await pipeline.SaveStatusChangeAsync(sender, packId, values, cancellationToken, preview, source);
+        if (preview)
+        {
+            return saved.Keys.All(key => key.Outcome == KeyOutcome.Saved)
+                ? new StatusChangeResult(true, null, saved.Keys, null, to)
+                : new StatusChangeResult(true, null, saved.Keys, ValuesRefused);
+        }
+
         if (!saved.Saved)
         {
             return new StatusChangeResult(true, null, saved.Keys, ValuesRefused);
@@ -257,7 +266,7 @@ public sealed class StatusChangeService(
             ChangedEvent,
             sender.Who,
             sender.Kind.ToString(),
-            "form",
+            source,
             change.What switch
             {
                 StatusEvent.Repaired => $"Battery {serial} repaired: {repair!.Description} Status {to}.",
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index 8a6b5a9..ce5e7d8 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -318,6 +318,9 @@ public sealed class UploadedBattery
 
     /// <summary>What the file's adapter wanted the sender to know, e.g. a value it couldn't read (step 5b).</summary>
     public List<string> Notes { get; init; } = [];
+
+    /// <summary>The status change or repair the upload made, in words (step 5d); null when it made none.</summary>
+    public string? Status { get; init; }
 }
 
 /// <summary>One value of an upload: its key, its outcome ("Saved", "KeptEmpty", "Ignored", "Rejected") and why.</summary>
@@ -328,6 +331,9 @@ public sealed class UploadedValue
     /// <summary>The value as the file sent it (JSON text), shown in the battery's upload history (step 5c).</summary>
     public string? Value { get; init; }
 
+    /// <summary>A re-stated value the file left out, taken from the battery's current one (step 5d, Q8).</summary>
+    public bool Kept { get; init; }
+
     public required string Outcome { get; init; }
 
     public string? Message { get; init; }
diff --git a/web/Dpp/Uploads/UploadFile.cs b/web/Dpp/Uploads/UploadFile.cs
index a2d82e8..ac43afd 100644
--- a/web/Dpp/Uploads/UploadFile.cs
+++ b/web/Dpp/Uploads/UploadFile.cs
@@ -1,6 +1,7 @@
 using System.Globalization;
 using System.Text.Json;
 using System.Text.Json.Nodes;
+using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Store;
 
 namespace BatteryPassWeb.Dpp.Uploads;
@@ -16,6 +17,7 @@ namespace BatteryPassWeb.Dpp.Uploads;
 /// <param name="Modules">The battery's module list, when the file has one (the battery system's export, decision 31).</param>
 /// <param name="Notes">What the file's adapter wants the sender to know, e.g. a value it couldn't read.</param>
 /// <param name="FillOnly">Keys that only fill a battery without a value, left out silently otherwise (the export's #17).</param>
+/// <param name="Status">A status change or repair the entry asks for (step 5d); null for values only.</param>
 /// <param name="RemainingCapacityAh">The measured remaining capacity, kept as app data for the fade suggestion (J-11).</param>
 public sealed record UploadEntry(
     int Number,
@@ -26,7 +28,14 @@ public sealed record UploadEntry(
     IReadOnlyList<BatteryModule>? Modules = null,
     IReadOnlyList<string>? Notes = null,
     IReadOnlyCollection<string>? FillOnly = null,
-    decimal? RemainingCapacityAh = null);
+    decimal? RemainingCapacityAh = null,
+    UploadStatus? Status = null);
+
+/// <summary>
+/// A status change in an upload entry (design 8.2, decision 35 Q20): what happened, and for a repair its day
+/// (yyyy-MM-dd; the day of measuredAt when the file gives none) and what was repaired.
+/// </summary>
+public sealed record UploadStatus(StatusEvent What, string? RepairDate = null, string? RepairDescription = null);
 
 /// <summary>A file read by its format's adapter: the format, its entries, or why it can't be read at all.</summary>
 public sealed record UploadDocument(string? Format, IReadOnlyList<UploadEntry> Entries, string? Problem = null)
@@ -103,18 +112,63 @@ public sealed class NativeUploadFormat : IUploadAdapter
             measuredAt = parsed.UtcDateTime;
         }
 
-        if (entry["values"] is not JsonObject values || values.Count == 0)
+        var repair = Text(entry["reason"]) is { } reason
+            ? reason == "repair" ? true : (bool?)null
+            : false;
+        if (repair is null)
+        {
+            return Broken(number, namedAs, "the only reason an entry gives is \"repair\"");
+        }
+
+        if ((entry["values"] is not JsonObject values || values.Count == 0) && repair != true)
         {
             return Broken(number, namedAs, "the entry has no values (\"values\": {...})");
         }
 
-        return new UploadEntry(
-            number,
-            namedAs,
-            measuredAt,
-            values.ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal));
+        var given = (entry["values"] as JsonObject ?? new JsonObject()).ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
+        UploadStatus? status = null;
+        var named = given.Remove(StatusProperty, out var statusValue) ? StatusText(statusValue) : null;
+        if (repair == true)
+        {
+            if (named is not null)
+            {
+                return Broken(number, namedAs, "a repair sets the battery status itself; leave batteryStatus out");
+            }
+
+            var date = Text(entry["repairDate"]);
+            if (date is not null && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
+            {
+                return Broken(number, namedAs, "repairDate isn't a day, such as 2026-10-30");
+            }
+
+            status = new UploadStatus(StatusEvent.Repaired, date ?? measuredAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Text(entry["repairDescription"]));
+        }
+        else if (statusValue is not null)
+        {
+            StatusEvent? what = named switch
+            {
+                "waste" => StatusEvent.Waste,
+                "re-used" => StatusEvent.PlacedOnMarketAgain,
+                "remanufactured" => StatusEvent.Remanufactured,
+                "repurposed" => StatusEvent.Repurposed,
+                _ => null,
+            };
+            if (what is null)
+            {
+                return Broken(number, namedAs, $"batteryStatus \"{named}\" can't be set by upload; it takes waste, re-used, remanufactured or repurposed, or a repair");
+            }
+
+            status = new UploadStatus(what.Value);
+        }
+
+        return new UploadEntry(number, namedAs, measuredAt, given, Status: status);
     }
 
+    private const string StatusProperty = "batteryStatus";
+
+    /// <summary>The battery status as a bare word or in the schema's own form.</summary>
+    private static string? StatusText(JsonNode? value) => Text(value) ?? Text(value?["batteryStatusValues"]);
+
     private static UploadEntry Broken(int number, string namedAs, string problem) =>
         new(number, namedAs, null, new Dictionary<string, JsonNode?>(), problem);
 
diff --git a/web/Dpp/Uploads/UploadService.cs b/web/Dpp/Uploads/UploadService.cs
index 8b67013..5066a82 100644
--- a/web/Dpp/Uploads/UploadService.cs
+++ b/web/Dpp/Uploads/UploadService.cs
@@ -16,6 +16,8 @@ namespace BatteryPassWeb.Dpp.Uploads;
 /// <param name="Modules">What happened to the battery's module list, when the file had one; null otherwise.</param>
 /// <param name="Notes">What the file's adapter wants the sender to know, e.g. a value it couldn't read.</param>
 /// <param name="Sent">The values the battery's entry sent, as JSON text, by key (step 5c).</param>
+/// <param name="Status">The status change or repair in words (step 5d); null when the entry asked for none.</param>
+/// <param name="Kept">Re-stated values the file left out, taken from the battery's current ones (Q8).</param>
 public sealed record UploadedBatteryReport(
     int Entry,
     string NamedAs,
@@ -27,7 +29,9 @@ public sealed record UploadedBatteryReport(
     string? Modules = null,
     IReadOnlyList<string>? Notes = null,
     string? ClusterId = null,
-    IReadOnlyDictionary<string, string?>? Sent = null);
+    IReadOnlyDictionary<string, string?>? Sent = null,
+    string? Status = null,
+    IReadOnlyCollection<string>? Kept = null);
 
 /// <summary>An upload's result: per battery what happened, or why the file couldn't be read at all.</summary>
 /// <param name="UploadId">The log entry; null for a preview or an unreadable file, which aren't logged.</param>
@@ -50,8 +54,12 @@ public sealed class UploadService(
     BatteryApi batteries,
     ApplicationAuditService audit,
     TimeProvider clock,
+    StatusChangeService statusChanges,
     IEnumerable<IUploadAdapter> adapters)
 {
+    /// <summary>Why an API upload's battery with a status change is refused (decision 35, Q7 and Q23).</summary>
+    public const string StatusOnThePage = "status changes are made on the battery's page";
+
     public const int MaxBatteries = 500;
     public const int MaxBytes = 2 * 1024 * 1024;
     public const string UploadedEvent = "dpp.upload";
@@ -142,19 +150,58 @@ public sealed class UploadService(
                 continue;
             }
 
+            // A status change or repair first (Q22): refused, nothing of the battery is saved; the API takes none (Q23).
+            string? statusWords = null;
+            IReadOnlyList<KeyResult> statusKeys = [];
+            IReadOnlyCollection<string> kept = [];
+            var restatedSent = new Dictionary<string, string?>(StringComparer.Ordinal);
+            var current = entry;
+            if (entry.Status is { } asked)
+            {
+                if (channel != "page")
+                {
+                    reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, StatusOnThePage, [], Notes: notes, ClusterId: Cluster(pack)));
+                    continue;
+                }
+
+                var (changed, keptKeys, restated, day) = await StatusAsync(sender, pack, entry, asked, preview, cancellationToken);
+                statusKeys = changed.Keys.Where(key => key.Key != StatusProperty).ToArray();
+                kept = keptKeys;
+                foreach (var key in restated.Keys)
+                {
+                    restatedSent[key] = keptKeys.Contains(key) ? null : restated[key]?.ToJsonString();
+                }
+
+                if (changed.Problem is { } refused)
+                {
+                    reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, refused, statusKeys, Notes: notes, ClusterId: Cluster(pack), Sent: restatedSent, Kept: kept));
+                    continue;
+                }
+
+                statusWords = StatusWords(asked, day, changed.NewStatus ?? string.Empty, done: !preview);
+                current = entry with { Values = entry.Values.Where(pair => !restated.ContainsKey(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) };
+            }
+
             var source = Source(sender);
-            var (values, unused) = await FillOnlyAsync(sender, pack, entry, cancellationToken);
+            var (values, unused) = await FillOnlyAsync(sender, pack, current, cancellationToken);
             notes = [.. notes, .. unused];
-            var result = await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, values, source, entry.MeasuredAtUtc), preview, cancellationToken);
+            var result = values.Count == 0
+                ? new SaveResult(true, false, [])
+                : await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, values, source, entry.MeasuredAtUtc), preview, cancellationToken);
             var modules = await ModulesAsync(sender, pack, entry.Modules, preview, cancellationToken);
             if (!preview && entry.RemainingCapacityAh is { } remaining)
             {
                 await store.SetRemainingCapacityAsync(pack.Id, new RemainingCapacityReading { Ah = remaining, MeasuredAtUtc = entry.MeasuredAtUtc ?? clock.GetUtcNow().UtcDateTime }, cancellationToken);
             }
 
-            var outcome = result.Keys.Any(key => key.Outcome == KeyOutcome.Saved) || modules is { Changed: true } ? Saved : NothingSaved;
+            var outcome = statusWords is not null || result.Keys.Any(key => key.Outcome == KeyOutcome.Saved) || modules is { Changed: true } ? Saved : NothingSaved;
             var sent = values.ToDictionary(pair => pair.Key, pair => pair.Value?.ToJsonString(), StringComparer.Ordinal);
-            reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, result.Keys, modules?.Words, notes, Cluster(pack), sent));
+            foreach (var (key, value) in restatedSent)
+            {
+                sent[key] = value;
+            }
+
+            reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, [.. statusKeys, .. result.Keys], modules?.Words, notes, Cluster(pack), sent, statusWords, kept));
         }
 
         string? uploadId = null;
@@ -320,6 +367,53 @@ public sealed class UploadService(
 
     private static string? Cluster(BatteryPack? pack) => pack?.ClusterId is { } cluster ? ClusterRepository.NormalizeClusterId(cluster) : null;
 
+    private const string StatusProperty = "batteryStatus";
+
+    /// <summary>
+    /// The entry's status change through <see cref="StatusChangeService"/> (Q20–Q22), as the upload: the re-stated values
+    /// it gives, and the battery's current ones for those it leaves out (Q8, "kept"). A repair's day is the file's
+    /// repairDate, else the day of measuredAt, else today.
+    /// </summary>
+    private async Task<(StatusChangeResult Result, IReadOnlyCollection<string> Kept, IReadOnlyDictionary<string, JsonNode?> Restated, string Day)> StatusAsync(
+        ChangeSender sender, BatteryPack pack, UploadEntry entry, UploadStatus asked, bool preview, CancellationToken cancellationToken)
+    {
+        var sources = await store.LoadSourcesAsync(pack.Id, cancellationToken);
+        var restated = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
+        var kept = new HashSet<string>(StringComparer.Ordinal);
+        foreach (var attribute in sources is null ? [] : StatusChangeService.RestatedAttributes(sources.Model))
+        {
+            if (entry.Values.TryGetValue(attribute.Property, out var given) && given is not null
+                && !(given.GetValueKind() == JsonValueKind.String && string.IsNullOrWhiteSpace(given.GetValue<string>())))
+            {
+                restated[attribute.Property] = given;
+            }
+            else
+            {
+                restated[attribute.Property] = (pack.Values.GetValueOrDefault(attribute.Property)?.Value ?? sources!.Model.Values.GetValueOrDefault(attribute.Property)?.Value)?.DeepClone();
+                kept.Add(attribute.Property);
+            }
+        }
+
+        var day = asked.RepairDate ?? clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
+        var result = await statusChanges.ChangeAsync(sender, pack.Id, new StatusChange(asked.What, restated, day, asked.RepairDescription), cancellationToken, preview, "upload");
+        return (result, kept, restated, day);
+    }
+
+    /// <summary>The status change in words, for the check (Q21) and, once saved, the battery's earlier uploads.</summary>
+    public static string StatusWords(UploadStatus asked, string day, string to, bool done)
+    {
+        ArgumentNullException.ThrowIfNull(asked);
+        var becomes = done ? "became" : "becomes";
+        var description = asked.RepairDescription?.Trim() ?? string.Empty;
+        var said = description.EndsWith('.') || description.EndsWith('!') || description.EndsWith('?') ? $"“{description}”" : $"“{description}.”";
+        return asked.What switch
+        {
+            StatusEvent.Repaired => $"Repaired on {ValueWords.Day(day)}: {said} The status {becomes} {to}, with a new version.",
+            StatusEvent.Waste => $"The status {becomes} waste, with a new version.",
+            _ => $"The status {becomes} {to}, with a new version. Then name who places the battery on the market again, on this page.",
+        };
+    }
+
     private static string Source(ChangeSender sender) => sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload";
 
     /// <summary>
@@ -404,10 +498,12 @@ public sealed class UploadService(
                 Problem = report.Problem,
                 Modules = report.Modules,
                 Notes = report.Notes?.ToList() ?? [],
+                Status = report.Status,
                 Values = report.Values.Select(value => new UploadedValue
                 {
                     Key = value.Key,
                     Value = report.Sent?.GetValueOrDefault(value.Key),
+                    Kept = report.Kept?.Contains(value.Key) == true,
                     Outcome = value.Outcome.ToString(),
                     Message = string.IsNullOrEmpty(value.Message) ? null : value.Message,
                 }).ToList(),
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusUploadTests | FullyQualifiedName~UploadServiceTests | FullyQualifiedName~UploadCheckTests | FullyQualifiedName~BmsExportUploadTests | FullyQualifiedName~DppApiControllerTests | FullyQualifiedName~StatusChangeTests | FullyQualifiedName~StatusPagesTests | FullyQualifiedName~SavePipelineTests | FullyQualifiedName~DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:   134`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs web/Dpp/Editing/SavePipeline.cs web/Dpp/Editing/StatusChangeService.cs web/Dpp/Store/DppRecords.cs web/Dpp/Uploads/UploadFile.cs web/Dpp/Uploads/UploadService.cs
git commit -m "feat(dpp): status changes and repairs by upload, through the battery's status change" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The status line, kept values and earlier uploads in "Upload a file"

The shots approved on 4 Nov 2026 (Q21). The check shows the status line above the values, the re-stated values the file leaves out in grey as "Kept: the battery's current value", and a refused status change in one line, without "Save". After saving: "Saved." and the line. Earlier uploads say "Status changed" with the line, and name a battery system's uploads "Battery system", never by its token's ID.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs`
- Modify: `web/Dpp/Pages/UploadPages.cs`
- Modify: `web/Views/Batteries/_UploadCard.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `5d-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs`). Save the block as `../5d-task2-tests.patch`, outside the worktree, and run `git apply ../5d-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs b/BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs
index b7d803d..ec6dca9 100644
--- a/BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs
@@ -101,6 +101,56 @@ public sealed class StatusUploadTests(MongoFixture mongo)
         Assert.Equal("original", StatusChangeService.CurrentStatus((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!));
     }
 
+    [Fact]
+    public async Task TheCheck_ShouldShowTheStatusLineAndTheKeptValuesInWords()
+    {
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", Repair(UploadServiceTests.Serial(rig), """{ "capacityFade": 7 }"""));
+        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
+
+        var view = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, check.CheckId, null)).Check!;
+
+        Assert.True(view.WillChange);
+        Assert.StartsWith("Repaired on 20 Oct 2026", view.Status, StringComparison.Ordinal);
+        Assert.Equal("Will be saved", Assert.Single(view.Values, row => row.Label.StartsWith("#59", StringComparison.Ordinal)).Result);
+        var kept = Assert.Single(view.Values, row => row.Label.StartsWith("#69", StringComparison.Ordinal));
+        Assert.Equal(("Kept: the battery's current value", string.Empty, true), (kept.Result, kept.InFile, kept.Quiet));
+        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_UploadCard.cshtml"));
+        Assert.Contains("check.Status is { } status", card, StringComparison.Ordinal);
+        Assert.Contains("upload.Status is { } uploadStatus", card, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public async Task TheCheck_ShouldSayWhyAStatusChangeIsRefused_AndOfferNoSave()
+    {
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", RepairWithoutWords(UploadServiceTests.Serial(rig)));
+        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
+
+        var view = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, check.CheckId, null)).Check!;
+
+        Assert.Equal((false, "Say what was repaired.", (string?)null), (view.WillChange, view.Problem, view.Status));
+    }
+
+    [Fact]
+    public async Task TheEarlierUploads_ShouldSayStatusChanged_AndNameABatterySystemSo()
+    {
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        var service = UploadServiceTests.Service(rig);
+        var check = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", Repair(UploadServiceTests.Serial(rig), """{ "capacityFade": 7 }"""));
+        await service.SaveCheckedAsync(PublicationTesting.Team, rig.PackId, check.CheckId);
+        rig.Clock.Now = rig.Clock.Now.AddMinutes(5);
+        var feed = ChangeSender.Feed("api-token:feed-1", BatteryTesting.North);
+        await service.RunAsync(feed, JsonNode.Parse(UploadServiceTests.File($$"""{ "batterySerialNumber": "{{UploadServiceTests.Serial(rig)}}", "values": { "stateOfChargeSoC": 40 } }""")), "api", null, preview: false);
+        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
+
+        var history = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, null, null)).History;
+
+        Assert.Equal(("Battery system · upload file", "1 value saved", (string?)null), (history[0].WhoAndWhat, history[0].Result, history[0].Status));
+        Assert.Equal("Status changed", history[1].Result);
+        Assert.Equal("Repaired on 20 Oct 2026: “Cell module 2 replaced.” The status became re-used, with a new version.", history[1].Status);
+    }
+
     private static byte[] Repair(string serial, string values) => Encoding.UTF8.GetBytes(UploadServiceTests.File(
         $$"""{ "batterySerialNumber": "{{serial}}", "reason": "repair", "repairDescription": "Cell module 2 replaced.", "repairDate": "2026-10-20", "values": {{values}} }"""));
 
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusUploadTests | FullyQualifiedName~UploadCheckTests | FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~ModuleDataTests | FullyQualifiedName~RedesignFixesTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'UploadCheckView' does not contain a definition for 'Status'`).

- [ ] **Step 3: Write the code**

Patch `5d-task2-code.patch` (`web/Dpp/Pages/UploadPages.cs`, `web/Views/Batteries/_UploadCard.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../5d-task2-code.patch`, outside the worktree, and run `git apply ../5d-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/UploadPages.cs b/web/Dpp/Pages/UploadPages.cs
index 1476672..8ea3cfc 100644
--- a/web/Dpp/Pages/UploadPages.cs
+++ b/web/Dpp/Pages/UploadPages.cs
@@ -22,7 +22,8 @@ public sealed record UploadCheckView(
     string? Problem,
     IReadOnlyList<UploadValueRow> Values,
     string? Modules,
-    IReadOnlyList<string> Notes);
+    IReadOnlyList<string> Notes,
+    string? Status = null);
 
 /// <summary>One earlier upload of the battery (mock-up B1, B3), cut to this battery's line.</summary>
 public sealed record UploadHistoryRow(
@@ -34,7 +35,8 @@ public sealed record UploadHistoryRow(
     string? Problem,
     IReadOnlyList<UploadValueRow> Values,
     string? Modules,
-    IReadOnlyList<string> Notes);
+    IReadOnlyList<string> Notes,
+    string? Status = null);
 
 /// <summary>The "Upload a file" card of a battery page (decision 35, step 5c, mock-up B1–B4).</summary>
 /// <param name="Action">Where the card's forms post: the battery page plus "/upload".</param>
@@ -73,9 +75,10 @@ public sealed class UploadPages(UploadService uploads, AttributeForms forms, Att
                 FormatWords(shown.Report.Format),
                 battery.Outcome == UploadService.Saved,
                 battery.Problem is null ? null : Capitalise(battery.Problem),
-                Rows(pack, battery.Values.Select(value => (value.Key, value.Outcome, value.Message, battery.Sent?.GetValueOrDefault(value.Key))), preview: true),
+                Rows(pack, battery.Values.Select(value => (value.Key, value.Outcome, value.Message, battery.Sent?.GetValueOrDefault(value.Key), battery.Kept?.Contains(value.Key) == true)), preview: true),
                 battery.Modules is null ? null : Capitalise(battery.Modules),
-                (battery.Notes ?? []).Select(Capitalise).ToArray());
+                (battery.Notes ?? []).Select(Capitalise).ToArray(),
+                battery.Status);
         }
 
         var history = (await uploads.HistoryAsync(pack, forTeam, cancellationToken))
@@ -83,21 +86,23 @@ public sealed class UploadPages(UploadService uploads, AttributeForms forms, Att
             .Select(upload =>
             {
                 var battery = upload.Batteries[0];
-                var values = Rows(pack, battery.Values.Select(value => (value.Key, Enum.Parse<KeyOutcome>(value.Outcome), value.Message ?? string.Empty, value.Value)), preview: false);
+                var values = Rows(pack, battery.Values.Select(value => (value.Key, Enum.Parse<KeyOutcome>(value.Outcome), value.Message ?? string.Empty, value.Value, value.Kept)), preview: false);
                 var saved = battery.Values.Count(value => value.Outcome == nameof(KeyOutcome.Saved));
                 var result = battery.Outcome == UploadService.Refused
                     ? "Refused"
+                    : battery.Status is not null ? "Status changed"
                     : saved == 0 && battery.Modules?.Contains("was set", StringComparison.Ordinal) != true ? "Nothing saved" : $"{Words.Count(saved, "value")} saved";
                 return new UploadHistoryRow(
                     upload.Id,
                     ValueWords.Time(upload.AtUtc),
                     upload.Channel == "api" ? "API" : "Page",
-                    $"{upload.Who} · {upload.FileName ?? FormatWords(upload.Format)}",
+                    $"{(upload.SentAs == nameof(SenderKind.BatterySystemFeed) ? "Battery system" : upload.Who)} · {upload.FileName ?? FormatWords(upload.Format)}",
                     result,
                     battery.Problem is null ? null : Capitalise(battery.Problem),
                     values,
                     battery.Modules is null ? null : Capitalise(battery.Modules),
-                    battery.Notes.Select(Capitalise).ToArray());
+                    battery.Notes.Select(Capitalise).ToArray(),
+                    battery.Status);
             })
             .ToArray();
         return new UploadCardView($"{href}/upload", forTeam, check, history, openId);
@@ -153,16 +158,16 @@ public sealed class UploadPages(UploadService uploads, AttributeForms forms, Att
 
         var saved = battery.Values.Count(value => value.Outcome == KeyOutcome.Saved);
         var modules = battery.Modules?.Contains("was set", StringComparison.Ordinal) == true ? $", and {battery.Modules}" : string.Empty;
-        return $"Saved: {Words.Count(saved, "value")}{modules}.";
+        return battery.Status is { } status ? $"Saved. {status}" : $"Saved: {Words.Count(saved, "value")}{modules}.";
     }
 
-    private UploadValueRow[] Rows(BatteryPack pack, IEnumerable<(string Key, KeyOutcome Outcome, string Message, string? Sent)> values, bool preview) =>
+    private UploadValueRow[] Rows(BatteryPack pack, IEnumerable<(string Key, KeyOutcome Outcome, string Message, string? Sent, bool Kept)> values, bool preview) =>
         values.Select(value =>
         {
             var attribute = AttributeCatalog.FindByProperty(value.Key);
             var label = attribute is null ? value.Key : $"{attribute.Label} {attribute.Name}";
             var sent = value.Sent is null ? null : JsonNode.Parse(value.Sent);
-            var result = value.Outcome switch
+            var result = value.Kept && value.Outcome == KeyOutcome.Saved ? "Kept: the battery's current value" : value.Outcome switch
             {
                 KeyOutcome.Saved => preview ? "Will be saved" : "Saved",
                 KeyOutcome.KeptEmpty => "Kept: empty in the file",
@@ -175,7 +180,7 @@ public sealed class UploadPages(UploadService uploads, AttributeForms forms, Att
                 attribute is null ? sent?.ToJsonString() ?? string.Empty : Text(attribute, sent is null ? null : normalizer.Normalize(attribute, sent)),
                 result,
                 value.Outcome == KeyOutcome.Rejected,
-                value.Outcome is KeyOutcome.Ignored or KeyOutcome.KeptEmpty);
+                value.Outcome is KeyOutcome.Ignored or KeyOutcome.KeptEmpty || value.Kept);
         }).ToArray();
 
     private string Text(AttributeDefinition attribute, JsonNode? value)
diff --git a/web/Views/Batteries/_UploadCard.cshtml b/web/Views/Batteries/_UploadCard.cshtml
index c28c8db..f29ecf0 100644
--- a/web/Views/Batteries/_UploadCard.cshtml
+++ b/web/Views/Batteries/_UploadCard.cshtml
@@ -14,7 +14,11 @@
             <span class="text-secondary">@check.FileName · @check.FormatWords</span>
             <span class="bp-soft-tag @(check.WillChange ? "is-saved" : "is-refused")">@(check.WillChange ? "Will change" : "Nothing to save")</span>
         </div>
-        <p class="bp-help-text mt-2 mb-0">Nothing is saved yet. Live values change the passport in place; the battery's own values make a new version.</p>
+        <p class="bp-help-text mt-2 mb-0">Nothing is saved yet. Live values change the passport in place; the battery's own values and a status change make a new version.</p>
+        @if (check.Status is { } status)
+        {
+            <p class="bp-upload-status mt-2 mb-0" data-upload-status>@status</p>
+        }
         @if (check.Problem is { } problem)
         {
             <p class="mt-2 mb-0">@problem</p>
@@ -103,6 +107,10 @@
                             {
                                 <p class="mb-0">@refused</p>
                             }
+                            @if (upload.Status is { } uploadStatus)
+                            {
+                                <p class="bp-upload-status mb-0">@uploadStatus</p>
+                            }
                             @if (upload.Values.Count > 0)
                             {
                                 <table class="bp-upload-values">
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 8ebe7cc..777f904 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3760,6 +3760,11 @@ main .bp-field-marker.is-published {
   color: #991b1b;
 }
 
+/* A status change or repair in the check (step 5d, mock-up B2's dashed line). */
+.bp-upload-status {
+  font-weight: 600;
+}
+
 .bp-upload-notes {
   margin: 8px 0 0;
   padding-left: 18px;
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusUploadTests | FullyQualifiedName~UploadCheckTests | FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~ModuleDataTests | FullyQualifiedName~RedesignFixesTests"`
Expected: `Passed!  - Failed:     0, Passed:    66`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs web/Dpp/Pages/UploadPages.cs web/Views/Batteries/_UploadCard.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): the status line and kept values in a battery's upload check and earlier uploads" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The help page's sample files and the demo uploads

Q24 and Q25. The guide's new part "Upload a file", for the team and cluster admins, links two sample files for the demo battery DEMO-C7M-0001, served by `GET /help/samples/{name}` to them only; the API's help says a battery with a status change is refused. The demo data sends DEMO-C7M-0001 an export from demo-north's battery system and a file from its cluster admin, through the upload service, marks both as demo data, and clears them with the demo batteries' kept checks.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs`
- Modify: `web/Controllers/HelpController.cs`
- Modify: `web/Dpp/Demo/DemoCatalogue.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Dpp/Demo/demo-catalogue.json`
- Modify: `web/Dpp/Store/DppStore.cs`
- Create: `web/Dpp/Uploads/UploadSamples.cs`
- Modify: `web/Views/Help/Guide.cshtml`
- Modify: `web/Views/Help/Index.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `5d-task3-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoClearTests.cs`, `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`, `BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs`). Save the block as `../5d-task3-tests.patch`, outside the worktree, and run `git apply ../5d-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
index fc50957..43e5e6b 100644
--- a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
@@ -20,10 +20,12 @@ public sealed class DemoClearTests(MongoFixture mongo)
     {
         using var rig = await DemoRig.StartAsync(mongo);
         await rig.Demo.SeedAsync(DemoRig.Who);
+        var demoPack = (await rig.All(DppStore.Packs)).First()["_id"].AsString;
+        await rig.Database.GetCollection<BsonDocument>(DppStore.UploadChecks).InsertOneAsync(new BsonDocument { ["_id"] = "a-kept-check", ["PackId"] = demoPack });
 
         var result = await rig.Demo.ClearAsync(DemoRig.Who);
 
-        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks", "users", "clusterMemberships", "clusters", "apiTokens" })
+        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DppStore.UploadChecks, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks", "users", "clusterMemberships", "clusters", "apiTokens" })
         {
             Assert.True(await rig.Count(collection) == 0, $"{collection} still has records");
         }
diff --git a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
index 1719e75..b829c6d 100644
--- a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
@@ -158,6 +158,23 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.Equal("demo-fleet", pack["ClusterId"].AsString); // it stays with the sender until demo-north accepts
     }
 
+    [Fact]
+    public async Task TheUploadExample_ShouldShowAnExportAndAFileOnTheCompact7M()
+    {
+        using var rig = await DemoRig.StartAsync(mongo);
+
+        await rig.Demo.SeedAsync(DemoRig.Who);
+
+        var pack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single();
+        var uploads = (await rig.All(DppStore.Uploads)).OrderBy(upload => upload["AtUtc"].ToUniversalTime()).ToList();
+        Assert.Equal(
+            [("api", "BatterySystemFeed", "BMS2_ACO 01.32.07", "saved"), ("page", "ClusterAdmin", "dpp-v3-upload/1", "saved")],
+            uploads.Select(upload => (upload["Channel"].AsString, upload["SentAs"].AsString, upload["Format"].AsString, upload["Batteries"][0]["Outcome"].AsString)));
+        Assert.All(uploads, upload => Assert.Equal(pack["_id"].AsString, upload["Batteries"].AsBsonArray.Single()["PackId"].AsString));
+        Assert.Equal(71, pack["Values"]["stateOfChargeSoC"]["Value"]["percentageValue"].ToInt32()); // the file came last
+        Assert.Equal(7, pack["Modules"].AsBsonArray.Count); // the export names the seed's modules: none added
+    }
+
     [Fact]
     public async Task EveryRecordItMakes_ShouldBeMarkedAsDemoData()
     {
@@ -165,7 +182,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 
         await rig.Demo.SeedAsync(DemoRig.Who);
 
-        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs })
+        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Uploads })
         {
             Assert.All(await rig.All(collection), record => Assert.True(record["IsDemo"].AsBoolean, $"{collection} {record["_id"]}"));
         }
diff --git a/BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs b/BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs
index ec6dca9..ac02b46 100644
--- a/BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs
@@ -1,9 +1,12 @@
 using System.Text;
 using System.Text.Json.Nodes;
+using BatteryPassWeb.Controllers;
 using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Store;
 using BatteryPassWeb.Dpp.Uploads;
 using BatteryPassWeb.Services;
+using Microsoft.AspNetCore.Authorization;
+using Microsoft.AspNetCore.Mvc;
 using MongoDB.Bson;
 using MongoDB.Driver;
 
@@ -11,7 +14,8 @@ namespace BatteryPassWeb.Tests.Dpp;
 
 /// <summary>
 /// Status changes and repairs by upload (design 8.2, decision 35 Q20–Q25; step 5d): read from the app's format, made
-/// through the status change on the battery's page only, shown in the check with the kept values and refused by the API.
+/// through the status change on the battery's page only, shown in the check with the kept values, refused by the API, and
+/// the help page's sample files.
 /// </summary>
 [Collection(MongoCollection.Name)]
 public sealed class StatusUploadTests(MongoFixture mongo)
@@ -151,6 +155,30 @@ public sealed class StatusUploadTests(MongoFixture mongo)
         Assert.Equal("Repaired on 20 Oct 2026: “Cell module 2 replaced.” The status became re-used, with a new version.", history[1].Status);
     }
 
+    [Fact]
+    public void TheHelpsSamples_ShouldBeForSignedInTeamsAndClusterAdmins_AndReadable()
+    {
+        var action = typeof(HelpController).GetMethod(nameof(HelpController.Sample))!;
+        var controller = new HelpController(null!);
+        var clock = new MutableTimeProvider(DppStoreTesting.Now);
+
+        var upload = Assert.IsType<FileContentResult>(controller.Sample(UploadSamples.UploadFileName, clock));
+        var export = Assert.IsType<FileContentResult>(controller.Sample(UploadSamples.ExportFileName, clock));
+
+        Assert.Equal("ClusterAdminOrAdmin", Assert.Single(action.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Policy);
+        Assert.Empty(action.GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
+        Assert.IsType<NotFoundResult>(controller.Sample("other.json", clock));
+        var entry = Assert.Single(Format.Read(JsonNode.Parse(upload.FileContents)!.AsObject()).Entries);
+        Assert.Equal((UploadSamples.Serial, StatusEvent.Repaired), (entry.NamedAs, entry.Status!.What));
+        var exported = Assert.Single(new BmsExportFormat().Read(JsonNode.Parse(export.FileContents)!.AsObject()).Entries);
+        Assert.Equal((UploadSamples.Serial, 7, "74"), (exported.NamedAs, exported.Modules!.Count, exported.Values["stateOfChargeSoC"]!.ToJsonString()));
+        var guide = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Guide.cshtml"));
+        Assert.Contains("/help/samples/@BatteryPassWeb.Dpp.Uploads.UploadSamples.UploadFileName", guide, StringComparison.Ordinal);
+        Assert.Contains("/help/samples/@BatteryPassWeb.Dpp.Uploads.UploadSamples.ExportFileName", guide, StringComparison.Ordinal);
+        var api = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Index.cshtml"));
+        Assert.Contains("@BatteryPassWeb.Dpp.Uploads.UploadService.StatusOnThePage", api, StringComparison.Ordinal); // decision (d)
+    }
+
     private static byte[] Repair(string serial, string values) => Encoding.UTF8.GetBytes(UploadServiceTests.File(
         $$"""{ "batterySerialNumber": "{{serial}}", "reason": "repair", "repairDescription": "Cell module 2 replaced.", "repairDate": "2026-10-20", "values": {{values}} }"""));
 
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusUploadTests | FullyQualifiedName~Tests.Demo. | FullyQualifiedName~Look.HelpTests | FullyQualifiedName~HelpWorkbenchLayoutTests | FullyQualifiedName~DppServiceWiringTests"`
Expected: the build fails with CS0103, CS0117, CS1501 (for example `error CS0103 The name 'UploadSamples' does not exist in the current context`).

- [ ] **Step 3: Write the code**

Patch `5d-task3-code.patch` (`web/Controllers/HelpController.cs`, `web/Dpp/Demo/DemoCatalogue.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/Demo/demo-catalogue.json`, `web/Dpp/Store/DppStore.cs`, `web/Views/Help/Guide.cshtml`, `web/Views/Help/Index.cshtml`). Save the block as `../5d-task3-code.patch`, outside the worktree, and run `git apply ../5d-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/HelpController.cs b/web/Controllers/HelpController.cs
index 77e9298..159efd3 100644
--- a/web/Controllers/HelpController.cs
+++ b/web/Controllers/HelpController.cs
@@ -26,6 +26,25 @@ public class HelpController : Controller
     [HttpGet("team")]
     public IActionResult Team() => Redirect("/help#workflow");
 
+    /// <summary>
+    /// A sample file for "Upload a file" (decision 35, Q24): the app's upload format or a battery system export, for the
+    /// demo battery DEMO-C7M-0001, to the team and cluster admins signed in only.
+    /// </summary>
+    [Authorize(Policy = "ClusterAdminOrAdmin")]
+    [HttpGet("samples/{name}")]
+    public IActionResult Sample(string name, [FromServices] TimeProvider clock)
+    {
+        var now = clock.GetUtcNow().UtcDateTime;
+        var json = name switch
+        {
+            BatteryPassWeb.Dpp.Uploads.UploadSamples.UploadFileName => BatteryPassWeb.Dpp.Uploads.UploadSamples.UploadFile(now.AddHours(-1)),
+            BatteryPassWeb.Dpp.Uploads.UploadSamples.ExportFileName => BatteryPassWeb.Dpp.Uploads.UploadSamples.Export(BatteryPassWeb.Dpp.Uploads.UploadSamples.Serial, now.AddHours(-1), 7)
+                .ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
+            _ => null,
+        };
+        return json is null ? NotFound() : File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", name);
+    }
+
     /// <summary>The API reference and request workbench.</summary>
     [AllowAnonymous]
     [HttpGet("api")]
diff --git a/web/Dpp/Demo/DemoCatalogue.cs b/web/Dpp/Demo/DemoCatalogue.cs
index 0171c4b..9fe039f 100644
--- a/web/Dpp/Demo/DemoCatalogue.cs
+++ b/web/Dpp/Demo/DemoCatalogue.cs
@@ -58,7 +58,8 @@ public sealed record DemoPack(
     string? Accident = null,
     DemoStatus? Status = null,
     DemoMark? Mark = null,
-    string? HandOverTo = null);
+    string? HandOverTo = null,
+    bool Uploads = false);
 
 /// <summary>
 /// What the demo data seed makes (decision 32): the Compact and Core families of the old demo on the new model, with
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index d7ea16e..f8d43c9 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -7,6 +7,7 @@ using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Publication;
 using BatteryPassWeb.Dpp.Store;
+using BatteryPassWeb.Dpp.Uploads;
 using BatteryPassWeb.Services;
 using Microsoft.Extensions.Options;
 using MongoDB.Bson;
@@ -45,7 +46,8 @@ public sealed class DemoDataService(
     ExternalApiRepository tokens,
     ApplicationAuditService audit,
     IOptions<BatteryPassOptions> options,
-    TimeProvider clock)
+    TimeProvider clock,
+    UploadService uploads)
 {
     public const string SeededEvent = "demo.seeded";
     public const string ClearedEvent = "demo.cleared";
@@ -176,7 +178,9 @@ public sealed class DemoDataService(
         var successors = outcomes.Sum(outcome => outcome.Successors);
         notes.AddRange(outcomes.SelectMany(outcome => outcome.Notes));
 
-        demoTokens.AddRange(await SeedTokensAsync(who, catalogue, cancellationToken));
+        var writeTokens = new Dictionary<string, string>(StringComparer.Ordinal);
+        demoTokens.AddRange(await SeedTokensAsync(who, catalogue, cancellationToken, writeTokens));
+        await SeedUploadsAsync(catalogue, writeTokens, cancellationToken);
 
         var done = new List<string>
         {
@@ -211,8 +215,11 @@ public sealed class DemoDataService(
         var removed = new Dictionary<string, int>(StringComparer.Ordinal);
         void Count(string what) => removed[what] = removed.GetValueOrDefault(what) + 1;
 
-        // Batteries first: nothing real can depend on a demo battery. Their versions and documents go with them.
-        foreach (var packId in await DemoIdsAsync(DppStore.Packs, cancellationToken))
+        // Batteries first: nothing real can depend on a demo battery. Their versions and documents go with them, and their
+        // kept upload checks in one delete (step 5d).
+        var demoPacks = await DemoIdsAsync(DppStore.Packs, cancellationToken);
+        await Collection(DppStore.UploadChecks).DeleteManyAsync(Builders<BsonDocument>.Filter.In("PackId", demoPacks), cancellationToken);
+        foreach (var packId in demoPacks)
         {
             await Collection(DppStore.Versions).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("PackId", packId), cancellationToken);
             await Collection(DppStore.HandOvers).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("PackId", packId), cancellationToken);
@@ -221,6 +228,9 @@ public sealed class DemoDataService(
             Count("batteries");
         }
 
+        // The demo uploads go with them (step 5d); a real upload naming a demo battery stays in the log.
+        await Collection(DppStore.Uploads).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("IsDemo", true), cancellationToken);
+
         await ClearUnlessUsedAsync<ProductionBatch>(DppStore.Batches, "batches", (DppStore.Packs, "BatchId"), "real batteries", TargetKind.Batch, notes, Count, cancellationToken);
         await ClearUnlessUsedAsync<EconomicOperator>(DppStore.Operators, "operators", (DppStore.Packs, "OperatorId"), "real batteries", null, notes, Count, cancellationToken);
         // A removed demo model takes its own picture with it (redesign R2); a kept one keeps it.
@@ -340,7 +350,7 @@ public sealed class DemoDataService(
 
     /// <summary>Each demo cluster's read and read-write tokens, and one team token with full access; values shown once.
     /// The cluster tokens are the cluster's own (autoClusterId), so the app's start adds no others for a demo cluster.</summary>
-    private async Task<List<DemoToken>> SeedTokensAsync(string who, DemoCatalogue catalogue, CancellationToken cancellationToken)
+    private async Task<List<DemoToken>> SeedTokensAsync(string who, DemoCatalogue catalogue, CancellationToken cancellationToken, Dictionary<string, string>? writeTokens = null)
     {
         var made = new List<DemoToken>();
         foreach (var cluster in catalogue.Clusters)
@@ -350,6 +360,10 @@ public sealed class DemoDataService(
                 var (document, value) = await tokens.CreateTokenAsync($"{cluster.Name} ({access.ToLowerInvariant()})", mode, [cluster.Id], false, false, who, autoClusterId: cluster.Id, cancellationToken: cancellationToken);
                 await MarkAccountRecordAsync("apiTokens", Builders<BsonDocument>.Filter.Eq("_id", document["_id"]), cancellationToken);
                 made.Add(new DemoToken($"{cluster.Name}", access, value));
+                if (mode == ExternalTokenAccessMode.ReadWrite && writeTokens is not null)
+                {
+                    writeTokens[cluster.Id] = document["_id"].ToString()!;
+                }
             }
         }
 
@@ -656,6 +670,39 @@ public sealed class DemoDataService(
         return Encoding.UTF8.GetBytes(export.ToJsonString());
     }
 
+    /// <summary>
+    /// The demo uploads (step 5d, Q25), after the tokens: for each battery the catalogue marks, an export from its
+    /// cluster's battery system (its read-write token) two hours ago, and a file of live values from its cluster admin half
+    /// an hour ago, through the one upload engine. Both are marked as demo data. No status change: the battery's demo
+    /// repair is 4b's.
+    /// </summary>
+    private async Task SeedUploadsAsync(DemoCatalogue catalogue, IReadOnlyDictionary<string, string> writeTokens, CancellationToken cancellationToken)
+    {
+        var now = clock.GetUtcNow().UtcDateTime;
+        foreach (var demo in catalogue.Packs.Where(pack => pack.Uploads && pack.Publish))
+        {
+            var admin = catalogue.Accounts.FirstOrDefault(account => account.Cluster == demo.Cluster && account.ClusterRole == "clusterAdmin");
+            var made = new List<string?>();
+            if (writeTokens.TryGetValue(demo.Cluster, out var tokenId))
+            {
+                var export = await uploads.RunAsync(ChangeSender.Feed($"api-token:{tokenId}", demo.Cluster), UploadSamples.Export(demo.Serial, now.AddHours(-2), demo.Modules), "api", null, preview: false, cancellationToken);
+                made.Add(export.UploadId);
+            }
+
+            if (admin is not null)
+            {
+                var file = JsonNode.Parse(UploadSamples.LiveValuesFile(demo.Serial, now.AddMinutes(-30), 71, 22));
+                var readings = await uploads.RunAsync(ChangeSender.ClusterAdmin(admin.Email, demo.Cluster), file, "page", "readings.json", preview: false, cancellationToken);
+                made.Add(readings.UploadId);
+            }
+
+            foreach (var id in made.OfType<string>())
+            {
+                await store.SetUploadDemoAsync(id, cancellationToken);
+            }
+        }
+    }
+
     private IMongoCollection<BsonDocument> Collection(string name) => Database.GetCollection<BsonDocument>(name);
 
     private async Task<List<string>> DemoIdsAsync(string collection, CancellationToken cancellationToken) =>
diff --git a/web/Dpp/Demo/demo-catalogue.json b/web/Dpp/Demo/demo-catalogue.json
index 70c7e36..32a0027 100644
--- a/web/Dpp/Demo/demo-catalogue.json
+++ b/web/Dpp/Demo/demo-catalogue.json
@@ -893,6 +893,7 @@
       "history": true,
       "modules": 7,
       "accident": "Coolant leak found at the 12-month service; module 4 replaced.",
+      "uploads": true,
       "status": {
         "what": "Repaired",
         "daysAgo": 10,
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index b65a23f..1162cd8 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -130,6 +130,10 @@ public sealed class DppStore
         }
     }
 
+    /// <summary>Marks an upload as the demo data's (decision 32); clearing the demo data removes it.</summary>
+    public Task SetUploadDemoAsync(string uploadId, CancellationToken cancellationToken = default) =>
+        Collection<UploadRecord>().UpdateOneAsync(upload => upload.Id == uploadId, Builders<UploadRecord>.Update.Set(upload => upload.IsDemo, true), cancellationToken: cancellationToken);
+
     /// <summary>The latest uploads, newest first.</summary>
     public async Task<IReadOnlyList<UploadRecord>> ListUploadsAsync(int limit, CancellationToken cancellationToken = default)
     {
diff --git a/web/Views/Help/Guide.cshtml b/web/Views/Help/Guide.cshtml
index 4518095..d4503a5 100644
--- a/web/Views/Help/Guide.cshtml
+++ b/web/Views/Help/Guide.cshtml
@@ -56,6 +56,15 @@
                         <li><strong><a href="/registry">Look after your batteries</a></strong><p>In the Registry, "Manage" opens a battery's page: correct its live values and add accident reports. Its modules and versions are shown there too; the team keeps the rest.</p></li>
                     </ol>
                 </section>
+                <section class="bp-card bp-help-part" id="upload">
+                    <h2>Upload a file <span class="bp-soft-tag">Team and cluster admins</span></h2>
+                    <p>On a battery's page, "Upload a file" takes one JSON file for that battery: the app's upload format, or the battery system's export as it is. You see what would change before anything is saved. A marked or earlier passport takes no file.</p>
+                    <ul>
+                        <li><a href="/help/samples/@BatteryPassWeb.Dpp.Uploads.UploadSamples.UploadFileName" download>An upload file</a>: live values and a repair, with one re-stated value, for the demo battery @BatteryPassWeb.Dpp.Uploads.UploadSamples.Serial. A status change goes in the same way: <code>"batteryStatus": "waste"</code> (or re-used, remanufactured, repurposed) among the values.</li>
+                        <li><a href="/help/samples/@BatteryPassWeb.Dpp.Uploads.UploadSamples.ExportFileName" download>A battery system export</a> (mapping BMS2_ACO) of the same demo battery, with made-up values.</li>
+                    </ul>
+                    <p class="bp-help-text mb-0">Status changes and repairs are made on the battery's page only; the API refuses them. Many batteries in one file go through the API's upload.</p>
+                </section>
             }
 
             @if (isTeam)
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
index 8a13931..c136722 100644
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -373,7 +373,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                     <div>
                         <span class="bp-summary-title">Uploads</span>
                         <h3>Send values for many batteries in one file.</h3>
-                        <p>The body is the upload file. Name each battery by <code>uniqueProductIdentifier</code> (its battery ID) or <code>batterySerialNumber</code>; <code>measuredAt</code> is when its values were measured, and <code>values</code> holds them by key, as on the live values call. A cluster token sends as its battery system, so only the live values of its own clusters' published batteries are saved; a team token may also set a battery's own values where the team's pages allow it. Each battery is saved or refused on its own, and each value is checked on its own. A battery the file names twice, or whose passport is no longer active, is refused. Up to @BatteryPassWeb.Dpp.Uploads.UploadService.MaxBatteries batteries and 2 MB per file. Add <code>?preview=true</code> to check without saving.</p>
+                        <p>The body is the upload file. Name each battery by <code>uniqueProductIdentifier</code> (its battery ID) or <code>batterySerialNumber</code>; <code>measuredAt</code> is when its values were measured, and <code>values</code> holds them by key, as on the live values call. A cluster token sends as its battery system, so only the live values of its own clusters' published batteries are saved; a team token may also set a battery's own values where the team's pages allow it. Each battery is saved or refused on its own, and each value is checked on its own. A battery the file names twice, or whose passport is no longer active, is refused, and so is one with a status change or a repair: @BatteryPassWeb.Dpp.Uploads.UploadService.StatusOnThePage. Up to @BatteryPassWeb.Dpp.Uploads.UploadService.MaxBatteries batteries and 2 MB per file. Add <code>?preview=true</code> to check without saving.</p>
                         <p>The body can also be the battery system's export as it is (mapping <code>@BatteryPassWeb.Dpp.Uploads.BmsExportFormat.Mapping</code>): one battery, found by its serial number, measured at <code>exportedAt</code>. The app takes the state of charge from its readable text as a whole percent, the full cycles, and the times in extreme temperatures as whole minutes (E1 to E4), and sets the battery's module list. A manufacturing date in the export only fills a draft without one, sent by the team; otherwise a note says it isn't used. The remaining capacity is kept to suggest the capacity fade when the status changes. Each battery's <code>notes</code> say what the export lacks, such as the pack temperature.</p>
                     </div>
                     <pre class="bp-help-pre"><code>POST @Model.BasePath/uploads
```

`web/Dpp/Uploads/UploadSamples.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>
/// Sample files for "Upload a file" (decision 35, Q24 and Q25): an upload file in the app's format and a battery system
/// export (mapping BMS2_ACO), for a demo battery only. The help page offers them to the team and cluster admins; the
/// demo data sends them as its two demo uploads. The export's modules are named as the demo seed names them, so it
/// leaves a demo battery's module list as it is.
/// </summary>
public static class UploadSamples
{
    /// <summary>The demo battery the help page's samples are for.</summary>
    public const string Serial = "DEMO-C7M-0001";

    public const string UploadFileName = "upload-file-DEMO-C7M-0001.json";

    public const string ExportFileName = "battery-system-export-DEMO-C7M-0001.json";

    /// <summary>The help page's upload file: live values and a repair with one re-stated value (design 8.2).</summary>
    public static string UploadFile(DateTime measuredAtUtc) => new JsonObject
    {
        ["format"] = NativeUploadFormat.Format,
        ["batteries"] = new JsonArray(new JsonObject
        {
            ["batterySerialNumber"] = Serial,
            ["measuredAt"] = Stamp(measuredAtUtc),
            ["reason"] = "repair",
            ["repairDescription"] = "Cell module 2 replaced after a cooling fault.",
            ["values"] = new JsonObject
            {
                ["stateOfChargeSoC"] = 64,
                ["temperatureInformation"] = 22,
                ["capacityFade"] = 7,
            },
        }),
    }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    /// <summary>An upload file of live values only, as a cluster admin might send one (the demo data's second upload).</summary>
    public static string LiveValuesFile(string serial, DateTime measuredAtUtc, int stateOfCharge, int temperature) => new JsonObject
    {
        ["format"] = NativeUploadFormat.Format,
        ["batteries"] = new JsonArray(new JsonObject
        {
            ["batterySerialNumber"] = serial,
            ["measuredAt"] = Stamp(measuredAtUtc),
            ["values"] = new JsonObject { ["stateOfChargeSoC"] = stateOfCharge, ["temperatureInformation"] = temperature },
        }),
    }.ToJsonString();

    /// <summary>
    /// A battery system export of one battery, with made-up values in the shape of mapping BMS2_ACO 01.32.07: the state of
    /// charge as its readable text, the cycles, the extreme-temperature times in seconds, the remaining capacity, and the
    /// module list as the demo seed names it.
    /// </summary>
    public static JsonObject Export(string serial, DateTime exportedAtUtc, int modules)
    {
        var children = new JsonArray();
        for (var position = 1; position <= modules; position++)
        {
            children.Add(new JsonObject
            {
                ["serialNumber"] = $"{serial}-M{position:D2}",
                ["connectivityId"] = $"CAN-{serial}-{position:D2}",
                ["position"] = position,
            });
        }

        static JsonObject Value(JsonNode raw, JsonNode formatted) => new() { ["raw"] = raw, ["formatted"] = formatted };
        var produced = new JsonArray(Encoding.ASCII.GetBytes("2026-07").Select(code => (JsonNode)JsonValue.Create((int)code)!).ToArray());
        return new JsonObject
        {
            ["version"] = 1,
            ["metadata"] = new JsonObject
            {
                ["exportedAt"] = Stamp(exportedAtUtc),
                ["identifiers"] = new JsonObject
                {
                    ["deviceIdentifiers"] = new JsonObject { ["serialNumber"] = serial, ["position"] = 1, ["childrenIdentifiers"] = children },
                },
                ["mappingName"] = BmsExportFormat.Mapping,
                ["mappingVersion"] = BmsExportFormat.CheckedVersion,
            },
            ["values"] = new JsonObject
            {
                ["remainingCapacityAh"] = Value(131, "131.0 Ah"),
                ["productionDate"] = Value(produced, "2026-07"),
                ["equivalentChargeAndDischargeCycles"] = Value(418.6, "418.6"),
                ["timeSpentInExtremeHighTemperature"] = Value(0, "0.000 s"),
                ["timeSpentInExtremeLowTemperature"] = Value(0, "0.000 s"),
                ["timeSpentChargingInExtremeTemperature"] = Value(0, "0.000 s"),
                ["stateOfCharge"] = Value(47616, "74.4 %"),
            },
        };
    }

    private static string Stamp(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~StatusUploadTests | FullyQualifiedName~Tests.Demo. | FullyQualifiedName~Look.HelpTests | FullyQualifiedName~HelpWorkbenchLayoutTests | FullyQualifiedName~DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:    94`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoClearTests.cs BatteryPassWeb.Tests/Demo/DemoSeedTests.cs BatteryPassWeb.Tests/Dpp/StatusUploadTests.cs web/Controllers/HelpController.cs web/Dpp/Demo/DemoCatalogue.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/Demo/demo-catalogue.json web/Dpp/Store/DppStore.cs web/Dpp/Uploads/UploadSamples.cs web/Views/Help/Guide.cshtml web/Views/Help/Index.cshtml
git commit -m "feat(dpp): the help's sample upload files and DEMO-C7M-0001's demo uploads" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2499, Skipped:     0, Total:  2499`. From the 2481 before: 18 new tests.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 86dc2a7 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. The managing session brings them to the product owner:

1. **Restart the app with the usual launcher**, sign in with a team account and press "Reset demo data" at the end of the Clusters tab.
2. **As the team, DEMO-C7M-0001's page** (Batteries, then the battery), "Upload a file" in the side menu: Earlier uploads lists "Battery system · battery system export (BMS2_ACO 01.32.07)", about 2 hours ago, and "demo.north.admin@example.test · readings.json", about 30 minutes ago, the newest first.
3. **Help** (in the menu), the part "Upload a file": download "An upload file". Back on DEMO-C7M-0001's "Upload a file", choose it and press "Check the file":
   - the line "Repaired on … : “Cell module 2 replaced after a cooling fault.” The status becomes re-used, with a new version.";
   - capacity fade, state of charge and temperature "Will be saved"; the other re-stated values in grey, "Kept: the battery's current value".
   - "Save": "Saved. Repaired on … The status became re-used, with a new version.", and the upload open first, "Status changed".
   - "Status" in the side menu shows re-used and the repair. The audit log (and the cluster admin's History) has "Battery DEMO-C7M-0001 repaired: Cell module 2 replaced after a cooling fault. Status re-used.".
4. **The same file without its `repairDescription` line:** "Say what was repaired.", nothing would change, and no "Save".
5. **As demo-north's cluster admin** (`demo.north.admin@example.test`): the same two demo uploads on DEMO-C7M-0001's page ("Manage" on the Registry), and the help's two downloads. As a cluster user (`demo.north.user@example.test`): no "Upload a file" part in the help.
6. **Light and dark, and a phone:** the moon button in the top bar switches; the check fits a phone's width.

## Next

Step 5's wrap-up note.
