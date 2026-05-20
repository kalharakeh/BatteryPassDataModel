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
    public const string RoleLegitimateInterest = "legitimateInterest";

    private readonly ClusterRepository _clusterRepository;

    public AccessControlService(ClusterRepository clusterRepository)
    {
        _clusterRepository = clusterRepository;
    }

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(RoleAdmin);

    public static bool IsClusterAdmin(ClaimsPrincipal user) => user.IsInRole(RoleClusterAdmin) || user.IsInRole(RoleAdmin);

    public static string DisplayRoleLabel(ClaimsPrincipal user)
    {
        if (user.IsInRole(RoleAdmin)) return "Global Admin";
        if (user.IsInRole(RoleClusterAdmin)) return "Local Admin";
        if (user.IsInRole(RoleCommission)) return "Commission";
        if (user.IsInRole(RoleMarketSurveillanceAuthority)) return "Market Surveillance Authorities";
        if (user.IsInRole(RoleNotifiedBody)) return "Notified Body";
        if (user.IsInRole(RoleLegitimateInterest)) return "Person with Legitimate Interest";
        return "Normal User";
    }

    public static string DisplayRoleLabel(string role)
    {
        return role switch
        {
            RoleAdmin => "Global Admin",
            RoleClusterAdmin => "Local Admin",
            RoleCommission => "Commission",
            RoleMarketSurveillanceAuthority => "Market Surveillance Authorities",
            RoleNotifiedBody => "Notified Body",
            RoleLegitimateInterest => "Person with Legitimate Interest",
            RoleNormalUser => "Normal User",
            _ => "Normal User"
        };
    }

    public static bool CanSeeDraftPassports(ClaimsPrincipal user) =>
        IsAdmin(user) || user.IsInRole(RoleCommission);

    public static bool CanSeeSignedPassports(ClaimsPrincipal user) =>
        IsAdmin(user)
        || user.IsInRole(RoleNotifiedBody)
        || user.IsInRole(RoleMarketSurveillanceAuthority)
        || user.IsInRole(RoleCommission)
        || user.IsInRole(RoleLegitimateInterest);

    public static bool HasGlobalReportReadRole(ClaimsPrincipal user) =>
        user.IsInRole(RoleNotifiedBody)
        || user.IsInRole(RoleMarketSurveillanceAuthority)
        || user.IsInRole(RoleCommission)
        || user.IsInRole(RoleLegitimateInterest);

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

    public async Task<bool> CanOpenPassportDetailAsync(ClaimsPrincipal user, string clusterId, CancellationToken cancellationToken = default)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        if (HasGlobalReportReadRole(user))
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

    public async Task<bool> CanOpenPassportSummaryAsync(
        ClaimsPrincipal user,
        BsonDocument passport,
        PassportPublishPolicyService passportPublishPolicyService,
        CancellationToken cancellationToken = default)
    {
        if (passportPublishPolicyService.IsPubliclyVisible(passport))
        {
            return true;
        }

        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return await CanOpenPassportDetailAsync(user, passport, passportPublishPolicyService, cancellationToken);
    }

    public async Task<bool> CanOpenPassportDetailAsync(
        ClaimsPrincipal user,
        BsonDocument passport,
        PassportPublishPolicyService passportPublishPolicyService,
        CancellationToken cancellationToken = default)
    {
        if (IsArchived(passport))
        {
            return false;
        }

        if (IsAdmin(user))
        {
            return true;
        }

        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (HasGlobalReportReadRole(user))
        {
            return CanSeeDraftPassports(user) && IsDraft(passport)
                || IsSignedOrPublished(passport, passportPublishPolicyService);
        }

        var clusterId = BsonHelpers.GetString(passport, "clusterId");
        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return false;
        }

        if (await CanAdministerClusterAsync(user, clusterId, cancellationToken))
        {
            return true;
        }

        var clusterIds = await GetClusterIdsForUserAsync(user, cancellationToken);
        return clusterIds.Contains(clusterId, StringComparer.OrdinalIgnoreCase)
            && IsSignedOrPublished(passport, passportPublishPolicyService);
    }

    public async Task<bool> CanViewTrustConformanceAsync(ClaimsPrincipal user, string clusterId, CancellationToken cancellationToken = default)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        if (user.IsInRole(RoleMarketSurveillanceAuthority) || user.IsInRole(RoleCommission))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(clusterId) || !user.IsInRole(RoleClusterAdmin))
        {
            return false;
        }

        var managedClusterIds = await GetAdministeredClusterIdsForUserAsync(user, cancellationToken);
        return managedClusterIds.Contains(clusterId, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> CanViewBatteryHistoryAsync(ClaimsPrincipal user, string clusterId, CancellationToken cancellationToken = default)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        return await CanAdministerClusterAsync(user, clusterId, cancellationToken);
    }

    public async Task<bool> CanEditLatestBatteryPassportAsync(
        ClaimsPrincipal user,
        BsonDocument passport,
        CancellationToken cancellationToken = default)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        return passport.GetValue("isLatestForBattery", false).ToBoolean()
            && await CanAdministerClusterAsync(user, BsonHelpers.GetString(passport, "clusterId"), cancellationToken);
    }

    public async Task<bool> CanDownloadPassportDocumentAsync(
        ClaimsPrincipal user,
        string clusterId,
        string visibility,
        bool isPassportPublic,
        CancellationToken cancellationToken = default)
    {
        if (IsPublicVisibility(visibility) && isPassportPublic)
        {
            return true;
        }

        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return await CanOpenPassportDetailAsync(user, clusterId, cancellationToken);
    }

    public async Task<bool> CanDownloadPassportDocumentAsync(
        ClaimsPrincipal user,
        BsonDocument passport,
        string visibility,
        bool isPassportPublic,
        PassportPublishPolicyService passportPublishPolicyService,
        CancellationToken cancellationToken = default)
    {
        if (IsPublicVisibility(visibility) && isPassportPublic)
        {
            return true;
        }

        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return await CanOpenPassportDetailAsync(user, passport, passportPublishPolicyService, cancellationToken);
    }

    private static bool IsPublicVisibility(string visibility)
    {
        return visibility.Equals("public", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArchived(BsonDocument passport)
    {
        return BsonHelpers.GetString(passport, "registryInfo", "status")
            .Equals("archived", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDraft(BsonDocument passport)
    {
        return BsonHelpers.GetString(passport, "registryInfo", "status")
            .Equals("draft", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSignedOrPublished(BsonDocument passport, PassportPublishPolicyService passportPublishPolicyService)
    {
        var registryStatus = BsonHelpers.GetString(passport, "registryInfo", "status");
        return registryStatus.Equals("published", StringComparison.OrdinalIgnoreCase)
            || registryStatus.Equals("signed", StringComparison.OrdinalIgnoreCase)
            || passportPublishPolicyService.HasCurrentValidSignature(passport);
    }
}
