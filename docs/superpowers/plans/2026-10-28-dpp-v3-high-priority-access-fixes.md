# DPP V3 High-Priority Access Fixes Implementation Plan

> **Superseded on 28 Oct 2026 — don't execute.** The product owner chose to rebuild on the v2.0 data model instead of patching the current one (decision 1 in `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-decisions.md`). These access rules move into step 3 of `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`; step 1 is `docs/superpowers/plans/2026-10-28-dpp-v3-step1-data-foundation.md`. Kept for reference only.

> **For agentic workers:** REQUIRED SUB-SKILL: Use the `execute-plan` skill (or subagent-driven execution, one fresh subagent per task) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make battery status, the other purchaser data on the detail page, and the test reports follow the access and editing rules in `DPP_Internal_2026_BatteryPass-Ready_DataAttributeLongList_v2.0_August version_V3.xlsx`, read with the product owner's role rule (purchaser = the battery's cluster).

**Architecture:** Small static rule holders (`BatteryStatusCatalog`, `PassportDocumentAccessPolicy`, the purchaser-editable set in `EditableFieldPolicyService`) plus pure role checks in `AccessControlService` (`CanSeePurchaserData`, `CanSeeLegitimateInterestData`, `CanSeeAuthorityOnlyData`). The rules are keyed on field and document keys in code, so they also hold for policies, templates and passports already saved in MongoDB — no data migration. Every place that shows or returns the data (summary, detail, edit forms, file download/upload, external API, internal JSON API) uses them.

**Tech Stack:** ASP.NET Core MVC on .NET 10, Razor views, MongoDB.Driver BSON, xUnit 2.9.

---

## Background for the engineer

### The role rule (decided by the product owner, 28 Oct 2026)

Where the V3 file says **"the person who has legally purchased the battery (or a third party on their behalf)"**, it means **the members of the cluster the battery belongs to**. **Cluster users see** that data; **cluster admins edit** it. The global "Person with Legitimate Interest" role stands for the other legitimate-interest persons (repairers, remanufacturers, recyclers).

Who may see what (global admins see everything, because they produce the data):

| Data in the V3 file | Battery's cluster (users see, admins edit) | Legitimate-interest role | Notified body | Market surveillance | Commission |
|---|---|---|---|---|---|
| Public (54 attributes) | ✓ | ✓ | ✓ | ✓ | ✓ |
| Purchaser data: status, SoC, cycles, temperature, accidents, fades, lifetime (10) | ✓ | ✗ | ✗ | ✓ | ✗ |
| Shared legitimate-interest data: composition, dismantling, part numbers, spare parts, safety (5) | ✓ (see only) | ✓ | ✗ | ✓ | ✓ |
| Authorities only: test reports, operator/manufacturer IDs, service provider (4) | ✗ | ✗ | ✓ | ✓ | ✓ |

### What this plan fixes

From `Downloads\DPP_V3_vs_App_comparison_2026-10-28_v2.xlsx`, tab "Decisions":

| Ref | V3 row | Problem today | Required behaviour | Tasks |
|---|---|---|---|---|
| D-01 | #22 Battery status | Shown on the **public** summary and to every role that opens the detail page | Battery's cluster + market surveillance only | 1, 5 |
| D-02 | #22 Battery status | Fixed to `Original`; nobody can change it | Dynamic; the battery's cluster admins (and global admins) change it; a new passport follows | 2–4 |
| D-59 | #22 Battery status | Free text | Only the regulation values | 2, 4 |
| D-03 | #29 Test reports | `conformityAssessment` document seeded **public**, linked for every logged-in user | Notified bodies, market surveillance, Commission only | 6–9 |
| D-04, D-05 | #63 SoC, #83 Full cycles | Detail page shows them to legitimate-interest, notified body and Commission users of every cluster | Battery's cluster + market surveillance only | 5 |
| D-44, D-51 | #63 SoC, #83 Full cycles | Cluster admins can't edit them by default | Always editable by the battery's cluster admins | 3 |
| D-34 | #43 Materials | Notified bodies see the material composition tab | Battery's cluster, legitimate-interest role, Commission, market surveillance | 5 |

### Decisions this plan encodes (change them here before executing if needed)

1. Status values are the BatteryPass v1.2 schema enum the app validates against: `Original`, `Repurposed`, `Reused`, `Remanufactured`, `Waste`. `re-used` is accepted as input and stored as `Reused`.
2. Battery status, state of charge and full cycles are **always editable by the battery's cluster admins** (and by global admins), whatever is saved in the Editable Fields policy. A status change is passport-affecting, so the existing check flags "new passport needed"; cluster admins can already create, validate, sign and publish passports for their clusters. SoC and full cycles stay operational values: changing them never needs a new passport.
   Seeing purchaser data depends on **membership** of the battery's cluster, whatever the user's role (a notified-body user who is also a member of that cluster sees it). Market surveillance also sees it, because every purchaser row in the V3 file lists market surveillance. A battery with no cluster shows its purchaser data only to global admins and market surveillance.
3. On the detail page, the purchaser section also covers values that are **not** in the V3 file but describe the purchaser's battery and operations: remaining capacity, remaining energy, telemetry, active status, location of use and contact person (workbook rows X-10, X-11, X-21–X-24). They are hidden from everyone outside the battery's cluster until you decide whether to remove them.
4. "Authorities" = roles `notifiedBody`, `marketSurveillanceAuthority`, `commission`, plus global `admin` (the data owner who uploads the report).
5. The test-report rule is keyed on the document key `conformityAssessment`, not on the stored `visibility` value, so old passports saved as `public` are covered without migrating data.
6. `GET /api/passports` and `GET /api/passports/{id}` only require *any* login today, so every logged-in user can read the full JSON of every passport. Task 9 restricts both to global admins, like the write endpoints in the same controller. Nothing in the UI calls them.

**Not in this plan (decide in the workbook first):** D-09 cluster admins' default rights over manufacturer data (model, facility, software version, cluster); D-35 whether cluster admins edit shared legitimate-interest data; D-43 who re-states fades and expected lifetime; DPP status values and marking the old passport `inactive` (#2); the other Medium/Low rows; an authorities-only view for #7, #12, #13; removing placeholder data; the v2.0 data model.

### Repository facts

- Work in the worktree `C:\Users\KassemAlHarakeh\source\repos\BatteryPassDataModel\.worktrees\cloned-kassem-rewrite`, branch `security/demo-api-hardening` (baseline commit `8c8bfe7`). Create a feature branch first: `git switch -c feature/dpp-v3-high-priority-access`.
- App: `web\` (ASP.NET Core MVC). Tests: `BatteryPassWeb.Tests\` (xUnit).
- Baseline: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` → **464 passed, 0 failed**.
- House test style: real unit tests for static/pure code, plus "source assertion" tests that read a `.cs`/`.cshtml` file and `Assert.Contains` on its text. Every test class carries its own private `RepoFile(...)` helper (copy it as shown).
- Many files use CRLF line endings. Keep source assertions on single-line snippets; use `\s*` in regexes that span lines.
- Cluster admins edit batteries through `ClusterAdminController.SavePassport`, which only writes fields whose Editable Fields "Edit" toggle (`EditableByLocalAdmin`) is on and that appear in its own `BatteryFormFieldsByEditableKey` map.
- Run one test class: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~<ClassName>"`.

## File structure

| File | Action | Responsibility |
|---|---|---|
| `web/Services/BatteryStatusCatalog.cs` | Create | Allowed battery status values and input normalisation |
| `web/Services/PassportDocumentAccessPolicy.cs` | Create | Which documents are authorities-only; strips them from data leaving the app |
| `web/Services/AccessControlService.cs` | Modify | `CanSeePurchaserData`, `CanSeeLegitimateInterestData`, `CanSeeAuthorityOnlyData` |
| `web/Services/EditableFieldPolicyService.cs` | Modify | Purchaser data always editable by the battery's cluster admins |
| `web/Views/Passport/Summary.cshtml` | Modify | Remove battery status row |
| `web/Views/Admin/Clusters.cshtml` | Modify | Locked toggles in the Editable fields tab |
| `web/Views/Admin/EditPassport.cshtml` | Modify | Status dropdown; test-report row hidden or fixed |
| `web/Controllers/AdminController.cs` | Modify | Status validation/normalisation; fixed test-report visibility on save |
| `web/Controllers/ClusterAdminController.cs` | Modify | Cluster admins can save battery status |
| `web/Models/ViewModels/PassportDetailViewModel.cs`, `web/Controllers/PassportController.cs`, `web/Views/Passport/Detail.cshtml` | Modify | Detail page sections per audience |
| `web/Controllers/FilesApiController.cs` | Modify | Download and upload guards for the test report |
| `web/Services/ProductTemplateModels.cs`, `web/Views/Admin/Product.cshtml` | Modify | New templates seed the report as authorities-only; family editor label |
| `web/Controllers/ExternalApiController.cs` | Modify | Read endpoints drop the test report |
| `web/Controllers/PassportsApiController.cs` | Modify | JSON reads admin-only |
| `docs/end-user-testing-guide.md` | Modify | Two lines describing the new behaviour |
| `BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs` | Create | Tests for battery status and cluster-admin editing |
| `BatteryPassWeb.Tests/PurchaserDataAccessTests.cs` | Create | Tests for the detail-page audiences |
| `BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs` | Create | Tests for the test report |

---

### Task 1: Remove battery status from the public summary (D-01)

**Files:**
- Modify: `web/Views/Passport/Summary.cshtml:76`
- Create (test): `BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs`:

```csharp
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class BatteryStatusComplianceTests
{
    [Fact]
    public void PublicSummary_ShouldNotShowBatteryStatus()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

        // V3 row #22: battery status is purchaser data (the battery's cluster) + market surveillance only.
        Assert.DoesNotContain("passport.BatteryStatus", summary);
        Assert.DoesNotContain("Battery status", summary);
        Assert.Contains("<dt>Battery status</dt><dd>@passport.BatteryStatus</dd>", detail);
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

- [ ] **Step 2: Run the test and see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryStatusComplianceTests"`
Expected: FAIL — `Assert.DoesNotContain() Failure` (the summary still contains `passport.BatteryStatus`).

- [ ] **Step 3: Remove the row**

In `web/Views/Passport/Summary.cshtml`, delete this line (line 76) and nothing else:

```cshtml
                    <div><dt>Battery status</dt><dd>@passport.BatteryStatus</dd></div>
```

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryStatusComplianceTests|FullyQualifiedName~BatteryFamilyAccessApiRevisedTests"`
Expected: PASS. (`BatteryFamilyAccessApiRevisedTests` reads several views together and still finds "Battery status" in `Detail.cshtml`.)

- [ ] **Step 5: Commit**

```bash
git add web/Views/Passport/Summary.cshtml BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs
git commit -m "fix: hide battery status from the public summary (DPP V3 #22)"
```

---

### Task 2: Battery status value list (D-02, D-59)

**Files:**
- Create: `web/Services/BatteryStatusCatalog.cs`
- Modify (test): `BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `BatteryStatusComplianceTests` (above `RepoFile`):

```csharp
    [Fact]
    public void BatteryStatusCatalog_ShouldOnlyAcceptRegulationValues()
    {
        Assert.Equal(new[] { "Original", "Repurposed", "Reused", "Remanufactured", "Waste" }, BatteryStatusCatalog.AllowedValues);
        Assert.Equal("Original", BatteryStatusCatalog.Normalize("original"));
        Assert.Equal("Repurposed", BatteryStatusCatalog.Normalize(" REPURPOSED "));
        Assert.Equal("Reused", BatteryStatusCatalog.Normalize("re-used"));
        Assert.Equal("Waste", BatteryStatusCatalog.Normalize("Waste"));
        Assert.Null(BatteryStatusCatalog.Normalize("Repaired"));
        Assert.Null(BatteryStatusCatalog.Normalize("Updated"));
        Assert.Null(BatteryStatusCatalog.Normalize(string.Empty));
        Assert.Null(BatteryStatusCatalog.Normalize(null));
    }
```

- [ ] **Step 2: Run the test and see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryStatusComplianceTests"`
Expected: build FAIL — `The name 'BatteryStatusCatalog' does not exist in the current context`.

- [ ] **Step 3: Create the catalog**

Create `web/Services/BatteryStatusCatalog.cs`:

```csharp
namespace BatteryPassWeb.Services;

// Battery status values from the Battery Regulation (Annex XIII), spelled exactly as in the
// BatteryPass v1.2 schema enum that passports are validated against.
public static class BatteryStatusCatalog
{
    public const string Original = "Original";

    public const string InvalidValueMessage =
        "Battery status must be Original, Repurposed, Reused, Remanufactured or Waste.";

    public static IReadOnlyList<string> AllowedValues { get; } =
        ["Original", "Repurposed", "Reused", "Remanufactured", "Waste"];

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["re-used"] = "Reused"
        };

    // Returns the schema spelling, or null when the value is not an allowed status.
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return null;
        }

        var match = AllowedValues.FirstOrDefault(allowed => allowed.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            return match;
        }

        return Aliases.TryGetValue(trimmed, out var alias) ? alias : null;
    }
}
```

- [ ] **Step 4: Run the test and see it pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryStatusComplianceTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add web/Services/BatteryStatusCatalog.cs BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs
git commit -m "feat: add battery status value list from the Battery Regulation"
```

---

### Task 3: Purchaser data always editable by the battery's cluster admins (D-02, D-44, D-51)

**Files:**
- Modify: `web/Services/EditableFieldPolicyService.cs` (after the `DefaultEditableAfterCreationFieldKeys` array; `EnforceDependencies` at the end of the class)
- Modify: `web/Views/Admin/Clusters.cshtml:716-745`
- Modify (test): `BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `BatteryStatusComplianceTests` (above `RepoFile`):

```csharp
    [Theory]
    [InlineData("general.batteryStatus")]
    [InlineData("performance.stateOfCharge")]
    [InlineData("performance.fullCycles")]
    public void PurchaserData_ShouldAlwaysBeEditableByTheBatterysClusterAdmins(string fieldKey)
    {
        Assert.True(EditableFieldPolicyService.IsPurchaserEditableField(fieldKey));

        var defaults = EditableFieldPolicyService.CreateDefaultPolicy();
        Assert.True(defaults.IsEditableByLocalAdmin(fieldKey));
        Assert.True(defaults.IsEditableAfterCreation(fieldKey));
        Assert.True(defaults.IsVisibleToClusterAdmin(fieldKey));

        var requested = EditableFieldPolicyService.CreateDefaultPolicy(
            new[] { new EditableFieldPermission(fieldKey, EditableAtCreation: false, EditableAfterCreation: false, EditableByLocalAdmin: false) });
        Assert.True(requested.IsEditableByLocalAdmin(fieldKey));

        // A policy saved in MongoDB before this change must not switch the rule off.
        var stored = EditableFieldPolicyService.FromBsonDocument(new BsonDocument
        {
            ["policyKey"] = EditableFieldPolicyService.PolicyKey,
            ["permissions"] = new BsonArray
            {
                new BsonDocument
                {
                    ["fieldKey"] = fieldKey,
                    ["editableAtCreation"] = false,
                    ["editableAfterCreation"] = false,
                    ["editableByLocalAdmin"] = false,
                    ["visibleToClusterAdmin"] = false
                }
            }
        });
        Assert.True(stored.IsEditableByLocalAdmin(fieldKey));
        Assert.True(stored.IsEditableAfterCreation(fieldKey));
    }

    [Fact]
    public void ManufacturerData_ShouldNotBeForcedOpenForClusterAdmins()
    {
        Assert.False(EditableFieldPolicyService.IsPurchaserEditableField("general.batteryMass"));
        Assert.False(EditableFieldPolicyService.IsPurchaserEditableField("general.facilityId"));
        Assert.False(EditableFieldPolicyService.IsPurchaserEditableField("general.batteryModel"));
    }

    [Fact]
    public void BatteryStatusChange_ShouldMarkNewPassportNeeded_ButStateOfChargeShouldNot()
    {
        var policy = EditableFieldPolicyService.CreateDefaultPolicy();
        var latestPassport = WithStatus("Original");

        Assert.True(BatteryPassportDeltaService.HasEditableDifferences(WithStatus("Repurposed"), latestPassport, policy));
        Assert.False(BatteryPassportDeltaService.HasEditableDifferences(WithStatus("Original"), latestPassport, policy));

        var stateOfChargeChanged = WithStatus("Original");
        stateOfChargeChanged["aspects"]["performanceAndDurability"] = new BsonDocument
        {
            ["payload"] = new BsonDocument
            {
                ["batteryCondition"] = new BsonDocument
                {
                    ["stateOfCharge"] = new BsonDocument { ["stateOfChargeValue"] = 40 }
                }
            }
        };
        Assert.False(BatteryPassportDeltaService.HasEditableDifferences(stateOfChargeChanged, latestPassport, policy));
    }

    [Fact]
    public void EditableFieldsAdmin_ShouldShowPurchaserTogglesAsLocked()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("EditableFieldPolicyService.IsPurchaserEditableField(field.FieldKey)", markup);
        Assert.Contains("checked=\"@(isEditableAtCreation || isPurchaserEditable)\" disabled=\"@isPurchaserEditable\"", markup);
        Assert.Contains("checked=\"@(isEditableAfterCreation || isPurchaserEditable)\" disabled=\"@isPurchaserEditable\"", markup);
        Assert.Contains("checked=\"@(isVisibleToClusterAdmin || isPurchaserEditable)\" disabled=\"@isPurchaserEditable\"", markup);
        Assert.Contains("checked=\"@(isEditableByLocalAdmin || isPurchaserEditable)\" disabled=\"@isPurchaserEditable\"", markup);
        Assert.Contains("Purchaser data in the DPP attribute list", markup);
    }

    private static BsonDocument WithStatus(string status) => new()
    {
        ["aspects"] = new BsonDocument
        {
            ["generalProductInformation"] = new BsonDocument
            {
                ["payload"] = new BsonDocument { ["batteryStatus"] = status }
            }
        }
    };
```

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryStatusComplianceTests"`
Expected: build FAIL — `'EditableFieldPolicyService' does not contain a definition for 'IsPurchaserEditableField'`.

- [ ] **Step 3: Lock purchaser data open for cluster admins**

In `web/Services/EditableFieldPolicyService.cs`, find the end of `DefaultEditableAfterCreationFieldKeys` (it is directly followed by `FieldPathsByKey`):

```csharp
        "general.softwareVersion",
        "software.version"
    ];

    public static readonly IReadOnlyDictionary<string, string[]> FieldPathsByKey =
```

Replace it with:

```csharp
        "general.softwareVersion",
        "software.version"
    ];

    // DPP V3 purchaser data. Product-owner rule: the battery's cluster users see it, its cluster admins edit it.
    // These stay editable by cluster admins (and so after creation) whatever is saved in the policy.
    private static readonly HashSet<string> PurchaserEditableFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "general.batteryStatus",
        "performance.stateOfCharge",
        "performance.fullCycles"
    };

    public static bool IsPurchaserEditableField(string fieldKey) =>
        PurchaserEditableFieldKeys.Contains(fieldKey);

    public static readonly IReadOnlyDictionary<string, string[]> FieldPathsByKey =
```

Then replace the whole `EnforceDependencies` method at the end of the class with:

```csharp
    private static EditableFieldPermission EnforceDependencies(EditableFieldPermission permission)
    {
        var editableByLocalAdmin = permission.EditableByLocalAdmin || IsPurchaserEditableField(permission.FieldKey);
        var editableAtCreation = permission.EditableAtCreation
            || permission.EditableAfterCreation
            || editableByLocalAdmin;
        var editableAfterCreation = permission.EditableAfterCreation || editableByLocalAdmin;
        var visibleToClusterAdmin = permission.VisibleToClusterAdmin || editableByLocalAdmin;
        return permission with
        {
            EditableAtCreation = editableAtCreation,
            EditableAfterCreation = editableAfterCreation,
            EditableByLocalAdmin = editableByLocalAdmin,
            VisibleToClusterAdmin = visibleToClusterAdmin
        };
    }
```

`EnforceDependencies` already runs for defaults, requested permissions and policies loaded from MongoDB (`FromBsonDocument` → `CreateDefaultPolicy`). Because battery status is now editable after creation, the existing "new passport needed" comparison includes it. SoC and full cycles are skipped by that comparison (`BatteryPassportDeltaService.OperationalFieldKeys`), so they still never need a new passport.

- [ ] **Step 4: Show the locks in the Editable fields tab**

In `web/Views/Admin/Clusters.cshtml`, after this line (line 719):

```cshtml
                                                    var isEditableByLocalAdmin = editableByLocalAdminKeys.Contains(field.FieldKey);
```

add:

```cshtml
                                                    var isPurchaserEditable = BatteryPassWeb.Services.EditableFieldPolicyService.IsPurchaserEditableField(field.FieldKey);
```

Replace:

```cshtml
                                                            <strong>@field.Label</strong>
                                                            <span>@field.Guidance</span>
                                                            <code>@field.FieldKey</code>
```

with:

```cshtml
                                                            <strong>@field.Label</strong>
                                                            <span>@field.Guidance</span>
                                                            <code>@field.FieldKey</code>
                                                            @if (isPurchaserEditable)
                                                            {
                                                                <span>Purchaser data in the DPP attribute list: the battery's cluster admins can always edit it.</span>
                                                            }
```

Lock all four toggles for these fields: the page script unticks "After", "Show" and "Edit" whenever "Creation" is unticked, so leaving any toggle enabled would make a locked row look switched off until the page reloads.

Replace the "Creation" checkbox line (line 729):

```cshtml
                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="editableAtCreationFieldKeys" value="@field.FieldKey" checked="@isEditableAtCreation" aria-label="@field.Label editable at creation" data-editable-policy-creation />
```

with:

```cshtml
                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="editableAtCreationFieldKeys" value="@field.FieldKey" checked="@(isEditableAtCreation || isPurchaserEditable)" disabled="@isPurchaserEditable" aria-label="@field.Label editable at creation" data-editable-policy-creation />
```

Replace the "After" checkbox line (line 734):

```cshtml
                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="editableAfterCreationFieldKeys" value="@field.FieldKey" checked="@isEditableAfterCreation" aria-label="@field.Label editable after creation" data-editable-policy-after />
```

with:

```cshtml
                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="editableAfterCreationFieldKeys" value="@field.FieldKey" checked="@(isEditableAfterCreation || isPurchaserEditable)" disabled="@isPurchaserEditable" aria-label="@field.Label editable after creation" data-editable-policy-after />
```

Replace the "Show" checkbox line (line 739):

```cshtml
                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="visibleToClusterAdminFieldKeys" value="@field.FieldKey" checked="@isVisibleToClusterAdmin" aria-label="@field.Label Show for Cluster Admin" title="Show for Cluster Admin" data-editable-policy-visible />
```

with:

```cshtml
                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="visibleToClusterAdminFieldKeys" value="@field.FieldKey" checked="@(isVisibleToClusterAdmin || isPurchaserEditable)" disabled="@isPurchaserEditable" aria-label="@field.Label Show for Cluster Admin" title="Show for Cluster Admin" data-editable-policy-visible />
```

Replace the "Edit" checkbox line (line 744):

```cshtml
                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="editableByLocalAdminFieldKeys" value="@field.FieldKey" checked="@isEditableByLocalAdmin" aria-label="@field.Label editable by cluster admins" data-editable-policy-local />
```

with:

```cshtml
                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="editableByLocalAdminFieldKeys" value="@field.FieldKey" checked="@(isEditableByLocalAdmin || isPurchaserEditable)" disabled="@isPurchaserEditable" aria-label="@field.Label editable by cluster admins" data-editable-policy-local />
```

Disabled checkboxes are not posted; that is fine because the service forces the values on every load.

- [ ] **Step 5: Run the tests and see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryStatusComplianceTests|FullyQualifiedName~LocalAdminEditableFieldPolicyTests|FullyQualifiedName~AdminFeedbackImplementationTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add web/Services/EditableFieldPolicyService.cs web/Views/Admin/Clusters.cshtml BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs
git commit -m "feat: let the battery's cluster admins always edit purchaser data (DPP V3)"
```

---

### Task 4: Status dropdown and saving for admins and cluster admins (D-02, D-59)

**Files:**
- Modify: `web/Views/Admin/EditPassport.cshtml:245`
- Modify: `web/Controllers/AdminController.cs` (`SaveBattery` after line 423; `ApplyPassportForm` line 3042)
- Modify: `web/Controllers/ClusterAdminController.cs` (`BatteryFormFieldsByEditableKey` line 22; `SavePassport` after line 454; `ApplyLocalBatteryForm` before line 1300)
- Modify (test): `BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `BatteryStatusComplianceTests` (above `RepoFile`):

```csharp
    [Fact]
    public void BatteryEditForm_ShouldOfferOnlyAllowedStatusValues()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("<select name=\"batteryStatus\">", markup);
        Assert.Contains("BatteryStatusCatalog.AllowedValues", markup);
        Assert.DoesNotContain("<input type=\"text\" name=\"batteryStatus\"", markup);
        Assert.Contains("BatteryStatusCatalog.InvalidValueMessage", controller);
        Assert.Contains("generalPayload[\"batteryStatus\"] = BatteryStatusCatalog.Normalize(", controller);
    }

    [Fact]
    public void ClusterAdminSave_ShouldLetTheBatterysClusterAdminsChangeStatus()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));

        Assert.Contains("[\"general.batteryStatus\"] = [\"batteryStatus\"]", controller);
        Assert.Contains("editableFieldKeys.Contains(\"general.batteryStatus\")", controller);
        Assert.Contains("BatteryStatusCatalog.InvalidValueMessage", controller);
        Assert.Contains("generalPayload[\"batteryStatus\"] = BatteryStatusCatalog.Normalize(", controller);
    }
```

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryStatusComplianceTests"`
Expected: 2 FAIL — `Assert.Contains() Failure` on `<select name="batteryStatus">` and on `["general.batteryStatus"] = ["batteryStatus"]`.

- [ ] **Step 3: Replace the free-text input with a dropdown**

The same view serves global admins and cluster admins (cluster-edit mode). In `web/Views/Admin/EditPassport.cshtml`, replace line 245:

```cshtml
                    <label class="bp-field-shell" data-field-key="general.batteryStatus"><span>Battery status <em class="bp-field-marker">@RequirementLabel("general.batteryStatus")</em></span><input type="text" name="batteryStatus" value="@passport.BatteryStatus" /><small class="bp-field-help">@DirtySignedDataHelp("general.batteryStatus", "Lifecycle/original status for the battery.")</small></label>
```

with:

```cshtml
                    @{
                        var selectedBatteryStatus = BatteryPassWeb.Services.BatteryStatusCatalog.Normalize(passport.BatteryStatus) ?? BatteryPassWeb.Services.BatteryStatusCatalog.Original;
                    }
                    <label class="bp-field-shell" data-field-key="general.batteryStatus"><span>Battery status <em class="bp-field-marker">@RequirementLabel("general.batteryStatus")</em></span><select name="batteryStatus">@foreach (var statusOption in BatteryPassWeb.Services.BatteryStatusCatalog.AllowedValues) { if (statusOption == selectedBatteryStatus) { <option value="@statusOption" selected>@statusOption</option> } else { <option value="@statusOption">@statusOption</option> } }</select><small class="bp-field-help">@DirtySignedDataHelp("general.batteryStatus", "Original, Repurposed, Reused (incl. repaired), Remanufactured or Waste. A change needs a new passport.")</small></label>
```

- [ ] **Step 4: Global admin save — reject unknown values**

In `web/Controllers/AdminController.cs`, in `SaveBattery`, after this line (the only `forCreation: false` call in the file, line 423):

```csharp
        var filteredForm = ApplyBatteryEditableFormValues(Request.Form, editablePolicy, forCreation: false);
```

add:

```csharp
        var postedBatteryStatus = Text(filteredForm, "batteryStatus");
        if (!string.IsNullOrWhiteSpace(postedBatteryStatus) && BatteryStatusCatalog.Normalize(postedBatteryStatus) == null)
        {
            return Redirect($"/admin/batteries/{Uri.EscapeDataString(decodedBatteryId)}/edit?error={Uri.EscapeDataString(BatteryStatusCatalog.InvalidValueMessage)}");
        }
```

- [ ] **Step 5: Global admin save — store only allowed values**

In `ApplyPassportForm` (used by battery create, battery save and passport save), replace line 3042:

```csharp
        generalPayload["batteryStatus"] = Text(form, "batteryStatus", generalPayload.GetValue("batteryStatus", "Original").ToString());
```

with:

```csharp
        var existingBatteryStatus = generalPayload.GetValue("batteryStatus", BatteryStatusCatalog.Original).ToString();
        generalPayload["batteryStatus"] = BatteryStatusCatalog.Normalize(Text(form, "batteryStatus", existingBatteryStatus))
            ?? BatteryStatusCatalog.Normalize(existingBatteryStatus)
            ?? BatteryStatusCatalog.Original;
```

- [ ] **Step 6: Cluster admin save — accept and store the status**

In `web/Controllers/ClusterAdminController.cs`, in `BatteryFormFieldsByEditableKey`, replace:

```csharp
            ["general.facilityId"] = ["facilityId"],
```

with:

```csharp
            ["general.facilityId"] = ["facilityId"],
            ["general.batteryStatus"] = ["batteryStatus"],
```

In `SavePassport`, after:

```csharp
        var filteredForm = ApplyLocalBatteryEditableFormValues(Request.Form, editablePolicy);
```

add:

```csharp
        var postedBatteryStatus = Text(filteredForm, "batteryStatus");
        if (!string.IsNullOrWhiteSpace(postedBatteryStatus) && BatteryStatusCatalog.Normalize(postedBatteryStatus) == null)
        {
            return Redirect(ClusterPassportEditRedirect(passportId, error: BatteryStatusCatalog.InvalidValueMessage));
        }
```

In `ApplyLocalBatteryForm`, replace:

```csharp
        if (editableFieldKeys.Contains("general.batteryImageUrl"))
```

with:

```csharp
        if (editableFieldKeys.Contains("general.batteryStatus"))
        {
            var existingBatteryStatus = generalPayload.GetValue("batteryStatus", BatteryStatusCatalog.Original).ToString();
            generalPayload["batteryStatus"] = BatteryStatusCatalog.Normalize(Text(form, "batteryStatus", existingBatteryStatus))
                ?? BatteryStatusCatalog.Normalize(existingBatteryStatus)
                ?? BatteryStatusCatalog.Original;
        }

        if (editableFieldKeys.Contains("general.batteryImageUrl"))
```

`SavePassport` already runs `UpdateNewPassportRequiredAsync` after saving, so a status change by a cluster admin flags "new passport needed" for that battery.

- [ ] **Step 7: Run the tests and see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryStatusComplianceTests|FullyQualifiedName~BatteryAdminWorkflowTests|FullyQualifiedName~AdminEditGuidanceTests|FullyQualifiedName~ApplicationAuditCoverageTests"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add web/Views/Admin/EditPassport.cshtml web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs BatteryPassWeb.Tests/BatteryStatusComplianceTests.cs
git commit -m "feat: battery status dropdown for admins and the battery's cluster admins (DPP V3 #22)"
```

---

### Task 5: Detail page sections per audience (D-01, D-04, D-05, D-34)

**Files:**
- Modify: `web/Services/AccessControlService.cs` (after `HasAllClusterReadScope`)
- Modify: `web/Models/ViewModels/PassportDetailViewModel.cs`
- Modify: `web/Controllers/PassportController.cs` (`Detail`, lines 209–235)
- Modify: `web/Views/Passport/Detail.cshtml` (status rows; Material composition tab; Performance metrics and operational block)
- Create (test): `BatteryPassWeb.Tests/PurchaserDataAccessTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `BatteryPassWeb.Tests/PurchaserDataAccessTests.cs`:

```csharp
using System.Security.Claims;
using System.Text.RegularExpressions;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class PurchaserDataAccessTests
{
    private const string BatteryCluster = "cluster-north-operations";

    [Theory]
    [InlineData("member", true, true)]
    [InlineData("clusterAdmin", true, true)]
    [InlineData("member", false, false)]
    [InlineData("clusterAdmin", false, false)]
    [InlineData("legitimateInterest", false, false)]
    [InlineData("legitimateInterest", true, true)]
    [InlineData("notifiedBody", false, false)]
    [InlineData("notifiedBody", true, true)]
    [InlineData("commission", false, false)]
    [InlineData("marketSurveillanceAuthority", false, true)]
    [InlineData("admin", false, true)]
    public void PurchaserData_ShouldBeVisibleToTheBatterysClusterAndMarketSurveillanceOnly(string role, bool inBatteryCluster, bool expected)
    {
        Assert.Equal(expected, AccessControlService.CanSeePurchaserData(UserWithRole(role), BatteryCluster, ClustersOf(inBatteryCluster)));
    }

    [Theory]
    [InlineData("member", true, true)]
    [InlineData("member", false, false)]
    [InlineData("legitimateInterest", false, true)]
    [InlineData("commission", false, true)]
    [InlineData("marketSurveillanceAuthority", false, true)]
    [InlineData("notifiedBody", false, false)]
    [InlineData("admin", false, true)]
    public void SharedLegitimateInterestData_ShouldExcludeNotifiedBodiesAndOtherClusters(string role, bool inBatteryCluster, bool expected)
    {
        Assert.Equal(expected, AccessControlService.CanSeeLegitimateInterestData(UserWithRole(role), BatteryCluster, ClustersOf(inBatteryCluster)));
    }

    [Fact]
    public void DetailPage_ShouldShowEachSectionOnlyToItsAudience()
    {
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var detailModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportDetailViewModel.cs"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));

        Assert.Contains("public bool CanViewPurchaserData { get; init; }", detailModel);
        Assert.Contains("public bool CanViewLegitimateInterestData { get; init; }", detailModel);
        Assert.Contains("AccessControlService.CanSeePurchaserData(User, passport.ClusterId, userClusterIds)", controller);
        Assert.Contains("AccessControlService.CanSeeLegitimateInterestData(User, passport.ClusterId, userClusterIds)", controller);
        Assert.Contains("!canViewPurchaserData || string.IsNullOrWhiteSpace(telemetryBatteryId)", controller);
        Assert.Equal(2, Regex.Matches(detail, @"@if \(Model\.CanViewPurchaserData\)\s*\{\s*<div><dt>Battery status</dt>").Count);
        Assert.Matches(new Regex(@"@if \(Model\.CanViewPurchaserData\)\s*\{\s*<div class=""bp-metric""><p>State of charge</p>"), detail);
        Assert.Contains("@if (Model.CanViewPurchaserData && passport.IsHistoricalPassport)", detail);
        Assert.Contains("else if (Model.CanViewPurchaserData)", detail);
        Assert.Equal(2, Regex.Matches(detail, @"@if \(Model\.CanViewLegitimateInterestData\)").Count);
    }

    private static IReadOnlyList<string> ClustersOf(bool inBatteryCluster) =>
        inBatteryCluster ? new[] { BatteryCluster } : new[] { "cluster-south-operations" };

    private static ClaimsPrincipal UserWithRole(string role) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tester@example.test"), new Claim(ClaimTypes.Role, role) }, "test"));

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

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PurchaserDataAccessTests"`
Expected: build FAIL — `'AccessControlService' does not contain a definition for 'CanSeePurchaserData'`.

- [ ] **Step 3: Add the audience checks**

In `web/Services/AccessControlService.cs`, after:

```csharp
    public static bool HasAllClusterReadScope(ClaimsPrincipal user) =>
        IsAdmin(user) || HasGlobalReportReadRole(user);
```

add:

```csharp
    // DPP V3 + product-owner rule: "the person who has legally purchased the battery" = members of the battery's cluster.
    // Purchaser data (status, SoC, cycles, temperature, accidents, fades, lifetime): the battery's cluster + market surveillance.
    public static bool CanSeePurchaserData(ClaimsPrincipal user, string batteryClusterId, IEnumerable<string> userClusterIds) =>
        IsAdmin(user)
        || user.IsInRole(RoleMarketSurveillanceAuthority)
        || IsMemberOfCluster(batteryClusterId, userClusterIds);

    // Shared legitimate-interest data (composition, dismantling, part numbers, spare parts, safety):
    // the battery's cluster + legitimate-interest role + Commission + market surveillance. Not notified bodies.
    public static bool CanSeeLegitimateInterestData(ClaimsPrincipal user, string batteryClusterId, IEnumerable<string> userClusterIds) =>
        CanSeePurchaserData(user, batteryClusterId, userClusterIds)
        || user.IsInRole(RoleLegitimateInterest)
        || user.IsInRole(RoleCommission);

    private static bool IsMemberOfCluster(string batteryClusterId, IEnumerable<string> userClusterIds) =>
        !string.IsNullOrWhiteSpace(batteryClusterId)
        && userClusterIds.Contains(batteryClusterId, StringComparer.OrdinalIgnoreCase);
```

- [ ] **Step 4: Pass the flags to the detail view**

In `web/Models/ViewModels/PassportDetailViewModel.cs`, after `public bool CanViewTrustConformance { get; init; }` add:

```csharp
    public bool CanViewPurchaserData { get; init; }
    public bool CanViewLegitimateInterestData { get; init; }
```

In `web/Controllers/PassportController.cs`, `Detail`, after:

```csharp
        var canViewTrustConformance = await _accessControlService.CanViewTrustConformanceAsync(User, passport.ClusterId, cancellationToken);
```

add:

```csharp
        var userClusterIds = await _accessControlService.GetClusterIdsForUserAsync(User, cancellationToken);
        var canViewPurchaserData = AccessControlService.CanSeePurchaserData(User, passport.ClusterId, userClusterIds);
        var canViewLegitimateInterestData = AccessControlService.CanSeeLegitimateInterestData(User, passport.ClusterId, userClusterIds);
```

Replace:

```csharp
        IReadOnlyList<BsonDocument> telemetryHistory = string.IsNullOrWhiteSpace(telemetryBatteryId) || passport.IsHistoricalPassport
```

with:

```csharp
        IReadOnlyList<BsonDocument> telemetryHistory = !canViewPurchaserData || string.IsNullOrWhiteSpace(telemetryBatteryId) || passport.IsHistoricalPassport
```

(The telemetry rows are embedded in the page script, so they must not be loaded at all for other audiences.)

Replace (line 226 — the occurrence **with** a trailing comma):

```csharp
            CanViewTrustConformance = canViewTrustConformance,
```

with:

```csharp
            CanViewTrustConformance = canViewTrustConformance,
            CanViewPurchaserData = canViewPurchaserData,
            CanViewLegitimateInterestData = canViewLegitimateInterestData,
```

- [ ] **Step 5: Wrap the sections in the detail view**

In `web/Views/Passport/Detail.cshtml`:

a) Replace **both** occurrences (hero header and General tab — use replace-all) of:

```cshtml
                    <div><dt>Battery status</dt><dd>@passport.BatteryStatus</dd></div>
```

with:

```cshtml
                    @if (Model.CanViewPurchaserData)
                    {
                        <div><dt>Battery status</dt><dd>@passport.BatteryStatus</dd></div>
                    }
```

b) Replace the Material composition tab button:

```cshtml
            <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-material" type="button" role="tab">Material composition</button></li>
```

with:

```cshtml
            @if (Model.CanViewLegitimateInterestData)
            {
                <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-material" type="button" role="tab">Material composition</button></li>
            }
```

c) Replace the opening of the Material composition pane:

```cshtml
            <div class="tab-pane fade" id="tab-material" role="tabpanel">
```

with:

```cshtml
            @if (Model.CanViewLegitimateInterestData)
            {
            <div class="tab-pane fade" id="tab-material" role="tabpanel">
```

and close it — replace:

```cshtml
            </div>

            <div class="tab-pane fade" id="tab-performance" role="tabpanel">
```

with:

```cshtml
            </div>
            }

            <div class="tab-pane fade" id="tab-performance" role="tabpanel">
```

d) Replace the four purchaser metrics:

```cshtml
                    <div class="bp-metric"><p>State of charge</p><strong>@passport.Performance.StateOfCharge.ToString("0.##") %</strong></div>
                    <div class="bp-metric"><p>Remaining capacity</p><strong>@passport.Performance.RemainingCapacity.ToString("0.##") %</strong></div>
                    <div class="bp-metric"><p>Remaining energy</p><strong>@passport.Performance.RemainingEnergy.ToString("0.##") kWh</strong></div>
                    <div class="bp-metric"><p>Full cycles</p><strong>@passport.Performance.Cycles.ToString("0.##")</strong></div>
```

with:

```cshtml
                    @if (Model.CanViewPurchaserData)
                    {
                        <div class="bp-metric"><p>State of charge</p><strong>@passport.Performance.StateOfCharge.ToString("0.##") %</strong></div>
                        <div class="bp-metric"><p>Remaining capacity</p><strong>@passport.Performance.RemainingCapacity.ToString("0.##") %</strong></div>
                        <div class="bp-metric"><p>Remaining energy</p><strong>@passport.Performance.RemainingEnergy.ToString("0.##") kWh</strong></div>
                        <div class="bp-metric"><p>Full cycles</p><strong>@passport.Performance.Cycles.ToString("0.##")</strong></div>
                    }
```

e) Hide the operational block (telemetry, location of use, contact person). Replace:

```cshtml
                @if (passport.IsHistoricalPassport)
                {
                    <div class="bp-warning mt-4">
```

with:

```cshtml
                @if (Model.CanViewPurchaserData && passport.IsHistoricalPassport)
                {
                    <div class="bp-warning mt-4">
```

and replace:

```cshtml
                }
                else
                {
                    <h3 class="bp-subheading mt-4">Operational status</h3>
```

with:

```cshtml
                }
                else if (Model.CanViewPurchaserData)
                {
                    <h3 class="bp-subheading mt-4">Operational status</h3>
```

- [ ] **Step 6: Run the tests and see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PurchaserDataAccessTests|FullyQualifiedName~BatteryStatusComplianceTests|FullyQualifiedName~Passport|FullyQualifiedName~BatteryFamilyAccessApiRevisedTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add web/Services/AccessControlService.cs web/Models/ViewModels/PassportDetailViewModel.cs web/Controllers/PassportController.cs web/Views/Passport/Detail.cshtml BatteryPassWeb.Tests/PurchaserDataAccessTests.cs
git commit -m "fix: show purchaser and shared legitimate-interest sections only to their audience (DPP V3)"
```

---

### Task 6: Authorities role check and test-report rule (D-03)

**Files:**
- Modify: `web/Services/AccessControlService.cs` (after `HasAllClusterReadScope`)
- Create: `web/Services/PassportDocumentAccessPolicy.cs`
- Create (test): `BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs`:

```csharp
using System.Security.Claims;
using System.Text.RegularExpressions;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class AuthorityOnlyDocumentTests
{
    [Theory]
    [InlineData("notifiedBody", true)]
    [InlineData("marketSurveillanceAuthority", true)]
    [InlineData("commission", true)]
    [InlineData("admin", true)]
    [InlineData("legitimateInterest", false)]
    [InlineData("clusterAdmin", false)]
    [InlineData("member", false)]
    public void AuthorityOnlyData_ShouldBeLimitedToAuthoritiesAndGlobalAdmin(string role, bool expected)
    {
        Assert.Equal(expected, AccessControlService.CanSeeAuthorityOnlyData(UserWithRole(role)));
    }

    [Fact]
    public void AuthorityOnlyData_ShouldBeHiddenFromAnonymousVisitors()
    {
        Assert.False(AccessControlService.CanSeeAuthorityOnlyData(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void TestReport_ShouldBeTheOnlyAuthorityOnlyDocument()
    {
        Assert.True(PassportDocumentAccessPolicy.IsAuthorityOnlyDocument("conformityAssessment"));
        Assert.False(PassportDocumentAccessPolicy.IsAuthorityOnlyDocument("euDeclarationOfConformity"));
        Assert.False(PassportDocumentAccessPolicy.IsAuthorityOnlyDocument("co2StudyReference"));
        Assert.False(PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(string.Empty));
    }

    [Fact]
    public void WithoutAuthorityOnlyData_ShouldRemoveOnlyTheTestReport()
    {
        var passport = new BsonDocument
        {
            ["app"] = new BsonDocument
            {
                ["documents"] = new BsonDocument
                {
                    ["conformityAssessment"] = new BsonDocument { ["url"] = "https://example.test/test-report.pdf" },
                    ["euDeclarationOfConformity"] = new BsonDocument { ["url"] = "https://example.test/doc.pdf" }
                }
            },
            ["aspects"] = new BsonDocument
            {
                ["labeling"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["resultOfTestReport"] = "https://example.test/test-report.pdf",
                        ["declarationOfConformity"] = "https://example.test/doc.pdf"
                    }
                }
            }
        };

        var readable = PassportDocumentAccessPolicy.WithoutAuthorityOnlyData(passport);

        Assert.Null(BsonHelpers.GetValue(readable, "app", "documents", "conformityAssessment"));
        Assert.Null(BsonHelpers.GetValue(readable, "aspects", "labeling", "payload", "resultOfTestReport"));
        Assert.NotNull(BsonHelpers.GetValue(readable, "app", "documents", "euDeclarationOfConformity"));
        Assert.Equal("https://example.test/doc.pdf", BsonHelpers.GetString(readable, "aspects", "labeling", "payload", "declarationOfConformity"));
        // The stored passport itself is untouched.
        Assert.Equal("https://example.test/test-report.pdf", BsonHelpers.GetString(passport, "aspects", "labeling", "payload", "resultOfTestReport"));
    }

    private static ClaimsPrincipal UserWithRole(string role) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tester@example.test"), new Claim(ClaimTypes.Role, role) }, "test"));

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

(`Regex` and `RepoFile` are used by the tests added in Tasks 7–9. `BsonHelpers.GetValue` returns `null` for a missing path.)

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AuthorityOnlyDocumentTests"`
Expected: build FAIL — `'AccessControlService' does not contain a definition for 'CanSeeAuthorityOnlyData'` and `The name 'PassportDocumentAccessPolicy' does not exist`.

- [ ] **Step 3: Add the role check**

In `web/Services/AccessControlService.cs`, after:

```csharp
    public static bool HasAllClusterReadScope(ClaimsPrincipal user) =>
        IsAdmin(user) || HasGlobalReportReadRole(user);
```

add:

```csharp
    // V3 rows #7, #12, #13, #29: notified bodies, market surveillance authorities and the Commission only.
    // Global admins keep access because they own and upload this data.
    public static bool CanSeeAuthorityOnlyData(ClaimsPrincipal user) =>
        IsAdmin(user)
        || user.IsInRole(RoleNotifiedBody)
        || user.IsInRole(RoleMarketSurveillanceAuthority)
        || user.IsInRole(RoleCommission);
```

- [ ] **Step 4: Create the document rule**

Create `web/Services/PassportDocumentAccessPolicy.cs`:

```csharp
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

// Document access fixed by the DPP attribute list (V3), independent of the stored "visibility" value:
// row #29 "Results of test reports proving compliance" is for notified bodies,
// market surveillance authorities and the Commission only.
public static class PassportDocumentAccessPolicy
{
    public const string AuthoritiesOnlyVisibility = "authorities";

    private static readonly HashSet<string> AuthorityOnlyDocumentKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "conformityAssessment"
    };

    // Where the test report lives in a passport document.
    private static readonly IReadOnlyList<string[]> AuthorityOnlyDataPaths =
    [
        ["app", "documents", "conformityAssessment"],
        ["aspects", "labeling", "payload", "resultOfTestReport"]
    ];

    public static bool IsAuthorityOnlyDocument(string? documentKey) =>
        !string.IsNullOrWhiteSpace(documentKey) && AuthorityOnlyDocumentKeys.Contains(documentKey.Trim());

    // Returns a copy without authority-only data, for readers that are not authorities.
    public static BsonDocument WithoutAuthorityOnlyData(BsonDocument passport)
    {
        var copy = passport.DeepClone().AsBsonDocument;
        foreach (var path in AuthorityOnlyDataPaths)
        {
            RemovePath(copy, path);
        }

        return copy;
    }

    private static void RemovePath(BsonDocument document, string[] path)
    {
        var current = document;
        for (var index = 0; index < path.Length - 1; index++)
        {
            if (!current.TryGetValue(path[index], out var next) || next is not BsonDocument child)
            {
                return;
            }

            current = child;
        }

        current.Remove(path[^1]);
    }
}
```

- [ ] **Step 5: Run the tests and see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AuthorityOnlyDocumentTests|FullyQualifiedName~PublicVisibilityPolicyTests|FullyQualifiedName~PurchaserDataAccessTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add web/Services/AccessControlService.cs web/Services/PassportDocumentAccessPolicy.cs BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs
git commit -m "feat: add authorities-only rule for test reports (DPP V3 #29)"
```

---

### Task 7: Enforce the rule on file download and upload (D-03)

**Files:**
- Modify: `web/Controllers/FilesApiController.cs` (`Upload` after line 86 and line 102; `Download` after line 225)
- Modify (test): `BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `AuthorityOnlyDocumentTests` (above `UserWithRole`):

```csharp
    [Fact]
    public void FilesApi_ShouldGuardTheTestReport()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));

        Assert.Contains("PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(documentKey)", controller);
        Assert.Contains("!AccessControlService.CanSeeAuthorityOnlyData(User)", controller);
        Assert.Contains("\"authorityOnlyDocument\"", controller);
        Assert.Contains("PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(normalizedDocumentKey) && !AccessControlService.IsAdmin(User)", controller);
        Assert.Contains("\"authorityOnlyDocumentUpload\"", controller);
    }
```

- [ ] **Step 2: Run the test and see it fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AuthorityOnlyDocumentTests.FilesApi_ShouldGuardTheTestReport"`
Expected: FAIL — `Assert.Contains() Failure`.

- [ ] **Step 3: Guard the download**

In `web/Controllers/FilesApiController.cs`, `Download`, directly before:

```csharp
        var isPassportPublic = _passportPublishPolicyService.IsPubliclyVisible(passport);
```

insert:

```csharp
        if (PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(documentKey)
            && !AccessControlService.CanSeeAuthorityOnlyData(User))
        {
            return await DenyDownloadAsync(
                fileId,
                linkedPassportId,
                documentKey,
                PassportDocumentAccessPolicy.AuthoritiesOnlyVisibility,
                "authorityOnlyDocument",
                "This document is only available to notified bodies, market surveillance authorities and the Commission.",
                cancellationToken);
        }
```

The denial is audited through the existing `passport.file.download.denied` event.

- [ ] **Step 4: Guard the upload**

In `Upload`, directly after the block that ends with:

```csharp
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "You do not have access to upload files for this passport." });
        }
```

insert:

```csharp
        if (PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(normalizedDocumentKey) && !AccessControlService.IsAdmin(User))
        {
            await AppendFileDeniedAuditEventAsync(
                normalizedPassportId,
                string.Empty,
                normalizedDocumentKey,
                PassportDocumentAccessPolicy.AuthoritiesOnlyVisibility,
                "authorityOnlyDocumentUpload",
                cancellationToken);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Only global admins can upload this document." });
        }
```

Then replace (line 102):

```csharp
        var visibility = NormalizeVisibility(publicAccess ?? string.Empty);
```

with:

```csharp
        var visibility = PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(normalizedDocumentKey)
            ? PassportDocumentAccessPolicy.AuthoritiesOnlyVisibility
            : NormalizeVisibility(publicAccess ?? string.Empty);
```

- [ ] **Step 5: Run the tests and see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AuthorityOnlyDocumentTests|FullyQualifiedName~DocumentAccessControlTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add web/Controllers/FilesApiController.cs BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs
git commit -m "fix: restrict test report download and upload to authorities (DPP V3 #29)"
```

---

### Task 8: Hide the test report in the pages (D-03)

**Files:**
- Modify: `web/Models/ViewModels/PassportDetailViewModel.cs`
- Modify: `web/Controllers/PassportController.cs` (`Detail`, second view-model initialiser)
- Modify: `web/Views/Passport/Detail.cshtml:240`
- Modify: `web/Views/Admin/EditPassport.cshtml` (top code block; compliance documents loop)
- Modify: `web/Controllers/AdminController.cs:3026-3029`
- Modify: `web/Services/ProductTemplateModels.cs:148`
- Modify: `web/Views/Admin/Product.cshtml` (template document list)
- Modify (test): `BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `AuthorityOnlyDocumentTests` (above `UserWithRole`):

```csharp
    [Fact]
    public void Pages_ShouldOnlyShowTheTestReportToAuthorities()
    {
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var detailModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportDetailViewModel.cs"));
        var passportController = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
        var editForm = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var familyEditor = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("Model.CanViewAuthorityOnlyData && !string.IsNullOrWhiteSpace(passport.Documents.ConformityAssessment.Url)", detail);
        Assert.Contains("public bool CanViewAuthorityOnlyData { get; init; }", detailModel);
        Assert.Contains("CanViewAuthorityOnlyData = AccessControlService.CanSeeAuthorityOnlyData(User)", passportController);
        Assert.Contains("PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(document.DocumentKey)", editForm);
        Assert.Contains("if (isAuthorityOnlyDocument && !canSeeAuthorityOnlyData)", editForm);
        Assert.Contains("Only notified bodies, market surveillance authorities and the Commission can see this document.", editForm);
        Assert.Contains("Authorities only (notified bodies, market surveillance, Commission)", editForm);
        Assert.Contains("PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(key)", adminController);
        Assert.Contains("PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(document.DocumentKey) ? \"authorities only\" : document.Visibility", familyEditor);
    }

    [Fact]
    public void NewFamilyTemplates_ShouldSeedTheTestReportAsAuthoritiesOnly()
    {
        var testReport = BatteryProductTemplateCatalog.DefaultProduct.TemplateDocuments
            .Single(document => document.DocumentKey == "conformityAssessment");

        Assert.Equal(PassportDocumentAccessPolicy.AuthoritiesOnlyVisibility, testReport.Visibility);
    }
```

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AuthorityOnlyDocumentTests"`
Expected: 2 FAIL (`Pages_...` on `Model.CanViewAuthorityOnlyData`; `NewFamilyTemplates_...` expected `authorities`, actual `public`).

- [ ] **Step 3: Detail page**

In `web/Models/ViewModels/PassportDetailViewModel.cs`, after `public bool CanViewTrustConformance { get; init; }` add:

```csharp
    public bool CanViewAuthorityOnlyData { get; init; }
```

In `web/Controllers/PassportController.cs`, `Detail`, replace (the occurrence **with** a trailing comma; after Task 5 it is followed by `CanViewPurchaserData`):

```csharp
            CanViewTrustConformance = canViewTrustConformance,
```

with:

```csharp
            CanViewTrustConformance = canViewTrustConformance,
            CanViewAuthorityOnlyData = AccessControlService.CanSeeAuthorityOnlyData(User),
```

In `web/Views/Passport/Detail.cshtml`, replace line 240:

```cshtml
                    @if (!string.IsNullOrWhiteSpace(passport.Documents.ConformityAssessment.Url))
```

with:

```cshtml
                    @if (Model.CanViewAuthorityOnlyData && !string.IsNullOrWhiteSpace(passport.Documents.ConformityAssessment.Url))
```

- [ ] **Step 4: Edit form (cluster admins reuse it)**

In `web/Views/Admin/EditPassport.cshtml`, in the top `@{ ... }` block, after `var isBatteryRecordEdit = isBatteryEdit || isClusterEdit;` add:

```cshtml
    var canSeeAuthorityOnlyData = BatteryPassWeb.Services.AccessControlService.CanSeeAuthorityOnlyData(User);
```

Replace the start of the compliance loop:

```cshtml
                    @foreach (var document in complianceDocuments)
                    {
                        <div class="bp-document-row bp-span-all" data-document-upload-card data-document-key="@document.DocumentKey" data-upload-state="idle">
```

with:

```cshtml
                    @foreach (var document in complianceDocuments)
                    {
                        var isAuthorityOnlyDocument = BatteryPassWeb.Services.PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(document.DocumentKey);
                        if (isAuthorityOnlyDocument && !canSeeAuthorityOnlyData)
                        {
                            <div class="bp-document-row bp-span-all" data-document-key="@document.DocumentKey">
                                <div class="bp-field-shell bp-document-field-shell">
                                    <span>@document.Document.Label</span>
                                    <small class="bp-field-help">Only notified bodies, market surveillance authorities and the Commission can see this document.</small>
                                </div>
                            </div>
                            continue;
                        }
                        <div class="bp-document-row bp-span-all" data-document-upload-card data-document-key="@document.DocumentKey" data-upload-state="idle">
```

In the same compliance loop, replace the visibility label and the lines that close the section (this anchor is unique because it ends at the supply-chain section):

```cshtml
                            <label class="bp-field-shell bp-document-visibility-field"><span>@document.Document.Label visibility <em class="bp-field-marker">@RequirementLabel(document.Key)</em></span><select name="@document.VisibilityName" data-document-visibility="@document.DocumentKey"><option value="private" selected="@(!IsPublicDocument(document.Document))">Private / privileged</option><option value="public" selected="@(IsPublicDocument(document.Document))">Public document</option></select><small class="bp-field-help">@DirtySignedDataHelp(document.Key, "Controls who can download this document.")</small></label>
                        </div>
                    }
                </div>
            </section>

            <section id="admin-supply-chain" class="bp-card bp-edit-section">
```

with:

```cshtml
                            @if (isAuthorityOnlyDocument)
                            {
                                <div class="bp-field-shell bp-document-visibility-field"><span>@document.Document.Label visibility</span><input type="text" readonly value="Authorities only (notified bodies, market surveillance, Commission)" aria-label="@document.Document.Label visibility" /><small class="bp-field-help">Fixed by the Battery Regulation access rules.</small></div>
                            }
                            else
                            {
                                <label class="bp-field-shell bp-document-visibility-field"><span>@document.Document.Label visibility <em class="bp-field-marker">@RequirementLabel(document.Key)</em></span><select name="@document.VisibilityName" data-document-visibility="@document.DocumentKey"><option value="private" selected="@(!IsPublicDocument(document.Document))">Private / privileged</option><option value="public" selected="@(IsPublicDocument(document.Document))">Public document</option></select><small class="bp-field-help">@DirtySignedDataHelp(document.Key, "Controls who can download this document.")</small></label>
                            }
                        </div>
                    }
                </div>
            </section>

            <section id="admin-supply-chain" class="bp-card bp-edit-section">
```

The upload script already falls back to `private` when no visibility select exists (`visibilitySelect?.value ?? 'private'`), and the upload endpoint now stores `authorities` for this key anyway.

- [ ] **Step 5: Save the fixed visibility**

In `web/Controllers/AdminController.cs` (`ApplyPassportForm`, lines 3026–3029), replace:

```csharp
            documentNode["visibility"] = NormalizeDocumentVisibility(Text(
                form,
                $"document_{key}_visibility",
                documentNode.GetValue("visibility", "private").ToString()));
```

with:

```csharp
            documentNode["visibility"] = PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(key)
                ? PassportDocumentAccessPolicy.AuthoritiesOnlyVisibility
                : NormalizeDocumentVisibility(Text(
                    form,
                    $"document_{key}_visibility",
                    documentNode.GetValue("visibility", "private").ToString()));
```

- [ ] **Step 6: Template seed and family editor label**

In `web/Services/ProductTemplateModels.cs` (line 148), replace:

```csharp
        new("conformityAssessment", "Conformity assessment", "01-conformity-assessment-report.pdf", "public"),
```

with:

```csharp
        new("conformityAssessment", "Conformity assessment", "01-conformity-assessment-report.pdf", PassportDocumentAccessPolicy.AuthoritiesOnlyVisibility),
```

In `web/Views/Admin/Product.cshtml`, replace:

```cshtml
                        <span class="bp-template-document-visibility">@document.Visibility</span>
```

with:

```cshtml
                        <span class="bp-template-document-visibility">@(BatteryPassWeb.Services.PassportDocumentAccessPolicy.IsAuthorityOnlyDocument(document.DocumentKey) ? "authorities only" : document.Visibility)</span>
```

(`FromProductDocument` / `FromProductVersionDocument` keep the template document list from this code default rather than loading it from MongoDB, so the new value applies to every family at once. Batteries and passports created earlier keep the stored text `public`, which no longer controls access.)

- [ ] **Step 7: Run the tests and see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AuthorityOnlyDocumentTests|FullyQualifiedName~PurchaserDataAccessTests|FullyQualifiedName~DocumentAccessControlTests|FullyQualifiedName~ProductTemplateServiceTests|FullyQualifiedName~ProductTemplateWorkflowLayoutTests"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add web/Models/ViewModels/PassportDetailViewModel.cs web/Controllers/PassportController.cs web/Views/Passport/Detail.cshtml web/Views/Admin/EditPassport.cshtml web/Controllers/AdminController.cs web/Services/ProductTemplateModels.cs web/Views/Admin/Product.cshtml BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs
git commit -m "fix: show the test report only to authorities in the pages (DPP V3 #29)"
```

---

### Task 9: Close the API side doors (D-01, D-03)

**Files:**
- Modify: `web/Controllers/ExternalApiController.cs` (lines 215, 262, 309, 355; helper after `GetLatestPassportForBatteryAsync` at line 806)
- Modify: `web/Controllers/PassportsApiController.cs` (`List` line 49, `Detail` line 56)
- Modify (test): `BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs`

External API tokens are cluster-scoped integrations (acting for the battery's cluster) or global integrations created by your team, so they keep reading purchaser data; only the test report is removed.

- [ ] **Step 1: Write the failing tests**

Add to `AuthorityOnlyDocumentTests` (above `UserWithRole`):

```csharp
    [Fact]
    public void ExternalApiReads_ShouldDropTheTestReport()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Equal(4, Regex.Matches(controller, @"var passport = await GetLatestReadablePassportForBatteryAsync\(batteryId, cancellationToken\);").Count);
        Assert.DoesNotContain("var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);", controller);
        Assert.Contains("PassportDocumentAccessPolicy.WithoutAuthorityOnlyData(latest)", controller);
    }

    [Fact]
    public void InternalPassportJsonReads_ShouldBeAdminOnly()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));

        Assert.Matches(new Regex(@"IActionResult> List\(\[FromQuery\] string\? q, CancellationToken cancellationToken\)\s*\{\s*if \(!User\.IsInRole\(""admin""\)\)"), controller);
        Assert.Matches(new Regex(@"IActionResult> Detail\(string passportId, CancellationToken cancellationToken\)\s*\{\s*if \(!User\.IsInRole\(""admin""\)\)"), controller);
    }
```

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AuthorityOnlyDocumentTests"`
Expected: 2 FAIL (count 0 instead of 4; regex does not match).

- [ ] **Step 3: Redact the external API reads**

In `web/Controllers/ExternalApiController.cs`, after the existing helper:

```csharp
    private async Task<BsonDocument?> GetLatestPassportForBatteryAsync(string batteryId, CancellationToken cancellationToken)
    {
        return (await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: false, cancellationToken)).FirstOrDefault();
    }
```

add:

```csharp
    // Read endpoints serve integration tokens, which are never authorities (V3 row #29).
    private async Task<BsonDocument?> GetLatestReadablePassportForBatteryAsync(string batteryId, CancellationToken cancellationToken)
    {
        var latest = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
        return latest == null ? null : PassportDocumentAccessPolicy.WithoutAuthorityOnlyData(latest);
    }
```

Then replace all four occurrences of this exact line (in `GetBattery`, `GetBatterySection`, `GetBatteryValues`, `GetBatteryPaths` — lines 215, 262, 309, 355):

```csharp
        var passport = await GetLatestPassportForBatteryAsync(batteryId, cancellationToken);
```

with:

```csharp
        var passport = await GetLatestReadablePassportForBatteryAsync(batteryId, cancellationToken);
```

Leave lines 455 and 588 (`var latestPassport = ...`) alone: the telemetry and operations writes only read the passport ID.

- [ ] **Step 4: Make the internal JSON reads admin-only**

In `web/Controllers/PassportsApiController.cs`, replace:

```csharp
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var passports = await _passportRepository.SearchAsync(q ?? string.Empty, includeArchived: true, cancellationToken);
```

with:

```csharp
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        var passports = await _passportRepository.SearchAsync(q ?? string.Empty, includeArchived: true, cancellationToken);
```

and replace:

```csharp
    public async Task<IActionResult> Detail(string passportId, CancellationToken cancellationToken)
    {
        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
```

with:

```csharp
    public async Task<IActionResult> Detail(string passportId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
```

- [ ] **Step 5: Run the tests and see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AuthorityOnlyDocumentTests|FullyQualifiedName~AnonymousVerifyAccessTests|FullyQualifiedName~ExternalApi"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add web/Controllers/ExternalApiController.cs web/Controllers/PassportsApiController.cs BatteryPassWeb.Tests/AuthorityOnlyDocumentTests.cs
git commit -m "fix: keep restricted passport data out of API reads"
```

---

### Task 10: Verify end to end

- [ ] **Step 1: Full test suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: **508 passed, 0 failed** (464 baseline + 10 in `BatteryStatusComplianceTests` + 19 in `PurchaserDataAccessTests` + 15 in `AuthorityOnlyDocumentTests`).

- [ ] **Step 2: Manual check in the running app**

Start the app against a test MongoDB: `dotnet run --project web` → `http://localhost:5186`. Accounts are in `docs/sample-cluster-test-accounts.md` (password `<the fallback password>`). No authority or legitimate-interest accounts are seeded: as `admin@example.test`, open `/admin/clusters?tab=users` and create one user each with the roles **Person with Legitimate Interest**, **Notified Body** and **Market Surveillance Authorities** (no cluster membership).

1. Signed out, open the summary of a published passport of battery `CP7M-NORTH-001`: no "Battery status" row.
2. As `north.user@example.test` (North cluster user), open its detail page: battery status, state of charge, full cycles, the operational block and the Material composition tab are shown; the Compliance tab shows the EU declaration of conformity but **not** the conformity assessment report.
3. As the legitimate-interest user: no battery status, SoC, full cycles, remaining capacity/energy or operational block; the Material composition tab is shown; no conformity assessment report.
4. As the notified-body user: no purchaser data and no Material composition tab; the conformity assessment report is shown.
5. As the market-surveillance user: everything, including the conformity assessment report.
6. As `admin@example.test`, open `/admin/clusters?tab=local-editable-fields`: Battery status, State of charge and Full cycles show all four toggles checked and disabled, with the purchaser-data note; save the tab and reload — they stay checked.
7. As `north.admin@example.test` (cluster admin), edit a North battery: Battery status is an enabled dropdown; change it to `Repurposed` and save → the battery shows "New passport needed"; create, validate, sign and publish the new passport. The conformity assessment row shows only the restriction notice (no URL, no upload).
8. As admin, edit battery `CP7M-NORTH-002`: Battery status is a dropdown; changing it also flags "New passport needed".
9. As `north.user@example.test`, browse to `/api/passports/<a passport ID>`: you are redirected to `/login` (cookie auth sends denied requests to `AccessDeniedPath = "/login"`) and no passport JSON is returned. As admin, the same URL returns the JSON.

- [ ] **Step 3: Update the testing guide**

In `docs/end-user-testing-guide.md`, replace:

```markdown
- `GET/POST/PUT/DELETE /api/passports` (admin write required)
```

with:

```markdown
- `GET/POST/PUT/DELETE /api/passports` (global admin only)
```

and replace:

```markdown
- Document links:
  - Conformity assessment
  - EU declaration of conformity
```

with:

```markdown
- Document links:
  - Conformity assessment (shown only to notified bodies, market surveillance authorities, the Commission and global admins)
  - EU declaration of conformity
```

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~Phase6ADocumentationTests|FullyQualifiedName~QaTesterPackDocumentationTests|FullyQualifiedName~AdminHelpPageTests"`
Expected: PASS.

```bash
git add docs/end-user-testing-guide.md
git commit -m "docs: note restricted test report and admin-only passport JSON API"
```

---

## Self-review notes

- **Coverage:** D-01 → Tasks 1 + 5 (+ Task 9 closes the JSON API). D-02, D-59 → Tasks 2–4. D-44, D-51 → Task 3. D-04, D-05, D-34 → Task 5. D-03 → Tasks 6–9.
- **Names used across tasks:** `BatteryStatusCatalog.{AllowedValues, Normalize, Original, InvalidValueMessage}`; `EditableFieldPolicyService.IsPurchaserEditableField`; `AccessControlService.{CanSeePurchaserData, CanSeeLegitimateInterestData, CanSeeAuthorityOnlyData}`; `PassportDocumentAccessPolicy.{IsAuthorityOnlyDocument, WithoutAuthorityOnlyData, AuthoritiesOnlyVisibility}`; `PassportDetailViewModel.{CanViewPurchaserData, CanViewLegitimateInterestData, CanViewAuthorityOnlyData}`; `GetLatestReadablePassportForBatteryAsync`.
- **Order dependencies:** Tasks 5, 6 and 8 all edit `AccessControlService`, `PassportDetailViewModel` and `PassportController`; each inserts after an anchor the earlier task leaves in place, so run the tasks in order.
- **Known leftovers (deliberate):** batteries and passports created before this change keep the stored text `visibility: "public"` on the test report until re-saved (access is key-based, so this is cosmetic); the Conformance page still shows cluster admins that test-report evidence exists and its hash (no link, no content); cluster admins keep their default edit rights over model, facility, software version and cluster until D-09 is decided.
