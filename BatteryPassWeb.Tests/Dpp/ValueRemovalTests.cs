using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Removing a stored value: the team may, through its editors; uploads and feeds can't, because for them empty never
/// overwrites (design 6.2, step 5). A value a published passport needs by February 2027 can't be removed. The audit log
/// keeps the value each change replaced (design 6.2, step 8).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ValueRemovalTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    private static ChangeSet Remove(params string[] keys) =>
        new(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?>(), "form", Removals: keys);

    [Fact]
    public async Task Team_ShouldRemoveAValueOfAnUnpublishedModel()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("batteryMass"));

        Assert.True(result.Saved);
        Assert.Equal(new KeyResult("batteryMass", KeyOutcome.Removed, string.Empty), Assert.Single(result.Keys));
        Assert.False((await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values.ContainsKey("batteryMass"));
    }

    [Fact]
    public async Task AValueAPublishedPassportNeeds_ShouldNotBeRemoved()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("batteryMass"));

        Assert.False(result.Saved);
        var key = Assert.Single(result.Keys);
        Assert.Equal(KeyOutcome.Rejected, key.Outcome);
        Assert.Contains("published passport", key.Message, StringComparison.Ordinal);
        Assert.True((await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values.ContainsKey("batteryMass"));
    }

    [Fact]
    public async Task AConditionalValueOfAPublishedPassport_ShouldBeRemovedAndPublishANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("symbolsForCadmiumAndLead"));

        Assert.Equal(KeyOutcome.Removed, Assert.Single(result.Keys).Outcome);
        Assert.False((await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values.ContainsKey("symbolsForCadmiumAndLead"));
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldRemoveValues()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var change = new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?>(), "feed", Removals: ["stateOfChargeSoC"]);

        var result = await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), change);

        Assert.Equal(KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task AValueThatIsntStored_ShouldHaveNothingToRemove()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("batteryMass"));

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("batteryMass"));

        Assert.False(result.Saved);
        Assert.Equal(KeyOutcome.Ignored, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task AChange_ShouldLogTheValueItReplaced()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var before = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["batteryMass"].Value!.ToJsonString();

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(
            TargetKind.Model,
            ModelId,
            new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonNode.Parse("""{"gramKilogramValue":600,"gramKilogramUnit":"kg"}""") },
            "form"));

        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var logged = Assert.Single(events, e => e["eventType"].AsString == SavePipeline.AuditEventType);
        Assert.Equal(before, logged["metadata"]["keys"][0]["previous"].AsString);
        Assert.Equal("""{"gramKilogramValue":600,"gramKilogramUnit":"kg"}""", logged["metadata"]["keys"][0]["value"].AsString);
    }
}
