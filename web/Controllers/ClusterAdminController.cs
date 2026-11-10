using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The cluster admins' workspace: their clusters' users and API tokens. Their batteries are listed on the Registry,
/// which replaces "Managed passports" (decision 30); step 4 brings the cluster admins' editing.
/// </summary>
[Authorize(Policy = "ClusterAdminOrAdmin")]
[Route("cluster-admin")]
public class ClusterAdminController : Controller
{
    /// <summary>The users whose name and password this cluster admin may change (finding R19).</summary>
    public const string MayChangeAccountKey = "MayChangeAccount";

    private readonly AccessControlService _accessControlService;
    private readonly ApplicationAuditService _applicationAuditService;
    private readonly ClusterRepository _clusterRepository;
    private readonly ExternalApiRepository _externalApiRepository;

    public ClusterAdminController(
        AccessControlService accessControlService,
        ApplicationAuditService applicationAuditService,
        ClusterRepository clusterRepository,
        ExternalApiRepository externalApiRepository)
    {
        _accessControlService = accessControlService;
        _applicationAuditService = applicationAuditService;
        _clusterRepository = clusterRepository;
        _externalApiRepository = externalApiRepository;
    }

    [HttpGet("")]
    public IActionResult Index() => Redirect("/cluster-admin/users");

    /// <summary>The old "Managed passports" address leads to the Registry, which replaces it (decision 30).</summary>
    [HttpGet("passports")]
    public IActionResult Passports() => Redirect("/registry");

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? error, CancellationToken cancellationToken)
    {
        var managedClusterIds = await ManagedClusterIdsAsync(cancellationToken);
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var visibleClusters = AccessControlService.IsAdmin(User)
            ? clusters
            : clusters.Where(cluster => managedClusterIds.Contains(BsonHelpers.GetString(cluster, "clusterId"))).ToList();

        var memberships = await _clusterRepository.ListClusterMembershipsAsync(cancellationToken);
        var visibleMemberships = AccessControlService.IsAdmin(User)
            ? memberships
            : memberships
                .Where(membership => managedClusterIds.Contains(BsonHelpers.GetString(membership, "clusterId")))
                .ToList();
        var visibleEmails = visibleMemberships
            .Select(membership => BsonHelpers.GetString(membership, "email"))
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var users = await _clusterRepository.ListUsersAsync(cancellationToken);
        var visibleUsers = AccessControlService.IsAdmin(User)
            ? users
            : users.Where(user => visibleEmails.Contains(BsonHelpers.GetString(user, "email"))).ToList();

        var model = new AdminClusterViewModel
        {
            SelectedTab = "users",
            Clusters = visibleClusters.Select(cluster => new ClusterViewModel
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name"),
                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
            }).OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            Users = visibleUsers.Select(user => new UserViewModel
            {
                Email = BsonHelpers.GetString(user, "email"),
                Name = BsonHelpers.GetString(user, "name"),
                Roles = (user.GetValue("roles", new BsonArray()) as BsonArray ?? new BsonArray())
                    .Select(role => role.ToString() ?? string.Empty)
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Select(AccessControlService.DisplayRoleLabel)
                    .ToList()
            }).OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase).ToList(),
            Memberships = visibleMemberships.Select(membership => new ClusterMembershipViewModel
            {
                Email = BsonHelpers.GetString(membership, "email"),
                ClusterId = BsonHelpers.GetString(membership, "clusterId"),
                Role = BsonHelpers.GetString(membership, "role"),
                CreatedAt = BsonHelpers.GetString(membership, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(membership, "updatedAt")
            }).ToList()
        };

        // The users whose name and password this cluster admin may change: the rule of finding R19, on what is already read.
        var clusterIdsByEmail = memberships
            .GroupBy(membership => BsonHelpers.GetString(membership, "email"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<string>)group.Select(membership => BsonHelpers.GetString(membership, "clusterId")).ToList(), StringComparer.OrdinalIgnoreCase);
        var currentEmail = AccessControlService.CurrentEmail(User);
        ViewData[MayChangeAccountKey] = visibleUsers
            .Where(user => AccessControlService.IsAdmin(User)
                || (!BsonHelpers.GetString(user, "email").Equals(currentEmail, StringComparison.OrdinalIgnoreCase)
                    && !_accessControlService.IsFallbackAdminAddress(BsonHelpers.GetString(user, "email"))
                    && AccessControlService.ClusterAdminMayChangeNameAndPassword(
                        AccessControlService.RolesOf(user),
                        clusterIdsByEmail.GetValueOrDefault(BsonHelpers.GetString(user, "email")) ?? [],
                        managedClusterIds,
                        BsonHelpers.GetString(user, ClusterRepository.CreatedInClusterField))))
            .Select(user => BsonHelpers.GetString(user, "email"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ViewData["StatusMessage"] = TempData["StatusMessage"]?.ToString() ?? string.Empty;
        ViewData["ErrorMessage"] = TempData["ErrorMessage"]?.ToString()
            ?? (string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error));
        return View(model);
    }

    [HttpGet("secrets")]
    public IActionResult Secrets() => Redirect("/cluster-admin/api-tokens");

    [HttpGet("api-tokens")]
    public async Task<IActionResult> ApiTokens(CancellationToken cancellationToken)
    {
        var managedClusterIds = await ManagedClusterIdsAsync(cancellationToken);
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var visibleClusters = clusters
            .Where(cluster => managedClusterIds.Contains(BsonHelpers.GetString(cluster, "clusterId")))
            .Select(cluster => new ClusterViewModel
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name"),
                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
            })
            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var clusterNamesById = visibleClusters.ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);

        var tokens = await _externalApiRepository.ListTokensAsync(cancellationToken);
        var visibleTokens = tokens
            .Where(token => TokenIsInManagedScope(token, managedClusterIds))
            .Select(token => MapApiToken(token, clusterNamesById))
            .OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View("ApiTokens", new AdminClusterViewModel
        {
            SelectedTab = "api-token-management",
            Clusters = visibleClusters,
            ApiTokens = visibleTokens,
            StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
            ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty,
            GeneratedCredential = TempData["GeneratedCredential"]?.ToString() ?? string.Empty
        });
    }

    [HttpPost("api-tokens/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateClusterApiToken(CancellationToken cancellationToken)
    {
        var clusterIds = Request.Form["clusterIds"]
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (clusterIds.Count == 0 || !await CanAdministerAllClustersAsync(clusterIds, cancellationToken))
        {
            TempData["ErrorMessage"] = "Choose at least one cluster you administer.";
            return Redirect("/cluster-admin/api-tokens");
        }

        var name = Text(Request.Form, "name", "Cluster API token");
        // A cluster token reads its clusters' batteries or also sends their live values; creating and publishing are the
        // team's (decision 29), so nothing above read + write is given here.
        var accessMode = ParseTokenMode(Text(Request.Form, "accessMode", "read")) == ExternalTokenAccessMode.Read
            ? ExternalTokenAccessMode.Read
            : ExternalTokenAccessMode.ReadWrite;
        var actor = AccessControlService.CurrentEmail(User);
        var (tokenDocument, tokenValue) = await _externalApiRepository.CreateTokenAsync(
            name,
            accessMode,
            clusterIds,
            allowUnassigned: false,
            globalAccess: false,
            actor: string.IsNullOrWhiteSpace(actor) ? "cluster-admin" : actor,
            isDemo: await _accessControlService.IsDemoAccountAsync(User, cancellationToken), // a demo admin's token is demo (finding R03)
            cancellationToken: cancellationToken);

        await AppendClusterAdminAuditAsync(
            "apiToken.created",
            "Cluster API token created.",
            new BsonDocument
            {
                ["tokenId"] = BsonHelpers.GetString(tokenDocument, "tokenId"),
                ["name"] = name,
                ["accessMode"] = accessMode.ToString(),
                ["clusterIds"] = new BsonArray(clusterIds.Select(clusterId => (BsonValue)clusterId))
            },
            "apiToken",
            BsonHelpers.GetString(tokenDocument, "tokenId"),
            cancellationToken);
        TempData["GeneratedCredential"] = tokenValue;
        TempData["StatusMessage"] = "API token created.";
        return Redirect("/cluster-admin/api-tokens");
    }

    [HttpPost("api-tokens/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteClusterApiToken(CancellationToken cancellationToken)
    {
        var tokenId = Text(Request.Form, "tokenId");
        if (!await ValidateClusterTokenScopeAsync(tokenId, cancellationToken))
        {
            return NotFound();
        }

        await _externalApiRepository.SetTokenActiveAsync(tokenId, false, AccessControlService.CurrentEmail(User), cancellationToken);
        await AppendClusterAdminAuditAsync(
            "apiToken.deleted",
            "Cluster API token deactivated.",
            new BsonDocument { ["tokenId"] = tokenId },
            "apiToken",
            tokenId,
            cancellationToken);
        TempData["StatusMessage"] = "API token deactivated.";
        return Redirect("/cluster-admin/api-tokens");
    }

    [HttpPost("api-tokens/regenerate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateClusterApiToken(CancellationToken cancellationToken)
    {
        var tokenId = Text(Request.Form, "tokenId");
        if (!await ValidateClusterTokenScopeAsync(tokenId, cancellationToken))
        {
            return NotFound();
        }

        var tokenValue = await _externalApiRepository.RegenerateTokenAsync(tokenId, AccessControlService.CurrentEmail(User), cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            TempData["ErrorMessage"] = "API token could not be regenerated.";
        }
        else
        {
            TempData["GeneratedCredential"] = tokenValue;
            TempData["StatusMessage"] = "API token regenerated.";
            await AppendClusterAdminAuditAsync(
                "apiToken.regenerated",
                "Cluster API token regenerated.",
                new BsonDocument { ["tokenId"] = tokenId },
                "apiToken",
                tokenId,
                cancellationToken);
        }

        return Redirect("/cluster-admin/api-tokens");
    }

    /// <summary>What "Add user" says when the address already has an account (finding R19).</summary>
    public const string AlreadyHasAnAccount = "This person already has an account. They were added to your cluster; their name and password stay as they are.";

    /// <summary>
    /// "Add user" (no <c>change</c>), and a row's "Name", "New password" and "Clusters" (<c>change</c> = name, password,
    /// cluster). Every one asks <see cref="AccessControlService.RightsOverAccountAsync"/> first (finding R19): an existing
    /// account only joins the cluster, and its name and password change only where the rule allows.
    /// </summary>
    [HttpPost("users/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email").Trim().ToLowerInvariant();
        var clusterId = Text(Request.Form, "clusterId");
        var role = Text(Request.Form, "role", "member");
        var change = Text(Request.Form, "change").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Redirect("/cluster-admin/users");
        }

        var back = $"/cluster-admin/users?openUser={Uri.EscapeDataString(email)}";
        var rights = await _accessControlService.RightsOverAccountAsync(User, email, cancellationToken);
        if (rights.Refusal is { } refusal)
        {
            TempData["ErrorMessage"] = refusal;
            return Redirect(back);
        }

        var password = Text(Request.Form, "password");
        var passwordConfirmation = Text(Request.Form, "passwordConfirmation");
        if (change is "name" or "password")
        {
            if (!rights.Exists || !rights.MayChangeNameAndPassword)
            {
                TempData["ErrorMessage"] = AccessControlService.CantChangeThisAccount;
                return Redirect(back);
            }

            // The write itself carries the rule's conditions, so it changes nothing if the team gave the account a role meanwhile.
            var administered = await ManagedClusterIdsAsync(cancellationToken);
            if (change == "name")
            {
                var name = Text(Request.Form, "name", email);
                if (!await _clusterRepository.ChangeIfAClusterAdminMayAsync(email, administered, name, null, cancellationToken))
                {
                    TempData["ErrorMessage"] = AccessControlService.CantChangeThisAccount;
                    return Redirect(back);
                }

                await AuditUserSavedAsync("Cluster user's name changed.", email, name, null, null, false, cancellationToken);
                TempData["StatusMessage"] = "Name saved.";
                return Redirect(back);
            }

            if (string.IsNullOrWhiteSpace(password) || !password.Equals(passwordConfirmation, StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(password) ? "Type the new password." : "Passwords do not match.";
                return Redirect(back);
            }

            if (AuthService.NewPasswordProblem(password) is { } shortPassword)
            {
                TempData["ErrorMessage"] = shortPassword;
                return Redirect(back);
            }

            if (!await _clusterRepository.ChangeIfAClusterAdminMayAsync(email, administered, null, BCryptNet.HashPassword(password), cancellationToken))
            {
                TempData["ErrorMessage"] = AccessControlService.CantChangeThisAccount;
                return Redirect(back);
            }

            await AuditUserSavedAsync("Cluster user's password changed.", email, null, null, null, true, cancellationToken);
            TempData["StatusMessage"] = "Password saved.";
            return Redirect(back);
        }

        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return Redirect(back);
        }

        if (!ClusterRepository.IsClusterMembershipRole(role))
        {
            TempData["ErrorMessage"] = ClusterRepository.OnlyTwoClusterRoles;
            return Redirect(back);
        }

        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }

        if (rights.Exists)
        {
            // An existing account only joins the cluster: whatever was typed, its name and password stay (finding R19).
            await _clusterRepository.UpsertClusterMembershipAsync(email, clusterId, role, cancellationToken);
            await AuditUserSavedAsync("Cluster user's membership saved.", email, null, clusterId, role, false, cancellationToken);
            TempData["StatusMessage"] = change == "cluster" ? "Clusters saved." : AlreadyHasAnAccount;
            return Redirect(back);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            TempData["ErrorMessage"] = "Password is required for new users.";
            return Redirect(back);
        }

        if (!password.Equals(passwordConfirmation, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "Passwords do not match.";
            return Redirect(back);
        }

        if (AuthService.NewPasswordProblem(password) is { } problem)
        {
            TempData["ErrorMessage"] = problem;
            return Redirect(back);
        }

        var displayName = Text(Request.Form, "name", email);
        // An account a demo cluster admin makes is a demo account, cleared with the demo data (finding R03).
        await _clusterRepository.CreateAccountInClusterAsync(
            email, displayName, BCryptNet.HashPassword(password), clusterId, cancellationToken,
            isDemo: await _accessControlService.IsDemoAccountAsync(User, cancellationToken));
        await _clusterRepository.UpsertClusterMembershipAsync(email, clusterId, role, cancellationToken);
        await AuditUserSavedAsync("Cluster user created.", email, displayName, clusterId, role, true, cancellationToken);
        TempData["StatusMessage"] = "User added.";
        return Redirect(back);
    }

    private Task AuditUserSavedAsync(string message, string email, string? name, string? clusterId, string? role, bool passwordChanged, CancellationToken cancellationToken)
    {
        var metadata = new BsonDocument { ["email"] = email, ["passwordChanged"] = passwordChanged };
        if (name is not null) metadata["name"] = name;
        if (clusterId is not null) metadata["clusterId"] = clusterId;
        if (role is not null) metadata["role"] = role;
        return AppendClusterAdminAuditAsync("cluster.user.saved", message, metadata, "user", email, cancellationToken);
    }

    [HttpPost("users/delete-membership")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserMembership(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email");
        var clusterId = Text(Request.Form, "clusterId");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return Redirect($"/cluster-admin/users{(string.IsNullOrWhiteSpace(email) ? string.Empty : $"?openUser={Uri.EscapeDataString(email)}")}");
        }

        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }

        var rights = await _accessControlService.RightsOverAccountAsync(User, email, cancellationToken);
        if (rights.Refusal is { } refusal)
        {
            TempData["ErrorMessage"] = refusal;
            return Redirect($"/cluster-admin/users?openUser={Uri.EscapeDataString(email)}");
        }

        await _clusterRepository.DeleteClusterMembershipAsync(email, clusterId, cancellationToken);
        TempData["StatusMessage"] = await _clusterRepository.MembershipRemovedMessageAsync(email, clusterId, cancellationToken);
        await AppendClusterAdminAuditAsync(
            "cluster.user.membership.deleted",
            "Cluster user membership deleted.",
            new BsonDocument
            {
                ["email"] = email,
                ["clusterId"] = clusterId
            },
            "user",
            email,
            cancellationToken);
        return Redirect($"/cluster-admin/users?openUser={Uri.EscapeDataString(email)}");
    }

    private async Task<bool> ValidateClusterTokenScopeAsync(string tokenId, CancellationToken cancellationToken)
    {
        var token = await _externalApiRepository.GetTokenByIdAsync(tokenId, cancellationToken);
        if (token == null)
        {
            return false;
        }

        var managedClusterIds = await ManagedClusterIdsAsync(cancellationToken);
        return TokenIsInManagedScope(token, managedClusterIds);
    }

    private async Task<bool> CanAdministerAllClustersAsync(IEnumerable<string> clusterIds, CancellationToken cancellationToken)
    {
        foreach (var clusterId in clusterIds)
        {
            if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private bool TokenIsInManagedScope(BsonDocument token, IReadOnlySet<string> managedClusterIds)
    {
        if (AccessControlService.IsAdmin(User))
        {
            return true;
        }

        if (token.GetValue("globalAccess", false).ToBoolean()
            || token.GetValue("allowUnassigned", false).ToBoolean())
        {
            return false;
        }

        var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
        var clusterIds = tokenClusterIds
            .Select(entry => entry.ToString() ?? string.Empty)
            .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
            .ToList();
        return clusterIds.Count > 0
            && clusterIds.All(clusterId => managedClusterIds.Contains(clusterId));
    }

    private static ApiTokenViewModel MapApiToken(BsonDocument token, IReadOnlyDictionary<string, string> clusterNamesById)
    {
        var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
        var clusterNames = tokenClusterIds
            .Select(entry => entry.ToString() ?? string.Empty)
            .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
            .Select(clusterId => ClusterTokenScopeLabel(clusterId, clusterNamesById))
            .ToList();
        return new ApiTokenViewModel
        {
            TokenId = BsonHelpers.GetString(token, "tokenId"),
            Name = BsonHelpers.GetString(token, "name"),
            AccessMode = BsonHelpers.GetString(token, "accessMode"),
            GlobalAccess = token.GetValue("globalAccess", false).ToBoolean(),
            AllowUnassigned = token.GetValue("allowUnassigned", false).ToBoolean(),
            IsActive = token.GetValue("isActive", false).ToBoolean(),
            IsSample = token.GetValue("isSample", false).ToBoolean(),
            ClusterIdsLabel = clusterNames.Count == 0 ? "No clusters" : string.Join(", ", clusterNames),
            ClusterScopeNames = clusterNames,
            CreatedAt = BsonHelpers.GetString(token, "createdAt"),
            UpdatedAt = BsonHelpers.GetString(token, "updatedAt"),
            LastUsedAt = BsonHelpers.GetString(token, "lastUsedAt")
        };
    }

    private static string ClusterTokenScopeLabel(string clusterId, IReadOnlyDictionary<string, string> clusterNamesById)
    {
        return clusterNamesById.TryGetValue(clusterId, out var clusterName) && !string.IsNullOrWhiteSpace(clusterName)
            ? clusterName
            : clusterId;
    }

    private static ExternalTokenAccessMode ParseTokenMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "readwritesign" => ExternalTokenAccessMode.Lifecycle,
            "lifecycle" => ExternalTokenAccessMode.Lifecycle,
            "sign" => ExternalTokenAccessMode.Sign,
            "readwrite" => ExternalTokenAccessMode.ReadWrite,
            _ => ExternalTokenAccessMode.Read
        };
    }

    private async Task<HashSet<string>> ManagedClusterIdsAsync(CancellationToken cancellationToken)
    {
        if (AccessControlService.IsAdmin(User))
        {
            var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
            return clusters
                .Select(cluster => BsonHelpers.GetString(cluster, "clusterId"))
                .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var managedClusterIds = await _accessControlService.GetAdministeredClusterIdsForUserAsync(User, cancellationToken);
        return managedClusterIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ClusterViewModel> BuildClusterViewModels(IEnumerable<BsonDocument> clusters) =>
        clusters
            .Select(cluster => new ClusterViewModel
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name"),
                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
            })
            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Text(IFormCollection form, string key, string? fallback = "")
    {
        var value = form[key].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }

    private string CurrentActor()
    {
        var email = AccessControlService.CurrentEmail(User);
        return string.IsNullOrWhiteSpace(email) ? "cluster-admin" : email;
    }

    private Task AppendClusterAdminAuditAsync(
        string eventType,
        string message,
        BsonDocument? metadata,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        return _applicationAuditService.AppendApplicationAuditEventAsync(
            eventType,
            CurrentActor(),
            "clusterAdmin",
            "cluster-admin-ui",
            message,
            metadata,
            entityType,
            entityId,
            cancellationToken: cancellationToken);
    }
}
