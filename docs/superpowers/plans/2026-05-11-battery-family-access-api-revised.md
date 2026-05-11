# Battery Family Access API Revised Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved Battery Family terminology, Battery version workflow, sign-token API, publish lifecycle, role visibility, and admin safety guardrails.

**Architecture:** Keep the existing ASP.NET Core MVC and MongoDB document model, while using user-facing Battery Family/Battery version names over the existing internal Product Template concept. Centralize trust validation/sign/publish behavior in a workflow service so the admin UI and external API share the same first-publish and auto-republish rules. Remove battery secrets entirely and make cluster-scoped API tokens the only external API credential mechanism.

**Tech Stack:** ASP.NET Core MVC on .NET 10, Razor views, MongoDB BSON documents, cookie authentication, xUnit tests, source-level regression tests for views/docs, service-level tests for policy and model behavior.

---

## Source Spec

Implement the approved design in:

- `docs/superpowers/specs/2026-05-11-battery-family-access-api-revised-design.md`

This plan supersedes:

- `docs/superpowers/plans/2026-05-11-passport-admin-ux-guardrails.md`
- `docs/superpowers/specs/2026-05-11-passport-admin-ux-guardrails-design.md`

## Existing Worktree Note

The worktree already contains unrelated modified files. Before each task, run `git status --short` and do not revert edits outside the files listed in that task. Commit only the task files.

## File Structure

- Create `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`: cross-cutting source and behavior tests for the revised requirements.
- Create `web/Services/PassportTrustWorkflowService.cs`: shared validate, sign, publish, and auto-republish workflow for admin UI and external API.
- Create `web/Models/ViewModels/AccountProfileViewModel.cs`: self-service account form model.
- Create `web/Controllers/AccountController.cs`: authenticated profile/email/password updates.
- Modify `web/Models/ViewModels/PassportViewModel.cs`: add Battery Family, Battery version, Battery serial number, and Passport status display properties; remove Software from tab list.
- Modify `web/Models/ViewModels/PassportSummaryViewModel.cs`: add Battery Family, Battery version, Battery serial number, and Passport status summary properties.
- Modify `web/Models/ViewModels/AdminClusterViewModel.cs`: remove battery-secret view model usage, add token sign mode display, cluster linked counts, self-row flag, and role labels.
- Modify `web/Models/ViewModels/EditPassportViewModel.cs`: remove selected software-version list state; keep selected Battery Family and Battery version state.
- Modify `web/Services/ProductTemplateModels.cs`: keep internal Product Template names, but make software version/release/latest update scalar parameters on `BatteryProductVersion`.
- Modify `web/Services/ProductTemplateService.cs`: remove software-version collection/push logic, keep Battery version push, add safe single-passport Battery version change.
- Modify `web/Services/DataCompletionPolicyService.cs`: rename user-facing labels and stop treating Battery Name/Model Number/software variant fields as required identity fields.
- Modify `web/Services/PassportViewModelFactory.cs`: populate new display properties and compute Passport status.
- Modify `web/Services/PassportRepository.cs`: compute summary Passport status, unarchive, linked cluster counts, and durable `hasBeenPublished` flag on publish.
- Modify `web/Services/PassportPublishPolicyService.cs`: expose publish-before and auto-republish policy helpers.
- Modify `web/Services/ExternalApiRepository.cs`: add Sign token mode and delete/drop `batterySecrets` data.
- Modify `web/Services/ExternalApiInitializer.cs`: seed read, read-write, and sign sample/cluster tokens; stop creating battery secrets.
- Modify `web/Services/AccessControlService.cs`: add revised role constants, display labels, registry/report visibility, and Trust & conformance visibility.
- Modify `web/Services/AuthService.cs`: keep new role claims stable and expose principal refresh for account edits.
- Modify `web/Services/ClusterRepository.cs`: add linked counts, safe cluster delete helpers, user email/profile update helpers, and membership email migration.
- Modify `web/Controllers/AdminController.cs`: rename UI workflows, delegate trust workflow, remove secrets, add sign-token creation, archive/unarchive, safer cluster delete, and user role guardrails.
- Modify `web/Controllers/ClusterAdminController.cs`: remove secrets page/actions, add local-admin token management, block self role/membership downgrades, and preserve local-admin scope.
- Modify `web/Controllers/ExternalApiController.cs`: remove battery secret checks and software endpoint, add Battery version endpoint, validate endpoint, and sign endpoint.
- Modify `web/Controllers/PassportController.cs`: use revised visibility rules and Trust & conformance access.
- Modify `web/Controllers/RegistryController.cs`: use revised role visibility and icon-only summary/detail actions.
- Modify `web/Views/Admin/Clusters.cshtml`: Battery Family wording, compact actions, token management without secrets, sign mode, safer cluster delete UI.
- Modify `web/Views/Admin/EditPassport.cshtml`: Battery Family/Battery version/Battery serial number wording; remove Battery Name, Model Number, and software category selector.
- Modify `web/Views/Admin/NewPassport.cshtml` when the view exists in the working tree: align create copy and fields.
- Modify `web/Views/Admin/Product.cshtml`: Battery Family editor with Battery versions and scalar software parameters.
- Modify `web/Views/Admin/Passports.cshtml`: Passport status wording and compact archive/unarchive actions.
- Modify `web/Views/Admin/Conformance.cshtml`: reflect auto-republish messaging after first explicit publish.
- Modify `web/Views/ClusterAdmin/Users.cshtml`: self-protection and role labels.
- Modify `web/Views/ClusterAdmin/Passports.cshtml`: Passport status wording.
- Delete `web/Views/ClusterAdmin/Secrets.cshtml`: battery secrets no longer exist.
- Modify `web/Views/Passport/Summary.cshtml`: Passport status hero, Battery status field, Battery Family identity, no prominent image alt filename text.
- Modify `web/Views/Passport/Detail.cshtml`: Battery Family identity, no Software tab category, Trust tab visibility via revised roles.
- Modify `web/Views/Registry/Index.cshtml`: icon-only Summary report and Detailed report actions.
- Modify `web/Views/Shared/_Layout.cshtml`: customer name and strongest role under identity row, Account link, remove secrets nav.
- Create `web/Views/Account/Index.cshtml`: self-service account page.
- Modify `web/Views/Help/Index.cshtml`, `web/Views/Admin/Help.cshtml`: API docs and terminology updates.
- Modify `web/wwwroot/css/site.css`: compact icon buttons, status badges, token forms, account form, safer delete controls.
- Modify `docs/end-user-testing-guide.md`, `docs/qa-test-pack.md`, `docs/sample-cluster-test-accounts.md`: terminology, API, role, and battery secret removal docs.

---

### Task 1: Shared Revised Requirement Tests

**Files:**
- Create: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Create the failing cross-cutting test file**

Create `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs` with this content:

```csharp
using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class BatteryFamilyAccessApiRevisedTests
{
    [Fact]
    public void PassportFactory_ShouldExposeRevisedIdentityAndPassportStatus()
    {
        var passport = MinimalPassport(registryStatus: "draft", trustState: TrustState.Signed);

        var model = new PassportViewModelFactory().Create(passport);

        Assert.Equal("Compact 7M", model.BatteryFamily);
        Assert.Equal("2.0", model.BatteryVersion);
        Assert.Equal("SN-001", model.BatterySerialNumber);
        Assert.Equal("Signed", model.PassportStatus);
        Assert.Equal("Original", model.BatteryStatus);
    }

    [Fact]
    public void ProductTemplateSource_ShouldDocumentBatteryFamilyMapping()
    {
        var docs = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"))
            + File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));

        Assert.Contains("Battery Family", docs);
        Assert.Contains("Product Template", docs);
        Assert.Contains("Product Template is the internal implementation name for Battery Family", docs);
    }

    [Fact]
    public void UserFacingViews_ShouldNotUseRemovedIdentityLabels()
    {
        var files = new[]
        {
            RepoFile("web", "Views", "Registry", "Index.cshtml"),
            RepoFile("web", "Views", "Passport", "Summary.cshtml"),
            RepoFile("web", "Views", "Passport", "Detail.cshtml"),
            RepoFile("web", "Views", "Admin", "EditPassport.cshtml"),
            RepoFile("web", "Views", "Admin", "Clusters.cshtml"),
            RepoFile("web", "Views", "Admin", "Product.cshtml")
        };

        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.Contains("Battery Family", text);
        Assert.Contains("Battery version", text);
        Assert.Contains("Battery serial number", text);
        Assert.Contains("Passport status", text);
        Assert.Contains("Battery status", text);
        Assert.DoesNotContain(">Name <", text);
        Assert.DoesNotContain(">Model Number<", text);
        Assert.DoesNotContain("Product/battery version", text);
        Assert.DoesNotContain(">Product<", text);
        Assert.DoesNotContain("Product templates", text);
    }

    [Fact]
    public void Software_ShouldBeAParameterNotASeparateVariantWorkflow()
    {
        var modelSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateModels.cs"));
        var serviceSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
        var apiSource = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var productView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("string SoftwareVersion", modelSource);
        Assert.Contains("string SoftwareReleaseDate", modelSource);
        Assert.Contains("string SoftwareLatestUpdate", modelSource);
        Assert.DoesNotContain("IReadOnlyList<BatteryProductSoftwareVersion> SoftwareVersions", modelSource);
        Assert.DoesNotContain("batteryProductTemplateSoftwareVersions", serviceSource);
        Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/software\")]", apiSource);
        Assert.DoesNotContain("data-product-software", productView);
        Assert.DoesNotContain("Software versions", productView);
    }

    [Fact]
    public void ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign()
    {
        var api = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
        var initializer = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));

        Assert.Contains("ExternalTokenAccessMode.Sign", repository);
        Assert.Contains("ValidateTokenAsync(token, ExternalTokenRequirement.Sign", api);
        Assert.Contains("[HttpPatch(\"batteries/{passportId}/battery-version\")]", api);
        Assert.Contains("[HttpPost(\"batteries/{passportId}/validate\")]", api);
        Assert.Contains("[HttpPost(\"batteries/{passportId}/sign\")]", api);
        Assert.DoesNotContain("X-Battery-Secret", api);
        Assert.DoesNotContain("ValidateBatterySecretAsync", api);
        Assert.DoesNotContain("UpsertBatterySecretAsync", repository);
        Assert.Contains("DropCollectionAsync(\"batterySecrets\"", repository);
        Assert.DoesNotContain("EnsureBatterySecret", initializer);
    }

    [Fact]
    public void AccessControl_ShouldDefineRevisedRolesAndTrustVisibility()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));

        Assert.Contains("Normal User", source);
        Assert.Contains("Notified Body", source);
        Assert.Contains("Market Surveillance Authorities", source);
        Assert.Contains("Commission", source);
        Assert.Contains("Person with Legitimate Interest", source);
        Assert.Contains("CanViewTrustConformanceAsync", source);
        Assert.Contains("marketSurveillanceAuthority", source);
        Assert.Contains("commission", source);
        Assert.DoesNotContain("notifiedBody\") ||", source);
        Assert.DoesNotContain("legitimateInterest\") ||", source);
    }

    private static BsonDocument MinimalPassport(string registryStatus, string trustState)
    {
        return new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:test-001",
            ["registryInfo"] = new BsonDocument
            {
                ["status"] = registryStatus,
                ["updatedAt"] = "2026-05-11T00:00:00Z"
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = trustState,
                ["isDirty"] = false
            },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = trustState.Equals(TrustState.Signed, StringComparison.OrdinalIgnoreCase),
                ["hash"] = "hash-1"
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["name"] = "Old display name",
                    ["modelNumber"] = "Old model",
                    ["serialNumber"] = "SN-001",
                    ["manufacturerName"] = "Scania Industrial Batteries",
                    ["facilityId"] = "LINE-1"
                },
                ["product"] = new BsonDocument
                {
                    ["productId"] = "compact-7m",
                    ["productName"] = "Compact 7M",
                    ["productVersion"] = "2.0",
                    ["softwareVersion"] = "4.0",
                    ["softwareReleaseDate"] = "2026-05-01",
                    ["softwareLatestUpdate"] = "2026-05-08"
                },
                ["media"] = new BsonDocument(),
                ["documents"] = new BsonDocument(),
                ["operations"] = new BsonDocument()
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryStatus"] = "Original",
                        ["batteryCategory"] = "industrial",
                        ["batteryMass"] = 320,
                        ["manufacturingDate"] = "2026-05-01T00:00:00Z"
                    }
                }
            }
        };
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

- [ ] **Step 2: Run the test file and verify red**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryFamilyAccessApiRevisedTests" --no-restore
```

Expected: FAIL because the revised properties, wording, token mode, API endpoints, and role labels are not fully implemented.

- [ ] **Step 3: Commit the red tests**

```powershell
git add BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
git commit -m "test: capture revised battery family access API requirements"
```

---

### Task 2: Passport Identity And Status View Models

**Files:**
- Modify: `web/Models/ViewModels/PassportViewModel.cs`
- Modify: `web/Models/ViewModels/PassportSummaryViewModel.cs`
- Modify: `web/Services/PassportViewModelFactory.cs`
- Modify: `web/Services/PassportRepository.cs`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Add view-model properties**

In `web/Models/ViewModels/PassportViewModel.cs`, add these properties after `ClusterLabel`:

```csharp
public string BatteryFamily { get; init; } = string.Empty;
public string BatteryVersion { get; init; } = string.Empty;
public string BatterySerialNumber { get; init; } = string.Empty;
public string PassportStatus { get; init; } = "Draft";
```

In the same file, remove `"Software"` from `Sections` so it starts:

```csharp
public IReadOnlyList<string> Sections { get; init; } =
[
    "General",
    "Material composition",
    "Performance",
    "Compliance",
    "Supply chain",
    "Circularity",
    "Carbon Footprint"
];
```

In `web/Models/ViewModels/PassportSummaryViewModel.cs`, add:

```csharp
public string BatteryFamily { get; init; } = string.Empty;
public string BatteryVersion { get; init; } = string.Empty;
public string BatterySerialNumber { get; init; } = string.Empty;
public string PassportStatus { get; init; } = "Draft";
```

- [ ] **Step 2: Compute Passport status in factory and repository**

In `web/Services/PassportViewModelFactory.cs`, set the new properties in the returned `PassportViewModel`:

```csharp
BatteryFamily = BsonHelpers.GetString(appProduct, "productName"),
BatteryVersion = BsonHelpers.GetString(appProduct, "productVersion"),
BatterySerialNumber = serialNumber,
PassportStatus = BuildPassportStatus(document, trustState),
```

Add this helper near the other private helpers:

```csharp
private static string BuildPassportStatus(BsonDocument document, string trustState)
{
    var registryStatus = BsonHelpers.GetString(document, "registryInfo", "status");
    if (registryStatus.Equals("archived", StringComparison.OrdinalIgnoreCase))
    {
        return "Archived";
    }

    if (registryStatus.Equals("published", StringComparison.OrdinalIgnoreCase))
    {
        return "Published";
    }

    return trustState.Equals(TrustState.Signed, StringComparison.OrdinalIgnoreCase)
        ? "Signed"
        : "Draft";
}
```

In `web/Services/PassportRepository.cs`, update `ToSummary` to read product fields and return:

```csharp
BatteryFamily = BsonHelpers.GetString(document, "app", "product", "productName"),
BatteryVersion = BsonHelpers.GetString(document, "app", "product", "productVersion"),
BatterySerialNumber = serialNumber,
PassportStatus = BuildPassportStatus(document),
```

Add this helper near `ToSummary`:

```csharp
private static string BuildPassportStatus(BsonDocument document)
{
    var registryStatus = BsonHelpers.GetString(document, "registryInfo", "status");
    if (registryStatus.Equals("archived", StringComparison.OrdinalIgnoreCase))
    {
        return "Archived";
    }

    if (registryStatus.Equals("published", StringComparison.OrdinalIgnoreCase))
    {
        return "Published";
    }

    var trustState = BsonHelpers.GetString(document, "trust", "state");
    return trustState.Equals(TrustState.Signed, StringComparison.OrdinalIgnoreCase)
        ? "Signed"
        : "Draft";
}
```

- [ ] **Step 3: Run the identity/status tests**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PassportFactory_ShouldExposeRevisedIdentityAndPassportStatus" --no-restore
```

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
git add web/Models/ViewModels/PassportViewModel.cs web/Models/ViewModels/PassportSummaryViewModel.cs web/Services/PassportViewModelFactory.cs web/Services/PassportRepository.cs
git commit -m "feat: expose battery family identity and passport status"
```

---

### Task 3: Battery Family Model And Software Parameter Collapse

**Files:**
- Modify: `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`
- Modify: `web/Services/ProductTemplateModels.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Services/DataCompletionPolicyService.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Models/ViewModels/EditPassportViewModel.cs`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Update product template tests for Battery Family and scalar software parameters**

In `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`, replace tests that assert nested software versions with tests asserting:

```csharp
[Fact]
public void BatteryFamilyCatalog_ShouldSeedThreeFamiliesWithBatteryVersionsAndSoftwareParameters()
{
    var families = BatteryProductTemplateCatalog.DefaultProducts;

    Assert.Equal(["compact-7m", "compact-13m", "core"], families.Select(family => family.ProductId).ToArray());
    Assert.All(families, family =>
    {
        Assert.NotEmpty(family.ProductVersions);
        Assert.All(family.ProductVersions, version =>
        {
            Assert.False(string.IsNullOrWhiteSpace(version.SoftwareVersion));
            Assert.False(string.IsNullOrWhiteSpace(version.SoftwareReleaseDate));
            Assert.False(string.IsNullOrWhiteSpace(version.SoftwareLatestUpdate));
        });
    });
}
```

Update `BuildPassportFromTemplate_ShouldStoreProductVersionAndSoftwareVersion` so it calls the new builder overload without a software object and asserts the scalar software fields from the selected Battery version:

```csharp
var passport = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
    "did:web:acme.battery.pass:versioned-001",
    product,
    productVersion,
    new ProductTemplateBatteryIdentity(),
    "2026-05-11T10:00:00.0000000Z");

Assert.Equal(productVersion.SoftwareVersion, BsonHelpers.GetString(passport, "app", "product", "softwareVersion"));
Assert.Equal(productVersion.SoftwareReleaseDate, BsonHelpers.GetString(passport, "app", "product", "softwareReleaseDate"));
Assert.Equal(productVersion.SoftwareLatestUpdate, BsonHelpers.GetString(passport, "app", "product", "softwareLatestUpdate"));
```

- [ ] **Step 2: Run product tests and verify red**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ProductTemplateServiceTests|FullyQualifiedName~Software_ShouldBeAParameterNotASeparateVariantWorkflow" --no-restore
```

Expected: FAIL because `BatteryProductVersion` still contains nested software versions and the service still uses `batteryProductTemplateSoftwareVersions`.

- [ ] **Step 3: Refactor product template records**

In `web/Services/ProductTemplateModels.cs`, change `BatteryProductVersion` to:

```csharp
public sealed record BatteryProductVersion(
    string Version,
    double BatteryMassKg,
    double RatedEnergyKwh,
    double RatedCapacityAh,
    double RatedMaximumPowerKw,
    double NominalVoltageV,
    double ExpectedLifetimeYears,
    double ExpectedCycles,
    double SupplyChainIndex,
    double CarbonFootprint,
    string PerformanceClass,
    IReadOnlyDictionary<string, double> MaterialMassesKg,
    IReadOnlyDictionary<string, double> CarbonStages,
    IReadOnlyDictionary<string, ProductTemplateRecycledContent> RecycledContent,
    string SoftwareVersion,
    string SoftwareReleaseDate,
    string SoftwareLatestUpdate,
    IReadOnlyList<ProductTemplateDocumentSeed> TemplateDocuments,
    IReadOnlyList<string> RequiredFieldKeys);
```

Remove `IReadOnlyList<BatteryProductSoftwareVersion> SoftwareVersions` from `BatteryProductTemplate`. Remove the `BatteryProductSoftwareVersion` record after all references are gone.

Update default versions so each `BatteryProductVersion` receives scalar software values. For Compact 7M, use:

```csharp
SoftwareVersion: "4.0",
SoftwareReleaseDate: "2026-05-02",
SoftwareLatestUpdate: "2026-05-09",
```

for version `2.0`, and:

```csharp
SoftwareVersion: "2.0",
SoftwareReleaseDate: "2026-01-05",
SoftwareLatestUpdate: "2026-04-18",
```

for version `1.0`. Use the existing date values from `BuildSoftwareVersions(productId, productVersion)` for Compact 13M and Core.

- [ ] **Step 4: Refactor passport builder overloads**

In `ProductTemplatePassportBuilder`, remove overloads that accept `BatteryProductSoftwareVersion`. The remaining builder signature should be:

```csharp
public static BsonDocument BuildPassportFromTemplate(
    string passportId,
    BatteryProductTemplate product,
    BatteryProductVersion productVersion,
    ProductTemplateBatteryIdentity identity,
    string now)
```

In `ApplyProductTemplateValues`, replace software object usage with:

```csharp
productNode["softwareVersion"] = productVersion.SoftwareVersion;
productNode["softwareReleaseDate"] = productVersion.SoftwareReleaseDate;
productNode["softwareLatestUpdate"] = productVersion.SoftwareLatestUpdate;
```

- [ ] **Step 5: Remove software collection and software push from service**

In `web/Services/ProductTemplateService.cs`:

- remove `SoftwareCollection()`
- remove writes to `batteryProductTemplateSoftwareVersions`
- remove reads from `batteryProductTemplateSoftwareVersions`
- remove `PushTemplateAsync(... softwareVersion ...)`
- keep `PushProductVersionAsync(productId, productVersion, actor, cancellationToken)` and have it apply the Battery version scalar software parameters
- delete/drop the legacy software collection during initialization/reset:

```csharp
await _mongoContext.Database.DropCollectionAsync("batteryProductTemplateSoftwareVersions", cancellationToken);
```

Store software parameters on `batteryProductTemplateVersions` documents:

```csharp
["softwareVersion"] = productVersion.SoftwareVersion,
["softwareReleaseDate"] = productVersion.SoftwareReleaseDate,
["softwareLatestUpdate"] = productVersion.SoftwareLatestUpdate,
```

- [ ] **Step 6: Update admin form state**

In `web/Models/ViewModels/EditPassportViewModel.cs`, remove:

```csharp
public IReadOnlyList<ProductSoftwareVersionViewModel> ProductSoftwareVersions { get; init; } = [];
public string SelectedSoftwareVersion { get; init; } = string.Empty;
```

In `web/Models/ViewModels/AdminClusterViewModel.cs`, remove `ProductSoftwareVersionViewModel` and remove `SoftwareVersionCount` from `ProductTemplateSummaryViewModel`. Add:

```csharp
public int BatteryVersionCount { get; init; }
```

In `AdminController`, remove `softwareVersion` from `BuildDraftPassportDocumentAsync`, `ApplyPassportForm`, and `ApplySelectedProductTemplateMetadataAsync`. The selected Battery version is the only version selector.

- [ ] **Step 7: Run product/model tests**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ProductTemplateServiceTests|FullyQualifiedName~Software_ShouldBeAParameterNotASeparateVariantWorkflow" --no-restore
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add BatteryPassWeb.Tests/ProductTemplateServiceTests.cs web/Services/ProductTemplateModels.cs web/Services/ProductTemplateService.cs web/Services/DataCompletionPolicyService.cs web/Controllers/AdminController.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Models/ViewModels/EditPassportViewModel.cs
git commit -m "feat: make software a battery version parameter"
```

---

### Task 4: Battery Family UI Terminology And Identity Fields

**Files:**
- Modify: `web/Views/Admin/EditPassport.cshtml`
- Modify: `web/Views/Admin/Product.cshtml`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Admin/Help.cshtml`
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `web/Views/Help/Index.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Run terminology tests and verify red**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~UserFacingViews_ShouldNotUseRemovedIdentityLabels" --no-restore
```

Expected: FAIL because views still show Product, Product templates, Product/battery version, Name, and Model Number labels.

- [ ] **Step 2: Update admin edit/create labels and remove fields**

In `web/Views/Admin/EditPassport.cshtml`:

- remove the `general.name` label/input
- remove the `general.modelNumber` label/input
- change Serial Number label to `Battery serial number`
- change Status label to `Battery status`
- change Registry status label to `Passport status`
- change Product label to `Battery Family`
- change Product/battery version label to `Battery version`
- remove the Software section and `softwareVersion` select
- keep software version, release date, and latest update as read-only Battery version parameters in the General section:

```cshtml
<label class="bp-field-shell">
    <span>Software version</span>
    <input type="text" value="@passport.SoftwareVersion" readonly />
</label>
<label class="bp-field-shell">
    <span>Released date</span>
    <input type="text" value="@passport.SoftwareReleaseDate" readonly />
</label>
<label class="bp-field-shell">
    <span>Latest update</span>
    <input type="text" value="@passport.SoftwareLatestUpdate" readonly />
</label>
```

- [ ] **Step 3: Update product editor labels**

In `web/Views/Admin/Product.cshtml`:

- change page title and headings from Product/Product template to Battery Family
- change add/edit buttons from product/template wording to Battery Family wording
- change Product/battery version to Battery version
- remove nested software-version add/remove UI
- inside each Battery version editor, render scalar fields:

```cshtml
<label class="bp-field-shell">
    <span>Software version</span>
    <input type="text" name="softwareVersion" value="@version.SoftwareVersion" />
</label>
<label class="bp-field-shell">
    <span>Released date</span>
    <input type="date" name="softwareReleaseDate" value="@version.SoftwareReleaseDate" />
</label>
<label class="bp-field-shell">
    <span>Latest update</span>
    <input type="date" name="softwareLatestUpdate" value="@version.SoftwareLatestUpdate" />
</label>
```

- [ ] **Step 4: Update summary, detail, registry, and admin list identity**

Use these display labels wherever the old identity appeared:

```text
Battery Family
Battery version
Battery serial number
Passport status
Battery status
```

Registry and admin passport lists should show:

```text
Battery ID | Battery Family | Battery version | Battery serial number | Passport status | Updated | Actions
```

Do not show Battery Name or Model Number in these user-facing lists.

- [ ] **Step 5: Run terminology tests**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~UserFacingViews_ShouldNotUseRemovedIdentityLabels" --no-restore
```

Expected: PASS.

- [ ] **Step 6: Commit UI terminology**

```powershell
git add web/Views/Admin/EditPassport.cshtml web/Views/Admin/Product.cshtml web/Views/Admin/Clusters.cshtml web/Views/Admin/Help.cshtml web/Views/Passport/Summary.cshtml web/Views/Passport/Detail.cshtml web/Views/Registry/Index.cshtml web/Views/Help/Index.cshtml web/wwwroot/css/site.css
git commit -m "feat: rename product UI to battery family"
```

---

### Task 5: Battery Version Push And External API Battery Version Change

**Files:**
- Modify: `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`
- Modify: `BatteryPassWeb.Tests/ExternalApiSoftwareVersionTests.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ExternalApiController.cs`
- Modify: `web/Views/Admin/Product.cshtml`
- Modify: `web/Views/Admin/Help.cshtml`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Replace software API tests with Battery version API tests**

Rename the test file `BatteryPassWeb.Tests/ExternalApiSoftwareVersionTests.cs` to `BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs`.

Replace its assertions with source tests that require:

```csharp
Assert.Contains("[HttpPatch(\"batteries/{passportId}/battery-version\")]", apiSource);
Assert.Contains("batteryVersion", apiSource);
Assert.Contains("validationSigningRequired = true", apiSource);
Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/software\")]", apiSource);
Assert.DoesNotContain("softwareVersion is required", apiSource);
```

- [ ] **Step 2: Run API/version tests and verify red**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiBatteryVersionTests|FullyQualifiedName~ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign" --no-restore
```

Expected: FAIL because the external API still exposes `/software` and no `/battery-version`.

- [ ] **Step 3: Add safe single-passport Battery version change**

In `ProductTemplateService`, add:

```csharp
public async Task<ProductTemplateSafeUpdateResult?> ChangePassportBatteryVersionAsync(
    BsonDocument passport,
    string requestedBatteryVersion,
    string actor,
    CancellationToken cancellationToken = default)
{
    var productId = BsonHelpers.GetString(passport, "app", "product", "productId");
    var product = await GetProductAsync(productId, cancellationToken);
    if (product == null)
    {
        return null;
    }

    var selectedVersion = product.ProductVersions.FirstOrDefault(version =>
        version.Version.Equals(requestedBatteryVersion, StringComparison.OrdinalIgnoreCase));
    if (selectedVersion == null)
    {
        return null;
    }

    var result = BuildSafeTemplatePushUpdate(passport, product, selectedVersion, DateTime.UtcNow.ToString("O"));
    return result;
}
```

Change `BuildSafeTemplatePushUpdate` to accept only `BatteryProductVersion selectedProductVersion` and use that version's scalar software parameters. Preserve:

- `passportId`
- `clusterId`
- `app.product.productId`
- `app.display.serialNumber`
- `app.display.facilityId`
- manufacturing date
- `app.operations`
- telemetry history

- [ ] **Step 4: Add external Battery version endpoint**

In `ExternalApiController`, remove `UpdateSoftwareVersion`.

Add:

```csharp
[HttpPatch("batteries/{passportId}/battery-version")]
public async Task<IActionResult> UpdateBatteryVersion(string passportId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
{
    var auth = await AuthorizeAsync(passportId, ExternalTokenRequirement.Write, cancellationToken);
    if (auth.ErrorResult != null)
    {
        return auth.ErrorResult;
    }

    if (payload.ValueKind != JsonValueKind.Object
        || !TryGetPropertyIgnoreCase(payload, "batteryVersion", out var batteryVersionElement)
        || batteryVersionElement.ValueKind != JsonValueKind.String
        || string.IsNullOrWhiteSpace(batteryVersionElement.GetString()))
    {
        return Envelope(StatusCodes.Status400BadRequest, "batteryVersion is required and must be a string.");
    }

    var result = await _productTemplateService.ChangePassportBatteryVersionAsync(
        auth.Passport!,
        batteryVersionElement.GetString()!.Trim(),
        auth.TokenContext!.Name,
        cancellationToken);
    if (result == null)
    {
        return Envelope(StatusCodes.Status400BadRequest, "Battery version is not defined for this Battery Family.");
    }

    await _passportRepository.ReplaceAsync(passportId, result.UpdatedPassport, cancellationToken);
    await _passportRepository.MarkCanonicalDirtyAsync(passportId, "batteryVersionChanged", cancellationToken);
    return Envelope(StatusCodes.Status200OK, "Battery version updated. Validation and signing are required.", new
    {
        passportId,
        batteryFamily = BsonHelpers.GetString(result.UpdatedPassport, "app", "product", "productName"),
        batteryVersion = BsonHelpers.GetString(result.UpdatedPassport, "app", "product", "productVersion"),
        validationSigningRequired = true,
        changedPaths = result.UpdatedPaths
    });
}
```

- [ ] **Step 5: Verify telemetry remains operational**

Confirm `WriteTelemetry` and `UpdateOperations` still call `UpdateFieldsAsync` and do not call `MarkCanonicalDirtyAsync`.

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiBatteryVersionTests|FullyQualifiedName~ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign|FullyQualifiedName~ProductTemplateServiceTests" --no-restore
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -A BatteryPassWeb.Tests/ExternalApiSoftwareVersionTests.cs BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs BatteryPassWeb.Tests/ProductTemplateServiceTests.cs web/Services/ProductTemplateService.cs web/Controllers/AdminController.cs web/Controllers/ExternalApiController.cs web/Views/Admin/Product.cshtml web/Views/Admin/Help.cshtml
git commit -m "feat: change batteries by battery version"
```

---

### Task 6: Shared Validate, Sign, Publish, And Auto-Republish Workflow

**Files:**
- Create: `web/Services/PassportTrustWorkflowService.cs`
- Modify: `web/Program.cs`
- Modify: `web/Services/PassportRepository.cs`
- Modify: `web/Services/PassportPublishPolicyService.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ExternalApiController.cs`
- Modify: `web/Views/Admin/Conformance.cshtml`
- Modify: `BatteryPassWeb.Tests/PassportPublishPolicyServiceTests.cs`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Add publish lifecycle policy tests**

In `BatteryPassWeb.Tests/PassportPublishPolicyServiceTests.cs`, add:

```csharp
[Fact]
public void HasBeenPublishedBefore_ShouldUseDurableHistoricalFlag()
{
    var service = new PassportPublishPolicyService();

    Assert.False(service.HasBeenPublishedBefore(new BsonDocument()));
    Assert.True(service.HasBeenPublishedBefore(new BsonDocument
    {
        ["registryInfo"] = new BsonDocument
        {
            ["hasBeenPublished"] = true,
            ["status"] = "draft"
        }
    }));
}
```

- [ ] **Step 2: Run publish policy tests and verify red**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PassportPublishPolicyServiceTests" --no-restore
```

Expected: FAIL because `HasBeenPublishedBefore` does not exist.

- [ ] **Step 3: Add durable publish marker**

In `PassportPublishPolicyService`, add:

```csharp
public bool HasBeenPublishedBefore(BsonDocument passport)
{
    return BsonHelpers.GetValue(passport, "registryInfo", "hasBeenPublished") is { IsBoolean: true } value
        && value.AsBoolean;
}

public bool ShouldAutoPublishAfterSign(BsonDocument passport)
{
    return HasBeenPublishedBefore(passport);
}
```

In `PassportRepository.PublishPassportAsync`, add:

```csharp
.Set("registryInfo.hasBeenPublished", true)
```

to the update definition.

- [ ] **Step 4: Create workflow service**

Create `web/Services/PassportTrustWorkflowService.cs`:

```csharp
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed record PassportTrustWorkflowResult(
    bool Success,
    string Message,
    TrustValidationSummary? ValidationSummary,
    string RevisionId,
    bool AutoPublished);

public sealed class PassportTrustWorkflowService
{
    private readonly PassportRepository _passportRepository;
    private readonly DataCompletionPolicyService _dataCompletionPolicyService;
    private readonly PassportValidationService _passportValidationService;
    private readonly PassportEvidenceService _passportEvidenceService;
    private readonly AuditRevisionService _auditRevisionService;
    private readonly PassportTrustService _passportTrustService;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;

    public PassportTrustWorkflowService(
        PassportRepository passportRepository,
        DataCompletionPolicyService dataCompletionPolicyService,
        PassportValidationService passportValidationService,
        PassportEvidenceService passportEvidenceService,
        AuditRevisionService auditRevisionService,
        PassportTrustService passportTrustService,
        PassportPublishPolicyService passportPublishPolicyService)
    {
        _passportRepository = passportRepository;
        _dataCompletionPolicyService = dataCompletionPolicyService;
        _passportValidationService = passportValidationService;
        _passportEvidenceService = passportEvidenceService;
        _auditRevisionService = auditRevisionService;
        _passportTrustService = passportTrustService;
        _passportPublishPolicyService = passportPublishPolicyService;
    }

    public async Task<PassportTrustWorkflowResult> ValidateAsync(string passportId, string actor, string source, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return new(false, "Battery passport was not found.", null, string.Empty, false);
        }

        var summary = await ValidateDocumentAsync(passportId, document, cancellationToken);
        await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
        await _auditRevisionService.AppendAuditEventAsync(passportId, "passport.validated", actor, source, source, "Passport validation completed.", new BsonDocument
        {
            ["blockingErrors"] = summary.BlockingErrorCount,
            ["warnings"] = summary.WarningCount,
            ["canSign"] = summary.CanSign
        }, cancellationToken);

        return new(true, "Passport validation completed.", summary, string.Empty, false);
    }

    public async Task<PassportTrustWorkflowResult> SignAsync(string passportId, string actor, string source, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return new(false, "Battery passport was not found.", null, string.Empty, false);
        }

        var summary = await ValidateDocumentAsync(passportId, document, cancellationToken);
        if (!_passportPublishPolicyService.CanSign(summary))
        {
            await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
            return new(false, "Resolve blocking validation errors before signing.", summary, string.Empty, false);
        }

        var signature = _passportTrustService.Sign(document, actor);
        var revision = await _auditRevisionService.CreateSignedRevisionAsync(passportId, signature.Snapshot, signature.Hash, signature.Proof, actor, signature.SignedAt, cancellationToken);
        var revisionId = BsonHelpers.GetString(revision, "revisionId");
        await _passportRepository.UpdateTrustSignatureAsync(passportId, summary, signature.Hash, signature.Proof, revisionId, signature.SignedAt, cancellationToken);

        var autoPublished = false;
        if (_passportPublishPolicyService.ShouldAutoPublishAfterSign(document))
        {
            var publishedAt = DateTimeOffset.UtcNow.ToString("O");
            await _passportRepository.PublishPassportAsync(passportId, revisionId, publishedAt, $"sha256:{signature.Hash}", signature.Proof, cancellationToken);
            await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, publishedAt, cancellationToken);
            autoPublished = true;
        }

        await _auditRevisionService.AppendAuditEventAsync(passportId, autoPublished ? "passport.signed.autoPublished" : "passport.signed", actor, source, source, "Passport signed.", new BsonDocument
        {
            ["revisionId"] = revisionId,
            ["autoPublished"] = autoPublished
        }, cancellationToken);

        return new(true, autoPublished ? "Passport signed and published." : "Passport signed.", summary, revisionId, autoPublished);
    }

    private async Task<TrustValidationSummary> ValidateDocumentAsync(string passportId, BsonDocument document, CancellationToken cancellationToken)
    {
        var policy = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
        var summary = _passportValidationService.Validate(document, policy);
        var latestRevision = await _auditRevisionService.GetLatestSignedRevisionAsync(passportId, cancellationToken);
        var evidencePack = _passportEvidenceService.Evaluate(document, latestRevision, policy);
        return PassportEvidenceService.AppendValidationSection(summary, evidencePack);
    }
}
```

Register it in `web/Program.cs`:

```csharp
builder.Services.AddSingleton<PassportTrustWorkflowService>();
```

- [ ] **Step 5: Delegate admin validate/sign**

In `AdminController`, inject `PassportTrustWorkflowService`. Change `ValidatePassport` and `SignPassport` to call the workflow service. Redirect signed results to:

```csharp
return Redirect(BuildConformanceRedirect(passportId, status: result.AutoPublished ? "published" : "signed"));
```

Keep explicit `PublishPassport` for first publish.

- [ ] **Step 6: Add API validate/sign endpoints**

In `ExternalApiController`, inject `PassportTrustWorkflowService` and add:

```csharp
[HttpPost("batteries/{passportId}/validate")]
public async Task<IActionResult> ValidatePassport(string passportId, CancellationToken cancellationToken)
{
    var auth = await AuthorizeAsync(passportId, ExternalTokenRequirement.Sign, cancellationToken);
    if (auth.ErrorResult != null)
    {
        return auth.ErrorResult;
    }

    var result = await _passportTrustWorkflowService.ValidateAsync(passportId, auth.TokenContext!.Name, "external-api", cancellationToken);
    return Envelope(result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, result.Message, new
    {
        passportId,
        blockingErrors = result.ValidationSummary?.BlockingErrorCount ?? 0,
        warnings = result.ValidationSummary?.WarningCount ?? 0,
        canSign = result.ValidationSummary?.CanSign ?? false
    });
}

[HttpPost("batteries/{passportId}/sign")]
public async Task<IActionResult> SignPassport(string passportId, CancellationToken cancellationToken)
{
    var auth = await AuthorizeAsync(passportId, ExternalTokenRequirement.Sign, cancellationToken);
    if (auth.ErrorResult != null)
    {
        return auth.ErrorResult;
    }

    var result = await _passportTrustWorkflowService.SignAsync(passportId, auth.TokenContext!.Name, "external-api", cancellationToken);
    return Envelope(result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, result.Message, new
    {
        passportId,
        revisionId = result.RevisionId,
        autoPublished = result.AutoPublished,
        passportStatus = result.AutoPublished ? "Published" : "Signed",
        blockingErrors = result.ValidationSummary?.BlockingErrorCount ?? 0,
        warnings = result.ValidationSummary?.WarningCount ?? 0
    });
}
```

- [ ] **Step 7: Run lifecycle/API tests**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PassportPublishPolicyServiceTests|FullyQualifiedName~ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign" --no-restore
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add BatteryPassWeb.Tests/PassportPublishPolicyServiceTests.cs web/Services/PassportTrustWorkflowService.cs web/Program.cs web/Services/PassportRepository.cs web/Services/PassportPublishPolicyService.cs web/Controllers/AdminController.cs web/Controllers/ExternalApiController.cs web/Views/Admin/Conformance.cshtml
git commit -m "feat: share validate sign publish workflow"
```

---

### Task 7: API Tokens And Battery Secrets Removal

**Files:**
- Modify: `BatteryPassWeb.Tests/ExternalApiSecurityServiceTests.cs`
- Modify: `BatteryPassWeb.Tests/ExternalApiInitializerTests.cs`
- Modify: `web/Services/ExternalApiRepository.cs`
- Modify: `web/Services/ExternalApiInitializer.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Delete: `web/Views/ClusterAdmin/Secrets.cshtml`
- Modify: `web/Views/Shared/_Layout.cshtml`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Add token mode tests**

In `BatteryPassWeb.Tests/ExternalApiSecurityServiceTests.cs`, add a source test:

```csharp
[Fact]
public void ExternalApiRepository_ShouldSupportSignModeAndDropBatterySecrets()
{
    var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));

    Assert.Contains("Sign", repository);
    Assert.Contains("ExternalTokenRequirement.Sign", repository);
    Assert.Contains("DropCollectionAsync(\"batterySecrets\"", repository);
    Assert.DoesNotContain("BatterySecretValidationResult", repository);
    Assert.DoesNotContain("ValidateBatterySecretAsync", repository);
}
```

Add a `RepoFile` helper to that test file if it does not already have one.

- [ ] **Step 2: Run token tests and verify red**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiRepository_ShouldSupportSignModeAndDropBatterySecrets|FullyQualifiedName~ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign" --no-restore
```

Expected: FAIL because repository still supports only read/readWrite and still manages battery secrets.

- [ ] **Step 3: Replace token validation boolean with requirement enum**

In `ExternalApiRepository`, change the enum to:

```csharp
public enum ExternalTokenAccessMode
{
    Read,
    ReadWrite,
    Sign
}

public enum ExternalTokenRequirement
{
    Read,
    Write,
    Sign
}
```

Change token creation storage:

```csharp
["accessMode"] = accessMode switch
{
    ExternalTokenAccessMode.ReadWrite => "readWrite",
    ExternalTokenAccessMode.Sign => "sign",
    _ => "read"
},
```

Change validation signature:

```csharp
public async Task<ExternalApiTokenValidationResult> ValidateTokenAsync(
    string rawToken,
    ExternalTokenRequirement requirement,
    CancellationToken cancellationToken = default)
```

Use this requirement check:

```csharp
var allowed = requirement switch
{
    ExternalTokenRequirement.Write => context.AccessMode == ExternalTokenAccessMode.ReadWrite,
    ExternalTokenRequirement.Sign => context.AccessMode == ExternalTokenAccessMode.Sign,
    _ => context.AccessMode is ExternalTokenAccessMode.Read or ExternalTokenAccessMode.ReadWrite
};
if (!allowed)
{
    return new ExternalApiTokenValidationResult
    {
        Success = false,
        StatusCode = StatusCodes.Status403Forbidden,
        Message = requirement == ExternalTokenRequirement.Sign
            ? "Token does not have sign access."
            : "Token does not have write access.",
        Context = context
    };
}
```

- [ ] **Step 4: Delete battery secret repository behavior**

Remove these types and methods from `ExternalApiRepository`:

- `BatterySecretValidationResult`
- `ListBatterySecretsAsync`
- `GetBatterySecretAsync`
- `UpsertBatterySecretAsync`
- `SetBatterySecretActiveAsync`
- `ValidateBatterySecretAsync`
- `RevealBatterySecret`

In `EnsureIndexesAsync`, delete the secret index creation and add:

```csharp
var collections = await _mongoContext.Database.ListCollectionNames().ToListAsync(cancellationToken);
if (collections.Contains("batterySecrets", StringComparer.OrdinalIgnoreCase))
{
    await _mongoContext.Database.DropCollectionAsync("batterySecrets", cancellationToken);
}
```

- [ ] **Step 5: Update controllers and views**

In `ExternalApiController.AuthorizeAsync`, remove the `X-Battery-Secret` header check and call:

```csharp
var tokenValidation = await _externalApiRepository.ValidateTokenAsync(token, requirement, cancellationToken);
```

In `AdminController` and `ClusterAdminController`, remove battery secret actions. Add local-admin token management actions in `ClusterAdminController` that call `CreateTokenAsync`, `RegenerateTokenAsync`, and `SetTokenActiveAsync` only after checking every submitted cluster with `CanAdministerClusterAsync`.

In `Admin/Clusters.cshtml`, remove the Battery secrets tab/panel and add Sign to the token access mode select:

```cshtml
<option value="sign">Sign</option>
```

Delete `web/Views/ClusterAdmin/Secrets.cshtml` and remove secrets navigation from `_Layout.cshtml`.

- [ ] **Step 6: Seed sign tokens**

In `ExternalApiInitializer`, add sample sign token constants and create one sign token for each cluster in `EnsureClusterTokensAsync`:

```csharp
await _externalApiRepository.CreateTokenAsync(
    $"{clusterName} - sign",
    ExternalTokenAccessMode.Sign,
    [clusterId],
    allowUnassigned: false,
    globalAccess: false,
    actor: "system",
    autoClusterId: clusterId,
    cancellationToken: cancellationToken);
```

- [ ] **Step 7: Run token/secrets tests**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiRepository_ShouldSupportSignModeAndDropBatterySecrets|FullyQualifiedName~ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign|FullyQualifiedName~ExternalApiInitializerTests" --no-restore
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add BatteryPassWeb.Tests/ExternalApiSecurityServiceTests.cs BatteryPassWeb.Tests/ExternalApiInitializerTests.cs web/Services/ExternalApiRepository.cs web/Services/ExternalApiInitializer.cs web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Views/Admin/Clusters.cshtml web/Views/Shared/_Layout.cshtml
git add -u web/Views/ClusterAdmin/Secrets.cshtml
git commit -m "feat: replace battery secrets with sign tokens"
```

---

### Task 8: Revised Roles And Visibility

**Files:**
- Modify: `BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs`
- Modify: `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`
- Modify: `web/Services/AccessControlService.cs`
- Modify: `web/Services/AuthService.cs`
- Modify: `web/Services/ClusterRepository.cs`
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Users.cshtml`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Add role visibility tests**

In `BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs`, add source tests for `AccessControlService`:

```csharp
[Fact]
public void AccessControlService_ShouldEncodeRevisedRoleVisibility()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));

    Assert.Contains("RoleNormalUser", source);
    Assert.Contains("RoleNotifiedBody", source);
    Assert.Contains("RoleMarketSurveillanceAuthority", source);
    Assert.Contains("RoleCommission", source);
    Assert.Contains("RoleLegitimateInterest", source);
    Assert.Contains("CanSeeDraftPassports", source);
    Assert.Contains("CanSeeSignedPassports", source);
    Assert.Contains("CanViewTrustConformanceAsync", source);
}
```

Add or reuse a `RepoFile` helper.

- [ ] **Step 2: Run role tests and verify red**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AccessControlService_ShouldEncodeRevisedRoleVisibility|FullyQualifiedName~AccessControl_ShouldDefineRevisedRolesAndTrustVisibility" --no-restore
```

Expected: FAIL because roles are still only admin, clusterAdmin, and member/viewer.

- [ ] **Step 3: Add role constants and display labels**

In `AccessControlService`, add:

```csharp
public const string RoleAdmin = "admin";
public const string RoleClusterAdmin = "clusterAdmin";
public const string RoleNormalUser = "member";
public const string RoleNotifiedBody = "notifiedBody";
public const string RoleMarketSurveillanceAuthority = "marketSurveillanceAuthority";
public const string RoleCommission = "commission";
public const string RoleLegitimateInterest = "legitimateInterest";
```

Add display labels:

```csharp
public static string DisplayRoleLabel(ClaimsPrincipal user)
{
    if (user.IsInRole(RoleAdmin)) return "Global Admin";
    if (user.IsInRole(RoleClusterAdmin)) return "Local Admin";
    if (user.IsInRole(RoleCommission)) return "Commission";
    if (user.IsInRole(RoleMarketSurveillanceAuthority)) return "Market Surveillance Authorities";
    if (user.IsInRole(RoleNotifiedBody)) return "Notified Body";
    if (user.IsInRole(RoleLegitimateInterest)) return "Person with Legitimate Interest";
    return "Normal User";
}
```

- [ ] **Step 4: Add visibility helpers**

In `AccessControlService`, add helpers used by controllers:

```csharp
public static bool CanSeeDraftPassports(ClaimsPrincipal user) =>
    IsAdmin(user) || user.IsInRole(RoleCommission);

public static bool CanSeeSignedPassports(ClaimsPrincipal user) =>
    IsAdmin(user)
    || user.IsInRole(RoleNotifiedBody)
    || user.IsInRole(RoleMarketSurveillanceAuthority)
    || user.IsInRole(RoleCommission)
    || user.IsInRole(RoleLegitimateInterest);

public static bool HasGlobalReportReadRole(ClaimsPrincipal user) =>
    user.IsInRole(RoleNotifiedBody)
    || user.IsInRole(RoleMarketSurveillanceAuthority)
    || user.IsInRole(RoleCommission)
    || user.IsInRole(RoleLegitimateInterest);
```

Update `CanOpenPassportDetailAsync`:

- Global Admin: all
- Local Admin: administered clusters, same as current
- Notified Body, Market Surveillance Authorities, Commission, Person with Legitimate Interest: all clusters according to lifecycle visibility
- Normal User: assigned clusters only

Update `CanViewTrustConformanceAsync` so only Global Admin, Local Admin for administered clusters, Market Surveillance Authorities, and Commission return true.

- [ ] **Step 5: Update registry and passport controllers**

In `RegistryController`, filter documents:

- Global Admin: include archived and all statuses
- Local Admin: non-archived managed cluster passports
- Commission: non-archived draft, signed, published in all clusters
- Notified Body, Market Surveillance Authorities, Person with Legitimate Interest: non-archived signed or published in all clusters
- Normal User: non-archived published passports in assigned clusters

In `PassportController`, use the same lifecycle visibility for summary/detail. Keep archived hidden outside admin workflows.

- [ ] **Step 6: Update user role forms**

In `Admin/Clusters.cshtml` and `ClusterAdmin/Users.cshtml`, expose the exact role labels:

```text
Normal User
Notified Body
Market Surveillance Authorities
Commission
Person with Legitimate Interest
Local Admin
Global Admin
```

Store machine keys from `AccessControlService` constants.

- [ ] **Step 7: Run role tests**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PublicVisibilityPolicyTests|FullyQualifiedName~DocumentAccessControlTests|FullyQualifiedName~AccessControl_ShouldDefineRevisedRolesAndTrustVisibility" --no-restore
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs BatteryPassWeb.Tests/DocumentAccessControlTests.cs web/Services/AccessControlService.cs web/Services/AuthService.cs web/Services/ClusterRepository.cs web/Controllers/RegistryController.cs web/Controllers/PassportController.cs web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/Users.cshtml
git commit -m "feat: implement revised access roles"
```

---

### Task 9: Registry, Passport, And Admin Guardrail UX

**Files:**
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/Views/Admin/Passports.cshtml`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Passports.cshtml`
- Modify: `web/Services/PassportRepository.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/wwwroot/css/site.css`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Add UX source tests**

Append this test to `BatteryFamilyAccessApiRevisedTests`:

```csharp
[Fact]
public void RegistryAndAdminViews_ShouldUseIconActionsAndRecoverableArchive()
{
    var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
    var adminClusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
    var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

    Assert.Contains("aria-label=\"Summary report\"", registry);
    Assert.Contains("title=\"Summary report\"", registry);
    Assert.Contains("aria-label=\"Detailed report\"", registry);
    Assert.Contains("title=\"Detailed report\"", registry);
    Assert.Contains("title=\"Edit\"", adminClusters);
    Assert.Contains("title=\"Conformance\"", adminClusters);
    Assert.Contains("title=\"Archive\"", adminClusters);
    Assert.Contains("title=\"Unarchive\"", adminClusters);
    Assert.Contains("return confirm('Archive passport", adminClusters);
    Assert.Contains("UnarchivePassportAsync", repository);
    Assert.Contains("[HttpPost(\"passports/unarchive\")]", controller);
}
```

- [ ] **Step 2: Run UX tests and verify red**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~RegistryAndAdminViews_ShouldUseIconActionsAndRecoverableArchive" --no-restore
```

Expected: FAIL until the registry actions, admin icons, archive confirmation, and unarchive route are present.

- [ ] **Step 3: Registry icon actions**

In `Registry/Index.cshtml`, replace action text buttons with icon-only links:

```cshtml
<a href="/@Uri.EscapeDataString(row.PassportId)/summary" class="bp-icon-button" aria-label="Summary report" title="Summary report">
    <span aria-hidden="true">S</span>
</a>
<a href="/@Uri.EscapeDataString(row.PassportId)" class="bp-icon-button" aria-label="Detailed report" title="Detailed report">
    <span aria-hidden="true">D</span>
</a>
```

Use stable button dimensions in CSS:

```css
.bp-icon-button {
  inline-size: 2rem;
  block-size: 2rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
```

- [ ] **Step 4: Passport summary/detail status labels**

In `Passport/Summary.cshtml` and `Passport/Detail.cshtml`:

- hero badge uses `@passport.PassportStatus`
- field labels include `Passport status` and `Battery status`
- no prominent `<p>` renders `@passport.BatteryImageAlt`
- identity fields are Battery Family, Battery version, Battery serial number

- [ ] **Step 5: Admin compact actions and unarchive**

In `PassportRepository`, add `UnarchivePassportAsync` that restores `registryInfo.previousStatus` or `draft`.

In `ArchivePassportAsync`, store:

```csharp
.Set("registryInfo.previousStatus", previousStatus)
.Set("registryInfo.status", "archived")
```

In `AdminController`, add:

```csharp
[HttpPost("passports/unarchive")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UnarchivePassport(CancellationToken cancellationToken)
{
    var passportId = Text(Request.Form, "passportId");
    if (!string.IsNullOrWhiteSpace(passportId))
    {
        await _passportRepository.UnarchivePassportAsync(passportId, cancellationToken);
    }

    return Redirect("/admin/clusters?tab=passports");
}
```

In admin passport list action cells, use compact icon buttons with `title` and `aria-label` values `Edit`, `Conformance`, `Archive`, and `Unarchive`. Archive form button includes:

```cshtml
onclick="return confirm('Archive passport @row.PassportId? You can unarchive it from Administration.');"
```

- [ ] **Step 6: Run UX tests**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~RegistryAndAdminViews_ShouldUseIconActionsAndRecoverableArchive|FullyQualifiedName~PassportFactory_ShouldExposeRevisedIdentityAndPassportStatus" --no-restore
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs web/Views/Registry/Index.cshtml web/Views/Passport/Summary.cshtml web/Views/Passport/Detail.cshtml web/Views/Admin/Passports.cshtml web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/Passports.cshtml web/Services/PassportRepository.cs web/Controllers/AdminController.cs web/wwwroot/css/site.css
git commit -m "feat: polish registry status and archive actions"
```

---

### Task 10: Cluster Delete, User Self-Protection, And Account Self-Service

**Files:**
- Create: `web/Models/ViewModels/AccountProfileViewModel.cs`
- Create: `web/Controllers/AccountController.cs`
- Create: `web/Views/Account/Index.cshtml`
- Modify: `web/Services/ClusterRepository.cs`
- Modify: `web/Services/AuthService.cs`
- Modify: `web/Services/AccessControlService.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Users.cshtml`
- Modify: `web/Views/Shared/_Layout.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Add account and cluster guardrail tests**

Append this test to `BatteryFamilyAccessApiRevisedTests`:

```csharp
[Fact]
public void AccountAndClusterGuardrails_ShouldBePresent()
{
    var accountController = RepoFile("web", "Controllers", "AccountController.cs");
    var accountView = RepoFile("web", "Views", "Account", "Index.cshtml");
    var clusterRepository = File.ReadAllText(RepoFile("web", "Services", "ClusterRepository.cs"));
    var authService = File.ReadAllText(RepoFile("web", "Services", "AuthService.cs"));
    var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
    var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
    var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));

    Assert.True(File.Exists(accountController));
    Assert.True(File.Exists(accountView));
    Assert.Contains("CountPassportsByClusterAsync", clusterRepository);
    Assert.Contains("CountMembershipsByClusterAsync", clusterRepository);
    Assert.Contains("ForceDeleteClusterAsync", adminController);
    Assert.Contains("I understand this deletes linked cluster data", adminController);
    Assert.Contains("CreatePrincipalForUserAsync", authService);
    Assert.Contains("UpdateUserEmailAsync", clusterRepository);
    Assert.Contains("UpdateUserProfileAsync", clusterRepository);
    Assert.Contains("Cannot change your own local admin role", clusterAdminController);
    Assert.Contains("href=\"/account\"", layout);
    Assert.Contains("@customerName / @customerRole", layout);
}
```

- [ ] **Step 2: Run guardrail tests and verify red**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AccountAndClusterGuardrails_ShouldBePresent" --no-restore
```

Expected: FAIL until the account page, delete guards, and self-protection are implemented.

- [ ] **Step 3: Cluster delete safeguards**

In `ClusterRepository`, add:

```csharp
public async Task<long> CountMembershipsByClusterAsync(string clusterId, CancellationToken cancellationToken = default)
```

In `PassportRepository`, add:

```csharp
public async Task<long> CountPassportsByClusterAsync(string clusterId, CancellationToken cancellationToken = default)
```

In `AdminController.DeleteCluster`, block deletion when either count is above zero and redirect with an error. Add `ForceDeleteClusterAsync` route that requires exact form value:

```text
I understand this deletes linked cluster data
```

The force route clears passport cluster assignments and deletes memberships before deleting the cluster.

- [ ] **Step 4: Local admin self-protection**

In `ClusterAdminController.SaveUser` and `DeleteUserMembership`, detect the current email:

```csharp
var currentEmail = AccessControlService.CurrentEmail(User).Trim().ToLowerInvariant();
```

If a local admin is trying to change their own membership role or remove their own membership, redirect with:

```text
Cannot change your own local admin role.
```

Global admins may still manage other users.

- [ ] **Step 5: Account self-service**

Create `AccountProfileViewModel`, `AccountController`, and `Views/Account/Index.cshtml` using the account implementation from the superseded guardrails plan. Required behavior:

- authenticated user can edit display name
- authenticated user can edit email
- authenticated user can set password
- email update changes `users.email`
- email update changes `clusterMemberships.email`
- successful update refreshes cookie principal through `AuthService.CreatePrincipalForUserAsync`

- [ ] **Step 6: Header customer name and role**

In `_Layout.cshtml`, compute:

```cshtml
var customerName = string.Empty;
var customerRole = string.Empty;
```

For authenticated users, load the stored user and render:

```cshtml
<div class="bp-identity">
    <p>@identityLabel</p>
    <p>@customerName / @customerRole</p>
</div>
```

Add an Account link:

```cshtml
<a href="/account">Account</a>
```

- [ ] **Step 7: Run guardrail tests**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AccountAndClusterGuardrails_ShouldBePresent" --no-restore
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs web/Models/ViewModels/AccountProfileViewModel.cs web/Controllers/AccountController.cs web/Views/Account/Index.cshtml web/Services/ClusterRepository.cs web/Services/AuthService.cs web/Services/AccessControlService.cs web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/Users.cshtml web/Views/Shared/_Layout.cshtml web/wwwroot/css/site.css
git commit -m "feat: add account and cluster safety guardrails"
```

---

### Task 11: Documentation And Tester Pack

**Files:**
- Modify: `docs/end-user-testing-guide.md`
- Modify: `docs/qa-test-pack.md`
- Modify: `docs/sample-cluster-test-accounts.md`
- Modify: `web/Views/Admin/Help.cshtml`
- Modify: `web/Views/Help/Index.cshtml`
- Test: `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`
- Test: `BatteryPassWeb.Tests/QaTesterPackDocumentationTests.cs`
- Test: `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`

- [ ] **Step 1: Run doc tests and verify red**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ProductTemplateSource_ShouldDocumentBatteryFamilyMapping|FullyQualifiedName~QaTesterPackDocumentationTests|FullyQualifiedName~Phase6ADocumentationTests" --no-restore
```

Expected: FAIL because docs still mention Product templates, nested software versions, software patching, and battery secrets.

- [ ] **Step 2: Update docs with required mapping and API flow**

In each doc, use these statements:

```markdown
Product Template is the internal implementation name for Battery Family. In the UI and tester workflows, Product Template and Battery Family mean the same stored template concept.
```

Document:

- Battery Family -> Battery version
- software version, released date, and latest update are parameters on Battery version
- no software update endpoint exists
- `PATCH /api/external/v1/batteries/{passportId}/battery-version`
- `POST /api/external/v1/batteries/{passportId}/validate`
- `POST /api/external/v1/batteries/{passportId}/sign`
- Sign tokens can only validate/sign
- read-write tokens can change Battery version and write operational telemetry
- telemetry does not require signing
- first publish requires Validate, Sign, Publish in the app
- subsequent successful signs auto-publish after the passport has been published once
- battery secrets are removed

- [ ] **Step 3: Update role documentation**

Document the exact roles and visibility:

```text
Normal User: published passports in assigned clusters.
Notified Body: signed or published passports in all clusters, no Trust & conformance.
Market Surveillance Authorities: signed or published passports in all clusters, Trust & conformance allowed.
Commission: draft, signed, and published passports in all clusters, Trust & conformance allowed.
Person with Legitimate Interest: signed or published passports in all clusters, no Trust & conformance.
Local Admin: same cluster-admin behavior as now.
Global Admin: same global-admin behavior as now.
```

- [ ] **Step 4: Run documentation tests**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ProductTemplateSource_ShouldDocumentBatteryFamilyMapping|FullyQualifiedName~QaTesterPackDocumentationTests|FullyQualifiedName~Phase6ADocumentationTests" --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add docs/end-user-testing-guide.md docs/qa-test-pack.md docs/sample-cluster-test-accounts.md web/Views/Admin/Help.cshtml web/Views/Help/Index.cshtml
git commit -m "docs: update battery family API tester guidance"
```

---

### Task 12: Final Verification

**Files:**
- Verify all files changed by Tasks 1-11.

- [ ] **Step 1: Run focused revised suite**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~ProductTemplateServiceTests|FullyQualifiedName~ExternalApi|FullyQualifiedName~PublicVisibilityPolicyTests|FullyQualifiedName~DocumentAccessControlTests|FullyQualifiedName~PassportPublishPolicyServiceTests" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 2: Run full test suite**

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore
```

Expected: all tests pass.

- [ ] **Step 3: Build the app**

```powershell
dotnet build web/BatteryPassWeb.csproj --no-restore
```

Expected: build succeeds.

- [ ] **Step 4: Run diff checks**

```powershell
git diff --check
git status --short
```

Expected: `git diff --check` reports no whitespace errors. `git status --short` shows only unrelated pre-existing changes or files intentionally changed by this work before final commit.

- [ ] **Step 5: Manual smoke test**

Start the app:

```powershell
dotnet run --project web/BatteryPassWeb.csproj
```

Open the local URL printed by the app and verify:

- registry rows show Summary report and Detailed report icon actions
- passport summary hero shows Passport status, not Battery status
- Battery Family, Battery version, and Battery serial number are the identity fields
- admin token management can create Read, Read + write, and Sign tokens
- battery secret UI is gone
- `PATCH /api/external/v1/batteries/{passportId}/battery-version` with a read-write token says validation/signing is required
- `POST /api/external/v1/batteries/{passportId}/validate` with a sign token validates
- `POST /api/external/v1/batteries/{passportId}/sign` signs
- a passport published once before auto-publishes on the next successful sign

- [ ] **Step 6: Final commit if verification required code/doc corrections**

If verification required fixes, commit only those fixes:

```powershell
git add <fixed files>
git commit -m "fix: complete revised battery family verification"
```

Expected: no commit is created when verification made no changes.
