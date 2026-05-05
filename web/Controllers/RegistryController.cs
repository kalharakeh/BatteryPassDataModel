using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Authorize]
[Route("registry")]
public class RegistryController : Controller
{
    private const string SamplePassportId = "did:web:acme.battery.pass:sample-customer-north-001";
    private readonly PassportRepository _passportRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly AccessControlService _accessControlService;

    public RegistryController(
        PassportRepository passportRepository,
        ClusterRepository clusterRepository,
        AccessControlService accessControlService)
    {
        _passportRepository = passportRepository;
        _clusterRepository = clusterRepository;
        _accessControlService = accessControlService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("admin") && !User.IsInRole("clusterAdmin"))
        {
            return NotFound();
        }

        var query = q?.Trim() ?? string.Empty;
        var passports = await _passportRepository.SearchAsync(query, includeArchived: User.IsInRole("admin"), cancellationToken);
        if (!User.IsInRole("admin"))
        {
            var clusterIds = await _accessControlService.GetAdministeredClusterIdsForUserAsync(User, cancellationToken);
            passports = passports
                .Where(passport => !string.IsNullOrWhiteSpace(passport.ClusterId)
                                   && clusterIds.Contains(passport.ClusterId, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNameById = clusters
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);

        passports = passports.Select(passport => new PassportSummaryViewModel
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
                : clusterNameById.TryGetValue(passport.ClusterId, out var clusterName)
                    ? clusterName
                    : passport.ClusterId,
            BatteryImageUrl = passport.BatteryImageUrl,
            UpdatedDate = passport.UpdatedDate
        }).ToList();

        var exactMatch = passports.FirstOrDefault(passport => passport.PassportId.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return Redirect($"/{Uri.EscapeDataString(exactMatch.PassportId)}/summary");
        }

        ViewData["SamplePassportId"] = SamplePassportId;
        return View(passports);
    }
}
