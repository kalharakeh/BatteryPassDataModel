using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Models.ViewModels;

public sealed class ConformanceViewModel
{
    public required PassportViewModel Passport { get; init; }
    public required TrustValidationSummary ValidationSummary { get; init; }
    public bool CanSign { get; init; }
    public bool CanPublish { get; init; }
    public string PublishBlockReason { get; init; } = string.Empty;
    public PassportVerificationResult VerificationResult { get; init; } = new();
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class PassportAuditTrailViewModel
{
    public required PassportViewModel Passport { get; init; }
    public IReadOnlyList<BsonDocument> AuditEvents { get; init; } = [];
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class PassportRevisionHistoryViewModel
{
    public required PassportViewModel Passport { get; init; }
    public IReadOnlyList<BsonDocument> Revisions { get; init; } = [];
    public string StatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
