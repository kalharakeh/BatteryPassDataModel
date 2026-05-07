using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class AuditRevisionServiceTests
{
    [Fact]
    public void BuildRevisionDocument_ShouldDeepCloneSnapshotAndIncludeProofHashMetadata()
    {
        var service = new AuditRevisionService();
        var snapshot = BuildSnapshot();
        var proof = new BsonDocument
        {
            ["type"] = "DataIntegrityProof",
            ["proofValue"] = "signature-demo"
        };

        var revision = service.BuildRevisionDocument(
            "did:web:acme.battery.pass:test-001",
            revisionNumber: 3,
            snapshot,
            "abc123",
            proof,
            "admin@example.test",
            "2026-05-06T10:00:00.0000000Z");

        snapshot["aspects"]["generalProductInformation"]["payload"]["batteryMass"] = 99.9;
        proof["proofValue"] = "changed";

        Assert.True(revision.Contains("_id"));
        Assert.False(string.IsNullOrWhiteSpace(revision["revisionId"].AsString));
        Assert.Equal("did:web:acme.battery.pass:test-001", revision["passportId"].AsString);
        Assert.Equal(3, revision["revisionNumber"].AsInt32);
        Assert.Equal("sha256:abc123", revision["hash"].AsString);
        Assert.Equal("admin@example.test", revision["actor"].AsString);
        Assert.Equal("signed", revision["status"].AsString);
        Assert.True(revision["immutable"].AsBoolean);
        Assert.True(revision["publishedAt"].IsBsonNull);
        Assert.Equal(12.5, revision["snapshot"]["aspects"]["generalProductInformation"]["payload"]["batteryMass"].ToDouble());
        Assert.Equal("signature-demo", revision["proof"]["proofValue"].AsString);
    }

    [Fact]
    public void BuildRevisionDocument_ShouldMarkPublishedWhenPublishedAtIsProvided()
    {
        var revision = new AuditRevisionService().BuildRevisionDocument(
            "did:web:acme.battery.pass:test-001",
            revisionNumber: 1,
            BuildSnapshot(),
            "sha256:abc123",
            new BsonDocument(),
            "admin@example.test",
            "2026-05-06T10:00:00.0000000Z",
            "2026-05-06T10:05:00.0000000Z");

        Assert.Equal("published", revision["status"].AsString);
        Assert.Equal("2026-05-06T10:05:00.0000000Z", revision["publishedAt"].AsString);
        Assert.Equal("sha256:abc123", revision["hash"].AsString);
    }

    [Fact]
    public void BuildAuditEventDocument_ShouldDeepCloneMetadataAndIncludeActorSourceAndMessage()
    {
        var service = new AuditRevisionService();
        var metadata = new BsonDocument
        {
            ["revisionId"] = "rev-001",
            ["reason"] = "Validated and signed for demo publish"
        };

        var auditEvent = service.BuildAuditEventDocument(
            "did:web:acme.battery.pass:test-001",
            "passport.signed",
            "admin@example.test",
            "admin",
            "admin-ui",
            "Passport signed.",
            metadata,
            "2026-05-06T10:00:00.0000000Z");

        metadata["reason"] = "changed";

        Assert.True(auditEvent.Contains("_id"));
        Assert.False(string.IsNullOrWhiteSpace(auditEvent["eventId"].AsString));
        Assert.Equal("did:web:acme.battery.pass:test-001", auditEvent["passportId"].AsString);
        Assert.Equal("passport.signed", auditEvent["eventType"].AsString);
        Assert.Equal("admin@example.test", auditEvent["actor"].AsString);
        Assert.Equal("admin", auditEvent["actorRole"].AsString);
        Assert.Equal("admin-ui", auditEvent["source"].AsString);
        Assert.Equal("Passport signed.", auditEvent["message"].AsString);
        Assert.Equal("2026-05-06T10:00:00.0000000Z", auditEvent["createdAt"].AsString);
        Assert.Equal("Validated and signed for demo publish", auditEvent["metadata"]["reason"].AsString);
    }

    [Fact]
    public async Task CreateSignedRevisionAsync_ShouldFailWhenMongoPersistenceIsUnavailable()
    {
        var service = new AuditRevisionService();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSignedRevisionAsync(
                "did:web:acme.battery.pass:test-001",
                BuildSnapshot(),
                "abc123",
                new BsonDocument { ["proofValue"] = "signature-demo" },
                "admin@example.test",
                "2026-05-06T10:00:00.0000000Z"));

        Assert.Contains("signed revision was not recorded", exception.Message);
        Assert.Contains("trust state was not changed", exception.Message);
    }

    [Fact]
    public void AuditRevisionService_ShouldExposeLatestSignedRevisionLookup()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "AuditRevisionService.cs"));

        Assert.Contains("GetLatestSignedRevisionAsync", source);
        Assert.Contains("passportRevisions", source);
        Assert.Contains("revisionNumber", source);
        Assert.Contains("status", source);
        Assert.Contains("published", source);
        Assert.Contains("signed", source);
    }

    [Fact]
    public void BuildChangeMetadata_ShouldCaptureChangedFieldsAndIgnoreTrustSystemState()
    {
        var before = new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:test-001",
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["modelNumber"] = "M-100",
                    ["manufacturerName"] = "Old maker"
                }
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = "signed"
            },
            ["registryInfo"] = new BsonDocument
            {
                ["updatedAt"] = "2026-05-07T10:00:00Z"
            }
        };
        var after = before.DeepClone().AsBsonDocument;
        after["app"]["display"]["manufacturerName"] = "New maker";
        after["trust"]["state"] = "dirty";
        after["registryInfo"]["updatedAt"] = "2026-05-07T11:00:00Z";

        var metadata = AuditRevisionService.BuildChangeMetadata(before, after, "adminPassportSave");
        var changedFields = metadata["changedFields"].AsBsonArray.OfType<BsonDocument>().ToList();

        Assert.Equal("adminPassportSave", metadata["dirtyReason"].AsString);
        Assert.Equal(1, metadata["changedFieldCount"].AsInt32);
        var changedField = Assert.Single(changedFields);
        Assert.Equal("app.display.manufacturerName", changedField["path"].AsString);
        Assert.Equal("Old maker", changedField["before"].AsString);
        Assert.Equal("New maker", changedField["after"].AsString);
    }

    [Fact]
    public void PassportRepository_ShouldExposeTrustSignatureAndPublishPersistenceMethods()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));

        Assert.Contains("UpdateTrustSignatureAsync", source);
        Assert.Contains("PublishPassportAsync", source);
        Assert.Contains("trust.latestHash", source);
        Assert.Contains("trust.latestProof", source);
        Assert.Contains("trust.latestRevisionId", source);
        Assert.Contains("registryInfo.status", source);
    }

    private static BsonDocument BuildSnapshot()
    {
        return new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:test-001",
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMass"] = 12.5
                    }
                }
            }
        };
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
