using System.Security.Claims;

namespace BatteryPassWeb.Services;

public sealed class AccessControlService
{
    private readonly ClusterRepository _clusterRepository;

    public AccessControlService(ClusterRepository clusterRepository)
    {
        _clusterRepository = clusterRepository;
    }

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("admin");

    public static bool IsClusterAdmin(ClaimsPrincipal user) => user.IsInRole("clusterAdmin") || user.IsInRole("admin");

    public static string CurrentEmail(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email)
        ?? user.Identity?.Name
        ?? string.Empty;

    public async Task<IReadOnlyList<string>> GetClusterIdsForUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var email = CurrentEmail(user).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return [];
        }

        var memberships = await _clusterRepository.GetClusterMembershipsForUserAsync(email, cancellationToken);
        return memberships
            .Select(membership => BsonHelpers.GetString(membership, "clusterId"))
            .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

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
            .Where(membership => BsonHelpers.GetString(membership, "role").Equals("clusterAdmin", StringComparison.OrdinalIgnoreCase))
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

    public async Task<bool> CanOpenPassportDetailAsync(ClaimsPrincipal user, string clusterId, CancellationToken cancellationToken = default)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return false;
        }

        var clusterIds = await GetClusterIdsForUserAsync(user, cancellationToken);
        return clusterIds.Contains(clusterId, StringComparer.OrdinalIgnoreCase);
    }
}
