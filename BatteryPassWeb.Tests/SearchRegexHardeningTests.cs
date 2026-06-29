using System.Reflection;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class SearchRegexHardeningTests
{
    [Fact]
    public void SearchRegexBuilder_ShouldEscapeRegexSyntaxAndCapInputLength()
    {
        var helperType = typeof(BatteryRepository).Assembly.GetType("BatteryPassWeb.Services.SearchRegexBuilder");
        Assert.NotNull(helperType);

        var normalize = helperType!.GetMethod(
            "NormalizeLiteralSearchPattern",
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(normalize);

        var escaped = Assert.IsType<string>(normalize!.Invoke(null, ["  .*demo+(pack)?  "]));
        Assert.Equal(@"\.\*demo\+\(pack\)\?", escaped);

        var capped = Assert.IsType<string>(normalize.Invoke(null, [new string('a', 160)]));
        Assert.Equal(128, capped.Length);
    }

    [Fact]
    public void UserSearchRepositories_ShouldUseLiteralSearchRegexHelper()
    {
        var passportRepository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
        var batteryRepository = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));
        var combined = passportRepository + batteryRepository;

        Assert.Contains("SearchRegexBuilder.CreateLiteralContainsRegex(query)", passportRepository);
        Assert.Contains("SearchRegexBuilder.CreateLiteralContainsRegex(query)", batteryRepository);
        Assert.DoesNotContain("new BsonRegularExpression(query.Trim()", combined);
        Assert.Contains("Regex.Escape(serialNumber.Trim())", batteryRepository);
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
