# Battery Pass Trust State And Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Phase 1 of the trust upgrade: trust state fields, schema registry, validation service, dirty tracking, validation endpoints, and a conformance dashboard skeleton.

**Architecture:** Add focused trust models and services under `web/Models/Trust` and `web/Services`, then wire them through `Program.cs`, `PassportRepository`, `AdminController`, and `PassportsApiController`. Validation starts with deterministic schema presence/shape checks plus business rules; later signing phases can depend on the same trust summary and dirty-state fields.

**Tech Stack:** ASP.NET Core MVC, C# 14, MongoDB.Bson, MongoDB.Driver, xUnit, Razor views.

---

## Scope Check

The approved design covers six subsystems: validation, signing/revisions/audit, secure files, QR, dashboard guidance, and test hardening. This plan implements only the first subsystem because it creates a working trust-state foundation and a safe handoff point for signing. Later phase plans should build on the service contracts and trust fields defined here.

## File Structure

- Create `web/Models/Trust/TrustModels.cs`: trust state constants, validation severities, validation issue/result/summary models, schema descriptor model.
- Create `web/Services/SchemaRegistryService.cs`: canonical aspect-to-schema mapping and schema file discovery.
- Create `web/Services/PassportValidationService.cs`: validation rules and trust summary generation.
- Modify `web/Services/PassportRepository.cs`: trust persistence helpers and canonical dirty-state update helper.
- Modify `web/Services/PassportViewModelFactory.cs`: map trust state into existing passport view model fields.
- Modify `web/Models/ViewModels/PassportViewModel.cs`: add trust summary fields for UI.
- Create `web/Models/ViewModels/ConformanceViewModel.cs`: conformance dashboard model.
- Modify `web/Controllers/AdminController.cs`: conformance route, validation action, dirty tracking after canonical admin saves.
- Modify `web/Controllers/PassportsApiController.cs`: validate endpoint for admin/API use.
- Modify `web/Controllers/ExternalApiController.cs`: do not dirty trust state for external writes; add explicit tests around current behavior.
- Modify `web/Program.cs`: register new services.
- Create `web/Views/Admin/Conformance.cshtml`: first conformance dashboard.
- Modify `web/Views/Admin/EditPassport.cshtml`: add conformance link and trust status banner.
- Modify `web/Views/Admin/Clusters.cshtml`: add conformance action link in passport table.
- Modify `web/Views/Passport/Summary.cshtml` and `web/Views/Passport/Detail.cshtml`: display trust state text using existing badge area.
- Create tests in `BatteryPassWeb.Tests/TrustModelsTests.cs`, `BatteryPassWeb.Tests/SchemaRegistryServiceTests.cs`, `BatteryPassWeb.Tests/PassportValidationServiceTests.cs`, `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`, and `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`.

## Tasks

### Task 1: Trust Models

**Files:**
- Create: `web/Models/Trust/TrustModels.cs`
- Test: `BatteryPassWeb.Tests/TrustModelsTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `BatteryPassWeb.Tests/TrustModelsTests.cs`:

```csharp
using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Tests;

public sealed class TrustModelsTests
{
    [Fact]
    public void ValidationSummary_ShouldCountBlockingErrorsAndWarnings()
    {
        var summary = new TrustValidationSummary
        {
            Sections =
            [
                new TrustValidationSectionResult
                {
                    SectionKey = "generalProductInformation",
                    SectionLabel = "General",
                    Issues =
                    [
                        new TrustValidationIssue(TrustValidationSeverity.BlockingError, "passportId", "Passport ID is required."),
                        new TrustValidationIssue(TrustValidationSeverity.Warning, "batteryImageUrl", "Battery image uses fallback.")
                    ]
                },
                new TrustValidationSectionResult
                {
                    SectionKey = "performanceAndDurability",
                    SectionLabel = "Performance",
                    Issues =
                    [
                        new TrustValidationIssue(TrustValidationSeverity.Passed, "ratedEnergy", "Rated energy is present.")
                    ]
                }
            ]
        };

        Assert.Equal(1, summary.BlockingErrorCount);
        Assert.Equal(1, summary.WarningCount);
        Assert.Equal(1, summary.PassedCount);
        Assert.False(summary.CanSign);
    }

    [Fact]
    public void TrustState_ShouldExposeKnownLifecycleValues()
    {
        Assert.Equal("unvalidated", TrustState.Unvalidated);
        Assert.Equal("invalid", TrustState.Invalid);
        Assert.Equal("valid", TrustState.Valid);
        Assert.Equal("signed", TrustState.Signed);
        Assert.Equal("dirty", TrustState.Dirty);
        Assert.Equal("signatureInvalid", TrustState.SignatureInvalid);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~TrustModelsTests
```

Expected: FAIL with compiler errors for missing `BatteryPassWeb.Models.Trust` types.

- [ ] **Step 3: Add trust model implementation**

Create `web/Models/Trust/TrustModels.cs`:

```csharp
namespace BatteryPassWeb.Models.Trust;

public static class TrustState
{
    public const string Unvalidated = "unvalidated";
    public const string Invalid = "invalid";
    public const string Valid = "valid";
    public const string Signed = "signed";
    public const string Dirty = "dirty";
    public const string SignatureInvalid = "signatureInvalid";
}

public enum TrustValidationSeverity
{
    Passed = 0,
    Warning = 1,
    BlockingError = 2
}

public sealed record TrustValidationIssue(
    TrustValidationSeverity Severity,
    string Path,
    string Message);

public sealed class TrustValidationSectionResult
{
    public string SectionKey { get; init; } = string.Empty;
    public string SectionLabel { get; init; } = string.Empty;
    public IReadOnlyList<TrustValidationIssue> Issues { get; init; } = [];
    public bool HasBlockingErrors => Issues.Any(issue => issue.Severity == TrustValidationSeverity.BlockingError);
    public bool HasWarnings => Issues.Any(issue => issue.Severity == TrustValidationSeverity.Warning);
}

public sealed class TrustValidationSummary
{
    public string PassportId { get; init; } = string.Empty;
    public string State { get; init; } = TrustState.Unvalidated;
    public string ValidatedAt { get; init; } = string.Empty;
    public IReadOnlyList<TrustValidationSectionResult> Sections { get; init; } = [];
    public int BlockingErrorCount => Sections.Sum(section => section.Issues.Count(issue => issue.Severity == TrustValidationSeverity.BlockingError));
    public int WarningCount => Sections.Sum(section => section.Issues.Count(issue => issue.Severity == TrustValidationSeverity.Warning));
    public int PassedCount => Sections.Sum(section => section.Issues.Count(issue => issue.Severity == TrustValidationSeverity.Passed));
    public bool CanSign => BlockingErrorCount == 0;
}

public sealed class SchemaDescriptor
{
    public required string AspectKey { get; init; }
    public required string Label { get; init; }
    public required string Version { get; init; }
    public required string RelativePath { get; init; }
    public string AbsolutePath { get; init; } = string.Empty;
    public bool Exists => !string.IsNullOrWhiteSpace(AbsolutePath) && File.Exists(AbsolutePath);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~TrustModelsTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add web\Models\Trust\TrustModels.cs BatteryPassWeb.Tests\TrustModelsTests.cs
git commit -m "feat: add trust validation models"
```

### Task 2: Schema Registry Service

**Files:**
- Create: `web/Services/SchemaRegistryService.cs`
- Modify: `web/Program.cs`
- Test: `BatteryPassWeb.Tests/SchemaRegistryServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `BatteryPassWeb.Tests/SchemaRegistryServiceTests.cs`:

```csharp
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class SchemaRegistryServiceTests
{
    [Fact]
    public void ListSchemas_ShouldResolveKnownBatteryPassSchemas()
    {
        var service = new SchemaRegistryService();

        var schemas = service.ListSchemas();

        Assert.Contains(schemas, schema => schema.AspectKey == "generalProductInformation" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "carbonFootprintForBatteries" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "circularity" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "materialComposition" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "performanceAndDurability" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "labeling" && schema.Exists);
        Assert.Contains(schemas, schema => schema.AspectKey == "supplyChainDueDiligence" && schema.Exists);
    }

    [Fact]
    public void GetSchema_ShouldReturnNullForUnknownAspect()
    {
        var service = new SchemaRegistryService();

        var schema = service.GetSchema("unknown");

        Assert.Null(schema);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~SchemaRegistryServiceTests
```

Expected: FAIL with missing `SchemaRegistryService`.

- [ ] **Step 3: Add schema registry service**

Create `web/Services/SchemaRegistryService.cs`:

```csharp
using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Services;

public sealed class SchemaRegistryService
{
    private static readonly SchemaDescriptor[] KnownSchemas =
    [
        new()
        {
            AspectKey = "generalProductInformation",
            Label = "General product information",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.GeneralProductInformation", "1.2.0", "gen", "GeneralProductInformation-schema.json")
        },
        new()
        {
            AspectKey = "carbonFootprintForBatteries",
            Label = "Carbon footprint",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.CarbonFootprint", "1.2.0", "gen", "CarbonFootprintForBatteries-schema.json")
        },
        new()
        {
            AspectKey = "circularity",
            Label = "Circularity",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.Circularity", "1.2.0", "gen", "Circularity-schema.json")
        },
        new()
        {
            AspectKey = "materialComposition",
            Label = "Material composition",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.MaterialComposition", "1.2.0", "gen", "MaterialComposition-schema.json")
        },
        new()
        {
            AspectKey = "performanceAndDurability",
            Label = "Performance and durability",
            Version = "1.2.1",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.Performance", "1.2.1", "gen", "PerformanceAndDurability.schema")
        },
        new()
        {
            AspectKey = "labeling",
            Label = "Labels and certification",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.Labels", "1.2.0", "gen", "Labeling-schema.json")
        },
        new()
        {
            AspectKey = "supplyChainDueDiligence",
            Label = "Supply chain due diligence",
            Version = "1.2.0",
            RelativePath = Path.Combine("BatteryPass", "io.BatteryPass.SupplyChainDueDiligence", "1.2.0", "gen", "SupplyChainDueDiligence-schema.json")
        }
    ];

    private readonly string _repoRoot;

    public SchemaRegistryService()
    {
        _repoRoot = ResolveRepoRoot(AppContext.BaseDirectory);
    }

    public IReadOnlyList<SchemaDescriptor> ListSchemas()
    {
        return KnownSchemas.Select(WithAbsolutePath).ToList();
    }

    public SchemaDescriptor? GetSchema(string aspectKey)
    {
        var descriptor = KnownSchemas.FirstOrDefault(schema => schema.AspectKey.Equals(aspectKey, StringComparison.OrdinalIgnoreCase));
        return descriptor == null ? null : WithAbsolutePath(descriptor);
    }

    private SchemaDescriptor WithAbsolutePath(SchemaDescriptor descriptor)
    {
        return new SchemaDescriptor
        {
            AspectKey = descriptor.AspectKey,
            Label = descriptor.Label,
            Version = descriptor.Version,
            RelativePath = descriptor.RelativePath,
            AbsolutePath = Path.Combine(_repoRoot, descriptor.RelativePath)
        };
    }

    private static string ResolveRepoRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "BatteryPass"))
                && Directory.Exists(Path.Combine(directory.FullName, "web")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
```

Modify `web/Program.cs` service registrations:

```csharp
builder.Services.AddSingleton<SchemaRegistryService>();
```

Place it near the other singleton service registrations:

```csharp
builder.Services.AddSingleton<PassportViewModelFactory>();
builder.Services.AddSingleton<SchemaRegistryService>();
builder.Services.AddSingleton<AccessControlService>();
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~SchemaRegistryServiceTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add web\Services\SchemaRegistryService.cs web\Program.cs BatteryPassWeb.Tests\SchemaRegistryServiceTests.cs
git commit -m "feat: add battery pass schema registry"
```

### Task 3: Passport Validation Service

**Files:**
- Create: `web/Services/PassportValidationService.cs`
- Modify: `web/Program.cs`
- Test: `BatteryPassWeb.Tests/PassportValidationServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `BatteryPassWeb.Tests/PassportValidationServiceTests.cs`:

```csharp
using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportValidationServiceTests
{
    [Fact]
    public void Validate_ShouldReturnBlockingErrorsForMissingIdentityFields()
    {
        var service = new PassportValidationService(new SchemaRegistryService());
        var document = new BsonDocument
        {
            ["registryInfo"] = new BsonDocument(),
            ["aspects"] = new BsonDocument()
        };

        var summary = service.Validate(document);

        Assert.Equal(TrustState.Invalid, summary.State);
        Assert.True(summary.BlockingErrorCount >= 2);
        Assert.Contains(summary.Sections.SelectMany(section => section.Issues), issue => issue.Path == "passportId");
        Assert.Contains(summary.Sections.SelectMany(section => section.Issues), issue => issue.Path == "registryInfo.registryId");
        Assert.False(summary.CanSign);
    }

    [Fact]
    public void Validate_ShouldAllowWarningsWithoutBlockingSigning()
    {
        var service = new PassportValidationService(new SchemaRegistryService());
        var document = BuildValidMinimalPassport();

        var summary = service.Validate(document);

        Assert.Equal(TrustState.Valid, summary.State);
        Assert.Equal(0, summary.BlockingErrorCount);
        Assert.True(summary.WarningCount > 0);
        Assert.True(summary.CanSign);
    }

    [Fact]
    public void Validate_ShouldRejectNegativeBatteryMass()
    {
        var service = new PassportValidationService(new SchemaRegistryService());
        var document = BuildValidMinimalPassport();
        document["aspects"]["generalProductInformation"]["payload"]["batteryMass"] = -1;

        var summary = service.Validate(document);

        Assert.Equal(TrustState.Invalid, summary.State);
        Assert.Contains(summary.Sections.SelectMany(section => section.Issues), issue => issue.Path == "aspects.generalProductInformation.payload.batteryMass");
    }

    private static BsonDocument BuildValidMinimalPassport()
    {
        return new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:test-001",
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = "registry-001",
                ["status"] = "draft"
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["modelNumber"] = "MODEL-1",
                    ["serialNumber"] = "SERIAL-1",
                    ["manufacturerName"] = "ACME Batteries"
                }
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMass"] = 10,
                        ["batteryStatus"] = "Original"
                    }
                },
                ["performanceAndDurability"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryTechicalProperties"] = new BsonDocument
                        {
                            ["ratedEnergy"] = 120,
                            ["ratedCapacity"] = 300,
                            ["ratedMaximumPower"] = 420,
                            ["nominalVoltage"] = 800
                        }
                    }
                }
            }
        };
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~PassportValidationServiceTests
```

Expected: FAIL with missing `PassportValidationService`.

- [ ] **Step 3: Add validation service**

Create `web/Services/PassportValidationService.cs`:

```csharp
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportValidationService
{
    private readonly SchemaRegistryService _schemaRegistryService;

    public PassportValidationService(SchemaRegistryService schemaRegistryService)
    {
        _schemaRegistryService = schemaRegistryService;
    }

    public TrustValidationSummary Validate(BsonDocument passport)
    {
        var sections = new List<TrustValidationSectionResult>
        {
            ValidateIdentity(passport)
        };

        foreach (var schema in _schemaRegistryService.ListSchemas())
        {
            sections.Add(ValidateAspect(passport, schema));
        }

        sections.Add(ValidateBusinessRules(passport));
        var state = sections.Any(section => section.HasBlockingErrors) ? TrustState.Invalid : TrustState.Valid;

        return new TrustValidationSummary
        {
            PassportId = BsonHelpers.GetString(passport, "passportId"),
            State = state,
            ValidatedAt = DateTime.UtcNow.ToString("O"),
            Sections = sections
        };
    }

    private static TrustValidationSectionResult ValidateIdentity(BsonDocument passport)
    {
        var issues = new List<TrustValidationIssue>();
        AddRequiredString(passport, "passportId", "passportId", "Passport ID is required.", issues);
        AddRequiredString(passport, "registryInfo.registryId", "registryInfo", "registryId", "Registry ID is required.", issues);

        var display = BsonHelpers.GetValue(passport, "app", "display") as BsonDocument ?? new BsonDocument();
        AddRequiredString(display, "app.display.modelNumber", "modelNumber", "Model number is required.", issues);
        AddRequiredString(display, "app.display.serialNumber", "serialNumber", "Serial number is required.", issues);
        AddRequiredString(display, "app.display.manufacturerName", "manufacturerName", "Manufacturer name is required.", issues);

        if (issues.Count == 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, "identity", "Required identity fields are present."));
        }

        return new TrustValidationSectionResult
        {
            SectionKey = "identity",
            SectionLabel = "Identity",
            Issues = issues
        };
    }

    private static TrustValidationSectionResult ValidateAspect(BsonDocument passport, SchemaDescriptor schema)
    {
        var issues = new List<TrustValidationIssue>();
        var aspect = BsonHelpers.GetValue(passport, "aspects", schema.AspectKey);
        if (aspect is not BsonDocument aspectDocument)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Warning, $"aspects.{schema.AspectKey}", $"{schema.Label} aspect is not present."));
        }
        else if (aspectDocument.GetValue("payload", BsonNull.Value) is not BsonDocument)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.BlockingError, $"aspects.{schema.AspectKey}.payload", $"{schema.Label} payload must be an object."));
        }
        else
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, $"aspects.{schema.AspectKey}.payload", $"{schema.Label} payload is present."));
        }

        if (!schema.Exists)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Warning, schema.RelativePath, $"{schema.Label} schema file was not found."));
        }
        else
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, schema.RelativePath, $"{schema.Label} schema file was found."));
        }

        return new TrustValidationSectionResult
        {
            SectionKey = schema.AspectKey,
            SectionLabel = schema.Label,
            Issues = issues
        };
    }

    private static TrustValidationSectionResult ValidateBusinessRules(BsonDocument passport)
    {
        var issues = new List<TrustValidationIssue>();
        var batteryMass = BsonHelpers.GetValue(passport, "aspects", "generalProductInformation", "payload", "batteryMass");
        if (batteryMass != null && batteryMass.IsNumeric && batteryMass.ToDouble() < 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.BlockingError, "aspects.generalProductInformation.payload.batteryMass", "Battery mass cannot be negative."));
        }

        AddNonNegativeNumber(passport, "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy", issues);
        AddNonNegativeNumber(passport, "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedCapacity", issues);
        AddNonNegativeNumber(passport, "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedMaximumPower", issues);
        AddNonNegativeNumber(passport, "aspects.performanceAndDurability.payload.batteryTechicalProperties.nominalVoltage", issues);

        var batteryImageUrl = BsonHelpers.GetString(passport, "app", "media", "batteryImageUrl");
        if (string.IsNullOrWhiteSpace(batteryImageUrl))
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Warning, "app.media.batteryImageUrl", "Battery image is missing or using fallback."));
        }

        if (issues.Count == 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, "businessRules", "Business rule checks passed."));
        }

        return new TrustValidationSectionResult
        {
            SectionKey = "businessRules",
            SectionLabel = "Business rules",
            Issues = issues
        };
    }

    private static void AddRequiredString(BsonDocument document, string path, string key, string message, ICollection<TrustValidationIssue> issues)
    {
        var value = document.GetValue(key, BsonNull.Value);
        if (value.IsBsonNull || string.IsNullOrWhiteSpace(value.ToString()))
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.BlockingError, path, message));
        }
    }

    private static void AddRequiredString(BsonDocument document, string path, string firstKey, string secondKey, string message, ICollection<TrustValidationIssue> issues)
    {
        var parent = document.GetValue(firstKey, new BsonDocument()) as BsonDocument ?? new BsonDocument();
        AddRequiredString(parent, path, secondKey, message, issues);
    }

    private static void AddNonNegativeNumber(BsonDocument passport, string path, ICollection<TrustValidationIssue> issues)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var value = BsonHelpers.GetValue(passport, segments);
        if (value != null && value.IsNumeric && value.ToDouble() < 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.BlockingError, path, $"{path.Split('.').Last()} cannot be negative."));
        }
    }
}
```

Modify `web/Program.cs`:

```csharp
builder.Services.AddSingleton<SchemaRegistryService>();
builder.Services.AddSingleton<PassportValidationService>();
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~PassportValidationServiceTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add web\Services\PassportValidationService.cs web\Program.cs BatteryPassWeb.Tests\PassportValidationServiceTests.cs
git commit -m "feat: add passport validation service"
```

### Task 4: Trust Persistence And Dirty Tracking

**Files:**
- Modify: `web/Services/PassportRepository.cs`
- Test: `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class TrustDirtyStateTests
{
    [Fact]
    public void PassportRepository_ShouldExposeCanonicalDirtyMethod()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));

        Assert.Contains("MarkCanonicalDirtyAsync", source);
        Assert.Contains("\"trust.isDirty\"", source);
        Assert.Contains("\"trust.state\"", source);
        Assert.Contains("TrustState.Dirty", source);
    }

    [Fact]
    public void ExternalApiController_ShouldNotMarkPassportDirtyForHttpWrites()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.DoesNotContain("MarkCanonicalDirtyAsync", source);
        Assert.Contains("UpdateFieldsAsync(passportId, setValues, cancellationToken)", source);
    }

    [Fact]
    public void AdminController_ShouldMarkCanonicalSavesDirty()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("MarkCanonicalDirtyAsync(passportId", source);
    }

    private static string RepoFile(params string[] parts)
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

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~TrustDirtyStateTests
```

Expected: FAIL because repository and admin dirty-tracking methods are missing.

- [ ] **Step 3: Add repository methods**

Modify `web/Services/PassportRepository.cs` with this using:

```csharp
using BatteryPassWeb.Models.Trust;
```

Add these methods before `private IMongoCollection<BsonDocument>? GetCollection()`:

```csharp
public async Task UpdateTrustValidationAsync(string passportId, TrustValidationSummary summary, CancellationToken cancellationToken = default)
{
    var collection = GetCollection();
    if (collection == null || string.IsNullOrWhiteSpace(passportId))
    {
        return;
    }

    var summaryDocument = new BsonDocument
    {
        ["state"] = summary.State,
        ["validatedAt"] = summary.ValidatedAt,
        ["blockingErrorCount"] = summary.BlockingErrorCount,
        ["warningCount"] = summary.WarningCount,
        ["passedCount"] = summary.PassedCount,
        ["canSign"] = summary.CanSign,
        ["sections"] = new BsonArray(summary.Sections.Select(section => new BsonDocument
        {
            ["sectionKey"] = section.SectionKey,
            ["sectionLabel"] = section.SectionLabel,
            ["issues"] = new BsonArray(section.Issues.Select(issue => new BsonDocument
            {
                ["severity"] = issue.Severity.ToString(),
                ["path"] = issue.Path,
                ["message"] = issue.Message
            }))
        }))
    };

    await collection.UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("passportId", passportId),
        Builders<BsonDocument>.Update
            .Set("trust.state", summary.State)
            .Set("trust.lastValidatedAt", summary.ValidatedAt)
            .Set("trust.validationSummary", summaryDocument)
            .Set("registryInfo.updatedAt", DateTime.UtcNow.ToString("O")),
        cancellationToken: cancellationToken);
}

public async Task MarkCanonicalDirtyAsync(string passportId, string actor, CancellationToken cancellationToken = default)
{
    var collection = GetCollection();
    if (collection == null || string.IsNullOrWhiteSpace(passportId))
    {
        return;
    }

    await collection.UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("passportId", passportId),
        Builders<BsonDocument>.Update
            .Set("trust.isDirty", true)
            .Set("trust.state", TrustState.Dirty)
            .Set("trust.dirtyAt", DateTime.UtcNow.ToString("O"))
            .Set("trust.dirtyBy", string.IsNullOrWhiteSpace(actor) ? "unknown" : actor)
            .Set("registryInfo.updatedAt", DateTime.UtcNow.ToString("O")),
        cancellationToken: cancellationToken);
}
```

- [ ] **Step 4: Mark canonical admin saves dirty**

Modify `web/Controllers/AdminController.cs` `CreatePassport` after `ReplaceAsync`:

```csharp
await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
await _passportRepository.UpdateTrustValidationAsync(passportId, _passportValidationService.Validate(document), cancellationToken);
return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/edit?status=created");
```

Modify `SavePassport` after `ReplaceAsync`:

```csharp
await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
await _passportRepository.MarkCanonicalDirtyAsync(passportId, AccessControlService.CurrentEmail(User), cancellationToken);
return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/edit?status=saved");
```

Add a constructor dependency and field in `AdminController`:

```csharp
private readonly PassportValidationService _passportValidationService;
```

Constructor signature:

```csharp
public AdminController(
    PassportRepository passportRepository,
    ClusterRepository clusterRepository,
    PassportViewModelFactory viewModelFactory,
    ExternalApiRepository externalApiRepository,
    PassportValidationService passportValidationService)
```

Constructor body:

```csharp
_passportValidationService = passportValidationService;
```

- [ ] **Step 5: Run test to verify it passes**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~TrustDirtyStateTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add web\Services\PassportRepository.cs web\Controllers\AdminController.cs BatteryPassWeb.Tests\TrustDirtyStateTests.cs
git commit -m "feat: track passport trust dirty state"
```

### Task 5: Validation Endpoint And Conformance Route

**Files:**
- Modify: `web/Controllers/PassportsApiController.cs`
- Modify: `web/Controllers/AdminController.cs`
- Create: `web/Models/ViewModels/ConformanceViewModel.cs`
- Create: `web/Views/Admin/Conformance.cshtml`
- Test: `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class ConformanceLayoutTests
{
    [Fact]
    public void PassportsApiController_ShouldExposeValidateEndpoint()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));

        Assert.Contains("[HttpPost(\"{passportId}/validate\")]", source);
        Assert.Contains("PassportValidationService", source);
        Assert.Contains("UpdateTrustValidationAsync", source);
    }

    [Fact]
    public void AdminController_ShouldExposeConformancePageAndValidateAction()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("[HttpGet(\"passports/{passportId}/conformance\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", source);
        Assert.Contains("ConformanceViewModel", source);
    }

    [Fact]
    public void ConformanceView_ShouldRenderTrustSummaryAndActions()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.Contains("Conformance", markup);
        Assert.Contains("Blocking errors", markup);
        Assert.Contains("Warnings", markup);
        Assert.Contains("Validate passport", markup);
        Assert.Contains("Latest validation", markup);
    }

    private static string RepoFile(params string[] parts)
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

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~ConformanceLayoutTests
```

Expected: FAIL because the endpoint, view model, and view are missing.

- [ ] **Step 3: Add conformance view model**

Create `web/Models/ViewModels/ConformanceViewModel.cs`:

```csharp
using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class ConformanceViewModel
{
    public required PassportViewModel Passport { get; init; }
    public required TrustValidationSummary ValidationSummary { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Add validate API endpoint**

Modify `web/Controllers/PassportsApiController.cs`:

Add constructor dependencies:

```csharp
private readonly PassportValidationService _passportValidationService;
```

Constructor:

```csharp
public PassportsApiController(
    PassportRepository passportRepository,
    PassportValidationService passportValidationService)
{
    _passportRepository = passportRepository;
    _passportValidationService = passportValidationService;
}
```

Add endpoint before `Archive`:

```csharp
[HttpPost("{passportId}/validate")]
public async Task<IActionResult> Validate(string passportId, CancellationToken cancellationToken)
{
    if (!User.IsInRole("admin"))
    {
        return Forbid();
    }

    var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
    if (passport == null)
    {
        return NotFound(new { error = "Passport does not exist", passportId });
    }

    var summary = _passportValidationService.Validate(passport);
    await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
    return Ok(new
    {
        passportId,
        state = summary.State,
        blockingErrors = summary.BlockingErrorCount,
        warnings = summary.WarningCount,
        passed = summary.PassedCount,
        canSign = summary.CanSign,
        sections = summary.Sections
    });
}
```

- [ ] **Step 5: Add admin conformance actions**

Modify `web/Controllers/AdminController.cs` with methods before `Clusters`:

```csharp
[HttpGet("passports/{passportId}/conformance")]
public async Task<IActionResult> Conformance(string passportId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
{
    var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
    if (document == null)
    {
        return NotFound();
    }

    var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
    var clusterNamesById = BuildClusterDictionary(clusters);
    var summary = _passportValidationService.Validate(document);

    return View(new ConformanceViewModel
    {
        Passport = _viewModelFactory.Create(document, clusterNamesById),
        ValidationSummary = summary,
        StatusMessage = status == "validated" ? "Passport validation completed." : string.Empty,
        ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error)
    });
}

[HttpPost("passports/{passportId}/validate")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ValidatePassport(string passportId, CancellationToken cancellationToken)
{
    var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
    if (document == null)
    {
        return NotFound();
    }

    var summary = _passportValidationService.Validate(document);
    await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
    return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/conformance?status=validated");
}
```

- [ ] **Step 6: Add conformance view**

Create `web/Views/Admin/Conformance.cshtml`:

```cshtml
@model BatteryPassWeb.Models.ViewModels.ConformanceViewModel
@{
    ViewData["Title"] = "Passport conformance";
    var passport = Model.Passport;
    var summary = Model.ValidationSummary;
}
<main class="bp-page bp-admin-page">
    <div class="d-flex justify-content-between flex-wrap gap-3 align-items-end">
        <div>
            <h1 class="mb-1">Conformance</h1>
            <p class="bp-passport-id mb-0">@passport.PassportId</p>
        </div>
        <div class="d-flex flex-wrap gap-2">
            <a href="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/edit" class="bp-secondary-button">Edit passport</a>
            <a href="/@Uri.EscapeDataString(passport.PassportId)/summary" class="bp-secondary-button">Public summary</a>
        </div>
    </div>

    @if (!string.IsNullOrWhiteSpace(Model.StatusMessage))
    {
        <div class="alert alert-success mt-3 mb-0">@Model.StatusMessage</div>
    }
    @if (!string.IsNullOrWhiteSpace(Model.ErrorMessage))
    {
        <div class="alert alert-danger mt-3 mb-0">@Model.ErrorMessage</div>
    }

    <section class="bp-card mt-4">
        <div class="d-flex justify-content-between flex-wrap gap-3 align-items-start">
            <div>
                <span class="bp-summary-title">Latest validation</span>
                <h2 class="h4 mb-1">@summary.State</h2>
                <p class="text-secondary mb-0">@(string.IsNullOrWhiteSpace(summary.ValidatedAt) ? "Not persisted yet" : summary.ValidatedAt)</p>
            </div>
            <form method="post" action="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/validate">
                @Html.AntiForgeryToken()
                <button type="submit" class="bp-primary-button">Validate passport</button>
            </form>
        </div>
        <div class="bp-metric-grid mt-3">
            <div class="bp-metric"><p>Blocking errors</p><strong>@summary.BlockingErrorCount</strong></div>
            <div class="bp-metric"><p>Warnings</p><strong>@summary.WarningCount</strong></div>
            <div class="bp-metric"><p>Passed checks</p><strong>@summary.PassedCount</strong></div>
            <div class="bp-metric"><p>Can sign</p><strong>@(summary.CanSign ? "Yes" : "No")</strong></div>
        </div>
    </section>

    @foreach (var section in summary.Sections)
    {
        <section class="bp-card">
            <h2 class="h5 mb-2">@section.SectionLabel</h2>
            <div class="table-responsive">
                <table class="table table-sm align-middle mb-0">
                    <thead>
                        <tr>
                            <th>Severity</th>
                            <th>Path</th>
                            <th>Message</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var issue in section.Issues)
                        {
                            <tr>
                                <td>@issue.Severity</td>
                                <td><code>@issue.Path</code></td>
                                <td>@issue.Message</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </section>
    }
</main>
```

- [ ] **Step 7: Run test to verify it passes**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~ConformanceLayoutTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add web\Controllers\PassportsApiController.cs web\Controllers\AdminController.cs web\Models\ViewModels\ConformanceViewModel.cs web\Views\Admin\Conformance.cshtml BatteryPassWeb.Tests\ConformanceLayoutTests.cs
git commit -m "feat: add passport conformance validation page"
```

### Task 6: Trust State In Existing View Models And Pages

**Files:**
- Modify: `web/Models/ViewModels/PassportViewModel.cs`
- Modify: `web/Services/PassportViewModelFactory.cs`
- Modify: `web/Views/Admin/EditPassport.cshtml`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Test: extend `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`

- [ ] **Step 1: Add failing tests**

Append these tests to `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`:

```csharp
[Fact]
public void PassportViewModel_ShouldExposeTrustFields()
{
    var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));

    Assert.Contains("TrustState", source);
    Assert.Contains("TrustIsDirty", source);
    Assert.Contains("TrustLastValidatedAt", source);
    Assert.Contains("TrustBlockingErrorCount", source);
    Assert.Contains("TrustWarningCount", source);
}

[Fact]
public void PassportPages_ShouldDisplayTrustState()
{
    var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
    var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

    Assert.Contains("passport.TrustState", summary);
    Assert.Contains("passport.TrustState", detail);
}

[Fact]
public void AdminPages_ShouldLinkToConformance()
{
    var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
    var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

    Assert.Contains("/conformance", edit);
    Assert.Contains("/conformance", clusters);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~ConformanceLayoutTests
```

Expected: FAIL because trust fields and links are missing.

- [ ] **Step 3: Add trust fields to `PassportViewModel`**

Add these properties after `VerificationState` in `web/Models/ViewModels/PassportViewModel.cs`:

```csharp
public string TrustState { get; init; } = "unvalidated";
public bool TrustIsDirty { get; init; }
public string TrustLastValidatedAt { get; init; } = string.Empty;
public int TrustBlockingErrorCount { get; init; }
public int TrustWarningCount { get; init; }
```

- [ ] **Step 4: Map trust fields in `PassportViewModelFactory`**

In `web/Services/PassportViewModelFactory.cs`, before the `return new PassportViewModel` block, add:

```csharp
var trust = GetDocument(document.GetValue("trust", new BsonDocument()));
var validationSummary = GetDocument(trust.GetValue("validationSummary", new BsonDocument()));
var trustState = FirstNonEmpty(
    trust.GetValue("state", string.Empty).ToString(),
    isValid ? "signed" : "unvalidated");
var trustBlockingErrors = NumberAt(validationSummary, "blockingErrorCount");
var trustWarnings = NumberAt(validationSummary, "warningCount");
```

Inside the `PassportViewModel` initializer, add:

```csharp
TrustState = trustState,
TrustIsDirty = trust.GetValue("isDirty", false).ToBoolean(),
TrustLastValidatedAt = trust.GetValue("lastValidatedAt", string.Empty).ToString(),
TrustBlockingErrorCount = (int)trustBlockingErrors,
TrustWarningCount = (int)trustWarnings,
```

- [ ] **Step 5: Add conformance link and trust banner to admin edit page**

In `web/Views/Admin/EditPassport.cshtml`, after the passport ID paragraph, add:

```cshtml
<div class="bp-card mt-3">
    <div class="d-flex justify-content-between flex-wrap gap-3 align-items-center">
        <div>
            <span class="bp-summary-title">Trust state</span>
            <p class="mb-0">
                <strong>@passport.TrustState</strong>
                @if (passport.TrustIsDirty)
                {
                    <span class="bp-pill bp-pill-warn ms-2">Dirty</span>
                }
            </p>
        </div>
        <a href="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/conformance" class="bp-secondary-button">Open conformance</a>
    </div>
</div>
```

- [ ] **Step 6: Add conformance link to admin passport table**

In `web/Views/Admin/Clusters.cshtml`, inside the passport action row after the edit link, add:

```cshtml
<a href="/admin/passports/@Uri.EscapeDataString(row.PassportId)/conformance">Conformance</a>
```

- [ ] **Step 7: Display trust state on public summary and detail pages**

In `web/Views/Passport/Summary.cshtml`, inside the `bp-badge-row`, add:

```cshtml
<span class="bp-pill bp-pill-muted">@passport.TrustState</span>
```

In `web/Views/Passport/Detail.cshtml`, inside the `bp-badge-row`, add:

```cshtml
<span class="bp-pill bp-pill-muted">@passport.TrustState</span>
```

- [ ] **Step 8: Run tests to verify they pass**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~ConformanceLayoutTests
```

Expected: PASS.

- [ ] **Step 9: Commit**

```powershell
git add web\Models\ViewModels\PassportViewModel.cs web\Services\PassportViewModelFactory.cs web\Views\Admin\EditPassport.cshtml web\Views\Admin\Clusters.cshtml web\Views\Passport\Summary.cshtml web\Views\Passport\Detail.cshtml BatteryPassWeb.Tests\ConformanceLayoutTests.cs
git commit -m "feat: surface passport trust state in UI"
```

### Task 7: Full Test Run And Phase 1 Verification

**Files:**
- No planned source files.

- [ ] **Step 1: Run the full test suite**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj
```

Expected: PASS.

- [ ] **Step 2: Run a build**

Run:

```powershell
dotnet build web\BatteryPassWeb.csproj
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 3: Inspect git status**

Run:

```powershell
git status --short
```

Expected: no uncommitted tracked source changes. Ignored `.superpowers/` files may exist and should not be committed.

- [ ] **Step 4: Confirm no final commit is needed**

Run:

```powershell
git log --oneline -5
```

Expected: recent commits include the task commits from this plan, and `git status --short` from Step 3 is empty for tracked files.

## Self-Review

Spec coverage:

- Trust state fields are covered by Tasks 1, 4, and 6.
- Schema registry is covered by Task 2.
- Validation service and blocking/warning model are covered by Task 3.
- Dirty tracking for admin canonical edits is covered by Task 4.
- External HTTP writes not dirtying signed passport state is covered by Task 4 tests.
- Validation endpoint and conformance skeleton are covered by Task 5.
- Public/admin trust state display is covered by Task 6.
- Full verification is covered by Task 7.

Deferred to later phase plans:

- cryptographic signing and verification,
- immutable revision snapshots,
- audit event collection,
- secure file access and document hashing,
- QR lookup and generation,
- final admin guidance polish.

Placeholder scan:

- This plan contains concrete file paths, code snippets, commands, and expected results for every Phase 1 task.

Type consistency:

- `TrustValidationSummary`, `TrustValidationSectionResult`, `TrustValidationIssue`, `SchemaDescriptor`, `SchemaRegistryService`, and `PassportValidationService` are defined before use.
- Repository methods used by controllers are defined in Task 4 before controller endpoint work in Task 5.
