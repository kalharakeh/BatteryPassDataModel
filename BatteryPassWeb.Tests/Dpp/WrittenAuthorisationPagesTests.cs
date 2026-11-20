using System.Text.Json;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Accounts;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R20 on the pages and the API: the cluster admin's battery page reads as before and shows its actions as
/// unavailable with one line; a form sent anyway changes nothing; a cluster token gets a clear 403; the team records and
/// takes away the authorisation on the Clusters page, both in the audit log; the demo clusters come authorised.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WrittenAuthorisationPagesTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";

    [Fact]
    public async Task TheClusterAdminsPage_ShouldReadAsBefore_WithItsActionsUnavailable_UntilTheTick()
    {
        var rig = await RigAsync(authorised: false);
        var sender = ChangeSenderFor(rig);

        var page = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(sender, rig.PackId))!;

        Assert.Equal(WrittenAuthorisation.Missing, page.CannotChange);
        Assert.False(page.Changes);
        Assert.False(page.ReadOnly); // still the active passport's page, to read
        Assert.Equal(WrittenAuthorisation.Missing, page.Status!.WhyNot); // one line, and no advice to do what they can't
        Assert.Equal((false, false, true), (page.Status.CanChange, page.Status.MayAddNote, page.Status.Locked));
        Assert.Equal(WrittenAuthorisation.Missing, page.Cards.FrozenNote);
        Assert.Equal(WrittenAuthorisation.Missing, page.HandOver.CannotChange);
        Assert.Contains(page.Sections, section => section.Id == "live");

        await new ClusterRepository(rig.Context).AuthoriseAsync(BatteryTesting.North);
        var after = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(sender, rig.PackId))!;

        Assert.Null(after.CannotChange);
        Assert.True(after.Changes);
        Assert.Null(after.Cards.FrozenNote);
        Assert.False(after.Status!.Locked);
    }

    [Theory]
    [InlineData("live")]
    [InlineData("status")]
    [InlineData("note")]
    [InlineData("handover")]
    [InlineData("upload")]
    public async Task AFormSentAnyway_ShouldChangeNothing_AndSayTheOneLine(string form)
    {
        var rig = await RigAsync(authorised: false);
        var before = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!.ToJsonString();
        var controller = Controller(rig, new Dictionary<string, string> { ["v.stateOfChargeSoC"] = "12", ["what"] = "waste", ["note"] = "A note", ["toClusterId"] = BatteryTesting.South, ["checkedId"] = BatteryTesting.South });

        var result = form switch
        {
            "live" => await controller.SaveLiveValues(rig.PackId, CancellationToken.None),
            "status" => await controller.ChangeStatus(rig.PackId, "waste", null, null, CancellationToken.None),
            "note" => await controller.AddHistoryNote(rig.PackId, "A note", CancellationToken.None),
            "handover" => await controller.HandOver(rig.PackId, BatteryTesting.South, BatteryTesting.South, HandOverTests.Service(rig), CancellationToken.None),
            _ => await controller.CheckUpload(rig.PackId, new FormFile(new MemoryStream(UploadCheckTests.Native(UploadServiceTests.Serial(rig), 12)), 0, 10, "file", "readings.json"), CancellationToken.None),
        };

        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal(WrittenAuthorisation.Missing, controller.TempData[ProductDataController.ErrorKey]);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal(before, pack.Values["stateOfChargeSoC"].Value!.ToJsonString());
        Assert.Null(pack.ChangesWaitingSinceUtc);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
    }

    [Fact]
    public async Task AClusterToken_ShouldGetAClear403_UntilTheTick()
    {
        var rig = await RigAsync(authorised: false);
        var token = (await DppApiControllerTests.Repository(rig).CreateTokenAsync("north-write", ExternalTokenAccessMode.ReadWrite, [BatteryTesting.North], false, false, "test")).PlainToken;
        var id = Uri.EscapeDataString(PassportPagesTests.Identifier(rig));

        var refused = Assert.IsAssignableFrom<ObjectResult>(await DppApiControllerTests.Controller(rig, token).WriteLiveValues(id, Body("""{ "values": { "stateOfChargeSoC": 12 } }"""), false, CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, refused.StatusCode);
        Assert.Contains(WrittenAuthorisation.Missing, JsonSerializer.Serialize(refused.Value, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), StringComparison.Ordinal);

        await new ClusterRepository(rig.Context).AuthoriseAsync(BatteryTesting.North);
        var saved = Assert.IsAssignableFrom<ObjectResult>(await DppApiControllerTests.Controller(rig, token).WriteLiveValues(id, Body("""{ "values": { "stateOfChargeSoC": 12 } }"""), false, CancellationToken.None));
        Assert.Equal(StatusCodes.Status200OK, saved.StatusCode);
    }

    [Fact]
    public async Task TheTeam_ShouldRecordAndTakeAwayTheAuthorisation_BothInTheAuditLog()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var admin = AccountRig.Form(rig.TeamMember(), ("clusterId", "north"), ("authorised", "true"), ("on", "2026-11-03"));

        Assert.IsType<RedirectResult>(await admin.ClusterAuthorisation(TimeProvider.System, CancellationToken.None));

        var given = WrittenAuthorisation.Of(await rig.Clusters.GetClusterByIdAsync("north"))!;
        Assert.Equal((new DateOnly(2026, 11, 3), AccountRig.Team), (given.On, given.By));
        Assert.Null(WrittenAuthorisation.Of(await rig.Clusters.GetClusterByIdAsync("south"))); // new clusters start without it
        var list = Assert.IsType<AdminClusterViewModel>(Assert.IsType<ViewResult>(await rig.TeamMember().Clusters("clusters", null, CancellationToken.None)).Model);
        Assert.Equal(new DateOnly(2026, 11, 3), list.Clusters.Single(cluster => cluster.ClusterId == "north").Authorisation!.On);
        Assert.Null(list.Clusters.Single(cluster => cluster.ClusterId == "south").Authorisation);

        var removing = AccountRig.Form(rig.TeamMember(), ("clusterId", "north"), ("remove", "true"));
        Assert.IsType<RedirectResult>(await removing.ClusterAuthorisation(TimeProvider.System, CancellationToken.None));

        Assert.Null(WrittenAuthorisation.Of(await rig.Clusters.GetClusterByIdAsync("north")));
        var events = await rig.Database.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("entityId", "north")).SortBy(entry => entry["_id"]).ToListAsync();
        Assert.Equal(["cluster.authorised", "cluster.authorisation-removed"], events.Select(entry => entry["eventType"].AsString).Where(type => type.StartsWith("cluster.auth", StringComparison.Ordinal)));
        Assert.All(events, entry => Assert.Equal(AccountRig.Team, entry["actor"].AsString));
    }

    [Theory]
    [InlineData("false", "2026-11-03")] // the tick not set
    [InlineData("true", "")]
    [InlineData("true", "3 Nov")]
    [InlineData("true", "2999-01-01")] // in the future
    public async Task TheTeam_ShouldBeAskedForTheTickAndAPastDate(string authorised, string on)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var admin = AccountRig.Form(rig.TeamMember(), ("clusterId", "north"), ("authorised", authorised), ("on", on));

        await admin.ClusterAuthorisation(TimeProvider.System, CancellationToken.None);

        Assert.NotNull(admin.TempData["ErrorMessage"]);
        Assert.Null(WrittenAuthorisation.Of(await rig.Clusters.GetClusterByIdAsync("north")));
    }

    [Fact]
    public async Task TheDemoClusters_ShouldComeAuthorised_AndAKeptOneShouldLoseTheDemosAuthorisation()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var clusters = await rig.All("clusters");
        Assert.NotEmpty(clusters);
        Assert.All(clusters, cluster => Assert.Equal("demo-data", WrittenAuthorisation.Of(cluster)!.By));

        // A demo cluster a real account still uses is kept, no longer as demo data, and without the demo's authorisation.
        var repository = rig.Services.GetRequiredService<ClusterRepository>();
        var kept = clusters[0]["clusterId"].AsString;
        await repository.UpsertUserAsync("real.person@example.com", "Real Person", [AccessControlService.RoleNormalUser], "x");
        await repository.UpsertClusterMembershipAsync("real.person@example.com", kept, AccessControlService.RoleNormalUser);
        await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Null(WrittenAuthorisation.Of(await repository.GetClusterByIdAsync(kept)));
    }

    private async Task<PublicationRig> RigAsync(bool authorised)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(NorthAdmin, BatteryTesting.North, "clusterAdmin");
        if (!authorised)
        {
            await clusters.RemoveWrittenAuthorisationAsync(BatteryTesting.North, DateTime.UtcNow);
        }

        return rig;
    }

    private static BatteryPassWeb.Dpp.Editing.ChangeSender ChangeSenderFor(PublicationRig rig) =>
        BatteryPassWeb.Dpp.Editing.ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North);

    private static ClusterBatteriesController Controller(PublicationRig rig, IReadOnlyDictionary<string, string> form)
    {
        var http = new DefaultHttpContext { User = TestAccounts.SignedIn(rig.Context, NorthAdmin, "clusterAdmin") };
        http.Request.Form = new FormCollection(form.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
        return new ClusterBatteriesController(ClusterBatteryPagesTests.Pages(rig), new DppReaders(new ClusterRepository(rig.Context)), UploadCheckTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
