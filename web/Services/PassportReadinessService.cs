using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportReadinessService
{
    public PassportReadinessDecision Evaluate(
        BsonDocument passport,
        TrustValidationSummary summary,
        PassportPublishDecision publishDecision,
        PassportVerificationResult verificationResult)
    {
        var isPublished = BsonHelpers.GetString(passport, "registryInfo", "status")
            .Equals("published", StringComparison.OrdinalIgnoreCase);
        var trustState = BsonHelpers.GetString(passport, "trust", "state");
        var isDirty = GetBoolean(passport, "trust", "isDirty")
            || trustState.Equals(TrustState.Dirty, StringComparison.OrdinalIgnoreCase);
        var hasCurrentProof = HasCurrentProof(passport);
        var blockers = summary.BlockingErrorCount;
        var warnings = summary.WarningCount;

        if (blockers > 0)
        {
            return Decision(
                PassportReadinessState.Incomplete,
                "Incomplete data",
                PassportReadinessSeverity.Blocked,
                PassportReadinessAction.CompleteData,
                "Complete required data",
                "Required Battery Pass data is missing or invalid.",
                "Fix the blocking validation issues before signing.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canValidate: true,
                canCompleteDemoData: true,
                canSign: false);
        }

        if (isDirty)
        {
            return Decision(
                PassportReadinessState.DirtyNeedsResign,
                "Changed: validate required",
                PassportReadinessSeverity.Warning,
                PassportReadinessAction.Validate,
                "Validate passport",
                "The passport changed after the latest signature.",
                "Validate the current data before signing a new proof.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canValidate: true,
                canCompleteDemoData: false,
                canSign: false);
        }

        if (verificationResult.State.Equals(TrustState.SignatureInvalid, StringComparison.OrdinalIgnoreCase)
            && publishDecision.CanSign)
        {
            return Decision(
                PassportReadinessState.InvalidSignature,
                "Invalid signature",
                PassportReadinessSeverity.Blocked,
                PassportReadinessAction.Sign,
                "Sign passport",
                "Validation is clean, but the current proof is missing, stale, or invalid.",
                "Signature verification failed. Sign the current passport core again before publishing or relying on this proof.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canValidate: false,
                canCompleteDemoData: false);
        }

        if (verificationResult.State.Equals(TrustState.SignatureInvalid, StringComparison.OrdinalIgnoreCase))
        {
            return Decision(
                PassportReadinessState.InvalidSignature,
                "Invalid signature",
                PassportReadinessSeverity.Blocked,
                PassportReadinessAction.ReviewDiagnostics,
                "Review diagnostics",
                "The proof could not verify against the current passport core.",
                "Signature verification failed. Review proof and hash diagnostics before relying on this passport.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canValidate: false,
                canCompleteDemoData: false,
                canSign: false);
        }

        if (publishDecision.CanPublish && isPublished && verificationResult.IsValid)
        {
            return Decision(
                PassportReadinessState.PublishedTrusted,
                "Published and trusted",
                PassportReadinessSeverity.Trusted,
                PassportReadinessAction.None,
                "No action needed",
                "This passport is public, clean, signed, and verifiable.",
                "The latest signed revision is published and current.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canValidate: false,
                canCompleteDemoData: false);
        }

        if (publishDecision.CanPublish)
        {
            return Decision(
                PassportReadinessState.ReadyToPublish,
                "Ready to publish",
                PassportReadinessSeverity.Ready,
                PassportReadinessAction.Publish,
                "Publish passport",
                "A current valid signature proof exists.",
                "Publish this revision to make the passport publicly discoverable.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canValidate: false,
                canCompleteDemoData: false);
        }

        if (publishDecision.CanSign)
        {
            return Decision(
                hasCurrentProof ? PassportReadinessState.SignedClean : PassportReadinessState.ReadyToSign,
                hasCurrentProof ? "Signed clean" : "Ready to sign",
                PassportReadinessSeverity.Ready,
                PassportReadinessAction.Sign,
                "Sign passport",
                hasCurrentProof ? "A proof exists but publish is still blocked." : "Validation has zero blocking errors.",
                hasCurrentProof ? publishDecision.PublishBlockReason : "Create a proof over the current canonical passport core.",
                publishDecision,
                blockers,
                warnings,
                isDirty,
                hasCurrentProof,
                isPublished,
                canValidate: false,
                canCompleteDemoData: false);
        }

        return Decision(
            PassportReadinessState.Draft,
            "Draft",
            PassportReadinessSeverity.Neutral,
            PassportReadinessAction.Validate,
            "Validate passport",
            "Run validation to refresh readiness.",
            "Draft saves are allowed, but trust actions require validation.",
            publishDecision,
            blockers,
            warnings,
            isDirty,
            hasCurrentProof,
            isPublished,
            canValidate: true,
            canCompleteDemoData: false,
            canSign: false);
    }

    private static PassportReadinessDecision Decision(
        string stateKey,
        string stateLabel,
        string severity,
        string nextActionKey,
        string nextActionLabel,
        string nextActionDescription,
        string primaryReason,
        PassportPublishDecision publishDecision,
        int blockers,
        int warnings,
        bool isDirty,
        bool hasCurrentProof,
        bool isPublished,
        bool canValidate,
        bool canCompleteDemoData,
        bool? canSign = null)
    {
        return new PassportReadinessDecision
        {
            StateKey = stateKey,
            StateLabel = stateLabel,
            Severity = severity,
            NextActionKey = nextActionKey,
            NextActionLabel = nextActionLabel,
            NextActionDescription = nextActionDescription,
            PrimaryReason = primaryReason,
            CanValidate = canValidate,
            CanCompleteDemoData = canCompleteDemoData,
            CanSign = canSign ?? (publishDecision.CanSign && blockers == 0),
            CanPublish = publishDecision.CanPublish,
            BlockerCount = blockers,
            WarningCount = warnings,
            TrustIsDirty = isDirty,
            HasCurrentProof = hasCurrentProof,
            IsPublished = isPublished
        };
    }

    private static bool HasCurrentProof(BsonDocument passport)
    {
        var latestHash = BsonHelpers.GetString(passport, "trust", "latestHash");
        var proof = BsonHelpers.GetValue(passport, "trust", "latestProof") as BsonDocument;
        var proofValue = proof == null ? string.Empty : BsonHelpers.GetString(proof, "proofValue");

        return !string.IsNullOrWhiteSpace(latestHash) && !string.IsNullOrWhiteSpace(proofValue);
    }

    private static bool GetBoolean(BsonDocument document, params string[] path)
    {
        var value = BsonHelpers.GetValue(document, path);
        return value is { IsBoolean: true } && value.AsBoolean;
    }
}
