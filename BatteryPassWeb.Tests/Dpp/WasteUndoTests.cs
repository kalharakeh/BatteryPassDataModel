using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R10: a cluster admin's "Waste" asks a second question before it saves, and the team can undo a "waste" set by mistake:
/// in its status window, with a required reason, back to the status the battery had before. The history and the audit
/// log keep both steps. Nobody else undoes it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WasteUndoTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<PublicationRig> WasteAfterARepairAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = StatusChangeTests.Service(rig);
        Assert.Null((await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Repaired, await StatusChangeTests.CurrentAsync(rig), "2026-10-20", "Fan."))).Problem);
        Assert.Null((await service.ChangeAsync(NorthAdmin, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)))).Problem);
        return rig;
    }

    [Fact]
    public async Task TheTeam_ShouldUndoAWasteBackToTheStatusItHadBefore_KeepingBothSteps()
    {
        var rig = await WasteAfterARepairAsync();
        var versionsBefore = await rig.Store.GetLatestVersionNumberAsync(rig.PackId);

        var result = await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.WasteUndone, await StatusChangeTests.CurrentAsync(rig), Reason: "Set by mistake"));

        Assert.Null(result.Problem);
        Assert.Equal("re-used", result.NewStatus);
        Assert.Equal(versionsBefore + 1, result.Version!.Number); // a new version; the one with waste stays in the passport's history
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("re-used", StatusChangeService.CurrentStatus(pack));
        Assert.Null(pack.StatusBeforeWaste);
        var events = (await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter()))
            .Where(entry => entry["eventType"].AsString == StatusChangeService.ChangedEvent)
            .ToArray();
        Assert.Contains(events, entry => entry["metadata"]["to"].AsString == "waste" && entry["actor"].AsString == NorthAdmin.Who);
        var undone = Assert.Single(events, entry => entry["metadata"]["what"].AsString == nameof(StatusEvent.WasteUndone));
        Assert.Equal(("waste", "re-used", "Set by mistake"), (undone["metadata"]["from"].AsString, undone["metadata"]["to"].AsString, undone["metadata"]["reason"].AsString));
        Assert.Contains("waste undone, back to re-used. Set by mistake", undone["message"].AsString, StringComparison.Ordinal);
        Assert.Equal(PublicationTesting.Team.Who, undone["actor"].AsString);
    }

    [Fact]
    public async Task TheUndoneBattery_ShouldAcceptANewWaste()
    {
        var rig = await WasteAfterARepairAsync();
        var service = StatusChangeTests.Service(rig);
        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.WasteUndone, await StatusChangeTests.CurrentAsync(rig), Reason: "Set by mistake"));

        var again = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));

        Assert.Null(again.Problem);
        Assert.Equal("re-used", (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.StatusBeforeWaste);
    }

    [Theory]
    [InlineData("team", "", "say why")]
    [InlineData("team", "   ", "say why")]
    [InlineData("team", null, "say why")]
    [InlineData("cluster-admin", "Set by mistake", "only the team")]
    public async Task AnUndo_ShouldNeedTheTeamAndAReason(string who, string? reason, string expected)
    {
        var rig = await WasteAfterARepairAsync();

        var result = await StatusChangeTests.Service(rig).ChangeAsync(who == "team" ? PublicationTesting.Team : NorthAdmin, rig.PackId, new StatusChange(StatusEvent.WasteUndone, await StatusChangeTests.CurrentAsync(rig), Reason: reason));

        Assert.Contains(expected, result.Problem, StringComparison.Ordinal);
        Assert.Equal("waste", StatusChangeService.CurrentStatus((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!));
    }

    [Fact]
    public async Task AnUndo_ShouldBeRefusedForABatteryThatIsNotWaste()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.WasteUndone, await StatusChangeTests.CurrentAsync(rig), Reason: "Set by mistake"));

        Assert.Equal(StatusChangeService.NotWaste, result.Problem);
    }

    [Fact]
    public async Task AWasteSetBeforeTheStatusWasKept_ShouldGoBackToOriginal()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = StatusChangeTests.Service(rig);
        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
        await rig.Store.SetStatusBeforeWasteAsync(rig.PackId, null); // as a waste made before this fix

        var result = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.WasteUndone, await StatusChangeTests.CurrentAsync(rig), Reason: "Set by mistake"));

        Assert.Equal("original", result.NewStatus);
    }

    [Fact]
    public async Task TheTeamsWindow_ShouldOfferTheUndoOnAWasteBattery_AndTheClusterAdminsNothing()
    {
        var rig = await WasteAfterARepairAsync();

        var team = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        var cluster = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(NorthAdmin, rig.PackId))!.Status!;

        Assert.Equal([StatusEvent.Corrected, StatusEvent.WasteUndone], team.Choices.Select(choice => choice.What));
        Assert.Empty(cluster.Choices);
    }

    [Fact]
    public async Task TheWindowsAnswer_ShouldReadTheReasonAndUndoTheWaste()
    {
        var rig = await WasteAfterARepairAsync();
        var card = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Status!;
        var form = card.Restated.Sections.SelectMany(section => section.Fields)
            .SelectMany(field => field.Form.Inputs.Select(input => field.Form.FieldName(input)).Select(name => (name, text: field.Text(name))))
            .ToDictionary(entry => entry.name, entry => entry.text, StringComparer.Ordinal);

        var result = await StatusChangeTests.Pages(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, nameof(StatusEvent.WasteUndone), name => name == "undoReason" ? "Set by mistake" : form.GetValueOrDefault(name), null, null);

        Assert.True(result is { Found: true, Problem: null }, result?.Problem);
        Assert.Equal("re-used", result!.NewStatus);
    }

    [Fact]
    public void TheClusterAdminsWindow_ShouldAskASecondQuestionBeforeWaste_NamingTheBattery()
    {
        var window = Read("Views", "Batteries", "_StatusDialog.cshtml");

        Assert.Contains("data-waste-confirm=\"@(!Model.ForTeam ? Model.BatteryName : null)\"", window, StringComparison.Ordinal);
        Assert.Contains("!Model.ForTeam", window, StringComparison.Ordinal);
        Assert.Contains("data-waste-step", window, StringComparison.Ordinal);
        Assert.Contains("ends the battery's life in the passport", window, StringComparison.Ordinal);
        Assert.Contains("Only the Scania team can undo it.", window, StringComparison.Ordinal);
        Assert.Contains("name=\"undoReason\"", window, StringComparison.Ordinal);
    }
}
