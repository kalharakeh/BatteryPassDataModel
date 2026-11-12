using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6b, the owner's choice (3 Nov 2026, "Yes, read-only page"): a battery whose passport is marked invalid or
/// for deletion had no page for its cluster admin (only an active passport did), so they couldn't see its History, its
/// passports or why it was frozen. Its cluster admin now gets the page read-only: the mark, Status, Live values,
/// Modules, Versions and History; no forms, no hand-over, no accident upload. Every change is still refused. Another
/// cluster's admin, and an archived or replaced passport, still get nothing.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ReadOnlyClusterPageTests(MongoFixture mongo)
{
    private const string Reason = "Demo: declared values don't match the test report";
    private static readonly ChangeSender Team = PublicationTesting.Team;
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);
    private static readonly ChangeSender SouthAdmin = ChangeSender.ClusterAdmin("south-admin@example.com", BatteryTesting.South);

    private async Task<PublicationRig> MarkedAsync(bool forDeletion)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        if (forDeletion)
        {
            await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)));
            Assert.Null(await PassportMarkTests.Service(rig).MarkForDeletionAsync(Team, rig.PackId));
        }
        else
        {
            Assert.Null(await PassportMarkTests.Service(rig).MarkInvalidAsync(Team, rig.PackId, Reason));
        }

        return rig;
    }

    [Theory]
    [InlineData(false, DppStatuses.Invalid)]
    [InlineData(true, DppStatuses.MarkedForDeletion)]
    public async Task AMarkedBattery_ShouldOpenReadOnlyForItsClustersAdmin(bool forDeletion, string status)
    {
        var rig = await MarkedAsync(forDeletion);
        var pages = ClusterBatteryPagesTests.Pages(rig);

        var page = await pages.BatteryAsync(NorthAdmin, rig.PackId);

        Assert.NotNull(page);
        Assert.Equal(status, page.FrozenAs);
        Assert.True(page.ReadOnly);
        Assert.Equal(["overview", "status", "live", "modules", "versions", "history"], page.Sections.Select(section => section.Id));
        Assert.True(page.Cards.Frozen);
        Assert.False(page.Status!.CanChange);
        Assert.False(page.Status.MayAddNote);
        Assert.NotNull(page.MarkedSince);
        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), await pages.ManageHrefAsync(NorthAdmin, rig.PackId));
        Assert.Null(await pages.BatteryAsync(SouthAdmin, rig.PackId)); // only the cluster's own battery
        Assert.Null(await pages.ManageHrefAsync(SouthAdmin, rig.PackId));
    }

    [Fact]
    public async Task AnActiveBattery_ShouldStayEditable()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(NorthAdmin, rig.PackId))!;

        Assert.False(page.ReadOnly);
        Assert.Null(page.MarkedSince);
        Assert.Contains("handover", page.Sections.Select(section => section.Id));
        Assert.Contains("accidents", page.Sections.Select(section => section.Id));
    }

    [Fact]
    public async Task AMarkedBattery_ShouldRefuseEveryChange()
    {
        var rig = await MarkedAsync(forDeletion: false);
        var pages = ClusterBatteryPagesTests.Pages(rig);
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("stateOfChargeSoC")] = "42";

        Assert.Null(await pages.SaveLiveAsync(NorthAdmin, rig.PackId, PageTesting.Field(form), PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null), null));
        Assert.Null(await pages.AddAccidentReportAsync(NorthAdmin, rig.PackId, new DocumentUpload("report.pdf", "%PDF-1.4 report"u8.ToArray(), "Report", "en")));
        Assert.Null(await pages.ChangeStatusAsync(NorthAdmin, rig.PackId, nameof(StatusEvent.Repaired), _ => null, "2026-11-03", "A repair."));
        Assert.Null(await pages.AddHistoryNoteAsync(NorthAdmin, rig.PackId, "A note."));
        Assert.Null(await pages.OtherOperatorAsync(NorthAdmin, rig.PackId, new OtherOperator("Other operator", "Somewhere", null), link: false));
        Assert.NotNull((await HandOverTests.Service(rig).AskAsync(NorthAdmin, rig.PackId, BatteryTesting.South, confirm: true)).Problem);
        Assert.Null(await rig.Store.FindWaitingHandOverAsync(rig.PackId));
        Assert.Equal(stored["stateOfChargeSoC"].Value!.ToJsonString(), (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].Value!.ToJsonString());
    }

    [Fact]
    public async Task AReplacedPassport_ShouldStillGetNoPage()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        await rig.Store.SetPackClusterAsync(rig.PackId, BatteryTesting.North);

        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
        Assert.Null(await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(NorthAdmin, rig.PackId));
    }

    [Fact]
    public async Task TheRegistry_ShouldGiveAMarkedBatterysAdminItsPage()
    {
        var rig = await MarkedAsync(forDeletion: false);
        var batteries = await BatteryTesting.Pages(rig).ListAsync(null);

        var north = RegistryList.Build(Reader.With("admin", ReaderRole.ClusterAdmin, BatteryTesting.North), batteries, NorthAdmin);
        var south = RegistryList.Build(Reader.With("admin", ReaderRole.ClusterAdmin, BatteryTesting.South), batteries, SouthAdmin);

        Assert.Equal(ClusterBatteryPages.BatteryHref(rig.PackId), north.ManageHref(Assert.Single(north.Rows)));
        Assert.All(south.Rows, row => Assert.Null(south.ManageHref(row)));
    }

    [Fact]
    public void TheReadOnlyPage_ShouldOfferNothingThatChangesTheBattery()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var live = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_LiveValuesCard.cshtml"));

        // The mark's fact and date, without the reason (decision 34: the team's and market surveillance's).
        Assert.Contains("id=\"mark-notice\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Reason", view, StringComparison.Ordinal);
        // No hand-over button or windows on a read-only page.
        Assert.Contains("@if (!Model.ReadOnly && Model.HandOver.Waiting is null)\n            {\n                @* Opens the window directly", view, StringComparison.Ordinal);
        Assert.Contains("@if (!Model.ReadOnly && Model.Status is { } statusWindows)", view, StringComparison.Ordinal);
        Assert.Contains("@if (!Model.ReadOnly && Model.HandOver.Waiting is null)\n    {\n        <div class=\"bp-console-form-card bp-dialog-host\">", view, StringComparison.Ordinal);
        // A frozen passport's live values still show, read-only, with when each was measured.
        Assert.Contains("data-frozen-values", live, StringComparison.Ordinal);
        Assert.Contains("@if (Model.Frozen)", live, StringComparison.Ordinal); // no "type them here" on a frozen passport
        Assert.Contains("The last values measured on the battery, to read: nothing on the passport changes any more.", live, StringComparison.Ordinal);
    }
}
