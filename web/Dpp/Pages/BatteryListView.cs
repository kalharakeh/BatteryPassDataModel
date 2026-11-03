using System.Globalization;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One battery list for the team's Batteries page and the Registry (redesign R7, the owner's choice "One shared list"):
/// the same search, columns and row icons; each page brings its own rows, rules and links. Who sees which batteries,
/// clusters and battery statuses (#22) stays each page's rule, unchanged.
/// </summary>
/// <param name="Action">Where the search goes: "/admin/batteries" or "/registry".</param>
/// <param name="ShowClusters">Whether the reader may see clusters (and search by them, R6).</param>
/// <param name="OpenHref">Where a battery's serial number leads: its battery page for the team and its cluster admins, else its detail page.</param>
/// <param name="ShowsBatteryStatus">Whether the reader may see this battery's status (#22, purchaser data).</param>
public sealed record BatteryListView(
    string Action,
    string Query,
    IReadOnlyList<BatteryRow> Rows,
    bool ShowClusters,
    Func<BatteryRow, string> OpenHref,
    Func<BatteryRow, RowActions> Actions,
    Func<BatteryRow, bool> ShowsBatteryStatus)
{
    /// <summary>The team's "Create battery" button beside the search; null on the Registry.</summary>
    public string? CreateHref { get; init; }

    /// <summary>What an empty list says when nothing was searched.</summary>
    public string Empty { get; init; } = "No batteries to show.";

    /// <summary>What the search finds by: a cluster only for readers who may see clusters (R6).</summary>
    public string Placeholder => ShowClusters ? "Serial number, battery ID, passport ID or cluster" : "Serial number, battery ID or passport ID";

    public bool Searched => !string.IsNullOrWhiteSpace(Query);

    /// <summary>The version and its last update, short: "v2 · 3 Nov"; null for a draft.</summary>
    public static string? Updated(BatteryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return (row.Active ?? row.Latest) is { } shown ? $"v{shown.Number} · {shown.LastUpdatedUtc.ToString("d MMM", CultureInfo.InvariantCulture)}" : null;
    }

    /// <summary>The same in full, on hover: "Version 2 · updated 3 Nov 2026, 15:36".</summary>
    public static string? UpdatedInFull(BatteryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return (row.Active ?? row.Latest) is { } shown ? $"Version {shown.Number} · updated {ValueWords.Time(shown.LastUpdatedUtc)}" : null;
    }
}
