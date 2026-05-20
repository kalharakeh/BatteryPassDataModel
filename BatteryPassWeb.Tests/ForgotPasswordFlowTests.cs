namespace BatteryPassWeb.Tests;

public sealed class ForgotPasswordFlowTests
{
    [Fact]
    public void Login_ShouldExposeForgotPasswordRequestFlow()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
        var auth = File.ReadAllText(RepoFile("web", "Services", "AuthService.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "Login", "Index.cshtml"));

        Assert.Contains("[HttpPost(\"forgot-password\")]", controller);
        Assert.Contains("StorePasswordResetRequestAsync", auth);
        Assert.Contains("Forgot password?", view);
        Assert.Contains("If an account exists, reset instructions will be sent when email delivery is configured.", view);
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
