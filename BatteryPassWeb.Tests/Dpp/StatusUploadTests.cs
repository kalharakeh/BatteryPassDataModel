using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Status changes and repairs by upload (design 8.2, decision 35 Q20–Q25; step 5d): read from the app's format, made
/// through the status change on the battery's page only, shown in the check with the kept values, refused by the API, and
/// the help page's sample files.
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

    [Fact]
    public async Task TheCheck_ShouldShowTheStatusLineAndTheKeptValuesInWords()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", Repair(UploadServiceTests.Serial(rig), """{ "capacityFade": 7 }"""));
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var view = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, check.CheckId, null)).Check!;

        Assert.True(view.WillChange);
        Assert.StartsWith("Repaired on 20 Oct 2026", view.Status, StringComparison.Ordinal);
        Assert.Equal("Will be saved", Assert.Single(view.Values, row => row.Label.StartsWith("#59", StringComparison.Ordinal)).Result);
        var kept = Assert.Single(view.Values, row => row.Label.StartsWith("#69", StringComparison.Ordinal));
        Assert.Equal(("Kept: the battery's current value", string.Empty, true), (kept.Result, kept.InFile, kept.Quiet));
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_UploadCard.cshtml"));
        Assert.Contains("check.Status is { } status", card, StringComparison.Ordinal);
        Assert.Contains("upload.Status is { } uploadStatus", card, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCheck_ShouldSayWhyAStatusChangeIsRefused_AndOfferNoSave()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", RepairWithoutWords(UploadServiceTests.Serial(rig)));
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var view = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, check.CheckId, null)).Check!;

        Assert.Equal((false, "Say what was repaired.", (string?)null), (view.WillChange, view.Problem, view.Status));
    }

    [Fact]
    public async Task TheEarlierUploads_ShouldSayStatusChanged_AndNameABatterySystemSo()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = UploadServiceTests.Service(rig);
        var check = await service.CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", Repair(UploadServiceTests.Serial(rig), """{ "capacityFade": 7 }"""));
        await service.SaveCheckedAsync(PublicationTesting.Team, rig.PackId, check.CheckId);
        rig.Clock.Now = rig.Clock.Now.AddMinutes(5);
        var feed = ChangeSender.Feed("api-token:feed-1", BatteryTesting.North);
        await service.RunAsync(feed, JsonNode.Parse(UploadServiceTests.File($$"""{ "batterySerialNumber": "{{UploadServiceTests.Serial(rig)}}", "values": { "stateOfChargeSoC": 40 } }""")), "api", null, preview: false);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var history = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, null, null)).History;

        Assert.Equal(("Battery system · upload file", "1 value saved", (string?)null), (history[0].WhoAndWhat, history[0].Result, history[0].Status));
        Assert.Equal("Status changed", history[1].Result);
        Assert.Equal("Repaired on 20 Oct 2026: “Cell module 2 replaced.” The status became re-used, with a new version.", history[1].Status);
    }

    [Fact]
    public void TheHelpsSamples_ShouldBeForSignedInTeamsAndClusterAdmins_AndReadable()
    {
        var action = typeof(HelpController).GetMethod(nameof(HelpController.Sample))!;
        var controller = new HelpController(null!);
        var clock = new MutableTimeProvider(DppStoreTesting.Now);

        var upload = Assert.IsType<FileContentResult>(controller.Sample(UploadSamples.UploadFileName, clock));
        var export = Assert.IsType<FileContentResult>(controller.Sample(UploadSamples.ExportFileName, clock));

        Assert.Equal("ClusterAdminOrAdmin", Assert.Single(action.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Policy);
        Assert.Empty(action.GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
        Assert.IsType<NotFoundResult>(controller.Sample("other.json", clock));
        var entry = Assert.Single(Format.Read(JsonNode.Parse(upload.FileContents)!.AsObject()).Entries);
        Assert.Equal((UploadSamples.Serial, StatusEvent.Repaired), (entry.NamedAs, entry.Status!.What));
        var exported = Assert.Single(new BmsExportFormat().Read(JsonNode.Parse(export.FileContents)!.AsObject()).Entries);
        Assert.Equal((UploadSamples.Serial, 7, "74"), (exported.NamedAs, exported.Modules!.Count, exported.Values["stateOfChargeSoC"]!.ToJsonString()));
        var guide = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Guide.cshtml"));
        Assert.Contains("/help/samples/@BatteryPassWeb.Dpp.Uploads.UploadSamples.UploadFileName", guide, StringComparison.Ordinal);
        Assert.Contains("/help/samples/@BatteryPassWeb.Dpp.Uploads.UploadSamples.ExportFileName", guide, StringComparison.Ordinal);
        var api = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Index.cshtml"));
        Assert.Contains("@BatteryPassWeb.Dpp.Uploads.UploadService.StatusOnThePage", api, StringComparison.Ordinal); // decision (d)
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
