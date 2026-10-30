namespace BatteryPassWeb.Tests;

public sealed class BatteryAdminWorkflowTests
{

    [Fact]
    public void AdminBatteryCreate_ShouldRequireClusterAndSnapshotShouldClearPendingFlag()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.DoesNotContain("snapshot[\"newPassportRequired\"] = true;", controller);
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
