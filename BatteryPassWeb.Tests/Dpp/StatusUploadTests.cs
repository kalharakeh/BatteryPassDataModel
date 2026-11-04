using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Status changes and repairs by upload (design 8.2, decision 35 Q20–Q25; step 5d): read from the app's format, made
/// through the status change on the battery's page only, shown in the check with the kept values and refused by the API.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class StatusUploadTests(MongoFixture mongo)
{
    private static readonly NativeUploadFormat Format = new();

    [Theory]
    [InlineData("""{ "batterySerialNumber": "B", "reason": "repair", "repairDescription": "Fan replaced", "repairDate": "2026-10-30", "values": {} }""", StatusEvent.Repaired, "2026-10-30", null)]
    [InlineData("""{ "batterySerialNumber": "B", "measuredAt": "2026-10-28T09:00:00Z", "reason": "repair", "repairDescription": "Fan replaced" }""", StatusEvent.Repaired, "2026-10-28", null)]
    [InlineData("""{ "batterySerialNumber": "B", "values": { "batteryStatus": "waste" } }""", StatusEvent.Waste, null, null)]
    [InlineData("""{ "batterySerialNumber": "B", "values": { "batteryStatus": { "batteryStatusValues": "re-used" } } }""", StatusEvent.PlacedOnMarketAgain, null, null)]
    [InlineData("""{ "batterySerialNumber": "B", "values": { "batteryStatus": "original" } }""", null, null, "batteryStatus \"original\" can't be set by upload; it takes waste, re-used, remanufactured or repurposed, or a repair")]
    [InlineData("""{ "batterySerialNumber": "B", "reason": "repair", "repairDescription": "x", "values": { "batteryStatus": "waste" } }""", null, null, "a repair sets the battery status itself; leave batteryStatus out")]
    [InlineData("""{ "batterySerialNumber": "B", "reason": "upgrade", "values": { "stateOfChargeSoC": 5 } }""", null, null, "the only reason an entry gives is \"repair\"")]
    public void TheFormat_ShouldReadAStatusChangeOrARepair(string entry, StatusEvent? what, string? day, string? problem)
    {
        var read = Assert.Single(Format.Read(JsonNode.Parse($$"""{ "format": "dpp-v3-upload/1", "batteries": [ {{entry}} ] }""")!.AsObject()).Entries);

        Assert.Equal(problem, read.Problem);
        Assert.Equal(what, read.Status?.What);
        Assert.Equal(day, read.Status?.RepairDate);
        Assert.DoesNotContain("batteryStatus", read.Values.Keys);
    }

    [Theory]
    [InlineData("Fan replaced", "Repaired on 20 Oct 2026: “Fan replaced.” The status becomes re-used, with a new version.")]
    [InlineData("Fan replaced. ", "Repaired on 20 Oct 2026: “Fan replaced.” The status becomes re-used, with a new version.")]
    [InlineData("Fan replaced?", "Repaired on 20 Oct 2026: “Fan replaced?” The status becomes re-used, with a new version.")]
    public void TheRepairLine_ShouldEndTheDescriptionWithOneFullStop(string description, string line) =>
        Assert.Equal(line, UploadService.StatusWords(new UploadStatus(StatusEvent.Repaired, "2026-10-20", description), "2026-10-20", "re-used", done: false));

    [Fact]
    public async Task ARepairOnTheBatterysPage_ShouldBeCheckedThenSavedThroughTheStatusChange()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        var file = Repair(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 41, "capacityFade": 7 }""");

        var check = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", file);
        var battery = Assert.Single(check.Batteries);

        Assert.Null(check.Problem);
        Assert.Null(battery.Problem);
        Assert.Equal("Repaired on 20 Oct 2026: “Cell module 2 replaced.” The status becomes re-used, with a new version.", battery.Status);
        Assert.Contains("capacityFade", battery.Sent!.Keys);
        Assert.DoesNotContain("capacityFade", battery.Kept!);
        Assert.Contains("powerFade", battery.Kept!); // left out of the file: the battery's current value (Q8)
        Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId)); // a check changes nothing

        var saved = (await service.SaveCheckedAsync(PublicationTesting.Team, rig.PackId, check.CheckId))!;
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        Assert.Equal("Repaired on 20 Oct 2026: “Cell module 2 replaced.” The status became re-used, with a new version.", Assert.Single(saved.Batteries).Status);
        Assert.Equal("re-used", StatusChangeService.CurrentStatus(pack));
        Assert.Equal(("2026-10-20", "Cell module 2 replaced."), (pack.Repairs[^1].Date, pack.Repairs[^1].Description));
        Assert.Equal(7, pack.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(41, pack.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>()); // then the live values (Q22)
        Assert.Equal(2, (await rig.Store.ListVersionsAsync(rig.PackId)).Count);
        Assert.Equal("upload", (await LastAsync(rig, StatusChangeService.ChangedEvent))["source"].AsString);
        var logged = Assert.Single(Assert.Single(await rig.Store.ListUploadsAsync(10)).Batteries);
        Assert.Equal(saved.Batteries[0].Status, logged.Status);
        Assert.True(Assert.Single(logged.Values, value => value.Key == "powerFade").Kept);
    }

    [Fact]
    public async Task ARefusedStatusChange_ShouldSaveNothingOfTheBattery()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);

        var check = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", RepairWithoutWords(UploadServiceTests.Serial(rig)));
        var battery = Assert.Single(check.Batteries);

        Assert.Equal((UploadService.Refused, StatusChangeService.RepairNeedsDescription), (battery.Outcome, battery.Problem));
        Assert.Null(battery.Status);
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task TheApi_ShouldRefuseABatteryWithAStatusChange()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await UploadServiceTests.Service(rig).RunAsync(PublicationTesting.Team, JsonNode.Parse(Encoding.UTF8.GetString(Repair(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 41 }"""))), "api", null, preview: false);

        Assert.Equal((UploadService.Refused, UploadService.StatusOnThePage), (Assert.Single(report.Batteries).Outcome, report.Batteries[0].Problem));
        Assert.Equal("original", StatusChangeService.CurrentStatus((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!));
    }

    private static byte[] Repair(string serial, string values) => Encoding.UTF8.GetBytes(UploadServiceTests.File(
        $$"""{ "batterySerialNumber": "{{serial}}", "reason": "repair", "repairDescription": "Cell module 2 replaced.", "repairDate": "2026-10-20", "values": {{values}} }"""));

    private static byte[] RepairWithoutWords(string serial) => Encoding.UTF8.GetBytes(UploadServiceTests.File(
        $$"""{ "batterySerialNumber": "{{serial}}", "reason": "repair", "values": { "stateOfChargeSoC": 41 } }"""));

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
