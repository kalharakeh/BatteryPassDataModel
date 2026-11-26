# DPP V3 Step 5b: The Battery System's Export — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The second part of step 5 (design 8.3; decisions 17, 18, 28, 31 and 35). The battery system's JSON export (mapping BMS2_ACO) goes through the same upload as the app's own format, on the API's upload endpoint today and on 5c's upload page:
- **The live values in the passport's units:** #63 from the export's readable state of charge ("27.1 %") as a whole percent; #83 as full cycles completed; E1–E4 from seconds to whole minutes, rounded down (decision 28). A charging time above zero can't be split into above and below the boundary, so E3 and E4 stay as they are, with a note.
- **The module list** from `metadata.identifiers.deviceIdentifiers.childrenIdentifiers` replaces the battery's (decision 31), whoever sends it; an empty list never overwrites, and an unchanged one isn't saved again.
- **#17** from the export only fills a draft without a date, sent by the team; otherwise the battery's notes say "the manufacturing date in the file isn't used" (no refusal).
- **Notes** tell the sender what the export lacks: no pack temperature yet (#87), another mapping version.
- **The remaining capacity** is kept as app data and starts the status window's capacity-fade suggestion (J-11).

**Architecture:**
- **`Dpp/Uploads/BmsExportFormat.cs`:** a second `IUploadAdapter`, chosen when the file has `metadata.mappingName`. Another mapping is refused as a whole; another BMS2_ACO version is read the same way, with a note. One export gives one entry: the serial number from `metadata.identifiers.deviceIdentifiers.serialNumber`, `measuredAt` from `metadata.exportedAt`, the values, the module list (`ModuleList.FromDevice`, split out of `FromExport`), notes, and the keys that only fill a value the battery hasn't got (`FillOnly`: #17).
- **`UploadService`** uses the export's entries like any other:
  - It leaves out a fill-only value unless the battery is a draft without it and the sender may change it (`EditRights`), and notes it instead.
  - It saves the module list beside the pipeline (app data, no new version; logged as `dpp.pack-modules-set`).
  - It reports per battery what happened to the module list and the adapter's notes, in its answer and in the upload log.
- **The remaining capacity** (task 2): `BatteryPack.RemainingCapacity` (Ah and measurement time), set only when it is newer than the one kept; `StatusCardView.Remaining` fills the "Change status" window's box and says when it was measured. The window is in `_StatusDialog.cshtml`, where the redesign (R4) put the status windows at page level.
- **The API's answer** gains `modules` and `notes` per battery; the help page describes the export.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, section 8.3 (corrected by this plan's commit) and section 14's step-5 note. Decisions record: decisions 17, 18, 28, 31 and 35. Plan 5a: `docs/superpowers/plans/2026-11-02-dpp-v3-step5a-upload-engine.md`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 6f2a982 (plan 5a's run and the battery-page redesign R1–R7), plus the commit that adds this plan. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo). No new collection: the remaining capacity is a new field on the battery records, empty until an export brings one.
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Each task runs only its own tests** (the filter in its steps); **the whole suite runs once**, in the final checks. Never run two whole suites at once.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started (by their process IDs, never by name) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2459`. After this plan: 2469. The suite takes about four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../5b-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (2 Nov 2026, through the managing session)

Decision 35 holds step 5's answers; this plan's commit adds 5b's and corrects design 8.3. The ones 5b builds on:

1. **Q2: #63 from the export's readable text** ("27.1 %"), rounded to a whole percent: the raw number (17323 in the sample of 28 Oct) is on a scale the export team is asked to name.
2. **Q3 and Q14: E1–E4 and #83 are rounded down:** whole minutes and full cycles completed, never more than measured.
3. **Q4: an export's module list replaces the battery's, whoever sends it** (a cluster token too: it is app data, so J-10 holds). An empty list never overwrites.
4. **Q5 and Q16: the latest remaining capacity is kept as app data** and starts the status window's fade suggestion, in this plan; no new mock-up.
5. **Q12: any BMS2_ACO version is read**, with a note when it isn't 01.32.07.
6. **Q15: the export's #17 fills the date only when the sender may set it (the team, by page or a global token) and the battery is a draft without one.** Otherwise it is left out quietly, with one note, "the manufacturing date in the file isn't used": the save rules aren't bent for uploads.
7. **Q17: design 8.3's table is corrected** to match the sample exports (this plan's commit).

## Found while prototyping, and what this plan does

- **The sample exports differ from design 8.3:** the serial number and export time are under `metadata` (`metadata.identifiers.deviceIdentifiers.serialNumber`, `metadata.exportedAt`), the raw dates are character codes (`[50, 48, 50, 54, 45, 49, 48]` is "2026-10") and an unset date is seven zeros, and the times are seconds. The adapter reads the readable text for #63, #17 and the remaining capacity, and the raw seconds and cycles.
- **The newer sample's modules have connectivity IDs but no serial numbers;** the module list already accepts one of the two.
- **The export's #87 field doesn't exist yet** (design 8.5, item 3): every export's notes say so until the export team adds it.
- **A number set in code** (rather than parsed) is read through its JSON text, so the adapter never mistakes an integer for something else.
- **Updated onto the battery-page redesign (R1–R7, 3 Nov 2026):** the code is as written before, except that the remaining capacity fills the box in `_StatusDialog.cshtml` (R4 moved the "Change status" window there). An upload still refuses a passport that is no longer active (marked invalid or for deletion, or an earlier passport replaced by a new one) as a whole, so neither its values nor its module list nor its remaining capacity change there. The upload's audit entries read in the redesign's History: the live values of an export join the run of "the latest of N live readings" (the upload's own `dpp.upload` entry is the upload's, not the battery's), and a changed module list reads "Modules of battery … set from the battery system's export: 8 modules".

## What happens to the old tests

Nothing is removed.
- **`UploadServiceTests`** builds its service with both adapters (Task 1).
- **`StatusPagesTests`** runs unchanged beside the new remaining-capacity test (Task 2).

## A part to write yourself (optional)

The product owner chose "you write it" before; the plan gives the code. If they want to try one, the natural spot is `BmsExportFormat.Read` in Task 1: how each export field becomes a passport value. `AFilledExport_ShouldGiveTheLiveValuesInTheSchemasUnits` and `ChargingInExtremeTemperatures_ShouldLeaveE3AndE4AsTheyAreAndSaySo` list it.

---

### Task 1: The battery system's export

The battery system's JSON export (mapping BMS2_ACO, design 8.3) is read by its own adapter, beside the native format, on the same upload: one battery, found by its serial number, measured at `exportedAt`. #63 comes from the readable text as a whole percent, #83 and E1–E4 are rounded down (E1–E4 from seconds to minutes; a charging time above zero leaves E3 and E4 as they are, with a note), and the module list replaces the battery's (an empty one never does). The export's #17 only fills the team's draft that has none; otherwise a note says it isn't used. Each battery's notes say what the export lacks, such as #87.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs`
- Modify: `web/Controllers/DppApiController.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Pages/ModuleList.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Create: `web/Dpp/Uploads/BmsExportFormat.cs`
- Modify: `web/Dpp/Uploads/UploadFile.cs`
- Modify: `web/Dpp/Uploads/UploadService.cs`
- Modify: `web/Views/Help/Index.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `5b-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs`). Save the block as `../5b-task1-tests.patch`, outside the worktree, and run `git apply ../5b-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs b/BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs
index 1b18922..611b861 100644
--- a/BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs
@@ -190,7 +190,7 @@ public sealed class UploadServiceTests(MongoFixture mongo)
     }
 
     internal static UploadService Service(PublicationRig rig) =>
-        new(rig.Store, rig.Pipeline, BatteryApiTests.Api(rig), new ApplicationAuditService(rig.Context), rig.Clock, [new NativeUploadFormat()]);
+        new(rig.Store, rig.Pipeline, BatteryApiTests.Api(rig), new ApplicationAuditService(rig.Context), rig.Clock, [new NativeUploadFormat(), new BmsExportFormat()]);
 
     internal static Task<UploadReport> Run(PublicationRig rig, ChangeSender sender, string file, bool preview = false) =>
         Service(rig).RunAsync(sender, JsonNode.Parse(file), "page", "batteries.json", preview);
```

`BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The battery system's export, mapping BMS2_ACO (design 8.3, decisions 17, 18, 28 and 31; step 5b): the live values in
/// the schema's units, E1-E4 in whole minutes, #17 for the team, and the module list, through the same upload engine.
/// The export here is made up, in the shape of the samples from 27 and 28 Oct 2026.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BmsExportUploadTests(MongoFixture mongo)
{
    private static readonly BmsExportFormat Adapter = new();

    [Fact]
    public void AFilledExport_ShouldGiveTheLiveValuesInTheSchemasUnits()
    {
        var entry = Assert.Single(Read(Export("VPC9-0001")).Entries);

        Assert.Equal(("VPC9-0001", new DateTime(2026, 10, 28, 7, 40, 26, 319, DateTimeKind.Utc)), (entry.NamedAs, entry.MeasuredAtUtc));
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["stateOfChargeSoC"] = "27",
                ["numberOfFullChargingAndDischargingCycles"] = "12",
                ["timeSpentInExtremeTemperaturesAboveBoundary"] = "59",
                ["timeSpentInExtremeTemperaturesBelowBoundary"] = "121",
                ["timeSpentChargingDuringExtremeTemperaturesAboveBoundary"] = "0",
                ["timeSpentChargingDuringExtremeTemperaturesBelowBoundary"] = "0",
                ["manufacturingDate"] = "\"2026-03\"",
            },
            entry.Values.ToDictionary(pair => pair.Key, pair => pair.Value!.ToJsonString()));
        Assert.Equal([(1, null, "MD-A1"), (2, "MD-2", "MD-A2")], entry.Modules!.Select(module => (module.Position, module.SerialNumber, module.ConnectivityId)));
        Assert.Equal([BmsExportFormat.NoTemperature], entry.Notes);
        Assert.Equal(["manufacturingDate"], entry.FillOnly);
    }

    [Fact]
    public void AnExportWithEmptyValues_ShouldGiveNoValuesButItsModules()
    {
        var export = Export("VPC9-0002");
        foreach (var (_, value) in (JsonObject)export["values"]!)
        {
            value!["raw"] = null;
            value["formatted"] = null;
        }

        var entry = Assert.Single(Read(export).Entries);

        Assert.Empty(entry.Values);
        Assert.Equal(2, entry.Modules!.Count);
    }

    [Fact]
    public void ChargingInExtremeTemperatures_ShouldLeaveE3AndE4AsTheyAreAndSaySo()
    {
        var export = Export("VPC9-0003");
        export["values"]!["timeSpentChargingInExtremeTemperature"]!["raw"] = 30;

        var entry = Assert.Single(Read(export).Entries);

        Assert.DoesNotContain("timeSpentChargingDuringExtremeTemperaturesAboveBoundary", entry.Values.Keys);
        Assert.DoesNotContain("timeSpentChargingDuringExtremeTemperaturesBelowBoundary", entry.Values.Keys);
        Assert.Contains(BmsExportFormat.ChargingAboveZero, entry.Notes!);
    }

    [Fact]
    public void AnUnsetProductionDateOrAnUnreadableStateOfCharge_ShouldBeLeftOut()
    {
        var export = Export("VPC9-0004");
        export["values"]!["productionDate"] = JsonNode.Parse("""{ "raw": [0, 0, 0, 0, 0, 0, null], "formatted": [0, 0, 0, 0, 0, 0, null] }""");
        export["values"]!["stateOfCharge"]!["formatted"] = "17323";

        var entry = Assert.Single(Read(export).Entries);

        Assert.DoesNotContain("manufacturingDate", entry.Values.Keys);
        Assert.DoesNotContain("stateOfChargeSoC", entry.Values.Keys);
        Assert.Contains("the export's state of charge \"17323\" isn't a percentage, so #63 is left as it is", entry.Notes!);
    }

    [Fact]
    public void AnotherMappingOrVersion_ShouldBeRefusedOrNoted()
    {
        var other = Export("VPC9-0005");
        other["metadata"]!["mappingName"] = "BMS3_XYZ";
        var newer = Export("VPC9-0005");
        newer["metadata"]!["mappingVersion"] = "01.40.00";
        var anonymous = Export("VPC9-0005");
        anonymous["metadata"]!["identifiers"]!["deviceIdentifiers"]!["serialNumber"] = "";

        Assert.Equal("the export's mapping \"BMS3_XYZ\" isn't one this app reads; it reads BMS2_ACO", Read(other).Problem);
        Assert.Equal("BMS2_ACO 01.40.00", Read(newer).Format);
        Assert.Contains("read as mapping version 01.32.07, the one this app was checked against", Assert.Single(Read(newer).Entries).Notes!);
        Assert.Equal("the export has no serial number (metadata.identifiers.deviceIdentifiers.serialNumber)", Assert.Single(Read(anonymous).Entries).Problem);
    }

    [Fact]
    public async Task ABatterySystemsExport_ShouldSaveItsLiveValuesAndModulesThroughTheApi()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens["north-write"])
            .Upload(JsonDocument.Parse(Export(UploadServiceTests.Serial(rig)).ToJsonString()).RootElement.Clone(), false, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, answer.StatusCode);
        var data = DppApiControllerTests.Json(answer).GetProperty("data");
        Assert.Equal("BMS2_ACO 01.32.07", data.GetProperty("format").GetString());
        var battery = Assert.Single(data.GetProperty("batteries").EnumerateArray());
        var outcomes = battery.GetProperty("values").EnumerateArray().ToDictionary(value => value.GetProperty("key").GetString()!, value => value.GetProperty("outcome").GetString());
        Assert.DoesNotContain("manufacturingDate", outcomes.Keys); // the battery has one: left out, not refused (Q15)
        Assert.Equal(6, outcomes.Count);
        Assert.All(outcomes, pair => Assert.Equal("saved", pair.Value));
        Assert.Equal("the module list was set: 2 modules", battery.GetProperty("modules").GetString());
        Assert.Equal([BmsExportFormat.NoTemperature, BmsExportUploadTests.DateNotUsed], battery.GetProperty("notes").EnumerateArray().Select(note => note.GetString()));

        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal((27, "feed"), (pack.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>(), pack.Values["stateOfChargeSoC"].Source));
        Assert.Equal(59, pack.Values["timeSpentInExtremeTemperaturesAboveBoundary"].Value!.GetValue<int>());
        Assert.Equal(new DateTime(2026, 10, 28, 7, 40, 26, 319, DateTimeKind.Utc), pack.Values["timeSpentInExtremeTemperaturesAboveBoundary"].GeneratedAtUtc);
        Assert.Equal(["MD-A1", "MD-A2"], pack.Modules.Select(module => module.ConnectivityId));
        var logged = Assert.Single(Assert.Single(await rig.Store.ListUploadsAsync(10)).Batteries);
        Assert.Equal("the module list was set: 2 modules", logged.Modules);
        Assert.Equal([BmsExportFormat.NoTemperature, DateNotUsed], logged.Notes);
        Assert.Equal("Modules of battery VPC8C05-6788 set from the battery system's export: 2 modules", (await LastAsync(rig, BatteryPages.ModulesSetEvent))["message"].AsString);
    }

    [Fact]
    public async Task TheTeamsExportOfADraftWithoutADate_ShouldAloneFillTheManufacturingDate()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var kept = await Run(rig, PublicationTesting.Team, Export(UploadServiceTests.Serial(rig)));
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), ["manufacturingDate"]);
        var clusterAdmin = await Run(rig, ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), Export(UploadServiceTests.Serial(rig)), preview: true);

        var filled = await Run(rig, PublicationTesting.Team, Export(UploadServiceTests.Serial(rig)));

        Assert.DoesNotContain(Assert.Single(kept.Batteries).Values, value => value.Key == "manufacturingDate");
        Assert.Contains(DateNotUsed, Assert.Single(kept.Batteries).Notes!);
        Assert.Equal(UploadService.NotFound, Assert.Single(clusterAdmin.Batteries).Problem); // a draft is the team's (J-6)
        Assert.Equal(KeyOutcome.Saved, Assert.Single(Assert.Single(filled.Batteries).Values, value => value.Key == "manufacturingDate").Outcome);
        Assert.DoesNotContain(DateNotUsed, Assert.Single(filled.Batteries).Notes!);
        Assert.Equal("2026-03", (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["manufacturingDate"].Value!.GetValue<string>());
    }

    [Fact]
    public async Task APublishedBatteryWithoutADate_ShouldNotTakeTheExportsDate()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), ["manufacturingDate"]);

        var report = await Run(rig, PublicationTesting.Team, Export(UploadServiceTests.Serial(rig)), preview: true);

        Assert.DoesNotContain(Assert.Single(report.Batteries).Values, value => value.Key == "manufacturingDate");
        Assert.Contains(DateNotUsed, Assert.Single(report.Batteries).Notes!);
    }

    internal const string DateNotUsed = "the manufacturing date in the file isn't used";

    [Fact]
    public async Task TheSameModulesAgain_ShouldBeLeftAsTheyAreAndAPreviewShouldSaveNone()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var feed = ChangeSender.Feed("north-feed", BatteryTesting.North);

        var preview = await Run(rig, feed, Export(UploadServiceTests.Serial(rig)), preview: true);
        Assert.Equal("the module list would be set: 2 modules", Assert.Single(preview.Batteries).Modules);
        Assert.Empty((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Modules);

        await Run(rig, feed, Export(UploadServiceTests.Serial(rig)));
        var again = await Run(rig, feed, Export(UploadServiceTests.Serial(rig)));

        Assert.Equal("the module list is unchanged", Assert.Single(again.Batteries).Modules);
        Assert.Equal(1, await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("eventType", BatteryPages.ModulesSetEvent)));
    }

    /// <summary>A made-up export of one battery, in the shape of mapping BMS2_ACO 01.32.07.</summary>
    internal static JsonObject Export(string serial) => JsonNode.Parse($$"""
        {
          "version": 1,
          "metadata": {
            "exportedAt": "2026-10-28T07:40:26.319Z",
            "identifiers": {
              "deviceIdentifiers": {
                "serialNumber": "{{serial}}",
                "connectivityId": "ST-TEST",
                "position": 1,
                "childrenIdentifiers": [
                  { "serialNumber": "", "connectivityId": "MD-A1", "position": 1 },
                  { "serialNumber": "MD-2", "connectivityId": "MD-A2", "position": 2 }
                ]
              },
              "parentIdentifiers": { "serialNumber": "SYSTEM-TEST", "connectivityId": "SY-TEST" }
            },
            "address": "192.0.2.10",
            "mappingName": "BMS2_ACO",
            "mappingVersion": "01.32.07"
          },
          "values": {
            "remainingCapacityAh": { "raw": 126, "formatted": "126.0 Ah" },
            "productionDate": { "raw": [50, 48, 50, 54, 45, 48, 51], "formatted": "2026-03" },
            "totalChargedEnergyKwh": { "raw": 174, "formatted": "174.0 kWh" },
            "equivalentChargeAndDischargeCycles": { "raw": 12.7, "formatted": "12.7" },
            "timeSpentInExtremeHighTemperature": { "raw": 3599, "formatted": "3.599 ks" },
            "timeSpentInExtremeLowTemperature": { "raw": 7260, "formatted": "7.260 ks" },
            "timeSpentChargingInExtremeTemperature": { "raw": 0, "formatted": "0.000 s" },
            "stateOfCharge": { "raw": 17323, "formatted": "27.1 %" },
            "canPayloadBase64": { "raw": [126, 0], "formatted": "fgA=" }
          },
          "exportee": { "name": "Test Person", "email": "test.person@example.com" }
        }
        """)!.AsObject();

    private static UploadDocument Read(JsonObject export)
    {
        Assert.True(Adapter.Reads(export));
        return Adapter.Read(export);
    }

    private static Task<UploadReport> Run(PublicationRig rig, ChangeSender sender, JsonObject export, bool preview = false) =>
        UploadServiceTests.Service(rig).RunAsync(sender, export, "page", "export.json", preview);

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BmsExportUploadTests | FullyQualifiedName~UploadServiceTests | FullyQualifiedName~DppApiUploadTests"`
Expected: the build fails with CS0246 (for example `error CS0246 The type or namespace name 'BmsExportFormat' could not be found`).

- [ ] **Step 3: Write the code**

Patch `5b-task1-code.patch` (`web/Controllers/DppApiController.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Pages/ModuleList.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Uploads/UploadFile.cs`, `web/Dpp/Uploads/UploadService.cs`, `web/Views/Help/Index.cshtml`). Save the block as `../5b-task1-code.patch`, outside the worktree, and run `git apply ../5b-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/DppApiController.cs b/web/Controllers/DppApiController.cs
index 2a2ca37..04d0619 100644
--- a/web/Controllers/DppApiController.cs
+++ b/web/Controllers/DppApiController.cs
@@ -337,7 +337,8 @@ public sealed class DppApiController(ExternalApiRepository tokens, ClusterReposi
     }
 
     /// <summary>
-    /// An upload of many batteries (design 8.1, step 5a): the native format, through the one save pipeline. A cluster
+    /// An upload of many batteries (design 8.1, steps 5a and 5b): the native format or the battery system's export
+    /// (mapping BMS2_ACO), through the one save pipeline. A cluster
     /// token sends as its battery system, so only live values are saved (J-10); a global token sends as the team.
     /// </summary>
     [HttpPost("uploads")]
@@ -374,6 +375,8 @@ public sealed class DppApiController(ExternalApiRepository tokens, ClusterReposi
                     outcome = battery.Outcome,
                     problem = battery.Problem,
                     values = Keys(battery.Values),
+                    modules = battery.Modules,
+                    notes = battery.Notes ?? [],
                 }).ToArray(),
             });
     }
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index f909db4..2922de4 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -57,6 +57,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<PassportApi>();
         services.AddSingleton<BatteryApi>();
         services.AddSingleton<IUploadAdapter, NativeUploadFormat>();
+        services.AddSingleton<IUploadAdapter, BmsExportFormat>();
         services.AddSingleton<UploadService>();
         services.AddSingleton<PassportPages>();
         services.AddSingleton<AuditLines>();
diff --git a/web/Dpp/Pages/ModuleList.cs b/web/Dpp/Pages/ModuleList.cs
index 09de16e..da0e5f8 100644
--- a/web/Dpp/Pages/ModuleList.cs
+++ b/web/Dpp/Pages/ModuleList.cs
@@ -81,6 +81,13 @@ public static class ModuleList
             return ([], $"the file is for battery {exported ?? "without a serial number"}, not {batterySerialNumber}");
         }
 
+        return FromDevice(device);
+    }
+
+    /// <summary>The modules an export lists under one device (its <c>childrenIdentifiers</c>), used by uploads (step 5b).</summary>
+    public static (IReadOnlyList<BatteryModule> Modules, string? Problem) FromDevice(JsonObject device)
+    {
+        ArgumentNullException.ThrowIfNull(device);
         if (device["childrenIdentifiers"] is not JsonArray children || children.Count == 0)
         {
             return ([], "the file lists no modules");
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index e1e8247..577c5cd 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -281,6 +281,12 @@ public sealed class UploadedBattery
     public string? Problem { get; init; }
 
     public List<UploadedValue> Values { get; init; } = [];
+
+    /// <summary>What happened to the battery's module list, when the file had one (step 5b).</summary>
+    public string? Modules { get; init; }
+
+    /// <summary>What the file's adapter wanted the sender to know, e.g. a value it couldn't read (step 5b).</summary>
+    public List<string> Notes { get; init; } = [];
 }
 
 /// <summary>One value of an upload: its key, its outcome ("Saved", "KeptEmpty", "Ignored", "Rejected") and why.</summary>
diff --git a/web/Dpp/Uploads/UploadFile.cs b/web/Dpp/Uploads/UploadFile.cs
index d737bff..60c7e6f 100644
--- a/web/Dpp/Uploads/UploadFile.cs
+++ b/web/Dpp/Uploads/UploadFile.cs
@@ -1,6 +1,7 @@
 using System.Globalization;
 using System.Text.Json;
 using System.Text.Json.Nodes;
+using BatteryPassWeb.Dpp.Store;
 
 namespace BatteryPassWeb.Dpp.Uploads;
 
@@ -12,12 +13,18 @@ namespace BatteryPassWeb.Dpp.Uploads;
 /// <param name="NamedAs">The battery ID (#6) or serial number (#11) the file names the battery by.</param>
 /// <param name="MeasuredAtUtc">When every value of the entry was generated; the upload time when null.</param>
 /// <param name="Problem">Why the entry can't be used as it is; null when it can.</param>
+/// <param name="Modules">The battery's module list, when the file has one (the battery system's export, decision 31).</param>
+/// <param name="Notes">What the file's adapter wants the sender to know, e.g. a value it couldn't read.</param>
+/// <param name="FillOnly">Keys that only fill a battery without a value, left out silently otherwise (the export's #17).</param>
 public sealed record UploadEntry(
     int Number,
     string NamedAs,
     DateTime? MeasuredAtUtc,
     IReadOnlyDictionary<string, JsonNode?> Values,
-    string? Problem = null);
+    string? Problem = null,
+    IReadOnlyList<BatteryModule>? Modules = null,
+    IReadOnlyList<string>? Notes = null,
+    IReadOnlyCollection<string>? FillOnly = null);
 
 /// <summary>A file read by its format's adapter: the format, its entries, or why it can't be read at all.</summary>
 public sealed record UploadDocument(string? Format, IReadOnlyList<UploadEntry> Entries, string? Problem = null)
diff --git a/web/Dpp/Uploads/UploadService.cs b/web/Dpp/Uploads/UploadService.cs
index 140fdf0..0b5774b 100644
--- a/web/Dpp/Uploads/UploadService.cs
+++ b/web/Dpp/Uploads/UploadService.cs
@@ -1,7 +1,9 @@
 using System.Text.Json;
 using System.Text.Json.Nodes;
 using BatteryPassWeb.Dpp.Api;
+using BatteryPassWeb.Dpp.Catalog;
 using BatteryPassWeb.Dpp.Editing;
+using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Store;
 using BatteryPassWeb.Services;
 using MongoDB.Bson;
@@ -11,6 +13,8 @@ namespace BatteryPassWeb.Dpp.Uploads;
 /// <summary>What happened to one battery of an upload.</summary>
 /// <param name="Outcome">One of <see cref="UploadService.Saved"/>, <see cref="UploadService.NothingSaved"/> or <see cref="UploadService.Refused"/>.</param>
 /// <param name="Problem">Why the battery was refused as a whole; null when it wasn't.</param>
+/// <param name="Modules">What happened to the battery's module list, when the file had one; null otherwise.</param>
+/// <param name="Notes">What the file's adapter wants the sender to know, e.g. a value it couldn't read.</param>
 public sealed record UploadedBatteryReport(
     int Entry,
     string NamedAs,
@@ -18,7 +22,9 @@ public sealed record UploadedBatteryReport(
     string? PackId,
     string Outcome,
     string? Problem,
-    IReadOnlyList<KeyResult> Values);
+    IReadOnlyList<KeyResult> Values,
+    string? Modules = null,
+    IReadOnlyList<string>? Notes = null);
 
 /// <summary>An upload's result: per battery what happened, or why the file couldn't be read at all.</summary>
 /// <param name="UploadId">The log entry; null for a preview or an unreadable file, which aren't logged.</param>
@@ -50,7 +56,7 @@ public sealed class UploadService(
     public const string NothingSaved = "nothing saved";
     public const string Refused = "refused";
 
-    public const string NotAnUpload = "the file isn't an upload this app reads: give \"format\": \"dpp-v3-upload/1\" and the batteries";
+    public const string NotAnUpload = "the file isn't an upload this app reads: give \"format\": \"dpp-v3-upload/1\" and the batteries, or send the battery system's export (mapping BMS2_ACO)";
     public const string NotFound = "no battery you may change has this ID";
     public const string NotYours = "this battery belongs to another cluster";
     public const string Ambiguous = "more than one battery has this serial number; use the battery ID";
@@ -104,22 +110,26 @@ public sealed class UploadService(
         foreach (var (entry, target, problem) in found)
         {
             var pack = target?.Pack;
+            var notes = entry.Notes ?? [];
             if (problem is not null || pack is null || twice.Contains(pack.Id))
             {
-                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target?.Identifier, pack?.Id, Refused, problem ?? NamedTwice, []));
+                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target?.Identifier, pack?.Id, Refused, problem ?? NamedTwice, [], Notes: notes));
                 continue;
             }
 
             if (await store.PassportStatusAsync(pack.Id, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
             {
-                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, SavePipeline.NoLongerChanges(status), []));
+                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, SavePipeline.NoLongerChanges(status), [], Notes: notes));
                 continue;
             }
 
-            var source = sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload";
-            var result = await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, entry.Values, source, entry.MeasuredAtUtc), preview, cancellationToken);
-            var outcome = result.Keys.Any(key => key.Outcome == KeyOutcome.Saved) ? Saved : NothingSaved;
-            reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, result.Keys));
+            var source = Source(sender);
+            var (values, unused) = await FillOnlyAsync(sender, pack, entry, cancellationToken);
+            notes = [.. notes, .. unused];
+            var result = await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, values, source, entry.MeasuredAtUtc), preview, cancellationToken);
+            var modules = await ModulesAsync(sender, pack, entry.Modules, preview, cancellationToken);
+            var outcome = result.Keys.Any(key => key.Outcome == KeyOutcome.Saved) || modules is { Changed: true } ? Saved : NothingSaved;
+            reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, result.Keys, modules?.Words, notes));
         }
 
         string? uploadId = null;
@@ -131,6 +141,75 @@ public sealed class UploadService(
         return new UploadReport(document.Format, preview, reports, null, uploadId);
     }
 
+    /// <summary>
+    /// The entry's values without the fill-only ones it may not use (decision 35, Q15): such a value (the export's #17)
+    /// fills a draft without one, from a sender who may set it. Otherwise it is left out with a note, not refused, so
+    /// the pipeline's rules aren't bent and a battery system's report isn't one refusal longer each time.
+    /// </summary>
+    private async Task<(IReadOnlyDictionary<string, JsonNode?> Values, IReadOnlyList<string> Unused)> FillOnlyAsync(
+        ChangeSender sender, BatteryPack pack, UploadEntry entry, CancellationToken cancellationToken)
+    {
+        if (entry.FillOnly is not { Count: > 0 } fillOnly || !entry.Values.Keys.Any(fillOnly.Contains))
+        {
+            return (entry.Values, []);
+        }
+
+        var draft = await store.GetLatestVersionNumberAsync(pack.Id, cancellationToken) == 0;
+        var values = new Dictionary<string, JsonNode?>(entry.Values, StringComparer.Ordinal);
+        var unused = new List<string>();
+        foreach (var key in entry.Values.Keys.Where(fillOnly.Contains))
+        {
+            var attribute = AttributeCatalog.FindByProperty(key)!;
+            if (!draft || pack.Values.ContainsKey(key) || !EditRights.MayEdit(sender, attribute, pack.ClusterId))
+            {
+                values.Remove(key);
+                unused.Add($"the {char.ToLowerInvariant(attribute.Name[0])}{attribute.Name[1..]} in the file isn't used");
+            }
+        }
+
+        return (values, unused);
+    }
+
+    private static string Source(ChangeSender sender) => sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload";
+
+    /// <summary>
+    /// The battery's module list from the file (decision 31): app data, not passport data, so it is set beside the save
+    /// pipeline, without a new version. An empty list never overwrites; an unchanged list isn't saved again.
+    /// </summary>
+    private async Task<(bool Changed, string Words)?> ModulesAsync(ChangeSender sender, BatteryPack pack, IReadOnlyList<BatteryModule>? modules, bool preview, CancellationToken cancellationToken)
+    {
+        if (modules is not { Count: > 0 })
+        {
+            return null;
+        }
+
+        static string Key(BatteryModule module) => $"{module.Position}|{module.SerialNumber}|{module.ConnectivityId}";
+        if (pack.Modules.Select(Key).SequenceEqual(modules.OrderBy(module => module.Position).Select(Key)))
+        {
+            return (false, "the module list is unchanged");
+        }
+
+        var count = Words.Count(modules.Count, "module");
+        if (preview)
+        {
+            return (true, $"the module list would be set: {count}");
+        }
+
+        await store.SetPackModulesAsync(pack.Id, modules, cancellationToken);
+        var serial = pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? pack.Id;
+        await audit.AppendApplicationAuditEventAsync(
+            BatteryPages.ModulesSetEvent,
+            sender.Who,
+            sender.Kind.ToString(),
+            Source(sender),
+            $"Modules of battery {serial} set from the battery system's export: {count}",
+            new BsonDocument { ["from"] = pack.Modules.Count, ["to"] = modules.Count },
+            entityType: "dpp-pack",
+            entityId: pack.Id,
+            cancellationToken: cancellationToken);
+        return (true, $"the module list was set: {count}");
+    }
+
     private async Task<(UploadEntry Entry, WriteTarget? Target, string? Problem)> FindAsync(ChangeSender sender, UploadEntry entry, CancellationToken cancellationToken)
     {
         if (entry.Problem is not null)
@@ -172,6 +251,8 @@ public sealed class UploadService(
                 PackId = report.PackId,
                 Outcome = report.Outcome,
                 Problem = report.Problem,
+                Modules = report.Modules,
+                Notes = report.Notes?.ToList() ?? [],
                 Values = report.Values.Select(value => new UploadedValue
                 {
                     Key = value.Key,
@@ -187,7 +268,7 @@ public sealed class UploadService(
             UploadedEvent,
             sender.Who,
             sender.Kind.ToString(),
-            sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload",
+            Source(sender),
             $"Upload of {reports.Count} {(reports.Count == 1 ? "battery" : "batteries")}: {Count(Saved)} saved, {Count(NothingSaved)} with nothing saved, {Count(Refused)} refused",
             new BsonDocument { ["upload"] = upload.Id, ["format"] = format, ["channel"] = channel },
             entityType: "dpp-upload",
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
index 5bb28d2..bb42af4 100644
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -374,6 +374,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                         <span class="bp-summary-title">Uploads</span>
                         <h3>Send values for many batteries in one file.</h3>
                         <p>The body is the upload file. Name each battery by <code>uniqueProductIdentifier</code> (its battery ID) or <code>batterySerialNumber</code>; <code>measuredAt</code> is when its values were measured, and <code>values</code> holds them by key, as on the live values call. A cluster token sends as its battery system, so only the live values of its own clusters' published batteries are saved; a team token may also set a battery's own values where the team's pages allow it. Each battery is saved or refused on its own, and each value is checked on its own. A battery the file names twice, or whose passport is no longer active, is refused. Up to @BatteryPassWeb.Dpp.Uploads.UploadService.MaxBatteries batteries and 2 MB per file. Add <code>?preview=true</code> to check without saving.</p>
+                        <p>The body can also be the battery system's export as it is (mapping <code>@BatteryPassWeb.Dpp.Uploads.BmsExportFormat.Mapping</code>): one battery, found by its serial number, measured at <code>exportedAt</code>. The app takes the state of charge from its readable text as a whole percent, the full cycles, and the times in extreme temperatures as whole minutes (E1 to E4), and sets the battery's module list. A manufacturing date in the export only fills a draft without one, sent by the team; otherwise a note says it isn't used. Each battery's <code>notes</code> say what the export lacks, such as the pack temperature.</p>
                     </div>
                     <pre class="bp-help-pre"><code>POST @Model.BasePath/uploads
 {
```

`web/Dpp/Uploads/BmsExportFormat.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Uploads;

/// <summary>
/// The battery system's JSON export, mapping <c>BMS2_ACO</c> (design 8.3, decisions 17, 28 and 31; step 5b). One export is
/// one battery: its serial number and export time from <c>metadata</c>, its live values from <c>values</c>, and its
/// module list from <c>metadata.identifiers.deviceIdentifiers.childrenIdentifiers</c>. Everything else in the export
/// isn't in the passport and is ignored.
/// </summary>
public sealed partial class BmsExportFormat : IUploadAdapter
{
    public const string Mapping = "BMS2_ACO";

    /// <summary>The mapping version the adapter was checked against; another version is read the same way, with a note.</summary>
    public const string CheckedVersion = "01.32.07";

    public const string NoTemperature = "the export has no pack temperature yet, so #87 is left as it is";
    public const string ChargingAboveZero = "the export's time spent charging in extreme temperatures is above zero and can't be split into above and below the boundary, so E3 and E4 are left as they are";

    public bool Reads(JsonObject file) => file["metadata"] is JsonObject metadata && metadata["mappingName"] is JsonValue;

    public UploadDocument Read(JsonObject file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var metadata = (JsonObject)file["metadata"]!;
        var mapping = Text(metadata["mappingName"]);
        if (mapping != Mapping)
        {
            return UploadDocument.Unreadable($"the export's mapping \"{mapping}\" isn't one this app reads; it reads {Mapping}");
        }

        var format = $"{Mapping} {Text(metadata["mappingVersion"]) ?? "(no version)"}";
        var device = metadata["identifiers"]?["deviceIdentifiers"] as JsonObject;
        var serial = Text(device?["serialNumber"]);
        if (serial is null)
        {
            return new UploadDocument(format, [new UploadEntry(1, string.Empty, null, new Dictionary<string, JsonNode?>(), "the export has no serial number (metadata.identifiers.deviceIdentifiers.serialNumber)")]);
        }

        DateTime? measuredAt = null;
        if (metadata["exportedAt"] is { } exported)
        {
            if (Text(exported) is not { } stamp || !DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return new UploadDocument(format, [new UploadEntry(1, serial, null, new Dictionary<string, JsonNode?>(), "the export's exportedAt isn't a time")]);
            }

            measuredAt = parsed.UtcDateTime;
        }

        var notes = new List<string>();
        if (Text(metadata["mappingVersion"]) != CheckedVersion)
        {
            notes.Add($"read as mapping version {CheckedVersion}, the one this app was checked against");
        }

        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var exportValues = file["values"] as JsonObject ?? new JsonObject();

        // #63: the readable text, e.g. "27.1 %", as a whole percent; the raw number is on another scale.
        if (Formatted(exportValues, "stateOfCharge") is { } charge)
        {
            if (PercentText().Match(charge) is { Success: true } percent)
            {
                values["stateOfChargeSoC"] = (int)Math.Round(double.Parse(percent.Groups[1].Value, CultureInfo.InvariantCulture), MidpointRounding.AwayFromZero);
            }
            else
            {
                notes.Add($"the export's state of charge \"{charge}\" isn't a percentage, so #63 is left as it is");
            }
        }

        // #83: whole cycles completed.
        if (Raw(exportValues, "equivalentChargeAndDischargeCycles") is { } cycles)
        {
            values["numberOfFullChargingAndDischargingCycles"] = (int)Math.Floor(cycles);
        }

        notes.Add(NoTemperature);

        // E1 and E2: seconds, as whole minutes completed (decision 28).
        if (Raw(exportValues, "timeSpentInExtremeHighTemperature") is { } high)
        {
            values["timeSpentInExtremeTemperaturesAboveBoundary"] = Minutes(high);
        }

        if (Raw(exportValues, "timeSpentInExtremeLowTemperature") is { } low)
        {
            values["timeSpentInExtremeTemperaturesBelowBoundary"] = Minutes(low);
        }

        // E3 and E4: one charging time that is always zero (charging in extreme temperatures is prevented).
        if (Raw(exportValues, "timeSpentChargingInExtremeTemperature") is { } charging)
        {
            if (charging == 0)
            {
                values["timeSpentChargingDuringExtremeTemperaturesAboveBoundary"] = 0;
                values["timeSpentChargingDuringExtremeTemperaturesBelowBoundary"] = 0;
            }
            else
            {
                notes.Add(ChargingAboveZero);
            }
        }

        // #17: year and month (decision 18), readable only when the export has it. It only fills a battery that has none
        // (a new draft): once set, the team corrects it on the battery's page (decision 35, Q15).
        if (Formatted(exportValues, "productionDate") is { } produced && YearMonth().IsMatch(produced))
        {
            values["manufacturingDate"] = produced;
        }

        IReadOnlyList<BatteryModule> modules = [];
        string? problem = null;
        if (device is not null)
        {
            (modules, problem) = ModuleList.FromDevice(device);
        }

        if (problem is not null)
        {
            notes.Add($"the module list wasn't read: {problem}");
        }

        return new UploadDocument(format, [new UploadEntry(1, serial, measuredAt, values, Modules: modules, Notes: notes, FillOnly: ["manufacturingDate"])]);
    }

    private static int Minutes(double seconds) => (int)Math.Floor(seconds / 60);

    private static string? Formatted(JsonObject values, string name) => Text(values[name]?["formatted"]);

    private static double? Raw(JsonObject values, string name) =>
        values[name]?["raw"] is JsonValue raw && raw.GetValueKind() == JsonValueKind.Number
            && double.Parse(raw.ToJsonString(), CultureInfo.InvariantCulture) is var number && number >= 0
            ? number
            : null;

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Trim() is { Length: > 0 } text ? text : null;

    [GeneratedRegex(@"^(\d{1,3}(?:\.\d+)?)\s*%$")]
    private static partial Regex PercentText();

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex YearMonth();
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BmsExportUploadTests | FullyQualifiedName~UploadServiceTests | FullyQualifiedName~DppApiUploadTests"`
Expected: `Passed!  - Failed:     0, Passed:    30`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs BatteryPassWeb.Tests/Dpp/UploadServiceTests.cs web/Controllers/DppApiController.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Pages/ModuleList.cs web/Dpp/Store/DppRecords.cs web/Dpp/Uploads/BmsExportFormat.cs web/Dpp/Uploads/UploadFile.cs web/Dpp/Uploads/UploadService.cs web/Views/Help/Index.cshtml
git commit -m "feat(dpp): uploads read the battery system's export (BMS2_ACO)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The remaining capacity for the fade suggestion

The export's remaining capacity isn't passport data (J-11). The latest reading is kept on the battery as app data, with its measurement time, and an older export doesn't replace a newer one. The status window's "Measured remaining capacity (Ah)" box starts with it, saying when it was measured.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs`
- Modify: `web/Dpp/Pages/StatusPages.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Store/DppStore.cs`
- Modify: `web/Dpp/Uploads/BmsExportFormat.cs`
- Modify: `web/Dpp/Uploads/UploadFile.cs`
- Modify: `web/Dpp/Uploads/UploadService.cs`
- Modify: `web/Views/Batteries/_StatusDialog.cshtml`
- Modify: `web/Views/Help/Index.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `5b-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs`). Save the block as `../5b-task2-tests.patch`, outside the worktree, and run `git apply ../5b-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs b/BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs
index abffe90..34d8dc6 100644
--- a/BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs
@@ -44,6 +44,7 @@ public sealed class BmsExportUploadTests(MongoFixture mongo)
         Assert.Equal([(1, null, "MD-A1"), (2, "MD-2", "MD-A2")], entry.Modules!.Select(module => (module.Position, module.SerialNumber, module.ConnectivityId)));
         Assert.Equal([BmsExportFormat.NoTemperature], entry.Notes);
         Assert.Equal(["manufacturingDate"], entry.FillOnly);
+        Assert.Equal(126.0m, entry.RemainingCapacityAh);
     }
 
     [Fact]
@@ -168,6 +169,26 @@ public sealed class BmsExportUploadTests(MongoFixture mongo)
 
     internal const string DateNotUsed = "the manufacturing date in the file isn't used";
 
+    [Fact]
+    public async Task TheRemainingCapacity_ShouldBeKeptForTheStatusWindowUnlessANewerOneIs()
+    {
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        var feed = ChangeSender.Feed("north-feed", BatteryTesting.North);
+        var older = Export(UploadServiceTests.Serial(rig));
+        older["metadata"]!["exportedAt"] = "2026-10-27T07:00:00Z";
+        older["values"]!["remainingCapacityAh"]!["formatted"] = "150.0 Ah";
+
+        await Run(rig, feed, Export(UploadServiceTests.Serial(rig)), preview: true);
+        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.RemainingCapacity);
+        await Run(rig, feed, Export(UploadServiceTests.Serial(rig)));
+        await Run(rig, feed, older);
+
+        var kept = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.RemainingCapacity!;
+        Assert.Equal((126.0m, new DateTime(2026, 10, 28, 7, 40, 26, 319, DateTimeKind.Utc)), (kept.Ah, kept.MeasuredAtUtc));
+        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
+        Assert.Equal(126.0m, card.Remaining!.Ah);
+    }
+
     [Fact]
     public async Task TheSameModulesAgain_ShouldBeLeftAsTheyAreAndAPreviewShouldSaveNone()
     {
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BmsExportUploadTests | FullyQualifiedName~StatusPagesTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'BatteryPack' does not contain a definition for 'RemainingCapacity'`).

- [ ] **Step 3: Write the code**

Patch `5b-task2-code.patch` (`web/Dpp/Pages/StatusPages.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Store/DppStore.cs`, `web/Dpp/Uploads/BmsExportFormat.cs`, `web/Dpp/Uploads/UploadFile.cs`, `web/Dpp/Uploads/UploadService.cs`, `web/Views/Batteries/_StatusDialog.cshtml`, `web/Views/Help/Index.cshtml`). Save the block as `../5b-task2-code.patch`, outside the worktree, and run `git apply ../5b-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/StatusPages.cs b/web/Dpp/Pages/StatusPages.cs
index 19a0ea7..695f5c3 100644
--- a/web/Dpp/Pages/StatusPages.cs
+++ b/web/Dpp/Pages/StatusPages.cs
@@ -39,6 +39,7 @@ public sealed record PredecessorView(string SerialNumber, string Href, bool Inac
 /// </summary>
 /// <param name="CanChange">False for a draft or a waste battery; the window isn't offered then.</param>
 /// <param name="RatedCapacityAh">The model's rated capacity (#57), for the fade suggestion; null when it has none.</param>
+/// <param name="Remaining">The latest remaining capacity the battery system exported, which the suggestion starts with (step 5b).</param>
 public sealed record StatusCardView(
     string Status,
     DateTime? Since,
@@ -52,7 +53,8 @@ public sealed record StatusCardView(
     bool ForTeam,
     bool MayAddNote = false,
     SuccessionView? Succession = null,
-    PredecessorView? Predecessor = null)
+    PredecessorView? Predecessor = null,
+    RemainingCapacityReading? Remaining = null)
 {
     /// <summary>Where "Add a note to the history" posts (decision 34).</summary>
     public string NoteAction => $"{Restated.SaveAction}/note";
@@ -137,7 +139,8 @@ public sealed class StatusPages(DppStore store, RecordEditor editor, StatusChang
             forTeam,
             MayAddNote: active,
             Succession: successionView,
-            Predecessor: await PredecessorAsync(pack, forTeam, cancellationToken));
+            Predecessor: await PredecessorAsync(pack, forTeam, cancellationToken),
+            Remaining: pack.RemainingCapacity);
     }
 
     private async Task<SuccessionView?> SuccessionAsync(BatteryPack pack, string href, bool forTeam, CancellationToken cancellationToken)
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index 577c5cd..ad2e08e 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -128,6 +128,9 @@ public sealed class BatteryPack : IDppRecord
     /// </summary>
     public List<RepairEntry> Repairs { get; set; } = [];
 
+    /// <summary>The latest remaining capacity its battery system exported (step 5b); null when none was.</summary>
+    public RemainingCapacityReading? RemainingCapacity { get; set; }
+
     /// <summary>
     /// Placed on the market again (design 7.5, step 4c-1): what the status was, and the battery's new passport, Scania's
     /// (a new pack) or another operator's. Null while the battery isn't waiting for a new passport. App data.
@@ -299,6 +302,18 @@ public sealed class UploadedValue
     public string? Message { get; init; }
 }
 
+/// <summary>
+/// The battery's latest measured remaining capacity from its battery system's export (decision 35, step 5b). App data,
+/// not passport data: the status window offers it to suggest the capacity fade (#59, J-11).
+/// </summary>
+public sealed class RemainingCapacityReading
+{
+    public decimal Ah { get; init; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public DateTime MeasuredAtUtc { get; init; }
+}
+
 /// <summary>One repair of a pack (design 7.9): when, what was repaired, and who recorded it when.</summary>
 public sealed class RepairEntry
 {
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index 65b483e..e6b3988 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -253,6 +253,21 @@ public sealed class DppStore
         return result.MatchedCount == 1;
     }
 
+    /// <summary>
+    /// Keeps a remaining capacity reading (step 5b) unless the pack already has a newer one. False when nothing changed.
+    /// </summary>
+    public async Task<bool> SetRemainingCapacityAsync(string packId, RemainingCapacityReading reading, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(reading);
+        var result = await Collection<BatteryPack>().UpdateOneAsync(
+            Builders<BatteryPack>.Filter.Eq(pack => pack.Id, packId)
+            & (Builders<BatteryPack>.Filter.Eq(pack => pack.RemainingCapacity, null)
+               | Builders<BatteryPack>.Filter.Lte(pack => pack.RemainingCapacity!.MeasuredAtUtc, reading.MeasuredAtUtc)),
+            Builders<BatteryPack>.Update.Set(pack => pack.RemainingCapacity, reading),
+            cancellationToken: cancellationToken);
+        return result.ModifiedCount == 1;
+    }
+
     /// <summary>Replaces a pack's module list (decision 31). False when the pack doesn't exist.</summary>
     public async Task<bool> SetPackModulesAsync(string packId, IReadOnlyList<BatteryModule> modules, CancellationToken cancellationToken = default)
     {
diff --git a/web/Dpp/Uploads/BmsExportFormat.cs b/web/Dpp/Uploads/BmsExportFormat.cs
index bc6b6c5..b306966 100644
--- a/web/Dpp/Uploads/BmsExportFormat.cs
+++ b/web/Dpp/Uploads/BmsExportFormat.cs
@@ -116,6 +116,13 @@ public sealed partial class BmsExportFormat : IUploadAdapter
             values["manufacturingDate"] = produced;
         }
 
+        // The remaining capacity isn't passport data; it is kept to suggest the capacity fade (J-11, Q16).
+        decimal? remaining = null;
+        if (Formatted(exportValues, "remainingCapacityAh") is { } capacity && AmpereHours().Match(capacity) is { Success: true } ah)
+        {
+            remaining = decimal.Parse(ah.Groups[1].Value, CultureInfo.InvariantCulture);
+        }
+
         IReadOnlyList<BatteryModule> modules = [];
         string? problem = null;
         if (device is not null)
@@ -128,7 +135,7 @@ public sealed partial class BmsExportFormat : IUploadAdapter
             notes.Add($"the module list wasn't read: {problem}");
         }
 
-        return new UploadDocument(format, [new UploadEntry(1, serial, measuredAt, values, Modules: modules, Notes: notes, FillOnly: ["manufacturingDate"])]);
+        return new UploadDocument(format, [new UploadEntry(1, serial, measuredAt, values, Modules: modules, Notes: notes, FillOnly: ["manufacturingDate"], RemainingCapacityAh: remaining)]);
     }
 
     private static int Minutes(double seconds) => (int)Math.Floor(seconds / 60);
@@ -149,4 +156,7 @@ public sealed partial class BmsExportFormat : IUploadAdapter
 
     [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
     private static partial Regex YearMonth();
+
+    [GeneratedRegex(@"^(\d{1,6}(?:\.\d+)?)\s*Ah$")]
+    private static partial Regex AmpereHours();
 }
diff --git a/web/Dpp/Uploads/UploadFile.cs b/web/Dpp/Uploads/UploadFile.cs
index 60c7e6f..6d9f44e 100644
--- a/web/Dpp/Uploads/UploadFile.cs
+++ b/web/Dpp/Uploads/UploadFile.cs
@@ -16,6 +16,7 @@ namespace BatteryPassWeb.Dpp.Uploads;
 /// <param name="Modules">The battery's module list, when the file has one (the battery system's export, decision 31).</param>
 /// <param name="Notes">What the file's adapter wants the sender to know, e.g. a value it couldn't read.</param>
 /// <param name="FillOnly">Keys that only fill a battery without a value, left out silently otherwise (the export's #17).</param>
+/// <param name="RemainingCapacityAh">The measured remaining capacity, kept as app data for the fade suggestion (J-11).</param>
 public sealed record UploadEntry(
     int Number,
     string NamedAs,
@@ -24,7 +25,8 @@ public sealed record UploadEntry(
     string? Problem = null,
     IReadOnlyList<BatteryModule>? Modules = null,
     IReadOnlyList<string>? Notes = null,
-    IReadOnlyCollection<string>? FillOnly = null);
+    IReadOnlyCollection<string>? FillOnly = null,
+    decimal? RemainingCapacityAh = null);
 
 /// <summary>A file read by its format's adapter: the format, its entries, or why it can't be read at all.</summary>
 public sealed record UploadDocument(string? Format, IReadOnlyList<UploadEntry> Entries, string? Problem = null)
diff --git a/web/Dpp/Uploads/UploadService.cs b/web/Dpp/Uploads/UploadService.cs
index 0b5774b..df1511b 100644
--- a/web/Dpp/Uploads/UploadService.cs
+++ b/web/Dpp/Uploads/UploadService.cs
@@ -128,6 +128,11 @@ public sealed class UploadService(
             notes = [.. notes, .. unused];
             var result = await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, values, source, entry.MeasuredAtUtc), preview, cancellationToken);
             var modules = await ModulesAsync(sender, pack, entry.Modules, preview, cancellationToken);
+            if (!preview && entry.RemainingCapacityAh is { } remaining)
+            {
+                await store.SetRemainingCapacityAsync(pack.Id, new RemainingCapacityReading { Ah = remaining, MeasuredAtUtc = entry.MeasuredAtUtc ?? clock.GetUtcNow().UtcDateTime }, cancellationToken);
+            }
+
             var outcome = result.Keys.Any(key => key.Outcome == KeyOutcome.Saved) || modules is { Changed: true } ? Saved : NothingSaved;
             reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, result.Keys, modules?.Words, notes));
         }
diff --git a/web/Views/Batteries/_StatusDialog.cshtml b/web/Views/Batteries/_StatusDialog.cshtml
index 57d2158..29ecd55 100644
--- a/web/Views/Batteries/_StatusDialog.cshtml
+++ b/web/Views/Batteries/_StatusDialog.cshtml
@@ -38,9 +38,9 @@
                         @if (field.Attribute.Number == 59 && Model.RatedCapacityAh is { } rated)
                         {
                             <div class="bp-suggest" data-fade-suggest data-rated="@rated.ToString(System.Globalization.CultureInfo.InvariantCulture)">
-                                <label><span>Measured remaining capacity (Ah)</span><input type="text" inputmode="decimal" data-fade-remaining /></label>
+                                <label><span>Measured remaining capacity (Ah)</span><input type="text" inputmode="decimal" data-fade-remaining value="@(Model.Remaining?.Ah.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture))" /></label>
                                 <button type="button" class="bp-text-button" data-fade-button>Suggest the fade</button>
-                                <small class="bp-field-help" data-fade-note>1 − remaining / @ValueWords.Number(rated) Ah rated. The measured capacity isn't stored.</small>
+                                <small class="bp-field-help" data-fade-note>1 − remaining / @ValueWords.Number(rated) Ah rated.@(Model.Remaining is { } reading ? $" It starts with the battery system's last export, {ValueWords.Time(reading.MeasuredAtUtc)}." : " The measured capacity isn't part of the passport.")</small>
                             </div>
                         }
                     }
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
index bb42af4..ff92db6 100644
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -374,7 +374,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                         <span class="bp-summary-title">Uploads</span>
                         <h3>Send values for many batteries in one file.</h3>
                         <p>The body is the upload file. Name each battery by <code>uniqueProductIdentifier</code> (its battery ID) or <code>batterySerialNumber</code>; <code>measuredAt</code> is when its values were measured, and <code>values</code> holds them by key, as on the live values call. A cluster token sends as its battery system, so only the live values of its own clusters' published batteries are saved; a team token may also set a battery's own values where the team's pages allow it. Each battery is saved or refused on its own, and each value is checked on its own. A battery the file names twice, or whose passport is no longer active, is refused. Up to @BatteryPassWeb.Dpp.Uploads.UploadService.MaxBatteries batteries and 2 MB per file. Add <code>?preview=true</code> to check without saving.</p>
-                        <p>The body can also be the battery system's export as it is (mapping <code>@BatteryPassWeb.Dpp.Uploads.BmsExportFormat.Mapping</code>): one battery, found by its serial number, measured at <code>exportedAt</code>. The app takes the state of charge from its readable text as a whole percent, the full cycles, and the times in extreme temperatures as whole minutes (E1 to E4), and sets the battery's module list. A manufacturing date in the export only fills a draft without one, sent by the team; otherwise a note says it isn't used. Each battery's <code>notes</code> say what the export lacks, such as the pack temperature.</p>
+                        <p>The body can also be the battery system's export as it is (mapping <code>@BatteryPassWeb.Dpp.Uploads.BmsExportFormat.Mapping</code>): one battery, found by its serial number, measured at <code>exportedAt</code>. The app takes the state of charge from its readable text as a whole percent, the full cycles, and the times in extreme temperatures as whole minutes (E1 to E4), and sets the battery's module list. A manufacturing date in the export only fills a draft without one, sent by the team; otherwise a note says it isn't used. The remaining capacity is kept to suggest the capacity fade when the status changes. Each battery's <code>notes</code> say what the export lacks, such as the pack temperature.</p>
                     </div>
                     <pre class="bp-help-pre"><code>POST @Model.BasePath/uploads
 {
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BmsExportUploadTests | FullyQualifiedName~StatusPagesTests"`
Expected: `Passed!  - Failed:     0, Passed:    24`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BmsExportUploadTests.cs web/Dpp/Pages/StatusPages.cs web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs web/Dpp/Uploads/BmsExportFormat.cs web/Dpp/Uploads/UploadFile.cs web/Dpp/Uploads/UploadService.cs web/Views/Batteries/_StatusDialog.cshtml web/Views/Help/Index.cshtml
git commit -m "feat(dpp): the export's remaining capacity starts the fade suggestion" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2469, Skipped:     0, Total:  2469`. From the 2459 before: 10 new tests.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 6f2a982 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. Uploading on a battery's page comes with 5c; until then exports go through the API. The managing session brings these to the product owner:

1. **Restart the app with the usual launcher**, sign in with a team account and press "Reset demo data" at the end of the Clusters tab.
2. **The help page** (Help, then "API reference and workbench"): the uploads row's details now describe the battery system's export.
3. **Send an export in the workbench:** make a read-write token for demo-north on the API tokens tab (its value is shown once) and paste it in the workbench. Choose "Send an upload of many batteries" and replace the body with a battery system export whose serial number is changed to `DEMO-C7M-0001`. The answer lists the saved live values, "the module list was set", and the notes (no pack temperature yet). Delete the token afterwards.
4. **DEMO-C7M-0001's page** (Batteries, then the battery): "Live values" in the side menu shows the values "from the battery system"; "Modules" shows the export's list; "Change status" among the page's actions → "Repaired" → "Next" starts the "Measured remaining capacity (Ah)" box with the export's value, saying when it was measured. "History" among the page's links opens the audit log for this battery: the export's values, from "The API", and "Modules of battery DEMO-C7M-0001 set from the battery system's export: 8 modules".
5. **As demo-north's cluster admin** (`demo.north.admin@example.test`), the same battery's page ("Manage" on the Registry): the same box in "Change status", and "History" in the side menu: their battery system's readings as one line ("the latest of N live readings" after several) and the module list's change, by "Your battery system".

## Next

5c: "Upload a file" on each battery's page (mock-up B1–B4, approved), with "Fill from export" merged into it. Then 5d: status changes and repairs by upload, the help page's sample files and the demo example.
