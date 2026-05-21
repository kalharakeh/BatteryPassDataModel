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
        var batteryId = BsonHelpers.GetString(battery, "batteryId");
        var latestPassport = (await _passportRepository.ListByBatteryIdAsync(
            batteryId,
            includeArchived: false,
            cancellationToken)).FirstOrDefault();
        var policy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
        var required = latestPassport != null && HasEditableDifferences(battery, latestPassport, policy);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var snapshot = EnsureDocument(EnsureDocument(battery, "app"), "snapshot");
        snapshot["newPassportRequired"] = required;
        snapshot["requiredSince"] = required ? now : BsonNull.Value;
        snapshot["reason"] = required ? "editable-fields-differ-from-latest-passport" : string.Empty;
        battery["updatedAt"] = now;

        await _batteryRepository.ReplaceAsync(batteryId, battery, cancellationToken);
        return required;
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
        var checkedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var permission in policy.PermissionByKey.Values.Where(permission => permission.EditableAfterCreation))
        {
            if (OperationalFieldKeys.Contains(permission.FieldKey))
            {
                continue;
            }

            if (!checkedKeys.Add(permission.FieldKey)
                || !EditableFieldPolicyService.FieldPathsByKey.TryGetValue(permission.FieldKey, out var paths))
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
