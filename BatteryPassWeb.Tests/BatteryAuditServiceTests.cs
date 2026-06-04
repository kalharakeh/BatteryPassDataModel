using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class BatteryAuditServiceTests
{
    [Fact]
    public void BuildBatteryAuditEventDocument_ShouldCaptureBatteryCreationActorAndMetadata()
    {
        var service = new BatteryAuditService();
        var metadata = new BsonDocument
        {
            ["batteryFamily"] = "Compact 7M",
            ["batteryModel"] = "2.0",
            ["softwareVersion"] = "4.0",
            ["serialNumber"] = "SN-API-001",
            ["clusterId"] = "cluster-north-operations"
        };

        var document = service.BuildBatteryAuditEventDocument(
            "battery-001",
            "battery.created",
            "Read Write Token",
            "external-api",
            "external-api",
            "Battery created.",
            metadata,
            "token-001",
            "2026-06-04T10:00:00.0000000Z");

        Assert.False(string.IsNullOrWhiteSpace(BsonHelpers.GetString(document, "eventId")));
        Assert.Equal("battery-001", BsonHelpers.GetString(document, "batteryId"));
        Assert.Equal("battery.created", BsonHelpers.GetString(document, "eventType"));
        Assert.Equal("Read Write Token", BsonHelpers.GetString(document, "actor"));
        Assert.Equal("external-api", BsonHelpers.GetString(document, "actorType"));
        Assert.Equal("token-001", BsonHelpers.GetString(document, "actorTokenId"));
        Assert.Equal("external-api", BsonHelpers.GetString(document, "source"));
        Assert.Equal("Battery created.", BsonHelpers.GetString(document, "message"));
        Assert.Equal("Compact 7M", BsonHelpers.GetString(document, "metadata", "batteryFamily"));
        Assert.Equal("2026-06-04T10:00:00.0000000Z", BsonHelpers.GetString(document, "createdAt"));
    }
}
