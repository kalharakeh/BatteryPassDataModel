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
    private const string ClusterSearchRequiresGlobalAdminMessage = "Cluster search requires global admin access.";

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
        var isClusterAdmin = AccessControlService.IsClusterAdmin(User);
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNameById = clusters
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);

        RegistrySearchResult? searchResult = null;
        if (!string.IsNullOrWhiteSpace(query))
        {
            searchResult = await ResolveSearchAsync(query, isAdmin, isClusterAdmin, clusters, cancellationToken);
            if (!string.IsNullOrWhiteSpace(searchResult.RedirectPath))
            {
                return Redirect(searchResult.RedirectPath);
            }

            if (!string.IsNullOrWhiteSpace(searchResult.AccessMessage))
            {
                ViewData["RegistryAccessMessage"] = searchResult.AccessMessage;
            }
        }

        var batteryDocuments = searchResult?.Rows
            ?? await _batteryRepository.SearchDocumentsAsync(query, includeArchived: isAdmin, cancellationToken);
        var rows = await BuildBatteryRowsAsync(batteryDocuments, isAdmin, clusterNameById, cancellationToken);

        ViewData["RegistryScopeLabel"] = isAdmin
            ? "Search and open all registered batteries, including drafts and archived passport records."
            : "Search and open batteries with signed or published passports available to your role.";
        ViewData["RegistryEmptyLabel"] = isAdmin
            ? "No batteries are currently available in the registry."
            : "No signed or published batteries are currently available to your role.";
        return View(rows);
    }

    private async Task<RegistrySearchResult> ResolveSearchAsync(
        string query,
        bool isAdmin,
        bool isClusterAdmin,
        IReadOnlyList<BsonDocument> clusters,
        CancellationToken cancellationToken)
    {
        var passport = await _passportRepository.GetByPassportIdAsync(query, cancellationToken);
        if (passport != null)
        {
            return RegistrySearchResult.Redirect($"/{Uri.EscapeDataString(BsonHelpers.GetString(passport, "passportId"))}");
        }

        var battery = await _batteryRepository.GetByBatteryIdAsync(query, cancellationToken);
        if (battery != null)
        {
            return RegistrySearchResult.Redirect(BatterySearchRedirectPath(BsonHelpers.GetString(battery, "batteryId"), isAdmin, isClusterAdmin));
        }

        var bySerial = await _batteryRepository.SearchDocumentsAsync(query, includeArchived: isAdmin, cancellationToken);
        var exactSerialMatches = bySerial
            .Where(row => BsonHelpers.GetString(row, "identity", "serialNumber").Equals(query, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        if (exactSerialMatches.Count == 1)
        {
            return RegistrySearchResult.Redirect(BatterySearchRedirectPath(BsonHelpers.GetString(exactSerialMatches[0], "batteryId"), isAdmin, isClusterAdmin));
        }

        if (!isAdmin && await LooksLikeClusterQueryAsync(query, clusters, cancellationToken))
        {
            return RegistrySearchResult.ClusterDenied(ClusterSearchRequiresGlobalAdminMessage);
        }

        if (isAdmin)
        {
            var clusterRows = await SearchBatteriesForClusterQueryAsync(query, clusters, cancellationToken);
            if (clusterRows.Count > 0)
            {
                return RegistrySearchResult.WithRows(clusterRows);
            }
        }

        return RegistrySearchResult.WithRows(bySerial);
    }

    private async Task<List<BatterySummaryViewModel>> BuildBatteryRowsAsync(
        IReadOnlyList<BsonDocument> batteryDocuments,
        bool includeArchived,
        IReadOnlyDictionary<string, string> clusterNameById,
        CancellationToken cancellationToken)
    {
        var rows = new List<BatterySummaryViewModel>();
        foreach (var battery in batteryDocuments)
        {
            var batteryId = BsonHelpers.GetString(battery, "batteryId");
            var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived, cancellationToken);
            var visible = new List<BatteryPassportHistoryRowViewModel>();
            foreach (var passport in passports)
            {
                if (await _accessControlService.CanOpenPassportSummaryAsync(User, passport, _passportPublishPolicyService, cancellationToken))
                {
                    visible.Add(ToHistoryRow(passport));
                }
            }

            if (visible.Count > 0 || includeArchived)
            {
                rows.Add(_batteryRepository.ToSummary(battery, visible, ResolveClusterLabel(battery, clusterNameById)));
            }
        }

        return rows;
    }

    private async Task<bool> LooksLikeClusterQueryAsync(
        string query,
        IReadOnlyList<BsonDocument> clusters,
        CancellationToken cancellationToken)
    {
        if (MatchingClusterIds(query, clusters).Any())
        {
            return true;
        }

        return (await _batteryRepository.SearchByClusterAsync(query, cancellationToken)).Count > 0;
    }

    private async Task<IReadOnlyList<BsonDocument>> SearchBatteriesForClusterQueryAsync(
        string query,
        IReadOnlyList<BsonDocument> clusters,
        CancellationToken cancellationToken)
    {
        var clusterIds = MatchingClusterIds(query, clusters).ToList();
        if (clusterIds.Count == 0)
        {
            return await _batteryRepository.SearchByClusterAsync(query, cancellationToken);
        }

        var rows = new List<BsonDocument>();
        foreach (var clusterId in clusterIds)
        {
            rows.AddRange(await _batteryRepository.SearchByClusterAsync(clusterId, cancellationToken));
        }

        return rows
            .GroupBy(row => BsonHelpers.GetString(row, "batteryId"), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static IEnumerable<string> MatchingClusterIds(string query, IReadOnlyList<BsonDocument> clusters)
    {
        foreach (var cluster in clusters)
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            var clusterName = BsonHelpers.GetString(cluster, "name");
            if (string.IsNullOrWhiteSpace(clusterId))
            {
                continue;
            }

            if (clusterId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || clusterName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                yield return clusterId;
            }
        }
    }

    private static string BatterySearchRedirectPath(string batteryId, bool isAdmin, bool isClusterAdmin)
    {
        var escapedBatteryId = Uri.EscapeDataString(batteryId);
        if (isAdmin)
        {
            return $"/admin/batteries/{escapedBatteryId}/passports";
        }

        return isClusterAdmin ? $"/{escapedBatteryId}" : $"/{escapedBatteryId}/latest";
    }

    private BatteryPassportHistoryRowViewModel ToHistoryRow(BsonDocument passport)
    {
        return new BatteryPassportHistoryRowViewModel
        {
            PassportId = BsonHelpers.GetString(passport, "passportId"),
            BatteryId = BsonHelpers.GetString(passport, "batteryId"),
            CreatedAt = BsonHelpers.GetString(passport, "snapshot", "createdAt"),
            PassportStatus = PassportRepository.BuildPassportStatusLabel(passport),
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

    private sealed class RegistrySearchResult
    {
        public IReadOnlyList<BsonDocument>? Rows { get; init; }
        public string RedirectPath { get; init; } = string.Empty;
        public string AccessMessage { get; init; } = string.Empty;

        public static RegistrySearchResult Redirect(string path) => new()
        {
            RedirectPath = path
        };

        public static RegistrySearchResult WithRows(IReadOnlyList<BsonDocument> rows) => new()
        {
            Rows = rows
        };

        public static RegistrySearchResult ClusterDenied(string message) => new()
        {
            Rows = [],
            AccessMessage = message
        };
    }
}
