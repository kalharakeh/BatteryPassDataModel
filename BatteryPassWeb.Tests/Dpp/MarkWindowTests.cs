using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, owner's item 2: "Mark as invalid or for deletion…" under More opens a window at page level, like Change
/// status (choose, give the reason, confirm); the separate Passport status section is gone, and More's menu has the
/// page's font and room for its words.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class MarkWindowTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task AWasteBattery_ShouldBeMarkedForDeletionWithItsReason()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        Assert.Null((await StatusChangeTests.Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await StatusChangeTests.CurrentAsync(rig)))).Problem);
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        var marked = Assert.IsType<RedirectResult>(await controller.Mark(rig.PackId, "deletion", "  Recycled at the north plant  ", CancellationToken.None));
        var view = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Marks;

        Assert.Equal(BatteryPages.BatteryHref(rig.PackId), marked.Url);
        Assert.Equal(DppStatuses.MarkedForDeletion, view.Status);
        Assert.Equal("Recycled at the north plant", view.Mark!.Reason);
        Assert.Equal("The passport is marked for deletion. It stays readable until it is deleted.", controller.TempData[BatteriesController.CardStatusKey]);
    }

    [Fact]
    public async Task AnInvalidMarkWithoutAReason_ShouldChangeNothingAndSaySoInTheHeader()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());

        await controller.Mark(rig.PackId, "invalid", " ", CancellationToken.None);

        Assert.Equal($"Nothing changed: {PassportMarks.NeedsReason}.", controller.TempData[BatteriesController.CardErrorKey]);
        Assert.Equal(DppStatuses.Active, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public void More_ShouldOpenTheMarkWindow_WhichAsksWhatAndWhy()
    {
        var page = Read("Views", "Batteries", "Battery.cshtml");
        var header = page[page.IndexOf("<header class=\"bp-bh\">", StringComparison.Ordinal)..page.IndexOf("</header>", StringComparison.Ordinal)];
        var window = Read("Views", "Batteries", "_MarkDialog.cshtml");

        Assert.Contains("<button type=\"button\" class=\"is-danger\" data-open-dialog=\"mark-dialog\">", header, StringComparison.Ordinal);
        Assert.Contains("@{ await CardMessagesAsync(\"mark\"); }", header, StringComparison.Ordinal);
        Assert.DoesNotContain("passport-status", page, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"))!, "_PassportStatusCard.cshtml")));
        Assert.Contains("<dialog id=\"mark-dialog\" class=\"bp-modal-panel bp-dialog\"", window, StringComparison.Ordinal);
        Assert.Contains("<form method=\"post\" action=\"@Model.Action\" class=\"bp-dialog-form\">", window, StringComparison.Ordinal);
        Assert.Contains("name=\"what\" value=\"invalid\" checked", window, StringComparison.Ordinal);
        Assert.Contains("name=\"what\" value=\"deletion\" disabled=\"@(!Model.Waste)\"", window, StringComparison.Ordinal);
        Assert.Contains("name=\"reason\"", window, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-console-form-card bp-dialog-host\">", window, StringComparison.Ordinal); // the radios keep the card's look
    }

    [Fact]
    public void MoresMenu_ShouldUseThePagesFontAndRoomForItsWords()
    {
        var css = Read("wwwroot", "css", "bp-product-data.css");

        Assert.Contains(".bp-more-list a,\n.bp-more-list button {", css, StringComparison.Ordinal);
        Assert.Contains("  font: inherit;\n  font-size: 0.92rem;\n  font-weight: 600;", css, StringComparison.Ordinal);
        Assert.Contains("  width: max-content;\n  min-width: 300px;", css, StringComparison.Ordinal);
    }
}
