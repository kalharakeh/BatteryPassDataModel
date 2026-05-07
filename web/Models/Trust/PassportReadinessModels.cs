namespace BatteryPassWeb.Models.Trust;

public static class PassportReadinessState
{
    public const string Draft = "draft";
    public const string Incomplete = "incomplete";
    public const string ReadyToSign = "readyToSign";
    public const string SignedClean = "signedClean";
    public const string ReadyToPublish = "readyToPublish";
    public const string PublishedTrusted = "publishedTrusted";
    public const string DirtyNeedsResign = "dirtyNeedsResign";
    public const string InvalidSignature = "invalidSignature";
    public const string ServiceError = "serviceError";
}

public static class PassportReadinessSeverity
{
    public const string Neutral = "neutral";
    public const string Warning = "warning";
    public const string Blocked = "blocked";
    public const string Ready = "ready";
    public const string Trusted = "trusted";
}

public static class PassportReadinessAction
{
    public const string Validate = "validate";
    public const string CompleteData = "completeData";
    public const string EditRequiredData = "editRequiredData";
    public const string Sign = "sign";
    public const string Publish = "publish";
    public const string ReviewDiagnostics = "reviewDiagnostics";
    public const string None = "none";
}

public sealed class PassportReadinessDecision
{
    public string StateKey { get; init; } = PassportReadinessState.Draft;
    public string StateLabel { get; init; } = "Draft";
    public string Severity { get; init; } = PassportReadinessSeverity.Neutral;
    public string NextActionKey { get; init; } = PassportReadinessAction.Validate;
    public string NextActionLabel { get; init; } = "Validate passport";
    public string NextActionDescription { get; init; } = "Run validation to refresh readiness.";
    public bool CanValidate { get; init; } = true;
    public bool CanCompleteDemoData { get; init; }
    public bool CanSign { get; init; }
    public bool CanPublish { get; init; }
    public string PrimaryReason { get; init; } = "Save draft is allowed. Validation decides the next trust action.";
    public int BlockerCount { get; init; }
    public int WarningCount { get; init; }
    public bool TrustIsDirty { get; init; }
    public bool HasCurrentProof { get; init; }
    public bool IsPublished { get; init; }
}
