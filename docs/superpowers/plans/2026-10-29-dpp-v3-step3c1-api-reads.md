# DPP V3 Step 3c-1: The API Reads the New Passports — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The external API reads the new passports, each answer cut to what the token may see by the same rule as the pages, and `/api/passports` becomes the team's read-only list. The API help page documents and tries the new reading calls. Writing through the API (creating batteries, live values, publication) follows in 3c-2.

**Architecture:** Builds on 3a's readers and filter and 3b's finding rules. `PassportApi` finds a published passport by battery ID (#6), passport ID (#5) or serial number with `PassportPages.LocateAsync`, reads it with `PassportReader` and cuts it with `PassportFilter.Cut` for the token's reader (`DppReaders.For`); it also lists a battery's versions, a schema section, chosen values with their origin (the detail page's rule, `PassportPages.Origin`), the readable values, and a cluster's batteries (`BatteryPages.ListAsync`). `DppApiController` serves the reading endpoints under `/api/external/v1` in place of the old controller's, which keeps only its writing endpoints until 3c-2. `PassportsApiController` becomes a read-only team list with a public signature check. The help page's reading rows, panels and workbench templates follow.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9 (GridFS), JsonSchema.Net 9.5.0, xUnit 2.9, EphemeralMongo 3.2.0. Plain CSS from `site.css`; nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 5, 8.4 and the 3c bullet of 14) and the decisions record (decisions 28 and 29). Earlier plans: steps 1, 2a–2d-2, 3a and 3b in `docs/superpowers/plans/`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at b777eb2 (3b with its fixes, decision 29 and the 3c split) plus the commit that adds this plan. Check with `git branch --show-current`.
- **Close the local app first.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Restart it after the plan.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  2061`. After this plan: 2099. The suite takes about a minute.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../3c1-task1-code.patch`), and run `git apply ../<name>` from the worktree root. `git apply` copes with this worktree's mixed line endings. Each patch applies to the files as the previous tasks left them, so keep the task order.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.
- **No data changes:** this plan changes no stored data. The four sample tokens stay until 3d's reset.

## Decisions taken with the product owner (29 Oct 2026)

1. **The external API is kept,** rebuilt on the new model and the new access rules (decision 29): the same calls, with the live values in place of telemetry. Operations, the battery-model label and the software version go in 3c-2.
2. **3c in two parts:** reading here (3c-1), writing in 3c-2 (creating batteries, the live values and their history, publication, the removed calls). Between the two, the old writing calls keep working on the old data.
3. **Creating batteries and publishing through the API** are for team (global) tokens only, as on the pages (design 6.1); this applies in 3c-2.
4. **The four sample tokens** stay until 3d's reset.
5. **The behaviour of the prototype** (what each kind of token reads) and the help page were approved as shown.

## Judgement calls in this plan

1. **One rule.** Every passport answer is `PassportFilter.Cut` for the token's reader: a cluster token reads like a user of its clusters, a global token like the team (3a). There is no other access check beside it, so a change to who sees what (for example an answer that gives the live values to legitimate-interest readers) changes the API by itself. A battery outside a cluster token's clusters therefore gives its public data, as the public summary does, where today's API answered 403. Only listing a cluster's batteries stays with that cluster's tokens and global tokens. A token's "unassigned batteries" setting has no meaning any more (a battery without a cluster gives purchaser data to nobody) and is ignored.
2. **Finding a battery:** `{id}` is a battery ID (#6, the current version), a passport ID (#5, that version) or a serial number, by 3b's rules; the colons of a battery ID may be written as they are or escaped. Today's "use the Battery ID, not the Passport ID" refusal goes.
3. **Published passports only,** to every token, global ones included, as on the pages (3b). A global token's cluster listing shows drafts with the status `draft` and no passport ID.
4. **Answers** keep today's envelope (`success`, `code`, `message`, `data`); the passport is in its BatteryPass-Ready v2.0.1 form.
5. **Sections** are the schema's eight section names (`dppHeader`, `identifiersAndProductData`, …, `performanceAndDurability`), case ignored. Today's names (`general`, `performance`, `operations`, `display`, `full`, …) are refused with the list (400); the whole passport is `GET batteries/{id}`.
6. **Values** are named by key (`stateOfChargeSoC`), number (`#63`, sent as `%2363`, or `63`), E code (`E1`) or path, case ignored. Each answer gives the attribute, key, name, unit (from the catalogue, so E1–E4 follow decision 28 if their unit changes), whether it exists, the value, and when and where it came from (the detail page's rule). A value the token may not see and a value the passport doesn't have look the same (`"exists": false`), so a token can't learn what it may not see. Today's aliases (`ratedEnergy`, `nickelMass`, …) name nothing.
7. **Readable values** (`/paths`) are the attributes the token sees a value of, with an optional `section`; today's document paths and `includeContainers` go.
8. **Versions** (`/passports`) are the battery's published versions, newest first, each with whether its signature still holds.
9. **`/api/passports`** is the team's (`AdminOnly`) and read-only: the list (the batteries list's search), one passport in full, and a public `/{id}/verify` that says whether a published version's signature holds (its IDs and status are public data). Creating, changing, validating, signing, publishing and archiving through it go: the team's pages do that, and the external API publishes for team tokens in 3c-2.
10. **The help page:** the reading rows, panels and workbench templates follow the new calls. The toolbar offers a published battery as the example for reading; the old sample battery stays for the writing templates until 3c-2.
11. **Older tests:** 14 source-assertion tests read the replaced code. 10 now check the same thing in the new files; 4 go with what they checked: the old `/api/passports` trust actions, its evidence section and its verify gating.

## File structure

| File | Responsibility |
|---|---|
| `web/Dpp/Api/PassportApi.cs` | The API's reading: a published passport cut for a token, its versions, sections, values, readable values and a cluster's batteries |
| `web/Dpp/Pages/PassportPages.cs` (modify) | The detail page's origin rule, shared with the API |
| `web/Dpp/DppServiceCollectionExtensions.cs` (modify) | Registers `PassportApi` |
| `web/Services/ExternalApiCredentials.cs` | The token an API request carries |
| `web/Controllers/DppApiController.cs` | The reading endpoints under `/api/external/v1` |
| `web/Controllers/ExternalApiController.cs` (modify) | Keeps only its writing endpoints until 3c-2 |
| `web/Controllers/PassportsApiController.cs` (replace) | The team's read-only passport list and the public signature check |
| `web/Views/Help/Index.cshtml`, `web/Controllers/HelpController.cs`, `web/Models/ViewModels/ExternalApiHelpViewModel.cs` (modify) | The help page's reading calls and example battery |


---

### Task 1: Reading passports for the API

One service answers every reading call with the pages' passports: found by the same rules, cut by the same filter.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PassportApiTests.cs`
- Create: `web/Dpp/Api/PassportApi.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`

- [ ] **Step 1: Write the failing tests**

Patch `3c1-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`). Save the block as `../3c1-task1-tests.patch`, outside the worktree, and run `git apply ../3c1-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
--- a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
@@ -1,6 +1,7 @@
 using BatteryPassWeb.Configuration;
 using BatteryPassWeb.Dpp;
 using BatteryPassWeb.Dpp.Access;
+using BatteryPassWeb.Dpp.Api;
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Forms;
@@ -41,6 +42,7 @@ public sealed class DppServiceWiringTests
         Assert.NotNull(provider.GetRequiredService<BatteryPages>());
         Assert.NotNull(provider.GetRequiredService<DppReaders>());
         Assert.NotNull(provider.GetRequiredService<ReaderPages>());
+        Assert.NotNull(provider.GetRequiredService<PassportApi>());
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Dpp/PassportApiTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The API's reading side (step 3c-1): the pages' passports and finding rules, cut by the one visibility rule.</summary>
[Collection(MongoCollection.Name)]
public sealed class PassportApiTests(MongoFixture mongo)
{
    private static readonly Reader Team = Reader.With("team@example.com", ReaderRole.Team);
    private static readonly Reader OwnCluster = Reader.With("token-north", ReaderRole.ClusterUser, BatteryTesting.North);
    private static readonly Reader OtherCluster = Reader.With("token-south", ReaderRole.ClusterUser, BatteryTesting.South);

    [Fact]
    public async Task APassport_ShouldBeFoundByBatteryIdPassportIdOrSerialNumber()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        var api = Api(rig);

        foreach (var id in new[] { PassportPagesTests.Identifier(rig), version.DigitalProductPassportId, Serial(rig) })
        {
            var passport = await api.ReadAsync(OwnCluster, id);
            Assert.Equal(version.Id, passport?.Version.Id);
            Assert.True(passport!.SignatureVerified);
        }

        Assert.Null(await api.ReadAsync(Team, "no-such-battery"));
    }

    [Fact]
    public async Task ADraft_ShouldNotBeServedEvenToTheTeam()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        Assert.Null(await Api(rig).ReadAsync(Team, PassportPagesTests.Identifier(rig)));
    }

    [Theory]
    [InlineData("team", true, true)]
    [InlineData("own-cluster", true, false)]
    [InlineData("other-cluster", false, false)]
    public async Task ThePassport_ShouldBeCutForTheReader(string who, bool seesPurchaserData, bool seesAuthoritiesData)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var passport = (await Api(rig).ReadAsync(ReaderFor(who), PassportPagesTests.Identifier(rig)))!;

        Assert.Equal(seesPurchaserData, Has(passport, 63));
        Assert.Equal(seesAuthoritiesData, Has(passport, 7));
        Assert.True(Has(passport, 11));
    }

    [Fact]
    public void TheSections_ShouldBeTheSchemasInItsOrder()
    {
        Assert.Equal(
            ["dppHeader", "identifiersAndProductData", "symbolsLabelsAndDocumentationOfConformity", "batteryCarbonFootprint", "supplyChainDueDiligence", "batteryMaterialsAndComposition", "circularityAndResourceEfficiency", "performanceAndDurability"],
            PassportApi.Sections);
        Assert.Equal("performanceAndDurability", PassportApi.SectionNamed(" PerformanceAndDurability "));
        Assert.Null(PassportApi.SectionNamed("performance"));
    }

    [Fact]
    public async Task ASection_ShouldHoldOnlyWhatTheReaderSees()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var api = Api(rig);

        var own = PassportApi.Section((await api.ReadAsync(OwnCluster, Serial(rig)))!, "performanceAndDurability");
        var other = PassportApi.Section((await api.ReadAsync(OtherCluster, Serial(rig)))!, "performanceAndDurability");
        var header = PassportApi.Section((await api.ReadAsync(OtherCluster, Serial(rig)))!, "dppHeader");

        Assert.True(own.ContainsKey("stateOfChargeSoC"));
        Assert.False(other.ContainsKey("stateOfChargeSoC"));
        Assert.True(other.ContainsKey("ratedCapacity"));
        Assert.False(header.ContainsKey("economicOperatorId"));
        Assert.True(header.ContainsKey("uniqueProductIdentifier"));
    }

    [Theory]
    [InlineData("stateOfChargeSoC", 63)]
    [InlineData("STATEOFCHARGESOC", 63)]
    [InlineData("#63", 63)]
    [InlineData("63", 63)]
    [InlineData("batteryPassportData.performanceAndDurability.stateOfChargeSoC", 63)]
    [InlineData("dppHeader.uniqueProductIdentifier", 6)]
    [InlineData("E1", 1001)]
    [InlineData("e4", 1004)]
    public void AValue_ShouldBeNamedByKeyNumberCodeOrPath(string request, int number)
    {
        Assert.Equal(number, PassportApi.AttributeNamed(request)?.Number);
    }

    [Theory]
    [InlineData("ratedEnergy")]
    [InlineData("#18")]
    [InlineData("E5")]
    [InlineData("aspects.performanceAndDurability.payload")]
    public void ARequestNamingNoAttribute_ShouldNameNone(string request)
    {
        Assert.Null(PassportApi.AttributeNamed(request));
    }

    [Fact]
    public async Task Values_ShouldComeWithTheirOriginOrNotAtAllForThisReader()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var api = Api(rig);

        var own = PassportApi.Values((await api.ReadAsync(OwnCluster, Serial(rig)))!, ["#63", "batteryMass", "nothing", " "]);
        var other = PassportApi.Values((await api.ReadAsync(OtherCluster, Serial(rig)))!, ["#63"]);

        Assert.Equal(["#63", "batteryMass", "nothing"], own.Select(value => value.Request));
        Assert.Equal(76, own[0].Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(new ValueOrigin(DppTestSeed.GeneratedAtUtc, "seed", Measured: true), own[0].Origin);
        Assert.Equal(21, own[1].Attribute!.Number);
        Assert.Null(own[2].Attribute);
        Assert.Null(own[2].Value);
        Assert.Equal(63, other[0].Attribute!.Number);
        Assert.Null(other[0].Value);
        Assert.Null(other[0].Origin);
    }

    [Fact]
    public async Task TheReadableValues_ShouldBeThoseTheReaderSeesAValueOf()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var api = Api(rig);

        var other = PassportApi.Readable((await api.ReadAsync(OtherCluster, Serial(rig)))!);
        var team = PassportApi.Readable((await api.ReadAsync(Team, Serial(rig)))!);
        var performance = PassportApi.Readable((await api.ReadAsync(Team, Serial(rig)))!, "performanceAndDurability");

        Assert.All(other, attribute => Assert.Equal(Audience.Public, attribute.Audience));
        Assert.Contains(team, attribute => attribute.Number == 7);
        Assert.Contains(team, attribute => attribute.Number == 63);
        Assert.All(performance, attribute => Assert.Equal("performanceAndDurability", attribute.Section));
        Assert.True(other.Count < team.Count);
    }

    [Fact]
    public async Task TheUnitsOfE1ToE4_ShouldComeFromTheCatalogue()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["timeSpentInExtremeTemperaturesAboveBoundary"] = JsonValue.Create(7200) }, "form"));

        var value = PassportApi.Values((await Api(rig).ReadAsync(OwnCluster, Serial(rig)))!, ["E1"]).Single();

        Assert.NotNull(value.Value);
        Assert.Equal(AttributeCatalog.Extensions[0].Unit, value.Attribute!.Unit);
    }

    [Fact]
    public async Task Versions_ShouldBeListedNewestFirst()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddHours(1);
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonValue.Create(520) }, "form"));

        var versions = await Api(rig).VersionsAsync((await Api(rig).ReadAsync(OtherCluster, Serial(rig)))!);

        Assert.Equal([2, 1], versions.Select(version => version.Number));
        Assert.Equal([DppStatuses.Active, DppStatuses.Archived], versions.Select(version => version.DppStatus));
        Assert.All(versions, version => Assert.True(PassportReader.Verify(version)));
    }

    [Fact]
    public async Task AClustersBatteries_ShouldListDraftsForTheTeamOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var creation = new PackCreationService(rig.Store, rig.Pipeline, new TestIdentifierScheme(), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock);
        var created = await creation.CreateAsync(PublicationTesting.Team, new NewPack(
            rig.Seed.Sources.Batch.Id,
            rig.Seed.Sources.Operator.Id,
            BatteryTesting.North,
            new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = JsonValue.Create("VPC9-3001") }));
        Assert.NotNull(created.Pack);
        var api = Api(rig);

        var team = await api.BatteriesAsync(Team, BatteryTesting.North);
        var own = await api.BatteriesAsync(OwnCluster, BatteryTesting.North);
        var south = await api.BatteriesAsync(Team, BatteryTesting.South);

        Assert.Equal(2, team.Count);
        Assert.Equal([rig.PackId], own.Select(row => row.PackId));
        Assert.Empty(south);
    }

    internal static PassportApi Api(PublicationRig rig) =>
        new(rig.Store, new PassportReader(rig.Store), PassportPagesTests.Pages(rig), BatteryTesting.Pages(rig));

    private static string Serial(PublicationRig rig) => rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value.GetValue<string>();

    private static bool Has(ApiPassport passport, int number)
    {
        JsonNode? node = passport.Passport;
        foreach (var name in AttributeCatalog.ByNumber(number).Path.Split('.'))
        {
            node = node?[name];
        }

        return node is not null;
    }

    private static Reader ReaderFor(string who) => who switch
    {
        "team" => Team,
        "own-cluster" => OwnCluster,
        _ => OtherCluster,
    };
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportApiTests"`
Expected: the build fails because the namespace `BatteryPassWeb.Dpp.Api` and `PassportApi` don't exist yet (CS0234, CS0246).

- [ ] **Step 3: Write `PassportApi.cs`**

`web/Dpp/Api/PassportApi.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Api;

/// <summary>A published passport as one API reader may see it, with the version it comes from.</summary>
public sealed record ApiPassport(PassportLocation Location, JsonObject Passport, bool SignatureVerified, PassportSources Sources)
{
    public PassportVersion Version => Location.Version;
}

/// <summary>
/// One requested value: the attribute the request names (null when it names none), and the value when the reader may
/// see it and the passport has it, with the stored value's origin while it is still the one shown.
/// </summary>
public sealed record ApiValue(string Request, AttributeDefinition? Attribute, JsonNode? Value, ValueOrigin? Origin);

/// <summary>
/// The external API's reading side (decision 29, step 3c-1): the same passports, finding rules and visibility filter as
/// the pages. Every answer is cut by <see cref="PassportFilter"/>, so a change to who sees what changes the API too.
/// </summary>
public sealed class PassportApi(DppStore store, PassportReader reader, PassportPages pages, BatteryPages batteries)
{
    /// <summary>The passport's sections in the schema's order: the header, then the data sections.</summary>
    public static IReadOnlyList<string> Sections { get; } = AttributeCatalog.All.Select(attribute => attribute.Section).Distinct().ToArray();

    /// <summary>
    /// The published passport a battery ID (#6), passport ID (#5) or serial number names, cut for the reader; null when
    /// there is none. Drafts aren't served, to anyone (J-6): the team sees them on its battery page.
    /// </summary>
    public async Task<ApiPassport?> ReadAsync(Reader who, string? id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(who);
        var location = await pages.LocateAsync(id, cancellationToken);
        var published = location is null ? null : await reader.VersionAsync(location.Version.DigitalProductPassportId, cancellationToken);
        var sources = location is null ? null : await store.LoadSourcesAsync(location.Version.PackId, cancellationToken);
        if (location is null || published is null || sources is null)
        {
            return null;
        }

        var clusters = await store.FindPublishedClusterIdsUsingAsync(packId: location.Version.PackId, cancellationToken: cancellationToken);
        return new ApiPassport(location, PassportFilter.Cut(published.Passport, who, clusters), PassportReader.Verify(location.Version), sources);
    }

    /// <summary>The battery's published versions, newest first.</summary>
    public async Task<IReadOnlyList<PassportVersion>> VersionsAsync(ApiPassport passport, CancellationToken cancellationToken = default) =>
        (await store.ListVersionsAsync(passport.Version.PackId, cancellationToken)).OrderByDescending(version => version.Number).ToArray();

    /// <summary>
    /// A cluster's batteries as the reader may list them: every battery for the team; for anyone else only those with a
    /// published passport, since drafts stay with the team (J-6).
    /// </summary>
    public async Task<IReadOnlyList<BatteryRow>> BatteriesAsync(Reader who, string clusterId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(who);
        var team = who.Roles.Contains(ReaderRole.Team);
        return (await batteries.ListAsync(null, cancellationToken)).Rows
            .Where(row => string.Equals(row.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase) && (team || !row.IsDraft))
            .ToArray();
    }

    /// <summary>The section a name gives, ignoring case (e.g. "performanceAndDurability"); null when it names none.</summary>
    public static string? SectionNamed(string? name) =>
        Sections.FirstOrDefault(section => section.Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>A section of the cut passport: what the reader sees there, which may be nothing.</summary>
    public static JsonObject Section(ApiPassport passport, string section)
    {
        ArgumentNullException.ThrowIfNull(passport);
        var node = section == AttributeDefinition.HeaderSection
            ? passport.Passport[section]
            : passport.Passport[AttributeDefinition.DataRoot]?[section];
        return node is JsonObject found ? (JsonObject)found.DeepClone() : new JsonObject();
    }

    /// <summary>
    /// The attribute a request names: its property name (stateOfChargeSoC), its label (#63, 63, E1) or its path in the
    /// passport (batteryPassportData.performanceAndDurability.stateOfChargeSoC), ignoring case.
    /// </summary>
    public static AttributeDefinition? AttributeNamed(string? request)
    {
        var text = request?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        var label = text.StartsWith('#') || text.StartsWith("E", StringComparison.OrdinalIgnoreCase) ? text : $"#{text}";
        return AttributeCatalog.All.FirstOrDefault(attribute =>
            attribute.Property.Equals(text, StringComparison.OrdinalIgnoreCase)
            || attribute.Label.Equals(label, StringComparison.OrdinalIgnoreCase)
            || attribute.Path.Equals(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Each requested value, in the order asked.</summary>
    public static IReadOnlyList<ApiValue> Values(ApiPassport passport, IEnumerable<string> requests)
    {
        ArgumentNullException.ThrowIfNull(passport);
        ArgumentNullException.ThrowIfNull(requests);
        return requests
            .Where(request => !string.IsNullOrWhiteSpace(request))
            .Select(request =>
            {
                var attribute = AttributeNamed(request);
                var value = attribute is null ? null : Find(passport.Passport, attribute);
                var origin = attribute is null || value is null ? null : PassportPages.Origin(attribute, passport.Sources, value);
                return new ApiValue(request.Trim(), attribute, value?.DeepClone(), origin);
            })
            .ToArray();
    }

    /// <summary>The attributes this reader sees a value of, in catalogue order; one section only when it is given.</summary>
    public static IReadOnlyList<AttributeDefinition> Readable(ApiPassport passport, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(passport);
        return AttributeCatalog.All
            .Where(attribute => (section is null || attribute.Section == section) && Find(passport.Passport, attribute) is not null)
            .ToArray();
    }

    /// <summary>#4 as the passport states it, for listings.</summary>
    public static string Timestamp(DateTime utc) => PassportAssembler.FormatTimestamp(utc);

    private static JsonNode? Find(JsonObject passport, AttributeDefinition attribute)
    {
        JsonNode? node = passport;
        foreach (var name in attribute.Path.Split('.'))
        {
            node = node?[name];
        }

        return node;
    }
}
```

- [ ] **Step 4: Share the origin rule and register the service**

Patch `3c1-task1-code.patch` (`web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Pages/PassportPages.cs`). Save the block as `../3c1-task1-code.patch`, outside the worktree, and run `git apply ../3c1-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -1,4 +1,5 @@
 using BatteryPassWeb.Dpp.Access;
+using BatteryPassWeb.Dpp.Api;
 using BatteryPassWeb.Dpp.Audit;
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
@@ -45,6 +46,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<BatteryPages>();
         services.AddSingleton<DppReaders>();
         services.AddSingleton<ReaderPages>();
+        services.AddSingleton<PassportApi>();
         services.AddSingleton<PassportPages>();
         services.AddSingleton<AuditLines>();
         return services;
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -250,9 +250,10 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
 
     /// <summary>
     /// The stored value's time and source, when the stored value is still the one shown. An archived version may show a
-    /// value that has changed since; its origin isn't known any more, so none is given. #1–#5 are set by the app.
+    /// value that has changed since; its origin isn't known any more, so none is given. #1–#5 are set by the app. The
+    /// detail page and the API (3c) both show it.
     /// </summary>
-    private static ValueOrigin? Origin(AttributeDefinition attribute, PassportSources sources, JsonNode? shown)
+    public static ValueOrigin? Origin(AttributeDefinition attribute, PassportSources sources, JsonNode? shown)
     {
         var stored = attribute.Storage switch
         {
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportApiTests"`
Expected: `Passed!  - Failed:     0, Passed:    26`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2085`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs BatteryPassWeb.Tests/Dpp/PassportApiTests.cs web/Dpp/Api/PassportApi.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Pages/PassportPages.cs
git commit -m "feat(dpp): read passports for the API through the pages' finding rules and the one visibility filter" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 2: The reading endpoints and the team's passport list

The reading endpoints move to `DppApiController`, on the same addresses; the old controller keeps only its writing endpoints until 3c-2. `/api/passports` becomes read-only and the team's. Fourteen older source-assertion tests read the replaced code: ten now check the same thing in the new files, four go with what they checked.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AnonymousVerifyAccessTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`
- Test (modify): `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs`
- Create: `web/Controllers/DppApiController.cs`
- Replace: `web/Controllers/PassportsApiController.cs`
- Create: `web/Services/ExternalApiCredentials.cs`
- Modify: `web/Controllers/ExternalApiController.cs`

- [ ] **Step 1: Write the failing tests**

Patch `3c1-task2-tests.patch` (`BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`, `BatteryPassWeb.Tests/AnonymousVerifyAccessTests.cs`, `BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs`, `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`, `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`, `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`, `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`, `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`, `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`). Save the block as `../3c1-task2-tests.patch`, outside the worktree, and run `git apply ../3c1-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
@@ -150,7 +150,7 @@ public sealed class AdminFeedbackImplementationTests
     [Fact]
     public void ExternalApi_ShouldListAccessibleClustersAndUseLifecycleTokens()
     {
-        var api = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
+        var api = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
         var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
         var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
diff --git a/BatteryPassWeb.Tests/AnonymousVerifyAccessTests.cs b/BatteryPassWeb.Tests/AnonymousVerifyAccessTests.cs
--- a/BatteryPassWeb.Tests/AnonymousVerifyAccessTests.cs
+++ b/BatteryPassWeb.Tests/AnonymousVerifyAccessTests.cs
@@ -2,32 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class AnonymousVerifyAccessTests
 {
-    [Fact]
-    public void PublicVerifyEndpoint_ShouldGateTrustDataBehindPublicVisibilityOrInternalTrustAccess()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
-        var verify = ExtractMethod(controller, "Verify");
-
-        Assert.Contains("AccessControlService", controller);
-        Assert.Contains("_accessControlService", controller);
-        Assert.Contains("_passportPublishPolicyService.IsPubliclyVisible(passport)", verify);
-        Assert.Contains("User.Identity?.IsAuthenticated != true", verify);
-        Assert.Contains("_accessControlService.CanOpenPassportDetailAsync(User, passport, _passportPublishPolicyService, cancellationToken)", verify);
-        Assert.Contains("_accessControlService.CanViewTrustConformanceAsync(User, BsonHelpers.GetString(passport, \"clusterId\"), cancellationToken)", verify);
-
-        Assert.True(
-            verify.IndexOf("_passportPublishPolicyService.IsPubliclyVisible(passport)", StringComparison.Ordinal)
-            < verify.IndexOf("_passportTrustService.Verify(passport)", StringComparison.Ordinal),
-            "Public visibility must be checked before trust verification is returned.");
-
-        Assert.True(
-            verify.IndexOf("_accessControlService.CanOpenPassportDetailAsync", StringComparison.Ordinal)
-            < verify.IndexOf("_passportTrustService.Verify(passport)", StringComparison.Ordinal),
-            "Internal access must be checked before trust verification is returned.");
-
-        Assert.Contains("return NotFound", verify);
-        Assert.Contains("trust = BsonHelpers.ToDotNet", verify);
-    }
 
     [Fact]
     public void PublicVisibilityPolicy_ShouldRemainTheAnonymousVerificationBoundary()
diff --git a/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs b/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs
--- a/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs
+++ b/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs
@@ -5,17 +5,9 @@ public sealed class AntiForgeryProtectionTests
     [Fact]
     public void CookieAuthenticatedJsonMutationApis_ShouldRequireAntiForgeryTokens()
     {
-        var passportsApi = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
         var filesApi = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));
         var authApi = File.ReadAllText(RepoFile("web", "Controllers", "AuthApiController.cs"));
 
-        AssertMethodHasAttribute(passportsApi, "Create", "ValidateAntiForgeryToken");
-        AssertMethodHasAttribute(passportsApi, "Update", "ValidateAntiForgeryToken");
-        AssertMethodHasAttribute(passportsApi, "Validate", "ValidateAntiForgeryToken");
-        AssertMethodHasAttribute(passportsApi, "Sign", "ValidateAntiForgeryToken");
-        AssertMethodHasAttribute(passportsApi, "Publish", "ValidateAntiForgeryToken");
-        AssertMethodHasAttribute(passportsApi, "Archive", "ValidateAntiForgeryToken");
-
         AssertMethodHasAttribute(filesApi, "Upload", "ValidateAntiForgeryToken");
         AssertMethodHasAttribute(authApi, "Login", "ValidateAntiForgeryToken");
         AssertMethodHasAttribute(authApi, "Logout", "ValidateAntiForgeryToken");
diff --git a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
--- a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
+++ b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
@@ -128,7 +128,6 @@ public sealed class ApplicationAuditCoverageTests
     {
         var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
         var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
-        var passportsApi = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
         var snapshot = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));
 
         Assert.Contains("\"battery.updated\"", clusterAdmin);
@@ -136,10 +135,6 @@ public sealed class ApplicationAuditCoverageTests
         Assert.Contains("\"battery.operations.updated\"", externalApi);
         Assert.Contains("\"battery.model.updated\"", externalApi);
         Assert.Contains("\"battery.software.updated\"", externalApi);
-        Assert.Contains("\"passport.created\"", passportsApi);
-        Assert.Contains("\"passport.updated\"", passportsApi);
-        Assert.Contains("\"passport.archived\"", passportsApi);
-        Assert.Contains("\"passport.validated\"", passportsApi);
         Assert.Contains("\"passport.created\"", snapshot);
         Assert.Contains("\"battery.passport.created\"", snapshot);
     }
diff --git a/BatteryPassWeb.Tests/ConformanceLayoutTests.cs b/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
--- a/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
@@ -2,15 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class ConformanceLayoutTests
 {
-    [Fact]
-    public void PassportsApiController_ShouldExposeValidateEndpoint()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
-
-        Assert.Contains("[HttpPost(\"{passportId}/validate\")]", source);
-        Assert.Contains("PassportValidationService", source);
-        Assert.Contains("UpdateTrustValidationAsync", source);
-    }
 
     [Fact]
     public void AdminController_ShouldExposeConformancePageAndValidateAction()
@@ -238,17 +229,6 @@ public sealed class ConformanceLayoutTests
         Assert.Contains("AddSingleton<PassportReadinessService>", program);
     }
 
-    [Fact]
-    public void AdminAndApiControllers_ShouldAppendEvidenceValidationSection()
-    {
-        var api = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
-
-
-        Assert.Contains("PassportEvidenceService", api);
-        Assert.Contains("_passportEvidenceService.Evaluate", api);
-        Assert.Contains("AppendValidationSection", api);
-    }
-
     [Fact]
     public void ConformanceView_ShouldRenderOneGuidedNextActionAndCollapsibleDiagnostics()
     {
diff --git a/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs b/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs
--- a/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs
+++ b/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs
@@ -5,14 +5,11 @@ public sealed class ErrorHandlingRolloutTests
     [Fact]
     public void TrustChangingAdminActions_ShouldShowServiceErrorInsteadOfClaimingSuccess()
     {
-        var api = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
         var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
         var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
 
         Assert.Contains("if (!trustUpdated)", workflow);
         Assert.Contains("if (!published)", workflow);
-        Assert.Contains("StatusCodes.Status503ServiceUnavailable", api);
-        Assert.Contains("retrySafe = true", api);
         Assert.Contains("Task<bool> UpdateTrustSignatureAsync", repository);
         Assert.Contains("Task<bool> PublishPassportAsync", repository);
         Assert.Contains("result.MatchedCount > 0", repository);
diff --git a/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs b/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
@@ -7,7 +7,6 @@ public sealed class ExternalApiBatteryIdTests
     {
         var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
 
-        Assert.Contains("[HttpGet(\"batteries/{batteryId}\")]", source);
         Assert.Contains("[HttpPost(\"batteries/{batteryId}/telemetry\")]", source);
         Assert.Contains("[HttpPatch(\"batteries/{batteryId}/battery-model\")]", source);
         Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports\")]", source);
@@ -48,12 +47,12 @@ public sealed class ExternalApiBatteryIdTests
     [Fact]
     public void ExternalApi_ShouldListBatteryPassportsAndUsePassportLifecycleLabel()
     {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
+        var controller = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
         var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
         var seed = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
 
-        Assert.Contains("[HttpGet(\"batteries/{batteryId}/passports\")]", controller);
-        Assert.Contains("ListBatteryPassports", controller);
+        Assert.Contains("[HttpGet(\"batteries/{id}/passports\")]", controller);
+        Assert.Contains("api.VersionsAsync", controller);
         Assert.Contains("Passport Lifecycle", help);
         Assert.DoesNotContain("create, validate, sign and publish passport", help);
         Assert.Contains("Sample token (Passport Lifecycle)", seed);
diff --git a/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs b/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
--- a/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
+++ b/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
@@ -28,12 +28,10 @@ public sealed class SigningWorkflowLayoutTests
     {
         var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
 
-        Assert.Contains("[HttpPost(\"{passportId}/sign\")]", source);
-        Assert.Contains("[HttpPost(\"{passportId}/publish\")]", source);
+        Assert.DoesNotContain("HttpPost", source);
         Assert.Contains("[AllowAnonymous]", source);
-        Assert.Contains("[HttpGet(\"{passportId}/verify\")]", source);
-        Assert.Contains("PassportTrustService", source);
-        Assert.Contains("AuditRevisionService", source);
+        Assert.Contains("[HttpGet(\"{id}/verify\")]", source);
+        Assert.Contains("PassportReader.Verify(version)", source);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs b/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
--- a/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
@@ -19,18 +19,6 @@ public sealed class ValidationPolicyLayoutTests
         Assert.DoesNotContain("verification[\"signedAt\"] = now", source);
     }
 
-    [Fact]
-    public void PassportsApiController_ShouldBlockDirectPublishAndSanitizeTrustClaims()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
-
-        Assert.Contains("PassportPublishPolicyService", source);
-        Assert.Contains("RejectDirectPublishRequest", source);
-        Assert.Contains("SanitizeTrustClaimsForDraftSave", source);
-        Assert.Contains("Direct publish is blocked", source);
-        Assert.DoesNotContain("MarkCanonicalDirtyAsync", source);
-    }
-
     [Fact]
     public void EditPassportView_ShouldNotExposeDirectPublishedStatusSelection()
     {
```

`BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The external API's reading endpoints (step 3c-1) and the team-only passports list, called with real tokens.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppApiControllerTests(MongoFixture mongo)
{
    private const string Key = "3c1-test-key-for-the-token-hashes-000";

    [Fact]
    public void TheReadingEndpoints_ShouldBeGetsUnderTheApisAddressWithTheReadLimit()
    {
        var type = typeof(DppApiController);

        Assert.Equal("api/external/v1", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Null(type.GetCustomAttribute<AuthorizeAttribute>());
        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(
            ["batteries/{id}", "batteries/{id}/passports", "batteries/{id}/paths", "batteries/{id}/section/{sectionName}", "batteries/{id}/values", "clusters", "clusters/{clusterId}/batteries"],
            actions.Select(action => action.GetCustomAttribute<HttpGetAttribute>()!.Template).Order());
        Assert.All(actions, action => Assert.Equal(SecurityRateLimitPolicyNames.ExternalApiRead, action.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName));
    }

    [Fact]
    public void TheOldController_ShouldKeepOnlyItsWritingEndpointsUntil3c2()
    {
        var gets = typeof(ExternalApiController).GetMethods()
            .Select(method => method.GetCustomAttribute<HttpGetAttribute>()?.Template)
            .OfType<string>()
            .ToArray();

        Assert.Equal(["batteries/{batteryId}/telemetry/history"], gets);
    }

    [Theory]
    [InlineData(null, StatusCodes.Status401Unauthorized)]
    [InlineData("not-a-token", StatusCodes.Status401Unauthorized)]
    public async Task WithoutAValidToken_NothingShouldBeRead(string? token, int status)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = Assert.IsType<ObjectResult>(await Controller(rig, token).Battery(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.Equal(status, result.StatusCode);
    }

    [Theory]
    [InlineData("north", true, false)]
    [InlineData("south", false, false)]
    [InlineData("global", true, true)]
    public async Task ABattery_ShouldBeReadCutToTheToken(string token, bool seesStateOfCharge, bool seesOperatorId)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var data = Data(await Controller(rig, tokens[token]).Battery(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.Equal(PassportPagesTests.Identifier(rig), data.GetProperty("batteryId").GetString());
        Assert.Equal(1, data.GetProperty("version").GetInt32());
        Assert.Equal("active", data.GetProperty("dppStatus").GetString());
        Assert.True(data.GetProperty("signatureVerified").GetBoolean());
        var passport = data.GetProperty("passport");
        Assert.Equal(seesStateOfCharge, passport.GetProperty("batteryPassportData").GetProperty("performanceAndDurability").TryGetProperty("stateOfChargeSoC", out _));
        Assert.Equal(seesOperatorId, passport.GetProperty("dppHeader").TryGetProperty("economicOperatorId", out _));
    }

    [Fact]
    public async Task AnUnknownIdOrADraft_ShouldBeNotFound()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var draft = Assert.IsType<ObjectResult>(await Controller(rig, tokens["global"]).Battery(PassportPagesTests.Identifier(rig), CancellationToken.None));
        var unknown = Assert.IsType<ObjectResult>(await Controller(rig, tokens["global"]).Passports("no-such-battery", CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, draft.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, unknown.StatusCode);
        Assert.Equal(DppApiController.NoPassport, Json(draft).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("north", new[] { BatteryTesting.North })]
    [InlineData("global", new[] { BatteryTesting.North, BatteryTesting.South })]
    public async Task Clusters_ShouldBeTheTokensOwnOrAllForAGlobalToken(string token, string[] expected)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var data = Data(await Controller(rig, tokens[token]).Clusters(CancellationToken.None));

        Assert.Equal(token == "global", data.GetProperty("globalAccess").GetBoolean());
        Assert.Equal(expected.Order(), data.GetProperty("clusters").EnumerateArray().Select(cluster => cluster.GetProperty("clusterId").GetString()).Order());
    }

    [Fact]
    public async Task AClustersBatteries_ShouldBeListedToItsOwnTokensOnly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var own = Data(await Controller(rig, tokens["north"]).ClusterBatteries(BatteryTesting.North, CancellationToken.None));
        var other = Assert.IsType<ObjectResult>(await Controller(rig, tokens["south"]).ClusterBatteries(BatteryTesting.North, CancellationToken.None));
        var missing = Assert.IsType<ObjectResult>(await Controller(rig, tokens["global"]).ClusterBatteries("no-such-cluster", CancellationToken.None));

        var battery = Assert.Single(own.GetProperty("batteries").EnumerateArray());
        Assert.Equal(PassportPagesTests.Identifier(rig), battery.GetProperty("batteryId").GetString());
        Assert.Equal("active", battery.GetProperty("dppStatus").GetString());
        Assert.Equal(StatusCodes.Status403Forbidden, other.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ASection_ShouldBeOneSchemaSectionAndAnUnknownNameRefused()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var section = Data(await Controller(rig, tokens["north"]).Section(PassportPagesTests.Identifier(rig), "PerformanceAndDurability", CancellationToken.None));
        var unknown = Assert.IsType<ObjectResult>(await Controller(rig, tokens["north"]).Section(PassportPagesTests.Identifier(rig), "performance", CancellationToken.None));

        Assert.Equal("performanceAndDurability", section.GetProperty("section").GetString());
        Assert.True(section.GetProperty("value").TryGetProperty("stateOfChargeSoC", out _));
        Assert.Equal(StatusCodes.Status400BadRequest, unknown.StatusCode);
        Assert.Contains("performanceAndDurability", Json(unknown).GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Values_ShouldAnswerEachRequestWithItsUnitAndOrigin()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var values = Data(await Controller(rig, tokens["south"]).Values(PassportPagesTests.Identifier(rig), ["batteryMass", "#63", "ratedEnergy"], CancellationToken.None))
            .GetProperty("values").EnumerateArray().ToArray();
        var none = Assert.IsType<ObjectResult>(await Controller(rig, tokens["south"]).Values(PassportPagesTests.Identifier(rig), [], CancellationToken.None));

        Assert.Equal("#21", values[0].GetProperty("attribute").GetString());
        Assert.Equal("kg", values[0].GetProperty("unit").GetString());
        Assert.True(values[0].GetProperty("exists").GetBoolean());
        Assert.Equal("test data", values[0].GetProperty("source").GetString());
        Assert.Equal("2026-10-28T09:30:00Z", values[0].GetProperty("generatedAt").GetString());
        Assert.False(values[1].GetProperty("exists").GetBoolean());
        Assert.Equal("No value this token may see.", values[1].GetProperty("message").GetString());
        Assert.Equal("Not an attribute of the passport.", values[2].GetProperty("message").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, none.StatusCode);
    }

    [Fact]
    public async Task Paths_ShouldListWhatTheTokenCanRead()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var south = Keys(await Controller(rig, tokens["south"]).Paths(PassportPagesTests.Identifier(rig), null, CancellationToken.None));
        var north = Keys(await Controller(rig, tokens["north"]).Paths(PassportPagesTests.Identifier(rig), "performanceAndDurability", CancellationToken.None));
        var unknown = Assert.IsType<ObjectResult>(await Controller(rig, tokens["north"]).Paths(PassportPagesTests.Identifier(rig), "operations", CancellationToken.None));

        Assert.Contains("batterySerialNumber", south);
        Assert.DoesNotContain("stateOfChargeSoC", south);
        Assert.Contains("stateOfChargeSoC", north);
        Assert.DoesNotContain("batterySerialNumber", north);
        Assert.Equal(StatusCodes.Status400BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Passports_ShouldListTheVersions()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);

        var versions = Data(await Controller(rig, tokens["south"]).Passports(PassportPagesTests.Identifier(rig), CancellationToken.None))
            .GetProperty("passports").EnumerateArray().ToArray();

        var version = Assert.Single(versions);
        Assert.Equal(1, version.GetProperty("version").GetInt32());
        Assert.True(version.GetProperty("signatureVerified").GetBoolean());
    }

    [Fact]
    public void ThePassportsList_ShouldBeTheTeamsWithAPublicSignatureCheck()
    {
        var type = typeof(PassportsApiController);

        Assert.Equal("AdminOnly", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            ["Detail", "List", "Verify"],
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(method => method.Name).Order());
        Assert.NotNull(type.GetMethod("Verify")!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(type.GetMethod("Detail")!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task ThePassportsList_ShouldListEveryBatteryAndShowOnePassportInFull()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = PassportsController(rig);

        var list = Json(Assert.IsType<OkObjectResult>(await controller.List(null, CancellationToken.None)));
        var detail = Json(Assert.IsType<OkObjectResult>(await controller.Detail(PassportPagesTests.Identifier(rig), CancellationToken.None)));
        var verify = Json(Assert.IsType<OkObjectResult>(await controller.Verify(PassportPagesTests.Identifier(rig), CancellationToken.None)));
        var missing = await controller.Verify("no-such-passport", CancellationToken.None);

        Assert.Single(list.GetProperty("passports").EnumerateArray());
        Assert.True(detail.GetProperty("passport").GetProperty("dppHeader").TryGetProperty("economicOperatorId", out _));
        Assert.True(verify.GetProperty("signatureVerified").GetBoolean());
        Assert.IsType<NotFoundObjectResult>(missing);
    }

    /// <summary>Tokens for the seed pack's cluster (north), another cluster (south) and the team (global).</summary>
    private static async Task<Dictionary<string, string>> TokensAsync(PublicationRig rig)
    {
        var repository = Repository(rig);
        return new Dictionary<string, string>
        {
            ["north"] = (await repository.CreateTokenAsync("north", ExternalTokenAccessMode.Read, [BatteryTesting.North], false, false, "test")).PlainToken,
            ["south"] = (await repository.CreateTokenAsync("south", ExternalTokenAccessMode.Read, [BatteryTesting.South], false, false, "test")).PlainToken,
            ["global"] = (await repository.CreateTokenAsync("global", ExternalTokenAccessMode.Read, [], false, true, "test")).PlainToken,
        };
    }

    private static ExternalApiRepository Repository(PublicationRig rig) => new(rig.Context, new ExternalApiSecurityService(Key));

    private static DppApiController Controller(PublicationRig rig, string? token)
    {
        var http = new DefaultHttpContext();
        if (token is not null)
        {
            http.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":"));
        }

        return new DppApiController(Repository(rig), new ClusterRepository(rig.Context), PassportApiTests.Api(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private static PassportsApiController PassportsController(PublicationRig rig) => new(
        new DppReaders(new ClusterRepository(rig.Context)),
        BatteryTesting.Pages(rig),
        PassportApiTests.Api(rig),
        PassportPagesTests.Pages(rig))
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Email, "team@example.com"), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)],
                    "test")),
            },
        },
    };

    private static JsonElement Json(ObjectResult result) => JsonSerializer.SerializeToElement(result.Value);

    private static JsonElement Data(IActionResult result)
    {
        var answer = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, answer.StatusCode);
        return Json(answer).GetProperty("data");
    }

    private static string[] Keys(IActionResult result) =>
        Data(result).GetProperty("attributes").EnumerateArray().Select(attribute => attribute.GetProperty("key").GetString()!).ToArray();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AnonymousVerifyAccessTests|FullyQualifiedName~BatteryPassWeb.Tests.AntiForgeryProtectionTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.ConformanceLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppApiControllerTests|FullyQualifiedName~BatteryPassWeb.Tests.ErrorHandlingRolloutTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryIdTests|FullyQualifiedName~BatteryPassWeb.Tests.SigningWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.ValidationPolicyLayoutTests"`
Expected: the build fails because `DppApiController` doesn't exist yet (CS0246).

- [ ] **Step 3: Write `DppApiController.cs`**

`web/Controllers/DppApiController.cs`:

```csharp
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The external API's reading side on the new model (decision 29, step 3c-1). A token is read as a reader
/// (<see cref="DppReaders.For"/>): a cluster token like a user of its clusters, a global token like the team. Every
/// passport answer is cut by the same rule as the pages, so a battery outside the token's clusters gives public data.
/// </summary>
[ApiController]
[Route("api/external/v1")]
public sealed class DppApiController(ExternalApiRepository tokens, ClusterRepository clusters, PassportApi api) : ControllerBase
{
    public const string NoPassport = "No published battery passport has this ID.";

    [HttpGet("clusters")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Clusters(CancellationToken cancellationToken)
    {
        var (reader, error) = await ReaderAsync(cancellationToken);
        if (reader is null)
        {
            return error!;
        }

        var listed = (await clusters.ListClustersAsync(cancellationToken))
            .Select(cluster => (Id: BsonHelpers.GetString(cluster, "clusterId"), Name: BsonHelpers.GetString(cluster, "name")))
            .Where(cluster => MayList(reader, cluster.Id))
            .Select(cluster => new { clusterId = cluster.Id, name = cluster.Name })
            .ToArray();
        return Envelope(StatusCodes.Status200OK, "Accessible clusters read successfully.", new
        {
            globalAccess = reader.Roles.Contains(ReaderRole.Team),
            clusters = listed,
        });
    }

    [HttpGet("clusters/{clusterId}/batteries")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> ClusterBatteries(string clusterId, CancellationToken cancellationToken)
    {
        var (reader, error) = await ReaderAsync(cancellationToken);
        if (reader is null)
        {
            return error!;
        }

        var id = ClusterRepository.NormalizeClusterId(clusterId);
        if (!MayList(reader, id))
        {
            return Envelope(StatusCodes.Status403Forbidden, "Token cannot access this cluster.");
        }

        if (await clusters.GetClusterByIdAsync(id, cancellationToken) is not { } cluster)
        {
            return Envelope(StatusCodes.Status404NotFound, "Cluster was not found.");
        }

        var rows = await api.BatteriesAsync(reader, id, cancellationToken);
        return Envelope(StatusCodes.Status200OK, "Cluster batteries read successfully.", new
        {
            clusterId = id,
            clusterName = BsonHelpers.GetString(cluster, "name"),
            batteries = rows.Select(row => new
            {
                batteryId = row.Identifier,
                serialNumber = row.SerialNumber,
                batteryFamily = row.FamilyName,
                batteryModel = row.ModelName,
                passportId = row.Active?.DigitalProductPassportId,
                version = row.Active?.Number,
                dppStatus = row.Active?.DppStatus ?? "draft",
            }).ToArray(),
        });
    }

    [HttpGet("batteries/{id}")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Battery(string id, CancellationToken cancellationToken)
    {
        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        return Envelope(StatusCodes.Status200OK, "Battery passport read successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            version = passport.Version.Number,
            dppStatus = passport.Version.DppStatus,
            signatureVerified = passport.SignatureVerified,
            passport = passport.Passport,
        });
    }

    [HttpGet("batteries/{id}/passports")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Passports(string id, CancellationToken cancellationToken)
    {
        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        var versions = await api.VersionsAsync(passport, cancellationToken);
        return Envelope(StatusCodes.Status200OK, "Battery passport versions read successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passports = versions.Select(version => new
            {
                passportId = version.DigitalProductPassportId,
                version = version.Number,
                dppStatus = version.DppStatus,
                publishedAt = PassportApi.Timestamp(version.PublishedAtUtc),
                lastUpdated = PassportApi.Timestamp(version.LastUpdatedUtc),
                signatureVerified = PassportReader.Verify(version),
            }).ToArray(),
        });
    }

    [HttpGet("batteries/{id}/section/{sectionName}")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Section(string id, string sectionName, CancellationToken cancellationToken)
    {
        if (PassportApi.SectionNamed(sectionName) is not { } section)
        {
            return Envelope(StatusCodes.Status400BadRequest, $"Unknown section '{sectionName}'. Sections: {string.Join(", ", PassportApi.Sections)}.");
        }

        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        return Envelope(StatusCodes.Status200OK, "Section read successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            section,
            value = PassportApi.Section(passport, section),
        });
    }

    [HttpGet("batteries/{id}/values")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Values(string id, [FromQuery(Name = "path")] string[] paths, CancellationToken cancellationToken)
    {
        if (!paths.Any(path => !string.IsNullOrWhiteSpace(path)))
        {
            return Envelope(StatusCodes.Status400BadRequest, "At least one 'path' query parameter is required, e.g. path=stateOfChargeSoC or path=%2363.");
        }

        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        return Envelope(StatusCodes.Status200OK, "Values read successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            values = PassportApi.Values(passport, paths).Select(value => new
            {
                request = value.Request,
                attribute = value.Attribute?.Label,
                key = value.Attribute?.Property,
                name = value.Attribute?.Name,
                unit = value.Attribute?.Unit,
                exists = value.Value is not null,
                value = value.Value,
                generatedAt = value.Origin is null ? null : PassportApi.Timestamp(value.Origin.GeneratedAtUtc),
                source = value.Origin is null ? null : PassportPages.SourceWord(value.Origin.Source),
                message = value.Attribute is null
                    ? "Not an attribute of the passport."
                    : value.Value is null ? "No value this token may see." : null,
            }).ToArray(),
        });
    }

    [HttpGet("batteries/{id}/paths")]
    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
    public async Task<IActionResult> Paths(string id, [FromQuery] string? section, CancellationToken cancellationToken)
    {
        var wanted = string.IsNullOrWhiteSpace(section) || section.Trim().Equals("full", StringComparison.OrdinalIgnoreCase)
            ? null
            : PassportApi.SectionNamed(section);
        if (!string.IsNullOrWhiteSpace(section) && !section.Trim().Equals("full", StringComparison.OrdinalIgnoreCase) && wanted is null)
        {
            return Envelope(StatusCodes.Status400BadRequest, $"Unknown section '{section}'. Sections: {string.Join(", ", PassportApi.Sections)}.");
        }

        var (passport, error) = await PassportAsync(id, cancellationToken);
        if (passport is null)
        {
            return error!;
        }

        return Envelope(StatusCodes.Status200OK, "Readable values listed successfully.", new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            section = wanted ?? "full",
            attributes = PassportApi.Readable(passport, wanted).Select(attribute => new
            {
                attribute = attribute.Label,
                key = attribute.Property,
                name = attribute.Name,
                section = attribute.Section,
                path = attribute.Path,
                unit = attribute.Unit,
            }).ToArray(),
        });
    }

    /// <summary>The token's reader; on failure the answer to send (401, 403 or 503, as before).</summary>
    private async Task<(Reader? Reader, IActionResult? Error)> ReaderAsync(CancellationToken cancellationToken)
    {
        if (!tokens.IsAvailable)
        {
            return (null, Envelope(StatusCodes.Status503ServiceUnavailable, "Database is not connected."));
        }

        var validation = await tokens.ValidateTokenAsync(ExternalApiCredentials.Token(Request), ExternalTokenRequirement.Read, cancellationToken);
        return validation.Success && validation.Context is not null
            ? (DppReaders.For(validation.Context), null)
            : (null, Envelope(validation.StatusCode, validation.Message));
    }

    private async Task<(ApiPassport? Passport, IActionResult? Error)> PassportAsync(string id, CancellationToken cancellationToken)
    {
        var (reader, error) = await ReaderAsync(cancellationToken);
        if (reader is null)
        {
            return (null, error);
        }

        var passport = await api.ReadAsync(reader, id, cancellationToken);
        return passport is null ? (null, Envelope(StatusCodes.Status404NotFound, NoPassport)) : (passport, null);
    }

    /// <summary>A cluster's batteries are listed to the team and to the cluster's own tokens.</summary>
    private static bool MayList(Reader reader, string clusterId) =>
        reader.Roles.Contains(ReaderRole.Team) || reader.ClusterIds.Contains(ClusterRepository.NormalizeClusterId(clusterId));

    private ObjectResult Envelope(int statusCode, string message, object? data = null) =>
        StatusCode(statusCode, new { success = statusCode is >= 200 and < 300, code = statusCode, message, data });
}
```

- [ ] **Step 4: Replace `PassportsApiController.cs`**

`web/Controllers/PassportsApiController.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The list of all passports, for the team only (design 5.2), on the new model (step 3c-1). Passports are created,
/// changed and published on the team's pages; the external API publishes for team tokens in 3c-2. Anyone may check a
/// published version's signature.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/passports")]
public sealed class PassportsApiController(DppReaders readers, BatteryPages batteries, PassportApi api, PassportPages pages) : ControllerBase
{
    /// <summary>Every battery with its current passport, found by part of a passport ID, battery ID, serial number or cluster.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var list = await batteries.ListAsync(q, cancellationToken);
        return Ok(new
        {
            query = list.Query,
            total = list.Total,
            passports = list.Rows.Select(row => new
            {
                batteryId = row.Identifier,
                serialNumber = row.SerialNumber,
                batteryFamily = row.FamilyName,
                batteryModel = row.ModelName,
                batch = row.BatchLabel,
                clusterId = row.ClusterId,
                passportId = row.Active?.DigitalProductPassportId,
                version = row.Active?.Number,
                dppStatus = row.Active?.DppStatus ?? "draft",
            }).ToArray(),
        });
    }

    /// <summary>A published passport in full: the team sees every attribute.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken)
    {
        var passport = await api.ReadAsync(await readers.ForAsync(User, cancellationToken), id, cancellationToken);
        if (passport is null)
        {
            return NotFound(new { error = DppApiController.NoPassport, id });
        }

        return Ok(new
        {
            batteryId = passport.Location.Identifier,
            passportId = passport.Version.DigitalProductPassportId,
            version = passport.Version.Number,
            dppStatus = passport.Version.DppStatus,
            signatureVerified = passport.SignatureVerified,
            passport = passport.Passport,
        });
    }

    /// <summary>Whether a published version's stored core is still the one signed. Its IDs and status are public data.</summary>
    [AllowAnonymous]
    [HttpGet("{id}/verify")]
    public async Task<IActionResult> Verify(string id, CancellationToken cancellationToken)
    {
        var version = await pages.FindAsync(id, cancellationToken);
        if (version is null)
        {
            return NotFound(new { error = DppApiController.NoPassport, id });
        }

        return Ok(new
        {
            passportId = version.DigitalProductPassportId,
            version = version.Number,
            dppStatus = version.DppStatus,
            signatureVerified = PassportReader.Verify(version),
        });
    }
}
```

- [ ] **Step 5: Write `ExternalApiCredentials.cs`**

`web/Services/ExternalApiCredentials.cs`:

```csharp
using System.Text;
using Microsoft.AspNetCore.Http;

namespace BatteryPassWeb.Services;

/// <summary>
/// The token an external API request carries: HTTP Basic with the token as user name ("token:") or, for older clients,
/// the raw token after "Basic ".
/// </summary>
public static class ExternalApiCredentials
{
    public static string Token(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = request.Headers.Authorization.FirstOrDefault();
        const string prefix = "Basic ";
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var raw = authorization[prefix.Length..].Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        return TryDecodeBasic(raw, out var token) ? token : raw;
    }

    private static bool TryDecodeBasic(string raw, out string token)
    {
        token = string.Empty;
        try
        {
            var bytes = Convert.FromBase64String(raw);
            if (bytes.Length == 0 || bytes.Length > 512)
            {
                return false;
            }

            var decoded = Encoding.UTF8.GetString(bytes);
            if (decoded.Length == 0 || decoded.Any(character => character is < ' ' or > '~'))
            {
                return false;
            }

            var separator = decoded.IndexOf(':');
            var candidate = separator <= 0 ? string.Empty : decoded[..separator].Trim();
            if (candidate.Length == 0)
            {
                return false;
            }

            token = candidate;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 6: Keep only the writing endpoints in the old controller and make /api/passports the team's**

Patch `3c1-task2-code.patch` (`web/Controllers/ExternalApiController.cs`). Save the block as `../3c1-task2-code.patch`, outside the worktree, and run `git apply ../3c1-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/ExternalApiController.cs b/web/Controllers/ExternalApiController.cs
--- a/web/Controllers/ExternalApiController.cs
+++ b/web/Controllers/ExternalApiController.cs
@@ -15,39 +15,6 @@ public class ExternalApiController : ControllerBase
 {
     private const string UnknownBatteryModelMessage = "Unknown Battery Model.";
 
-    private static readonly IReadOnlyDictionary<string, string> SectionPathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
-    {
-        ["general"] = "aspects.generalProductInformation.payload",
-        ["materialcomposition"] = "aspects.materialComposition.payload",
-        ["performance"] = "aspects.performanceAndDurability.payload",
-        ["circularity"] = "aspects.circularity.payload",
-        ["supplychain"] = "aspects.supplyChainDueDiligence.payload",
-        ["carbonfootprint"] = "aspects.carbonFootprintForBatteries.payload",
-        ["compliance"] = "aspects.labeling.payload",
-        ["operations"] = "app.operations",
-        ["display"] = "app.display",
-        ["full"] = string.Empty
-    };
-
-    private static readonly IReadOnlyDictionary<string, string> ParameterAliasPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
-    {
-        ["ratedEnergy"] = "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy",
-        ["ratedCapacity"] = "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedCapacity",
-        ["ratedMaximumPower"] = "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedMaximumPower",
-        ["nominalVoltage"] = "aspects.performanceAndDurability.payload.batteryTechicalProperties.nominalVoltage",
-        ["nickelMass"] = "@nickelMass",
-        ["currentConsumptionKwh"] = "app.operations.latestTelemetry.currentConsumptionKwh",
-        ["currentChargeLevelPct"] = "app.operations.latestTelemetry.currentChargeLevelPct",
-        ["currentVoltageV"] = "app.operations.latestTelemetry.currentVoltageV",
-        ["currentCurrentA"] = "app.operations.latestTelemetry.currentCurrentA",
-        ["softwareVersion"] = "app.product.softwareVersion",
-        ["softwareReleaseDate"] = "app.product.softwareReleaseDate",
-        ["softwareLatestUpdate"] = "app.product.softwareLatestUpdate",
-        ["locationOfUse"] = "app.operations.locationOfUse",
-        ["contactPerson"] = "app.operations.contactPerson",
-        ["isActive"] = "app.operations.isActive"
-    };
-
     private readonly PassportRepository _passportRepository;
     private readonly BatteryRepository _batteryRepository;
     private readonly ClusterRepository _clusterRepository;
@@ -89,73 +56,6 @@ public class ExternalApiController : ControllerBase
         _options = options.Value;
     }
 
-    [HttpGet("clusters")]
-    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
-    public async Task<IActionResult> ListAccessibleClusters(CancellationToken cancellationToken)
-    {
-        var auth = await AuthorizeExternalApiAsync(ExternalTokenRequirement.Read, cancellationToken);
-        if (auth.ErrorResult != null)
-        {
-            return auth.ErrorResult;
-        }
-
-        var tokenContext = auth.TokenContext!;
-        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var accessibleClusters = clusters
-            .Where(cluster => CanAccessCluster(tokenContext, BsonHelpers.GetString(cluster, "clusterId")))
-            .Select(cluster => new
-            {
-                clusterId = BsonHelpers.GetString(cluster, "clusterId"),
-                name = BsonHelpers.GetString(cluster, "name")
-            })
-            .ToList();
-
-        return Envelope(StatusCodes.Status200OK, "Accessible clusters read successfully.", new
-        {
-            globalAccess = tokenContext.GlobalAccess,
-            allowUnassignedBatteries = tokenContext.AllowUnassigned,
-            clusters = accessibleClusters
-        });
-    }
-
-    [HttpGet("clusters/{clusterId}/batteries")]
-    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
-    public async Task<IActionResult> ListClusterBatteries(string clusterId, CancellationToken cancellationToken)
-    {
-        var auth = await AuthorizeExternalApiAsync(ExternalTokenRequirement.Read, cancellationToken);
-        if (auth.ErrorResult != null)
-        {
-            return auth.ErrorResult;
-        }
-
-        var normalizedClusterId = ClusterRepository.NormalizeClusterId(clusterId);
-        if (!CanAccessCluster(auth.TokenContext!, normalizedClusterId))
-        {
-            return Envelope(StatusCodes.Status403Forbidden, "Token cannot access this cluster scope.");
-        }
-
-        var cluster = await _clusterRepository.GetClusterByIdAsync(normalizedClusterId, cancellationToken);
-        if (cluster == null)
-        {
-            return Envelope(StatusCodes.Status404NotFound, "Cluster was not found.");
-        }
-
-        var batteries = await _batteryRepository.ListByClusterIdAsync(normalizedClusterId, cancellationToken);
-        return Envelope(StatusCodes.Status200OK, "Cluster batteries read successfully.", new
-        {
-            clusterId = normalizedClusterId,
-            clusterName = BsonHelpers.GetString(cluster, "name"),
-            batteries = batteries.Select(battery => new
-            {
-                batteryId = BsonHelpers.GetString(battery, "batteryId"),
-                batteryFamily = BsonHelpers.GetString(battery, "identity", "batteryFamily"),
-                batteryModel = BsonHelpers.GetString(battery, "identity", "batteryModel"),
-                serialNumber = BsonHelpers.GetString(battery, "identity", "serialNumber"),
-                newPassportRequired = BatteryRequiresNewPassport(battery)
-            }).ToList()
-        });
-    }
-
     [HttpPost("batteries")]
     [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)]
     public async Task<IActionResult> CreateBattery([FromBody] JsonElement payload, CancellationToken cancellationToken)
@@ -202,214 +102,6 @@ public class ExternalApiController : ControllerBase
         });
     }
 
-    [HttpGet("batteries/{batteryId}")]
-    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
-    public async Task<IActionResult> GetBattery(string batteryId, CancellationToken cancellationToken)
-    {
-        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
-        if (auth.ErrorResult != null)
-        {
-            return auth.ErrorResult;
-        }
-
-        var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
-        if (passport == null)
-        {
-            return Envelope(StatusCodes.Status404NotFound, "No passport snapshot exists for this battery.");
-        }
-
-        return Envelope(StatusCodes.Status200OK, "Battery data read successfully.", new
-        {
-            batteryId,
-            passportId = BsonHelpers.GetString(passport, "passportId"),
-            battery = BsonHelpers.ToDotNet(passport)
-        });
-    }
-
-    [HttpGet("batteries/{batteryId}/passports")]
-    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
-    public async Task<IActionResult> ListBatteryPassports(string batteryId, CancellationToken cancellationToken)
-    {
-        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
-        if (auth.ErrorResult != null)
-        {
-            return auth.ErrorResult;
-        }
-
-        var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: false, cancellationToken);
-        return Envelope(StatusCodes.Status200OK, "Battery passports read successfully.", new
-        {
-            batteryId,
-            passports = passports.Select(passport => new
-            {
-                passportId = BsonHelpers.GetString(passport, "passportId"),
-                status = PassportRepository.BuildPassportStatusLabel(passport),
-                createdAt = BsonHelpers.GetString(passport, "snapshot", "createdAt"),
-                isLatestForBattery = passport.GetValue("isLatestForBattery", false).ToBoolean()
-            }).ToList()
-        });
-    }
-
-    [HttpGet("batteries/{batteryId}/section/{sectionName}")]
-    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
-    public async Task<IActionResult> GetBatterySection(string batteryId, string sectionName, CancellationToken cancellationToken)
-    {
-        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
-        if (auth.ErrorResult != null)
-        {
-            return auth.ErrorResult;
-        }
-        var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
-        if (passport == null)
-        {
-            return Envelope(StatusCodes.Status404NotFound, "No passport snapshot exists for this battery.");
-        }
-
-        if (!SectionPathByName.TryGetValue(sectionName, out var sectionPath))
-        {
-            return Envelope(StatusCodes.Status400BadRequest, $"Unknown section '{sectionName}'.");
-        }
-
-        if (string.IsNullOrWhiteSpace(sectionPath))
-        {
-            return Envelope(StatusCodes.Status200OK, "Section read successfully.", new
-            {
-                batteryId,
-                passportId = BsonHelpers.GetString(passport, "passportId"),
-                section = sectionName,
-                value = BsonHelpers.ToDotNet(passport)
-            });
-        }
-
-        var sectionValue = ResolvePathValue(passport, sectionPath);
-        if (sectionValue == null)
-        {
-            return Envelope(StatusCodes.Status404NotFound, $"Section '{sectionName}' does not exist for this battery.");
-        }
-
-        return Envelope(StatusCodes.Status200OK, "Section read successfully.", new
-        {
-            batteryId,
-            passportId = BsonHelpers.GetString(passport, "passportId"),
-            section = sectionName,
-            path = sectionPath,
-            value = BsonHelpers.ToDotNet(sectionValue)
-        });
-    }
-
-    [HttpGet("batteries/{batteryId}/values")]
-    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
-    public async Task<IActionResult> GetBatteryValues(string batteryId, [FromQuery(Name = "path")] string[] paths, CancellationToken cancellationToken)
-    {
-        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
-        if (auth.ErrorResult != null)
-        {
-            return auth.ErrorResult;
-        }
-        var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
-        if (passport == null)
-        {
-            return Envelope(StatusCodes.Status404NotFound, "No passport snapshot exists for this battery.");
-        }
-
-        if (paths.Length == 0)
-        {
-            return Envelope(StatusCodes.Status400BadRequest, "At least one 'path' query parameter is required.");
-        }
-
-        var results = new List<object>();
-        foreach (var rawPath in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
-        {
-            var resolvedPath = ResolveParameterPath(rawPath.Trim());
-            var value = ResolvePathValue(passport, resolvedPath);
-            results.Add(new
-            {
-                requestPath = rawPath,
-                resolvedPath,
-                exists = value != null,
-                value = BsonHelpers.ToDotNet(value)
-            });
-        }
-
-        return Envelope(StatusCodes.Status200OK, "Values read successfully.", new
-        {
-            batteryId,
-            passportId = BsonHelpers.GetString(passport, "passportId"),
-            values = results
-        });
-    }
-
-    [HttpGet("batteries/{batteryId}/paths")]
-    [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)]
-    public async Task<IActionResult> GetBatteryPaths(
-        string batteryId,
-        [FromQuery] string? section,
-        [FromQuery] bool includeContainers,
-        CancellationToken cancellationToken)
-    {
-        var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
-        if (auth.ErrorResult != null)
-        {
-            return auth.ErrorResult;
-        }
-        var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
-        if (passport == null)
-        {
-            return Envelope(StatusCodes.Status404NotFound, "No passport snapshot exists for this battery.");
-        }
-
-        var requestedSection = string.IsNullOrWhiteSpace(section) ? "full" : section.Trim();
-        BsonValue rootValue;
-        string rootPath;
-        if (requestedSection.Equals("full", StringComparison.OrdinalIgnoreCase))
-        {
-            rootValue = passport;
-            rootPath = string.Empty;
-        }
-        else
-        {
-            if (!SectionPathByName.TryGetValue(requestedSection, out var sectionPath))
-            {
-                return Envelope(StatusCodes.Status400BadRequest, $"Unknown section '{requestedSection}'.");
-            }
-
-            rootPath = sectionPath;
-            rootValue = ResolvePathValue(passport, sectionPath) ?? BsonNull.Value;
-            if (rootValue.IsBsonNull)
-            {
-                return Envelope(StatusCodes.Status404NotFound, $"Section '{requestedSection}' does not exist for this battery.");
-            }
-        }
-
-        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
-        CollectReadablePaths(rootValue, rootPath, includeContainers, paths);
-
-        var aliases = ParameterAliasPath
-            .Where(item => IsAliasRelevantForSection(item.Value, requestedSection))
-            .Select(item =>
-            {
-                var value = ResolvePathValue(passport, item.Value);
-                return new
-                {
-                    alias = item.Key,
-                    path = item.Value,
-                    exists = value != null
-                };
-            })
-            .ToList();
-
-        return Envelope(StatusCodes.Status200OK, "Readable paths resolved successfully.", new
-        {
-            batteryId,
-            passportId = BsonHelpers.GetString(passport, "passportId"),
-            section = requestedSection,
-            rootPath = string.IsNullOrWhiteSpace(rootPath) ? "(full document)" : rootPath,
-            includeContainers,
-            aliases,
-            paths
-        });
-    }
-
     [HttpPost("batteries/{batteryId}/telemetry")]
     [EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)]
     public async Task<IActionResult> WriteTelemetry(string batteryId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
@@ -1044,145 +736,6 @@ public class ExternalApiController : ControllerBase
         }
     }
 
-    private static string ResolveParameterPath(string pathOrAlias)
-    {
-        if (ParameterAliasPath.TryGetValue(pathOrAlias, out var resolvedPath))
-        {
-            return resolvedPath;
-        }
-
-        return pathOrAlias;
-    }
-
-    private static bool IsAliasRelevantForSection(string aliasPath, string section)
-    {
-        if (section.Equals("full", StringComparison.OrdinalIgnoreCase))
-        {
-            return true;
-        }
-
-        if (section.Equals("materialComposition", StringComparison.OrdinalIgnoreCase))
-        {
-            return aliasPath.StartsWith("aspects.materialComposition.", StringComparison.OrdinalIgnoreCase)
-                || aliasPath.Equals("@nickelMass", StringComparison.OrdinalIgnoreCase);
-        }
-
-        if (!SectionPathByName.TryGetValue(section, out var sectionPath))
-        {
-            return false;
-        }
-
-        if (string.IsNullOrWhiteSpace(sectionPath))
-        {
-            return true;
-        }
-
-        return aliasPath.StartsWith(sectionPath + ".", StringComparison.OrdinalIgnoreCase)
-            || aliasPath.Equals(sectionPath, StringComparison.OrdinalIgnoreCase);
-    }
-
-    private static BsonValue? ResolvePathValue(BsonDocument root, string path)
-    {
-        if (string.IsNullOrWhiteSpace(path))
-        {
-            return root;
-        }
-
-        if (path.Equals("@nickelMass", StringComparison.OrdinalIgnoreCase))
-        {
-            var materialPayload = ResolvePathValue(root, "aspects.materialComposition.payload.batteryMaterials");
-            if (materialPayload is BsonArray rows)
-            {
-                var nickel = rows
-                    .OfType<BsonDocument>()
-                    .FirstOrDefault(row =>
-                    {
-                        var materialName = row.GetValue("batteryMaterialName", string.Empty).ToString() ?? string.Empty;
-                        return materialName.Equals("Nickel", StringComparison.OrdinalIgnoreCase);
-                    });
-                if (nickel != null)
-                {
-                    return nickel.GetValue("batteryMaterialMass", BsonNull.Value);
-                }
-            }
-
-            return null;
-        }
-
-        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
-        BsonValue current = root;
-        foreach (var segment in segments)
-        {
-            if (current is BsonDocument document)
-            {
-                if (!document.TryGetValue(segment, out current))
-                {
-                    return null;
-                }
-                continue;
-            }
-
-            if (current is BsonArray array)
-            {
-                if (!int.TryParse(segment, out var index) || index < 0 || index >= array.Count)
-                {
-                    return null;
-                }
-
-                current = array[index];
-                continue;
-            }
-
-            return null;
-        }
-
-        return current;
-    }
-
-    private static void CollectReadablePaths(BsonValue value, string currentPath, bool includeContainers, ISet<string> paths)
-    {
-        if (value is BsonDocument document)
-        {
-            if (includeContainers && !string.IsNullOrWhiteSpace(currentPath))
-            {
-                paths.Add(currentPath);
-            }
-
-            foreach (var element in document.Elements)
-            {
-                var nextPath = string.IsNullOrWhiteSpace(currentPath)
-                    ? element.Name
-                    : $"{currentPath}.{element.Name}";
-                CollectReadablePaths(element.Value, nextPath, includeContainers, paths);
-            }
-
-            return;
-        }
-
-        if (value is BsonArray array)
-        {
-            if (includeContainers && !string.IsNullOrWhiteSpace(currentPath))
-            {
-                paths.Add(currentPath);
-            }
-
-            for (var index = 0; index < array.Count; index++)
-            {
-                var nextPath = string.IsNullOrWhiteSpace(currentPath)
-                    ? index.ToString()
-                    : $"{currentPath}.{index}";
-                CollectReadablePaths(array[index], nextPath, includeContainers, paths);
-            }
-
-            return;
-        }
-
-        if (!string.IsNullOrWhiteSpace(currentPath))
-        {
-            paths.Add(currentPath);
-        }
-    }
-
     private IActionResult Envelope(int statusCode, string message, object? data = null)
     {
         return StatusCode(statusCode, new
```

- [ ] **Step 7: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AnonymousVerifyAccessTests|FullyQualifiedName~BatteryPassWeb.Tests.AntiForgeryProtectionTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.ConformanceLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppApiControllerTests|FullyQualifiedName~BatteryPassWeb.Tests.ErrorHandlingRolloutTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryIdTests|FullyQualifiedName~BatteryPassWeb.Tests.SigningWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.ValidationPolicyLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:    79`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2098`, none failed.

- [ ] **Step 8: Commit**

```bash
git add BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs BatteryPassWeb.Tests/AnonymousVerifyAccessTests.cs BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs BatteryPassWeb.Tests/ConformanceLayoutTests.cs BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs BatteryPassWeb.Tests/Dpp/DppApiControllerTests.cs web/Controllers/DppApiController.cs web/Controllers/PassportsApiController.cs web/Services/ExternalApiCredentials.cs web/Controllers/ExternalApiController.cs
git commit -m "feat(dpp): the external API reads the new passports, cut to the token, and /api/passports is the team's" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 3: The help page's reading calls

Razor views compile with the build, so a mistake in the view fails Step 2's build like one in C#.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`
- Modify: `web/Controllers/HelpController.cs`
- Modify: `web/Models/ViewModels/ExternalApiHelpViewModel.cs`
- Modify: `web/Views/Help/Index.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `3c1-task3-tests.patch` (`BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`). Save the block as `../3c1-task3-tests.patch`, outside the worktree, and run `git apply ../3c1-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
--- a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
+++ b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
@@ -163,12 +163,26 @@ public sealed class HelpWorkbenchLayoutTests
             attribute => attribute is Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute);
     }
 
+    [Fact]
+    public void HelpPage_ShouldOfferAPublishedBatteryForTheReadingCalls()
+    {
+        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
+        var controller = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));
+
+        Assert.Contains("Example battery ID for reading", markup);
+        Assert.Contains("No battery passport is published yet.", markup);
+        Assert.Contains("value=\"@(string.IsNullOrWhiteSpace(Model.ExampleBatteryId) ? Model.SampleBatteryId : Model.ExampleBatteryId)\"", markup);
+        Assert.Contains("Rows.FirstOrDefault(row => !row.IsDraft)?.Identifier", controller);
+        Assert.Contains("BatteryPassWeb.Dpp.Api.PassportApi.Sections", markup);
+        Assert.DoesNotContain("<code>materialComposition</code>", markup);
+    }
+
     [Fact]
     public void HelpWorkbenchValuesTemplate_ShouldTargetValuesEndpoint()
     {
         var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
 
-        Assert.Contains("/values?path=ratedEnergy&path=ratedCapacity&path=nickelMass", markup);
+        Assert.Contains("/values?path=stateOfChargeSoC&path=%2311&path=batteryMass", markup);
         Assert.Contains("Values read successfully", markup);
     }
 
@@ -246,7 +260,7 @@ public sealed class HelpWorkbenchLayoutTests
 
         Assert.Contains("id=\"supported-values-link\"", markup);
         Assert.Contains("target=\"_blank\"", markup);
-        Assert.Contains("/paths?section=full&amp;includeContainers=true", markup);
+        Assert.Contains("/batteries/{Model.ExampleBatteryId}/paths", markup);
         Assert.Contains("openSupportedValues", markup);
     }
 
@@ -255,9 +269,9 @@ public sealed class HelpWorkbenchLayoutTests
     {
         var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
 
-        Assert.Contains("Copy any item from", markup);
-        Assert.Contains("/values?path=aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy&amp;path=aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedCapacity", markup);
-        Assert.Contains("/values?path=app.operations.locationOfUse&amp;path=app.operations.contactPerson", markup);
+        Assert.Contains("its attribute number (<code>%2363</code> for #63", markup);
+        Assert.Contains("/values?path=stateOfChargeSoC&amp;path=%2311", markup);
+        Assert.Contains("/paths?section=performanceAndDurability", markup);
     }
 
     [Fact]
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests"`
Expected: `Failed:     4, Passed:    12`: the three help-page checks changed in Step 1 (`HelpDocumentation_ShouldExposeReadablePathsNewTabLink`, `HelpDocumentation_ShouldShowHowPathsAreReusedWithValues`, `HelpWorkbenchValuesTemplate_ShouldTargetValuesEndpoint`) and the new `HelpPage_ShouldOfferAPublishedBatteryForTheReadingCalls`.

- [ ] **Step 3: Document and try the new reading calls**

Patch `3c1-task3-code.patch` (`web/Controllers/HelpController.cs`, `web/Models/ViewModels/ExternalApiHelpViewModel.cs`, `web/Views/Help/Index.cshtml`). Save the block as `../3c1-task3-code.patch`, outside the worktree, and run `git apply ../3c1-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/HelpController.cs b/web/Controllers/HelpController.cs
--- a/web/Controllers/HelpController.cs
+++ b/web/Controllers/HelpController.cs
@@ -1,4 +1,5 @@
 using BatteryPassWeb.Configuration;
+using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Models.ViewModels;
 using BatteryPassWeb.Services;
 using Microsoft.AspNetCore.Authorization;
@@ -16,14 +17,17 @@ public class HelpController : Controller
     private readonly BatteryIdService _batteryIdService;
     private readonly AccessControlService _accessControlService;
     private readonly BatteryPassOptions _options;
+    private readonly BatteryPages _batteryPages;
 
     public HelpController(
         ExternalApiRepository externalApiRepository,
         PassportRepository passportRepository,
         BatteryIdService batteryIdService,
         AccessControlService accessControlService,
-        IOptions<BatteryPassOptions> options)
+        IOptions<BatteryPassOptions> options,
+        BatteryPages batteryPages)
     {
+        _batteryPages = batteryPages;
         _externalApiRepository = externalApiRepository;
         _passportRepository = passportRepository;
         _batteryIdService = batteryIdService;
@@ -46,6 +50,7 @@ public class HelpController : Controller
             BasePath = "/api/external/v1",
             PublicBaseUrl = RequestBaseUrl(),
             SampleBatteryId = sampleIds.BatteryId,
+            ExampleBatteryId = (await _batteryPages.ListAsync(null, cancellationToken)).Rows.FirstOrDefault(row => !row.IsDraft)?.Identifier ?? string.Empty,
             SamplePassportId = sampleIds.PassportId,
             SampleReadToken = _options.EnableDemoData && _options.EnablePublicDemoReadToken && readTokenDocument != null
                 ? ExternalApiInitializer.SampleReadTokenValue
diff --git a/web/Models/ViewModels/ExternalApiHelpViewModel.cs b/web/Models/ViewModels/ExternalApiHelpViewModel.cs
--- a/web/Models/ViewModels/ExternalApiHelpViewModel.cs
+++ b/web/Models/ViewModels/ExternalApiHelpViewModel.cs
@@ -5,6 +5,9 @@ public sealed class ExternalApiHelpViewModel
     public string BasePath { get; init; } = "/api/external/v1";
     public string PublicBaseUrl { get; init; } = string.Empty;
     public string SampleBatteryId { get; init; } = string.Empty;
+
+    /// <summary>A published battery's ID (#6), to try the reading calls on; empty when none is published yet.</summary>
+    public string ExampleBatteryId { get; init; } = string.Empty;
     public string SamplePassportId { get; init; } = string.Empty;
     public string SampleReadToken { get; init; } = string.Empty;
     public string SampleReadWriteToken { get; init; } = string.Empty;
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -21,7 +21,18 @@
                 <code>@Model.BasePath</code>
             </div>
             <div>
-                <p class="mb-1 text-secondary small">Sample Battery ID for these tokens</p>
+                <p class="mb-1 text-secondary small">Example battery ID for reading</p>
+                @if (!string.IsNullOrWhiteSpace(Model.ExampleBatteryId))
+                {
+                    <span class="bp-id-copy-row"><code class="bp-id-value">@Model.ExampleBatteryId</code>@await Html.PartialAsync("_CopyIdButton", Model.ExampleBatteryId)</span>
+                }
+                else
+                {
+                    <span class="text-secondary small">No battery passport is published yet.</span>
+                }
+            </div>
+            <div>
+                <p class="mb-1 text-secondary small">Sample Battery ID for writing and passport actions</p>
                 <span class="bp-id-copy-row"><code class="bp-id-value">@Model.SampleBatteryId</code>@await Html.PartialAsync("_CopyIdButton", Model.SampleBatteryId)</span>
             </div>
             <div>
@@ -110,9 +121,9 @@
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">GET</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/clusters/{clusterId}/batteries</code><small>Battery IDs in one cluster</small></span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/clusters/{clusterId}/batteries</code><small>Batteries of one cluster, with their passports</small></span></td>
                             <td><span class="bp-help-access-pill">Read</span></td>
-                            <td>List batteries before reading or patching one.</td>
+                            <td>List a cluster's batteries before reading one. Drafts are listed for global tokens only.</td>
                             <td class="bp-action-cell">
                                 <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-cluster-batteries" aria-expanded="false" aria-controls="api-detail-cluster-batteries" aria-label="Show cluster batteries endpoint details" title="Show cluster batteries endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
@@ -132,9 +143,9 @@
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">GET</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}</code><small>Latest passport snapshot</small></span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}</code><small>The published passport, as this token may see it</small></span></td>
                             <td><span class="bp-help-access-pill">Read</span></td>
-                            <td>Read the complete battery passport response.</td>
+                            <td>Read a battery's whole passport by its battery ID, passport ID or serial number.</td>
                             <td class="bp-action-cell">
                                 <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-battery" aria-expanded="false" aria-controls="api-detail-battery" aria-label="Show full battery endpoint details" title="Show full battery endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
@@ -143,9 +154,9 @@
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">GET</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/section/{sectionName}</code><small>One named section</small></span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/section/{section}</code><small>One section of the passport</small></span></td>
                             <td><span class="bp-help-access-pill">Read</span></td>
-                            <td>Keep responses focused on one passport area.</td>
+                            <td>Keep responses to one part of the passport.</td>
                             <td class="bp-action-cell">
                                 <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-section" aria-expanded="false" aria-controls="api-detail-section" aria-label="Show section endpoint details" title="Show section endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
@@ -154,9 +165,9 @@
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">GET</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/values?path=...</code><small>Alias or exact document paths</small></span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/values?path=...</code><small>By key, attribute number or path</small></span></td>
                             <td><span class="bp-help-access-pill">Read</span></td>
-                            <td>Pull a small set of named values.</td>
+                            <td>Pull a few values, each with when and where it came from.</td>
                             <td class="bp-action-cell">
                                 <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-values" aria-expanded="false" aria-controls="api-detail-values" aria-label="Show values endpoint details" title="Show values endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
@@ -165,9 +176,9 @@
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">GET</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/paths?section=full</code><small>Discover readable path names</small></span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/paths</code><small>What this token can read</small></span></td>
                             <td><span class="bp-help-access-pill">Read</span></td>
-                            <td>Find reusable paths before calling values.</td>
+                            <td>List the attributes with a value before asking for some of them.</td>
                             <td class="bp-action-cell">
                                 <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-paths" aria-expanded="false" aria-controls="api-detail-paths" aria-label="Show readable paths endpoint details" title="Show readable paths endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
@@ -242,9 +253,9 @@
                         </tr>
                         <tr class="bp-help-doc-item">
                             <td><span class="bp-help-method">GET</span></td>
-                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{batteryId}/passports</code><small>Passport history</small></span></td>
+                            <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries/{id}/passports</code><small>Passport versions</small></span></td>
                             <td><span class="bp-help-access-pill">Read</span></td>
-                            <td>List non-archived passport snapshots for a Battery ID.</td>
+                            <td>List the battery's published versions, newest first.</td>
                             <td class="bp-action-cell">
                                 <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-passport-list" aria-expanded="false" aria-controls="api-detail-passport-list" aria-label="Show passport list endpoint details" title="Show passport list endpoint details" data-tooltip="Details">
                                     <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
@@ -309,12 +320,12 @@
                 </article>
 
                 <article id="api-detail-clusters" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Clusters</span><h3>List the clusters available to the token.</h3><p>Returns cluster IDs and names for the current token scope. Global tokens include every registered cluster.</p></div>
+                    <div><span class="bp-summary-title">Clusters</span><h3>List the clusters available to the token.</h3><p>Returns cluster IDs and names for the token's clusters. A global token is the team's token: it lists every registered cluster.</p></div>
                     <pre class="bp-help-pre"><code>GET @Model.BasePath/clusters</code></pre>
                 </article>
 
                 <article id="api-detail-cluster-batteries" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Cluster batteries</span><h3>List battery IDs in one cluster.</h3><p>Use this before reading, patching, or creating passports when an integration only knows the cluster ID.</p></div>
+                    <div><span class="bp-summary-title">Cluster batteries</span><h3>List the batteries of one cluster.</h3><p>Each battery with its battery ID, serial number, family, model and current passport (passport ID, version and status). A cluster token lists its own clusters' batteries that have a published passport; a global token lists every battery, drafts included.</p></div>
                     <pre class="bp-help-pre"><code>GET @Model.BasePath/clusters/demo-cluster/batteries</code></pre>
                 </article>
 
@@ -339,34 +350,52 @@
                 </article>
 
                 <article id="api-detail-battery" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Full battery</span><h3>Read the latest passport snapshot.</h3><p>Returns app metadata, aspects, registry data, schema versions, and validation details for the requested Battery ID.</p></div>
-                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/@Model.SampleBatteryId</code></pre>
+                    <div><span class="bp-summary-title">Full battery</span><h3>Read the published passport.</h3><p>Find the battery by its battery ID (the current version), a passport ID (that version) or its serial number. The passport comes in its BatteryPass-Ready v2.0.1 form, cut to what the token may see, as on the passport pages: public data for every token; a battery's own cluster also sees its purchaser and shared data; a global token sees everything. Drafts aren't served.</p></div>
+                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/{id}</code></pre>
                 </article>
 
                 <article id="api-detail-section" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Sections</span><h3>Read one passport section only.</h3><p>Supported section names: <code>general</code>, <code>materialComposition</code>, <code>performance</code>, <code>circularity</code>, <code>supplyChain</code>, <code>carbonFootprint</code>, <code>compliance</code>, <code>operations</code>, <code>display</code>, <code>full</code>.</p></div>
-                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/@Model.SampleBatteryId/section/performance
-GET @Model.BasePath/batteries/@Model.SampleBatteryId/section/circularity</code></pre>
+                    <div><span class="bp-summary-title">Sections</span><h3>Read one passport section only.</h3><p>Sections, as the schema names them: @foreach (var name in BatteryPassWeb.Dpp.Api.PassportApi.Sections) { <code>@name</code> } A section the token sees nothing of comes back empty.</p></div>
+                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/{id}/section/performanceAndDurability
+GET @Model.BasePath/batteries/{id}/section/circularityAndResourceEfficiency</code></pre>
                 </article>
 
                 <article id="api-detail-values" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
                     <div>
                         <span class="bp-summary-title">Values</span>
                         <h3>Read one or more specific values.</h3>
-                        <p>Aliases include <code>ratedEnergy</code>, <code>ratedCapacity</code>, <code>ratedMaximumPower</code>, <code>nominalVoltage</code>, <code>nickelMass</code>, <code>currentConsumptionKwh</code>, <code>currentChargeLevelPct</code>, <code>currentVoltageV</code>, <code>currentCurrentA</code>, <code>softwareVersion</code>, <code>softwareReleaseDate</code>, <code>softwareLatestUpdate</code>, <code>locationOfUse</code>, <code>contactPerson</code>, and <code>isActive</code>.</p>
-                        <p>Need the full set of readable values? Open <a id="supported-values-link" href="@($"{Model.BasePath}/batteries/{Model.SampleBatteryId}/paths?section=full")&amp;includeContainers=true" target="_blank" rel="noopener noreferrer"><code>@Model.BasePath/batteries/@Model.SampleBatteryId/paths?section=full&amp;includeContainers=true</code></a> in a new tab.</p>
+                        <p>Name each value by its key (<code>stateOfChargeSoC</code>), its attribute number (<code>%2363</code> for #63, or <code>63</code>), its E code (<code>E1</code>) or its path in the passport (<code>batteryPassportData.performanceAndDurability.stateOfChargeSoC</code>). Each answer gives the value in the passport's own form, its unit, and when and where it came from. A value the token may not see, or the passport doesn't have, comes back with <code>"exists": false</code>.</p>
+                        @if (!string.IsNullOrWhiteSpace(Model.ExampleBatteryId))
+                        {
+                            <p>To see which values a battery has for this token, open <a id="supported-values-link" href="@($"{Model.BasePath}/batteries/{Model.ExampleBatteryId}/paths")" target="_blank" rel="noopener noreferrer"><code>@Model.BasePath/batteries/@Model.ExampleBatteryId/paths</code></a> in a new tab.</p>
+                        }
+                        else
+                        {
+                            <p>To see which values a battery has for this token, use <code>/paths</code>.</p>
+                        }
                     </div>
                     <div class="bp-help-code-grid">
-                        <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=ratedEnergy&amp;path=ratedCapacity
-GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=nickelMass
-GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=aspects.circularity.payload</code></pre>
+                        <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/{id}/values?path=stateOfChargeSoC&amp;path=%2311
+GET @Model.BasePath/batteries/{id}/values?path=batteryMass</code></pre>
                         <pre class="bp-help-pre"><code>{
-  "message": "Values read successfully",
+  "message": "Values read successfully.",
   "data": {
     "values": [
-      { "requestPath": "ratedEnergy", "value": 102 },
-      { "requestPath": "ratedCapacity", "value": 218 },
-      { "requestPath": "nickelMass", "value": 138.7 }
+      {
+        "request": "stateOfChargeSoC",
+        "attribute": "#63",
+        "unit": "%",
+        "exists": true,
+        "value": { "percentUnit": "%", "percentageValue": 76 },
+        "generatedAt": "2026-10-29T08:55:00Z",
+        "source": "battery system"
+      },
+      {
+        "request": "#11",
+        "attribute": "#11",
+        "exists": true,
+        "value": "VPC8C05-6788"
+      }
     ]
   }
 }</code></pre>
@@ -374,19 +403,9 @@ GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=aspects.circula
                 </article>
 
                 <article id="api-detail-paths" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Readable paths</span><h3>Discover paths, then reuse them with values.</h3><p>Query options: <code>section</code> and <code>includeContainers=true</code>. Copy any item from the returned <code>paths</code> array and pass it as a repeated <code>path</code> query parameter to <code>/values</code>.</p></div>
-                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/@Model.SampleBatteryId/paths
-GET @Model.BasePath/batteries/@Model.SampleBatteryId/paths?section=performance
-GET @Model.BasePath/batteries/@Model.SampleBatteryId/paths?section=materialComposition&amp;includeContainers=true
-
-# Example: read values returned from /paths?section=performance
-GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy&amp;path=aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedCapacity
-
-# Example: read an indexed array value discovered by /paths
-GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=aspects.materialComposition.payload.batteryMaterials.0.batteryMaterialMass
-
-# Example: read operations objects discovered by /paths?section=operations
-GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=app.operations.locationOfUse&amp;path=app.operations.contactPerson</code></pre>
+                    <div><span class="bp-summary-title">Readable values</span><h3>List what this token can read, then ask for some of it.</h3><p>Every attribute of the battery's passport that has a value this token may see: its number, key, name, section, path and unit. Add <code>?section=</code> with a section's name to list one section only.</p></div>
+                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/{id}/paths
+GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code></pre>
                 </article>
 
                 <article id="api-detail-telemetry" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
@@ -460,8 +479,8 @@ GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=app.operations.
                 </article>
 
                 <article id="api-detail-passport-list" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
-                    <div><span class="bp-summary-title">Passport list</span><h3>List battery passport snapshots.</h3><p>Returns non-archived passports for the Battery ID with Passport ID, status, created date, and latest marker.</p></div>
-                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/@Model.SampleBatteryId/passports</code></pre>
+                    <div><span class="bp-summary-title">Passport versions</span><h3>List the battery's published versions.</h3><p>Newest first, each with its passport ID, version number, status (active or archived), when it was published and last updated, and whether its signature still holds.</p></div>
+                    <pre class="bp-help-pre"><code>GET @Model.BasePath/batteries/{id}/passports</code></pre>
                 </article>
 
                 <article id="api-detail-validate" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
@@ -501,12 +520,12 @@ GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=app.operations.
                         <option value="listClusters">List accessible clusters</option>
                         <option value="listClusterBatteries">List cluster batteries</option>
                         <option value="createBattery">Create battery</option>
-                        <option value="readSection">Read section</option>
-                        <option value="readSectionPerformance">Read section: Performance</option>
+                        <option value="readSection">Read section: Identifiers and product data</option>
+                        <option value="readSectionPerformance">Read section: Performance and durability</option>
                         <option value="readSectionCircularity">Read section: Circularity</option>
-                        <option value="readValuesAliases">Read values: ratedEnergy, ratedCapacity, nickelMass</option>
-                        <option value="readPathsAll">List readable paths (full)</option>
-                        <option value="readBatteryPassports">List battery passports</option>
+                        <option value="readValuesAliases">Read values: state of charge, serial number, mass</option>
+                        <option value="readPathsAll">List readable values</option>
+                        <option value="readBatteryPassports">List passport versions</option>
                         <option value="readHistory24h">Read telemetry history 24h</option>
                         <option value="writeTelemetryPoint">Write telemetry point</option>
                         <option value="writeTelemetrySeries">Write telemetry series</option>
@@ -534,7 +553,7 @@ GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=app.operations.
                 </label>
                 <label class="bp-help-sticky-id-field">Battery ID
                     <span class="bp-help-sticky-id-control">
-                        <input id="tester-battery-id" class="bp-id-value" value="@Model.SampleBatteryId" />
+                        <input id="tester-battery-id" class="bp-id-value" value="@(string.IsNullOrWhiteSpace(Model.ExampleBatteryId) ? Model.SampleBatteryId : Model.ExampleBatteryId)" />
                         <button type="button" class="bp-copy-id-button" aria-label="Copy Battery ID" title="Copy Battery ID" data-tooltip="Copy Battery ID" data-copy-id-button data-copy-target="#tester-battery-id">
                             <svg class="bp-copy-id-icon" viewBox="0 0 24 24" aria-hidden="true">
                                 <rect x="9" y="9" width="10" height="10" rx="2" />
@@ -733,31 +752,31 @@ GET @Model.BasePath/batteries/@Model.SampleBatteryId/values?path=app.operations.
                 },
                 readSection: {
                     method: 'GET',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/section/general`,
+                    pathTemplate: `${basePath}/batteries/{batteryId}/section/identifiersAndProductData`,
                     body: '',
                     tokenPreset: 'sample-read'
                 },
                 readSectionPerformance: {
                     method: 'GET',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/section/performance`,
+                    pathTemplate: `${basePath}/batteries/{batteryId}/section/performanceAndDurability`,
                     body: '',
                     tokenPreset: 'sample-read'
                 },
                 readSectionCircularity: {
                     method: 'GET',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/section/circularity`,
+                    pathTemplate: `${basePath}/batteries/{batteryId}/section/circularityAndResourceEfficiency`,
                     body: '',
                     tokenPreset: 'sample-read'
                 },
                 readValuesAliases: {
                     method: 'GET',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/values?path=ratedEnergy&path=ratedCapacity&path=nickelMass`,
+                    pathTemplate: `${basePath}/batteries/{batteryId}/values?path=stateOfChargeSoC&path=%2311&path=batteryMass`,
                     body: '',
                     tokenPreset: 'sample-read'
                 },
                 readPathsAll: {
                     method: 'GET',
-                    pathTemplate: `${basePath}/batteries/{batteryId}/paths?section=full`,
+                    pathTemplate: `${basePath}/batteries/{batteryId}/paths`,
                     body: '',
                     tokenPreset: 'sample-read'
                 },
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:    16`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2099`, none failed.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs web/Controllers/HelpController.cs web/Models/ViewModels/ExternalApiHelpViewModel.cs web/Views/Help/Index.cshtml
git commit -m "feat(dpp): the API help page documents and tries the new reading calls" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 4: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2099, Skipped:     0, Total:  2099`: the 2061 from before, 24 new in Task 1, 13 more in Task 2 (four older tests go) and 1 in Task 3.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

- [ ] **Step 4: Try the API**

Start the app from your PC against `battery_pass_v3`, as for the earlier steps, with `APP_BASE_URL` set too. Never point it at `battery_pass_demo`. You need two tokens: on **API Token Management**, create a "Read" token for the cluster of a published battery, and a "Read" token for another cluster; each token's value is shown once, so keep it until the checks are done. Then open **Help** (`/help`):

1. The toolbar shows "Example battery ID for reading" with a published battery. Under "API documentation", the reading rows use `{id}`, and the Sections panel lists the schema's sections.
2. **Request workbench**, template "Read full battery", with the first token in "Token": status 200, and the passport includes `stateOfChargeSoC` under `performanceAndDurability`.
3. The same with the second token: status 200, public data only: no `stateOfChargeSoC`, no `economicOperatorId`.
4. Template "Read values: state of charge, serial number, mass" with the first token: `#63` with its unit `%`, its value, and when and where it came from; `#11` the serial number.
5. Change the battery ID to a draft battery's serial number: 404, "No published battery passport has this ID."
6. Signed in as a team member, open `/api/passports` in the browser: the list of batteries. Signed out, it is refused; `/api/passports/<battery ID>/verify` still answers, with `"signatureVerified": true`.

Delete the two tokens afterwards (API Token Management).

## After this plan

- **3c-2** rebuilds the writing side on the new model: creating batteries and publication for team tokens, the live values (#63, #83, #87, E1–E4) in place of telemetry and their history from the change log (with each value's measurement time added to its entry), and the removal of operations, the battery-model label, the software version and the old passport snapshot, validate and sign calls. The old `ExternalApiController` and the services only it uses go then, and the help page's writing rows and templates follow.
- **3d** removes the old model and pages and resets `battery_pass_v3`, with the four sample tokens.
- **Known small gaps until 3c-2:**
  - The help page's writing templates still use the old sample battery; the workbench's battery field starts with the example battery for reading, so paste the old sample battery's ID to try a writing template.
  - The old telemetry history reads the old data.

