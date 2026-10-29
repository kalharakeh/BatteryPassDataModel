using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The batteries pages' services (step 2d-2): the list and its search, a new battery, and the battery page.</summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryPagesTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    [Fact]
    public void ABattery_ShouldTypeItsOwnValuesAndItsLiveValuesInTwoForms()
    {
        Assert.Equal([11, 13, 17, 19], RecordEditor.AttributesOf(TargetKind.Pack).Select(attribute => attribute.Number));
        Assert.Equal([63, 83, 87], RecordEditor.LiveAttributes.Select(attribute => attribute.Number));
        Assert.Equal([13], BatteryPages.ValueAttributes(published: true).Select(attribute => attribute.Number));
        Assert.Equal([11, 13, 17, 19], BatteryPages.ValueAttributes(published: false).Select(attribute => attribute.Number));
    }

    [Fact]
    public async Task TheList_ShouldNameEachBatterysModelBatchClusterAndPassport()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);

        var draft = Assert.Single((await pages.ListAsync(null)).Rows);
        Assert.Equal("VPC8C05-6788", draft.SerialNumber);
        Assert.Equal(rig.Seed.Sources.Model.Name, draft.ModelName);
        Assert.Equal($"2026 · {rig.Seed.Sources.Site.Name}", draft.BatchLabel);
        Assert.Equal("Customer North", draft.ClusterLabel);
        Assert.True(draft.IsDraft);

        await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);
        Assert.Equal(1, Assert.Single((await pages.ListAsync(null)).Rows).Active?.Number);
    }

    [Theory]
    [InlineData("VPC8C05-6788", true)] // serial number
    [InlineData("c05-67", true)] // part of one, any case
    [InlineData("urn:test:battery", true)] // battery ID (#6)
    [InlineData("TEST-DPP", true)] // passport ID (#5)
    [InlineData("customer north", true)] // cluster name
    [InlineData("cluster-test", true)] // cluster ID
    [InlineData("Customer South", false)]
    [InlineData("nothing like it", false)]
    public async Task TheSearch_ShouldFindPassportIdsBatteryIdsSerialNumbersAndClusters(string query, bool found)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = await BatteryTesting.Pages(rig).ListAsync(query);

        Assert.Equal(found ? 1 : 0, page.Rows.Count);
        Assert.Equal(1, page.Total);
        Assert.Equal(query.Trim(), page.Query);
    }

    [Fact]
    public async Task ANewBattery_ShouldBeADraftWithOrWithoutACluster()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);

        var (without, _) = await pages.CreateAsync(Who, rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, "", Form(("batterySerialNumber", "VPC9-1001")));
        var (with, _) = await pages.CreateAsync(Who, rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, BatteryTesting.South, Form(("batterySerialNumber", "VPC9-1002"), ("warrantyPeriod", "P8Y")));

        Assert.Null(without.Problem);
        Assert.Null(without.Pack!.ClusterId);
        Assert.Equal(BatteryTesting.South, with.Pack!.ClusterId);
        Assert.Equal("P8Y", (await rig.Store.GetAsync<BatteryPack>(with.Pack.Id))!.Values["warrantyPeriod"].Value.GetValue<string>());
        Assert.Equal(0, await rig.Store.GetLatestVersionNumberAsync(with.Pack.Id));
    }

    [Theory]
    [InlineData("", "operator-test", "cluster-test", "choose the batch")]
    [InlineData("batch-test-2026", "", "cluster-test", "choose the economic operator")]
    [InlineData("batch-test-2026", "operator-test", "cluster-gone", "cluster cluster-gone doesn't exist")]
    public async Task ANewBattery_ShouldNeedABatchAnOperatorAndAClusterThatExists(string batch, string economicOperator, string cluster, string problem)
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var (result, post) = await BatteryTesting.Pages(rig).CreateAsync(Who, batch, economicOperator, cluster, Form(("batterySerialNumber", "VPC9-1003")));

        Assert.Null(result.Pack);
        Assert.Contains(problem, result.Problem, StringComparison.Ordinal);
        Assert.Equal("VPC9-1003", post.Texts[BatteryTesting.Field("batterySerialNumber")]); // what was typed stays
        Assert.Single(await rig.Store.ListAsync<BatteryPack>());
    }

    [Fact]
    public async Task ADraftsPage_ShouldSayWhatIsMissingUnderTheRecordThatHoldsIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), ["thirdPartyServiceProvider", "temperatureInformation"]);
        await rig.Store.SetValuesAsync<BatteryModel>(rig.Seed.Sources.Model.Id, new Dictionary<string, AttributeValue>(), ["batteryMass"]);

        var page = await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId);

        Assert.NotNull(page);
        Assert.False(page.Publication.IsPublished);
        Assert.False(page.Publication.Ready);
        Assert.Collection(
            page.Publication.Missing,
            group => Assert.Equal(("This battery", "/admin/batteries/pack-VPC8C05-6788#values", 13), (group.Record, group.Href, group.Attributes.Single().Number)),
            group => Assert.Equal(("This battery's live values", "/admin/batteries/pack-VPC8C05-6788#live", 87), (group.Record, group.Href, group.Attributes.Single().Number)),
            group => Assert.Equal(($"Model {rig.Seed.Sources.Model.Name}", $"{ProductDataPages.FamilyHref(rig.Seed.Sources.Model.FamilyId, rig.Seed.Sources.Model.Id)}#editor", 21), (group.Record, group.Href, group.Attributes.Single().Number)));
        Assert.Equal(3, page.Publication.MissingCount);
        Assert.Equal([11, 13, 17, 19], page.Values.Sections.SelectMany(section => section.Fields).Select(field => field.Attribute.Number));
        Assert.Null(page.Removal.Blocked);
        Assert.Empty(page.Versions);
    }

    [Fact]
    public async Task ADraftWithEverythingNeededByFebruary2027_ShouldBeReadyAndTheChecksShouldPublishNothing()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<ProductionBatch>(rig.Seed.Sources.Batch.Id, new Dictionary<string, AttributeValue>(), ["carbonFootprintPerformanceClass"]);

        var page = await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId);

        Assert.True(page!.Publication.Ready);
        Assert.Equal(35, Assert.Single(page.Publication.NeededLater).Number); // required only after February 2027
        Assert.Empty(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task APublishedBatterysPage_ShouldLockItsFixedValuesAndListItsVersions()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId);

        Assert.True(page!.Publication.IsPublished);
        Assert.Equal([13], page.Values.Sections.SelectMany(section => section.Fields).Select(field => field.Attribute.Number));
        Assert.Equal(1, page.Values.PublishedPassports);
        var version = Assert.Single(page.Versions);
        Assert.True(version.SignatureHolds);
        Assert.NotNull(page.Removal.Blocked);
        Assert.Equal("Customer North", page.ClusterLabel);
    }

    [Fact]
    public async Task LiveValues_ShouldKeepTheirMeasurementTimeAndMakeNoNewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("stateOfChargeSoC")] = "55";
        var measured = new DateTime(2026, 10, 29, 6, 30, 0, DateTimeKind.Utc);

        var (_, save) = (await pages.SaveAsync(Who, rig.PackId, liveValues: true, PageTesting.Field(form), PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null), measured, confirmed: false))!.Value;

        Assert.Equal(EditorOutcome.Saved, save.Outcome);
        var soc = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"];
        Assert.Equal(55, soc.Value["percentageValue"]!.GetValue<int>());
        Assert.Equal(measured, soc.GeneratedAtUtc);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task ALiveFormLeftOpen_ShouldNotUndoANewerValueFromTheBatterySystem()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);
        var shown = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var stamp = PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, shown, null);
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, shown);
        await rig.Pipeline.SaveAsync(
            ChangeSender.Feed("bms@example.com", BatteryTesting.North),
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 61 }, "feed"));
        form[BatteryTesting.Field("temperatureInformation")] = "19";

        var (_, save) = (await pages.SaveAsync(Who, rig.PackId, liveValues: true, PageTesting.Field(form), stamp, null, confirmed: false))!.Value;

        Assert.Equal(1, save.SavedCount);
        var values = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        Assert.Equal(61, values["stateOfChargeSoC"].Value["percentageValue"]!.GetValue<int>());
        Assert.Equal(19, values["temperatureInformation"].Value["celsiusValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task TheCluster_ShouldMoveToARegisteredOneOrNoneWithoutANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);

        Assert.Equal("cluster cluster-gone doesn't exist", await pages.SetClusterAsync(Who, rig.PackId, "cluster-gone"));
        Assert.Equal("it is already there", await pages.SetClusterAsync(Who, rig.PackId, BatteryTesting.North));
        Assert.Null(await pages.SetClusterAsync(Who, rig.PackId, BatteryTesting.South));
        Assert.Equal(BatteryTesting.South, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Null(await pages.SetClusterAsync(Who, rig.PackId, ""));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Equal(2, events.Count(entry => entry["eventType"].AsString == BatteryPages.ClusterChangedEvent));
    }

    [Fact]
    public async Task ADraft_ShouldBeRemovedWithItsAccidentReports()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var upload = await PageTesting.Documents(rig).AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("report.pdf", DocumentTesting.Pdf()));
        Assert.Null(upload.Problem);

        var (problem, serial) = await BatteryTesting.Pages(rig).RemoveDraftAsync(Who, rig.PackId);

        Assert.Null(problem);
        Assert.Equal("VPC8C05-6788", serial);
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(rig.PackId));
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Pack, rig.PackId, DocumentService.Accidents));
    }

    [Fact]
    public async Task APublishedBattery_ShouldNotBeRemoved()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var (problem, _) = await BatteryTesting.Pages(rig).RemoveDraftAsync(Who, rig.PackId);

        Assert.Equal("its passport is published, so it stays with its versions", problem);
        Assert.NotNull(await rig.Store.GetAsync<BatteryPack>(rig.PackId));
    }

    [Fact]
    public async Task UploadedFiles_ShouldBeCheckedAgainstTheirSha256()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var documents = new DocumentStore(rig.Context);
        await PageTesting.Documents(rig).AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("report.pdf", DocumentTesting.Pdf("collision")));
        var tampered = new DppDocument
        {
            Id = "doc-tampered",
            OwnerKind = TargetKind.Pack,
            OwnerId = rig.PackId,
            Property = DocumentService.Accidents,
            FileName = "other.pdf",
            ContentType = "application/pdf",
            Length = 10,
            Sha256 = new string('0', 64),
            FileId = await documents.UploadFileAsync("other.pdf", DocumentTesting.Pdf("changed")),
            Title = "Changed later",
            UploadedAtUtc = DppStoreTesting.Now.UtcDateTime,
            UploadedBy = Who,
        };
        await documents.InsertAsync(tampered);

        var files = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Publication.Files;

        Assert.Equal(2, files.Count);
        Assert.True(files.Single(file => file.Document.FileName == "report.pdf").Unchanged);
        Assert.False(files.Single(file => file.Document.Id == "doc-tampered").Unchanged);
    }

    private static Func<string, string?> Form(params (string Property, string Text)[] fields) =>
        PageTesting.Field(fields.ToDictionary(field => BatteryTesting.Field(field.Property), field => field.Text, StringComparer.Ordinal));
}
