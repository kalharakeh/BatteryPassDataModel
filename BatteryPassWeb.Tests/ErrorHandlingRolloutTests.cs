namespace BatteryPassWeb.Tests;

public sealed class ErrorHandlingRolloutTests
{
    [Fact]
    public void TrustChangingAdminActions_ShouldShowServiceErrorInsteadOfClaimingSuccess()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var api = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));

        Assert.Contains("TrustWorkflowServiceErrorMessage", admin);
        Assert.Contains("catch (Exception exception) when (IsTrustPersistenceFailure(exception))", admin);
        Assert.Contains("if (!trustUpdated)", admin);
        Assert.Contains("if (!published)", admin);
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
