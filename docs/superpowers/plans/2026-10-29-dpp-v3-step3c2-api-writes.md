# DPP V3 Step 3c-2: The API Writes on the New Model — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The external API's writing side moves to the new model. Team (global) tokens create draft batteries and publish passports; a battery system sends a battery's live values (#63, #83, #87, E1–E4) in place of telemetry, and their history is read from the change log, each value with its measurement time. The old writing calls go with the old controller and the services only it used: telemetry, operations, battery model, software version and the old passport snapshot, validate, sign and publish calls. The API help page documents and tries the new calls in a redesigned request workbench.

**Architecture:** `BatteryApi` writes through the pages' own services: `PackCreationService` creates a draft, `PublicationService` publishes, and `SavePipeline` saves the live values, so who may change what stays the pipeline's rule (design 6.1, J-10). A token becomes a sender beside the reader 3c-1 made of it (`DppReaders.SenderFor`): a global token is the team, a cluster token a battery system of its clusters. The pipeline's change log now records each saved value's measurement time, and `BatteryApi.HistoryAsync` reads it back, cut for the reader by `AudienceRules.MaySee`, the one rule of 3a. `DppApiController` serves the writing calls beside the reading ones; `ExternalApiController`, `BatteryCreationService` and `BatteryTelemetryRepository` are removed. The help page's workbench groups the calls, builds live values from fields and shows the answer in plain words, with a stylesheet of its own (`site.css` is not changed).

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, JsonSchema.Net 9.5.0, xUnit 2.9, EphemeralMongo 3.2.0. Plain CSS; nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 5, 6, 7.1, 7.3, 8.1–8.4 and the 3c bullets of 14) and the decisions record (decisions 28 and 29). Earlier plans: steps 1, 2a–2d-2, 3a, 3b and 3c-1 in `docs/superpowers/plans/`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 552705d (3c-1 done) plus the commit that adds this plan. Check with `git branch --show-current`.
- **Close the local app first.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Restart it after the plan.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  2099`. After this plan: 2116. The suite takes about a minute.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../3c2-task1-code.patch`), and run `git apply ../<name>` from the worktree root. `git apply` copes with this worktree's mixed line endings. Each patch applies to the files as the previous tasks left them, so keep the task order.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.
- **No data changes:** this plan changes no stored data. The old telemetry collection stays until 3d's reset; the four sample tokens stay too.

## Decisions taken with the product owner (29 Oct 2026)

1. **Creating batteries and publishing through the API** are for team (global) tokens only, as on the pages (design 6.1; taken for 3c-1's plan).
2. **The live-value call** is `POST batteries/{id}/live-values` and its history `GET batteries/{id}/live-values/history`, in place of `telemetry` and `telemetry/history`: the body changes anyway, so the address says what it is.
3. **A measurement older than the stored value is accepted,** as the pages do; the history shows every measurement with its own time.
4. **Two team-only lists, `GET batches` and `GET operators`,** give the IDs a new battery needs: the same choices as the New battery page.
5. **The telemetry simulator** (`scripts/demo-telemetry-simulator.ps1`) is removed with its five checks: the call it used is gone.
6. **The request workbench is redesigned:** the calls grouped (Reading, Live values, Team token), the method set by the call and shown as a tag; fields for the live values with their units from the attribute list; the answer as a table in plain words, with the JSON under "Show JSON"; the constructed request, the cURL command and the auth format under "Show request".
7. **The prototype's behaviour and the redesigned workbench** were approved as shown.

## Judgement calls in this plan

1. **Who writes what.** A global token is the team's (`ChangeSender.Team`); a cluster token is a battery system of its clusters (`ChangeSender` of kind `BatterySystemFeed`), so the pipeline lets it change only #63, #83, #87 and E1–E4 of its own batteries (J-10). Its clusters are compared ignoring case. The token's name in the change log is the reader's from 3c-1 (`api-token:<token ID>`).
2. **Finding the battery to write to:** the same `{id}` as the reading calls (battery ID, passport ID or serial number). The team writes to any battery, drafts included (end-of-line live values before publication, design 7.1). A cluster token writes only to its clusters' published batteries: a draft is unknown to it (404, J-6); a published battery of another cluster is refused (403), since its existence is public anyway. A serial number that more than one writable battery has is refused (409).
3. **The live-value call takes the live values only,** for every token: the catalogue's attributes that change in place and that a battery system feeds (`BatteryApi.LiveSet`: #63, #83, #87, E1–E4). Any other attribute is refused by key ("not a live value"); a key that isn't an attribute is ignored. Each key is judged on its own, an empty value never overwrites (J-12), and `?preview=true` checks without saving. The answer is 200 when a value was saved (or would be), 422 when none could be.
4. **Measurement time:** `measuredAt`, ISO 8601 with its offset; without it, the time of the call. A time more than five minutes ahead of the app's clock is refused (400). The values are stored with the source `feed` ("battery system" on the pages).
5. **The history:** every change set the pipeline saves is logged (`dpp.change`); each saved value's entry now also records when it was generated. The history lists the battery's saved live values of the last `hours` (1–168, default 168) by measurement time, newest first, each with its unit from the catalogue (so E1–E4 follow the catalogue if their unit changes, decision 28), when it was measured and saved, and its source. Entries saved before this plan have no measurement time; their save time stands in. It is served for published batteries only and cut by `AudienceRules.MaySee` for the battery's published cluster, so a change to who sees what (for example legitimate-interest readers seeing live values) changes the history by itself.
6. **Creating a battery:** `batchId`, `operatorId`, an optional `clusterId` that must exist, and the battery's own values by key (#11 required; #13, #17, #19 optional), through `PackCreationService` with the same checks as the New battery page. The values are stored with the source `api` ("API" on the pages). The old body (`batteryFamily`, `batteryModel`, `softwareVersion`, …) is refused with what is needed.
7. **Publishing:** one call checks, signs and publishes (`PublicationService`), for team tokens with Passport Lifecycle (sign) access: a draft becomes version 1, a changed battery gets a new version, an unchanged one stays (`"unchanged": true`). If something blocks publication, the answer is 409 with the blocking list. Creating needs write access, as before.
8. **The sample tokens** are tokens of `demo-cluster`: they send live values to that cluster's published batteries but can't create or publish. The workbench offers "Your own token" for the team calls and says so.
9. **Removed:** `ExternalApiController`, `BatteryCreationService`, `BatteryTelemetryRepository` (its index set-up in `ExternalApiInitializer` and its registration), the `MaxTelemetryPoints` setting (`MAX_TELEMETRY_POINTS`), the help page's writing rows, panels and templates for the old calls, and its old sample battery and passport IDs. The old services the cluster admins' pages still use stay until 3d.
10. **The help page** names the section route `{sectionName}`, as the controller does, so its check that every route is documented reads the new controller. The workbench's stylesheet `web/wwwroot/css/bp-api-help.css` is loaded by the help page only, as `bp-product-data.css` is by the team pages.
11. **Older tests:** 35 source-assertion tests read the replaced code. 22 now check the same thing in the new files; 13 go with what they checked (the telemetry parser and repository, the old creation service, the old battery-model, software and passport-lifecycle routes, the help page's old sample battery). The simulator's five checks go with it.
12. **Not changed:** `docs/end-user-testing-guide.md` and `docs/qa-test-pack.md` still describe the old calls; step 6 rewrites the help pages and the test pack.

## File structure

| File | Responsibility |
|---|---|
| `web/Dpp/Api/BatteryApi.cs` | The API's writing: finding the battery for a sender, the live values and their history, the creation choices, creating and publishing |
| `web/Dpp/Access/DppReaders.cs` (modify) | An API token as a sender (`SenderFor`) beside the reader |
| `web/Dpp/Editing/SavePipeline.cs` (modify) | Each saved value's measurement time in the change log |
| `web/Services/ApplicationAuditService.cs` (modify) | One record's events of one type since a moment |
| `web/Dpp/Pages/PassportPages.cs` (modify) | "API" as a source word |
| `web/Dpp/DppServiceCollectionExtensions.cs` (modify) | Registers `BatteryApi` |
| `web/Controllers/DppApiController.cs` (modify) | The writing endpoints beside the reading ones |
| `web/Controllers/ExternalApiController.cs`, `web/Services/BatteryCreationService.cs`, `web/Services/BatteryTelemetryRepository.cs` (delete) | The old writing side |
| `web/Program.cs`, `web/Services/ExternalApiInitializer.cs`, `web/Configuration/BatteryPassOptions.cs`, `web/appsettings*.json`, `web/.env.example` (modify) | Without the old services and the telemetry setting |
| `web/Views/Help/Index.cshtml`, `web/Controllers/HelpController.cs`, `web/Models/ViewModels/ExternalApiHelpViewModel.cs` (modify) | The help page's writing calls and the redesigned workbench |
| `web/wwwroot/css/bp-api-help.css` | The workbench's layout, loaded by the help page only |
| `scripts/demo-telemetry-simulator.ps1` (delete) | The telemetry simulator |


---

### Task 1: Writing through the API on the pages' services

One service does the API's writing with the services the team's pages use, so the save pipeline keeps deciding who may change what. The change log learns each value's measurement time, and the history is read back from it.

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/BatteryApiTests.cs`
- Create: `web/Dpp/Api/BatteryApi.cs`
- Modify: `web/Dpp/Access/DppReaders.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`
- Modify: `web/Services/ApplicationAuditService.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/BatteryApiTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The API's writing side (step 3c-2): creating, publishing and the live values, on the pages' services.</summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryApiTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = ChangeSender.Team("api-token:team");
    private static readonly ChangeSender NorthFeed = ChangeSender.Feed("api-token:north", BatteryTesting.North);
    private static readonly ChangeSender SouthFeed = ChangeSender.Feed("api-token:south", BatteryTesting.South);

    [Fact]
    public void TheLiveValues_ShouldBeTheCataloguesLiveSetThatABatterySystemFeeds()
    {
        Assert.Equal(["#63", "#83", "#87", "E1", "E2", "E3", "E4"], BatteryApi.LiveSet.Select(attribute => attribute.Label));
        Assert.Equal("#63, #83, #87, E1, E2, E3 and E4", BatteryApi.LiveSetWords);
    }

    [Fact]
    public void AToken_ShouldWriteAsTheTeamOrAsABatterySystemOfItsClusters()
    {
        var global = new ExternalApiTokenContext { TokenId = "t-1", GlobalAccess = true };
        var cluster = new ExternalApiTokenContext { TokenId = "t-2", ClusterIds = [" Cluster-Test "] };

        var team = DppReaders.SenderFor(global);
        var feed = DppReaders.SenderFor(cluster);

        Assert.Equal(SenderKind.Team, team.Kind);
        Assert.Equal(DppReaders.For(global).Who, team.Who);
        Assert.Equal(SenderKind.BatterySystemFeed, feed.Kind);
        Assert.Equal(DppReaders.For(cluster).Who, feed.Who);
        Assert.Contains(BatteryTesting.North, feed.ClusterIds);
        Assert.Contains("CLUSTER-TEST", feed.ClusterIds);
    }

    [Fact]
    public async Task ABatteryToWrite_ShouldBeFoundForItsOwnClusterAndADraftForTheTeamOnly()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var api = Api(rig);

        Assert.Equal(WriteRefusal.None, (await api.FindAsync(Team, Serial(rig))).Refusal);
        Assert.Equal(WriteRefusal.NotFound, (await api.FindAsync(NorthFeed, Serial(rig))).Refusal);

        await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        foreach (var id in new[] { PassportPagesTests.Identifier(rig), version.DigitalProductPassportId, Serial(rig) })
        {
            var found = await api.FindAsync(NorthFeed, id);
            Assert.Equal(rig.PackId, found.Pack?.Id);
            Assert.Equal(PassportPagesTests.Identifier(rig), found.Identifier);
        }

        Assert.Equal(WriteRefusal.NotYours, (await api.FindAsync(SouthFeed, Serial(rig))).Refusal);
        Assert.Equal(WriteRefusal.NotFound, (await api.FindAsync(Team, "no-such-battery")).Refusal);
    }

    [Fact]
    public async Task LiveValues_ShouldBeSavedInPlaceWithTheirMeasurementTime()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var measured = rig.Clock.Now.UtcDateTime.AddMinutes(-10);
        rig.Clock.Now = rig.Clock.Now.AddMinutes(1);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var result = await Api(rig).WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 64), ("temperatureInformation", 23), ("timeSpentInExtremeTemperaturesAboveBoundary", 120), ("batteryStatus", "waste"), ("ipAddress", "10.0.0.1")), measured, preview: false);

        Assert.True(result.Saved);
        Assert.Equal(
            ["stateOfChargeSoC:Saved", "temperatureInformation:Saved", "timeSpentInExtremeTemperaturesAboveBoundary:Saved", "batteryStatus:Rejected", "ipAddress:Ignored"],
            result.Keys.Select(key => $"{key.Key}:{key.Outcome}"));
        Assert.Equal("#22 is not a live value; this call takes #63, #83, #87, E1, E2, E3 and E4", result.Keys[3].Message);
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        Assert.Equal(64, stored["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(measured, stored["stateOfChargeSoC"].GeneratedAtUtc);
        Assert.Equal(BatteryApi.FeedSource, stored["stateOfChargeSoC"].Source);
        Assert.Equal(NorthFeed.Who, stored["stateOfChargeSoC"].Who);
        var versions = await rig.Store.ListVersionsAsync(rig.PackId);
        Assert.Single(versions);
        Assert.Equal(rig.Clock.Now.UtcDateTime, versions[0].LastUpdatedUtc);
    }

    [Fact]
    public async Task ABatterySystemOfAnotherCluster_ShouldChangeNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var result = await Api(rig).WriteLiveValuesAsync(SouthFeed, pack, Values(("stateOfChargeSoC", 10)), null, preview: false);

        Assert.False(result.Saved);
        Assert.Equal(KeyOutcome.Rejected, result.Keys.Single().Outcome);
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task APreview_ShouldReportWithoutSaving()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var result = await Api(rig).WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 10), ("temperatureInformation", "")), null, preview: true);

        Assert.False(result.Saved);
        Assert.Equal([KeyOutcome.Saved, KeyOutcome.KeptEmpty], result.Keys.Select(key => key.Outcome));
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task TheChangeLog_ShouldKeepEachSavedValuesMeasurementTime()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var measured = new DateTime(2026, 10, 28, 8, 15, 0, DateTimeKind.Utc);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        await Api(rig).WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 64)), measured, preview: false);

        var change = await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", SavePipeline.AuditEventType)).SortByDescending(document => document["createdAt"]).FirstAsync();
        var key = change["metadata"]["keys"][0].AsBsonDocument;
        Assert.Equal("stateOfChargeSoC", key["key"].AsString);
        Assert.Equal("2026-10-28T08:15:00.0000000Z", key["generatedAt"].AsString);
    }

    [Fact]
    public async Task TheHistory_ShouldListEachSavedLiveValueNewestFirstForThoseWhoMaySeeIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var api = Api(rig);
        var now = rig.Clock.Now.UtcDateTime;
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        await api.WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 70), ("temperatureInformation", 21)), now.AddHours(-30), preview: false);
        await api.WriteLiveValuesAsync(NorthFeed, pack, Values(("stateOfChargeSoC", 64)), now.AddHours(-2), preview: false);
        await api.WriteLiveValuesAsync(NorthFeed, pack, Values(("timeSpentInExtremeTemperaturesAboveBoundary", 120)), now.AddHours(-1), preview: false);

        var own = await api.HistoryAsync(Reader.With("north", ReaderRole.ClusterUser, BatteryTesting.North), rig.PackId, 24);
        var week = await api.HistoryAsync(Reader.With("team", ReaderRole.Team), rig.PackId, BatteryApi.MaxHistoryHours);
        var other = await api.HistoryAsync(Reader.With("south", ReaderRole.ClusterUser, BatteryTesting.South), rig.PackId, 24);

        Assert.Equal(["E1", "#63"], own.Select(entry => entry.Attribute.Label));
        Assert.Equal([now.AddHours(-1), now.AddHours(-2)], own.Select(entry => entry.MeasuredAtUtc));
        Assert.Equal(64, own[1].Value["percentageValue"]!.GetValue<int>());
        Assert.Equal(AttributeCatalog.Extensions[0].Unit, own[0].Attribute.Unit);
        Assert.All(own, entry => Assert.Equal(BatteryApi.FeedSource, entry.Source));
        Assert.Equal(["E1", "#63", "#63", "#87"], week.Select(entry => entry.Attribute.Label));
        Assert.Empty(other);
    }

    [Fact]
    public async Task AMeasurementTime_ShouldNotLieAheadOfTheAppsClock()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var now = rig.Clock.Now.UtcDateTime;

        Assert.False(Api(rig).InTheFuture(now.Add(BatteryApi.ClockTolerance)));
        Assert.True(Api(rig).InTheFuture(now.Add(BatteryApi.ClockTolerance).AddSeconds(1)));
    }

    [Fact]
    public async Task TheTeam_ShouldCreateADraftBatteryAndPublishIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var api = Api(rig);
        var choices = await api.ChoicesAsync();
        var batch = choices.Batches.Single();
        var economicOperator = choices.Operators.Single();

        var created = await api.CreateAsync(Team, batch.BatchId, economicOperator.Id, " Cluster-South ", Values(("batterySerialNumber", "VPC9-4001"), ("manufacturingDate", "2026-09")));

        Assert.Null(created.Problem);
        var pack = created.Pack!;
        Assert.Equal(BatteryTesting.South, pack.ClusterId);
        Assert.Equal(BatteryApi.ApiSource, pack.Values["batterySerialNumber"].Source);
        Assert.Equal("API", PassportPages.SourceWord(BatteryApi.ApiSource));
        Assert.Null(await rig.Store.GetActiveVersionAsync(pack.Id));

        var copied = await CopySeedValuesAsync(rig, pack.Id);
        var published = await api.PublishAsync(Team, copied);
        Assert.Null(published.Problem);
        Assert.Equal(1, published.Version!.Number);
    }

    [Theory]
    [InlineData("feed", null, "only the team creates batteries")]
    [InlineData("team", "no-such-cluster", "cluster no-such-cluster doesn't exist; name a registered cluster or none")]
    [InlineData("team-without-batch", null, "batchId and operatorId are required; GET batches and GET operators list them")]
    public async Task ABatteryThatCannotBeCreated_ShouldSayWhy(string who, string? cluster, string problem)
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var batch = who == "team-without-batch" ? null : rig.Seed.Sources.Batch.Id;

        var created = await Api(rig).CreateAsync(who == "feed" ? NorthFeed : Team, batch, rig.Seed.Sources.Operator.Id, cluster, Values(("batterySerialNumber", "VPC9-4002")));

        Assert.Null(created.Pack);
        Assert.Equal(problem, created.Problem);
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldPublish()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var result = await Api(rig).PublishAsync(NorthFeed, pack);

        Assert.Null(result.Version);
        Assert.Equal("only the team publishes passports", result.Problem);
    }

    internal static BatteryApi Api(PublicationRig rig) => new(
        rig.Store,
        rig.Pipeline,
        BatteryTesting.Creation(rig),
        rig.Publication,
        BatteryTesting.Pages(rig),
        new ClusterRepository(rig.Context),
        new ApplicationAuditService(rig.Context),
        rig.Clock);

    internal static Dictionary<string, JsonNode?> Values(params (string Key, object? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value is null ? null : JsonValue.Create(value.Value) as JsonNode, StringComparer.Ordinal);

    private static string Serial(PublicationRig rig) => rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value!.GetValue<string>();

    /// <summary>The seed pack's own values on a new pack, so it can be published; returns the pack as stored.</summary>
    private static async Task<BatteryPack> CopySeedValuesAsync(PublicationRig rig, string packId)
    {
        var values = rig.Seed.Sources.Pack.Values
            .Where(entry => entry.Key is not ("uniqueProductIdentifier" or "batteryStatus" or "informationOnAccidents" or "batterySerialNumber" or "manufacturingDate"))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        await rig.Store.SetValuesAsync<BatteryPack>(packId, values);
        return (await rig.Store.GetAsync<BatteryPack>(packId))!;
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.BatteryApiTests"`
Expected: the build fails because `BatteryApi` doesn't exist yet (CS0246).

- [ ] **Step 3: Write `BatteryApi.cs`**

`web/Dpp/Api/BatteryApi.cs`:

```csharp
using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Api;

/// <summary>Why a battery can't be written to by this sender; <see cref="None"/> when it can.</summary>
public enum WriteRefusal
{
    None,

    /// <summary>No battery this sender may know of has the ID: unknown, or a draft for a cluster token (J-6).</summary>
    NotFound,

    /// <summary>A published battery outside the token's clusters.</summary>
    NotYours,

    /// <summary>A serial number that more than one battery the sender may change has.</summary>
    Ambiguous,
}

/// <summary>The battery a write names, or why there is none.</summary>
public sealed record WriteTarget(BatteryPack? Pack, WriteRefusal Refusal)
{
    public string Identifier => Pack?.Values.GetValueOrDefault("uniqueProductIdentifier")?.Value?.GetValue<string>() ?? string.Empty;
}

/// <summary>One saved live value from the change log, as a reader may see it.</summary>
public sealed record LiveHistoryEntry(AttributeDefinition Attribute, JsonNode Value, DateTime MeasuredAtUtc, DateTime SavedAtUtc, string Source);

/// <summary>A batch a new battery can be made in, and the economic operators that can place it on the market.</summary>
public sealed record CreationChoices(IReadOnlyList<BatchChoice> Batches, IReadOnlyList<EconomicOperator> Operators);

/// <summary>
/// The external API's writing side (decision 29, step 3c-2), on the same services as the team's pages: creating a
/// battery (<see cref="PackCreationService"/>), publishing (<see cref="PublicationService"/>) and the live values
/// (<see cref="SavePipeline"/>). Who may change what is the pipeline's rule (design 6.1, J-10); this class only finds the
/// battery and keeps the live-value call to the live values. Their history is read from the pipeline's change log.
/// </summary>
public sealed class BatteryApi(
    DppStore store,
    SavePipeline pipeline,
    PackCreationService creation,
    PublicationService publication,
    BatteryPages batteries,
    ClusterRepository clusters,
    ApplicationAuditService audit,
    TimeProvider clock)
{
    /// <summary>The source stored with values a battery system sends through the API ("battery system" on the pages).</summary>
    public const string FeedSource = "feed";

    /// <summary>The source stored with values the API sends when it creates a battery.</summary>
    public const string ApiSource = "api";

    /// <summary>
    /// The live values (design 7.3): the attributes that change in place and that a battery system feeds, from the
    /// catalogue. Today #63, #83, #87 and E1–E4 (decision 28).
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> LiveSet { get; } = AttributeCatalog.All
        .Where(attribute => attribute.Change == ChangeRule.Live && attribute.Editors.HasFlag(AttributeEditors.BatterySystemFeed))
        .ToArray();

    /// <summary>How far ahead of the app's clock a measurement time may be, for clocks slightly apart.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    /// <summary>The longest history one call reads, in hours (one week, as the telemetry history did).</summary>
    public const int MaxHistoryHours = 168;

    /// <summary>"#63, #83, #87, E1, E2, E3 and E4", for messages.</summary>
    public static string LiveSetWords { get; } =
        $"{string.Join(", ", LiveSet.SkipLast(1).Select(attribute => attribute.Label))} and {LiveSet[^1].Label}";

    /// <summary>
    /// The battery a write names: its battery ID (#6), a passport ID (#5) or its serial number, as the reading calls take
    /// them. The team may change any battery, drafts included; any other sender only the published batteries of its own
    /// clusters, and a draft is unknown to it (J-6).
    /// </summary>
    public async Task<WriteTarget> FindAsync(ChangeSender sender, string? id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var text = id?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return new WriteTarget(null, WriteRefusal.NotFound);
        }

        var candidates = new List<BatteryPack>();
        if (await store.FindPackByUniqueProductIdentifierAsync(text, cancellationToken) is { } byIdentifier)
        {
            candidates.Add(byIdentifier);
        }
        else if (await store.GetVersionByPassportIdAsync(text, cancellationToken) is { } version
            && await store.GetAsync<BatteryPack>(version.PackId, cancellationToken) is { } byPassport)
        {
            candidates.Add(byPassport);
        }
        else
        {
            candidates.AddRange(await store.FindPacksBySerialNumberAsync(text, cancellationToken));
        }

        var known = new List<BatteryPack>();
        foreach (var pack in candidates)
        {
            if (sender.Kind == SenderKind.Team || await store.GetActiveVersionAsync(pack.Id, cancellationToken) is not null)
            {
                known.Add(pack);
            }
        }

        var mine = known.Where(pack => MayChange(sender, pack)).ToArray();
        return mine.Length switch
        {
            1 => new WriteTarget(mine[0], WriteRefusal.None),
            > 1 => new WriteTarget(null, WriteRefusal.Ambiguous),
            _ => new WriteTarget(null, known.Count > 0 ? WriteRefusal.NotYours : WriteRefusal.NotFound),
        };
    }

    /// <summary>
    /// Saves live values measured at one moment, through the save pipeline (design 6.2): each key is judged on its own
    /// and a refused key doesn't block the others (J-12). A key outside the live set is refused here, whoever sends it:
    /// static values change on the team's pages and, from step 5, by upload.
    /// </summary>
    public async Task<SaveResult> WriteLiveValuesAsync(
        ChangeSender sender,
        BatteryPack pack,
        IReadOnlyDictionary<string, JsonNode?> values,
        DateTime? measuredAtUtc,
        bool preview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(values);
        var notLive = values.Keys
            .Where(key => AttributeCatalog.FindByProperty(key) is { } attribute && !LiveSet.Contains(attribute))
            .ToHashSet(StringComparer.Ordinal);
        var live = values.Where(entry => !notLive.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var saved = live.Count == 0
            ? new SaveResult(true, false, [])
            : await pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, pack.Id, live, FeedSource, measuredAtUtc), preview, cancellationToken);
        var results = saved.Keys.ToDictionary(result => result.Key, StringComparer.Ordinal);
        var keys = values.Keys
            .Select(key => notLive.Contains(key)
                ? new KeyResult(key, KeyOutcome.Rejected, $"{AttributeCatalog.FindByProperty(key)!.Label} is not a live value; this call takes {LiveSetWords}")
                : results[key])
            .ToArray();
        return saved with { Keys = keys };
    }

    /// <summary>
    /// The live values saved for a battery since a moment, newest measurement first, as the reader may see them: the one
    /// rule (<see cref="AudienceRules.MaySee"/>) for the battery's published cluster, so a change to who sees what
    /// changes the history too. <paramref name="hours"/> counts back from now, by measurement time. Read from the change
    /// log, which keeps every saved value with its measurement time; an entry written before 3c-2 has none, and its save
    /// time stands in.
    /// </summary>
    public async Task<IReadOnlyList<LiveHistoryEntry>> HistoryAsync(Reader reader, string packId, int hours, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfLessThan(hours, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hours, MaxHistoryHours);
        var sinceUtc = clock.GetUtcNow().UtcDateTime.AddHours(-hours);
        var batteryClusters = await store.FindPublishedClusterIdsUsingAsync(packId: packId, cancellationToken: cancellationToken);
        var visible = LiveSet.Where(attribute => AudienceRules.MaySee(reader, attribute, batteryClusters)).ToDictionary(attribute => attribute.Property, StringComparer.Ordinal);
        if (visible.Count == 0)
        {
            return [];
        }

        var entries = new List<LiveHistoryEntry>();
        var events = await audit.ListEntityEventsAsync(SavePipeline.AuditEventType, "dpp-pack", packId, sinceUtc, cancellationToken);
        foreach (var change in events)
        {
            var savedAt = Moment(BsonHelpers.GetString(change, "createdAt")) ?? sinceUtc;
            var keys = change.GetValue("metadata", new BsonDocument()).AsBsonDocument.GetValue("keys", new BsonArray()).AsBsonArray;
            foreach (var key in keys.OfType<BsonDocument>())
            {
                if (key.GetValue("outcome", string.Empty) != KeyOutcome.Saved.ToString()
                    || !visible.TryGetValue(key.GetValue("key", string.Empty).ToString()!, out var attribute)
                    || key.GetValue("value", BsonNull.Value) is not BsonString text
                    || JsonNode.Parse(text.Value) is not { } value)
                {
                    continue;
                }

                var measuredAt = Moment(key.GetValue("generatedAt", BsonNull.Value) is BsonString generated ? generated.Value : null) ?? savedAt;
                if (measuredAt >= sinceUtc)
                {
                    entries.Add(new LiveHistoryEntry(attribute, value, measuredAt, savedAt, change.GetValue("source", string.Empty).ToString()!));
                }
            }
        }

        return entries.OrderByDescending(entry => entry.MeasuredAtUtc).ThenByDescending(entry => entry.SavedAtUtc).ToArray();
    }

    /// <summary>True when a measurement time lies ahead of the app's clock by more than <see cref="ClockTolerance"/>.</summary>
    public bool InTheFuture(DateTime measuredAtUtc) => measuredAtUtc.ToUniversalTime() > clock.GetUtcNow().UtcDateTime + ClockTolerance;

    /// <summary>The batches and economic operators a new battery can name, as the team's New battery page offers them.</summary>
    public async Task<CreationChoices> ChoicesAsync(CancellationToken cancellationToken = default)
    {
        var page = await batteries.NewAsync(cancellationToken: cancellationToken);
        return new CreationChoices(page.Batches, page.Operators);
    }

    /// <summary>
    /// Creates a draft battery (design 7.1), for the team only: the same rules as the New battery page. The cluster is
    /// optional but must exist.
    /// </summary>
    public async Task<PackCreationResult> CreateAsync(
        ChangeSender sender,
        string? batchId,
        string? operatorId,
        string? clusterId,
        IReadOnlyDictionary<string, JsonNode?> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(values);
        if (sender.Kind != SenderKind.Team)
        {
            return Problem("only the team creates batteries");
        }

        if (string.IsNullOrWhiteSpace(batchId) || string.IsNullOrWhiteSpace(operatorId))
        {
            return Problem("batchId and operatorId are required; GET batches and GET operators list them");
        }

        var cluster = string.IsNullOrWhiteSpace(clusterId) ? null : ClusterRepository.NormalizeClusterId(clusterId);
        if (cluster is not null && await clusters.GetClusterByIdAsync(cluster, cancellationToken) is null)
        {
            return Problem($"cluster {cluster} doesn't exist; name a registered cluster or none");
        }

        return await creation.CreateAsync(sender, new NewPack(batchId.Trim(), operatorId.Trim(), cluster, values, ApiSource), cancellationToken);

        static PackCreationResult Problem(string problem) => new(null, [], problem);
    }

    /// <summary>Publishes the battery's passport, or a new version of it (design 7.1, 7.2); the team only.</summary>
    public Task<PublicationResult> PublishAsync(ChangeSender sender, BatteryPack pack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return publication.PublishAsync(sender, pack.Id, cancellationToken);
    }

    /// <summary>The team changes every battery; a cluster admin or feed only those of its clusters, as the save pipeline checks again.</summary>
    private static bool MayChange(ChangeSender sender, BatteryPack pack) =>
        sender.Kind == SenderKind.Team || (pack.ClusterId is not null && sender.ClusterIds.Contains(pack.ClusterId));

    private static DateTime? Moment(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment) ? moment.UtcDateTime : null;
}
```

- [ ] **Step 4: Map tokens to senders, log measurement times and register the service**

Patch `3c2-task1-code.patch` (`web/Dpp/Access/DppReaders.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Editing/SavePipeline.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Services/ApplicationAuditService.cs`). Save the block as `../3c2-task1-code.patch`, outside the worktree, and run `git apply ../3c2-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Access/DppReaders.cs b/web/Dpp/Access/DppReaders.cs
--- a/web/Dpp/Access/DppReaders.cs
+++ b/web/Dpp/Access/DppReaders.cs
@@ -1,4 +1,5 @@
 using System.Security.Claims;
+using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Services;
 
 namespace BatteryPassWeb.Dpp.Access;
@@ -71,4 +72,21 @@ public sealed class DppReaders(ClusterRepository clusters)
                 token.ClusterIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(ClusterRepository.NormalizeClusterId).ToHashSet(StringComparer.Ordinal),
                 who);
     }
+
+    /// <summary>
+    /// An API token as the sender of a change (design 6.1, J-10), with the same name as its reader: a global token is the
+    /// team's; a token for named clusters is a battery system of those clusters, which changes only the live values of
+    /// their batteries.
+    /// </summary>
+    public static ChangeSender SenderFor(ExternalApiTokenContext token)
+    {
+        ArgumentNullException.ThrowIfNull(token);
+        var who = For(token).Who;
+        return token.GlobalAccess
+            ? ChangeSender.Team(who)
+            : new ChangeSender(
+                SenderKind.BatterySystemFeed,
+                who,
+                token.ClusterIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(ClusterRepository.NormalizeClusterId).ToHashSet(StringComparer.OrdinalIgnoreCase));
+    }
 }
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -47,6 +47,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<DppReaders>();
         services.AddSingleton<ReaderPages>();
         services.AddSingleton<PassportApi>();
+        services.AddSingleton<BatteryApi>();
         services.AddSingleton<PassportPages>();
         services.AddSingleton<AuditLines>();
         return services;
diff --git a/web/Dpp/Editing/SavePipeline.cs b/web/Dpp/Editing/SavePipeline.cs
--- a/web/Dpp/Editing/SavePipeline.cs
+++ b/web/Dpp/Editing/SavePipeline.cs
@@ -384,8 +384,10 @@ public sealed class SavePipeline(
         _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
     };
 
-    /// <summary>Each key's outcome, and for a saved or removed value the one it replaced, so the history shows before and after.</summary>
-    /// <summary>Each key's outcome, with the value it replaced and, when saved, the new value (both as JSON text).</summary>
+    /// <summary>
+    /// Each key's outcome, with the value it replaced and, when saved, the new value (both as JSON text) and when it was
+    /// generated, e.g. measured by the battery system: the live values' history is read from here (step 3c-2).
+    /// </summary>
     private static BsonDocument AuditMetadata(
         IReadOnlyList<KeyResult> results,
         IReadOnlyDictionary<string, AttributeValue> before,
@@ -409,6 +411,7 @@ public sealed class SavePipeline(
             if (result.Outcome == KeyOutcome.Saved && after.TryGetValue(result.Key, out var value) && value.Value is not null)
             {
                 entry["value"] = value.Value.ToJsonString();
+                entry["generatedAt"] = value.GeneratedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
             }
 
             return entry;
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -119,6 +119,7 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
         "form" => "form",
         "upload" => "upload",
         "feed" => "battery system",
+        "api" => "API",
         "seed" => "test data",
         _ => source,
     };
diff --git a/web/Services/ApplicationAuditService.cs b/web/Services/ApplicationAuditService.cs
--- a/web/Services/ApplicationAuditService.cs
+++ b/web/Services/ApplicationAuditService.cs
@@ -152,6 +152,31 @@ public sealed class ApplicationAuditService
             .ToList();
     }
 
+    /// <summary>
+    /// One record's events of one type since a moment, newest first, e.g. a battery's saved values (the change log). The
+    /// times are stored as round-trip text in UTC, which sorts and compares like the moments themselves.
+    /// </summary>
+    public async Task<IReadOnlyList<BsonDocument>> ListEntityEventsAsync(
+        string eventType,
+        string entityType,
+        string entityId,
+        DateTime sinceUtc,
+        CancellationToken cancellationToken = default)
+    {
+        if (GetApplicationAuditEventsCollection() is not { } collection)
+        {
+            return [];
+        }
+
+        var filter = Builders<BsonDocument>.Filter;
+        var since = new DateTimeOffset(DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc)).ToString("O");
+        return await collection
+            .Find(filter.Eq("eventType", eventType) & filter.Eq("entityType", entityType) & filter.Eq("entityId", entityId) & filter.Gte("createdAt", since))
+            .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
+            .Limit(5000)
+            .ToListAsync(cancellationToken);
+    }
+
     public async Task<IReadOnlyList<BsonDocument>> ListApplicationAuditEventsAsync(
         ApplicationAuditFilter filter,
         CancellationToken cancellationToken = default)
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.BatteryApiTests"`
Expected: `Passed!  - Failed:     0, Passed:    14`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2113`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteryApiTests.cs web/Dpp/Api/BatteryApi.cs web/Dpp/Access/DppReaders.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Editing/SavePipeline.cs web/Dpp/Pages/PassportPages.cs web/Services/ApplicationAuditService.cs
git commit -m "feat(dpp): write through the API on the pages' services: creating, publishing and the live values with their history" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 2: The writing calls replace the old ones

The writing endpoints join the reading ones in `DppApiController`; the old controller and the services only it used go, and the help page's writing rows, panels and templates follow in the same task, because a help-page test checks that every route of the controller is documented. Thirty-five older source-assertion tests read the replaced code: twenty-two now check the same thing in the new files, thirteen go with what they checked.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiInitializerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/SecurityLimitsTests.cs`
- Test (modify): `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryCreationServiceTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiTelemetryTemplateTests.cs`
- Modify: `web/.env.example`
- Modify: `web/Configuration/BatteryPassOptions.cs`
- Modify: `web/Controllers/DppApiController.cs`
- Modify: `web/Controllers/HelpController.cs`
- Modify: `web/Models/ViewModels/ExternalApiHelpViewModel.cs`
- Modify: `web/Program.cs`
- Modify: `web/Services/ExternalApiInitializer.cs`
- Modify: `web/Views/Help/Index.cshtml`
- Modify: `web/appsettings.Development.json`
- Modify: `web/appsettings.json`
- Delete: `web/Controllers/ExternalApiController.cs`
- Delete: `web/Services/BatteryCreationService.cs`
- Delete: `web/Services/BatteryTelemetryRepository.cs`

- [ ] **Step 1: Write the failing tests**

Patch `3c2-task2-tests.patch` (`BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`, `BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs`, `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`, `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`, `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`, `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`, `BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs`, `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`, `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`, `BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs`, `BatteryPassWeb.Tests/ExternalApiInitializerTests.cs`, `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`, `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`, `BatteryPassWeb.Tests/SecurityLimitsTests.cs`, `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`). Save the block as `../3c2-task2-tests.patch`, outside the worktree, and run `git apply ../3c2-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
@@ -140,11 +140,9 @@ public sealed class AdminFeedbackImplementationTests
     {
         var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
         var historyModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs"));
-        var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
 
         Assert.Contains("CanCreateBatteryPassport", clusterAdminController);
         Assert.Contains("CanCreatePassport", historyModel);
-        Assert.Contains("ClearNewPassportRequiredAsync", externalApi);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs b/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs
--- a/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs
+++ b/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs
@@ -16,10 +16,10 @@ public sealed class AntiForgeryProtectionTests
     [Fact]
     public void ExternalApiTokenEndpoints_ShouldNotRequireBrowserAntiForgeryTokens()
     {
-        var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
+        var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
 
         Assert.DoesNotContain("ValidateAntiForgeryToken", externalApi);
-        Assert.Contains("Authorization", externalApi);
+        Assert.Contains("ExternalApiCredentials.Token(Request)", externalApi);
         Assert.Contains("ValidateTokenAsync", externalApi);
     }
 
diff --git a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
--- a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
+++ b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
@@ -127,14 +127,9 @@ public sealed class ApplicationAuditCoverageTests
     public void BatteryAndPassportWritePaths_ShouldAppendMeaningfulAuditEvents()
     {
         var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
         var snapshot = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));
 
         Assert.Contains("\"battery.updated\"", clusterAdmin);
-        Assert.Contains("\"battery.telemetry.written\"", externalApi);
-        Assert.Contains("\"battery.operations.updated\"", externalApi);
-        Assert.Contains("\"battery.model.updated\"", externalApi);
-        Assert.Contains("\"battery.software.updated\"", externalApi);
         Assert.Contains("\"passport.created\"", snapshot);
         Assert.Contains("\"battery.passport.created\"", snapshot);
     }
diff --git a/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs b/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
--- a/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
+++ b/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
@@ -39,9 +39,7 @@ public sealed class BatteryAdminWorkflowTests
     public void AdminBatteryCreate_ShouldRequireClusterAndSnapshotShouldClearPendingFlag()
     {
         var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-        var creationService = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));
 
-        Assert.Contains("Battery cluster is required.", creationService);
         Assert.DoesNotContain("snapshot[\"newPassportRequired\"] = true;", controller);
     }
 
@@ -97,13 +95,9 @@ public sealed class BatteryAdminWorkflowTests
     [Fact]
     public void FollowupBatteryCreate_ShouldRequireClusterAndUniqueSerial()
     {
-        var creationService = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));
         var repository = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));
         var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
 
-        Assert.Contains("Battery cluster is required.", creationService);
-        Assert.Contains("Battery serial number already exists.", creationService);
-        Assert.Contains("GetBySerialNumberAsync", creationService);
         Assert.Contains("GetBySerialNumberAsync", repository);
         Assert.Contains("name=\"clusterId\" required", view);
         Assert.Contains("is-policy-locked", view);
diff --git a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
--- a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
+++ b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
@@ -62,7 +62,7 @@ public sealed class BatteryFamilyAccessApiRevisedTests
     {
         var modelSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateModels.cs"));
         var serviceSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
-        var apiSource = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
+        var apiSource = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
 
         Assert.Contains("string SoftwareVersion", modelSource);
         Assert.Contains("string SoftwareReleaseDate", modelSource);
@@ -76,18 +76,14 @@ public sealed class BatteryFamilyAccessApiRevisedTests
     [Fact]
     public void ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign()
     {
-        var api = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
+        var api = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
         var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
         var initializer = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));
 
         Assert.Contains("ExternalTokenAccessMode.Sign", repository);
         Assert.Contains("ExternalTokenRequirement.Sign", api);
-        Assert.Contains("ValidateTokenForRequirementAsync", api);
-        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/battery-model\")]", api);
-        Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports\")]", api);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", api);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/sign\")]", api);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/publish\")]", api);
+        Assert.Contains("tokens.ValidateTokenAsync", api);
+        Assert.Contains("[HttpPost(\"batteries/{id}/publish\")]", api);
         Assert.DoesNotContain("X-Battery-Secret", api);
         Assert.DoesNotContain("ValidateBatterySecretAsync", api);
         Assert.DoesNotContain("UpsertBatterySecretAsync", repository);
@@ -98,11 +94,9 @@ public sealed class BatteryFamilyAccessApiRevisedTests
     [Fact]
     public void ValidateSignPublish_ShouldUseSharedAutoRepublishWorkflow()
     {
-        var api = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
         var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
         var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
 
-        Assert.Contains("PassportTrustWorkflowService", api);
         Assert.Contains("ShouldAutoPublishAfterSign", workflow);
         Assert.Contains("AutoPublished", workflow);
         Assert.Contains("registryInfo.hasBeenPublished", repository);
diff --git a/BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs b/BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs
--- a/BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs
+++ b/BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs
@@ -79,17 +79,6 @@ public sealed class BatterySnapshotWorkflowTests
         Assert.Contains("_batteryRepository.EnsureIndexesAsync", source);
     }
 
-    [Fact]
-    public void TelemetryRepository_ShouldUseBatteryIdInsteadOfPassportId()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryTelemetryRepository.cs"));
-
-        Assert.Contains("batteryId", source);
-        Assert.Contains("AppendTelemetryAsync(string batteryId", source);
-        Assert.Contains("ReadHistoryAsync(string batteryId", source);
-        Assert.DoesNotContain("Ascending(\"passportId\")", source);
-    }
-
     [Fact]
     public void PassportViewModel_ShouldExposeBatteryIdAndHistoricalState()
     {
diff --git a/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs b/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs
--- a/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs
@@ -14,7 +14,7 @@ using Microsoft.AspNetCore.RateLimiting;
 
 namespace BatteryPassWeb.Tests.Dpp;
 
-/// <summary>The external API's reading endpoints (step 3c-1) and the team-only passports list, called with real tokens.</summary>
+/// <summary>The external API's reading endpoints (steps 3c-1 and 3c-2) and the team-only passports list, called with real tokens.</summary>
 [Collection(MongoCollection.Name)]
 public sealed class DppApiControllerTests(MongoFixture mongo)
 {
@@ -27,24 +27,15 @@ public sealed class DppApiControllerTests(MongoFixture mongo)
 
         Assert.Equal("api/external/v1", type.GetCustomAttribute<RouteAttribute>()?.Template);
         Assert.Null(type.GetCustomAttribute<AuthorizeAttribute>());
-        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
+        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
+            .Where(action => action.GetCustomAttribute<HttpGetAttribute>() is not null)
+            .ToArray();
         Assert.Equal(
-            ["batteries/{id}", "batteries/{id}/passports", "batteries/{id}/paths", "batteries/{id}/section/{sectionName}", "batteries/{id}/values", "clusters", "clusters/{clusterId}/batteries"],
+            ["batches", "batteries/{id}", "batteries/{id}/live-values/history", "batteries/{id}/passports", "batteries/{id}/paths", "batteries/{id}/section/{sectionName}", "batteries/{id}/values", "clusters", "clusters/{clusterId}/batteries", "operators"],
             actions.Select(action => action.GetCustomAttribute<HttpGetAttribute>()!.Template).Order());
         Assert.All(actions, action => Assert.Equal(SecurityRateLimitPolicyNames.ExternalApiRead, action.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName));
     }
 
-    [Fact]
-    public void TheOldController_ShouldKeepOnlyItsWritingEndpointsUntil3c2()
-    {
-        var gets = typeof(ExternalApiController).GetMethods()
-            .Select(method => method.GetCustomAttribute<HttpGetAttribute>()?.Template)
-            .OfType<string>()
-            .ToArray();
-
-        Assert.Equal(["batteries/{batteryId}/telemetry/history"], gets);
-    }
-
     [Theory]
     [InlineData(null, StatusCodes.Status401Unauthorized)]
     [InlineData("not-a-token", StatusCodes.Status401Unauthorized)]
@@ -231,9 +222,9 @@ public sealed class DppApiControllerTests(MongoFixture mongo)
         };
     }
 
-    private static ExternalApiRepository Repository(PublicationRig rig) => new(rig.Context, new ExternalApiSecurityService(Key));
+    internal static ExternalApiRepository Repository(PublicationRig rig) => new(rig.Context, new ExternalApiSecurityService(Key));
 
-    private static DppApiController Controller(PublicationRig rig, string? token)
+    internal static DppApiController Controller(PublicationRig rig, string? token)
     {
         var http = new DefaultHttpContext();
         if (token is not null)
@@ -241,7 +232,7 @@ public sealed class DppApiControllerTests(MongoFixture mongo)
             http.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
         }
 
-        return new DppApiController(Repository(rig), new ClusterRepository(rig.Context), PassportApiTests.Api(rig))
+        return new DppApiController(Repository(rig), new ClusterRepository(rig.Context), PassportApiTests.Api(rig), BatteryApiTests.Api(rig))
         {
             ControllerContext = new ControllerContext { HttpContext = http },
         };
@@ -264,7 +255,7 @@ public sealed class DppApiControllerTests(MongoFixture mongo)
         },
     };
 
-    private static JsonElement Json(ObjectResult result) => JsonSerializer.SerializeToElement(result.Value);
+    internal static JsonElement Json(ObjectResult result) => JsonSerializer.SerializeToElement(result.Value);
 
     private static JsonElement Data(IActionResult result)
     {
diff --git a/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs b/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs
@@ -13,46 +13,44 @@ public sealed class ExternalApiBatteryCreateTests
     [Fact]
     public void ExternalApi_ShouldExposeCreateBatteryEndpointWithWriteAccessAndSharedService()
     {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
+        var source = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
 
         Assert.Contains("[HttpPost(\"batteries\")]", source);
         Assert.Contains("CreateBattery(", source);
-        Assert.Contains("ExternalTokenRequirement.Write", source);
-        Assert.Contains("_batteryCreationService.CreateBatteryAsync", source);
-        Assert.Contains("BatteryCreationClusterScope.FromToken(auth.TokenContext!)", source);
-        Assert.Contains("createdByTokenId", source);
-        Assert.Contains("createPassportPath", source);
+        Assert.Contains("TeamTokenAsync(ExternalTokenRequirement.Write, TeamOnlyCreate", source);
+        Assert.Contains("writing.CreateAsync", source);
+        Assert.Contains("publishPath", source);
     }
 
     [Fact]
     public void ExternalApiCreateBattery_ShouldParseOnlyExplicitCreationFields()
     {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
+        var source = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
 
-        Assert.Contains("ReadBatteryCreationCommand", source);
-        Assert.Contains("ReadString(payload, \"batteryFamily\")", source);
-        Assert.Contains("ReadString(payload, \"productId\")", source);
-        Assert.Contains("ReadString(payload, \"batteryModel\")", source);
-        Assert.Contains("ReadString(payload, \"productVersion\")", source);
-        Assert.Contains("ReadString(payload, \"softwareVersion\")", source);
-        Assert.Contains("ReadString(payload, \"serialNumber\")", source);
-        Assert.Contains("ReadString(payload, \"clusterId\")", source);
+        Assert.Contains("Text(body, \"batchId\")", source);
+        Assert.Contains("Text(body, \"operatorId\")", source);
+        Assert.Contains("Text(body, \"clusterId\")", source);
+        Assert.Contains("body.TryGetProperty(\"values\"", source);
+        Assert.DoesNotContain("batteryFamily\")", source);
+        Assert.DoesNotContain("softwareVersion\")", source);
         Assert.DoesNotContain("BsonDocument.Parse", source);
     }
 
     [Fact]
-    public void ExternalApiHelp_ShouldDocumentCreateBatteryEndpointAndSeparatePassportCreation()
+    public void ExternalApiHelp_ShouldDocumentCreatingADraftBatteryInABatch()
     {
         var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
 
-        Assert.Contains("@Model.BasePath/batteries</code><small>Create battery record", help);
-        Assert.Contains("serialNumber", help);
-        Assert.Contains("batteryFamily", help);
-        Assert.Contains("batteryModel", help);
-        Assert.Contains("softwareVersion", help);
+        Assert.Contains("@Model.BasePath/batteries</code><small>Create a draft battery", help);
+        Assert.Contains("@Model.BasePath/batches</code>", help);
+        Assert.Contains("@Model.BasePath/operators</code>", help);
+        Assert.Contains("batterySerialNumber", help);
+        Assert.Contains("batchId", help);
+        Assert.Contains("operatorId", help);
         Assert.Contains("clusterId", help);
-        Assert.Contains("Create the battery record first, then create a passport snapshot", help);
         Assert.Contains("SN-API-REPLACE-ME", help);
+        Assert.DoesNotContain("softwareVersion", help);
+        Assert.DoesNotContain("passport snapshot", help);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs b/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
@@ -3,43 +3,14 @@ namespace BatteryPassWeb.Tests;
 public sealed class ExternalApiBatteryIdTests
 {
     [Fact]
-    public void ExternalApi_ShouldUseBatteryIdForBatteryOperations()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
-
-        Assert.Contains("[HttpPost(\"batteries/{batteryId}/telemetry\")]", source);
-        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/battery-model\")]", source);
-        Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports\")]", source);
-        Assert.Contains("RejectPassportIdForBatteryRouteAsync", source);
-        Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/battery-version\")]", source);
-    }
-
-    [Fact]
-    public void ExternalApi_ShouldUsePassportIdForTrustOperationsAndPublish()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
-        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
-        var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
-
-        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", source);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/sign\")]", source);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/publish\")]", source);
-        Assert.Contains("PublishAsync", source);
-        Assert.Contains("PublishAsync", workflow);
-        Assert.Contains("Passport published.", workflow);
-        Assert.Contains("ValidateSignPublish", repository);
-        Assert.DoesNotContain("external-api.publish\",\r\n            cancellationToken);", source);
-    }
-
-    [Fact]
-    public void ExternalApiHelp_ShouldDocumentBatteryIdAndBatteryModel()
+    public void ExternalApiHelp_ShouldDocumentBatteryIdsAndNoneOfTheOldBatteryCalls()
     {
         var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
 
         Assert.Contains("Battery ID", help);
-        Assert.Contains("Battery Model", help);
-        Assert.Contains("/battery-model", help);
-        Assert.Contains("/passports/{passportId}/publish", help);
+        Assert.Contains("/batteries/{id}/publish", help);
+        Assert.DoesNotContain("/battery-model", help);
+        Assert.DoesNotContain("/passports/{passportId}", help);
         Assert.DoesNotContain("Battery version", help);
         Assert.DoesNotContain("/battery-version", help);
     }
diff --git a/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs b/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs
@@ -2,38 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class ExternalApiBatteryVersionTests
 {
-    [Fact]
-    public void ExternalApiController_ShouldExposeBatteryVersionEndpointRequiringNewTrustFlow()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
-
-        Assert.Contains("BatteryRepository", source);
-        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/battery-model\")]", source);
-        Assert.Contains("UpdateBatteryModel", source);
-        Assert.Contains("batteryModel", source);
-        Assert.Contains("newPassportRequired =", source);
-        Assert.Contains("BatteryTemplateUpdateService", source);
-        Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports\")]", source);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", source);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/sign\")]", source);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/publish\")]", source);
-        Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/battery-version\")]", source);
-        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/software-version\")]", source);
-        Assert.Contains("softwareVersion is required", source);
-    }
-
-    [Fact]
-    public void ExternalApi_ShouldRejectUndefinedBatteryModelAndExposeSoftwareVersionEndpoint()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
-        var updateService = File.ReadAllText(RepoFile("web", "Services", "BatteryTemplateUpdateService.cs"));
-
-        Assert.Contains("UpdateBatteryModel", controller);
-        Assert.Contains("Unknown Battery Model", controller);
-        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/software-version\")]", controller);
-        Assert.Contains("ApplyBatteryModelAsync", updateService);
-        Assert.Contains("ApplySoftwareVersionAsync", updateService);
-    }
 
     [Fact]
     public void BatteryTemplateUpdateService_ShouldCopySoftwareReleaseMetadata()
@@ -63,8 +31,8 @@ public sealed class ExternalApiBatteryVersionTests
     {
         var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
 
-        Assert.Contains("/software-version", markup);
-        Assert.Contains("Battery ID, family, and software parameters", markup);
+        Assert.DoesNotContain("/software-version", markup);
+        Assert.DoesNotContain("software parameters", markup);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/ExternalApiInitializerTests.cs b/BatteryPassWeb.Tests/ExternalApiInitializerTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiInitializerTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiInitializerTests.cs
@@ -90,7 +90,6 @@ public sealed class ExternalApiInitializerTests
         Assert.DoesNotContain("Sample create, validate, sign and publish passport token", helpView);
         Assert.Contains("const sampleLifecycleToken", helpView);
         Assert.Contains("'sample-lifecycle': canUsePrivilegedDemoTokens ? sampleLifecycleToken : ''", helpView);
-        Assert.Contains("tokenPreset: 'sample-lifecycle'", helpView);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
--- a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
+++ b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
@@ -68,12 +68,11 @@ public sealed class HelpWorkbenchLayoutTests
         var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
 
         Assert.Contains("id=\"tester-battery-id\"", markup);
-        Assert.Contains("id=\"tester-passport-id\"", markup);
+        Assert.DoesNotContain("id=\"tester-passport-id\"", markup);
         Assert.Contains("id=\"tester-token\"", markup);
         Assert.Contains("const getWorkbenchContext", markup);
         Assert.Contains("resolveTemplatePath(template.pathTemplate", markup);
         Assert.Contains("batteryIdEl?.addEventListener", markup);
-        Assert.Contains("passportIdEl?.addEventListener", markup);
         Assert.Contains("tokenPresetEl?.addEventListener('change'", markup);
         Assert.DoesNotContain("tokenPresetEl.value = template.tokenPreset;", markup);
     }
@@ -92,15 +91,9 @@ public sealed class HelpWorkbenchLayoutTests
         Assert.Contains("bp-help-sticky-id-field", workbenchMarkup);
         Assert.Contains("bp-help-sticky-id-control", workbenchMarkup);
         Assert.Contains("id=\"tester-battery-id\"", workbenchMarkup);
-        Assert.Contains("id=\"tester-passport-id\"", workbenchMarkup);
-        Assert.DoesNotContain("id=\"tester-battery-id\" class=\"bp-id-value\" value=\"@Model.SampleBatteryId\" readonly", workbenchMarkup);
-        Assert.DoesNotContain("id=\"tester-passport-id\" class=\"bp-id-value\" value=\"@Model.SamplePassportId\" readonly", workbenchMarkup);
+        Assert.DoesNotContain("id=\"tester-battery-id\" class=\"bp-id-value\" value=\"@Model.ExampleBatteryId\" readonly", workbenchMarkup);
         Assert.Contains("data-copy-target=\"#tester-battery-id\"", workbenchMarkup);
-        Assert.Contains("data-copy-target=\"#tester-passport-id\"", workbenchMarkup);
         Assert.Contains("Copy Battery ID", workbenchMarkup);
-        Assert.Contains("Copy Passport ID", workbenchMarkup);
-        Assert.DoesNotContain("Html.PartialAsync(\"_CopyIdButton\", Model.SampleBatteryId)", workbenchMarkup);
-        Assert.DoesNotContain("Html.PartialAsync(\"_CopyIdButton\", Model.SamplePassportId)", workbenchMarkup);
 
         Assert.Contains(".bp-help-sticky-id-field", css);
         Assert.Contains(".bp-help-sticky-id-control", css);
@@ -164,14 +157,16 @@ public sealed class HelpWorkbenchLayoutTests
     }
 
     [Fact]
-    public void HelpPage_ShouldOfferAPublishedBatteryForTheReadingCalls()
+    public void HelpPage_ShouldOfferAPublishedBatteryForTheCalls()
     {
         var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
         var controller = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));
 
-        Assert.Contains("Example battery ID for reading", markup);
+        Assert.Contains("Example battery ID", markup);
         Assert.Contains("No battery passport is published yet.", markup);
-        Assert.Contains("value=\"@(string.IsNullOrWhiteSpace(Model.ExampleBatteryId) ? Model.SampleBatteryId : Model.ExampleBatteryId)\"", markup);
+        Assert.Contains("id=\"tester-battery-id\" class=\"bp-id-value\" value=\"@Model.ExampleBatteryId\"", markup);
+        Assert.DoesNotContain("SampleBatteryId", markup);
+        Assert.DoesNotContain("SampleBatteryId", controller);
         Assert.Contains("Rows.FirstOrDefault(row => !row.IsDraft)?.Identifier", controller);
         Assert.Contains("BatteryPassWeb.Dpp.Api.PassportApi.Sections", markup);
         Assert.DoesNotContain("<code>materialComposition</code>", markup);
@@ -190,7 +185,7 @@ public sealed class HelpWorkbenchLayoutTests
     public void HelpWorkbench_ShouldExposeRunnableTemplateForEveryExternalApiEndpoint()
     {
         var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
+        var controller = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
         var initializer = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));
         var helpController = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));
         var helpModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ExternalApiHelpViewModel.cs"));
@@ -214,15 +209,13 @@ public sealed class HelpWorkbenchLayoutTests
             "readSection",
             "readValuesAliases",
             "readPathsAll",
-            "writeTelemetryPoint",
-            "readHistory24h",
-            "patchOperations",
-            "patchBatteryModel",
-            "patchSoftwareVersion",
-            "createPassport",
-            "validatePassport",
-            "signPassport",
-            "publishPassport"
+            "writeLiveValues",
+            "previewLiveValues",
+            "readLiveHistory24h",
+            "listBatches",
+            "listOperators",
+            "createBattery",
+            "publishBattery"
         };
         foreach (var templateKey in workbenchTemplateKeys)
         {
@@ -234,7 +227,6 @@ public sealed class HelpWorkbenchLayoutTests
         Assert.Contains("SampleLifecycleToken", helpModel);
         Assert.Contains("const sampleLifecycleToken", markup);
         Assert.Contains("'sample-lifecycle': canUsePrivilegedDemoTokens ? sampleLifecycleToken : ''", markup);
-        Assert.Contains("tokenPreset: 'sample-lifecycle'", markup);
         Assert.Contains("SAMPLEBATTERYPASSPORTLIFECYC001", initializer);
     }
 
@@ -274,18 +266,6 @@ public sealed class HelpWorkbenchLayoutTests
         Assert.Contains("/paths?section=performanceAndDurability", markup);
     }
 
-    [Fact]
-    public void HelpController_ShouldResolveSampleBatteryCompatibleWithSampleReadToken()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));
-
-        Assert.Contains("PassportRepository", source);
-        Assert.Contains("ResolveSampleIdsAsync", source);
-        Assert.Contains("SampleBatteryId", source);
-        Assert.Contains("BsonHelpers.GetString(passport, \"batteryId\")", source);
-        Assert.DoesNotContain("SamplePassportId = ExternalApiInitializer.SamplePassportId,", source);
-    }
-
     [Fact]
     public void HelpCss_ShouldKeepDocumentationAndWorkbenchFullWidth()
     {
diff --git a/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs b/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
--- a/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
+++ b/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
@@ -55,14 +55,8 @@ public sealed class Phase6ADocumentationTests
     [Fact]
     public void Guidance_ShouldMatchCurrentProductTemplateSoftwareAndUiWorkflow()
     {
-        var apiHelp = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
         var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
 
-        Assert.Contains("software parameters", apiHelp);
-        Assert.Contains("Battery Family baseline", apiHelp);
-        Assert.Contains("software-version endpoint", apiHelp);
-        Assert.Contains("Battery Model and software changes may require a new passport", apiHelp);
-
         Assert.Contains("software parameters", guide);
         Assert.Contains("Detailed report tabs", guide);
         Assert.Contains("software version parameter", guide);
diff --git a/BatteryPassWeb.Tests/SecurityLimitsTests.cs b/BatteryPassWeb.Tests/SecurityLimitsTests.cs
--- a/BatteryPassWeb.Tests/SecurityLimitsTests.cs
+++ b/BatteryPassWeb.Tests/SecurityLimitsTests.cs
@@ -1,7 +1,3 @@
-using System.Reflection;
-using System.Text.Json;
-using BatteryPassWeb.Controllers;
-
 namespace BatteryPassWeb.Tests;
 
 public sealed class SecurityLimitsTests
@@ -25,7 +21,6 @@ public sealed class SecurityLimitsTests
         Assert.Contains("FileUploadRateLimitPerWindow", options);
         Assert.Contains("MaxAuthorizationHeaderBytes", options);
         Assert.Contains("MaxJsonBodyBytes", options);
-        Assert.Contains("MaxTelemetryPoints", options);
         Assert.Contains("MaxUploadBytes", options);
 
         Assert.Contains("Login", policyNames);
@@ -46,7 +41,6 @@ public sealed class SecurityLimitsTests
         Assert.Contains("\"EnableRateLimiting\": true", developmentSettings);
         Assert.Contains("ENABLE_RATE_LIMITING=true", envExample);
         Assert.Contains("MAX_JSON_BODY_BYTES=1048576", envExample);
-        Assert.Contains("MAX_TELEMETRY_POINTS=250", envExample);
         Assert.Contains("MAX_UPLOAD_BYTES=10485760", envExample);
     }
 
@@ -55,7 +49,7 @@ public sealed class SecurityLimitsTests
     {
         var loginController = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
         var authApiController = File.ReadAllText(RepoFile("web", "Controllers", "AuthApiController.cs"));
-        var externalApiController = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
+        var externalApiController = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
         var filesApiController = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));
 
         Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.Login)", loginController);
@@ -66,30 +60,6 @@ public sealed class SecurityLimitsTests
         Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.FileUpload)", filesApiController);
     }
 
-    [Fact]
-    public void TelemetryParser_ShouldRejectPayloadsAboveConfiguredPointLimit()
-    {
-        var method = typeof(ExternalApiController).GetMethod(
-            "TryParseTelemetryPointsWithinLimit",
-            BindingFlags.NonPublic | BindingFlags.Static);
-
-        Assert.NotNull(method);
-
-        using var document = JsonDocument.Parse("""
-        [
-          { "measuredAt": "2026-05-20T09:00:00Z", "currentVoltageV": 398.4 },
-          { "measuredAt": "2026-05-20T09:01:00Z", "currentVoltageV": 399.1 },
-          { "measuredAt": "2026-05-20T09:02:00Z", "currentVoltageV": 399.7 }
-        ]
-        """);
-
-        object?[] args = [document.RootElement, 2, null!, ""];
-        var success = (bool)method!.Invoke(null, args)!;
-
-        Assert.False(success);
-        Assert.Contains("maximum of 2", args[3]!.ToString(), StringComparison.OrdinalIgnoreCase);
-    }
-
     [Fact]
     public void FileUpload_ShouldCheckConfiguredFileSizeBeforeBuffering()
     {
diff --git a/BatteryPassWeb.Tests/TrustDirtyStateTests.cs b/BatteryPassWeb.Tests/TrustDirtyStateTests.cs
--- a/BatteryPassWeb.Tests/TrustDirtyStateTests.cs
+++ b/BatteryPassWeb.Tests/TrustDirtyStateTests.cs
@@ -13,31 +13,6 @@ public sealed class TrustDirtyStateTests
         Assert.Contains("TrustState.Dirty", source);
     }
 
-    [Fact]
-    public void ExternalApiController_ShouldWriteBatteryModelChangesToBatteryRecord()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
-
-        Assert.Contains("UpdateBatteryModel", source);
-        Assert.Contains("BatteryTemplateUpdateService", source);
-        Assert.Contains("ApplyBatteryModelAsync", source);
-        Assert.Contains("newPassportRequired =", source);
-        Assert.Contains("CreateBatteryPassport", source);
-    }
-
-    [Fact]
-    public void ExternalApiAcceptance_ShouldOnlyWriteOperationalFieldsWithoutCallingDirtyMarker()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
-
-        Assert.Contains("app.operations.latestTelemetry", controller);
-        Assert.Contains("app.operations.locationOfUse", controller);
-        Assert.Contains("app.operations.contactPerson", controller);
-        Assert.Contains("UpdateBatteryModel", controller);
-        Assert.DoesNotContain("MarkCanonicalDirtyAsync", controller);
-        Assert.DoesNotContain("trust.isDirty", controller);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
```

`BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The external API's writing endpoints (step 3c-2), called with real tokens of each kind.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppApiWritingTests(MongoFixture mongo)
{
    [Fact]
    public void TheWritingEndpoints_ShouldReplaceTheOldControllerWithTheirLimits()
    {
        var type = typeof(DppApiController);
        var posts = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(action => action.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToDictionary(action => action.GetCustomAttribute<HttpPostAttribute>()!.Template!, action => action.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);

        Assert.Equal(
            new Dictionary<string, string?>
            {
                ["batteries"] = SecurityRateLimitPolicyNames.ExternalApiWrite,
                ["batteries/{id}/live-values"] = SecurityRateLimitPolicyNames.ExternalApiWrite,
                ["batteries/{id}/publish"] = SecurityRateLimitPolicyNames.ExternalApiLifecycle,
            },
            posts);
        Assert.Null(type.Assembly.GetType("BatteryPassWeb.Controllers.ExternalApiController"));
        Assert.Null(type.Assembly.GetType("BatteryPassWeb.Services.BatteryCreationService"));
        Assert.Null(type.Assembly.GetType("BatteryPassWeb.Services.BatteryTelemetryRepository"));
    }

    [Fact]
    public async Task ABatterySystem_ShouldWriteItsClustersLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var answer = await Write(rig, tokens["north-write"], Serial(rig), """{ "measuredAt": "2026-10-29T07:30:00+01:00", "values": { "stateOfChargeSoC": 64, "batteryStatus": "waste" } }""");

        var data = Data(answer, StatusCodes.Status200OK);
        Assert.Equal(PassportPagesTests.Identifier(rig), data.GetProperty("batteryId").GetString());
        Assert.True(data.GetProperty("saved").GetBoolean());
        Assert.Equal("2026-10-29T06:30:00Z", data.GetProperty("measuredAt").GetString());
        var values = data.GetProperty("values").EnumerateArray().ToArray();
        Assert.Equal(["saved", "rejected"], values.Select(value => value.GetProperty("outcome").GetString()));
        Assert.Equal("#22", values[1].GetProperty("attribute").GetString());
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"];
        Assert.Equal(64, stored.Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(new DateTime(2026, 10, 29, 6, 30, 0, DateTimeKind.Utc), stored.GeneratedAtUtc);
    }

    [Theory]
    [InlineData("north-read", StatusCodes.Status403Forbidden, "Token does not have write access.")]
    [InlineData("south-write", StatusCodes.Status403Forbidden, DppApiController.NotYours)]
    [InlineData(null, StatusCodes.Status401Unauthorized, null)]
    public async Task OtherTokens_ShouldChangeNothing(string? token, int status, string? message)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await Write(rig, token is null ? null : tokens[token], Serial(rig), """{ "values": { "stateOfChargeSoC": 10 } }"""));

        Assert.Equal(status, answer.StatusCode);
        if (message is not null)
        {
            Assert.Equal(message, DppApiControllerTests.Json(answer).GetProperty("message").GetString());
        }

        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task ADraft_ShouldTakeLiveValuesFromTheTeamOnly()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var cluster = Assert.IsType<ObjectResult>(await Write(rig, tokens["north-write"], Serial(rig), """{ "values": { "temperatureInformation": 22 } }"""));
        var team = await Write(rig, tokens["global-write"], Serial(rig), """{ "values": { "temperatureInformation": 22 } }""");

        Assert.Equal(StatusCodes.Status404NotFound, cluster.StatusCode);
        Assert.Equal(DppApiController.NoBattery, DppApiControllerTests.Json(cluster).GetProperty("message").GetString());
        Assert.True(Data(team, StatusCodes.Status200OK).GetProperty("saved").GetBoolean());
    }

    [Theory]
    [InlineData("""{ "points": [ { "measuredAt": "2026-10-29T07:00:00Z", "currentChargeLevelPct": 64 } ] }""", DppApiController.LiveValuesBody)]
    [InlineData("""{ "values": {} }""", DppApiController.LiveValuesBody)]
    [InlineData("""{ "measuredAt": "yesterday", "values": { "stateOfChargeSoC": 64 } }""", DppApiController.LiveValuesBody)]
    [InlineData("""{ "measuredAt": "2026-10-29T09:00:00Z", "values": { "stateOfChargeSoC": 64 } }""", "measuredAt lies in the future.")]
    public async Task ABodyThatIsNotOneMeasurement_ShouldBeRefused(string body, string message)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await Write(rig, tokens["north-write"], Serial(rig), body));

        Assert.Equal(StatusCodes.Status400BadRequest, answer.StatusCode);
        Assert.Equal(message, DppApiControllerTests.Json(answer).GetProperty("message").GetString());
    }

    [Fact]
    public async Task NothingToSave_ShouldBeUnprocessableAndAPreviewShouldSaveNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var refused = Assert.IsType<ObjectResult>(await Write(rig, tokens["north-write"], Serial(rig), """{ "values": { "batteryMass": 500, "stateOfChargeSoC": 140 } }"""));
        var preview = await Write(rig, tokens["north-write"], Serial(rig), """{ "values": { "stateOfChargeSoC": 12 } }""", preview: true);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.StatusCode);
        var data = Data(preview, StatusCodes.Status200OK);
        Assert.True(data.GetProperty("preview").GetBoolean());
        Assert.False(data.GetProperty("saved").GetBoolean());
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task TheHistory_ShouldBeCutToTheToken()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        await Write(rig, tokens["north-write"], Serial(rig), """{ "measuredAt": "2026-10-29T07:00:00Z", "values": { "stateOfChargeSoC": 64, "timeSpentInExtremeTemperaturesBelowBoundary": 30 } }""");

        var own = Data(await DppApiControllerTests.Controller(rig, tokens["north-read"]).LiveValueHistory(Serial(rig), 24, CancellationToken.None), StatusCodes.Status200OK);
        var other = Data(await DppApiControllerTests.Controller(rig, tokens["south-write"]).LiveValueHistory(Serial(rig), null, CancellationToken.None), StatusCodes.Status200OK);
        var tooLong = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens["north-read"]).LiveValueHistory(Serial(rig), 169, CancellationToken.None));

        var values = own.GetProperty("values").EnumerateArray().ToArray();
        Assert.Equal(["#63", "E2"], values.Select(value => value.GetProperty("attribute").GetString()).Order());
        Assert.All(values, value => Assert.Equal("2026-10-29T07:00:00Z", value.GetProperty("measuredAt").GetString()));
        Assert.All(values, value => Assert.Equal("battery system", value.GetProperty("source").GetString()));
        Assert.Equal(AttributeCatalog.Extensions[1].Unit, values.Single(value => value.GetProperty("attribute").GetString() == "E2").GetProperty("unit").GetString());
        Assert.Equal(168, other.GetProperty("hours").GetInt32());
        Assert.Empty(other.GetProperty("values").EnumerateArray());
        Assert.Equal(StatusCodes.Status400BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task ATeamToken_ShouldCreateADraftBattery()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        var controller = DppApiControllerTests.Controller(rig, tokens["global-write"]);
        var batch = Data(await controller.Batches(CancellationToken.None), StatusCodes.Status200OK).GetProperty("batches")[0];
        var economicOperator = Data(await controller.Operators(CancellationToken.None), StatusCodes.Status200OK).GetProperty("operators")[0];

        var body = Body($$"""{ "batchId": "{{batch.GetProperty("batchId").GetString()}}", "operatorId": "{{economicOperator.GetProperty("operatorId").GetString()}}", "clusterId": "{{BatteryTesting.South}}", "values": { "batterySerialNumber": "VPC9-5001" } }""");
        var created = Data(await controller.CreateBattery(body, CancellationToken.None), StatusCodes.Status201Created);

        Assert.Equal("VPC9-5001", created.GetProperty("serialNumber").GetString());
        Assert.Equal("draft", created.GetProperty("dppStatus").GetString());
        Assert.Equal(BatteryTesting.South, created.GetProperty("clusterId").GetString());
        Assert.EndsWith("/publish", created.GetProperty("publishPath").GetString());
        Assert.NotNull(await rig.Store.FindPackByUniqueProductIdentifierAsync(created.GetProperty("batteryId").GetString()!));
    }

    [Theory]
    [InlineData("north-lifecycle", StatusCodes.Status403Forbidden, DppApiController.TeamOnlyCreate)]
    [InlineData("global-read", StatusCodes.Status403Forbidden, "Token does not have write access.")]
    [InlineData("global-write", StatusCodes.Status400BadRequest, "The battery was not created: the battery serial number (#11) is required.")]
    public async Task ABatteryThatCannotBeCreated_ShouldSayWhy(string token, int status, string message)
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        var body = Body($$"""{ "batchId": "{{rig.Seed.Sources.Batch.Id}}", "operatorId": "{{rig.Seed.Sources.Operator.Id}}", "values": { "manufacturingDate": "2026-09" } }""");

        var answer = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens[token]).CreateBattery(body, CancellationToken.None));

        Assert.Equal(status, answer.StatusCode);
        Assert.Equal(message, DppApiControllerTests.Json(answer).GetProperty("message").GetString());
    }

    [Fact]
    public async Task ATeamToken_ShouldPublishAndRepublishOnlyWhatChanged()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        var controller = DppApiControllerTests.Controller(rig, tokens["global-lifecycle"]);

        var first = Data(await controller.Publish(Serial(rig), CancellationToken.None), StatusCodes.Status200OK);
        var again = Data(await controller.Publish(PassportPagesTests.Identifier(rig), CancellationToken.None), StatusCodes.Status200OK);

        Assert.Equal(1, first.GetProperty("version").GetInt32());
        Assert.Equal("active", first.GetProperty("dppStatus").GetString());
        Assert.False(first.GetProperty("unchanged").GetBoolean());
        Assert.True(again.GetProperty("unchanged").GetBoolean());
        Assert.Equal(first.GetProperty("passportId").GetString(), again.GetProperty("passportId").GetString());
    }

    [Theory]
    [InlineData("north-lifecycle", DppApiController.TeamOnlyPublish)]
    [InlineData("global-write", "Token does not have sign access.")]
    public async Task OtherTokens_ShouldNotPublish(string token, string message)
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens[token]).Publish(Serial(rig), CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, answer.StatusCode);
        Assert.Equal(message, DppApiControllerTests.Json(answer).GetProperty("message").GetString());
        Assert.Null(await rig.Store.GetActiveVersionAsync(rig.PackId));
    }

    [Fact]
    public async Task ABatteryThatCannotBePublished_ShouldListWhatBlocksIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), new[] { "stateOfChargeSoC" });
        var tokens = await TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens["global-lifecycle"]).Publish(Serial(rig), CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, answer.StatusCode);
        var blocking = DppApiControllerTests.Json(answer).GetProperty("data").GetProperty("blocking").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(blocking, item => item!.Contains("#63", StringComparison.Ordinal));
    }

    /// <summary>Tokens of each access mode for the seed pack's cluster (north), another cluster (south) and the team (global).</summary>
    private static async Task<Dictionary<string, string>> TokensAsync(PublicationRig rig)
    {
        var repository = DppApiControllerTests.Repository(rig);
        var tokens = new Dictionary<string, string>();
        foreach (var (name, mode, clusters, global) in new (string, ExternalTokenAccessMode, string[], bool)[]
        {
            ("north-read", ExternalTokenAccessMode.Read, [BatteryTesting.North], false),
            ("north-write", ExternalTokenAccessMode.ReadWrite, [BatteryTesting.North], false),
            ("north-lifecycle", ExternalTokenAccessMode.Lifecycle, [BatteryTesting.North], false),
            ("south-write", ExternalTokenAccessMode.ReadWrite, [BatteryTesting.South], false),
            ("global-read", ExternalTokenAccessMode.Read, [], true),
            ("global-write", ExternalTokenAccessMode.ReadWrite, [], true),
            ("global-lifecycle", ExternalTokenAccessMode.Lifecycle, [], true),
        })
        {
            tokens[name] = (await repository.CreateTokenAsync(name, mode, clusters, false, global, "test")).PlainToken;
        }

        return tokens;
    }

    private static Task<IActionResult> Write(PublicationRig rig, string? token, string id, string body, bool preview = false) =>
        DppApiControllerTests.Controller(rig, token).WriteLiveValues(id, Body(body), preview, CancellationToken.None);

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonElement Data(IActionResult result, int status)
    {
        var answer = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, answer.StatusCode);
        return DppApiControllerTests.Json(answer).GetProperty("data");
    }

    private static string Serial(PublicationRig rig) => rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value!.GetValue<string>();
}
```

```bash
git rm -q BatteryPassWeb.Tests/BatteryCreationServiceTests.cs BatteryPassWeb.Tests/ExternalApiTelemetryTemplateTests.cs
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AntiForgeryProtectionTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryAdminWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryCreationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.BatterySnapshotWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppApiControllerTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppApiWritingTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryCreateTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryIdTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryVersionTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiInitializerTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiTelemetryTemplateTests|FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Phase6ADocumentationTests|FullyQualifiedName~BatteryPassWeb.Tests.SecurityLimitsTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustDirtyStateTests"`
Expected: the build fails because `DppApiController` doesn't have the new messages yet: `LiveValuesBody`, `NotYours`, `TeamOnlyCreate`, `TeamOnlyPublish` (CS0117). The compiler stops there, before the missing endpoints and the controller's new constructor.

- [ ] **Step 3: Add the writing endpoints and remove the old writing side**

Patch `3c2-task2-code.patch` (`web/.env.example`, `web/Configuration/BatteryPassOptions.cs`, `web/Controllers/DppApiController.cs`, `web/Controllers/HelpController.cs`, `web/Models/ViewModels/ExternalApiHelpViewModel.cs`, `web/Program.cs`, `web/Services/ExternalApiInitializer.cs`, `web/Views/Help/Index.cshtml`, `web/appsettings.Development.json`, `web/appsettings.json`). Save the block as `../3c2-task2-code.patch`, outside the worktree, and run `git apply ../3c2-task2-code.patch` from the worktree root:

```diff
diff --git a/web/.env.example b/web/.env.example
--- a/web/.env.example
+++ b/web/.env.example
@@ -29,7 +29,6 @@ EXTERNAL_API_LIFECYCLE_RATE_LIMIT_PER_WINDOW=10
 FILE_UPLOAD_RATE_LIMIT_PER_WINDOW=10
 MAX_AUTHORIZATION_HEADER_BYTES=4096
 MAX_JSON_BODY_BYTES=1048576
-MAX_TELEMETRY_POINTS=250
 MAX_UPLOAD_BYTES=10485760
 POWER_AUTOMATE_RESET_WEBHOOK_URL=https://replace-with-your-power-automate-http-trigger-url
 POWER_AUTOMATE_RESET_WEBHOOK_SECRET=replace-with-a-long-random-shared-secret
diff --git a/web/Configuration/BatteryPassOptions.cs b/web/Configuration/BatteryPassOptions.cs
--- a/web/Configuration/BatteryPassOptions.cs
+++ b/web/Configuration/BatteryPassOptions.cs
@@ -14,7 +14,6 @@ public sealed class BatteryPassOptions
     public const int DefaultFileUploadRateLimitPerWindow = 10;
     public const int DefaultMaxAuthorizationHeaderBytes = 4096;
     public const long DefaultMaxJsonBodyBytes = 1_048_576;
-    public const int DefaultMaxTelemetryPoints = 250;
     public const long DefaultMaxUploadBytes = 10_485_760;
 
     public string MongoDbUri { get; set; } = string.Empty;
@@ -43,7 +42,6 @@ public sealed class BatteryPassOptions
     public int FileUploadRateLimitPerWindow { get; set; } = DefaultFileUploadRateLimitPerWindow;
     public int MaxAuthorizationHeaderBytes { get; set; } = DefaultMaxAuthorizationHeaderBytes;
     public long MaxJsonBodyBytes { get; set; } = DefaultMaxJsonBodyBytes;
-    public int MaxTelemetryPoints { get; set; } = DefaultMaxTelemetryPoints;
     public long MaxUploadBytes { get; set; } = DefaultMaxUploadBytes;
 
     public static int NormalizeSessionTimeoutMinutes(int minutes)
diff --git a/web/Controllers/DppApiController.cs b/web/Controllers/DppApiController.cs
--- a/web/Controllers/DppApiController.cs
+++ b/web/Controllers/DppApiController.cs
@@ -1,6 +1,11 @@
+using System.Globalization;
+using System.Text.Json;
+using System.Text.Json.Nodes;
 using BatteryPassWeb.Configuration;
 using BatteryPassWeb.Dpp.Access;
 using BatteryPassWeb.Dpp.Api;
+using BatteryPassWeb.Dpp.Catalog;
+using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Publication;
 using BatteryPassWeb.Services;
@@ -11,15 +16,23 @@ using Microsoft.AspNetCore.RateLimiting;
 namespace BatteryPassWeb.Controllers;
 
 /// <summary>
-/// The external API's reading side on the new model (decision 29, step 3c-1). A token is read as a reader
-/// (<see cref="DppReaders.For"/>): a cluster token like a user of its clusters, a global token like the team. Every
+/// The external API on the new model (decision 29, steps 3c-1 and 3c-2). A token reads as a reader
+/// (<see cref="DppReaders.For"/>) and writes as a sender (<see cref="DppReaders.SenderFor"/>): a cluster token like a user
+/// of its clusters and as a battery system that feeds their live values (J-10), a global token like the team. Every
 /// passport answer is cut by the same rule as the pages, so a battery outside the token's clusters gives public data.
 /// </summary>
 [ApiController]
 [Route("api/external/v1")]
-public sealed class DppApiController(ExternalApiRepository tokens, ClusterRepository clusters, PassportApi api) : ControllerBase
+public sealed class DppApiController(ExternalApiRepository tokens, ClusterRepository clusters, PassportApi api, BatteryApi writing) : ControllerBase
 {
     public const string NoPassport = "No published battery passport has this ID.";
+    public const string NoBattery = "No battery this token may change has this ID.";
+    public const string NotYours = "This token cannot change this battery: it belongs to another cluster.";
+    public const string Ambiguous = "More than one battery has this serial number; use the battery ID.";
+    public const string TeamOnlyCreate = "Only team tokens create batteries.";
+    public const string TeamOnlyPublish = "Only team tokens publish passports.";
+    public const string LiveValuesBody = "Send a JSON object with 'values' (live values by key, e.g. stateOfChargeSoC) and, optionally, 'measuredAt' (ISO 8601 with its offset, e.g. 2026-10-27T10:02:59Z).";
+    public const string CreateBody = "Send a JSON object with batchId, operatorId, optionally clusterId, and values (batterySerialNumber is required).";
 
     [HttpGet("clusters")]
     [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
@@ -225,20 +238,284 @@ public sealed class DppApiController(ExternalApiRepository tokens, ClusterReposi
         });
     }
 
-    /// <summary>The token's reader; on failure the answer to send (401, 403 or 503, as before).</summary>
-    private async Task<(Reader? Reader, IActionResult? Error)> ReaderAsync(CancellationToken cancellationToken)
+    [HttpGet("batteries/{id}/live-values/history")]
+    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
+    public async Task<IActionResult> LiveValueHistory(string id, [FromQuery] int? hours, CancellationToken cancellationToken)
+    {
+        var span = hours ?? BatteryApi.MaxHistoryHours;
+        if (span is < 1 or > BatteryApi.MaxHistoryHours)
+        {
+            return Envelope(StatusCodes.Status400BadRequest, $"hours must be between 1 and {BatteryApi.MaxHistoryHours}.");
+        }
+
+        var (reader, error) = await ReaderAsync(cancellationToken);
+        if (reader is null)
+        {
+            return error!;
+        }
+
+        var passport = await api.ReadAsync(reader, id, cancellationToken);
+        if (passport is null)
+        {
+            return Envelope(StatusCodes.Status404NotFound, NoPassport);
+        }
+
+        var history = await writing.HistoryAsync(reader, passport.Version.PackId, span, cancellationToken);
+        return Envelope(StatusCodes.Status200OK, "Live value history read successfully.", new
+        {
+            batteryId = passport.Location.Identifier,
+            hours = span,
+            values = history.Select(entry => new
+            {
+                attribute = entry.Attribute.Label,
+                key = entry.Attribute.Property,
+                name = entry.Attribute.Name,
+                unit = entry.Attribute.Unit,
+                value = entry.Value,
+                measuredAt = PassportApi.Timestamp(entry.MeasuredAtUtc),
+                savedAt = PassportApi.Timestamp(entry.SavedAtUtc),
+                source = PassportPages.SourceWord(entry.Source),
+            }).ToArray(),
+        });
+    }
+
+    [HttpPost("batteries/{id}/live-values")]
+    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)]
+    public async Task<IActionResult> WriteLiveValues(string id, [FromBody] JsonElement body, [FromQuery] bool preview, CancellationToken cancellationToken)
+    {
+        if (body.ValueKind != JsonValueKind.Object
+            || !body.TryGetProperty("values", out var valuesElement)
+            || valuesElement.ValueKind != JsonValueKind.Object
+            || !valuesElement.EnumerateObject().Any())
+        {
+            return Envelope(StatusCodes.Status400BadRequest, LiveValuesBody);
+        }
+
+        DateTime? measuredAt = null;
+        if (body.TryGetProperty("measuredAt", out var measuredElement) && measuredElement.ValueKind != JsonValueKind.Null)
+        {
+            if (measuredElement.ValueKind != JsonValueKind.String
+                || !DateTimeOffset.TryParse(measuredElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment))
+            {
+                return Envelope(StatusCodes.Status400BadRequest, LiveValuesBody);
+            }
+
+            measuredAt = moment.UtcDateTime;
+            if (writing.InTheFuture(measuredAt.Value))
+            {
+                return Envelope(StatusCodes.Status400BadRequest, "measuredAt lies in the future.");
+            }
+        }
+
+        var (token, error) = await TokenAsync(ExternalTokenRequirement.Write, cancellationToken);
+        if (token is null)
+        {
+            return error!;
+        }
+
+        var sender = DppReaders.SenderFor(token);
+        var (target, refused) = await TargetAsync(sender, id, cancellationToken);
+        if (target is null)
+        {
+            return refused!;
+        }
+
+        var result = await writing.WriteLiveValuesAsync(sender, target.Pack!, Values(valuesElement), measuredAt, preview, cancellationToken);
+        var accepted = result.Keys.Any(key => key.Outcome == KeyOutcome.Saved);
+        return Envelope(
+            accepted ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity,
+            !accepted ? "No value was saved; see each key." : preview ? "Preview only: nothing was saved." : "Live values saved.",
+            new
+            {
+                batteryId = target.Identifier,
+                preview,
+                saved = result.Saved,
+                measuredAt = measuredAt is null ? null : PassportApi.Timestamp(measuredAt.Value),
+                values = Keys(result.Keys),
+            });
+    }
+
+    [HttpGet("batches")]
+    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
+    public async Task<IActionResult> Batches(CancellationToken cancellationToken)
+    {
+        var (token, error) = await TeamTokenAsync(ExternalTokenRequirement.Read, TeamOnlyCreate, cancellationToken);
+        if (token is null)
+        {
+            return error!;
+        }
+
+        var choices = await writing.ChoicesAsync(cancellationToken);
+        return Envelope(StatusCodes.Status200OK, "Batches read successfully.", new
+        {
+            batches = choices.Batches.Select(batch => new
+            {
+                batchId = batch.BatchId,
+                batteryFamily = batch.FamilyName,
+                batteryModel = batch.ModelName,
+                year = batch.Year,
+                manufacturingSite = batch.SiteName,
+            }).ToArray(),
+        });
+    }
+
+    [HttpGet("operators")]
+    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
+    public async Task<IActionResult> Operators(CancellationToken cancellationToken)
+    {
+        var (token, error) = await TeamTokenAsync(ExternalTokenRequirement.Read, TeamOnlyCreate, cancellationToken);
+        if (token is null)
+        {
+            return error!;
+        }
+
+        var choices = await writing.ChoicesAsync(cancellationToken);
+        return Envelope(StatusCodes.Status200OK, "Economic operators read successfully.", new
+        {
+            operators = choices.Operators.Select(item => new { operatorId = item.Id, name = item.Name }).ToArray(),
+        });
+    }
+
+    [HttpPost("batteries")]
+    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)]
+    public async Task<IActionResult> CreateBattery([FromBody] JsonElement body, CancellationToken cancellationToken)
+    {
+        var (token, error) = await TeamTokenAsync(ExternalTokenRequirement.Write, TeamOnlyCreate, cancellationToken);
+        if (token is null)
+        {
+            return error!;
+        }
+
+        if (body.ValueKind != JsonValueKind.Object)
+        {
+            return Envelope(StatusCodes.Status400BadRequest, CreateBody);
+        }
+
+        var values = body.TryGetProperty("values", out var valuesElement) && valuesElement.ValueKind == JsonValueKind.Object
+            ? Values(valuesElement)
+            : new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
+        var result = await writing.CreateAsync(DppReaders.SenderFor(token), Text(body, "batchId"), Text(body, "operatorId"), Text(body, "clusterId"), values, cancellationToken);
+        if (result.Pack is not { } pack)
+        {
+            return Envelope(StatusCodes.Status400BadRequest, $"The battery was not created: {result.Problem}.", new { values = Keys(result.Keys) });
+        }
+
+        var batteryId = pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>();
+        return Envelope(StatusCodes.Status201Created, "Draft battery created.", new
+        {
+            batteryId,
+            serialNumber = pack.Values["batterySerialNumber"].Value!.GetValue<string>(),
+            clusterId = pack.ClusterId,
+            dppStatus = "draft",
+            values = Keys(result.Keys),
+            publishPath = $"/api/external/v1/batteries/{Uri.EscapeDataString(batteryId)}/publish",
+        });
+    }
+
+    [HttpPost("batteries/{id}/publish")]
+    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiLifecycle)]
+    public async Task<IActionResult> Publish(string id, CancellationToken cancellationToken)
+    {
+        var (token, error) = await TeamTokenAsync(ExternalTokenRequirement.Sign, TeamOnlyPublish, cancellationToken);
+        if (token is null)
+        {
+            return error!;
+        }
+
+        var sender = DppReaders.SenderFor(token);
+        var (target, refused) = await TargetAsync(sender, id, cancellationToken);
+        if (target is null)
+        {
+            return refused!;
+        }
+
+        var result = await writing.PublishAsync(sender, target.Pack!, cancellationToken);
+        if (result.Version is not { } version)
+        {
+            return Envelope(StatusCodes.Status409Conflict, $"The passport was not published: {result.Problem}.", new
+            {
+                batteryId = target.Identifier,
+                blocking = result.Report?.Blocking ?? [],
+                warnings = result.Report?.Warnings ?? [],
+            });
+        }
+
+        return Envelope(StatusCodes.Status200OK, result.Unchanged ? "Nothing changed since the current version, which stays as it is." : $"Version {version.Number} published.", new
+        {
+            batteryId = target.Identifier,
+            passportId = version.DigitalProductPassportId,
+            version = version.Number,
+            dppStatus = version.DppStatus,
+            unchanged = result.Unchanged,
+            warnings = result.Report?.Warnings ?? [],
+        });
+    }
+
+    /// <summary>The token, when it has the access the call needs; on failure the answer to send (401, 403 or 503, as before).</summary>
+    private async Task<(ExternalApiTokenContext? Token, IActionResult? Error)> TokenAsync(ExternalTokenRequirement requirement, CancellationToken cancellationToken)
     {
         if (!tokens.IsAvailable)
         {
             return (null, Envelope(StatusCodes.Status503ServiceUnavailable, "Database is not connected."));
         }
 
-        var validation = await tokens.ValidateTokenAsync(ExternalApiCredentials.Token(Request), ExternalTokenRequirement.Read, cancellationToken);
+        var validation = await tokens.ValidateTokenAsync(ExternalApiCredentials.Token(Request), requirement, cancellationToken);
         return validation.Success && validation.Context is not null
-            ? (DppReaders.For(validation.Context), null)
+            ? (validation.Context, null)
             : (null, Envelope(validation.StatusCode, validation.Message));
     }
 
+    /// <summary>A global (team) token with the access the call needs: creating and publishing are the team's (design 6.1).</summary>
+    private async Task<(ExternalApiTokenContext? Token, IActionResult? Error)> TeamTokenAsync(ExternalTokenRequirement requirement, string refusal, CancellationToken cancellationToken)
+    {
+        var (token, error) = await TokenAsync(requirement, cancellationToken);
+        return token is null ? (null, error)
+            : token.GlobalAccess ? (token, null)
+            : (null, Envelope(StatusCodes.Status403Forbidden, refusal));
+    }
+
+    /// <summary>The battery a write names, for this sender; on failure the answer to send (404, 403 or 409).</summary>
+    private async Task<(WriteTarget? Target, IActionResult? Error)> TargetAsync(ChangeSender sender, string id, CancellationToken cancellationToken)
+    {
+        var target = await writing.FindAsync(sender, id, cancellationToken);
+        return target.Refusal switch
+        {
+            WriteRefusal.None => (target, null),
+            WriteRefusal.NotYours => (null, Envelope(StatusCodes.Status403Forbidden, NotYours)),
+            WriteRefusal.Ambiguous => (null, Envelope(StatusCodes.Status409Conflict, Ambiguous)),
+            _ => (null, Envelope(StatusCodes.Status404NotFound, NoBattery)),
+        };
+    }
+
+    /// <summary>The token's reader; on failure the answer to send (401, 403 or 503, as before).</summary>
+    private async Task<(Reader? Reader, IActionResult? Error)> ReaderAsync(CancellationToken cancellationToken)
+    {
+        var (token, error) = await TokenAsync(ExternalTokenRequirement.Read, cancellationToken);
+        return token is null ? (null, error) : (DppReaders.For(token), null);
+    }
+
+    /// <summary>What happened to each key, in the order sent: saved, kept (empty), ignored or rejected, and why.</summary>
+    private static object[] Keys(IEnumerable<KeyResult> keys) => keys.Select(key => (object)new
+    {
+        key = key.Key,
+        attribute = AttributeCatalog.FindByProperty(key.Key)?.Label,
+        outcome = key.Outcome switch
+        {
+            KeyOutcome.Saved => "saved",
+            KeyOutcome.KeptEmpty => "kept",
+            KeyOutcome.Ignored => "ignored",
+            KeyOutcome.Removed => "removed",
+            _ => "rejected",
+        },
+        message = string.IsNullOrEmpty(key.Message) ? null : key.Message,
+    }).ToArray();
+
+    private static Dictionary<string, JsonNode?> Values(JsonElement values) =>
+        values.EnumerateObject().ToDictionary(property => property.Name, property => JsonNode.Parse(property.Value.GetRawText()), StringComparer.Ordinal);
+
+    private static string? Text(JsonElement body, string name) =>
+        body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
+
     private async Task<(ApiPassport? Passport, IActionResult? Error)> PassportAsync(string id, CancellationToken cancellationToken)
     {
         var (reader, error) = await ReaderAsync(cancellationToken);
diff --git a/web/Controllers/HelpController.cs b/web/Controllers/HelpController.cs
--- a/web/Controllers/HelpController.cs
+++ b/web/Controllers/HelpController.cs
@@ -13,24 +13,18 @@ namespace BatteryPassWeb.Controllers;
 public class HelpController : Controller
 {
     private readonly ExternalApiRepository _externalApiRepository;
-    private readonly PassportRepository _passportRepository;
-    private readonly BatteryIdService _batteryIdService;
     private readonly AccessControlService _accessControlService;
     private readonly BatteryPassOptions _options;
     private readonly BatteryPages _batteryPages;
 
     public HelpController(
         ExternalApiRepository externalApiRepository,
-        PassportRepository passportRepository,
-        BatteryIdService batteryIdService,
         AccessControlService accessControlService,
         IOptions<BatteryPassOptions> options,
         BatteryPages batteryPages)
     {
         _batteryPages = batteryPages;
         _externalApiRepository = externalApiRepository;
-        _passportRepository = passportRepository;
-        _batteryIdService = batteryIdService;
         _accessControlService = accessControlService;
         _options = options.Value;
     }
@@ -42,16 +36,13 @@ public class HelpController : Controller
         var readTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadTokenId, cancellationToken);
         var readWriteTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadWriteTokenId, cancellationToken);
         var lifecycleTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleLifecycleTokenId, cancellationToken);
-        var sampleIds = await ResolveSampleIdsAsync(cancellationToken);
         var canUsePrivilegedDemoTokens = await CanUsePrivilegedDemoTokensAsync(cancellationToken);
 
         var model = new ExternalApiHelpViewModel
         {
             BasePath = "/api/external/v1",
             PublicBaseUrl = RequestBaseUrl(),
-            SampleBatteryId = sampleIds.BatteryId,
             ExampleBatteryId = (await _batteryPages.ListAsync(null, cancellationToken)).Rows.FirstOrDefault(row => !row.IsDraft)?.Identifier ?? string.Empty,
-            SamplePassportId = sampleIds.PassportId,
             SampleReadToken = _options.EnableDemoData && _options.EnablePublicDemoReadToken && readTokenDocument != null
                 ? ExternalApiInitializer.SampleReadTokenValue
                 : string.Empty,
@@ -101,30 +92,4 @@ public class HelpController : Controller
 
         return "Use an approved tester account: global admin or demo-cluster cluster admin.";
     }
-
-    private async Task<(string BatteryId, string PassportId)> ResolveSampleIdsAsync(CancellationToken cancellationToken)
-    {
-        var passports = await _passportRepository.SearchDocumentsAsync(string.Empty, includeArchived: false, cancellationToken);
-        var clusterPassport = passports
-            .Where(passport => BsonHelpers.GetString(passport, "clusterId").Equals(ExternalApiInitializer.SampleApiClusterId, StringComparison.OrdinalIgnoreCase))
-            .OrderByDescending(passport => passport.GetValue("isLatestForBattery", false).ToBoolean())
-            .ThenByDescending(passport => BsonHelpers.GetString(passport, "registryInfo", "updatedAt"))
-            .FirstOrDefault();
-        if (clusterPassport != null)
-        {
-            return ToSampleIds(clusterPassport);
-        }
-
-        var passport = passports.FirstOrDefault(passport => string.IsNullOrWhiteSpace(BsonHelpers.GetString(passport, "clusterId")));
-        return passport != null
-            ? ToSampleIds(passport)
-            : (ExternalApiInitializer.CreateSampleBatteryId(_batteryIdService), ExternalApiInitializer.SamplePassportId);
-    }
-
-    private static (string BatteryId, string PassportId) ToSampleIds(MongoDB.Bson.BsonDocument passport)
-    {
-        var passportId = BsonHelpers.GetString(passport, "passportId");
-        var batteryId = BsonHelpers.GetString(passport, "batteryId");
-        return (string.IsNullOrWhiteSpace(batteryId) ? passportId : batteryId, passportId);
-    }
 }
diff --git a/web/Models/ViewModels/ExternalApiHelpViewModel.cs b/web/Models/ViewModels/ExternalApiHelpViewModel.cs
--- a/web/Models/ViewModels/ExternalApiHelpViewModel.cs
+++ b/web/Models/ViewModels/ExternalApiHelpViewModel.cs
@@ -4,11 +4,9 @@ public sealed class ExternalApiHelpViewModel
 {
     public string BasePath { get; init; } = "/api/external/v1";
     public string PublicBaseUrl { get; init; } = string.Empty;
-    public string SampleBatteryId { get; init; } = string.Empty;
 
     /// <summary>A published battery's ID (#6), to try the reading calls on; empty when none is published yet.</summary>
     public string ExampleBatteryId { get; init; } = string.Empty;
-    public string SamplePassportId { get; init; } = string.Empty;
     public string SampleReadToken { get; init; } = string.Empty;
     public string SampleReadWriteToken { get; init; } = string.Empty;
     public string SampleLifecycleToken { get; init; } = string.Empty;
diff --git a/web/Program.cs b/web/Program.cs
--- a/web/Program.cs
+++ b/web/Program.cs
@@ -46,9 +46,6 @@ var maxAuthorizationHeaderBytes = ParsePositiveIntSetting(
 var maxJsonBodyBytes = ParsePositiveLongSetting(
     EnvironmentOrConfig(builder.Configuration, "MAX_JSON_BODY_BYTES", "MaxJsonBodyBytes"),
     BatteryPassOptions.DefaultMaxJsonBodyBytes);
-var maxTelemetryPoints = ParsePositiveIntSetting(
-    EnvironmentOrConfig(builder.Configuration, "MAX_TELEMETRY_POINTS", "MaxTelemetryPoints"),
-    BatteryPassOptions.DefaultMaxTelemetryPoints);
 var maxUploadBytes = ParsePositiveLongSetting(
     EnvironmentOrConfig(builder.Configuration, "MAX_UPLOAD_BYTES", "MaxUploadBytes"),
     BatteryPassOptions.DefaultMaxUploadBytes);
@@ -119,7 +116,6 @@ builder.Services.Configure<BatteryPassOptions>(options =>
     options.FileUploadRateLimitPerWindow = fileUploadRateLimitPerWindow;
     options.MaxAuthorizationHeaderBytes = maxAuthorizationHeaderBytes;
     options.MaxJsonBodyBytes = maxJsonBodyBytes;
-    options.MaxTelemetryPoints = maxTelemetryPoints;
     options.MaxUploadBytes = maxUploadBytes;
 });
 
@@ -236,7 +232,6 @@ builder.Services.AddSingleton<BatteryAuditService>();
 builder.Services.AddSingleton<ApplicationAuditService>();
 builder.Services.AddSingleton<ApplicationSettingsService>();
 builder.Services.AddSingleton<AuthenticationSessionService>();
-builder.Services.AddSingleton<BatteryCreationService>();
 builder.Services.AddSingleton<PassportTrustWorkflowService>();
 builder.Services.AddSingleton<AccessControlService>();
 builder.Services.AddSingleton<PassportQrCodeService>();
@@ -250,7 +245,6 @@ builder.Services.AddSingleton<AuthService>();
 builder.Services.AddHttpClient<IEmailSender, PowerAutomateEmailSender>();
 builder.Services.AddSingleton<ExternalApiSecurityService>();
 builder.Services.AddSingleton<ExternalApiRepository>();
-builder.Services.AddSingleton<BatteryTelemetryRepository>();
 builder.Services.AddSingleton<ExternalApiInitializer>();
 builder.Services.AddDpp();
 
diff --git a/web/Services/ExternalApiInitializer.cs b/web/Services/ExternalApiInitializer.cs
--- a/web/Services/ExternalApiInitializer.cs
+++ b/web/Services/ExternalApiInitializer.cs
@@ -33,7 +33,6 @@ public sealed class ExternalApiInitializer
     private readonly ExternalApiRepository _externalApiRepository;
     private readonly PassportRepository _passportRepository;
     private readonly BatteryRepository _batteryRepository;
-    private readonly BatteryTelemetryRepository _batteryTelemetryRepository;
     private readonly ClusterRepository _clusterRepository;
     private readonly PassportDataNormalizationService _passportDataNormalizationService;
     private readonly BatteryIdService _batteryIdService;
@@ -45,7 +44,6 @@ public sealed class ExternalApiInitializer
         ExternalApiRepository externalApiRepository,
         PassportRepository passportRepository,
         BatteryRepository batteryRepository,
-        BatteryTelemetryRepository batteryTelemetryRepository,
         ClusterRepository clusterRepository,
         PassportDataNormalizationService passportDataNormalizationService,
         BatteryIdService batteryIdService,
@@ -54,7 +52,6 @@ public sealed class ExternalApiInitializer
         _externalApiRepository = externalApiRepository;
         _passportRepository = passportRepository;
         _batteryRepository = batteryRepository;
-        _batteryTelemetryRepository = batteryTelemetryRepository;
         _clusterRepository = clusterRepository;
         _passportDataNormalizationService = passportDataNormalizationService;
         _batteryIdService = batteryIdService;
@@ -79,7 +76,6 @@ public sealed class ExternalApiInitializer
             await _externalApiRepository.EnsureIndexesAsync(cancellationToken);
             await _batteryRepository.EnsureIndexesAsync(cancellationToken);
             await _clusterRepository.EnsureIndexesAsync(cancellationToken);
-            await _batteryTelemetryRepository.EnsureIndexesAsync(cancellationToken);
             if (_options.EnableDemoData)
             {
                 await EnsureSamplePassportAsync(cancellationToken);
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -6,7 +6,7 @@
     <header class="bp-console-header">
         <div>
             <h1>API help</h1>
-            <p class="text-secondary mb-0">Read passport data, update operational values, and run token-protected trust actions.</p>
+            <p class="text-secondary mb-0">Read passports, send a battery's live values, and create and publish batteries with a team token.</p>
         </div>
     </header>
 
@@ -21,7 +21,7 @@
                 <code>@Model.BasePath</code>
             </div>
             <div>
-                <p class="mb-1 text-secondary small">Example battery ID for reading</p>
+                <p class="mb-1 text-secondary small">Example battery ID</p>
                 @if (!string.IsNullOrWhiteSpace(Model.ExampleBatteryId))
                 {
                     <span class="bp-id-copy-row"><code class="bp-id-value">@Model.ExampleBatteryId</code>@await Html.PartialAsync("_CopyIdButton", Model.ExampleBatteryId)</span>
@@ -31,14 +31,6 @@
                     <span class="text-secondary small">No battery passport is published yet.</span>
                 }
             </div>
-            <div>
-                <p class="mb-1 text-secondary small">Sample Battery ID for writing and passport actions</p>
-                <span class="bp-id-copy-row"><code class="bp-id-value">@Model.SampleBatteryId</code>@await Html.PartialAsync("_CopyIdButton", Model.SampleBatteryId)</span>
-            </div>
-            <div>
-                <p class="mb-1 text-secondary small">Sample Passport ID for trust actions</p>
-                <span class="bp-id-copy-row"><code class="bp-id-value">@Model.SamplePassportId</code>@await Html.PartialAsync("_CopyIdButton", Model.SamplePassportId)</span>
-            </div>
             <div>
                 <p class="mb-1 text-secondary small">Sample read token</p>
                 @if (!string.IsNullOrWhiteSpace(Model.SampleReadToken))
@@ -130,11 +122,33 @@
                                 </button>
                             </td>
                         </tr>
+                        <tr class="bp-help-doc-item">
+                            <td><span class="bp-help-method">GET</span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batches</code><small>Batches a new battery can be made in</small></span></td>
+                            <td><span class="bp-help-access-pill">Team, read</span></td>
+                            <td>Find the batch ID for a new battery: family, model, year and manufacturing site.</td>
+                            <td class="bp-action-cell">
+                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-batches" aria-expanded="false" aria-controls="api-detail-batches" aria-label="Show batches endpoint details" title="Show batches endpoint details" data-tooltip="Details">
+                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
+                                </button>
+                            </td>
+                        </tr>
+                        <tr class="bp-help-doc-item">
+                            <td><span class="bp-help-method">GET</span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/operators</code><small>Economic operators</small></span></td>
+                            <td><span class="bp-help-access-pill">Team, read</span></td>
+                            <td>Find the economic operator ID for a new battery.</td>
+                            <td class="bp-action-cell">
+                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-batches" aria-expanded="false" aria-controls="api-detail-batches" aria-label="Show operators endpoint details" title="Show operators endpoint details" data-tooltip="Details">
+                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
+                                </button>
+                            </td>
+                        </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">POST</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries</code><small>Create battery record</small></span></td>
-                            <td><span class="bp-help-access-pill">Read-write</span></td>
-                            <td>Create a Battery ID after validating family, model, software, serial, and cluster scope.</td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries</code><small>Create a draft battery</small></span></td>
+                            <td><span class="bp-help-access-pill">Team, read-write</span></td>
+                            <td>Create a battery in a batch, with its serial number; the app issues its battery ID.</td>
                             <td class="bp-action-cell">
                                 <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-create-battery" aria-expanded="false" aria-controls="api-detail-create-battery" aria-label="Show battery creation endpoint details" title="Show battery creation endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
@@ -154,7 +168,7 @@
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">GET</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/section/{section}</code><small>One section of the passport</small></span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/section/{sectionName}</code><small>One section of the passport</small></span></td>
                             <td><span class="bp-help-access-pill">Read</span></td>
                             <td>Keep responses to one part of the passport.</td>
                             <td class="bp-action-cell">
@@ -187,66 +201,22 @@
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">POST</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/telemetry</code><small>Write operational points</small></span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/live-values</code><small>Live values, measured at one moment</small></span></td>
                             <td><span class="bp-help-access-pill">Read-write</span></td>
-                            <td>Append current consumption, charge, voltage, or current.</td>
+                            <td>Send state of charge, cycles, temperature and the extreme-temperature times from the battery system.</td>
                             <td class="bp-action-cell">
-                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-telemetry" aria-expanded="false" aria-controls="api-detail-telemetry" aria-label="Show telemetry endpoint details" title="Show telemetry endpoint details" data-tooltip="Details">
+                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-live-values" aria-expanded="false" aria-controls="api-detail-live-values" aria-label="Show live values endpoint details" title="Show live values endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
                                 </button>
                             </td>
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">GET</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/telemetry/history?hours=24</code><small>Recent operational history</small></span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/live-values/history?hours=24</code><small>Saved live values, newest first</small></span></td>
                             <td><span class="bp-help-access-pill">Read</span></td>
-                            <td>Review telemetry history for 1 to 168 hours.</td>
+                            <td>Review the live values saved in the last 1 to 168 hours.</td>
                             <td class="bp-action-cell">
-                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-history" aria-expanded="false" aria-controls="api-detail-history" aria-label="Show telemetry history endpoint details" title="Show telemetry history endpoint details" data-tooltip="Details">
-                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
-                                </button>
-                            </td>
-                        </tr>
-                        <tr class="bp-help-doc-item">
-                            <td><span class="bp-help-method">PATCH</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/operations</code><small>Writable operations metadata</small></span></td>
-                            <td><span class="bp-help-access-pill">Read-write</span></td>
-                            <td>Patch site, contact, or active-state metadata.</td>
-                            <td class="bp-action-cell">
-                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-operations" aria-expanded="false" aria-controls="api-detail-operations" aria-label="Show operations endpoint details" title="Show operations endpoint details" data-tooltip="Details">
-                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
-                                </button>
-                            </td>
-                        </tr>
-                        <tr class="bp-help-doc-item">
-                            <td><span class="bp-help-method">PATCH</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/battery-model</code><small>New passport required</small></span></td>
-                            <td><span class="bp-help-access-pill">Read-write</span></td>
-                            <td>Move a battery to another saved Battery Model.</td>
-                            <td class="bp-action-cell">
-                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-model" aria-expanded="false" aria-controls="api-detail-model" aria-label="Show battery model endpoint details" title="Show battery model endpoint details" data-tooltip="Details">
-                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
-                                </button>
-                            </td>
-                        </tr>
-                        <tr class="bp-help-doc-item">
-                            <td><span class="bp-help-method">PATCH</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/software-version</code><small>New passport may be required</small></span></td>
-                            <td><span class="bp-help-access-pill">Read-write</span></td>
-                            <td>Move a battery to a software version defined for its Battery Model.</td>
-                            <td class="bp-action-cell">
-                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-model" aria-expanded="false" aria-controls="api-detail-model" aria-label="Show software version endpoint details" title="Show software version endpoint details" data-tooltip="Details">
-                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
-                                </button>
-                            </td>
-                        </tr>
-                        <tr class="bp-help-doc-item">
-                            <td><span class="bp-help-method">POST</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/passports</code><small>Create a passport snapshot</small></span></td>
-                            <td><span class="bp-help-access-pill">Passport Lifecycle</span></td>
-                            <td>Create the next passport revision for a Battery ID.</td>
-                            <td class="bp-action-cell">
-                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-passports" aria-expanded="false" aria-controls="api-detail-passports" aria-label="Show passport creation endpoint details" title="Show passport creation endpoint details" data-tooltip="Details">
+                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-history" aria-expanded="false" aria-controls="api-detail-history" aria-label="Show live value history endpoint details" title="Show live value history endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
                                 </button>
                             </td>
@@ -264,31 +234,9 @@
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">POST</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/passports/{passportId}/validate</code><small>Validation gate</small></span></td>
-                            <td><span class="bp-help-access-pill">Passport Lifecycle</span></td>
-                            <td>Run trust validation before signing.</td>
-                            <td class="bp-action-cell">
-                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-validate" aria-expanded="false" aria-controls="api-detail-validate" aria-label="Show validate endpoint details" title="Show validate endpoint details" data-tooltip="Details">
-                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
-                                </button>
-                            </td>
-                        </tr>
-                        <tr class="bp-help-doc-item">
-                            <td><span class="bp-help-method">POST</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/passports/{passportId}/sign</code><small>Proof creation</small></span></td>
-                            <td><span class="bp-help-access-pill">Passport Lifecycle</span></td>
-                            <td>Create the signed proof for the current passport core.</td>
-                            <td class="bp-action-cell">
-                                <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-sign" aria-expanded="false" aria-controls="api-detail-sign" aria-label="Show sign endpoint details" title="Show sign endpoint details" data-tooltip="Details">
-                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
-                                </button>
-                            </td>
-                        </tr>
-                        <tr class="bp-help-doc-item">
-                            <td><span class="bp-help-method">POST</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/passports/{passportId}/publish</code><small>Public registry gate</small></span></td>
-                            <td><span class="bp-help-access-pill">Passport Lifecycle</span></td>
-                            <td>Publish only after validation and signing are current.</td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/publish</code><small>Publish, or publish a new version</small></span></td>
+                            <td><span class="bp-help-access-pill">Team, Passport Lifecycle</span></td>
+                            <td>Check, sign and publish a battery's passport in one step.</td>
                             <td class="bp-action-cell">
                                 <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-publish" aria-expanded="false" aria-controls="api-detail-publish" aria-label="Show publish endpoint details" title="Show publish endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
@@ -308,11 +256,13 @@
                         <pre class="bp-help-pre"><code>Authorization: Basic &lt;base64(token:)&gt;</code></pre>
                         <ul>
                             <li><code>200 OK</code>: Request succeeded.</li>
-                            <li><code>201 Created</code>: Telemetry point(s) were stored.</li>
+                            <li><code>201 Created</code>: A draft battery was created.</li>
                             <li><code>400 Bad Request</code>: Invalid input.</li>
                             <li><code>401 Unauthorized</code>: Missing or invalid API token.</li>
-                            <li><code>403 Forbidden</code>: Token scope/access is not allowed, or Sign token is required/invalid.</li>
-                            <li><code>404 Not Found</code>: Battery passport or requested section/path does not exist.</li>
+                            <li><code>403 Forbidden</code>: The token lacks the access the call needs: write or Passport Lifecycle access, a team token, or the battery's cluster.</li>
+                            <li><code>404 Not Found</code>: No battery or published passport this token may use has this ID.</li>
+                            <li><code>409 Conflict</code>: The passport can't be published yet (see <code>blocking</code>), or a serial number names more than one battery.</li>
+                            <li><code>422 Unprocessable Entity</code>: No live value could be saved; each key says why.</li>
                             <li><code>500 Server Error</code>: Unexpected server-side failure.</li>
                             <li><code>503 Service Unavailable</code>: Backend dependency is unavailable.</li>
                         </ul>
@@ -329,23 +279,27 @@
                     <pre class="bp-help-pre"><code>GET @Model.BasePath/clusters/demo-cluster/batteries</code></pre>
                 </article>
 
+                <article id="api-detail-batches" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
+                    <div><span class="bp-summary-title">Batches and operators</span><h3>Find the batch and economic operator for a new battery.</h3><p>A team (global) token lists the batches a battery can be made in, each with its family, model, year and manufacturing site, and the economic operators that place batteries on the market, as the team's New battery page offers them.</p></div>
+                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batches
+GET @Model.BasePath/operators</code></pre>
+                </article>
+
                 <article id="api-detail-create-battery" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
                     <div>
                         <span class="bp-summary-title">Battery creation</span>
-                        <h3>Create a battery record and receive its generated Battery ID.</h3>
-                        <p>Requires a read-write token scoped to the submitted cluster. The API validates Battery Family, Battery Model, software version, globally unique serial number, and cluster access before inserting the battery. Created records are audit logged with source <code>external-api</code> and the token ID.</p>
-                        <p>Create the battery record first, then create a passport snapshot with <code>POST @Model.BasePath/batteries/{batteryId}/passports</code> when a passport is needed.</p>
+                        <h3>Create a draft battery and receive its battery ID.</h3>
+                        <p>For team (global) tokens with read-write access, as on the team's pages. Name the batch (which gives the model and manufacturing site) and the economic operator by their IDs, optionally the cluster, and give the battery's own values by key: <code>batterySerialNumber</code> (#11) is required; <code>thirdPartyServiceProvider</code> (#13), <code>manufacturingDate</code> (#17, year and month) and <code>warrantyPeriod</code> (#19) are optional. The values go through the same checks as on the pages, and a serial number another battery has is refused. The app issues the battery ID (#6). The battery stays a draft, seen by the team only, until it is published.</p>
                     </div>
                     <pre class="bp-help-pre"><code>POST @Model.BasePath/batteries
 {
-  "batteryFamily": "Compact 7M",
-  "productId": "compact-7m",
-  "batteryModel": "2.0",
-  "productVersion": "2.0",
-  "softwareVersion": "4.0",
-  "serialNumber": "SN-API-REPLACE-ME",
+  "batchId": "BATCH-ID",
+  "operatorId": "OPERATOR-ID",
   "clusterId": "demo-cluster",
-  "displayName": "API created battery"
+  "values": {
+    "batterySerialNumber": "SN-API-REPLACE-ME",
+    "manufacturingDate": "2026-09"
+  }
 }</code></pre>
                 </article>
 
@@ -408,74 +362,29 @@ GET @Model.BasePath/batteries/{id}/values?path=batteryMass</code></pre>
 GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code></pre>
                 </article>
 
-                <article id="api-detail-telemetry" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Telemetry write</span><h3>Append one or more operational points.</h3><p>Supports a single object, an array, <code>{ "points": [...] }</code>, or a <code>series</code> object for one or more variables. Series points are sorted by timestamp; malformed points are ignored and reported without breaking the request when at least one valid point remains.</p></div>
-                    <pre class="bp-help-pre"><code>{
-  "points": [
-    {
-      "currentConsumptionKwh": 154.6,
-      "currentChargeLevelPct": 82.1,
-      "currentVoltageV": 401.7,
-      "currentCurrentA": 49.2,
-      "measuredAt": "2026-05-05T10:15:00Z"
-    }
-  ]
-}
-
+                <article id="api-detail-live-values" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
+                    <div>
+                        <span class="bp-summary-title">Live values</span>
+                        <h3>Send live values measured at one moment.</h3>
+                        <p>The battery system's call. <code>values</code> holds the live values by key; a bare number is enough, and the app adds the unit. <code>measuredAt</code> is when they were measured (ISO 8601 with its offset); without it, the time of the call is used. A cluster token changes the published batteries of its own clusters; a team token any battery, drafts included. Each value is checked on its own: one refused value doesn't stop the others, and an empty value never overwrites. Add <code>?preview=true</code> to check without saving.</p>
+                        <p>The live values:</p>
+                        <ul>@foreach (var live in BatteryPassWeb.Dpp.Api.BatteryApi.LiveSet) { <li><code>@live.Property</code> (@live.Label, @live.Name@(live.Unit is null ? "" : $", in {live.Unit}"))</li> }</ul>
+                        <p>Any other attribute is refused here: static values change on the team's pages.</p>
+                    </div>
+                    <pre class="bp-help-pre"><code>POST @Model.BasePath/batteries/{id}/live-values
 {
-  "series": {
-    "currentConsumptionKwh": [
-      { "measuredAt": "2026-05-20T09:00:00Z", "value": 163.4 },
-      { "measuredAt": "2026-05-20T09:05:00Z", "value": 164.1 }
-    ],
-    "currentVoltageV": [
-      { "measuredAt": "2026-05-20T09:00:00Z", "value": 398.4 },
-      { "measuredAt": "2026-05-20T09:05:00Z", "value": 399.1 }
-    ]
+  "measuredAt": "2026-10-27T10:02:59Z",
+  "values": {
+    "stateOfChargeSoC": 76,
+    "numberOfFullChargingAndDischargingCycles": 412,
+    "temperatureInformation": 23
   }
 }</code></pre>
                 </article>
 
                 <article id="api-detail-history" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Telemetry history</span><h3>Read recent telemetry history.</h3><p>The <code>hours</code> value must be in <code>1..168</code>.</p></div>
-                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/@Model.SampleBatteryId/telemetry/history?hours=24</code></pre>
-                </article>
-
-                <article id="api-detail-operations" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Operations patch</span><h3>Patch writable operations metadata.</h3><p>Writable fields are <code>locationOfUse</code>, <code>contactPerson</code>, and <code>isActive</code>.</p></div>
-                    <pre class="bp-help-pre"><code>{
-  "isActive": true,
-  "locationOfUse": {
-    "siteName": "Factory 4",
-    "address": "Main street 7",
-    "city": "Berlin",
-    "country": "DE",
-    "latitude": 52.52,
-    "longitude": 13.41
-  },
-  "contactPerson": {
-    "name": "Anna Becker",
-    "email": "anna@example.test",
-    "phone": "+49 30 1234"
-  }
-}</code></pre>
-                </article>
-
-                <article id="api-detail-model" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div>
-                        <span class="bp-summary-title">Battery Model</span>
-                        <h3>Battery ID, family, and software parameters</h3>
-                        <p>Product Template is the internal implementation name for Battery Family. In the UI and tester workflows, Product Template and Battery Family mean the same stored template concept.</p>
-                        <p>Each passport is created from a MongoDB Battery Family baseline. Battery families contain Battery Models, and each Battery Model carries software parameters such as software version, released date, and latest update values.</p>
-                        <p>The software-version endpoint accepts only a software version defined for the battery's current Battery Model and copies its release date and latest update metadata. Battery Model and software changes may require a new passport when they differ from the latest passport snapshot.</p>
-                    </div>
-                    <pre class="bp-help-pre"><code>{ "batteryModel": "2.0" }
-{ "softwareVersion": "4.0" }</code></pre>
-                </article>
-
-                <article id="api-detail-passports" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Passport snapshot</span><h3>Create a new passport snapshot.</h3><p>Create a new passport snapshot for the Battery ID after current battery data differs from the latest passport. This requires the Passport Lifecycle token. The response returns the new Passport ID; use that Passport ID for validate, sign, and publish.</p></div>
-                    <pre class="bp-help-pre"><code>POST @Model.BasePath/batteries/@Model.SampleBatteryId/passports</code></pre>
+                    <div><span class="bp-summary-title">Live value history</span><h3>Read the live values saved for a battery.</h3><p>Each saved live value, newest measurement first, with its unit, when it was measured and saved, and where it came from: the battery system, a form or an upload. <code>hours</code> counts back from now, from 1 to 168 (the default). Cut like the passport: only the live values this token may see.</p></div>
+                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/{id}/live-values/history?hours=24</code></pre>
                 </article>
 
                 <article id="api-detail-passport-list" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
@@ -483,19 +392,9 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                     <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/{id}/passports</code></pre>
                 </article>
 
-                <article id="api-detail-validate" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Validate</span><h3>Validate with the Passport Lifecycle token.</h3><p>Passport Lifecycle tokens can run trust actions only for their scoped clusters. If you just created a passport, replace the sample Passport ID with the Passport ID returned by create.</p></div>
-                    <pre class="bp-help-pre"><code>POST @Model.BasePath/passports/@Model.SamplePassportId/validate</code></pre>
-                </article>
-
-                <article id="api-detail-sign" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Sign</span><h3>Sign the current passport core.</h3><p>The first publication still requires explicit publish in the app. After a passport has been published once, later successful signs auto-publish the new revision.</p></div>
-                    <pre class="bp-help-pre"><code>POST @Model.BasePath/passports/@Model.SamplePassportId/sign</code></pre>
-                </article>
-
                 <article id="api-detail-publish" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Publish</span><h3>Publish after validation and signing.</h3><p>Publish with the Passport Lifecycle token after validation and signing are current.</p></div>
-                    <pre class="bp-help-pre"><code>POST @Model.BasePath/passports/@Model.SamplePassportId/publish</code></pre>
+                    <div><span class="bp-summary-title">Publish</span><h3>Publish a battery's passport.</h3><p>For team (global) tokens with Passport Lifecycle access. Checks the passport, signs it and publishes it in one step: a draft becomes version 1; for a published battery, a new version is published when its data changed since the current one, and nothing happens when it didn't. If something blocks publication, nothing is published and <code>blocking</code> lists what is missing.</p></div>
+                    <pre class="bp-help-pre"><code>POST @Model.BasePath/batteries/{id}/publish</code></pre>
                 </article>
                                 </div>
                             </td>
@@ -519,30 +418,25 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                         <option value="readFull">Read full battery</option>
                         <option value="listClusters">List accessible clusters</option>
                         <option value="listClusterBatteries">List cluster batteries</option>
-                        <option value="createBattery">Create battery</option>
                         <option value="readSection">Read section: Identifiers and product data</option>
                         <option value="readSectionPerformance">Read section: Performance and durability</option>
                         <option value="readSectionCircularity">Read section: Circularity</option>
                         <option value="readValuesAliases">Read values: state of charge, serial number, mass</option>
                         <option value="readPathsAll">List readable values</option>
                         <option value="readBatteryPassports">List passport versions</option>
-                        <option value="readHistory24h">Read telemetry history 24h</option>
-                        <option value="writeTelemetryPoint">Write telemetry point</option>
-                        <option value="writeTelemetrySeries">Write telemetry series</option>
-                        <option value="patchOperations">Patch operations metadata</option>
-                        <option value="patchBatteryModel">Patch Battery Model</option>
-                        <option value="patchSoftwareVersion">Patch Software Version</option>
-                        <option value="createPassport">Create passport snapshot</option>
-                        <option value="validatePassport">Validate passport</option>
-                        <option value="signPassport">Sign passport</option>
-                        <option value="publishPassport">Publish passport</option>
+                        <option value="writeLiveValues">Send live values</option>
+                        <option value="previewLiveValues">Check live values without saving</option>
+                        <option value="readLiveHistory24h">Read live value history 24h</option>
+                        <option value="listBatches">List batches (team token)</option>
+                        <option value="listOperators">List economic operators (team token)</option>
+                        <option value="createBattery">Create battery (team token)</option>
+                        <option value="publishBattery">Publish battery (team token)</option>
                     </select>
                 </label>
                 <label>HTTP method
                     <select id="tester-method">
                         <option>GET</option>
                         <option>POST</option>
-                        <option>PATCH</option>
                     </select>
                 </label>
                 <label>Auth format
@@ -553,7 +447,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 </label>
                 <label class="bp-help-sticky-id-field">Battery ID
                     <span class="bp-help-sticky-id-control">
-                        <input id="tester-battery-id" class="bp-id-value" value="@(string.IsNullOrWhiteSpace(Model.ExampleBatteryId) ? Model.SampleBatteryId : Model.ExampleBatteryId)" />
+                        <input id="tester-battery-id" class="bp-id-value" value="@Model.ExampleBatteryId" />
                         <button type="button" class="bp-copy-id-button" aria-label="Copy Battery ID" title="Copy Battery ID" data-tooltip="Copy Battery ID" data-copy-id-button data-copy-target="#tester-battery-id">
                             <svg class="bp-copy-id-icon" viewBox="0 0 24 24" aria-hidden="true">
                                 <rect x="9" y="9" width="10" height="10" rx="2" />
@@ -562,19 +456,8 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                         </button>
                     </span>
                 </label>
-                <label class="bp-help-sticky-id-field">Passport ID
-                    <span class="bp-help-sticky-id-control">
-                        <input id="tester-passport-id" class="bp-id-value" value="@Model.SamplePassportId" />
-                        <button type="button" class="bp-copy-id-button" aria-label="Copy Passport ID" title="Copy Passport ID" data-tooltip="Copy Passport ID" data-copy-id-button data-copy-target="#tester-passport-id">
-                            <svg class="bp-copy-id-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                <rect x="9" y="9" width="10" height="10" rx="2" />
-                                <path d="M6 15H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v1" />
-                            </svg>
-                        </button>
-                    </span>
-                </label>
                 <label class="bp-span-all">Path
-                    <input id="tester-path" value="@Model.BasePath/batteries/@Model.SampleBatteryId" />
+                    <input id="tester-path" value="@Model.BasePath/batteries/@Model.ExampleBatteryId" />
                 </label>
                 <label>Token preset
                     <select id="tester-token-preset">
@@ -607,9 +490,9 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
 
             <div class="d-flex flex-wrap gap-2 mt-3">
                 <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="readFull">Read battery</button>
-                <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="readHistory24h">History 24h</button>
+                <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="readLiveHistory24h">History 24h</button>
                 <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="readValuesAliases">Read specific values</button>
-                <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="writeTelemetryPoint">Post telemetry</button>
+                <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="writeLiveValues">Send live values</button>
             </div>
 
             <div class="mt-3">
@@ -629,8 +512,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
     <script>
         (() => {
             const basePath = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.BasePath));
-            const sampleBatteryId = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SampleBatteryId));
-            const samplePassportId = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SamplePassportId));
+            const exampleBatteryId = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.ExampleBatteryId));
             const sampleReadToken = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SampleReadToken));
             const sampleReadWriteToken = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SampleReadWriteToken));
             const sampleLifecycleToken = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SampleLifecycleToken));
@@ -690,7 +572,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
             const pathEl = document.getElementById('tester-path');
             const authFormatEl = document.getElementById('tester-auth-format');
             const batteryIdEl = document.getElementById('tester-battery-id');
-            const passportIdEl = document.getElementById('tester-passport-id');
             const tokenPresetEl = document.getElementById('tester-token-preset');
             const tokenEl = document.getElementById('tester-token');
             const bodyEl = document.getElementById('tester-body');
@@ -716,6 +597,15 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 targetDocument.body.appendChild(pre);
             };
 
+            const liveValuesBody = JSON.stringify({
+                measuredAt: new Date().toISOString().replace(/\.\d{3}Z$/, 'Z'),
+                values: {
+                    stateOfChargeSoC: 76,
+                    numberOfFullChargingAndDischargingCycles: 412,
+                    temperatureInformation: 23
+                }
+            }, null, 2);
+
             const templates = {
                 readFull: {
                     method: 'GET',
@@ -735,21 +625,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                     body: '',
                     tokenPreset: 'sample-read'
                 },
-                createBattery: {
-                    method: 'POST',
-                    pathTemplate: `${basePath}/batteries`,
-                    body: JSON.stringify({
-                        batteryFamily: 'Compact 7M',
-                        productId: 'compact-7m',
-                        batteryModel: '2.0',
-                        productVersion: '2.0',
-                        softwareVersion: '4.0',
-                        serialNumber: 'SN-API-REPLACE-ME',
-                        clusterId: 'demo-cluster',
-                        displayName: 'API created battery'
-                    }, null, 2),
-                    tokenPreset: 'sample-read-write'
-                },
                 readSection: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batteries/{batteryId}/section/identifiersAndProductData`,
@@ -786,103 +661,55 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                     body: '',
                     tokenPreset: 'sample-read'
                 },
-                readHistory24h: {
-                    method: 'GET',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/telemetry/history?hours=24`,
-                    body: '',
-                    tokenPreset: 'sample-read'
-                },
-                writeTelemetryPoint: {
+                writeLiveValues: {
                     method: 'POST',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/telemetry`,
-                    body: JSON.stringify({
-                        points: [
-                            {
-                                currentConsumptionKwh: 163.4,
-                                currentChargeLevelPct: 79.8,
-                                currentVoltageV: 398.4,
-                                currentCurrentA: 52.3
-                            }
-                        ]
-                    }, null, 2),
+                    pathTemplate: `${basePath}/batteries/{batteryId}/live-values`,
+                    body: liveValuesBody,
                     tokenPreset: 'sample-read-write'
                 },
-                writeTelemetrySeries: {
+                previewLiveValues: {
                     method: 'POST',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/telemetry`,
-                    body: JSON.stringify({
-                        series: {
-                            currentConsumptionKwh: [
-                                { measuredAt: '2026-05-20T09:00:00Z', value: 163.4 },
-                                { measuredAt: '2026-05-20T09:05:00Z', value: 164.1 }
-                            ],
-                            currentChargeLevelPct: [
-                                { measuredAt: '2026-05-20T09:00:00Z', value: 79.8 }
-                            ],
-                            currentVoltageV: [
-                                { measuredAt: '2026-05-20T09:00:00Z', value: 398.4 },
-                                { measuredAt: '2026-05-20T09:05:00Z', value: 399.1 }
-                            ]
-                        }
-                    }, null, 2),
+                    pathTemplate: `${basePath}/batteries/{batteryId}/live-values?preview=true`,
+                    body: liveValuesBody,
                     tokenPreset: 'sample-read-write'
                 },
-                patchOperations: {
-                    method: 'PATCH',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/operations`,
-                    body: JSON.stringify({
-                        isActive: true,
-                        locationOfUse: {
-                            siteName: 'Sample Site',
-                            city: 'Berlin',
-                            country: 'DE'
-                        },
-                        contactPerson: {
-                            name: 'Sample Contact',
-                            email: 'sample@example.test'
-                        }
-                    }, null, 2),
-                    tokenPreset: 'sample-read-write'
-                },
-                patchBatteryModel: {
-                    method: 'PATCH',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/battery-model`,
-                    body: JSON.stringify({
-                        batteryModel: '2.0'
-                    }, null, 2),
-                    tokenPreset: 'sample-read-write'
-                },
-                patchSoftwareVersion: {
-                    method: 'PATCH',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/software-version`,
-                    body: JSON.stringify({
-                        softwareVersion: '4.0'
-                    }, null, 2),
-                    tokenPreset: 'sample-read-write'
+                readLiveHistory24h: {
+                    method: 'GET',
+                    pathTemplate: `${basePath}/batteries/{batteryId}/live-values/history?hours=24`,
+                    body: '',
+                    tokenPreset: 'sample-read'
                 },
-                createPassport: {
-                    method: 'POST',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/passports`,
+                listBatches: {
+                    method: 'GET',
+                    pathTemplate: `${basePath}/batches`,
                     body: '',
-                    tokenPreset: 'sample-lifecycle'
+                    tokenPreset: 'custom'
                 },
-                validatePassport: {
-                    method: 'POST',
-                    pathTemplate: `${basePath}/passports/{passportId}/validate`,
+                listOperators: {
+                    method: 'GET',
+                    pathTemplate: `${basePath}/operators`,
                     body: '',
-                    tokenPreset: 'sample-lifecycle'
+                    tokenPreset: 'custom'
                 },
-                signPassport: {
+                createBattery: {
                     method: 'POST',
-                    pathTemplate: `${basePath}/passports/{passportId}/sign`,
-                    body: '',
-                    tokenPreset: 'sample-lifecycle'
+                    pathTemplate: `${basePath}/batteries`,
+                    body: JSON.stringify({
+                        batchId: 'BATCH-ID',
+                        operatorId: 'OPERATOR-ID',
+                        clusterId: 'demo-cluster',
+                        values: {
+                            batterySerialNumber: 'SN-API-REPLACE-ME',
+                            manufacturingDate: new Date().toISOString().slice(0, 7)
+                        }
+                    }, null, 2),
+                    tokenPreset: 'custom'
                 },
-                publishPassport: {
+                publishBattery: {
                     method: 'POST',
-                    pathTemplate: `${basePath}/passports/{passportId}/publish`,
+                    pathTemplate: `${basePath}/batteries/{batteryId}/publish`,
                     body: '',
-                    tokenPreset: 'sample-lifecycle'
+                    tokenPreset: 'custom'
                 }
             };
 
@@ -975,14 +802,11 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
             };
 
             const getWorkbenchContext = () => ({
-                batteryId: (batteryIdEl?.value || sampleBatteryId).trim() || sampleBatteryId,
-                passportId: (passportIdEl?.value || samplePassportId).trim() || samplePassportId
+                batteryId: (batteryIdEl?.value || exampleBatteryId).trim() || exampleBatteryId
             });
 
             const resolveTemplatePath = (pathTemplate, context = getWorkbenchContext()) => {
-                return (pathTemplate || '')
-                    .replaceAll('{batteryId}', context.batteryId)
-                    .replaceAll('{passportId}', context.passportId);
+                return (pathTemplate || '').replaceAll('{batteryId}', context.batteryId);
             };
 
             const setTokenFromPreset = () => {
@@ -1180,8 +1004,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
             document.getElementById('tester-reset')?.addEventListener('click', () => {
                 templateSelect.value = 'readFull';
                 authFormatEl.value = 'base64';
-                batteryIdEl.value = sampleBatteryId;
-                passportIdEl.value = samplePassportId;
+                batteryIdEl.value = exampleBatteryId;
                 tokenPresetEl.value = 'sample-read';
                 tokenEl.value = sampleReadToken;
                 applyTemplate('readFull');
@@ -1196,8 +1019,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
 
             batteryIdEl?.addEventListener('input', refreshTemplatePathFromContext);
             batteryIdEl?.addEventListener('change', refreshTemplatePathFromContext);
-            passportIdEl?.addEventListener('input', refreshTemplatePathFromContext);
-            passportIdEl?.addEventListener('change', refreshTemplatePathFromContext);
 
             document.querySelectorAll('.template-shortcut').forEach(button => {
                 button.addEventListener('click', () => {
diff --git a/web/appsettings.Development.json b/web/appsettings.Development.json
--- a/web/appsettings.Development.json
+++ b/web/appsettings.Development.json
@@ -28,7 +28,6 @@
     "FileUploadRateLimitPerWindow": 10,
     "MaxAuthorizationHeaderBytes": 4096,
     "MaxJsonBodyBytes": 1048576,
-    "MaxTelemetryPoints": 250,
     "MaxUploadBytes": 10485760,
     "RequireHttpsRedirection": true
   }
diff --git a/web/appsettings.json b/web/appsettings.json
--- a/web/appsettings.json
+++ b/web/appsettings.json
@@ -28,7 +28,6 @@
     "FileUploadRateLimitPerWindow": 10,
     "MaxAuthorizationHeaderBytes": 4096,
     "MaxJsonBodyBytes": 1048576,
-    "MaxTelemetryPoints": 250,
     "MaxUploadBytes": 10485760,
     "RequireHttpsRedirection": true
   }
```

- [ ] **Step 4: Delete the files**

```bash
git rm -q web/Controllers/ExternalApiController.cs web/Services/BatteryCreationService.cs web/Services/BatteryTelemetryRepository.cs
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AntiForgeryProtectionTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryAdminWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryCreationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.BatterySnapshotWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppApiControllerTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppApiWritingTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryCreateTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryIdTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryVersionTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiInitializerTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiTelemetryTemplateTests|FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Phase6ADocumentationTests|FullyQualifiedName~BatteryPassWeb.Tests.SecurityLimitsTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustDirtyStateTests"`
Expected: `Passed!  - Failed:     0, Passed:   130`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2120`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs BatteryPassWeb.Tests/ExternalApiInitializerTests.cs BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs BatteryPassWeb.Tests/Phase6ADocumentationTests.cs BatteryPassWeb.Tests/SecurityLimitsTests.cs BatteryPassWeb.Tests/TrustDirtyStateTests.cs BatteryPassWeb.Tests/Dpp/DppApiWritingTests.cs web/.env.example web/Configuration/BatteryPassOptions.cs web/Controllers/DppApiController.cs web/Controllers/HelpController.cs web/Models/ViewModels/ExternalApiHelpViewModel.cs web/Program.cs web/Services/ExternalApiInitializer.cs web/Views/Help/Index.cshtml web/appsettings.Development.json web/appsettings.json
git commit -m "feat(dpp): the external API creates, publishes and takes live values on the new model; the old writing calls go" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 3: The redesigned request workbench

The product owner's redesign (decision 6 above): grouped calls, live-value fields, a plain answer, the technical parts folded away. The telemetry simulator goes with its checks. Razor views compile with the build, so a mistake in the view fails like one in C#.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/DemoTelemetrySimulatorScriptTests.cs`
- Create: `web/wwwroot/css/bp-api-help.css`
- Modify: `web/Views/Help/Index.cshtml`
- Delete: `scripts/demo-telemetry-simulator.ps1`

- [ ] **Step 1: Write the failing tests**

Patch `3c2-task3-tests.patch` (`BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`). Save the block as `../3c2-task3-tests.patch`, outside the worktree, and run `git apply ../3c2-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
--- a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
+++ b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
@@ -230,6 +230,34 @@ public sealed class HelpWorkbenchLayoutTests
         Assert.Contains("SAMPLEBATTERYPASSPORTLIFECYC001", initializer);
     }
 
+    [Fact]
+    public void HelpWorkbench_ShouldGroupTheCallsBuildLiveValuesAndAnswerInPlainWords()
+    {
+        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
+        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "bp-api-help.css"));
+        var workbench = markup[markup.IndexOf("Request workbench", StringComparison.Ordinal)..];
+
+        Assert.Contains("<link rel=\"stylesheet\" href=\"~/css/bp-api-help.css\"", markup);
+        Assert.Contains("<optgroup label=\"Reading\">", workbench);
+        Assert.Contains("<optgroup label=\"Live values\">", workbench);
+        Assert.Contains("<optgroup label=\"Team token: create and publish\">", workbench);
+        Assert.Contains("id=\"tester-method-tag\"", workbench);
+        Assert.Contains("@foreach (var live in BatteryPassWeb.Dpp.Api.BatteryApi.LiveSet)", workbench);
+        Assert.Contains("data-live-key=\"@live.Property\"", workbench);
+        Assert.Contains("<span class=\"bp-wb-unit\">@live.Unit</span>", workbench);
+        Assert.Contains("id=\"tester-measured-now\"", workbench);
+        Assert.Contains("const renderAnswer", workbench);
+        Assert.Contains("<summary>Show JSON</summary>", workbench);
+        Assert.Contains("<summary>Show request</summary>", workbench);
+        Assert.True(
+            workbench.IndexOf("<summary>Show request</summary>", StringComparison.Ordinal) < workbench.IndexOf("id=\"tester-auth-format\"", StringComparison.Ordinal),
+            "The auth format belongs under Show request.");
+        Assert.DoesNotContain("template-shortcut", workbench);
+        Assert.DoesNotContain("tester-load-template", workbench);
+        Assert.DoesNotContain(">s</span>", workbench);
+        Assert.Contains(".bp-wb-tag.is-done", css);
+    }
+
     [Fact]
     public void HelpDocumentation_ShouldExplainExternalApiHttpCodes()
     {
```

```bash
git rm -q BatteryPassWeb.Tests/DemoTelemetrySimulatorScriptTests.cs
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.DemoTelemetrySimulatorScriptTests|FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests"`
Expected: `Failed:     1, Passed:    15`: the new `HelpWorkbench_ShouldGroupTheCallsBuildLiveValuesAndAnswerInPlainWords`. The simulator's five checks are already deleted.

- [ ] **Step 3: Write `bp-api-help.css`**

`web/wwwroot/css/bp-api-help.css`:

```css
/* The API help page's request workbench (step 3c-2). The palette, fonts, buttons and inputs come from site.css; this
   file, loaded only on the help page, lays the workbench out: the call, battery and token first, the live value
   fields when they apply, then the answer in plain words, with the JSON and the raw request folded away. Quiet outlined
   tags, green only for "done", as on the team pages (decision 27). */

.bp-wb .bp-wb-hint {
  color: var(--bp-muted);
  font-size: 0.82rem;
  font-weight: 400;
}

/* The card has no padding of its own (site.css pads its form grids): the other parts keep the same 16px edge. */
.bp-wb > :is(.bp-wb-live, .bp-wb-actions, .bp-wb-answer, .bp-wb-more) {
  margin-left: 16px;
  margin-right: 16px;
}

.bp-wb > .bp-wb-more:last-child {
  margin-bottom: 16px;
}

.bp-wb > .bp-wb-body {
  padding-top: 0;
}

/* A field keeps its own height when the field beside it is taller (a list with a hint under it). */
.bp-wb-grid > label {
  align-content: start;
}

@media (min-width: 900px) {
  .bp-wb-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .bp-wb-request-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

.bp-wb-call-control,
.bp-wb-unit-control {
  display: flex;
  align-items: center;
  gap: 8px;
}

.bp-wb-call-control select {
  flex: 1;
  min-width: 0;
}

/* Small outlined tags in sentence case; green only for a call that worked or a value that was saved. */
.bp-wb-tag {
  display: inline-block;
  flex: none;
  padding: 1px 8px;
  border: 1px solid #d6dee8;
  border-radius: 999px;
  color: #405367;
  font-size: 0.75rem;
  font-weight: 600;
  white-space: nowrap;
}

.bp-wb-tag.is-done {
  border-color: #b7dfc4;
  color: #166534;
}

.bp-wb-tag.is-refused {
  border-color: #f1b8bf;
  color: var(--bp-danger);
}

.bp-wb-live {
  margin-top: 0;
  margin-bottom: 14px;
}

.bp-wb-live .bp-wb-hint {
  margin-bottom: 10px;
}

.bp-wb-live-grid {
  display: grid;
  gap: 10px 16px;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
}

.bp-wb-live-grid label {
  display: grid;
  gap: 4px;
  font-size: 0.82rem;
  font-weight: 600;
}

.bp-wb-live-grid input[type="number"],
.bp-wb-live-grid input[type="datetime-local"] {
  flex: 1;
  min-width: 0;
  min-height: 38px;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  padding: 6px 10px;
  font-size: 0.92rem;
}

.bp-wb-live-grid input:disabled {
  background: #eef2f6;
  color: #5c6c7d;
}

.bp-wb-code {
  color: var(--bp-muted);
  font-weight: 700;
}

.bp-wb-unit {
  min-width: 2.5em;
  color: var(--bp-muted);
  font-weight: 600;
}

.bp-wb-now {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-weight: 600;
  white-space: nowrap;
}

.bp-wb-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 0;
}

.bp-wb-answer {
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--bp-border);
}

.bp-wb-answer-head {
  display: flex;
  align-items: center;
  gap: 10px;
}

.bp-wb-answer-head h3 {
  margin: 0;
  font-size: 1rem;
  font-weight: 700;
}

.bp-wb-message {
  margin: 8px 0 0;
}

.bp-wb-facts {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 24px;
  margin: 12px 0 0;
  font-size: 0.85rem;
}

.bp-wb-facts dt {
  color: var(--bp-muted);
  font-weight: 600;
}

.bp-wb-facts dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.bp-wb-table {
  margin: 12px 0 0;
  font-size: 0.85rem;
}

.bp-wb-table td {
  overflow-wrap: anywhere;
}

.bp-wb-list-title {
  margin: 12px 0 4px;
  font-weight: 700;
}

.bp-wb-list {
  margin: 0;
  padding-left: 1.2em;
}

.bp-wb-more {
  margin-top: 14px;
}

.bp-wb-more > summary {
  width: fit-content;
  color: var(--bp-primary);
  font-weight: 600;
  cursor: pointer;
}

.bp-wb-more > summary:focus-visible {
  outline: 2px solid var(--bp-primary);
  outline-offset: 2px;
}

.bp-wb-more[open] > summary {
  margin-bottom: 10px;
}

[data-theme="dark"] .bp-wb-tag {
  border-color: var(--bp-border);
  color: var(--bp-text);
}

[data-theme="dark"] .bp-wb-tag.is-done {
  border-color: #2f6b45;
  color: #86efac;
}

[data-theme="dark"] .bp-wb-tag.is-refused {
  border-color: #7a2b35;
}

[data-theme="dark"] .bp-wb-live-grid :is(input[type="number"], input[type="datetime-local"]) {
  border-color: var(--bp-border);
  background: var(--bp-surface);
  color: var(--bp-text);
}

[data-theme="dark"] .bp-wb-live-grid input:disabled {
  background: var(--bp-bg);
  color: var(--bp-muted);
}
```

- [ ] **Step 4: Redesign the workbench and remove the simulator**

Patch `3c2-task3-code.patch` (`web/Views/Help/Index.cshtml`). Save the block as `../3c2-task3-code.patch`, outside the worktree, and run `git apply ../3c2-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -2,6 +2,7 @@
 @{
     ViewData["Title"] = "External API Help";
 }
+<link rel="stylesheet" href="~/css/bp-api-help.css" asp-append-version="true" />
 <main class="bp-page bp-admin-page bp-help-page bp-api-help-page">
     <header class="bp-console-header">
         <div>
@@ -404,50 +405,48 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
             </div>
         </section>
 
-        <section class="bp-card bp-help-tester-card bp-api-help-section-card">
+        <section class="bp-card bp-help-tester-card bp-api-help-section-card bp-wb">
             <div class="bp-api-help-doc-heading">
                 <div>
                     <span class="bp-summary-title">Sandbox</span>
                     <h2>Request workbench</h2>
+                    <p class="bp-wb-hint mb-0">Choose a call, check the battery ID and the token, and send it. The answer is shown in plain words first.</p>
                 </div>
             </div>
 
-            <div class="bp-form-grid">
-                <label>Template
-                    <select id="tester-template">
-                        <option value="readFull">Read full battery</option>
-                        <option value="listClusters">List accessible clusters</option>
-                        <option value="listClusterBatteries">List cluster batteries</option>
-                        <option value="readSection">Read section: Identifiers and product data</option>
-                        <option value="readSectionPerformance">Read section: Performance and durability</option>
-                        <option value="readSectionCircularity">Read section: Circularity</option>
-                        <option value="readValuesAliases">Read values: state of charge, serial number, mass</option>
-                        <option value="readPathsAll">List readable values</option>
-                        <option value="readBatteryPassports">List passport versions</option>
-                        <option value="writeLiveValues">Send live values</option>
-                        <option value="previewLiveValues">Check live values without saving</option>
-                        <option value="readLiveHistory24h">Read live value history 24h</option>
-                        <option value="listBatches">List batches (team token)</option>
-                        <option value="listOperators">List economic operators (team token)</option>
-                        <option value="createBattery">Create battery (team token)</option>
-                        <option value="publishBattery">Publish battery (team token)</option>
-                    </select>
-                </label>
-                <label>HTTP method
-                    <select id="tester-method">
-                        <option>GET</option>
-                        <option>POST</option>
-                    </select>
-                </label>
-                <label>Auth format
-                    <select id="tester-auth-format">
-                        <option value="base64">Basic &lt;base64(token:)&gt;</option>
-                        <option value="raw">Basic &lt;token&gt;</option>
-                    </select>
+            <div class="bp-form-grid bp-wb-grid">
+                <label>Call
+                    <span class="bp-wb-call-control">
+                        <select id="tester-template">
+                            <optgroup label="Reading">
+                                <option value="readFull">Read full battery</option>
+                                <option value="listClusters">List accessible clusters</option>
+                                <option value="listClusterBatteries">List cluster batteries</option>
+                                <option value="readSection">Read section: Identifiers and product data</option>
+                                <option value="readSectionPerformance">Read section: Performance and durability</option>
+                                <option value="readSectionCircularity">Read section: Circularity</option>
+                                <option value="readValuesAliases">Read values: state of charge, serial number, mass</option>
+                                <option value="readPathsAll">List readable values</option>
+                                <option value="readBatteryPassports">List passport versions</option>
+                            </optgroup>
+                            <optgroup label="Live values">
+                                <option value="writeLiveValues">Send live values</option>
+                                <option value="previewLiveValues">Check live values without saving</option>
+                                <option value="readLiveHistory24h">Read live value history 24h</option>
+                            </optgroup>
+                            <optgroup label="Team token: create and publish">
+                                <option value="listBatches">List batches</option>
+                                <option value="listOperators">List economic operators</option>
+                                <option value="createBattery">Create battery</option>
+                                <option value="publishBattery">Publish battery</option>
+                            </optgroup>
+                        </select>
+                        <span id="tester-method-tag" class="bp-wb-tag" title="HTTP method, set by the call">GET</span>
+                    </span>
                 </label>
                 <label class="bp-help-sticky-id-field">Battery ID
                     <span class="bp-help-sticky-id-control">
-                        <input id="tester-battery-id" class="bp-id-value" value="@Model.ExampleBatteryId" />
+                        <input id="tester-battery-id" class="bp-id-value" value="@Model.ExampleBatteryId" spellcheck="false" />
                         <button type="button" class="bp-copy-id-button" aria-label="Copy Battery ID" title="Copy Battery ID" data-tooltip="Copy Battery ID" data-copy-id-button data-copy-target="#tester-battery-id">
                             <svg class="bp-copy-id-icon" viewBox="0 0 24 24" aria-hidden="true">
                                 <rect x="9" y="9" width="10" height="10" rx="2" />
@@ -456,54 +455,101 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                         </button>
                     </span>
                 </label>
-                <label class="bp-span-all">Path
-                    <input id="tester-path" value="@Model.BasePath/batteries/@Model.ExampleBatteryId" />
-                </label>
-                <label>Token preset
+                <label>Token
                     <select id="tester-token-preset">
                         <option value="sample-read">Sample read token</option>
                         <option value="sample-read-write">Sample read-write token</option>
                         <option value="sample-lifecycle">Sample Passport Lifecycle token</option>
-                        <option value="custom">Custom token</option>
+                        <option value="custom">Your own token</option>
                     </select>
                     @if (!Model.CanUsePrivilegedDemoTokens)
                     {
-                        <small class="text-secondary">@Model.PrivilegedDemoTokenMessage</small>
+                        <small class="bp-wb-hint">@Model.PrivilegedDemoTokenMessage</small>
                     }
                 </label>
-                <label class="bp-span-2">Token value
-                    <input id="tester-token" value="@Model.SampleReadToken" />
-                </label>
-                <label class="bp-span-all">Body JSON
-                    <textarea id="tester-body" rows="9"></textarea>
+                <label>Token value
+                    <input id="tester-token" value="@Model.SampleReadToken" autocomplete="off" spellcheck="false" />
+                    <small id="tester-team-note" class="bp-wb-hint" hidden>This call needs a team (global) token: paste one from API Token Management.</small>
                 </label>
-                <label class="bp-span-all">Constructed HTTP request
-                    <textarea id="tester-raw-request" class="bp-help-request-editor" rows="12"></textarea>
+            </div>
+
+            <fieldset id="tester-live-form" class="bp-fieldset bp-wb-live" hidden>
+                <legend>Live values</legend>
+                <p class="bp-wb-hint">Fill in the values to send; empty fields are left out. The units come from the attribute list.</p>
+                <div class="bp-wb-live-grid">
+                    @foreach (var live in BatteryPassWeb.Dpp.Api.BatteryApi.LiveSet)
+                    {
+                        <label>
+                            <span><span class="bp-wb-code">@live.Label</span> @live.Name</span>
+                            <span class="bp-wb-unit-control">
+                                <input type="number" step="any" inputmode="decimal" data-live-key="@live.Property" aria-label="@live.Label @live.Name" />
+                                @if (live.Unit is not null)
+                                {
+                                    <span class="bp-wb-unit">@live.Unit</span>
+                                }
+                            </span>
+                        </label>
+                    }
+                    <label>
+                        <span>Measured at</span>
+                        <span class="bp-wb-unit-control">
+                            <input id="tester-measured-at" type="datetime-local" step="1" aria-label="Measured at" disabled />
+                            <span class="bp-wb-now"><input id="tester-measured-now" type="checkbox" checked aria-label="Measured now" /> Now</span>
+                        </span>
+                    </label>
+                </div>
+            </fieldset>
+
+            <div id="tester-body-field" class="bp-form-grid bp-wb-body">
+                <label class="bp-span-all">Body (JSON)
+                    <textarea id="tester-body" rows="8" spellcheck="false"></textarea>
+                    <small id="tester-body-note" class="bp-wb-hint" hidden>Built from the live value fields; you can still edit it.</small>
                 </label>
             </div>
 
-            <div class="d-flex flex-wrap gap-2 mt-3">
-                <button id="tester-load-template" type="button" class="bp-secondary-button">Load template</button>
+            <div class="bp-wb-actions">
                 <button id="tester-send" type="button" class="bp-primary-button">Send request</button>
                 <button id="tester-reset" type="button" class="bp-secondary-button">Reset</button>
             </div>
 
-            <div class="d-flex flex-wrap gap-2 mt-3">
-                <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="readFull">Read battery</button>
-                <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="readLiveHistory24h">History 24h</button>
-                <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="readValuesAliases">Read specific values</button>
-                <button type="button" class="bp-secondary-button bp-button-compact template-shortcut" data-template="writeLiveValues">Send live values</button>
-            </div>
-
-            <div class="mt-3">
-                <p class="mb-1"><strong>Status:</strong> <span id="tester-status">-</span></p>
-                <pre id="tester-response" class="bp-help-pre"><code>{}</code></pre>
+            <div class="bp-wb-answer" aria-live="polite">
+                <div class="bp-wb-answer-head">
+                    <h3>Answer</h3>
+                    <span id="tester-status" class="bp-wb-tag">-</span>
+                </div>
+                <p id="tester-message" class="bp-wb-message" hidden></p>
+                <div id="tester-summary"></div>
+                <details class="bp-wb-more">
+                    <summary>Show JSON</summary>
+                    <pre id="tester-response" class="bp-help-pre"><code>{}</code></pre>
+                </details>
             </div>
 
-            <div class="mt-3">
-                <p class="mb-1"><strong>cURL preview</strong></p>
+            <details class="bp-wb-more">
+                <summary>Show request</summary>
+                <div class="bp-form-grid bp-wb-request-grid">
+                    <label>HTTP method
+                        <select id="tester-method">
+                            <option>GET</option>
+                            <option>POST</option>
+                        </select>
+                    </label>
+                    <label>Auth format
+                        <select id="tester-auth-format">
+                            <option value="base64">Basic &lt;base64(token:)&gt;</option>
+                            <option value="raw">Basic &lt;token&gt;</option>
+                        </select>
+                    </label>
+                    <label class="bp-span-all">Path
+                        <input id="tester-path" value="@Model.BasePath/batteries/@Model.ExampleBatteryId" spellcheck="false" />
+                    </label>
+                    <label class="bp-span-all">Constructed HTTP request
+                        <textarea id="tester-raw-request" class="bp-help-request-editor" rows="10" spellcheck="false"></textarea>
+                    </label>
+                </div>
+                <p class="mb-1 mt-3"><strong>cURL</strong></p>
                 <pre id="tester-curl" class="bp-help-pre"><code></code></pre>
-            </div>
+            </details>
         </section>
     </div>
 </main>
@@ -973,6 +1019,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 pathEl.value = resolveTemplatePath(template.pathTemplate, getWorkbenchContext());
                 bodyEl.value = template.body;
                 syncRawRequestFromControls();
+                showCallParts(templateKey);
             };
 
             const refreshTemplatePathFromContext = () => {
@@ -985,8 +1032,10 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 syncRawRequestFromControls();
             };
 
-            const setStatus = (text) => {
+            const setStatus = (text, ok) => {
                 statusEl.innerText = text;
+                statusEl.classList.toggle('is-done', ok === true);
+                statusEl.classList.toggle('is-refused', ok === false);
             };
 
             const setResponse = (value) => {
@@ -997,19 +1046,19 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 applyTemplate(templateSelect.value);
             });
 
-            document.getElementById('tester-load-template')?.addEventListener('click', () => {
-                applyTemplate(templateSelect.value);
-            });
-
             document.getElementById('tester-reset')?.addEventListener('click', () => {
                 templateSelect.value = 'readFull';
                 authFormatEl.value = 'base64';
                 batteryIdEl.value = exampleBatteryId;
                 tokenPresetEl.value = 'sample-read';
                 tokenEl.value = sampleReadToken;
+                liveInputs.forEach(input => { input.value = ''; });
+                measuredNowEl.checked = true;
+                measuredAtEl.disabled = true;
                 applyTemplate('readFull');
                 setStatus('-');
                 setResponse('{}');
+                renderAnswer(null);
             });
 
             tokenPresetEl?.addEventListener('change', () => {
@@ -1020,18 +1069,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
             batteryIdEl?.addEventListener('input', refreshTemplatePathFromContext);
             batteryIdEl?.addEventListener('change', refreshTemplatePathFromContext);
 
-            document.querySelectorAll('.template-shortcut').forEach(button => {
-                button.addEventListener('click', () => {
-                    const key = button.getAttribute('data-template');
-                    if (!key) {
-                        return;
-                    }
-
-                    templateSelect.value = key;
-                    applyTemplate(key);
-                });
-            });
-
             [methodEl, pathEl, authFormatEl, tokenEl, bodyEl].forEach(element => {
                 element?.addEventListener('input', syncRawRequestFromControls);
                 element?.addEventListener('change', syncRawRequestFromControls);
@@ -1045,7 +1082,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
             document.getElementById('tester-send')?.addEventListener('click', async () => {
                 const parsed = parseRawRequest(rawRequestEl.value);
                 if (!parsed || !parsed.absoluteUrl) {
-                    setStatus('Validation failed');
+                    setStatus('Validation failed', false);
                     setResponse('Constructed HTTP request is invalid.');
                     return;
                 }
@@ -1063,20 +1100,173 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                     });
 
                     const text = await httpResponse.text();
-                    setStatus(`${httpResponse.status} ${httpResponse.statusText}`);
+                    setStatus(`${httpResponse.status} ${httpResponse.statusText}`, httpResponse.ok);
                     try {
-                        setResponse(JSON.stringify(JSON.parse(text), null, 2));
+                        const answer = JSON.parse(text);
+                        setResponse(JSON.stringify(answer, null, 2));
+                        renderAnswer(answer);
                     } catch {
                         setResponse(text);
+                        renderAnswer(null);
                     }
                 } catch (error) {
-                    setStatus('Request failed');
+                    setStatus('Request failed', false);
                     setResponse(String(error));
+                    renderAnswer(null);
                 } finally {
                     updateCurlPreview();
                 }
             });
 
+            // The answer in plain words: the API's message, then a table of what came back. The JSON stays under "Show JSON".
+            const messageEl = document.getElementById('tester-message');
+            const summaryEl = document.getElementById('tester-summary');
+            const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]));
+            const plainValue = (value) => {
+                if (value === null || value === undefined) {
+                    return '-';
+                }
+
+                if (typeof value !== 'object') {
+                    return String(value);
+                }
+
+                const parts = Object.entries(value)
+                    .filter(([key, part]) => part !== null && typeof part !== 'object' && !/unit$/i.test(key))
+                    .map(([, part]) => String(part));
+                return parts.length > 0 ? parts.join(', ') : JSON.stringify(value);
+            };
+            const answerTag = (text, kind) => `<span class="bp-wb-tag${kind ? ` is-${kind}` : ''}">${escapeHtml(text)}</span>`;
+            const answerTable = (heads, rows) => `<div class="bp-table-scroll"><table class="table bp-console-table bp-wb-table"><thead><tr>${heads.map(head => `<th>${escapeHtml(head)}</th>`).join('')}</tr></thead><tbody>${rows.map(row => `<tr>${row.map((cell, index) => `<td data-label="${escapeHtml(heads[index])}">${cell}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`;
+            const code = (text) => text ? `<code>${escapeHtml(text)}</code>` : '-';
+            const factWords = {
+                batteryId: 'Battery ID', passportId: 'Passport ID', serialNumber: 'Serial number', clusterId: 'Cluster', clusterName: 'Cluster name',
+                version: 'Version', dppStatus: 'Status', signatureVerified: 'Signature holds', unchanged: 'Unchanged', preview: 'Preview only',
+                saved: 'Saved', measuredAt: 'Measured at', hours: 'Hours', section: 'Section', globalAccess: 'Team token', publishPath: 'Publish with'
+            };
+            const outcomeWords = { saved: ['Saved', 'done'], kept: ['Kept (empty)', ''], ignored: ['Ignored', ''], removed: ['Removed', ''], rejected: ['Refused', 'refused'] };
+
+            const renderAnswer = (body) => {
+                summaryEl.innerHTML = '';
+                messageEl.hidden = true;
+                if (!body || typeof body !== 'object') {
+                    return;
+                }
+
+                if (body.message) {
+                    messageEl.textContent = body.message;
+                    messageEl.hidden = false;
+                }
+
+                const data = body.data && typeof body.data === 'object' ? body.data : {};
+                const values = Array.isArray(data.values) ? data.values : null;
+                const parts = [];
+                if (values && values.length > 0 && 'outcome' in values[0]) {
+                    parts.push(answerTable(['Value', 'Key', 'Result', 'Why'], values.map(value => {
+                        const [word, kind] = outcomeWords[value.outcome] || [value.outcome, ''];
+                        return [escapeHtml(value.attribute || '-'), code(value.key), answerTag(word, kind), escapeHtml(value.message || '')];
+                    })));
+                } else if (values && values.length > 0 && 'measuredAt' in values[0]) {
+                    parts.push(answerTable(['Measured', 'Value', 'Name', 'Reading', 'Unit', 'Source'], values.map(value =>
+                        [escapeHtml(value.measuredAt), escapeHtml(value.attribute), escapeHtml(value.name), escapeHtml(plainValue(value.value)), escapeHtml(value.unit || '-'), escapeHtml(value.source)])));
+                } else if (values && values.length > 0 && 'exists' in values[0]) {
+                    parts.push(answerTable(['Asked', 'Value', 'Name', 'Reading', 'Unit', 'Came from'], values.map(value =>
+                        [code(value.request), escapeHtml(value.attribute || '-'), escapeHtml(value.name || ''), value.exists ? escapeHtml(plainValue(value.value)) : answerTag('Not available', ''), escapeHtml(value.unit || '-'),
+                            escapeHtml([value.source, value.generatedAt].filter(Boolean).join(', ') || '-')])));
+                } else if (values) {
+                    parts.push('<p class="bp-wb-hint">No values in this answer.</p>');
+                } else if (Array.isArray(data.batteries)) {
+                    parts.push(answerTable(['Battery ID', 'Serial number', 'Model', 'Status'], data.batteries.map(battery =>
+                        [code(battery.batteryId), escapeHtml(battery.serialNumber), escapeHtml(battery.batteryModel), answerTag(battery.dppStatus, battery.dppStatus === 'active' ? 'done' : '')])));
+                } else if (Array.isArray(data.batches)) {
+                    parts.push(answerTable(['Batch ID', 'Family', 'Model', 'Year', 'Manufacturing site'], data.batches.map(batch =>
+                        [code(batch.batchId), escapeHtml(batch.batteryFamily), escapeHtml(batch.batteryModel), escapeHtml(batch.year), escapeHtml(batch.manufacturingSite)])));
+                } else if (Array.isArray(data.operators)) {
+                    parts.push(answerTable(['Operator ID', 'Name'], data.operators.map(item => [code(item.operatorId), escapeHtml(item.name)])));
+                } else if (Array.isArray(data.clusters)) {
+                    parts.push(answerTable(['Cluster ID', 'Name'], data.clusters.map(cluster => [code(cluster.clusterId), escapeHtml(cluster.name)])));
+                } else if (Array.isArray(data.passports)) {
+                    parts.push(answerTable(['Version', 'Passport ID', 'Status', 'Published', 'Signature'], data.passports.map(version =>
+                        [escapeHtml(version.version), code(version.passportId), answerTag(version.dppStatus, version.dppStatus === 'active' ? 'done' : ''), escapeHtml(version.publishedAt), version.signatureVerified ? answerTag('Holds', 'done') : answerTag('Broken', 'refused')])));
+                } else if (Array.isArray(data.attributes)) {
+                    parts.push(answerTable(['Value', 'Key', 'Name', 'Unit'], data.attributes.map(attribute =>
+                        [escapeHtml(attribute.attribute), code(attribute.key), escapeHtml(attribute.name), escapeHtml(attribute.unit || '-')])));
+                }
+
+                if (Array.isArray(data.blocking) && data.blocking.length > 0) {
+                    parts.push(`<p class="bp-wb-list-title">What blocks publication</p><ul class="bp-wb-list">${data.blocking.map(item => `<li>${escapeHtml(item)}</li>`).join('')}</ul>`);
+                }
+
+                if (Array.isArray(data.warnings) && data.warnings.length > 0) {
+                    parts.push(`<p class="bp-wb-list-title">Warnings</p><ul class="bp-wb-list">${data.warnings.map(item => `<li>${escapeHtml(item)}</li>`).join('')}</ul>`);
+                }
+
+                const facts = Object.entries(data).filter(([, value]) => value !== null && typeof value !== 'object');
+                if (facts.length > 0) {
+                    parts.unshift(`<dl class="bp-wb-facts">${facts.map(([key, value]) => `<div><dt>${escapeHtml(factWords[key] || key)}</dt><dd>${escapeHtml(value === true ? 'yes' : value === false ? 'no' : value)}</dd></div>`).join('')}</dl>`);
+                }
+
+                summaryEl.innerHTML = parts.join('');
+            };
+
+            // The live value fields build the body of the two live-value calls.
+            const liveCalls = ['writeLiveValues', 'previewLiveValues'];
+            const teamCalls = ['listBatches', 'listOperators', 'createBattery', 'publishBattery'];
+            const liveForm = document.getElementById('tester-live-form');
+            const liveInputs = Array.from(document.querySelectorAll('[data-live-key]'));
+            const measuredAtEl = document.getElementById('tester-measured-at');
+            const measuredNowEl = document.getElementById('tester-measured-now');
+            const bodyField = document.getElementById('tester-body-field');
+            const bodyNote = document.getElementById('tester-body-note');
+            const methodTag = document.getElementById('tester-method-tag');
+            const teamNote = document.getElementById('tester-team-note');
+            const sampleLiveValues = { stateOfChargeSoC: 76, numberOfFullChargingAndDischargingCycles: 412, temperatureInformation: 23 };
+
+            const isoSeconds = (date) => date.toISOString().replace(/\.\d{3}Z$/, 'Z');
+            const buildLiveBody = () => {
+                const values = {};
+                liveInputs.forEach(input => {
+                    const text = (input.value || '').trim();
+                    if (text !== '' && !Number.isNaN(Number(text))) {
+                        values[input.getAttribute('data-live-key')] = Number(text);
+                    }
+                });
+                const measuredAt = measuredNowEl.checked || !measuredAtEl.value
+                    ? isoSeconds(new Date())
+                    : isoSeconds(new Date(measuredAtEl.value));
+                bodyEl.value = JSON.stringify({ measuredAt, values }, null, 2);
+                syncRawRequestFromControls();
+            };
+
+            const showCallParts = (templateKey) => {
+                const live = liveCalls.includes(templateKey);
+                liveForm.hidden = !live;
+                bodyNote.hidden = !live;
+                bodyField.hidden = (templates[templateKey]?.method || 'GET') === 'GET';
+                methodTag.textContent = templates[templateKey]?.method || 'GET';
+                teamNote.hidden = !teamCalls.includes(templateKey);
+                if (live) {
+                    if (liveInputs.every(input => !input.value)) {
+                        liveInputs.forEach(input => {
+                            const sample = sampleLiveValues[input.getAttribute('data-live-key')];
+                            input.value = sample === undefined ? '' : String(sample);
+                        });
+                    }
+                    buildLiveBody();
+                }
+            };
+
+            liveInputs.forEach(input => input.addEventListener('input', buildLiveBody));
+            measuredAtEl?.addEventListener('input', buildLiveBody);
+            measuredNowEl?.addEventListener('change', () => {
+                measuredAtEl.disabled = measuredNowEl.checked;
+                if (!measuredNowEl.checked && !measuredAtEl.value) {
+                    const local = new Date(Date.now() - new Date().getTimezoneOffset() * 60000);
+                    measuredAtEl.value = local.toISOString().slice(0, 19);
+                }
+                buildLiveBody();
+            });
+
             applyTemplate(templateSelect.value);
         })();
     </script>
```

- [ ] **Step 5: Delete the files**

```bash
git rm -q scripts/demo-telemetry-simulator.ps1
```

- [ ] **Step 6: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.DemoTelemetrySimulatorScriptTests|FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:    16`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2116`, none failed.

- [ ] **Step 7: Commit**

```bash
git add BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs web/wwwroot/css/bp-api-help.css web/Views/Help/Index.cshtml
git commit -m "feat(dpp): the API help page's workbench groups the calls, builds live values from fields and answers in plain words; the telemetry simulator goes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2116, Skipped:     0, Total:  2116`: the 2099 from before, 14 new in Task 1, 7 more in Task 2 (twenty new controller checks, thirteen older tests go) and 4 fewer after Task 3 (one new check, the simulator's five go).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

- [ ] **Step 4: Try the writing calls**

Start the app from your PC against `battery_pass_v3`, as for the earlier steps, with `APP_BASE_URL` set too. Never point it at `battery_pass_demo`. You need two tokens from **API Token Management**; each token's value is shown once, so keep it until the checks are done:

- a **team token**: global access, "Passport Lifecycle" access;
- a **cluster token**: "Read-write" access, for the cluster of a published battery (for example the sample batteries' cluster).

Then open **Help** (`/help`) and use the **Request workbench**, with "Your own token" and the token pasted in "Token value":

1. Under "API documentation", the writing rows are `batches`, `operators`, `batteries`, `batteries/{id}/live-values`, `live-values/history` and `batteries/{id}/publish`; nothing about telemetry, operations, battery model or software version. The "Call" list has three groups: Reading, Live values, Team token.
2. "List batches" with the team token: a table of batches (family, model, year, site). "List economic operators": a table of operators.
3. "Create battery" with the team token: in the body, put a batch ID and an operator ID from the two tables, one of your cluster IDs in `clusterId` (or remove that line), and a new serial number such as `API-CHECK-0001`. The answer is "201 Created", status `draft`, and the battery shows on **Batteries** as a draft.
4. "Publish battery" with the team token and that battery's ID: "409 Conflict", with what blocks publication listed (the new battery has no values yet). Remove the draft afterwards on its battery page.
5. "Send live values" with the cluster token and a published battery of its cluster: change state of charge to 55 and E1 to 120. The answer is "200 OK" with a table of saved values. The battery's page shows 55 % under live values, from "battery system".
6. "Read live value history 24h" with the cluster token: a table with the values just sent, each with when it was measured.
7. "Send live values" with the cluster token for a published battery of another cluster: "403 Forbidden", "This token cannot change this battery: it belongs to another cluster."
8. Switch to the dark theme and to a narrow window (about 390 px): the workbench stays readable and doesn't scroll sideways.

Delete the two tokens afterwards (API Token Management).

## After this plan

- **3d** removes the old model and pages and resets `battery_pass_v3`, with the old telemetry data and the four sample tokens.
- **Step 5** adds the bulk upload endpoint (`POST uploads`) with the BMS adapter, on the same save pipeline and live set.
- **Known small gaps:**
  - The sample tokens are cluster tokens: they send live values but can't create or publish; the team calls need a team token.
  - `docs/end-user-testing-guide.md` and `docs/qa-test-pack.md` still describe the old calls until step 6.
  - E1–E4 are in seconds, as the catalogue says (decision 28). If they change to minutes, the catalogue changes and the API, history and workbench follow it.

