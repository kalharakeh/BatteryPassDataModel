using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>The Registry: the published batteries a signed-in reader may open, and whether to show their clusters.</summary>
public sealed record RegistryPage(IReadOnlyList<BatteryRow> Rows, string Query, bool ShowClusters, string Scope)
{
    /// <summary>The clusters the signed-in user is admin of (step 4a); empty for everyone else, the team included.</summary>
    public IReadOnlySet<string> ManagedClusterIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The hand-overs waiting for the signed-in cluster admin's answer (step 4d, mock-up S1).</summary>
    public IReadOnlyList<WaitingHandOver> Waiting { get; init; } = [];

    /// <summary>The cluster admin's page of a battery of a cluster they are admin of; null otherwise.</summary>
    public string? ManageHref(BatteryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Active is not null && row.ClusterId is { } cluster && ManagedClusterIds.Contains(cluster) ? ClusterBatteryPages.BatteryHref(row.PackId) : null;
    }
}

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
        // An active, invalid or marked-for-deletion passport is listed (decision 34): market surveillance finds what it
        // flagged, and the cluster still owns the battery. An inactive battery continues on its new passport (step 4c).
        return (row.Active is not null || row.Latest?.DppStatus is DppStatuses.Invalid or DppStatuses.MarkedForDeletion)
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

    /// <param name="editor">The signed-in user as the sender of a change; a cluster admin's rows get a "Manage" link (step 4a).</param>
    public static RegistryPage Build(Reader reader, BatteriesPage batteries, ChangeSender? editor = null)
    {
        ArgumentNullException.ThrowIfNull(batteries);
        return new RegistryPage(
            batteries.Rows.Where(row => MayList(reader, row)).ToArray(),
            batteries.Query,
            ShowsClusters(reader),
            Scope(reader))
        {
            ManagedClusterIds = editor is { Kind: SenderKind.ClusterAdmin } ? editor.ClusterIds.ToHashSet(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal),
        };
    }
}
