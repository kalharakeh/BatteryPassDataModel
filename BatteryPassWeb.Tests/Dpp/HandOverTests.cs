using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Hand-overs between clusters (design 7.8, decisions 9, 14 and 34; step 4d): a battery's cluster admin asks, naming the
/// receiving cluster by its ID; the receiver's cluster admins accept or decline; the sender or the team can cancel while
/// it waits; on acceptance the battery moves, with no new passport version.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class HandOverTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    private static readonly ChangeSender SouthAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

    [Fact]
    public async Task AskingWithoutConfirming_ShouldOnlyNameTheReceivingCluster()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var check = await Service(rig).AskAsync(NorthAdmin, rig.PackId, $" {BatteryTesting.South.ToUpperInvariant()} ", confirm: false);

        Assert.Null(check.Problem);
        Assert.Equal("Customer South", check.ReceiverName);
        Assert.Null(check.HandOver);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
    }

    [Fact]
    public async Task AConfirmedHandOver_ShouldWaitWhileTheBatteryStaysWithTheSender()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var asked = await Service(rig).AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Null(asked.Problem);
        var waiting = (await rig.Store.FindWaitingHandOverAsync(rig.PackId))!;
        Assert.Equal(asked.HandOver!.Id, waiting.Id);
        Assert.Equal((BatteryTesting.North, BatteryTesting.South, HandOverStates.Waiting, "north-admin@example.com"), (waiting.FromClusterId, waiting.ToClusterId, waiting.State, waiting.AskedBy));
        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId); // Q11: the sender keeps it
        Assert.Equal([waiting.Id], (await Service(rig).WaitingForAsync(SouthAdmin)).Select(item => item.HandOver.Id));
        Assert.Empty(await Service(rig).WaitingForAsync(NorthAdmin));
        Assert.Equal("north-admin@example.com", (await LastAsync(rig, HandOverService.AskedEvent))["actor"].AsString);
    }

    [Theory]
    [InlineData("cluster-unknown", HandOverService.NoSuchCluster)]
    [InlineData("cluster-test", HandOverService.SameCluster)]
    [InlineData("", HandOverService.NoSuchCluster)]
    public async Task AWrongReceiver_ShouldBeRefused(string to, string problem)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Equal(problem, (await Service(rig).AskAsync(NorthAdmin, rig.PackId, to, confirm: true)).Problem);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
    }

    [Fact]
    public async Task OnlyTheBatterysClusterAdmin_ShouldAskAndOnlyOnce()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        Assert.Equal(HandOverService.NotYours, (await service.AskAsync(SouthAdmin, rig.PackId, BatteryTesting.North, confirm: true)).Problem);
        Assert.Equal(HandOverService.TeamMovesDirectly, (await service.AskAsync(PublicationTesting.Team, rig.PackId, BatteryTesting.South, confirm: true)).Problem);
        Assert.Equal(HandOverService.OnlyActive, (await Service(draft).AskAsync(NorthAdmin, draft.PackId, BatteryTesting.South, confirm: true)).Problem);
        await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        Assert.Equal(HandOverService.AlreadyWaiting, (await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true)).Problem);
    }

    [Fact]
    public async Task Accepting_ShouldMoveTheBatteryWithoutANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        var asked = await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Equal(HandOverService.NotTheReceiver, await service.AnswerAsync(NorthAdmin, asked.HandOver!.Id, accept: true));
        var accepted = await service.AnswerAsync(SouthAdmin, asked.HandOver.Id, accept: true);

        Assert.Null(accepted);
        Assert.Equal(BatteryTesting.South, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId)); // the cluster isn't passport data
        var done = (await rig.Store.GetAsync<HandOver>(asked.HandOver.Id))!;
        Assert.Equal((HandOverStates.Accepted, "south-admin@example.com"), (done.State, done.AnsweredBy));
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
        // decision 14: the previous cluster no longer sees the purchaser data
        Assert.False(AudienceRules.MaySeePurchaserData(Reader.With("north-user", ReaderRole.ClusterUser, BatteryTesting.North), [BatteryTesting.South]));
        Assert.Equal(HandOverService.NotWaiting, await service.AnswerAsync(SouthAdmin, asked.HandOver.Id, accept: true));
    }

    [Fact]
    public async Task AcceptingAPassportNoLongerActive_ShouldBeRefused()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        var asked = await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        await new PassportMarks(rig.Store, new ApplicationAuditService(rig.Context), rig.Clock).MarkInvalidAsync(PublicationTesting.Team, rig.PackId, "Wrong figures");

        Assert.Equal(HandOverService.NoLongerActive, await service.AnswerAsync(SouthAdmin, asked.HandOver!.Id, accept: true));

        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Null(await service.AnswerAsync(SouthAdmin, asked.HandOver.Id, accept: false)); // it can still be declined
    }

    [Fact]
    public async Task Declining_ShouldLeaveTheBatteryWithTheSender()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        var asked = await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Null(await service.AnswerAsync(SouthAdmin, asked.HandOver!.Id, accept: false));

        Assert.Equal(BatteryTesting.North, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Equal(HandOverStates.Declined, (await rig.Store.GetAsync<HandOver>(asked.HandOver.Id))!.State);
        Assert.Null((await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true)).Problem); // it may ask again
    }

    [Fact]
    public async Task TheSenderOrTheTeam_ShouldCancelAWaitingHandOver()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Equal(HandOverService.NotYours, await service.CancelAsync(SouthAdmin, rig.PackId));
        Assert.Null(await service.CancelAsync(NorthAdmin, rig.PackId));
        Assert.Equal(HandOverService.NothingWaiting, await service.CancelAsync(NorthAdmin, rig.PackId));
        await service.AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);
        Assert.Null(await service.CancelAsync(PublicationTesting.Team, rig.PackId));
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
    }

    [Fact]
    public async Task TheTeamsDirectMove_ShouldCancelAWaitingHandOver()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var asked = await Service(rig).AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true);

        Assert.Null(await BatteryTesting.Pages(rig).SetClusterAsync("team@example.com", rig.PackId, BatteryTesting.South));

        var cancelled = (await rig.Store.GetAsync<HandOver>(asked.HandOver!.Id))!;
        Assert.Equal((HandOverStates.Cancelled, "team@example.com"), (cancelled.State, cancelled.AnsweredBy));
    }

    internal static HandOverService Service(PublicationRig rig) =>
        new(rig.Store, new ClusterRepository(rig.Context), new ApplicationAuditService(rig.Context), rig.Clock);

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
