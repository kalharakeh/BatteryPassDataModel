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
/// Finding R08 (the manager's decision): the team removes a recycled battery's passport, marked for deletion, after
/// typing its ID. Its data goes; one audit entry and a marker for its IDs stay; every address answers 410 with the plain
/// sentence; the ID is never used again; nothing publishes for it afterwards.
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
    public async Task Removing_ShouldDeleteThePassportsData_AndLeaveOneAuditEntryAndTheMarker()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var report = await DocumentServiceTests.Service(rig).AddAccidentReportAsync(Team, rig.PackId, new DocumentUpload("accident.pdf", DocumentTesting.Pdf()));
        Assert.Null(report.Problem);
        Assert.True((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(40) }, "form"))).Saved);
        await MarkForDeletionAsync(rig);
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow); // a marker, which goes with it
        var passportIds = (await rig.Store.ListVersionsAsync(rig.PackId)).Select(version => version.DigitalProductPassportId).ToArray();

        Assert.Null(await Removal(rig).RemoveAsync(Team, rig.PackId, Identifier(rig)));

        var database = rig.Context.Database!;
        Assert.Equal(0, await database.GetCollection<BsonDocument>(DppStore.Packs).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)));
        Assert.Equal(0, await database.GetCollection<BsonDocument>(DppStore.Versions).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)));
        Assert.Equal(0, await database.GetCollection<BsonDocument>(DocumentStore.Records).CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));
        Assert.Equal(0, await database.GetCollection<BsonDocument>($"{DocumentStore.Bucket}.files").CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));
        var audit = database.GetCollection<BsonDocument>("applicationAuditEvents");
        Assert.Equal(0, await audit.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("eventType", SavePipeline.AuditEventType) & Builders<BsonDocument>.Filter.Eq("entityId", rig.PackId)));
        var entry = await audit.Find(Builders<BsonDocument>.Filter.Eq("eventType", PassportRemoval.RemovedEvent)).SingleAsync();
        Assert.Equal(("team@example.com", Identifier(rig)), (entry["actor"].AsString, entry["metadata"]["batteryId"].AsString));

        var marker = (await rig.Store.FindRemovedAsync(Identifier(rig)))!;
        Assert.Equal(rig.PackId, marker.Id);
        Assert.All(passportIds, passportId => Assert.Contains(passportId, marker.Ids));
        Assert.NotNull(await rig.Store.FindRemovedAsync(IdentifierForms.Short(Identifier(rig))));
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

        var api = DppApiControllerTests.PassportsController(rig);
        api.ControllerContext.HttpContext.RequestServices = services;
        AssertGoneJson(await api.Detail(shortId, CancellationToken.None));
        AssertGoneJson(await api.Verify(shortId, CancellationToken.None));
        Assert.Equal(["Removed.cshtml"], new[] { Path.GetFileName(DppTestFiles.RepoFile("web", "Views", "Dpp", "Removed.cshtml")) });
        Assert.Contains("@PassportRemoval.Gone", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Dpp", "Removed.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHalfDoneRemoval_ShouldStillAnswer410_AndTheCheckShouldPassItBy()
    {
        // A crash after the marker and the pack went, with the versions still there.
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await MarkForDeletionAsync(rig);
        await rig.Store.AddRemovedAsync(new RemovedPassport { Id = rig.PackId, BatteryId = Identifier(rig), Ids = [rig.PackId, Identifier(rig), IdentifierForms.Short(Identifier(rig))], RemovedAtUtc = DateTime.UtcNow, RemovedBy = "team@example.com" });
        await rig.Store.DeleteAsync<BatteryPack>(rig.PackId);
        var services = new ServiceCollection().AddSingleton(Removal(rig)).BuildServiceProvider();
        var versions = await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).ToListAsync();

        Assert.Equal(410, Assert.IsType<ViewResult>(await Passport(rig, services).Summary(IdentifierForms.Short(Identifier(rig)), CancellationToken.None)).StatusCode);
        var result = await rig.Publication.ReconcileAsync("check-test");

        Assert.Equal((0, 0, 0, 0), (result.Checked, result.Marked, result.Archived, result.Failed));
        var after = await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).ToListAsync();
        Assert.Equal(versions.Select(version => version.ToJson()), after.Select(version => version.ToJson())); // nothing archived or revived
    }

    [Fact]
    public async Task ARemovalDuringTheCheck_ShouldBePassedBy()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await MarkForDeletionAsync(rig);
        // A marked battery's lower versions aside, give it an active one again so the check would look at it.
        var logger = new HookLogger();
        var service = new PublicationService(rig.Store, TestDemo.For(rig), new PublicationChecks(DppStoreTesting.Schemas), new PassportSigner(SigningTesting.Keys), new TestIdentifierScheme(), new ApplicationAuditService(rig.Context), rig.Clock, logger: logger);
        logger.OnDebug = () => Assert.Null(Removal(rig).RemoveAsync(Team, rig.PackId, Identifier(rig)).GetAwaiter().GetResult());

        var result = await service.ReconcileAsync("check-test");

        Assert.Equal(0, result.Failed);
        Assert.Empty(logger.Errors);
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(rig.PackId));
    }

    [Fact]
    public async Task NothingShouldPublishForARemovedBattery_AndItsIdShouldNotBeUsedAgain()
    {
        var rig = await RemovedRigAsync();

        Assert.Null(await rig.Publication.PublishWaitingAsync(Team, rig.PackId)); // "Publish the waiting changes"
        Assert.NotNull((await rig.Publication.PublishAsync(Team, rig.PackId)).Problem);
        Assert.False((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(40) }, "form"))).Found);
        Assert.Equal(WriteRefusal.NotFound, (await BatteryApiTests.Api(rig).FindAsync(ChangeSender.Feed("feed", BatteryTesting.North), Identifier(rig))).Refusal); // a live value from the feed
        Assert.Equal(0, await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)));

        var creation = new PackCreationService(rig.Store, TestDemo.For(rig), rig.Pipeline, new SameIdScheme(Identifier(rig)), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock);
        var again = await creation.CreateAsync(Team, new NewPack(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "VPC9-0201" }));
        Assert.Equal(PackCreationService.RemovedId, again.Problem);
    }

    [Fact]
    public async Task TheTeamsPage_ShouldOfferTheRemoval_OnlyWhenMarkedForDeletion_AndAskForTheId()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.Null((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.RemovePassportAction);
        await MarkForDeletionAsync(rig);
        Assert.Equal($"/admin/batteries/{rig.PackId}/remove-passport", (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.RemovePassportAction);

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

        public Action? OnDebug { get; set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
            else if (logLevel == LogLevel.Debug && formatter(state, exception).Contains("read the versions", StringComparison.Ordinal))
            {
                var hook = OnDebug;
                OnDebug = null;
                hook?.Invoke();
            }
        }
    }
}
