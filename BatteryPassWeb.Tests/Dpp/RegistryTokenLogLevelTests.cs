using System.Text.Json.Nodes;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (decision 5): the framework's own request line ("Request starting … ?access_token=…") would write a token sent in
/// the query into the log. The app keeps Microsoft.AspNetCore at Warning in every environment, so it never does; this pins it.
/// A proxy's access log in front of the app may still hold such a URL (the runbook says so).
/// </summary>
public sealed class RegistryTokenLogLevelTests
{
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void TheFrameworksRequestLines_ShouldStayOutOfTheLog(string file)
    {
        var settings = JsonNode.Parse(File.ReadAllText(DppTestFiles.RepoFile("web", file)))!;

        var level = settings["Logging"]?["LogLevel"]?["Microsoft.AspNetCore"]?.GetValue<string>();

        Assert.Contains(level, new[] { "Warning", "Error", "Critical", "None" });
    }
}
