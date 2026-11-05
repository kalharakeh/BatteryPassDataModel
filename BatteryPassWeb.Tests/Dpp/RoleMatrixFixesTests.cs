using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Step 6c, the role-matrix check (5 Nov 2026): the faults it found, on the demo data. A battery placed on the market
/// again stays findable by its serial number when its newest passport is marked invalid (6); the audit log names a token
/// and a battery in words (3); and the page fixes 2, 4 and 5 in the stylesheets and the lists. Fault 1 (another cluster's
/// admin is sent to "Not allowed") is in ClusterBatteriesControllerTests.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed partial class RoleMatrixFixesTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n");

    [Theory]
    [InlineData(DppStatuses.Invalid)]
    [InlineData(DppStatuses.MarkedForDeletion)]
    public async Task ABatteryWithANewPassport_ShouldStayFindableBySerialNumber_WhenItsNewestIsMarked(string mark)
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pages = rig.Services.GetRequiredService<PassportPages>();

        // DEMO-C13M-0001: three passports, the two older inactive. Mark the newest, as market surveillance or the team would.
        var newest = await pages.FindAsync("DEMO-C13M-0001");
        Assert.NotNull(newest);
        Assert.Equal(DppStatuses.Active, newest.DppStatus);
        await rig.Store.SetVersionStatusAsync(newest.Id, mark, DateTime.UtcNow);

        var found = await pages.FindAsync("DEMO-C13M-0001");

        Assert.NotNull(found);
        Assert.Equal(newest.Id, found.Id);
        Assert.Equal(mark, found.DppStatus);
    }

    [Fact]
    public async Task TheApiAndUploads_ShouldFindTheSameBatteryBySerialNumber_WhenItsNewestIsMarked()
    {
        // The API's and the uploads' lookup (BatteryApi.FindAsync) already skips inactive passports (decision 34); this
        // keeps the pages and it in step.
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var newest = (await rig.Services.GetRequiredService<PassportPages>().FindAsync("DEMO-C13M-0001"))!;
        await rig.Store.SetVersionStatusAsync(newest.Id, DppStatuses.Invalid, DateTime.UtcNow);

        var target = await rig.Services.GetRequiredService<BatteryPassWeb.Dpp.Api.BatteryApi>().FindAsync(BatteryPassWeb.Dpp.Editing.ChangeSender.Team(DemoRig.Who), "DEMO-C13M-0001");

        Assert.Equal(newest.PackId, target.Pack?.Id);
    }

    [Fact]
    public async Task TheAuditLog_ShouldNameATokenAndABatteryInWords()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var events = await rig.Services.GetRequiredService<ApplicationAuditService>().ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var lines = await rig.Services.GetRequiredService<AuditLines>().DescribeAsync(events);

        // The demo data has a battery system's uploads (sent with a token) and DEMO-C13M-0001's earlier passports made inactive.
        Assert.Contains(lines, line => line.EventType == BatteryPassWeb.Dpp.Uploads.UploadService.UploadedEvent && line.Text.StartsWith("Battery system ", StringComparison.Ordinal));
        var inactive = lines.Where(line => line.EventType == BatteryPassWeb.Dpp.Publication.PublicationService.InactiveEvent).ToArray();
        Assert.NotEmpty(inactive);
        Assert.All(inactive, line => Assert.Contains(" set the passport of battery DEMO-C13M-0001 inactive", line.Text, StringComparison.Ordinal));
        Assert.All(lines, line => Assert.DoesNotMatch(RawId(), line.Text));
    }

    [Fact]
    public void TheHeadersChangeButton_ShouldBeDrawnAboveTheClusterValue() =>
        Assert.Contains(".bp-bh-facts dt .bp-text-button {\n  position: relative;\n  z-index: 1;\n  margin-block: -4px;", Read("web", "wwwroot", "css", "bp-product-data.css"), StringComparison.Ordinal);

    [Fact]
    public void OnAPhone_TheListsExplainer_ShouldSitBesideTheCount()
    {
        var list = Read("web", "Views", "Shared", "_BatteryList.cshtml");
        var css = Read("web", "wwwroot", "css", "bp-status.css");

        Assert.Contains("<div class=\"bp-batteries-count-row\">", list, StringComparison.Ordinal);
        Assert.True(list.IndexOf("@await Html.PartialAsync(\"_StatusHelp\")", StringComparison.Ordinal) < list.IndexOf("<table", StringComparison.Ordinal));
        Assert.Contains("<th>Status @await Html.PartialAsync(\"_StatusHelp\")</th>", list, StringComparison.Ordinal);
        Assert.Contains(".bp-batteries-count-row > .bp-status-help-button {\n  display: none;\n}", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 767px) {\n  .bp-batteries-count-row > .bp-status-help-button {\n    display: inline-flex;\n  }\n\n  .bp-mobile-card-table thead .bp-status-help-button {\n    display: none;\n  }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void OnAPhone_TheBadges_ShouldWrap() =>
        Assert.Contains("  .bp-mobile-card-table thead .bp-status-help-button {\n    display: none;\n  }\n\n  /* A long badge (\"Passport Marked for deletion · version 2\") wraps instead of widening the page (step 6c). */\n  .bp-badge {\n    max-width: 100%;\n    flex-wrap: wrap;\n    white-space: normal;\n  }\n}", Read("web", "wwwroot", "css", "bp-status.css"), StringComparison.Ordinal);

    [Fact]
    public void OnAPhone_TheStatusesInWords_ShouldStack()
    {
        // Found while checking fault 4: at 375 the explainer's words were squeezed beside the widest badge.
        var css = Read("web", "wwwroot", "css", "bp-status.css");
        var stack = css.LastIndexOf("@media (max-width: 767px) {\n  .bp-status-meanings {\n    grid-template-columns: minmax(0, 1fr);", StringComparison.Ordinal);

        Assert.True(stack > css.IndexOf(".bp-status-meanings {\n  display: grid;", StringComparison.Ordinal), "the phone rule comes after the list's own rule");
    }

    [GeneratedRegex(@"api-token:|\bpack-[0-9a-f]{6,}")]
    private static partial Regex RawId();
}
