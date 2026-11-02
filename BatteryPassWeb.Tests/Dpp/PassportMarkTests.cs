using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// "Invalid" and "marked for deletion" (design 7.6 and 7.7, J-8, decision 34; step 4c-2): set on an active passport,
/// with who and why; the passport then takes no more changes; only the team undoes either.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportMarkTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    private static readonly ChangeSender Surveillance = ChangeSender.MarketSurveillance("msa@example.com");

    private const string Reason = "Declared values don't match the test report";

    [Fact]
    public async Task MarketSurveillance_ShouldMarkAnActivePassportInvalidWithItsReason()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        rig.Clock.Now = rig.Clock.Now.AddHours(1);

        var problem = await Service(rig).MarkInvalidAsync(Surveillance, rig.PackId, $"  {Reason}  ");

        Assert.Null(problem);
        var version = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;
        Assert.Equal(DppStatuses.Invalid, version.DppStatus);
        Assert.Equal(1, version.Number); // no new version: #2 isn't signed
        Assert.Equal(rig.Clock.Now.UtcDateTime, version.LastUpdatedUtc);
        var mark = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Mark!;
        Assert.Equal(DppStatuses.Invalid, mark.Status);
        Assert.Equal(Reason, mark.Reason);
        Assert.Equal("msa@example.com", mark.By);
        Assert.Equal(nameof(SenderKind.MarketSurveillance), mark.ByRole);
        Assert.Equal(rig.Clock.Now.UtcDateTime, mark.AtUtc);
        var entry = await LastAsync(rig, PassportMarks.InvalidEvent);
        Assert.Equal(Reason, entry["metadata"]["reason"].AsString);
    }

    [Fact]
    public async Task AnInvalidPassport_ShouldTakeNoChangesAndGetNoNewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await Service(rig).MarkInvalidAsync(Team, rig.PackId, Reason);

        var live = await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 40 }, "form"));
        var publish = await rig.Publication.PublishAsync(Team, rig.PackId);

        Assert.False(live.Saved);
        Assert.Contains("invalid", Assert.Single(live.Keys).Message, StringComparison.Ordinal);
        Assert.Equal(PublicationService.NoNewVersion("invalid"), publish.Problem);
    }

    [Fact]
    public async Task OnlyTheTeamAndMarketSurveillance_ShouldMarkInvalidAndOnlyWithAReason()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        Assert.Equal(PassportMarks.NotYoursToMarkInvalid, await service.MarkInvalidAsync(ChangeSender.ClusterAdmin("admin@example.com", BatteryTesting.North), rig.PackId, Reason));
        Assert.Equal(PassportMarks.NeedsReason, await service.MarkInvalidAsync(Surveillance, rig.PackId, "  "));
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task OnlyAnActivePassport_ShouldBeMarked()
    {
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var inactive = await new SuccessionTests(mongo).InactiveAsync();

        Assert.Equal(PassportMarks.OnlyActive, await Service(draft).MarkInvalidAsync(Team, draft.PackId, Reason));
        Assert.Equal(PassportMarks.OnlyActive, await Service(inactive).MarkInvalidAsync(Surveillance, inactive.PackId, Reason));
        Assert.Equal(PassportMarks.NoBattery, await Service(draft).MarkInvalidAsync(Team, "pack-unknown", Reason));
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldUndoInvalid()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.MarkInvalidAsync(Surveillance, rig.PackId, Reason);

        var bySurveillance = await service.UndoAsync(Surveillance, rig.PackId);
        var byTeam = await service.UndoAsync(Team, rig.PackId);

        Assert.Equal(PassportMarks.OnlyTheTeamUndoes, bySurveillance);
        Assert.Null(byTeam);
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Mark);
        Assert.Equal(Reason, (await LastAsync(rig, PassportMarks.UndoneEvent))["metadata"]["reason"].AsString); // kept in the log
        Assert.True((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 41 }, "form"))).Saved);
    }

    [Fact]
    public async Task OnlyAWasteBattery_ShouldBeMarkedForDeletionByTheTeam()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        var notWaste = await service.MarkForDeletionAsync(Team, rig.PackId);
        await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
        var bySurveillance = await service.MarkForDeletionAsync(Surveillance, rig.PackId);
        var marked = await service.MarkForDeletionAsync(Team, rig.PackId);

        Assert.Equal(PassportMarks.OnlyWaste, notWaste);
        Assert.Equal(PassportMarks.OnlyTheTeamMarksForDeletion, bySurveillance);
        Assert.Null(marked);
        Assert.Equal(DppStatuses.MarkedForDeletion, await rig.Store.PassportStatusAsync(rig.PackId));
        Assert.Equal(DppStatuses.MarkedForDeletion, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Mark!.Status);
        Assert.Null(await service.UndoAsync(Team, rig.PackId));
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task NothingToUndo_ShouldBeSaid()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.Equal(PassportMarks.NothingToUndo, await Service(rig).UndoAsync(Team, rig.PackId));
    }

    internal static PassportMarks Service(PublicationRig rig) =>
        new(rig.Store, new ApplicationAuditService(rig.Context), rig.Clock);

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
