using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Values;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Store;

/// <summary>DPP status values (#2), the schema's five.</summary>
public static class DppStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";

    /// <summary>Replaced by a linked new passport (design 7.5, step 4c-1).</summary>
    public const string Inactive = "inactive";

    /// <summary>Marked invalid by the team or market surveillance (design 7.7, step 4c-2).</summary>
    public const string Invalid = "invalid";

    /// <summary>After recycling or export (design 7.6, step 4c-2).</summary>
    public const string MarkedForDeletion = "marked-for-deletion";

    /// <summary>A passport no longer active (step 4c): it takes no changes and gets no new version.</summary>
    public static bool IsNoLongerActive(string? status) => status is Inactive or Invalid or MarkedForDeletion;

    /// <summary>Frozen by a mark (step 4c-2): still the battery's newest passport, unlike an inactive one (redesign R6b).</summary>
    public static bool IsMarked(string? status) => status is Invalid or MarkedForDeletion;

    /// <summary>A DPP status in words, for pills and tables: "Active", "Marked for deletion".</summary>
    public static string Words(string status) => status switch
    {
        Active => "Active",
        Archived => "Archived",
        Inactive => "Inactive",
        Invalid => "Invalid",
        MarkedForDeletion => "Marked for deletion",
        _ => status,
    };
}

/// <summary>
/// One published version of a pack's passport (design 4.2): its passport ID (#5), its DPP status (#2), when it was
/// published and last made available (#4), and the signed static core. Found by its #5 forever.
/// </summary>
public sealed class PassportVersion : IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string PackId { get; init; }

    /// <summary>1 for the first publication, then +1 per version.</summary>
    public required int Number { get; init; }

    public required string DigitalProductPassportId { get; init; }

    public required string DppStatus { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime PublishedAtUtc { get; init; }

    /// <summary>#4: publication time, then the time of each live update while the version is active.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime LastUpdatedUtc { get; init; }

    public required string PublishedBy { get; init; }

    [BsonSerializer(typeof(JsonNodeBsonSerializer))]
    public required JsonNode StaticCore { get; init; }

    public required SignatureProof Proof { get; init; }
}
