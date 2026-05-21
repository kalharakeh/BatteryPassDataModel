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

        ProductTemplatePassportBuilder.ApplyProductVersionToBattery(
            battery,
            product,
            selectedVersion,
            DateTimeOffset.UtcNow.ToString("O"));
        await _batteryPassportDeltaService.UpdateNewPassportRequiredForPassportDataAsync(battery, cancellationToken);
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
            version.Version.Equals(batteryModel, StringComparison.OrdinalIgnoreCase));
        if (product == null || selectedVersion == null)
        {
            return new BatteryTemplateUpdateResult(false, "Unknown Software Version.", battery);
        }

        var selectedSoftwareVersion = selectedVersion.SoftwareVersions.FirstOrDefault(version =>
            version.SoftwareVersion.Equals(requestedSoftwareVersion.Trim(), StringComparison.OrdinalIgnoreCase));
        if (selectedSoftwareVersion == null)
        {
            return new BatteryTemplateUpdateResult(false, "Unknown Software Version.", battery);
        }

        var setValues = new Dictionary<string, BsonValue>
        {
            ["app.product.softwareVersion"] = selectedSoftwareVersion.SoftwareVersion,
            ["app.product.softwareReleaseDate"] = selectedSoftwareVersion.SoftwareReleaseDate,
            ["app.product.softwareLatestUpdate"] = selectedSoftwareVersion.SoftwareLatestUpdate,
            ["identity.softwareVersion"] = selectedSoftwareVersion.SoftwareVersion,
            ["updatedAt"] = DateTimeOffset.UtcNow.ToString("O")
        };
        foreach (var pair in setValues)
        {
            SetPath(battery, pair.Key, pair.Value);
        }

        await _batteryPassportDeltaService.UpdateNewPassportRequiredForPassportDataAsync(battery, cancellationToken);
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
