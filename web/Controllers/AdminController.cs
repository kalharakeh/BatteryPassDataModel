using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Globalization;
using System.Text.Json;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BatteryPassWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly ClusterRepository _clusterRepository;
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly ApplicationAuditService _applicationAuditService;
    private readonly ApplicationSettingsService _applicationSettingsService;
    private readonly DppStore _dppStore;
    private readonly DemoDataService _demoData;

    public AdminController(
        ClusterRepository clusterRepository,
        ExternalApiRepository externalApiRepository,
        ApplicationAuditService applicationAuditService,
        ApplicationSettingsService applicationSettingsService,
        DppStore dppStore,
        DemoDataService demoData)
    {
        _clusterRepository = clusterRepository;
        _externalApiRepository = externalApiRepository;
        _applicationAuditService = applicationAuditService;
        _applicationSettingsService = applicationSettingsService;
        _dppStore = dppStore;
        _demoData = demoData;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return Redirect("/admin/batteries");
    }

    /// <summary>The team's help moved into the one Help area (decision 30).</summary>
    [HttpGet("help")]
    public IActionResult Help() => Redirect("/help/team");

    [HttpGet("audit")]
    public async Task<IActionResult> AuditLog(
        [FromQuery] string? q,
        [FromQuery] string? entityType,
        [FromQuery] string? category,
        [FromQuery] string? actor,
        [FromQuery] string? source,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? show,
        [FromServices] AuditLines auditLines,
        CancellationToken cancellationToken)
    {
        // 50 events at a time; one more is read to know whether "Show 50 more" has anything to show.
        const int PageSize = 50;
        var shown = Math.Clamp(show ?? PageSize, PageSize, 1000);
        var filter = new ApplicationAuditFilter(
            Search: q ?? string.Empty,
            EntityType: entityType ?? string.Empty,
            Category: category ?? string.Empty,
            Actor: actor ?? string.Empty,
            Source: source ?? string.Empty,
            From: from ?? string.Empty,
            To: to ?? string.Empty,
            Limit: shown + 1);
        var events = await _applicationAuditService.ListApplicationAuditEventsAsync(filter, cancellationToken);
        var lines = await auditLines.DescribeAsync(events.Take(shown).ToArray(), cancellationToken);
        return View("AuditLog", new ApplicationAuditLogViewModel
        {
            Filter = filter,
            Events = events,
            Days = AuditLines.ByDay(lines, auditLines.Zone, DateTimeOffset.UtcNow),
            Show = shown,
            HasMore = events.Count > shown,
            Zone = auditLines.Zone,
        });
    }

    [HttpGet("clusters")]
    public async Task<IActionResult> Clusters([FromQuery] string? tab, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        // The batteries and their cluster assignments moved to the batteries pages (plan 2d-2).
        if (NormalizeTab(tab) is "batteries" or "battery")
        {
            return Redirect(string.IsNullOrWhiteSpace(q) ? "/admin/batteries" : $"/admin/batteries?q={Uri.EscapeDataString(q)}");
        }

        // The battery families moved to their own pages, and the attribute catalogue replaced the editable-fields tab.
        if (tab?.Equals("products", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Redirect("/admin/families");
        }

        if (tab?.Equals("local-editable-fields", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Redirect("/admin/clusters");
        }

        var selectedTab = NormalizeTab(tab);
        var selectedCredentialTab = NormalizeCredentialTab(tab);
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterViewModels = clusters
            .Select(cluster => new ClusterViewModel
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name"),
                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var clusterNamesById = clusterViewModels.ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        var needsUsers = selectedTab is "users" or "clusters";
        var needsCredentials = selectedTab == "api-token-management";
        IReadOnlyList<BsonDocument> users = needsUsers
            ? await _clusterRepository.ListUsersAsync(cancellationToken)
            : Array.Empty<BsonDocument>();
        IReadOnlyList<BsonDocument> memberships = needsUsers
            ? await _clusterRepository.ListClusterMembershipsAsync(cancellationToken)
            : Array.Empty<BsonDocument>();
        IReadOnlyList<BsonDocument> apiTokens = needsCredentials
            ? await _externalApiRepository.ListTokensAsync(cancellationToken)
            : Array.Empty<BsonDocument>();

        var model = new AdminClusterViewModel
        {
            SelectedTab = selectedTab,
            SelectedCredentialTab = selectedCredentialTab,
            Clusters = clusterViewModels,
            Users = users.Select(user => new UserViewModel
            {
                Email = BsonHelpers.GetString(user, "email"),
                Name = BsonHelpers.GetString(user, "name"),
                Roles = (user.GetValue("roles", new BsonArray()) as BsonArray ?? new BsonArray())
                    .Select(role => role.ToString() ?? string.Empty)
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Select(AccessControlService.DisplayRoleLabel)
                    .ToList()
            }).ToList(),
            Memberships = memberships.Select(membership => new ClusterMembershipViewModel
            {
                Email = BsonHelpers.GetString(membership, "email"),
                ClusterId = BsonHelpers.GetString(membership, "clusterId"),
                Role = BsonHelpers.GetString(membership, "role"),
                CreatedAt = BsonHelpers.GetString(membership, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(membership, "updatedAt")
            }).ToList(),
            ApiTokens = apiTokens.Select(token =>
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
            }).OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
            ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty,
            GeneratedCredential = TempData["GeneratedCredential"]?.ToString() ?? string.Empty,
            GeneratedCredentials = TempData["GeneratedCredentials"] is string tokens ? JsonSerializer.Deserialize<List<DemoToken>>(tokens) ?? [] : [],
            HasDemoData = await _demoData.HasDemoDataAsync(cancellationToken),
            DemoAccountsPossible = _demoData.AccountsPossible,
            SessionTimeoutMinutes = await _applicationSettingsService.GetSessionTimeoutMinutesAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpPost("clusters/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCluster(CancellationToken cancellationToken)
    {
        var name = Text(Request.Form, "name");
        var clusterId = Text(Request.Form, "clusterId");
        if (string.IsNullOrWhiteSpace(name))
        {
            return Redirect("/admin/clusters?tab=clusters");
        }

        if (string.IsNullOrWhiteSpace(clusterId))
        {
            clusterId = $"cluster-{Guid.NewGuid():N}";
        }
        clusterId = ClusterRepository.NormalizeClusterId(clusterId);

        await _clusterRepository.EnsureIndexesAsync(cancellationToken);
        if (await _clusterRepository.GetClusterByIdAsync(clusterId, cancellationToken) != null)
        {
            TempData["ErrorMessage"] = "Cluster ID already exists.";
            TempData["ClusterCreateName"] = name;
            TempData["ClusterCreateId"] = clusterId;
            return Redirect("/admin/clusters?tab=clusters");
        }

        try
        {
            await _clusterRepository.UpsertClusterAsync(clusterId, name, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            TempData["ErrorMessage"] = "Cluster ID already exists.";
            TempData["ClusterCreateName"] = name;
            TempData["ClusterCreateId"] = clusterId;
            return Redirect("/admin/clusters?tab=clusters");
        }
        await AppendAdminAuditAsync(
            "cluster.created",
            "Cluster created.",
            new BsonDocument
            {
                ["clusterId"] = clusterId,
                ["name"] = name
            },
            "cluster",
            clusterId,
            cancellationToken);
        return Redirect("/admin/clusters?tab=clusters");
    }

    [HttpPost("clusters/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCluster(CancellationToken cancellationToken)
    {
        var clusterId = Text(Request.Form, "clusterId");
        var name = Text(Request.Form, "name");
        await _clusterRepository.UpdateClusterNameAsync(clusterId, name, cancellationToken);
        await AppendAdminAuditAsync(
            "cluster.updated",
            "Cluster updated.",
            new BsonDocument
            {
                ["clusterId"] = clusterId,
                ["name"] = name
            },
            "cluster",
            clusterId,
            cancellationToken);
        return Redirect("/admin/clusters?tab=clusters");
    }

    [HttpPost("clusters/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCluster(CancellationToken cancellationToken)
    {
        var clusterId = Text(Request.Form, "clusterId");
        var batteryCount = await _dppStore.CountPacksInClusterAsync(clusterId, cancellationToken);
        var membershipCount = await _clusterRepository.CountMembershipsByClusterAsync(clusterId, cancellationToken);
        if (batteryCount > 0 || membershipCount > 0)
        {
            TempData["ErrorMessage"] = $"Cluster {clusterId} has {batteryCount} linked battery(ies) and {membershipCount} linked user membership(s). Use force delete only after confirming linked data removal.";
            return Redirect("/admin/clusters?tab=clusters");
        }

        await _clusterRepository.DeleteClusterAsync(clusterId, cancellationToken);
        await AppendAdminAuditAsync(
            "cluster.deleted",
            "Cluster deleted.",
            new BsonDocument { ["clusterId"] = clusterId },
            "cluster",
            clusterId,
            cancellationToken);
        return Redirect("/admin/clusters?tab=clusters");
    }

    [HttpPost("clusters/force-delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForceDeleteClusterAsync(CancellationToken cancellationToken)
    {
        var clusterId = Text(Request.Form, "clusterId");
        var confirmation = Text(Request.Form, "confirmation");
        if (!confirmation.Equals("I understand this deletes linked cluster data", StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "Type: I understand this deletes linked cluster data";
            return Redirect("/admin/clusters?tab=clusters");
        }

        var batteriesCleared = await _dppStore.ClearClusterAsync(clusterId, cancellationToken);
        await _clusterRepository.DeleteClusterAsync(clusterId, cancellationToken);
        await AppendAdminAuditAsync(
            "cluster.deleted",
            "Cluster force-deleted after linked data was cleared.",
            new BsonDocument
            {
                ["clusterId"] = clusterId,
                ["forceDelete"] = true,
                ["batteriesCleared"] = batteriesCleared
            },
            "cluster",
            clusterId,
            cancellationToken);
        return Redirect("/admin/clusters?tab=clusters");
    }

    [HttpPost("clusters/assign-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUserMembership(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email");
        var clusterId = Text(Request.Form, "clusterId");
        var role = Text(Request.Form, "role", "member");
        await _clusterRepository.UpsertClusterMembershipAsync(email, clusterId, role, cancellationToken);
        await AppendAdminAuditAsync(
            "cluster.user.assigned",
            "User assigned to cluster.",
            new BsonDocument
            {
                ["email"] = email,
                ["clusterId"] = clusterId,
                ["role"] = role
            },
            "cluster",
            clusterId,
            cancellationToken);
        return Redirect($"/admin/clusters?tab=users&openUser={Uri.EscapeDataString(email)}");
    }

    [HttpPost("clusters/save-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Redirect("/admin/clusters?tab=users");
        }

        var displayName = Text(Request.Form, "name", email);
        var password = Text(Request.Form, "password");
        var passwordConfirmation = Text(Request.Form, "passwordConfirmation");
        var requestedSystemRole = Text(Request.Form, "systemRole", "member");
        var existingUser = await _clusterRepository.GetUserByEmailAsync(email, cancellationToken);

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeSystemRole(requestedSystemRole)
        };

        if (!string.IsNullOrWhiteSpace(password) && !password.Equals(passwordConfirmation, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "Passwords do not match.";
            return Redirect($"/admin/clusters?tab=users&openUser={Uri.EscapeDataString(email)}");
        }

        if (existingUser == null && string.IsNullOrWhiteSpace(password))
        {
            TempData["ErrorMessage"] = "Password is required for new users.";
            return Redirect($"/admin/clusters?tab=users&openUser={Uri.EscapeDataString(email)}");
        }

        var passwordHash = string.IsNullOrWhiteSpace(password) ? string.Empty : BCryptNet.HashPassword(password);
        await _clusterRepository.UpsertUserAsync(email, displayName, roles.ToList(), passwordHash, cancellationToken);

        var clusterId = Text(Request.Form, "clusterId");
        if (!string.IsNullOrWhiteSpace(clusterId))
        {
            var membershipRole = Text(Request.Form, "role", "member");
            await _clusterRepository.UpsertClusterMembershipAsync(email, clusterId, membershipRole, cancellationToken);
        }

        await AppendAdminAuditAsync(
            "cluster.user.saved",
            existingUser == null ? "User created." : "User updated.",
            new BsonDocument
            {
                ["email"] = email,
                ["name"] = displayName,
                ["roles"] = new BsonArray(roles.Select(role => (BsonValue)role)),
                ["clusterId"] = clusterId,
                ["passwordChanged"] = !string.IsNullOrWhiteSpace(passwordHash)
            },
            "user",
            email,
            cancellationToken);

        return Redirect($"/admin/clusters?tab=users&openUser={Uri.EscapeDataString(email)}");
    }

    [HttpPost("clusters/session-timeout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSessionTimeout(CancellationToken cancellationToken)
    {
        var requestedTimeout = int.TryParse(Text(Request.Form, "sessionTimeoutMinutes"), out var parsedTimeout)
            ? parsedTimeout
            : BatteryPassOptions.DefaultSessionTimeoutMinutes;
        var actor = AccessControlService.CurrentEmail(User);
        var normalizedTimeout = await _applicationSettingsService.SetSessionTimeoutMinutesAsync(
            requestedTimeout,
            actor,
            cancellationToken);

        await AppendAdminAuditAsync(
            "settings.sessionTimeout.updated",
            "Global session timeout updated.",
            new BsonDocument
            {
                ["sessionTimeoutMinutes"] = normalizedTimeout,
                ["updatedBy"] = actor
            },
            "settings",
            "session-timeout",
            cancellationToken);

        TempData["StatusMessage"] = $"Global session timeout updated to {FormatSessionTimeout(normalizedTimeout)}.";
        return Redirect("/admin/clusters?tab=users");
    }

    [HttpPost("clusters/delete-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserMembership(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email");
        var clusterId = Text(Request.Form, "clusterId");
        await _clusterRepository.DeleteClusterMembershipAsync(email, clusterId, cancellationToken);
        await AppendAdminAuditAsync(
            "cluster.user.membership.deleted",
            "User cluster membership deleted.",
            new BsonDocument
            {
                ["email"] = email,
                ["clusterId"] = clusterId
            },
            "user",
            email,
            cancellationToken);
        return Redirect($"/admin/clusters?tab=users&openUser={Uri.EscapeDataString(email)}");
    }

    /// <summary>
    /// Deletes an account and its cluster memberships (decision 30). Your own account and the last team account stay,
    /// so the team can always sign in.
    /// </summary>
    [HttpPost("clusters/delete-account")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email").Trim().ToLowerInvariant();
        var back = $"/admin/clusters?tab=users&openUser={Uri.EscapeDataString(email)}";
        if (email.Length == 0)
        {
            return Redirect("/admin/clusters?tab=users");
        }

        if (string.Equals(email, AccessControlService.CurrentEmail(User), StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "You can't delete your own account.";
            return Redirect(back);
        }

        var account = await _clusterRepository.GetUserByEmailAsync(email, cancellationToken);
        if (account is null)
        {
            TempData["ErrorMessage"] = "No account has this email.";
            return Redirect("/admin/clusters?tab=users");
        }

        static bool IsTeam(BsonDocument user) =>
            user.GetValue("roles", new BsonArray()) is BsonArray roles && roles.Any(role => role.IsString && role.AsString == AccessControlService.RoleAdmin);
        if (IsTeam(account) && (await _clusterRepository.ListUsersAsync(cancellationToken)).Count(IsTeam) <= 1)
        {
            TempData["ErrorMessage"] = "This is the last team account, so it stays.";
            return Redirect(back);
        }

        await _clusterRepository.DeleteUserAsync(email, cancellationToken);
        await AppendAdminAuditAsync(
            "account.deleted",
            "User account deleted with its cluster memberships.",
            new BsonDocument { ["email"] = email },
            "account",
            email,
            cancellationToken);
        TempData["StatusMessage"] = $"The account {email} is deleted.";
        return Redirect("/admin/clusters?tab=users");
    }

    [HttpPost("api/tokens/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateApiToken(CancellationToken cancellationToken)
    {
        var name = Text(Request.Form, "name", "External API token");
        var accessModeText = Text(Request.Form, "accessMode", "read");
        var accessMode = accessModeText.ToLowerInvariant() switch
        {
            "readwritesign" => ExternalTokenAccessMode.Lifecycle,
            "lifecycle" => ExternalTokenAccessMode.Lifecycle,
            "sign" => ExternalTokenAccessMode.Sign,
            "readwrite" => ExternalTokenAccessMode.ReadWrite,
            _ => ExternalTokenAccessMode.Read
        };
        var allowUnassigned = Request.Form["allowUnassigned"].FirstOrDefault()?.Equals("on", StringComparison.OrdinalIgnoreCase) == true;
        var globalAccess = Request.Form["globalAccess"].FirstOrDefault()?.Equals("on", StringComparison.OrdinalIgnoreCase) == true;
        if (globalAccess)
        {
            allowUnassigned = true;
        }
        var clusterIds = Request.Form["clusterIds"]
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        var actor = AccessControlService.CurrentEmail(User);
        var (tokenDocument, tokenValue) = await _externalApiRepository.CreateTokenAsync(
            name,
            accessMode,
            clusterIds,
            allowUnassigned,
            globalAccess,
            string.IsNullOrWhiteSpace(actor) ? "admin" : actor,
            cancellationToken: cancellationToken);

        await AppendAdminAuditAsync(
            "apiToken.created",
            "API token created.",
            new BsonDocument
            {
                ["tokenId"] = BsonHelpers.GetString(tokenDocument, "tokenId"),
                ["name"] = name,
                ["accessMode"] = accessMode.ToString(),
                ["globalAccess"] = globalAccess,
                ["allowUnassigned"] = allowUnassigned,
                ["clusterIds"] = new BsonArray(clusterIds.Select(clusterId => (BsonValue)clusterId))
            },
            "apiToken",
            BsonHelpers.GetString(tokenDocument, "tokenId"),
            cancellationToken);
        TempData["StatusMessage"] = $"API token \"{name}\" created.";
        TempData["GeneratedCredential"] = tokenValue;
        return Redirect("/admin/clusters?tab=api-tokens");
    }

    [HttpPost("api/tokens/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteApiToken(CancellationToken cancellationToken)
    {
        var tokenId = Text(Request.Form, "tokenId");
        var success = await _externalApiRepository.DeleteTokenAsync(tokenId, cancellationToken);
        if (success)
        {
            await AppendAdminAuditAsync(
                "apiToken.deleted",
                "API token deleted.",
                new BsonDocument { ["tokenId"] = tokenId },
                "apiToken",
                tokenId,
                cancellationToken);
        }
        TempData[success ? "StatusMessage" : "ErrorMessage"] = success
            ? $"Token {tokenId} deleted."
            : $"Token {tokenId} was not found.";
        return Redirect("/admin/clusters?tab=api-token-management");
    }

    [HttpPost("api/tokens/cleanup-generated")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CleanupGeneratedApiTokens(CancellationToken cancellationToken)
    {
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = clusters
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        var tokens = await _externalApiRepository.ListTokensAsync(cancellationToken);
        var duplicateTokenIds = GeneratedClusterDuplicateTokenIds(tokens, clusterNamesById);
        var deletedCount = await _externalApiRepository.DeleteTokensAsync(duplicateTokenIds, cancellationToken);
        if (deletedCount > 0)
        {
            await AppendAdminAuditAsync(
                "apiToken.deleted",
                "Generated API token duplicates cleaned up.",
                new BsonDocument
                {
                    ["deletedCount"] = deletedCount,
                    ["tokenIds"] = new BsonArray(duplicateTokenIds.Select(tokenId => (BsonValue)tokenId))
                },
                "apiToken",
                "generated-duplicates",
                cancellationToken);
        }
        TempData[deletedCount > 0 ? "StatusMessage" : "ErrorMessage"] = deletedCount > 0
            ? $"Cleaned up {deletedCount} unused generated token{(deletedCount == 1 ? string.Empty : "s")}."
            : "No unused generated token duplicates were found.";
        return Redirect("/admin/clusters?tab=api-token-management");
    }

    [HttpPost("api/tokens/set-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetApiTokenActive(CancellationToken cancellationToken)
    {
        var tokenId = Text(Request.Form, "tokenId");
        var isActive = Request.Form["isActive"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        var actor = AccessControlService.CurrentEmail(User);
        var success = await _externalApiRepository.SetTokenActiveAsync(tokenId, isActive, string.IsNullOrWhiteSpace(actor) ? "admin" : actor, cancellationToken);
        if (success)
        {
            await AppendAdminAuditAsync(
                "apiToken.updated",
                "API token active state changed.",
                new BsonDocument
                {
                    ["tokenId"] = tokenId,
                    ["isActive"] = isActive
                },
                "apiToken",
                tokenId,
                cancellationToken);
        }
        TempData[success ? "StatusMessage" : "ErrorMessage"] = success
            ? $"Token {tokenId} updated."
            : $"Token {tokenId} was not found.";
        return Redirect("/admin/clusters?tab=api-tokens");
    }

    [HttpPost("api/tokens/regenerate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateApiToken(CancellationToken cancellationToken)
    {
        var tokenId = Text(Request.Form, "tokenId");
        var actor = AccessControlService.CurrentEmail(User);
        var newToken = await _externalApiRepository.RegenerateTokenAsync(tokenId, string.IsNullOrWhiteSpace(actor) ? "admin" : actor, cancellationToken);
        if (string.IsNullOrWhiteSpace(newToken))
        {
            TempData["ErrorMessage"] = $"Token {tokenId} was not found.";
        }
        else
        {
            TempData["StatusMessage"] = $"Token {tokenId} regenerated.";
            TempData["GeneratedCredential"] = newToken;
            await AppendAdminAuditAsync(
                "apiToken.regenerated",
                "API token regenerated.",
                new BsonDocument { ["tokenId"] = tokenId },
                "apiToken",
                tokenId,
                cancellationToken);
        }

        return Redirect("/admin/clusters?tab=api-tokens");
    }

    [HttpPost("api/tokens/generate-per-cluster")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateClusterTokens(CancellationToken cancellationToken)
    {
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var existingTokens = await _externalApiRepository.ListTokensAsync(cancellationToken);
        var actor = AccessControlService.CurrentEmail(User);
        var actorValue = string.IsNullOrWhiteSpace(actor) ? "admin" : actor;
        var generatedCount = 0;
        var skippedCount = 0;

        foreach (var cluster in clusters)
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            var clusterName = BsonHelpers.GetString(cluster, "name");
            if (string.IsNullOrWhiteSpace(clusterId))
            {
                continue;
            }

            if (ExistingGeneratedClusterTokenExists(existingTokens, clusterId, clusterName, "read"))
            {
                skippedCount++;
            }
            else
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - read",
                    ExternalTokenAccessMode.Read,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: actorValue,
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
                generatedCount++;
            }

            if (ExistingGeneratedClusterTokenExists(existingTokens, clusterId, clusterName, "readWrite"))
            {
                skippedCount++;
            }
            else
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - readwrite",
                    ExternalTokenAccessMode.ReadWrite,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: actorValue,
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
                generatedCount++;
            }

            if (ExistingGeneratedClusterTokenExists(existingTokens, clusterId, clusterName, "readWriteSign"))
            {
                skippedCount++;
            }
            else
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - readwritesign",
                    ExternalTokenAccessMode.Lifecycle,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: actorValue,
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
                generatedCount++;
            }
        }

        TempData["StatusMessage"] = generatedCount == 0
            ? $"Per-cluster tokens already exist. Skipped {skippedCount} existing token{(skippedCount == 1 ? string.Empty : "s")}."
            : $"Generated {generatedCount} cluster token{(generatedCount == 1 ? string.Empty : "s")} and skipped {skippedCount} existing token{(skippedCount == 1 ? string.Empty : "s")}.";
        await AppendAdminAuditAsync(
            "apiToken.created",
            "Per-cluster API token generation finished.",
            new BsonDocument
            {
                ["generatedCount"] = generatedCount,
                ["skippedCount"] = skippedCount
            },
            "apiToken",
            "per-cluster-generation",
            cancellationToken);
        return Redirect("/admin/clusters?tab=api-token-management");
    }

    private static string NormalizeTab(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "batteries" => "batteries",
            "battery" => "battery",
            "passports" => "batteries",
            "clusters" => "clusters",
            "users" => "users",
            "api-token-management" or "api-tokens" => "api-token-management",
            _ => "batteries"
        };
    }

    private sealed record GeneratedClusterTokenCandidate(
        string TokenId,
        string ClusterId,
        string AccessMode,
        bool HasUsage,
        bool HasAutoClusterId,
        string UpdatedAt);

    private static string ClusterTokenScopeLabel(string clusterId, IReadOnlyDictionary<string, string> clusterNamesById)
    {
        return clusterNamesById.TryGetValue(clusterId, out var clusterName) && !string.IsNullOrWhiteSpace(clusterName)
            ? clusterName
            : clusterId;
    }

    private static bool ExistingGeneratedClusterTokenExists(
        IReadOnlyList<BsonDocument> existingTokens,
        string clusterId,
        string clusterName,
        string accessMode)
    {
        return existingTokens.Any(token => IsExistingGeneratedClusterToken(token, clusterId, clusterName, accessMode));
    }

    private static bool IsExistingGeneratedClusterToken(
        BsonDocument token,
        string clusterId,
        string clusterName,
        string accessMode)
    {
        if (!token.GetValue("isActive", false).ToBoolean())
        {
            return false;
        }

        var tokenAccessMode = BsonHelpers.GetString(token, "accessMode");
        if (!tokenAccessMode.Equals(accessMode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
        var clusterIds = tokenClusterIds
            .Select(entry => entry.ToString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (clusterIds.Count != 1 || !clusterIds[0].Equals(clusterId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var autoClusterId = BsonHelpers.GetString(token, "autoClusterId");
        var tokenName = BsonHelpers.GetString(token, "name");
        return autoClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase)
            || GeneratedClusterTokenNameMatches(tokenName, clusterName, accessMode);
    }

    private static IReadOnlyList<string> GeneratedClusterDuplicateTokenIds(
        IReadOnlyList<BsonDocument> tokens,
        IReadOnlyDictionary<string, string> clusterNamesById)
    {
        var candidates = tokens
            .Select(token => ToGeneratedClusterTokenCandidate(token, clusterNamesById))
            .Where(candidate => candidate != null)
            .Select(candidate => candidate!)
            .ToList();

        return candidates
            .GroupBy(candidate => $"{candidate.ClusterId}\u001f{candidate.AccessMode}", StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
            {
                var ordered = group
                    .OrderByDescending(candidate => candidate.HasUsage)
                    .ThenByDescending(candidate => candidate.HasAutoClusterId)
                    .ThenByDescending(candidate => candidate.UpdatedAt, StringComparer.Ordinal)
                    .ToList();
                return ordered
                    .Skip(1)
                    .Where(candidate => !candidate.HasUsage)
                    .Select(candidate => candidate.TokenId);
            })
            .ToList();
    }

    private static GeneratedClusterTokenCandidate? ToGeneratedClusterTokenCandidate(
        BsonDocument token,
        IReadOnlyDictionary<string, string> clusterNamesById)
    {
        if (token.GetValue("isSample", false).ToBoolean()
            || token.GetValue("globalAccess", false).ToBoolean()
            || token.GetValue("allowUnassigned", false).ToBoolean())
        {
            return null;
        }

        var accessMode = BsonHelpers.GetString(token, "accessMode");
        if (!accessMode.Equals("read", StringComparison.OrdinalIgnoreCase)
            && !accessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
            && !accessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
        var clusterIds = tokenClusterIds
            .Select(entry => entry.ToString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (clusterIds.Count != 1)
        {
            return null;
        }

        var clusterId = clusterIds[0];
        var autoClusterId = BsonHelpers.GetString(token, "autoClusterId");
        var clusterName = ClusterTokenScopeLabel(clusterId, clusterNamesById);
        var tokenName = BsonHelpers.GetString(token, "name");
        if (!autoClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase)
            && !GeneratedClusterTokenNameMatches(tokenName, clusterName, accessMode))
        {
            return null;
        }

        var tokenId = BsonHelpers.GetString(token, "tokenId");
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return null;
        }

        return new GeneratedClusterTokenCandidate(
            tokenId,
            clusterId,
            accessMode,
            HasUsage: !string.IsNullOrWhiteSpace(BsonHelpers.GetString(token, "lastUsedAt")),
            HasAutoClusterId: autoClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase),
            UpdatedAt: BsonHelpers.GetString(token, "updatedAt"));
    }

    private static bool GeneratedClusterTokenNameMatches(string tokenName, string clusterName, string accessMode)
    {
        var expectedSuffix = accessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase)
            ? "readwritesign"
            : accessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
                ? "readwrite"
                : "read";
        return tokenName.Equals($"{clusterName} - {expectedSuffix}", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCredentialTab(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            _ => "api-tokens"
        };
    }

    private static string NormalizeSystemRole(string role)
    {
        return role.Trim() switch
        {
            AccessControlService.RoleAdmin => AccessControlService.RoleAdmin,
            AccessControlService.RoleNotifiedBody => AccessControlService.RoleNotifiedBody,
            AccessControlService.RoleMarketSurveillanceAuthority => AccessControlService.RoleMarketSurveillanceAuthority,
            AccessControlService.RoleCommission => AccessControlService.RoleCommission,
            AccessControlService.RoleLiRepair => AccessControlService.RoleLiRepair,
            AccessControlService.RoleLiRecycling => AccessControlService.RoleLiRecycling,
            AccessControlService.RoleLiTreatment => AccessControlService.RoleLiTreatment,
            _ => AccessControlService.RoleNormalUser
        };
    }

    private string CurrentActor()
    {
        var actor = AccessControlService.CurrentEmail(User);
        return string.IsNullOrWhiteSpace(actor) ? "admin" : actor;
    }

    /// <summary>Removes the demo data and makes it again (decision 32); the new tokens are shown once.</summary>
    [HttpPost("demo-data/reset")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResetDemoData(CancellationToken cancellationToken) =>
        DemoDataAsync("Demo data reset", () => _demoData.ResetAsync(CurrentActor(), cancellationToken));

    /// <summary>Removes the demo data and keeps the real data (decision 32).</summary>
    [HttpPost("demo-data/clear")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ClearDemoData(CancellationToken cancellationToken) =>
        DemoDataAsync("Demo data cleared", () => _demoData.ClearAsync(CurrentActor(), cancellationToken));

    private async Task<IActionResult> DemoDataAsync(string heading, Func<Task<DemoResult>> run)
    {
        try
        {
            var result = await run();
            var message = string.Join(" ", result.Notes);
            if (result.Done.Count > 0)
            {
                TempData["StatusMessage"] = $"{heading}: {string.Join("; ", result.Done.Select(line => line.TrimEnd('.')))}. {message}".TrimEnd();
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            if (result.Tokens.Count > 0)
            {
                TempData["GeneratedCredentials"] = JsonSerializer.Serialize(result.Tokens);
            }
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = $"The demo data couldn't be made: {exception.Message}";
        }

        return Redirect("/admin/clusters?tab=clusters");
    }

    private Task AppendAdminAuditAsync(
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
            "admin",
            "admin-ui",
            message,
            metadata,
            entityType,
            entityId,
            cancellationToken: cancellationToken);
    }

    private static string Text(IFormCollection form, string key, string? fallback = "")
    {
        var value = form[key].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }

    private static string FormatSessionTimeout(int minutes)
    {
        return minutes % 60 == 0
            ? $"{minutes / 60} {(minutes == 60 ? "hour" : "hours")}"
            : $"{minutes} minutes";
    }

}
