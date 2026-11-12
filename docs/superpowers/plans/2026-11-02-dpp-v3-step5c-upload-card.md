# DPP V3 Step 5c: "Upload a File" on Each Battery's Page — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The third part of step 5 (decisions 8 and 35). Uploads happen on each battery's page, not on a central page (the product owner, 2 Nov 2026):
- **"Upload a file"**, a section of the side menu after "Live values" on the team's battery page and on the cluster admin's, takes one JSON file for that battery: the app's upload format or the battery system's export as it is.
- **Check before saving:** the section shows what the file would do (each value now, in the file, and what happens; the module list; the notes), then "Save" or "Start again". A file for another battery, for several, over 2 MB or not JSON is refused in one line.
- **Earlier uploads:** the battery's latest five, newest first, one line each, opening in place; the one just saved opens first. The team sees every upload of the battery; a cluster admin only their cluster's (its admins' and its battery system's).
- **Read-only pages:** a marked passport (invalid or for deletion) and an earlier passport replaced by a new one have no "Upload a file" section, and the app refuses a file for them.
- **One way in per battery:** the modules section's "Fill from export" merges into the new section; the team still types or corrects the list.
- **Rights are the save pipeline's, as everywhere:** the team's file may also set the battery's own values; a cluster admin's changes the live values and the module list. Status changes by upload come with 5d.

Mock-up B1–B4 (approved on 2 Nov 2026): `docs/mockups/dpp-v3-step5-live-data-and-uploads.html` (this plan's commit adds it); on the redesigned pages the card is a side-menu section, as in the shots the product owner approved on 3 Nov 2026.

**Architecture:**
- **`UploadService`** (task 1):
  - `CheckBatteryFileAsync(sender, packId, fileName, bytes)` refuses a file over 2 MB, one that isn't JSON, one no adapter reads, one listing more than one battery, one whose battery isn't this one (found as the API finds it), and one for a passport that is no longer active. Otherwise it runs the file as a preview and keeps it as an `UploadCheck` (battery, checker, file name and JSON) in the new collection `dpp_upload_checks`, whose TTL index `expires` drops it after an hour.
  - `ShowCheckAsync` runs a kept check again for the page. `SaveCheckedAsync` saves it, only for its checker, on the same battery, within the hour, and removes it.
  - `HistoryAsync(pack, forTeam)` lists the battery's latest uploads, cut to its line (new index `pack_at` on `dpp_uploads`).
  - The upload log keeps each value as sent (`UploadedValue.Value`) and the battery's cluster (`UploadedBattery.ClusterId`).
- **`UploadPages`** (task 2) turns a check and the history into words (`UploadCardView`, `UploadCheckView`, `UploadHistoryRow`): values through the forms the passport pages use, "Will be saved", "Not saved: …", "Kept: empty in the file".
- **The pages** (task 2): `BatteryPage.Upload` and `ClusterBatteryPage.Upload` add the section "upload" to the side menu after "Live values" (hint: the last upload, or "A JSON file for this battery"). The controllers add the card's model on every path that shows the page, except a read-only one (`Cards.FrozenAs`, `ClusterBatteryPage.ReadOnly`).
- **The controllers** (task 2) add `POST {id}/upload` (check) and `POST {id}/upload/save` on `/admin/batteries` and `/cluster-admin/batteries`, back to `?section=upload`. They pass the kept check's ID across the redirect in TempData (read once, so a reload clears it); a cluster admin who doesn't manage the battery, or whose page is read-only, gets 404.
- **`_UploadCard.cshtml`** is one partial for both pages, in the `data-section="upload"` block. The mock-up's CSS block goes into `bp-product-data.css`; `site.css` is not changed. `BatteriesController.ImportModules` ("Fill from export") and `MaxExportBytes` go; `BatteryPages.ImportModulesAsync` stays for the demo seed.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 6.2 and 8.1 (amended by this plan's commit) and section 14's step-5 note. Decisions record: decisions 8 and 35. Plans 5a and 5b: `docs/superpowers/plans/2026-11-02-dpp-v3-step5a-upload-engine.md`, `docs/superpowers/plans/2026-11-02-dpp-v3-step5b-battery-system-export.md`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at ed8782e (the end of plan 5b's run: "feat(dpp): the export's remaining capacity starts the fade suggestion", on a546e15 and e8f0806), plus the commit that adds this plan. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo). Once this plan runs, the app's start (its `EnsureIndexesAsync`) makes the new collection `dpp_upload_checks` with its TTL index `expires`, and the index `pack_at` on `dpp_uploads`, in `battery_pass_v3`; the checks collection stays nearly empty (a check lives an hour). The reset tool knows it as new test data.
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Each task runs only its own tests** (the filter in its steps); **the whole suite runs once**, in the final checks. Never run two whole suites at once.
  - **One older test can fail by chance:** `BatteryHistoryLinkTests.ABatterysHistory_ShouldSayWhoseItIs_AndLeadBack` (redesign R6) takes the first demo battery the database returns, without an order; when that is the repurposed DEMO-C13M-0001 (which has another passport), it fails. It passes when run again on its own (`--filter "FullyQualifiedName~BatteryHistoryLinkTests"`). If it is the only failure, note it and go on; this plan doesn't touch it.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started (by their process IDs, never by name) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2469` (plan 5b's result). After this plan: 2481. The suite takes about four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../5c-task2-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (2 and 3 Nov 2026, through the managing session)

Decision 35 holds step 5's answers; this plan's commit adds 5c's and amends design 8.1. The ones 5c builds on:

1. **Uploads happen on each battery's page, not on a central page** (the product owner's change): no "Uploads" tab for the team or cluster admins. In the product owner's words: "do we go to each battery, as team or cluster admin, and then go to a section to upload the json? becasue the second is what i want". A file for many batteries goes through the external API (plan 5a).
2. **One file for that battery only:** a file naming another battery, or several, is refused with a plain reason, as the modules' "Fill from export" did.
3. **Mock-up B1–B4 approved as drawn** ("Approve it", 2 Nov), and **its place on the redesigned pages approved** ("Approve", 3 Nov, shots of the empty section, the check, after saving, the cluster admin's section, a bad file and a read-only page): a side-menu section after "Live values".
4. **Q11: a checked file is kept for one hour,** so "Save" doesn't need it again.
5. **Q18: "Fill from export" merges into "Upload a file"** ("Merge it"); the modules section keeps the list and "Type or correct the list".
6. **Q19: the team sees every upload of the battery; a cluster admin only their cluster's** (its admins' and its battery system's), not the team's, which can carry values the cluster doesn't see.
7. **Approved on 3 Nov with the shots:** no extra confirm window on "Save" (the check is the confirmation, and nothing here deletes); a file that would change nothing is still shown, with its refusals, without "Save"; the check kept an hour; "Fill from export" out of Modules.
8. **Every upload stays in `dpp_uploads` and in the audit log.**

## Found while prototyping, and what this plan does

- **Read-only pages** (redesign R6b, R7): a marked or earlier passport's page has no upload section, and the service refuses a file for it before the check ("the passport is invalid; nothing on it changes"), so a stale form can't change it either.
- **The battery pages' services can't use the upload service** without a loop (the upload service finds batteries through `BatteryApi`, which uses `BatteryPages`). The controllers add the section's model to the page instead, on every path that shows the page.
- **A file that would change nothing is still shown in the check,** with its refusals and without "Save", so the sender sees why.
- **The history needs each value as the file sent it,** so the upload log keeps it (`UploadedValue.Value`); uploads logged before this plan show no value there.
- **The check is read once from TempData,** so a reload or "Start again" shows the section's file picker again; the kept check expires by itself.
- **The file picker checks the file's kind and size in the browser first** (`data-kind-words`, `data-max-bytes`), as the redesign's other file fields do; the server checks again.
- **Browser checks** (17, on a throwaway app with the demo data, at 1280 and 375 px, light and dark): the section after "Live values"; a file for another battery and a file that isn't JSON refused; the team's check and save of a battery system export, the upload open first; the export's modules on the battery; no section on the invalid demo battery; the cluster admin's section without the team's upload, the team's value refused, and their save; no sideways scrolling.

## What happens to the old tests

Nothing is removed.
- **`ResetCollectionsTests`** knows the new collection `dpp_upload_checks` (Task 1).
- **`ModuleDataTests`** checks that "Fill from export" is gone from the modules section and the upload section is on the page (Task 2).
- **`BatteriesControllerTests`, `ClusterBatteriesControllerTests`, `HandOverPagesTests`, `StatusPagesTests`, `SuccessionPagesTests`** build their controllers with the upload pages (Task 2).

## A part to write yourself (optional)

The product owner chose "you write it" before; the plan gives the code. If they want to try one, the natural spot is `UploadService.CheckBatteryFileAsync` in Task 1: when a file on a battery's page is refused before it is checked. `AFileForAnotherBatteryOrForSeveral_ShouldBeRefusedInPlainWords` and `APassportNoLongerActive_ShouldRefuseTheFileBeforeTheCheck` list it.

---

### Task 1: A file per battery, checked and kept for an hour, and its history

The rules before the section. A file chosen on a battery's page must name that battery only (by battery ID, passport ID or serial number); a file for another battery, or for several, is refused in plain words, as are a file over 2 MB, one that isn't JSON, and one for a passport that is no longer active (a marked or earlier passport, whose page is read-only). A readable file is checked like a preview and kept for an hour in the new collection `dpp_upload_checks` (the database drops it when it expires); only its checker saves it, on the same battery, and it is run again then. The upload log keeps each value as the file sent it and the battery's cluster, so the battery's history shows the team every upload and a cluster admin only their cluster's (Q19).

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs`
- Modify: `tools/V3Reset/ResetCollections.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Store/DppStore.cs`
- Modify: `web/Dpp/Uploads/UploadService.cs`

- [ ] **Step 1: Write the failing tests**

Patch `5c-task1-tests.patch` (`BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs`). Save the block as `../5c-task1-tests.patch`, outside the worktree, and run `git apply ../5c-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs b/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
index d179306..8604cba 100644
--- a/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
+++ b/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
@@ -16,7 +16,7 @@ public sealed class ResetCollectionsTests
         var named = Directory.GetFiles(web, "*.cs", SearchOption.AllDirectories)
             .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
             .SelectMany(file => Regex.Matches(File.ReadAllText(file), "GetCollection<[^>]*>\\(\"([^\"]+)\"\\)").Select(match => match.Groups[1].Value));
-        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };
+        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DppStore.UploadChecks, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };
 
         foreach (var collection in named.Concat(dpp).Distinct())
         {
@@ -27,7 +27,7 @@ public sealed class ResetCollectionsTests
     [Fact]
     public void TheNewModelsCollections_ShouldBeEmptiedAsTestData()
     {
-        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
+        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DppStore.UploadChecks, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
         {
             Assert.Equal(CollectionFate.NewTestData, ResetCollections.FateOf(collection));
         }
```

`BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs`:

```csharp
using System.Text;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// A file uploaded on a battery's page (decision 35, step 5c): for that battery only, checked before saving, kept an
/// hour for its checker's "Save", and the battery's upload history as each reader may see it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class UploadCheckTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    [Fact]
    public async Task ACheckedFile_ShouldSaveNothingUntilItsCheckerSavesItOnce()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);

        var check = await service.CheckBatteryFileAsync(NorthAdmin, rig.PackId, "readings.json", Native(UploadServiceTests.Serial(rig), 41));

        Assert.Null(check.Problem);
        Assert.NotNull(check.CheckId);
        Assert.True(check.Preview);
        Assert.Equal(76, StateOfCharge(await Pack(rig)));
        Assert.Null(await service.SaveCheckedAsync(ChangeSender.ClusterAdmin("other-admin@example.com", BatteryTesting.North), rig.PackId, check.CheckId));
        Assert.Null(await service.SaveCheckedAsync(NorthAdmin, "another-pack", check.CheckId));

        var saved = (await service.SaveCheckedAsync(NorthAdmin, rig.PackId, check.CheckId))!;

        Assert.False(saved.Preview);
        Assert.Equal(UploadService.Saved, Assert.Single(saved.Batteries).Outcome);
        Assert.Equal(41, StateOfCharge(await Pack(rig)));
        var logged = Assert.Single(await rig.Store.ListUploadsAsync(10));
        Assert.Equal(("readings.json", "page", BatteryTesting.North), (logged.FileName, logged.Channel, logged.Batteries[0].ClusterId));
        Assert.Null(await service.SaveCheckedAsync(NorthAdmin, rig.PackId, check.CheckId)); // saved once
    }

    [Fact]
    public async Task APassportNoLongerActive_ShouldRefuseTheFileBeforeTheCheck()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures"));

        var report = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "readings.json", Native(UploadServiceTests.Serial(rig), 41));

        Assert.Equal(SavePipeline.NoLongerChanges(DppStatuses.Invalid), report.Problem);
        Assert.Null(report.CheckId);
    }

    [Fact]
    public async Task ACheck_ShouldExpireAfterAnHour()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        var check = await service.CheckBatteryFileAsync(NorthAdmin, rig.PackId, "readings.json", Native(UploadServiceTests.Serial(rig), 41));

        rig.Clock.Now += UploadService.CheckKeptFor;

        Assert.Null(await service.SaveCheckedAsync(NorthAdmin, rig.PackId, check.CheckId));
        Assert.Equal(76, StateOfCharge(await Pack(rig)));
    }

    [Fact]
    public async Task AFileForAnotherBatteryOrForSeveral_ShouldBeRefusedInPlainWords()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        var several = Encoding.UTF8.GetBytes(UploadServiceTests.File(
            UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 1 }"""),
            UploadServiceTests.Entry("VPC9-9999", """{ "stateOfChargeSoC": 2 }""")));

        var other = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "a.json", Native("VPC9-9999", 41));
        var export = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "b.json", Encoding.UTF8.GetBytes(BmsExportUploadTests.Export("VPC9-9999").ToJsonString()));
        var two = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "c.json", several);
        var text = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "d.txt", "not json"u8.ToArray());
        var big = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "e.json", new byte[UploadService.MaxBytes + 1]);

        Assert.Equal(UploadService.NotThisBattery("VPC9-9999"), other.Problem);
        Assert.Equal(UploadService.NotThisBattery("VPC9-9999"), export.Problem);
        Assert.Equal(UploadService.OneBatteryOnly(2), two.Problem);
        Assert.Equal(UploadService.NotJson, text.Problem);
        Assert.Equal(UploadService.TooLarge, big.Problem);
        Assert.All(new[] { other, export, two, text, big }, report => Assert.Null(report.CheckId));
    }

    [Fact]
    public async Task AKeptCheck_ShouldBeShownAgainAsItsCheckerWouldSaveIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        var refused = await service.CheckBatteryFileAsync(NorthAdmin, rig.PackId, "a.json", Native(UploadServiceTests.Serial(rig), 400));
        var check = await service.CheckBatteryFileAsync(NorthAdmin, rig.PackId, "b.json", Native(UploadServiceTests.Serial(rig), 41));
        var pack = (await Pack(rig))!;

        var shown = (await service.ShowCheckAsync(pack, forTeam: false, check.CheckId))!.Value;

        Assert.Equal(UploadService.NothingSaved, Assert.Single(refused.Batteries).Outcome);
        Assert.NotNull(refused.CheckId); // shown with its refusal, without "Save"
        Assert.Equal(("b.json", UploadService.Saved, true), (shown.Check.FileName, Assert.Single(shown.Report.Batteries).Outcome, shown.Report.Preview));
        Assert.Equal("41", Assert.Single(shown.Report.Batteries).Sent!["stateOfChargeSoC"]);
        Assert.Null(await service.ShowCheckAsync(pack, forTeam: true, check.CheckId)); // the cluster admin's, not the team's
        Assert.Equal(76, StateOfCharge(await Pack(rig)));
    }

    [Fact]
    public async Task TheHistory_ShouldShowTheTeamEveryUploadAndAClusterAdminTheirClusters()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        await UploadServiceTests.Run(rig, ChangeSender.Feed("north-feed", BatteryTesting.North), UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 50 }""")));
        rig.Clock.Now += TimeSpan.FromMinutes(1);
        await UploadServiceTests.Run(rig, PublicationTesting.Team, UploadServiceTests.File(
            UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 51 }"""),
            UploadServiceTests.Entry("VPC9-9999", """{ "stateOfChargeSoC": 52 }""")));

        var pack = (await Pack(rig))!;
        var team = await service.HistoryAsync(pack, forTeam: true);
        var clusterAdmins = await service.HistoryAsync(pack, forTeam: false);

        Assert.Equal(["team@example.com", "north-feed"], team.Select(upload => upload.Who));
        Assert.All(team, upload => Assert.Equal(rig.PackId, Assert.Single(upload.Batteries).PackId)); // only this battery's line
        Assert.Equal("51", team[0].Batteries[0].Values[0].Value);
        Assert.Equal(["north-feed"], clusterAdmins.Select(upload => upload.Who)); // not the team's (Q19)
    }

    internal static byte[] Native(string serial, int stateOfCharge) =>
        Encoding.UTF8.GetBytes(UploadServiceTests.File(UploadServiceTests.Entry(serial, $$"""{ "stateOfChargeSoC": {{stateOfCharge}} }""")));

    private static Task<BatteryPack?> Pack(PublicationRig rig) => rig.Store.GetAsync<BatteryPack>(rig.PackId);

    private static int StateOfCharge(BatteryPack? pack) => pack!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~UploadCheckTests | FullyQualifiedName~ResetCollectionsTests"`
Expected: the build fails with CS0117, CS1061, CS1929 (for example `error CS0117 'DppStore' does not contain a definition for 'UploadChecks'`).

- [ ] **Step 3: Write the code**

Patch `5c-task1-code.patch` (`tools/V3Reset/ResetCollections.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Store/DppStore.cs`, `web/Dpp/Uploads/UploadService.cs`). Save the block as `../5c-task1-code.patch`, outside the worktree, and run `git apply ../5c-task1-code.patch` from the worktree root:

```diff
diff --git a/tools/V3Reset/ResetCollections.cs b/tools/V3Reset/ResetCollections.cs
index f3deedf..2863ae8 100644
--- a/tools/V3Reset/ResetCollections.cs
+++ b/tools/V3Reset/ResetCollections.cs
@@ -42,6 +42,7 @@ public static class ResetCollections
         ["dpp_passport_versions"] = CollectionFate.NewTestData,
         ["dpp_transfers"] = CollectionFate.NewTestData,
         ["dpp_uploads"] = CollectionFate.NewTestData,
+        ["dpp_upload_checks"] = CollectionFate.NewTestData,
         ["dpp_documents"] = CollectionFate.NewTestData,
         ["dpp_files.files"] = CollectionFate.NewTestData,
         ["dpp_files.chunks"] = CollectionFate.NewTestData,
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index ad2e08e..9acfec0 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -265,6 +265,31 @@ public sealed class UploadRecord : IDppRecord
     public bool IsDemo { get; set; }
 }
 
+/// <summary>
+/// A file checked on a battery's page and kept for an hour (step 5c), so "Save" doesn't need it again. Only the person
+/// who checked it saves it, for that battery; the database drops it when it expires.
+/// </summary>
+public sealed class UploadCheck : IDppRecord
+{
+    [BsonId]
+    public required string Id { get; init; }
+
+    public required string PackId { get; init; }
+
+    public required string Who { get; init; }
+
+    /// <summary>The sender's kind (<c>SenderKind</c>) when the file was checked.</summary>
+    public required string SentAs { get; init; }
+
+    public required string FileName { get; init; }
+
+    /// <summary>The file's JSON text.</summary>
+    public required string Json { get; init; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public required DateTime ExpiresAtUtc { get; init; }
+}
+
 /// <summary>What happened to one battery of an upload: found or not, refused or not, and each value's outcome.</summary>
 public sealed class UploadedBattery
 {
@@ -277,6 +302,9 @@ public sealed class UploadedBattery
     /// <summary>The battery found; null when none was.</summary>
     public string? PackId { get; init; }
 
+    /// <summary>The battery's cluster when the upload ran (step 5c).</summary>
+    public string? ClusterId { get; init; }
+
     /// <summary>"saved", "nothing saved" or "refused".</summary>
     public required string Outcome { get; init; }
 
@@ -297,6 +325,9 @@ public sealed class UploadedValue
 {
     public required string Key { get; init; }
 
+    /// <summary>The value as the file sent it (JSON text), shown in the battery's upload history (step 5c).</summary>
+    public string? Value { get; init; }
+
     public required string Outcome { get; init; }
 
     public string? Message { get; init; }
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index e6b3988..dbcf397 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -22,6 +22,7 @@ public sealed class DppStore
     public const string Versions = "dpp_passport_versions";
     public const string HandOvers = "dpp_transfers";
     public const string Uploads = "dpp_uploads";
+    public const string UploadChecks = "dpp_upload_checks";
 
     private const string UniqueProductIdentifierField = "Values.uniqueProductIdentifier.Value";
     private const string SerialNumberField = "Values.batterySerialNumber.Value";
@@ -37,6 +38,7 @@ public sealed class DppStore
         [typeof(PassportVersion)] = Versions,
         [typeof(HandOver)] = HandOvers,
         [typeof(UploadRecord)] = Uploads,
+        [typeof(UploadCheck)] = UploadChecks,
     };
 
     private readonly IMongoDatabase? _database;
@@ -106,8 +108,15 @@ public sealed class DppStore
                 Builders<HandOver>.IndexKeys.Ascending(handOver => handOver.ToClusterId).Ascending(handOver => handOver.State),
                 new CreateIndexOptions { Name = "to_state" }),
         ], cancellationToken);
-        await Collection<UploadRecord>().Indexes.CreateOneAsync(
+        await Collection<UploadRecord>().Indexes.CreateManyAsync(
+        [
             new CreateIndexModel<UploadRecord>(Builders<UploadRecord>.IndexKeys.Descending(upload => upload.AtUtc), new CreateIndexOptions { Name = "at" }),
+            new CreateIndexModel<UploadRecord>(Builders<UploadRecord>.IndexKeys.Ascending("Batteries.PackId").Descending(upload => upload.AtUtc), new CreateIndexOptions { Name = "pack_at" }),
+        ], cancellationToken);
+        await Collection<UploadCheck>().Indexes.CreateOneAsync(
+            new CreateIndexModel<UploadCheck>(
+                Builders<UploadCheck>.IndexKeys.Ascending(check => check.ExpiresAtUtc),
+                new CreateIndexOptions { Name = "expires", ExpireAfter = TimeSpan.Zero }),
             cancellationToken: cancellationToken);
     }
 
@@ -132,6 +141,21 @@ public sealed class DppStore
         return await Collection<UploadRecord>().Find(Builders<UploadRecord>.Filter.Empty).SortByDescending(upload => upload.AtUtc).Limit(limit).ToListAsync(cancellationToken);
     }
 
+    /// <summary>The latest uploads that named this battery, newest first (step 5c).</summary>
+    public async Task<IReadOnlyList<UploadRecord>> ListUploadsForPackAsync(string packId, int limit, CancellationToken cancellationToken = default)
+    {
+        if (_database is null)
+        {
+            return [];
+        }
+
+        return await Collection<UploadRecord>()
+            .Find(Builders<UploadRecord>.Filter.ElemMatch(upload => upload.Batteries, battery => battery.PackId == packId))
+            .SortByDescending(upload => upload.AtUtc)
+            .Limit(limit)
+            .ToListAsync(cancellationToken);
+    }
+
     public async Task<T?> GetAsync<T>(string id, CancellationToken cancellationToken = default)
         where T : class, IDppRecord
     {
diff --git a/web/Dpp/Uploads/UploadService.cs b/web/Dpp/Uploads/UploadService.cs
index df1511b..266d5b4 100644
--- a/web/Dpp/Uploads/UploadService.cs
+++ b/web/Dpp/Uploads/UploadService.cs
@@ -15,6 +15,7 @@ namespace BatteryPassWeb.Dpp.Uploads;
 /// <param name="Problem">Why the battery was refused as a whole; null when it wasn't.</param>
 /// <param name="Modules">What happened to the battery's module list, when the file had one; null otherwise.</param>
 /// <param name="Notes">What the file's adapter wants the sender to know, e.g. a value it couldn't read.</param>
+/// <param name="Sent">The values the battery's entry sent, as JSON text, by key (step 5c).</param>
 public sealed record UploadedBatteryReport(
     int Entry,
     string NamedAs,
@@ -24,11 +25,14 @@ public sealed record UploadedBatteryReport(
     string? Problem,
     IReadOnlyList<KeyResult> Values,
     string? Modules = null,
-    IReadOnlyList<string>? Notes = null);
+    IReadOnlyList<string>? Notes = null,
+    string? ClusterId = null,
+    IReadOnlyDictionary<string, string?>? Sent = null);
 
 /// <summary>An upload's result: per battery what happened, or why the file couldn't be read at all.</summary>
 /// <param name="UploadId">The log entry; null for a preview or an unreadable file, which aren't logged.</param>
-public sealed record UploadReport(string? Format, bool Preview, IReadOnlyList<UploadedBatteryReport> Batteries, string? Problem, string? UploadId)
+/// <param name="CheckId">The checked file kept for "Save" on a battery's page (step 5c); null otherwise.</param>
+public sealed record UploadReport(string? Format, bool Preview, IReadOnlyList<UploadedBatteryReport> Batteries, string? Problem, string? UploadId, string? CheckId = null)
 {
     /// <summary>True when at least one battery had a value saved (or, in a preview, would have).</summary>
     public bool AnySaved => Batteries.Any(battery => battery.Outcome == UploadService.Saved);
@@ -62,6 +66,21 @@ public sealed class UploadService(
     public const string Ambiguous = "more than one battery has this serial number; use the battery ID";
     public const string NamedTwice = "the file names this battery more than once";
     public const string InTheFuture = "measuredAt lies in the future";
+    public const string TooLarge = "the file is larger than 2 MB";
+    public const string NotJson = "the file isn't a JSON file";
+    public const string CheckExpired = "the check has expired; choose the file again";
+
+    /// <summary>How long a checked file is kept for "Save" (decision 35).</summary>
+    public static readonly TimeSpan CheckKeptFor = TimeSpan.FromHours(1);
+
+    /// <summary>How many earlier uploads a battery's page lists.</summary>
+    public const int HistoryLength = 5;
+
+    /// <summary>Why a file on a battery's page is refused: it names another battery.</summary>
+    public static string NotThisBattery(string namedAs) => $"the file is for battery {namedAs}, not this one";
+
+    /// <summary>Why a file on a battery's page is refused: it names several batteries.</summary>
+    public static string OneBatteryOnly(int count) => $"the file lists {count} batteries; a battery's page takes a file for that battery only";
 
     private readonly IUploadAdapter[] _adapters = adapters.ToArray();
 
@@ -113,13 +132,13 @@ public sealed class UploadService(
             var notes = entry.Notes ?? [];
             if (problem is not null || pack is null || twice.Contains(pack.Id))
             {
-                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target?.Identifier, pack?.Id, Refused, problem ?? NamedTwice, [], Notes: notes));
+                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target?.Identifier, pack?.Id, Refused, problem ?? NamedTwice, [], Notes: notes, ClusterId: Cluster(pack)));
                 continue;
             }
 
             if (await store.PassportStatusAsync(pack.Id, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
             {
-                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, SavePipeline.NoLongerChanges(status), [], Notes: notes));
+                reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, Refused, SavePipeline.NoLongerChanges(status), [], Notes: notes, ClusterId: Cluster(pack)));
                 continue;
             }
 
@@ -134,7 +153,8 @@ public sealed class UploadService(
             }
 
             var outcome = result.Keys.Any(key => key.Outcome == KeyOutcome.Saved) || modules is { Changed: true } ? Saved : NothingSaved;
-            reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, result.Keys, modules?.Words, notes));
+            var sent = values.ToDictionary(pair => pair.Key, pair => pair.Value?.ToJsonString(), StringComparer.Ordinal);
+            reports.Add(new UploadedBatteryReport(entry.Number, entry.NamedAs, target!.Identifier, pack.Id, outcome, null, result.Keys, modules?.Words, notes, Cluster(pack), sent));
         }
 
         string? uploadId = null;
@@ -175,6 +195,131 @@ public sealed class UploadService(
         return (values, unused);
     }
 
+    /// <summary>
+    /// A file chosen on a battery's page (decision 35, step 5c): it must name that battery only, and is checked like a
+    /// preview. It is kept for an hour, so the page can show the check and its checker can "Save" it.
+    /// </summary>
+    public async Task<UploadReport> CheckBatteryFileAsync(ChangeSender sender, string packId, string fileName, byte[] content, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(sender);
+        ArgumentNullException.ThrowIfNull(content);
+        if (content.Length > MaxBytes)
+        {
+            return new UploadReport(null, true, [], TooLarge, null);
+        }
+
+        if (Parse(content) is not { } json)
+        {
+            return new UploadReport(null, true, [], NotJson, null);
+        }
+
+        var document = json is JsonObject root
+            ? _adapters.FirstOrDefault(adapter => adapter.Reads(root))?.Read(root) ?? UploadDocument.Unreadable(NotAnUpload)
+            : UploadDocument.Unreadable(NotAnUpload);
+        var problem = document.Problem
+            ?? (document.Entries.Count != 1 ? OneBatteryOnly(document.Entries.Count) : null)
+            ?? document.Entries[0].Problem;
+        if (problem is null)
+        {
+            var named = await batteries.FindAsync(sender, document.Entries[0].NamedAs, cancellationToken);
+            if (named.Pack?.Id != packId)
+            {
+                problem = NotThisBattery(document.Entries[0].NamedAs);
+            }
+            else if (await store.PassportStatusAsync(packId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
+            {
+                problem = SavePipeline.NoLongerChanges(status); // a read-only page has no upload section; this guards a stale form
+            }
+        }
+
+        if (problem is not null)
+        {
+            return new UploadReport(document.Format, true, [], problem, null);
+        }
+
+        var report = await RunAsync(sender, json, "page", fileName, preview: true, cancellationToken);
+        var check = new UploadCheck
+        {
+            Id = Guid.NewGuid().ToString("N"),
+            PackId = packId,
+            Who = sender.Who,
+            SentAs = sender.Kind.ToString(),
+            FileName = fileName,
+            Json = json.ToJsonString(),
+            ExpiresAtUtc = clock.GetUtcNow().UtcDateTime + CheckKeptFor,
+        };
+        await store.InsertAsync(check, cancellationToken);
+        return report with { CheckId = check.Id };
+    }
+
+    /// <summary>
+    /// Saves a checked file (step 5c): only for the person who checked it, on the same battery, within the hour. It is
+    /// run again, so the result is what the save pipeline says now. Null when it expired or isn't theirs.
+    /// </summary>
+    public async Task<UploadReport?> SaveCheckedAsync(ChangeSender sender, string packId, string? checkId, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(sender);
+        if (string.IsNullOrWhiteSpace(checkId) || await store.GetAsync<UploadCheck>(checkId, cancellationToken) is not { } check
+            || check.PackId != packId || check.Who != sender.Who || check.SentAs != sender.Kind.ToString()
+            || check.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
+        {
+            return null;
+        }
+
+        await store.DeleteAsync<UploadCheck>(check.Id, cancellationToken);
+        return await RunAsync(sender, JsonNode.Parse(check.Json), "page", check.FileName, preview: false, cancellationToken);
+    }
+
+    /// <summary>
+    /// A kept check shown again on its battery's page (step 5c): run as a preview now, as its checker (the team on the
+    /// team's page, the battery's cluster admin on theirs). Null when it expired or is another battery's.
+    /// </summary>
+    public async Task<(UploadCheck Check, UploadReport Report)?> ShowCheckAsync(BatteryPack pack, bool forTeam, string? checkId, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(pack);
+        if (string.IsNullOrWhiteSpace(checkId) || await store.GetAsync<UploadCheck>(checkId, cancellationToken) is not { } check
+            || check.PackId != pack.Id || check.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime
+            || check.SentAs != (forTeam ? nameof(SenderKind.Team) : nameof(SenderKind.ClusterAdmin)))
+        {
+            return null;
+        }
+
+        var sender = forTeam ? ChangeSender.Team(check.Who) : ChangeSender.ClusterAdmin(check.Who, Cluster(pack) ?? string.Empty);
+        return (check, await RunAsync(sender, JsonNode.Parse(check.Json), "page", check.FileName, preview: true, cancellationToken));
+    }
+
+    /// <summary>
+    /// The battery's latest uploads, each cut to this battery's line (step 5c, Q19). The team sees them all; a cluster
+    /// admin those sent for the battery's cluster (its admins and its battery system), not the team's.
+    /// </summary>
+    public async Task<IReadOnlyList<UploadRecord>> HistoryAsync(BatteryPack pack, bool forTeam, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(pack);
+        var packId = pack.Id;
+        var cluster = Cluster(pack);
+        var uploads = await store.ListUploadsForPackAsync(packId, HistoryLength * 4, cancellationToken);
+        return uploads
+            .Where(upload => forTeam
+                || (upload.SentAs != nameof(SenderKind.Team) && cluster is not null && upload.ClusterIds.Contains(cluster)))
+            .Take(HistoryLength)
+            .Select(upload => new UploadRecord
+            {
+                Id = upload.Id,
+                AtUtc = upload.AtUtc,
+                Who = upload.Who,
+                SentAs = upload.SentAs,
+                ClusterIds = upload.ClusterIds,
+                Channel = upload.Channel,
+                FileName = upload.FileName,
+                Format = upload.Format,
+                IsDemo = upload.IsDemo,
+                Batteries = upload.Batteries.Where(battery => battery.PackId == packId).ToList(),
+            })
+            .ToArray();
+    }
+
+    private static string? Cluster(BatteryPack? pack) => pack?.ClusterId is { } cluster ? ClusterRepository.NormalizeClusterId(cluster) : null;
+
     private static string Source(ChangeSender sender) => sender.Kind == SenderKind.BatterySystemFeed ? BatteryApi.FeedSource : "upload";
 
     /// <summary>
@@ -254,6 +399,7 @@ public sealed class UploadService(
                 Entry = report.Entry,
                 NamedAs = report.NamedAs,
                 PackId = report.PackId,
+                ClusterId = report.ClusterId,
                 Outcome = report.Outcome,
                 Problem = report.Problem,
                 Modules = report.Modules,
@@ -261,6 +407,7 @@ public sealed class UploadService(
                 Values = report.Values.Select(value => new UploadedValue
                 {
                     Key = value.Key,
+                    Value = report.Sent?.GetValueOrDefault(value.Key),
                     Outcome = value.Outcome.ToString(),
                     Message = string.IsNullOrEmpty(value.Message) ? null : value.Message,
                 }).ToList(),
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~UploadCheckTests | FullyQualifiedName~ResetCollectionsTests"`
Expected: `Passed!  - Failed:     0, Passed:    19`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs tools/V3Reset/ResetCollections.cs web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs web/Dpp/Uploads/UploadService.cs
git commit -m "feat(dpp): a file per battery, checked before saving, and the battery's upload history" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The "Upload a file" section on both battery pages

Mock-up B1–B4 on the redesigned pages (shots approved on 3 Nov 2026): a side-menu section after "Live values" on the team's and the cluster admin's battery pages, never on a read-only page. "Check the file" shows the check in the section (each value now, in the file, and what happens; the module list; the notes), then "Save" or "Start again"; after saving, one line and the upload open first among the earlier uploads. The modules section's "Fill from export" merges into it (Q18).

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Look/RedesignFixesTests.cs`
- Modify: `web/Controllers/BatteriesController.cs`
- Modify: `web/Controllers/ClusterBatteriesController.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Create: `web/Dpp/Pages/UploadPages.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/Batteries/_ModulesCard.cshtml`
- Create: `web/Views/Batteries/_UploadCard.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `5c-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`, `BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs`, `BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs`, `BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs`, `BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs`, `BatteryPassWeb.Tests/Look/RedesignFixesTests.cs`). Save the block as `../5c-task2-tests.patch`, outside the worktree, and run `git apply ../5c-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
index a5e18cf..4163ea1 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
@@ -193,6 +193,64 @@ public sealed class BatteriesControllerTests(MongoFixture mongo)
     /// <summary>The controller as a team member reaches it, with this form posted (also used by the status tests, step 4b).</summary>
     internal static BatteriesController Controller(PublicationRig rig, IReadOnlyDictionary<string, string> form) => Controller(rig, form.ToArray());
 
+    [Fact]
+    public async Task AFileOnTheBatterysPage_ShouldBeCheckedThenSavedAndListed()
+    {
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        var bytes = UploadCheckTests.Native(UploadServiceTests.Serial(rig), 41);
+        var checking = Controller(rig);
+
+        Assert.IsType<RedirectResult>(await checking.CheckUpload(rig.PackId, new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "readings.json"), CancellationToken.None));
+        var checkId = Assert.IsType<string>(checking.TempData[UploadPages.CheckKey]);
+        var showing = Controller(rig);
+        showing.TempData[UploadPages.CheckKey] = checkId;
+        var check = Assert.IsType<BatteryPage>(Assert.IsType<ViewResult>(await showing.Battery(rig.PackId, CancellationToken.None)).Model).Upload!.Check!;
+
+        Assert.Equal(("readings.json", "upload file", true), (check.FileName, check.FormatWords, check.WillChange));
+        Assert.Equal(new UploadValueRow("#63 State of Charge (SoC)", "76 %", "41 %", "Will be saved", false, false), Assert.Single(check.Values));
+
+        var saving = Controller(rig, ("checkId", checkId));
+        Assert.IsType<RedirectResult>(await saving.SaveUpload(rig.PackId, checkId, CancellationToken.None));
+        Assert.Equal("Saved: 1 value.", saving.TempData[BatteriesController.CardStatusKey]);
+        var after = Controller(rig);
+        after.TempData[UploadPages.OpenKey] = saving.TempData[UploadPages.OpenKey];
+        var card = Assert.IsType<BatteryPage>(Assert.IsType<ViewResult>(await after.Battery(rig.PackId, CancellationToken.None)).Model).Upload!;
+
+        Assert.Null(card.Check);
+        var row = Assert.Single(card.History);
+        Assert.Equal((row.Id, "Page", "team@example.com · readings.json", "1 value saved"), (card.OpenId, row.Channel, row.WhoAndWhat, row.Result));
+        Assert.Equal("Saved", Assert.Single(row.Values).Result);
+        Assert.Equal(41, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
+    }
+
+    [Fact]
+    public async Task AMarkedPassportsPage_ShouldHaveNoUploadSection()
+    {
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        var open = Assert.IsType<BatteryPage>(Assert.IsType<ViewResult>(await Controller(rig).Battery(rig.PackId, CancellationToken.None)).Model);
+        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures"));
+
+        var marked = Assert.IsType<BatteryPage>(Assert.IsType<ViewResult>(await Controller(rig).Battery(rig.PackId, CancellationToken.None)).Model);
+
+        Assert.Equal("upload", open.Sections[open.Sections.ToList().FindIndex(section => section.Id == "live") + 1].Id);
+        Assert.Null(marked.Upload);
+        Assert.DoesNotContain(marked.Sections, section => section.Id == "upload");
+    }
+
+    [Fact]
+    public async Task AFileForAnotherBattery_ShouldBeRefusedOnTheCard()
+    {
+        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
+        var bytes = UploadCheckTests.Native("VPC9-9999", 41);
+        var controller = Controller(rig);
+
+        await controller.CheckUpload(rig.PackId, new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "other.json"), CancellationToken.None);
+        await Controller(rig).CheckUpload(rig.PackId, null, CancellationToken.None);
+
+        Assert.Equal(("upload", "Not checked: the file is for battery VPC9-9999, not this one."), (controller.TempData[BatteriesController.CardKey], controller.TempData[BatteriesController.CardErrorKey]));
+        Assert.Null(controller.TempData[UploadPages.CheckKey]);
+    }
+
     private static BatteriesController Controller(PublicationRig rig, params KeyValuePair<string, string>[] form)
     {
         var http = new DefaultHttpContext
@@ -202,7 +260,7 @@ public sealed class BatteriesControllerTests(MongoFixture mongo)
                 "test")),
         };
         http.Request.Form = new FormCollection(form.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
-        return new BatteriesController(BatteryTesting.Pages(rig), PageTesting.Documents(rig), StatusChangeTests.Pages(rig), PassportMarkTests.Service(rig))
+        return new BatteriesController(BatteryTesting.Pages(rig), PageTesting.Documents(rig), StatusChangeTests.Pages(rig), PassportMarkTests.Service(rig), UploadCheckTests.Pages(rig))
         {
             ControllerContext = new ControllerContext { HttpContext = http },
             TempData = new TempDataDictionary(http, new NoTempDataStorage()),
diff --git a/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs b/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs
index 45c3f19..5c13dad 100644
--- a/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs
@@ -217,6 +217,55 @@ public sealed class ClusterBatteriesControllerTests(MongoFixture mongo)
         return form;
     }
 
+    [Fact]
+    public async Task TheClusterAdmin_ShouldUploadLiveValuesButNotTheTeamsValues()
+    {
+        var rig = await RigAsync();
+        var bytes = System.Text.Encoding.UTF8.GetBytes(UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 33, "warrantyPeriod": "P9Y" }""")));
+        var checking = Controller(rig, NorthAdmin, "clusterAdmin");
+
+        await checking.CheckUpload(rig.PackId, Upload("readings.json", bytes), CancellationToken.None);
+        var checkId = Assert.IsType<string>(checking.TempData[UploadPages.CheckKey]);
+        var showing = Controller(rig, NorthAdmin, "clusterAdmin");
+        showing.TempData[UploadPages.CheckKey] = checkId;
+        var card = Assert.IsType<ClusterBatteryPage>(Assert.IsType<ViewResult>(await showing.Battery(rig.PackId, CancellationToken.None)).Model).Upload!;
+        var saving = Controller(rig, NorthAdmin, "clusterAdmin");
+        await saving.SaveUpload(rig.PackId, checkId, CancellationToken.None);
+
+        Assert.False(card.ForTeam);
+        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}/upload", card.Action);
+        Assert.Equal(["Will be saved", "Not saved: #19 can't be changed by this sender"], card.Check!.Values.Select(row => row.Result));
+        Assert.Equal("Saved: 1 value.", saving.TempData[BatteriesController.CardStatusKey]);
+        Assert.Equal(33, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
+        Assert.IsType<NotFoundResult>(await Controller(rig, SouthAdmin, "clusterAdmin").CheckUpload(rig.PackId, Upload("readings.json", bytes), CancellationToken.None));
+    }
+
+    [Fact]
+    public async Task AMarkedPassport_ShouldOfferAndTakeNoUploadForItsClusterAdmin()
+    {
+        var rig = await RigAsync();
+        var bytes = UploadCheckTests.Native(UploadServiceTests.Serial(rig), 41);
+        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures"));
+
+        var page = Assert.IsType<ClusterBatteryPage>(Assert.IsType<ViewResult>(await Controller(rig, NorthAdmin, "clusterAdmin").Battery(rig.PackId, CancellationToken.None)).Model);
+        var check = await Controller(rig, NorthAdmin, "clusterAdmin").CheckUpload(rig.PackId, Upload("readings.json", bytes), CancellationToken.None);
+
+        Assert.True(page.ReadOnly);
+        Assert.Null(page.Upload);
+        Assert.DoesNotContain(page.Sections, section => section.Id == "upload");
+        Assert.IsType<NotFoundResult>(check);
+    }
+
+    [Fact]
+    public void BothBatteryPages_ShouldDrawTheUploadCardAfterTheLiveValues()
+    {
+        foreach (var page in new[] { DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"), DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml") })
+        {
+            var text = File.ReadAllText(page);
+            Assert.True(text.IndexOf("_LiveValuesCard", StringComparison.Ordinal) < text.IndexOf("_UploadCard", StringComparison.Ordinal));
+        }
+    }
+
     private static ClusterBatteriesController Controller(PublicationRig rig, string email, string role, IReadOnlyDictionary<string, string>? form = null)
     {
         var http = new DefaultHttpContext
@@ -224,7 +273,7 @@ public sealed class ClusterBatteriesControllerTests(MongoFixture mongo)
             User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
         };
         http.Request.Form = new FormCollection((form ?? new Dictionary<string, string>()).ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
-        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)))
+        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)), UploadCheckTests.Pages(rig))
         {
             ControllerContext = new ControllerContext { HttpContext = http },
             TempData = new TempDataDictionary(http, new NoTempDataStorage()),
diff --git a/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs b/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
index 814fad0..d011ae4 100644
--- a/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs
@@ -173,7 +173,7 @@ public sealed class HandOverPagesTests(MongoFixture mongo)
     }
 
     private static ClusterBatteriesController ClusterController(PublicationRig rig, string email) =>
-        With(new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context))), email);
+        With(new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)), UploadCheckTests.Pages(rig)), email);
 
     private static RegistryController RegistryController(PublicationRig rig, string email) =>
         With(new RegistryController(new DppReaders(new ClusterRepository(rig.Context)), BatteryTesting.Pages(rig)), email);
diff --git a/BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs b/BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs
index d473156..1781ecc 100644
--- a/BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs
@@ -154,7 +154,8 @@ public sealed class ModuleDataTests(MongoFixture mongo)
         var family = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "Family.cshtml"));
         var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
 
-        Assert.Contains("action=\"@Model.Href/modules/from-export\"", battery);
+        Assert.DoesNotContain("modules/from-export", battery); // merged into "Upload a file" (decision 35, Q18)
+        Assert.Contains("~/Views/Batteries/_UploadCard.cshtml", battery);
         Assert.Contains("action=\"@Model.Href/modules\"", battery);
         Assert.Contains("action=\"/admin/models/@selected.Id/module-description\"", family);
         Assert.Contains("Model.ModuleCount", hero);
diff --git a/BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs b/BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs
index 12c72cf..d4b762a 100644
--- a/BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs
@@ -211,7 +211,7 @@ public sealed class StatusPagesTests(MongoFixture mongo)
             User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, "clusterAdmin")], "test")),
         };
         http.Request.Form = new FormCollection(form.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
-        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)))
+        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)), UploadCheckTests.Pages(rig))
         {
             ControllerContext = new ControllerContext { HttpContext = http },
             TempData = new TempDataDictionary(http, new NoTempDataStorage()),
diff --git a/BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs b/BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs
index 7fe34cd..0ad4b97 100644
--- a/BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs
@@ -158,7 +158,7 @@ public sealed class SuccessionPagesTests(MongoFixture mongo)
             User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, NorthAdmin), new Claim(ClaimTypes.Role, "clusterAdmin")], "test")),
         };
         http.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
-        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)))
+        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)), UploadCheckTests.Pages(rig))
         {
             ControllerContext = new ControllerContext { HttpContext = http },
             TempData = new TempDataDictionary(http, new NoTempDataStorage()),
diff --git a/BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs b/BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs
index d3eeda6..79dec29 100644
--- a/BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs
@@ -1,7 +1,9 @@
 using System.Text;
 using BatteryPassWeb.Dpp.Editing;
+using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Store;
 using BatteryPassWeb.Dpp.Uploads;
+using BatteryPassWeb.Dpp.Values;
 
 namespace BatteryPassWeb.Tests.Dpp;
 
@@ -127,6 +129,9 @@ public sealed class UploadCheckTests(MongoFixture mongo)
         Assert.Equal(["north-feed"], clusterAdmins.Select(upload => upload.Who)); // not the team's (Q19)
     }
 
+    internal static UploadPages Pages(PublicationRig rig) =>
+        new(UploadServiceTests.Service(rig), AttributeFormsTests.Forms, new AttributeValueNormalizer(DppStoreTesting.Schemas));
+
     internal static byte[] Native(string serial, int stateOfCharge) =>
         Encoding.UTF8.GetBytes(UploadServiceTests.File(UploadServiceTests.Entry(serial, $$"""{ "stateOfChargeSoC": {{stateOfCharge}} }""")));
 
diff --git a/BatteryPassWeb.Tests/Look/RedesignFixesTests.cs b/BatteryPassWeb.Tests/Look/RedesignFixesTests.cs
index d64fdeb..2be47ec 100644
--- a/BatteryPassWeb.Tests/Look/RedesignFixesTests.cs
+++ b/BatteryPassWeb.Tests/Look/RedesignFixesTests.cs
@@ -39,7 +39,7 @@ public sealed class RedesignFixesTests
 
     [Theory]
     [InlineData("Batteries", "_AccidentsCard.cshtml", "a PDF, PNG or JPEG", "@AppOptions.Value.MaxUploadBytes")]
-    [InlineData("Batteries", "_ModulesCard.cshtml", "a JSON file", "@BatteryPassWeb.Controllers.BatteriesController.MaxExportBytes")]
+    [InlineData("Batteries", "_UploadCard.cshtml", "a JSON file", "@BatteryPassWeb.Dpp.Uploads.UploadService.MaxBytes")] // "Fill from export" merged into "Upload a file" (step 5c)
     [InlineData("ProductData", "_EditorField.cshtml", "a PDF, PNG or JPEG", "@Options.Value.MaxUploadBytes")]
     [InlineData("ProductData", "Family.cshtml", "a PNG or JPEG", "@AppOptions.Value.MaxUploadBytes")]
     public void EveryUpload_ShouldSayWhichKindAndHowLargeAFileItTakes(string folder, string view, string kind, string limit)
@@ -55,7 +55,7 @@ public sealed class RedesignFixesTests
     public void TheAccidentAndModuleUploads_ShouldShowTheChosenFile()
     {
         // The script finds the chosen-file line through the form's data-upload-row; these two forms had none (point 13).
-        foreach (var view in new[] { "_AccidentsCard.cshtml", "_ModulesCard.cshtml" })
+        foreach (var view in new[] { "_AccidentsCard.cshtml", "_UploadCard.cshtml" })
         {
             var page = Read("web", "Views", "Batteries", view);
             var upload = page.IndexOf("enctype=\"multipart/form-data\"", StringComparison.Ordinal);
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~UploadCheckTests | FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~ClusterBatteriesControllerTests | FullyQualifiedName~ModuleDataTests | FullyQualifiedName~HandOverPagesTests | FullyQualifiedName~StatusPagesTests | FullyQualifiedName~SuccessionPagesTests | FullyQualifiedName~RedesignFixesTests"`
Expected: the build fails with CS0246 (for example `error CS0246 The type or namespace name 'UploadPages' could not be found`).

- [ ] **Step 3: Write the code**

Patch `5c-task2-code.patch` (`web/Controllers/BatteriesController.cs`, `web/Controllers/ClusterBatteriesController.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/Batteries/_ModulesCard.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../5c-task2-code.patch`, outside the worktree, and run `git apply ../5c-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/BatteriesController.cs b/web/Controllers/BatteriesController.cs
index 20c2b68..a7da4d2 100644
--- a/web/Controllers/BatteriesController.cs
+++ b/web/Controllers/BatteriesController.cs
@@ -2,6 +2,7 @@ using System.Globalization;
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Pages;
+using BatteryPassWeb.Dpp.Uploads;
 using BatteryPassWeb.Services;
 using Microsoft.AspNetCore.Authorization;
 using Microsoft.AspNetCore.Mvc;
@@ -14,7 +15,7 @@ namespace BatteryPassWeb.Controllers;
 /// </summary>
 [Authorize(Policy = "AdminOnly")]
 [Route("admin/batteries")]
-public sealed class BatteriesController(BatteryPages pages, DocumentService documents, StatusPages status, PassportMarks marks) : Controller
+public sealed class BatteriesController(BatteryPages pages, DocumentService documents, StatusPages status, PassportMarks marks, UploadPages uploads) : Controller
 {
     /// <summary>Which card of the battery page a message belongs to, so it shows where the action was taken.</summary>
     public const string CardKey = "BatteryCard";
@@ -60,10 +61,33 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
     [HttpGet("{packId}")]
     public async Task<IActionResult> Battery(string packId, CancellationToken cancellationToken)
     {
-        var page = await pages.BatteryAsync(packId, cancellationToken: cancellationToken);
+        var page = await WithUploadAsync(await pages.BatteryAsync(packId, cancellationToken: cancellationToken), cancellationToken);
         return page is null ? NotFound() : View(page);
     }
 
+    /// <summary>"Check the file" in the "Upload a file" section (decision 35, step 5c): the check shows in the section.</summary>
+    [HttpPost("{packId}/upload")]
+    [ValidateAntiForgeryToken]
+    [RequestSizeLimit(UploadService.MaxBytes + 65536)]
+    public async Task<IActionResult> CheckUpload(string packId, IFormFile? file, CancellationToken cancellationToken)
+    {
+        var (checkId, problem) = await uploads.CheckAsync(ChangeSender.Team(Who), packId, file?.FileName, file?.OpenReadStream(), file?.Length ?? 0, cancellationToken);
+        TempData[UploadPages.CheckKey] = checkId;
+        Card("upload", null, problem is null ? null : $"Not checked: {problem}.");
+        return Redirect($"{BatteryPages.BatteryHref(packId)}?section=upload");
+    }
+
+    /// <summary>"Save" after the check (step 5c): the kept file goes through the save pipeline as the team.</summary>
+    [HttpPost("{packId}/upload/save")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> SaveUpload(string packId, [FromForm] string? checkId, CancellationToken cancellationToken)
+    {
+        var (done, uploadId, problem) = await uploads.SaveAsync(ChangeSender.Team(Who), packId, checkId, cancellationToken);
+        TempData[UploadPages.OpenKey] = uploadId;
+        Card("upload", done, problem is null ? null : $"Not saved: {problem}.");
+        return Redirect($"{BatteryPages.BatteryHref(packId)}?section=upload");
+    }
+
     [HttpPost("{packId}")]
     [ValidateAntiForgeryToken]
     public Task<IActionResult> SaveValues(string packId, [FromForm] bool confirmed, CancellationToken cancellationToken) =>
@@ -93,8 +117,6 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
         return Redirect(BatteryPages.BatteryHref(packId));
     }
 
-    /// <summary>The largest JSON export taken for the module list; the battery system's are a few kilobytes.</summary>
-    public const long MaxExportBytes = 1024 * 1024;
 
     [HttpPost("{packId}/modules")]
     [ValidateAntiForgeryToken]
@@ -105,30 +127,6 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
         return Redirect($"{BatteryPages.BatteryHref(packId)}#modules");
     }
 
-    [HttpPost("{packId}/modules/from-export")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> ImportModules(string packId, IFormFile? file, CancellationToken cancellationToken)
-    {
-        string? problem;
-        if (file is null || file.Length == 0)
-        {
-            problem = "choose the battery system's JSON export first";
-        }
-        else if (file.Length > MaxExportBytes)
-        {
-            problem = "the file is larger than an export (1 MB)";
-        }
-        else
-        {
-            using var buffer = new MemoryStream();
-            await file.CopyToAsync(buffer, cancellationToken);
-            problem = await pages.ImportModulesAsync(Who, packId, buffer.ToArray(), cancellationToken);
-        }
-
-        Card("modules", problem is null ? "Modules filled from the export. The passport didn't change: modules aren't passport data." : null, problem is null ? null : $"The modules weren't filled: {problem}.");
-        return Redirect($"{BatteryPages.BatteryHref(packId)}#modules");
-    }
-
     [HttpPost("{packId}/publish")]
     [ValidateAntiForgeryToken]
     public async Task<IActionResult> Publish(string packId, CancellationToken cancellationToken)
@@ -261,6 +259,16 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
 
     private string Who => AccessControlService.CurrentEmail(User) is { Length: > 0 } email ? email : "admin";
 
+    /// <summary>
+    /// The page with its "Upload a file" section: the check kept by "Check the file" and the upload just saved. A page
+    /// that takes no changes (a marked or earlier passport) gets none.
+    /// </summary>
+    private async Task<BatteryPage?> WithUploadAsync(BatteryPage? page, CancellationToken cancellationToken) =>
+        page is null || page.Cards.FrozenAs is not null ? page : page with
+        {
+            Upload = await uploads.CardAsync(page.Pack, BatteryPages.BatteryHref(page.Pack.Id), forTeam: true, TempData[UploadPages.CheckKey] as string, TempData[UploadPages.OpenKey] as string, cancellationToken),
+        };
+
     private void Card(string card, string? status, string? error)
     {
         TempData[CardKey] = card;
@@ -306,18 +314,18 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
             case EditorOutcome.NotFound:
                 return NotFound();
             case EditorOutcome.NeedsConfirmation:
-                return View("Battery", await pages.BatteryAsync(packId, values: state, cancellationToken: cancellationToken));
+                return View("Battery", await WithUploadAsync(await pages.BatteryAsync(packId, values: state, cancellationToken: cancellationToken), cancellationToken));
             case EditorOutcome.Refused:
                 var refused = $"{done} {Words.Count(save.RefusedCount, "value")} not saved; {(save.RefusedCount == 1 ? "it's" : "they're")} marked below.{version}".TrimStart();
                 if (liveValues)
                 {
                     ViewData[CardKey] = "live";
                     ViewData[CardErrorKey] = refused;
-                    return View("Battery", await pages.BatteryAsync(packId, live: state, cancellationToken: cancellationToken));
+                    return View("Battery", await WithUploadAsync(await pages.BatteryAsync(packId, live: state, cancellationToken: cancellationToken), cancellationToken));
                 }
 
                 ViewData[ProductDataController.EditorErrorKey] = refused;
-                return View("Battery", await pages.BatteryAsync(packId, values: state, cancellationToken: cancellationToken));
+                return View("Battery", await WithUploadAsync(await pages.BatteryAsync(packId, values: state, cancellationToken: cancellationToken), cancellationToken));
             case EditorOutcome.NothingChanged:
                 if (liveValues)
                 {
diff --git a/web/Controllers/ClusterBatteriesController.cs b/web/Controllers/ClusterBatteriesController.cs
index 9253b50..2cee844 100644
--- a/web/Controllers/ClusterBatteriesController.cs
+++ b/web/Controllers/ClusterBatteriesController.cs
@@ -2,6 +2,7 @@ using BatteryPassWeb.Dpp.Access;
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Pages;
+using BatteryPassWeb.Dpp.Uploads;
 using BatteryPassWeb.Services;
 using Microsoft.AspNetCore.Authorization;
 using Microsoft.AspNetCore.Mvc;
@@ -15,7 +16,7 @@ namespace BatteryPassWeb.Controllers;
 /// </summary>
 [Authorize(Policy = "ClusterAdminOrAdmin")]
 [Route("cluster-admin/batteries")]
-public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppReaders readers) : Controller
+public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppReaders readers, UploadPages uploads) : Controller
 {
     private const string PageView = "~/Views/ClusterAdmin/Battery.cshtml";
 
@@ -33,7 +34,7 @@ public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppRea
             return Redirect(BatteryPages.BatteryHref(packId));
         }
 
-        var page = sender is null ? null : await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken);
+        var page = sender is null ? null : await WithUploadAsync(await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken), cancellationToken);
         return page is null ? NotFound() : View(PageView, page);
     }
 
@@ -68,7 +69,7 @@ public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppRea
             case EditorOutcome.Refused:
                 ViewData[BatteriesController.CardKey] = "live";
                 ViewData[BatteriesController.CardErrorKey] = $"{done} {Words.Count(save.RefusedCount, "value")} not saved; {(save.RefusedCount == 1 ? "it's" : "they're")} marked below.".TrimStart();
-                return View(PageView, await pages.BatteryAsync(sender, packId, new EditorState(post, save), cancellationToken));
+                return View(PageView, await WithUploadAsync(await pages.BatteryAsync(sender, packId, new EditorState(post, save), cancellationToken), cancellationToken));
             case EditorOutcome.NothingChanged:
                 TempData[BatteriesController.CardKey] = "live";
                 TempData[BatteriesController.CardInfoKey] = "Nothing changed.";
@@ -208,6 +209,46 @@ public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppRea
     }
 
     /// <summary>The signed-in user as a cluster admin; null for the team (it has its own page) and for anyone else.</summary>
+    /// <summary>"Check the file" in the "Upload a file" section (decision 35, step 5c), as the battery's cluster admin.</summary>
+    [HttpPost("{packId}/upload")]
+    [ValidateAntiForgeryToken]
+    [RequestSizeLimit(UploadService.MaxBytes + 65536)]
+    public async Task<IActionResult> CheckUpload(string packId, IFormFile? file, CancellationToken cancellationToken)
+    {
+        if (await ClusterAdminAsync(cancellationToken) is not { } sender || await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken) is not { ReadOnly: false })
+        {
+            return NotFound();
+        }
+
+        var (checkId, problem) = await uploads.CheckAsync(sender, packId, file?.FileName, file?.OpenReadStream(), file?.Length ?? 0, cancellationToken);
+        TempData[UploadPages.CheckKey] = checkId;
+        Card("upload", null, problem is null ? null : $"Not checked: {problem}.");
+        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}?section=upload");
+    }
+
+    /// <summary>"Save" after the check (step 5c): the kept file goes through the save pipeline as the cluster admin.</summary>
+    [HttpPost("{packId}/upload/save")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> SaveUpload(string packId, [FromForm] string? checkId, CancellationToken cancellationToken)
+    {
+        if (await ClusterAdminAsync(cancellationToken) is not { } sender || await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken) is not { ReadOnly: false })
+        {
+            return NotFound();
+        }
+
+        var (done, uploadId, problem) = await uploads.SaveAsync(sender, packId, checkId, cancellationToken);
+        TempData[UploadPages.OpenKey] = uploadId;
+        Card("upload", done, problem is null ? null : $"Not saved: {problem}.");
+        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}?section=upload");
+    }
+
+    /// <summary>The page with its "Upload a file" section; a read-only page (a marked passport) gets none.</summary>
+    private async Task<ClusterBatteryPage?> WithUploadAsync(ClusterBatteryPage? page, CancellationToken cancellationToken) =>
+        page is null || page.ReadOnly ? page : page with
+        {
+            Upload = await uploads.CardAsync(page.Pack, ClusterBatteryPages.BatteryHref(page.Pack.Id), forTeam: false, TempData[UploadPages.CheckKey] as string, TempData[UploadPages.OpenKey] as string, cancellationToken),
+        };
+
     private async Task<ChangeSender?> ClusterAdminAsync(CancellationToken cancellationToken) =>
         await readers.EditorForAsync(User, cancellationToken) is { Kind: SenderKind.ClusterAdmin } sender ? sender : null;
 
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index 2922de4..cad16b7 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -59,6 +59,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<IUploadAdapter, NativeUploadFormat>();
         services.AddSingleton<IUploadAdapter, BmsExportFormat>();
         services.AddSingleton<UploadService>();
+        services.AddSingleton<UploadPages>();
         services.AddSingleton<PassportPages>();
         services.AddSingleton<AuditLines>();
         services.AddSingleton<ClusterHistory>();
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index df1e13b..7257f4c 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -124,6 +124,9 @@ public sealed record BatteryPage(
     /// <summary>The receiving cluster's name while a hand-over waits for its answer (step 4d); null otherwise.</summary>
     public string? HandOverTo { get; init; }
 
+    /// <summary>The "Upload a file" section (step 5c); the controller adds it, never to a read-only page.</summary>
+    public UploadCardView? Upload { get; init; }
+
     /// <summary>The overview's tiles: state of charge, cycles, temperature and capacity fade, with their last 7 days (redesign R4).</summary>
     public IReadOnlyList<LiveTile> Tiles { get; init; } = [];
 
@@ -176,6 +179,11 @@ public sealed record BatteryPage(
             }
 
             sections.Add(new("live", "Live values", "live", BatterySections.LiveHint(Pack, Live)));
+            if (Upload is { } upload)
+            {
+                sections.Add(new("upload", "Upload a file", "upload", upload.Hint)); // step 5c, mock-up B1
+            }
+
             sections.Add(new("accidents", "Accident reports", "accident", Count: AccidentReports.Count));
             sections.Add(new("modules", "Modules", "modules", Count: Pack.Modules.Count));
             sections.Add(new("versions", "Versions", "versions", Count: Versions.Count));
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index 221b784..85d6c1a 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -38,6 +38,9 @@ public sealed record ClusterBatteryPage(
     /// <summary>What happened while the battery was in this cluster, as its admin may read it (redesign R6), newest first.</summary>
     public IReadOnlyList<BatteryPassWeb.Dpp.Audit.AuditLine> History { get; init; } = [];
 
+    /// <summary>The "Upload a file" section (step 5c); the controller adds it, never to a read-only page.</summary>
+    public UploadCardView? Upload { get; init; }
+
     /// <summary>The mark that froze the passport (invalid, marked for deletion); null while it is active (redesign R6b).</summary>
     public string? FrozenAs { get; init; }
 
@@ -65,6 +68,11 @@ public sealed record ClusterBatteryPage(
             }
 
             sections.Add(new("live", "Live values", "live", BatterySections.LiveHint(Pack, Live)));
+            if (Upload is { } upload && !ReadOnly)
+            {
+                sections.Add(new("upload", "Upload a file", "upload", upload.Hint)); // step 5c, mock-up B4
+            }
+
             if (!ReadOnly) // a marked passport takes no report and can't be handed over; its reports stay on the Accident page
             {
                 sections.Add(new("accidents", "Accident reports", "accident", Count: AccidentReports.Count));
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index f9e505e..b2d868d 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -364,6 +364,12 @@
             <div data-section="live" hidden="@Hidden("live")">
                 @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
             </div>
+            @if (Model.Upload is { } upload)
+            {
+                <div data-section="upload" hidden="@Hidden("upload")">
+                    @await Html.PartialAsync("~/Views/Batteries/_UploadCard.cshtml", upload)
+                </div>
+            }
             <div data-section="accidents" hidden="@Hidden("accidents")">
                 @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
             </div>
diff --git a/web/Views/Batteries/_ModulesCard.cshtml b/web/Views/Batteries/_ModulesCard.cshtml
index 4329d90..b8e567b 100644
--- a/web/Views/Batteries/_ModulesCard.cshtml
+++ b/web/Views/Batteries/_ModulesCard.cshtml
@@ -8,11 +8,12 @@
         @if (Model.ForTeam)
         {
             <text>The public page shows how many modules the battery has, with the model's
-            <a href="@Model.ModuleDescriptionHref">module description</a>@(Model.HasModuleDescription ? string.Empty : " (not typed yet)").</text>
+            <a href="@Model.ModuleDescriptionHref">module description</a>@(Model.HasModuleDescription ? string.Empty : " (not typed yet)"). The battery
+            system's export sets this list through "Upload a file"; type or correct it here.</text>
         }
         else
         {
-            <text>The team keeps this list.</text>
+            <text>The battery system's export sets this list, through "Upload a file" or the API; the team corrects it.</text>
         }
         The module IDs are seen only by the battery's cluster, market surveillance and @Model.Team.
     </p>
@@ -41,20 +42,6 @@
     }
     else if (Model.ForTeam)
     {
-        <form method="post" action="@Model.Href/modules/from-export" enctype="multipart/form-data" class="bp-attr-upload bp-module-upload" data-upload-row>
-            @Html.AntiForgeryToken()
-            <div class="bp-attr-file">
-                <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
-                    Choose file
-                    <input type="file" name="file" class="bp-document-hidden-file" accept="application/json,.json" data-kind-words="a JSON file" data-max-bytes="@BatteryPassWeb.Controllers.BatteriesController.MaxExportBytes" data-upload-file />
-                </label>
-                <span class="bp-help-text" data-upload-chosen>No file chosen</span>
-            </div>
-            <div class="bp-live-save">
-                <small class="bp-field-help">The battery system's JSON export of this battery. It replaces the list.</small>
-                <button type="submit" class="bp-secondary-button" data-upload-button>Fill from export</button>
-            </div>
-        </form>
         var listCard = (TempData.Peek(BatteryPassWeb.Controllers.BatteriesController.CardKey) ?? ViewData[BatteryPassWeb.Controllers.BatteriesController.CardKey]) as string;
         <details class="bp-module-edit" open="@(listCard == "module-list")">
             <summary>Type or correct the list</summary>
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 57c190b..9dbc385 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -149,6 +149,12 @@
             <div data-section="live" hidden="@Hidden("live")">
                 @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
             </div>
+            @if (Model.Upload is { } upload)
+            {
+                <div data-section="upload" hidden="@Hidden("upload")">
+                    @await Html.PartialAsync("~/Views/Batteries/_UploadCard.cshtml", upload)
+                </div>
+            }
             @if (!Model.ReadOnly)
             {
                 <div data-section="accidents" hidden="@Hidden("accidents")">
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index b5496ec..0735d5c 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3716,3 +3716,91 @@ main .bp-field-marker.is-published {
 .bp-one-line-list .bp-icon-action-row > .bp-report-action:last-child:is(:hover, :focus-visible)::after {
   transform: translateY(0);
 }
+
+/* ==== Uploads on each battery's page (step 5c) =====================================================================
+   The few new pieces step 5 adds to bp-product-data.css: the check's value table, its outcome tags and the notes.
+   Everything else is the app's existing classes. */
+
+.bp-upload-head {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: baseline;
+  gap: 4px 12px;
+}
+
+.bp-soft-tag.is-refused {
+  border-color: #fca5a5;
+  color: #991b1b;
+}
+
+.bp-soft-tag.is-saved {
+  border-color: #86efac;
+  color: #166534;
+}
+
+/* The values: what the battery has now, what the file brings, and what happens. */
+.bp-upload-values {
+  width: 100%;
+  margin-top: 8px;
+  font-size: 0.92rem;
+}
+
+.bp-upload-values th {
+  font-weight: 600;
+  color: var(--bp-muted);
+}
+
+.bp-upload-values th,
+.bp-upload-values td {
+  padding: 5px 10px 5px 0;
+  vertical-align: top;
+}
+
+.bp-upload-values .is-refused {
+  color: #991b1b;
+}
+
+.bp-upload-notes {
+  margin: 8px 0 0;
+  padding-left: 18px;
+  color: var(--bp-muted);
+  font-size: 0.9rem;
+}
+
+@media (max-width: 640px) {
+  .bp-upload-values thead {
+    display: none;
+  }
+
+  .bp-upload-values tr {
+    display: grid;
+    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
+    padding: 6px 0;
+    border-bottom: 1px solid var(--bp-border);
+  }
+
+  .bp-upload-values td:first-child,
+  .bp-upload-values td:last-child {
+    grid-column: 1 / -1;
+  }
+
+  .bp-upload-values td:first-child {
+    font-weight: 600;
+  }
+
+  .bp-upload-values td[data-label]::before {
+    content: attr(data-label);
+    display: block;
+    color: var(--bp-muted);
+    font-size: 0.8rem;
+  }
+}
+
+[data-theme="dark"] .bp-soft-tag.is-refused,
+[data-theme="dark"] .bp-upload-values .is-refused {
+  color: #fca5a5;
+}
+
+[data-theme="dark"] .bp-soft-tag.is-saved {
+  color: #86efac;
+}
```

`web/Dpp/Pages/UploadPages.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>One value in the check or the history: the attribute, its value now and in the file, and what happens.</summary>
/// <param name="Quiet">An empty or unknown value, shown muted.</param>
public sealed record UploadValueRow(string Label, string Now, string InFile, string Result, bool Refused, bool Quiet);

/// <summary>The check before saving (mock-up B2): what the kept file would do to this battery.</summary>
/// <param name="Problem">Why the battery as a whole would be refused (e.g. a passport no longer active); null otherwise.</param>
public sealed record UploadCheckView(
    string CheckId,
    string FileName,
    string FormatWords,
    bool WillChange,
    string? Problem,
    IReadOnlyList<UploadValueRow> Values,
    string? Modules,
    IReadOnlyList<string> Notes);

/// <summary>One earlier upload of the battery (mock-up B1, B3), cut to this battery's line.</summary>
public sealed record UploadHistoryRow(
    string Id,
    string When,
    string Channel,
    string WhoAndWhat,
    string Result,
    string? Problem,
    IReadOnlyList<UploadValueRow> Values,
    string? Modules,
    IReadOnlyList<string> Notes);

/// <summary>The "Upload a file" card of a battery page (decision 35, step 5c, mock-up B1–B4).</summary>
/// <param name="Action">Where the card's forms post: the battery page plus "/upload".</param>
/// <param name="OpenId">The upload to show open in the history, just after it was saved.</param>
public sealed record UploadCardView(string Action, bool ForTeam, UploadCheckView? Check, IReadOnlyList<UploadHistoryRow> History, string? OpenId)
{
    /// <summary>"Start again": the battery page at the card, without the check.</summary>
    public string AgainHref => $"{Action[..^"/upload".Length]}?section=upload";

    /// <summary>The side menu's hint: when the last upload was, or what the section takes.</summary>
    public string Hint => History.Count > 0 ? $"Last {History[0].When}" : "A JSON file for this battery";
}

/// <summary>
/// The "Upload a file" card on both battery pages (decision 35, step 5c): the check of a kept file and the battery's
/// earlier uploads, in words. The checking and saving are <see cref="UploadService"/>'s.
/// </summary>
public sealed class UploadPages(UploadService uploads, AttributeForms forms, AttributeValueNormalizer normalizer)
{
    /// <summary>The kept check to show, across the redirect after "Check the file".</summary>
    public const string CheckKey = "UploadCheck";

    /// <summary>The upload just saved, shown open in the history.</summary>
    public const string OpenKey = "UploadOpen";

    public async Task<UploadCardView> CardAsync(BatteryPack pack, string href, bool forTeam, string? checkId, string? openId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        UploadCheckView? check = null;
        if (await uploads.ShowCheckAsync(pack, forTeam, checkId, cancellationToken) is { } shown && shown.Report.Batteries.Count == 1)
        {
            var battery = shown.Report.Batteries[0];
            check = new UploadCheckView(
                shown.Check.Id,
                shown.Check.FileName,
                FormatWords(shown.Report.Format),
                battery.Outcome == UploadService.Saved,
                battery.Problem is null ? null : Capitalise(battery.Problem),
                Rows(pack, battery.Values.Select(value => (value.Key, value.Outcome, value.Message, battery.Sent?.GetValueOrDefault(value.Key))), preview: true),
                battery.Modules is null ? null : Capitalise(battery.Modules),
                (battery.Notes ?? []).Select(Capitalise).ToArray());
        }

        var history = (await uploads.HistoryAsync(pack, forTeam, cancellationToken))
            .Where(upload => upload.Batteries.Count == 1)
            .Select(upload =>
            {
                var battery = upload.Batteries[0];
                var values = Rows(pack, battery.Values.Select(value => (value.Key, Enum.Parse<KeyOutcome>(value.Outcome), value.Message ?? string.Empty, value.Value)), preview: false);
                var saved = battery.Values.Count(value => value.Outcome == nameof(KeyOutcome.Saved));
                var result = battery.Outcome == UploadService.Refused
                    ? "Refused"
                    : saved == 0 && battery.Modules?.Contains("was set", StringComparison.Ordinal) != true ? "Nothing saved" : $"{Words.Count(saved, "value")} saved";
                return new UploadHistoryRow(
                    upload.Id,
                    ValueWords.Time(upload.AtUtc),
                    upload.Channel == "api" ? "API" : "Page",
                    $"{upload.Who} · {upload.FileName ?? FormatWords(upload.Format)}",
                    result,
                    battery.Problem is null ? null : Capitalise(battery.Problem),
                    values,
                    battery.Modules is null ? null : Capitalise(battery.Modules),
                    battery.Notes.Select(Capitalise).ToArray());
            })
            .ToArray();
        return new UploadCardView($"{href}/upload", forTeam, check, history, openId);
    }

    /// <summary>"Check the file" (mock-up B1): the kept check's ID, or why the file wasn't checked.</summary>
    public async Task<(string? CheckId, string? Problem)> CheckAsync(ChangeSender sender, string packId, string? fileName, Stream? content, long length, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (content is null || length == 0)
        {
            return (null, "choose the file first");
        }

        if (length > UploadService.MaxBytes)
        {
            return (null, UploadService.TooLarge);
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var report = await uploads.CheckBatteryFileAsync(sender, packId, Path.GetFileName(fileName ?? "file.json"), buffer.ToArray(), cancellationToken);
        return report.Problem is { } problem ? (null, problem) : (report.CheckId, null);
    }

    /// <summary>"Save" (mock-up B3): what was saved and the upload's ID, or why nothing was.</summary>
    public async Task<(string? Done, string? UploadId, string? Problem)> SaveAsync(ChangeSender sender, string packId, string? checkId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return await uploads.SaveCheckedAsync(sender, packId, checkId, cancellationToken) is { } report
            ? (SavedWords(report), report.UploadId, null)
            : (null, null, UploadService.CheckExpired);
    }

    /// <summary>"battery system export (BMS2_ACO 01.32.07)" or "upload file", for the format a file was read as.</summary>
    public static string FormatWords(string? format) => format switch
    {
        null or "" => "file",
        NativeUploadFormat.Format => "upload file",
        _ when format.StartsWith(BmsExportFormat.Mapping, StringComparison.Ordinal) => $"battery system export ({format})",
        _ => format,
    };

    /// <summary>The line after "Save": what was saved, in words.</summary>
    public static string SavedWords(UploadReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var battery = report.Batteries.FirstOrDefault();
        if (battery is null || battery.Outcome != UploadService.Saved)
        {
            return $"Nothing was saved{(battery?.Problem is { } problem ? $": {problem}" : string.Empty)}.";
        }

        var saved = battery.Values.Count(value => value.Outcome == KeyOutcome.Saved);
        var modules = battery.Modules?.Contains("was set", StringComparison.Ordinal) == true ? $", and {battery.Modules}" : string.Empty;
        return $"Saved: {Words.Count(saved, "value")}{modules}.";
    }

    private UploadValueRow[] Rows(BatteryPack pack, IEnumerable<(string Key, KeyOutcome Outcome, string Message, string? Sent)> values, bool preview) =>
        values.Select(value =>
        {
            var attribute = AttributeCatalog.FindByProperty(value.Key);
            var label = attribute is null ? value.Key : $"{attribute.Label} {attribute.Name}";
            var sent = value.Sent is null ? null : JsonNode.Parse(value.Sent);
            var result = value.Outcome switch
            {
                KeyOutcome.Saved => preview ? "Will be saved" : "Saved",
                KeyOutcome.KeptEmpty => "Kept: empty in the file",
                KeyOutcome.Ignored => "Ignored: not in the attribute list",
                _ => $"Not saved: {value.Message}",
            };
            return new UploadValueRow(
                label,
                attribute is null ? string.Empty : Text(attribute, pack.Values.GetValueOrDefault(value.Key)?.Value),
                attribute is null ? sent?.ToJsonString() ?? string.Empty : Text(attribute, sent is null ? null : normalizer.Normalize(attribute, sent)),
                result,
                value.Outcome == KeyOutcome.Rejected,
                value.Outcome is KeyOutcome.Ignored or KeyOutcome.KeptEmpty);
        }).ToArray();

    private string Text(AttributeDefinition attribute, JsonNode? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var lines = ValueWords.Lines(forms.For(attribute), value);
        return lines.Count == 0 ? value.ToJsonString() : string.Join("; ", lines.Select(line => line.Label is null ? line.Text : $"{line.Label}: {line.Text}"));
    }

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..] + (text.EndsWith('.') ? string.Empty : ".");
}
```

`web/Views/Batteries/_UploadCard.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model UploadCardView
@* "Upload a file" (decision 35, step 5c, mock-up B1–B4): one JSON file for this battery, checked before saving, and the
   battery's earlier uploads. On the team's battery page and on the cluster admin's. *@
@functions {
    private static string ResultClass(UploadValueRow row) => row.Refused ? "is-refused" : row.Quiet ? "text-secondary" : string.Empty;
}
<section class="bp-card bp-console-form-card bp-battery-card mt-4" id="upload">
    <span class="bp-summary-title">Upload a file</span>
    @if (Model.Check is { } check)
    {
        <div class="bp-upload-head">
            <h2 class="mb-0">Check before saving</h2>
            <span class="text-secondary">@check.FileName · @check.FormatWords</span>
            <span class="bp-soft-tag @(check.WillChange ? "is-saved" : "is-refused")">@(check.WillChange ? "Will change" : "Nothing to save")</span>
        </div>
        <p class="bp-help-text mt-2 mb-0">Nothing is saved yet. Live values change the passport in place; the battery's own values make a new version.</p>
        @if (check.Problem is { } problem)
        {
            <p class="mt-2 mb-0">@problem</p>
        }
        @if (check.Values.Count > 0)
        {
            <table class="bp-upload-values">
                <thead><tr><th>Value</th><th>Now</th><th>In the file</th><th>Result</th></tr></thead>
                <tbody>
                    @foreach (var row in check.Values)
                    {
                        <tr><td>@row.Label</td><td data-label="Now">@row.Now</td><td data-label="In the file">@row.InFile</td><td class="@ResultClass(row)">@row.Result</td></tr>
                    }
                </tbody>
            </table>
        }
        @if (check.Modules is { } modules)
        {
            <p class="bp-help-text mt-2 mb-0">@modules</p>
        }
        @if (check.Notes.Count > 0)
        {
            <ul class="bp-upload-notes">
                @foreach (var note in check.Notes)
                {
                    <li>@note</li>
                }
            </ul>
        }
        <form method="post" action="@Model.Action/save" class="bp-dialog-actions mt-3">
            @Html.AntiForgeryToken()
            <input type="hidden" name="checkId" value="@check.CheckId" />
            <a class="bp-secondary-button" href="@Model.AgainHref" data-upload-again>Start again</a>
            @if (check.WillChange)
            {
                <button type="submit" class="bp-primary-button">Save</button>
            }
        </form>
    }
    else
    {
        <h2>Values from a file</h2>
        <p class="mb-0">
            @if (Model.ForTeam)
            {
                <text>A JSON file for this battery: the app's upload format, or the battery system's export as it is (it also sets the module list).</text>
            }
            else
            {
                <text>A JSON file for this battery: your battery system's export as it is, or the app's upload format. It changes the live values and the module list.</text>
            }
            You see what would change before anything is saved. The help page has an example of each.
        </p>
        @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "upload")
        <form method="post" action="@Model.Action" enctype="multipart/form-data" class="bp-attr-upload bp-module-upload" data-upload-row>
            @Html.AntiForgeryToken()
            <div class="bp-attr-file">
                <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                    Choose file
                    <input type="file" name="file" class="bp-document-hidden-file" accept="application/json,.json" data-kind-words="a JSON file" data-max-bytes="@BatteryPassWeb.Dpp.Uploads.UploadService.MaxBytes" data-upload-file />
                </label>
                <span class="bp-help-text" data-upload-chosen>No file chosen</span>
            </div>
            <div class="bp-live-save">
                <small class="bp-field-help">One file, up to 2 MB, for this battery only.</small>
                <button type="submit" class="bp-secondary-button" data-upload-button>Check the file</button>
            </div>
        </form>
    }
    @if (Model.History.Count > 0)
    {
        <h3 class="h6 mt-4 mb-2">Earlier uploads</h3>
        <section class="bp-list-card" style="--bp-row-columns: minmax(0, 1.3fr) minmax(0, 1.7fr) minmax(0, 1.2fr) 96px" aria-label="Earlier uploads">
            @foreach (var upload in Model.History)
            {
                <details class="bp-row" open="@(upload.Id == Model.OpenId)" data-upload-row="@upload.Id">
                    <summary>
                        <span class="bp-row-title"><svg class="bp-i" aria-hidden="true"><use href="#i-upload" /></svg><strong>@upload.When</strong><code>@upload.Channel</code></span>
                        <span><span class="bp-row-label">Who and what</span>@upload.WhoAndWhat</span>
                        <span><span class="bp-row-label">Result</span>@upload.Result</span>
                        <span class="bp-row-open"><span class="is-open">Open</span><span class="is-close">Close</span></span>
                    </summary>
                    <div class="bp-row-panel bp-console-form-card">
                        <section class="bp-subcard">
                            @if (upload.Problem is { } refused)
                            {
                                <p class="mb-0">@refused</p>
                            }
                            @if (upload.Values.Count > 0)
                            {
                                <table class="bp-upload-values">
                                    <thead><tr><th>Value</th><th>In the file</th><th>Result</th></tr></thead>
                                    <tbody>
                                        @foreach (var row in upload.Values)
                                        {
                                            <tr><td>@row.Label</td><td data-label="In the file">@row.InFile</td><td class="@ResultClass(row)">@row.Result</td></tr>
                                        }
                                    </tbody>
                                </table>
                            }
                            @if (upload.Modules is { } uploadModules)
                            {
                                <p class="bp-help-text mt-2 mb-0">@uploadModules</p>
                            }
                            @if (upload.Notes.Count > 0)
                            {
                                <ul class="bp-upload-notes">
                                    @foreach (var note in upload.Notes)
                                    {
                                        <li>@note</li>
                                    }
                                </ul>
                            }
                        </section>
                    </div>
                </details>
            }
        </section>
    }
</section>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~UploadCheckTests | FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~ClusterBatteriesControllerTests | FullyQualifiedName~ModuleDataTests | FullyQualifiedName~HandOverPagesTests | FullyQualifiedName~StatusPagesTests | FullyQualifiedName~SuccessionPagesTests | FullyQualifiedName~RedesignFixesTests"`
Expected: `Passed!  - Failed:     0, Passed:    80`. The whole suite runs once, in the final checks.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs BatteryPassWeb.Tests/Dpp/UploadCheckTests.cs BatteryPassWeb.Tests/Look/RedesignFixesTests.cs web/Controllers/BatteriesController.cs web/Controllers/ClusterBatteriesController.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Dpp/Pages/UploadPages.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_ModulesCard.cshtml web/Views/Batteries/_UploadCard.cshtml web/Views/ClusterAdmin/Battery.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): the Upload a file section on both battery pages" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2481, Skipped:     0, Total:  2481`. From the 2469 before: 12 new tests.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat ed8782e -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. The managing session brings them to the product owner:

1. **Restart the app with the usual launcher**, sign in with a team account and press "Reset demo data" at the end of the Clusters tab. The app's start makes the collection `dpp_upload_checks`.
2. **As the team, DEMO-C7M-0001's page** (Batteries, then the battery): "Upload a file" in the side menu, after "Live values".
   - "Choose file" with a battery system export whose serial number is changed to `DEMO-C7M-0001`, then "Check the file": the values now and in the file, "The module list would be set: … modules", and the notes (no pack temperature yet; the manufacturing date in the file isn't used). "Save": one line, and the upload open first under "Earlier uploads". "Modules" in the side menu shows the export's list.
   - The same with a file for another battery: "Not checked: the file is for battery …, not this one."
   - "Modules" has no "Fill from export"; "Type or correct the list" is still there.
3. **DEMO-C7M-0003's page** (marked invalid by the demo data): no "Upload a file" in the side menu.
4. **As demo-north's cluster admin** (`demo.north.admin@example.test`), DEMO-C7M-0001's page ("Manage" on the Registry): the same section. A file with a warranty (#19) shows "Not saved: #19 can't be changed by this sender" beside the live values it saves; their earlier uploads don't list the team's.
5. **Light and dark, and a phone:** the moon button in the top bar switches; the section fits a phone's width.

## Next

5d: status changes and repairs by upload (on the battery's page only, by a person), the help page's sample files, and the demo example. Then step 5's wrap-up.
