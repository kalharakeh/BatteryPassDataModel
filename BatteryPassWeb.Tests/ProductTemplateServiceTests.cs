using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class ProductTemplateServiceTests
{
    [Fact]
    public void BatteryFamilyCatalog_ShouldSeedThreeFamiliesWithBatteryVersionsAndSoftwareParameters()
    {
        var families = BatteryProductTemplateCatalog.DefaultProducts;

        Assert.Equal(["compact-7m", "compact-13m", "core"], families.Select(family => family.ProductId).ToArray());
        Assert.All(families, family =>
        {
            Assert.NotEmpty(family.ImageUrl);
            Assert.NotEmpty(family.ProductVersions);
            Assert.Contains(family.RequiredFieldKeys, key => key == "general.product");
            Assert.Contains(family.RequiredFieldKeys, key => key == "general.productVersion");
            Assert.Contains(family.TemplateDocuments, document => document.DocumentKey == "conformityAssessment");
            Assert.Contains(family.TemplateDocuments, document => document.DocumentKey == "co2StudyReference");
            Assert.All(family.ProductVersions, version =>
            {
                Assert.False(string.IsNullOrWhiteSpace(version.SoftwareVersion));
                Assert.False(string.IsNullOrWhiteSpace(version.SoftwareReleaseDate));
                Assert.False(string.IsNullOrWhiteSpace(version.SoftwareLatestUpdate));
            });
        });
    }

    [Fact]
    public void BatteryFamilyCatalog_ShouldListBatteryVersionsNewestFirst()
    {
        var product = BatteryProductTemplateCatalog.DefaultProducts.Single(item => item.ProductId == "compact-7m");

        Assert.Equal(["2.0", "1.0"], product.ProductVersions.Select(version => version.Version).ToArray());
        Assert.Equal("4.0", product.ProductVersions.First().SoftwareVersion);
        Assert.Equal("2.0", product.ProductVersions.Last().SoftwareVersion);
    }

    [Fact]
    public void BuildPassportFromTemplate_ShouldStoreBatteryVersionAndSoftwareParameters()
    {
        var product = BatteryProductTemplateCatalog.DefaultProducts.Single(item => item.ProductId == "compact-7m");
        var productVersion = product.ProductVersions.Single(item => item.Version == "2.0");

        var passport = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
            "did:web:acme.battery.pass:versioned-001",
            product,
            productVersion,
            new ProductTemplateBatteryIdentity(),
            "2026-05-11T10:00:00.0000000Z");

        Assert.Equal("compact-7m", BsonHelpers.GetString(passport, "app", "product", "productId"));
        Assert.Equal("2.0", BsonHelpers.GetString(passport, "app", "product", "productVersion"));
        Assert.Equal(productVersion.SoftwareVersion, BsonHelpers.GetString(passport, "app", "product", "softwareVersion"));
        Assert.Equal(productVersion.SoftwareReleaseDate, BsonHelpers.GetString(passport, "app", "product", "softwareReleaseDate"));
        Assert.Equal(productVersion.SoftwareLatestUpdate, BsonHelpers.GetString(passport, "app", "product", "softwareLatestUpdate"));
        Assert.True((BsonHelpers.GetValue(passport, "app", "templateBaseline") as BsonDocument)?.ElementCount > 0);
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

        var passport = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
            "did:web:acme.battery.pass:new-empty-fields-001",
            product,
            product.LatestProductVersion,
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

        var passport = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
            "did:web:acme.battery.pass:test-template-001",
            product,
            product.LatestProductVersion,
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
        Assert.Equal(product.LatestProductVersion.SoftwareVersion, BsonHelpers.GetString(passport, "app", "product", "softwareVersion"));
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

    [Fact]
    public void ComputeSafeTemplateUpdates_ShouldUpdateBetweenBatteryVersionsAndPreserveManualOverrides()
    {
        var product = BatteryProductTemplateCatalog.DefaultProducts.Single(item => item.ProductId == "compact-7m");
        var oldVersion = product.ProductVersions.Single(item => item.Version == "1.0");
        var newVersion = product.ProductVersions.Single(item => item.Version == "2.0");

        var passport = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
            "did:web:acme.battery.pass:safe-version-push-001",
            product,
            oldVersion,
            new ProductTemplateBatteryIdentity
            {
                ModelNumber = "CP7M-TEST-001",
                SerialNumber = "SN-TEST-001",
                DisplayName = "Safe push test",
                FacilityId = "LINE-TEST"
            },
            "2026-05-11T10:00:00.0000000Z");
        var oldTemplate = BsonHelpers.GetValue(passport, "app", "templateBaseline") as BsonDocument ?? new BsonDocument();
        BsonHelpers.GetValue(passport, "aspects", "generalProductInformation", "payload")!.AsBsonDocument["batteryStatus"] = "Manually changed";

        var fresh = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
            BsonHelpers.GetString(passport, "passportId"),
            product,
            newVersion,
            new ProductTemplateBatteryIdentity
            {
                ModelNumber = BsonHelpers.GetString(passport, "app", "display", "modelNumber"),
                SerialNumber = BsonHelpers.GetString(passport, "app", "display", "serialNumber"),
                DisplayName = BsonHelpers.GetString(passport, "app", "display", "name"),
                FacilityId = BsonHelpers.GetString(passport, "app", "display", "facilityId")
            },
            "2026-05-11T11:00:00.0000000Z");
        var newTemplate = ProductTemplatePassportBuilder.BuildTemplateBaseline(fresh);

        var result = ProductTemplatePassportBuilder.ComputeSafeTemplateUpdates(passport, oldTemplate, newTemplate);

        Assert.Contains("app.product.productVersion", result.UpdatedPaths);
        Assert.Contains("app.product.softwareVersion", result.UpdatedPaths);
        Assert.Contains("aspects.generalProductInformation.payload.batteryStatus", result.SkippedOverridePaths);
        Assert.Equal("2.0", BsonHelpers.GetString(result.UpdatedPassport, "app", "product", "productVersion"));
        Assert.Equal(newVersion.SoftwareVersion, BsonHelpers.GetString(result.UpdatedPassport, "app", "product", "softwareVersion"));
        Assert.Equal("Manually changed", BsonHelpers.GetString(result.UpdatedPassport, "aspects", "generalProductInformation", "payload", "batteryStatus"));
    }

    [Fact]
    public void ProductTemplateService_ShouldSupportBatteryVersionPushWithSoftwareParameters()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

        Assert.Contains("PushProductVersionAsync", source);
        Assert.Contains("Eq(\"app.product.productVersion\", selectedProductVersion.Version)", source);
        Assert.Contains("BuildSafeTemplatePushUpdate(passport, product, selectedProductVersion", source);
        Assert.DoesNotContain("PushTemplateAsync", source);
        Assert.DoesNotContain("SoftwareCollection", source);
    }

    [Fact]
    public void ResetTemplateDemo_ShouldSeedVersionedBatteriesAndNorthCustomerAccount()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

        Assert.Contains("customer_001_001@customer.org", source);
        Assert.Contains("\"12345\"", source);
        Assert.Contains("CP7M-NORTH-001", source);
        Assert.Contains("CP7M-NORTH-002", source);
        Assert.Contains("CP13M-SOUTH-001", source);
        Assert.Contains("CP13M-SOUTH-002", source);
        Assert.Contains("CORE-FLEET-001", source);
        Assert.Contains("CORE-FLEET-002", source);
        Assert.Contains("sample-customer-north-002", source);
        Assert.Contains("sample-customer-south-002", source);
        Assert.Contains("sample-end-user-fleet-002", source);
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
