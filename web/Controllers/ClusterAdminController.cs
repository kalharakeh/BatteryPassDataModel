using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BatteryPassWeb.Controllers;

[Authorize(Policy = "ClusterAdminOrAdmin")]
[Route("cluster-admin")]
public class ClusterAdminController : Controller
{
    private readonly PassportRepository _passportRepository;
    private readonly BatteryRepository _batteryRepository;
    private readonly BatteryPassportSnapshotService _batteryPassportSnapshotService;
    private readonly BatteryPassportDeltaService _batteryPassportDeltaService;
    private readonly ClusterRepository _clusterRepository;
    private readonly BatteryTableService _batteryTableService;
    private readonly PassportViewModelFactory _viewModelFactory;
    private readonly AccessControlService _accessControlService;
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly EditableFieldPolicyService _editableFieldPolicyService;
    private readonly LocalAdminEditableFieldPolicyService _localAdminEditableFieldPolicyService;
    private readonly PassportTrustWorkflowService _passportTrustWorkflowService;

    public ClusterAdminController(
        PassportRepository passportRepository,
        BatteryRepository batteryRepository,
        BatteryPassportSnapshotService batteryPassportSnapshotService,
        BatteryPassportDeltaService batteryPassportDeltaService,
        ClusterRepository clusterRepository,
        BatteryTableService batteryTableService,
        PassportViewModelFactory viewModelFactory,
        AccessControlService accessControlService,
        ExternalApiRepository externalApiRepository,
        EditableFieldPolicyService editableFieldPolicyService,
        LocalAdminEditableFieldPolicyService localAdminEditableFieldPolicyService,
        PassportTrustWorkflowService passportTrustWorkflowService)
    {
        _passportRepository = passportRepository;
        _batteryRepository = batteryRepository;
        _batteryPassportSnapshotService = batteryPassportSnapshotService;
        _batteryPassportDeltaService = batteryPassportDeltaService;
        _clusterRepository = clusterRepository;
        _batteryTableService = batteryTableService;
        _viewModelFactory = viewModelFactory;
        _accessControlService = accessControlService;
        _externalApiRepository = externalApiRepository;
        _editableFieldPolicyService = editableFieldPolicyService;
        _localAdminEditableFieldPolicyService = localAdminEditableFieldPolicyService;
        _passportTrustWorkflowService = passportTrustWorkflowService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return Redirect("/cluster-admin/passports");
    }

    [HttpGet("passports")]
    public async Task<IActionResult> Passports([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var model = await _batteryTableService.BuildAsync(User, BatteryTableScope.ClusterAdminPassports, q, cancellationToken);
        if (!string.IsNullOrWhiteSpace(model.RedirectPath))
        {
            return Redirect(model.RedirectPath);
        }

        return View(model);
    }

    [HttpPost("batteries/{batteryId}/passports/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBatteryPassport(string batteryId, CancellationToken cancellationToken)
    {
        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
        var battery = await _batteryRepository.GetByBatteryIdAsync(decodedBatteryId, cancellationToken);
        if (battery == null)
        {
            return NotFound();
        }

        var clusterId = BsonHelpers.GetString(battery, "clusterId");
        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return Forbid();
        }

        if (!await CanCreateBatteryPassport(battery, cancellationToken))
        {
            TempData["ErrorMessage"] = "No new passport is needed for this battery because the latest snapshot still matches the battery data.";
            return Redirect($"/cluster-admin/batteries/{Uri.EscapeDataString(decodedBatteryId)}/passports?returnUrl={Uri.EscapeDataString("/cluster-admin/passports")}");
        }

        var passport = await _batteryPassportSnapshotService.CreatePassportSnapshotAsync(
            battery,
            CurrentActor(),
            DateTimeOffset.UtcNow,
            cancellationToken);
        var passportId = BsonHelpers.GetString(passport, "passportId");
        await _batteryPassportDeltaService.ClearNewPassportRequiredAsync(decodedBatteryId, passportId, cancellationToken);
        TempData["StatusMessage"] = $"Passport {passportId} created for battery {decodedBatteryId}.";
        return Redirect($"/cluster-admin/passports?q={Uri.EscapeDataString(decodedBatteryId)}");
    }

    [HttpGet("batteries/{batteryId}/passports")]
    public async Task<IActionResult> BatteryPassports(string batteryId, [FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
        var battery = await _batteryRepository.GetByBatteryIdAsync(decodedBatteryId, cancellationToken);
        if (battery == null)
        {
            return NotFound();
        }

        var clusterId = BsonHelpers.GetString(battery, "clusterId");
        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }

        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = clusters
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        var passports = await _passportRepository.ListByBatteryIdAsync(decodedBatteryId, includeArchived: false, cancellationToken);
        var history = passports.Select(ToHistoryRow).ToList();
        var batterySummary = _batteryRepository.ToSummary(battery, history, ResolveClusterLabel(battery, clusterNamesById));
        var safeReturnUrl = SafeReturnUrl(returnUrl, "/cluster-admin/passports");

        return View("BatteryPassports", new BatteryPassportHistoryPageViewModel
        {
            Battery = batterySummary,
            CanCreatePassport = CanCreateBatteryPassport(batterySummary, passports),
            StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
            ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty,
            ReturnUrl = safeReturnUrl,
            ReturnLabel = safeReturnUrl.StartsWith("/cluster-admin/passports", StringComparison.OrdinalIgnoreCase)
                ? "Back to Managed Passports"
                : "Back"
        });
    }

    [HttpGet("passports/{passportId}/edit")]
    public async Task<IActionResult> EditPassport(string passportId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var clusterId = BsonHelpers.GetString(document, "clusterId");
        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }
        if (!AccessControlService.IsAdmin(User) && !IsLatestForBattery(document))
        {
            return Forbid();
        }
        if (!await _accessControlService.CanEditLatestBatteryPassportAsync(User, document, cancellationToken))
        {
            return Forbid();
        }

        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = clusters
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);

        var editablePolicy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
        var model = new Models.ViewModels.EditPassportViewModel
        {
            Passport = _viewModelFactory.Create(document, clusterNamesById),
            Mode = "cluster-edit",
            FieldEditableByKey = BuildEditableFieldDictionary(editablePolicy),
            FieldVisibleByKey = BuildVisibleFieldDictionary(editablePolicy),
            StatusMessage = status switch
            {
                "saved" => "Local passport fields saved.",
                "validated" => "Passport validation completed.",
                "signed" => "Passport signed and immutable revision recorded.",
                "published" => "Passport published from the latest verified revision.",
                _ => string.Empty
            },
            ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error)
        };

        return View(model);
    }

    [HttpPost("passports/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePassport(CancellationToken cancellationToken)
    {
        var passportId = Text(Request.Form, "passportId");
        if (string.IsNullOrWhiteSpace(passportId))
        {
            return Redirect("/cluster-admin/passports");
        }

        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return Redirect("/cluster-admin/passports");
        }

        var clusterId = BsonHelpers.GetString(document, "clusterId");
        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }
        if (!AccessControlService.IsAdmin(User) && !IsLatestForBattery(document))
        {
            return Forbid();
        }
        if (!await _accessControlService.CanEditLatestBatteryPassportAsync(User, document, cancellationToken))
        {
            return Forbid();
        }

        var editablePolicy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
        var editableFieldKeys = editablePolicy.PermissionByKey.Values
            .Where(permission => permission.EditableByLocalAdmin)
            .Select(permission => permission.FieldKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ApplyLocalPassportForm(document, Request.Form, DateTime.UtcNow.ToString("O"), editableFieldKeys);
        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        return Redirect($"/cluster-admin/passports/{Uri.EscapeDataString(passportId)}/edit?status=saved");
    }

    [HttpPost("passports/{passportId}/validate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidatePassport(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        if (!await _accessControlService.CanEditLatestBatteryPassportAsync(User, document, cancellationToken))
        {
            return Forbid();
        }

        var result = await _passportTrustWorkflowService.ValidateAsync(
            passportId,
            CurrentActor(),
            "cluster-admin-ui",
            cancellationToken);
        return Redirect(ClusterPassportEditRedirect(passportId, result.Success ? "validated" : string.Empty, result.Success ? string.Empty : result.Message));
    }

    [HttpPost("passports/{passportId}/sign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignPassport(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        if (!await _accessControlService.CanEditLatestBatteryPassportAsync(User, document, cancellationToken))
        {
            return Forbid();
        }

        var result = await _passportTrustWorkflowService.SignAsync(
            passportId,
            CurrentActor(),
            "cluster-admin-ui",
            cancellationToken);
        var status = result.Success ? result.AutoPublished ? "published" : "signed" : string.Empty;
        return Redirect(ClusterPassportEditRedirect(passportId, status, result.Success ? string.Empty : result.Message));
    }

    [HttpPost("passports/{passportId}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishPassport(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        if (!await _accessControlService.CanEditLatestBatteryPassportAsync(User, document, cancellationToken))
        {
            return Forbid();
        }

        var result = await _passportTrustWorkflowService.PublishAsync(
            passportId,
            CurrentActor(),
            "cluster-admin-ui",
            cancellationToken);
        return Redirect(ClusterPassportEditRedirect(passportId, result.Success ? "published" : string.Empty, result.Success ? string.Empty : result.Message));
    }

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

        ViewData["ErrorMessage"] = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error);
        return View(model);
    }

    [HttpGet("secrets")]
    public IActionResult Secrets()
    {
        return Redirect("/cluster-admin/passports");
    }

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
        var accessMode = ParseTokenMode(Text(Request.Form, "accessMode", "read"));
        var actor = AccessControlService.CurrentEmail(User);
        var (_, tokenValue) = await _externalApiRepository.CreateTokenAsync(
            name,
            accessMode,
            clusterIds,
            allowUnassigned: false,
            globalAccess: false,
            actor: string.IsNullOrWhiteSpace(actor) ? "cluster-admin" : actor,
            cancellationToken: cancellationToken);
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
        TempData["StatusMessage"] = "API token deleted.";
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
        }

        return Redirect("/cluster-admin/api-tokens");
    }

    [HttpPost("users/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email").Trim().ToLowerInvariant();
        var clusterId = Text(Request.Form, "clusterId");
        var role = Text(Request.Form, "role", "member");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return Redirect($"/cluster-admin/users{(string.IsNullOrWhiteSpace(email) ? string.Empty : $"?openUser={Uri.EscapeDataString(email)}")}");
        }

        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }

        var currentEmail = AccessControlService.CurrentEmail(User).Trim().ToLowerInvariant();
        if (!AccessControlService.IsAdmin(User) && email.Equals(currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Cannot change your own local admin role.")}&openUser={Uri.EscapeDataString(email)}");
        }

        var existingUser = await _clusterRepository.GetUserByEmailAsync(email, cancellationToken);
        var existingRoles = existingUser?.GetValue("roles", new BsonArray()) is BsonArray roleArray
            ? roleArray
                .Select(entry => entry.ToString())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry!)
                .ToList()
            : [];
        if (!AccessControlService.IsAdmin(User) && existingRoles.Contains(AccessControlService.RoleAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Local admins cannot modify global admin users.")}&openUser={Uri.EscapeDataString(email)}");
        }

        var password = Text(Request.Form, "password");
        var passwordConfirmation = Text(Request.Form, "passwordConfirmation");
        if (!string.IsNullOrWhiteSpace(password) && !password.Equals(passwordConfirmation, StringComparison.Ordinal))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Passwords do not match.")}&openUser={Uri.EscapeDataString(email)}");
        }

        if (existingUser == null && string.IsNullOrWhiteSpace(password))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Password is required for new users.")}&openUser={Uri.EscapeDataString(email)}");
        }

        var roles = existingRoles.Where(existingRole => !existingRole.Equals(AccessControlService.RoleAdmin, StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (roles.Count == 0)
        {
            roles.Add(AccessControlService.RoleNormalUser);
        }

        var displayName = Text(Request.Form, "name", email);
        var passwordHash = string.IsNullOrWhiteSpace(password) ? string.Empty : BCryptNet.HashPassword(password);
        await _clusterRepository.UpsertUserAsync(email, displayName, roles.ToList(), passwordHash, cancellationToken);
        await _clusterRepository.UpsertClusterMembershipAsync(email, clusterId, role, cancellationToken);
        return Redirect($"/cluster-admin/users?openUser={Uri.EscapeDataString(email)}");
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

        var currentEmail = AccessControlService.CurrentEmail(User).Trim().ToLowerInvariant();
        if (!AccessControlService.IsAdmin(User) && email.Trim().ToLowerInvariant().Equals(currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Cannot change your own local admin role.")}&openUser={Uri.EscapeDataString(email)}");
        }

        await _clusterRepository.DeleteClusterMembershipAsync(email, clusterId, cancellationToken);
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

    private async Task<bool> CanCreateBatteryPassport(BsonDocument battery, CancellationToken cancellationToken)
    {
        var batteryId = BsonHelpers.GetString(battery, "batteryId");
        var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: false, cancellationToken);
        return CanCreateBatteryPassport(
            _batteryRepository.ToSummary(battery, passports.Select(ToHistoryRow).ToList(), string.Empty),
            passports);
    }

    private static bool CanCreateBatteryPassport(BatterySummaryViewModel battery, IReadOnlyList<BsonDocument> passports)
    {
        if (passports.Count == 0 || battery.PassportCount == 0)
        {
            return true;
        }

        var latest = passports.FirstOrDefault(passport => passport.GetValue("isLatestForBattery", false).ToBoolean())
            ?? passports.FirstOrDefault();
        return battery.NewPassportRequired && latest != null && !IsDraftPassport(latest);
    }

    private static bool IsDraftPassport(BsonDocument passport)
    {
        var status = BsonHelpers.GetString(passport, "registryInfo", "status");
        return status.Contains("draft", StringComparison.OrdinalIgnoreCase)
            || status.Contains("awaiting", StringComparison.OrdinalIgnoreCase);
    }

    private static BatteryPassportHistoryRowViewModel ToHistoryRow(BsonDocument passport)
    {
        return new BatteryPassportHistoryRowViewModel
        {
            PassportId = BsonHelpers.GetString(passport, "passportId"),
            BatteryId = BsonHelpers.GetString(passport, "batteryId"),
            CreatedAt = BsonHelpers.GetString(passport, "snapshot", "createdAt"),
            PassportStatus = PassportRepository.BuildPassportStatusLabel(passport),
            IsLatestForBattery = passport.GetValue("isLatestForBattery", false).ToBoolean(),
            IsPubliclyVisible = true
        };
    }

    private static string ResolveClusterLabel(BsonDocument battery, IReadOnlyDictionary<string, string> clusterNamesById)
    {
        var clusterId = BsonHelpers.GetString(battery, "clusterId");
        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return "No cluster assigned";
        }

        return clusterNamesById.TryGetValue(clusterId, out var clusterName) && !string.IsNullOrWhiteSpace(clusterName)
            ? clusterName
            : clusterId;
    }

    private static string SafeReturnUrl(string? returnUrl, string fallback)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/", StringComparison.Ordinal))
        {
            return fallback;
        }

        if (returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.Contains("://", StringComparison.Ordinal))
        {
            return fallback;
        }

        return returnUrl;
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

    private static IReadOnlyDictionary<string, bool> BuildVisibleFieldDictionary(EditableFieldPolicySnapshot policy)
    {
        return policy.PermissionByKey.Values
            .GroupBy(permission => permission.FieldKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().VisibleToClusterAdmin,
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, bool> BuildEditableFieldDictionary(EditableFieldPolicySnapshot policy)
    {
        return policy.PermissionByKey.Values
            .GroupBy(permission => permission.FieldKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().EditableByLocalAdmin,
                StringComparer.OrdinalIgnoreCase);
    }

    private static void ApplyLocalPassportForm(BsonDocument document, IFormCollection form, string now, IReadOnlySet<string> editableFieldKeys)
    {
        var app = EnsureDocument(document, "app");
        var display = EnsureDocument(app, "display");
        var media = EnsureDocument(app, "media");
        var aspects = EnsureDocument(document, "aspects");

        var generalAspect = EnsureDocument(aspects, "generalProductInformation");
        var generalPayload = EnsureDocument(generalAspect, "payload");
        generalPayload["batteryCategory"] = BatteryPassCanonicalDataCatalog.NormalizeBatteryCategory(
            BsonHelpers.GetString(generalPayload, "batteryCategory"));
        var performanceAspect = EnsureDocument(aspects, "performanceAndDurability");
        var performancePayload = EnsureDocument(performanceAspect, "payload");
        var batteryCondition = EnsureDocument(performancePayload, "batteryCondition");

        if (editableFieldKeys.Contains("general.facilityId"))
        {
            var facilityId = Text(form, "facilityId", display.GetValue("facilityId", string.Empty).ToString());
            display["facilityId"] = facilityId;
            var manufacturingPlace = EnsureDocument(generalPayload, "manufacturingPlace");
            manufacturingPlace["streetAddress"] = facilityId;
            var manufacturerInformation = EnsureDocument(generalPayload, "manufacturerInformation");
            var postalAddress = EnsureDocument(manufacturerInformation, "postalAddress");
            postalAddress["streetAddress"] = facilityId;
        }

        if (editableFieldKeys.Contains("general.batteryImageUrl"))
        {
            var batteryImageUrl = BatteryImageCatalog.NormalizeKnownImageUrl(
                Text(form, "batteryImageUrl", media.GetValue("batteryImageUrl", BatteryImageCatalog.DefaultImageUrl).ToString()),
                BsonHelpers.GetString(document, "passportId"));
            media["batteryImageUrl"] = batteryImageUrl;
            media["batteryImageAlt"] = $"Industrial battery pack for passport {display.GetValue("modelNumber", string.Empty)}";
        }

        if (editableFieldKeys.Contains("performance.stateOfCharge"))
        {
            var stateOfCharge = ClampNumber(Number(form, "stateOfCharge", NumberAtDocument(batteryCondition, "stateOfCharge", "stateOfChargeValue")), 0, 100);
            batteryCondition["stateOfCharge"] = new BsonDocument
        {
            ["stateOfChargeValue"] = stateOfCharge,
            ["lastUpdate"] = now
        };
        }

        if (editableFieldKeys.Contains("performance.remainingCapacity"))
        {
            var remainingCapacity = ClampNumber(Number(form, "remainingCapacity", NumberAtDocument(batteryCondition, "remainingCapacity", "remainingCapacityValue")), 0, 100);
            batteryCondition["remainingCapacity"] = new BsonDocument
        {
            ["remainingCapacityValue"] = remainingCapacity,
            ["lastUpdate"] = now
        };
        }

        if (editableFieldKeys.Contains("performance.remainingEnergy"))
        {
            var remainingEnergy = Math.Max(0, Number(form, "remainingEnergy", NumberAtDocument(batteryCondition, "remainingEnergy", "remainingEnergyValue")));
            batteryCondition["remainingEnergy"] = new BsonDocument
        {
            ["remainingEnergyValue"] = remainingEnergy,
            ["lastUpdate"] = now
        };
        }

        if (editableFieldKeys.Contains("performance.fullCycles"))
        {
            var fullCycles = Math.Max(0, Math.Truncate(Number(form, "fullCycles", NumberAtDocument(batteryCondition, "numberOfFullCycles", "numberOfFullCyclesValue"))));
            batteryCondition["numberOfFullCycles"] = new BsonDocument
        {
            ["numberOfFullCyclesValue"] = fullCycles,
            ["lastUpdate"] = now
        };
        }

        var registryInfo = EnsureDocument(document, "registryInfo");
        registryInfo["updatedAt"] = now;
    }

    private static BsonDocument EnsureDocument(BsonDocument parent, string key)
    {
        if (!parent.TryGetValue(key, out var value) || value is not BsonDocument document)
        {
            document = new BsonDocument();
            parent[key] = document;
        }

        return document;
    }

    private static string Text(IFormCollection form, string key, string? fallback = "")
    {
        var value = form[key].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }

    private static bool IsLatestForBattery(BsonDocument passport) =>
        passport.GetValue("isLatestForBattery", false).ToBoolean();

    private static string ClusterPassportEditRedirect(string passportId, string status = "", string error = "")
    {
        var url = $"/cluster-admin/passports/{Uri.EscapeDataString(passportId)}/edit";
        if (!string.IsNullOrWhiteSpace(status))
        {
            return $"{url}?status={Uri.EscapeDataString(status)}";
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            return $"{url}?error={Uri.EscapeDataString(error)}";
        }

        return url;
    }

    private string CurrentActor()
    {
        var email = AccessControlService.CurrentEmail(User);
        return string.IsNullOrWhiteSpace(email) ? "cluster-admin" : email;
    }

    private static double Number(IFormCollection form, string key, double fallback)
    {
        var text = Text(form, key);
        return double.TryParse(text, out var parsed) ? parsed : fallback;
    }

    private static double ClampNumber(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static double NumberAtDocument(BsonDocument parent, string key, string nestedKey)
    {
        if (!parent.TryGetValue(key, out var value) || value is not BsonDocument document)
        {
            return 0;
        }

        var nestedValue = document.GetValue(nestedKey, 0);
        return nestedValue.IsNumeric ? nestedValue.ToDouble() : 0;
    }
}
