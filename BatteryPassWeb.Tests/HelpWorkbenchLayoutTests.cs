namespace BatteryPassWeb.Tests;

public sealed class HelpWorkbenchLayoutTests
{
    [Fact]
    public void HelpPage_ShouldPlaceRequestWorkbenchAfterFullWidthDocumentation()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        var documentationIndex = markup.IndexOf("API documentation", StringComparison.Ordinal);
        var workbenchIndex = markup.IndexOf("Request workbench", StringComparison.Ordinal);

        Assert.True(documentationIndex >= 0, "Documentation heading should exist.");
        Assert.True(workbenchIndex > documentationIndex, "Request Workbench should be rendered under Documentation.");
        Assert.Contains("bp-help-stack", markup);
    }

    [Fact]
    public void HelpPage_ShouldUseDenseConsoleHelpShell()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-api-help-page", markup);
        Assert.Contains("_HelpContents", markup);
        Assert.Contains("bp-console-toolbar", markup);
        Assert.Contains("bp-api-help-meta-strip", markup);
        Assert.Contains("bp-api-help-section-card", markup);
        Assert.Contains("bp-api-help-doc-heading", markup);
        Assert.Contains("bp-api-help-reference-console", markup);
        Assert.Contains("bp-api-help-endpoint-table", markup);
        Assert.Contains("<th>Method</th>", markup);
        Assert.Contains("<th>Endpoint</th>", markup);
        Assert.Contains("<th>Access</th>", markup);
        Assert.Contains("<th>Use when</th>", markup);
        Assert.Contains("bp-api-help-detail-row", markup);
        Assert.Contains("data-api-help-detail-row", markup);
        Assert.Contains("data-api-help-detail-target", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
        Assert.Contains("parentNode.insertBefore", markup);
        Assert.DoesNotContain("scrollIntoView", markup);

        Assert.Contains(".bp-api-help-page", css);
        Assert.Contains(".bp-api-help-meta-strip", css);
        Assert.Contains(".bp-api-help-section-card", css);
        Assert.Contains(".bp-api-help-doc-heading", css);
        Assert.Contains(".bp-api-help-reference-console", css);
        Assert.Contains(".bp-api-help-endpoint-table", css);
        Assert.Contains(".bp-api-help-detail-row", css);
    }

    [Fact]
    public void HelpWorkbench_ShouldExposeEditableConstructedHttpRequest()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("id=\"tester-raw-request\"", markup);
        Assert.Contains("buildRawRequest", markup);
        Assert.Contains("parseRawRequest", markup);
        Assert.Contains("requestSource: 'raw'", markup);
        Assert.Contains("templateSelect?.addEventListener('change'", markup);
        Assert.DoesNotContain("batterySecretEl", markup);
    }

    [Fact]
    public void HelpWorkbench_ShouldKeepIdentityAndTokenFieldsStableAcrossTemplateChanges()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("id=\"tester-battery-id\"", markup);
        Assert.DoesNotContain("id=\"tester-passport-id\"", markup);
        Assert.Contains("id=\"tester-token\"", markup);
        Assert.Contains("const getWorkbenchContext", markup);
        Assert.Contains("resolveTemplatePath(template.pathTemplate", markup);
        Assert.Contains("batteryIdEl?.addEventListener", markup);
        Assert.DoesNotContain("tester-token-preset", markup);
        Assert.Contains("Paste a token from API Token Management.", markup);
    }

    [Fact]
    public void HelpWorkbench_ShouldShowStickyEditableIdsWithCurrentValueCopyButtons()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
        var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
        var workbenchIndex = markup.IndexOf("Request workbench", StringComparison.Ordinal);

        Assert.True(workbenchIndex >= 0, "Request workbench should exist.");
        var workbenchMarkup = markup[workbenchIndex..];

        Assert.Contains("bp-help-sticky-id-field", workbenchMarkup);
        Assert.Contains("bp-help-sticky-id-control", workbenchMarkup);
        Assert.Contains("id=\"tester-battery-id\"", workbenchMarkup);
        Assert.DoesNotContain("id=\"tester-battery-id\" class=\"bp-id-value\" value=\"@Model.ExampleBatteryId\" readonly", workbenchMarkup);
        Assert.Contains("data-copy-target=\"#tester-battery-id\"", workbenchMarkup);
        Assert.Contains("Copy Battery ID", workbenchMarkup);

        Assert.Contains(".bp-help-sticky-id-field", css);
        Assert.Contains(".bp-help-sticky-id-control", css);
        Assert.Contains(".bp-help-sticky-id-control input {", css);
        Assert.DoesNotContain(".bp-help-sticky-id-control input[readonly]", css);
        Assert.Contains(".bp-help-sticky-id-control .bp-copy-id-button", css);

        Assert.Contains("const targetSelector = button.getAttribute('data-copy-target')", layout);
        Assert.Contains("target.value", layout);
    }

    [Fact]
    public void HelpPage_ShouldDisplayRuntimePublicApiUrl()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));
        var helpModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ExternalApiHelpViewModel.cs"));

        Assert.Contains("PublicBaseUrl", helpModel);
        Assert.Contains("RequestBaseUrl()", controller);
        Assert.Contains("Current API URL", markup);
        Assert.Contains("@Model.PublicBaseUrl@Model.BasePath", markup);
        Assert.Contains("resolveAbsoluteUrl", markup);
        Assert.Contains("window.location.origin", markup);
    }

    [Fact]
    public void HelpController_ShouldAllowAnonymousReadOnlyWorkbench()
    {
        var action = typeof(BatteryPassWeb.Controllers.HelpController).GetMethod(nameof(BatteryPassWeb.Controllers.HelpController.Index));

        Assert.NotNull(action);
        Assert.Contains(
            action!.GetCustomAttributes(inherit: true),
            attribute => attribute is Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute);
    }

    [Fact]
    public void HelpPage_ShouldOfferAPublishedBatteryForTheCalls()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));

        Assert.Contains("Example battery ID", markup);
        Assert.Contains("No published demo battery to show.", markup);
        Assert.Contains("id=\"tester-battery-id\" class=\"bp-id-value\" value=\"@Model.ExampleBatteryId\"", markup);
        Assert.DoesNotContain("SampleBatteryId", markup);
        Assert.DoesNotContain("SampleBatteryId", controller);
        Assert.Contains("ExampleBatteryIdAsync(cancellationToken)", controller);
        Assert.Contains("BatteryPassWeb.Dpp.Api.PassportApi.Sections", markup);
        Assert.DoesNotContain("<code>materialComposition</code>", markup);
    }

    [Fact]
    public void HelpWorkbenchValuesTemplate_ShouldTargetValuesEndpoint()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("/values?path=stateOfChargeSoC&path=%2311&path=batteryMass", markup);
        Assert.Contains("Values read successfully", markup);
    }

    [Fact]
    public void HelpWorkbench_ShouldExposeRunnableTemplateForEveryExternalApiEndpoint()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));

        var documentedRouteFragments = System.Text.RegularExpressions.Regex
            .Matches(controller, "\\[Http(?:Get|Post|Patch)\\(\"([^\"]+)\"\\)\\]")
            .Select(match => $"@Model.BasePath/{match.Groups[1].Value}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var route in documentedRouteFragments)
        {
            Assert.Contains(route, markup);
        }

        var workbenchTemplateKeys = new[]
        {
            "readFull",
            "listClusters",
            "listClusterBatteries",
            "readBatteryPassports",
            "readSection",
            "readValuesAliases",
            "readPathsAll",
            "writeLiveValues",
            "previewLiveValues",
            "readLiveHistory24h",
            "sendUpload",
            "previewUpload",
            "listBatches",
            "listOperators",
            "createBattery",
            "publishBattery"
        };
        foreach (var templateKey in workbenchTemplateKeys)
        {
            Assert.Contains($"value=\"{templateKey}\"", markup);
            Assert.Contains($"{templateKey}: {{", markup);
        }

    }

    [Fact]
    public void HelpWorkbench_ShouldGroupTheCallsBuildLiveValuesAndAnswerInPlainWords()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "bp-api-help.css"));
        var workbench = markup[markup.IndexOf("Request workbench", StringComparison.Ordinal)..];

        Assert.Contains("<link rel=\"stylesheet\" href=\"~/css/bp-api-help.css\"", markup);
        Assert.Contains("<optgroup label=\"Reading\">", workbench);
        Assert.Contains("<optgroup label=\"Live values\">", workbench);
        Assert.Contains("<optgroup label=\"Team token: create and publish\">", workbench);
        Assert.Contains("id=\"tester-method-tag\"", workbench);
        Assert.Contains("@foreach (var live in BatteryPassWeb.Dpp.Api.BatteryApi.LiveSet)", workbench);
        Assert.Contains("data-live-key=\"@live.Property\"", workbench);
        Assert.Contains("<span class=\"bp-wb-unit\">@live.Unit</span>", workbench);
        Assert.Contains("id=\"tester-measured-now\"", workbench);
        Assert.Contains("const renderAnswer", workbench);
        Assert.Contains("<summary>Show JSON</summary>", workbench);
        Assert.Contains("<summary>Show request</summary>", workbench);
        Assert.True(
            workbench.IndexOf("<summary>Show request</summary>", StringComparison.Ordinal) < workbench.IndexOf("id=\"tester-auth-format\"", StringComparison.Ordinal),
            "The auth format belongs under Show request.");
        Assert.DoesNotContain("template-shortcut", workbench);
        Assert.DoesNotContain("tester-load-template", workbench);
        Assert.DoesNotContain(">s</span>", workbench);
        Assert.Contains(".bp-wb-tag.is-done", css);
    }

    [Fact]
    public void HelpDocumentation_ShouldExplainExternalApiHttpCodes()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("200 OK", markup);
        Assert.Contains("201 Created", markup);
        Assert.Contains("400 Bad Request", markup);
        Assert.Contains("401 Unauthorized", markup);
        Assert.Contains("403 Forbidden", markup);
        Assert.Contains("404 Not Found", markup);
        Assert.Contains("500 Server Error", markup);
        Assert.Contains("503 Service Unavailable", markup);
    }

    [Fact]
    public void HelpDocumentation_ShouldExposeReadablePathsNewTabLink()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("id=\"supported-values-link\"", markup);
        Assert.Contains("target=\"_blank\"", markup);
        Assert.Contains("/batteries/{Model.ExampleBatteryId}/paths", markup);
        Assert.Contains("openSupportedValues", markup);
    }

    [Fact]
    public void HelpDocumentation_ShouldShowHowPathsAreReusedWithValues()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("its attribute number (<code>%2363</code> for #63", markup);
        Assert.Contains("/values?path=stateOfChargeSoC&amp;path=%2311", markup);
        Assert.Contains("/paths?section=performanceAndDurability", markup);
    }

    [Fact]
    public void HelpCss_ShouldKeepDocumentationAndWorkbenchFullWidth()
    {
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains(".bp-help-stack", css);
        Assert.DoesNotContain(".bp-help-layout {\r\n    grid-template-columns", css);
        Assert.DoesNotContain(".bp-help-tester-card {\r\n    position: sticky", css);
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
