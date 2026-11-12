namespace BatteryPassWeb.Tests;

public sealed class CopyIdButtonLayoutTests
{
    [Fact]
    public void SharedCopyButton_ShouldUseIconOnlyClipboardControl()
    {
        var partial = File.ReadAllText(RepoFile("web", "Views", "Shared", "_CopyIdButton.cshtml"));
        var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("data-copy-id-button", partial);
        Assert.Contains("data-copy-value", partial);
        Assert.Contains("aria-label=\"Copy ID\"", partial);
        Assert.Contains("bp-copy-id-icon", partial);
        Assert.DoesNotContain(">Copy<", partial);

        Assert.Contains("data-copy-id-button", layout);
        Assert.Contains("navigator.clipboard.writeText", layout);
        Assert.Contains("data-copy-value", layout);
        Assert.Contains("is-copied", layout);

        Assert.Contains(".bp-id-copy-row", css);
        Assert.Contains(".bp-id-value", css);
        Assert.Contains(".bp-copy-id-button", css);
        Assert.Contains(".bp-copy-id-button::after", css);
    }

    [Fact]
    public void PassportReports_ShouldRenderCopyButtonsForPassportAndBatteryIds()
    {
        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.Contains("Passport ID:", hero);
        Assert.Contains("Battery ID:", hero);
        Assert.True(CountOccurrences(hero, "_CopyIdButton") >= 2);
        Assert.True(CountOccurrences(hero, "bp-id-copy-row") >= 2);
    }

    [Fact]
    public void AdminAndRegistryIdSurfaces_ShouldRenderInlineCopyButtons()
    {
        var files = new[]
        {
            RepoFile("web", "Views", "Shared", "_ApiTokenList.cshtml"),
            RepoFile("web", "Views", "Help", "Index.cshtml")
        };

        foreach (var file in files)
        {
            var markup = File.ReadAllText(file);
            Assert.Contains("_CopyIdButton", markup);
            Assert.Contains("bp-id-copy-row", markup);
        }
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
