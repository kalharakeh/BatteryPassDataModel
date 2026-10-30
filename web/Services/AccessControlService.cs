using System.Security.Claims;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class AccessControlService
{
    public const string RoleAdmin = "admin";
    public const string RoleClusterAdmin = "clusterAdmin";
    public const string RoleNormalUser = "member";
    public const string RoleNotifiedBody = "notifiedBody";
    public const string RoleMarketSurveillanceAuthority = "marketSurveillanceAuthority";
    public const string RoleCommission = "commission";
    public const string RoleLiRepair = "liRepair";
    public const string RoleLiRecycling = "liRecycling";
    public const string RoleLiTreatment = "liTreatment";

    /// <summary>
    /// The three legitimate-interest roles of decision 6 (design 5.2), in place of the single "legitimateInterest" role,
    /// which no longer gives anything beyond public data.
    /// </summary>
    public static IReadOnlyList<string> LegitimateInterestRoles { get; } = [RoleLiRepair, RoleLiRecycling, RoleLiTreatment];

    private readonly ClusterRepository _clusterRepository;

    public AccessControlService(ClusterRepository clusterRepository)
    {
        _clusterRepository = clusterRepository;
    }

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(RoleAdmin);

    public static bool HasLegitimateInterestRole(ClaimsPrincipal user) => LegitimateInterestRoles.Any(user.IsInRole);

    public static string DisplayRoleLabel(ClaimsPrincipal user)
    {
        if (user.IsInRole(RoleAdmin)) return "Global Admin";
        if (user.IsInRole(RoleClusterAdmin)) return "Cluster Admin";
        if (user.IsInRole(RoleCommission)) return "Commission";
        if (user.IsInRole(RoleMarketSurveillanceAuthority)) return "Market Surveillance Authorities";
        if (user.IsInRole(RoleNotifiedBody)) return "Notified Body";
        if (user.IsInRole(RoleLiRepair)) return "Legitimate interest: repair, repurposing, remanufacturing";
        if (user.IsInRole(RoleLiRecycling)) return "Legitimate interest: recycling and battery treatment";
        if (user.IsInRole(RoleLiTreatment)) return "Legitimate interest: authorised or WEEE treatment";
        if (user.IsInRole(RoleNormalUser)) return "Normal User";
        return "Cluster Member";
    }

    public static string DisplayRoleLabel(string role)
    {
        return role switch
        {
            RoleAdmin => "Global Admin",
            RoleClusterAdmin => "Cluster Admin",
            RoleCommission => "Commission",
            RoleMarketSurveillanceAuthority => "Market Surveillance Authorities",
            RoleNotifiedBody => "Notified Body",
            RoleLiRepair => "Legitimate interest: repair, repurposing, remanufacturing",
            RoleLiRecycling => "Legitimate interest: recycling and battery treatment",
            RoleLiTreatment => "Legitimate interest: authorised or WEEE treatment",
            RoleNormalUser => "Normal User",
            _ => "Cluster Member"
        };
    }

    public static string CurrentEmail(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email)
        ?? user.Identity?.Name
        ?? string.Empty;

    public async Task<IReadOnlyList<string>> GetAdministeredClusterIdsForUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (IsAdmin(user))
        {
            return [];
        }

        var email = CurrentEmail(user).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return [];
        }

        var memberships = await _clusterRepository.GetClusterMembershipsForUserAsync(email, cancellationToken);
        return memberships
            .Where(membership => BsonHelpers.GetString(membership, "role").Equals(RoleClusterAdmin, StringComparison.OrdinalIgnoreCase))
            .Select(membership => BsonHelpers.GetString(membership, "clusterId"))
            .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> CanAdministerClusterAsync(ClaimsPrincipal user, string clusterId, CancellationToken cancellationToken = default)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return false;
        }

        var managedClusterIds = await GetAdministeredClusterIdsForUserAsync(user, cancellationToken);
        return managedClusterIds.Contains(clusterId, StringComparer.OrdinalIgnoreCase);
    }
}
