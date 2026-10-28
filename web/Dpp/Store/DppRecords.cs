using BatteryPassWeb.Dpp.Values;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Store;

// The records of design section 4.1 that a passport is built from. Values are keyed by v2.0.1 property name
// and kept in the schema's own form. Repositories, passport versions and documents come in step 2.

/// <summary>Groups battery models. Holds no passport data.</summary>
public sealed class BatteryFamily
{
    [BsonId]
    public required string Id { get; init; }

    public required string Name { get; set; }
}

/// <summary>The 32 model attributes, plus the starting values of the 5 re-stated ones (#59, #69, #74, #79, #81).</summary>
public sealed class BatteryModel
{
    [BsonId]
    public required string Id { get; init; }

    public required string FamilyId { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>Manufacturer identifier and information, and the manufacturing place (#12, #15, #16).</summary>
public sealed class ManufacturingSite
{
    [BsonId]
    public required string Id { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>One model × calendar year × manufacturing site (decision 3), holding the 16 batch attributes.</summary>
public sealed class ProductionBatch
{
    [BsonId]
    public required string Id { get; init; }

    public required string ModelId { get; init; }

    public required string SiteId { get; init; }

    public required int Year { get; init; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>An economic operator's identifier and information (#7, #14), entered once and linked from packs.</summary>
public sealed class EconomicOperator
{
    [BsonId]
    public required string Id { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>One battery pack, the subject of one passport (decision 16).</summary>
public sealed class BatteryPack
{
    [BsonId]
    public required string Id { get; init; }

    public required string BatchId { get; init; }

    public required string OperatorId { get; set; }

    /// <summary>The cluster the pack belongs to (the purchaser); null until assigned.</summary>
    public string? ClusterId { get; set; }

    /// <summary>Pack attributes, the live values and the pack's own re-stated values.</summary>
    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}
