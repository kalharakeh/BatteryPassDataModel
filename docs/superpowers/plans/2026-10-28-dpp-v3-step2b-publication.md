# DPP V3 Step 2b: Publication and Versions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish signed passport versions: the publication checks, passport IDs (#5), signing of the static core, automatic new versions after accepted static changes, live updates that only refresh #4, archived versions readable by their #5, and tamper detection.

**Architecture:** Builds on 2a. `PublicationService` assembles a pack's passport, checks it (`PublicationChecks`), extracts the static core (everything except #2, #4 and the live set), signs it with the app's existing ECDSA key (`PassportSigner`, over `CanonicalJson`) and stores a `PassportVersion`. It listens to the save pipeline (`IPackChangeListener`) to keep versions current. `PassportReader` composes what readers see: the active version's core, its #2 and #4, and the pack's live values.

**Tech Stack:** .NET 10, MongoDB.Driver 3.9, JsonSchema.Net 9.5.0, xUnit 2.9, EphemeralMongo 3.2.0 (MongoDB 8, the same major as the Atlas database, 8.0.34).

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 4.2, 4.3, 7.1, 7.2, 11, 14) and the decisions record (1–22). Earlier plans: step 1 and 2a in `docs/superpowers/plans/`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** 2a is on `feature/dpp-v3-step2a-store-and-save` (local only). Start 2b on top of it: `git switch feature/dpp-v3-step2a-store-and-save && git switch -c feature/dpp-v3-step2b-publication`.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:   730`. After this plan: 783. The suite takes about 12 seconds.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`.
- Tests use a throwaway in-memory signing key (`SigningTesting.Keys`), never the app's local key file.

## Judgement calls in this plan

1. **What a version holds:** its #5, its DPP status, its publication time, #4, who published it, the signed static core and the signature proof. Readers get the core plus #2, #4 and, for the active version, the pack's live values. **Archived versions carry no live values** (live values aren't versioned), so they lack the required live set. That is expected; never add today's values to them.
2. **Versions follow the save pipeline:**
   - an accepted static change on a published pack publishes a new version and archives the old one;
   - identical content creates no version;
   - a live change sets #4 to the **save time** (the file: when it was made available), while the value keeps its measurement time;
   - a model, batch, site or operator change republishes the published packs that use it (the editor's confirmation comes in 2d);
   - drafts are never published by a change.
3. **A published pack can't become unpublishable through the app.** The pipeline never deletes a value (empty values never overwrite) and checks every value. If a republish is blocked anyway (data damaged outside the app, or a rule change), the active version stays, a `dpp.publish-blocked` event records why, and the save itself stands.
4. **Version numbers** are the highest stored number + 1, unique per pack. Two saves publishing at the same moment hit the unique index; the loser retries once.
5. **"Unchanged"** compares the canonical hash of the content without #5, which is new for every version.
6. **Canonical JSON:** keys sorted, no whitespace, numbers in their shortest form (520.0 and 520 give the same text), escaping only where JSON requires. The same value always gives the same text, wherever it came from.
7. **Signing** reuses `DemoSigningKeyService` (ECDSA P-256) and the existing proof shape, with cryptosuite `ecdsa-p256-sha256-dpp-canonical-2026`. **Verification uses the public key stored in the proof**, so changing the signing key never breaks old versions. Whether that key belongs to the issuer is the trust pages' question.
8. **Publication gate:**
   - **Missing attributes:** a missing attribute due by Feb 2027 blocks; one required only later warns; a conditional one is ignored (J-9).
   - **Schema errors:** shape errors from either official schema block, including a document link without its url.
   - **Left to the catalogue:** the schemas' own "attribute missing" errors and their summary lines.
9. **Only the team publishes a first version;** automatic republishes are recorded under the person whose save caused them.
10. **#5 test IDs** are `urn:x-battery-pass:TEST-DPP-<guid>`. DPP status values here are "active" and "archived"; step 4 adds the others.

## File structure

| File | Responsibility |
|---|---|
| `web/Dpp/Signing/CanonicalJson.cs` | Canonical text and hash of a JSON value |
| `web/Dpp/Assembly/PassportStaticCore.cs` | What a version signs, its content hash, and the passport readers see |
| `web/Dpp/Assembly/PassportAssembler.cs` (modify) | Shares #4's timestamp format |
| `web/Dpp/Signing/PassportSigner.cs` | The signature proof; sign and verify |
| `web/Dpp/Publication/PublicationChecks.cs` | Blocking items and warnings before publication |
| `web/Dpp/Store/PassportVersion.cs` | The version record and the DPP status values |
| `web/Dpp/Store/DppStore.cs` (modify) | Versions: indexes, active version, by #5, list, numbers, archive, #4, packs using a record |
| `web/Dpp/Identifiers/TestIdentifierScheme.cs` (modify) | #5 test IDs |
| `web/Dpp/Editing/IPackChangeListener.cs`, `web/Dpp/Editing/SavePipeline.cs` (modify) | The pipeline tells listeners what it saved |
| `web/Dpp/Publication/PublicationService.cs` | Publishes and keeps versions current |
| `web/Dpp/Publication/PassportReader.cs` | Current passport, versions by #5, verification |
| `web/Dpp/DppServiceCollectionExtensions.cs` (modify) | Registers the publication services |
| `BatteryPassWeb.Tests/Dpp/*` | Tests and helpers (`SigningTesting`, `PublicationTesting` with a movable clock) |


---

### Task 1: Canonical JSON


**Files:**
- Create: `web/Dpp/Signing/CanonicalJson.cs`
- Test: `BatteryPassWeb.Tests/Dpp/CanonicalJsonTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/CanonicalJsonTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Signing;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class CanonicalJsonTests
{
    [Fact]
    public void Serialize_ShouldSortKeysAndDropWhitespace()
    {
        var value = JsonNode.Parse("""{ "b": 1, "a": { "d": 2, "c": [3, 1] } }""");

        Assert.Equal("""{"a":{"c":[3,1],"d":2},"b":1}""", CanonicalJson.Serialize(value));
    }

    [Theory]
    [InlineData("520.0", "520")]
    [InlineData("520", "520")]
    [InlineData("75.4", "75.4")]
    [InlineData("-20", "-20")]
    [InlineData("0.5", "0.5")]
    [InlineData("1.20", "1.2")]
    public void Numbers_ShouldBeWrittenInTheirShortestForm(string json, string expected)
    {
        Assert.Equal(expected, CanonicalJson.Serialize(JsonNode.Parse(json)));
    }

    [Fact]
    public void SameValueFromDifferentSources_ShouldGiveTheSameText()
    {
        var parsed = JsonNode.Parse("""{"voltValue":520.0,"x":75.4}""");
        var built = new JsonObject { ["x"] = 75.4, ["voltValue"] = 520d };

        Assert.Equal(CanonicalJson.Serialize(parsed), CanonicalJson.Serialize(built));
        Assert.Equal(CanonicalJson.Hash(parsed), CanonicalJson.Hash(built));
    }

    [Fact]
    public void Text_ShouldKeepLettersAndEscapeOnlyWhatJsonRequires()
    {
        var value = new JsonObject { ["unit"] = "°C", ["address"] = "Exempelvägen 1", ["quote"] = "say \"hi\"" };

        Assert.Equal("""{"address":"Exempelvägen 1","quote":"say \"hi\"","unit":"°C"}""", CanonicalJson.Serialize(value));
    }

    [Fact]
    public void Hash_ShouldChangeWithTheValue()
    {
        var hash = CanonicalJson.Hash(JsonNode.Parse("""{"a":1}"""));

        Assert.StartsWith("sha256:", hash, StringComparison.Ordinal);
        Assert.Equal(71, hash.Length);
        Assert.NotEqual(hash, CanonicalJson.Hash(JsonNode.Parse("""{"a":2}""")));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.CanonicalJsonTests"`
Expected: the build fails because `CanonicalJson` isn't defined yet (CS0234 or CS0246 errors).

- [ ] **Step 3: Write `CanonicalJson.cs`**

`web/Dpp/Signing/CanonicalJson.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Signing;

/// <summary>
/// The canonical text of a JSON value, for hashing and signing: object keys sorted by ordinal, no whitespace, numbers
/// written in their shortest form (520.0 and 520 give the same text), and text escaped only where JSON requires it.
/// The same value always gives the same text, whether it came from a form, a file or MongoDB.
/// </summary>
public static class CanonicalJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    public static string Serialize(JsonNode? node)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            Write(writer, node);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>"sha256:" and the lowercase hex SHA-256 of the canonical text.</summary>
    public static string Hash(JsonNode? node) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(node))));

    private static void Write(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var (key, value) in obj.OrderBy(property => property.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(key);
                    Write(writer, value);
                }

                writer.WriteEndObject();
                break;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                WriteValue(writer, node.AsValue());
                break;
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonValue value)
    {
        var element = JsonSerializer.SerializeToElement(value);
        if (element.ValueKind != JsonValueKind.Number)
        {
            element.WriteTo(writer);
        }
        else if (element.TryGetInt64(out var whole))
        {
            writer.WriteNumberValue(whole);
        }
        else
        {
            var number = element.GetDouble();
            if (number == Math.Floor(number) && Math.Abs(number) < 1e15)
            {
                writer.WriteNumberValue((long)number);
            }
            else
            {
                writer.WriteNumberValue(number);
            }
        }
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.CanonicalJsonTests"`
Expected: `Passed!  - Failed:     0, Passed:    10`. The whole suite now has 740.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Signing/CanonicalJson.cs BatteryPassWeb.Tests/Dpp/CanonicalJsonTests.cs
git commit -m "feat(dpp): canonical JSON text and hash for signing"
```


---

### Task 2: The static core

What a version signs, and the passport readers see. #4's format moves into a shared helper on the assembler.

**Files:**
- Modify: `web/Dpp/Assembly/PassportAssembler.cs`
- Create: `web/Dpp/Assembly/PassportStaticCore.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PassportStaticCoreTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PassportStaticCoreTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class PassportStaticCoreTests
{
    [Fact]
    public void Extract_ShouldLeaveOutTheStatusTheLastUpdateAndTheLiveSet()
    {
        var core = PassportStaticCore.Extract(SeedPassport());

        Assert.Equal([2, 4, 63, 83, 87, 92], PassportStaticCore.NotSigned.Select(a => a.Number));
        Assert.All(PassportStaticCore.NotSigned, attribute => Assert.Null(Find(core, attribute)));
        Assert.All(
            AttributeCatalog.All.Except(PassportStaticCore.NotSigned),
            attribute => Assert.NotNull(Find(core, attribute)));
    }

    [Fact]
    public void ContentHash_ShouldIgnoreThePassportIdButNotTheContent()
    {
        var core = PassportStaticCore.Extract(SeedPassport());
        var otherId = core.DeepClone().AsObject();
        otherId["dppHeader"]!["digitalProductPassportId"] = "urn:test:dpp:VPC8C05-6788:v2";
        var otherContent = core.DeepClone().AsObject();
        otherContent["batteryPassportData"]!["identifiersAndProductData"]!["thirdPartyServiceProvider"] = "Another AB";

        Assert.Equal(PassportStaticCore.ContentHash(core), PassportStaticCore.ContentHash(otherId));
        Assert.NotEqual(PassportStaticCore.ContentHash(core), PassportStaticCore.ContentHash(otherContent));
    }

    [Fact]
    public void Compose_ShouldAddTheHeaderAndTheLiveValues()
    {
        var seed = DppTestSeed.Load();
        var core = PassportStaticCore.Extract(PassportAssembler.Assemble(seed.Sources, seed.Header));
        var updated = new DateTime(2026, 10, 29, 7, 15, 0, DateTimeKind.Utc);

        var passport = PassportStaticCore.Compose(core, "active", updated, seed.Sources.Pack.Values);

        Assert.Equal("active", passport["dppHeader"]!["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Equal("2026-10-29T07:15:00Z", passport["dppHeader"]!["lastUpdated"]!.GetValue<string>());
        Assert.Equal(76, passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]!["percentageValue"]!.GetValue<int>());
        Assert.Empty(DppStoreTesting.Schemas.CheckPassport(passport, feb2027Variant: true));
    }

    [Fact]
    public void ComposeWithoutLiveValues_ShouldLeaveThemOut()
    {
        var core = PassportStaticCore.Extract(SeedPassport());

        var passport = PassportStaticCore.Compose(core, "archived", DppStoreTesting.Now.UtcDateTime, liveValues: null);

        Assert.Equal("archived", passport["dppHeader"]!["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Null(passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]);
    }

    private static JsonObject SeedPassport()
    {
        var seed = DppTestSeed.Load();
        return PassportAssembler.Assemble(seed.Sources, seed.Header);
    }

    private static JsonNode? Find(JsonObject passport, AttributeDefinition attribute) =>
        attribute.Section == AttributeDefinition.HeaderSection
            ? passport["dppHeader"]?[attribute.Property]
            : passport["batteryPassportData"]?[attribute.Section]?[attribute.Property];
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportStaticCoreTests"`
Expected: the build fails because `PassportStaticCore` isn't defined yet (a CS0103 error).

- [ ] **Step 3: Write `PassportAssembler.cs`**

`web/Dpp/Assembly/PassportAssembler.cs` (whole file after the change):

```csharp
using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Assembly;

/// <summary>The values that belong to one passport version (#2, #4, #5); versioning supplies them from step 2.</summary>
public sealed record PassportHeader(string DigitalProductPassportId, string DppStatus, DateTime LastUpdatedUtc);

/// <summary>The records one pack's passport is built from.</summary>
public sealed record PassportSources(
    BatteryPack Pack,
    ProductionBatch Batch,
    BatteryModel Model,
    ManufacturingSite Site,
    EconomicOperator Operator);

/// <summary>
/// Builds the v2.0.1 passport JSON for one pack (design section 3). Each attribute is read from the record the
/// catalogue names; stored values are copied as they are, and missing ones are left out.
/// </summary>
public static class PassportAssembler
{
    /// <summary>#1. Names the data model until the JTC-24 standards are published in the Official Journal (plan judgement call).</summary>
    public const string DppSchemaVersion = "BatteryPass-Ready 2.0.1";

    public static JsonObject Assemble(PassportSources sources, PassportHeader header)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(header);

        var passport = new JsonObject();
        foreach (var attribute in AttributeCatalog.All)
        {
            if (ValueFor(attribute, sources, header) is not { } value)
            {
                continue;
            }

            var parent = attribute.Section == AttributeDefinition.HeaderSection
                ? Child(passport, AttributeDefinition.HeaderSection)
                : Child(Child(passport, AttributeDefinition.DataRoot), attribute.Section);
            parent[attribute.Property] = value;
        }

        return passport;
    }

    private static JsonNode? ValueFor(AttributeDefinition attribute, PassportSources sources, PassportHeader header) =>
        attribute.Storage switch
        {
            StorageLocation.System => SystemValue(attribute.Number, header),
            StorageLocation.Pack => Stored(sources.Pack.Values, attribute),
            StorageLocation.Operator => Stored(sources.Operator.Values, attribute),
            StorageLocation.Batch => Stored(sources.Batch.Values, attribute),
            StorageLocation.Site => Stored(sources.Site.Values, attribute),
            StorageLocation.Model => Stored(sources.Model.Values, attribute),
            StorageLocation.ModelWithPackOverride => Stored(sources.Pack.Values, attribute) ?? Stored(sources.Model.Values, attribute),
            _ => throw new InvalidOperationException($"Unknown storage for #{attribute.Number}."),
        };

    private static JsonNode? Stored(Dictionary<string, AttributeValue> values, AttributeDefinition attribute) =>
        values.TryGetValue(attribute.Property, out var stored) ? stored.Value.DeepClone() : null;

    private static JsonNode SystemValue(int number, PassportHeader header) => number switch
    {
        1 => JsonValue.Create(DppSchemaVersion),
        2 => new JsonObject { ["dppStatusValue"] = header.DppStatus },
        3 => new JsonObject { ["dppGranularityValue"] = "item" },
        4 => JsonValue.Create(FormatTimestamp(header.LastUpdatedUtc)),
        5 => JsonValue.Create(header.DigitalProductPassportId),
        _ => throw new InvalidOperationException($"#{number} is not a system attribute."),
    };

    /// <summary>#4's format: UTC, ISO 8601, whole seconds (e.g. 2026-10-28T09:30:00Z).</summary>
    public static string FormatTimestamp(DateTime utc) =>
        (utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static JsonObject Child(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[name] = created;
        return created;
    }
}
```

- [ ] **Step 4: Write `PassportStaticCore.cs`**

`web/Dpp/Assembly/PassportStaticCore.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Assembly;

/// <summary>
/// The part of a passport a version signs (design 4.3): everything except #2 (DPP status), #4 (last update) and the
/// live set (#63, #83, #87, #92), which change without a new version.
/// </summary>
public static class PassportStaticCore
{
    public static IReadOnlyList<AttributeDefinition> NotSigned { get; } =
        AttributeCatalog.All.Where(a => a.Number is 2 or 4 || a.Change == ChangeRule.Live).ToArray();

    public static JsonObject Extract(JsonObject passport)
    {
        ArgumentNullException.ThrowIfNull(passport);
        var core = passport.DeepClone().AsObject();
        foreach (var attribute in NotSigned)
        {
            Parent(core, attribute)?.Remove(attribute.Property);
        }

        return core;
    }

    /// <summary>
    /// Identifies a version's content without its own passport ID (#5), which is new for every version: two cores with
    /// the same content hash would publish the same passport.
    /// </summary>
    public static string ContentHash(JsonNode core)
    {
        ArgumentNullException.ThrowIfNull(core);
        var content = core.DeepClone().AsObject();
        (content[AttributeDefinition.HeaderSection] as JsonObject)?.Remove(AttributeCatalog.ByNumber(5).Property);
        return CanonicalJson.Hash(content);
    }

    /// <summary>
    /// The passport readers see: the signed core with the version's #2 and #4 and, for the active version, the pack's
    /// live values. Archived versions get no live values, because live values aren't versioned; so an archived passport
    /// lacks the required live set and doesn't pass the schema's required lists. That is expected.
    /// </summary>
    public static JsonObject Compose(JsonNode core, string dppStatus, DateTime lastUpdatedUtc, IReadOnlyDictionary<string, AttributeValue>? liveValues)
    {
        ArgumentNullException.ThrowIfNull(core);
        var passport = core.DeepClone().AsObject();
        var header = Child(passport, AttributeDefinition.HeaderSection);
        header["dppStatus"] = new JsonObject { ["dppStatusValue"] = dppStatus };
        header["lastUpdated"] = PassportAssembler.FormatTimestamp(lastUpdatedUtc);
        foreach (var attribute in NotSigned.Where(a => a.Change == ChangeRule.Live))
        {
            if (liveValues is not null && liveValues.TryGetValue(attribute.Property, out var value))
            {
                Child(Child(passport, AttributeDefinition.DataRoot), attribute.Section)[attribute.Property] = value.Value.DeepClone();
            }
        }

        return passport;
    }

    private static JsonObject? Parent(JsonObject passport, AttributeDefinition attribute) =>
        attribute.Section == AttributeDefinition.HeaderSection
            ? passport[AttributeDefinition.HeaderSection] as JsonObject
            : passport[AttributeDefinition.DataRoot]?[attribute.Section] as JsonObject;

    private static JsonObject Child(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[name] = created;
        return created;
    }
}
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportStaticCoreTests"`
Expected: `Passed!  - Failed:     0, Passed:     4`. The whole suite now has 744.

- [ ] **Step 6: Commit**

```bash
git add web/Dpp/Assembly/PassportAssembler.cs web/Dpp/Assembly/PassportStaticCore.cs BatteryPassWeb.Tests/Dpp/PassportStaticCoreTests.cs
git commit -m "feat(dpp): separate the signed static core from the live parts of a passport"
```


---

### Task 3: Signing


**Files:**
- Create: `web/Dpp/Signing/PassportSigner.cs`
- Test: `BatteryPassWeb.Tests/Dpp/SigningTesting.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PassportSignerTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/SigningTesting.cs`:

```csharp
using System.Security.Cryptography;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

internal static class SigningTesting
{
    /// <summary>A throwaway signing key, so tests never create the app's local key file.</summary>
    public static readonly DemoSigningKeyService Keys = new(ECDsa.Create(ECCurve.NamedCurves.nistP256));
}
```

`BatteryPassWeb.Tests/Dpp/PassportSignerTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class PassportSignerTests
{
    private static readonly DateTime Created = new(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void SignedCore_ShouldVerify()
    {
        var core = SeedCore();

        var proof = new PassportSigner(SigningTesting.Keys).Sign(core, Created);

        Assert.True(PassportSigner.Verify(core, proof));
        Assert.Equal(PassportSigner.ProofType, proof.Type);
        Assert.Equal(PassportSigner.Cryptosuite, proof.Cryptosuite);
        Assert.Equal(CanonicalJson.Hash(core), proof.Hash);
        Assert.Equal(Created, proof.CreatedUtc);
    }

    [Fact]
    public void ChangedCore_ShouldNotVerify()
    {
        var core = SeedCore();
        var proof = new PassportSigner(SigningTesting.Keys).Sign(core, Created);

        core["batteryPassportData"]!["identifiersAndProductData"]!["batterySerialNumber"] = "VPC8C05-6789";

        Assert.False(PassportSigner.Verify(core, proof));
    }

    [Fact]
    public void Verification_ShouldUseTheKeyStoredInTheProof()
    {
        var core = SeedCore();
        var proof = new PassportSigner(SigningTesting.Keys).Sign(core, Created);
        using var otherKey = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        var otherPublic = otherKey.ExportPublicParameters();
        var swapped = new SignatureProof
        {
            Type = proof.Type,
            Cryptosuite = proof.Cryptosuite,
            CreatedUtc = proof.CreatedUtc,
            Issuer = proof.Issuer,
            VerificationMethod = proof.VerificationMethod,
            Hash = proof.Hash,
            ProofValue = proof.ProofValue,
            PublicKeyX = Base64Url.Encode(otherPublic.Q.X!),
            PublicKeyY = Base64Url.Encode(otherPublic.Q.Y!),
        };

        Assert.True(PassportSigner.Verify(core, proof)); // still true after the app's key changes: the proof carries its key
        Assert.False(PassportSigner.Verify(core, swapped));
    }

    [Fact]
    public void MalformedProof_ShouldNotVerifyOrThrow()
    {
        var core = SeedCore();
        var proof = new PassportSigner(SigningTesting.Keys).Sign(core, Created);
        var broken = new SignatureProof
        {
            Type = proof.Type,
            Cryptosuite = proof.Cryptosuite,
            CreatedUtc = proof.CreatedUtc,
            Issuer = proof.Issuer,
            VerificationMethod = proof.VerificationMethod,
            Hash = proof.Hash,
            ProofValue = "not base64url!",
            PublicKeyX = proof.PublicKeyX,
            PublicKeyY = proof.PublicKeyY,
        };

        Assert.False(PassportSigner.Verify(core, broken));
    }

    private static JsonObject SeedCore()
    {
        var seed = DppTestSeed.Load();
        return PassportStaticCore.Extract(PassportAssembler.Assemble(seed.Sources, seed.Header));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportSignerTests"`
Expected: the build fails because `PassportSigner` and `SignatureProof` aren't defined yet (CS0246 errors).

- [ ] **Step 3: Write `PassportSigner.cs`**

`web/Dpp/Signing/PassportSigner.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Services;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Signing;

/// <summary>
/// The signature of one passport version's static core, with the public key that made it, so a version stays
/// verifiable after the signing key changes (same proof shape as today's trust services).
/// </summary>
public sealed class SignatureProof
{
    public required string Type { get; init; }

    public required string Cryptosuite { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime CreatedUtc { get; init; }

    public required string Issuer { get; init; }

    public required string VerificationMethod { get; init; }

    /// <summary>"sha256:" and the hex SHA-256 of the canonical static core.</summary>
    public required string Hash { get; init; }

    /// <summary>The ECDSA P-256 signature (IEEE P1363), base64url.</summary>
    public required string ProofValue { get; init; }

    /// <summary>The signing key's public point, base64url.</summary>
    public required string PublicKeyX { get; init; }

    public required string PublicKeyY { get; init; }
}

/// <summary>Signs and verifies a version's static core (design 4.3) with the app's existing ECDSA key.</summary>
public sealed class PassportSigner(DemoSigningKeyService keys)
{
    public const string ProofType = "DataIntegrityProof";
    public const string Cryptosuite = "ecdsa-p256-sha256-dpp-canonical-2026";

    public SignatureProof Sign(JsonNode staticCore, DateTime createdUtc)
    {
        ArgumentNullException.ThrowIfNull(staticCore);
        var canonical = CanonicalJson.Serialize(staticCore);
        var signature = keys.SignData(Encoding.UTF8.GetBytes(canonical));
        var publicKey = keys.ExportPublicParameters();
        return new SignatureProof
        {
            Type = ProofType,
            Cryptosuite = Cryptosuite,
            CreatedUtc = createdUtc,
            Issuer = keys.Issuer,
            VerificationMethod = keys.VerificationMethod,
            Hash = CanonicalJson.Hash(staticCore),
            ProofValue = Base64Url.Encode(signature),
            PublicKeyX = Base64Url.Encode(publicKey.Q.X ?? throw new CryptographicException("The public key has no X coordinate.")),
            PublicKeyY = Base64Url.Encode(publicKey.Q.Y ?? throw new CryptographicException("The public key has no Y coordinate.")),
        };
    }

    /// <summary>
    /// True when the core is exactly what was signed. Uses the public key stored in the proof, not today's key.
    /// Whether that key belongs to the issuer is the trust pages' question, not this check's.
    /// </summary>
    public static bool Verify(JsonNode staticCore, SignatureProof proof)
    {
        ArgumentNullException.ThrowIfNull(staticCore);
        ArgumentNullException.ThrowIfNull(proof);
        try
        {
            if (!string.Equals(proof.Hash, CanonicalJson.Hash(staticCore), StringComparison.Ordinal))
            {
                return false;
            }

            using var key = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = Base64Url.Decode(proof.PublicKeyX), Y = Base64Url.Decode(proof.PublicKeyY) },
            });
            return key.VerifyData(
                Encoding.UTF8.GetBytes(CanonicalJson.Serialize(staticCore)),
                Base64Url.Decode(proof.ProofValue),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportSignerTests"`
Expected: `Passed!  - Failed:     0, Passed:     4`. The whole suite now has 748.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Signing/PassportSigner.cs BatteryPassWeb.Tests/Dpp/SigningTesting.cs BatteryPassWeb.Tests/Dpp/PassportSignerTests.cs
git commit -m "feat(dpp): sign and verify a version's static core"
```


---

### Task 4: Publication checks


**Files:**
- Create: `web/Dpp/Publication/PublicationChecks.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PublicationChecksTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PublicationChecksTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Publication;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class PublicationChecksTests
{
    private static readonly PublicationChecks Checks = new(DppStoreTesting.Schemas);

    [Fact]
    public void CompletePassport_ShouldPublishWithoutWarnings()
    {
        var report = Checks.Check(SeedPassport());

        Assert.True(report.CanPublish);
        Assert.Empty(report.Blocking);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void MissingAttributeDueByFeb2027_ShouldBlock()
    {
        var passport = SeedPassport();
        Section(passport, "identifiersAndProductData").Remove("thirdPartyServiceProvider");

        var report = Checks.Check(passport);

        Assert.False(report.CanPublish);
        Assert.Contains(report.Blocking, item => item.StartsWith("#13 ", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingAttributeRequiredOnlyLater_ShouldWarn()
    {
        var passport = SeedPassport();
        Section(passport, "performanceAndDurability").Remove("ratedCapacity");

        var report = Checks.Check(passport);

        Assert.True(report.CanPublish);
        Assert.Contains(report.Warnings, item => item.StartsWith("#57 ", StringComparison.Ordinal) && item.Contains("required later", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingConditionalAttribute_ShouldNeitherBlockNorWarn()
    {
        var passport = SeedPassport();
        Section(passport, "identifiersAndProductData").Remove("warrantyPeriod");

        var report = Checks.Check(passport);

        Assert.True(report.CanPublish);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void ValueWithAMissingPart_ShouldBlock()
    {
        var passport = SeedPassport();
        Section(passport, "symbolsLabelsAndDocumentationOfConformity")["euDeclarationOfConformity"] = new JsonObject { ["contentType"] = "application/pdf" };

        var report = Checks.Check(passport);

        Assert.False(report.CanPublish);
        Assert.Contains(report.Blocking, item => item.Contains("euDeclarationOfConformity", StringComparison.Ordinal) && item.Contains("url", StringComparison.Ordinal));
    }

    [Fact]
    public void ValueOutOfRange_ShouldBlock()
    {
        var passport = SeedPassport();
        Section(passport, "performanceAndDurability")["stateOfChargeSoC"] = JsonNode.Parse("""{"percentUnit":"%","percentageValue":120}""");

        Assert.False(Checks.Check(passport).CanPublish);
    }

    private static JsonObject SeedPassport()
    {
        var seed = DppTestSeed.Load();
        return PassportAssembler.Assemble(seed.Sources, seed.Header);
    }

    private static JsonObject Section(JsonObject passport, string section) => passport["batteryPassportData"]![section]!.AsObject();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PublicationChecksTests"`
Expected: the build fails because `BatteryPassWeb.Dpp.Publication` isn't defined yet (a CS0234 or CS0246 error).

- [ ] **Step 3: Write `PublicationChecks.cs`**

`web/Dpp/Publication/PublicationChecks.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Dpp.Publication;

/// <summary>What stops a passport from being published, and what is only worth knowing.</summary>
public sealed record PublicationReport(IReadOnlyList<string> Blocking, IReadOnlyList<string> Warnings)
{
    public bool CanPublish => Blocking.Count == 0;
}

/// <summary>
/// The checks before a version is published (design section 11). The catalogue decides what must be present; the
/// official schemas then check the shape of what is there.
/// </summary>
public sealed class PublicationChecks(OfficialSchemas schemas)
{
    public PublicationReport Check(JsonObject passport)
    {
        ArgumentNullException.ThrowIfNull(passport);
        var blocking = new List<string>();
        var warnings = new List<string>();

        foreach (var attribute in AttributeCatalog.All.Where(a => Find(passport, a) is null))
        {
            // Conditional attributes never block (J-9); attributes required only after Feb 2027 warn.
            if (attribute.Requirement == Requirement.Conditional)
            {
                continue;
            }

            if (attribute.MandatoryFromFeb2027)
            {
                blocking.Add($"#{attribute.Number} {attribute.Name} is missing");
            }
            else
            {
                warnings.Add($"#{attribute.Number} {attribute.Name} is missing (required later, not by Feb 2027)");
            }
        }

        foreach (var (variant, feb2027) in new[] { ("Feb-2027 schema", true), ("Full schema", false) })
        {
            foreach (var issue in schemas.CheckPassport(passport, feb2027))
            {
                // A missing attribute is the catalogue's call above, and summaries repeat errors reported on their own.
                // Anything else, including a required part missing inside a value (e.g. a document link without its
                // url), is a shape error that blocks.
                if (!IsMissingAttribute(issue) && !IsSummary(issue))
                {
                    blocking.Add($"{variant}: {issue.Location} {issue.Message}".Trim());
                }
            }
        }

        return new PublicationReport(blocking.Distinct().ToArray(), warnings);
    }

    /// <summary>"properties" and "items" errors only say that something inside failed; that error is reported itself.</summary>
    private static bool IsSummary(SchemaIssue issue) =>
        issue.Message.StartsWith("properties:", StringComparison.Ordinal) || issue.Message.StartsWith("items:", StringComparison.Ordinal);

    /// <summary>A "required" error on the passport, the header, the data root or a section is about a missing attribute.</summary>
    private static bool IsMissingAttribute(SchemaIssue issue) =>
        issue.Message.StartsWith("required", StringComparison.Ordinal)
        && issue.Location.Count(c => c == '/') <= 2
        && (issue.Location.Length == 0
            || issue.Location == "/" + AttributeDefinition.HeaderSection
            || issue.Location.StartsWith("/" + AttributeDefinition.DataRoot, StringComparison.Ordinal));

    private static JsonNode? Find(JsonObject passport, AttributeDefinition attribute) =>
        attribute.Section == AttributeDefinition.HeaderSection
            ? passport[AttributeDefinition.HeaderSection]?[attribute.Property]
            : passport[AttributeDefinition.DataRoot]?[attribute.Section]?[attribute.Property];
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PublicationChecksTests"`
Expected: `Passed!  - Failed:     0, Passed:     6`. The whole suite now has 754.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Publication/PublicationChecks.cs BatteryPassWeb.Tests/Dpp/PublicationChecksTests.cs
git commit -m "feat(dpp): check a passport before publication"
```


---

### Task 5: Versions in the store


**Files:**
- Create: `web/Dpp/Store/PassportVersion.cs`
- Modify: `web/Dpp/Store/DppStore.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PassportVersionStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PassportVersionStoreTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class PassportVersionStoreTests(MongoFixture mongo)
{
    [Fact]
    public async Task Version_ShouldRoundTripAndStillVerify()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var version = Version(seed, number: 1, status: DppStatuses.Active);

        await store.InsertAsync(version);
        var stored = await store.GetVersionByPassportIdAsync(version.DigitalProductPassportId);

        Assert.NotNull(stored);
        Assert.True(PassportSigner.Verify(stored.StaticCore, stored.Proof)); // decimals come back in a form that hashes the same
        Assert.Equal(version.Proof.Hash, stored.Proof.Hash);
        Assert.Equal(version.PublishedAtUtc, stored.PublishedAtUtc);
    }

    [Fact]
    public async Task ActiveVersion_ShouldBeTheNewestActiveOne()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        Assert.Equal(0, await store.GetLatestVersionNumberAsync(seed.Sources.Pack.Id));
        await store.InsertAsync(Version(seed, 1, DppStatuses.Archived));
        await store.InsertAsync(Version(seed, 2, DppStatuses.Active));

        var active = await store.GetActiveVersionAsync(seed.Sources.Pack.Id);

        Assert.Equal(2, active?.Number);
        Assert.Equal(2, await store.GetLatestVersionNumberAsync(seed.Sources.Pack.Id));
        Assert.Equal([1, 2], (await store.ListVersionsAsync(seed.Sources.Pack.Id)).Select(v => v.Number));
    }

    [Fact]
    public async Task PassportIdAndVersionNumber_ShouldBeUnique()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var first = Version(seed, 1, DppStatuses.Active);
        await store.InsertAsync(first);

        var samePassportId = Version(seed, 2, DppStatuses.Active, first.DigitalProductPassportId);
        var sameNumber = Version(seed, 1, DppStatuses.Active);

        Assert.Equal(ServerErrorCategory.DuplicateKey, (await Assert.ThrowsAsync<MongoWriteException>(() => store.InsertAsync(samePassportId))).WriteError.Category);
        Assert.Equal(ServerErrorCategory.DuplicateKey, (await Assert.ThrowsAsync<MongoWriteException>(() => store.InsertAsync(sameNumber))).WriteError.Category);
    }

    [Fact]
    public async Task ArchiveOthers_ShouldKeepOnlyTheGivenVersionActive()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var older = Version(seed, 1, DppStatuses.Active);
        var newer = Version(seed, 2, DppStatuses.Active);
        await store.InsertAsync(older);
        await store.InsertAsync(newer);

        await store.ArchiveOtherVersionsAsync(seed.Sources.Pack.Id, newer.Id);

        var versions = await store.ListVersionsAsync(seed.Sources.Pack.Id);
        Assert.Equal([DppStatuses.Archived, DppStatuses.Active], versions.Select(v => v.DppStatus));
    }

    [Fact]
    public async Task LastUpdated_ShouldBeSettable()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var version = Version(seed, 1, DppStatuses.Active);
        await store.InsertAsync(version);
        var later = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        await store.SetVersionLastUpdatedAsync(version.Id, later);

        Assert.Equal(later, (await store.GetActiveVersionAsync(seed.Sources.Pack.Id))?.LastUpdatedUtc);
    }

    [Fact]
    public async Task PacksUsingARecord_ShouldBeFoundByModelSiteBatchAndOperator()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var packId = seed.Sources.Pack.Id;

        Assert.Equal([packId], await store.FindPackIdsUsingAsync(modelId: seed.Sources.Model.Id));
        Assert.Equal([packId], await store.FindPackIdsUsingAsync(siteId: seed.Sources.Site.Id));
        Assert.Equal([packId], await store.FindPackIdsUsingAsync(batchId: seed.Sources.Batch.Id));
        Assert.Equal([packId], await store.FindPackIdsUsingAsync(operatorId: seed.Sources.Operator.Id));
        Assert.Empty(await store.FindPackIdsUsingAsync(modelId: "no-such-model"));
    }

    private static PassportVersion Version(SeedPassport seed, int number, string status, string? passportId = null)
    {
        var core = PassportStaticCore.Extract(PassportAssembler.Assemble(seed.Sources, seed.Header));
        return new PassportVersion
        {
            Id = $"version-{Guid.NewGuid():N}",
            PackId = seed.Sources.Pack.Id,
            Number = number,
            DigitalProductPassportId = passportId ?? $"urn:x-battery-pass:TEST-DPP-{Guid.NewGuid():N}",
            DppStatus = status,
            PublishedAtUtc = DppStoreTesting.Now.UtcDateTime,
            LastUpdatedUtc = DppStoreTesting.Now.UtcDateTime,
            PublishedBy = "team@example.com",
            StaticCore = core,
            Proof = new PassportSigner(SigningTesting.Keys).Sign(core, DppStoreTesting.Now.UtcDateTime),
        };
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportVersionStoreTests"`
Expected: the build fails because `PassportVersion` and the new store methods aren't defined yet (CS0246 and CS1061 errors).

- [ ] **Step 3: Write `PassportVersion.cs`**

`web/Dpp/Store/PassportVersion.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Values;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Store;

/// <summary>DPP status values (#2) that versioning sets; step 4 adds inactive, invalid and marked-for-deletion.</summary>
public static class DppStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";
}

/// <summary>
/// One published version of a pack's passport (design 4.2): its passport ID (#5), its DPP status (#2), when it was
/// published and last made available (#4), and the signed static core. Found by its #5 forever.
/// </summary>
public sealed class PassportVersion : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string PackId { get; init; }

    /// <summary>1 for the first publication, then +1 per version.</summary>
    public required int Number { get; init; }

    public required string DigitalProductPassportId { get; init; }

    public required string DppStatus { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime PublishedAtUtc { get; init; }

    /// <summary>#4: publication time, then the time of each live update while the version is active.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime LastUpdatedUtc { get; init; }

    public required string PublishedBy { get; init; }

    [BsonSerializer(typeof(JsonNodeBsonSerializer))]
    public required JsonNode StaticCore { get; init; }

    public required SignatureProof Proof { get; init; }
}
```

- [ ] **Step 4: Write `DppStore.cs`**

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

    private IMongoCollection<T> Collection<T>() => RequireDatabase().GetCollection<T>(CollectionName<T>());

    private IMongoDatabase RequireDatabase() =>
        _database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");

    private static string CollectionName<T>() =>
        CollectionNames.TryGetValue(typeof(T), out var name)
            ? name
            : throw new ArgumentException($"{typeof(T).Name} is not a passport record.");
}
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportVersionStoreTests"`
Expected: `Passed!  - Failed:     0, Passed:     6`. The whole suite now has 760.

- [ ] **Step 6: Commit**

```bash
git add web/Dpp/Store/PassportVersion.cs web/Dpp/Store/DppStore.cs BatteryPassWeb.Tests/Dpp/PassportVersionStoreTests.cs
git commit -m "feat(dpp): store passport versions"
```


---

### Task 6: Passport IDs (#5)


**Files:**
- Modify: `web/Dpp/Identifiers/TestIdentifierScheme.cs`
- Test: `BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs` (whole file after the change):

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class TestIdentifierSchemeTests
{
    private readonly TestIdentifierScheme _scheme = new();

    [Fact]
    public void NewPackIdentifier_ShouldBeAMarkedTestUrnThatPassesTheValueChecks()
    {
        var identifier = _scheme.NewPackIdentifier();

        Assert.StartsWith("urn:x-battery-pass:TEST-", identifier, StringComparison.Ordinal);
        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(6), JsonValue.Create(identifier)));
    }

    [Fact]
    public void NewPassportIdentifier_ShouldBeAMarkedTestUrnThatPassesTheValueChecks()
    {
        var identifier = _scheme.NewPassportIdentifier();

        Assert.StartsWith("urn:x-battery-pass:TEST-DPP-", identifier, StringComparison.Ordinal);
        Assert.True(_scheme.IsTestIdentifier(identifier));
        Assert.NotEqual(identifier, _scheme.NewPassportIdentifier());
        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(5), JsonValue.Create(identifier)));
    }

    [Fact]
    public void NewPackIdentifier_ShouldNeverRepeat()
    {
        var identifiers = Enumerable.Range(0, 200).Select(_ => _scheme.NewPackIdentifier()).ToList();

        Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("urn:x-battery-pass:TEST-0f8c", true)]
    [InlineData("urn:test:battery:VPC8C05-6788", false)]
    [InlineData("https://id.example.com/01/123", false)]
    public void IsTestIdentifier_ShouldRecogniseOnlyMarkedIdentifiers(string identifier, bool expected)
    {
        Assert.Equal(expected, _scheme.IsTestIdentifier(identifier));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.TestIdentifierSchemeTests"`
Expected: the build fails because `NewPassportIdentifier` isn't defined yet (a CS1061 error).

- [ ] **Step 3: Write `TestIdentifierScheme.cs`**

`web/Dpp/Identifiers/TestIdentifierScheme.cs` (whole file after the change):

```csharp
namespace BatteryPassWeb.Dpp.Identifiers;

/// <summary>Issues unique product identifiers (#6). The real EN 18219 scheme is chosen before go-live (decision 5, O-1).</summary>
public interface IIdentifierScheme
{
    string NewPackIdentifier();

    /// <summary>A new passport ID (#5) for one passport version.</summary>
    string NewPassportIdentifier();

    /// <summary>True for identifiers issued for testing; pages show a banner for these (design 10).</summary>
    bool IsTestIdentifier(string identifier);
}

/// <summary>
/// Test identifiers: a URN in a private namespace, marked TEST-, so they pass the absolute-URI check, can never be
/// mistaken for a registered identifier, and can be recognised later (plan judgement call).
/// </summary>
public sealed class TestIdentifierScheme : IIdentifierScheme
{
    public const string Prefix = "urn:x-battery-pass:TEST-";

    public string NewPackIdentifier() => $"{Prefix}{Guid.NewGuid():N}";

    public string NewPassportIdentifier() => $"{Prefix}DPP-{Guid.NewGuid():N}";

    public bool IsTestIdentifier(string identifier) =>
        identifier?.StartsWith(Prefix, StringComparison.Ordinal) == true;
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.TestIdentifierSchemeTests"`
Expected: `Passed!  - Failed:     0, Passed:     6`. The whole suite now has 761.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Identifiers/TestIdentifierScheme.cs BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs
git commit -m "feat(dpp): issue test passport IDs for versions"
```


---

### Task 7: The pipeline tells listeners what it saved

2a's pipeline tests keep passing unchanged: the test helper passes no listeners unless given some.

**Files:**
- Create: `web/Dpp/Editing/IPackChangeListener.cs`
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Test: `BatteryPassWeb.Tests/Dpp/SavePipelineTesting.cs`
- Test: `BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs`

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
        listeners);
}
```

`BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class SavePipelineListenerTests(MongoFixture mongo)
{
    private const string PackId = "pack-VPC8C05-6788";

    [Fact]
    public async Task SavedValues_ShouldReachTheListeners()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var listener = new RecordingListener();

        await SavePipelineTesting.Pipeline(store, context, listener).SaveAsync(
            ChangeSender.Team("team@example.com"),
            Change(("thirdPartyServiceProvider", "\"Heard AB\""), ("ratedCapacity", "200")));

        var call = Assert.Single(listener.Calls);
        Assert.Equal((TargetKind.Pack, PackId, "team@example.com"), (call.Target, call.TargetId, call.Who));
        Assert.Equal(["thirdPartyServiceProvider"], call.Keys); // only what was saved
    }

    [Fact]
    public async Task PreviewsAndRejectedChanges_ShouldNotReachTheListeners()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var listener = new RecordingListener();
        var pipeline = SavePipelineTesting.Pipeline(store, context, listener);

        await pipeline.SaveAsync(ChangeSender.Team("team@example.com"), Change(("thirdPartyServiceProvider", "\"Preview AB\"")), preview: true);
        await pipeline.SaveAsync(ChangeSender.Team("team@example.com"), Change(("ratedCapacity", "200")));

        Assert.Empty(listener.Calls);
    }

    private static ChangeSet Change(params (string Key, string Json)[] values) =>
        new(TargetKind.Pack, PackId, values.ToDictionary(v => v.Key, v => JsonNode.Parse(v.Json), StringComparer.Ordinal), "form");

    private sealed class RecordingListener : IPackChangeListener
    {
        public List<(TargetKind Target, string TargetId, string[] Keys, string Who)> Calls { get; } = [];

        public Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, string who, CancellationToken cancellationToken)
        {
            Calls.Add((target, targetId, keys.ToArray(), who));
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.SavePipelineListenerTests"`
Expected: the build fails because `IPackChangeListener` isn't defined yet (CS0246 errors).

- [ ] **Step 3: Write `IPackChangeListener.cs`**

`web/Dpp/Editing/IPackChangeListener.cs`:

```csharp
namespace BatteryPassWeb.Dpp.Editing;

/// <summary>Told after the save pipeline stored values on a record; publication uses it to keep versions current.</summary>
public interface IPackChangeListener
{
    Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, string who, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Write `SavePipeline.cs`**

`web/Dpp/Editing/SavePipeline.cs` (whole file after the change):

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
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
        return issues.Count > 0
            ? (KeyOutcome.Rejected, string.Join("; ", issues.Select(issue => $"{issue.Location} {issue.Message}".Trim())), null)
            : (KeyOutcome.Saved, string.Empty, value);
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

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.SavePipelineListenerTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite now has 763.

- [ ] **Step 6: Commit**

```bash
git add web/Dpp/Editing/IPackChangeListener.cs web/Dpp/Editing/SavePipeline.cs BatteryPassWeb.Tests/Dpp/SavePipelineTesting.cs BatteryPassWeb.Tests/Dpp/SavePipelineListenerTests.cs
git commit -m "feat(dpp): let the save pipeline notify listeners"
```


---

### Task 8: Publishing and keeping versions current


**Files:**
- Create: `web/Dpp/Publication/PublicationService.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PublicationTesting.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PublicationServiceTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/VersioningTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PublicationTesting.cs`:

```csharp
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>A clock a test can move forward.</summary>
internal sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>The seed pack in a fresh store, with publication wired to the save pipeline as in the app.</summary>
internal sealed record PublicationRig(
    DppStore Store,
    MongoContext Context,
    SeedPassport Seed,
    PublicationService Publication,
    SavePipeline Pipeline,
    MutableTimeProvider Clock)
{
    public string PackId => Seed.Sources.Pack.Id;
}

internal static class PublicationTesting
{
    public static readonly ChangeSender Team = ChangeSender.Team("team@example.com");

    public static async Task<PublicationRig> RigAsync(MongoFixture mongo)
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var clock = new MutableTimeProvider(DppStoreTesting.Now);
        var publication = new PublicationService(
            store,
            new PublicationChecks(DppStoreTesting.Schemas),
            new PassportSigner(SigningTesting.Keys),
            new TestIdentifierScheme(),
            new ApplicationAuditService(context),
            clock);
        var pipeline = SavePipelineTesting.Pipeline(store, context, clock, publication);
        return new PublicationRig(store, context, seed, publication, pipeline, clock);
    }

    /// <summary>The rig with the seed pack published once (version 1).</summary>
    public static async Task<PublicationRig> PublishedRigAsync(MongoFixture mongo)
    {
        var rig = await RigAsync(mongo);
        var result = await rig.Publication.PublishAsync(Team, rig.PackId);
        Assert.Null(result.Problem);
        return rig;
    }
}
```

`BatteryPassWeb.Tests/Dpp/PublicationServiceTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class PublicationServiceTests(MongoFixture mongo)
{
    [Fact]
    public async Task Team_ShouldPublishADraftAsSignedVersion1()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.Null(result.Problem);
        var version = result.Version!;
        Assert.Equal(1, version.Number);
        Assert.Equal(DppStatuses.Active, version.DppStatus);
        Assert.StartsWith(TestIdentifierScheme.Prefix + "DPP-", version.DigitalProductPassportId, StringComparison.Ordinal);
        Assert.Equal(DppStoreTesting.Now.UtcDateTime, version.PublishedAtUtc);
        Assert.Equal(DppStoreTesting.Now.UtcDateTime, version.LastUpdatedUtc);
        Assert.Equal("team@example.com", version.PublishedBy);
        var stored = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        Assert.True(PassportSigner.Verify(stored.StaticCore, stored.Proof));
        Assert.Equal(version.DigitalProductPassportId, version.StaticCore["dppHeader"]!["digitalProductPassportId"]!.GetValue<string>());
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Contains(events, e => e["eventType"].AsString == PublicationService.PublishedEvent && e["entityId"].AsString == rig.PackId);
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldPublish()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Publication.PublishAsync(ChangeSender.ClusterAdmin("admin@example.com", "cluster-test"), rig.PackId);

        Assert.Contains("only the team", result.Problem, StringComparison.Ordinal);
        Assert.Empty(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task IncompletePack_ShouldNotBePublished()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await RemoveStoredValueAsync(rig, "thirdPartyServiceProvider");

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.Null(result.Version);
        Assert.Contains(result.Report!.Blocking, item => item.StartsWith("#13 ", StringComparison.Ordinal));
        Assert.Empty(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task MissingPack_ShouldBeReported()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, "no-such-pack");

        Assert.Contains("not found", result.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishingAgainWithoutChanges_ShouldNotCreateAVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var again = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.True(again.Unchanged);
        Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task VersionNumber_ShouldFollowTheHighestStoredOne()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var core = PassportStaticCore.Extract(PassportAssembler.Assemble(rig.Seed.Sources, rig.Seed.Header));
        await rig.Store.InsertAsync(new PassportVersion
        {
            Id = "version-earlier",
            PackId = rig.PackId,
            Number = 2,
            DigitalProductPassportId = "urn:x-battery-pass:TEST-DPP-earlier",
            DppStatus = DppStatuses.Archived,
            PublishedAtUtc = DppStoreTesting.Now.UtcDateTime,
            LastUpdatedUtc = DppStoreTesting.Now.UtcDateTime,
            PublishedBy = "team@example.com",
            StaticCore = core,
            Proof = new PassportSigner(SigningTesting.Keys).Sign(core, DppStoreTesting.Now.UtcDateTime),
        });

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.Equal(3, result.Version?.Number);
    }

    internal static Task RemoveStoredValueAsync(PublicationRig rig, string property) =>
        rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Packs).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", rig.PackId),
            Builders<BsonDocument>.Update.Unset($"Values.{property}"));
}
```

`BatteryPassWeb.Tests/Dpp/VersioningTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Saves after publication keep the passport's versions current (design 4.2).</summary>
[Collection(MongoCollection.Name)]
public sealed class VersioningTests(MongoFixture mongo)
{
    [Fact]
    public async Task StaticChange_ShouldPublishANewVersionAndArchiveTheOld()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddHours(1);

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, PackChange(rig, ("thirdPartyServiceProvider", "\"New Hosting AB\"")));

        var versions = await rig.Store.ListVersionsAsync(rig.PackId);
        Assert.Equal([DppStatuses.Archived, DppStatuses.Active], versions.Select(v => v.DppStatus));
        var current = versions[1];
        Assert.Equal(2, current.Number);
        Assert.Equal("New Hosting AB", current.StaticCore["batteryPassportData"]!["identifiersAndProductData"]!["thirdPartyServiceProvider"]!.GetValue<string>());
        Assert.Equal(rig.Clock.Now.UtcDateTime, current.PublishedAtUtc);
        Assert.NotEqual(versions[0].DigitalProductPassportId, current.DigitalProductPassportId);
        Assert.True(PassportSigner.Verify(current.StaticCore, current.Proof));
    }

    [Fact]
    public async Task LiveChange_ShouldOnlyMoveTheLastUpdateToTheSaveTime()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddHours(2);
        var measured = rig.Clock.Now.UtcDateTime.AddMinutes(-30);
        var change = new ChangeSet(TargetKind.Pack, rig.PackId, Values(("stateOfChargeSoC", "55")), "feed", measured);

        await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), change);

        var version = Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId));
        Assert.Equal(rig.Clock.Now.UtcDateTime, version.LastUpdatedUtc); // #4: made available now
        Assert.Equal(measured, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].GeneratedAtUtc); // when it was measured
    }

    [Theory]
    [InlineData(TargetKind.Model, "model-test", "ratedCapacity", "210")]
    [InlineData(TargetKind.Batch, "batch-test-2026", "carbonFootprintPerformanceClass", "\"Class A\"")]
    [InlineData(TargetKind.Site, "site-test", "manufacturingPlace", "\"New plant, Stockholm, Sweden\"")]
    [InlineData(TargetKind.Operator, "operator-test", "economicOperatorId", "\"urn:test:operator:renamed\"")]
    public async Task SharedRecordChange_ShouldRepublishItsPublishedPacks(TargetKind target, string id, string key, string json)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(target, id, Values((key, json)), "form"));

        Assert.True(result.Saved);
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Fact]
    public async Task ChangeToADraft_ShouldNotPublishIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, PackChange(rig, ("thirdPartyServiceProvider", "\"Draft Hosting AB\"")));

        Assert.Empty(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task ClearingAValue_ShouldKeepTheActiveVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, PackChange(rig, ("thirdPartyServiceProvider", "\"\"")));

        Assert.Equal(KeyOutcome.KeptEmpty, Assert.Single(result.Keys).Outcome);
        Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task BlockedRepublish_ShouldKeepTheActiveVersionAndBeLogged()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await PublicationServiceTests.RemoveStoredValueAsync(rig, "thirdPartyServiceProvider"); // e.g. data damaged outside the app

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, PackChange(rig, ("warrantyPeriod", "\"P10Y\"")));

        Assert.True(result.Saved); // the save itself stands
        var version = Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId));
        Assert.Equal(DppStatuses.Active, version.DppStatus);
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var blocked = Assert.Single(events, e => e["eventType"].AsString == PublicationService.BlockedEvent);
        Assert.Contains("#13", blocked["metadata"]["blocking"][0].AsString, StringComparison.Ordinal);
    }

    private static ChangeSet PackChange(PublicationRig rig, params (string Key, string Json)[] values) =>
        new(TargetKind.Pack, rig.PackId, Values(values), "form");

    private static Dictionary<string, JsonNode?> Values(params (string Key, string Json)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonNode.Parse(v.Json), StringComparer.Ordinal);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PublicationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.VersioningTests"`
Expected: the build fails because `PublicationService` isn't defined yet (CS0246 errors).

- [ ] **Step 3: Write `PublicationService.cs`**

`web/Dpp/Publication/PublicationService.cs`:

```csharp
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Publication;

/// <summary>The new version, or why there is none; <see cref="Unchanged"/> when the content equals the active version's.</summary>
public sealed record PublicationResult(PassportVersion? Version, PublicationReport? Report, string? Problem, bool Unchanged = false);

/// <summary>
/// Publishes passport versions (design 4.2, 7.1, 7.2). The team publishes a draft; after that, accepted static changes
/// publish a new version by themselves and live changes only refresh #4. Nothing a change can do makes a published
/// pack unpublishable, because the save pipeline never deletes a value and checks each one; if a republish is ever
/// blocked anyway, the active version stays and the block is logged.
/// </summary>
public sealed class PublicationService(
    DppStore store,
    PublicationChecks checks,
    PassportSigner signer,
    IIdentifierScheme identifiers,
    ApplicationAuditService audit,
    TimeProvider clock) : IPackChangeListener
{
    public const string PublishedEvent = "dpp.published";
    public const string BlockedEvent = "dpp.publish-blocked";

    public Task<PublicationResult> PublishAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.Kind == SenderKind.Team
            ? PublishCoreAsync(packId, sender.Who, retryOnConflict: true, cancellationToken)
            : Task.FromResult(new PublicationResult(null, null, "only the team publishes passports"));
    }

    public async Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, string who, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var packIds = target switch
        {
            TargetKind.Pack => [targetId],
            TargetKind.Model => await store.FindPackIdsUsingAsync(modelId: targetId, cancellationToken: cancellationToken),
            TargetKind.Site => await store.FindPackIdsUsingAsync(siteId: targetId, cancellationToken: cancellationToken),
            TargetKind.Batch => await store.FindPackIdsUsingAsync(batchId: targetId, cancellationToken: cancellationToken),
            TargetKind.Operator => await store.FindPackIdsUsingAsync(operatorId: targetId, cancellationToken: cancellationToken),
            _ => [],
        };
        var staticChange = keys.Any(key => AttributeCatalog.FindByProperty(key)?.Change != ChangeRule.Live);

        foreach (var packId in packIds)
        {
            var active = await store.GetActiveVersionAsync(packId, cancellationToken);
            if (active is null)
            {
                continue; // a draft: publication is the team's step
            }

            if (!staticChange)
            {
                // #4 is when the update was made available, not when it was measured (file, #4).
                await store.SetVersionLastUpdatedAsync(active.Id, clock.GetUtcNow().UtcDateTime, cancellationToken);
                continue;
            }

            var result = await PublishCoreAsync(packId, who, retryOnConflict: true, cancellationToken);
            if (result.Problem is not null)
            {
                await audit.AppendApplicationAuditEventAsync(
                    BlockedEvent,
                    who,
                    "system",
                    "publication",
                    $"A new version of pack {packId} was blocked; version {active.Number} stays active",
                    new BsonDocument { ["blocking"] = new BsonArray(result.Report?.Blocking ?? [result.Problem]) },
                    entityType: "dpp-pack",
                    entityId: packId,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<PublicationResult> PublishCoreAsync(string packId, string who, bool retryOnConflict, CancellationToken cancellationToken)
    {
        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        if (sources is null)
        {
            return new PublicationResult(null, null, $"pack {packId}, or a record it links to, was not found");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var passportId = identifiers.NewPassportIdentifier();
        var passport = PassportAssembler.Assemble(sources, new PassportHeader(passportId, DppStatuses.Active, now));
        var report = checks.Check(passport);
        if (!report.CanPublish)
        {
            return new PublicationResult(null, report, "the passport can't be published yet; see the blocking items");
        }

        var core = PassportStaticCore.Extract(passport);
        var active = await store.GetActiveVersionAsync(packId, cancellationToken);
        if (active is not null && PassportStaticCore.ContentHash(active.StaticCore) == PassportStaticCore.ContentHash(core))
        {
            return new PublicationResult(active, report, null, Unchanged: true);
        }

        var version = new PassportVersion
        {
            Id = $"version-{Guid.NewGuid():N}",
            PackId = packId,
            Number = await store.GetLatestVersionNumberAsync(packId, cancellationToken) + 1,
            DigitalProductPassportId = passportId,
            DppStatus = DppStatuses.Active,
            PublishedAtUtc = now,
            LastUpdatedUtc = now,
            PublishedBy = who,
            StaticCore = core,
            Proof = signer.Sign(core, now),
        };
        try
        {
            await store.InsertAsync(version, cancellationToken);
        }
        catch (MongoWriteException exception) when (retryOnConflict && exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another save published this pack at the same moment: start again from the new state, once.
            return await PublishCoreAsync(packId, who, retryOnConflict: false, cancellationToken);
        }

        await store.ArchiveOtherVersionsAsync(packId, version.Id, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            PublishedEvent,
            who,
            "team",
            "publication",
            $"Version {version.Number} of pack {packId} published",
            new BsonDocument
            {
                ["passportId"] = passportId,
                ["number"] = version.Number,
                ["warnings"] = new BsonArray(report.Warnings),
            },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return new PublicationResult(version, report, null);
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PublicationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.VersioningTests"`
Expected: `Passed!  - Failed:     0, Passed:    15`. The whole suite now has 778.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Publication/PublicationService.cs BatteryPassWeb.Tests/Dpp/PublicationTesting.cs BatteryPassWeb.Tests/Dpp/PublicationServiceTests.cs BatteryPassWeb.Tests/Dpp/VersioningTests.cs
git commit -m "feat(dpp): publish signed versions and keep them current after changes"
```


---

### Task 9: Reading published passports


**Files:**
- Create: `web/Dpp/Publication/PassportReader.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PassportReaderTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PassportReaderTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class PassportReaderTests(MongoFixture mongo)
{
    [Fact]
    public async Task Current_ShouldBeTheSignedCoreWithTheHeaderAndTheLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var current = await new PassportReader(rig.Store).CurrentAsync(rig.PackId);

        Assert.NotNull(current);
        var passport = current.Passport;
        Assert.Equal("active", passport["dppHeader"]!["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Equal("2026-10-29T08:00:00Z", passport["dppHeader"]!["lastUpdated"]!.GetValue<string>());
        Assert.Equal(76, passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]!["percentageValue"]!.GetValue<int>());
        Assert.Empty(DppStoreTesting.Schemas.CheckPassport(passport, feb2027Variant: false));
    }

    [Fact]
    public async Task Current_ShouldShowTheLatestLiveValue()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddMinutes(10);
        await rig.Pipeline.SaveAsync(
            ChangeSender.Feed("bms-feed", "cluster-test"),
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 55 }, "feed"));

        var passport = (await new PassportReader(rig.Store).CurrentAsync(rig.PackId))!.Passport;

        Assert.Equal(55, passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]!["percentageValue"]!.GetValue<int>());
        Assert.Equal("2026-10-29T08:10:00Z", passport["dppHeader"]!["lastUpdated"]!.GetValue<string>());
    }

    [Fact]
    public async Task ArchivedVersion_ShouldStayReadableByItsPassportIdWithoutLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var first = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["thirdPartyServiceProvider"] = "New Hosting AB" }, "form"));

        var archived = await new PassportReader(rig.Store).VersionAsync(first.DigitalProductPassportId);

        Assert.NotNull(archived);
        Assert.Equal("archived", archived.Passport["dppHeader"]!["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Equal("Example Passport Hosting AB", archived.Passport["batteryPassportData"]!["identifiersAndProductData"]!["thirdPartyServiceProvider"]!.GetValue<string>());
        Assert.Null(archived.Passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]);
        Assert.True(PassportReader.Verify(archived.Version));
    }

    [Fact]
    public async Task ChangedStoredCore_ShouldFailVerification()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", version.Id),
            Builders<BsonDocument>.Update.Set("StaticCore.batteryPassportData.identifiersAndProductData.batterySerialNumber", "TAMPERED"));

        var reloaded = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;

        Assert.False(PassportReader.Verify(reloaded));
    }

    [Fact]
    public async Task UnknownPackOrPassportId_ShouldReadAsNothing()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var reader = new PassportReader(rig.Store);

        Assert.Null(await reader.CurrentAsync(rig.PackId)); // a draft has no published passport
        Assert.Null(await reader.VersionAsync("urn:x-battery-pass:TEST-DPP-none"));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportReaderTests"`
Expected: the build fails because `PassportReader` isn't defined yet (CS0246 errors).

- [ ] **Step 3: Write `PassportReader.cs`**

`web/Dpp/Publication/PassportReader.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Publication;

/// <summary>A published passport as readers see it, with the version it comes from.</summary>
public sealed record PublishedPassport(PassportVersion Version, JsonObject Passport);

/// <summary>
/// Reads published passports (design 4.2): the active one per pack, or any version by its #5. Visibility filtering is
/// step 3's job; this returns the whole passport.
/// </summary>
public sealed class PassportReader(DppStore store)
{
    /// <summary>The pack's current passport: the active version's signed core, its #2 and #4, and the live values.</summary>
    public async Task<PublishedPassport?> CurrentAsync(string packId, CancellationToken cancellationToken = default)
    {
        var version = await store.GetActiveVersionAsync(packId, cancellationToken);
        return version is null ? null : await ComposeAsync(version, cancellationToken);
    }

    /// <summary>
    /// The version with this passport ID (#5). An archived version comes without live values (they aren't versioned),
    /// so it lacks the required live set; that is expected and must not be "fixed" with today's live values.
    /// </summary>
    public async Task<PublishedPassport?> VersionAsync(string passportId, CancellationToken cancellationToken = default)
    {
        var version = await store.GetVersionByPassportIdAsync(passportId, cancellationToken);
        return version is null ? null : await ComposeAsync(version, cancellationToken);
    }

    /// <summary>True when the stored core is exactly what was signed at publication.</summary>
    public static bool Verify(PassportVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return PassportSigner.Verify(version.StaticCore, version.Proof);
    }

    private async Task<PublishedPassport> ComposeAsync(PassportVersion version, CancellationToken cancellationToken)
    {
        var liveValues = version.DppStatus == DppStatuses.Active
            ? (await store.GetAsync<BatteryPack>(version.PackId, cancellationToken))?.Values
            : null;
        return new PublishedPassport(version, PassportStaticCore.Compose(version.StaticCore, version.DppStatus, version.LastUpdatedUtc, liveValues));
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportReaderTests"`
Expected: `Passed!  - Failed:     0, Passed:     5`. The whole suite now has 783.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Publication/PassportReader.cs BatteryPassWeb.Tests/Dpp/PassportReaderTests.cs
git commit -m "feat(dpp): read current and archived passports and verify them"
```


---

### Task 10: Register the publication services

`DemoSigningKeyService` is already registered in `web/Program.cs`; `AddDpp()` now needs it.

**Files:**
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Test: `BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs` (whole file after the change):

```csharp
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp;
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
            .AddSingleton(Options.Create(new BatteryPassOptions()))
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
        Assert.Same(provider.GetRequiredService<PublicationService>(), Assert.Single(provider.GetServices<IPackChangeListener>()));
    }

    [Fact]
    public void Program_ShouldRegisterTheServicesAndCreateTheIndexesAtStartup()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("builder.Services.AddDpp();", program, StringComparison.Ordinal);
        Assert.Contains("await scope.ServiceProvider.GetRequiredService<DppStore>().EnsureIndexesAsync();", program, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: `AddDpp_ShouldResolveEveryServiceWithoutADatabase` fails: `PassportReader` isn't registered.

- [ ] **Step 3: Write `DppServiceCollectionExtensions.cs`**

`web/Dpp/DppServiceCollectionExtensions.cs` (whole file after the change):

```csharp
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
        return services;
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`. The whole suite now has 783.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/DppServiceCollectionExtensions.cs BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
git commit -m "feat(dpp): register the publication services"
```


---

### Task 11: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:   783, Skipped:     0, Total:   783` (730 + 53 new).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

## After this plan

Plan 2c (documents) adds the document record, public links and uploaded files behind access-checked links; the stored `RelatedResource` values then point at those links. Plan 2d builds the team's pages on `SavePipeline`, `PackCreationService`, `PublicationService` and `PassportReader`, including the confirmation before a model, batch or site change republishes its packs and the banner for test IDs. Step 4's status change, invalid and marked-for-deletion add DPP statuses beyond "active" and "archived" and need their own way past the pipeline's rule for #22 and the re-stated values (noted in plan 2a).

Two notes for later plans:

- `PublicationService` records published versions with the actor role "team". That holds in 2b, where only the team changes static data; once step 4 lets cluster admins trigger republishes, take the role from the sender.
- If a listener fails after the pipeline has stored the values (an infrastructure error during republishing), the exception reaches the caller although the values are saved. That is deliberate, so the failure is visible; 2d's form handling should say "saved, but publishing failed" rather than "not saved".

