using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round three, 6 (decision 63): the battery system's export gives a production date only when it can be true: not
/// before 2015-01 and not after the export's month. A real core's export of 11 November 2026 carried the placeholder
/// 2000-01. The file below has that export's shape with made-up identifiers: eight modules without serial numbers or
/// connectivity IDs, the extreme-temperature times at 0, and fields the app doesn't use.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ExportProductionDateTests(MongoFixture mongo)
{
    private static readonly BmsExportFormat Adapter = new();

    [Theory]
    [InlineData("2000-01", false)]
    [InlineData("2014-12", false)]
    [InlineData("2015-01", true)]
    [InlineData("2026-11", true)] // the export's own month
    [InlineData("2026-12", false)] // after the export
    public void TheProductionDate_ShouldBeTakenOnlyWhenItCanBeTrue(string produced, bool taken)
    {
        var entry = Assert.Single(Adapter.Read(RealShape("VPC9-0200", produced)).Entries);

        Assert.Equal(taken, entry.Values.ContainsKey("manufacturingDate"));
        Assert.Equal(!taken, entry.Notes!.Contains($"the export's production date {produced} can't be right, so #17 is left as it is; type it"));
    }

    [Fact]
    public void AnExportDatedInTheFuture_ShouldNotLiftTheLimit()
    {
        var export = RealShape("VPC9-0200", "2098-01");
        export["metadata"]!["exportedAt"] = "2099-01-01T00:00:00Z";

        var entry = Assert.Single(Adapter.Read(export).Entries);

        Assert.False(entry.Values.ContainsKey("manufacturingDate")); // round four, 7: the earlier of exportedAt's month and now
    }

    [Fact]
    public void TheRealExportsShape_ShouldStillBeRead()
    {
        var entry = Assert.Single(Adapter.Read(RealShape("VPC9-0200", "2000-01")).Entries);

        Assert.Null(entry.Problem);
        Assert.Equal("VPC9-0200", entry.NamedAs);
        Assert.Equal(64, entry.Values["stateOfChargeSoC"]!.GetValue<int>());
        Assert.Equal(3, entry.Values["numberOfFullChargingAndDischargingCycles"]!.GetValue<int>());
        Assert.Equal(0, entry.Values["timeSpentInExtremeTemperaturesAboveBoundary"]!.GetValue<int>());
        Assert.Equal(0, entry.Values["timeSpentInExtremeTemperaturesBelowBoundary"]!.GetValue<int>());
        Assert.Equal(0, entry.Values["timeSpentChargingDuringExtremeTemperaturesAboveBoundary"]!.GetValue<int>());
        Assert.False(entry.Values.ContainsKey("manufacturingDate"));
        Assert.Contains(BmsExportFormat.NoTemperature, entry.Notes!);
    }

    [Fact]
    public async Task ABatteryCreatedFromSuchAnExport_ShouldKeepItsManufacturingDateEmpty_AndSaySo()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var created = await BatteryTesting.Creation(rig).CreateAsync(
            PublicationTesting.Team,
            new NewPack(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "VPC9-0200" }));

        var report = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, created.Pack!.Id, "export.json", Encoding.UTF8.GetBytes(RealShape("VPC9-0200", "2000-01").ToJsonString()));

        var battery = Assert.Single(report.Batteries);
        Assert.Contains("the export's production date 2000-01 can't be right, so #17 is left as it is; type it", battery.Notes!);
        Assert.DoesNotContain(battery.Values, value => value.Key == "manufacturingDate");
        Assert.False((await rig.Store.GetAsync<BatteryPack>(created.Pack.Id))!.Values.ContainsKey("manufacturingDate"));
    }

    /// <summary>
    /// The shape of the real core's export (BMS2_ACO 01.32.07, 11 Nov 2026), with made-up identifiers and no person's
    /// details: eight modules without serial numbers or connectivity IDs, the placeholder production date, and the
    /// fields the app doesn't read (service date, discharge totals, deep discharge events, two histograms).
    /// </summary>
    internal static JsonObject RealShape(string serial, string productionDate)
    {
        var modules = new JsonArray();
        for (var position = 1; position <= 8; position++)
        {
            modules.Add(new JsonObject { ["serialNumber"] = string.Empty, ["connectivityId"] = string.Empty, ["position"] = position });
        }

        return new JsonObject
        {
            ["version"] = 1,
            ["metadata"] = new JsonObject
            {
                ["exportedAt"] = "2026-11-11T09:12:44.120Z",
                ["identifiers"] = new JsonObject
                {
                    ["deviceIdentifiers"] = new JsonObject { ["serialNumber"] = serial, ["connectivityId"] = "ST-MADE-UP", ["position"] = 1, ["childrenIdentifiers"] = modules },
                    ["parentIdentifiers"] = new JsonObject { ["serialNumber"] = "SYSTEM-MADE-UP", ["connectivityId"] = "SY-MADE-UP" },
                },
                ["address"] = "192.0.2.20",
                ["mappingName"] = "BMS2_ACO",
                ["mappingVersion"] = "01.32.07",
            },
            ["values"] = new JsonObject
            {
                ["remainingCapacityAh"] = new JsonObject { ["raw"] = 128, ["formatted"] = "128.0 Ah" },
                ["productionDate"] = new JsonObject { ["raw"] = new JsonArray(50, 48, 48, 48, 45, 48, 49), ["formatted"] = productionDate },
                ["serviceDate"] = new JsonObject { ["raw"] = new JsonArray(50, 48, 48, 48, 45, 48, 49), ["formatted"] = "2000-01" },
                ["totalChargedEnergyKwh"] = new JsonObject { ["raw"] = 41, ["formatted"] = "41.0 kWh" },
                ["totalDischargedEnergyKwh"] = new JsonObject { ["raw"] = 37, ["formatted"] = "37.0 kWh" },
                ["totalDischargedChargeAh"] = new JsonObject { ["raw"] = 520, ["formatted"] = "520.0 Ah" },
                ["equivalentChargeAndDischargeCycles"] = new JsonObject { ["raw"] = 3.4, ["formatted"] = "3.4" },
                ["deepDischargeEvents"] = new JsonObject { ["raw"] = 0, ["formatted"] = "0" },
                ["timeSpentInExtremeHighTemperature"] = new JsonObject { ["raw"] = 0, ["formatted"] = "0.000 s" },
                ["timeSpentInExtremeLowTemperature"] = new JsonObject { ["raw"] = 0, ["formatted"] = "0.000 s" },
                ["timeSpentChargingInExtremeTemperature"] = new JsonObject { ["raw"] = 0, ["formatted"] = "0.000 s" },
                ["stateOfChargeHistogram"] = new JsonObject { ["raw"] = new JsonArray(0, 12, 40, 31, 9, 0), ["formatted"] = "6 bins" },
                ["temperatureHistogram"] = new JsonObject { ["raw"] = new JsonArray(0, 3, 55, 34, 0, 0), ["formatted"] = "6 bins" },
                ["stateOfCharge"] = new JsonObject { ["raw"] = 40960, ["formatted"] = "64.0 %" },
            },
        };
    }
}
