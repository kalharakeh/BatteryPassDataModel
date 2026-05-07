using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportCanonicalDataTests
{
    [Fact]
    public void ViewModelFactory_ShouldUseAspectPayloadAsSingleMaterialSource()
    {
        var passport = BsonDocument.Parse(
            """
            {
              "passportId": "did:web:local.battery.pass:canonical-material",
              "registryInfo": { "status": "draft" },
              "app": {
                "display": {
                  "modelNumber": "M-CANONICAL",
                  "serialNumber": "SN-CANONICAL",
                  "manufacturerName": "Canonical Batteries"
                },
                "charts": {
                  "materialComposition": [
                    { "label": "Nickel", "value": 9.999999999999999E+29, "unit": "kg", "color": "#000000" }
                  ]
                }
              },
              "aspects": {
                "generalProductInformation": {
                  "payload": {
                    "batteryCategory": "EV",
                    "batteryStatus": "Original",
                    "batteryMass": 499,
                    "manufacturingDate": "2026-05-06T00:00:00.000Z"
                  }
                },
                "materialComposition": {
                  "payload": {
                    "batteryMaterials": [
                      { "batteryMaterialName": "Nickel", "batteryMaterialMass": 28.4, "batteryMaterialLocation": { "componentName": "Cell" } },
                      { "batteryMaterialName": "Lithium", "batteryMaterialMass": 20.6, "batteryMaterialLocation": { "componentName": "Cell" } }
                    ]
                  }
                }
              }
            }
            """);

        var viewModel = new PassportViewModelFactory().Create(passport);

        Assert.Equal(2, viewModel.MaterialCompositionSegments.Count);
        Assert.Equal(2, viewModel.BatteryMaterials.Count);
        Assert.Equal(
            viewModel.MaterialCompositionSegments.Select(row => (row.Label, row.Value)).OrderBy(row => row.Label),
            viewModel.BatteryMaterials.Select(row => (row.MaterialName, row.MaterialMass)).OrderBy(row => row.MaterialName));
        Assert.DoesNotContain(viewModel.BatteryMaterials, row => row.MaterialMass > 500);
    }

    [Fact]
    public void DemoCompletion_ShouldWriteReadableCanonicalAspectValues()
    {
        var passport = BsonDocument.Parse(
            """
            {
              "passportId": "did:web:local.battery.pass:demo-readable",
              "registryInfo": { "registryId": "demo-readable", "status": "draft" },
              "app": {
                "display": {
                  "modelNumber": "DemoPack-Readable",
                  "serialNumber": "SN-DEMO-READABLE",
                  "manufacturerName": "Demo Batteries GmbH"
                }
              }
            }
            """);

        var completed = new DemoRequiredDataCompletionService(new SchemaRegistryService())
            .CompleteRequiredData(passport, "2026-05-06T12:00:00.0000000Z");

        var materialRows = BsonHelpers.GetValue(
            completed,
            "aspects",
            "materialComposition",
            "payload",
            "batteryMaterials")!.AsBsonArray.OfType<BsonDocument>().ToList();
        var supplyChainIndex = BsonHelpers.GetValue(
            completed,
            "aspects",
            "supplyChainDueDiligence",
            "payload",
            "supplyChainIndicies")!.ToDouble();
        var carbonFootprint = BsonHelpers.GetValue(
            completed,
            "aspects",
            "carbonFootprintForBatteries",
            "payload",
            "batteryCarbonFootprint")!.ToDouble();

        Assert.Equal(8, materialRows.Count);
        Assert.Equal(20.6, materialRows.Single(row => BsonHelpers.GetString(row, "batteryMaterialName") == "Lithium")["batteryMaterialMass"].ToDouble(), 1);
        Assert.All(materialRows, row => Assert.InRange(row["batteryMaterialMass"].ToDouble(), 0.1, 500));
        Assert.InRange(supplyChainIndex, 0, 100);
        Assert.InRange(carbonFootprint, 0, 1000);
        Assert.Matches("^[A-E]$", BsonHelpers.GetString(completed, "aspects", "carbonFootprintForBatteries", "payload", "carbonFootprintPerformanceClass"));
        Assert.False(BsonHelpers.GetValue(completed, "app", "charts", "materialComposition") is BsonArray);
    }

    [Fact]
    public void CanonicalCatalog_ShouldPreserveOrRepairOfficialGeneralIdentityValues()
    {
        Assert.Equal(
            "urn:bmwk:123456687678",
            BatteryPassCanonicalDataCatalog.NormalizeBatteryPassportIdentifier(
                "urn:bmwk:123456687678",
                "SN-WITH-DASHES",
                "did:web:local.battery.pass:test"));
        Assert.Equal(
            "urn:acme:snwithdashes",
            BatteryPassCanonicalDataCatalog.NormalizeBatteryPassportIdentifier(
                "urn:acme:SN-WITH-DASHES",
                "SN-WITH-DASHES",
                "did:web:local.battery.pass:test"));
        Assert.Equal("lmt", BatteryPassCanonicalDataCatalog.NormalizeBatteryCategory("LMT"));
        Assert.Equal("industrial", BatteryPassCanonicalDataCatalog.NormalizeBatteryCategory("Compact 7M"));
    }

    [Fact]
    public void CanonicalCatalog_ShouldRepairUrnValuesThatWereSavedAsManufacturerSerialNumbers()
    {
        Assert.Equal(
            "SN-0226151E949CD067",
            BatteryPassCanonicalDataCatalog.NormalizeManufacturerSerialNumber(
                "urn:local:0226151e949cd0678ef3162431e28976",
                "did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976"));
        Assert.Equal(
            "SERIAL-42",
            BatteryPassCanonicalDataCatalog.NormalizeManufacturerSerialNumber(
                "SERIAL-42",
                "did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976"));
    }

    [Fact]
    public void ViewModelFactory_ShouldNotDisplayBatteryPassportIdentifierAsSerialNumber()
    {
        var passport = BsonDocument.Parse(
            """
            {
              "passportId": "did:web:local.battery.pass:no-serial",
              "registryInfo": { "status": "draft" },
              "app": {
                "display": {
                  "modelNumber": "M-NO-SERIAL"
                }
              },
              "aspects": {
                "generalProductInformation": {
                  "payload": {
                    "batteryPassportIdentifier": "urn:local:0226151e949cd0678ef3162431e28976"
                  }
                }
              }
            }
            """);

        var viewModel = new PassportViewModelFactory().Create(passport);

        Assert.Equal(string.Empty, viewModel.SerialNumber);
    }

    [Fact]
    public void AdminSave_ShouldUseOfficialSchemaIdentityNormalizers()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("NormalizeBatteryPassportIdentifier", controller);
        Assert.Contains("NormalizeBatteryCategory", controller);
        Assert.Contains("NormalizeManufacturerSerialNumber", controller);
        Assert.DoesNotContain("generalPayload[\"batteryPassportIdentifier\"] = $\"urn:acme:", controller);
        Assert.DoesNotContain("generalPayload[\"batteryCategory\"] = BatteryImageCatalog.CategoryForImageUrl", controller);
    }

    [Fact]
    public void NormalizationService_ShouldCleanExistingMongoShapeIntoCanonicalPayloads()
    {
        var normalizerType = Type.GetType("BatteryPassWeb.Services.PassportDataNormalizationService, BatteryPassWeb");
        Assert.NotNull(normalizerType);

        var normalizer = Activator.CreateInstance(normalizerType!);
        var normalize = normalizerType!.GetMethod("Normalize");
        Assert.NotNull(normalize);

        var passport = BsonDocument.Parse(
            """
            {
              "passportId": "did:web:local.battery.pass:normalizer",
              "registryInfo": { "status": "published" },
              "trust": {
                "state": "signed",
                "isDirty": false,
                "latestHash": "sha256-old",
                "latestProof": { "proofValue": "proof-old" }
              },
              "app": {
                "display": {
                  "serialNumber": "urn:local:0226151e949cd0678ef3162431e28976"
                },
                "charts": {
                  "materialComposition": [
                    { "label": "Nickel", "value": 28.4, "unit": "kg", "color": "#4f6f7d" },
                    { "label": "Lithium", "value": 20.6, "unit": "kg", "color": "#85c7d6" }
                  ],
                  "carbonFootprint": [
                    { "label": "raw material extraction", "value": 21.0, "unit": "gCO2e/kWh", "color": "#08a348" }
                  ]
                }
              },
              "aspects": {
                "generalProductInformation": {
                  "payload": {
                    "batteryPassportIdentifier": "bad-passport-id",
                    "batteryCategory": "Compact 7M"
                  }
                },
                "materialComposition": {
                  "payload": {
                    "batteryMaterials": [
                      { "batteryMaterialName": "Lithium", "batteryMaterialMass": 1.6111172E+38 }
                    ]
                  }
                },
                "supplyChainDueDiligence": {
                  "payload": {
                    "supplyChainIndicies": 2.1624482E+38
                  }
                },
                "carbonFootprintForBatteries": {
                  "payload": {
                    "batteryCarbonFootprint": 1.7976931348623157E+308,
                    "carbonFootprintPerformanceClass": "eOMtThyhVNLWUZNRcBaQKxI",
                    "carbonFootprintPerLifecycleStage": [
                      { "lifecycleStage": "RawMaterialExtraction", "carbonFootprint": 1.7976931348623157E+308 }
                    ]
                  }
                }
              }
            }
            """);

        var normalized = Assert.IsType<BsonDocument>(normalize!.Invoke(normalizer, [passport, "2026-05-07T00:00:00.0000000Z"]));
        var materials = BsonHelpers.GetValue(
            normalized,
            "aspects",
            "materialComposition",
            "payload",
            "batteryMaterials")!.AsBsonArray.OfType<BsonDocument>().ToList();

        Assert.Equal(2, materials.Count);
        Assert.Equal("SN-0226151E949CD067", BsonHelpers.GetString(normalized, "app", "display", "serialNumber"));
        Assert.Equal("urn:acme:sn0226151e949cd067", BsonHelpers.GetString(normalized, "aspects", "generalProductInformation", "payload", "batteryPassportIdentifier"));
        Assert.Equal("industrial", BsonHelpers.GetString(normalized, "aspects", "generalProductInformation", "payload", "batteryCategory"));
        Assert.Equal(20.6, materials.Single(row => BsonHelpers.GetString(row, "batteryMaterialName") == "Lithium")["batteryMaterialMass"].ToDouble(), 1);
        Assert.InRange(BsonHelpers.GetValue(normalized, "aspects", "supplyChainDueDiligence", "payload", "supplyChainIndicies")!.ToDouble(), 0, 100);
        Assert.Equal("B", BsonHelpers.GetString(normalized, "aspects", "carbonFootprintForBatteries", "payload", "carbonFootprintPerformanceClass"));
        Assert.InRange(BsonHelpers.GetValue(normalized, "aspects", "carbonFootprintForBatteries", "payload", "batteryCarbonFootprint")!.ToDouble(), 0, 1000);
        Assert.False(BsonHelpers.GetValue(normalized, "app", "charts", "materialComposition") is BsonArray);
        Assert.True(BsonHelpers.GetValue(normalized, "trust", "isDirty")!.AsBoolean);
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
