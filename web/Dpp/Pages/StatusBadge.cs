using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One of a battery's two statuses as a badge (redesign R3, decision 36), so they never look alike: the battery status
/// (#22, what happened to the battery) is a square "Battery" tag, the passport status (#2, whether this passport counts)
/// a round "Passport" pill. Each status has its own colour and icon, drawn by Views/Shared/_StatusBadge.cshtml.
/// </summary>
/// <param name="Kind">"battery" or "passport".</param>
/// <param name="Css">The colour class, e.g. "is-reused" or "is-invalid".</param>
/// <param name="Icon">The icon's id in Views/Shared/_Icons.cshtml, without "i-".</param>
/// <param name="Words">The status in words: "Re-used", "Marked for deletion".</param>
public sealed record StatusBadge(string Kind, string Css, string Icon, string Words)
{
    public const string BatteryKind = "battery";
    public const string PassportKind = "passport";

    /// <summary>The battery status (#22) as a badge; null when the battery has none (or the reader may not see it).</summary>
    public static StatusBadge? Battery(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        null or "" or "-" => null,
        "original" => new(BatteryKind, "is-original", "star", "Original"),
        "re-used" => new(BatteryKind, "is-reused", "reuse", "Re-used"),
        "repurposed" => new(BatteryKind, "is-repurposed", "repurpose", "Repurposed"),
        "remanufactured" => new(BatteryKind, "is-remanufactured", "wrench", "Remanufactured"),
        "waste" => new(BatteryKind, "is-waste", "waste", "Waste"),
        var other => new(BatteryKind, "is-original", "battery", char.ToUpperInvariant(other[0]) + other[1..]),
    };

    /// <summary>The passport status (#2) as a badge; a passport never published is a draft.</summary>
    public static StatusBadge Passport(string? dppStatus) => dppStatus switch
    {
        null or "" or "draft" => new(PassportKind, "is-draft", "passport", "Draft"),
        DppStatuses.Active => new(PassportKind, "is-active", "check", "Active"),
        DppStatuses.Archived => new(PassportKind, "is-archived", "archive", "Archived"),
        DppStatuses.Inactive => new(PassportKind, "is-inactive", "pause", "Inactive"),
        DppStatuses.Invalid => new(PassportKind, "is-invalid", "alert", "Invalid"),
        DppStatuses.MarkedForDeletion => new(PassportKind, "is-marked", "trash", "Marked for deletion"),
        var other => new(PassportKind, "is-inactive", "passport", DppStatuses.Words(other)),
    };

    /// <summary>The same badge with more words after the status, e.g. "· version 3".</summary>
    public string? Detail { get; init; }

    public StatusBadge With(string? detail) => this with { Detail = detail };
}
