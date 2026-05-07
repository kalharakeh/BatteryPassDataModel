# Guided Readiness Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Phase 5A guided readiness polish so admins can immediately understand what each passport needs next to become complete, trusted, and publishable.

**Architecture:** Add a pure `PassportReadinessService` that translates existing validation, publish, verification, dirty, and registry state into one decision object. Then wire that decision into conformance, admin edit guidance, admin help, and the testing guide without changing trust semantics or public summary visibility.

**Tech Stack:** ASP.NET Core MVC, C# / .NET 10, MongoDB `BsonDocument`, Razor views, xUnit source/layout tests.

---

## File Structure

- Create `web/Models/Trust/PassportReadinessModels.cs`: readiness state/action/severity constants plus the decision model rendered by admin pages.
- Create `web/Services/PassportReadinessService.cs`: pure service that computes the next readiness state and action from existing validation/publish/verification data.
- Modify `web/Program.cs`: register `PassportReadinessService`.
- Modify `web/Models/ViewModels/ConformanceViewModel.cs`: add readiness decision and grouped issue view models.
- Modify `web/Controllers/AdminController.cs`: inject/readiness service, pass readiness and grouped validation issues into the conformance model, and pass data requirements to edit view.
- Modify `web/Models/ViewModels/EditPassportViewModel.cs`: add field guidance metadata for admin edit rendering.
- Modify `web/Views/Admin/Conformance.cshtml`: simplify around a single readiness/next-action area, grouped blockers, and collapsible diagnostics.
- Modify `web/Views/Admin/EditPassport.cshtml`: render required/optional markers and short field guidance.
- Modify `web/Views/Admin/Help.cshtml`: replace bulky parameter duplicate content with first-time and dirty-recovery task checklists.
- Modify `web/wwwroot/css/site.css`: style readiness and guidance UI.
- Modify `docs/end-user-testing-guide.md`: add Phase 5A guided-readiness verification steps.
- Create `BatteryPassWeb.Tests/PassportReadinessServiceTests.cs`: unit coverage for readiness state decisions.
- Modify `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`: source/layout coverage for readiness page behavior.
- Modify `BatteryPassWeb.Tests/AdminHelpPageTests.cs`: source/layout coverage for shorter task-focused help.
- Create `BatteryPassWeb.Tests/AdminEditGuidanceTests.cs`: source/layout coverage for required/optional field guidance.
- Modify `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`: guide checklist coverage.

## Task 1: Passport Readiness Service

**Files:**
- Create: `web/Models/Trust/PassportReadinessModels.cs`
- Create: `web/Services/PassportReadinessService.cs`
- Modify: `web/Program.cs`
- Test: `BatteryPassWeb.Tests/PassportReadinessServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `BatteryPassWeb.Tests/PassportReadinessServiceTests.cs`:

```csharp
using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportReadinessServiceTests
{
    [Fact]
    public void Evaluate_ShouldMarkIncompletePassportAsBlockedWithCompleteDataNextAction()
    {
        var passport = Passport(status: "draft", trustState: TrustState.Invalid, isDirty: false, hasProof: false);
        var summary = Summary(TrustValidationSeverity.BlockingError);
        var decision = PublishDecision(canSign: false, canPublish: false);
        var verification = Verification(TrustState.Unvalidated, isValid: false);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.Incomplete, readiness.StateKey);
        Assert.Equal(PassportReadinessSeverity.Blocked, readiness.Severity);
        Assert.Equal(PassportReadinessAction.CompleteData, readiness.NextActionKey);
        Assert.True(readiness.CanValidate);
        Assert.True(readiness.CanCompleteDemoData);
        Assert.False(readiness.CanSign);
        Assert.False(readiness.CanPublish);
    }

    [Fact]
    public void Evaluate_ShouldMarkZeroBlockersWithoutProofAsReadyToSign()
    {
        var passport = Passport(status: "draft", trustState: TrustState.Valid, isDirty: false, hasProof: false);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: false);
        var verification = Verification(TrustState.Unvalidated, isValid: false);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.ReadyToSign, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.Sign, readiness.NextActionKey);
        Assert.True(readiness.CanSign);
        Assert.False(readiness.CanPublish);
        Assert.Equal(1, readiness.WarningCount);
    }

    [Fact]
    public void Evaluate_ShouldMarkSignedCleanUnpublishedAsReadyToPublish()
    {
        var passport = Passport(status: "draft", trustState: TrustState.Signed, isDirty: false, hasProof: true);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: true);
        var verification = Verification(TrustState.Signed, isValid: true);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.ReadyToPublish, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.Publish, readiness.NextActionKey);
        Assert.True(readiness.CanPublish);
        Assert.True(readiness.HasCurrentProof);
    }

    [Fact]
    public void Evaluate_ShouldMarkSignedCleanPublishedAsTrusted()
    {
        var passport = Passport(status: "published", trustState: TrustState.Signed, isDirty: false, hasProof: true);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: true);
        var verification = Verification(TrustState.Signed, isValid: true);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.PublishedTrusted, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.None, readiness.NextActionKey);
        Assert.Equal(PassportReadinessSeverity.Trusted, readiness.Severity);
        Assert.True(readiness.IsPublished);
    }

    [Fact]
    public void Evaluate_ShouldMarkDirtyPublishedPassportAsNeedingResign()
    {
        var passport = Passport(status: "published", trustState: TrustState.Dirty, isDirty: true, hasProof: true);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: false);
        var verification = Verification(TrustState.SignatureInvalid, isValid: false);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.DirtyNeedsResign, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.Sign, readiness.NextActionKey);
        Assert.True(readiness.TrustIsDirty);
        Assert.False(readiness.CanPublish);
    }

    [Fact]
    public void Evaluate_ShouldSendInvalidSignatureToDiagnostics()
    {
        var passport = Passport(status: "draft", trustState: TrustState.SignatureInvalid, isDirty: false, hasProof: true);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: false);
        var verification = Verification(TrustState.SignatureInvalid, isValid: false);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.InvalidSignature, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.ReviewDiagnostics, readiness.NextActionKey);
        Assert.Equal(PassportReadinessSeverity.Blocked, readiness.Severity);
    }

    private static BsonDocument Passport(string status, string trustState, bool isDirty, bool hasProof)
    {
        var proof = hasProof
            ? new BsonDocument { ["proofValue"] = "proof-value" }
            : new BsonDocument();

        return new BsonDocument
        {
            ["registryInfo"] = new BsonDocument { ["status"] = status },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = hasProof && !isDirty,
                ["hash"] = hasProof ? "hash-1" : string.Empty
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = trustState,
                ["isDirty"] = isDirty,
                ["latestHash"] = hasProof ? "hash-1" : string.Empty,
                ["latestProof"] = proof
            }
        };
    }

    private static TrustValidationSummary Summary(TrustValidationSeverity severity)
    {
        return new TrustValidationSummary
        {
            State = severity == TrustValidationSeverity.BlockingError ? TrustState.Invalid : TrustState.Valid,
            Sections =
            [
                new TrustValidationSectionResult
                {
                    SectionKey = "test",
                    SectionLabel = "Test",
                    Issues = [new TrustValidationIssue(severity, "test.path", "Test issue")]
                }
            ]
        };
    }

    private static PassportPublishDecision PublishDecision(bool canSign, bool canPublish)
    {
        return new PassportPublishDecision(
            CanSign: canSign,
            CanPublish: canPublish,
            PublishBlockReason: canPublish ? string.Empty : "Blocked for test.");
    }

    private static PassportVerificationResult Verification(string state, bool isValid)
    {
        return new PassportVerificationResult
        {
            State = state,
            IsValid = isValid,
            Message = isValid ? "Signature verified." : "Signature is not valid."
        };
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter PassportReadinessServiceTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: FAIL with compiler errors because `PassportReadinessService`, `PassportReadinessState`, `PassportReadinessAction`, and `PassportReadinessSeverity` do not exist.

- [ ] **Step 3: Add readiness models**

Create `web/Models/Trust/PassportReadinessModels.cs`:

```csharp
namespace BatteryPassWeb.Models.Trust;

public static class PassportReadinessState
{
    public const string Draft = "draft";
    public const string Incomplete = "incomplete";
    public const string ReadyToSign = "readyToSign";
    public const string SignedClean = "signedClean";
    public const string ReadyToPublish = "readyToPublish";
    public const string PublishedTrusted = "publishedTrusted";
    public const string DirtyNeedsResign = "dirtyNeedsResign";
    public const string InvalidSignature = "invalidSignature";
    public const string ServiceError = "serviceError";
}

public static class PassportReadinessSeverity
{
    public const string Neutral = "neutral";
    public const string Warning = "warning";
    public const string Blocked = "blocked";
    public const string Ready = "ready";
    public const string Trusted = "trusted";
}

public static class PassportReadinessAction
{
    public const string Validate = "validate";
    public const string CompleteData = "completeData";
    public const string EditRequiredData = "editRequiredData";
    public const string Sign = "sign";
    public const string Publish = "publish";
    public const string ReviewDiagnostics = "reviewDiagnostics";
    public const string None = "none";
}

public sealed class PassportReadinessDecision
{
    public string StateKey { get; init; } = PassportReadinessState.Draft;
    public string StateLabel { get; init; } = "Draft";
    public string Severity { get; init; } = PassportReadinessSeverity.Neutral;
    public string NextActionKey { get; init; } = PassportReadinessAction.Validate;
    public string NextActionLabel { get; init; } = "Validate passport";
    public string NextActionDescription { get; init; } = "Run validation to refresh readiness.";
    public bool CanValidate { get; init; } = true;
    public bool CanCompleteDemoData { get; init; }
    public bool CanSign { get; init; }
    public bool CanPublish { get; init; }
    public string PrimaryReason { get; init; } = "Save draft is allowed. Validation decides the next trust action.";
    public int BlockerCount { get; init; }
    public int WarningCount { get; init; }
    public bool TrustIsDirty { get; init; }
    public bool HasCurrentProof { get; init; }
    public bool IsPublished { get; init; }
}
```

- [ ] **Step 4: Add readiness service**

Create `web/Services/PassportReadinessService.cs`:

```csharp
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportReadinessService
{
    public PassportReadinessDecision Evaluate(
        BsonDocument passport,
        TrustValidationSummary summary,
        PassportPublishDecision publishDecision,
        PassportVerificationResult verificationResult)
    {
        var isPublished = BsonHelpers.GetString(passport, "registryInfo", "status")
            .Equals("published", StringComparison.OrdinalIgnoreCase);
        var trustState = BsonHelpers.GetString(passport, "trust", "state");
        var isDirty = GetBoolean(passport, "trust", "isDirty")
            || trustState.Equals(TrustState.Dirty, StringComparison.OrdinalIgnoreCase);
        var hasCurrentProof = HasCurrentProof(passport);
        var blockers = summary.BlockingErrorCount;
        var warnings = summary.WarningCount;

        if (verificationResult.State.Equals(TrustState.SignatureInvalid, StringComparison.OrdinalIgnoreCase)
            && !isDirty)
        {
            return Decision(
                PassportReadinessState.InvalidSignature,
                "Invalid signature",
                PassportReadinessSeverity.Blocked,
                PassportReadinessAction.ReviewDiagnostics,
                "Review diagnostics",
                "The proof could not verify against the current passport core.",
                "Signature verification failed. Review proof and hash diagnostics before relying on this passport.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canCompleteDemoData: false);
        }

        if (blockers > 0)
        {
            return Decision(
                PassportReadinessState.Incomplete,
                "Incomplete data",
                PassportReadinessSeverity.Blocked,
                PassportReadinessAction.CompleteData,
                "Complete required data",
                "Required Battery Pass data is missing or invalid.",
                "Fix the blocking validation issues before signing.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canCompleteDemoData: true);
        }

        if (isDirty)
        {
            return Decision(
                PassportReadinessState.DirtyNeedsResign,
                "Dirty: re-sign required",
                PassportReadinessSeverity.Warning,
                PassportReadinessAction.Sign,
                "Sign passport",
                "The passport changed after the latest signature.",
                "Validate is clean, but the current core must be signed again before publishing is trusted.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canCompleteDemoData: false);
        }

        if (publishDecision.CanPublish && isPublished && verificationResult.IsValid)
        {
            return Decision(
                PassportReadinessState.PublishedTrusted,
                "Published and trusted",
                PassportReadinessSeverity.Trusted,
                PassportReadinessAction.None,
                "No action needed",
                "This passport is public, clean, signed, and verifiable.",
                "The latest signed revision is published and current.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canCompleteDemoData: false);
        }

        if (publishDecision.CanPublish)
        {
            return Decision(
                PassportReadinessState.ReadyToPublish,
                "Ready to publish",
                PassportReadinessSeverity.Ready,
                PassportReadinessAction.Publish,
                "Publish passport",
                "A current valid signature proof exists.",
                "Publish this revision to make the passport publicly discoverable.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canCompleteDemoData: false);
        }

        if (publishDecision.CanSign)
        {
            return Decision(
                hasCurrentProof ? PassportReadinessState.SignedClean : PassportReadinessState.ReadyToSign,
                hasCurrentProof ? "Signed clean" : "Ready to sign",
                PassportReadinessSeverity.Ready,
                PassportReadinessAction.Sign,
                "Sign passport",
                hasCurrentProof ? "A proof exists but publish is still blocked." : "Validation has zero blocking errors.",
                hasCurrentProof ? publishDecision.PublishBlockReason : "Create a proof over the current canonical passport core.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canCompleteDemoData: false);
        }

        return Decision(
            PassportReadinessState.Draft,
            "Draft",
            PassportReadinessSeverity.Neutral,
            PassportReadinessAction.Validate,
            "Validate passport",
            "Run validation to refresh readiness.",
            "Draft saves are allowed, but trust actions require validation.",
            publishDecision,
            blockers,
            warnings,
            isDirty,
            hasCurrentProof,
            isPublished,
            canCompleteDemoData: false);
    }

    private static PassportReadinessDecision Decision(
        string stateKey,
        string stateLabel,
        string severity,
        string nextActionKey,
        string nextActionLabel,
        string nextActionDescription,
        string primaryReason,
        PassportPublishDecision publishDecision,
        int blockers,
        int warnings,
        bool isDirty,
        bool hasCurrentProof,
        bool isPublished,
        bool canCompleteDemoData)
    {
        return new PassportReadinessDecision
        {
            StateKey = stateKey,
            StateLabel = stateLabel,
            Severity = severity,
            NextActionKey = nextActionKey,
            NextActionLabel = nextActionLabel,
            NextActionDescription = nextActionDescription,
            PrimaryReason = string.IsNullOrWhiteSpace(primaryReason) ? publishDecision.PublishBlockReason : primaryReason,
            CanValidate = true,
            CanCompleteDemoData = canCompleteDemoData,
            CanSign = publishDecision.CanSign,
            CanPublish = publishDecision.CanPublish,
            BlockerCount = blockers,
            WarningCount = warnings,
            TrustIsDirty = isDirty,
            HasCurrentProof = hasCurrentProof,
            IsPublished = isPublished
        };
    }

    private static bool HasCurrentProof(BsonDocument passport)
    {
        var proof = BsonHelpers.GetValue(passport, "trust", "latestProof") as BsonDocument;
        return !string.IsNullOrWhiteSpace(BsonHelpers.GetString(passport, "trust", "latestHash"))
            && proof != null
            && !string.IsNullOrWhiteSpace(BsonHelpers.GetString(proof, "proofValue"));
    }

    private static bool GetBoolean(BsonDocument document, params string[] path)
    {
        var value = BsonHelpers.GetValue(document, path);
        return value is { IsBoolean: true } && value.AsBoolean;
    }
}
```

- [ ] **Step 5: Register the service**

Modify `web/Program.cs` by adding the registration next to publish/trust services:

```csharp
builder.Services.AddSingleton<PassportPublishPolicyService>();
builder.Services.AddSingleton<PassportReadinessService>();
builder.Services.AddSingleton<DataCompletionPolicyService>();
```

- [ ] **Step 6: Run tests to verify they pass**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter PassportReadinessServiceTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS.

- [ ] **Step 7: Commit**

Run:

```powershell
git add web\Models\Trust\PassportReadinessModels.cs web\Services\PassportReadinessService.cs web\Program.cs BatteryPassWeb.Tests\PassportReadinessServiceTests.cs
git commit -m "feat: add passport readiness service"
```

## Task 2: Conformance Readiness Dashboard

**Files:**
- Modify: `web/Models/ViewModels/ConformanceViewModel.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Views/Admin/Conformance.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Test: `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`

- [ ] **Step 1: Write failing layout/source tests**

Append these tests to `BatteryPassWeb.Tests/ConformanceLayoutTests.cs` before `RepoFile`:

```csharp
[Fact]
public void ConformanceModel_ShouldExposeGuidedReadinessData()
{
    var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ConformanceViewModel.cs"));

    Assert.Contains("PassportReadinessDecision", source);
    Assert.Contains("GroupedBlockingIssues", source);
    Assert.Contains("GroupedWarningIssues", source);
    Assert.Contains("ConformanceIssueGroupViewModel", source);
}

[Fact]
public void AdminController_ShouldComputePassportReadinessForConformance()
{
    var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
    var program = File.ReadAllText(RepoFile("web", "Program.cs"));

    Assert.Contains("PassportReadinessService", source);
    Assert.Contains("_passportReadinessService.Evaluate", source);
    Assert.Contains("GroupedBlockingIssues", source);
    Assert.Contains("AddSingleton<PassportReadinessService>", program);
}

[Fact]
public void ConformanceView_ShouldRenderOneGuidedNextActionAndCollapsibleDiagnostics()
{
    var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
    var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

    Assert.Contains("bp-readiness-hero", markup);
    Assert.Contains("Model.Readiness.StateLabel", markup);
    Assert.Contains("Model.Readiness.NextActionLabel", markup);
    Assert.Contains("bp-next-action-panel", markup);
    Assert.Contains("bp-blocker-groups", markup);
    Assert.Contains("bp-advanced-diagnostics", markup);
    Assert.Contains("<details", markup);
    Assert.DoesNotContain("Action blocked", markup);
    Assert.DoesNotContain("disabled=\"", markup);

    Assert.Contains(".bp-readiness-hero", css);
    Assert.Contains(".bp-next-action-panel", css);
    Assert.Contains(".bp-blocker-groups", css);
    Assert.Contains(".bp-advanced-diagnostics", css);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "ConformanceModel_ShouldExposeGuidedReadinessData|AdminController_ShouldComputePassportReadinessForConformance|ConformanceView_ShouldRenderOneGuidedNextActionAndCollapsibleDiagnostics" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: FAIL because conformance model/controller/view do not expose the readiness decision yet.

- [ ] **Step 3: Extend conformance view models**

Modify `web/Models/ViewModels/ConformanceViewModel.cs`:

```csharp
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class ConformanceViewModel
{
    public required PassportViewModel Passport { get; init; }
    public required TrustValidationSummary ValidationSummary { get; init; }
    public PassportReadinessDecision Readiness { get; init; } = new();
    public IReadOnlyList<ConformanceIssueGroupViewModel> GroupedBlockingIssues { get; init; } = [];
    public IReadOnlyList<ConformanceIssueGroupViewModel> GroupedWarningIssues { get; init; } = [];
    public bool CanSign { get; init; }
    public bool CanPublish { get; init; }
    public string PublishBlockReason { get; init; } = string.Empty;
    public PassportVerificationResult VerificationResult { get; init; } = new();
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class ConformanceIssueGroupViewModel
{
    public string SectionKey { get; init; } = string.Empty;
    public string SectionLabel { get; init; } = string.Empty;
    public IReadOnlyList<TrustValidationIssue> Issues { get; init; } = [];
    public string EditAnchor { get; init; } = string.Empty;
}

public sealed class PassportAuditTrailViewModel
{
    public required PassportViewModel Passport { get; init; }
    public IReadOnlyList<BsonDocument> AuditEvents { get; init; } = [];
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class PassportRevisionHistoryViewModel
{
    public required PassportViewModel Passport { get; init; }
    public IReadOnlyList<BsonDocument> Revisions { get; init; } = [];
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Inject and use readiness service**

Modify `web/Controllers/AdminController.cs`.

Add the field:

```csharp
private readonly PassportReadinessService _passportReadinessService;
```

Add constructor parameter after `PassportPublishPolicyService passportPublishPolicyService`:

```csharp
PassportReadinessService passportReadinessService,
```

Assign it in the constructor:

```csharp
_passportReadinessService = passportReadinessService;
```

In `Conformance`, after `verificationResult`:

```csharp
var readiness = _passportReadinessService.Evaluate(document, summary, publishDecision, verificationResult);
var groupedBlockingIssues = BuildIssueGroups(summary, TrustValidationSeverity.BlockingError);
var groupedWarningIssues = BuildIssueGroups(summary, TrustValidationSeverity.Warning);
```

Add these properties to the `ConformanceViewModel` initializer:

```csharp
Readiness = readiness,
GroupedBlockingIssues = groupedBlockingIssues,
GroupedWarningIssues = groupedWarningIssues,
```

Add this helper near the other private static helpers in `AdminController`:

```csharp
private static IReadOnlyList<ConformanceIssueGroupViewModel> BuildIssueGroups(
    TrustValidationSummary summary,
    TrustValidationSeverity severity)
{
    return summary.Sections
        .Select(section => new ConformanceIssueGroupViewModel
        {
            SectionKey = section.SectionKey,
            SectionLabel = section.SectionLabel,
            EditAnchor = AdminSectionAnchor(section.SectionKey),
            Issues = section.Issues
                .Where(issue => issue.Severity == severity)
                .ToList()
        })
        .Where(group => group.Issues.Count > 0)
        .ToList();
}

private static string AdminSectionAnchor(string sectionKey)
{
    return sectionKey switch
    {
        "generalProductInformation" or "identity" or "dataCompletionPolicy" => "admin-general",
        "materialComposition" => "admin-material-composition",
        "performanceAndDurability" => "admin-performance",
        "labeling" => "admin-compliance",
        "supplyChainDueDiligence" => "admin-supply-chain",
        "circularity" => "admin-circularity",
        "carbonFootprintForBatteries" => "admin-carbon-footprint",
        _ => "admin-general"
    };
}
```

- [ ] **Step 5: Replace conformance workflow markup**

Modify `web/Views/Admin/Conformance.cshtml`.

At the top, keep existing `@using` and model, then add:

```cshtml
@{
    ViewData["Title"] = "Passport conformance";
    var passport = Model.Passport;
    var readiness = Model.Readiness;
    var summary = Model.ValidationSummary;
}
```

Replace the current workflow/action section with this single guided action area:

```cshtml
<section class="bp-card bp-readiness-hero bp-readiness-@readiness.Severity">
    <div>
        <span class="bp-summary-title">Passport readiness</span>
        <h1>@readiness.StateLabel</h1>
        <p class="bp-passport-id">@passport.PassportId</p>
        <p class="bp-help-text mb-0">@readiness.PrimaryReason</p>
    </div>
    <aside class="bp-next-action-panel">
        <span>Next action</span>
        <strong>@readiness.NextActionLabel</strong>
        <p>@readiness.NextActionDescription</p>
        <div class="bp-action-row bp-action-row-primary">
            <form method="post" action="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/validate">
                @Html.AntiForgeryToken()
                <button type="submit" class="bp-secondary-button">Validate passport</button>
            </form>
            @if (readiness.NextActionKey == PassportReadinessAction.CompleteData && readiness.CanCompleteDemoData)
            {
                <form method="post" action="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/complete-required-data">
                    @Html.AntiForgeryToken()
                    <button type="submit" class="bp-secondary-button bp-button-accent">Complete required demo data</button>
                </form>
            }
            @if (readiness.NextActionKey == PassportReadinessAction.Sign && readiness.CanSign)
            {
                <form method="post" action="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/sign">
                    @Html.AntiForgeryToken()
                    <button type="submit" class="bp-primary-button">Sign passport</button>
                </form>
            }
            @if (readiness.NextActionKey == PassportReadinessAction.Publish && readiness.CanPublish)
            {
                <form method="post" action="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/publish">
                    @Html.AntiForgeryToken()
                    <button type="submit" class="bp-primary-button">Publish passport</button>
                </form>
            }
        </div>
    </aside>
</section>
```

Replace the current blocker checklist summary with grouped blockers:

```cshtml
<section class="bp-card bp-blocker-groups">
    <div class="bp-panel-heading">
        <span class="bp-summary-title">Blocking data to fix</span>
        <span class="bp-count-pill">@summary.BlockingErrorCount</span>
    </div>
    @if (Model.GroupedBlockingIssues.Count == 0)
    {
        <p class="bp-empty-state">No blocking errors. The passport can move to the next trust action.</p>
    }
    else
    {
        @foreach (var group in Model.GroupedBlockingIssues)
        {
            <article class="bp-blocker-group">
                <div class="bp-completion-section-header">
                    <div>
                        <span>Section</span>
                        <strong>@group.SectionLabel</strong>
                    </div>
                    <a class="bp-secondary-button bp-button-compact" href="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/edit#@group.EditAnchor">Edit section</a>
                </div>
                <ol class="bp-issue-list">
                    @foreach (var issue in group.Issues)
                    {
                        <li>
                            <code>@FriendlyDataPoint(issue.Path)</code>
                            <span>@CompletionGuidance(issue)</span>
                            <small>@issue.Message</small>
                        </li>
                    }
                </ol>
            </article>
        }
    }
</section>
```

Wrap warning/proof/raw validation sections in a collapsible diagnostics area:

```cshtml
<section class="bp-card bp-advanced-diagnostics">
    <details>
        <summary>
            <strong>Advanced diagnostics</strong>
            <span>@summary.WarningCount warnings</span>
        </summary>
        <div class="bp-evidence-grid">
            <div>
                <h2>Warnings</h2>
                @if (Model.GroupedWarningIssues.Count == 0)
                {
                    <p class="bp-empty-state">No warnings were reported.</p>
                }
                else
                {
                    @foreach (var group in Model.GroupedWarningIssues)
                    {
                        <h3 class="h6">@group.SectionLabel</h3>
                        <ol class="bp-issue-list is-warning">
                            @foreach (var issue in group.Issues)
                            {
                                <li><code>@issue.Path</code><span>@issue.Message</span></li>
                            }
                        </ol>
                    }
                }
            </div>
            <div>
                <h2>Proof and hash diagnostics</h2>
                <dl class="bp-diagnostics-list">
                    <div><dt>Issuer</dt><dd>@(string.IsNullOrWhiteSpace(Model.VerificationResult.Issuer) ? "-" : Model.VerificationResult.Issuer)</dd></div>
                    <div><dt>Signed date</dt><dd>@(string.IsNullOrWhiteSpace(Model.VerificationResult.SignedAt) ? "-" : Model.VerificationResult.SignedAt)</dd></div>
                    <div><dt>Dirty state</dt><dd>@(passport.TrustIsDirty ? "Dirty" : "Clean")</dd></div>
                    <div><dt>Message</dt><dd>@Model.VerificationResult.Message</dd></div>
                    <div><dt>Expected hash</dt><dd><code>@(string.IsNullOrWhiteSpace(Model.VerificationResult.ExpectedHash) ? "-" : Model.VerificationResult.ExpectedHash)</code></dd></div>
                    <div><dt>Current hash</dt><dd><code>@(string.IsNullOrWhiteSpace(Model.VerificationResult.CurrentHash) ? "-" : Model.VerificationResult.CurrentHash)</code></dd></div>
                </dl>
            </div>
        </div>
    </details>
</section>
```

- [ ] **Step 6: Add readiness CSS**

Append to `web/wwwroot/css/site.css` near conformance styles:

```css
.bp-readiness-hero {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(280px, 420px);
  gap: 1.5rem;
  align-items: stretch;
  border: 1px solid rgba(15, 42, 73, 0.14);
  background:
    radial-gradient(circle at top left, rgba(38, 127, 184, 0.18), transparent 34rem),
    linear-gradient(135deg, #ffffff, #f7fbff);
}

.bp-readiness-hero h1 {
  margin: 0.35rem 0;
  font-size: clamp(2rem, 4vw, 4rem);
  letter-spacing: -0.05em;
}

.bp-readiness-ready {
  border-color: rgba(18, 126, 93, 0.24);
}

.bp-readiness-trusted {
  border-color: rgba(10, 139, 91, 0.34);
  background:
    radial-gradient(circle at top left, rgba(8, 163, 72, 0.2), transparent 34rem),
    linear-gradient(135deg, #ffffff, #f3fff8);
}

.bp-readiness-blocked {
  border-color: rgba(191, 77, 35, 0.28);
  background:
    radial-gradient(circle at top left, rgba(223, 107, 59, 0.2), transparent 34rem),
    linear-gradient(135deg, #ffffff, #fff8f3);
}

.bp-next-action-panel {
  display: grid;
  gap: 0.8rem;
  padding: 1.25rem;
  border-radius: 24px;
  background: rgba(255, 255, 255, 0.82);
  box-shadow: inset 0 0 0 1px rgba(15, 42, 73, 0.08);
}

.bp-next-action-panel > span {
  font-size: 0.78rem;
  font-weight: 800;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: #607189;
}

.bp-next-action-panel > strong {
  font-size: 1.45rem;
  color: #071d33;
}

.bp-blocker-groups {
  display: grid;
  gap: 1rem;
}

.bp-blocker-group {
  padding: 1rem;
  border-radius: 18px;
  background: #f8fbff;
  border: 1px solid rgba(15, 42, 73, 0.08);
}

.bp-advanced-diagnostics details {
  border-radius: 18px;
  border: 1px solid rgba(15, 42, 73, 0.1);
  background: #ffffff;
}

.bp-advanced-diagnostics summary {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  cursor: pointer;
  padding: 1rem;
}

.bp-advanced-diagnostics details > div {
  padding: 0 1rem 1rem;
}

@media (max-width: 900px) {
  .bp-readiness-hero {
    grid-template-columns: 1fr;
  }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "ConformanceModel_ShouldExposeGuidedReadinessData|AdminController_ShouldComputePassportReadinessForConformance|ConformanceView_ShouldRenderOneGuidedNextActionAndCollapsibleDiagnostics" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS.

- [ ] **Step 8: Commit**

Run:

```powershell
git add web\Models\ViewModels\ConformanceViewModel.cs web\Controllers\AdminController.cs web\Views\Admin\Conformance.cshtml web\wwwroot\css\site.css BatteryPassWeb.Tests\ConformanceLayoutTests.cs
git commit -m "feat: guide conformance by readiness state"
```

## Task 3: Admin Edit Field Guidance

**Files:**
- Modify: `web/Models/ViewModels/EditPassportViewModel.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Views/Admin/EditPassport.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Test: `BatteryPassWeb.Tests/AdminEditGuidanceTests.cs`

- [ ] **Step 1: Write failing tests**

Create `BatteryPassWeb.Tests/AdminEditGuidanceTests.cs`:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class AdminEditGuidanceTests
{
    [Fact]
    public void EditPassportModel_ShouldExposeDataRequirementGuidance()
    {
        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "EditPassportViewModel.cs"));

        Assert.Contains("DataRequirements", source);
        Assert.Contains("FieldRequirementByKey", source);
    }

    [Fact]
    public void AdminController_ShouldLoadDataRequirementsForEditForms()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("DataRequirements = dataRequirements", source);
        Assert.Contains("BuildFieldRequirementDictionary", source);
    }

    [Fact]
    public void EditPassportView_ShouldRenderRequiredOptionalMarkersAndGuidance()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-field-shell", markup);
        Assert.Contains("bp-field-marker", markup);
        Assert.Contains("Required", markup);
        Assert.Contains("Optional", markup);
        Assert.Contains("Changing this after signing makes the passport dirty", markup);
        Assert.Contains("External HTTP updates to this live operational data do not dirty", markup);
        Assert.Contains("FieldRequirementByKey", markup);
        Assert.Contains("general.passportId", markup);
        Assert.Contains("general.serialNumber", markup);
        Assert.Contains("general.batteryImageUrl", markup);
        Assert.Contains("material.nickelMass", markup);
        Assert.Contains("material.lithiumMass", markup);
        Assert.Contains("performance.stateOfCharge", markup);
        Assert.Contains("compliance.euDeclarationOfConformity", markup);
        Assert.Contains("supplyChain.dueDiligenceReport", markup);
        Assert.Contains("circularity.recycledLeadPrimary", markup);
        Assert.Contains("carbon.co2StudyReference", markup);
        Assert.DoesNotContain("<label>Name<input", markup);
        Assert.DoesNotContain("<label>Rated energy kWh<input", markup);
        Assert.DoesNotContain("<label>Amount gCO2e/kWh<input", markup);

        Assert.Contains(".bp-field-shell", css);
        Assert.Contains(".bp-field-marker", css);
        Assert.Contains(".bp-field-help", css);
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
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter AdminEditGuidanceTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: FAIL because edit view models and markup do not expose field guidance yet.

- [ ] **Step 3: Extend edit view model**

Modify `web/Models/ViewModels/EditPassportViewModel.cs`:

```csharp
using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class EditPassportViewModel
{
    public required PassportViewModel Passport { get; init; }
    public string Mode { get; init; } = "edit";
    public DataCompletionPolicySnapshot DataRequirements { get; init; } = new();
    public IReadOnlyDictionary<string, DataRequirementField> FieldRequirementByKey { get; init; } =
        new Dictionary<string, DataRequirementField>(StringComparer.OrdinalIgnoreCase);
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Load requirements in edit/create actions**

Modify `NewPassport` and `EditPassport` in `web/Controllers/AdminController.cs`.

Before constructing the model in each action:

```csharp
var dataRequirements = await _dataCompletionPolicyService.GetPolicyAsync(cancellationToken);
```

Add these properties to each `EditPassportViewModel` initializer:

```csharp
DataRequirements = dataRequirements,
FieldRequirementByKey = BuildFieldRequirementDictionary(dataRequirements),
```

Add helper near other private static helpers:

```csharp
private static IReadOnlyDictionary<string, DataRequirementField> BuildFieldRequirementDictionary(DataCompletionPolicySnapshot dataRequirements)
{
    return dataRequirements.Sections
        .SelectMany(section => section.Fields)
        .ToDictionary(field => field.FieldKey, field => field, StringComparer.OrdinalIgnoreCase);
}
```

- [ ] **Step 5: Add guidance helpers to edit view**

Modify `web/Views/Admin/EditPassport.cshtml`.

Add these helpers near existing local functions:

```cshtml
    DataRequirementField? Requirement(string fieldKey)
        => Model.FieldRequirementByKey.TryGetValue(fieldKey, out var requirement) ? requirement : null;

    string RequirementLabel(string fieldKey)
        => Requirement(fieldKey)?.IsRequired == true ? "Required" : "Optional";

    string RequirementHelp(string fieldKey, string fallback)
        => string.IsNullOrWhiteSpace(Requirement(fieldKey)?.Guidance) ? fallback : Requirement(fieldKey)!.Guidance;

    string DirtySignedDataHelp(string fieldKey, string fallback)
        => $"{RequirementHelp(fieldKey, fallback)} Changing this after signing makes the passport dirty until it is validated and signed again.";

    string LiveOperationalHelp(string fieldKey, string fallback)
        => $"{RequirementHelp(fieldKey, fallback)} External HTTP updates to this live operational data do not dirty the passport signature.";
```

Replace every visible editable form label in the seven admin form sections with the `bp-field-shell` pattern. The field key on each label must match this map so required/optional toggles saved in MongoDB drive the edit form:

| Form input name | Requirement field key | Help text helper |
| --- | --- | --- |
| `passportId` | `general.passportId` | `DirtySignedDataHelp` |
| `name` | `general.name` | `DirtySignedDataHelp` |
| `modelNumber` | `general.modelNumber` | `DirtySignedDataHelp` |
| `serialNumber` | `general.serialNumber` | `DirtySignedDataHelp` |
| `category` | `general.category` | `DirtySignedDataHelp` |
| `batteryStatus` | `general.batteryStatus` | `DirtySignedDataHelp` |
| `batteryMass` | `general.batteryMass` | `DirtySignedDataHelp` |
| `manufacturingDate` | `general.manufacturingDate` | `DirtySignedDataHelp` |
| `facilityId` | `general.facilityId` | `DirtySignedDataHelp` |
| `manufacturerName` | `general.manufacturerName` | `DirtySignedDataHelp` |
| `status` | `general.registryStatus` | `DirtySignedDataHelp` |
| `batteryImageUrl` | `general.batteryImageUrl` | `RequirementHelp` |
| `materialNickel` | `material.nickelMass` | `DirtySignedDataHelp` |
| `materialCopper` | `material.copperMass` | `DirtySignedDataHelp` |
| `materialAluminium` | `material.aluminiumMass` | `DirtySignedDataHelp` |
| `materialGraphite` | `material.graphiteMass` | `DirtySignedDataHelp` |
| `materialManganese` | `material.manganeseMass` | `DirtySignedDataHelp` |
| `materialCobalt` | `material.cobaltMass` | `DirtySignedDataHelp` |
| `materialLithium` | `material.lithiumMass` | `DirtySignedDataHelp` |
| `materialElectrolyte` | `material.electrolyteMass` | `DirtySignedDataHelp` |
| `ratedEnergy` | `performance.ratedEnergy` | `DirtySignedDataHelp` |
| `ratedCapacity` | `performance.ratedCapacity` | `DirtySignedDataHelp` |
| `ratedMaximumPower` | `performance.ratedMaximumPower` | `DirtySignedDataHelp` |
| `nominalVoltage` | `performance.nominalVoltage` | `DirtySignedDataHelp` |
| `expectedLifetime` | `performance.expectedLifetime` | `DirtySignedDataHelp` |
| `expectedNumberOfCycles` | `performance.expectedNumberOfCycles` | `DirtySignedDataHelp` |
| `stateOfCharge` | `performance.stateOfCharge` | `LiveOperationalHelp` |
| `remainingCapacity` | `performance.remainingCapacity` | `LiveOperationalHelp` |
| `remainingEnergy` | `performance.remainingEnergy` | `LiveOperationalHelp` |
| `fullCycles` | `performance.fullCycles` | `LiveOperationalHelp` |
| `document_conformityAssessment` | `compliance.conformityAssessment` | `DirtySignedDataHelp` |
| `document_conformityAssessment_visibility` | `compliance.conformityAssessment` | `DirtySignedDataHelp` |
| `document_euDeclarationOfConformity` | `compliance.euDeclarationOfConformity` | `DirtySignedDataHelp` |
| `document_euDeclarationOfConformity_visibility` | `compliance.euDeclarationOfConformity` | `DirtySignedDataHelp` |
| `supplyChainIndex` | `supplyChain.supplyChainIndex` | `DirtySignedDataHelp` |
| `document_sustainabilityReport` | `supplyChain.sustainabilityReport` | `DirtySignedDataHelp` |
| `document_sustainabilityReport_visibility` | `supplyChain.sustainabilityReport` | `DirtySignedDataHelp` |
| `document_dueDiligenceReport` | `supplyChain.dueDiligenceReport` | `DirtySignedDataHelp` |
| `document_dueDiligenceReport_visibility` | `supplyChain.dueDiligenceReport` | `DirtySignedDataHelp` |
| `document_thirdPartyAudit` | `supplyChain.thirdPartyAudit` | `DirtySignedDataHelp` |
| `document_thirdPartyAudit_visibility` | `supplyChain.thirdPartyAudit` | `DirtySignedDataHelp` |
| `document_taxonomyReport` | `supplyChain.taxonomyReport` | `DirtySignedDataHelp` |
| `document_taxonomyReport_visibility` | `supplyChain.taxonomyReport` | `DirtySignedDataHelp` |
| `separateCollection` | `circularity.separateCollection` | `DirtySignedDataHelp` |
| `wastePrevention` | `circularity.wastePrevention` | `DirtySignedDataHelp` |
| `recycledContentShareVerification` | `circularity.recycledContentShareVerification` | `RequirementHelp` |
| `recycledNickelPre` | `circularity.recycledNickelPre` | `DirtySignedDataHelp` |
| `recycledNickelPost` | `circularity.recycledNickelPost` | `DirtySignedDataHelp` |
| `recycledNickelPrimary` | `circularity.recycledNickelPrimary` | `RequirementHelp` |
| `recycledCobaltPre` | `circularity.recycledCobaltPre` | `DirtySignedDataHelp` |
| `recycledCobaltPost` | `circularity.recycledCobaltPost` | `DirtySignedDataHelp` |
| `recycledCobaltPrimary` | `circularity.recycledCobaltPrimary` | `RequirementHelp` |
| `recycledLithiumPre` | `circularity.recycledLithiumPre` | `DirtySignedDataHelp` |
| `recycledLithiumPost` | `circularity.recycledLithiumPost` | `DirtySignedDataHelp` |
| `recycledLithiumPrimary` | `circularity.recycledLithiumPrimary` | `RequirementHelp` |
| `recycledLeadPre` | `circularity.recycledLeadPre` | `DirtySignedDataHelp` |
| `recycledLeadPost` | `circularity.recycledLeadPost` | `DirtySignedDataHelp` |
| `recycledLeadPrimary` | `circularity.recycledLeadPrimary` | `RequirementHelp` |
| `carbonFootprint` | `carbon.amount` | `DirtySignedDataHelp` |
| `performanceClass` | `carbon.performanceClass` | `DirtySignedDataHelp` |
| `carbonRawMaterial` | `carbon.rawMaterial` | `DirtySignedDataHelp` |
| `carbonMainProduction` | `carbon.mainProduction` | `DirtySignedDataHelp` |
| `carbonDistribution` | `carbon.distribution` | `DirtySignedDataHelp` |
| `carbonRecycling` | `carbon.recycling` | `DirtySignedDataHelp` |
| `document_co2StudyReference` | `carbon.co2StudyReference` | `DirtySignedDataHelp` |
| `document_co2StudyReference_visibility` | `carbon.co2StudyReference` | `DirtySignedDataHelp` |

Use this exact pattern for single-line text/number/date inputs:

```cshtml
<label class="bp-field-shell">
    <span>Serial Number <em class="bp-field-marker">@RequirementLabel("general.serialNumber")</em></span>
    <input type="text" name="serialNumber" value="@passport.SerialNumber" />
    <small class="bp-field-help">@DirtySignedDataHelp("general.serialNumber", "Manufacturer serial number.")</small>
</label>
```

Use this exact pattern for select controls:

```cshtml
<label class="bp-field-shell">
    <span>Registry status <em class="bp-field-marker">@RequirementLabel("general.registryStatus")</em></span>
    <select name="status">
        <option value="draft" selected="@(passport.Status == "draft")">Draft</option>
        <option value="published" selected="@(passport.Status == "published")">Published</option>
        <option value="archived" selected="@(passport.Status == "archived")">Archived</option>
    </select>
    <small class="bp-field-help">@DirtySignedDataHelp("general.registryStatus", "Draft, published, or archived status.")</small>
</label>
```

Use this exact pattern for live HTTP operational fields:

```cshtml
<label class="bp-field-shell">
    <span>State of charge % <em class="bp-field-marker">@RequirementLabel("performance.stateOfCharge")</em></span>
    <input type="number" step="any" name="stateOfCharge" value="@passport.Performance.StateOfCharge" />
    <small class="bp-field-help">@LiveOperationalHelp("performance.stateOfCharge", "Current state of charge.")</small>
</label>
```

Use this exact pattern for textarea controls:

```cshtml
<label class="bp-field-shell bp-span-2">
    <span>Separate collection <em class="bp-field-marker">@RequirementLabel("circularity.separateCollection")</em></span>
    <textarea name="separateCollection">@passport.Circularity.SeparateCollection</textarea>
    <small class="bp-field-help">@DirtySignedDataHelp("circularity.separateCollection", "Collection instructions or URL.")</small>
</label>
```

- [ ] **Step 6: Add edit guidance CSS**

Append to `web/wwwroot/css/site.css`:

```css
.bp-field-shell {
  display: grid;
  gap: 0.35rem;
}

.bp-field-shell > span {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  font-weight: 750;
}

.bp-field-marker {
  border-radius: 999px;
  padding: 0.16rem 0.55rem;
  background: #edf4fb;
  color: #35526e;
  font-size: 0.68rem;
  font-style: normal;
  font-weight: 850;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.bp-field-help {
  color: #64758a;
  line-height: 1.45;
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter AdminEditGuidanceTests /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS.

- [ ] **Step 8: Commit**

Run:

```powershell
git add web\Models\ViewModels\EditPassportViewModel.cs web\Controllers\AdminController.cs web\Views\Admin\EditPassport.cshtml web\wwwroot\css\site.css BatteryPassWeb.Tests\AdminEditGuidanceTests.cs
git commit -m "feat: add admin edit field guidance"
```

## Task 4: Admin Help And Testing Guide Polish

**Files:**
- Modify: `web/Views/Admin/Help.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Modify: `docs/end-user-testing-guide.md`
- Modify: `BatteryPassWeb.Tests/AdminHelpPageTests.cs`
- Modify: `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`

- [ ] **Step 1: Write failing tests**

Append these tests to `BatteryPassWeb.Tests/AdminHelpPageTests.cs`:

```csharp
[Fact]
public void AdminHelpView_ShouldPrioritizeFirstTimeAndDirtyRecoveryChecklists()
{
    var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
    var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

    Assert.Contains("First-time passport checklist", markup);
    Assert.Contains("Dirty recovery checklist", markup);
    Assert.Contains("What public users can see", markup);
    Assert.Contains("What admins can see", markup);
    Assert.Contains("Open data requirements", markup);
    Assert.DoesNotContain("Parameter-by-parameter fill list", markup);

    Assert.Contains(".bp-admin-help-check-card", css);
    Assert.Contains(".bp-admin-help-split-checklists", css);
}
```

Append this test to `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`:

```csharp
[Fact]
public void TestingGuide_ShouldIncludeGuidedReadinessPhase5AChecklist()
{
    var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));

    Assert.Contains("Phase 5A guided readiness checklist", guide);
    Assert.Contains("Expected next action: Complete required data", guide);
    Assert.Contains("Expected next action: Sign passport", guide);
    Assert.Contains("Expected next action: Publish passport", guide);
    Assert.Contains("Expected state: Published and trusted", guide);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "AdminHelpView_ShouldPrioritizeFirstTimeAndDirtyRecoveryChecklists|TestingGuide_ShouldIncludeGuidedReadinessPhase5AChecklist" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: FAIL because the help page and testing guide do not yet include the new Phase 5A wording.

- [ ] **Step 3: Simplify help page content**

Modify `web/Views/Admin/Help.cshtml`.

Replace the long parameter guide block that starts with `Parameter-by-parameter fill list` with:

```cshtml
<div class="bp-admin-help-split-checklists">
    <article class="bp-admin-help-check-card">
        <span class="bp-summary-title">First-time passport checklist</span>
        <h4>Create a trusted passport from scratch</h4>
        <ol>
            <li>Create the passport ID and save the draft.</li>
            <li>Fill the visible admin sections.</li>
            <li>Open <a href="/admin/clusters?tab=data-requirements">data requirements</a> to confirm what is required.</li>
            <li>Open conformance and resolve every blocking field.</li>
            <li>Validate until blockers are zero.</li>
            <li>Sign the passport.</li>
            <li>Publish once the page says publishing is ready.</li>
        </ol>
    </article>
    <article class="bp-admin-help-check-card">
        <span class="bp-summary-title">Dirty recovery checklist</span>
        <h4>Return edited data to clean trust</h4>
        <ol>
            <li>Save the admin edit. Draft save is allowed.</li>
            <li>Open conformance and confirm the passport is dirty or not publishable.</li>
            <li>Validate the updated data.</li>
            <li>Fix every blocking issue.</li>
            <li>Sign again so the latest core has a new proof.</li>
            <li>Publish again when the latest proof is current.</li>
        </ol>
    </article>
</div>
<div class="bp-admin-help-split-checklists">
    <article class="bp-admin-help-check-card">
        <span class="bp-summary-title">What public users can see</span>
        <p>Public search and QR lookup show published passports. The summary page stays focused on battery facts and does not expose internal validation/conformance diagnostics.</p>
    </article>
    <article class="bp-admin-help-check-card">
        <span class="bp-summary-title">What admins can see</span>
        <p>General admins and local cluster admins for the battery's cluster can open privileged Trust &amp; conformance evidence, audit history, revision history, proof hashes, and blocker diagnostics.</p>
    </article>
</div>
<p class="mb-0">
    Use the <a href="/admin/clusters?tab=data-requirements">Open data requirements</a> tab as the authoritative list of required and optional fields. The conformance page shows which of those fields are currently blocking the passport.
</p>
```

- [ ] **Step 4: Add help checklist CSS**

Append to `web/wwwroot/css/site.css`:

```css
.bp-admin-help-split-checklists {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
  margin: 1rem 0;
}

.bp-admin-help-check-card {
  display: grid;
  gap: 0.65rem;
  padding: 1rem;
  border-radius: 20px;
  background: #f8fbff;
  border: 1px solid rgba(15, 42, 73, 0.09);
}

.bp-admin-help-check-card h4 {
  margin: 0;
  color: #071d33;
}

.bp-admin-help-check-card ol {
  margin: 0;
  padding-left: 1.2rem;
}

.bp-admin-help-check-card li + li {
  margin-top: 0.35rem;
}

@media (max-width: 800px) {
  .bp-admin-help-split-checklists {
    grid-template-columns: 1fr;
  }
}
```

- [ ] **Step 5: Update the testing guide**

Append this section to `docs/end-user-testing-guide.md`:

```markdown
### 11.4 Phase 5A guided readiness checklist

Use this checklist after the guided readiness polish to confirm the conformance page tells admins exactly what to do next.

1. Open an incomplete draft passport in `/admin/passports/{passportId}/conformance`.
   - Expected next action: Complete required data.
   - Expected state: Incomplete data.
2. Complete required data or manually fill the blocking fields, then validate.
   - Expected next action: Sign passport.
   - Expected state: Ready to sign.
3. Sign the passport.
   - Expected next action: Publish passport.
   - Expected state: Ready to publish.
4. Publish the passport.
   - Expected state: Published and trusted.
   - Expected public behavior: the summary is searchable and still does not show internal conformance diagnostics.
5. Edit a signed core field such as battery mass and save.
   - Expected next action: Sign passport after validation succeeds.
   - Expected state: Dirty: re-sign required.
6. Confirm warnings and proof/hash diagnostics are available under advanced diagnostics, not as the first thing an admin must parse.
```

- [ ] **Step 6: Run tests to verify they pass**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "AdminHelpView_ShouldPrioritizeFirstTimeAndDirtyRecoveryChecklists|TestingGuide_ShouldIncludeGuidedReadinessPhase5AChecklist" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS.

- [ ] **Step 7: Commit**

Run:

```powershell
git add web\Views\Admin\Help.cshtml web\wwwroot\css\site.css docs\end-user-testing-guide.md BatteryPassWeb.Tests\AdminHelpPageTests.cs BatteryPassWeb.Tests\ErrorHandlingRolloutTests.cs
git commit -m "docs: polish admin readiness guidance"
```

## Task 5: Full Verification

**Files:** all changed files.

- [ ] **Step 1: Run readiness and layout test slice**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "PassportReadinessServiceTests|ConformanceLayoutTests|AdminEditGuidanceTests|AdminHelpPageTests|ErrorHandlingRolloutTests" /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS with zero failed tests.

- [ ] **Step 2: Run full tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj /p:UseAppHost=false /p:BaseOutputPath=.verify-bin\
```

Expected: PASS with zero failed tests. Existing `Snappier` vulnerability warnings may appear.

- [ ] **Step 3: Run web build**

Run:

```powershell
$out = Join-Path $env:TEMP 'bp-verify-bin-web'
dotnet build web\BatteryPassWeb.csproj /p:UseAppHost=false /p:BaseOutputPath="$out\"
```

Expected: build succeeds with `0 Error(s)`. Existing `Snappier` vulnerability warnings may appear.

- [ ] **Step 4: Remove verification build outputs**

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

Expected: temporary verification directories are removed.

- [ ] **Step 5: Inspect changed files**

Run:

```powershell
git status --short
git diff --stat
```

Expected: changes are limited to Phase 5A readiness/guidance files plus any pre-existing dirty files from earlier phases. Do not revert unrelated existing work.

- [ ] **Step 6: Commit final verification/doc cleanup if needed**

If Task 5 required any fixes, commit them:

```powershell
git add <files-fixed-during-task-5>
git commit -m "chore: verify guided readiness polish"
```

Expected: no commit is needed if Task 5 only ran verification commands.

## Self-Review

Spec coverage:

- Central readiness decision service is covered by Task 1.
- Cleaner conformance dashboard is covered by Task 2.
- Admin edit form guidance is covered by Task 3.
- Simplified admin help page is covered by Task 4.
- End-user testing guide update is covered by Task 4.
- Regression/layout tests and full verification are covered by Tasks 1 through 5.

Placeholder scan:

- The disallowed-marker search is clean. Each task has concrete files, snippets, commands, expected results, and commit boundaries.

Type consistency:

- `PassportReadinessDecision`, `PassportReadinessState`, `PassportReadinessSeverity`, and `PassportReadinessAction` are defined in Task 1 before use in Tasks 2 through 4.
- `ConformanceIssueGroupViewModel` is defined before controller/view usage.
- `DataRequirementField` and `DataCompletionPolicySnapshot` already exist under `BatteryPassWeb.Models.Trust` and are referenced consistently.
