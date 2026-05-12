namespace BatteryPassWeb.Tests;

public sealed class BatteryAdminWorkflowTests
{
    [Fact]
    public void AdminController_ShouldExposeBatteryCreationAndPassportSnapshotActions()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("[HttpGet(\"batteries/new\")]", source);
        Assert.Contains("[HttpPost(\"batteries/create\")]", source);
        Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports/create\")]", source);
        Assert.Contains("CreateBattery", source);
        Assert.Contains("CreateBatteryPassport", source);
        Assert.Contains("CreatePassportSnapshotAsync", source);
        Assert.Contains("Battery Family and serial number are locked", source);
    }

    [Fact]
    public void BatteryCreatePage_ShouldPreviewGeneratedBatteryId()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("[HttpGet(\"batteries/id-preview\")]", controller);
        Assert.Contains("PreviewBatteryId", controller);
        Assert.Contains("CreateBatteryId(product.ProductName, serialNumber)", controller);
        Assert.Contains("Battery ID preview", view);
        Assert.Contains("data-battery-id-preview", view);
        Assert.Contains("/admin/batteries/id-preview", view);
    }

    [Fact]
    public void AdminClustersView_ShouldManageBatteriesWithEmbeddedPassportHistory()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("Battery ID", view);
        Assert.Contains("Battery Model", view);
        Assert.Contains("Passport history", view);
        Assert.Contains("Create passport", view);
        Assert.Contains("data-battery-passport-history", view);
        Assert.DoesNotContain("Battery version", view);
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
