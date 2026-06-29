using System.Xml.Linq;

namespace BatteryPassWeb.Tests;

public sealed class DependencySecurityTests
{
    [Fact]
    public void MongoDriver_ShouldUseVersionThatDoesNotResolveVulnerableSharpCompress()
    {
        var project = XDocument.Load(RepoFile("web", "BatteryPassWeb.csproj"));
        var mongoDriverReference = project
            .Descendants("PackageReference")
            .SingleOrDefault(element => string.Equals(
                element.Attribute("Include")?.Value,
                "MongoDB.Driver",
                StringComparison.Ordinal));

        Assert.NotNull(mongoDriverReference);

        var versionValue = mongoDriverReference.Attribute("Version")?.Value;
        Assert.True(
            Version.TryParse(versionValue, out var version),
            $"MongoDB.Driver package version must be a concrete version, but found '{versionValue}'.");

        Assert.True(
            version >= new Version(3, 9, 0),
            $"MongoDB.Driver {versionValue} resolves vulnerable SharpCompress 0.30.1; use MongoDB.Driver 3.9.0 or newer.");
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
