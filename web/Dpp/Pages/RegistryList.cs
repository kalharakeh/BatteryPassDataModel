using BatteryPassWeb.Dpp.Access;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>The Registry: the published batteries a signed-in reader may open, and whether to show their clusters.</summary>
public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query, bool ShowClusters, string Scope);

/// <summary>
/// Who the Registry lists which batteries for (decision 30). The team, the authorities and the legitimate-interest roles
/// see every published battery: each one's public and shared values are theirs to read. A cluster's users and admins see
/// their own clusters' batteries. Drafts are never listed here; the team finds them on the Batteries page.
/// </summary>
public static class RegistryList
{
    private static readonly ReaderRole[] EveryPublishedBattery =
    [
        ReaderRole.Team, ReaderRole.NotifiedBody, ReaderRole.MarketSurveillance, ReaderRole.Commission,
        ReaderRole.LiRepair, ReaderRole.LiRecycling, ReaderRole.LiTreatment,
    ];

    public static bool MayList(Reader reader, BatteryRow row)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(row);
        return !row.IsDraft
            && (EveryPublishedBattery.Any(reader.Roles.Contains) || (row.ClusterId is { } cluster && reader.ClusterIds.Contains(cluster)));
    }

    /// <summary>A battery's cluster is the team's and its members' business, not the authorities' or other readers'.</summary>
    public static bool ShowsClusters(Reader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return reader.Roles.Contains(ReaderRole.Team) || reader.ClusterIds.Count > 0;
    }

    public static string Scope(Reader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return EveryPublishedBattery.Any(reader.Roles.Contains) ? "Every published battery passport."
            : reader.ClusterIds.Count > 0 ? "The published batteries of your clusters."
            : "No batteries are shared with your account yet. Ask your cluster admin or the manufacturer's team.";
    }

    public static RegistryPage Build(Reader reader, BatteriesPage batteries)
    {
        ArgumentNullException.ThrowIfNull(batteries);
        return new RegistryPage(
            batteries.Rows.Where(row => MayList(reader, row)).ToArray(),
            batteries.Query,
            ShowsClusters(reader),
            Scope(reader));
    }
}
