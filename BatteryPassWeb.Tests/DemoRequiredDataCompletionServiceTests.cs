using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class DemoRequiredDataCompletionServiceTests
{
    [Fact]
    public void CompleteRequiredData_ShouldMakeOfficialSchemaPayloadsSignableWithoutWeakeningValidation()
    {
        var passport = BuildPartialPassport();
        var completionService = new DemoRequiredDataCompletionService(new SchemaRegistryService());
        var validationService = CreateValidationService();

        var completed = completionService.CompleteRequiredData(passport, "2026-05-06T12:00:00.0000000Z");
        var summary = validationService.Validate(completed);
        var blockers = string.Join(
            Environment.NewLine,
            summary.Sections.SelectMany(section => section.Issues
                .Where(issue => issue.Severity == TrustValidationSeverity.BlockingError)
                .Select(issue => $"{section.SectionLabel}: {issue.Path} - {issue.Message}")));

        Assert.True(summary.CanSign, blockers);
        Assert.Equal(0, summary.BlockingErrorCount);
    }

    [Fact]
    public void CompleteRequiredData_ShouldPreservePassportIdentityAndOperationalAppData()
    {
        var passport = BuildPartialPassport();
        var completionService = new DemoRequiredDataCompletionService(new SchemaRegistryService());

        var completed = completionService.CompleteRequiredData(passport, "2026-05-06T12:00:00.0000000Z");

        Assert.Equal("did:web:local.battery.pass:test", BsonHelpers.GetString(completed, "passportId"));
        Assert.Equal("local-registry-test", BsonHelpers.GetString(completed, "registryInfo", "registryId"));
        Assert.Equal("DemoPack-42", BsonHelpers.GetString(completed, "app", "display", "modelNumber"));
        Assert.Equal("SN-42", BsonHelpers.GetString(completed, "app", "display", "serialNumber"));
        Assert.Equal(25.9, BsonHelpers.GetValue(completed, "app", "operations", "latestTelemetry", "currentVoltageV")!.ToDouble(), 1);
    }

    private static PassportValidationService CreateValidationService()
    {
        return new PassportValidationService(new SchemaRegistryService(), new JsonSchemaValidationService());
    }

    private static BsonDocument BuildPartialPassport()
    {
        return BsonDocument.Parse(
            """
            {
              "passportId": "did:web:local.battery.pass:test",
              "registryInfo": {
                "registryId": "local-registry-test",
                "status": "draft",
                "createdAt": "2026-05-06T00:00:00.0000000Z",
                "updatedAt": "2026-05-06T00:00:00.0000000Z"
              },
              "app": {
                "display": {
                  "name": "Strict validation demo battery",
                  "modelNumber": "DemoPack-42",
                  "serialNumber": "SN-42",
                  "manufacturerName": "Demo Batteries GmbH"
                },
                "media": {
                  "batteryImageUrl": "https://example.test/battery.jpg"
                },
                "operations": {
                  "latestTelemetry": {
                    "currentVoltageV": 25.9
                  }
                }
              },
              "aspects": {
                "generalProductInformation": {
                  "payload": {
                    "batteryPassportIdentifier": "bad-passport-id",
                    "batteryCategory": "EV",
                    "warrentyPeriod": "two years"
                  }
                }
              }
            }
            """);
    }
}
