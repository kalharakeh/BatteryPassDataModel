using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>The roles of design 5.2. A reader with no roles is anonymous.</summary>
public enum ReaderRole
{
    ClusterUser,
    ClusterAdmin,
    Team,
    NotifiedBody,
    MarketSurveillance,
    Commission,
    LiRepair,
    LiRecycling,
    LiTreatment,
}

/// <summary>Someone reading passport data: their roles and the clusters they belong to. Step 3 maps sign-ins and tokens to readers.</summary>
public sealed record Reader(IReadOnlySet<ReaderRole> Roles, IReadOnlySet<string> ClusterIds, string Who)
{
    public static Reader Anonymous { get; } = new(new HashSet<ReaderRole>(), new HashSet<string>(StringComparer.Ordinal), "anonymous");

    public static Reader With(string who, ReaderRole role, params string[] clusterIds) =>
        new(new HashSet<ReaderRole> { role }, clusterIds.ToHashSet(StringComparer.Ordinal), who);
}

/// <summary>Who may see an attribute (design 5.1 and 5.2): the one rule behind pages, the API, exports and documents.</summary>
public static class AudienceRules
{
    /// <param name="batteryClusterIds">
    /// The clusters of the published packs the value belongs to: one for a pack value; every cluster owning a published
    /// pack of the model or batch for a model or batch value; empty when there is none (drafts count for nobody, J-6).
    /// </param>
    public static bool MaySee(Reader reader, AttributeDefinition attribute, IReadOnlyCollection<string> batteryClusterIds)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(batteryClusterIds);
        if (attribute.Audience == Audience.Public || reader.Roles.Contains(ReaderRole.Team) || reader.Roles.Contains(ReaderRole.MarketSurveillance))
        {
            return true;
        }

        var ownsTheBattery = (reader.Roles.Contains(ReaderRole.ClusterUser) || reader.Roles.Contains(ReaderRole.ClusterAdmin))
            && batteryClusterIds.Any(reader.ClusterIds.Contains);
        return attribute.Audience switch
        {
            Audience.Purchaser => ownsTheBattery,
            Audience.Authorities => reader.Roles.Contains(ReaderRole.NotifiedBody) || reader.Roles.Contains(ReaderRole.Commission),
            Audience.Shared => ownsTheBattery
                || reader.Roles.Contains(ReaderRole.Commission)
                || (reader.Roles.Contains(ReaderRole.LiRepair) && attribute.SharedWith.HasFlag(LegitimateInterestGroups.Repair))
                || (reader.Roles.Contains(ReaderRole.LiRecycling) && attribute.SharedWith.HasFlag(LegitimateInterestGroups.Recycling))
                || (reader.Roles.Contains(ReaderRole.LiTreatment) && attribute.SharedWith.HasFlag(LegitimateInterestGroups.Treatment)),
            _ => false,
        };
    }

    /// <summary>
    /// Whether the reader may see app data kept for the battery's purchaser, such as its module IDs (decision 31): the
    /// team, market surveillance and the battery's cluster, the same readers as a purchaser attribute's.
    /// </summary>
    public static bool MaySeePurchaserData(Reader reader, IReadOnlyCollection<string> batteryClusterIds)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(batteryClusterIds);
        return reader.Roles.Contains(ReaderRole.Team)
            || reader.Roles.Contains(ReaderRole.MarketSurveillance)
            || ((reader.Roles.Contains(ReaderRole.ClusterUser) || reader.Roles.Contains(ReaderRole.ClusterAdmin))
                && batteryClusterIds.Any(reader.ClusterIds.Contains));
    }
}
