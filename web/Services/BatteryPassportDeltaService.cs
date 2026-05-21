using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class BatteryPassportDeltaService
{
    private static readonly HashSet<string> OperationalFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "performance.stateOfCharge",
        "performance.remainingCapacity",
        "performance.remainingEnergy",
        "performance.fullCycles"
    };

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

    public async Task<bool> UpdateNewPassportRequiredAsync(
        BsonDocument battery,
        CancellationToken cancellationToken = default)
    {
        return await UpdateNewPassportRequiredCoreAsync(
            battery,
            compareAllPassportData: false,
            cancellationToken);
    }

    public async Task<bool> UpdateNewPassportRequiredForPassportDataAsync(
        BsonDocument battery,
        CancellationToken cancellationToken = default)
    {
        return await UpdateNewPassportRequiredCoreAsync(
            battery,
            compareAllPassportData: true,
            cancellationToken);
    }

    private async Task<bool> UpdateNewPassportRequiredCoreAsync(
        BsonDocument battery,
        bool compareAllPassportData,
        CancellationToken cancellationToken)
    {
        var batteryId = BsonHelpers.GetString(battery, "batteryId");
        var latestPassport = (await _passportRepository.ListByBatteryIdAsync(
            batteryId,
            includeArchived: false,
            cancellationToken)).FirstOrDefault();
        var policy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
        var required = latestPassport != null
            && !IsDraftStatus(latestPassport)
            && (compareAllPassportData
                ? HasPassportDataDifferences(battery, latestPassport)
                : HasEditableDifferences(battery, latestPassport, policy));
        var now = DateTimeOffset.UtcNow.ToString("O");
        var snapshot = EnsureDocument(EnsureDocument(battery, "app"), "snapshot");
        snapshot["newPassportRequired"] = required;
        snapshot["requiredSince"] = required ? now : BsonNull.Value;
        snapshot["reason"] = required
            ? compareAllPassportData
                ? "passport-data-differs-from-latest-passport"
                : "editable-fields-differ-from-latest-passport"
            : string.Empty;
        battery["updatedAt"] = now;

        await _batteryRepository.ReplaceAsync(batteryId, battery, cancellationToken);
        return required;
    }

    public async Task<bool> UpdateNewPassportRequiredByBatteryIdAsync(
        string batteryId,
        bool compareAllPassportData = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(batteryId))
        {
            return false;
        }

        var battery = await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken);
        return battery != null && await UpdateNewPassportRequiredCoreAsync(
            battery,
            compareAllPassportData,
            cancellationToken);
    }

    public async Task ClearNewPassportRequiredAsync(
        string batteryId,
        string latestPassportId,
        CancellationToken cancellationToken = default)
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

    public static bool HasEditableDifferences(
        BsonDocument battery,
        BsonDocument latestPassport,
        EditableFieldPolicySnapshot policy)
    {
        var comparableFieldKeys = policy.PermissionByKey.Values
            .Where(permission => permission.EditableAfterCreation)
            .Select(permission => permission.FieldKey)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return HasDifferencesForFieldKeys(battery, latestPassport, comparableFieldKeys);
    }

    public static bool HasPassportDataDifferences(
        BsonDocument battery,
        BsonDocument latestPassport)
    {
        return HasDifferencesForFieldKeys(
            battery,
            latestPassport,
            EditableFieldPolicyService.FieldPathsByKey.Keys);
    }

    private static bool HasDifferencesForFieldKeys(
        BsonDocument battery,
        BsonDocument latestPassport,
        IEnumerable<string> comparableFieldKeys)
    {
        var checkedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fieldKey in comparableFieldKeys)
        {
            if (OperationalFieldKeys.Contains(fieldKey))
            {
                continue;
            }

            if (!checkedKeys.Add(fieldKey)
                || !EditableFieldPolicyService.FieldPathsByKey.TryGetValue(fieldKey, out var paths))
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
        if (!document.Contains("snapshot"))
        {
            var batteryIdentityPath = path switch
            {
                "snapshot.batteryFamily" => "identity.batteryFamily",
                "snapshot.batteryModel" => "identity.batteryModel",
                "snapshot.batterySerialNumber" => "identity.serialNumber",
                _ => string.Empty
            };
            if (!string.IsNullOrWhiteSpace(batteryIdentityPath))
            {
                return ReadPath(document, batteryIdentityPath);
            }
        }

        BsonValue current = document;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            current = ReadSegment(current, segment);
            if (current.IsBsonNull)
            {
                return BsonNull.Value;
            }
        }

        return current;
    }

    private static BsonValue ReadSegment(BsonValue current, string segment)
    {
        if (current is not BsonDocument currentDocument)
        {
            return BsonNull.Value;
        }

        var bracketStart = segment.IndexOf('[', StringComparison.Ordinal);
        var bracketEnd = segment.EndsWith(']') ? segment.Length - 1 : -1;
        if (bracketStart > 0 && bracketEnd > bracketStart)
        {
            var arrayName = segment[..bracketStart];
            var selector = segment[(bracketStart + 1)..bracketEnd];
            return arrayName.Equals("batteryMaterials", StringComparison.OrdinalIgnoreCase)
                ? ReadMaterialMass(currentDocument, selector, returnRow: true)
                : ReadArrayItem(currentDocument, arrayName, selector);
        }

        return currentDocument.TryGetValue(segment, out var next)
            ? next
            : BsonNull.Value;
    }

    private static BsonValue ReadMaterialMass(BsonDocument document, string material, bool returnRow = false)
    {
        var row = ReadArrayItem(document, "batteryMaterials", material);
        if (returnRow)
        {
            return row;
        }

        return row is BsonDocument materialDocument && materialDocument.TryGetValue("batteryMaterialMass", out var mass)
            ? mass
            : BsonNull.Value;
    }

    private static BsonValue ReadArrayItem(BsonDocument document, string arrayName, string selector)
    {
        var labelField = arrayName switch
        {
            "batteryMaterials" => "batteryMaterialName",
            "recycledContent" => "recycledMaterial",
            "carbonFootprintPerLifecycleStage" => "lifecycleStage",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(labelField)
            || !document.TryGetValue(arrayName, out var arrayValue)
            || arrayValue is not BsonArray rows)
        {
            return BsonNull.Value;
        }

        var row = rows
            .OfType<BsonDocument>()
            .FirstOrDefault(row => BsonHelpers.GetString(row, labelField).Equals(selector, StringComparison.OrdinalIgnoreCase));
        return row ?? (BsonValue)BsonNull.Value;
    }

    private static bool BsonValueEquals(BsonValue left, BsonValue right) =>
        NumericBsonEquals(left, right)
        || BsonText(left).Equals(BsonText(right), StringComparison.OrdinalIgnoreCase);

    private static bool NumericBsonEquals(BsonValue left, BsonValue right)
    {
        if (!TryBsonDouble(left, out var leftNumber) || !TryBsonDouble(right, out var rightNumber))
        {
            return false;
        }

        return Math.Abs(leftNumber - rightNumber) < 0.000001;
    }

    private static bool TryBsonDouble(BsonValue value, out double number)
    {
        number = 0;
        if (value == null || value.IsBsonNull)
        {
            return false;
        }

        if (value.IsInt32 || value.IsInt64 || value.IsDouble || value.IsDecimal128)
        {
            number = value.ToDouble();
            return true;
        }

        return double.TryParse(
            BsonText(value),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out number);
    }

    private static bool IsDraftStatus(BsonDocument passport)
    {
        var registryStatus = BsonHelpers.GetString(passport, "registryInfo", "status");
        var trustStatus = BsonHelpers.GetString(passport, "trust", "status");
        return registryStatus.Contains("draft", StringComparison.OrdinalIgnoreCase)
            || registryStatus.Contains("awaiting", StringComparison.OrdinalIgnoreCase)
            || trustStatus.Contains("draft", StringComparison.OrdinalIgnoreCase)
            || trustStatus.Contains("awaiting", StringComparison.OrdinalIgnoreCase);
    }

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
