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
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var combined = summary + Environment.NewLine + detail;

        Assert.Contains("Passport ID:", summary);
        Assert.Contains("Battery ID:", summary);
        Assert.Contains("Passport ID:", detail);
        Assert.Contains("Battery ID:", detail);
        Assert.True(CountOccurrences(combined, "_CopyIdButton") >= 4);
        Assert.True(CountOccurrences(combined, "bp-id-copy-row") >= 4);
    }

    [Fact]
    public void AdminAndRegistryIdSurfaces_ShouldRenderInlineCopyButtons()
    {
        var files = new[]
        {
            RepoFile("web", "Views", "Registry", "Index.cshtml"),
            RepoFile("web", "Views", "Passport", "Battery.cshtml"),
            RepoFile("web", "Views", "Admin", "Clusters.cshtml"),
            RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"),
            RepoFile("web", "Views", "Admin", "Conformance.cshtml"),
            RepoFile("web", "Views", "Admin", "Audit.cshtml"),
            RepoFile("web", "Views", "Admin", "Revisions.cshtml"),
            RepoFile("web", "Views", "Admin", "EditPassport.cshtml"),
            RepoFile("web", "Views", "Admin", "Passports.cshtml"),
            RepoFile("web", "Views", "ClusterAdmin", "Passports.cshtml"),
            RepoFile("web", "Views", "ClusterAdmin", "EditPassport.cshtml"),
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
