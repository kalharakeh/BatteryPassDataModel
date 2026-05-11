using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

public sealed class LocalAdminEditableFieldPolicySnapshot
{
    public string PolicyKey { get; init; } = LocalAdminEditableFieldPolicyService.PolicyKey;
    public string UpdatedAt { get; init; } = string.Empty;
    public string UpdatedBy { get; init; } = string.Empty;
    public IReadOnlyList<DataRequirementSection> Sections { get; init; } = [];
    public IReadOnlyList<string> EditableFieldKeys { get; init; } = [];

    public bool IsEditable(string fieldKey) =>
        EditableFieldKeys.Contains(fieldKey, StringComparer.OrdinalIgnoreCase);
}

public sealed class LocalAdminEditableFieldPolicyService
{
    public const string PolicyKey = "localAdminEditableFields:v1";

    private static readonly string[] DefaultEditableFieldKeys =
    [
        "general.facilityId",
        "general.batteryImageUrl",
        "performance.stateOfCharge",
        "performance.remainingCapacity",
        "performance.remainingEnergy",
        "performance.fullCycles"
    ];

    private readonly MongoContext _mongoContext;

    public LocalAdminEditableFieldPolicyService(MongoContext mongoContext)
    {
        _mongoContext = mongoContext;
    }

    public static LocalAdminEditableFieldPolicySnapshot CreateDefaultPolicy(
        IReadOnlyCollection<string>? editableFieldKeys = null,
        string updatedAt = "",
        string updatedBy = "system")
    {
        var metadata = DataCompletionPolicyService.CreateDefaultPolicy();
        var knownKeys = metadata.Sections
            .SelectMany(section => section.Fields)
            .Select(field => field.FieldKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedKeys = (editableFieldKeys ?? DefaultEditableFieldKeys)
            .Where(key => knownKeys.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LocalAdminEditableFieldPolicySnapshot
        {
            PolicyKey = PolicyKey,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy,
            Sections = metadata.Sections,
            EditableFieldKeys = selectedKeys
        };
    }

    public async Task<LocalAdminEditableFieldPolicySnapshot> GetPolicyAsync(CancellationToken cancellationToken = default)
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
            await SavePolicyAsync(defaultPolicy.EditableFieldKeys, "system", cancellationToken);
            return defaultPolicy;
        }

        return FromBsonDocument(document);
    }

    public async Task SavePolicyAsync(
        IReadOnlyCollection<string> editableFieldKeys,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();
        if (collection == null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        var policy = CreateDefaultPolicy(editableFieldKeys, now, string.IsNullOrWhiteSpace(actor) ? "system" : actor);
        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("policyKey", PolicyKey),
            ToBsonDocument(policy),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public static BsonDocument ToBsonDocument(LocalAdminEditableFieldPolicySnapshot policy)
    {
        return new BsonDocument
        {
            ["policyKey"] = PolicyKey,
            ["editableFieldKeys"] = new BsonArray(policy.EditableFieldKeys),
            ["updatedAt"] = policy.UpdatedAt,
            ["updatedBy"] = policy.UpdatedBy
        };
    }

    public static LocalAdminEditableFieldPolicySnapshot FromBsonDocument(BsonDocument document)
    {
        var editableFieldKeys = (document.GetValue("editableFieldKeys", new BsonArray()) as BsonArray ?? new BsonArray())
            .Select(value => value.ToString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return CreateDefaultPolicy(
            editableFieldKeys,
            BsonHelpers.GetString(document, "updatedAt"),
            BsonHelpers.GetString(document, "updatedBy"));
    }

    private IMongoCollection<BsonDocument>? GetCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("localAdminEditableFieldPolicies");
}
