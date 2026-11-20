# DPP V3 Step 3d-2: The Reset Tool — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A small tool that the product owner runs to reset `battery_pass_v3`. It clears:
- the old app's data;
- the new model's test data (families, models, sites, batches, operators, batteries, versions, documents);
- the four sample API tokens.

It also applies the owner's decisions about accounts, clusters and tokens. It keeps the settings and the audit log, and it adds one "data reset" entry to the log. This plan writes and tests the tool only. **It never runs the tool against Atlas.** The owner's runs come after it (see "After this plan").

**Architecture:** A console project `tools/V3Reset` that references the web project, so it deletes accounts and clusters with the app's own code. Its parts:
- `ResetCollections`: what the reset does with each collection;
- `Inventory`: reads the database for the review;
- `DecisionsFile`: the CSV the owner edits;
- `ResetPlanner`: checks the decisions against the database;
- `ResetRunner`: the safety copy, the changes and the audit entry;
- `App`: the two commands and their refusals;
- `v3-reset.ps1`: the launcher that asks for the Atlas password, with a README.

**Tech Stack:** .NET 10, MongoDB.Driver 3.9 (through the web project), xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`: section 14, the 3d bullet (3d-2, the reset). Decisions record: decisions 1, 23 and 30. Earlier plans: steps 1–3d-1 in `docs/superpowers/plans/`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at c11f36b (3d-1's last commit) plus the commit that adds this plan. Check with `git branch --show-current`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Never run the tool or its launcher against Atlas.** The tests use a throwaway MongoDB (EphemeralMongo). Nothing in this plan touches `battery_pass_v3` or `battery_pass_demo`.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  1980`. After this plan: 2042. The suite takes about two minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../3d2-task1-tests.patch`), and run `git apply ../<name>` from the worktree root. New and rewritten files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (30 Oct 2026, through the managing session)

1. **Form:** a console tool in the repository with two commands, `inventory` and `reset`, plus a launcher that asks for the Atlas password each time. No mongosh.
   - The tool works only on `battery_pass_v3`.
   - `reset` is a dry run by default.
   - A real run needs `-Run` and the database name typed out.
2. **Account review:** `inventory` lists the accounts, clusters and tokens, and writes a decisions file.
   - The file has one row per account, cluster and token, with the default already filled in. Its first column is a short `keep`/`delete`, because the owner is often on a phone.
   - `reset` applies the file.
   - The owner makes new accounts on the Users page afterwards; no code creates real accounts.
3. **demo-cluster:** kept by default (the 28 Oct decision keeps users, clusters and tokens). It is listed in the review with a delete option.
4. **A safety copy** of everything removed or changed, written to a folder outside the repository before anything changes. The launcher says where it is and reminds the owner to delete it when satisfied.
5. **The audit log:** all three audit collections stay, and the reset adds one "data.reset" entry with the counts.
6. **Module data:** all models and batteries go (design 14). The code's module fields stay. Nothing is re-created.
7. Password reset requests are cleared, the four sample tokens always go, and collections the app doesn't know are listed and left alone.

## Judgement calls in this plan

1. **What happens to each collection:**
   - The new model's test data is emptied; the collections and their indexes stay for the app.
   - The old app's collections are dropped, since nothing reads them since 3d-1.
   - Password reset requests are emptied.
   - Accounts, memberships, clusters and tokens are kept as the decisions say.
   - The settings and the audit collections are kept.
   - Anything else is left alone.

   A test checks that every collection the app names has a known fate, so a later step that adds one must decide what the reset does with it.
2. **The app's own code does the deleting.** `ClusterRepository.DeleteClusterAsync` and `DeleteUserAsync` remove clusters and accounts together with their memberships, as the pages do. `ApplicationAuditService` writes the entry. The tool builds its own connection from `MONGODB_URI` and `MONGODB_DB`, with no fallback to `battery_pass_demo`.
3. **Refusals:**
   - `MONGODB_DB` must be exactly `battery_pass_v3`; the refusal names `battery_pass_demo` as the live app's database.
   - Without `--run`, `reset` only shows the plan.
   - With `--run`, it shows the plan and stops unless `battery_pass_v3` is typed.
   - The review and the copy each need a new, empty folder.
   - The launcher refuses while a `BatteryPassWeb` process runs: the tool builds the app's code, and nothing should write during a reset.
   - When Atlas can't be reached, the tool says so and points at Atlas's Network Access list.
4. **The decisions file** is a CSV with a byte order mark, so Excel shows names correctly. Its columns are `decision, kind, id, name, role, details`. Reading accepts commas or the semicolons Excel writes in some countries.
   - **Mistakes stop the reset** and name the line to fix: a word other than keep or delete, an unknown account, cluster or token, a repeated row, a role outside the eight account roles, or no account left with the admin role.
   - **A missing row** keeps its item as it is, with a note.
   - **Old `legitimateInterest` accounts** are pointed out in the details. Their role stays unless the owner changes it.
5. **Deleting a cluster** also deletes its memberships and the tokens that belong only to it: its default tokens, or a token that lists only that cluster. Team tokens stay.
6. **The safety copy** holds every document removed or changed, one JSON file per collection (MongoDB extended JSON). Its README says the folder holds password hashes and should be deleted when the owner is satisfied.
7. **Order of changes:** test data emptied, old data dropped, tokens, clusters, accounts, roles, then the audit entry.
8. **No app code changes.** The API help examples keep naming `demo-cluster`; if the owner deletes it, renaming the examples is a small follow-up in 3e.

## File structure

| File | Responsibility |
|---|---|
| `tools/V3Reset/V3Reset.csproj` | The console project; references the web project |
| `tools/V3Reset/ResetCollections.cs` | What the reset does with each collection |
| `tools/V3Reset/Inventory.cs` | Reads the collections, accounts, clusters and tokens (never token values or hashes) |
| `tools/V3Reset/DecisionsFile.cs` | Writes and reads the owner's decisions file |
| `tools/V3Reset/ResetPlanner.cs` | Checks the decisions and works out the plan |
| `tools/V3Reset/ResetRunner.cs` | Safety copy, changes, audit entry |
| `tools/V3Reset/App.cs`, `Program.cs` | The commands and their refusals |
| `tools/V3Reset/v3-reset.ps1`, `README.md` | The owner's launcher and steps |
| `BatteryPassWeb.Tests/Reset/*` | The tests, on a throwaway database shaped like `battery_pass_v3` |


---

### Task 1: The review: an inventory and a decisions file

The tool's first command reads `battery_pass_v3` and writes the review folder: `inventory.txt` and the decisions file. It changes nothing, and it refuses any database but `battery_pass_v3`.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
- Test: `BatteryPassWeb.Tests/Reset/DecisionsFileTests.cs`
- Test: `BatteryPassWeb.Tests/Reset/InventoryCommandTests.cs`
- Test: `BatteryPassWeb.Tests/Reset/InventoryTests.cs`
- Test: `BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs`
- Test: `BatteryPassWeb.Tests/Reset/ResetTesting.cs`
- Create: `tools/V3Reset/App.cs`
- Create: `tools/V3Reset/DecisionsFile.cs`
- Create: `tools/V3Reset/Inventory.cs`
- Create: `tools/V3Reset/Program.cs`
- Create: `tools/V3Reset/ResetCollections.cs`
- Create: `tools/V3Reset/V3Reset.csproj`

- [ ] **Step 1: Write the failing tests**

Patch `3d2-task1-tests.patch` (`BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`). Save the block as `../3d2-task1-tests.patch`, outside the worktree, and run `git apply ../3d2-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj b/BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj
--- a/BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj
+++ b/BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj
@@ -21,6 +21,7 @@
 
   <ItemGroup>
     <ProjectReference Include="..\web\BatteryPassWeb.csproj" />
+    <ProjectReference Include="..\tools\V3Reset\V3Reset.csproj" />
   </ItemGroup>
 
   <ItemGroup>
```

`BatteryPassWeb.Tests/Reset/DecisionsFileTests.cs`:

```csharp
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the decisions file, one row per account, cluster and token, easy to change on a phone.</summary>
public sealed class DecisionsFileTests
{
    private static readonly InventoryReport Inventory = new(
        [],
        [new AccountRow("ann@team.test", "Ann, Team", "admin", "north (clusterAdmin)", "2026-10-01T08:00:00Z")],
        [new ClusterRow("north", "Customer North", 2, 1, 4)],
        [
            new TokenRow("sample-read-token", "Sample read", "read", null, [], false, true, ""),
            new TokenRow("north-read", "North \"read\"", "read", "north", [], false, false, "2026-10-29T10:00:00Z"),
        ]);

    [Fact]
    public void Defaults_ShouldKeepEverythingButTheSampleTokens()
    {
        var rows = DecisionsFile.Defaults(Inventory);

        Assert.Equal(["keep account ann@team.test", "keep cluster north", "delete token sample-read-token", "keep token north-read"], rows.Select(row => $"{row.Decision} {row.Kind} {row.Id}"));
        Assert.Equal("admin", rows[0].Role);
        Assert.Equal("clusters: north (clusterAdmin); created 2026-10-01", rows[0].Details);
        Assert.Equal("2 members, 1 tokens; its 4 batteries go with the reset", rows[1].Details);
        Assert.Equal("sample token: always removed", rows[2].Details);
        Assert.Equal("read, cluster north, last used 2026-10-29", rows[3].Details);
    }

    [Fact]
    public void AnOldLegitimateInterestAccount_ShouldBePointedOut()
    {
        var inventory = Inventory with { Accounts = [new AccountRow("li@repair.test", "Old LI", "legitimateInterest", string.Empty, "2026-09-01T08:00:00Z")] };

        var row = DecisionsFile.Defaults(inventory)[0];

        Assert.Equal(("keep", "legitimateInterest"), (row.Decision, row.Role));
        Assert.Equal("old role legitimateInterest: public data only; give a new role or delete; no clusters; created 2026-09-01", row.Details);
    }

    [Fact]
    public void TheFile_ShouldStartWithTheDecisionColumn()
    {
        var text = DecisionsFile.Write(DecisionsFile.Defaults(Inventory));

        Assert.StartsWith("decision,kind,id,name,role,details\r\nkeep,account,ann@team.test,\"Ann, Team\",admin,", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WritingThenReading_ShouldGiveTheSameRows()
    {
        var rows = DecisionsFile.Defaults(Inventory);

        var read = DecisionsFile.Read(DecisionsFile.Write(rows));

        Assert.Equal(rows.Select(row => row with { Line = 0 }), read.Select(row => row with { Line = 0 }));
        Assert.Equal([2, 3, 4, 5], read.Select(row => row.Line));
    }

    [Fact]
    public void ExcelsSemicolonsAndByteOrderMark_ShouldBeRead()
    {
        var text = "﻿decision;kind;id;name;role;details\r\nDelete ;account;ann@team.test;Ann, Team;admin;x\r\n\r\n";

        var row = Assert.Single(DecisionsFile.Read(text));

        Assert.Equal(("delete", "account", "ann@team.test", "Ann, Team"), (row.Decision, row.Kind, row.Id, row.Name));
    }

    [Fact]
    public void AFileWithoutTheColumns_ShouldBeRefused()
    {
        var error = Assert.Throws<FormatException>(() => DecisionsFile.Read("keep,account,ann@team.test\r\n"));

        Assert.Contains("Start again from the file the inventory wrote", error.Message, StringComparison.Ordinal);
    }
}
```

`BatteryPassWeb.Tests/Reset/InventoryCommandTests.cs`:

```csharp
using System.Text;
using BatteryPassWeb.Tests.Dpp;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the tool works only on battery_pass_v3, and its inventory writes the review folder without changing anything.</summary>
[Collection(MongoCollection.Name)]
public sealed class InventoryCommandTests(MongoFixture mongo)
{
    [Theory]
    [InlineData("battery_pass_demo", "battery_pass_demo is the live app's database")]
    [InlineData("BATTERY_PASS_DEMO", "battery_pass_demo is the live app's database")]
    [InlineData("battery_pass_v3_copy", "works only on battery_pass_v3")]
    [InlineData("", "works only on battery_pass_v3")]
    public async Task AnyDatabaseButV3_ShouldBeRefused(string database, string message)
    {
        var output = new StringWriter();

        var code = await App.RunAsync(["inventory", "--out", ResetTesting.NewFolder()], ResetTesting.Environment("mongodb://127.0.0.1:1", database), new StringReader(string.Empty), output, "test");

        Assert.Equal(App.Refused, code);
        Assert.Contains(message, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownCommand_ShouldShowHowToUseTheTool()
    {
        var output = new StringWriter();

        var code = await App.RunAsync(["wipe"], ResetTesting.Environment("mongodb://127.0.0.1:1", "battery_pass_v3"), new StringReader(string.Empty), output, "test");

        Assert.Equal(App.Refused, code);
        Assert.Contains("Usage: V3Reset inventory --out <new folder>", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inventory_ShouldWriteTheReviewFolderAndChangeNothing()
    {
        var (database, uri) = await ResetTesting.FreshV3Async(mongo);
        var before = await ResetTesting.Snapshot(database);
        var folder = ResetTesting.NewFolder();
        var output = new StringWriter();

        var code = await App.RunAsync(["inventory", "--out", folder], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader(string.Empty), output, "test");

        Assert.Equal(App.Done, code);
        Assert.Equal(before, await ResetTesting.Snapshot(database));
        var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, "decisions.csv"));
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        var rows = DecisionsFile.Read(Encoding.UTF8.GetString(bytes));
        Assert.Equal(4 + 2 + 5, rows.Count);
        Assert.Contains("ownerNotes", await File.ReadAllTextAsync(Path.Combine(folder, "inventory.txt")), StringComparison.Ordinal);
        Assert.Contains("left alone (not known to the app)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Nothing was changed.", output.ToString(), StringComparison.Ordinal);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Inventory_ShouldNotWriteIntoAFolderThatIsInUse()
    {
        var (_, uri) = await ResetTesting.FreshV3Async(mongo);
        var folder = ResetTesting.NewFolder();
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "decisions.csv"), "the owner's edits");

        var code = await App.RunAsync(["inventory", "--out", folder], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader(string.Empty), new StringWriter(), "test");

        Assert.Equal(App.Refused, code);
        Assert.Equal("the owner's edits", await File.ReadAllTextAsync(Path.Combine(folder, "decisions.csv")));
        Directory.Delete(folder, recursive: true);
    }
}
```

`BatteryPassWeb.Tests/Reset/InventoryTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the inventory shows the owner what battery_pass_v3 holds, and only reads.</summary>
[Collection(MongoCollection.Name)]
public sealed class InventoryTests(MongoFixture mongo)
{
    [Fact]
    public async Task Inventory_ShouldSayWhatTheResetDoesWithEachCollection()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);

        var inventory = await Inventory.ReadAsync(database);

        var fates = inventory.Collections.ToDictionary(collection => collection.Name, collection => collection.Fate);
        Assert.Equal(CollectionFate.NewTestData, fates["dpp_packs"]);
        Assert.Equal(CollectionFate.OldData, fates["passports"]);
        Assert.Equal(CollectionFate.Cleared, fates["passwordResetRequests"]);
        Assert.Equal(CollectionFate.Reviewed, fates["users"]);
        Assert.Equal(CollectionFate.Kept, fates["auditEvents"]);
        Assert.Equal(CollectionFate.Unknown, fates["ownerNotes"]);
        Assert.Equal(3, inventory.Collections.Single(collection => collection.Name == "dpp_packs").Count);
    }

    [Fact]
    public async Task Inventory_ShouldListEachAccountWithItsRolesAndClusters()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);

        var inventory = await Inventory.ReadAsync(database);

        Assert.Equal(["admin@team.test", "demo@demo.test", "north-admin@north.test", "old-li@repair.test"], inventory.Accounts.Select(account => account.Email));
        var northAdmin = inventory.Accounts.Single(account => account.Email == "north-admin@north.test");
        Assert.Equal("member", northAdmin.Roles);
        Assert.Equal("north (clusterAdmin)", northAdmin.Memberships);
        Assert.Equal("legitimateInterest", inventory.Accounts.Single(account => account.Email == "old-li@repair.test").Roles);
    }

    [Fact]
    public async Task Inventory_ShouldCountEachClustersMembersTokensAndBatteries()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);

        var inventory = await Inventory.ReadAsync(database);

        var north = inventory.Clusters.Single(cluster => cluster.ClusterId == "north");
        Assert.Equal(("Customer North", 2L, 1L, 2L), (north.Name, north.Members, north.Tokens, north.Batteries));
        var demo = inventory.Clusters.Single(cluster => cluster.ClusterId == "demo-cluster");
        Assert.Equal((1L, 2L, 1L), (demo.Members, demo.Tokens, demo.Batteries));
    }

    [Fact]
    public async Task Inventory_ShouldListTokensButNeverTheirHashes()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);

        var inventory = await Inventory.ReadAsync(database);
        var file = DecisionsFile.Write(DecisionsFile.Defaults(inventory));

        Assert.Equal(5, inventory.Tokens.Count);
        Assert.True(inventory.Tokens.Single(token => token.TokenId == "sample-read-token").IsSample);
        Assert.DoesNotContain("secret-hash", file, StringComparison.Ordinal);
        Assert.DoesNotContain("hash-of", file, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inventory_ShouldChangeNothing()
    {
        var database = mongo.NewContext().Database!;
        await ResetTesting.SeedAsync(database);
        var before = await ResetTesting.Snapshot(database);

        await Inventory.ReadAsync(database);

        Assert.Equal(before, await ResetTesting.Snapshot(database));
    }
}
```

`BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs`:

```csharp
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the reset knows what to do with every collection the app uses (design 14).</summary>
public sealed class ResetCollectionsTests
{
    [Fact]
    public void EveryCollectionTheAppUses_ShouldHaveAKnownFate()
    {
        var web = Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Program.cs"))!;
        var named = Directory.GetFiles(web, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "GetCollection<[^>]*>\\(\"([^\"]+)\"\\)").Select(match => match.Groups[1].Value));
        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };

        foreach (var collection in named.Concat(dpp).Distinct())
        {
            Assert.True(ResetCollections.FateOf(collection) != CollectionFate.Unknown, $"The reset doesn't know {collection}");
        }
    }

    [Fact]
    public void TheNewModelsCollections_ShouldBeEmptiedAsTestData()
    {
        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
        {
            Assert.Equal(CollectionFate.NewTestData, ResetCollections.FateOf(collection));
        }
    }

    [Theory]
    [InlineData("passports", CollectionFate.OldData)]
    [InlineData("batteryProductTemplates", CollectionFate.OldData)]
    [InlineData("passportFiles.chunks", CollectionFate.OldData)]
    [InlineData("passwordResetRequests", CollectionFate.Cleared)]
    [InlineData("users", CollectionFate.Reviewed)]
    [InlineData("apiTokens", CollectionFate.Reviewed)]
    [InlineData("applicationAuditEvents", CollectionFate.Kept)]
    [InlineData("auditEvents", CollectionFate.Kept)]
    [InlineData("batteryAuditEvents", CollectionFate.Kept)]
    [InlineData("applicationSettings", CollectionFate.Kept)]
    [InlineData("ownerNotes", CollectionFate.Unknown)]
    public void EachCollection_ShouldHaveItsFate(string collection, CollectionFate fate)
    {
        Assert.Equal(fate, ResetCollections.FateOf(collection));
    }
}
```

`BatteryPassWeb.Tests/Reset/ResetTesting.cs`:

```csharp
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>A small database shaped like battery_pass_v3 before the reset, and helpers to look at it.</summary>
internal static class ResetTesting
{
    public static readonly string[] NewTestData = ["dpp_families", "dpp_models", "dpp_sites", "dpp_batches", "dpp_operators", "dpp_packs", "dpp_passport_versions", "dpp_documents", "dpp_files.files", "dpp_files.chunks"];
    public static readonly string[] OldData = ["passports", "batteries", "passportRevisions", "batteryProductTemplates", "passportFiles.files", "passportFiles.chunks"];
    public static readonly string[] Kept = ["applicationSettings", "applicationAuditEvents", "auditEvents", "batteryAuditEvents"];

    /// <summary>Four accounts, two clusters, five tokens, old and new data, the audit log and one collection the app doesn't know.</summary>
    public static async Task SeedAsync(IMongoDatabase database)
    {
        await Insert(database, "users",
            User("admin@team.test", "Team Admin", "admin"),
            User("north-admin@north.test", "North Admin", "member"),
            User("old-li@repair.test", "Old LI", "legitimateInterest"),
            User("demo@demo.test", "Demo User", "member"));
        await Insert(database, "clusterMemberships",
            Membership("north-admin@north.test", "north", "clusterAdmin"),
            Membership("old-li@repair.test", "north", "legitimateInterest"),
            Membership("demo@demo.test", "demo-cluster", "member"));
        await Insert(database, "clusters",
            new BsonDocument { ["clusterId"] = "north", ["name"] = "Customer North" },
            new BsonDocument { ["clusterId"] = "demo-cluster", ["name"] = "Demo cluster" });
        await Insert(database, "apiTokens",
            Token("sample-read-token", "Sample read", "read", isSample: true),
            Token("north-read", "North read", "read", autoClusterId: "north"),
            Token("demo-read", "Demo read", "read", autoClusterId: "demo-cluster"),
            Token("demo-listed", "Demo listed", "readWrite", clusterIds: ["demo-cluster"]),
            Token("team-full", "Team full access", "readWriteSign", globalAccess: true));
        await Insert(database, "passwordResetRequests", new BsonDocument { ["email"] = "demo@demo.test" });
        foreach (var collection in NewTestData.Where(name => name != "dpp_packs"))
        {
            await Insert(database, collection, new BsonDocument { ["note"] = "new test data" });
        }

        await Insert(database, "dpp_packs",
            new BsonDocument { ["ClusterId"] = "north", ["Values"] = new BsonDocument("stateOfCharge", 55) },
            new BsonDocument { ["ClusterId"] = "north" },
            new BsonDocument { ["ClusterId"] = "demo-cluster" });
        foreach (var collection in OldData)
        {
            await Insert(database, collection, new BsonDocument { ["note"] = "v1.2 data" });
        }

        foreach (var collection in Kept)
        {
            await Insert(database, collection, new BsonDocument { ["note"] = "kept" });
        }

        await Insert(database, "ownerNotes", new BsonDocument { ["note"] = "not the app's" });
    }

    public static async Task<long> Count(IMongoDatabase database, string collection) =>
        await database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    public static async Task<List<string>> CollectionNames(IMongoDatabase database) =>
        await (await database.ListCollectionNamesAsync()).ToListAsync();

    public static async Task<List<string>> Values(IMongoDatabase database, string collection, string field) =>
        (await database.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
            .Select(document => BsonHelpers.GetString(document, field)).Order(StringComparer.Ordinal).ToList();

    /// <summary>Every collection with its count, to show that nothing changed.</summary>
    public static async Task<Dictionary<string, long>> Snapshot(IMongoDatabase database)
    {
        var counts = new Dictionary<string, long>();
        foreach (var name in await CollectionNames(database))
        {
            counts[name] = await Count(database, name);
        }

        return counts;
    }

    /// <summary>The throwaway server's address, for the tool's own connection.</summary>
    public static string ServerUri(MongoContext context)
    {
        var server = context.Database!.Client.Settings.Server;
        return $"mongodb://{server.Host}:{server.Port}";
    }

    /// <summary>A fresh battery_pass_v3 on the throwaway server: the name the tool insists on.</summary>
    public static async Task<(IMongoDatabase Database, string Uri)> FreshV3Async(MongoFixture mongo)
    {
        var uri = ServerUri(mongo.NewContext());
        var client = new MongoClient(uri);
        await client.DropDatabaseAsync("battery_pass_v3");
        var database = client.GetDatabase("battery_pass_v3");
        await SeedAsync(database);
        return (database, uri);
    }

    public static Dictionary<string, string?> Environment(string uri, string database) =>
        new() { ["MONGODB_URI"] = uri, ["MONGODB_DB"] = database };

    public static string NewFolder() => Path.Combine(Path.GetTempPath(), $"v3reset-test-{Guid.NewGuid():N}");

    private static BsonDocument User(string email, string name, string role) => new()
    {
        ["email"] = email,
        ["name"] = name,
        ["roles"] = new BsonArray { role },
        ["passwordHash"] = $"hash-of-{email}",
        ["createdAt"] = "2026-10-01T08:00:00.0000000Z",
    };

    private static BsonDocument Membership(string email, string clusterId, string role) =>
        new() { ["email"] = email, ["clusterId"] = clusterId, ["role"] = role };

    private static BsonDocument Token(string tokenId, string name, string accessMode, bool isSample = false, string? autoClusterId = null, string[]? clusterIds = null, bool globalAccess = false)
    {
        var token = new BsonDocument
        {
            ["tokenId"] = tokenId,
            ["name"] = name,
            ["accessMode"] = accessMode,
            ["tokenHash"] = $"secret-hash-{tokenId}",
            ["isSample"] = isSample,
            ["globalAccess"] = globalAccess,
            ["clusterIds"] = new BsonArray(clusterIds ?? []),
        };
        if (autoClusterId is not null)
        {
            token["autoClusterId"] = autoClusterId;
        }

        return token;
    }

    private static Task Insert(IMongoDatabase database, string collection, params BsonDocument[] documents) =>
        database.GetCollection<BsonDocument>(collection).InsertManyAsync(documents);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Reset.DecisionsFileTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.InventoryCommandTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.InventoryTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetCollectionsTests"`
Expected: the build fails: `CollectionFate`, `InventoryReport` and the tool's other types don't exist yet (CS0246, CS0103).

- [ ] **Step 3: Write `App.cs`**

`tools/V3Reset/App.cs`:

```csharp
using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace V3Reset;

/// <summary>
/// The tool's command <c>inventory</c>: it only reads, and writes the review folder. It works only on battery_pass_v3
/// (design 14, 3d-2).
/// </summary>
public static class App
{
    public const int Done = 0;
    public const int Stopped = 1;
    public const int Refused = 2;

    private const string Usage =
        "Usage: V3Reset inventory --out <new folder>\n" +
        "       V3Reset reset --decisions <decisions.csv> [--run --copy <new folder>]";

    public static async Task<int> RunAsync(string[] args, IReadOnlyDictionary<string, string?> environment, TextReader input, TextWriter output, string actor)
    {
        var command = args.FirstOrDefault() ?? string.Empty;
        string? Option(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        if (command != "inventory")
        {
            await output.WriteLineAsync(Usage);
            return Refused;
        }

        var databaseName = environment.GetValueOrDefault("MONGODB_DB")?.Trim() ?? string.Empty;
        var uri = environment.GetValueOrDefault("MONGODB_URI") ?? string.Empty;
        if (databaseName.Equals(ResetCollections.LiveDatabase, StringComparison.OrdinalIgnoreCase))
        {
            await output.WriteLineAsync($"Refused: {ResetCollections.LiveDatabase} is the live app's database. This tool works only on {ResetCollections.Database}.");
            return Refused;
        }

        if (databaseName != ResetCollections.Database)
        {
            await output.WriteLineAsync($"Refused: MONGODB_DB is \"{databaseName}\". This tool works only on {ResetCollections.Database}.");
            return Refused;
        }

        if (string.IsNullOrWhiteSpace(uri))
        {
            await output.WriteLineAsync("Refused: MONGODB_URI is not set.");
            return Refused;
        }

        var context = new MongoContext(Options.Create(new BatteryPassOptions { MongoDbUri = uri, MongoDbName = databaseName }));
        try
        {
            return await InventoryAsync(context, Option("--out"), output);
        }
        catch (Exception exception) when (exception is TimeoutException or MongoDB.Driver.MongoException)
        {
            await output.WriteLineAsync($"Can't reach {ResetCollections.Database}: {exception.Message.Split('\n')[0]}");
            await output.WriteLineAsync("If this PC's internet address changed, add it in Atlas under Security > Network Access, then try again.");
            return Refused;
        }
    }

    private static async Task<int> InventoryAsync(MongoContext context, string? folder, TextWriter output)
    {
        if (context.Database is null)
        {
            await output.WriteLineAsync($"Can't connect: {context.ConnectionError?.Message}");
            return Refused;
        }

        if (folder is null || (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()))
        {
            await output.WriteLineAsync("Give a new, empty folder for the review: --out <folder>.");
            return Refused;
        }

        var inventory = await Inventory.ReadAsync(context.Database);
        Directory.CreateDirectory(folder);
        var decisions = Path.Combine(folder, "decisions.csv");
        await File.WriteAllTextAsync(decisions, DecisionsFile.Write(DecisionsFile.Defaults(inventory)), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var listing = DescribeInventory(inventory);
        await File.WriteAllTextAsync(Path.Combine(folder, "inventory.txt"), listing);
        await output.WriteAsync(listing);
        await output.WriteLineAsync($"Nothing was changed. Your decisions file: {decisions}");
        await output.WriteLineAsync("In it, change \"keep\" to \"delete\" for what should go, or an account's role; then run reset with that file for a dry run.");
        return Done;
    }

    private static string DescribeInventory(InventoryReport inventory)
    {
        var text = new StringBuilder();
        text.AppendLine($"{ResetCollections.Database}: what is in it and what a reset would do");
        text.AppendLine();
        foreach (var collection in inventory.Collections)
        {
            text.AppendLine($"  {collection.Name,-34} {collection.Count,7}  {ResetCollections.Describe(collection.Fate)}");
        }

        text.AppendLine();
        text.AppendLine($"Accounts ({inventory.Accounts.Count}):");
        foreach (var account in inventory.Accounts)
        {
            text.AppendLine($"  {account.Email}  {account.Name}  role: {account.Roles}  {(account.Memberships.Length == 0 ? "no clusters" : account.Memberships)}");
        }

        text.AppendLine($"Clusters ({inventory.Clusters.Count}):");
        foreach (var cluster in inventory.Clusters)
        {
            text.AppendLine($"  {cluster.ClusterId}  {cluster.Name}  {cluster.Members} members, {cluster.Tokens} tokens, {cluster.Batteries} batteries");
        }

        text.AppendLine($"API tokens ({inventory.Tokens.Count}; values and hashes are never shown):");
        foreach (var token in inventory.Tokens)
        {
            text.AppendLine($"  {token.TokenId}  {token.Name}  {token.AccessMode}{(token.IsSample ? "  sample: always removed" : string.Empty)}");
        }

        text.AppendLine();
        return text.ToString();
    }
}
```

- [ ] **Step 4: Write `DecisionsFile.cs`**

`tools/V3Reset/DecisionsFile.cs`:

```csharp
using System.Text;

namespace V3Reset;

/// <summary>One row of the decisions file: an account, a cluster or a token, and whether it stays.</summary>
public sealed record DecisionRow(string Decision, string Kind, string Id, string Name, string Role, string Details, int Line = 0);

/// <summary>
/// The owner's decisions file: a CSV with one row per account, cluster and token, the default already filled in.
/// It opens in Excel or on a phone; the owner changes "keep" to "delete", or an account's role. Reading accepts the
/// commas this tool writes and the semicolons Excel writes in some countries.
/// </summary>
public static class DecisionsFile
{
    public const string Keep = "keep";
    public const string Delete = "delete";
    public const string Account = "account";
    public const string Cluster = "cluster";
    public const string Token = "token";

    /// <summary>The v1.2 role the three LI roles replaced; the app gives it public data only (3a).</summary>
    public const string LegacyRole = "legitimateInterest";

    private static readonly string[] Columns = ["decision", "kind", "id", "name", "role", "details"];

    /// <summary>The rows for everything the inventory found, with the defaults filled in.</summary>
    public static IReadOnlyList<DecisionRow> Defaults(InventoryReport inventory)
    {
        var rows = new List<DecisionRow>();
        rows.AddRange(inventory.Accounts.Select(account => new DecisionRow(
            Keep,
            Account,
            account.Email,
            account.Name,
            account.Roles,
            (account.Roles.Contains(LegacyRole, StringComparison.Ordinal) ? $"old role {LegacyRole}: public data only; give a new role or delete; " : string.Empty)
            + $"{(account.Memberships.Length == 0 ? "no clusters" : "clusters: " + account.Memberships)}; created {Day(account.CreatedAt)}")));
        rows.AddRange(inventory.Clusters.Select(cluster => new DecisionRow(
            Keep,
            Cluster,
            cluster.ClusterId,
            cluster.Name,
            string.Empty,
            $"{cluster.Members} members, {cluster.Tokens} tokens; its {cluster.Batteries} batteries go with the reset")));
        rows.AddRange(inventory.Tokens.Select(token => new DecisionRow(
            token.IsSample ? Delete : Keep,
            Token,
            token.TokenId,
            token.Name,
            string.Empty,
            token.IsSample
                ? "sample token: always removed"
                : $"{token.AccessMode}, {Reach(token)}, last used {(token.LastUsedAt.Length == 0 ? "never" : Day(token.LastUsedAt))}")));
        return rows;
    }

    /// <summary>The file's text: a header and one line per row, separated by commas.</summary>
    public static string Write(IEnumerable<DecisionRow> rows)
    {
        var text = new StringBuilder();
        text.Append(string.Join(',', Columns)).Append("\r\n");
        foreach (var row in rows)
        {
            text.Append(string.Join(',', new[] { row.Decision, row.Kind, row.Id, row.Name, row.Role, row.Details }.Select(Quote))).Append("\r\n");
        }

        return text.ToString();
    }

    /// <summary>Reads the file back; a missing column or a line that can't be read is an error.</summary>
    public static IReadOnlyList<DecisionRow> Read(string text)
    {
        var lines = Records(text.TrimStart('﻿')).ToList();
        if (lines.Count > 0 && lines[0].Fields.Count == 1 && lines[0].Fields[0].StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count == 0)
        {
            throw new FormatException("The decisions file is empty.");
        }

        var header = lines[0].Fields.Select(field => field.Trim().ToLowerInvariant()).ToList();
        var missing = Columns.Where(column => !header.Contains(column)).ToList();
        if (missing.Count > 0)
        {
            throw new FormatException($"The decisions file has no {string.Join(", ", missing)} column. Start again from the file the inventory wrote.");
        }

        string Field(IReadOnlyList<string> fields, string column)
        {
            var index = header.IndexOf(column);
            return index < fields.Count ? fields[index].Trim() : string.Empty;
        }

        return lines.Skip(1)
            .Where(line => line.Fields.Any(field => field.Trim().Length > 0))
            .Select(line => new DecisionRow(
                Field(line.Fields, "decision").ToLowerInvariant(),
                Field(line.Fields, "kind").ToLowerInvariant(),
                Field(line.Fields, "id"),
                Field(line.Fields, "name"),
                Field(line.Fields, "role"),
                Field(line.Fields, "details"),
                line.Number))
            .ToList();
    }

    private static string Reach(TokenRow token) =>
        token.GlobalAccess ? "all clusters (team)"
        : token.AutoClusterId is not null ? $"cluster {token.AutoClusterId}"
        : token.ClusterIds.Count == 0 ? "no clusters"
        : $"clusters {string.Join(" ", token.ClusterIds)}";

    private static string Day(string timestamp) => timestamp.Length >= 10 ? timestamp[..10] : timestamp;

    private static string Quote(string field) =>
        field.IndexOfAny([',', ';', '"', '\r', '\n']) >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field;

    /// <summary>Splits CSV text into records, with quoted fields; the separator is the header line's comma or semicolon.</summary>
    private static IEnumerable<(int Number, IReadOnlyList<string> Fields)> Records(string text)
    {
        var firstLine = text.Split('\n')[0];
        var separator = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var line = 1;
        var start = 1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == separator)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n')
            {
                fields.Add(field.ToString().TrimEnd('\r'));
                yield return (start, fields);
                fields = [];
                field.Clear();
                line++;
                start = line;
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString().TrimEnd('\r'));
            yield return (start, fields);
        }
    }
}
```

- [ ] **Step 5: Write `Inventory.cs`**

`tools/V3Reset/Inventory.cs`:

```csharp
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace V3Reset;

public sealed record CollectionCount(string Name, long Count, CollectionFate Fate);

/// <summary>An account as the review shows it; its roles are the stored ones, joined with " + ".</summary>
public sealed record AccountRow(string Email, string Name, string Roles, string Memberships, string CreatedAt);

public sealed record ClusterRow(string ClusterId, string Name, long Members, long Tokens, long Batteries);

/// <summary>A token as the review shows it: never its value or hash.</summary>
public sealed record TokenRow(string TokenId, string Name, string AccessMode, string? AutoClusterId, IReadOnlyList<string> ClusterIds, bool GlobalAccess, bool IsSample, string LastUsedAt);

public sealed record InventoryReport(
    IReadOnlyList<CollectionCount> Collections,
    IReadOnlyList<AccountRow> Accounts,
    IReadOnlyList<ClusterRow> Clusters,
    IReadOnlyList<TokenRow> Tokens);

/// <summary>Reads what battery_pass_v3 holds, for the owner's review. It only reads.</summary>
public static class Inventory
{
    public static async Task<InventoryReport> ReadAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var names = await (await database.ListCollectionNamesAsync(cancellationToken: cancellationToken)).ToListAsync(cancellationToken);
        var collections = new List<CollectionCount>();
        foreach (var name in names.Where(name => !name.StartsWith("system.", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            var count = await database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: cancellationToken);
            collections.Add(new CollectionCount(name, count, ResetCollections.FateOf(name)));
        }

        var memberships = await All(database, "clusterMemberships", cancellationToken);
        var tokens = (await All(database, "apiTokens", cancellationToken)).Select(ToTokenRow).OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var accounts = (await All(database, "users", cancellationToken))
            .Select(user =>
            {
                var email = BsonHelpers.GetString(user, "email");
                var clusters = memberships
                    .Where(membership => BsonHelpers.GetString(membership, "email").Equals(email, StringComparison.OrdinalIgnoreCase))
                    .Select(membership => $"{BsonHelpers.GetString(membership, "clusterId")} ({BsonHelpers.GetString(membership, "role")})")
                    .Order(StringComparer.Ordinal);
                return new AccountRow(email, BsonHelpers.GetString(user, "name"), RolesText(user), string.Join("; ", clusters), BsonHelpers.GetString(user, "createdAt"));
            })
            .OrderBy(account => account.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var packs = database.GetCollection<BsonDocument>("dpp_packs");
        var clusters = new List<ClusterRow>();
        foreach (var cluster in await All(database, "clusters", cancellationToken))
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            clusters.Add(new ClusterRow(
                clusterId,
                BsonHelpers.GetString(cluster, "name"),
                memberships.Count(membership => BsonHelpers.GetString(membership, "clusterId") == clusterId),
                tokens.Count(token => BelongsTo(token, clusterId)),
                await packs.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ClusterId", clusterId), cancellationToken: cancellationToken)));
        }

        return new InventoryReport(collections, accounts, clusters.OrderBy(cluster => cluster.ClusterId, StringComparer.Ordinal).ToList(), tokens);
    }

    /// <summary>A token that belongs to one cluster only, so it goes when that cluster goes.</summary>
    public static bool BelongsTo(TokenRow token, string clusterId) =>
        token.AutoClusterId == clusterId || (token.AutoClusterId is null && token.ClusterIds.Count == 1 && token.ClusterIds[0] == clusterId);

    /// <summary>The stored roles, joined with " + ", as the decisions file shows them.</summary>
    public static string RolesText(BsonDocument user) =>
        user.TryGetValue("roles", out var roles) && roles is BsonArray array
            ? string.Join(" + ", array.Where(role => role.IsString).Select(role => role.AsString))
            : string.Empty;

    private static TokenRow ToTokenRow(BsonDocument token)
    {
        var clusterIds = token.TryGetValue("clusterIds", out var ids) && ids is BsonArray array
            ? array.Where(id => id.IsString).Select(id => id.AsString).ToList()
            : [];
        var autoClusterId = BsonHelpers.GetString(token, "autoClusterId");
        return new TokenRow(
            BsonHelpers.GetString(token, "tokenId"),
            BsonHelpers.GetString(token, "name"),
            BsonHelpers.GetString(token, "accessMode"),
            string.IsNullOrWhiteSpace(autoClusterId) ? null : autoClusterId,
            clusterIds,
            token.GetValue("globalAccess", false).ToBoolean(),
            token.GetValue("isSample", false).ToBoolean(),
            BsonHelpers.GetString(token, "lastUsedAt"));
    }

    private static Task<List<BsonDocument>> All(IMongoDatabase database, string collection, CancellationToken cancellationToken) =>
        database.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
}
```

- [ ] **Step 6: Write `Program.cs`**

`tools/V3Reset/Program.cs`:

```csharp
using System.Collections;
using V3Reset;

var environment = Environment.GetEnvironmentVariables()
    .Cast<DictionaryEntry>()
    .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value, StringComparer.OrdinalIgnoreCase);
return await App.RunAsync(args, environment, Console.In, Console.Out, $"v3-reset ({Environment.UserName})");
```

- [ ] **Step 7: Write `ResetCollections.cs`**

`tools/V3Reset/ResetCollections.cs`:

```csharp
namespace V3Reset;

/// <summary>What the reset does with a collection of battery_pass_v3 (design 14, 3d-2).</summary>
public enum CollectionFate
{
    /// <summary>The new model's test data: emptied; the app keeps the collections and their indexes.</summary>
    NewTestData,

    /// <summary>The v1.2 app's data, which nothing reads since 3d-1: dropped.</summary>
    OldData,

    /// <summary>Short-lived records: emptied.</summary>
    Cleared,

    /// <summary>Kept; the decisions file says which accounts, clusters and tokens go.</summary>
    Reviewed,

    /// <summary>Kept as they are.</summary>
    Kept,

    /// <summary>A collection the app doesn't know: listed and left alone.</summary>
    Unknown,
}

/// <summary>The collections the reset knows, by what it does with them.</summary>
public static class ResetCollections
{
    /// <summary>The only database the tool works on.</summary>
    public const string Database = "battery_pass_v3";

    /// <summary>The live app's database, refused by name.</summary>
    public const string LiveDatabase = "battery_pass_demo";

    private static readonly Dictionary<string, CollectionFate> Fates = new(StringComparer.Ordinal)
    {
        ["dpp_families"] = CollectionFate.NewTestData,
        ["dpp_models"] = CollectionFate.NewTestData,
        ["dpp_sites"] = CollectionFate.NewTestData,
        ["dpp_batches"] = CollectionFate.NewTestData,
        ["dpp_operators"] = CollectionFate.NewTestData,
        ["dpp_packs"] = CollectionFate.NewTestData,
        ["dpp_passport_versions"] = CollectionFate.NewTestData,
        ["dpp_documents"] = CollectionFate.NewTestData,
        ["dpp_files.files"] = CollectionFate.NewTestData,
        ["dpp_files.chunks"] = CollectionFate.NewTestData,
        ["passports"] = CollectionFate.OldData,
        ["batteries"] = CollectionFate.OldData,
        ["passportRevisions"] = CollectionFate.OldData,
        ["batteryProductTemplates"] = CollectionFate.OldData,
        ["batteryProductTemplateVersions"] = CollectionFate.OldData,
        ["batteryProductTemplatePushRuns"] = CollectionFate.OldData,
        ["batteryProductCompletionPolicies"] = CollectionFate.OldData,
        ["dataCompletionPolicies"] = CollectionFate.OldData,
        ["editableFieldPolicies"] = CollectionFate.OldData,
        ["batteryTelemetry"] = CollectionFate.OldData,
        ["passportFiles.files"] = CollectionFate.OldData,
        ["passportFiles.chunks"] = CollectionFate.OldData,
        ["passwordResetRequests"] = CollectionFate.Cleared,
        ["users"] = CollectionFate.Reviewed,
        ["clusterMemberships"] = CollectionFate.Reviewed,
        ["clusters"] = CollectionFate.Reviewed,
        ["apiTokens"] = CollectionFate.Reviewed,
        ["applicationSettings"] = CollectionFate.Kept,
        ["applicationAuditEvents"] = CollectionFate.Kept,
        ["auditEvents"] = CollectionFate.Kept,
        ["batteryAuditEvents"] = CollectionFate.Kept,
    };

    public static CollectionFate FateOf(string collection) =>
        Fates.TryGetValue(collection, out var fate) ? fate : CollectionFate.Unknown;

    public static string Describe(CollectionFate fate) => fate switch
    {
        CollectionFate.NewTestData => "emptied (new test data)",
        CollectionFate.OldData => "removed (the old app's data)",
        CollectionFate.Cleared => "emptied (short-lived)",
        CollectionFate.Reviewed => "kept, as your decisions say",
        CollectionFate.Kept => "kept",
        _ => "left alone (not known to the app)",
    };
}
```

- [ ] **Step 8: Write `V3Reset.csproj`**

`tools/V3Reset/V3Reset.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>V3Reset</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reuses the app's own account, cluster and audit code, so the reset deletes exactly as the app does. -->
    <ProjectReference Include="..\..\web\BatteryPassWeb.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 9: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Reset.DecisionsFileTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.InventoryCommandTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.InventoryTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetCollectionsTests"`
Expected: `Passed!  - Failed:     0, Passed:    31`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2011`, none failed.

- [ ] **Step 10: Commit**

```bash
git add BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj BatteryPassWeb.Tests/Reset/DecisionsFileTests.cs BatteryPassWeb.Tests/Reset/InventoryCommandTests.cs BatteryPassWeb.Tests/Reset/InventoryTests.cs BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs BatteryPassWeb.Tests/Reset/ResetTesting.cs tools/V3Reset/App.cs tools/V3Reset/DecisionsFile.cs tools/V3Reset/Inventory.cs tools/V3Reset/Program.cs tools/V3Reset/ResetCollections.cs tools/V3Reset/V3Reset.csproj
git commit -m "feat(dpp): the reset tool's review, an inventory and a decisions file" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 2: The reset: a dry run first, then a safety copy and the changes

The second command checks the decisions file against the database and shows the plan. With `--run` and the database name typed out, it writes the safety copy, makes the changes and adds the audit entry. `App.cs` is given whole again, with the reset added.

**Files:**
- Test: `BatteryPassWeb.Tests/Reset/ResetCommandTests.cs`
- Test: `BatteryPassWeb.Tests/Reset/ResetPlannerTests.cs`
- Test: `BatteryPassWeb.Tests/Reset/ResetRunnerTests.cs`
- Replace: `tools/V3Reset/App.cs`
- Create: `tools/V3Reset/ResetPlanner.cs`
- Create: `tools/V3Reset/ResetRunner.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Reset/ResetCommandTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: reset is a dry run unless told to run, and then needs the database name typed out.</summary>
[Collection(MongoCollection.Name)]
public sealed class ResetCommandTests(MongoFixture mongo)
{
    private async Task<(MongoDB.Driver.IMongoDatabase Database, string Uri, string Decisions)> ReviewedAsync(Func<string, string>? edit = null)
    {
        var (database, uri) = await ResetTesting.FreshV3Async(mongo);
        var review = ResetTesting.NewFolder();
        Assert.Equal(App.Done, await App.RunAsync(["inventory", "--out", review], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader(string.Empty), new StringWriter(), "test"));
        var decisions = Path.Combine(review, "decisions.csv");
        if (edit is not null)
        {
            await File.WriteAllTextAsync(decisions, edit(await File.ReadAllTextAsync(decisions)));
        }

        return (database, uri, decisions);
    }

    [Fact]
    public async Task WithoutRun_ShouldShowThePlanAndChangeNothing()
    {
        var (database, uri, decisions) = await ReviewedAsync();
        var before = await ResetTesting.Snapshot(database);
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", decisions], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader("battery_pass_v3\n"), output, "test");

        Assert.Equal(App.Done, code);
        Assert.Equal(before, await ResetTesting.Snapshot(database));
        Assert.Contains("empties dpp_packs (3)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("removes passports (1)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("deletes 1 token(s): Sample read (sample-read-token)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("leaves ownerNotes (1) alone", output.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("Dry run: nothing was changed." + Environment.NewLine, output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("yes")]
    [InlineData("battery_pass_demo")]
    public async Task TheRealRun_ShouldStopUnlessTheDatabaseNameIsTyped(string typed)
    {
        var (database, uri, decisions) = await ReviewedAsync();
        var before = await ResetTesting.Snapshot(database);
        var copy = ResetTesting.NewFolder();
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", decisions, "--run", "--copy", copy], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader(typed + "\n"), output, "test");

        Assert.Equal(App.Stopped, code);
        Assert.Equal(before, await ResetTesting.Snapshot(database));
        Assert.False(Directory.Exists(copy));
        Assert.Contains("Stopped: nothing was changed.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRealRun_WithTheNameTyped_ShouldResetAndSayWhereTheCopyIs()
    {
        var (database, uri, decisions) = await ReviewedAsync();
        var copy = ResetTesting.NewFolder();
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", decisions, "--run", "--copy", copy], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader("battery_pass_v3\n"), output, "test");

        Assert.Equal(App.Done, code);
        Assert.Equal(0, await ResetTesting.Count(database, "dpp_packs"));
        Assert.DoesNotContain("passports", await ResetTesting.CollectionNames(database));
        Assert.Equal(4, await ResetTesting.Count(database, "users"));
        Assert.Contains($"Safety copy of everything removed or changed: {copy}", output.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(copy, "dpp_packs.json")));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task AMistakeInTheFile_ShouldStopBeforeAnything()
    {
        var (database, uri, decisions) = await ReviewedAsync(text => text.Replace("keep,account,demo@demo.test", "remove,account,demo@demo.test", StringComparison.Ordinal));
        var before = await ResetTesting.Snapshot(database);
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", decisions, "--run", "--copy", ResetTesting.NewFolder()], ResetTesting.Environment(uri, "battery_pass_v3"), new StringReader("battery_pass_v3\n"), output, "test");

        Assert.Equal(App.Stopped, code);
        Assert.Equal(before, await ResetTesting.Snapshot(database));
        Assert.Contains("The decisions file needs a fix; nothing was changed:", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"remove\" should be keep or delete", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLiveDatabase_ShouldBeRefusedForAResetToo()
    {
        var output = new StringWriter();

        var code = await App.RunAsync(["reset", "--decisions", "x.csv", "--run", "--copy", "y"], ResetTesting.Environment("mongodb://127.0.0.1:1", "battery_pass_demo"), new StringReader("battery_pass_demo\n"), output, "test");

        Assert.Equal(App.Refused, code);
        Assert.Contains("battery_pass_demo is the live app's database", output.ToString(), StringComparison.Ordinal);
    }
}
```

`BatteryPassWeb.Tests/Reset/ResetPlannerTests.cs`:

```csharp
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the owner's decisions are checked against the database before anything changes.</summary>
public sealed class ResetPlannerTests
{
    private static readonly InventoryReport Inventory = new(
        [
            new CollectionCount("dpp_packs", 3, CollectionFate.NewTestData),
            new CollectionCount("passwordResetRequests", 1, CollectionFate.Cleared),
            new CollectionCount("passports", 2, CollectionFate.OldData),
            new CollectionCount("users", 3, CollectionFate.Reviewed),
            new CollectionCount("auditEvents", 9, CollectionFate.Kept),
            new CollectionCount("ownerNotes", 1, CollectionFate.Unknown),
        ],
        [
            new AccountRow("admin@team.test", "Team Admin", "admin", string.Empty, string.Empty),
            new AccountRow("north@north.test", "North", "member", "north (clusterAdmin)", string.Empty),
            new AccountRow("old-li@repair.test", "Old LI", "legitimateInterest", string.Empty, string.Empty),
        ],
        [new ClusterRow("north", "Customer North", 1, 1, 2), new ClusterRow("demo-cluster", "Demo", 0, 2, 1)],
        [
            new TokenRow("sample-read-token", "Sample", "read", null, [], false, true, string.Empty),
            new TokenRow("north-read", "North read", "read", "north", [], false, false, string.Empty),
            new TokenRow("demo-read", "Demo read", "read", "demo-cluster", [], false, false, string.Empty),
            new TokenRow("demo-listed", "Demo listed", "readWrite", null, ["demo-cluster"], false, false, string.Empty),
            new TokenRow("team-full", "Team", "readWriteSign", null, [], true, false, string.Empty),
        ]);

    private static List<DecisionRow> Defaults() => DecisionsFile.Defaults(Inventory).Select((row, index) => row with { Line = index + 2 }).ToList();

    private static List<DecisionRow> With(string kind, string id, string decision, string? role = null) =>
        Defaults().Select(row => row.Kind == kind && row.Id == id ? row with { Decision = decision, Role = role ?? row.Role } : row).ToList();

    [Fact]
    public void TheDefaults_ShouldOnlyRemoveDataAndTheSampleTokens()
    {
        var (plan, errors) = ResetPlanner.Build(Inventory, Defaults());

        Assert.Empty(errors);
        Assert.Equal(["dpp_packs", "passwordResetRequests"], plan!.Emptied.Select(collection => collection.Name));
        Assert.Equal(["passports"], plan.Removed.Select(collection => collection.Name));
        Assert.Equal(["ownerNotes"], plan.LeftAlone.Select(collection => collection.Name));
        Assert.Empty(plan.AccountsDeleted);
        Assert.Empty(plan.RoleChanges);
        Assert.Empty(plan.ClustersDeleted);
        Assert.Equal(["sample-read-token"], plan.TokensDeleted);
    }

    [Fact]
    public void DeletingACluster_ShouldTakeTheTokensThatBelongOnlyToIt()
    {
        var (plan, errors) = ResetPlanner.Build(Inventory, With("cluster", "demo-cluster", "delete"));

        Assert.Empty(errors);
        Assert.Equal(["demo-cluster"], plan!.ClustersDeleted);
        Assert.Equal(["demo-listed", "demo-read", "sample-read-token"], plan.TokensDeleted.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnAccountMarkedDelete_ShouldBeDeleted()
    {
        var (plan, _) = ResetPlanner.Build(Inventory, With("account", "old-li@repair.test", "delete"));

        Assert.Equal(["old-li@repair.test"], plan!.AccountsDeleted);
    }

    [Fact]
    public void ANewRole_ShouldBeGivenAndAnUnchangedOldRoleLeftAsItIs()
    {
        var (plan, errors) = ResetPlanner.Build(Inventory, With("account", "north@north.test", "keep", "liRepair + member"));

        Assert.Empty(errors);
        var change = Assert.Single(plan!.RoleChanges);
        Assert.Equal(("north@north.test", "member"), (change.Email, change.From));
        Assert.Equal(["liRepair", "member"], change.To);
    }

    [Theory]
    [InlineData("account", "north@north.test", "remove", null, "\"remove\" should be keep or delete")]
    [InlineData("account", "north@north.test", "keep", "legitimateInterest", "isn't a role")]
    [InlineData("account", "north@north.test", "keep", "boss", "isn't a role")]
    [InlineData("account", "admin@team.test", "delete", null, "No account would have the admin role")]
    [InlineData("account", "admin@team.test", "keep", "member", "No account would have the admin role")]
    public void AMistake_ShouldStopTheReset(string kind, string id, string decision, string? role, string message)
    {
        var (plan, errors) = ResetPlanner.Build(Inventory, With(kind, id, decision, role));

        Assert.Null(plan);
        Assert.Contains(errors, error => error.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void ARowForSomethingThatIsntThere_ShouldStopTheReset()
    {
        var rows = Defaults();
        rows.Add(new DecisionRow("delete", "account", "nobody@x.test", string.Empty, string.Empty, string.Empty, 99));
        rows.Add(new DecisionRow("delete", "cluster", "north", string.Empty, string.Empty, string.Empty, 100));

        var (plan, errors) = ResetPlanner.Build(Inventory, rows);

        Assert.Null(plan);
        Assert.Equal(["Line 99 (account nobody@x.test): there is no such account.", "Line 100 (cluster north): this cluster has more than one row."], errors);
    }

    [Fact]
    public void WhatHasNoRow_ShouldStayAsItIs()
    {
        var rows = Defaults().Where(row => row.Id is not ("north@north.test" or "north")).ToList();

        var (plan, errors) = ResetPlanner.Build(Inventory, rows);

        Assert.Empty(errors);
        Assert.Empty(plan!.AccountsDeleted);
        Assert.Contains("Account north@north.test has no row in the decisions file: it stays as it is.", plan.Notes);
        Assert.Contains("Cluster north has no row in the decisions file: it stays as it is.", plan.Notes);
    }

    [Fact]
    public void ASampleTokenMarkedKeep_ShouldStillGo()
    {
        var (plan, _) = ResetPlanner.Build(Inventory, With("token", "sample-read-token", "keep"));

        Assert.Contains("sample-read-token", plan!.TokensDeleted);
        Assert.Contains("Token sample-read-token is a sample token: it is removed anyway.", plan.Notes);
    }
}
```

`BatteryPassWeb.Tests/Reset/ResetRunnerTests.cs`:

```csharp
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using MongoDB.Bson;
using MongoDB.Driver;
using V3Reset;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the reset removes the old and the test data, applies the decisions, keeps the rest, copies first and logs once.</summary>
[Collection(MongoCollection.Name)]
public sealed class ResetRunnerTests(MongoFixture mongo)
{
    private async Task<(MongoContext Context, IMongoDatabase Database, ResetPlan Plan, string Copy)> PlannedAsync(Func<DecisionRow, DecisionRow>? decide = null)
    {
        var context = mongo.NewContext();
        var database = context.Database!;
        await ResetTesting.SeedAsync(database);
        var inventory = await Inventory.ReadAsync(database);
        var rows = DecisionsFile.Defaults(inventory).Select(decide ?? (row => row)).ToList();
        var (plan, errors) = ResetPlanner.Build(inventory, rows);
        Assert.Empty(errors);
        return (context, database, plan!, ResetTesting.NewFolder());
    }

    [Fact]
    public async Task TheReset_ShouldEmptyTheTestDataAndRemoveTheOldData()
    {
        var (context, database, plan, copy) = await PlannedAsync();

        await ResetRunner.RunAsync(context, plan, copy, "test");

        var names = await ResetTesting.CollectionNames(database);
        foreach (var collection in ResetTesting.NewTestData.Append("passwordResetRequests"))
        {
            Assert.Contains(collection, names);
            Assert.Equal(0, await ResetTesting.Count(database, collection));
        }

        Assert.DoesNotContain(names, name => ResetTesting.OldData.Contains(name));
        Assert.Equal(1, await ResetTesting.Count(database, "ownerNotes"));
        Assert.Equal(1, await ResetTesting.Count(database, "applicationSettings"));
        Assert.Equal(1, await ResetTesting.Count(database, "auditEvents"));
        Assert.Equal(1, await ResetTesting.Count(database, "batteryAuditEvents"));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task TheReset_ShouldKeepAccountsClustersAndRealTokensUnlessMarkedDelete()
    {
        var (context, database, plan, copy) = await PlannedAsync();

        await ResetRunner.RunAsync(context, plan, copy, "test");

        Assert.Equal(["admin@team.test", "demo@demo.test", "north-admin@north.test", "old-li@repair.test"], await ResetTesting.Values(database, "users", "email"));
        Assert.Equal(3, await ResetTesting.Count(database, "clusterMemberships"));
        Assert.Equal(["demo-cluster", "north"], await ResetTesting.Values(database, "clusters", "clusterId"));
        Assert.Equal(["demo-listed", "demo-read", "north-read", "team-full"], await ResetTesting.Values(database, "apiTokens", "tokenId"));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task TheReset_ShouldApplyTheOwnersDecisions()
    {
        var (context, database, plan, copy) = await PlannedAsync(row => row switch
        {
            { Kind: "account", Id: "demo@demo.test" } => row with { Decision = "delete" },
            { Kind: "account", Id: "old-li@repair.test" } => row with { Role = "liRepair" },
            { Kind: "cluster", Id: "demo-cluster" } => row with { Decision = "delete" },
            { Kind: "token", Id: "north-read" } => row with { Decision = "delete" },
            _ => row,
        });

        await ResetRunner.RunAsync(context, plan, copy, "test");

        Assert.Equal(["admin@team.test", "north-admin@north.test", "old-li@repair.test"], await ResetTesting.Values(database, "users", "email"));
        var oldLi = await database.GetCollection<BsonDocument>("users").Find(Builders<BsonDocument>.Filter.Eq("email", "old-li@repair.test")).SingleAsync();
        Assert.Equal(new BsonArray { "liRepair" }, oldLi["roles"]);
        Assert.Equal("hash-of-old-li@repair.test", oldLi["passwordHash"].AsString);
        Assert.Equal(["north", "north"], await ResetTesting.Values(database, "clusterMemberships", "clusterId"));
        Assert.Equal(["north"], await ResetTesting.Values(database, "clusters", "clusterId"));
        Assert.Equal(["team-full"], await ResetTesting.Values(database, "apiTokens", "tokenId"));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task TheSafetyCopy_ShouldHoldEverythingRemovedOrChanged()
    {
        var (context, _, plan, copy) = await PlannedAsync(row => row switch
        {
            { Kind: "account", Id: "demo@demo.test" } => row with { Decision = "delete" },
            { Kind: "account", Id: "old-li@repair.test" } => row with { Role = "liRepair" },
            _ => row,
        });

        await ResetRunner.RunAsync(context, plan, copy, "test");

        Assert.Equal(3, BsonSerializerArray(Path.Combine(copy, "dpp_packs.json")).Count);
        Assert.Single(BsonSerializerArray(Path.Combine(copy, "passports.json")));
        Assert.Equal(["demo@demo.test", "old-li@repair.test"], BsonSerializerArray(Path.Combine(copy, "users.deleted-or-changed.json")).Select(user => user["email"].AsString).Order(StringComparer.Ordinal));
        Assert.Equal(["sample-read-token"], BsonSerializerArray(Path.Combine(copy, "apiTokens.deleted.json")).Select(token => token["tokenId"].AsString));
        Assert.Contains("delete the folder once you're satisfied", await File.ReadAllTextAsync(Path.Combine(copy, "README.txt")), StringComparison.Ordinal);
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task AFolderInUse_ShouldStopTheResetBeforeAnyChange()
    {
        var (context, database, plan, copy) = await PlannedAsync();
        Directory.CreateDirectory(copy);
        await File.WriteAllTextAsync(Path.Combine(copy, "older.json"), "[]");
        var before = await ResetTesting.Snapshot(database);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ResetRunner.RunAsync(context, plan, copy, "test"));

        Assert.Equal(before, await ResetTesting.Snapshot(database));
        Directory.Delete(copy, recursive: true);
    }

    [Fact]
    public async Task TheReset_ShouldAddOneAuditEntryWithTheCounts()
    {
        var (context, database, plan, copy) = await PlannedAsync();

        await ResetRunner.RunAsync(context, plan, copy, "v3-reset (owner)");

        var entries = await database.GetCollection<BsonDocument>("applicationAuditEvents").Find(Builders<BsonDocument>.Filter.Eq("eventType", ResetRunner.AuditEventType)).ToListAsync();
        var entry = Assert.Single(entries);
        Assert.Equal("v3-reset (owner)", entry["actor"].AsString);
        Assert.Equal(3, entry["metadata"]["emptied"]["dpp_packs"].ToInt64());
        Assert.Equal(1, entry["metadata"]["removed"]["passports"].ToInt64());
        Assert.Equal(1, entry["metadata"]["tokensDeleted"].ToInt32());
        Assert.Equal(2, await ResetTesting.Count(database, "applicationAuditEvents"));
        Directory.Delete(copy, recursive: true);
    }

    private static List<BsonDocument> BsonSerializerArray(string path) =>
        MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonArray>(File.ReadAllText(path)).Select(value => value.AsBsonDocument).ToList();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetCommandTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetPlannerTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetRunnerTests"`
Expected: the build fails: `ResetPlan` and the reset's other types don't exist yet (CS0246).

- [ ] **Step 3: Replace `App.cs`**

`tools/V3Reset/App.cs`:

```csharp
using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace V3Reset;

/// <summary>
/// The tool's commands. <c>inventory</c> only reads and writes the review folder. <c>reset</c> is a dry run unless
/// given <c>--run</c>, and then asks for the database name typed out before it changes anything. Both work only on
/// battery_pass_v3 (design 14, 3d-2).
/// </summary>
public static class App
{
    public const int Done = 0;
    public const int Stopped = 1;
    public const int Refused = 2;

    private const string Usage =
        "Usage: V3Reset inventory --out <new folder>\n" +
        "       V3Reset reset --decisions <decisions.csv> [--run --copy <new folder>]";

    public static async Task<int> RunAsync(string[] args, IReadOnlyDictionary<string, string?> environment, TextReader input, TextWriter output, string actor)
    {
        var command = args.FirstOrDefault() ?? string.Empty;
        string? Option(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        if (command is not ("inventory" or "reset"))
        {
            await output.WriteLineAsync(Usage);
            return Refused;
        }

        var databaseName = environment.GetValueOrDefault("MONGODB_DB")?.Trim() ?? string.Empty;
        var uri = environment.GetValueOrDefault("MONGODB_URI") ?? string.Empty;
        if (databaseName.Equals(ResetCollections.LiveDatabase, StringComparison.OrdinalIgnoreCase))
        {
            await output.WriteLineAsync($"Refused: {ResetCollections.LiveDatabase} is the live app's database. This tool works only on {ResetCollections.Database}.");
            return Refused;
        }

        if (databaseName != ResetCollections.Database)
        {
            await output.WriteLineAsync($"Refused: MONGODB_DB is \"{databaseName}\". This tool works only on {ResetCollections.Database}.");
            return Refused;
        }

        if (string.IsNullOrWhiteSpace(uri))
        {
            await output.WriteLineAsync("Refused: MONGODB_URI is not set.");
            return Refused;
        }

        var context = new MongoContext(Options.Create(new BatteryPassOptions { MongoDbUri = uri, MongoDbName = databaseName }));
        try
        {
            return command == "inventory"
                ? await InventoryAsync(context, Option("--out"), output)
                : await ResetAsync(context, args, Option("--decisions"), Option("--copy"), input, output, actor);
        }
        catch (Exception exception) when (exception is TimeoutException or MongoDB.Driver.MongoException)
        {
            await output.WriteLineAsync($"Can't reach {ResetCollections.Database}: {exception.Message.Split('\n')[0]}");
            await output.WriteLineAsync("If this PC's internet address changed, add it in Atlas under Security > Network Access, then try again.");
            return Refused;
        }
    }

    private static async Task<int> InventoryAsync(MongoContext context, string? folder, TextWriter output)
    {
        if (context.Database is null)
        {
            await output.WriteLineAsync($"Can't connect: {context.ConnectionError?.Message}");
            return Refused;
        }

        if (folder is null || (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()))
        {
            await output.WriteLineAsync("Give a new, empty folder for the review: --out <folder>.");
            return Refused;
        }

        var inventory = await Inventory.ReadAsync(context.Database);
        Directory.CreateDirectory(folder);
        var decisions = Path.Combine(folder, "decisions.csv");
        await File.WriteAllTextAsync(decisions, DecisionsFile.Write(DecisionsFile.Defaults(inventory)), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var listing = DescribeInventory(inventory);
        await File.WriteAllTextAsync(Path.Combine(folder, "inventory.txt"), listing);
        await output.WriteAsync(listing);
        await output.WriteLineAsync($"Nothing was changed. Your decisions file: {decisions}");
        await output.WriteLineAsync("In it, change \"keep\" to \"delete\" for what should go, or an account's role; then run reset with that file for a dry run.");
        return Done;
    }

    private static async Task<int> ResetAsync(MongoContext context, string[] args, string? decisionsPath, string? copy, TextReader input, TextWriter output, string actor)
    {
        if (context.Database is null)
        {
            await output.WriteLineAsync($"Can't connect: {context.ConnectionError?.Message}");
            return Refused;
        }

        if (decisionsPath is null || !File.Exists(decisionsPath))
        {
            await output.WriteLineAsync("Give the decisions file from the inventory: --decisions <decisions.csv>.");
            return Refused;
        }

        IReadOnlyList<DecisionRow> rows;
        try
        {
            rows = DecisionsFile.Read(await File.ReadAllTextAsync(decisionsPath));
        }
        catch (FormatException exception)
        {
            await output.WriteLineAsync(exception.Message);
            return Stopped;
        }

        var inventory = await Inventory.ReadAsync(context.Database);
        var (plan, errors) = ResetPlanner.Build(inventory, rows);
        if (plan is null)
        {
            await output.WriteLineAsync("The decisions file needs a fix; nothing was changed:");
            foreach (var error in errors)
            {
                await output.WriteLineAsync($"  - {error}");
            }

            return Stopped;
        }

        await output.WriteAsync(DescribePlan(plan, inventory));
        if (!args.Contains("--run"))
        {
            await output.WriteLineAsync("Dry run: nothing was changed.");
            return Done;
        }

        if (copy is null)
        {
            await output.WriteLineAsync("Give a new folder for the safety copy: --copy <folder>.");
            return Refused;
        }

        await output.WriteLineAsync($"Type {ResetCollections.Database} to make these changes, or anything else to stop:");
        if ((await input.ReadLineAsync())?.Trim() != ResetCollections.Database)
        {
            await output.WriteLineAsync("Stopped: nothing was changed.");
            return Stopped;
        }

        await ResetRunner.RunAsync(context, plan, copy, actor);
        await output.WriteLineAsync($"Done. The audit log has a \"{ResetRunner.AuditEventType}\" entry with the counts.");
        await output.WriteLineAsync($"Safety copy of everything removed or changed: {copy}");
        return Done;
    }

    private static string DescribeInventory(InventoryReport inventory)
    {
        var text = new StringBuilder();
        text.AppendLine($"{ResetCollections.Database}: what is in it and what a reset would do");
        text.AppendLine();
        foreach (var collection in inventory.Collections)
        {
            text.AppendLine($"  {collection.Name,-34} {collection.Count,7}  {ResetCollections.Describe(collection.Fate)}");
        }

        text.AppendLine();
        text.AppendLine($"Accounts ({inventory.Accounts.Count}):");
        foreach (var account in inventory.Accounts)
        {
            text.AppendLine($"  {account.Email}  {account.Name}  role: {account.Roles}  {(account.Memberships.Length == 0 ? "no clusters" : account.Memberships)}");
        }

        text.AppendLine($"Clusters ({inventory.Clusters.Count}):");
        foreach (var cluster in inventory.Clusters)
        {
            text.AppendLine($"  {cluster.ClusterId}  {cluster.Name}  {cluster.Members} members, {cluster.Tokens} tokens, {cluster.Batteries} batteries");
        }

        text.AppendLine($"API tokens ({inventory.Tokens.Count}; values and hashes are never shown):");
        foreach (var token in inventory.Tokens)
        {
            text.AppendLine($"  {token.TokenId}  {token.Name}  {token.AccessMode}{(token.IsSample ? "  sample: always removed" : string.Empty)}");
        }

        text.AppendLine();
        return text.ToString();
    }

    private static string DescribePlan(ResetPlan plan, InventoryReport inventory)
    {
        string TokenName(string tokenId) =>
            inventory.Tokens.FirstOrDefault(token => token.TokenId == tokenId) is { } token && token.Name != tokenId ? $"{token.Name} ({tokenId})" : tokenId;
        var text = new StringBuilder();
        text.AppendLine($"What the reset does to {ResetCollections.Database}:");
        foreach (var collection in plan.Emptied)
        {
            text.AppendLine($"  empties {collection.Name} ({collection.Count})");
        }

        foreach (var collection in plan.Removed)
        {
            text.AppendLine($"  removes {collection.Name} ({collection.Count})");
        }

        text.AppendLine($"  deletes {plan.AccountsDeleted.Count} account(s){List(plan.AccountsDeleted)}");
        foreach (var change in plan.RoleChanges)
        {
            text.AppendLine($"  changes {change.Email}'s role from {(change.From.Length == 0 ? "none" : change.From)} to {string.Join(" + ", change.To)}");
        }

        text.AppendLine($"  deletes {plan.ClustersDeleted.Count} cluster(s){List(plan.ClustersDeleted)}, with their memberships and tokens");
        text.AppendLine($"  deletes {plan.TokensDeleted.Count} token(s){List(plan.TokensDeleted.Select(TokenName).ToList())}");
        foreach (var collection in plan.LeftAlone)
        {
            text.AppendLine($"  leaves {collection.Name} ({collection.Count}) alone: the app doesn't know it");
        }

        text.AppendLine("  keeps everything else: accounts, clusters and tokens not marked delete, the settings and the audit log");
        foreach (var note in plan.Notes)
        {
            text.AppendLine($"  note: {note}");
        }

        return text.ToString();
    }

    private static string List(IReadOnlyList<string> items) => items.Count == 0 ? string.Empty : $": {string.Join(", ", items)}";
}
```

- [ ] **Step 4: Write `ResetPlanner.cs`**

`tools/V3Reset/ResetPlanner.cs`:

```csharp
using BatteryPassWeb.Services;

namespace V3Reset;

public sealed record RoleChange(string Email, string From, IReadOnlyList<string> To);

/// <summary>What a reset will do, worked out from the database as it is now and the owner's decisions.</summary>
public sealed record ResetPlan(
    IReadOnlyList<CollectionCount> Emptied,
    IReadOnlyList<CollectionCount> Removed,
    IReadOnlyList<CollectionCount> LeftAlone,
    IReadOnlyList<string> AccountsDeleted,
    IReadOnlyList<RoleChange> RoleChanges,
    IReadOnlyList<string> ClustersDeleted,
    IReadOnlyList<string> TokensDeleted,
    IReadOnlyList<string> Notes);

/// <summary>Checks the decisions against the database and turns them into a plan; any mistake stops the reset.</summary>
public static class ResetPlanner
{
    /// <summary>The roles an account can be given (design 5.2): the team, members and the app-wide reader roles.</summary>
    public static readonly IReadOnlyList<string> SystemRoles =
    [
        AccessControlService.RoleAdmin,
        AccessControlService.RoleNormalUser,
        AccessControlService.RoleNotifiedBody,
        AccessControlService.RoleMarketSurveillanceAuthority,
        AccessControlService.RoleCommission,
        AccessControlService.RoleLiRepair,
        AccessControlService.RoleLiRecycling,
        AccessControlService.RoleLiTreatment,
    ];

    public static (ResetPlan? Plan, IReadOnlyList<string> Errors) Build(InventoryReport inventory, IReadOnlyList<DecisionRow> rows)
    {
        var errors = new List<string>();
        var notes = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var accountsDeleted = new List<string>();
        var roleChanges = new List<RoleChange>();
        var clustersDeleted = new List<string>();
        var tokensDeleted = new List<string>();

        foreach (var row in rows)
        {
            var where = $"Line {row.Line} ({row.Kind} {row.Id})";
            if (row.Decision is not (DecisionsFile.Keep or DecisionsFile.Delete))
            {
                errors.Add($"{where}: \"{row.Decision}\" should be keep or delete.");
                continue;
            }

            if (!seen.Add($"{row.Kind}|{row.Id}"))
            {
                errors.Add($"{where}: this {row.Kind} has more than one row.");
                continue;
            }

            switch (row.Kind)
            {
                case DecisionsFile.Account:
                    var account = inventory.Accounts.FirstOrDefault(found => found.Email.Equals(row.Id, StringComparison.OrdinalIgnoreCase));
                    if (account is null)
                    {
                        errors.Add($"{where}: there is no such account.");
                    }
                    else if (row.Decision == DecisionsFile.Delete)
                    {
                        accountsDeleted.Add(account.Email);
                    }
                    else if (!SameRoles(row.Role, account.Roles))
                    {
                        var roles = Split(row.Role);
                        var unknown = roles.Where(role => !SystemRoles.Contains(role, StringComparer.Ordinal)).ToList();
                        if (roles.Count == 0 || unknown.Count > 0)
                        {
                            errors.Add($"{where}: \"{row.Role}\" isn't a role. Use {string.Join(", ", SystemRoles)}, or several joined with +.");
                        }
                        else
                        {
                            roleChanges.Add(new RoleChange(account.Email, account.Roles, roles));
                        }
                    }

                    break;
                case DecisionsFile.Cluster:
                    if (inventory.Clusters.All(cluster => cluster.ClusterId != row.Id))
                    {
                        errors.Add($"{where}: there is no such cluster.");
                    }
                    else if (row.Decision == DecisionsFile.Delete)
                    {
                        clustersDeleted.Add(row.Id);
                    }

                    break;
                case DecisionsFile.Token:
                    var token = inventory.Tokens.FirstOrDefault(found => found.TokenId == row.Id);
                    if (token is null)
                    {
                        errors.Add($"{where}: there is no such token.");
                    }
                    else if (row.Decision == DecisionsFile.Delete)
                    {
                        tokensDeleted.Add(token.TokenId);
                    }
                    else if (token.IsSample)
                    {
                        notes.Add($"Token {token.TokenId} is a sample token: it is removed anyway.");
                    }

                    break;
                default:
                    errors.Add($"{where}: \"{row.Kind}\" should be account, cluster or token.");
                    break;
            }
        }

        foreach (var account in inventory.Accounts.Where(account => !seen.Contains($"{DecisionsFile.Account}|{account.Email}")))
        {
            notes.Add($"Account {account.Email} has no row in the decisions file: it stays as it is.");
        }

        foreach (var cluster in inventory.Clusters.Where(cluster => !seen.Contains($"{DecisionsFile.Cluster}|{cluster.ClusterId}")))
        {
            notes.Add($"Cluster {cluster.ClusterId} has no row in the decisions file: it stays as it is.");
        }

        var adminsLeft = inventory.Accounts
            .Where(account => !accountsDeleted.Contains(account.Email))
            .Count(account => (roleChanges.FirstOrDefault(change => change.Email == account.Email)?.To ?? Split(account.Roles)).Contains(AccessControlService.RoleAdmin));
        if (adminsLeft == 0)
        {
            errors.Add("No account would have the admin role. Keep one, or give one the role admin in the role column; otherwise nobody can manage the app.");
        }

        tokensDeleted.AddRange(inventory.Tokens
            .Where(token => token.IsSample || clustersDeleted.Any(clusterId => Inventory.BelongsTo(token, clusterId)))
            .Select(token => token.TokenId)
            .Where(tokenId => !tokensDeleted.Contains(tokenId)));

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ResetPlan(
            inventory.Collections.Where(collection => collection.Fate is CollectionFate.NewTestData or CollectionFate.Cleared).ToList(),
            inventory.Collections.Where(collection => collection.Fate == CollectionFate.OldData).ToList(),
            inventory.Collections.Where(collection => collection.Fate == CollectionFate.Unknown).ToList(),
            accountsDeleted,
            roleChanges,
            clustersDeleted,
            tokensDeleted,
            notes), errors);
    }

    private static List<string> Split(string roles) =>
        roles.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    private static bool SameRoles(string a, string b) =>
        Split(a).Order(StringComparer.Ordinal).SequenceEqual(Split(b).Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
```

- [ ] **Step 5: Write `ResetRunner.cs`**

`tools/V3Reset/ResetRunner.cs`:

```csharp
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace V3Reset;

/// <summary>Carries out a checked plan: first a safety copy of everything it removes or changes, then the changes, then one audit entry.</summary>
public static class ResetRunner
{
    public const string AuditEventType = "data.reset";

    public static async Task<BsonDocument> RunAsync(
        MongoContext context,
        ResetPlan plan,
        string copyFolder,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var database = context.Database ?? throw new InvalidOperationException("No database connection.", context.ConnectionError);
        await WriteSafetyCopyAsync(database, plan, copyFolder, cancellationToken);

        foreach (var collection in plan.Emptied)
        {
            await database.GetCollection<BsonDocument>(collection.Name).DeleteManyAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken);
        }

        foreach (var collection in plan.Removed)
        {
            await database.DropCollectionAsync(collection.Name, cancellationToken);
        }

        // The app's own code removes clusters (with their memberships) and accounts (with theirs), as its pages do.
        var clusters = new ClusterRepository(context);
        await database.GetCollection<BsonDocument>("apiTokens").DeleteManyAsync(Builders<BsonDocument>.Filter.In("tokenId", plan.TokensDeleted), cancellationToken);
        foreach (var clusterId in plan.ClustersDeleted)
        {
            await clusters.DeleteClusterAsync(clusterId, cancellationToken);
        }

        foreach (var email in plan.AccountsDeleted)
        {
            await clusters.DeleteUserAsync(email, cancellationToken);
        }

        var now = DateTime.UtcNow.ToString("O");
        foreach (var change in plan.RoleChanges)
        {
            await database.GetCollection<BsonDocument>("users").UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("email", change.Email),
                Builders<BsonDocument>.Update.Set("roles", new BsonArray(change.To)).Set("updatedAt", now),
                cancellationToken: cancellationToken);
        }

        return await new ApplicationAuditService(context).AppendApplicationAuditEventAsync(
            AuditEventType,
            actor,
            "admin",
            "v3-reset",
            "battery_pass_v3 was reset: the old app's data and the new test data were removed.",
            new BsonDocument
            {
                ["emptied"] = new BsonDocument(plan.Emptied.Select(collection => new BsonElement(collection.Name, collection.Count))),
                ["removed"] = new BsonDocument(plan.Removed.Select(collection => new BsonElement(collection.Name, collection.Count))),
                ["accountsDeleted"] = plan.AccountsDeleted.Count,
                ["rolesChanged"] = plan.RoleChanges.Count,
                ["clustersDeleted"] = new BsonArray(plan.ClustersDeleted),
                ["tokensDeleted"] = plan.TokensDeleted.Count,
            },
            "database",
            ResetCollections.Database,
            cancellationToken: cancellationToken);
    }

    /// <summary>Writes every document the reset removes or changes, one JSON file per collection, before anything changes.</summary>
    private static async Task WriteSafetyCopyAsync(IMongoDatabase database, ResetPlan plan, string folder, CancellationToken cancellationToken)
    {
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            throw new InvalidOperationException($"The safety copy folder {folder} isn't empty; choose a new one.");
        }

        Directory.CreateDirectory(folder);
        foreach (var collection in plan.Emptied.Concat(plan.Removed))
        {
            await CopyAsync(database, collection.Name, FilterDefinition<BsonDocument>.Empty, Path.Combine(folder, $"{collection.Name}.json"), cancellationToken);
        }

        var memberships = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.In("email", plan.AccountsDeleted),
            Builders<BsonDocument>.Filter.In("clusterId", plan.ClustersDeleted));
        await CopyAsync(database, "users", Builders<BsonDocument>.Filter.In("email", plan.AccountsDeleted.Concat(plan.RoleChanges.Select(change => change.Email))), Path.Combine(folder, "users.deleted-or-changed.json"), cancellationToken);
        await CopyAsync(database, "clusterMemberships", memberships, Path.Combine(folder, "clusterMemberships.deleted.json"), cancellationToken);
        await CopyAsync(database, "clusters", Builders<BsonDocument>.Filter.In("clusterId", plan.ClustersDeleted), Path.Combine(folder, "clusters.deleted.json"), cancellationToken);
        await CopyAsync(database, "apiTokens", Builders<BsonDocument>.Filter.In("tokenId", plan.TokensDeleted), Path.Combine(folder, "apiTokens.deleted.json"), cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(folder, "README.txt"),
            "Safety copy of what the battery_pass_v3 reset removed or changed, one file per collection (MongoDB extended JSON).\r\n"
            + "It holds removed accounts' password hashes and removed tokens' hashes: keep it on this PC only, and delete the folder once you're satisfied with the reset.\r\n",
            cancellationToken);
    }

    private static async Task CopyAsync(IMongoDatabase database, string collection, FilterDefinition<BsonDocument> filter, string path, CancellationToken cancellationToken)
    {
        var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson };
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("[");
        var first = true;
        using var cursor = await database.GetCollection<BsonDocument>(collection).FindAsync(filter, cancellationToken: cancellationToken);
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var document in cursor.Current)
            {
                await writer.WriteLineAsync((first ? string.Empty : ",") + document.ToJson(settings));
                first = false;
            }
        }

        await writer.WriteLineAsync("]");
    }
}
```

- [ ] **Step 6: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetCommandTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetPlannerTests|FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetRunnerTests"`
Expected: `Passed!  - Failed:     0, Passed:    25`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2036`, none failed.

- [ ] **Step 7: Commit**

```bash
git add BatteryPassWeb.Tests/Reset/ResetCommandTests.cs BatteryPassWeb.Tests/Reset/ResetPlannerTests.cs BatteryPassWeb.Tests/Reset/ResetRunnerTests.cs tools/V3Reset/App.cs tools/V3Reset/ResetPlanner.cs tools/V3Reset/ResetRunner.cs
git commit -m "feat(dpp): the reset, a dry run first, then a safety copy and the changes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 3: The launcher and its steps

The owner's launcher asks for the Atlas password, points the tool at `battery_pass_v3` only, and says where the review and the safety copy are. The README gives the steps in order.

**Files:**
- Test: `BatteryPassWeb.Tests/Reset/ResetLauncherTests.cs`
- Create: `tools/V3Reset/README.md`
- Create: `tools/V3Reset/v3-reset.ps1`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Reset/ResetLauncherTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the owner's launcher asks for the password, points only at battery_pass_v3 and says where the safety copy is.</summary>
public sealed class ResetLauncherTests
{
    private static readonly string Launcher = File.ReadAllText(DppTestFiles.RepoFile("tools", "V3Reset", "v3-reset.ps1"));

    [Fact]
    public void ThePassword_ShouldBeAskedEachTimeAndKeptOnlyInTheProcess()
    {
        Assert.Contains("Read-Host \"Atlas password for $User\" -AsSecureString", Launcher, StringComparison.Ordinal);
        Assert.Contains("Remove-Item Env:MONGODB_URI, Env:MONGODB_DB", Launcher, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Content", Launcher, StringComparison.Ordinal);
        Assert.DoesNotContain("[Environment]::SetEnvironmentVariable", Launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDatabase_ShouldOnlyBeBatteryPassV3()
    {
        Assert.Contains("$Database = \"battery_pass_v3\"", Launcher, StringComparison.Ordinal);
        Assert.Contains("$env:MONGODB_DB = $Database", Launcher, StringComparison.Ordinal);
        Assert.Contains("@${ClusterHost}/${Database}?retryWrites=true&w=majority\"", Launcher, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(Launcher, "\\$env:MONGODB_DB ="));
    }

    [Fact]
    public void TheApp_ShouldBeClosedFirst()
    {
        Assert.Contains("Get-Process -Name \"BatteryPassWeb\"", Launcher, StringComparison.Ordinal);
        Assert.Contains("Close the Battery Pass app first", Launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void ARealRun_ShouldNeedRunAndSayWhereTheSafetyCopyIs()
    {
        Assert.Contains("[switch]$Run", Launcher, StringComparison.Ordinal);
        Assert.Contains("reset --decisions $Decisions --run --copy $copy", Launcher, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project $project -- reset --decisions $Decisions\r", Launcher.Replace("\r\n", "\n").Replace("\n", "\r\n"), StringComparison.Ordinal);
        Assert.Contains("The safety copy is in $copy", Launcher, StringComparison.Ordinal);
        Assert.Contains("delete the folder once you're satisfied", Launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFolders_ShouldBeOutsideTheRepository()
    {
        Assert.Contains("Join-Path $HOME \"battery-pass-v3-review-$stamp\"", Launcher, StringComparison.Ordinal);
        Assert.Contains("Join-Path $HOME \"battery-pass-v3-safety-copy-$stamp\"", Launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReadme_ShouldGiveTheStepsInOrder()
    {
        var readme = File.ReadAllText(DppTestFiles.RepoFile("tools", "V3Reset", "README.md"));

        var steps = new[] { "1. **Review**", "2. **Decide**", "3. **Dry run**", "4. **Reset**", "5. **Check**" }.Select(step => readme.IndexOf(step, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, steps);
        Assert.Equal(steps.Order(), steps);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetLauncherTests"`
Expected: `Failed:     6, Passed:     0`: the launcher and its README don't exist yet.

- [ ] **Step 3: Write `README.md`**

`tools/V3Reset/README.md`:

```markdown
# V3Reset: the reset of battery_pass_v3

A one-off tool for step 3d-2 of the DPP V3 rebuild (design section 14). It clears the old app's data, the new model's
test data and the four sample API tokens from the Atlas database `battery_pass_v3`, and applies the product owner's
decisions about accounts, clusters and tokens. It works only on `battery_pass_v3` and refuses `battery_pass_demo`,
the live app's database, by name.

| Collections | What the reset does |
|---|---|
| `dpp_families`, `dpp_models`, `dpp_sites`, `dpp_batches`, `dpp_operators`, `dpp_packs`, `dpp_passport_versions`, `dpp_documents`, `dpp_files` | emptied: the new model's test data |
| `passports`, `batteries`, `passportRevisions`, the product templates and policies, `batteryTelemetry`, `passportFiles` | removed: the old app's data |
| `passwordResetRequests` | emptied |
| `users`, `clusterMemberships`, `clusters`, `apiTokens` | kept, except what the decisions file marks delete; the sample tokens always go |
| `applicationSettings`, `applicationAuditEvents`, `auditEvents`, `batteryAuditEvents` | kept; the audit log gets one "data.reset" entry with the counts |
| anything else | left alone and listed |

## Steps

Close the Battery Pass app first, and run these from the repository root in PowerShell. The launcher asks for the
Atlas password each time; use the same Atlas user and cluster host as `run-batterypass-v3-local.ps1`.

1. **Review**: `.\tools\V3Reset\v3-reset.ps1 inventory -User <Atlas user> -ClusterHost <cluster host>`
   It only reads. It writes `battery-pass-v3-review-<date>` in your user folder, with `inventory.txt` (what is in
   the database and what the reset would do) and `decisions.csv`.
2. **Decide**: open `decisions.csv` (Excel or a phone). Each row is an account, a cluster or a token; the first
   column says `keep` or `delete`, already filled in. Change `keep` to `delete` for what should go. For an account,
   the `role` column can take a new role: `admin`, `member`, `notifiedBody`, `marketSurveillanceAuthority`,
   `commission`, `liRepair`, `liRecycling`, `liTreatment`, or several joined with `+`.
3. **Dry run**: `.\tools\V3Reset\v3-reset.ps1 reset -User <Atlas user> -ClusterHost <cluster host> -Decisions <path to decisions.csv>`
   It shows what the reset would do and changes nothing. A mistake in the file stops it with the line to fix.
4. **Reset**: the same command with `-Run`. It shows the plan again and asks you to type `battery_pass_v3`; anything
   else stops it. It then saves a safety copy of everything it removes or changes in
   `battery-pass-v3-safety-copy-<date>` in your user folder, and only then makes the changes.
5. **Check**: start the app with `run-batterypass-v3-local.ps1`, sign in, and look at Batteries (empty), Users and
   the audit log (the "data.reset" entry). New accounts are made on the Users page. Delete the safety copy folder
   once you're satisfied: it holds removed accounts' password hashes.

If a reset stops halfway (a lost connection, say), run the inventory again and start from its new decisions file:
rows for what is already gone would stop the reset with "there is no such account".
```

- [ ] **Step 4: Write `v3-reset.ps1`**

`tools/V3Reset/v3-reset.ps1`:

```powershell
# Runs the reset tool against the Atlas database battery_pass_v3 (design 14, 3d-2).
#   inventory  only reads: it writes a review folder with decisions.csv in your user folder.
#   reset      a dry run that changes nothing, unless you add -Run; then it asks you to type the database name,
#              saves a safety copy of everything it removes in your user folder, and only then makes the changes.
# The live app (AWS, database battery_pass_demo) is never touched; the tool refuses any other database.
# The password is asked each time and kept only in this process.
#
# Examples (close the Battery Pass app first):
#   .\tools\V3Reset\v3-reset.ps1 inventory -User <Atlas user> -ClusterHost <cluster host>
#   .\tools\V3Reset\v3-reset.ps1 reset -User <Atlas user> -ClusterHost <cluster host> -Decisions <path to decisions.csv>
#   .\tools\V3Reset\v3-reset.ps1 reset -User <Atlas user> -ClusterHost <cluster host> -Decisions <path to decisions.csv> -Run
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("inventory", "reset")]
    [string]$Command,
    [Parameter(Mandatory = $true)]
    [string]$User,
    [Parameter(Mandatory = $true)]
    [string]$ClusterHost,
    [string]$Decisions = "",
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$Database = "battery_pass_v3"
$project = Join-Path $PSScriptRoot "V3Reset.csproj"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"

if (Get-Process -Name "BatteryPassWeb" -ErrorAction SilentlyContinue) {
    throw "Close the Battery Pass app first: the tool builds the app's code, and the reset shouldn't run while the app writes."
}

if ($Command -eq "reset" -and -not (Test-Path -LiteralPath $Decisions)) {
    throw "Give the decisions file from the inventory: -Decisions <path to decisions.csv>."
}

$secure = Read-Host "Atlas password for $User" -AsSecureString
$plain = [System.Net.NetworkCredential]::new("", $secure).Password

# Process-only environment variables: they disappear when this window closes.
$env:MONGODB_URI = "mongodb+srv://$([Uri]::EscapeDataString($User)):$([Uri]::EscapeDataString($plain))@${ClusterHost}/${Database}?retryWrites=true&w=majority"
$env:MONGODB_DB = $Database

try {
    if ($Command -eq "inventory") {
        $folder = Join-Path $HOME "battery-pass-v3-review-$stamp"
        dotnet run --project $project -- inventory --out $folder
    }
    elseif (-not $Run) {
        dotnet run --project $project -- reset --decisions $Decisions
    }
    else {
        $copy = Join-Path $HOME "battery-pass-v3-safety-copy-$stamp"
        dotnet run --project $project -- reset --decisions $Decisions --run --copy $copy
        if (Test-Path -LiteralPath $copy) {
            Write-Host ""
            Write-Host "The safety copy is in $copy" -ForegroundColor Cyan
            Write-Host "It holds removed accounts' password hashes: keep it on this PC, and delete the folder once you're satisfied with the reset." -ForegroundColor Cyan
        }
    }
}
finally {
    Remove-Item Env:MONGODB_URI, Env:MONGODB_DB -ErrorAction SilentlyContinue
    $plain = $null
}
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Reset.ResetLauncherTests"`
Expected: `Passed!  - Failed:     0, Passed:     6`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2042`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Reset/ResetLauncherTests.cs tools/V3Reset/README.md tools/V3Reset/v3-reset.ps1
git commit -m "feat(dpp): the reset launcher and its steps" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2042, Skipped:     0, Total:  2042`: the 1980 from before, then 31, 25 and 6 new tests in Tasks 1 to 3.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental` and `dotnet build tools/V3Reset/V3Reset.csproj --nologo -v q --no-incremental`
Expected for both: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

Do **not** run `tools/V3Reset/v3-reset.ps1` or the tool itself: its runs against Atlas are the product owner's, after this plan.

## After this plan: the product owner's runs

These are not steps of this plan. The managing session brings each one to the product owner and gets their yes; nobody else runs them. Close the Battery Pass app first, then from the worktree root in PowerShell, with the Atlas user and cluster host from `run-batterypass-v3-local.ps1`:

1. **Review:** `.\tools\V3Reset\v3-reset.ps1 inventory -User <Atlas user> -ClusterHost <cluster host>`. It only reads, and it writes `battery-pass-v3-review-<date>` in the user folder. The managing session goes through `decisions.csv` with the product owner.
2. **Dry run:** `.\tools\V3Reset\v3-reset.ps1 reset -User <Atlas user> -ClusterHost <cluster host> -Decisions <path to decisions.csv>`. It changes nothing.
3. **Reset**, after the product owner's yes: the same command with `-Run`, typing `battery_pass_v3` when asked.
4. **Check:** start the app with the usual launcher and look at three things: Batteries (empty), Users (as decided) and the audit log (the "data.reset" entry). Then make the new accounts on the Users page, and delete the safety copy folder once satisfied.

Then **3e** gives the admin, cluster admin and help pages a cleaner look, with mock-ups first.

**Known small gaps:**
- If `demo-cluster` is deleted, the API help page's examples still name it; renaming them is a small follow-up in 3e.
- `docs/end-user-testing-guide.md`, `docs/qa-test-pack.md` and `docs/sample-cluster-test-accounts.md` still describe the old app; their rewrite is step 6.

