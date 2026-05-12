# Battery ID Passport Snapshot Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the ASP.NET demonstrator from passport-centered management to battery-centered management with separate deterministic Battery IDs and Passport IDs.

**Architecture:** Add focused battery services alongside the existing passport services, then move routes, registry, admin, API, QR, telemetry, and reset flows onto Battery ID while preserving passport snapshots for trust/reporting. Keep existing ProductTemplate internals where useful, but expose Battery Family and Battery Model in all user-facing and API surfaces.

**Tech Stack:** ASP.NET Core MVC on .NET 10, MongoDB.Driver, Razor views, QRCoder, xUnit tests.

---

## Scope Check

The spec spans several surfaces, but they are tightly coupled by one data model change: Battery as mutable source and Passport as immutable snapshot. This plan keeps them in one implementation sequence because partial completion without the Battery/Passport split would leave the app inconsistent. Each task produces a testable slice and ends with a commit.

## File Structure

Create:

- `web/Services/BatteryIdService.cs`: deterministic HMAC ID generation and input normalization.
- `web/Services/BatteryRepository.cs`: `batteries` collection reads, writes, indexes, and summaries.
- `web/Services/BatteryPassportSnapshotService.cs`: creates immutable passport snapshots from battery source documents.
- `web/Services/BatteryRouteResolutionService.cs`: resolves root route IDs as Battery ID first, then Passport ID.
- `web/Models/ViewModels/BatteryViewModels.cs`: battery registry/admin/public view models and passport history rows.
- `web/Views/Passport/Battery.cshtml`: public battery-level passport list.
- `BatteryPassWeb.Tests/BatteryIdServiceTests.cs`: deterministic ID behavior.
- `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`: source/snapshot/supersede behavior.
- `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`: route/search/registry/QR expectations.
- `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`: API route and token workflow expectations.

Modify:

- `web/Configuration/BatteryPassOptions.cs`: add `IdGenerationSecret`.
- `web/Program.cs`: bind `ID_GENERATION_SECRET`, register new services.
- `web/Services/ProductTemplateModels.cs`: rename user-facing Battery version output to Battery Model where view models/API responses are built; add battery document builders where needed.
- `web/Services/ProductTemplateService.cs`: reset/reseed batteries and passports, not only passports.
- `web/Services/PassportRepository.cs`: add Battery ID-aware lookup, list-by-battery, latest public/admin queries, snapshot metadata updates.
- `web/Services/BatteryTelemetryRepository.cs`: switch primary key from `passportId` to `batteryId`.
- `web/Services/PassportViewModelFactory.cs`: include Battery ID, historical/latest flags, and telemetry overlay behavior.
- `web/Models/ViewModels/PassportViewModel.cs`, `PassportSummaryViewModel.cs`, `PassportDetailViewModel.cs`, `EditPassportViewModel.cs`: add Battery ID, Battery Model, latest/historical fields.
- `web/Controllers/HomeController.cs`: front-page search resolves Battery ID to `/<batteryId>` and Passport ID to `/<passportId>`.
- `web/Controllers/PassportController.cs`: root route resolution for battery view, latest view, exact passport detail/summary, historical telemetry behavior.
- `web/Controllers/RegistryController.cs`: signed-in battery rows; no anonymous registry browsing.
- `web/Controllers/AdminController.cs`: battery-centered admin table, create/edit battery, create passport snapshot, reset wording.
- `web/Controllers/ClusterAdminController.cs`: battery-centered local admin list/edit where cluster-scoped.
- `web/Controllers/ExternalApiController.cs`: Battery ID routes for battery operations and Passport ID routes for trust operations.
- `web/Controllers/QrController.cs`: QR generated from Battery ID and latest route.
- `web/Controllers/PassportsApiController.cs`: align admin API or mark it admin/passport-specific with Battery ID in responses.
- `web/Views/Home/Index.cshtml`: ID search copy and QR scan extraction.
- `web/Views/Registry/Index.cshtml`: battery-row registry.
- `web/Views/Admin/Clusters.cshtml`: battery-centered admin tab with embedded passport history.
- `web/Views/Admin/EditPassport.cshtml`: convert creation/editing flow to battery/passport snapshot model.
- `web/Views/ClusterAdmin/Passports.cshtml`, `web/Views/ClusterAdmin/EditPassport.cshtml`: update to battery language and Battery ID display.
- `web/Views/Passport/Summary.cshtml`, `web/Views/Passport/Detail.cshtml`: show Battery ID and historical/latest indicators.
- `web/Views/Help/Index.cshtml`: external API docs use Battery ID and Battery Model.
- `web/wwwroot/css/site.css`: table/history/status styling for battery rows and historical badges.
- `web/.env.example`, `web/appsettings.json`, `web/appsettings.Development.json`: document `ID_GENERATION_SECRET`.
- `docs/end-user-testing-guide.md`, `docs/qa-test-pack.md`: update user-facing wording and API workflow.

Verification commands:

- `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
- `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatteryId`
- `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatterySnapshot`
- `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatteryRoute`
- `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter ExternalApiBatteryId`

---

### Task 1: ID Generation And Configuration

**Files:**
- Create: `web/Services/BatteryIdService.cs`
- Create: `BatteryPassWeb.Tests/BatteryIdServiceTests.cs`
- Modify: `web/Configuration/BatteryPassOptions.cs`
- Modify: `web/Program.cs`
- Modify: `web/.env.example`
- Modify: `web/appsettings.json`
- Modify: `web/appsettings.Development.json`

- [ ] **Step 1: Write failing ID generation tests**

Create `BatteryPassWeb.Tests/BatteryIdServiceTests.cs`:

```csharp
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class BatteryIdServiceTests
{
    private const string Secret = "local-development-id-generation-secret-32-bytes";

    [Fact]
    public void CreateBatteryId_ReturnsStableSeventyCharacterBase64Url()
    {
        var service = new BatteryIdService(Secret, allowMissingSecret: false);

        var first = service.CreateBatteryId("Compact 7M", "SN-001");
        var second = service.CreateBatteryId(" compact 7m ", " sn-001 ");

        Assert.Equal(first, second);
        Assert.Equal(70, first.Length);
        Assert.Matches("^[A-Za-z0-9_-]{70}$", first);
    }

    [Fact]
    public void CreateBatteryId_ChangesWhenIdentityInputsChange()
    {
        var service = new BatteryIdService(Secret, allowMissingSecret: false);

        var original = service.CreateBatteryId("Compact 7M", "SN-001");

        Assert.NotEqual(original, service.CreateBatteryId("Core", "SN-001"));
        Assert.NotEqual(original, service.CreateBatteryId("Compact 7M", "SN-002"));
    }

    [Fact]
    public void CreatePassportId_UsesTimestampBatteryIdAndBatteryModel()
    {
        var service = new BatteryIdService(Secret, allowMissingSecret: false);
        var batteryId = service.CreateBatteryId("Compact 7M", "SN-001");
        var createdAt = DateTimeOffset.Parse("2026-05-12T10:15:30.1234567Z");

        var first = service.CreatePassportId(batteryId, "Model 2.0", createdAt);
        var second = service.CreatePassportId(batteryId, "Model 2.0", createdAt);

        Assert.Equal(first, second);
        Assert.Equal(70, first.Length);
        Assert.Matches("^[A-Za-z0-9_-]{70}$", first);
        Assert.NotEqual(first, service.CreatePassportId(batteryId, "Model 3.0", createdAt));
        Assert.NotEqual(first, service.CreatePassportId(batteryId, "Model 2.0", createdAt.AddTicks(1)));
    }

    [Fact]
    public void Constructor_RejectsMissingSecretWhenFallbackNotAllowed()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new BatteryIdService(string.Empty, allowMissingSecret: false));

        Assert.Contains("ID_GENERATION_SECRET", exception.Message);
    }
}
```

- [ ] **Step 2: Run the failing ID tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatteryIdServiceTests`

Expected: FAIL because `BatteryIdService` does not exist.

- [ ] **Step 3: Implement `BatteryIdService`**

Create `web/Services/BatteryIdService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace BatteryPassWeb.Services;

public sealed class BatteryIdService
{
    public const int IdLength = 70;
    private const string DevelopmentFallbackSecret = "battery-pass-local-development-id-secret";
    private readonly byte[] _secret;

    public BatteryIdService(string secret, bool allowMissingSecret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            if (!allowMissingSecret)
            {
                throw new InvalidOperationException("ID_GENERATION_SECRET is required for Battery ID and Passport ID generation.");
            }

            secret = DevelopmentFallbackSecret;
        }

        _secret = Encoding.UTF8.GetBytes(secret.Trim());
    }

    public string CreateBatteryId(string batteryFamily, string batterySerialNumber)
    {
        var normalizedFamily = NormalizeIdentityPart(batteryFamily);
        var normalizedSerial = NormalizeIdentityPart(batterySerialNumber);
        if (string.IsNullOrWhiteSpace(normalizedFamily) || string.IsNullOrWhiteSpace(normalizedSerial))
        {
            throw new ArgumentException("Battery Family and Battery serial number are required.");
        }

        return CreateOpaqueId($"battery|{normalizedFamily}|{normalizedSerial}");
    }

    public string CreatePassportId(string batteryId, string batteryModel, DateTimeOffset snapshotCreatedAt)
    {
        var normalizedBatteryId = NormalizeIdentityPart(batteryId);
        var normalizedBatteryModel = NormalizeIdentityPart(batteryModel);
        if (string.IsNullOrWhiteSpace(normalizedBatteryId) || string.IsNullOrWhiteSpace(normalizedBatteryModel))
        {
            throw new ArgumentException("Battery ID and Battery Model are required.");
        }

        return CreateOpaqueId($"passport|{snapshotCreatedAt.UtcDateTime:O}|{normalizedBatteryId}|{normalizedBatteryModel}");
    }

    public static string NormalizeIdentityPart(string value)
    {
        return string.Join(
            " ",
            (value ?? string.Empty).Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private string CreateOpaqueId(string payload)
    {
        using var hmac = new HMACSHA512(_secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var encoded = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        while (encoded.Length < IdLength)
        {
            var extra = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{payload}|{encoded.Length}"));
            encoded += Convert.ToBase64String(extra)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        return encoded[..IdLength];
    }
}
```

- [ ] **Step 4: Wire configuration**

Add to `web/Configuration/BatteryPassOptions.cs`:

```csharp
public string IdGenerationSecret { get; set; } = string.Empty;
```

Add to the options binding in `web/Program.cs`:

```csharp
options.IdGenerationSecret = Environment.GetEnvironmentVariable("ID_GENERATION_SECRET")
    ?? builder.Configuration["BatteryPass:IdGenerationSecret"]
    ?? string.Empty;
```

Register the service in `web/Program.cs`:

```csharp
builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BatteryPassOptions>>().Value;
    var environment = provider.GetRequiredService<IWebHostEnvironment>();
    return new BatteryIdService(options.IdGenerationSecret, environment.IsDevelopment());
});
```

Add `ID_GENERATION_SECRET=` to `web/.env.example`.

Add `"IdGenerationSecret": ""` under `BatteryPass` in `web/appsettings.json` and `web/appsettings.Development.json`.

- [ ] **Step 5: Run ID tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatteryIdServiceTests`

Expected: PASS.

Commit:

```bash
git add web/Services/BatteryIdService.cs web/Configuration/BatteryPassOptions.cs web/Program.cs web/.env.example web/appsettings.json web/appsettings.Development.json BatteryPassWeb.Tests/BatteryIdServiceTests.cs
git commit -m "feat: add deterministic battery and passport ids"
```

---

### Task 2: Battery Repository And View Models

**Files:**
- Create: `web/Services/BatteryRepository.cs`
- Create: `web/Models/ViewModels/BatteryViewModels.cs`
- Modify: `web/Program.cs`
- Modify: `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`

- [ ] **Step 1: Write repository contract tests as source assertions**

Create `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs` with the first tests:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class BatterySnapshotWorkflowTests
{
    [Fact]
    public void Program_ShouldRegisterBatteryServices()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<BatteryRepository>", program);
        Assert.Contains("AddSingleton<BatteryPassportSnapshotService>", program);
    }

    [Fact]
    public void BatteryRepository_ShouldUseBatteriesCollectionAndBatteryIdIndexes()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));

        Assert.Contains("GetCollection<BsonDocument>(\"batteries\")", source);
        Assert.Contains("EnsureIndexesAsync", source);
        Assert.Contains("Ascending(\"batteryId\")", source);
        Assert.Contains("Unique = true", source);
        Assert.Contains("CreateBatteryAsync", source);
        Assert.Contains("UpdateBatteryFieldsAsync", source);
        Assert.Contains("GetByBatteryIdAsync", source);
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

- [ ] **Step 2: Run the failing repository tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatterySnapshotWorkflowTests`

Expected: FAIL because the repository and snapshot service are not present.

- [ ] **Step 3: Add battery view models**

Create `web/Models/ViewModels/BatteryViewModels.cs`:

```csharp
namespace BatteryPassWeb.Models.ViewModels;

public sealed class BatterySummaryViewModel
{
    public string BatteryId { get; init; } = string.Empty;
    public string BatteryFamily { get; init; } = string.Empty;
    public string BatteryModel { get; init; } = string.Empty;
    public string BatterySerialNumber { get; init; } = string.Empty;
    public string ClusterId { get; init; } = string.Empty;
    public string ClusterLabel { get; init; } = "No cluster assigned";
    public int PassportCount { get; init; }
    public string LatestPassportId { get; init; } = string.Empty;
    public string LatestPassportStatus { get; init; } = "Draft";
    public string UpdatedDate { get; init; } = string.Empty;
    public IReadOnlyList<BatteryPassportHistoryRowViewModel> Passports { get; init; } = [];
}

public sealed class BatteryPassportHistoryRowViewModel
{
    public string PassportId { get; init; } = string.Empty;
    public string BatteryId { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string PassportStatus { get; init; } = "Draft";
    public bool IsLatestForBattery { get; init; }
    public bool IsPubliclyVisible { get; init; }
}

public sealed class BatteryDetailViewModel
{
    public required BatterySummaryViewModel Battery { get; init; }
    public string AccessNotice { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Implement `BatteryRepository`**

Create `web/Services/BatteryRepository.cs`:

```csharp
using BatteryPassWeb.Models.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class BatteryRepository
{
    private readonly MongoContext _mongoContext;

    public BatteryRepository(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public bool IsAvailable => _mongoContext.Database != null;

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("batteryId"),
                    new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("clusterId")),
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("identity.batteryFamily").Ascending("identity.serialNumber"),
                    new CreateIndexOptions { Unique = true })
            ],
            cancellationToken);
    }

    public async Task<BsonDocument?> GetByBatteryIdAsync(string batteryId, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(batteryId))
        {
            return null;
        }

        return await collection.Find(Builders<BsonDocument>.Filter.Eq("batteryId", batteryId.Trim())).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BsonDocument>> SearchDocumentsAsync(string query, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return [];
        }

        var builder = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>();
        if (!includeArchived)
        {
            filters.Add(builder.Ne("status", "archived"));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var regex = new BsonRegularExpression(query.Trim(), "i");
            filters.Add(builder.Or(
                builder.Regex("batteryId", regex),
                builder.Regex("identity.batteryFamily", regex),
                builder.Regex("identity.batteryModel", regex),
                builder.Regex("identity.serialNumber", regex),
                builder.Regex("clusterId", regex)));
        }

        var filter = filters.Count == 0 ? builder.Empty : filters.Count == 1 ? filters[0] : builder.And(filters);
        return await collection.Find(filter).SortByDescending(row => row["updatedAt"]).Limit(500).ToListAsync(cancellationToken);
    }

    public async Task CreateBatteryAsync(BsonDocument battery, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        battery.Remove("_id");
        await collection.InsertOneAsync(battery, cancellationToken: cancellationToken);
    }

    public async Task ReplaceAsync(string batteryId, BsonDocument battery, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        battery.Remove("_id");
        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("batteryId", batteryId),
            battery,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<bool> UpdateBatteryFieldsAsync(string batteryId, IReadOnlyDictionary<string, BsonValue> setValues, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null || string.IsNullOrWhiteSpace(batteryId) || setValues.Count == 0)
        {
            return false;
        }

        var updates = setValues.Select(pair => Builders<BsonDocument>.Update.Set(pair.Key, pair.Value)).ToList();
        updates.Add(Builders<BsonDocument>.Update.Set("updatedAt", DateTimeOffset.UtcNow.ToString("O")));

        var result = await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("batteryId", batteryId),
            Builders<BsonDocument>.Update.Combine(updates),
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public BatterySummaryViewModel ToSummary(BsonDocument battery, IReadOnlyList<BatteryPassportHistoryRowViewModel> passportRows, string clusterLabel)
    {
        var latest = passportRows.FirstOrDefault(row => row.IsLatestForBattery) ?? passportRows.OrderByDescending(row => row.CreatedAt).FirstOrDefault();
        return new BatterySummaryViewModel
        {
            BatteryId = BsonHelpers.GetString(battery, "batteryId"),
            BatteryFamily = BsonHelpers.GetString(battery, "identity", "batteryFamily"),
            BatteryModel = BsonHelpers.GetString(battery, "identity", "batteryModel"),
            BatterySerialNumber = BsonHelpers.GetString(battery, "identity", "serialNumber"),
            ClusterId = BsonHelpers.GetString(battery, "clusterId"),
            ClusterLabel = clusterLabel,
            PassportCount = passportRows.Count,
            LatestPassportId = latest?.PassportId ?? string.Empty,
            LatestPassportStatus = latest?.PassportStatus ?? "Draft",
            UpdatedDate = BsonHelpers.GetString(battery, "updatedAt"),
            Passports = passportRows
        };
    }

    private IMongoCollection<BsonDocument>? GetCollection()
    {
        return _mongoContext.Database?.GetCollection<BsonDocument>("batteries");
    }
}
```

- [ ] **Step 5: Register repository**

Add to `web/Program.cs` near the other repositories:

```csharp
builder.Services.AddSingleton<BatteryRepository>();
builder.Services.AddSingleton<BatteryPassportSnapshotService>();
builder.Services.AddSingleton<BatteryRouteResolutionService>();
```

The snapshot and route services are added as empty classes in later tasks. For this task, create minimal files with constructors if compilation requires them:

```csharp
namespace BatteryPassWeb.Services;

public sealed class BatteryPassportSnapshotService
{
}
```

```csharp
namespace BatteryPassWeb.Services;

public sealed class BatteryRouteResolutionService
{
}
```

- [ ] **Step 6: Run tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatterySnapshotWorkflowTests`

Expected: PASS.

Commit:

```bash
git add web/Services/BatteryRepository.cs web/Services/BatteryPassportSnapshotService.cs web/Services/BatteryRouteResolutionService.cs web/Models/ViewModels/BatteryViewModels.cs web/Program.cs BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs
git commit -m "feat: add battery repository foundation"
```

---

### Task 3: Passport Snapshot Creation And Superseding

**Files:**
- Modify: `web/Services/BatteryPassportSnapshotService.cs`
- Modify: `web/Services/PassportRepository.cs`
- Modify: `web/Services/ProductTemplateModels.cs`
- Modify: `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`

- [ ] **Step 1: Add failing snapshot behavior tests**

Append to `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`:

```csharp
[Fact]
public void SnapshotService_ShouldCreatePassportFromBatteryAndRecordSnapshotMetadata()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));

    Assert.Contains("CreatePassportSnapshotAsync", source);
    Assert.Contains("CreatePassportId", source);
    Assert.Contains("[\"batteryId\"]", source);
    Assert.Contains("[\"snapshot\"]", source);
    Assert.Contains("isLatestForBattery", source);
    Assert.Contains("supersededByPassportId", source);
}

[Fact]
public void PassportRepository_ShouldListAndSupersedePassportsByBatteryId()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));

    Assert.Contains("GetByBatteryIdAsync", source);
    Assert.Contains("ListByBatteryIdAsync", source);
    Assert.Contains("GetLatestPublicByBatteryIdAsync", source);
    Assert.Contains("MarkPreviousLatestSupersededAsync", source);
    Assert.Contains("isLatestForBattery", source);
    Assert.Contains("supersededAt", source);
}
```

- [ ] **Step 2: Run failing snapshot tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatterySnapshotWorkflowTests`

Expected: FAIL because snapshot methods and repository methods are missing.

- [ ] **Step 3: Implement PassportRepository battery methods**

Add to `web/Services/PassportRepository.cs` before `GetCollection()`:

```csharp
public async Task<BsonDocument?> GetByBatteryIdAsync(string batteryId, CancellationToken cancellationToken = default)
{
    var collection = GetCollection();
    if (collection == null || string.IsNullOrWhiteSpace(batteryId))
    {
        return null;
    }

    return await collection.Find(Builders<BsonDocument>.Filter.Eq("batteryId", batteryId.Trim())).FirstOrDefaultAsync(cancellationToken);
}

public async Task<IReadOnlyList<BsonDocument>> ListByBatteryIdAsync(string batteryId, bool includeArchived = false, CancellationToken cancellationToken = default)
{
    var collection = GetCollection();
    if (collection == null || string.IsNullOrWhiteSpace(batteryId))
    {
        return [];
    }

    var builder = Builders<BsonDocument>.Filter;
    var filter = builder.Eq("batteryId", batteryId.Trim());
    if (!includeArchived)
    {
        filter = builder.And(filter, builder.Ne("registryInfo.status", "archived"));
    }

    return await collection.Find(filter).SortByDescending(row => row["snapshot"]["createdAt"]).ToListAsync(cancellationToken);
}

public async Task<BsonDocument?> GetLatestPublicByBatteryIdAsync(
    string batteryId,
    PassportPublishPolicyService publishPolicy,
    CancellationToken cancellationToken = default)
{
    var passports = await ListByBatteryIdAsync(batteryId, includeArchived: false, cancellationToken);
    return passports.FirstOrDefault(publishPolicy.IsPubliclyVisible);
}

public async Task MarkPreviousLatestSupersededAsync(
    string batteryId,
    string supersededByPassportId,
    string supersededAt,
    CancellationToken cancellationToken = default)
{
    var collection = GetCollection();
    if (collection == null || string.IsNullOrWhiteSpace(batteryId))
    {
        return;
    }

    await collection.UpdateManyAsync(
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("batteryId", batteryId),
            Builders<BsonDocument>.Filter.Eq("isLatestForBattery", true)),
        Builders<BsonDocument>.Update
            .Set("isLatestForBattery", false)
            .Set("supersededAt", supersededAt)
            .Set("supersededByPassportId", supersededByPassportId)
            .Set("registryInfo.updatedAt", supersededAt),
        cancellationToken: cancellationToken);
}
```

Update `SearchDocumentsAsync` filters in `PassportRepository` to include:

```csharp
builder.Regex("batteryId", regex),
builder.Regex("snapshot.batteryModel", regex),
```

- [ ] **Step 4: Implement snapshot service**

Replace `web/Services/BatteryPassportSnapshotService.cs`:

```csharp
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class BatteryPassportSnapshotService
{
    private readonly BatteryIdService _batteryIdService;
    private readonly PassportRepository _passportRepository;

    public BatteryPassportSnapshotService(
        BatteryIdService batteryIdService,
        PassportRepository passportRepository)
    {
        _batteryIdService = batteryIdService;
        _passportRepository = passportRepository;
    }

    public async Task<BsonDocument> CreatePassportSnapshotAsync(
        BsonDocument battery,
        string actor,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        var batteryId = BsonHelpers.GetString(battery, "batteryId");
        var batteryModel = BsonHelpers.GetString(battery, "identity", "batteryModel");
        var passportId = _batteryIdService.CreatePassportId(batteryId, batteryModel, createdAt);
        var passport = BuildPassportDocument(battery, passportId, actor, createdAt);

        await _passportRepository.MarkPreviousLatestSupersededAsync(
            batteryId,
            passportId,
            createdAt.ToString("O"),
            cancellationToken);

        passport["isLatestForBattery"] = true;
        passport["snapshot"] = new BsonDocument
        {
            ["createdAt"] = createdAt.ToString("O"),
            ["batteryFamily"] = BsonHelpers.GetString(battery, "identity", "batteryFamily"),
            ["batteryModel"] = batteryModel,
            ["batterySerialNumber"] = BsonHelpers.GetString(battery, "identity", "serialNumber")
        };
        passport["batteryId"] = batteryId;
        passport["passportId"] = passportId;
        passport.Remove("_id");

        await _passportRepository.ReplaceAsync(passportId, passport, cancellationToken);
        return passport;
    }

    private static BsonDocument BuildPassportDocument(
        BsonDocument battery,
        string passportId,
        string actor,
        DateTimeOffset createdAt)
    {
        var now = createdAt.ToString("O");
        var passport = new BsonDocument
        {
            ["passportId"] = passportId,
            ["batteryId"] = BsonHelpers.GetString(battery, "batteryId"),
            ["clusterId"] = BsonHelpers.GetString(battery, "clusterId"),
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = Guid.NewGuid().ToString("N"),
                ["status"] = "draft",
                ["createdAt"] = now,
                ["updatedAt"] = now
            },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = false,
                ["status"] = "unvalidated",
                ["signedAt"] = BsonNull.Value
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = "unvalidated",
                ["isDirty"] = false,
                ["latestHash"] = string.Empty,
                ["latestProof"] = new BsonDocument()
            },
            ["snapshot"] = new BsonDocument
            {
                ["createdAt"] = now,
                ["createdBy"] = actor,
                ["batteryFamily"] = BsonHelpers.GetString(battery, "identity", "batteryFamily"),
                ["batteryModel"] = BsonHelpers.GetString(battery, "identity", "batteryModel"),
                ["batterySerialNumber"] = BsonHelpers.GetString(battery, "identity", "serialNumber")
            }
        };

        foreach (var key in new[] { "app", "aspects" })
        {
            if (battery.TryGetValue(key, out var value) && value is BsonDocument document)
            {
                passport[key] = document.DeepClone();
            }
        }

        return passport;
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
}
```

- [ ] **Step 5: Ensure snapshot immutability is explicit**

Add a comment at the top of `CreatePassportSnapshotAsync`:

```csharp
// Passport snapshots are immutable for non-telemetry data after this point.
// Later battery edits must create another passport instead of mutating this document.
```

- [ ] **Step 6: Run tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatterySnapshotWorkflowTests`

Expected: PASS.

Commit:

```bash
git add web/Services/BatteryPassportSnapshotService.cs web/Services/PassportRepository.cs web/Services/ProductTemplateModels.cs BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs
git commit -m "feat: snapshot passports from batteries"
```

---

### Task 4: Reset And Seed Battery-Centered Demo Data

**Files:**
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Services/ExternalApiInitializer.cs`
- Modify: `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`

- [ ] **Step 1: Add failing reset tests**

Append to `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`:

```csharp
[Fact]
public void ProductTemplateReset_ShouldResetBatteriesPassportsAndTelemetry()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

    Assert.Contains("BatteryRepository", source);
    Assert.Contains("batteryTelemetry", source);
    Assert.Contains("CreatePassportSnapshotAsync", source);
    Assert.Contains("Seeded batteries", source);
    Assert.Contains("multiple passports", source);
}

[Fact]
public void Initializer_ShouldEnsureBatteryIndexes()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));

    Assert.Contains("BatteryRepository", source);
    Assert.Contains("_batteryRepository.EnsureIndexesAsync", source);
}
```

- [ ] **Step 2: Run failing reset tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter ProductTemplateReset`

Expected: FAIL because reset does not seed batteries.

- [ ] **Step 3: Inject BatteryRepository and snapshot service into ProductTemplateService**

Modify `ProductTemplateService` constructor parameters and fields:

```csharp
private readonly BatteryRepository _batteryRepository;
private readonly BatteryPassportSnapshotService _batteryPassportSnapshotService;

public ProductTemplateService(
    MongoContext mongoContext,
    PassportRepository passportRepository,
    PassportValidationService passportValidationService,
    PassportTrustService passportTrustService,
    AuditRevisionService auditRevisionService,
    DataCompletionPolicyService dataCompletionPolicyService,
    ClusterRepository clusterRepository,
    BatteryRepository batteryRepository,
    BatteryPassportSnapshotService batteryPassportSnapshotService)
{
    _mongoContext = mongoContext;
    _passportRepository = passportRepository;
    _passportValidationService = passportValidationService;
    _passportTrustService = passportTrustService;
    _auditRevisionService = auditRevisionService;
    _dataCompletionPolicyService = dataCompletionPolicyService;
    _clusterRepository = clusterRepository;
    _batteryRepository = batteryRepository;
    _batteryPassportSnapshotService = batteryPassportSnapshotService;
}
```

Preserve any existing constructor dependencies already present in the file when applying this change.

- [ ] **Step 4: Add battery and telemetry cleanup to reset**

Inside `ResetTemplateDemoAsync`, before seeding:

```csharp
await _mongoContext.Database.GetCollection<BsonDocument>("batteries")
    .DeleteManyAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken);
await _mongoContext.Database.GetCollection<BsonDocument>("batteryTelemetry")
    .DeleteManyAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken);
await collection.DeleteManyAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken);
```

Keep existing audit/revision cleanup, expanding the ID list after creating new seed passport IDs.

- [ ] **Step 5: Seed batteries and multiple passports**

Replace the passport-only seed loop with a battery-first loop. Use the existing seed tuples, but build battery documents first:

```csharp
// Seeded batteries: at least one seed creates multiple passports for historical/latest behavior.
var batterySeeds = new[]
{
    (Family: "compact-7m", Model: "1.0", ClusterId: "cluster-north-operations", Serial: "SN-NORTH-001", FacilityId: "NORTH-LINE-01", PassportCount: 2),
    (Family: "compact-13m", Model: "2.0", ClusterId: "cluster-south-operations", Serial: "SN-SOUTH-001", FacilityId: "SOUTH-LINE-01", PassportCount: 1),
    (Family: "core", Model: "2.0", ClusterId: "cluster-fleet-operations", Serial: "SN-FLEET-001", FacilityId: "FLEET-LINE-01", PassportCount: 1)
};
```

For each seed:

```csharp
var product = productsById.TryGetValue(seed.Family, out var selectedProduct)
    ? selectedProduct
    : BatteryProductTemplateCatalog.DefaultProduct;
var batteryId = _batteryIdService.CreateBatteryId(product.ProductName, seed.Serial);
var battery = ProductTemplatePassportBuilder.BuildBatteryFromTemplate(
    batteryId,
    product,
    seed.Model,
    seed.Serial,
    seed.ClusterId,
    seed.FacilityId,
    resetAt);
await _batteryRepository.ReplaceAsync(batteryId, battery, cancellationToken);

for (var index = 0; index < seed.PassportCount; index++)
{
    var snapshotTime = DateTimeOffset.Parse(resetAt).AddMinutes(index);
    var passport = await _batteryPassportSnapshotService.CreatePassportSnapshotAsync(battery, actor, snapshotTime, cancellationToken);
    await SignAndPublishSeedPassportAsync(passport, actor, resetAt, cancellationToken);
}
```

Add `BuildBatteryFromTemplate` to `ProductTemplatePassportBuilder` by adapting the existing passport builder into a battery document with `batteryId`, `identity`, `product`, `app`, and `aspects` fields.

- [ ] **Step 6: Ensure initializer creates battery indexes**

Modify `ExternalApiInitializer` constructor and fields:

```csharp
private readonly BatteryRepository _batteryRepository;
```

Call in `InitializeAsync` before normalizing data:

```csharp
await _batteryRepository.EnsureIndexesAsync(cancellationToken);
```

- [ ] **Step 7: Run reset tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatterySnapshotWorkflowTests`

Expected: PASS.

Commit:

```bash
git add web/Services/ProductTemplateService.cs web/Services/ProductTemplateModels.cs web/Services/ExternalApiInitializer.cs BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs
git commit -m "feat: reset demo data around batteries"
```

---

### Task 5: Telemetry By Battery ID And Latest/Historical View Models

**Files:**
- Modify: `web/Services/BatteryTelemetryRepository.cs`
- Modify: `web/Services/PassportViewModelFactory.cs`
- Modify: `web/Models/ViewModels/PassportViewModel.cs`
- Modify: `web/Models/ViewModels/PassportDetailViewModel.cs`
- Modify: `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`

- [ ] **Step 1: Add failing telemetry-by-battery tests**

Append:

```csharp
[Fact]
public void TelemetryRepository_ShouldUseBatteryIdInsteadOfPassportId()
{
    var source = File.ReadAllText(RepoFile("web", "Services", "BatteryTelemetryRepository.cs"));

    Assert.Contains("batteryId", source);
    Assert.Contains("AppendTelemetryAsync(string batteryId", source);
    Assert.Contains("ReadHistoryAsync(string batteryId", source);
    Assert.DoesNotContain("Ascending(\"passportId\")", source);
}

[Fact]
public void PassportViewModel_ShouldExposeBatteryIdAndHistoricalState()
{
    var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));
    var factory = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));

    Assert.Contains("BatteryId", model);
    Assert.Contains("BatteryModel", model);
    Assert.Contains("IsLatestForBattery", model);
    Assert.Contains("IsHistoricalPassport", model);
    Assert.Contains("batteryId", factory);
    Assert.Contains("isLatestForBattery", factory);
}
```

- [ ] **Step 2: Run failing telemetry tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter TelemetryRepository_ShouldUseBatteryIdInsteadOfPassportId`

Expected: FAIL because telemetry still uses passport ID.

- [ ] **Step 3: Change telemetry repository key**

In `BatteryTelemetryRepository`, replace `passportId` parameter names and stored fields with `batteryId`.

Index definition:

```csharp
new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("batteryId").Descending("measuredAt")),
```

Append document field:

```csharp
["batteryId"] = batteryId.Trim(),
```

Read filter:

```csharp
Builders<BsonDocument>.Filter.Eq("batteryId", batteryId.Trim()),
```

- [ ] **Step 4: Extend PassportViewModel**

Add to `PassportViewModel`:

```csharp
public string BatteryId { get; init; } = string.Empty;
public string BatteryModel { get; init; } = string.Empty;
public bool IsLatestForBattery { get; init; }
public bool IsHistoricalPassport { get; init; }
public string LatestPassportUrl { get; init; } = string.Empty;
public int RelatedPassportCount { get; init; }
```

Keep `BatteryVersion` temporarily if internal consumers still compile, but set it to the same value as `BatteryModel` until later cleanup removes user-facing labels.

- [ ] **Step 5: Populate view model fields**

In `PassportViewModelFactory.Create`, read:

```csharp
var batteryId = BsonHelpers.GetString(document, "batteryId");
var isLatestForBattery = document.GetValue("isLatestForBattery", false).ToBoolean();
var batteryModel = FirstNonEmpty(
    BsonHelpers.GetString(document, "snapshot", "batteryModel"),
    BsonHelpers.GetString(appProduct, "productVersion"));
```

Set:

```csharp
BatteryId = batteryId,
BatteryModel = batteryModel,
BatteryVersion = batteryModel,
IsLatestForBattery = isLatestForBattery,
IsHistoricalPassport = !isLatestForBattery,
LatestPassportUrl = string.IsNullOrWhiteSpace(batteryId) ? string.Empty : $"/{Uri.EscapeDataString(batteryId)}/latest",
```

- [ ] **Step 6: Run tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatterySnapshotWorkflowTests`

Expected: PASS.

Commit:

```bash
git add web/Services/BatteryTelemetryRepository.cs web/Services/PassportViewModelFactory.cs web/Models/ViewModels/PassportViewModel.cs web/Models/ViewModels/PassportDetailViewModel.cs BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs
git commit -m "feat: key telemetry by battery id"
```

---

### Task 6: Public Route Resolver, Search, And QR

**Files:**
- Modify: `web/Services/BatteryRouteResolutionService.cs`
- Modify: `web/Controllers/HomeController.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Controllers/QrController.cs`
- Modify: `web/Services/PassportQrCodeService.cs`
- Create: `web/Views/Passport/Battery.cshtml`
- Create/Modify: `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`

- [ ] **Step 1: Write failing route and QR tests**

Create `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class BatteryRouteAndRegistryTests
{
    [Fact]
    public void PublicRoutes_ShouldResolveBatteryIdLatestAndPassportId()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
        var resolver = File.ReadAllText(RepoFile("web", "Services", "BatteryRouteResolutionService.cs"));

        Assert.Contains("[HttpGet(\"{id}\")]", controller);
        Assert.Contains("[HttpGet(\"{batteryId}/latest\")]", controller);
        Assert.Contains("ResolveAsync", resolver);
        Assert.Contains("GetByBatteryIdAsync", resolver);
        Assert.Contains("GetByPassportIdAsync", resolver);
        Assert.Contains("BatteryFirst", resolver);
    }

    [Fact]
    public void HomeSearch_ShouldSendBatteryIdsToBatteryPageAndPassportIdsToSnapshot()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));

        Assert.Contains("BatteryRouteResolutionService", source);
        Assert.Contains("Battery", source);
        Assert.Contains("Passport", source);
        Assert.Contains("/{Uri.EscapeDataString(query)}", source);
        Assert.DoesNotContain("/summary\");", source);
    }

    [Fact]
    public void QrService_ShouldBuildStableBatteryLatestPayload()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportQrCodeService.cs"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));

        Assert.Contains("BuildPayloadUrl(HttpRequest request, string batteryId)", source);
        Assert.Contains("/latest", source);
        Assert.Contains("BatteryRepository", controller);
        Assert.Contains("batteryId", controller);
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

- [ ] **Step 2: Run failing route tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatteryRouteAndRegistryTests`

Expected: FAIL because route resolution is not implemented.

- [ ] **Step 3: Implement route resolver**

Replace `BatteryRouteResolutionService`:

```csharp
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public enum BatteryRouteTargetKind
{
    NotFound,
    Battery,
    Passport
}

public sealed record BatteryRouteResolution(BatteryRouteTargetKind Kind, BsonDocument? Document);

public sealed class BatteryRouteResolutionService
{
    private readonly BatteryRepository _batteryRepository;
    private readonly PassportRepository _passportRepository;

    public BatteryRouteResolutionService(BatteryRepository batteryRepository, PassportRepository passportRepository)
    {
        _batteryRepository = batteryRepository;
        _passportRepository = passportRepository;
    }

    public async Task<BatteryRouteResolution> ResolveAsync(string id, CancellationToken cancellationToken = default)
    {
        // BatteryFirst: root IDs resolve as Battery ID before Passport ID.
        var battery = await _batteryRepository.GetByBatteryIdAsync(id, cancellationToken);
        if (battery != null)
        {
            return new BatteryRouteResolution(BatteryRouteTargetKind.Battery, battery);
        }

        var passport = await _passportRepository.GetByPassportIdAsync(id, cancellationToken);
        return passport == null
            ? new BatteryRouteResolution(BatteryRouteTargetKind.NotFound, null)
            : new BatteryRouteResolution(BatteryRouteTargetKind.Passport, passport);
    }
}
```

- [ ] **Step 4: Update Home search**

Inject `BatteryRouteResolutionService` into `HomeController` and change exact match handling:

```csharp
var resolution = await _batteryRouteResolutionService.ResolveAsync(query, cancellationToken);
return resolution.Kind switch
{
    BatteryRouteTargetKind.Battery => Redirect($"/{Uri.EscapeDataString(query)}"),
    BatteryRouteTargetKind.Passport => Redirect($"/{Uri.EscapeDataString(query)}"),
    _ => RedirectToAction(nameof(Index), new { q = query, notFound = "1" })
};
```

Keep QR payload extraction but allow 70-character IDs, not only `did:web:` values.

- [ ] **Step 5: Update PassportController public routes**

Change `Detail` route signature to:

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken)
```

Resolve:

```csharp
var decodedId = Uri.UnescapeDataString(id);
var resolution = await _batteryRouteResolutionService.ResolveAsync(decodedId, cancellationToken);
if (resolution.Kind == BatteryRouteTargetKind.Battery)
{
    return await Battery(decodedId, cancellationToken);
}

if (resolution.Kind != BatteryRouteTargetKind.Passport || resolution.Document == null)
{
    return NotFound();
}
```

Add:

```csharp
[HttpGet("{batteryId}/latest")]
public async Task<IActionResult> Latest(string batteryId, CancellationToken cancellationToken)
{
    var decodedBatteryId = Uri.UnescapeDataString(batteryId);
    var document = User.Identity?.IsAuthenticated == true
        ? (await _passportRepository.ListByBatteryIdAsync(decodedBatteryId, includeArchived: false, cancellationToken)).FirstOrDefault()
        : await _passportRepository.GetLatestPublicByBatteryIdAsync(decodedBatteryId, _passportPublishPolicyService, cancellationToken);

    if (document == null)
    {
        return NotFound();
    }

    return Redirect($"/{Uri.EscapeDataString(BsonHelpers.GetString(document, "passportId"))}");
}
```

Add a private `Battery` method that builds `BatteryDetailViewModel` from the battery and `ListByBatteryIdAsync`.

- [ ] **Step 6: Create battery-level view**

Create `web/Views/Passport/Battery.cshtml`:

```cshtml
@model BatteryPassWeb.Models.ViewModels.BatteryDetailViewModel
@{
    ViewData["Title"] = "Battery passports";
    var battery = Model.Battery;
}
<main class="bp-page bp-admin-page bp-registry-page">
    <header class="bp-console-header">
        <div>
            <p class="bp-passport-id">@battery.BatteryId</p>
            <h1>@battery.BatteryFamily @battery.BatteryModel</h1>
            <p class="text-secondary mb-0">Battery serial number: @battery.BatterySerialNumber</p>
        </div>
        @if (!string.IsNullOrWhiteSpace(battery.LatestPassportId))
        {
            <a class="bp-primary-button" href="/@Uri.EscapeDataString(battery.BatteryId)/latest">Open latest passport</a>
        }
    </header>

    <section class="bp-card bp-table-card">
        <div class="bp-table-scroll">
            <table class="table table-hover align-middle bp-console-table">
                <thead>
                    <tr>
                        <th>Passport ID</th>
                        <th>Status</th>
                        <th>Created</th>
                        <th>Marker</th>
                        <th class="bp-action-cell">Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var row in battery.Passports)
                    {
                        <tr>
                            <td class="text-break">@row.PassportId</td>
                            <td>@row.PassportStatus</td>
                            <td>@(string.IsNullOrWhiteSpace(row.CreatedAt) ? "-" : row.CreatedAt[..Math.Min(10, row.CreatedAt.Length)])</td>
                            <td>@(row.IsLatestForBattery ? "Latest" : "Historical")</td>
                            <td class="bp-action-cell">
                                <div class="bp-icon-action-row">
                                    <a href="/@Uri.EscapeDataString(row.PassportId)/summary" class="bp-report-action" aria-label="Summary report" title="Summary report" data-tooltip="Summary report"></a>
                                    <a href="/@Uri.EscapeDataString(row.PassportId)" class="bp-report-action" aria-label="Detailed report" title="Detailed report" data-tooltip="Detailed report"></a>
                                </div>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </section>
</main>
```

- [ ] **Step 7: Update QR service and controller**

Change `PassportQrCodeService.BuildPayloadUrl`:

```csharp
public string BuildPayloadUrl(HttpRequest request, string batteryId)
{
    var escapedBatteryId = Uri.EscapeDataString(batteryId);
    return $"{request.Scheme}://{request.Host}/{escapedBatteryId}/latest";
}
```

Update `QrController` to accept `{batteryId}` and verify a battery exists. For access, allow QR if at least one latest public passport exists or the authenticated user can open battery detail.

- [ ] **Step 8: Run route tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatteryRouteAndRegistryTests`

Expected: PASS.

Commit:

```bash
git add web/Services/BatteryRouteResolutionService.cs web/Controllers/HomeController.cs web/Controllers/PassportController.cs web/Controllers/QrController.cs web/Services/PassportQrCodeService.cs web/Views/Passport/Battery.cshtml BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
git commit -m "feat: resolve public battery and passport routes"
```

---

### Task 7: Signed-In Registry And Anonymous Registry Blocking

**Files:**
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Views/Registry/Index.cshtml`
- Modify: `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`

- [ ] **Step 1: Add failing registry tests**

Append:

```csharp
[Fact]
public void Registry_ShouldUseBatteryRowsAndPassportCounts()
{
    var controller = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
    var view = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));

    Assert.Contains("BatteryRepository", controller);
    Assert.Contains("BatterySummaryViewModel", controller);
    Assert.Contains("PassportCount", view);
    Assert.Contains("passports", view);
    Assert.Contains("@row.BatteryId", view);
    Assert.Contains("@row.BatteryModel", view);
    Assert.DoesNotContain("@row.PassportId</span></td>", view);
}
```

- [ ] **Step 2: Run failing registry tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter Registry_ShouldUseBatteryRowsAndPassportCounts`

Expected: FAIL because registry still uses passport rows.

- [ ] **Step 3: Refactor RegistryController to batteries**

Inject `BatteryRepository`.

Change model to `IReadOnlyList<BatterySummaryViewModel>`:

```csharp
var batteryDocuments = await _batteryRepository.SearchDocumentsAsync(query, includeArchived: isAdmin, cancellationToken);
var rows = new List<BatterySummaryViewModel>();
foreach (var battery in batteryDocuments)
{
    var batteryId = BsonHelpers.GetString(battery, "batteryId");
    var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: isAdmin, cancellationToken);
    var visible = new List<BatteryPassportHistoryRowViewModel>();
    foreach (var passport in passports)
    {
        if (await _accessControlService.CanOpenPassportSummaryAsync(User, passport, _passportPublishPolicyService, cancellationToken))
        {
            visible.Add(ToHistoryRow(passport));
        }
    }

    if (visible.Count > 0 || isAdmin)
    {
        rows.Add(_batteryRepository.ToSummary(battery, visible, ResolveClusterLabel(battery)));
    }
}
return View(rows);
```

Add private helpers `ToHistoryRow` and `ResolveClusterLabel`.

- [ ] **Step 4: Refactor registry view**

Change model:

```cshtml
@model IReadOnlyList<BatteryPassWeb.Models.ViewModels.BatterySummaryViewModel>
```

Use columns:

```cshtml
<th>Battery ID</th>
<th>Battery Family</th>
<th>Battery Model</th>
<th>Battery serial number</th>
<th>Passports</th>
<th>Latest passport status</th>
<th>Updated</th>
<th class="bp-action-cell">Actions</th>
```

Use row actions:

```cshtml
<td><span class="bp-battery-id-cell">@row.BatteryId</span></td>
<td>@row.BatteryFamily</td>
<td>@row.BatteryModel</td>
<td>@row.BatterySerialNumber</td>
<td>@row.PassportCount passports</td>
<td>@row.LatestPassportStatus</td>
<td>@(string.IsNullOrWhiteSpace(row.UpdatedDate) ? "-" : row.UpdatedDate[..Math.Min(10, row.UpdatedDate.Length)])</td>
<td class="bp-action-cell">
    <div class="bp-icon-action-row">
        <a href="/@Uri.EscapeDataString(row.BatteryId)/latest" class="bp-report-action" aria-label="Detailed report" title="Detailed report" data-tooltip="Detailed report"></a>
        <a href="/@Uri.EscapeDataString(row.BatteryId)" class="bp-report-action" aria-label="Passport history" title="Passport history" data-tooltip="Passport history"></a>
    </div>
</td>
```

- [ ] **Step 5: Run registry tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatteryRouteAndRegistryTests`

Expected: PASS.

Commit:

```bash
git add web/Controllers/RegistryController.cs web/Views/Registry/Index.cshtml BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
git commit -m "feat: show battery rows in registry"
```

---

### Task 8: Admin Battery Management And Passport History

**Files:**
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Admin/EditPassport.cshtml`
- Modify: `web/Views/ClusterAdmin/Passports.cshtml`
- Modify: `web/Views/ClusterAdmin/EditPassport.cshtml`
- Modify: `web/wwwroot/css/site.css`
- Create/Modify: `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`

- [ ] **Step 1: Add failing admin workflow tests**

Create `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class BatteryAdminWorkflowTests
{
    [Fact]
    public void AdminController_ShouldExposeBatteryCreationAndPassportSnapshotActions()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("[HttpGet(\"batteries/new\")]", source);
        Assert.Contains("[HttpPost(\"batteries/create\")]", source);
        Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports/create\")]", source);
        Assert.Contains("CreateBattery", source);
        Assert.Contains("CreateBatteryPassport", source);
        Assert.Contains("CreatePassportSnapshotAsync", source);
        Assert.Contains("Battery Family and serial number are locked", source);
    }

    [Fact]
    public void AdminClustersView_ShouldManageBatteriesWithEmbeddedPassportHistory()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("Battery ID", view);
        Assert.Contains("Battery Model", view);
        Assert.Contains("Passport history", view);
        Assert.Contains("Create passport", view);
        Assert.Contains("data-battery-passport-history", view);
        Assert.DoesNotContain("Battery version", view);
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

- [ ] **Step 2: Run failing admin tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatteryAdminWorkflowTests`

Expected: FAIL because admin battery actions do not exist.

- [ ] **Step 3: Add admin battery routes**

Inject `BatteryRepository`, `BatteryIdService`, and `BatteryPassportSnapshotService` into `AdminController`.

Add:

```csharp
[HttpGet("batteries/new")]
public async Task<IActionResult> NewBattery([FromQuery] string? error, CancellationToken cancellationToken)
{
    var products = await _productTemplateService.ListProductsAsync(cancellationToken);
    return View("EditPassport", await BuildNewBatteryModelAsync(products, error, cancellationToken));
}

[HttpPost("batteries/create")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateBattery(CancellationToken cancellationToken)
{
    var form = Request.Form;
    var productId = Text(form, "productId", BatteryProductTemplateCatalog.DefaultProductId);
    var serialNumber = Text(form, "serialNumber");
    if (string.IsNullOrWhiteSpace(serialNumber))
    {
        return Redirect($"/admin/batteries/new?error={Uri.EscapeDataString("Battery serial number is required.")}");
    }

    var product = await _productTemplateService.GetProductAsync(productId, cancellationToken) ?? BatteryProductTemplateCatalog.DefaultProduct;
    var batteryId = _batteryIdService.CreateBatteryId(product.ProductName, serialNumber);
    if (await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken) != null)
    {
        return Redirect($"/admin/batteries/new?error={Uri.EscapeDataString("Battery already exists for this family and serial number.")}");
    }

    var now = DateTimeOffset.UtcNow.ToString("O");
    var battery = ProductTemplatePassportBuilder.BuildBatteryFromTemplate(
        batteryId,
        product,
        Text(form, "batteryModel", product.LatestProductVersion.Version),
        serialNumber,
        Text(form, "clusterId"),
        Text(form, "facilityId"),
        now);
    ApplyPassportForm(battery, form, now);
    battery["updatedAt"] = now;
    await _batteryRepository.CreateBatteryAsync(battery, cancellationToken);
    TempData["StatusMessage"] = $"Battery {batteryId} created. Create the first passport when the data is ready.";
    return Redirect("/admin/clusters?tab=batteries");
}

[HttpPost("batteries/{batteryId}/passports/create")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateBatteryPassport(string batteryId, CancellationToken cancellationToken)
{
    var battery = await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken);
    if (battery == null)
    {
        return NotFound();
    }

    var passport = await _batteryPassportSnapshotService.CreatePassportSnapshotAsync(
        battery,
        CurrentActor(),
        DateTimeOffset.UtcNow,
        cancellationToken);
    var passportId = BsonHelpers.GetString(passport, "passportId");
    TempData["StatusMessage"] = $"Passport {passportId} created for battery {batteryId}.";
    return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/edit");
}
```

Keep existing passport edit/conformance routes for the generated snapshots.

- [ ] **Step 4: Replace admin cluster tab rows**

In `AdminController.Index` or the method building `AdminClusterViewModel`, build battery rows when selected tab is `batteries` or `passports`. Preserve existing API token and products tabs.

Use:

```csharp
var batteryDocuments = needsPassports
    ? await _batteryRepository.SearchDocumentsAsync(q ?? string.Empty, includeArchived: true, cancellationToken)
    : [];
```

Map to `BatterySummaryViewModel` with passport history.

- [ ] **Step 5: Update `Admin/Clusters.cshtml`**

Add a `batteries` tab label. Replace the passport table content for that tab with battery rows and an embedded passport history table using `data-battery-passport-history`.

Each battery row includes a form:

```cshtml
<form method="post" action="/admin/batteries/@Uri.EscapeDataString(row.BatteryId)/passports/create">
    @Html.AntiForgeryToken()
    <button type="submit" class="bp-secondary-button">Create passport</button>
</form>
```

Each embedded passport row keeps:

```cshtml
<a href="/@Uri.EscapeDataString(passport.PassportId)/summary" class="bp-report-action" aria-label="Summary report" title="Summary report"></a>
<a href="/@Uri.EscapeDataString(passport.PassportId)" class="bp-report-action" aria-label="Detailed report" title="Detailed report"></a>
<a href="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/conformance" class="bp-icon-button" title="Conformance"></a>
<a href="/admin/passports/@Uri.EscapeDataString(passport.PassportId)/audit" class="bp-icon-button" title="Audit trail"></a>
```

- [ ] **Step 6: Update create/edit copy**

In `Admin/EditPassport.cshtml`, change create mode labels:

```cshtml
<h1 class="mb-1">@(isNew ? "Create battery" : "Edit passport snapshot")</h1>
```

For existing snapshots, render Battery Family and serial as readonly with help text:

```cshtml
<small class="bp-field-help">Battery Family and serial number are locked because they define the Battery ID.</small>
```

- [ ] **Step 7: Add CSS for embedded passport history**

In `web/wwwroot/css/site.css`:

```css
.bp-passport-history-row {
    background: color-mix(in srgb, var(--bp-surface) 92%, var(--bp-border));
}

.bp-history-marker {
    font-size: 0.78rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0;
}
```

- [ ] **Step 8: Run admin tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter BatteryAdminWorkflowTests`

Expected: PASS.

Commit:

```bash
git add web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs web/Views/Admin/Clusters.cshtml web/Views/Admin/EditPassport.cshtml web/Views/ClusterAdmin/Passports.cshtml web/Views/ClusterAdmin/EditPassport.cshtml web/wwwroot/css/site.css BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
git commit -m "feat: manage batteries in admin"
```

---

### Task 9: External API Battery ID Routes And Passport Trust Routes

**Files:**
- Modify: `web/Controllers/ExternalApiController.cs`
- Modify: `web/Services/ExternalApiRepository.cs`
- Modify: `web/Views/Help/Index.cshtml`
- Create/Modify: `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`

- [ ] **Step 1: Write failing external API tests**

Create `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`:

```csharp
namespace BatteryPassWeb.Tests;

public sealed class ExternalApiBatteryIdTests
{
    [Fact]
    public void ExternalApi_ShouldUseBatteryIdForBatteryOperations()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("[HttpGet(\"batteries/{batteryId}\")]", source);
        Assert.Contains("[HttpPost(\"batteries/{batteryId}/telemetry\")]", source);
        Assert.Contains("[HttpPatch(\"batteries/{batteryId}/battery-model\")]", source);
        Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports\")]", source);
        Assert.Contains("RejectPassportIdForBatteryRouteAsync", source);
        Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/battery-version\")]", source);
    }

    [Fact]
    public void ExternalApi_ShouldUsePassportIdForTrustOperationsAndPublish()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));

        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/sign\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/publish\")]", source);
        Assert.Contains("ValidateSignPublish", repository);
    }

    [Fact]
    public void ExternalApiHelp_ShouldDocumentBatteryIdAndBatteryModel()
    {
        var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("Battery ID", help);
        Assert.Contains("Battery Model", help);
        Assert.Contains("/battery-model", help);
        Assert.Contains("/passports/{passportId}/publish", help);
        Assert.DoesNotContain("Battery version", help);
        Assert.DoesNotContain("/battery-version", help);
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

- [ ] **Step 2: Run failing API tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter ExternalApiBatteryIdTests`

Expected: FAIL.

- [ ] **Step 3: Refactor API authorization**

In `ExternalApiController`, split authorization:

```csharp
private async Task<ExternalAuthResult> AuthorizeBatteryAsync(string batteryId, ExternalTokenRequirement requirement, CancellationToken cancellationToken)
{
    var tokenValidation = await ValidateTokenForRequirementAsync(requirement, cancellationToken);
    if (!tokenValidation.Success || tokenValidation.Context == null)
    {
        return new ExternalAuthResult { ErrorResult = Envelope(tokenValidation.StatusCode, tokenValidation.Message) };
    }

    var battery = await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken);
    if (battery == null)
    {
        if (await _passportRepository.GetByPassportIdAsync(batteryId, cancellationToken) != null)
        {
            return new ExternalAuthResult { ErrorResult = Envelope(StatusCodes.Status400BadRequest, "Telemetry and battery operations are keyed by Battery ID. Use the linked Battery ID, not Passport ID.") };
        }

        return new ExternalAuthResult { ErrorResult = Envelope(StatusCodes.Status404NotFound, "Battery was not found.") };
    }

    if (!CanAccessCluster(tokenValidation.Context, BsonHelpers.GetString(battery, "clusterId")))
    {
        return new ExternalAuthResult { ErrorResult = Envelope(StatusCodes.Status403Forbidden, "Token cannot access this battery cluster scope.") };
    }

    return new ExternalAuthResult { Battery = battery, TokenContext = tokenValidation.Context };
}
```

Extend `ExternalAuthResult` with:

```csharp
public BsonDocument? Battery { get; init; }
```

- [ ] **Step 4: Change battery routes**

Rename route parameters and methods:

```csharp
[HttpGet("batteries/{batteryId}")]
public async Task<IActionResult> GetBattery(string batteryId, CancellationToken cancellationToken)
```

For reads, find latest accessible passport:

```csharp
var latest = await _passportRepository.GetLatestPublicByBatteryIdAsync(
    batteryId,
    _passportPublishPolicyService,
    cancellationToken);
```

For authenticated tokens, if public latest is absent, use admin latest from `ListByBatteryIdAsync` if token scope allows the battery.

Change telemetry append:

```csharp
await _batteryTelemetryRepository.AppendTelemetryAsync(batteryId, points, cancellationToken);
```

Change battery model patch route:

```csharp
[HttpPatch("batteries/{batteryId}/battery-model")]
```

Update mutable battery source fields:

```csharp
await _batteryRepository.UpdateBatteryFieldsAsync(
    batteryId,
    new Dictionary<string, BsonValue>
    {
        ["identity.batteryModel"] = requestedBatteryModel,
        ["product.productVersion"] = requestedBatteryModel
    },
    cancellationToken);
```

Return:

```csharp
validationSigningRequired = true,
newPassportRequired = true
```

- [ ] **Step 5: Add API create-passport endpoint**

Add:

```csharp
[HttpPost("batteries/{batteryId}/passports")]
public async Task<IActionResult> CreateBatteryPassport(string batteryId, CancellationToken cancellationToken)
{
    var auth = await AuthorizeBatteryAsync(batteryId, ExternalTokenRequirement.Sign, cancellationToken);
    if (auth.ErrorResult != null)
    {
        return auth.ErrorResult;
    }

    var passport = await _batteryPassportSnapshotService.CreatePassportSnapshotAsync(
        auth.Battery!,
        auth.TokenContext!.Name,
        DateTimeOffset.UtcNow,
        cancellationToken);
    var passportId = BsonHelpers.GetString(passport, "passportId");
    return Envelope(StatusCodes.Status201Created, "Passport snapshot created.", new { batteryId, passportId });
}
```

- [ ] **Step 6: Move trust routes to passports**

Change:

```csharp
[HttpPost("passports/{passportId}/validate")]
[HttpPost("passports/{passportId}/sign")]
[HttpPost("passports/{passportId}/publish")]
```

Add publish method using existing admin publish workflow:

```csharp
var published = await _passportRepository.PublishPassportAsync(
    passportId,
    revisionId,
    publishedAt,
    verification.CurrentHash,
    publishedProof,
    cancellationToken);
```

- [ ] **Step 7: Rename token mode display**

In `ExternalApiRepository`, keep enum value if useful but expose display string:

```csharp
public const string ValidateSignPublish = "validateSignPublish";
```

Make Sign tokens validate for validate/sign/publish endpoints.

- [ ] **Step 8: Update API help**

Replace route samples in `Help/Index.cshtml`:

```text
GET /api/external/v1/batteries/{batteryId}
PATCH /api/external/v1/batteries/{batteryId}/battery-model
POST /api/external/v1/batteries/{batteryId}/passports
POST /api/external/v1/passports/{passportId}/validate
POST /api/external/v1/passports/{passportId}/sign
POST /api/external/v1/passports/{passportId}/publish
```

Replace “Battery version” text with “Battery Model”.

- [ ] **Step 9: Run API tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter ExternalApiBatteryIdTests`

Expected: PASS.

Commit:

```bash
git add web/Controllers/ExternalApiController.cs web/Services/ExternalApiRepository.cs web/Views/Help/Index.cshtml BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
git commit -m "feat: move external api to battery ids"
```

---

### Task 10: Passport Reports, Historical Indicators, And User-Facing Naming

**Files:**
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/Views/Admin/Product.cshtml`
- Modify: `docs/end-user-testing-guide.md`
- Modify: `docs/qa-test-pack.md`
- Modify: `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Modify: existing naming tests in `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`

- [ ] **Step 1: Add failing report naming tests**

Append to `BatteryRouteAndRegistryTests.cs`:

```csharp
[Fact]
public void PassportReports_ShouldShowBatteryIdPassportIdAndHistoricalLatestState()
{
    var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
    var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
    var combined = summary + Environment.NewLine + detail;

    Assert.Contains("Battery ID", combined);
    Assert.Contains("Passport ID", combined);
    Assert.Contains("Historical passport", combined);
    Assert.Contains("Latest passport", combined);
    Assert.Contains("Battery Model", combined);
    Assert.DoesNotContain("Battery version", combined);
}
```

- [ ] **Step 2: Run failing report tests**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter PassportReports_ShouldShowBatteryIdPassportIdAndHistoricalLatestState`

Expected: FAIL.

- [ ] **Step 3: Update summary/detail hero**

In both report views, add:

```cshtml
<p class="bp-passport-id">Passport ID: @passport.PassportId</p>
<p class="bp-passport-id">Battery ID: @passport.BatteryId</p>
@if (passport.IsHistoricalPassport)
{
    <span class="bp-status-pill is-draft">Historical passport</span>
    <a href="@passport.LatestPassportUrl" class="bp-secondary-button">Open latest passport</a>
}
else
{
    <span class="bp-status-pill is-published">Latest passport</span>
}
```

Replace labels:

```cshtml
<dt>Battery Model</dt><dd>@(string.IsNullOrWhiteSpace(passport.BatteryModel) ? "-" : passport.BatteryModel)</dd>
```

- [ ] **Step 4: Hide live telemetry on historical passports**

In `Detail.cshtml`, wrap telemetry section:

```cshtml
@if (passport.IsHistoricalPassport)
{
    <section class="bp-card">
        <h2>Telemetry</h2>
        <p class="text-secondary mb-0">Live telemetry is available from the latest passport for this battery.</p>
    </section>
}
else
{
    <!-- existing telemetry metrics and history -->
}
```

- [ ] **Step 5: Update Product and docs naming**

Replace user-facing “Battery version” with “Battery Model” in:

- `web/Views/Admin/Product.cshtml`
- `docs/end-user-testing-guide.md`
- `docs/qa-test-pack.md`

Keep internal code names where they are form field names until later refactor requires changing them.

- [ ] **Step 6: Run naming tests and commit**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "BatteryRouteAndRegistryTests|BatteryFamilyAccessApiRevisedTests"`

Expected: PASS after updating older tests to expect Battery Model.

Commit:

```bash
git add web/Views/Passport/Summary.cshtml web/Views/Passport/Detail.cshtml web/Views/Admin/Product.cshtml docs/end-user-testing-guide.md docs/qa-test-pack.md BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
git commit -m "feat: show battery ids and passport history state"
```

---

### Task 11: Full Build, Test Repair, And Documentation Polish

**Files:**
- Modify files indicated by failed tests.
- Modify: `README.md` only if it contains demonstrator route/API instructions that conflict with the new behavior.
- Modify: `RELEASE_NOTES.md`

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`

Expected: FAIL initially if older tests assert passport-centered text, routes, or API paths.

- [ ] **Step 2: Repair stale test expectations**

For each failed source-assertion test, update only expectations that conflict with the approved spec:

```csharp
Assert.Contains("Battery Model", text);
Assert.DoesNotContain("Battery version", text);
Assert.Contains("[HttpPost(\"passports/{passportId}/publish\")]", api);
Assert.Contains("batteryId", telemetryRepository);
```

Do not weaken tests by deleting behavior assertions. Replace old behavior with new behavior.

- [ ] **Step 3: Add release note**

Append to `RELEASE_NOTES.md`:

```markdown
## Battery ID and passport snapshots

- Added first-class Battery IDs and Passport IDs.
- Moved management, registry, QR, telemetry, and external battery API routes to Battery ID.
- Kept passports as immutable non-telemetry snapshots with historical/latest state.
- Renamed user-facing Battery version to Battery Model.
```

- [ ] **Step 4: Run full test suite again**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests web docs README.md RELEASE_NOTES.md
git commit -m "test: align suite with battery id passport redesign"
```

---

### Task 12: Manual Verification Checklist

**Files:**
- Modify only files needed to fix issues found during manual verification.

- [ ] **Step 1: Start app**

Run:

```bash
dotnet run --project web/BatteryPassWeb.csproj
```

Expected: app starts and initializes Mongo indexes without errors.

- [ ] **Step 2: Reset demo data**

Open the admin reset action already present in the app. Confirm the status message references seeded batteries and passports.

- [ ] **Step 3: Verify admin battery flow**

In the browser:

1. Sign in as global admin.
2. Open Administration.
3. Confirm battery rows show Battery ID, Battery Family, Battery Model, serial number, and passport count.
4. Create a new battery with Battery Family and serial number.
5. Confirm Battery ID preview is 70 Base64URL characters.
6. Create a passport for that battery.
7. Confirm the passport page shows both Battery ID and Passport ID.

- [ ] **Step 4: Verify public routes**

Use a seeded battery:

1. Open `/<batteryId>`.
2. Confirm linked passports are listed.
3. Open `/<batteryId>/latest`.
4. Confirm it redirects or renders the latest visible passport.
5. Open an older `/<passportId>`.
6. Confirm Historical passport is shown and live telemetry is hidden.

- [ ] **Step 5: Verify API routes**

Use a read/write/sign token and call:

```bash
curl -i -H "Authorization: Basic <token>" https://localhost:<port>/api/external/v1/batteries/<batteryId>
curl -i -H "Authorization: Basic <token>" -H "Content-Type: application/json" -d "{\"batteryModel\":\"2.0\"}" https://localhost:<port>/api/external/v1/batteries/<batteryId>/battery-model
curl -i -H "Authorization: Basic <token>" -X POST https://localhost:<port>/api/external/v1/batteries/<batteryId>/passports
curl -i -H "Authorization: Basic <token>" -X POST https://localhost:<port>/api/external/v1/passports/<passportId>/validate
curl -i -H "Authorization: Basic <token>" -X POST https://localhost:<port>/api/external/v1/passports/<passportId>/sign
curl -i -H "Authorization: Basic <token>" -X POST https://localhost:<port>/api/external/v1/passports/<passportId>/publish
```

Expected: battery read returns latest passport data, battery model write says a new passport is needed, create passport returns Passport ID, validate/sign/publish succeed when data is valid.

- [ ] **Step 6: Final full test run**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`

Expected: PASS.

- [ ] **Step 7: Commit manual verification fixes**

If any fixes were needed:

```bash
git add web BatteryPassWeb.Tests docs RELEASE_NOTES.md
git commit -m "fix: polish battery id passport workflow"
```

If no fixes were needed, no commit is required.

---

## Self-Review Notes

Spec coverage:

- ID generation: Task 1.
- Batteries collection and snapshot passports: Tasks 2 and 3.
- Reset/reseed: Task 4.
- Telemetry by Battery ID and latest/historical behavior: Task 5 and Task 10.
- Public routes/search/QR: Task 6.
- Signed-in registry and anonymous registry removal: Task 7.
- Admin battery management: Task 8.
- External API Battery ID and Passport ID split: Task 9.
- Battery Model naming: Task 10 and Task 11.
- Verification: Task 12.

Placeholder scan:

- The plan avoids placeholder markers and gives concrete tests, method names, routes, and commands for every task.

Type consistency:

- `BatteryId`, `BatteryModel`, `BatterySummaryViewModel`, `BatteryPassportHistoryRowViewModel`, `BatteryRepository`, `BatteryPassportSnapshotService`, and `BatteryRouteResolutionService` are introduced before later tasks use them.
