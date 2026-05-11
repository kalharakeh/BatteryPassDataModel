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
    }

    [Fact]
    public void PassportSummary_ShouldIncludeSoftwareVersion()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));

        Assert.Contains("Software version", summary);
        Assert.Contains("passport.SoftwareVersion", summary);
    }

    [Fact]
    public void PassportDetail_ShouldRenderDedicatedSoftwareTab()
    {
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("data-bs-target=\"#tab-software\"", detail);
        Assert.Contains("id=\"tab-software\"", detail);
        Assert.Contains("passport.ProductName", detail);
        Assert.Contains("passport.ProductId", detail);
        Assert.Contains("passport.SoftwareVersion", detail);
        Assert.Contains("passport.SoftwareReleaseDate", detail);
        Assert.Contains("passport.SoftwareLatestUpdate", detail);
        Assert.Contains("bp-software-panel", detail);
        Assert.Contains(".bp-software-panel", css);
    }

    [Fact]
    public void PassportDetailTabs_ShouldStayOnSingleScrollableRow()
    {
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("class=\"nav nav-tabs bp-tabs\"", detail);
        Assert.Contains("flex-wrap: nowrap;", css);
        Assert.Contains("overflow-x: auto;", css);
        Assert.Contains(".bp-tabs .nav-item", css);
        Assert.Contains("flex: 0 0 auto;", css);
        Assert.Contains("white-space: nowrap;", css);
    }

    [Fact]
    public void PassportDetailSoftwareTab_ShouldRenderCurrentVersionAsCompactField()
    {
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

        Assert.Contains("<div><dt>Current version</dt><dd>@(string.IsNullOrWhiteSpace(passport.SoftwareVersion) ? \"-\" : passport.SoftwareVersion)</dd></div>", detail);
        Assert.DoesNotContain("bp-software-version-card", detail);
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
