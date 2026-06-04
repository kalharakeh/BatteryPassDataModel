# External Battery Create API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `POST /api/external/v1/batteries` so external systems can create battery records with the same checks and interlocks as the admin UI, then audit who created each battery.

**Architecture:** Extract shared battery creation into a service used by both the admin controller and external API controller. Keep controllers as transport adapters: they parse form or JSON input, pass actor and scope metadata, then map the shared service result to UI redirects or API envelopes. Add a battery audit service backed by `batteryAuditEvents`, separate from passport audit events because a battery can exist before its first passport.

**Tech Stack:** ASP.NET Core MVC controllers, Razor help page, MongoDB BSON repositories, xUnit source and unit tests, PowerShell test commands.

---

## File Structure

- Create: `web/Services/BatteryAuditService.cs`
  - Builds and persists `batteryAuditEvents`.
  - Lists battery audit events by Battery ID for future UI use.
- Create: `web/Services/BatteryCreationService.cs`
  - Owns all battery creation validation, template resolution, Battery ID generation, insertion, attribution, duplicate handling, and `battery.created` audit event writing.
  - Accepts an optional admin-only pre-insert customization hook so the existing editable-field policy form behavior remains intact without letting API requests submit arbitrary document overrides.
- Modify: `web/Program.cs`
  - Register `BatteryAuditService` and `BatteryCreationService`.
- Modify: `web/Controllers/AdminController.cs`
  - Replace direct battery construction and insert in `CreateBattery` with `BatteryCreationService`.
  - Preserve `ApplyBatteryEditableFormValues` and `ApplyPassportForm` through the pre-insert hook.
- Modify: `web/Controllers/ExternalApiController.cs`
  - Inject `BatteryCreationService`.
  - Add `POST /api/external/v1/batteries`.
  - Parse the explicit JSON contract and pass external token scope into the shared service.
- Modify: `web/Views/Help/Index.cshtml`
  - Document the create-battery endpoint and add a workbench preset.
- Modify: `docs/end-user-testing-guide.md`
  - Add API create-battery verification steps.
- Modify: `docs/qa-test-pack.md`
  - Add a concise QA checklist row for API battery creation.
- Create: `BatteryPassWeb.Tests/BatteryAuditServiceTests.cs`
  - Unit coverage for event shape.
- Create: `BatteryPassWeb.Tests/BatteryCreationServiceTests.cs`
  - Source-level coverage that the shared service owns required interlocks.
- Create: `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`
  - Source and docs coverage for controller wiring, admin reuse, and help docs.

---

### Task 1: Battery Audit Service

**Files:**
- Create: `BatteryPassWeb.Tests/BatteryAuditServiceTests.cs`
- Create: `web/Services/BatteryAuditService.cs`
- Modify: `web/Program.cs:99-100`

- [ ] **Step 1: Write the failing audit service test**

Create `BatteryPassWeb.Tests/BatteryAuditServiceTests.cs`:

```csharp
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class BatteryAuditServiceTests
{
    [Fact]
    public void BuildBatteryAuditEventDocument_ShouldCaptureBatteryCreationActorAndMetadata()
    {
        var service = new BatteryAuditService();
        var metadata = new BsonDocument
        {
            ["batteryFamily"] = "Compact 7M",
            ["batteryModel"] = "2.0",
            ["softwareVersion"] = "4.0",
            ["serialNumber"] = "SN-API-001",
            ["clusterId"] = "cluster-north-operations"
        };

        var document = service.BuildBatteryAuditEventDocument(
            "battery-001",
            "battery.created",
            "Read Write Token",
            "external-api",
            "external-api",
            "Battery created.",
            metadata,
            "token-001",
            "2026-06-04T10:00:00.0000000Z");

        Assert.False(string.IsNullOrWhiteSpace(BsonHelpers.GetString(document, "eventId")));
        Assert.Equal("battery-001", BsonHelpers.GetString(document, "batteryId"));
        Assert.Equal("battery.created", BsonHelpers.GetString(document, "eventType"));
        Assert.Equal("Read Write Token", BsonHelpers.GetString(document, "actor"));
        Assert.Equal("external-api", BsonHelpers.GetString(document, "actorType"));
        Assert.Equal("token-001", BsonHelpers.GetString(document, "actorTokenId"));
        Assert.Equal("external-api", BsonHelpers.GetString(document, "source"));
        Assert.Equal("Battery created.", BsonHelpers.GetString(document, "message"));
        Assert.Equal("Compact 7M", BsonHelpers.GetString(document, "metadata", "batteryFamily"));
        Assert.Equal("2026-06-04T10:00:00.0000000Z", BsonHelpers.GetString(document, "createdAt"));
    }
}
```

- [ ] **Step 2: Run the audit test and verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryAuditServiceTests" /p:UseAppHost=false
```

Expected: FAIL with a compile error that `BatteryAuditService` does not exist.

- [ ] **Step 3: Implement `BatteryAuditService`**

Create `web/Services/BatteryAuditService.cs`:

```csharp
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class BatteryAuditService
{
    private readonly MongoContext? _mongoContext;

    public BatteryAuditService(MongoContext? mongoContext = null)
    {
        _mongoContext = mongoContext;
    }

    public BsonDocument BuildBatteryAuditEventDocument(
        string batteryId,
        string eventType,
        string actor,
        string actorType,
        string source,
        string message,
        BsonDocument? metadata = null,
        string actorTokenId = "",
        string createdAt = "")
    {
        var id = ObjectId.GenerateNewId();
        var timestamp = string.IsNullOrWhiteSpace(createdAt) ? DateTimeOffset.UtcNow.ToString("O") : createdAt;

        return new BsonDocument
        {
            ["_id"] = id,
            ["eventId"] = id.ToString(),
            ["batteryId"] = batteryId,
            ["eventType"] = eventType,
            ["actor"] = actor,
            ["actorType"] = actorType,
            ["actorTokenId"] = string.IsNullOrWhiteSpace(actorTokenId) ? BsonNull.Value : actorTokenId,
            ["source"] = source,
            ["message"] = message,
            ["metadata"] = metadata == null ? new BsonDocument() : metadata.DeepClone(),
            ["createdAt"] = timestamp
        };
    }

    public async Task<BsonDocument> AppendBatteryAuditEventAsync(
        string batteryId,
        string eventType,
        string actor,
        string actorType,
        string source,
        string message,
        BsonDocument? metadata = null,
        string actorTokenId = "",
        CancellationToken cancellationToken = default)
    {
        var auditEvent = BuildBatteryAuditEventDocument(
            batteryId,
            eventType,
            actor,
            actorType,
            source,
            message,
            metadata,
            actorTokenId);

        var collection = GetBatteryAuditEventsCollection();
        if (collection != null)
        {
            await collection.InsertOneAsync(auditEvent, cancellationToken: cancellationToken);
        }

        return auditEvent;
    }

    public async Task<IReadOnlyList<BsonDocument>> ListBatteryAuditEventsAsync(
        string batteryId,
        CancellationToken cancellationToken = default)
    {
        var collection = GetBatteryAuditEventsCollection();
        if (collection == null || string.IsNullOrWhiteSpace(batteryId))
        {
            return [];
        }

        return await collection
            .Find(Builders<BsonDocument>.Filter.Eq("batteryId", batteryId.Trim()))
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
            .ToListAsync(cancellationToken);
    }

    private IMongoCollection<BsonDocument>? GetBatteryAuditEventsCollection()
    {
        return _mongoContext?.Database?.GetCollection<BsonDocument>("batteryAuditEvents");
    }
}
```

- [ ] **Step 4: Register the service**

In `web/Program.cs`, add `BatteryAuditService` next to `AuditRevisionService`:

```csharp
builder.Services.AddSingleton<AuditRevisionService>();
builder.Services.AddSingleton<BatteryAuditService>();
builder.Services.AddSingleton<PassportTrustWorkflowService>();
```

- [ ] **Step 5: Run the audit test and verify it passes**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryAuditServiceTests" /p:UseAppHost=false
```

Expected: PASS.

- [ ] **Step 6: Commit**

Run:

```powershell
git add BatteryPassWeb.Tests\BatteryAuditServiceTests.cs web\Services\BatteryAuditService.cs web\Program.cs
git commit -m "feat: add battery audit service"
```

---

### Task 2: Shared Battery Creation Service

**Files:**
- Create: `BatteryPassWeb.Tests/BatteryCreationServiceTests.cs`
- Create: `web/Services/BatteryCreationService.cs`
- Modify: `web/Program.cs:99-115`

- [ ] **Step 1: Write the failing shared-service source tests**

Create `BatteryPassWeb.Tests/BatteryCreationServiceTests.cs`:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class BatteryCreationServiceTests
{
    [Fact]
    public void BatteryCreationService_ShouldOwnCoreCreationInterlocks()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));

        Assert.Contains("GetBySerialNumberAsync", source);
        Assert.Contains("CreateBatteryId(product.ProductName, serialNumber)", source);
        Assert.Contains("GetClusterByIdAsync", source);
        Assert.Contains("SoftwareVersions.FirstOrDefault", source);
        Assert.Contains("GetByBatteryIdAsync(batteryId", source);
        Assert.Contains("MongoWriteException", source);
        Assert.Contains("ServerErrorCategory.DuplicateKey", source);
        Assert.Contains("AppendBatteryAuditEventAsync", source);
        Assert.Contains("CustomizeBatteryBeforeInsert", source);
    }

    [Fact]
    public void BatteryCreationService_ShouldRejectInvalidInputsBeforeInsert()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));
        var insertIndex = source.IndexOf("CreateBatteryAsync(battery", StringComparison.Ordinal);

        Assert.True(insertIndex > 0);
        Assert.True(source.IndexOf("Battery serial number is required.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Battery serial number already exists.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Unknown Battery Family.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Unknown Battery Model.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Unknown Software Version.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Battery cluster is required.", StringComparison.Ordinal) < insertIndex);
        Assert.True(source.IndexOf("Token cannot access this cluster scope.", StringComparison.Ordinal) < insertIndex);
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

- [ ] **Step 2: Run the shared-service tests and verify they fail**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryCreationServiceTests" /p:UseAppHost=false
```

Expected: FAIL with `FileNotFoundException` for `web\Services\BatteryCreationService.cs`.

- [ ] **Step 3: Implement the shared service records and validation boundary**

Create `web/Services/BatteryCreationService.cs` with this structure:

```csharp
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class BatteryCreationCommand
{
    public string ProductId { get; init; } = string.Empty;
    public string BatteryFamily { get; init; } = string.Empty;
    public string ProductVersion { get; init; } = string.Empty;
    public string BatteryModel { get; init; } = string.Empty;
    public string SoftwareVersion { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string ModelNumber { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FacilityId { get; init; } = string.Empty;
    public string ManufacturingDate { get; init; } = string.Empty;
    public Action<BsonDocument, string>? CustomizeBatteryBeforeInsert { get; init; }
}

public sealed record BatteryCreationActor(
    string Actor,
    string ActorType,
    string Source,
    string TokenId = "");

public sealed record BatteryCreationClusterScope(
    bool Restricted,
    bool GlobalAccess,
    IReadOnlyList<string> ClusterIds)
{
    public static BatteryCreationClusterScope Unrestricted { get; } = new(false, true, []);

    public static BatteryCreationClusterScope FromToken(ExternalApiTokenContext tokenContext)
    {
        return new BatteryCreationClusterScope(
            Restricted: true,
            GlobalAccess: tokenContext.GlobalAccess,
            ClusterIds: tokenContext.ClusterIds);
    }

    public bool CanAccess(string clusterId)
    {
        if (!Restricted || GlobalAccess)
        {
            return true;
        }

        return ClusterIds.Contains(clusterId, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class BatteryCreationResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string BatteryId { get; init; } = string.Empty;
    public BsonDocument? Battery { get; init; }

    public static BatteryCreationResult Failure(int statusCode, string message) =>
        new() { Success = false, StatusCode = statusCode, Message = message };

    public static BatteryCreationResult Created(string batteryId, BsonDocument battery) =>
        new()
        {
            Success = true,
            StatusCode = StatusCodes.Status201Created,
            Message = "Battery created successfully.",
            BatteryId = batteryId,
            Battery = battery
        };
}

public sealed class BatteryCreationService
{
    private readonly BatteryRepository _batteryRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly ProductTemplateService _productTemplateService;
    private readonly BatteryIdService _batteryIdService;
    private readonly BatteryAuditService _batteryAuditService;

    public BatteryCreationService(
        BatteryRepository batteryRepository,
        ClusterRepository clusterRepository,
        ProductTemplateService productTemplateService,
        BatteryIdService batteryIdService,
        BatteryAuditService batteryAuditService)
    {
        _batteryRepository = batteryRepository;
        _clusterRepository = clusterRepository;
        _productTemplateService = productTemplateService;
        _batteryIdService = batteryIdService;
        _batteryAuditService = batteryAuditService;
    }

    public async Task<BatteryCreationResult> CreateBatteryAsync(
        BatteryCreationCommand command,
        BatteryCreationActor actor,
        BatteryCreationClusterScope scope,
        CancellationToken cancellationToken = default)
    {
        if (!_batteryRepository.IsAvailable)
        {
            return BatteryCreationResult.Failure(StatusCodes.Status503ServiceUnavailable, "Database is not connected.");
        }

        var serialNumber = command.SerialNumber.Trim();
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return BatteryCreationResult.Failure(StatusCodes.Status400BadRequest, "Battery serial number is required.");
        }

        if (await _batteryRepository.GetBySerialNumberAsync(serialNumber, cancellationToken) != null)
        {
            return BatteryCreationResult.Failure(StatusCodes.Status409Conflict, "Battery serial number already exists.");
        }

        var product = await ResolveProductAsync(command, cancellationToken);
        if (product == null)
        {
            return BatteryCreationResult.Failure(StatusCodes.Status400BadRequest, "Unknown Battery Family.");
        }

        var requestedModel = FirstNonEmpty(command.BatteryModel, command.ProductVersion);
        if (string.IsNullOrWhiteSpace(requestedModel))
        {
            return BatteryCreationResult.Failure(StatusCodes.Status400BadRequest, "Battery Model is required.");
        }

        var selectedVersion = product.ProductVersions.FirstOrDefault(version =>
            version.Version.Equals(requestedModel.Trim(), StringComparison.OrdinalIgnoreCase));
        if (selectedVersion == null)
        {
            return BatteryCreationResult.Failure(StatusCodes.Status400BadRequest, "Unknown Battery Model.");
        }

        var requestedSoftwareVersion = command.SoftwareVersion.Trim();
        if (string.IsNullOrWhiteSpace(requestedSoftwareVersion))
        {
            return BatteryCreationResult.Failure(StatusCodes.Status400BadRequest, "softwareVersion is required.");
        }

        var selectedSoftwareVersion = selectedVersion.SoftwareVersions.FirstOrDefault(version =>
            version.SoftwareVersion.Equals(requestedSoftwareVersion, StringComparison.OrdinalIgnoreCase));
        if (selectedSoftwareVersion == null)
        {
            return BatteryCreationResult.Failure(StatusCodes.Status400BadRequest, "Unknown Software Version.");
        }

        var clusterId = ClusterRepository.NormalizeClusterId(command.ClusterId);
        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return BatteryCreationResult.Failure(StatusCodes.Status400BadRequest, "Battery cluster is required.");
        }

        if (!scope.CanAccess(clusterId))
        {
            return BatteryCreationResult.Failure(StatusCodes.Status403Forbidden, "Token cannot access this cluster scope.");
        }

        if (await _clusterRepository.GetClusterByIdAsync(clusterId, cancellationToken) == null)
        {
            return BatteryCreationResult.Failure(StatusCodes.Status400BadRequest, "Cluster was not found.");
        }

        var batteryId = _batteryIdService.CreateBatteryId(product.ProductName, serialNumber);
        if (await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken) != null)
        {
            return BatteryCreationResult.Failure(StatusCodes.Status409Conflict, "Battery already exists for this family and serial number.");
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        var battery = ProductTemplatePassportBuilder.BuildBatteryFromTemplate(
            batteryId,
            product,
            selectedVersion,
            new ProductTemplateBatteryIdentity
            {
                ModelNumber = FirstNonEmpty(command.ModelNumber, $"{product.ProductId}-{serialNumber}"),
                SerialNumber = serialNumber,
                DisplayName = FirstNonEmpty(command.DisplayName, $"{product.ProductName} {serialNumber}"),
                FacilityId = command.FacilityId.Trim(),
                ClusterId = clusterId,
                ManufacturingDate = FirstNonEmpty(command.ManufacturingDate, now[..10])
            },
            now);

        command.CustomizeBatteryBeforeInsert?.Invoke(battery, now);
        ApplyLockedCreationFields(battery, batteryId, product, selectedVersion, selectedSoftwareVersion, serialNumber, clusterId, actor, now);

        try
        {
            await _batteryRepository.CreateBatteryAsync(battery, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return BatteryCreationResult.Failure(StatusCodes.Status409Conflict, "Battery serial number already exists.");
        }

        await _batteryAuditService.AppendBatteryAuditEventAsync(
            batteryId,
            "battery.created",
            actor.Actor,
            actor.ActorType,
            actor.Source,
            "Battery created.",
            BuildAuditMetadata(battery),
            actor.TokenId,
            cancellationToken);

        return BatteryCreationResult.Created(batteryId, battery);
    }

    private async Task<BatteryProductTemplate?> ResolveProductAsync(
        BatteryCreationCommand command,
        CancellationToken cancellationToken)
    {
        var products = await _productTemplateService.ListProductsAsync(cancellationToken);
        var productId = command.ProductId.Trim();
        if (!string.IsNullOrWhiteSpace(productId))
        {
            return products.FirstOrDefault(product => product.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
        }

        var batteryFamily = command.BatteryFamily.Trim();
        if (string.IsNullOrWhiteSpace(batteryFamily))
        {
            return null;
        }

        return products.FirstOrDefault(product =>
            product.ProductName.Equals(batteryFamily, StringComparison.OrdinalIgnoreCase)
            || product.ProductId.Equals(batteryFamily, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyLockedCreationFields(
        BsonDocument battery,
        string batteryId,
        BatteryProductTemplate product,
        BatteryProductVersion selectedVersion,
        BatteryProductSoftwareVersion selectedSoftwareVersion,
        string serialNumber,
        string clusterId,
        BatteryCreationActor actor,
        string now)
    {
        var app = EnsureDocument(battery, "app");
        var display = EnsureDocument(app, "display");
        var productNode = EnsureDocument(app, "product");
        var identity = EnsureDocument(battery, "identity");

        display["serialNumber"] = BatteryPassCanonicalDataCatalog.NormalizeManufacturerSerialNumber(serialNumber, batteryId);
        productNode["productId"] = product.ProductId;
        productNode["productVersion"] = selectedVersion.Version;
        productNode["batteryModel"] = selectedVersion.Version;
        productNode["softwareVersion"] = selectedSoftwareVersion.SoftwareVersion;
        productNode["softwareReleaseDate"] = selectedSoftwareVersion.SoftwareReleaseDate;
        productNode["softwareLatestUpdate"] = selectedSoftwareVersion.SoftwareLatestUpdate;

        battery["batteryId"] = batteryId;
        battery["clusterId"] = clusterId;
        battery["createdAt"] = now;
        battery["createdBy"] = actor.Actor;
        battery["createdByType"] = actor.ActorType;
        battery["updatedAt"] = now;
        battery["updatedBy"] = actor.Actor;
        battery["updatedByType"] = actor.ActorType;

        if (!string.IsNullOrWhiteSpace(actor.TokenId))
        {
            battery["createdByTokenId"] = actor.TokenId;
            battery["updatedByTokenId"] = actor.TokenId;
        }

        identity["batteryFamily"] = product.ProductName;
        identity["batteryModel"] = selectedVersion.Version;
        identity["modelNumber"] = BsonHelpers.GetString(display, "modelNumber");
        identity["serialNumber"] = BsonHelpers.GetString(display, "serialNumber");
        identity["displayName"] = BsonHelpers.GetString(display, "name");
        identity["facilityId"] = BsonHelpers.GetString(display, "facilityId");
        identity["productId"] = product.ProductId;
        identity["productVersion"] = selectedVersion.Version;
        identity["softwareVersion"] = selectedSoftwareVersion.SoftwareVersion;
    }

    private static BsonDocument BuildAuditMetadata(BsonDocument battery)
    {
        return new BsonDocument
        {
            ["batteryFamily"] = BsonHelpers.GetString(battery, "identity", "batteryFamily"),
            ["batteryModel"] = BsonHelpers.GetString(battery, "identity", "batteryModel"),
            ["softwareVersion"] = BsonHelpers.GetString(battery, "identity", "softwareVersion"),
            ["serialNumber"] = BsonHelpers.GetString(battery, "identity", "serialNumber"),
            ["clusterId"] = BsonHelpers.GetString(battery, "clusterId")
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

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
```

- [ ] **Step 4: Register the shared service**

In `web/Program.cs`, add:

```csharp
builder.Services.AddSingleton<BatteryCreationService>();
```

Place it after `BatteryAuditService` so the constructor dependencies are visually grouped:

```csharp
builder.Services.AddSingleton<AuditRevisionService>();
builder.Services.AddSingleton<BatteryAuditService>();
builder.Services.AddSingleton<BatteryCreationService>();
builder.Services.AddSingleton<PassportTrustWorkflowService>();
```

- [ ] **Step 5: Run the shared-service tests and compile check**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryCreationServiceTests" /p:UseAppHost=false
```

Expected: PASS.

- [ ] **Step 6: Commit**

Run:

```powershell
git add BatteryPassWeb.Tests\BatteryCreationServiceTests.cs web\Services\BatteryCreationService.cs web\Program.cs
git commit -m "feat: add shared battery creation service"
```

---

### Task 3: Admin Battery Creation Uses Shared Service

**Files:**
- Create: `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`
- Modify: `web/Controllers/AdminController.cs:103-166`
- Modify: `web/Controllers/AdminController.cs:287-345`

- [ ] **Step 1: Write the failing admin reuse test**

Create `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs` with the admin test first:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class ExternalApiBatteryCreateTests
{
    [Fact]
    public void AdminBatteryCreate_ShouldUseSharedCreationServiceAndKeepEditableFormHook()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("BatteryCreationService", source);
        Assert.Contains("_batteryCreationService.CreateBatteryAsync", source);
        Assert.Contains("CustomizeBatteryBeforeInsert", source);
        Assert.Contains("ApplyBatteryEditableFormValues(form, editablePolicy, forCreation: true)", source);
        Assert.Contains("ApplyPassportForm(battery, filteredForm, createNow)", source);
        Assert.Contains("new BatteryCreationActor(CurrentActor(), \"admin-ui\", \"admin-ui\")", source);
        Assert.DoesNotContain("await _batteryRepository.CreateBatteryAsync(battery, cancellationToken);", source);
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

- [ ] **Step 2: Run the admin reuse test and verify it fails**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiBatteryCreateTests.AdminBatteryCreate_ShouldUseSharedCreationServiceAndKeepEditableFormHook" /p:UseAppHost=false
```

Expected: FAIL because `AdminController` still creates batteries directly.

- [ ] **Step 3: Inject `BatteryCreationService` into `AdminController`**

In `web/Controllers/AdminController.cs`, add a field near the existing battery services:

```csharp
private readonly BatteryCreationService _batteryCreationService;
```

Add the constructor parameter after `BatteryTemplateUpdateService batteryTemplateUpdateService`:

```csharp
BatteryCreationService batteryCreationService,
```

Assign it near the other battery services:

```csharp
_batteryCreationService = batteryCreationService;
```

- [ ] **Step 4: Replace the admin create method body**

Replace `CreateBattery` in `web/Controllers/AdminController.cs` with:

```csharp
[HttpPost("batteries/create")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateBattery(CancellationToken cancellationToken)
{
    var form = Request.Form;
    var productId = Text(form, "productId", BatteryProductTemplateCatalog.DefaultProductId);
    var serialNumber = Text(form, "serialNumber");
    var requestedBatteryModel = FirstNonEmpty(
        Text(form, "batteryModel"),
        Text(form, "productVersion"));
    var now = DateTimeOffset.UtcNow.ToString("O");
    var editablePolicy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
    var filteredForm = ApplyBatteryEditableFormValues(form, editablePolicy, forCreation: true);

    var result = await _batteryCreationService.CreateBatteryAsync(
        new BatteryCreationCommand
        {
            ProductId = productId,
            BatteryModel = requestedBatteryModel,
            ProductVersion = Text(form, "productVersion"),
            SoftwareVersion = Text(form, "softwareVersion"),
            SerialNumber = serialNumber,
            ClusterId = Text(form, "clusterId"),
            ModelNumber = Text(form, "modelNumber"),
            DisplayName = Text(form, "name"),
            FacilityId = Text(form, "facilityId"),
            ManufacturingDate = Text(form, "manufacturingDate", now[..10]),
            CustomizeBatteryBeforeInsert = (battery, createNow) =>
            {
                ApplyPassportForm(battery, filteredForm, createNow);
            }
        },
        new BatteryCreationActor(CurrentActor(), "admin-ui", "admin-ui"),
        BatteryCreationClusterScope.Unrestricted,
        cancellationToken);

    if (!result.Success)
    {
        return await ReturnNewBatteryFormWithErrorAsync(form, result.Message, cancellationToken);
    }

    TempData["StatusMessage"] = $"Battery {result.BatteryId} created. Create the first passport when the data is ready.";
    return Redirect("/admin/clusters?tab=batteries");
}
```

- [ ] **Step 5: Run the admin reuse test**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiBatteryCreateTests.AdminBatteryCreate_ShouldUseSharedCreationServiceAndKeepEditableFormHook" /p:UseAppHost=false
```

Expected: PASS.

- [ ] **Step 6: Run existing battery admin workflow tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryAdminWorkflowTests|FullyQualifiedName~BatteryRouteAndRegistryTests|FullyQualifiedName~AdminFeedbackImplementationTests" /p:UseAppHost=false
```

Expected: PASS. If a source test expects the old direct insert string, update that test to assert `_batteryCreationService.CreateBatteryAsync` instead.

- [ ] **Step 7: Commit**

Run:

```powershell
git add BatteryPassWeb.Tests\ExternalApiBatteryCreateTests.cs web\Controllers\AdminController.cs
git commit -m "refactor: route admin battery creation through shared service"
```

---

### Task 4: External API Create Battery Endpoint

**Files:**
- Modify: `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`
- Modify: `web/Controllers/ExternalApiController.cs:48-78`
- Modify: `web/Controllers/ExternalApiController.cs:108-145`
- Modify: `web/Controllers/ExternalApiController.cs:1338-1355`

- [ ] **Step 1: Add failing external API endpoint tests**

Append these tests to `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`:

```csharp
[Fact]
public void ExternalApi_ShouldExposeCreateBatteryEndpointWithWriteAccessAndSharedService()
{
    var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

    Assert.Contains("[HttpPost(\"batteries\")]", source);
    Assert.Contains("CreateBattery(", source);
    Assert.Contains("ExternalTokenRequirement.Write", source);
    Assert.Contains("_batteryCreationService.CreateBatteryAsync", source);
    Assert.Contains("BatteryCreationClusterScope.FromToken(auth.TokenContext!)", source);
    Assert.Contains("createdByTokenId", source);
    Assert.Contains("createPassportPath", source);
}

[Fact]
public void ExternalApiCreateBattery_ShouldParseOnlyExplicitCreationFields()
{
    var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

    Assert.Contains("ReadBatteryCreationCommand", source);
    Assert.Contains("ReadString(payload, \"batteryFamily\")", source);
    Assert.Contains("ReadString(payload, \"productId\")", source);
    Assert.Contains("ReadString(payload, \"batteryModel\")", source);
    Assert.Contains("ReadString(payload, \"productVersion\")", source);
    Assert.Contains("ReadString(payload, \"softwareVersion\")", source);
    Assert.Contains("ReadString(payload, \"serialNumber\")", source);
    Assert.Contains("ReadString(payload, \"clusterId\")", source);
    Assert.DoesNotContain("BsonDocument.Parse", source);
}
```

- [ ] **Step 2: Run the external API endpoint tests and verify they fail**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiBatteryCreateTests.ExternalApi" /p:UseAppHost=false
```

Expected: FAIL because `ExternalApiController` does not expose `POST /api/external/v1/batteries`.

- [ ] **Step 3: Inject `BatteryCreationService` into `ExternalApiController`**

In `web/Controllers/ExternalApiController.cs`, add:

```csharp
private readonly BatteryCreationService _batteryCreationService;
```

Add the constructor parameter after `BatteryTemplateUpdateService batteryTemplateUpdateService`:

```csharp
BatteryCreationService batteryCreationService,
```

Assign it:

```csharp
_batteryCreationService = batteryCreationService;
```

- [ ] **Step 4: Add the create endpoint**

Add this action after `ListClusterBatteries` and before `GetBattery`:

```csharp
[HttpPost("batteries")]
public async Task<IActionResult> CreateBattery([FromBody] JsonElement payload, CancellationToken cancellationToken)
{
    var auth = await AuthorizeExternalApiAsync(ExternalTokenRequirement.Write, cancellationToken);
    if (auth.ErrorResult != null)
    {
        return auth.ErrorResult;
    }

    if (payload.ValueKind != JsonValueKind.Object)
    {
        return Envelope(StatusCodes.Status400BadRequest, "Body must be a JSON object.");
    }

    var result = await _batteryCreationService.CreateBatteryAsync(
        ReadBatteryCreationCommand(payload),
        new BatteryCreationActor(
            auth.TokenContext!.Name,
            "external-api",
            "external-api",
            auth.TokenContext.TokenId),
        BatteryCreationClusterScope.FromToken(auth.TokenContext!),
        cancellationToken);

    if (!result.Success)
    {
        return Envelope(result.StatusCode, result.Message);
    }

    var battery = result.Battery!;
    return Envelope(StatusCodes.Status201Created, result.Message, new
    {
        batteryId = result.BatteryId,
        batteryFamily = BsonHelpers.GetString(battery, "identity", "batteryFamily"),
        batteryModel = BsonHelpers.GetString(battery, "identity", "batteryModel"),
        softwareVersion = BsonHelpers.GetString(battery, "identity", "softwareVersion"),
        serialNumber = BsonHelpers.GetString(battery, "identity", "serialNumber"),
        clusterId = BsonHelpers.GetString(battery, "clusterId"),
        createdBy = BsonHelpers.GetString(battery, "createdBy"),
        createdByType = BsonHelpers.GetString(battery, "createdByType"),
        createdByTokenId = BsonHelpers.GetString(battery, "createdByTokenId"),
        createPassportPath = $"/api/external/v1/batteries/{Uri.EscapeDataString(result.BatteryId)}/passports"
    });
}
```

- [ ] **Step 5: Add explicit JSON parsing helpers**

Near the existing `TryGetPropertyIgnoreCase` helper in `ExternalApiController`, add:

```csharp
private static BatteryCreationCommand ReadBatteryCreationCommand(JsonElement payload)
{
    return new BatteryCreationCommand
    {
        BatteryFamily = ReadString(payload, "batteryFamily"),
        ProductId = ReadString(payload, "productId"),
        BatteryModel = ReadString(payload, "batteryModel"),
        ProductVersion = ReadString(payload, "productVersion"),
        SoftwareVersion = ReadString(payload, "softwareVersion"),
        SerialNumber = ReadString(payload, "serialNumber"),
        ClusterId = ReadString(payload, "clusterId"),
        ModelNumber = ReadString(payload, "modelNumber"),
        DisplayName = FirstNonEmpty(ReadString(payload, "displayName"), ReadString(payload, "name")),
        FacilityId = ReadString(payload, "facilityId"),
        ManufacturingDate = ReadString(payload, "manufacturingDate")
    };
}

private static string ReadString(JsonElement payload, string propertyName)
{
    if (!TryGetPropertyIgnoreCase(payload, propertyName, out var property)
        || property.ValueKind != JsonValueKind.String)
    {
        return string.Empty;
    }

    return property.GetString()?.Trim() ?? string.Empty;
}

private static string FirstNonEmpty(params string[] values)
{
    return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
```

- [ ] **Step 6: Run external API tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiBatteryCreateTests.ExternalApi" /p:UseAppHost=false
```

Expected: PASS.

- [ ] **Step 7: Run existing external API tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiBatteryIdTests|FullyQualifiedName~ExternalApiBatteryVersionTests|FullyQualifiedName~ExternalApiSecurityServiceTests" /p:UseAppHost=false
```

Expected: PASS.

- [ ] **Step 8: Commit**

Run:

```powershell
git add BatteryPassWeb.Tests\ExternalApiBatteryCreateTests.cs web\Controllers\ExternalApiController.cs
git commit -m "feat: expose external battery creation endpoint"
```

---

### Task 5: Help Page And QA Documentation

**Files:**
- Modify: `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`
- Modify: `web/Views/Help/Index.cshtml`
- Modify: `docs/end-user-testing-guide.md`
- Modify: `docs/qa-test-pack.md`

- [ ] **Step 1: Add failing docs tests**

Append these tests to `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`:

```csharp
[Fact]
public void ExternalApiHelp_ShouldDocumentCreateBatteryEndpointAndSeparatePassportCreation()
{
    var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

    Assert.Contains("@Model.BasePath/batteries</code><small>Create battery record", help);
    Assert.Contains("serialNumber", help);
    Assert.Contains("batteryFamily", help);
    Assert.Contains("batteryModel", help);
    Assert.Contains("softwareVersion", help);
    Assert.Contains("clusterId", help);
    Assert.Contains("Create the battery record first, then create a passport snapshot", help);
    Assert.Contains("SN-API-REPLACE-ME", help);
}

[Fact]
public void QaDocs_ShouldIncludeExternalApiBatteryCreationChecks()
{
    var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
    var qa = File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));

    Assert.Contains("POST /api/external/v1/batteries", guide);
    Assert.Contains("duplicate serial", guide, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("POST /api/external/v1/batteries", qa);
    Assert.Contains("battery.created", qa);
}
```

- [ ] **Step 2: Run the docs tests and verify they fail**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiBatteryCreateTests.ExternalApiHelp|FullyQualifiedName~ExternalApiBatteryCreateTests.QaDocs" /p:UseAppHost=false
```

Expected: FAIL because `/help` and QA docs do not document battery creation yet.

- [ ] **Step 3: Update `/help` endpoint table**

In `web/Views/Help/Index.cshtml`, add a table row near the cluster battery list row and before `GET @Model.BasePath/batteries/{batteryId}`:

```cshtml
<tr class="bp-help-doc-item">
    <td><span class="bp-help-method">POST</span></td>
    <td><span class="bp-help-endpoint-cell"><code>@Model.BasePath/batteries</code><small>Create battery record</small></span></td>
    <td><span class="bp-help-access-pill">Read-write</span></td>
    <td>Create a battery after validating family, model, software, serial, and cluster.</td>
    <td class="bp-action-cell">
        <button type="button" class="bp-action-button bp-help-detail-toggle" data-api-help-detail-target="api-detail-create-battery" aria-expanded="false" aria-controls="api-detail-create-battery" aria-label="Show create battery endpoint details" title="Show create battery endpoint details" data-tooltip="Details">
            <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14" /><path d="M5 12h14" /></svg>
        </button>
    </td>
</tr>
```

- [ ] **Step 4: Add the help detail panel**

Inside the detail panel area in `web/Views/Help/Index.cshtml`, add:

```cshtml
<article id="api-detail-create-battery" class="bp-help-detail-panel" data-api-help-detail-panel>
    <div>
        <span class="bp-summary-title">Create battery</span>
        <h3>Create the battery record first, then create a passport snapshot.</h3>
        <p>Requires a read-write token scoped to the submitted cluster. The API validates Battery Family, Battery Model, software version, globally unique serial number, and cluster access before inserting the battery. It returns the generated Battery ID.</p>
    </div>
    <pre class="bp-help-pre"><code>POST @Model.BasePath/batteries
{
  "batteryFamily": "Compact 7M",
  "batteryModel": "2.0",
  "softwareVersion": "4.0",
  "serialNumber": "SN-API-REPLACE-ME",
  "clusterId": "cluster-north-operations"
}</code></pre>
</article>
```

- [ ] **Step 5: Add the workbench preset**

In the JavaScript examples object in `web/Views/Help/Index.cshtml`, add a preset:

```javascript
createBattery: {
    method: 'POST',
    path: `${basePath}/batteries`,
    token: 'sample-read-write',
    body: JSON.stringify({
        batteryFamily: 'Compact 7M',
        batteryModel: '2.0',
        softwareVersion: '4.0',
        serialNumber: 'SN-API-REPLACE-ME',
        clusterId: 'cluster-north-operations'
    }, null, 2)
},
```

Add the option wherever the preset dropdown lists examples:

```html
<option value="createBattery">Create battery</option>
```

- [ ] **Step 6: Update QA docs**

In `docs/end-user-testing-guide.md`, add this API example near the external API examples:

````markdown
Create a battery through the external API. Change the serial number before each run because serial numbers are globally unique.

```bash
curl -X POST "http://localhost:5186/api/external/v1/batteries" \
  -H "Authorization: Basic $readWriteToken" \
  -H "Content-Type: application/json" \
  -d '{
    "batteryFamily": "Compact 7M",
    "batteryModel": "2.0",
    "softwareVersion": "4.0",
    "serialNumber": "SN-API-REPLACE-ME",
    "clusterId": "cluster-north-operations"
  }'
```

Expected: `201 Created` with a generated `batteryId`. Reusing the same serial returns `409 Conflict`. Create the first passport separately with `POST /api/external/v1/batteries/{batteryId}/passports`.
````

In `docs/qa-test-pack.md`, add a row:

```markdown
| API battery creation | POST `/api/external/v1/batteries` with a read-write token, valid family/model/software, valid cluster, and a unique serial. | Response returns `201 Created` and a generated Battery ID; a `battery.created` audit event is recorded; reusing the serial returns `409 Conflict`; no passport is created until `/batteries/{batteryId}/passports` is called. |
```

- [ ] **Step 7: Run docs tests**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~ExternalApiBatteryCreateTests.ExternalApiHelp|FullyQualifiedName~ExternalApiBatteryCreateTests.QaDocs" /p:UseAppHost=false
```

Expected: PASS.

- [ ] **Step 8: Commit**

Run:

```powershell
git add BatteryPassWeb.Tests\ExternalApiBatteryCreateTests.cs web\Views\Help\Index.cshtml docs\end-user-testing-guide.md docs\qa-test-pack.md
git commit -m "docs: document external battery creation"
```

---

### Task 6: Full Verification

**Files:**
- Verify all files touched by Tasks 1-5.

- [ ] **Step 1: Run focused test suite**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryAuditServiceTests|FullyQualifiedName~BatteryCreationServiceTests|FullyQualifiedName~ExternalApiBatteryCreateTests|FullyQualifiedName~ExternalApiBatteryIdTests|FullyQualifiedName~ExternalApiBatteryVersionTests|FullyQualifiedName~BatteryAdminWorkflowTests|FullyQualifiedName~BatteryRouteAndRegistryTests" /p:UseAppHost=false
```

Expected: PASS.

- [ ] **Step 2: Run full test suite**

Run:

```powershell
dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj /p:UseAppHost=false
```

Expected: PASS.

- [ ] **Step 3: Inspect final diff**

Run:

```powershell
git status --short --untracked-files=all
git log --oneline -6
```

Expected: no uncommitted files from these tasks, and recent commits include the task commits.

- [ ] **Step 4: Manual API smoke test when MongoDB is available**

Start the app if it is not already running:

```powershell
dotnet run --project web\BatteryPassWeb.csproj
```

Use `/help` to copy the current read-write token. Then run a create request with a unique serial:

```powershell
$token = "<read-write-token-from-help>"
$encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$token`:"))
$serial = "SN-API-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
$body = @{
  batteryFamily = "Compact 7M"
  batteryModel = "2.0"
  softwareVersion = "4.0"
  serialNumber = $serial
  clusterId = "cluster-north-operations"
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:5186/api/external/v1/batteries" -Headers @{ Authorization = "Basic $encoded" } -ContentType "application/json" -Body $body
```

Expected: response has `success = true`, `code = 201`, and a non-empty `data.batteryId`.

- [ ] **Step 5: Manual duplicate serial smoke test**

Run the same request body again.

Expected: response has `success = false`, `code = 409`, and message `Battery serial number already exists.`

- [ ] **Step 6: Manual passport separation smoke test**

Search the returned Battery ID from the home page or list the battery from `/admin/clusters?tab=batteries`.

Expected: battery exists and has no passport until `POST /api/external/v1/batteries/{batteryId}/passports` is called with a lifecycle token.
