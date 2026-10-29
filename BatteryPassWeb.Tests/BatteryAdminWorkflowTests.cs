namespace BatteryPassWeb.Tests;

public sealed class BatteryAdminWorkflowTests
{
    [Fact]
    public void BatteryCreatePage_ShouldPreviewGeneratedBatteryId()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("Battery ID preview", view);
        Assert.Contains("data-battery-id-preview", view);
        Assert.Contains("/admin/batteries/id-preview", view);
    }

    [Fact]
    public void AdminClustersView_ShouldManageBatteriesWithEmbeddedPassportHistory()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));

        Assert.Contains("Battery ID", view);
        Assert.Contains("Battery Model", view);
        Assert.Contains("row.HistoryUrl", view);
        Assert.Contains("Create passport", view);
        Assert.DoesNotContain("data-battery-passport-history", view);
        Assert.DoesNotContain("Battery version", view);
    }

    [Fact]
    public void AdminBatteryEdit_ShouldEditBatteryAndReturnToBatteryListWithPendingSnapshot()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("battery-edit", view);
        Assert.Contains("readonly", view);
        Assert.Contains("New passport", view);
    }

    [Fact]
    public void AdminBatteryCreate_ShouldRequireClusterAndSnapshotShouldClearPendingFlag()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var creationService = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));

        Assert.Contains("Battery cluster is required.", creationService);
        Assert.DoesNotContain("snapshot[\"newPassportRequired\"] = true;", controller);
    }

    [Fact]
    public void AdminBatteryList_ShouldUseBatteriesTabAsCanonicalDestination()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var conformance = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.Contains("\"passports\" => \"batteries\"", controller);
        Assert.DoesNotContain("href=\"/admin/clusters?tab=passports\"", help);
        Assert.Contains("href=\"/admin/batteries\"", help);
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
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-action-button", css);
        Assert.Contains("bp-action-icon", css);
        Assert.Contains("data-action-icon=\"edit\"", clusters);
        Assert.Contains("data-action-icon=\"conformance\"", clusters);
        Assert.Contains("data-action-icon=\"create-passport\"", clusters);
        Assert.DoesNotContain("&#9776;", clusters);
        Assert.DoesNotContain("&#9998;", clusters);
        Assert.DoesNotContain("&#10003;", clusters);
    }

    [Fact]
    public void FollowupBatteryCreate_ShouldRequireClusterAndUniqueSerial()
    {
        var creationService = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("Battery cluster is required.", creationService);
        Assert.Contains("Battery serial number already exists.", creationService);
        Assert.Contains("GetBySerialNumberAsync", creationService);
        Assert.Contains("GetBySerialNumberAsync", repository);
        Assert.Contains("name=\"clusterId\" required", view);
        Assert.Contains("is-policy-locked", view);
        Assert.Contains("data-editable-policy-locked", view);
    }

    [Fact]
    public void FollowupPassportCreation_ShouldBeSnapshotOnlyAndAvoidDraftNewPassportConflict()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var deltaService = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportDeltaService.cs"));
        var table = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));

        Assert.Contains("CreatePassportSnapshotAsync", clusterAdminController);
        Assert.Contains("Redirect($\"/cluster-admin/passports", clusterAdminController);
        Assert.DoesNotContain("return Redirect($\"/admin/passports/{Uri.EscapeDataString(passportId)}/edit\")", controller);
        Assert.Contains("NumericBsonEquals", deltaService);
        Assert.Contains("row.DisplayStatus", table);
        Assert.Contains("row.ShowNewPassportRequired = row.NewPassportRequired && !IsDraftStatus(row.LatestPassportStatus);", File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs")));
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
