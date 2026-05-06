namespace BatteryPassWeb.Tests;

public sealed class SigningWorkflowLayoutTests
{
    [Fact]
    public void Program_ShouldRegisterSigningRevisionAndAuditServices()
    {
        var source = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<CanonicalPassportSnapshotService>", source);
        Assert.Contains("AddSingleton<DemoSigningKeyService>", source);
        Assert.Contains("AddSingleton<PassportTrustService>", source);
        Assert.Contains("AddSingleton<AuditRevisionService>", source);
    }

    [Fact]
    public void AdminController_ShouldExposeSignPublishAuditAndRevisionRoutes()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("[HttpPost(\"passports/{passportId}/sign\")]", source);
        Assert.Contains("[HttpPost(\"passports/{passportId}/publish\")]", source);
        Assert.Contains("[HttpGet(\"passports/{passportId}/audit\")]", source);
        Assert.Contains("[HttpGet(\"passports/{passportId}/revisions\")]", source);
        Assert.Contains("CreateSignedRevisionAsync", source);
        Assert.Contains("UpdateTrustSignatureAsync", source);
        Assert.Contains("AppendAuditEventAsync", source);
        Assert.Contains("PublishPassportAsync", source);
    }

    [Fact]
    public void PassportsApiController_ShouldExposeSignPublishAndPublicVerifyRoutes()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));

        Assert.Contains("[HttpPost(\"{passportId}/sign\")]", source);
        Assert.Contains("[HttpPost(\"{passportId}/publish\")]", source);
        Assert.Contains("[AllowAnonymous]", source);
        Assert.Contains("[HttpGet(\"{passportId}/verify\")]", source);
        Assert.Contains("PassportTrustService", source);
        Assert.Contains("AuditRevisionService", source);
    }

    [Fact]
    public void ConformanceView_ShouldRenderSigningActionsAndProofDiagnostics()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.Contains("Sign passport", markup);
        Assert.Contains("Publish passport", markup);
        Assert.Contains("Verification diagnostics", markup);
        Assert.Contains("Audit trail", markup);
        Assert.Contains("Revision history", markup);
        Assert.Contains("Model.VerificationResult", markup);
    }

    [Fact]
    public void PassportViewModel_ShouldExposePublicProofMetadata()
    {
        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));

        Assert.Contains("TrustLatestHash", source);
        Assert.Contains("TrustLatestRevisionId", source);
        Assert.Contains("TrustLastSignedAt", source);
        Assert.Contains("TrustIssuer", source);
        Assert.Contains("TrustVerificationMethod", source);
        Assert.Contains("TrustProofStatus", source);
        Assert.Contains("TrustVerificationMessage", source);
    }

    [Fact]
    public void PassportSummaryShouldHideTrustDataAndDetailShouldRenderTrustTab()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

        Assert.DoesNotContain("Public verification", summary);
        Assert.DoesNotContain("passport.Trust", summary);
        Assert.DoesNotContain("passport.IsValid", summary);
        Assert.DoesNotContain("Conformance", summary);
        Assert.DoesNotContain("Verified", summary);
        Assert.DoesNotContain("Unverified", summary);
        Assert.Contains("Trust & conformance", detail);
        Assert.Contains("data-bs-target=\"#tab-trust\"", detail);
        Assert.Contains("id=\"tab-trust\"", detail);
        Assert.Contains("Public verification", detail);
        Assert.Contains("passport.TrustLatestHash", detail);
        Assert.Contains("passport.TrustIssuer", detail);
        Assert.Contains("passport.TrustVerificationMethod", detail);
        Assert.Contains("passport.TrustLatestRevisionId", detail);
        Assert.Contains("passport.TrustProofStatus", detail);
        Assert.Contains("passport.TrustState", detail);
    }

    [Fact]
    public void AdminAuditAndRevisionViews_ShouldRenderLedgerPages()
    {
        var audit = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));
        var revisions = File.ReadAllText(RepoFile("web", "Views", "Admin", "Revisions.cshtml"));

        Assert.Contains("Audit trail", audit);
        Assert.Contains("Model.AuditEvents", audit);
        Assert.Contains("Revision history", revisions);
        Assert.Contains("Model.Revisions", revisions);
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
