# Passport Admin Test Feedback Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved manual-testing feedback fixes for reports, shared battery/admin pages, editable-field enforcement, reset behavior, cluster/user/token UX, and per-model software versions.

**Architecture:** Add shared view models/partials/services where four pages currently duplicate battery table behavior, then harden the data rules behind those pages. Keep the existing Razor/MVC/MongoDB architecture and avoid large rewrites outside the touched flows.

**Tech Stack:** ASP.NET Core MVC, Razor views, MongoDB.Bson/MongoDB.Driver, xUnit, CSS/vanilla JavaScript.

---

## File Structure

- Modify `web/Models/ViewModels/BatteryViewModels.cs` to add shared battery table/page metadata.
- Create `web/Services/BatteryTableService.cs` to centralize battery row building, search resolution, role-aware URLs, and access messages.
- Modify `web/Program.cs` to register `BatteryTableService`.
- Modify `web/Controllers/RegistryController.cs`, `web/Controllers/AdminController.cs`, and `web/Controllers/ClusterAdminController.cs` to consume the shared battery table service.
- Create or replace `web/Views/Shared/_BatteryTable.cshtml` for the shared battery table.
- Modify `web/Views/Registry/Index.cshtml`, `web/Views/Admin/Clusters.cshtml`, `web/Views/Admin/Passports.cshtml`, and `web/Views/ClusterAdmin/Passports.cshtml` to render the shared table in their current shells.
- Modify `web/Views/Admin/BatteryPassports.cshtml` to preserve return destinations.
- Modify `web/Services/ClusterRepository.cs` and `web/Controllers/AdminController.cs` for duplicate cluster normalization and role normalization.
- Modify `web/Views/Admin/Clusters.cshtml`, `web/Views/ClusterAdmin/Users.cshtml`, and `web/Services/AccessControlService.cs` for role labels and membership roles.
- Modify `web/Views/ClusterAdmin/ApiTokens.cshtml`, `web/Views/Admin/Clusters.cshtml`, and shared scripts/CSS in `web/wwwroot/css/site.css` for token modal and consistent token UX.
- Modify `web/Views/Admin/EditPassport.cshtml`, `web/Controllers/AdminController.cs`, `web/Controllers/ClusterAdminController.cs`, `web/Services/EditableFieldPolicyService.cs`, and `web/Services/BatteryPassportDeltaService.cs` for editable-field enforcement and creation locking.
- Modify `web/Services/ProductTemplateModels.cs`, `web/Services/ProductTemplateService.cs`, `web/Services/BatteryTemplateUpdateService.cs`, `web/Models/ViewModels/AdminClusterViewModel.cs`, `web/Views/Admin/Product.cshtml`, `web/Controllers/ExternalApiController.cs`, and `web/Views/Help/Index.cshtml` for per-model software-version lists.
- Modify `web/Views/Passport/Summary.cshtml`, `web/Views/Passport/Detail.cshtml`, and `web/wwwroot/css/site.css` for shared report header, chart colors, and bounded telemetry charts.
- Add/update xUnit tests in `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`, plus focused existing test updates where old assertions conflict with approved behavior.

---

### Task 1: Lock In Regression Tests

**Files:**
- Create: `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`
- Modify: existing tests only when an old assertion directly contradicts the approved spec.

- [ ] **Step 1: Add failing structural tests**

Add tests that assert:

- `_BatteryTable.cshtml` exists and is referenced by registry/admin/cluster-admin/passport pages.
- `BatteryTableService` exists and is registered.
- Users UI has `Access`, `Global Admin`, `Cluster Member`, and all approved cluster membership role labels.
- Cluster repository exposes unique cluster index creation and role normalization allows all approved membership roles.
- EditPassport renders policy-aware disabled/readonly markers for locked battery fields, cluster selector on battery creation, and software-version dropdown.
- Product editor has add/remove software-version rows.
- Summary/detail report use shared header classes, carbon colors, and telemetry chart wrappers.
- Token credential modal appears on both token pages.

- [ ] **Step 2: Run tests to verify failures**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter AdminTestFeedbackFixesTests
```

Expected: new tests fail because the shared service/partial, modal, per-model software rows, and enforcement markers are not implemented yet.

---

### Task 2: Shared Battery Table And Search

**Files:**
- Modify: `web/Models/ViewModels/BatteryViewModels.cs`
- Create: `web/Services/BatteryTableService.cs`
- Modify: `web/Program.cs`
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Create: `web/Views/Shared/_BatteryTable.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Admin/Passports.cshtml`
- Modify: `web/Views/ClusterAdmin/Passports.cshtml`
- Modify: `web/Views/Admin/BatteryPassports.cshtml`

- [ ] **Step 1: Implement shared view models**

Add `BatteryTablePageViewModel`, `BatteryTableRowActionViewModel`, and row URL/action properties to `BatterySummaryViewModel`.

- [ ] **Step 2: Implement `BatteryTableService`**

Move shared row building and search behavior into a service. The service must:

- Build rows from battery documents and visible passport history.
- Resolve cluster labels from the cluster collection.
- Redirect global-admin battery ID/serial searches to `/admin/batteries/{batteryId}/passports`.
- Keep cluster-admin battery searches on the existing local behavior.
- Redirect anonymous sample search to the latest public summary through the existing public route behavior.
- Restrict cluster search to global admins and return an access message for other roles.
- Set role-aware action URLs and booleans on every row.

- [ ] **Step 3: Replace duplicated Razor tables**

Render `_BatteryTable.cshtml` from the four page shells. The shared partial should include Battery ID, Battery Family, Battery Model, Battery serial number, Cluster, Passports, Latest passport status, New passport needed, Updated, and role-aware action icons.

- [ ] **Step 4: Preserve history return URL**

Add a `returnUrl` query parameter for history links opened from registry and use it for the history page back button.

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "AdminTestFeedbackFixesTests|BatteryRouteAndRegistryTests|BatteryAdminWorkflowTests"
```

Expected: shared table/search tests pass or reveal only task-local issues.

---

### Task 3: Users, Roles, Clusters, Reset, And Tokens

**Files:**
- Modify: `web/Services/AccessControlService.cs`
- Modify: `web/Services/ClusterRepository.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Users.cshtml`
- Modify: `web/Views/ClusterAdmin/ApiTokens.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Expand cluster membership role normalization**

Normalize global account access to only `admin` or `member`. Normalize cluster membership roles to allow `clusterAdmin`, `member`, `notifiedBody`, `marketSurveillanceAuthority`, `commission`, and `legitimateInterest`.

- [ ] **Step 2: Enforce cluster ID uniqueness**

Normalize cluster IDs on create/update, add a unique index for `clusters.clusterId`, and handle duplicate key exceptions with `Cluster ID already exists.`

- [ ] **Step 3: Implement full reset**

Ensure reset clears and reseeds users, memberships, clusters, API tokens, products/product versions/software versions, editable-field policy, batteries, passports, audit/supporting data, and telemetry. Seed only clustered batteries.

- [ ] **Step 4: Add token credential modal**

Replace inline generated credential alerts with a reusable modal on global and cluster token pages. `Copy` copies and stays open; `Okay` copies and closes.

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "AdminTestFeedbackFixesTests|AdminDenseConsoleLayoutTests|AdminCredentialLayoutTests|ProductTemplateServiceTests"
```

Expected: role, cluster, reset, and token tests pass or expose task-local failures.

---

### Task 4: Editable Fields And Battery Create/Edit Enforcement

**Files:**
- Modify: `web/Services/EditableFieldPolicyService.cs`
- Modify: `web/Services/BatteryPassportDeltaService.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Views/Admin/EditPassport.cshtml`
- Modify: `web/Views/ClusterAdmin/EditPassport.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Complete editable field path mapping**

Add mappings for battery mass/material/performance/carbon/circularity fields that can appear in the editable policy, while excluding telemetry/operational fields from passport-affecting diffs.

- [ ] **Step 2: Render locked fields visibly disabled**

Create helper logic in the edit view to determine create/edit/local-admin editability. Locked fields stay visible with disabled/readonly styling and a hidden original value only where needed for model binding.

- [ ] **Step 3: Enforce policy server-side**

On create, accept only approved creation fields. On edit, apply only fields allowed by the policy. Treat telemetry/operational/location/contact/isActive fields as always allowed and excluded from new-passport-needed.

- [ ] **Step 4: Preserve form state on creation validation**

When cluster or required creation fields are missing, return the edit view with user-entered values instead of clearing the form.

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "AdminTestFeedbackFixesTests|LocalAdminEditableFieldPolicyTests|BatteryAdminWorkflowTests"
```

Expected: editable-field and create/edit tests pass or expose task-local failures.

---

### Task 5: Per-Model Software Versions

**Files:**
- Modify: `web/Services/ProductTemplateModels.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Services/BatteryTemplateUpdateService.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ExternalApiController.cs`
- Modify: `web/Views/Admin/Product.cshtml`
- Modify: `web/Views/Admin/EditPassport.cshtml`
- Modify: `web/Views/Help/Index.cshtml`

- [ ] **Step 1: Extend product-version model**

Add `SoftwareVersions` collection to `BatteryProductVersion`, with a compatibility `SoftwareVersion` property returning the highest software version string row.

- [ ] **Step 2: Persist software-version rows**

Serialize/deserialize software-version arrays in product version documents. Migrate existing single software fields into one row when no array exists.

- [ ] **Step 3: Update product editor UI**

Render `Software version | Release date | Latest update | Actions` rows with add/remove controls for the selected Battery Model.

- [ ] **Step 4: Update battery create/edit/API selection**

Use a dropdown of allowed software versions. Selecting a version copies release/latest update. API patch validates against the same list and rejects undefined values.

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "AdminTestFeedbackFixesTests|ExternalApiBatteryVersionTests|ProductTemplateServiceTests|ProductTemplateWorkflowLayoutTests"
```

Expected: software-version model/UI/API tests pass or expose task-local failures.

---

### Task 6: Reports, Carbon Colors, And Telemetry Chart Bounds

**Files:**
- Modify: `web/Services/PassportViewModelFactory.cs`
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Add carbon stage colors**

Assign deterministic colors to carbon lifecycle stages in `PassportViewModelFactory`.

- [ ] **Step 2: Share the report header layout**

Align detail header markup with the summary fixed-header structure and responsive media/QR behavior.

- [ ] **Step 3: Fix responsive report CSS**

Constrain IDs, cards, QR panels, image panels, and recycled-content grids so they wrap/collapse without overlap or clipping.

- [ ] **Step 4: Bound telemetry charts**

Wrap each canvas in a fixed-height chart frame, keep `maintainAspectRatio: false`, and set a finite container height rather than styling only `minHeight`.

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "AdminTestFeedbackFixesTests|AdminFeedbackFollowupTests|QrWorkflowTests|PassportSoftwarePresentationTests"
```

Expected: report/layout/chart tests pass or expose task-local failures.

---

### Task 7: Final Verification And Browser Smoke

**Files:**
- Modify docs only if manual-test steps need updating after implementation.

- [ ] **Step 1: Run full test suite**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 2: Start local app**

Run:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run --project web/BatteryPassWeb.csproj --urls "http://localhost:5099" --no-launch-profile
```

Expected: app serves on `http://localhost:5099`.

- [ ] **Step 3: Browser smoke**

Check:

- Summary/detail at desktop and narrow widths.
- Anonymous sample search from `/`.
- `/registry`.
- `/admin/clusters?tab=batteries`.
- `/admin/passports`.
- `/cluster-admin/passports`.
- Users, editable fields, battery create/edit, battery family software versions, reset, token modal, and Performance telemetry tab.

- [ ] **Step 4: Commit implementation**

Commit after tests and browser smoke:

```powershell
git add -- web BatteryPassWeb.Tests docs
git commit -m "fix: address admin test feedback"
```
