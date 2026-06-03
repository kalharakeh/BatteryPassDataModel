namespace BatteryPassWeb.Tests;

public sealed class ForgotPasswordFlowTests
{
    [Fact]
    public void Login_ShouldExposeForgotPasswordRequestFlow()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
        var auth = File.ReadAllText(RepoFile("web", "Services", "AuthService.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Login", "Index.cshtml"));
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
        var options = File.ReadAllText(RepoFile("web", "Configuration", "BatteryPassOptions.cs"));

        Assert.Contains("[HttpPost(\"forgot-password\")]", controller);
        Assert.Contains("[HttpGet(\"reset-password\")]", controller);
        Assert.Contains("[HttpPost(\"reset-password\")]", controller);
        Assert.Contains("CreatePasswordResetAsync", auth);
        Assert.Contains("ConsumePasswordResetTokenAsync", auth);
        Assert.Contains("IEmailSender", program);
        Assert.Contains("EmailSmtpHost", options);
        Assert.Contains("Forgot password?", view);
        Assert.Contains("If an account exists, reset instructions have been sent.", view);
        Assert.True(File.Exists(RepoPath("web", "Views", "Login", "ResetPassword.cshtml")));
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
