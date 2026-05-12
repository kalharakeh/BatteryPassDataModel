using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace BatteryPassWeb.Controllers;

[Authorize]
[Route("registry")]
public class RegistryController : Controller
{
    private readonly PassportRepository _passportRepository;
    private readonly BatteryRepository _batteryRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly AccessControlService _accessControlService;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;

    public RegistryController(
        PassportRepository passportRepository,
        BatteryRepository batteryRepository,
        ClusterRepository clusterRepository,
        AccessControlService accessControlService,
        PassportPublishPolicyService passportPublishPolicyService)
    {
        _passportRepository = passportRepository;
        _batteryRepository = batteryRepository;
        _clusterRepository = clusterRepository;
        _accessControlService = accessControlService;
        _passportPublishPolicyService = passportPublishPolicyService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = q?.Trim() ?? string.Empty;
        var isAdmin = AccessControlService.IsAdmin(User);
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNameById = clusters
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);

        var batteryDocuments = await _batteryRepository.SearchDocumentsAsync(query, includeArchived: isAdmin, cancellationToken);
        var rows = new List<BatterySummaryViewModel>();
        foreach (var battery in batteryDocuments)
        {
            var batteryId = BsonHelpers.GetString(battery, "batteryId");
            var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: isAdmin, cancellationToken);
            var visible = new List<BatteryPassportHistoryRowViewModel>();
            foreach (var passport in passports)
            {
                if (await _accessControlService.CanOpenPassportSummaryAsync(User, passport, _passportPublishPolicyService, cancellationToken))
                {
                    visible.Add(ToHistoryRow(passport));
                }
            }

            if (visible.Count > 0 || isAdmin)
            {
                rows.Add(_batteryRepository.ToSummary(battery, visible, ResolveClusterLabel(battery, clusterNameById)));
            }
        }

        var exactMatch = rows.FirstOrDefault(row => row.BatteryId.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return Redirect($"/{Uri.EscapeDataString(exactMatch.BatteryId)}");
        }

        ViewData["RegistryScopeLabel"] = isAdmin
            ? "Search and open all registered batteries, including drafts and archived passport records."
            : "Search and open batteries with signed or published passports available to your role.";
        ViewData["RegistryEmptyLabel"] = isAdmin
            ? "No batteries are currently available in the registry."
            : "No signed or published batteries are currently available to your role.";
        return View(rows);
    }

    private BatteryPassportHistoryRowViewModel ToHistoryRow(BsonDocument passport)
    {
        var status = BsonHelpers.GetString(passport, "registryInfo", "status");
        return new BatteryPassportHistoryRowViewModel
        {
            PassportId = BsonHelpers.GetString(passport, "passportId"),
            BatteryId = BsonHelpers.GetString(passport, "batteryId"),
            CreatedAt = BsonHelpers.GetString(passport, "snapshot", "createdAt"),
            PassportStatus = string.IsNullOrWhiteSpace(status) ? "Draft" : char.ToUpperInvariant(status[0]) + status[1..],
            IsLatestForBattery = passport.GetValue("isLatestForBattery", false).ToBoolean(),
            IsPubliclyVisible = _passportPublishPolicyService.IsPubliclyVisible(passport)
        };
    }

    private static string ResolveClusterLabel(BsonDocument battery, IReadOnlyDictionary<string, string> clusterNameById)
    {
        var clusterId = BsonHelpers.GetString(battery, "clusterId");
        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return "No cluster assigned";
        }

        return clusterNameById.TryGetValue(clusterId, out var clusterName) && !string.IsNullOrWhiteSpace(clusterName)
            ? clusterName
            : clusterId;
    }
}
