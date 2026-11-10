using System.Security.Claims;
using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;
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

    public const string KeptForTheTeam = "Only the team can add this address.";
    public const string AddressKeptForTheTeam = "Only the team can use this address.";
    public const string NewPasswordForAppWideRole = "Set a new password when you give this account access across the app: someone else may know its old one.";
    public const string OnlyTheTeamChangesThisAccount = "Only the team can change this person's name or password.";
    public const string TeamAccountsAreTheTeams = "This is a team account: only the team changes it.";
    public const string NotYourOwnAccount = "You can't change your own account here. Use Account at the top of the page.";

    private readonly ClusterRepository _clusterRepository;
    private readonly string _fallbackAdminEmail;

    public AccessControlService(ClusterRepository clusterRepository, IOptions<BatteryPassOptions>? options = null)
    {
        _clusterRepository = clusterRepository;
        _fallbackAdminEmail = options?.Value.DemoAdminEmail?.Trim() ?? string.Empty;
    }

    /// <summary>Whether the address is the fallback admin's (DemoAdminEmail), whatever its capitals and outer spaces.</summary>
    public bool IsFallbackAdminAddress(string? email) =>
        _fallbackAdminEmail.Length > 0 && string.Equals(email?.Trim(), _fallbackAdminEmail, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A role that gives something across the app: the team, the authorities and the legitimate-interest roles. A plain
    /// member (and the old "viewer") has none; "clusterAdmin" is a cluster's role, not one across the app.
    /// </summary>
    public static bool IsAppWideRole(string? role) =>
        !string.IsNullOrWhiteSpace(role)
        && !role.Trim().Equals(RoleNormalUser, StringComparison.OrdinalIgnoreCase)
        && !role.Trim().Equals("viewer", StringComparison.OrdinalIgnoreCase)
        && !role.Trim().Equals(RoleClusterAdmin, StringComparison.OrdinalIgnoreCase);

    public static bool HasAppWideRole(IEnumerable<string> roles) => roles.Any(IsAppWideRole);

    /// <summary>
    /// Finding R19's rule: a cluster admin may change the name or set the password of an account only when a cluster admin
    /// of one of their clusters made it (<paramref name="createdInCluster"/>, the account's mark; none for the team's
    /// accounts and older ones), it has no role across the app, and every cluster it belongs to (at least one) is theirs.
    /// </summary>
    public static bool ClusterAdminMayChangeNameAndPassword(
        IEnumerable<string> accountRoles,
        IReadOnlyCollection<string> accountClusterIds,
        IReadOnlySet<string> administeredClusterIds,
        string? createdInCluster) =>
        !string.IsNullOrWhiteSpace(createdInCluster)
        && administeredClusterIds.Contains(createdInCluster)
        && !HasAppWideRole(accountRoles)
        && accountClusterIds.Count > 0
        && accountClusterIds.All(administeredClusterIds.Contains);

    public static IReadOnlyList<string> RolesOf(BsonDocument? user) =>
        user?.GetValue("roles", new BsonArray()) is BsonArray roles
            ? roles.Where(role => role.IsString).Select(role => role.AsString).ToList()
            : [];

    /// <summary>
    /// What <paramref name="actor"/> may do to the account with this address (finding R19). Every path that changes an
    /// account from the cluster admins' pages asks this first: "Add user", the row's name, password and clusters, removing.
    /// </summary>
    public async Task<AccountRights> RightsOverAccountAsync(ClaimsPrincipal actor, string email, CancellationToken cancellationToken = default)
    {
        var account = await _clusterRepository.GetUserByEmailAsync(email, cancellationToken);
        if (IsAdmin(actor))
        {
            return new AccountRights(account is not null, null, true);
        }

        if (IsFallbackAdminAddress(email))
        {
            return new AccountRights(account is not null, KeptForTheTeam, false);
        }

        if (string.Equals(email.Trim(), CurrentEmail(actor).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new AccountRights(account is not null, NotYourOwnAccount, false);
        }

        if (account is null)
        {
            return new AccountRights(false, null, false);
        }

        var roles = RolesOf(account);
        if (roles.Contains(RoleAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return new AccountRights(true, TeamAccountsAreTheTeams, false);
        }

        var administered = (await GetAdministeredClusterIdsForUserAsync(actor, cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clusters = (await _clusterRepository.GetClusterMembershipsForUserAsync(email, cancellationToken))
            .Select(membership => BsonHelpers.GetString(membership, "clusterId"))
            .ToList();
        return new AccountRights(true, null, ClusterAdminMayChangeNameAndPassword(
            roles, clusters, administered, BsonHelpers.GetString(account, ClusterRepository.CreatedInClusterField)));
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
        if (user.IsInRole(RoleNormalUser)) return "Cluster member"; // as the user lists say (finding W04)
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
            RoleNormalUser => "Cluster member",
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

/// <summary>
/// What someone may do to one account: <see cref="Refusal"/> says why they may change nothing; otherwise they may create it
/// (when it doesn't exist) or add and remove their own clusters' memberships, and change its name and password only when
/// <see cref="MayChangeNameAndPassword"/>.
/// </summary>
public sealed record AccountRights(bool Exists, string? Refusal, bool MayChangeNameAndPassword);
