namespace BatteryPassWeb.Models.Trust;

public static class PassportEvidenceStatus
{
    public const string MissingRequired = "missingRequired";
    public const string MissingOptional = "missingOptional";
    public const string UploadedUnsigned = "uploadedUnsigned";
    public const string Verified = "verified";
    public const string ChangedSinceSigning = "changedSinceSigning";
    public const string ExternalLinkOnly = "externalLinkOnly";
    public const string MissingFileReference = "missingFileReference";
}

public sealed class EvidencePackResult
{
    public static EvidencePackResult Empty { get; } = new();

    public IReadOnlyList<EvidenceItemResult> Items { get; init; } = [];
    public int RequiredCount => Items.Count(item => item.IsRequired);
    public int OptionalCount => Items.Count(item => !item.IsRequired);
    public int BlockingCount => Items.Count(item => item.IsBlocking);
    public int MissingRequiredCount => Items.Count(item => item.Status == PassportEvidenceStatus.MissingRequired);
    public int UploadedUnsignedCount => Items.Count(item => item.Status == PassportEvidenceStatus.UploadedUnsigned);
    public int ChangedSinceSigningCount => Items.Count(item => item.Status == PassportEvidenceStatus.ChangedSinceSigning);
    public int VerifiedCount => Items.Count(item => item.Status == PassportEvidenceStatus.Verified);
    public int PublicCount => Items.Count(item => item.HasCurrentReference && item.Visibility == "public");
    public int RestrictedCount => Items.Count(item => item.HasCurrentReference && item.Visibility != "public");
}

public sealed class EvidenceItemResult
{
    public string DocumentKey { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string FieldKey { get; init; } = string.Empty;
    public string DataPath { get; init; } = string.Empty;
    public string DocumentPath { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public bool IsBlocking { get; init; }
    public string Status { get; init; } = PassportEvidenceStatus.MissingOptional;
    public string StatusLabel { get; init; } = string.Empty;
    public string Visibility { get; init; } = "private";
    public string FileId { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string CurrentHash { get; init; } = string.Empty;
    public string SignedHash { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public bool HasCurrentReference =>
        !string.IsNullOrWhiteSpace(FileId)
        || !string.IsNullOrWhiteSpace(Url)
        || !string.IsNullOrWhiteSpace(CurrentHash);
}
