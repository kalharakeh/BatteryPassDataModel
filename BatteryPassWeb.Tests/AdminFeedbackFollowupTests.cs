using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class AdminFeedbackFollowupTests
{
    [Fact]
    public void BatteryFamilyResetButtons_ShouldUseBatteryFamilyCopy()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));

        Assert.Contains("Reset battery-family passports", clusters);
        Assert.Contains("Reset battery-family passports", help);
        Assert.DoesNotContain("Reset template passports", clusters);
        Assert.DoesNotContain("Reset battery family passports", help);
    }

    [Fact]
    public void PassportReportHero_ShouldNotShowPassportStatusAbovePassportId()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

        Assert.DoesNotContain("<span class=\"bp-pill bp-pill-muted\">@passport.PassportStatus</span>", summary);
        Assert.DoesNotContain("<span class=\"bp-pill bp-pill-muted\">@passport.PassportStatus</span>", detail);
        Assert.Contains("<div><dt>Passport status</dt><dd>@passport.PassportStatus</dd></div>", summary);
        Assert.Contains("<div><dt>Passport status</dt><dd>@passport.PassportStatus</dd></div>", detail);
    }

    [Fact]
    public void ReportActions_ShouldUseConsistentIconButtonsWithDesignedTooltips()
    {
        var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        var combined = string.Join(Environment.NewLine, registry, summary, detail);
        Assert.Contains("bp-report-action", combined);
        Assert.Contains("data-tooltip=\"Summary report\"", registry);
        Assert.Contains("data-tooltip=\"Detailed report\"", registry);
        Assert.Contains("data-tooltip=\"Detailed report\"", summary);
        Assert.Contains("data-tooltip=\"Summary report\"", detail);
        Assert.Contains("bp-report-action-icon", combined);
        Assert.DoesNotContain("<span aria-hidden=\"true\">S</span>", registry);
        Assert.DoesNotContain("<span aria-hidden=\"true\">D</span>", registry);
        Assert.Contains(".bp-report-action::after", css);
        Assert.Contains("top: calc(-100% -", css);
    }

    [Fact]
    public void AdminManagementViews_ShouldUseDesignedConfirmationsAndStructuredLayouts()
    {
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
        var clusterTokens = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.DoesNotContain("confirm(", clusters);
        Assert.DoesNotContain("confirm(", clusterTokens);
        Assert.Contains("bp-confirm-modal", clusters);
        Assert.Contains("bp-confirm-modal", clusterTokens);
        Assert.Contains("data-confirm-action", clusters);
        Assert.Contains("data-confirm-action", clusterTokens);
        Assert.Contains("bp-cluster-management-list", clusters);
        Assert.Contains("bp-user-management-list", clusters);
        Assert.Contains("bp-user-management-list", clusterUsers);
        Assert.DoesNotContain("class=\"bp-tag\"", clusters);
        Assert.DoesNotContain("class=\"bp-tag\"", clusterUsers);
        Assert.Contains("bp-token-console", clusters);
        Assert.Contains("bp-cluster-picker", clusters);
        Assert.Contains("bp-cluster-picker", clusterTokens);
        Assert.Contains(".bp-confirm-modal", css);
        Assert.Contains(".bp-cluster-picker", css);
    }

    [Fact]
    public void RegistryController_ShouldPreserveBatteryIdentityFieldsForNormalUsers()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));

        Assert.Contains("BatteryFamily = passport.BatteryFamily", source);
        Assert.Contains("BatteryVersion = passport.BatteryVersion", source);
        Assert.Contains("BatterySerialNumber = passport.BatterySerialNumber", source);
        Assert.Contains("PassportStatus = passport.PassportStatus", source);
    }

    [Fact]
    public void RemovedNameAndModelNumber_ShouldNotBlockPassportValidationOrCreation()
    {
        var policy = DataCompletionPolicyService.CreateDefaultPolicy();
        var fields = policy.Sections.SelectMany(section => section.Fields).ToList();
        var document = new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:identity-without-model",
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = "registry-001",
                ["status"] = "draft"
            },
            ["app"] = new BsonDocument
            {
                ["display"] = new BsonDocument
                {
                    ["serialNumber"] = "SERIAL-1",
                    ["manufacturerName"] = "ACME Batteries",
                    ["facilityId"] = "FAC-1"
                },
                ["product"] = new BsonDocument
                {
                    ["productId"] = "compact-7m",
                    ["productVersion"] = "1.0",
                    ["softwareVersion"] = "2.0"
                }
            }
        };

        var summary = new PassportValidationService(new SchemaRegistryService(), new JsonSchemaValidationService())
            .Validate(document, policy);
        var issuePaths = summary.Sections.SelectMany(section => section.Issues).Select(issue => issue.Path).ToList();

        Assert.DoesNotContain(fields, field => field.FieldKey == "general.name");
        Assert.DoesNotContain(fields, field => field.FieldKey == "general.modelNumber");
        Assert.DoesNotContain("app.display.name", issuePaths);
        Assert.DoesNotContain("app.display.modelNumber", issuePaths);
    }

    [Fact]
    public void PublicSearchEmptyCopy_ShouldSayActivePublicPassport()
    {
        var home = File.ReadAllText(RepoFile("web", "Views", "Home", "Index.cshtml"));
        var search = File.ReadAllText(RepoFile("web", "Views", "Home", "Search.cshtml"));

        Assert.Contains("No active public battery passport was found", home);
        Assert.Contains("No active public battery passport was found", search);
        Assert.DoesNotContain("No published, verified battery passport ID was found", home);
        Assert.DoesNotContain("No published, verified battery passport ID was found", search);
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
