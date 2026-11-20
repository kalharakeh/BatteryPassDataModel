using System.IO.Compression;
using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Step 6d-4 (the owner, 9 Nov 2026): voided IDs are hidden by default, with a "Show voided" tick box like "Show demo
/// IDs". All IDs hides them unless it is ticked or the status asked for is Voided, and its export follows. Releases hides a
/// release whose IDs are all voided; a partly voided one stays with its counts. A release's own page and its export are
/// the full record, and the summary counts are unchanged.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryIdsVoidedTests(MongoFixture mongo)
{
    private const string Who = DemoRig.Who;

    /// <summary>"PO keep" (KEEP-0001 to 0003, KEEP-0002 voided) and "PO gone" (GONE-0001 and 0002, both voided).</summary>
    private static async Task<(DemoRig Rig, BatteryIdPool Pool, string Keep, string Gone)> SeededAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(Who);
        var pool = rig.Services.GetRequiredService<BatteryIdPool>();
        var keep = await pool.ReleaseAsync(Who, new ReleaseRequest("PO keep", null, [], "KEEP-0001\nKEEP-0002\nKEEP-0003"));
        var gone = await pool.ReleaseAsync(Who, new ReleaseRequest("PO gone", null, [], "GONE-0001\nGONE-0002"));
        Assert.Null(await pool.VoidAsync(Who, "KEEP-0002", "Label damaged"));
        Assert.Equal((2L, (string?)null), await pool.VoidUnusedAsync(Who, gone.ReleaseId!, "Order cancelled"));
        return (rig, pool, keep.ReleaseId!, gone.ReleaseId!);
    }

    private static BatteryIdsController Controller(DemoRig rig)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, Who), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)], "test")),
        };
        return new BatteryIdsController(rig.Services.GetRequiredService<BatteryIdPool>(), rig.Store)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private static async Task<BatteryIdsPage> PageAsync(BatteryIdsController controller, string? tab, string? status = null, string? release = null, bool voided = false) =>
        Assert.IsType<BatteryIdsPage>(Assert.IsType<ViewResult>(await controller.Index(tab, status, release, null, null, false, voided, CancellationToken.None)).Model);

    private static string[] Serials(IEnumerable<ReleasedBatteryId> items) => items.Select(item => item.SerialNumber).Order(StringComparer.Ordinal).ToArray();

    private static string Sheet(IActionResult result)
    {
        using var zip = new ZipArchive(new MemoryStream(Assert.IsType<FileContentResult>(result).FileContents));
        return new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
    }

    [Fact]
    public async Task AllIds_ShouldHideVoidedIds_UnlessShowVoidedIsTicked_OrTheStatusAskedForIsVoided()
    {
        var (rig, _, keep, _) = await SeededAsync(mongo);
        using var __ = rig;
        var controller = Controller(rig);

        var hidden = await PageAsync(controller, BatteryIdsTabs.Ids);
        var shown = await PageAsync(controller, BatteryIdsTabs.Ids, voided: true);
        var asked = await PageAsync(controller, BatteryIdsTabs.Ids, status: ReleasedIdStatuses.Voided);
        var oneRelease = await PageAsync(controller, BatteryIdsTabs.Ids, release: keep);

        Assert.Equal(["KEEP-0001", "KEEP-0003"], Serials(hidden.Items));
        Assert.Equal(3, hidden.HiddenVoidedIds); // the list says how many it hides
        Assert.Equal(["GONE-0001", "GONE-0002", "KEEP-0001", "KEEP-0002", "KEEP-0003"], Serials(shown.Items));
        Assert.Equal(0, shown.HiddenVoidedIds);
        Assert.Equal(["GONE-0001", "GONE-0002", "KEEP-0002"], Serials(asked.Items));
        Assert.Equal(["KEEP-0001", "KEEP-0003"], Serials(oneRelease.Items)); // a release filter doesn't bring them back
        Assert.Equal(3, hidden.Counts.GetValueOrDefault(ReleasedIdStatuses.Voided)); // the summary still counts them
    }

    [Fact]
    public async Task AllIdsExport_ShouldFollowWhatIsShown_WhileAReleasesExportHasEveryId()
    {
        var (rig, _, _, gone) = await SeededAsync(mongo);
        using var __ = rig;
        var controller = Controller(rig);

        var hidden = Sheet(await controller.Export(null, null, null, null, false, false, CancellationToken.None));
        var shown = Sheet(await controller.Export(null, null, null, null, false, true, CancellationToken.None));
        var asked = Sheet(await controller.Export(ReleasedIdStatuses.Voided, null, null, null, false, false, CancellationToken.None));
        var release = Sheet(await controller.Export(null, gone, null, null, false, true, CancellationToken.None));

        Assert.Contains("KEEP-0001", hidden, StringComparison.Ordinal);
        Assert.DoesNotContain("KEEP-0002", hidden, StringComparison.Ordinal);
        Assert.DoesNotContain("GONE-0001", hidden, StringComparison.Ordinal);
        Assert.Contains("KEEP-0002", shown, StringComparison.Ordinal);
        Assert.Contains("GONE-0001", shown, StringComparison.Ordinal);
        Assert.Contains("GONE-0002", asked, StringComparison.Ordinal);
        Assert.DoesNotContain("KEEP-0001", asked, StringComparison.Ordinal);
        Assert.Contains("GONE-0001", release, StringComparison.Ordinal);
        Assert.Contains("GONE-0002", release, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Releases_ShouldHideAReleaseWithOnlyVoidedIds_UnlessShowVoidedIsTicked_AndKeepAPartlyVoidedOne()
    {
        var (rig, _, keep, gone) = await SeededAsync(mongo);
        using var __ = rig;
        var controller = Controller(rig);

        var hidden = await PageAsync(controller, null);
        var shown = await PageAsync(controller, null, voided: true);

        Assert.DoesNotContain(hidden.Releases, release => release.ReleaseId == gone);
        Assert.Equal(1, hidden.HiddenReleases);
        var partly = Assert.Single(hidden.Releases, release => release.ReleaseId == keep);
        Assert.Equal(1, partly.Count(ReleasedIdStatuses.Voided)); // with its counts
        Assert.Equal(2, partly.Count(ReleasedIdStatuses.Available));
        Assert.False(partly.AllVoided);
        Assert.True(Assert.Single(shown.Releases, release => release.ReleaseId == gone).AllVoided);
        Assert.Equal(0, shown.HiddenReleases);
        Assert.Contains(hidden.Releases, release => release.IsDemo);
    }

    [Fact]
    public async Task AReleasesOwnPage_ShouldListEveryIdWithItsStatus_EvenWhenAllAreVoided()
    {
        var (rig, pool, keep, gone) = await SeededAsync(mongo);
        using var __ = rig;
        var controller = Controller(rig);

        var allVoided = Assert.IsType<ReleasePage>(Assert.IsType<ViewResult>(await controller.Release(gone, false, CancellationToken.None)).Model);
        var partly = Assert.IsType<ReleasePage>(Assert.IsType<ViewResult>(await controller.Release(keep, false, CancellationToken.None)).Model);

        Assert.Equal(["GONE-0001", "GONE-0002"], Serials(allVoided.Items));
        Assert.All(allVoided.Items, item => Assert.Equal(ReleasedIdStatuses.Voided, item.Status));
        Assert.Equal(["KEEP-0001", "KEEP-0002", "KEEP-0003"], Serials(partly.Items));
        Assert.NotNull(await pool.ReleaseAsync(gone)); // still found by its own address
    }

    [Fact]
    public void TheViews_ShouldOfferShowVoided_AndAReleasesExportShouldAskForEveryId()
    {
        var index = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Index.cshtml"));
        var release = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Release.cshtml"));

        Assert.Equal(2, index.Split("<input type=\"checkbox\" name=\"voided\" value=\"true\" checked=\"@filter.Voided\" /><span>Show voided</span>").Length - 1); // on Releases and All IDs
        Assert.Contains("(\"voided\", filter.Voided ? \"true\" : null)", index, StringComparison.Ordinal); // All IDs' export follows the box
        Assert.Contains("/admin/battery-ids/export?release=@Uri.EscapeDataString(release.ReleaseId)&voided=true", index, StringComparison.Ordinal);
        Assert.Contains("/admin/battery-ids/export?release={Uri.EscapeDataString(release.ReleaseId)}&voided=true", release, StringComparison.Ordinal);
    }

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
