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
    private readonly ClusterRepository _clusterRepository;
    private readonly PassportViewModelFactory _viewModelFactory;
    private readonly AccessControlService _accessControlService;
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly LocalAdminEditableFieldPolicyService _localAdminEditableFieldPolicyService;

    public ClusterAdminController(
        PassportRepository passportRepository,
        ClusterRepository clusterRepository,
        PassportViewModelFactory viewModelFactory,
        AccessControlService accessControlService,
        ExternalApiRepository externalApiRepository,
        LocalAdminEditableFieldPolicyService localAdminEditableFieldPolicyService)
    {
        _passportRepository = passportRepository;
        _clusterRepository = clusterRepository;
        _viewModelFactory = viewModelFactory;
        _accessControlService = accessControlService;
        _externalApiRepository = externalApiRepository;
        _localAdminEditableFieldPolicyService = localAdminEditableFieldPolicyService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return Redirect("/cluster-admin/passports");
    }

    [HttpGet("passports")]
    public async Task<IActionResult> Passports([FromQuery] string? q, CancellationToken cancellationToken)
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

        var allPassports = await _passportRepository.SearchAsync(q ?? string.Empty, includeArchived: false, cancellationToken);
        var managedClusterIds = await ManagedClusterIdsAsync(cancellationToken);
        var passports = AccessControlService.IsAdmin(User)
            ? allPassports
            : allPassports
                .Where(passport => !string.IsNullOrWhiteSpace(passport.ClusterId) && managedClusterIds.Contains(passport.ClusterId))
                .ToList();

        var model = passports
            .Select(passport => new PassportSummaryViewModel
            {
                PassportId = passport.PassportId,
                DisplayName = passport.DisplayName,
                ModelNumber = passport.ModelNumber,
                ManufacturerName = passport.ManufacturerName,
                SerialNumber = passport.SerialNumber,
                RegistryStatus = passport.RegistryStatus,
                ClusterId = passport.ClusterId,
                ClusterLabel = string.IsNullOrWhiteSpace(passport.ClusterId)
                    ? "No cluster assigned"
                    : clusterNamesById.TryGetValue(passport.ClusterId, out var clusterName)
                        ? clusterName
                        : passport.ClusterId,
                BatteryImageUrl = passport.BatteryImageUrl,
                UpdatedDate = passport.UpdatedDate
            })
            .OrderBy(passport => passport.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ViewData["Query"] = q ?? string.Empty;
        return View(model);
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

        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = clusters
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);

        var editablePolicy = await _localAdminEditableFieldPolicyService.GetPolicyAsync(cancellationToken);
        var model = new Models.ViewModels.EditPassportViewModel
        {
            Passport = _viewModelFactory.Create(document, clusterNamesById),
            Mode = "cluster-edit",
            FieldEditableByKey = BuildEditableFieldDictionary(editablePolicy),
            StatusMessage = status == "saved" ? "Local passport fields saved." : string.Empty,
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

        var editablePolicy = await _localAdminEditableFieldPolicyService.GetPolicyAsync(cancellationToken);
        ApplyLocalPassportForm(document, Request.Form, DateTime.UtcNow.ToString("O"), editablePolicy.EditableFieldKeys.ToHashSet(StringComparer.OrdinalIgnoreCase));
        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        return Redirect($"/cluster-admin/passports/{Uri.EscapeDataString(passportId)}/edit?status=saved");
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

    [HttpPost("users/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email").Trim().ToLowerInvariant();
        var clusterId = Text(Request.Form, "clusterId");
        var role = Text(Request.Form, "role", "member");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return Redirect("/cluster-admin/users");
        }

        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }

        var existingUser = await _clusterRepository.GetUserByEmailAsync(email, cancellationToken);
        var existingRoles = existingUser?.GetValue("roles", new BsonArray()) is BsonArray roleArray
            ? roleArray
                .Select(entry => entry.ToString())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry!)
                .ToList()
            : [];
        if (!AccessControlService.IsAdmin(User) && existingRoles.Contains("admin", StringComparer.OrdinalIgnoreCase))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Local admins cannot modify global admin users.")}");
        }

        var password = Text(Request.Form, "password");
        if (existingUser == null && string.IsNullOrWhiteSpace(password))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Password is required for new users.")}");
        }

        var roles = existingRoles.Where(existingRole => !existingRole.Equals("admin", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (roles.Count == 0)
        {
            roles.Add("viewer");
        }

        var displayName = Text(Request.Form, "name", email);
        var passwordHash = string.IsNullOrWhiteSpace(password) ? string.Empty : BCryptNet.HashPassword(password);
        await _clusterRepository.UpsertUserAsync(email, displayName, roles.ToList(), passwordHash, cancellationToken);
        await _clusterRepository.UpsertClusterMembershipAsync(email, clusterId, role, cancellationToken);
        return Redirect("/cluster-admin/users");
    }

    [HttpPost("users/delete-membership")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserMembership(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email");
        var clusterId = Text(Request.Form, "clusterId");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return Redirect("/cluster-admin/users");
        }

        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }

        await _clusterRepository.DeleteClusterMembershipAsync(email, clusterId, cancellationToken);
        return Redirect("/cluster-admin/users");
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

    private static IReadOnlyDictionary<string, bool> BuildEditableFieldDictionary(LocalAdminEditableFieldPolicySnapshot policy)
    {
        return policy.Sections
            .SelectMany(section => section.Fields)
            .ToDictionary(field => field.FieldKey, field => policy.IsEditable(field.FieldKey), StringComparer.OrdinalIgnoreCase);
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
