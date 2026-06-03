using System.Security.Claims;
using BatteryPassWeb.Models.ViewModels;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public enum BatteryTableScope
{
    Registry,
    AdminBatteries,
    AdminPassports,
    ClusterAdminPassports
}

public sealed class BatteryTableService
{
    private const string ClusterSearchNotAvailableMessage = "Cluster search is not available for this role.";

    private readonly BatteryRepository _batteryRepository;
    private readonly PassportRepository _passportRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly AccessControlService _accessControlService;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;

    public BatteryTableService(
        BatteryRepository batteryRepository,
        PassportRepository passportRepository,
        ClusterRepository clusterRepository,
        AccessControlService accessControlService,
        PassportPublishPolicyService passportPublishPolicyService)
    {
        _batteryRepository = batteryRepository;
        _passportRepository = passportRepository;
        _clusterRepository = clusterRepository;
        _accessControlService = accessControlService;
        _passportPublishPolicyService = passportPublishPolicyService;
    }

    public async Task<BatteryTablePageViewModel> BuildAsync(
        ClaimsPrincipal user,
        BatteryTableScope scope,
        string? query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var isGlobalAdmin = AccessControlService.IsAdmin(user);
        var isClusterAdmin = AccessControlService.IsClusterAdmin(user) && !isGlobalAdmin;
        var hasAllClusterReadScope = AccessControlService.HasAllClusterReadScope(user);
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = clusters
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);

        var baseModel = CreateBaseModel(scope, normalizedQuery);
        var searchResult = string.IsNullOrWhiteSpace(normalizedQuery)
            ? BatteryTableSearchResult.Empty()
            : await ResolveSearchAsync(user, scope, normalizedQuery, isGlobalAdmin, isClusterAdmin, hasAllClusterReadScope, clusters, cancellationToken);
        if (!string.IsNullOrWhiteSpace(searchResult.RedirectPath))
        {
            return baseModel.WithRedirect(searchResult.RedirectPath);
        }

        var batteryDocuments = searchResult.Rows
            ?? await _batteryRepository.SearchDocumentsAsync(normalizedQuery, includeArchived: isGlobalAdmin, cancellationToken);
        batteryDocuments = await FilterRowsForScopeAsync(user, batteryDocuments, scope, isGlobalAdmin, cancellationToken);
        var rows = await BuildRowsAsync(user, batteryDocuments, scope, isGlobalAdmin, clusterNamesById, baseModel.ReturnUrl, cancellationToken);

        return new BatteryTablePageViewModel
        {
            Query = baseModel.Query,
            SearchAction = baseModel.SearchAction,
            SearchPlaceholder = baseModel.SearchPlaceholder,
            ScopeLabel = baseModel.ScopeLabel,
            EmptyLabel = baseModel.EmptyLabel,
            AccessMessage = searchResult.AccessMessage,
            ReturnUrl = baseModel.ReturnUrl,
            ShowCreateBattery = baseModel.ShowCreateBattery,
            ShowHelp = baseModel.ShowHelp,
            Rows = rows
        };
    }

    private async Task<BatteryTableSearchResult> ResolveSearchAsync(
        ClaimsPrincipal user,
        BatteryTableScope scope,
        string query,
        bool isGlobalAdmin,
        bool isClusterAdmin,
        bool hasAllClusterReadScope,
        IReadOnlyList<BsonDocument> clusters,
        CancellationToken cancellationToken)
    {
        var passport = await _passportRepository.GetByPassportIdAsync(query, cancellationToken);
        if (passport != null)
        {
            var passportBatteryId = BsonHelpers.GetString(passport, "batteryId");
            var passportBattery = await _batteryRepository.GetByBatteryIdAsync(passportBatteryId, cancellationToken);
            return BatteryTableSearchResult.WithRows(passportBattery == null ? [] : [passportBattery]);
        }

        var battery = await _batteryRepository.GetByBatteryIdAsync(query, cancellationToken);
        if (battery != null)
        {
            return BatteryTableSearchResult.WithRows([battery]);
        }

        var bySerial = await _batteryRepository.SearchDocumentsAsync(query, includeArchived: isGlobalAdmin, cancellationToken);
        var exactSerialMatches = bySerial
            .Where(row => BsonHelpers.GetString(row, "identity", "serialNumber").Equals(query, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        if (exactSerialMatches.Count == 1)
        {
            return BatteryTableSearchResult.WithRows(exactSerialMatches);
        }

        if (!hasAllClusterReadScope && await LooksLikeClusterQueryAsync(query, clusters, cancellationToken))
        {
            return BatteryTableSearchResult.Denied(ClusterSearchNotAvailableMessage);
        }

        if (hasAllClusterReadScope)
        {
            var clusterRows = await SearchBatteriesForClusterQueryAsync(query, clusters, cancellationToken);
            if (clusterRows.Count > 0)
            {
                return BatteryTableSearchResult.WithRows(clusterRows);
            }
        }

        return BatteryTableSearchResult.WithRows(bySerial);
    }

    private async Task<IReadOnlyList<BsonDocument>> FilterRowsForScopeAsync(
        ClaimsPrincipal user,
        IReadOnlyList<BsonDocument> batteryDocuments,
        BatteryTableScope scope,
        bool isGlobalAdmin,
        CancellationToken cancellationToken)
    {
        if (isGlobalAdmin)
        {
            return batteryDocuments;
        }

        if (AccessControlService.HasAllClusterReadScope(user))
        {
            return batteryDocuments;
        }

        var managedClusterIds = scope == BatteryTableScope.ClusterAdminPassports
            ? (await _accessControlService.GetAdministeredClusterIdsForUserAsync(user, cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : (await _accessControlService.GetClusterIdsForUserAsync(user, cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (managedClusterIds.Count == 0)
        {
            return [];
        }

        return batteryDocuments
            .Where(battery => managedClusterIds.Contains(BsonHelpers.GetString(battery, "clusterId")))
            .ToList();
    }

    private async Task<IReadOnlyList<BatterySummaryViewModel>> BuildRowsAsync(
        ClaimsPrincipal user,
        IReadOnlyList<BsonDocument> batteryDocuments,
        BatteryTableScope scope,
        bool isGlobalAdmin,
        IReadOnlyDictionary<string, string> clusterNamesById,
        string returnUrl,
        CancellationToken cancellationToken)
    {
        var rows = new List<BatterySummaryViewModel>();
        foreach (var battery in batteryDocuments)
        {
            var batteryId = BsonHelpers.GetString(battery, "batteryId");
            if (string.IsNullOrWhiteSpace(batteryId))
            {
                continue;
            }

            var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: isGlobalAdmin, cancellationToken);
            var history = new List<BatteryPassportHistoryRowViewModel>();
            foreach (var passport in passports)
            {
                if (isGlobalAdmin || await _accessControlService.CanOpenPassportSummaryAsync(user, passport, _passportPublishPolicyService, cancellationToken))
                {
                    history.Add(ToHistoryRow(passport));
                }
            }

            if (history.Count == 0 && !isGlobalAdmin)
            {
                continue;
            }

            var row = _batteryRepository.ToSummary(battery, history, ResolveClusterLabel(battery, clusterNamesById));
            ApplyActions(row, scope, isGlobalAdmin, returnUrl);
            rows.Add(row);
        }

        return rows
            .OrderBy(row => row.BatteryFamily, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.BatterySerialNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ApplyActions(BatterySummaryViewModel row, BatteryTableScope scope, bool isGlobalAdmin, string returnUrl)
    {
        var escapedBatteryId = Uri.EscapeDataString(row.BatteryId);
        var escapedReturnUrl = Uri.EscapeDataString(returnUrl);
        row.ReturnUrl = returnUrl;
        row.SummaryUrl = string.IsNullOrWhiteSpace(row.LatestPassportId)
            ? string.Empty
            : $"/{Uri.EscapeDataString(row.LatestPassportId)}/summary";
        row.DetailUrl = string.IsNullOrWhiteSpace(row.LatestPassportId)
            ? string.Empty
            : $"/{escapedBatteryId}/latest";
        row.HistoryUrl = isGlobalAdmin
            ? $"/admin/batteries/{escapedBatteryId}/passports?returnUrl={escapedReturnUrl}"
            : scope == BatteryTableScope.ClusterAdminPassports
                ? $"/cluster-admin/batteries/{escapedBatteryId}/passports?returnUrl={escapedReturnUrl}"
                : $"/{escapedBatteryId}";
        row.EditUrl = isGlobalAdmin
            ? $"/admin/batteries/{escapedBatteryId}/edit"
            : string.IsNullOrWhiteSpace(row.LatestPassportId)
                ? string.Empty
                : $"/cluster-admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/edit";
        row.CreatePassportUrl = isGlobalAdmin
            ? $"/admin/batteries/{escapedBatteryId}/passports/create"
            : $"/cluster-admin/batteries/{escapedBatteryId}/passports/create";
        row.ConformanceUrl = string.IsNullOrWhiteSpace(row.LatestPassportId)
            ? string.Empty
            : isGlobalAdmin
                ? $"/admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/conformance"
                : $"/cluster-admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/conformance";
        row.CanViewHistory = true;
        row.CanEditBattery = isGlobalAdmin || (scope == BatteryTableScope.ClusterAdminPassports && !string.IsNullOrWhiteSpace(row.EditUrl));
        row.CanCreatePassport = (isGlobalAdmin || scope == BatteryTableScope.ClusterAdminPassports)
            && CanCreateBatteryPassport(row);
        row.CanOpenConformance = (isGlobalAdmin || scope == BatteryTableScope.ClusterAdminPassports)
            && !string.IsNullOrWhiteSpace(row.ConformanceUrl);
        row.ShowNewPassportRequired = row.NewPassportRequired && !IsDraftStatus(row.LatestPassportStatus);
        row.DisplayStatus = row.ShowNewPassportRequired ? "New passport needed" : row.LatestPassportStatus;
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

    private static bool IsDraftStatus(string status) =>
        status.Contains("draft", StringComparison.OrdinalIgnoreCase)
        || status.Contains("awaiting", StringComparison.OrdinalIgnoreCase);

    private static bool CanCreateBatteryPassport(BatterySummaryViewModel row) =>
        row.PassportCount == 0
        || row.NewPassportRequired && !IsDraftStatus(row.LatestPassportStatus);

    private static string BatterySearchRedirectPath(string batteryId, bool isGlobalAdmin, bool isClusterAdmin)
    {
        var escapedBatteryId = Uri.EscapeDataString(batteryId);
        if (isGlobalAdmin)
        {
            return $"/admin/batteries/{escapedBatteryId}/passports";
        }

        return isClusterAdmin ? $"/{escapedBatteryId}" : $"/{escapedBatteryId}/latest";
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

    private static BatteryTablePageViewModel CreateBaseModel(BatteryTableScope scope, string query)
    {
        var querySuffix = string.IsNullOrWhiteSpace(query) ? string.Empty : $"?q={Uri.EscapeDataString(query)}";
        return scope switch
        {
            BatteryTableScope.AdminBatteries => new BatteryTablePageViewModel
            {
                Query = query,
                SearchAction = "/admin/clusters",
                SearchPlaceholder = "Search by battery ID, passport ID, serial, family, or cluster",
                ScopeLabel = "Search and manage all registered batteries.",
                EmptyLabel = "No batteries are currently available.",
                ReturnUrl = $"/admin/clusters?tab=batteries{(string.IsNullOrWhiteSpace(query) ? string.Empty : $"&q={Uri.EscapeDataString(query)}")}",
                ShowCreateBattery = true,
                ShowHelp = true
            },
            BatteryTableScope.AdminPassports => new BatteryTablePageViewModel
            {
                Query = query,
                SearchAction = "/admin/passports",
                SearchPlaceholder = "Search by battery ID, passport ID, serial, family, or cluster",
                ScopeLabel = "Search and manage batteries through their passport history.",
                EmptyLabel = "No batteries are currently available.",
                ReturnUrl = $"/admin/passports{querySuffix}",
                ShowCreateBattery = true,
                ShowHelp = true
            },
            BatteryTableScope.ClusterAdminPassports => new BatteryTablePageViewModel
            {
                Query = query,
                SearchAction = "/cluster-admin/passports",
                SearchPlaceholder = "Search by battery ID, passport ID, serial, family, or cluster",
                ScopeLabel = "Search batteries in clusters you administer.",
                EmptyLabel = "No managed batteries found for this account.",
                ReturnUrl = $"/cluster-admin/passports{querySuffix}"
            },
            _ => new BatteryTablePageViewModel
            {
                Query = query,
                SearchAction = "/registry",
                SearchPlaceholder = "Search by battery ID, passport ID, serial, or cluster",
                ScopeLabel = "Search and open batteries available to your role.",
                EmptyLabel = "No batteries are currently available in the registry.",
                ReturnUrl = $"/registry{querySuffix}"
            }
        };
    }

    private sealed class BatteryTableSearchResult
    {
        public IReadOnlyList<BsonDocument>? Rows { get; init; }
        public string RedirectPath { get; init; } = string.Empty;
        public string AccessMessage { get; init; } = string.Empty;

        public static BatteryTableSearchResult Empty() => new();

        public static BatteryTableSearchResult Redirect(string path) => new()
        {
            RedirectPath = path
        };

        public static BatteryTableSearchResult WithRows(IReadOnlyList<BsonDocument> rows) => new()
        {
            Rows = rows
        };

        public static BatteryTableSearchResult Denied(string message) => new()
        {
            Rows = [],
            AccessMessage = message
        };
    }
}

file static class BatteryTablePageViewModelExtensions
{
    public static BatteryTablePageViewModel WithRedirect(this BatteryTablePageViewModel model, string redirectPath) => new()
    {
        Query = model.Query,
        SearchAction = model.SearchAction,
        SearchPlaceholder = model.SearchPlaceholder,
        ScopeLabel = model.ScopeLabel,
        EmptyLabel = model.EmptyLabel,
        AccessMessage = model.AccessMessage,
        RedirectPath = redirectPath,
        ReturnUrl = model.ReturnUrl,
        ShowCreateBattery = model.ShowCreateBattery,
        ShowHelp = model.ShowHelp,
        Rows = model.Rows
    };
}
