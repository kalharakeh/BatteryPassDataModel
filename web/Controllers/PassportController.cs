using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace BatteryPassWeb.Controllers;

public class PassportController : Controller
{
    private readonly PassportRepository _passportRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly PassportViewModelFactory _viewModelFactory;
    private readonly AccessControlService _accessControlService;
    private readonly BatteryTelemetryRepository _batteryTelemetryRepository;

    public PassportController(
        PassportRepository passportRepository,
        ClusterRepository clusterRepository,
        PassportViewModelFactory viewModelFactory,
        AccessControlService accessControlService,
        BatteryTelemetryRepository batteryTelemetryRepository)
    {
        _passportRepository = passportRepository;
        _clusterRepository = clusterRepository;
        _viewModelFactory = viewModelFactory;
        _accessControlService = accessControlService;
        _batteryTelemetryRepository = batteryTelemetryRepository;
    }

    [HttpGet("{passportId}/summary")]
    public async Task<IActionResult> Summary(string passportId, [FromQuery] string? access, CancellationToken cancellationToken)
    {
        if (IsReservedSegment(passportId))
        {
            return NotFound();
        }

        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }
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
        var passport = _viewModelFactory.Create(document, clusterNamesById);
        var canOpenDetail = false;

        if (User.Identity?.IsAuthenticated == true)
        {
            canOpenDetail = await _accessControlService.CanOpenPassportDetailAsync(User, passport.ClusterId, cancellationToken);
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

        if (string.Equals(access, "wrong-cluster", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(detailAccessNotice))
        {
            detailAccessNotice = $"Sign in with a user connected to {passport.ClusterLabel} to open the detailed report.";
        }

        return View(new PassportSummaryPageViewModel
        {
            Passport = passport,
            DetailAccessNotice = detailAccessNotice
        });
    }

    [HttpGet("{passportId}")]
    public async Task<IActionResult> Detail(string passportId, CancellationToken cancellationToken)
    {
        if (IsReservedSegment(passportId))
        {
            return NotFound();
        }

        var decodedPassportId = Uri.UnescapeDataString(passportId);
        var nextPath = $"/{Uri.EscapeDataString(decodedPassportId)}";
        if (User.Identity?.IsAuthenticated != true)
        {
            return Redirect($"/login?next={Uri.EscapeDataString(nextPath)}");
        }

        var document = await _passportRepository.GetByPassportIdAsync(decodedPassportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }
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

        var passport = _viewModelFactory.Create(document, clusterNamesById);
        var canOpen = await _accessControlService.CanOpenPassportDetailAsync(User, passport.ClusterId, cancellationToken);
        if (!canOpen)
        {
            return Redirect($"/{Uri.EscapeDataString(decodedPassportId)}/summary?access=wrong-cluster");
        }

        var model = new PassportDetailViewModel
        {
            Passport = passport
        };

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-7);
        var telemetryHistory = await _batteryTelemetryRepository.ReadHistoryAsync(decodedPassportId, fromUtc, toUtc, cancellationToken);
        model = new PassportDetailViewModel
        {
            Passport = passport,
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
