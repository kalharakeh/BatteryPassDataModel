# Phase 6A End-to-End Demo Hardening And Acceptance Pack Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add repeatable demo scenario reset tooling, acceptance tests, and exact testing documentation for the complete Battery Pass trust workflow.

**Architecture:** Add a small scenario catalog and a guarded `DemoScenarioResetService` that builds only known demo passports, resets their audit/revision ledger safely, and drives existing validation/sign/publish services. Add an admin-only reset entry point and source/service acceptance tests that prove trust, QR, file, public visibility, and external HTTP dirty-state contracts without introducing a browser automation stack.

**Tech Stack:** ASP.NET Core MVC, C# / .NET 10, MongoDB `BsonDocument`, xUnit, Razor views, existing Battery Pass services.

---

## File Structure

- Create `web/Models/Demo/DemoScenarioModels.cs`: scenario constants, definitions, and reset result records.
- Create `web/Services/DemoScenarioResetService.cs`: builds/reset known demo scenarios only; orchestrates validation, signing, publishing, dirty, invalid-signature, and restricted-document states.
- Modify `web/Services/AuditRevisionService.cs`: add a targeted demo-ledger cleanup method for known passport IDs.
- Modify `web/Program.cs`: register `DemoScenarioResetService`.
- Modify `web/Controllers/AdminController.cs`: inject reset service, expose admin-only reset route, and pass reset status messages to admin pages.
- Modify `web/Views/Admin/Help.cshtml`: add a compact demo reset/admin testing affordance.
- Modify `web/wwwroot/css/site.css`: style the small reset affordance using existing admin-help card patterns.
- Modify `docs/sample-cluster-test-accounts.md`: document exact Phase 6A scenario IDs and accounts.
- Modify `docs/end-user-testing-guide.md`: add exact Phase 6A manual acceptance script.
- Create `BatteryPassWeb.Tests/DemoScenarioResetServiceTests.cs`: catalog, allow-list, scenario construction, and reset safety tests.
- Create `BatteryPassWeb.Tests/TrustWorkflowAcceptanceTests.cs`: validate/sign/publish/public-visibility contract tests.
- Create `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`: docs and admin-help coverage for exact scenario IDs and reset instructions.
- Modify `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`: add restricted-document acceptance assertions.
- Modify `BatteryPassWeb.Tests/QrWorkflowTests.cs`: add QR-public-summary acceptance assertions.
- Modify `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`: add external API non-dirty acceptance assertions.

## Demo Scenario IDs

Use these exact IDs throughout the service, tests, and docs:

```text
did:web:acme.battery.pass:demo-published-trusted-001
did:web:acme.battery.pass:demo-draft-incomplete-001
did:web:acme.battery.pass:demo-ready-to-sign-001
did:web:acme.battery.pass:demo-signed-unpublished-001
did:web:acme.battery.pass:demo-dirty-after-edit-001
did:web:acme.battery.pass:demo-invalid-signature-001
did:web:acme.battery.pass:demo-restricted-document-001
```

Use `cluster-north-operations` for all scenario passports so the existing north demo accounts can test authorized access.

---

## Task 1: Demo Scenario Catalog Models

**Files:**
- Create: `web/Models/Demo/DemoScenarioModels.cs`
- Test: `BatteryPassWeb.Tests/DemoScenarioResetServiceTests.cs`

- [ ] **Step 1: Write the failing catalog tests**

Create `BatteryPassWeb.Tests/DemoScenarioResetServiceTests.cs`:

```csharp
using BatteryPassWeb.Models.Demo;

namespace BatteryPassWeb.Tests;

public sealed class DemoScenarioResetServiceTests
{
    [Fact]
    public void ScenarioCatalog_ShouldExposeEveryPhase6ADemoState()
    {
        var scenarios = DemoScenarioCatalog.All;

        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.PublishedTrusted);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.DraftIncomplete);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.ReadyToSign);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.SignedUnpublished);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.DirtyAfterEdit);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.InvalidSignature);
        Assert.Contains(scenarios, scenario => scenario.Key == DemoScenarioKey.RestrictedDocument);
        Assert.Equal(7, scenarios.Select(scenario => scenario.PassportId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ScenarioCatalog_ShouldRefuseUnknownPassportIds()
    {
        Assert.True(DemoScenarioCatalog.IsKnownPassportId("did:web:acme.battery.pass:demo-published-trusted-001"));
        Assert.False(DemoScenarioCatalog.IsKnownPassportId("did:web:acme.battery.pass:user-created-production-id"));
    }

    [Fact]
    public void ScenarioCatalog_ShouldUseNorthClusterForAuthorizedDemoAccess()
    {
        Assert.All(DemoScenarioCatalog.All, scenario =>
            Assert.Equal(DemoScenarioCatalog.DefaultClusterId, scenario.ClusterId));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter DemoScenarioResetServiceTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: FAIL with compiler errors because `BatteryPassWeb.Models.Demo` and `DemoScenarioCatalog` do not exist.

- [ ] **Step 3: Add demo scenario models**

Create `web/Models/Demo/DemoScenarioModels.cs`:

```csharp
namespace BatteryPassWeb.Models.Demo;

public static class DemoScenarioKey
{
    public const string PublishedTrusted = "publishedTrusted";
    public const string DraftIncomplete = "draftIncomplete";
    public const string ReadyToSign = "readyToSign";
    public const string SignedUnpublished = "signedUnpublished";
    public const string DirtyAfterEdit = "dirtyAfterEdit";
    public const string InvalidSignature = "invalidSignature";
    public const string RestrictedDocument = "restrictedDocument";
}

public sealed record DemoScenarioDefinition(
    string Key,
    string PassportId,
    string Label,
    string ExpectedState,
    string ClusterId);

public sealed record DemoScenarioResetResult(
    int ResetCount,
    IReadOnlyList<string> PassportIds,
    string ResetAt);

public static class DemoScenarioCatalog
{
    public const string DefaultClusterId = "cluster-north-operations";
    public const string PublishedTrustedPassportId = "did:web:acme.battery.pass:demo-published-trusted-001";
    public const string DraftIncompletePassportId = "did:web:acme.battery.pass:demo-draft-incomplete-001";
    public const string ReadyToSignPassportId = "did:web:acme.battery.pass:demo-ready-to-sign-001";
    public const string SignedUnpublishedPassportId = "did:web:acme.battery.pass:demo-signed-unpublished-001";
    public const string DirtyAfterEditPassportId = "did:web:acme.battery.pass:demo-dirty-after-edit-001";
    public const string InvalidSignaturePassportId = "did:web:acme.battery.pass:demo-invalid-signature-001";
    public const string RestrictedDocumentPassportId = "did:web:acme.battery.pass:demo-restricted-document-001";

    public static IReadOnlyList<DemoScenarioDefinition> All { get; } =
    [
        new(DemoScenarioKey.PublishedTrusted, PublishedTrustedPassportId, "Published trusted", "Published, signed, clean, public, QR-ready", DefaultClusterId),
        new(DemoScenarioKey.DraftIncomplete, DraftIncompletePassportId, "Draft incomplete", "Missing required data, blocked from signing", DefaultClusterId),
        new(DemoScenarioKey.ReadyToSign, ReadyToSignPassportId, "Ready to sign", "Complete and validated, unsigned", DefaultClusterId),
        new(DemoScenarioKey.SignedUnpublished, SignedUnpublishedPassportId, "Signed unpublished", "Signed and clean, not public yet", DefaultClusterId),
        new(DemoScenarioKey.DirtyAfterEdit, DirtyAfterEditPassportId, "Dirty after edit", "Signed core changed after proof", DefaultClusterId),
        new(DemoScenarioKey.InvalidSignature, InvalidSignaturePassportId, "Invalid signature", "Proof diagnostics fail verification", DefaultClusterId),
        new(DemoScenarioKey.RestrictedDocument, RestrictedDocumentPassportId, "Restricted document access", "Published trusted with restricted evidence metadata", DefaultClusterId)
    ];

    public static bool IsKnownPassportId(string passportId)
    {
        return All.Any(scenario => scenario.PassportId.Equals(passportId, StringComparison.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter DemoScenarioResetServiceTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS for the catalog tests.

- [ ] **Step 5: Commit**

```powershell
git add web\Models\Demo\DemoScenarioModels.cs BatteryPassWeb.Tests\DemoScenarioResetServiceTests.cs
git commit -m "feat: add demo scenario catalog"
```

---

## Task 2: Targeted Demo Ledger Cleanup

**Files:**
- Modify: `web/Services/AuditRevisionService.cs`
- Test: `BatteryPassWeb.Tests/DemoScenarioResetServiceTests.cs`

- [ ] **Step 1: Add failing source test for targeted cleanup**

Append this test to `BatteryPassWeb.Tests/DemoScenarioResetServiceTests.cs`:

```csharp
[Fact]
public void AuditRevisionService_ShouldExposeTargetedDemoLedgerCleanupOnly()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "AuditRevisionService.cs"));

    Assert.Contains("DeleteDemoLedgerAsync", source);
    Assert.Contains("Builders<BsonDocument>.Filter.In(\"passportId\"", source);
    Assert.DoesNotContain("DeleteManyAsync(Builders<BsonDocument>.Filter.Empty", source);
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
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter AuditRevisionService_ShouldExposeTargetedDemoLedgerCleanupOnly /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: FAIL because `DeleteDemoLedgerAsync` does not exist.

- [ ] **Step 3: Add targeted cleanup method**

In `web/Services/AuditRevisionService.cs`, add this public method after `MarkRevisionPublishedAsync`:

```csharp
public async Task DeleteDemoLedgerAsync(
    IReadOnlyCollection<string> passportIds,
    CancellationToken cancellationToken = default)
{
    var filteredPassportIds = passportIds
        .Where(passportId => !string.IsNullOrWhiteSpace(passportId))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (filteredPassportIds.Length == 0)
    {
        return;
    }

    var revisionCollection = GetPassportRevisionsCollection();
    if (revisionCollection != null)
    {
        await revisionCollection.DeleteManyAsync(
            Builders<BsonDocument>.Filter.In("passportId", filteredPassportIds),
            cancellationToken);
    }

    var auditCollection = GetAuditEventsCollection();
    if (auditCollection != null)
    {
        await auditCollection.DeleteManyAsync(
            Builders<BsonDocument>.Filter.In("passportId", filteredPassportIds),
            cancellationToken);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "DemoScenarioResetServiceTests" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add web\Services\AuditRevisionService.cs BatteryPassWeb.Tests\DemoScenarioResetServiceTests.cs
git commit -m "feat: add targeted demo ledger cleanup"
```

---

## Task 3: Demo Scenario Reset Service

**Files:**
- Create: `web/Services/DemoScenarioResetService.cs`
- Modify: `web/Program.cs`
- Test: `BatteryPassWeb.Tests/DemoScenarioResetServiceTests.cs`

- [ ] **Step 1: Add failing source tests for reset service and registration**

Append these tests to `BatteryPassWeb.Tests/DemoScenarioResetServiceTests.cs`:

```csharp
[Fact]
public void DemoScenarioResetService_ShouldBuildEveryTrustScenarioExplicitly()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "DemoScenarioResetService.cs"));

    Assert.Contains("ResetAllAsync", source);
    Assert.Contains("BuildBaseScenarioDocument", source);
    Assert.Contains("ApplyValidatedStateAsync", source);
    Assert.Contains("ApplySignedStateAsync", source);
    Assert.Contains("ApplyPublishedStateAsync", source);
    Assert.Contains("ApplyDirtyStateAsync", source);
    Assert.Contains("ApplyInvalidSignatureStateAsync", source);
    Assert.Contains("ApplyRestrictedDocumentMetadata", source);
    Assert.Contains("DemoScenarioCatalog.All", source);
    Assert.Contains("DemoScenarioCatalog.IsKnownPassportId", source);
}

[Fact]
public void Program_ShouldRegisterDemoScenarioResetService()
{
    var program = File.ReadAllText(RepoFile("web", "Program.cs"));

    Assert.Contains("AddSingleton<DemoScenarioResetService>", program);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "DemoScenarioResetService_ShouldBuildEveryTrustScenarioExplicitly|Program_ShouldRegisterDemoScenarioResetService" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: FAIL because the service file and registration do not exist.

- [ ] **Step 3: Add reset service**

Create `web/Services/DemoScenarioResetService.cs`:

```csharp
using BatteryPassWeb.Models.Demo;
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class DemoScenarioResetService
{
    private readonly PassportRepository _passportRepository;
    private readonly DataCompletionPolicyService _dataCompletionPolicyService;
    private readonly DemoRequiredDataCompletionService _demoRequiredDataCompletionService;
    private readonly PassportValidationService _passportValidationService;
    private readonly PassportTrustService _passportTrustService;
    private readonly AuditRevisionService _auditRevisionService;

    public DemoScenarioResetService(
        PassportRepository passportRepository,
        DataCompletionPolicyService dataCompletionPolicyService,
        DemoRequiredDataCompletionService demoRequiredDataCompletionService,
        PassportValidationService passportValidationService,
        PassportTrustService passportTrustService,
        AuditRevisionService auditRevisionService)
    {
        _passportRepository = passportRepository;
        _dataCompletionPolicyService = dataCompletionPolicyService;
        _demoRequiredDataCompletionService = demoRequiredDataCompletionService;
        _passportValidationService = passportValidationService;
        _passportTrustService = passportTrustService;
        _auditRevisionService = auditRevisionService;
    }

    public IReadOnlyList<DemoScenarioDefinition> ListScenarios()
    {
        return DemoScenarioCatalog.All;
    }

    public async Task<DemoScenarioResetResult> ResetAllAsync(
        string actor,
        CancellationToken cancellationToken = default)
    {
        var resetAt = DateTimeOffset.UtcNow.ToString("O");
        var passportIds = DemoScenarioCatalog.All.Select(scenario => scenario.PassportId).ToArray();
        foreach (var passportId in passportIds)
        {
            if (!DemoScenarioCatalog.IsKnownPassportId(passportId))
            {
                throw new InvalidOperationException($"Demo reset refused unknown passport ID '{passportId}'.");
            }
        }

        await _auditRevisionService.DeleteDemoLedgerAsync(passportIds, cancellationToken);
        foreach (var scenario in DemoScenarioCatalog.All)
        {
            await ResetScenarioAsync(scenario, actor, resetAt, cancellationToken);
        }

        return new DemoScenarioResetResult(passportIds.Length, passportIds, resetAt);
    }

    private async Task ResetScenarioAsync(
        DemoScenarioDefinition scenario,
        string actor,
        string resetAt,
        CancellationToken cancellationToken)
    {
        if (!DemoScenarioCatalog.IsKnownPassportId(scenario.PassportId))
        {
            throw new InvalidOperationException($"Demo reset refused unknown passport ID '{scenario.PassportId}'.");
        }

        var policy = await _dataCompletionPolicyService.GetPolicyAsync(cancellationToken);
        var document = BuildBaseScenarioDocument(scenario, resetAt);

        if (scenario.Key != DemoScenarioKey.DraftIncomplete)
        {
            document = _demoRequiredDataCompletionService.CompleteRequiredData(document, resetAt);
            document["passportId"] = scenario.PassportId;
            document["clusterId"] = scenario.ClusterId;
            EnsureDisplay(document, scenario);
        }

        if (scenario.Key == DemoScenarioKey.RestrictedDocument)
        {
            ApplyRestrictedDocumentMetadata(document, resetAt);
        }

        await _passportRepository.ReplaceAsync(scenario.PassportId, document, cancellationToken);

        if (scenario.Key is DemoScenarioKey.ReadyToSign)
        {
            await ApplyValidatedStateAsync(scenario.PassportId, policy, cancellationToken);
        }
        else if (scenario.Key is DemoScenarioKey.SignedUnpublished)
        {
            await ApplySignedStateAsync(scenario.PassportId, policy, actor, cancellationToken);
        }
        else if (scenario.Key is DemoScenarioKey.PublishedTrusted or DemoScenarioKey.RestrictedDocument)
        {
            await ApplySignedStateAsync(scenario.PassportId, policy, actor, cancellationToken);
            await ApplyPublishedStateAsync(scenario.PassportId, cancellationToken);
        }
        else if (scenario.Key is DemoScenarioKey.DirtyAfterEdit)
        {
            await ApplySignedStateAsync(scenario.PassportId, policy, actor, cancellationToken);
            await ApplyPublishedStateAsync(scenario.PassportId, cancellationToken);
            await ApplyDirtyStateAsync(scenario.PassportId, cancellationToken);
        }
        else if (scenario.Key is DemoScenarioKey.InvalidSignature)
        {
            await ApplySignedStateAsync(scenario.PassportId, policy, actor, cancellationToken);
            await ApplyInvalidSignatureStateAsync(scenario.PassportId, cancellationToken);
        }

        await _auditRevisionService.AppendAuditEventAsync(
            scenario.PassportId,
            "demo.scenario.reset",
            actor,
            "admin",
            "admin-ui",
            $"Demo scenario reset: {scenario.Label}.",
            new BsonDocument
            {
                ["scenarioKey"] = scenario.Key,
                ["expectedState"] = scenario.ExpectedState,
                ["resetAt"] = resetAt
            },
            cancellationToken);
    }

    private static BsonDocument BuildBaseScenarioDocument(DemoScenarioDefinition scenario, string now)
    {
        return new BsonDocument
        {
            ["passportId"] = scenario.PassportId,
            ["clusterId"] = scenario.ClusterId,
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = scenario.PassportId.Split(':').Last(),
                ["status"] = "draft",
                ["createdAt"] = now,
                ["updatedAt"] = now
            },
            ["app"] = new BsonDocument
            {
                ["demoScenario"] = new BsonDocument
                {
                    ["key"] = scenario.Key,
                    ["label"] = scenario.Label,
                    ["expectedState"] = scenario.ExpectedState
                },
                ["display"] = new BsonDocument
                {
                    ["name"] = $"Phase 6A - {scenario.Label}",
                    ["modelNumber"] = $"P6A-{scenario.Key}",
                    ["serialNumber"] = $"SN-P6A-{scenario.Key}",
                    ["manufacturerName"] = "Demo Batteries GmbH",
                    ["facilityId"] = "NORTH-DEMO-FACILITY"
                },
                ["media"] = new BsonDocument
                {
                    ["batteryImageUrl"] = BatteryImageCatalog.DefaultImageUrl
                },
                ["operations"] = new BsonDocument
                {
                    ["isActive"] = true,
                    ["lastUpdatedAt"] = now
                }
            },
            ["aspects"] = new BsonDocument(),
            ["validation"] = new BsonDocument
            {
                ["isValid"] = false,
                ["status"] = "unvalidated"
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = TrustState.Unvalidated,
                ["isDirty"] = false,
                ["latestHash"] = string.Empty,
                ["latestProof"] = new BsonDocument()
            }
        };
    }

    private static void EnsureDisplay(BsonDocument document, DemoScenarioDefinition scenario)
    {
        var app = EnsureDocument(document, "app");
        var display = EnsureDocument(app, "display");
        display["name"] = $"Phase 6A - {scenario.Label}";
        display["modelNumber"] = $"P6A-{scenario.Key}";
        display["serialNumber"] = $"SN-P6A-{scenario.Key}";
        display["manufacturerName"] = "Demo Batteries GmbH";
        display["facilityId"] = "NORTH-DEMO-FACILITY";
        app["demoScenario"] = new BsonDocument
        {
            ["key"] = scenario.Key,
            ["label"] = scenario.Label,
            ["expectedState"] = scenario.ExpectedState
        };
    }

    private async Task ApplyValidatedStateAsync(
        string passportId,
        DataCompletionPolicySnapshot policy,
        CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken)
            ?? throw new InvalidOperationException($"Demo passport '{passportId}' was not found for validation.");
        var summary = _passportValidationService.Validate(document, policy);
        await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
    }

    private async Task ApplySignedStateAsync(
        string passportId,
        DataCompletionPolicySnapshot policy,
        string actor,
        CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken)
            ?? throw new InvalidOperationException($"Demo passport '{passportId}' was not found for signing.");
        var summary = _passportValidationService.Validate(document, policy);
        if (summary.BlockingErrorCount > 0)
        {
            throw new InvalidOperationException($"Demo passport '{passportId}' cannot be signed because it has {summary.BlockingErrorCount} blocking validation errors.");
        }

        var signature = _passportTrustService.Sign(document, actor);
        var revision = await _auditRevisionService.CreateSignedRevisionAsync(
            passportId,
            signature.Snapshot,
            signature.Hash,
            signature.Proof,
            actor,
            signature.SignedAt,
            cancellationToken);

        var revisionId = BsonHelpers.GetString(revision, "revisionId");
        var updated = await _passportRepository.UpdateTrustSignatureAsync(
            passportId,
            summary,
            signature.Hash,
            signature.Proof,
            revisionId,
            signature.SignedAt,
            cancellationToken);
        if (!updated)
        {
            throw new InvalidOperationException($"Demo passport '{passportId}' signature state could not be persisted.");
        }
    }

    private async Task ApplyPublishedStateAsync(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken)
            ?? throw new InvalidOperationException($"Demo passport '{passportId}' was not found for publishing.");
        var verification = _passportTrustService.Verify(document);
        if (!verification.IsValid)
        {
            throw new InvalidOperationException($"Demo passport '{passportId}' cannot be published: {verification.Message}");
        }

        var revisionId = BsonHelpers.GetString(document, "trust", "latestRevisionId");
        var proof = BsonHelpers.GetValue(document, "trust", "latestProof") as BsonDocument ?? new BsonDocument();
        var publishedAt = DateTimeOffset.UtcNow.ToString("O");
        await _passportRepository.PublishPassportAsync(passportId, revisionId, publishedAt, verification.CurrentHash, proof, cancellationToken);
        await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, publishedAt, cancellationToken);
    }

    private async Task ApplyDirtyStateAsync(string passportId, CancellationToken cancellationToken)
    {
        await _passportRepository.UpdateFieldsAsync(
            passportId,
            new Dictionary<string, BsonValue>
            {
                ["app.display.modelNumber"] = "P6A-DIRTY-EDITED"
            },
            cancellationToken);
        await _passportRepository.MarkCanonicalDirtyAsync(passportId, "phase6aDemoSignedCoreEdit", cancellationToken);
    }

    private async Task ApplyInvalidSignatureStateAsync(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken)
            ?? throw new InvalidOperationException($"Demo passport '{passportId}' was not found for invalid-signature mutation.");
        var trust = EnsureDocument(document, "trust");
        var proof = trust.GetValue("latestProof", new BsonDocument()) as BsonDocument ?? new BsonDocument();
        proof["proofValue"] = "invalid-demo-proof-value";
        trust["latestProof"] = proof;
        trust["state"] = TrustState.SignatureInvalid;
        trust["isDirty"] = false;
        document.Remove("_id");
        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
    }

    private static void ApplyRestrictedDocumentMetadata(BsonDocument document, string now)
    {
        var app = EnsureDocument(document, "app");
        var documents = EnsureDocument(app, "documents");
        documents["dueDiligenceReport"] = new BsonDocument
        {
            ["label"] = "Restricted due diligence report",
            ["url"] = "https://example.test/restricted-due-diligence-report.pdf",
            ["contentType"] = "application/pdf",
            ["sha256"] = "phase6a-demo-restricted-document-hash",
            ["visibility"] = "restricted",
            ["uploadedAt"] = now
        };
    }

    private static BsonDocument EnsureDocument(BsonDocument parent, string key)
    {
        if (!parent.TryGetValue(key, out var value) || value is not BsonDocument document)
        {
            document = new BsonDocument();
            parent[key] = document;
        }

        return document;
    }
}
```

- [ ] **Step 4: Register service**

In `web/Program.cs`, add after `DemoRequiredDataCompletionService`:

```csharp
builder.Services.AddSingleton<DemoScenarioResetService>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter DemoScenarioResetServiceTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add web\Services\DemoScenarioResetService.cs web\Program.cs BatteryPassWeb.Tests\DemoScenarioResetServiceTests.cs
git commit -m "feat: add demo scenario reset service"
```

---

## Task 4: Admin Reset Entry Point

**Files:**
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Views/Admin/Help.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Test: `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`

- [ ] **Step 1: Write failing admin route and UI tests**

Create `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class Phase6ADocumentationTests
{
    [Fact]
    public void AdminController_ShouldExposeAdminOnlyDemoScenarioReset()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("DemoScenarioResetService", source);
        Assert.Contains("[HttpPost(\"demo-scenarios/reset\")]", source);
        Assert.Contains("ResetDemoScenarios", source);
        Assert.Contains("_demoScenarioResetService.ResetAllAsync", source);
        Assert.Contains("Demo scenarios reset", source);
    }

    [Fact]
    public void AdminHelp_ShouldShowCompactPhase6ADemoResetAction()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Phase 6A demo reset", markup);
        Assert.Contains("/admin/demo-scenarios/reset", markup);
        Assert.Contains("bp-admin-help-demo-reset", markup);
        Assert.Contains(".bp-admin-help-demo-reset", css);
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

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter Phase6ADocumentationTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: FAIL because route, UI, and styles do not exist.

- [ ] **Step 3: Inject reset service into AdminController**

In `web/Controllers/AdminController.cs`, add this field near the other service fields:

```csharp
private readonly DemoScenarioResetService _demoScenarioResetService;
```

Add the constructor parameter after `DemoRequiredDataCompletionService demoRequiredDataCompletionService`:

```csharp
DemoScenarioResetService demoScenarioResetService,
```

Assign it in the constructor:

```csharp
_demoScenarioResetService = demoScenarioResetService;
```

- [ ] **Step 4: Add admin reset action**

In `web/Controllers/AdminController.cs`, add this action before the `Clusters` action:

```csharp
[HttpPost("demo-scenarios/reset")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ResetDemoScenarios(CancellationToken cancellationToken)
{
    try
    {
        var result = await _demoScenarioResetService.ResetAllAsync(CurrentActor(), cancellationToken);
        return Redirect($"/admin/help?status={Uri.EscapeDataString($"Demo scenarios reset: {result.ResetCount} passports restored.")}");
    }
    catch (Exception exception) when (IsTrustPersistenceFailure(exception))
    {
        return Redirect($"/admin/help?error={Uri.EscapeDataString($"{TrustWorkflowServiceErrorMessage} {exception.Message}")}");
    }
    catch (InvalidOperationException exception)
    {
        return Redirect($"/admin/help?error={Uri.EscapeDataString(exception.Message)}");
    }
}
```

Update the existing `Help` action to accept status/error query values:

```csharp
[HttpGet("help")]
public IActionResult Help([FromQuery] string? status, [FromQuery] string? error)
{
    ViewData["StatusMessage"] = string.IsNullOrWhiteSpace(status) ? string.Empty : Uri.UnescapeDataString(status);
    ViewData["ErrorMessage"] = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error);
    return View();
}
```

- [ ] **Step 5: Add compact reset affordance to admin help**

In `web/Views/Admin/Help.cshtml`, add this block after the page-level return link and before the hero section:

```cshtml
@{
    var statusMessage = ViewData["StatusMessage"] as string ?? string.Empty;
    var errorMessage = ViewData["ErrorMessage"] as string ?? string.Empty;
}

@if (!string.IsNullOrWhiteSpace(statusMessage))
{
    <div class="bp-alert bp-alert-success">@statusMessage</div>
}
@if (!string.IsNullOrWhiteSpace(errorMessage))
{
    <div class="bp-alert bp-alert-danger">@errorMessage</div>
}

<section class="bp-admin-help-demo-reset" aria-label="Phase 6A demo reset">
    <div>
        <span class="bp-summary-title">Phase 6A demo reset</span>
        <strong>Restore the known demo passports before a guided test run.</strong>
        <p>Only the allow-listed Phase 6A demo IDs are reset. User-created passports are not touched.</p>
    </div>
    <form method="post" action="/admin/demo-scenarios/reset">
        @Html.AntiForgeryToken()
        <button type="submit" class="bp-secondary-button bp-button-accent">Reset demo scenarios</button>
    </form>
</section>
```

- [ ] **Step 6: Add small CSS**

In `web/wwwroot/css/site.css`, near other `.bp-admin-help-*` styles, add:

```css
.bp-admin-help-demo-reset {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  border: 1px solid #d9e4f2;
  border-radius: 22px;
  background: linear-gradient(135deg, #f7fbff 0%, #ffffff 58%, #fff8ed 100%);
  box-shadow: 0 14px 36px rgba(15, 42, 73, 0.07);
  padding: 1rem 1.1rem;
}

.bp-admin-help-demo-reset p {
  color: #5f7087;
  margin: 0.35rem 0 0;
}

@media (max-width: 720px) {
  .bp-admin-help-demo-reset {
    align-items: flex-start;
    flex-direction: column;
  }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter Phase6ADocumentationTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add web\Controllers\AdminController.cs web\Views\Admin\Help.cshtml web\wwwroot\css\site.css BatteryPassWeb.Tests\Phase6ADocumentationTests.cs
git commit -m "feat: add admin demo scenario reset action"
```

---

## Task 5: Trust Workflow Acceptance Tests

**Files:**
- Create: `BatteryPassWeb.Tests/TrustWorkflowAcceptanceTests.cs`

- [ ] **Step 1: Write failing acceptance tests**

Create `BatteryPassWeb.Tests/TrustWorkflowAcceptanceTests.cs`:

```csharp
using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class TrustWorkflowAcceptanceTests
{
    [Fact]
    public void IncompletePassport_ShouldNotBeSignableOrPubliclyVisible()
    {
        var validation = CreateValidationService().Validate(new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:acceptance-incomplete",
            ["registryInfo"] = new BsonDocument { ["status"] = "draft" },
            ["app"] = new BsonDocument { ["display"] = new BsonDocument() }
        });

        var policy = new PassportPublishPolicyService();
        var passport = new BsonDocument
        {
            ["registryInfo"] = new BsonDocument { ["status"] = "published" },
            ["validation"] = new BsonDocument { ["isValid"] = false },
            ["trust"] = new BsonDocument { ["state"] = TrustState.Invalid }
        };

        Assert.True(validation.BlockingErrorCount > 0);
        Assert.False(policy.CanSign(validation));
        Assert.False(policy.IsPubliclyVisible(passport));
    }

    [Fact]
    public void SignedCleanPublishedPassport_ShouldBePubliclyVisible()
    {
        var passport = PublishedSignedPassport();

        Assert.True(new PassportPublishPolicyService().IsPubliclyVisible(passport));
    }

    [Fact]
    public void DraftUnsignedDirtyOrInvalidSignaturePassports_ShouldNotBePubliclyVisible()
    {
        var policy = new PassportPublishPolicyService();

        Assert.False(policy.IsPubliclyVisible(PublishedSignedPassport(status: "draft")));
        Assert.False(policy.IsPubliclyVisible(PublishedSignedPassport(hasProof: false)));
        Assert.False(policy.IsPubliclyVisible(PublishedSignedPassport(isDirty: true)));
        Assert.False(policy.IsPubliclyVisible(PublishedSignedPassport(trustState: TrustState.SignatureInvalid)));
    }

    [Fact]
    public void SignedCleanUnpublishedPassport_ShouldBePublishableByPolicy()
    {
        var summary = new TrustValidationSummary
        {
            State = TrustState.Valid,
            Sections =
            [
                new TrustValidationSectionResult
                {
                    SectionKey = "identity",
                    SectionLabel = "Identity",
                    Issues = [new TrustValidationIssue(TrustValidationSeverity.Passed, "identity", "ok")]
                }
            ]
        };

        var decision = new PassportPublishPolicyService().Evaluate(PublishedSignedPassport(status: "draft"), summary);

        Assert.True(decision.CanSign);
        Assert.True(decision.CanPublish);
    }

    private static PassportValidationService CreateValidationService()
    {
        return new PassportValidationService(new SchemaRegistryService(), new JsonSchemaValidationService());
    }

    private static BsonDocument PublishedSignedPassport(
        string status = "published",
        string trustState = TrustState.Signed,
        bool isDirty = false,
        bool hasProof = true)
    {
        var hash = "sha256-acceptance";
        return new BsonDocument
        {
            ["registryInfo"] = new BsonDocument { ["status"] = status },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = true,
                ["hash"] = hash
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = trustState,
                ["isDirty"] = isDirty,
                ["latestHash"] = hash,
                ["latestProof"] = hasProof
                    ? new BsonDocument { ["proofValue"] = "acceptance-proof" }
                    : new BsonDocument()
            }
        };
    }
}
```

- [ ] **Step 2: Run acceptance tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter TrustWorkflowAcceptanceTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS if existing trust policy behavior is correct. If a test fails, fix the production service behavior rather than weakening the test.

- [ ] **Step 3: Commit**

```powershell
git add BatteryPassWeb.Tests\TrustWorkflowAcceptanceTests.cs
git commit -m "test: add trust workflow acceptance coverage"
```

---

## Task 6: QR, Document, And External API Acceptance Coverage

**Files:**
- Modify: `BatteryPassWeb.Tests/QrWorkflowTests.cs`
- Modify: `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`
- Modify: `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`

- [ ] **Step 1: Add QR acceptance assertion**

Append this test to `BatteryPassWeb.Tests/QrWorkflowTests.cs`:

```csharp
[Fact]
public void QrService_ShouldBuildPublicSummaryPayloadForDemoScenarioPassport()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "PassportQrCodeService.cs"));
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));

    Assert.Contains("/summary", source);
    Assert.Contains("BuildPayloadUrl", source);
    Assert.Contains("IsPubliclyVisible", controller);
    Assert.Contains("CanOpenPassportDetailAsync", controller);
}
```

- [ ] **Step 2: Add document access acceptance assertion**

Append this test to `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`:

```csharp
[Fact]
public void RestrictedDocumentAcceptance_ShouldDenyUnauthorizedDownloadAndAuditIt()
{
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));

    Assert.Contains("visibility", controller);
    Assert.Contains("restricted", controller);
    Assert.Contains("CanOpenPassportDetailAsync", controller);
    Assert.Contains("StatusCodes.Status403Forbidden", controller);
    Assert.Contains("passport.file.download.denied", controller);
}
```

- [ ] **Step 3: Add external API non-dirty acceptance assertion**

Append this test to `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`:

```csharp
[Fact]
public void ExternalApiAcceptance_ShouldOnlyWriteOperationalFieldsWithoutCallingDirtyMarker()
{
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

    Assert.Contains("app.operations.latestTelemetry", controller);
    Assert.Contains("app.operations.locationOfUse", controller);
    Assert.Contains("app.operations.contactPerson", controller);
    Assert.DoesNotContain("MarkCanonicalDirtyAsync", controller);
    Assert.DoesNotContain("trust.isDirty", controller);
}
```

- [ ] **Step 4: Run targeted tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "QrWorkflowTests|DocumentAccessControlTests|TrustDirtyStateTests" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS. If a test fails because an existing assertion helper is missing `RepoFile`, use the helper already present in that test file or add the same helper pattern used in `AdminHelpPageTests`.

- [ ] **Step 5: Commit**

```powershell
git add BatteryPassWeb.Tests\QrWorkflowTests.cs BatteryPassWeb.Tests\DocumentAccessControlTests.cs BatteryPassWeb.Tests\TrustDirtyStateTests.cs
git commit -m "test: add qr document and external api acceptance checks"
```

---

## Task 7: Documentation And Manual Acceptance Script

**Files:**
- Modify: `docs/sample-cluster-test-accounts.md`
- Modify: `docs/end-user-testing-guide.md`
- Modify: `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`

- [ ] **Step 1: Add failing docs tests**

Append these tests to `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`:

```csharp
[Fact]
public void SampleAccounts_ShouldDocumentEveryPhase6AScenarioPassport()
{
    var docs = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));

    Assert.Contains("Phase 6A demo scenarios", docs);
    Assert.Contains("did:web:acme.battery.pass:demo-published-trusted-001", docs);
    Assert.Contains("did:web:acme.battery.pass:demo-draft-incomplete-001", docs);
    Assert.Contains("did:web:acme.battery.pass:demo-ready-to-sign-001", docs);
    Assert.Contains("did:web:acme.battery.pass:demo-signed-unpublished-001", docs);
    Assert.Contains("did:web:acme.battery.pass:demo-dirty-after-edit-001", docs);
    Assert.Contains("did:web:acme.battery.pass:demo-invalid-signature-001", docs);
    Assert.Contains("did:web:acme.battery.pass:demo-restricted-document-001", docs);
}

[Fact]
public void TestingGuide_ShouldDocumentPhase6AAcceptanceScript()
{
    var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));

    Assert.Contains("Phase 6A end-to-end demo hardening checklist", guide);
    Assert.Contains("Reset demo scenarios", guide);
    Assert.Contains("Expected state: Published, signed, clean, public, QR-ready", guide);
    Assert.Contains("Expected state: Missing required data, blocked from signing", guide);
    Assert.Contains("Expected state: Signed core changed after proof", guide);
    Assert.Contains("External HTTP telemetry update does not dirty the passport", guide);
    Assert.Contains("Restricted document download returns 403 for unauthorized users", guide);
}
```

- [ ] **Step 2: Run docs tests to verify they fail**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "SampleAccounts_ShouldDocumentEveryPhase6AScenarioPassport|TestingGuide_ShouldDocumentPhase6AAcceptanceScript" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: FAIL because docs do not yet mention Phase 6A.

- [ ] **Step 3: Update sample account matrix**

Append this section to `docs/sample-cluster-test-accounts.md`:

```markdown
## Phase 6A demo scenarios

Use these allow-listed passports after pressing **Reset demo scenarios** on `/admin/help`.

| Scenario | Passport ID | Cluster | Admin account | Expected state |
|---|---|---|---|---|
| Published trusted | `did:web:acme.battery.pass:demo-published-trusted-001` | North Operations Cluster | `admin@example.test` or `north.admin@example.test` | Published, signed, clean, public, QR-ready |
| Draft incomplete | `did:web:acme.battery.pass:demo-draft-incomplete-001` | North Operations Cluster | `admin@example.test` | Missing required data, blocked from signing |
| Ready to sign | `did:web:acme.battery.pass:demo-ready-to-sign-001` | North Operations Cluster | `admin@example.test` | Complete and validated, unsigned |
| Signed unpublished | `did:web:acme.battery.pass:demo-signed-unpublished-001` | North Operations Cluster | `admin@example.test` | Signed and clean, not public until published |
| Dirty after edit | `did:web:acme.battery.pass:demo-dirty-after-edit-001` | North Operations Cluster | `admin@example.test` | Signed core changed after proof |
| Invalid signature | `did:web:acme.battery.pass:demo-invalid-signature-001` | North Operations Cluster | `admin@example.test` | Proof diagnostics fail verification |
| Restricted document access | `did:web:acme.battery.pass:demo-restricted-document-001` | North Operations Cluster | `admin@example.test` or `north.admin@example.test` | Published trusted with restricted evidence metadata |
```

- [ ] **Step 4: Update end-user testing guide**

Append this section to `docs/end-user-testing-guide.md` near the existing Phase 5A section:

```markdown
### Phase 6A end-to-end demo hardening checklist

Use this checklist after Phase 6A changes to prove the demo can be restored and verified from a known state.

1. Login as `admin@example.test`.
2. Open `/admin/help`.
3. Press **Reset demo scenarios**.
4. Confirm the success message says the demo scenarios were reset.
5. Open `/admin/passports/did%3Aweb%3Aacme.battery.pass%3Ademo-published-trusted-001/conformance`.
6. Expected state: Published, signed, clean, public, QR-ready.
7. Logout or use a public browser session.
8. Search for `did:web:acme.battery.pass:demo-published-trusted-001` from `/`.
9. Expected result: the public summary opens.
10. Download or click the QR code from the summary page.
11. Expected result: the QR resolves back to the public summary URL.
12. Login again as `admin@example.test`.
13. Open `/admin/passports/did%3Aweb%3Aacme.battery.pass%3Ademo-draft-incomplete-001/conformance`.
14. Expected state: Missing required data, blocked from signing.
15. Open `/admin/passports/did%3Aweb%3Aacme.battery.pass%3Ademo-ready-to-sign-001/conformance`.
16. Expected result: the next available action is **Sign passport**.
17. Open `/admin/passports/did%3Aweb%3Aacme.battery.pass%3Ademo-signed-unpublished-001/conformance`.
18. Expected result: the next available action is **Publish passport** and public search does not expose it yet.
19. Open `/admin/passports/did%3Aweb%3Aacme.battery.pass%3Ademo-dirty-after-edit-001/conformance`.
20. Expected state: Signed core changed after proof; validate and sign again before relying on it.
21. Open `/admin/passports/did%3Aweb%3Aacme.battery.pass%3Ademo-invalid-signature-001/conformance`.
22. Expected result: invalid signature diagnostics are visible to the admin without exposing private key material.
23. Use the external API workbench or curl to send a telemetry update to the published trusted scenario.
24. Expected result: External HTTP telemetry update does not dirty the passport.
25. Try restricted document access on `did:web:acme.battery.pass:demo-restricted-document-001`.
26. Expected result: Restricted document download returns 403 for unauthorized users and remains accessible only to authorized admin/cluster users when a linked file exists.
```

- [ ] **Step 5: Run docs tests to verify they pass**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter Phase6ADocumentationTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add docs\sample-cluster-test-accounts.md docs\end-user-testing-guide.md BatteryPassWeb.Tests\Phase6ADocumentationTests.cs
git commit -m "docs: add phase 6a demo acceptance script"
```

---

## Task 8: Final Verification And Cleanup

**Files:**
- No source changes unless verification reveals a defect.

- [ ] **Step 1: Run Phase 6A targeted test group**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "DemoScenarioResetServiceTests|Phase6ADocumentationTests|TrustWorkflowAcceptanceTests|QrWorkflowTests|DocumentAccessControlTests|TrustDirtyStateTests" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS with known `Snappier 1.0.0` advisory warnings only.

- [ ] **Step 2: Run web build**

Run:

```powershell
$out = Join-Path $env:TEMP 'bp-verify-bin-web'
dotnet build web\BatteryPassWeb.csproj /p:UseAppHost=false /p:BaseOutputPath="$out\"
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 3: Clean verification output**

Run:

```powershell
$root = (Get-Location).Path
$paths = @('web\.verify-bin', 'BatteryPassWeb.Tests\.verify-bin')
foreach ($path in $paths) {
    $resolved = Resolve-Path -LiteralPath $path -ErrorAction SilentlyContinue
    if ($resolved -and $resolved.Path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolved.Path -Recurse -Force
    }
}
$tempOut = Join-Path $env:TEMP 'bp-verify-bin-web'
if (Test-Path -LiteralPath $tempOut) {
    Remove-Item -LiteralPath $tempOut -Recurse -Force
}
```

Expected: no output, verification directories removed.

- [ ] **Step 4: Check git status**

Run:

```powershell
git status --short
```

Expected: clean working tree. If dirty files remain, inspect with `git diff` and commit only Phase 6A-related intentional changes.

- [ ] **Step 5: Manual demo reset smoke test**

Run the app, then:

1. Login as `admin@example.test`.
2. Open `/admin/help`.
3. Press **Reset demo scenarios**.
4. Open `/admin/passports/did%3Aweb%3Aacme.battery.pass%3Ademo-published-trusted-001/conformance`.
5. Confirm it shows published/trusted or ready public state.
6. Search the public home page for `did:web:acme.battery.pass:demo-published-trusted-001`.
7. Confirm the public summary opens.

Expected: reset works against the configured demo MongoDB and the published trusted passport is publicly searchable.

Do not run the reset against a production database.
