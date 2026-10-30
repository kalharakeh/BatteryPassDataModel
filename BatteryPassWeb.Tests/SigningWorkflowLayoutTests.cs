namespace BatteryPassWeb.Tests;

public sealed class SigningWorkflowLayoutTests
{

    [Fact]
    public void PassportsApiController_ShouldExposeSignPublishAndPublicVerifyRoutes()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));

        Assert.DoesNotContain("HttpPost", source);
        Assert.Contains("[AllowAnonymous]", source);
        Assert.Contains("[HttpGet(\"{id}/verify\")]", source);
        Assert.Contains("PassportReader.Verify(version)", source);
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
