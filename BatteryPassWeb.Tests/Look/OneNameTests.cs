using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Finding W03: one name per thing, everywhere. The app is "Battery Passport"; the Registry, Admin and Help keep one name
/// each; the API reference is "API reference" inside the Help; "Log in" and "Log out". The home page's tab says its name once.
/// </summary>
public sealed class OneNameTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    private static IEnumerable<(string Name, string Text)> Views() =>
        Directory.GetFiles(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "_ViewStart.cshtml"))!, "*.cshtml", SearchOption.AllDirectories)
            .Select(file => (Path.GetFileName(file), File.ReadAllText(file)));

    [Theory]
    [InlineData("Scania Battery Passport Viewer")]
    [InlineData("Battery Passport Viewer")]
    [InlineData("Scania Passport Registry")]
    [InlineData("Open Registry")]
    [InlineData("Open registry")]
    [InlineData("Administration")]
    [InlineData("Admin workspace")]
    [InlineData("Cluster workspace")]
    [InlineData("Developer help")]
    [InlineData("External API Help")]
    [InlineData("Account access")]
    [InlineData(">Login<")]
    [InlineData(">Logout<")]
    [InlineData(">Sign in<")]
    [InlineData("Sign in to see more")]
    [InlineData("Sign in as someone else")]
    public void NoPage_ShouldUseAnOldName(string old)
    {
        foreach (var (name, text) in Views())
        {
            Assert.False(text.Contains(old, StringComparison.Ordinal), $"{name} still says \"{old}\"");
        }
    }

    [Fact]
    public void EachThing_ShouldHaveItsOneName()
    {
        Assert.Contains("<h1>Registry</h1>", Read("web", "Views", "Registry", "Index.cshtml"), StringComparison.Ordinal);
        Assert.Contains("<h1>Admin</h1>", Read("web", "Views", "Shared", "_AdminTabs.cshtml"), StringComparison.Ordinal);
        Assert.Contains("<h1>API reference</h1>", Read("web", "Views", "Help", "Index.cshtml"), StringComparison.Ordinal);
        Assert.Contains("<h2>API reference</h2>", Read("web", "Views", "Help", "Guide.cshtml"), StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"h4 mb-3\">Log in</h1>", Read("web", "Views", "Login", "Index.cshtml"), StringComparison.Ordinal);
        Assert.Contains("w-100\">Log in</button>", Read("web", "Views", "Login", "Index.cshtml"), StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"mb-1\">Your account</h1>", Read("web", "Views", "Account", "Index.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheHomePagesTab_ShouldSayTheNameOnce()
    {
        var layout = Read("web", "Views", "Shared", "_Layout.cshtml");
        var home = Read("web", "Views", "Home", "Index.cshtml");

        Assert.Contains("<title>@(title == BatteryPassWeb.Services.TopBar.AppName ? title : $\"{title} - {BatteryPassWeb.Services.TopBar.AppName}\")</title>", layout, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"Title\"] = BatteryPassWeb.Services.TopBar.AppName;", home, StringComparison.Ordinal);
        Assert.Contains("<h1>Find a battery passport</h1>", home, StringComparison.Ordinal);
        Assert.Equal("Battery Passport", BatteryPassWeb.Services.TopBar.AppName);
    }
}
