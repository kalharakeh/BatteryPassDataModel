using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The cluster admin's battery page (design 6.1 and 12, step 4a): only for a published battery of a cluster the
/// sender is admin of; live values and accident reports saved as the cluster admin; modules and versions shown.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterBatteryPagesTests(MongoFixture mongo)
{
    private const string Admin = "north-admin@example.com";

    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin(Admin, BatteryTesting.North);

    private static readonly ChangeSender SouthAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

    [Fact]
    public async Task TheBatterysClusterAdmin_ShouldOpenItsPage()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = await Pages(rig).BatteryAsync(NorthAdmin, rig.PackId);

        Assert.NotNull(page);
        Assert.Equal(["#63", "#83", "#87", "E1", "E2", "E3", "E4"], page.Live.Sections.SelectMany(section => section.Fields).Select(field => field.Attribute.Label));
        Assert.Equal($"{ClusterBatteryPages.BatteryHref(rig.PackId)}/live", page.Live.SaveAction);
        Assert.False(page.Live.ForTeam); // its fields say "the team", not "your team"
        Assert.All(page.Live.Sections.SelectMany(section => section.Fields), field => Assert.EndsWith("market surveillance and the team", field.SeenByOutsideTheTeam, StringComparison.Ordinal));
        Assert.Equal("Customer North", page.ClusterLabel);
        Assert.True(Assert.Single(page.Versions).SignatureHolds);
        Assert.Empty(page.AccidentReports);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotOpenIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Null(await Pages(rig).BatteryAsync(SouthAdmin, rig.PackId));
    }

    [Fact]
    public async Task ADraft_ShouldStayWithTheTeam()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        Assert.Null(await Pages(rig).BatteryAsync(NorthAdmin, rig.PackId));
    }

    [Fact]
    public async Task AnUnknownBattery_ShouldNotOpen()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Null(await Pages(rig).BatteryAsync(NorthAdmin, "pack-unknown"));
    }

    [Fact]
    public async Task LiveValues_ShouldBeSavedAsTheClusterAdminWithoutANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("stateOfChargeSoC")] = "42";
        var measured = new DateTime(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);

        var (_, save) = (await Pages(rig).SaveLiveAsync(NorthAdmin, rig.PackId, PageTesting.Field(form), PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null), measured))!.Value;

        Assert.Equal(EditorOutcome.Saved, save.Outcome);
        var soc = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"];
        Assert.Equal(42, soc.Value["percentageValue"]!.GetValue<int>());
        Assert.Equal(Admin, soc.Who);
        Assert.Equal(measured, soc.GeneratedAtUtc);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
        var entry = await LastChangeAsync(rig);
        Assert.Equal(Admin, entry["actor"].AsString);
        Assert.Equal(nameof(SenderKind.ClusterAdmin), entry["actorRole"].AsString);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotSaveLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("stateOfChargeSoC")] = "42";

        Assert.Null(await Pages(rig).SaveLiveAsync(SouthAdmin, rig.PackId, PageTesting.Field(form), PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null), null));
        Assert.Equal(stored["stateOfChargeSoC"].Value!.ToJsonString(), (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!.ToJsonString());
    }

    [Fact]
    public async Task AnAccidentReport_ShouldBeAddedByTheBatterysClusterAdmin()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = Pages(rig);

        var result = await pages.AddAccidentReportAsync(NorthAdmin, rig.PackId, new DocumentUpload("fall.pdf", DocumentTesting.Pdf("fall"), "Dropped in the yard"));

        Assert.NotNull(result);
        Assert.Null(result.Problem);
        var report = Assert.Single((await pages.BatteryAsync(NorthAdmin, rig.PackId))!.AccidentReports);
        Assert.Equal("Dropped in the yard", report.Title);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldNotAddAnAccidentReport()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Null(await Pages(rig).AddAccidentReportAsync(SouthAdmin, rig.PackId, new DocumentUpload("fall.pdf", DocumentTesting.Pdf())));
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Pack, rig.PackId, DocumentService.Accidents));
    }

    [Fact]
    public async Task ABatteryMovedAway_ShouldCloseAtOnce()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetPackClusterAsync(rig.PackId, BatteryTesting.South);

        Assert.Null(await Pages(rig).BatteryAsync(NorthAdmin, rig.PackId));
        Assert.NotNull(await Pages(rig).BatteryAsync(SouthAdmin, rig.PackId));
    }

    internal static ClusterBatteryPages Pages(PublicationRig rig) => new(
        rig.Store,
        PageTesting.Editor(rig),
        PageTesting.Documents(rig),
        new DocumentStore(rig.Context),
        new ClusterRepository(rig.Context));

    private static async Task<BsonDocument> LastChangeAsync(PublicationRig rig) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", SavePipeline.AuditEventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
