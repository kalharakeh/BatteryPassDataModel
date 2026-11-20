# DPP V3 Step 2c: Documents — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restricted documents uploaded to the app and served only to their audience, #92's accident reports, and the audience rule of design 5.2 that decides who may see what — backend only.

**Architecture:** Builds on 2b. `AudienceRules.MaySee` turns design 5.2 into one rule over a `Reader` (roles and clusters); step 3 reuses it for pages and the API. `DppLinks` builds the app's own document addresses from `APP_BASE_URL`, and the save pipeline refuses any other address for the six restricted document attributes. `DocumentService` checks files by their bytes, keeps them in GridFS (`DocumentStore`), links the attribute through the pipeline, adds accident reports, and serves files only to their audience.

**Tech Stack:** .NET 10, MongoDB.Driver 3.9 (GridFS), JsonSchema.Net 9.5.0, xUnit 2.9, EphemeralMongo 3.2.0.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 5, 9, 14 and judgement call J-14) and the decisions record (decision 7). Earlier plans: step 1, 2a and 2b in `docs/superpowers/plans/`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** 2b is on `feature/dpp-v3-step2b-publication` (local only). Start 2c on top of it: `git switch -c feature/dpp-v3-step2c-documents`.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:   783`. After this plan: 1635 (803 of the new tests are the role matrix). The suite takes about 15 seconds.
- **`APP_BASE_URL` becomes required** for creating packs and linking documents (an environment variable, or `BatteryPass:AppBaseUrl` in settings, e.g. `https://sib-dpd.eu`). Without it the app still starts, logs a warning, and refuses those two actions with a clear message.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`; the old `FilesApiController` and its tests stay until step 3.

## Judgement calls in this plan

1. **The audience rule arrives now** because the download check is that rule; step 3 reuses it everywhere.
   - **Who sees everything:** the team and market surveillance.
   - **Drafts:** a draft pack gives its cluster nothing (J-6).
   - **Model and batch values:** visible to every cluster that owns a published pack of that model or batch.
   - **Several roles:** a reader sees whatever any of their roles allows.
2. **The six restricted document attributes** (#29, #45–48, #92) may only link to the app's own `/dpp/` addresses. The save pipeline rejects anything else, so a restricted document can never end up published at an outside address. Public documents stay plain links through the pipeline and need no upload.
3. **Files are checked by their bytes:**
   - only PDF, PNG and JPEG;
   - the detected type is the one stored and published;
   - empty files and files over `MaxUploadBytes` are refused (0 or less means no limit, as today);
   - folders are stripped from file names;
   - the SHA-256 is kept.
4. **The link is checked before the file is stored** (record exists, sender may change it, value valid), so a refusal leaves nothing behind.
5. **Accident reports (#92):**
   - pack creation points #92 at the pack's accident page;
   - adding a report saves the same link again, which counts as a live update and moves #4 on a published passport;
   - the team and the battery's cluster admins add reports;
   - deleting reports isn't part of 2c (neither the design nor the decisions ask for it).
6. **Logging:** refused downloads are logged (`dpp.document-denied`), and uploads are logged with their hash.
7. **No web addresses yet:** the download route and the accident page arrive with step 3, which maps sign-ins to readers.
8. **The test pack's document links** switch to the app's address shape (`https://passport.example.com/dpp/...`).

## File structure

| File | Responsibility |
|---|---|
| `web/Dpp/Access/AudienceRules.cs` | `ReaderRole`, `Reader` and `AudienceRules.MaySee` |
| `web/Dpp/Documents/DppLinks.cs` | The app's document addresses, the restricted attributes, #92's accident-page link |
| `web/Dpp/Editing/SavePipeline.cs` (modify) | Rejects outside links for restricted documents |
| `web/Dpp/Editing/PackCreationService.cs` (modify) | Sets #92 at creation; refuses without `APP_BASE_URL` |
| `web/Dpp/Store/DppStore.cs` (modify) | Clusters of the published packs using a model, batch or pack |
| `web/Dpp/Documents/DocumentStore.cs` | `DppDocument` records and GridFS files |
| `web/Dpp/Documents/DocumentService.cs` | Uploads, accident reports, downloads for the audience |
| `web/Dpp/DppServiceCollectionExtensions.cs` (modify) | Registers the links, the document store and the service |
| `web/Program.cs` (modify) | Document indexes at startup; warning when `APP_BASE_URL` is missing |
| `BatteryPassWeb.Tests/Dpp/*` | Tests, `DocumentTesting` helpers, and the test pack's links (modify) |


---

### Task 1: Who may see what

Design 5.2 as one rule, tested over every reader kind and every attribute.

**Files:**
- Create: `web/Dpp/Access/AudienceRules.cs`
- Test: `BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Design 5.2's role matrix, over every attribute.</summary>
public sealed class AudienceRulesTests
{
    private const string OwnCluster = "cluster-test";

    private static readonly string[] ReaderKinds =
    [
        "anonymous", "own-cluster-user", "own-cluster-admin", "other-cluster", "team", "notified-body",
        "market-surveillance", "commission", "li-repair", "li-recycling", "li-treatment",
    ];

    public static TheoryData<string, int> EveryReaderAndAttribute()
    {
        var data = new TheoryData<string, int>();
        foreach (var kind in ReaderKinds)
        {
            foreach (var attribute in AttributeCatalog.All)
            {
                data.Add(kind, attribute.Number);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryReaderAndAttribute))]
    public void MaySee_ShouldFollowTheRoleMatrix(string kind, int number)
    {
        var attribute = AttributeCatalog.ByNumber(number);

        Assert.Equal(Expected(kind, attribute), AudienceRules.MaySee(ReaderFor(kind), attribute, [OwnCluster]));
    }

    [Fact]
    public void WithoutAPublishedPack_NoClusterShouldSeePurchaserOrSharedData()
    {
        var admin = ReaderFor("own-cluster-admin");

        Assert.False(AudienceRules.MaySee(admin, AttributeCatalog.ByNumber(63), []));
        Assert.False(AudienceRules.MaySee(admin, AttributeCatalog.ByNumber(45), []));
        Assert.True(AudienceRules.MaySee(admin, AttributeCatalog.ByNumber(11), []));
        Assert.True(AudienceRules.MaySee(ReaderFor("team"), AttributeCatalog.ByNumber(63), []));
        Assert.True(AudienceRules.MaySee(ReaderFor("market-surveillance"), AttributeCatalog.ByNumber(63), []));
        Assert.True(AudienceRules.MaySee(ReaderFor("commission"), AttributeCatalog.ByNumber(45), []));
    }

    [Fact]
    public void ReaderWithSeveralRoles_ShouldSeeWhatAnyOfThemSees()
    {
        var reader = new Reader(new HashSet<ReaderRole> { ReaderRole.NotifiedBody, ReaderRole.LiRepair }, new HashSet<string>(), "both@example.com");

        Assert.True(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(29), [OwnCluster]));
        Assert.True(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(46), [OwnCluster]));
        Assert.False(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(63), [OwnCluster]));
    }

    // Design 5.2 written out per reader; the shared rows by attribute number, from design 5.1.
    private static bool Expected(string kind, AttributeDefinition attribute) => kind switch
    {
        "team" or "market-surveillance" => true,
        "anonymous" or "other-cluster" => attribute.Audience == Audience.Public,
        "own-cluster-user" or "own-cluster-admin" => attribute.Audience is Audience.Public or Audience.Purchaser or Audience.Shared,
        "notified-body" => attribute.Audience is Audience.Public or Audience.Authorities,
        "commission" => attribute.Audience is Audience.Public or Audience.Shared or Audience.Authorities,
        "li-repair" => attribute.Audience == Audience.Public || attribute.Number is 43 or 45 or 46 or 47 or 48,
        "li-recycling" => attribute.Audience == Audience.Public || attribute.Number is 43 or 45 or 48,
        "li-treatment" => attribute.Audience == Audience.Public || attribute.Number is 48,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static Reader ReaderFor(string kind) => kind switch
    {
        "anonymous" => Reader.Anonymous,
        "own-cluster-user" => Reader.With("user@example.com", ReaderRole.ClusterUser, OwnCluster),
        "own-cluster-admin" => Reader.With("admin@example.com", ReaderRole.ClusterAdmin, OwnCluster),
        "other-cluster" => Reader.With("other@example.com", ReaderRole.ClusterAdmin, "another-cluster"),
        "team" => Reader.With("team@example.com", ReaderRole.Team),
        "notified-body" => Reader.With("nb@example.com", ReaderRole.NotifiedBody),
        "market-surveillance" => Reader.With("msa@example.com", ReaderRole.MarketSurveillance),
        "commission" => Reader.With("ec@example.com", ReaderRole.Commission),
        "li-repair" => Reader.With("repair@example.com", ReaderRole.LiRepair),
        "li-recycling" => Reader.With("recycling@example.com", ReaderRole.LiRecycling),
        "li-treatment" => Reader.With("treatment@example.com", ReaderRole.LiTreatment),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AudienceRulesTests"`
Expected: the build fails because `BatteryPassWeb.Dpp.Access` isn't defined yet (CS0234 or CS0246 errors).

- [ ] **Step 3: Write `AudienceRules.cs`**

`web/Dpp/Access/AudienceRules.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>The roles of design 5.2. A reader with no roles is anonymous.</summary>
public enum ReaderRole
{
    ClusterUser,
    ClusterAdmin,
    Team,
    NotifiedBody,
    MarketSurveillance,
    Commission,
    LiRepair,
    LiRecycling,
    LiTreatment,
}

/// <summary>Someone reading passport data: their roles and the clusters they belong to. Step 3 maps sign-ins and tokens to readers.</summary>
public sealed record Reader(IReadOnlySet<ReaderRole> Roles, IReadOnlySet<string> ClusterIds, string Who)
{
    public static Reader Anonymous { get; } = new(new HashSet<ReaderRole>(), new HashSet<string>(StringComparer.Ordinal), "anonymous");

    public static Reader With(string who, ReaderRole role, params string[] clusterIds) =>
        new(new HashSet<ReaderRole> { role }, clusterIds.ToHashSet(StringComparer.Ordinal), who);
}

/// <summary>Who may see an attribute (design 5.1 and 5.2): the one rule behind pages, the API, exports and documents.</summary>
public static class AudienceRules
{
    /// <param name="batteryClusterIds">
    /// The clusters of the published packs the value belongs to: one for a pack value; every cluster owning a published
    /// pack of the model or batch for a model or batch value; empty when there is none (drafts count for nobody, J-6).
    /// </param>
    public static bool MaySee(Reader reader, AttributeDefinition attribute, IReadOnlyCollection<string> batteryClusterIds)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(batteryClusterIds);
        if (attribute.Audience == Audience.Public || reader.Roles.Contains(ReaderRole.Team) || reader.Roles.Contains(ReaderRole.MarketSurveillance))
        {
            return true;
        }

        var ownsTheBattery = (reader.Roles.Contains(ReaderRole.ClusterUser) || reader.Roles.Contains(ReaderRole.ClusterAdmin))
            && batteryClusterIds.Any(reader.ClusterIds.Contains);
        return attribute.Audience switch
        {
            Audience.Purchaser => ownsTheBattery,
            Audience.Authorities => reader.Roles.Contains(ReaderRole.NotifiedBody) || reader.Roles.Contains(ReaderRole.Commission),
            Audience.Shared => ownsTheBattery
                || reader.Roles.Contains(ReaderRole.Commission)
                || (reader.Roles.Contains(ReaderRole.LiRepair) && attribute.SharedWith.HasFlag(LegitimateInterestGroups.Repair))
                || (reader.Roles.Contains(ReaderRole.LiRecycling) && attribute.SharedWith.HasFlag(LegitimateInterestGroups.Recycling))
                || (reader.Roles.Contains(ReaderRole.LiTreatment) && attribute.SharedWith.HasFlag(LegitimateInterestGroups.Treatment)),
            _ => false,
        };
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AudienceRulesTests"`
Expected: `Passed!  - Failed:     0, Passed:   805`. The whole suite now has 1588.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Access/AudienceRules.cs BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs
git commit -m "feat(dpp): decide who may see each attribute"
```


---

### Task 2: The app's document addresses


**Files:**
- Create: `web/Dpp/Documents/DppLinks.cs`
- Test: `BatteryPassWeb.Tests/Dpp/DocumentTesting.cs`
- Test: `BatteryPassWeb.Tests/Dpp/DppLinksTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/DocumentTesting.cs`:

```csharp
using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Documents;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Document links for the test address, and small files whose first bytes say what they are.</summary>
internal static class DocumentTesting
{
    public const string BaseUrl = "https://passport.example.com";

    public static readonly DppLinks Links = new(Options.Create(new BatteryPassOptions { AppBaseUrl = BaseUrl }));

    public static readonly DppLinks NoLinks = new(Options.Create(new BatteryPassOptions()));

    public static byte[] Pdf(string text = "test") => Encoding.ASCII.GetBytes($"%PDF-1.7\n{text}\n%%EOF");

    public static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    public static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
}
```

`BatteryPassWeb.Tests/Dpp/DppLinksTests.cs`:

```csharp
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Values;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class DppLinksTests
{
    [Theory]
    [InlineData("https://passport.example.com")]
    [InlineData("https://passport.example.com/")]
    public void Links_ShouldBeBuiltOnTheAppBaseUrl(string baseUrl)
    {
        var links = new DppLinks(Options.Create(new BatteryPassOptions { AppBaseUrl = baseUrl }));

        Assert.Equal("https://passport.example.com/dpp/documents/doc-1", links.DocumentUrl("doc-1"));
        Assert.Equal("https://passport.example.com/dpp/packs/pack-1/accidents", links.AccidentPageUrl("pack-1"));
    }

    [Theory]
    [InlineData("https://passport.example.com/dpp/documents/doc-1", true)]
    [InlineData("https://passport.example.com/dpp/packs/pack-1/accidents", true)]
    [InlineData("https://passport.example.com/other/doc-1", false)]
    [InlineData("https://elsewhere.example.com/dpp/documents/doc-1", false)]
    [InlineData("https://passport.example.com.evil.example/dpp/documents/doc-1", false)]
    public void IsAppUrl_ShouldAcceptOnlyTheAppsOwnDocumentAddresses(string url, bool expected)
    {
        Assert.Equal(expected, DocumentTesting.Links.IsAppUrl(url));
    }

    [Fact]
    public void WithoutAnAppBaseUrl_NoLinkShouldBeIssuedOrAccepted()
    {
        Assert.False(DocumentTesting.NoLinks.IsConfigured);
        Assert.False(DocumentTesting.NoLinks.IsAppUrl("https://passport.example.com/dpp/documents/doc-1"));
        Assert.Equal(DppLinks.NotConfigured, Assert.Throws<InvalidOperationException>(() => DocumentTesting.NoLinks.DocumentUrl("doc-1")).Message);
    }

    [Fact]
    public void AccidentPageLink_ShouldBeAValidValueForAccidents()
    {
        var validator = new AttributeValueValidator(DppStoreTesting.Schemas);

        Assert.Empty(validator.Validate(AttributeCatalog.ByNumber(92), DocumentTesting.Links.AccidentPageLink("pack-1")));
    }

    [Fact]
    public void RestrictedDocuments_ShouldBeTheDocumentAttributesThatArentPublic()
    {
        var documents = AttributeCatalog.All.Where(a =>
            DppStoreTesting.Schemas.FullDocument["$defs"]![a.Section]!["properties"]![a.Property]!["$ref"]?.GetValue<string>() == "#/$defs/RelatedResource");

        Assert.Equal(
            documents.Where(a => a.Audience != Audience.Public).Select(a => a.Property).Order(StringComparer.Ordinal),
            DppLinks.RestrictedDocumentProperties.Order(StringComparer.Ordinal));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppLinksTests"`
Expected: the build fails because `BatteryPassWeb.Dpp.Documents` isn't defined yet (CS0234 or CS0246 errors).

- [ ] **Step 3: Write `DppLinks.cs`**

`web/Dpp/Documents/DppLinks.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Dpp.Documents;

/// <summary>
/// The app's own addresses for restricted documents and #92's accident page (design 9), built from APP_BASE_URL.
/// Restricted documents may only link here, so they are always served behind the app's access checks.
/// </summary>
public sealed class DppLinks(IOptions<BatteryPassOptions> options)
{
    public const string NotConfigured = "APP_BASE_URL is not set: the app can't issue document links or create packs without it.";

    /// <summary>The document attributes that are uploaded to the app and served only to their audience (design 9).</summary>
    public static IReadOnlySet<string> RestrictedDocumentProperties { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "resultsOfTestReportsProvingCompliance",
        "dismantlingInformation",
        "partNumbersForComponents",
        "informationOnSourcesOfSpareParts",
        "safetyMeasures",
        "informationOnAccidents",
    };

    public bool IsConfigured => BaseOrNull is not null;

    public string DocumentUrl(string documentId) => $"{Base}/dpp/documents/{Uri.EscapeDataString(documentId)}";

    public string AccidentPageUrl(string packId) => $"{Base}/dpp/packs/{Uri.EscapeDataString(packId)}/accidents";

    /// <summary>#92's value: the pack's accident page, which lists its reports or says none were reported (J-14).</summary>
    public JsonObject AccidentPageLink(string packId) => new()
    {
        ["contentType"] = "text/html",
        ["url"] = AccidentPageUrl(packId),
        ["language"] = "en-GB",
        ["resourceTitle"] = "Accident information",
    };

    /// <summary>True for an address the app serves itself, behind its access checks.</summary>
    public bool IsAppUrl(string url) =>
        BaseOrNull is { } baseUrl && url.StartsWith(baseUrl + "/dpp/", StringComparison.Ordinal);

    private string? BaseOrNull =>
        string.IsNullOrWhiteSpace(options.Value.AppBaseUrl) ? null : options.Value.AppBaseUrl.Trim().TrimEnd('/');

    private string Base => BaseOrNull ?? throw new InvalidOperationException(NotConfigured);
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppLinksTests"`
Expected: `Passed!  - Failed:     0, Passed:    10`. The whole suite now has 1598.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Documents/DppLinks.cs BatteryPassWeb.Tests/Dpp/DocumentTesting.cs BatteryPassWeb.Tests/Dpp/DppLinksTests.cs
git commit -m "feat(dpp): build the app's own document addresses"
```


---

### Task 3: Restricted documents link only to the app

The save pipeline now needs `DppLinks`, so this task also registers it (the rest of the registrations come in Task 8). The test pack's restricted links move to the app's address shape.

**Files:**
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/SavePipelineTesting.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/seed-other-industrial.json`
- Test: `BatteryPassWeb.Tests/Dpp/RestrictedDocumentLinkTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/SavePipelineTesting.cs` (whole file after the change):

```csharp
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The save pipeline around a test store; the fixed test clock unless a clock is given.</summary>
internal static class SavePipelineTesting
{
    public static SavePipeline Pipeline(DppStore store, MongoContext context, params IPackChangeListener[] listeners) =>
        Pipeline(store, context, new FixedTimeProvider(DppStoreTesting.Now), listeners);

    public static SavePipeline Pipeline(DppStore store, MongoContext context, TimeProvider clock, params IPackChangeListener[] listeners) => new(
        store,
        new AttributeValueNormalizer(DppStoreTesting.Schemas),
        new AttributeValueValidator(DppStoreTesting.Schemas),
        new ApplicationAuditService(context),
        clock,
        DocumentTesting.Links,
        listeners);
}
```

`BatteryPassWeb.Tests/Dpp/seed-other-industrial.json` (whole file after the change):

```json
{
  "header": {
    "digitalProductPassportId": "urn:test:dpp:VPC8C05-6788:v1",
    "dppStatus": "active",
    "lastUpdatedUtc": "2026-10-28T09:30:00Z"
  },
  "family": { "id": "family-test", "name": "Test battery family" },
  "model": {
    "id": "model-test",
    "familyId": "family-test",
    "name": "EXB-NMC-100",
    "values": {
      "batteryCategory": { "batteryCategoryValue": "industrial/non-stationary battery" },
      "batteryMass": { "gramKilogramUnit": "kg", "gramKilogramValue": 512.5 },
      "separateCollectionSymbol": { "contentType": "image/svg+xml", "url": "https://www.example.com/battery/symbols/separate-collection.svg", "language": "en-GB", "resourceTitle": "Separate collection symbol" },
      "symbolsForCadmiumAndLead": { "contentType": "image/svg+xml", "url": "https://www.example.com/battery/symbols/cd-pb.svg", "language": "en-GB", "resourceTitle": "Symbols for cadmium and lead" },
      "extinguishingAgent": { "agentFireClass": "B", "extinguishingAgent": "Water" },
      "meaningOfLabelsAndSymbols": "The crossed-out wheeled bin means the battery must be collected separately from other waste.",
      "informationOfDueDiligenceReport": { "contentType": "application/pdf", "url": "https://www.example.com/battery/due-diligence-report.pdf", "language": "en-GB", "resourceTitle": "Due diligence report" },
      "batteryChemistry": { "chemicalCodeValue": "Li-ion NMC" },
      "criticalRawMaterials": [ "Cobalt", "Lithium", "Natural graphite", "Nickel" ],
      "materialsUsedInCathodeAnodeAndElectrolyte": [ "Cathode: lithium nickel manganese cobalt oxide (NMC 811)", "Anode: graphite", "Electrolyte: LiPF6 in carbonate solvents" ],
      "hazardousSubstancesAndTheirImpact": [
        { "chemicalName": "Lithium hexafluorophosphate", "identifierType": "CAS No", "identifier": "21324-40-3", "concentration": 1.2, "hazardPhrases": [ "H301", "H314", "H372" ], "precautionaryPhrases": [ "P260", "P280", "P301+P310" ], "substanceLocation": "Electrolyte" }
      ],
      "dismantlingInformation": { "contentType": "application/pdf", "url": "https://passport.example.com/dpp/documents/doc-dismantling", "language": "en-GB", "resourceTitle": "Dismantling manual" },
      "partNumbersForComponents": { "contentType": "application/pdf", "url": "https://passport.example.com/dpp/documents/doc-part-numbers", "language": "en-GB", "resourceTitle": "Part numbers for components" },
      "informationOnSourcesOfSpareParts": { "contentType": "application/pdf", "url": "https://passport.example.com/dpp/documents/doc-spare-parts", "language": "en-GB", "resourceTitle": "Sources of spare parts" },
      "safetyMeasures": { "contentType": "application/pdf", "url": "https://passport.example.com/dpp/documents/doc-safety-measures", "language": "en-GB", "resourceTitle": "Safety measures" },
      "informationOnTheRoleOfEndUsersInContributingToWastePrevention": { "contentType": "text/html", "url": "https://www.example.com/battery/waste-prevention", "language": "en-GB", "resourceTitle": "Your role in waste prevention" },
      "informationOnTheRoleOfEndUsersInContributingToTheSeparateCollectionOfWasteBatteries": { "contentType": "text/html", "url": "https://www.example.com/battery/separate-collection", "language": "en-GB", "resourceTitle": "Your role in separate collection" },
      "informationOnBatteryCollectionPreparationForSecondLifeAndOnTreatmentAtEndOfLife": { "contentType": "application/pdf", "url": "https://www.example.com/battery/collection-and-treatment.pdf", "language": "en-GB", "resourceTitle": "Collection, second life and treatment" },
      "ratedCapacity": { "amperehourMiliamperehourValue": 200, "ampereHourMiliamperehourUnit": "Ah" },
      "capacityFade": { "percentUnit": "%", "percentageValue": 0 },
      "minimumVoltage": { "voltValue": 520.0, "voltUnit": "V" },
      "maximumVoltage": { "voltValue": 740.0, "voltUnit": "V" },
      "nominalVoltage": { "voltValue": 650.0, "voltUnit": "V", "temperatureRangeLowerBoundaryValue": -20, "temperatureRangeLowerBoundaryUnit": "°C", "temperatureRangeUpperBoundaryValue": 45, "temperatureRangeUpperBoundaryUnit": "°C" },
      "originalPowerCapability": { "wattValueAt80PercentStateOfCharge": 250000, "wattValueAt20PercentStateOfCharge": 180000, "wattUnit": "W" },
      "powerFade": { "percentUnit": "%", "percentageValue": 0 },
      "maximumPermittedBatteryPower": { "wattValue": 300000, "wattUnit": "W" },
      "initialRoundTripEnergyEfficiency": { "percentUnit": "%", "percentageValue": 95 },
      "roundTripEnergyEfficiencyAt50PerCentOfCycleLife": { "percentUnit": "%", "percentageValue": 92 },
      "energyRoundTripEfficiencyFade": { "percentUnit": "%", "percentageValue": 0 },
      "initialInternalResistanceOfBatteryCellAndPackModuleRecommended": { "ohmValueForBatteryPack": 1, "ohmValueForBatteryCell": 1, "ohmUnit": "Ohm" },
      "internalResistanceIncreaseOfPackCellAndModuleRecommended": { "internalResistanceIncreaseValueForBatteryPack": 0, "percentUnit": "%" },
      "expectedLifetimeInCalendarYears": 12,
      "expectedLifetimeNumberOfChargeDischargeCycles": 4000,
      "cycleLifeReferenceTest": "IEC 62660-1",
      "cRateOfRelevantCycleLifeTest": { "amperePerAmpereHourValue": 0.5, "amperePerAmpereHourUnit": "A/Ah" },
      "temperatureRangeIdleStateLowerBoundary": { "degreeCelsiusUnit": "°C", "celsiusValue": -30 },
      "temperatureRangeIdleStateUpperBoundary": { "degreeCelsiusUnit": "°C", "celsiusValue": 55 }
    }
  },
  "site": {
    "id": "site-test",
    "name": "Example plant",
    "values": {
      "uniqueManufacturerIdentifier": "urn:test:manufacturer:example-plant",
      "manufacturerInformation": { "name": "Example Battery Systems AB", "registeredTradeNameOrRegisteredTrademark": "Example Batteries", "postalAddress": "Exempelvägen 1, 111 11 Stockholm, Sweden", "webAddress": "https://www.example.com", "eMailAddress": "battery.passport@example.com" },
      "manufacturingPlace": "Example plant, Stockholm, Sweden"
    }
  },
  "batch": {
    "id": "batch-test-2026",
    "modelId": "model-test",
    "siteId": "site-test",
    "year": 2026,
    "values": {
      "batteryModelIdentifier": "EXB-NMC-100",
      "carbonFootprintLabel": { "contentType": "image/png", "url": "https://www.example.com/battery/carbon-footprint-label.png", "language": "en-GB", "resourceTitle": "Carbon footprint label" },
      "euDeclarationOfConformity": { "contentType": "application/pdf", "url": "https://www.example.com/battery/eu-declaration-of-conformity.pdf", "language": "en-GB", "resourceTitle": "EU declaration of conformity" },
      "resultsOfTestReportsProvingCompliance": { "contentType": "application/pdf", "url": "https://passport.example.com/dpp/documents/doc-test-reports", "language": "en-GB", "resourceTitle": "Test reports" },
      "batteryCarbonFootprintPerFunctionalUnit": { "kgCO2EquivalentPerKilowattHourValue": 75.4, "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh" },
      "contributionOfRawMaterialAcquisitionAndPreProcessingLifecycleStage": { "kgCO2EquivalentPerKilowattHourValue": 48.1, "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh" },
      "contributionOfMainProductProductionLifecycleStage": { "kgCO2EquivalentPerKilowattHourValue": 20.3, "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh" },
      "contributionOfDistributionLifecycleStage": { "kgCO2EquivalentPerKilowattHourValue": 2.2, "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh" },
      "contributionOfEndOfLifeAndRecyclingLifecycleStage": { "kgCO2EquivalentPerKilowattHourValue": 4.8, "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh" },
      "carbonFootprintPerformanceClass": "Class B",
      "webLinkToPublicCarbonFootprintStudy": "https://www.example.com/battery/carbon-footprint-study",
      "nickelRecycledContentShare": { "percentUnit": "%", "percentageValue": 6 },
      "cobaltRecycledContentShare": { "percentUnit": "%", "percentageValue": 16 },
      "lithiumRecycledContentShare": { "percentUnit": "%", "percentageValue": 6 },
      "leadRecycledContentShare": { "percentUnit": "%", "percentageValue": 0 },
      "renewableContentShare": { "percentUnit": "%", "percentageValue": 3 }
    }
  },
  "operator": {
    "id": "operator-test",
    "name": "Example Battery Systems AB",
    "values": {
      "economicOperatorId": "urn:test:operator:example-battery-systems",
      "economicOperatorInformation": { "name": "Example Battery Systems AB", "registeredTradeNameOrRegisteredTrademark": "Example Batteries", "postalAddress": "Exempelvägen 1, 111 11 Stockholm, Sweden", "webAddress": "https://www.example.com", "eMailAddress": "battery.passport@example.com" }
    }
  },
  "pack": {
    "id": "pack-VPC8C05-6788",
    "batchId": "batch-test-2026",
    "operatorId": "operator-test",
    "clusterId": "cluster-test",
    "values": {
      "uniqueProductIdentifier": "urn:test:battery:VPC8C05-6788",
      "batterySerialNumber": "VPC8C05-6788",
      "thirdPartyServiceProvider": "Example Passport Hosting AB",
      "manufacturingDate": "2026-09",
      "warrantyPeriod": "P8Y",
      "batteryStatus": { "batteryStatusValues": "original" },
      "capacityFade": { "percentUnit": "%", "percentageValue": 4 },
      "stateOfChargeSoC": { "percentUnit": "%", "percentageValue": 76 },
      "numberOfFullChargingAndDischargingCycles": 412,
      "temperatureInformation": { "degreeCelsiusUnit": "°C", "celsiusValue": 23 },
      "informationOnAccidents": { "contentType": "text/html", "url": "https://passport.example.com/dpp/packs/pack-VPC8C05-6788/accidents", "language": "en-GB", "resourceTitle": "Accident information" }
    }
  }
}
```

`BatteryPassWeb.Tests/Dpp/RestrictedDocumentLinkTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Restricted documents may only link to the app's own addresses, so they stay behind its access checks.</summary>
[Collection(MongoCollection.Name)]
public sealed class RestrictedDocumentLinkTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = ChangeSender.Team("team@example.com");

    [Theory]
    [InlineData("https://www.example.com/dismantling.pdf", KeyOutcome.Rejected)]
    [InlineData("https://passport.example.com/dpp/documents/doc-new", KeyOutcome.Saved)]
    public async Task ModelDocument_ShouldOnlyLinkToTheApp(string url, KeyOutcome expected)
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, Change(TargetKind.Model, seed.Sources.Model.Id, "dismantlingInformation", url));

        var key = Assert.Single(result.Keys);
        Assert.Equal(expected, key.Outcome);
        if (expected == KeyOutcome.Rejected)
        {
            Assert.Contains("must link to a file uploaded to the app", key.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("https://www.example.com/accidents", KeyOutcome.Rejected)]
    [InlineData("https://passport.example.com/dpp/packs/pack-VPC8C05-6788/accidents", KeyOutcome.Saved)]
    public async Task ClusterAdmin_ShouldNotPointAccidentsElsewhere(string url, KeyOutcome expected)
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(
            ChangeSender.ClusterAdmin("admin@example.com", "cluster-test"),
            Change(TargetKind.Pack, "pack-VPC8C05-6788", "informationOnAccidents", url, "text/html"));

        Assert.Equal(expected, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task PublicDocument_ShouldStillLinkAnywhere()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, Change(TargetKind.Batch, seed.Sources.Batch.Id, "euDeclarationOfConformity", "https://www.example.com/doc-2026.pdf"));

        Assert.True(result.Saved);
    }

    [Fact]
    public async Task WithoutAnAppBaseUrl_RestrictedDocumentsShouldBeRejected()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var pipeline = new SavePipeline(
            store,
            new AttributeValueNormalizer(DppStoreTesting.Schemas),
            new AttributeValueValidator(DppStoreTesting.Schemas),
            new ApplicationAuditService(context),
            new FixedTimeProvider(DppStoreTesting.Now),
            DocumentTesting.NoLinks,
            []);

        var result = await pipeline.SaveAsync(Team, Change(TargetKind.Model, seed.Sources.Model.Id, "safetyMeasures", "https://passport.example.com/dpp/documents/doc-new"));

        Assert.Equal(KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
    }

    private static ChangeSet Change(TargetKind target, string id, string property, string url, string contentType = "application/pdf") =>
        new(target, id, new Dictionary<string, JsonNode?> { [property] = new JsonObject { ["contentType"] = contentType, ["url"] = url } }, "form");
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.RestrictedDocumentLinkTests"`
Expected: the build fails because `SavePipeline` doesn't take `DppLinks` yet (CS1503 or CS1739 errors).

- [ ] **Step 3: Write `SavePipeline.cs`**

`web/Dpp/Editing/SavePipeline.cs` (whole file after the change):

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>The record kinds that hold attribute values.</summary>
public enum TargetKind
{
    Model,
    Site,
    Batch,
    Operator,
    Pack,
}

public enum KeyOutcome
{
    /// <summary>Accepted (saved, or would be saved in a preview).</summary>
    Saved,

    /// <summary>Empty: the stored value is kept.</summary>
    KeptEmpty,

    /// <summary>Not in the attribute list; never stored.</summary>
    Ignored,

    Rejected,
}

public sealed record KeyResult(string Key, KeyOutcome Outcome, string Message);

/// <summary>One change set for one record. <paramref name="Source"/> is "form", "upload" or "feed".</summary>
/// <param name="MeasuredAtUtc">When the values were generated (e.g. a feed's measurement time); the save time if null.</param>
public sealed record ChangeSet(
    TargetKind Target,
    string TargetId,
    IReadOnlyDictionary<string, JsonNode?> Values,
    string Source,
    DateTime? MeasuredAtUtc = null);

public sealed record SaveResult(bool Found, bool Saved, IReadOnlyList<KeyResult> Keys);

/// <summary>The keys of a change set, judged one by one, and the accepted values in the schema's own form.</summary>
public sealed record Evaluation(IReadOnlyList<KeyResult> Results, IReadOnlyDictionary<string, JsonNode> Accepted);

/// <summary>
/// The one save path for passport data (design 6.2). For each key: is it a catalogue attribute of this record kind,
/// may the sender change it, is it empty, is it valid. Accepted keys are saved together and logged; rejected keys are
/// reported and don't block the rest (J-12). Every pack is a draft until publication arrives (plan 2b).
/// </summary>
public sealed class SavePipeline(
    DppStore store,
    AttributeValueNormalizer normalizer,
    AttributeValueValidator validator,
    ApplicationAuditService audit,
    TimeProvider clock,
    DppLinks links,
    IEnumerable<IPackChangeListener> listeners)
{
    public const string AuditEventType = "dpp.change";

    private readonly IPackChangeListener[] _listeners = listeners.ToArray();

    public async Task<SaveResult> SaveAsync(ChangeSender sender, ChangeSet changes, bool preview = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(changes);

        var target = await FindTargetAsync(changes.Target, changes.TargetId, cancellationToken);
        if (!target.Found)
        {
            return new SaveResult(false, false, []);
        }

        var evaluation = Evaluate(sender, changes.Target, target.ClusterId, changes.Values);
        if (preview || evaluation.Accepted.Count == 0)
        {
            return new SaveResult(true, false, evaluation.Results);
        }

        var generatedAt = changes.MeasuredAtUtc?.ToUniversalTime() ?? clock.GetUtcNow().UtcDateTime;
        var stored = evaluation.Accepted.ToDictionary(
            entry => entry.Key,
            entry => new AttributeValue { Value = entry.Value, GeneratedAtUtc = generatedAt, Source = changes.Source, Who = sender.Who },
            StringComparer.Ordinal);
        var saved = await SetValuesAsync(changes.Target, changes.TargetId, stored, cancellationToken);
        if (saved)
        {
            await audit.AppendApplicationAuditEventAsync(
                AuditEventType,
                sender.Who,
                sender.Kind.ToString(),
                changes.Source,
                $"{stored.Count} value(s) saved on {changes.Target} {changes.TargetId}",
                AuditMetadata(evaluation.Results),
                entityType: $"dpp-{changes.Target.ToString().ToLowerInvariant()}",
                entityId: changes.TargetId,
                cancellationToken: cancellationToken);
            foreach (var listener in _listeners)
            {
                await listener.ValuesSavedAsync(changes.Target, changes.TargetId, stored.Keys.ToArray(), sender.Who, cancellationToken);
            }
        }

        return new SaveResult(true, saved, evaluation.Results);
    }

    /// <summary>Judges every key without saving anything; also used when a pack is created.</summary>
    /// <param name="packClusterId">The pack's cluster; null for other record kinds.</param>
    public Evaluation Evaluate(ChangeSender sender, TargetKind target, string? packClusterId, IReadOnlyDictionary<string, JsonNode?> values)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(values);
        var results = new List<KeyResult>();
        var accepted = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var (key, raw) in values)
        {
            var (outcome, message, value) = EvaluateKey(sender, target, packClusterId, key, raw);
            results.Add(new KeyResult(key, outcome, message));
            if (outcome == KeyOutcome.Saved)
            {
                accepted[key] = value!;
            }
        }

        return new Evaluation(results, accepted);
    }

    private (KeyOutcome Outcome, string Message, JsonNode? Value) EvaluateKey(
        ChangeSender sender, TargetKind target, string? packClusterId, string key, JsonNode? raw)
    {
        if (AttributeCatalog.FindByProperty(key) is not { } attribute)
        {
            return (KeyOutcome.Ignored, "not in the attribute list", null);
        }

        if (attribute.Storage == StorageLocation.System || attribute.Editors == AttributeEditors.System)
        {
            return (KeyOutcome.Rejected, $"#{attribute.Number} is set by the app", null);
        }

        if (!BelongsTo(attribute.Storage, target))
        {
            return (KeyOutcome.Rejected, $"#{attribute.Number} is stored on the {RecordName(attribute.Storage)}, not on the {target.ToString().ToLowerInvariant()}", null);
        }

        if (target == TargetKind.Pack && (attribute.Change is ChangeRule.OnStatusChange or ChangeRule.RestatedOnStatusChange))
        {
            return (KeyOutcome.Rejected, $"#{attribute.Number} changes only through the status change", null);
        }

        if (!EditRights.MayEdit(sender, attribute, packClusterId))
        {
            return (KeyOutcome.Rejected, $"#{attribute.Number} can't be changed by this sender", null);
        }

        if (IsEmpty(raw))
        {
            return (KeyOutcome.KeptEmpty, "empty values never overwrite", null);
        }

        var value = normalizer.Normalize(attribute, raw)!;
        var issues = validator.Validate(attribute, value);
        if (issues.Count > 0)
        {
            return (KeyOutcome.Rejected, string.Join("; ", issues.Select(issue => $"{issue.Location} {issue.Message}".Trim())), null);
        }

        // Restricted documents are served by the app behind access checks; a link elsewhere would publish them.
        if (DppLinks.RestrictedDocumentProperties.Contains(attribute.Property) && !links.IsAppUrl(value["url"]?.GetValue<string>() ?? string.Empty))
        {
            return (KeyOutcome.Rejected, $"#{attribute.Number} must link to a file uploaded to the app", null);
        }

        return (KeyOutcome.Saved, string.Empty, value);
    }

    /// <summary>Re-stated attributes start on the model; a pack only gets its own value through the status change.</summary>
    private static bool BelongsTo(StorageLocation storage, TargetKind target) => (storage, target) switch
    {
        (StorageLocation.Pack, TargetKind.Pack) => true,
        (StorageLocation.ModelWithPackOverride, TargetKind.Pack) => true,
        (StorageLocation.ModelWithPackOverride, TargetKind.Model) => true,
        (StorageLocation.Model, TargetKind.Model) => true,
        (StorageLocation.Batch, TargetKind.Batch) => true,
        (StorageLocation.Site, TargetKind.Site) => true,
        (StorageLocation.Operator, TargetKind.Operator) => true,
        _ => false,
    };

    private static string RecordName(StorageLocation storage) => storage switch
    {
        StorageLocation.ModelWithPackOverride => "model",
        StorageLocation.Site => "manufacturing site",
        StorageLocation.Operator => "economic operator",
        _ => storage.ToString().ToLowerInvariant(),
    };

    /// <summary>Missing, null, blank text, [] and {} never overwrite: no attribute accepts an empty list or object.</summary>
    private static bool IsEmpty(JsonNode? value) => value switch
    {
        null => true,
        JsonArray array => array.Count == 0,
        JsonObject obj => obj.Count == 0,
        _ => value.GetValueKind() == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetValue<string>()),
    };

    private async Task<(bool Found, string? ClusterId)> FindTargetAsync(TargetKind target, string id, CancellationToken cancellationToken)
    {
        switch (target)
        {
            case TargetKind.Pack:
                var pack = await store.GetAsync<BatteryPack>(id, cancellationToken);
                return (pack is not null, pack?.ClusterId);
            case TargetKind.Model:
                return (await store.GetAsync<BatteryModel>(id, cancellationToken) is not null, null);
            case TargetKind.Site:
                return (await store.GetAsync<ManufacturingSite>(id, cancellationToken) is not null, null);
            case TargetKind.Batch:
                return (await store.GetAsync<ProductionBatch>(id, cancellationToken) is not null, null);
            case TargetKind.Operator:
                return (await store.GetAsync<EconomicOperator>(id, cancellationToken) is not null, null);
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    private Task<bool> SetValuesAsync(TargetKind target, string id, IReadOnlyDictionary<string, AttributeValue> values, CancellationToken cancellationToken) => target switch
    {
        TargetKind.Pack => store.SetValuesAsync<BatteryPack>(id, values, cancellationToken),
        TargetKind.Model => store.SetValuesAsync<BatteryModel>(id, values, cancellationToken),
        TargetKind.Site => store.SetValuesAsync<ManufacturingSite>(id, values, cancellationToken),
        TargetKind.Batch => store.SetValuesAsync<ProductionBatch>(id, values, cancellationToken),
        TargetKind.Operator => store.SetValuesAsync<EconomicOperator>(id, values, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };

    private static BsonDocument AuditMetadata(IReadOnlyList<KeyResult> results) => new()
    {
        ["keys"] = new BsonArray(results.Select(result => new BsonDocument
        {
            ["key"] = result.Key,
            ["outcome"] = result.Outcome.ToString(),
            ["message"] = result.Message,
        })),
    };
}
```

- [ ] **Step 4: Write `DppServiceCollectionExtensions.cs`**

`web/Dpp/DppServiceCollectionExtensions.cs` (whole file after the change):

```csharp
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BatteryPassWeb.Dpp;

public static class DppServiceCollectionExtensions
{
    /// <summary>Registers the V3 passport services (design section 3). They need MongoContext, ApplicationAuditService and DemoSigningKeyService.</summary>
    public static IServiceCollection AddDpp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(_ => OfficialSchemas.Load());
        services.AddSingleton<AttributeValueValidator>();
        services.AddSingleton<AttributeValueNormalizer>();
        services.AddSingleton<DppStore>();
        services.AddSingleton<IIdentifierScheme, TestIdentifierScheme>();
        services.AddSingleton<SavePipeline>();
        services.AddSingleton<PackCreationService>();
        services.AddSingleton<PassportSigner>();
        services.AddSingleton<PublicationChecks>();
        services.AddSingleton<PublicationService>();
        services.AddSingleton<IPackChangeListener>(provider => provider.GetRequiredService<PublicationService>());
        services.AddSingleton<PassportReader>();
        services.AddSingleton<DppLinks>();
        return services;
    }
}
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.RestrictedDocumentLinkTests"`
Expected: `Passed!  - Failed:     0, Passed:     6`. The whole suite now has 1604.

- [ ] **Step 6: Commit**

```bash
git add web/Dpp/Editing/SavePipeline.cs web/Dpp/DppServiceCollectionExtensions.cs BatteryPassWeb.Tests/Dpp/SavePipelineTesting.cs BatteryPassWeb.Tests/Dpp/seed-other-industrial.json BatteryPassWeb.Tests/Dpp/RestrictedDocumentLinkTests.cs
git commit -m "feat(dpp): keep restricted documents behind the app's own links"
```


---

### Task 4: #92 at pack creation


**Files:**
- Modify: `web/Dpp/Editing/PackCreationService.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs` (whole file after the change):

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class PackCreationServiceTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = ChangeSender.Team("team@example.com");

    [Fact]
    public async Task Team_ShouldCreateADraftPackWithATestIdentifierAndStatusOriginal()
    {
        var (store, service, seed) = await ServiceAsync();

        var result = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0001\""), ("manufacturingDate", "\"2026-10\""), ("warrantyPeriod", "\"P8Y\"")));

        Assert.Null(result.Problem);
        var pack = await store.GetAsync<BatteryPack>(result.Pack!.Id);
        Assert.NotNull(pack);
        var identifier = pack.Values["uniqueProductIdentifier"].Value.GetValue<string>();
        Assert.StartsWith(TestIdentifierScheme.Prefix, identifier, StringComparison.Ordinal);
        Assert.Equal("original", pack.Values["batteryStatus"].Value["batteryStatusValues"]!.GetValue<string>());
        Assert.Equal("VPC9-0001", pack.Values["batterySerialNumber"].Value.GetValue<string>());
        Assert.Equal("cluster-test", pack.ClusterId);
        Assert.Equal("system", pack.Values["uniqueProductIdentifier"].Source);
        Assert.Equal("form", pack.Values["warrantyPeriod"].Source);
        Assert.Equal(DppStoreTesting.Now.UtcDateTime, pack.Values["manufacturingDate"].GeneratedAtUtc);
        Assert.Equal(DocumentTesting.Links.AccidentPageUrl(pack.Id), pack.Values["informationOnAccidents"].Value["url"]!.GetValue<string>());
    }

    [Fact]
    public async Task WithoutAnAppBaseUrl_PackCreationShouldStopWithAClearMessage()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var service = new PackCreationService(
            store,
            SavePipelineTesting.Pipeline(store, context),
            new TestIdentifierScheme(),
            DocumentTesting.NoLinks,
            new ApplicationAuditService(context),
            new FixedTimeProvider(DppStoreTesting.Now));

        var result = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0006\"")));

        Assert.Equal(DppLinks.NotConfigured, result.Problem);
        Assert.Empty(await store.FindPacksBySerialNumberAsync("VPC9-0006"));
    }

    [Fact]
    public async Task EachPack_ShouldGetItsOwnIdentifier()
    {
        var (_, service, seed) = await ServiceAsync();

        var first = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0002\"")));
        var second = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0002\"")));

        Assert.NotEqual(
            first.Pack!.Values["uniqueProductIdentifier"].Value.GetValue<string>(),
            second.Pack!.Values["uniqueProductIdentifier"].Value.GetValue<string>());
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldCreatePacks()
    {
        var (store, service, seed) = await ServiceAsync();

        var result = await service.CreateAsync(ChangeSender.ClusterAdmin("admin@example.com", "cluster-test"), NewPack(seed, ("batterySerialNumber", "\"VPC9-0003\"")));

        Assert.Contains("only the team", result.Problem, StringComparison.Ordinal);
        Assert.Empty(await store.FindPacksBySerialNumberAsync("VPC9-0003"));
    }

    [Theory]
    [InlineData("no-such-batch", "operator-test", "batch")]
    [InlineData("batch-test-2026", "no-such-operator", "economic operator")]
    public async Task MissingBatchOrOperator_ShouldStopCreation(string batchId, string operatorId, string expected)
    {
        var (_, service, _) = await ServiceAsync();

        var result = await service.CreateAsync(Team, new NewPack(batchId, operatorId, null, Values(("batterySerialNumber", "\"VPC9-0004\""))));

        Assert.Null(result.Pack);
        Assert.Contains(expected, result.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingSerialNumber_ShouldStopCreation()
    {
        var (_, service, seed) = await ServiceAsync();

        var result = await service.CreateAsync(Team, NewPack(seed, ("warrantyPeriod", "\"P8Y\"")));

        Assert.Null(result.Pack);
        Assert.Contains("#11", result.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("manufacturingDate", "\"2026-10-14\"")] // decision 18
    [InlineData("capacityFade", "4")] // re-stated values only through the status change
    [InlineData("stateOfChargeSoC", "120")]
    public async Task AnyRejectedValue_ShouldSaveNothing(string key, string json)
    {
        var (store, service, seed) = await ServiceAsync();

        var result = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0005\""), (key, json)));

        Assert.Null(result.Pack);
        Assert.Equal(KeyOutcome.Rejected, result.Keys.Single(k => k.Key == key).Outcome);
        Assert.Empty(await store.FindPacksBySerialNumberAsync("VPC9-0005"));
    }

    private async Task<(DppStore Store, PackCreationService Service, SeedPassport Seed)> ServiceAsync()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var service = new PackCreationService(
            store,
            SavePipelineTesting.Pipeline(store, context),
            new TestIdentifierScheme(),
            DocumentTesting.Links,
            new ApplicationAuditService(context),
            new FixedTimeProvider(DppStoreTesting.Now));
        return (store, service, seed);
    }

    private static NewPack NewPack(SeedPassport seed, params (string Key, string Json)[] values) =>
        new(seed.Sources.Batch.Id, seed.Sources.Operator.Id, "cluster-test", Values(values));

    private static Dictionary<string, JsonNode?> Values(params (string Key, string Json)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonNode.Parse(v.Json), StringComparer.Ordinal);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PackCreationServiceTests"`
Expected: the build fails because `PackCreationService` doesn't take `DppLinks` yet (CS1503 or CS1729 errors).

- [ ] **Step 3: Write `PackCreationService.cs`**

`web/Dpp/Editing/PackCreationService.cs` (whole file after the change):

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>A new pack: its batch (which gives model and site), operator, cluster and the values the team enters (#11 required; #13, #17, #19 optional).</summary>
public sealed record NewPack(
    string BatchId,
    string OperatorId,
    string? ClusterId,
    IReadOnlyDictionary<string, JsonNode?> Values,
    string Source = "form");

/// <summary>The created pack, or the problem that stopped it; <see cref="Keys"/> says what happened to each value.</summary>
public sealed record PackCreationResult(BatteryPack? Pack, IReadOnlyList<KeyResult> Keys, string? Problem);

/// <summary>
/// Creates a draft pack (design 7.1): the team only, all values accepted or nothing saved. The app issues #6, sets
/// #22 to "original" and points #92 at the pack's accident page; everything else goes through the save pipeline's checks.
/// </summary>
public sealed class PackCreationService(
    DppStore store,
    SavePipeline pipeline,
    IIdentifierScheme identifiers,
    DppLinks links,
    ApplicationAuditService audit,
    TimeProvider clock)
{
    public const string AuditEventType = "dpp.pack-created";

    public async Task<PackCreationResult> CreateAsync(ChangeSender sender, NewPack request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(request);

        if (sender.Kind != SenderKind.Team)
        {
            return Failed("only the team creates packs");
        }

        if (!links.IsConfigured)
        {
            return Failed(DppLinks.NotConfigured);
        }

        if (await store.GetAsync<ProductionBatch>(request.BatchId, cancellationToken) is null)
        {
            return Failed($"batch {request.BatchId} not found");
        }

        if (await store.GetAsync<EconomicOperator>(request.OperatorId, cancellationToken) is null)
        {
            return Failed($"economic operator {request.OperatorId} not found");
        }

        var evaluation = pipeline.Evaluate(sender, TargetKind.Pack, request.ClusterId, request.Values);
        if (evaluation.Results.Any(result => result.Outcome == KeyOutcome.Rejected))
        {
            return new PackCreationResult(null, evaluation.Results, "some values were rejected; nothing was saved");
        }

        if (!evaluation.Accepted.ContainsKey("batterySerialNumber"))
        {
            return new PackCreationResult(null, evaluation.Results, "the battery serial number (#11) is required");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var pack = new BatteryPack
        {
            Id = $"pack-{Guid.NewGuid():N}",
            BatchId = request.BatchId,
            OperatorId = request.OperatorId,
            ClusterId = request.ClusterId,
        };
        pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(identifiers.NewPackIdentifier()), now, "system", sender.Who);
        pack.Values["batteryStatus"] = Stored(new JsonObject { ["batteryStatusValues"] = "original" }, now, "system", sender.Who);
        pack.Values["informationOnAccidents"] = Stored(links.AccidentPageLink(pack.Id), now, "system", sender.Who);
        foreach (var (key, value) in evaluation.Accepted)
        {
            pack.Values[key] = Stored(value, now, request.Source, sender.Who);
        }

        await store.InsertAsync(pack, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            AuditEventType,
            sender.Who,
            sender.Kind.ToString(),
            request.Source,
            $"Draft pack {pack.Id} created",
            entityType: "dpp-pack",
            entityId: pack.Id,
            cancellationToken: cancellationToken);
        return new PackCreationResult(pack, evaluation.Results, null);

        static PackCreationResult Failed(string problem) => new(null, [], problem);
    }

    private static AttributeValue Stored(JsonNode value, DateTime generatedAtUtc, string source, string who) =>
        new() { Value = value, GeneratedAtUtc = generatedAtUtc, Source = source, Who = who };
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PackCreationServiceTests"`
Expected: `Passed!  - Failed:     0, Passed:    10`. The whole suite now has 1605.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Editing/PackCreationService.cs BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs
git commit -m "feat(dpp): point a new pack's accident information at its accident page"
```


---

### Task 5: Clusters of published packs


**Files:**
- Modify: `web/Dpp/Store/DppStore.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs`:

```csharp
namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Which clusters a model, batch or pack value is shown to: only those owning a published pack (J-6).</summary>
[Collection(MongoCollection.Name)]
public sealed class PublishedClusterStoreTests(MongoFixture mongo)
{
    [Fact]
    public async Task DraftPack_ShouldGiveItsClusterNothing()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId));
        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(modelId: "model-test"));
        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(batchId: "batch-test-2026"));
    }

    [Fact]
    public async Task PublishedPack_ShouldGiveItsClusterTheModelBatchAndPack()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        Assert.Equal(["cluster-test"], await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId));
        Assert.Equal(["cluster-test"], await rig.Store.FindPublishedClusterIdsUsingAsync(modelId: "model-test"));
        Assert.Equal(["cluster-test"], await rig.Store.FindPublishedClusterIdsUsingAsync(batchId: "batch-test-2026"));
        Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(modelId: "no-such-model"));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PublishedClusterStoreTests"`
Expected: the build fails because `FindPublishedClusterIdsUsingAsync` isn't defined yet (CS1061 errors).

- [ ] **Step 3: Write `DppStore.cs`**

`web/Dpp/Store/DppStore.cs` (whole file after the change):

```csharp
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Store;

/// <summary>
/// Reads and writes the passport source records in MongoDB (design 4.1). Without a configured database, reads
/// return nothing and writes throw, like the app's other repositories.
/// </summary>
public sealed class DppStore
{
    public const string Families = "dpp_families";
    public const string Models = "dpp_models";
    public const string Sites = "dpp_sites";
    public const string Batches = "dpp_batches";
    public const string Operators = "dpp_operators";
    public const string Packs = "dpp_packs";
    public const string Versions = "dpp_passport_versions";

    private const string UniqueProductIdentifierField = "Values.uniqueProductIdentifier.Value";
    private const string SerialNumberField = "Values.batterySerialNumber.Value";

    private static readonly Dictionary<Type, string> CollectionNames = new()
    {
        [typeof(BatteryFamily)] = Families,
        [typeof(BatteryModel)] = Models,
        [typeof(ManufacturingSite)] = Sites,
        [typeof(ProductionBatch)] = Batches,
        [typeof(EconomicOperator)] = Operators,
        [typeof(BatteryPack)] = Packs,
        [typeof(PassportVersion)] = Versions,
    };

    private readonly IMongoDatabase? _database;

    public DppStore(MongoContext mongoContext)
    {
        ArgumentNullException.ThrowIfNull(mongoContext);
        _database = mongoContext.Database;
    }

    public bool IsAvailable => _database is not null;

    /// <summary>
    /// #6 is unique; serial numbers are not (design 8.2), so they are indexed for lookup only.
    /// A batch is unique per model, site and year (decision 3). Safe to run at every start.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return;
        }

        await Collection<BatteryPack>().Indexes.CreateManyAsync(
        [
            new CreateIndexModel<BatteryPack>(
                Builders<BatteryPack>.IndexKeys.Ascending(UniqueProductIdentifierField),
                new CreateIndexOptions { Unique = true, Sparse = true, Name = "unique_product_identifier" }),
            new CreateIndexModel<BatteryPack>(
                Builders<BatteryPack>.IndexKeys.Ascending(SerialNumberField),
                new CreateIndexOptions { Name = "serial_number" }),
            new CreateIndexModel<BatteryPack>(
                Builders<BatteryPack>.IndexKeys.Ascending(pack => pack.ClusterId),
                new CreateIndexOptions { Name = "cluster" }),
            new CreateIndexModel<BatteryPack>(
                Builders<BatteryPack>.IndexKeys.Ascending(pack => pack.BatchId),
                new CreateIndexOptions { Name = "batch" }),
        ], cancellationToken);
        await Collection<ProductionBatch>().Indexes.CreateOneAsync(
            new CreateIndexModel<ProductionBatch>(
                Builders<ProductionBatch>.IndexKeys.Ascending(batch => batch.ModelId).Ascending(batch => batch.SiteId).Ascending(batch => batch.Year),
                new CreateIndexOptions { Unique = true, Name = "model_site_year" }),
            cancellationToken: cancellationToken);
        await Collection<BatteryModel>().Indexes.CreateOneAsync(
            new CreateIndexModel<BatteryModel>(
                Builders<BatteryModel>.IndexKeys.Ascending(model => model.FamilyId),
                new CreateIndexOptions { Name = "family" }),
            cancellationToken: cancellationToken);
        await Collection<PassportVersion>().Indexes.CreateManyAsync(
        [
            new CreateIndexModel<PassportVersion>(
                Builders<PassportVersion>.IndexKeys.Ascending(version => version.DigitalProductPassportId),
                new CreateIndexOptions { Unique = true, Name = "passport_id" }),
            new CreateIndexModel<PassportVersion>(
                Builders<PassportVersion>.IndexKeys.Ascending(version => version.PackId).Ascending(version => version.Number),
                new CreateIndexOptions { Unique = true, Name = "pack_number" }),
            new CreateIndexModel<PassportVersion>(
                Builders<PassportVersion>.IndexKeys.Ascending(version => version.PackId).Ascending(version => version.DppStatus),
                new CreateIndexOptions { Name = "pack_status" }),
        ], cancellationToken);
    }

    public async Task<T?> GetAsync<T>(string id, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        if (_database is null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return await Collection<T>().Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync(cancellationToken);
    }

    public Task InsertAsync<T>(T record, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        ArgumentNullException.ThrowIfNull(record);
        RequireDatabase();
        return Collection<T>().InsertOneAsync(record, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sets the given values on one record, leaving its other values as they are; false when no record has that id.
    /// Each key is set on its own, so a live feed and a form saving at the same time don't overwrite each other.
    /// </summary>
    public async Task<bool> SetValuesAsync<T>(string id, IReadOnlyDictionary<string, AttributeValue> values, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        ArgumentNullException.ThrowIfNull(values);
        RequireDatabase();
        if (values.Count == 0)
        {
            throw new ArgumentException("There are no values to set.", nameof(values));
        }

        var update = Builders<T>.Update.Combine(values.Select(entry => Builders<T>.Update.Set($"Values.{entry.Key}", entry.Value)));
        var result = await Collection<T>().UpdateOneAsync(Builders<T>.Filter.Eq("_id", id), update, cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    /// <summary>All packs with this serial number (#11). Serial numbers are not unique.</summary>
    public async Task<IReadOnlyList<BatteryPack>> FindPacksBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(serialNumber))
        {
            return [];
        }

        return await Collection<BatteryPack>().Find(Builders<BatteryPack>.Filter.Eq(SerialNumberField, serialNumber)).ToListAsync(cancellationToken);
    }

    /// <summary>The pack with this unique product identifier (#6), if any.</summary>
    public async Task<BatteryPack?> FindPackByUniqueProductIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        return await Collection<BatteryPack>().Find(Builders<BatteryPack>.Filter.Eq(UniqueProductIdentifierField, identifier)).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Everything one pack's passport is built from; null if the pack or a record it links to is missing.</summary>
    public async Task<PassportSources?> LoadSourcesAsync(string packId, CancellationToken cancellationToken = default)
    {
        var pack = await GetAsync<BatteryPack>(packId, cancellationToken);
        var batch = pack is null ? null : await GetAsync<ProductionBatch>(pack.BatchId, cancellationToken);
        if (pack is null || batch is null)
        {
            return null;
        }

        var model = await GetAsync<BatteryModel>(batch.ModelId, cancellationToken);
        var site = await GetAsync<ManufacturingSite>(batch.SiteId, cancellationToken);
        var economicOperator = await GetAsync<EconomicOperator>(pack.OperatorId, cancellationToken);
        return model is null || site is null || economicOperator is null
            ? null
            : new PassportSources(pack, batch, model, site, economicOperator);
    }

    /// <summary>The pack's active version; the newest one if two briefly overlap while a version is published.</summary>
    public async Task<PassportVersion?> GetActiveVersionAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(packId))
        {
            return null;
        }

        return await Collection<PassportVersion>()
            .Find(version => version.PackId == packId && version.DppStatus == DppStatuses.Active)
            .SortByDescending(version => version.Number)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>A version by its passport ID (#5), whatever its status.</summary>
    public async Task<PassportVersion?> GetVersionByPassportIdAsync(string passportId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(passportId))
        {
            return null;
        }

        return await Collection<PassportVersion>().Find(version => version.DigitalProductPassportId == passportId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PassportVersion>> ListVersionsAsync(string packId, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(packId))
        {
            return [];
        }

        return await Collection<PassportVersion>().Find(version => version.PackId == packId).SortBy(version => version.Number).ToListAsync(cancellationToken);
    }

    /// <summary>The highest version number of the pack; 0 before its first publication.</summary>
    public async Task<int> GetLatestVersionNumberAsync(string packId, CancellationToken cancellationToken = default)
    {
        var latest = await Collection<PassportVersion>()
            .Find(version => version.PackId == packId)
            .SortByDescending(version => version.Number)
            .Project(version => version.Number)
            .FirstOrDefaultAsync(cancellationToken);
        return latest;
    }

    /// <summary>Marks every other active version of the pack as archived.</summary>
    public Task ArchiveOtherVersionsAsync(string packId, string activeVersionId, CancellationToken cancellationToken = default) =>
        Collection<PassportVersion>().UpdateManyAsync(
            version => version.PackId == packId && version.DppStatus == DppStatuses.Active && version.Id != activeVersionId,
            Builders<PassportVersion>.Update.Set(version => version.DppStatus, DppStatuses.Archived),
            cancellationToken: cancellationToken);

    /// <summary>Sets #4 of a version (a live update made available now).</summary>
    public Task SetVersionLastUpdatedAsync(string versionId, DateTime lastUpdatedUtc, CancellationToken cancellationToken = default) =>
        Collection<PassportVersion>().UpdateOneAsync(
            version => version.Id == versionId,
            Builders<PassportVersion>.Update.Set(version => version.LastUpdatedUtc, lastUpdatedUtc),
            cancellationToken: cancellationToken);

    /// <summary>The packs whose passport is built from this model, batch, site or operator record.</summary>
    public async Task<IReadOnlyList<string>> FindPackIdsUsingAsync(string? modelId = null, string? siteId = null, string? batchId = null, string? operatorId = null, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        if (operatorId is not null)
        {
            return await Collection<BatteryPack>().Find(pack => pack.OperatorId == operatorId).Project(pack => pack.Id).ToListAsync(cancellationToken);
        }

        var batchIds = batchId is not null
            ? [batchId]
            : await Collection<ProductionBatch>()
                .Find(batch => (modelId != null && batch.ModelId == modelId) || (siteId != null && batch.SiteId == siteId))
                .Project(batch => batch.Id)
                .ToListAsync(cancellationToken);
        return await Collection<BatteryPack>().Find(pack => batchIds.Contains(pack.BatchId)).Project(pack => pack.Id).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The clusters of the published packs whose passport uses this model, batch or pack record. Draft packs count for
    /// nobody: a draft is visible to the team only (J-6).
    /// </summary>
    public async Task<IReadOnlyList<string>> FindPublishedClusterIdsUsingAsync(string? modelId = null, string? batchId = null, string? packId = null, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        var packIds = packId is not null
            ? [packId]
            : (await FindPackIdsUsingAsync(modelId: modelId, batchId: batchId, cancellationToken: cancellationToken)).ToList();
        if (packIds.Count == 0)
        {
            return [];
        }

        var published = await Collection<PassportVersion>()
            .Find(version => packIds.Contains(version.PackId) && version.DppStatus == DppStatuses.Active)
            .Project(version => version.PackId)
            .ToListAsync(cancellationToken);
        var clusters = await Collection<BatteryPack>()
            .Find(pack => published.Contains(pack.Id) && pack.ClusterId != null)
            .Project(pack => pack.ClusterId)
            .ToListAsync(cancellationToken);
        return clusters.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
    }

    private IMongoCollection<T> Collection<T>() => RequireDatabase().GetCollection<T>(CollectionName<T>());

    private IMongoDatabase RequireDatabase() =>
        _database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");

    private static string CollectionName<T>() =>
        CollectionNames.TryGetValue(typeof(T), out var name)
            ? name
            : throw new ArgumentException($"{typeof(T).Name} is not a passport record.");
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PublishedClusterStoreTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite now has 1607.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Store/DppStore.cs BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs
git commit -m "feat(dpp): find the clusters of the published packs using a record"
```


---

### Task 6: The document store


**Files:**
- Create: `web/Dpp/Documents/DocumentStore.cs`
- Test: `BatteryPassWeb.Tests/Dpp/DocumentStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/DocumentStoreTests.cs`:

```csharp
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class DocumentStoreTests(MongoFixture mongo)
{
    [Fact]
    public async Task File_ShouldComeBackUnchanged()
    {
        var documents = await StoreAsync();
        var content = DocumentTesting.Pdf("dismantling");
        var document = Record("doc-1", await documents.UploadFileAsync("dismantling.pdf", content), DppStoreTesting.Now.UtcDateTime);

        await documents.InsertAsync(document);
        var stored = await documents.GetAsync("doc-1");

        Assert.NotNull(stored);
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.Equal(TargetKind.Model, stored.OwnerKind);
        Assert.Equal(content, await documents.DownloadAsync(stored));
    }

    [Fact]
    public async Task List_ShouldShowTheNewestFirst()
    {
        var documents = await StoreAsync();
        var fileId = await documents.UploadFileAsync("a.pdf", DocumentTesting.Pdf());
        await documents.InsertAsync(Record("doc-older", fileId, DppStoreTesting.Now.UtcDateTime));
        await documents.InsertAsync(Record("doc-newer", fileId, DppStoreTesting.Now.UtcDateTime.AddHours(1)));

        var listed = await documents.ListAsync(TargetKind.Model, "model-test", "dismantlingInformation");

        Assert.Equal(["doc-newer", "doc-older"], listed.Select(d => d.Id));
        Assert.Empty(await documents.ListAsync(TargetKind.Model, "model-test", "safetyMeasures"));
    }

    [Fact]
    public async Task MissingDocument_ShouldReadAsNothing()
    {
        var documents = await StoreAsync();

        Assert.Null(await documents.GetAsync("doc-none"));
    }

    [Fact]
    public async Task WithoutADatabase_ReadsShouldReturnNothingAndWritesShouldThrow()
    {
        var documents = new DocumentStore(new MongoContext(Options.Create(new BatteryPassOptions())));

        await documents.EnsureIndexesAsync();
        Assert.Null(await documents.GetAsync("doc-1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => documents.UploadFileAsync("a.pdf", DocumentTesting.Pdf()));
    }

    private async Task<DocumentStore> StoreAsync()
    {
        var documents = new DocumentStore(mongo.NewContext());
        await documents.EnsureIndexesAsync();
        return documents;
    }

    private static DppDocument Record(string id, string fileId, DateTime uploadedAtUtc) => new()
    {
        Id = id,
        OwnerKind = TargetKind.Model,
        OwnerId = "model-test",
        Property = "dismantlingInformation",
        FileName = "dismantling.pdf",
        ContentType = "application/pdf",
        Length = 30,
        Sha256 = new string('0', 64),
        FileId = fileId,
        Title = "Dismantling manual",
        UploadedAtUtc = uploadedAtUtc,
        UploadedBy = "team@example.com",
    };
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DocumentStoreTests"`
Expected: the build fails because `DocumentStore` and `DppDocument` aren't defined yet (CS0246 errors).

- [ ] **Step 3: Write `DocumentStore.cs`**

`web/Dpp/Documents/DocumentStore.cs`:

```csharp
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace BatteryPassWeb.Dpp.Documents;

/// <summary>One uploaded file for a restricted document attribute of a model, batch or pack (design 4.1).</summary>
public sealed class DppDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonRepresentation(BsonType.String)]
    public required TargetKind OwnerKind { get; init; }

    public required string OwnerId { get; init; }

    /// <summary>The attribute's v2.0.1 property name, e.g. dismantlingInformation.</summary>
    public required string Property { get; init; }

    public required string FileName { get; init; }

    /// <summary>The type found in the file's own bytes, never the one the uploader claimed.</summary>
    public required string ContentType { get; init; }

    public required long Length { get; init; }

    /// <summary>Lowercase hex SHA-256 of the content.</summary>
    public required string Sha256 { get; init; }

    /// <summary>The GridFS file id.</summary>
    public required string FileId { get; init; }

    public required string Title { get; init; }

    public string? Language { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime UploadedAtUtc { get; init; }

    public required string UploadedBy { get; init; }
}

/// <summary>Keeps uploaded files in GridFS and their records in a collection.</summary>
public sealed class DocumentStore
{
    public const string Records = "dpp_documents";
    public const string Bucket = "dpp_files";

    private readonly IMongoDatabase? _database;

    public DocumentStore(MongoContext mongoContext)
    {
        ArgumentNullException.ThrowIfNull(mongoContext);
        _database = mongoContext.Database;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return;
        }

        await Collection().Indexes.CreateOneAsync(
            new CreateIndexModel<DppDocument>(
                Builders<DppDocument>.IndexKeys.Ascending(d => d.OwnerKind).Ascending(d => d.OwnerId).Ascending(d => d.Property),
                new CreateIndexOptions { Name = "owner_property" }),
            cancellationToken: cancellationToken);
    }

    /// <summary>Stores the file's bytes and returns the GridFS id for its record.</summary>
    public async Task<string> UploadFileAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var id = await Files().UploadFromBytesAsync(fileName, content, cancellationToken: cancellationToken);
        return id.ToString();
    }

    public Task InsertAsync(DppDocument document, CancellationToken cancellationToken = default) =>
        Collection().InsertOneAsync(document, cancellationToken: cancellationToken);

    public async Task<DppDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return await Collection().Find(d => d.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The documents of one attribute of one record, newest first.</summary>
    public async Task<IReadOnlyList<DppDocument>> ListAsync(TargetKind ownerKind, string ownerId, string property, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection()
            .Find(d => d.OwnerKind == ownerKind && d.OwnerId == ownerId && d.Property == property)
            .SortByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<byte[]> DownloadAsync(DppDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Files().DownloadAsBytesAsync(ObjectId.Parse(document.FileId), cancellationToken: cancellationToken);
    }

    private IMongoCollection<DppDocument> Collection() => RequireDatabase().GetCollection<DppDocument>(Records);

    private GridFSBucket Files() => new(RequireDatabase(), new GridFSBucketOptions { BucketName = Bucket });

    private IMongoDatabase RequireDatabase() =>
        _database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DocumentStoreTests"`
Expected: `Passed!  - Failed:     0, Passed:     4`. The whole suite now has 1611.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Documents/DocumentStore.cs BatteryPassWeb.Tests/Dpp/DocumentStoreTests.cs
git commit -m "feat(dpp): keep uploaded documents in GridFS"
```


---

### Task 7: Uploads, accident reports and downloads


**Files:**
- Create: `web/Dpp/Documents/DocumentService.cs`
- Test: `BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs`:

```csharp
using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class DocumentServiceTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task Upload_ShouldStoreTheFileAndLinkTheAttributeToIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var service = Service(rig);

        var result = await service.UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("C:\\temp\\dismantling.pdf", DocumentTesting.Pdf(), "Dismantling manual", "en-GB"));

        Assert.Null(result.Problem);
        var document = result.Document!;
        Assert.Equal("dismantling.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(64, document.Sha256.Length);
        var link = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["dismantlingInformation"].Value;
        Assert.Equal(DocumentTesting.Links.DocumentUrl(document.Id), link["url"]!.GetValue<string>());
        Assert.Equal("application/pdf", link["contentType"]!.GetValue<string>());
        Assert.Equal("Dismantling manual", link["resourceTitle"]!.GetValue<string>());
        Assert.Equal("en-GB", link["language"]!.GetValue<string>());
    }

    [Fact]
    public async Task UploadForAPublishedPacksModel_ShouldPublishANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        await Service(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "safetyMeasures", new DocumentUpload("safety.pdf", DocumentTesting.Pdf()));

        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    public async Task Images_ShouldBeRecognisedByTheirBytes(string kind, string expected)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var content = kind == "png" ? DocumentTesting.Png() : DocumentTesting.Jpeg();

        var result = await Service(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "partNumbersForComponents", new DocumentUpload("parts.pdf", content));

        Assert.Equal(expected, result.Document?.ContentType);
    }

    [Theory]
    [InlineData("not a pdf at all", 0, "only PDF, PNG and JPEG")]
    [InlineData("", 0, "empty")]
    [InlineData("%PDF-1.7 a little too long", 10, "upload limit")]
    public async Task UnacceptableFile_ShouldBeRefusedAndNothingStored(string text, long limit, string expected)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var service = Service(rig, maxUploadBytes: limit);

        var result = await service.UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("manual.pdf", Encoding.ASCII.GetBytes(text)));

        Assert.Contains(expected, result.Problem, StringComparison.Ordinal);
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Model, ModelId, "dismantlingInformation"));
    }

    [Theory]
    [InlineData("cluster-admin", TargetKind.Model, ModelId, "dismantlingInformation", "only the team")]
    [InlineData("team", TargetKind.Batch, "batch-test-2026", "euDeclarationOfConformity", "public documents are links")]
    [InlineData("team", TargetKind.Pack, "pack-VPC8C05-6788", "informationOnAccidents", "AddAccidentReportAsync")]
    [InlineData("team", TargetKind.Model, "no-such-model", "dismantlingInformation", "not found")]
    public async Task WrongUpload_ShouldBeRefused(string sender, TargetKind owner, string ownerId, string property, string expected)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var who = sender == "team" ? PublicationTesting.Team : ChangeSender.ClusterAdmin("admin@example.com", "cluster-test");

        var result = await Service(rig).UploadAsync(who, owner, ownerId, property, new DocumentUpload("file.pdf", DocumentTesting.Pdf()));

        Assert.Null(result.Document);
        Assert.Contains(expected, result.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAnAppBaseUrl_UploadsShouldBeRefused()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await Service(rig, DocumentTesting.NoLinks).UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("a.pdf", DocumentTesting.Pdf()));

        Assert.Equal(DppLinks.NotConfigured, result.Problem);
    }

    [Theory]
    [InlineData("team", true, true)]
    [InlineData("market-surveillance", true, true)]
    [InlineData("li-repair", true, true)]
    [InlineData("li-treatment", true, false)] // #45 isn't shared with treatment facilities
    [InlineData("own-cluster-user", true, true)]
    [InlineData("own-cluster-user", false, false)] // a draft pack gives its cluster nothing (J-6)
    [InlineData("other-cluster", true, false)]
    [InlineData("anonymous", true, false)]
    public async Task Read_ShouldServeTheFileOnlyToItsAudience(string kind, bool published, bool allowed)
    {
        var rig = published ? await PublicationTesting.PublishedRigAsync(mongo) : await PublicationTesting.RigAsync(mongo);
        var service = Service(rig);
        var content = DocumentTesting.Pdf("dismantling");
        var uploaded = (await service.UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("d.pdf", content))).Document!;

        var file = await service.ReadAsync(ReaderFor(kind), uploaded.Id);

        Assert.Equal(allowed, file is not null);
        if (file is not null)
        {
            Assert.Equal(content, file.Content);
        }
    }

    [Fact]
    public async Task RefusedRead_ShouldBeLogged()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var service = Service(rig);
        var uploaded = (await service.UploadAsync(PublicationTesting.Team, TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance", new DocumentUpload("tests.pdf", DocumentTesting.Pdf()))).Document!;

        Assert.Null(await service.ReadAsync(Reader.Anonymous, uploaded.Id));

        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Contains(events, e => e["eventType"].AsString == DocumentService.DeniedEvent && e["entityId"].AsString == uploaded.Id);
    }

    [Fact]
    public async Task AccidentReport_ShouldBeStoredAndOnlyMoveTheLastUpdate()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddDays(3);

        var result = await Service(rig).AddAccidentReportAsync(ChangeSender.ClusterAdmin("admin@example.com", "cluster-test"), rig.PackId, new DocumentUpload("incident-2026-11.pdf", DocumentTesting.Pdf()));

        Assert.Null(result.Problem);
        var version = Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId)); // a live update: no new version
        Assert.Equal(rig.Clock.Now.UtcDateTime, version.LastUpdatedUtc);
        var link = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["informationOnAccidents"].Value;
        Assert.Equal(DocumentTesting.Links.AccidentPageUrl(rig.PackId), link["url"]!.GetValue<string>());
    }

    [Fact]
    public async Task AccidentReportFromAnotherCluster_ShouldBeRefusedAndNothingStored()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await Service(rig).AddAccidentReportAsync(ChangeSender.ClusterAdmin("other@example.com", "another-cluster"), rig.PackId, new DocumentUpload("incident.pdf", DocumentTesting.Pdf()));

        Assert.Contains("can't be changed by this sender", result.Problem, StringComparison.Ordinal);
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Pack, rig.PackId, "informationOnAccidents"));
    }

    [Fact]
    public async Task AccidentReports_ShouldBeListedNewestFirstForTheirAudience()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var service = Service(rig);
        await service.AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("first.pdf", DocumentTesting.Pdf("1")));
        rig.Clock.Now = rig.Clock.Now.AddHours(1);
        await service.AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("second.pdf", DocumentTesting.Pdf("2")));

        var reports = await service.AccidentReportsAsync(ReaderFor("own-cluster-user"), rig.PackId);

        Assert.Equal(["second.pdf", "first.pdf"], reports!.Select(r => r.FileName));
        Assert.Null(await service.AccidentReportsAsync(Reader.Anonymous, rig.PackId));
        Assert.Null(await service.AccidentReportsAsync(ReaderFor("team"), "no-such-pack"));
    }

    private static DocumentService Service(PublicationRig rig, DppLinks? links = null, long maxUploadBytes = BatteryPassOptions.DefaultMaxUploadBytes) => new(
        new DocumentStore(rig.Context),
        rig.Store,
        rig.Pipeline,
        links ?? DocumentTesting.Links,
        Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl, MaxUploadBytes = maxUploadBytes }),
        new ApplicationAuditService(rig.Context),
        rig.Clock);

    private static Reader ReaderFor(string kind) => kind switch
    {
        "team" => Reader.With("team@example.com", ReaderRole.Team),
        "market-surveillance" => Reader.With("msa@example.com", ReaderRole.MarketSurveillance),
        "li-repair" => Reader.With("repair@example.com", ReaderRole.LiRepair),
        "li-treatment" => Reader.With("treatment@example.com", ReaderRole.LiTreatment),
        "own-cluster-user" => Reader.With("user@example.com", ReaderRole.ClusterUser, "cluster-test"),
        "other-cluster" => Reader.With("other@example.com", ReaderRole.ClusterUser, "another-cluster"),
        _ => Reader.Anonymous,
    };
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DocumentServiceTests"`
Expected: the build fails because `DocumentService`, `DocumentUpload` and `DocumentFile` aren't defined yet (CS0246 errors).

- [ ] **Step 3: Write `DocumentService.cs`**

`web/Dpp/Documents/DocumentService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Documents;

/// <summary>A file to upload. The content type is found from the bytes, so none is asked for.</summary>
public sealed record DocumentUpload(string FileName, byte[] Content, string? Title = null, string? Language = null);

public sealed record DocumentUploadResult(DppDocument? Document, string? Problem);

public sealed record DocumentFile(DppDocument Document, byte[] Content);

/// <summary>
/// Restricted documents (design 9): uploads for #29 (batch) and #45–48 (model) by the team, accident reports for #92
/// by the team or the battery's cluster admins, and downloads only for the attribute's audience. Public documents
/// are plain links saved through the pipeline and need nothing here.
/// </summary>
public sealed class DocumentService(
    DocumentStore documents,
    DppStore store,
    SavePipeline pipeline,
    DppLinks links,
    IOptions<BatteryPassOptions> options,
    ApplicationAuditService audit,
    TimeProvider clock)
{
    public const string UploadedEvent = "dpp.document-uploaded";
    public const string DeniedEvent = "dpp.document-denied";

    private const string Accidents = "informationOnAccidents";

    /// <summary>Uploads the file for a restricted document of a model or batch and links the attribute to it.</summary>
    public async Task<DocumentUploadResult> UploadAsync(ChangeSender sender, TargetKind owner, string ownerId, string property, DocumentUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(upload);
        if (property == Accidents)
        {
            return Failed("accident reports are added with AddAccidentReportAsync");
        }

        if (!DppLinks.RestrictedDocumentProperties.Contains(property))
        {
            return Failed($"{property} isn't an uploaded document; public documents are links");
        }

        if (sender.Kind != SenderKind.Team)
        {
            return Failed("only the team uploads these documents");
        }

        var documentId = $"doc-{Guid.NewGuid():N}";
        var title = string.IsNullOrWhiteSpace(upload.Title) ? SafeFileName(upload.FileName) : upload.Title.Trim();
        return await StoreAndLinkAsync(sender, owner, ownerId, property, documentId, title, upload, contentType => DocumentLink(contentType, documentId, title, upload.Language), cancellationToken);
    }

    /// <summary>
    /// Adds an accident report to a pack (#92). #92 keeps pointing at the pack's accident page; saving it again counts
    /// as a live update, so a published passport's #4 moves (J-14).
    /// </summary>
    public Task<DocumentUploadResult> AddAccidentReportAsync(ChangeSender sender, string packId, DocumentUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(upload);
        var title = string.IsNullOrWhiteSpace(upload.Title) ? SafeFileName(upload.FileName) : upload.Title.Trim();
        return StoreAndLinkAsync(sender, TargetKind.Pack, packId, Accidents, $"doc-{Guid.NewGuid():N}", title, upload, _ => links.AccidentPageLink(packId), cancellationToken);
    }

    /// <summary>The file, when the reader may see its attribute; otherwise null, and the refusal is logged.</summary>
    public async Task<DocumentFile?> ReadAsync(Reader reader, string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var document = await documents.GetAsync(documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (!await MaySeeAsync(reader, document.OwnerKind, document.OwnerId, document.Property, cancellationToken))
        {
            await audit.AppendApplicationAuditEventAsync(
                DeniedEvent,
                reader.Who,
                string.Join(",", reader.Roles),
                "documents",
                $"Download of document {documentId} refused",
                entityType: "dpp-document",
                entityId: documentId,
                cancellationToken: cancellationToken);
            return null;
        }

        return new DocumentFile(document, await documents.DownloadAsync(document, cancellationToken));
    }

    /// <summary>A pack's accident reports, newest first, when the reader may see #92; otherwise null.</summary>
    public async Task<IReadOnlyList<DppDocument>?> AccidentReportsAsync(Reader reader, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is null
            || !await MaySeeAsync(reader, TargetKind.Pack, packId, Accidents, cancellationToken))
        {
            return null;
        }

        return await documents.ListAsync(TargetKind.Pack, packId, Accidents, cancellationToken);
    }

    private async Task<DocumentUploadResult> StoreAndLinkAsync(
        ChangeSender sender,
        TargetKind owner,
        string ownerId,
        string property,
        string documentId,
        string title,
        DocumentUpload upload,
        Func<string, JsonObject> linkFor,
        CancellationToken cancellationToken)
    {
        if (!links.IsConfigured)
        {
            return Failed(DppLinks.NotConfigured);
        }

        var (contentType, problem) = Inspect(upload);
        if (problem is not null)
        {
            return Failed(problem);
        }

        // Check the link first (record exists, sender may change it, value valid), so nothing is stored for a refusal.
        var change = new ChangeSet(owner, ownerId, new Dictionary<string, JsonNode?> { [property] = linkFor(contentType!) }, "form");
        var preview = await pipeline.SaveAsync(sender, change, preview: true, cancellationToken);
        if (!preview.Found)
        {
            return Failed($"{owner.ToString().ToLowerInvariant()} {ownerId} not found");
        }

        var outcome = preview.Keys.Single();
        if (outcome.Outcome != KeyOutcome.Saved)
        {
            return Failed(outcome.Message);
        }

        var document = new DppDocument
        {
            Id = documentId,
            OwnerKind = owner,
            OwnerId = ownerId,
            Property = property,
            FileName = SafeFileName(upload.FileName),
            ContentType = contentType!,
            Length = upload.Content.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(upload.Content)),
            FileId = await documents.UploadFileAsync(SafeFileName(upload.FileName), upload.Content, cancellationToken),
            Title = title,
            Language = upload.Language,
            UploadedAtUtc = clock.GetUtcNow().UtcDateTime,
            UploadedBy = sender.Who,
        };
        await documents.InsertAsync(document, cancellationToken);
        await pipeline.SaveAsync(sender, change, cancellationToken: cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            UploadedEvent,
            sender.Who,
            sender.Kind.ToString(),
            "documents",
            $"{document.FileName} uploaded for {property} of {owner.ToString().ToLowerInvariant()} {ownerId}",
            new BsonDocument { ["documentId"] = document.Id, ["sha256"] = document.Sha256, ["contentType"] = document.ContentType },
            entityType: "dpp-document",
            entityId: document.Id,
            cancellationToken: cancellationToken);
        return new DocumentUploadResult(document, null);
    }

    private async Task<bool> MaySeeAsync(Reader reader, TargetKind owner, string ownerId, string property, CancellationToken cancellationToken)
    {
        var attribute = AttributeCatalog.FindByProperty(property);
        if (attribute is null)
        {
            return false;
        }

        var clusters = owner switch
        {
            TargetKind.Pack => await store.FindPublishedClusterIdsUsingAsync(packId: ownerId, cancellationToken: cancellationToken),
            TargetKind.Batch => await store.FindPublishedClusterIdsUsingAsync(batchId: ownerId, cancellationToken: cancellationToken),
            TargetKind.Model => await store.FindPublishedClusterIdsUsingAsync(modelId: ownerId, cancellationToken: cancellationToken),
            _ => [],
        };
        return AudienceRules.MaySee(reader, attribute, clusters);
    }

    /// <summary>Only PDF, PNG and JPEG, recognised by their first bytes; empty and oversized files are refused.</summary>
    private (string? ContentType, string? Problem) Inspect(DocumentUpload upload)
    {
        if (upload.Content.Length == 0)
        {
            return (null, "the file is empty");
        }

        var limit = options.Value.MaxUploadBytes;
        if (limit > 0 && upload.Content.LongLength > limit)
        {
            return (null, $"the file is larger than the {limit}-byte upload limit");
        }

        ReadOnlySpan<byte> content = upload.Content;
        var contentType = content.StartsWith("%PDF-"u8) ? "application/pdf"
            : content.StartsWith(PngSignature) ? "image/png"
            : content.StartsWith(JpegSignature) ? "image/jpeg"
            : null;
        return contentType is null
            ? (null, "only PDF, PNG and JPEG files are accepted, recognised by their content")
            : (contentType, null);
    }

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    private JsonObject DocumentLink(string contentType, string documentId, string title, string? language)
    {
        var link = new JsonObject
        {
            ["contentType"] = contentType,
            ["url"] = links.DocumentUrl(documentId),
            ["resourceTitle"] = title,
        };
        if (!string.IsNullOrWhiteSpace(language))
        {
            link["language"] = language;
        }

        return link;
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        return name.Length == 0 ? "document" : name;
    }

    private static DocumentUploadResult Failed(string problem) => new(null, problem);
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DocumentServiceTests"`
Expected: `Passed!  - Failed:     0, Passed:    24`. The whole suite now has 1635.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Documents/DocumentService.cs BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs
git commit -m "feat(dpp): upload restricted documents and serve them only to their audience"
```


---

### Task 8: Register the document services


**Files:**
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs` (whole file after the change):

```csharp
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class DppServiceWiringTests
{
    [Fact]
    public void AddDpp_ShouldResolveEveryServiceWithoutADatabase()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl }))
            .AddSingleton<MongoContext>()
            .AddSingleton<ApplicationAuditService>()
            .AddSingleton(new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256)))
            .AddDpp()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<SavePipeline>());
        Assert.NotNull(provider.GetRequiredService<PackCreationService>());
        Assert.IsType<TestIdentifierScheme>(provider.GetRequiredService<IIdentifierScheme>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        Assert.NotNull(provider.GetRequiredService<PassportReader>());
        Assert.NotNull(provider.GetRequiredService<DocumentService>());
        Assert.True(provider.GetRequiredService<DppLinks>().IsConfigured);
        Assert.Same(provider.GetRequiredService<PublicationService>(), Assert.Single(provider.GetServices<IPackChangeListener>()));
    }

    [Fact]
    public void Program_ShouldRegisterTheServicesAndCreateTheIndexesAtStartup()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("builder.Services.AddDpp();", program, StringComparison.Ordinal);
        Assert.Contains("await scope.ServiceProvider.GetRequiredService<DppStore>().EnsureIndexesAsync();", program, StringComparison.Ordinal);
        Assert.Contains("await scope.ServiceProvider.GetRequiredService<DocumentStore>().EnsureIndexesAsync();", program, StringComparison.Ordinal);
        Assert.Contains("APP_BASE_URL is not set", program, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: both wiring tests fail: `DocumentService` isn't registered, and `Program.cs` doesn't create the document indexes or warn about `APP_BASE_URL`.

- [ ] **Step 3: Write `DppServiceCollectionExtensions.cs`**

`web/Dpp/DppServiceCollectionExtensions.cs` (whole file after the change):

```csharp
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BatteryPassWeb.Dpp;

public static class DppServiceCollectionExtensions
{
    /// <summary>
    /// Registers the V3 passport services (design section 3). They need MongoContext, ApplicationAuditService,
    /// DemoSigningKeyService and the BatteryPassOptions (APP_BASE_URL for document links).
    /// </summary>
    public static IServiceCollection AddDpp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(_ => OfficialSchemas.Load());
        services.AddSingleton<AttributeValueValidator>();
        services.AddSingleton<AttributeValueNormalizer>();
        services.AddSingleton<DppStore>();
        services.AddSingleton<IIdentifierScheme, TestIdentifierScheme>();
        services.AddSingleton<SavePipeline>();
        services.AddSingleton<PackCreationService>();
        services.AddSingleton<PassportSigner>();
        services.AddSingleton<PublicationChecks>();
        services.AddSingleton<PublicationService>();
        services.AddSingleton<IPackChangeListener>(provider => provider.GetRequiredService<PublicationService>());
        services.AddSingleton<PassportReader>();
        services.AddSingleton<DppLinks>();
        services.AddSingleton<DocumentStore>();
        services.AddSingleton<DocumentService>();
        return services;
    }
}
```

- [ ] **Step 4: Wire `web/Program.cs`**

In `web/Program.cs`, add after `using BatteryPassWeb.Dpp;`:

```csharp
using BatteryPassWeb.Dpp.Documents;
```

and in the startup block, right after `await scope.ServiceProvider.GetRequiredService<DppStore>().EnsureIndexesAsync();`:

```csharp
    await scope.ServiceProvider.GetRequiredService<DocumentStore>().EnsureIndexesAsync();
    if (!scope.ServiceProvider.GetRequiredService<DppLinks>().IsConfigured)
    {
        app.Logger.LogWarning("APP_BASE_URL is not set: battery packs can't be created and documents can't be linked until it is.");
    }
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite now has 1635.

- [ ] **Step 6: Commit**

```bash
git add web/Dpp/DppServiceCollectionExtensions.cs BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs web/Program.cs
git commit -m "feat(dpp): register the document services and warn when APP_BASE_URL is missing"
```


---

### Task 9: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  1635, Skipped:     0, Total:  1635` (783 + 852 new).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

## After this plan

Plan 2d builds the team's pages on these services: uploading documents, adding accident reports, publishing. Step 3 maps sign-ins and tokens to a `Reader` and adds the web addresses `DppLinks` issues: the access-checked download at `/dpp/documents/{id}` (through `DocumentService.ReadAsync`) and the accident page at `/dpp/packs/{packId}/accidents` (through `DocumentService.AccidentReportsAsync`), plus the visibility filter on pages and the API with `AudienceRules`.

Two notes for step 3's download route:

- **Headers:** set `Content-Type` from the stored (byte-checked) type, never from the request, and add `Content-Disposition` and `X-Content-Type-Options: nosniff`.
- **Memory:** `DocumentService.ReadAsync` loads the whole file into memory. That is fine at the 10 MB upload limit; keep that limit, or stream if it ever has to grow.

