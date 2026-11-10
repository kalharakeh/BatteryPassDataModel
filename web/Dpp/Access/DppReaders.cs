using System.Security.Claims;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>
/// Who is reading (design 5.2): a sign-in or an API token as a <see cref="Reader"/>, with its app-wide roles and the
/// clusters it belongs to. Pages, the API and document downloads all ask here, then apply <see cref="AudienceRules"/>.
/// </summary>
public sealed class DppReaders(ClusterRepository clusters)
{
    /// <summary>The app-wide roles the team gives on the Users page, as readers. Any other role gives nothing extra.</summary>
    public static IReadOnlyDictionary<string, ReaderRole> AppRoles { get; } = new Dictionary<string, ReaderRole>(StringComparer.OrdinalIgnoreCase)
    {
        [AccessControlService.RoleAdmin] = ReaderRole.Team,
        [AccessControlService.RoleNotifiedBody] = ReaderRole.NotifiedBody,
        [AccessControlService.RoleMarketSurveillanceAuthority] = ReaderRole.MarketSurveillance,
        [AccessControlService.RoleCommission] = ReaderRole.Commission,
        [AccessControlService.RoleLiRepair] = ReaderRole.LiRepair,
        [AccessControlService.RoleLiRecycling] = ReaderRole.LiRecycling,
        [AccessControlService.RoleLiTreatment] = ReaderRole.LiTreatment,
    };

    /// <summary>
    /// The signed-in user as a reader: their app-wide role, and a cluster user or cluster admin of each cluster they are
    /// a member of. Read from the memberships on every request, so a membership removed by an admin counts at once.
    /// </summary>
    public async Task<Reader> ForAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var email = AccessControlService.CurrentEmail(user).Trim().ToLowerInvariant();
        if (user.Identity?.IsAuthenticated != true || email.Length == 0)
        {
            return Reader.Anonymous;
        }

        var roles = user.FindAll(ClaimTypes.Role)
            .Select(claim => AppRoles.TryGetValue(claim.Value, out var role) ? role : (ReaderRole?)null)
            .OfType<ReaderRole>()
            .ToHashSet();
        var clusterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var membership in await clusters.GetClusterMembershipsForUserAsync(email, cancellationToken))
        {
            var clusterId = BsonHelpers.GetString(membership, "clusterId");
            if (clusterId.Length == 0)
            {
                continue;
            }

            clusterIds.Add(ClusterRepository.NormalizeClusterId(clusterId));
            roles.Add(BsonHelpers.GetString(membership, "role").Equals(AccessControlService.RoleClusterAdmin, StringComparison.OrdinalIgnoreCase)
                ? ReaderRole.ClusterAdmin
                : ReaderRole.ClusterUser);
        }

        // A demo account's roles and memberships count only on demo batteries (finding R03, DemoScope).
        var demo = ClusterRepository.IsDemo(await clusters.GetUserByEmailAsync(AccessControlService.CurrentEmail(user), cancellationToken));
        return new Reader(roles, clusterIds, email) { DemoOnly = demo };
    }

    /// <summary>
    /// The signed-in user as the sender of a change (design 6.1, step 4a): the team for an admin; otherwise a cluster
    /// admin of only the clusters where their membership is cluster admin, since a plain member of a cluster changes
    /// nothing there. Null for anyone who changes nothing. Read from the memberships on every request, like
    /// <see cref="ForAsync"/>.
    /// </summary>
    public async Task<ChangeSender?> EditorForAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var email = AccessControlService.CurrentEmail(user).Trim().ToLowerInvariant();
        if (user.Identity?.IsAuthenticated != true || email.Length == 0)
        {
            return null;
        }

        // A demo account changes only demo batteries (finding R03, DemoScope).
        var demo = ClusterRepository.IsDemo(await clusters.GetUserByEmailAsync(AccessControlService.CurrentEmail(user), cancellationToken));
        if (user.FindAll(ClaimTypes.Role).Any(claim => claim.Value.Equals(AccessControlService.RoleAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            return ChangeSender.Team(email) with { DemoOnly = demo };
        }

        var adminOf = (await clusters.GetClusterMembershipsForUserAsync(email, cancellationToken))
            .Where(membership => BsonHelpers.GetString(membership, "role").Equals(AccessControlService.RoleClusterAdmin, StringComparison.OrdinalIgnoreCase))
            .Select(membership => BsonHelpers.GetString(membership, "clusterId"))
            .Where(clusterId => clusterId.Length > 0)
            .Select(ClusterRepository.NormalizeClusterId)
            .ToArray();
        return adminOf.Length == 0 ? null : ChangeSender.ClusterAdmin(email, adminOf) with { DemoOnly = demo };
    }

    /// <summary>Market surveillance marking a passport (step 4c-2), as the reader it signed in as: demo stays demo (finding R03).</summary>
    public static ChangeSender SurveillanceSenderFor(Reader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ChangeSender.MarketSurveillance(reader.Who) with { DemoOnly = reader.DemoOnly };
    }

    /// <summary>
    /// An API token as a reader (design 5.2). A token for named clusters reads like a cluster user of those clusters. A
    /// global token is the team's token, issued only for Scania's own systems, so it reads like the team.
    /// </summary>
    public static Reader For(ExternalApiTokenContext token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var who = $"api-token:{token.TokenId}";
        var reader = token.GlobalAccess
            ? Reader.With(who, ReaderRole.Team)
            : new Reader(
                new HashSet<ReaderRole> { ReaderRole.ClusterUser },
                token.ClusterIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(ClusterRepository.NormalizeClusterId).ToHashSet(StringComparer.Ordinal),
                who);
        return reader with { DemoOnly = token.IsDemo }; // a demo token reaches only demo batteries (finding R03)
    }

    /// <summary>
    /// An API token as the sender of a change (design 6.1, J-10), with the same name as its reader: a global token is the
    /// team's; a token for named clusters is a battery system of those clusters, which changes only the live values of
    /// their batteries.
    /// </summary>
    public static ChangeSender SenderFor(ExternalApiTokenContext token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var who = For(token).Who;
        var sender = token.GlobalAccess
            ? ChangeSender.Team(who)
            : new ChangeSender(
                SenderKind.BatterySystemFeed,
                who,
                token.ClusterIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(ClusterRepository.NormalizeClusterId).ToHashSet(StringComparer.OrdinalIgnoreCase));
        return sender with { DemoOnly = token.IsDemo }; // a demo token changes only demo batteries (finding R03)
    }
}
