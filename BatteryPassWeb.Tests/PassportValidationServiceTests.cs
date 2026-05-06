using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportValidationServiceTests
{
    [Fact]
    public void Validate_ShouldReturnBlockingErrorsForMissingIdentityFields()
    {
        var service = CreateService();
        var document = new BsonDocument
        {
            ["registryInfo"] = new BsonDocument(),
            ["aspects"] = new BsonDocument()
        };

        var summary = service.Validate(document);

        Assert.Equal(TrustState.Invalid, summary.State);
        Assert.True(summary.BlockingErrorCount >= 2);
        Assert.Contains(summary.Sections.SelectMany(section => section.Issues), issue => issue.Path == "passportId");
        Assert.Contains(summary.Sections.SelectMany(section => section.Issues), issue => issue.Path == "registryInfo.registryId");
        Assert.False(summary.CanSign);
    }

    [Fact]
    public void Validate_ShouldAllowWarningsWithoutBlockingSigning()
    {
        var service = CreateService();
        var document = BuildIdentityOnlyDraftPassport();

        var summary = service.Validate(document);

        Assert.Equal(TrustState.Valid, summary.State);
        Assert.Equal(0, summary.BlockingErrorCount);
        Assert.True(summary.WarningCount > 0);
        Assert.True(summary.CanSign);
    }

    [Fact]
    public void Validate_ShouldRejectNegativeBatteryMass()
    {
        var service = CreateService();
        var document = BuildValidMinimalPassport();
        document["aspects"]["generalProductInformation"]["payload"]["batteryMass"] = -1;

        var summary = service.Validate(document);

        Assert.Equal(TrustState.Invalid, summary.State);
        Assert.Contains(summary.Sections.SelectMany(section => section.Issues), issue => issue.Path == "aspects.generalProductInformation.payload.batteryMass");
    }

    [Fact]
    public void Validate_ShouldReturnSchemaBlockingErrorsForIncompleteOfficialAspectPayload()
    {
        var service = CreateService();
        var document = BuildValidMinimalPassport();

        var summary = service.Validate(document);

        Assert.Equal(TrustState.Invalid, summary.State);
        Assert.Contains(
            summary.Sections.SelectMany(section => section.Issues),
            issue => issue.Severity == TrustValidationSeverity.BlockingError
                && issue.Path == "aspects.generalProductInformation.payload.productIdentifier");
        Assert.False(summary.CanSign);
    }

    private static PassportValidationService CreateService()
    {
        return new PassportValidationService(new SchemaRegistryService(), new JsonSchemaValidationService());
    }

    private static BsonDocument BuildIdentityOnlyDraftPassport()
    {
        return new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:test-001",
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = "registry-001",
                ["status"] = "draft"
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["modelNumber"] = "MODEL-1",
                    ["serialNumber"] = "SERIAL-1",
                    ["manufacturerName"] = "ACME Batteries"
                }
            },
            ["aspects"] = new BsonDocument()
        };
    }

    private static BsonDocument BuildValidMinimalPassport()
    {
        return new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:test-001",
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = "registry-001",
                ["status"] = "draft"
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["modelNumber"] = "MODEL-1",
                    ["serialNumber"] = "SERIAL-1",
                    ["manufacturerName"] = "ACME Batteries"
                }
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMass"] = 10,
                        ["batteryStatus"] = "Original"
                    }
                },
                ["performanceAndDurability"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryTechicalProperties"] = new BsonDocument
                        {
                            ["ratedEnergy"] = 120,
                            ["ratedCapacity"] = 300,
                            ["ratedMaximumPower"] = 420,
                            ["nominalVoltage"] = 800
                        }
                    }
                }
            }
        };
    }
}
