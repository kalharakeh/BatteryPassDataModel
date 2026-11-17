using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// H1 round two, point 4: the restricted-read log has no gaps. The registry list shows battery statuses (purchaser data)
/// to market surveillance and the other restricted readers: the notice and one entry per reader and day. Each kind of page
/// has its own key, so opening the accident reports first doesn't hide that day's detail page. And a day's entry can't be
/// lost to a cancelled request or a failed audit write.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RestrictedReadsRoundTwoTests(MongoFixture mongo)
{
    private static readonly string[] Sections = ["purchaser"];

    private static async Task<(RestrictedReads Reads, MutableTimeProvider Clock, IMongoDatabase Database)> StartAsync(MongoFixture mongo)
    {
        var context = mongo.NewContext();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero));
        var reads = new RestrictedReads(context, new ApplicationAuditService(context), clock);
        await reads.EnsureIndexesAsync();
        return (reads, clock, context.Database!);
    }

    private static async Task<List<BsonDocument>> EntriesAsync(IMongoDatabase database) =>
        await database.GetCollection<BsonDocument>("applicationAuditEvents").Find(Builders<BsonDocument>.Filter.Eq("eventType", RestrictedReads.EventType)).ToListAsync();

    [Fact]
    public async Task TheAccidentReportsFirst_ShouldNotHideThatDaysDetailPage()
    {
        var (reads, _, database) = await StartAsync(mongo);

        Assert.True(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", RestrictedReads.AccidentsPage, "pack-1", "SN-0001", ["accident reports"]));
        Assert.True(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", RestrictedReads.DetailPage, "pack-1", "SN-0001", Sections));
        Assert.False(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", RestrictedReads.DetailPage, "pack-1", "SN-0001", Sections));

        Assert.Equal(2, (await EntriesAsync(database)).Count);
    }

    [Fact]
    public async Task ACancelledRequest_ShouldStillLeaveTheDaysEntry()
    {
        var (reads, _, database) = await StartAsync(mongo);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Assert.True(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", RestrictedReads.DetailPage, "pack-1", "SN-0001", Sections, cancelled.Token));

        Assert.Single(await EntriesAsync(database));
    }

    [Fact]
    public async Task AFailedAuditWrite_ShouldLeaveNoKey_SoTheNextOpeningIsLogged()
    {
        var (reads, _, database) = await StartAsync(mongo);
        var events = database.GetCollection<BsonDocument>("applicationAuditEvents");
        // Make the audit write fail: a unique index on the message, already taken.
        await events.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("message"), new CreateIndexOptions { Unique = true, Name = "fail" }));
        await events.InsertOneAsync(new BsonDocument { ["message"] = "opened the restricted data of battery SN-0001: purchaser" });

        await Assert.ThrowsAnyAsync<MongoException>(() => reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", RestrictedReads.DetailPage, "pack-1", "SN-0001", Sections));

        await events.Indexes.DropOneAsync("fail");
        Assert.True(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", RestrictedReads.DetailPage, "pack-1", "SN-0001", Sections));
        Assert.Single(await EntriesAsync(database));
    }

    [Fact]
    public async Task AKeyLeftHalfWayByAStoppedApp_ShouldBeTakenAgain()
    {
        var (reads, clock, database) = await StartAsync(mongo);
        await database.GetCollection<BsonDocument>(RestrictedReads.Collection).InsertOneAsync(new BsonDocument
        {
            ["_id"] = $"msa@example.test|{RestrictedReads.DetailPage}|pack-1|2026-11-13",
            ["at"] = clock.Now.UtcDateTime.AddMinutes(-10),
            ["done"] = false,
        });

        Assert.True(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", RestrictedReads.DetailPage, "pack-1", "SN-0001", Sections));
        Assert.Single(await EntriesAsync(database));
    }

    [Fact]
    public async Task TheRegistryList_ShouldBeLoggedOncePerReaderAndDay()
    {
        var (reads, _, database) = await StartAsync(mongo);

        Assert.True(await reads.NoteListAsync("msa@example.test", "Market Surveillance Authorities", ["pack-1", "pack-2"]));
        Assert.False(await reads.NoteListAsync("msa@example.test", "Market Surveillance Authorities", ["pack-1", "pack-2", "pack-3"]));

        var entry = Assert.Single(await EntriesAsync(database));
        Assert.Equal(2, entry["metadata"]["batteries"].ToInt32());
        Assert.Contains("battery statuses", entry["message"].AsString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarketSurveillanceOnTheRegistry_ShouldGetTheNoticeAndOneEntry_AndTheTeamNeither()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var reads = new RestrictedReads(rig.Context, new ApplicationAuditService(rig.Context), rig.Clock);

        var msa = Registry(rig, "msa@example.test", AccessControlService.RoleMarketSurveillanceAuthority);
        var page = (RegistryPage)Assert.IsType<ViewResult>(await msa.Index(null, HandOverTests.Service(rig), CancellationToken.None, reads)).Model!;
        Assert.NotEmpty(page.BatteryStatusShown);
        Assert.Equal("Market Surveillance Authorities", msa.ViewData[RestrictedReads.RoleKey]);
        await Registry(rig, "msa@example.test", AccessControlService.RoleMarketSurveillanceAuthority).Index(null, HandOverTests.Service(rig), CancellationToken.None, reads);

        var team = Registry(rig, "team@example.test", AccessControlService.RoleAdmin);
        await team.Index(null, HandOverTests.Service(rig), CancellationToken.None, reads);
        Assert.Null(team.ViewData[RestrictedReads.RoleKey]);

        Assert.Single(await EntriesAsync(rig.Context.Database!));
    }

    [Fact]
    public void TheRegistryPage_ShouldShowTheNoticeAboveTheList_NotInTheSharedListPartial()
    {
        var registry = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));
        var list = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_BatteryList.cshtml"));

        Assert.Contains("<partial name=\"_RestrictedNotice\" />", registry, StringComparison.Ordinal);
        Assert.DoesNotContain("_RestrictedNotice", list, StringComparison.Ordinal);
    }

    private static RegistryController Registry(PublicationRig rig, string email, string role)
    {
        var controller = new RegistryController(new DppReaders(new ClusterRepository(rig.Context)), BatteryTesting.Pages(rig));
        var http = new DefaultHttpContext { User = TestAccounts.SignedIn(rig.Context, email, role) };
        http.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new NoTempData());
        return controller;
    }

    private sealed class NoTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
