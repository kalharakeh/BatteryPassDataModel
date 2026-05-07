using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class DataCompletionPolicyServiceTests
{
    [Fact]
    public void DefaultPolicy_ShouldListAdminEditableFieldsBySection()
    {
        var policy = DataCompletionPolicyService.CreateDefaultPolicy();
        var fields = policy.Sections.SelectMany(section => section.Fields).ToList();

        Assert.Contains(policy.Sections, section => section.SectionKey == "general" && section.Label == "General");
        Assert.Contains(policy.Sections, section => section.SectionKey == "materialComposition" && section.Label == "Material composition");
        Assert.Contains(policy.Sections, section => section.SectionKey == "performance" && section.Label == "Performance");
        Assert.Contains(policy.Sections, section => section.SectionKey == "compliance" && section.Label == "Compliance");
        Assert.Contains(policy.Sections, section => section.SectionKey == "supplyChain" && section.Label == "Supply chain");
        Assert.Contains(policy.Sections, section => section.SectionKey == "circularity" && section.Label == "Circularity");
        Assert.Contains(policy.Sections, section => section.SectionKey == "carbonFootprint" && section.Label == "Carbon Footprint");

        Assert.Contains(fields, field => field.FieldKey == "general.modelNumber" && field.IsRequired);
        Assert.Contains(fields, field => field.FieldKey == "general.batteryImageUrl" && !field.IsRequired);
        Assert.Contains(fields, field => field.FieldKey == "material.lithiumMass" && field.Label == "Lithium kg");
        Assert.Contains(fields, field => field.FieldKey == "performance.ratedEnergy" && field.DataPath.Contains("ratedEnergy"));
        Assert.Contains(fields, field => field.FieldKey == "compliance.euDeclarationOfConformity");
        Assert.Contains(fields, field => field.FieldKey == "supplyChain.dueDiligenceReport");
        Assert.Contains(fields, field => field.FieldKey == "circularity.recycledNickelPre");
        Assert.Contains(fields, field => field.FieldKey == "carbon.co2StudyReference");
    }

    [Fact]
    public void Validation_ShouldBlockMissingFieldsRequiredByCompletionPolicy()
    {
        var service = CreateValidationService();
        var policy = DataCompletionPolicyService.CreateDefaultPolicy();
        var document = BuildIdentityOnlyDraftPassport();

        var summary = service.Validate(document, policy);

        Assert.Equal(TrustState.Invalid, summary.State);
        Assert.Contains(
            summary.Sections.SelectMany(section => section.Issues),
            issue => issue.Severity == TrustValidationSeverity.BlockingError
                && issue.Path == "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy");
        Assert.False(summary.CanSign);
    }

    [Fact]
    public void Validation_ShouldIgnoreFieldsMarkedOptionalByCompletionPolicy()
    {
        var service = CreateValidationService();
        var policy = DataCompletionPolicyService.CreateDefaultPolicy(new Dictionary<string, bool>
        {
            ["performance.ratedEnergy"] = false
        });
        var document = BuildIdentityOnlyDraftPassport();

        var summary = service.Validate(document, policy);

        Assert.DoesNotContain(
            summary.Sections.SelectMany(section => section.Issues),
            issue => issue.Severity == TrustValidationSeverity.BlockingError
                && issue.Path == "aspects.performanceAndDurability.payload.batteryTechicalProperties.ratedEnergy");
    }

    [Fact]
    public void PolicySnapshot_ShouldSerializeToMongoDocumentForPersistence()
    {
        var policy = DataCompletionPolicyService.CreateDefaultPolicy(new Dictionary<string, bool>
        {
            ["general.batteryImageUrl"] = true
        });

        var document = DataCompletionPolicyService.ToBsonDocument(policy, "admin@example.test", "2026-05-06T10:00:00.0000000Z");

        Assert.Equal("batteryPassportCompletion:v1", document["policyKey"].AsString);
        Assert.Equal("admin@example.test", document["updatedBy"].AsString);
        Assert.True(document["sections"].AsBsonArray.Count >= 7);
        Assert.Contains(
            document["sections"].AsBsonArray.SelectMany(section => section["fields"].AsBsonArray),
            field => field["fieldKey"].AsString == "general.batteryImageUrl" && field["isRequired"].AsBoolean);
    }

    private static PassportValidationService CreateValidationService()
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
                    ["name"] = "Demo battery",
                    ["modelNumber"] = "MODEL-1",
                    ["serialNumber"] = "SERIAL-1",
                    ["manufacturerName"] = "ACME Batteries",
                    ["facilityId"] = "FAC-1"
                }
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryCategory"] = "lmt",
                        ["batteryStatus"] = "Original",
                        ["batteryMass"] = 10,
                        ["manufacturingDate"] = "2026-05-06T00:00:00.000Z"
                    }
                }
            }
        };
    }
}
