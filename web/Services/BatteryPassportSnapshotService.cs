using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class BatteryPassportSnapshotService
{
    private readonly BatteryIdService _batteryIdService;
    private readonly PassportRepository _passportRepository;
    private readonly AuditRevisionService _auditRevisionService;
    private readonly BatteryAuditService _batteryAuditService;

    public BatteryPassportSnapshotService(
        BatteryIdService batteryIdService,
        PassportRepository passportRepository,
        AuditRevisionService auditRevisionService,
        BatteryAuditService batteryAuditService)
    {
        _batteryIdService = batteryIdService;
        _passportRepository = passportRepository;
        _auditRevisionService = auditRevisionService;
        _batteryAuditService = batteryAuditService;
    }

    public Task<BsonDocument> CreatePassportSnapshotAsync(
        BsonDocument battery,
        string actor,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        return CreatePassportSnapshotAsync(
            battery,
            actor,
            createdAt,
            "admin",
            "admin-ui",
            string.Empty,
            cancellationToken);
    }

    public async Task<BsonDocument> CreatePassportSnapshotAsync(
        BsonDocument battery,
        string actor,
        DateTimeOffset createdAt,
        string actorType,
        string source,
        string actorTokenId = "",
        CancellationToken cancellationToken = default)
    {
        // Passport snapshots are immutable for non-telemetry data after this point.
        // Later battery edits must create another passport instead of mutating this document.
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
        passport["supersededAt"] = BsonNull.Value;
        passport["supersededByPassportId"] = BsonNull.Value;
        passport["batteryId"] = batteryId;
        passport["passportId"] = passportId;
        passport.Remove("_id");

        await _passportRepository.ReplaceAsync(passportId, passport, cancellationToken);
        var metadata = new BsonDocument
        {
            ["batteryId"] = batteryId,
            ["passportId"] = passportId,
            ["batteryFamily"] = BsonHelpers.GetString(battery, "identity", "batteryFamily"),
            ["batteryModel"] = batteryModel,
            ["serialNumber"] = BsonHelpers.GetString(battery, "identity", "serialNumber"),
            ["clusterId"] = BsonHelpers.GetString(battery, "clusterId")
        };
        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.created",
            actor,
            actorType,
            source,
            "Passport snapshot created from battery record.",
            metadata,
            cancellationToken);
        await _batteryAuditService.AppendBatteryAuditEventAsync(
            batteryId,
            "battery.passport.created",
            actor,
            actorType,
            source,
            "Passport snapshot created for battery.",
            metadata,
            actorTokenId,
            cancellationToken);
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

        ApplyPassportSpecificFields(passport, passportId);
        return passport;
    }

    private static void ApplyPassportSpecificFields(BsonDocument passport, string passportId)
    {
        var generalPayload = EnsureDocument(EnsureDocument(EnsureDocument(passport, "aspects"), "generalProductInformation"), "payload");
        generalPayload["batteryPassportIdentifier"] = BatteryPassCanonicalDataCatalog.NormalizeBatteryPassportIdentifier(
            BsonHelpers.GetString(generalPayload, "batteryPassportIdentifier"),
            BsonHelpers.GetString(passport, "app", "display", "serialNumber"),
            passportId);
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
