using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed record PassportTrustWorkflowResult(
    bool Success,
    string Message,
    TrustValidationSummary? ValidationSummary,
    string RevisionId,
    bool AutoPublished);

public sealed class PassportTrustWorkflowService
{
    private readonly PassportRepository _passportRepository;
    private readonly DataCompletionPolicyService _dataCompletionPolicyService;
    private readonly PassportValidationService _passportValidationService;
    private readonly PassportEvidenceService _passportEvidenceService;
    private readonly AuditRevisionService _auditRevisionService;
    private readonly PassportTrustService _passportTrustService;
    private readonly PassportPublishPolicyService _passportPublishPolicyService;

    public PassportTrustWorkflowService(
        PassportRepository passportRepository,
        DataCompletionPolicyService dataCompletionPolicyService,
        PassportValidationService passportValidationService,
        PassportEvidenceService passportEvidenceService,
        AuditRevisionService auditRevisionService,
        PassportTrustService passportTrustService,
        PassportPublishPolicyService passportPublishPolicyService)
    {
        _passportRepository = passportRepository;
        _dataCompletionPolicyService = dataCompletionPolicyService;
        _passportValidationService = passportValidationService;
        _passportEvidenceService = passportEvidenceService;
        _auditRevisionService = auditRevisionService;
        _passportTrustService = passportTrustService;
        _passportPublishPolicyService = passportPublishPolicyService;
    }

    public async Task<PassportTrustWorkflowResult> ValidateAsync(
        string passportId,
        string actor,
        string source,
        CancellationToken cancellationToken = default)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return new PassportTrustWorkflowResult(false, "Battery passport was not found.", null, string.Empty, false);
        }

        var summary = await ValidateDocumentAsync(passportId, document, cancellationToken);
        await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            "passport.validated",
            actor,
            source,
            source,
            "Passport validation completed.",
            new BsonDocument
            {
                ["blockingErrors"] = summary.BlockingErrorCount,
                ["warnings"] = summary.WarningCount,
                ["passedChecks"] = summary.PassedCount,
                ["canSign"] = summary.CanSign
            },
            cancellationToken);

        return new PassportTrustWorkflowResult(true, "Passport validation completed.", summary, string.Empty, false);
    }

    public async Task<PassportTrustWorkflowResult> SignAsync(
        string passportId,
        string actor,
        string source,
        CancellationToken cancellationToken = default)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
        if (document == null)
        {
            return new PassportTrustWorkflowResult(false, "Battery passport was not found.", null, string.Empty, false);
        }

        var summary = await ValidateDocumentAsync(passportId, document, cancellationToken);
        if (!_passportPublishPolicyService.CanSign(summary))
        {
            await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
            await _auditRevisionService.AppendAuditEventAsync(
                passportId,
                "passport.sign.blocked",
                actor,
                source,
                source,
                "Passport signing blocked by validation errors.",
                new BsonDocument
                {
                    ["blockingErrors"] = summary.BlockingErrorCount,
                    ["warnings"] = summary.WarningCount
                },
                cancellationToken);

            return new PassportTrustWorkflowResult(false, "Resolve blocking validation errors before signing.", summary, string.Empty, false);
        }

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

        var autoPublished = false;
        if (_passportPublishPolicyService.ShouldAutoPublishAfterSign(document))
        {
            var publishedAt = DateTimeOffset.UtcNow.ToString("O");
            var published = await _passportRepository.PublishPassportAsync(
                passportId,
                revisionId,
                publishedAt,
                $"sha256:{signature.Hash}",
                signature.Proof,
                cancellationToken);
            if (!published)
            {
                throw new InvalidOperationException("Publishing service could not persist the auto-published trust state.");
            }

            var revisionMarked = await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, publishedAt, cancellationToken);
            if (!revisionMarked)
            {
                throw new InvalidOperationException("Publishing service could not mark the immutable revision as published.");
            }

            autoPublished = true;
        }

        await _auditRevisionService.AppendAuditEventAsync(
            passportId,
            autoPublished ? "passport.signed.autoPublished" : "passport.signed",
            actor,
            source,
            source,
            autoPublished ? "Passport signed and published." : "Passport signed.",
            new BsonDocument
            {
                ["revisionId"] = revisionId,
                ["hash"] = $"sha256:{signature.Hash}",
                ["autoPublished"] = autoPublished,
                ["blockingErrors"] = summary.BlockingErrorCount,
                ["warnings"] = summary.WarningCount
            },
            cancellationToken);

        return new PassportTrustWorkflowResult(
            true,
            autoPublished ? "Passport signed and published." : "Passport signed.",
            summary,
            revisionId,
            autoPublished);
    }

    private async Task<TrustValidationSummary> ValidateDocumentAsync(
        string passportId,
        BsonDocument document,
        CancellationToken cancellationToken)
    {
        var policy = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
        var summary = _passportValidationService.Validate(document, policy);
        var latestRevision = await _auditRevisionService.GetLatestSignedRevisionAsync(passportId, cancellationToken);
        var evidencePack = _passportEvidenceService.Evaluate(document, latestRevision, policy);
        return PassportEvidenceService.AppendValidationSection(summary, evidencePack);
    }
}
