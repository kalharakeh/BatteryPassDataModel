using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BatteryPassWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("admin")]
public class AdminController : Controller
{
    private static readonly (string Field, string Label, string Color)[] MaterialFields =
    [
        ("materialNickel", "Nickel", "#4f6f7d"),
        ("materialCopper", "Copper", "#d76f3d"),
        ("materialAluminium", "Aluminium", "#aeb4ba"),
        ("materialGraphite", "Graphite", "#27313f"),
        ("materialManganese", "Manganese", "#d9b64e"),
        ("materialCobalt", "Cobalt", "#0aa34f"),
        ("materialLithium", "Lithium", "#85c7d6"),
        ("materialElectrolyte", "Electrolyte and separators", "#e7d99d")
    ];

    private static readonly (string Field, string Label, string Stage, string Color)[] CarbonFields =
    [
        ("carbonRawMaterial", "raw material extraction", "RawMaterialExtraction", "#08a348"),
        ("carbonMainProduction", "main production", "MainProduction", "#df6b3b"),
        ("carbonDistribution", "distribution", "Distribution", "#ead9a4"),
        ("carbonRecycling", "recycling", "Recycling", "#4f6f7d")
    ];

    private static readonly string[] DocumentKeys =
    [
        "conformityAssessment",
        "euDeclarationOfConformity",
        "sustainabilityReport",
        "dueDiligenceReport",
        "thirdPartyAudit",
        "taxonomyReport",
        "co2StudyReference"
    ];

    private const string SamplePassportId = "did:web:acme.battery.pass:sample-customer-north-001";

    private readonly PassportRepository _passportRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly PassportViewModelFactory _viewModelFactory;

    public AdminController(
        PassportRepository passportRepository,
        ClusterRepository clusterRepository,
        PassportViewModelFactory viewModelFactory)
    {
        _passportRepository = passportRepository;
        _clusterRepository = clusterRepository;
        _viewModelFactory = viewModelFactory;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return Redirect("/admin/clusters?tab=passports");
    }

    [HttpGet("passports")]
    public IActionResult Passports([FromQuery] string? q)
    {
        var queryPart = string.IsNullOrWhiteSpace(q) ? string.Empty : $"&q={Uri.EscapeDataString(q)}";
        return Redirect($"/admin/clusters?tab=passports{queryPart}");
    }

    [HttpGet("passports/new")]
    public async Task<IActionResult> NewPassport([FromQuery] string? status, [FromQuery] string? error, [FromQuery] string? passportId, CancellationToken cancellationToken)
    {
        var draftPassportId = string.IsNullOrWhiteSpace(passportId)
            ? $"did:web:acme.battery.pass:{Guid.NewGuid():N}"
            : passportId.Trim();
        var document = await BuildDraftPassportDocumentAsync(draftPassportId, cancellationToken);
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = BuildClusterDictionary(clusters);

        var model = new EditPassportViewModel
        {
            Passport = _viewModelFactory.Create(document, clusterNamesById),
            Mode = "new",
            StatusMessage = status == "created" ? "Passport created." : string.Empty,
            ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error)
        };

        return View("EditPassport", model);
    }

    [HttpPost("passports/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchivePassport(CancellationToken cancellationToken)
    {
        var passportId = Text(Request.Form, "passportId");
        if (!string.IsNullOrWhiteSpace(passportId))
        {
            await _passportRepository.ArchivePassportAsync(passportId, cancellationToken);
        }

        return Redirect("/admin/clusters?tab=passports");
    }

    [HttpPost("passports/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePassport(CancellationToken cancellationToken)
    {
        var form = Request.Form;
        var passportId = Text(form, "passportId");
        if (string.IsNullOrWhiteSpace(passportId))
        {
            return Redirect($"/admin/passports/new?error={Uri.EscapeDataString("Passport ID is required.")}");
        }

        var existing = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (existing != null)
        {
            return Redirect($"/admin/passports/new?passportId={Uri.EscapeDataString(passportId)}&error={Uri.EscapeDataString("Passport ID already exists.")}");
        }

        var document = await BuildDraftPassportDocumentAsync(passportId, cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        ApplyPassportForm(document, form, now);
        document["passportId"] = passportId;
        document.Remove("_id");

        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/edit?status=created");
    }

    [HttpGet("passports/{passportId}/edit")]
    public async Task<IActionResult> EditPassport(string passportId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = BuildClusterDictionary(clusters);
        var model = new EditPassportViewModel
        {
            Passport = _viewModelFactory.Create(document, clusterNamesById),
            Mode = "edit",
            StatusMessage = status switch
            {
                "saved" => "Passport changes saved.",
                "created" => "Passport created.",
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
        var form = Request.Form;
        var passportId = Text(form, "passportId");
        if (string.IsNullOrWhiteSpace(passportId))
        {
            return Redirect("/admin/clusters?tab=passports");
        }

        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/edit?error={Uri.EscapeDataString("Passport not found.")}");
        }

        var now = DateTime.UtcNow.ToString("O");
        ApplyPassportForm(document, form, now);

        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/edit?status=saved");
    }

    [HttpGet("clusters")]
    public async Task<IActionResult> Clusters([FromQuery] string? tab, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        var selectedTab = NormalizeTab(tab);
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
        var passports = await _passportRepository.SearchAsync(q ?? string.Empty, includeArchived: true, cancellationToken);
        var users = await _clusterRepository.ListUsersAsync(cancellationToken);
        var memberships = await _clusterRepository.ListClusterMembershipsAsync(cancellationToken);

        var model = new AdminClusterViewModel
        {
            SelectedTab = selectedTab,
            PassportsQuery = q ?? string.Empty,
            Clusters = clusterViewModels,
            Passports = passports
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
                .ToList(),
            Users = users.Select(user => new UserViewModel
            {
                Email = BsonHelpers.GetString(user, "email"),
                Name = BsonHelpers.GetString(user, "name"),
                Roles = (user.GetValue("roles", new BsonArray()) as BsonArray ?? new BsonArray())
                    .Select(role => role.ToString() ?? string.Empty)
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .ToList()
            }).ToList(),
            Memberships = memberships.Select(membership => new ClusterMembershipViewModel
            {
                Email = BsonHelpers.GetString(membership, "email"),
                ClusterId = BsonHelpers.GetString(membership, "clusterId"),
                Role = BsonHelpers.GetString(membership, "role"),
                CreatedAt = BsonHelpers.GetString(membership, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(membership, "updatedAt")
            }).ToList()
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

        await _clusterRepository.UpsertClusterAsync(clusterId, name, cancellationToken);
        return Redirect("/admin/clusters?tab=clusters");
    }

    [HttpPost("clusters/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCluster(CancellationToken cancellationToken)
    {
        var clusterId = Text(Request.Form, "clusterId");
        var name = Text(Request.Form, "name");
        await _clusterRepository.UpdateClusterNameAsync(clusterId, name, cancellationToken);
        return Redirect("/admin/clusters?tab=clusters");
    }

    [HttpPost("clusters/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCluster(CancellationToken cancellationToken)
    {
        var clusterId = Text(Request.Form, "clusterId");
        await _passportRepository.ClearPassportClusterAsync(clusterId, cancellationToken);
        await _clusterRepository.DeleteClusterAsync(clusterId, cancellationToken);
        return Redirect("/admin/clusters?tab=clusters");
    }

    [HttpPost("clusters/assign-passport")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPassportCluster(CancellationToken cancellationToken)
    {
        var passportId = Text(Request.Form, "passportId");
        var clusterId = Text(Request.Form, "clusterId");
        await _passportRepository.UpdatePassportClusterAsync(passportId, clusterId, cancellationToken);
        return Redirect("/admin/clusters?tab=battery");
    }

    [HttpPost("clusters/assign-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUserMembership(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email");
        var clusterId = Text(Request.Form, "clusterId");
        var role = Text(Request.Form, "role", "member");
        await _clusterRepository.UpsertClusterMembershipAsync(email, clusterId, role, cancellationToken);
        return Redirect("/admin/clusters?tab=users");
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
        var requestedSystemRole = Text(Request.Form, "systemRole", "member");
        var existingUser = await _clusterRepository.GetUserByEmailAsync(email, cancellationToken);

        var existingRoles = existingUser?.GetValue("roles", new BsonArray()) is BsonArray roleArray
            ? roleArray
                .Select(role => role.ToString())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role!)
                .ToList()
            : [];
        var roles = new HashSet<string>(existingRoles, StringComparer.OrdinalIgnoreCase);
        if (requestedSystemRole.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            roles.Add("admin");
            roles.Add("viewer");
        }
        else
        {
            roles.Remove("admin");
            if (roles.Count == 0)
            {
                roles.Add("viewer");
            }
        }

        if (existingUser == null && string.IsNullOrWhiteSpace(password))
        {
            return Redirect("/admin/clusters?tab=users");
        }

        var passwordHash = string.IsNullOrWhiteSpace(password) ? string.Empty : BCryptNet.HashPassword(password);
        await _clusterRepository.UpsertUserAsync(email, displayName, roles.ToList(), passwordHash, cancellationToken);

        var clusterId = Text(Request.Form, "clusterId");
        if (!string.IsNullOrWhiteSpace(clusterId))
        {
            var membershipRole = Text(Request.Form, "role", "member");
            await _clusterRepository.UpsertClusterMembershipAsync(email, clusterId, membershipRole, cancellationToken);
        }

        return Redirect("/admin/clusters?tab=users");
    }

    [HttpPost("clusters/delete-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserMembership(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email");
        var clusterId = Text(Request.Form, "clusterId");
        await _clusterRepository.DeleteClusterMembershipAsync(email, clusterId, cancellationToken);
        return Redirect("/admin/clusters?tab=users");
    }

    private static string NormalizeTab(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "battery" => "battery",
            "passports" => "passports",
            "clusters" => "clusters",
            "users" => "users",
            _ => "passports"
        };
    }

    private static Dictionary<string, string> BuildClusterDictionary(IEnumerable<BsonDocument> clusters)
    {
        return clusters
            .Select(cluster => new
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name")
            })
            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<BsonDocument> BuildDraftPassportDocumentAsync(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(SamplePassportId, cancellationToken);
        if (document == null)
        {
            var candidates = await _passportRepository.SearchDocumentsAsync(string.Empty, includeArchived: true, cancellationToken);
            document = candidates.FirstOrDefault();
        }

        var draft = document != null ? document.DeepClone().AsBsonDocument : new BsonDocument();
        draft.Remove("_id");
        draft.Remove("clusterId");
        draft["passportId"] = passportId;

        var now = DateTime.UtcNow.ToString("O");
        var registryInfo = EnsureDocument(draft, "registryInfo");
        registryInfo["registryId"] = Guid.NewGuid().ToString("N");
        registryInfo["status"] = "draft";
        registryInfo["createdAt"] = now;
        registryInfo["updatedAt"] = now;

        var validation = EnsureDocument(draft, "validation");
        validation["isValid"] = false;
        validation["signedAt"] = BsonNull.Value;
        if (!validation.Contains("hash"))
        {
            validation["hash"] = string.Empty;
        }
        if (!validation.Contains("signature"))
        {
            validation["signature"] = string.Empty;
        }
        if (!validation.Contains("proof"))
        {
            validation["proof"] = new BsonDocument();
        }

        return draft;
    }

    private static void ApplyPassportForm(BsonDocument document, IFormCollection form, string now)
    {
        var app = EnsureDocument(document, "app");
        var display = EnsureDocument(app, "display");
        var media = EnsureDocument(app, "media");
        var appDocuments = EnsureDocument(app, "documents");
        var appCharts = EnsureDocument(app, "charts");
        var appNotes = EnsureDocument(app, "notes");
        var appCircularityNotes = EnsureDocument(appNotes, "circularity");

        display["name"] = Text(form, "name", display.GetValue("name", string.Empty).ToString());
        display["modelNumber"] = Text(form, "modelNumber", display.GetValue("modelNumber", string.Empty).ToString());
        display["serialNumber"] = Text(form, "serialNumber", display.GetValue("serialNumber", string.Empty).ToString());
        display["facilityId"] = Text(form, "facilityId", display.GetValue("facilityId", string.Empty).ToString());
        display["manufacturerName"] = Text(form, "manufacturerName", display.GetValue("manufacturerName", string.Empty).ToString());

        var batteryImageUrl = Text(form, "batteryImageUrl", media.GetValue("batteryImageUrl", "/sample-battery.png").ToString());
        media["batteryImageUrl"] = string.IsNullOrWhiteSpace(batteryImageUrl) ? "/sample-battery.png" : batteryImageUrl;
        media["batteryImageAlt"] = $"Industrial battery pack for passport {display.GetValue("modelNumber", string.Empty)}";

        foreach (var key in DocumentKeys)
        {
            var documentNode = EnsureDocument(appDocuments, key);
            var url = Text(form, $"document_{key}", documentNode.GetValue("url", string.Empty).ToString());
            if (!string.IsNullOrWhiteSpace(url))
            {
                documentNode["url"] = url;
            }
        }

        var aspects = EnsureDocument(document, "aspects");
        var generalAspect = EnsureAspectPayload(aspects, "generalProductInformation", now, "verified");
        var generalPayload = EnsureDocument(generalAspect, "payload");
        generalPayload["productIdentifier"] = display.GetValue("modelNumber", string.Empty).ToString();
        generalPayload["batteryPassportIdentifier"] = $"urn:acme:{display.GetValue("serialNumber", string.Empty).ToString().ToLowerInvariant().Replace("-", string.Empty)}";
        generalPayload["batteryCategory"] = Text(form, "category", generalPayload.GetValue("batteryCategory", "ev").ToString()).ToLowerInvariant();
        generalPayload["batteryStatus"] = Text(form, "batteryStatus", generalPayload.GetValue("batteryStatus", "Original").ToString());
        generalPayload["batteryMass"] = Number(form, "batteryMass", generalPayload.GetValue("batteryMass", 0).ToDouble());
        generalPayload["manufacturingDate"] = $"{Text(form, "manufacturingDate", DateOnly(generalPayload.GetValue("manufacturingDate", string.Empty).ToString()))}T00:00:00.000Z";

        var materialAspect = EnsureAspectPayload(aspects, "materialComposition", now, "verified");
        var materialPayload = EnsureDocument(materialAspect, "payload");
        var existingMaterials = materialPayload.GetValue("batteryMaterials", new BsonArray()) is BsonArray materials
            ? materials
            : new BsonArray();
        var materialChart = new BsonArray();
        var materialRows = new BsonArray();
        foreach (var (field, label, color) in MaterialFields)
        {
            var fallbackMass = existingMaterials
                .OfType<BsonDocument>()
                .FirstOrDefault(item => item.GetValue("batteryMaterialName", string.Empty).ToString() == label)?
                .GetValue("batteryMaterialMass", 0).ToDouble() ?? 0;
            var mass = Number(form, field, fallbackMass);
            materialChart.Add(new BsonDocument
            {
                ["label"] = label,
                ["value"] = mass,
                ["unit"] = "kg",
                ["color"] = color
            });

            var existing = existingMaterials.OfType<BsonDocument>()
                .FirstOrDefault(item => item.GetValue("batteryMaterialName", string.Empty).ToString() == label);
            if (existing != null)
            {
                existing["batteryMaterialMass"] = mass;
                materialRows.Add(existing);
            }
            else
            {
                materialRows.Add(new BsonDocument
                {
                    ["batteryMaterialName"] = label,
                    ["batteryMaterialMass"] = mass
                });
            }
        }
        materialPayload["batteryMaterials"] = materialRows;
        appCharts["materialComposition"] = materialChart;

        var performanceAspect = EnsureAspectPayload(aspects, "performanceAndDurability", now, "verified");
        var performancePayload = EnsureDocument(performanceAspect, "payload");
        var technical = EnsureDocument(performancePayload, "batteryTechicalProperties");
        technical["ratedEnergy"] = Number(form, "ratedEnergy", technical.GetValue("ratedEnergy", 0).ToDouble());
        technical["ratedCapacity"] = Number(form, "ratedCapacity", technical.GetValue("ratedCapacity", 0).ToDouble());
        technical["ratedMaximumPower"] = Number(form, "ratedMaximumPower", technical.GetValue("ratedMaximumPower", 0).ToDouble());
        technical["nominalVoltage"] = Number(form, "nominalVoltage", technical.GetValue("nominalVoltage", 0).ToDouble());
        technical["expectedLifetime"] = Number(form, "expectedLifetime", technical.GetValue("expectedLifetime", 0).ToDouble());
        technical["expectedNumberOfCycles"] = Number(form, "expectedNumberOfCycles", technical.GetValue("expectedNumberOfCycles", 0).ToDouble());
        var batteryCondition = EnsureDocument(performancePayload, "batteryCondition");
        batteryCondition["stateOfCharge"] = new BsonDocument
        {
            ["stateOfChargeValue"] = Number(form, "stateOfCharge", NumberAtDocument(batteryCondition, "stateOfCharge", "stateOfChargeValue")),
            ["lastUpdate"] = now
        };
        batteryCondition["remainingCapacity"] = new BsonDocument
        {
            ["remainingCapacityValue"] = Number(form, "remainingCapacity", NumberAtDocument(batteryCondition, "remainingCapacity", "remainingCapacityValue")),
            ["lastUpdate"] = now
        };
        batteryCondition["remainingEnergy"] = new BsonDocument
        {
            ["remainingEnergyValue"] = Number(form, "remainingEnergy", NumberAtDocument(batteryCondition, "remainingEnergy", "remainingEnergyValue")),
            ["lastUpdate"] = now
        };
        batteryCondition["numberOfFullCycles"] = new BsonDocument
        {
            ["numberOfFullCyclesValue"] = Number(form, "fullCycles", NumberAtDocument(batteryCondition, "numberOfFullCycles", "numberOfFullCyclesValue")),
            ["lastUpdate"] = now
        };

        var carbonAspect = EnsureAspectPayload(aspects, "carbonFootprintForBatteries", now, "verified");
        var carbonPayload = EnsureDocument(carbonAspect, "payload");
        carbonPayload["batteryCarbonFootprint"] = Number(form, "carbonFootprint", carbonPayload.GetValue("batteryCarbonFootprint", 0).ToDouble());
        carbonPayload["carbonFootprintPerformanceClass"] = Text(form, "performanceClass", carbonPayload.GetValue("carbonFootprintPerformanceClass", "B").ToString());
        var lifecycleRows = new BsonArray();
        var carbonChart = new BsonArray();
        foreach (var (field, label, stage, color) in CarbonFields)
        {
            var value = Number(form, field, 0);
            lifecycleRows.Add(new BsonDocument
            {
                ["lifecycleStage"] = stage,
                ["carbonFootprint"] = value
            });
            carbonChart.Add(new BsonDocument
            {
                ["label"] = label,
                ["value"] = value,
                ["unit"] = "gCO2e/kWh",
                ["color"] = color
            });
        }
        carbonPayload["carbonFootprintPerLifecycleStage"] = lifecycleRows;
        appCharts["carbonFootprint"] = carbonChart;
        carbonPayload["carbonFootprintStudy"] = EnsureDocument(appDocuments, "co2StudyReference").GetValue("url", string.Empty).ToString();

        var supplyAspect = EnsureAspectPayload(aspects, "supplyChainDueDiligence", now, "verified");
        var supplyPayload = EnsureDocument(supplyAspect, "payload");
        supplyPayload["supplyChainIndicies"] = Number(form, "supplyChainIndex", supplyPayload.GetValue("supplyChainIndicies", 0).ToDouble());
        supplyPayload["supplyChainDueDiligenceReport"] = EnsureDocument(appDocuments, "dueDiligenceReport").GetValue("url", string.Empty).ToString();
        supplyPayload["thirdPartyAussurances"] = EnsureDocument(appDocuments, "thirdPartyAudit").GetValue("url", string.Empty).ToString();
        supplyPayload["sustainabilityReport"] = EnsureDocument(appDocuments, "sustainabilityReport").GetValue("url", string.Empty).ToString();
        supplyPayload["taxonomyReport"] = EnsureDocument(appDocuments, "taxonomyReport").GetValue("url", string.Empty).ToString();

        var labelAspect = EnsureAspectPayload(aspects, "labeling", now, "verified");
        var labelPayload = EnsureDocument(labelAspect, "payload");
        labelPayload["resultOfTestReport"] = EnsureDocument(appDocuments, "conformityAssessment").GetValue("url", string.Empty).ToString();
        labelPayload["declarationOfConformity"] = EnsureDocument(appDocuments, "euDeclarationOfConformity").GetValue("url", string.Empty).ToString();

        var circularityAspect = EnsureAspectPayload(aspects, "circularity", now, "partial");
        var circularityPayload = EnsureDocument(circularityAspect, "payload");
        var endOfLifeInformation = EnsureDocument(circularityPayload, "endOfLifeInformation");
        endOfLifeInformation["separateCollection"] = Text(form, "separateCollection", endOfLifeInformation.GetValue("separateCollection", string.Empty).ToString());
        endOfLifeInformation["wastePrevention"] = Text(form, "wastePrevention", endOfLifeInformation.GetValue("wastePrevention", string.Empty).ToString());
        appCircularityNotes["separateCollection"] = endOfLifeInformation.GetValue("separateCollection", string.Empty).ToString();
        appCircularityNotes["wastePrevention"] = endOfLifeInformation.GetValue("wastePrevention", string.Empty).ToString();
        appCircularityNotes["recycledContentShareVerification"] = Text(form, "recycledContentShareVerification", "unverified");

        var recycledMaterials = new[]
        {
            ("recycledNickel", "Nickel"),
            ("recycledCobalt", "Cobalt"),
            ("recycledLithium", "Lithium"),
            ("recycledLead", "Lead")
        };
        var recycledChart = new BsonArray();
        var recycledAspectRows = new BsonArray();
        foreach (var (prefix, material) in recycledMaterials)
        {
            var pre = Number(form, $"{prefix}Pre", 0);
            var post = Number(form, $"{prefix}Post", 0);
            var primary = Number(form, $"{prefix}Primary", Math.Max(0, 100 - pre - post));
            recycledChart.Add(new BsonDocument
            {
                ["material"] = material,
                ["preConsumerShare"] = pre,
                ["postConsumerShare"] = post,
                ["primaryMaterialShare"] = primary
            });
            recycledAspectRows.Add(new BsonDocument
            {
                ["recycledMaterial"] = material,
                ["preConsumerShare"] = pre,
                ["postConsumerShare"] = post
            });
        }
        appCharts["recycledContent"] = recycledChart;
        circularityPayload["recycledContent"] = recycledAspectRows;

        var registryInfo = EnsureDocument(document, "registryInfo");
        registryInfo["status"] = Text(form, "status", registryInfo.GetValue("status", "draft").ToString());
        registryInfo["updatedAt"] = now;
        if (!registryInfo.Contains("createdAt"))
        {
            registryInfo["createdAt"] = now;
        }
        if (!registryInfo.Contains("registryId") || string.IsNullOrWhiteSpace(registryInfo.GetValue("registryId", string.Empty).ToString()))
        {
            registryInfo["registryId"] = Guid.NewGuid().ToString("N");
        }

        var validation = EnsureDocument(document, "validation");
        validation["isValid"] = true;
        validation["signedAt"] = now;
    }

    private static BsonDocument EnsureAspectPayload(BsonDocument aspects, string key, string now, string state)
    {
        var aspect = EnsureDocument(aspects, key);
        var verification = EnsureDocument(aspect, "verification");
        verification["state"] = state;
        verification["signedAt"] = now;
        if (!verification.Contains("issuer"))
        {
            verification["issuer"] = "did:web:acme.battery.pass:issuer";
        }
        EnsureDocument(aspect, "payload");
        if (!aspect.Contains("visibility"))
        {
            aspect["visibility"] = "public";
        }
        return aspect;
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

    private static string Text(IFormCollection form, string key, string fallback = "")
    {
        var value = form[key].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static double Number(IFormCollection form, string key, double fallback)
    {
        var text = Text(form, key);
        return double.TryParse(text, out var parsed) ? parsed : fallback;
    }

    private static string DateOnly(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        return value.Length >= 10 ? value[..10] : value;
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

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
