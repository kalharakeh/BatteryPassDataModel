using MongoDB.Bson;

namespace BatteryPassWeb.Models.Trust;

public static class TrustState
{
    public const string Unvalidated = "unvalidated";
    public const string Invalid = "invalid";
    public const string Valid = "valid";
    public const string Signed = "signed";
    public const string Dirty = "dirty";
    public const string SignatureInvalid = "signatureInvalid";
}

public enum TrustValidationSeverity
{
    Passed = 0,
    Warning = 1,
    BlockingError = 2
}

public sealed record TrustValidationIssue(
    TrustValidationSeverity Severity,
    string Path,
    string Message);

public sealed class TrustValidationSectionResult
{
    public string SectionKey { get; init; } = string.Empty;
    public string SectionLabel { get; init; } = string.Empty;
    public IReadOnlyList<TrustValidationIssue> Issues { get; init; } = [];
    public bool HasBlockingErrors => Issues.Any(issue => issue.Severity == TrustValidationSeverity.BlockingError);
    public bool HasWarnings => Issues.Any(issue => issue.Severity == TrustValidationSeverity.Warning);
}

public sealed class TrustValidationSummary
{
    public string PassportId { get; init; } = string.Empty;
    public string State { get; init; } = TrustState.Unvalidated;
    public string ValidatedAt { get; init; } = string.Empty;
    public IReadOnlyList<TrustValidationSectionResult> Sections { get; init; } = [];
    public int BlockingErrorCount => Sections.Sum(section => section.Issues.Count(issue => issue.Severity == TrustValidationSeverity.BlockingError));
    public int WarningCount => Sections.Sum(section => section.Issues.Count(issue => issue.Severity == TrustValidationSeverity.Warning));
    public int PassedCount => Sections.Sum(section => section.Issues.Count(issue => issue.Severity == TrustValidationSeverity.Passed));
    public bool CanSign => BlockingErrorCount == 0;
}

public sealed class SchemaDescriptor
{
    public required string AspectKey { get; init; }
    public required string Label { get; init; }
    public required string Version { get; init; }
    public required string RelativePath { get; init; }
    public string AbsolutePath { get; init; } = string.Empty;
    public bool Exists => !string.IsNullOrWhiteSpace(AbsolutePath) && File.Exists(AbsolutePath);
}

public sealed class PassportSignatureResult
{
    public BsonDocument Snapshot { get; init; } = new();
    public string CanonicalJson { get; init; } = string.Empty;
    public string Hash { get; init; } = string.Empty;
    public BsonDocument Proof { get; init; } = new();
    public string SignedAt { get; init; } = string.Empty;
}

public sealed class PassportVerificationResult
{
    public bool IsValid { get; init; }
    public string State { get; init; } = TrustState.Unvalidated;
    public string Message { get; init; } = string.Empty;
    public string CurrentHash { get; init; } = string.Empty;
    public string ExpectedHash { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string VerificationMethod { get; init; } = string.Empty;
    public string SignedAt { get; init; } = string.Empty;
}
