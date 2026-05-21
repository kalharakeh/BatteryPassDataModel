# Passport Admin Followup Feedback Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the latest manual-testing feedback around QR/report layout, shared battery search tables, user/cluster UX, editable battery fields, passport snapshot creation, software versions, template push scope, and telemetry chart sizing.

**Architecture:** Keep the existing ASP.NET Core MVC/Razor/MongoDB shape. Tighten the shared services and partials that already exist, then patch the few page-specific gaps where the user experience still diverges.

**Tech Stack:** ASP.NET Core MVC, Razor, MongoDB.Driver/MongoDB.Bson, xUnit, vanilla JavaScript, CSS, Playwright CLI for browser smoke verification.

---

## File Structure

- Modify `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs` for structural regression tests covering report layout, user row state, table compactness, password reveal, and token/search UI consistency.
- Modify `BatteryPassWeb.Tests/QrWorkflowTests.cs` for QR public access and three-column header expectations.
- Modify `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs` for battery creation, serial uniqueness, snapshot-only passport creation, and status/new-passport-needed behavior.
- Modify `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs` and `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs` for software-version row and exact family-model push scope.
- Modify `web/Controllers/QrController.cs`, `web/Services/PassportQrCodeService.cs`, `web/Views/Passport/Summary.cshtml`, `web/Views/Passport/Detail.cshtml`, and `web/wwwroot/css/site.css` for QR, header, responsive, carbon chart, and telemetry chart fixes.
- Modify `web/Controllers/AdminController.cs`, `web/Controllers/ClusterAdminController.cs`, `web/Services/BatteryRepository.cs`, `web/Services/BatteryPassportDeltaService.cs`, `web/Services/EditableFieldPolicyService.cs`, and `web/Services/BatteryPassportSnapshotService.cs` for battery create/edit, snapshot creation, and new-passport-needed rules.
- Modify `web/Services/BatteryTableService.cs`, `web/Views/Shared/_BatteryTable.cshtml`, `web/Views/Registry/Index.cshtml`, `web/Views/Admin/Clusters.cshtml`, and `web/Views/Admin/BatteryPassports.cshtml` for shared search/table layout and return destinations.
- Modify `web/Services/ClusterRepository.cs`, `web/Views/Admin/Clusters.cshtml`, `web/Views/ClusterAdmin/Users.cshtml`, and `web/wwwroot/css/site.css` for duplicate cluster feedback, access default, row persistence, and password reveal controls.
- Modify `web/Views/Admin/Product.cshtml`, `web/Services/ProductTemplateService.cs`, `web/Services/ProductTemplateModels.cs`, and related product view models for software-version add/save/remove and model-scoped push behavior.

---

### Task 1: Add Focused Regression Tests

**Files:**
- Modify: `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Modify: `BatteryPassWeb.Tests/QrWorkflowTests.cs`
- Modify: `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`
- Modify: `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`
- Modify: `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`

- [ ] **Step 1: Add report and QR layout assertions**

Add tests that assert:

```csharp
Assert.Contains("bp-report-identity-grid", summary);
Assert.Contains("bp-report-identity-panel--battery", summary);
Assert.Contains("bp-report-identity-panel--identifiers", summary);
Assert.Contains("bp-report-identity-panel--qr", summary);
Assert.DoesNotContain("bp-report-media-code-row", summary);
Assert.Contains("CultureInfo.InvariantCulture", summary);
Assert.Contains("CultureInfo.InvariantCulture", detail);
Assert.Contains("GetLatestPublicByBatteryIdAsync", qrController);
Assert.Contains("image/png", qrController);
```

- [ ] **Step 2: Add shared table/search assertions**

Add tests that assert `_BatteryTable.cshtml` renders columns in this order: `Battery serial number`, `Battery ID`, `Family`, `Model`, `Cluster`, `Passports`, `Status`, `Updated`, `Actions`. Assert `New passport needed` is inside the status cell and not rendered as a separate action badge.

- [ ] **Step 3: Add user/cluster UX assertions**

Add tests that assert:

```csharp
Assert.Contains("value=\"member\" selected", clusters);
Assert.Contains("data-open-user", clusters);
Assert.Contains("data-open-user", clusterUsers);
Assert.Contains("::-ms-reveal", css);
Assert.Contains("Cluster ID already exists.", adminController);
Assert.Contains("TempData[\"ClusterCreateName\"]", adminController);
Assert.Contains("TempData[\"ClusterCreateId\"]", adminController);
```

- [ ] **Step 4: Add battery rule assertions**

Add tests that assert:

```csharp
Assert.Contains("Battery cluster is required.", adminController);
Assert.Contains("Battery serial number already exists.", adminController);
Assert.Contains("GetBySerialNumberAsync", adminController);
Assert.Contains("CreatePassportSnapshotAsync", adminController);
Assert.DoesNotContain("/edit\"", createPassportActionSnippet);
Assert.Contains("UpdateNewPassportRequiredAsync", adminController);
Assert.Contains("NumericBsonEquals", deltaService);
```

- [ ] **Step 5: Add software-version and push-scope assertions**

Add tests that assert product software rows include pending-save behavior:

```csharp
Assert.Contains("data-software-version-pending", productView);
Assert.Contains("data-save-software-version", productView);
Assert.Contains("incrementSoftwareVersion", productView);
Assert.Contains("new Date().toISOString().slice(0, 10)", productView);
Assert.Contains("readonly", productView);
Assert.Contains("data-remove-software-version", productView);
```

Add service tests that push a change to `Compact 13M` model `2.0` and assert only batteries whose `app.product.productId` and `app.product.productVersion` match are changed or marked as requiring a new passport.

- [ ] **Step 6: Run the focused tests and confirm failures**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "AdminFeedbackFollowupTests|QrWorkflowTests|BatteryAdminWorkflowTests|ProductTemplateWorkflowLayoutTests|ProductTemplateServiceTests"
```

Expected: failures point to the missing follow-up fixes, not compile errors.

---

### Task 2: Fix QR, Report Header, Carbon Charts, And Telemetry Sizing

**Files:**
- Modify: `web/Controllers/QrController.cs`
- Modify: `web/Services/PassportQrCodeService.cs`
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Fix public QR access**

Update `QrController.CanAccessQrAsync` so public QR rendering succeeds when a battery has a latest public passport, including sample/demo batteries reached anonymously from the landing page. Keep authenticated cluster/global admin fallback access.

- [ ] **Step 2: Make the QR payload open the latest public summary**

Ensure `PassportQrCodeService.BuildPayloadUrl` generates the public latest-battery summary URL for battery IDs. The payload should not point to a private/admin route or a raw battery ID path that can 404 anonymously.

- [ ] **Step 3: Move summary/detail headers to the same three-column identity grid**

Use the same Razor structure in `Summary.cshtml` and `Detail.cshtml`:

```html
<article class="bp-report-identity-grid">
  <section class="bp-report-identity-panel bp-report-identity-panel--battery">...</section>
  <section class="bp-report-identity-panel bp-report-identity-panel--identifiers">...</section>
  <section class="bp-report-identity-panel bp-report-identity-panel--qr">...</section>
</article>
```

The battery image remains in the media area. The QR code lives in the third identity panel with its download link and must not be placed outside the header grid.

- [ ] **Step 4: Fix intermediate responsive widths**

Replace fragile width rules with `grid-template-columns: minmax(220px, 1fr) minmax(280px, 1.2fr) minmax(160px, 220px)` at desktop widths, collapse to two columns at medium widths, then one column below phone width. Set `min-width: 0` on ID/value containers and image/QR panels.

- [ ] **Step 5: Fix carbon pie colors**

Format all `conic-gradient(...)` percentages with `CultureInfo.InvariantCulture` so decimal values render with `.` instead of locale commas. Keep table legends with values for carbon footprint and recycled content.

- [ ] **Step 6: Normalize telemetry chart panels**

Give every performance chart the same wrapper class and fixed responsive dimensions. Set Chart.js options to `responsive: true`, `maintainAspectRatio: false`, and render every chart inside the same CSS grid/card structure.

- [ ] **Step 7: Run tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "QrWorkflowTests|AdminFeedbackFollowupTests"
```

Expected: QR/report/chart structural tests pass.

---

### Task 3: Fix Cluster/User UX Regressions

**Files:**
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Services/ClusterRepository.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Users.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Preserve duplicate-cluster form values**

When duplicate cluster creation is blocked, set:

```csharp
TempData["ErrorMessage"] = "Cluster ID already exists.";
TempData["ClusterCreateName"] = name;
TempData["ClusterCreateId"] = clusterId;
```

Redirect back to `/admin/clusters?tab=clusters` and render the previous name/ID values in the create form.

- [ ] **Step 2: Hide native browser password reveal buttons**

Keep the custom outside reveal button and suppress native Edge/IE reveal controls:

```css
.bp-password-field input::-ms-reveal,
.bp-password-field input::-ms-clear {
  display: none;
}
```

- [ ] **Step 3: Default new users to Cluster Member**

Render the `systemRole` select with `member` selected by default and `admin` available only when deliberately chosen.

- [ ] **Step 4: Keep edited user rows expanded**

After saving user details, password changes, or cluster membership changes, redirect with `openUser=<email>`. In both admin and cluster-admin users pages, open the matching row/details element and scroll it into view without resetting the table.

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "AdminFeedbackFollowupTests|AdminDenseConsoleLayoutTests|AdminCredentialLayoutTests"
```

Expected: user/cluster UX tests pass.

---

### Task 4: Fix Editable Battery Data, Create Battery, And Snapshot Creation

**Files:**
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Services/BatteryRepository.cs`
- Modify: `web/Services/EditableFieldPolicyService.cs`
- Modify: `web/Services/BatteryPassportDeltaService.cs`
- Modify: `web/Services/BatteryPassportSnapshotService.cs`
- Modify: `web/Views/Admin/EditPassport.cshtml`
- Modify: `web/Views/ClusterAdmin/EditPassport.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Add exact serial-number uniqueness**

Add `BatteryRepository.GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken)` and use it during battery creation. If any existing battery has the same `identity.serialNumber`, return the create form with `Battery serial number already exists.`

- [ ] **Step 2: Mark cluster as required and align create fields**

In the battery create view, render the cluster selector as required, align all select/input controls using a consistent grid row layout, and keep helper text from changing input baseline alignment.

- [ ] **Step 3: Lock non-creation fields on new battery**

During battery creation, only enable:

- Battery Family
- Battery Model
- Battery serial number
- Manufactured date
- Facility ID
- Manufactured by
- Software version
- Cluster

All other fields stay visible as disabled gray read-only fields populated from the selected family-model template.

- [ ] **Step 4: Save editable values exactly**

Change the battery save flow so allowed fields from the form are applied after model/software template updates. Values such as `material.nickelMass = 5163` must be stored and reloaded as `5163`, not scaled or overwritten by the family-model baseline.

- [ ] **Step 5: Compare current battery data against latest passport data**

Update `BatteryPassportDeltaService` to compare numeric BSON values numerically and strings case-insensitively. Exclude telemetry/operational fields. When the edited value is reverted to match the latest passport, clear `app.snapshot.newPassportRequired`.

- [ ] **Step 6: Make passport creation snapshot-only**

After `CreatePassportSnapshotAsync`, redirect to the conformance/workflow page or the battery history page, not the editable passport data form. Creating a passport should assign a new passport ID and freeze a snapshot of current battery data; the validation/sign/publish workflow comes afterward.

- [ ] **Step 7: Prevent Draft plus New passport needed together**

If a latest draft snapshot already represents the current battery data, status remains Draft/Awaiting sign-off and `New passport needed` is false. In the table, when new passport is needed, show that text in the `Status` cell instead of adding a separate badge.

- [ ] **Step 8: Run tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "BatteryAdminWorkflowTests|LocalAdminEditableFieldPolicyTests|AdminFeedbackFollowupTests|PassportStatusLabelTests"
```

Expected: create/edit/snapshot/status tests pass.

---

### Task 5: Compact And Unify Battery Table Search

**Files:**
- Modify: `web/Services/BatteryTableService.cs`
- Modify: `web/Views/Shared/_BatteryTable.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Admin/BatteryPassports.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Stop table searches from redirecting**

Change shared table search so battery serial, battery ID, passport ID, cluster ID, and cluster name all filter the displayed rows. Passport ID search should display the battery row that owns that passport.

- [ ] **Step 2: Keep cluster search global-admin-only**

For non-global-admin users, cluster search returns a visible access message instead of listing batteries.

- [ ] **Step 3: Compact table columns**

Render columns as:

`Battery serial number | Battery ID | Family | Model | Cluster | Passports | Status | Updated | Actions`

Use truncated battery IDs with copy buttons. Rename `Battery Model` to `Model` and `Latest passport status` to `Status`.

- [ ] **Step 4: Make `/registry` and `/admin/clusters?tab=batteries` controls match**

Use the same search form markup and shared table partial in both page shells. Keep the URLs and surrounding page chrome unchanged.

- [ ] **Step 5: Fix history return URL**

When opening battery history from `/registry`, set `returnUrl=/registry` and have the back button return there.

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "BatteryRouteAndRegistryTests|BatteryAdminWorkflowTests|AdminFeedbackFollowupTests"
```

Expected: shared table/search tests pass.

---

### Task 6: Fix Software-Version Rows And Template Push Scope

**Files:**
- Modify: `web/Views/Admin/Product.cshtml`
- Modify: `web/Services/ProductTemplateModels.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Services/BatteryTemplateUpdateService.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Render saved software rows as read-only**

Saved software-version rows show version, release date, and latest update as read-only fields. The action button is an icon-only remove button aligned on the same row.

- [ ] **Step 2: Add pending software rows with defaults**

When `Add software version` is pressed, create one pending row with:

- version = incremented highest version for that family-model
- release date = today
- latest update = today

Use `new Date().toISOString().slice(0, 10)` for the default dates.

- [ ] **Step 3: Save pending row before it becomes removable**

Pending rows show a save icon button, not remove. The hidden product-version JSON should include the row only after the row save button is pressed. After save, make the row read-only and swap the save icon to remove.

- [ ] **Step 4: Increment the highest numeric version**

Implement `incrementSoftwareVersion` for dotted numeric versions by incrementing the last numeric segment, for example `1.0` to `1.1` and `2.4.9` to `2.4.10`. For non-numeric strings, append `-1`.

- [ ] **Step 5: Enforce exact family-model push scope**

In `PushProductVersionAsync`, update only batteries where both `app.product.productId` and `app.product.productVersion` match the selected product and model. Keep `newPassportRequired` recalculation scoped to only those changed batteries.

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "ProductTemplateWorkflowLayoutTests|ProductTemplateServiceTests|ExternalApiBatteryVersionTests"
```

Expected: software row and push-scope tests pass.

---

### Task 7: Browser Verification And Full Test Run

**Files:**
- No production file edits unless verification exposes a bug.

- [ ] **Step 1: Run the full automated suite**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj
```

Expected: all tests pass. Existing SharpCompress NU1902 warnings may appear and are not part of this fix.

- [ ] **Step 2: Start the app locally**

Run:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project web/BatteryPassWeb.csproj --urls "http://localhost:5099" --no-launch-profile
```

Expected: app starts and connects using the local development configuration.

- [ ] **Step 3: Browser-smoke the feedback points**

Use Playwright/browser verification for:

- Anonymous landing-page sample search opens public summary.
- Summary QR image renders and QR download returns `image/png`.
- Summary and detail headers hold three fixed panels at wide, mid, and narrow widths.
- Carbon charts show multiple colored sections and value legends.
- Performance tab charts have matching widths and bounded height.
- Duplicate cluster ID shows a message and preserves typed name/ID.
- User membership edits keep the edited user row open.
- Battery create requires cluster and rejects duplicate serial.
- Editable material mass persists exactly and clears new-passport-needed when reverted to latest passport value.
- Registry and admin batteries tables share compact columns/search behavior.
- Software-version add/save/remove behaves as designed.

- [ ] **Step 4: Update manual test documentation only if flow text changed**

If redirects or labels changed from the current guide, update `docs/passport-admin-registry-manual-test-flow.md` with the corrected manual steps.

---

## Coverage Check

- Points 1-3 are covered by Task 2.
- Point 4 is covered by Task 2, Step 5.
- Point 5 from the latest list is covered by Task 2, Steps 1-2.
- Points 3 duplicate, 4, 5, and 6 around cluster/user/password UX are covered by Task 3.
- Point 7 is covered by Task 4, Steps 4-5.
- Points 8, 11, and 12 are covered by Task 5.
- Point 9 is covered by Task 4, Steps 1-3.
- Point 10 is covered by Task 4, Step 6.
- Points 13 and 14 are covered by Task 6, Steps 1-4.
- Point 15 is covered by Task 6, Step 5.
- Point 16 is covered by Task 4, Step 7.
- Point 17 is covered by Task 2, Step 6.

No approved feedback item is intentionally left out of scope.
