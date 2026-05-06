using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportPublishPolicyServiceTests
{
    [Fact]
    public void CanSign_ShouldAllowWarningsWhenThereAreNoBlockingErrors()
    {
        var summary = SummaryWith(TrustValidationSeverity.Warning);

        Assert.True(new PassportPublishPolicyService().CanSign(summary));
    }

    [Fact]
    public void CanSign_ShouldBlockWhenValidationHasBlockingErrors()
    {
        var summary = SummaryWith(TrustValidationSeverity.BlockingError);

        Assert.False(new PassportPublishPolicyService().CanSign(summary));
    }

    [Fact]
    public void CanPublish_ShouldBlockWithoutCurrentSignatureProof()
    {
        var passport = new BsonDocument
        {
            ["validation"] = new BsonDocument
            {
                ["isValid"] = true,
                ["hash"] = "sha256-demo"
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = TrustState.Valid,
                ["isDirty"] = false
            }
        };

        var decision = new PassportPublishPolicyService().Evaluate(passport, SummaryWith(TrustValidationSeverity.Warning));

        Assert.True(decision.CanSign);
        Assert.False(decision.CanPublish);
        Assert.Contains("current valid signature", decision.PublishBlockReason);
    }

    [Fact]
    public void CanPublish_ShouldAllowCleanValidationAndCurrentSignatureProof()
    {
        var passport = new BsonDocument
        {
            ["validation"] = new BsonDocument
            {
                ["isValid"] = true,
                ["hash"] = "sha256-demo"
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = TrustState.Signed,
                ["isDirty"] = false,
                ["latestHash"] = "sha256-demo",
                ["latestProof"] = new BsonDocument
                {
                    ["type"] = "DataIntegrityProof",
                    ["proofValue"] = "signature-demo"
                }
            }
        };

        var decision = new PassportPublishPolicyService().Evaluate(passport, SummaryWith(TrustValidationSeverity.Warning));

        Assert.True(decision.CanSign);
        Assert.True(decision.CanPublish);
        Assert.Equal(string.Empty, decision.PublishBlockReason);
    }

    [Fact]
    public void NormalizeRegistryStatus_ShouldForceDraftWhenPublishIsBlocked()
    {
        var passport = new BsonDocument();
        var summary = SummaryWith(TrustValidationSeverity.Warning);

        var normalized = new PassportPublishPolicyService().NormalizeRegistryStatus("published", passport, summary);

        Assert.Equal("draft", normalized);
    }

    [Fact]
    public void SanitizeTrustClaimsForDraftSave_ShouldRemoveFakeVerifiedState()
    {
        var passport = new BsonDocument
        {
            ["registryInfo"] = new BsonDocument { ["status"] = "published" },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = true,
                ["signedAt"] = "2026-05-06T00:00:00.000Z",
                ["hash"] = "fake-hash",
                ["signature"] = "fake-signature",
                ["proof"] = new BsonDocument { ["proofValue"] = "fake-proof" }
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = TrustState.Signed,
                ["isDirty"] = false,
                ["latestHash"] = "fake-hash",
                ["latestProof"] = new BsonDocument { ["proofValue"] = "fake-proof" }
            }
        };

        new PassportPublishPolicyService().SanitizeTrustClaimsForDraftSave(passport);

        Assert.False(passport["validation"]["isValid"].ToBoolean());
        Assert.True(passport["validation"]["signedAt"].IsBsonNull);
        Assert.Equal(string.Empty, passport["validation"]["hash"].AsString);
        Assert.Equal(string.Empty, passport["validation"]["signature"].AsString);
        Assert.Empty(passport["validation"]["proof"].AsBsonDocument);
        Assert.Equal(TrustState.Unvalidated, passport["trust"]["state"].AsString);
        Assert.False(passport["trust"]["isDirty"].ToBoolean());
        Assert.Equal(string.Empty, passport["trust"]["latestHash"].AsString);
        Assert.Empty(passport["trust"]["latestProof"].AsBsonDocument);
    }

    [Fact]
    public void InvalidateValidationClaimForDraftSave_ShouldPreserveProofDiagnostics()
    {
        var passport = new BsonDocument
        {
            ["validation"] = new BsonDocument
            {
                ["isValid"] = true,
                ["signedAt"] = "2026-05-06T00:00:00.000Z",
                ["hash"] = "hash-before-save",
                ["signature"] = "signature-before-save",
                ["proof"] = new BsonDocument { ["proofValue"] = "signature-before-save" }
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = TrustState.Signed,
                ["isDirty"] = false,
                ["latestHash"] = "hash-before-save",
                ["latestProof"] = new BsonDocument { ["proofValue"] = "signature-before-save" },
                ["latestRevisionId"] = "revision-001",
                ["lastSignedAt"] = "2026-05-06T00:00:00.000Z"
            }
        };

        new PassportPublishPolicyService().InvalidateValidationClaimForDraftSave(passport);

        Assert.False(passport["validation"]["isValid"].ToBoolean());
        Assert.True(passport["validation"]["signedAt"].IsBsonNull);
        Assert.Equal(string.Empty, passport["validation"]["hash"].AsString);
        Assert.Equal(string.Empty, passport["validation"]["signature"].AsString);
        Assert.Empty(passport["validation"]["proof"].AsBsonDocument);
        Assert.Equal(TrustState.Signed, passport["trust"]["state"].AsString);
        Assert.Equal("hash-before-save", passport["trust"]["latestHash"].AsString);
        Assert.Equal("signature-before-save", passport["trust"]["latestProof"]["proofValue"].AsString);
        Assert.Equal("revision-001", passport["trust"]["latestRevisionId"].AsString);
        Assert.Equal("2026-05-06T00:00:00.000Z", passport["trust"]["lastSignedAt"].AsString);
    }

    private static TrustValidationSummary SummaryWith(TrustValidationSeverity severity)
    {
        return new TrustValidationSummary
        {
            PassportId = "did:web:acme.battery.pass:test-001",
            State = severity == TrustValidationSeverity.BlockingError ? TrustState.Invalid : TrustState.Valid,
            ValidatedAt = DateTime.UtcNow.ToString("O"),
            Sections =
            [
                new TrustValidationSectionResult
                {
                    SectionKey = "test",
                    SectionLabel = "Test",
                    Issues =
                    [
                        new TrustValidationIssue(severity, "test.path", "Test issue")
                    ]
                }
            ]
        };
    }
}
