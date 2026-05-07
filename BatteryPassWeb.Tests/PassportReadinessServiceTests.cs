using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportReadinessServiceTests
{
    [Fact]
    public void Evaluate_ShouldMarkIncompletePassportAsBlockedWithCompleteDataNextAction()
    {
        var passport = Passport(status: "draft", trustState: TrustState.Invalid, isDirty: false, hasProof: false);
        var summary = Summary(TrustValidationSeverity.BlockingError);
        var decision = PublishDecision(canSign: false, canPublish: false);
        var verification = Verification(TrustState.Unvalidated, isValid: false);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.Incomplete, readiness.StateKey);
        Assert.Equal(PassportReadinessSeverity.Blocked, readiness.Severity);
        Assert.Equal(PassportReadinessAction.CompleteData, readiness.NextActionKey);
        Assert.True(readiness.CanValidate);
        Assert.True(readiness.CanCompleteDemoData);
        Assert.False(readiness.CanSign);
        Assert.False(readiness.CanPublish);
    }

    [Fact]
    public void Evaluate_ShouldMarkZeroBlockersWithoutProofAsReadyToSign()
    {
        var passport = Passport(status: "draft", trustState: TrustState.Valid, isDirty: false, hasProof: false);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: false);
        var verification = Verification(TrustState.Unvalidated, isValid: false);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.ReadyToSign, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.Sign, readiness.NextActionKey);
        Assert.True(readiness.CanSign);
        Assert.False(readiness.CanPublish);
        Assert.Equal(1, readiness.WarningCount);
    }

    [Fact]
    public void Evaluate_ShouldMarkSignedCleanUnpublishedAsReadyToPublish()
    {
        var passport = Passport(status: "draft", trustState: TrustState.Signed, isDirty: false, hasProof: true);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: true);
        var verification = Verification(TrustState.Signed, isValid: true);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.ReadyToPublish, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.Publish, readiness.NextActionKey);
        Assert.True(readiness.CanPublish);
        Assert.True(readiness.HasCurrentProof);
    }

    [Fact]
    public void Evaluate_ShouldMarkSignedCleanPublishedAsTrusted()
    {
        var passport = Passport(status: "published", trustState: TrustState.Signed, isDirty: false, hasProof: true);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: true);
        var verification = Verification(TrustState.Signed, isValid: true);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.PublishedTrusted, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.None, readiness.NextActionKey);
        Assert.Equal(PassportReadinessSeverity.Trusted, readiness.Severity);
        Assert.True(readiness.IsPublished);
    }

    [Fact]
    public void Evaluate_ShouldMarkDirtyPublishedPassportAsNeedingResign()
    {
        var passport = Passport(status: "published", trustState: TrustState.Dirty, isDirty: true, hasProof: true);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: false);
        var verification = Verification(TrustState.SignatureInvalid, isValid: false);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.DirtyNeedsResign, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.Sign, readiness.NextActionKey);
        Assert.True(readiness.TrustIsDirty);
        Assert.False(readiness.CanPublish);
    }

    [Fact]
    public void Evaluate_ShouldSendInvalidSignatureToDiagnostics()
    {
        var passport = Passport(status: "draft", trustState: TrustState.SignatureInvalid, isDirty: false, hasProof: true);
        var summary = Summary(TrustValidationSeverity.BlockingError);
        var decision = PublishDecision(canSign: false, canPublish: false);
        var verification = Verification(TrustState.SignatureInvalid, isValid: false);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.Incomplete, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.CompleteData, readiness.NextActionKey);
        Assert.Equal(PassportReadinessSeverity.Blocked, readiness.Severity);
    }

    [Fact]
    public void Evaluate_ShouldAllowSigningWhenValidationIsCleanButExistingSignatureIsInvalid()
    {
        var passport = Passport(status: "draft", trustState: TrustState.SignatureInvalid, isDirty: false, hasProof: true);
        var summary = Summary(TrustValidationSeverity.Warning);
        var decision = PublishDecision(canSign: true, canPublish: false);
        var verification = Verification(TrustState.SignatureInvalid, isValid: false);

        var readiness = new PassportReadinessService().Evaluate(passport, summary, decision, verification);

        Assert.Equal(PassportReadinessState.InvalidSignature, readiness.StateKey);
        Assert.Equal(PassportReadinessAction.Sign, readiness.NextActionKey);
        Assert.Equal("Sign passport", readiness.NextActionLabel);
        Assert.True(readiness.CanSign);
        Assert.False(readiness.CanPublish);
    }

    private static BsonDocument Passport(string status, string trustState, bool isDirty, bool hasProof)
    {
        var proof = hasProof
            ? new BsonDocument { ["proofValue"] = "proof-value" }
            : new BsonDocument();

        return new BsonDocument
        {
            ["registryInfo"] = new BsonDocument { ["status"] = status },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = hasProof && !isDirty,
                ["hash"] = hasProof ? "hash-1" : string.Empty
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = trustState,
                ["isDirty"] = isDirty,
                ["latestHash"] = hasProof ? "hash-1" : string.Empty,
                ["latestProof"] = proof
            }
        };
    }

    private static TrustValidationSummary Summary(TrustValidationSeverity severity)
    {
        return new TrustValidationSummary
        {
            State = severity == TrustValidationSeverity.BlockingError ? TrustState.Invalid : TrustState.Valid,
            Sections =
            [
                new TrustValidationSectionResult
                {
                    SectionKey = "test",
                    SectionLabel = "Test",
                    Issues = [new TrustValidationIssue(severity, "test.path", "Test issue")]
                }
            ]
        };
    }

    private static PassportPublishDecision PublishDecision(bool canSign, bool canPublish)
    {
        return new PassportPublishDecision(
            CanSign: canSign,
            CanPublish: canPublish,
            PublishBlockReason: canPublish ? string.Empty : "Blocked for test.");
    }

    private static PassportVerificationResult Verification(string state, bool isValid)
    {
        return new PassportVerificationResult
        {
            State = state,
            IsValid = isValid,
            Message = isValid ? "Signature verified." : "Signature is not valid."
        };
    }
}
