using BatteryPassWeb.Models.Trust;
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
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly PassportValidationService _passportValidationService;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;
    private readonly DemoRequiredDataCompletionService _demoRequiredDataCompletionService;
    private readonly PassportTrustService _passportTrustService;
    private readonly AuditRevisionService _auditRevisionService;

    public AdminController(
        PassportRepository passportRepository,
        ClusterRepository clusterRepository,
        PassportViewModelFactory viewModelFactory,
        ExternalApiRepository externalApiRepository,
        PassportValidationService passportValidationService,
        PassportPublishPolicyService passportPublishPolicyService,
        DemoRequiredDataCompletionService demoRequiredDataCompletionService,
        PassportTrustService passportTrustService,
        AuditRevisionService auditRevisionService)
    {
        _passportRepository = passportRepository;
        _clusterRepository = clusterRepository;
        _viewModelFactory = viewModelFactory;
        _externalApiRepository = externalApiRepository;
        _passportValidationService = passportValidationService;
        _passportPublishPolicyService = passportPublishPolicyService;
        _demoRequiredDataCompletionService = demoRequiredDataCompletionService;
        _passportTrustService = passportTrustService;
        _auditRevisionService = auditRevisionService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return Redirect("/admin/clusters?tab=passports");
    }

    [HttpGet("help")]
    public IActionResult Help()
    {
        return View();
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
        var requestedStatus = Text(form, "status", "draft");
        ApplyPassportForm(document, form, now);
        _passportPublishPolicyService.SanitizeTrustClaimsForDraftSave(document);
        document["passportId"] = passportId;
        document.Remove("_id");

        var validationSummary = _passportValidationService.Validate(document);
        var normalizedStatus = _passportPublishPolicyService.NormalizeRegistryStatus(requestedStatus, document, validationSummary);
        EnsureDocument(document, "registryInfo")["status"] = normalizedStatus;

        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        await _passportRepository.UpdateTrustValidationAsync(passportId, validationSummary, cancellationToken);
        var blockedPublishMessage = BuildBlockedPublishMessage(requestedStatus, normalizedStatus);
        var redirectUrl = $"/admin/passports/{Uri.EscapeDataString(passportId)}/edit?status=created";
        return string.IsNullOrWhiteSpace(blockedPublishMessage)
            ? Redirect(redirectUrl)
            : Redirect($"{redirectUrl}&error={Uri.EscapeDataString(blockedPublishMessage)}");
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
        var requestedStatus = Text(form, "status", BsonHelpers.GetString(document, "registryInfo", "status"));
        ApplyPassportForm(document, form, now);
        _passportPublishPolicyService.InvalidateValidationClaimForDraftSave(document);
        var validationSummary = _passportValidationService.Validate(document);
        var normalizedStatus = _passportPublishPolicyService.NormalizeRegistryStatus(requestedStatus, document, validationSummary);
        EnsureDocument(document, "registryInfo")["status"] = normalizedStatus;

        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        await _passportRepository.MarkCanonicalDirtyAsync(passportId, "adminPassportSave", cancellationToken);
        var blockedPublishMessage = BuildBlockedPublishMessage(requestedStatus, normalizedStatus);
        var redirectUrl = $"/admin/passports/{Uri.EscapeDataString(passportId)}/edit?status=saved";
        return string.IsNullOrWhiteSpace(blockedPublishMessage)
            ? Redirect(redirectUrl)
            : Redirect($"{redirectUrl}&error={Uri.EscapeDataString(blockedPublishMessage)}");
    }

    [HttpGet("passports/{passportId}/conformance")]
    public async Task<IActionResult> Conformance(string passportId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = BuildClusterDictionary(clusters);
        var summary = _passportValidationService.Validate(document);
        var publishDecision = _passportPublishPolicyService.Evaluate(document, summary);
        var verificationResult = _passportTrustService.Verify(document);

        return View(new ConformanceViewModel
        {
            Passport = _viewModelFactory.Create(document, clusterNamesById, verificationResult),
            ValidationSummary = summary,
            CanSign = publishDecision.CanSign,
            CanPublish = publishDecision.CanPublish,
            PublishBlockReason = publishDecision.PublishBlockReason,
            VerificationResult = verificationResult,
            StatusMessage = status switch
            {
                "validated" => "Passport validation completed.",
                "completed-data" => "Required demo data completed and validation recalculated. Sign once the page shows Can sign = Yes.",
                "signed" => "Passport signed and immutable revision recorded.",
                "published" => "Passport published from the latest verified revision.",
                _ => string.Empty
            },
            ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error)
        });
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

        var summary = _passportValidationService.Validate(document);
        await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
        return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/conformance?status=validated");
    }

    [HttpPost("passports/{passportId}/complete-required-data")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteRequiredData(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var actor = CurrentActor();
        var completed = _demoRequiredDataCompletionService.CompleteRequiredData(document);
        _passportPublishPolicyService.InvalidateValidationClaimForDraftSave(completed);
        var summary = _passportValidationService.Validate(completed);

        await _passportRepository.ReplaceAsync(passportId, completed, cancellationToken);
        await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
        await _passportRepository.MarkCanonicalDirtyAsync(passportId, "requiredDemoDataCompleted", cancellationToken);
        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.requiredData.completed",
            actor,
            "admin",
            "admin-ui",
            "Required schema demo data completed.",
            new BsonDocument
            {
                ["blockingErrors"] = summary.BlockingErrorCount,
                ["warnings"] = summary.WarningCount
            },
            cancellationToken);

        return Redirect(BuildConformanceRedirect(passportId, status: "completed-data"));
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

        var summary = _passportValidationService.Validate(document);
        if (!_passportPublishPolicyService.CanSign(summary))
        {
            await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
            await _auditRevisionService.AppendAuditEventAsync(
                passportId,
                "passport.sign.blocked",
                CurrentActor(),
                "admin",
                "admin-ui",
                "Passport signing blocked by validation errors.",
                new BsonDocument
                {
                    ["blockingErrors"] = summary.BlockingErrorCount,
                    ["warnings"] = summary.WarningCount
                },
                cancellationToken);
            return Redirect(BuildConformanceRedirect(passportId, error: "Resolve blocking validation errors before signing."));
        }

        var actor = CurrentActor();
        var signature = _passportTrustService.Sign(document, actor);
        var revision = await _auditRevisionService.CreateSignedRevisionAsync(
            passportId,
            signature.Snapshot,
            signature.Hash,
            signature.Proof,
            actor,
            signature.SignedAt,
            cancellationToken);
        var revisionId = BsonHelpers.GetString(revision, "revisionId");

        await _passportRepository.UpdateTrustSignatureAsync(
            passportId,
            summary,
            signature.Hash,
            signature.Proof,
            revisionId,
            signature.SignedAt,
            cancellationToken);
        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.signed",
            actor,
            "admin",
            "admin-ui",
            "Passport signed.",
            new BsonDocument
            {
                ["revisionId"] = revisionId,
                ["hash"] = $"sha256:{signature.Hash}",
                ["blockingErrors"] = summary.BlockingErrorCount,
                ["warnings"] = summary.WarningCount
            },
            cancellationToken);

        return Redirect(BuildConformanceRedirect(passportId, status: "signed"));
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

        var summary = _passportValidationService.Validate(document);
        if (!_passportPublishPolicyService.CanSign(summary))
        {
            await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
            return Redirect(BuildConformanceRedirect(passportId, error: "Resolve blocking validation errors before publishing."));
        }

        var verification = _passportTrustService.Verify(document);
        if (!verification.IsValid)
        {
            await _auditRevisionService.AppendAuditEventAsync(
                passportId,
                "passport.publish.blocked",
                CurrentActor(),
                "admin",
                "admin-ui",
                "Passport publishing blocked by signature verification.",
                new BsonDocument
                {
                    ["state"] = verification.State,
                    ["message"] = verification.Message,
                    ["currentHash"] = verification.CurrentHash,
                    ["expectedHash"] = verification.ExpectedHash
                },
                cancellationToken);
            return Redirect(BuildConformanceRedirect(passportId, error: verification.Message));
        }

        var revisionId = BsonHelpers.GetString(document, "trust", "latestRevisionId");
        if (string.IsNullOrWhiteSpace(revisionId))
        {
            return Redirect(BuildConformanceRedirect(passportId, error: "Publish requires a signed revision."));
        }

        var actor = CurrentActor();
        var publishedAt = DateTimeOffset.UtcNow.ToString("O");
        await _passportRepository.PublishPassportAsync(passportId, revisionId, publishedAt, cancellationToken);
        await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, publishedAt, cancellationToken);
        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.published",
            actor,
            "admin",
            "admin-ui",
            "Passport published.",
            new BsonDocument
            {
                ["revisionId"] = revisionId,
                ["hash"] = verification.CurrentHash,
                ["publishedAt"] = publishedAt
            },
            cancellationToken);

        return Redirect(BuildConformanceRedirect(passportId, status: "published"));
    }

    [HttpGet("passports/{passportId}/audit")]
    public async Task<IActionResult> Audit(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = BuildClusterDictionary(clusters);
        var auditEvents = await _auditRevisionService.ListAuditEventsAsync(passportId, cancellationToken);
        return View("Audit", new PassportAuditTrailViewModel
        {
            Passport = _viewModelFactory.Create(document, clusterNamesById, _passportTrustService.Verify(document)),
            AuditEvents = auditEvents
        });
    }

    [HttpGet("passports/{passportId}/revisions")]
    public async Task<IActionResult> Revisions(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = BuildClusterDictionary(clusters);
        var revisions = await _auditRevisionService.ListRevisionsAsync(passportId, cancellationToken);
        return View("Revisions", new PassportRevisionHistoryViewModel
        {
            Passport = _viewModelFactory.Create(document, clusterNamesById, _passportTrustService.Verify(document)),
            Revisions = revisions
        });
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
        var apiTokens = await _externalApiRepository.ListTokensAsync(cancellationToken);
        var batterySecrets = await _externalApiRepository.ListBatterySecretsAsync(cancellationToken: cancellationToken);

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
            }).ToList(),
            ApiTokens = apiTokens.Select(token =>
            {
                var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
                var clusterNames = tokenClusterIds
                    .Select(entry => entry.ToString() ?? string.Empty)
                    .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
                    .Select(clusterId => clusterNamesById.TryGetValue(clusterId, out var clusterName) ? $"{clusterName} ({clusterId})" : clusterId)
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
                    CreatedAt = BsonHelpers.GetString(token, "createdAt"),
                    UpdatedAt = BsonHelpers.GetString(token, "updatedAt"),
                    LastUsedAt = BsonHelpers.GetString(token, "lastUsedAt")
                };
            }).OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            BatterySecrets = batterySecrets.Select(secret =>
            {
                var clusterId = BsonHelpers.GetString(secret, "clusterId");
                return new BatterySecretViewModel
                {
                    PassportId = BsonHelpers.GetString(secret, "passportId"),
                    ClusterId = clusterId,
                    ClusterLabel = string.IsNullOrWhiteSpace(clusterId)
                        ? "No cluster assigned"
                        : clusterNamesById.TryGetValue(clusterId, out var clusterName)
                            ? clusterName
                            : clusterId,
                    IsActive = secret.GetValue("isActive", false).ToBoolean(),
                    CreatedAt = BsonHelpers.GetString(secret, "createdAt"),
                    UpdatedAt = BsonHelpers.GetString(secret, "updatedAt")
                };
            }).OrderBy(secret => secret.PassportId, StringComparer.OrdinalIgnoreCase).ToList(),
            SamplePassportId = ExternalApiInitializer.SamplePassportId,
            SampleReadToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadTokenId, ExternalApiInitializer.SampleReadTokenValue, cancellationToken),
            SampleReadWriteToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadWriteTokenId, ExternalApiInitializer.SampleReadWriteTokenValue, cancellationToken),
            StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
            ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty,
            GeneratedCredential = TempData["GeneratedCredential"]?.ToString() ?? string.Empty
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

    [HttpPost("api/tokens/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateApiToken(CancellationToken cancellationToken)
    {
        var name = Text(Request.Form, "name", "External API token");
        var accessModeText = Text(Request.Form, "accessMode", "read");
        var accessMode = accessModeText.Equals("readwrite", StringComparison.OrdinalIgnoreCase)
            ? ExternalTokenAccessMode.ReadWrite
            : ExternalTokenAccessMode.Read;
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
        var (_, tokenValue) = await _externalApiRepository.CreateTokenAsync(
            name,
            accessMode,
            clusterIds,
            allowUnassigned,
            globalAccess,
            string.IsNullOrWhiteSpace(actor) ? "admin" : actor,
            cancellationToken: cancellationToken);

        TempData["StatusMessage"] = $"API token \"{name}\" created.";
        TempData["GeneratedCredential"] = tokenValue;
        return Redirect("/admin/clusters?tab=api-tokens");
    }

    [HttpPost("api/tokens/set-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetApiTokenActive(CancellationToken cancellationToken)
    {
        var tokenId = Text(Request.Form, "tokenId");
        var isActive = Request.Form["isActive"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        var actor = AccessControlService.CurrentEmail(User);
        var success = await _externalApiRepository.SetTokenActiveAsync(tokenId, isActive, string.IsNullOrWhiteSpace(actor) ? "admin" : actor, cancellationToken);
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
        }

        return Redirect("/admin/clusters?tab=api-tokens");
    }

    [HttpPost("api/tokens/generate-per-cluster")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateClusterTokens(CancellationToken cancellationToken)
    {
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var actor = AccessControlService.CurrentEmail(User);
        var actorValue = string.IsNullOrWhiteSpace(actor) ? "admin" : actor;
        var generatedCount = 0;

        foreach (var cluster in clusters)
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            var clusterName = BsonHelpers.GetString(cluster, "name");
            if (string.IsNullOrWhiteSpace(clusterId))
            {
                continue;
            }

            await _externalApiRepository.CreateTokenAsync(
                $"{clusterName} - read",
                ExternalTokenAccessMode.Read,
                [clusterId],
                allowUnassigned: false,
                globalAccess: false,
                actor: actorValue,
                cancellationToken: cancellationToken);
            await _externalApiRepository.CreateTokenAsync(
                $"{clusterName} - readwrite",
                ExternalTokenAccessMode.ReadWrite,
                [clusterId],
                allowUnassigned: false,
                globalAccess: false,
                actor: actorValue,
                cancellationToken: cancellationToken);
            generatedCount += 2;
        }

        TempData["StatusMessage"] = $"Generated {generatedCount} cluster tokens.";
        return Redirect("/admin/clusters?tab=api-tokens");
    }

    [HttpPost("api/secrets/upsert")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpsertBatterySecret(CancellationToken cancellationToken)
    {
        var passportId = Text(Request.Form, "passportId");
        var active = Request.Form["isActive"].FirstOrDefault()?.Equals("on", StringComparison.OrdinalIgnoreCase) != false;
        if (string.IsNullOrWhiteSpace(passportId))
        {
            TempData["ErrorMessage"] = "Passport ID is required.";
            return Redirect("/admin/clusters?tab=battery-secrets");
        }

        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            TempData["ErrorMessage"] = $"Passport {passportId} does not exist.";
            return Redirect("/admin/clusters?tab=battery-secrets");
        }

        var clusterId = BsonHelpers.GetString(passport, "clusterId");
        var actor = AccessControlService.CurrentEmail(User);
        var secretValue = await _externalApiRepository.UpsertBatterySecretAsync(
            passportId,
            clusterId,
            string.IsNullOrWhiteSpace(actor) ? "admin" : actor,
            active,
            cancellationToken: cancellationToken);

        TempData["StatusMessage"] = $"Battery secret created/updated for {passportId}.";
        TempData["GeneratedCredential"] = secretValue;
        return Redirect("/admin/clusters?tab=battery-secrets");
    }

    [HttpPost("api/secrets/set-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetBatterySecretActive(CancellationToken cancellationToken)
    {
        var passportId = Text(Request.Form, "passportId");
        var isActive = Request.Form["isActive"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        var actor = AccessControlService.CurrentEmail(User);
        var success = await _externalApiRepository.SetBatterySecretActiveAsync(
            passportId,
            isActive,
            string.IsNullOrWhiteSpace(actor) ? "admin" : actor,
            cancellationToken);
        TempData[success ? "StatusMessage" : "ErrorMessage"] = success
            ? $"Battery secret for {passportId} updated."
            : $"Battery secret for {passportId} not found.";
        return Redirect("/admin/clusters?tab=battery-secrets");
    }

    [HttpPost("api/secrets/regenerate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateBatterySecret(CancellationToken cancellationToken)
    {
        var passportId = Text(Request.Form, "passportId");
        if (string.IsNullOrWhiteSpace(passportId))
        {
            TempData["ErrorMessage"] = "Passport ID is required.";
            return Redirect("/admin/clusters?tab=battery-secrets");
        }

        var passport = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (passport == null)
        {
            TempData["ErrorMessage"] = $"Passport {passportId} does not exist.";
            return Redirect("/admin/clusters?tab=battery-secrets");
        }

        var clusterId = BsonHelpers.GetString(passport, "clusterId");
        var actor = AccessControlService.CurrentEmail(User);
        var secretValue = await _externalApiRepository.UpsertBatterySecretAsync(
            passportId,
            clusterId,
            string.IsNullOrWhiteSpace(actor) ? "admin" : actor,
            active: true,
            cancellationToken: cancellationToken);
        TempData["StatusMessage"] = $"Battery secret for {passportId} regenerated.";
        TempData["GeneratedCredential"] = secretValue;
        return Redirect("/admin/clusters?tab=battery-secrets");
    }

    private static string NormalizeTab(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "battery" => "battery",
            "passports" => "passports",
            "clusters" => "clusters",
            "users" => "users",
            "api-tokens" => "api-tokens",
            "battery-secrets" => "battery-secrets",
            _ => "passports"
        };
    }

    private static string BuildBlockedPublishMessage(string requestedStatus, string normalizedStatus)
    {
        return requestedStatus.Equals("published", StringComparison.OrdinalIgnoreCase)
            && !normalizedStatus.Equals("published", StringComparison.OrdinalIgnoreCase)
                ? "Draft saved. Publishing is blocked until validation passes and a current signature proof exists."
                : string.Empty;
    }

    private static string BuildConformanceRedirect(string passportId, string status = "", string error = "")
    {
        var url = $"/admin/passports/{Uri.EscapeDataString(passportId)}/conformance";
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
        var actor = AccessControlService.CurrentEmail(User);
        return string.IsNullOrWhiteSpace(actor) ? "admin" : actor;
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

    private async Task<string> ResolveTokenValueAsync(string tokenId, string fallback, CancellationToken cancellationToken)
    {
        var tokenDocument = await _externalApiRepository.GetTokenByIdAsync(tokenId, cancellationToken);
        if (tokenDocument == null)
        {
            return fallback;
        }

        return _externalApiRepository.RevealToken(tokenDocument);
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

        var fallbackImageUrl = BatteryImageCatalog.NormalizeKnownImageUrl(
            media.GetValue("batteryImageUrl", BatteryImageCatalog.DefaultImageUrl).ToString(),
            BsonHelpers.GetString(document, "passportId"));
        var batteryImageUrl = BatteryImageCatalog.NormalizeKnownImageUrl(
            Text(form, "batteryImageUrl", fallbackImageUrl),
            BsonHelpers.GetString(document, "passportId"));
        media["batteryImageUrl"] = batteryImageUrl;
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
        var generalAspect = EnsureAspectPayload(aspects, "generalProductInformation", now, "draft");
        var generalPayload = EnsureDocument(generalAspect, "payload");
        generalPayload["productIdentifier"] = display.GetValue("modelNumber", string.Empty).ToString();
        generalPayload["batteryPassportIdentifier"] = $"urn:acme:{display.GetValue("serialNumber", string.Empty).ToString().ToLowerInvariant().Replace("-", string.Empty)}";
        generalPayload["batteryCategory"] = BatteryImageCatalog.CategoryForImageUrl(batteryImageUrl);
        generalPayload["batteryStatus"] = Text(form, "batteryStatus", generalPayload.GetValue("batteryStatus", "Original").ToString());
        generalPayload["batteryMass"] = Number(form, "batteryMass", generalPayload.GetValue("batteryMass", 0).ToDouble());
        generalPayload["manufacturingDate"] = $"{Text(form, "manufacturingDate", DateOnly(generalPayload.GetValue("manufacturingDate", string.Empty).ToString()))}T00:00:00.000Z";

        var materialAspect = EnsureAspectPayload(aspects, "materialComposition", now, "draft");
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

        var performanceAspect = EnsureAspectPayload(aspects, "performanceAndDurability", now, "draft");
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

        var carbonAspect = EnsureAspectPayload(aspects, "carbonFootprintForBatteries", now, "draft");
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

        var supplyAspect = EnsureAspectPayload(aspects, "supplyChainDueDiligence", now, "draft");
        var supplyPayload = EnsureDocument(supplyAspect, "payload");
        supplyPayload["supplyChainIndicies"] = Number(form, "supplyChainIndex", supplyPayload.GetValue("supplyChainIndicies", 0).ToDouble());
        supplyPayload["supplyChainDueDiligenceReport"] = EnsureDocument(appDocuments, "dueDiligenceReport").GetValue("url", string.Empty).ToString();
        supplyPayload["thirdPartyAussurances"] = EnsureDocument(appDocuments, "thirdPartyAudit").GetValue("url", string.Empty).ToString();
        supplyPayload["sustainabilityReport"] = EnsureDocument(appDocuments, "sustainabilityReport").GetValue("url", string.Empty).ToString();
        supplyPayload["taxonomyReport"] = EnsureDocument(appDocuments, "taxonomyReport").GetValue("url", string.Empty).ToString();

        var labelAspect = EnsureAspectPayload(aspects, "labeling", now, "draft");
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
        validation["isValid"] = false;
        validation["signedAt"] = BsonNull.Value;
    }

    private static BsonDocument EnsureAspectPayload(BsonDocument aspects, string key, string now, string state)
    {
        var aspect = EnsureDocument(aspects, key);
        var verification = EnsureDocument(aspect, "verification");
        verification["state"] = state;
        verification["signedAt"] = BsonNull.Value;
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
