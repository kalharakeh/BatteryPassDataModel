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
        Assert.Contains("ProductTemplateService", source);
        Assert.Contains("GetPolicyForPassportAsync", source);
        Assert.DoesNotContain("[HttpPost(\"passports/{passportId}/complete-required-data\")]", source);
        Assert.Contains("ConformanceViewModel", source);
    }

    [Fact]
    public void Program_ShouldRegisterRequiredDataCompletionService()
    {
        var source = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<DemoRequiredDataCompletionService>", source);
    }

    [Fact]
    public void Program_ShouldRegisterPassportEvidenceService()
    {
        var source = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<PassportEvidenceService>", source);
    }

    [Fact]
    public void ConformanceView_ShouldRenderTrustSummaryAndActions()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Conformance", markup);
        Assert.Contains("Blocking errors", markup);
        Assert.Contains("Warnings", markup);
        Assert.Contains("Validate passport", markup);
        Assert.Contains("Back to battery list", markup);
        Assert.Contains("/admin/clusters?tab=batteries", markup);
        Assert.Contains("bp-conformance-return-row", markup);
        Assert.Contains("bp-conformance-return-link", markup);
        Assert.True(
            markup.IndexOf("bp-conformance-return-row", StringComparison.Ordinal) <
            markup.IndexOf("bp-readiness-hero", StringComparison.Ordinal),
            "Back navigation should sit outside and above the passport readiness container.");
        Assert.Contains(".bp-conformance-return-row", css);
        Assert.Contains(".bp-conformance-return-link", css);
        Assert.Contains("Latest validation", markup);
        Assert.Contains("What is stopping signing?", markup);
        Assert.Contains("Full-data requirements", markup);
        Assert.Contains("Completion checklist", markup);
        Assert.Contains("Required data by section", markup);
        Assert.Contains("Missing required data", markup);
        Assert.Contains("Invalid format", markup);
        Assert.Contains("Invalid value", markup);
        Assert.Contains("Business-rule data", markup);
        Assert.Contains("Product template", markup);
        Assert.Contains("bp-conformance-shell", markup);
        Assert.Contains("bp-issue-list", markup);
        Assert.Contains("bp-completion-checklist", markup);
        Assert.Contains("bp-completion-section", markup);
        Assert.Contains("bp-action-control", markup);
        Assert.DoesNotContain("showCompleteRequiredData", markup);
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
        Assert.DoesNotContain("@if (showCompleteRequiredData)", markup);
        Assert.DoesNotContain("Complete required demo data", markup);
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

    [Fact]
    public void ConformanceModel_ShouldExposeGuidedReadinessData()
    {
        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ConformanceViewModel.cs"));

        Assert.Contains("PassportReadinessDecision", source);
        Assert.Contains("GroupedBlockingIssues", source);
        Assert.Contains("GroupedWarningIssues", source);
        Assert.Contains("ConformanceIssueGroupViewModel", source);
    }

    [Fact]
    public void ConformanceModel_ShouldExposeEvidencePack()
    {
        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ConformanceViewModel.cs"));

        Assert.Contains("EvidencePackResult", source);
        Assert.Contains("EvidencePack", source);
    }

    [Fact]
    public void AdminController_ShouldComputePassportReadinessForConformance()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("PassportReadinessService", source);
        Assert.Contains("_passportReadinessService.Evaluate", source);
        Assert.Contains("GroupedBlockingIssues", source);
        Assert.Contains("AddSingleton<PassportReadinessService>", program);
    }

    [Fact]
    public void AdminAndApiControllers_ShouldAppendEvidenceValidationSection()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var api = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));

        Assert.Contains("PassportEvidenceService", admin);
        Assert.Contains("_passportEvidenceService.Evaluate", admin);
        Assert.Contains("AppendValidationSection", admin);
        Assert.Contains("EvidencePack", admin);

        Assert.Contains("PassportEvidenceService", api);
        Assert.Contains("_passportEvidenceService.Evaluate", api);
        Assert.Contains("AppendValidationSection", api);
    }

    [Fact]
    public void ConformanceView_ShouldRenderOneGuidedNextActionAndCollapsibleDiagnostics()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-readiness-hero", markup);
        Assert.Contains("Model.Readiness.StateLabel", markup);
        Assert.Contains("Model.Readiness.NextActionLabel", markup);
        Assert.Contains("bp-next-action-panel", markup);
        Assert.Contains("bp-blocker-groups", markup);
        Assert.Contains("bp-advanced-diagnostics", markup);
        Assert.Contains("<details", markup);
        Assert.DoesNotContain("Action blocked", markup);
        Assert.DoesNotContain("disabled=\"", markup);

        Assert.Contains(".bp-readiness-hero", css);
        Assert.Contains(".bp-next-action-panel", css);
        Assert.Contains(".bp-blocker-groups", css);
        Assert.Contains(".bp-advanced-diagnostics", css);
    }

    [Fact]
    public void ConformanceView_ShouldRenderEvidenceReadinessPanel()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("Evidence readiness", markup);
        Assert.Contains("Model.EvidencePack", markup);
        Assert.Contains("Missing required", markup);
        Assert.Contains("Changed since signing", markup);
        Assert.Contains("Uploaded unsigned", markup);
        Assert.Contains("Verified documents", markup);
        Assert.Contains("bp-evidence-panel", markup);
        Assert.Contains("bp-evidence-item", markup);
        Assert.Contains("bp-evidence-status", markup);

        Assert.Contains(".bp-evidence-panel", css);
        Assert.Contains(".bp-evidence-list", css);
        Assert.Contains(".bp-evidence-item", css);
        Assert.Contains(".bp-evidence-status", css);
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
