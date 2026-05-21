using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace BatteryPassWeb.Controllers;

public class PassportController : Controller
{
    private readonly PassportRepository _passportRepository;
    private readonly BatteryRepository _batteryRepository;
    private readonly BatteryRouteResolutionService _batteryRouteResolutionService;
    private readonly ClusterRepository _clusterRepository;
    private readonly PassportViewModelFactory _viewModelFactory;
    private readonly AccessControlService _accessControlService;
    private readonly BatteryTelemetryRepository _batteryTelemetryRepository;
    private readonly PassportTrustService _passportTrustService;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;
    private readonly BatteryIdService _batteryIdService;

    public PassportController(
        PassportRepository passportRepository,
        BatteryRepository batteryRepository,
        BatteryRouteResolutionService batteryRouteResolutionService,
        ClusterRepository clusterRepository,
        PassportViewModelFactory viewModelFactory,
        AccessControlService accessControlService,
        BatteryTelemetryRepository batteryTelemetryRepository,
        PassportTrustService passportTrustService,
        PassportPublishPolicyService passportPublishPolicyService,
        BatteryIdService batteryIdService)
    {
        _passportRepository = passportRepository;
        _batteryRepository = batteryRepository;
        _batteryRouteResolutionService = batteryRouteResolutionService;
        _clusterRepository = clusterRepository;
        _viewModelFactory = viewModelFactory;
        _accessControlService = accessControlService;
        _batteryTelemetryRepository = batteryTelemetryRepository;
        _passportTrustService = passportTrustService;
        _passportPublishPolicyService = passportPublishPolicyService;
        _batteryIdService = batteryIdService;
    }

    [HttpGet("{passportId}/summary")]
    public async Task<IActionResult> Summary(string passportId, [FromQuery] string? access, CancellationToken cancellationToken)
    {
        if (IsReservedSegment(passportId))
        {
            return NotFound();
        }

        var decodedPassportId = Uri.UnescapeDataString(passportId);
        var document = await _passportRepository.GetByPassportIdAsync(decodedPassportId, cancellationToken)
            ?? FallbackSamplePassport(decodedPassportId);
        if (document == null)
        {
            return NotFound();
        }
        if (string.Equals(BsonHelpers.GetString(document, "registryInfo", "status"), "archived", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        if (!await _accessControlService.CanOpenPassportSummaryAsync(User, document, _passportPublishPolicyService, cancellationToken))
        {
            return NotFound();
        }

        var clusterDocuments = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = clusterDocuments
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        var passport = _viewModelFactory.Create(document, clusterNamesById, _passportTrustService.Verify(document));
        var canOpenDetail = false;

        if (User.Identity?.IsAuthenticated == true)
        {
            canOpenDetail = await _accessControlService.CanOpenPassportDetailAsync(User, document, _passportPublishPolicyService, cancellationToken);
        }

        var detailAccessNotice = string.Empty;
        if (!canOpenDetail)
        {
            if (string.IsNullOrWhiteSpace(passport.ClusterId))
            {
                detailAccessNotice = "This battery is not assigned to a cluster. Sign in as the general admin to open the detailed report.";
            }
            else if (User.Identity?.IsAuthenticated == true)
            {
                var email = AccessControlService.CurrentEmail(User);
                detailAccessNotice = $"Current user {email} is not connected to {passport.ClusterLabel}. Sign in with a user connected to {passport.ClusterLabel} to open the detailed report.";
            }
            else
            {
                detailAccessNotice = $"Sign in with a user connected to {passport.ClusterLabel} to open the detailed report.";
            }
        }

        if (!canOpenDetail && string.Equals(access, "wrong-cluster", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(detailAccessNotice))
            {
                detailAccessNotice = $"Sign in with a user connected to {passport.ClusterLabel} to open the detailed report.";
            }
        }

        return View(new PassportSummaryPageViewModel
        {
            Passport = passport,
            DetailAccessNotice = detailAccessNotice
        });
    }

    [HttpGet("{batteryId}/latest")]
    public async Task<IActionResult> Latest(string batteryId, CancellationToken cancellationToken)
    {
        if (IsReservedSegment(batteryId))
        {
            return NotFound();
        }

        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
        var document = User.Identity?.IsAuthenticated == true
            ? (await _passportRepository.ListByBatteryIdAsync(decodedBatteryId, includeArchived: false, cancellationToken)).FirstOrDefault()
            : await _passportRepository.GetLatestPublicByBatteryIdAsync(decodedBatteryId, _passportPublishPolicyService, cancellationToken);

        if (document == null)
        {
            if (IsGeneratedSampleBatteryId(decodedBatteryId))
            {
                return Redirect($"/{Uri.EscapeDataString(ExternalApiInitializer.SamplePassportId)}/summary");
            }

            return NotFound();
        }

        var passportPath = $"/{Uri.EscapeDataString(BsonHelpers.GetString(document, "passportId"))}";
        return User.Identity?.IsAuthenticated == true
            ? Redirect(passportPath)
            : Redirect($"{passportPath}/summary");
    }

    [HttpGet("{batteryId}/latest/summary")]
    public Task<IActionResult> LatestSummary(string batteryId, CancellationToken cancellationToken)
    {
        return Latest(batteryId, cancellationToken);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken)
    {
        if (IsReservedSegment(id))
        {
            return NotFound();
        }

        var decodedPassportId = Uri.UnescapeDataString(id);
        var resolution = await _batteryRouteResolutionService.ResolveAsync(decodedPassportId, cancellationToken);
        if (resolution.Kind == BatteryRouteTargetKind.Battery)
        {
            return await Battery(decodedPassportId, cancellationToken);
        }

        if (resolution.Kind != BatteryRouteTargetKind.Passport || resolution.Document == null)
        {
            return FallbackSamplePassport(decodedPassportId) != null
                ? RedirectToPublicSummaryWithAccessNotice(decodedPassportId)
                : NotFound();
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPublicSummaryWithAccessNotice(decodedPassportId);
        }

        var document = resolution.Document;
        if (string.Equals(BsonHelpers.GetString(document, "registryInfo", "status"), "archived", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        var clusterDocuments = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = clusterDocuments
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);

        var passport = _viewModelFactory.Create(document, clusterNamesById, _passportTrustService.Verify(document));
        var canOpen = await _accessControlService.CanOpenPassportDetailAsync(User, document, _passportPublishPolicyService, cancellationToken);
        if (!canOpen)
        {
            if (!await _accessControlService.CanOpenPassportSummaryAsync(User, document, _passportPublishPolicyService, cancellationToken))
            {
                return NotFound();
            }

            return Redirect($"/{Uri.EscapeDataString(decodedPassportId)}/summary?access=wrong-cluster");
        }
        var canViewTrustConformance = await _accessControlService.CanViewTrustConformanceAsync(User, passport.ClusterId, cancellationToken);

        var model = new PassportDetailViewModel
        {
            Passport = passport,
            CanViewTrustConformance = canViewTrustConformance
        };

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-7);
        var telemetryBatteryId = passport.BatteryId;
        IReadOnlyList<BsonDocument> telemetryHistory = string.IsNullOrWhiteSpace(telemetryBatteryId) || passport.IsHistoricalPassport
            ? []
            : await _batteryTelemetryRepository.ReadHistoryAsync(telemetryBatteryId, fromUtc, toUtc, cancellationToken);
        model = new PassportDetailViewModel
        {
            Passport = passport,
            CanViewTrustConformance = canViewTrustConformance,
            TelemetryHistory = telemetryHistory.Select(row => new TelemetryHistoryPointViewModel
            {
                MeasuredAt = BsonHelpers.GetString(row, "measuredAt"),
                CurrentConsumptionKwh = ReadNullableNumber(row, "currentConsumptionKwh"),
                CurrentChargeLevelPct = ReadNullableNumber(row, "currentChargeLevelPct"),
                CurrentVoltageV = ReadNullableNumber(row, "currentVoltageV"),
                CurrentCurrentA = ReadNullableNumber(row, "currentCurrentA")
            }).ToList()
        };

        return View(model);
    }

    private async Task<IActionResult> Battery(string batteryId, CancellationToken cancellationToken)
    {
        var battery = await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken);
        if (battery == null)
        {
            return NotFound();
        }

        var clusterDocuments = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = clusterDocuments
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: User.Identity?.IsAuthenticated == true, cancellationToken);
        var visibleRows = new List<BatteryPassportHistoryRowViewModel>();
        foreach (var passport in passports)
        {
            if (!await _accessControlService.CanOpenPassportSummaryAsync(User, passport, _passportPublishPolicyService, cancellationToken))
            {
                continue;
            }

            visibleRows.Add(ToHistoryRow(passport));
        }

        if (visibleRows.Count == 0)
        {
            return NotFound();
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            var latestPublic = visibleRows.FirstOrDefault(row => row.IsLatestForBattery && row.IsPubliclyVisible)
                ?? visibleRows.FirstOrDefault(row => row.IsPubliclyVisible);
            if (latestPublic != null)
            {
                return Redirect($"/{Uri.EscapeDataString(latestPublic.PassportId)}/summary");
            }
        }

        var clusterId = BsonHelpers.GetString(battery, "clusterId");
        var clusterLabel = string.IsNullOrWhiteSpace(clusterId)
            ? "No cluster assigned"
            : clusterNamesById.TryGetValue(clusterId, out var clusterName) && !string.IsNullOrWhiteSpace(clusterName)
                ? clusterName
                : clusterId;

        return View("Battery", new BatteryDetailViewModel
        {
            Battery = _batteryRepository.ToSummary(battery, visibleRows, clusterLabel)
        });
    }

    private BsonDocument? FallbackSamplePassport(string passportId)
    {
        if (!passportId.Equals(ExternalApiInitializer.SamplePassportId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ExternalApiInitializer.CreateFallbackSampleDocument(ExternalApiInitializer.CreateSampleBatteryId(_batteryIdService));
    }

    private static IActionResult RedirectToPublicSummaryWithAccessNotice(string passportId)
    {
        return new RedirectResult($"/{Uri.EscapeDataString(passportId)}/summary?access=detail-required");
    }

    private bool IsGeneratedSampleBatteryId(string batteryId)
    {
        return batteryId.Equals(ExternalApiInitializer.CreateSampleBatteryId(_batteryIdService), StringComparison.OrdinalIgnoreCase);
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

    private static double? ReadNullableNumber(BsonDocument document, string key)
    {
        var value = document.GetValue(key, BsonNull.Value);
        if (value.IsBsonNull)
        {
            return null;
        }

        return value.IsNumeric ? value.ToDouble() : null;
    }

    private static bool IsReservedSegment(string value)
    {
        return value.Equals("login", StringComparison.OrdinalIgnoreCase)
            || value.Equals("registry", StringComparison.OrdinalIgnoreCase)
            || value.Equals("search", StringComparison.OrdinalIgnoreCase)
            || value.Equals("help", StringComparison.OrdinalIgnoreCase)
            || value.Equals("admin", StringComparison.OrdinalIgnoreCase)
            || value.Equals("cluster-admin", StringComparison.OrdinalIgnoreCase)
            || value.Equals("api", StringComparison.OrdinalIgnoreCase);
    }
}
