# Battery Admin Flow Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved approach 2 battery-first admin flow polish.

**Architecture:** Keep the existing ASP.NET Core MVC structure. Add battery edit/history view models and admin routes around the existing battery repository, reuse the existing passport snapshot service, and keep passport pages for report/trust actions. Track the “new passport needed” state directly on battery documents and clear it when a new passport snapshot is created.

**Tech Stack:** ASP.NET Core MVC, Razor views, MongoDB BSON documents/repositories, xUnit source/layout tests and focused runtime-friendly controller/service tests where practical.

---

### Task 1: Admin Battery List Layout

**Files:**
- Modify: `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Modify: `web/Models/ViewModels/BatteryViewModels.cs`
- Modify: `web/Services/BatteryRepository.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`

- [ ] **Step 1: Write failing tests**

Add tests asserting:

```csharp
[Fact]
public void AdminBatteryList_ShouldUseCompactBatteryRowsWithoutEmbeddedHistory()
{
    var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
    var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs"));

    Assert.Contains("@row.PassportCount</td>", view);
    Assert.DoesNotContain("@row.PassportCount passports", view);
    Assert.DoesNotContain("bp-passport-history-row", view);
    Assert.DoesNotContain("data-battery-passport-history", view);
    Assert.Contains("/admin/batteries/@Uri.EscapeDataString(row.BatteryId)/passports", view);
    Assert.Contains("NewPassportRequired", model);
}
```

- [ ] **Step 2: Run the focused test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter AdminBatteryList_ShouldUseCompactBatteryRowsWithoutEmbeddedHistory
```

Expected: FAIL because the embedded rows and `passports` text still exist.

- [ ] **Step 3: Implement compact rows**

Add `NewPassportRequired` to `BatterySummaryViewModel`. Populate it in `BatteryRepository.ToSummary` from `app.snapshot.newPassportRequired`.

Update `web/Views/Admin/Clusters.cshtml`:

- passport count cell renders only `@row.PassportCount`
- Battery ID cell uses nowrap styling/class
- remove embedded passport history `<tr>`
- history icon links to `/admin/batteries/{batteryId}/passports`
- edit icon links to `/admin/batteries/{batteryId}/edit`
- create passport action is icon-style or compact
- show persistent `New passport needed` when `row.NewPassportRequired` is true

- [ ] **Step 4: Run focused test and commit**

Run the focused test again. Expected: PASS.

Commit:

```powershell
git add BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs web/Models/ViewModels/BatteryViewModels.cs web/Services/BatteryRepository.cs web/Views/Admin/Clusters.cshtml
git commit -m "feat: compact admin battery rows"
```

### Task 2: Dedicated Admin Passport History Page

**Files:**
- Modify: `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Models/ViewModels/BatteryViewModels.cs`
- Create: `web/Views/Admin/BatteryPassports.cshtml`

- [ ] **Step 1: Write failing tests**

Add tests asserting the admin history route and view exist:

```csharp
[Fact]
public void AdminBatteryPassportHistory_ShouldHaveDedicatedRouteAndIconActions()
{
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
    var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));

    Assert.Contains("[HttpGet(\"batteries/{batteryId}/passports\")]", controller);
    Assert.Contains("BatteryPassports", controller);
    Assert.Contains("Back to Batteries", view);
    Assert.Contains("/admin/clusters?tab=batteries", view);
    Assert.Contains("Summary report", view);
    Assert.Contains("Detailed report", view);
    Assert.Contains("Conformance", view);
    Assert.Contains("Audit trail", view);
    Assert.Contains("Archive", view);
    Assert.Contains("Unarchive", view);
}
```

- [ ] **Step 2: Run focused test**

Expected: FAIL because the page does not exist.

- [ ] **Step 3: Implement route and view**

Add a `BatteryPassportHistoryPageViewModel` with `Battery`, `StatusMessage`, and `ErrorMessage`.

Add `AdminController.BatteryPassports(string batteryId, ...)` to load the battery, cluster label, and all passport rows, then return `View("BatteryPassports", model)`.

Create `web/Views/Admin/BatteryPassports.cshtml` with:

- Back to Batteries button
- battery identity header
- create passport form
- wide table of passport history rows
- icon-only actions with titles for summary, detail, conformance, audit, archive/unarchive

- [ ] **Step 4: Run focused test and commit**

Run focused test. Expected: PASS.

Commit:

```powershell
git add BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs web/Controllers/AdminController.cs web/Models/ViewModels/BatteryViewModels.cs web/Views/Admin/BatteryPassports.cshtml
git commit -m "feat: add admin battery passport history"
```

### Task 3: Battery Edit Flow And Pending Snapshot State

**Files:**
- Modify: `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Models/ViewModels/EditPassportViewModel.cs`
- Modify: `web/Views/Admin/EditPassport.cshtml`
- Modify: `web/Services/BatteryRepository.cs`

- [ ] **Step 1: Write failing tests**

Add source/layout tests asserting:

```csharp
[Fact]
public void AdminBatteryEdit_ShouldEditBatteryAndReturnToBatteryListWithPendingSnapshot()
{
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
    var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

    Assert.Contains("[HttpGet(\"batteries/{batteryId}/edit\")]", controller);
    Assert.Contains("[HttpPost(\"batteries/{batteryId}/save\")]", controller);
    Assert.Contains("SaveBattery", controller);
    Assert.Contains("app.snapshot.newPassportRequired", controller);
    Assert.Contains("/admin/clusters?tab=batteries", controller);
    Assert.Contains("readonly", view);
    Assert.Contains("New passport", view);
}

[Fact]
public void BatteryCreate_ShouldDefaultModelToLatestFamilyModel()
{
    var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

    Assert.Contains("data-latest-product-version", view);
    Assert.Contains("selectLatestProductVersion", view);
}
```

- [ ] **Step 2: Run focused tests**

Expected: FAIL because battery edit routes and latest default behavior are missing.

- [ ] **Step 3: Implement battery edit mode**

Add `Mode = "battery-edit"` support in `EditPassportViewModel`/`EditPassport.cshtml`.

Add:

- `GET /admin/batteries/{batteryId}/edit`
- `POST /admin/batteries/{batteryId}/save`

The GET builds the existing edit model from the battery document. The POST applies editable form fields to the battery document, keeps locked identity fields unchanged, sets:

```csharp
["app.snapshot.newPassportRequired"] = true
["app.snapshot.requiredSince"] = now
["app.snapshot.reason"] = "battery-data-updated"
```

and redirects to `/admin/clusters?tab=batteries`.

In creation mode, change the product-family JavaScript to select that family's latest model by default when family changes.

- [ ] **Step 4: Clear pending flag on passport creation**

In `CreateBatteryPassport`, after snapshot creation, update the battery to clear:

```csharp
app.snapshot.newPassportRequired = false
app.snapshot.lastPassportCreatedAt = now
app.snapshot.latestPassportId = passportId
```

- [ ] **Step 5: Run focused tests and commit**

Run focused tests. Expected: PASS.

Commit:

```powershell
git add BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs web/Controllers/AdminController.cs web/Models/ViewModels/EditPassportViewModel.cs web/Views/Admin/EditPassport.cshtml web/Services/BatteryRepository.cs
git commit -m "feat: edit batteries before passport snapshots"
```

### Task 4: Report Header And Historical Latest Icon

**Files:**
- Modify: `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`

- [ ] **Step 1: Write failing tests**

Add a test asserting summary/detail use serial number as the bold top identity and historical latest action is icon-only:

```csharp
[Fact]
public void PassportReports_ShouldUseSerialHeaderAndIconLatestAction()
{
    var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
    var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
    var combined = summary + detail;

    Assert.Contains("passport.BatterySerialNumber", summary);
    Assert.Contains("passport.BatterySerialNumber", detail);
    Assert.DoesNotContain(">Open latest passport<", combined);
    Assert.Contains("aria-label=\"Open latest passport\"", combined);
}
```

- [ ] **Step 2: Run focused test**

Expected: FAIL because the text button still exists.

- [ ] **Step 3: Implement view polish**

Replace the bold top display of Battery Family with Battery serial number. Replace text latest-passport button with an icon-only anchor that has `aria-label`, `title`, and tooltip text.

- [ ] **Step 4: Run focused test and commit**

Run focused test. Expected: PASS.

Commit:

```powershell
git add BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs web/Views/Passport/Summary.cshtml web/Views/Passport/Detail.cshtml
git commit -m "feat: polish passport report identity header"
```

### Task 5: Registry Access Bug

**Files:**
- Modify: `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Modify: `web/Controllers/RegistryController.cs`
- Modify as needed: `web/Services/AccessControlService.cs`

- [ ] **Step 1: Investigate root cause**

Confirm how `north.user@example.test` is seeded, which cluster memberships it has, and why published North passports are filtered out.

- [ ] **Step 2: Write failing test**

Add a source/layout or service-level regression test that proves `RegistryController` uses cluster-visible published passports for signed-in regular users and does not require admin status.

- [ ] **Step 3: Run focused test**

Expected: FAIL against current code.

- [ ] **Step 4: Implement minimal fix**

Fix the permission/filtering path so signed-in regular users see signed/published batteries for their cluster while public users remain blocked by `[Authorize]`.

- [ ] **Step 5: Run focused test and commit**

Run focused test. Expected: PASS.

Commit:

```powershell
git add BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs web/Controllers/RegistryController.cs web/Services/AccessControlService.cs
git commit -m "fix: show cluster registry batteries to members"
```

### Task 6: Fixed API Demo Tokens In Reset And Help

**Files:**
- Modify: `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`
- Modify: `BatteryPassWeb.Tests/ExternalApiInitializerTests.cs`
- Modify: `web/Models/ViewModels/ExternalApiHelpViewModel.cs`
- Modify: `web/Services/ExternalApiInitializer.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Controllers/HelpController.cs`
- Modify: `web/Views/Help/Index.cshtml`

- [ ] **Step 1: Write failing tests**

Add tests asserting:

- a fixed sign token constant exists
- reset creates read, read-write, and sign tokens for the sample cluster battery
- help model/view exposes sample sign token

- [ ] **Step 2: Run focused tests**

Expected: FAIL because sign token help/reset support is missing.

- [ ] **Step 3: Implement fixed demo sign token**

Add constants to `ExternalApiInitializer`:

```csharp
SampleSignTokenId
SampleSignTokenValue
```

Ensure initialization/reset creates read, read-write, and sign tokens with fixed values. Scope the help sample to the stable demo cluster battery.

Add `SampleSignToken` to `ExternalApiHelpViewModel`, `HelpController`, and `Help/Index.cshtml`.

- [ ] **Step 4: Run focused tests and commit**

Run focused tests. Expected: PASS.

Commit:

```powershell
git add BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs BatteryPassWeb.Tests/ExternalApiInitializerTests.cs web/Models/ViewModels/ExternalApiHelpViewModel.cs web/Services/ExternalApiInitializer.cs web/Services/ProductTemplateService.cs web/Controllers/HelpController.cs web/Views/Help/Index.cshtml
git commit -m "feat: seed fixed api demo tokens"
```

### Task 7: Full Verification

**Files:**
- Modify tests if legitimate compile/source assertion drift is found.

- [ ] **Step 1: Stop running app on port 5186**

Run:

```powershell
Get-NetTCPConnection -LocalPort 5186 -ErrorAction SilentlyContinue | Select-Object OwningProcess
Stop-Process -Id <pid> -Force
```

- [ ] **Step 2: Run full suite**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 3: Start app and manual smoke test**

Start:

```powershell
dotnet run --project web/BatteryPassWeb.csproj --urls http://localhost:5186
```

Smoke test:

- `/admin/clusters?tab=batteries`
- `/admin/batteries/{batteryId}/passports`
- `/admin/batteries/{batteryId}/edit`
- `/registry` as `north.user@example.test`
- `/help` sample token display

- [ ] **Step 4: Final commit if needed**

Commit any verification fixes with a focused message.
