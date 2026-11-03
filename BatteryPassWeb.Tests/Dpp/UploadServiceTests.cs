using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Uploads in the native format (design 8.1 and 8.2, decision 8, step 5a): each battery found as the API finds it, its
/// values through the one save pipeline, a battery that can't be changed refused without blocking the others, and every
/// upload that isn't a preview logged.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class UploadServiceTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthFeed = ChangeSender.Feed("north-feed", BatteryTesting.North);

    [Fact]
    public async Task ATeamUpload_ShouldSaveEachValueThroughThePipelineAndBeLogged()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, PublicationTesting.Team, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 55, "temperatureInformation": 21, "batteryMass": 500, "colour": "red" }""", "2026-10-29T07:00:00Z")));

        Assert.Null(report.Problem);
        var battery = Assert.Single(report.Batteries);
        Assert.Equal((UploadService.Saved, rig.PackId, PassportPagesTests.Identifier(rig)), (battery.Outcome, battery.PackId, battery.BatteryId));
        Assert.Equal([KeyOutcome.Saved, KeyOutcome.Saved, KeyOutcome.Rejected, KeyOutcome.Ignored], battery.Values.Select(value => value.Outcome));
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"];
        Assert.Equal((55, "upload", new DateTime(2026, 10, 29, 7, 0, 0, DateTimeKind.Utc)), (stored.Value!["percentageValue"]!.GetValue<int>(), stored.Source, stored.GeneratedAtUtc));

        var logged = Assert.Single(await rig.Store.ListUploadsAsync(10));
        Assert.Equal(report.UploadId, logged.Id);
        Assert.Equal(("team@example.com", "Team", "page", "batteries.json", NativeUploadFormat.Format), (logged.Who, logged.SentAs, logged.Channel, logged.FileName, logged.Format));
        var loggedBattery = Assert.Single(logged.Batteries);
        Assert.Equal((1, Serial(rig), UploadService.Saved), (loggedBattery.Entry, loggedBattery.NamedAs, loggedBattery.Outcome));
        Assert.Equal(["Saved", "Saved", "Rejected", "Ignored"], loggedBattery.Values.Select(value => value.Outcome));
        Assert.Equal("Upload of 1 battery: 1 saved, 0 with nothing saved, 0 refused", (await LastAsync(rig, UploadService.UploadedEvent))["message"].AsString);
    }

    [Fact]
    public async Task TheAuditLog_ShouldSayWhoUploadedHowManyBatteries()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await Run(rig, PublicationTesting.Team, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 55 }"""), Entry("no-such-battery", """{ "stateOfChargeSoC": 1 }""")));

        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var lines = await new AuditLines(rig.Store, new DocumentStore(rig.Context), AttributeFormsTests.Forms, rig.Context).DescribeAsync(events);

        var line = Assert.Single(lines, line => line.EventType == UploadService.UploadedEvent);
        Assert.Equal("team@example.com uploaded values for 2 batteries", line.Text);
        Assert.Equal("1 saved, 0 with nothing saved, 1 refused", line.Change);
    }

    [Fact]
    public async Task ABatterySystem_ShouldSaveOnlyLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, NorthFeed, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 40, "manufacturingDate": "2026-01", "batteryStatus": "waste" }""")));

        var battery = Assert.Single(report.Batteries);
        Assert.Equal(UploadService.Saved, battery.Outcome);
        Assert.Equal([KeyOutcome.Saved, KeyOutcome.Rejected, KeyOutcome.Rejected], battery.Values.Select(value => value.Outcome));
        Assert.Equal("feed", (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Source);
        Assert.Equal("BatterySystemFeed", Assert.Single(await rig.Store.ListUploadsAsync(10)).SentAs);
    }

    [Fact]
    public async Task TheTeam_ShouldSetABatterysOwnValuesWhileItIsADraft()
    {
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var published = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var onDraft = await Run(draft, PublicationTesting.Team, File(Entry(Serial(draft), """{ "manufacturingDate": "2026-08" }""")));
        var onPublished = await Run(published, PublicationTesting.Team, File(Entry(Serial(published), """{ "manufacturingDate": "2026-08" }""")));

        Assert.Equal(UploadService.Saved, Assert.Single(onDraft.Batteries).Outcome);
        Assert.Equal("2026-08", (await draft.Store.GetAsync<BatteryPack>(draft.PackId))!.Values["manufacturingDate"].Value!.GetValue<string>());
        var refused = Assert.Single(onPublished.Batteries);
        Assert.Equal(UploadService.NothingSaved, refused.Outcome);
        Assert.Equal(SavePipeline.FixedOncePublished, Assert.Single(refused.Values).Message);
    }

    [Fact]
    public async Task ABatteryThatCannotBeChanged_ShouldBeRefusedWithoutBlockingTheOthers()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var southAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

        var report = await Run(rig, PublicationTesting.Team, File(
            Entry("no-such-battery", """{ "stateOfChargeSoC": 10 }"""),
            """{ "batterySerialNumber": "x" }""",
            Entry(Serial(rig), """{ "stateOfChargeSoC": 11 }""", "2026-10-29T09:00:00Z"),
            """{ "values": { "stateOfChargeSoC": 12 } }""",
            Entry(PassportPagesTests.Identifier(rig), """{ "stateOfChargeSoC": 13 }""")));
        var notYours = await Run(rig, southAdmin, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 14 }""")));

        Assert.Equal(
            [UploadService.NotFound, "the entry has no values (\"values\": {...})", UploadService.InTheFuture, "name the battery by uniqueProductIdentifier or batterySerialNumber", null],
            report.Batteries.Select(battery => battery.Problem));
        Assert.Equal([1, 2, 3, 4, 5], report.Batteries.Select(battery => battery.Entry));
        Assert.Equal(UploadService.Saved, report.Batteries[^1].Outcome);
        Assert.Equal(UploadService.NotYours, Assert.Single(notYours.Batteries).Problem);
        Assert.Equal(13, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task ABatteryNamedTwice_ShouldBeRefusedBothTimes()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, PublicationTesting.Team, File(
            Entry(Serial(rig), """{ "stateOfChargeSoC": 20 }"""),
            Entry(PassportPagesTests.Identifier(rig), """{ "stateOfChargeSoC": 21 }""")));

        Assert.All(report.Batteries, battery => Assert.Equal((UploadService.Refused, UploadService.NamedTwice), (battery.Outcome, battery.Problem)));
        Assert.False(report.AnySaved);
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task APassportThatIsNoLongerActive_ShouldRefuseTheUpload()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures"));

        var report = await Run(rig, PublicationTesting.Team, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 30 }""")));

        var battery = Assert.Single(report.Batteries);
        Assert.Equal((UploadService.Refused, SavePipeline.NoLongerChanges(DppStatuses.Invalid)), (battery.Outcome, battery.Problem));
    }

    [Fact]
    public async Task APreview_ShouldSaveAndLogNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, NorthFeed, File(Entry(Serial(rig), """{ "stateOfChargeSoC": 12 }""")), preview: true);

        Assert.True(report.Preview);
        Assert.True(report.AnySaved);
        Assert.Null(report.UploadId);
        Assert.Empty(await rig.Store.ListUploadsAsync(10));
        Assert.Equal(76, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!["percentageValue"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("[]", UploadService.NotAnUpload)]
    [InlineData("""{ "batteries": [] }""", UploadService.NotAnUpload)]
    [InlineData("""{ "format": "dpp-v9" }""", "the format \"dpp-v9\" isn't one this app reads; use \"dpp-v3-upload/1\"")]
    [InlineData("""{ "format": 5 }""", "the format 5 isn't one this app reads; use \"dpp-v3-upload/1\"")]
    [InlineData("""{ "format": "dpp-v3-upload/1", "batteries": [] }""", "the file lists no batteries (\"batteries\": [...])")]
    public async Task AFileThatIsNotAnUpload_ShouldSayWhyAndLogNothing(string file, string problem)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var report = await Run(rig, PublicationTesting.Team, file);

        Assert.Equal(problem, report.Problem);
        Assert.Empty(report.Batteries);
        Assert.Empty(await rig.Store.ListUploadsAsync(10));
    }

    [Fact]
    public async Task TooManyBatteries_ShouldBeRefusedAsAWhole()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var entries = Enumerable.Range(0, UploadService.MaxBatteries + 1).Select(index => Entry($"B-{index}", """{ "stateOfChargeSoC": 1 }""")).ToArray();

        var report = await Run(rig, PublicationTesting.Team, File(entries));

        Assert.Equal($"the file lists {UploadService.MaxBatteries + 1} batteries; one upload takes at most {UploadService.MaxBatteries}", report.Problem);
    }

    [Fact]
    public void AFilesBytes_ShouldBeReadWithOrWithoutAByteOrderMark()
    {
        var json = Encoding.UTF8.GetBytes("""{ "format": "dpp-v3-upload/1" }""");

        Assert.Equal(NativeUploadFormat.Format, UploadService.Parse([0xEF, 0xBB, 0xBF, .. json])!["format"]!.GetValue<string>());
        Assert.Equal(NativeUploadFormat.Format, UploadService.Parse(json)!["format"]!.GetValue<string>());
        Assert.Null(UploadService.Parse("not json"u8));
    }

    internal static UploadService Service(PublicationRig rig) =>
        new(rig.Store, rig.Pipeline, BatteryApiTests.Api(rig), new ApplicationAuditService(rig.Context), rig.Clock, [new NativeUploadFormat(), new BmsExportFormat()]);

    internal static Task<UploadReport> Run(PublicationRig rig, ChangeSender sender, string file, bool preview = false) =>
        Service(rig).RunAsync(sender, JsonNode.Parse(file), "page", "batteries.json", preview);

    internal static string File(params string[] entries) => $$"""{ "format": "dpp-v3-upload/1", "batteries": [ {{string.Join(", ", entries)}} ] }""";

    internal static string Entry(string serialOrId, string values, string? measuredAt = null) =>
        $$"""{ "batterySerialNumber": "{{serialOrId}}", {{(measuredAt is null ? string.Empty : $"\"measuredAt\": \"{measuredAt}\", ")}}"values": {{values}} }""";

    internal static string Serial(PublicationRig rig) => rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value!.GetValue<string>();

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
