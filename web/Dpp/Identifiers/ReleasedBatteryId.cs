using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Identifiers;

/// <summary>
/// A battery ID the team released in advance (step 6d, the owner, 6 Nov 2026), so its label can be printed before the
/// battery is made. Its <see cref="Id"/> is the serial number's letters and digits ("VPC12221"), so the same serial
/// number can't be released twice, in any spelling. A battery is created by picking an available one.
/// </summary>
public sealed class ReleasedBatteryId : Store.IDppRecord
{
    [BsonId]
    public required string Id { get; init; }

    /// <summary>The serial number (#11) as it was released ("VPC-12221"); the battery made from it gets it.</summary>
    public required string SerialNumber { get; init; }

    /// <summary>The battery ID (#6), fixed at release.</summary>
    public required string Identifier { get; init; }

    /// <summary>The release it came in; "void the unused IDs of this release" works on it.</summary>
    public required string ReleaseId { get; init; }

    /// <summary>The team's reference for the release, such as a purchase order ("PO 4512, line 2").</summary>
    public required string Reference { get; init; }

    /// <summary>The model the release is for, or null when any model may use it.</summary>
    public string? ModelId { get; init; }

    public required string Status { get; set; }

    public required string ReleasedBy { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime ReleasedAtUtc { get; init; }

    /// <summary>The battery that has it (Used or Published).</summary>
    public string? PackId { get; set; }

    public string? VoidReason { get; set; }

    public string? VoidedBy { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? VoidedAtUtc { get; set; }

    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
    public bool IsDemo { get; set; }
}

/// <summary>A released ID's status: Available → Used (a draft has it) → Published; or Voided, never reused.</summary>
public static class ReleasedIdStatuses
{
    public const string Available = "available";
    public const string Used = "used";
    public const string Published = "published";
    public const string Voided = "voided";

    public static readonly string[] All = [Available, Used, Published, Voided];

    public static string Words(string status) => status switch
    {
        Available => "Available",
        Used => "Used",
        Published => "Published",
        Voided => "Voided",
        _ => status,
    };
}
