# DPP V3 Step 6d-2: Battery IDs Released in Advance — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The second of 6d's three parts (the owner, 6 Nov 2026; the screens approved the same day). The team releases serial numbers in advance, so labels can be printed before the batteries are made, and a battery is created by picking one. 6d-1 made each battery's ID from a typed serial number; from here on the serial number is picked, never typed.

1. **A team-only "Battery IDs" tab** (after "Batteries"), one page:
   - **Release battery IDs:**
     - a required reference (e.g. "PO 4512, line 2") and an optional model;
     - one or more ranges (start, end, quantity: same letters before the number, same digit count, and the count must match);
     - a pasted list, one per line.
     Each released serial number gets its battery ID (#6) at release, fixed for life.
   - **All or nothing.** If any serial number or range fails, nothing is released, and every failure is listed with its reason. The checks:
     - letters and digits after cleaning, at most 36 (32 for demo);
     - not starting with TEST for a real one;
     - not twice in the release, also after cleaning;
     - not already released, in any status;
     - not already a battery's serial number or battery ID.
   - **Releases**, with counts per status, "Show its IDs" and "Void unused…".
   - **Released IDs**, filtered by status, release and model, searched by serial number. It lists at most 200; the filters narrow the list.
     - "Export to Excel" gives every matching ID: a real .xlsx, written with .NET's own zip support, no package, which opens the same on every PC whatever its list separator. Its columns: serial number, battery ID, QR code address, model, reference, released by and when, status.
       - Every identifier and free text is a text cell: 000123 and 2026-11 stay as written, and "=…" stays text, never a formula.
       - The release time is a real date cell (yyyy-mm-dd hh:mm), in a column wide enough to show it. Each column fits its content, and the header is bold.
2. **Statuses:**
   - Available → Used (a draft has it) → Published (fixed for life).
   - **Voided**, with a required reason, is never reused. Only an Available ID can be voided, alone or as "the unused IDs of this release".
3. **Creating a battery picks an available released serial number**, on the team's create page and in the API.
   - The picker offers the batch's model's IDs first, then those for any model, then the demo IDs. An ID tagged with another model isn't offered or accepted.
   - The battery takes the released spelling of the serial number and its battery ID. The API's create call names a released serial number; the API can't release.
   - A battery's new passport (a successor) doesn't use the pool: it adds R1, R2, … to its own serial number, as in 6d-1.
4. **While a draft**, the battery can change its battery ID ("More" → "Change its battery ID…"): it takes another available ID, and its old one is available again.
   - **A removed draft's ID is available again too:** its labelled battery still exists.
   - The draft's QR code works as before, since the draft has its ID.
5. **Scanning a released ID before publication** (Available, or a draft's) shows "Not published yet": a Scania Industrial Batteries battery ID whose passport isn't published yet, with no battery data. A voided ID shows "Not available".
6. **Demo data:**
   - "Reset demo data" releases a "Demo" set of TEST IDs, makes the demo batteries from them, and leaves four spares (DEMO-SPARE-0001 … 0004) to try the flow.
   - Clearing the demo data removes only demo IDs; real released IDs are never touched.
7. **Race-safe:**
   - A released ID's database key is its serial number's letters and digits, so two releases can't hold the same one.
   - A release goes in in one transaction where the database has them (Atlas); otherwise in order, taking back what went in if one fails.
   - A pick is one conditional update, so two people can't take the same ID.
8. **The audit log** lists releases, voids and swaps (with the reference and the reason) under a new filter, "Battery IDs".

**Architecture:**
- **New, in `web/Dpp/Identifiers/`:**
  - `ReleasedBatteryId`: the record, in the new collection `dpp_battery_ids`, with `ReleasedIdStatuses`;
  - `BatteryIdPool`:
    - `ReleaseAsync` (`Expand` for ranges), `ReleasesAsync`, `ListAsync`, `AvailableAsync`;
    - `PickAsync` (one conditional update), `SwapAsync`, `GiveBackAsync`, `MarkPublishedAsync`;
    - `VoidAsync`, `VoidUnusedAsync`, `FindUnpublishedAsync`, `OfAsync`, `Export`;
  - `XlsxWriter`: a one-sheet .xlsx: a bold header, columns sized to their content, text cells for strings and date cells for `DateTime`s.
- **`DppStore`:**
  - the collection and its indexes (release, pack, status and model);
  - `InsertReleaseAsync`, a transaction when `SupportsTransactions` (a replica-set primary or a sharded router), else in order with a take-back.
- **Creating a pack:** `NewPack.FromPool` makes `PackCreationService` pick the released ID (refused with `PackCreationService.NotAvailable`). The pack takes the ID's serial-number spelling and #6, and `IsDemo` for a demo ID. If the pack can't be stored, the ID goes back.
  - Who passes `FromPool`: `BatteryPages.CreateAsync` (the team's page), `BatteryApi.CreateAsync` (the API) and the demo data, whenever the pool is registered.
  - A successor never picks.
- **The pages:**
  - new `BatteryIdsController` (`/admin/battery-ids`: list, release, void, void-unused, export; `AdminOnly`) and `Views/BatteryIds/Index.cshtml`, with `css/bp-battery-ids.css` and `js/bp-battery-ids.js`;
  - `_AdminTabs` gets the tab;
  - `NewBattery.cshtml`: the picker replaces the serial number field, grouped by model; the batch filters it;
  - `BatteriesController`: the draft's swap (`/{packId}/battery-id`) and `Views/Batteries/_SwapIdDialog.cshtml`, from "More" on `Battery.cshtml`;
  - `BatteryPages.RemoveDraftAsync` gives the ID back.
- **Publication:** a battery's first version marks its ID Published (`PublicationService`).
- **Scanning:** `PassportController.Summary` shows `Views/Dpp/NotPublishedYet.cshtml` (a 404) for a released, unpublished, unvoided ID.
- **Demo data:** `DemoDataService` releases its "Demo" set with `DemoDataService.SpareSerials`, creates from it, and clears only demo IDs.
- **Elsewhere:**
  - `tools/V3Reset`: `dpp_battery_ids` is new test data at the cutover, like the batteries it points to;
  - `Admin/AuditLog.cshtml`: the "Battery IDs" filter;
  - `Help/Index.cshtml`: the API's create call names a released serial number.
- **`site.css` is not changed.**
- **Tests:**
  - new `BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs`, covering:
    - ranges and their refusals, and the all-or-nothing release with every reason;
    - overlapping releases at once, and one ID picked once at once;
    - creating from a released ID and only from one, and model tagging;
    - swap, give back and publish, and voiding;
    - the scan pages, a draft's QR code, the demo release and its clearing, and the API's create;
    - the Excel export read back cell by cell, and the wiring;
  - new `ReplicaSetReleaseTests.cs`: the release race on its own single-node replica set, so the transaction path runs as on Atlas;
  - changed: `DunsIdentifierTests` (the summary action's new lines) and `ResetCollectionsTests` (the new collection's fate).

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** the managing session's notes:
- `step6d-id-release-design.md`, with "Settled after the lead's questions";
- the approved screens in `step6d-release-shots/` (their export button then read "Export for Excel (CSV)"; the manager chose the built-in .xlsx, so it reads "Export to Excel").

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at 0489ba4 (plan 6d-1's commit, "feat(dpp): the battery ID scheme…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas. The tests use throwaway MongoDBs (EphemeralMongo, already downloaded). `ReplicaSetReleaseTests` starts its own single-node replica set, which takes about half a minute.
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own tests. The whole suite runs once**, in the final checks. Never run two whole suites at once.
- **Baseline:** the whole suite shows `Passed:  2579` (plan 6d-1's result). After this plan: 2602.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6d2-test.patch`). Then run `git apply ../<name>` from the worktree root. New files are given inside the patches.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**

## Found while prototyping

- **The screens** were checked by eye at 1280 and 375 before the owner saw them. On a phone:
  - the two lists are compact rows, about 100 px an ID; they were 14,000 px of cards;
  - the refusal list wraps inside the screen;
  - no "Swipe" hint under lists that fit.
  A script measured that no page scrolls sideways.
- **The picker** is a normal drop-down. The batch filters it in the browser; the create call checks the model again on the server, as `PickAsync` only takes an ID for the batch's model or any model.
- **CSV first, then .xlsx.** Tested in Excel on a Swedish-format PC, a comma CSV opened in one column, and plain 000123 became 123. A CSV splits for one locale only, so the manager chose the .xlsx.
  - A sample from this writer, opened in Excel on this PC, showed 000123 and 2026-11 as written, "=PO Åsa" and "+1 PO" as text (no formula), the release time as a date reading 2026-11-08 13:22 (not ####), and a bold header.
  - The test reads the cells back: text cells, the date cell's value and format, and the column's width.
- **Transactions:** the driver reports a direct connection to a replica set as "standalone". So the store checks the server's own role (primary or router) rather than the cluster's type. Found by `ReplicaSetReleaseTests`.
- **One reason per serial number:** a serial number both released and some battery's (a demo battery's) gets only the "already released" reason.

**Measured on a clean clone (db7bd45 plus 6d-1):**
- the tests first fail to build (`BatteryIdPool` doesn't exist yet);
- with the code, Task 1's own classes pass: 70 tests across the 4 classes this plan adds or changes;
- the whole suite: 2600 of 2602 pass (the clean clone's two known `ApplicationAuditCoverageTests` failures, `site.css` with CRLF line endings, pass in the worktree).

### Task 1: Battery IDs released in advance

**Files:**
- Create (tests): `BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs`, `BatteryPassWeb.Tests/Dpp/ReplicaSetReleaseTests.cs`
- Create (app): `web/Controllers/BatteryIdsController.cs`, `web/Dpp/Identifiers/BatteryIdPool.cs`, `web/Dpp/Identifiers/ReleasedBatteryId.cs`, `web/Dpp/Identifiers/XlsxWriter.cs`, `web/Views/Batteries/_SwapIdDialog.cshtml`, `web/Views/BatteryIds/Index.cshtml`, `web/Views/Dpp/NotPublishedYet.cshtml`, `web/wwwroot/css/bp-battery-ids.css`, `web/wwwroot/js/bp-battery-ids.js`
- Modify (tests): `BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs`, `BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs`
- Modify (app and tools): `tools/V3Reset/ResetCollections.cs`, `web/Controllers/BatteriesController.cs`, `web/Controllers/PassportController.cs`, `web/Dpp/Api/BatteryApi.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Editing/PackCreationService.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Publication/PublicationService.cs`, `web/Dpp/Store/DppStore.cs`, `web/Views/Admin/AuditLog.cshtml`, `web/Views/Batteries/Battery.cshtml`, `web/Views/Batteries/NewBattery.cshtml`, `web/Views/Help/Index.cshtml`, `web/Views/Shared/_AdminTabs.cshtml`

- [ ] **Step 1: Write the failing tests**

Save as `../6d2-test.patch` and run `git apply ../6d2-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs b/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs
new file mode 100644
index 0000000..6b38519
--- /dev/null
+++ b/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs
@@ -0,0 +1,337 @@
+using System.IO.Compression;
+using System.Security.Claims;
+using System.Text.Json.Nodes;
+using System.Text.RegularExpressions;
+using BatteryPassWeb.Controllers;
+using BatteryPassWeb.Dpp.Access;
+using BatteryPassWeb.Dpp.Demo;
+using BatteryPassWeb.Dpp.Documents;
+using BatteryPassWeb.Dpp.Editing;
+using BatteryPassWeb.Dpp.Identifiers;
+using BatteryPassWeb.Dpp.Pages;
+using BatteryPassWeb.Dpp.Publication;
+using BatteryPassWeb.Dpp.Store;
+using BatteryPassWeb.Services;
+using BatteryPassWeb.Tests.Demo;
+using Microsoft.AspNetCore.Authorization;
+using Microsoft.AspNetCore.Http;
+using Microsoft.AspNetCore.Mvc;
+using Microsoft.Extensions.DependencyInjection;
+using MongoDB.Driver;
+
+namespace BatteryPassWeb.Tests.Dpp;
+
+/// <summary>
+/// Step 6d-2, battery IDs released in advance (the owner, 6 Nov 2026): the team releases serial numbers on the "Battery
+/// IDs" page, each with its #6 fixed at release; a battery is created by picking an available one; a draft may swap it;
+/// a voided one is never reused; scanning one before publication says "Not published yet". Releases and picks are
+/// all-or-nothing and race-safe.
+/// </summary>
+[Collection(MongoCollection.Name)]
+public sealed class BatteryIdPoolTests(MongoFixture mongo)
+{
+    private const string Base = "https://www.sib-scania-eubp.eu";
+    private const string Who = DemoRig.Who;
+
+    private static ReleaseRequest Range(string reference, string start, string end, int quantity, string? model = null) =>
+        new(reference, model, [new IdRange(start, end, quantity.ToString(System.Globalization.CultureInfo.InvariantCulture))], null);
+
+    private static ReleaseRequest List(string reference, params string[] serials) => new(reference, null, [], string.Join('\n', serials));
+
+    private static async Task<(DemoRig Rig, BatteryIdPool Pool, BatteryPack C7)> SeededAsync(MongoFixture mongo)
+    {
+        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
+        await rig.Demo.SeedAsync(Who);
+        var c7 = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single();
+        return (rig, rig.Services.GetRequiredService<BatteryIdPool>(), c7);
+    }
+
+    private static NewPack NewBattery(BatteryPack like, string serial) =>
+        new(like.BatchId, like.OperatorId, like.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, FromPool: true);
+
+    [Fact]
+    public async Task ARange_ShouldBeReleasedWithItsBatteryIds_AvailableAndUnderItsReference()
+    {
+        var (rig, pool, c7) = await SeededAsync(mongo);
+        using var _ = rig;
+
+        var model = (await rig.Store.GetAsync<ProductionBatch>(c7.BatchId))!.ModelId;
+
+        var result = await pool.ReleaseAsync(Who, Range("PO 4512, line 2", "VPC-12221", "VPC-12230", 10, model));
+
+        Assert.Equal(10, result.Released);
+        var items = await pool.ListAsync(new ReleasedIdFilter(Reference: result.ReleaseId));
+        Assert.Equal(Enumerable.Range(12221, 10).Select(number => $"VPC-{number}"), items.Select(item => item.SerialNumber).Order(StringComparer.Ordinal));
+        Assert.All(items, item => Assert.Equal(ReleasedIdStatuses.Available, item.Status));
+        Assert.Equal($"{Base}/25S/UN778474481VPC12221", items.Single(item => item.SerialNumber == "VPC-12221").Identifier);
+        var release = (await pool.ReleasesAsync()).Single(summary => summary.ReleaseId == result.ReleaseId);
+        Assert.Equal(("PO 4512, line 2", model, 10, 10), (release.Reference, release.ModelId, release.Total, release.Count(ReleasedIdStatuses.Available)));
+    }
+
+    [Theory]
+    [InlineData("VPC-12221", "VPC-12230", "9", "that is 10 IDs, not 9")]
+    [InlineData("VPC-12221", "XYZ-12230", "10", "the same letters before the number")]
+    [InlineData("VPC-0001", "VPC-00010", "10", "the same letters before the number")]
+    [InlineData("VPC-12230", "VPC-12221", "10", "the end comes before the start")]
+    [InlineData("VPC-12221", "VPC-12230", "", "a range needs a start, an end and a quantity")]
+    [InlineData("VPC-A", "VPC-B", "2", "end in a number")]
+    public void ARangeThatDoesntAddUp_ShouldBeRefused(string start, string end, string quantity, string reason)
+    {
+        var problems = new List<IdProblem>();
+
+        Assert.Empty(BatteryIdPool.Expand(new IdRange(start, end, quantity), problems));
+        Assert.Contains(reason, Assert.Single(problems).Reason, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void ARange_ShouldKeepItsLettersAndDigitCount() =>
+        Assert.Equal(["SN-0098", "SN-0099", "SN-0100"], BatteryIdPool.Expand(new IdRange("SN-0098", "SN-0100", "3"), new List<IdProblem>()));
+
+    [Fact]
+    public async Task ARelease_ShouldBeAllOrNothing_ListingEveryReason()
+    {
+        var (rig, pool, c7) = await SeededAsync(mongo);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, List("PO 4513", "VPC-12301"));
+        // A battery made before the pool (6d-1), from a typed serial number.
+        await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "LEGACY-0001") with { FromPool = false });
+
+        var result = await pool.ReleaseAsync(Who, new ReleaseRequest("PO 4514", null, [new IdRange("VPC-12228", "VPC-12240", "12")],
+            "VPC-12401\nVPC-12401\nvpc 12301\nTEST-9\n---\nDEMO-C7M-0001\nlegacy 0001\nvpc 12402\nVPC-12402"));
+
+        Assert.Equal(0, result.Released);
+        Assert.Null(result.ReleaseId);
+        var reasons = result.Problems.ToDictionary(problem => problem.What, problem => problem.Reason);
+        Assert.Equal("that is 13 IDs, not 12", reasons["VPC-12228 to VPC-12240"]);
+        Assert.Equal("it is in this release twice", reasons["VPC-12401"]);
+        Assert.Equal("already released (Available, PO 4513)", reasons["vpc 12301"]);
+        Assert.Equal(DunsIdentifierScheme.StartsWithTest, reasons["TEST-9"]);
+        Assert.Equal(DunsIdentifierScheme.NoLettersOrDigits, reasons["---"]);
+        Assert.Equal("already released (Published, Demo)", reasons["DEMO-C7M-0001"]); // one reason per serial number
+        Assert.Equal("battery LEGACY-0001 already has this serial number or battery ID", reasons["legacy 0001"]);
+        Assert.Equal("it is in this release twice, also as vpc 12402", reasons["VPC-12402"]);
+        Assert.DoesNotContain(await pool.ReleasesAsync(), release => release.Reference == "PO 4514");
+    }
+
+    [Fact]
+    public async Task AReleaseWithoutAReference_ShouldBeRefused() =>
+        Assert.Equal("give the release a reference, such as the purchase order", (await new BatteryIdPool(new DppStore(mongo.NewContext()), new TestIdentifierScheme(), new ApplicationAuditService(mongo.NewContext()), TimeProvider.System).ReleaseAsync(Who, List(" ", "VPC-1"))).Problem);
+
+    [Fact]
+    public async Task TwoOverlappingReleasesAtOnce_ShouldNeverShareAnId()
+    {
+        var (rig, pool, _) = await SeededAsync(mongo);
+        using var __ = rig;
+
+        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => pool.ReleaseAsync(Who, Range($"PO race {index}", "RACE-0001", "RACE-0020", 20))));
+
+        Assert.Single(results, result => result.Done);
+        Assert.All(results.Where(result => !result.Done), result => Assert.True(result.Problem == BatteryIdPool.RaceLost || result.Problems.Count > 0));
+        var raced = await pool.ListAsync(new ReleasedIdFilter(Query: "RACE"));
+        Assert.Equal(20, raced.Count);
+        Assert.Single(raced.Select(item => item.ReleaseId).Distinct());
+    }
+
+    [Fact]
+    public async Task OneId_ShouldBePickedByOneBatteryOnly_EvenAtOnce()
+    {
+        var (rig, pool, _) = await SeededAsync(mongo);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, List("PO pick", "PICK-0001"));
+
+        var picks = await Task.WhenAll(Enumerable.Range(0, 20).Select(index => pool.PickAsync("PICK-0001", $"pack-{index}", null)));
+
+        Assert.Single(picks, picked => picked is not null);
+    }
+
+    [Fact]
+    public async Task ABattery_ShouldBeCreatedFromAReleasedId_AndOnlyFromOne()
+    {
+        var (rig, pool, c7) = await SeededAsync(mongo);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, List("PO 4512", "VPC-12221"));
+        var creation = rig.Services.GetRequiredService<PackCreationService>();
+
+        var made = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "vpc 12221"));
+        var again = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC-12221"));
+        var never = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC-99999"));
+
+        var pack = made.Pack!;
+        Assert.Equal("VPC-12221", pack.Values["batterySerialNumber"].Value!.GetValue<string>()); // the released spelling
+        Assert.Equal($"{Base}/25S/UN778474481VPC12221", pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>());
+        Assert.Equal((ReleasedIdStatuses.Used, pack.Id), ((await pool.GetAsync("VPC-12221"))!.Status, (await pool.GetAsync("VPC-12221"))!.PackId));
+        Assert.Null(again.Pack); // taken (the serial number is another battery's)
+        Assert.Equal(PackCreationService.NotAvailable, never.Problem);
+    }
+
+    [Fact]
+    public async Task AModelsId_ShouldOnlyGoToThatModel()
+    {
+        var (rig, pool, c7) = await SeededAsync(mongo);
+        using var __ = rig;
+        var c7Model = (await rig.Store.GetAsync<ProductionBatch>(c7.BatchId))!.ModelId;
+        var otherModel = (await rig.Store.ListAsync<BatteryModel>()).First(model => model.Id != c7Model).Id;
+        await pool.ReleaseAsync(Who, new ReleaseRequest("PO model", otherModel, [], "MODEL-0001"));
+
+        var made = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "MODEL-0001"));
+
+        Assert.Equal(PackCreationService.NotAvailable, made.Problem);
+        Assert.DoesNotContain(await pool.AvailableAsync(c7Model), item => item.SerialNumber == "MODEL-0001");
+        Assert.Contains(await pool.AvailableAsync(otherModel), item => item.SerialNumber == "MODEL-0001");
+    }
+
+    [Fact]
+    public async Task ADraft_ShouldSwapItsId_GiveItBackWhenRemoved_AndKeepItOncePublished()
+    {
+        var (rig, pool, c7) = await SeededAsync(mongo);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, List("PO swap", "SWAP-0001", "SWAP-0002"));
+        var draft = (await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "SWAP-0001"))).Pack!;
+
+        Assert.Null(await pool.SwapAsync(Who, draft.Id, "SWAP-0002"));
+        var swapped = (await rig.Store.GetAsync<BatteryPack>(draft.Id))!;
+        Assert.Equal("SWAP-0002", swapped.Values["batterySerialNumber"].Value!.GetValue<string>());
+        Assert.EndsWith("/25S/UN778474481SWAP0002", swapped.Values["uniqueProductIdentifier"].Value!.GetValue<string>(), StringComparison.Ordinal);
+        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("SWAP-0001"))!.Status);
+
+        Assert.Null((await rig.Services.GetRequiredService<BatteryPages>().RemoveDraftAsync(Who, draft.Id)).Problem);
+        Assert.Equal((ReleasedIdStatuses.Available, (string?)null), ((await pool.GetAsync("SWAP-0002"))!.Status, (await pool.GetAsync("SWAP-0002"))!.PackId));
+
+        // A published battery's ID is fixed: DEMO-C7M-0001 was published by the demo data.
+        Assert.Equal(ReleasedIdStatuses.Published, (await pool.GetAsync("DEMO-C7M-0001"))!.Status);
+        Assert.Equal("its passport is published, so its battery ID is fixed", await pool.SwapAsync(Who, c7.Id, "SWAP-0001"));
+    }
+
+    [Fact]
+    public async Task AVoidedId_ShouldNeverBeUsedOrReleasedAgain()
+    {
+        var (rig, pool, c7) = await SeededAsync(mongo);
+        using var __ = rig;
+        var release = await pool.ReleaseAsync(Who, List("PO void", "VOID-0001", "VOID-0002", "VOID-0003"));
+
+        Assert.Equal("give a reason, such as a damaged label", await pool.VoidAsync(Who, "VOID-0001", " "));
+        Assert.Null(await pool.VoidAsync(Who, "VOID-0001", "Label damaged"));
+        Assert.Equal((2L, (string?)null), await pool.VoidUnusedAsync(Who, release.ReleaseId!, "Order cancelled"));
+        Assert.Equal("only an available ID can be voided; this one is voided", await pool.VoidAsync(Who, "VOID-0002", "Again"));
+
+        Assert.Equal(PackCreationService.NotAvailable, (await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VOID-0001"))).Problem);
+        Assert.Equal("already released (Voided, PO void)", Assert.Single((await pool.ReleaseAsync(Who, List("PO again", "VOID-0001"))).Problems).Reason);
+        Assert.Equal("Label damaged", (await pool.GetAsync("VOID-0001"))!.VoidReason);
+    }
+
+    [Fact]
+    public async Task ScanningAReleasedIdBeforePublication_ShouldSayNotPublishedYet_AndAVoidedOneNotAvailable()
+    {
+        var (rig, pool, _) = await SeededAsync(mongo);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, List("PO scan", "SCAN-0001", "SCAN-0002"));
+        await pool.VoidAsync(Who, "SCAN-0002", "Label damaged");
+        var controller = new PassportController(new DppReaders(rig.Services.GetRequiredService<ClusterRepository>()), rig.Services.GetRequiredService<PassportPages>())
+        {
+            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } },
+        };
+
+        var available = Assert.IsType<ViewResult>(await controller.Summary("UN778474481SCAN0001", CancellationToken.None, pool));
+        var voided = Assert.IsType<ViewResult>(await controller.Summary("UN778474481SCAN0002", CancellationToken.None, pool));
+        var published = Assert.IsType<ViewResult>(await controller.Summary("UN778474481TESTDEMOC7M0001", CancellationToken.None, pool));
+
+        Assert.Equal(("~/Views/Dpp/NotPublishedYet.cshtml", 404), (available.ViewName, available.StatusCode));
+        Assert.Equal(("~/Views/Dpp/NotAvailable.cshtml", 404), (voided.ViewName, voided.StatusCode));
+        Assert.Equal("Summary", published.ViewName);
+        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Dpp", "NotPublishedYet.cshtml"));
+        Assert.Contains("This is a Scania Industrial Batteries battery ID, but the battery's passport isn't published yet.", view, StringComparison.Ordinal);
+        Assert.DoesNotContain("Model.PackId", view, StringComparison.Ordinal); // no battery data, not even which battery
+    }
+
+    [Fact]
+    public async Task ADraftsQrCode_ShouldCarryItsReleasedIdsAddress_ForTheTeam()
+    {
+        var (rig, pool, c7) = await SeededAsync(mongo);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, List("PO qr", "QR-0001"));
+        await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "QR-0001"));
+        var qr = new QrController(rig.Services.GetRequiredService<PassportPages>(), rig.Services.GetRequiredService<DppLinks>(), new PassportQrCodeService(), rig.Store)
+        {
+            ControllerContext = new ControllerContext
+            {
+                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, Who), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)], "test")) },
+            },
+        };
+
+        var svg = Assert.IsType<ContentResult>(await qr.QrCode("UN778474481QR0001", CancellationToken.None));
+
+        Assert.Contains("<svg", svg.Content, StringComparison.Ordinal); // labels can be printed before publication (decision 30)
+    }
+
+    [Fact]
+    public async Task TheDemoData_ShouldComeFromADemoRelease_WithSpares_AndClearOnlyItself()
+    {
+        var (rig, pool, _) = await SeededAsync(mongo);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, List("PO real", "REAL-0001"));
+
+        var demo = (await pool.ReleasesAsync()).Single(release => release.Reference == DemoDataService.DemoReference);
+        var spares = await pool.ListAsync(new ReleasedIdFilter(Query: "DEMOSPARE"));
+        await rig.Demo.ClearAsync(Who);
+
+        Assert.Equal(DemoDataService.SpareSerials.Length, demo.Count(ReleasedIdStatuses.Available));
+        Assert.True(demo.Count(ReleasedIdStatuses.Published) > 0);
+        Assert.All(spares, spare => Assert.Matches("/25S/UN778474481TESTDEMOSPARE000[1-4]$", spare.Identifier));
+        Assert.Equal(["REAL-0001"], (await pool.ListAsync(new ReleasedIdFilter())).Select(item => item.SerialNumber)); // real IDs stay
+    }
+
+    [Fact]
+    public async Task TheApi_ShouldCreateABatteryOnlyFromAReleasedSerial()
+    {
+        var (rig, pool, c7) = await SeededAsync(mongo);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, List("PO api", "API-0001"));
+        var api = rig.Services.GetRequiredService<BatteryPassWeb.Dpp.Api.BatteryApi>();
+
+        var made = await api.CreateAsync(ChangeSender.Team(Who), c7.BatchId, c7.OperatorId, c7.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "API-0001" });
+        var refused = await api.CreateAsync(ChangeSender.Team(Who), c7.BatchId, c7.OperatorId, c7.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "API-0002" });
+
+        Assert.EndsWith("/25S/UN778474481API0001", made.Pack!.Values["uniqueProductIdentifier"].Value!.GetValue<string>(), StringComparison.Ordinal);
+        Assert.Equal(PackCreationService.NotAvailable, refused.Problem);
+    }
+
+    [Fact]
+    public async Task TheExport_ShouldBeAnExcelFileWithTextCells_ThatKeepLeadingZeros()
+    {
+        var (rig, pool, _) = await SeededAsync(mongo);
+        using var __ = rig;
+        await pool.ReleaseAsync(Who, List("=PO Åsa; \"x\"", "000123", "2026-11"));
+        var items = await pool.ListAsync(new ReleasedIdFilter(Reference: (await pool.ReleasesAsync()).First().ReleaseId));
+
+        var file = BatteryIdPool.Export(items, new Dictionary<string, string>());
+
+        using var zip = new ZipArchive(new MemoryStream(file));
+        var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
+        var rows = Regex.Matches(sheet, "<row r=\"(\\d+)\">(.*?)</row>").Select(row => Regex.Matches(row.Groups[2].Value, "<t xml:space=\"preserve\">(.*?)</t>").Select(cell => System.Net.WebUtility.HtmlDecode(cell.Groups[1].Value)).ToArray()).ToArray();
+        Assert.Equal(["Serial number", "Battery ID", "QR code address", "Model", "Reference", "Released by", "Released (UTC)", "Status"], rows[0]);
+        Assert.Equal(["000123", "UN778474481000123", $"{Base}/25S/UN778474481000123", "Any model", "=PO Åsa; \"x\""], rows[1][..5]);
+        Assert.Equal([items[0].ReleasedBy, "Available"], rows[1][5..]); // the date cell isn't text
+        Assert.DoesNotContain("<f>", sheet, StringComparison.Ordinal); // text cells: no formula, nothing turned into a number or date
+        Assert.Contains("t=\"inlineStr\"", sheet, StringComparison.Ordinal);
+
+        // The release time is a real date cell, formatted as a date, in a column wide enough to show it.
+        var date = Regex.Match(sheet, "<c r=\"G2\" s=\"2\"><v>([0-9.]+)</v></c>");
+        Assert.True(date.Success);
+        Assert.Equal(items[0].ReleasedAtUtc, DateTime.FromOADate(double.Parse(date.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)), TimeSpan.FromMilliseconds(1));
+        Assert.True(int.Parse(Regex.Match(sheet, "<col min=\"7\" max=\"7\" width=\"(\\d+)\" customWidth=\"1\"/>").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 18); // "2026-11-08 13:22" shows, not ####
+        Assert.Contains($"formatCode=\"{XlsxWriter.DateFormat}\"", new StreamReader(zip.GetEntry("xl/styles.xml")!.Open()).ReadToEnd(), StringComparison.Ordinal);
+        Assert.Equal("Battery IDs", Regex.Match(new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open()).ReadToEnd(), "sheet name=\"([^\"]+)\"").Groups[1].Value);
+    }
+
+    [Fact]
+    public void ThePages_ShouldBeTheTeamsOnly_AndWiredIn()
+    {
+        Assert.Equal("AdminOnly", typeof(BatteryIdsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy);
+        Assert.Contains("href=\"/admin/battery-ids\">Battery IDs</a>", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml")), StringComparison.Ordinal);
+        var create = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "NewBattery.cshtml"));
+        Assert.Contains("<select name=\"v.batterySerialNumber\" required data-id-picker>", create, StringComparison.Ordinal); // picked, never typed
+        Assert.Contains("field.Attribute.Property != \"batterySerialNumber\"", create, StringComparison.Ordinal);
+        Assert.Contains("data-open-dialog=\"swap-id-dialog\"", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml")), StringComparison.Ordinal);
+        Assert.Contains("@Option(\"dpp-battery-ids\", \"Battery IDs\", filter.EntityType)", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "AuditLog.cshtml")), StringComparison.Ordinal);
+    }
+}
diff --git a/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs b/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
index 4627616..da6a52e 100644
--- a/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
@@ -272,5 +272,5 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
 
     [Fact]
     public void TheVersionsAddress_ShouldShowThatVersion() =>
-        Assert.Contains("return page is null ? NotFoundPage() : View(nameof(Summary), page);", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "PassportController.cs")), StringComparison.Ordinal);
+        Assert.Contains("return View(nameof(Summary), page);", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "PassportController.cs")), StringComparison.Ordinal);
 }
diff --git a/BatteryPassWeb.Tests/Dpp/ReplicaSetReleaseTests.cs b/BatteryPassWeb.Tests/Dpp/ReplicaSetReleaseTests.cs
new file mode 100644
index 0000000..3ce01e9
--- /dev/null
+++ b/BatteryPassWeb.Tests/Dpp/ReplicaSetReleaseTests.cs
@@ -0,0 +1,44 @@
+using BatteryPassWeb.Configuration;
+using BatteryPassWeb.Dpp.Identifiers;
+using BatteryPassWeb.Dpp.Store;
+using BatteryPassWeb.Services;
+using EphemeralMongo;
+using Microsoft.Extensions.Options;
+
+namespace BatteryPassWeb.Tests.Dpp;
+
+/// <summary>
+/// Step 6d-2: on a replica set, as on Atlas, a release goes in through one transaction. Its own throwaway single-node
+/// replica set (the shared test database is a plain single node, which takes the in-order path instead).
+/// </summary>
+public sealed class ReplicaSetReleaseTests : IAsyncLifetime
+{
+    private IMongoRunner? _runner;
+
+    public async Task InitializeAsync() =>
+        _runner = await MongoRunner.RunAsync(new MongoRunnerOptions { Version = MongoVersion.V8, UseSingleNodeReplicaSet = true });
+
+    public Task DisposeAsync()
+    {
+        _runner?.Dispose();
+        return Task.CompletedTask;
+    }
+
+    [Fact]
+    public async Task OverlappingReleasesAtOnce_ShouldNeverShareAnId_InATransaction()
+    {
+        var context = new MongoContext(Options.Create(new BatteryPassOptions { MongoDbUri = _runner!.ConnectionString, MongoDbName = $"dpp_rs_{Guid.NewGuid():N}" }));
+        var store = new DppStore(context);
+        await store.EnsureIndexesAsync();
+        var pool = new BatteryIdPool(store, new TestIdentifierScheme(), new ApplicationAuditService(context), TimeProvider.System);
+
+        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(index =>
+            pool.ReleaseAsync("team@example.test", new ReleaseRequest($"PO race {index}", null, [new IdRange("RACE-0001", "RACE-0020", "20")], null))));
+
+        Assert.True(DppStore.SupportsTransactions(context.Database!.Client)); // the transaction path
+        Assert.Single(results, result => result.Done);
+        var released = await pool.ListAsync(new ReleasedIdFilter());
+        Assert.Equal(20, released.Count);
+        Assert.Single(released.Select(item => item.ReleaseId).Distinct()); // the others left nothing behind
+    }
+}
diff --git a/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs b/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
index 8604cba..db3b099 100644
--- a/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
+++ b/BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs
@@ -16,7 +16,7 @@ public sealed class ResetCollectionsTests
         var named = Directory.GetFiles(web, "*.cs", SearchOption.AllDirectories)
             .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
             .SelectMany(file => Regex.Matches(File.ReadAllText(file), "GetCollection<[^>]*>\\(\"([^\"]+)\"\\)").Select(match => match.Groups[1].Value));
-        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DppStore.UploadChecks, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };
+        var dpp = new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DppStore.UploadChecks, DppStore.BatteryIds, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks" };
 
         foreach (var collection in named.Concat(dpp).Distinct())
         {
@@ -27,7 +27,7 @@ public sealed class ResetCollectionsTests
     [Fact]
     public void TheNewModelsCollections_ShouldBeEmptiedAsTestData()
     {
-        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DppStore.UploadChecks, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
+        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DppStore.HandOvers, DppStore.Uploads, DppStore.UploadChecks, DppStore.BatteryIds, DocumentStore.Records, $"{DocumentStore.Bucket}.files" })
         {
             Assert.Equal(CollectionFate.NewTestData, ResetCollections.FateOf(collection));
         }
```

- [ ] **Step 2: See them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`
Expected: the build fails: `CS0246: The type or namespace name 'BatteryIdPool' could not be found` (and the same for `ReleaseRequest`).

- [ ] **Step 3: Write the code**

Save as `../6d2-app.patch` and run `git apply ../6d2-app.patch`:

```diff
diff --git a/tools/V3Reset/ResetCollections.cs b/tools/V3Reset/ResetCollections.cs
index 2863ae8..e4d1a7d 100644
--- a/tools/V3Reset/ResetCollections.cs
+++ b/tools/V3Reset/ResetCollections.cs
@@ -43,6 +43,7 @@ public static class ResetCollections
         ["dpp_transfers"] = CollectionFate.NewTestData,
         ["dpp_uploads"] = CollectionFate.NewTestData,
         ["dpp_upload_checks"] = CollectionFate.NewTestData,
+        ["dpp_battery_ids"] = CollectionFate.NewTestData,
         ["dpp_documents"] = CollectionFate.NewTestData,
         ["dpp_files.files"] = CollectionFate.NewTestData,
         ["dpp_files.chunks"] = CollectionFate.NewTestData,
diff --git a/web/Controllers/BatteriesController.cs b/web/Controllers/BatteriesController.cs
index 0342fb8..e767062 100644
--- a/web/Controllers/BatteriesController.cs
+++ b/web/Controllers/BatteriesController.cs
@@ -59,10 +59,37 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
     }
 
     [HttpGet("{packId}")]
-    public async Task<IActionResult> Battery(string packId, CancellationToken cancellationToken)
+    public async Task<IActionResult> Battery(string packId, CancellationToken cancellationToken, [FromServices] Dpp.Identifiers.BatteryIdPool? pool = null)
     {
         var page = await WithUploadAsync(await pages.BatteryAsync(packId, cancellationToken: cancellationToken), cancellationToken);
-        return page is null ? NotFound() : View(page);
+        if (page is null)
+        {
+            return NotFound();
+        }
+
+        // A draft whose ID was picked may swap it for another available one (6d).
+        if (pool is not null && await pool.OfAsync(packId, cancellationToken) is { Status: Dpp.Identifiers.ReleasedIdStatuses.Used } picked)
+        {
+            ViewData[SwapFromKey] = picked;
+            ViewData[SwapIdsKey] = await pool.AvailableAsync(page.Sources.Model.Id, picked.IsDemo, cancellationToken);
+        }
+
+        return View(page);
+    }
+
+    /// <summary>The draft's released ID (6d), for "Change its battery ID…".</summary>
+    public const string SwapFromKey = "SwapFrom";
+
+    /// <summary>The IDs a draft may swap to: available, for its model or any.</summary>
+    public const string SwapIdsKey = "SwapIds";
+
+    [HttpPost("{packId}/battery-id")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> SwapBatteryId(string packId, [FromForm] string? serialNumber, [FromServices] Dpp.Identifiers.BatteryIdPool pool, CancellationToken cancellationToken)
+    {
+        var problem = await pool.SwapAsync(Who, packId, serialNumber, cancellationToken);
+        Card(packId, "mark", problem is null ? $"The battery now has serial number {serialNumber} and its battery ID; the old one is available again." : null, problem is null ? null : $"The battery ID didn't change: {problem}.");
+        return Redirect(BatteryPages.BatteryHref(packId));
     }
 
     /// <summary>"Check the file" in the "Upload a file" section (decision 35, step 5c): the check shows in the section.</summary>
diff --git a/web/Controllers/BatteryIdsController.cs b/web/Controllers/BatteryIdsController.cs
new file mode 100644
index 0000000..fd2f155
--- /dev/null
+++ b/web/Controllers/BatteryIdsController.cs
@@ -0,0 +1,139 @@
+using System.Globalization;
+using BatteryPassWeb.Dpp.Identifiers;
+using BatteryPassWeb.Dpp.Store;
+using Microsoft.AspNetCore.Authorization;
+using Microsoft.AspNetCore.Mvc;
+
+namespace BatteryPassWeb.Controllers;
+
+/// <summary>The release form as typed, kept when a release is refused.</summary>
+public sealed record ReleaseForm(string? Reference, string? ModelId, IReadOnlyList<IdRange> Ranges, string? List)
+{
+    public static readonly ReleaseForm Empty = new(null, null, [new IdRange(null, null, null)], null);
+}
+
+/// <summary>The "Battery IDs" page: the release form, its refusals, the releases and the released IDs.</summary>
+public sealed record BatteryIdsPage(
+    ReleaseForm Form,
+    ReleaseResult? Refusal,
+    IReadOnlyList<ReleaseSummary> Releases,
+    IReadOnlyList<ReleasedBatteryId> Items,
+    ReleasedIdFilter Filter,
+    IReadOnlyDictionary<string, string> Models,
+    IReadOnlyDictionary<string, string> Batteries,
+    int Total)
+{
+    /// <summary>The most rows the page lists; the export has them all.</summary>
+    public const int MaxRows = 200;
+
+    public string ModelName(string? modelId) => modelId is null ? "Any model" : Models.GetValueOrDefault(modelId, modelId);
+}
+
+/// <summary>
+/// The team's "Battery IDs" page (step 6d, the owner, 6 Nov 2026): serial numbers released in advance, each with its
+/// battery ID, so labels can be printed before the batteries are made. Team only.
+/// </summary>
+[Authorize(Policy = "AdminOnly")]
+[Route("admin/battery-ids")]
+public sealed class BatteryIdsController(BatteryIdPool pool, DppStore store) : Controller
+{
+    public const string ReleaseDialog = "void-unused-dialog";
+
+    [HttpGet("")]
+    public async Task<IActionResult> Index([FromQuery] string? status, [FromQuery] string? release, [FromQuery] string? model, [FromQuery] string? q, CancellationToken cancellationToken) =>
+        View(await PageAsync(ReleaseForm.Empty, null, new ReleasedIdFilter(status, release, model, q), cancellationToken));
+
+    [HttpPost("")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> Release(
+        [FromForm] string? reference,
+        [FromForm] string? modelId,
+        [FromForm] string[]? start,
+        [FromForm] string[]? end,
+        [FromForm] string[]? quantity,
+        [FromForm] string? list,
+        CancellationToken cancellationToken)
+    {
+        var count = new[] { start?.Length ?? 0, end?.Length ?? 0, quantity?.Length ?? 0 }.Max();
+        var ranges = Enumerable.Range(0, count)
+            .Select(index => new IdRange(At(start, index), At(end, index), At(quantity, index)))
+            .ToArray();
+        var result = await pool.ReleaseAsync(Who, new ReleaseRequest(reference, modelId, ranges, list), cancellationToken);
+        if (result.Done)
+        {
+            TempData[ProductDataController.StatusKey] = $"Released {result.Released.ToString(CultureInfo.InvariantCulture)} battery {(result.Released == 1 ? "ID" : "IDs")} ({reference?.Trim()}). They are listed below; export them for the labels.";
+            return Redirect($"/admin/battery-ids?release={Uri.EscapeDataString(result.ReleaseId!)}");
+        }
+
+        if (result.Problems.Count == 0)
+        {
+            ViewData[ProductDataController.ErrorKey] = $"Nothing was released: {result.Problem}."; // a listed refusal says it in its own box
+        }
+        var form = new ReleaseForm(reference, modelId, ranges.Length == 0 ? ReleaseForm.Empty.Ranges : ranges, list);
+        var page = await PageAsync(form, result, new ReleasedIdFilter(), cancellationToken);
+        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
+        return View(nameof(Index), page);
+    }
+
+    [HttpPost("void")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> Void([FromForm] string? serialNumber, [FromForm] string? reason, [FromForm] string? returnTo, CancellationToken cancellationToken)
+    {
+        var problem = await pool.VoidAsync(Who, serialNumber ?? string.Empty, reason, cancellationToken);
+        Message(problem is null ? $"Battery ID {serialNumber} is voided. It stays listed and is never used again." : null, problem is null ? null : $"It wasn't voided: {problem}.");
+        return Redirect(Back(returnTo));
+    }
+
+    [HttpPost("void-unused")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> VoidUnused([FromForm] string? releaseId, [FromForm] string? reason, [FromForm] string? returnTo, CancellationToken cancellationToken)
+    {
+        var (voided, problem) = await pool.VoidUnusedAsync(Who, releaseId ?? string.Empty, reason, cancellationToken);
+        Message(problem is null ? $"{voided.ToString(CultureInfo.InvariantCulture)} unused battery {(voided == 1 ? "ID is" : "IDs are")} voided." : null, problem is null ? null : $"Nothing was voided: {problem}.");
+        return Redirect(Back(returnTo));
+    }
+
+    [HttpGet("export")]
+    public async Task<IActionResult> Export([FromQuery] string? status, [FromQuery] string? release, [FromQuery] string? model, [FromQuery] string? q, CancellationToken cancellationToken)
+    {
+        var items = await pool.ListAsync(new ReleasedIdFilter(status, release, model, q), cancellationToken);
+        var models = await ModelsAsync(cancellationToken);
+        return File(BatteryIdPool.Export(items, models), XlsxWriter.ContentType, $"battery-ids-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
+    }
+
+    private async Task<BatteryIdsPage> PageAsync(ReleaseForm form, ReleaseResult? refusal, ReleasedIdFilter filter, CancellationToken cancellationToken)
+    {
+        var all = await pool.ListAsync(filter, cancellationToken);
+        var items = all.Take(BatteryIdsPage.MaxRows).ToArray();
+        var batteries = new Dictionary<string, string>(StringComparer.Ordinal);
+        foreach (var packId in items.Select(item => item.PackId).OfType<string>().Distinct())
+        {
+            batteries[packId] = Dpp.Pages.BatteryPages.BatteryHref(packId);
+        }
+
+        return new BatteryIdsPage(form, refusal, await pool.ReleasesAsync(cancellationToken), items, filter, await ModelsAsync(cancellationToken), batteries, all.Count);
+    }
+
+    private async Task<IReadOnlyDictionary<string, string>> ModelsAsync(CancellationToken cancellationToken) =>
+        (await store.ListAsync<BatteryModel>(cancellationToken)).OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(model => model.Id, model => model.Name, StringComparer.Ordinal);
+
+    private void Message(string? status, string? error)
+    {
+        if (status is not null)
+        {
+            TempData[ProductDataController.StatusKey] = status;
+        }
+
+        if (error is not null)
+        {
+            TempData[ProductDataController.ErrorKey] = error;
+        }
+    }
+
+    private static string Back(string? returnTo) =>
+        returnTo is { Length: > 0 } back && back.StartsWith("/admin/battery-ids", StringComparison.Ordinal) && !back.StartsWith("//", StringComparison.Ordinal) ? back : "/admin/battery-ids";
+
+    private static string? At(string[]? values, int index) => values is not null && index < values.Length ? values[index] : null;
+
+    private string Who => Services.AccessControlService.CurrentEmail(User) is { Length: > 0 } email ? email : "admin";
+}
diff --git a/web/Controllers/PassportController.cs b/web/Controllers/PassportController.cs
index 875ea3f..535a2f8 100644
--- a/web/Controllers/PassportController.cs
+++ b/web/Controllers/PassportController.cs
@@ -42,11 +42,27 @@ public sealed class PassportController(DppReaders readers, PassportPages pages)
     }
 
     [HttpGet("{id}")]
-    public async Task<IActionResult> Summary(string id, CancellationToken cancellationToken)
+    public async Task<IActionResult> Summary(string id, CancellationToken cancellationToken, [FromServices] BatteryPassWeb.Dpp.Identifiers.BatteryIdPool? pool = null)
     {
         var reader = await readers.ForAsync(User, cancellationToken);
         var page = await pages.SummaryAsync(reader, Uri.UnescapeDataString(id), cancellationToken);
-        return page is null ? NotFoundPage() : View(nameof(Summary), page);
+        if (page is not null)
+        {
+            return View(nameof(Summary), page);
+        }
+
+        // A released ID scanned before its passport is published (6d): ours, but nothing to show yet; a voided one isn't.
+        return pool is not null && await pool.FindUnpublishedAsync(Uri.UnescapeDataString(id), cancellationToken) is { Status: not BatteryPassWeb.Dpp.Identifiers.ReleasedIdStatuses.Voided } released
+            ? NotPublishedYet(released)
+            : NotFoundPage();
+    }
+
+    /// <summary>"Not published yet": a 404, so it is never read as a passport, with the scanned ID and no battery data.</summary>
+    private ViewResult NotPublishedYet(BatteryPassWeb.Dpp.Identifiers.ReleasedBatteryId released)
+    {
+        var view = View("~/Views/Dpp/NotPublishedYet.cshtml", released);
+        view.StatusCode = StatusCodes.Status404NotFound;
+        return view;
     }
 
     /// <summary>A version's own address (#5, step 6d): <c>/dpp/UN778474481VPC12221/v2</c>.</summary>
diff --git a/web/Dpp/Api/BatteryApi.cs b/web/Dpp/Api/BatteryApi.cs
index c442a15..44094d6 100644
--- a/web/Dpp/Api/BatteryApi.cs
+++ b/web/Dpp/Api/BatteryApi.cs
@@ -52,7 +52,8 @@ public sealed class BatteryApi(
     BatteryPages batteries,
     ClusterRepository clusters,
     ApplicationAuditService audit,
-    TimeProvider clock)
+    TimeProvider clock,
+    Identifiers.BatteryIdPool? pool = null)
 {
     /// <summary>The source stored with values a battery system sends through the API ("battery system" on the pages).</summary>
     public const string FeedSource = "feed";
@@ -275,7 +276,7 @@ public sealed class BatteryApi(
             return Problem($"cluster {cluster} doesn't exist; name a registered cluster or none");
         }
 
-        return await creation.CreateAsync(sender, new NewPack(batchId.Trim(), operatorId.Trim(), cluster, values, ApiSource), cancellationToken);
+        return await creation.CreateAsync(sender, new NewPack(batchId.Trim(), operatorId.Trim(), cluster, values, ApiSource, FromPool: pool is not null), cancellationToken);
 
         static PackCreationResult Problem(string problem) => new(null, [], problem);
     }
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index 290c19f..d2404e3 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -47,9 +47,16 @@ public sealed class DemoDataService(
     ApplicationAuditService audit,
     IOptions<BatteryPassOptions> options,
     TimeProvider clock,
-    UploadService uploads)
+    UploadService uploads,
+    Identifiers.BatteryIdPool? pool = null)
 {
     public const string SeededEvent = "demo.seeded";
+
+    /// <summary>The demo release's reference (6d): the demo batteries' IDs and a few spare ones to try the flow with.</summary>
+    public const string DemoReference = "Demo";
+
+    /// <summary>Spare demo IDs, released but unused, for trying the create picker and the swap.</summary>
+    public static readonly string[] SpareSerials = ["DEMO-SPARE-0001", "DEMO-SPARE-0002", "DEMO-SPARE-0003", "DEMO-SPARE-0004"];
     public const string ClearedEvent = "demo.cleared";
     public const string Mark = "IsDemo";
     public const string AccountMark = "isDemo";
@@ -173,6 +180,16 @@ public sealed class DemoDataService(
         // The batteries don't depend on each other: on a remote database each call waits for its round trip, so they are
         // made side by side, which about halves the reset (redesign R1, point 1). Each battery's own steps stay in order,
         // and every save still goes through the save rules.
+        // The demo batteries are created as real ones are (6d): from a release of TEST-marked IDs, with a few spares.
+        if (pool is not null)
+        {
+            var release = await pool.ReleaseAsync(who, new Identifiers.ReleaseRequest(DemoReference, null, [], string.Join('\n', catalogue.Packs.Select(demo => demo.Serial).Concat(SpareSerials)), IsDemo: true), cancellationToken);
+            if (!release.Done)
+            {
+                throw new InvalidOperationException($"The demo battery IDs couldn't be released: {release.Problem} {string.Join("; ", release.Problems.Select(problem => $"{problem.What}: {problem.Reason}"))}");
+            }
+        }
+
         var outcomes = await Task.WhenAll(catalogue.Packs.Select(demo => SeedPackAsync(who, team, demo, batches, operators, catalogue, cancellationToken)));
         var published = outcomes.Sum(outcome => outcome.Published);
         var successors = outcomes.Sum(outcome => outcome.Successors);
@@ -218,6 +235,7 @@ public sealed class DemoDataService(
         // Batteries first: nothing real can depend on a demo battery. Their versions and documents go with them, and their
         // kept upload checks in one delete (step 5d).
         var demoPacks = await DemoIdsAsync(DppStore.Packs, cancellationToken);
+        await Collection(DppStore.BatteryIds).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("IsDemo", true), cancellationToken); // the demo release only (6d)
         await Collection(DppStore.UploadChecks).DeleteManyAsync(Builders<BsonDocument>.Filter.In("PackId", demoPacks), cancellationToken);
         foreach (var packId in demoPacks)
         {
@@ -402,7 +420,7 @@ public sealed class DemoDataService(
     {
         var published = 0;
         var successors = 0;
-        var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form", IsDemo: true), cancellationToken);
+        var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form", IsDemo: true, FromPool: pool is not null), cancellationToken);
         if (created.Pack is null)
         {
             var rejected = created.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key} ({key.Message})");
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index 33ad050..18b5a24 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -38,6 +38,7 @@ public static class DppServiceCollectionExtensions
                 : new TestIdentifierScheme();
         });
         services.AddSingleton<SavePipeline>();
+        services.AddSingleton<BatteryIdPool>();
         services.AddSingleton<PackCreationService>();
         services.AddSingleton<StatusChangeService>();
         services.AddSingleton<SuccessionService>();
diff --git a/web/Dpp/Editing/PackCreationService.cs b/web/Dpp/Editing/PackCreationService.cs
index 65d1231..fd7bda5 100644
--- a/web/Dpp/Editing/PackCreationService.cs
+++ b/web/Dpp/Editing/PackCreationService.cs
@@ -15,7 +15,8 @@ public sealed record NewPack(
     IReadOnlyDictionary<string, JsonNode?> Values,
     string Source = "form",
     string? PredecessorPackId = null,
-    bool IsDemo = false);
+    bool IsDemo = false,
+    bool FromPool = false);
 
 /// <summary>The created pack, or the problem that stopped it; <see cref="Keys"/> says what happened to each value.</summary>
 public sealed record PackCreationResult(BatteryPack? Pack, IReadOnlyList<KeyResult> Keys, string? Problem);
@@ -30,8 +31,12 @@ public sealed class PackCreationService(
     IIdentifierScheme identifiers,
     DppLinks links,
     ApplicationAuditService audit,
-    TimeProvider clock)
+    TimeProvider clock,
+    BatteryIdPool? pool = null)
 {
+    /// <summary>Why a picked battery ID can't be used: someone took or voided it first, or it is for another model.</summary>
+    public const string NotAvailable = "that serial number isn't an available released battery ID for this battery's model; pick one released on the Battery IDs page";
+
     public const string AuditEventType = "dpp.pack-created";
 
     /// <summary>The start of the refusal when the serial number can't make a battery ID (6d).</summary>
@@ -103,16 +108,40 @@ public sealed class PackCreationService(
             pack.Values[key] = Stored(value, now, request.Source, sender.Who);
         }
 
-        // #6 (step 6d): made from the serial number (a battery's new passport adds R1, R2, …), and refused with the reason
-        // when the serial number can't make a valid one or another battery has it.
-        var made = await identifiers.NewPackIdentifierAsync(pack, cancellationToken);
-        if (made.Identifier is not { } identifier)
+        // #6 (step 6d): a battery takes a released ID, picked in one step so that no one else can; a battery's new passport
+        // makes its own from its serial number (R1, R2, …), as does a battery made without the pool (tests, scripts).
+        if (request.FromPool && request.PredecessorPackId is null && pool is not null)
+        {
+            var model = (await store.GetAsync<ProductionBatch>(request.BatchId, cancellationToken))!.ModelId;
+            if (await pool.PickAsync(evaluation.Accepted["batterySerialNumber"].GetValue<string>(), pack.Id, model, cancellationToken) is not { } picked)
+            {
+                return Failed(NotAvailable);
+            }
+
+            pack.IsDemo |= picked.IsDemo; // a demo ID makes a demo battery, which clearing the demo data removes
+            pack.Values["batterySerialNumber"] = Stored(JsonValue.Create(picked.SerialNumber), now, request.Source, sender.Who);
+            pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(picked.Identifier), now, "system", sender.Who);
+        }
+        else
         {
-            return Failed($"{CantGetId}: {made.Problem}");
+            var made = await identifiers.NewPackIdentifierAsync(pack, cancellationToken);
+            if (made.Identifier is not { } identifier)
+            {
+                return Failed($"{CantGetId}: {made.Problem}");
+            }
+
+            pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(identifier), now, "system", sender.Who);
         }
 
-        pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(identifier), now, "system", sender.Who);
-        await store.InsertAsync(pack, cancellationToken);
+        try
+        {
+            await store.InsertAsync(pack, cancellationToken);
+        }
+        catch when (pool is not null && request.FromPool)
+        {
+            await pool.GiveBackAsync(pack.Id, CancellationToken.None); // the ID is available again
+            throw;
+        }
 
         await audit.AppendApplicationAuditEventAsync(
             AuditEventType,
diff --git a/web/Dpp/Identifiers/BatteryIdPool.cs b/web/Dpp/Identifiers/BatteryIdPool.cs
new file mode 100644
index 0000000..acafc90
--- /dev/null
+++ b/web/Dpp/Identifiers/BatteryIdPool.cs
@@ -0,0 +1,538 @@
+using System.Globalization;
+using System.Text.RegularExpressions;
+using BatteryPassWeb.Dpp.Store;
+using BatteryPassWeb.Services;
+using MongoDB.Bson;
+using MongoDB.Driver;
+
+namespace BatteryPassWeb.Dpp.Identifiers;
+
+/// <summary>One range of a release: "VPC-12221" to "VPC-12230", 10 IDs.</summary>
+public sealed record IdRange(string? Start, string? End, string? Quantity)
+{
+    public bool IsEmpty => string.IsNullOrWhiteSpace(Start) && string.IsNullOrWhiteSpace(End) && string.IsNullOrWhiteSpace(Quantity);
+}
+
+/// <summary>What the team asks to release: ranges, a pasted list, or both, under one reference.</summary>
+public sealed record ReleaseRequest(string? Reference, string? ModelId, IReadOnlyList<IdRange> Ranges, string? List, bool IsDemo = false);
+
+/// <summary>Why one serial number (or one range) can't be released.</summary>
+public sealed record IdProblem(string What, string Reason);
+
+/// <summary>A release's outcome: all of it, or nothing and every reason.</summary>
+public sealed record ReleaseResult(int Released, string? ReleaseId, IReadOnlyList<IdProblem> Problems, string? Problem)
+{
+    public bool Done => Released > 0;
+}
+
+/// <summary>One release, as the list shows it.</summary>
+public sealed record ReleaseSummary(string ReleaseId, string Reference, string? ModelId, string ReleasedBy, DateTime ReleasedAtUtc, IReadOnlyDictionary<string, int> Counts)
+{
+    public int Total => Counts.Values.Sum();
+
+    public int Count(string status) => Counts.GetValueOrDefault(status);
+}
+
+/// <summary>Which released IDs the list shows.</summary>
+public sealed record ReleasedIdFilter(string? Status = null, string? Reference = null, string? ModelId = null, string? Query = null);
+
+/// <summary>
+/// The battery IDs the team releases in advance (step 6d, the owner, 6 Nov 2026). A release is checked whole and is
+/// all-or-nothing; each serial number gets its #6 at release, fixed for life. A battery is created by picking an available
+/// one; a draft may swap it for another; a voided one is never reused.
+/// </summary>
+public sealed partial class BatteryIdPool(DppStore store, IIdentifierScheme identifiers, ApplicationAuditService audit, TimeProvider clock)
+{
+    public const string ReleasedEvent = "dpp.ids-released";
+    public const string VoidedEvent = "dpp.ids-voided";
+    public const string SwappedEvent = "dpp.id-swapped";
+
+    /// <summary>The most IDs one release may hold.</summary>
+    public const int MaxPerRelease = 5000;
+
+    public const int MaxReference = 120;
+
+    public const int MaxReason = 200;
+
+    /// <summary>Why a release was refused when another took some of its serial numbers at the same moment.</summary>
+    public const string RaceLost = "another release took some of these serial numbers at the same moment; nothing was released, try again";
+
+    public async Task<ReleaseResult> ReleaseAsync(string who, ReleaseRequest request, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(request);
+        var reference = request.Reference?.Trim() ?? string.Empty;
+        if (reference.Length == 0)
+        {
+            return Refused("give the release a reference, such as the purchase order");
+        }
+
+        if (reference.Length > MaxReference)
+        {
+            return Refused($"the reference is longer than {MaxReference} characters");
+        }
+
+        if (request.ModelId is { Length: > 0 } modelId && await store.GetAsync<BatteryModel>(modelId, cancellationToken) is null)
+        {
+            return Refused("that model doesn't exist");
+        }
+
+        var problems = new List<IdProblem>();
+        var serials = new List<string>();
+        foreach (var range in request.Ranges.Where(range => !range.IsEmpty))
+        {
+            serials.AddRange(Expand(range, problems));
+        }
+
+        serials.AddRange((request.List ?? string.Empty).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
+        if (serials.Count == 0 && problems.Count == 0)
+        {
+            return Refused("give a range or at least one serial number");
+        }
+
+        if (serials.Count > MaxPerRelease)
+        {
+            return Refused($"a release holds at most {MaxPerRelease} IDs; this one has {serials.Count}");
+        }
+
+        // Each serial number on its own, then against each other, then against the pool and the batteries.
+        var wanted = new List<(string Serial, string Key, string Identifier)>();
+        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
+        foreach (var serial in serials)
+        {
+            var made = identifiers.ItemFor(serial, request.IsDemo);
+            if (made.Identifier is not { } identifier)
+            {
+                problems.Add(new IdProblem(serial, made.Problem!));
+                continue;
+            }
+
+            var key = IdentifierForms.Clean(serial);
+            if (seen.TryGetValue(key, out var first))
+            {
+                problems.Add(new IdProblem(serial, first == serial ? "it is in this release twice" : $"it is in this release twice, also as {first}"));
+                continue;
+            }
+
+            seen[key] = serial;
+            wanted.Add((serial, key, identifier));
+        }
+
+        var keys = wanted.Select(item => item.Key).ToArray();
+        var pooled = await store.ReleasedIds.Find(Builders<ReleasedBatteryId>.Filter.In(item => item.Id, keys)).ToListAsync(cancellationToken);
+        foreach (var item in pooled)
+        {
+            problems.Add(new IdProblem(seen[item.Id], $"already released ({ReleasedIdStatuses.Words(item.Status)}, {item.Reference})"));
+        }
+
+        var released = pooled.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
+        var batteries = await BatteriesByKeyAsync(cancellationToken);
+        foreach (var (serial, key, _) in wanted)
+        {
+            if (!released.Contains(key) && batteries.TryGetValue(key, out var battery)) // one reason per serial number
+            {
+                problems.Add(new IdProblem(serial, $"battery {battery} already has this serial number or battery ID"));
+            }
+        }
+
+        if (problems.Count > 0)
+        {
+            return new ReleaseResult(0, null, problems, $"{Words(problems.Count, "serial number or range", "serial numbers or ranges")} can't be released");
+        }
+
+        var releaseId = $"release-{Guid.NewGuid():N}";
+        var now = clock.GetUtcNow().UtcDateTime;
+        var records = wanted.Select(item => new ReleasedBatteryId
+        {
+            Id = item.Key,
+            SerialNumber = item.Serial,
+            Identifier = item.Identifier,
+            ReleaseId = releaseId,
+            Reference = reference,
+            ModelId = string.IsNullOrEmpty(request.ModelId) ? null : request.ModelId,
+            Status = ReleasedIdStatuses.Available,
+            ReleasedBy = who,
+            ReleasedAtUtc = now,
+            IsDemo = request.IsDemo,
+        }).ToList();
+        if (!await store.InsertReleaseAsync(records, cancellationToken))
+        {
+            // Another release took one of these at the same moment (the _id is unique), so this one went in whole or not at all.
+            return Refused(RaceLost);
+        }
+
+        await audit.AppendApplicationAuditEventAsync(
+            ReleasedEvent,
+            who,
+            "Team",
+            "battery-ids",
+            $"Released {Words(records.Count, "battery ID", "battery IDs")} ({reference}): {Span(records)}",
+            new BsonDocument { ["releaseId"] = releaseId, ["reference"] = reference, ["count"] = records.Count },
+            entityType: "dpp-battery-ids",
+            entityId: releaseId,
+            cancellationToken: cancellationToken);
+        return new ReleaseResult(records.Count, releaseId, [], null);
+    }
+
+    /// <summary>The releases, newest first, with how many of their IDs are in each status.</summary>
+    public async Task<IReadOnlyList<ReleaseSummary>> ReleasesAsync(CancellationToken cancellationToken = default)
+    {
+        var all = await store.ReleasedIds.Find(FilterDefinition<ReleasedBatteryId>.Empty).ToListAsync(cancellationToken);
+        return all.GroupBy(item => item.ReleaseId)
+            .Select(group =>
+            {
+                var first = group.First();
+                return new ReleaseSummary(group.Key, first.Reference, first.ModelId, first.ReleasedBy, first.ReleasedAtUtc,
+                    group.GroupBy(item => item.Status).ToDictionary(status => status.Key, status => status.Count(), StringComparer.Ordinal));
+            })
+            .OrderByDescending(release => release.ReleasedAtUtc)
+            .ToArray();
+    }
+
+    /// <summary>The released IDs the filter keeps, newest release first, then by serial number.</summary>
+    public async Task<IReadOnlyList<ReleasedBatteryId>> ListAsync(ReleasedIdFilter filter, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(filter);
+        var where = Builders<ReleasedBatteryId>.Filter.Empty;
+        if (!string.IsNullOrEmpty(filter.Status))
+        {
+            where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, filter.Status);
+        }
+
+        if (!string.IsNullOrEmpty(filter.Reference))
+        {
+            where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.ReleaseId, filter.Reference);
+        }
+
+        if (!string.IsNullOrEmpty(filter.ModelId))
+        {
+            where &= filter.ModelId == "any"
+                ? Builders<ReleasedBatteryId>.Filter.Eq(item => item.ModelId, null)
+                : Builders<ReleasedBatteryId>.Filter.Eq(item => item.ModelId, filter.ModelId);
+        }
+
+        if (IdentifierForms.Clean(filter.Query) is { Length: > 0 } query)
+        {
+            where &= Builders<ReleasedBatteryId>.Filter.Regex(item => item.Id, new BsonRegularExpression(Regex.Escape(query), string.Empty));
+        }
+
+        return await store.ReleasedIds.Find(where).SortByDescending(item => item.ReleasedAtUtc).ThenBy(item => item.Id).ToListAsync(cancellationToken);
+    }
+
+    /// <summary>The IDs a battery of <paramref name="modelId"/> may take: its model's first, then those for any model.</summary>
+    public async Task<IReadOnlyList<ReleasedBatteryId>> AvailableAsync(string? modelId = null, bool? demo = null, CancellationToken cancellationToken = default)
+    {
+        var where = Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Available);
+        if (demo is { } isDemo)
+        {
+            where &= Builders<ReleasedBatteryId>.Filter.Eq(item => item.IsDemo, isDemo);
+        }
+
+        var all = await store.ReleasedIds.Find(where).ToListAsync(cancellationToken);
+        return all.Where(item => modelId is null || item.ModelId is null || item.ModelId == modelId)
+            .OrderBy(item => item.IsDemo ? 1 : 0)
+            .ThenBy(item => item.ModelId is null ? 1 : 0)
+            .ThenBy(item => item.ReleasedAtUtc)
+            .ThenBy(item => item.Id, StringComparer.Ordinal)
+            .ToArray();
+    }
+
+    public Task<ReleasedBatteryId?> GetAsync(string serialNumber, CancellationToken cancellationToken = default) =>
+        store.GetAsync<ReleasedBatteryId>(IdentifierForms.Clean(serialNumber), cancellationToken);
+
+    /// <summary>A scanned or typed ID or serial number that is released but not published: its record, else null.</summary>
+    public async Task<ReleasedBatteryId?> FindUnpublishedAsync(string text, CancellationToken cancellationToken = default)
+    {
+        var wanted = text?.Trim() ?? string.Empty;
+        if (wanted.Length == 0)
+        {
+            return null;
+        }
+
+        var item = IdentifierForms.Short(wanted);
+        var where = Builders<ReleasedBatteryId>.Filter.Eq(released => released.Id, IdentifierForms.Clean(wanted))
+            | Builders<ReleasedBatteryId>.Filter.Eq(released => released.Identifier, wanted);
+        if (IdentifierForms.IsItem(item))
+        {
+            where |= Builders<ReleasedBatteryId>.Filter.Regex(released => released.Identifier, new BsonRegularExpression($"/{IdentifierForms.DataIdentifier}/{Regex.Escape(item)}$", string.Empty));
+        }
+
+        var found = await store.ReleasedIds.Find(where).FirstOrDefaultAsync(cancellationToken);
+        return found is { Status: not ReleasedIdStatuses.Published } ? found : null;
+    }
+
+    /// <summary>
+    /// Gives an available ID to a draft of <paramref name="modelId"/>, in one step so that no one else can; null when it
+    /// isn't available (taken or voided first) or is for another model.
+    /// </summary>
+    public async Task<ReleasedBatteryId?> PickAsync(string serialNumber, string packId, string? modelId, CancellationToken cancellationToken = default) =>
+        await store.ReleasedIds.FindOneAndUpdateAsync(
+            Builders<ReleasedBatteryId>.Filter.Eq(item => item.Id, IdentifierForms.Clean(serialNumber))
+                & Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Available)
+                & (Builders<ReleasedBatteryId>.Filter.Eq(item => item.ModelId, null) | Builders<ReleasedBatteryId>.Filter.Eq(item => item.ModelId, modelId)),
+            Builders<ReleasedBatteryId>.Update.Set(item => item.Status, ReleasedIdStatuses.Used).Set(item => item.PackId, packId),
+            new FindOneAndUpdateOptions<ReleasedBatteryId> { ReturnDocument = ReturnDocument.After },
+            cancellationToken);
+
+    /// <summary>The released ID a battery took, while it is a draft or published; null for a battery made otherwise.</summary>
+    public Task<ReleasedBatteryId?> OfAsync(string packId, CancellationToken cancellationToken = default) =>
+        store.ReleasedIds.Find(item => item.PackId == packId).FirstOrDefaultAsync(cancellationToken)!;
+
+    /// <summary>
+    /// A draft takes another available ID (the owner's "swap while draft"): it gets that serial number and battery ID, and
+    /// its old one is available again. Null when done, else why not. After publication the ID is fixed.
+    /// </summary>
+    public async Task<string?> SwapAsync(string who, string packId, string? serialNumber, CancellationToken cancellationToken = default)
+    {
+        if (string.IsNullOrWhiteSpace(serialNumber))
+        {
+            return "choose the battery ID it takes instead";
+        }
+
+        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack)
+        {
+            return "the battery wasn't found";
+        }
+
+        if (await store.GetLatestVersionNumberAsync(packId, cancellationToken) > 0)
+        {
+            return "its passport is published, so its battery ID is fixed";
+        }
+
+        if (await OfAsync(packId, cancellationToken) is not { Status: ReleasedIdStatuses.Used } old)
+        {
+            return "its battery ID wasn't picked from the released IDs";
+        }
+
+        var model = (await store.GetAsync<ProductionBatch>(pack.BatchId, cancellationToken))?.ModelId;
+        if (await PickAsync(serialNumber, packId, model, cancellationToken) is not { } picked)
+        {
+            return "that battery ID isn't available for this battery's model any more; pick another";
+        }
+
+        var now = clock.GetUtcNow().UtcDateTime;
+        await store.SetValuesAsync<BatteryPack>(packId, new Dictionary<string, Values.AttributeValue>(StringComparer.Ordinal)
+        {
+            ["batterySerialNumber"] = new() { Value = System.Text.Json.Nodes.JsonValue.Create(picked.SerialNumber), GeneratedAtUtc = now, Source = "form", Who = who },
+            ["uniqueProductIdentifier"] = new() { Value = System.Text.Json.Nodes.JsonValue.Create(picked.Identifier), GeneratedAtUtc = now, Source = "system", Who = who },
+        }, cancellationToken);
+        await store.ReleasedIds.UpdateOneAsync(
+            item => item.Id == old.Id && item.PackId == packId,
+            Builders<ReleasedBatteryId>.Update.Set(item => item.Status, ReleasedIdStatuses.Available).Set(item => item.PackId, null),
+            cancellationToken: cancellationToken);
+        await audit.AppendApplicationAuditEventAsync(
+            SwappedEvent,
+            who,
+            "Team",
+            "battery-ids",
+            $"Draft battery {old.SerialNumber} now has battery ID {picked.SerialNumber}; {old.SerialNumber} is available again",
+            new BsonDocument { ["from"] = old.Id, ["to"] = picked.Id },
+            entityType: "dpp-pack",
+            entityId: packId,
+            cancellationToken: cancellationToken);
+        return null;
+    }
+
+    /// <summary>A draft gives its ID back (swapped, or the draft removed): it is available again.</summary>
+    public Task GiveBackAsync(string packId, CancellationToken cancellationToken = default) =>
+        store.ReleasedIds.UpdateManyAsync(
+            Builders<ReleasedBatteryId>.Filter.Eq(item => item.PackId, packId) & Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Used),
+            Builders<ReleasedBatteryId>.Update.Set(item => item.Status, ReleasedIdStatuses.Available).Set(item => item.PackId, null),
+            cancellationToken: cancellationToken);
+
+    /// <summary>The battery's passport is published: its ID is fixed for life.</summary>
+    public Task MarkPublishedAsync(string packId, CancellationToken cancellationToken = default) =>
+        store.ReleasedIds.UpdateManyAsync(
+            Builders<ReleasedBatteryId>.Filter.Eq(item => item.PackId, packId) & Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Used),
+            Builders<ReleasedBatteryId>.Update.Set(item => item.Status, ReleasedIdStatuses.Published),
+            cancellationToken: cancellationToken);
+
+    /// <summary>Voids one available ID, for good; null when done, else why not.</summary>
+    public async Task<string?> VoidAsync(string who, string serialNumber, string? reason, CancellationToken cancellationToken = default)
+    {
+        if (Reason(reason) is not { } why)
+        {
+            return "give a reason, such as a damaged label";
+        }
+
+        var item = await GetAsync(serialNumber, cancellationToken);
+        if (item is null)
+        {
+            return "that ID isn't released";
+        }
+
+        if (item.Status != ReleasedIdStatuses.Available)
+        {
+            return $"only an available ID can be voided; this one is {ReleasedIdStatuses.Words(item.Status).ToLowerInvariant()}";
+        }
+
+        var voided = await VoidWhereAsync(who, why, Builders<ReleasedBatteryId>.Filter.Eq(released => released.Id, item.Id), cancellationToken);
+        if (voided == 0)
+        {
+            return "it was just taken by a battery";
+        }
+
+        await LogVoidAsync(who, why, item.ReleaseId, $"Voided battery ID {item.SerialNumber} ({item.Reference}): {why}", cancellationToken);
+        return null;
+    }
+
+    /// <summary>Voids every still available ID of one release; the count voided.</summary>
+    public async Task<(long Voided, string? Problem)> VoidUnusedAsync(string who, string releaseId, string? reason, CancellationToken cancellationToken = default)
+    {
+        if (Reason(reason) is not { } why)
+        {
+            return (0, "give a reason, such as an order cancelled");
+        }
+
+        var first = await store.ReleasedIds.Find(item => item.ReleaseId == releaseId).FirstOrDefaultAsync(cancellationToken);
+        if (first is null)
+        {
+            return (0, "that release doesn't exist");
+        }
+
+        var voided = await VoidWhereAsync(who, why, Builders<ReleasedBatteryId>.Filter.Eq(item => item.ReleaseId, releaseId), cancellationToken);
+        if (voided > 0)
+        {
+            await LogVoidAsync(who, why, releaseId, $"Voided the {Words((int)voided, "unused battery ID", "unused battery IDs")} of release {first.Reference}: {why}", cancellationToken);
+        }
+
+        return (voided, voided == 0 ? "the release has no available IDs left" : null);
+    }
+
+    /// <summary>The list as an Excel file (text cells, the release time as a date): one row per ID, for label printers and mail merge.</summary>
+    public static byte[] Export(IReadOnlyList<ReleasedBatteryId> items, IReadOnlyDictionary<string, string> modelNames) =>
+        XlsxWriter.Write(
+            "Battery IDs",
+            ["Serial number", "Battery ID", "QR code address", "Model", "Reference", "Released by", "Released (UTC)", "Status"],
+            items.Select(item => (IReadOnlyList<object?>)
+            [
+                item.SerialNumber,
+                IdentifierForms.Display(item.Identifier),
+                item.Identifier,
+                item.ModelId is null ? "Any model" : modelNames.GetValueOrDefault(item.ModelId, item.ModelId),
+                item.Reference,
+                item.ReleasedBy,
+                item.ReleasedAtUtc, // a real date cell
+                ReleasedIdStatuses.Words(item.Status),
+            ]).ToArray());
+
+    /// <summary>A range's serial numbers, or its problem: same letters, same digit count, and the count matches.</summary>
+    public static IReadOnlyList<string> Expand(IdRange range, ICollection<IdProblem> problems)
+    {
+        ArgumentNullException.ThrowIfNull(range);
+        ArgumentNullException.ThrowIfNull(problems);
+        var start = range.Start?.Trim() ?? string.Empty;
+        var end = range.End?.Trim() ?? string.Empty;
+        var label = $"{(start.Length > 0 ? start : "?")} to {(end.Length > 0 ? end : "?")}";
+        if (start.Length == 0 || end.Length == 0 || !int.TryParse(range.Quantity?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity < 1)
+        {
+            problems.Add(new IdProblem(label, "a range needs a start, an end and a quantity"));
+            return [];
+        }
+
+        var first = Numbered().Match(start);
+        var last = Numbered().Match(end);
+        if (!first.Success || !last.Success)
+        {
+            problems.Add(new IdProblem(label, "a range's start and end end in a number"));
+            return [];
+        }
+
+        var prefix = first.Groups["prefix"].Value;
+        var width = first.Groups["number"].Value.Length;
+        if (last.Groups["prefix"].Value != prefix || last.Groups["number"].Value.Length != width)
+        {
+            problems.Add(new IdProblem(label, "the start and end must have the same letters before the number, and as many digits"));
+            return [];
+        }
+
+        if (!long.TryParse(first.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var from)
+            || !long.TryParse(last.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var to))
+        {
+            problems.Add(new IdProblem(label, "the number is too long"));
+            return [];
+        }
+
+        if (to < from)
+        {
+            problems.Add(new IdProblem(label, "the end comes before the start"));
+            return [];
+        }
+
+        var count = to - from + 1;
+        if (count != quantity)
+        {
+            problems.Add(new IdProblem(label, $"that is {count.ToString(CultureInfo.InvariantCulture)} IDs, not {quantity.ToString(CultureInfo.InvariantCulture)}"));
+            return [];
+        }
+
+        if (count > MaxPerRelease)
+        {
+            problems.Add(new IdProblem(label, $"a release holds at most {MaxPerRelease} IDs"));
+            return [];
+        }
+
+        return Enumerable.Range(0, (int)count).Select(step => prefix + (from + step).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')).ToArray();
+    }
+
+    private async Task<long> VoidWhereAsync(string who, string why, FilterDefinition<ReleasedBatteryId> which, CancellationToken cancellationToken)
+    {
+        var result = await store.ReleasedIds.UpdateManyAsync(
+            which & Builders<ReleasedBatteryId>.Filter.Eq(item => item.Status, ReleasedIdStatuses.Available),
+            Builders<ReleasedBatteryId>.Update
+                .Set(item => item.Status, ReleasedIdStatuses.Voided)
+                .Set(item => item.VoidReason, why)
+                .Set(item => item.VoidedBy, who)
+                .Set(item => item.VoidedAtUtc, clock.GetUtcNow().UtcDateTime),
+            cancellationToken: cancellationToken);
+        return result.ModifiedCount;
+    }
+
+    private Task LogVoidAsync(string who, string why, string releaseId, string line, CancellationToken cancellationToken) =>
+        audit.AppendApplicationAuditEventAsync(
+            VoidedEvent,
+            who,
+            "Team",
+            "battery-ids",
+            line,
+            new BsonDocument { ["releaseId"] = releaseId, ["reason"] = why },
+            entityType: "dpp-battery-ids",
+            entityId: releaseId,
+            cancellationToken: cancellationToken);
+
+    /// <summary>Every battery's serial number and battery ID, by their letters and digits, naming the battery.</summary>
+    private async Task<Dictionary<string, string>> BatteriesByKeyAsync(CancellationToken cancellationToken)
+    {
+        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
+        foreach (var pack in await store.ListAsync<BatteryPack>(cancellationToken))
+        {
+            var serial = Text(pack, "batterySerialNumber");
+            var name = serial ?? pack.Id;
+            if (serial is not null)
+            {
+                byKey.TryAdd(IdentifierForms.Clean(serial), name);
+            }
+
+            if (Text(pack, "uniqueProductIdentifier") is { } identifier && IdentifierForms.Short(identifier) is var item && item != identifier)
+            {
+                byKey.TryAdd(item, name);
+            }
+        }
+
+        return byKey;
+    }
+
+    private static string? Text(BatteryPack pack, string key) =>
+        pack.Values.GetValueOrDefault(key)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
+
+    private static string? Reason(string? reason) => reason?.Trim() is { Length: > 0 and <= MaxReason } why ? why : null;
+
+    private static string Span(IReadOnlyList<ReleasedBatteryId> records) =>
+        records.Count == 1 ? records[0].SerialNumber : $"{records[0].SerialNumber} … {records[^1].SerialNumber}";
+
+    private static string Words(int count, string one, string many) => $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? one : many)}";
+
+    private static ReleaseResult Refused(string problem) => new(0, null, [], problem);
+
+    [GeneratedRegex("^(?<prefix>.*?)(?<number>[0-9]+)$")]
+    private static partial Regex Numbered();
+}
diff --git a/web/Dpp/Identifiers/ReleasedBatteryId.cs b/web/Dpp/Identifiers/ReleasedBatteryId.cs
new file mode 100644
index 0000000..52ff0dd
--- /dev/null
+++ b/web/Dpp/Identifiers/ReleasedBatteryId.cs
@@ -0,0 +1,69 @@
+using MongoDB.Bson.Serialization.Attributes;
+
+namespace BatteryPassWeb.Dpp.Identifiers;
+
+/// <summary>
+/// A battery ID the team released in advance (step 6d, the owner, 6 Nov 2026), so its label can be printed before the
+/// battery is made. Its <see cref="Id"/> is the serial number's letters and digits ("VPC12221"), so the same serial
+/// number can't be released twice, in any spelling. A battery is created by picking an available one.
+/// </summary>
+public sealed class ReleasedBatteryId : Store.IDppRecord
+{
+    [BsonId]
+    public required string Id { get; init; }
+
+    /// <summary>The serial number (#11) as it was released ("VPC-12221"); the battery made from it gets it.</summary>
+    public required string SerialNumber { get; init; }
+
+    /// <summary>The battery ID (#6), fixed at release.</summary>
+    public required string Identifier { get; init; }
+
+    /// <summary>The release it came in; "void the unused IDs of this release" works on it.</summary>
+    public required string ReleaseId { get; init; }
+
+    /// <summary>The team's reference for the release, such as a purchase order ("PO 4512, line 2").</summary>
+    public required string Reference { get; init; }
+
+    /// <summary>The model the release is for, or null when any model may use it.</summary>
+    public string? ModelId { get; init; }
+
+    public required string Status { get; set; }
+
+    public required string ReleasedBy { get; init; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public required DateTime ReleasedAtUtc { get; init; }
+
+    /// <summary>The battery that has it (Used or Published).</summary>
+    public string? PackId { get; set; }
+
+    public string? VoidReason { get; set; }
+
+    public string? VoidedBy { get; set; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public DateTime? VoidedAtUtc { get; set; }
+
+    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
+    public bool IsDemo { get; set; }
+}
+
+/// <summary>A released ID's status: Available → Used (a draft has it) → Published; or Voided, never reused.</summary>
+public static class ReleasedIdStatuses
+{
+    public const string Available = "available";
+    public const string Used = "used";
+    public const string Published = "published";
+    public const string Voided = "voided";
+
+    public static readonly string[] All = [Available, Used, Published, Voided];
+
+    public static string Words(string status) => status switch
+    {
+        Available => "Available",
+        Used => "Used",
+        Published => "Published",
+        Voided => "Voided",
+        _ => status,
+    };
+}
diff --git a/web/Dpp/Identifiers/XlsxWriter.cs b/web/Dpp/Identifiers/XlsxWriter.cs
new file mode 100644
index 0000000..0329bff
--- /dev/null
+++ b/web/Dpp/Identifiers/XlsxWriter.cs
@@ -0,0 +1,133 @@
+using System.Globalization;
+using System.IO.Compression;
+using System.Security;
+using System.Text;
+
+namespace BatteryPassWeb.Dpp.Identifiers;
+
+/// <summary>
+/// A one-sheet Excel workbook (.xlsx, Office Open XML) written with .NET's own zip support, so no package is needed (step
+/// 6d). It opens the same on every PC, whatever its list separator, unlike a CSV:
+/// <list type="bullet">
+/// <item>a bold header row and columns wide enough for what they hold;</item>
+/// <item>every text as a text cell, never a formula or a number: the serial number 000123 and 2026-11 stay as written,
+/// and "=…" stays text;</item>
+/// <item>a <see cref="DateTime"/> as a real date cell, shown as yyyy-mm-dd hh:mm.</item>
+/// </list>
+/// </summary>
+public static class XlsxWriter
+{
+    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
+
+    /// <summary>The date cells' format and the style index that uses it.</summary>
+    public const string DateFormat = "yyyy-mm-dd hh:mm";
+
+    private const int HeaderStyle = 1;
+    private const int DateStyle = 2;
+    private const int DateWidth = 18;
+
+    /// <param name="rows">Each cell a <see cref="string"/> (a text cell) or a <see cref="DateTime"/> (a date cell); null is empty.</param>
+    public static byte[] Write(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
+    {
+        ArgumentNullException.ThrowIfNull(headers);
+        ArgumentNullException.ThrowIfNull(rows);
+        using var buffer = new MemoryStream();
+        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
+        {
+            Add(zip, "[Content_Types].xml", """
+                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
+                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
+                """);
+            Add(zip, "_rels/.rels", """
+                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
+                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
+                """);
+            Add(zip, "xl/workbook.xml", $"""
+                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
+                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Escape(sheetName)}" sheetId="1" r:id="rId1"/></sheets></workbook>
+                """);
+            Add(zip, "xl/_rels/workbook.xml.rels", """
+                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
+                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
+                """);
+            Add(zip, "xl/styles.xml", $"""
+                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
+                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="{DateFormat}"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="3"><xf fontId="0"/><xf fontId="1" applyFont="1"/><xf fontId="0" numFmtId="164" applyNumberFormat="1"/></cellXfs></styleSheet>
+                """);
+            Add(zip, "xl/worksheets/sheet1.xml", Sheet(headers, rows));
+        }
+
+        return buffer.ToArray();
+    }
+
+    private static string Sheet(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
+    {
+        var sheet = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><cols>""");
+        for (var column = 0; column < headers.Count; column++)
+        {
+            sheet.Append(CultureInfo.InvariantCulture, $"<col min=\"{column + 1}\" max=\"{column + 1}\" width=\"{Width(column, headers, rows)}\" customWidth=\"1\"/>");
+        }
+
+        sheet.Append("</cols><sheetData>");
+        Row(sheet, 1, headers, header: true);
+        for (var index = 0; index < rows.Count; index++)
+        {
+            Row(sheet, index + 2, rows[index], header: false);
+        }
+
+        return sheet.Append("</sheetData></worksheet>").ToString();
+    }
+
+    /// <summary>A column wide enough for its longest text (within 10 and 60 characters), or for a date.</summary>
+    private static int Width(int column, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
+    {
+        var longest = Math.Max(headers[column].Length, rows.Select(row => column < row.Count ? row[column] switch
+        {
+            DateTime => DateWidth,
+            string text => text.Length,
+            _ => 0,
+        } : 0).DefaultIfEmpty(0).Max());
+        return Math.Clamp(longest + 2, 10, 60);
+    }
+
+    private static void Row(StringBuilder sheet, int number, IEnumerable<object?> cells, bool header)
+    {
+        sheet.Append(CultureInfo.InvariantCulture, $"<row r=\"{number}\">");
+        var column = 0;
+        foreach (var cell in cells)
+        {
+            var reference = $"{Column(column++)}{number.ToString(CultureInfo.InvariantCulture)}";
+            switch (cell)
+            {
+                case DateTime date:
+                    sheet.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{DateStyle}\"><v>{date.ToOADate().ToString("R", CultureInfo.InvariantCulture)}</v></c>");
+                    break;
+                case string text:
+                    sheet.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" t=\"inlineStr\"{(header ? $" s=\"{HeaderStyle}\"" : string.Empty)}><is><t xml:space=\"preserve\">{Escape(text)}</t></is></c>");
+                    break;
+            }
+        }
+
+        sheet.Append("</row>");
+    }
+
+    /// <summary>A, B, …, Z, AA, AB, … for a zero-based column.</summary>
+    public static string Column(int index)
+    {
+        var name = string.Empty;
+        for (var number = index + 1; number > 0; number = (number - 1) / 26)
+        {
+            name = (char)('A' + ((number - 1) % 26)) + name;
+        }
+
+        return name;
+    }
+
+    private static void Add(ZipArchive zip, string path, string text)
+    {
+        using var writer = new StreamWriter(zip.CreateEntry(path, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
+        writer.Write(text.Trim());
+    }
+
+    private static string Escape(string? text) => SecurityElement.Escape(text ?? string.Empty);
+}
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 62cb5d0..4182254 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -47,6 +47,9 @@ public sealed record BatteriesPage(IReadOnlyList<BatteryRow> Rows, string Query,
 public sealed record BatchChoice(string BatchId, string FamilyName, string ModelName, int Year, string SiteName)
 {
     public string Label => $"{ModelName} · {Year} · {SiteName}";
+
+    /// <summary>The batch's model, so the create page offers that model's released IDs (6d).</summary>
+    public string ModelId { get; init; } = string.Empty;
 }
 
 /// <summary>The form for a new battery: where it comes from, and its own values as typed.</summary>
@@ -58,7 +61,19 @@ public sealed record NewBatteryPage(
     string? BatchId,
     string? OperatorId,
     string? ClusterId,
-    bool LinksConfigured);
+    bool LinksConfigured)
+{
+    /// <summary>The released IDs a new battery may take (6d); the page offers its batch's model's first, then any model's.</summary>
+    public IReadOnlyList<Identifiers.ReleasedBatteryId> AvailableIds { get; init; } = [];
+
+    /// <summary>True when batteries are created by picking a released ID (the app); false on a rig without the pool.</summary>
+    public bool Picks { get; init; }
+
+    public IReadOnlyDictionary<string, string> ModelNames { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
+
+    /// <summary>The serial number picked before a refused create, so the page keeps it.</summary>
+    public string? PickedSerial { get; init; }
+}
 
 /// <summary>Values the passport still needs, grouped by the record that holds them, with a link to its editor.</summary>
 public sealed record MissingGroup(string Record, string Href, IReadOnlyList<AttributeDefinition> Attributes);
@@ -211,7 +226,8 @@ public sealed class BatteryPages(
     ClusterRepository clusters,
     ApplicationAuditService audit,
     StatusPages status,
-    LiveTrends? trends = null)
+    LiveTrends? trends = null,
+    Identifiers.BatteryIdPool? pool = null)
 {
     public const string ClusterChangedEvent = "dpp.pack-cluster-changed";
     public const string ModulesSetEvent = "dpp.pack-modules-set";
@@ -383,7 +399,10 @@ public sealed class BatteryPages(
                     families.GetValueOrDefault(model.FamilyId)?.Name ?? "Unknown family",
                     model.Name,
                     batch.Year,
-                    sites.GetValueOrDefault(batch.SiteId)?.Name ?? batch.SiteId);
+                    sites.GetValueOrDefault(batch.SiteId)?.Name ?? batch.SiteId)
+                {
+                    ModelId = batch.ModelId,
+                };
             })
             .OrderBy(choice => choice.FamilyName, StringComparer.OrdinalIgnoreCase)
             .ThenBy(choice => choice.ModelName, StringComparer.OrdinalIgnoreCase)
@@ -394,7 +413,13 @@ public sealed class BatteryPages(
         var empty = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
         var sections = await editor.SectionsAsync(TargetKind.Pack, string.Empty, empty, state?.Post, state?.Save.Refusals(editor.Forms), cancellationToken);
         var values = new RecordEditorView(TargetKind.Pack, string.Empty, string.Empty, "/admin/batteries", "/admin/batteries", sections, [], 0, editor.Forms.Languages, string.Empty, new RemovalView(string.Empty, null));
-        return new NewBatteryPage(choices, operators, await ClustersAsync(cancellationToken), values, batchId, operatorId, clusterId, links.IsConfigured);
+        return new NewBatteryPage(choices, operators, await ClustersAsync(cancellationToken), values, batchId, operatorId, clusterId, links.IsConfigured)
+        {
+            Picks = pool is not null,
+            AvailableIds = pool is null ? [] : await pool.AvailableAsync(cancellationToken: cancellationToken),
+            ModelNames = models.ToDictionary(model => model.Key, model => model.Value.Name, StringComparer.Ordinal),
+            PickedSerial = state?.Post.Texts.GetValueOrDefault("v.batterySerialNumber"),
+        };
     }
 
     /// <summary>
@@ -427,7 +452,7 @@ public sealed class BatteryPages(
         }
 
         var values = post.Changed.ToDictionary(entry => entry.Key, entry => (System.Text.Json.Nodes.JsonNode?)entry.Value.DeepClone(), StringComparer.Ordinal);
-        return (await creation.CreateAsync(ChangeSender.Team(who), new NewPack(batchId, operatorId, cluster, values), cancellationToken), post);
+        return (await creation.CreateAsync(ChangeSender.Team(who), new NewPack(batchId, operatorId, cluster, values, FromPool: pool is not null), cancellationToken), post);
 
         static PackCreationResult Problem(string problem) => new(null, [], problem);
     }
@@ -657,6 +682,11 @@ public sealed class BatteryPages(
 
         await store.DeleteAsync<BatteryPack>(packId, cancellationToken);
         await documents.DeleteOwnedAsync(TargetKind.Pack, packId, cancellationToken);
+        if (pool is not null)
+        {
+            await pool.GiveBackAsync(packId, cancellationToken); // its labelled battery still needs the ID, so it is available again (6d)
+        }
+
         await audit.AppendApplicationAuditEventAsync(
             ProductDataPages.RemovedEvent,
             who,
diff --git a/web/Dpp/Publication/PublicationService.cs b/web/Dpp/Publication/PublicationService.cs
index 8d97f38..d993867 100644
--- a/web/Dpp/Publication/PublicationService.cs
+++ b/web/Dpp/Publication/PublicationService.cs
@@ -25,7 +25,8 @@ public sealed class PublicationService(
     PassportSigner signer,
     IIdentifierScheme identifiers,
     ApplicationAuditService audit,
-    TimeProvider clock) : IPackChangeListener
+    TimeProvider clock,
+    BatteryIdPool? pool = null) : IPackChangeListener
 {
     public const string PublishedEvent = "dpp.published";
     public const string BlockedEvent = "dpp.publish-blocked";
@@ -202,6 +203,11 @@ public sealed class PublicationService(
         }
 
         await store.ArchiveOtherVersionsAsync(packId, version.Id, cancellationToken);
+        if (version.Number == 1 && pool is not null)
+        {
+            await pool.MarkPublishedAsync(packId, cancellationToken); // its released ID is fixed for life (6d)
+        }
+
         if (version.Number == 1 && sources.Pack.SuccessorOf is { } predecessor)
         {
             await InactivateAsync(sender, predecessor, $"its new passport {passportId} is published", cancellationToken);
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index f11e7a6..557359a 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -24,6 +24,9 @@ public sealed class DppStore
     public const string Uploads = "dpp_uploads";
     public const string UploadChecks = "dpp_upload_checks";
 
+    /// <summary>The battery IDs the team released in advance (step 6d), keyed by the serial number's letters and digits.</summary>
+    public const string BatteryIds = "dpp_battery_ids";
+
     private const string UniqueProductIdentifierField = "Values.uniqueProductIdentifier.Value";
     private const string SerialNumberField = "Values.batterySerialNumber.Value";
 
@@ -39,6 +42,7 @@ public sealed class DppStore
         [typeof(HandOver)] = HandOvers,
         [typeof(UploadRecord)] = Uploads,
         [typeof(UploadCheck)] = UploadChecks,
+        [typeof(Identifiers.ReleasedBatteryId)] = BatteryIds,
     };
 
     private readonly IMongoDatabase? _database;
@@ -51,6 +55,59 @@ public sealed class DppStore
 
     public bool IsAvailable => _database is not null;
 
+    /// <summary>The released battery IDs (step 6d), for <see cref="Identifiers.BatteryIdPool"/>.</summary>
+    public IMongoCollection<Identifiers.ReleasedBatteryId> ReleasedIds => Collection<Identifiers.ReleasedBatteryId>();
+
+    /// <summary>
+    /// Stores a whole release or none of it (step 6d): in one transaction where the database has them (a replica set, as
+    /// on Atlas), otherwise in order, taking back what went in when one fails. False when an ID was already released (its
+    /// _id, the serial number's letters and digits, is unique), so two releases at once can't share one.
+    /// </summary>
+    /// <summary>
+    /// True when the database takes transactions: a replica-set primary or a sharded router (Atlas), however the app is
+    /// connected to it (a direct connection reports the cluster as standalone).
+    /// </summary>
+    public static bool SupportsTransactions(IMongoClient client) =>
+        client.Cluster.Description.Servers.Any(server => server.Type is MongoDB.Driver.Core.Servers.ServerType.ReplicaSetPrimary or MongoDB.Driver.Core.Servers.ServerType.ShardRouter);
+
+    public async Task<bool> InsertReleaseAsync(IReadOnlyList<Identifiers.ReleasedBatteryId> records, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(records);
+        var options = new InsertManyOptions { IsOrdered = true };
+        var client = RequireDatabase().Client;
+        if (SupportsTransactions(client))
+        {
+            using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
+            try
+            {
+                await session.WithTransactionAsync(
+                    async (handle, token) =>
+                    {
+                        await ReleasedIds.InsertManyAsync(handle, records, options, token);
+                        return true;
+                    },
+                    cancellationToken: cancellationToken);
+                return true;
+            }
+            catch (MongoBulkWriteException)
+            {
+                return false;
+            }
+        }
+
+        try
+        {
+            await ReleasedIds.InsertManyAsync(records, options, cancellationToken);
+            return true;
+        }
+        catch (MongoBulkWriteException)
+        {
+            var releaseIds = records.Select(record => record.ReleaseId).Distinct().ToArray();
+            await ReleasedIds.DeleteManyAsync(Builders<Identifiers.ReleasedBatteryId>.Filter.In(record => record.ReleaseId, releaseIds), CancellationToken.None);
+            return false;
+        }
+    }
+
     /// <summary>
     /// #6 is unique; serial numbers are not (design 8.2), so they are indexed for lookup only.
     /// A batch is unique per model, site and year (decision 3). Safe to run at every start.
@@ -118,6 +175,12 @@ public sealed class DppStore
                 Builders<UploadCheck>.IndexKeys.Ascending(check => check.ExpiresAtUtc),
                 new CreateIndexOptions { Name = "expires", ExpireAfter = TimeSpan.Zero }),
             cancellationToken: cancellationToken);
+        await ReleasedIds.Indexes.CreateManyAsync(
+        [
+            new CreateIndexModel<Identifiers.ReleasedBatteryId>(Builders<Identifiers.ReleasedBatteryId>.IndexKeys.Ascending(item => item.ReleaseId), new CreateIndexOptions { Name = "release" }),
+            new CreateIndexModel<Identifiers.ReleasedBatteryId>(Builders<Identifiers.ReleasedBatteryId>.IndexKeys.Ascending(item => item.PackId), new CreateIndexOptions { Name = "pack", Sparse = true }),
+            new CreateIndexModel<Identifiers.ReleasedBatteryId>(Builders<Identifiers.ReleasedBatteryId>.IndexKeys.Ascending(item => item.Status).Ascending(item => item.ModelId), new CreateIndexOptions { Name = "status_model" }),
+        ], cancellationToken);
     }
 
     /// <summary>Logs an upload (step 5a).</summary>
diff --git a/web/Views/Admin/AuditLog.cshtml b/web/Views/Admin/AuditLog.cshtml
index b468140..7433e72 100644
--- a/web/Views/Admin/AuditLog.cshtml
+++ b/web/Views/Admin/AuditLog.cshtml
@@ -61,6 +61,7 @@
                     @Option("dpp-site", "Manufacturing sites", filter.EntityType)
                     @Option("dpp-operator", "Economic operators", filter.EntityType)
                     @Option("dpp-pack", "Batteries", filter.EntityType)
+                    @Option("dpp-battery-ids", "Battery IDs", filter.EntityType)
                     @Option("dpp-document", "Documents", filter.EntityType)
                     @Option("cluster", "Clusters", filter.EntityType)
                     @Option("apiToken", "API tokens", filter.EntityType)
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 8f19646..a55296f 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -185,6 +185,10 @@
                             @* Opens a window, like Change status (redesign R5, owner's item 2). *@
                             <button type="button" class="is-danger" data-open-dialog="mark-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-alert" /></svg><span>Mark as invalid or for deletion…<small>Freezes the passport; every page says so</small></span></button>
                         }
+                        @if (!publication.IsPublished && ViewData[BatteriesController.SwapFromKey] is BatteryPassWeb.Dpp.Identifiers.ReleasedBatteryId)
+                        {
+                            <button type="button" data-open-dialog="swap-id-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-swap" /></svg><span>Change its battery ID…<small>Take another released ID while it is a draft</small></span></button>
+                        }
                         @if (!publication.IsPublished)
                         {
                             <a href="@href?section=overview&open=remove#remove" class="is-danger"><svg class="bp-i" aria-hidden="true"><use href="#i-trash" /></svg><span>Remove this draft battery…<small>Only while nothing is published</small></span></a>
@@ -432,6 +436,10 @@
     {
         @await Html.PartialAsync("~/Views/Batteries/_MarkDialog.cshtml", markable)
     }
+    @if (!publication.IsPublished && ViewData[BatteriesController.SwapFromKey] is BatteryPassWeb.Dpp.Identifiers.ReleasedBatteryId swapFrom)
+    {
+        @await Html.PartialAsync("~/Views/Batteries/_SwapIdDialog.cshtml", ($"{href}/battery-id", swapFrom, (IReadOnlyList<BatteryPassWeb.Dpp.Identifiers.ReleasedBatteryId>)ViewData[BatteriesController.SwapIdsKey]!))
+    }
     @* Asks before a removal that can't be undone (redesign R6). *@
     @await Html.PartialAsync("_ConfirmModal")
 </main>
diff --git a/web/Views/Batteries/NewBattery.cshtml b/web/Views/Batteries/NewBattery.cshtml
index d8cc3b9..7449cfc 100644
--- a/web/Views/Batteries/NewBattery.cshtml
+++ b/web/Views/Batteries/NewBattery.cshtml
@@ -2,11 +2,19 @@
 @model NewBatteryPage
 @{
     ViewData["Title"] = "Create battery";
-    var fields = Model.Values.Sections.SelectMany(section => section.Fields).ToArray();
+    // With released IDs (6d) the serial number is picked, never typed: its field gives way to the picker.
+    var fields = Model.Values.Sections.SelectMany(section => section.Fields)
+        .Where(field => !Model.Picks || field.Attribute.Property != "batterySerialNumber")
+        .ToArray();
+    var byModel = Model.AvailableIds.Where(item => !item.IsDemo && item.ModelId is not null).GroupBy(item => item.ModelId!).ToArray();
+    var anyModel = Model.AvailableIds.Where(item => !item.IsDemo && item.ModelId is null).ToArray();
+    var demoIds = Model.AvailableIds.Where(item => item.IsDemo).ToArray();
+    string Option(BatteryPassWeb.Dpp.Identifiers.ReleasedBatteryId item) => $"{item.SerialNumber} · {item.Reference}";
 }
 
 <main class="bp-page bp-admin-page">
     <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
+    <link rel="stylesheet" href="~/css/bp-battery-ids.css" asp-append-version="true" />
     <nav class="bp-breadcrumb" aria-label="Breadcrumb">
         <a href="/admin/batteries">Batteries</a>
         <span aria-current="page">Create battery</span>
@@ -37,14 +45,14 @@
             <div class="bp-new-battery-choices">
                 <label>
                     <span>Batch</span>
-                    <select name="batchId" required>
+                    <select name="batchId" required data-batch-select>
                         <option value="">Choose the model, year and site</option>
                         @foreach (var family in Model.Batches.GroupBy(choice => choice.FamilyName))
                         {
                             <optgroup label="@family.Key">
                                 @foreach (var choice in family)
                                 {
-                                    <option value="@choice.BatchId" selected="@(choice.BatchId == Model.BatchId)">@choice.Label</option>
+                                    <option value="@choice.BatchId" data-model="@choice.ModelId" selected="@(choice.BatchId == Model.BatchId)">@choice.Label</option>
                                 }
                             </optgroup>
                         }
@@ -82,9 +90,58 @@
             </div>
         </section>
 
+        @if (Model.Picks)
+        {
+            <section class="bp-new-battery-part">
+                <h2>Its serial number and battery ID</h2>
+                <p class="bp-template-sync-note">Picked from the IDs released on the <a href="/admin/battery-ids">Battery IDs</a> page: the battery takes the serial number (#11) and its battery ID (#6), whose label may already be printed. While it is a draft it can swap to another.</p>
+                @if (Model.AvailableIds.Count == 0)
+                {
+                    <p class="alert alert-warning bp-status-line mt-2 mb-0" role="status"><span>No battery ID is available. Release some on the <a href="/admin/battery-ids">Battery IDs</a> page first.</span></p>
+                }
+                else
+                {
+                    <label class="bp-id-picker">
+                        <span>Serial number</span>
+                        <select name="v.batterySerialNumber" required data-id-picker>
+                            <option value="">Choose a released serial number</option>
+                            @foreach (var group in byModel)
+                            {
+                                <optgroup label="For @Model.ModelNames.GetValueOrDefault(group.Key, group.Key)" data-model="@group.Key">
+                                    @foreach (var item in group)
+                                    {
+                                        <option value="@item.SerialNumber" selected="@(item.SerialNumber == Model.PickedSerial)">@Option(item)</option>
+                                    }
+                                </optgroup>
+                            }
+                            @if (anyModel.Length > 0)
+                            {
+                                <optgroup label="For any model" data-model="">
+                                    @foreach (var item in anyModel)
+                                    {
+                                        <option value="@item.SerialNumber" selected="@(item.SerialNumber == Model.PickedSerial)">@Option(item)</option>
+                                    }
+                                </optgroup>
+                            }
+                            @if (demoIds.Length > 0)
+                            {
+                                <optgroup label="Demo IDs (marked TEST, cleared with the demo data)" data-model="">
+                                    @foreach (var item in demoIds)
+                                    {
+                                        <option value="@item.SerialNumber" selected="@(item.SerialNumber == Model.PickedSerial)">@Option(item)</option>
+                                    }
+                                </optgroup>
+                            }
+                        </select>
+                        <small class="bp-field-help" data-id-picker-note>@Words.Count(Model.AvailableIds.Count, "ID") available. Choose the batch first to see only those its model may use.</small>
+                    </label>
+                }
+            </section>
+        }
+
         <section class="bp-new-battery-part">
             <h2>Its own values</h2>
-            <p class="bp-template-sync-note">The battery ID (#6) is issued by the app, and its status starts as “original”.</p>
+            <p class="bp-template-sync-note">@(Model.Picks ? "Its status starts as “original”." : "The battery ID (#6) is issued by the app, and its status starts as “original”.")</p>
             <div class="bp-attr-list">
                 @foreach (var field in fields)
                 {
@@ -97,7 +154,7 @@
             <span class="bp-savebar-text" data-save-status>The serial number is required; the rest can follow on the battery's page.</span>
             <div class="bp-savebar-actions">
                 <button type="button" class="bp-secondary-button" data-cancel-edits data-cancel-href="/admin/batteries">Cancel</button>
-                <button type="submit" class="bp-primary-button" disabled="@(!Model.LinksConfigured)">Create draft battery</button>
+                <button type="submit" class="bp-primary-button" disabled="@(!Model.LinksConfigured || (Model.Picks && Model.AvailableIds.Count == 0))">Create draft battery</button>
             </div>
         </div>
     </form>
@@ -117,4 +174,5 @@
 
 @section Scripts {
     <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
+    <script src="~/js/bp-battery-ids.js" asp-append-version="true"></script>
 }
diff --git a/web/Views/Batteries/_SwapIdDialog.cshtml b/web/Views/Batteries/_SwapIdDialog.cshtml
new file mode 100644
index 0000000..f5c36e5
--- /dev/null
+++ b/web/Views/Batteries/_SwapIdDialog.cshtml
@@ -0,0 +1,37 @@
+@using BatteryPassWeb.Dpp.Identifiers
+@model (string Action, ReleasedBatteryId From, IReadOnlyList<ReleasedBatteryId> Available)
+@* "Change its battery ID…" (step 6d, the owner's "swap while draft"): a draft takes another released ID, for example when
+   its label was put on another battery. The old ID is available again. After publication the ID is fixed. *@
+<div class="bp-console-form-card bp-dialog-host">
+    <dialog id="swap-id-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="swap-id-title">
+        <form method="post" action="@Model.Action" class="bp-dialog-form">
+            @Html.AntiForgeryToken()
+            <h2 id="swap-id-title">Change its battery ID</h2>
+            <p class="bp-help-text">Now <strong>@Model.From.SerialNumber</strong> (@IdentifierForms.Display(Model.From.Identifier)). The battery takes another released serial number and its battery ID; @Model.From.SerialNumber is available again. Once the passport is published, the ID is fixed.</p>
+            @if (Model.Available.Count == 0)
+            {
+                <p class="mb-0">No other battery ID is available for this battery's model. Release some on the <a href="/admin/battery-ids">Battery IDs</a> page.</p>
+                <div class="bp-dialog-actions">
+                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Close</button>
+                </div>
+            }
+            else
+            {
+                <label>
+                    <span>New serial number</span>
+                    <select name="serialNumber" required>
+                        <option value="">Choose a released serial number</option>
+                        @foreach (var item in Model.Available)
+                        {
+                            <option value="@item.SerialNumber">@item.SerialNumber · @item.Reference</option>
+                        }
+                    </select>
+                </label>
+                <div class="bp-dialog-actions">
+                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
+                    <button type="submit" class="bp-primary-button">Change the battery ID</button>
+                </div>
+            }
+        </form>
+    </dialog>
+</div>
diff --git a/web/Views/BatteryIds/Index.cshtml b/web/Views/BatteryIds/Index.cshtml
new file mode 100644
index 0000000..2c0241f
--- /dev/null
+++ b/web/Views/BatteryIds/Index.cshtml
@@ -0,0 +1,266 @@
+@using BatteryPassWeb.Controllers
+@using BatteryPassWeb.Dpp.Identifiers
+@using BatteryPassWeb.Dpp.Pages
+@model BatteryIdsPage
+@{
+    ViewData["Title"] = "Battery IDs";
+    var filter = Model.Filter;
+    var here = Context.Request.Path + Context.Request.QueryString;
+    var exportQuery = Context.Request.QueryString.HasValue ? Context.Request.QueryString.Value : string.Empty;
+    var filtered = !string.IsNullOrEmpty(filter.Status) || !string.IsNullOrEmpty(filter.Reference) || !string.IsNullOrEmpty(filter.ModelId) || !string.IsNullOrEmpty(filter.Query);
+    var problems = Model.Refusal?.Problems ?? [];
+}
+
+<main class="bp-page bp-admin-page">
+    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
+    <link rel="stylesheet" href="~/css/bp-battery-ids.css" asp-append-version="true" />
+    @await Html.PartialAsync("_AdminTabs", "battery-ids")
+    @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
+
+    <section class="bp-card bp-product-editor-section bp-console-form-card mt-4" id="release">
+        <div class="bp-product-section-heading">
+            <div>
+                <h2>Release battery IDs</h2>
+                <p class="bp-help-text mb-0">Reserve serial numbers before the batteries are made, so their labels can be printed. Each gets its battery ID (#6) now, fixed for life. A battery is then created by picking one.</p>
+            </div>
+        </div>
+
+        @if (problems.Count > 0)
+        {
+            <div class="bp-release-problems" role="alert">
+                <h3>Fix these, then release again</h3>
+                <p>@Words.Count(problems.Count, "serial number or range", "serial numbers or ranges") can't be released, so none was. The form keeps what you typed.</p>
+                <div class="bp-table-scroll">
+                    <table class="table align-middle mb-0 bp-console-table bp-problems-table">
+                        <thead><tr><th>Serial number or range</th><th>Why not</th></tr></thead>
+                        <tbody>
+                            @foreach (var problem in problems)
+                            {
+                                <tr><td data-label="Serial number or range"><code>@problem.What</code></td><td data-label="Why not">@problem.Reason</td></tr>
+                            }
+                        </tbody>
+                    </table>
+                </div>
+            </div>
+        }
+
+        <form method="post" action="/admin/battery-ids" class="bp-release-form">
+            @Html.AntiForgeryToken()
+            <div class="bp-release-grid">
+                <label>
+                    <span>Reference</span>
+                    <input type="text" name="reference" value="@Model.Form.Reference" maxlength="@BatteryIdPool.MaxReference" placeholder="e.g. PO 4512, line 2" required />
+                    <small class="bp-field-help">Shown in the list and the export.</small>
+                </label>
+                <label>
+                    <span>Model <span class="bp-attr-optional">(optional)</span></span>
+                    <select name="modelId">
+                        <option value="">Any model</option>
+                        @foreach (var (id, name) in Model.Models)
+                        {
+                            <option value="@id" selected="@(id == Model.Form.ModelId)">@name</option>
+                        }
+                    </select>
+                    <small class="bp-field-help">A model's IDs are offered first when creating a battery of it, and only for it.</small>
+                </label>
+            </div>
+
+            <fieldset class="bp-release-ranges" data-ranges>
+                <legend>Ranges</legend>
+                <p class="bp-field-help">The start and end share the letters before the number and its number of digits. The quantity checks the count.</p>
+                @foreach (var range in Model.Form.Ranges)
+                {
+                    <div class="bp-release-range" data-range-row>
+                        <label><span>Start</span><input type="text" name="start" value="@range.Start" placeholder="VPC-12221" autocomplete="off" /></label>
+                        <label><span>End</span><input type="text" name="end" value="@range.End" placeholder="VPC-12230" autocomplete="off" /></label>
+                        <label><span>Quantity</span><input type="text" name="quantity" value="@range.Quantity" placeholder="10" inputmode="numeric" autocomplete="off" /></label>
+                        <button type="button" class="bp-icon-button" data-remove-range aria-label="Remove this range" title="Remove this range"><svg class="bp-i" aria-hidden="true"><use href="#i-trash" /></svg></button>
+                    </div>
+                }
+                <button type="button" class="bp-text-button" data-add-range>+ Add a range</button>
+            </fieldset>
+
+            <label class="bp-release-list">
+                <span>Individual serial numbers <span class="bp-attr-optional">(optional)</span></span>
+                <textarea name="list" rows="4" placeholder="One per line, e.g.&#10;VPC-12301&#10;VPC-12305">@Model.Form.List</textarea>
+                <small class="bp-field-help">Pasted from a list, one per line. Use them with ranges or on their own.</small>
+            </label>
+
+            <div class="bp-release-actions">
+                <small class="bp-field-help">All or nothing: if any serial number can't be released, none is, and each reason is listed.</small>
+                <button type="submit" class="bp-primary-button">Check and release</button>
+            </div>
+        </form>
+    </section>
+
+    <section class="bp-card bp-product-editor-section bp-console-form-card mt-4" id="releases">
+        <div class="bp-product-section-heading">
+            <div>
+                <h2>Releases</h2>
+                <p class="bp-help-text mb-0">Each release with how many of its IDs are available, used by a draft, published or voided.</p>
+            </div>
+        </div>
+        @if (Model.Releases.Count == 0)
+        {
+            <p class="mb-0">No battery IDs released yet.</p>
+        }
+        else
+        {
+            <div class="bp-table-scroll">
+                <table class="table align-middle mb-0 bp-console-table bp-releases-table">
+                    <thead><tr><th>Reference</th><th>Model</th><th>Released</th><th>IDs</th><th><span class="visually-hidden">Actions</span></th></tr></thead>
+                    <tbody>
+                        @foreach (var release in Model.Releases)
+                        {
+                            <tr class="@(release.ReleaseId == filter.Reference ? "table-active" : null)">
+                                <td data-label="Reference"><strong>@release.Reference</strong></td>
+                                <td data-label="Model">@Model.ModelName(release.ModelId)</td>
+                                <td data-label="Released">@ValueWords.Time(release.ReleasedAtUtc)<small class="bp-batteries-muted d-block">by @release.ReleasedBy</small></td>
+                                <td data-label="IDs">
+                                    <span class="bp-release-counts">
+                                        <strong>@release.Total</strong>
+                                        @foreach (var status in ReleasedIdStatuses.All.Where(status => release.Count(status) > 0))
+                                        {
+                                            <span class="bp-id-status is-@status">@release.Count(status) @ReleasedIdStatuses.Words(status).ToLowerInvariant()</span>
+                                        }
+                                    </span>
+                                </td>
+                                <td data-label="Actions" class="bp-release-row-actions">
+                                    <a href="/admin/battery-ids?release=@Uri.EscapeDataString(release.ReleaseId)#released-ids">Show its IDs</a>
+                                    @if (release.Count(ReleasedIdStatuses.Available) > 0)
+                                    {
+                                        <button type="button" class="bp-text-button is-danger" data-void-unused data-release-id="@release.ReleaseId" data-release-reference="@release.Reference" data-release-available="@release.Count(ReleasedIdStatuses.Available)">Void unused…</button>
+                                    }
+                                </td>
+                            </tr>
+                        }
+                    </tbody>
+                </table>
+            </div>
+        }
+    </section>
+
+    <section class="bp-card bp-product-editor-section bp-console-form-card mt-4" id="released-ids">
+        <div class="bp-product-section-heading">
+            <div>
+                <h2>Released IDs</h2>
+                <p class="bp-help-text mb-0">@Words.Count(Model.Total, "ID")@(filtered ? " match" : null)@(Model.Items.Count < Model.Total ? $"; the first {Model.Items.Count} are shown, the export has them all" : null). The export has a row for each, with its QR code's full address for the label printer.</p>
+            </div>
+            <a class="bp-secondary-button bp-button-compact text-nowrap" href="/admin/battery-ids/export@(exportQuery)"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Export to Excel</a>
+        </div>
+        <form method="get" action="/admin/battery-ids#released-ids" class="bp-release-filter">
+            <label><span>Status</span>
+                <select name="status">
+                    <option value="">All</option>
+                    @foreach (var status in ReleasedIdStatuses.All)
+                    {
+                        <option value="@status" selected="@(status == filter.Status)">@ReleasedIdStatuses.Words(status)</option>
+                    }
+                </select>
+            </label>
+            <label><span>Release</span>
+                <select name="release">
+                    <option value="">All</option>
+                    @foreach (var release in Model.Releases)
+                    {
+                        <option value="@release.ReleaseId" selected="@(release.ReleaseId == filter.Reference)">@release.Reference</option>
+                    }
+                </select>
+            </label>
+            <label><span>Model</span>
+                <select name="model">
+                    <option value="">All</option>
+                    <option value="any" selected="@(filter.ModelId == "any")">Any model</option>
+                    @foreach (var (id, name) in Model.Models)
+                    {
+                        <option value="@id" selected="@(id == filter.ModelId)">@name</option>
+                    }
+                </select>
+            </label>
+            <label class="bp-release-search"><span>Serial number</span><input type="search" name="q" value="@filter.Query" placeholder="Part of one" /></label>
+            <div class="bp-release-filter-actions">
+                <button type="submit" class="bp-secondary-button bp-button-compact">Show</button>
+                @if (filtered)
+                {
+                    <a href="/admin/battery-ids#released-ids">Clear</a>
+                }
+            </div>
+        </form>
+        @if (Model.Items.Count == 0)
+        {
+            <p class="mb-0 mt-3">@(filtered ? "No released ID matches." : "No battery IDs released yet.")</p>
+        }
+        else
+        {
+            <div class="bp-table-scroll mt-3">
+                <table class="table align-middle mb-0 bp-console-table bp-released-table">
+                    <thead><tr><th>Serial number</th><th>Battery ID</th><th>Model</th><th>Reference</th><th>Status</th><th><span class="visually-hidden">Actions</span></th></tr></thead>
+                    <tbody>
+                        @foreach (var item in Model.Items)
+                        {
+                            <tr>
+                                <td data-label="Serial number"><strong>@item.SerialNumber</strong></td>
+                                <td data-label="Battery ID">@await Html.PartialAsync("_IdValue", item.Identifier)</td>
+                                <td data-label="Model">@Model.ModelName(item.ModelId)</td>
+                                <td data-label="Reference">@item.Reference<small class="bp-batteries-muted d-block">@ValueWords.Day(item.ReleasedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))</small></td>
+                                <td data-label="Status">
+                                    <span class="bp-id-status is-@item.Status">@ReleasedIdStatuses.Words(item.Status)</span>
+                                    @if (item.Status == ReleasedIdStatuses.Voided)
+                                    {
+                                        <small class="bp-batteries-muted d-block">@item.VoidReason</small>
+                                    }
+                                </td>
+                                <td data-label="Actions" class="bp-release-row-actions">
+                                    @if (item.PackId is { } packId)
+                                    {
+                                        <a href="@BatteryPages.BatteryHref(packId)">Open battery</a>
+                                    }
+                                    else if (item.Status == ReleasedIdStatuses.Available)
+                                    {
+                                        <button type="button" class="bp-text-button is-danger" data-void-id data-serial="@item.SerialNumber">Void…</button>
+                                    }
+                                </td>
+                            </tr>
+                        }
+                    </tbody>
+                </table>
+            </div>
+        }
+    </section>
+
+    <div class="bp-console-form-card bp-dialog-host">
+        <dialog id="void-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="void-dialog-title">
+            <form method="post" action="/admin/battery-ids/void" class="bp-dialog-form">
+                @Html.AntiForgeryToken()
+                <input type="hidden" name="serialNumber" data-void-serial-field />
+                <input type="hidden" name="returnTo" value="@here" />
+                <h2 id="void-dialog-title">Void battery ID <span data-void-serial-name></span></h2>
+                <p class="bp-help-text">A voided ID is never used again, and stays listed with its reason. Only an available ID can be voided.</p>
+                <label><span>Reason</span><input type="text" name="reason" maxlength="@BatteryIdPool.MaxReason" placeholder="e.g. Label damaged" required /></label>
+                <div class="bp-dialog-actions">
+                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
+                    <button type="submit" class="bp-danger-button">Void the ID</button>
+                </div>
+            </form>
+        </dialog>
+        <dialog id="@BatteryIdsController.ReleaseDialog" class="bp-modal-panel bp-dialog" aria-labelledby="void-unused-title">
+            <form method="post" action="/admin/battery-ids/void-unused" class="bp-dialog-form">
+                @Html.AntiForgeryToken()
+                <input type="hidden" name="releaseId" data-void-release-field />
+                <input type="hidden" name="returnTo" value="@here" />
+                <h2 id="void-unused-title">Void the unused IDs of <span data-void-release-name></span></h2>
+                <p class="bp-help-text">Its <strong data-void-release-count></strong> available IDs are voided and never used again. IDs a battery already has stay as they are.</p>
+                <label><span>Reason</span><input type="text" name="reason" maxlength="@BatteryIdPool.MaxReason" placeholder="e.g. Order cancelled" required /></label>
+                <div class="bp-dialog-actions">
+                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
+                    <button type="submit" class="bp-danger-button">Void the unused IDs</button>
+                </div>
+            </form>
+        </dialog>
+    </div>
+</main>
+
+@section Scripts {
+    <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
+    <script src="~/js/bp-battery-ids.js" asp-append-version="true"></script>
+}
diff --git a/web/Views/Dpp/NotPublishedYet.cshtml b/web/Views/Dpp/NotPublishedYet.cshtml
new file mode 100644
index 0000000..0cd8c1d
--- /dev/null
+++ b/web/Views/Dpp/NotPublishedYet.cshtml
@@ -0,0 +1,16 @@
+@using BatteryPassWeb.Dpp.Identifiers
+@model ReleasedBatteryId
+@* A released battery ID scanned before its passport is published (step 6d, the owner): it is ours, but there is nothing to
+   show yet. No battery data, not even whether a battery has it. *@
+@{
+    ViewData["Title"] = "Not published yet";
+}
+<main class="bp-page">
+    <section class="bp-card bp-report-wide-section bp-not-published">
+        <span class="bp-summary-title">Battery passport</span>
+        <h1 class="h5 mt-2 mb-1">Not published yet</h1>
+        <p class="mb-2">This is a Scania Industrial Batteries battery ID, but the battery's passport isn't published yet. Its passport shows here once it is.</p>
+        <p class="bp-passport-id mb-2"><span class="bp-id-label">Battery ID:</span> <span class="bp-id-value">@IdentifierForms.Display(Model.Identifier)</span></p>
+        <p class="text-secondary small mb-0">Scanned a label on a battery that is already in use? Try again later, or <a href="/">search</a> by the battery's serial number.</p>
+    </section>
+</main>
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
index 38e0c8e..2f1dc8b 100644
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -271,7 +271,7 @@ GET @Model.BasePath/operators</code></pre>
                     <div>
                         <span class="bp-summary-title">Battery creation</span>
                         <h3>Create a draft battery and receive its battery ID.</h3>
-                        <p>For team (global) tokens with read-write access, as on the team's pages. Name the batch (which gives the model and manufacturing site) and the economic operator by their IDs, optionally the cluster, and give the battery's own values by key: <code>batterySerialNumber</code> (#11) is required; <code>thirdPartyServiceProvider</code> (#13), <code>manufacturingDate</code> (#17, year and month) and <code>warrantyPeriod</code> (#19) are optional. The values go through the same checks as on the pages, and a serial number another battery has is refused. The app issues the battery ID (#6). The battery stays a draft, seen by the team only, until it is published.</p>
+                        <p>For team (global) tokens with read-write access, as on the team's pages. Name the batch (which gives the model and manufacturing site) and the economic operator by their IDs, optionally the cluster, and give the battery's own values by key: <code>batterySerialNumber</code> (#11) is required; <code>thirdPartyServiceProvider</code> (#13), <code>manufacturingDate</code> (#17, year and month) and <code>warrantyPeriod</code> (#19) are optional. The values go through the same checks as on the pages. <code>batterySerialNumber</code> must be a serial number released on the Battery IDs page and still available; the battery takes its battery ID (#6). The API can't release serial numbers. The battery stays a draft, seen by the team only, until it is published.</p>
                     </div>
                     <pre class="bp-help-pre"><code>POST @Model.BasePath/batteries
 {
diff --git a/web/Views/Shared/_AdminTabs.cshtml b/web/Views/Shared/_AdminTabs.cshtml
index 71530a4..ec31432 100644
--- a/web/Views/Shared/_AdminTabs.cshtml
+++ b/web/Views/Shared/_AdminTabs.cshtml
@@ -16,6 +16,7 @@
     <label for="bp-admin-mobile-tab-select">Admin section</label>
     <select id="bp-admin-mobile-tab-select" data-admin-mobile-tab-select>
         <option value="/admin/batteries" selected="@IsSelected("batteries")">Batteries</option>
+        <option value="/admin/battery-ids" selected="@IsSelected("battery-ids")">Battery IDs</option>
         <option value="/admin/families" selected="@IsSelected("products")">Battery families</option>
         <option value="/admin/sites-and-operators" selected="@IsSelected("sites-and-operators")">Sites and operators</option>
         <option value="/admin/clusters?tab=clusters" selected="@IsSelected("clusters")">Clusters</option>
@@ -27,6 +28,7 @@
 
 <nav class="bp-tab-row mt-4" aria-label="Administration tabs">
     <a class="@TabClass("batteries")" href="/admin/batteries">Batteries</a>
+    <a class="@TabClass("battery-ids")" href="/admin/battery-ids">Battery IDs</a>
     <a class="@TabClass("products")" href="/admin/families">Battery families</a>
     <a class="@TabClass("sites-and-operators")" href="/admin/sites-and-operators">Sites and operators</a>
     <a class="@TabClass("clusters")" href="/admin/clusters?tab=clusters">Clusters</a>
diff --git a/web/wwwroot/css/bp-battery-ids.css b/web/wwwroot/css/bp-battery-ids.css
new file mode 100644
index 0000000..dc2bcca
--- /dev/null
+++ b/web/wwwroot/css/bp-battery-ids.css
@@ -0,0 +1,324 @@
+/* The "Battery IDs" page (step 6d): the release form, its refusals, the releases and the released IDs. */
+
+.bp-release-form {
+  display: grid;
+  gap: 18px;
+}
+
+.bp-release-grid {
+  display: grid;
+  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
+  gap: 16px;
+}
+
+.bp-release-grid label,
+.bp-release-range label,
+.bp-release-list,
+.bp-release-filter label {
+  display: grid;
+  gap: 4px;
+  min-width: 0;
+}
+
+.bp-release-grid label > span,
+.bp-release-range label > span,
+.bp-release-list > span,
+.bp-release-filter label > span,
+.bp-release-ranges legend {
+  font-size: 0.86rem;
+  font-weight: 600;
+  color: var(--bp-text);
+}
+
+.bp-release-ranges {
+  display: grid;
+  gap: 10px;
+  margin: 0;
+  padding: 14px 16px;
+  border: 1px solid var(--bp-border);
+  border-radius: 12px;
+}
+
+.bp-release-ranges legend {
+  float: left;
+  width: 100%;
+  margin: 0;
+  font-size: 0.95rem;
+}
+
+.bp-release-ranges > p {
+  margin: 0;
+}
+
+.bp-release-range {
+  display: grid;
+  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) 7.5rem 2.5rem;
+  align-items: end;
+  gap: 10px;
+}
+
+.bp-release-range [data-remove-range] {
+  display: inline-flex;
+  align-items: center;
+  justify-content: center;
+  width: 2.5rem;
+  height: 2.4rem;
+  border: 1px solid var(--bp-border);
+  border-radius: 9px;
+  background: var(--bp-surface);
+  color: var(--bp-muted);
+}
+
+.bp-release-range [data-remove-range][hidden] {
+  display: none;
+}
+
+.bp-release-ranges [data-add-range] {
+  justify-self: start;
+}
+
+.bp-release-actions {
+  display: flex;
+  align-items: center;
+  justify-content: space-between;
+  gap: 16px;
+  flex-wrap: wrap;
+}
+
+.bp-release-problems {
+  margin: 14px 0 18px;
+  padding: 14px 16px;
+  border: 1px solid #f1b4b4;
+  border-radius: 12px;
+  background: #fff5f5;
+}
+
+.bp-release-problems h3 {
+  margin: 0 0 4px;
+  font-size: 1rem;
+  color: #a12020;
+}
+
+.bp-release-problems > p {
+  margin: 0 0 10px;
+}
+
+.bp-release-problems table {
+  background: transparent;
+}
+
+.bp-release-filter {
+  display: grid;
+  grid-template-columns: repeat(3, minmax(0, 10rem)) minmax(0, 1fr) auto;
+  align-items: end;
+  gap: 12px;
+}
+
+.bp-release-filter-actions {
+  display: flex;
+  align-items: center;
+  gap: 12px;
+}
+
+.bp-release-counts {
+  display: inline-flex;
+  align-items: center;
+  flex-wrap: wrap;
+  gap: 6px;
+}
+
+.bp-release-row-actions {
+  text-align: right;
+  white-space: nowrap;
+}
+
+.bp-release-row-actions > * + * {
+  margin-left: 14px;
+}
+
+.bp-text-button.is-danger {
+  color: #b42318;
+}
+
+/* A released ID's status: a small calm chip, one colour per status. */
+.bp-id-status {
+  display: inline-flex;
+  align-items: center;
+  min-height: 22px;
+  padding: 0 8px;
+  border-radius: 999px;
+  font-size: 0.78rem;
+  font-weight: 600;
+  white-space: nowrap;
+}
+
+.bp-id-status.is-available {
+  background: #e7f6ee;
+  color: #17663f;
+}
+
+.bp-id-status.is-used {
+  background: #e8f0fc;
+  color: #1d4f91;
+}
+
+.bp-id-status.is-published {
+  background: #edf0f5;
+  color: #0a2850;
+}
+
+.bp-id-status.is-voided {
+  background: #f3f4f6;
+  color: #6b7280;
+}
+
+[data-theme="dark"] .bp-release-problems {
+  border-color: rgba(248, 113, 113, 0.45);
+  background: rgba(127, 29, 29, 0.25);
+}
+
+[data-theme="dark"] .bp-release-problems h3 {
+  color: #fca5a5;
+}
+
+[data-theme="dark"] .bp-id-status.is-available { background: rgba(34, 197, 94, 0.16); color: #86efac; }
+[data-theme="dark"] .bp-id-status.is-used { background: rgba(96, 165, 250, 0.16); color: #93c5fd; }
+[data-theme="dark"] .bp-id-status.is-published { background: rgba(148, 163, 184, 0.18); color: #e2e8f0; }
+[data-theme="dark"] .bp-id-status.is-voided { background: rgba(148, 163, 184, 0.12); color: #94a3b8; }
+[data-theme="dark"] .bp-text-button.is-danger { color: #fca5a5; }
+
+@media (max-width: 767px) {
+  .bp-release-grid,
+  .bp-release-filter {
+    grid-template-columns: minmax(0, 1fr);
+  }
+
+  .bp-release-range {
+    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
+    padding-bottom: 10px;
+    border-bottom: 1px solid var(--bp-border);
+  }
+
+  .bp-release-range label:nth-child(3) {
+    grid-column: 1;
+  }
+
+  .bp-release-row-actions {
+    text-align: left;
+    white-space: normal;
+  }
+}
+
+/* The create page's picker (6d). */
+.bp-id-picker {
+  display: grid;
+  gap: 4px;
+  max-width: 34rem;
+}
+
+.bp-id-picker > span {
+  font-size: 0.86rem;
+  font-weight: 600;
+}
+
+/* On a phone the two lists are compact rows rather than the console's one-card-per-cell layout: a release or an ID in
+   three or four short lines, so a long list stays short to scroll. */
+@media (max-width: 767px) {
+  .bp-problems-table,
+  .bp-problems-table tbody,
+  .bp-problems-table tr,
+  .bp-problems-table td,
+  .bp-releases-table,
+  .bp-releases-table tbody,
+  .bp-released-table,
+  .bp-released-table tbody {
+    display: block;
+    width: 100%;
+    min-width: 0 !important; /* the console table's own minimum width would push the right-hand column off screen */
+  }
+
+  .bp-problems-table thead,
+  .bp-releases-table thead,
+  .bp-released-table thead {
+    display: none;
+  }
+
+  .bp-releases-table tr,
+  .bp-released-table tr {
+    display: grid;
+    grid-template-columns: minmax(0, 1fr) auto;
+    gap: 4px 12px;
+    padding: 12px 2px;
+    border-bottom: 1px solid var(--bp-border);
+  }
+
+  .bp-releases-table td,
+  .bp-released-table td {
+    display: block;
+    min-width: 0;
+    padding: 0 !important;
+    border: 0 !important;
+    background: none !important;
+    box-shadow: none !important;
+  }
+
+  .bp-releases-table td:nth-child(1) { grid-column: 1; grid-row: 1; }
+  .bp-releases-table td:nth-child(4) { grid-column: 1 / -1; grid-row: 3; }
+  .bp-releases-table td:nth-child(2) { grid-column: 2; grid-row: 1; text-align: right; color: var(--bp-muted); font-size: 0.86rem; }
+  .bp-releases-table td:nth-child(3) { grid-column: 1 / -1; grid-row: 2; font-size: 0.86rem; }
+  .bp-releases-table td:nth-child(3) small { display: inline !important; margin-left: 4px; }
+  .bp-releases-table td:nth-child(5) { grid-column: 1 / -1; grid-row: 4; text-align: left; }
+
+  .bp-released-table td:nth-child(1) { grid-column: 1; grid-row: 1; }
+  .bp-released-table td:nth-child(5) { grid-column: 2; grid-row: 1; text-align: right; }
+  .bp-released-table td:nth-child(5) small { max-width: 10rem; }
+  .bp-released-table td:nth-child(2) { grid-column: 1 / -1; grid-row: 2; }
+  .bp-released-table td:nth-child(3),
+  .bp-released-table td:nth-child(4) { grid-row: 3; font-size: 0.84rem; color: var(--bp-muted); }
+  .bp-released-table td:nth-child(3) { grid-column: 1; }
+  .bp-released-table td:nth-child(4) { grid-column: 2; text-align: right; }
+  .bp-released-table td:nth-child(4) small { display: none !important; }
+  .bp-released-table td:nth-child(6) { grid-column: 1 / -1; grid-row: 4; text-align: left; }
+  .bp-released-table td:nth-child(6):empty { display: none; }
+
+  /* These lists fit the screen, so the "Swipe" hint site.css puts under phone tables isn't wanted. */
+  .bp-problems-table tr {
+    padding: 8px 0;
+    border-bottom: 1px solid #f1b4b4;
+  }
+
+  .bp-problems-table td,
+  .bp-releases-table td,
+  .bp-released-table td {
+    padding: 0 !important;
+    border: 0 !important;
+    background: none !important;
+    white-space: normal !important; /* the console table keeps cells on one line, wider than a phone */
+    overflow-wrap: anywhere;
+  }
+
+  .bp-table-scroll:has(.bp-problems-table, .bp-releases-table, .bp-released-table) {
+    overflow-x: visible;
+    padding-bottom: 0;
+  }
+
+  .bp-table-scroll:has(.bp-problems-table, .bp-releases-table, .bp-released-table)::after {
+    display: none;
+  }
+
+  .bp-releases-table .bp-text-button,
+  .bp-released-table .bp-text-button {
+    padding-left: 0;
+  }
+
+  .bp-released-table .bp-id-copy-row {
+    min-width: 0;
+    max-width: 100%;
+  }
+
+  .bp-released-table .bp-id-value {
+    min-width: 0;
+    white-space: normal;
+    overflow-wrap: anywhere;
+  }
+}
diff --git a/web/wwwroot/js/bp-battery-ids.js b/web/wwwroot/js/bp-battery-ids.js
new file mode 100644
index 0000000..11c0f78
--- /dev/null
+++ b/web/wwwroot/js/bp-battery-ids.js
@@ -0,0 +1,83 @@
+// The "Battery IDs" page (step 6d): more ranges in one release, and the void windows. Without JavaScript one range and
+// the list still release, and the IDs can be voided from their own rows' forms in a later step.
+(function () {
+    const ranges = document.querySelector('[data-ranges]');
+    const add = ranges?.querySelector('[data-add-range]');
+    const sync = () => {
+        const rows = ranges.querySelectorAll('[data-range-row]');
+        rows.forEach((row) => { row.querySelector('[data-remove-range]').hidden = rows.length === 1; });
+    };
+    if (ranges && add) {
+        add.addEventListener('click', () => {
+            const rows = ranges.querySelectorAll('[data-range-row]');
+            const copy = rows[rows.length - 1].cloneNode(true);
+            copy.querySelectorAll('input').forEach((input) => { input.value = ''; });
+            add.before(copy);
+            copy.querySelector('input')?.focus();
+            sync();
+        });
+        ranges.addEventListener('click', (event) => {
+            const remove = event.target.closest('[data-remove-range]');
+            if (remove && ranges.querySelectorAll('[data-range-row]').length > 1) {
+                remove.closest('[data-range-row]').remove();
+                sync();
+            }
+        });
+        sync();
+    }
+
+    const voidDialog = document.getElementById('void-dialog');
+    document.querySelectorAll('[data-void-id]').forEach((button) => {
+        button.addEventListener('click', () => {
+            voidDialog.querySelector('[data-void-serial-field]').value = button.dataset.serial;
+            voidDialog.querySelector('[data-void-serial-name]').textContent = button.dataset.serial;
+            voidDialog.showModal();
+        });
+    });
+
+    const unusedDialog = document.getElementById('void-unused-dialog');
+    document.querySelectorAll('[data-void-unused]').forEach((button) => {
+        button.addEventListener('click', () => {
+            unusedDialog.querySelector('[data-void-release-field]').value = button.dataset.releaseId;
+            unusedDialog.querySelector('[data-void-release-name]').textContent = button.dataset.releaseReference;
+            unusedDialog.querySelector('[data-void-release-count]').textContent = button.dataset.releaseAvailable;
+            unusedDialog.showModal();
+        });
+    });
+})();
+
+// The create page's picker (6d): once a batch is chosen, only the IDs its model may use are offered, its model's first.
+(function () {
+    const batch = document.querySelector('[data-batch-select]');
+    const picker = document.querySelector('[data-id-picker]');
+    if (!batch || !picker) {
+        return;
+    }
+
+    const note = document.querySelector('[data-id-picker-note]');
+    const groups = Array.from(picker.querySelectorAll('optgroup'));
+    const words = (count) => `${count} ${count === 1 ? 'ID' : 'IDs'}`;
+    const filter = () => {
+        const model = batch.value ? (batch.selectedOptions[0]?.dataset.model ?? null) : null;
+        let offered = 0;
+        groups.forEach((group) => {
+            const usable = model === null || group.dataset.model === '' || group.dataset.model === model;
+            group.hidden = !usable;
+            group.disabled = !usable;
+            offered += usable ? group.children.length : 0;
+        });
+        if (picker.selectedOptions[0]?.parentElement?.disabled) {
+            picker.value = '';
+        }
+
+        if (note) {
+            note.textContent = model === null
+                ? `${words(offered)} available. Choose the batch first to see only those its model may use.`
+                : offered === 0
+                    ? 'No released ID may be used for this batch’s model. Release some on the Battery IDs page.'
+                    : `${words(offered)} available for this batch’s model, its own first.`;
+        }
+    };
+    batch.addEventListener('change', filter);
+    filter();
+})();
```

- [ ] **Step 4: See the tests pass**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~BatteryIdPoolTests|FullyQualifiedName~ReplicaSetReleaseTests|FullyQualifiedName~DunsIdentifierTests|FullyQualifiedName~ResetCollectionsTests"`
Expected: `Passed!  - Failed:     0, Passed:   70`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs BatteryPassWeb.Tests/Dpp/ReplicaSetReleaseTests.cs BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs BatteryPassWeb.Tests/Reset/ResetCollectionsTests.cs web/Controllers/BatteryIdsController.cs web/Dpp/Identifiers/BatteryIdPool.cs web/Dpp/Identifiers/ReleasedBatteryId.cs web/Dpp/Identifiers/XlsxWriter.cs web/Views/Batteries/_SwapIdDialog.cshtml web/Views/BatteryIds/Index.cshtml web/Views/Dpp/NotPublishedYet.cshtml web/wwwroot/css/bp-battery-ids.css web/wwwroot/js/bp-battery-ids.js tools/V3Reset/ResetCollections.cs web/Controllers/BatteriesController.cs web/Controllers/PassportController.cs web/Dpp/Api/BatteryApi.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Editing/PackCreationService.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Publication/PublicationService.cs web/Dpp/Store/DppStore.cs web/Views/Admin/AuditLog.cshtml web/Views/Batteries/Battery.cshtml web/Views/Batteries/NewBattery.cshtml web/Views/Help/Index.cshtml web/Views/Shared/_AdminTabs.cshtml
git commit -m "feat(dpp): battery IDs released in advance: the Battery IDs page, picking at creation, swap while a draft, Not published yet" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2602, Skipped:     0, Total:  2602`. That is the 2579 from before plus 23 new test cases.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat db7bd45 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

These are not steps of this plan. The managing session brings them to the product owner, on the v3 app started with the three ID settings and after "Reset demo data":

1. Admin → Battery IDs: release VPC-12221 to VPC-12230 (10) for PO 4512, line 2, Compact 7M. "Export to Excel" opens in Excel with every ID and its QR address.
2. Release VPC-12228 to VPC-12240 with quantity 12: nothing is released, and the reasons are listed.
3. Batteries → Create battery: choose a Compact 7M batch, pick VPC-12221, create. The draft shows UN778474481VPC12221 and its QR code.
4. On the draft, More → Change its battery ID…: VPC-12222. VPC-12221 is available again.
5. Signed out, open /25S/UN778474481VPC12223: "Not published yet".

## Next

Plan 6d-3: the team's Help for the release flow, the test pack and the scenario matrix, and the role check re-run for the new page and the changed create flow.
