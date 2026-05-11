namespace BatteryPassWeb.Tests;

public sealed class DataRequirementsAdminLayoutTests
{
    [Fact]
    public void Program_ShouldRegisterDataCompletionPolicyService()
    {
        var source = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<DataCompletionPolicyService>", source);
    }

    [Fact]
    public void AdminController_ShouldUseProductScopedRequirementsInsteadOfGlobalAdminSave()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("DataCompletionPolicyService", source);
        Assert.Contains("GetPolicyForPassportAsync", source);
        Assert.Contains("GetProductPolicyAsync", source);
        Assert.DoesNotContain("[HttpPost(\"data-requirements/save\")]", source);
        Assert.DoesNotContain("SaveDataRequirements", source);
    }

    [Fact]
    public void AdminWorkspace_ShouldNotRenderGlobalDataRequirementsTab()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "AdminClusterViewModel.cs"));
        var adminClusterModel = model[..model.IndexOf("public sealed class ProductTemplateSummaryViewModel", StringComparison.Ordinal)];

        Assert.Contains("Battery families", markup);
        Assert.Contains("tab=products", markup);
        Assert.Contains("Required fields", markup);
        Assert.DoesNotContain("tab=data-requirements", markup);
        Assert.DoesNotContain("selectedTab == \"data-requirements\"", markup);
        Assert.DoesNotContain("Save data requirements", markup);
        Assert.DoesNotContain("Model.DataRequirements", markup);

        Assert.DoesNotContain("DataRequirements", adminClusterModel);
    }

    [Fact]
    public void ProductTemplateEditor_ShouldRemainRequirementSourceOfTruth()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));

        Assert.Contains("Template completion policy", markup);
        Assert.Contains("Required and optional parameters", markup);
        Assert.Contains("name=\"requiredFieldKeys\"", markup);
        Assert.Contains("Battery families tab", File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml")));
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
