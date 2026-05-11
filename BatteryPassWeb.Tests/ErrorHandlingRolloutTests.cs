namespace BatteryPassWeb.Tests;

public sealed class ErrorHandlingRolloutTests
{
    [Fact]
    public void TrustChangingAdminActions_ShouldShowServiceErrorInsteadOfClaimingSuccess()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var api = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
        var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));

        Assert.Contains("TrustWorkflowServiceErrorMessage", admin);
        Assert.Contains("catch (Exception exception) when (IsTrustPersistenceFailure(exception))", admin);
        Assert.Contains("if (!trustUpdated)", workflow);
        Assert.Contains("if (!published)", admin);
        Assert.Contains("if (!published)", workflow);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", api);
        Assert.Contains("retrySafe = true", api);
        Assert.Contains("Task<bool> UpdateTrustSignatureAsync", repository);
        Assert.Contains("Task<bool> PublishPassportAsync", repository);
        Assert.Contains("result.MatchedCount > 0", repository);
    }

    [Fact]
    public void RolloutGuide_ShouldDocumentFullTrustQrAndFailureChecklist()
    {
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));

        Assert.Contains("Trust workflow hardening checklist", guide);
        Assert.Contains("MongoDB/service failure", guide);
        Assert.Contains("validation -> sign -> publish -> QR scan", guide);
        Assert.Contains("dirty -> validate -> sign -> publish", guide);
        Assert.Contains("restricted document download returns 403 and writes an audit event", guide);
    }

    [Fact]
    public void TestingGuide_ShouldIncludeGuidedReadinessPhase5AChecklist()
    {
        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));

        Assert.Contains("Phase 5A guided readiness checklist", guide);
        Assert.Contains("Expected next action: Complete required data", guide);
        Assert.Contains("Expected next action: Sign passport", guide);
        Assert.Contains("Expected next action: Publish passport", guide);
        Assert.Contains("Expected state: Published and trusted", guide);
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
