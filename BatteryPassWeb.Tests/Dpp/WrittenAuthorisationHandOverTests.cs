using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R20, round two: accepting a hand-over needs both clusters' written authorisation (the receiver's, and the
/// giver's still); the receiver's Registry shows Accept as unavailable with the one line; declining and cancelling stay
/// open; a seed or reset keeps a tick the team set on a demo cluster.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WrittenAuthorisationHandOverTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);
    private static readonly ChangeSender SouthAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

    [Fact]
    public async Task Accepting_ShouldNeedTheGiversTickStill_DecliningShouldNot()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var handOvers = HandOverTests.Service(rig);
        var asked = await handOvers.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        await new ClusterRepository(rig.Context).RemoveWrittenAuthorisationAsync(BatteryTesting.North, DateTime.UtcNow); // the giver loses it

        Assert.Equal(WrittenAuthorisation.GiverRefusal, await handOvers.AnswerAsync(SouthAdmin, asked.HandOver!.Id, accept: true));
        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Null(await handOvers.AnswerAsync(SouthAdmin, asked.HandOver.Id, accept: false));
    }

    [Fact]
    public async Task CancellingAWaitingHandOver_ShouldStayOpenWithoutTheTick()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var handOvers = HandOverTests.Service(rig);
        await handOvers.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        await new ClusterRepository(rig.Context).RemoveWrittenAuthorisationAsync(BatteryTesting.North, DateTime.UtcNow);

        Assert.Null(await handOvers.CancelAsync(NorthAdmin, rig.PackId));
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
    }

    [Theory]
    [InlineData("both", null)]
    [InlineData("receiver", WrittenAuthorisation.Missing)]
    [InlineData("giver", WrittenAuthorisation.GiverMissing)]
    public async Task TheReceiversRegistry_ShouldShowAcceptAsUnavailable_WithTheOneLine(string missing, string? line)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var handOvers = HandOverTests.Service(rig);
        await handOvers.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        var clusters = new ClusterRepository(rig.Context);
        if (missing == "receiver")
        {
            await clusters.RemoveWrittenAuthorisationAsync(BatteryTesting.South, DateTime.UtcNow);
        }
        else if (missing == "giver")
        {
            await clusters.RemoveWrittenAuthorisationAsync(BatteryTesting.North, DateTime.UtcNow);
        }

        Assert.Equal(line, Assert.Single(await handOvers.WaitingForAsync(SouthAdmin)).CannotAccept);
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Registry", "Index.cshtml"));
        Assert.Contains("<button type=\"button\" class=\"bp-primary-button\" disabled aria-describedby=\"cannot-accept-@waiting.HandOver.Id\">Accept</button>", view, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASeed_ShouldKeepATickTheTeamSetOnADemoCluster()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var clusters = rig.Services.GetRequiredService<ClusterRepository>();
        await clusters.UpsertClusterAsync("demo-north", "Demo North Operations");
        await rig.Database.GetCollection<BsonDocument>("clusters").UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("clusterId", "demo-north"), Builders<BsonDocument>.Update.Set(ClusterRepository.DemoMark, true));
        await clusters.SetWrittenAuthorisationAsync("demo-north", new DateOnly(2026, 10, 20), "team@example.com", DateTime.UtcNow);

        Assert.Empty((await rig.Demo.SeedAsync(DemoRig.Who)).Notes);

        var north = WrittenAuthorisation.Of(await clusters.GetClusterByIdAsync("demo-north"))!;
        Assert.Equal((new DateOnly(2026, 10, 20), "team@example.com"), (north.On, north.By)); // kept, not overwritten
        Assert.Equal("demo-data", WrittenAuthorisation.Of(await clusters.GetClusterByIdAsync("demo-south"))!.By);
    }

    [Fact]
    public async Task Clearing_ShouldKeepATickTheTeamSet_OnACLusterKeptForRealUse()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var clusters = rig.Services.GetRequiredService<ClusterRepository>();
        await clusters.SetWrittenAuthorisationAsync("demo-north", new DateOnly(2026, 10, 20), "team@example.com", DateTime.UtcNow);
        await clusters.UpsertUserAsync("real.person@example.com", "Real Person", [AccessControlService.RoleNormalUser], "x");
        await clusters.UpsertClusterMembershipAsync("real.person@example.com", "demo-north", AccessControlService.RoleNormalUser);

        await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal("team@example.com", WrittenAuthorisation.Of(await clusters.GetClusterByIdAsync("demo-north"))!.By);
    }

    [Fact]
    public void TheClusterAdminsPage_ShouldHoldNoWorkingFormWhenLocked()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));

        Assert.Contains("@if (Model.Changes && Model.Status is { } statusWindows)", view, StringComparison.Ordinal);
        Assert.Contains("@if (Model.Changes && Model.HandOver.Waiting is null)\n    {", view, StringComparison.Ordinal);
        Assert.DoesNotContain("!Model.ReadOnly && Model.Status", view, StringComparison.Ordinal);
    }
}
