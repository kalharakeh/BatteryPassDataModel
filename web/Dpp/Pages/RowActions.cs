namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// A battery list row's actions as icons with their names on hover, like the old app's row actions (redesign R5, owner's
/// item 1): the battery page (pencil) for whoever may change the battery, the public and detail pages (globe, document)
/// once the passport is published, and the battery's history in the audit log (clock) for the team, whose log it is.
/// </summary>
public sealed record RowActions(string Serial, string? Manage, string? Public, string? Detail, string? History)
{
    /// <param name="manage">The battery page this reader opens, or null when they may not change the battery.</param>
    /// <param name="team">True for the team: only it reads the audit log.</param>
    public static RowActions For(BatteryRow row, string? manage, bool team)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = Uri.EscapeDataString(row.Identifier);
        var published = !row.IsDraft && row.Identifier.Length > 0;
        return new RowActions(
            row.SerialNumber,
            manage,
            published ? $"/dpp/{key}" : null,
            published ? $"/dpp/{key}/detail" : null,
            team ? $"/admin/audit?q={Uri.EscapeDataString(row.PackId)}" : null);
    }
}
