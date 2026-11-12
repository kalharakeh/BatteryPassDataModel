using System.Text;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 69: a file that can't be saved says so as the card's main message: what the file asks for, why it can't be
/// done for this battery now, and what to do. The rule stays: the file is saved whole or not at all for a battery.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class UploadRefusalWordsTests(MongoFixture mongo)
{
    private static readonly UploadStatus Repair = new(StatusEvent.Repaired, "2026-10-20", "Fan replaced");

    [Fact]
    public async Task ARepairOnADraft_ShouldSayPlainlyWhyNothingCanBeSaved_AndWhatToDo()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var file = Encoding.UTF8.GetBytes(UploadServiceTests.File(
            $$"""{ "batterySerialNumber": "{{UploadServiceTests.Serial(rig)}}", "reason": "repair", "repairDescription": "Fan replaced", "repairDate": "2026-10-20", "values": { "stateOfChargeSoC": 41 } }"""));
        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "repair.json", file);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;

        var view = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, check.CheckId, null)).Check!;

        Assert.False(view.WillChange);
        Assert.Equal(
            "This file asks for a repair, and a file is saved whole or not at all. A status change needs a published passport, and this battery isn't published yet. Publish the battery first, or take the repair out of the file to save its readings.",
            view.Refusal);
    }

    [Fact]
    public async Task AFileThatChangesNothing_ShouldSaySoAndWhatToDo()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var file = Encoding.UTF8.GetBytes(UploadServiceTests.File($$"""{ "batterySerialNumber": "{{UploadServiceTests.Serial(rig)}}", "values": { "noSuchValue": 1 } }"""));
        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "same.json", file);

        var view = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, check.CheckId, null)).Check!;

        Assert.False(view.WillChange);
        Assert.Equal(UploadPages.RefusalWords(null, null), view.Refusal);
        Assert.StartsWith("Nothing in this file would change the battery", view.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFileThatSaves_ShouldHaveNoRefusal()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var file = Encoding.UTF8.GetBytes(UploadServiceTests.File($$"""{ "batterySerialNumber": "{{UploadServiceTests.Serial(rig)}}", "values": { "stateOfChargeSoC": 41 } }"""));
        var check = await UploadServiceTests.Service(rig).CheckBatteryFileAsync(PublicationTesting.Team, rig.PackId, "soc.json", file);

        var view = (await UploadCheckTests.Pages(rig).CardAsync(pack, "/admin/batteries/x", forTeam: true, check.CheckId, null)).Check!;

        Assert.True(view.WillChange);
        Assert.Null(view.Refusal);
    }

    public static TheoryData<string?, bool, string> Reasons => new()
    {
        { StatusChangeService.WaitingForSuccessor, true, "This file asks for a repair, and a file is saved whole or not at all. This battery is waiting for its new passport, so its status doesn't change now. Publish or cancel the new passport on the battery's page first, or take the repair out of the file to save its readings." },
        { StatusChangeService.WasteIsFinal, true, "This file asks for a repair, and a file is saved whole or not at all. This battery is waste, which is final. Take the repair out of the file to save its readings." },
        { StatusChangeService.ValuesRefused, true, "This file asks for a repair, and a file is saved whole or not at all. A value the status change states again was refused or is missing; the table marks it. Correct it in the file and check it again, or take the repair out of the file to save its readings." },
        { StatusChangeService.RepairNeedsDescription, true, "This file asks for a repair, and a file is saved whole or not at all. The repair doesn't say what was repaired. Add repairDescription to the file and check it again, or take the repair out of the file to save its readings." },
        { StatusChangeService.RepairNeedsDate, true, "This file asks for a repair, and a file is saved whole or not at all. The repair has no day. Add repairDate to the file and check it again, or take the repair out of the file to save its readings." },
        { StatusChangeService.RepairNotInTheFuture, true, "This file asks for a repair, and a file is saved whole or not at all. The repair's day lies in the future. Correct repairDate in the file and check it again, or take the repair out of the file to save its readings." },
        { StatusChangeService.NotYours, true, "This file asks for a repair, and a file is saved whole or not at all. Only the team and the battery's cluster admins change its status. Ask the team, or take the repair out of the file to save its readings." },
        { "a battery that is waste can't become repurposed", false, "This file asks for the status repurposed, and a file is saved whole or not at all. A battery that is waste can't become repurposed. Take the status change out of the file to save its readings." },
        { "the passport can't get a new version now; the team sees why on its battery page", true, "This file asks for a repair, and a file is saved whole or not at all. The passport can't get a new version now; the team sees why on the battery's page. Put that right first, or take the repair out of the file to save its readings." },
        { SavePipeline.NoLongerChanges(DppStatuses.Invalid), false, "This battery's passport is marked invalid, so nothing on it changes any more. Nothing from this file can be saved for it; check that the file is for the right battery." },
        { SavePipeline.NoLongerChanges(DppStatuses.MarkedForDeletion), false, "This battery's passport is marked for deletion, so nothing on it changes any more. Nothing from this file can be saved for it; check that the file is for the right battery." },
        { SavePipeline.NoLongerChanges(DppStatuses.Inactive), false, "This battery's passport is inactive: the battery has a new passport. Upload the file on the new passport's battery page instead." },
        { UploadService.NamedTwice, false, "The file names this battery more than once, so none of its entries is used. Keep one entry for this battery and check the file again." },
        { UploadService.Ambiguous, false, "The file's entry for this battery can't be used: more than one battery has this serial number; use the battery ID. Correct the file and check it again." },
    };

    [Theory]
    [MemberData(nameof(Reasons))]
    public void EveryReason_ShouldSayWhyAndWhatToDo(string? problem, bool repair, string words) =>
        Assert.Equal(words, UploadPages.RefusalWords(problem, repair ? Repair : problem?.Contains("repurposed", StringComparison.Ordinal) == true ? new UploadStatus(StatusEvent.Repurposed) : null));

    [Fact]
    public void TheCard_ShouldShowTheRefusalAsItsMainMessage()
    {
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_UploadCard.cshtml"));

        Assert.Contains("@if (check.Refusal is { } refusal)", card, StringComparison.Ordinal);
        Assert.Contains("<strong>Nothing can be saved from this file.</strong>", card, StringComparison.Ordinal);
        Assert.True(card.IndexOf("check.Refusal is { } refusal", StringComparison.Ordinal) < card.IndexOf("check.Values.Count > 0", StringComparison.Ordinal));
    }
}
