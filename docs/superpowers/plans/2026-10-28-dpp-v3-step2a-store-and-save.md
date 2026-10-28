# DPP V3 Step 2a: Store and Save Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Save and read the passport records in MongoDB through one save pipeline that checks every value, and create draft packs with test identifiers — backend only.

**Architecture:** Builds on step 1's `web/Dpp/` code. `DppStore` reads and writes the records; `SavePipeline` is the one way values get in (catalogue attribute of this record kind → sender may change it → not empty → bare value turned into the schema form → valid), logging each save through the existing `ApplicationAuditService`. `PackCreationService` issues #6 from a test identifier scheme. Every pack stays a draft: publication and versions are plan 2b.

**Tech Stack:** .NET 10, MongoDB.Driver 3.9, JsonSchema.Net 9.5.0 (from step 1), xUnit 2.9, EphemeralMongo 3.2.0 (a throwaway MongoDB 8 inside the test run, decision 22).

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 4.1, 6, 7.1, 8.2, 10, 14 and the 2a–2d split) and the decisions record (1–22). Step 1: `docs/superpowers/plans/2026-10-28-dpp-v3-step1-data-foundation.md`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** step 1 is on `feature/dpp-v3-step1-data-foundation` (local only). Start 2a on top of it: `git switch feature/dpp-v3-step1-data-foundation && git switch -c feature/dpp-v3-step2a-store-and-save`.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:   637`. After this plan: 730.
- **First database test run:** EphemeralMongo downloads a MongoDB 8 server (about 100 MB, about a minute) into `%LOCALAPPDATA%\ephemeral-mongo` the first time a database test runs (Task 2). Later runs start it in about a second; the whole suite takes about 7 seconds instead of 1. The test server is MongoDB 8; the hosted database is MongoDB Atlas, so if its cluster runs version 6 or 7, change `MongoVersion.V8` in `MongoFixture.cs` to match.
- **Old app:** the pre-rebuild app is tagged `app-v1.2-before-dpp-v3` (commit `8c8bfe7`). Don't delete that tag or the branch `security/demo-api-hardening`.

## Judgement calls in this plan

1. **Step 2 is split into 2a–2d** (design section 14): 2a store and save pipeline, 2b publication and versions, 2c documents, 2d the team's editing pages after a layout mock-up.
2. **A step-1 bug is fixed first (Task 1).** Stored decimals came back from MongoDB with 17 digits (75.4 as `75.400000000000006`), because the serializer went through the driver's JSON text. Step 1's tests used numbers that happen to be exact in binary. The fix converts BSON to JSON directly.
3. **Indexes are created in `Program.cs`'s startup block**, not in `ExternalApiInitializer`, which belongs to the old app and goes in step 3. #6 is unique; serial numbers (#11) are indexed for lookup only, since they aren't unique (design 8.2); a batch is unique per model, site and year (decision 3).
4. **Each saved key is set on its own** (MongoDB `$set`), so a live feed and a form saving at the same time don't overwrite each other's values.
5. **Changes are logged through the existing `ApplicationAuditService`** as `dpp.change` events with the result per key. The upload log of design 4.1 belongs to step 5.
6. **The pipeline rejects, with a reason:** a key stored on another record kind (e.g. `ratedCapacity` on a pack), #22 and the five re-stated values on a pack (they change only through the status change, step 4), and anything the app sets itself (#1–#6). **Empty** means missing, null, blank text, `[]` or `{}`; empty values never overwrite, and no attribute accepts an empty list or object.
7. **Bare values become the schema's form** for the 29 attributes that are one value with a fixed unit (percentages, °C, Ah, kg, kgCO2-eq/kWh, A/Ah, the three voltages, #70, #79's pack value, #20 and #22). Ah and kg come from the catalogue's unit.
8. **Senders are a small abstraction** (`ChangeSender`: team, cluster admin or feed, plus cluster ids), not signed-in users. Plan 2d maps signed-in users and step 5 maps API tokens to it.
9. **Test identifiers** are `urn:x-battery-pass:TEST-<guid>`: absolute URIs that pass the value checks, carry the `TEST-` mark (design 10) and can be recognised for the banner in 2d.
10. **Pack creation is all-or-nothing** and needs only #11; completeness is checked at publication (2b). The app sets #6 and #22 = `original`.
11. **`TimeProvider.System` is registered**, so tests can fix the clock.

## File structure

| File | Responsibility |
|---|---|
| `web/Dpp/Values/JsonNodeBsonSerializer.cs` (modify) | Reads stored BSON back as JSON without going through text |
| `web/Dpp/Store/DppRecords.cs` (modify) | Adds the `IDppRecord` marker (an `Id`) to the six records |
| `web/Dpp/Store/DppStore.cs` | Collections, indexes, get/insert/set-values, lookups by #6 and #11, loading a pack's sources |
| `web/Dpp/Values/AttributeValueNormalizer.cs` | Bare value → the schema's form |
| `web/Dpp/Editing/ChangeSender.cs` | Who sends a change, and `EditRights.MayEdit` |
| `web/Dpp/Editing/SavePipeline.cs` | The save pipeline: judge each key, save the accepted ones, log the change |
| `web/Dpp/Identifiers/TestIdentifierScheme.cs` | Issues #6 test identifiers |
| `web/Dpp/Editing/PackCreationService.cs` | Creates draft packs |
| `web/Dpp/DppServiceCollectionExtensions.cs` | `AddDpp()` registers the services |
| `web/Program.cs` (modify) | Calls `AddDpp()` and creates the indexes at startup |
| `BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` (modify) | Adds EphemeralMongo |
| `BatteryPassWeb.Tests/Dpp/MongoFixture.cs` | The throwaway MongoDB, a fixed clock, and the seeded test store |
| `BatteryPassWeb.Tests/Dpp/SavePipelineTesting.cs` | The save pipeline around a test store |
| `BatteryPassWeb.Tests/Dpp/*Tests.cs` | Tests for each piece |


---

### Task 1: Fix stored decimals

**Files:**
- Modify: `web/Dpp/Values/JsonNodeBsonSerializer.cs`
- Test: `BatteryPassWeb.Tests/Dpp/AttributeValueStorageTests.cs` (four new cases)

- [ ] **Step 1: Add the failing cases**

`BatteryPassWeb.Tests/Dpp/AttributeValueStorageTests.cs` (whole file; the new cases are `75.4`, the kgCO2 object, the array and `true`):

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Checks that stored values survive the MongoDB mapping unchanged. No database is needed.</summary>
public sealed class AttributeValueStorageTests
{
    private static readonly DateTime Generated = new(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("""{"percentUnit":"%","percentageValue":76}""")]
    [InlineData("""{"degreeCelsiusUnit":"°C","celsiusValue":-30}""")]
    [InlineData("""{"gramKilogramUnit":"kg","gramKilogramValue":512.5}""")]
    [InlineData("""["Cobalt","Lithium"]""")]
    [InlineData("\"2026-09\"")]
    [InlineData("412")]
    [InlineData("12.5")]
    [InlineData("75.4")] // not exact in binary: must not come back as 75.400000000000006
    [InlineData("""{"kgCO2EquivalentPerKilowattHourValue":4.8,"kgCO2EquivalentPerKilowattHourUnit":"kgCO2-eq/kWh"}""")]
    [InlineData("""[{"concentration":1.2,"hazardPhrases":["H301"]}]""")]
    [InlineData("true")]
    public void Value_ShouldRoundTripThroughBson(string json)
    {
        var original = new AttributeValue { Value = JsonNode.Parse(json)!, GeneratedAtUtc = Generated, Source = "form", Who = "team@example.com" };

        var restored = BsonSerializer.Deserialize<AttributeValue>(original.ToBsonDocument());

        Assert.True(JsonNode.DeepEquals(original.Value, restored.Value), $"{original.Value.ToJsonString()} became {restored.Value.ToJsonString()}");
        Assert.Equal(Generated, restored.GeneratedAtUtc);
        Assert.Equal(DateTimeKind.Utc, restored.GeneratedAtUtc.Kind);
        Assert.Equal("form", restored.Source);
        Assert.Equal("team@example.com", restored.Who);
    }

    [Fact]
    public void Value_ShouldBeStoredAsNativeBson()
    {
        var value = new AttributeValue { Value = JsonNode.Parse("""{"percentUnit":"%","percentageValue":76}""")!, GeneratedAtUtc = Generated, Source = "feed" };

        var stored = value.ToBsonDocument()["Value"].AsBsonDocument;

        Assert.Equal(76, stored["percentageValue"].AsInt32);
    }

    [Fact]
    public void Pack_ShouldRoundTripWithItsValues()
    {
        var pack = new BatteryPack
        {
            Id = "pack-1",
            BatchId = "batch-1",
            OperatorId = "operator-1",
            ClusterId = "cluster-1",
            Values =
            {
                ["batterySerialNumber"] = new AttributeValue { Value = JsonValue.Create("VPC8C05-6788"), GeneratedAtUtc = Generated, Source = "form" },
                ["stateOfChargeSoC"] = new AttributeValue { Value = JsonNode.Parse("""{"percentUnit":"%","percentageValue":76}""")!, GeneratedAtUtc = Generated, Source = "feed" },
            },
        };

        var document = pack.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<BatteryPack>(document);

        Assert.Equal("pack-1", document["_id"].AsString);
        Assert.Equal("cluster-1", restored.ClusterId);
        Assert.Equal("VPC8C05-6788", restored.Values["batterySerialNumber"].Value.GetValue<string>());
        Assert.Equal(76, restored.Values["stateOfChargeSoC"].Value["percentageValue"]!.GetValue<int>());
    }
}
```

- [ ] **Step 2: Run them to see the bug**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueStorageTests"`
Expected: 2 failures, `json: "75.4"` and the kgCO2 object; the message shows `75.400000000000006` / `4.7999999999999998`.

- [ ] **Step 3: Read BSON back as JSON directly**

`web/Dpp/Values/JsonNodeBsonSerializer.cs` (whole file):

```csharp
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// Stores a JSON value as native BSON (objects as documents, arrays as arrays), so stored values stay readable
/// and queryable in MongoDB, and reads it back as the same JSON.
/// </summary>
public sealed class JsonNodeBsonSerializer : SerializerBase<JsonNode?>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, JsonNode? value)
    {
        if (value is null)
        {
            context.Writer.WriteNull();
            return;
        }

        var wrapper = BsonDocument.Parse($"{{\"v\":{value.ToJsonString()}}}");
        BsonValueSerializer.Instance.Serialize(context, wrapper["v"]);
    }

    public override JsonNode? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) =>
        ToJson(BsonValueSerializer.Instance.Deserialize(context));

    /// <summary>
    /// Converts BSON back to JSON directly. Going through the driver's JSON text would write doubles with 17 digits
    /// (75.4 becomes 75.400000000000006); System.Text.Json writes the shortest form.
    /// </summary>
    private static JsonNode? ToJson(BsonValue value) => value.BsonType switch
    {
        BsonType.Null => null,
        BsonType.Document => new JsonObject(value.AsBsonDocument.Select(element => KeyValuePair.Create(element.Name, ToJson(element.Value)))),
        BsonType.Array => new JsonArray(value.AsBsonArray.Select(ToJson).ToArray()),
        BsonType.String => JsonValue.Create(value.AsString),
        BsonType.Boolean => JsonValue.Create(value.AsBoolean),
        BsonType.Int32 => JsonValue.Create(value.AsInt32),
        BsonType.Int64 => JsonValue.Create(value.AsInt64),
        BsonType.Double => JsonValue.Create(value.AsDouble),
        BsonType.Decimal128 => JsonValue.Create(Decimal128.ToDecimal(value.AsDecimal128)),
        _ => throw new FormatException($"Unexpected BSON type {value.BsonType} in a stored value."),
    };
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueStorageTests"`
Expected: `Passed!  - Failed:     0, Passed:    13`. The whole suite now has 641.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Values/JsonNodeBsonSerializer.cs BatteryPassWeb.Tests/Dpp/AttributeValueStorageTests.cs
git commit -m "fix(dpp): keep stored decimals short when reading them back from MongoDB"
```


---

### Task 2: The store, tested against a throwaway MongoDB

**Files:**
- Modify: `BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`, `web/Dpp/Store/DppRecords.cs`
- Create: `web/Dpp/Store/DppStore.cs`
- Test: `BatteryPassWeb.Tests/Dpp/MongoFixture.cs`, `BatteryPassWeb.Tests/Dpp/DppStoreTests.cs`

- [ ] **Step 1: Add EphemeralMongo to the test project**

`BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` (whole file; one new line):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="EphemeralMongo" Version="3.2.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\web\BatteryPassWeb.csproj" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the fixture and the failing store tests**

`BatteryPassWeb.Tests/Dpp/MongoFixture.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using EphemeralMongo;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// One throwaway MongoDB for all database tests (decision 22). The first run downloads mongod (about 100 MB, cached
/// in %LOCALAPPDATA%\ephemeral-mongo); later runs start it in about a second.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private IMongoRunner? _runner;

    public async Task InitializeAsync() =>
        _runner = await MongoRunner.RunAsync(new MongoRunnerOptions { Version = MongoVersion.V8 });

    public Task DisposeAsync()
    {
        _runner?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>A context on a new, empty database, so tests never see each other's data.</summary>
    public MongoContext NewContext() => new(Options.Create(new BatteryPassOptions
    {
        MongoDbUri = _runner!.ConnectionString,
        MongoDbName = $"dpp_test_{Guid.NewGuid():N}",
    }));
}

[CollectionDefinition(Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "DPP MongoDB";
}

/// <summary>A clock that always shows the same time.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Shared set-up for the database tests: the seed pack in a fresh store.</summary>
internal static class DppStoreTesting
{
    public static readonly OfficialSchemas Schemas = OfficialSchemas.Load();
    public static readonly DateTimeOffset Now = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);

    public static async Task<(DppStore Store, MongoContext Context, SeedPassport Seed)> SeededStoreAsync(MongoFixture mongo)
    {
        var context = mongo.NewContext();
        var store = new DppStore(context);
        await store.EnsureIndexesAsync();
        var seed = DppTestSeed.Load();
        await store.InsertAsync(new BatteryFamily { Id = seed.Sources.Model.FamilyId, Name = "Test battery family" });
        await store.InsertAsync(seed.Sources.Model);
        await store.InsertAsync(seed.Sources.Site);
        await store.InsertAsync(seed.Sources.Batch);
        await store.InsertAsync(seed.Sources.Operator);
        await store.InsertAsync(seed.Sources.Pack);
        return (store, context, seed);
    }

    public static AttributeValue Value(JsonNode value) =>
        new() { Value = value, GeneratedAtUtc = Now.UtcDateTime, Source = "seed" };
}
```

`BatteryPassWeb.Tests/Dpp/DppStoreTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class DppStoreTests(MongoFixture mongo)
{
    [Fact]
    public async Task StoredSeed_ShouldLoadBackIntoTheSamePassport()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var sources = await store.LoadSourcesAsync(seed.Sources.Pack.Id);

        Assert.NotNull(sources);
        var passport = PassportAssembler.Assemble(sources, seed.Header);
        Assert.True(JsonNode.DeepEquals(PassportAssembler.Assemble(seed.Sources, seed.Header), passport));
        Assert.Empty(DppStoreTesting.Schemas.CheckPassport(passport, feb2027Variant: false));
    }

    [Fact]
    public async Task EnsureIndexes_ShouldBeSafeToRunAgain()
    {
        var (store, _, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        await store.EnsureIndexesAsync();
        await store.EnsureIndexesAsync();
    }

    [Fact]
    public async Task UniqueProductIdentifier_ShouldBeUnique()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var copy = CopyOfSeedPack(seed, "pack-copy");

        var error = await Assert.ThrowsAsync<MongoWriteException>(() => store.InsertAsync(copy));

        Assert.Equal(ServerErrorCategory.DuplicateKey, error.WriteError.Category);
    }

    [Fact]
    public async Task Batch_ShouldBeUniquePerModelSiteAndYear()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var batch = seed.Sources.Batch;
        var same = new ProductionBatch { Id = "batch-again", ModelId = batch.ModelId, SiteId = batch.SiteId, Year = batch.Year };

        var error = await Assert.ThrowsAsync<MongoWriteException>(() => store.InsertAsync(same));

        Assert.Equal(ServerErrorCategory.DuplicateKey, error.WriteError.Category);
        await store.InsertAsync(new ProductionBatch { Id = "batch-next-year", ModelId = batch.ModelId, SiteId = batch.SiteId, Year = batch.Year + 1 });
    }

    [Fact]
    public async Task SerialNumber_ShouldFindEveryPackWithIt()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var second = CopyOfSeedPack(seed, "pack-second");
        second.Values["uniqueProductIdentifier"] = DppStoreTesting.Value(JsonValue.Create("urn:x-battery-pass:TEST-second")!);
        await store.InsertAsync(second);

        var packs = await store.FindPacksBySerialNumberAsync("VPC8C05-6788");

        Assert.Equal(["pack-VPC8C05-6788", "pack-second"], packs.Select(p => p.Id).Order(StringComparer.Ordinal));
        Assert.Empty(await store.FindPacksBySerialNumberAsync("no-such-serial"));
    }

    [Fact]
    public async Task UniqueProductIdentifier_ShouldFindThePack()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var pack = await store.FindPackByUniqueProductIdentifierAsync("urn:test:battery:VPC8C05-6788");

        Assert.Equal(seed.Sources.Pack.Id, pack?.Id);
        Assert.Null(await store.FindPackByUniqueProductIdentifierAsync("urn:test:battery:none"));
    }

    [Fact]
    public async Task SetValues_ShouldChangeOnlyTheGivenValues()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var packId = seed.Sources.Pack.Id;

        var changed = await store.SetValuesAsync<BatteryPack>(packId, new Dictionary<string, AttributeValue>
        {
            ["stateOfChargeSoC"] = DppStoreTesting.Value(JsonNode.Parse("""{"percentUnit":"%","percentageValue":55}""")!),
        });

        Assert.True(changed);
        var pack = await store.GetAsync<BatteryPack>(packId);
        Assert.Equal(55, pack!.Values["stateOfChargeSoC"].Value["percentageValue"]!.GetValue<int>());
        Assert.Equal("VPC8C05-6788", pack.Values["batterySerialNumber"].Value.GetValue<string>());
        Assert.Equal(seed.Sources.Pack.Values.Count, pack.Values.Count);
        Assert.False(await store.SetValuesAsync<BatteryPack>("no-such-pack", new Dictionary<string, AttributeValue>
        {
            ["stateOfChargeSoC"] = DppStoreTesting.Value(JsonNode.Parse("""{"percentUnit":"%","percentageValue":1}""")!),
        }));
    }

    [Fact]
    public async Task MissingRecords_ShouldReadAsNothing()
    {
        var (store, _, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var orphan = CopyOfSeedPack(seed, "pack-orphan");
        orphan.Values.Remove("uniqueProductIdentifier");
        var orphanWithMissingBatch = new BatteryPack { Id = orphan.Id, BatchId = "no-such-batch", OperatorId = orphan.OperatorId, Values = orphan.Values };
        await store.InsertAsync(orphanWithMissingBatch);

        Assert.Null(await store.GetAsync<BatteryModel>("no-such-model"));
        Assert.Null(await store.LoadSourcesAsync("no-such-pack"));
        Assert.Null(await store.LoadSourcesAsync("pack-orphan"));
    }

    [Fact]
    public async Task WithoutADatabase_ReadsShouldReturnNothingAndWritesShouldThrow()
    {
        var store = new DppStore(new MongoContext(Options.Create(new BatteryPassOptions())));

        Assert.False(store.IsAvailable);
        await store.EnsureIndexesAsync();
        Assert.Null(await store.GetAsync<BatteryPack>("pack-1"));
        Assert.Empty(await store.FindPacksBySerialNumberAsync("VPC8C05-6788"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InsertAsync(new BatteryFamily { Id = "f", Name = "F" }));
    }

    private static BatteryPack CopyOfSeedPack(SeedPassport seed, string id) => new()
    {
        Id = id,
        BatchId = seed.Sources.Pack.BatchId,
        OperatorId = seed.Sources.Pack.OperatorId,
        ClusterId = seed.Sources.Pack.ClusterId,
        Values = new Dictionary<string, AttributeValue>(seed.Sources.Pack.Values, StringComparer.Ordinal),
    };
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppStoreTests"`
Expected: the build fails because `DppStore` isn't defined yet (CS0246 or CS0234 errors).

- [ ] **Step 4: Mark the records with their id**

`web/Dpp/Store/DppRecords.cs` (whole file; adds `IDppRecord` and implements it on the six records):

```csharp
using BatteryPassWeb.Dpp.Values;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Store;

// The records of design section 4.1 that a passport is built from. Values are keyed by v2.0.1 property name
// and kept in the schema's own form. Passport versions and documents come in plan 2b and 2c.

/// <summary>A stored record; <see cref="Id"/> is its MongoDB _id.</summary>
public interface IDppRecord
{
    string Id { get; }
}

/// <summary>Groups battery models. Holds no passport data.</summary>
public sealed class BatteryFamily : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string Name { get; set; }
}

/// <summary>The 32 model attributes, plus the starting values of the 5 re-stated ones (#59, #69, #74, #79, #81).</summary>
public sealed class BatteryModel : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string FamilyId { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>Manufacturer identifier and information, and the manufacturing place (#12, #15, #16).</summary>
public sealed class ManufacturingSite : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>One model × calendar year × manufacturing site (decision 3), holding the 16 batch attributes.</summary>
public sealed class ProductionBatch : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string ModelId { get; init; }

    public required string SiteId { get; init; }

    public required int Year { get; init; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>An economic operator's identifier and information (#7, #14), entered once and linked from packs.</summary>
public sealed class EconomicOperator : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>One battery pack, the subject of one passport (decision 16).</summary>
public sealed class BatteryPack : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string BatchId { get; init; }

    public required string OperatorId { get; set; }

    /// <summary>The cluster the pack belongs to (the purchaser); null until assigned.</summary>
    public string? ClusterId { get; set; }

    /// <summary>Pack attributes, the live values and the pack's own re-stated values.</summary>
    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}
```

- [ ] **Step 5: Write the store**

`web/Dpp/Store/DppStore.cs`:

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

    private IMongoCollection<T> Collection<T>() => RequireDatabase().GetCollection<T>(CollectionName<T>());

    private IMongoDatabase RequireDatabase() =>
        _database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");

    private static string CollectionName<T>() =>
        CollectionNames.TryGetValue(typeof(T), out var name)
            ? name
            : throw new ArgumentException($"{typeof(T).Name} is not a passport record.");
}
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppStoreTests"`
Expected: `Passed!  - Failed:     0, Passed:     9`. The first run downloads MongoDB (about a minute). The whole suite now has 650.

- [ ] **Step 7: Commit**

```bash
git add BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs BatteryPassWeb.Tests/Dpp/MongoFixture.cs BatteryPassWeb.Tests/Dpp/DppStoreTests.cs
git commit -m "feat(dpp): store passport records in MongoDB, tested against a throwaway server"
```


---

### Task 3: Bare values into the schema's form

**Files:**
- Create: `web/Dpp/Values/AttributeValueNormalizer.cs`
- Test: `BatteryPassWeb.Tests/Dpp/AttributeValueNormalizerTests.cs`

- [ ] **Step 1: Write the failing test**

`BatteryPassWeb.Tests/Dpp/AttributeValueNormalizerTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class AttributeValueNormalizerTests
{
    private static readonly AttributeValueNormalizer Normalizer = new(DppStoreTesting.Schemas);
    private static readonly AttributeValueValidator Validator = new(DppStoreTesting.Schemas);

    // Design 8.2: one value with a fixed unit — percentages, °C, Ah, kg, kgCO2-eq/kWh, A/Ah, the three voltages, #70,
    // #79 (the pack value) and the two status-like codes.
    private static readonly int[] SingleValueAttributes =
        [20, 21, 22, 30, 31, 32, 33, 34, 49, 50, 51, 52, 53, 57, 59, 63, 64, 65, 66, 69, 70, 71, 72, 74, 79, 85, 87, 88, 89];

    [Fact]
    public void BareValues_ShouldBeWrappedForExactlyTheSingleValueAttributes()
    {
        var wrapped = AttributeCatalog.All
            .Where(a => a.Storage != StorageLocation.System)
            .Where(a => Normalizer.Normalize(a, JsonValue.Create(1)) is JsonObject)
            .Select(a => a.Number);

        Assert.Equal(SingleValueAttributes, wrapped.Order());
    }

    [Theory]
    [InlineData(63, "76")]
    [InlineData(53, "3")]
    [InlineData(59, "4")]
    [InlineData(87, "23")]
    [InlineData(88, "-30")]
    [InlineData(57, "200")]
    [InlineData(21, "512.5")]
    [InlineData(30, "75.4")]
    [InlineData(85, "0.5")]
    [InlineData(64, "520.0")]
    [InlineData(70, "300000")]
    [InlineData(79, "5")]
    [InlineData(22, "\"re-used\"")]
    [InlineData(20, "\"industrial/non-stationary battery\"")]
    public void BareValue_ShouldBecomeAValidSchemaValue(int number, string bare)
    {
        var attribute = AttributeCatalog.ByNumber(number);

        var normalized = Normalizer.Normalize(attribute, JsonNode.Parse(bare));

        Assert.IsType<JsonObject>(normalized);
        Assert.Empty(Validator.Validate(attribute, normalized));
    }

    [Fact]
    public void BareCapacityAndMass_ShouldGetTheCataloguesUnit()
    {
        Assert.Equal("Ah", Normalizer.Normalize(AttributeCatalog.ByNumber(57), JsonValue.Create(200))!["ampereHourMiliamperehourUnit"]!.GetValue<string>());
        Assert.Equal("kg", Normalizer.Normalize(AttributeCatalog.ByNumber(21), JsonValue.Create(512.5))!["gramKilogramUnit"]!.GetValue<string>());
    }

    [Fact]
    public void ValuesInTheSchemaForm_ShouldPassThroughUnchanged()
    {
        var sources = DppTestSeed.Load().Sources;
        var stored = new[] { sources.Pack.Values, sources.Operator.Values, sources.Batch.Values, sources.Site.Values, sources.Model.Values }
            .SelectMany(values => values);

        foreach (var (property, value) in stored)
        {
            var attribute = AttributeCatalog.FindByProperty(property)!;
            Assert.True(JsonNode.DeepEquals(value.Value, Normalizer.Normalize(attribute, value.Value)), property);
        }
    }

    [Theory]
    [InlineData(83, "412")]
    [InlineData(81, "12.5")]
    [InlineData(11, "\"VPC8C05-6788\"")]
    [InlineData(17, "\"2026-09\"")]
    public void PlainValuesOfOtherAttributes_ShouldPassThroughUnchanged(int number, string json)
    {
        var value = JsonNode.Parse(json);

        Assert.True(JsonNode.DeepEquals(value, Normalizer.Normalize(AttributeCatalog.ByNumber(number), value)));
    }

    [Fact]
    public void Null_ShouldStayNull()
    {
        Assert.Null(Normalizer.Normalize(AttributeCatalog.ByNumber(63), null));
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueNormalizerTests"`
Expected: the build fails because `AttributeValueNormalizer` isn't defined yet (a CS0246 error).

- [ ] **Step 3: Write the normalizer**

`web/Dpp/Values/AttributeValueNormalizer.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// Turns a bare value into the schema's own form, e.g. 76 into {"percentUnit":"%","percentageValue":76}, for
/// attributes that are one value with a fixed unit (design 8.2). Everything else is returned unchanged; the
/// validator then decides whether it is right.
/// </summary>
public sealed class AttributeValueNormalizer(OfficialSchemas schemas)
{
    /// <summary>Schema definition → the property that holds the value, and the unit property with its fixed value (null = the catalogue's unit).</summary>
    private static readonly Dictionary<string, (string ValueProperty, string? UnitProperty, string? Unit)> SingleValueDefinitions =
        new(StringComparer.Ordinal)
        {
            ["percentInteger"] = ("percentageValue", "percentUnit", "%"),
            ["celsiusInteger"] = ("celsiusValue", "degreeCelsiusUnit", "°C"),
            ["ampereHourMiliamperehourInteger"] = ("amperehourMiliamperehourValue", "ampereHourMiliamperehourUnit", null),
            ["gramKilogramDecimal"] = ("gramKilogramValue", "gramKilogramUnit", null),
            ["kgCO2EquivalentPerKilowattHourDecimal"] = ("kgCO2EquivalentPerKilowattHourValue", "kgCO2EquivalentPerKilowattHourUnit", "kgCO2-eq/kWh"),
            ["amperePerAmpereHourDecimal"] = ("amperePerAmpereHourValue", "amperePerAmpereHourUnit", "A/Ah"),
            ["voltCelsiusDecimal"] = ("voltValue", "voltUnit", "V"),
            ["wattCelsiusDecimalOneValue"] = ("wattValue", "wattUnit", "W"),
            ["internalResistanceIncreaseType"] = ("internalResistanceIncreaseValueForBatteryPack", "percentUnit", "%"),
            ["batteryStatusType"] = ("batteryStatusValues", null, null),
            ["batteryCategoryType"] = ("batteryCategoryValue", null, null),
        };

    public JsonNode? Normalize(AttributeDefinition attribute, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (value is null || value.GetValueKind() is JsonValueKind.Object or JsonValueKind.Array)
        {
            return value;
        }

        var reference = schemas.FullDocument["$defs"]?[attribute.Section]?["properties"]?[attribute.Property]?["$ref"]?.GetValue<string>();
        var definition = reference?[(reference.LastIndexOf('/') + 1)..];
        if (definition is null || !SingleValueDefinitions.TryGetValue(definition, out var shape))
        {
            return value;
        }

        var wrapped = new JsonObject { [shape.ValueProperty] = value.DeepClone() };
        if (shape.UnitProperty is not null)
        {
            wrapped[shape.UnitProperty] = shape.Unit ?? attribute.Unit;
        }

        return wrapped;
    }
}
```

- [ ] **Step 4: Run it to see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueNormalizerTests"`
Expected: `Passed!  - Failed:     0, Passed:    22`. The whole suite now has 672.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Values/AttributeValueNormalizer.cs BatteryPassWeb.Tests/Dpp/AttributeValueNormalizerTests.cs
git commit -m "feat(dpp): turn bare values into the schema's form"
```


---

### Task 4: Senders and edit rights

**Files:**
- Create: `web/Dpp/Editing/ChangeSender.cs`
- Test: `BatteryPassWeb.Tests/Dpp/EditRightsTests.cs`

- [ ] **Step 1: Write the failing test**

`BatteryPassWeb.Tests/Dpp/EditRightsTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class EditRightsTests
{
    private const string OwnCluster = "cluster-test";

    [Theory]
    [InlineData("team", 11, null, true)] // pack data at creation
    [InlineData("team", 43, null, true)] // shared data: the team only (decision 10)
    [InlineData("team", 63, OwnCluster, true)]
    [InlineData("team", 6, OwnCluster, false)] // issued by the app
    [InlineData("cluster-admin", 63, OwnCluster, true)] // purchaser data (role rule)
    [InlineData("cluster-admin", 92, OwnCluster, true)]
    [InlineData("cluster-admin", 22, OwnCluster, true)]
    [InlineData("cluster-admin", 59, OwnCluster, true)]
    [InlineData("cluster-admin", 11, OwnCluster, false)] // not purchaser data
    [InlineData("cluster-admin", 43, OwnCluster, false)]
    [InlineData("cluster-admin", 63, "another-cluster", false)] // not their battery
    [InlineData("cluster-admin", 59, null, false)] // a model record has no cluster
    [InlineData("feed", 63, OwnCluster, true)] // live values only (J-10)
    [InlineData("feed", 83, OwnCluster, true)]
    [InlineData("feed", 87, OwnCluster, true)]
    [InlineData("feed", 92, OwnCluster, false)]
    [InlineData("feed", 22, OwnCluster, false)]
    [InlineData("feed", 63, "another-cluster", false)]
    public void MayEdit_ShouldFollowTheCatalogueAndTheBatterysCluster(string sender, int number, string? packClusterId, bool expected)
    {
        var who = sender switch
        {
            "team" => ChangeSender.Team("team@example.com"),
            "cluster-admin" => ChangeSender.ClusterAdmin("admin@example.com", OwnCluster),
            _ => ChangeSender.Feed("bms-feed", OwnCluster),
        };

        Assert.Equal(expected, EditRights.MayEdit(who, AttributeCatalog.ByNumber(number), packClusterId));
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.EditRightsTests"`
Expected: the build fails because `ChangeSender` and `EditRights` aren't defined yet (CS0234 or CS0246 errors).

- [ ] **Step 3: Write the sender and the edit rule**

`web/Dpp/Editing/ChangeSender.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Dpp.Editing;

public enum SenderKind
{
    Team,
    ClusterAdmin,
    BatterySystemFeed,
}

/// <summary>
/// Who sends a change, independent of how they signed in. Pages (plan 2d) and API tokens (step 5) map a signed-in
/// user or token to a sender.
/// </summary>
/// <param name="ClusterIds">The clusters a cluster admin or a feed token acts for; empty for the team.</param>
public sealed record ChangeSender(SenderKind Kind, string Who, IReadOnlySet<string> ClusterIds)
{
    public static ChangeSender Team(string who) => new(SenderKind.Team, who, new HashSet<string>(StringComparer.Ordinal));

    public static ChangeSender ClusterAdmin(string who, params string[] clusterIds) =>
        new(SenderKind.ClusterAdmin, who, clusterIds.ToHashSet(StringComparer.Ordinal));

    public static ChangeSender Feed(string who, params string[] clusterIds) =>
        new(SenderKind.BatterySystemFeed, who, clusterIds.ToHashSet(StringComparer.Ordinal));
}

/// <summary>"May this sender change this attribute?" (design 6.1, decisions 9 and 10, J-10).</summary>
public static class EditRights
{
    /// <param name="packClusterId">The cluster of the pack being changed; null for model, batch, site and operator records.</param>
    public static bool MayEdit(ChangeSender sender, AttributeDefinition attribute, string? packClusterId)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(attribute);
        var ownsPack = packClusterId is not null && sender.ClusterIds.Contains(packClusterId);
        return sender.Kind switch
        {
            SenderKind.Team => attribute.Editors.HasFlag(AttributeEditors.Team),
            SenderKind.ClusterAdmin => ownsPack && attribute.Editors.HasFlag(AttributeEditors.BatteryClusterAdmins),
            SenderKind.BatterySystemFeed => ownsPack && attribute.Editors.HasFlag(AttributeEditors.BatterySystemFeed),
            _ => false,
        };
    }
}
```

- [ ] **Step 4: Run it to see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.EditRightsTests"`
Expected: `Passed!  - Failed:     0, Passed:    18`. The whole suite now has 690.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Editing/ChangeSender.cs BatteryPassWeb.Tests/Dpp/EditRightsTests.cs
git commit -m "feat(dpp): decide who may change which attribute"
```


---

### Task 5: The save pipeline

**Files:**
- Create: `web/Dpp/Editing/SavePipeline.cs`
- Test: `BatteryPassWeb.Tests/Dpp/SavePipelineTesting.cs`, `BatteryPassWeb.Tests/Dpp/SavePipelineTests.cs`

- [ ] **Step 1: Write the test helper and the failing tests**

`BatteryPassWeb.Tests/Dpp/SavePipelineTesting.cs`:

```csharp
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The save pipeline around a test store, with the fixed test clock.</summary>
internal static class SavePipelineTesting
{
    public static SavePipeline Pipeline(DppStore store, MongoContext context) => new(
        store,
        new AttributeValueNormalizer(DppStoreTesting.Schemas),
        new AttributeValueValidator(DppStoreTesting.Schemas),
        new ApplicationAuditService(context),
        new FixedTimeProvider(DppStoreTesting.Now));
}
```

`BatteryPassWeb.Tests/Dpp/SavePipelineTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class SavePipelineTests(MongoFixture mongo)
{
    private const string PackId = "pack-VPC8C05-6788";
    private static readonly ChangeSender Team = ChangeSender.Team("team@example.com");

    [Fact]
    public async Task Team_ShouldSaveAValidValueWithWhenSourceAndWho()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange(("thirdPartyServiceProvider", "\"New Hosting AB\"")));

        Assert.True(result.Saved);
        Assert.Equal(KeyOutcome.Saved, Assert.Single(result.Keys).Outcome);
        var stored = (await store.GetAsync<BatteryPack>(PackId))!.Values["thirdPartyServiceProvider"];
        Assert.Equal("New Hosting AB", stored.Value.GetValue<string>());
        Assert.Equal(DppStoreTesting.Now.UtcDateTime, stored.GeneratedAtUtc);
        Assert.Equal("form", stored.Source);
        Assert.Equal("team@example.com", stored.Who);
    }

    [Fact]
    public async Task FeedValue_ShouldBeStoredInTheSchemaFormWithItsMeasurementTime()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var measured = new DateTime(2026, 10, 27, 10, 2, 59, DateTimeKind.Utc);
        var change = new ChangeSet(TargetKind.Pack, PackId, Values(("stateOfChargeSoC", "55")), "feed", measured);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), change);

        Assert.True(result.Saved);
        var stored = (await store.GetAsync<BatteryPack>(PackId))!.Values["stateOfChargeSoC"];
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"percentageValue":55,"percentUnit":"%"}"""), stored.Value));
        Assert.Equal(measured, stored.GeneratedAtUtc);
        Assert.Equal("feed", stored.Source);
    }

    [Fact]
    public async Task Preview_ShouldReportWithoutSaving()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange(("thirdPartyServiceProvider", "\"Preview AB\"")), preview: true);

        Assert.False(result.Saved);
        Assert.Equal(KeyOutcome.Saved, Assert.Single(result.Keys).Outcome);
        Assert.Equal("Example Passport Hosting AB", (await store.GetAsync<BatteryPack>(PackId))!.Values["thirdPartyServiceProvider"].Value.GetValue<string>());
    }

    [Theory]
    [InlineData("remainingCapacity", "{}", KeyOutcome.Ignored, "not in the attribute list")] // schema property left out of V3
    [InlineData("totalChargedEnergyKwh", "69", KeyOutcome.Ignored, "not in the attribute list")]
    [InlineData("ratedCapacity", "200", KeyOutcome.Rejected, "stored on the model")]
    [InlineData("batteryStatus", "\"waste\"", KeyOutcome.Rejected, "status change")]
    [InlineData("capacityFade", "5", KeyOutcome.Rejected, "status change")]
    [InlineData("uniqueProductIdentifier", "\"urn:other\"", KeyOutcome.Rejected, "set by the app")]
    [InlineData("dppSchemaVersion", "\"x\"", KeyOutcome.Rejected, "set by the app")]
    [InlineData("stateOfChargeSoC", "120", KeyOutcome.Rejected, "maximum")]
    [InlineData("manufacturingDate", "\"2026-10-14\"", KeyOutcome.Rejected, "decision 18")]
    public async Task Key_ShouldBeIgnoredOrRejectedWithTheReason(string key, string json, KeyOutcome expected, string reason)
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange((key, json), ("thirdPartyServiceProvider", "\"Still Saved AB\"")));

        var keyResult = result.Keys.Single(k => k.Key == key);
        Assert.Equal(expected, keyResult.Outcome);
        Assert.Contains(reason, keyResult.Message, StringComparison.Ordinal);
        Assert.True(result.Saved); // the valid key in the same change set is still saved (J-12)
        var pack = (await store.GetAsync<BatteryPack>(PackId))!;
        Assert.Equal(seed.Sources.Pack.Values.ContainsKey(key), pack.Values.ContainsKey(key));
        if (seed.Sources.Pack.Values.TryGetValue(key, out var original))
        {
            Assert.True(JsonNode.DeepEquals(original.Value, pack.Values[key].Value));
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"  \"")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task EmptyValue_ShouldKeepTheStoredValue(string json)
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange(("thirdPartyServiceProvider", json)));

        Assert.False(result.Saved);
        Assert.Equal(KeyOutcome.KeptEmpty, Assert.Single(result.Keys).Outcome);
        Assert.Equal("Example Passport Hosting AB", (await store.GetAsync<BatteryPack>(PackId))!.Values["thirdPartyServiceProvider"].Value.GetValue<string>());
    }

    [Theory]
    [InlineData("cluster-test", "temperatureInformation", true)]
    [InlineData("another-cluster", "temperatureInformation", false)]
    [InlineData("cluster-test", "batterySerialNumber", false)]
    public async Task ClusterAdmin_ShouldChangeOnlyPurchaserDataOfTheirOwnBatteries(string adminCluster, string key, bool saved)
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var value = key == "temperatureInformation" ? "31" : "\"X-1\"";

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(ChangeSender.ClusterAdmin("admin@example.com", adminCluster), PackChange((key, value)));

        Assert.Equal(saved, result.Saved);
        Assert.Equal(saved ? KeyOutcome.Saved : KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task Feed_ShouldNotChangeAccidentInformation()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var accident = """{"contentType":"text/html","url":"https://example.com/accident"}""";

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), PackChange(("informationOnAccidents", accident)));

        Assert.Equal(KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task Team_ShouldSetAModelsStartingValueForARestatedAttribute()
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var change = new ChangeSet(TargetKind.Model, seed.Sources.Model.Id, Values(("capacityFade", "1")), "form");

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, change);

        Assert.True(result.Saved);
        Assert.Equal(1, (await store.GetAsync<BatteryModel>(seed.Sources.Model.Id))!.Values["capacityFade"].Value["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task MissingRecord_ShouldBeReportedAsNotFound()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var result = await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, new ChangeSet(TargetKind.Batch, "no-such-batch", Values(("carbonFootprintPerformanceClass", "\"Class A\"")), "form"));

        Assert.False(result.Found);
        Assert.False(result.Saved);
    }

    [Fact]
    public async Task Save_ShouldBeLoggedWithTheResultPerKey()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        await SavePipelineTesting.Pipeline(store, context).SaveAsync(Team, PackChange(("thirdPartyServiceProvider", "\"Logged AB\""), ("ratedCapacity", "200")));

        var events = await new ApplicationAuditService(context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var logged = Assert.Single(events, e => e["eventType"].AsString == SavePipeline.AuditEventType);
        Assert.Equal(PackId, logged["entityId"].AsString);
        Assert.Equal("team@example.com", logged["actor"].AsString);
        Assert.Equal(2, logged["metadata"]["keys"].AsBsonArray.Count);
    }

    private static ChangeSet PackChange(params (string Key, string Json)[] values) => new(TargetKind.Pack, PackId, Values(values), "form");

    private static Dictionary<string, JsonNode?> Values(params (string Key, string Json)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonNode.Parse(v.Json), StringComparer.Ordinal);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.SavePipelineTests"`
Expected: the build fails because `SavePipeline`, `ChangeSet` and `KeyOutcome` aren't defined yet (CS0246 errors).

- [ ] **Step 3: Write the pipeline**

`web/Dpp/Editing/SavePipeline.cs`:

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
    TimeProvider clock)
{
    public const string AuditEventType = "dpp.change";

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

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.SavePipelineTests"`
Expected: `Passed!  - Failed:     0, Passed:    24`. The whole suite now has 714.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Editing/SavePipeline.cs BatteryPassWeb.Tests/Dpp/SavePipelineTesting.cs BatteryPassWeb.Tests/Dpp/SavePipelineTests.cs
git commit -m "feat(dpp): add the save pipeline with preview and a change log"
```


---

### Task 6: Test identifiers

**Files:**
- Create: `web/Dpp/Identifiers/TestIdentifierScheme.cs`
- Test: `BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs`

- [ ] **Step 1: Write the failing test**

`BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs`:

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

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.TestIdentifierSchemeTests"`
Expected: the build fails because `BatteryPassWeb.Dpp.Identifiers` isn't defined yet (a CS0234 or CS0246 error).

- [ ] **Step 3: Write the scheme**

`web/Dpp/Identifiers/TestIdentifierScheme.cs`:

```csharp
namespace BatteryPassWeb.Dpp.Identifiers;

/// <summary>Issues unique product identifiers (#6). The real EN 18219 scheme is chosen before go-live (decision 5, O-1).</summary>
public interface IIdentifierScheme
{
    string NewPackIdentifier();

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

    public bool IsTestIdentifier(string identifier) =>
        identifier?.StartsWith(Prefix, StringComparison.Ordinal) == true;
}
```

- [ ] **Step 4: Run it to see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.TestIdentifierSchemeTests"`
Expected: `Passed!  - Failed:     0, Passed:     5`. The whole suite now has 719.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Identifiers/TestIdentifierScheme.cs BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs
git commit -m "feat(dpp): issue marked test identifiers for packs"
```


---

### Task 7: Creating draft packs

**Files:**
- Create: `web/Dpp/Editing/PackCreationService.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs`

- [ ] **Step 1: Write the failing test**

`BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs`:

```csharp
using System.Text.Json.Nodes;
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

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PackCreationServiceTests"`
Expected: the build fails because `PackCreationService` and `NewPack` aren't defined yet (CS0246 errors).

- [ ] **Step 3: Write the service**

`web/Dpp/Editing/PackCreationService.cs`:

```csharp
using System.Text.Json.Nodes;
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
/// Creates a draft pack (design 7.1): the team only, all values accepted or nothing saved. The app issues #6 and sets
/// #22 to "original"; everything else goes through the save pipeline's checks.
/// </summary>
public sealed class PackCreationService(
    DppStore store,
    SavePipeline pipeline,
    IIdentifierScheme identifiers,
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

- [ ] **Step 4: Run it to see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PackCreationServiceTests"`
Expected: `Passed!  - Failed:     0, Passed:     9`. The whole suite now has 728.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Editing/PackCreationService.cs BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs
git commit -m "feat(dpp): create draft packs with a test identifier and status original"
```


---

### Task 8: Register the services and create the indexes at startup

**Files:**
- Create: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Program.cs` (three small edits)
- Test: `BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`

- [ ] **Step 1: Write the failing test**

`BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`:

```csharp
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
            .AddDpp()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<SavePipeline>());
        Assert.NotNull(provider.GetRequiredService<PackCreationService>());
        Assert.IsType<TestIdentifierScheme>(provider.GetRequiredService<IIdentifierScheme>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
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

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: the build fails because `AddDpp` isn't defined yet (a CS1061 error).

- [ ] **Step 3: Write the registration**

`web/Dpp/DppServiceCollectionExtensions.cs`:

```csharp
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BatteryPassWeb.Dpp;

public static class DppServiceCollectionExtensions
{
    /// <summary>Registers the V3 passport services (design section 3). They need MongoContext and ApplicationAuditService.</summary>
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
        return services;
    }
}
```

- [ ] **Step 4: Wire it into `web/Program.cs`**

At the top, after `using BatteryPassWeb.Configuration;`:

```csharp
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Store;
```

After `builder.Services.AddSingleton<ExternalApiInitializer>();`:

```csharp
builder.Services.AddDpp();
```

At the start of the startup block `using (var scope = app.Services.CreateScope())`, before the `ExternalApiInitializer` lines:

```csharp
    await scope.ServiceProvider.GetRequiredService<DppStore>().EnsureIndexesAsync();
```

The block then reads:

```csharp
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DppStore>().EnsureIndexesAsync();
    var initializer = scope.ServiceProvider.GetRequiredService<ExternalApiInitializer>();
    await initializer.InitializeAsync();
}
```

Without a database (`MONGODB_URI` empty) the index call does nothing, so the app still starts.

- [ ] **Step 5: Run the test to see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:     2`.

- [ ] **Step 6: Commit**

```bash
git add web/Dpp/DppServiceCollectionExtensions.cs web/Program.cs BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
git commit -m "feat(dpp): register the passport services and create their indexes at startup"
```


---

### Task 9: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:   730, Skipped:     0, Total:   730` (637 from step 1 + 93 new), in about 7 seconds.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed, except documents the product owner chose not to commit.

## After this plan

Plan 2b (publication and versions) builds on `DppStore.LoadSourcesAsync`, `PassportAssembler` and `OfficialSchemas.CheckPassport`: the completeness gate, #5 passport IDs, signing of the static core with today's ECDSA services, versions and archiving, and turning the pipeline's "apply" into "new version" for published packs.

Step 4's status change needs its own way past the pipeline's rule that rejects #22 and the re-stated values on a pack: either an explicit status-change entry on `SavePipeline.Evaluate`, or the status-change service writing through `DppStore.SetValuesAsync` after its own checks. The rule stays for forms, uploads and the feed.

