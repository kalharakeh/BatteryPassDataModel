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
        Assert.Contains("Warnings do not block signing", markup);
        Assert.Contains("Publish requires a current valid signature proof", markup);
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
