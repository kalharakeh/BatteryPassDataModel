# DPP V3 Step 4b: The Status Change and Repairs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The team and the battery's cluster admins change a published battery's status (#22) from a "Change status" card on both battery pages (design 7.4, 7.6 and 7.9; decisions 15, 19 and 34). The window asks what happened, "Repaired" or "Waste". It then asks for the re-stated fades and lifetime (#59, #69, #74 where the model has it, #79, #81), filled in with the battery's current values. A repair joins the battery's repair history, and every change publishes a new version of the same passport. The team can also correct mistyped values. Both the team and the cluster admins can add a correcting note to the history. The detail page shows the history with the status. The demo data gets a repaired battery and a waste one. The three choices that lead to a new linked passport (remanufactured, repurposed, re-used and placed on the market again) come with 4c.

**Architecture:**
- **One service for the change:** `StatusChangeService` (`web/Dpp/Editing`).
  - It checks who may change the battery, that its passport is published and active, and the transition (`NewStatus`: waste is final; a repair makes "original" into "re-used" and leaves the others, J-15).
  - It checks a repair's day and description, and that the passport could get a new version now.
  - It then saves the status and the re-stated values together, and adds the repair to the history.
- **The save pipeline's own way in for it:** `SavePipeline.SaveStatusChangeAsync`.
  - Every other change set is still refused #22 and the re-stated values ("changes only through the status change").
  - This way in accepts only those keys, checks rights and values as usual, and is all or nothing (design 6.2, step 6).
  - Publication then makes the new version as for any static change.
  - The listener now gets the sender, so the version's log entry names a cluster admin when it is theirs (it always said "team").
- **The repair history is app data on the pack:** `BatteryPack.Repairs`, a list of `RepairEntry` items: day, what, who, when, team or cluster, and whether it is a correction note. It is not in the passport JSON and not signed; it is shown with the status to the same readers (design 7.9). Entries are never changed or removed (decision 34).
- **The pages:** `StatusPages` builds the card and reads the window for both battery pages. The window's fields are the editors' own (`_EditorField`), so values are read and refused in the same words. One partial, `_StatusCard.cshtml`, and one small script, `bp-status-change.js` (the two steps and the fade suggestion), serve both pages. The new styles are the approved mock-up's step-4 block at the end of `bp-product-data.css`; `site.css` is not changed.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 7.4, 7.6, 7.9 and section 14's step-4 note. Decisions record: decisions 15, 19 and 34; J-11, J-15. Mock-up (approved 31 Oct 2026): `docs/mockups/dpp-v3-step4-purchaser-and-lifecycle.html`, screens S2's status card, S3 and S6's repair history.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at a77015d (the end of plan 4a's run), plus the commit that adds this plan. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo).
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  2129`. After this plan: 2178. The suite takes about two minutes. Run the whole suite with `--blame-hang --blame-hang-timeout 10m`: if a run ever hangs (seen twice while plan 4a was measured, never in its run), the log names the test; stop it and run again.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../4b-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New and rewritten files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (1 Nov 2026, through the managing session)

Decision 34 holds step 4's answers; these are 4b's, added to it by this plan's commit.

1. **4b offers "Repaired" and "Waste" only.** The other three choices arrive in 4c with the linked passport, so no battery waits half-way between the two runs.
2. **"Correct the values", for the team only**, in the same window. The status stays, nothing joins the history, a new version is published, and the audit log records it as a correction. Cluster admins ask the team. It also fixes a waste battery's values.
3. **"Add a note to the history"**, for the team and the battery's cluster admins. It adds an entry marked "Correction", with who added it and when, and makes no new version (the history isn't passport data).
4. **Settled by the design:**
   - Only a published, active passport changes status.
   - Waste is final, and a waste battery isn't repaired.
   - Waste also asks for the re-stated values (decision 15).
   - A repair's day may be in the past, not the future.
   - A passport that couldn't get a new version now changes nothing.
   - The fade suggestion shows only when the model has a rated capacity (#57).
5. **Demo:** DEMO-C7M-0001 gets the repair that matches its accident report: re-used, version 3. A new published DEMO-CORE-0003 is set to waste. Both go through the app's own status change. The demo data now has 7 batteries, 4 of them published.

## Found while prototyping, and what this plan does

- **Fades are whole percentages in the official schema** (`percentageValue` is an integer), so a typed 6.5 is refused with the editors' usual words. The fade suggestion (J-11: 1 − remaining / rated capacity) rounds to a whole percent.
- **The pipeline refused #22 and the re-stated values everywhere**, which was right until now. Rather than loosen that rule, the status change gets its own way in. A test keeps the old refusal for every other change set.
- **The version log said "team" for every new version.** With cluster admins changing statuses, `IPackChangeListener.ValuesSavedAsync` now takes the `ChangeSender`, and `PublicationService` logs its kind.
- **The detail page doesn't load `bp-product-data.css`**, so its repair history uses the page's own value grid. It says "Recorded by the manufacturer's team" or "by the battery's cluster", not an e-mail address (mock-up S6).
- **A demo test run once failed with "I/O error" and passed on the next run** while this was prototyped. It looks like the same flakiness as the hangs, not this plan.

## What happens to the old tests

Nothing is removed. Some tests change with the code:
- **The listener's signature:** `SavePipelineListenerTests` (Task 1).
- **The page services' and the team controller's helpers**, which now take `StatusPages`: `BatteryTesting`, `ClusterBatteryPagesTests` and `BatteriesControllerTests` (Task 2).
- **The demo tests' counts**, from 6 batteries and 3 published to 7 and 4, with more versions (Task 3).

## A part to write yourself (optional)

The product owner chose "you write it" before; the plan gives the code. If they want to try one, the natural spot is `StatusChangeService.NewStatus` in Task 1: which status follows what happened. Task 1's `WhatHappened_ShouldSetTheStatusTheDesignSays` lists every case.

---

### Task 1: The status change, repairs, corrections and notes

The service behind the window, before the window: what happened sets the battery status, the re-stated values are saved with it through the save pipeline's own way in (all or nothing), a repair joins the battery's repair history, and publication makes the new version. The team alone corrects mistyped values; the team and the battery's cluster admins add correcting notes to the history. The version log now names the sender (the listener gets the `ChangeSender`).

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Editing/IPackChangeListener.cs`
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Create: `web/Dpp/Editing/StatusChangeService.cs`
- Modify: `web/Dpp/Publication/PublicationService.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Store/DppStore.cs`

- [ ] **Step 1: Write the failing tests**

Patch `4b-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`, `BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs`). Save the block as `../4b-task1-tests.patch`, outside the worktree, and run `git apply ../4b-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
index 9b08cd1..3f85c8f 100644
--- a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
@@ -41,6 +41,7 @@ public sealed class DppServiceWiringTests
         Assert.Same(provider.GetRequiredService<AttributeForms>(), provider.GetRequiredService<RecordEditor>().Forms);
         Assert.NotNull(provider.GetRequiredService<BatteryPages>());
         Assert.NotNull(provider.GetRequiredService<ClusterBatteryPages>());
+        Assert.NotNull(provider.GetRequiredService<StatusChangeService>());
         Assert.NotNull(provider.GetRequiredService<DppReaders>());
         Assert.NotNull(provider.GetRequiredService<ReaderPages>());
         Assert.NotNull(provider.GetRequiredService<PassportApi>());
diff --git a/BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs b/BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs
index 1fdd356..f4d7929 100644
--- a/BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs
@@ -43,9 +43,9 @@ public sealed class SavePipelineListenerTests(MongoFixture mongo)
     {
         public List<(TargetKind Target, string TargetId, string[] Keys, string Who)> Calls { get; } = [];
 
-        public Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, string who, CancellationToken cancellationToken)
+        public Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, ChangeSender sender, CancellationToken cancellationToken)
         {
-            Calls.Add((target, targetId, keys.ToArray(), who));
+            Calls.Add((target, targetId, keys.ToArray(), sender.Who));
             return Task.CompletedTask;
         }
     }
```

`BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The status change (design 7.4, 7.6 and 7.9, decisions 15, 19 and 34; step 4b): what happened sets the battery status
/// (#22), the fades and lifetime are re-stated with it, a repair joins the battery's repair history, and the passport
/// gets a new version. All or nothing.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class StatusChangeTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    [Fact]
    public void TheRestatedValues_ShouldBeTheFadesAndLifetimeWithRoundTripFadeOnlyWhereTheModelHasIt()
    {
        var with = new BatteryModel { Id = "m1", FamilyId = "f", Name = "With" };
        with.Values["energyRoundTripEfficiencyFade"] = new BatteryPassWeb.Dpp.Values.AttributeValue { Value = JsonNode.Parse("{\"percentageValue\":0,\"percentUnit\":\"%\"}")!, GeneratedAtUtc = DateTime.UtcNow, Source = "form" };
        var without = new BatteryModel { Id = "m2", FamilyId = "f", Name = "Without" };

        Assert.Equal([59, 69, 74, 79, 81], StatusChangeService.RestatedAttributes(with).Select(attribute => attribute.Number));
        Assert.Equal([59, 69, 79, 81], StatusChangeService.RestatedAttributes(without).Select(attribute => attribute.Number));
    }

    [Theory]
    [InlineData("original", StatusEvent.Repaired, "re-used")]
    [InlineData("re-used", StatusEvent.Repaired, "re-used")]
    [InlineData("remanufactured", StatusEvent.Repaired, "remanufactured")]
    [InlineData("repurposed", StatusEvent.Repaired, "repurposed")]
    [InlineData("original", StatusEvent.Waste, "waste")]
    [InlineData("re-used", StatusEvent.Waste, "waste")]
    [InlineData("repurposed", StatusEvent.Waste, "waste")]
    [InlineData("waste", StatusEvent.Repaired, null)]
    [InlineData("waste", StatusEvent.Waste, null)]
    [InlineData("unknown", StatusEvent.Repaired, null)]
    [InlineData("original", StatusEvent.Corrected, "original")]
    [InlineData("re-used", StatusEvent.Corrected, "re-used")]
    [InlineData("waste", StatusEvent.Corrected, "waste")]
    public void WhatHappened_ShouldSetTheStatusTheDesignSays(string current, StatusEvent what, string? expected)
    {
        Assert.Equal(expected, StatusChangeService.NewStatus(current, what));
    }

    [Fact]
    public async Task ARepair_ShouldMakeTheBatteryReUsedRecordItAndPublishANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        rig.Clock.Now = rig.Clock.Now.AddHours(1);

        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));

        Assert.Null(result.Problem);
        Assert.Equal(2, result.Version!.Number);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("re-used", Status(pack));
        Assert.Equal(6, pack.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(12, pack.Values["expectedLifetimeInCalendarYears"].Value!.GetValue<int>()); // re-stated as it was
        var repair = Assert.Single(pack.Repairs);
        Assert.Equal("2026-10-20", repair.Date);
        Assert.Equal("Module 4 replaced after a coolant leak.", repair.Description);
        Assert.Equal("team@example.com", repair.RecordedBy);
        Assert.Equal(rig.Clock.Now.UtcDateTime, repair.RecordedAtUtc);
        Assert.Equal("re-used", result.Version.StaticCore["batteryPassportData"]!["identifiersAndProductData"]!["batteryStatus"]!["batteryStatusValues"]!.GetValue<string>());
        Assert.Equal(DppStatuses.Active, result.Version.DppStatus);
    }

    [Fact]
    public async Task ASecondRepair_ShouldKeepTheStatusAndAddToTheHistory()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));
        rig.Clock.Now = rig.Clock.Now.AddDays(1);

        var result = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "7", description: "Cooling plate resealed."));

        Assert.Null(result.Problem);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("re-used", Status(pack));
        Assert.Equal(["Module 4 replaced after a coolant leak.", "Cooling plate resealed."], pack.Repairs.Select(entry => entry.Description));
        Assert.Equal(3, result.Version!.Number);
    }

    [Fact]
    public async Task Waste_ShouldBeFinal()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        var waste = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await CurrentAsync(rig)));
        var after = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));

        Assert.Null(waste.Problem);
        Assert.Equal(DppStatuses.Active, waste.Version!.DppStatus); // the passport stays active (7.6)
        Assert.Equal(StatusChangeService.WasteIsFinal, after.Problem);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("waste", Status(pack));
        Assert.Empty(pack.Repairs);
        Assert.Equal(2, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task TheTeamsCorrection_ShouldReStateTheValuesKeepTheStatusAndAddNoHistory()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var values = await CurrentAsync(rig);
        values["powerFade"] = JsonValue.Create(3);

        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Corrected, values));

        Assert.Null(result.Problem);
        Assert.Equal(2, result.Version!.Number);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("original", Status(pack));
        Assert.Equal(3, pack.Values["powerFade"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Empty(pack.Repairs);
        var changed = await LastAsync(rig, StatusChangeService.ChangedEvent);
        Assert.Equal("Corrected", changed["metadata"]["what"].AsString);
        Assert.Contains("values corrected", changed["message"].AsString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACorrection_ShouldBeTheTeamsAndAlsoFixAWasteBattery()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await CurrentAsync(rig)));

        var byAdmin = await service.ChangeAsync(NorthAdmin, rig.PackId, new StatusChange(StatusEvent.Corrected, await CurrentAsync(rig)));
        var values = await CurrentAsync(rig);
        values["expectedLifetimeInCalendarYears"] = JsonValue.Create(0);
        var byTeam = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Corrected, values));

        Assert.Equal(StatusChangeService.OnlyTheTeamCorrects, byAdmin.Problem);
        Assert.Null(byTeam.Problem);
        Assert.Equal("waste", byTeam.NewStatus);
        Assert.Equal(3, byTeam.Version!.Number);
    }

    [Fact]
    public async Task ANote_ShouldJoinTheHistoryAsACorrectionWithoutANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));
        rig.Clock.Now = rig.Clock.Now.AddDays(1);

        var problem = await service.AddNoteAsync(NorthAdmin, rig.PackId, " The repair was on module 5, not module 4. ");

        Assert.Null(problem);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal([false, true], pack.Repairs.Select(entry => entry.IsCorrection));
        var note = pack.Repairs[1];
        Assert.Equal("The repair was on module 5, not module 4.", note.Description);
        Assert.Equal("north-admin@example.com", note.RecordedBy);
        Assert.Equal(nameof(SenderKind.ClusterAdmin), note.RecordedAs);
        Assert.Equal(rig.Clock.Now.UtcDateTime, note.RecordedAtUtc);
        Assert.Equal("2026-10-30", note.Date);
        Assert.Equal(2, await rig.Store.GetLatestVersionNumberAsync(rig.PackId)); // the repair's version, none for the note
        Assert.Equal("north-admin@example.com", (await LastAsync(rig, StatusChangeService.NoteAddedEvent))["actor"].AsString);
    }

    [Fact]
    public async Task ANote_ShouldNeedTextAPublishedPassportAndTheRightSender()
    {
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        Assert.Equal(StatusChangeService.NoteNeedsText, await service.AddNoteAsync(PublicationTesting.Team, rig.PackId, "  "));
        Assert.Equal(StatusChangeService.NotYours, await service.AddNoteAsync(ChangeSender.ClusterAdmin("south@example.com", BatteryTesting.South), rig.PackId, "A note."));
        Assert.Equal(StatusChangeService.OnlyPublished, await Service(draft).AddNoteAsync(PublicationTesting.Team, draft.PackId, "A note."));
        Assert.Equal(StatusChangeService.NoBattery, await service.AddNoteAsync(PublicationTesting.Team, "pack-unknown", "A note."));
        Assert.Empty((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs);
    }

    [Fact]
    public async Task TheBatterysClusterAdmin_ShouldChangeItsStatusAndThePublicationLogNameThem()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await Service(rig).ChangeAsync(NorthAdmin, rig.PackId, Repair(rig, capacityFade: "6"));

        Assert.Null(result.Problem);
        Assert.Equal("north-admin@example.com", Assert.Single((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs).RecordedBy);
        var published = await LastAsync(rig, BatteryPassWeb.Dpp.Publication.PublicationService.PublishedEvent);
        Assert.Equal("north-admin@example.com", published["actor"].AsString);
        Assert.Equal(nameof(SenderKind.ClusterAdmin), published["actorRole"].AsString);
        var changed = await LastAsync(rig, StatusChangeService.ChangedEvent);
        Assert.Equal("original", changed["metadata"]["from"].AsString);
        Assert.Equal("re-used", changed["metadata"]["to"].AsString);
        Assert.Equal("Repaired", changed["metadata"]["what"].AsString);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldChangeNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await Service(rig).ChangeAsync(ChangeSender.ClusterAdmin("south@example.com", BatteryTesting.South), rig.PackId, Repair(rig, capacityFade: "6"));

        Assert.Equal(StatusChangeService.NotYours, result.Problem);
        await AssertUnchangedAsync(rig);
    }

    [Fact]
    public async Task ADraft_ShouldNotChangeStatus()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));

        Assert.Equal(StatusChangeService.OnlyPublished, result.Problem);
        Assert.Empty((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs);
    }

    [Fact]
    public async Task AnUnknownBattery_ShouldNotBeFound()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.False((await Service(rig).ChangeAsync(PublicationTesting.Team, "pack-unknown", Repair(rig, capacityFade: "6"))).Found);
    }

    [Theory]
    [InlineData("140")]
    [InlineData("-3")]
    [InlineData("a lot")]
    [InlineData("")]
    public async Task ARefusedOrMissingRestatedValue_ShouldSaveNothing(string capacityFade)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade));

        Assert.Equal(StatusChangeService.ValuesRefused, result.Problem);
        Assert.Contains(result.Keys, key => key.Key == "capacityFade" && key.Outcome != KeyOutcome.Saved);
        await AssertUnchangedAsync(rig);
    }

    [Theory]
    [InlineData(null, "Module 4 replaced.", StatusChangeService.RepairNeedsDate)]
    [InlineData("2026-13-01", "Module 4 replaced.", StatusChangeService.RepairNeedsDate)]
    [InlineData("2099-01-01", "Module 4 replaced.", StatusChangeService.RepairNotInTheFuture)]
    [InlineData("2026-10-20", " ", StatusChangeService.RepairNeedsDescription)]
    public async Task ARepair_ShouldNeedItsDateAndWhatWasRepaired(string? date, string description, string problem)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var change = Repair(rig, capacityFade: "6") with { RepairDate = date, RepairDescription = description };

        Assert.Equal(problem, (await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, change)).Problem);
        await AssertUnchangedAsync(rig);
    }

    [Fact]
    public async Task TheSavePipeline_ShouldStillRefuseTheStatusAndFadesOutsideTheStatusChange()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(
            TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["batteryStatus"] = "waste", ["capacityFade"] = 9 }, "form"));

        Assert.All(result.Keys, key => Assert.Equal(KeyOutcome.Rejected, key.Outcome));
        await AssertUnchangedAsync(rig);
    }

    internal static StatusChangeService Service(PublicationRig rig) =>
        new(rig.Store, rig.Pipeline, rig.Publication, new ApplicationAuditService(rig.Context), rig.Clock);

    /// <summary>The battery's current re-stated values as the window fills them in, as text.</summary>
    internal static async Task<Dictionary<string, JsonNode?>> CurrentAsync(PublicationRig rig)
    {
        var sources = (await rig.Store.LoadSourcesAsync(rig.PackId))!;
        return StatusChangeService.RestatedAttributes(sources.Model).ToDictionary(
            attribute => attribute.Property,
            attribute => (sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values[attribute.Property]).Value?.DeepClone(),
            StringComparer.Ordinal);
    }

    private static StatusChange Repair(PublicationRig rig, string capacityFade, string description = "Module 4 replaced after a coolant leak.")
    {
        var values = CurrentAsync(rig).GetAwaiter().GetResult();
        values["capacityFade"] = capacityFade.Length == 0 ? null : (JsonNode?)JsonValue.Create(capacityFade);
        if (decimal.TryParse(capacityFade, out var number))
        {
            values["capacityFade"] = JsonValue.Create(number);
        }

        return new StatusChange(StatusEvent.Repaired, values, "2026-10-20", description);
    }

    private static string Status(BatteryPack pack) => pack.Values["batteryStatus"].Value!["batteryStatusValues"]!.GetValue<string>();

    private static async Task AssertUnchangedAsync(PublicationRig rig)
    {
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("original", Status(pack));
        Assert.Equal(4, pack.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Empty(pack.Repairs);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~StatusChangeTests | FullyQualifiedName~SavePipelineListenerTests | FullyQualifiedName~DppServiceWiringTests"`
Expected: the build fails with CS0103, CS0246, CS0535 (for example `error CS0103 The name 'StatusChangeService' does not exist in the current context`).

- [ ] **Step 3: Write the code**

Patch `4b-task1-code.patch` (`web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Editing/IPackChangeListener.cs`, `web/Dpp/Editing/SavePipeline.cs`, `web/Dpp/Publication/PublicationService.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Store/DppStore.cs`). Save the block as `../4b-task1-code.patch`, outside the worktree, and run `git apply ../4b-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index ae193f8..13a9367 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -32,6 +32,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<IIdentifierScheme, TestIdentifierScheme>();
         services.AddSingleton<SavePipeline>();
         services.AddSingleton<PackCreationService>();
+        services.AddSingleton<StatusChangeService>();
         services.AddSingleton<PassportSigner>();
         services.AddSingleton<PublicationChecks>();
         services.AddSingleton<PublicationService>();
diff --git a/web/Dpp/Editing/IPackChangeListener.cs b/web/Dpp/Editing/IPackChangeListener.cs
index 8475ded..24d6fc2 100644
--- a/web/Dpp/Editing/IPackChangeListener.cs
+++ b/web/Dpp/Editing/IPackChangeListener.cs
@@ -3,5 +3,6 @@ namespace BatteryPassWeb.Dpp.Editing;
 /// <summary>Told after the save pipeline stored values on a record; publication uses it to keep versions current.</summary>
 public interface IPackChangeListener
 {
-    Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, string who, CancellationToken cancellationToken);
+    /// <param name="sender">Who sent the change, so what follows from it (a new version) is logged in their name (step 4b).</param>
+    Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, ChangeSender sender, CancellationToken cancellationToken);
 }
diff --git a/web/Dpp/Editing/SavePipeline.cs b/web/Dpp/Editing/SavePipeline.cs
index 9ba9edf..0bcf3f9 100644
--- a/web/Dpp/Editing/SavePipeline.cs
+++ b/web/Dpp/Editing/SavePipeline.cs
@@ -72,7 +72,19 @@ public sealed class SavePipeline(
 
     private readonly IPackChangeListener[] _listeners = listeners.ToArray();
 
-    public async Task<SaveResult> SaveAsync(ChangeSender sender, ChangeSet changes, bool preview = false, CancellationToken cancellationToken = default)
+    public Task<SaveResult> SaveAsync(ChangeSender sender, ChangeSet changes, bool preview = false, CancellationToken cancellationToken = default) =>
+        SaveCoreAsync(sender, changes, preview, statusChange: false, cancellationToken);
+
+    /// <summary>
+    /// The status change's own way in (design 6.2 step 6 and 7.4, step 4b): the battery status (#22) and its re-stated
+    /// values, which every other change set is refused. Rights and values are checked as for any change, and it is all
+    /// or nothing: one refused or empty key saves none. Only <see cref="StatusChangeService"/> calls it, after its own
+    /// checks of the transition.
+    /// </summary>
+    public Task<SaveResult> SaveStatusChangeAsync(ChangeSender sender, string packId, IReadOnlyDictionary<string, JsonNode?> values, CancellationToken cancellationToken = default) =>
+        SaveCoreAsync(sender, new ChangeSet(TargetKind.Pack, packId, values, "form"), preview: false, statusChange: true, cancellationToken);
+
+    private async Task<SaveResult> SaveCoreAsync(ChangeSender sender, ChangeSet changes, bool preview, bool statusChange, CancellationToken cancellationToken)
     {
         ArgumentNullException.ThrowIfNull(sender);
         ArgumentNullException.ThrowIfNull(changes);
@@ -83,7 +95,7 @@ public sealed class SavePipeline(
             return new SaveResult(false, false, []);
         }
 
-        var evaluation = Evaluate(sender, changes.Target, target.ClusterId, changes.Values);
+        var evaluation = EvaluateCore(sender, changes.Target, target.ClusterId, changes.Values, statusChange);
         if (changes.Target == TargetKind.Pack)
         {
             evaluation = await CheckPackRulesAsync(changes.TargetId, evaluation, cancellationToken);
@@ -92,7 +104,8 @@ public sealed class SavePipeline(
         var removals = await EvaluateRemovalsAsync(sender, changes, target, cancellationToken);
         var results = evaluation.Results.Concat(removals).ToArray();
         var removed = removals.Where(result => result.Outcome == KeyOutcome.Removed).Select(result => result.Key).ToArray();
-        if (preview || (evaluation.Accepted.Count == 0 && removed.Length == 0))
+        if (preview || (evaluation.Accepted.Count == 0 && removed.Length == 0)
+            || (statusChange && results.Any(result => result.Outcome != KeyOutcome.Saved)))
         {
             return new SaveResult(true, false, results);
         }
@@ -119,7 +132,7 @@ public sealed class SavePipeline(
                 cancellationToken: cancellationToken);
             foreach (var listener in _listeners)
             {
-                await listener.ValuesSavedAsync(changes.Target, changes.TargetId, stored.Keys.Concat(removed).ToArray(), sender.Who, cancellationToken);
+                await listener.ValuesSavedAsync(changes.Target, changes.TargetId, stored.Keys.Concat(removed).ToArray(), sender, cancellationToken);
             }
         }
 
@@ -142,7 +155,7 @@ public sealed class SavePipeline(
         var results = new List<KeyResult>();
         foreach (var key in removals.Distinct(StringComparer.Ordinal))
         {
-            var (outcome, message, _) = EvaluateKey(sender, changes.Target, target.ClusterId, key, JsonValue.Create("remove"), checkValue: false);
+            var (outcome, message, _) = EvaluateKey(sender, changes.Target, target.ClusterId, key, JsonValue.Create("remove"), statusChange: false, checkValue: false);
             if (outcome != KeyOutcome.Saved)
             {
                 results.Add(new KeyResult(key, outcome, message));
@@ -252,7 +265,10 @@ public sealed class SavePipeline(
 
     /// <summary>Judges every key without saving anything; also used when a pack is created.</summary>
     /// <param name="packClusterId">The pack's cluster; null for other record kinds.</param>
-    public Evaluation Evaluate(ChangeSender sender, TargetKind target, string? packClusterId, IReadOnlyDictionary<string, JsonNode?> values)
+    public Evaluation Evaluate(ChangeSender sender, TargetKind target, string? packClusterId, IReadOnlyDictionary<string, JsonNode?> values) =>
+        EvaluateCore(sender, target, packClusterId, values, statusChange: false);
+
+    private Evaluation EvaluateCore(ChangeSender sender, TargetKind target, string? packClusterId, IReadOnlyDictionary<string, JsonNode?> values, bool statusChange)
     {
         ArgumentNullException.ThrowIfNull(sender);
         ArgumentNullException.ThrowIfNull(values);
@@ -260,7 +276,7 @@ public sealed class SavePipeline(
         var accepted = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
         foreach (var (key, raw) in values)
         {
-            var (outcome, message, value) = EvaluateKey(sender, target, packClusterId, key, raw);
+            var (outcome, message, value) = EvaluateKey(sender, target, packClusterId, key, raw, statusChange);
             results.Add(new KeyResult(key, outcome, message));
             if (outcome == KeyOutcome.Saved)
             {
@@ -271,9 +287,10 @@ public sealed class SavePipeline(
         return new Evaluation(results, accepted);
     }
 
+    /// <param name="statusChange">True for the status change's own keys (#22 and the re-stated values), which only it may send.</param>
     /// <param name="checkValue">False when only the attribute and the sender's rights are judged (a removal).</param>
     private (KeyOutcome Outcome, string Message, JsonNode? Value) EvaluateKey(
-        ChangeSender sender, TargetKind target, string? packClusterId, string key, JsonNode? raw, bool checkValue = true)
+        ChangeSender sender, TargetKind target, string? packClusterId, string key, JsonNode? raw, bool statusChange, bool checkValue = true)
     {
         if (AttributeCatalog.FindByProperty(key) is not { } attribute)
         {
@@ -290,9 +307,10 @@ public sealed class SavePipeline(
             return (KeyOutcome.Rejected, $"{attribute.Label} is stored on the {RecordName(attribute.Storage)}, not on the {target.ToString().ToLowerInvariant()}", null);
         }
 
-        if (target == TargetKind.Pack && (attribute.Change is ChangeRule.OnStatusChange or ChangeRule.RestatedOnStatusChange))
+        var throughStatusChange = target == TargetKind.Pack && attribute.Change is ChangeRule.OnStatusChange or ChangeRule.RestatedOnStatusChange;
+        if (throughStatusChange != statusChange)
         {
-            return (KeyOutcome.Rejected, $"{attribute.Label} changes only through the status change", null);
+            return (KeyOutcome.Rejected, statusChange ? $"{attribute.Label} isn't part of the status change" : $"{attribute.Label} changes only through the status change", null);
         }
 
         if (!EditRights.MayEdit(sender, attribute, packClusterId))
diff --git a/web/Dpp/Publication/PublicationService.cs b/web/Dpp/Publication/PublicationService.cs
index 7917db1..a43783c 100644
--- a/web/Dpp/Publication/PublicationService.cs
+++ b/web/Dpp/Publication/PublicationService.cs
@@ -50,13 +50,14 @@ public sealed class PublicationService(
     {
         ArgumentNullException.ThrowIfNull(sender);
         return sender.Kind == SenderKind.Team
-            ? PublishCoreAsync(packId, sender.Who, retryOnConflict: true, cancellationToken)
+            ? PublishCoreAsync(packId, sender, retryOnConflict: true, cancellationToken)
             : Task.FromResult(new PublicationResult(null, null, "only the team publishes passports"));
     }
 
-    public async Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, string who, CancellationToken cancellationToken)
+    public async Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, ChangeSender sender, CancellationToken cancellationToken)
     {
         ArgumentNullException.ThrowIfNull(keys);
+        ArgumentNullException.ThrowIfNull(sender);
         var packIds = target switch
         {
             TargetKind.Pack => [targetId],
@@ -83,12 +84,12 @@ public sealed class PublicationService(
                 continue;
             }
 
-            var result = await PublishCoreAsync(packId, who, retryOnConflict: true, cancellationToken);
+            var result = await PublishCoreAsync(packId, sender, retryOnConflict: true, cancellationToken);
             if (result.Problem is not null)
             {
                 await audit.AppendApplicationAuditEventAsync(
                     BlockedEvent,
-                    who,
+                    sender.Who,
                     "system",
                     "publication",
                     $"A new version of pack {packId} was blocked; version {active.Number} stays active",
@@ -100,8 +101,10 @@ public sealed class PublicationService(
         }
     }
 
-    private async Task<PublicationResult> PublishCoreAsync(string packId, string who, bool retryOnConflict, CancellationToken cancellationToken)
+    /// <param name="sender">Who the new version is published for; the log names them (a cluster admin's status change, step 4b).</param>
+    private async Task<PublicationResult> PublishCoreAsync(string packId, ChangeSender sender, bool retryOnConflict, CancellationToken cancellationToken)
     {
+        var who = sender.Who;
         var sources = await store.LoadSourcesAsync(packId, cancellationToken);
         if (sources is null)
         {
@@ -144,14 +147,14 @@ public sealed class PublicationService(
         catch (MongoWriteException exception) when (retryOnConflict && exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
         {
             // Another save published this pack at the same moment: start again from the new state, once.
-            return await PublishCoreAsync(packId, who, retryOnConflict: false, cancellationToken);
+            return await PublishCoreAsync(packId, sender, retryOnConflict: false, cancellationToken);
         }
 
         await store.ArchiveOtherVersionsAsync(packId, version.Id, cancellationToken);
         await audit.AppendApplicationAuditEventAsync(
             PublishedEvent,
             who,
-            "team",
+            sender.Kind.ToString(),
             "publication",
             $"Version {version.Number} of pack {packId} published",
             new BsonDocument
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index d6accfc..e666d2a 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -115,6 +115,33 @@ public sealed class BatteryPack : IDppRecord
 
     /// <summary>The pack's modules, by position (decision 31). App data, not passport data; empty until listed.</summary>
     public List<BatteryModule> Modules { get; set; } = [];
+
+    /// <summary>
+    /// The pack's repair history (design 7.9, decision 19), oldest first. App data, not passport data: it isn't in the
+    /// passport JSON and is shown with the battery status to the same readers. Entries are never changed or removed
+    /// (decision 34).
+    /// </summary>
+    public List<RepairEntry> Repairs { get; set; } = [];
+}
+
+/// <summary>One repair of a pack (design 7.9): when, what was repaired, and who recorded it when.</summary>
+public sealed class RepairEntry
+{
+    /// <summary>The day of the repair, as yyyy-MM-dd.</summary>
+    public required string Date { get; init; }
+
+    public required string Description { get; init; }
+
+    public required string RecordedBy { get; init; }
+
+    /// <summary>The team or the battery's cluster (a <c>SenderKind</c>): the detail page says which, not who.</summary>
+    public string RecordedAs { get; init; } = "Team";
+
+    /// <summary>A note that corrects the history (decision 34): entries are never changed, so a mistake gets one of these.</summary>
+    public bool IsCorrection { get; init; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public required DateTime RecordedAtUtc { get; init; }
 }
 
 /// <summary>One module of a pack, as the battery system's export lists it: its position and its IDs (decision 31).</summary>
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index b204d04..55544ec 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -179,6 +179,16 @@ public sealed class DppStore
         return result.MatchedCount == 1;
     }
 
+    /// <summary>Adds an entry to a pack's repair history (design 7.9). False when the pack doesn't exist.</summary>
+    public async Task<bool> AddRepairAsync(string packId, RepairEntry entry, CancellationToken cancellationToken = default)
+    {
+        var result = await Collection<BatteryPack>().UpdateOneAsync(
+            pack => pack.Id == packId,
+            Builders<BatteryPack>.Update.Push(pack => pack.Repairs, entry),
+            cancellationToken: cancellationToken);
+        return result.MatchedCount == 1;
+    }
+
     /// <summary>Replaces a pack's module list (decision 31). False when the pack doesn't exist.</summary>
     public async Task<bool> SetPackModulesAsync(string packId, IReadOnlyList<BatteryModule> modules, CancellationToken cancellationToken = default)
     {
```

`web/Dpp/Editing/StatusChangeService.cs`:

```csharp
using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>What happened to the battery, as the status window asks it (decision 34). 4c adds the three that lead to a new passport.</summary>
public enum StatusEvent
{
    /// <summary>A repair (design 7.9): "re-used" for an original battery, otherwise the status stays (J-15).</summary>
    Repaired,

    /// <summary>Waste (design 7.6): final; the passport stays active until the team marks it for deletion.</summary>
    Waste,

    /// <summary>
    /// The team corrects mistyped re-stated values (decision 34): the status stays, nothing joins the history, and a new
    /// version is published. Cluster admins ask the team.
    /// </summary>
    Corrected,
}

/// <summary>One status change: what happened, the re-stated values, and for a repair its day (yyyy-MM-dd) and what was repaired.</summary>
public sealed record StatusChange(
    StatusEvent What,
    IReadOnlyDictionary<string, JsonNode?> Restated,
    string? RepairDate = null,
    string? RepairDescription = null);

/// <summary>The new version, or why nothing changed; <see cref="Keys"/> says which value was refused.</summary>
public sealed record StatusChangeResult(bool Found, PassportVersion? Version, IReadOnlyList<KeyResult> Keys, string? Problem, string? NewStatus = null);

/// <summary>
/// The status change (design 7.4, 7.6 and 7.9; decisions 15, 19 and 34; step 4b). The team or the battery's cluster
/// admins say what happened; the app sets the battery status (#22) from it, saves it with the re-stated fades and
/// lifetime through the save pipeline's status-change way in, adds a repair to the history, and publication makes the
/// new version. All or nothing: a refused value, a missing repair detail or a passport that couldn't get a new version
/// now leaves everything as it was.
/// </summary>
public sealed class StatusChangeService(
    DppStore store,
    SavePipeline pipeline,
    PublicationService publication,
    ApplicationAuditService audit,
    TimeProvider clock)
{
    public const string ChangedEvent = "dpp.status-changed";

    public const string NotYours = "only the team and the battery's cluster admins change its status";
    public const string OnlyPublished = "only a published, active passport changes status";
    public const string WasteIsFinal = "the battery is waste, which is final";
    public const string ValuesRefused = "a re-stated value was refused or missing, so nothing was saved";
    public const string RepairNeedsDate = "give the day of the repair";
    public const string RepairNotInTheFuture = "the day of the repair can't be in the future";
    public const string RepairNeedsDescription = "say what was repaired";
    public const string OnlyTheTeamCorrects = "only the team corrects the values; ask the team";
    public const string NoteNeedsText = "write the note first";
    public const string NoBattery = "the battery was not found";
    public const string NoteAddedEvent = "dpp.history-note-added";

    /// <summary>The longest repair description kept, in characters.</summary>
    public const int MaxDescription = 500;

    private const string StatusProperty = "batteryStatus";

    /// <summary>
    /// The values a status change re-states (decision 15): capacity fade, power fade, internal resistance increase and
    /// expected lifetime, and the round trip efficiency fade where the model has it (#74 is conditional).
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> RestatedAttributes(BatteryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return AttributeCatalog.All
            .Where(attribute => attribute.Change == ChangeRule.RestatedOnStatusChange)
            .Where(attribute => attribute.Requirement != Requirement.Conditional || model.Values.ContainsKey(attribute.Property))
            .OrderBy(attribute => attribute.Number)
            .ToArray();
    }

    /// <summary>
    /// The battery status after what happened (design 7.4 and 7.9, J-15); null when it can't happen. Waste is final. A
    /// repair makes an original battery "re-used" and leaves a re-used, remanufactured or repurposed one as it is.
    /// </summary>
    public static string? NewStatus(string current, StatusEvent what)
    {
        string[] inUse = ["original", "re-used", "remanufactured", "repurposed"];
        if (what == StatusEvent.Corrected)
        {
            return inUse.Contains(current) || current == "waste" ? current : null;
        }

        if (!inUse.Contains(current))
        {
            return null;
        }

        return what switch
        {
            StatusEvent.Repaired => current == "original" ? "re-used" : current,
            StatusEvent.Waste => "waste",
            _ => null,
        };
    }

    /// <summary>The battery status stored on a pack, e.g. "original"; null when it has none.</summary>
    public static string? CurrentStatus(BatteryPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return pack.Values.GetValueOrDefault(StatusProperty)?.Value?["batteryStatusValues"]?.GetValue<string>();
    }

    public async Task<StatusChangeResult> ChangeAsync(ChangeSender sender, string packId, StatusChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(change);

        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        if (sources is null)
        {
            return new StatusChangeResult(false, null, [], null);
        }

        var pack = sources.Pack;
        var mayChange = sender.Kind == SenderKind.Team
            || (sender.Kind == SenderKind.ClusterAdmin && pack.ClusterId is { } cluster && sender.ClusterIds.Contains(cluster));
        if (!mayChange)
        {
            return Refused(NotYours);
        }

        if (change.What == StatusEvent.Corrected && sender.Kind != SenderKind.Team)
        {
            return Refused(OnlyTheTeamCorrects);
        }

        if (await store.GetActiveVersionAsync(packId, cancellationToken) is null)
        {
            return Refused(OnlyPublished);
        }

        var from = CurrentStatus(pack) ?? "original";
        if (from == "waste" && change.What != StatusEvent.Corrected)
        {
            return Refused(WasteIsFinal);
        }

        if (NewStatus(from, change.What) is not { } to)
        {
            return Refused($"a battery that is {from} can't become {change.What.ToString().ToLowerInvariant()}");
        }

        RepairEntry? repair = null;
        if (change.What == StatusEvent.Repaired)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            if (!DateOnly.TryParseExact(change.RepairDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                return Refused(RepairNeedsDate);
            }

            if (day > today)
            {
                return Refused(RepairNotInTheFuture);
            }

            var description = change.RepairDescription?.Trim() ?? string.Empty;
            if (description.Length == 0)
            {
                return Refused(RepairNeedsDescription);
            }

            repair = new RepairEntry
            {
                Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Description = description.Length > MaxDescription ? description[..MaxDescription] : description,
                RecordedBy = sender.Who,
                RecordedAs = sender.Kind.ToString(),
                RecordedAtUtc = clock.GetUtcNow().UtcDateTime,
            };
        }

        // The status change publishes a new version, so a passport that couldn't get one now changes nothing.
        if (await publication.CheckAsync(packId, cancellationToken) is not { CanPublish: true })
        {
            return Refused("the passport can't get a new version now; the team sees why on its battery page");
        }

        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            [StatusProperty] = new JsonObject { ["batteryStatusValues"] = to },
        };
        foreach (var attribute in RestatedAttributes(sources.Model))
        {
            values[attribute.Property] = change.Restated.GetValueOrDefault(attribute.Property)?.DeepClone();
        }

        var saved = await pipeline.SaveStatusChangeAsync(sender, packId, values, cancellationToken);
        if (!saved.Saved)
        {
            return new StatusChangeResult(true, null, saved.Keys, ValuesRefused);
        }

        if (repair is not null)
        {
            await store.AddRepairAsync(packId, repair, cancellationToken);
        }

        var version = await store.GetActiveVersionAsync(packId, cancellationToken);
        var serial = pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? packId;
        var metadata = new BsonDocument
        {
            ["what"] = change.What.ToString(),
            ["from"] = from,
            ["to"] = to,
            ["version"] = version?.Number ?? 0,
        };
        if (repair is not null)
        {
            metadata["repairDate"] = repair.Date;
            metadata["repair"] = repair.Description;
        }

        await audit.AppendApplicationAuditEventAsync(
            ChangedEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            change.What switch
            {
                StatusEvent.Repaired => $"Battery {serial} repaired: {repair!.Description} Status {to}.",
                StatusEvent.Corrected => $"Battery {serial}: values corrected; status {to}.",
                _ => $"Battery {serial} is now {to}.",
            },
            metadata,
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return new StatusChangeResult(true, version, saved.Keys, null, to);

        static StatusChangeResult Refused(string problem) => new(true, null, [], problem);
    }

    /// <summary>
    /// Adds a note marked "Correction" to the repair history (decision 34): entries are never changed or removed, so a
    /// mistake is corrected with one of these. The team or the battery's cluster admins, on a published passport. The
    /// history isn't passport data, so no version is made. Null when it was added; otherwise why not.
    /// </summary>
    public async Task<string?> AddNoteAsync(ChangeSender sender, string packId, string? text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return NoBattery;
        }

        var mayAdd = sender.Kind == SenderKind.Team
            || (sender.Kind == SenderKind.ClusterAdmin && pack.ClusterId is { } cluster && sender.ClusterIds.Contains(cluster));
        if (!mayAdd)
        {
            return NotYours;
        }

        if (await store.GetActiveVersionAsync(packId, cancellationToken) is null)
        {
            return OnlyPublished;
        }

        var note = text?.Trim() ?? string.Empty;
        if (note.Length == 0)
        {
            return NoteNeedsText;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var entry = new RepairEntry
        {
            Date = DateOnly.FromDateTime(now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Description = note.Length > MaxDescription ? note[..MaxDescription] : note,
            RecordedBy = sender.Who,
            RecordedAs = sender.Kind.ToString(),
            RecordedAtUtc = now,
            IsCorrection = true,
        };
        await store.AddRepairAsync(packId, entry, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            NoteAddedEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"Note added to the repair history of battery {pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? packId}: {entry.Description}",
            new BsonDocument { ["note"] = entry.Description },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~StatusChangeTests | FullyQualifiedName~SavePipelineListenerTests | FullyQualifiedName~DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:    38`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2163`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Editing/IPackChangeListener.cs web/Dpp/Editing/SavePipeline.cs web/Dpp/Editing/StatusChangeService.cs web/Dpp/Publication/PublicationService.cs web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs
git commit -m "feat(dpp): the status change, with repairs, corrections and notes in the repair history" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The status card and window on both battery pages, and the history on the detail page

The card of mock-up S2 with the two-step window of S3, on the team's battery page and the cluster admin's, one partial for both; "Add a note to the history" under the history; the repair history in the detail page's Purchaser tab for those who see the status (S6). The window's fields are the editors' own. The approved mock-up's step-4 style block goes at the end of `bp-product-data.css`.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatteryTesting.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs`
- Modify: `web/Controllers/BatteriesController.cs`
- Modify: `web/Controllers/ClusterBatteriesController.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`
- Create: `web/Dpp/Pages/StatusPages.cs`
- Modify: `web/Dpp/Pages/ValueWords.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Create: `web/Views/Batteries/_StatusCard.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`
- Modify: `web/Views/Passport/_PassportTabs.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`
- Create: `web/wwwroot/js/bp-status-change.js`

- [ ] **Step 1: Write the failing tests**

Patch `4b-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/BatteryTesting.cs`, `BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs`, `BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs`). Save the block as `../4b-task2-tests.patch`, outside the worktree, and run `git apply ../4b-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
index cd648a4..0c4e5f4 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
@@ -190,6 +190,9 @@ public sealed class BatteriesControllerTests(MongoFixture mongo)
         Assert.Contains("~/Views/ProductData/_EditorField.cshtml", editor, StringComparison.Ordinal);
     }
 
+    /// <summary>The controller as a team member reaches it, with this form posted (also used by the status tests, step 4b).</summary>
+    internal static BatteriesController Controller(PublicationRig rig, IReadOnlyDictionary<string, string> form) => Controller(rig, form.ToArray());
+
     private static BatteriesController Controller(PublicationRig rig, params KeyValuePair<string, string>[] form)
     {
         var http = new DefaultHttpContext
@@ -199,7 +202,7 @@ public sealed class BatteriesControllerTests(MongoFixture mongo)
                 "test")),
         };
         http.Request.Form = new FormCollection(form.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
-        return new BatteriesController(BatteryTesting.Pages(rig), PageTesting.Documents(rig))
+        return new BatteriesController(BatteryTesting.Pages(rig), PageTesting.Documents(rig), StatusChangeTests.Pages(rig))
         {
             ControllerContext = new ControllerContext { HttpContext = http },
             TempData = new TempDataDictionary(http, new NoTempDataStorage()),
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryTesting.cs b/BatteryPassWeb.Tests/Dpp/BatteryTesting.cs
index 3e52e87..0b2fa9e 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteryTesting.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteryTesting.cs
@@ -36,7 +36,8 @@ internal static class BatteryTesting
         new DocumentStore(rig.Context),
         DocumentTesting.Links,
         new ClusterRepository(rig.Context),
-        new ApplicationAuditService(rig.Context));
+        new ApplicationAuditService(rig.Context),
+        StatusChangeTests.Pages(rig));
 
     /// <summary>The fields of a form that shows these attributes, as the page shows them.</summary>
     public static Dictionary<string, string> ShownForm(IEnumerable<AttributeDefinition> attributes, IReadOnlyDictionary<string, AttributeValue> stored)
diff --git a/BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs b/BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs
index 83b426a..ae3d2a2 100644
--- a/BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs
@@ -135,7 +135,8 @@ public sealed class ClusterBatteryPagesTests(MongoFixture mongo)
         PageTesting.Editor(rig),
         PageTesting.Documents(rig),
         new DocumentStore(rig.Context),
-        new ClusterRepository(rig.Context));
+        new ClusterRepository(rig.Context),
+        StatusChangeTests.Pages(rig));
 
     private static async Task<BsonDocument> LastChangeAsync(PublicationRig rig) =>
         await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
diff --git a/BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs b/BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs
index ca84690..26c7dec 100644
--- a/BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs
@@ -274,6 +274,9 @@ public sealed class StatusChangeTests(MongoFixture mongo)
     internal static StatusChangeService Service(PublicationRig rig) =>
         new(rig.Store, rig.Pipeline, rig.Publication, new ApplicationAuditService(rig.Context), rig.Clock);
 
+    internal static BatteryPassWeb.Dpp.Pages.StatusPages Pages(PublicationRig rig) =>
+        new(rig.Store, PageTesting.Editor(rig), Service(rig), rig.Clock);
+
     /// <summary>The battery's current re-stated values as the window fills them in, as text.</summary>
     internal static async Task<Dictionary<string, JsonNode?>> CurrentAsync(PublicationRig rig)
     {
```

`BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs`:

```csharp
using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The battery status card and its window on both battery pages, and the repair history on the detail page (step 4b).</summary>
[Collection(MongoCollection.Name)]
public sealed class StatusPagesTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";

    [Fact]
    public async Task TheCard_ShouldShowTheStatusAndFillInTheBatterysCurrentValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;

        Assert.Equal("original", card.Status);
        Assert.Equal("Original", card.StatusWords);
        Assert.True(card.CanChange);
        Assert.Empty(card.Repairs);
        Assert.Equal(200m, card.RatedCapacityAh);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}/status", card.Restated.SaveAction);
        var fields = card.Restated.Sections.SelectMany(section => section.Fields).ToArray();
        Assert.Equal([59, 69, 74, 79, 81], fields.Select(field => field.Attribute.Number));
        Assert.Equal("4", fields[0].Text(BatteryTesting.Field("capacityFade"))); // the battery's own value
        Assert.Equal("12", fields[4].Text(BatteryTesting.Field("expectedLifetimeInCalendarYears"))); // the model's, until re-stated
        Assert.Equal([StatusEvent.Repaired, StatusEvent.Waste, StatusEvent.Corrected], card.Choices.Select(choice => choice.What));
        Assert.True(card.MayAddNote);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}/status/note", card.NoteAction);
    }

    [Fact]
    public async Task TheClusterAdminsCard_ShouldOfferNoCorrection()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var card = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId))!.Status!;

        Assert.Equal([StatusEvent.Repaired, StatusEvent.Waste], card.Choices.Select(choice => choice.What));
        Assert.True(card.MayAddNote);
    }

    [Fact]
    public async Task ADraftOrAWasteBattery_ShouldOfferNoStatusChange()
    {
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var waste = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await StatusChangeTests.Service(waste).ChangeAsync(PublicationTesting.Team, waste.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(waste)));

        var draftCard = (await BatteryTesting.Pages(draft).BatteryAsync(draft.PackId))!.Status!;
        var wasteCard = (await BatteryTesting.Pages(waste).BatteryAsync(waste.PackId))!.Status!;

        Assert.False(draftCard.CanChange);
        Assert.Equal("The status changes once the passport is published.", draftCard.WhyNot);
        Assert.True(wasteCard.CanChange); // the team may still correct the values
        Assert.Equal([StatusEvent.Corrected], wasteCard.Choices.Select(choice => choice.What));
        Assert.Equal("Waste", wasteCard.StatusWords);
        Assert.False(draftCard.MayAddNote);
    }

    [Fact]
    public async Task TheWindow_ShouldBeReadLikeTheEditorsAndSaved()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var form = await WindowAsync(rig, ("capacityFade", "7")); // whole percent, as the schema says

        var result = (await StatusChangeTests.Pages(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, "Repaired", PageTesting.Field(form), "2026-10-20", "Module 4 replaced."))!;

        Assert.Null(result.Problem);
        Assert.Equal("re-used", result.NewStatus);
        Assert.Equal(("Saved: the battery is re-used. Version 2 of the passport is published.", (string?)null), StatusChangeTests.Pages(rig).Message(result, versionBefore: 1));
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal(7, pack.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>());
        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        Assert.True(card.AfterRepair);
        Assert.Equal("Module 4 replaced.", Assert.Single(card.Repairs).Description);
    }

    [Fact]
    public async Task ARefusal_ShouldSayWhatAndWhichValueInWords()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var none = (await StatusChangeTests.Pages(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, "Lost", PageTesting.Field(await WindowAsync(rig)), null, null))!;
        var empty = (await StatusChangeTests.Pages(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, "Waste", PageTesting.Field(await WindowAsync(rig, ("capacityFade", ""))), null, null))!;

        Assert.Equal((null, "Nothing was changed: choose what happened."), StatusChangeTests.Pages(rig).Message(none, 1));
        var (_, problem) = StatusChangeTests.Pages(rig).Message(empty, 1);
        Assert.Equal($"Nothing was changed: {StatusChangeService.ValuesRefused}. Capacity fade: it needs a value.", problem);
    }

    [Fact]
    public async Task TheTeamsPage_ShouldChangeTheStatusAndOpenAtTheCard()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = BatteriesControllerTests.Controller(rig, await WindowAsync(rig));

        var redirect = Assert.IsType<RedirectResult>(await controller.ChangeStatus(rig.PackId, "Waste", null, null, CancellationToken.None));

        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#status", redirect.Url);
        Assert.Equal("status", controller.TempData[BatteriesController.CardKey]);
        Assert.Equal("Saved: the battery is waste. Version 2 of the passport is published.", controller.TempData[BatteriesController.CardStatusKey]);
    }

    [Fact]
    public async Task TheClusterAdminsPage_ShouldChangeTheStatusOfTheirOwnBatteryOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        await clusters.UpsertClusterMembershipAsync("south-admin@example.com", BatteryTesting.South, "clusterAdmin");
        var form = await WindowAsync(rig);

        var other = await ClusterController(rig, "south-admin@example.com", form).ChangeStatus(rig.PackId, "Repaired", "2026-10-20", "Fan replaced.", CancellationToken.None);
        var own = ClusterController(rig, NorthAdmin, form);
        var redirect = Assert.IsType<RedirectResult>(await own.ChangeStatus(rig.PackId, "Repaired", "2026-10-20", "Fan replaced.", CancellationToken.None));

        Assert.IsType<NotFoundResult>(other);
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#status", redirect.Url);
        Assert.Equal("Saved: the battery is re-used. Version 2 of the passport is published.", own.TempData[BatteriesController.CardStatusKey]);
        var repair = Assert.Single((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs);
        Assert.Equal(NorthAdmin, repair.RecordedBy);
        Assert.Equal(nameof(SenderKind.ClusterAdmin), repair.RecordedAs);
    }

    [Fact]
    public async Task BothPages_ShouldAddANoteAndOpenAtTheCard()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await new ClusterRepository(rig.Context).UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        var team = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());
        var admin = ClusterController(rig, NorthAdmin, new Dictionary<string, string>());

        var byTeam = Assert.IsType<RedirectResult>(await team.AddHistoryNote(rig.PackId, "Checked again: the seal holds.", CancellationToken.None));
        var empty = Assert.IsType<RedirectResult>(await admin.AddHistoryNote(rig.PackId, " ", CancellationToken.None));

        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#status", byTeam.Url);
        Assert.Equal("Note added to the history.", team.TempData[BatteriesController.CardStatusKey]);
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#status", empty.Url);
        Assert.Equal($"The note wasn't added: {StatusChangeService.NoteNeedsText}.", admin.TempData[BatteriesController.CardErrorKey]);
        Assert.True(Assert.Single((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs).IsCorrection);
    }

    [Theory]
    [InlineData(ReaderRole.ClusterUser, true)]
    [InlineData(ReaderRole.MarketSurveillance, true)]
    [InlineData(ReaderRole.Team, true)]
    [InlineData(ReaderRole.Commission, false)]
    [InlineData(ReaderRole.LiRepair, false)]
    public async Task TheDetailPage_ShouldShowTheRepairHistoryToThoseWhoSeeTheStatus(ReaderRole role, bool sees)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await StatusChangeTests.Pages(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, "Repaired", PageTesting.Field(await WindowAsync(rig)), "2026-10-20", "Fan replaced.");
        var reader = role == ReaderRole.ClusterUser ? Reader.With("user", role, BatteryTesting.North) : Reader.With("reader", role);

        var page = (await PassportPagesTests.Pages(rig).DetailAsync(reader, PassportPagesTests.Identifier(rig)))!;

        Assert.Equal(sees, page.Repairs is { Count: 1 });
    }

    [Fact]
    public void ThePages_ShouldDrawTheCardTheWindowsScriptAndTheHistory()
    {
        var team = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var clusterAdmin = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml"));
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css"));

        Assert.All(new[] { team, clusterAdmin }, page =>
        {
            Assert.Contains("~/Views/Batteries/_StatusCard.cshtml\", status", page, StringComparison.Ordinal);
            Assert.Contains("~/js/bp-status-change.js", page, StringComparison.Ordinal);
        });
        Assert.Contains("Model.Repairs is { } repairs", tabs, StringComparison.Ordinal);
        Assert.Contains("repair.IsCorrection", tabs, StringComparison.Ordinal);
        Assert.Contains("repair.IsCorrection", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_StatusCard.cshtml")), StringComparison.Ordinal);
        Assert.Contains("Purchaser editing and lifecycle (step 4)", css, StringComparison.Ordinal);
        Assert.Contains(".bp-history", css, StringComparison.Ordinal);
    }

    /// <summary>The status window as it opens, with the given fields typed over.</summary>
    private static async Task<Dictionary<string, string>> WindowAsync(PublicationRig rig, params (string Property, string Text)[] typed)
    {
        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        var form = card.Restated.Sections.SelectMany(section => section.Fields)
            .SelectMany(field => field.Form.Inputs.Select(input => field.Form.FieldName(input)).Select(name => (name, text: field.Text(name))))
            .ToDictionary(entry => entry.name, entry => entry.text, StringComparer.Ordinal);
        foreach (var (property, text) in typed)
        {
            form[BatteryTesting.Field(property)] = text;
        }

        return form;
    }

    private static ClusterBatteriesController ClusterController(PublicationRig rig, string email, IReadOnlyDictionary<string, string> form)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, "clusterAdmin")], "test")),
        };
        http.Request.Form = new FormCollection(form.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~StatusPagesTests | FullyQualifiedName~StatusChangeTests | FullyQualifiedName~BatteryPagesTests | FullyQualifiedName~ClusterBatteryPagesTests | FullyQualifiedName~BatteriesControllerTests"`
Expected: the build fails with CS0234 (for example `error CS0234 The type or namespace name 'StatusPages' does not exist in the namespace 'BatteryPassWeb.Dpp.Pages'`).

- [ ] **Step 3: Write the code**

Patch `4b-task2-code.patch` (`web/Controllers/BatteriesController.cs`, `web/Controllers/ClusterBatteriesController.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Dpp/Pages/ValueWords.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/Views/Passport/_PassportTabs.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../4b-task2-code.patch`, outside the worktree, and run `git apply ../4b-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/BatteriesController.cs b/web/Controllers/BatteriesController.cs
index 85f7898..6379fad 100644
--- a/web/Controllers/BatteriesController.cs
+++ b/web/Controllers/BatteriesController.cs
@@ -14,7 +14,7 @@ namespace BatteryPassWeb.Controllers;
 /// </summary>
 [Authorize(Policy = "AdminOnly")]
 [Route("admin/batteries")]
-public sealed class BatteriesController(BatteryPages pages, DocumentService documents) : Controller
+public sealed class BatteriesController(BatteryPages pages, DocumentService documents, StatusPages status) : Controller
 {
     /// <summary>Which card of the battery page a message belongs to, so it shows where the action was taken.</summary>
     public const string CardKey = "BatteryCard";
@@ -153,6 +153,33 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
         return Redirect($"{BatteryPages.BatteryHref(packId)}#accidents");
     }
 
+    /// <summary>The "Change status" window (design 7.4, step 4b): what happened, the re-stated values, a repair's details.</summary>
+    [HttpPost("{packId}/status")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> ChangeStatus(string packId, [FromForm] string? what, [FromForm] string? repairDate, [FromForm] string? repairDescription, CancellationToken cancellationToken)
+    {
+        var form = Request.Form;
+        var before = (await pages.BatteryAsync(packId, cancellationToken: cancellationToken))?.Versions.Count ?? 0;
+        if (await status.ChangeAsync(ChangeSender.Team(Who), packId, what, name => form.TryGetValue(name, out var value) ? value.ToString() : null, repairDate, repairDescription, cancellationToken) is not { Found: true } result)
+        {
+            return NotFound();
+        }
+
+        var (done, problem) = status.Message(result, before);
+        Card("status", done, problem);
+        return Redirect($"{BatteryPages.BatteryHref(packId)}#status");
+    }
+
+    /// <summary>"Add a note to the history" (decision 34): a correcting entry, with no new version.</summary>
+    [HttpPost("{packId}/status/note")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> AddHistoryNote(string packId, [FromForm] string? note, CancellationToken cancellationToken)
+    {
+        var (done, problem) = await status.AddNoteAsync(ChangeSender.Team(Who), packId, note, cancellationToken);
+        Card("status", done, problem);
+        return Redirect($"{BatteryPages.BatteryHref(packId)}#status");
+    }
+
     [HttpPost("{packId}/remove")]
     [ValidateAntiForgeryToken]
     public async Task<IActionResult> Remove(string packId, CancellationToken cancellationToken)
diff --git a/web/Controllers/ClusterBatteriesController.cs b/web/Controllers/ClusterBatteriesController.cs
index 16d94ff..a256366 100644
--- a/web/Controllers/ClusterBatteriesController.cs
+++ b/web/Controllers/ClusterBatteriesController.cs
@@ -101,6 +101,43 @@ public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppRea
         return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#accidents");
     }
 
+    /// <summary>The "Change status" window (design 7.4, step 4b), as the battery's cluster admin.</summary>
+    [HttpPost("{packId}/status")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> ChangeStatus(string packId, [FromForm] string? what, [FromForm] string? repairDate, [FromForm] string? repairDescription, CancellationToken cancellationToken)
+    {
+        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
+        {
+            return NotFound();
+        }
+
+        var form = Request.Form;
+        var before = (await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken))?.Versions.Count ?? 0;
+        if (await pages.ChangeStatusAsync(sender, packId, what, name => form.TryGetValue(name, out var value) ? value.ToString() : null, repairDate, repairDescription, cancellationToken) is not { } result)
+        {
+            return NotFound();
+        }
+
+        var (done, problem) = pages.StatusMessage(result, before);
+        Card("status", done, problem);
+        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#status");
+    }
+
+    /// <summary>"Add a note to the history" (decision 34), as the battery's cluster admin.</summary>
+    [HttpPost("{packId}/status/note")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> AddHistoryNote(string packId, [FromForm] string? note, CancellationToken cancellationToken)
+    {
+        if (await ClusterAdminAsync(cancellationToken) is not { } sender
+            || await pages.AddHistoryNoteAsync(sender, packId, note, cancellationToken) is not { } message)
+        {
+            return NotFound();
+        }
+
+        Card("status", message.Done, message.Problem);
+        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#status");
+    }
+
     /// <summary>The signed-in user as a cluster admin; null for the team (it has its own page) and for anyone else.</summary>
     private async Task<ChangeSender?> ClusterAdminAsync(CancellationToken cancellationToken) =>
         await readers.EditorForAsync(User, cancellationToken) is { Kind: SenderKind.ClusterAdmin } sender ? sender : null;
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index 13a9367..ffe86b0 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -44,6 +44,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<AttributeForms>();
         services.AddSingleton<RecordEditor>();
         services.AddSingleton<ProductDataPages>();
+        services.AddSingleton<StatusPages>();
         services.AddSingleton<BatteryPages>();
         services.AddSingleton<ClusterBatteryPages>();
         services.AddSingleton<DppReaders>();
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 3c8d308..031a02d 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -90,6 +90,9 @@ public sealed record BatteryPage(
 {
     public string BatchLabel => $"{Sources.Batch.Year} · {Sources.Site.Name}";
 
+    /// <summary>The battery status card (step 4b), shared with the cluster admin's page.</summary>
+    public StatusCardView? Status { get; init; }
+
     /// <summary>The cards this page shares with the cluster admin's battery page (step 4a).</summary>
     public BatteryCards Cards => new(
         Pack,
@@ -117,7 +120,8 @@ public sealed class BatteryPages(
     DocumentStore documents,
     DppLinks links,
     ClusterRepository clusters,
-    ApplicationAuditService audit)
+    ApplicationAuditService audit,
+    StatusPages status)
 {
     public const string ClusterChangedEvent = "dpp.pack-cluster-changed";
     public const string ModulesSetEvent = "dpp.pack-modules-set";
@@ -323,7 +327,10 @@ public sealed class BatteryPages(
             await PublicationAsync(sources, active, cancellationToken),
             versions.OrderByDescending(version => version.Number).Select(version => new VersionRow(version, PassportReader.Verify(version))).ToArray(),
             await documents.ListAsync(TargetKind.Pack, packId, DocumentService.Accidents, cancellationToken),
-            new RemovalView($"{href}/remove", versions.Count > 0 ? "its passport is published, so it stays with its versions." : null));
+            new RemovalView($"{href}/remove", versions.Count > 0 ? "its passport is published, so it stays with its versions." : null))
+        {
+            Status = await status.CardAsync(sources, active is not null, href, forTeam: true, cancellationToken: cancellationToken),
+        };
     }
 
     /// <summary>
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index 7b38a01..99a9025 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -23,6 +23,9 @@ public sealed record ClusterBatteryPage(
 
     public string Href => ClusterBatteryPages.BatteryHref(Pack.Id);
 
+    /// <summary>The battery status card (step 4b), shared with the team's page.</summary>
+    public StatusCardView? Status { get; init; }
+
     /// <summary>The team page's cards, without what only the team does (step 4a).</summary>
     public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false);
 }
@@ -37,7 +40,8 @@ public sealed class ClusterBatteryPages(
     RecordEditor editor,
     DocumentService documents,
     DocumentStore documentStore,
-    ClusterRepository clusters)
+    ClusterRepository clusters,
+    StatusPages status)
 {
     public static string BatteryHref(string packId) => $"/cluster-admin/batteries/{Uri.EscapeDataString(packId)}";
 
@@ -94,9 +98,34 @@ public sealed class ClusterBatteryPages(
             BatteryPages.ClusterLabel(pack.ClusterId, names),
             liveView,
             versions.OrderByDescending(version => version.Number).Select(version => new VersionRow(version, PassportReader.Verify(version))).ToArray(),
-            await documentStore.ListAsync(TargetKind.Pack, pack.Id, DocumentService.Accidents, cancellationToken));
+            await documentStore.ListAsync(TargetKind.Pack, pack.Id, DocumentService.Accidents, cancellationToken))
+        {
+            Status = await status.CardAsync(sources, active: true, href, forTeam: false, cancellationToken: cancellationToken),
+        };
     }
 
+    /// <summary>Adds a note to the history as the cluster admin (decision 34); null when the sender may not manage the battery.</summary>
+    public async Task<(string? Done, string? Problem)?> AddHistoryNoteAsync(ChangeSender sender, string packId, string? note, CancellationToken cancellationToken = default) =>
+        await ManagedAsync(sender, packId, cancellationToken) is { } pack
+            ? await status.AddNoteAsync(sender, pack.Id, note, cancellationToken)
+            : null;
+
+    /// <summary>What the status card says after the window was sent (the team page's words).</summary>
+    public (string? Done, string? Problem) StatusMessage(StatusChangeResult result, int versionBefore) => status.Message(result, versionBefore);
+
+    /// <summary>Makes a status change from the window, as the cluster admin; null when the sender may not manage the battery.</summary>
+    public async Task<StatusChangeResult?> ChangeStatusAsync(
+        ChangeSender sender,
+        string packId,
+        string? what,
+        Func<string, string?> field,
+        string? repairDate,
+        string? repairDescription,
+        CancellationToken cancellationToken = default) =>
+        await ManagedAsync(sender, packId, cancellationToken) is { } pack
+            ? await status.ChangeAsync(sender, pack.Id, what, field, repairDate, repairDescription, cancellationToken)
+            : null;
+
     /// <summary>Saves the live values typed on the page, as the cluster admin; null when the sender may not manage the battery.</summary>
     public async Task<(EditorPost Post, EditorSave Save)?> SaveLiveAsync(
         ChangeSender sender,
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index 9f04985..4646cf5 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -64,6 +64,12 @@ public sealed record PassportPage(
 
     /// <summary>The battery's modules with their IDs, on the detail page for the battery's purchaser's readers; null otherwise.</summary>
     public IReadOnlyList<BatteryModule>? Modules { get; init; }
+
+    /// <summary>
+    /// The battery's repair history, newest first, on the detail page for those who may see its status (design 7.9);
+    /// null otherwise. Not part of the signed passport.
+    /// </summary>
+    public IReadOnlyList<RepairEntry>? Repairs { get; init; }
 }
 
 /// <summary>Where a published passport is: its version, and the battery's ID (#6) and serial number (#11).</summary>
@@ -242,6 +248,7 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
             ModuleCount = sources.Pack.Modules.Count,
             ModuleDescription = sources.Model.ModuleDescription,
             Modules = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Modules : null,
+            Repairs = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Repairs.AsEnumerable().Reverse().ToArray() : null,
         };
     }
 
diff --git a/web/Dpp/Pages/ValueWords.cs b/web/Dpp/Pages/ValueWords.cs
index 5bf22ea..82e7cda 100644
--- a/web/Dpp/Pages/ValueWords.cs
+++ b/web/Dpp/Pages/ValueWords.cs
@@ -78,6 +78,12 @@ public static class ValueWords
     public static string Time(DateTime utc) =>
         TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local).ToString("d MMM yyyy, HH:mm", English);
 
+    /// <summary>A day kept as yyyy-MM-dd (a repair's), in words: "20 Oct 2026"; the text itself when it isn't one.</summary>
+    public static string Day(string day) =>
+        DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
+            ? parsed.ToString("d MMM yyyy", English)
+            : day;
+
     /// <summary>A JSON number as a decimal, however it was read (the database gives doubles); null for anything else.</summary>
     public static decimal? Decimal(JsonNode? node)
     {
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index ae85855..31cf458 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -276,6 +276,10 @@
         <p class="bp-battery-fixed-note">The serial number, manufacturing date and warranty are fixed now that the passport is published; they're shown at the top.</p>
     }
 
+    @if (Model.Status is { } status)
+    {
+        @await Html.PartialAsync("~/Views/Batteries/_StatusCard.cshtml", status)
+    }
     @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
     @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
     @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
@@ -304,4 +308,5 @@
 
 @section Scripts {
     <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
+    <script src="~/js/bp-status-change.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index eca598e..0955986 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -49,6 +49,10 @@
         </dl>
     </header>
 
+    @if (Model.Status is { } status)
+    {
+        @await Html.PartialAsync("~/Views/Batteries/_StatusCard.cshtml", status)
+    }
     @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
     @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
     @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
@@ -57,4 +61,5 @@
 
 @section Scripts {
     <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
+    <script src="~/js/bp-status-change.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/Passport/_PassportTabs.cshtml b/web/Views/Passport/_PassportTabs.cshtml
index 4ef6554..5660e03 100644
--- a/web/Views/Passport/_PassportTabs.cshtml
+++ b/web/Views/Passport/_PassportTabs.cshtml
@@ -75,6 +75,29 @@
                         }
                     </dl>
                 }
+                @if (tab.Key == "purchaser" && Model.Repairs is { } repairs)
+                {
+                    <h3 class="h6 mt-3 mb-0">Repair history</h3>
+                    @if (repairs.Count == 0)
+                    {
+                        <p class="text-secondary mt-2 mb-0">No repairs recorded.</p>
+                    }
+                    else
+                    {
+                        <dl class="bp-field-grid mt-2">
+                            @foreach (var repair in repairs)
+                            {
+                                <div>
+                                    <dt><time datetime="@repair.Date">@ValueWords.Day(repair.Date)</time></dt>
+                                    <dd>
+                                        @(repair.IsCorrection ? $"Correction: {repair.Description}" : repair.Description)
+                                        <span class="d-block small text-secondary">@(repair.IsCorrection ? "Added" : "Recorded") by @(repair.RecordedAs == "Team" ? "the manufacturer's team" : "the battery's cluster")</span>
+                                    </dd>
+                                </div>
+                            }
+                        </dl>
+                    }
+                }
             </div>
         }
     </div>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 0f737e3..3880b20 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -2638,3 +2638,130 @@ main .bp-field-marker.is-published {
   border-color: var(--bp-border);
   color: var(--bp-text);
 }
+
+/* ==== Purchaser editing and lifecycle (step 4) =====================================================================
+   The few new pieces step 4 adds to bp-product-data.css: the battery status line, the repair history, the notices for
+   a passport that is no longer active, and the hand-over rows. Everything else is the app's existing classes. */
+
+/* The battery status at the top of its card: the status in words, and since when. */
+.bp-battery-status {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: baseline;
+  gap: 6px 14px;
+}
+
+.bp-battery-status strong {
+  font-size: 1.15rem;
+  font-weight: 650;
+}
+
+/* The repair history: one line per repair, newest first. */
+.bp-history {
+  list-style: none;
+  margin: 12px 0 0;
+  padding: 0;
+  border-top: 1px solid var(--bp-border);
+}
+
+.bp-history li {
+  display: grid;
+  grid-template-columns: 9rem minmax(0, 1fr);
+  gap: 2px 16px;
+  padding: 10px 0;
+  border-bottom: 1px solid var(--bp-border);
+}
+
+.bp-history time {
+  font-weight: 600;
+}
+
+.bp-history small {
+  grid-column: 2;
+  color: var(--bp-muted);
+}
+
+/* A passport that is no longer active, or a battery whose passport replaced another: one calm line on every page. */
+.bp-passport-notice {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 6px 14px;
+  margin: 0 0 16px;
+  padding: 12px 16px;
+  border: 1px solid var(--bp-border);
+  border-left: 4px solid #64748b;
+  border-radius: 8px;
+  background: var(--bp-surface, #fff);
+}
+
+.bp-passport-notice.is-invalid {
+  border-left-color: #c2410c;
+}
+
+.bp-passport-notice strong {
+  font-weight: 650;
+}
+
+/* Hand-overs waiting for a cluster admin: one row each, with its two answers. */
+.bp-handover-row {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  justify-content: space-between;
+  gap: 10px 18px;
+  padding: 12px 0;
+  border-top: 1px solid var(--bp-border);
+}
+
+.bp-handover-row:first-of-type {
+  border-top: 0;
+}
+
+.bp-handover-row .bp-dialog-actions {
+  margin: 0;
+}
+
+/* The suggestion under capacity fade: a measured capacity in, a suggested fade out. */
+.bp-suggest {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: end;
+  gap: 8px 12px;
+  margin-top: 6px;
+}
+
+.bp-suggest label {
+  display: grid;
+  gap: 4px;
+  font-size: 0.9rem;
+}
+
+.bp-suggest input {
+  width: 9rem;
+}
+
+@media (max-width: 640px) {
+  .bp-history li {
+    grid-template-columns: minmax(0, 1fr);
+  }
+
+  .bp-history small {
+    grid-column: 1;
+  }
+}
+
+[data-theme="dark"] .bp-passport-notice {
+  background: transparent;
+}
+
+/* The status window's two steps: the second shows once "Next" is pressed (bp-status-change.js); without the script
+   both show. */
+[data-status-dialog].is-stepped [data-status-step="2"],
+[data-status-dialog].is-step-2 [data-status-step="1"] {
+  display: none;
+}
+
+[data-status-dialog].is-step-2 [data-status-step="2"] {
+  display: block;
+}
```

`web/Dpp/Pages/StatusPages.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// The battery status card both battery pages show (mock-up S2 and S3, step 4b): the status and since when, the repair
/// history newest first, and the "Change status" window with the re-stated values filled in.
/// </summary>
/// <param name="CanChange">False for a draft or a waste battery; the window isn't offered then.</param>
/// <param name="RatedCapacityAh">The model's rated capacity (#57), for the fade suggestion; null when it has none.</param>
public sealed record StatusCardView(
    string Status,
    DateTime? Since,
    bool AfterRepair,
    IReadOnlyList<RepairEntry> Repairs,
    RecordEditorView Restated,
    bool CanChange,
    string? WhyNot,
    decimal? RatedCapacityAh,
    string Today,
    bool ForTeam,
    bool MayAddNote = false)
{
    /// <summary>Where "Add a note to the history" posts (decision 34).</summary>
    public string NoteAction => $"{Restated.SaveAction}/note";

    /// <summary>The status in words, capitalised: "Re-used".</summary>
    public string StatusWords => Status.Length == 0 ? Status : char.ToUpperInvariant(Status[0]) + Status[1..];

    /// <summary>The choices of the window's first step, in order, with what each does (decision 34).</summary>
    public IReadOnlyList<(StatusEvent What, string Label, string Help)> Choices
    {
        get
        {
            var choices = new List<(StatusEvent, string, string)>();
            if (Status != "waste")
            {
                choices.Add((StatusEvent.Repaired, "Repaired", Status == "original"
                    ? "Status becomes re-used; the repair joins its history. Same passport."
                    : $"Status stays {Status}; the repair joins its history. Same passport."));
                choices.Add((StatusEvent.Waste, "Waste", "Final. The passport stays active until the team marks it for deletion after recycling."));
            }

            if (ForTeam)
            {
                choices.Add((StatusEvent.Corrected, "Correct the values", "For a mistyped value: the status stays, nothing joins the history, and a new version is published."));
            }

            return choices;
        }
    }
}

/// <summary>
/// Builds the status card and reads the status window for both battery pages (step 4b). The change itself is
/// <see cref="StatusChangeService"/>'s; the window's fields are the editors' own, so values are read and checked alike.
/// </summary>
public sealed class StatusPages(DppStore store, RecordEditor editor, StatusChangeService changes, TimeProvider clock)
{
    /// <summary>The card of a battery; <paramref name="typed"/> is the window as typed after a refused change.</summary>
    /// <param name="href">The battery page's address; the window posts to it plus "/status".</param>
    public async Task<StatusCardView> CardAsync(PassportSources sources, bool active, string href, bool forTeam, EditorState? typed = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var pack = sources.Pack;
        var attributes = StatusChangeService.RestatedAttributes(sources.Model);
        var current = CurrentValues(sources, attributes);
        var view = new RecordEditorView(
            TargetKind.Pack,
            pack.Id,
            string.Empty,
            $"{href}/status",
            $"{href}#status",
            await editor.SectionsAsync(TargetKind.Pack, pack.Id, attributes, current, typed?.Post, typed?.Save.Refusals(editor.Forms), cancellationToken),
            [],
            0,
            editor.Forms.Languages,
            string.Empty,
            new RemovalView(string.Empty, null))
        {
            ForTeam = forTeam,
        };
        var status = StatusChangeService.CurrentStatus(pack) ?? "original";
        var whyNot = !active ? "The status changes once the passport is published."
            : status == "waste" && !forTeam ? "The battery is waste, which is final."
            : null;
        return new StatusCardView(
            status,
            pack.Values.GetValueOrDefault("batteryStatus")?.GeneratedAtUtc,
            pack.Repairs.Count > 0 && status != "waste",
            pack.Repairs.AsEnumerable().Reverse().ToArray(),
            view,
            whyNot is null,
            whyNot,
            ValueWords.Decimal(sources.Model.Values.GetValueOrDefault("ratedCapacity")?.Value?["amperehourMiliamperehourValue"]),
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            forTeam,
            MayAddNote: active);
    }

    /// <summary>Reads the window and makes the change; null when the battery doesn't exist.</summary>
    public async Task<StatusChangeResult?> ChangeAsync(
        ChangeSender sender,
        string packId,
        string? what,
        Func<string, string?> field,
        string? repairDate,
        string? repairDescription,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);
        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        if (sources is null)
        {
            return null;
        }

        var restated = StatusChangeService.RestatedAttributes(sources.Model).ToDictionary(
            attribute => attribute.Property,
            attribute => FormValues.Read(editor.Forms.For(attribute), field),
            StringComparer.Ordinal);
        return Enum.TryParse<StatusEvent>(what, ignoreCase: false, out var happened) && Enum.IsDefined(happened)
            ? await changes.ChangeAsync(sender, packId, new StatusChange(happened, restated, repairDate, repairDescription), cancellationToken)
            : new StatusChangeResult(true, null, [], "choose what happened");
    }

    /// <summary>Adds a note to the history; the card's message, done or why not.</summary>
    public async Task<(string? Done, string? Problem)> AddNoteAsync(ChangeSender sender, string packId, string? text, CancellationToken cancellationToken = default) =>
        await changes.AddNoteAsync(sender, packId, text, cancellationToken) is { } problem
            ? (null, $"The note wasn't added: {problem}.")
            : ("Note added to the history.", null);

    /// <summary>What the card says after the window was sent: done (with the new version) or why nothing changed, in plain words.</summary>
    public (string? Done, string? Problem) Message(StatusChangeResult result, int versionBefore)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Problem is not null)
        {
            var words = new EditorSave(EditorOutcome.Refused, result.Keys, []).Refusals(editor.Forms);
            var refused = result.Keys
                .Where(key => key.Outcome is KeyOutcome.Rejected or KeyOutcome.KeptEmpty && AttributeCatalog.FindByProperty(key.Key) is not null)
                .Select(key => $"{AttributeCatalog.FindByProperty(key.Key)!.Name}: {(words.TryGetValue(key.Key, out var refusal) ? refusal.Text : "it needs a value")}")
                .ToArray();
            return (null, $"Nothing was changed: {result.Problem}.{(refused.Length == 0 ? string.Empty : " " + string.Join(". ", refused.Select(text => text.TrimEnd('.'))) + ".")}");
        }

        return result.Version is { } version && version.Number > versionBefore
            ? ($"Saved: the battery is {result.NewStatus}. Version {version.Number} of the passport is published.", null)
            : ($"Saved: the battery is {result.NewStatus}. The passport's values didn't change, so it keeps version {versionBefore}.", null);
    }

    /// <summary>The re-stated values as the battery shows them now: its own once re-stated, else its model's (design 4.1).</summary>
    private static Dictionary<string, AttributeValue> CurrentValues(PassportSources sources, IReadOnlyList<AttributeDefinition> attributes)
    {
        var values = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var attribute in attributes)
        {
            if ((sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values.GetValueOrDefault(attribute.Property)) is { } value)
            {
                values[attribute.Property] = value;
            }
        }

        return values;
    }
}
```

`web/Views/Batteries/_StatusCard.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Editing
@using BatteryPassWeb.Dpp.Pages
@model StatusCardView
@* The battery status card and its "Change status" window (step 4b, mock-up S2 and S3), on both battery pages. *@
<section class="bp-card bp-console-form-card bp-battery-card mt-4" id="status">
    <div class="bp-battery-card-head">
        <div>
            <span class="bp-summary-title">Battery status <span class="bp-attr-number">#22</span></span>
            <div class="bp-battery-status">
                <strong>@Model.StatusWords</strong>
                @if (Model.Since is { } since)
                {
                    <span class="text-secondary">since @ValueWords.Time(since)@(Model.AfterRepair ? ", after a repair" : string.Empty)</span>
                }
            </div>
            <p class="mb-0">@(Model.WhyNot ?? (Model.Status == "waste" ? "The battery is waste, which is final. Mistyped values can still be corrected." : "A status change also re-states the fades and the expected lifetime, and publishes a new version of this passport."))</p>
        </div>
        @if (Model.CanChange)
        {
            <button type="button" class="bp-primary-button" data-open-dialog="status-dialog">Change status</button>
        }
    </div>
    @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "status")
    <h3 class="h6 mt-3 mb-0">Repair history <em class="bp-field-marker">Not passport data</em></h3>
    @if (Model.Repairs.Count == 0)
    {
        <p class="bp-help-text mt-2 mb-0">No repairs recorded.</p>
    }
    else
    {
        <ul class="bp-history">
            @foreach (var repair in Model.Repairs)
            {
                <li><time datetime="@repair.Date">@ValueWords.Day(repair.Date)</time><span>@if (repair.IsCorrection)
                    {
                        <span class="bp-soft-tag">Correction</span>
                    } @repair.Description</span><small>@(repair.IsCorrection ? "Added" : "Recorded") @ValueWords.Time(repair.RecordedAtUtc) by @repair.RecordedBy</small></li>
            }
        </ul>
    }
    <small class="bp-field-help d-block mt-2">Seen by the battery's cluster, market surveillance and @(Model.ForTeam ? "your team" : "the team"), with the status.</small>
    @if (Model.MayAddNote)
    {
        <details class="bp-module-edit mt-2">
            <summary>Add a note to the history</summary>
            <form method="post" action="@Model.NoteAction" class="bp-dialog-form mt-2">
                @Html.AntiForgeryToken()
                <label><span>Entries are never changed or removed. To correct one, add a note; it shows as "Correction".</span><input type="text" name="note" maxlength="@StatusChangeService.MaxDescription" placeholder="e.g. The repair of 20 Oct was on module 5, not module 4." /></label>
                <div class="bp-live-save"><span></span><button type="submit" class="bp-secondary-button">Add note</button></div>
            </form>
        </details>
    }

    @if (Model.CanChange)
    {
        <dialog id="status-dialog" class="bp-modal-panel bp-dialog bp-dialog-wide" aria-labelledby="status-dialog-title" data-status-dialog>
            <form method="post" action="@Model.Restated.SaveAction" class="bp-dialog-form">
                @Html.AntiForgeryToken()
                <div data-status-step="1">
                    <h2 id="status-dialog-title">Change the battery status</h2>
                    <p class="bp-help-text">Now: <strong>@Model.Status</strong>. What happened to the battery?</p>
                    <div class="bp-choice-list">
                        @foreach (var (what, label, help) in Model.Choices)
                        {
                            <label class="bp-choice"><input type="radio" name="what" value="@what" checked="@(what == Model.Choices[0].What)" data-status-choice="@label" /><strong>@label</strong><small>@help</small></label>
                        }
                    </div>
                    <div class="bp-dialog-actions">
                        <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
                        <button type="button" class="bp-primary-button" data-status-next>Next</button>
                    </div>
                </div>
                <div data-status-step="2">
                    <h2><span data-status-chosen>@Model.Choices[0].Label</span>: re-state the values</h2>
                    <p class="bp-help-text">Check each value; they start as the battery's current ones.</p>
                    <div data-status-repair>
                        <label><span>Day of the repair</span><input type="date" name="repairDate" value="@Model.Today" max="@Model.Today" /></label>
                        <label><span>What was repaired</span><input type="text" name="repairDescription" maxlength="@StatusChangeService.MaxDescription" placeholder="e.g. Module 4 replaced after a coolant leak" /></label>
                    </div>
                    <div class="bp-attr-list">
                        @foreach (var field in Model.Restated.Sections.SelectMany(section => section.Fields))
                        {
                            @await Html.PartialAsync("~/Views/ProductData/_EditorField.cshtml", new EditorFieldView(Model.Restated, field))
                            @if (field.Attribute.Number == 59 && Model.RatedCapacityAh is { } rated)
                            {
                                <div class="bp-suggest" data-fade-suggest data-rated="@rated.ToString(System.Globalization.CultureInfo.InvariantCulture)">
                                    <label><span>Measured remaining capacity (Ah)</span><input type="text" inputmode="decimal" data-fade-remaining /></label>
                                    <button type="button" class="bp-text-button" data-fade-button>Suggest the fade</button>
                                    <small class="bp-field-help" data-fade-note>1 − remaining / @ValueWords.Number(rated) Ah rated. The measured capacity isn't stored.</small>
                                </div>
                            }
                        }
                    </div>
                    <div class="bp-dialog-actions">
                        <button type="button" class="bp-secondary-button" data-status-back>Back</button>
                        <button type="submit" class="bp-primary-button">Save and publish a new version</button>
                    </div>
                </div>
            </form>
        </dialog>
    }
</section>
```

`web/wwwroot/js/bp-status-change.js`:

```
// The "Change status" window (step 4b): what happened first, then the details; and the capacity fade suggested from a
// measured remaining capacity (1 - remaining / rated, J-11). Without this script both steps show and the form still works.
(() => {
    document.querySelectorAll('[data-status-dialog]').forEach((dialog) => {
        dialog.classList.add('is-stepped');
        const chosen = () => dialog.querySelector('input[name="what"]:checked');
        const show = (step) => {
            dialog.classList.toggle('is-step-2', step === 2);
            const choice = chosen();
            dialog.querySelector('[data-status-chosen]').textContent = choice?.dataset.statusChoice ?? '';
            const repair = dialog.querySelector('[data-status-repair]');
            const repaired = choice?.value === 'Repaired';
            repair.hidden = !repaired;
            repair.querySelectorAll('input').forEach((input) => { input.required = repaired; });
        };
        dialog.querySelector('[data-status-next]')?.addEventListener('click', () => show(2));
        dialog.querySelector('[data-status-back]')?.addEventListener('click', () => show(1));
        dialog.addEventListener('close', () => show(1));
        show(1);
    });

    document.querySelectorAll('[data-fade-suggest]').forEach((suggest) => {
        const rated = Number(suggest.dataset.rated);
        const remaining = suggest.querySelector('[data-fade-remaining]');
        const note = suggest.querySelector('[data-fade-note]');
        const fade = suggest.closest('form')?.querySelector('input[name="v.capacityFade"]');
        suggest.querySelector('[data-fade-button]')?.addEventListener('click', () => {
            const measured = Number(String(remaining.value).replace(',', '.'));
            if (!fade || !(rated > 0) || !(measured >= 0) || remaining.value.trim() === '') {
                note.textContent = 'Type the measured remaining capacity in Ah first.';
                return;
            }

            const percent = Math.max(0, Math.round((1 - measured / rated) * 100)); // the passport keeps whole percent
            fade.value = String(percent);
            fade.dispatchEvent(new Event('input', { bubbles: true }));
            note.textContent = `1 − ${measured} / ${rated} Ah rated = ${percent} %. The measured capacity isn't stored.`;
        });
    });
})();
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~StatusPagesTests | FullyQualifiedName~StatusChangeTests | FullyQualifiedName~BatteryPagesTests | FullyQualifiedName~ClusterBatteryPagesTests | FullyQualifiedName~BatteriesControllerTests"`
Expected: `Passed!  - Failed:     0, Passed:   106`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2177`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs BatteryPassWeb.Tests/Dpp/BatteryTesting.cs BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs web/Controllers/BatteriesController.cs web/Controllers/ClusterBatteriesController.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Dpp/Pages/PassportPages.cs web/Dpp/Pages/StatusPages.cs web/Dpp/Pages/ValueWords.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_StatusCard.cshtml web/Views/ClusterAdmin/Battery.cshtml web/Views/Passport/_PassportTabs.cshtml web/wwwroot/css/bp-product-data.css web/wwwroot/js/bp-status-change.js
git commit -m "feat(dpp): the status card and window on both battery pages, the repair history on the detail page" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The status examples in the demo data

The demo seed repairs DEMO-C7M-0001 (the repair its accident report describes) and adds DEMO-CORE-0003, published and then set to waste, both through the status change like a person. The demo tests count 7 batteries, 4 published.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`
- Modify: `web/Dpp/Demo/DemoCatalogue.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Dpp/Demo/demo-catalogue.json`

- [ ] **Step 1: Write the failing tests**

Patch `4b-task3-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoCardTests.cs`, `BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`, `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`, `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`). Save the block as `../4b-task3-tests.patch`, outside the worktree, and run `git apply ../4b-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
index dab564c..9083866 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
@@ -51,7 +51,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
         var result = await controller.ResetDemoData(CancellationToken.None);
 
         Assert.Equal("/admin/clusters?tab=clusters", Assert.IsType<RedirectResult>(result).Url);
-        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 6 batteries, 3 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
+        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 7 batteries, 4 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
         var tokens = JsonSerializer.Deserialize<List<DemoToken>>((string)controller.TempData["GeneratedCredentials"]!)!;
         Assert.Equal(7, tokens.Count);
         Assert.All(tokens, token => Assert.False(string.IsNullOrWhiteSpace(token.Value)));
@@ -66,7 +66,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
 
         await controller.ClearDemoData(CancellationToken.None);
 
-        Assert.StartsWith("Demo data cleared: 6 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
+        Assert.StartsWith("Demo data cleared: 7 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
         Assert.Null(controller.TempData["GeneratedCredentials"]);
         Assert.Equal(0, await rig.Count(DppStore.Packs));
     }
diff --git a/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs b/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
index 6a2921f..6f1618e 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
@@ -67,7 +67,8 @@ public sealed class DemoCatalogueTests
     [Fact]
     public void TheBatteries_ShouldBeInSeveralStates()
     {
-        Assert.Equal(["DEMO-C7M-0001", "DEMO-C13M-0001", "DEMO-CORE-0001"], Catalogue.Packs.Where(pack => pack.Publish).Select(pack => pack.Serial));
+        Assert.Equal(["DEMO-C7M-0001", "DEMO-C13M-0001", "DEMO-CORE-0001", "DEMO-CORE-0003"], Catalogue.Packs.Where(pack => pack.Publish).Select(pack => pack.Serial));
+        Assert.Equal(["DEMO-C7M-0001 Repaired", "DEMO-CORE-0003 Waste"], Catalogue.Packs.Where(pack => pack.Status is not null).Select(pack => $"{pack.Serial} {pack.Status!.What}"));
         Assert.Equal(["DEMO-C7M-0001"], Catalogue.Packs.Where(pack => pack.NewVersion is not null).Select(pack => pack.Serial));
         Assert.Equal(2, Catalogue.Packs.Count(pack => pack.Accident is not null));
         Assert.Equal(2, Catalogue.Packs.Count(pack => !pack.Values.ContainsKey("manufacturingDate")));
diff --git a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
index dc81dfc..5190a9c 100644
--- a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
@@ -29,7 +29,7 @@ public sealed class DemoClearTests(MongoFixture mongo)
         }
 
         Assert.Empty(result.Notes);
-        Assert.Contains("6 batteries removed", result.Done);
+        Assert.Contains("7 batteries removed", result.Done);
         Assert.Contains("12 accounts removed", result.Done);
     }
 
@@ -44,7 +44,7 @@ public sealed class DemoClearTests(MongoFixture mongo)
 
         Assert.Equal(before + 1, await rig.Count("applicationAuditEvents"));
         var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.ClearedEvent))).Single();
-        Assert.Equal(6, entry["metadata"]["batteries"].ToInt32());
+        Assert.Equal(7, entry["metadata"]["batteries"].ToInt32());
     }
 
     [Fact]
@@ -97,12 +97,12 @@ public sealed class DemoClearTests(MongoFixture mongo)
         var result = await rig.Demo.ResetAsync(DemoRig.Who);
 
         var packs = (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).ToList();
-        Assert.Equal(6, packs.Count);
+        Assert.Equal(7, packs.Count);
         Assert.Empty(packs.Intersect(firstPacks));
-        Assert.Equal(4, await rig.Count(DppStore.Versions));
+        Assert.Equal(7, await rig.Count(DppStore.Versions));
         Assert.Equal(7, result.Tokens.Count);
         Assert.Empty(result.Tokens.Select(token => token.Value).Intersect(first.Tokens.Select(token => token.Value)));
-        Assert.Contains("6 batteries removed", result.Done);
-        Assert.Contains("6 batteries, 3 of them published", result.Done);
+        Assert.Contains("7 batteries removed", result.Done);
+        Assert.Contains("7 batteries, 4 of them published", result.Done);
     }
 }
diff --git a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
index 78d799a..bdfdf5e 100644
--- a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
@@ -30,8 +30,8 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.Equal(2, await rig.Count(DppStore.Sites));
         Assert.Equal(2, await rig.Count(DppStore.Operators));
         Assert.Equal(3, await rig.Count(DppStore.Batches));
-        Assert.Equal(6, await rig.Count(DppStore.Packs));
-        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "6 batteries, 3 of them published", "12 accounts and 7 API tokens"], result.Done);
+        Assert.Equal(7, await rig.Count(DppStore.Packs));
+        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "7 batteries, 4 of them published", "12 accounts and 7 API tokens"], result.Done);
     }
 
     [Fact]
@@ -42,14 +42,31 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         await rig.Demo.SeedAsync(DemoRig.Who);
 
         var versions = await rig.All(DppStore.Versions);
-        Assert.Equal(4, versions.Count);
-        Assert.Equal(3, versions.Count(version => version["DppStatus"].AsString == "active"));
+        Assert.Equal(7, versions.Count); // C7M-0001: 3 (a new version, then its repair); CORE-0003: 2 (waste); two with 1
+        Assert.Equal(4, versions.Count(version => version["DppStatus"].AsString == "active"));
         Assert.All(versions, version => Assert.True(version.Contains("Proof")));
         var firstPack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single()["_id"].AsString;
-        Assert.Equal([1, 2], versions.Where(version => version["PackId"] == firstPack).Select(version => version["Number"].ToInt32()).Order());
+        Assert.Equal([1, 2, 3], versions.Where(version => version["PackId"] == firstPack).Select(version => version["Number"].ToInt32()).Order());
         Assert.All(await rig.All(DppStore.Packs), pack => Assert.StartsWith("urn:x-battery-pass:TEST-", pack["Values"]["uniqueProductIdentifier"]["Value"].AsString, StringComparison.Ordinal));
     }
 
+    [Fact]
+    public async Task TheStatusExamples_ShouldBeARepairedBatteryAndAWasteOne()
+    {
+        using var rig = await DemoRig.StartAsync(mongo);
+
+        await rig.Demo.SeedAsync(DemoRig.Who);
+
+        var repaired = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single();
+        var waste = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-CORE-0003"))).Single();
+        Assert.Equal("re-used", repaired["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
+        Assert.Equal("Module 4 replaced after a coolant leak.", repaired["Repairs"].AsBsonArray.Single()["Description"].AsString);
+        Assert.Equal(6, repaired["Values"]["capacityFade"]["Value"]["percentageValue"].ToInt32());
+        Assert.Equal("waste", waste["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
+        Assert.Empty(waste["Repairs"].AsBsonArray);
+        Assert.Equal(2, await rig.Count("applicationAuditEvents", Eq("eventType", BatteryPassWeb.Dpp.Editing.StatusChangeService.ChangedEvent)));
+    }
+
     [Fact]
     public async Task TheDrafts_ShouldShowAReadyOneAndTwoWithMissingValues()
     {
@@ -71,7 +88,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         await rig.Demo.SeedAsync(DemoRig.Who);
 
         var packs = await rig.All(DppStore.Packs);
-        Assert.Equal([0, 0, 0, 7, 10, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
+        Assert.Equal([0, 0, 0, 7, 10, 10, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
         Assert.Equal(2, await rig.Count(DocumentStore.Records, Eq("Property", "informationOnAccidents")));
         Assert.Equal(12, await rig.Count(DocumentStore.Records, Eq("OwnerKind", "Model")));
         Assert.Equal(3, await rig.Count(DocumentStore.Records, Eq("Property", "resultsOfTestReportsProvingCompliance")));
@@ -132,7 +149,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 
         Assert.Equal([DemoDataService.NoPassword], result.Notes);
         Assert.Equal(0, await rig.Count("users"));
-        Assert.Equal(6, await rig.Count(DppStore.Packs));
+        Assert.Equal(7, await rig.Count(DppStore.Packs));
     }
 
     [Fact]
@@ -181,7 +198,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         var again = await rig.Demo.SeedAsync(DemoRig.Who);
 
         Assert.Equal(["The demo data is already there: reset it or clear it first."], again.Notes);
-        Assert.Equal(6, await rig.Count(DppStore.Packs));
+        Assert.Equal(7, await rig.Count(DppStore.Packs));
     }
 
     [Fact]
@@ -204,6 +221,6 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 
         var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.SeededEvent))).Single();
         Assert.Equal(DemoRig.Who, entry["actor"].AsString);
-        Assert.Equal((6, 3, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
+        Assert.Equal((7, 4, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
     }
 }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo"`
Expected: the build fails with CS1061 (for example `error CS1061 'DemoPack' does not contain a definition for 'Status'`).

- [ ] **Step 3: Write the code**

Patch `4b-task3-code.patch` (`web/Dpp/Demo/DemoCatalogue.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/Demo/demo-catalogue.json`). Save the block as `../4b-task3-code.patch`, outside the worktree, and run `git apply ../4b-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Demo/DemoCatalogue.cs b/web/Dpp/Demo/DemoCatalogue.cs
index 47eba8e..65d8109 100644
--- a/web/Dpp/Demo/DemoCatalogue.cs
+++ b/web/Dpp/Demo/DemoCatalogue.cs
@@ -17,10 +17,17 @@ public sealed record DemoNamed(string Key, string Name, JsonObject Values);
 
 public sealed record DemoBatch(string Key, string Model, string Site, int Year, string TestReport, JsonObject Values);
 
+/// <summary>
+/// A status change the seed makes on a published demo battery (step 4b): what happened ("Repaired" or "Waste"), the
+/// re-stated values that differ from the battery's current ones, and for a repair how many days ago and what.
+/// </summary>
+public sealed record DemoStatus(string What, JsonObject? Restated = null, int DaysAgo = 0, string? Repair = null);
+
 /// <summary>
 /// A demo battery. <see cref="Publish"/> publishes it; <see cref="NewVersion"/> then changes these values, which makes
 /// version 2; <see cref="History"/> writes seven days of live values; <see cref="Modules"/> imports that many modules
-/// from an export-shaped file; <see cref="Accident"/> adds an accident report.
+/// from an export-shaped file; <see cref="Accident"/> adds an accident report; <see cref="Status"/> then changes its
+/// status through the app's own status change.
 /// </summary>
 public sealed record DemoPack(
     string Serial,
@@ -32,7 +39,8 @@ public sealed record DemoPack(
     JsonObject? NewVersion = null,
     bool History = false,
     int Modules = 0,
-    string? Accident = null);
+    string? Accident = null,
+    DemoStatus? Status = null);
 
 /// <summary>
 /// What the demo data seed makes (decision 32): the Compact and Core families of the old demo on the new model, with
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index a7c3ee4..4bdeae5 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -33,6 +33,7 @@ public sealed class DemoDataService(
     PackCreationService packCreation,
     SavePipeline pipeline,
     PublicationService publication,
+    StatusChangeService statusChanges,
     DocumentService documents,
     DocumentStore documentStore,
     DppLinks links,
@@ -193,6 +194,11 @@ public sealed class DemoDataService(
             {
                 await documents.AddAccidentReportAsync(team, packId, new DocumentUpload("accident-report.pdf", DemoPdf.Make("Accident report", demo.Accident), demo.Accident, "en-GB"), cancellationToken);
             }
+
+            if (demo.Status is { } status && demo.Publish)
+            {
+                await ChangeStatusAsync(team, packId, demo, status, cancellationToken);
+            }
         }
 
         demoTokens.AddRange(await SeedTokensAsync(who, catalogue, cancellationToken));
@@ -459,6 +465,28 @@ public sealed class DemoDataService(
     private static Dictionary<string, JsonNode?> Values(JsonObject values) =>
         values.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone(), StringComparer.Ordinal);
 
+    /// <summary>
+    /// A demo battery's status change (step 4b), through the app's own status change like a person's: the re-stated
+    /// values start as the battery's current ones, as the window fills them in, with the catalogue's on top.
+    /// </summary>
+    private async Task ChangeStatusAsync(ChangeSender team, string packId, DemoPack demo, DemoStatus status, CancellationToken cancellationToken)
+    {
+        var sources = await store.LoadSourcesAsync(packId, cancellationToken) ?? throw new InvalidOperationException($"The demo battery {demo.Serial} is missing.");
+        var restated = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
+        foreach (var attribute in StatusChangeService.RestatedAttributes(sources.Model))
+        {
+            restated[attribute.Property] = status.Restated?[attribute.Property]?.DeepClone()
+                ?? (sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values.GetValueOrDefault(attribute.Property))?.Value?.DeepClone();
+        }
+
+        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-status.DaysAgo).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
+        var result = await statusChanges.ChangeAsync(team, packId, new StatusChange(Enum.Parse<StatusEvent>(status.What), restated, day, status.Repair), cancellationToken);
+        if (result.Problem is not null)
+        {
+            throw new InvalidOperationException($"The demo battery {demo.Serial}'s status couldn't change: {result.Problem}.");
+        }
+    }
+
     /// <summary>An export-shaped file (decision 31) with this many modules for the battery.</summary>
     private static byte[] ModuleExport(string serial, int modules)
     {
diff --git a/web/Dpp/Demo/demo-catalogue.json b/web/Dpp/Demo/demo-catalogue.json
index f581e27..d1da27d 100644
--- a/web/Dpp/Demo/demo-catalogue.json
+++ b/web/Dpp/Demo/demo-catalogue.json
@@ -891,7 +891,16 @@
       },
       "history": true,
       "modules": 7,
-      "accident": "Coolant leak found at the 12-month service; module 4 replaced."
+      "accident": "Coolant leak found at the 12-month service; module 4 replaced.",
+      "status": {
+        "what": "Repaired",
+        "daysAgo": 10,
+        "repair": "Module 4 replaced after a coolant leak.",
+        "restated": {
+          "capacityFade": 6,
+          "powerFade": 4
+        }
+      }
     },
     {
       "serial": "DEMO-C7M-0002",
@@ -1000,6 +1009,37 @@
           "celsiusValue": 19
         }
       }
+    },
+    {
+      "serial": "DEMO-CORE-0003",
+      "batch": "core",
+      "operator": "fleet",
+      "cluster": "demo-fleet",
+      "values": {
+        "batterySerialNumber": "DEMO-CORE-0003",
+        "thirdPartyServiceProvider": "Demo Passport Hosting AB",
+        "manufacturingDate": "2026-07",
+        "warrantyPeriod": "P8Y",
+        "stateOfChargeSoC": {
+          "percentUnit": "%",
+          "percentageValue": 0
+        },
+        "numberOfFullChargingAndDischargingCycles": 2950,
+        "temperatureInformation": {
+          "degreeCelsiusUnit": "°C",
+          "celsiusValue": 15
+        }
+      },
+      "publish": true,
+      "modules": 10,
+      "status": {
+        "what": "Waste",
+        "restated": {
+          "capacityFade": 38,
+          "powerFade": 31,
+          "expectedLifetimeInCalendarYears": 0
+        }
+      }
     }
   ]
 }
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo"`
Expected: `Passed!  - Failed:     0, Passed:    40`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2178`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCardTests.cs BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs BatteryPassWeb.Tests/Demo/DemoClearTests.cs BatteryPassWeb.Tests/Demo/DemoSeedTests.cs web/Dpp/Demo/DemoCatalogue.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/Demo/demo-catalogue.json
git commit -m "feat(dpp): a repaired and a waste battery in the demo data" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2178, Skipped:     0, Total:  2178`. From the 2129 before: 2163 after Task 1, 2177 after Task 2, 2178 after Task 3.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat a77015d -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. The managing session brings them to the product owner:

1. **Restart the app with the usual launcher**, sign in with a team account and press "Reset demo data" at the end of the Clusters tab, so the demo data has the two new examples.
2. **As the team, on the computer:**
   - **Batteries, DEMO-C7M-0001:** "Battery status: Re-used, after a repair", with "Module 4 replaced after a coolant leak." in the history, and 3 versions.
   - **"Change status", "Correct the values":** change the power fade and save. It says "Version 4 … published", and nothing joins the history.
   - **DEMO-CORE-0003:** "Waste". "Change status" offers only "Correct the values".
3. **As a demo cluster admin** (`demo.north.admin@example.test`), on the phone:
   - **Registry, "Manage" on DEMO-C7M-0001, "Change status", "Repaired", "Next":** type what was repaired. Try "Suggest the fade" with a measured capacity, then save.
   - **"Add a note to the history":** add one. It shows as "Correction", and the versions stay as they were.
   - **There is no "Correct the values" choice** for a cluster admin.
4. **As the demo cluster user** (`demo.north.user@example.test`): the detail page's Purchaser tab shows the repair history.
5. **Light and dark:** the moon button in the top bar switches.

## Next

Plan 4c (successor passports, "invalid" and "marked for deletion") adds the three choices that lead to a new linked passport, and the passport notices.
