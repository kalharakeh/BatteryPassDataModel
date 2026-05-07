# Phase 6B Evidence Pack And Document Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add admin-facing document evidence readiness that compares current supporting-document hashes with the latest signed revision and records upload/replacement audit events.

**Architecture:** Add a read-only `PassportEvidenceService` that evaluates `app.documents` against the data-completion policy and latest signed revision snapshot. Wire its result into admin/API validation summaries, conformance view models, and the upload route audit trail. Keep document content parsing out of scope; Phase 6B verifies references, hashes, visibility, and signed-revision consistency.

**Tech Stack:** ASP.NET Core MVC, Razor, MongoDB BSON/GridFS, xUnit, existing Battery Pass trust services.

---

## File Structure

- Create `web/Models/Trust/PassportEvidenceModels.cs`
  - Evidence status constants and immutable result models.
- Create `web/Services/PassportEvidenceService.cs`
  - Evaluates current document references, latest signed revision hashes, required/optional policy state, and validation-section output.
- Create `BatteryPassWeb.Tests/PassportEvidenceServiceTests.cs`
  - Unit tests for missing, uploaded unsigned, verified, changed, and external-link-only evidence.
- Modify `web/Services/AuditRevisionService.cs`
  - Add latest signed revision lookup.
- Modify `web/Controllers/AdminController.cs`
  - Inject evidence service, evaluate evidence in conformance/validate/sign/publish flows, append evidence validation section.
- Modify `web/Controllers/PassportsApiController.cs`
  - Inject evidence service and append evidence validation section for API validate/sign/publish.
- Modify `web/Controllers/FilesApiController.cs`
  - Add upload/replacement audit event after document reference persistence.
- Modify `web/Models/ViewModels/ConformanceViewModel.cs`
  - Add evidence pack property for the Razor page.
- Modify `web/Views/Admin/Conformance.cshtml`
  - Add evidence readiness panel with counts and item-level chips.
- Modify `web/wwwroot/css/site.css`
  - Add evidence panel styles using existing conformance visual language.
- Modify `web/Program.cs`
  - Register `PassportEvidenceService`.
- Modify `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`
  - Source/layout coverage for evidence panel.
- Modify `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`
  - Source coverage for upload/replacement audit.
- Modify `BatteryPassWeb.Tests/AuditRevisionServiceTests.cs`
  - Source coverage for latest revision lookup helper.
- Modify `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`
  - Extend docs checks to include Phase 6B evidence guidance.
- Modify `docs/end-user-testing-guide.md`
  - Manual evidence verification script.
- Modify `web/Views/Admin/Help.cshtml`
  - Admin workflow help explanation for document evidence and re-signing.

---

### Task 1: Add Evidence Models And Service

**Files:**
- Create: `web/Models/Trust/PassportEvidenceModels.cs`
- Create: `web/Services/PassportEvidenceService.cs`
- Create: `BatteryPassWeb.Tests/PassportEvidenceServiceTests.cs`

- [ ] **Step 1: Write failing evidence service tests**

Create `BatteryPassWeb.Tests/PassportEvidenceServiceTests.cs` with these tests:

```csharp
using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportEvidenceServiceTests
{
    [Fact]
    public void Evaluate_ShouldBlockWhenRequiredEvidenceIsMissing()
    {
        var passport = PassportWithDocuments();
        var policy = Policy(required: true);

        var result = new PassportEvidenceService().Evaluate(passport, latestSignedRevision: null, policy);

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.MissingRequired, item.Status);
        Assert.True(item.IsRequired);
        Assert.True(item.IsBlocking);
        Assert.Equal(1, result.MissingRequiredCount);
        Assert.Equal(1, result.BlockingCount);
    }

    [Fact]
    public void Evaluate_ShouldTreatUploadedHashWithoutRevisionAsUnsigned()
    {
        var passport = PassportWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-1", "/api/files/file-1", "abc123", "private")
        });

        var result = new PassportEvidenceService().Evaluate(passport, latestSignedRevision: null, Policy(required: true));

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.UploadedUnsigned, item.Status);
        Assert.False(item.IsBlocking);
        Assert.Equal(1, result.UploadedUnsignedCount);
    }

    [Fact]
    public void Evaluate_ShouldVerifyHashWhenCurrentAndSignedHashesMatch()
    {
        var passport = PassportWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-1", "/api/files/file-1", "abc123", "public")
        });
        var revision = RevisionWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-1", "/api/files/file-1", "sha256:abc123", "public")
        });

        var result = new PassportEvidenceService().Evaluate(passport, revision, Policy(required: true));

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.Verified, item.Status);
        Assert.Equal("sha256:abc123", item.CurrentHash);
        Assert.Equal("sha256:abc123", item.SignedHash);
        Assert.Equal(1, result.VerifiedCount);
        Assert.Equal(1, result.PublicCount);
    }

    [Fact]
    public void Evaluate_ShouldShowChangedSinceSigningWhenHashDiffers()
    {
        var passport = PassportWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-2", "/api/files/file-2", "newhash", "private")
        });
        var revision = RevisionWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-1", "/api/files/file-1", "oldhash", "private")
        });

        var result = new PassportEvidenceService().Evaluate(passport, revision, Policy(required: true));

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.ChangedSinceSigning, item.Status);
        Assert.False(item.IsBlocking);
        Assert.Equal(1, result.ChangedSinceSigningCount);
        Assert.Equal(1, result.RestrictedCount);
    }

    [Fact]
    public void Evaluate_ShouldMarkRequiredExternalLinkWithoutHashAsBlockingEvidence()
    {
        var passport = PassportWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = new BsonDocument
            {
                ["url"] = "https://example.test/conformity.pdf",
                ["visibility"] = "public"
            }
        });

        var result = new PassportEvidenceService().Evaluate(passport, latestSignedRevision: null, Policy(required: true));

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.ExternalLinkOnly, item.Status);
        Assert.True(item.IsBlocking);
        Assert.Equal(1, result.BlockingCount);
    }

    [Fact]
    public void BuildValidationSection_ShouldConvertBlockingEvidenceToValidationIssue()
    {
        var passport = PassportWithDocuments();
        var result = new PassportEvidenceService().Evaluate(passport, latestSignedRevision: null, Policy(required: true));

        var section = PassportEvidenceService.BuildValidationSection(result);

        Assert.Equal("supportingEvidence", section.SectionKey);
        Assert.Contains(section.Issues, issue =>
            issue.Severity == TrustValidationSeverity.BlockingError
            && issue.Path == "app.documents.conformityAssessment");
    }

    private static BsonDocument PassportWithDocuments(BsonDocument? documents = null)
    {
        return new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:evidence-test-001",
            ["app"] = new BsonDocument
            {
                ["documents"] = documents ?? new BsonDocument()
            }
        };
    }

    private static BsonDocument RevisionWithDocuments(BsonDocument documents)
    {
        return new BsonDocument
        {
            ["revisionNumber"] = 4,
            ["status"] = "signed",
            ["snapshot"] = PassportWithDocuments(documents)
        };
    }

    private static BsonDocument DocumentReference(string fileId, string url, string sha256, string visibility)
    {
        return new BsonDocument
        {
            ["fileId"] = fileId,
            ["url"] = url,
            ["sha256"] = sha256,
            ["visibility"] = visibility,
            ["contentType"] = "application/pdf"
        };
    }

    private static DataCompletionPolicySnapshot Policy(bool required)
    {
        return new DataCompletionPolicySnapshot
        {
            PolicyKey = DataCompletionPolicyService.PolicyKey,
            Sections =
            [
                new DataRequirementSection
                {
                    SectionKey = "compliance",
                    Label = "Compliance",
                    SortOrder = 1,
                    Fields =
                    [
                        new DataRequirementField
                        {
                            FieldKey = "compliance.conformityAssessment",
                            SectionKey = "compliance",
                            Label = "Conformity assessment report",
                            DataPath = "aspects.labeling.payload.resultOfTestReport",
                            Guidance = "Upload the conformity assessment report.",
                            DefaultRequired = required,
                            IsRequired = required,
                            SortOrder = 1
                        }
                    ]
                }
            ]
        };
    }
}
```

- [ ] **Step 2: Run the failing tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~PassportEvidenceServiceTests /p:UseAppHost=false
```

Expected: build fails because `PassportEvidenceService`, `PassportEvidenceStatus`, and evidence models do not exist.

- [ ] **Step 3: Add evidence result models**

Create `web/Models/Trust/PassportEvidenceModels.cs`:

```csharp
namespace BatteryPassWeb.Models.Trust;

public static class PassportEvidenceStatus
{
    public const string MissingRequired = "missingRequired";
    public const string MissingOptional = "missingOptional";
    public const string UploadedUnsigned = "uploadedUnsigned";
    public const string Verified = "verified";
    public const string ChangedSinceSigning = "changedSinceSigning";
    public const string ExternalLinkOnly = "externalLinkOnly";
    public const string MissingFileReference = "missingFileReference";
}

public sealed class EvidencePackResult
{
    public static EvidencePackResult Empty { get; } = new();

    public IReadOnlyList<EvidenceItemResult> Items { get; init; } = [];
    public int RequiredCount => Items.Count(item => item.IsRequired);
    public int OptionalCount => Items.Count(item => !item.IsRequired);
    public int BlockingCount => Items.Count(item => item.IsBlocking);
    public int MissingRequiredCount => Items.Count(item => item.Status == PassportEvidenceStatus.MissingRequired);
    public int UploadedUnsignedCount => Items.Count(item => item.Status == PassportEvidenceStatus.UploadedUnsigned);
    public int ChangedSinceSigningCount => Items.Count(item => item.Status == PassportEvidenceStatus.ChangedSinceSigning);
    public int VerifiedCount => Items.Count(item => item.Status == PassportEvidenceStatus.Verified);
    public int PublicCount => Items.Count(item => item.Visibility == "public");
    public int RestrictedCount => Items.Count(item => item.Visibility != "public");
}

public sealed class EvidenceItemResult
{
    public string DocumentKey { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string FieldKey { get; init; } = string.Empty;
    public string DataPath { get; init; } = string.Empty;
    public string DocumentPath { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public bool IsBlocking { get; init; }
    public string Status { get; init; } = PassportEvidenceStatus.MissingOptional;
    public string StatusLabel { get; init; } = string.Empty;
    public string Visibility { get; init; } = "private";
    public string FileId { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string CurrentHash { get; init; } = string.Empty;
    public string SignedHash { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Add evidence service implementation**

Create `web/Services/PassportEvidenceService.cs`:

```csharp
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportEvidenceService
{
    private static readonly IReadOnlyList<EvidenceDocumentDefinition> Definitions =
    [
        new("conformityAssessment", "Conformity assessment report", "compliance.conformityAssessment", "aspects.labeling.payload.resultOfTestReport"),
        new("euDeclarationOfConformity", "EU declaration of conformity", "compliance.euDeclarationOfConformity", "aspects.labeling.payload.declarationOfConformity"),
        new("sustainabilityReport", "Sustainability report", "supplyChain.sustainabilityReport", "aspects.supplyChainDueDiligence.payload.sustainabilityReport"),
        new("dueDiligenceReport", "Due diligence report", "supplyChain.dueDiligenceReport", "aspects.supplyChainDueDiligence.payload.supplyChainDueDiligenceReport"),
        new("thirdPartyAudit", "Third-party audit", "supplyChain.thirdPartyAudit", "aspects.supplyChainDueDiligence.payload.thirdPartyAussurances"),
        new("taxonomyReport", "Taxonomy report", "supplyChain.taxonomyReport", "aspects.supplyChainDueDiligence.payload.taxonomyReport"),
        new("co2StudyReference", "CO2 study reference", "carbon.co2StudyReference", "aspects.carbonFootprintForBatteries.payload.carbonFootprintStudy")
    ];

    public EvidencePackResult Evaluate(
        BsonDocument passport,
        BsonDocument? latestSignedRevision,
        DataCompletionPolicySnapshot policy)
    {
        var requiredFields = policy.Sections
            .SelectMany(section => section.Fields)
            .Where(field => field.IsRequired)
            .Select(field => field.FieldKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var signedSnapshot = latestSignedRevision?.GetValue("snapshot", BsonNull.Value) as BsonDocument;
        var items = Definitions
            .Select(definition => EvaluateDefinition(passport, signedSnapshot, requiredFields, definition))
            .ToList();

        return new EvidencePackResult { Items = items };
    }

    public static TrustValidationSummary AppendValidationSection(
        TrustValidationSummary summary,
        EvidencePackResult evidencePack)
    {
        var sections = summary.Sections
            .Where(section => !section.SectionKey.Equals("supportingEvidence", StringComparison.OrdinalIgnoreCase))
            .Append(BuildValidationSection(evidencePack))
            .ToList();

        return new TrustValidationSummary
        {
            PassportId = summary.PassportId,
            State = sections.Any(section => section.HasBlockingErrors) ? TrustState.Invalid : TrustState.Valid,
            ValidatedAt = summary.ValidatedAt,
            Sections = sections
        };
    }

    public static TrustValidationSectionResult BuildValidationSection(EvidencePackResult evidencePack)
    {
        var issues = evidencePack.Items.Select(item =>
        {
            var severity = item.IsBlocking
                ? TrustValidationSeverity.BlockingError
                : item.Status is PassportEvidenceStatus.UploadedUnsigned or PassportEvidenceStatus.ChangedSinceSigning
                    ? TrustValidationSeverity.Warning
                    : TrustValidationSeverity.Passed;

            return new TrustValidationIssue(severity, item.DocumentPath, item.RecommendedAction);
        }).ToList();

        if (issues.Count == 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, "app.documents", "No supporting evidence definitions are configured."));
        }

        return new TrustValidationSectionResult
        {
            SectionKey = "supportingEvidence",
            SectionLabel = "Supporting document evidence",
            Issues = issues
        };
    }

    private static EvidenceItemResult EvaluateDefinition(
        BsonDocument passport,
        BsonDocument? signedSnapshot,
        ISet<string> requiredFields,
        EvidenceDocumentDefinition definition)
    {
        var currentReference = ResolveDocument(passport, "app", "documents", definition.DocumentKey);
        var signedReference = signedSnapshot == null
            ? new BsonDocument()
            : ResolveDocument(signedSnapshot, "app", "documents", definition.DocumentKey);
        var isRequired = requiredFields.Contains(definition.FieldKey);
        var url = FirstNonEmpty(
            BsonHelpers.GetString(currentReference, "url"),
            ResolveString(passport, definition.DataPath));
        var fileId = BsonHelpers.GetString(currentReference, "fileId");
        var currentHash = NormalizeHash(BsonHelpers.GetString(currentReference, "sha256"));
        var signedHash = NormalizeHash(BsonHelpers.GetString(signedReference, "sha256"));
        var visibility = NormalizeVisibility(BsonHelpers.GetString(currentReference, "visibility"));
        var status = ResolveStatus(isRequired, url, fileId, currentHash, signedHash, signedSnapshot != null);
        var isBlocking = status == PassportEvidenceStatus.MissingRequired
            || (isRequired && status == PassportEvidenceStatus.ExternalLinkOnly)
            || (isRequired && status == PassportEvidenceStatus.MissingFileReference);

        return new EvidenceItemResult
        {
            DocumentKey = definition.DocumentKey,
            Label = definition.Label,
            FieldKey = definition.FieldKey,
            DataPath = definition.DataPath,
            DocumentPath = $"app.documents.{definition.DocumentKey}",
            IsRequired = isRequired,
            IsBlocking = isBlocking,
            Status = status,
            StatusLabel = StatusLabel(status),
            Visibility = visibility,
            FileId = fileId,
            Url = url,
            CurrentHash = currentHash,
            SignedHash = signedHash,
            RecommendedAction = RecommendedAction(status, definition.Label, isRequired)
        };
    }

    private static string ResolveStatus(bool isRequired, string url, string fileId, string currentHash, string signedHash, bool hasSignedSnapshot)
    {
        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(fileId) && string.IsNullOrWhiteSpace(currentHash))
        {
            return isRequired ? PassportEvidenceStatus.MissingRequired : PassportEvidenceStatus.MissingOptional;
        }

        if (!string.IsNullOrWhiteSpace(fileId) && string.IsNullOrWhiteSpace(currentHash))
        {
            return PassportEvidenceStatus.MissingFileReference;
        }

        if (string.IsNullOrWhiteSpace(currentHash))
        {
            return PassportEvidenceStatus.ExternalLinkOnly;
        }

        if (!hasSignedSnapshot || string.IsNullOrWhiteSpace(signedHash))
        {
            return PassportEvidenceStatus.UploadedUnsigned;
        }

        return currentHash.Equals(signedHash, StringComparison.OrdinalIgnoreCase)
            ? PassportEvidenceStatus.Verified
            : PassportEvidenceStatus.ChangedSinceSigning;
    }

    private static BsonDocument ResolveDocument(BsonDocument document, params string[] path)
    {
        var value = BsonHelpers.GetValue(document, path);
        return value as BsonDocument ?? new BsonDocument();
    }

    private static string ResolveString(BsonDocument document, string path)
    {
        var value = BsonHelpers.GetValue(document, path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return value == null || value.IsBsonNull ? string.Empty : value.ToString() ?? string.Empty;
    }

    private static string NormalizeHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"sha256:{value}";
    }

    private static string NormalizeVisibility(string value)
    {
        return value.Equals("public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
    }

    private static string StatusLabel(string status)
    {
        return status switch
        {
            PassportEvidenceStatus.MissingRequired => "Missing required evidence",
            PassportEvidenceStatus.MissingOptional => "Optional evidence missing",
            PassportEvidenceStatus.UploadedUnsigned => "Uploaded, not signed yet",
            PassportEvidenceStatus.Verified => "Verified in signed revision",
            PassportEvidenceStatus.ChangedSinceSigning => "Changed since signing",
            PassportEvidenceStatus.ExternalLinkOnly => "External link only",
            PassportEvidenceStatus.MissingFileReference => "Missing file hash",
            _ => "Evidence needs review"
        };
    }

    private static string RecommendedAction(string status, string label, bool isRequired)
    {
        return status switch
        {
            PassportEvidenceStatus.MissingRequired => $"Upload the required {label} file before signing.",
            PassportEvidenceStatus.MissingOptional => $"Optional {label} evidence is not uploaded.",
            PassportEvidenceStatus.UploadedUnsigned => $"Sign the passport so this {label} hash becomes trusted.",
            PassportEvidenceStatus.Verified => $"{label} matches the latest signed revision.",
            PassportEvidenceStatus.ChangedSinceSigning => $"Sign the passport again to trust the current {label} hash.",
            PassportEvidenceStatus.ExternalLinkOnly when isRequired => $"Upload a file for {label}; external links alone cannot be hash-verified.",
            PassportEvidenceStatus.ExternalLinkOnly => $"{label} is an external link without a stored hash.",
            PassportEvidenceStatus.MissingFileReference => $"Replace the {label} upload so the file hash can be checked.",
            _ => $"Review {label} evidence."
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private sealed record EvidenceDocumentDefinition(
        string DocumentKey,
        string Label,
        string FieldKey,
        string DataPath);
}
```

- [ ] **Step 5: Run evidence service tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~PassportEvidenceServiceTests /p:UseAppHost=false
```

Expected: all `PassportEvidenceServiceTests` pass.

- [ ] **Step 6: Commit Task 1**

Run:

```powershell
git add web\Models\Trust\PassportEvidenceModels.cs web\Services\PassportEvidenceService.cs BatteryPassWeb.Tests\PassportEvidenceServiceTests.cs
git commit -m "feat: add passport evidence evaluation"
```

---

### Task 2: Add Latest Signed Revision Lookup

**Files:**
- Modify: `web/Services/AuditRevisionService.cs`
- Modify: `BatteryPassWeb.Tests/AuditRevisionServiceTests.cs`

- [ ] **Step 1: Write source test for latest revision lookup**

Add this test to `BatteryPassWeb.Tests/AuditRevisionServiceTests.cs`:

```csharp
[Fact]
public void AuditRevisionService_ShouldExposeLatestSignedRevisionLookup()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "AuditRevisionService.cs"));

    Assert.Contains("GetLatestSignedRevisionAsync", source);
    Assert.Contains("passportRevisions", source);
    Assert.Contains("revisionNumber", source);
    Assert.Contains("status", source);
    Assert.Contains("published", source);
    Assert.Contains("signed", source);
}
```

- [ ] **Step 2: Run the failing source test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~AuditRevisionServiceTests.AuditRevisionService_ShouldExposeLatestSignedRevisionLookup /p:UseAppHost=false
```

Expected: fail because `GetLatestSignedRevisionAsync` is not present.

- [ ] **Step 3: Add latest revision helper**

In `web/Services/AuditRevisionService.cs`, add this method after `ListRevisionsAsync`:

```csharp
public async Task<BsonDocument?> GetLatestSignedRevisionAsync(
    string passportId,
    CancellationToken cancellationToken = default)
{
    var collection = GetPassportRevisionsCollection();
    if (collection == null || string.IsNullOrWhiteSpace(passportId))
    {
        return null;
    }

    var filter = Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Eq("passportId", passportId),
        Builders<BsonDocument>.Filter.In("status", new[] { "signed", "published" }));

    return await collection
        .Find(filter)
        .Sort(Builders<BsonDocument>.Sort.Descending("revisionNumber"))
        .Limit(1)
        .FirstOrDefaultAsync(cancellationToken);
}
```

- [ ] **Step 4: Run latest revision test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~AuditRevisionServiceTests.AuditRevisionService_ShouldExposeLatestSignedRevisionLookup /p:UseAppHost=false
```

Expected: pass.

- [ ] **Step 5: Commit Task 2**

Run:

```powershell
git add web\Services\AuditRevisionService.cs BatteryPassWeb.Tests\AuditRevisionServiceTests.cs
git commit -m "feat: add latest signed revision lookup"
```

---

### Task 3: Wire Evidence Into Validation Workflows

**Files:**
- Modify: `web/Program.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/PassportsApiController.cs`
- Modify: `web/Models/ViewModels/ConformanceViewModel.cs`
- Modify: `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`

- [ ] **Step 1: Write source tests for workflow wiring**

Add these tests to `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`:

```csharp
[Fact]
public void Program_ShouldRegisterPassportEvidenceService()
{
    var source = File.ReadAllText(RepoFile("web", "Program.cs"));

    Assert.Contains("AddSingleton<PassportEvidenceService>", source);
}

[Fact]
public void ConformanceModel_ShouldExposeEvidencePack()
{
    var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ConformanceViewModel.cs"));

    Assert.Contains("EvidencePackResult", source);
    Assert.Contains("EvidencePack", source);
}

[Fact]
public void AdminAndApiControllers_ShouldAppendEvidenceValidationSection()
{
    var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
    var api = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));

    Assert.Contains("PassportEvidenceService", admin);
    Assert.Contains("_passportEvidenceService.Evaluate", admin);
    Assert.Contains("AppendValidationSection", admin);
    Assert.Contains("EvidencePack", admin);

    Assert.Contains("PassportEvidenceService", api);
    Assert.Contains("_passportEvidenceService.Evaluate", api);
    Assert.Contains("AppendValidationSection", api);
}
```

- [ ] **Step 2: Run failing workflow source tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ConformanceLayoutTests.Program_ShouldRegisterPassportEvidenceService|FullyQualifiedName~ConformanceLayoutTests.ConformanceModel_ShouldExposeEvidencePack|FullyQualifiedName~ConformanceLayoutTests.AdminAndApiControllers_ShouldAppendEvidenceValidationSection" /p:UseAppHost=false
```

Expected: tests fail because evidence service is not registered or wired.

- [ ] **Step 3: Register evidence service**

In `web/Program.cs`, add:

```csharp
builder.Services.AddSingleton<PassportEvidenceService>();
```

Place it after:

```csharp
builder.Services.AddSingleton<PassportReadinessService>();
```

- [ ] **Step 4: Add evidence pack to conformance view model**

In `web/Models/ViewModels/ConformanceViewModel.cs`, keep the existing `using BatteryPassWeb.Models.Trust;` and add this property to `ConformanceViewModel`:

```csharp
public EvidencePackResult EvidencePack { get; init; } = EvidencePackResult.Empty;
```

- [ ] **Step 5: Inject service in admin controller**

In `web/Controllers/AdminController.cs`, add a field:

```csharp
private readonly PassportEvidenceService _passportEvidenceService;
```

Add the constructor parameter after `PassportReadinessService passportReadinessService`:

```csharp
PassportEvidenceService passportEvidenceService,
```

Assign it:

```csharp
_passportEvidenceService = passportEvidenceService;
```

- [ ] **Step 6: Add admin validation helper**

In `web/Controllers/AdminController.cs`, add this private method near the other private helpers:

```csharp
private async Task<(TrustValidationSummary Summary, EvidencePackResult EvidencePack)> ValidateWithEvidenceAsync(
    string passportId,
    BsonDocument document,
    DataCompletionPolicySnapshot dataRequirements,
    CancellationToken cancellationToken)
{
    var summary = _passportValidationService.Validate(document, dataRequirements);
    var latestRevision = await _auditRevisionService.GetLatestSignedRevisionAsync(passportId, cancellationToken);
    var evidencePack = _passportEvidenceService.Evaluate(document, latestRevision, dataRequirements);
    summary = PassportEvidenceService.AppendValidationSection(summary, evidencePack);
    return (summary, evidencePack);
}
```

- [ ] **Step 7: Use helper in admin conformance/validate/sign/publish**

In `Conformance`, replace:

```csharp
var summary = _passportValidationService.Validate(document, dataRequirements);
```

with:

```csharp
var validation = await ValidateWithEvidenceAsync(passportId, document, dataRequirements, cancellationToken);
var summary = validation.Summary;
var evidencePack = validation.EvidencePack;
```

Add this to the returned `ConformanceViewModel`:

```csharp
EvidencePack = evidencePack,
```

In `ValidatePassport`, `SignPassport`, and `PublishPassport`, replace each direct `_passportValidationService.Validate(document, dataRequirements)` call with:

```csharp
var validation = await ValidateWithEvidenceAsync(passportId, document, dataRequirements, cancellationToken);
var summary = validation.Summary;
```

In `CompleteRequiredData`, keep `_passportValidationService.Validate(completed, dataRequirements)` unchanged. Completing required demo data should not claim uploaded evidence exists.

- [ ] **Step 8: Inject service in API controller**

In `web/Controllers/PassportsApiController.cs`, add a field:

```csharp
private readonly PassportEvidenceService _passportEvidenceService;
```

Add constructor parameter after `DataCompletionPolicyService dataCompletionPolicyService`:

```csharp
PassportEvidenceService passportEvidenceService,
```

Assign it:

```csharp
_passportEvidenceService = passportEvidenceService;
```

- [ ] **Step 9: Add API validation helper and use it**

In `web/Controllers/PassportsApiController.cs`, add:

```csharp
private async Task<TrustValidationSummary> ValidateWithEvidenceAsync(
    string passportId,
    BsonDocument passport,
    DataCompletionPolicySnapshot dataRequirements,
    CancellationToken cancellationToken)
{
    var summary = _passportValidationService.Validate(passport, dataRequirements);
    var latestRevision = await _auditRevisionService.GetLatestSignedRevisionAsync(passportId, cancellationToken);
    var evidencePack = _passportEvidenceService.Evaluate(passport, latestRevision, dataRequirements);
    return PassportEvidenceService.AppendValidationSection(summary, evidencePack);
}
```

Replace the direct validation calls in API `Validate`, `Sign`, and `Publish` actions with:

```csharp
var summary = await ValidateWithEvidenceAsync(passportId, passport, dataRequirements, cancellationToken);
```

- [ ] **Step 10: Run workflow wiring tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ConformanceLayoutTests.Program_ShouldRegisterPassportEvidenceService|FullyQualifiedName~ConformanceLayoutTests.ConformanceModel_ShouldExposeEvidencePack|FullyQualifiedName~ConformanceLayoutTests.AdminAndApiControllers_ShouldAppendEvidenceValidationSection" /p:UseAppHost=false
```

Expected: pass.

- [ ] **Step 11: Commit Task 3**

Run:

```powershell
git add web\Program.cs web\Controllers\AdminController.cs web\Controllers\PassportsApiController.cs web\Models\ViewModels\ConformanceViewModel.cs BatteryPassWeb.Tests\ConformanceLayoutTests.cs
git commit -m "feat: wire evidence into trust validation"
```

---

### Task 4: Render Evidence Readiness On Conformance Page

**Files:**
- Modify: `web/Views/Admin/Conformance.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Modify: `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`

- [ ] **Step 1: Write layout test for evidence panel**

Add this test to `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`:

```csharp
[Fact]
public void ConformanceView_ShouldRenderEvidenceReadinessPanel()
{
    var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
    var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

    Assert.Contains("Evidence readiness", markup);
    Assert.Contains("Model.EvidencePack", markup);
    Assert.Contains("Missing required", markup);
    Assert.Contains("Changed since signing", markup);
    Assert.Contains("Uploaded unsigned", markup);
    Assert.Contains("Verified documents", markup);
    Assert.Contains("bp-evidence-panel", markup);
    Assert.Contains("bp-evidence-item", markup);
    Assert.Contains("bp-evidence-status", markup);

    Assert.Contains(".bp-evidence-panel", css);
    Assert.Contains(".bp-evidence-list", css);
    Assert.Contains(".bp-evidence-item", css);
    Assert.Contains(".bp-evidence-status", css);
}
```

- [ ] **Step 2: Run failing layout test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~ConformanceLayoutTests.ConformanceView_ShouldRenderEvidenceReadinessPanel /p:UseAppHost=false
```

Expected: fail because the panel and styles are not present.

- [ ] **Step 3: Add evidence variables to Razor**

At the top of `web/Views/Admin/Conformance.cshtml`, inside the existing Razor block, add:

```csharp
var evidencePack = Model.EvidencePack;
```

- [ ] **Step 4: Add evidence panel markup**

Insert this section after the readiness strip and before `bp-conformance-main-grid`:

```cshtml
<section class="bp-card bp-conformance-panel bp-evidence-panel">
    <div class="bp-panel-heading">
        <span class="bp-summary-title">Evidence readiness</span>
        <span class="bp-count-pill @(evidencePack.BlockingCount == 0 ? "is-ready" : string.Empty)">@evidencePack.BlockingCount blockers</span>
    </div>
    <h2>Supporting document evidence</h2>
    <p class="bp-help-text">Document hashes are checked against the latest signed revision. Replacing a file makes the passport dirty until it is signed again.</p>

    <div class="bp-evidence-metrics" aria-label="Evidence metrics">
        <div class="bp-readiness-card @(evidencePack.MissingRequiredCount == 0 ? "is-ready" : "is-blocked")">
            <p>Missing required</p>
            <strong>@evidencePack.MissingRequiredCount</strong>
            <span>Required supporting files that still need an upload.</span>
        </div>
        <div class="bp-readiness-card @(evidencePack.ChangedSinceSigningCount == 0 ? "is-ready" : "is-warning")">
            <p>Changed since signing</p>
            <strong>@evidencePack.ChangedSinceSigningCount</strong>
            <span>Uploaded hashes that differ from the latest signed revision.</span>
        </div>
        <div class="bp-readiness-card @(evidencePack.UploadedUnsignedCount == 0 ? "is-muted" : "is-warning")">
            <p>Uploaded unsigned</p>
            <strong>@evidencePack.UploadedUnsignedCount</strong>
            <span>Uploaded files waiting for the next signature.</span>
        </div>
        <div class="bp-readiness-card @(evidencePack.VerifiedCount == evidencePack.RequiredCount && evidencePack.RequiredCount > 0 ? "is-ready" : "is-muted")">
            <p>Verified documents</p>
            <strong>@evidencePack.VerifiedCount</strong>
            <span>Current hashes found in the latest signed revision.</span>
        </div>
    </div>

    <div class="bp-evidence-list">
        @foreach (var item in evidencePack.Items)
        {
            <article class="bp-evidence-item bp-evidence-status-@item.Status">
                <div class="bp-evidence-item-header">
                    <div>
                        <span>@(item.IsRequired ? "Required evidence" : "Optional evidence")</span>
                        <strong>@item.Label</strong>
                    </div>
                    <span class="bp-evidence-status">@item.StatusLabel</span>
                </div>
                <dl class="bp-evidence-details">
                    <div><dt>Visibility</dt><dd>@(item.Visibility == "public" ? "Public" : "Private / privileged")</dd></div>
                    <div><dt>Current hash</dt><dd><code>@(string.IsNullOrWhiteSpace(item.CurrentHash) ? "-" : item.CurrentHash)</code></dd></div>
                    <div><dt>Signed hash</dt><dd><code>@(string.IsNullOrWhiteSpace(item.SignedHash) ? "-" : item.SignedHash)</code></dd></div>
                    <div><dt>Next action</dt><dd>@item.RecommendedAction</dd></div>
                </dl>
                <a class="bp-secondary-button bp-button-compact" href="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/edit#admin-documents">Upload or replace</a>
            </article>
        }
    </div>
</section>
```

- [ ] **Step 5: Add evidence styles**

Append these styles near the existing conformance styles in `web/wwwroot/css/site.css`:

```css
.bp-evidence-panel {
  margin-top: 18px;
  border-color: #c7d7ea;
  background:
    radial-gradient(circle at top right, rgba(22, 163, 74, 0.1), transparent 30%),
    linear-gradient(135deg, #ffffff 0%, #f7fbff 100%);
}

.bp-evidence-metrics {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: 12px;
  margin-top: 16px;
}

.bp-evidence-list {
  display: grid;
  gap: 12px;
  margin-top: 16px;
}

.bp-evidence-item {
  display: grid;
  gap: 12px;
  border: 1px solid #d8e3ef;
  border-left: 4px solid #64748b;
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.92);
  padding: 14px;
}

.bp-evidence-status-missingRequired,
.bp-evidence-status-externalLinkOnly,
.bp-evidence-status-missingFileReference {
  border-left-color: #dc2626;
}

.bp-evidence-status-uploadedUnsigned,
.bp-evidence-status-changedSinceSigning {
  border-left-color: #d97706;
}

.bp-evidence-status-verified {
  border-left-color: #16a34a;
}

.bp-evidence-item-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.bp-evidence-item-header span:first-child {
  display: block;
  color: #64748b;
  font-size: 0.68rem;
  font-weight: 850;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.bp-evidence-item-header strong {
  display: block;
  margin-top: 2px;
  color: #041e42;
}

.bp-evidence-status {
  border-radius: 999px;
  background: #eef4fb;
  color: #35506f;
  font-size: 0.72rem;
  font-weight: 850;
  padding: 5px 10px;
  white-space: nowrap;
}

.bp-evidence-details {
  display: grid;
  gap: 8px;
  margin: 0;
}

.bp-evidence-details div {
  display: grid;
  gap: 3px;
}

.bp-evidence-details dt {
  color: #64748b;
  font-size: 0.68rem;
  font-weight: 850;
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.bp-evidence-details dd {
  margin: 0;
  color: #172033;
  font-size: 0.9rem;
  line-height: 1.4;
}

.bp-evidence-details code {
  white-space: normal;
  word-break: break-word;
}

@media (min-width: 900px) {
  .bp-evidence-metrics {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }

  .bp-evidence-details {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
```

- [ ] **Step 6: Run conformance layout test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~ConformanceLayoutTests.ConformanceView_ShouldRenderEvidenceReadinessPanel /p:UseAppHost=false
```

Expected: pass.

- [ ] **Step 7: Commit Task 4**

Run:

```powershell
git add web\Views\Admin\Conformance.cshtml web\wwwroot\css\site.css BatteryPassWeb.Tests\ConformanceLayoutTests.cs
git commit -m "feat: show evidence readiness on conformance"
```

---

### Task 5: Audit Document Upload And Replacement

**Files:**
- Modify: `web/Controllers/FilesApiController.cs`
- Modify: `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`

- [ ] **Step 1: Write source test for upload audit**

Add this test to `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`:

```csharp
[Fact]
public void FilesApiController_ShouldAuditDocumentUploadAndReplacement()
{
    var source = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));

    Assert.Contains("AppendFileUploadedAuditEventAsync", source);
    Assert.Contains("passport.file.uploaded", source);
    Assert.Contains("passport.file.replaced", source);
    Assert.Contains("previousFileId", source);
    Assert.Contains("sha256", source);
    Assert.Contains("supportingDocumentChanged", source);
}
```

- [ ] **Step 2: Run failing upload audit test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~DocumentAccessControlTests.FilesApiController_ShouldAuditDocumentUploadAndReplacement /p:UseAppHost=false
```

Expected: fail because upload audit helper is not present.

- [ ] **Step 3: Capture previous reference before upload**

In `web/Controllers/FilesApiController.cs`, after the access check and before content-type validation, add:

```csharp
var previousReference = ResolveDocumentReference(passport, normalizedDocumentKey, string.Empty);
var previousFileId = BsonHelpers.GetString(previousReference, "fileId");
```

- [ ] **Step 4: Append upload/replacement audit after dirty mark**

After:

```csharp
await _passportRepository.MarkCanonicalDirtyAsync(normalizedPassportId, "supportingDocumentChanged", cancellationToken);
```

add:

```csharp
await AppendFileUploadedAuditEventAsync(
    normalizedPassportId,
    uploadId.ToString(),
    previousFileId,
    normalizedDocumentKey,
    visibility,
    sha256,
    contentType,
    cancellationToken);
```

- [ ] **Step 5: Add upload audit helper**

Add this method near the existing audit helpers in `FilesApiController`:

```csharp
private async Task AppendFileUploadedAuditEventAsync(
    string passportId,
    string fileId,
    string previousFileId,
    string documentKey,
    string visibility,
    string sha256,
    string contentType,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(passportId))
    {
        return;
    }

    var isReplacement = !string.IsNullOrWhiteSpace(previousFileId)
        && !previousFileId.Equals(fileId, StringComparison.OrdinalIgnoreCase);

    await _auditRevisionService.AppendAuditEventAsync(
        passportId,
        isReplacement ? "passport.file.replaced" : "passport.file.uploaded",
        CurrentActor(),
        CurrentActorRole(),
        "files-api",
        isReplacement ? "Passport supporting file replaced." : "Passport supporting file uploaded.",
        new BsonDocument
        {
            ["fileId"] = fileId,
            ["previousFileId"] = previousFileId,
            ["documentKey"] = documentKey,
            ["visibility"] = visibility,
            ["sha256"] = sha256,
            ["contentType"] = contentType
        },
        cancellationToken);
}
```

- [ ] **Step 6: Run upload audit test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~DocumentAccessControlTests.FilesApiController_ShouldAuditDocumentUploadAndReplacement /p:UseAppHost=false
```

Expected: pass.

- [ ] **Step 7: Commit Task 5**

Run:

```powershell
git add web\Controllers\FilesApiController.cs BatteryPassWeb.Tests\DocumentAccessControlTests.cs
git commit -m "feat: audit passport evidence uploads"
```

---

### Task 6: Update Guidance And Acceptance Coverage

**Files:**
- Modify: `web/Views/Admin/Help.cshtml`
- Modify: `docs/end-user-testing-guide.md`
- Modify: `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`

- [ ] **Step 1: Write documentation source test**

Add this test to `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`:

```csharp
[Fact]
public void Documentation_ShouldExplainPhase6BEvidenceWorkflow()
{
    var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
    var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));

    Assert.Contains("Phase 6B", guide);
    Assert.Contains("Evidence readiness", guide);
    Assert.Contains("Upload or replace", guide);
    Assert.Contains("Changed since signing", guide);
    Assert.Contains("sign the passport again", guide, StringComparison.OrdinalIgnoreCase);

    Assert.Contains("Document evidence", help);
    Assert.Contains("Evidence readiness", help);
    Assert.Contains("document hash", help, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("sign again", help, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run failing documentation test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~Phase6ADocumentationTests.Documentation_ShouldExplainPhase6BEvidenceWorkflow /p:UseAppHost=false
```

Expected: fail because Phase 6B guidance is not documented.

- [ ] **Step 3: Update admin help**

In `web/Views/Admin/Help.cshtml`, inside `<section id="workflow-steps" class="bp-card">`, add this `article` inside the first `<div class="bp-admin-help-split-checklists">` immediately after the dirty recovery checklist article:

```html
<article class="bp-admin-help-check-card">
    <span class="bp-summary-title">Document evidence</span>
    <h2>How supporting files become trusted</h2>
    <ol>
        <li>Open the passport edit page and upload or replace the required supporting document.</li>
        <li>The app stores the document hash and marks the passport dirty because the signed evidence changed.</li>
        <li>Open Trust & conformance and review Evidence readiness.</li>
        <li>Validate the passport. Missing required evidence must be fixed before signing.</li>
        <li>Sign again so the current document hash is captured in a new immutable revision.</li>
        <li>Publish after the signature is clean and current.</li>
    </ol>
    <p>External links can describe evidence, but uploaded files with document hash values are what the app can verify against a signed revision.</p>
</article>
```

- [ ] **Step 4: Update end-user testing guide**

Add this section to `docs/end-user-testing-guide.md`:

```markdown
## Phase 6B Evidence Readiness Check

Use an admin account and a passport from the demo catalog.

1. Open `/admin/clusters?tab=passports`.
2. Open the passport edit page.
3. Upload or replace one required supporting document.
4. Open the passport conformance page.
5. Confirm the Evidence readiness panel shows the file as uploaded unsigned or changed since signing.
6. Validate the passport.
7. Sign the passport again.
8. Confirm the Evidence readiness panel shows the document as verified in the latest signed revision.
9. Publish when the readiness page shows the publish action.
10. Try a restricted document download from a public browser session and confirm it is denied.

Expected result: document hashes are visible to admins, changed evidence requires a new signature, and restricted files remain access-controlled.
```

- [ ] **Step 5: Run documentation test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter FullyQualifiedName~Phase6ADocumentationTests.Documentation_ShouldExplainPhase6BEvidenceWorkflow /p:UseAppHost=false
```

Expected: pass.

- [ ] **Step 6: Commit Task 6**

Run:

```powershell
git add web\Views\Admin\Help.cshtml docs\end-user-testing-guide.md BatteryPassWeb.Tests\Phase6ADocumentationTests.cs
git commit -m "docs: add evidence readiness guidance"
```

---

### Task 7: Final Verification

**Files:**
- Verify all touched files.

- [ ] **Step 1: Run targeted Phase 6B tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PassportEvidenceServiceTests|FullyQualifiedName~ConformanceLayoutTests|FullyQualifiedName~DocumentAccessControlTests|FullyQualifiedName~AuditRevisionServiceTests|FullyQualifiedName~Phase6ADocumentationTests" /p:UseAppHost=false
```

Expected: all targeted tests pass.

- [ ] **Step 2: Run full test suite with hang detection**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --blame-hang --blame-hang-timeout 60s --logger "console;verbosity=normal" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: all tests pass. The existing Snappier advisory warning may still appear.

- [ ] **Step 3: Run web build**

Run:

```powershell
dotnet build web\BatteryPassWeb.csproj /p:UseAppHost=false
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 4: Check git status**

Run:

```powershell
git status --short
```

Expected: clean working tree.

---

## Self-Review Checklist

- Spec coverage:
  - Evidence service and model are covered by Tasks 1 and 3.
  - Latest signed revision comparison is covered by Tasks 1 and 2.
  - Conformance evidence panel is covered by Task 4.
  - Upload/replacement audit events are covered by Task 5.
  - Admin help and testing guide updates are covered by Task 6.
  - Verification commands are covered by Task 7.
- Placeholder scan:
  - The plan contains no placeholder requirements, no unfinished task markers, and no deferred implementation language.
- Type consistency:
  - `EvidencePackResult`, `EvidenceItemResult`, `PassportEvidenceStatus`, `PassportEvidenceService`, and `AppendValidationSection` are defined before use.
  - Controller wiring uses the same `ValidateWithEvidenceAsync` pattern in admin and API controllers.
