namespace BatteryPassWeb.Tests;

public sealed class AntiForgeryProtectionTests
{
    [Fact]
    public void CookieAuthenticatedJsonMutationApis_ShouldRequireAntiForgeryTokens()
    {
        var passportsApi = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
        var filesApi = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));
        var authApi = File.ReadAllText(RepoFile("web", "Controllers", "AuthApiController.cs"));

        AssertMethodHasAttribute(passportsApi, "Create", "ValidateAntiForgeryToken");
        AssertMethodHasAttribute(passportsApi, "Update", "ValidateAntiForgeryToken");
        AssertMethodHasAttribute(passportsApi, "Validate", "ValidateAntiForgeryToken");
        AssertMethodHasAttribute(passportsApi, "Sign", "ValidateAntiForgeryToken");
        AssertMethodHasAttribute(passportsApi, "Publish", "ValidateAntiForgeryToken");
        AssertMethodHasAttribute(passportsApi, "Archive", "ValidateAntiForgeryToken");

        AssertMethodHasAttribute(filesApi, "Upload", "ValidateAntiForgeryToken");
        AssertMethodHasAttribute(authApi, "Login", "ValidateAntiForgeryToken");
        AssertMethodHasAttribute(authApi, "Logout", "ValidateAntiForgeryToken");
    }

    [Fact]
    public void ExternalApiTokenEndpoints_ShouldNotRequireBrowserAntiForgeryTokens()
    {
        var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.DoesNotContain("ValidateAntiForgeryToken", externalApi);
        Assert.Contains("Authorization", externalApi);
        Assert.Contains("ValidateTokenAsync", externalApi);
    }

    [Fact]
    public void ExistingFileUploadJavascript_ShouldSendAntiForgeryHeader()
    {
        var editPassport = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));

        Assert.Contains("@Html.AntiForgeryToken()", editPassport);
        Assert.Contains("fetch('/api/files'", editPassport);
        Assert.Contains("'RequestVerificationToken': token", editPassport);
    }

    private static void AssertMethodHasAttribute(string source, string methodName, string attribute)
    {
        var methodStart = source.IndexOf($"Task<IActionResult> {methodName}(", StringComparison.Ordinal);
        if (methodStart < 0)
        {
            methodStart = source.IndexOf($"IActionResult {methodName}(", StringComparison.Ordinal);
        }

        Assert.True(methodStart >= 0, $"Could not find controller action {methodName}.");

        var methodLineStart = source.LastIndexOf('\n', methodStart);
        var actionBlockStart = methodLineStart;
        var cursor = methodLineStart;
        while (cursor > 0)
        {
            var previousLineStart = source.LastIndexOf('\n', cursor - 1);
            if (previousLineStart < 0)
            {
                break;
            }

            var previousLine = source[(previousLineStart + 1)..cursor].Trim();
            if (!previousLine.StartsWith("[", StringComparison.Ordinal))
            {
                break;
            }

            actionBlockStart = previousLineStart;
            cursor = previousLineStart;
        }

        Assert.True(actionBlockStart < methodLineStart, $"Could not find action attributes for {methodName}.");

        var actionDeclaration = source[actionBlockStart..methodStart];
        Assert.Contains(attribute, actionDeclaration);
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
