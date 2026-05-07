using BatteryPassWeb.Models.Demo;
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class DemoScenarioResetService
{
    private readonly PassportRepository _passportRepository;
    private readonly DataCompletionPolicyService _dataCompletionPolicyService;
    private readonly DemoRequiredDataCompletionService _demoRequiredDataCompletionService;
    private readonly PassportValidationService _passportValidationService;
    private readonly PassportTrustService _passportTrustService;
    private readonly AuditRevisionService _auditRevisionService;

    public DemoScenarioResetService(
        PassportRepository passportRepository,
        DataCompletionPolicyService dataCompletionPolicyService,
        DemoRequiredDataCompletionService demoRequiredDataCompletionService,
        PassportValidationService passportValidationService,
        PassportTrustService passportTrustService,
        AuditRevisionService auditRevisionService)
    {
        _passportRepository = passportRepository;
        _dataCompletionPolicyService = dataCompletionPolicyService;
        _demoRequiredDataCompletionService = demoRequiredDataCompletionService;
        _passportValidationService = passportValidationService;
        _passportTrustService = passportTrustService;
        _auditRevisionService = auditRevisionService;
    }

    public IReadOnlyList<DemoScenarioDefinition> ListScenarios()
    {
        return DemoScenarioCatalog.All;
    }

    public async Task<DemoScenarioResetResult> ResetAllAsync(
        string actor,
        CancellationToken cancellationToken = default)
    {
        var resetAt = DateTimeOffset.UtcNow.ToString("O");
        var passportIds = DemoScenarioCatalog.All.Select(scenario => scenario.PassportId).ToArray();
        foreach (var passportId in passportIds)
        {
            if (!DemoScenarioCatalog.IsKnownPassportId(passportId))
            {
                throw new InvalidOperationException($"Demo reset refused unknown passport ID '{passportId}'.");
            }
        }

        await _auditRevisionService.DeleteDemoLedgerAsync(passportIds, cancellationToken);
        foreach (var scenario in DemoScenarioCatalog.All)
        {
            await ResetScenarioAsync(scenario, actor, resetAt, cancellationToken);
        }

        return new DemoScenarioResetResult(passportIds.Length, passportIds, resetAt);
    }

    private async Task ResetScenarioAsync(
        DemoScenarioDefinition scenario,
        string actor,
        string resetAt,
        CancellationToken cancellationToken)
    {
        if (!DemoScenarioCatalog.IsKnownPassportId(scenario.PassportId))
        {
            throw new InvalidOperationException($"Demo reset refused unknown passport ID '{scenario.PassportId}'.");
        }

        var policy = await _dataCompletionPolicyService.GetPolicyAsync(cancellationToken);
        var document = BuildBaseScenarioDocument(scenario, resetAt);

        if (scenario.Key != DemoScenarioKey.DraftIncomplete)
        {
            document = _demoRequiredDataCompletionService.CompleteRequiredData(document, resetAt);
            document["passportId"] = scenario.PassportId;
            document["clusterId"] = scenario.ClusterId;
            EnsureDisplay(document, scenario);
        }

        if (scenario.Key == DemoScenarioKey.RestrictedDocument)
        {
            ApplyRestrictedDocumentMetadata(document, resetAt);
        }

        await _passportRepository.ReplaceAsync(scenario.PassportId, document, cancellationToken);

        if (scenario.Key is DemoScenarioKey.ReadyToSign)
        {
            await ApplyValidatedStateAsync(scenario.PassportId, policy, cancellationToken);
        }
        else if (scenario.Key is DemoScenarioKey.SignedUnpublished)
        {
            await ApplySignedStateAsync(scenario.PassportId, policy, actor, cancellationToken);
        }
        else if (scenario.Key is DemoScenarioKey.PublishedTrusted or DemoScenarioKey.RestrictedDocument)
        {
            await ApplySignedStateAsync(scenario.PassportId, policy, actor, cancellationToken);
            await ApplyPublishedStateAsync(scenario.PassportId, cancellationToken);
        }
        else if (scenario.Key is DemoScenarioKey.DirtyAfterEdit)
        {
            await ApplySignedStateAsync(scenario.PassportId, policy, actor, cancellationToken);
            await ApplyPublishedStateAsync(scenario.PassportId, cancellationToken);
            await ApplyDirtyStateAsync(scenario.PassportId, cancellationToken);
        }
        else if (scenario.Key is DemoScenarioKey.InvalidSignature)
        {
            await ApplySignedStateAsync(scenario.PassportId, policy, actor, cancellationToken);
            await ApplyInvalidSignatureStateAsync(scenario.PassportId, cancellationToken);
        }

        await _auditRevisionService.AppendAuditEventAsync(
            scenario.PassportId,
            "demo.scenario.reset",
            actor,
            "admin",
            "admin-ui",
            $"Demo scenario reset: {scenario.Label}.",
            new BsonDocument
            {
                ["scenarioKey"] = scenario.Key,
                ["expectedState"] = scenario.ExpectedState,
                ["resetAt"] = resetAt
            },
            cancellationToken);
    }

    private static BsonDocument BuildBaseScenarioDocument(DemoScenarioDefinition scenario, string now)
    {
        return new BsonDocument
        {
            ["passportId"] = scenario.PassportId,
            ["clusterId"] = scenario.ClusterId,
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = scenario.PassportId.Split(':').Last(),
                ["status"] = "draft",
                ["createdAt"] = now,
                ["updatedAt"] = now
            },
            ["app"] = new BsonDocument
            {
                ["demoScenario"] = new BsonDocument
                {
                    ["key"] = scenario.Key,
                    ["label"] = scenario.Label,
                    ["expectedState"] = scenario.ExpectedState
                },
                ["display"] = new BsonDocument
                {
                    ["name"] = $"Phase 6A - {scenario.Label}",
                    ["modelNumber"] = $"P6A-{scenario.Key}",
                    ["serialNumber"] = $"SN-P6A-{scenario.Key}",
                    ["manufacturerName"] = "Demo Batteries GmbH",
                    ["facilityId"] = "NORTH-DEMO-FACILITY"
                },
                ["media"] = new BsonDocument
                {
                    ["batteryImageUrl"] = BatteryImageCatalog.DefaultImageUrl
                },
                ["operations"] = new BsonDocument
                {
                    ["isActive"] = true,
                    ["lastUpdatedAt"] = now
                }
            },
            ["aspects"] = new BsonDocument(),
            ["validation"] = new BsonDocument
            {
                ["isValid"] = false,
                ["status"] = "unvalidated"
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = TrustState.Unvalidated,
                ["isDirty"] = false,
                ["latestHash"] = string.Empty,
                ["latestProof"] = new BsonDocument()
            }
        };
    }

    private static void EnsureDisplay(BsonDocument document, DemoScenarioDefinition scenario)
    {
        var app = EnsureDocument(document, "app");
        var display = EnsureDocument(app, "display");
        display["name"] = $"Phase 6A - {scenario.Label}";
        display["modelNumber"] = $"P6A-{scenario.Key}";
        display["serialNumber"] = $"SN-P6A-{scenario.Key}";
        display["manufacturerName"] = "Demo Batteries GmbH";
        display["facilityId"] = "NORTH-DEMO-FACILITY";
        app["demoScenario"] = new BsonDocument
        {
            ["key"] = scenario.Key,
            ["label"] = scenario.Label,
            ["expectedState"] = scenario.ExpectedState
        };
    }

    private async Task ApplyValidatedStateAsync(
        string passportId,
        DataCompletionPolicySnapshot policy,
        CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken)
            ?? throw new InvalidOperationException($"Demo passport '{passportId}' was not found for validation.");
        var summary = _passportValidationService.Validate(document, policy);
        await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
    }

    private async Task ApplySignedStateAsync(
        string passportId,
        DataCompletionPolicySnapshot policy,
        string actor,
        CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken)
            ?? throw new InvalidOperationException($"Demo passport '{passportId}' was not found for signing.");
        var summary = _passportValidationService.Validate(document, policy);
        if (summary.BlockingErrorCount > 0)
        {
            throw new InvalidOperationException($"Demo passport '{passportId}' cannot be signed because it has {summary.BlockingErrorCount} blocking validation errors.");
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
        var updated = await _passportRepository.UpdateTrustSignatureAsync(
            passportId,
            summary,
            signature.Hash,
            signature.Proof,
            revisionId,
            signature.SignedAt,
            cancellationToken);
        if (!updated)
        {
            throw new InvalidOperationException($"Demo passport '{passportId}' signature state could not be persisted.");
        }
    }

    private async Task ApplyPublishedStateAsync(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken)
            ?? throw new InvalidOperationException($"Demo passport '{passportId}' was not found for publishing.");
        var verification = _passportTrustService.Verify(document);
        if (!verification.IsValid)
        {
            throw new InvalidOperationException($"Demo passport '{passportId}' cannot be published: {verification.Message}");
        }

        var revisionId = BsonHelpers.GetString(document, "trust", "latestRevisionId");
        var proof = BsonHelpers.GetValue(document, "trust", "latestProof") as BsonDocument ?? new BsonDocument();
        var publishedAt = DateTimeOffset.UtcNow.ToString("O");
        await _passportRepository.PublishPassportAsync(passportId, revisionId, publishedAt, verification.CurrentHash, proof, cancellationToken);
        await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, publishedAt, cancellationToken);
    }

    private async Task ApplyDirtyStateAsync(string passportId, CancellationToken cancellationToken)
    {
        await _passportRepository.UpdateFieldsAsync(
            passportId,
            new Dictionary<string, BsonValue>
            {
                ["app.display.modelNumber"] = "P6A-DIRTY-EDITED"
            },
            cancellationToken);
        await _passportRepository.MarkCanonicalDirtyAsync(passportId, "phase6aDemoSignedCoreEdit", cancellationToken);
    }

    private async Task ApplyInvalidSignatureStateAsync(string passportId, CancellationToken cancellationToken)
    {
        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken)
            ?? throw new InvalidOperationException($"Demo passport '{passportId}' was not found for invalid-signature mutation.");
        var trust = EnsureDocument(document, "trust");
        var proof = trust.GetValue("latestProof", new BsonDocument()) as BsonDocument ?? new BsonDocument();
        proof["proofValue"] = "invalid-demo-proof-value";
        trust["latestProof"] = proof;
        trust["state"] = TrustState.SignatureInvalid;
        trust["isDirty"] = false;
        document.Remove("_id");
        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
    }

    private static void ApplyRestrictedDocumentMetadata(BsonDocument document, string now)
    {
        var app = EnsureDocument(document, "app");
        var documents = EnsureDocument(app, "documents");
        documents["dueDiligenceReport"] = new BsonDocument
        {
            ["label"] = "Restricted due diligence report",
            ["url"] = "https://example.test/restricted-due-diligence-report.pdf",
            ["contentType"] = "application/pdf",
            ["sha256"] = "phase6a-demo-restricted-document-hash",
            ["visibility"] = "restricted",
            ["uploadedAt"] = now
        };
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
}
