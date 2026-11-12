# DPP V3 Step 4c-1: The New Linked Passport — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A battery placed on the market again gets a new passport linked to the old one, and the old one becomes inactive (design 7.5; decisions 13, 19 and 34; J-2, J-13). The status window gains the three choices that do this: "Prepared for re-use and placed on the market again", "Remanufactured" and "Repurposed". The battery then waits for its new passport, and the status card asks who places it on the market again (mock-up S4).
- **Scania:** the team makes the new passport as a draft that starts from the old battery, types its warranty and publishes it. Publishing makes the old passport inactive.
- **Another operator:** the team or the battery's cluster admins enter the operator, then link its new passport by ID or address. Linking makes the old passport inactive.
- **A mistake:** the team cancels the new passport and the old status comes back.

An inactive passport stays readable and takes no more changes. The passport pages link the old and the new passport (S7), and the new battery's pages name the earlier one (S5). The demo data gets a repurposed battery with its published new passport. "Invalid" and "marked for deletion" follow in 4c-2.

**Architecture:**
- **The passport's status is its newest version's** (`DppStore.PassportStatusAsync`): null for a draft, else active, inactive (and from 4c-2 invalid or marked-for-deletion).
  - `DppStatuses.IsNoLongerActive` covers the last three. Such a passport takes no change through the save pipeline (every key refused, "the passport is inactive; nothing on it changes"), no status change or note, and no new version: the team's "Publish" refuses it.
  - Without this, a passport with no active version looked like a draft, and "Publish" would have started a new active version.
  - The version's #2 is set in place (`SetVersionStatusAsync`, #4 moves too). #2 isn't signed (4.3), so no new version is made.
- **The succession is app data on the pack:** `BatteryPack.Succession` (status before and after, Scania's new pack or another operator's details and passport, when linked) and, on the new pack, `SuccessorOf`.
  - `StatusChangeService` starts the succession for the three new choices. While it waits, only the team's correction is accepted.
  - `SuccessionService` makes Scania's new pack through `PackCreationService` with `NewPack.PredecessorPackId`. The duplicate-serial rule then lets the old battery's serial number through for its new passport only.
  - The same service links another operator's passport and cancels.
  - `PublicationService` makes the old passport inactive when the new pack's version 1 is published (`InactivateAsync`).
- **Lookups:**
  - By battery ID, an inactive passport still opens (its newest version).
  - By serial number, the passport pages already count only active passports, and the API's write target now skips an inactive battery when two share the number.
- **Pages:**
  - The status card gets the S4 block and the S5 line, and the window the three choices.
  - The battery list, the team's battery page and the versions card name "Inactive". The live values and accident cards of an inactive battery show one line instead of their forms.
  - The Registry lists only active passports, so an inactive battery continues on its new one.
  - The API's `dppStatus` reports "inactive".
  - The passport pages show the S7 line, and the hero's pill names the version's status.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 7.4, 7.5 and 12, and section 14's step-4 note. Decisions record: decisions 13, 19 and 34; J-2, J-13. Mock-up (approved 31 Oct 2026): `docs/mockups/dpp-v3-step4-purchaser-and-lifecycle.html`, screens S3 (the three choices), S4, S5 and S7.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 03a61b5 (the end of plan 4b's run), plus the commit that adds this plan. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo).
- **Builds and test runs:** run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. Whole-suite runs also take `--blame-hang --blame-hang-timeout 10m`. A few runs took over 20 minutes while plans 4a, 4b and 4c-1 were measured. In 30 runs of the demo tests alone, nothing hung, but one run took 16 minutes with no single test over 3 minutes: a slowdown, not a stuck test. Its cause is still being chased. If a run seems stuck for more than 20 minutes, stop it (and any `dotnet` and `testhost` it left) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2178`. After this plan: 2199. The suite takes about two to four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../4c1-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New and rewritten files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (1 Nov 2026, through the managing session)

Decision 34 holds step 4's answers; these are 4c's, added to it by this plan's commit.

1. **4c is two plans:** this one, then 4c-2 ("invalid" and "marked for deletion").
2. **Scania's new passport starts from the old battery:**
   - a new battery ID;
   - the same serial number (the same battery), batch and cluster;
   - the service provider, manufacturing date, module list and the re-stated values copied;
   - its status as what happened;
   - the warranty left for the team before publishing.
   - The repair history and the accident reports stay with the old passport.
3. **The team picks the operator,** with the old battery's operator pre-selected. Only the team makes Scania's new passport; a cluster admin's card says the team makes it.
4. **"Cancel the new passport"**, for the team only, while the old passport isn't inactive. A draft new battery is removed and the old status comes back, with a new version.
5. **An inactive passport takes no more changes:** no live values, accident reports, status changes or notes. Feeds and uploads that find a battery by serial number pick the battery that isn't inactive.
6. **Another operator's details** are saved without the new passport's ID or address; "Link and set this passport inactive" needs it. The team or the battery's cluster admins fill this in.
7. **Demo:** DEMO-C13M-0001 is repurposed, and its new passport is made, given a three-year warranty and published.

## Found while prototyping, and what this plan does

- **The new passport also carries the battery's last measured values** (#63, #83, #87, E1–E4), with their measurement times: the publication checks require #63 and #87, and it is the same battery. #92 stays the new passport's own accident page. This wasn't in the list the owner answered; it follows from it.
- **"Link" without the new passport's ID kept nothing typed.** The details are now saved first, and the card says "The details are saved, but not linked".
- **Publishing never turned "inactive" or "invalid" into "archived"**: `ArchiveOtherVersionsAsync` touches active versions only. The gap was the other way round: a passport with no active version looked like a draft (see Architecture).
- **The Registry assumed every non-draft row has an active version** (`row.Active!`). It now lists active passports only.
- **The battery list's rows get `Latest`** (the newest version, whatever its status) beside `Active`, so a list tells an inactive passport from a draft. The two APIs' `dppStatus` reads it.

## What happens to the old tests

Nothing is removed. Some tests change with the code:
- **The status window's choices**, now six for the team and five for a cluster admin: two tests in `StatusPagesTests` (Task 2).
- **`StatusChangeTests`** (Task 2):
  - `Pages`, since `StatusPages` takes the succession service.
  - The repair helper, which now awaits (`RepairAsync`) instead of blocking with `GetAwaiter().GetResult()`. A blocking wait inside xUnit's few-threaded context can stall other tests.
- **The hero's pill** shows the version's status in words: `BatteryRouteAndRegistryTests` (Task 2).
- **The demo tests' counts:** 8 batteries, 5 published, 9 versions, one of them inactive (Task 3).

## A part to write yourself (optional)

The product owner chose "you write it" before; the plan gives the code. If they want to try one, the natural spot is `SuccessionService.CreateSuccessorAsync` in Task 1: what the new passport starts with. `TheTeam_ShouldMakeTheNewPassportAsADraftThatStartsFromTheOldBattery` lists it.

---

### Task 1: Inactive passports and the new linked passport

The rules before the pages: the passport's status is its newest version's, and an inactive one takes no change, status change, note or new version. The three choices that place a battery on the market again start a succession; the team makes Scania's new passport from the old battery (its serial number allowed through for it only), another operator's is linked by its ID or address, and the team can cancel. Publishing Scania's new passport, or linking another operator's, makes the old one inactive. By battery ID an inactive passport still opens; by serial number the API picks the battery that isn't inactive.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/SuccessionTests.cs`
- Modify: `web/Dpp/Api/BatteryApi.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Editing/PackCreationService.cs`
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Modify: `web/Dpp/Editing/StatusChangeService.cs`
- Create: `web/Dpp/Editing/SuccessionService.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`
- Modify: `web/Dpp/Publication/PublicationService.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Store/DppStore.cs`
- Modify: `web/Dpp/Store/PassportVersion.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/SuccessionTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The new linked passport (design 7.5, decisions 13, 19 and 34; step 4c-1): placed on the market again, the old passport
/// waits for its successor; Scania's is a new draft made by the team, another operator's is linked by its ID or address;
/// once the new one is published or linked, the old one is inactive and takes no more changes.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SuccessionTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    [Theory]
    [InlineData(StatusEvent.PlacedOnMarketAgain, "re-used")]
    [InlineData(StatusEvent.Remanufactured, "remanufactured")]
    [InlineData(StatusEvent.Repurposed, "repurposed")]
    public async Task PlacedOnTheMarketAgain_ShouldSetTheStatusAndWaitForTheNewPassport(StatusEvent what, string status)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await StatusChangeTests.Service(rig).ChangeAsync(NorthAdmin, rig.PackId, new StatusChange(what, await StatusChangeTests.CurrentAsync(rig)));

        Assert.Null(result.Problem);
        Assert.Equal(status, result.NewStatus);
        Assert.Equal(DppStatuses.Active, result.Version!.DppStatus); // J-13: still active until the new one comes
        var succession = (await PackAsync(rig)).Succession!;
        Assert.Equal("original", succession.StatusBefore);
        Assert.Equal(status, succession.Status);
        Assert.Null(succession.SuccessorPackId);
        Assert.Null(succession.LinkedAtUtc);
    }

    [Fact]
    public async Task AWaitingPassport_ShouldTakeNoOtherStatusChangeButTheTeamsCorrection()
    {
        var rig = await RepurposedAsync();
        var service = StatusChangeTests.Service(rig);

        var repair = await service.ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Repaired, await StatusChangeTests.CurrentAsync(rig), "2026-10-20", "Fan."));
        var correction = await service.ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Corrected, await StatusChangeTests.CurrentAsync(rig)));

        Assert.Equal(StatusChangeService.WaitingForSuccessor, repair.Problem);
        Assert.Null(correction.Problem);
        Assert.Equal("repurposed", correction.NewStatus);
    }

    [Fact]
    public async Task TheTeam_ShouldMakeTheNewPassportAsADraftThatStartsFromTheOldBattery()
    {
        var rig = await RepurposedAsync();
        var old = await PackAsync(rig);

        var (successorId, problem) = await Service(rig).CreateSuccessorAsync(Team, rig.PackId, old.OperatorId);

        Assert.Null(problem);
        var successor = (await rig.Store.GetAsync<BatteryPack>(successorId!))!;
        Assert.NotEqual(Text(old, "uniqueProductIdentifier"), Text(successor, "uniqueProductIdentifier")); // a new #6 (J-2)
        Assert.Equal(Text(old, "batterySerialNumber"), Text(successor, "batterySerialNumber"));
        Assert.Equal(Text(old, "thirdPartyServiceProvider"), Text(successor, "thirdPartyServiceProvider"));
        Assert.Equal(Text(old, "manufacturingDate"), Text(successor, "manufacturingDate"));
        Assert.False(successor.Values.ContainsKey("warrantyPeriod")); // the team types the new warranty
        Assert.Equal("repurposed", successor.Values["batteryStatus"].Value!["batteryStatusValues"]!.GetValue<string>());
        Assert.Equal(4, successor.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>()); // the re-stated values
        Assert.Equal(old.Values["stateOfChargeSoC"].Value!.ToJsonString(), successor.Values["stateOfChargeSoC"].Value!.ToJsonString()); // the last measured values
        Assert.Equal(old.Values["stateOfChargeSoC"].GeneratedAtUtc, successor.Values["stateOfChargeSoC"].GeneratedAtUtc);
        Assert.NotEqual(old.Values["informationOnAccidents"].Value!.ToJsonString(), successor.Values["informationOnAccidents"].Value!.ToJsonString()); // its own accident page
        Assert.Equal(old.BatchId, successor.BatchId);
        Assert.Equal(old.ClusterId, successor.ClusterId);
        Assert.Equal(rig.PackId, successor.SuccessorOf);
        Assert.Empty(successor.Repairs);
        Assert.Equal(successorId, (await PackAsync(rig)).Succession!.SuccessorPackId);
        Assert.Equal(0, await rig.Store.GetLatestVersionNumberAsync(successorId!)); // a draft
        Assert.NotNull(await rig.Store.GetActiveVersionAsync(rig.PackId)); // the old one stays active (J-13)
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldMakeScaniasNewPassportAndOnlyWhenOneIsAwaited()
    {
        var waiting = await RepurposedAsync();
        var plain = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var old = await PackAsync(waiting);

        Assert.Equal(SuccessionService.TeamMakesIt, (await Service(waiting).CreateSuccessorAsync(NorthAdmin, waiting.PackId, old.OperatorId)).Problem);
        Assert.Equal(SuccessionService.NotAwaited, (await Service(plain).CreateSuccessorAsync(Team, plain.PackId, old.OperatorId)).Problem);
        Assert.Equal(SuccessionService.NoOperator, (await Service(waiting).CreateSuccessorAsync(Team, waiting.PackId, "operator-unknown")).Problem);
        var made = await Service(waiting).CreateSuccessorAsync(Team, waiting.PackId, old.OperatorId);
        Assert.Equal(SuccessionService.AlreadyMade, (await Service(waiting).CreateSuccessorAsync(Team, waiting.PackId, old.OperatorId)).Problem);
        Assert.Null(made.Problem);
    }

    [Fact]
    public async Task PublishingTheNewPassport_ShouldMakeTheOldOneInactive()
    {
        var rig = await RepurposedAsync();
        var (successorId, _) = await Service(rig).CreateSuccessorAsync(Team, rig.PackId, (await PackAsync(rig)).OperatorId);
        await TypeWarrantyAsync(rig, successorId!);
        rig.Clock.Now = rig.Clock.Now.AddHours(1);

        var published = await rig.Publication.PublishAsync(Team, successorId!);

        Assert.Null(published.Problem);
        Assert.Null(await rig.Store.GetActiveVersionAsync(rig.PackId));
        var last = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;
        Assert.Equal(DppStatuses.Inactive, last.DppStatus);
        Assert.Equal(rig.Clock.Now.UtcDateTime, last.LastUpdatedUtc); // #4: the status was made available now
        Assert.Equal(rig.Clock.Now.UtcDateTime, (await PackAsync(rig)).Succession!.LinkedAtUtc);
        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
        var entry = await LastAsync(rig, PublicationService.InactiveEvent);
        Assert.Equal(rig.PackId, entry["entityId"].AsString);
    }

    [Fact]
    public async Task AnInactivePassport_ShouldTakeNoMoreChanges()
    {
        var rig = await InactiveAsync();

        var live = await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms", BatteryTesting.North), new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 40 }, "feed"));
        var status = await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Corrected, await StatusChangeTests.CurrentAsync(rig)));
        var note = await StatusChangeTests.Service(rig).AddNoteAsync(Team, rig.PackId, "A note.");
        var publish = await rig.Publication.PublishAsync(Team, rig.PackId);

        Assert.False(live.Saved);
        Assert.All(live.Keys, key => Assert.Equal(KeyOutcome.Rejected, key.Outcome));
        Assert.Contains("inactive", Assert.Single(live.Keys).Message, StringComparison.Ordinal);
        Assert.Equal(StatusChangeService.NoLongerActive("inactive"), status.Problem);
        Assert.Equal(StatusChangeService.NoLongerActive("inactive"), note);
        Assert.Equal(PublicationService.NoNewVersion("inactive"), publish.Problem);
        Assert.Equal(DppStatuses.Inactive, (await rig.Store.GetLatestVersionAsync(rig.PackId))!.DppStatus);
    }

    [Fact]
    public async Task TheSerialNumber_ShouldFindTheBatteryThatIsNotInactiveAndTheOldIdItsOwnPassport()
    {
        var rig = await InactiveAsync();
        var old = await PackAsync(rig);
        var serial = Text(old, "batterySerialNumber")!;
        var successorId = old.Succession!.SuccessorPackId!;

        var bySerial = await PassportPagesTests.Pages(rig).FindAsync(serial);
        var byOldId = await PassportPagesTests.Pages(rig).FindAsync(Text(old, "uniqueProductIdentifier"));
        var feedTarget = await BatteryApiTests.Api(rig).FindAsync(ChangeSender.Feed("bms", BatteryTesting.North), serial);
        var teamTarget = await BatteryApiTests.Api(rig).FindAsync(Team, serial);

        Assert.Equal(successorId, bySerial!.PackId);
        Assert.Equal(rig.PackId, byOldId!.PackId);
        Assert.Equal(DppStatuses.Inactive, byOldId.DppStatus);
        Assert.Equal(successorId, feedTarget.Pack?.Id); // feeds by serial number reach the battery that isn't inactive
        Assert.Equal(successorId, teamTarget.Pack?.Id);
    }

    [Fact]
    public async Task AnotherOperator_ShouldBeSavedFirstAndLinkedOnceItsPassportIsKnown()
    {
        var rig = await RepurposedAsync();
        var service = Service(rig);
        var details = new OtherOperator("urn:test:operator:reman-ab", "Example Remanufacturing AB, Box 1, 111 11 Stockholm", null);

        var saved = await service.SaveOtherOperatorAsync(NorthAdmin, rig.PackId, details, link: false);
        var tooEarly = await service.SaveOtherOperatorAsync(NorthAdmin, rig.PackId, details, link: true);
        var otherCluster = await service.SaveOtherOperatorAsync(ChangeSender.ClusterAdmin("south@example.com", BatteryTesting.South), rig.PackId, details, link: false);
        Assert.NotNull(await rig.Store.GetActiveVersionAsync(rig.PackId));
        var linked = await service.SaveOtherOperatorAsync(NorthAdmin, rig.PackId, details with { Passport = "https://example.com/passports/reman-1" }, link: true);

        Assert.Null(saved);
        Assert.Equal(SuccessionService.LinkNeedsPassport, tooEarly);
        Assert.Equal("urn:test:operator:reman-ab", (await PackAsync(rig)).Succession!.OtherOperatorId); // kept for the next try
        Assert.Equal(SuccessionService.NotYours, otherCluster);
        Assert.Null(linked);
        var succession = (await PackAsync(rig)).Succession!;
        Assert.Equal("Example Remanufacturing AB, Box 1, 111 11 Stockholm", succession.OtherOperatorInfo);
        Assert.Equal("https://example.com/passports/reman-1", succession.OtherPassport);
        Assert.NotNull(succession.LinkedAtUtc);
        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task TheTeam_ShouldCancelTheNewPassportAndPutTheOldStatusBack()
    {
        var rig = await RepurposedAsync();
        var service = Service(rig);
        var (successorId, _) = await service.CreateSuccessorAsync(Team, rig.PackId, (await PackAsync(rig)).OperatorId);

        Assert.Equal(SuccessionService.TeamCancels, await service.CancelAsync(NorthAdmin, rig.PackId));
        var cancelled = await service.CancelAsync(Team, rig.PackId);

        Assert.Null(cancelled);
        var pack = await PackAsync(rig);
        Assert.Null(pack.Succession);
        Assert.Equal("original", pack.Values["batteryStatus"].Value!["batteryStatusValues"]!.GetValue<string>());
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(successorId!));
        Assert.Equal(3, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number); // published, repurposed, put back
        Assert.Equal(rig.PackId, (await LastAsync(rig, SuccessionService.CancelledEvent))["entityId"].AsString);
    }

    [Fact]
    public async Task AnInactivePassport_ShouldNotBeCancelled()
    {
        var rig = await InactiveAsync();

        Assert.Equal(SuccessionService.NotAwaited, await Service(rig).CancelAsync(Team, rig.PackId));
    }

    [Fact]
    public async Task TheNewPassport_ShouldKeepItsSerialNumberWhileADraft()
    {
        var rig = await RepurposedAsync();
        var (successorId, _) = await Service(rig).CreateSuccessorAsync(Team, rig.PackId, (await PackAsync(rig)).OperatorId);
        var serial = Text(await PackAsync(rig), "batterySerialNumber")!;

        var same = await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, successorId!, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = $" {serial.ToLowerInvariant()} " }, "form"));
        var another = await new PackCreationService(rig.Store, rig.Pipeline, new BatteryPassWeb.Dpp.Identifiers.TestIdentifierScheme(), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock)
            .CreateAsync(Team, new NewPack(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, BatteryTesting.North, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, "form"));

        Assert.DoesNotContain(same.Keys, key => key.Message == SavePipeline.SerialNumberTaken);
        Assert.Null(another.Pack); // an unrelated battery still can't take it
    }

    internal static SuccessionService Service(PublicationRig rig) =>
        new(rig.Store, BatteryTesting.Creation(rig), rig.Pipeline, rig.Publication, new ApplicationAuditService(rig.Context), rig.Clock);

    /// <summary>The seed pack, published and then repurposed by the team: waiting for its new passport.</summary>
    internal async Task<PublicationRig> RepurposedAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var result = await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Repurposed, await StatusChangeTests.CurrentAsync(rig)));
        Assert.Null(result.Problem);
        return rig;
    }

    /// <summary>Repurposed, with Scania's new passport published: the old passport is inactive.</summary>
    internal async Task<PublicationRig> InactiveAsync()
    {
        var rig = await RepurposedAsync();
        var (successorId, problem) = await Service(rig).CreateSuccessorAsync(Team, rig.PackId, (await PackAsync(rig)).OperatorId);
        Assert.Null(problem);
        await TypeWarrantyAsync(rig, successorId!);
        Assert.Null((await rig.Publication.PublishAsync(Team, successorId!)).Problem);
        return rig;
    }

    private static async Task TypeWarrantyAsync(PublicationRig rig, string packId) =>
        Assert.True((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, packId, new Dictionary<string, JsonNode?> { ["warrantyPeriod"] = "P5Y" }, "form"))).Saved);

    private static Task<BatteryPack> PackAsync(PublicationRig rig) => rig.Store.GetAsync<BatteryPack>(rig.PackId)!;

    private static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~SuccessionTests | FullyQualifiedName~StatusChangeTests | FullyQualifiedName~PublicationServiceTests"`
Expected: the build fails with CS0117, CS0246 (for example `error CS0117 'StatusEvent' does not contain a definition for 'PlacedOnMarketAgain'`).

- [ ] **Step 3: Write the code**

Patch `4c1-task1-code.patch` (`web/Dpp/Api/BatteryApi.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Editing/PackCreationService.cs`, `web/Dpp/Editing/SavePipeline.cs`, `web/Dpp/Editing/StatusChangeService.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Dpp/Publication/PublicationService.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Store/DppStore.cs`, `web/Dpp/Store/PassportVersion.cs`). Save the block as `../4c1-task1-code.patch`, outside the worktree, and run `git apply ../4c1-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Api/BatteryApi.cs b/web/Dpp/Api/BatteryApi.cs
index 6446c0e..aa425cb 100644
--- a/web/Dpp/Api/BatteryApi.cs
+++ b/web/Dpp/Api/BatteryApi.cs
@@ -116,6 +116,21 @@ public sealed class BatteryApi(
             }
         }
 
+        // A serial number names the battery whose passport isn't inactive: its new linked passport (step 4c-1, decision 34).
+        if (known.Count > 1)
+        {
+            var current = new List<BatteryPack>();
+            foreach (var pack in known)
+            {
+                if (await store.PassportStatusAsync(pack.Id, cancellationToken) != DppStatuses.Inactive)
+                {
+                    current.Add(pack);
+                }
+            }
+
+            known = current;
+        }
+
         var mine = known.Where(pack => MayChange(sender, pack)).ToArray();
         return mine.Length switch
         {
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index ffe86b0..4da3924 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -33,6 +33,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<SavePipeline>();
         services.AddSingleton<PackCreationService>();
         services.AddSingleton<StatusChangeService>();
+        services.AddSingleton<SuccessionService>();
         services.AddSingleton<PassportSigner>();
         services.AddSingleton<PublicationChecks>();
         services.AddSingleton<PublicationService>();
diff --git a/web/Dpp/Editing/PackCreationService.cs b/web/Dpp/Editing/PackCreationService.cs
index 3540d31..ef0582d 100644
--- a/web/Dpp/Editing/PackCreationService.cs
+++ b/web/Dpp/Editing/PackCreationService.cs
@@ -13,7 +13,8 @@ public sealed record NewPack(
     string OperatorId,
     string? ClusterId,
     IReadOnlyDictionary<string, JsonNode?> Values,
-    string Source = "form");
+    string Source = "form",
+    string? PredecessorPackId = null);
 
 /// <summary>The created pack, or the problem that stopped it; <see cref="Keys"/> says what happened to each value.</summary>
 public sealed record PackCreationResult(BatteryPack? Pack, IReadOnlyList<KeyResult> Keys, string? Problem);
@@ -68,8 +69,11 @@ public sealed class PackCreationService(
             return new PackCreationResult(null, evaluation.Results, "the battery serial number (#11) is required");
         }
 
-        // Linked successor packs (step 4) will be the one exception (design 8.2).
-        if (await store.SerialNumberTakenAsync(evaluation.Accepted["batterySerialNumber"].GetValue<string>(), cancellationToken: cancellationToken))
+        // A battery's new linked passport keeps its serial number (design 7.5 and 8.2): the old pack may share it.
+        if (await store.SerialNumberTakenAsync(
+            evaluation.Accepted["batterySerialNumber"].GetValue<string>(),
+            request.PredecessorPackId is null ? [] : [request.PredecessorPackId],
+            cancellationToken))
         {
             var keys = evaluation.Results
                 .Select(result => result.Key == "batterySerialNumber" ? result with { Outcome = KeyOutcome.Rejected, Message = SavePipeline.SerialNumberTaken } : result)
@@ -84,6 +88,7 @@ public sealed class PackCreationService(
             BatchId = request.BatchId,
             OperatorId = request.OperatorId,
             ClusterId = request.ClusterId,
+            SuccessorOf = request.PredecessorPackId,
         };
         pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(identifiers.NewPackIdentifier()), now, "system", sender.Who);
         pack.Values["batteryStatus"] = Stored(new JsonObject { ["batteryStatusValues"] = "original" }, now, "system", sender.Who);
diff --git a/web/Dpp/Editing/SavePipeline.cs b/web/Dpp/Editing/SavePipeline.cs
index 0bcf3f9..6d8affb 100644
--- a/web/Dpp/Editing/SavePipeline.cs
+++ b/web/Dpp/Editing/SavePipeline.cs
@@ -95,6 +95,15 @@ public sealed class SavePipeline(
             return new SaveResult(false, false, []);
         }
 
+        // An inactive, invalid or marked-for-deletion passport takes no more changes (step 4c, decision 34).
+        if (changes.Target == TargetKind.Pack && await store.PassportStatusAsync(changes.TargetId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
+        {
+            var frozen = changes.Values.Keys.Concat(changes.Removals ?? []).Distinct(StringComparer.Ordinal)
+                .Select(key => new KeyResult(key, KeyOutcome.Rejected, NoLongerChanges(status)))
+                .ToArray();
+            return new SaveResult(true, false, frozen);
+        }
+
         var evaluation = EvaluateCore(sender, changes.Target, target.ClusterId, changes.Values, statusChange);
         if (changes.Target == TargetKind.Pack)
         {
@@ -193,6 +202,9 @@ public sealed class SavePipeline(
         return results;
     }
 
+    /// <summary>Why a value of a passport that is no longer active is refused.</summary>
+    public static string NoLongerChanges(string status) => $"the passport is {status}; nothing on it changes";
+
     /// <summary>Why a pack's fixed value (#11, #17, #19) is refused after publication.</summary>
     public const string FixedOncePublished = "fixed once the passport is published";
 
@@ -234,7 +246,7 @@ public sealed class SavePipeline(
             else if (attribute.Number == 11
                 && accepted[result.Key] is JsonValue serial
                 && serial.TryGetValue<string>(out var text)
-                && await store.SerialNumberTakenAsync(text, packId, cancellationToken))
+                && await store.SerialNumberTakenAsync(text, await LinkedPacksAsync(packId, cancellationToken), cancellationToken))
             {
                 refusal = SerialNumberTaken;
             }
@@ -249,6 +261,13 @@ public sealed class SavePipeline(
         return new Evaluation(results, accepted);
     }
 
+    /// <summary>The pack and the passports linked to it (its predecessor and successor), which share its serial number.</summary>
+    private async Task<IReadOnlyCollection<string>> LinkedPacksAsync(string packId, CancellationToken cancellationToken)
+    {
+        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
+        return new[] { packId, pack?.SuccessorOf, pack?.Succession?.SuccessorPackId }.OfType<string>().ToArray();
+    }
+
     private async Task<bool> UsedByPublishedPassportAsync(TargetKind target, string id, CancellationToken cancellationToken)
     {
         var packIds = target switch
diff --git a/web/Dpp/Editing/StatusChangeService.cs b/web/Dpp/Editing/StatusChangeService.cs
index 8c1b8f3..a243fd6 100644
--- a/web/Dpp/Editing/StatusChangeService.cs
+++ b/web/Dpp/Editing/StatusChangeService.cs
@@ -22,6 +22,15 @@ public enum StatusEvent
     /// version is published. Cluster admins ask the team.
     /// </summary>
     Corrected,
+
+    /// <summary>Prepared for re-use and placed on the market again (design 7.5): "re-used", and a new linked passport follows.</summary>
+    PlacedOnMarketAgain,
+
+    /// <summary>Remanufactured (design 7.5): a new linked passport follows.</summary>
+    Remanufactured,
+
+    /// <summary>Repurposed, for a new use (design 7.5): a new linked passport follows.</summary>
+    Repurposed,
 }
 
 /// <summary>One status change: what happened, the re-stated values, and for a repair its day (yyyy-MM-dd) and what was repaired.</summary>
@@ -58,6 +67,13 @@ public sealed class StatusChangeService(
     public const string RepairNotInTheFuture = "the day of the repair can't be in the future";
     public const string RepairNeedsDescription = "say what was repaired";
     public const string OnlyTheTeamCorrects = "only the team corrects the values; ask the team";
+    public const string WaitingForSuccessor = "the battery is waiting for its new passport; the team can cancel it";
+
+    /// <summary>Why a passport that is no longer active changes nothing (step 4c).</summary>
+    public static string NoLongerActive(string status) => SavePipeline.NoLongerChanges(status);
+
+    /// <summary>What happened that places the battery on the market again, with a new linked passport (design 7.5).</summary>
+    public static bool LeadsToNewPassport(StatusEvent what) => what is StatusEvent.PlacedOnMarketAgain or StatusEvent.Remanufactured or StatusEvent.Repurposed;
     public const string NoteNeedsText = "write the note first";
     public const string NoBattery = "the battery was not found";
     public const string NoteAddedEvent = "dpp.history-note-added";
@@ -102,6 +118,9 @@ public sealed class StatusChangeService(
         {
             StatusEvent.Repaired => current == "original" ? "re-used" : current,
             StatusEvent.Waste => "waste",
+            StatusEvent.PlacedOnMarketAgain => "re-used",
+            StatusEvent.Remanufactured => "remanufactured",
+            StatusEvent.Repurposed => "repurposed",
             _ => null,
         };
     }
@@ -139,7 +158,12 @@ public sealed class StatusChangeService(
 
         if (await store.GetActiveVersionAsync(packId, cancellationToken) is null)
         {
-            return Refused(OnlyPublished);
+            return Refused(await store.PassportStatusAsync(packId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status) ? NoLongerActive(status) : OnlyPublished);
+        }
+
+        if (pack.Succession is { LinkedAtUtc: null } && change.What != StatusEvent.Corrected)
+        {
+            return Refused(WaitingForSuccessor);
         }
 
         var from = CurrentStatus(pack) ?? "original";
@@ -209,6 +233,11 @@ public sealed class StatusChangeService(
             await store.AddRepairAsync(packId, repair, cancellationToken);
         }
 
+        if (LeadsToNewPassport(change.What))
+        {
+            await store.SetSuccessionAsync(packId, new Succession { StatusBefore = from, Status = to, StartedAtUtc = clock.GetUtcNow().UtcDateTime }, cancellationToken);
+        }
+
         var version = await store.GetActiveVersionAsync(packId, cancellationToken);
         var serial = pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? packId;
         var metadata = new BsonDocument
@@ -266,7 +295,7 @@ public sealed class StatusChangeService(
 
         if (await store.GetActiveVersionAsync(packId, cancellationToken) is null)
         {
-            return OnlyPublished;
+            return await store.PassportStatusAsync(packId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status) ? NoLongerActive(status) : OnlyPublished;
         }
 
         var note = text?.Trim() ?? string.Empty;
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index 4646cf5..3c1b0a1 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -159,7 +159,8 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
 
         if (await store.FindPackByUniqueProductIdentifierAsync(text, cancellationToken) is { } pack)
         {
-            return await store.GetActiveVersionAsync(pack.Id, cancellationToken);
+            // A passport that is no longer active stays readable by its battery ID (step 4c).
+            return await store.GetActiveVersionAsync(pack.Id, cancellationToken) ?? await store.GetLatestVersionAsync(pack.Id, cancellationToken);
         }
 
         var published = new List<PassportVersion>();
diff --git a/web/Dpp/Publication/PublicationService.cs b/web/Dpp/Publication/PublicationService.cs
index a43783c..bb58123 100644
--- a/web/Dpp/Publication/PublicationService.cs
+++ b/web/Dpp/Publication/PublicationService.cs
@@ -29,6 +29,10 @@ public sealed class PublicationService(
 {
     public const string PublishedEvent = "dpp.published";
     public const string BlockedEvent = "dpp.publish-blocked";
+    public const string InactiveEvent = "dpp.passport-inactive";
+
+    /// <summary>Why a passport that is no longer active gets no new version.</summary>
+    public static string NoNewVersion(string status) => $"the passport is {status}, so it gets no new version";
 
     /// <summary>
     /// The checks the pack's passport would meet if it were published now, without publishing anything (the battery
@@ -46,12 +50,53 @@ public sealed class PublicationService(
         return checks.Check(PassportAssembler.Assemble(sources, header));
     }
 
-    public Task<PublicationResult> PublishAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
+    public async Task<PublicationResult> PublishAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(sender);
-        return sender.Kind == SenderKind.Team
-            ? PublishCoreAsync(packId, sender, retryOnConflict: true, cancellationToken)
-            : Task.FromResult(new PublicationResult(null, null, "only the team publishes passports"));
+        if (sender.Kind != SenderKind.Team)
+        {
+            return new PublicationResult(null, null, "only the team publishes passports");
+        }
+
+        // A passport with no active version is either a draft or no longer active (step 4c): only a draft is published.
+        if (await store.PassportStatusAsync(packId, cancellationToken) is { } status && DppStatuses.IsNoLongerActive(status))
+        {
+            return new PublicationResult(null, null, NoNewVersion(status));
+        }
+
+        return await PublishCoreAsync(packId, sender, retryOnConflict: true, cancellationToken);
+    }
+
+    /// <summary>
+    /// Makes a passport inactive once its new linked passport is published or linked (design 7.5, J-13): its active
+    /// version's DPP status becomes "inactive" and #4 moves to now. No new version: #2 isn't signed (4.3).
+    /// </summary>
+    public async Task InactivateAsync(ChangeSender sender, string packId, string why, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(sender);
+        if (await store.GetActiveVersionAsync(packId, cancellationToken) is not { } active)
+        {
+            return;
+        }
+
+        var now = clock.GetUtcNow().UtcDateTime;
+        await store.SetVersionStatusAsync(active.Id, DppStatuses.Inactive, now, cancellationToken);
+        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is { Succession: { } succession })
+        {
+            succession.LinkedAtUtc = now;
+            await store.SetSuccessionAsync(packId, succession, cancellationToken);
+        }
+
+        await audit.AppendApplicationAuditEventAsync(
+            InactiveEvent,
+            sender.Who,
+            sender.Kind.ToString(),
+            "publication",
+            $"The passport of pack {packId} is inactive: {why}",
+            new BsonDocument { ["version"] = active.Number, ["why"] = why },
+            entityType: "dpp-pack",
+            entityId: packId,
+            cancellationToken: cancellationToken);
     }
 
     public async Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, ChangeSender sender, CancellationToken cancellationToken)
@@ -151,6 +196,11 @@ public sealed class PublicationService(
         }
 
         await store.ArchiveOtherVersionsAsync(packId, version.Id, cancellationToken);
+        if (version.Number == 1 && sources.Pack.SuccessorOf is { } predecessor)
+        {
+            await InactivateAsync(sender, predecessor, $"its new passport {passportId} is published", cancellationToken);
+        }
+
         await audit.AppendApplicationAuditEventAsync(
             PublishedEvent,
             who,
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index e666d2a..18a18b8 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -122,6 +122,44 @@ public sealed class BatteryPack : IDppRecord
     /// (decision 34).
     /// </summary>
     public List<RepairEntry> Repairs { get; set; } = [];
+
+    /// <summary>
+    /// Placed on the market again (design 7.5, step 4c-1): what the status was, and the battery's new passport, Scania's
+    /// (a new pack) or another operator's. Null while the battery isn't waiting for a new passport. App data.
+    /// </summary>
+    public Succession? Succession { get; set; }
+
+    /// <summary>The pack whose passport this one replaces (design 7.5); null for a battery placed on the market new.</summary>
+    public string? SuccessorOf { get; set; }
+}
+
+/// <summary>A battery placed on the market again, and its new passport (design 7.5).</summary>
+public sealed class Succession
+{
+    /// <summary>The status before (to put back if the team cancels, decision 34).</summary>
+    public required string StatusBefore { get; init; }
+
+    /// <summary>The status it was placed on the market again with: re-used, remanufactured or repurposed.</summary>
+    public required string Status { get; init; }
+
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public required DateTime StartedAtUtc { get; init; }
+
+    /// <summary>Scania's new pack, once the team made it.</summary>
+    public string? SuccessorPackId { get; set; }
+
+    /// <summary>Another operator's identifier (#7), as typed.</summary>
+    public string? OtherOperatorId { get; set; }
+
+    /// <summary>Another operator's name and contact (#14), as typed.</summary>
+    public string? OtherOperatorInfo { get; set; }
+
+    /// <summary>Another operator's new passport: its ID or web address.</summary>
+    public string? OtherPassport { get; set; }
+
+    /// <summary>When the new passport was published or linked, which made this one inactive.</summary>
+    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
+    public DateTime? LinkedAtUtc { get; set; }
 }
 
 /// <summary>One repair of a pack (design 7.9): when, what was repaired, and who recorded it when.</summary>
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index 55544ec..d75b266 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -156,7 +156,14 @@ public sealed class DppStore
     /// True when another pack already has this serial number (#11), ignoring case and surrounding spaces, as the old
     /// battery pages judged it. <paramref name="exceptPackId"/> is the pack being corrected, which may keep its own.
     /// </summary>
-    public async Task<bool> SerialNumberTakenAsync(string serialNumber, string? exceptPackId = null, CancellationToken cancellationToken = default)
+    public Task<bool> SerialNumberTakenAsync(string serialNumber, string? exceptPackId = null, CancellationToken cancellationToken = default) =>
+        SerialNumberTakenAsync(serialNumber, exceptPackId is null ? [] : [exceptPackId], cancellationToken);
+
+    /// <summary>
+    /// The same, with several packs that may keep it: a battery's new passport shares its old one's serial number, as it
+    /// is the same battery (design 7.5, step 4c-1).
+    /// </summary>
+    public async Task<bool> SerialNumberTakenAsync(string serialNumber, IReadOnlyCollection<string> exceptPackIds, CancellationToken cancellationToken = default)
     {
         var wanted = serialNumber?.Trim();
         if (_database is null || string.IsNullOrEmpty(wanted))
@@ -165,7 +172,7 @@ public sealed class DppStore
         }
 
         var sameSerial = Builders<BatteryPack>.Filter.Regex(SerialNumberField, new BsonRegularExpression($@"^\s*{Regex.Escape(wanted)}\s*$", "i"));
-        var filter = exceptPackId is null ? sameSerial : sameSerial & Builders<BatteryPack>.Filter.Ne("_id", exceptPackId);
+        var filter = exceptPackIds.Count == 0 ? sameSerial : sameSerial & Builders<BatteryPack>.Filter.Nin("_id", exceptPackIds);
         return await Collection<BatteryPack>().Find(filter).Limit(1).AnyAsync(cancellationToken);
     }
 
@@ -278,6 +285,38 @@ public sealed class DppStore
             .FirstOrDefaultAsync(cancellationToken);
     }
 
+    /// <summary>The pack's newest version, whatever its status; null for a draft.</summary>
+    public async Task<PassportVersion?> GetLatestVersionAsync(string packId, CancellationToken cancellationToken = default)
+    {
+        if (_database is null || string.IsNullOrWhiteSpace(packId))
+        {
+            return null;
+        }
+
+        return await Collection<PassportVersion>().Find(version => version.PackId == packId).SortByDescending(version => version.Number).FirstOrDefaultAsync(cancellationToken);
+    }
+
+    /// <summary>The passport's DPP status (#2): its newest version's; null for a draft (step 4c).</summary>
+    public async Task<string?> PassportStatusAsync(string packId, CancellationToken cancellationToken = default) =>
+        (await GetLatestVersionAsync(packId, cancellationToken))?.DppStatus;
+
+    /// <summary>Sets a version's DPP status and #4 (the status is made available then); #2 isn't signed (4.3).</summary>
+    public Task SetVersionStatusAsync(string versionId, string status, DateTime lastUpdatedUtc, CancellationToken cancellationToken = default) =>
+        Collection<PassportVersion>().UpdateOneAsync(
+            version => version.Id == versionId,
+            Builders<PassportVersion>.Update.Set(version => version.DppStatus, status).Set(version => version.LastUpdatedUtc, lastUpdatedUtc),
+            cancellationToken: cancellationToken);
+
+    /// <summary>Sets or clears a pack's succession (design 7.5). False when the pack doesn't exist.</summary>
+    public async Task<bool> SetSuccessionAsync(string packId, Succession? succession, CancellationToken cancellationToken = default)
+    {
+        var result = await Collection<BatteryPack>().UpdateOneAsync(
+            pack => pack.Id == packId,
+            Builders<BatteryPack>.Update.Set(pack => pack.Succession, succession),
+            cancellationToken: cancellationToken);
+        return result.MatchedCount == 1;
+    }
+
     /// <summary>A version by its passport ID (#5), whatever its status.</summary>
     public async Task<PassportVersion?> GetVersionByPassportIdAsync(string passportId, CancellationToken cancellationToken = default)
     {
diff --git a/web/Dpp/Store/PassportVersion.cs b/web/Dpp/Store/PassportVersion.cs
index 3a5e030..77c9b3e 100644
--- a/web/Dpp/Store/PassportVersion.cs
+++ b/web/Dpp/Store/PassportVersion.cs
@@ -5,11 +5,34 @@ using MongoDB.Bson.Serialization.Attributes;
 
 namespace BatteryPassWeb.Dpp.Store;
 
-/// <summary>DPP status values (#2) that versioning sets; step 4 adds inactive, invalid and marked-for-deletion.</summary>
+/// <summary>DPP status values (#2), the schema's five.</summary>
 public static class DppStatuses
 {
     public const string Active = "active";
     public const string Archived = "archived";
+
+    /// <summary>Replaced by a linked new passport (design 7.5, step 4c-1).</summary>
+    public const string Inactive = "inactive";
+
+    /// <summary>Marked invalid by the team or market surveillance (design 7.7, step 4c-2).</summary>
+    public const string Invalid = "invalid";
+
+    /// <summary>After recycling or export (design 7.6, step 4c-2).</summary>
+    public const string MarkedForDeletion = "marked-for-deletion";
+
+    /// <summary>A passport no longer active (step 4c): it takes no changes and gets no new version.</summary>
+    public static bool IsNoLongerActive(string? status) => status is Inactive or Invalid or MarkedForDeletion;
+
+    /// <summary>A DPP status in words, for pills and tables: "Active", "Marked for deletion".</summary>
+    public static string Words(string status) => status switch
+    {
+        Active => "Active",
+        Archived => "Archived",
+        Inactive => "Inactive",
+        Invalid => "Invalid",
+        MarkedForDeletion => "Marked for deletion",
+        _ => status,
+    };
 }
 
 /// <summary>
```

`web/Dpp/Editing/SuccessionService.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>Another operator who places the battery on the market again: their #7 and #14, and their new passport's ID or address.</summary>
public sealed record OtherOperator(string? OperatorId, string? OperatorInfo, string? Passport);

/// <summary>
/// The battery's new linked passport (design 7.5, decisions 13, 19 and 34; step 4c-1), once a status change placed it on
/// the market again. Scania's: the team makes a new draft pack that starts from the old one, and publishing it makes the
/// old passport inactive. Another operator's: the team or the battery's cluster admins enter the operator and, once known,
/// link its new passport, which makes the old one inactive. Until then the team can cancel and put the old status back.
/// </summary>
public sealed class SuccessionService(
    DppStore store,
    PackCreationService creation,
    SavePipeline pipeline,
    PublicationService publication,
    ApplicationAuditService audit,
    TimeProvider clock)
{
    public const string SuccessorMadeEvent = "dpp.successor-made";
    public const string OtherOperatorEvent = "dpp.successor-other-operator";
    public const string CancelledEvent = "dpp.successor-cancelled";

    public const string NoBattery = "the battery was not found";
    public const string NotAwaited = "the battery isn't waiting for a new passport";
    public const string TeamMakesIt = "the team makes Scania's new passport";
    public const string TeamCancels = "only the team cancels the new passport";
    public const string NotYours = "only the team and the battery's cluster admins name the operator";
    public const string NoOperator = "choose an economic operator";
    public const string AlreadyMade = "the new passport is already made";
    public const string LinkNeedsPassport = "give the new passport's ID or web address to link it";
    public const string NeedsOperator = "give the operator's identifier or name";

    /// <summary>The pack values the new passport starts from (Q2 of 4c): serial number, service provider, manufacturing date.</summary>
    public static readonly string[] CarriedValues = ["batterySerialNumber", "thirdPartyServiceProvider", "manufacturingDate"];

    /// <summary>
    /// Makes Scania's new passport as a draft (design 7.5, J-2): a new battery ID, the chosen operator, the old battery's
    /// batch, cluster, serial number, service provider and manufacturing date, its new status and re-stated values, and
    /// its module list and its last measured values. The warranty (#19) is left for the team. Not copied: the repair history and accident reports,
    /// which stay with the old passport.
    /// </summary>
    public async Task<(string? SuccessorPackId, string? Problem)> CreateSuccessorAsync(ChangeSender sender, string packId, string? operatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.LoadSourcesAsync(packId, cancellationToken) is not { } sources)
        {
            return (null, NoBattery);
        }

        var old = sources.Pack;
        if (sender.Kind != SenderKind.Team)
        {
            return (null, TeamMakesIt);
        }

        if (old.Succession is not { LinkedAtUtc: null } succession)
        {
            return (null, NotAwaited);
        }

        if (succession.SuccessorPackId is not null || succession.OtherPassport is not null)
        {
            return (null, AlreadyMade);
        }

        if (string.IsNullOrWhiteSpace(operatorId) || await store.GetAsync<EconomicOperator>(operatorId, cancellationToken) is null)
        {
            return (null, NoOperator);
        }

        var values = CarriedValues
            .Where(old.Values.ContainsKey)
            .ToDictionary(key => key, key => old.Values[key].Value?.DeepClone(), StringComparer.Ordinal);
        var created = await creation.CreateAsync(sender, new NewPack(old.BatchId, operatorId, old.ClusterId, values, "form", PredecessorPackId: old.Id), cancellationToken);
        if (created.Pack is not { } pack)
        {
            return (null, created.Problem ?? "the new passport couldn't be made");
        }

        // The new status and the re-stated values, as the old passport's last status change stated them.
        var now = clock.GetUtcNow().UtcDateTime;
        var carried = new Dictionary<string, Values.AttributeValue>(StringComparer.Ordinal)
        {
            ["batteryStatus"] = new() { Value = new JsonObject { ["batteryStatusValues"] = succession.Status }, GeneratedAtUtc = now, Source = "system", Who = sender.Who },
        };
        foreach (var attribute in StatusChangeService.RestatedAttributes(sources.Model))
        {
            if ((old.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values.GetValueOrDefault(attribute.Property)) is { } value)
            {
                carried[attribute.Property] = value;
            }
        }

        // The battery's last measured values continue on the new passport, with their measurement times: it is the
        // same battery, and the passport needs them to be published (7.1). #92 stays the new passport's own page.
        foreach (var attribute in AttributeCatalog.All.Where(attribute => attribute.Change == ChangeRule.Live && attribute.Property != Documents.DocumentService.Accidents))
        {
            if (old.Values.GetValueOrDefault(attribute.Property) is { } value)
            {
                carried[attribute.Property] = value;
            }
        }

        await store.SetValuesAsync<BatteryPack>(pack.Id, carried, cancellationToken);
        await store.SetPackModulesAsync(pack.Id, old.Modules, cancellationToken);
        succession.SuccessorPackId = pack.Id;
        await store.SetSuccessionAsync(old.Id, succession, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            SuccessorMadeEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"New passport of battery {Serial(old)} made as a draft ({succession.Status})",
            new BsonDocument { ["successor"] = pack.Id, ["status"] = succession.Status },
            entityType: "dpp-pack",
            entityId: old.Id,
            cancellationToken: cancellationToken);
        return (pack.Id, null);
    }

    /// <summary>
    /// Another operator places the battery on the market again (design 7.5): their details are saved, and with
    /// <paramref name="link"/> their new passport's ID or address links it, which makes the old passport inactive.
    /// Null when done; otherwise why not.
    /// </summary>
    public async Task<string?> SaveOtherOperatorAsync(ChangeSender sender, string packId, OtherOperator details, bool link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(details);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } old)
        {
            return NoBattery;
        }

        var mayName = sender.Kind == SenderKind.Team
            || (sender.Kind == SenderKind.ClusterAdmin && old.ClusterId is { } cluster && sender.ClusterIds.Contains(cluster));
        if (!mayName)
        {
            return NotYours;
        }

        if (old.Succession is not { LinkedAtUtc: null } succession)
        {
            return NotAwaited;
        }

        if (succession.SuccessorPackId is not null)
        {
            return AlreadyMade;
        }

        var operatorId = Clean(details.OperatorId);
        var operatorInfo = Clean(details.OperatorInfo);
        var passport = Clean(details.Passport);
        if (operatorId is null && operatorInfo is null)
        {
            return NeedsOperator;
        }

        // The details are kept even when the link must wait for the new passport's ID, so nothing typed is lost.
        succession.OtherOperatorId = operatorId;
        succession.OtherOperatorInfo = operatorInfo;
        succession.OtherPassport = passport;
        await store.SetSuccessionAsync(old.Id, succession, cancellationToken);
        if (link && passport is null)
        {
            return LinkNeedsPassport;
        }

        await audit.AppendApplicationAuditEventAsync(
            OtherOperatorEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            link
                ? $"Battery {Serial(old)} is placed on the market again by {operatorInfo ?? operatorId}; new passport {passport}"
                : $"Battery {Serial(old)} will be placed on the market again by {operatorInfo ?? operatorId}",
            new BsonDocument
            {
                ["operatorId"] = (BsonValue?)operatorId ?? BsonNull.Value,
                ["operator"] = (BsonValue?)operatorInfo ?? BsonNull.Value,
                ["passport"] = (BsonValue?)passport ?? BsonNull.Value,
                ["linked"] = link,
            },
            entityType: "dpp-pack",
            entityId: old.Id,
            cancellationToken: cancellationToken);
        if (link)
        {
            await publication.InactivateAsync(sender, old.Id, $"another operator's new passport {passport} is linked", cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// The team cancels the new passport while the old one isn't yet inactive (decision 34): a draft new pack is
    /// deleted, the old status is put back with a new version, and the battery no longer waits. Null when done.
    /// </summary>
    public async Task<string?> CancelAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } old)
        {
            return NoBattery;
        }

        if (sender.Kind != SenderKind.Team)
        {
            return TeamCancels;
        }

        if (old.Succession is not { LinkedAtUtc: null } succession)
        {
            return NotAwaited;
        }

        if (succession.SuccessorPackId is { } successorId)
        {
            if (await store.GetLatestVersionNumberAsync(successorId, cancellationToken) > 0)
            {
                return NotAwaited; // published: the old passport is already inactive
            }

            await store.DeleteAsync<BatteryPack>(successorId, cancellationToken);
        }

        await store.SetSuccessionAsync(old.Id, null, cancellationToken);
        var saved = await pipeline.SaveStatusChangeAsync(
            sender,
            old.Id,
            new Dictionary<string, JsonNode?> { ["batteryStatus"] = new JsonObject { ["batteryStatusValues"] = succession.StatusBefore } },
            cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            CancelledEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"New passport of battery {Serial(old)} cancelled; status back to {succession.StatusBefore}",
            new BsonDocument { ["from"] = succession.Status, ["to"] = succession.StatusBefore, ["saved"] = saved.Saved },
            entityType: "dpp-pack",
            entityId: old.Id,
            cancellationToken: cancellationToken);
        return null;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Serial(BatteryPack pack) =>
        pack.Values.GetValueOrDefault("batterySerialNumber")?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : pack.Id;
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~SuccessionTests | FullyQualifiedName~StatusChangeTests | FullyQualifiedName~PublicationServiceTests"`
Expected: `Passed!  - Failed:     0, Passed:    53`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2191`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/SuccessionTests.cs web/Dpp/Api/BatteryApi.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Editing/PackCreationService.cs web/Dpp/Editing/SavePipeline.cs web/Dpp/Editing/StatusChangeService.cs web/Dpp/Editing/SuccessionService.cs web/Dpp/Pages/PassportPages.cs web/Dpp/Publication/PublicationService.cs web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs web/Dpp/Store/PassportVersion.cs
git commit -m "feat(dpp): the new linked passport, and inactive passports that take no more changes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The new passport on the pages

Mock-up S3's three new choices in the status window, the S4 block in the status card (Scania's new passport for the team, another operator for the team and the battery's cluster admins, and the team's cancel), the S5 line on the new battery's pages, and the S7 line and status pill on the passport pages. The lists, the team's battery page and the versions card name "Inactive"; an inactive battery's live values and accident cards show one line instead of their forms; the Registry lists active passports only; the APIs' `dppStatus` says "inactive".

**Files:**
- Test (modify): `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs`
- Test (create): `BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs`
- Modify: `web/Controllers/BatteriesController.cs`
- Modify: `web/Controllers/ClusterBatteriesController.cs`
- Modify: `web/Controllers/DppApiController.cs`
- Modify: `web/Controllers/PassportsApiController.cs`
- Modify: `web/Dpp/Pages/BatteryCards.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Pages/ClusterBatteryPages.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`
- Modify: `web/Dpp/Pages/RegistryList.cs`
- Modify: `web/Dpp/Pages/StatusPages.cs`
- Modify: `web/Views/Batteries/Batteries.cshtml`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/Batteries/_AccidentsCard.cshtml`
- Modify: `web/Views/Batteries/_LiveValuesCard.cshtml`
- Modify: `web/Views/Batteries/_StatusCard.cshtml`
- Modify: `web/Views/Batteries/_VersionsCard.cshtml`
- Modify: `web/Views/Passport/_PassportHero.cshtml`
- Modify: `web/Views/Passport/_PassportTabs.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `4c1-task2-tests.patch` (`BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`, `BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs`, `BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs`). Save the block as `../4c1-task2-tests.patch`, outside the worktree, and run `git apply ../4c1-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
index 624e12e..32aecf0 100644
--- a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
+++ b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
@@ -13,7 +13,7 @@ public sealed class BatteryRouteAndRegistryTests
 
         Assert.Contains("Battery ID", hero);
         Assert.Contains("Passport ID", hero);
-        Assert.Contains(">Archived<", hero);
+        Assert.Contains(">@Model.StatusWords<", hero); // archived, inactive, … (step 4c-1)
         Assert.Contains(">Active<", hero);
         Assert.Contains("Battery model", hero);
         Assert.DoesNotContain("Battery version", hero);
diff --git a/BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs b/BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs
index 26c7dec..826e945 100644
--- a/BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs
@@ -54,7 +54,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
         rig.Clock.Now = rig.Clock.Now.AddHours(1);
 
-        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));
+        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));
 
         Assert.Null(result.Problem);
         Assert.Equal(2, result.Version!.Number);
@@ -76,10 +76,10 @@ public sealed class StatusChangeTests(MongoFixture mongo)
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
         var service = Service(rig);
-        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));
+        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));
         rig.Clock.Now = rig.Clock.Now.AddDays(1);
 
-        var result = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "7", description: "Cooling plate resealed."));
+        var result = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "7", description: "Cooling plate resealed."));
 
         Assert.Null(result.Problem);
         var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
@@ -95,7 +95,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
         var service = Service(rig);
 
         var waste = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await CurrentAsync(rig)));
-        var after = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));
+        var after = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));
 
         Assert.Null(waste.Problem);
         Assert.Equal(DppStatuses.Active, waste.Version!.DppStatus); // the passport stays active (7.6)
@@ -149,7 +149,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
         var service = Service(rig);
-        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));
+        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));
         rig.Clock.Now = rig.Clock.Now.AddDays(1);
 
         var problem = await service.AddNoteAsync(NorthAdmin, rig.PackId, " The repair was on module 5, not module 4. ");
@@ -186,7 +186,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
 
-        var result = await Service(rig).ChangeAsync(NorthAdmin, rig.PackId, Repair(rig, capacityFade: "6"));
+        var result = await Service(rig).ChangeAsync(NorthAdmin, rig.PackId, await RepairAsync(rig, capacityFade: "6"));
 
         Assert.Null(result.Problem);
         Assert.Equal("north-admin@example.com", Assert.Single((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs).RecordedBy);
@@ -204,7 +204,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
 
-        var result = await Service(rig).ChangeAsync(ChangeSender.ClusterAdmin("south@example.com", BatteryTesting.South), rig.PackId, Repair(rig, capacityFade: "6"));
+        var result = await Service(rig).ChangeAsync(ChangeSender.ClusterAdmin("south@example.com", BatteryTesting.South), rig.PackId, await RepairAsync(rig, capacityFade: "6"));
 
         Assert.Equal(StatusChangeService.NotYours, result.Problem);
         await AssertUnchangedAsync(rig);
@@ -215,7 +215,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
     {
         var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
 
-        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade: "6"));
+        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));
 
         Assert.Equal(StatusChangeService.OnlyPublished, result.Problem);
         Assert.Empty((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs);
@@ -226,7 +226,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
 
-        Assert.False((await Service(rig).ChangeAsync(PublicationTesting.Team, "pack-unknown", Repair(rig, capacityFade: "6"))).Found);
+        Assert.False((await Service(rig).ChangeAsync(PublicationTesting.Team, "pack-unknown", await RepairAsync(rig, capacityFade: "6"))).Found);
     }
 
     [Theory]
@@ -238,7 +238,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
 
-        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, Repair(rig, capacityFade));
+        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade));
 
         Assert.Equal(StatusChangeService.ValuesRefused, result.Problem);
         Assert.Contains(result.Keys, key => key.Key == "capacityFade" && key.Outcome != KeyOutcome.Saved);
@@ -253,7 +253,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
     public async Task ARepair_ShouldNeedItsDateAndWhatWasRepaired(string? date, string description, string problem)
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
-        var change = Repair(rig, capacityFade: "6") with { RepairDate = date, RepairDescription = description };
+        var change = (await RepairAsync(rig, capacityFade: "6")) with { RepairDate = date, RepairDescription = description };
 
         Assert.Equal(problem, (await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, change)).Problem);
         await AssertUnchangedAsync(rig);
@@ -275,7 +275,7 @@ public sealed class StatusChangeTests(MongoFixture mongo)
         new(rig.Store, rig.Pipeline, rig.Publication, new ApplicationAuditService(rig.Context), rig.Clock);
 
     internal static BatteryPassWeb.Dpp.Pages.StatusPages Pages(PublicationRig rig) =>
-        new(rig.Store, PageTesting.Editor(rig), Service(rig), rig.Clock);
+        new(rig.Store, PageTesting.Editor(rig), Service(rig), SuccessionTests.Service(rig), rig.Clock);
 
     /// <summary>The battery's current re-stated values as the window fills them in, as text.</summary>
     internal static async Task<Dictionary<string, JsonNode?>> CurrentAsync(PublicationRig rig)
@@ -287,9 +287,10 @@ public sealed class StatusChangeTests(MongoFixture mongo)
             StringComparer.Ordinal);
     }
 
-    private static StatusChange Repair(PublicationRig rig, string capacityFade, string description = "Module 4 replaced after a coolant leak.")
+    /// <summary>A repair with the window's values and one capacity fade typed (no blocking wait: xUnit's context has few threads).</summary>
+    private static async Task<StatusChange> RepairAsync(PublicationRig rig, string capacityFade, string description = "Module 4 replaced after a coolant leak.")
     {
-        var values = CurrentAsync(rig).GetAwaiter().GetResult();
+        var values = await CurrentAsync(rig);
         values["capacityFade"] = capacityFade.Length == 0 ? null : (JsonNode?)JsonValue.Create(capacityFade);
         if (decimal.TryParse(capacityFade, out var number))
         {
diff --git a/BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs b/BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs
index c0b0197..12c72cf 100644
--- a/BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs
@@ -35,7 +35,7 @@ public sealed class StatusPagesTests(MongoFixture mongo)
         Assert.Equal([59, 69, 74, 79, 81], fields.Select(field => field.Attribute.Number));
         Assert.Equal("4", fields[0].Text(BatteryTesting.Field("capacityFade"))); // the battery's own value
         Assert.Equal("12", fields[4].Text(BatteryTesting.Field("expectedLifetimeInCalendarYears"))); // the model's, until re-stated
-        Assert.Equal([StatusEvent.Repaired, StatusEvent.Waste, StatusEvent.Corrected], card.Choices.Select(choice => choice.What));
+        Assert.Equal([StatusEvent.Repaired, StatusEvent.PlacedOnMarketAgain, StatusEvent.Remanufactured, StatusEvent.Repurposed, StatusEvent.Waste, StatusEvent.Corrected], card.Choices.Select(choice => choice.What));
         Assert.True(card.MayAddNote);
         Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}/status/note", card.NoteAction);
     }
@@ -47,7 +47,7 @@ public sealed class StatusPagesTests(MongoFixture mongo)
 
         var card = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId))!.Status!;
 
-        Assert.Equal([StatusEvent.Repaired, StatusEvent.Waste], card.Choices.Select(choice => choice.What));
+        Assert.Equal([StatusEvent.Repaired, StatusEvent.PlacedOnMarketAgain, StatusEvent.Remanufactured, StatusEvent.Repurposed, StatusEvent.Waste], card.Choices.Select(choice => choice.What));
         Assert.True(card.MayAddNote);
     }
 
```

`BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs`:

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

/// <summary>The new linked passport on the pages (mock-up S4, S5 and S7; step 4c-1).</summary>
[Collection(MongoCollection.Name)]
public sealed class SuccessionPagesTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";

    [Fact]
    public async Task TheWindow_ShouldOfferThePlacingOnTheMarketAgainChoices()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;

        Assert.Equal(
            [StatusEvent.Repaired, StatusEvent.PlacedOnMarketAgain, StatusEvent.Remanufactured, StatusEvent.Repurposed, StatusEvent.Waste, StatusEvent.Corrected],
            card.Choices.Select(choice => choice.What));
        Assert.Null(card.Succession);
    }

    [Fact]
    public async Task AWaitingBattery_ShouldAskWhoPlacesItOnTheMarketAgain()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var team = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        var admin = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North), rig.PackId))!.Status!;

        Assert.Equal("repurposed", team.Succession!.Status);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}/successor", team.Succession.Action);
        Assert.Contains(team.Succession.Operators, item => item.Id == old.OperatorId);
        Assert.Equal(old.OperatorId, team.Succession.DefaultOperatorId);
        Assert.Equal([StatusEvent.Corrected], team.Choices.Select(choice => choice.What)); // no other status change while waiting
        Assert.Empty(admin.Succession!.Operators); // the team makes Scania's
        Assert.False(admin.CanChange);
        Assert.Equal("The battery is waiting for its new passport.", admin.WhyNot);
    }

    [Fact]
    public async Task TheTeamsPage_ShouldMakeTheNewPassportAndOpenIt()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        var redirect = Assert.IsType<RedirectResult>(await controller.MakeSuccessor(rig.PackId, old.OperatorId, CancellationToken.None));

        var successorId = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;
        Assert.Equal($"{BatteryPages.BatteryHref(successorId)}#status", redirect.Url);
        Assert.StartsWith("The new passport is made as a draft.", (string)controller.TempData[BatteriesController.CardStatusKey]!, StringComparison.Ordinal);
        var successorCard = (await BatteryTesting.Pages(rig).BatteryAsync(successorId))!.Status!;
        Assert.Equal(new PredecessorView(Text(old, "batterySerialNumber")!, BatteryPages.BatteryHref(rig.PackId), Inactive: false), successorCard.Predecessor);
        var oldCard = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        Assert.True(oldCard.Succession!.SuccessorMade);
        Assert.False(oldCard.Succession.SuccessorPublished);
    }

    [Fact]
    public async Task TheClusterAdmin_ShouldLinkAnotherOperatorsPassportAndGoBackToTheRegistry()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        await new ClusterRepository(rig.Context).UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        var controller = ClusterController(rig);

        var redirect = Assert.IsType<RedirectResult>(await controller.OtherOperator(rig.PackId, "urn:test:op", "Reman AB", "https://example.com/p/1", link: true, CancellationToken.None));

        Assert.Equal("/registry", redirect.Url); // the battery's page closes: its passport is inactive
        Assert.Equal("Linked: the new passport is named, and this passport is inactive.", controller.TempData[BatteriesController.CardStatusKey]);
        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task TheTeamsPage_ShouldCancelTheNewPassport()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        Assert.IsType<RedirectResult>(await controller.CancelSuccessor(rig.PackId, CancellationToken.None));

        Assert.Equal("The new passport is cancelled, and the old status is back.", controller.TempData[BatteriesController.CardStatusKey]);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession);
    }

    [Fact]
    public async Task ThePassportPages_ShouldLinkTheOldAndTheNewPassport()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var successor = (await rig.Store.GetAsync<BatteryPack>(old.Succession!.SuccessorPackId!))!;
        var oldId = Text(old, "uniqueProductIdentifier")!;
        var newId = Text(successor, "uniqueProductIdentifier")!;
        var pages = PassportPagesTests.Pages(rig);

        var oldSummary = (await pages.SummaryAsync(Reader.Anonymous, oldId))!;
        var newDetail = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), newId))!;

        Assert.Equal("Inactive", oldSummary.StatusWords);
        Assert.Equal(new PassportNotice("Inactive", "This battery was repurposed and has a new passport.", PassportPages.SummaryHref(newId), "Open the new passport"), oldSummary.Notice);
        Assert.Equal(new PassportNotice("Active", "This passport replaces an earlier one of the same battery.", PassportPages.DetailHref(oldId), "Open the earlier passport"), newDetail.Notice);
    }

    [Fact]
    public async Task AnotherOperatorsPassport_ShouldBeNamedOnTheOldPassport()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        await SuccessionTests.Service(rig).SaveOtherOperatorAsync(PublicationTesting.Team, rig.PackId, new OtherOperator(null, "Example Remanufacturing AB", "https://example.com/p/1"), link: true);
        var oldId = Text((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!, "uniqueProductIdentifier")!;

        var summary = (await PassportPagesTests.Pages(rig).SummaryAsync(Reader.Anonymous, oldId))!;

        Assert.Equal(new PassportNotice("Inactive", "This battery was repurposed by Example Remanufacturing AB, which issued its new passport.", "https://example.com/p/1", "https://example.com/p/1"), summary.Notice);
    }

    [Fact]
    public async Task TheListsAndTheRegistry_ShouldTellAnInactivePassportFromADraft()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();

        var rows = (await BatteryTesting.Pages(rig).ListAsync(null)).Rows;
        var registry = RegistryList.Build(Reader.With("team", ReaderRole.Team), await BatteryTesting.Pages(rig).ListAsync(null));

        var old = rows.Single(row => row.PackId == rig.PackId);
        Assert.False(old.IsDraft);
        Assert.Equal(DppStatuses.Inactive, old.StatusName);
        Assert.Null(old.Active);
        Assert.DoesNotContain(registry.Rows, row => row.PackId == rig.PackId); // it continues on its new passport
        Assert.Single(registry.Rows);
        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        Assert.True(page.Publication.NoLongerActive);
        Assert.True(page.Cards.Frozen); // no live values or accident report forms
        Assert.False(page.Status!.CanChange);
    }

    private static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static ClusterBatteriesController ClusterController(PublicationRig rig)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, NorthAdmin), new Claim(ClaimTypes.Role, "clusterAdmin")], "test")),
        };
        http.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
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

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~SuccessionPagesTests | FullyQualifiedName~StatusPagesTests | FullyQualifiedName~StatusChangeTests | FullyQualifiedName~BatteryRouteAndRegistryTests"`
Expected: the build fails with CS0246, CS1061, CS1729 (for example `error CS0246 The type or namespace name 'PassportNotice' could not be found`).

- [ ] **Step 3: Write the code**

Patch `4c1-task2-code.patch` (`web/Controllers/BatteriesController.cs`, `web/Controllers/ClusterBatteriesController.cs`, `web/Controllers/DppApiController.cs`, `web/Controllers/PassportsApiController.cs`, `web/Dpp/Pages/BatteryCards.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/ClusterBatteryPages.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Dpp/Pages/RegistryList.cs`, `web/Dpp/Pages/StatusPages.cs`, `web/Views/Batteries/Batteries.cshtml`, `web/Views/Batteries/Battery.cshtml`, `web/Views/Batteries/_AccidentsCard.cshtml`, `web/Views/Batteries/_LiveValuesCard.cshtml`, `web/Views/Batteries/_StatusCard.cshtml`, `web/Views/Batteries/_VersionsCard.cshtml`, `web/Views/Passport/_PassportHero.cshtml`, `web/Views/Passport/_PassportTabs.cshtml`). Save the block as `../4c1-task2-code.patch`, outside the worktree, and run `git apply ../4c1-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/BatteriesController.cs b/web/Controllers/BatteriesController.cs
index 6379fad..73676e1 100644
--- a/web/Controllers/BatteriesController.cs
+++ b/web/Controllers/BatteriesController.cs
@@ -180,6 +180,36 @@ public sealed class BatteriesController(BatteryPages pages, DocumentService docu
         return Redirect($"{BatteryPages.BatteryHref(packId)}#status");
     }
 
+    /// <summary>Scania places the battery on the market again (design 7.5): the team makes its new passport as a draft.</summary>
+    [HttpPost("{packId}/successor")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> MakeSuccessor(string packId, [FromForm] string? operatorId, CancellationToken cancellationToken)
+    {
+        var (done, problem, successorId) = await status.MakeSuccessorAsync(ChangeSender.Team(Who), packId, operatorId, cancellationToken);
+        Card("status", done, problem);
+        return Redirect($"{BatteryPages.BatteryHref(successorId ?? packId)}#status");
+    }
+
+    /// <summary>Another operator places it on the market again (design 7.5): their details, or the link to their passport.</summary>
+    [HttpPost("{packId}/successor/other")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> OtherOperator(string packId, [FromForm] string? operatorIdentifier, [FromForm] string? operatorInfo, [FromForm] string? passport, [FromForm] bool link, CancellationToken cancellationToken)
+    {
+        var (done, problem) = await status.OtherOperatorAsync(ChangeSender.Team(Who), packId, new OtherOperator(operatorIdentifier, operatorInfo, passport), link, cancellationToken);
+        Card("status", done, problem);
+        return Redirect($"{BatteryPages.BatteryHref(packId)}#status");
+    }
+
+    /// <summary>The team cancels the new passport while the old one isn't inactive (decision 34).</summary>
+    [HttpPost("{packId}/successor/cancel")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> CancelSuccessor(string packId, CancellationToken cancellationToken)
+    {
+        var (done, problem) = await status.CancelSuccessorAsync(ChangeSender.Team(Who), packId, cancellationToken);
+        Card("status", done, problem);
+        return Redirect($"{BatteryPages.BatteryHref(packId)}#status");
+    }
+
     [HttpPost("{packId}/remove")]
     [ValidateAntiForgeryToken]
     public async Task<IActionResult> Remove(string packId, CancellationToken cancellationToken)
diff --git a/web/Controllers/ClusterBatteriesController.cs b/web/Controllers/ClusterBatteriesController.cs
index a256366..3eeee58 100644
--- a/web/Controllers/ClusterBatteriesController.cs
+++ b/web/Controllers/ClusterBatteriesController.cs
@@ -138,6 +138,21 @@ public sealed class ClusterBatteriesController(ClusterBatteryPages pages, DppRea
         return Redirect($"{ClusterBatteryPages.BatteryHref(packId)}#status");
     }
 
+    /// <summary>Another operator places the battery on the market again (design 7.5), as its cluster admin.</summary>
+    [HttpPost("{packId}/successor/other")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> OtherOperator(string packId, [FromForm] string? operatorIdentifier, [FromForm] string? operatorInfo, [FromForm] string? passport, [FromForm] bool link, CancellationToken cancellationToken)
+    {
+        if (await ClusterAdminAsync(cancellationToken) is not { } sender
+            || await pages.OtherOperatorAsync(sender, packId, new OtherOperator(operatorIdentifier, operatorInfo, passport), link, cancellationToken) is not { } message)
+        {
+            return NotFound();
+        }
+
+        Card("status", message.Done, message.Problem);
+        return Redirect(link && message.Problem is null ? "/registry" : $"{ClusterBatteryPages.BatteryHref(packId)}#status");
+    }
+
     /// <summary>The signed-in user as a cluster admin; null for the team (it has its own page) and for anyone else.</summary>
     private async Task<ChangeSender?> ClusterAdminAsync(CancellationToken cancellationToken) =>
         await readers.EditorForAsync(User, cancellationToken) is { Kind: SenderKind.ClusterAdmin } sender ? sender : null;
diff --git a/web/Controllers/DppApiController.cs b/web/Controllers/DppApiController.cs
index 28a78c0..3641ff3 100644
--- a/web/Controllers/DppApiController.cs
+++ b/web/Controllers/DppApiController.cs
@@ -88,9 +88,9 @@ public sealed class DppApiController(ExternalApiRepository tokens, ClusterReposi
                 serialNumber = row.SerialNumber,
                 batteryFamily = row.FamilyName,
                 batteryModel = row.ModelName,
-                passportId = row.Active?.DigitalProductPassportId,
-                version = row.Active?.Number,
-                dppStatus = row.Active?.DppStatus ?? "draft",
+                passportId = (row.Active ?? row.Latest)?.DigitalProductPassportId,
+                version = (row.Active ?? row.Latest)?.Number,
+                dppStatus = row.StatusName,
             }).ToArray(),
         });
     }
diff --git a/web/Controllers/PassportsApiController.cs b/web/Controllers/PassportsApiController.cs
index a7f1911..7b96ba4 100644
--- a/web/Controllers/PassportsApiController.cs
+++ b/web/Controllers/PassportsApiController.cs
@@ -34,9 +34,9 @@ public sealed class PassportsApiController(DppReaders readers, BatteryPages batt
                 batteryModel = row.ModelName,
                 batch = row.BatchLabel,
                 clusterId = row.ClusterId,
-                passportId = row.Active?.DigitalProductPassportId,
-                version = row.Active?.Number,
-                dppStatus = row.Active?.DppStatus ?? "draft",
+                passportId = (row.Active ?? row.Latest)?.DigitalProductPassportId,
+                version = (row.Active ?? row.Latest)?.Number,
+                dppStatus = row.StatusName,
             }).ToArray(),
         });
     }
diff --git a/web/Dpp/Pages/BatteryCards.cs b/web/Dpp/Pages/BatteryCards.cs
index 2f21660..7c1c14e 100644
--- a/web/Dpp/Pages/BatteryCards.cs
+++ b/web/Dpp/Pages/BatteryCards.cs
@@ -19,8 +19,12 @@ public sealed record BatteryCards(
     IReadOnlyList<string> Languages,
     bool ForTeam,
     string? ModuleDescriptionHref = null,
-    bool HasModuleDescription = false)
+    bool HasModuleDescription = false,
+    bool Frozen = false)
 {
+    /// <summary>Why the live values and accident reports take nothing more (an inactive passport, step 4c); null otherwise.</summary>
+    public string? FrozenNote => Frozen ? "The passport is no longer active: nothing on it changes any more. The battery's data continue on its new passport." : null;
+
     /// <summary>Who the team is, in the reader's words.</summary>
     public string Team => ForTeam ? "your team" : "the team";
 
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 031a02d..d2a7918 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -26,7 +26,13 @@ public sealed record BatteryRow(
     string ClusterLabel,
     PassportVersion? Active)
 {
-    public bool IsDraft => Active is null;
+    /// <summary>The newest version, whatever its status: the active one, or an inactive one (step 4c); null for a draft.</summary>
+    public PassportVersion? Latest { get; init; }
+
+    public bool IsDraft => Active is null && Latest is null;
+
+    /// <summary>The passport's DPP status (#2), or "draft".</summary>
+    public string StatusName => (Active ?? Latest)?.DppStatus ?? "draft";
 }
 
 public sealed record BatteriesPage(IReadOnlyList<BatteryRow> Rows, string Query, int Total);
@@ -57,12 +63,16 @@ public sealed record FileCheck(DppDocument Document, string Owner, bool Unchange
 /// <summary>Where the battery's passport stands: published or not, what blocks it, what is needed later, and its files.</summary>
 public sealed record PublicationView(
     PassportVersion? Active,
+    PassportVersion? Latest,
     IReadOnlyList<MissingGroup> Missing,
     IReadOnlyList<string> Problems,
     IReadOnlyList<AttributeDefinition> NeededLater,
     IReadOnlyList<FileCheck> Files)
 {
-    public bool IsPublished => Active is not null;
+    public bool IsPublished => Latest is not null;
+
+    /// <summary>Published once and no longer active: inactive, invalid or marked for deletion (step 4c).</summary>
+    public bool NoLongerActive => Active is null && Latest is not null;
 
     public bool Ready => Missing.Count == 0 && Problems.Count == 0;
 
@@ -103,7 +113,8 @@ public sealed record BatteryPage(
         Values.Languages,
         ForTeam: true,
         $"{ProductDataPages.FamilyHref(Sources.Model.FamilyId, Sources.Model.Id)}#modules",
-        !string.IsNullOrWhiteSpace(Sources.Model.ModuleDescription));
+        !string.IsNullOrWhiteSpace(Sources.Model.ModuleDescription),
+        Frozen: Publication.NoLongerActive);
 }
 
 /// <summary>
@@ -159,7 +170,10 @@ public sealed class BatteryPages(
                 batch is null ? "Unknown batch" : $"{batch.Year} · {site?.Name ?? batch.SiteId}",
                 pack.ClusterId,
                 ClusterLabel(pack.ClusterId, clusterNames),
-                active));
+                active)
+            {
+                Latest = versions[pack.Id].MaxBy(version => version.Number),
+            });
         }
 
         var wanted = query?.Trim() ?? string.Empty;
@@ -524,7 +538,7 @@ public sealed class BatteryPages(
             .Select(group => Group(group.Key, sources, group.OrderBy(attribute => attribute.Number).ToArray()))
             .ToArray();
         var later = report.Warnings.Select(Numbered).OfType<AttributeDefinition>().OrderBy(attribute => attribute.Number).ToArray();
-        return new PublicationView(active, groups, problems, later, await FilesAsync(sources, cancellationToken));
+        return new PublicationView(active, await store.GetLatestVersionAsync(sources.Pack.Id, cancellationToken), groups, problems, later, await FilesAsync(sources, cancellationToken));
     }
 
     /// <summary>The attribute a report line such as "#21 Battery mass is missing" is about.</summary>
diff --git a/web/Dpp/Pages/ClusterBatteryPages.cs b/web/Dpp/Pages/ClusterBatteryPages.cs
index 99a9025..220481a 100644
--- a/web/Dpp/Pages/ClusterBatteryPages.cs
+++ b/web/Dpp/Pages/ClusterBatteryPages.cs
@@ -110,6 +110,12 @@ public sealed class ClusterBatteryPages(
             ? await status.AddNoteAsync(sender, pack.Id, note, cancellationToken)
             : null;
 
+    /// <summary>Another operator's details or link, as the cluster admin (design 7.5); null when the sender may not manage the battery.</summary>
+    public async Task<(string? Done, string? Problem)?> OtherOperatorAsync(ChangeSender sender, string packId, OtherOperator details, bool link, CancellationToken cancellationToken = default) =>
+        await ManagedAsync(sender, packId, cancellationToken) is { } pack
+            ? await status.OtherOperatorAsync(sender, pack.Id, details, link, cancellationToken)
+            : null;
+
     /// <summary>What the status card says after the window was sent (the team page's words).</summary>
     public (string? Done, string? Problem) StatusMessage(StatusChangeResult result, int versionBefore) => status.Message(result, versionBefore);
 
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index 3c1b0a1..e7243f5 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -70,8 +70,20 @@ public sealed record PassportPage(
     /// null otherwise. Not part of the signed passport.
     /// </summary>
     public IReadOnlyList<RepairEntry>? Repairs { get; init; }
+
+    /// <summary>The line about an inactive passport or one that replaces an earlier passport (S7); null otherwise.</summary>
+    public PassportNotice? Notice { get; init; }
+
+    /// <summary>This version's DPP status (#2) in words, for the hero's pill.</summary>
+    public string StatusWords => DppStatuses.Words(Version.DppStatus);
 }
 
+/// <summary>
+/// One line at the top of both passport pages when the passport isn't simply active, or replaces an earlier one (mock-up
+/// S7, step 4c): what it says, and where it leads (a passport page, or another operator's address).
+/// </summary>
+public sealed record PassportNotice(string Pill, string Text, string? Href, string? LinkText);
+
 /// <summary>Where a published passport is: its version, and the battery's ID (#6) and serial number (#11).</summary>
 public sealed record PassportLocation(PassportVersion Version, string Identifier, string SerialNumber)
 {
@@ -250,9 +262,48 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
             ModuleDescription = sources.Model.ModuleDescription,
             Modules = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Modules : null,
             Repairs = detail && AudienceRules.MaySeePurchaserData(who, clusters) ? sources.Pack.Repairs.AsEnumerable().Reverse().ToArray() : null,
+            Notice = await NoticeAsync(version, sources.Pack, detail, cancellationToken),
         };
     }
 
+    /// <summary>
+    /// The S7 line: an inactive passport leads to the battery's new one (Scania's page, or another operator's address);
+    /// a passport that replaces an earlier one leads back to it (design 7.5, step 4c-1). Null for any other passport.
+    /// </summary>
+    private async Task<PassportNotice?> NoticeAsync(PassportVersion version, BatteryPack pack, bool detail, CancellationToken cancellationToken)
+    {
+        string Href(string identifier) => detail ? DetailHref(identifier) : SummaryHref(identifier);
+        if (version.DppStatus == DppStatuses.Inactive && pack.Succession is { } succession)
+        {
+            if (succession.SuccessorPackId is { } successorId && await store.GetAsync<BatteryPack>(successorId, cancellationToken) is { } successor
+                && PackText(successor, "uniqueProductIdentifier") is { } newIdentifier)
+            {
+                return new PassportNotice("Inactive", $"This battery was {succession.Status} and has a new passport.", Href(newIdentifier), "Open the new passport");
+            }
+
+            if (succession.OtherPassport is { } other)
+            {
+                var link = Uri.TryCreate(other, UriKind.Absolute, out var address) && address.Scheme is "https" or "http" ? other : null;
+                return new PassportNotice(
+                    "Inactive",
+                    $"This battery was {succession.Status} by {succession.OtherOperatorInfo ?? succession.OtherOperatorId}, which issued its new passport{(link is null ? $": {other}" : string.Empty)}.",
+                    link,
+                    link);
+            }
+
+            return new PassportNotice("Inactive", $"This battery was {succession.Status} and has a new passport.", null, null);
+        }
+
+        if (version.DppStatus == DppStatuses.Active && pack.SuccessorOf is { } predecessorId
+            && await store.GetAsync<BatteryPack>(predecessorId, cancellationToken) is { } predecessor
+            && PackText(predecessor, "uniqueProductIdentifier") is { } oldIdentifier)
+        {
+            return new PassportNotice("Active", "This passport replaces an earlier one of the same battery.", Href(oldIdentifier), "Open the earlier passport");
+        }
+
+        return null;
+    }
+
     /// <summary>The attributes' values in the cut passport, in catalogue order; the detail page adds each value's origin.</summary>
     private IReadOnlyList<ShownValue> Shown(JsonObject passport, PassportSources sources, bool detail, IEnumerable<AttributeDefinition> attributes)
     {
diff --git a/web/Dpp/Pages/RegistryList.cs b/web/Dpp/Pages/RegistryList.cs
index f83b722..a986ea1 100644
--- a/web/Dpp/Pages/RegistryList.cs
+++ b/web/Dpp/Pages/RegistryList.cs
@@ -34,7 +34,8 @@ public static class RegistryList
     {
         ArgumentNullException.ThrowIfNull(reader);
         ArgumentNullException.ThrowIfNull(row);
-        return !row.IsDraft
+        // Only an active passport is listed: an inactive battery continues on its new passport (step 4c).
+        return row.Active is not null
             && (EveryPublishedBattery.Any(reader.Roles.Contains) || (row.ClusterId is { } cluster && reader.ClusterIds.Contains(cluster)));
     }
 
diff --git a/web/Dpp/Pages/StatusPages.cs b/web/Dpp/Pages/StatusPages.cs
index f812349..c764f9d 100644
--- a/web/Dpp/Pages/StatusPages.cs
+++ b/web/Dpp/Pages/StatusPages.cs
@@ -8,6 +8,31 @@ using BatteryPassWeb.Dpp.Values;
 
 namespace BatteryPassWeb.Dpp.Pages;
 
+/// <summary>
+/// "Who places it on the market again?" (mock-up S4, step 4c-1): the battery waits for its new passport, or has it.
+/// </summary>
+/// <param name="Action">Where the card's forms post: the battery page plus "/successor".</param>
+/// <param name="SuccessorHref">The new pack's page for this reader (the team's battery page, or the detail page); null until made.</param>
+/// <param name="Operators">The economic operators the team picks from for Scania's new passport; empty for a cluster admin.</param>
+public sealed record SuccessionView(
+    string Status,
+    string Action,
+    bool Linked,
+    string? SuccessorHref,
+    bool SuccessorPublished,
+    string? OtherOperatorId,
+    string? OtherOperatorInfo,
+    string? OtherPassport,
+    IReadOnlyList<(string Id, string Name)> Operators,
+    string DefaultOperatorId)
+{
+    /// <summary>Scania's new pack has been made (a draft until the team publishes it).</summary>
+    public bool SuccessorMade => SuccessorHref is not null;
+}
+
+/// <summary>The S5 line on a new passport's battery pages: the battery it replaces, and whether that one is inactive yet.</summary>
+public sealed record PredecessorView(string SerialNumber, string Href, bool Inactive);
+
 /// <summary>
 /// The battery status card both battery pages show (mock-up S2 and S3, step 4b): the status and since when, the repair
 /// history newest first, and the "Change status" window with the re-stated values filled in.
@@ -25,7 +50,9 @@ public sealed record StatusCardView(
     decimal? RatedCapacityAh,
     string Today,
     bool ForTeam,
-    bool MayAddNote = false)
+    bool MayAddNote = false,
+    SuccessionView? Succession = null,
+    PredecessorView? Predecessor = null)
 {
     /// <summary>Where "Add a note to the history" posts (decision 34).</summary>
     public string NoteAction => $"{Restated.SaveAction}/note";
@@ -39,11 +66,14 @@ public sealed record StatusCardView(
         get
         {
             var choices = new List<(StatusEvent, string, string)>();
-            if (Status != "waste")
+            if (Status != "waste" && Succession is null)
             {
                 choices.Add((StatusEvent.Repaired, "Repaired", Status == "original"
                     ? "Status becomes re-used; the repair joins its history. Same passport."
                     : $"Status stays {Status}; the repair joins its history. Same passport."));
+                choices.Add((StatusEvent.PlacedOnMarketAgain, "Prepared for re-use and placed on the market again", "Status re-used. A new, linked passport follows."));
+                choices.Add((StatusEvent.Remanufactured, "Remanufactured", "A new, linked passport follows."));
+                choices.Add((StatusEvent.Repurposed, "Repurposed", "For a new use. A new, linked passport follows."));
                 choices.Add((StatusEvent.Waste, "Waste", "Final. The passport stays active until the team marks it for deletion after recycling."));
             }
 
@@ -61,7 +91,7 @@ public sealed record StatusCardView(
 /// Builds the status card and reads the status window for both battery pages (step 4b). The change itself is
 /// <see cref="StatusChangeService"/>'s; the window's fields are the editors' own, so values are read and checked alike.
 /// </summary>
-public sealed class StatusPages(DppStore store, RecordEditor editor, StatusChangeService changes, TimeProvider clock)
+public sealed class StatusPages(DppStore store, RecordEditor editor, StatusChangeService changes, SuccessionService succession, TimeProvider clock)
 {
     /// <summary>The card of a battery; <paramref name="typed"/> is the window as typed after a refused change.</summary>
     /// <param name="href">The battery page's address; the window posts to it plus "/status".</param>
@@ -87,13 +117,17 @@ public sealed class StatusPages(DppStore store, RecordEditor editor, StatusChang
             ForTeam = forTeam,
         };
         var status = StatusChangeService.CurrentStatus(pack) ?? "original";
-        var whyNot = !active ? "The status changes once the passport is published."
+        var passportStatus = await store.PassportStatusAsync(pack.Id, cancellationToken);
+        var successionView = await SuccessionAsync(pack, href, forTeam, cancellationToken);
+        var whyNot = DppStatuses.IsNoLongerActive(passportStatus) ? $"The passport is {passportStatus}: nothing on it changes any more."
+            : !active ? "The status changes once the passport is published."
             : status == "waste" && !forTeam ? "The battery is waste, which is final."
+            : successionView is not null && !forTeam ? "The battery is waiting for its new passport."
             : null;
         return new StatusCardView(
             status,
             pack.Values.GetValueOrDefault("batteryStatus")?.GeneratedAtUtc,
-            pack.Repairs.Count > 0 && status != "waste",
+            pack.Repairs.Count > 0 && status != "waste" && pack.Succession is null,
             pack.Repairs.AsEnumerable().Reverse().ToArray(),
             view,
             whyNot is null,
@@ -101,9 +135,78 @@ public sealed class StatusPages(DppStore store, RecordEditor editor, StatusChang
             ValueWords.Decimal(sources.Model.Values.GetValueOrDefault("ratedCapacity")?.Value?["amperehourMiliamperehourValue"]),
             DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
             forTeam,
-            MayAddNote: active);
+            MayAddNote: active,
+            Succession: successionView,
+            Predecessor: await PredecessorAsync(pack, forTeam, cancellationToken));
     }
 
+    private async Task<SuccessionView?> SuccessionAsync(BatteryPack pack, string href, bool forTeam, CancellationToken cancellationToken)
+    {
+        if (pack.Succession is not { } waiting)
+        {
+            return null;
+        }
+
+        string? successorHref = null;
+        var published = false;
+        if (waiting.SuccessorPackId is { } successorId && await store.GetAsync<BatteryPack>(successorId, cancellationToken) is { } successor)
+        {
+            published = await store.GetLatestVersionNumberAsync(successorId, cancellationToken) > 0;
+            successorHref = forTeam
+                ? BatteryPages.BatteryHref(successorId)
+                : published ? PassportPages.DetailHref(BatteryPages.Text(successor, "uniqueProductIdentifier") ?? successorId) : null;
+        }
+
+        var operators = forTeam
+            ? (await store.ListAsync<EconomicOperator>(cancellationToken)).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Select(item => (item.Id, item.Name)).ToArray()
+            : [];
+        return new SuccessionView(
+            waiting.Status,
+            $"{href}/successor",
+            waiting.LinkedAtUtc is not null,
+            successorHref,
+            published,
+            waiting.OtherOperatorId,
+            waiting.OtherOperatorInfo,
+            waiting.OtherPassport,
+            operators,
+            pack.OperatorId);
+    }
+
+    private async Task<PredecessorView?> PredecessorAsync(BatteryPack pack, bool forTeam, CancellationToken cancellationToken)
+    {
+        if (pack.SuccessorOf is not { } predecessorId || await store.GetAsync<BatteryPack>(predecessorId, cancellationToken) is not { } predecessor)
+        {
+            return null;
+        }
+
+        return new PredecessorView(
+            BatteryPages.Text(predecessor, "batterySerialNumber") ?? predecessorId,
+            forTeam ? BatteryPages.BatteryHref(predecessorId) : PassportPages.DetailHref(BatteryPages.Text(predecessor, "uniqueProductIdentifier") ?? predecessorId),
+            await store.PassportStatusAsync(predecessorId, cancellationToken) == DppStatuses.Inactive);
+    }
+
+    /// <summary>The team makes Scania's new passport; the message, and the new battery's page when it was made.</summary>
+    public async Task<(string? Done, string? Problem, string? SuccessorId)> MakeSuccessorAsync(ChangeSender sender, string packId, string? operatorId, CancellationToken cancellationToken = default)
+    {
+        var (successorId, problem) = await succession.CreateSuccessorAsync(sender, packId, operatorId, cancellationToken);
+        return problem is null
+            ? ("The new passport is made as a draft. Type its warranty, check it and publish it; the old passport then becomes inactive.", null, successorId)
+            : (null, $"The new passport wasn't made: {problem}.", null);
+    }
+
+    /// <summary>Another operator's details, saved or linked; the card's message.</summary>
+    public async Task<(string? Done, string? Problem)> OtherOperatorAsync(ChangeSender sender, string packId, OtherOperator details, bool link, CancellationToken cancellationToken = default) =>
+        await succession.SaveOtherOperatorAsync(sender, packId, details, link, cancellationToken) is { } problem
+            ? (null, problem == SuccessionService.LinkNeedsPassport ? $"The details are saved, but not linked: {problem}." : $"Nothing was saved: {problem}.")
+            : (link ? "Linked: the new passport is named, and this passport is inactive." : "The operator's details are saved.", null);
+
+    /// <summary>The team cancels the new passport; the card's message.</summary>
+    public async Task<(string? Done, string? Problem)> CancelSuccessorAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default) =>
+        await succession.CancelAsync(sender, packId, cancellationToken) is { } problem
+            ? (null, $"Nothing was cancelled: {problem}.")
+            : ("The new passport is cancelled, and the old status is back.", null);
+
     /// <summary>Reads the window and makes the change; null when the battery doesn't exist.</summary>
     public async Task<StatusChangeResult?> ChangeAsync(
         ChangeSender sender,
diff --git a/web/Views/Batteries/Batteries.cshtml b/web/Views/Batteries/Batteries.cshtml
index e58e265..52de502 100644
--- a/web/Views/Batteries/Batteries.cshtml
+++ b/web/Views/Batteries/Batteries.cshtml
@@ -1,4 +1,5 @@
 @using BatteryPassWeb.Dpp.Pages
+@using BatteryPassWeb.Dpp.Store
 @model BatteriesPage
 @{
     ViewData["Title"] = "Batteries";
@@ -81,6 +82,10 @@
                                     {
                                         <span class="bp-field-marker is-published">Published · version @active.Number</span>
                                     }
+                                    else if (row.Latest is { } last)
+                                    {
+                                        <span class="bp-field-marker">@DppStatuses.Words(last.DppStatus) · version @last.Number</span>
+                                    }
                                     else
                                     {
                                         <span class="bp-field-marker">Draft</span>
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 31cf458..9aec6f4 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -56,6 +56,10 @@
             {
                 <span class="bp-field-marker is-published">Published · version @current.Number</span>
             }
+            else if (publication.Latest is { } last)
+            {
+                <span class="bp-field-marker">@DppStatuses.Words(last.DppStatus) · version @last.Number</span>
+            }
             else
             {
                 <span class="bp-field-marker">Draft · only your team sees it</span>
@@ -160,6 +164,11 @@
                     <h2>Published since @When(active.PublishedAtUtc)</h2>
                     <p class="mb-0">Version @active.Number is live. A change to its values publishes a new version, and you're asked first; live values and accident reports update it in place.</p>
                 }
+                else if (publication.Latest is { } last)
+                {
+                    <h2>@DppStatuses.Words(last.DppStatus) since @When(last.LastUpdatedUtc)</h2>
+                    <p class="mb-0">Version @last.Number is its last. Nothing on this passport changes any more; it stays readable by its passport and battery IDs.</p>
+                }
                 else if (publication.Ready)
                 {
                     <h2>Ready to publish</h2>
diff --git a/web/Views/Batteries/_AccidentsCard.cshtml b/web/Views/Batteries/_AccidentsCard.cshtml
index 4f36d68..037e7f3 100644
--- a/web/Views/Batteries/_AccidentsCard.cshtml
+++ b/web/Views/Batteries/_AccidentsCard.cshtml
@@ -18,6 +18,12 @@
             }
         </ul>
     }
+    @if (Model.FrozenNote is { } frozen)
+    {
+        <p class="bp-help-text mt-2 mb-0" data-frozen>@frozen</p>
+    }
+    else
+    {
     <form method="post" action="@Model.Href/accidents" enctype="multipart/form-data" class="bp-attr-upload bp-accident-upload">
         @Html.AntiForgeryToken()
         <div class="bp-attr-file">
@@ -48,4 +54,5 @@
             <button type="submit" class="bp-secondary-button" data-upload-button>Add report</button>
         </div>
     </form>
+    }
 </section>
diff --git a/web/Views/Batteries/_LiveValuesCard.cshtml b/web/Views/Batteries/_LiveValuesCard.cshtml
index 1a8d556..3fb268e 100644
--- a/web/Views/Batteries/_LiveValuesCard.cshtml
+++ b/web/Views/Batteries/_LiveValuesCard.cshtml
@@ -18,6 +18,12 @@
         }
     </p>
     @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "live")
+    @if (Model.FrozenNote is { } frozen)
+    {
+        <p class="bp-help-text mt-2 mb-0" data-frozen>@frozen</p>
+    }
+    else
+    {
     <form method="post" action="@Model.Live.SaveAction" class="bp-live-form">
         @Html.AntiForgeryToken()
         <input type="hidden" name="@ProductDataController.StampField" value="@Model.Live.Stamp" />
@@ -39,4 +45,5 @@
             <button type="submit" class="bp-primary-button">Save live values</button>
         </div>
     </form>
+    }
 </section>
diff --git a/web/Views/Batteries/_StatusCard.cshtml b/web/Views/Batteries/_StatusCard.cshtml
index ef05849..cc1c82c 100644
--- a/web/Views/Batteries/_StatusCard.cshtml
+++ b/web/Views/Batteries/_StatusCard.cshtml
@@ -21,6 +21,72 @@
         }
     </div>
     @await Html.PartialAsync("~/Views/Batteries/_CardMessages.cshtml", "status")
+    @if (Model.Predecessor is { } earlier)
+    {
+        <p class="bp-passport-notice mt-3"><strong>New passport of an earlier battery.</strong><span>It replaces @earlier.SerialNumber, @(earlier.Inactive ? "whose passport is now inactive." : "which becomes inactive when this one is published.")</span><a href="@earlier.Href">Open the earlier battery</a></p>
+    }
+    @if (Model.Succession is { } next)
+    {
+        <div class="mt-3 pt-3 border-top" id="successor">
+            @if (next.Linked)
+            {
+                <p class="mb-0"><strong>This passport is inactive.</strong> The battery continues on its new passport@(next.OtherPassport is { } other ? $", issued by {next.OtherOperatorInfo ?? next.OtherOperatorId}: {other}" : ".")
+                    @if (next.SuccessorHref is { } newPage)
+                    {
+                        <a href="@newPage">Open the new passport</a>
+                    }
+                </p>
+            }
+            else if (next.SuccessorMade)
+            {
+                <p class="mb-2"><strong>The new passport is @(next.SuccessorPublished ? "published" : "a draft").</strong> This passport becomes inactive once it is published.
+                    <a href="@next.SuccessorHref">Open the new battery</a>
+                </p>
+            }
+            else
+            {
+                <p class="mb-2">This passport becomes inactive once the battery's new passport is published or linked. Who places it on the market again?</p>
+                @if (Model.ForTeam)
+                {
+                    <form method="post" action="@next.Action" class="bp-dialog-form mb-3">
+                        @Html.AntiForgeryToken()
+                        <label><span><strong>Scania</strong>: the team makes the new passport as a draft, with a new battery ID, the same batch and cluster, and these values to start.</span>
+                            <select name="operatorId">
+                                @foreach (var (id, name) in next.Operators)
+                                {
+                                    <option value="@id" selected="@(id == next.DefaultOperatorId)">@name</option>
+                                }
+                            </select>
+                        </label>
+                        <div class="bp-dialog-actions"><button type="submit" class="bp-primary-button">Make the new passport</button></div>
+                    </form>
+                }
+                else
+                {
+                    <p class="bp-help-text">If Scania places it on the market again, the team makes the new passport.</p>
+                }
+                <form method="post" action="@next.Action/other" class="bp-dialog-form">
+                    @Html.AntiForgeryToken()
+                    <p class="mb-1"><strong>Another operator</strong>: their details, and the new passport's ID or web address once known.</p>
+                    <label><span>Operator identifier <span class="bp-attr-number">#7</span></span><input type="text" name="operatorIdentifier" value="@next.OtherOperatorId" placeholder="e.g. urn:…" /></label>
+                    <label><span>Operator name and contact <span class="bp-attr-number">#14</span></span><input type="text" name="operatorInfo" value="@next.OtherOperatorInfo" placeholder="Name, postal address, e-mail, web address" /></label>
+                    <label><span>New passport's ID or web address (once known)</span><input type="text" name="passport" value="@next.OtherPassport" placeholder="e.g. https://…" /></label>
+                    <div class="bp-dialog-actions">
+                        <button type="submit" name="link" value="false" class="bp-secondary-button">Save details</button>
+                        <button type="submit" name="link" value="true" class="bp-primary-button">Link and set this passport inactive</button>
+                    </div>
+                </form>
+            }
+            @if (Model.ForTeam && !next.Linked && !next.SuccessorPublished)
+            {
+                <form method="post" action="@next.Action/cancel" class="mt-3">
+                    @Html.AntiForgeryToken()
+                    <button type="submit" class="bp-text-button">Cancel the new passport (it was a mistake)</button>
+                    <small class="bp-field-help d-block">The status goes back to what it was, with a new version, and a draft new battery is removed.</small>
+                </form>
+            }
+        </div>
+    }
     <h3 class="h6 mt-3 mb-0">Repair history <em class="bp-field-marker">Not passport data</em></h3>
     @if (Model.Repairs.Count == 0)
     {
diff --git a/web/Views/Batteries/_VersionsCard.cshtml b/web/Views/Batteries/_VersionsCard.cshtml
index 199bb4d..7fdc432 100644
--- a/web/Views/Batteries/_VersionsCard.cshtml
+++ b/web/Views/Batteries/_VersionsCard.cshtml
@@ -29,7 +29,7 @@
                         <tr>
                             <td data-label="Version">@row.Version.Number</td>
                             <td data-label="Passport ID"><span class="bp-id-copy-row"><a class="bp-id-value" href="/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail" target="_blank" rel="noreferrer">@row.Version.DigitalProductPassportId</a>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
-                            <td data-label="Status">@(row.Version.DppStatus == DppStatuses.Active ? "Active" : "Archived")</td>
+                            <td data-label="Status">@DppStatuses.Words(row.Version.DppStatus)</td>
                             <td data-label="Published">@ValueWords.Time(row.Version.PublishedAtUtc) <small class="bp-batteries-muted d-block">by @row.Version.PublishedBy</small></td>
                             <td data-label="Signature">
                                 @if (row.SignatureHolds)
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index 44d627e..0d7acce 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -12,6 +12,16 @@
 {
     <p class="bp-warning">Test passport: this battery's IDs start with TEST-. Real ones come once the identifier scheme is chosen.</p>
 }
+@if (Model.Notice is { } notice)
+{
+    <p class="bp-warning" data-passport-notice>
+        <strong>@notice.Pill.</strong> @notice.Text
+        @if (notice.Href is not null)
+        {
+            <a href="@notice.Href">@notice.LinkText</a>
+        }
+    </p>
+}
 <section class="bp-report-hero-card">
     <div class="bp-report-hero-layout">
         <article class="bp-report-hero-info bp-summary-identity-grid bp-report-identity-grid">
@@ -26,7 +36,7 @@
                     }
                     else
                     {
-                        <span class="bp-status-pill is-inactive">Archived</span>
+                        <span class="bp-status-pill is-inactive">@Model.StatusWords</span>
                         @if (Model.Current is not null)
                         {
                             <a href="@(detail ? PassportPages.DetailHref(Model.Identifier) : PassportPages.SummaryHref(Model.Identifier))" class="bp-report-action" aria-label="Open the current version" title="Open the current version" data-tooltip="Open the current version" data-action-icon="latest">
diff --git a/web/Views/Passport/_PassportTabs.cshtml b/web/Views/Passport/_PassportTabs.cshtml
index 5660e03..9b5c661 100644
--- a/web/Views/Passport/_PassportTabs.cshtml
+++ b/web/Views/Passport/_PassportTabs.cshtml
@@ -31,7 +31,7 @@
             <div class="tab-pane fade@(index == 0 ? " show active" : string.Empty)" id="tab-@tab.Key" role="tabpanel">
                 <h2 class="h5 mb-2">@tab.Title</h2>
                 <p class="bp-help-text">@tab.Note</p>
-                @if (tab.Key == "purchaser" && !Model.IsCurrent)
+                @if (tab.Key == "purchaser" && Model.Current is not null)
                 {
                     <p class="bp-warning">The live values (state of charge, cycles, temperature, accidents and the extreme-temperature times) are on the <a href="@PassportPages.DetailHref(Model.Identifier)">current version</a>.</p>
                 }
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~SuccessionPagesTests | FullyQualifiedName~StatusPagesTests | FullyQualifiedName~StatusChangeTests | FullyQualifiedName~BatteryRouteAndRegistryTests"`
Expected: `Passed!  - Failed:     0, Passed:    59`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2199`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs BatteryPassWeb.Tests/Dpp/StatusChangeTests.cs BatteryPassWeb.Tests/Dpp/StatusPagesTests.cs BatteryPassWeb.Tests/Dpp/SuccessionPagesTests.cs web/Controllers/BatteriesController.cs web/Controllers/ClusterBatteriesController.cs web/Controllers/DppApiController.cs web/Controllers/PassportsApiController.cs web/Dpp/Pages/BatteryCards.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/ClusterBatteryPages.cs web/Dpp/Pages/PassportPages.cs web/Dpp/Pages/RegistryList.cs web/Dpp/Pages/StatusPages.cs web/Views/Batteries/Batteries.cshtml web/Views/Batteries/Battery.cshtml web/Views/Batteries/_AccidentsCard.cshtml web/Views/Batteries/_LiveValuesCard.cshtml web/Views/Batteries/_StatusCard.cshtml web/Views/Batteries/_VersionsCard.cshtml web/Views/Passport/_PassportHero.cshtml web/Views/Passport/_PassportTabs.cshtml
git commit -m "feat(dpp): the new linked passport on the battery and passport pages" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: A repurposed battery with its new passport in the demo data

The demo seed repurposes DEMO-C13M-0001 and makes its new passport the way the team does: made from the old battery, given a three-year warranty, published, which makes the old passport inactive. The demo tests count 8 batteries, 5 published, 9 versions.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`
- Modify: `web/Dpp/Demo/DemoCatalogue.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Dpp/Demo/demo-catalogue.json`

- [ ] **Step 1: Write the failing tests**

Patch `4c1-task3-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoCardTests.cs`, `BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`, `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`, `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`). Save the block as `../4c1-task3-tests.patch`, outside the worktree, and run `git apply ../4c1-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
index 9083866..7ed1c0c 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
@@ -51,7 +51,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
         var result = await controller.ResetDemoData(CancellationToken.None);
 
         Assert.Equal("/admin/clusters?tab=clusters", Assert.IsType<RedirectResult>(result).Url);
-        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 7 batteries, 4 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
+        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 8 batteries, 5 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
         var tokens = JsonSerializer.Deserialize<List<DemoToken>>((string)controller.TempData["GeneratedCredentials"]!)!;
         Assert.Equal(7, tokens.Count);
         Assert.All(tokens, token => Assert.False(string.IsNullOrWhiteSpace(token.Value)));
@@ -66,7 +66,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
 
         await controller.ClearDemoData(CancellationToken.None);
 
-        Assert.StartsWith("Demo data cleared: 7 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
+        Assert.StartsWith("Demo data cleared: 8 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
         Assert.Null(controller.TempData["GeneratedCredentials"]);
         Assert.Equal(0, await rig.Count(DppStore.Packs));
     }
diff --git a/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs b/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
index 6f1618e..92f98db 100644
--- a/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs
@@ -68,7 +68,7 @@ public sealed class DemoCatalogueTests
     public void TheBatteries_ShouldBeInSeveralStates()
     {
         Assert.Equal(["DEMO-C7M-0001", "DEMO-C13M-0001", "DEMO-CORE-0001", "DEMO-CORE-0003"], Catalogue.Packs.Where(pack => pack.Publish).Select(pack => pack.Serial));
-        Assert.Equal(["DEMO-C7M-0001 Repaired", "DEMO-CORE-0003 Waste"], Catalogue.Packs.Where(pack => pack.Status is not null).Select(pack => $"{pack.Serial} {pack.Status!.What}"));
+        Assert.Equal(["DEMO-C7M-0001 Repaired", "DEMO-C13M-0001 Repurposed", "DEMO-CORE-0003 Waste"], Catalogue.Packs.Where(pack => pack.Status is not null).Select(pack => $"{pack.Serial} {pack.Status!.What}"));
         Assert.Equal(["DEMO-C7M-0001"], Catalogue.Packs.Where(pack => pack.NewVersion is not null).Select(pack => pack.Serial));
         Assert.Equal(2, Catalogue.Packs.Count(pack => pack.Accident is not null));
         Assert.Equal(2, Catalogue.Packs.Count(pack => !pack.Values.ContainsKey("manufacturingDate")));
diff --git a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
index 5190a9c..51f8b65 100644
--- a/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoClearTests.cs
@@ -29,7 +29,7 @@ public sealed class DemoClearTests(MongoFixture mongo)
         }
 
         Assert.Empty(result.Notes);
-        Assert.Contains("7 batteries removed", result.Done);
+        Assert.Contains("8 batteries removed", result.Done);
         Assert.Contains("12 accounts removed", result.Done);
     }
 
@@ -44,7 +44,7 @@ public sealed class DemoClearTests(MongoFixture mongo)
 
         Assert.Equal(before + 1, await rig.Count("applicationAuditEvents"));
         var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.ClearedEvent))).Single();
-        Assert.Equal(7, entry["metadata"]["batteries"].ToInt32());
+        Assert.Equal(8, entry["metadata"]["batteries"].ToInt32());
     }
 
     [Fact]
@@ -97,12 +97,12 @@ public sealed class DemoClearTests(MongoFixture mongo)
         var result = await rig.Demo.ResetAsync(DemoRig.Who);
 
         var packs = (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).ToList();
-        Assert.Equal(7, packs.Count);
+        Assert.Equal(8, packs.Count);
         Assert.Empty(packs.Intersect(firstPacks));
-        Assert.Equal(7, await rig.Count(DppStore.Versions));
+        Assert.Equal(9, await rig.Count(DppStore.Versions));
         Assert.Equal(7, result.Tokens.Count);
         Assert.Empty(result.Tokens.Select(token => token.Value).Intersect(first.Tokens.Select(token => token.Value)));
-        Assert.Contains("7 batteries removed", result.Done);
-        Assert.Contains("7 batteries, 4 of them published", result.Done);
+        Assert.Contains("8 batteries removed", result.Done);
+        Assert.Contains("8 batteries, 5 of them published", result.Done);
     }
 }
diff --git a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
index bdfdf5e..ecb2bc6 100644
--- a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
@@ -30,8 +30,8 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.Equal(2, await rig.Count(DppStore.Sites));
         Assert.Equal(2, await rig.Count(DppStore.Operators));
         Assert.Equal(3, await rig.Count(DppStore.Batches));
-        Assert.Equal(7, await rig.Count(DppStore.Packs));
-        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "7 batteries, 4 of them published", "12 accounts and 7 API tokens"], result.Done);
+        Assert.Equal(8, await rig.Count(DppStore.Packs));
+        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "8 batteries, 5 of them published", "12 accounts and 7 API tokens"], result.Done);
     }
 
     [Fact]
@@ -42,8 +42,9 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         await rig.Demo.SeedAsync(DemoRig.Who);
 
         var versions = await rig.All(DppStore.Versions);
-        Assert.Equal(7, versions.Count); // C7M-0001: 3 (a new version, then its repair); CORE-0003: 2 (waste); two with 1
-        Assert.Equal(4, versions.Count(version => version["DppStatus"].AsString == "active"));
+        Assert.Equal(9, versions.Count); // C7M-0001: 3 (a new version, then its repair); C13M-0001: 2 (repurposed) and its new passport 1; CORE-0003: 2 (waste); CORE-0001: 1
+        Assert.Equal(4, versions.Count(version => version["DppStatus"].AsString == "active")); // C13M-0001's old passport is inactive
+        Assert.Equal(1, versions.Count(version => version["DppStatus"].AsString == "inactive"));
         Assert.All(versions, version => Assert.True(version.Contains("Proof")));
         var firstPack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single()["_id"].AsString;
         Assert.Equal([1, 2, 3], versions.Where(version => version["PackId"] == firstPack).Select(version => version["Number"].ToInt32()).Order());
@@ -51,7 +52,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
     }
 
     [Fact]
-    public async Task TheStatusExamples_ShouldBeARepairedBatteryAndAWasteOne()
+    public async Task TheStatusExamples_ShouldBeARepairedARepurposedAndAWasteBattery()
     {
         using var rig = await DemoRig.StartAsync(mongo);
 
@@ -64,7 +65,13 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.Equal(6, repaired["Values"]["capacityFade"]["Value"]["percentageValue"].ToInt32());
         Assert.Equal("waste", waste["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
         Assert.Empty(waste["Repairs"].AsBsonArray);
-        Assert.Equal(2, await rig.Count("applicationAuditEvents", Eq("eventType", BatteryPassWeb.Dpp.Editing.StatusChangeService.ChangedEvent)));
+        Assert.Equal(3, await rig.Count("applicationAuditEvents", Eq("eventType", BatteryPassWeb.Dpp.Editing.StatusChangeService.ChangedEvent)));
+        var repurposed = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C13M-0001"))).ToList();
+        Assert.Equal(2, repurposed.Count); // the old battery and its new passport, same serial number
+        var newOne = repurposed.Single(pack => pack.Contains("SuccessorOf") && !pack["SuccessorOf"].IsBsonNull);
+        Assert.Equal("repurposed", newOne["Values"]["batteryStatus"]["Value"]["batteryStatusValues"].AsString);
+        Assert.Equal("P3Y", newOne["Values"]["warrantyPeriod"]["Value"].AsString);
+        Assert.True(newOne["IsDemo"].AsBoolean);
     }
 
     [Fact]
@@ -88,7 +95,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         await rig.Demo.SeedAsync(DemoRig.Who);
 
         var packs = await rig.All(DppStore.Packs);
-        Assert.Equal([0, 0, 0, 7, 10, 10, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
+        Assert.Equal([0, 0, 0, 7, 10, 10, 13, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
         Assert.Equal(2, await rig.Count(DocumentStore.Records, Eq("Property", "informationOnAccidents")));
         Assert.Equal(12, await rig.Count(DocumentStore.Records, Eq("OwnerKind", "Model")));
         Assert.Equal(3, await rig.Count(DocumentStore.Records, Eq("Property", "resultsOfTestReportsProvingCompliance")));
@@ -149,7 +156,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 
         Assert.Equal([DemoDataService.NoPassword], result.Notes);
         Assert.Equal(0, await rig.Count("users"));
-        Assert.Equal(7, await rig.Count(DppStore.Packs));
+        Assert.Equal(8, await rig.Count(DppStore.Packs));
     }
 
     [Fact]
@@ -198,7 +205,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         var again = await rig.Demo.SeedAsync(DemoRig.Who);
 
         Assert.Equal(["The demo data is already there: reset it or clear it first."], again.Notes);
-        Assert.Equal(7, await rig.Count(DppStore.Packs));
+        Assert.Equal(8, await rig.Count(DppStore.Packs));
     }
 
     [Fact]
@@ -221,6 +228,6 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 
         var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.SeededEvent))).Single();
         Assert.Equal(DemoRig.Who, entry["actor"].AsString);
-        Assert.Equal((7, 4, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
+        Assert.Equal((8, 5, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
     }
 }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo"`
Expected: `Failed:    13, Passed:    27` (failing: `DemoCardTests.ClearingFromTheCard_ShouldClearAndSayWhatWasRemoved`, `DemoCardTests.ResettingFromTheCard_ShouldSeedAndShowEveryNewTokenOnce`, `DemoCatalogueTests.TheBatteries_ShouldBeInSeveralStates`, `DemoClearTests.Clearing_ShouldKeepTheAuditLogAndLogItself`, `DemoClearTests.Clearing_ShouldRemoveEveryDemoRecordWithItsVersionsDocumentsAndFiles`, `DemoClearTests.Resetting_ShouldClearThenSeedWithNewBatteries`, `DemoSeedTests.SeedingAgain_ShouldAskToResetOrClearFirst`, `DemoSeedTests.TheBatteries_ShouldHaveModulesAccidentReportsAndDocuments`, `DemoSeedTests.ThePublishedBatteries_ShouldHaveSignedVersionsAndTheSecondOneAVersionTwo`, `DemoSeedTests.TheSeed_ShouldBeLoggedOnceWithTheCountsButNoTokenValues`, `DemoSeedTests.TheSeed_ShouldMakeTheFamiliesModelsAndBatteries`, `DemoSeedTests.TheStatusExamples_ShouldBeARepairedARepurposedAndAWasteBattery`, `DemoSeedTests.WithoutTheSetting_ShouldSeedEverythingButTheAccounts`).

- [ ] **Step 3: Write the code**

Patch `4c1-task3-code.patch` (`web/Dpp/Demo/DemoCatalogue.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/Demo/demo-catalogue.json`). Save the block as `../4c1-task3-code.patch`, outside the worktree, and run `git apply ../4c1-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Demo/DemoCatalogue.cs b/web/Dpp/Demo/DemoCatalogue.cs
index 65d8109..549deb4 100644
--- a/web/Dpp/Demo/DemoCatalogue.cs
+++ b/web/Dpp/Demo/DemoCatalogue.cs
@@ -21,7 +21,10 @@ public sealed record DemoBatch(string Key, string Model, string Site, int Year,
 /// A status change the seed makes on a published demo battery (step 4b): what happened ("Repaired" or "Waste"), the
 /// re-stated values that differ from the battery's current ones, and for a repair how many days ago and what.
 /// </summary>
-public sealed record DemoStatus(string What, JsonObject? Restated = null, int DaysAgo = 0, string? Repair = null);
+public sealed record DemoStatus(string What, JsonObject? Restated = null, int DaysAgo = 0, string? Repair = null, DemoSuccessor? Successor = null);
+
+/// <summary>Scania's new passport of a demo battery placed on the market again (step 4c-1): made, given a warranty, published.</summary>
+public sealed record DemoSuccessor(string Warranty);
 
 /// <summary>
 /// A demo battery. <see cref="Publish"/> publishes it; <see cref="NewVersion"/> then changes these values, which makes
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index 4bdeae5..42b675e 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -34,6 +34,7 @@ public sealed class DemoDataService(
     SavePipeline pipeline,
     PublicationService publication,
     StatusChangeService statusChanges,
+    SuccessionService successions,
     DocumentService documents,
     DocumentStore documentStore,
     DppLinks links,
@@ -153,6 +154,7 @@ public sealed class DemoDataService(
         }
 
         var published = 0;
+        var successors = 0;
         foreach (var demo in catalogue.Packs)
         {
             var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form"), cancellationToken);
@@ -198,6 +200,12 @@ public sealed class DemoDataService(
             if (demo.Status is { } status && demo.Publish)
             {
                 await ChangeStatusAsync(team, packId, demo, status, cancellationToken);
+                if (status.Successor is { } next)
+                {
+                    await MakeSuccessorAsync(team, packId, demo, next, cancellationToken);
+                    successors++;
+                    published++;
+                }
             }
         }
 
@@ -206,7 +214,7 @@ public sealed class DemoDataService(
         var done = new List<string>
         {
             $"{catalogue.Families.Count} families, {catalogue.Models.Count} models, {catalogue.Sites.Count} sites, {catalogue.Operators.Count} operators and {catalogue.Batches.Count} batches",
-            $"{catalogue.Packs.Count} batteries, {published} of them published",
+            $"{catalogue.Packs.Count + successors} batteries, {published} of them published",
             $"{accounts} accounts and {demoTokens.Count} API tokens",
         };
         await audit.AppendApplicationAuditEventAsync(
@@ -219,7 +227,7 @@ public sealed class DemoDataService(
             {
                 ["families"] = catalogue.Families.Count,
                 ["models"] = catalogue.Models.Count,
-                ["batteries"] = catalogue.Packs.Count,
+                ["batteries"] = catalogue.Packs.Count + successors,
                 ["published"] = published,
                 ["accounts"] = accounts,
                 ["tokens"] = demoTokens.Count,
@@ -487,6 +495,28 @@ public sealed class DemoDataService(
         }
     }
 
+    /// <summary>
+    /// Scania's new passport of a demo battery placed on the market again (step 4c-1), the way the team makes it: made
+    /// from the old battery with its operator, given a warranty, published, which makes the old passport inactive.
+    /// </summary>
+    private async Task MakeSuccessorAsync(ChangeSender team, string packId, DemoPack demo, DemoSuccessor next, CancellationToken cancellationToken)
+    {
+        var old = await store.GetAsync<BatteryPack>(packId, cancellationToken) ?? throw new InvalidOperationException($"The demo battery {demo.Serial} is missing.");
+        var (successorId, problem) = await successions.CreateSuccessorAsync(team, packId, old.OperatorId, cancellationToken);
+        if (successorId is null)
+        {
+            throw new InvalidOperationException($"The demo battery {demo.Serial}'s new passport couldn't be made: {problem}.");
+        }
+
+        await MarkAsync(DppStore.Packs, successorId, cancellationToken);
+        await SaveAsync(team, TargetKind.Pack, successorId, new JsonObject { ["warrantyPeriod"] = next.Warranty }, cancellationToken);
+        var result = await publication.PublishAsync(team, successorId, cancellationToken);
+        if (result.Version is null)
+        {
+            throw new InvalidOperationException($"The demo battery {demo.Serial}'s new passport couldn't be published: {result.Problem ?? string.Join("; ", result.Report?.Blocking ?? [])}.");
+        }
+    }
+
     /// <summary>An export-shaped file (decision 31) with this many modules for the battery.</summary>
     private static byte[] ModuleExport(string serial, int modules)
     {
diff --git a/web/Dpp/Demo/demo-catalogue.json b/web/Dpp/Demo/demo-catalogue.json
index d1da27d..c8855f8 100644
--- a/web/Dpp/Demo/demo-catalogue.json
+++ b/web/Dpp/Demo/demo-catalogue.json
@@ -945,7 +945,18 @@
       },
       "publish": true,
       "history": true,
-      "modules": 13
+      "modules": 13,
+      "status": {
+        "what": "Repurposed",
+        "restated": {
+          "capacityFade": 12,
+          "powerFade": 9,
+          "expectedLifetimeInCalendarYears": 6
+        },
+        "successor": {
+          "warranty": "P3Y"
+        }
+      }
     },
     {
       "serial": "DEMO-C13M-0002",
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo"`
Expected: `Passed!  - Failed:     0, Passed:    40`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2199`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCardTests.cs BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs BatteryPassWeb.Tests/Demo/DemoClearTests.cs BatteryPassWeb.Tests/Demo/DemoSeedTests.cs web/Dpp/Demo/DemoCatalogue.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/Demo/demo-catalogue.json
git commit -m "feat(dpp): a repurposed battery with its new passport in the demo data" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2199, Skipped:     0, Total:  2199`. From the 2178 before: 2191 after Task 1, 2199 after Task 2, 2199 after Task 3.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 03a61b5 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. The managing session brings them to the product owner:

1. **Restart the app with the usual launcher**, sign in with a team account and press "Reset demo data" at the end of the Clusters tab.
2. **As the team:**
   - **Batteries:** DEMO-C13M-0001 is there twice: the old one "Inactive · version 2", the new one "Published · version 1".
   - **The old one:** "Inactive since …", no forms for live values or accident reports, and "This passport is inactive" with a link to the new one.
   - **The new one:** "New passport of an earlier battery … whose passport is now inactive".
   - **The old one's public summary** (from its page's "Public summary"): the "Inactive" pill and "Open the new passport". On the new one: "This passport replaces an earlier one".
   - **DEMO-C7M-0001, "Change status", "Prepared for re-use and placed on the market again":** the card asks who places it on the market again. "Make the new passport" opens the draft. Back on DEMO-C7M-0001, "Cancel the new passport": the draft goes, and the status is back.
3. **As a demo cluster admin** (`demo.fleet.admin@example.test`), on the phone:
   - **"Manage" on DEMO-CORE-0001, "Change status", "Remanufactured".**
   - **Another operator:** type a name and a web address, then "Link and set this passport inactive". You're back on the Registry, and DEMO-CORE-0001 has left it.
4. **Light and dark:** the moon button in the top bar switches.

## Next

Plan 4c-2 adds "invalid" (team and market surveillance, with a reason) and "marked for deletion" (team, waste only), both undone by the team, with an invalid demo battery and DEMO-CORE-0003 marked for deletion.
