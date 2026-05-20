# Passport Admin Registry Rules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved passport admin, registry, API, editable-field, telemetry, and report fixes from the 2026-05-20 design.

**Architecture:** Keep the existing ASP.NET Core MVC, Razor, MongoDB BSON repository style. Add two focused domain services: one for editable-field policy and one for battery/passport diffing, then route all app/API battery edits through shared template update helpers. UI tasks stay after state and API tasks so screens render the final rules instead of duplicating logic.

**Tech Stack:** ASP.NET Core MVC on .NET 10, Razor views, MongoDB.Driver BSON documents, QRCoder, Chart.js, xUnit source/layout tests plus focused service tests.

---

## Scope Check

This spec touches several surfaces, but the work is not independent: API behavior, admin UI, registry search, and report labels all depend on the same battery/passport state rules. Keep this as one phased plan and execute in order.

Current phase tracker:

1. Phase 1: Core data and status rules.
2. Phase 2: APIs and template-driven editing.
3. Phase 3: Admin, registry, reports, and UX polish.

## File Map

- `web/Services/EditableFieldPolicyService.cs`: replace the local-admin-only policy with a global policy that tracks creation, after-creation, and local-admin edit permissions.
- `web/Services/BatteryPassportDeltaService.cs`: compare battery documents to latest passport snapshots and set or clear `app.snapshot.newPassportRequired`.
- `web/Services/BatteryTemplateUpdateService.cs`: validate and apply Battery Model and Software Version updates to battery documents.
- `web/Services/BatteryRepository.cs`: add search helpers, cluster-scoped lookup helpers, and lightweight update helpers for battery records.
- `web/Services/PassportRepository.cs`: add user-facing status labels, battery-history helpers, and search updates.
- `web/Services/ProductTemplateService.cs`: push family/model changes to battery records, not only passport snapshots.
- `web/Controllers/AdminController.cs`: enforce battery creation rules, update battery-level cluster assignment, wire Editable Fields, and global admin history editing.
- `web/Controllers/ClusterAdminController.cs`: switch local admin edits to battery/latest-passport workflow and mirror the global Users tab UX.
- `web/Controllers/ExternalApiController.cs`: add battery passport list, software update endpoint, stricter Battery Model endpoint, tolerant telemetry parsing, and token scope behavior.
- `web/Controllers/RegistryController.cs`: implement battery/passport/cluster search routing and global-admin-only cluster search messaging.
- `web/Controllers/HomeController.cs`: ensure public sample battery opens the public latest summary without login.
- `web/Controllers/LoginController.cs`: add temporary forgot-password request routes.
- `web/Services/AuthService.cs`: store forgot-password reset requests in MongoDB.
- `web/Views/Admin/Clusters.cshtml`: update Batteries, cluster assignments, Registered Clusters, Users, Editable Fields, token labels, select styling.
- `web/Views/ClusterAdmin/Users.cshtml`: mirror `/admin/clusters?tab=users` layout with scoped data.
- `web/Views/Passport/Summary.cshtml`: three-column header, QR, heading, chart legends.
- `web/Views/Passport/Detail.cshtml`: chart/telemetry resilience and user-facing dirty labels.
- `web/Views/Registry/Index.cshtml`: search messages and admin history links.
- `web/Views/Help/Index.cshtml`: API endpoint dropdown/help and telemetry examples.
- `web/wwwroot/css/site.css`: responsive report, table, select, and editable-fields styling.
- `BatteryPassWeb.Tests/*`: update existing source/layout tests and add policy/service tests listed below.

---

### Task 1: Core Labels And Editable Field Policy

**Files:**
- Create: `web/Services/EditableFieldPolicyService.cs`
- Modify: `web/Services/LocalAdminEditableFieldPolicyService.cs`
- Modify: `web/Program.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs`
- Modify: `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Create: `BatteryPassWeb.Tests/PassportStatusLabelTests.cs`

- [ ] **Step 1: Write failing policy tests**

Add these tests to `BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs`:

```csharp
[Fact]
public void EditableFieldPolicy_ShouldSeedCreationAfterCreationAndLocalAdminDefaults()
{
    var policy = EditableFieldPolicyService.CreateDefaultPolicy();

    Assert.True(policy.IsEditableAtCreation("general.clusterId"));
    Assert.True(policy.IsEditableAtCreation("general.batteryFamily"));
    Assert.True(policy.IsEditableAtCreation("general.batteryModel"));
    Assert.True(policy.IsEditableAtCreation("general.serialNumber"));
    Assert.True(policy.IsEditableAtCreation("general.manufacturedDate"));
    Assert.True(policy.IsEditableAtCreation("general.facilityId"));
    Assert.True(policy.IsEditableAtCreation("general.manufacturedBy"));
    Assert.True(policy.IsEditableAtCreation("software.version"));

    Assert.True(policy.IsEditableAfterCreation("general.batteryModel"));
    Assert.True(policy.IsEditableAfterCreation("general.facilityId"));
    Assert.True(policy.IsEditableAfterCreation("software.version"));
    Assert.True(policy.IsEditableAfterCreation("general.clusterId"));

    Assert.True(policy.IsEditableByLocalAdmin("general.batteryModel"));
    Assert.True(policy.IsEditableByLocalAdmin("general.facilityId"));
    Assert.True(policy.IsEditableByLocalAdmin("software.version"));
    Assert.True(policy.IsEditableByLocalAdmin("general.clusterId"));
}

[Fact]
public void EditableFieldPolicy_ShouldEnforcePermissionDependencies()
{
    var policy = EditableFieldPolicyService.CreateDefaultPolicy(
        new[]
        {
            new EditableFieldPermission("general.facilityId", false, true, true),
            new EditableFieldPermission("software.version", false, false, true),
            new EditableFieldPermission("general.clusterId", false, false, false)
        });

    var facility = policy.PermissionByKey["general.facilityId"];
    Assert.True(facility.EditableAtCreation);
    Assert.True(facility.EditableAfterCreation);
    Assert.True(facility.EditableByLocalAdmin);

    var software = policy.PermissionByKey["software.version"];
    Assert.True(software.EditableAtCreation);
    Assert.True(software.EditableAfterCreation);
    Assert.True(software.EditableByLocalAdmin);

    var cluster = policy.PermissionByKey["general.clusterId"];
    Assert.False(cluster.EditableAtCreation);
    Assert.False(cluster.EditableAfterCreation);
    Assert.False(cluster.EditableByLocalAdmin);
}
```

- [ ] **Step 2: Write failing label/layout tests**

Create `BatteryPassWeb.Tests/PassportStatusLabelTests.cs`:

```csharp
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportStatusLabelTests
{
    [Fact]
    public void PassportRepositoryStatusLabel_ShouldShowAwaitingSignOffForDirtyState()
    {
        var document = new BsonDocument
        {
            ["registryInfo"] = new BsonDocument { ["status"] = "draft" },
            ["trust"] = new BsonDocument
            {
                ["state"] = "dirty",
                ["isDirty"] = true
            }
        };

        Assert.Equal("Awaiting sign-off", PassportRepository.BuildPassportStatusLabel(document));
    }
}
```

Add layout assertions to `AdminDenseConsoleLayoutTests.AdminLocalEditableFields_ShouldUseConsolePolicyLayout`:

```csharp
Assert.Contains("Editable fields", localEditableTab);
Assert.Contains("name=\"editableAtCreationFieldKeys\"", localEditableTab);
Assert.Contains("name=\"editableAfterCreationFieldKeys\"", localEditableTab);
Assert.Contains("name=\"editableByLocalAdminFieldKeys\"", localEditableTab);
Assert.DoesNotContain("Local editable fields", localEditableTab);
```

- [ ] **Step 3: Run focused tests to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "EditableFieldPolicy|PassportRepositoryStatusLabel|AdminLocalEditableFields"
```

Expected: FAIL because `EditableFieldPolicyService`, `EditableFieldPermission`, `PermissionByKey`, and `BuildPassportStatusLabel` do not exist and the view still uses local-editable-only controls.

- [ ] **Step 4: Add editable policy model/service**

Create `web/Services/EditableFieldPolicyService.cs`:

```csharp
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed record EditableFieldPermission(
    string FieldKey,
    bool EditableAtCreation,
    bool EditableAfterCreation,
    bool EditableByLocalAdmin);

public sealed class EditableFieldPolicySnapshot
{
    public string PolicyKey { get; init; } = EditableFieldPolicyService.PolicyKey;
    public string UpdatedAt { get; init; } = string.Empty;
    public string UpdatedBy { get; init; } = string.Empty;
    public IReadOnlyList<DataRequirementSection> Sections { get; init; } = [];
    public IReadOnlyDictionary<string, EditableFieldPermission> PermissionByKey { get; init; }
        = new Dictionary<string, EditableFieldPermission>(StringComparer.OrdinalIgnoreCase);

    public bool IsEditableAtCreation(string fieldKey) =>
        PermissionByKey.TryGetValue(fieldKey, out var permission) && permission.EditableAtCreation;

    public bool IsEditableAfterCreation(string fieldKey) =>
        PermissionByKey.TryGetValue(fieldKey, out var permission) && permission.EditableAfterCreation;

    public bool IsEditableByLocalAdmin(string fieldKey) =>
        PermissionByKey.TryGetValue(fieldKey, out var permission) && permission.EditableByLocalAdmin;
}

public sealed class EditableFieldPolicyService
{
    public const string PolicyKey = "editableFields:v1";

    public static readonly IReadOnlyDictionary<string, string[]> FieldPathsByKey =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["general.clusterId"] = ["clusterId"],
            ["general.batteryFamily"] = ["identity.batteryFamily", "app.product.productName"],
            ["general.batteryModel"] = ["identity.batteryModel", "app.product.productVersion", "app.product.batteryModel"],
            ["general.serialNumber"] = ["identity.serialNumber", "app.display.serialNumber"],
            ["general.manufacturedDate"] = ["aspects.generalProductInformation.payload.manufacturingDate"],
            ["general.facilityId"] = ["app.display.facilityId"],
            ["general.manufacturedBy"] = ["app.display.manufacturerName"],
            ["software.version"] = ["app.product.softwareVersion"]
        };

    private static readonly string[] DefaultCreationKeys =
    [
        "general.clusterId",
        "general.batteryFamily",
        "general.batteryModel",
        "general.serialNumber",
        "general.manufacturedDate",
        "general.facilityId",
        "general.manufacturedBy",
        "software.version"
    ];

    private static readonly string[] DefaultAfterCreationKeys =
    [
        "general.batteryModel",
        "general.facilityId",
        "software.version",
        "general.clusterId"
    ];

    private readonly MongoContext _mongoContext;

    public EditableFieldPolicyService(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public static EditableFieldPolicySnapshot CreateDefaultPolicy(
        IReadOnlyCollection<EditableFieldPermission>? permissions = null,
        string updatedAt = "",
        string updatedBy = "system")
    {
        var metadata = DataCompletionPolicyService.CreateDefaultPolicy();
        var knownKeys = metadata.Sections
            .SelectMany(section => section.Fields)
            .Select(field => field.FieldKey)
            .Concat(FieldPathsByKey.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var permissionMap = knownKeys.ToDictionary(
            key => key,
            key => new EditableFieldPermission(
                key,
                DefaultCreationKeys.Contains(key, StringComparer.OrdinalIgnoreCase),
                DefaultAfterCreationKeys.Contains(key, StringComparer.OrdinalIgnoreCase),
                DefaultAfterCreationKeys.Contains(key, StringComparer.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var permission in permissions ?? [])
        {
            if (!knownKeys.Contains(permission.FieldKey))
            {
                continue;
            }

            var normalized = Normalize(permission);
            permissionMap[normalized.FieldKey] = normalized;
        }

        return new EditableFieldPolicySnapshot
        {
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy,
            Sections = metadata.Sections,
            PermissionByKey = permissionMap
        };
    }

    public async Task<EditableFieldPolicySnapshot> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return CreateDefaultPolicy();
        }

        var document = await collection
            .Find(Builders<BsonDocument>.Filter.Eq("policyKey", PolicyKey))
            .FirstOrDefaultAsync(cancellationToken);

        if (document == null)
        {
            var defaultPolicy = CreateDefaultPolicy();
            await SavePolicyAsync(defaultPolicy.PermissionByKey.Values.ToList(), "system", cancellationToken);
            return defaultPolicy;
        }

        return FromBsonDocument(document);
    }

    public async Task SavePolicyAsync(
        IReadOnlyCollection<EditableFieldPermission> permissions,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        var policy = CreateDefaultPolicy(permissions, DateTimeOffset.UtcNow.ToString("O"), string.IsNullOrWhiteSpace(actor) ? "system" : actor);
        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("policyKey", PolicyKey),
            ToBsonDocument(policy),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public static BsonDocument ToBsonDocument(EditableFieldPolicySnapshot policy) =>
        new()
        {
            ["policyKey"] = PolicyKey,
            ["updatedAt"] = policy.UpdatedAt,
            ["updatedBy"] = policy.UpdatedBy,
            ["permissions"] = new BsonArray(policy.PermissionByKey.Values.Select(permission => new BsonDocument
            {
                ["fieldKey"] = permission.FieldKey,
                ["editableAtCreation"] = permission.EditableAtCreation,
                ["editableAfterCreation"] = permission.EditableAfterCreation,
                ["editableByLocalAdmin"] = permission.EditableByLocalAdmin
            }))
        };

    public static EditableFieldPolicySnapshot FromBsonDocument(BsonDocument document)
    {
        var permissions = (document.GetValue("permissions", new BsonArray()) as BsonArray ?? new BsonArray())
            .OfType<BsonDocument>()
            .Select(item => new EditableFieldPermission(
                BsonHelpers.GetString(item, "fieldKey"),
                item.GetValue("editableAtCreation", false).ToBoolean(),
                item.GetValue("editableAfterCreation", false).ToBoolean(),
                item.GetValue("editableByLocalAdmin", false).ToBoolean()))
            .Where(permission => !string.IsNullOrWhiteSpace(permission.FieldKey))
            .ToList();

        return CreateDefaultPolicy(permissions, BsonHelpers.GetString(document, "updatedAt"), BsonHelpers.GetString(document, "updatedBy"));
    }

    private static EditableFieldPermission Normalize(EditableFieldPermission permission)
    {
        var atCreation = permission.EditableAtCreation || permission.EditableAfterCreation || permission.EditableByLocalAdmin;
        var afterCreation = permission.EditableAfterCreation || permission.EditableByLocalAdmin;
        return permission with
        {
            EditableAtCreation = atCreation,
            EditableAfterCreation = afterCreation
        };
    }

    private IMongoCollection<BsonDocument>? GetCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("editableFieldPolicies");
}
```

- [ ] **Step 5: Keep compatibility wrapper and register DI**

Modify `web/Services/LocalAdminEditableFieldPolicyService.cs` so existing callers compile while they are migrated:

```csharp
public sealed class LocalAdminEditableFieldPolicyService
{
    private readonly EditableFieldPolicyService _editableFieldPolicyService;

    public LocalAdminEditableFieldPolicyService(EditableFieldPolicyService editableFieldPolicyService)
    {
        _editableFieldPolicyService = editableFieldPolicyService;
    }

    public static LocalAdminEditableFieldPolicySnapshot CreateDefaultPolicy(
        IReadOnlyCollection<string>? editableFieldKeys = null,
        string updatedAt = "",
        string updatedBy = "system")
    {
        var permissions = (editableFieldKeys ?? [])
            .Select(key => new EditableFieldPermission(key, true, true, true))
            .ToList();
        var policy = EditableFieldPolicyService.CreateDefaultPolicy(permissions, updatedAt, updatedBy);
        return LocalAdminEditableFieldPolicySnapshot.FromEditablePolicy(policy);
    }

    public async Task<LocalAdminEditableFieldPolicySnapshot> GetPolicyAsync(CancellationToken cancellationToken = default) =>
        LocalAdminEditableFieldPolicySnapshot.FromEditablePolicy(await _editableFieldPolicyService.GetPolicyAsync(cancellationToken));

    public Task SavePolicyAsync(IReadOnlyCollection<string> editableFieldKeys, string actor, CancellationToken cancellationToken = default) =>
        _editableFieldPolicyService.SavePolicyAsync(
            editableFieldKeys.Select(key => new EditableFieldPermission(key, true, true, true)).ToList(),
            actor,
            cancellationToken);
}
```

Add to `LocalAdminEditableFieldPolicySnapshot`:

```csharp
public static LocalAdminEditableFieldPolicySnapshot FromEditablePolicy(EditableFieldPolicySnapshot policy) =>
    new()
    {
        PolicyKey = LocalAdminEditableFieldPolicyService.PolicyKey,
        UpdatedAt = policy.UpdatedAt,
        UpdatedBy = policy.UpdatedBy,
        Sections = policy.Sections,
        EditableFieldKeys = policy.PermissionByKey.Values
            .Where(permission => permission.EditableByLocalAdmin)
            .Select(permission => permission.FieldKey)
            .ToList()
    };
```

Register before the compatibility wrapper in `web/Program.cs`:

```csharp
builder.Services.AddSingleton<EditableFieldPolicyService>();
builder.Services.AddSingleton<LocalAdminEditableFieldPolicyService>();
```

- [ ] **Step 6: Add user-facing status label helper**

Modify `web/Services/PassportRepository.cs`:

```csharp
public static string BuildPassportStatusLabel(BsonDocument document)
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

    if (BsonHelpers.GetString(document, "trust", "state").Equals(TrustState.Dirty, StringComparison.OrdinalIgnoreCase)
        || document.GetValue("trust", new BsonDocument()) is BsonDocument trust
        && trust.GetValue("isDirty", false).ToBoolean())
    {
        return "Awaiting sign-off";
    }

    var trustState = BsonHelpers.GetString(document, "trust", "state");
    return trustState.Equals(TrustState.Signed, StringComparison.OrdinalIgnoreCase)
        ? "Signed"
        : "Draft";
}
```

Then have the private `BuildPassportStatus` return `BuildPassportStatusLabel(document)`.

- [ ] **Step 7: Update Editable Fields UI names**

In `web/Views/Admin/Clusters.cshtml`, change tab text to `Editable fields`, change headings to `Editable fields`, and update the local editable drawer fields to render three checkboxes per field:

```html
<input type="checkbox" name="editableAtCreationFieldKeys" value="@field.FieldKey" data-editable-at-creation />
<input type="checkbox" name="editableAfterCreationFieldKeys" value="@field.FieldKey" data-editable-after-creation />
<input type="checkbox" name="editableByLocalAdminFieldKeys" value="@field.FieldKey" data-editable-by-local-admin />
```

Add script in the existing Scripts block:

```javascript
document.querySelectorAll('[data-editable-field-row]').forEach((row) => {
    const atCreation = row.querySelector('[data-editable-at-creation]');
    const afterCreation = row.querySelector('[data-editable-after-creation]');
    const localAdmin = row.querySelector('[data-editable-by-local-admin]');
    const sync = (source) => {
        if (source === localAdmin && localAdmin.checked) {
            afterCreation.checked = true;
            atCreation.checked = true;
        }
        if (source === afterCreation && afterCreation.checked) {
            atCreation.checked = true;
        }
        if (source === atCreation && !atCreation.checked) {
            afterCreation.checked = false;
            localAdmin.checked = false;
        }
        if (source === afterCreation && !afterCreation.checked) {
            localAdmin.checked = false;
        }
    };
    [atCreation, afterCreation, localAdmin].forEach((input) => input?.addEventListener('change', () => sync(input)));
});
```

- [ ] **Step 8: Run tests and commit**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "EditableFieldPolicy|PassportRepositoryStatusLabel|AdminLocalEditableFields"
```

Expected: PASS.

Commit:

```powershell
git add web/Services/EditableFieldPolicyService.cs web/Services/LocalAdminEditableFieldPolicyService.cs web/Program.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Views/Admin/Clusters.cshtml web/Services/PassportRepository.cs BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/PassportStatusLabelTests.cs
git commit -m "feat: add editable field policy and status labels"
```

---

### Task 2: Battery Passport Delta Service And Core Battery Rules

**Files:**
- Create: `web/Services/BatteryPassportDeltaService.cs`
- Modify: `web/Program.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Services/BatteryRepository.cs`
- Modify: `web/Services/BatteryPassportSnapshotService.cs`
- Modify: `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`
- Modify: `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`

- [ ] **Step 1: Write failing service tests**

Add to `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`:

```csharp
[Fact]
public void BatteryPassportDeltaService_ShouldDetectAndClearEditableDifferences()
{
    var battery = new BsonDocument
    {
        ["batteryId"] = "battery-1",
        ["clusterId"] = "cluster-a",
        ["identity"] = new BsonDocument { ["batteryModel"] = "2.0" },
        ["app"] = new BsonDocument
        {
            ["display"] = new BsonDocument { ["facilityId"] = "line-a" },
            ["product"] = new BsonDocument { ["softwareVersion"] = "4.0" }
        }
    };
    var latestPassport = battery.DeepClone().AsBsonDocument;

    Assert.False(BatteryPassportDeltaService.HasEditableDifferences(battery, latestPassport, EditableFieldPolicyService.CreateDefaultPolicy()));

    battery["clusterId"] = "cluster-b";
    Assert.True(BatteryPassportDeltaService.HasEditableDifferences(battery, latestPassport, EditableFieldPolicyService.CreateDefaultPolicy()));

    battery["clusterId"] = "cluster-a";
    Assert.False(BatteryPassportDeltaService.HasEditableDifferences(battery, latestPassport, EditableFieldPolicyService.CreateDefaultPolicy()));
}
```

Add to `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`:

```csharp
[Fact]
public void AdminBatteryCreate_ShouldRequireClusterAndSnapshotShouldClearPendingFlag()
{
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

    Assert.Contains("Battery cluster is required.", controller);
    Assert.Contains("UpdateNewPassportRequiredAsync", controller);
    Assert.Contains("ClearNewPassportRequiredAsync", controller);
    Assert.DoesNotContain("snapshot[\"newPassportRequired\"] = true;", controller);
}
```

- [ ] **Step 2: Run focused tests to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "BatteryPassportDeltaService|AdminBatteryCreate_ShouldRequireCluster"
```

Expected: FAIL because the service and new controller calls do not exist.

- [ ] **Step 3: Create delta service**

Create `web/Services/BatteryPassportDeltaService.cs`:

```csharp
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class BatteryPassportDeltaService
{
    private readonly PassportRepository _passportRepository;
    private readonly BatteryRepository _batteryRepository;
    private readonly EditableFieldPolicyService _editableFieldPolicyService;

    public BatteryPassportDeltaService(
        PassportRepository passportRepository,
        BatteryRepository batteryRepository,
        EditableFieldPolicyService editableFieldPolicyService)
    {
        _passportRepository = passportRepository;
        _batteryRepository = batteryRepository;
        _editableFieldPolicyService = editableFieldPolicyService;
    }

    public async Task<bool> UpdateNewPassportRequiredAsync(BsonDocument battery, CancellationToken cancellationToken = default)
    {
        var batteryId = BsonHelpers.GetString(battery, "batteryId");
        var latestPassport = (await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: false, cancellationToken)).FirstOrDefault();
        var policy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
        var required = latestPassport != null && HasEditableDifferences(battery, latestPassport, policy);
        var snapshot = EnsureDocument(EnsureDocument(battery, "app"), "snapshot");
        snapshot["newPassportRequired"] = required;
        snapshot["requiredSince"] = required ? DateTimeOffset.UtcNow.ToString("O") : BsonNull.Value;
        snapshot["reason"] = required ? "editable-fields-differ-from-latest-passport" : string.Empty;
        await _batteryRepository.ReplaceAsync(batteryId, battery, cancellationToken);
        return required;
    }

    public async Task ClearNewPassportRequiredAsync(string batteryId, string latestPassportId, CancellationToken cancellationToken = default)
    {
        await _batteryRepository.UpdateBatteryFieldsAsync(
            batteryId,
            new Dictionary<string, BsonValue>
            {
                ["app.snapshot.newPassportRequired"] = false,
                ["app.snapshot.requiredSince"] = BsonNull.Value,
                ["app.snapshot.reason"] = string.Empty,
                ["app.snapshot.latestPassportId"] = latestPassportId,
                ["app.snapshot.lastPassportCreatedAt"] = DateTimeOffset.UtcNow.ToString("O")
            },
            cancellationToken);
    }

    public static bool HasEditableDifferences(BsonDocument battery, BsonDocument latestPassport, EditableFieldPolicySnapshot policy)
    {
        foreach (var permission in policy.PermissionByKey.Values.Where(permission => permission.EditableAfterCreation))
        {
            if (!EditableFieldPolicyService.FieldPathsByKey.TryGetValue(permission.FieldKey, out var paths))
            {
                continue;
            }

            foreach (var path in paths)
            {
                if (!BsonValueEquals(ReadPath(battery, path), ReadPath(latestPassport, path)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static BsonValue ReadPath(BsonDocument document, string path)
    {
        BsonValue current = document;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is not BsonDocument currentDocument || !currentDocument.TryGetValue(segment, out current))
            {
                return BsonNull.Value;
            }
        }

        return current;
    }

    private static bool BsonValueEquals(BsonValue left, BsonValue right) =>
        BsonText(left).Equals(BsonText(right), StringComparison.OrdinalIgnoreCase);

    private static string BsonText(BsonValue value) =>
        value == null || value.IsBsonNull ? string.Empty : value.ToString() ?? string.Empty;

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

Register in `web/Program.cs`:

```csharp
builder.Services.AddSingleton<BatteryPassportDeltaService>();
```

- [ ] **Step 4: Enforce cluster on battery creation**

Inject `BatteryPassportDeltaService` into `AdminController`.

In `CreateBattery`, add before building the battery:

```csharp
var clusterId = Text(form, "clusterId");
if (string.IsNullOrWhiteSpace(clusterId))
{
    return Redirect($"/admin/batteries/new?error={Uri.EscapeDataString("Battery cluster is required.")}");
}
```

Use `ClusterId = clusterId` in `ProductTemplateBatteryIdentity`.

- [ ] **Step 5: Replace sticky pending flag logic**

In `SaveBattery`, replace direct pending flag writes with:

```csharp
await _batteryPassportDeltaService.UpdateNewPassportRequiredAsync(battery, cancellationToken);
TempData["StatusMessage"] = "Battery data saved.";
return Redirect("/admin/clusters?tab=batteries");
```

In `CreateBatteryPassport`, replace manual snapshot update with:

```csharp
await _batteryPassportDeltaService.ClearNewPassportRequiredAsync(
    BsonHelpers.GetString(battery, "batteryId"),
    passportId,
    cancellationToken);
```

- [ ] **Step 6: Ensure snapshot inherits current cluster**

In `BatteryPassportSnapshotService.CreatePassportSnapshotAsync`, keep the current behavior that reads `clusterId` from `battery`. Add this regression test to `BatterySnapshotWorkflowTests.cs`:

```csharp
[Fact]
public void BatteryPassportSnapshotService_ShouldCopyCurrentBatteryCluster()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));

    Assert.Contains("[\"clusterId\"] = BsonHelpers.GetString(battery, \"clusterId\")", source);
    Assert.DoesNotContain("previousPassport", source);
}
```

- [ ] **Step 7: Run tests and commit**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "BatteryPassportDeltaService|AdminBatteryCreate_ShouldRequireCluster|BatteryPassportSnapshotService_ShouldCopyCurrentBatteryCluster"
```

Expected: PASS.

Commit:

```powershell
git add web/Services/BatteryPassportDeltaService.cs web/Program.cs web/Controllers/AdminController.cs web/Services/BatteryRepository.cs web/Services/BatteryPassportSnapshotService.cs BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
git commit -m "feat: derive new passport needed from battery deltas"
```

---

### Task 3: Local Admin Latest Workflow And History Permissions

**Files:**
- Modify: `web/Services/AccessControlService.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Views/Admin/BatteryPassports.cshtml`
- Modify: `web/Views/ClusterAdmin/Passports.cshtml`
- Modify: `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`
- Modify: `BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs`

- [ ] **Step 1: Write failing permission tests**

Add to `SigningWorkflowLayoutTests.cs`:

```csharp
[Fact]
public void LocalAdmins_ShouldViewHistoryReadOnlyAndOnlyEditLatestCurrentPassport()
{
    var access = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
    var clusterController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
    var adminHistory = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));

    Assert.Contains("CanViewBatteryHistoryAsync", access);
    Assert.Contains("CanEditLatestBatteryPassportAsync", access);
    Assert.Contains("isLatestForBattery", clusterController);
    Assert.Contains("readonly history", adminHistory, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run focused test to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter LocalAdmins_ShouldViewHistoryReadOnlyAndOnlyEditLatestCurrentPassport
```

Expected: FAIL because the explicit helpers and read-only copy are missing.

- [ ] **Step 3: Add access helpers**

Add to `AccessControlService`:

```csharp
public async Task<bool> CanViewBatteryHistoryAsync(ClaimsPrincipal user, string clusterId, CancellationToken cancellationToken = default)
{
    if (IsAdmin(user))
    {
        return true;
    }

    return await CanAdministerClusterAsync(user, clusterId, cancellationToken);
}

public async Task<bool> CanEditLatestBatteryPassportAsync(ClaimsPrincipal user, BsonDocument passport, CancellationToken cancellationToken = default)
{
    if (IsAdmin(user))
    {
        return true;
    }

    return passport.GetValue("isLatestForBattery", false).ToBoolean()
        && await CanAdministerClusterAsync(user, BsonHelpers.GetString(passport, "clusterId"), cancellationToken);
}
```

- [ ] **Step 4: Gate cluster-admin edit routes**

In `ClusterAdminController.EditPassport` and `SavePassport`, after cluster access succeeds, require latest:

```csharp
if (!await _accessControlService.CanEditLatestBatteryPassportAsync(User, document, cancellationToken))
{
    return Forbid();
}
```

For history pages shown to local admins, render history actions without edit/archive controls. Keep global admin archive/unarchive/edit controls.

- [ ] **Step 5: Allow local admin create/validate/sign/publish on latest/current in scope**

Add cluster-admin routes or reuse existing trust endpoints with `CanEditLatestBatteryPassportAsync`:

```csharp
if (!await _accessControlService.CanEditLatestBatteryPassportAsync(User, passport, cancellationToken))
{
    return Forbid();
}
```

Use the existing `PassportTrustWorkflowService.ValidateAsync`, `SignAsync`, and `PublishAsync` calls so behavior stays identical.

- [ ] **Step 6: Run tests and commit**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "LocalAdmins_ShouldViewHistoryReadOnlyAndOnlyEditLatestCurrentPassport|PublicVisibility"
```

Expected: PASS.

Commit:

```powershell
git add web/Services/AccessControlService.cs web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs web/Views/Admin/BatteryPassports.cshtml web/Views/ClusterAdmin/Passports.cshtml BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs
git commit -m "feat: scope local admin passport history workflow"
```

---

### Task 4: Template-Driven Battery Model And Software Updates

**Files:**
- Create: `web/Services/BatteryTemplateUpdateService.cs`
- Modify: `web/Program.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ExternalApiController.cs`
- Modify: `web/Services/ProductTemplateModels.cs`
- Modify: `BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs`
- Modify: `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `ExternalApiBatteryVersionTests.cs`:

```csharp
[Fact]
public void ExternalApi_ShouldRejectUndefinedBatteryModelAndExposeSoftwareVersionEndpoint()
{
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
    var updateService = File.ReadAllText(RepoFile("web", "Services", "BatteryTemplateUpdateService.cs"));

    Assert.Contains("UpdateBatteryModel", controller);
    Assert.Contains("Unknown Battery Model", controller);
    Assert.Contains("[HttpPatch(\"batteries/{batteryId}/software-version\")]", controller);
    Assert.Contains("ApplyBatteryModelAsync", updateService);
    Assert.Contains("ApplySoftwareVersionAsync", updateService);
}

[Fact]
public void BatteryTemplateUpdateService_ShouldCopySoftwareReleaseMetadata()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "BatteryTemplateUpdateService.cs"));

    Assert.Contains("[\"app.product.softwareReleaseDate\"]", source);
    Assert.Contains("[\"app.product.softwareLatestUpdate\"]", source);
    Assert.Contains("Unknown Software Version", source);
}
```

- [ ] **Step 2: Run focused tests to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "ExternalApi_ShouldRejectUndefinedBatteryModel|BatteryTemplateUpdateService_ShouldCopySoftwareReleaseMetadata"
```

Expected: FAIL because the service and software endpoint do not exist.

- [ ] **Step 3: Create shared update service**

Create `web/Services/BatteryTemplateUpdateService.cs`:

```csharp
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed record BatteryTemplateUpdateResult(bool Success, string Message, BsonDocument Battery);

public sealed class BatteryTemplateUpdateService
{
    private readonly ProductTemplateService _productTemplateService;
    private readonly BatteryPassportDeltaService _batteryPassportDeltaService;

    public BatteryTemplateUpdateService(
        ProductTemplateService productTemplateService,
        BatteryPassportDeltaService batteryPassportDeltaService)
    {
        _productTemplateService = productTemplateService;
        _batteryPassportDeltaService = batteryPassportDeltaService;
    }

    public async Task<BatteryTemplateUpdateResult> ApplyBatteryModelAsync(
        BsonDocument battery,
        string requestedBatteryModel,
        CancellationToken cancellationToken = default)
    {
        var productId = FirstNonEmpty(
            BsonHelpers.GetString(battery, "identity", "productId"),
            BsonHelpers.GetString(battery, "app", "product", "productId"),
            BatteryProductTemplateCatalog.DefaultProductId);
        var product = await _productTemplateService.GetProductAsync(productId, cancellationToken);
        var selectedVersion = product?.ProductVersions.FirstOrDefault(version =>
            version.Version.Equals(requestedBatteryModel.Trim(), StringComparison.OrdinalIgnoreCase));
        if (product == null || selectedVersion == null)
        {
            return new BatteryTemplateUpdateResult(false, "Unknown Battery Model.", battery);
        }

        ProductTemplatePassportBuilder.ApplyProductVersionToBattery(battery, product, selectedVersion, DateTimeOffset.UtcNow.ToString("O"));
        await _batteryPassportDeltaService.UpdateNewPassportRequiredAsync(battery, cancellationToken);
        return new BatteryTemplateUpdateResult(true, "Battery Model updated.", battery);
    }

    public async Task<BatteryTemplateUpdateResult> ApplySoftwareVersionAsync(
        BsonDocument battery,
        string requestedSoftwareVersion,
        CancellationToken cancellationToken = default)
    {
        var productId = FirstNonEmpty(
            BsonHelpers.GetString(battery, "identity", "productId"),
            BsonHelpers.GetString(battery, "app", "product", "productId"),
            BatteryProductTemplateCatalog.DefaultProductId);
        var batteryModel = FirstNonEmpty(
            BsonHelpers.GetString(battery, "identity", "batteryModel"),
            BsonHelpers.GetString(battery, "app", "product", "productVersion"));
        var product = await _productTemplateService.GetProductAsync(productId, cancellationToken);
        var selectedVersion = product?.ProductVersions.FirstOrDefault(version =>
            version.Version.Equals(batteryModel, StringComparison.OrdinalIgnoreCase)
            && version.SoftwareVersion.Equals(requestedSoftwareVersion.Trim(), StringComparison.OrdinalIgnoreCase));
        if (product == null || selectedVersion == null)
        {
            return new BatteryTemplateUpdateResult(false, "Unknown Software Version.", battery);
        }

        var setValues = new Dictionary<string, BsonValue>
        {
            ["app.product.softwareVersion"] = selectedVersion.SoftwareVersion,
            ["app.product.softwareReleaseDate"] = selectedVersion.SoftwareReleaseDate,
            ["app.product.softwareLatestUpdate"] = selectedVersion.SoftwareLatestUpdate,
            ["identity.softwareVersion"] = selectedVersion.SoftwareVersion,
            ["updatedAt"] = DateTimeOffset.UtcNow.ToString("O")
        };
        foreach (var pair in setValues)
        {
            SetPath(battery, pair.Key, pair.Value);
        }

        await _batteryPassportDeltaService.UpdateNewPassportRequiredAsync(battery, cancellationToken);
        return new BatteryTemplateUpdateResult(true, "Software Version updated.", battery);
    }

    private static void SetPath(BsonDocument document, string path, BsonValue value)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = document;
        foreach (var segment in segments.Take(segments.Length - 1))
        {
            if (!current.TryGetValue(segment, out var child) || child is not BsonDocument childDocument)
            {
                childDocument = new BsonDocument();
                current[segment] = childDocument;
            }

            current = childDocument;
        }

        current[segments.Last()] = value;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
```

Add `ApplyProductVersionToBattery` to `ProductTemplatePassportBuilder` in `ProductTemplateModels.cs`:

```csharp
public static void ApplyProductVersionToBattery(BsonDocument battery, BatteryProductTemplate product, BatteryProductVersion productVersion, string now)
{
    var identity = new ProductTemplateBatteryIdentity
    {
        ClusterId = BsonHelpers.GetString(battery, "clusterId"),
        ModelNumber = BsonHelpers.GetString(battery, "app", "display", "modelNumber"),
        SerialNumber = BsonHelpers.GetString(battery, "identity", "serialNumber"),
        DisplayName = BsonHelpers.GetString(battery, "app", "display", "name"),
        FacilityId = BsonHelpers.GetString(battery, "app", "display", "facilityId"),
        ManufacturingDate = BsonHelpers.GetString(battery, "aspects", "generalProductInformation", "payload", "manufacturingDate")
    };
    var fresh = BuildBatteryFromTemplate(BsonHelpers.GetString(battery, "batteryId"), product, productVersion, identity, now);
    battery.Clear();
    foreach (var element in fresh)
    {
        battery[element.Name] = element.Value.DeepClone();
    }
}
```

Register:

```csharp
builder.Services.AddSingleton<BatteryTemplateUpdateService>();
```

- [ ] **Step 4: Route admin and API through shared service**

Inject `BatteryTemplateUpdateService` into `AdminController` and `ExternalApiController`.

In `AdminController.SaveBattery`, replace inline model/software writes with service calls:

```csharp
var modelResult = await _batteryTemplateUpdateService.ApplyBatteryModelAsync(battery, requestedBatteryModel, cancellationToken);
if (!modelResult.Success)
{
    return Redirect($"/admin/batteries/{Uri.EscapeDataString(decodedBatteryId)}/edit?error={Uri.EscapeDataString(modelResult.Message)}");
}

var softwareVersion = Text(Request.Form, "softwareVersion");
if (!string.IsNullOrWhiteSpace(softwareVersion))
{
    var softwareResult = await _batteryTemplateUpdateService.ApplySoftwareVersionAsync(battery, softwareVersion, cancellationToken);
    if (!softwareResult.Success)
    {
        return Redirect($"/admin/batteries/{Uri.EscapeDataString(decodedBatteryId)}/edit?error={Uri.EscapeDataString(softwareResult.Message)}");
    }
}
```

In `ExternalApiController.UpdateBatteryModel`, replace direct path updates with:

```csharp
var result = await _batteryTemplateUpdateService.ApplyBatteryModelAsync(auth.Battery!, requestedBatteryModel, cancellationToken);
if (!result.Success)
{
    return Envelope(StatusCodes.Status400BadRequest, result.Message);
}

return Envelope(StatusCodes.Status200OK, "Battery Model updated on the battery record.", new { batteryId, newPassportRequired = true });
```

Add endpoint:

```csharp
[HttpPatch("batteries/{batteryId}/software-version")]
public async Task<IActionResult> UpdateSoftwareVersion(string batteryId, [FromBody] JsonElement payload, CancellationToken cancellationToken)
{
    var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Write, cancellationToken);
    if (auth.ErrorResult != null)
    {
        return auth.ErrorResult;
    }

    if (payload.ValueKind != JsonValueKind.Object
        || !TryGetPropertyIgnoreCase(payload, "softwareVersion", out var softwareVersionElement)
        || softwareVersionElement.ValueKind != JsonValueKind.String
        || string.IsNullOrWhiteSpace(softwareVersionElement.GetString()))
    {
        return Envelope(StatusCodes.Status400BadRequest, "softwareVersion is required and must be a string.");
    }

    var result = await _batteryTemplateUpdateService.ApplySoftwareVersionAsync(auth.Battery!, softwareVersionElement.GetString()!, cancellationToken);
    return Envelope(result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, result.Message, new { batteryId });
}
```

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "ExternalApi_ShouldRejectUndefinedBatteryModel|BatteryTemplateUpdateService_ShouldCopySoftwareReleaseMetadata|ProductTemplate"
```

Expected: PASS.

Commit:

```powershell
git add web/Services/BatteryTemplateUpdateService.cs web/Program.cs web/Controllers/AdminController.cs web/Controllers/ExternalApiController.cs web/Services/ProductTemplateModels.cs BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs BatteryPassWeb.Tests/ProductTemplateServiceTests.cs
git commit -m "feat: validate template driven battery updates"
```

---

### Task 5: Product Template Push Updates Batteries

**Files:**
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Services/ProductTemplateModels.cs`
- Modify: `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `ProductTemplateServiceTests.cs`:

```csharp
[Fact]
public void ProductTemplatePush_ShouldTargetBatteryRecordsAndOnlyFlagChangedBatteries()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

    Assert.Contains("BatteryCollection()", source);
    Assert.Contains("UpdateNewPassportRequiredAsync", source);
    Assert.Contains("changedBatteryIds", source);
    Assert.Contains("if (result.UpdatedPaths.Count == 0)", source);
    Assert.DoesNotContain("MarkCanonicalDirtyAsync(passportId, \"productVersionTemplatePushed\"", source);
}
```

- [ ] **Step 2: Run focused test to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter ProductTemplatePush_ShouldTargetBatteryRecordsAndOnlyFlagChangedBatteries
```

Expected: FAIL because push still targets passports and marks them dirty.

- [ ] **Step 3: Modify push algorithm**

Inject `BatteryPassportDeltaService` into `ProductTemplateService`.

Change `PushProductVersionAsync` to load batteries matching:

```csharp
Builders<BsonDocument>.Filter.And(
    Builders<BsonDocument>.Filter.Eq("app.product.productId", product.ProductId),
    Builders<BsonDocument>.Filter.Eq("app.product.productVersion", selectedProductVersion.Version))
```

For each battery:

```csharp
var before = battery.DeepClone().AsBsonDocument;
ProductTemplatePassportBuilder.ApplyProductVersionToBattery(battery, product, selectedProductVersion, now);
var result = ProductTemplatePassportBuilder.ComputeSafeTemplateUpdates(before, oldTemplate, ProductTemplatePassportBuilder.BuildTemplateBaseline(battery));
if (result.UpdatedPaths.Count == 0)
{
    continue;
}

await _batteryRepository.ReplaceAsync(BsonHelpers.GetString(battery, "batteryId"), battery, cancellationToken);
await _batteryPassportDeltaService.UpdateNewPassportRequiredAsync(battery, cancellationToken);
changedBatteryIds.Add(BsonHelpers.GetString(battery, "batteryId"));
```

Keep audit run metadata, but write battery IDs to `UpdatedPassportIds` only if the existing view model cannot be changed safely in this task. Prefer adding `UpdatedBatteryIds` to `ProductTemplatePushResult` and keeping the old property for compatibility.

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter ProductTemplatePush_ShouldTargetBatteryRecordsAndOnlyFlagChangedBatteries
```

Expected: PASS.

Commit:

```powershell
git add web/Services/ProductTemplateService.cs web/Services/ProductTemplateModels.cs BatteryPassWeb.Tests/ProductTemplateServiceTests.cs
git commit -m "feat: push model template changes to batteries"
```

---

### Task 6: External API Passport List, Tokens, And Telemetry Template

**Files:**
- Modify: `web/Controllers/ExternalApiController.cs`
- Modify: `web/Services/ExternalApiRepository.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Services/ExternalApiInitializer.cs`
- Modify: `web/Controllers/HelpController.cs`
- Modify: `web/Views/Help/Index.cshtml`
- Modify: `BatteryPassWeb.Tests/ExternalApiInitializerTests.cs`
- Modify: `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`
- Create: `BatteryPassWeb.Tests/ExternalApiTelemetryTemplateTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `ExternalApiBatteryIdTests.cs`:

```csharp
[Fact]
public void ExternalApi_ShouldListBatteryPassportsAndUseCreateValidateSignPublishLabel()
{
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
    var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
    var seed = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

    Assert.Contains("[HttpGet(\"batteries/{batteryId}/passports\")]", controller);
    Assert.Contains("ListBatteryPassports", controller);
    Assert.Contains("create, validate, sign and publish passport", help);
    Assert.Contains("Sample token (create, validate, sign and publish passport)", seed);
}
```

Create `BatteryPassWeb.Tests/ExternalApiTelemetryTemplateTests.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using BatteryPassWeb.Controllers;

namespace BatteryPassWeb.Tests;

public sealed class ExternalApiTelemetryTemplateTests
{
    [Fact]
    public void TelemetryParser_ShouldAcceptSeriesAndIgnoreMalformedPoints()
    {
        var method = typeof(ExternalApiController).GetMethod("TryParseTelemetryPoints", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        using var document = JsonDocument.Parse("""
        {
          "series": {
            "currentConsumptionKwh": [
              { "measuredAt": "2026-05-20T09:00:00Z", "value": 10.5 },
              { "measuredAt": "broken", "value": 12.1 }
            ],
            "currentVoltageV": [
              { "measuredAt": "2026-05-20T09:05:00Z", "value": 398.4 }
            ]
          }
        }
        """);

        object?[] args = [document.RootElement, null!, ""];
        var success = (bool)method.Invoke(null, args)!;

        Assert.True(success);
        Assert.Contains("ignored", args[2]!.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run focused tests to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "ExternalApi_ShouldListBatteryPassports|TelemetryParser_ShouldAcceptSeries"
```

Expected: FAIL because the endpoint, label, and parser behavior are missing.

- [ ] **Step 3: Add battery passports endpoint**

In `ExternalApiController`:

```csharp
[HttpGet("batteries/{batteryId}/passports")]
public async Task<IActionResult> ListBatteryPassports(string batteryId, CancellationToken cancellationToken)
{
    var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Read, cancellationToken);
    if (auth.ErrorResult != null)
    {
        return auth.ErrorResult;
    }

    var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: false, cancellationToken);
    return Envelope(StatusCodes.Status200OK, "Battery passports read successfully.", new
    {
        batteryId,
        passports = passports.Select(passport => new
        {
            passportId = BsonHelpers.GetString(passport, "passportId"),
            status = PassportRepository.BuildPassportStatusLabel(passport),
            createdAt = BsonHelpers.GetString(passport, "snapshot", "createdAt"),
            isLatestForBattery = passport.GetValue("isLatestForBattery", false).ToBoolean()
        }).ToList()
    });
}
```

- [ ] **Step 4: Rename sign token capability labels**

Change sample labels in `ProductTemplateService.EnsureFixedApiDemoTokensAsync`, `ExternalApiInitializer`, `HelpController`, `ExternalApiHelpViewModel` text, and help view from validate/sign/publish to:

```text
create, validate, sign and publish passport
```

Keep `ExternalTokenAccessMode.Sign` as the internal enum for this task.

- [ ] **Step 5: Make telemetry parser tolerant and templated**

Extend `TryParseTelemetryPoints` to accept:

```json
{
  "series": {
    "currentConsumptionKwh": [
      { "measuredAt": "2026-05-20T09:00:00Z", "value": 163.4 }
    ],
    "currentChargeLevelPct": [
      { "measuredAt": "2026-05-20T09:00:00Z", "value": 79.8 }
    ]
  }
}
```

Implementation rule:

```csharp
// Keep existing points/object formats.
// For series format, ignore malformed entries and collect an "ignored N malformed telemetry point(s)" warning.
// Return false only when no valid telemetry point remains.
```

Sort before append:

```csharp
points = points.OrderBy(point => point.MeasuredAtUtc).ToList();
```

Do not update passport trust state or pending passport flags from telemetry.

- [ ] **Step 6: Update API help templates**

In `Views/Help/Index.cshtml`, add:

- `GET /api/external/v1/batteries/{batteryId}/passports`
- `PATCH /api/external/v1/batteries/{batteryId}/software-version`
- single telemetry point example
- multiple points for one variable example
- multiple variables with multiple points example

Add tester templates:

```javascript
readBatteryPassports: {
    method: 'GET',
    path: `${basePath}/batteries/${sampleBatteryId}/passports`,
    body: '',
    tokenPreset: 'sample-read'
},
patchSoftwareVersion: {
    method: 'PATCH',
    path: `${basePath}/batteries/${sampleBatteryId}/software-version`,
    body: JSON.stringify({ softwareVersion: '4.0' }, null, 2),
    tokenPreset: 'sample-read-write'
},
writeTelemetrySeries: {
    method: 'POST',
    path: `${basePath}/batteries/${sampleBatteryId}/telemetry`,
    body: JSON.stringify({
        series: {
            currentConsumptionKwh: [
                { measuredAt: '2026-05-20T09:00:00Z', value: 163.4 },
                { measuredAt: '2026-05-20T10:00:00Z', value: 166.1 }
            ],
            currentChargeLevelPct: [
                { measuredAt: '2026-05-20T09:00:00Z', value: 79.8 }
            ]
        }
    }, null, 2),
    tokenPreset: 'sample-read-write'
}
```

- [ ] **Step 7: Run tests and commit**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "ExternalApi_ShouldListBatteryPassports|TelemetryParser_ShouldAcceptSeries|DemoTokens"
```

Expected: PASS.

Commit:

```powershell
git add web/Controllers/ExternalApiController.cs web/Services/ExternalApiRepository.cs web/Services/ProductTemplateService.cs web/Services/ExternalApiInitializer.cs web/Controllers/HelpController.cs web/Views/Help/Index.cshtml BatteryPassWeb.Tests/ExternalApiInitializerTests.cs BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs BatteryPassWeb.Tests/ExternalApiTelemetryTemplateTests.cs
git commit -m "feat: expand external api passport and telemetry flows"
```

---

### Task 7: Registry And Managed Passport Search

**Files:**
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Services/BatteryRepository.cs`
- Modify: `web/Services/PassportRepository.cs`
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `web/Views/ClusterAdmin/Passports.cshtml`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Modify: `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `BatteryRouteAndRegistryTests.cs`:

```csharp
[Fact]
public void RegistrySearch_ShouldSupportPassportBatterySerialAndGlobalAdminClusterSearch()
{
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
    var repository = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));
    var view = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));

    Assert.Contains("ResolveSearchAsync", controller);
    Assert.Contains("SearchByClusterAsync", repository);
    Assert.Contains("Cluster search requires global admin access.", controller);
    Assert.Contains("Cluster search requires global admin access.", view);
}
```

Add to `AdminDenseConsoleLayoutTests.LocalAdminPassports_ShouldUseBatteryIdentityColumnsAndIconActions`:

```csharp
Assert.Contains("<th>Passport ID</th>", passports);
Assert.Contains("<th>Status</th>", passports);
Assert.Contains("PassportStatus", passports);
Assert.Contains("serial", controller, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Run focused tests to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "RegistrySearch_ShouldSupportPassportBatterySerial|LocalAdminPassports_ShouldUseBatteryIdentityColumns"
```

Expected: FAIL until registry and managed-passport search are updated.

- [ ] **Step 3: Add repository search helpers**

In `BatteryRepository`, add:

```csharp
public async Task<IReadOnlyList<BsonDocument>> SearchByClusterAsync(string query, CancellationToken cancellationToken = default)
{
    var collection = GetCollection();
    if (collection == null || string.IsNullOrWhiteSpace(query))
    {
        return [];
    }

    var regex = new BsonRegularExpression(query.Trim(), "i");
    return await collection.Find(Builders<BsonDocument>.Filter.Regex("clusterId", regex))
        .SortByDescending(row => row["updatedAt"])
        .Limit(500)
        .ToListAsync(cancellationToken);
}
```

In `PassportRepository.SearchDocumentsAsync`, ensure search includes:

```csharp
builder.Regex("passportId", regex),
builder.Regex("batteryId", regex),
builder.Regex("app.display.serialNumber", regex)
```

- [ ] **Step 4: Implement registry resolver**

In `RegistryController`, add private resolver:

```csharp
private async Task<RegistrySearchResult> ResolveSearchAsync(string query, bool isAdmin, CancellationToken cancellationToken)
{
    var passport = await _passportRepository.GetByPassportIdAsync(query, cancellationToken);
    if (passport != null)
    {
        return RegistrySearchResult.Passport(passport);
    }

    var battery = await _batteryRepository.GetByBatteryIdAsync(query, cancellationToken);
    if (battery != null)
    {
        return RegistrySearchResult.Battery(battery);
    }

    var bySerial = await _batteryRepository.SearchDocumentsAsync(query, includeArchived: isAdmin, cancellationToken);
    if (bySerial.Count == 1 && BsonHelpers.GetString(bySerial[0], "identity", "serialNumber").Equals(query, StringComparison.OrdinalIgnoreCase))
    {
        return RegistrySearchResult.Battery(bySerial[0]);
    }

    if (!isAdmin && await LooksLikeClusterQueryAsync(query, cancellationToken))
    {
        return RegistrySearchResult.ClusterDenied();
    }

    if (isAdmin)
    {
        var clusterRows = await SearchBatteriesForClusterQueryAsync(query, cancellationToken);
        if (clusterRows.Count > 0)
        {
            return RegistrySearchResult.Rows(clusterRows);
        }
    }

    return RegistrySearchResult.Rows(bySerial);
}
```

Use redirects:

- passport ID: `/{passportId}`.
- battery ID/serial for non-admin: `/{batteryId}`.
- battery ID/serial for admin/local admin: `/admin/batteries/{batteryId}/passports` for global admin and a read-only history route for local admin.
- cluster denied: set `ViewData["RegistryAccessMessage"] = "Cluster search requires global admin access."`.

- [ ] **Step 5: Managed Passport list/search/status**

In `ClusterAdminController.Passports`, keep search through `PassportRepository.SearchAsync`, but ensure source query matches Passport ID, Battery ID, and serial number. In `Views/ClusterAdmin/Passports.cshtml`, change first column heading to `Passport ID`, add a `Status` column, and render `@row.PassportStatus`.

- [ ] **Step 6: Run tests and commit**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "RegistrySearch_ShouldSupportPassportBatterySerial|LocalAdminPassports_ShouldUseBatteryIdentityColumns"
```

Expected: PASS.

Commit:

```powershell
git add web/Controllers/RegistryController.cs web/Services/BatteryRepository.cs web/Services/PassportRepository.cs web/Views/Registry/Index.cshtml web/Views/ClusterAdmin/Passports.cshtml web/Controllers/ClusterAdminController.cs BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
git commit -m "feat: strengthen registry and managed passport search"
```

---

### Task 8: Reports, QR Download, Charts, And Telemetry Graph Resilience

**Files:**
- Modify: `web/Services/PassportQrCodeService.cs`
- Modify: `web/Controllers/QrController.cs`
- Modify: `web/Services/PassportViewModelFactory.cs`
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Modify: `BatteryPassWeb.Tests/QrWorkflowTests.cs`
- Modify: `BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs`
- Modify: `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`

- [ ] **Step 1: Write failing tests**

Add to `QrWorkflowTests.cs`:

```csharp
[Fact]
public void SummaryReport_ShouldUseThreeColumnHeaderAndImageQrDownload()
{
    var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
    var qrController = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));
    var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

    Assert.Contains("bp-summary-identity-grid", summary);
    Assert.Contains("bp-summary-id-panel", summary);
    Assert.Contains("bp-summary-qr-panel", summary);
    Assert.Contains("image/png", qrController);
    Assert.Contains(".bp-summary-identity-grid", css);
    Assert.Contains("@media (max-width: 900px)", css);
}
```

Add to `AdminFeedbackFollowupTests.cs`:

```csharp
[Fact]
public void ReportCharts_ShouldUseTableLegendsAndOriginalPowerHeading()
{
    var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
    var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

    Assert.Contains("<h3 class=\"bp-subheading\">Original Power</h3>", summary);
    Assert.Contains("bp-chart-value-legend", summary);
    Assert.Contains("bp-chart-value-legend", detail);
    Assert.Contains("filter(row => Number.isFinite", detail);
}
```

- [ ] **Step 2: Run focused tests to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "SummaryReport_ShouldUseThreeColumnHeader|ReportCharts_ShouldUseTableLegends"
```

Expected: FAIL because the header, QR PNG download, table legends, and graph filtering are not in place.

- [ ] **Step 3: Add PNG QR download**

In `PassportQrCodeService`, add:

```csharp
public byte[] GeneratePng(string payload)
{
    using var generator = new QRCodeGenerator();
    using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
    using var qr = new PngByteQRCode(data);
    return qr.GetGraphic(12);
}
```

In `QrController.Download`, return:

```csharp
var png = _passportQrCodeService.GeneratePng(payload);
return File(png, "image/png", Path.ChangeExtension(fileName, ".png"));
```

- [ ] **Step 4: Rework summary header and charts**

In `Summary.cshtml`, replace the hero top identity block with:

```html
<div class="bp-summary-identity-grid">
    <section class="bp-summary-id-panel">
        <span class="bp-summary-title">Battery</span>
        <h1>@(string.IsNullOrWhiteSpace(passport.BatterySerialNumber) ? passport.PassportId : passport.BatterySerialNumber)</h1>
        <p>Cluster: @passport.ClusterLabel</p>
        <span class="bp-status-pill is-published">@(passport.IsHistoricalPassport ? "Historical passport" : "Latest passport")</span>
    </section>
    <section class="bp-summary-id-panel">
        <span class="bp-summary-title">Identifiers</span>
        <p class="bp-passport-id">Passport ID: <span class="bp-id-copy-row"><span class="bp-id-value">@passport.PassportId</span>@await Html.PartialAsync("_CopyIdButton", passport.PassportId)</span></p>
        <p class="bp-passport-id">Battery ID: <span class="bp-id-copy-row"><span class="bp-id-value">@passport.BatteryId</span>@await Html.PartialAsync("_CopyIdButton", passport.BatteryId)</span></p>
    </section>
    <a class="bp-summary-qr-panel" href="/qr/@Uri.EscapeDataString(qrBatteryId)/download" aria-label="Save battery QR image for @qrBatteryId">
        <img src="/qr/@Uri.EscapeDataString(qrBatteryId)/svg" alt="QR code for @qrBatteryId" />
    </a>
</div>
```

Change `Original power` to:

```html
<h3 class="bp-subheading">Original Power</h3>
```

Render table legends:

```html
<table class="bp-chart-value-legend">
@foreach (var segment in passport.CarbonFootprintSegments)
{
    <tr>
        <td><span class="bp-dot" style="background:@segment.Color"></span>@segment.Label</td>
        <td>@segment.Value.ToString("0.#") @segment.Unit</td>
    </tr>
}
</table>
```

Use the same table legend for recycled content in Summary and Detail.

- [ ] **Step 5: Harden telemetry chart script**

In `Detail.cshtml`, filter and sort before labels:

```javascript
const rows = (@Html.Raw(telemetryJson) || [])
    .filter(row => row && row.MeasuredAt && Date.parse(row.MeasuredAt))
    .sort((left, right) => Date.parse(left.MeasuredAt) - Date.parse(right.MeasuredAt));
const values = (field) => rows.map(row => Number.isFinite(Number(row[field])) ? Number(row[field]) : null);
```

- [ ] **Step 6: Add responsive CSS**

Add to `site.css`:

```css
.bp-summary-identity-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr) minmax(132px, 160px);
  gap: 14px;
  align-items: stretch;
}

.bp-summary-id-panel,
.bp-summary-qr-panel {
  min-width: 0;
  border: 1px solid var(--bp-border);
  border-radius: 8px;
  background: #fff;
  padding: 14px;
}

.bp-summary-qr-panel img {
  display: block;
  inline-size: min(132px, 100%);
  block-size: auto;
  margin-inline: auto;
}

.bp-chart-value-legend {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.86rem;
}

.bp-chart-value-legend td {
  border-top: 1px solid #e4e8ef;
  padding: 6px 4px;
}

.bp-chart-value-legend td:last-child {
  text-align: right;
  font-weight: 700;
}

@media (max-width: 900px) {
  .bp-summary-identity-grid {
    grid-template-columns: 1fr;
  }
}
```

- [ ] **Step 7: Run tests and commit**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "SummaryReport_ShouldUseThreeColumnHeader|ReportCharts_ShouldUseTableLegends|Qr"
```

Expected: PASS.

Commit:

```powershell
git add web/Services/PassportQrCodeService.cs web/Controllers/QrController.cs web/Services/PassportViewModelFactory.cs web/Views/Passport/Summary.cshtml web/Views/Passport/Detail.cshtml web/wwwroot/css/site.css BatteryPassWeb.Tests/QrWorkflowTests.cs BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
git commit -m "feat: polish report qr and chart presentation"
```

---

### Task 9: Admin UX Polish, Users, Clusters, Selects, And Forgot Password

**Files:**
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Controllers/LoginController.cs`
- Modify: `web/Services/AuthService.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Users.cshtml`
- Modify: `web/Views/Login/Index.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Modify: `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Create: `BatteryPassWeb.Tests/ForgotPasswordFlowTests.cs`

- [ ] **Step 1: Write failing tests**

Create `BatteryPassWeb.Tests/ForgotPasswordFlowTests.cs`:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class ForgotPasswordFlowTests
{
    [Fact]
    public void Login_ShouldExposeForgotPasswordRequestFlow()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
        var auth = File.ReadAllText(RepoFile("web", "Services", "AuthService.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Login", "Index.cshtml"));

        Assert.Contains("[HttpPost(\"forgot-password\")]", controller);
        Assert.Contains("StorePasswordResetRequestAsync", auth);
        Assert.Contains("Forgot password?", view);
        Assert.Contains("If an account exists, reset instructions will be sent when email delivery is configured.", view);
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

Add to `AdminDenseConsoleLayoutTests`:

```csharp
[Fact]
public void AdminUxPolish_ShouldUseExplicitRolesDuplicateClusterValidationAndStandardSelects()
{
    var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
    var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
    var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
    var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

    Assert.Contains("Create Cluster", clusters);
    Assert.Contains("Cluster ID already exists.", admin);
    Assert.Contains("Global admin: Yes", clusters);
    Assert.Contains("Global admin: No", clusters);
    Assert.DoesNotContain("No global admin", clusters);
    Assert.Contains("bp-select-shell", clusters);
    Assert.Contains("bp-select-shell", clusterUsers);
    Assert.Contains(".bp-select-shell::after", css);
    Assert.Contains("bp-user-management-table", clusterUsers);
    Assert.DoesNotContain("bp-console-split", clusterUsers);
}
```

- [ ] **Step 2: Run focused tests to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "ForgotPasswordFlow|AdminUxPolish"
```

Expected: FAIL because forgot-password routes/storage and UI polish are incomplete.

- [ ] **Step 3: Block duplicate cluster creation and relabel button**

In `AdminController.CreateCluster`, before upsert:

```csharp
if (await _clusterRepository.GetClusterByIdAsync(clusterId, cancellationToken) != null)
{
    return Redirect($"/admin/clusters?tab=clusters&error={Uri.EscapeDataString("Cluster ID already exists.")}");
}
```

Add `GetClusterByIdAsync` to `ClusterRepository`:

```csharp
public async Task<BsonDocument?> GetClusterByIdAsync(string clusterId, CancellationToken cancellationToken = default)
{
    if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(clusterId))
    {
        return null;
    }

    return await _mongoContext.Database.GetCollection<BsonDocument>("clusters")
        .Find(Builders<BsonDocument>.Filter.Eq("clusterId", clusterId.Trim()))
        .FirstOrDefaultAsync(cancellationToken);
}
```

In `Clusters.cshtml`, change button text:

```html
<button type="submit" class="bp-primary-button">Create Cluster</button>
```

- [ ] **Step 4: Standardize selects**

Wrap admin and cluster-admin selects:

```html
<span class="bp-select-shell">
    <select class="form-select" name="clusterId">...</select>
</span>
```

Add CSS:

```css
.bp-select-shell {
  position: relative;
  display: block;
}

.bp-select-shell select {
  appearance: none;
  padding-right: 2.2rem !important;
}

.bp-select-shell::after {
  position: absolute;
  right: 0.85rem;
  top: 50%;
  width: 0.48rem;
  height: 0.48rem;
  border-right: 1.8px solid #405367;
  border-bottom: 1.8px solid #405367;
  content: "";
  pointer-events: none;
  transform: translateY(-65%) rotate(45deg);
}
```

- [ ] **Step 5: Mirror global Users layout for cluster admins**

Replace `web/Views/ClusterAdmin/Users.cshtml` with the same structural classes and drawer pattern used by `/admin/clusters?tab=users`, keeping scoped `Model.Users`, `Model.Memberships`, and `Model.Clusters`.

Use labels:

```html
<span>Global admin: @(user.Roles.Contains("Global Admin") ? "Yes" : "No")</span>
```

Rename the create panel heading and button:

```html
<h2 class="h5 mb-2">Create User</h2>
<button type="submit" class="bp-primary-button">Create User</button>
```

- [ ] **Step 6: Add forgot-password request flow**

In `AuthService`, add:

```csharp
public async Task StorePasswordResetRequestAsync(string email, string remoteIp, CancellationToken cancellationToken = default)
{
    if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(email))
    {
        return;
    }

    await _mongoContext.Database.GetCollection<BsonDocument>("passwordResetRequests").InsertOneAsync(new BsonDocument
    {
        ["email"] = email.Trim().ToLowerInvariant(),
        ["remoteIp"] = remoteIp,
        ["requestedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        ["status"] = "email-not-configured"
    }, cancellationToken: cancellationToken);
}
```

In `LoginController`:

```csharp
[HttpPost("forgot-password")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ForgotPassword([FromForm] string email, CancellationToken cancellationToken)
{
    await _authService.StorePasswordResetRequestAsync(email, HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty, cancellationToken);
    TempData["ForgotPasswordMessage"] = "If an account exists, reset instructions will be sent when email delivery is configured.";
    return Redirect("/login");
}
```

In `Login/Index.cshtml`, render the message and form:

```html
@if (TempData["ForgotPasswordMessage"] is string forgotMessage && !string.IsNullOrWhiteSpace(forgotMessage))
{
    <div class="alert alert-info">@forgotMessage</div>
}
<form method="post" action="/login/forgot-password" class="mt-3">
    @Html.AntiForgeryToken()
    <label class="form-label" for="ForgotEmail">Forgot password?</label>
    <div class="input-group">
        <input class="form-control" type="email" id="ForgotEmail" name="email" aria-label="Email" required />
        <button class="btn btn-outline-secondary" type="submit">Request reset</button>
    </div>
</form>
```

- [ ] **Step 7: Run tests and commit**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "ForgotPasswordFlow|AdminUxPolish|UserPasswordChanges"
```

Expected: PASS.

Commit:

```powershell
git add web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs web/Controllers/LoginController.cs web/Services/AuthService.cs web/Services/ClusterRepository.cs web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/Users.cshtml web/Views/Login/Index.cshtml web/wwwroot/css/site.css BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/ForgotPasswordFlowTests.cs
git commit -m "feat: polish admin users clusters and login"
```

---

### Task 10: Public Sample Battery And Final Responsive Verification

**Files:**
- Modify: `web/Controllers/HomeController.cs`
- Modify: `web/Services/BatteryRouteResolutionService.cs`
- Modify: `web/Views/Home/Index.cshtml`
- Modify: `BatteryPassWeb.Tests/ExternalApiInitializerTests.cs`
- Modify: `BatteryPassWeb.Tests/QrWorkflowTests.cs`

- [ ] **Step 1: Write failing sample access test**

Update `ExternalApiInitializerTests.LandingSearchSample_ShouldUseGeneratedDemoBatteryIdInsteadOfLegacyDid`:

```csharp
Assert.Contains("sample battery ID", landing);
Assert.Contains("public sample passport", landing, StringComparison.OrdinalIgnoreCase);
Assert.Contains("return Redirect($\"/{Uri.EscapeDataString(query)}\")", homeController);
```

- [ ] **Step 2: Run focused test to verify failure**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter LandingSearchSample_ShouldUseGeneratedDemoBatteryIdInsteadOfLegacyDid
```

Expected: FAIL until sample copy/routing reflects public summary access.

- [ ] **Step 3: Ensure public sample opens latest public summary**

In `HomeController.Search`, keep battery route redirect to `/{batteryId}`.

In `PassportController.Detail`, when `resolution.Kind == BatteryRouteTargetKind.Battery`, keep returning `await Battery(decodedPassportId, cancellationToken)`.

In `PassportController.Battery`, change anonymous handling so a battery with exactly one visible public latest passport redirects directly to that passport summary:

```csharp
if (User.Identity?.IsAuthenticated != true)
{
    var latestPublic = visibleRows.FirstOrDefault(row => row.IsLatestForBattery && row.IsPubliclyVisible)
        ?? visibleRows.FirstOrDefault(row => row.IsPubliclyVisible);
    if (latestPublic != null)
    {
        return Redirect($"/{Uri.EscapeDataString(latestPublic.PassportId)}/summary");
    }
}
```

In `Home/Index.cshtml`, change sample copy to:

```html
sample battery ID for the public sample passport
```

- [ ] **Step 4: Run full automated tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Start local app for visual checks**

Run:

```powershell
dotnet run --project web/BatteryPassWeb.csproj --urls http://localhost:5099
```

Expected: app starts and listens on `http://localhost:5099`.

- [ ] **Step 6: Use Browser for responsive checks**

Open these targets in the in-app browser and check desktop plus narrow widths:

- `http://localhost:5099/`
- `http://localhost:5099/login`
- `http://localhost:5099/registry`
- `http://localhost:5099/admin/clusters?tab=batteries`
- `http://localhost:5099/admin/clusters?tab=clusters`
- `http://localhost:5099/admin/clusters?tab=users`
- `http://localhost:5099/admin/clusters?tab=local-editable-fields`
- `http://localhost:5099/cluster-admin/users`
- a sample Summary report
- a sample Detailed report Performance tab
- `http://localhost:5099/help`

Verify:

- No text overlaps.
- IDs truncate or wrap only inside intended containers.
- Tables horizontally scroll when needed.
- QR remains visible and not distorted.
- Select chevrons render on all admin selects.
- Chart legends fit on mobile.
- Telemetry charts render with malformed or sparse points.

- [ ] **Step 7: Commit final polish**

Commit any fixes from visual verification:

```powershell
git add web BatteryPassWeb.Tests
git commit -m "fix: complete responsive passport admin verification"
```

---

## Final Verification

- [ ] Run all tests:

```powershell
dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj
```

Expected: PASS.

- [ ] Run app:

```powershell
dotnet run --project web/BatteryPassWeb.csproj --urls http://localhost:5099
```

Expected: app starts successfully.

- [ ] Verify phase completion:

Phase 1 complete when Tasks 1 to 3 pass and are committed.

Phase 2 complete when Tasks 4 to 6 pass and are committed.

Phase 3 complete when Tasks 7 to 10 pass, visual checks are complete, and final verification passes.
