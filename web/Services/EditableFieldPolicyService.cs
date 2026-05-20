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

    public int EditableAtCreationCount => PermissionByKey.Values.Count(permission => permission.EditableAtCreation);
    public int EditableAfterCreationCount => PermissionByKey.Values.Count(permission => permission.EditableAfterCreation);
    public int EditableByLocalAdminCount => PermissionByKey.Values.Count(permission => permission.EditableByLocalAdmin);

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

    private static readonly string[] DefaultEditableAtCreationFieldKeys =
    [
        "general.clusterId",
        "general.product",
        "general.batteryFamily",
        "general.productVersion",
        "general.batteryModel",
        "general.serialNumber",
        "general.manufacturingDate",
        "general.manufacturedDate",
        "general.facilityId",
        "general.manufacturerName",
        "general.manufacturedBy",
        "general.softwareVersion",
        "software.version"
    ];

    private static readonly string[] DefaultEditableAfterCreationFieldKeys =
    [
        "general.clusterId",
        "general.productVersion",
        "general.batteryModel",
        "general.facilityId",
        "general.softwareVersion",
        "software.version"
    ];

    public static readonly IReadOnlyDictionary<string, string[]> FieldPathsByKey =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["general.clusterId"] = ["clusterId"],
            ["general.product"] = ["identity.batteryFamily", "app.product.productName"],
            ["general.batteryFamily"] = ["identity.batteryFamily", "app.product.productName"],
            ["general.productVersion"] = ["identity.batteryModel", "app.product.productVersion", "app.product.batteryModel"],
            ["general.batteryModel"] = ["identity.batteryModel", "app.product.productVersion", "app.product.batteryModel"],
            ["general.serialNumber"] = ["identity.serialNumber", "app.display.serialNumber"],
            ["general.manufacturingDate"] = ["aspects.generalProductInformation.payload.manufacturingDate"],
            ["general.manufacturedDate"] = ["aspects.generalProductInformation.payload.manufacturingDate"],
            ["general.facilityId"] = ["app.display.facilityId"],
            ["general.manufacturerName"] = ["app.display.manufacturerName"],
            ["general.manufacturedBy"] = ["app.display.manufacturerName"],
            ["general.softwareVersion"] = ["app.product.softwareVersion"],
            ["software.version"] = ["app.product.softwareVersion"]
        };

    private static readonly IReadOnlyDictionary<string, string> AliasByFieldKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["general.batteryFamily"] = "general.product",
            ["general.batteryModel"] = "general.productVersion",
            ["general.manufacturedDate"] = "general.manufacturingDate",
            ["general.manufacturedBy"] = "general.manufacturerName",
            ["software.version"] = "general.softwareVersion"
        };

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
        var sections = BuildEditableSections();
        var knownKeys = BuildKnownKeys(sections);
        var defaultCreation = ExpandAliases(DefaultEditableAtCreationFieldKeys);
        var defaultAfterCreation = ExpandAliases(DefaultEditableAfterCreationFieldKeys);
        var defaultLocalAdmin = ExpandAliases(DefaultEditableAfterCreationFieldKeys);
        var requested = permissions?
            .GroupBy(permission => permission.FieldKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, EditableFieldPermission>(StringComparer.OrdinalIgnoreCase);
        var permissionByKey = new Dictionary<string, EditableFieldPermission>(StringComparer.OrdinalIgnoreCase);

        foreach (var fieldKey in knownKeys)
        {
            var basePermission = new EditableFieldPermission(
                fieldKey,
                defaultCreation.Contains(fieldKey),
                defaultAfterCreation.Contains(fieldKey),
                defaultLocalAdmin.Contains(fieldKey));
            if (requested.TryGetValue(fieldKey, out var requestedPermission))
            {
                basePermission = requestedPermission with { FieldKey = fieldKey };
            }

            permissionByKey[fieldKey] = EnforceDependencies(basePermission);
        }

        foreach (var alias in AliasByFieldKey)
        {
            if (!permissionByKey.TryGetValue(alias.Value, out var canonicalPermission))
            {
                continue;
            }

            if (requested.TryGetValue(alias.Key, out var requestedAliasPermission))
            {
                canonicalPermission = EnforceDependencies(requestedAliasPermission with { FieldKey = alias.Key });
            }

            permissionByKey[alias.Key] = canonicalPermission with { FieldKey = alias.Key };
        }

        return new EditableFieldPolicySnapshot
        {
            PolicyKey = PolicyKey,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy,
            Sections = sections,
            PermissionByKey = permissionByKey
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

        var policy = CreateDefaultPolicy(
            permissions,
            DateTimeOffset.UtcNow.ToString("O"),
            string.IsNullOrWhiteSpace(actor) ? "system" : actor);
        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("policyKey", PolicyKey),
            ToBsonDocument(policy),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public static BsonDocument ToBsonDocument(EditableFieldPolicySnapshot policy)
    {
        var sectionsFieldKeys = policy.Sections
            .SelectMany(section => section.Fields)
            .Select(field => field.FieldKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new BsonDocument
        {
            ["policyKey"] = PolicyKey,
            ["permissions"] = new BsonArray(policy.PermissionByKey.Values
                .Where(permission => sectionsFieldKeys.Contains(permission.FieldKey))
                .OrderBy(permission => permission.FieldKey, StringComparer.OrdinalIgnoreCase)
                .Select(permission => new BsonDocument
                {
                    ["fieldKey"] = permission.FieldKey,
                    ["editableAtCreation"] = permission.EditableAtCreation,
                    ["editableAfterCreation"] = permission.EditableAfterCreation,
                    ["editableByLocalAdmin"] = permission.EditableByLocalAdmin
                })),
            ["updatedAt"] = policy.UpdatedAt,
            ["updatedBy"] = policy.UpdatedBy
        };
    }

    public static EditableFieldPolicySnapshot FromBsonDocument(BsonDocument document)
    {
        var permissions = (document.GetValue("permissions", new BsonArray()) as BsonArray ?? new BsonArray())
            .OfType<BsonDocument>()
            .Select(permission => new EditableFieldPermission(
                BsonHelpers.GetString(permission, "fieldKey"),
                permission.GetValue("editableAtCreation", false).ToBoolean(),
                permission.GetValue("editableAfterCreation", false).ToBoolean(),
                permission.GetValue("editableByLocalAdmin", false).ToBoolean()))
            .Where(permission => !string.IsNullOrWhiteSpace(permission.FieldKey))
            .ToList();

        return CreateDefaultPolicy(
            permissions,
            BsonHelpers.GetString(document, "updatedAt"),
            BsonHelpers.GetString(document, "updatedBy"));
    }

    private static IReadOnlyList<DataRequirementSection> BuildEditableSections()
    {
        var metadata = DataCompletionPolicyService.CreateDefaultPolicy();
        return metadata.Sections.Select(section =>
        {
            var fields = section.Fields.Select(field => new DataRequirementField
            {
                FieldKey = field.FieldKey,
                SectionKey = field.SectionKey,
                Label = field.Label,
                DataPath = field.DataPath,
                Guidance = field.Guidance,
                DefaultRequired = field.DefaultRequired,
                IsRequired = field.IsRequired,
                SortOrder = field.SortOrder
            }).ToList();

            if (section.SectionKey.Equals("general", StringComparison.OrdinalIgnoreCase)
                && fields.All(field => !field.FieldKey.Equals("general.clusterId", StringComparison.OrdinalIgnoreCase)))
            {
                fields.Insert(0, new DataRequirementField
                {
                    FieldKey = "general.clusterId",
                    SectionKey = "general",
                    Label = "Cluster",
                    DataPath = "clusterId",
                    Guidance = "Cluster assignment for this battery.",
                    DefaultRequired = true,
                    IsRequired = true,
                    SortOrder = 5
                });
            }

            return new DataRequirementSection
            {
                SectionKey = section.SectionKey,
                Label = section.Label,
                SortOrder = section.SortOrder,
                Fields = fields
            };
        }).ToList();
    }

    private static HashSet<string> BuildKnownKeys(IReadOnlyList<DataRequirementSection> sections)
    {
        var knownKeys = sections
            .SelectMany(section => section.Fields)
            .Select(field => field.FieldKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in AliasByFieldKey.Keys)
        {
            knownKeys.Add(alias);
        }

        return knownKeys;
    }

    private static HashSet<string> ExpandAliases(IEnumerable<string> fieldKeys)
    {
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fieldKey in fieldKeys)
        {
            expanded.Add(fieldKey);
            if (AliasByFieldKey.TryGetValue(fieldKey, out var canonicalKey))
            {
                expanded.Add(canonicalKey);
            }
        }

        return expanded;
    }

    private static EditableFieldPermission EnforceDependencies(EditableFieldPermission permission)
    {
        var editableAtCreation = permission.EditableAtCreation
            || permission.EditableAfterCreation
            || permission.EditableByLocalAdmin;
        var editableAfterCreation = permission.EditableAfterCreation || permission.EditableByLocalAdmin;
        return permission with
        {
            EditableAtCreation = editableAtCreation,
            EditableAfterCreation = editableAfterCreation
        };
    }

    private IMongoCollection<BsonDocument>? GetCollection() =>
        _mongoContext.Database?.GetCollection<BsonDocument>("editableFieldPolicies");
}
