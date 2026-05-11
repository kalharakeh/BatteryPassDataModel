# Product Version Admin Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement nested product/battery versions with version-scoped software, restore broken create/push/admin flows, and update help/test documentation.

**Architecture:** Keep the ASP.NET MVC shape already in the app. Add product-version records as a MongoDB-backed template layer between product and software. Keep controllers thin by adding focused service/model helpers, and keep Razor changes tied to the existing admin page conventions.

**Tech Stack:** ASP.NET Core MVC on .NET 10, Razor views, MongoDB.Driver, xUnit source/service tests, existing CSS in `web/wwwroot/css/site.css`.

---

## File Map

- Modify `web/Services/ProductTemplateModels.cs`: add product-version records, version-aware software records, catalog defaults, builder inputs, BSON conversion helpers, version sorting helpers.
- Modify `web/Services/ProductTemplateService.cs`: add MongoDB collections for product versions and product-version software, migration fallback from flat documents, version-aware build/save/push/reset logic.
- Modify `web/Services/DataCompletionPolicyService.cs`: add product-version policy lookup/save while retaining product/global fallback.
- Create `web/Services/LocalAdminEditableFieldPolicyService.cs`: MongoDB-backed global editable-field policy.
- Modify `web/Program.cs`: register the new policy service.
- Modify `web/Models/ViewModels/AdminClusterViewModel.cs`: add product-version and credential-management view models.
- Modify `web/Models/ViewModels/EditPassportViewModel.cs`: add selected product version and version-aware catalog fields.
- Modify `web/Controllers/AdminController.cs`: version-aware product/passport routes, API Token Management tab, local editable fields page/actions, push route with product version.
- Modify `web/Controllers/ClusterAdminController.cs`: enforce global local-admin editable-field policy in model and save path.
- Modify `web/Controllers/PassportController.cs`: fix summary detail notice behavior for connected normal users.
- Modify `web/Views/Admin/Clusters.cshtml`: combine API token and battery secret panes under one tab, add local editable fields nav, one-row navigation, password reveal control.
- Modify `web/Views/Admin/Product.cshtml`: product-version editor, software versions inside each product version, base-on-existing controls.
- Modify `web/Views/Admin/EditPassport.cshtml`: cascading product -> product version -> software controls, empty identity fields on new passport.
- Modify `web/Views/ClusterAdmin/EditPassport.cshtml`: render fields according to global editable-field policy.
- Modify `web/Views/ClusterAdmin/Users.cshtml`: password reveal controls.
- Modify `web/Views/Help/Index.cshtml` and `web/Views/Admin/Help.cshtml`: explain nested versions and credential usage.
- Modify `web/wwwroot/css/site.css`: compact current-version fields, password reveal layout, one-row admin nav, product-version UI.
- Modify docs: `docs/end-user-testing-guide.md`, `docs/qa-test-pack.md`, `docs/sample-cluster-test-accounts.md`.
- Add/modify tests under `BatteryPassWeb.Tests/`.

---

### Task 1: Product-Version Model And Builder Tests

**Files:**
- Modify: `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`
- Modify: `web/Services/ProductTemplateModels.cs`

- [ ] **Step 1: Write failing catalog and builder tests**

Add tests to `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`:

```csharp
[Fact]
public void ProductTemplateCatalog_ShouldNestSoftwareUnderProductVersionsNewestFirst()
{
    var product = BatteryProductTemplateCatalog.DefaultProducts.Single(item => item.ProductId == "compact-7m");

    Assert.Equal(["2.0", "1.0"], product.ProductVersions.Select(version => version.Version).ToArray());
    Assert.All(product.ProductVersions, version => Assert.NotEmpty(version.SoftwareVersions));
    Assert.Equal(["4.0", "3.0"], product.ProductVersions.First().SoftwareVersions.Select(version => version.Version).ToArray());
}

[Fact]
public void BuildPassportFromTemplate_ShouldStoreProductVersionAndSoftwareVersion()
{
    var product = BatteryProductTemplateCatalog.DefaultProducts.Single(item => item.ProductId == "compact-7m");
    var productVersion = product.ProductVersions.Single(item => item.Version == "2.0");
    var software = productVersion.SoftwareVersions.Single(item => item.Version == "4.0");

    var passport = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
        "did:web:acme.battery.pass:versioned-001",
        product,
        productVersion,
        software,
        new ProductTemplateBatteryIdentity(),
        "2026-05-11T10:00:00.0000000Z");

    Assert.Equal("compact-7m", BsonHelpers.GetString(passport, "app", "product", "productId"));
    Assert.Equal("2.0", BsonHelpers.GetString(passport, "app", "product", "productVersion"));
    Assert.Equal("4.0", BsonHelpers.GetString(passport, "app", "product", "softwareVersion"));
    Assert.True((BsonHelpers.GetValue(passport, "app", "templateBaseline") as BsonDocument)?.ElementCount > 0);
}
```

- [ ] **Step 2: Run tests and verify red**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter ProductTemplateServiceTests`

Expected: FAIL because `ProductVersions` and the new builder overload do not exist.

- [ ] **Step 3: Implement model records and catalog defaults**

In `web/Services/ProductTemplateModels.cs`:

- add `BatteryProductVersion` with `Version`, all current shared template fields, `SoftwareVersions`, `TemplateDocuments`, and `RequiredFieldKeys`
- move shared values from `BatteryProductTemplate` into product versions
- keep product-level identity fields on `BatteryProductTemplate`
- seed two product versions, `2.0` then `1.0`, for Compact 7M, Compact 13M, and Core
- seed software versions inside each product version, newest-first
- keep compatibility properties on `BatteryProductTemplate` that return the latest product version values where old call sites still need them during the refactor

- [ ] **Step 4: Implement builder overload**

Update `ProductTemplatePassportBuilder.BuildPassportFromTemplate` to accept:

```csharp
BatteryProductTemplate product,
BatteryProductVersion productVersion,
BatteryProductSoftwareVersion softwareVersion
```

Set `app.product.productVersion`, use product-version shared values for all payload fields, and compute `app.templateBaseline` from that versioned template.

- [ ] **Step 5: Run tests and verify green**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter ProductTemplateServiceTests`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add web/Services/ProductTemplateModels.cs BatteryPassWeb.Tests/ProductTemplateServiceTests.cs
git commit -m "feat: add product version template model"
```

---

### Task 2: MongoDB Persistence, Fallback, And Push

**Files:**
- Modify: `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`
- Modify: `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Services/DataCompletionPolicyService.cs`

- [ ] **Step 1: Write failing source/service tests**

Add assertions proving:

```csharp
Assert.Contains("batteryProductTemplateVersions", source);
Assert.Contains("batteryProductTemplateSoftwareVersions", source);
Assert.Contains("productVersion", source);
Assert.Contains("PushTemplateAsync(string productId, string productVersion, string softwareVersion", source);
Assert.Contains("GetProductVersionPolicyAsync", dataCompletionSource);
```

Add a builder test that creates a passport at product version `1.0`, then computes a product version `2.0` baseline and confirms safe updates preserve manual overrides.

- [ ] **Step 2: Run tests and verify red**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "ProductTemplateServiceTests|ProductTemplateWorkflowLayoutTests"`

Expected: FAIL because collections, policy methods, and version-aware push do not exist.

- [ ] **Step 3: Implement MongoDB collection split**

In `ProductTemplateService.cs`, use these collections:

- `batteryProductTemplates` for product-level documents
- `batteryProductTemplateVersions` for product-version documents keyed by `productId + version`
- `batteryProductTemplateSoftwareVersions` for software documents keyed by `productId + productVersion + version`
- keep `batteryProductTemplatePushRuns`

Update `EnsureDefaultTemplatesAsync`, `ListProductsAsync`, `GetProductAsync`, `SaveProductAsync`, and `GetProductDocumentAsync` to read/write the split collections.

- [ ] **Step 4: Add flat-document fallback**

When no version documents exist for a product but the old product document has shared fields/software, convert it in memory to product version `1.0` and old software versions so existing databases still render.

- [ ] **Step 5: Add product-version required policy**

In `DataCompletionPolicyService.cs`, add:

```csharp
public Task<DataCompletionPolicySnapshot> GetProductVersionPolicyAsync(string productId, string productVersion, CancellationToken cancellationToken = default)
public Task SaveProductVersionPolicyAsync(string productId, string productVersion, IReadOnlyCollection<string> requiredFieldKeys, string actor, CancellationToken cancellationToken = default)
```

Store in `batteryProductCompletionPolicies` using both `productId` and `productVersion`. `GetPolicyForPassportAsync` reads `app.product.productVersion` and falls back to product policy, then global policy.

- [ ] **Step 6: Implement version-aware push**

Change push signature to:

```csharp
PushTemplateAsync(string productId, string productVersion, string softwareVersion, string actor, CancellationToken cancellationToken = default)
```

Filter passports by:

- `app.product.productId`
- `app.product.productVersion`
- `app.product.softwareVersion`

Build the fresh baseline from the selected product version and software version. Keep safe-update semantics unchanged.

- [ ] **Step 7: Run tests and verify green**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "ProductTemplateServiceTests|ProductTemplateWorkflowLayoutTests"`

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add web/Services/ProductTemplateService.cs web/Services/DataCompletionPolicyService.cs BatteryPassWeb.Tests/ProductTemplateServiceTests.cs BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
git commit -m "feat: persist versioned product templates"
```

---

### Task 3: Product Template Admin UI And New Product Creation

**Files:**
- Modify: `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Views/Admin/Product.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Write failing UI/source tests**

In `ProductTemplateWorkflowLayoutTests.cs`, assert:

```csharp
Assert.Contains("name=\"productVersion\"", productView);
Assert.Contains("data-product-version-list", productView);
Assert.Contains("data-base-product-select", productView);
Assert.Contains("data-base-product-version-select", productView);
Assert.Contains("data-base-software-select", productView);
Assert.Contains("products/{productId}/versions/{productVersion}/software/{softwareVersion}/push", adminController);
Assert.DoesNotContain("does not create three default software versions", productView);
```

- [ ] **Step 2: Run tests and verify red**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter ProductTemplateWorkflowLayoutTests`

Expected: FAIL because the product editor is still flat.

- [ ] **Step 3: Extend view models**

Add:

- `ProductVersionViewModel`
- `ProductVersionEditViewModel`
- `ProductTemplateBaseOptionViewModel`

Each software view model remains `ProductSoftwareVersionViewModel`.

- [ ] **Step 4: Update product routes**

In `AdminController.cs`:

- `NewProduct` returns empty product-level and empty first version fields, plus base catalog JSON
- `Product` loads product plus versions newest-first
- `SaveProduct` reads `productVersion`, version-specific fields, and nested software rows
- push route becomes `[HttpPost("products/{productId}/versions/{productVersion}/software/{softwareVersion}/push")]`

- [ ] **Step 5: Update `Product.cshtml`**

Render product identity at top, then product-version rows/cards. Each version contains:

- version input
- shared product values
- software version rows
- required/optional policy
- push buttons for each software version

Add base-on-existing controls for new product templates:

- product select
- product version select
- software select
- client-side fill from serialized MongoDB catalog

- [ ] **Step 6: Style compact version rows**

In `site.css`, keep `.bp-template-version-row` compact and add `.bp-product-version-row`, `.bp-product-version-current`, and `.bp-version-field-compact` with field widths matching neighboring form fields.

- [ ] **Step 7: Run tests and verify green**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter ProductTemplateWorkflowLayoutTests`

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add web/Models/ViewModels/AdminClusterViewModel.cs web/Controllers/AdminController.cs web/Views/Admin/Product.cshtml web/wwwroot/css/site.css BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
git commit -m "feat: add versioned product template editor"
```

---

### Task 4: New Passport Flow And Summary Access Fix

**Files:**
- Modify: `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`
- Modify: `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`
- Modify: `web/Models/ViewModels/EditPassportViewModel.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Views/Admin/EditPassport.cshtml`

- [ ] **Step 1: Write failing tests**

Add source/layout assertions:

```csharp
Assert.Contains("name=\"productVersion\"", edit);
Assert.Contains("data-product-version-select", edit);
Assert.Contains("updateProductVersionOptions", edit);
Assert.Contains("updateSoftwareOptions(productVersion", edit);
Assert.Contains("SelectedProductVersion", editModelSource);
```

Add access-control test assertions that `PassportController.cs` does not set `DetailAccessNotice` when `CanOpenPassportDetailAsync` is true and that the wrong-cluster redirect notice is only applied when access is actually wrong.

- [ ] **Step 2: Run tests and verify red**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "ProductTemplateWorkflowLayoutTests|DocumentAccessControlTests"`

Expected: FAIL.

- [ ] **Step 3: Update edit passport view model**

Add:

- `SelectedProductVersion`
- version-aware catalog items with `ProductVersions`
- `ProductSoftwareVersions` derived from selected product version

- [ ] **Step 4: Update new passport document build**

Change `BuildDraftPassportDocumentAsync` and create/save paths to accept product ID, product version, and software version. New page identity fields stay empty, and shared values come from selected version/software.

- [ ] **Step 5: Update admin edit Razor JavaScript**

Add cascade functions:

- `updateProductVersionOptions(product, preferredVersion)`
- `updateSoftwareOptions(productVersion, preferredSoftware)`
- `applyProductTemplateSelection(updateVersionList, updateSoftwareList)`

Ensure product changes update product versions, and product version changes update software versions.

- [ ] **Step 6: Fix summary access notice**

In `PassportController.Summary`, keep `detailAccessNotice` empty whenever `canOpenDetail` is true. For `access=wrong-cluster`, only show the notice when `canOpenDetail` is false.

- [ ] **Step 7: Run tests and verify green**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "ProductTemplateWorkflowLayoutTests|DocumentAccessControlTests"`

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add web/Models/ViewModels/EditPassportViewModel.cs web/Controllers/AdminController.cs web/Controllers/PassportController.cs web/Views/Admin/EditPassport.cshtml BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs BatteryPassWeb.Tests/DocumentAccessControlTests.cs
git commit -m "fix: cascade versioned passport creation"
```

---

### Task 5: Global Local-Admin Editable Fields

**Files:**
- Create: `web/Services/LocalAdminEditableFieldPolicyService.cs`
- Add: `BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs`
- Modify: `web/Program.cs`
- Modify: `web/Models/ViewModels/EditPassportViewModel.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/EditPassport.cshtml`

- [ ] **Step 1: Write failing service and layout tests**

Create `LocalAdminEditableFieldPolicyTests.cs` with tests for default editable keys:

```csharp
Assert.Contains("general.facilityId", snapshot.EditableFieldKeys);
Assert.Contains("general.batteryImageUrl", snapshot.EditableFieldKeys);
Assert.Contains("performance.stateOfCharge", snapshot.EditableFieldKeys);
Assert.Contains("performance.remainingCapacity", snapshot.EditableFieldKeys);
Assert.Contains("performance.remainingEnergy", snapshot.EditableFieldKeys);
Assert.Contains("performance.fullCycles", snapshot.EditableFieldKeys);
Assert.DoesNotContain("general.modelNumber", snapshot.EditableFieldKeys);
```

Add layout assertions that admin has `tab=local-editable-fields` and cluster edit view uses `FieldEditableByKey`.

- [ ] **Step 2: Run tests and verify red**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "LocalAdminEditableFieldPolicyTests|ProductTemplateWorkflowLayoutTests"`

Expected: FAIL.

- [ ] **Step 3: Implement policy service**

Create `LocalAdminEditableFieldPolicyService` backed by MongoDB collection `localAdminEditableFieldPolicies`. Reuse `DataCompletionPolicyService.CreateDefaultPolicy()` field metadata for labels and section grouping. Store `policyKey`, `editableFieldKeys`, `updatedAt`, and `updatedBy`.

- [ ] **Step 4: Register service**

Add `builder.Services.AddSingleton<LocalAdminEditableFieldPolicyService>();` in `Program.cs`.

- [ ] **Step 5: Add admin page**

In `AdminController.Clusters`, load editable policy into model. Add POST action `/admin/local-editable-fields/save` to save checked keys. In `Clusters.cshtml`, add top nav item `Local editable fields` and render global checkboxes grouped by section.

- [ ] **Step 6: Enforce cluster-admin edit policy**

In `ClusterAdminController.EditPassport`, include editable policy in `EditPassportViewModel`. In `SavePassport`, only apply fields present in the saved policy. If no policy exists, use the default policy from the service.

- [ ] **Step 7: Update cluster-admin edit view**

Render only editable fields as inputs. Render non-editable fields as readonly facts or omit them from the form. Keep the save action from posting disallowed fields.

- [ ] **Step 8: Run tests and verify green**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "LocalAdminEditableFieldPolicyTests|ProductTemplateWorkflowLayoutTests"`

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add web/Services/LocalAdminEditableFieldPolicyService.cs web/Program.cs web/Models/ViewModels/EditPassportViewModel.cs web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/EditPassport.cshtml BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
git commit -m "feat: add global local editable field policy"
```

---

### Task 6: API Token Management, Password Reveal, And Navigation Polish

**Files:**
- Modify: `BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`
- Modify: `BatteryPassWeb.Tests/AdminHelpPageTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Users.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Write failing layout tests**

Assert:

```csharp
Assert.Contains("API Token Management", clusters);
Assert.Contains("data-credential-tab=\"api-tokens\"", clusters);
Assert.Contains("data-credential-tab=\"battery-secrets\"", clusters);
Assert.DoesNotContain(">API tokens</a>", clusters);
Assert.DoesNotContain(">Battery secrets</a>", clusters);
Assert.Contains("data-password-reveal", clusters);
Assert.Contains("data-password-reveal", clusterUsers);
Assert.Contains("bp-tab-row", css);
Assert.Contains("flex-wrap: nowrap", css);
```

- [ ] **Step 2: Run tests and verify red**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "AdminCredentialLayoutTests|AdminHelpPageTests"`

Expected: FAIL.

- [ ] **Step 3: Normalize credential tab**

Change tab normalization so `api-token-management`, `api-tokens`, and `battery-secrets` all route to one selected admin tab, with an inner tab determined by query or hash.

- [ ] **Step 4: Merge credential UI**

In `Clusters.cshtml`, replace top-level API token and battery secret sections with one `API Token Management` section containing two internal tab buttons/panels.

- [ ] **Step 5: Add password reveal control**

Wrap password inputs in `.bp-password-field` and add a button with `data-password-reveal` that toggles the sibling input between `password` and `text`. Use text label `Show`/`Hide` or an existing icon-safe button style.

- [ ] **Step 6: Fix admin nav and current-version sizing**

In CSS:

- `.bp-tab-row { flex-wrap: nowrap; overflow-x: auto; }`
- compact credential/product version controls so labels and inputs stay one row on desktop
- apply `.bp-version-field-compact` to current-version fields

- [ ] **Step 7: Run tests and verify green**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "AdminCredentialLayoutTests|AdminHelpPageTests"`

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add web/Controllers/AdminController.cs web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/Users.cshtml web/wwwroot/css/site.css BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs BatteryPassWeb.Tests/AdminHelpPageTests.cs
git commit -m "feat: combine credential management"
```

---

### Task 7: Reset Seed Data And Test Accounts

**Files:**
- Modify: `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `docs/sample-cluster-test-accounts.md`

- [ ] **Step 1: Write failing reset/account tests**

Add source assertions:

```csharp
Assert.Contains("customer_001_001@customer.org", source);
Assert.Contains("\"12345\"", source);
Assert.Contains("CP7M-NORTH-001", source);
Assert.Contains("CP7M-NORTH-002", source);
Assert.Contains("CP13M-SOUTH-001", source);
Assert.Contains("CP13M-SOUTH-002", source);
Assert.Contains("CORE-FLEET-001", source);
Assert.Contains("CORE-FLEET-002", source);
```

Assert reset result returns 7 passports when using a source-level or service-level test seam.

- [ ] **Step 2: Run tests and verify red**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter ProductTemplateServiceTests`

Expected: FAIL.

- [ ] **Step 3: Update reset seeds**

In `ResetTemplateDemoAsync`, seed:

- unassigned/default demonstrator
- `sample-customer-north-001`, Compact 7M version `1.0`
- `sample-customer-north-002`, Compact 7M version `2.0`
- `sample-customer-south-001`, Compact 13M version `1.0`
- `sample-customer-south-002`, Compact 13M version `2.0`
- `sample-end-user-fleet-001`, Core version `1.0`
- `sample-end-user-fleet-002`, Core version `2.0`

Use newest valid software version under each product version unless the seed explicitly needs an older one.

- [ ] **Step 4: Update demo users**

Seed `customer_001_001@customer.org` with password `12345` and membership in `cluster-north-operations`. Keep existing admin/local admin demo accounts unless tests require a smaller set.

- [ ] **Step 5: Run tests and verify green**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter ProductTemplateServiceTests`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add web/Services/ProductTemplateService.cs BatteryPassWeb.Tests/ProductTemplateServiceTests.cs
git commit -m "feat: seed versioned template passports"
```

---

### Task 8: Help And Tester Documentation

**Files:**
- Modify: `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`
- Modify: `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`
- Modify: `web/Views/Help/Index.cshtml`
- Modify: `web/Views/Admin/Help.cshtml`
- Modify: `docs/end-user-testing-guide.md`
- Modify: `docs/qa-test-pack.md`
- Modify: `docs/sample-cluster-test-accounts.md`

- [ ] **Step 1: Write failing documentation tests**

Assert docs contain:

```csharp
Assert.Contains("product/battery version", guide, StringComparison.OrdinalIgnoreCase);
Assert.Contains("API Token Management", guide);
Assert.Contains("one unassigned demonstrator plus six clustered customer batteries", guide);
Assert.Contains("customer_001_001@customer.org", accounts);
Assert.Contains("12345", accounts);
Assert.Contains("CP7M-NORTH-002", accounts);
Assert.Contains("Local editable fields", adminHelp);
```

- [ ] **Step 2: Run tests and verify red**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "Phase6ADocumentationTests|ProductTemplateWorkflowLayoutTests"`

Expected: FAIL.

- [ ] **Step 3: Update public API help**

In `web/Views/Help/Index.cshtml`, explain:

- product templates have product/battery versions
- software versions belong to a product/battery version
- API token value authenticates external API clients
- battery secret is a per-battery shared write credential
- software PATCH must match the selected product/version software list

- [ ] **Step 4: Update admin help**

In `web/Views/Admin/Help.cshtml`, update reset count, product-version workflow, local editable field page, API Token Management, and push-to-batteries wording.

- [ ] **Step 5: Update docs**

Update:

- `docs/end-user-testing-guide.md`
- `docs/qa-test-pack.md`
- `docs/sample-cluster-test-accounts.md`

Replace the old four-passport reset set with the seven-passport set. Replace separate credential-page instructions with `API Token Management`. Add customer user credentials and expected North detail access.

- [ ] **Step 6: Run tests and verify green**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore --filter "Phase6ADocumentationTests|ProductTemplateWorkflowLayoutTests"`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add web/Views/Help/Index.cshtml web/Views/Admin/Help.cshtml docs/end-user-testing-guide.md docs/qa-test-pack.md docs/sample-cluster-test-accounts.md BatteryPassWeb.Tests/Phase6ADocumentationTests.cs BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
git commit -m "docs: update versioned template guidance"
```

---

### Task 9: Full Verification And Local App Check

**Files:**
- Modify only if verification reveals a bug.

- [ ] **Step 1: Run full test suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore`

Expected: PASS, 0 failed.

- [ ] **Step 2: Build web app**

Run: `dotnet build web/BatteryPassWeb.csproj --no-restore`

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Start local app**

Run: `dotnet run --project web/BatteryPassWeb.csproj --urls http://localhost:5186`

Expected: app listens at `http://localhost:5186`. Keep the process running until manual checks are complete.

- [ ] **Step 4: Verify admin UI paths**

In browser:

- sign in as `admin@example.test` using configured demo admin password
- open `/admin/clusters?tab=products`
- reset template data
- create a new product template from empty fields
- create a new product template based on existing product/version/software
- add software `4.0` to a product version, save, and push to matching batteries
- open `/admin/passports/new`, select product -> product version -> software, fill identity fields, save

Expected: no HTTP 500 or browser error page; created records open for edit.

- [ ] **Step 5: Verify North customer access**

In browser:

- sign out
- sign in as `customer_001_001@customer.org` with password `12345`
- open the summary for `CP7M-NORTH-001`
- open detailed report

Expected: summary does not show the wrong-cluster notice, and detail opens for the connected North user.

- [ ] **Step 6: Commit verification fixes only if needed**

If a fix was required:

```bash
git add <changed-files>
git commit -m "fix: address versioned workflow verification"
```

If no fix was required, do not create an empty commit.

