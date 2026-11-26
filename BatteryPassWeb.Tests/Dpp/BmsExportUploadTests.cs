using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The battery system's export, mapping BMS2_ACO (design 8.3, decisions 17, 18, 28 and 31; step 5b): the live values in
/// the schema's units, E1-E4 in whole minutes, #17 for the team, and the module list, through the same upload engine.
/// The export here is made up, in the shape of the samples from 27 and 28 Oct 2026.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BmsExportUploadTests(MongoFixture mongo)
{
    private static readonly BmsExportFormat Adapter = new();

    [Fact]
    public void AFilledExport_ShouldGiveTheLiveValuesInTheSchemasUnits()
    {
        var entry = Assert.Single(Read(Export("VPC9-0001")).Entries);

        Assert.Equal(("VPC9-0001", new DateTime(2026, 10, 28, 7, 40, 26, 319, DateTimeKind.Utc)), (entry.NamedAs, entry.MeasuredAtUtc));
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["stateOfChargeSoC"] = "27",
                ["numberOfFullChargingAndDischargingCycles"] = "12",
                ["timeSpentInExtremeTemperaturesAboveBoundary"] = "59",
                ["timeSpentInExtremeTemperaturesBelowBoundary"] = "121",
                ["timeSpentChargingDuringExtremeTemperaturesAboveBoundary"] = "0",
                ["timeSpentChargingDuringExtremeTemperaturesBelowBoundary"] = "0",
                ["manufacturingDate"] = "\"2026-03\"",
            },
            entry.Values.ToDictionary(pair => pair.Key, pair => pair.Value!.ToJsonString()));
        Assert.Equal([(1, null, "MD-A1"), (2, "MD-2", "MD-A2")], entry.Modules!.Select(module => (module.Position, module.SerialNumber, module.ConnectivityId)));
        Assert.Equal([BmsExportFormat.NoTemperature], entry.Notes);
        Assert.Equal(["manufacturingDate"], entry.FillOnly);
        Assert.Equal(126.0m, entry.RemainingCapacityAh);
    }

    [Fact]
    public void AnExportWithEmptyValues_ShouldGiveNoValuesButItsModules()
    {
        var export = Export("VPC9-0002");
        foreach (var (_, value) in (JsonObject)export["values"]!)
        {
            value!["raw"] = null;
            value["formatted"] = null;
        }

        var entry = Assert.Single(Read(export).Entries);

        Assert.Empty(entry.Values);
        Assert.Equal(2, entry.Modules!.Count);
    }

    [Fact]
    public void ChargingInExtremeTemperatures_ShouldLeaveE3AndE4AsTheyAreAndSaySo()
    {
        var export = Export("VPC9-0003");
        export["values"]!["timeSpentChargingInExtremeTemperature"]!["raw"] = 30;

        var entry = Assert.Single(Read(export).Entries);

        Assert.DoesNotContain("timeSpentChargingDuringExtremeTemperaturesAboveBoundary", entry.Values.Keys);
        Assert.DoesNotContain("timeSpentChargingDuringExtremeTemperaturesBelowBoundary", entry.Values.Keys);
        Assert.Contains(BmsExportFormat.ChargingAboveZero, entry.Notes!);
    }

    [Fact]
    public void AnUnsetProductionDateOrAnUnreadableStateOfCharge_ShouldBeLeftOut()
    {
        var export = Export("VPC9-0004");
        export["values"]!["productionDate"] = JsonNode.Parse("""{ "raw": [0, 0, 0, 0, 0, 0, null], "formatted": [0, 0, 0, 0, 0, 0, null] }""");
        export["values"]!["stateOfCharge"]!["formatted"] = "17323";

        var entry = Assert.Single(Read(export).Entries);

        Assert.DoesNotContain("manufacturingDate", entry.Values.Keys);
        Assert.DoesNotContain("stateOfChargeSoC", entry.Values.Keys);
        Assert.Contains("the export's state of charge \"17323\" isn't a percentage, so #63 is left as it is", entry.Notes!);
    }

    [Fact]
    public void AnotherMappingOrVersion_ShouldBeRefusedOrNoted()
    {
        var other = Export("VPC9-0005");
        other["metadata"]!["mappingName"] = "BMS3_XYZ";
        var newer = Export("VPC9-0005");
        newer["metadata"]!["mappingVersion"] = "01.40.00";
        var anonymous = Export("VPC9-0005");
        anonymous["metadata"]!["identifiers"]!["deviceIdentifiers"]!["serialNumber"] = "";

        Assert.Equal("the export's mapping \"BMS3_XYZ\" isn't one this app reads; it reads BMS2_ACO", Read(other).Problem);
        Assert.Equal("BMS2_ACO 01.40.00", Read(newer).Format);
        Assert.Contains("read as mapping version 01.32.07, the one this app was checked against", Assert.Single(Read(newer).Entries).Notes!);
        Assert.Equal("the export has no serial number (metadata.identifiers.deviceIdentifiers.serialNumber)", Assert.Single(Read(anonymous).Entries).Problem);
    }

    [Fact]
    public async Task ABatterySystemsExport_ShouldSaveItsLiveValuesAndModulesThroughTheApi()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await DppApiWritingTests.TokensAsync(rig);

        var answer = Assert.IsType<ObjectResult>(await DppApiControllerTests.Controller(rig, tokens["north-write"])
            .Upload(JsonDocument.Parse(Export(UploadServiceTests.Serial(rig)).ToJsonString()).RootElement.Clone(), false, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, answer.StatusCode);
        var data = DppApiControllerTests.Json(answer).GetProperty("data");
        Assert.Equal("BMS2_ACO 01.32.07", data.GetProperty("format").GetString());
        var battery = Assert.Single(data.GetProperty("batteries").EnumerateArray());
        var outcomes = battery.GetProperty("values").EnumerateArray().ToDictionary(value => value.GetProperty("key").GetString()!, value => value.GetProperty("outcome").GetString());
        Assert.DoesNotContain("manufacturingDate", outcomes.Keys); // the battery has one: left out, not refused (Q15)
        Assert.Equal(6, outcomes.Count);
        Assert.All(outcomes, pair => Assert.Equal("saved", pair.Value));
        Assert.Equal("the module list was set: 2 modules", battery.GetProperty("modules").GetString());
        Assert.Equal([BmsExportFormat.NoTemperature, BmsExportUploadTests.DateNotUsed], battery.GetProperty("notes").EnumerateArray().Select(note => note.GetString()));

        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal((27, "feed"), (pack.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>(), pack.Values["stateOfChargeSoC"].Source));
        Assert.Equal(59, pack.Values["timeSpentInExtremeTemperaturesAboveBoundary"].Value!.GetValue<int>());
        Assert.Equal(new DateTime(2026, 10, 28, 7, 40, 26, 319, DateTimeKind.Utc), pack.Values["timeSpentInExtremeTemperaturesAboveBoundary"].GeneratedAtUtc);
        Assert.Equal(["MD-A1", "MD-A2"], pack.Modules.Select(module => module.ConnectivityId));
        var logged = Assert.Single(Assert.Single(await rig.Store.ListUploadsAsync(10)).Batteries);
        Assert.Equal("the module list was set: 2 modules", logged.Modules);
        Assert.Equal([BmsExportFormat.NoTemperature, DateNotUsed], logged.Notes);
        Assert.Equal("Modules of battery VPC8C05-6788 set from the battery system's export: 2 modules", (await LastAsync(rig, BatteryPages.ModulesSetEvent))["message"].AsString);
    }

    [Fact]
    public async Task TheTeamsExportOfADraftWithoutADate_ShouldAloneFillTheManufacturingDate()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var kept = await Run(rig, PublicationTesting.Team, Export(UploadServiceTests.Serial(rig)));
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), ["manufacturingDate"]);
        var clusterAdmin = await Run(rig, ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), Export(UploadServiceTests.Serial(rig)), preview: true);

        var filled = await Run(rig, PublicationTesting.Team, Export(UploadServiceTests.Serial(rig)));

        Assert.DoesNotContain(Assert.Single(kept.Batteries).Values, value => value.Key == "manufacturingDate");
        Assert.Contains(DateNotUsed, Assert.Single(kept.Batteries).Notes!);
        Assert.Equal(UploadService.NotFound, Assert.Single(clusterAdmin.Batteries).Problem); // a draft is the team's (J-6)
        Assert.Equal(KeyOutcome.Saved, Assert.Single(Assert.Single(filled.Batteries).Values, value => value.Key == "manufacturingDate").Outcome);
        Assert.DoesNotContain(DateNotUsed, Assert.Single(filled.Batteries).Notes!);
        Assert.Equal("2026-03", (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["manufacturingDate"].Value!.GetValue<string>());
    }

    [Fact]
    public async Task APublishedBatteryWithoutADate_ShouldNotTakeTheExportsDate()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), ["manufacturingDate"]);

        var report = await Run(rig, PublicationTesting.Team, Export(UploadServiceTests.Serial(rig)), preview: true);

        Assert.DoesNotContain(Assert.Single(report.Batteries).Values, value => value.Key == "manufacturingDate");
        Assert.Contains(DateNotUsed, Assert.Single(report.Batteries).Notes!);
    }

    internal const string DateNotUsed = "the manufacturing date in the file isn't used";

    [Fact]
    public async Task TheRemainingCapacity_ShouldBeKeptForTheStatusWindowUnlessANewerOneIs()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var feed = ChangeSender.Feed("north-feed", BatteryTesting.North);
        var older = Export(UploadServiceTests.Serial(rig));
        older["metadata"]!["exportedAt"] = "2026-10-27T07:00:00Z";
        older["values"]!["remainingCapacityAh"]!["formatted"] = "150.0 Ah";

        await Run(rig, feed, Export(UploadServiceTests.Serial(rig)), preview: true);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.RemainingCapacity);
        await Run(rig, feed, Export(UploadServiceTests.Serial(rig)));
        await Run(rig, feed, older);

        var kept = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.RemainingCapacity!;
        Assert.Equal((126.0m, new DateTime(2026, 10, 28, 7, 40, 26, 319, DateTimeKind.Utc)), (kept.Ah, kept.MeasuredAtUtc));
        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        Assert.Equal(126.0m, card.Remaining!.Ah);
    }

    [Fact]
    public async Task TheSameModulesAgain_ShouldBeLeftAsTheyAreAndAPreviewShouldSaveNone()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var feed = ChangeSender.Feed("north-feed", BatteryTesting.North);

        var preview = await Run(rig, feed, Export(UploadServiceTests.Serial(rig)), preview: true);
        Assert.Equal("the module list would be set: 2 modules", Assert.Single(preview.Batteries).Modules);
        Assert.Empty((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Modules);

        await Run(rig, feed, Export(UploadServiceTests.Serial(rig)));
        var again = await Run(rig, feed, Export(UploadServiceTests.Serial(rig)));

        Assert.Equal("the module list is unchanged", Assert.Single(again.Batteries).Modules);
        Assert.Equal(1, await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("eventType", BatteryPages.ModulesSetEvent)));
    }

    /// <summary>A made-up export of one battery, in the shape of mapping BMS2_ACO 01.32.07.</summary>
    internal static JsonObject Export(string serial) => JsonNode.Parse($$"""
        {
          "version": 1,
          "metadata": {
            "exportedAt": "2026-10-28T07:40:26.319Z",
            "identifiers": {
              "deviceIdentifiers": {
                "serialNumber": "{{serial}}",
                "connectivityId": "ST-TEST",
                "position": 1,
                "childrenIdentifiers": [
                  { "serialNumber": "", "connectivityId": "MD-A1", "position": 1 },
                  { "serialNumber": "MD-2", "connectivityId": "MD-A2", "position": 2 }
                ]
              },
              "parentIdentifiers": { "serialNumber": "SYSTEM-TEST", "connectivityId": "SY-TEST" }
            },
            "address": "192.0.2.10",
            "mappingName": "BMS2_ACO",
            "mappingVersion": "01.32.07"
          },
          "values": {
            "remainingCapacityAh": { "raw": 126, "formatted": "126.0 Ah" },
            "productionDate": { "raw": [50, 48, 50, 54, 45, 48, 51], "formatted": "2026-03" },
            "totalChargedEnergyKwh": { "raw": 174, "formatted": "174.0 kWh" },
            "equivalentChargeAndDischargeCycles": { "raw": 12.7, "formatted": "12.7" },
            "timeSpentInExtremeHighTemperature": { "raw": 3599, "formatted": "3.599 ks" },
            "timeSpentInExtremeLowTemperature": { "raw": 7260, "formatted": "7.260 ks" },
            "timeSpentChargingInExtremeTemperature": { "raw": 0, "formatted": "0.000 s" },
            "stateOfCharge": { "raw": 17323, "formatted": "27.1 %" },
            "canPayloadBase64": { "raw": [126, 0], "formatted": "fgA=" }
          },
          "exportee": { "name": "Test Person", "email": "test.person@example.com" }
        }
        """)!.AsObject();

    private static UploadDocument Read(JsonObject export)
    {
        Assert.True(Adapter.Reads(export));
        return Adapter.Read(export);
    }

    private static Task<UploadReport> Run(PublicationRig rig, ChangeSender sender, JsonObject export, bool preview = false) =>
        UploadServiceTests.Service(rig).RunAsync(sender, export, "page", "export.json", preview);

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
