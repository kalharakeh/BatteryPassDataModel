using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Dpp.Editing;

public enum SenderKind
{
    Team,
    ClusterAdmin,
    BatterySystemFeed,

    /// <summary>Market surveillance (step 4c-2): marks a passport invalid and changes no value.</summary>
    MarketSurveillance,
}

/// <summary>
/// Who sends a change, independent of how they signed in. Pages (plan 2d) and API tokens (step 5) map a signed-in
/// user or token to a sender.
/// </summary>
/// <param name="ClusterIds">The clusters a cluster admin or a feed token acts for; empty for the team.</param>
public sealed record ChangeSender(SenderKind Kind, string Who, IReadOnlySet<string> ClusterIds)
{
    /// <summary>A demo account or demo token: it changes only demo batteries (finding R03, <c>DemoScope</c>).</summary>
    public bool DemoOnly { get; init; }

    public static ChangeSender Team(string who) => new(SenderKind.Team, who, new HashSet<string>(StringComparer.Ordinal));

    public static ChangeSender ClusterAdmin(string who, params string[] clusterIds) =>
        new(SenderKind.ClusterAdmin, who, clusterIds.ToHashSet(StringComparer.Ordinal));

    public static ChangeSender Feed(string who, params string[] clusterIds) =>
        new(SenderKind.BatterySystemFeed, who, clusterIds.ToHashSet(StringComparer.Ordinal));

    public static ChangeSender MarketSurveillance(string who) => new(SenderKind.MarketSurveillance, who, new HashSet<string>(StringComparer.Ordinal));
}

/// <summary>"May this sender change this attribute?" (design 6.1, decisions 9 and 10, J-10).</summary>
public static class EditRights
{
    /// <param name="packClusterId">The cluster of the pack being changed; null for model, batch, site and operator records.</param>
    public static bool MayEdit(ChangeSender sender, AttributeDefinition attribute, string? packClusterId)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(attribute);
        var ownsPack = packClusterId is not null && sender.ClusterIds.Contains(packClusterId);
        return sender.Kind switch
        {
            SenderKind.Team => attribute.Editors.HasFlag(AttributeEditors.Team),
            SenderKind.ClusterAdmin => ownsPack && attribute.Editors.HasFlag(AttributeEditors.BatteryClusterAdmins),
            SenderKind.BatterySystemFeed => ownsPack && attribute.Editors.HasFlag(AttributeEditors.BatterySystemFeed),
            _ => false,
        };
    }
}
