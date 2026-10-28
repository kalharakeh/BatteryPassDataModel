using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Values;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Store;

/// <summary>DPP status values (#2) that versioning sets; step 4 adds inactive, invalid and marked-for-deletion.</summary>
public static class DppStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";
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
