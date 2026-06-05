using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests;

public sealed class MobileThemeSessionLayoutTests
{
    [Fact]
    public void MobileCss_ShouldAddMobileOnlyNavigationTouchAndTableGuardrails()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
        var sharedBatteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));

        Assert.Contains("@media (max-width: 767px)", css);
        Assert.Contains(".bp-header-nav-row::after", css);
        Assert.Contains("scroll-snap-type: x proximity;", css);
        Assert.Contains(".bp-tab-row", css);
        Assert.Contains(".bp-table-scroll::after", css);
        Assert.Contains(".bp-mobile-card-table", css);
        Assert.Contains("min-height: 44px;", css);
        Assert.Contains(".bp-id-copy-row", css);
        Assert.Contains("grid-template-columns: minmax(0, 1fr) 44px;", css);

        Assert.Contains("bp-mobile-card-table", sharedBatteryTable);
        Assert.Contains("data-label=\"Battery ID\"", sharedBatteryTable);
        Assert.Contains("data-label=\"Actions\"", sharedBatteryTable);
    }

    [Fact]
    public void ThemeSwitch_ShouldBeAvailableFromSharedLayoutAndPreserveLightDefaults()
    {
        var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
        var controllerPath = RepoPath("web", "Controllers", "ThemeController.cs");

        Assert.Contains("data-theme=\"@theme\"", layout);
        Assert.Contains("action=\"/theme\"", layout);
        Assert.Contains("name=\"theme\"", layout);
        Assert.Contains("bp-theme-toggle", layout);
        Assert.True(File.Exists(controllerPath));

        Assert.Contains("--bp-bg: #f6f7f9;", css);
        Assert.Contains("--bp-surface: #ffffff;", css);
        Assert.Contains("--bp-primary: #2a6ecf;", css);
        Assert.Contains("[data-theme=\"dark\"]", css);
        Assert.Contains("--bp-bg: #07111f;", css);
        Assert.Contains("--bp-surface: #0d1b2f;", css);
    }

    [Fact]
    public void DarkTheme_ShouldCoverPageSpecificHelpPassportAndTokenSurfaces()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("[data-theme=\"dark\"] .bp-api-help-meta-strip", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-api-help-doc-heading", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-api-help-selected-panel", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-help-detail-panel h3", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-admin-help-reference-toolbar", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-admin-help-detail-card", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-summary-id-panel", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-summary-fields-panel", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-token-clusters", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-ledger-panel", css);
    }

    [Fact]
    public void DarkTheme_ShouldCoverAdminPolicyFormsAndProductEditorControls()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("[data-theme=\"dark\"] .bp-report-action", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-cluster-create-form input", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-cluster-rename-form .form-control", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-user-create-strip input", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-user-create-strip label", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-user-count-pill", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-local-editable-field-card", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-local-editable-toggle-shell", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-requirement-switch", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-requirement-toggle", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-product-editor-section .bp-field-shell input", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-product-editor-section .bp-field-shell textarea", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-product-version-selected-field input", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-product-version-tab", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-template-version-row", css);
        Assert.Contains("[data-theme=\"dark\"] .bp-template-document-row", css);
    }

    [Fact]
    public void LoginReturnUrl_ShouldUseFrameworkReturnUrlButRejectApiDestinations()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
        var apiController = File.ReadAllText(RepoFile("web", "Controllers", "AuthApiController.cs"));

        Assert.Contains("Index([FromQuery] string? next, [FromQuery] string? returnUrl, [FromQuery] bool switchAccount)", controller);
        Assert.Contains("&& !switchAccount", controller);
        Assert.Contains("SafeInteractiveReturnUrl", controller);
        Assert.Contains("StartsWith(\"/api/\"", controller);
        Assert.Contains("SafeInteractiveReturnUrl(model.ReturnUrl)", controller);
        Assert.Contains("SafeInteractiveReturnUrl(next)", apiController);
    }

    [Fact]
    public async Task SessionSettings_ShouldDefaultToThreeHourInactivityTimeout()
    {
        var options = Options.Create(new BatteryPassOptions());
        var service = new ApplicationSettingsService(new MongoContext(options), options);

        Assert.Equal(180, options.Value.SessionTimeoutMinutes);
        Assert.Equal(TimeSpan.FromHours(3), await service.GetSessionTimeoutAsync());
    }

    [Fact]
    public void GlobalAdminUsersPage_ShouldExposeGlobalSessionTimeoutControl()
    {
        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("Global session timeout", view);
        Assert.Contains("name=\"sessionTimeoutMinutes\"", view);
        Assert.Contains("/admin/clusters/session-timeout", view);
        Assert.Contains("[HttpPost(\"clusters/session-timeout\")]", controller);
        Assert.Contains("ApplicationSettingsService", program);
        Assert.Contains("ExpireTimeSpan", program);
        Assert.Contains("SlidingExpiration = true", program);
    }

    private static string RepoPath(params string[] parts)
    {
        var root = Path.GetDirectoryName(RepoFile("web", "Program.cs"))!;
        return Path.Combine(new[] { Directory.GetParent(root)!.FullName }.Concat(parts).ToArray());
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
