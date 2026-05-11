using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BatteryPassWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("admin")]
public class AdminController : Controller
{
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
    private const string TrustWorkflowServiceErrorMessage = "The trust workflow could not be completed because MongoDB/service persistence is unavailable. No signed or published trust state was claimed. Please retry after the service is healthy.";

    private readonly PassportRepository _passportRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly PassportViewModelFactory _viewModelFactory;
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly PassportValidationService _passportValidationService;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;
    private readonly PassportReadinessService _passportReadinessService;
    private readonly PassportEvidenceService _passportEvidenceService;
    private readonly DataCompletionPolicyService _dataCompletionPolicyService;
    private readonly LocalAdminEditableFieldPolicyService _localAdminEditableFieldPolicyService;
    private readonly ProductTemplateService _productTemplateService;
    private readonly PassportTrustService _passportTrustService;
    private readonly AuditRevisionService _auditRevisionService;

    public AdminController(
        PassportRepository passportRepository,
        ClusterRepository clusterRepository,
        PassportViewModelFactory viewModelFactory,
        ExternalApiRepository externalApiRepository,
        PassportValidationService passportValidationService,
        PassportPublishPolicyService passportPublishPolicyService,
        PassportReadinessService passportReadinessService,
        PassportEvidenceService passportEvidenceService,
        DataCompletionPolicyService dataCompletionPolicyService,
        LocalAdminEditableFieldPolicyService localAdminEditableFieldPolicyService,
        ProductTemplateService productTemplateService,
        PassportTrustService passportTrustService,
        AuditRevisionService auditRevisionService)
    {
        _passportRepository = passportRepository;
        _clusterRepository = clusterRepository;
        _viewModelFactory = viewModelFactory;
        _externalApiRepository = externalApiRepository;
        _passportValidationService = passportValidationService;
        _passportPublishPolicyService = passportPublishPolicyService;
        _passportReadinessService = passportReadinessService;
        _passportEvidenceService = passportEvidenceService;
        _dataCompletionPolicyService = dataCompletionPolicyService;
        _localAdminEditableFieldPolicyService = localAdminEditableFieldPolicyService;
        _productTemplateService = productTemplateService;
        _passportTrustService = passportTrustService;
        _auditRevisionService = auditRevisionService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return Redirect("/admin/clusters?tab=passports");
    }

    [HttpGet("help")]
    public IActionResult Help([FromQuery] string? status, [FromQuery] string? error)
    {
        ViewData["StatusMessage"] = string.IsNullOrWhiteSpace(status) ? string.Empty : Uri.UnescapeDataString(status);
        ViewData["ErrorMessage"] = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error);
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
        var defaultProduct = BatteryProductTemplateCatalog.DefaultProduct;
        var defaultProductVersion = defaultProduct.LatestProductVersion;
        var document = await BuildDraftPassportDocumentAsync(
            draftPassportId,
            defaultProduct.ProductId,
            defaultProductVersion.Version,
            defaultProductVersion.SoftwareVersions.FirstOrDefault()?.Version ?? BatteryProductTemplateCatalog.DefaultSoftwareVersion,
            cancellationToken);
        var model = await BuildEditPassportModelAsync(
            document,
            "new",
            status == "created" ? "Passport created." : string.Empty,
            error,
            cancellationToken);

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

        var document = await BuildDraftPassportDocumentAsync(
            passportId,
            Text(form, "productId", BatteryProductTemplateCatalog.DefaultProductId),
            Text(form, "productVersion", BatteryProductTemplateCatalog.DefaultProduct.LatestProductVersion.Version),
            Text(form, "softwareVersion", BatteryProductTemplateCatalog.DefaultSoftwareVersion),
            cancellationToken);
        var now = DateTime.UtcNow.ToString("O");
        var requestedStatus = Text(form, "status", "draft");
        ApplyPassportForm(document, form, now);
        await ApplySelectedProductTemplateMetadataAsync(document, form, cancellationToken);
        _passportPublishPolicyService.SanitizeTrustClaimsForDraftSave(document);
        document["passportId"] = passportId;
        document.Remove("_id");

        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
        var validationSummary = _passportValidationService.Validate(document, dataRequirements);
        if (validationSummary.BlockingErrorCount > 0)
        {
            var model = await BuildEditPassportModelAsync(
                document,
                "new",
                string.Empty,
                BuildCreateBlockedMessage(validationSummary),
                cancellationToken);
            return View("EditPassport", model);
        }

        var normalizedStatus = _passportPublishPolicyService.NormalizeRegistryStatus(requestedStatus, document, validationSummary);
        EnsureDocument(document, "registryInfo")["status"] = normalizedStatus;

        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        await _passportRepository.UpdateTrustValidationAsync(passportId, validationSummary, cancellationToken);
        var blockedPublishMessage = BuildBlockedPublishMessage(requestedStatus, normalizedStatus);
        if (!string.IsNullOrWhiteSpace(blockedPublishMessage))
        {
            TempData["ErrorMessage"] = blockedPublishMessage;
        }

        TempData["StatusMessage"] = $"Passport {passportId} created.";
        return Redirect("/admin/clusters?tab=passports");
    }

    [HttpGet("passports/{passportId}/edit")]
    public async Task<IActionResult> EditPassport(string passportId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return NotFound();
        }

        var model = await BuildEditPassportModelAsync(
            document,
            "edit",
            status switch
            {
                "saved" => "Passport changes saved.",
                "created" => "Passport created.",
                _ => string.Empty
            },
            error,
            cancellationToken);

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

        var beforeSave = document.DeepClone().AsBsonDocument;
        var now = DateTime.UtcNow.ToString("O");
        var requestedStatus = Text(form, "status", BsonHelpers.GetString(document, "registryInfo", "status"));
        ApplyPassportForm(document, form, now);
        await ApplySelectedProductTemplateMetadataAsync(document, form, cancellationToken);
        _passportPublishPolicyService.InvalidateValidationClaimForDraftSave(document);
        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
        var validationSummary = _passportValidationService.Validate(document, dataRequirements);
        var normalizedStatus = _passportPublishPolicyService.NormalizeRegistryStatus(requestedStatus, document, validationSummary);
        EnsureDocument(document, "registryInfo")["status"] = normalizedStatus;

        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
        await _passportRepository.MarkCanonicalDirtyAsync(passportId, "adminPassportSave", cancellationToken);
        var changeMetadata = AuditRevisionService.BuildChangeMetadata(beforeSave, document, "adminPassportSave");
        var changedFields = changeMetadata.GetValue("changedFields", new BsonArray());
        changeMetadata["requestedStatus"] = requestedStatus;
        changeMetadata["normalizedStatus"] = normalizedStatus;
        changeMetadata["blockingErrors"] = validationSummary.BlockingErrorCount;
        changeMetadata["warnings"] = validationSummary.WarningCount;
        if (changedFields is BsonArray { Count: > 0 })
        {
            await _auditRevisionService.AppendAuditEventAsync(
                passportId,
                "passport.updated",
                CurrentActor(),
                "admin",
                "admin-edit-form",
                "Passport data updated from the admin form.",
                changeMetadata,
                cancellationToken);
        }

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
        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
        var validation = await ValidateWithEvidenceAsync(passportId, document, dataRequirements, cancellationToken);
        var summary = validation.Summary;
        var evidencePack = validation.EvidencePack;
        var publishDecision = _passportPublishPolicyService.Evaluate(document, summary);
        var verificationResult = _passportTrustService.Verify(document);
        var readiness = _passportReadinessService.Evaluate(document, summary, publishDecision, verificationResult);
        var groupedBlockingIssues = BuildIssueGroups(summary, TrustValidationSeverity.BlockingError);
        var groupedWarningIssues = BuildIssueGroups(summary, TrustValidationSeverity.Warning);

        return View(new ConformanceViewModel
        {
            Passport = _viewModelFactory.Create(document, clusterNamesById, verificationResult),
            ValidationSummary = summary,
            Readiness = readiness,
            EvidencePack = evidencePack,
            GroupedBlockingIssues = groupedBlockingIssues,
            GroupedWarningIssues = groupedWarningIssues,
            CanSign = publishDecision.CanSign,
            CanPublish = publishDecision.CanPublish,
            PublishBlockReason = publishDecision.PublishBlockReason,
            VerificationResult = verificationResult,
            StatusMessage = status switch
            {
                "validated" => "Passport validation completed.",
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

        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
        var validation = await ValidateWithEvidenceAsync(passportId, document, dataRequirements, cancellationToken);
        var summary = validation.Summary;
        await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.validated",
            CurrentActor(),
            "admin",
            "admin-ui",
            "Passport validation completed.",
            new BsonDocument
            {
                ["blockingErrors"] = summary.BlockingErrorCount,
                ["warnings"] = summary.WarningCount,
                ["passedChecks"] = summary.PassedCount,
                ["canSign"] = summary.CanSign
            },
            cancellationToken);
        return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/conformance?status=validated");
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

        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
        var validation = await ValidateWithEvidenceAsync(passportId, document, dataRequirements, cancellationToken);
        var summary = validation.Summary;
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

        try
        {
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

            var trustUpdated = await _passportRepository.UpdateTrustSignatureAsync(
                passportId,
                summary,
                signature.Hash,
                signature.Proof,
                revisionId,
                signature.SignedAt,
                cancellationToken);
            if (!trustUpdated)
            {
                throw new InvalidOperationException("Signing service could not persist the trust state after recording the signed revision.");
            }

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
        catch (Exception exception) when (IsTrustPersistenceFailure(exception))
        {
            return Redirect(BuildConformanceRedirect(passportId, error: $"{TrustWorkflowServiceErrorMessage} {exception.Message}"));
        }
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

        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
        var validation = await ValidateWithEvidenceAsync(passportId, document, dataRequirements, cancellationToken);
        var summary = validation.Summary;
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
        var publishedProof = BsonHelpers.GetValue(document, "trust", "latestProof") as BsonDocument ?? new BsonDocument();
        try
        {
            var published = await _passportRepository.PublishPassportAsync(
                passportId,
                revisionId,
                publishedAt,
                verification.CurrentHash,
                publishedProof,
                cancellationToken);
            if (!published)
            {
                throw new InvalidOperationException("Publishing service could not persist the published trust state.");
            }

            var revisionMarked = await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, publishedAt, cancellationToken);
            if (!revisionMarked)
            {
                throw new InvalidOperationException("Publishing service could not mark the immutable revision as published.");
            }

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
        catch (Exception exception) when (IsTrustPersistenceFailure(exception))
        {
            return Redirect(BuildConformanceRedirect(passportId, error: $"{TrustWorkflowServiceErrorMessage} {exception.Message}"));
        }
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

    [HttpGet("products/new")]
    public async Task<IActionResult> NewProduct([FromQuery] string? error, CancellationToken cancellationToken)
    {
        await _productTemplateService.EnsureDefaultTemplatesAsync(CurrentActor(), cancellationToken);
        var starter = BatteryProductTemplateCatalog.DefaultProduct with
        {
            ProductId = string.Empty,
            ProductName = string.Empty,
            Description = string.Empty
        };
        var dataRequirements = DataCompletionPolicyService.CreateDefaultPolicy();
        return View("Product", BuildProductEditModel(starter, dataRequirements, string.Empty, error));
    }

    [HttpGet("products/{productId}")]
    public async Task<IActionResult> Product(string productId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        await _productTemplateService.EnsureDefaultTemplatesAsync(CurrentActor(), cancellationToken);
        var product = await _productTemplateService.GetProductAsync(productId, cancellationToken);
        if (product == null)
        {
            return NotFound();
        }

        var dataRequirements = await _dataCompletionPolicyService.GetProductPolicyAsync(product.ProductId, cancellationToken);
        return View("Product", BuildProductEditModel(product, dataRequirements, status, error));
    }

    [HttpPost("products/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProduct(CancellationToken cancellationToken)
    {
        var form = Request.Form;
        var productId = Text(form, "productId").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(productId))
        {
            return Redirect($"/admin/products/new?error={Uri.EscapeDataString("Product ID is required.")}");
        }

        var existing = await _productTemplateService.GetProductAsync(productId, cancellationToken)
            ?? BatteryProductTemplateCatalog.FindProduct(productId)
            ?? (BatteryProductTemplateCatalog.DefaultProduct with { ProductId = productId });
        var product = BuildProductTemplateFromForm(form, existing);
        await _productTemplateService.SaveProductAsync(product, CurrentActor(), cancellationToken);
        return Redirect($"/admin/products/{Uri.EscapeDataString(product.ProductId)}?status={Uri.EscapeDataString("Product template saved. Push a software version when you want matching batteries to receive safe template changes.")}");
    }

    [HttpPost("products/{productId}/software/{softwareVersion}/push")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PushProductTemplate(string productId, string softwareVersion, CancellationToken cancellationToken)
    {
        var result = await _productTemplateService.PushTemplateAsync(productId, softwareVersion, CurrentActor(), cancellationToken);
        var message = $"Template push finished: {result.UpdatedBatteries} of {result.MatchedBatteries} matching batteries updated. Manual overrides were preserved.";
        return Redirect($"/admin/products/{Uri.EscapeDataString(productId)}?status={Uri.EscapeDataString(message)}");
    }

    [HttpPost("products/{productId}/versions/{productVersion}/software/{softwareVersion}/push")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PushProductTemplateVersion(string productId, string productVersion, string softwareVersion, CancellationToken cancellationToken)
    {
        var result = await _productTemplateService.PushTemplateAsync(productId, productVersion, softwareVersion, CurrentActor(), cancellationToken);
        var message = $"Template push finished: {result.UpdatedBatteries} of {result.MatchedBatteries} matching batteries updated. Manual overrides were preserved.";
        return Redirect($"/admin/products/{Uri.EscapeDataString(productId)}?status={Uri.EscapeDataString(message)}");
    }

    [HttpPost("products/{productId}/versions/{productVersion}/push")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PushProductVersionTemplate(string productId, string productVersion, CancellationToken cancellationToken)
    {
        var result = await _productTemplateService.PushProductVersionAsync(productId, productVersion, CurrentActor(), cancellationToken);
        var message = $"Product/battery version push finished: {result.UpdatedBatteries} of {result.MatchedBatteries} matching batteries updated. Manual overrides were preserved.";
        return Redirect($"/admin/products/{Uri.EscapeDataString(productId)}?status={Uri.EscapeDataString(message)}");
    }

    [HttpPost("product-templates/reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetProductTemplateDemo(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _productTemplateService.ResetTemplateDemoAsync(CurrentActor(), cancellationToken);
            TempData["StatusMessage"] = $"Product template demo reset completed: {result.PassportCount} passports restored from MongoDB product templates.";
            return Redirect("/admin/clusters?tab=products");
        }
        catch (Exception exception) when (IsTrustPersistenceFailure(exception))
        {
            TempData["ErrorMessage"] = $"{TrustWorkflowServiceErrorMessage} {exception.Message}";
            return Redirect("/admin/clusters?tab=products");
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
            return Redirect("/admin/clusters?tab=products");
        }
    }

    [HttpGet("clusters")]
    public async Task<IActionResult> Clusters([FromQuery] string? tab, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        var selectedTab = NormalizeTab(tab);
        var selectedCredentialTab = NormalizeCredentialTab(tab);
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
        var needsPassports = selectedTab is "passports" or "battery" or "api-token-management";
        var needsUsers = selectedTab == "users";
        var needsCredentials = selectedTab == "api-token-management";
        var needsProducts = selectedTab == "products";
        var needsLocalEditablePolicy = selectedTab == "local-editable-fields";

        IReadOnlyList<PassportSummaryViewModel> passports = needsPassports
            ? await _passportRepository.SearchAsync(q ?? string.Empty, includeArchived: true, cancellationToken)
            : Array.Empty<PassportSummaryViewModel>();
        IReadOnlyList<BsonDocument> users = needsUsers
            ? await _clusterRepository.ListUsersAsync(cancellationToken)
            : Array.Empty<BsonDocument>();
        IReadOnlyList<BsonDocument> memberships = needsUsers
            ? await _clusterRepository.ListClusterMembershipsAsync(cancellationToken)
            : Array.Empty<BsonDocument>();
        IReadOnlyList<BsonDocument> apiTokens = needsCredentials
            ? await _externalApiRepository.ListTokensAsync(cancellationToken)
            : Array.Empty<BsonDocument>();
        IReadOnlyList<BsonDocument> batterySecrets = needsCredentials
            ? await _externalApiRepository.ListBatterySecretsAsync(cancellationToken: cancellationToken)
            : Array.Empty<BsonDocument>();
        IReadOnlyList<BatteryProductTemplate> productTemplates = needsProducts
            ? await _productTemplateService.ListProductsAsync(cancellationToken)
            : Array.Empty<BatteryProductTemplate>();
        var localEditableFieldPolicy = needsLocalEditablePolicy
            ? await _localAdminEditableFieldPolicyService.GetPolicyAsync(cancellationToken)
            : LocalAdminEditableFieldPolicyService.CreateDefaultPolicy();

        var model = new AdminClusterViewModel
        {
            SelectedTab = selectedTab,
            SelectedCredentialTab = selectedCredentialTab,
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
            ProductTemplates = BuildProductTemplateSummaries(productTemplates),
            LocalEditableFieldPolicy = localEditableFieldPolicy,
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

    [HttpPost("local-editable-fields/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLocalEditableFields(CancellationToken cancellationToken)
    {
        var editableFieldKeys = Request.Form["editableFieldKeys"]
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await _localAdminEditableFieldPolicyService.SavePolicyAsync(editableFieldKeys, CurrentActor(), cancellationToken);
        TempData["StatusMessage"] = "Local editable fields updated.";
        return Redirect("/admin/clusters?tab=local-editable-fields");
    }

    private static string NormalizeTab(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "battery" => "battery",
            "passports" => "passports",
            "clusters" => "clusters",
            "users" => "users",
            "api-token-management" or "api-tokens" or "battery-secrets" => "api-token-management",
            "local-editable-fields" => "local-editable-fields",
            "products" => "products",
            _ => "passports"
        };
    }

    private static string NormalizeCredentialTab(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "battery-secrets" => "battery-secrets",
            _ => "api-tokens"
        };
    }

    private static string BuildBlockedPublishMessage(string requestedStatus, string normalizedStatus)
    {
        return requestedStatus.Equals("published", StringComparison.OrdinalIgnoreCase)
            && !normalizedStatus.Equals("published", StringComparison.OrdinalIgnoreCase)
                ? "Draft saved. Publishing is blocked until validation passes and a current signature proof exists."
                : string.Empty;
    }

    private static string BuildCreateBlockedMessage(TrustValidationSummary summary)
    {
        var blockers = summary.Sections
            .SelectMany(section => section.Issues)
            .Where(issue => issue.Severity == TrustValidationSeverity.BlockingError)
            .Select(issue => string.IsNullOrWhiteSpace(issue.Path)
                ? issue.Message
                : $"{issue.Path}: {issue.Message}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        var details = blockers.Count == 0
            ? string.Empty
            : $" Missing or invalid: {string.Join("; ", blockers)}";
        return $"Create passport is blocked until {summary.BlockingErrorCount} required or invalid field(s) are fixed.{details}";
    }

    private async Task<(TrustValidationSummary Summary, EvidencePackResult EvidencePack)> ValidateWithEvidenceAsync(
        string passportId,
        BsonDocument document,
        DataCompletionPolicySnapshot dataRequirements,
        CancellationToken cancellationToken)
    {
        var summary = _passportValidationService.Validate(document, dataRequirements);
        var latestRevision = await _auditRevisionService.GetLatestSignedRevisionAsync(passportId, cancellationToken);
        var evidencePack = _passportEvidenceService.Evaluate(document, latestRevision, dataRequirements);
        summary = PassportEvidenceService.AppendValidationSection(summary, evidencePack);
        return (summary, evidencePack);
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

    private static bool IsTrustPersistenceFailure(Exception exception)
    {
        return exception is InvalidOperationException or MongoException or TimeoutException;
    }

    private static IReadOnlyList<ConformanceIssueGroupViewModel> BuildIssueGroups(
        TrustValidationSummary summary,
        TrustValidationSeverity severity)
    {
        return summary.Sections
            .Select(section => new ConformanceIssueGroupViewModel
            {
                SectionKey = section.SectionKey,
                SectionLabel = section.SectionLabel,
                EditAnchor = AdminSectionAnchor(section.SectionKey),
                Issues = section.Issues
                    .Where(issue => issue.Severity == severity)
                    .ToList()
            })
            .Where(group => group.Issues.Count > 0)
            .ToList();
    }

    private static string AdminSectionAnchor(string sectionKey)
    {
        return sectionKey switch
        {
            "generalProductInformation" or "identity" or "dataCompletionPolicy" => "admin-general",
            "materialComposition" => "admin-material-composition",
            "performanceAndDurability" => "admin-performance",
            "labeling" => "admin-compliance",
            "supplyChainDueDiligence" => "admin-supply-chain",
            "circularity" => "admin-circularity",
            "carbonFootprintForBatteries" => "admin-carbon-footprint",
            _ => "admin-general"
        };
    }

    private static IReadOnlyDictionary<string, DataRequirementField> BuildFieldRequirementDictionary(DataCompletionPolicySnapshot dataRequirements)
    {
        return dataRequirements.Sections
            .SelectMany(section => section.Fields)
            .ToDictionary(field => field.FieldKey, field => field, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<EditPassportViewModel> BuildEditPassportModelAsync(
        BsonDocument document,
        string mode,
        string statusMessage,
        string? error,
        CancellationToken cancellationToken)
    {
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var clusterNamesById = BuildClusterDictionary(clusters);
        var products = await _productTemplateService.ListProductsAsync(cancellationToken);
        var selectedProductId = FirstNonEmpty(
            BsonHelpers.GetString(document, "app", "product", "productId"),
            BatteryProductTemplateCatalog.DefaultProductId);
        var selectedProduct = products.FirstOrDefault(product => product.ProductId.Equals(selectedProductId, StringComparison.OrdinalIgnoreCase))
            ?? BatteryProductTemplateCatalog.DefaultProduct;
        var selectedProductVersion = FirstNonEmpty(
            BsonHelpers.GetString(document, "app", "product", "productVersion"),
            selectedProduct.LatestProductVersion.Version);
        var selectedVersion = selectedProduct.ProductVersions.FirstOrDefault(version => version.Version.Equals(selectedProductVersion, StringComparison.OrdinalIgnoreCase))
            ?? selectedProduct.LatestProductVersion;
        var selectedSoftwareVersion = FirstNonEmpty(
            BsonHelpers.GetString(document, "app", "product", "softwareVersion"),
            selectedVersion.SoftwareVersions.FirstOrDefault()?.Version ?? BatteryProductTemplateCatalog.DefaultSoftwareVersion);
        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);

        return new EditPassportViewModel
        {
            Passport = _viewModelFactory.Create(document, clusterNamesById),
            Mode = mode,
            DataRequirements = dataRequirements,
            ProductTemplates = BuildProductTemplateSummaries(products),
            ProductTemplateCatalog = BuildProductTemplateFormCatalog(products),
            ProductSoftwareVersions = selectedVersion.SoftwareVersions
                .Select(software => new ProductSoftwareVersionViewModel
                {
                    Version = software.Version,
                    ReleaseDate = software.ReleaseDate,
                    LatestUpdate = software.LatestUpdate
                })
                .ToList(),
            SelectedProductId = selectedProduct.ProductId,
            SelectedProductVersion = selectedVersion.Version,
            SelectedSoftwareVersion = selectedSoftwareVersion,
            FieldRequirementByKey = BuildFieldRequirementDictionary(dataRequirements),
            StatusMessage = statusMessage,
            ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error)
        };
    }

    private static IReadOnlyList<ProductTemplateSummaryViewModel> BuildProductTemplateSummaries(IEnumerable<BatteryProductTemplate> products)
    {
        return products
            .Select(product => new ProductTemplateSummaryViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Description = product.Description,
                ImageUrl = product.ImageUrl,
                ModuleCount = product.ModuleCount,
                SoftwareVersionCount = product.SoftwareVersions.Count,
                RequiredFieldCount = product.RequiredFieldKeys.Count,
                DocumentCount = product.TemplateDocuments.Count
            })
            .OrderBy(product => product.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ProductTemplateEditViewModel BuildProductEditModel(
        BatteryProductTemplate product,
        DataCompletionPolicySnapshot dataRequirements,
        string? status,
        string? error)
    {
        var productVersions = BuildProductVersionEditModels(product.ProductVersions.Count == 0
            ? [product.LatestProductVersion]
            : product.ProductVersions);
        var currentVersion = productVersions.FirstOrDefault();

        return new ProductTemplateEditViewModel
        {
            ProductId = product.ProductId,
            ProductName = product.ProductName,
            Description = product.Description,
            ImageUrl = product.ImageUrl,
            ModuleCount = product.ModuleCount,
            BatteryMassKg = currentVersion?.BatteryMassKg ?? product.BatteryMassKg,
            RatedEnergyKwh = currentVersion?.RatedEnergyKwh ?? product.RatedEnergyKwh,
            RatedCapacityAh = currentVersion?.RatedCapacityAh ?? product.RatedCapacityAh,
            RatedMaximumPowerKw = currentVersion?.RatedMaximumPowerKw ?? product.RatedMaximumPowerKw,
            NominalVoltageV = currentVersion?.NominalVoltageV ?? product.NominalVoltageV,
            ExpectedLifetimeYears = currentVersion?.ExpectedLifetimeYears ?? product.ExpectedLifetimeYears,
            ExpectedCycles = currentVersion?.ExpectedCycles ?? product.ExpectedCycles,
            SupplyChainIndex = currentVersion?.SupplyChainIndex ?? product.SupplyChainIndex,
            CarbonFootprint = currentVersion?.CarbonFootprint ?? product.CarbonFootprint,
            PerformanceClass = currentVersion?.PerformanceClass ?? product.PerformanceClass,
            MaterialMassesKg = currentVersion?.MaterialMassesKg ?? product.MaterialMassesKg,
            CarbonStages = currentVersion?.CarbonStages ?? product.CarbonStages,
            RecycledContent = currentVersion?.RecycledContent ?? product.RecycledContent.ToDictionary(
                pair => pair.Key,
                pair => new ProductTemplateRecycledContentViewModel
                {
                    PreConsumerShare = pair.Value.PreConsumerShare,
                    PostConsumerShare = pair.Value.PostConsumerShare
                },
                StringComparer.OrdinalIgnoreCase),
            SoftwareVersions = currentVersion?.SoftwareVersions ?? product.SoftwareVersions
                .Select(software => new ProductSoftwareVersionViewModel
                {
                    Version = software.Version,
                    ReleaseDate = software.ReleaseDate,
                    LatestUpdate = software.LatestUpdate
                })
                .ToList(),
            DataRequirements = dataRequirements,
            StatusMessage = string.IsNullOrWhiteSpace(status) ? string.Empty : Uri.UnescapeDataString(status),
            ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error),
            ProductVersions = productVersions,
            BaseProductCatalog = BuildProductTemplateFormCatalog(BatteryProductTemplateCatalog.DefaultProducts)
        };
    }

    private static IReadOnlyList<ProductVersionEditViewModel> BuildProductVersionEditModels(IEnumerable<BatteryProductVersion> productVersions)
    {
        return productVersions
            .OrderBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
            .Reverse()
            .Select(version => new ProductVersionEditViewModel
            {
                Version = version.Version,
                BatteryMassKg = version.BatteryMassKg,
                RatedEnergyKwh = version.RatedEnergyKwh,
                RatedCapacityAh = version.RatedCapacityAh,
                RatedMaximumPowerKw = version.RatedMaximumPowerKw,
                NominalVoltageV = version.NominalVoltageV,
                ExpectedLifetimeYears = version.ExpectedLifetimeYears,
                ExpectedCycles = version.ExpectedCycles,
                SupplyChainIndex = version.SupplyChainIndex,
                CarbonFootprint = version.CarbonFootprint,
                PerformanceClass = version.PerformanceClass,
                MaterialMassesKg = version.MaterialMassesKg,
                CarbonStages = version.CarbonStages,
                RecycledContent = version.RecycledContent.ToDictionary(
                    pair => pair.Key,
                    pair => new ProductTemplateRecycledContentViewModel
                    {
                        PreConsumerShare = pair.Value.PreConsumerShare,
                        PostConsumerShare = pair.Value.PostConsumerShare
                    },
                    StringComparer.OrdinalIgnoreCase),
                SoftwareVersions = version.SoftwareVersions
                    .Select(software => new ProductSoftwareVersionViewModel
                    {
                        Version = software.Version,
                        ReleaseDate = software.ReleaseDate,
                        LatestUpdate = software.LatestUpdate
                    })
                    .ToList()
            })
            .ToList();
    }

    private static BatteryProductTemplate BuildProductTemplateFromForm(IFormCollection form, BatteryProductTemplate existing)
    {
        var requiredFieldKeys = form["requiredFieldKeys"]
            .Select(value => value ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        var productVersions = ReadProductVersions(form, existing, requiredFieldKeys);
        var latestProductVersion = productVersions.FirstOrDefault() ?? existing.LatestProductVersion;

        return existing with
        {
            ProductId = Text(form, "productId", existing.ProductId).Trim().ToLowerInvariant(),
            ProductName = Text(form, "productName", existing.ProductName),
            Description = Text(form, "description", existing.Description),
            ImageUrl = BatteryImageCatalog.NormalizeKnownImageUrl(Text(form, "imageUrl", existing.ImageUrl), string.Empty),
            ModuleCount = (int)Math.Max(1, Number(form, "moduleCount", existing.ModuleCount)),
            BatteryMassKg = latestProductVersion.BatteryMassKg,
            RatedEnergyKwh = latestProductVersion.RatedEnergyKwh,
            RatedCapacityAh = latestProductVersion.RatedCapacityAh,
            RatedMaximumPowerKw = latestProductVersion.RatedMaximumPowerKw,
            NominalVoltageV = latestProductVersion.NominalVoltageV,
            ExpectedLifetimeYears = latestProductVersion.ExpectedLifetimeYears,
            ExpectedCycles = latestProductVersion.ExpectedCycles,
            SupplyChainIndex = latestProductVersion.SupplyChainIndex,
            CarbonFootprint = latestProductVersion.CarbonFootprint,
            PerformanceClass = latestProductVersion.PerformanceClass,
            MaterialMassesKg = latestProductVersion.MaterialMassesKg,
            CarbonStages = latestProductVersion.CarbonStages,
            RecycledContent = latestProductVersion.RecycledContent,
            SoftwareVersions = latestProductVersion.SoftwareVersions,
            RequiredFieldKeys = requiredFieldKeys,
            ProductVersions = productVersions
        };
    }

    private static IReadOnlyList<BatteryProductVersion> ReadProductVersions(
        IFormCollection form,
        BatteryProductTemplate existing,
        IReadOnlyList<string> requiredFieldKeys)
    {
        var payloadJson = Text(form, "productVersionsJson");
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            try
            {
                var payloads = JsonSerializer.Deserialize<List<ProductVersionFormPayload>>(
                    payloadJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                var parsed = payloads
                    .Select(payload => BuildProductVersionFromPayload(payload, existing, requiredFieldKeys))
                    .Where(version => !string.IsNullOrWhiteSpace(version.Version))
                    .GroupBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(version => version.Version, VersionStringComparer.Descending)
                    .ToList();
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Fall back to the visible form fields below so a malformed client payload does not drop edits.
            }
        }

        var softwareVersions = ReadSoftwareVersions(form, existing.LatestProductVersion.SoftwareVersions);
        var productVersion = new BatteryProductVersion(
            Text(form, "productVersion", existing.LatestProductVersion.Version),
            Number(form, "batteryMassKg", existing.LatestProductVersion.BatteryMassKg),
            Number(form, "ratedEnergyKwh", existing.LatestProductVersion.RatedEnergyKwh),
            Number(form, "ratedCapacityAh", existing.LatestProductVersion.RatedCapacityAh),
            Number(form, "ratedMaximumPowerKw", existing.LatestProductVersion.RatedMaximumPowerKw),
            Number(form, "nominalVoltageV", existing.LatestProductVersion.NominalVoltageV),
            Number(form, "expectedLifetimeYears", existing.LatestProductVersion.ExpectedLifetimeYears),
            Number(form, "expectedCycles", existing.LatestProductVersion.ExpectedCycles),
            BatteryPassCanonicalDataCatalog.NormalizeSupplyChainIndex(Number(form, "supplyChainIndex", existing.LatestProductVersion.SupplyChainIndex)),
            BatteryPassCanonicalDataCatalog.NormalizeCarbonFootprint(Number(form, "carbonFootprint", existing.LatestProductVersion.CarbonFootprint)),
            BatteryPassCanonicalDataCatalog.NormalizePerformanceClass(Text(form, "performanceClass", existing.LatestProductVersion.PerformanceClass)),
            ReadNumberMap(form, "materialName", "materialMass", existing.LatestProductVersion.MaterialMassesKg),
            ReadNumberMap(form, "carbonStage", "carbonStageValue", existing.LatestProductVersion.CarbonStages),
            ReadRecycledContentMap(form, existing.LatestProductVersion.RecycledContent),
            softwareVersions,
            existing.LatestProductVersion.TemplateDocuments,
            requiredFieldKeys);

        return [productVersion];
    }

    private static BatteryProductVersion BuildProductVersionFromPayload(
        ProductVersionFormPayload payload,
        BatteryProductTemplate existing,
        IReadOnlyList<string> requiredFieldKeys)
    {
        var fallback = existing.ProductVersions.FirstOrDefault(version =>
                version.Version.Equals(payload.Version ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?? existing.LatestProductVersion;
        var softwareVersions = (payload.SoftwareVersions ?? [])
            .Select(software =>
            {
                var fallbackSoftware = fallback.SoftwareVersions.FirstOrDefault(item =>
                    item.Version.Equals(software.Version ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                return new BatteryProductSoftwareVersion(
                    software.Version?.Trim() ?? string.Empty,
                    DateOnly(software.ReleaseDate ?? fallbackSoftware?.ReleaseDate),
                    DateOnly(software.LatestUpdate ?? fallbackSoftware?.LatestUpdate));
            })
            .Where(software => !string.IsNullOrWhiteSpace(software.Version))
            .GroupBy(software => software.Version, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(software => software.Version, VersionStringComparer.Descending)
            .ToList();

        return new BatteryProductVersion(
            payload.Version?.Trim() ?? fallback.Version,
            payload.BatteryMassKg,
            payload.RatedEnergyKwh,
            payload.RatedCapacityAh,
            payload.RatedMaximumPowerKw,
            payload.NominalVoltageV,
            payload.ExpectedLifetimeYears,
            payload.ExpectedCycles,
            BatteryPassCanonicalDataCatalog.NormalizeSupplyChainIndex(payload.SupplyChainIndex),
            BatteryPassCanonicalDataCatalog.NormalizeCarbonFootprint(payload.CarbonFootprint),
            BatteryPassCanonicalDataCatalog.NormalizePerformanceClass(payload.PerformanceClass ?? fallback.PerformanceClass),
            payload.MaterialMassesKg?.Count > 0 ? payload.MaterialMassesKg : fallback.MaterialMassesKg,
            payload.CarbonStages?.Count > 0 ? payload.CarbonStages : fallback.CarbonStages,
            ToRecycledContent(payload.RecycledContent, fallback.RecycledContent),
            softwareVersions.Count == 0 ? fallback.SoftwareVersions : softwareVersions,
            fallback.TemplateDocuments,
            requiredFieldKeys);
    }

    private static IReadOnlyDictionary<string, ProductTemplateRecycledContent> ToRecycledContent(
        Dictionary<string, ProductTemplateRecycledContentFormPayload>? payload,
        IReadOnlyDictionary<string, ProductTemplateRecycledContent> fallback)
    {
        if (payload == null || payload.Count == 0)
        {
            return fallback;
        }

        return payload.ToDictionary(
            pair => pair.Key,
            pair => new ProductTemplateRecycledContent(pair.Value.PreConsumerShare, pair.Value.PostConsumerShare),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ProductTemplateFormCatalogItemViewModel> BuildProductTemplateFormCatalog(IEnumerable<BatteryProductTemplate> products)
    {
        return products
            .Select(product => new ProductTemplateFormCatalogItemViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                ImageUrl = product.ImageUrl,
                ModuleCount = product.ModuleCount,
                BatteryMassKg = product.BatteryMassKg,
                RatedEnergyKwh = product.RatedEnergyKwh,
                RatedCapacityAh = product.RatedCapacityAh,
                RatedMaximumPowerKw = product.RatedMaximumPowerKw,
                NominalVoltageV = product.NominalVoltageV,
                ExpectedLifetimeYears = product.ExpectedLifetimeYears,
                ExpectedCycles = product.ExpectedCycles,
                SupplyChainIndex = product.SupplyChainIndex,
                CarbonFootprint = product.CarbonFootprint,
                PerformanceClass = product.PerformanceClass,
                MaterialMassesKg = product.MaterialMassesKg,
                CarbonStages = product.CarbonStages,
                RecycledContent = product.RecycledContent.ToDictionary(
                    pair => pair.Key,
                    pair => new ProductTemplateRecycledContentViewModel
                    {
                        PreConsumerShare = pair.Value.PreConsumerShare,
                        PostConsumerShare = pair.Value.PostConsumerShare
                    },
                    StringComparer.OrdinalIgnoreCase),
                SoftwareVersions = product.SoftwareVersions
                    .Select(software => new ProductSoftwareVersionViewModel
                    {
                        Version = software.Version,
                        ReleaseDate = software.ReleaseDate,
                        LatestUpdate = software.LatestUpdate
                    })
                    .ToList(),
                ProductVersions = BuildProductVersionEditModels(product.ProductVersions.Count == 0
                    ? [product.LatestProductVersion]
                    : product.ProductVersions)
            })
            .ToList();
    }

    private static IReadOnlyDictionary<string, double> ReadNumberMap(
        IFormCollection form,
        string nameKey,
        string valueKey,
        IReadOnlyDictionary<string, double> fallback)
    {
        var names = form[nameKey];
        var values = form[valueKey];
        if (names.Count == 0 || values.Count == 0)
        {
            return fallback;
        }

        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < names.Count; index++)
        {
            var name = names[index]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var fallbackValue = fallback.TryGetValue(name, out var existingValue) ? existingValue : 0;
            result[name] = double.TryParse(index < values.Count ? values[index] : string.Empty, out var parsed)
                ? parsed
                : fallbackValue;
        }

        return result.Count == 0 ? fallback : result;
    }

    private static IReadOnlyDictionary<string, ProductTemplateRecycledContent> ReadRecycledContentMap(
        IFormCollection form,
        IReadOnlyDictionary<string, ProductTemplateRecycledContent> fallback)
    {
        var result = new Dictionary<string, ProductTemplateRecycledContent>(StringComparer.OrdinalIgnoreCase);
        foreach (var material in new[] { "Nickel", "Cobalt", "Lithium", "Lead" })
        {
            var fallbackValue = fallback.TryGetValue(material, out var existing)
                ? existing
                : new ProductTemplateRecycledContent(0, 0);
            result[material] = new ProductTemplateRecycledContent(
                Number(form, $"recycled{material}Pre", fallbackValue.PreConsumerShare),
                Number(form, $"recycled{material}Post", fallbackValue.PostConsumerShare));
        }

        return result;
    }

    private static IReadOnlyList<BatteryProductSoftwareVersion> ReadSoftwareVersions(
        IFormCollection form,
        IReadOnlyList<BatteryProductSoftwareVersion> fallback)
    {
        var versions = form["softwareVersion"];
        var releases = form["softwareReleaseDate"];
        var updates = form["softwareLatestUpdate"];
        if (versions.Count == 0)
        {
            return fallback;
        }

        var result = new List<BatteryProductSoftwareVersion>();
        for (var index = 0; index < versions.Count; index++)
        {
            var version = versions[index]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            var fallbackVersion = fallback.FirstOrDefault(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
            result.Add(new BatteryProductSoftwareVersion(
                version,
                DateOnly(index < releases.Count ? releases[index] : fallbackVersion?.ReleaseDate),
                DateOnly(index < updates.Count ? updates[index] : fallbackVersion?.LatestUpdate)));
        }

        return result.Count == 0
            ? fallback
            : result.OrderBy(item => item.Version, VersionStringComparer.Descending).ToList();
    }

    private sealed class ProductVersionFormPayload
    {
        public string? Version { get; init; }
        public double BatteryMassKg { get; init; }
        public double RatedEnergyKwh { get; init; }
        public double RatedCapacityAh { get; init; }
        public double RatedMaximumPowerKw { get; init; }
        public double NominalVoltageV { get; init; }
        public double ExpectedLifetimeYears { get; init; }
        public double ExpectedCycles { get; init; }
        public double SupplyChainIndex { get; init; }
        public double CarbonFootprint { get; init; }
        public string? PerformanceClass { get; init; }
        public Dictionary<string, double>? MaterialMassesKg { get; init; }
        public Dictionary<string, double>? CarbonStages { get; init; }
        public Dictionary<string, ProductTemplateRecycledContentFormPayload>? RecycledContent { get; init; }
        public List<ProductSoftwareVersionFormPayload>? SoftwareVersions { get; init; }
    }

    private sealed class ProductSoftwareVersionFormPayload
    {
        public string? Version { get; init; }
        public string? ReleaseDate { get; init; }
        public string? LatestUpdate { get; init; }
    }

    private sealed class ProductTemplateRecycledContentFormPayload
    {
        public double PreConsumerShare { get; init; }
        public double PostConsumerShare { get; init; }
    }

    private sealed class VersionStringComparer : IComparer<string>
    {
        public static readonly VersionStringComparer Descending = new(descending: true);

        private readonly bool _descending;

        private VersionStringComparer(bool descending)
        {
            _descending = descending;
        }

        public int Compare(string? x, string? y)
        {
            var result = CompareAscending(x ?? string.Empty, y ?? string.Empty);
            return _descending ? -result : result;
        }

        private static int CompareAscending(string left, string right)
        {
            var leftParts = left.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var rightParts = right.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var max = Math.Max(leftParts.Length, rightParts.Length);
            for (var index = 0; index < max; index++)
            {
                var leftValue = index < leftParts.Length && int.TryParse(leftParts[index], out var parsedLeft) ? parsedLeft : 0;
                var rightValue = index < rightParts.Length && int.TryParse(rightParts[index], out var parsedRight) ? parsedRight : 0;
                var partResult = leftValue.CompareTo(rightValue);
                if (partResult != 0)
                {
                    return partResult;
                }
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left, right);
        }
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

    private async Task<BsonDocument> BuildDraftPassportDocumentAsync(
        string passportId,
        string productId,
        string productVersion,
        string softwareVersion,
        CancellationToken cancellationToken)
    {
        var suffix = passportId.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? Guid.NewGuid().ToString("N");
        var normalizedSuffix = new string(suffix.Where(char.IsLetterOrDigit).Take(12).ToArray());
        if (string.IsNullOrWhiteSpace(normalizedSuffix))
        {
            normalizedSuffix = Guid.NewGuid().ToString("N")[..12];
        }

        return await _productTemplateService.BuildPassportFromTemplateAsync(
            passportId,
            FirstNonEmpty(productId, BatteryProductTemplateCatalog.DefaultProductId),
            FirstNonEmpty(productVersion, BatteryProductTemplateCatalog.DefaultProduct.LatestProductVersion.Version),
            FirstNonEmpty(softwareVersion, BatteryProductTemplateCatalog.DefaultSoftwareVersion),
            new ProductTemplateBatteryIdentity
            {
                ModelNumber = string.Empty,
                SerialNumber = string.Empty,
                DisplayName = string.Empty,
                FacilityId = string.Empty,
                ClusterId = string.Empty,
                ManufacturingDate = string.Empty
            },
            CurrentActor(),
            cancellationToken);
    }

    private static void ApplyPassportForm(BsonDocument document, IFormCollection form, string now)
    {
        var app = EnsureDocument(document, "app");
        var display = EnsureDocument(app, "display");
        var media = EnsureDocument(app, "media");
        var appDocuments = EnsureDocument(app, "documents");
        var appNotes = EnsureDocument(app, "notes");
        var appCircularityNotes = EnsureDocument(appNotes, "circularity");
        var productNode = EnsureDocument(app, "product");
        productNode["productId"] = Text(form, "productId", productNode.GetValue("productId", BatteryProductTemplateCatalog.DefaultProductId).ToString());
        productNode["productVersion"] = Text(form, "productVersion", productNode.GetValue("productVersion", BatteryProductTemplateCatalog.DefaultProduct.LatestProductVersion.Version).ToString());
        productNode["softwareVersion"] = Text(form, "softwareVersion", productNode.GetValue("softwareVersion", BatteryProductTemplateCatalog.DefaultSoftwareVersion).ToString());

        display["name"] = Text(form, "name", display.GetValue("name", string.Empty).ToString());
        display["modelNumber"] = Text(form, "modelNumber", display.GetValue("modelNumber", string.Empty).ToString());
        display["serialNumber"] = BatteryPassCanonicalDataCatalog.NormalizeManufacturerSerialNumber(
            Text(form, "serialNumber", display.GetValue("serialNumber", string.Empty).ToString()),
            BsonHelpers.GetString(document, "passportId"));
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

            documentNode["visibility"] = NormalizeDocumentVisibility(Text(
                form,
                $"document_{key}_visibility",
                documentNode.GetValue("visibility", "private").ToString()));
        }

        var aspects = EnsureDocument(document, "aspects");
        var generalAspect = EnsureAspectPayload(aspects, "generalProductInformation", now, "draft");
        var generalPayload = EnsureDocument(generalAspect, "payload");
        generalPayload["productIdentifier"] = display.GetValue("modelNumber", string.Empty).ToString();
        generalPayload["batteryPassportIdentifier"] = BatteryPassCanonicalDataCatalog.NormalizeBatteryPassportIdentifier(
            BsonHelpers.GetString(generalPayload, "batteryPassportIdentifier"),
            BsonText(display.GetValue("serialNumber", string.Empty)),
            BsonHelpers.GetString(document, "passportId"));
        generalPayload["batteryCategory"] = BatteryPassCanonicalDataCatalog.NormalizeBatteryCategory(
            BsonHelpers.GetString(generalPayload, "batteryCategory"));
        generalPayload["batteryStatus"] = Text(form, "batteryStatus", generalPayload.GetValue("batteryStatus", "Original").ToString());
        generalPayload["batteryMass"] = Number(form, "batteryMass", generalPayload.GetValue("batteryMass", 0).ToDouble());
        var manufacturingDate = Text(form, "manufacturingDate", DateOnly(generalPayload.GetValue("manufacturingDate", string.Empty).ToString()));
        generalPayload["manufacturingDate"] = $"{manufacturingDate}T00:00:00.000Z";
        var existingPuttingIntoService = BsonHelpers.GetString(generalPayload, "puttingIntoService");
        var puttingIntoServiceDate = string.IsNullOrWhiteSpace(existingPuttingIntoService)
            ? manufacturingDate
            : DateOnly(existingPuttingIntoService);
        generalPayload["puttingIntoService"] = $"{puttingIntoServiceDate}T00:00:00.000Z";

        var materialAspect = EnsureAspectPayload(aspects, "materialComposition", now, "draft");
        var materialPayload = EnsureDocument(materialAspect, "payload");
        var existingMaterials = materialPayload.GetValue("batteryMaterials", new BsonArray()) is BsonArray materials
            ? materials
            : new BsonArray();
        var materialRows = new BsonArray();
        foreach (var materialDefinition in BatteryPassCanonicalDataCatalog.Materials)
        {
            var fallbackMass = existingMaterials
                .OfType<BsonDocument>()
                .FirstOrDefault(item => BsonText(item.GetValue("batteryMaterialName", string.Empty)).Equals(materialDefinition.Label, StringComparison.OrdinalIgnoreCase))?
                .GetValue("batteryMaterialMass", 0).ToDouble() ?? 0;
            var mass = BatteryPassCanonicalDataCatalog.NormalizeMaterialMass(
                materialDefinition.Label,
                Number(form, materialDefinition.Field, fallbackMass));

            var existing = existingMaterials.OfType<BsonDocument>()
                .FirstOrDefault(item => BsonText(item.GetValue("batteryMaterialName", string.Empty)).Equals(materialDefinition.Label, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing["batteryMaterialName"] = materialDefinition.Label;
                existing["batteryMaterialMass"] = mass;
                if (existing.GetValue("batteryMaterialLocation", BsonNull.Value) is not BsonDocument)
                {
                    existing["batteryMaterialLocation"] = DemoMaterialLocation();
                }
                existing["isCriticalRawMaterial"] = materialDefinition.IsCriticalRawMaterial;
                materialRows.Add(existing);
            }
            else
            {
                materialRows.Add(new BsonDocument
                {
                    ["batteryMaterialName"] = materialDefinition.Label,
                    ["batteryMaterialMass"] = mass,
                    ["batteryMaterialLocation"] = DemoMaterialLocation(),
                    ["isCriticalRawMaterial"] = materialDefinition.IsCriticalRawMaterial
                });
            }
        }
        materialPayload["batteryMaterials"] = materialRows;

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
        carbonPayload["batteryCarbonFootprint"] = BatteryPassCanonicalDataCatalog.NormalizeCarbonFootprint(
            Number(form, "carbonFootprint", carbonPayload.GetValue("batteryCarbonFootprint", 0).ToDouble()));
        carbonPayload["carbonFootprintPerformanceClass"] = BatteryPassCanonicalDataCatalog.NormalizePerformanceClass(
            Text(form, "performanceClass", carbonPayload.GetValue("carbonFootprintPerformanceClass", "B").ToString()));
        var lifecycleRows = new BsonArray();
        foreach (var carbonStage in BatteryPassCanonicalDataCatalog.CarbonStages)
        {
            var value = BatteryPassCanonicalDataCatalog.NormalizeCarbonStageValue(
                carbonStage.Stage,
                Number(form, carbonStage.Field, 0));
            lifecycleRows.Add(new BsonDocument
            {
                ["lifecycleStage"] = carbonStage.Stage,
                ["carbonFootprint"] = value
            });
        }
        carbonPayload["carbonFootprintPerLifecycleStage"] = lifecycleRows;
        carbonPayload["carbonFootprintStudy"] = EnsureDocument(appDocuments, "co2StudyReference").GetValue("url", string.Empty).ToString();

        var supplyAspect = EnsureAspectPayload(aspects, "supplyChainDueDiligence", now, "draft");
        var supplyPayload = EnsureDocument(supplyAspect, "payload");
        supplyPayload["supplyChainIndicies"] = BatteryPassCanonicalDataCatalog.NormalizeSupplyChainIndex(
            Number(form, "supplyChainIndex", supplyPayload.GetValue("supplyChainIndicies", 0).ToDouble()));
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
        var recycledAspectRows = new BsonArray();
        foreach (var (prefix, material) in recycledMaterials)
        {
            var pre = Number(form, $"{prefix}Pre", 0);
            var post = Number(form, $"{prefix}Post", 0);
            recycledAspectRows.Add(new BsonDocument
            {
                ["recycledMaterial"] = material,
                ["preConsumerShare"] = pre,
                ["postConsumerShare"] = post
            });
        }
        circularityPayload["recycledContent"] = recycledAspectRows;
        RemoveDuplicatedDisplayCharts(app);

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

    private async Task ApplySelectedProductTemplateMetadataAsync(
        BsonDocument document,
        IFormCollection form,
        CancellationToken cancellationToken)
    {
        var app = EnsureDocument(document, "app");
        var productNode = EnsureDocument(app, "product");
        var productId = Text(form, "productId", BsonHelpers.GetString(productNode, "productId"));
        var productVersion = Text(form, "productVersion", BsonHelpers.GetString(productNode, "productVersion"));
        var softwareVersion = Text(form, "softwareVersion", BsonHelpers.GetString(productNode, "softwareVersion"));
        var product = await _productTemplateService.GetProductAsync(productId, cancellationToken);
        if (product == null)
        {
            return;
        }

        var selectedProductVersion = product.ProductVersions.FirstOrDefault(version =>
                version.Version.Equals(productVersion, StringComparison.OrdinalIgnoreCase))
            ?? product.LatestProductVersion;
        var software = selectedProductVersion.SoftwareVersions.FirstOrDefault(version => version.Version.Equals(softwareVersion, StringComparison.OrdinalIgnoreCase))
            ?? selectedProductVersion.SoftwareVersions.FirstOrDefault();
        productNode["productId"] = product.ProductId;
        productNode["productVersion"] = selectedProductVersion.Version;
        productNode["productName"] = product.ProductName;
        productNode["description"] = product.Description;
        productNode["moduleCount"] = product.ModuleCount;
        if (software != null)
        {
            productNode["softwareVersion"] = software.Version;
            productNode["softwareReleaseDate"] = software.ReleaseDate;
            productNode["softwareLatestUpdate"] = software.LatestUpdate;
        }

        await _productTemplateService.ApplyProductTemplateReferencesAsync(document, product.ProductId, cancellationToken);
        productNode["templateHash"] = ProductTemplatePassportBuilder.ComputeTemplateHash(ProductTemplatePassportBuilder.BuildTemplateBaseline(document));
        EnsureDocument(app, "templateBaseline").Clear();
        app["templateBaseline"] = ProductTemplatePassportBuilder.BuildTemplateBaseline(document);
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

    private static string DateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        return value.Length >= 10 ? value[..10] : value;
    }

    private static string NormalizeDocumentVisibility(string visibility)
    {
        return visibility.Equals("public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
    }

    private static string BsonText(BsonValue? value)
    {
        return value == null || value.IsBsonNull ? string.Empty : value.ToString() ?? string.Empty;
    }

    private static BsonDocument DemoMaterialLocation()
    {
        return new BsonDocument
        {
            ["componentName"] = "Cell",
            ["componentId"] = "DEMO-CELL-01"
        };
    }

    private static void RemoveDuplicatedDisplayCharts(BsonDocument app)
    {
        if (app.GetValue("charts", BsonNull.Value) is not BsonDocument charts)
        {
            return;
        }

        charts.Remove("materialComposition");
        charts.Remove("carbonFootprint");
        charts.Remove("recycledContent");
        if (!charts.Any())
        {
            app.Remove("charts");
        }
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
