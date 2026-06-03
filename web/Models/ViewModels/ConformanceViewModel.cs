using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class ConformanceViewModel
{
    public string Mode { get; init; } = "admin";
    public required PassportViewModel Passport { get; init; }
    public required TrustValidationSummary ValidationSummary { get; init; }
    public PassportReadinessDecision Readiness { get; init; } = new();
    public EvidencePackResult EvidencePack { get; init; } = EvidencePackResult.Empty;
    public IReadOnlyList<ConformanceIssueGroupViewModel> GroupedBlockingIssues { get; init; } = [];
    public IReadOnlyList<ConformanceIssueGroupViewModel> GroupedWarningIssues { get; init; } = [];
    public bool CanSign { get; init; }
    public bool CanPublish { get; init; }
    public string PublishBlockReason { get; init; } = string.Empty;
    public PassportVerificationResult VerificationResult { get; init; } = new();
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class ConformanceIssueGroupViewModel
{
    public string SectionKey { get; init; } = string.Empty;
    public string SectionLabel { get; init; } = string.Empty;
    public IReadOnlyList<TrustValidationIssue> Issues { get; init; } = [];
    public string EditAnchor { get; init; } = string.Empty;
}

public sealed class PassportAuditTrailViewModel
{
    public string Mode { get; init; } = "admin";
    public required PassportViewModel Passport { get; init; }
    public IReadOnlyList<BsonDocument> AuditEvents { get; init; } = [];
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class PassportRevisionHistoryViewModel
{
    public string Mode { get; init; } = "admin";
    public required PassportViewModel Passport { get; init; }
    public IReadOnlyList<BsonDocument> Revisions { get; init; } = [];
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
