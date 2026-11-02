# DPP V3 Step 4c-2: "Invalid" and "Marked for Deletion" — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The two remaining DPP statuses (#2, design 7.6 and 7.7; J-8; decision 34).
- **Invalid:** the team (on the battery page, mock-up S5) or market surveillance (on the detail page, S6) marks an active passport invalid, with a reason. The reason is seen only by the team and market surveillance. The public and detail pages say "invalid" and the date (S7).
- **Marked for deletion:** the team marks a waste battery's passport for deletion, after recycling or export.
- **Both:** the passport takes no more changes, and only the team undoes either, with the reason kept in the audit log. An invalid or marked-for-deletion battery stays in the Registry with its status, so market surveillance finds what it flagged.
- **Demo data:** a new DEMO-C7M-0003 marked invalid by the demo market-surveillance account, and DEMO-CORE-0003 (waste) marked for deletion.

**Architecture:**
- **One small service, `PassportMarks`:**
  - It sets the active version's DPP status in place (`SetVersionStatusAsync`, #4 moves too). #2 isn't signed (4.3), so no new version is made.
  - It keeps the mark on the pack (`BatteryPack.Mark`: status, reason, who, team or market surveillance, when) and logs every change with its reason.
  - Undo puts the newest version back to active and clears the mark.
  - Plan 4c-1's freeze (`DppStatuses.IsNoLongerActive`) already covers both statuses: no change, status change, note or new version.
- **Market surveillance as a sender:** `ChangeSender.MarketSurveillance` is a new `SenderKind`. `EditRights` gives it no attribute, so it marks passports and changes no value. The detail page's `POST /dpp/{id}/detail/invalid` accepts only a reader with the market-surveillance role, and anyone else finds nothing.
- **Pages:**
  - The team's battery page gets the "Passport status" card (`_PassportStatusCard.cshtml`, S5), with the mark's reason and an undo button when marked.
  - The passport pages get the S7 line for both statuses, plus the reason for the team and market surveillance.
  - Market surveillance gets a "Mark as invalid…" form on an active passport's detail page.
  - The Registry lists invalid and marked-for-deletion batteries with their status pill. There is no "Manage" link for them, since nothing on them changes.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 7.6, 7.7 and 12, and section 14's step-4 note. Decisions record: decision 34; J-8. Mock-up (approved 31 Oct 2026): `docs/mockups/dpp-v3-step4-purchaser-and-lifecycle.html`, screens S5, S6 and S7.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 8d8a0db (the end of plan 4c-1's run), plus the commit that adds this plan. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo).
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. Whole-suite runs also take `--blame-hang --blame-hang-timeout 10m`.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop it (and any `dotnet` and `testhost` it left) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2199`. After this plan: 2212. The suite takes about two to four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../4c2-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New and rewritten files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (2 Nov 2026, through the managing session)

Decision 34 holds step 4's answers; these are 4c-2's, added to it by this plan's commit.

1. **Only an active passport is marked**, invalid or for deletion. That includes one waiting for its new passport; an inactive or archived passport is already out of use.
2. **Invalid:** marked by the team or market surveillance, with a required reason, seen only by the team and market surveillance. Only the team undoes it.
3. **Marked for deletion:** by the team, for a waste battery only. The team undoes it.
4. **Both stay in the Registry, with their status**, for everyone who could list the battery before: market surveillance finds what it flagged, and the cluster still owns the battery. Only an inactive battery leaves the Registry, as its new passport takes its place.
5. **Demo:**
   - A new published DEMO-C7M-0003 in demo-north is marked invalid by the demo market-surveillance account, with the reason "Demo: wrong carbon footprint figures".
   - DEMO-CORE-0003 (waste) is marked for deletion.
   - The demo data then has 9 batteries, 6 of them published.

## Found while prototyping, and what this plan does

- **There was no market surveillance sender.** Senders were the team, cluster admins and battery-system feeds. `SenderKind.MarketSurveillance` is added; `EditRights` gives it no attribute (its `switch` falls to "no").
- **`[Authorize]` on the new detail-page action would be overridden** by the controller's `[AllowAnonymous]` (warning ASP0026). The action checks the reader's role itself instead.
- **The Registry's "Manage" link** now needs an active passport, so an invalid or marked-for-deletion row has none.

## What happens to the old tests

Nothing is removed.
- **`BatteriesControllerTests`' controller helper** takes `PassportMarks` (Task 2).
- **The demo tests' counts:** 9 batteries, 6 published, 10 versions: 3 active, 1 inactive, 1 invalid, 1 marked for deletion (Task 3).

## A part to write yourself (optional)

The product owner chose "you write it" before; the plan gives the code. If they want to try one, the natural spot is `PassportMarks.MarkForDeletionAsync` in Task 1: who may mark a passport for deletion, and when. `OnlyAWasteBattery_ShouldBeMarkedForDeletionByTheTeam` lists it.

---

### Task 1: Invalid and marked for deletion, and their undo

The rules before the pages: the team or market surveillance (a new sender kind that changes no value) marks an active passport invalid with a reason; the team marks a waste battery's passport for deletion; either sets the version's DPP status in place, keeps the mark on the pack and logs it with its reason, and plan 4c-1's freeze then holds. Only the team undoes either.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/PassportMarkTests.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Editing/ChangeSender.cs`
- Create: `web/Dpp/Editing/PassportMarks.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Store/DppStore.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PassportMarkTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// "Invalid" and "marked for deletion" (design 7.6 and 7.7, J-8, decision 34; step 4c-2): set on an active passport,
/// with who and why; the passport then takes no more changes; only the team undoes either.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportMarkTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    private static readonly ChangeSender Surveillance = ChangeSender.MarketSurveillance("msa@example.com");

    private const string Reason = "Declared values don't match the test report";

    [Fact]
    public async Task MarketSurveillance_ShouldMarkAnActivePassportInvalidWithItsReason()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        rig.Clock.Now = rig.Clock.Now.AddHours(1);

        var problem = await Service(rig).MarkInvalidAsync(Surveillance, rig.PackId, $"  {Reason}  ");

        Assert.Null(problem);
        var version = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;
        Assert.Equal(DppStatuses.Invalid, version.DppStatus);
        Assert.Equal(1, version.Number); // no new version: #2 isn't signed
        Assert.Equal(rig.Clock.Now.UtcDateTime, version.LastUpdatedUtc);
        var mark = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Mark!;
        Assert.Equal(DppStatuses.Invalid, mark.Status);
        Assert.Equal(Reason, mark.Reason);
        Assert.Equal("msa@example.com", mark.By);
        Assert.Equal(nameof(SenderKind.MarketSurveillance), mark.ByRole);
        Assert.Equal(rig.Clock.Now.UtcDateTime, mark.AtUtc);
        var entry = await LastAsync(rig, PassportMarks.InvalidEvent);
        Assert.Equal(Reason, entry["metadata"]["reason"].AsString);
    }

    [Fact]
    public async Task AnInvalidPassport_ShouldTakeNoChangesAndGetNoNewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await Service(rig).MarkInvalidAsync(Team, rig.PackId, Reason);

        var live = await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 40 }, "form"));
        var publish = await rig.Publication.PublishAsync(Team, rig.PackId);

        Assert.False(live.Saved);
        Assert.Contains("invalid", Assert.Single(live.Keys).Message, StringComparison.Ordinal);
        Assert.Equal(PublicationService.NoNewVersion("invalid"), publish.Problem);
    }

    [Fact]
    public async Task OnlyTheTeamAndMarketSurveillance_ShouldMarkInvalidAndOnlyWithAReason()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        Assert.Equal(PassportMarks.NotYoursToMarkInvalid, await service.MarkInvalidAsync(ChangeSender.ClusterAdmin("admin@example.com", BatteryTesting.North), rig.PackId, Reason));
        Assert.Equal(PassportMarks.NeedsReason, await service.MarkInvalidAsync(Surveillance, rig.PackId, "  "));
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task OnlyAnActivePassport_ShouldBeMarked()
    {
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var inactive = await new SuccessionTests(mongo).InactiveAsync();

        Assert.Equal(PassportMarks.OnlyActive, await Service(draft).MarkInvalidAsync(Team, draft.PackId, Reason));
        Assert.Equal(PassportMarks.OnlyActive, await Service(inactive).MarkInvalidAsync(Surveillance, inactive.PackId, Reason));
        Assert.Equal(PassportMarks.NoBattery, await Service(draft).MarkInvalidAsync(Team, "pack-unknown", Reason));
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldUndoInvalid()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.MarkInvalidAsync(Surveillance, rig.PackId, Reason);

        var bySurveillance = await service.UndoAsync(Surveillance, rig.PackId);
        var byTeam = await service.UndoAsync(Team, rig.PackId);

        Assert.Equal(PassportMarks.OnlyTheTeamUndoes, bySurveillance);
        Assert.Null(byTeam);
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Mark);
        Assert.Equal(Reason, (await LastAsync(rig, PassportMarks.UndoneEvent))["metadata"]["reason"].AsString); // kept in the log
        Assert.True((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 41 }, "form"))).Saved);
    }

    [Fact]
    public async Task OnlyAWasteBattery_ShouldBeMarkedForDeletionByTheTeam()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        var notWaste = await service.MarkForDeletionAsync(Team, rig.PackId);
        await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
        var bySurveillance = await service.MarkForDeletionAsync(Surveillance, rig.PackId);
        var marked = await service.MarkForDeletionAsync(Team, rig.PackId);

        Assert.Equal(PassportMarks.OnlyWaste, notWaste);
        Assert.Equal(PassportMarks.OnlyTheTeamMarksForDeletion, bySurveillance);
        Assert.Null(marked);
        Assert.Equal(DppStatuses.MarkedForDeletion, await rig.Store.PassportStatusAsync(rig.PackId));
        Assert.Equal(DppStatuses.MarkedForDeletion, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Mark!.Status);
        Assert.Null(await service.UndoAsync(Team, rig.PackId));
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task NothingToUndo_ShouldBeSaid()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Equal(PassportMarks.NothingToUndo, await Service(rig).UndoAsync(Team, rig.PackId));
    }

    internal static PassportMarks Service(PublicationRig rig) =>
        new(rig.Store, new ApplicationAuditService(rig.Context), rig.Clock);

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkTests | FullyQualifiedName~SuccessionTests"`
Expected: the build fails with CS0246 (for example `error CS0246 The type or namespace name 'PassportMarks' could not be found`).

- [ ] **Step 3: Write the code**

Patch `4c2-task1-code.patch` (`web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Editing/ChangeSender.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Store/DppStore.cs`). Save the block as `../4c2-task1-code.patch`, outside the worktree, and run `git apply ../4c2-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index 4da3924..75c3bce 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -34,6 +34,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<PackCreationService>();
         services.AddSingleton<StatusChangeService>();
         services.AddSingleton<SuccessionService>();
+        services.AddSingleton<PassportMarks>();
         services.AddSingleton<PassportSigner>();
         services.AddSingleton<PublicationChecks>();
         services.AddSingleton<PublicationService>();
diff --git a/web/Dpp/Editing/ChangeSender.cs b/web/Dpp/Editing/ChangeSender.cs
index 10ac0cc..9560cfd 100644
--- a/web/Dpp/Editing/ChangeSender.cs
+++ b/web/Dpp/Editing/ChangeSender.cs
@@ -7,6 +7,9 @@ public enum SenderKind
     Team,
     ClusterAdmin,
     BatterySystemFeed,
+
+    /// <summary>Market surveillance (step 4c-2): marks a passport invalid and changes no value.</summary>
+    MarketSurveillance,
 }
 
 /// <summary>
@@ -23,6 +26,8 @@ public sealed record ChangeSender(SenderKind Kind, string Who, IReadOnlySet<stri
 
     public static ChangeSender Feed(string who, params string[] clusterIds) =>
         new(SenderKind.BatterySystemFeed, who, clusterIds.ToHashSet(StringComparer.Ordinal));
+
+    public static ChangeSender MarketSurveillance(string who) => new(SenderKind.MarketSurveillance, who, new HashSet<string>(StringComparer.Ordinal));
 }
 
 /// <summary>"May this sender change this attribute?" (design 6.1, decisions 9 and 10, J-10).</summary>
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index 18a18b8..b524a78 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -131,6 +131,26 @@ public sealed class BatteryPack : IDppRecord
 
     /// <summary>The pack whose passport this one replaces (design 7.5); null for a battery placed on the market new.</summary>
     public string? SuccessorOf { get; set; }
+
+    /// <summary>Why the passport is invalid or marked for deletion, by whom and when (step 4c-2); null otherwise.</summary>
+    public PassportMark? Mark { get; set; }
+}
+
+/// <summary>An invalid or marked-for-deletion passport's mark (design 7.6 and 7.7): its reason is seen by the team and market surveillance only.</summary>
+public sealed class PassportMark
+{
+    /// <summary>invalid or marked-for-deletion.</summary>
+    public required string Status { get; init; }
+
+    public string? Reason { get; init; }
+
+    public required string By { get; init; }
+
+    /// <summary>The sender's kind: Team or MarketSurveillance.</summary>
+    public required string ByRole { get; init; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public required DateTime AtUtc { get; init; }
 }
 
 /// <summary>A battery placed on the market again, and its new passport (design 7.5).</summary>
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index d75b266..45ac075 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -307,6 +307,16 @@ public sealed class DppStore
             Builders<PassportVersion>.Update.Set(version => version.DppStatus, status).Set(version => version.LastUpdatedUtc, lastUpdatedUtc),
             cancellationToken: cancellationToken);
 
+    /// <summary>Sets or clears a pack's invalid or deletion mark (step 4c-2). False when the pack doesn't exist.</summary>
+    public async Task<bool> SetMarkAsync(string packId, PassportMark? mark, CancellationToken cancellationToken = default)
+    {
+        var result = await Collection<BatteryPack>().UpdateOneAsync(
+            pack => pack.Id == packId,
+            Builders<BatteryPack>.Update.Set(pack => pack.Mark, mark),
+            cancellationToken: cancellationToken);
+        return result.MatchedCount == 1;
+    }
+
     /// <summary>Sets or clears a pack's succession (design 7.5). False when the pack doesn't exist.</summary>
     public async Task<bool> SetSuccessionAsync(string packId, Succession? succession, CancellationToken cancellationToken = default)
     {
```

`web/Dpp/Editing/PassportMarks.cs`:

```csharp
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>
/// "Invalid" and "marked for deletion" (design 7.6 and 7.7, J-8, decision 34; step 4c-2). The team or market
/// surveillance marks an active passport invalid, with a reason; the team marks a waste battery's passport for deletion,
/// after recycling or export. Either sets the active version's DPP status (#2) in place, with no new version, and the
/// passport then takes no more changes. Only the team undoes either; the reason stays in the audit log. Null when done.
/// </summary>
public sealed class PassportMarks(DppStore store, ApplicationAuditService audit, TimeProvider clock)
{
    public const string InvalidEvent = "dpp.passport-invalid";
    public const string DeletionEvent = "dpp.passport-marked-for-deletion";
    public const string UndoneEvent = "dpp.passport-mark-undone";

    public const string NoBattery = "the battery was not found";
    public const string OnlyActive = "only an active passport is marked";
    public const string NotYoursToMarkInvalid = "only the team and market surveillance mark a passport invalid";
    public const string NeedsReason = "give the reason";
    public const string OnlyTheTeamMarksForDeletion = "only the team marks a passport for deletion";
    public const string OnlyWaste = "only a waste battery's passport is marked for deletion, after recycling or export";
    public const string OnlyTheTeamUndoes = "only the team undoes it";
    public const string NothingToUndo = "the passport is neither invalid nor marked for deletion";

    /// <summary>The longest reason kept, in characters.</summary>
    public const int MaxReason = 500;

    public async Task<string?> MarkInvalidAsync(ChangeSender sender, string packId, string? reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind is not (SenderKind.Team or SenderKind.MarketSurveillance))
        {
            return NotYoursToMarkInvalid;
        }

        var why = reason?.Trim() ?? string.Empty;
        if (why.Length == 0)
        {
            return NeedsReason;
        }

        return await MarkAsync(sender, packId, DppStatuses.Invalid, why.Length > MaxReason ? why[..MaxReason] : why, cancellationToken);
    }

    public async Task<string?> MarkForDeletionAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.Team)
        {
            return OnlyTheTeamMarksForDeletion;
        }

        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is { } pack && StatusChangeService.CurrentStatus(pack) != "waste"
            && await store.GetActiveVersionAsync(packId, cancellationToken) is not null)
        {
            return OnlyWaste;
        }

        return await MarkAsync(sender, packId, DppStatuses.MarkedForDeletion, null, cancellationToken);
    }

    /// <summary>The team puts an invalid or marked-for-deletion passport back to active (J-8).</summary>
    public async Task<string?> UndoAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return NoBattery;
        }

        if (sender.Kind != SenderKind.Team)
        {
            return OnlyTheTeamUndoes;
        }

        var latest = await store.GetLatestVersionAsync(packId, cancellationToken);
        if (latest is null || latest.DppStatus is not (DppStatuses.Invalid or DppStatuses.MarkedForDeletion))
        {
            return NothingToUndo;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await store.SetVersionStatusAsync(latest.Id, DppStatuses.Active, now, cancellationToken);
        await store.SetMarkAsync(packId, null, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            UndoneEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"Battery {Serial(pack)}: {DppStatuses.Words(latest.DppStatus).ToLowerInvariant()} undone; the passport is active again",
            new BsonDocument
            {
                ["was"] = latest.DppStatus,
                ["reason"] = (BsonValue?)pack.Mark?.Reason ?? BsonNull.Value,
                ["markedBy"] = (BsonValue?)pack.Mark?.By ?? BsonNull.Value,
            },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }

    private async Task<string?> MarkAsync(ChangeSender sender, string packId, string status, string? reason, CancellationToken cancellationToken)
    {
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return NoBattery;
        }

        if (await store.GetActiveVersionAsync(packId, cancellationToken) is not { } active)
        {
            return OnlyActive;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await store.SetVersionStatusAsync(active.Id, status, now, cancellationToken);
        await store.SetMarkAsync(packId, new PassportMark { Status = status, Reason = reason, By = sender.Who, ByRole = sender.Kind.ToString(), AtUtc = now }, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            status == DppStatuses.Invalid ? InvalidEvent : DeletionEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            status == DppStatuses.Invalid
                ? $"Battery {Serial(pack)}: passport marked invalid. {reason}"
                : $"Battery {Serial(pack)}: passport marked for deletion",
            new BsonDocument { ["version"] = active.Number, ["reason"] = (BsonValue?)reason ?? BsonNull.Value },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }

    private static string Serial(BatteryPack pack) =>
        pack.Values.GetValueOrDefault("batterySerialNumber")?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : pack.Id;
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkTests | FullyQualifiedName~SuccessionTests"`
Expected: `Passed!  - Failed:     0, Passed:    20`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2206`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/PassportMarkTests.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Editing/ChangeSender.cs web/Dpp/Editing/PassportMarks.cs web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs
git commit -m "feat(dpp): invalid and marked-for-deletion passports, undone by the team" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The passport status card, market surveillance's mark and the notices

Mock-up S5's "Passport status" card on the team's battery page (mark invalid with a reason, mark for deletion when waste, undo), S6's "Mark as invalid…" on an active passport's detail page for market surveillance, and S7's line on the passport pages, with the reason for the team and market surveillance only. The Registry keeps invalid and marked-for-deletion batteries, with their status and no "Manage".

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`
- Modify: `web/Controllers/BatteriesController.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`
- Modify: `web/Dpp/Pages/RegistryList.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Create: `web/Views/Batteries/_PassportStatusCard.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/Views/Passport/_PassportHero.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `4c2-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`). Save the block as `../4c2-task2-tests.patch`, outside the worktree, and run `git apply ../4c2-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
index 0c4e5f4..54f9b89 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
@@ -202,7 +202,7 @@ public sealed class BatteriesControllerTests(MongoFixture mongo)
                 "test")),
         };
         http.Request.Form = new FormCollection(form.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
-        return new BatteriesController(BatteryTesting.Pages(rig), PageTesting.Documents(rig), StatusChangeTests.Pages(rig))
+        return new BatteriesController(BatteryTesting.Pages(rig), PageTesting.Documents(rig), StatusChangeTests.Pages(rig), PassportMarkTests.Service(rig))
         {
             ControllerContext = new ControllerContext { HttpContext = http },
             TempData = new TempDataDictionary(http, new NoTempDataStorage()),
```

`BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs`:

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

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>"Invalid" and "marked for deletion" on the pages (mock-up S5, S6 and S7; step 4c-2).</summary>
[Collection(MongoCollection.Name)]
public sealed class PassportMarkPagesTests(MongoFixture mongo)
{
    private const string Reason = "Demo: wrong carbon footprint figures";

    [Fact]
    public async Task TheTeamsPage_ShouldMarkInvalidAndUndoItFromThePassportStatusCard()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        Assert.True((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks.Active);
        var marked = Assert.IsType<RedirectResult>(await controller.MarkInvalid(rig.PackId, Reason, CancellationToken.None));
        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks;
        await controller.UndoMark(rig.PackId, CancellationToken.None);

        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#passport-status", marked.Url);
        Assert.True(card.Marked);
        Assert.Equal(DppStatuses.Invalid, card.Status);
        Assert.Equal(Reason, card.Mark!.Reason);
        Assert.Equal("Undone: the passport is active again.", controller.TempData[BatteriesController.CardStatusKey]);
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task MarkingANonWasteBatteryForDeletion_ShouldBeSaidOnTheCard()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        await controller.MarkForDeletion(rig.PackId, CancellationToken.None);

        Assert.Equal($"Nothing changed: {PassportMarks.OnlyWaste}.", controller.TempData[BatteriesController.CardErrorKey]);
        Assert.False((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks.Waste);
    }

    [Fact]
    public async Task ThePassportPages_ShouldSayInvalidAndShowTheReasonToTheTeamAndSurveillanceOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await PassportMarkTests.Service(rig).MarkInvalidAsync(ChangeSender.MarketSurveillance("msa@example.com"), rig.PackId, Reason);
        var id = PassportPagesTests.Identifier(rig);
        var pages = PassportPagesTests.Pages(rig);

        var summary = (await pages.SummaryAsync(Reader.Anonymous, id))!;
        var clusterUser = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), id))!;
        var surveillance = (await pages.DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), id))!;

        Assert.Equal("Invalid", summary.StatusWords);
        Assert.Equal("Invalid", summary.Notice!.Pill);
        Assert.StartsWith("This passport was marked invalid on ", summary.Notice.Text, StringComparison.Ordinal);
        Assert.Null(summary.MarkReason);
        Assert.Null(clusterUser.MarkReason);
        Assert.Equal($"{Reason} (marked by market surveillance, msa@example.com)", surveillance.MarkReason);
        Assert.False(surveillance.MayMarkInvalid); // already invalid
    }

    [Fact]
    public async Task MarketSurveillance_ShouldMarkInvalidFromTheDetailPageAndNobodyElse()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var id = PassportPagesTests.Identifier(rig);
        Assert.True((await PassportPagesTests.Pages(rig).DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), id))!.MayMarkInvalid);
        Assert.False((await PassportPagesTests.Pages(rig).DetailAsync(Reader.With("team", ReaderRole.Team), id))!.MayMarkInvalid); // the team uses its battery page

        var byUser = await Controller(rig, "user@example.com", "member").MarkInvalid(Uri.EscapeDataString(id), Reason, PassportMarkTests.Service(rig), CancellationToken.None);
        var surveillance = Controller(rig, "msa@example.com", "marketSurveillanceAuthority");
        var bySurveillance = await surveillance.MarkInvalid(Uri.EscapeDataString(id), Reason, PassportMarkTests.Service(rig), CancellationToken.None);

        Assert.IsType<NotFoundResult>(byUser);
        Assert.Equal(PassportPages.DetailHref(id), Assert.IsType<RedirectResult>(bySurveillance).Url);
        Assert.Equal("The passport is marked invalid. The team is the only one who can undo it.", surveillance.TempData[PassportController.MarkMessageKey]);
        Assert.Equal(DppStatuses.Invalid, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task TheRegistry_ShouldKeepAnInvalidBatteryWithItsStatusButNoManageLink()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, Reason);
        var batteries = await BatteryTesting.Pages(rig).ListAsync(null);

        var surveillance = RegistryList.Build(Reader.With("msa", ReaderRole.MarketSurveillance), batteries);
        var admin = RegistryList.Build(
            Reader.With("admin", ReaderRole.ClusterAdmin, BatteryTesting.North), batteries, ChangeSender.ClusterAdmin("admin", BatteryTesting.North));

        var row = Assert.Single(surveillance.Rows);
        Assert.Equal(DppStatuses.Invalid, row.StatusName);
        Assert.Null(admin.ManageHref(Assert.Single(admin.Rows))); // nothing on it changes, so there is nothing to manage
    }

    [Fact]
    public void ThePages_ShouldDrawTheCardTheButtonAndTheReason()
    {
        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));

        Assert.Contains("~/Views/Batteries/_PassportStatusCard.cshtml\", marks", battery, StringComparison.Ordinal);
        Assert.Contains("Model.MayMarkInvalid", hero, StringComparison.Ordinal);
        Assert.Contains("Model.MarkReason is { } reason", hero, StringComparison.Ordinal);
        Assert.Contains("DppStatuses.Words(shown.DppStatus)", registry, StringComparison.Ordinal);
    }

    private static PassportController Controller(PublicationRig rig, string email, string role)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
        };
        return new PassportController(new DppReaders(new ClusterRepository(rig.Context)), PassportPagesTests.Pages(rig))
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

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkPagesTests | FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~SuccessionPagesTests | FullyQualifiedName~RegistryListTests"`
Expected: the build fails with CS0117, CS1061, CS1729 (for example `error CS0117 'PassportController' does not contain a definition for 'MarkMessageKey'`).

- [ ] **Step 3: Write the code**

Patch `4c2-task2-code.patch` (`web/Controllers/BatteriesController.cs`, `web/Controllers/PassportController.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Dpp/Pages/RegistryList.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/Passport/Detail.cshtml`, `web/Views/Passport/_PassportHero.cshtml`, `web/Views/Registry/Index.cshtml`). Save the block as `../4c2-task2-code.patch`, outside the worktree, and run `git apply ../4c2-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/BatteriesController.cs b/web/Controllers/BatteriesController.cs
index 73676e1..0c41405 100644
--- a/web/Controllers/BatteriesController.cs
+++ b/web/Controllers/BatteriesController.cs
@@ -14,7 +14,7 @@ namespace BatteryPassWeb.Controllers;
 /// </summary>
 [Authorize(Policy = "AdminOnly")]
 [Route("admin/batteries")]
-public sealed class BatteriesController(BatteryPages pages, DocumentService documents, StatusPages status) : Controller
+public sealed class BatteriesController(BatteryPages pages, DocumentService documents, StatusPages status, PassportMarks marks) : Controller
 {
     /// <summary>Which card of the battery page a message belongs to, so it shows where the action was taken.</summary>
     public const string CardKey = "BatteryCard";
@@ -210,6 +210,30 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
         return Redirect($"{BatteryPages.BatteryHref(packId)}#status");
     }
 
+    /// <summary>The team marks the passport invalid, with a reason (design 7.7).</summary>
+    [HttpPost("{packId}/mark/invalid")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> MarkInvalid(string packId, [FromForm] string? reason, CancellationToken cancellationToken) =>
+        MarkResult(packId, await marks.MarkInvalidAsync(ChangeSender.Team(Who), packId, reason, cancellationToken), "The passport is marked invalid. Every page says so; only the team undoes it.");
+
+    /// <summary>The team marks a waste battery's passport for deletion (design 7.6).</summary>
+    [HttpPost("{packId}/mark/deletion")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> MarkForDeletion(string packId, CancellationToken cancellationToken) =>
+        MarkResult(packId, await marks.MarkForDeletionAsync(ChangeSender.Team(Who), packId, cancellationToken), "The passport is marked for deletion. It stays readable until it is deleted.");
+
+    /// <summary>The team undoes "invalid" or "marked for deletion" (J-8).</summary>
+    [HttpPost("{packId}/mark/undo")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> UndoMark(string packId, CancellationToken cancellationToken) =>
+        MarkResult(packId, await marks.UndoAsync(ChangeSender.Team(Who), packId, cancellationToken), "Undone: the passport is active again.");
+
+    private RedirectResult MarkResult(string packId, string? problem, string done)
+    {
+        Card("passport-status", problem is null ? done : null, problem is null ? null : $"Nothing changed: {problem}.");
+        return Redirect($"{BatteryPages.BatteryHref(packId)}#passport-status");
+    }
+
     [HttpPost("{packId}/remove")]
     [ValidateAntiForgeryToken]
     public async Task<IActionResult> Remove(string packId, CancellationToken cancellationToken)
diff --git a/web/Controllers/PassportController.cs b/web/Controllers/PassportController.cs
index cf5538c..8e3daa5 100644
--- a/web/Controllers/PassportController.cs
+++ b/web/Controllers/PassportController.cs
@@ -1,5 +1,6 @@
 using BatteryPassWeb.Dpp.Api;
 using BatteryPassWeb.Dpp.Access;
+using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Pages;
 using Microsoft.AspNetCore.Authorization;
 using Microsoft.AspNetCore.Http;
@@ -18,6 +19,28 @@ namespace BatteryPassWeb.Controllers;
 [Route("dpp")]
 public sealed class PassportController(DppReaders readers, PassportPages pages) : Controller
 {
+    /// <summary>Where the detail page says what happened to "Mark as invalid".</summary>
+    public const string MarkMessageKey = "PassportMarkMessage";
+
+    /// <summary>
+    /// Market surveillance marks a passport invalid from its detail page (design 7.7, mock-up S6, step 4c-2). Anyone else,
+    /// signed in or not, finds nothing here.
+    /// </summary>
+    [HttpPost("{id}/detail/invalid")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> MarkInvalid(string id, [FromForm] string? reason, [FromServices] PassportMarks marks, CancellationToken cancellationToken)
+    {
+        var reader = await readers.ForAsync(User, cancellationToken);
+        if (!reader.Roles.Contains(ReaderRole.MarketSurveillance) || await pages.FindAsync(Uri.UnescapeDataString(id), cancellationToken) is not { } version)
+        {
+            return NotFound();
+        }
+
+        var problem = await marks.MarkInvalidAsync(ChangeSender.MarketSurveillance(reader.Who), version.PackId, reason, cancellationToken);
+        TempData[MarkMessageKey] = problem is null ? "The passport is marked invalid. The team is the only one who can undo it." : $"The passport wasn't marked: {problem}.";
+        return Redirect(PassportPages.DetailHref(Uri.UnescapeDataString(id)));
+    }
+
     [HttpGet("{id}")]
     public async Task<IActionResult> Summary(string id, CancellationToken cancellationToken)
     {
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index d2a7918..42b3d2e 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -79,6 +79,15 @@ public sealed record PublicationView(
     public int MissingCount => Missing.Sum(group => group.Attributes.Count);
 }
 
+/// <summary>The team's "Passport status" card (S5): the passport's DPP status, its mark, whether the battery is waste.</summary>
+/// <param name="Status">The DPP status (#2) of the passport's newest version; null for a draft.</param>
+public sealed record PassportMarkView(string? Status, PassportMark? Mark, bool Waste, string Action)
+{
+    public bool Active => Status == DppStatuses.Active;
+
+    public bool Marked => Status is DppStatuses.Invalid or DppStatuses.MarkedForDeletion;
+}
+
 /// <summary>One published version of the passport and whether its signature still holds.</summary>
 public sealed record VersionRow(PassportVersion Version, bool SignatureHolds);
 
@@ -103,6 +112,13 @@ public sealed record BatteryPage(
     /// <summary>The battery status card (step 4b), shared with the cluster admin's page.</summary>
     public StatusCardView? Status { get; init; }
 
+    /// <summary>The team's "Passport status" card (mock-up S5, step 4c-2): invalid, marked for deletion, and their undo.</summary>
+    public PassportMarkView Marks => new(
+        Publication.Active is not null ? DppStatuses.Active : Publication.Latest?.DppStatus,
+        Pack.Mark,
+        StatusChangeService.CurrentStatus(Pack) == "waste",
+        $"{BatteryPages.BatteryHref(Pack.Id)}/mark");
+
     /// <summary>The cards this page shares with the cluster admin's battery page (step 4a).</summary>
     public BatteryCards Cards => new(
         Pack,
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index e7243f5..44cb28b 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -74,6 +74,12 @@ public sealed record PassportPage(
     /// <summary>The line about an inactive passport or one that replaces an earlier passport (S7); null otherwise.</summary>
     public PassportNotice? Notice { get; init; }
 
+    /// <summary>Why the passport is invalid and who said so, for the team and market surveillance only (decision 34); null otherwise.</summary>
+    public string? MarkReason { get; init; }
+
+    /// <summary>The reader may mark this passport invalid here: market surveillance on an active passport's detail page (S6).</summary>
+    public bool MayMarkInvalid { get; init; }
+
     /// <summary>This version's DPP status (#2) in words, for the hero's pill.</summary>
     public string StatusWords => DppStatuses.Words(Version.DppStatus);
 }
@@ -263,6 +269,11 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
             Modules = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Modules : null,
             Repairs = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Repairs.AsEnumerable().Reverse().ToArray() : null,
             Notice = await NoticeAsync(version, sources.Pack, detail, cancellationToken),
+            MarkReason = detail && sources.Pack.Mark is { Reason: { } reason } mark && version.DppStatus == mark.Status
+                && (who.Roles.Contains(ReaderRole.Team) || who.Roles.Contains(ReaderRole.MarketSurveillance))
+                ? $"{reason} (marked by {(mark.ByRole == "MarketSurveillance" ? "market surveillance" : "the team")}, {mark.By})"
+                : null,
+            MayMarkInvalid = detail && version.DppStatus == DppStatuses.Active && who.Roles.Contains(ReaderRole.MarketSurveillance),
         };
     }
 
@@ -273,6 +284,16 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
     private async Task<PassportNotice?> NoticeAsync(PassportVersion version, BatteryPack pack, bool detail, CancellationToken cancellationToken)
     {
         string Href(string identifier) => detail ? DetailHref(identifier) : SummaryHref(identifier);
+        if (version.DppStatus == DppStatuses.Invalid)
+        {
+            return new PassportNotice("Invalid", $"This passport was marked invalid on {ValueWords.Day(version.LastUpdatedUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))}. Its data can't be relied on.", null, null);
+        }
+
+        if (version.DppStatus == DppStatuses.MarkedForDeletion)
+        {
+            return new PassportNotice("Marked for deletion", "The battery was recycled or exported. This passport stays readable until it is deleted.", null, null);
+        }
+
         if (version.DppStatus == DppStatuses.Inactive && pack.Succession is { } succession)
         {
             if (succession.SuccessorPackId is { } successorId && await store.GetAsync<BatteryPack>(successorId, cancellationToken) is { } successor
diff --git a/web/Dpp/Pages/RegistryList.cs b/web/Dpp/Pages/RegistryList.cs
index a986ea1..b506a36 100644
--- a/web/Dpp/Pages/RegistryList.cs
+++ b/web/Dpp/Pages/RegistryList.cs
@@ -1,5 +1,6 @@
 using BatteryPassWeb.Dpp.Access;
 using BatteryPassWeb.Dpp.Editing;
+using BatteryPassWeb.Dpp.Store;
 
 namespace BatteryPassWeb.Dpp.Pages;
 
@@ -13,7 +14,7 @@ public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query,
     public string? ManageHref(BatteryRow row)
     {
         ArgumentNullException.ThrowIfNull(row);
-        return row.ClusterId is { } cluster && ManagedClusterIds.Contains(cluster) ? ClusterBatteryPages.BatteryHref(row.PackId) : null;
+        return row.Active is not null && row.ClusterId is { } cluster && ManagedClusterIds.Contains(cluster) ? ClusterBatteryPages.BatteryHref(row.PackId) : null;
     }
 }
 
@@ -34,8 +35,9 @@ public static class RegistryList
     {
         ArgumentNullException.ThrowIfNull(reader);
         ArgumentNullException.ThrowIfNull(row);
-        // Only an active passport is listed: an inactive battery continues on its new passport (step 4c).
-        return row.Active is not null
+        // An active, invalid or marked-for-deletion passport is listed (decision 34): market surveillance finds what it
+        // flagged, and the cluster still owns the battery. An inactive battery continues on its new passport (step 4c).
+        return (row.Active is not null || row.Latest?.DppStatus is DppStatuses.Invalid or DppStatuses.MarkedForDeletion)
             && (EveryPublishedBattery.Any(reader.Roles.Contains) || (row.ClusterId is { } cluster && reader.ClusterIds.Contains(cluster)));
     }
 
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 9aec6f4..2713c7d 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -294,6 +294,11 @@
     @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
     @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
 
+    @if (Model.Marks is { } marks && (marks.Active || marks.Marked))
+    {
+        @await Html.PartialAsync("~/Views/Batteries/_PassportStatusCard.cshtml", marks)
+    }
+
     @if (!publication.IsPublished)
     {
     <details class="bp-card bp-remove-record mt-4" id="remove" open="@(card == "remove")">
diff --git a/web/Views/Passport/Detail.cshtml b/web/Views/Passport/Detail.cshtml
index 6c1e77c..c4fe9a4 100644
--- a/web/Views/Passport/Detail.cshtml
+++ b/web/Views/Passport/Detail.cshtml
@@ -7,6 +7,10 @@
     ViewData["Detail"] = true;
 }
 <main class="bp-page">
+    @if (TempData[BatteryPassWeb.Controllers.PassportController.MarkMessageKey] is string marked)
+    {
+        <p class="bp-warning" role="status">@marked</p>
+    }
     <partial name="_PassportHero" model="Model" view-data="ViewData" />
     <partial name="_PassportTabs" model="Model" />
     @if (Model.Modules is { Count: > 0 } modules)
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index 0d7acce..48948b9 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -20,8 +20,25 @@
         {
             <a href="@notice.Href">@notice.LinkText</a>
         }
+        @if (Model.MarkReason is { } reason)
+        {
+            <span class="d-block mt-1">Reason, seen only by the manufacturer's team and market surveillance: @reason</span>
+        }
     </p>
 }
+@if (Model.MayMarkInvalid)
+{
+    <details class="bp-warning" data-mark-invalid>
+        <summary>Mark as invalid…</summary>
+        <form method="post" action="@PassportPages.DetailHref(Model.Address)/invalid" class="mt-2">
+            @Html.AntiForgeryToken()
+            <label class="d-block">Reason (seen only by the manufacturer's team and market surveillance)
+                <input type="text" name="reason" maxlength="500" class="form-control mt-1" required />
+            </label>
+            <button type="submit" class="btn btn-danger btn-sm mt-2">Mark this passport invalid</button>
+        </form>
+    </details>
+}
 <section class="bp-report-hero-card">
     <div class="bp-report-hero-layout">
         <article class="bp-report-hero-info bp-summary-identity-grid bp-report-identity-grid">
diff --git a/web/Views/Registry/Index.cshtml b/web/Views/Registry/Index.cshtml
index 7132d0b..6ddce11 100644
--- a/web/Views/Registry/Index.cshtml
+++ b/web/Views/Registry/Index.cshtml
@@ -59,7 +59,15 @@
                                 {
                                     <td data-label="Cluster">@row.ClusterLabel</td>
                                 }
-                                <td data-label="Passport">Version @row.Active!.Number <small class="d-block text-secondary">updated @ValueWords.Time(row.Active.LastUpdatedUtc)</small></td>
+                                @{ var shown = (row.Active ?? row.Latest)!; }
+                                <td data-label="Passport">
+                                    Version @shown.Number
+                                    @if (row.Active is null)
+                                    {
+                                        <span class="bp-status-pill is-inactive">@BatteryPassWeb.Dpp.Store.DppStatuses.Words(shown.DppStatus)</span>
+                                    }
+                                    <small class="d-block text-secondary">updated @ValueWords.Time(shown.LastUpdatedUtc)</small>
+                                </td>
                                 @if (Model.ManagedClusterIds.Count > 0)
                                 {
                                     <td data-label="">
```

`web/Views/Batteries/_PassportStatusCard.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@using BatteryPassWeb.Dpp.Store
@model PassportMarkView
@* The team's "Passport status" card (step 4c-2, mock-up S5): mark the passport invalid or for deletion, or undo it. *@
<details class="bp-card bp-danger-zone mt-4" id="passport-status" open="@(Model.Marked)">
    <summary><strong>Passport status</strong> <span class="text-secondary">· @DppStatuses.Words(Model.Status ?? "draft").ToLowerInvariant()</span></summary>
    @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "passport-status")
    @if (Model.Marked)
    {
        <p class="mt-2 mb-2">
            <strong>@DppStatuses.Words(Model.Status!) since @(Model.Mark is { } mark ? ValueWords.Time(mark.AtUtc) : "—").</strong>
            @if (Model.Mark is { } why)
            {
                <span>Marked by @(why.ByRole == "MarketSurveillance" ? "market surveillance" : "the team") (@why.By)@(why.Reason is null ? "." : $": {why.Reason}")</span>
            }
        </p>
        <form method="post" action="@Model.Action/undo">
            @Html.AntiForgeryToken()
            <div class="bp-danger-actions"><button type="submit" class="bp-secondary-button">Undo: make the passport active again</button></div>
            <small class="bp-field-help d-block">The reason stays in the audit log.</small>
        </form>
    }
    else
    {
        <p class="mt-2 mb-2"><strong>Mark as invalid</strong>, for example after a market surveillance finding. Every page then says so, and nothing on the passport changes any more. Only the team undoes it.</p>
        <form method="post" action="@Model.Action/invalid" class="bp-dialog-form">
            @Html.AntiForgeryToken()
            <label><span>Reason (seen only by your team and market surveillance)</span><input type="text" name="reason" maxlength="@BatteryPassWeb.Dpp.Editing.PassportMarks.MaxReason" placeholder="e.g. Declared values don't match the test report" required /></label>
            <div class="bp-danger-actions"><button type="submit" class="bp-danger-button">Mark as invalid</button></div>
        </form>
        <hr />
        <p class="mb-2"><strong>Mark for deletion</strong>, after recycling or export. Only for a battery whose status is waste; the passport stays readable until it is deleted. The team can undo it.</p>
        <form method="post" action="@Model.Action/deletion">
            @Html.AntiForgeryToken()
            <div class="bp-danger-actions">
                <button type="submit" class="bp-danger-button" disabled="@(!Model.Waste)">Mark for deletion</button>
                @if (!Model.Waste)
                {
                    <small class="bp-field-help">The battery status isn't waste.</small>
                }
            </div>
        </form>
    }
</details>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~PassportMarkPagesTests | FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~SuccessionPagesTests | FullyQualifiedName~RegistryListTests"`
Expected: `Passed!  - Failed:     0, Passed:    49`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2212`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs BatteryPassWeb.Tests/Dpp/PassportMarkPagesTests.cs web/Controllers/BatteriesController.cs web/Controllers/PassportController.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/PassportPages.cs web/Dpp/Pages/RegistryList.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_PassportStatusCard.cshtml web/Views/Passport/Detail.cshtml web/Views/Passport/_PassportHero.cshtml web/Views/Registry/Index.cshtml
git commit -m "feat(dpp): the passport status card, market surveillance's mark and the notices" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: An invalid and a marked-for-deletion battery in the demo data

The demo seed adds DEMO-C7M-0003, published and then marked invalid by the demo market-surveillance account with its reason, and marks DEMO-CORE-0003 (waste) for deletion. The demo tests count 9 batteries, 6 published, 10 versions.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`
- Modify: `web/Dpp/Demo/DemoCatalogue.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Dpp/Demo/demo-catalogue.json`

- [ ] **Step 1: Write the failing tests**

Patch `4c2-task3-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoCardTests.cs`, `BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`, `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`, `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`). Save the block as `../4c2-task3-tests.patch`, outside the worktree, and run `git apply ../4c2-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
index 7ed1c0c..5680709 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
@@ -51,7 +51,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
         var result = await controller.ResetDemoData(CancellationToken.None);
 
         Assert.Equal("/admin/clusters?tab=clusters", Assert.IsType<RedirectResult>(result).Url);
-        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 8 batteries, 5 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
+        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 9 batteries, 6 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
         var tokens = JsonSerializer.Deserialize<List<DemoToken>>((string)controller.TempData["GeneratedCredentials"]!)!;
         Assert.Equal(7, tokens.Count);
         Assert.All(tokens, token => Assert.False(string.IsNullOrWhiteSpace(token.Value)));
@@ -66,7 +66,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
 
         await controller.ClearDemoData(CancellationToken.None);
 
-        Assert.StartsWith("Demo data cleared: 8 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
+        Assert.StartsWith("Demo data cleared: 9 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
         Assert.Null(controller.TempData["GeneratedCredentials"]);
         Assert.Equal(0, await rig.Count(DppStore.Packs));
     }
diff --git a/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs b/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
index 92f98db..12d8463 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
@@ -67,7 +67,7 @@ public sealed class DemoCatalogueTests
     [Fact]
     public void TheBatteries_ShouldBeInSeveralStates()
     {
-        Assert.Equal(["DEMO-C7M-0001", "DEMO-C13M-0001", "DEMO-CORE-0001", "DEMO-CORE-0003"], Catalogue.Packs.Where(pack => pack.Publish).Select(pack => pack.Serial));
+        Assert.Equal(["DEMO-C7M-0001", "DEMO-C7M-0003", "DEMO-C13M-0001", "DEMO-CORE-0001", "DEMO-CORE-0003"], Catalogue.Packs.Where(pack => pack.Publish).Select(pack => pack.Serial));
         Assert.Equal(["DEMO-C7M-0001 Repaired", "DEMO-C13M-0001 Repurposed", "DEMO-CORE-0003 Waste"], Catalogue.Packs.Where(pack => pack.Status is not null).Select(pack => $"{pack.Serial} {pack.Status!.What}"));
         Assert.Equal(["DEMO-C7M-0001"], Catalogue.Packs.Where(pack => pack.NewVersion is not null).Select(pack => pack.Serial));
         Assert.Equal(2, Catalogue.Packs.Count(pack => pack.Accident is not null));
diff --git a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
index 51f8b65..4db4329 100644
--- a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
@@ -29,7 +29,7 @@ public sealed class DemoClearTests(MongoFixture mongo)
         }
 
         Assert.Empty(result.Notes);
-        Assert.Contains("8 batteries removed", result.Done);
+        Assert.Contains("9 batteries removed", result.Done);
         Assert.Contains("12 accounts removed", result.Done);
     }
 
@@ -44,7 +44,7 @@ public sealed class DemoClearTests(MongoFixture mongo)
 
         Assert.Equal(before + 1, await rig.Count("applicationAuditEvents"));
         var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.ClearedEvent))).Single();
-        Assert.Equal(8, entry["metadata"]["batteries"].ToInt32());
+        Assert.Equal(9, entry["metadata"]["batteries"].ToInt32());
     }
 
     [Fact]
@@ -97,12 +97,12 @@ public sealed class DemoClearTests(MongoFixture mongo)
         var result = await rig.Demo.ResetAsync(DemoRig.Who);
 
         var packs = (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).ToList();
-        Assert.Equal(8, packs.Count);
+        Assert.Equal(9, packs.Count);
         Assert.Empty(packs.Intersect(firstPacks));
-        Assert.Equal(9, await rig.Count(DppStore.Versions));
+        Assert.Equal(10, await rig.Count(DppStore.Versions));
         Assert.Equal(7, result.Tokens.Count);
         Assert.Empty(result.Tokens.Select(token => token.Value).Intersect(first.Tokens.Select(token => token.Value)));
-        Assert.Contains("8 batteries removed", result.Done);
-        Assert.Contains("8 batteries, 5 of them published", result.Done);
+        Assert.Contains("9 batteries removed", result.Done);
+        Assert.Contains("9 batteries, 6 of them published", result.Done);
     }
 }
diff --git a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
index ecb2bc6..b034d19 100644
--- a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
@@ -30,8 +30,8 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.Equal(2, await rig.Count(DppStore.Sites));
         Assert.Equal(2, await rig.Count(DppStore.Operators));
         Assert.Equal(3, await rig.Count(DppStore.Batches));
-        Assert.Equal(8, await rig.Count(DppStore.Packs));
-        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "8 batteries, 5 of them published", "12 accounts and 7 API tokens"], result.Done);
+        Assert.Equal(9, await rig.Count(DppStore.Packs));
+        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "9 batteries, 6 of them published", "12 accounts and 7 API tokens"], result.Done);
     }
 
     [Fact]
@@ -42,8 +42,10 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         await rig.Demo.SeedAsync(DemoRig.Who);
 
         var versions = await rig.All(DppStore.Versions);
-        Assert.Equal(9, versions.Count); // C7M-0001: 3 (a new version, then its repair); C13M-0001: 2 (repurposed) and its new passport 1; CORE-0003: 2 (waste); CORE-0001: 1
-        Assert.Equal(4, versions.Count(version => version["DppStatus"].AsString == "active")); // C13M-0001's old passport is inactive
+        Assert.Equal(10, versions.Count); // C7M-0001: 3 (a new version, then its repair); C13M-0001: 2 (repurposed) and its new passport 1; CORE-0003: 2 (waste); CORE-0001 and C7M-0003: 1
+        Assert.Equal(3, versions.Count(version => version["DppStatus"].AsString == "active")); // C13M-0001's old passport is inactive, C7M-0003's invalid, CORE-0003's marked for deletion
+        Assert.Equal(1, versions.Count(version => version["DppStatus"].AsString == "invalid"));
+        Assert.Equal(1, versions.Count(version => version["DppStatus"].AsString == "marked-for-deletion"));
         Assert.Equal(1, versions.Count(version => version["DppStatus"].AsString == "inactive"));
         Assert.All(versions, version => Assert.True(version.Contains("Proof")));
         var firstPack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single()["_id"].AsString;
@@ -52,7 +54,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
     }
 
     [Fact]
-    public async Task TheStatusExamples_ShouldBeARepairedARepurposedAndAWasteBattery()
+    public async Task TheStatusExamples_ShouldShowEveryStatusAndMark()
     {
         using var rig = await DemoRig.StartAsync(mongo);
 
@@ -72,6 +74,12 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.Equal("repurposed", newOne["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
         Assert.Equal("P3Y", newOne["Values"]["warrantyPeriod"]["Value"].AsString);
         Assert.True(newOne["IsDemo"].AsBoolean);
+        var invalid = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0003"))).Single();
+        Assert.Equal("invalid", invalid["Mark"]["Status"].AsString);
+        Assert.Equal("Demo: wrong carbon footprint figures", invalid["Mark"]["Reason"].AsString);
+        Assert.Equal("demo.msa@example.test", invalid["Mark"]["By"].AsString);
+        Assert.Equal("MarketSurveillance", invalid["Mark"]["ByRole"].AsString);
+        Assert.Equal("marked-for-deletion", waste["Mark"]["Status"].AsString);
     }
 
     [Fact]
@@ -95,7 +103,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         await rig.Demo.SeedAsync(DemoRig.Who);
 
         var packs = await rig.All(DppStore.Packs);
-        Assert.Equal([0, 0, 0, 7, 10, 10, 13, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
+        Assert.Equal([0, 0, 0, 7, 7, 10, 10, 13, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
         Assert.Equal(2, await rig.Count(DocumentStore.Records, Eq("Property", "informationOnAccidents")));
         Assert.Equal(12, await rig.Count(DocumentStore.Records, Eq("OwnerKind", "Model")));
         Assert.Equal(3, await rig.Count(DocumentStore.Records, Eq("Property", "resultsOfTestReportsProvingCompliance")));
@@ -156,7 +164,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 
         Assert.Equal([DemoDataService.NoPassword], result.Notes);
         Assert.Equal(0, await rig.Count("users"));
-        Assert.Equal(8, await rig.Count(DppStore.Packs));
+        Assert.Equal(9, await rig.Count(DppStore.Packs));
     }
 
     [Fact]
@@ -205,7 +213,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         var again = await rig.Demo.SeedAsync(DemoRig.Who);
 
         Assert.Equal(["The demo data is already there: reset it or clear it first."], again.Notes);
-        Assert.Equal(8, await rig.Count(DppStore.Packs));
+        Assert.Equal(9, await rig.Count(DppStore.Packs));
     }
 
     [Fact]
@@ -228,6 +236,6 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 
         var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.SeededEvent))).Single();
         Assert.Equal(DemoRig.Who, entry["actor"].AsString);
-        Assert.Equal((8, 5, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
+        Assert.Equal((9, 6, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
     }
 }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo"`
Expected: `Failed:    13, Passed:    27` (failing: `DemoCardTests.ClearingFromTheCard_ShouldClearAndSayWhatWasRemoved`, `DemoCardTests.ResettingFromTheCard_ShouldSeedAndShowEveryNewTokenOnce`, `DemoCatalogueTests.TheBatteries_ShouldBeInSeveralStates`, `DemoClearTests.Clearing_ShouldKeepTheAuditLogAndLogItself`, `DemoClearTests.Clearing_ShouldRemoveEveryDemoRecordWithItsVersionsDocumentsAndFiles`, `DemoClearTests.Resetting_ShouldClearThenSeedWithNewBatteries`, `DemoSeedTests.SeedingAgain_ShouldAskToResetOrClearFirst`, `DemoSeedTests.TheBatteries_ShouldHaveModulesAccidentReportsAndDocuments`, `DemoSeedTests.ThePublishedBatteries_ShouldHaveSignedVersionsAndTheSecondOneAVersionTwo`, `DemoSeedTests.TheSeed_ShouldBeLoggedOnceWithTheCountsButNoTokenValues`, `DemoSeedTests.TheSeed_ShouldMakeTheFamiliesModelsAndBatteries`, `DemoSeedTests.TheStatusExamples_ShouldShowEveryStatusAndMark`, `DemoSeedTests.WithoutTheSetting_ShouldSeedEverythingButTheAccounts`).

- [ ] **Step 3: Write the code**

Patch `4c2-task3-code.patch` (`web/Dpp/Demo/DemoCatalogue.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/Demo/demo-catalogue.json`). Save the block as `../4c2-task3-code.patch`, outside the worktree, and run `git apply ../4c2-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Demo/DemoCatalogue.cs b/web/Dpp/Demo/DemoCatalogue.cs
index 549deb4..d6aad69 100644
--- a/web/Dpp/Demo/DemoCatalogue.cs
+++ b/web/Dpp/Demo/DemoCatalogue.cs
@@ -26,6 +26,12 @@ public sealed record DemoStatus(string What, JsonObject? Restated = null, int Da
 /// <summary>Scania's new passport of a demo battery placed on the market again (step 4c-1): made, given a warranty, published.</summary>
 public sealed record DemoSuccessor(string Warranty);
 
+/// <summary>
+/// A demo battery's passport marked invalid, by the demo market surveillance account with this reason, or marked for
+/// deletion by the team (step 4c-2).
+/// </summary>
+public sealed record DemoMark(string? Invalid = null, bool Deletion = false);
+
 /// <summary>
 /// A demo battery. <see cref="Publish"/> publishes it; <see cref="NewVersion"/> then changes these values, which makes
 /// version 2; <see cref="History"/> writes seven days of live values; <see cref="Modules"/> imports that many modules
@@ -43,7 +49,8 @@ public sealed record DemoPack(
     bool History = false,
     int Modules = 0,
     string? Accident = null,
-    DemoStatus? Status = null);
+    DemoStatus? Status = null,
+    DemoMark? Mark = null);
 
 /// <summary>
 /// What the demo data seed makes (decision 32): the Compact and Core families of the old demo on the new model, with
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index 42b675e..fa5c54f 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -35,6 +35,7 @@ public sealed class DemoDataService(
     PublicationService publication,
     StatusChangeService statusChanges,
     SuccessionService successions,
+    PassportMarks marks,
     DocumentService documents,
     DocumentStore documentStore,
     DppLinks links,
@@ -207,6 +208,11 @@ public sealed class DemoDataService(
                     published++;
                 }
             }
+
+            if (demo.Mark is { } mark && demo.Publish)
+            {
+                await MarkAsync(team, packId, demo, mark, catalogue, cancellationToken);
+            }
         }
 
         demoTokens.AddRange(await SeedTokensAsync(who, catalogue, cancellationToken));
@@ -517,6 +523,29 @@ public sealed class DemoDataService(
         }
     }
 
+    /// <summary>
+    /// A demo battery's passport marked invalid by the demo market surveillance account, as it would mark it on the
+    /// detail page, or marked for deletion by the team (step 4c-2).
+    /// </summary>
+    private async Task MarkAsync(ChangeSender team, string packId, DemoPack demo, DemoMark mark, DemoCatalogue catalogue, CancellationToken cancellationToken)
+    {
+        string? problem = null;
+        if (mark.Invalid is { } reason)
+        {
+            var surveillance = catalogue.Accounts.FirstOrDefault(account => account.Role == AccessControlService.RoleMarketSurveillanceAuthority)?.Email ?? team.Who;
+            problem = await marks.MarkInvalidAsync(ChangeSender.MarketSurveillance(surveillance), packId, reason, cancellationToken);
+        }
+        else if (mark.Deletion)
+        {
+            problem = await marks.MarkForDeletionAsync(team, packId, cancellationToken);
+        }
+
+        if (problem is not null)
+        {
+            throw new InvalidOperationException($"The demo battery {demo.Serial} couldn't be marked: {problem}.");
+        }
+    }
+
     /// <summary>An export-shaped file (decision 31) with this many modules for the battery.</summary>
     private static byte[] ModuleExport(string serial, int modules)
     {
diff --git a/web/Dpp/Demo/demo-catalogue.json b/web/Dpp/Demo/demo-catalogue.json
index c8855f8..4ab833a 100644
--- a/web/Dpp/Demo/demo-catalogue.json
+++ b/web/Dpp/Demo/demo-catalogue.json
@@ -902,6 +902,32 @@
         }
       }
     },
+    {
+      "serial": "DEMO-C7M-0003",
+      "batch": "compact-7m",
+      "operator": "batteries",
+      "cluster": "demo-north",
+      "values": {
+        "batterySerialNumber": "DEMO-C7M-0003",
+        "thirdPartyServiceProvider": "Demo Passport Hosting AB",
+        "manufacturingDate": "2026-07",
+        "warrantyPeriod": "P8Y",
+        "stateOfChargeSoC": {
+          "percentUnit": "%",
+          "percentageValue": 81
+        },
+        "numberOfFullChargingAndDischargingCycles": 96,
+        "temperatureInformation": {
+          "degreeCelsiusUnit": "°C",
+          "celsiusValue": 21
+        }
+      },
+      "publish": true,
+      "modules": 7,
+      "mark": {
+        "invalid": "Demo: wrong carbon footprint figures"
+      }
+    },
     {
       "serial": "DEMO-C7M-0002",
       "batch": "compact-7m",
@@ -1050,6 +1076,9 @@
           "powerFade": 31,
           "expectedLifetimeInCalendarYears": 0
         }
+      },
+      "mark": {
+        "deletion": true
       }
     }
   ]
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo"`
Expected: `Passed!  - Failed:     0, Passed:    40`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2212`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCardTests.cs BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs BatteryPassWeb.Tests/Demo/DemoClearTests.cs BatteryPassWeb.Tests/Demo/DemoSeedTests.cs web/Dpp/Demo/DemoCatalogue.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/Demo/demo-catalogue.json
git commit -m "feat(dpp): an invalid and a marked-for-deletion battery in the demo data" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2212, Skipped:     0, Total:  2212`. From the 2199 before: 2206 after Task 1, 2212 after Task 2, 2212 after Task 3.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 8d8a0db -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. The managing session brings them to the product owner:

1. **Restart the app with the usual launcher**, sign in with a team account and press "Reset demo data" at the end of the Clusters tab.
2. **As the team:**
   - **DEMO-C7M-0003:** "Invalid · version 1", and the "Passport status" card says who marked it and why, with "Undo".
   - **Its public summary:** "Invalid", the date, and no reason.
   - **DEMO-CORE-0003:** "Marked for deletion".
   - **DEMO-CORE-0001:** "Passport status", "Mark as invalid" with a reason. The public summary says invalid. Then "Undo" on the battery page.
3. **As the demo market-surveillance account** (`demo.msa@example.test`):
   - **The Registry** lists DEMO-C7M-0003 with "Invalid".
   - **Its detail page** shows the reason.
   - **On another battery's detail page,** "Mark as invalid…" with a reason marks it.
4. **As a demo cluster user** (`demo.north.user@example.test`): DEMO-C7M-0003 is in the Registry with "Invalid", and its detail page shows no reason.
5. **Light and dark:** the moon button in the top bar switches.

## Next

Plan 4d (hand-overs between clusters) ends step 4.
