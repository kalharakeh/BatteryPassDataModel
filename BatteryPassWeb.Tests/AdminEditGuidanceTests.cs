namespace BatteryPassWeb.Tests;

public sealed class AdminEditGuidanceTests
{
    [Fact]
    public void EditPassportModel_ShouldExposeDataRequirementGuidance()
    {
        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "EditPassportViewModel.cs"));

        Assert.Contains("DataRequirements", source);
        Assert.Contains("FieldRequirementByKey", source);
    }

    [Fact]
    public void AdminController_ShouldLoadDataRequirementsForEditForms()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("DataRequirements = dataRequirements", source);
        Assert.Contains("BuildFieldRequirementDictionary", source);
    }

    [Fact]
    public void EditPassportView_ShouldRenderRequiredOptionalMarkersAndGuidance()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-field-shell", markup);
        Assert.Contains("bp-field-marker", markup);
        Assert.Contains("Required", markup);
        Assert.Contains("Optional", markup);
        Assert.Contains("Changing this after signing makes the passport dirty", markup);
        Assert.Contains("External HTTP updates to this live operational data do not dirty", markup);
        Assert.Contains("FieldRequirementByKey", markup);
        Assert.Contains("general.passportId", markup);
        Assert.Contains("general.serialNumber", markup);
        Assert.Contains("general.batteryImageUrl", markup);
        Assert.Contains("material.nickelMass", markup);
        Assert.Contains("material.lithiumMass", markup);
        Assert.Contains("performance.stateOfCharge", markup);
        Assert.Contains("compliance.euDeclarationOfConformity", markup);
        Assert.Contains("supplyChain.dueDiligenceReport", markup);
        Assert.Contains("circularity.recycledLeadPrimary", markup);
        Assert.Contains("carbon.co2StudyReference", markup);
        Assert.DoesNotContain("<label>Name<input", markup);
        Assert.DoesNotContain("<label>Rated energy kWh<input", markup);
        Assert.DoesNotContain("<label>Amount gCO2e/kWh<input", markup);

        Assert.Contains(".bp-field-shell", css);
        Assert.Contains(".bp-field-marker", css);
        Assert.Contains(".bp-field-help", css);
    }

    [Fact]
    public void EditPassportView_ShouldGreyReadonlyAndDisabledDateFields()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("type=\"date\" name=\"softwareReleaseDate\"", markup);
        Assert.Contains("type=\"date\" name=\"softwareLatestUpdate\"", markup);
        Assert.Contains("readonly data-product-software-release", markup);
        Assert.Contains("readonly data-product-software-update", markup);
        Assert.Contains("control.setAttribute('disabled', 'disabled');", markup);

        Assert.Contains(".bp-form-grid input[type=\"date\"][readonly]", css);
        Assert.Contains(".bp-form-grid input[type=\"date\"]:disabled", css);
        Assert.Contains("background: #eef2f6;", css);
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
