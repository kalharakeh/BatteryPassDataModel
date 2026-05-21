using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using System.Globalization;
using System.Reflection;

namespace BatteryPassWeb.Tests;

public sealed class ManualFeedbackRound3Tests
{
    [Fact]
    public void ResetDemo_ShouldSeedCanonicalBatteriesForEveryFamilyAndModel()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

        Assert.Contains("BuildCanonicalSeedBatteryDefinitions", source);
        Assert.Contains("BatteryProductTemplateCatalog.DefaultProducts", source);
        Assert.Contains("\"cluster-north-operations\"", source);
        Assert.Contains("\"cluster-south-operations\"", source);
        Assert.Contains("\"cluster-fleet-operations\"", source);
        Assert.Contains("ProductVersions", source);
    }

    [Fact]
    public void AnonymousSampleDetail_ShouldRedirectToSummaryWithAccessMessageInsteadOfNotFound()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));

        Assert.Contains("RedirectToPublicSummaryWithAccessNotice", source);
        Assert.Contains("FallbackSamplePassport(decodedPassportId)", source);
        Assert.Contains("access=detail-required", source);
    }

    [Fact]
    public void SummaryReport_ShouldStackMainMetricRowsVertically()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-summary-report-row--power", summary);
        Assert.Contains("bp-summary-report-row--carbon", summary);
        Assert.Contains("bp-summary-report-row--recycled", summary);
        Assert.Contains(".bp-summary-report-row", css);
        Assert.Contains("grid-template-columns: 1fr;", ExtractBlock(css, ".bp-summary-section"));
    }

    [Fact]
    public void DetailedReportTelemetryCharts_ShouldUseTwoColumnsOnDesktop()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
        var telemetryGrid = ExtractBlock(css, ".bp-telemetry-chart-grid");

        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", telemetryGrid);
        Assert.Contains("bp-telemetry-chart-grid", css);
        Assert.Contains("grid-template-columns: 1fr;", css);
    }

    [Fact]
    public void NewPassportNeeded_ShouldRecalculateFromPersistedBatteryAndMaterialFields()
    {
        var delta = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportDeltaService.cs"));
        var policy = File.ReadAllText(RepoFile("web", "Services", "EditableFieldPolicyService.cs"));
        var productTemplates = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

        Assert.Contains("UpdateNewPassportRequiredByBatteryIdAsync", delta);
        Assert.Contains("ReadMaterialMass", delta);
        Assert.Contains("material.nickelMass", policy);
        Assert.Contains("UpdateNewPassportRequiredByBatteryIdAsync(", productTemplates);
        Assert.Contains("compareAllPassportData: true", productTemplates);
    }

    [Fact]
    public void NewPassportNeededComparison_ShouldDetectMaterialMassAndClearWhenReverted()
    {
        var battery = BuildMaterialBattery(51.63);
        var latestPassport = BuildMaterialBattery(51.63);
        var policy = EditableFieldPolicyService.CreateDefaultPolicy(
        [
            new EditableFieldPermission("material.nickelMass", true, true, true)
        ]);

        Assert.False(BatteryPassportDeltaService.HasEditableDifferences(battery, latestPassport, policy));

        SetNickelMass(battery, 5163);
        Assert.True(BatteryPassportDeltaService.HasEditableDifferences(battery, latestPassport, policy));

        SetNickelMass(battery, 51.63);
        Assert.False(BatteryPassportDeltaService.HasEditableDifferences(battery, latestPassport, policy));
    }

    [Fact]
    public void BatteryEditComparison_ShouldIgnoreLockedNonEditableDifferencesWhenEditableFieldIsReverted()
    {
        var battery = BuildMaterialBattery(52.74);
        var latestPassport = BuildMaterialBattery(52.74);
        AddCarbonStage(battery, "RawMaterialExtraction", 0);
        AddCarbonStage(latestPassport, "RawMaterialExtraction", 42.5);
        var policy = EditableFieldPolicyService.CreateDefaultPolicy(
        [
            new EditableFieldPermission("material.nickelMass", true, true, true),
            new EditableFieldPermission("carbon.rawMaterial", false, false, false)
        ]);

        Assert.False(BatteryPassportDeltaService.HasEditableDifferences(battery, latestPassport, policy));
        Assert.True(BatteryPassportDeltaService.HasPassportDataDifferences(battery, latestPassport));
    }

    [Fact]
    public void BatteryEditComparison_ShouldNotUseBatteryOnlyIdentityPathsAgainstPassportSnapshots()
    {
        var battery = BuildMaterialBattery(50.63);
        battery["identity"] = new BsonDocument { ["batteryModel"] = "2.0" };
        battery["app"] = new BsonDocument
        {
            ["product"] = new BsonDocument
            {
                ["productVersion"] = "2.0",
                ["batteryModel"] = "2.0"
            }
        };
        var latestPassport = BuildMaterialBattery(50.63);
        latestPassport["snapshot"] = new BsonDocument { ["batteryModel"] = "2.0" };
        latestPassport["app"] = new BsonDocument
        {
            ["product"] = new BsonDocument
            {
                ["productVersion"] = "2.0",
                ["batteryModel"] = "2.0"
            }
        };
        var policy = EditableFieldPolicyService.CreateDefaultPolicy();

        Assert.False(BatteryPassportDeltaService.HasEditableDifferences(battery, latestPassport, policy));
    }

    [Fact]
    public void BatterySave_ShouldParseDecimalInputsInvariantlyAndPreserveLockedArrayValues()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
        try
        {
            var document = BuildEditableSaveDocument();
            var form = new FormCollection(new Dictionary<string, StringValues>
            {
                ["materialNickel"] = "52.74"
            });

            InvokeApplyPassportForm(document, form);

            Assert.Equal(52.74, ReadMaterialMass(document, "Nickel"), precision: 2);
            Assert.Equal(42.5, ReadCarbonStage(document, "RawMaterialExtraction"), precision: 2);
            Assert.Equal(18.25, ReadRecycledShare(document, "Nickel", "preConsumerShare"), precision: 2);
            Assert.Equal(12.75, ReadRecycledShare(document, "Nickel", "postConsumerShare"), precision: 2);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void ConformancePage_ShouldRouteFixActionsToBatteryEditInsteadOfPassportSnapshotEdit()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.Contains("var batteryEditHref", view);
        Assert.Contains("/admin/batteries/{Uri.EscapeDataString(passport.BatteryId)}/edit", view);
        Assert.Contains("href=\"@batteryEditHref\"", view);
        Assert.Contains("Edit battery", view);
        Assert.DoesNotContain("/admin/passports/@Uri.EscapeDataString(passport.PassportId)/edit", view);
        Assert.DoesNotContain("Edit passport snapshot", view);
    }

    [Fact]
    public void PassportSnapshot_ShouldGenerateSchemaValidBatteryPassportIdentifier()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));

        Assert.Contains("NormalizeBatteryPassportIdentifier", source);
        Assert.DoesNotContain("generalPayload[\"batteryPassportIdentifier\"] = passportId;", source);
    }

    private static string ExtractBlock(string source, string selector)
    {
        var index = source.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(index >= 0, $"{selector} was not found.");
        var openBrace = source.IndexOf('{', index);
        Assert.True(openBrace >= 0, $"{selector} did not open a CSS block.");
        var closeBrace = source.IndexOf('}', openBrace);
        Assert.True(closeBrace > openBrace, $"{selector} did not close a CSS block.");
        return source[openBrace..(closeBrace + 1)];
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

    private static BsonDocument BuildMaterialBattery(double nickelMass) =>
        new()
        {
            ["aspects"] = new BsonDocument
            {
                ["materialComposition"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMaterials"] = new BsonArray
                        {
                            new BsonDocument
                            {
                                ["batteryMaterialName"] = "Nickel",
                                ["batteryMaterialMass"] = nickelMass
                            }
                        }
                    }
                }
            }
        };

    private static void SetNickelMass(BsonDocument document, double nickelMass)
    {
        var materials = document["aspects"]["materialComposition"]["payload"]["batteryMaterials"].AsBsonArray;
        materials[0].AsBsonDocument["batteryMaterialMass"] = nickelMass;
    }

    private static void AddCarbonStage(BsonDocument document, string stage, double value)
    {
        var aspects = document["aspects"].AsBsonDocument;
        aspects["carbonFootprintForBatteries"] = new BsonDocument
        {
            ["payload"] = new BsonDocument
            {
                ["carbonFootprintPerLifecycleStage"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["lifecycleStage"] = stage,
                        ["carbonFootprint"] = value
                    }
                }
            }
        };
    }

    private static BsonDocument BuildEditableSaveDocument() =>
        new()
        {
            ["passportId"] = "did:web:acme.battery.pass:test",
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["serialNumber"] = "SN-SOUTH-002",
                    ["modelNumber"] = "CP13M-SOUTH-002"
                },
                ["product"] = new BsonDocument()
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["manufacturingDate"] = "2026-05-01T00:00:00.000Z"
                    }
                },
                ["materialComposition"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMaterials"] = new BsonArray
                        {
                            new BsonDocument
                            {
                                ["batteryMaterialName"] = "Nickel",
                                ["batteryMaterialMass"] = 52.74
                            }
                        }
                    }
                },
                ["carbonFootprintForBatteries"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["batteryCarbonFootprint"] = 84.2,
                        ["carbonFootprintPerformanceClass"] = "B",
                        ["carbonFootprintPerLifecycleStage"] = new BsonArray
                        {
                            new BsonDocument
                            {
                                ["lifecycleStage"] = "RawMaterialExtraction",
                                ["carbonFootprint"] = 42.5
                            }
                        }
                    }
                },
                ["circularity"] = new BsonDocument
                {
                    ["payload"] = new BsonDocument
                    {
                        ["recycledContent"] = new BsonArray
                        {
                            new BsonDocument
                            {
                                ["recycledMaterial"] = "Nickel",
                                ["preConsumerShare"] = 18.25,
                                ["postConsumerShare"] = 12.75
                            }
                        }
                    }
                }
            }
        };

    private static void InvokeApplyPassportForm(BsonDocument document, IFormCollection form)
    {
        var method = typeof(AdminController).GetMethod(
            "ApplyPassportForm",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        method.Invoke(null, [document, form, "2026-05-21T12:00:00.000Z"]);
    }

    private static double ReadMaterialMass(BsonDocument document, string material) =>
        document["aspects"]["materialComposition"]["payload"]["batteryMaterials"]
            .AsBsonArray
            .OfType<BsonDocument>()
            .Single(row => row["batteryMaterialName"].AsString == material)["batteryMaterialMass"]
            .ToDouble();

    private static double ReadCarbonStage(BsonDocument document, string stage) =>
        document["aspects"]["carbonFootprintForBatteries"]["payload"]["carbonFootprintPerLifecycleStage"]
            .AsBsonArray
            .OfType<BsonDocument>()
            .Single(row => row["lifecycleStage"].AsString == stage)["carbonFootprint"]
            .ToDouble();

    private static double ReadRecycledShare(BsonDocument document, string material, string field) =>
        document["aspects"]["circularity"]["payload"]["recycledContent"]
            .AsBsonArray
            .OfType<BsonDocument>()
            .Single(row => row["recycledMaterial"].AsString == material)[field]
            .ToDouble();
}
