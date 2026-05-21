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
        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        var combined = string.Join(Environment.NewLine, registry, batteryTable, summary, detail);
        Assert.Contains("bp-report-action", combined);
        Assert.Contains("data-tooltip=\"Summary report\"", batteryTable);
        Assert.Contains("data-tooltip=\"Detailed report\"", batteryTable);
        Assert.Contains("View more about this passport", summary);
        Assert.Contains("Back to summary", detail);
        Assert.Contains("bp-report-action-icon", combined);
        Assert.DoesNotContain("<span aria-hidden=\"true\">S</span>", batteryTable);
        Assert.DoesNotContain("<span aria-hidden=\"true\">D</span>", batteryTable);
        Assert.Contains(".bp-report-action::after", css);
        Assert.Contains("top: calc(-100% -", css);
    }

    [Fact]
    public void ReportActions_ShouldUseHeroCtaWithoutOldHeaderRailLabels()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-report-header", summary);
        Assert.Contains("bp-report-header", detail);
        Assert.Contains("bp-report-title-block", summary);
        Assert.Contains("bp-report-title-block", detail);
        Assert.Contains("bp-report-hero-cta", summary);
        Assert.Contains("bp-report-hero-cta", detail);
        Assert.Contains("View more about this passport", summary);
        Assert.Contains("Back to summary", detail);
        Assert.DoesNotContain("bp-report-header-actions", summary);
        Assert.DoesNotContain("bp-report-header-actions", detail);
        Assert.DoesNotContain("bp-report-action-strip", summary);
        Assert.DoesNotContain("bp-report-action-strip", detail);
        Assert.DoesNotContain("bp-report-action-label", summary);
        Assert.DoesNotContain("bp-report-action-label", detail);
        Assert.DoesNotContain(">Latest</span>", summary);
        Assert.DoesNotContain(">Latest</span>", detail);
        Assert.DoesNotContain(">Detailed report</span>", summary);
        Assert.DoesNotContain(">Summary report</span>", detail);
        Assert.Contains("bp-latest-passport-action-icon", summary);
        Assert.Contains("bp-latest-passport-action-icon", detail);
        Assert.Contains(".bp-report-title-block", css);
        Assert.Contains(".bp-report-hero-cta", css);
        Assert.Contains("font-weight: 400;", css);
        Assert.DoesNotContain(".bp-report-action-label", css);
    }

    [Fact]
    public void PassportReportHero_ShouldUseApprovedSplitHeaderDesign()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-report-hero-card", summary);
        Assert.Contains("bp-report-hero-card", detail);
        Assert.Contains("bp-report-wide-section", summary);
        Assert.Contains("bp-report-wide-section", detail);
        Assert.Contains("bp-report-hero-layout", summary);
        Assert.Contains("bp-report-hero-layout", detail);
        Assert.Contains("bp-report-hero-media", summary);
        Assert.Contains("bp-report-hero-media", detail);
        Assert.Contains("View more about this passport", summary);
        Assert.Contains("Back to summary", detail);
        Assert.Contains("bp-report-hero-qr", summary);
        Assert.Contains("bp-report-hero-qr", detail);
        Assert.DoesNotContain("QR code for battery ID", summary);
        Assert.Contains(".bp-report-hero-card", css);
        Assert.Contains(".bp-report-wide-section", css);
        Assert.Contains("width: min(1860px, calc(100vw - 96px));", css);
        Assert.Contains("grid-template-columns: repeat(4, minmax(120px, 1fr));", css);
        Assert.Contains(".bp-report-hero-media .bp-image-wrap", css);
        Assert.Contains("background: transparent;", css);
        Assert.Contains(".bp-report-hero-cta", css);
        Assert.Contains("font-weight: 400;", css);
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
        Assert.Contains("bp-cluster-management-table", clusters);
        Assert.Contains("bp-user-management-table", clusters);
        Assert.Contains("bp-user-management-table", clusterUsers);
        Assert.DoesNotContain("class=\"bp-tag\"", clusters);
        Assert.DoesNotContain("class=\"bp-tag\"", clusterUsers);
        Assert.Contains("bp-token-console", clusters);
        Assert.Contains("bp-token-cluster-chip-picker", clusters);
        Assert.Contains("bp-token-cluster-chip-picker", clusterTokens);
        Assert.Contains(".bp-confirm-modal", css);
        Assert.Contains(".bp-token-cluster-chip-picker", css);
    }

    [Fact]
    public void RegistryController_ShouldPreserveBatteryIdentityFieldsForNormalUsers()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
        var service = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));

        Assert.Contains("BatterySummaryViewModel", source);
        Assert.Contains("_batteryRepository.ToSummary", service);
        Assert.Contains("BatteryPassportHistoryRowViewModel", source);
        Assert.Contains("LatestPassportStatus", batteryTable);
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

    [Fact]
    public void ReportCharts_ShouldUseTableLegendsAndOriginalPowerHeading()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

        Assert.Contains("<h3 class=\"bp-subheading\">Original Power</h3>", summary);
        Assert.Contains("bp-chart-value-legend", summary);
        Assert.Contains("bp-chart-value-legend", detail);
        Assert.Contains("filter(row => Number.isFinite", detail);
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
