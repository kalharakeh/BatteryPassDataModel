using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
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
/// Finding R08, round three: everything a removal will do is written down first, in one write, before anything is
/// deleted, and finishing reads only what was written down. A removal that fails after any step is finished from its
/// marker: nothing of any chain member is left, each ID answers 410, exactly one audit entry exists.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportRemovalFinishTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = ChangeSender.Team(DemoRig.Who);

    public static TheoryData<string> FailingSteps() => ["markers", "audit entry", "versions", "documents", "uploads", "audit rows"];

    [Theory]
    [MemberData(nameof(FailingSteps))]
    public async Task ARemovalThatFailsAfterAStep_ShouldBeFinishedFromItsMarker(string step)
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var chain = await ChainMarkedForDeletionAsync(rig);
        var newest = chain[0];

        // It fails right after the step: the rest of the removal never runs.
        var failing = Removal(rig, new FailAfter(step));
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.RemoveAsync(Team, newest.Id, PassportRemoval.BatteryIdOf(newest)));

        // Every passport of the chain already answers 410: its marker was written in the one first write.
        foreach (var pack in chain)
        {
            Assert.NotNull(await rig.Store.FindRemovedAsync(PassportRemoval.BatteryIdOf(pack)));
        }

        // The hourly check's way: every unfinished removal, from its marker.
        var removal = Removal(rig, null);
        Assert.NotEmpty(await removal.UnfinishedAsync());
        foreach (var packId in await removal.UnfinishedAsync())
        {
            await removal.FinishAsync(packId);
        }

        await AssertNothingLeftAsync(rig, chain);
        Assert.Empty(await removal.UnfinishedAsync());
    }

    [Fact]
    public async Task AFailedAuditWrite_ShouldLeaveTheEntryOwed_AndTheFinishShouldWriteIt()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var chain = await ChainMarkedForDeletionAsync(rig);
        var events = rig.Database.GetCollection<BsonDocument>("applicationAuditEvents");
        await rig.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["collMod"] = "applicationAuditEvents",
            ["validator"] = new BsonDocument("eventType", new BsonDocument("$ne", PassportRemoval.RemovedEvent)),
            ["validationLevel"] = "strict",
            ["validationAction"] = "error",
        });

        await Assert.ThrowsAnyAsync<Exception>(() => Removal(rig, null).RemoveAsync(Team, chain[0].Id, PassportRemoval.BatteryIdOf(chain[0])));
        Assert.False((await rig.Store.GetAsync<RemovedPassport>(chain[0].Id))!.AuditWritten); // owed
        await rig.Database.RunCommandAsync<BsonDocument>(new BsonDocument { ["collMod"] = "applicationAuditEvents", ["validator"] = new BsonDocument() });

        Assert.True(await Removal(rig, null).FinishAsync(chain[2].Id)); // any marker of the chain finishes the whole removal

        await AssertNothingLeftAsync(rig, chain);
    }

    [Fact]
    public async Task FinishingAgain_ShouldChangeNothing()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var chain = await ChainMarkedForDeletionAsync(rig);
        var removal = Removal(rig, null);
        Assert.Null(await removal.RemoveAsync(Team, chain[0].Id, PassportRemoval.BatteryIdOf(chain[0])));

        foreach (var pack in chain)
        {
            Assert.False(await removal.FinishAsync(pack.Id)); // nothing left
        }

        await AssertNothingLeftAsync(rig, chain);
    }

    [Fact]
    public async Task UndoOnABatteryBeingRemoved_ShouldBeRefused_AndHidden()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
        Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(PublicationTesting.Team, rig.PackId, "recycled"));
        await rig.Store.AddRemovedAsync(new RemovedPassport { Id = rig.PackId, BatteryId = "x", Ids = [rig.PackId], RemovedAtUtc = DateTime.UtcNow, RemovedBy = "team@example.com" });

        Assert.Equal(PassportMarks.BeingRemoved, await PassportMarkTests.Service(rig).UndoAsync(PublicationTesting.Team, rig.PackId));
        Assert.Equal(DppStatuses.MarkedForDeletion, (await rig.Store.GetLatestVersionAsync(rig.PackId))!.DppStatus);
        Assert.Contains("@if (!Model.RemovalStarted)\n                {\n                    <button type=\"submit\" class=\"bp-secondary-button bp-button-compact\">Undo: make it active again</button>", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChainWithAnEarlierActivePassport_ShouldNotBeRemoved()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var packs = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Packs);
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var earlier = (await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        earlier["_id"] = "pack-earlier";
        earlier["Values"]["uniqueProductIdentifier"]["Value"] = "urn:test:earlier-battery";
        await packs.InsertOneAsync(earlier);
        var version = (await versions.Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        version["_id"] = "version-earlier";
        version["PackId"] = "pack-earlier";
        version["DigitalProductPassportId"] = "urn:test:earlier";
        await versions.InsertOneAsync(version); // still active
        await packs.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId), Builders<BsonDocument>.Update.Set("SuccessorOf", "pack-earlier"));
        await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
        Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(PublicationTesting.Team, rig.PackId, "recycled"));
        var removal = PassportRemovalTests.Removal(rig);

        Assert.Equal((null, PassportRemoval.EarlierStillActive), await removal.CheckPlanAsync(rig.PackId));
        Assert.Equal(PassportRemoval.EarlierStillActive, await removal.RemoveAsync(PublicationTesting.Team, rig.PackId, rig.Seed.Sources.Pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>()));
        Assert.Equal(PassportRemoval.EarlierStillActive, (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.RemovePassportProblem);
        Assert.Null(await rig.Store.FindRemovedAsync("pack-earlier"));
    }

    [Fact]
    public async Task ASaveDuringARemoval_ShouldSayWhy_AndTheClusterAdminsPageShouldAnswer410()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.AddRemovedAsync(new RemovedPassport { Id = rig.PackId, BatteryId = "x", Ids = [rig.PackId], RemovedAtUtc = DateTime.UtcNow, RemovedBy = "team@example.com" });

        var saved = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(40) }, "form"));
        Assert.False(saved.Saved);
        Assert.Equal(PassportRemoval.BeingRemoved, Assert.Single(saved.Keys).Message);

        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync("north-admin@example.com", BatteryTesting.North, "clusterAdmin");
        Assert.Null(await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId));
        var http = new DefaultHttpContext
        {
            User = TestAccounts.SignedIn(rig.Context, "north-admin@example.com", "clusterAdmin"),
            RequestServices = new ServiceCollection().AddSingleton(PassportRemovalTests.Removal(rig)).BuildServiceProvider(),
        };
        var controller = new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(clusters), UploadCheckTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(http, new NoTempData()),
        };
        Assert.Equal(StatusCodes.Status410Gone, Assert.IsType<ViewResult>(await controller.Battery(rig.PackId, CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task ClearingTheDemo_ShouldFinishAHalfDoneDemoRemovalFirst()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var chain = await ChainMarkedForDeletionAsync(rig);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Removal(rig, new FailAfter("markers")).RemoveAsync(Team, chain[0].Id, PassportRemoval.BatteryIdOf(chain[0])));

        await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(1, await rig.Database.GetCollection<BsonDocument>("applicationAuditEvents").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("eventType", PassportRemoval.RemovedEvent))); // the finish ran
        Assert.Equal(0, await rig.Database.GetCollection<BsonDocument>(DppStore.RemovedPassports).CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty)); // then the demo markers went
        Assert.Empty((await rig.Demo.SeedAsync(DemoRig.Who)).Notes);
    }

    [Fact]
    public async Task ResetDemoData_ShouldKeepTheTeamsTick_AndNotPutBackOneTheTeamTookAway()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var clusters = rig.Services.GetRequiredService<ClusterRepository>();
        await clusters.SetWrittenAuthorisationAsync("demo-north", new DateOnly(2026, 10, 20), "team@example.com", DateTime.UtcNow);
        await clusters.RemoveWrittenAuthorisationAsync("demo-south", DateTime.UtcNow, takenAwayBy: "team@example.com");

        Assert.Empty((await rig.Demo.ResetAsync(DemoRig.Who)).Notes);

        var north = WrittenAuthorisation.Of(await clusters.GetClusterByIdAsync("demo-north"))!;
        Assert.Equal((new DateOnly(2026, 10, 20), "team@example.com"), (north.On, north.By));
        Assert.Null(WrittenAuthorisation.Of(await clusters.GetClusterByIdAsync("demo-south")));
        Assert.Equal("demo-data", WrittenAuthorisation.Of(await clusters.GetClusterByIdAsync("demo-fleet"))!.By);
    }

    /// <summary>
    /// DEMO-C13M-0001's three passports, newest first, marked for deletion; the newest with an accident report, a withdrawn
    /// one and two upload lines (one in a record it shares with another name); plus an upload line refused under its ID
    /// before a pack was found, and an unrelated empty upload record, which must stay.
    /// </summary>
    private static async Task<IReadOnlyList<BatteryPack>> ChainMarkedForDeletionAsync(DemoRig rig)
    {
        await rig.Demo.SeedAsync(DemoRig.Who);
        var packs = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C13M-0001")).ToList();
        var newest = packs.Single(pack => !packs.Any(other => other.SuccessorOf == pack.Id));
        var chain = new List<BatteryPack> { newest };
        for (var at = newest.SuccessorOf; at is not null; at = chain[^1].SuccessorOf)
        {
            chain.Add(packs.Single(pack => pack.Id == at));
        }

        var batteryId = PassportRemoval.BatteryIdOf(newest);
        var documents = rig.Services.GetRequiredService<DocumentService>();
        Assert.Null((await documents.AddAccidentReportAsync(Team, newest.Id, new DocumentUpload("kept.pdf", DocumentTesting.Pdf()))).Problem);
        var withdrawn = (await documents.AddAccidentReportAsync(Team, newest.Id, new DocumentUpload("withdrawn.pdf", DocumentTesting.Pdf("w")))).Document!;
        Assert.Null(await documents.WithdrawAccidentReportAsync(Team, newest.Id, withdrawn.Id, "a mistake"));
        var uploads = rig.Services.GetRequiredService<UploadService>();
        await uploads.RunAsync(Team, JsonNode.Parse(UploadServiceTests.File(UploadServiceTests.Entry(batteryId, """{ "stateOfChargeSoC": 41 }"""))), "page", "alone.json", preview: false);
        await uploads.RunAsync(Team, JsonNode.Parse(UploadServiceTests.File(UploadServiceTests.Entry(batteryId, """{ "stateOfChargeSoC": 42 }"""), UploadServiceTests.Entry("no-such-battery", """{ "stateOfChargeSoC": 1 }"""))), "page", "shared.json", preview: false);
        var records = rig.Database.GetCollection<BsonDocument>(DppStore.Uploads);
        await records.InsertOneAsync(new BsonDocument { ["_id"] = "upload-refused", ["AtUtc"] = DateTime.UtcNow, ["Who"] = "x", ["SentAs"] = "Team", ["Channel"] = "api", ["Format"] = "native", ["Batteries"] = new BsonArray { new BsonDocument { ["Entry"] = 1, ["NamedAs"] = batteryId, ["PackId"] = BsonNull.Value, ["Outcome"] = "refused" } } });
        await rig.Database.GetCollection<BsonDocument>("applicationAuditEvents").InsertOneAsync(new BsonDocument
        {
            ["eventType"] = BatteryPassWeb.Dpp.Publication.PublicationService.CheckedEvent,
            ["entityType"] = "dpp-passports",
            ["entityId"] = "all",
            ["message"] = "Checked 2 published batteries: 1 now marked as waiting.",
            ["metadata"] = new BsonDocument { ["batteries"] = new BsonArray { "DEMO-C13M-0001", "OTHER-1" } },
        }); // the check's own entry lists serial numbers
        await records.InsertOneAsync(new BsonDocument { ["_id"] = "upload-unrelated-empty", ["AtUtc"] = DateTime.UtcNow, ["Who"] = "x", ["SentAs"] = "Team", ["Channel"] = "api", ["Format"] = "native", ["Batteries"] = new BsonArray() });

        var sources = (await rig.Store.LoadSourcesAsync(newest.Id))!;
        var current = StatusChangeService.RestatedAttributes(sources.Model).ToDictionary(
            attribute => attribute.Property,
            attribute => (sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values[attribute.Property]).Value?.DeepClone(),
            StringComparer.Ordinal);
        Assert.Null((await rig.Services.GetRequiredService<StatusChangeService>().ChangeAsync(Team, newest.Id, new StatusChange(StatusEvent.Waste, current))).Problem);
        Assert.Null(await rig.Services.GetRequiredService<PassportMarks>().MarkForDeletionAsync(Team, newest.Id, "exported"));
        return [.. chain.Select(pack => rig.Store.GetAsync<BatteryPack>(pack.Id).GetAwaiter().GetResult()!)];
    }

    private static async Task AssertNothingLeftAsync(DemoRig rig, IReadOnlyList<BatteryPack> chain)
    {
        var database = rig.Database;
        var events = database.GetCollection<BsonDocument>("applicationAuditEvents");
        var records = database.GetCollection<BsonDocument>(DppStore.Uploads);
        foreach (var pack in chain)
        {
            Assert.False(await rig.Store.IsAnythingLeftAsync(pack.Id), pack.Id);
            Assert.Equal(0, await database.GetCollection<BsonDocument>(DocumentStore.Records).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("OwnerId", pack.Id)));
            Assert.NotNull(await rig.Store.FindRemovedAsync(PassportRemoval.BatteryIdOf(pack))); // each ID answers 410
            Assert.Equal(0, await events.CountDocumentsAsync(Builders<BsonDocument>.Filter.Ne("eventType", PassportRemoval.RemovedEvent)
                & (Builders<BsonDocument>.Filter.Eq("entityId", pack.Id) | Builders<BsonDocument>.Filter.Eq("batteryId", PassportRemoval.BatteryIdOf(pack)))));
            Assert.Equal(0, await records.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Batteries.PackId", pack.Id)));
        }

        var marker = (await rig.Store.GetAsync<RemovedPassport>(chain[0].Id))!;
        Assert.Equal(3, marker.Members.Count); // each marker names the whole chain
        Assert.Equal(2, marker.DocumentIds.Count); // the withdrawn report's too
        Assert.Equal(0, await events.CountDocumentsAsync(Builders<BsonDocument>.Filter.In("entityId", marker.DocumentIds)));
        Assert.Equal(0, await records.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Batteries.NamedAs", PassportRemoval.BatteryIdOf(chain[0]))));
        Assert.Equal(0, await records.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "upload-refused")));
        Assert.Equal(1, await records.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "upload-unrelated-empty"))); // not this removal's
        Assert.Equal(1, await records.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Batteries.NamedAs", "no-such-battery"))); // the shared record keeps the other line
        Assert.Equal(1, await events.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("eventType", PassportRemoval.RemovedEvent))); // exactly one entry
        var check = await events.Find(Builders<BsonDocument>.Filter.Eq("eventType", BatteryPassWeb.Dpp.Publication.PublicationService.CheckedEvent) & Builders<BsonDocument>.Filter.Eq("message", "Checked 2 published batteries: 1 now marked as waiting.")).SingleAsync();
        Assert.Equal(["OTHER-1"], check["metadata"]["batteries"].AsBsonArray.Select(serial => serial.AsString)); // its serial taken out, the other kept
        Assert.All(await rig.Store.ListAsync<RemovedPassport>(), item => Assert.True(item.AuditWritten));
    }

    private static PassportRemoval Removal(DemoRig rig, ILogger<PassportRemoval>? logger) => new(
        rig.Store,
        rig.Services.GetRequiredService<DemoBoundary>(),
        rig.Services.GetRequiredService<DocumentStore>(),
        rig.Services.GetRequiredService<ApplicationAuditService>(),
        TimeProvider.System,
        logger);

    /// <summary>Fails the removal right after one step, as a database failure would.</summary>
    private sealed class FailAfter(string step) : ILogger<PassportRemoval>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Debug && formatter(state, exception).EndsWith($": {step} done.", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Injected failure after {step}.");
            }
        }
    }

    private sealed class NoTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
