using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportEvidenceService
{
    private static readonly IReadOnlyList<EvidenceDocumentDefinition> Definitions =
    [
        new("conformityAssessment", "Conformity assessment report", "compliance.conformityAssessment", "aspects.labeling.payload.resultOfTestReport"),
        new("euDeclarationOfConformity", "EU declaration of conformity", "compliance.euDeclarationOfConformity", "aspects.labeling.payload.declarationOfConformity"),
        new("sustainabilityReport", "Sustainability report", "supplyChain.sustainabilityReport", "aspects.supplyChainDueDiligence.payload.sustainabilityReport"),
        new("dueDiligenceReport", "Due diligence report", "supplyChain.dueDiligenceReport", "aspects.supplyChainDueDiligence.payload.supplyChainDueDiligenceReport"),
        new("thirdPartyAudit", "Third-party audit", "supplyChain.thirdPartyAudit", "aspects.supplyChainDueDiligence.payload.thirdPartyAussurances"),
        new("taxonomyReport", "Taxonomy report", "supplyChain.taxonomyReport", "aspects.supplyChainDueDiligence.payload.taxonomyReport"),
        new("co2StudyReference", "CO2 study reference", "carbon.co2StudyReference", "aspects.carbonFootprintForBatteries.payload.carbonFootprintStudy")
    ];

    public EvidencePackResult Evaluate(
        BsonDocument passport,
        BsonDocument? latestSignedRevision,
        DataCompletionPolicySnapshot policy)
    {
        var requiredFields = policy.Sections
            .SelectMany(section => section.Fields)
            .Where(field => field.IsRequired)
            .Select(field => field.FieldKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var signedSnapshot = latestSignedRevision?.GetValue("snapshot", BsonNull.Value) as BsonDocument;
        var items = Definitions
            .Select(definition => EvaluateDefinition(passport, signedSnapshot, requiredFields, definition))
            .ToList();

        return new EvidencePackResult { Items = items };
    }

    public static TrustValidationSummary AppendValidationSection(
        TrustValidationSummary summary,
        EvidencePackResult evidencePack)
    {
        var sections = summary.Sections
            .Where(section => !section.SectionKey.Equals("supportingEvidence", StringComparison.OrdinalIgnoreCase))
            .Append(BuildValidationSection(evidencePack))
            .ToList();

        return new TrustValidationSummary
        {
            PassportId = summary.PassportId,
            State = sections.Any(section => section.HasBlockingErrors) ? TrustState.Invalid : TrustState.Valid,
            ValidatedAt = summary.ValidatedAt,
            Sections = sections
        };
    }

    public static TrustValidationSectionResult BuildValidationSection(EvidencePackResult evidencePack)
    {
        var issues = evidencePack.Items.Select(item =>
        {
            var severity = item.IsBlocking
                ? TrustValidationSeverity.BlockingError
                : item.Status is PassportEvidenceStatus.UploadedUnsigned or PassportEvidenceStatus.ChangedSinceSigning
                    ? TrustValidationSeverity.Warning
                    : TrustValidationSeverity.Passed;

            return new TrustValidationIssue(severity, item.DocumentPath, item.RecommendedAction);
        }).ToList();

        if (issues.Count == 0)
        {
            issues.Add(new TrustValidationIssue(TrustValidationSeverity.Passed, "app.documents", "No supporting evidence definitions are configured."));
        }

        return new TrustValidationSectionResult
        {
            SectionKey = "supportingEvidence",
            SectionLabel = "Supporting document evidence",
            Issues = issues
        };
    }

    private static EvidenceItemResult EvaluateDefinition(
        BsonDocument passport,
        BsonDocument? signedSnapshot,
        ISet<string> requiredFields,
        EvidenceDocumentDefinition definition)
    {
        var currentReference = ResolveDocument(passport, "app", "documents", definition.DocumentKey);
        var signedReference = signedSnapshot == null
            ? new BsonDocument()
            : ResolveDocument(signedSnapshot, "app", "documents", definition.DocumentKey);
        var isRequired = requiredFields.Contains(definition.FieldKey);
        var url = FirstNonEmpty(
            BsonHelpers.GetString(currentReference, "url"),
            ResolveString(passport, definition.DataPath));
        var fileId = BsonHelpers.GetString(currentReference, "fileId");
        var currentHash = NormalizeHash(BsonHelpers.GetString(currentReference, "sha256"));
        var signedHash = NormalizeHash(BsonHelpers.GetString(signedReference, "sha256"));
        var visibility = NormalizeVisibility(BsonHelpers.GetString(currentReference, "visibility"));
        var status = ResolveStatus(isRequired, url, fileId, currentHash, signedHash, signedSnapshot != null);
        var isBlocking = status == PassportEvidenceStatus.MissingRequired
            || (isRequired && status == PassportEvidenceStatus.ExternalLinkOnly)
            || (isRequired && status == PassportEvidenceStatus.MissingFileReference);

        return new EvidenceItemResult
        {
            DocumentKey = definition.DocumentKey,
            Label = definition.Label,
            FieldKey = definition.FieldKey,
            DataPath = definition.DataPath,
            DocumentPath = $"app.documents.{definition.DocumentKey}",
            IsRequired = isRequired,
            IsBlocking = isBlocking,
            Status = status,
            StatusLabel = StatusLabel(status),
            Visibility = visibility,
            FileId = fileId,
            Url = url,
            CurrentHash = currentHash,
            SignedHash = signedHash,
            RecommendedAction = RecommendedAction(status, definition.Label, isRequired)
        };
    }

    private static string ResolveStatus(bool isRequired, string url, string fileId, string currentHash, string signedHash, bool hasSignedSnapshot)
    {
        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(fileId) && string.IsNullOrWhiteSpace(currentHash))
        {
            return isRequired ? PassportEvidenceStatus.MissingRequired : PassportEvidenceStatus.MissingOptional;
        }

        if (!string.IsNullOrWhiteSpace(fileId) && string.IsNullOrWhiteSpace(currentHash))
        {
            return PassportEvidenceStatus.MissingFileReference;
        }

        if (string.IsNullOrWhiteSpace(currentHash))
        {
            return PassportEvidenceStatus.ExternalLinkOnly;
        }

        if (!hasSignedSnapshot || string.IsNullOrWhiteSpace(signedHash))
        {
            return PassportEvidenceStatus.UploadedUnsigned;
        }

        return currentHash.Equals(signedHash, StringComparison.OrdinalIgnoreCase)
            ? PassportEvidenceStatus.Verified
            : PassportEvidenceStatus.ChangedSinceSigning;
    }

    private static BsonDocument ResolveDocument(BsonDocument document, params string[] path)
    {
        var value = BsonHelpers.GetValue(document, path);
        return value as BsonDocument ?? new BsonDocument();
    }

    private static string ResolveString(BsonDocument document, string path)
    {
        var value = BsonHelpers.GetValue(document, path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return value == null || value.IsBsonNull ? string.Empty : value.ToString() ?? string.Empty;
    }

    private static string NormalizeHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"sha256:{value}";
    }

    private static string NormalizeVisibility(string value)
    {
        return value.Equals("public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
    }

    private static string StatusLabel(string status)
    {
        return status switch
        {
            PassportEvidenceStatus.MissingRequired => "Missing required evidence",
            PassportEvidenceStatus.MissingOptional => "Optional evidence missing",
            PassportEvidenceStatus.UploadedUnsigned => "Uploaded, not signed yet",
            PassportEvidenceStatus.Verified => "Verified in signed revision",
            PassportEvidenceStatus.ChangedSinceSigning => "Changed since signing",
            PassportEvidenceStatus.ExternalLinkOnly => "External link only",
            PassportEvidenceStatus.MissingFileReference => "Missing file hash",
            _ => "Evidence needs review"
        };
    }

    private static string RecommendedAction(string status, string label, bool isRequired)
    {
        return status switch
        {
            PassportEvidenceStatus.MissingRequired => $"Upload the required {label} file before signing.",
            PassportEvidenceStatus.MissingOptional => $"Optional {label} evidence is not uploaded.",
            PassportEvidenceStatus.UploadedUnsigned => $"Sign the passport so this {label} hash becomes trusted.",
            PassportEvidenceStatus.Verified => $"{label} matches the latest signed revision.",
            PassportEvidenceStatus.ChangedSinceSigning => $"Sign the passport again to trust the current {label} hash.",
            PassportEvidenceStatus.ExternalLinkOnly when isRequired => $"Upload a file for {label}; external links alone cannot be hash-verified.",
            PassportEvidenceStatus.ExternalLinkOnly => $"{label} is an external link without a stored hash.",
            PassportEvidenceStatus.MissingFileReference => $"Replace the {label} upload so the file hash can be checked.",
            _ => $"Review {label} evidence."
        };
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

    private sealed record EvidenceDocumentDefinition(
        string DocumentKey,
        string Label,
        string FieldKey,
        string DataPath);
}
