using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class ProductTemplateServiceTests
{
    [Fact]
    public void ProductTemplateCatalog_ShouldSeedThreeProductsWithSoftwareVersionsAndEvidence()
    {
        var products = BatteryProductTemplateCatalog.DefaultProducts;

        Assert.Equal(["compact-7m", "compact-13m", "core"], products.Select(product => product.ProductId).ToArray());
        Assert.All(products, product =>
        {
            Assert.Equal(["1.0", "2.0", "3.0"], product.SoftwareVersions.Select(version => version.Version).ToArray());
            Assert.NotEmpty(product.ImageUrl);
            Assert.Contains(product.RequiredFieldKeys, key => key == "general.product");
            Assert.Contains(product.TemplateDocuments, document => document.DocumentKey == "conformityAssessment");
            Assert.Contains(product.TemplateDocuments, document => document.DocumentKey == "co2StudyReference");
        });
    }

    [Fact]
    public void ProductTemplateCatalog_ShouldUseDistinctSharedValuesAcrossProducts()
    {
        var compact7 = BatteryProductTemplateCatalog.DefaultProducts.Single(product => product.ProductId == "compact-7m");
        var compact13 = BatteryProductTemplateCatalog.DefaultProducts.Single(product => product.ProductId == "compact-13m");
        var core = BatteryProductTemplateCatalog.DefaultProducts.Single(product => product.ProductId == "core");

        Assert.NotEqual(compact7.BatteryMassKg, compact13.BatteryMassKg);
        Assert.NotEqual(compact13.BatteryMassKg, core.BatteryMassKg);
        Assert.NotEqual(compact7.RatedEnergyKwh, compact13.RatedEnergyKwh);
        Assert.NotEqual(compact13.RatedEnergyKwh, core.RatedEnergyKwh);
        Assert.NotEqual(compact7.MaterialMassesKg["Lithium"], compact13.MaterialMassesKg["Lithium"]);
        Assert.NotEqual(compact13.MaterialMassesKg["Lithium"], core.MaterialMassesKg["Lithium"]);
        Assert.NotEqual(compact7.CarbonFootprint, compact13.CarbonFootprint);
        Assert.NotEqual(compact13.CarbonFootprint, core.CarbonFootprint);
        Assert.NotEqual(compact7.RecycledContent["Nickel"].PreConsumerShare, compact13.RecycledContent["Nickel"].PreConsumerShare);
        Assert.NotEqual(compact13.RecycledContent["Nickel"].PreConsumerShare, core.RecycledContent["Nickel"].PreConsumerShare);
    }

    [Fact]
    public void BuildPassportFromTemplate_ShouldKeepBatterySpecificFieldsEmptyWhenIdentityIsEmpty()
    {
        var product = BatteryProductTemplateCatalog.DefaultProducts.Single(item => item.ProductId == "compact-7m");
        var software = product.SoftwareVersions.Single(item => item.Version == "1.0");

        var passport = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
            "did:web:acme.battery.pass:new-empty-fields-001",
            product,
            software,
            new ProductTemplateBatteryIdentity(),
            "2026-05-08T10:00:00.0000000Z");

        Assert.Equal(string.Empty, BsonHelpers.GetString(passport, "app", "display", "name"));
        Assert.Equal(string.Empty, BsonHelpers.GetString(passport, "app", "display", "modelNumber"));
        Assert.Equal(string.Empty, BsonHelpers.GetString(passport, "app", "display", "serialNumber"));
        Assert.Equal(string.Empty, BsonHelpers.GetString(passport, "app", "display", "facilityId"));
        Assert.Equal(string.Empty, BsonHelpers.GetString(passport, "aspects", "generalProductInformation", "payload", "productIdentifier"));
        Assert.Equal(string.Empty, BsonHelpers.GetString(passport, "aspects", "generalProductInformation", "payload", "manufacturingDate"));
    }

    [Fact]
    public void BuildPassportFromTemplate_ShouldCopyProductDataAndPreserveBatterySpecificFields()
    {
        var product = BatteryProductTemplateCatalog.DefaultProducts.Single(item => item.ProductId == "compact-7m");
        var software = product.SoftwareVersions.Single(item => item.Version == "2.0");

        var passport = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
            "did:web:acme.battery.pass:test-template-001",
            product,
            software,
            new ProductTemplateBatteryIdentity
            {
                ModelNumber = "CT-7M-TEST-001",
                SerialNumber = "SN-TEMPLATE-001",
                DisplayName = "Template test battery",
                FacilityId = "NORTH-LINE-01",
                ClusterId = "cluster-north-operations",
                ManufacturingDate = "2026-05-08"
            },
            "2026-05-08T10:00:00.0000000Z");

        Assert.Equal("did:web:acme.battery.pass:test-template-001", BsonHelpers.GetString(passport, "passportId"));
        Assert.Equal("cluster-north-operations", BsonHelpers.GetString(passport, "clusterId"));
        Assert.Equal("Compact 7M", BsonHelpers.GetString(passport, "app", "product", "productName"));
        Assert.Equal("2.0", BsonHelpers.GetString(passport, "app", "product", "softwareVersion"));
        Assert.Equal("CT-7M-TEST-001", BsonHelpers.GetString(passport, "app", "display", "modelNumber"));
        Assert.Equal("SN-TEMPLATE-001", BsonHelpers.GetString(passport, "app", "display", "serialNumber"));
        Assert.Equal("industrial", BsonHelpers.GetString(passport, "aspects", "generalProductInformation", "payload", "batteryCategory"));
        Assert.Equal(7, BsonHelpers.GetValue(passport, "app", "product", "moduleCount")!.ToInt32());
        Assert.True((BsonHelpers.GetValue(passport, "app", "templateBaseline") as BsonDocument)?.ElementCount > 0);
    }

    [Fact]
    public void ComputeSafeTemplateUpdates_ShouldSkipManualOverridesAndUpdateMatchingTemplateFields()
    {
        var oldTemplate = new BsonDocument
        {
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMass"] = 320.0,
                        ["batteryStatus"] = "Original"
                    }
                }
            }
        };
        var newTemplate = oldTemplate.DeepClone().AsBsonDocument;
        newTemplate["aspects"]["generalProductInformation"]["payload"]["batteryMass"] = 330.0;
        newTemplate["aspects"]["generalProductInformation"]["payload"]["batteryStatus"] = "Updated";

        var passport = new BsonDocument
        {
            ["app"] = new BsonDocument { ["templateBaseline"] = oldTemplate.DeepClone() },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMass"] = 320.0,
                        ["batteryStatus"] = "Manually changed"
                    }
                }
            }
        };

        var diff = ProductTemplatePassportBuilder.ComputeSafeTemplateUpdates(passport, oldTemplate, newTemplate);

        Assert.Contains("aspects.generalProductInformation.payload.batteryMass", diff.UpdatedPaths);
        Assert.Contains("aspects.generalProductInformation.payload.batteryStatus", diff.SkippedOverridePaths);
        Assert.Equal(330.0, BsonHelpers.GetValue(diff.UpdatedPassport, "aspects", "generalProductInformation", "payload", "batteryMass")!.ToDouble());
        Assert.Equal("Manually changed", BsonHelpers.GetString(diff.UpdatedPassport, "aspects", "generalProductInformation", "payload", "batteryStatus"));
    }
}
