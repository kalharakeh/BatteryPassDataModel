namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Finds files in the repository from the test output folder (the house RepoFile pattern).</summary>
internal static class DppTestFiles
{
    public static string RepoFile(params string[] parts)
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

    /// <summary>The cards the team's battery page shares with the cluster admin's (step 4a), one partial view each.</summary>
    public static readonly string[] BatteryCardViews = ["_LiveValuesCard", "_ModulesCard", "_AccidentsCard", "_VersionsCard"];

    /// <summary>The team's battery page as it is drawn: Battery.cshtml and the shared card partials it renders.</summary>
    public static string BatteryPageSource() => string.Join(
        Environment.NewLine,
        new[] { "Battery" }.Concat(BatteryCardViews).Select(name => File.ReadAllText(RepoFile("web", "Views", "Batteries", $"{name}.cshtml"))));
}
