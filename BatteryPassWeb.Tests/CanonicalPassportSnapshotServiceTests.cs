using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class CanonicalPassportSnapshotServiceTests
{
    [Fact]
    public void BuildSnapshot_ShouldIncludeSignedCoreAndExcludeOperationalFields()
    {
        var passport = BuildPassport();

        var snapshot = new CanonicalPassportSnapshotService(new SchemaRegistryService()).BuildSnapshot(passport);

        Assert.Equal("did:web:acme.battery.pass:test-001", snapshot["passportId"].AsString);
        Assert.Equal("registry-001", snapshot["registryInfo"]["registryId"].AsString);
        Assert.True(snapshot["schemaVersions"].AsBsonDocument.Contains("generalProductInformation"));
        Assert.Equal(12.5, snapshot["aspects"]["generalProductInformation"]["payload"]["batteryMass"].ToDouble());
        Assert.Equal("https://example.test/report.pdf", snapshot["app"]["documents"]["sustainabilityReport"]["url"].AsString);
        Assert.False(snapshot.Contains("_id"));
        Assert.False(snapshot.Contains("clusterId"));
        Assert.False(snapshot.Contains("trust"));
        Assert.False(snapshot.Contains("validation"));
        Assert.False(snapshot["app"].AsBsonDocument.Contains("operations"));
        Assert.False(snapshot["app"].AsBsonDocument.Contains("charts"));
    }

    [Fact]
    public void Canonicalize_ShouldBeStableAcrossBsonFieldOrder()
    {
        var service = new CanonicalPassportSnapshotService(new SchemaRegistryService());
        var first = BuildPassport();
        var second = new BsonDocument
        {
            ["aspects"] = first["aspects"].DeepClone(),
            ["app"] = first["app"].DeepClone(),
            ["registryInfo"] = first["registryInfo"].DeepClone(),
            ["passportId"] = first["passportId"].DeepClone()
        };

        var firstCanonical = service.Canonicalize(service.BuildSnapshot(first));
        var secondCanonical = service.Canonicalize(service.BuildSnapshot(second));

        Assert.Equal(firstCanonical, secondCanonical);
        Assert.Equal(service.Sha256Hex(firstCanonical), service.Sha256Hex(secondCanonical));
    }

    [Fact]
    public void BuildSnapshot_ShouldExcludeRegistryStatusSoPublishDoesNotInvalidateSignature()
    {
        var service = new CanonicalPassportSnapshotService(new SchemaRegistryService());
        var draft = BuildPassport();
        var published = BuildPassport();
        published["registryInfo"]["status"] = "published";

        var draftHash = service.Sha256Hex(service.Canonicalize(service.BuildSnapshot(draft)));
        var publishedHash = service.Sha256Hex(service.Canonicalize(service.BuildSnapshot(published)));

        Assert.Equal(draftHash, publishedHash);
    }

    private static BsonDocument BuildPassport()
    {
        return new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["passportId"] = "did:web:acme.battery.pass:test-001",
            ["clusterId"] = "cluster-001",
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = "registry-001",
                ["status"] = "draft",
                ["createdAt"] = "2026-05-06T00:00:00.000Z",
                ["updatedAt"] = "2026-05-06T01:00:00.000Z"
            },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = true,
                ["hash"] = "fake-hash"
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = "signed",
                ["latestHash"] = "fake-hash"
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["modelNumber"] = "MODEL-1",
                    ["serialNumber"] = "SERIAL-1"
                },
                ["documents"] = new BsonDocument
                {
                    ["sustainabilityReport"] = new BsonDocument
                    {
                        ["url"] = "https://example.test/report.pdf",
                        ["fileId"] = "file-001",
                        ["sha256"] = "abc123"
                    }
                },
                ["operations"] = new BsonDocument
                {
                    ["latestTelemetry"] = new BsonDocument
                    {
                        ["currentVoltageV"] = 800,
                        ["measuredAt"] = "2026-05-06T02:00:00.000Z"
                    }
                },
                ["charts"] = new BsonDocument
                {
                    ["materialComposition"] = new BsonArray { 1, 2, 3 }
                }
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["visibility"] = "public",
                    ["verification"] = new BsonDocument
                    {
                        ["state"] = "draft"
                    },
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMass"] = 12.5,
                        ["batteryStatus"] = "Original"
                    }
                }
            }
        };
    }
}
