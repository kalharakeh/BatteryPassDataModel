using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R08 (the manager's decisions 72 and 73, round two): the team removes the passport of a battery marked for
/// deletion, and its earlier passports, after typing its ID. Everything about it goes; one audit entry and a marker per
/// passport stay; every address answers 410 with the plain sentence, also while a removal that stopped half way waits to be
/// finished; the IDs are never used again; nothing publishes for it afterwards.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportRemovalTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    [Fact]
    public async Task Removing_ShouldNeedTheTeamAMarkForDeletionAndTheTypedId()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var removal = Removal(rig);
        var id = Identifier(rig);

        Assert.Equal(PassportRemoval.NotMarked, await removal.RemoveAsync(Team, rig.PackId, id)); // still active
        await MarkForDeletionAsync(rig);
        Assert.Equal(PassportRemoval.OnlyTheTeam, await removal.RemoveAsync(ChangeSender.ClusterAdmin("north@example.com", BatteryTesting.North), rig.PackId, id));
        Assert.Equal(PassportRemoval.TypeTheId, await removal.RemoveAsync(Team, rig.PackId, null));
        Assert.Equal(PassportRemoval.TypeTheId, await removal.RemoveAsync(Team, rig.PackId, UploadServiceTests.Serial(rig))); // the serial isn't the ID
        Assert.Equal(PassportRemoval.TypeTheId, await removal.RemoveAsync(Team, rig.PackId, id + "x"));
        Assert.NotNull(await rig.Store.GetAsync<BatteryPack>(rig.PackId));

        Assert.Null(await removal.RemoveAsync(Team, rig.PackId, $"  {IdentifierForms.Short(id).ToLowerInvariant()} "));
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(rig.PackId));
    }

    [Fact]
    public async Task Removing_ShouldDeleteEverythingAboutTheBattery_AndLeaveOneAuditEntryAndTheMarker()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var report = await DocumentServiceTests.Service(rig).AddAccidentReportAsync(Team, rig.PackId, new DocumentUpload("accident.pdf", DocumentTesting.Pdf()));
        Assert.Null(report.Problem);
        Assert.True((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(40) }, "form"))).Saved);
        var alone = (await UploadServiceTests.Run(rig, Team, UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 41 }""")))).UploadId!;
        var shared = (await UploadServiceTests.Run(rig, Team, UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), """{ "stateOfChargeSoC": 42 }"""), UploadServiceTests.Entry("no-such-battery", """{ "stateOfChargeSoC": 1 }""")))).UploadId!;
        await MarkForDeletionAsync(rig);
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow); // a marker, which goes with it
        var passportIds = (await rig.Store.ListVersionsAsync(rig.PackId)).Select(version => version.DigitalProductPassportId).ToArray();
        var audit = rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents");
        Assert.True(await audit.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("entityId", rig.PackId)) > 3);

        Assert.Null(await Removal(rig).RemoveAsync(Team, rig.PackId, Identifier(rig)));

        var database = rig.Context.Database!;
        Assert.Equal(0, await database.GetCollection<BsonDocument>(DppStore.Packs).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)));
        Assert.Equal(0, await database.GetCollection<BsonDocument>(DppStore.Versions).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)));
        Assert.Equal(0, await database.GetCollection<BsonDocument>(DocumentStore.Records).CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));
        Assert.Equal(0, await database.GetCollection<BsonDocument>($"{DocumentStore.Bucket}.files").CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));
        var uploads = database.GetCollection<BsonDocument>(DppStore.Uploads);
        Assert.Equal(0, await uploads.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", alone))); // its only line went, so it went
        var kept = await uploads.Find(Builders<BsonDocument>.Filter.Eq("_id", shared)).SingleAsync();
        Assert.Equal(["no-such-battery"], kept["Batteries"].AsBsonArray.Select(line => line["NamedAs"].AsString)); // the other line stays

        // What the app keeps about the battery: the marker and ONE audit entry (decision 72).
        var rows = await audit.Find(Builders<BsonDocument>.Filter.Eq("entityId", rig.PackId) | Builders<BsonDocument>.Filter.Eq("entityId", alone) | Builders<BsonDocument>.Filter.Eq("batteryId", Identifier(rig))).ToListAsync();
        var entry = Assert.Single(rows);
        Assert.Equal(PassportRemoval.RemovedEvent, entry["eventType"].AsString);
        Assert.Equal(("team@example.com", Identifier(rig)), (entry["actor"].AsString, entry["metadata"]["batteryId"].AsString));
        Assert.Equal((2, 1, 2), (entry["metadata"]["versions"].AsInt32, entry["metadata"]["documents"].AsInt32, entry["metadata"]["uploadLines"].AsInt32));

        var marker = (await rig.Store.FindRemovedAsync(Identifier(rig)))!;
        Assert.Equal(rig.PackId, marker.Id);
        Assert.All(passportIds, passportId => Assert.Contains(passportId, marker.Ids));
        Assert.NotNull(await rig.Store.FindRemovedAsync(IdentifierForms.Short(Identifier(rig))));
        Assert.NotNull(await rig.Store.FindRemovedAsync(UploadServiceTests.Serial(rig))); // no other battery has its serial number
        Assert.Empty((await BatteryTesting.Pages(rig).TeamListAsync(null)).Waiting); // the list never names it
    }

    [Fact]
    public async Task EveryAddress_ShouldAnswer410WithThePlainSentence()
    {
        var rig = await RemovedRigAsync();
        var shortId = IdentifierForms.Short(Identifier(rig));
        var services = new ServiceCollection().AddSingleton(Removal(rig)).BuildServiceProvider();

        var summary = Assert.IsType<ViewResult>(await Passport(rig, services).Summary(shortId, CancellationToken.None));
        Assert.Equal((410, "~/Views/Dpp/Removed.cshtml"), (summary.StatusCode, summary.ViewName));
        var detail = Assert.IsType<ViewResult>(await Passport(rig, services, signedIn: true).Detail(shortId, CancellationToken.None));
        Assert.Equal(410, detail.StatusCode);
        var accidents = Assert.IsType<ViewResult>(await Accidents(rig, services).Accidents(rig.PackId, CancellationToken.None));
        Assert.Equal(410, accidents.StatusCode);

        var data = Passport(rig, services);
        AssertGoneJson(await data.SummaryJson(shortId, CancellationToken.None));
        Assert.Equal("no-cache, no-store", data.Response.Headers.CacheControl.ToString());
        AssertGoneJson(await Passport(rig, services).SummaryData(Uri.EscapeDataString(Identifier(rig)), CancellationToken.None));
        // A version's passport ID (the test scheme's IDs aren't addresses; the running app checks /v1 and /v1.json).
        AssertGoneJson(await Passport(rig, services).SummaryJson(Uri.EscapeDataString(PassportIdOf(rig)), CancellationToken.None));

        Assert.Equal(410, Assert.IsType<ViewResult>(await Passport(rig, services).Summary(UploadServiceTests.Serial(rig), CancellationToken.None)).StatusCode); // its serial number

        var api = DppApiControllerTests.PassportsController(rig);
        api.ControllerContext.HttpContext.RequestServices = services;
        AssertGoneJson(await api.Detail(shortId, CancellationToken.None));
        AssertGoneJson(await api.Verify(shortId, CancellationToken.None));
        Assert.Equal(["Removed.cshtml"], new[] { Path.GetFileName(DppTestFiles.RepoFile("web", "Views", "Dpp", "Removed.cshtml")) });
        Assert.Contains("@PassportRemoval.Gone", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Dpp", "Removed.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARemovalThatStoppedHalfWay_ShouldAnswer410_AndTheCheckShouldFinishIt()
    {
        // It stopped right after the marker: the battery, its versions and documents are all still there.
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await MarkForDeletionAsync(rig);
        await HalfRemovedAsync(rig);
        var services = new ServiceCollection().AddSingleton(Removal(rig)).BuildServiceProvider();
        var shortId = IdentifierForms.Short(Identifier(rig));

        Assert.Equal(410, Assert.IsType<ViewResult>(await Passport(rig, services).Summary(shortId, CancellationToken.None)).StatusCode);
        AssertGoneJson(await Passport(rig, services).SummaryJson(shortId, CancellationToken.None));
        var api = DppApiControllerTests.PassportsController(rig);
        api.ControllerContext.HttpContext.RequestServices = services;
        AssertGoneJson(await api.Verify(shortId, CancellationToken.None)); // not 200 from the version that is still there
        AssertGoneJson(await api.Detail(shortId, CancellationToken.None));
        Assert.True((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.RemovalStarted); // the page offers "Finish removing"
        Assert.False((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(40) }, "form"))).Saved);

        var logger = new HookLogger();
        var check = Check(rig, logger);
        var result = await check.ReconcileAsync("check-test");

        Assert.Equal((1, 0), (result.RemovalsFinished, result.Failed));
        Assert.Contains("Finished removing 1 passport", result.Words, StringComparison.Ordinal);
        Assert.Contains(logger.Information, line => line.Contains("finished removing", StringComparison.Ordinal));
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(rig.PackId));
        Assert.False(await rig.Store.IsAnythingLeftAsync(rig.PackId));
        Assert.Equal(0, (await check.ReconcileAsync("check-test")).RemovalsFinished); // nothing left the second time
    }

    [Fact]
    public async Task FinishRemoving_ShouldFinishARemovalThatStoppedHalfWay()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await MarkForDeletionAsync(rig);
        await HalfRemovedAsync(rig);

        Assert.Null(await Removal(rig).RemoveAsync(Team, rig.PackId, null)); // "Finish removing": confirmed when it started

        Assert.False(await rig.Store.IsAnythingLeftAsync(rig.PackId));
        Assert.Contains("data-removal-started", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARemovalAfterTheChecksReads_ShouldBePassedBy()
    {
        // The battery is active when the check reads it, so the check holds its active version; then it is removed.
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryModel>("model-test", new Dictionary<string, BatteryPassWeb.Dpp.Values.AttributeValue>
        {
            ["batteryMass"] = new() { Value = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 }, GeneratedAtUtc = DateTime.UtcNow, Source = "form" },
        }); // a difference the check would mark
        var logger = new HookLogger();
        var check = Check(rig, logger);
        logger.OnDebug = message =>
        {
            if (message.Contains("read the active versions", StringComparison.Ordinal))
            {
                MarkForDeletionAsync(rig).GetAwaiter().GetResult();
                Assert.Null(Removal(rig).RemoveAsync(Team, rig.PackId, Identifier(rig)).GetAwaiter().GetResult());
            }
        };

        var result = await check.ReconcileAsync("check-test");

        Assert.Equal(0, result.Failed);
        Assert.Empty(logger.Errors);
        Assert.False(await rig.Store.IsAnythingLeftAsync(rig.PackId)); // nothing marked, archived or revived for it
    }

    [Fact]
    public async Task NothingShouldPublishForARemovedBattery_AndItsIdShouldNotBeUsedAgain()
    {
        var rig = await RemovedRigAsync();

        Assert.Null(await rig.Publication.PublishWaitingAsync(Team, rig.PackId)); // "Publish the waiting changes"
        Assert.NotNull((await rig.Publication.PublishAsync(Team, rig.PackId)).Problem);
        Assert.False((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(40) }, "form"))).Found);
        Assert.Equal(WriteRefusal.Gone, (await BatteryApiTests.Api(rig).FindAsync(ChangeSender.Feed("feed", BatteryTesting.North), Identifier(rig))).Refusal); // a live value from the feed: gone (410), round four
        Assert.Equal(0, await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)));

        var creation = new PackCreationService(rig.Store, TestDemo.For(rig), rig.Pipeline, new SameIdScheme(Identifier(rig)), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock);
        var again = await creation.CreateAsync(Team, new NewPack(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "VPC9-0201" }));
        Assert.Equal(PackCreationService.RemovedId, again.Problem);
    }

    [Fact]
    public async Task TheTeamsPage_ShouldOfferTheRemoval_OnlyWhenMarkedForDeletion_AndAskForTheId()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.Null((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.RemovePassport);
        await MarkForDeletionAsync(rig);
        var page = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        Assert.Equal(($"/admin/batteries/{rig.PackId}/remove-passport", rig.PackId), (page.RemovePassportAction, page.RemovePassport!.Newest.Id));
        Assert.Empty(page.RemovePassport.EarlierIds);

        var wrong = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());
        Assert.Equal($"/admin/batteries/{rig.PackId}", Assert.IsType<RedirectResult>(await wrong.RemovePassport(rig.PackId, "not the ID", Removal(rig), CancellationToken.None)).Url);
        Assert.NotNull(await rig.Store.GetAsync<BatteryPack>(rig.PackId));

        var right = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());
        Assert.Equal("/admin/batteries", Assert.IsType<RedirectResult>(await right.RemovePassport(rig.PackId, Identifier(rig), Removal(rig), CancellationToken.None)).Url);
        Assert.Contains("is removed", right.TempData[ProductDataController.StatusKey] as string, StringComparison.Ordinal);
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(rig.PackId));
        var dialog = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_RemovePassportDialog.cshtml"));
        Assert.Contains("It can't be undone.", dialog, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmId\"", dialog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterARemovedDemoBattery_ResetDemoDataShouldStillWork()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var waste = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-CORE-0003")).Single();
        if ((await rig.Store.GetLatestVersionAsync(waste.Id))!.DppStatus != DppStatuses.MarkedForDeletion)
        {
            Assert.Null(await rig.Services.GetRequiredService<PassportMarks>().MarkForDeletionAsync(ChangeSender.Team(DemoRig.Who), waste.Id, "recycled"));
        }

        var batteryId = waste.Values["uniqueProductIdentifier"].Value!.GetValue<string>();
        Assert.Null(await rig.Services.GetRequiredService<PassportRemoval>().RemoveAsync(ChangeSender.Team(DemoRig.Who), waste.Id, batteryId));

        await rig.Demo.ClearAsync(DemoRig.Who);
        var seeded = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Empty(seeded.Notes);
        Assert.Null(await rig.Store.FindRemovedAsync(batteryId));
        Assert.Single(await rig.Store.FindPacksBySerialNumberAsync("DEMO-CORE-0003"));
    }

    [Fact]
    public async Task ABatteryPlacedOnTheMarketAgain_ShouldGoWithAllItsEarlierPassports()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var chain = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C13M-0001")).ToArray();
        Assert.Equal(3, chain.Length);
        var newest = chain.Single(pack => !chain.Any(other => other.SuccessorOf == pack.Id));
        var team = ChangeSender.Team(DemoRig.Who);
        var sources = (await rig.Store.LoadSourcesAsync(newest.Id))!;
        var current = StatusChangeService.RestatedAttributes(sources.Model).ToDictionary(
            attribute => attribute.Property,
            attribute => (sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values[attribute.Property]).Value?.DeepClone(),
            StringComparer.Ordinal);
        Assert.Null((await rig.Services.GetRequiredService<StatusChangeService>().ChangeAsync(team, newest.Id, new StatusChange(StatusEvent.Waste, current))).Problem);
        Assert.Null(await rig.Services.GetRequiredService<PassportMarks>().MarkForDeletionAsync(team, newest.Id, "exported"));
        var removal = rig.Services.GetRequiredService<PassportRemoval>();
        var plan = (await removal.PlanAsync(newest.Id))!;
        Assert.Equal(2, plan.EarlierIds.Count); // the window lists them

        Assert.Null(await removal.RemoveAsync(team, newest.Id, PassportRemoval.BatteryIdOf(newest)));

        foreach (var pack in chain)
        {
            Assert.False(await rig.Store.IsAnythingLeftAsync(pack.Id));
            Assert.NotNull(await rig.Store.FindRemovedAsync(PassportRemoval.BatteryIdOf(pack))); // each ID answers 410
        }

        var entry = await rig.Database.GetCollection<BsonDocument>("applicationAuditEvents").Find(Builders<BsonDocument>.Filter.Eq("eventType", PassportRemoval.RemovedEvent)).SingleAsync();
        Assert.Equal(plan.EarlierIds, entry["metadata"]["earlierBatteryIds"].AsBsonArray.Select(id => id.AsString)); // the one entry names them all
        Assert.True(await rig.Demo.HasDemoDataAsync()); // the demo catalogue is still there
    }

    [Fact]
    public async Task TheExternalApi_ShouldAnswer410ForARemovedBattery_ToReadsAndWrites()
    {
        var rig = await RemovedRigAsync();
        var repository = DppApiControllerTests.Repository(rig);
        var global = (await repository.CreateTokenAsync("global", ExternalTokenAccessMode.ReadWrite, [], false, true, "test")).PlainToken;
        var services = new ServiceCollection().AddSingleton(Removal(rig)).BuildServiceProvider();
        var id = Uri.EscapeDataString(IdentifierForms.Short(Identifier(rig)));
        DppApiController Api()
        {
            var controller = DppApiControllerTests.Controller(rig, global);
            controller.ControllerContext.HttpContext.RequestServices = services;
            return controller;
        }

        foreach (var answer in new[]
        {
            await Api().Battery(id, CancellationToken.None),
            await Api().WriteLiveValues(id, System.Text.Json.JsonDocument.Parse("""{ "values": { "stateOfChargeSoC": 5 } }""").RootElement.Clone(), false, CancellationToken.None),
        })
        {
            var gone = Assert.IsAssignableFrom<ObjectResult>(answer);
            Assert.Equal(StatusCodes.Status410Gone, gone.StatusCode);
            Assert.Contains(PassportRemoval.Gone, System.Text.Json.JsonSerializer.Serialize(gone.Value, new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheIdsPool_ShouldNeverGiveARemovedIdAgain()
    {
        var context = mongo.NewContext();
        var store = new DppStore(context);
        await store.EnsureIndexesAsync();
        var pool = new BatteryIdPool(store, new TestIdentifierScheme(), new ApplicationAuditService(context), TimeProvider.System);
        await store.AddRemovedAsync(new RemovedPassport { Id = "pack-gone", BatteryId = "urn:x:gone", Ids = ["pack-gone", "urn:x:gone", "VPC-77001"], RemovedAtUtc = DateTime.UtcNow, RemovedBy = "team@example.com" });

        var release = await pool.ReleaseAsync("team@example.com", new ReleaseRequest("PO 1", null, [], "VPC-77001\nVPC-77002"));

        Assert.Equal(0, release.Released);
        Assert.Equal(("VPC-77001", BatteryIdPool.RemovedId), (Assert.Single(release.Problems).What, release.Problems[0].Reason));
    }

    [Fact]
    public async Task Creating_ShouldAskTheMarkerBeforePickingAReleasedId()
    {
        var rig = await RemovedRigAsync();
        var pool = new BatteryIdPool(rig.Store, new TestIdentifierScheme(), new ApplicationAuditService(rig.Context), rig.Clock);
        await rig.Store.ReleasedIds.InsertOneAsync(new ReleasedBatteryId { Id = "VPC77003", SerialNumber = "VPC-77003", Identifier = Identifier(rig), Status = ReleasedIdStatuses.Available, Reference = "PO 2", ReleaseId = "release-x", ReleasedAtUtc = DateTime.UtcNow, ReleasedBy = "team@example.com" });
        var creation = new PackCreationService(rig.Store, TestDemo.For(rig), rig.Pipeline, new TestIdentifierScheme(), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock, pool);

        var made = await creation.CreateAsync(Team, new NewPack(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "VPC-77003" }, FromPool: true));

        Assert.Equal(PackCreationService.RemovedId, made.Problem);
        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("VPC-77003"))!.Status); // nothing left "used" for a pack that never existed
    }

    [Fact]
    public async Task ASeed_ShouldClearDemoMarkersFirst()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Store.AddRemovedAsync(new RemovedPassport { Id = "pack-old-demo", BatteryId = "x", Ids = ["DEMO-CORE-0001"], RemovedAtUtc = DateTime.UtcNow, RemovedBy = "team@example.com", IsDemo = true });
        await rig.Store.AddRemovedAsync(new RemovedPassport { Id = "pack-real", BatteryId = "y", Ids = ["REAL-1"], RemovedAtUtc = DateTime.UtcNow, RemovedBy = "team@example.com" });

        var seeded = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Empty(seeded.Notes);
        Assert.Null(await rig.Store.FindRemovedAsync("DEMO-CORE-0001"));
        Assert.NotNull(await rig.Store.FindRemovedAsync("REAL-1")); // a real one stays
    }

    private async Task<PublicationRig> RemovedRigAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await MarkForDeletionAsync(rig);
        Assert.Null(await Removal(rig).RemoveAsync(Team, rig.PackId, Identifier(rig)));
        return rig;
    }

    private static async Task MarkForDeletionAsync(PublicationRig rig)
    {
        await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
        Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(Team, rig.PackId, "recycled"));
    }

    private static string Identifier(PublicationRig rig) => rig.Seed.Sources.Pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>();

    private static string PassportIdOf(PublicationRig rig) => rig.Context.Database!.GetCollection<BsonDocument>(DppStore.RemovedPassports)
        .Find(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)).Single()["Ids"].AsBsonArray.Select(id => id.AsString).First(id => id.Contains("DPP", StringComparison.Ordinal));

    /// <summary>A removal that stopped right after its marker: nothing deleted yet.</summary>
    private static async Task HalfRemovedAsync(PublicationRig rig)
    {
        var ids = new List<string> { rig.PackId, Identifier(rig), IdentifierForms.Short(Identifier(rig)) };
        await rig.Store.AddRemovedAsync(new RemovedPassport { Id = rig.PackId, BatteryId = Identifier(rig), Ids = ids, RemovedAtUtc = DateTime.UtcNow, RemovedBy = "team@example.com", Newest = rig.PackId });
    }

    private static PublicationService Check(PublicationRig rig, ILogger<PublicationService> logger) =>
        new(rig.Store, TestDemo.For(rig), new PublicationChecks(DppStoreTesting.Schemas), new PassportSigner(SigningTesting.Keys), new TestIdentifierScheme(), new ApplicationAuditService(rig.Context), rig.Clock, logger: logger, removal: Removal(rig));

    internal static PassportRemoval Removal(PublicationRig rig) =>
        new(rig.Store, TestDemo.For(rig), new DocumentStore(rig.Context), new ApplicationAuditService(rig.Context), rig.Clock);

    private static PassportController Passport(PublicationRig rig, IServiceProvider services, bool signedIn = false)
    {
        var controller = PublicPassportDataTests.Controller(rig);
        controller.ControllerContext.HttpContext.RequestServices = services;
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(controller.ControllerContext.HttpContext, new NoTempData());
        if (signedIn)
        {
            controller.ControllerContext.HttpContext.User = TestAccounts.SignedIn(rig.Context, "team@example.com", AccessControlService.RoleAdmin);
        }

        return controller;
    }

    private static DppController Accidents(PublicationRig rig, IServiceProvider services)
    {
        var documents = PageTesting.Documents(rig);
        var http = new DefaultHttpContext { User = TestAccounts.SignedIn(rig.Context, "team@example.com", AccessControlService.RoleAdmin), RequestServices = services };
        return new DppController(new DppReaders(new ClusterRepository(rig.Context)), documents, new BatteryPassWeb.Dpp.Pages.ReaderPages(rig.Store, documents))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http, new NoTempData()),
        };
    }

    private static void AssertGoneJson(IActionResult result)
    {
        var gone = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status410Gone, gone.StatusCode);
        Assert.Equal(PassportRemoval.Gone, System.Text.Json.JsonSerializer.SerializeToElement(gone.Value).GetProperty("error").GetString());
    }

    private sealed class NoTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    /// <summary>A scheme that gives every new battery this one ID.</summary>
    private sealed class SameIdScheme(string identifier) : IIdentifierScheme
    {
        public Task<NewIdentifier> NewPackIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken = default) => Task.FromResult(new NewIdentifier(identifier, null));

        public NewIdentifier ItemFor(string serialNumber, bool demo) => new(identifier, null);

        public string NewPassportIdentifier(string packIdentifier, int version) => $"{packIdentifier}-v{version}";

        public bool IsTestIdentifier(string id) => IdentifierForms.IsTest(id);
    }

    private sealed class HookLogger : ILogger<PublicationService>
    {
        public List<string> Errors { get; } = [];

        public List<string> Information { get; } = [];

        public Action<string>? OnDebug { get; set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = formatter(state, exception);
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(line);
            }
            else if (logLevel == LogLevel.Information)
            {
                Information.Add(line);
            }
            else if (logLevel == LogLevel.Debug)
            {
                OnDebug?.Invoke(line);
            }
        }
    }
}
