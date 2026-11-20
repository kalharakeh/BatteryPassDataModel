using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R01, decisions 7 and 9: a battery published without a warranty (#19) while its model said "no" can't get a new version
/// once the model says "yes"; the team adds the warranty, once, through "Correct the values", which makes the new version.
/// A cluster admin is told what is missing and that the Scania team has to add it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WarrantyCorrectionTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";
    private const string Warranty = "warrantyPeriod";
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    [Fact]
    public async Task AfterTheModelSaysYes_TheTeamShouldAddTheMissingWarrantyThroughACorrection()
    {
        var rig = await PublishedWithoutWarrantyAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, true, true, true);
        Assert.Contains((await rig.Publication.CheckAsync(rig.PackId))!.Blocking, item => item.StartsWith("#19 ", StringComparison.Ordinal));

        var values = await StatusChangeTests.CurrentAsync(rig);
        values[Warranty] = JsonValue.Create("P8Y");
        var result = await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Corrected, values));

        Assert.Null(result.Problem);
        Assert.Equal(2, result.Version!.Number);
        Assert.Equal("P8Y", result.Version.StaticCore["batteryPassportData"]!["identifiersAndProductData"]![Warranty]!.GetValue<string>());
    }

    [Fact]
    public async Task AStoredWarranty_ShouldStayFixed()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var values = await StatusChangeTests.CurrentAsync(rig);
        values[Warranty] = JsonValue.Create("P2Y");

        var result = await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Corrected, values));

        Assert.Equal(StatusChangeService.WarrantyFixed, result.Problem);
        Assert.NotEqual("P2Y", (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values[Warranty].Value!.GetValue<string>());
    }

    [Fact]
    public async Task OnlyACorrection_ShouldAddTheWarranty()
    {
        var rig = await PublishedWithoutWarrantyAsync();
        var values = await StatusChangeTests.CurrentAsync(rig);
        values[Warranty] = JsonValue.Create("P8Y");

        var repair = await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Repaired, values, "2026-10-20", "Module 4 replaced."));

        Assert.Equal(StatusChangeService.WarrantyOnlyByCorrection, repair.Problem);
        Assert.False((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values.ContainsKey(Warranty));
    }

    [Fact]
    public async Task AClusterAdminsStatusChange_ShouldNameWhatIsMissingAndWhoAddsIt()
    {
        var rig = await PublishedWithoutWarrantyAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, true, true, true);

        var result = await StatusChangeTests.Service(rig).ChangeAsync(NorthAdmin, rig.PackId, new StatusChange(StatusEvent.Repaired, await StatusChangeTests.CurrentAsync(rig), "2026-10-20", "Module 4 replaced."));

        Assert.Equal(
            "the passport can't get a new version until the Scania team adds #19 Warranty period of the battery (needed because this model comes with a commercial warranty). Ask the Scania team",
            result.Problem);
    }

    [Fact]
    public async Task TheTeamsWindow_ShouldOfferTheWarrantyOnlyWhileTheBatteryHasNone()
    {
        var without = await PublishedWithoutWarrantyAsync();
        var sources = (await without.Store.LoadSourcesAsync(without.PackId))!;

        var team = await StatusChangeTests.Pages(without).CardAsync(sources, active: true, "/admin/batteries/x", forTeam: true);
        var admin = await StatusChangeTests.Pages(without).CardAsync(sources, active: true, "/x", forTeam: false);

        Assert.Contains(team.Restated.Sections.SelectMany(section => section.Fields), field => field.Attribute.Number == 19);
        Assert.DoesNotContain(admin.Restated.Sections.SelectMany(section => section.Fields), field => field.Attribute.Number == 19);
    }

    /// <summary>The seed pack, published once without a warranty while its model said "no commercial warranty".</summary>
    private async Task<PublicationRig> PublishedWithoutWarrantyAsync()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetModelAnswersAsync(ModelId, false, true, true);
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?>(), "form", Removals: [Warranty]));
        Assert.Null((await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId)).Problem);
        return rig;
    }
}
