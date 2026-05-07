namespace BatteryPassWeb.Tests;

public sealed class Phase6ADocumentationTests
{
    [Fact]
    public void AdminController_ShouldExposeAdminOnlyDemoScenarioReset()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("DemoScenarioResetService", source);
        Assert.Contains("[HttpPost(\"demo-scenarios/reset\")]", source);
        Assert.Contains("ResetDemoScenarios", source);
        Assert.Contains("_demoScenarioResetService.ResetAllAsync", source);
        Assert.Contains("Demo scenarios reset", source);
    }

    [Fact]
    public void AdminHelp_ShouldShowCompactPhase6ADemoResetAction()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Phase 6A demo reset", markup);
        Assert.Contains("/admin/demo-scenarios/reset", markup);
        Assert.Contains("bp-admin-help-demo-reset", markup);
        Assert.Contains(".bp-admin-help-demo-reset", css);
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
