namespace BatteryPassWeb.Tests;

public sealed class ValidationPolicyLayoutTests
{
    [Fact]
    public void Program_ShouldRegisterPublishPolicyService()
    {
        var source = File.ReadAllText(RepoFile("web", "Program.cs"));

        Assert.Contains("AddSingleton<PassportPublishPolicyService>", source);
    }

    [Fact]
    public void AdminController_ShouldSanitizeDraftSavesInsteadOfCreatingFakeVerifiedState()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("PassportPublishPolicyService", source);
        Assert.Contains("SanitizeTrustClaimsForDraftSave", source);
        Assert.DoesNotContain("validation[\"isValid\"] = true", source);
        Assert.DoesNotContain("verification[\"signedAt\"] = now", source);
    }

    [Fact]
    public void PassportsApiController_ShouldBlockDirectPublishAndSanitizeTrustClaims()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));

        Assert.Contains("PassportPublishPolicyService", source);
        Assert.Contains("RejectDirectPublishRequest", source);
        Assert.Contains("SanitizeTrustClaimsForDraftSave", source);
        Assert.Contains("Direct publish is blocked", source);
        Assert.DoesNotContain("MarkCanonicalDirtyAsync", source);
    }

    [Fact]
    public void EditPassportView_ShouldNotExposeDirectPublishedStatusSelection()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.DoesNotContain("value=\"published\"", markup);
        Assert.Contains("Publishing is handled from the trust workflow", markup);
    }

    [Fact]
    public void ConformanceView_ShouldExplainValidationPublishPolicy()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));

        Assert.Contains("Publish gate", markup);
        Assert.Contains("Save draft is always allowed", markup);
        Assert.Contains("Official schema violations block signing", markup);
        Assert.Contains("Publish requires a current valid signature proof", markup);
    }

    [Fact]
    public void PublicRoutes_ShouldExposeOnlyPublishedCleanSignedPassportsToPublicUsers()
    {
        var home = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));
        var registry = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
        var passport = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));

        Assert.Contains("PassportPublishPolicyService", home);
        Assert.Contains("SearchDocumentsAsync", home);
        Assert.Contains("IsPubliclyVisible", home);

        Assert.Contains("PassportPublishPolicyService", registry);
        Assert.Contains("SearchDocumentsAsync", registry);
        Assert.Contains("IsPubliclyVisible", registry);

        Assert.Contains("PassportPublishPolicyService", passport);
        Assert.Contains("IsPubliclyVisible", passport);
        Assert.Contains("AccessControlService.IsAdmin(User)", passport);
        Assert.Contains("return NotFound();", passport);
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
