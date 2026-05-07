using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportEvidenceServiceTests
{
    [Fact]
    public void Evaluate_ShouldBlockWhenRequiredEvidenceIsMissing()
    {
        var passport = PassportWithDocuments();
        var policy = Policy(required: true);

        var result = new PassportEvidenceService().Evaluate(passport, latestSignedRevision: null, policy);

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.MissingRequired, item.Status);
        Assert.True(item.IsRequired);
        Assert.True(item.IsBlocking);
        Assert.Equal(1, result.MissingRequiredCount);
        Assert.Equal(1, result.BlockingCount);
    }

    [Fact]
    public void Evaluate_ShouldTreatUploadedHashWithoutRevisionAsUnsigned()
    {
        var passport = PassportWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-1", "/api/files/file-1", "abc123", "private")
        });

        var result = new PassportEvidenceService().Evaluate(passport, latestSignedRevision: null, Policy(required: true));

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.UploadedUnsigned, item.Status);
        Assert.False(item.IsBlocking);
        Assert.Equal(1, result.UploadedUnsignedCount);
    }

    [Fact]
    public void Evaluate_ShouldVerifyHashWhenCurrentAndSignedHashesMatch()
    {
        var passport = PassportWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-1", "/api/files/file-1", "abc123", "public")
        });
        var revision = RevisionWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-1", "/api/files/file-1", "sha256:abc123", "public")
        });

        var result = new PassportEvidenceService().Evaluate(passport, revision, Policy(required: true));

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.Verified, item.Status);
        Assert.Equal("sha256:abc123", item.CurrentHash);
        Assert.Equal("sha256:abc123", item.SignedHash);
        Assert.Equal(1, result.VerifiedCount);
        Assert.Equal(1, result.PublicCount);
    }

    [Fact]
    public void Evaluate_ShouldShowChangedSinceSigningWhenHashDiffers()
    {
        var passport = PassportWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-2", "/api/files/file-2", "newhash", "private")
        });
        var revision = RevisionWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = DocumentReference("file-1", "/api/files/file-1", "oldhash", "private")
        });

        var result = new PassportEvidenceService().Evaluate(passport, revision, Policy(required: true));

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.ChangedSinceSigning, item.Status);
        Assert.False(item.IsBlocking);
        Assert.Equal(1, result.ChangedSinceSigningCount);
        Assert.Equal(1, result.RestrictedCount);
    }

    [Fact]
    public void Evaluate_ShouldMarkRequiredExternalLinkWithoutHashAsBlockingEvidence()
    {
        var passport = PassportWithDocuments(new BsonDocument
        {
            ["conformityAssessment"] = new BsonDocument
            {
                ["url"] = "https://example.test/conformity.pdf",
                ["visibility"] = "public"
            }
        });

        var result = new PassportEvidenceService().Evaluate(passport, latestSignedRevision: null, Policy(required: true));

        var item = Assert.Single(result.Items, evidence => evidence.DocumentKey == "conformityAssessment");
        Assert.Equal(PassportEvidenceStatus.ExternalLinkOnly, item.Status);
        Assert.True(item.IsBlocking);
        Assert.Equal(1, result.BlockingCount);
    }

    [Fact]
    public void BuildValidationSection_ShouldConvertBlockingEvidenceToValidationIssue()
    {
        var passport = PassportWithDocuments();
        var result = new PassportEvidenceService().Evaluate(passport, latestSignedRevision: null, Policy(required: true));

        var section = PassportEvidenceService.BuildValidationSection(result);

        Assert.Equal("supportingEvidence", section.SectionKey);
        Assert.Contains(section.Issues, issue =>
            issue.Severity == TrustValidationSeverity.BlockingError
            && issue.Path == "app.documents.conformityAssessment");
    }

    private static BsonDocument PassportWithDocuments(BsonDocument? documents = null)
    {
        return new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:evidence-test-001",
            ["app"] = new BsonDocument
            {
                ["documents"] = documents ?? new BsonDocument()
            }
        };
    }

    private static BsonDocument RevisionWithDocuments(BsonDocument documents)
    {
        return new BsonDocument
        {
            ["revisionNumber"] = 4,
            ["status"] = "signed",
            ["snapshot"] = PassportWithDocuments(documents)
        };
    }

    private static BsonDocument DocumentReference(string fileId, string url, string sha256, string visibility)
    {
        return new BsonDocument
        {
            ["fileId"] = fileId,
            ["url"] = url,
            ["sha256"] = sha256,
            ["visibility"] = visibility,
            ["contentType"] = "application/pdf"
        };
    }

    private static DataCompletionPolicySnapshot Policy(bool required)
    {
        return new DataCompletionPolicySnapshot
        {
            PolicyKey = DataCompletionPolicyService.PolicyKey,
            Sections =
            [
                new DataRequirementSection
                {
                    SectionKey = "compliance",
                    Label = "Compliance",
                    SortOrder = 1,
                    Fields =
                    [
                        new DataRequirementField
                        {
                            FieldKey = "compliance.conformityAssessment",
                            SectionKey = "compliance",
                            Label = "Conformity assessment report",
                            DataPath = "aspects.labeling.payload.resultOfTestReport",
                            Guidance = "Upload the conformity assessment report.",
                            DefaultRequired = required,
                            IsRequired = required,
                            SortOrder = 1
                        }
                    ]
                }
            ]
        };
    }
}
