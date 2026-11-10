using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03 in the API: the demo team token reads a real battery as a visitor and finds no real battery to change;
/// on a demo battery it keeps the team's reach.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoApiTests(MongoFixture mongo)
{
    private static Reader DemoTeamReader => Reader.With("api-token:demo", ReaderRole.Team) with { DemoOnly = true };

    private static ChangeSender DemoTeamSender => ChangeSender.Team("api-token:demo") with { DemoOnly = true };

    private static Task MakeDemoAsync(PublicationRig rig) => TestDemo.MakeInsideAsync(rig);

    /// <summary>The passport reader on its own gives a demo reader the visitor's passport (the account path uses it).</summary>
    [Fact]
    public async Task ThePassportReader_ShouldGiveADemoReaderTheVisitorsPassport()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var api = PassportApiTests.Api(rig);
        var id = PassportPagesTests.Identifier(rig);

        var demo = (await api.ReadAsync(DemoTeamReader, id))!;
        var visitor = (await api.ReadAsync(Reader.Anonymous, id))!;
        var team = (await api.ReadAsync(Reader.With("api-token:real", ReaderRole.Team), id))!;

        Assert.Equal(visitor.Passport.ToJsonString(), demo.Passport.ToJsonString());
        Assert.NotEqual(visitor.Passport.ToJsonString(), team.Passport.ToJsonString());
    }

    [Fact]
    public async Task TheDemoTeamToken_ShouldGetWhatAnUnknownIdGets_OnARealBattery_AndBeServedOnADemoOne()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var repository = DppApiControllerTests.Repository(rig);
        var (document, token) = await repository.CreateTokenAsync("Demo team (full access)", BatteryPassWeb.Services.ExternalTokenAccessMode.Lifecycle, [], false, true, "test");
        await repository.MarkDemoAsync(document["_id"]);
        var id = PassportPagesTests.Identifier(rig);

        var read = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(await DppApiControllerTests.Controller(rig, token).Battery(id, CancellationToken.None));
        var publish = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(await DppApiControllerTests.Controller(rig, token).Publish(id, CancellationToken.None));

        var unknown = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(await DppApiControllerTests.Controller(rig, token).Battery("no-such-battery", CancellationToken.None));
        var unknownPublish = Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(await DppApiControllerTests.Controller(rig, token).Publish("no-such-battery", CancellationToken.None));
        Assert.Equal((unknown.StatusCode, DppApiControllerTests.Json(unknown).GetProperty("message").GetString()), (read.StatusCode, DppApiControllerTests.Json(read).GetProperty("message").GetString()));
        Assert.Equal((unknownPublish.StatusCode, DppApiControllerTests.Json(unknownPublish).GetProperty("message").GetString()), (publish.StatusCode, DppApiControllerTests.Json(publish).GetProperty("message").GetString()));
        Assert.Equal(404, read.StatusCode);

        await MakeDemoAsync(rig);
        Assert.Equal(200, Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(await DppApiControllerTests.Controller(rig, token).Battery(id, CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task TheDemoTeamToken_ShouldFindNoRealBatteryToChange_ButADemoOne()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var api = BatteryApiTests.Api(rig);
        var id = PassportPagesTests.Identifier(rig);

        Assert.Null((await api.FindAsync(DemoTeamSender, id)).Pack);
        Assert.NotNull((await api.FindAsync(ChangeSender.Team("api-token:real"), id)).Pack);

        await MakeDemoAsync(rig);
        Assert.NotNull((await api.FindAsync(DemoTeamSender, id)).Pack);
    }
}
