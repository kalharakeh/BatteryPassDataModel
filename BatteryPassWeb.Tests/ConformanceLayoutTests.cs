namespace BatteryPassWeb.Tests;

public sealed class ConformanceLayoutTests
{
    [Fact]
    public void PassportsApiController_ShouldExposeValidateEndpoint()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));

        Assert.Contains("[HttpPost(\"{passportId}/validate\")]", source);
        Assert.Contains("PassportValidationService", source);
        Assert.Contains("UpdateTrustValidationAsync", source);
    }

    [Fact]
    public void AdminController_ShouldExposeConformancePageAndValidateAction()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("[HttpGet(\"passports/{passportId}/conformance\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/complete-required-data\")]", source);
        Assert.Contains("DemoRequiredDataCompletionService", source);
        Assert.Contains("ConformanceViewModel", source);
    }

    [Fact]
    public void Program_ShouldRegisterRequiredDataCompletionService()
    {
        var source = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<DemoRequiredDataCompletionService>", source);
    }

    [Fact]
    public void ConformanceView_ShouldRenderTrustSummaryAndActions()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.Contains("Conformance", markup);
        Assert.Contains("Blocking errors", markup);
        Assert.Contains("Warnings", markup);
        Assert.Contains("Validate passport", markup);
        Assert.Contains("Latest validation", markup);
        Assert.Contains("What is stopping signing?", markup);
        Assert.Contains("Full-data requirements", markup);
        Assert.Contains("Completion checklist", markup);
        Assert.Contains("Required data by section", markup);
        Assert.Contains("Missing required data", markup);
        Assert.Contains("Invalid format", markup);
        Assert.Contains("Invalid value", markup);
        Assert.Contains("Business-rule data", markup);
        Assert.Contains("Complete required demo data", markup);
        Assert.Contains("bp-conformance-shell", markup);
        Assert.Contains("bp-issue-list", markup);
        Assert.Contains("bp-completion-checklist", markup);
        Assert.Contains("bp-completion-section", markup);
        Assert.Contains("bp-action-control", markup);
        Assert.Contains("showCompleteRequiredData", markup);
        Assert.Contains("showSignPassport", markup);
        Assert.Contains("showPublishPassport", markup);
    }

    [Fact]
    public void ConformanceWorkflow_ShouldHideUnavailableActionsInsteadOfRenderingBlockedButtons()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.DoesNotContain("Action blocked", markup);
        Assert.DoesNotContain("bp-action-block-message", markup);
        Assert.DoesNotContain("bp-button-blocked", markup);
        Assert.DoesNotContain("disabled=\"", markup);
        Assert.DoesNotContain("disabled=@", markup);
        Assert.DoesNotContain("Sign passport is disabled", markup);
        Assert.Contains("@if (showCompleteRequiredData)", markup);
        Assert.Contains("@if (showSignPassport)", markup);
        Assert.Contains("@if (showPublishPassport)", markup);
    }

    [Fact]
    public void ConformanceView_ShouldUseGuidedWorkflowAndCleanEvidenceLayout()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-conformance-main-grid", markup);
        Assert.Contains("bp-workflow-panel", markup);
        Assert.Contains("bp-next-step-card", markup);
        Assert.Contains("Next action", markup);
        Assert.Contains("bp-evidence-grid", markup);
        Assert.Contains("bp-validation-drawer", markup);

        Assert.Contains(".bp-conformance-main-grid", css);
        Assert.Contains(".bp-workflow-panel", css);
        Assert.Contains(".bp-next-step-card", css);
        Assert.Contains(".bp-evidence-grid", css);
        Assert.Contains(".bp-validation-drawer", css);
    }

    [Fact]
    public void PassportViewModel_ShouldExposeTrustFields()
    {
        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));

        Assert.Contains("TrustState", source);
        Assert.Contains("TrustIsDirty", source);
        Assert.Contains("TrustLastValidatedAt", source);
        Assert.Contains("TrustBlockingErrorCount", source);
        Assert.Contains("TrustWarningCount", source);
    }

    [Fact]
    public void PassportSummary_ShouldHideTrustStateWhileDetailConditionallyShowsItInTrustTab()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

        Assert.DoesNotContain("passport.TrustState", summary);
        Assert.Contains("Model.CanViewTrustConformance", detail);
        Assert.Contains("tab-trust", detail);
        Assert.Contains("passport.TrustState", detail);
    }

    [Fact]
    public void AdminPages_ShouldLinkToConformance()
    {
        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("/conformance", edit);
        Assert.Contains("/conformance", clusters);
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
