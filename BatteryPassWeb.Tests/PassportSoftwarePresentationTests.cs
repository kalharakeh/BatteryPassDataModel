using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportSoftwarePresentationTests
{
    [Fact]
    public void PassportViewModelFactory_ShouldExposeCurrentSoftwareReleaseMetadata()
    {
        var document = new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:test-software-001",
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["modelNumber"] = "M-SW-001",
                    ["serialNumber"] = "SN-SW-001"
                },
                ["product"] = new BsonDocument
                {
                    ["productId"] = "compact-7m",
                    ["productName"] = "Compact 7M",
                    ["productVersion"] = "1.0",
                    ["softwareVersion"] = "1.0",
                    ["softwareReleaseDate"] = "2025-09-30",
                    ["softwareLatestUpdate"] = "2026-01-18"
                },
                ["operations"] = new BsonDocument
                {
                    ["softwareVersion"] = "2.0",
                    ["softwareReleaseDate"] = "2026-02-01",
                    ["softwareLatestUpdate"] = "2026-04-15"
                }
            }
        };

        var passport = new PassportViewModelFactory().Create(document);

        Assert.Equal("2.0", passport.SoftwareVersion);
        Assert.Equal("2026-02-01", passport.SoftwareReleaseDate);
        Assert.Equal("2026-04-15", passport.SoftwareLatestUpdate);
        Assert.Equal("Compact 7M", passport.ProductName);
        Assert.Equal("1.0", passport.ProductVersion);
    }

    [Fact]
    public void PassportDetailTabs_ShouldStayOnSingleScrollableRow()
    {
        var tabs = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("class=\"nav nav-tabs bp-tabs\"", tabs);
        Assert.Contains("flex-wrap: nowrap;", css);
        Assert.Contains("overflow-x: auto;", css);
        Assert.Contains(".bp-tabs .nav-item", css);
        Assert.Contains("flex: 0 0 auto;", css);
        Assert.Contains("white-space: nowrap;", css);
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
