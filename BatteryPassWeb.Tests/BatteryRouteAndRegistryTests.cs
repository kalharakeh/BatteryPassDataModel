namespace BatteryPassWeb.Tests;

public sealed class BatteryRouteAndRegistryTests
{
    [Fact]
    public void PublicRoutes_ShouldResolveBatteryIdLatestAndPassportId()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
        var resolver = File.ReadAllText(RepoFile("web", "Services", "BatteryRouteResolutionService.cs"));

        Assert.Contains("[HttpGet(\"{id}\")]", controller);
        Assert.Contains("[HttpGet(\"{batteryId}/latest\")]", controller);
        Assert.Contains("ResolveAsync", resolver);
        Assert.Contains("GetByBatteryIdAsync", resolver);
        Assert.Contains("GetByPassportIdAsync", resolver);
        Assert.Contains("BatteryFirst", resolver);
    }

    [Fact]
    public void HomeSearch_ShouldSendBatteryIdsToBatteryPageAndPassportIdsToSnapshot()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));

        Assert.Contains("BatteryRouteResolutionService", source);
        Assert.Contains("Battery", source);
        Assert.Contains("Passport", source);
        Assert.Contains("/{Uri.EscapeDataString(query)}", source);
        Assert.DoesNotContain("/summary\");", source);
    }

    [Fact]
    public void QrService_ShouldBuildStableBatteryLatestPayload()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportQrCodeService.cs"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));

        Assert.Contains("BuildPayloadUrl(HttpRequest request, string batteryId)", source);
        Assert.Contains("/latest", source);
        Assert.Contains("BatteryRepository", controller);
        Assert.Contains("batteryId", controller);
    }

    [Fact]
    public void Registry_ShouldUseBatteryRowsAndPassportCounts()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));

        Assert.Contains("BatteryRepository", controller);
        Assert.Contains("BatterySummaryViewModel", controller);
        Assert.Contains("PassportCount", view);
        Assert.Contains("passports", view);
        Assert.Contains("@row.BatteryId", view);
        Assert.Contains("@row.BatteryModel", view);
        Assert.DoesNotContain("@row.PassportId</span></td>", view);
    }

    [Fact]
    public void RegistrySearch_ShouldSupportPassportBatterySerialAndGlobalAdminClusterSearch()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));

        Assert.Contains("ResolveSearchAsync", controller);
        Assert.Contains("SearchByClusterAsync", repository);
        Assert.Contains("Cluster search requires global admin access.", controller);
        Assert.Contains("Cluster search requires global admin access.", view);
    }

    [Fact]
    public void AccessControl_ShouldAllowClusterMembersToSeeSignedOrPublishedRegistryPassports()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));

        Assert.Contains("registryStatus.Equals(\"published\"", source);
        Assert.Contains("registryStatus.Equals(\"signed\"", source);
        Assert.Contains("passportPublishPolicyService.HasCurrentValidSignature(passport)", source);
    }

    [Fact]
    public void PassportReports_ShouldShowBatteryIdPassportIdAndHistoricalLatestState()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var combined = summary + Environment.NewLine + detail;

        Assert.Contains("Battery ID", combined);
        Assert.Contains("Passport ID", combined);
        Assert.Contains("Historical passport", combined);
        Assert.Contains("Latest passport", combined);
        Assert.Contains("Battery Model", combined);
        Assert.DoesNotContain("Battery version", combined);
    }

    [Fact]
    public void PassportReports_ShouldUseSerialHeaderAndIconLatestAction()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var combined = summary + detail;

        Assert.Contains("passport.BatterySerialNumber", summary);
        Assert.Contains("passport.BatterySerialNumber", detail);
        Assert.DoesNotContain(">Open latest passport<", combined);
        Assert.Contains("aria-label=\"Open latest passport\"", combined);
        Assert.Contains("bp-latest-passport-action-icon", combined);
    }

    [Fact]
    public void BatteryLevelView_ShouldShowBatteryIdLabelAndIconActions()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Passport", "Battery.cshtml"));

        Assert.Contains("Battery ID:", view);
        Assert.Contains("Passport ID", view);
        Assert.Contains("Latest", view);
        Assert.Contains("Historical", view);
        Assert.Contains("aria-label=\"Summary report\"", view);
        Assert.Contains("aria-label=\"Detailed report\"", view);
        Assert.Contains("bp-report-action-icon", view);
    }

    [Fact]
    public void AdminBatteryList_ShouldUseCompactBatteryRowsWithoutEmbeddedHistory()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("@row.PassportCount</td>", view);
        Assert.DoesNotContain("@row.PassportCount passports", view);
        Assert.DoesNotContain("bp-passport-history-row", view);
        Assert.DoesNotContain("data-battery-passport-history", view);
        Assert.Contains("/admin/batteries/@Uri.EscapeDataString(row.BatteryId)/passports", view);
        Assert.Contains("NewPassportRequired", model);
        Assert.Contains(".bp-admin-battery-table .bp-battery-id-cell", css);
        Assert.Contains("max-width: none", css);
    }

    [Fact]
    public void AdminBatteryPassportHistory_ShouldHaveDedicatedRouteAndIconActions()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));

        Assert.Contains("[HttpGet(\"batteries/{batteryId}/passports\")]", controller);
        Assert.Contains("BatteryPassports", controller);
        Assert.Contains("Back to Batteries", view);
        Assert.Contains("/admin/clusters?tab=batteries", view);
        Assert.Contains("Summary report", view);
        Assert.Contains("Detailed report", view);
        Assert.Contains("Conformance", view);
        Assert.Contains("Audit trail", view);
        Assert.Contains("Archive", view);
        Assert.Contains("Unarchive", view);
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
