# Passport Admin UX Guardrails Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clarify passport lifecycle/status language, make admin archive and cluster deletion recoverable, add account self-service, and protect local-admin role management.

**Architecture:** Keep the existing MVC structure. Add display-only state and role helpers at the service/view-model layer, then update controllers and Razor views to use those helpers with focused guardrails. Persistence changes stay in `PassportRepository` and `ClusterRepository`.

**Tech Stack:** ASP.NET Core MVC on .NET 10, Razor views, MongoDB BSON documents, cookie authentication, xUnit source/behavior tests.

---

## File Structure

- Modify `web/Models/ViewModels/PassportViewModel.cs`: add `PassportState`.
- Modify `web/Models/ViewModels/PassportSummaryViewModel.cs`: add `PassportState`.
- Modify `web/Models/ViewModels/AdminClusterViewModel.cs`: add linked-data counts to clusters and self-row flag to users.
- Create `web/Models/ViewModels/AccountProfileViewModel.cs`: account edit form state.
- Modify `web/Services/PassportViewModelFactory.cs`: compute display-only passport lifecycle state.
- Modify `web/Services/PassportRepository.cs`: compute summary state, archive with previous status, unarchive, count cluster-linked passports.
- Modify `web/Services/ClusterRepository.cs`: count memberships, force-safe email/profile updates, membership email migration.
- Modify `web/Services/AccessControlService.cs`: add role display helpers.
- Modify `web/Services/AuthService.cs`: expose principal creation from stored user data for session refresh.
- Modify `web/Controllers/AdminController.cs`: add unarchive and cluster deletion guardrails.
- Modify `web/Controllers/ClusterAdminController.cs`: block local-admin self role/membership changes.
- Create `web/Controllers/AccountController.cs`: authenticated self-service profile page.
- Modify `web/Views/Passport/Summary.cshtml`: show Passport state in hero and explicit field labels.
- Modify `web/Views/Passport/Detail.cshtml`: show Passport state in hero and explicit field labels; remove top image helper text.
- Modify `web/Views/Registry/Index.cshtml`: expose Summary and Detailed report actions.
- Modify `web/Views/ClusterAdmin/Passports.cshtml`: label passport lifecycle state clearly.
- Modify `web/Views/Admin/Passports.cshtml`: compact action controls and archive/unarchive.
- Modify `web/Views/Admin/Clusters.cshtml`: compact action controls, archive/unarchive, cluster delete/force-delete UI, role labels.
- Modify `web/Views/ClusterAdmin/Users.cshtml`: role labels, self-protection UI.
- Modify `web/Views/Shared/_Layout.cshtml`: show customer name and strongest role, add Account navigation.
- Create `web/Views/Account/Index.cshtml`: account profile form.
- Modify `web/wwwroot/css/site.css`: compact action buttons, state pills, account form/table layout.
- Create `BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs`: focused tests for the above behavior and markup.

---

### Task 1: Passport Lifecycle Display State

**Files:**
- Create: `BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs`
- Modify: `web/Models/ViewModels/PassportViewModel.cs`
- Modify: `web/Models/ViewModels/PassportSummaryViewModel.cs`
- Modify: `web/Services/PassportViewModelFactory.cs`
- Modify: `web/Services/PassportRepository.cs`

- [ ] **Step 1: Write the failing lifecycle tests**

Create `BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs` with this initial content:

```csharp
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportAdminUxGuardrailTests
{
    [Theory]
    [InlineData("archived", "signed", "Archived")]
    [InlineData("published", "signed", "Published")]
    [InlineData("draft", "signed", "Signed")]
    [InlineData("draft", "unvalidated", "Draft")]
    [InlineData("", "", "Draft")]
    public void PassportViewModelFactory_ShouldExposeSingleDisplayLifecycleState(
        string registryStatus,
        string trustState,
        string expectedState)
    {
        var document = MinimalPassportDocument(registryStatus, trustState);

        var model = new PassportViewModelFactory().Create(document);

        Assert.Equal(expectedState, model.PassportState);
    }

    [Fact]
    public void PassportRepository_ShouldExposeSummaryLifecycleStateFromSourceText()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
        var summaryModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportSummaryViewModel.cs"));

        Assert.Contains("PassportState", summaryModel);
        Assert.Contains("BuildPassportState", source);
        Assert.Contains("PassportState = BuildPassportState(document)", source);
    }

    private static BsonDocument MinimalPassportDocument(string registryStatus, string trustState)
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
                ["state"] = trustState
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["name"] = "Test Battery",
                    ["modelNumber"] = "CP7M",
                    ["serialNumber"] = "TEST-001",
                    ["manufacturerName"] = "Scania"
                },
                ["media"] = new BsonDocument(),
                ["documents"] = new BsonDocument(),
                ["notes"] = new BsonDocument(),
                ["operations"] = new BsonDocument(),
                ["product"] = new BsonDocument()
            },
            ["aspects"] = new BsonDocument()
        };
    }

    private static string RepoFile(params string[] parts)
    {
        var path = RepoPath(parts);
        if (File.Exists(path))
        {
            return path;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }

    private static string RepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (Directory.Exists(Path.Combine(directory.FullName, "web"))
                && Directory.Exists(Path.Combine(directory.FullName, "BatteryPassWeb.Tests")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(parts);
    }
}
```

- [ ] **Step 2: Run the lifecycle tests and verify red**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PassportAdminUxGuardrailTests" --no-restore
```

Expected: compile failure or test failure mentioning missing `PassportState`.

- [ ] **Step 3: Add lifecycle properties and factory computation**

In `web/Models/ViewModels/PassportViewModel.cs`, add this property near `RegistryStatus`:

```csharp
public string PassportState { get; init; } = "Draft";
```

In `web/Models/ViewModels/PassportSummaryViewModel.cs`, add this property near `RegistryStatus`:

```csharp
public string PassportState { get; init; } = "Draft";
```

In `web/Services/PassportViewModelFactory.cs`, set the property in the returned `PassportViewModel`:

```csharp
PassportState = BuildPassportState(document, trustState),
```

Add this private helper near the other private helpers:

```csharp
private static string BuildPassportState(BsonDocument document, string trustState)
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

In `web/Services/PassportRepository.cs`, set `PassportState` in `ToSummary`:

```csharp
PassportState = BuildPassportState(document),
```

Add this private helper near `ToSummary`:

```csharp
private static string BuildPassportState(BsonDocument document)
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

Add `using BatteryPassWeb.Models.Trust;` to `PassportRepository.cs` only if the file does not already include it.

- [ ] **Step 4: Run the lifecycle tests and verify green**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PassportAdminUxGuardrailTests" --no-restore
```

Expected: tests in `PassportAdminUxGuardrailTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs web/Models/ViewModels/PassportViewModel.cs web/Models/ViewModels/PassportSummaryViewModel.cs web/Services/PassportViewModelFactory.cs web/Services/PassportRepository.cs
git commit -m "Add passport lifecycle display state"
```

---

### Task 2: Passport Views And Registry Actions

**Files:**
- Modify: `BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs`
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `web/Views/Admin/Passports.cshtml`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Passports.cshtml`
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`

- [ ] **Step 1: Add failing markup tests**

Append these tests to `PassportAdminUxGuardrailTests`:

```csharp
[Fact]
public void PassportViews_ShouldSeparatePassportStateFromBatteryStatus()
{
    var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
    var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

    Assert.Contains("@passport.PassportState", summary);
    Assert.Contains("<dt>Passport state</dt>", summary);
    Assert.Contains("<dt>Battery status</dt>", summary);
    Assert.DoesNotContain("<dt>Status</dt><dd>@passport.BatteryStatus</dd>", summary);

    Assert.Contains("@passport.PassportState", detail);
    Assert.Contains("<dt>Passport state</dt>", detail);
    Assert.Contains("<dt>Battery status</dt>", detail);
    Assert.DoesNotContain("<p class=\"bp-help-text mt-2\">@passport.BatteryImageAlt</p>", detail);
}

[Fact]
public void Registry_ShouldExposeSummaryAndDetailedReportActions()
{
    var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));

    Assert.Contains("Passport state", registry);
    Assert.Contains("@row.PassportState", registry);
    Assert.Contains(">Summary<", registry);
    Assert.Contains(">Detailed report<", registry);
    Assert.Contains("/{Uri.EscapeDataString(row.PassportId)}/summary", registry);
    Assert.Contains("/{Uri.EscapeDataString(row.PassportId)}", registry);
    Assert.Contains("PassportState = passport.PassportState", controller);
}

[Fact]
public void PassportListViews_ShouldShowPassportStateInsteadOfAmbiguousStatus()
{
    var adminPassports = File.ReadAllText(RepoFile("web", "Views", "Admin", "Passports.cshtml"));
    var adminClusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
    var clusterPassports = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Passports.cshtml"));
    var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
    var clusterController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));

    Assert.Contains("Passport state", adminPassports);
    Assert.Contains("@row.PassportState", adminPassports);
    Assert.Contains("Passport state", adminClusters);
    Assert.Contains("@row.PassportState", adminClusters);
    Assert.Contains("Passport state", clusterPassports);
    Assert.Contains("@row.PassportState", clusterPassports);
    Assert.Contains("PassportState = passport.PassportState", adminController);
    Assert.Contains("PassportState = passport.PassportState", clusterController);
}
```

- [ ] **Step 2: Run the markup tests and verify red**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PassportViews_ShouldSeparatePassportStateFromBatteryStatus|FullyQualifiedName~Registry_ShouldExposeSummaryAndDetailedReportActions|FullyQualifiedName~PassportListViews_ShouldShowPassportStateInsteadOfAmbiguousStatus" --no-restore
```

Expected: failures because the views still use generic status labels and registry has one action.

- [ ] **Step 3: Update passport summary/detail views**

In `web/Views/Passport/Summary.cshtml`, change the hero badge to:

```cshtml
<span class="bp-pill bp-pill-muted">@passport.PassportState</span>
```

In the summary `bp-field-grid`, replace the generic status row:

```cshtml
<div><dt>Status</dt><dd>@passport.BatteryStatus</dd></div>
```

with:

```cshtml
<div><dt>Passport state</dt><dd>@passport.PassportState</dd></div>
<div><dt>Battery status</dt><dd>@passport.BatteryStatus</dd></div>
```

In `web/Views/Passport/Detail.cshtml`, change the hero badge to:

```cshtml
<span class="bp-pill bp-pill-muted">@passport.PassportState</span>
```

Replace each top/general grid generic status row that renders `@passport.BatteryStatus` with:

```cshtml
<div><dt>Passport state</dt><dd>@passport.PassportState</dd></div>
<div><dt>Battery status</dt><dd>@passport.BatteryStatus</dd></div>
```

Remove this line from the image column:

```cshtml
<p class="bp-help-text mt-2">@passport.BatteryImageAlt</p>
```

- [ ] **Step 4: Update registry actions and summary mapping**

In `web/Views/Registry/Index.cshtml`, change the status header to:

```cshtml
<th class="px-3 py-3">Passport state</th>
```

Change the row status cell to:

```cshtml
<td class="px-3 py-3">@row.PassportState</td>
```

Replace the final action cell with:

```cshtml
<td class="px-3 py-3 text-end">
    <div class="bp-action-row justify-content-end">
        <a href="/@Uri.EscapeDataString(row.PassportId)/summary" class="bp-secondary-button bp-button-compact">Summary</a>
        <a href="/@Uri.EscapeDataString(row.PassportId)" class="bp-primary-button bp-button-compact">Detailed report</a>
    </div>
</td>
```

In `web/Controllers/RegistryController.cs`, add `PassportState = passport.PassportState,` inside the `passports.Select(passport => new PassportSummaryViewModel { ... })` projection.

In `web/Controllers/AdminController.cs`, add `PassportState = passport.PassportState,` inside the `Passports = passports.Select(passport => new PassportSummaryViewModel { ... })` projection.

In `web/Controllers/ClusterAdminController.cs`, add `PassportState = passport.PassportState,` inside every `new PassportSummaryViewModel` projection.

In `web/Views/Admin/Passports.cshtml`, `web/Views/Admin/Clusters.cshtml`, and `web/Views/ClusterAdmin/Passports.cshtml`, change the passport list header from `Status` to `Passport state` and change row cells from `@row.RegistryStatus` to `@row.PassportState`.

- [ ] **Step 5: Run the markup tests and verify green**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~PassportViews_ShouldSeparatePassportStateFromBatteryStatus|FullyQualifiedName~Registry_ShouldExposeSummaryAndDetailedReportActions|FullyQualifiedName~PassportListViews_ShouldShowPassportStateInsteadOfAmbiguousStatus" --no-restore
```

Expected: the two tests pass.

- [ ] **Step 6: Commit**

```powershell
git add BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs web/Views/Passport/Summary.cshtml web/Views/Passport/Detail.cshtml web/Views/Registry/Index.cshtml web/Views/Admin/Passports.cshtml web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/Passports.cshtml web/Controllers/RegistryController.cs web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs
git commit -m "Clarify passport view states and registry actions"
```

---

### Task 3: Admin Archive, Unarchive, And Compact Actions

**Files:**
- Modify: `BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs`
- Modify: `web/Services/PassportRepository.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Views/Admin/Passports.cshtml`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Add failing admin action tests**

Append this test to `PassportAdminUxGuardrailTests`:

```csharp
[Fact]
public void AdminPassportLists_ShouldUseCompactRecoverableArchiveActions()
{
    var adminPassports = File.ReadAllText(RepoFile("web", "Views", "Admin", "Passports.cshtml"));
    var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
    var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
    var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

    Assert.Contains("UnarchivePassportAsync", repository);
    Assert.Contains("previousStatus", repository);
    Assert.Contains("[HttpPost(\"passports/unarchive\")]", controller);
    Assert.Contains("title=\"Edit\"", clusters);
    Assert.Contains("title=\"Conformance\"", clusters);
    Assert.Contains("title=\"Archive\"", clusters);
    Assert.Contains("title=\"Unarchive\"", clusters);
    Assert.Contains("return confirm('Archive passport", clusters);
    Assert.Contains("bp-icon-button", clusters);
    Assert.Contains("bp-actions-cell", clusters);
    Assert.Contains("bp-table-nowrap", clusters);
    Assert.Contains("bp-icon-button", adminPassports);
    Assert.Contains(".bp-icon-button", css);
    Assert.Contains(".bp-actions-cell", css);
    Assert.Contains(".bp-table-nowrap", css);
}
```

- [ ] **Step 2: Run the admin action test and verify red**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AdminPassportLists_ShouldUseCompactRecoverableArchiveActions" --no-restore
```

Expected: failure because unarchive, compact action classes, and confirmation are missing.

- [ ] **Step 3: Implement repository unarchive and archive previous status**

In `web/Services/PassportRepository.cs`, update `ArchivePassportAsync` so it reads the current document before updating and stores the previous status:

```csharp
var document = await collection
    .Find(Builders<BsonDocument>.Filter.Eq("passportId", passportId))
    .FirstOrDefaultAsync(cancellationToken);
var previousStatus = document == null
    ? "draft"
    : BsonHelpers.GetString(document, "registryInfo", "status");
if (string.IsNullOrWhiteSpace(previousStatus) || previousStatus.Equals("archived", StringComparison.OrdinalIgnoreCase))
{
    previousStatus = "draft";
}
```

Add this update field to the archive update chain:

```csharp
.Set("registryInfo.previousStatus", previousStatus)
```

Add this method after `ArchivePassportAsync`:

```csharp
public async Task UnarchivePassportAsync(string passportId, CancellationToken cancellationToken = default)
{
    var collection = GetCollection();
    if (collection == null || string.IsNullOrWhiteSpace(passportId))
    {
        return;
    }

    var document = await collection
        .Find(Builders<BsonDocument>.Filter.Eq("passportId", passportId))
        .FirstOrDefaultAsync(cancellationToken);
    if (document == null)
    {
        return;
    }

    var previousStatus = BsonHelpers.GetString(document, "registryInfo", "previousStatus");
    if (string.IsNullOrWhiteSpace(previousStatus) || previousStatus.Equals("archived", StringComparison.OrdinalIgnoreCase))
    {
        previousStatus = "draft";
    }

    var now = DateTime.UtcNow.ToString("O");
    await collection.UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("passportId", passportId),
        Builders<BsonDocument>.Update
            .Set("registryInfo.status", previousStatus)
            .Unset("registryInfo.previousStatus")
            .Set("registryInfo.updatedAt", now),
        cancellationToken: cancellationToken);
}
```

- [ ] **Step 4: Add admin unarchive action**

In `web/Controllers/AdminController.cs`, add this action after `ArchivePassport`:

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

- [ ] **Step 5: Replace admin table action markup**

In both `web/Views/Admin/Passports.cshtml` and the passports table inside `web/Views/Admin/Clusters.cshtml`, set Model and Updated headers/cells to include `bp-table-nowrap` and set the action header/cell to include `bp-actions-cell`.

Use this action cell structure for each row:

```cshtml
<td class="px-4 py-3 bp-actions-cell">
    <div class="bp-admin-action-row">
        <a class="bp-icon-button" href="/admin/passports/@Uri.EscapeDataString(row.PassportId)/edit" title="Edit" aria-label="Edit @row.PassportId">
            <span aria-hidden="true">&#9998;</span>
        </a>
        <a class="bp-icon-button bp-conformance-action" href="/admin/passports/@Uri.EscapeDataString(row.PassportId)/conformance" title="Conformance" aria-label="Conformance for @row.PassportId">
            <span aria-hidden="true">C</span>
        </a>
        @if (row.RegistryStatus.Equals("archived", StringComparison.OrdinalIgnoreCase))
        {
            <form method="post" action="/admin/passports/unarchive">
                @Html.AntiForgeryToken()
                <input type="hidden" name="passportId" value="@row.PassportId" />
                <button type="submit" class="bp-icon-button" title="Unarchive" aria-label="Unarchive @row.PassportId">
                    <span aria-hidden="true">&#8634;</span>
                </button>
            </form>
        }
        else
        {
            <form method="post" action="/admin/passports/archive" onsubmit="return confirm('Archive passport @row.PassportId? Normal users will no longer be able to open its reports.');">
                @Html.AntiForgeryToken()
                <input type="hidden" name="passportId" value="@row.PassportId" />
                <button type="submit" class="bp-icon-button bp-icon-button-danger" title="Archive" aria-label="Archive @row.PassportId">
                    <span aria-hidden="true">&#128230;</span>
                </button>
            </form>
        }
    </div>
</td>
```

In `web/Views/Admin/Passports.cshtml`, omit the conformance link only if this older redirected view should remain minimal. If omitted there, keep `title="Conformance"` in `Clusters.cshtml` so the test remains meaningful.

- [ ] **Step 6: Add compact action CSS**

Append this CSS near the existing `.bp-action-row` styles in `web/wwwroot/css/site.css`:

```css
.bp-actions-cell {
  width: 1%;
  white-space: nowrap;
}

.bp-table-nowrap {
  white-space: nowrap;
}

.bp-admin-action-row {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.bp-admin-action-row form {
  margin: 0;
  display: inline-flex;
}

.bp-icon-button {
  width: 34px;
  height: 34px;
  min-height: 34px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #fff;
  color: #334155;
  font-weight: 800;
  line-height: 1;
  padding: 0;
}

.bp-icon-button:hover,
.bp-icon-button:focus-visible {
  border-color: var(--bp-primary);
  color: var(--bp-primary-dark);
}

.bp-icon-button-danger {
  border-color: #fda29b;
  color: var(--bp-danger);
}

.bp-conformance-action {
  background: #eef6ff;
  color: var(--bp-scania-blue);
}
```

- [ ] **Step 7: Run the admin action test and verify green**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AdminPassportLists_ShouldUseCompactRecoverableArchiveActions" --no-restore
```

Expected: the test passes.

- [ ] **Step 8: Commit**

```powershell
git add BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs web/Services/PassportRepository.cs web/Controllers/AdminController.cs web/Views/Admin/Passports.cshtml web/Views/Admin/Clusters.cshtml web/wwwroot/css/site.css
git commit -m "Add recoverable compact passport archive actions"
```

---

### Task 4: Cluster Delete Guardrails

**Files:**
- Modify: `BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Services/PassportRepository.cs`
- Modify: `web/Services/ClusterRepository.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`

- [ ] **Step 1: Add failing cluster delete tests**

Append this test to `PassportAdminUxGuardrailTests`:

```csharp
[Fact]
public void ClusterDeletion_ShouldRequireConfirmationAndProtectLinkedData()
{
    var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "AdminClusterViewModel.cs"));
    var passportRepository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
    var clusterRepository = File.ReadAllText(RepoFile("web", "Services", "ClusterRepository.cs"));
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
    var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

    Assert.Contains("LinkedPassportCount", model);
    Assert.Contains("MembershipCount", model);
    Assert.Contains("CountByClusterAsync", passportRepository);
    Assert.Contains("CountMembershipsByClusterAsync", clusterRepository);
    Assert.Contains("[HttpPost(\"clusters/delete-force\")]", controller);
    Assert.Contains("confirmationPhrase", controller);
    Assert.Contains("Cannot delete cluster", controller);
    Assert.Contains("LinkedPassportCount = await _passportRepository.CountByClusterAsync", controller);
    Assert.Contains("return confirm('Delete cluster", view);
    Assert.Contains("Force delete linked data", view);
    Assert.Contains("name=\"confirmationPhrase\"", view);
}
```

- [ ] **Step 2: Run the cluster delete test and verify red**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ClusterDeletion_ShouldRequireConfirmationAndProtectLinkedData" --no-restore
```

Expected: failure because counts and force-delete do not exist.

- [ ] **Step 3: Add linked-data count properties**

In `ClusterViewModel` inside `web/Models/ViewModels/AdminClusterViewModel.cs`, add:

```csharp
public long LinkedPassportCount { get; init; }
public long MembershipCount { get; init; }
```

- [ ] **Step 4: Add repository count methods**

In `web/Services/PassportRepository.cs`, add:

```csharp
public async Task<long> CountByClusterAsync(string clusterId, CancellationToken cancellationToken = default)
{
    var collection = GetCollection();
    if (collection == null || string.IsNullOrWhiteSpace(clusterId))
    {
        return 0;
    }

    return await collection.CountDocumentsAsync(
        Builders<BsonDocument>.Filter.Eq("clusterId", clusterId),
        cancellationToken: cancellationToken);
}
```

In `web/Services/ClusterRepository.cs`, add:

```csharp
public async Task<long> CountMembershipsByClusterAsync(string clusterId, CancellationToken cancellationToken = default)
{
    if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(clusterId))
    {
        return 0;
    }

    return await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships")
        .CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("clusterId", clusterId),
            cancellationToken: cancellationToken);
}
```

- [ ] **Step 5: Populate counts in admin clusters model**

In `AdminController.Clusters`, after the initial `clusterViewModels` projection, replace the list construction with a loop that sets counts:

```csharp
var clusterViewModels = new List<ClusterViewModel>();
foreach (var cluster in clusters)
{
    var clusterId = BsonHelpers.GetString(cluster, "clusterId");
    if (string.IsNullOrWhiteSpace(clusterId))
    {
        continue;
    }

    clusterViewModels.Add(new ClusterViewModel
    {
        ClusterId = clusterId,
        Name = BsonHelpers.GetString(cluster, "name"),
        CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
        UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt"),
        LinkedPassportCount = await _passportRepository.CountByClusterAsync(clusterId, cancellationToken),
        MembershipCount = await _clusterRepository.CountMembershipsByClusterAsync(clusterId, cancellationToken)
    });
}

clusterViewModels = clusterViewModels
    .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
    .ToList();
```

- [ ] **Step 6: Guard normal delete and add force delete**

Replace `DeleteCluster` in `web/Controllers/AdminController.cs` with:

```csharp
[HttpPost("clusters/delete")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteCluster(CancellationToken cancellationToken)
{
    var clusterId = Text(Request.Form, "clusterId");
    var linkedPassports = await _passportRepository.CountByClusterAsync(clusterId, cancellationToken);
    var linkedMemberships = await _clusterRepository.CountMembershipsByClusterAsync(clusterId, cancellationToken);
    if (linkedPassports > 0 || linkedMemberships > 0)
    {
        TempData["ErrorMessage"] = $"Cannot delete cluster {clusterId} because it has {linkedPassports} linked passport(s) and {linkedMemberships} user membership(s). Use force delete if you intend to remove linked data.";
        return Redirect("/admin/clusters?tab=clusters");
    }

    await _clusterRepository.DeleteClusterAsync(clusterId, cancellationToken);
    TempData["StatusMessage"] = $"Cluster {clusterId} deleted.";
    return Redirect("/admin/clusters?tab=clusters");
}
```

Add this action after it:

```csharp
[HttpPost("clusters/delete-force")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ForceDeleteCluster(CancellationToken cancellationToken)
{
    var clusterId = Text(Request.Form, "clusterId");
    var confirmationPhrase = Text(Request.Form, "confirmationPhrase");
    if (string.IsNullOrWhiteSpace(clusterId) || !confirmationPhrase.Equals(clusterId, StringComparison.Ordinal))
    {
        TempData["ErrorMessage"] = $"Force delete requires typing the exact cluster ID.";
        return Redirect("/admin/clusters?tab=clusters");
    }

    await _passportRepository.ClearPassportClusterAsync(clusterId, cancellationToken);
    await _clusterRepository.DeleteClusterAsync(clusterId, cancellationToken);
    TempData["StatusMessage"] = $"Cluster {clusterId} and linked memberships were deleted. Linked passports are now unassigned.";
    return Redirect("/admin/clusters?tab=clusters");
}
```

- [ ] **Step 7: Add guarded cluster delete UI**

In the cluster row action cell in `web/Views/Admin/Clusters.cshtml`, replace the existing delete form with:

```cshtml
<div class="bp-cluster-delete-stack">
    <form method="post" action="/admin/clusters/delete" onsubmit="return confirm('Delete cluster @cluster.ClusterId? This only succeeds when no passports or users are linked.');">
        @Html.AntiForgeryToken()
        <input type="hidden" name="clusterId" value="@cluster.ClusterId" />
        <button type="submit" class="bp-danger-button">Delete</button>
    </form>
    @if (cluster.LinkedPassportCount > 0 || cluster.MembershipCount > 0)
    {
        <details class="bp-force-delete">
            <summary>Force delete linked data</summary>
            <p class="bp-help-text">Linked passports: @cluster.LinkedPassportCount. User memberships: @cluster.MembershipCount. Type <code>@cluster.ClusterId</code> to confirm.</p>
            <form method="post" action="/admin/clusters/delete-force">
                @Html.AntiForgeryToken()
                <input type="hidden" name="clusterId" value="@cluster.ClusterId" />
                <input class="form-control form-control-sm" name="confirmationPhrase" aria-label="Type cluster ID @cluster.ClusterId" />
                <button type="submit" class="bp-danger-button">Force delete</button>
            </form>
        </details>
    }
</div>
```

- [ ] **Step 8: Run the cluster delete test and verify green**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ClusterDeletion_ShouldRequireConfirmationAndProtectLinkedData" --no-restore
```

Expected: the test passes.

- [ ] **Step 9: Commit**

```powershell
git add BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Services/PassportRepository.cs web/Services/ClusterRepository.cs web/Controllers/AdminController.cs web/Views/Admin/Clusters.cshtml
git commit -m "Protect cluster deletion with linked data guardrails"
```

---

### Task 5: Role Labels And Local Admin Self-Protection

**Files:**
- Modify: `BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Services/AccessControlService.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Users.cshtml`

- [ ] **Step 1: Add failing role-management tests**

Append this test to `PassportAdminUxGuardrailTests`:

```csharp
[Fact]
public void UserManagement_ShouldUseFriendlyRoleLabelsAndProtectLocalAdminSelfChanges()
{
    var access = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
    var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "AdminClusterViewModel.cs"));
    var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
    var adminView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
    var clusterView = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));

    Assert.Contains("DisplayRoleLabel", access);
    Assert.Contains("DisplayMembershipRole", access);
    Assert.Contains("IsCurrentUser", model);
    Assert.Contains("CurrentEmail(User).Equals(email", clusterAdmin);
    Assert.Contains("Local admins cannot change their own cluster role", clusterAdmin);
    Assert.Contains("AccessControlService.DisplayMembershipRole", adminView);
    Assert.Contains("AccessControlService.DisplayMembershipRole", clusterView);
    Assert.Contains("Local admins cannot edit their own membership role here.", clusterView);
}
```

- [ ] **Step 2: Run the role-management test and verify red**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~UserManagement_ShouldUseFriendlyRoleLabelsAndProtectLocalAdminSelfChanges" --no-restore
```

Expected: failure because helpers and self-protection are missing.

- [ ] **Step 3: Add role display helpers**

In `web/Services/AccessControlService.cs`, add:

```csharp
public static string DisplayRoleLabel(ClaimsPrincipal user)
{
    if (user.IsInRole("admin"))
    {
        return "Global Admin";
    }

    if (user.IsInRole("clusterAdmin"))
    {
        return "Local Admin";
    }

    return "Normal User";
}

public static string DisplayRoleLabel(IEnumerable<string> roles)
{
    var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (roleSet.Contains("admin"))
    {
        return "Global Admin";
    }

    if (roleSet.Contains("clusterAdmin"))
    {
        return "Local Admin";
    }

    return "Normal User";
}

public static string DisplayMembershipRole(string role)
{
    return role.Equals("clusterAdmin", StringComparison.OrdinalIgnoreCase)
        ? "Local admin"
        : "Normal user";
}
```

Add `using System.Security.Claims;` if the file does not already have it.

- [ ] **Step 4: Add current-user flag to user rows**

In `UserViewModel` inside `web/Models/ViewModels/AdminClusterViewModel.cs`, add:

```csharp
public bool IsCurrentUser { get; init; }
```

In `AdminController.Clusters` and `ClusterAdminController.Users`, set:

```csharp
IsCurrentUser = BsonHelpers.GetString(user, "email").Equals(AccessControlService.CurrentEmail(User), StringComparison.OrdinalIgnoreCase),
```

inside `new UserViewModel`.

- [ ] **Step 5: Block local-admin self role changes**

In `ClusterAdminController.SaveUser`, after `email`, `clusterId`, and `role` are read, add:

```csharp
if (!AccessControlService.IsAdmin(User)
    && AccessControlService.CurrentEmail(User).Equals(email, StringComparison.OrdinalIgnoreCase))
{
    return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Local admins cannot change their own cluster role here. Use Account for your own profile details.")}");
}
```

In `ClusterAdminController.DeleteUserMembership`, after validating `email` and `clusterId`, add:

```csharp
if (!AccessControlService.IsAdmin(User)
    && AccessControlService.CurrentEmail(User).Equals(email, StringComparison.OrdinalIgnoreCase))
{
    return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Local admins cannot remove their own cluster membership.")}");
}
```

- [ ] **Step 6: Use friendly role labels in user views**

In `web/Views/Admin/Clusters.cshtml`, replace:

```cshtml
@(membership.Role == "clusterAdmin" ? "Local admin" : "Normal user")
```

with:

```cshtml
@BatteryPassWeb.Services.AccessControlService.DisplayMembershipRole(membership.Role)
```

In `web/Views/ClusterAdmin/Users.cshtml`, make the same replacement.

In `web/Views/ClusterAdmin/Users.cshtml`, inside the edit-membership cell, render a non-editable message for the current user:

```cshtml
@if (user.IsCurrentUser && !User.IsInRole("admin"))
{
    <p class="bp-help-text mb-0">Local admins cannot edit their own membership role here.</p>
}
else
{
    <form method="post" action="/cluster-admin/users/save" class="d-flex flex-wrap gap-2">
        @Html.AntiForgeryToken()
        <input type="hidden" name="email" value="@user.Email" />
        <input class="form-control form-control-sm" name="name" value="@user.Name" aria-label="Display name" style="min-width: 180px;" />
        <span class="bp-password-field bp-password-field-inline">
            <input class="form-control form-control-sm" type="password" name="password" aria-label="New password" style="min-width: 180px;" />
            <button type="button" class="bp-password-reveal" data-password-reveal aria-label="Show password">Show</button>
        </span>
        <select name="clusterId" class="form-select form-select-sm" style="min-width: 220px;">
            @foreach (var cluster in Model.Clusters)
            {
                <option value="@cluster.ClusterId">@cluster.Name</option>
            }
        </select>
        <select name="role" class="form-select form-select-sm" style="min-width: 170px;">
            <option value="member">Normal user</option>
            <option value="clusterAdmin">Local admin</option>
        </select>
        <button type="submit" class="bp-secondary-button">Save</button>
    </form>
}
```

- [ ] **Step 7: Run the role-management test and verify green**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~UserManagement_ShouldUseFriendlyRoleLabelsAndProtectLocalAdminSelfChanges" --no-restore
```

Expected: the test passes.

- [ ] **Step 8: Commit**

```powershell
git add BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Services/AccessControlService.cs web/Controllers/ClusterAdminController.cs web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/Users.cshtml
git commit -m "Clarify user roles and protect local admin self changes"
```

---

### Task 6: Self-Service Account Page

**Files:**
- Modify: `BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs`
- Create: `web/Models/ViewModels/AccountProfileViewModel.cs`
- Modify: `web/Services/ClusterRepository.cs`
- Modify: `web/Services/AuthService.cs`
- Create: `web/Controllers/AccountController.cs`
- Create: `web/Views/Account/Index.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Add failing account tests**

Append this test to `PassportAdminUxGuardrailTests`:

```csharp
[Fact]
public void AccountPage_ShouldAllowAuthenticatedUsersToUpdateProfileAndRefreshSession()
{
    var accountModelPath = RepoFile("web", "Models", "ViewModels", "AccountProfileViewModel.cs");
    var accountControllerPath = RepoFile("web", "Controllers", "AccountController.cs");
    var accountViewPath = RepoFile("web", "Views", "Account", "Index.cshtml");
    var clusterRepository = File.ReadAllText(RepoFile("web", "Services", "ClusterRepository.cs"));
    var authService = File.ReadAllText(RepoFile("web", "Services", "AuthService.cs"));

    Assert.True(File.Exists(accountModelPath));
    Assert.True(File.Exists(accountControllerPath));
    Assert.True(File.Exists(accountViewPath));
    Assert.Contains("UpdateUserEmailAsync", clusterRepository);
    Assert.Contains("UpdateUserProfileAsync", clusterRepository);
    Assert.Contains("CreatePrincipalForUserAsync", authService);

    var controller = File.ReadAllText(accountControllerPath);
    var view = File.ReadAllText(accountViewPath);

    Assert.Contains("[Route(\"account\")]", controller);
    Assert.Contains("HttpContext.SignInAsync", controller);
    Assert.Contains("Display name", view);
    Assert.Contains("Email address", view);
    Assert.Contains("New password", view);
}
```

- [ ] **Step 2: Run the account test and verify red**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AccountPage_ShouldAllowAuthenticatedUsersToUpdateProfileAndRefreshSession" --no-restore
```

Expected: failure because the account files and methods do not exist.

- [ ] **Step 3: Create account view model**

Create `web/Models/ViewModels/AccountProfileViewModel.cs`:

```csharp
namespace BatteryPassWeb.Models.ViewModels;

public sealed class AccountProfileViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Add user profile repository methods**

In `web/Services/ClusterRepository.cs`, add:

```csharp
public async Task<bool> UpdateUserProfileAsync(
    string email,
    string name,
    string? passwordHash = null,
    CancellationToken cancellationToken = default)
{
    if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
    {
        return false;
    }

    var normalizedEmail = email.Trim().ToLowerInvariant();
    var updates = new List<UpdateDefinition<BsonDocument>>
    {
        Builders<BsonDocument>.Update.Set("name", string.IsNullOrWhiteSpace(name) ? normalizedEmail : name.Trim()),
        Builders<BsonDocument>.Update.Set("updatedAt", DateTime.UtcNow.ToString("O"))
    };

    if (!string.IsNullOrWhiteSpace(passwordHash))
    {
        updates.Add(Builders<BsonDocument>.Update.Set("passwordHash", passwordHash));
    }

    var result = await _mongoContext.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
        Builders<BsonDocument>.Update.Combine(updates),
        cancellationToken: cancellationToken);

    return result.MatchedCount > 0;
}

public async Task<bool> UpdateUserEmailAsync(
    string oldEmail,
    string newEmail,
    CancellationToken cancellationToken = default)
{
    if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(oldEmail) || string.IsNullOrWhiteSpace(newEmail))
    {
        return false;
    }

    var normalizedOldEmail = oldEmail.Trim().ToLowerInvariant();
    var normalizedNewEmail = newEmail.Trim().ToLowerInvariant();
    if (normalizedOldEmail.Equals(normalizedNewEmail, StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    var users = _mongoContext.Database.GetCollection<BsonDocument>("users");
    var existingTarget = await users.Find(Builders<BsonDocument>.Filter.Eq("email", normalizedNewEmail)).FirstOrDefaultAsync(cancellationToken);
    if (existingTarget != null)
    {
        return false;
    }

    var result = await users.UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("email", normalizedOldEmail),
        Builders<BsonDocument>.Update
            .Set("email", normalizedNewEmail)
            .Set("updatedAt", DateTime.UtcNow.ToString("O")),
        cancellationToken: cancellationToken);

    if (result.MatchedCount == 0)
    {
        return false;
    }

    await _mongoContext.Database.GetCollection<BsonDocument>("clusterMemberships").UpdateManyAsync(
        Builders<BsonDocument>.Filter.Eq("email", normalizedOldEmail),
        Builders<BsonDocument>.Update.Set("email", normalizedNewEmail).Set("updatedAt", DateTime.UtcNow.ToString("O")),
        cancellationToken: cancellationToken);

    return true;
}
```

- [ ] **Step 5: Expose principal refresh from AuthService**

In `web/Services/AuthService.cs`, add this public method:

```csharp
public async Task<ClaimsPrincipal?> CreatePrincipalForUserAsync(string email, CancellationToken cancellationToken = default)
{
    if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
    {
        return null;
    }

    var normalizedEmail = email.Trim().ToLowerInvariant();
    var user = await _mongoContext.Database.GetCollection<BsonDocument>("users")
        .Find(Builders<BsonDocument>.Filter.Eq("email", normalizedEmail))
        .FirstOrDefaultAsync(cancellationToken);
    if (user == null)
    {
        return null;
    }

    var roles = ExtractRoles(user).ToList();
    if (!roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
    {
        var hasClusterAdminMembership = await _mongoContext.Database
            .GetCollection<BsonDocument>("clusterMemberships")
            .Find(Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
                Builders<BsonDocument>.Filter.Eq("role", "clusterAdmin")))
            .AnyAsync(cancellationToken);
        if (hasClusterAdminMembership)
        {
            roles.Add("clusterAdmin");
        }
    }

    return BuildPrincipal(normalizedEmail, BsonHelpers.GetString(user, "name"), roles);
}
```

In `AuthenticateAsync`, after password verification succeeds, replace the duplicated role/principal block with:

```csharp
return await CreatePrincipalForUserAsync(normalizedEmail, cancellationToken);
```

- [ ] **Step 6: Create AccountController**

Create `web/Controllers/AccountController.cs`:

```csharp
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BatteryPassWeb.Controllers;

[Authorize]
[Route("account")]
public class AccountController : Controller
{
    private readonly ClusterRepository _clusterRepository;
    private readonly AuthService _authService;

    public AccountController(ClusterRepository clusterRepository, AuthService authService)
    {
        _clusterRepository = clusterRepository;
        _authService = authService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var email = AccessControlService.CurrentEmail(User);
        var user = await _clusterRepository.GetUserByEmailAsync(email, cancellationToken);
        return View(new AccountProfileViewModel
        {
            Email = email,
            Name = user == null ? User.Identity?.Name ?? email : BsonHelpers.GetString(user, "name"),
            StatusMessage = string.IsNullOrWhiteSpace(status) ? string.Empty : Uri.UnescapeDataString(status),
            ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error)
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AccountProfileViewModel model, CancellationToken cancellationToken)
    {
        var currentEmail = AccessControlService.CurrentEmail(User);
        var requestedEmail = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(requestedEmail))
        {
            return Redirect($"/account?error={Uri.EscapeDataString("Email address is required.")}");
        }

        if (!string.IsNullOrWhiteSpace(model.NewPassword)
            && !model.NewPassword.Equals(model.ConfirmPassword, StringComparison.Ordinal))
        {
            return Redirect($"/account?error={Uri.EscapeDataString("New password and confirmation do not match.")}");
        }

        var user = await _clusterRepository.GetUserByEmailAsync(currentEmail, cancellationToken);
        if (user == null)
        {
            return Redirect($"/account?error={Uri.EscapeDataString("This account can only be changed after it is stored in the user registry.")}");
        }

        if (!requestedEmail.Equals(currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailChanged = await _clusterRepository.UpdateUserEmailAsync(currentEmail, requestedEmail, cancellationToken);
            if (!emailChanged)
            {
                return Redirect($"/account?error={Uri.EscapeDataString("Email address is already used by another account.")}");
            }
        }

        var passwordHash = string.IsNullOrWhiteSpace(model.NewPassword) ? string.Empty : BCryptNet.HashPassword(model.NewPassword);
        var profileUpdated = await _clusterRepository.UpdateUserProfileAsync(requestedEmail, model.Name, passwordHash, cancellationToken);
        if (!profileUpdated)
        {
            return Redirect($"/account?error={Uri.EscapeDataString("Account profile could not be saved.")}");
        }

        var principal = await _authService.CreatePrincipalForUserAsync(requestedEmail, cancellationToken);
        if (principal != null)
        {
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        return Redirect($"/account?status={Uri.EscapeDataString("Account profile saved.")}");
    }
}
```

- [ ] **Step 7: Create account view**

Create `web/Views/Account/Index.cshtml`:

```cshtml
@model BatteryPassWeb.Models.ViewModels.AccountProfileViewModel
@{
    ViewData["Title"] = "Account";
}
<main class="bp-page bp-admin-page">
    <div>
        <h1 class="mb-1">Account</h1>
        <p class="text-secondary mb-0">Update your customer details and password.</p>
    </div>

    @if (!string.IsNullOrWhiteSpace(Model.StatusMessage))
    {
        <div class="alert alert-success mb-0">@Model.StatusMessage</div>
    }
    @if (!string.IsNullOrWhiteSpace(Model.ErrorMessage))
    {
        <div class="alert alert-danger mb-0">@Model.ErrorMessage</div>
    }

    <section class="bp-card bp-account-card">
        <form method="post" action="/account" class="bp-stack-form">
            @Html.AntiForgeryToken()
            <label>
                Display name
                <input type="text" name="Name" value="@Model.Name" autocomplete="name" />
            </label>
            <label>
                Email address
                <input type="email" name="Email" value="@Model.Email" autocomplete="email" required />
            </label>
            <label>
                New password
                <span class="bp-password-field">
                    <input type="password" name="NewPassword" autocomplete="new-password" />
                    <button type="button" class="bp-password-reveal" data-password-reveal aria-label="Show password">Show</button>
                </span>
            </label>
            <label>
                Confirm password
                <span class="bp-password-field">
                    <input type="password" name="ConfirmPassword" autocomplete="new-password" />
                    <button type="button" class="bp-password-reveal" data-password-reveal aria-label="Show password">Show</button>
                </span>
            </label>
            <button type="submit" class="bp-primary-button">Save account</button>
        </form>
    </section>
</main>

@section Scripts {
    <script>
        (function () {
            document.querySelectorAll('[data-password-reveal]').forEach((button) => {
                button.addEventListener('click', () => {
                    const input = button.closest('.bp-password-field')?.querySelector('input');
                    if (!input) {
                        return;
                    }

                    const reveal = input.type === 'password';
                    input.type = reveal ? 'text' : 'password';
                    button.textContent = reveal ? 'Hide' : 'Show';
                    button.setAttribute('aria-label', reveal ? 'Hide password' : 'Show password');
                });
            });
        })();
    </script>
}
```

Append this CSS to `web/wwwroot/css/site.css`:

```css
.bp-account-card {
  max-width: 560px;
}
```

- [ ] **Step 8: Run the account test and verify green**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~AccountPage_ShouldAllowAuthenticatedUsersToUpdateProfileAndRefreshSession" --no-restore
```

Expected: the test passes.

- [ ] **Step 9: Commit**

```powershell
git add BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs web/Models/ViewModels/AccountProfileViewModel.cs web/Services/ClusterRepository.cs web/Services/AuthService.cs web/Controllers/AccountController.cs web/Views/Account/Index.cshtml web/wwwroot/css/site.css
git commit -m "Add self-service account profile page"
```

---

### Task 7: Header Customer Name And Role

**Files:**
- Modify: `BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs`
- Modify: `web/Views/Shared/_Layout.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] **Step 1: Add failing header tests**

Append this test to `PassportAdminUxGuardrailTests`:

```csharp
[Fact]
public void Header_ShouldShowCustomerNameAndRoleAndLinkAccount()
{
    var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
    var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

    Assert.Contains("customerName", layout);
    Assert.Contains("customerRole", layout);
    Assert.Contains("AccessControlService.DisplayRoleLabel(User)", layout);
    Assert.Contains("href=\"/account\"", layout);
    Assert.Contains("@customerName / @customerRole", layout);
    Assert.Contains(".bp-identity a", css);
}
```

- [ ] **Step 2: Run the header test and verify red**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~Header_ShouldShowCustomerNameAndRoleAndLinkAccount" --no-restore
```

Expected: failure because layout still shows cluster names and no account link.

- [ ] **Step 3: Update layout identity computation**

In `web/Views/Shared/_Layout.cshtml`, add variables:

```cshtml
var customerName = string.Empty;
var customerRole = string.Empty;
```

Inside `if (isAuthenticated)`, set:

```cshtml
identityLabel = BatteryPassWeb.Services.AccessControlService.CurrentEmail(User);
var storedUser = await ClusterRepository.GetUserByEmailAsync(identityLabel);
customerName = storedUser == null
    ? User.Identity?.Name ?? identityLabel
    : BatteryPassWeb.Services.BsonHelpers.GetString(storedUser, "name");
if (string.IsNullOrWhiteSpace(customerName))
{
    customerName = identityLabel;
}
customerRole = BatteryPassWeb.Services.AccessControlService.DisplayRoleLabel(User);
```

Remove the `clusterNames` list computation from layout identity if no other layout section uses it.

- [ ] **Step 4: Add Account link and identity text**

In the authenticated primary links block, add:

```cshtml
<a href="/account">Account</a>
```

before `Help`.

Change the identity block to:

```cshtml
<div class="bp-identity">
    <p>@identityLabel</p>
    <p>@customerName / @customerRole</p>
</div>
```

- [ ] **Step 5: Add identity link CSS**

Append:

```css
.bp-identity a {
  color: inherit;
}
```

- [ ] **Step 6: Run the header test and verify green**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~Header_ShouldShowCustomerNameAndRoleAndLinkAccount" --no-restore
```

Expected: the test passes.

- [ ] **Step 7: Commit**

```powershell
git add BatteryPassWeb.Tests/PassportAdminUxGuardrailTests.cs web/Views/Shared/_Layout.cshtml web/wwwroot/css/site.css
git commit -m "Show customer name and role in header"
```

---

### Task 8: Full Verification And Cleanup

**Files:**
- Verify all modified files.
- Update docs only if tests reveal user-facing documentation assertions that must change.

- [ ] **Step 1: Run all tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore
```

Expected: all tests pass with zero failures.

- [ ] **Step 2: Run build**

Run:

```powershell
dotnet build web/BatteryPassWeb.csproj --no-restore
```

Expected: build succeeds with exit code 0.

- [ ] **Step 3: Inspect git diff**

Run:

```powershell
git diff --stat
git diff --check
```

Expected: changes are limited to the files in this plan, and `git diff --check` reports no whitespace errors.

- [ ] **Step 4: Commit verification-only doc updates if needed**

If documentation tests require wording updates, modify the exact asserted docs, then run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --no-restore
git add docs BatteryPassWeb.Tests
git commit -m "Update docs for passport admin guardrails"
```

Expected: no commit is created when no docs changed.

- [ ] **Step 5: Record final status**

Run:

```powershell
git status --short
```

Expected: only unrelated pre-existing changes remain, or the working tree is clean for files touched by this plan.
