# DPP V3 Step 4a: The Cluster Admin's Battery Page — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A battery's cluster admins look after its purchaser data on their own page per battery (design 6.1 and 12, decision 34). The page is `/cluster-admin/batteries/{id}`, reached by "Manage" on their Registry rows and on the battery's detail page. On it they correct the live values (#63, #83, #87, E1–E4) and add accident reports (#92), and see its modules and versions. The status change (4b) and the hand-over (4d) add their cards to this page later.

**Architecture:**
- **Who edits:** `DppReaders.EditorForAsync` turns a sign-in into the sender of a change.
  - The team is the team.
  - A cluster admin acts only for the clusters where their membership says cluster admin. Someone who is admin of one cluster and a plain member of another edits nothing in the second.
  - Reading is unchanged: every membership still counts the same there.
- **The page's service:** `ClusterBatteryPages` opens a battery only when the sender is admin of its cluster and its passport is published and active (drafts stay the team's, J-6). Every save goes through the save pipeline as the cluster admin, which already allows exactly the live values and accident reports (catalogue editors, design 6.1).
- **One source per card:** the team's battery page's live values, modules, accident reports and versions cards become partial views (`Views/Batteries/_*Card.cshtml`) with one small model, `BatteryCards`. Both battery pages draw them. On the cluster admin's page the module list is read-only, and the words say "the team" instead of "your team".
- **The links:**
  - The Registry gets a "Manage" column for cluster admins, filled on their own clusters' rows only.
  - The detail page shows "Manage this battery" to the battery's cluster admins.
  - The help's "Your cluster" part gets one step about it.

No new menu entry, no duplicate page (decision 23), no change to `site.css`, no data change.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 6.1 and 12 and section 14's step 4 bullet. Decisions record: decisions 9, 23, 30 and 34. Mock-up approved by the product owner: `docs/mockups/dpp-v3-step4-purchaser-and-lifecycle.html` (committed with this plan; this plan builds screens S1's "Manage" link and S2 without its dashed cards).

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 39c2a95 (the end of plan 3e's run), plus the commit that adds this plan, its mock-up and decision 34. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo).
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  2098`. After this plan: 2129. The suite takes about two minutes. If a run seems stuck for more than 20 minutes, stop it (and its `testhost`) and run it again: while this plan was measured and replayed, a run hung twice (once with two suites running at once, once on its own), and the next run passed each time. Step 3e's measuring saw the same once, before any of this plan's code.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../4a-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New and rewritten files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (31 Oct 2026, through the managing session)

These are decision 34; the ones for 4b–4d are listed there.

1. **Step 4 is built in four parts**, each ending with a working app: 4a this page; 4b the status change and repairs; 4c successor passports, "invalid" and "marked for deletion"; 4d hand-overs.
2. **Cluster admins edit on their own page per battery**, `/cluster-admin/batteries/{id}`, built from the team page's cards with no duplicates. It opens from "Manage" on their Registry rows and on the detail page.
3. **The module list and the versions are shown there, read-only.**
4. **One mock-up for all of step 4**, approved once.

## Found while prototyping, and what this plan does

- **No cluster admin could change anything yet.** `ChangeSender.ClusterAdmin` existed only in tests, and the battery pages are the team's (`AdminOnly`). The catalogue already lets the battery's cluster admins change the live values and #92, so the save pipeline needs no change.
- **Reading merges memberships; editing must not.** `DppReaders.ForAsync` puts every cluster a person belongs to in one set and adds "cluster admin" if any membership is admin. That is right for reading, because users and admins see the same, but it would let an admin of A edit B's batteries. Task 1 adds `EditorForAsync`, which takes admin memberships only.
- **The team battery page's cards were written in its view.** Copying them would make two sources. Task 3 moves them into partials, and both pages use them.
- **"Your team" in the shared field note.** The "Not public" note under each value says "seen only by … your team", which reads wrong to a cluster admin. A flag on the editor view (`ForTeam`) makes it "the team" on their page.
- **The public hero names no cluster.** A test keeps the word "Cluster" out of `_PassportHero.cshtml`, so the hero reads the detail page's link as `ViewData["ManageHref"]`, and a test pins that `ClusterBatteryPages.ManageHrefKey` is that text.
- **Another cluster's battery is "not found",** like an unknown one, so the page doesn't tell anyone which batteries exist elsewhere. A plain cluster member is stopped at the door: the area is cluster admins' and the team's (`ClusterAdminOrAdmin`), and the team is sent to its own battery page.

## What happens to the old tests

Five layout tests read the team's battery page as text: `BatteriesControllerTests.TheViews_ShouldUseTheTeamPagesFinishAndTheirParts`, `DppControllerTests.TheTeamsPages_ShouldLinkTheFilesAndTheAccidentPage` and `EveryFileLink_ShouldOpenInANewTab("Batteries", …)`, `ModuleDataTests.ThePages_ShouldOfferTheModuleForms` and `ReviewFixesTests.TheTeamsBatteryPage_ShouldLinkItsPassportPagesQrCodeAndAuditEntries`. Task 3 points them at the page together with its card partials (`DppTestFiles.BatteryPageSource()`), and the module forms' action is now `@Model.Href/modules`. Nothing is removed; the count only goes up.

## A part to write yourself (optional)

The product owner chose "you write it" for these before; the plan gives the code. If they want to try one, the natural spot is the editing rule in Task 1, `DppReaders.EditorForAsync`: which memberships give editing rights. The tests of Task 1 say what it must do.

---

### Task 1: Who edits as a cluster admin

A sign-in becomes the sender of a change: the team, or a cluster admin of only the clusters where the membership says cluster admin. Reading stays as it is. Nothing uses it yet; Task 2 does.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/ClusterAdminSenderTests.cs`
- Modify: `web/Dpp/Access/DppReaders.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/ClusterAdminSenderTests.cs`:

```csharp
using System.Security.Claims;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// A sign-in as the sender of a change (design 6.1, step 4a): the team, or a cluster admin of only the clusters where
/// the membership says cluster admin. Reading treats every membership alike; editing doesn't.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterAdminSenderTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheTeam_ShouldEditAsTheTeam()
    {
        var sender = await Readers().EditorForAsync(User("Admin@Example.com", "admin"));

        Assert.NotNull(sender);
        Assert.Equal(SenderKind.Team, sender.Kind);
        Assert.Equal("admin@example.com", sender.Who);
    }

    [Fact]
    public async Task AClusterAdmin_ShouldEditOnlyTheClustersTheyAreAdminOf()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("both@example.com", "Cluster-North", "clusterAdmin");
        await clusters.UpsertClusterMembershipAsync("both@example.com", "cluster-south", "member");

        var sender = await new DppReaders(clusters).EditorForAsync(User("both@example.com", "clusterAdmin"));

        Assert.NotNull(sender);
        Assert.Equal(SenderKind.ClusterAdmin, sender.Kind);
        Assert.Equal(["cluster-north"], sender.ClusterIds);
    }

    [Fact]
    public async Task APlainMember_ShouldEditNothing()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("member@example.com", "cluster-north", "member");

        Assert.Null(await new DppReaders(clusters).EditorForAsync(User("member@example.com", "member")));
    }

    [Theory]
    [InlineData("marketSurveillanceAuthority")]
    [InlineData("liRepair")]
    [InlineData("commission")]
    public async Task AnAppWideReader_ShouldEditNothing(string role)
    {
        Assert.Null(await Readers().EditorForAsync(User("reader@example.com", role)));
    }

    [Fact]
    public async Task NotSignedIn_ShouldEditNothing()
    {
        Assert.Null(await Readers().EditorForAsync(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public async Task ARemovedAdminMembership_ShouldCountAtOnce()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("admin@example.com", "cluster-north", "clusterAdmin");
        var readers = new DppReaders(clusters);
        Assert.NotNull(await readers.EditorForAsync(User("admin@example.com", "clusterAdmin")));

        await clusters.DeleteClusterMembershipAsync("admin@example.com", "cluster-north");

        Assert.Null(await readers.EditorForAsync(User("admin@example.com", "clusterAdmin")));
    }

    private DppReaders Readers() => new(new ClusterRepository(mongo.NewContext()));

    private static ClaimsPrincipal User(string email, string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test"));
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ClusterAdminSenderTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'DppReaders' does not contain a definition for 'EditorForAsync'`).

- [ ] **Step 3: Write the code**

Patch `4a-task1-code.patch` (`web/Dpp/Access/DppReaders.cs`). Save the block as `../4a-task1-code.patch`, outside the worktree, and run `git apply ../4a-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Access/DppReaders.cs b/web/Dpp/Access/DppReaders.cs
index a9a90d6..3b2b3c3 100644
--- a/web/Dpp/Access/DppReaders.cs
+++ b/web/Dpp/Access/DppReaders.cs
@@ -57,6 +57,35 @@ public sealed class DppReaders(ClusterRepository clusters)
         return new Reader(roles, clusterIds, email);
     }
 
+    /// <summary>
+    /// The signed-in user as the sender of a change (design 6.1, step 4a): the team for an admin; otherwise a cluster
+    /// admin of only the clusters where their membership is cluster admin, since a plain member of a cluster changes
+    /// nothing there. Null for anyone who changes nothing. Read from the memberships on every request, like
+    /// <see cref="ForAsync"/>.
+    /// </summary>
+    public async Task<ChangeSender?> EditorForAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(user);
+        var email = AccessControlService.CurrentEmail(user).Trim().ToLowerInvariant();
+        if (user.Identity?.IsAuthenticated != true || email.Length == 0)
+        {
+            return null;
+        }
+
+        if (user.FindAll(ClaimTypes.Role).Any(claim => claim.Value.Equals(AccessControlService.RoleAdmin, StringComparison.OrdinalIgnoreCase)))
+        {
+            return ChangeSender.Team(email);
+        }
+
+        var adminOf = (await clusters.GetClusterMembershipsForUserAsync(email, cancellationToken))
+            .Where(membership => BsonHelpers.GetString(membership, "role").Equals(AccessControlService.RoleClusterAdmin, StringComparison.OrdinalIgnoreCase))
+            .Select(membership => BsonHelpers.GetString(membership, "clusterId"))
+            .Where(clusterId => clusterId.Length > 0)
+            .Select(ClusterRepository.NormalizeClusterId)
+            .ToArray();
+        return adminOf.Length == 0 ? null : ChangeSender.ClusterAdmin(email, adminOf);
+    }
+
     /// <summary>
     /// An API token as a reader (design 5.2). A token for named clusters reads like a cluster user of those clusters. A
     /// global token is the team's token, issued only for Scania's own systems, so it reads like the team.
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ClusterAdminSenderTests"`
Expected: `Passed!  - Failed:     0, Passed:     8`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2106`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/ClusterAdminSenderTests.cs web/Dpp/Access/DppReaders.cs
git commit -m "feat(dpp): a sign-in edits as a cluster admin only where its membership says so" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The cluster admin's battery, its live values and accident reports

The page's service, before the page: it opens a published battery of a cluster the sender is admin of, and saves its live values and accident reports as the cluster admin, through the save pipeline. The editor view gets a `ForTeam` flag and the field a `SeenByOutsideTheTeam` wording, which Task 3's views use. `BatteryPages`' two small helpers become `internal` so the new service reuses them.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Create: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Modify: `web/Dpp/Pages/ProductDataPages.cs`
- Modify: `web/Dpp/Pages/RecordEditor.cs`

- [ ] **Step 1: Write the failing tests**

Patch `4a-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`). Save the block as `../4a-task2-tests.patch`, outside the worktree, and run `git apply ../4a-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
index 6e28280..9b08cd1 100644
--- a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
@@ -40,6 +40,7 @@ public sealed class DppServiceWiringTests
         Assert.NotNull(provider.GetRequiredService<RecordEditor>());
         Assert.Same(provider.GetRequiredService<AttributeForms>(), provider.GetRequiredService<RecordEditor>().Forms);
         Assert.NotNull(provider.GetRequiredService<BatteryPages>());
+        Assert.NotNull(provider.GetRequiredService<ClusterBatteryPages>());
         Assert.NotNull(provider.GetRequiredService<DppReaders>());
         Assert.NotNull(provider.GetRequiredService<ReaderPages>());
         Assert.NotNull(provider.GetRequiredService<PassportApi>());
```

`BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The cluster admin's battery page (design 6.1 and 12, step 4a): only for a published battery of a cluster the
/// sender is admin of; live values and accident reports saved as the cluster admin; modules and versions shown.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterBatteryPagesTests(MongoFixture mongo)
{
    private const string Admin = "north-admin@example.com";

    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin(Admin, BatteryTesting.North);

    private static readonly ChangeSender SouthAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

    [Fact]
    public async Task TheBatterysClusterAdmin_ShouldOpenItsPage()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = await Pages(rig).BatteryAsync(NorthAdmin, rig.PackId);

        Assert.NotNull(page);
        Assert.Equal(["#63", "#83", "#87", "E1", "E2", "E3", "E4"], page.Live.Sections.SelectMany(section => section.Fields).Select(field => field.Attribute.Label));
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}/live", page.Live.SaveAction);
        Assert.False(page.Live.ForTeam); // its fields say "the team", not "your team"
        Assert.All(page.Live.Sections.SelectMany(section => section.Fields), field => Assert.EndsWith("market surveillance and the team", field.SeenByOutsideTheTeam, StringComparison.Ordinal));
        Assert.Equal("Customer North", page.ClusterLabel);
        Assert.True(Assert.Single(page.Versions).SignatureHolds);
        Assert.Empty(page.AccidentReports);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotOpenIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Null(await Pages(rig).BatteryAsync(SouthAdmin, rig.PackId));
    }

    [Fact]
    public async Task ADraft_ShouldStayWithTheTeam()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        Assert.Null(await Pages(rig).BatteryAsync(NorthAdmin, rig.PackId));
    }

    [Fact]
    public async Task AnUnknownBattery_ShouldNotOpen()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Null(await Pages(rig).BatteryAsync(NorthAdmin, "pack-unknown"));
    }

    [Fact]
    public async Task LiveValues_ShouldBeSavedAsTheClusterAdminWithoutANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("stateOfChargeSoC")] = "42";
        var measured = new DateTime(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);

        var (_, save) = (await Pages(rig).SaveLiveAsync(NorthAdmin, rig.PackId, PageTesting.Field(form), PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null), measured))!.Value;

        Assert.Equal(EditorOutcome.Saved, save.Outcome);
        var soc = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"];
        Assert.Equal(42, soc.Value["percentageValue"]!.GetValue<int>());
        Assert.Equal(Admin, soc.Who);
        Assert.Equal(measured, soc.GeneratedAtUtc);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
        var entry = await LastChangeAsync(rig);
        Assert.Equal(Admin, entry["actor"].AsString);
        Assert.Equal(nameof(SenderKind.ClusterAdmin), entry["actorRole"].AsString);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotSaveLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("stateOfChargeSoC")] = "42";

        Assert.Null(await Pages(rig).SaveLiveAsync(SouthAdmin, rig.PackId, PageTesting.Field(form), PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null), null));
        Assert.Equal(stored["stateOfChargeSoC"].Value!.ToJsonString(), (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!.ToJsonString());
    }

    [Fact]
    public async Task AnAccidentReport_ShouldBeAddedByTheBatterysClusterAdmin()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = Pages(rig);

        var result = await pages.AddAccidentReportAsync(NorthAdmin, rig.PackId, new DocumentUpload("fall.pdf", DocumentTesting.Pdf("fall"), "Dropped in the yard"));

        Assert.NotNull(result);
        Assert.Null(result.Problem);
        var report = Assert.Single((await pages.BatteryAsync(NorthAdmin, rig.PackId))!.AccidentReports);
        Assert.Equal("Dropped in the yard", report.Title);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotAddAnAccidentReport()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Null(await Pages(rig).AddAccidentReportAsync(SouthAdmin, rig.PackId, new DocumentUpload("fall.pdf", DocumentTesting.Pdf())));
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Pack, rig.PackId, DocumentService.Accidents));
    }

    [Fact]
    public async Task ABatteryMovedAway_ShouldCloseAtOnce()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetPackClusterAsync(rig.PackId, BatteryTesting.South);

        Assert.Null(await Pages(rig).BatteryAsync(NorthAdmin, rig.PackId));
        Assert.NotNull(await Pages(rig).BatteryAsync(SouthAdmin, rig.PackId));
    }

    internal static ClusterBatteryPages Pages(PublicationRig rig) => new(
        rig.Store,
        PageTesting.Editor(rig),
        PageTesting.Documents(rig),
        new DocumentStore(rig.Context),
        new ClusterRepository(rig.Context));

    private static async Task<BsonDocument> LastChangeAsync(PublicationRig rig) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", SavePipeline.AuditEventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ClusterBatteryPagesTests | FullyQualifiedName~DppServiceWiringTests"`
Expected: the build fails with CS0246 (for example `error CS0246 The type or namespace name 'ClusterBatteryPages' could not be found`).

- [ ] **Step 3: Write the code**

Patch `4a-task2-code.patch` (`web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ProductDataPages.cs`, `web/Dpp/Pages/RecordEditor.cs`). Save the block as `../4a-task2-code.patch`, outside the worktree, and run `git apply ../4a-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index 6790d14..ae193f8 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -44,6 +44,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<RecordEditor>();
         services.AddSingleton<ProductDataPages>();
         services.AddSingleton<BatteryPages>();
+        services.AddSingleton<ClusterBatteryPages>();
         services.AddSingleton<DppReaders>();
         services.AddSingleton<ReaderPages>();
         services.AddSingleton<PassportApi>();
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index d4c66dd..86218fb 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -595,9 +595,9 @@ public sealed class BatteryPages(
         }
     }
 
-    private static string ClusterLabel(string? clusterId, IReadOnlyDictionary<string, string> names) =>
+    internal static string ClusterLabel(string? clusterId, IReadOnlyDictionary<string, string> names) =>
         clusterId is null ? "No cluster yet" : names.TryGetValue(clusterId, out var name) && name.Length > 0 ? name : clusterId;
 
-    private static string? Text(BatteryPack pack, string property) =>
+    internal static string? Text(BatteryPack pack, string property) =>
         pack.Values.GetValueOrDefault(property)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
 }
diff --git a/web/Dpp/Pages/ProductDataPages.cs b/web/Dpp/Pages/ProductDataPages.cs
index db76dcd..3a2d0e9 100644
--- a/web/Dpp/Pages/ProductDataPages.cs
+++ b/web/Dpp/Pages/ProductDataPages.cs
@@ -39,6 +39,9 @@ public sealed record RecordEditorView(
     string Stamp,
     RemovalView Removal)
 {
+    /// <summary>Shown to the team (the default); false on the cluster admin's battery page (step 4a), which says "the team".</summary>
+    public bool ForTeam { get; init; } = true;
+
     public string UploadAction(string property) => $"{SaveAction}/documents/{Uri.EscapeDataString(property)}";
 
     public string KindLabel => Kind switch
diff --git a/web/Dpp/Pages/RecordEditor.cs b/web/Dpp/Pages/RecordEditor.cs
index 5f4baf4..33b4760 100644
--- a/web/Dpp/Pages/RecordEditor.cs
+++ b/web/Dpp/Pages/RecordEditor.cs
@@ -111,6 +111,9 @@ public sealed record EditorField(
         _ => "everyone",
     };
 
+    /// <summary><see cref="SeenBy"/> in the words of a reader outside the team, e.g. a cluster admin (step 4a).</summary>
+    public string SeenByOutsideTheTeam => SeenBy.Replace("your team", "the team", StringComparison.Ordinal);
+
     private string SharedGroups()
     {
         var groups = new List<string>();
```

`web/Dpp/Pages/ClusterBatteryPages.cs`:

```csharp
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>A battery as its cluster admins manage it: its live values to type, its accident reports, modules and versions.</summary>
public sealed record ClusterBatteryPage(
    BatteryPack Pack,
    PassportSources Sources,
    BatteryFamily Family,
    string SerialNumber,
    string Identifier,
    string ClusterLabel,
    RecordEditorView Live,
    IReadOnlyList<VersionRow> Versions,
    IReadOnlyList<DppDocument> AccidentReports)
{
    public string BatchLabel => $"{Sources.Batch.Year} · {Sources.Site.Name}";

    public string Href => ClusterBatteryPages.BatteryHref(Pack.Id);
}

/// <summary>
/// The cluster admin's battery page (design 6.1 and 12, step 4a). A cluster admin manages only a battery of a cluster
/// they are admin of, and only once its passport is published and active: a draft is the team's (J-6). Every change
/// goes through the save pipeline as the cluster admin, which allows the live values and accident reports (design 6.1).
/// </summary>
public sealed class ClusterBatteryPages(
    DppStore store,
    RecordEditor editor,
    DocumentService documents,
    DocumentStore documentStore,
    ClusterRepository clusters)
{
    public static string BatteryHref(string packId) => $"/cluster-admin/batteries/{Uri.EscapeDataString(packId)}";

    /// <summary>Where the detail page finds the "Manage this battery" link, for the battery's cluster admins only.</summary>
    public const string ManageHrefKey = "ManageHref";

    /// <summary>The battery's page, when the sender may manage it; null otherwise.</summary>
    public async Task<string?> ManageHrefAsync(ChangeSender? sender, string packId, CancellationToken cancellationToken = default) =>
        sender is not null && await ManagedAsync(sender, packId, cancellationToken) is not null ? BatteryHref(packId) : null;

    /// <summary>The battery's page; with <paramref name="live"/>, the live form as typed after a refused save. Null when the sender may not manage it.</summary>
    public async Task<ClusterBatteryPage?> BatteryAsync(ChangeSender sender, string packId, EditorState? live = null, CancellationToken cancellationToken = default)
    {
        if (await ManagedAsync(sender, packId, cancellationToken) is not { } pack)
        {
            return null;
        }

        var sources = await store.LoadSourcesAsync(pack.Id, cancellationToken);
        var family = sources is null ? null : await store.GetAsync<BatteryFamily>(sources.Model.FamilyId, cancellationToken);
        if (sources is null || family is null)
        {
            return null;
        }

        var href = BatteryHref(pack.Id);
        var serial = BatteryPages.Text(pack, "batterySerialNumber") ?? pack.Id;
        var liveView = new RecordEditorView(
            TargetKind.Pack,
            pack.Id,
            serial,
            $"{href}/live",
            $"{href}#live",
            await editor.SectionsAsync(TargetKind.Pack, pack.Id, RecordEditor.LiveAttributes, pack.Values, live?.Post, live?.Save.Refusals(editor.Forms), cancellationToken),
            [],
            0,
            editor.Forms.Languages,
            editor.Stamp(RecordEditor.LiveAttributes, pack.Values, null),
            new RemovalView(string.Empty, null))
        {
            ForTeam = false,
        };
        var names = (await clusters.ListClustersAsync(cancellationToken)).ToDictionary(
            cluster => BsonHelpers.GetString(cluster, "clusterId"),
            cluster => BsonHelpers.GetString(cluster, "name"),
            StringComparer.OrdinalIgnoreCase);
        var versions = await store.ListVersionsAsync(pack.Id, cancellationToken);
        return new ClusterBatteryPage(
            pack,
            sources,
            family,
            serial,
            BatteryPages.Text(pack, "uniqueProductIdentifier") ?? string.Empty,
            BatteryPages.ClusterLabel(pack.ClusterId, names),
            liveView,
            versions.OrderByDescending(version => version.Number).Select(version => new VersionRow(version, PassportReader.Verify(version))).ToArray(),
            await documentStore.ListAsync(TargetKind.Pack, pack.Id, DocumentService.Accidents, cancellationToken));
    }

    /// <summary>Saves the live values typed on the page, as the cluster admin; null when the sender may not manage the battery.</summary>
    public async Task<(EditorPost Post, EditorSave Save)?> SaveLiveAsync(
        ChangeSender sender,
        string packId,
        Func<string, string?> field,
        string? stamp,
        DateTime? measuredAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (await ManagedAsync(sender, packId, cancellationToken) is not { } pack)
        {
            return null;
        }

        var post = editor.Read(RecordEditor.LiveAttributes, pack.Values, field, stamp) with { MeasuredAtUtc = measuredAtUtc };
        return (post, await editor.SaveAsync(sender, TargetKind.Pack, pack.Id, post, confirmed: true, cancellationToken));
    }

    /// <summary>Adds an accident report (#92), as the cluster admin; null when the sender may not manage the battery.</summary>
    public async Task<DocumentUploadResult?> AddAccidentReportAsync(ChangeSender sender, string packId, DocumentUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        return await ManagedAsync(sender, packId, cancellationToken) is { } pack
            ? await documents.AddAccidentReportAsync(sender, pack.Id, upload, cancellationToken)
            : null;
    }

    /// <summary>The pack, when it belongs to a cluster the sender is admin of and its passport is published and active.</summary>
    private async Task<BatteryPack?> ManagedAsync(ChangeSender sender, string packId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.ClusterAdmin
            || await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { ClusterId: { } clusterId } pack
            || !sender.ClusterIds.Contains(ClusterRepository.NormalizeClusterId(clusterId)))
        {
            return null;
        }

        return await store.GetActiveVersionAsync(pack.Id, cancellationToken) is null ? null : pack;
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ClusterBatteryPagesTests | FullyQualifiedName~DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:    11`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2115`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/ClusterBatteryPagesTests.cs BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Dpp/Pages/ProductDataPages.cs web/Dpp/Pages/RecordEditor.cs
git commit -m "feat(dpp): the cluster admin's battery, its live values and accident reports" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The battery page's cards as shared partials

The team's battery page draws its live values, modules, accident reports and versions cards from partial views with one model, `BatteryCards`, so the cluster admin's page can draw the same ones in Task 4. The team's page looks and works as before. The card message line becomes a partial too, and the field note says "the team" when the editor view isn't the team's. The five layout tests that read the page as text now read it with its partials.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppControllerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppTestFiles.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs`
- Create: `web/Dpp/Pages/BatteryCards.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Create: `web/Views/Batteries/_AccidentsCard.cshtml`
- Create: `web/Views/Batteries/_CardMessages.cshtml`
- Create: `web/Views/Batteries/_LiveValuesCard.cshtml`
- Create: `web/Views/Batteries/_ModulesCard.cshtml`
- Create: `web/Views/Batteries/_VersionsCard.cshtml`
- Modify: `web/Views/ProductData/_EditorField.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `4a-task3-tests.patch` (`BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/DppControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/DppTestFiles.cs`, `BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs`, `BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs`). Save the block as `../4a-task3-tests.patch`, outside the worktree, and run `git apply ../4a-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
index 74e4e54..cd648a4 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs
@@ -172,7 +172,7 @@ public sealed class BatteriesControllerTests(MongoFixture mongo)
     public void TheViews_ShouldUseTheTeamPagesFinishAndTheirParts()
     {
         var list = View("Batteries");
-        var battery = View("Battery");
+        var battery = DppTestFiles.BatteryPageSource();
         var create = View("NewBattery");
         var editor = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "_RecordEditor.cshtml"));
 
diff --git a/BatteryPassWeb.Tests/Dpp/DppControllerTests.cs b/BatteryPassWeb.Tests/Dpp/DppControllerTests.cs
index 18f49e8..82b07cf 100644
--- a/BatteryPassWeb.Tests/Dpp/DppControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppControllerTests.cs
@@ -146,7 +146,7 @@ public sealed class DppControllerTests(MongoFixture mongo)
     [Fact]
     public void TheTeamsPages_ShouldLinkTheFilesAndTheAccidentPage()
     {
-        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
+        var battery = DppTestFiles.BatteryPageSource();
         var field = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "_EditorField.cshtml"));
 
         Assert.Contains("ReaderPages.AccidentsHref(Model.Pack.Id)", battery, StringComparison.Ordinal);
@@ -156,7 +156,7 @@ public sealed class DppControllerTests(MongoFixture mongo)
 
     /// <summary>A file opens in a new tab, so the page it was opened from stays where it was (product owner, 29 Oct 2026).</summary>
     [Theory]
-    [InlineData("Batteries", "Battery.cshtml")]
+    [InlineData("Batteries", "_AccidentsCard.cshtml")]
     [InlineData("Dpp", "Accidents.cshtml")]
     [InlineData("ProductData", "_EditorField.cshtml")]
     public void EveryFileLink_ShouldOpenInANewTab(string folder, string file)
diff --git a/BatteryPassWeb.Tests/Dpp/DppTestFiles.cs b/BatteryPassWeb.Tests/Dpp/DppTestFiles.cs
index b5b36c0..da5272f 100644
--- a/BatteryPassWeb.Tests/Dpp/DppTestFiles.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppTestFiles.cs
@@ -19,4 +19,12 @@ internal static class DppTestFiles
 
         throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
     }
+
+    /// <summary>The cards the team's battery page shares with the cluster admin's (step 4a), one partial view each.</summary>
+    public static readonly string[] BatteryCardViews = ["_LiveValuesCard", "_ModulesCard", "_AccidentsCard", "_VersionsCard"];
+
+    /// <summary>The team's battery page as it is drawn: Battery.cshtml and the shared card partials it renders.</summary>
+    public static string BatteryPageSource() => string.Join(
+        Environment.NewLine,
+        new[] { "Battery" }.Concat(BatteryCardViews).Select(name => File.ReadAllText(RepoFile("web", "Views", "Batteries", $"{name}.cshtml"))));
 }
diff --git a/BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs b/BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs
index 7f36f13..d473156 100644
--- a/BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs
@@ -150,12 +150,12 @@ public sealed class ModuleDataTests(MongoFixture mongo)
     [Fact]
     public void ThePages_ShouldOfferTheModuleForms()
     {
-        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
+        var battery = DppTestFiles.BatteryPageSource();
         var family = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "Family.cshtml"));
         var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
 
-        Assert.Contains("action=\"@href/modules/from-export\"", battery);
-        Assert.Contains("action=\"@href/modules\"", battery);
+        Assert.Contains("action=\"@Model.Href/modules/from-export\"", battery);
+        Assert.Contains("action=\"@Model.Href/modules\"", battery);
         Assert.Contains("action=\"/admin/models/@selected.Id/module-description\"", family);
         Assert.Contains("Model.ModuleCount", hero);
     }
diff --git a/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs b/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs
index 8decd62..b5394b7 100644
--- a/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs
@@ -57,7 +57,7 @@ public sealed class ReviewFixesTests
     [Fact]
     public void TheTeamsBatteryPage_ShouldLinkItsPassportPagesQrCodeAndAuditEntries()
     {
-        var page = Read("web", "Views", "Batteries", "Battery.cshtml");
+        var page = DppTestFiles.BatteryPageSource();
 
         Assert.Contains("href=\"/dpp/@batteryKey\"", page);
         Assert.Contains("href=\"/dpp/@batteryKey/detail\"", page);
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~DppControllerTests | FullyQualifiedName~ModuleDataTests | FullyQualifiedName~ReviewFixesTests"`
Expected: `Failed:     5, Passed:    38` (failing: `BatteriesControllerTests.TheViews_ShouldUseTheTeamPagesFinishAndTheirParts`, `DppControllerTests.TheTeamsPages_ShouldLinkTheFilesAndTheAccidentPage`, `ModuleDataTests.ThePages_ShouldOfferTheModuleForms`, `ReviewFixesTests.TheTeamsBatteryPage_ShouldLinkItsPassportPagesQrCodeAndAuditEntries`, `DppControllerTests.EveryFileLink_ShouldOpenInANewTab` for the battery page's file).

- [ ] **Step 3: Write the code**

Patch `4a-task3-code.patch` (`web/Dpp/Pages/BatteryPages.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/ProductData/_EditorField.cshtml`). Save the block as `../4a-task3-code.patch`, outside the worktree, and run `git apply ../4a-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 86218fb..3c8d308 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -89,6 +89,18 @@ public sealed record BatteryPage(
     RemovalView Removal)
 {
     public string BatchLabel => $"{Sources.Batch.Year} · {Sources.Site.Name}";
+
+    /// <summary>The cards this page shares with the cluster admin's battery page (step 4a).</summary>
+    public BatteryCards Cards => new(
+        Pack,
+        BatteryPages.BatteryHref(Pack.Id),
+        Live,
+        AccidentReports,
+        Versions,
+        Values.Languages,
+        ForTeam: true,
+        $"{ProductDataPages.FamilyHref(Sources.Model.FamilyId, Sources.Model.Id)}#modules",
+        !string.IsNullOrWhiteSpace(Sources.Model.ModuleDescription));
 }
 
 /// <summary>
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 0ac0dc4..ae85855 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -32,54 +32,13 @@
         return string.Join(" ", parts);
     }
     var card = (TempData[BatteriesController.CardKey] ?? ViewData[BatteriesController.CardKey]) as string;
-    var cardStatus = (TempData[BatteriesController.CardStatusKey] ?? ViewData[BatteriesController.CardStatusKey]) as string;
-    var cardError = (TempData[BatteriesController.CardErrorKey] ?? ViewData[BatteriesController.CardErrorKey]) as string;
-    var cardInfo = (TempData[BatteriesController.CardInfoKey] ?? ViewData[BatteriesController.CardInfoKey]) as string;
     var testIds = Identifiers.IsTestIdentifier(Model.Identifier) || Model.Versions.Any(row => Identifiers.IsTestIdentifier(row.Version.DigitalProductPassportId));
-    var liveFields = Model.Live.Sections.SelectMany(section => section.Fields).ToArray();
     var filesChanged = publication.Files.Count(file => !file.Unchanged);
 
     async Task CardMessagesAsync(string name)
     {
-        if (card != name)
-        {
-            return;
-        }
-
-        if (!string.IsNullOrWhiteSpace(cardStatus))
-        {
-            <p class="alert alert-success bp-status-line is-done" role="status">
-                <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3.5 8.5l3 3 6-7" /></svg>
-                <span>@cardStatus</span>
-            </p>
-        }
-        if (!string.IsNullOrWhiteSpace(cardInfo))
-        {
-            <p class="alert alert-secondary bp-status-line is-info" role="status"><span>@cardInfo</span></p>
-        }
-        if (!string.IsNullOrWhiteSpace(cardError))
-        {
-            <p class="alert alert-danger bp-status-line is-problem" role="alert">
-                <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 4.5v4.5M8 11.5v.5" /></svg>
-                <span>@cardError</span>
-            </p>
-        }
-        await Task.CompletedTask;
+        @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", name)
     }
-
-    static string Size(long bytes) => bytes < 1024
-        ? $"{bytes} bytes"
-        : bytes < 1048576 ? $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB" : $"{(bytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture)} MB";
-
-    static string Source(string? source) => source switch
-    {
-        "form" => "typed on this page",
-        "upload" => "from an upload",
-        "feed" => "from the battery system",
-        "system" => "set by the app",
-        "seed" => "test data",
-        _ => source ?? "not recorded",
-    };
 }
 
 <main class="bp-page bp-admin-page">
@@ -317,200 +276,10 @@
         <p class="bp-battery-fixed-note">The serial number, manufacturing date and warranty are fixed now that the passport is published; they're shown at the top.</p>
     }
 
-    <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="live">
-        <span class="bp-summary-title">Live values</span>
-        <h2>Measured on the battery</h2>
-        <p class="mb-0">
-            Its battery system sends these; type them here before it does, for example the end-of-line values, or to
-            correct one. They change the passport in place, without a new version.
-        </p>
-        @{ await CardMessagesAsync("live"); }
-        <form method="post" action="@Model.Live.SaveAction" class="bp-live-form">
-            @Html.AntiForgeryToken()
-            <input type="hidden" name="@ProductDataController.StampField" value="@Model.Live.Stamp" />
-            <div class="bp-attr-list">
-                @foreach (var field in liveFields)
-                {
-                    @await Html.PartialAsync("~/Views/ProductData/_EditorField.cshtml", new EditorFieldView(Model.Live, field))
-                    @if (Model.Pack.Values.GetValueOrDefault(field.Attribute.Property) is { } stored)
-                    {
-                        <p class="bp-live-meta">Last value @When(stored.GeneratedAtUtc), @Source(stored.Source)@(string.IsNullOrEmpty(stored.Who) ? string.Empty : $" by {stored.Who}").</p>
-                    }
-                }
-            </div>
-            <div class="bp-live-save">
-                <label>
-                    <span>Measured at <span class="bp-attr-optional">(optional; now if empty)</span></span>
-                    <input type="datetime-local" name="@BatteriesController.MeasuredAtField" />
-                </label>
-                <button type="submit" class="bp-primary-button">Save live values</button>
-            </div>
-        </form>
-    </section>
-
-    <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="modules">
-        <span class="bp-summary-title">Modules <em class="bp-field-marker">Not passport data</em></span>
-        <h2>@(Model.Pack.Modules.Count == 0 ? "No modules listed" : Words.Count(Model.Pack.Modules.Count, "module"))</h2>
-        <p class="mb-0">
-            The public summary shows how many modules the battery has, with the model's
-            <a href="@ProductDataPages.FamilyHref(sources.Model.FamilyId, sources.Model.Id)#modules">module description</a>@(string.IsNullOrWhiteSpace(sources.Model.ModuleDescription) ? " (not typed yet)" : string.Empty).
-            The module IDs are seen only by the battery's cluster, market surveillance and your team.
-        </p>
-        @{ await CardMessagesAsync("modules"); }
-        @if (Model.Pack.Modules.Count > 0)
-        {
-            <div class="bp-table-scroll">
-                <table class="table align-middle bp-console-table bp-mobile-card-table bp-module-table mb-0">
-                    <thead><tr><th>Position</th><th>Serial number</th><th>Connectivity ID</th></tr></thead>
-                    <tbody>
-                        @foreach (var module in Model.Pack.Modules)
-                        {
-                            <tr>
-                                <td data-label="Position">@module.Position</td>
-                                <td data-label="Serial number">@(module.SerialNumber ?? "—")</td>
-                                <td data-label="Connectivity ID">@(module.ConnectivityId ?? "—")</td>
-                            </tr>
-                        }
-                    </tbody>
-                </table>
-            </div>
-        }
-        <form method="post" action="@href/modules/from-export" enctype="multipart/form-data" class="bp-attr-upload bp-module-upload">
-            @Html.AntiForgeryToken()
-            <div class="bp-attr-file">
-                <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
-                    Choose file
-                    <input type="file" name="file" class="bp-document-hidden-file" accept="application/json,.json" data-upload-file />
-                </label>
-                <span class="bp-help-text" data-upload-chosen>No file chosen</span>
-            </div>
-            <div class="bp-live-save">
-                <small class="bp-field-help">The battery system's JSON export of this battery. It replaces the list.</small>
-                <button type="submit" class="bp-secondary-button" data-upload-button>Fill from export</button>
-            </div>
-        </form>
-        <details class="bp-module-edit" open="@(card == "module-list")">
-            <summary>Type or correct the list</summary>
-            @{ await CardMessagesAsync("module-list"); }
-            <form method="post" action="@href/modules">
-                @Html.AntiForgeryToken()
-                <label>
-                    <span>One module per line: position; serial number; connectivity ID. One of the IDs may be empty. An empty list clears it.</span>
-                    <textarea name="modules" rows="@Math.Clamp(Model.Pack.Modules.Count + 2, 4, 14)" spellcheck="false" placeholder="1; ; MD-A9ASVW8">@ModuleList.ToText(Model.Pack.Modules)</textarea>
-                </label>
-                <div class="bp-live-save">
-                    <span></span>
-                    <button type="submit" class="bp-secondary-button">Save modules</button>
-                </div>
-            </form>
-        </details>
-    </section>
-
-    <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="accidents">
-        <span class="bp-summary-title">Accident reports <span class="bp-attr-number">#92</span></span>
-        <h2>@(Model.AccidentReports.Count == 0 ? "No accidents reported" : Words.Count(Model.AccidentReports.Count, "accident report"))</h2>
-        <p class="mb-0">The passport links to the battery's <a href="@ReaderPages.AccidentsHref(Model.Pack.Id)">accident page</a>, which lists these reports or says none were reported. Seen only by the battery's cluster, market surveillance and your team.</p>
-        @{ await CardMessagesAsync("accidents"); }
-        @if (Model.AccidentReports.Count > 0)
-        {
-            <ul class="bp-file-checks">
-                @foreach (var report in Model.AccidentReports)
-                {
-                    <li>
-                        <span><a href="@ReaderPages.DocumentHref(report.Id)" target="_blank" rel="noreferrer"><strong>@report.Title</strong></a> · @report.FileName · @Size(report.Length)</span>
-                        <span class="bp-batteries-muted">@When(report.UploadedAtUtc) by @report.UploadedBy</span>
-                    </li>
-                }
-            </ul>
-        }
-        <form method="post" action="@href/accidents" enctype="multipart/form-data" class="bp-attr-upload bp-accident-upload">
-            @Html.AntiForgeryToken()
-            <div class="bp-attr-file">
-                <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
-                    Choose file
-                    <input type="file" name="file" class="bp-document-hidden-file" accept="application/pdf,image/png,image/jpeg" data-upload-file />
-                </label>
-                <span class="bp-help-text" data-upload-chosen>No file chosen</span>
-            </div>
-            <div class="bp-attr-parts">
-                <label>
-                    <span>Title (optional)</span>
-                    <input type="text" name="title" placeholder="e.g. Collision on 12 Oct 2026" />
-                </label>
-                <label>
-                    <span>Language (optional)</span>
-                    <select name="language">
-                        <option value=""></option>
-                        @foreach (var language in Model.Values.Languages)
-                        {
-                            <option value="@language">@language</option>
-                        }
-                    </select>
-                </label>
-            </div>
-            <div class="bp-live-save">
-                <small class="bp-field-help">PDF, PNG or JPEG, checked by its content.</small>
-                <button type="submit" class="bp-secondary-button" data-upload-button>Add report</button>
-            </div>
-        </form>
-    </section>
-
-    <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="versions">
-        <span class="bp-summary-title">Versions</span>
-        @if (Model.Versions.Count == 0)
-        {
-            <h2>No versions yet</h2>
-            <p class="mb-0">Publishing makes version 1. Each later change to its values makes a new version; the older ones stay readable by their passport ID.</p>
-        }
-        else
-        {
-            <h2>@Words.Count(Model.Versions.Count, "version")</h2>
-            <div class="bp-table-scroll">
-                <table class="table align-middle bp-console-table bp-mobile-card-table bp-versions-table">
-                    <thead>
-                        <tr>
-                            <th>Version</th>
-                            <th>Passport ID <span class="bp-attr-number">#5</span></th>
-                            <th>Status</th>
-                            <th>Published</th>
-                            <th>Signature</th>
-                        </tr>
-                    </thead>
-                    <tbody>
-                        @foreach (var row in Model.Versions)
-                        {
-                            <tr>
-                                <td data-label="Version">@row.Version.Number</td>
-                                <td data-label="Passport ID"><span class="bp-id-copy-row"><a class="bp-id-value" href="/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail" target="_blank" rel="noreferrer">@row.Version.DigitalProductPassportId</a>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
-                                <td data-label="Status">@(row.Version.DppStatus == DppStatuses.Active ? "Active" : "Archived")</td>
-                                <td data-label="Published">@When(row.Version.PublishedAtUtc) <small class="bp-batteries-muted d-block">by @row.Version.PublishedBy</small></td>
-                                <td data-label="Signature">
-                                    @if (row.SignatureHolds)
-                                    {
-                                        <span class="bp-done-note">Verified</span>
-                                    }
-                                    else
-                                    {
-                                        <span class="bp-missing-note">Doesn't match</span>
-                                    }
-                                    <details class="bp-signature-details">
-                                        <summary>Details</summary>
-                                        <dl>
-                                            <div><dt>Signed</dt><dd>@When(row.Version.Proof.CreatedUtc)</dd></div>
-                                            <div><dt>Issuer</dt><dd>@row.Version.Proof.Issuer</dd></div>
-                                            <div><dt>Key</dt><dd>@row.Version.Proof.VerificationMethod</dd></div>
-                                            <div><dt>Method</dt><dd>@row.Version.Proof.Cryptosuite</dd></div>
-                                            <div><dt>Hash</dt><dd><code>@row.Version.Proof.Hash</code></dd></div>
-                                        </dl>
-                                    </details>
-                                </td>
-                            </tr>
-                        }
-                    </tbody>
-                </table>
-            </div>
-        }
-    </section>
+    @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
+    @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
+    @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
+    @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
 
     @if (!publication.IsPublished)
     {
diff --git a/web/Views/ProductData/_EditorField.cshtml b/web/Views/ProductData/_EditorField.cshtml
index 5c24908..4fc04b9 100644
--- a/web/Views/ProductData/_EditorField.cshtml
+++ b/web/Views/ProductData/_EditorField.cshtml
@@ -122,7 +122,7 @@
         {
             <details class="bp-attr-audience">
                 <summary><svg viewBox="0 0 16 16" aria-hidden="true"><rect x="3.5" y="7" width="9" height="6.5" rx="1.5" /><path d="M5.5 7V5.5a2.5 2.5 0 0 1 5 0V7" /></svg>Not public</summary>
-                <span>@(form.Kind == FormKind.Upload ? "Downloaded" : "Seen") only by @field.SeenBy.</span>
+                <span>@(form.Kind == FormKind.Upload ? "Downloaded" : "Seen") only by @(Model.Editor.ForTeam ? field.SeenBy : field.SeenByOutsideTheTeam).</span>
             </details>
         }
         @if (AttributeCatalog.ConditionOf(definition) is { } condition)
```

`web/Dpp/Pages/BatteryCards.cs`:

```csharp
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// The cards the team's battery page and the cluster admin's share (step 4a): live values, modules, accident reports
/// and versions, drawn by one partial view each. Forms post to <see cref="Href"/> plus "/live" or "/accidents"; the
/// team's page also edits the modules.
/// </summary>
/// <param name="ForTeam">The team's page: it edits the module list and links the model's module description.</param>
/// <param name="ModuleDescriptionHref">Where the team types the model's module description; null on the cluster admin's page.</param>
public sealed record BatteryCards(
    BatteryPack Pack,
    string Href,
    RecordEditorView Live,
    IReadOnlyList<DppDocument> AccidentReports,
    IReadOnlyList<VersionRow> Versions,
    IReadOnlyList<string> Languages,
    bool ForTeam,
    string? ModuleDescriptionHref = null,
    bool HasModuleDescription = false)
{
    /// <summary>Who the team is, in the reader's words.</summary>
    public string Team => ForTeam ? "your team" : "the team";

    /// <summary>A file size in plain words.</summary>
    public static string Size(long bytes) => bytes < 1024
        ? $"{bytes} bytes"
        : bytes < 1048576
            ? $"{(bytes / 1024.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} KB"
            : $"{(bytes / 1048576.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} MB";

    /// <summary>Where a stored value came from, in plain words.</summary>
    public static string Source(string? source) => source switch
    {
        "form" => "typed on a battery page",
        "upload" => "from an upload",
        "feed" => "from the battery system",
        "system" => "set by the app",
        "seed" => "test data",
        _ => source ?? "not recorded",
    };
}
```

`web/Views/Batteries/_AccidentsCard.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model BatteryCards
@* The accident reports card (#92): on the team's battery page and on the cluster admin's (step 4a). *@
<section class="bp-card bp-console-form-card bp-battery-card mt-4" id="accidents">
    <span class="bp-summary-title">Accident reports <span class="bp-attr-number">#92</span></span>
    <h2>@(Model.AccidentReports.Count == 0 ? "No accidents reported" : Words.Count(Model.AccidentReports.Count, "accident report"))</h2>
    <p class="mb-0">The passport links to the battery's <a href="@ReaderPages.AccidentsHref(Model.Pack.Id)">accident page</a>, which lists these reports or says none were reported. Seen only by the battery's cluster, market surveillance and @Model.Team.</p>
    @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "accidents")
    @if (Model.AccidentReports.Count > 0)
    {
        <ul class="bp-file-checks">
            @foreach (var report in Model.AccidentReports)
            {
                <li>
                    <span><a href="@ReaderPages.DocumentHref(report.Id)" target="_blank" rel="noreferrer"><strong>@report.Title</strong></a> · @report.FileName · @BatteryCards.Size(report.Length)</span>
                    <span class="bp-batteries-muted">@ValueWords.Time(report.UploadedAtUtc) by @report.UploadedBy</span>
                </li>
            }
        </ul>
    }
    <form method="post" action="@Model.Href/accidents" enctype="multipart/form-data" class="bp-attr-upload bp-accident-upload">
        @Html.AntiForgeryToken()
        <div class="bp-attr-file">
            <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                Choose file
                <input type="file" name="file" class="bp-document-hidden-file" accept="application/pdf,image/png,image/jpeg" data-upload-file />
            </label>
            <span class="bp-help-text" data-upload-chosen>No file chosen</span>
        </div>
        <div class="bp-attr-parts">
            <label>
                <span>Title (optional)</span>
                <input type="text" name="title" placeholder="e.g. Collision on 12 Oct 2026" />
            </label>
            <label>
                <span>Language (optional)</span>
                <select name="language">
                    <option value=""></option>
                    @foreach (var language in Model.Languages)
                    {
                        <option value="@language">@language</option>
                    }
                </select>
            </label>
        </div>
        <div class="bp-live-save">
            <small class="bp-field-help">PDF, PNG or JPEG, checked by its content.</small>
            <button type="submit" class="bp-secondary-button" data-upload-button>Add report</button>
        </div>
    </form>
</section>
```

`web/Views/Batteries/_CardMessages.cshtml`:

```cshtml
@using BatteryPassWeb.Controllers
@model string
@* The message line under a battery card after its form was sent: done, information or a problem (both battery pages). *@
@{
    var card = (TempData.Peek(BatteriesController.CardKey) ?? ViewData[BatteriesController.CardKey]) as string;
    var status = (TempData.Peek(BatteriesController.CardStatusKey) ?? ViewData[BatteriesController.CardStatusKey]) as string;
    var error = (TempData.Peek(BatteriesController.CardErrorKey) ?? ViewData[BatteriesController.CardErrorKey]) as string;
    var info = (TempData.Peek(BatteriesController.CardInfoKey) ?? ViewData[BatteriesController.CardInfoKey]) as string;
}
@if (card == Model)
{
    if (!string.IsNullOrWhiteSpace(status))
    {
        <p class="alert alert-success bp-status-line is-done" role="status">
            <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3.5 8.5l3 3 6-7" /></svg>
            <span>@status</span>
        </p>
    }
    if (!string.IsNullOrWhiteSpace(info))
    {
        <p class="alert alert-secondary bp-status-line is-info" role="status"><span>@info</span></p>
    }
    if (!string.IsNullOrWhiteSpace(error))
    {
        <p class="alert alert-danger bp-status-line is-problem" role="alert">
            <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 4.5v4.5M8 11.5v.5" /></svg>
            <span>@error</span>
        </p>
    }
}
```

`web/Views/Batteries/_LiveValuesCard.cshtml`:

```cshtml
@using BatteryPassWeb.Controllers
@using BatteryPassWeb.Dpp.Pages
@model BatteryCards
@* The live values card (#63, #83, #87, E1–E4): on the team's battery page and on the cluster admin's (step 4a). *@
<section class="bp-card bp-console-form-card bp-battery-card mt-4" id="live">
    <span class="bp-summary-title">Live values</span>
    <h2>Measured on the battery</h2>
    <p class="mb-0">
        @if (Model.ForTeam)
        {
            <text>Its battery system sends these; type them here before it does, for example the end-of-line values, or to
            correct one. They change the passport in place, without a new version.</text>
        }
        else
        {
            <text>Its battery system sends these; type them here to correct one. They change the passport in place, without a
            new version.</text>
        }
    </p>
    @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "live")
    <form method="post" action="@Model.Live.SaveAction" class="bp-live-form">
        @Html.AntiForgeryToken()
        <input type="hidden" name="@ProductDataController.StampField" value="@Model.Live.Stamp" />
        <div class="bp-attr-list">
            @foreach (var field in Model.Live.Sections.SelectMany(section => section.Fields))
            {
                @await Html.PartialAsync("~/Views/ProductData/_EditorField.cshtml", new EditorFieldView(Model.Live, field))
                @if (Model.Pack.Values.GetValueOrDefault(field.Attribute.Property) is { } stored)
                {
                    <p class="bp-live-meta">Last value @ValueWords.Time(stored.GeneratedAtUtc), @BatteryCards.Source(stored.Source)@(string.IsNullOrEmpty(stored.Who) ? string.Empty : $" by {stored.Who}").</p>
                }
            }
        </div>
        <div class="bp-live-save">
            <label>
                <span>Measured at <span class="bp-attr-optional">(optional; now if empty)</span></span>
                <input type="datetime-local" name="@BatteriesController.MeasuredAtField" />
            </label>
            <button type="submit" class="bp-primary-button">Save live values</button>
        </div>
    </form>
</section>
```

`web/Views/Batteries/_ModulesCard.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model BatteryCards
@* The module list (decision 31): the team edits it on its battery page; the cluster admin's page shows it (step 4a). *@
<section class="bp-card bp-console-form-card bp-battery-card mt-4" id="modules">
    <span class="bp-summary-title">Modules <em class="bp-field-marker">Not passport data</em></span>
    <h2>@(Model.Pack.Modules.Count == 0 ? "No modules listed" : Words.Count(Model.Pack.Modules.Count, "module"))</h2>
    <p class="mb-0">
        @if (Model.ForTeam)
        {
            <text>The public summary shows how many modules the battery has, with the model's
            <a href="@Model.ModuleDescriptionHref">module description</a>@(Model.HasModuleDescription ? string.Empty : " (not typed yet)").</text>
        }
        else
        {
            <text>The team keeps this list.</text>
        }
        The module IDs are seen only by the battery's cluster, market surveillance and @Model.Team.
    </p>
    @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "modules")
    @if (Model.Pack.Modules.Count > 0)
    {
        <div class="bp-table-scroll">
            <table class="table align-middle bp-console-table bp-mobile-card-table bp-module-table mb-0">
                <thead><tr><th>Position</th><th>Serial number</th><th>Connectivity ID</th></tr></thead>
                <tbody>
                    @foreach (var module in Model.Pack.Modules)
                    {
                        <tr>
                            <td data-label="Position">@module.Position</td>
                            <td data-label="Serial number">@(module.SerialNumber ?? "—")</td>
                            <td data-label="Connectivity ID">@(module.ConnectivityId ?? "—")</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
    @if (Model.ForTeam)
    {
        <form method="post" action="@Model.Href/modules/from-export" enctype="multipart/form-data" class="bp-attr-upload bp-module-upload">
            @Html.AntiForgeryToken()
            <div class="bp-attr-file">
                <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                    Choose file
                    <input type="file" name="file" class="bp-document-hidden-file" accept="application/json,.json" data-upload-file />
                </label>
                <span class="bp-help-text" data-upload-chosen>No file chosen</span>
            </div>
            <div class="bp-live-save">
                <small class="bp-field-help">The battery system's JSON export of this battery. It replaces the list.</small>
                <button type="submit" class="bp-secondary-button" data-upload-button>Fill from export</button>
            </div>
        </form>
        var listCard = (TempData.Peek(BatteryPassWeb.Controllers.BatteriesController.CardKey) ?? ViewData[BatteryPassWeb.Controllers.BatteriesController.CardKey]) as string;
        <details class="bp-module-edit" open="@(listCard == "module-list")">
            <summary>Type or correct the list</summary>
            @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "module-list")
            <form method="post" action="@Model.Href/modules">
                @Html.AntiForgeryToken()
                <label>
                    <span>One module per line: position; serial number; connectivity ID. One of the IDs may be empty. An empty list clears it.</span>
                    <textarea name="modules" rows="@Math.Clamp(Model.Pack.Modules.Count + 2, 4, 14)" spellcheck="false" placeholder="1; ; MD-A9ASVW8">@ModuleList.ToText(Model.Pack.Modules)</textarea>
                </label>
                <div class="bp-live-save">
                    <span></span>
                    <button type="submit" class="bp-secondary-button">Save modules</button>
                </div>
            </form>
        </details>
    }
</section>
```

`web/Views/Batteries/_VersionsCard.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@using BatteryPassWeb.Dpp.Store
@model BatteryCards
@* The passport's versions with their signatures: on the team's battery page and on the cluster admin's (step 4a). *@
<section class="bp-card bp-console-form-card bp-battery-card mt-4" id="versions">
    <span class="bp-summary-title">Versions</span>
    @if (Model.Versions.Count == 0)
    {
        <h2>No versions yet</h2>
        <p class="mb-0">Publishing makes version 1. Each later change to its values makes a new version; the older ones stay readable by their passport ID.</p>
    }
    else
    {
        <h2>@Words.Count(Model.Versions.Count, "version")</h2>
        <div class="bp-table-scroll">
            <table class="table align-middle bp-console-table bp-mobile-card-table bp-versions-table">
                <thead>
                    <tr>
                        <th>Version</th>
                        <th>Passport ID <span class="bp-attr-number">#5</span></th>
                        <th>Status</th>
                        <th>Published</th>
                        <th>Signature</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var row in Model.Versions)
                    {
                        <tr>
                            <td data-label="Version">@row.Version.Number</td>
                            <td data-label="Passport ID"><span class="bp-id-copy-row"><a class="bp-id-value" href="/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail" target="_blank" rel="noreferrer">@row.Version.DigitalProductPassportId</a>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
                            <td data-label="Status">@(row.Version.DppStatus == DppStatuses.Active ? "Active" : "Archived")</td>
                            <td data-label="Published">@ValueWords.Time(row.Version.PublishedAtUtc) <small class="bp-batteries-muted d-block">by @row.Version.PublishedBy</small></td>
                            <td data-label="Signature">
                                @if (row.SignatureHolds)
                                {
                                    <span class="bp-done-note">Verified</span>
                                }
                                else
                                {
                                    <span class="bp-missing-note">Doesn't match</span>
                                }
                                <details class="bp-signature-details">
                                    <summary>Details</summary>
                                    <dl>
                                        <div><dt>Signed</dt><dd>@ValueWords.Time(row.Version.Proof.CreatedUtc)</dd></div>
                                        <div><dt>Issuer</dt><dd>@row.Version.Proof.Issuer</dd></div>
                                        <div><dt>Key</dt><dd>@row.Version.Proof.VerificationMethod</dd></div>
                                        <div><dt>Method</dt><dd>@row.Version.Proof.Cryptosuite</dd></div>
                                        <div><dt>Hash</dt><dd><code>@row.Version.Proof.Hash</code></dd></div>
                                    </dl>
                                </details>
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

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteriesControllerTests | FullyQualifiedName~DppControllerTests | FullyQualifiedName~ModuleDataTests | FullyQualifiedName~ReviewFixesTests"`
Expected: `Passed!  - Failed:     0, Passed:    43`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2115`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs BatteryPassWeb.Tests/Dpp/DppControllerTests.cs BatteryPassWeb.Tests/Dpp/DppTestFiles.cs BatteryPassWeb.Tests/Dpp/ModuleDataTests.cs BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs web/Dpp/Pages/BatteryCards.cs web/Dpp/Pages/BatteryPages.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_AccidentsCard.cshtml web/Views/Batteries/_CardMessages.cshtml web/Views/Batteries/_LiveValuesCard.cshtml web/Views/Batteries/_ModulesCard.cshtml web/Views/Batteries/_VersionsCard.cshtml web/Views/ProductData/_EditorField.cshtml
git commit -m "feat(dpp): the battery page's cards as partials both battery pages share" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The cluster admin's battery page, the Registry's Manage link and the detail page's

The page itself (mock-up S2 without its dashed cards), its controller, "Manage" on the cluster admin's own Registry rows (S1), "Manage this battery" on the detail page for the battery's cluster admins, and one step in the help's "Your cluster" part.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/RegistryListTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Look/HelpTests.cs`
- Create: `web/Controllers/ClusterBatteriesController.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Modify: `web/Dpp/Pages/RegistryList.cs`
- Create: `web/Views/ClusterAdmin/Battery.cshtml`
- Modify: `web/Views/Help/Guide.cshtml`
- Modify: `web/Views/Passport/_PassportHero.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `4a-task4-tests.patch` (`BatteryPassWeb.Tests/Dpp/RegistryListTests.cs`, `BatteryPassWeb.Tests/Look/HelpTests.cs`). Save the block as `../4a-task4-tests.patch`, outside the worktree, and run `git apply ../4a-task4-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs b/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs
index 47ed382..327199d 100644
--- a/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/RegistryListTests.cs
@@ -1,4 +1,5 @@
 using BatteryPassWeb.Dpp.Access;
+using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Store;
 
@@ -52,5 +53,28 @@ public sealed class RegistryListTests
 
         Assert.Contains("/dpp/@Uri.EscapeDataString(row.Identifier)/detail", view);
         Assert.DoesNotContain("_BatteryTable", view); // the old model's table is gone from the Registry
+        Assert.Contains("Model.ManageHref(row)", view); // step 4a: the cluster admin's "Manage" link
+    }
+
+    [Fact]
+    public void AClusterAdmin_ShouldGetAManageLinkOnTheirOwnClustersRowsOnly()
+    {
+        var reader = new Reader(new HashSet<ReaderRole> { ReaderRole.ClusterAdmin, ReaderRole.ClusterUser }, new HashSet<string> { "north", "south" }, "x");
+        var editor = ChangeSender.ClusterAdmin("x", "north"); // admin of north, a plain member of south
+
+        var page = RegistryList.Build(reader, Batteries, editor);
+
+        Assert.Equal("/cluster-admin/batteries/pack-NORTH-1", page.ManageHref(page.Rows.Single(row => row.SerialNumber == "NORTH-1")));
+        Assert.Null(page.ManageHref(page.Rows.Single(row => row.SerialNumber == "SOUTH-1")));
+    }
+
+    [Fact]
+    public void EveryoneElse_ShouldGetNoManageLink()
+    {
+        var user = RegistryList.Build(Reader.With("x", ReaderRole.ClusterUser, "north"), Batteries);
+        var team = RegistryList.Build(Reader.With("x", ReaderRole.Team), Batteries, ChangeSender.Team("x"));
+
+        Assert.All(user.Rows, row => Assert.Null(user.ManageHref(row)));
+        Assert.All(team.Rows, row => Assert.Null(team.ManageHref(row)));
     }
 }
diff --git a/BatteryPassWeb.Tests/Look/HelpTests.cs b/BatteryPassWeb.Tests/Look/HelpTests.cs
index 7cb7ca6..6a22b16 100644
--- a/BatteryPassWeb.Tests/Look/HelpTests.cs
+++ b/BatteryPassWeb.Tests/Look/HelpTests.cs
@@ -78,6 +78,7 @@ public sealed class HelpTests(MongoFixture mongo)
         Assert.Contains("<a href=\"/cluster-admin/users\">Add your people</a>", part, StringComparison.Ordinal);
         Assert.Contains("<a href=\"/cluster-admin/api-tokens\">Give your systems a token</a>", part, StringComparison.Ordinal);
         Assert.Contains("it is shown once", part, StringComparison.Ordinal);
+        Assert.Contains("\"Manage\" opens a battery's page", part, StringComparison.Ordinal); // step 4a
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The cluster admin's battery page as a signed-in user reaches it (step 4a).</summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterBatteriesControllerTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";

    private const string SouthAdmin = "south-admin@example.com";

    [Fact]
    public void ThePage_ShouldBeTheClusterAdminsWithEveryPostChecked()
    {
        var type = typeof(ClusterBatteriesController);

        Assert.Equal("ClusterAdminOrAdmin", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("cluster-admin/batteries", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.All(
            type.GetMethods().Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null),
            method => Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
    }

    [Fact]
    public void BothBatteryPages_ShouldDrawTheSameCardsAndOnlyTheTeamsTheTeamOnlyForms()
    {
        var clusterAdmin = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));
        var team = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));

        Assert.All(DppTestFiles.BatteryCardViews, card =>
        {
            Assert.Contains($"~/Views/Batteries/{card}.cshtml\", Model.Cards", clusterAdmin, StringComparison.Ordinal);
            Assert.Contains($"~/Views/Batteries/{card}.cshtml\", Model.Cards", team, StringComparison.Ordinal);
        });
        foreach (var teamOnly in new[] { "_RecordEditor", "/publish", "/cluster", "/remove", "/audit" })
        {
            Assert.DoesNotContain(teamOnly, clusterAdmin, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheBatterysClusterAdmin_ShouldSeeItsPage()
    {
        var rig = await RigAsync();

        var view = Assert.IsType<ViewResult>(await Controller(rig, NorthAdmin, "clusterAdmin").Battery(rig.PackId, CancellationToken.None));

        Assert.Equal(rig.PackId, Assert.IsType<ClusterBatteryPage>(view.Model).Pack.Id);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldFindNothing()
    {
        var rig = await RigAsync();

        Assert.IsType<NotFoundResult>(await Controller(rig, SouthAdmin, "clusterAdmin").Battery(rig.PackId, CancellationToken.None));
    }

    [Fact]
    public async Task TheTeam_ShouldBeSentToItsOwnBatteryPage()
    {
        var rig = await RigAsync();

        var redirect = Assert.IsType<RedirectResult>(await Controller(rig, "team@example.com", "admin").Battery(rig.PackId, CancellationToken.None));

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), redirect.Url);
    }

    [Fact]
    public async Task LiveValues_ShouldBeSavedAndThePageOpenAtTheirCard()
    {
        var rig = await RigAsync();
        var form = await LiveFormAsync(rig, "temperatureInformation", "21");
        var controller = Controller(rig, NorthAdmin, "clusterAdmin", form);

        var redirect = Assert.IsType<RedirectResult>(await controller.SaveLiveValues(rig.PackId, CancellationToken.None));

        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#live", redirect.Url);
        Assert.Equal("live", controller.TempData[BatteriesController.CardKey]);
        var temperature = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["temperatureInformation"];
        Assert.Equal(21, temperature.Value!["celsiusValue"]!.GetValue<int>());
        Assert.Equal(NorthAdmin, temperature.Who);
    }

    [Fact]
    public async Task ARefusedLiveValue_ShouldShowThePageAgainAsTyped()
    {
        var rig = await RigAsync();
        var controller = Controller(rig, NorthAdmin, "clusterAdmin", await LiveFormAsync(rig, "stateOfChargeSoC", "140"));

        var view = Assert.IsType<ViewResult>(await controller.SaveLiveValues(rig.PackId, CancellationToken.None));

        Assert.Equal("live", view.ViewData[BatteriesController.CardKey]);
        var field = Assert.IsType<ClusterBatteryPage>(view.Model).Live.Sections.SelectMany(section => section.Fields).Single(item => item.Attribute.Number == 63);
        Assert.Equal("140", field.Text(BatteryTesting.Field("stateOfChargeSoC")));
        Assert.NotNull(field.Refusal);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotSaveLiveValues()
    {
        var rig = await RigAsync();
        var controller = Controller(rig, SouthAdmin, "clusterAdmin", await LiveFormAsync(rig, "stateOfChargeSoC", "12"));

        Assert.IsType<NotFoundResult>(await controller.SaveLiveValues(rig.PackId, CancellationToken.None));
    }

    [Fact]
    public async Task AnAccidentReport_ShouldBeAddedAndThePageOpenAtTheReports()
    {
        var rig = await RigAsync();
        var controller = Controller(rig, NorthAdmin, "clusterAdmin");

        var redirect = Assert.IsType<RedirectResult>(await controller.AddAccidentReport(rig.PackId, Upload("fall.pdf", DocumentTesting.Pdf("fall")), "Dropped", null, CancellationToken.None));

        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}#accidents", redirect.Url);
        Assert.Equal("fall.pdf added.", controller.TempData[BatteriesController.CardStatusKey]);
    }

    [Fact]
    public async Task NoFile_ShouldBeSaidOnTheReportsCard()
    {
        var rig = await RigAsync();
        var controller = Controller(rig, NorthAdmin, "clusterAdmin");

        Assert.IsType<RedirectResult>(await controller.AddAccidentReport(rig.PackId, null, null, null, CancellationToken.None));

        Assert.Equal("Choose a file first.", controller.TempData[BatteriesController.CardErrorKey]);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotAddAReport()
    {
        var rig = await RigAsync();

        Assert.IsType<NotFoundResult>(await Controller(rig, SouthAdmin, "clusterAdmin").AddAccidentReport(rig.PackId, Upload("fall.pdf", DocumentTesting.Pdf()), null, null, CancellationToken.None));
    }

    [Fact]
    public async Task TheDetailPage_ShouldOfferManageToTheBatterysClusterAdminOnly()
    {
        var rig = await RigAsync();
        await new ClusterRepository(rig.Context).UpsertClusterMembershipAsync("north-user@example.com", BatteryTesting.North, "member");

        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), (await DetailAsync(rig, NorthAdmin, "clusterAdmin")).ViewData[ClusterBatteryPages.ManageHrefKey]);
        Assert.Null((await DetailAsync(rig, SouthAdmin, "clusterAdmin")).ViewData[ClusterBatteryPages.ManageHrefKey]);
        Assert.Null((await DetailAsync(rig, "north-user@example.com", "member")).ViewData[ClusterBatteryPages.ManageHrefKey]);
        Assert.Equal("ManageHref", ClusterBatteryPages.ManageHrefKey); // the hero names no cluster, so it reads the key as text
        Assert.Contains("ViewData[\"ManageHref\"] is string manageHref", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml")), StringComparison.Ordinal);
    }

    private static async Task<ViewResult> DetailAsync(PublicationRig rig, string email, string role)
    {
        var controller = new PassportController(new DppReaders(new ClusterRepository(rig.Context)), PassportPagesTests.Pages(rig))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
                },
            },
        };
        return Assert.IsType<ViewResult>(await controller.Detail(PassportPagesTests.Identifier(rig), CancellationToken.None, manage: ClusterBatteryPagesTests.Pages(rig)));
    }

    private async Task<PublicationRig> RigAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        await clusters.UpsertClusterMembershipAsync(SouthAdmin, BatteryTesting.South, "clusterAdmin");
        return rig;
    }

    /// <summary>The live form as the page shows it, with its stamp, and one field typed.</summary>
    private static async Task<Dictionary<string, string>> LiveFormAsync(PublicationRig rig, string property, string typed)
    {
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field(property)] = typed;
        form[ProductDataController.StampField] = PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null);
        return form;
    }

    private static ClusterBatteriesController Controller(PublicationRig rig, string email, string role, IReadOnlyDictionary<string, string>? form = null)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
        };
        http.Request.Form = new FormCollection((form ?? new Dictionary<string, string>()).ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private static IFormFile Upload(string name, byte[] content) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

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

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ClusterBatteriesControllerTests | FullyQualifiedName~RegistryListTests | FullyQualifiedName~HelpTests"`
Expected: the build fails with CS0246 (for example `error CS0246 The type or namespace name 'ClusterBatteriesController' could not be found`).

- [ ] **Step 3: Write the code**

Patch `4a-task4-code.patch` (`web/Controllers/PassportController.cs`, `web/Controllers/RegistryController.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Dpp/Pages/RegistryList.cs`, `web/Views/Help/Guide.cshtml`, `web/Views/Passport/_PassportHero.cshtml`, `web/Views/Registry/Index.cshtml`). Save the block as `../4a-task4-code.patch`, outside the worktree, and run `git apply ../4a-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/PassportController.cs b/web/Controllers/PassportController.cs
index a431a4a..cf5538c 100644
--- a/web/Controllers/PassportController.cs
+++ b/web/Controllers/PassportController.cs
@@ -27,7 +27,11 @@ public sealed class PassportController(DppReaders readers, PassportPages pages)
     }
 
     [HttpGet("{id}/detail")]
-    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken, [FromServices] BatteryApi? liveHistory = null)
+    public async Task<IActionResult> Detail(
+        string id,
+        CancellationToken cancellationToken,
+        [FromServices] BatteryApi? liveHistory = null,
+        [FromServices] ClusterBatteryPages? manage = null)
     {
         if (User.Identity?.IsAuthenticated != true)
         {
@@ -48,6 +52,12 @@ public sealed class PassportController(DppReaders readers, PassportPages pages)
             ViewData["LiveHistory"] = await liveHistory.HistoryAsync(reader, page.Version.PackId, BatteryApi.MaxHistoryHours, cancellationToken);
         }
 
+        // The battery's cluster admins manage it from here too (step 4a).
+        if (manage is not null && page.IsCurrent)
+        {
+            ViewData[ClusterBatteryPages.ManageHrefKey] = await manage.ManageHrefAsync(await readers.EditorForAsync(User, cancellationToken), page.Version.PackId, cancellationToken);
+        }
+
         Response.Headers[HeaderNames.CacheControl] = "private, no-store";
         return View(page);
     }
diff --git a/web/Controllers/RegistryController.cs b/web/Controllers/RegistryController.cs
index 29af760..5ba5257 100644
--- a/web/Controllers/RegistryController.cs
+++ b/web/Controllers/RegistryController.cs
@@ -17,6 +17,7 @@ public sealed class RegistryController(DppReaders readers, BatteryPages batterie
     public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken cancellationToken)
     {
         var reader = await readers.ForAsync(User, cancellationToken);
-        return View(RegistryList.Build(reader, await batteries.ListAsync(q, cancellationToken)));
+        var editor = await readers.EditorForAsync(User, cancellationToken);
+        return View(RegistryList.Build(reader, await batteries.ListAsync(q, cancellationToken), editor));
     }
 }
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index abde6df..7b38a01 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -22,6 +22,9 @@ public sealed record ClusterBatteryPage(
     public string BatchLabel => $"{Sources.Batch.Year} · {Sources.Site.Name}";
 
     public string Href => ClusterBatteryPages.BatteryHref(Pack.Id);
+
+    /// <summary>The team page's cards, without what only the team does (step 4a).</summary>
+    public BatteryCards Cards => new(Pack, Href, Live, AccidentReports, Versions, Live.Languages, ForTeam: false);
 }
 
 /// <summary>
diff --git a/web/Dpp/Pages/RegistryList.cs b/web/Dpp/Pages/RegistryList.cs
index 545e0c1..f83b722 100644
--- a/web/Dpp/Pages/RegistryList.cs
+++ b/web/Dpp/Pages/RegistryList.cs
@@ -1,9 +1,21 @@
 using BatteryPassWeb.Dpp.Access;
+using BatteryPassWeb.Dpp.Editing;
 
 namespace BatteryPassWeb.Dpp.Pages;
 
 /// <summary>The Registry: the published batteries a signed-in reader may open, and whether to show their clusters.</summary>
-public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query, bool ShowClusters, string Scope);
+public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query, bool ShowClusters, string Scope)
+{
+    /// <summary>The clusters the signed-in user is admin of (step 4a); empty for everyone else, the team included.</summary>
+    public IReadOnlySet<string> ManagedClusterIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);
+
+    /// <summary>The cluster admin's page of a battery of a cluster they are admin of; null otherwise.</summary>
+    public string? ManageHref(BatteryRow row)
+    {
+        ArgumentNullException.ThrowIfNull(row);
+        return row.ClusterId is { } cluster && ManagedClusterIds.Contains(cluster) ? ClusterBatteryPages.BatteryHref(row.PackId) : null;
+    }
+}
 
 /// <summary>
 /// Who the Registry lists which batteries for (decision 30). The team, the authorities and the legitimate-interest roles
@@ -41,13 +53,17 @@ public static class RegistryList
             : "No batteries are shared with your account yet. Ask your cluster admin or the manufacturer's team.";
     }
 
-    public static RegistryPage Build(Reader reader, BatteriesPage batteries)
+    /// <param name="editor">The signed-in user as the sender of a change; a cluster admin's rows get a "Manage" link (step 4a).</param>
+    public static RegistryPage Build(Reader reader, BatteriesPage batteries, ChangeSender? editor = null)
     {
         ArgumentNullException.ThrowIfNull(batteries);
         return new RegistryPage(
             batteries.Rows.Where(row => MayList(reader, row)).ToArray(),
             batteries.Query,
             ShowsClusters(reader),
-            Scope(reader));
+            Scope(reader))
+        {
+            ManagedClusterIds = editor is { Kind: SenderKind.ClusterAdmin } ? editor.ClusterIds.ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal),
+        };
     }
 }
diff --git a/web/Views/Help/Guide.cshtml b/web/Views/Help/Guide.cshtml
index e575a22..a9d76ee 100644
--- a/web/Views/Help/Guide.cshtml
+++ b/web/Views/Help/Guide.cshtml
@@ -53,6 +53,7 @@
                         <li><strong><a href="/cluster-admin/users">Add your people</a></strong><p>Under Cluster admin, Users: add someone as a user (sees your cluster's batteries) or as a cluster admin (also looks after its users and tokens).</p></li>
                         <li><strong><a href="/cluster-admin/api-tokens">Give your systems a token</a></strong><p>Under Cluster admin, API tokens: "Read" reads your batteries' passports; "Read + write" also sends their live values. Copy the value when it is shown: it is shown once.</p></li>
                         <li><strong><a href="/registry">Check what they see</a></strong><p>Your users find your cluster's batteries in the Registry, with the purchaser values on the detail page.</p></li>
+                        <li><strong><a href="/registry">Look after your batteries</a></strong><p>In the Registry, "Manage" opens a battery's page: correct its live values and add accident reports. Its modules and versions are shown there too; the team keeps the rest.</p></li>
                     </ol>
                 </section>
             }
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index ba550c7..44d627e 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -92,6 +92,10 @@
             @if (detail)
             {
                 <a class="bp-report-hero-cta" href="@PassportPages.SummaryHref(Model.Address)">&lt;- Back to summary</a>
+                @if (ViewData["ManageHref"] is string manageHref)
+                {
+                    <a class="bp-report-hero-cta" href="@manageHref">Manage this battery -&gt;</a>
+                }
             }
             else if (signedIn && Model.ReaderSeesMore)
             {
diff --git a/web/Views/Registry/Index.cshtml b/web/Views/Registry/Index.cshtml
index 0acd7c9..7132d0b 100644
--- a/web/Views/Registry/Index.cshtml
+++ b/web/Views/Registry/Index.cshtml
@@ -39,6 +39,10 @@
                                 <th>Cluster</th>
                             }
                             <th>Passport</th>
+                            @if (Model.ManagedClusterIds.Count > 0)
+                            {
+                                <th><span class="visually-hidden">Manage</span></th>
+                            }
                         </tr>
                     </thead>
                     <tbody>
@@ -56,6 +60,15 @@
                                     <td data-label="Cluster">@row.ClusterLabel</td>
                                 }
                                 <td data-label="Passport">Version @row.Active!.Number <small class="d-block text-secondary">updated @ValueWords.Time(row.Active.LastUpdatedUtc)</small></td>
+                                @if (Model.ManagedClusterIds.Count > 0)
+                                {
+                                    <td data-label="">
+                                        @if (Model.ManageHref(row) is { } manage)
+                                        {
+                                            <a class="bp-text-button" href="@manage">Manage</a>
+                                        }
+                                    </td>
+                                }
                             </tr>
                         }
                     </tbody>
```

`web/Controllers/ClusterBatteriesController.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The cluster admin's battery page (design 6.1 and 12, step 4a), opened from the Registry: the live values and accident
/// reports of a published battery of a cluster they are admin of, and its modules and versions to read. Anyone else
/// finds nothing here; the team is sent to its own battery page.
/// </summary>
[Authorize(Policy = "ClusterAdminOrAdmin")]
[Route("cluster-admin/batteries")]
public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppReaders readers) : Controller
{
    private const string PageView = "~/Views/ClusterAdmin/Battery.cshtml";

    [HttpGet("{packId}")]
    public async Task<IActionResult> Battery(string packId, CancellationToken cancellationToken)
    {
        var sender = await readers.EditorForAsync(User, cancellationToken);
        if (sender?.Kind == SenderKind.Team)
        {
            return Redirect(BatteryPages.BatteryHref(packId));
        }

        var page = sender is null ? null : await pages.BatteryAsync(sender, packId, cancellationToken: cancellationToken);
        return page is null ? NotFound() : View(PageView, page);
    }

    [HttpPost("{packId}/live")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLiveValues(string packId, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
        {
            return NotFound();
        }

        var form = Request.Form;
        var saved = await pages.SaveLiveAsync(
            sender,
            packId,
            name => form.TryGetValue(name, out var value) ? value.ToString() : null,
            form[ProductDataController.StampField].ToString(),
            BatteriesController.MeasuredAt(form[BatteriesController.MeasuredAtField].ToString()),
            cancellationToken);
        if (saved is not { } result)
        {
            return NotFound();
        }

        var (post, save) = result;
        var done = save.SavedCount > 0 ? $"{Words.Count(save.SavedCount, "value")} saved." : string.Empty;
        switch (save.Outcome)
        {
            case EditorOutcome.NotFound:
                return NotFound();
            case EditorOutcome.Refused:
                ViewData[BatteriesController.CardKey] = "live";
                ViewData[BatteriesController.CardErrorKey] = $"{done} {Words.Count(save.RefusedCount, "value")} not saved; {(save.RefusedCount == 1 ? "it's" : "they're")} marked below.".TrimStart();
                return View(PageView, await pages.BatteryAsync(sender, packId, new EditorState(post, save), cancellationToken));
            case EditorOutcome.NothingChanged:
                TempData[BatteriesController.CardKey] = "live";
                TempData[BatteriesController.CardInfoKey] = "Nothing changed.";
                break;
            default:
                Card("live", $"{done} Live values change the passport in place, without a new version.", null);
                break;
        }

        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#live");
    }

    [HttpPost("{packId}/accidents")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAccidentReport(string packId, IFormFile? file, [FromForm] string? title, [FromForm] string? language, CancellationToken cancellationToken)
    {
        if (await ClusterAdminAsync(cancellationToken) is not { } sender)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            Card("accidents", null, "Choose a file first.");
            return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#accidents");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        if (await pages.AddAccidentReportAsync(sender, packId, new DocumentUpload(file.FileName, buffer.ToArray(), title, language), cancellationToken) is not { } added)
        {
            return NotFound();
        }

        Card("accidents", added.Problem is null ? $"{added.Document!.FileName} added." : null, added.Problem is null ? null : $"The report wasn't added: {added.Problem}.");
        return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#accidents");
    }

    /// <summary>The signed-in user as a cluster admin; null for the team (it has its own page) and for anyone else.</summary>
    private async Task<ChangeSender?> ClusterAdminAsync(CancellationToken cancellationToken) =>
        await readers.EditorForAsync(User, cancellationToken) is { Kind: SenderKind.ClusterAdmin } sender ? sender : null;

    private void Card(string card, string? status, string? error)
    {
        TempData[BatteriesController.CardKey] = card;
        TempData[BatteriesController.CardStatusKey] = status;
        TempData[BatteriesController.CardErrorKey] = error;
    }
}
```

`web/Views/ClusterAdmin/Battery.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model ClusterBatteryPage
@* The cluster admin's battery page (step 4a, mock-up S2): the team page's shared cards, without what only the team does. *@
@{
    ViewData["Title"] = $"Battery {Model.SerialNumber}";
    var active = Model.Versions.Select(row => row.Version).FirstOrDefault(version => version.DppStatus == BatteryPassWeb.Dpp.Store.DppStatuses.Active);
    var batteryKey = Uri.EscapeDataString(Model.Identifier);
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    <nav class="bp-breadcrumb" aria-label="Breadcrumb">
        <a href="/registry">Registry</a>
        <span aria-current="page">@Model.SerialNumber</span>
    </nav>

    <header class="bp-page-heading bp-battery-heading">
        <span class="bp-summary-title">Battery</span>
        <div class="bp-battery-title">
            <h1>@Model.SerialNumber</h1>
            @if (active is not null)
            {
                <span class="bp-field-marker is-published">Published · version @active.Number</span>
            }
        </div>
        <p class="mb-0">Model @Model.Sources.Model.Name of @Model.Family.Name, made at @Model.Sources.Site.Name in @Model.Sources.Batch.Year.</p>
        <nav class="bp-battery-links" aria-label="This battery elsewhere">
            <a href="/dpp/@batteryKey" target="_blank" rel="noreferrer">Public summary</a>
            <a href="/dpp/@batteryKey/detail" target="_blank" rel="noreferrer">Detail page</a>
            <a href="@ReaderPages.AccidentsHref(Model.Pack.Id)">Accident page</a>
        </nav>
        <dl class="bp-battery-facts">
            <div class="bp-battery-fact-wide">
                <dt>Battery ID <span class="bp-attr-number">#6</span></dt>
                <dd><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Identifier</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></dd>
            </div>
            <div>
                <dt>Batch</dt>
                <dd>@Model.BatchLabel</dd>
            </div>
            <div>
                <dt>Cluster</dt>
                <dd>@Model.ClusterLabel</dd>
            </div>
            <div>
                <dt>Serial number <span class="bp-attr-number">#11</span></dt>
                <dd>@Model.SerialNumber</dd>
            </div>
        </dl>
    </header>

    @await Html.PartialAsync("~/Views/Batteries/_LiveValuesCard.cshtml", Model.Cards)
    @await Html.PartialAsync("~/Views/Batteries/_AccidentsCard.cshtml", Model.Cards)
    @await Html.PartialAsync("~/Views/Batteries/_ModulesCard.cshtml", Model.Cards)
    @await Html.PartialAsync("~/Views/Batteries/_VersionsCard.cshtml", Model.Cards)
</main>

@section Scripts {
    <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ClusterBatteriesControllerTests | FullyQualifiedName~RegistryListTests | FullyQualifiedName~HelpTests"`
Expected: `Passed!  - Failed:     0, Passed:    27`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2129`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/ClusterBatteriesControllerTests.cs BatteryPassWeb.Tests/Dpp/RegistryListTests.cs BatteryPassWeb.Tests/Look/HelpTests.cs web/Controllers/ClusterBatteriesController.cs web/Controllers/PassportController.cs web/Controllers/RegistryController.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Dpp/Pages/RegistryList.cs web/Views/ClusterAdmin/Battery.cshtml web/Views/Help/Guide.cshtml web/Views/Passport/_PassportHero.cshtml web/Views/Registry/Index.cshtml
git commit -m "feat(dpp): the cluster admin's battery page, opened from the Registry and the detail page" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2129, Skipped:     0, Total:  2129`. From the 2098 before: 2106 after Task 1, 2115 after Task 2, 2115 after Task 3, 2129 after Task 4.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 39c2a95 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. The managing session brings them to the product owner:

1. **Restart the app with the usual launcher.** If there is no demo data, sign in with a team account and press "Reset demo data" at the end of the Clusters tab (it needs `DEMO_USERS_PASSWORD` for the demo accounts).
2. **As a demo cluster admin** (`demo.north.admin@example.test`, on the phone and on the computer):
   - **Registry:** "Manage" on your batteries' rows.
   - **Manage:** the battery's page opens. Change the state of charge and save: "1 value saved." Type 140 and save: it is refused and marked. Add an accident report (any small PDF). The modules and versions are shown, with nothing to edit.
   - **Detail page:** "Manage this battery" under "Back to summary".
   - **Help:** "Your cluster" now says how.
3. **As the demo cluster user** (`demo.north.user@example.test`): no "Manage" anywhere.
4. **As the team:** the battery page looks and works as before. The audit log names the cluster admin for their changes.
5. **Light and dark:** the moon button in the top bar switches.

## Next

Plan 4b (the status change and repairs) adds the battery status card to both battery pages, with the status-change demo battery.
