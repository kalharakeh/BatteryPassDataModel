using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class TrustWorkflowAcceptanceTests
{
    [Fact]
    public void IncompletePassport_ShouldNotBeSignableOrPubliclyVisible()
    {
        var validation = CreateValidationService().Validate(new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:acceptance-incomplete",
            ["registryInfo"] = new BsonDocument { ["status"] = "draft" },
            ["app"] = new BsonDocument { ["display"] = new BsonDocument() }
        });

        var policy = new PassportPublishPolicyService();
        var passport = new BsonDocument
        {
            ["registryInfo"] = new BsonDocument { ["status"] = "published" },
            ["validation"] = new BsonDocument { ["isValid"] = false },
            ["trust"] = new BsonDocument { ["state"] = TrustState.Invalid }
        };

        Assert.True(validation.BlockingErrorCount > 0);
        Assert.False(policy.CanSign(validation));
        Assert.False(policy.IsPubliclyVisible(passport));
    }

    [Fact]
    public void SignedCleanPublishedPassport_ShouldBePubliclyVisible()
    {
        var passport = PublishedSignedPassport();

        Assert.True(new PassportPublishPolicyService().IsPubliclyVisible(passport));
    }

    [Fact]
    public void DraftUnsignedDirtyOrInvalidSignaturePassports_ShouldNotBePubliclyVisible()
    {
        var policy = new PassportPublishPolicyService();

        Assert.False(policy.IsPubliclyVisible(PublishedSignedPassport(status: "draft")));
        Assert.False(policy.IsPubliclyVisible(PublishedSignedPassport(hasProof: false)));
        Assert.False(policy.IsPubliclyVisible(PublishedSignedPassport(isDirty: true)));
        Assert.False(policy.IsPubliclyVisible(PublishedSignedPassport(trustState: TrustState.SignatureInvalid)));
    }

    [Fact]
    public void SignedCleanUnpublishedPassport_ShouldBePublishableByPolicy()
    {
        var summary = new TrustValidationSummary
        {
            State = TrustState.Valid,
            Sections =
            [
                new TrustValidationSectionResult
                {
                    SectionKey = "identity",
                    SectionLabel = "Identity",
                    Issues = [new TrustValidationIssue(TrustValidationSeverity.Passed, "identity", "ok")]
                }
            ]
        };

        var decision = new PassportPublishPolicyService().Evaluate(PublishedSignedPassport(status: "draft"), summary);

        Assert.True(decision.CanSign);
        Assert.True(decision.CanPublish);
    }

    private static PassportValidationService CreateValidationService()
    {
        return new PassportValidationService(new SchemaRegistryService(), new JsonSchemaValidationService());
    }

    private static BsonDocument PublishedSignedPassport(
        string status = "published",
        string trustState = TrustState.Signed,
        bool isDirty = false,
        bool hasProof = true)
    {
        var hash = "sha256-acceptance";
        return new BsonDocument
        {
            ["registryInfo"] = new BsonDocument { ["status"] = status },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = true,
                ["hash"] = hash
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = trustState,
                ["isDirty"] = isDirty,
                ["latestHash"] = hash,
                ["latestProof"] = hasProof
                    ? new BsonDocument { ["proofValue"] = "acceptance-proof" }
                    : new BsonDocument()
            }
        };
    }
}
