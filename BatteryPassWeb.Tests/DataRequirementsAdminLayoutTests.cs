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
    public void AdminController_ShouldLoadAndSaveDataRequirementsPolicy()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("DataCompletionPolicyService", source);
        Assert.Contains("DataRequirements = dataRequirements", source);
        Assert.Contains("[HttpPost(\"data-requirements/save\")]", source);
        Assert.Contains("SavePolicyAsync", source);
    }

    [Fact]
    public void AdminWorkspace_ShouldRenderDataRequirementsTabBeforeHelp()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "AdminClusterViewModel.cs"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Data requirements", markup);
        Assert.Contains("tab=data-requirements", markup);
        Assert.Contains("selectedTab == \"data-requirements\"", markup);
        Assert.True(markup.IndexOf("Data requirements", StringComparison.Ordinal) < markup.IndexOf("Help", StringComparison.Ordinal));
        Assert.Contains("name=\"requiredFieldKeys\"", markup);
        Assert.Contains("Model.DataRequirements", markup);

        Assert.Contains("DataRequirements", model);
        Assert.Contains(".bp-data-requirements-shell", css);
        Assert.Contains(".bp-requirement-switch", css);
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
