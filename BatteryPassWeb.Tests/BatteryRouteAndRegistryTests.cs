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
