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
        Assert.DoesNotContain("data-battery-passport-history", view);
        Assert.DoesNotContain("Battery version", view);
    }

    [Fact]
    public void AdminBatteryEdit_ShouldEditBatteryAndReturnToBatteryListWithPendingSnapshot()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("[HttpGet(\"batteries/{batteryId}/edit\")]", controller);
        Assert.Contains("[HttpPost(\"batteries/{batteryId}/save\")]", controller);
        Assert.Contains("SaveBattery", controller);
        Assert.Contains("app.snapshot.newPassportRequired", controller);
        Assert.Contains("/admin/clusters?tab=batteries", controller);
        Assert.Contains("battery-edit", view);
        Assert.Contains("readonly", view);
        Assert.Contains("New passport", view);
    }

    [Fact]
    public void AdminBatteryCreate_ShouldRequireClusterAndSnapshotShouldClearPendingFlag()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("Battery cluster is required.", controller);
        Assert.Contains("UpdateNewPassportRequiredAsync", controller);
        Assert.Contains("ClearNewPassportRequiredAsync", controller);
        Assert.DoesNotContain("snapshot[\"newPassportRequired\"] = true;", controller);
    }

    [Fact]
    public void AdminBatteryList_ShouldUseBatteriesTabAsCanonicalDestination()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var conformance = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.Contains("private static string AdminBatteriesUrl", controller);
        Assert.Contains("return Redirect(AdminBatteriesUrl(q));", controller);
        Assert.Contains("TempData[\"StatusMessage\"] = $\"Passport {passportId} saved.\";", controller);
        Assert.Contains("return Redirect(AdminBatteriesUrl());", controller);
        Assert.Contains("\"passports\" => \"batteries\"", controller);
        Assert.DoesNotContain("href=\"/admin/clusters?tab=passports\"", help);
        Assert.Contains("href=\"/admin/clusters?tab=batteries\"", help);
        Assert.Contains("Back to battery list", conformance);
    }

    [Fact]
    public void BatteryCreate_ShouldDefaultModelToLatestFamilyModel()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("data-latest-product-version", view);
        Assert.Contains("selectLatestProductVersion", view);
    }

    [Fact]
    public void BatteryCreatePage_ShouldNotAskForPassportId()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("data-field-key=\"general.batteryIdPreview\"", view);
        Assert.Contains("if (!isNewBattery)", view);
        Assert.DoesNotContain("@(isBatteryEdit ? \"Battery ID\" : \"Passport ID\")", view);
        Assert.DoesNotContain("else if (isNew) { <input type=\"text\" name=\"passportId\" value=\"@passport.PassportId\" required /> }", view);
    }

    [Fact]
    public void AdminBatteryActions_ShouldUseUnifiedIconButtons()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var history = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-action-button", css);
        Assert.Contains("bp-action-icon", css);
        Assert.Contains("data-action-icon=\"history\"", clusters);
        Assert.Contains("data-action-icon=\"edit\"", clusters);
        Assert.Contains("data-action-icon=\"conformance\"", clusters);
        Assert.Contains("data-action-icon=\"create-passport\"", clusters);
        Assert.Contains("data-action-icon=\"summary\"", history);
        Assert.Contains("data-action-icon=\"detail\"", history);
        Assert.Contains("data-action-icon=\"audit\"", history);
        Assert.Contains("data-action-icon=\"archive\"", history);
        Assert.DoesNotContain("&#9776;", clusters);
        Assert.DoesNotContain("&#9998;", clusters);
        Assert.DoesNotContain("&#10003;", clusters);
        Assert.DoesNotContain(">A</a>", history);
        Assert.DoesNotContain(">X</button>", history);
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
