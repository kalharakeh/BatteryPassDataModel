using BatteryPassWeb.Dpp.Values;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Store;

// The records of design section 4.1 that a passport is built from. Values are keyed by v2.0.1 property name
// and kept in the schema's own form. Passport versions and documents come in plan 2b and 2c.

/// <summary>A stored record; <see cref="Id"/> is its MongoDB _id.</summary>
public interface IDppRecord
{
    string Id { get; }
}

/// <summary>Groups battery models. Holds no passport data.</summary>
public sealed class BatteryFamily : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
    public bool IsDemo { get; set; }

    public required string Name { get; set; }

    /// <summary>The family's picture (decision 20): a file in the dpp_files bucket, shown on the public summary. Not passport data.</summary>
    public string? PictureFileId { get; set; }

    public string? PictureContentType { get; set; }
}

/// <summary>The 32 model attributes, plus the starting values of the 5 re-stated ones (#59, #69, #74, #79, #81).</summary>
public sealed class BatteryModel : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
    public bool IsDemo { get; set; }

    public required string FamilyId { get; init; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);

    /// <summary>What the model's modules are, in a few words (decision 31). App data, not passport data; null when not typed.</summary>
    public string? ModuleDescription { get; set; }
}

/// <summary>Manufacturer identifier and information, and the manufacturing place (#12, #15, #16).</summary>
public sealed class ManufacturingSite : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
    public bool IsDemo { get; set; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>One model × calendar year × manufacturing site (decision 3), holding the 16 batch attributes.</summary>
public sealed class ProductionBatch : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
    public bool IsDemo { get; set; }

    public required string ModelId { get; init; }

    public required string SiteId { get; init; }

    public required int Year { get; init; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>An economic operator's identifier and information (#7, #14), entered once and linked from packs.</summary>
public sealed class EconomicOperator : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
    public bool IsDemo { get; set; }

    public required string Name { get; set; }

    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>One battery pack, the subject of one passport (decision 16).</summary>
public sealed class BatteryPack : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
    public bool IsDemo { get; set; }

    public required string BatchId { get; init; }

    public required string OperatorId { get; set; }

    /// <summary>The cluster the pack belongs to (the purchaser); null until assigned.</summary>
    public string? ClusterId { get; set; }

    /// <summary>Pack attributes, the live values and the pack's own re-stated values.</summary>
    public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);

    /// <summary>The pack's modules, by position (decision 31). App data, not passport data; empty until listed.</summary>
    public List<BatteryModule> Modules { get; set; } = [];

    /// <summary>
    /// The pack's repair history (design 7.9, decision 19), oldest first. App data, not passport data: it isn't in the
    /// passport JSON and is shown with the battery status to the same readers. Entries are never changed or removed
    /// (decision 34).
    /// </summary>
    public List<RepairEntry> Repairs { get; set; } = [];

    /// <summary>
    /// Placed on the market again (design 7.5, step 4c-1): what the status was, and the battery's new passport, Scania's
    /// (a new pack) or another operator's. Null while the battery isn't waiting for a new passport. App data.
    /// </summary>
    public Succession? Succession { get; set; }

    /// <summary>The pack whose passport this one replaces (design 7.5); null for a battery placed on the market new.</summary>
    public string? SuccessorOf { get; set; }

    /// <summary>Why the passport is invalid or marked for deletion, by whom and when (step 4c-2); null otherwise.</summary>
    public PassportMark? Mark { get; set; }
}

/// <summary>An invalid or marked-for-deletion passport's mark (design 7.6 and 7.7): its reason is seen by the team and market surveillance only.</summary>
public sealed class PassportMark
{
    /// <summary>invalid or marked-for-deletion.</summary>
    public required string Status { get; init; }

    public string? Reason { get; init; }

    public required string By { get; init; }

    /// <summary>The sender's kind: Team or MarketSurveillance.</summary>
    public required string ByRole { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime AtUtc { get; init; }
}

/// <summary>A battery placed on the market again, and its new passport (design 7.5).</summary>
public sealed class Succession
{
    /// <summary>The status before (to put back if the team cancels, decision 34).</summary>
    public required string StatusBefore { get; init; }

    /// <summary>The status it was placed on the market again with: re-used, remanufactured or repurposed.</summary>
    public required string Status { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime StartedAtUtc { get; init; }

    /// <summary>Scania's new pack, once the team made it.</summary>
    public string? SuccessorPackId { get; set; }

    /// <summary>Another operator's identifier (#7), as typed.</summary>
    public string? OtherOperatorId { get; set; }

    /// <summary>Another operator's name and contact (#14), as typed.</summary>
    public string? OtherOperatorInfo { get; set; }

    /// <summary>Another operator's new passport: its ID or web address.</summary>
    public string? OtherPassport { get; set; }

    /// <summary>When the new passport was published or linked, which made this one inactive.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LinkedAtUtc { get; set; }
}

/// <summary>The states of a hand-over (design 7.8).</summary>
public static class HandOverStates
{
    public const string Waiting = "waiting";
    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// A battery's hand-over from one cluster to another (design 4.1 and 7.8, step 4d): asked by the sending cluster's admin,
/// answered by the receiving cluster's, or cancelled. App data, not passport data.
/// </summary>
public sealed class HandOver : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string PackId { get; init; }

    public required string FromClusterId { get; init; }

    public required string ToClusterId { get; init; }

    /// <summary>One of <see cref="HandOverStates"/>.</summary>
    public required string State { get; set; }

    public required string AskedBy { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime AskedAtUtc { get; init; }

    /// <summary>Who accepted, declined or cancelled it.</summary>
    public string? AnsweredBy { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? AnsweredAtUtc { get; set; }
}

/// <summary>One repair of a pack (design 7.9): when, what was repaired, and who recorded it when.</summary>
public sealed class RepairEntry
{
    /// <summary>The day of the repair, as yyyy-MM-dd.</summary>
    public required string Date { get; init; }

    public required string Description { get; init; }

    public required string RecordedBy { get; init; }

    /// <summary>The team or the battery's cluster (a <c>SenderKind</c>): the detail page says which, not who.</summary>
    public string RecordedAs { get; init; } = "Team";

    /// <summary>A note that corrects the history (decision 34): entries are never changed, so a mistake gets one of these.</summary>
    public bool IsCorrection { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime RecordedAtUtc { get; init; }
}

/// <summary>One module of a pack, as the battery system's export lists it: its position and its IDs (decision 31).</summary>
public sealed class BatteryModule
{
    public int Position { get; init; }

    public string? SerialNumber { get; init; }

    public string? ConnectivityId { get; init; }
}
