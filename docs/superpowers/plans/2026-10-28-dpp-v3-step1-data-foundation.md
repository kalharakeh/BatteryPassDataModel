# DPP V3 Step 1: Data Foundation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the 73-attribute catalogue, the official v2.0.1 schemas, value checks, the stored records and the passport assembler beside today's app, all covered by tests.

**Architecture:** New code lives in `web/Dpp/` (namespaces `BatteryPassWeb.Dpp.*`) and doesn't touch today's v1.2 code, which keeps working until step 3 switches over. The catalogue is the single definition of the 73 attributes. Values are stored in the schema's own JSON form and checked against the embedded official schemas; the assembler builds a pack's passport JSON from the records the catalogue names.

**Tech Stack:** .NET 10, ASP.NET Core MVC, MongoDB.Driver 3.9 (BSON mapping only, no database needed in tests), JsonSchema.Net 9.5.0 (JSON Schema draft 2020-12), xUnit 2.9; Python 3 standard library for the extract script.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 3, 4, 11, 14, Appendices A and B) and `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-decisions.md` (decisions 1–21). This plan replaces `docs/superpowers/plans/2026-10-28-dpp-v3-high-priority-access-fixes.md`, whose access rules move to step 3.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite` and run every command from its root, in Git Bash.
- **Branch:** the worktree is on `security/demo-api-hardening`. Create a branch first: `git switch -c feature/dpp-v3-step1-data-foundation`.
- **Uncommitted docs:** the design and decision records in `docs/superpowers/specs/` (and the old plan in `docs/superpowers/plans/`) are not committed yet. Ask the product owner whether to commit them on this branch before Task 1.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` must show `Passed!  - Failed:     0, Passed:   464`. After this plan it shows 637.
- **Inputs:** network access to `git.gefeg.com` (Task 1; copying the files is approved in decision 21) and the V3 workbook at `C:\Users\KassemAlHarakeh\Downloads\DPP_Internal_2026_BatteryPass-Ready_DataAttributeLongList_v2.0_August version_V3.xlsx`, SHA-256 `f13b5d5cf60933f00ce280dfd4c88ed58249b9b1c33faf5d0b3d8f52e0d7ad3d` (Task 3).
- **Domain words:** *pack* = one battery and one passport (decision 16). *Batch* = model × calendar year × manufacturing site (decision 3). *Re-stated* = #59, #69, #74, #79, #81, model values that a pack overrides at a status change (decision 11). *Live set* = #63, #83, #87, #92, values that change without a new passport version.

## Judgement calls in this plan

1. **Nothing is removed in step 1.** The new code is built beside today's app; the switch-over and removals happen in step 3 (design section 14, updated 28 Oct 2026), so the app keeps working after every step.
2. **The store starts as records plus their MongoDB mapping.** Repositories, passport versions and documents arrive in step 2 with the editors that use them.
3. **Values are stored in the schema's own form, units included.** The save pipeline (step 2) will turn bare values such as `76` into `{"percentUnit":"%","percentageValue":76}`.
4. **#1 DPP schema version is `BatteryPass-Ready 2.0.1`.** The file's DIN note asks for the JTC-24 standards once they are published in the Official Journal; until then the value names the data model.
5. **JsonSchema.Net 9.5.0** checks the official schemas. Today's `JsonSchemaValidationService` is hand-written for the v1.2 aspect schemas and doesn't support draft 2020-12.
6. **The schema files are pinned** to GitLab commit `64761a79` and checked by SHA-256; `.gitattributes` keeps git from changing their line endings (`core.autocrlf` is on).
7. **Format keywords are only annotations** in JSON Schema 2020-12 (even `"not a date"` passes `format: date`), so the value checks add the app's own rules: absolute URIs, UTC timestamps, ISO 8601 periods, and year-month for #17 (decision 18). Units are fixed to `kg` for #21 and `Ah` for #57.

## File structure

| File | Responsibility |
|---|---|
| `web/Dpp/Schemas/v2.0.1/*.json` | The two official schemas, unchanged (embedded in the app) |
| `web/Dpp/Schemas/v2.0.1/NOTICE.md` | Source, commit, licence (CC BY 4.0) and SHA-256 of the copies |
| `web/Dpp/Schemas/OfficialSchemas.cs` | Loads the embedded schemas; checks whole passports or one property's value |
| `web/Dpp/Catalog/AttributeDefinition.cs` | The catalogue's enums and the `AttributeDefinition` record |
| `web/Dpp/Catalog/AttributeCatalog.cs` | The 73 attribute definitions and lookups |
| `web/Dpp/Values/AttributeValue.cs` | One stored value with when, source and who |
| `web/Dpp/Values/JsonNodeBsonSerializer.cs` | Stores JSON values as native BSON |
| `web/Dpp/Values/AttributeValueValidator.cs` | Checks one value: schema first, then the app's format and unit rules |
| `web/Dpp/Store/DppRecords.cs` | Family, model, site, batch, operator and pack records |
| `web/Dpp/Assembly/PassportAssembler.cs` | Builds the v2.0.1 passport JSON for one pack |
| `scripts/dpp/extract_longlist.py` | Extracts the V3 file's 73 rows for the compliance test |
| `BatteryPassWeb.Tests/Dpp/dpp-v3-longlist-extract.json` | The extract (generated, checked in) |
| `BatteryPassWeb.Tests/Dpp/seed-other-industrial.json` | One fully filled test pack |
| `BatteryPassWeb.Tests/Dpp/*.cs` | Tests, the seed loader and a repository-file helper |
| `.gitattributes` | Keeps the schema files byte-for-byte |
| `web/BatteryPassWeb.csproj` | Adds JsonSchema.Net and embeds the schemas |


---

### Task 1: Official schemas

**Files:**
- Create: `web/Dpp/Schemas/v2.0.1/Other_Industrial_Above_2kWh_2.0.1.json`, `web/Dpp/Schemas/v2.0.1/Other_Industrial_Above_2kWh_Feb27_2.0.1.json` (downloaded)
- Create: `web/Dpp/Schemas/v2.0.1/NOTICE.md`, `.gitattributes`, `web/Dpp/Schemas/OfficialSchemas.cs`
- Modify: `web/BatteryPassWeb.csproj`
- Test: `BatteryPassWeb.Tests/Dpp/DppTestFiles.cs`, `BatteryPassWeb.Tests/Dpp/OfficialSchemasTests.cs`

- [ ] **Step 1: Download the two schema files from the pinned commit and check them**

```bash
mkdir -p web/Dpp/Schemas/v2.0.1
BASE="https://git.gefeg.com/bp/BatteryPassDataModel/-/raw/64761a79bc287f2e0eb371837c1d02e4eacb9c37/JSONSchemaFiles/v2.0.1"
curl -sSfL "$BASE/Other_Industrial_Above_2kWh_2.0.1.json" -o web/Dpp/Schemas/v2.0.1/Other_Industrial_Above_2kWh_2.0.1.json
curl -sSfL "$BASE/Other_Industrial_Above_2kWh_Feb27_2.0.1.json" -o web/Dpp/Schemas/v2.0.1/Other_Industrial_Above_2kWh_Feb27_2.0.1.json
sha256sum web/Dpp/Schemas/v2.0.1/*.json
```

Expected:

```
31c0c4a5bdbbf2dc0d4908cfb94db1a0da1a65d7b41d545a04029a94c97000ab *web/Dpp/Schemas/v2.0.1/Other_Industrial_Above_2kWh_2.0.1.json
aa2ef3f30bd967023946032e338c02ca82f03bc8d26b0a110f0edeafbe4604f5 *web/Dpp/Schemas/v2.0.1/Other_Industrial_Above_2kWh_Feb27_2.0.1.json
```

(Git Bash may print a space instead of `*`.) If a hash differs, stop and ask: the files changed upstream.

- [ ] **Step 2: Add the notice next to the files**

`web/Dpp/Schemas/v2.0.1/NOTICE.md`:

````markdown
# Official BatteryPass-Ready schemas (v2.0.1)

These two files are unchanged copies of the BatteryPass-Ready JSON schemas for Other industrial batteries above 2 kWh (decision 21 in `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-decisions.md`).

| File | SHA-256 |
|---|---|
| `Other_Industrial_Above_2kWh_2.0.1.json` | `31c0c4a5bdbbf2dc0d4908cfb94db1a0da1a65d7b41d545a04029a94c97000ab` |
| `Other_Industrial_Above_2kWh_Feb27_2.0.1.json` | `aa2ef3f30bd967023946032e338c02ca82f03bc8d26b0a110f0edeafbe4604f5` |

- **Source:** https://git.gefeg.com/bp/BatteryPassDataModel, folder `JSONSchemaFiles/v2.0.1`, commit `64761a79bc287f2e0eb371837c1d02e4eacb9c37` (23 Sep 2026, "v2.0.1 updated").
- **Copied:** 28 Oct 2026.
- **Licence:** CC BY 4.0 (https://creativecommons.org/licenses/by/4.0), as stated in the repository's README.
- **Copyright:** © 2026 BatteryPass-Ready Consortium; maintained by acatech on the consortium's behalf.

The app embeds both files (see `web/BatteryPassWeb.csproj`) and checks passports and single values against them (`OfficialSchemas`). Don't edit them: `OfficialSchemasTests` compares their SHA-256 with the table above, and `.gitattributes` stops git from changing their line endings. To move to a newer version, add the new files beside these and update this note and the tests.
````

- [ ] **Step 3: Keep git from changing the schema files' line endings**

`.gitattributes` (new file at the repository root):

```text
web/Dpp/Schemas/**/*.json -text
```

- [ ] **Step 4: Add JsonSchema.Net and embed the schemas**

`web/BatteryPassWeb.csproj` (whole file after the change: one new package line, one new `ItemGroup`; `Content Remove` keeps the files out of the published output):

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="JsonSchema.Net" Version="9.5.0" />
    <PackageReference Include="MongoDB.Driver" Version="3.9.0" />
    <PackageReference Include="QRCoder" Version="1.6.0" />
    <PackageReference Include="Snappier" Version="1.3.1" />
  </ItemGroup>

  <ItemGroup>
    <Content Remove=".env;.env.*" />
    <None Remove=".env;.env.*" />
  </ItemGroup>

  <ItemGroup>
    <Content Remove="Dpp\Schemas\**" />
    <EmbeddedResource Include="Dpp\Schemas\v2.0.1\*.json" LogicalName="Dpp.Schemas.%(Filename)%(Extension)" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="..\BatteryPass\**\*">
      <Link>BatteryPass\%(RecursiveDir)%(Filename)%(Extension)</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </Content>
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/DppTestFiles.cs`:

```csharp
namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Finds files in the repository from the test output folder (the house RepoFile pattern).</summary>
internal static class DppTestFiles
{
    public static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
```

`BatteryPassWeb.Tests/Dpp/OfficialSchemasTests.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class OfficialSchemasTests
{
    private static readonly OfficialSchemas Schemas = OfficialSchemas.Load();

    [Theory]
    [InlineData(OfficialSchemas.FullResourceName, "31c0c4a5bdbbf2dc0d4908cfb94db1a0da1a65d7b41d545a04029a94c97000ab")]
    [InlineData(OfficialSchemas.Feb2027ResourceName, "aa2ef3f30bd967023946032e338c02ca82f03bc8d26b0a110f0edeafbe4604f5")]
    public void EmbeddedSchema_ShouldBeTheOfficialFileUnchanged(string resourceName, string expectedSha256)
    {
        using var stream = typeof(OfficialSchemas).Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        Assert.Equal(expectedSha256, Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckPassport_ShouldReportMissingTopLevelParts(bool feb2027Variant)
    {
        var issues = Schemas.CheckPassport(new JsonObject(), feb2027Variant);

        Assert.Contains(issues, issue => issue.Message.Contains("dppHeader", StringComparison.Ordinal));
    }

    [Fact]
    public void FindProperty_ShouldFollowReferencesToTheDefinition()
    {
        var capacityFade = Schemas.FindProperty("performanceAndDurability", "capacityFade");

        Assert.NotNull(capacityFade);
        Assert.NotNull(capacityFade["properties"]?["percentageValue"]);
    }

    [Fact]
    public void CheckProperty_ShouldAcceptAValidValue()
    {
        var value = JsonNode.Parse("""{"percentUnit":"%","percentageValue":12}""");

        Assert.Empty(Schemas.CheckProperty("performanceAndDurability", "capacityFade", value));
    }

    [Fact]
    public void CheckProperty_ShouldReportWhereAValueIsOutOfRange()
    {
        var value = JsonNode.Parse("""{"percentUnit":"%","percentageValue":120}""");

        var issues = Schemas.CheckProperty("performanceAndDurability", "capacityFade", value);

        Assert.Contains(issues, issue => issue.Location == "/percentageValue" && issue.Message.StartsWith("maximum", StringComparison.Ordinal));
    }

    [Fact]
    public void CheckProperty_ShouldRejectAPropertyTheSchemaDoesNotHave()
    {
        Assert.Throws<ArgumentException>(() => Schemas.CheckProperty("performanceAndDurability", "noSuchProperty", JsonValue.Create(1)));
    }

    [Fact]
    public void RequiredProperties_ShouldReadASectionsRequiredList()
    {
        var required = OfficialSchemas.RequiredProperties(Schemas.FullDocument, "dppHeader");

        Assert.Equal(7, required.Count);
        Assert.Contains("dppStatus", required);
        Assert.Empty(OfficialSchemas.RequiredProperties(Schemas.Feb2027Document, "batteryCarbonFootprint"));
    }
}
```

- [ ] **Step 6: Run the tests to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.OfficialSchemasTests"`
Expected: the build fails because `BatteryPassWeb.Dpp.Schemas.OfficialSchemas` isn't defined yet (a CS0234 or CS0246 error).

- [ ] **Step 7: Write `OfficialSchemas`**

`web/Dpp/Schemas/OfficialSchemas.cs`:

```csharp
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace BatteryPassWeb.Dpp.Schemas;

/// <summary>One problem found by a schema check: where it is (a JSON pointer into the checked value) and what is wrong.</summary>
public sealed record SchemaIssue(string Location, string Message);

/// <summary>
/// The official BatteryPass-Ready v2.0.1 schemas for Other industrial batteries above 2 kWh, embedded in this
/// assembly (see Dpp/Schemas/v2.0.1/NOTICE.md). Checks whole passports, or single values against one property.
/// </summary>
public sealed class OfficialSchemas
{
    public const string FullResourceName = "Dpp.Schemas.Other_Industrial_Above_2kWh_2.0.1.json";
    public const string Feb2027ResourceName = "Dpp.Schemas.Other_Industrial_Above_2kWh_Feb27_2.0.1.json";

    private const string BaseAddress = "https://battery-pass.local/schemas/v2.0.1/";
    private const string FullAddress = BaseAddress + "other-industrial.json";
    private const string Feb2027Address = BaseAddress + "other-industrial-feb27.json";
    private const string DefsPrefix = "#/$defs/";

    private static readonly EvaluationOptions ListOutput = new() { OutputFormat = OutputFormat.List };

    private readonly BuildOptions _buildOptions = new() { SchemaRegistry = new SchemaRegistry() };
    private readonly ConcurrentDictionary<string, Lazy<JsonSchema>> _propertySchemas = new(StringComparer.Ordinal);

    private OfficialSchemas()
    {
        var fullText = ReadResource(FullResourceName);
        var feb2027Text = ReadResource(Feb2027ResourceName);
        FullDocument = JsonNode.Parse(fullText)!;
        Feb2027Document = JsonNode.Parse(feb2027Text)!;
        Full = JsonSchema.FromText(fullText, _buildOptions, new Uri(FullAddress));
        Feb2027 = JsonSchema.FromText(feb2027Text, _buildOptions, new Uri(Feb2027Address));
    }

    /// <summary>The full schema as JSON, for reading property definitions and required lists.</summary>
    public JsonNode FullDocument { get; }

    /// <summary>The Feb-2027 variant as JSON.</summary>
    public JsonNode Feb2027Document { get; }

    public JsonSchema Full { get; }

    public JsonSchema Feb2027 { get; }

    /// <summary>Loads and builds both schemas. Build once and reuse it: building takes a moment.</summary>
    public static OfficialSchemas Load() => new();

    /// <summary>The definition of a property in a section (or "dppHeader"), with references followed; null if the full schema has no such property.</summary>
    public JsonObject? FindProperty(string section, string property) => FindProperty(FullDocument, section, property);

    public static JsonObject? FindProperty(JsonNode document, string section, string property)
    {
        var node = document["$defs"]?[section]?["properties"]?[property];
        return node is null ? null : Resolve(document, node);
    }

    /// <summary>Follows local "#/$defs/..." references until the node is a definition. All references in these schemas are local.</summary>
    public static JsonObject Resolve(JsonNode document, JsonNode node)
    {
        var current = node.AsObject();
        while (current["$ref"]?.GetValue<string>() is { } reference)
        {
            if (!reference.StartsWith(DefsPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported schema reference '{reference}'.");
            }

            current = document["$defs"]![reference[DefsPrefix.Length..]]!.AsObject();
        }

        return current;
    }

    /// <summary>The "required" list of a section (or "dppHeader"); empty when the schema has no such section.</summary>
    public static IReadOnlyList<string> RequiredProperties(JsonNode document, string section) =>
        document["$defs"]?[section]?["required"]?.AsArray().Select(name => name!.GetValue<string>()).ToArray() ?? [];

    /// <summary>Checks a whole passport against the full schema, or against the Feb-2027 variant.</summary>
    public IReadOnlyList<SchemaIssue> CheckPassport(JsonNode passport, bool feb2027Variant) =>
        Evaluate(feb2027Variant ? Feb2027 : Full, passport);

    /// <summary>Checks one value against its property's definition in the full schema.</summary>
    public IReadOnlyList<SchemaIssue> CheckProperty(string section, string property, JsonNode? value)
    {
        if (FindProperty(section, property) is null)
        {
            throw new ArgumentException($"The schema has no property {section}.{property}.", nameof(property));
        }

        var pointer = $"/$defs/{section}/properties/{property}";
        var schema = _propertySchemas.GetOrAdd(pointer, key => new Lazy<JsonSchema>(() => JsonSchema.FromText(
            $$"""{"$schema":"https://json-schema.org/draft/2020-12/schema","$ref":"{{FullAddress}}#{{key}}"}""",
            _buildOptions,
            new Uri($"{BaseAddress}property/{section}/{property}.json")))).Value;
        return Evaluate(schema, value);
    }

    private static IReadOnlyList<SchemaIssue> Evaluate(JsonSchema schema, JsonNode? instance)
    {
        var results = schema.Evaluate(JsonSerializer.SerializeToElement(instance), ListOutput);
        if (results.IsValid)
        {
            return [];
        }

        var issues = new List<SchemaIssue>();
        foreach (var result in (results.Details ?? []).Prepend(results))
        {
            foreach (var (keyword, message) in result.Errors ?? [])
            {
                issues.Add(new SchemaIssue(result.InstanceLocation.ToString(), $"{keyword}: {message}"));
            }
        }

        return issues;
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(OfficialSchemas).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded schema '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

- [ ] **Step 8: Run the tests to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.OfficialSchemasTests"`
Expected: `Passed!  - Failed:     0, Passed:     9`.

- [ ] **Step 9: Commit**

```bash
git add .gitattributes web/BatteryPassWeb.csproj web/Dpp/Schemas BatteryPassWeb.Tests/Dpp/DppTestFiles.cs BatteryPassWeb.Tests/Dpp/OfficialSchemasTests.cs
git commit -m "feat(dpp): embed the official v2.0.1 Other industrial schemas"
```


---

### Task 2: Catalogue types

**Files:**
- Create: `web/Dpp/Catalog/AttributeDefinition.cs`
- Test: `BatteryPassWeb.Tests/Dpp/AttributeDefinitionTests.cs`

- [ ] **Step 1: Write the failing test**

`BatteryPassWeb.Tests/Dpp/AttributeDefinitionTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class AttributeDefinitionTests
{
    [Theory]
    [InlineData("dppHeader", "uniqueProductIdentifier", "dppHeader.uniqueProductIdentifier")]
    [InlineData("performanceAndDurability", "stateOfChargeSoC", "batteryPassportData.performanceAndDurability.stateOfChargeSoC")]
    public void Path_ShouldPointIntoTheAssembledPassport(string section, string property, string expected)
    {
        Assert.Equal(expected, Definition(11, section, property, AttributeLevel.Pack).Path);
    }

    [Theory]
    [InlineData(7, AttributeLevel.Pack, StorageLocation.Operator)]
    [InlineData(14, AttributeLevel.Pack, StorageLocation.Operator)]
    [InlineData(11, AttributeLevel.Pack, StorageLocation.Pack)]
    [InlineData(1, AttributeLevel.PassportSystem, StorageLocation.System)]
    [InlineData(10, AttributeLevel.Batch, StorageLocation.Batch)]
    [InlineData(12, AttributeLevel.Site, StorageLocation.Site)]
    [InlineData(21, AttributeLevel.Model, StorageLocation.Model)]
    [InlineData(59, AttributeLevel.ModelRestatedPerPack, StorageLocation.ModelWithPackOverride)]
    public void Storage_ShouldFollowTheLevelExceptForOperatorAttributes(int number, AttributeLevel level, StorageLocation expected)
    {
        Assert.Equal(expected, Definition(number, "identifiersAndProductData", "anyProperty", level).Storage);
    }

    private static AttributeDefinition Definition(int number, string section, string property, AttributeLevel level) =>
        new(number, "Test attribute", section, property, level, Audience.Public, LegitimateInterestGroups.None,
            AttributeEditors.Team, ChangeRule.Fixed, null, Requirement.Mandatory, true);
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeDefinitionTests"`
Expected: the build fails because the `BatteryPassWeb.Dpp.Catalog` types aren't defined yet (a CS0234 or CS0246 error).

- [ ] **Step 3: Write the types**

`web/Dpp/Catalog/AttributeDefinition.cs`:

```csharp
namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>Where an attribute belongs (design section 4).</summary>
public enum AttributeLevel
{
    PassportSystem,
    Pack,
    Batch,
    Site,
    Model,
    ModelRestatedPerPack,
}

/// <summary>Who may see an attribute, from the file's access column (design section 5.1).</summary>
public enum Audience
{
    Public,
    Purchaser,
    Shared,
    Authorities,
}

/// <summary>The legitimate-interest roles that see a shared attribute (decision 6).</summary>
[Flags]
public enum LegitimateInterestGroups
{
    None = 0,
    Repair = 1,
    Recycling = 2,
    Treatment = 4,
}

/// <summary>Who may change an attribute (design section 6.1).</summary>
[Flags]
public enum AttributeEditors
{
    None = 0,
    System = 1,
    Team = 2,
    BatteryClusterAdmins = 4,
    BatterySystemFeed = 8,
    MarketSurveillance = 16,
}

/// <summary>When an attribute's value changes (design Appendix A, column "Changes").</summary>
public enum ChangeRule
{
    Fixed,
    Lifecycle,
    OnPublishedOrLiveUpdate,
    NewPerVersion,
    OnStatusChange,
    RestatedOnStatusChange,
    Live,
    IfProviderChanges,
}

/// <summary>The file's requirement for Other industrial batteries: M (Battery Regulation), (M) (ESPR and JTC-24) or C (conditional).</summary>
public enum Requirement
{
    Mandatory,
    MandatoryEspr,
    Conditional,
}

/// <summary>Which stored record holds an attribute's value.</summary>
public enum StorageLocation
{
    System,
    Pack,
    Operator,
    Batch,
    Site,
    Model,
    ModelWithPackOverride,
}

/// <summary>One row of the attribute catalogue (design Appendix A).</summary>
/// <param name="Section">"dppHeader", or a section of batteryPassportData in the v2.0.1 schema.</param>
/// <param name="Property">The v2.0.1 property name. Property names are unique across sections.</param>
/// <param name="Unit">The file's unit for this battery category, or null.</param>
public sealed record AttributeDefinition(
    int Number,
    string Name,
    string Section,
    string Property,
    AttributeLevel Level,
    Audience Audience,
    LegitimateInterestGroups SharedWith,
    AttributeEditors Editors,
    ChangeRule Change,
    string? Unit,
    Requirement Requirement,
    bool MandatoryFromFeb2027)
{
    public const string HeaderSection = "dppHeader";
    public const string DataRoot = "batteryPassportData";

    /// <summary>Dotted path in the assembled passport, e.g. batteryPassportData.performanceAndDurability.capacityFade.</summary>
    public string Path => Section == HeaderSection ? $"{HeaderSection}.{Property}" : $"{DataRoot}.{Section}.{Property}";

    /// <summary>The record that stores the value. #7 and #14 are per pack in the file, but entered once per economic operator (design 4.1).</summary>
    public StorageLocation Storage => Number is 7 or 14
        ? StorageLocation.Operator
        : Level switch
        {
            AttributeLevel.PassportSystem => StorageLocation.System,
            AttributeLevel.Pack => StorageLocation.Pack,
            AttributeLevel.Batch => StorageLocation.Batch,
            AttributeLevel.Site => StorageLocation.Site,
            AttributeLevel.Model => StorageLocation.Model,
            AttributeLevel.ModelRestatedPerPack => StorageLocation.ModelWithPackOverride,
            _ => throw new InvalidOperationException($"Unknown level {Level}."),
        };
}
```

- [ ] **Step 4: Run the test to see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeDefinitionTests"`
Expected: `Passed!  - Failed:     0, Passed:    10`.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Catalog/AttributeDefinition.cs BatteryPassWeb.Tests/Dpp/AttributeDefinitionTests.cs
git commit -m "feat(dpp): add attribute catalogue types"
```


---

### Task 3: Extract of the V3 file

The compliance test (Task 4) compares the catalogue with this extract. The script reads the workbook directly with the standard library, stops if the header layout changes, and writes ASCII-escaped JSON so special characters (°C, curly quotes) survive any tool.

**Files:**
- Create: `scripts/dpp/extract_longlist.py`
- Create (generated): `BatteryPassWeb.Tests/Dpp/dpp-v3-longlist-extract.json`

- [ ] **Step 1: Write the extract script**

`scripts/dpp/extract_longlist.py`:

```python
"""Extract the attribute rows of the DPP V3 longlist workbook into JSON for the compliance test.

Usage:
    python -I scripts/dpp/extract_longlist.py <V3 workbook .xlsx> BatteryPassWeb.Tests/Dpp/dpp-v3-longlist-extract.json

Standard library only. Reads the tab "Data attribute longlist_v2.0" and stops with an error if the
header layout differs from the one this script was written for, so a new file version can't slip
through unnoticed. Re-run it whenever the file changes, then run the compliance test.
"""
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

SHEET = "Data attribute longlist_v2.0"
MAIN = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
DOC_REL = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
PKG_REL = "{http://schemas.openxmlformats.org/package/2006/relationships}"

# Column letter -> (key in the extract, expected start of the column's header text).
COLUMNS = {
    "B": ("no", "#"),
    "F": ("oi", "Other Industrial"),
    "J": ("feb27", "Mandatory from Feb 2027"),
    "M": ("attribute", "Attribute"),
    "U": ("unit", "Unit of attribute"),
    "V": ("format", "Data format"),
    "W": ("access", "Access rights"),
    "X": ("staticDynamic", "Data behavioural characteristic"),
    "Y": ("updateRequirement", "Update requirement"),
    "Z": ("granularity", "Granularity level I"),
}


def shared_strings(book):
    if "xl/sharedStrings.xml" not in book.namelist():
        return []
    strings = []
    for item in ET.fromstring(book.read("xl/sharedStrings.xml")).findall(f"{MAIN}si"):
        parts = []
        for child in item:  # plain text or rich-text runs; phonetic runs (rPh) are skipped
            if child.tag == f"{MAIN}t":
                parts.append(child.text or "")
            elif child.tag == f"{MAIN}r":
                parts.extend(t.text or "" for t in child.findall(f"{MAIN}t"))
        strings.append("".join(parts))
    return strings


def sheet_path(book, name):
    workbook = ET.fromstring(book.read("xl/workbook.xml"))
    ids = [s.get(f"{DOC_REL}id") for s in workbook.iter(f"{MAIN}sheet") if s.get("name") == name]
    if not ids:
        raise SystemExit(f"tab {name!r} not found")
    rels = ET.fromstring(book.read("xl/_rels/workbook.xml.rels"))
    target = next(r.get("Target") for r in rels.iter(f"{PKG_REL}Relationship") if r.get("Id") == ids[0])
    return target.lstrip("/") if target.startswith("/") else "xl/" + target


def sheet_rows(book, path, strings):
    for row in ET.fromstring(book.read(path)).iter(f"{MAIN}row"):
        cells = {}
        for cell in row.findall(f"{MAIN}c"):
            column = re.match(r"[A-Z]+", cell.get("r")).group(0)
            value = cell.find(f"{MAIN}v")
            kind = cell.get("t")
            if kind == "s":
                text = strings[int(value.text)] if value is not None else ""
            elif kind == "inlineStr":
                text = "".join(t.text or "" for t in cell.iter(f"{MAIN}t"))
            else:
                text = value.text if value is not None and value.text else ""
            cells[column] = text
        yield cells


def main(xlsx, out):
    data = Path(xlsx).read_bytes()
    with zipfile.ZipFile(xlsx) as book:
        rows = list(sheet_rows(book, sheet_path(book, SHEET), shared_strings(book)))
    header = next((cells for cells in rows if cells.get("B", "").strip() == "#"), None)
    if header is None:
        raise SystemExit("header row ('#' in column B) not found")
    for column, (_, expected) in COLUMNS.items():
        found = header.get(column, "").strip()
        if not found.startswith(expected):
            raise SystemExit(f"column {column}: expected a header starting {expected!r}, found {found!r}")
    records = []
    for cells in rows:
        number = cells.get("B", "").strip()
        if not re.fullmatch(r"\d+(\.0)?", number):
            continue
        record = {key: cells.get(column, "").strip() for column, (key, _) in COLUMNS.items()}
        record["no"] = int(float(number))
        records.append(record)
    records.sort(key=lambda r: r["no"])
    document = {
        "source": {
            "file": Path(xlsx).name,
            "sha256": hashlib.sha256(data).hexdigest(),
            "sheet": SHEET,
            "extractedBy": "scripts/dpp/extract_longlist.py",
        },
        "rows": records,
    }
    Path(out).write_text(json.dumps(document, indent=1, ensure_ascii=True) + "\n", encoding="utf-8")
    print(f"{len(records)} rows written to {out}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
```

- [ ] **Step 2: Run it on the V3 file**

```bash
python -I scripts/dpp/extract_longlist.py "/c/Users/KassemAlHarakeh/Downloads/DPP_Internal_2026_BatteryPass-Ready_DataAttributeLongList_v2.0_August version_V3.xlsx" BatteryPassWeb.Tests/Dpp/dpp-v3-longlist-extract.json
```

Expected: `73 rows written to BatteryPassWeb.Tests/Dpp/dpp-v3-longlist-extract.json`.

- [ ] **Step 3: Check the extract**

```bash
python -I -c "import json; d = json.load(open('BatteryPassWeb.Tests/Dpp/dpp-v3-longlist-extract.json', encoding='utf-8')); r = d['rows']; print(d['source']['sha256'], len(r), r[0]['no'], r[-1]['no'], r[0]['attribute'], [x['no'] for x in r if '\u00b0' in x['unit']])"
```

Expected: `f13b5d5cf60933f00ce280dfd4c88ed58249b9b1c33faf5d0b3d8f52e0d7ad3d 73 1 92 DPP Schema version [87, 88, 89]`. A different hash means a different workbook: stop and ask.

- [ ] **Step 4: Commit**

```bash
git add scripts/dpp/extract_longlist.py BatteryPassWeb.Tests/Dpp/dpp-v3-longlist-extract.json
git commit -m "feat(dpp): extract the V3 attribute list for the compliance test"
```


---

### Task 4: The attribute catalogue

Three test classes pin the catalogue: to the design's counts and rules, to the file (via the extract, failing on any wording it doesn't recognise), and to the schema (property names and required lists of both schema files).

**Files:**
- Create: `web/Dpp/Catalog/AttributeCatalog.cs`
- Test: `BatteryPassWeb.Tests/Dpp/AttributeCatalogTests.cs`, `BatteryPassWeb.Tests/Dpp/LongListComplianceTests.cs`, `BatteryPassWeb.Tests/Dpp/SchemaCoverageTests.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/AttributeCatalogTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Checks the catalogue against the design's own counts and rules (design sections 4–6).</summary>
public sealed class AttributeCatalogTests
{
    [Fact]
    public void Catalogue_ShouldHave73UniqueAttributes()
    {
        Assert.Equal(73, AttributeCatalog.All.Count);
        Assert.Equal(73, AttributeCatalog.All.Select(a => a.Number).Distinct().Count());
        Assert.Equal(73, AttributeCatalog.All.Select(a => a.Property).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Audiences_ShouldMatchTheDesign()
    {
        var counts = AttributeCatalog.All.GroupBy(a => a.Audience).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(54, counts[Audience.Public]);
        Assert.Equal(10, counts[Audience.Purchaser]);
        Assert.Equal(5, counts[Audience.Shared]);
        Assert.Equal(4, counts[Audience.Authorities]);
    }

    [Fact]
    public void Levels_ShouldMatchTheDesign()
    {
        var counts = AttributeCatalog.All.GroupBy(a => a.Level).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(5, counts[AttributeLevel.PassportSystem]);
        Assert.Equal(12, counts[AttributeLevel.Pack]);
        Assert.Equal(16, counts[AttributeLevel.Batch]);
        Assert.Equal(3, counts[AttributeLevel.Site]);
        Assert.Equal(32, counts[AttributeLevel.Model]);
        Assert.Equal(5, counts[AttributeLevel.ModelRestatedPerPack]);
    }

    [Theory]
    [InlineData(43, LegitimateInterestGroups.Repair | LegitimateInterestGroups.Recycling)]
    [InlineData(45, LegitimateInterestGroups.Repair | LegitimateInterestGroups.Recycling)]
    [InlineData(46, LegitimateInterestGroups.Repair)]
    [InlineData(47, LegitimateInterestGroups.Repair)]
    [InlineData(48, LegitimateInterestGroups.Repair | LegitimateInterestGroups.Recycling | LegitimateInterestGroups.Treatment)]
    public void SharedAttributes_ShouldNameTheirLegitimateInterestRoles(int number, LegitimateInterestGroups expected)
    {
        Assert.Equal(expected, AttributeCatalog.ByNumber(number).SharedWith);
    }

    [Fact]
    public void OnlySharedAttributes_ShouldNameLegitimateInterestRoles()
    {
        Assert.All(AttributeCatalog.All.Where(a => a.Audience != Audience.Shared), a => Assert.Equal(LegitimateInterestGroups.None, a.SharedWith));
    }

    [Fact]
    public void ClusterAdmins_ShouldEditExactlyThePurchaserData()
    {
        Assert.All(AttributeCatalog.All, a => Assert.Equal(
            a.Audience == Audience.Purchaser,
            a.Editors.HasFlag(AttributeEditors.BatteryClusterAdmins)));
    }

    [Fact]
    public void SharedAttributes_ShouldBeEditedByTheTeamOnly()
    {
        Assert.All(AttributeCatalog.All.Where(a => a.Audience == Audience.Shared), a => Assert.Equal(AttributeEditors.Team, a.Editors));
    }

    [Fact]
    public void TheBatterySystemFeed_ShouldWriteOnlyStateOfChargeCyclesAndTemperature()
    {
        var fed = AttributeCatalog.All.Where(a => a.Editors.HasFlag(AttributeEditors.BatterySystemFeed)).Select(a => a.Number);

        Assert.Equal([63, 83, 87], fed);
    }

    [Fact]
    public void TheLiveSet_ShouldBeStateOfChargeCyclesTemperatureAndAccidents()
    {
        var live = AttributeCatalog.All.Where(a => a.Change == ChangeRule.Live).Select(a => a.Number);

        Assert.Equal([63, 83, 87, 92], live);
    }

    [Theory]
    [InlineData(7, StorageLocation.Operator)]
    [InlineData(14, StorageLocation.Operator)]
    [InlineData(11, StorageLocation.Pack)]
    [InlineData(59, StorageLocation.ModelWithPackOverride)]
    [InlineData(10, StorageLocation.Batch)]
    [InlineData(1, StorageLocation.System)]
    public void Storage_ShouldFollowTheDataModel(int number, StorageLocation expected)
    {
        Assert.Equal(expected, AttributeCatalog.ByNumber(number).Storage);
    }

    [Fact]
    public void Lookups_ShouldFindByNumberAndByProperty()
    {
        Assert.Equal("stateOfChargeSoC", AttributeCatalog.ByNumber(63).Property);
        Assert.Equal(63, AttributeCatalog.FindByProperty("stateOfChargeSoC")?.Number);
        Assert.Null(AttributeCatalog.FindByProperty("remainingCapacity"));
        Assert.Throws<KeyNotFoundException>(() => AttributeCatalog.ByNumber(8));
    }
}
```

`BatteryPassWeb.Tests/Dpp/LongListComplianceTests.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Keeps the catalogue in step with the V3 file (design section 11). Expectations are derived from the file's own
/// wording, and wording the test doesn't recognise fails, so a new file version can't slip through. When the file
/// changes, regenerate the extract with scripts/dpp/extract_longlist.py and run this test.
/// </summary>
public sealed partial class LongListComplianceTests
{
    // Our classifications that the file doesn't state directly (design Appendix A; decisions 11 and 16).
    private static readonly int[] SystemAttributes = [1, 2, 3, 4, 5];
    private static readonly int[] RestatedAttributes = [59, 69, 74, 79, 81];

    private const string PurchaserAccess =
        "- persons with a legitimate interest: o the natural or legal person who has legally purchased the battery or third party on behalf) and - market surveillance authorities";
    private const string AuthoritiesAccess = "Notified bodies, market surveillance authorities and the Commission";

    private static readonly Lazy<IReadOnlyList<FileRow>> Rows = new(ReadRows);

    public static TheoryData<int> FileAttributeNumbers()
    {
        var data = new TheoryData<int>();
        foreach (var row in Rows.Value)
        {
            data.Add(row.No);
        }

        return data;
    }

    [Fact]
    public void Catalogue_ShouldHaveExactlyTheFilesAttributes()
    {
        Assert.Equal(73, Rows.Value.Count);
        Assert.Equal(Rows.Value.Select(r => r.No).Order(), AttributeCatalog.All.Select(a => a.Number).Order());
    }

    [Theory]
    [MemberData(nameof(FileAttributeNumbers))]
    public void Attribute_ShouldMatchTheFile(int number)
    {
        var row = Rows.Value.Single(r => r.No == number);
        var attribute = AttributeCatalog.ByNumber(number);

        Assert.Equal(Normalize(row.Attribute), attribute.Name);
        Assert.Equal(ExpectedAudience(row.Access), attribute.Audience);
        Assert.Equal(ExpectedSharedWith(row.Access), attribute.SharedWith);
        Assert.Equal(ExpectedLevel(number, row.Granularity), attribute.Level);
        Assert.Equal(ExpectedChange(number, row.StaticDynamic, row.UpdateRequirement), attribute.Change);
        Assert.Equal(ExpectedRequirement(row.Oi), attribute.Requirement);
        Assert.Equal(ExpectedFeb2027(row.Feb27), attribute.MandatoryFromFeb2027);
        Assert.Equal(ExpectedUnit(row.Unit), attribute.Unit);
    }

    private static Audience ExpectedAudience(string access)
    {
        var text = Normalize(access);
        if (text == "Public")
        {
            return Audience.Public;
        }

        if (text == AuthoritiesAccess)
        {
            return Audience.Authorities;
        }

        if (text == PurchaserAccess)
        {
            return Audience.Purchaser;
        }

        var lower = text.ToLowerInvariant();
        if (lower.StartsWith("- persons with a legitimate interest:", StringComparison.Ordinal)
            && lower.Contains("legally purchased the battery", StringComparison.Ordinal)
            && lower.Contains("the commission", StringComparison.Ordinal)
            && lower.Contains("market surveillance authorities", StringComparison.Ordinal)
            && ExpectedSharedWith(access) != LegitimateInterestGroups.None)
        {
            return Audience.Shared;
        }

        throw new InvalidDataException($"Unrecognised access wording: {text}");
    }

    private static LegitimateInterestGroups ExpectedSharedWith(string access)
    {
        var lower = Normalize(access).ToLowerInvariant();
        var groups = LegitimateInterestGroups.None;
        if (lower.Contains("battery repair, repurposing and remanufacturing operators", StringComparison.Ordinal))
        {
            groups |= LegitimateInterestGroups.Repair;
        }

        if (lower.Contains("recyclers, battery treatment operators", StringComparison.Ordinal))
        {
            groups |= LegitimateInterestGroups.Recycling;
        }

        if (lower.Contains("authorized treatment facilities", StringComparison.Ordinal))
        {
            groups |= LegitimateInterestGroups.Treatment;
        }

        return groups;
    }

    private static AttributeLevel ExpectedLevel(int number, string granularity)
    {
        var text = Normalize(granularity);
        if (SystemAttributes.Contains(number))
        {
            return AttributeLevel.PassportSystem;
        }

        if (RestatedAttributes.Contains(number))
        {
            // Decision 11: a model value, re-stated per pack at a status change.
            return text == "Battery model"
                ? AttributeLevel.ModelRestatedPerPack
                : throw new InvalidDataException($"#{number}: re-stated attributes must be model-level in the file, found '{text}'");
        }

        return text switch
        {
            "Battery model" => AttributeLevel.Model,
            "Individual battery" => AttributeLevel.Pack,
            "Battery model per calendar year and per manufacturing site, i.e. batch level" => AttributeLevel.Batch,
            "Battery model per manufacturing site" => AttributeLevel.Site,
            _ => throw new InvalidDataException($"#{number}: unrecognised granularity '{text}'"),
        };
    }

    private static ChangeRule ExpectedChange(int number, string staticDynamic, string updateRequirement)
    {
        if (RestatedAttributes.Contains(number))
        {
            return ChangeRule.RestatedOnStatusChange;
        }

        if (number == 2)
        {
            // The file's DIN note on #2 defines the DPP status lifecycle (design 4.2).
            return ChangeRule.Lifecycle;
        }

        var kind = Normalize(staticDynamic);
        var update = Normalize(updateRequirement);
        return (kind, update) switch
        {
            ("Static", "n.a.") => ChangeRule.Fixed,
            ("Static", "After change of service provider") => ChangeRule.IfProviderChanges,
            ("Dynamic", "upon any update of the battery passport") => ChangeRule.OnPublishedOrLiveUpdate,
            ("Dynamic", "upon any update of the battery passport that includes archiving") => ChangeRule.NewPerVersion,
            ("Dynamic", "upon placement on market and battery status change") => ChangeRule.OnStatusChange,
            ("Dynamic", _) when update.StartsWith("depending on individual definition of", StringComparison.Ordinal) => ChangeRule.Live,
            _ => throw new InvalidDataException($"#{number}: unrecognised update rule '{kind}' / '{update}'"),
        };
    }

    private static Requirement ExpectedRequirement(string oi) => Normalize(oi) switch
    {
        "M" => Requirement.Mandatory,
        "(M)" => Requirement.MandatoryEspr,
        "C" => Requirement.Conditional,
        var other => throw new InvalidDataException($"Unrecognised requirement '{other}'"),
    };

    private static bool ExpectedFeb2027(string feb27) => Normalize(feb27) switch
    {
        "yes" => true,
        "no" => false,
        var other => throw new InvalidDataException($"Unrecognised Feb 2027 value '{other}'"),
    };

    private static string? ExpectedUnit(string unit) => Normalize(unit) switch
    {
        "" or "n.a." => null,
        "Ah or mAh (for LMT)" => "Ah", // Other industrial batteries use Ah.
        "g or kg" => "kg", // The app stores kg.
        var other => other,
    };

    private static string Normalize(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private static IReadOnlyList<FileRow> ReadRows()
    {
        var path = DppTestFiles.RepoFile("BatteryPassWeb.Tests", "Dpp", "dpp-v3-longlist-extract.json");
        var extract = JsonSerializer.Deserialize<ExtractFile>(File.ReadAllText(path), JsonSerializerOptions.Web)
            ?? throw new InvalidDataException("The extract is empty.");
        return extract.Rows;
    }

    private sealed record ExtractFile(IReadOnlyList<FileRow> Rows);

    private sealed record FileRow(
        int No,
        string Attribute,
        string Oi,
        string Feb27,
        string Unit,
        string Format,
        string Access,
        string StaticDynamic,
        string UpdateRequirement,
        string Granularity);
}
```

`BatteryPassWeb.Tests/Dpp/SchemaCoverageTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Checks the catalogue against the official schemas (design Appendix B).</summary>
public sealed class SchemaCoverageTests
{
    private static readonly OfficialSchemas Schemas = OfficialSchemas.Load();

    private static readonly string[] Sections =
    [
        AttributeDefinition.HeaderSection,
        "batteryCarbonFootprint",
        "batteryMaterialsAndComposition",
        "circularityAndResourceEfficiency",
        "identifiersAndProductData",
        "performanceAndDurability",
        "supplyChainDueDiligence",
        "symbolsLabelsAndDocumentationOfConformity",
    ];

    // Design Appendix B.4: optional in the schema and not in the file, so the app leaves them out.
    private static readonly string[] LeftOut =
    [
        "dppHeader.contentSpecificationIds",
        "dppHeader.facilityId",
        "batteryCarbonFootprint.absoluteBatteryCarbonFootprint",
        "identifiersAndProductData.dateOfPuttingTheBatteryIntoService",
        "performanceAndDurability.remainingCapacity",
        "performanceAndDurability.remainingPowerCapability",
        "performanceAndDurability.numberOfDeepDischargeEvents",
        "performanceAndDurability.numberOfOverchargeEvents",
        "supplyChainDueDiligence.thirdPartyAssurancesOfRecognisedSchemes",
        "supplyChainDueDiligence.supplyChainIndices",
    ];

    public static TheoryData<string> SectionNames() => new(Sections);

    [Fact]
    public void EveryCatalogueAttribute_ShouldExistInTheFullSchema()
    {
        Assert.All(AttributeCatalog.All, a => Assert.NotNull(Schemas.FindProperty(a.Section, a.Property)));
    }

    [Fact]
    public void SchemaProperties_ShouldBeTheCatalogueOrTheTenLeftOut()
    {
        var inSchema = Sections.SelectMany(section =>
            Schemas.FullDocument["$defs"]![section]!["properties"]!.AsObject().Select(p => $"{section}.{p.Key}"));
        var known = AttributeCatalog.All.Select(a => $"{a.Section}.{a.Property}").Concat(LeftOut);

        Assert.Equal(known.Order(StringComparer.Ordinal), inSchema.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void LeftOutProperties_ShouldBeOptionalInTheSchema()
    {
        foreach (var path in LeftOut)
        {
            var (section, property) = (path[..path.IndexOf('.')], path[(path.IndexOf('.') + 1)..]);
            Assert.DoesNotContain(property, OfficialSchemas.RequiredProperties(Schemas.FullDocument, section));
        }
    }

    [Theory]
    [MemberData(nameof(SectionNames))]
    public void FullSchemaRequired_ShouldBeTheMandatoryAttributes(string section)
    {
        var mandatory = AttributeCatalog.All
            .Where(a => a.Section == section && a.Requirement != Requirement.Conditional)
            .Select(a => a.Property);

        Assert.Equal(
            mandatory.Order(StringComparer.Ordinal),
            OfficialSchemas.RequiredProperties(Schemas.FullDocument, section).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SectionNames))]
    public void Feb2027SchemaRequired_ShouldBeTheMandatoryAttributesDueByFeb2027(string section)
    {
        var dueByFeb2027 = AttributeCatalog.All
            .Where(a => a.Section == section && a.Requirement != Requirement.Conditional && a.MandatoryFromFeb2027)
            .Select(a => a.Property);

        Assert.Equal(
            dueByFeb2027.Order(StringComparer.Ordinal),
            OfficialSchemas.RequiredProperties(Schemas.Feb2027Document, section).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Feb2027Schema_ShouldRequireTheSectionsThatHaveFeb2027Attributes()
    {
        var expected = AttributeCatalog.All
            .Where(a => a.Section != AttributeDefinition.HeaderSection && a.MandatoryFromFeb2027)
            .Select(a => a.Section)
            .Distinct();
        var required = OfficialSchemas.RequiredProperties(Schemas.Feb2027Document, "batteryPassportMaster");

        Assert.Equal(expected.Order(StringComparer.Ordinal), required.Order(StringComparer.Ordinal));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AttributeCatalogTests|FullyQualifiedName~LongListComplianceTests|FullyQualifiedName~SchemaCoverageTests"`
Expected: the build fails because `AttributeCatalog` isn't defined yet (a CS0103 error).

- [ ] **Step 3: Write the catalogue**

The entries were generated from the extract and the schema's property names, and match design Appendix A. `web/Dpp/Catalog/AttributeCatalog.cs`:

```csharp
using A = BatteryPassWeb.Dpp.Catalog.Audience;
using C = BatteryPassWeb.Dpp.Catalog.ChangeRule;
using E = BatteryPassWeb.Dpp.Catalog.AttributeEditors;
using G = BatteryPassWeb.Dpp.Catalog.LegitimateInterestGroups;
using L = BatteryPassWeb.Dpp.Catalog.AttributeLevel;
using R = BatteryPassWeb.Dpp.Catalog.Requirement;

namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>
/// The 73 attributes of the V3 file (design Appendix A) with their v2.0.1 schema names.
/// LongListComplianceTests keeps this list in step with the file, SchemaCoverageTests with the schema.
/// </summary>
public static class AttributeCatalog
{
    private const string Hdr = AttributeDefinition.HeaderSection;
    private const string Ident = "identifiersAndProductData";
    private const string Carbon = "batteryCarbonFootprint";
    private const string Materials = "batteryMaterialsAndComposition";
    private const string Circularity = "circularityAndResourceEfficiency";
    private const string Performance = "performanceAndDurability";
    private const string DueDiligence = "supplyChainDueDiligence";
    private const string Symbols = "symbolsLabelsAndDocumentationOfConformity";

    /// <summary>Purchaser data: the team and the battery's cluster admins (role rule, decision 9).</summary>
    private const E PurchaserEditors = E.Team | E.BatteryClusterAdmins;

    /// <summary>Live values: also the battery-system feed (decision 8, J-10).</summary>
    private const E Live = E.BatterySystemFeed | E.Team | E.BatteryClusterAdmins;

    public static IReadOnlyList<AttributeDefinition> All { get; } =
    [
        new(1, "DPP Schema version", Hdr, "dppSchemaVersion", L.PassportSystem, A.Public, G.None, E.System, C.Fixed, null, R.MandatoryEspr, true),
        new(2, "DPP Status", Hdr, "dppStatus", L.PassportSystem, A.Public, G.None, E.System | E.Team | E.MarketSurveillance, C.Lifecycle, null, R.MandatoryEspr, true),
        new(3, "DPP Granularity", Hdr, "granularity", L.PassportSystem, A.Public, G.None, E.System, C.Fixed, null, R.MandatoryEspr, true),
        new(4, "Date-time of latest update of DPP", Hdr, "lastUpdated", L.PassportSystem, A.Public, G.None, E.System, C.OnPublishedOrLiveUpdate, null, R.MandatoryEspr, true),
        new(5, "Unique battery passport identifier / unique DPP identifier", Hdr, "digitalProductPassportId", L.PassportSystem, A.Public, G.None, E.System, C.NewPerVersion, null, R.Mandatory, true),
        new(6, "Unique battery identifier / unique product identifier", Hdr, "uniqueProductIdentifier", L.Pack, A.Public, G.None, E.System, C.Fixed, null, R.Mandatory, true),
        new(7, "Unique economic operator identifier", Hdr, "economicOperatorId", L.Pack, A.Authorities, G.None, E.Team, C.Fixed, null, R.MandatoryEspr, true),
        new(10, "Battery model identifier", Ident, "batteryModelIdentifier", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(11, "Battery serial number", Ident, "batterySerialNumber", L.Pack, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(12, "Unique manufacturer identifier", Ident, "uniqueManufacturerIdentifier", L.Site, A.Authorities, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(13, "Third party service provider", Ident, "thirdPartyServiceProvider", L.Pack, A.Authorities, G.None, E.Team, C.IfProviderChanges, null, R.MandatoryEspr, true),
        new(14, "Economic operator information", Ident, "economicOperatorInformation", L.Pack, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(15, "Manufacturer information", Ident, "manufacturerInformation", L.Site, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(16, "Manufacturing place", Ident, "manufacturingPlace", L.Site, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(17, "Manufacturing date", Ident, "manufacturingDate", L.Pack, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(19, "Warranty period of the battery", Ident, "warrantyPeriod", L.Pack, A.Public, G.None, E.Team, C.Fixed, null, R.Conditional, true),
        new(20, "Battery category", Ident, "batteryCategory", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(21, "Battery mass", Ident, "batteryMass", L.Model, A.Public, G.None, E.Team, C.Fixed, "kg", R.Mandatory, true),
        new(22, "Battery status", Ident, "batteryStatus", L.Pack, A.Purchaser, G.None, PurchaserEditors, C.OnStatusChange, null, R.Mandatory, true),
        new(23, "Separate collection symbol", Symbols, "separateCollectionSymbol", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(24, "Symbols for cadmium and lead", Symbols, "symbolsForCadmiumAndLead", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Conditional, true),
        new(25, "Carbon footprint label", Symbols, "carbonFootprintLabel", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, false),
        new(26, "Extinguishing agent", Symbols, "extinguishingAgent", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(27, "Meaning of labels and symbols", Symbols, "meaningOfLabelsAndSymbols", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(28, "EU declaration of conformity", Symbols, "euDeclarationOfConformity", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(29, "Results of test reports proving compliance", Symbols, "resultsOfTestReportsProvingCompliance", L.Batch, A.Authorities, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(30, "Battery carbon footprint per Functional Unit", Carbon, "batteryCarbonFootprintPerFunctionalUnit", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(31, "Contribution of raw material acquisition and pre-processing lifecycle stage", Carbon, "contributionOfRawMaterialAcquisitionAndPreProcessingLifecycleStage", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(32, "Contribution of main product production lifecycle stage", Carbon, "contributionOfMainProductProductionLifecycleStage", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(33, "Contribution of distribution lifecycle stage", Carbon, "contributionOfDistributionLifecycleStage", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(34, "Contribution of end of life and recycling lifecycle stage", Carbon, "contributionOfEndOfLifeAndRecyclingLifecycleStage", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(35, "Carbon footprint performance class", Carbon, "carbonFootprintPerformanceClass", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, false),
        new(36, "Web link to public carbon footprint study", Carbon, "webLinkToPublicCarbonFootprintStudy", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, false),
        new(38, "Information of due diligence report", DueDiligence, "informationOfDueDiligenceReport", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, false),
        new(41, "Battery chemistry", Materials, "batteryChemistry", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(42, "Critical raw materials", Materials, "criticalRawMaterials", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(43, "Materials used in cathode, anode and electrolyte", Materials, "materialsUsedInCathodeAnodeAndElectrolyte", L.Model, A.Shared, G.Repair | G.Recycling, E.Team, C.Fixed, null, R.Mandatory, true),
        new(44, "Hazardous substances and their impact", Materials, "hazardousSubstancesAndTheirImpact", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(45, "Dismantling information: Manuals for the removal and the disassembly of the battery pack", Circularity, "dismantlingInformation", L.Model, A.Shared, G.Repair | G.Recycling, E.Team, C.Fixed, null, R.Mandatory, true),
        new(46, "Part numbers for components", Circularity, "partNumbersForComponents", L.Model, A.Shared, G.Repair, E.Team, C.Fixed, null, R.Mandatory, true),
        new(47, "Information on sources of spare parts", Circularity, "informationOnSourcesOfSpareParts", L.Model, A.Shared, G.Repair, E.Team, C.Fixed, null, R.Mandatory, true),
        new(48, "Safety measures", Circularity, "safetyMeasures", L.Model, A.Shared, G.Repair | G.Recycling | G.Treatment, E.Team, C.Fixed, null, R.Mandatory, true),
        new(49, "Nickel recycled content share", Circularity, "nickelRecycledContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, false),
        new(50, "Cobalt recycled content share", Circularity, "cobaltRecycledContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, false),
        new(51, "Lithium recycled content share", Circularity, "lithiumRecycledContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, false),
        new(52, "Lead recycled content share", Circularity, "leadRecycledContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, false),
        new(53, "Renewable content share", Circularity, "renewableContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Mandatory, true),
        new(54, "Information on the role of end-users in contributing to waste prevention", Circularity, "informationOnTheRoleOfEndUsersInContributingToWastePrevention", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(55, "Information on the role of end-users in contributing to the separate collection of waste batteries", Circularity, "informationOnTheRoleOfEndUsersInContributingToTheSeparateCollectionOfWasteBatteries", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(56, "Information on battery collection, preparation for second life and on treatment at end of life", Circularity, "informationOnBatteryCollectionPreparationForSecondLifeAndOnTreatmentAtEndOfLife", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(57, "Rated capacity", Performance, "ratedCapacity", L.Model, A.Public, G.None, E.Team, C.Fixed, "Ah", R.Mandatory, false),
        new(59, "Capacity fade", Performance, "capacityFade", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "%", R.Mandatory, true),
        new(63, "State of Charge (SoC)", Performance, "stateOfChargeSoC", L.Pack, A.Purchaser, G.None, Live, C.Live, "%", R.Mandatory, true),
        new(64, "Minimum voltage", Performance, "minimumVoltage", L.Model, A.Public, G.None, E.Team, C.Fixed, "V", R.Mandatory, true),
        new(65, "Maximum voltage", Performance, "maximumVoltage", L.Model, A.Public, G.None, E.Team, C.Fixed, "V", R.Mandatory, true),
        new(66, "Nominal voltage", Performance, "nominalVoltage", L.Model, A.Public, G.None, E.Team, C.Fixed, "V", R.Mandatory, true),
        new(67, "Original power capability", Performance, "originalPowerCapability", L.Model, A.Public, G.None, E.Team, C.Fixed, "W", R.Mandatory, true),
        new(69, "Power fade", Performance, "powerFade", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "%", R.Mandatory, true),
        new(70, "Maximum permitted battery power", Performance, "maximumPermittedBatteryPower", L.Model, A.Public, G.None, E.Team, C.Fixed, "W", R.Mandatory, true),
        new(71, "Initial round trip energy efficiency", Performance, "initialRoundTripEnergyEfficiency", L.Model, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, true),
        new(72, "Round trip energy efficiency at 50% of cycle life", Performance, "roundTripEnergyEfficiencyAt50PerCentOfCycleLife", L.Model, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, true),
        new(74, "Energy round trip efficiency fade", Performance, "energyRoundTripEfficiencyFade", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "%", R.Conditional, true),
        new(78, "Initial internal resistance of battery cell and pack (module recommended)", Performance, "initialInternalResistanceOfBatteryCellAndPackModuleRecommended", L.Model, A.Public, G.None, E.Team, C.Fixed, "Ohm", R.Mandatory, true),
        new(79, "Internal resistance increase of pack (cell and module recommended)", Performance, "internalResistanceIncreaseOfPackCellAndModuleRecommended", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "%", R.Mandatory, true),
        new(81, "Expected lifetime in calendar years", Performance, "expectedLifetimeInCalendarYears", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "years", R.Mandatory, true),
        new(82, "Expected lifetime - Number of charge-discharge cycles", Performance, "expectedLifetimeNumberOfChargeDischargeCycles", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Conditional, true),
        new(83, "Number of full charging and discharging cycles", Performance, "numberOfFullChargingAndDischargingCycles", L.Pack, A.Purchaser, G.None, Live, C.Live, null, R.Conditional, true),
        new(84, "Cycle-life reference test", Performance, "cycleLifeReferenceTest", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Conditional, true),
        new(85, "C-rate of relevant cycle-life test", Performance, "cRateOfRelevantCycleLifeTest", L.Model, A.Public, G.None, E.Team, C.Fixed, "A/Ah", R.Conditional, true),
        new(87, "Temperature information", Performance, "temperatureInformation", L.Pack, A.Purchaser, G.None, Live, C.Live, "°C", R.Mandatory, true),
        new(88, "Temperature range idle state, lower boundary", Performance, "temperatureRangeIdleStateLowerBoundary", L.Model, A.Public, G.None, E.Team, C.Fixed, "°C", R.Mandatory, true),
        new(89, "Temperature range idle state, upper boundary", Performance, "temperatureRangeIdleStateUpperBoundary", L.Model, A.Public, G.None, E.Team, C.Fixed, "°C", R.Mandatory, true),
        new(92, "Information on accidents", Performance, "informationOnAccidents", L.Pack, A.Purchaser, G.None, PurchaserEditors, C.Live, null, R.Mandatory, true),
    ];

    private static readonly Dictionary<int, AttributeDefinition> ByNumberIndex = All.ToDictionary(a => a.Number);
    private static readonly Dictionary<string, AttributeDefinition> ByPropertyIndex = All.ToDictionary(a => a.Property, StringComparer.Ordinal);

    public static AttributeDefinition ByNumber(int number) =>
        ByNumberIndex.TryGetValue(number, out var attribute)
            ? attribute
            : throw new KeyNotFoundException($"The catalogue has no attribute #{number}.");

    public static AttributeDefinition? FindByProperty(string property) => ByPropertyIndex.GetValueOrDefault(property);
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AttributeCatalogTests|FullyQualifiedName~LongListComplianceTests|FullyQualifiedName~SchemaCoverageTests"`
Expected: `Passed!  - Failed:     0, Passed:   114` (20 + 74 + 20).

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Catalog/AttributeCatalog.cs BatteryPassWeb.Tests/Dpp/AttributeCatalogTests.cs BatteryPassWeb.Tests/Dpp/LongListComplianceTests.cs BatteryPassWeb.Tests/Dpp/SchemaCoverageTests.cs
git commit -m "feat(dpp): add the 73-attribute catalogue with compliance and schema tests"
```


---

### Task 5: Stored values and records

**Files:**
- Create: `web/Dpp/Values/AttributeValue.cs`, `web/Dpp/Values/JsonNodeBsonSerializer.cs`, `web/Dpp/Store/DppRecords.cs`
- Test: `BatteryPassWeb.Tests/Dpp/AttributeValueStorageTests.cs`

- [ ] **Step 1: Write the failing test**

`BatteryPassWeb.Tests/Dpp/AttributeValueStorageTests.cs`:

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

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueStorageTests"`
Expected: the build fails because the `BatteryPassWeb.Dpp.Values` and `BatteryPassWeb.Dpp.Store` types aren't defined yet (CS0234 or CS0246 errors).

- [ ] **Step 3: Write the stored value and its BSON serializer**

`web/Dpp/Values/AttributeValue.cs`:

```csharp
using System.Text.Json.Nodes;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// One stored value in the schema's own JSON form (units included), with when the information was generated,
/// where it came from and who sent it (design 4.1).
/// </summary>
public sealed class AttributeValue
{
    [BsonSerializer(typeof(JsonNodeBsonSerializer))]
    public required JsonNode Value { get; init; }

    /// <summary>Measurement time for live values; entry time otherwise.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime GeneratedAtUtc { get; init; }

    /// <summary>"form", "upload", "feed" or "seed".</summary>
    public required string Source { get; init; }

    public string? Who { get; init; }
}
```

`web/Dpp/Values/JsonNodeBsonSerializer.cs`:

```csharp
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// Stores a JSON value as native BSON (objects as documents, arrays as arrays), so stored values stay readable
/// and queryable in MongoDB, and reads it back as the same JSON.
/// </summary>
public sealed class JsonNodeBsonSerializer : SerializerBase<JsonNode?>
{
    private static readonly JsonWriterSettings RelaxedJson = new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

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

    public override JsonNode? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bson = BsonValueSerializer.Instance.Deserialize(context);
        if (bson.IsBsonNull)
        {
            return null;
        }

        var json = new BsonDocument("v", bson).ToJson(RelaxedJson);
        return JsonNode.Parse(json)!["v"]!.DeepClone();
    }
}
```

- [ ] **Step 4: Write the records**

`web/Dpp/Store/DppRecords.cs`:

```csharp
using BatteryPassWeb.Dpp.Values;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Store;

// The records of design section 4.1 that a passport is built from. Values are keyed by v2.0.1 property name
// and kept in the schema's own form. Repositories, passport versions and documents come in step 2.

/// <summary>Groups battery models. Holds no passport data.</summary>
public sealed class BatteryFamily
{
    [BsonId]
    public required string Id { get; init; }

    public required string Name { get; set; }
}

/// <summary>The 32 model attributes, plus the starting values of the 5 re-stated ones (#59, #69, #74, #79, #81).</summary>
public sealed class BatteryModel
{
    [BsonId]
    public required string Id { get; init; }

    public required string FamilyId { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>Manufacturer identifier and information, and the manufacturing place (#12, #15, #16).</summary>
public sealed class ManufacturingSite
{
    [BsonId]
    public required string Id { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>One model × calendar year × manufacturing site (decision 3), holding the 16 batch attributes.</summary>
public sealed class ProductionBatch
{
    [BsonId]
    public required string Id { get; init; }

    public required string ModelId { get; init; }

    public required string SiteId { get; init; }

    public required int Year { get; init; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>An economic operator's identifier and information (#7, #14), entered once and linked from packs.</summary>
public sealed class EconomicOperator
{
    [BsonId]
    public required string Id { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>One battery pack, the subject of one passport (decision 16).</summary>
public sealed class BatteryPack
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

- [ ] **Step 5: Run the test to see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueStorageTests"`
Expected: `Passed!  - Failed:     0, Passed:     9`.

- [ ] **Step 6: Commit**

```bash
git add web/Dpp/Values/AttributeValue.cs web/Dpp/Values/JsonNodeBsonSerializer.cs web/Dpp/Store/DppRecords.cs BatteryPassWeb.Tests/Dpp/AttributeValueStorageTests.cs
git commit -m "feat(dpp): add stored values and the passport source records"
```


---

### Task 6: Value checks

**Files:**
- Create: `web/Dpp/Values/AttributeValueValidator.cs`
- Test: `BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs`

- [ ] **Step 1: Write the failing test**

`BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class AttributeValueValidatorTests
{
    private static readonly AttributeValueValidator Validator = new(OfficialSchemas.Load());

    [Theory]
    [InlineData(59, """{"percentUnit":"%","percentageValue":12}""")]
    [InlineData(17, "\"2026-10\"")]
    [InlineData(19, "\"P8Y\"")]
    [InlineData(19, "\"P3Y6M\"")]
    [InlineData(6, "\"urn:test:battery:VPC8C05-6788\"")]
    [InlineData(4, "\"2026-10-28T09:30:00Z\"")]
    [InlineData(36, "\"https://www.example.com/carbon-footprint-study\"")]
    [InlineData(57, """{"amperehourMiliamperehourValue":200,"ampereHourMiliamperehourUnit":"Ah"}""")]
    [InlineData(21, """{"gramKilogramUnit":"kg","gramKilogramValue":512.5}""")]
    [InlineData(22, """{"batteryStatusValues":"re-used"}""")]
    public void Validate_ShouldAcceptValidValues(int number, string json)
    {
        Assert.Empty(Validator.Validate(AttributeCatalog.ByNumber(number), JsonNode.Parse(json)));
    }

    [Theory]
    [InlineData(59, """{"percentUnit":"%","percentageValue":120}""", "maximum")] // schema range
    [InlineData(59, "12", "type")] // bare values are turned into the schema form before this check (step 2)
    [InlineData(22, """{"batteryStatusValues":"repaired"}""", "enum")] // decision 19: no "repaired" status
    [InlineData(28, """{"contentType":"application/pdf","url":"ftp://example.com/doc.pdf"}""", "pattern")]
    [InlineData(17, "\"2026-10-14\"", "decision 18")] // year and month only
    [InlineData(17, "\"2026-13\"", "decision 18")]
    [InlineData(19, "\"8 years\"", "ISO 8601 period")]
    [InlineData(6, "\"not an identifier\"", "absolute URI")]
    [InlineData(4, "\"2026-10-28 09:30\"", "UTC timestamp")]
    [InlineData(57, """{"amperehourMiliamperehourValue":200000,"ampereHourMiliamperehourUnit":"mAh"}""", "\"Ah\"")]
    [InlineData(21, """{"gramKilogramUnit":"g","gramKilogramValue":512500}""", "\"kg\"")]
    public void Validate_ShouldRejectInvalidValues(int number, string json, string expectedMessagePart)
    {
        var issues = Validator.Validate(AttributeCatalog.ByNumber(number), JsonNode.Parse(json));

        Assert.Contains(issues, issue => issue.Message.Contains(expectedMessagePart, StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueValidatorTests"`
Expected: the build fails because `AttributeValueValidator` isn't defined yet (a CS0246 error).

- [ ] **Step 3: Write the validator**

`web/Dpp/Values/AttributeValueValidator.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// Checks one value in the schema's own form: first against its property in the official schema (type, range,
/// allowed values, web-address pattern), then against the app's rules. The schema's "format" keywords are only
/// annotations, so this class checks them itself (design section 11, decision 18).
/// </summary>
public sealed partial class AttributeValueValidator(OfficialSchemas schemas)
{
    public IReadOnlyList<SchemaIssue> Validate(AttributeDefinition attribute, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        var issues = schemas.CheckProperty(attribute.Section, attribute.Property, value);
        return issues.Count > 0 ? issues : AppRuleIssues(attribute, value).ToArray();
    }

    private IEnumerable<SchemaIssue> AppRuleIssues(AttributeDefinition attribute, JsonNode? value)
    {
        var format = schemas.FindProperty(attribute.Section, attribute.Property)?["format"]?.GetValue<string>();
        if (format is not null && value?.GetValueKind() == JsonValueKind.String)
        {
            var text = value.GetValue<string>();
            var problem = format switch
            {
                "uri" => Uri.TryCreate(text, UriKind.Absolute, out _) ? null : "must be an absolute URI, e.g. urn:… or https://…",
                "date-time" => IsUtcTimestamp(text) ? null : "must be a UTC timestamp, e.g. 2026-10-28T09:30:00Z",
                "duration" => IsoPeriod().IsMatch(text) ? null : "must be an ISO 8601 period, e.g. P8Y or P3Y6M",
                "date" => YearMonth().IsMatch(text) ? null : "must be a year and month, e.g. 2026-10 (decision 18)",
                _ => null,
            };
            if (problem is not null)
            {
                yield return new SchemaIssue(string.Empty, $"format: {problem}");
            }
        }

        // The schema allows two units for these; the catalogue fixes one for this battery category.
        (string? UnitProperty, string? Unit) fixedUnit = attribute.Number switch
        {
            21 => ("gramKilogramUnit", "kg"),
            57 => ("ampereHourMiliamperehourUnit", "Ah"),
            _ => (null, null),
        };
        if (fixedUnit.UnitProperty is not null && value?[fixedUnit.UnitProperty]?.GetValue<string>() != fixedUnit.Unit)
        {
            yield return new SchemaIssue($"/{fixedUnit.UnitProperty}", $"unit: must be \"{fixedUnit.Unit}\" for #{attribute.Number}");
        }
    }

    private static bool IsUtcTimestamp(string text) => DateTime.TryParseExact(
        text,
        ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
        out _);

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex YearMonth();

    [GeneratedRegex(@"^P(?!$)(\d+Y)?(\d+M)?(\d+W)?(\d+D)?(T(?=\d)(\d+H)?(\d+M)?(\d+S)?)?$")]
    private static partial Regex IsoPeriod();
}
```

- [ ] **Step 4: Run the test to see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueValidatorTests"`
Expected: `Passed!  - Failed:     0, Passed:    21`.

- [ ] **Step 5: Commit**

```bash
git add web/Dpp/Values/AttributeValueValidator.cs BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs
git commit -m "feat(dpp): check single values against the schema and the app's format rules"
```


---

### Task 7: Test seed and passport assembler

The seed is one fully filled pack: every stored attribute has a value, and the pack re-states #59 (4 % instead of the model's 0 %). The tests prove the assembled passport passes both official schemas.

**Files:**
- Create: `web/Dpp/Assembly/PassportAssembler.cs`
- Test: `BatteryPassWeb.Tests/Dpp/seed-other-industrial.json`, `BatteryPassWeb.Tests/Dpp/DppTestSeed.cs`, `BatteryPassWeb.Tests/Dpp/PassportAssemblerTests.cs`

- [ ] **Step 1: Add the seed**

`BatteryPassWeb.Tests/Dpp/seed-other-industrial.json` (UTF-8; it contains `°C` and `ä`):

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
      "dismantlingInformation": { "contentType": "application/pdf", "url": "https://passport.example.com/documents/model-test/dismantling.pdf", "language": "en-GB", "resourceTitle": "Dismantling manual" },
      "partNumbersForComponents": { "contentType": "application/pdf", "url": "https://passport.example.com/documents/model-test/part-numbers.pdf", "language": "en-GB", "resourceTitle": "Part numbers for components" },
      "informationOnSourcesOfSpareParts": { "contentType": "application/pdf", "url": "https://passport.example.com/documents/model-test/spare-parts.pdf", "language": "en-GB", "resourceTitle": "Sources of spare parts" },
      "safetyMeasures": { "contentType": "application/pdf", "url": "https://passport.example.com/documents/model-test/safety-measures.pdf", "language": "en-GB", "resourceTitle": "Safety measures" },
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
      "resultsOfTestReportsProvingCompliance": { "contentType": "application/pdf", "url": "https://passport.example.com/documents/batch-test-2026/test-reports.pdf", "language": "en-GB", "resourceTitle": "Test reports" },
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
      "informationOnAccidents": { "contentType": "text/html", "url": "https://passport.example.com/packs/VPC8C05-6788/accidents", "language": "en-GB", "resourceTitle": "Accident information" }
    }
  }
}
```

- [ ] **Step 2: Add the seed loader**

`BatteryPassWeb.Tests/Dpp/DppTestSeed.cs`:

```csharp
using System.Globalization;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// One fully filled test pack (seed-other-industrial.json): every stored attribute has a value in the schema's own
/// form, and the pack re-states #59 (capacity fade 4 % instead of the model's 0 %). Each call returns fresh records.
/// </summary>
internal static class DppTestSeed
{
    public static readonly DateTime GeneratedAtUtc = new(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc);

    public static SeedPassport Load()
    {
        var root = JsonNode.Parse(File.ReadAllText(DppTestFiles.RepoFile("BatteryPassWeb.Tests", "Dpp", "seed-other-industrial.json")))!;
        var header = root["header"]!;
        var model = root["model"]!;
        var site = root["site"]!;
        var batch = root["batch"]!;
        var economicOperator = root["operator"]!;
        var pack = root["pack"]!;

        var sources = new PassportSources(
            new BatteryPack
            {
                Id = Text(pack, "id"),
                BatchId = Text(pack, "batchId"),
                OperatorId = Text(pack, "operatorId"),
                ClusterId = Text(pack, "clusterId"),
                Values = Values(pack),
            },
            new ProductionBatch
            {
                Id = Text(batch, "id"),
                ModelId = Text(batch, "modelId"),
                SiteId = Text(batch, "siteId"),
                Year = batch["year"]!.GetValue<int>(),
                Values = Values(batch),
            },
            new BatteryModel { Id = Text(model, "id"), FamilyId = Text(model, "familyId"), Name = Text(model, "name"), Values = Values(model) },
            new ManufacturingSite { Id = Text(site, "id"), Name = Text(site, "name"), Values = Values(site) },
            new EconomicOperator { Id = Text(economicOperator, "id"), Name = Text(economicOperator, "name"), Values = Values(economicOperator) });

        var passportHeader = new PassportHeader(
            Text(header, "digitalProductPassportId"),
            Text(header, "dppStatus"),
            DateTime.Parse(Text(header, "lastUpdatedUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));

        return new SeedPassport(sources, passportHeader);
    }

    private static string Text(JsonNode node, string name) => node[name]!.GetValue<string>();

    private static Dictionary<string, AttributeValue> Values(JsonNode record) =>
        record["values"]!.AsObject().ToDictionary(
            entry => entry.Key,
            entry => new AttributeValue { Value = entry.Value!.DeepClone(), GeneratedAtUtc = GeneratedAtUtc, Source = "seed" },
            StringComparer.Ordinal);
}

internal sealed record SeedPassport(PassportSources Sources, PassportHeader Header);
```

- [ ] **Step 3: Write the failing test**

`BatteryPassWeb.Tests/Dpp/PassportAssemblerTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class PassportAssemblerTests
{
    private static readonly OfficialSchemas Schemas = OfficialSchemas.Load();

    [Fact]
    public void Seed_ShouldHoldEveryStoredAttributeOnTheRightRecord()
    {
        var sources = DppTestSeed.Load().Sources;

        foreach (var attribute in AttributeCatalog.All.Where(a => a.Storage != StorageLocation.System))
        {
            var record = attribute.Storage switch
            {
                StorageLocation.Pack => sources.Pack.Values,
                StorageLocation.Operator => sources.Operator.Values,
                StorageLocation.Batch => sources.Batch.Values,
                StorageLocation.Site => sources.Site.Values,
                _ => sources.Model.Values,
            };
            Assert.True(record.ContainsKey(attribute.Property), $"#{attribute.Number} {attribute.Property} is missing from the seed");
        }
    }

    [Fact]
    public void EverySeedValue_ShouldPassTheValueChecks()
    {
        var validator = new AttributeValueValidator(Schemas);
        var sources = DppTestSeed.Load().Sources;
        var stored = new[] { sources.Pack.Values, sources.Operator.Values, sources.Batch.Values, sources.Site.Values, sources.Model.Values }
            .SelectMany(values => values);

        foreach (var (property, value) in stored)
        {
            var attribute = AttributeCatalog.FindByProperty(property);
            Assert.True(attribute is not null, $"{property} is not in the catalogue");
            Assert.Empty(validator.Validate(attribute, value.Value));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeedPassport_ShouldPassTheOfficialSchema(bool feb2027Variant)
    {
        var seed = DppTestSeed.Load();

        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);

        Assert.Empty(Schemas.CheckPassport(passport, feb2027Variant));
    }

    [Fact]
    public void Assemble_ShouldWriteTheSystemHeaderValues()
    {
        var seed = DppTestSeed.Load();

        var header = PassportAssembler.Assemble(seed.Sources, seed.Header)["dppHeader"]!;

        Assert.Equal(PassportAssembler.DppSchemaVersion, header["dppSchemaVersion"]!.GetValue<string>());
        Assert.Equal("active", header["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Equal("item", header["granularity"]!["dppGranularityValue"]!.GetValue<string>());
        Assert.Equal("2026-10-28T09:30:00Z", header["lastUpdated"]!.GetValue<string>());
        Assert.Equal("urn:test:dpp:VPC8C05-6788:v1", header["digitalProductPassportId"]!.GetValue<string>());
    }

    [Fact]
    public void Assemble_ShouldTakeTheOperatorValuesFromTheOperator()
    {
        var seed = DppTestSeed.Load();

        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);

        Assert.Equal("urn:test:operator:example-battery-systems", passport["dppHeader"]!["economicOperatorId"]!.GetValue<string>());
        Assert.Equal("Example Battery Systems AB", Section(passport, "identifiersAndProductData")["economicOperatorInformation"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Assemble_ShouldPreferThePacksRestatedValue()
    {
        var seed = DppTestSeed.Load();

        var performance = Section(PassportAssembler.Assemble(seed.Sources, seed.Header), "performanceAndDurability");

        Assert.Equal(4, performance["capacityFade"]!["percentageValue"]!.GetValue<int>());
        Assert.Equal(0, performance["powerFade"]!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public void Assemble_ShouldIgnoreAValueStoredOnTheWrongRecord()
    {
        var seed = DppTestSeed.Load();
        seed.Sources.Pack.Values["ratedCapacity"] = new AttributeValue
        {
            Value = JsonNode.Parse("""{"amperehourMiliamperehourValue":999,"ampereHourMiliamperehourUnit":"Ah"}""")!,
            GeneratedAtUtc = DppTestSeed.GeneratedAtUtc,
            Source = "seed",
        };

        var performance = Section(PassportAssembler.Assemble(seed.Sources, seed.Header), "performanceAndDurability");

        Assert.Equal(200, performance["ratedCapacity"]!["amperehourMiliamperehourValue"]!.GetValue<int>());
    }

    [Fact]
    public void Assemble_ShouldLeaveOutMissingValuesAndEmptySections()
    {
        var seed = DppTestSeed.Load();
        seed.Sources.Pack.Values.Remove("informationOnAccidents");
        seed.Sources.Model.Values.Remove("informationOfDueDiligenceReport");

        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);

        Assert.Null(Section(passport, "performanceAndDurability")["informationOnAccidents"]);
        Assert.Null(passport["batteryPassportData"]!["supplyChainDueDiligence"]);
        Assert.Contains(Schemas.CheckPassport(passport, feb2027Variant: true), issue => issue.Message.Contains("informationOnAccidents", StringComparison.Ordinal));
    }

    [Fact]
    public void Assemble_ShouldNotShareNodesWithTheStoredValues()
    {
        var seed = DppTestSeed.Load();

        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);
        Section(passport, "performanceAndDurability")["stateOfChargeSoC"]!["percentageValue"] = 1;

        Assert.Equal(76, seed.Sources.Pack.Values["stateOfChargeSoC"].Value["percentageValue"]!.GetValue<int>());
    }

    private static JsonNode Section(JsonObject passport, string section) => passport["batteryPassportData"]![section]!;
}
```

- [ ] **Step 4: Run it to see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportAssemblerTests"`
Expected: the build fails because the `BatteryPassWeb.Dpp.Assembly` types aren't defined yet (a CS0234 or CS0246 error).

- [ ] **Step 5: Write the assembler**

`web/Dpp/Assembly/PassportAssembler.cs`:

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
        4 => JsonValue.Create(ToUtc(header.LastUpdatedUtc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)),
        5 => JsonValue.Create(header.DigitalProductPassportId),
        _ => throw new InvalidOperationException($"#{number} is not a system attribute."),
    };

    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;

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

- [ ] **Step 6: Run the test to see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportAssemblerTests"`
Expected: `Passed!  - Failed:     0, Passed:    10`.

- [ ] **Step 7: Commit**

```bash
git add web/Dpp/Assembly/PassportAssembler.cs BatteryPassWeb.Tests/Dpp/seed-other-industrial.json BatteryPassWeb.Tests/Dpp/DppTestSeed.cs BatteryPassWeb.Tests/Dpp/PassportAssemblerTests.cs
git commit -m "feat(dpp): assemble v2.0.1 passports from the stored records"
```


---

### Task 8: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:   637, Skipped:     0, Total:   637` (464 existing + 173 new).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q`
Expected: `Build succeeded.` and `0 Warning(s)`.

- [ ] **Step 3: Check the published app embeds the schemas and ships no loose copies**

```bash
dotnet publish web/BatteryPassWeb.csproj -o "$TEMP/dpp-publish-check" --nologo -v q
find "$TEMP/dpp-publish-check" -iname "*Other_Industrial*" | wc -l
python -I -c "import sys; d = open(sys.argv[1], 'rb').read(); print(all(n in d for n in (b'Dpp.Schemas.Other_Industrial_Above_2kWh_2.0.1.json', b'Dpp.Schemas.Other_Industrial_Above_2kWh_Feb27_2.0.1.json')))" "$TEMP/dpp-publish-check/BatteryPassWeb.dll"
rm -rf "$TEMP/dpp-publish-check"
```

Expected: `0`, then `True`.

- [ ] **Step 4: Confirm everything is committed**

Run: `git status --short`
Expected: no step-1 files listed (the uncommitted docs from "Before you start" may still appear if the product owner chose not to commit them).

## After this plan

Step 2 (team editing and publication) builds on these pieces: repositories for the records, the save pipeline (which turns bare values into the schema form and runs `AttributeValueValidator`), the identifier service, documents, publication with `OfficialSchemas.CheckPassport`, signing of the static core and versions (design section 14).

