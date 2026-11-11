using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round two, C (decision 60): a status change publishes a new version, so a passport that can't get one changes no
/// status. The Status section says so before anything is typed, with the refusal's sentence, and "Change status" isn't
/// offered. Notes stay possible. For the team, a missing warranty alone leaves the window open: "Correct the values" adds it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class StatusBlockedUpFrontTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task AnUnansweredModel_ShouldCloseTheStatusWindowForTheTeamAndTheClusterAdmin_AndKeepNotes()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, null, null, null);

        var (team, admin) = await CardsAsync(rig);

        Assert.Equal((false, true), (team.CanChange, team.MayAddNote));
        Assert.Equal("No status change can be made now: the passport can't get a new version until this is filled in: the answers to its model's three questions.", team.WhyNot);
        Assert.Equal((false, true), (admin.CanChange, admin.MayAddNote));
        Assert.Equal("No status change can be made now: the passport can't get a new version until the Scania team adds the answers to its model's three questions. Ask the Scania team.", admin.WhyNot);
    }

    [Fact]
    public async Task AMissingLiveValue_ShouldTellTheClusterAdminToAddItUnderLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, BatteryPassWeb.Dpp.Values.AttributeValue>(), ["numberOfFullChargingAndDischargingCycles"]);

        var (_, admin) = await CardsAsync(rig);

        Assert.False(admin.CanChange);
        Assert.Equal("No status change can be made now: the passport can't get a new version until you add #83 Number of full charging and discharging cycles (needed because this model is made for cycling use) under Live values.", admin.WhyNot);
    }

    [Fact]
    public async Task AMissingWarrantyAlone_ShouldLeaveTheTeamsWindowOpen()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, false, true, true);
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?>(), "form", Removals: ["warrantyPeriod"]));
        Assert.Null((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem);
        await rig.Store.SetModelAnswersAsync(ModelId, true, true, true);

        var (team, admin) = await CardsAsync(rig);

        Assert.True(team.CanChange);
        Assert.Null(team.WhyNot);
        Assert.False(admin.CanChange);
        Assert.Contains("the Scania team adds #19 Warranty period of the battery", admin.WhyNot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APublishablePassport_ShouldOfferTheWindowAsBefore()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var (team, admin) = await CardsAsync(rig);

        Assert.Equal((true, null), (team.CanChange, team.WhyNot));
        Assert.Equal((true, null), (admin.CanChange, admin.WhyNot));
    }

    private static async Task<(BatteryPassWeb.Dpp.Pages.StatusCardView Team, BatteryPassWeb.Dpp.Pages.StatusCardView Admin)> CardsAsync(PublicationRig rig)
    {
        var sources = (await rig.Store.LoadSourcesAsync(rig.PackId))!;
        var pages = StatusChangeTests.Pages(rig);
        return (await pages.CardAsync(sources, active: true, "/admin/batteries/x", forTeam: true), await pages.CardAsync(sources, active: true, "/x", forTeam: false));
    }
}
