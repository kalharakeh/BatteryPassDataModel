using System.Reflection;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class ExternalApiInitializerTests
{
    [Fact]
    public void EnsureSamplePassport_ShouldNotCloneSeededPassports()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));

        Assert.Contains("if (existing == null)", source);
        Assert.Contains("return;", source);
        Assert.DoesNotContain("candidates.FirstOrDefault()?.DeepClone().AsBsonDocument", source);
    }

    [Fact]
    public void BuildFallbackSampleDocument_ShouldUseScaniaManufacturer()
    {
        var method = typeof(ExternalApiInitializer).GetMethod(
            "BuildFallbackSampleDocument",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var document = Assert.IsType<BsonDocument>(method.Invoke(null, []));

        Assert.Equal(
            "Scania Industrial Batteries",
            document["app"]["display"]["manufacturerName"].AsString);
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
