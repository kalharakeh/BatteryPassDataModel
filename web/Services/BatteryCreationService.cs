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
