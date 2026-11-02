# DPP V3 Step 4d: Hand-overs Between Clusters — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A battery moves from one customer's cluster to another's only when the receiver accepts (design 7.8; decisions 9, 14 and 34). This ends step 4.
- **Asking:** the battery's cluster admin types the receiving cluster's ID on the battery's page, sees the cluster's name, and asks (mock-up S2 and S8).
- **Waiting:** the battery stays with the sender, who keeps full access and can cancel. The receiver sees nothing of the battery yet.
- **Answering:** the receiving cluster's admins see a "Waiting for you" card at the top of the Registry, with Accept and Decline (S1). On Accept the battery moves; the sending cluster then no longer sees its purchaser data. The passport doesn't change.
- **The team:** keeps its direct move on its battery page (logged), which cancels a waiting hand-over, and can cancel one there.
- **Demo data:** DEMO-CORE-0001 handed over from demo-fleet to demo-north, waiting for demo-north's answer.

**Architecture:**
- **A new record, `HandOver`, in a new collection `dpp_transfers`:** battery, from and to clusters, state (waiting, accepted, declined, cancelled), who asked and when, who answered and when. It is app data, not passport data. Two indexes: by battery and state, and by receiving cluster and state.
- **One small service, `HandOverService`:**
  - `AskAsync` names the cluster (without `confirm`) or asks (with it). Only the battery's cluster admin asks, only for an active passport, only once at a time, and never to its own cluster. The team is told to use its direct move.
  - `AnswerAsync` accepts or declines, for the receiving cluster's admins only. An accept is refused once the passport is no longer active, saying why.
  - `CancelAsync` is for the sender's cluster admins or the team.
  - `WaitingForAsync` lists what waits for a cluster admin, for the Registry.
  - Ending a hand-over is one conditional update (`SetHandOverStateAsync`, "only while waiting"), so two answers at once can't both count. Every step is logged.
- **The move itself** is the existing `SetPackClusterAsync`: the cluster isn't passport data, so no version is made.
- **Pages:**
  - The cluster admin's battery page gets the "Hand over" card and window (`_HandOverCard.cshtml`). "Find the cluster" posts without confirming and comes back with the window open and the name shown. "Ask them to accept" then asks for the ID whose name was shown; a changed ID is named again first.
  - The Registry gets the "Waiting for you" card, answered by `POST /registry/handovers/{id}`.
  - The team's battery page shows a waiting hand-over beside the cluster, with "Cancel it".
  - The window reopens through one line added to `bp-record-editor.js` (`data-open-on-load`, as on the admin pages). The rows' style is the mock-up's `.bp-handover-row`, in `bp-product-data.css`. site.css is not changed.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 4.1, 7.8 and 12, and section 14's step-4 note. Decisions record: decisions 9, 14 and 34. Mock-up (approved 31 Oct 2026): `docs/mockups/dpp-v3-step4-purchaser-and-lifecycle.html`, screens S1, S2 and S8.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at e25d335 (the end of plan 4c-2's run), plus the commit that adds this plan. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo). The new collection `dpp_transfers` is made on Atlas only when the app first writes to it; the reset tool knows it as new test data.
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. Whole-suite runs also take `--blame-hang --blame-hang-timeout 10m`.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop it (and any `dotnet` and `testhost` it left) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2212`. After this plan: 2233. The suite takes about two to four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../4d-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New and rewritten files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (31 Oct 2026, through the managing session)

Decision 34 already holds these answers from step 4's first questions; this plan's commit adds the line found while prototyping (below).

1. **Q11: while it waits, the battery stays with the sender**, who keeps full access. The receiver sees nothing of it until it accepts.
2. **Q12: the sender types the receiving cluster's ID**, which the receiver gives them, and the app shows the cluster's name to confirm. There is no list of other customers' clusters.
3. **Q13: the receiver answers on a "Waiting for you" card at the top of its Registry**, with Accept and Decline. No e-mail is sent.
4. **Q14: the team keeps its direct move**, logged, and can cancel a waiting hand-over.
5. **Q16: one waiting hand-over in the demo data:** DEMO-CORE-0001 from demo-fleet to demo-north.

## Found while prototyping, and what this plan does

- **A passport can stop being active while its hand-over waits** (a successor's passport, an invalid mark). Its new cluster couldn't manage it then. **Accepting is refused**, and the receiver is told why in plain words (the managing session, 2 Nov 2026): "Not accepted: the battery's passport is no longer active (it was replaced by a new passport or marked invalid), so it can't move to your cluster. Decline it, or ask the sending cluster to cancel." The receiver can still decline and the sender or the team can still cancel. Nothing cancels it by itself.
- **The window's second step is checked again on the server.** "Ask them to accept" sends the typed ID and the ID whose name was shown; when they differ, the new ID is named first instead of asked for.
- **The window's typed ID and found name are read once.** They are kept across the redirect after "Find the cluster" and cleared when the page shows them, so the window doesn't reopen on a later visit (seen in the browser checks).
- **Clearing the demo data** also removes its batteries' hand-overs, like their versions.

## What happens to the old tests

Nothing is removed.
- **`ResetCollectionsTests`** knows the new collection `dpp_transfers` (Task 1).
- **`DemoClearTests`** checks that clearing leaves no hand-over (Task 3). The demo counts don't change: 9 batteries, 6 published, 10 versions.

## A part to write yourself (optional)

The product owner chose "you write it" before; the plan gives the code. If they want to try one, the natural spot is `HandOverService.AnswerAsync` in Task 1: who may answer a hand-over, and when an accept is refused. `Accepting_ShouldMoveTheBatteryWithoutANewVersion` and `AcceptingAPassportNoLongerActive_ShouldBeRefused` list it.

---

### Task 1: Hand-overs between clusters: asked, answered, cancelled

The rules before the pages: a battery's cluster admin names the receiving cluster by its ID and sees its name, then asks; the hand-over waits in a new collection, dpp_transfers, while the battery stays with the sender (Q11). Only the receiving cluster's admins accept (the battery moves, with no new version) or decline; the sender or the team cancels, and the team's direct move cancels a waiting one (Q14). An accept is refused once the passport is no longer active, saying why in plain words. Every step is logged; the reset tool and the demo clearing know the new collection.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/HandOverTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs`
- Modify: `tools/V3Reset/ResetCollections.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Create: `web/Dpp/Editing/HandOverService.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Store/DppStore.cs`

- [ ] **Step 1: Write the failing tests**

Patch `4d-task1-tests.patch` (`BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs`). Save the block as `../4d-task1-tests.patch`, outside the worktree, and run `git apply ../4d-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs b/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
index d2fae7d..e05ff1b 100644
--- a/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
+++ b/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
@@ -16,7 +16,7 @@ public sealed class ResetCollectionsTests
         var named = Directory.GetFiles(web, "*.cs", SearchOption.AllDirectories)
             .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
             .SelectMany(file => Regex.Matches(File.ReadAllText(file), "GetCollection<[^>]*>\\(\"([^\"]+)\"\\)").Select(match => match.Groups[1].Value));
-        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };
+        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };
 
         foreach (var collection in named.Concat(dpp).Distinct())
         {
@@ -27,7 +27,7 @@ public sealed class ResetCollectionsTests
     [Fact]
     public void TheNewModelsCollections_ShouldBeEmptiedAsTestData()
     {
-        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
+        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
         {
             Assert.Equal(CollectionFate.NewTestData, ResetCollections.FateOf(collection));
         }
```

`BatteryPassWeb.Tests/Dpp/HandOverTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Hand-overs between clusters (design 7.8, decisions 9, 14 and 34; step 4d): a battery's cluster admin asks, naming the
/// receiving cluster by its ID; the receiver's cluster admins accept or decline; the sender or the team can cancel while
/// it waits; on acceptance the battery moves, with no new passport version.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class HandOverTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    private static readonly ChangeSender SouthAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

    [Fact]
    public async Task AskingWithoutConfirming_ShouldOnlyNameTheReceivingCluster()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var check = await Service(rig).AskAsync(NorthAdmin, rig.PackId, $" {BatteryTesting.South.ToUpperInvariant()} ", confirm: false);

        Assert.Null(check.Problem);
        Assert.Equal("Customer South", check.ReceiverName);
        Assert.Null(check.HandOver);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
    }

    [Fact]
    public async Task AConfirmedHandOver_ShouldWaitWhileTheBatteryStaysWithTheSender()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var asked = await Service(rig).AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Null(asked.Problem);
        var waiting = (await rig.Store.FindWaitingHandOverAsync(rig.PackId))!;
        Assert.Equal(asked.HandOver!.Id, waiting.Id);
        Assert.Equal((BatteryTesting.North, BatteryTesting.South, HandOverStates.Waiting, "north-admin@example.com"), (waiting.FromClusterId, waiting.ToClusterId, waiting.State, waiting.AskedBy));
        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId); // Q11: the sender keeps it
        Assert.Equal([waiting.Id], (await Service(rig).WaitingForAsync(SouthAdmin)).Select(item => item.HandOver.Id));
        Assert.Empty(await Service(rig).WaitingForAsync(NorthAdmin));
        Assert.Equal("north-admin@example.com", (await LastAsync(rig, HandOverService.AskedEvent))["actor"].AsString);
    }

    [Theory]
    [InlineData("cluster-unknown", HandOverService.NoSuchCluster)]
    [InlineData("cluster-test", HandOverService.SameCluster)]
    [InlineData("", HandOverService.NoSuchCluster)]
    public async Task AWrongReceiver_ShouldBeRefused(string to, string problem)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Equal(problem, (await Service(rig).AskAsync(NorthAdmin, rig.PackId, to, confirm: true)).Problem);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
    }

    [Fact]
    public async Task OnlyTheBatterysClusterAdmin_ShouldAskAndOnlyOnce()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        Assert.Equal(HandOverService.NotYours, (await service.AskAsync(SouthAdmin, rig.PackId, BatteryTesting.North, confirm: true)).Problem);
        Assert.Equal(HandOverService.TeamMovesDirectly, (await service.AskAsync(PublicationTesting.Team, rig.PackId, BatteryTesting.South, confirm: true)).Problem);
        Assert.Equal(HandOverService.OnlyActive, (await Service(draft).AskAsync(NorthAdmin, draft.PackId, BatteryTesting.South, confirm: true)).Problem);
        await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        Assert.Equal(HandOverService.AlreadyWaiting, (await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true)).Problem);
    }

    [Fact]
    public async Task Accepting_ShouldMoveTheBatteryWithoutANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        var asked = await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Equal(HandOverService.NotTheReceiver, await service.AnswerAsync(NorthAdmin, asked.HandOver!.Id, accept: true));
        var accepted = await service.AnswerAsync(SouthAdmin, asked.HandOver.Id, accept: true);

        Assert.Null(accepted);
        Assert.Equal(BatteryTesting.South, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId)); // the cluster isn't passport data
        var done = (await rig.Store.GetAsync<HandOver>(asked.HandOver.Id))!;
        Assert.Equal((HandOverStates.Accepted, "south-admin@example.com"), (done.State, done.AnsweredBy));
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
        // decision 14: the previous cluster no longer sees the purchaser data
        Assert.False(AudienceRules.MaySeePurchaserData(Reader.With("north-user", ReaderRole.ClusterUser, BatteryTesting.North), [BatteryTesting.South]));
        Assert.Equal(HandOverService.NotWaiting, await service.AnswerAsync(SouthAdmin, asked.HandOver.Id, accept: true));
    }

    [Fact]
    public async Task AcceptingAPassportNoLongerActive_ShouldBeRefused()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        var asked = await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        await new PassportMarks(rig.Store, new ApplicationAuditService(rig.Context), rig.Clock).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures");

        Assert.Equal(HandOverService.NoLongerActive, await service.AnswerAsync(SouthAdmin, asked.HandOver!.Id, accept: true));

        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Null(await service.AnswerAsync(SouthAdmin, asked.HandOver.Id, accept: false)); // it can still be declined
    }

    [Fact]
    public async Task Declining_ShouldLeaveTheBatteryWithTheSender()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        var asked = await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Null(await service.AnswerAsync(SouthAdmin, asked.HandOver!.Id, accept: false));

        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Equal(HandOverStates.Declined, (await rig.Store.GetAsync<HandOver>(asked.HandOver.Id))!.State);
        Assert.Null((await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true)).Problem); // it may ask again
    }

    [Fact]
    public async Task TheSenderOrTheTeam_ShouldCancelAWaitingHandOver()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Equal(HandOverService.NotYours, await service.CancelAsync(SouthAdmin, rig.PackId));
        Assert.Null(await service.CancelAsync(NorthAdmin, rig.PackId));
        Assert.Equal(HandOverService.NothingWaiting, await service.CancelAsync(NorthAdmin, rig.PackId));
        await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        Assert.Null(await service.CancelAsync(PublicationTesting.Team, rig.PackId));
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
    }

    [Fact]
    public async Task TheTeamsDirectMove_ShouldCancelAWaitingHandOver()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var asked = await Service(rig).AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Null(await BatteryTesting.Pages(rig).SetClusterAsync("team@example.com", rig.PackId, BatteryTesting.South));

        var cancelled = (await rig.Store.GetAsync<HandOver>(asked.HandOver!.Id))!;
        Assert.Equal((HandOverStates.Cancelled, "team@example.com"), (cancelled.State, cancelled.AnsweredBy));
    }

    internal static HandOverService Service(PublicationRig rig) =>
        new(rig.Store, new ClusterRepository(rig.Context), new ApplicationAuditService(rig.Context), rig.Clock);

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HandOverTests | FullyQualifiedName~ResetCollectionsTests"`
Expected: the build fails with CS0103, CS0246 (for example `error CS0103 The name 'HandOverService' does not exist in the current context`).

- [ ] **Step 3: Write the code**

Patch `4d-task1-code.patch` (`tools/V3Reset/ResetCollections.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Store/DppStore.cs`). Save the block as `../4d-task1-code.patch`, outside the worktree, and run `git apply ../4d-task1-code.patch` from the worktree root:

```diff
diff --git a/tools/V3Reset/ResetCollections.cs b/tools/V3Reset/ResetCollections.cs
index 1998c07..88f36e3 100644
--- a/tools/V3Reset/ResetCollections.cs
+++ b/tools/V3Reset/ResetCollections.cs
@@ -40,6 +40,7 @@ public static class ResetCollections
         ["dpp_operators"] = CollectionFate.NewTestData,
         ["dpp_packs"] = CollectionFate.NewTestData,
         ["dpp_passport_versions"] = CollectionFate.NewTestData,
+        ["dpp_transfers"] = CollectionFate.NewTestData,
         ["dpp_documents"] = CollectionFate.NewTestData,
         ["dpp_files.files"] = CollectionFate.NewTestData,
         ["dpp_files.chunks"] = CollectionFate.NewTestData,
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index fa5c54f..c01bf79 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -254,6 +254,7 @@ public sealed class DemoDataService(
         foreach (var packId in await DemoIdsAsync(DppStore.Packs, cancellationToken))
         {
             await Collection(DppStore.Versions).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("PackId", packId), cancellationToken);
+            await Collection(DppStore.HandOvers).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("PackId", packId), cancellationToken);
             await documentStore.DeleteOwnedAsync(TargetKind.Pack, packId, cancellationToken);
             await store.DeleteAsync<BatteryPack>(packId, cancellationToken);
             Count("batteries");
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index 75c3bce..b9fac9d 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -35,6 +35,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<StatusChangeService>();
         services.AddSingleton<SuccessionService>();
         services.AddSingleton<PassportMarks>();
+        services.AddSingleton<HandOverService>();
         services.AddSingleton<PassportSigner>();
         services.AddSingleton<PublicationChecks>();
         services.AddSingleton<PublicationService>();
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 42b3d2e..e832e84 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -417,6 +417,12 @@ public sealed class BatteryPages(
         }
 
         await store.SetPackClusterAsync(packId, wanted, cancellationToken);
+
+        // The team's direct move (design 7.8) ends a hand-over that was waiting for this battery (step 4d).
+        if (await store.FindWaitingHandOverAsync(packId, cancellationToken) is { } waiting)
+        {
+            await store.SetHandOverStateAsync(waiting.Id, HandOverStates.Cancelled, who, DateTime.UtcNow, cancellationToken);
+        }
         var serial = Text(pack, "batterySerialNumber") ?? packId;
         await audit.AppendApplicationAuditEventAsync(
             ClusterChangedEvent,
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index b524a78..88f9dea 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -182,6 +182,45 @@ public sealed class Succession
     public DateTime? LinkedAtUtc { get; set; }
 }
 
+/// <summary>The states of a hand-over (design 7.8).</summary>
+public static class HandOverStates
+{
+    public const string Waiting = "waiting";
+    public const string Accepted = "accepted";
+    public const string Declined = "declined";
+    public const string Cancelled = "cancelled";
+}
+
+/// <summary>
+/// A battery's hand-over from one cluster to another (design 4.1 and 7.8, step 4d): asked by the sending cluster's admin,
+/// answered by the receiving cluster's, or cancelled. App data, not passport data.
+/// </summary>
+public sealed class HandOver : IDppRecord
+{
+    [BsonId]
+    public required string Id { get; init; }
+
+    public required string PackId { get; init; }
+
+    public required string FromClusterId { get; init; }
+
+    public required string ToClusterId { get; init; }
+
+    /// <summary>One of <see cref="HandOverStates"/>.</summary>
+    public required string State { get; set; }
+
+    public required string AskedBy { get; init; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public required DateTime AskedAtUtc { get; init; }
+
+    /// <summary>Who accepted, declined or cancelled it.</summary>
+    public string? AnsweredBy { get; set; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public DateTime? AnsweredAtUtc { get; set; }
+}
+
 /// <summary>One repair of a pack (design 7.9): when, what was repaired, and who recorded it when.</summary>
 public sealed class RepairEntry
 {
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index 45ac075..35359be 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -20,6 +20,7 @@ public sealed class DppStore
     public const string Operators = "dpp_operators";
     public const string Packs = "dpp_packs";
     public const string Versions = "dpp_passport_versions";
+    public const string HandOvers = "dpp_transfers";
 
     private const string UniqueProductIdentifierField = "Values.uniqueProductIdentifier.Value";
     private const string SerialNumberField = "Values.batterySerialNumber.Value";
@@ -33,6 +34,7 @@ public sealed class DppStore
         [typeof(EconomicOperator)] = Operators,
         [typeof(BatteryPack)] = Packs,
         [typeof(PassportVersion)] = Versions,
+        [typeof(HandOver)] = HandOvers,
     };
 
     private readonly IMongoDatabase? _database;
@@ -93,6 +95,15 @@ public sealed class DppStore
                 Builders<PassportVersion>.IndexKeys.Ascending(version => version.PackId).Ascending(version => version.DppStatus),
                 new CreateIndexOptions { Name = "pack_status" }),
         ], cancellationToken);
+        await Collection<HandOver>().Indexes.CreateManyAsync(
+        [
+            new CreateIndexModel<HandOver>(
+                Builders<HandOver>.IndexKeys.Ascending(handOver => handOver.PackId).Ascending(handOver => handOver.State),
+                new CreateIndexOptions { Name = "pack_state" }),
+            new CreateIndexModel<HandOver>(
+                Builders<HandOver>.IndexKeys.Ascending(handOver => handOver.ToClusterId).Ascending(handOver => handOver.State),
+                new CreateIndexOptions { Name = "to_state" }),
+        ], cancellationToken);
     }
 
     public async Task<T?> GetAsync<T>(string id, CancellationToken cancellationToken = default)
@@ -307,6 +318,45 @@ public sealed class DppStore
             Builders<PassportVersion>.Update.Set(version => version.DppStatus, status).Set(version => version.LastUpdatedUtc, lastUpdatedUtc),
             cancellationToken: cancellationToken);
 
+    /// <summary>The battery's hand-over waiting for an answer (step 4d); null when none is.</summary>
+    public async Task<HandOver?> FindWaitingHandOverAsync(string packId, CancellationToken cancellationToken = default)
+    {
+        if (_database is null || string.IsNullOrWhiteSpace(packId))
+        {
+            return null;
+        }
+
+        return await Collection<HandOver>().Find(handOver => handOver.PackId == packId && handOver.State == HandOverStates.Waiting).FirstOrDefaultAsync(cancellationToken);
+    }
+
+    /// <summary>The hand-overs waiting for an answer from these receiving clusters, oldest first.</summary>
+    public async Task<IReadOnlyList<HandOver>> ListWaitingHandOversToAsync(IEnumerable<string> clusterIds, CancellationToken cancellationToken = default)
+    {
+        if (_database is null)
+        {
+            return [];
+        }
+
+        var to = clusterIds.ToArray();
+        return await Collection<HandOver>()
+            .Find(Builders<HandOver>.Filter.In(handOver => handOver.ToClusterId, to) & Builders<HandOver>.Filter.Eq(handOver => handOver.State, HandOverStates.Waiting))
+            .SortBy(handOver => handOver.AskedAtUtc)
+            .ToListAsync(cancellationToken);
+    }
+
+    /// <summary>
+    /// Ends a waiting hand-over (accepted, declined or cancelled); false when it no longer waits, so two answers at once
+    /// can't both count.
+    /// </summary>
+    public async Task<bool> SetHandOverStateAsync(string handOverId, string state, string by, DateTime atUtc, CancellationToken cancellationToken = default)
+    {
+        var result = await Collection<HandOver>().UpdateOneAsync(
+            handOver => handOver.Id == handOverId && handOver.State == HandOverStates.Waiting,
+            Builders<HandOver>.Update.Set(handOver => handOver.State, state).Set(handOver => handOver.AnsweredBy, by).Set(handOver => handOver.AnsweredAtUtc, atUtc),
+            cancellationToken: cancellationToken);
+        return result.ModifiedCount == 1;
+    }
+
     /// <summary>Sets or clears a pack's invalid or deletion mark (step 4c-2). False when the pack doesn't exist.</summary>
     public async Task<bool> SetMarkAsync(string packId, PassportMark? mark, CancellationToken cancellationToken = default)
     {
```

`web/Dpp/Editing/HandOverService.cs`:

```csharp
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>What asking for a hand-over did: the receiving cluster's name to confirm, the waiting hand-over, or why not.</summary>
public sealed record HandOverAsk(string? Problem, string? ReceiverName, HandOver? HandOver);

/// <summary>A hand-over waiting for a cluster admin's answer, with what the Registry card shows about it.</summary>
public sealed record WaitingHandOver(HandOver HandOver, string SerialNumber, string ModelName, string FromName);

/// <summary>
/// Hand-overs between clusters (design 7.8, decisions 9, 14 and 34; step 4d). A battery's cluster admin names the receiving
/// cluster by its ID, sees its name, and confirms; the battery stays with them until the receiving cluster's admins
/// accept, and the receiver sees nothing until then. Either side can end it: the sender (or the team) cancels, the
/// receiver declines. On acceptance the battery moves; the cluster isn't passport data, so no version is made.
/// </summary>
public sealed class HandOverService(DppStore store, ClusterRepository clusters, ApplicationAuditService audit, TimeProvider clock)
{
    public const string AskedEvent = "dpp.handover-asked";
    public const string AnsweredEvent = "dpp.handover-answered";
    public const string CancelledEvent = "dpp.handover-cancelled";

    public const string NoBattery = "the battery was not found";
    public const string NotYours = "only the battery's cluster admins hand it over";
    public const string TeamMovesDirectly = "the team moves a battery directly on its battery page";
    public const string OnlyActive = "only a battery with an active passport is handed over";
    public const string NoSuchCluster = "no cluster has this ID; ask the receiving customer for theirs";
    public const string SameCluster = "the battery is already in this cluster";
    public const string AlreadyWaiting = "a hand-over of this battery is already waiting";
    public const string NothingWaiting = "no hand-over of this battery is waiting";
    public const string NotTheReceiver = "only the receiving cluster's admins answer a hand-over";
    public const string NotWaiting = "this hand-over isn't waiting any more";

    /// <summary>Why an accept is refused once the passport stopped being active while it waited, for the receiver.</summary>
    public const string NoLongerActive = "the battery's passport is no longer active (it was replaced by a new passport or marked invalid), so it can't move to your cluster. Decline it, or ask the sending cluster to cancel";

    /// <summary>Names (and with <paramref name="confirm"/>, asks for) the hand-over of a battery to the cluster with this ID.</summary>
    public async Task<HandOverAsk> AskAsync(ChangeSender sender, string packId, string? toClusterId, bool confirm, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind == SenderKind.Team)
        {
            return Refused(TeamMovesDirectly);
        }

        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
        {
            return Refused(NoBattery);
        }

        if (sender.Kind != SenderKind.ClusterAdmin || pack.ClusterId is not { } from || !sender.ClusterIds.Contains(from))
        {
            return Refused(NotYours);
        }

        if (await store.GetActiveVersionAsync(packId, cancellationToken) is null)
        {
            return Refused(OnlyActive);
        }

        var to = string.IsNullOrWhiteSpace(toClusterId) ? string.Empty : ClusterRepository.NormalizeClusterId(toClusterId);
        if (to == from)
        {
            return Refused(SameCluster);
        }

        if (to.Length == 0 || await clusters.GetClusterByIdAsync(to, cancellationToken) is not { } cluster)
        {
            return Refused(NoSuchCluster);
        }

        if (await store.FindWaitingHandOverAsync(packId, cancellationToken) is not null)
        {
            return Refused(AlreadyWaiting);
        }

        var name = BsonHelpers.GetString(cluster, "name") is { Length: > 0 } named ? named : to;
        if (!confirm)
        {
            return new HandOverAsk(null, name, null);
        }

        var handOver = new HandOver
        {
            Id = $"handover-{Guid.NewGuid():N}",
            PackId = packId,
            FromClusterId = from,
            ToClusterId = to,
            State = HandOverStates.Waiting,
            AskedBy = sender.Who,
            AskedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        await store.InsertAsync(handOver, cancellationToken);
        await LogAsync(AskedEvent, sender, handOver, $"Battery {Serial(pack)}: hand-over to {name} asked", cancellationToken);
        return new HandOverAsk(null, name, handOver);
    }

    /// <summary>The receiving cluster's admin accepts (the battery moves) or declines. Null when done.</summary>
    public async Task<string?> AnswerAsync(ChangeSender sender, string handOverId, bool accept, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetAsync<HandOver>(handOverId, cancellationToken) is not { } handOver)
        {
            return NotWaiting;
        }

        if (sender.Kind != SenderKind.ClusterAdmin || !sender.ClusterIds.Contains(handOver.ToClusterId))
        {
            return NotTheReceiver;
        }

        var pack = await store.GetAsync<BatteryPack>(handOver.PackId, cancellationToken);
        if (handOver.State != HandOverStates.Waiting || pack?.ClusterId != handOver.FromClusterId)
        {
            return NotWaiting;
        }

        // The passport may have stopped being active while it waited (a successor, a mark): it can't be managed then.
        if (accept && await store.GetActiveVersionAsync(handOver.PackId, cancellationToken) is null)
        {
            return NoLongerActive;
        }

        if (!await store.SetHandOverStateAsync(handOver.Id, accept ? HandOverStates.Accepted : HandOverStates.Declined, sender.Who, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            return NotWaiting; // answered or cancelled meanwhile
        }

        if (accept)
        {
            await store.SetPackClusterAsync(handOver.PackId, handOver.ToClusterId, cancellationToken);
        }

        await LogAsync(AnsweredEvent, sender, handOver, $"Battery {Serial(pack)}: hand-over {(accept ? "accepted; it moved" : "declined; it stays")}", cancellationToken, accept);
        return null;
    }

    /// <summary>The sender's cluster admin, or the team, cancels the battery's waiting hand-over. Null when done.</summary>
    public async Task<string?> CancelAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.FindWaitingHandOverAsync(packId, cancellationToken) is not { } handOver)
        {
            return NothingWaiting;
        }

        var mayCancel = sender.Kind == SenderKind.Team
            || (sender.Kind == SenderKind.ClusterAdmin && sender.ClusterIds.Contains(handOver.FromClusterId));
        if (!mayCancel)
        {
            return NotYours;
        }

        if (!await store.SetHandOverStateAsync(handOver.Id, HandOverStates.Cancelled, sender.Who, clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            return NothingWaiting;
        }

        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
        await LogAsync(CancelledEvent, sender, handOver, $"Battery {Serial(pack)}: hand-over cancelled", cancellationToken);
        return null;
    }

    /// <summary>The hand-overs waiting for the sender's answer: to a cluster they are admin of.</summary>
    public async Task<IReadOnlyList<WaitingHandOver>> WaitingForAsync(ChangeSender sender, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.ClusterAdmin || sender.ClusterIds.Count == 0)
        {
            return [];
        }

        var waiting = new List<WaitingHandOver>();
        foreach (var handOver in await store.ListWaitingHandOversToAsync(sender.ClusterIds, cancellationToken))
        {
            var sources = await store.LoadSourcesAsync(handOver.PackId, cancellationToken);
            var from = await clusters.GetClusterByIdAsync(handOver.FromClusterId, cancellationToken);
            waiting.Add(new WaitingHandOver(
                handOver,
                sources is null ? handOver.PackId : Serial(sources.Pack),
                sources?.Model.Name ?? string.Empty,
                from is null ? handOver.FromClusterId : BsonHelpers.GetString(from, "name")));
        }

        return waiting;
    }

    private Task LogAsync(string eventType, ChangeSender sender, HandOver handOver, string message, CancellationToken cancellationToken, bool? accepted = null)
    {
        var metadata = new BsonDocument { ["handOver"] = handOver.Id, ["from"] = handOver.FromClusterId, ["to"] = handOver.ToClusterId };
        if (accepted is { } answer)
        {
            metadata["accepted"] = answer;
        }

        return audit.AppendApplicationAuditEventAsync(eventType, sender.Who, sender.Kind.ToString(), "form", message, metadata, entityType: "dpp-pack", entityId: handOver.PackId, cancellationToken: cancellationToken);
    }

    private static HandOverAsk Refused(string problem) => new(problem, null, null);

    private static string Serial(BatteryPack? pack) => pack is null ? "?" : BatteryPassWeb.Dpp.Pages.BatteryPages.Text(pack, "batterySerialNumber") ?? pack.Id;
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HandOverTests | FullyQualifiedName~ResetCollectionsTests"`
Expected: `Passed!  - Failed:     0, Passed:    24`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2223`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/HandOverTests.cs BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs tools/V3Reset/ResetCollections.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Editing/HandOverService.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs
git commit -m "feat(dpp): hand-overs between clusters, asked, answered or cancelled" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The hand-over card and window, the Registry's "Waiting for you" and the team's cancel

Mock-up S2 and S8's "Hand over" card on the cluster admin's battery page: its window names the cluster first, then "Ask them to accept"; while it waits, the card says so with "Cancel the hand-over". S1's "Waiting for you" card at the top of the Registry, with Accept and Decline (Q13). The team's battery page shows a waiting hand-over beside the cluster, with "Cancel it".

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`
- Modify: `web/Controllers/BatteriesController.cs`
- Modify: `web/Controllers/ClusterBatteriesController.cs`
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Modify: `web/Dpp/Pages/RegistryList.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/ClusterAdmin/Battery.cshtml`
- Create: `web/Views/ClusterAdmin/_HandOverCard.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`
- Modify: `web/wwwroot/js/bp-record-editor.js`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs`:

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

/// <summary>Hand-overs on the pages (mock-up S1, S2 and S8; step 4d).</summary>
[Collection(MongoCollection.Name)]
public sealed class HandOverPagesTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";

    private const string SouthAdmin = "south-admin@example.com";

    [Fact]
    public async Task TheSendersPage_ShouldNameTheClusterFirstThenAskAndShowItWaiting()
    {
        var rig = await RigAsync();
        var sender = ClusterController(rig, NorthAdmin);

        var check = Assert.IsType<RedirectResult>(await sender.HandOver(rig.PackId, " Cluster-South ", null, HandOverTests.Service(rig), CancellationToken.None));
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#handover", check.Url);
        Assert.Equal(BatteryTesting.South, sender.TempData[ClusterBatteriesController.HandOverToKey]);
        Assert.Equal("Customer South", sender.TempData[ClusterBatteriesController.HandOverNameKey]);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));

        // "Ask them to accept" sends the ID whose name was shown; a changed ID is named again instead.
        await sender.HandOver(rig.PackId, "cluster-other", BatteryTesting.South, HandOverTests.Service(rig), CancellationToken.None);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
        await sender.HandOver(rig.PackId, BatteryTesting.South, BatteryTesting.South, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal("Asked. The battery stays with your cluster until Customer South accepts.", sender.TempData[BatteriesController.CardStatusKey]);
        var card = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId))!.HandOver;
        Assert.Equal((BatteryTesting.South, "Customer South"), (card.Waiting!.ToClusterId, card.ReceiverName));
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}/handover", card.Action);
    }

    [Fact]
    public async Task AnUnknownCluster_ShouldBeSaidOnTheCard()
    {
        var rig = await RigAsync();
        var sender = ClusterController(rig, NorthAdmin);

        await sender.HandOver(rig.PackId, "cluster-unknown", null, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal($"Not asked: {HandOverService.NoSuchCluster}.", sender.TempData[BatteriesController.CardErrorKey]);
        Assert.Equal("handover", sender.TempData[BatteriesController.CardKey]);
        Assert.Null(sender.TempData[ClusterBatteriesController.HandOverNameKey]);
    }

    [Fact]
    public async Task TheSender_ShouldCancelTheWaitingHandOverFromItsCard()
    {
        var rig = await RigAsync();
        var sender = ClusterController(rig, NorthAdmin);
        await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);

        await sender.CancelHandOver(rig.PackId, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal("Hand-over cancelled. The battery stays with your cluster.", sender.TempData[BatteriesController.CardStatusKey]);
        Assert.Null((await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId))!.HandOver.Waiting);
    }

    [Fact]
    public async Task TheReceiversRegistry_ShouldShowItWaitingAndAcceptIt()
    {
        var rig = await RigAsync();
        var asked = await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);
        var receiver = RegistryController(rig, SouthAdmin);

        var page = (RegistryPage)Assert.IsType<ViewResult>(await receiver.Index(null, HandOverTests.Service(rig), CancellationToken.None)).Model!;
        var waiting = Assert.Single(page.Waiting);
        Assert.Equal(("Customer North", NorthAdmin), (waiting.FromName, waiting.HandOver.AskedBy));
        Assert.Empty(page.Rows); // Q11: nothing of the battery until it accepts
        Assert.Empty(((RegistryPage)((ViewResult)await RegistryController(rig, NorthAdmin).Index(null, HandOverTests.Service(rig), CancellationToken.None)).Model!).Waiting);

        var accepted = Assert.IsType<RedirectResult>(await receiver.Answer(asked.HandOver!.Id, accept: true, HandOverTests.Service(rig), CancellationToken.None));

        Assert.Equal("/registry", accepted.Url);
        Assert.Equal("Accepted. The battery is now your cluster's; manage it from its row below.", receiver.TempData[ProductDataController.StatusKey]);
        var after = (RegistryPage)((ViewResult)await receiver.Index(null, HandOverTests.Service(rig), CancellationToken.None)).Model!;
        Assert.Empty(after.Waiting);
        Assert.NotNull(after.ManageHref(Assert.Single(after.Rows)));
    }

    [Fact]
    public async Task TheReceiver_ShouldDeclineAndAnOutsiderShouldBeRefused()
    {
        var rig = await RigAsync();
        var asked = await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);
        var outsider = RegistryController(rig, NorthAdmin);
        var receiver = RegistryController(rig, SouthAdmin);

        await outsider.Answer(asked.HandOver!.Id, accept: true, HandOverTests.Service(rig), CancellationToken.None);
        await receiver.Answer(asked.HandOver.Id, accept: false, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal($"Not accepted: {HandOverService.NotTheReceiver}.", outsider.TempData[ProductDataController.ErrorKey]);
        Assert.Equal("Declined. The battery stays with the sending cluster.", receiver.TempData[ProductDataController.StatusKey]);
        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
    }

    [Fact]
    public async Task ARefusedAccept_ShouldTellTheReceiverWhyInPlainWords()
    {
        var rig = await RigAsync();
        var asked = await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);
        await new PassportMarks(rig.Store, new ApplicationAuditService(rig.Context), rig.Clock).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures");
        var receiver = RegistryController(rig, SouthAdmin);

        await receiver.Answer(asked.HandOver!.Id, accept: true, HandOverTests.Service(rig), CancellationToken.None);

        Assert.Equal(
            "Not accepted: the battery's passport is no longer active (it was replaced by a new passport or marked invalid), so it can't move to your cluster. Decline it, or ask the sending cluster to cancel.",
            receiver.TempData[ProductDataController.ErrorKey]);
        Assert.Single(((RegistryPage)((ViewResult)await receiver.Index(null, HandOverTests.Service(rig), CancellationToken.None)).Model!).Waiting); // still there to decline
    }

    [Fact]
    public async Task TheTeamsPage_ShouldShowAWaitingHandOverAndCancelIt()
    {
        var rig = await RigAsync();
        await HandOverTests.Service(rig).AskAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId, BatteryTesting.South, confirm: true);
        var team = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        Assert.Equal("Customer South", (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.HandOverTo);
        var cancelled = Assert.IsType<RedirectResult>(await team.CancelHandOver(rig.PackId, HandOverTests.Service(rig), CancellationToken.None));

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), cancelled.Url);
        Assert.Equal("Hand-over cancelled. The battery stays in its cluster.", team.TempData[BatteriesController.CardStatusKey]);
        Assert.Null((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.HandOverTo);
    }

    [Fact]
    public void ThePages_ShouldDrawTheCardTheWindowAndTheWaitingRows()
    {
        var cluster = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "_HandOverCard.cshtml"));
        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));
        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-record-editor.js"));
        var styles = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css"));

        Assert.Contains("~/Views/ClusterAdmin/_HandOverCard.cshtml\", Model.HandOver", cluster, StringComparison.Ordinal);
        Assert.Contains("data-open-on-load=\"@(found ? \"true\" : \"false\")\"", card, StringComparison.Ordinal);
        Assert.Contains("name=\"checkedId\"", card, StringComparison.Ordinal);
        Assert.Contains("TempData[ClusterBatteriesController.HandOverToKey] as string", card, StringComparison.Ordinal); // read once, so the window opens only right after "Find the cluster"
        Assert.DoesNotContain("TempData.Peek", card, StringComparison.Ordinal);
        Assert.Contains("Cancel the hand-over", card, StringComparison.Ordinal);
        Assert.Contains("foreach (var waiting in Model.Waiting)", registry, StringComparison.Ordinal);
        Assert.Contains("bp-handover-row", registry, StringComparison.Ordinal);
        Assert.Contains("Model.HandOverTo is { } receiver", battery, StringComparison.Ordinal);
        Assert.Contains("dialog[data-open-on-load=\"true\"]", script, StringComparison.Ordinal);
        Assert.Contains(".bp-handover-row", styles, StringComparison.Ordinal);
    }

    private async Task<PublicationRig> RigAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        await clusters.UpsertClusterMembershipAsync(SouthAdmin, BatteryTesting.South, "clusterAdmin");
        return rig;
    }

    private static ClusterBatteriesController ClusterController(PublicationRig rig, string email) =>
        With(new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context))), email);

    private static RegistryController RegistryController(PublicationRig rig, string email) =>
        With(new RegistryController(new DppReaders(new ClusterRepository(rig.Context)), BatteryTesting.Pages(rig)), email);

    private static T With<T>(T controller, string email)
        where T : Controller
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, "member")], "test")),
        };
        http.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new NoTempDataStorage());
        return controller;
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

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HandOverPagesTests | FullyQualifiedName~ClusterBatteriesControllerTests | FullyQualifiedName~ClusterBatteryPagesTests | FullyQualifiedName~RegistryListTests | FullyQualifiedName~BatteriesControllerTests"`
Expected: the build fails with CS0117, CS1061, CS1501 (for example `error CS0117 'ClusterBatteriesController' does not contain a definition for 'HandOverNameKey'`).

- [ ] **Step 3: Write the code**

Patch `4d-task2-code.patch` (`web/Controllers/BatteriesController.cs`, `web/Controllers/ClusterBatteriesController.cs`, `web/Controllers/RegistryController.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Dpp/Pages/RegistryList.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/Views/Registry/Index.cshtml`, `web/wwwroot/css/bp-product-data.css`, `web/wwwroot/js/bp-record-editor.js`). Save the block as `../4d-task2-code.patch`, outside the worktree, and run `git apply ../4d-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/BatteriesController.cs b/web/Controllers/BatteriesController.cs
index 0c41405..68518f4 100644
--- a/web/Controllers/BatteriesController.cs
+++ b/web/Controllers/BatteriesController.cs
@@ -83,6 +83,16 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
         return Redirect(BatteryPages.BatteryHref(packId));
     }
 
+    /// <summary>The team cancels a hand-over waiting for the receiving cluster's answer (design 7.8, step 4d).</summary>
+    [HttpPost("{packId}/handover/cancel")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> CancelHandOver(string packId, [FromServices] HandOverService handOvers, CancellationToken cancellationToken)
+    {
+        var problem = await handOvers.CancelAsync(ChangeSender.Team(Who), packId, cancellationToken);
+        Card("cluster", problem is null ? "Hand-over cancelled. The battery stays in its cluster." : null, problem is null ? null : $"Not cancelled: {problem}.");
+        return Redirect(BatteryPages.BatteryHref(packId));
+    }
+
     /// <summary>The largest JSON export taken for the module list; the battery system's are a few kilobytes.</summary>
     public const long MaxExportBytes = 1024 * 1024;
 
diff --git a/web/Controllers/ClusterBatteriesController.cs b/web/Controllers/ClusterBatteriesController.cs
index 3eeee58..9253b50 100644
--- a/web/Controllers/ClusterBatteriesController.cs
+++ b/web/Controllers/ClusterBatteriesController.cs
@@ -2,6 +2,7 @@ using BatteryPassWeb.Dpp.Access;
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Pages;
+using BatteryPassWeb.Services;
 using Microsoft.AspNetCore.Authorization;
 using Microsoft.AspNetCore.Mvc;
 
@@ -18,6 +19,11 @@ public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppRea
 {
     private const string PageView = "~/Views/ClusterAdmin/Battery.cshtml";
 
+    /// <summary>The cluster ID typed in the "Hand over" window, and the name it was found by (step 4d).</summary>
+    public const string HandOverToKey = "HandOverTo";
+
+    public const string HandOverNameKey = "HandOverName";
+
     [HttpGet("{packId}")]
     public async Task<IActionResult> Battery(string packId, CancellationToken cancellationToken)
     {
@@ -153,6 +159,54 @@ public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppRea
         return Redirect(link && message.Problem is null ? "/registry" : $"{ClusterBatteryPages.BatteryHref(packId)}#status");
     }
 
+    /// <summary>
+    /// "Hand over…" (design 7.8, step 4d): first names the receiving cluster, then "Ask them to accept" asks for the ID
+    /// whose name was shown. A changed ID is named again instead of asked for.
+    /// </summary>
+    [HttpPost("{packId}/handover")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> HandOver(string packId, [FromForm] string? toClusterId, [FromForm] string? checkedId, [FromServices] HandOverService handOvers, CancellationToken cancellationToken)
+    {
+        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
+        {
+            return NotFound();
+        }
+
+        var typed = string.IsNullOrWhiteSpace(toClusterId) ? string.Empty : ClusterRepository.NormalizeClusterId(toClusterId);
+        var confirm = typed.Length > 0 && !string.IsNullOrWhiteSpace(checkedId) && ClusterRepository.NormalizeClusterId(checkedId) == typed;
+        var asked = await handOvers.AskAsync(sender, packId, typed, confirm, cancellationToken);
+        if (asked.Problem is { } problem)
+        {
+            Card("handover", null, $"Not asked: {problem}.");
+        }
+        else if (asked.HandOver is null)
+        {
+            TempData[HandOverToKey] = typed;
+            TempData[HandOverNameKey] = asked.ReceiverName;
+        }
+        else
+        {
+            Card("handover", $"Asked. The battery stays with your cluster until {asked.ReceiverName} accepts.", null);
+        }
+
+        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#handover");
+    }
+
+    /// <summary>The sender cancels its waiting hand-over (step 4d).</summary>
+    [HttpPost("{packId}/handover/cancel")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> CancelHandOver(string packId, [FromServices] HandOverService handOvers, CancellationToken cancellationToken)
+    {
+        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
+        {
+            return NotFound();
+        }
+
+        var problem = await handOvers.CancelAsync(sender, packId, cancellationToken);
+        Card("handover", problem is null ? "Hand-over cancelled. The battery stays with your cluster." : null, problem is null ? null : $"Not cancelled: {problem}.");
+        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#handover");
+    }
+
     /// <summary>The signed-in user as a cluster admin; null for the team (it has its own page) and for anyone else.</summary>
     private async Task<ChangeSender?> ClusterAdminAsync(CancellationToken cancellationToken) =>
         await readers.EditorForAsync(User, cancellationToken) is { Kind: SenderKind.ClusterAdmin } sender ? sender : null;
diff --git a/web/Controllers/RegistryController.cs b/web/Controllers/RegistryController.cs
index 5ba5257..91b9ce9 100644
--- a/web/Controllers/RegistryController.cs
+++ b/web/Controllers/RegistryController.cs
@@ -1,4 +1,5 @@
 using BatteryPassWeb.Dpp.Access;
+using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Pages;
 using Microsoft.AspNetCore.Authorization;
 using Microsoft.AspNetCore.Mvc;
@@ -7,17 +8,42 @@ namespace BatteryPassWeb.Controllers;
 
 /// <summary>
 /// The Registry (decision 30): a read-only list of the published batteries a signed-in reader may open, searched like the
-/// team's battery list, each opening its passport's detail page.
+/// team's battery list, each opening its passport's detail page. A cluster admin answers the hand-overs waiting for them
+/// at its top (step 4d).
 /// </summary>
 [Authorize]
 [Route("registry")]
 public sealed class RegistryController(DppReaders readers, BatteryPages batteries) : Controller
 {
     [HttpGet("")]
-    public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken cancellationToken)
+    public async Task<IActionResult> Index([FromQuery] string? q, [FromServices] HandOverService handOvers, CancellationToken cancellationToken)
     {
         var reader = await readers.ForAsync(User, cancellationToken);
         var editor = await readers.EditorForAsync(User, cancellationToken);
-        return View(RegistryList.Build(reader, await batteries.ListAsync(q, cancellationToken), editor));
+        return View(RegistryList.Build(reader, await batteries.ListAsync(q, cancellationToken), editor) with
+        {
+            Waiting = editor is null ? [] : await handOvers.WaitingForAsync(editor, cancellationToken),
+        });
+    }
+
+    /// <summary>The receiving cluster's admin accepts or declines a hand-over (step 4d).</summary>
+    [HttpPost("handovers/{handOverId}")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> Answer(string handOverId, [FromForm] bool accept, [FromServices] HandOverService handOvers, CancellationToken cancellationToken)
+    {
+        var editor = await readers.EditorForAsync(User, cancellationToken);
+        var problem = editor is null ? HandOverService.NotTheReceiver : await handOvers.AnswerAsync(editor, handOverId, accept, cancellationToken);
+        if (problem is null)
+        {
+            TempData[ProductDataController.StatusKey] = accept
+                ? "Accepted. The battery is now your cluster's; manage it from its row below."
+                : "Declined. The battery stays with the sending cluster.";
+        }
+        else
+        {
+            TempData[ProductDataController.ErrorKey] = $"{(accept ? "Not accepted" : "Not declined")}: {problem}.";
+        }
+
+        return Redirect("/registry");
     }
 }
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index e832e84..1342aaa 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -112,6 +112,9 @@ public sealed record BatteryPage(
     /// <summary>The battery status card (step 4b), shared with the cluster admin's page.</summary>
     public StatusCardView? Status { get; init; }
 
+    /// <summary>The receiving cluster's name while a hand-over waits for its answer (step 4d); null otherwise.</summary>
+    public string? HandOverTo { get; init; }
+
     /// <summary>The team's "Passport status" card (mock-up S5, step 4c-2): invalid, marked for deletion, and their undo.</summary>
     public PassportMarkView Marks => new(
         Publication.Active is not null ? DppStatuses.Active : Publication.Latest?.DppStatus,
@@ -360,6 +363,9 @@ public sealed class BatteryPages(
             new RemovalView($"{href}/remove", versions.Count > 0 ? "its passport is published, so it stays with its versions." : null))
         {
             Status = await status.CardAsync(sources, active is not null, href, forTeam: true, cancellationToken: cancellationToken),
+            HandOverTo = await store.FindWaitingHandOverAsync(packId, cancellationToken) is { } waiting
+                ? clusterChoices.FirstOrDefault(cluster => string.Equals(cluster.Id, waiting.ToClusterId, StringComparison.OrdinalIgnoreCase))?.Name ?? waiting.ToClusterId
+                : null,
         };
     }
 
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index 220481a..80de20b 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -26,10 +26,16 @@ public sealed record ClusterBatteryPage(
     /// <summary>The battery status card (step 4b), shared with the team's page.</summary>
     public StatusCardView? Status { get; init; }
 
+    /// <summary>The "Hand over" card (step 4d): the waiting hand-over, or the window to ask for one.</summary>
+    public HandOverCardView HandOver { get; init; } = new(string.Empty, string.Empty, null, null);
+
     /// <summary>The team page's cards, without what only the team does (step 4a).</summary>
     public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false);
 }
 
+/// <summary>The "Hand over" card (mock-up S2 and S8): where its forms go, and the hand-over waiting with its receiver's name.</summary>
+public sealed record HandOverCardView(string Action, string SerialNumber, HandOver? Waiting, string? ReceiverName);
+
 /// <summary>
 /// The cluster admin's battery page (design 6.1 and 12, step 4a). A cluster admin manages only a battery of a cluster
 /// they are admin of, and only once its passport is published and active: a draft is the team's (J-6). Every change
@@ -89,6 +95,7 @@ public sealed class ClusterBatteryPages(
             cluster => BsonHelpers.GetString(cluster, "name"),
             StringComparer.OrdinalIgnoreCase);
         var versions = await store.ListVersionsAsync(pack.Id, cancellationToken);
+        var waiting = await store.FindWaitingHandOverAsync(pack.Id, cancellationToken);
         return new ClusterBatteryPage(
             pack,
             sources,
@@ -101,6 +108,7 @@ public sealed class ClusterBatteryPages(
             await documentStore.ListAsync(TargetKind.Pack, pack.Id, DocumentService.Accidents, cancellationToken))
         {
             Status = await status.CardAsync(sources, active: true, href, forTeam: false, cancellationToken: cancellationToken),
+            HandOver = new HandOverCardView($"{href}/handover", serial, waiting, waiting is null ? null : names.GetValueOrDefault(waiting.ToClusterId) ?? waiting.ToClusterId),
         };
     }
 
diff --git a/web/Dpp/Pages/RegistryList.cs b/web/Dpp/Pages/RegistryList.cs
index b506a36..1c1b66b 100644
--- a/web/Dpp/Pages/RegistryList.cs
+++ b/web/Dpp/Pages/RegistryList.cs
@@ -10,6 +10,9 @@ public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query,
     /// <summary>The clusters the signed-in user is admin of (step 4a); empty for everyone else, the team included.</summary>
     public IReadOnlySet<string> ManagedClusterIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
 
+    /// <summary>The hand-overs waiting for the signed-in cluster admin's answer (step 4d, mock-up S1).</summary>
+    public IReadOnlyList<WaitingHandOver> Waiting { get; init; } = [];
+
     /// <summary>The cluster admin's page of a battery of a cluster they are admin of; null otherwise.</summary>
     public string? ManageHref(BatteryRow row)
     {
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 2713c7d..45d8930 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -104,6 +104,14 @@
                 <dd>
                     <span class="@(Model.Pack.ClusterId is null ? "bp-batteries-muted" : null)">@Model.ClusterLabel</span>
                     <button type="button" class="bp-text-button" data-open-dialog="cluster-dialog">Change</button>
+                    @if (Model.HandOverTo is { } receiver)
+                    {
+                        <form method="post" action="@href/handover/cancel" class="d-block">
+                            @Html.AntiForgeryToken()
+                            <small class="bp-field-help">Hand-over to @receiver waiting for their answer.</small>
+                            <button type="submit" class="bp-text-button">Cancel it</button>
+                        </form>
+                    }
                 </dd>
             </div>
             @if (publication.IsPublished)
@@ -137,7 +145,7 @@
         <form method="post" action="@href/cluster">
             @Html.AntiForgeryToken()
             <h2 id="cluster-dialog-title">Change the cluster</h2>
-            <p class="bp-help-text">The customer the battery belongs to. Its members see its purchaser data. The passport doesn't change.</p>
+            <p class="bp-help-text">The customer the battery belongs to. Its members see its purchaser data. The passport doesn't change.@(Model.HandOverTo is { } waitingFor ? $" Moving it cancels the hand-over waiting for {waitingFor}." : null)</p>
             <label class="bp-product-version-selected-field">
                 <span>Cluster</span>
                 <select name="clusterId">
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 0955986..15a887e 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -55,6 +55,7 @@
     }
     @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
     @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
+    @await Html.PartialAsync("~/Views/ClusterAdmin/_HandOverCard.cshtml", Model.HandOver)
     @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
     @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
 </main>
diff --git a/web/Views/Registry/Index.cshtml b/web/Views/Registry/Index.cshtml
index 6ddce11..e688c98 100644
--- a/web/Views/Registry/Index.cshtml
+++ b/web/Views/Registry/Index.cshtml
@@ -12,6 +12,32 @@
         </div>
     </header>
 
+    @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
+    @if (Model.Waiting.Count > 0)
+    {
+        <section class="bp-card bp-console-form-card bp-product-editor-section mt-4" id="waiting">
+            <span class="bp-summary-title">Waiting for you</span>
+            <h2 class="h5 mb-1">@Words.Count(Model.Waiting.Count, "battery", "batteries") handed over to your cluster</h2>
+            <p class="bp-help-text mb-2">Accept it to see its purchaser data and manage it. Until then it stays with the sending cluster.</p>
+            @foreach (var waiting in Model.Waiting)
+            {
+                <div class="bp-handover-row">
+                    <div>
+                        <strong>@waiting.SerialNumber</strong>@(waiting.ModelName.Length > 0 ? $" · {waiting.ModelName}" : null)
+                        <small class="d-block text-secondary">From @waiting.FromName, asked @ValueWords.Time(waiting.HandOver.AskedAtUtc) by @waiting.HandOver.AskedBy</small>
+                    </div>
+                    <form method="post" action="/registry/handovers/@Uri.EscapeDataString(waiting.HandOver.Id)">
+                        @Html.AntiForgeryToken()
+                        <div class="bp-dialog-actions">
+                            <button type="submit" name="accept" value="false" class="bp-secondary-button">Decline</button>
+                            <button type="submit" name="accept" value="true" class="bp-primary-button">Accept</button>
+                        </div>
+                    </form>
+                </div>
+            }
+        </section>
+    }
+
     <section class="bp-console-toolbar bp-battery-table-toolbar">
         <form action="/registry" class="bp-search-form" role="search">
             <input name="q" value="@Model.Query" placeholder="Serial number, battery ID or passport ID" aria-label="Search the registry" />
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index 3880b20..1e86d12 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -2765,3 +2765,22 @@ main .bp-field-marker.is-published {
 [data-status-dialog].is-step-2 [data-status-step="2"] {
   display: block;
 }
+
+/* Hand-overs waiting for a cluster admin on the Registry (step 4d, mock-up S1): one row each, with its two answers. */
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
diff --git a/web/wwwroot/js/bp-record-editor.js b/web/wwwroot/js/bp-record-editor.js
index f5fad24..df034de 100644
--- a/web/wwwroot/js/bp-record-editor.js
+++ b/web/wwwroot/js/bp-record-editor.js
@@ -38,6 +38,9 @@
         const dialog = document.getElementById(button.dataset.openDialog);
         button.addEventListener('click', () => dialog?.showModal());
     });
+    // A dialog marked data-open-on-load="true" opens at once, e.g. the "Hand over" window once the cluster is named (step
+    // 4d). (Razor writes data- attributes even when false, so the value says it.)
+    document.querySelectorAll('dialog[data-open-on-load="true"]').forEach((dialog) => dialog.showModal());
 
     const form = document.querySelector('[data-record-editor]');
     if (!form) {
```

`web/Views/ClusterAdmin/_HandOverCard.cshtml`:

```cshtml
@using BatteryPassWeb.Controllers
@using BatteryPassWeb.Dpp.Pages
@model HandOverCardView
@* The "Hand over" card and window (step 4d, mock-up S2 and S8): the sender names the receiving cluster by its ID, sees its
   name, and asks; while it waits, the battery stays here and the sender can cancel. *@
@{
    // Read (not peeked), so they are gone on the next visit: the window opens only right after "Find the cluster".
    var typed = TempData[ClusterBatteriesController.HandOverToKey] as string;
    var name = TempData[ClusterBatteriesController.HandOverNameKey] as string;
    var found = typed is not null && name is not null && Model.Waiting is null;
}
<section class="bp-card bp-console-form-card bp-battery-card mt-4" id="handover">
    @if (Model.Waiting is { } waiting)
    {
        <span class="bp-summary-title">Hand over <em class="bp-field-marker">Not passport data</em></span>
        <h2>Waiting for @Model.ReceiverName</h2>
        <p class="mb-2">Asked @ValueWords.Time(waiting.AskedAtUtc). The battery stays with your cluster until they accept.</p>
        @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "handover")
        <form method="post" action="@Model.Action/cancel">
            @Html.AntiForgeryToken()
            <div class="bp-dialog-actions"><button type="submit" class="bp-secondary-button">Cancel the hand-over</button></div>
        </form>
    }
    else
    {
        <div class="bp-battery-card-head">
            <div>
                <span class="bp-summary-title">Hand over <em class="bp-field-marker">Not passport data</em></span>
                <h2>Sold or moved to another customer?</h2>
                <p class="mb-0">Hand the battery over to their cluster. It moves once their cluster admin accepts; your cluster then no longer sees its purchaser data. The passport doesn't change.</p>
            </div>
            <button type="button" class="bp-secondary-button" data-open-dialog="handover-dialog">Hand over…</button>
        </div>
        @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "handover")
        <dialog id="handover-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="handover-dialog-title" data-open-on-load="@(found ? "true" : "false")">
            <form method="post" action="@Model.Action" class="bp-dialog-form">
                @Html.AntiForgeryToken()
                <h2 id="handover-dialog-title">Hand @Model.SerialNumber over</h2>
                <p class="bp-help-text">Ask the receiving customer for their cluster ID.</p>
                <label><span>Receiving cluster ID</span><input type="text" name="toClusterId" value="@typed" autocomplete="off" required /></label>
                @if (found)
                {
                    <input type="hidden" name="checkedId" value="@typed" />
                    <p class="bp-done-note mb-0">@name</p>
                }
                <div class="bp-dialog-actions">
                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
                    <button type="submit" class="bp-primary-button">@(found ? "Ask them to accept" : "Find the cluster")</button>
                </div>
            </form>
        </dialog>
    }
</section>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~HandOverPagesTests | FullyQualifiedName~ClusterBatteriesControllerTests | FullyQualifiedName~ClusterBatteryPagesTests | FullyQualifiedName~RegistryListTests | FullyQualifiedName~BatteriesControllerTests"`
Expected: `Passed!  - Failed:     0, Passed:    52`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2231`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/HandOverPagesTests.cs web/Controllers/BatteriesController.cs web/Controllers/ClusterBatteriesController.cs web/Controllers/RegistryController.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Dpp/Pages/RegistryList.cs web/Views/Batteries/Battery.cshtml web/Views/ClusterAdmin/Battery.cshtml web/Views/ClusterAdmin/_HandOverCard.cshtml web/Views/Registry/Index.cshtml web/wwwroot/css/bp-product-data.css web/wwwroot/js/bp-record-editor.js
git commit -m "feat(dpp): the hand-over card and window, the Registry's waiting card and the team's cancel" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: A waiting hand-over in the demo data

The demo seed has DEMO-CORE-0001's cluster admin (demo.fleet.admin) hand it over to demo-north, left waiting (Q16): demo.north.admin sees it on the Registry. The demo counts don't change.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`
- Modify: `web/Dpp/Demo/DemoCatalogue.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Dpp/Demo/demo-catalogue.json`

- [ ] **Step 1: Write the failing tests**

Patch `4d-task3-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`, `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`, `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`). Save the block as `../4d-task3-tests.patch`, outside the worktree, and run `git apply ../4d-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs b/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
index 12d8463..24b9983 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
@@ -84,6 +84,16 @@ public sealed class DemoCatalogueTests
         Assert.Equal(3, Catalogue.Accounts.Count(account => account.ClusterRole == "clusterAdmin"));
     }
 
+    [Fact]
+    public void TheHandOverExample_ShouldGoFromOnePublishedClusterBatteryToAnother()
+    {
+        var handedOver = Assert.Single(Catalogue.Packs, pack => pack.HandOverTo is not null);
+
+        Assert.Equal(("DEMO-CORE-0001", "demo-fleet", "demo-north"), (handedOver.Serial, handedOver.Cluster, handedOver.HandOverTo));
+        Assert.True(handedOver.Publish);
+        Assert.Contains(Catalogue.Clusters, cluster => cluster.Id == handedOver.HandOverTo);
+    }
+
     [Fact]
     public void ThePictures_ShouldBeTheOldDemosPngFiles()
     {
diff --git a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
index 4db4329..653ea76 100644
--- a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
@@ -23,7 +23,7 @@ public sealed class DemoClearTests(MongoFixture mongo)
 
         var result = await rig.Demo.ClearAsync(DemoRig.Who);
 
-        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks", "users", "clusterMemberships", "clusters", "apiTokens" })
+        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks", "users", "clusterMemberships", "clusters", "apiTokens" })
         {
             Assert.True(await rig.Count(collection) == 0, $"{collection} still has records");
         }
diff --git a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
index b034d19..e7f74e7 100644
--- a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
@@ -123,6 +123,22 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.InRange((DateTime.UtcNow - times.Min()).TotalHours, 160, 168);
     }
 
+    [Fact]
+    public async Task TheHandOverExample_ShouldWaitForDemoNorthsAnswer()
+    {
+        using var rig = await DemoRig.StartAsync(mongo);
+
+        await rig.Demo.SeedAsync(DemoRig.Who);
+
+        var pack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-CORE-0001"))).Single();
+        var waiting = (await rig.All(DppStore.HandOvers)).Single();
+        Assert.Equal(pack["_id"].AsString, waiting["PackId"].AsString);
+        Assert.Equal(
+            ("demo-fleet", "demo-north", "waiting", "demo.fleet.admin@example.test"),
+            (waiting["FromClusterId"].AsString, waiting["ToClusterId"].AsString, waiting["State"].AsString, waiting["AskedBy"].AsString));
+        Assert.Equal("demo-fleet", pack["ClusterId"].AsString); // it stays with the sender until demo-north accepts
+    }
+
     [Fact]
     public async Task EveryRecordItMakes_ShouldBeMarkedAsDemoData()
     {
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo"`
Expected: the build fails with CS1061 (for example `error CS1061 'DemoPack' does not contain a definition for 'HandOverTo'`).

- [ ] **Step 3: Write the code**

Patch `4d-task3-code.patch` (`web/Dpp/Demo/DemoCatalogue.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/Demo/demo-catalogue.json`). Save the block as `../4d-task3-code.patch`, outside the worktree, and run `git apply ../4d-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Demo/DemoCatalogue.cs b/web/Dpp/Demo/DemoCatalogue.cs
index d6aad69..ae36474 100644
--- a/web/Dpp/Demo/DemoCatalogue.cs
+++ b/web/Dpp/Demo/DemoCatalogue.cs
@@ -33,7 +33,8 @@ public sealed record DemoSuccessor(string Warranty);
 public sealed record DemoMark(string? Invalid = null, bool Deletion = false);
 
 /// <summary>
-/// A demo battery. <see cref="Publish"/> publishes it; <see cref="NewVersion"/> then changes these values, which makes
+/// A demo battery. <see cref="Publish"/> publishes it; <see cref="HandOverTo"/> then has its cluster admin hand it over to
+/// that cluster, waiting for its answer (step 4d); <see cref="NewVersion"/> then changes these values, which makes
 /// version 2; <see cref="History"/> writes seven days of live values; <see cref="Modules"/> imports that many modules
 /// from an export-shaped file; <see cref="Accident"/> adds an accident report; <see cref="Status"/> then changes its
 /// status through the app's own status change.
@@ -50,7 +51,8 @@ public sealed record DemoPack(
     int Modules = 0,
     string? Accident = null,
     DemoStatus? Status = null,
-    DemoMark? Mark = null);
+    DemoMark? Mark = null,
+    string? HandOverTo = null);
 
 /// <summary>
 /// What the demo data seed makes (decision 32): the Compact and Core families of the old demo on the new model, with
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index c01bf79..a7919f1 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -36,6 +36,7 @@ public sealed class DemoDataService(
     StatusChangeService statusChanges,
     SuccessionService successions,
     PassportMarks marks,
+    HandOverService handOvers,
     DocumentService documents,
     DocumentStore documentStore,
     DppLinks links,
@@ -213,6 +214,11 @@ public sealed class DemoDataService(
             {
                 await MarkAsync(team, packId, demo, mark, catalogue, cancellationToken);
             }
+
+            if (demo.HandOverTo is { } receiver && demo.Publish)
+            {
+                await HandOverAsync(packId, demo, receiver, catalogue, cancellationToken);
+            }
         }
 
         demoTokens.AddRange(await SeedTokensAsync(who, catalogue, cancellationToken));
@@ -547,6 +553,21 @@ public sealed class DemoDataService(
         }
     }
 
+    /// <summary>
+    /// A demo battery handed over by its cluster's demo admin, as they would on its page, and left waiting for the
+    /// receiving cluster's answer (step 4d).
+    /// </summary>
+    private async Task HandOverAsync(string packId, DemoPack demo, string receiver, DemoCatalogue catalogue, CancellationToken cancellationToken)
+    {
+        var admin = catalogue.Accounts.FirstOrDefault(account => account.Cluster == demo.Cluster && account.ClusterRole == "clusterAdmin")?.Email
+            ?? throw new InvalidOperationException($"The demo cluster {demo.Cluster} has no admin to hand {demo.Serial} over.");
+        var asked = await handOvers.AskAsync(ChangeSender.ClusterAdmin(admin, demo.Cluster), packId, receiver, confirm: true, cancellationToken);
+        if (asked.Problem is { } problem)
+        {
+            throw new InvalidOperationException($"The demo battery {demo.Serial} couldn't be handed over: {problem}.");
+        }
+    }
+
     /// <summary>An export-shaped file (decision 31) with this many modules for the battery.</summary>
     private static byte[] ModuleExport(string serial, int modules)
     {
diff --git a/web/Dpp/Demo/demo-catalogue.json b/web/Dpp/Demo/demo-catalogue.json
index 4ab833a..c0894e2 100644
--- a/web/Dpp/Demo/demo-catalogue.json
+++ b/web/Dpp/Demo/demo-catalogue.json
@@ -1026,7 +1026,8 @@
       "publish": true,
       "history": true,
       "modules": 10,
-      "accident": "Collision during loading; the housing was inspected and found sound."
+      "accident": "Collision during loading; the housing was inspected and found sound.",
+      "handOverTo": "demo-north"
     },
     {
       "serial": "DEMO-CORE-0002",
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo"`
Expected: `Passed!  - Failed:     0, Passed:    42`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2233`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs BatteryPassWeb.Tests/Demo/DemoClearTests.cs BatteryPassWeb.Tests/Demo/DemoSeedTests.cs web/Dpp/Demo/DemoCatalogue.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/Demo/demo-catalogue.json
git commit -m "feat(dpp): a waiting hand-over in the demo data" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2233, Skipped:     0, Total:  2233`. From the 2212 before: 2223 after Task 1, 2231 after Task 2, 2233 after Task 3.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat e25d335 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. The managing session brings them to the product owner:

1. **Restart the app with the usual launcher**, sign in with a team account and press "Reset demo data" at the end of the Clusters tab.
2. **As demo-north's cluster admin** (`demo.north.admin@example.test`): the Registry starts with "Waiting for you": DEMO-CORE-0001 from Demo Fleet Operations. "Accept": the battery is in the list below with "Manage".
3. **As demo-fleet's cluster admin** (`demo.fleet.admin@example.test`), after a fresh "Reset demo data":
   - **DEMO-CORE-0001's page** ("Manage" on the Registry): the "Hand over" card says "Waiting for Demo North Operations", with "Cancel the hand-over". Cancel it.
   - **"Hand over…"** again: type `demo-south`, "Find the cluster" shows its name, then "Ask them to accept".
4. **As demo-south's cluster admin:** "Decline" on the Registry. DEMO-CORE-0001 stays with demo-fleet.
5. **As the team:** DEMO-CORE-0001's page shows a waiting hand-over beside the cluster with "Cancel it" (after another ask), and "Change" moves it directly.
6. **Light and dark:** the moon button in the top bar switches.

## Next

Step 4 is complete. Step 5 is the managing session's to plan.
