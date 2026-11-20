using System.Security.Claims;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Finding L04 (the owner's approved picture, 13 Nov 2026): one top bar, its main links by who is signed in, the current
/// page's link marked, and the account as initials and a name that opens a menu.
/// </summary>
public sealed class TopBarTests
{
    private static readonly string Layout = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
    private static readonly string Script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-topbar.js"));

    private static ClaimsPrincipal Signed(params string[] roles) => new(new ClaimsIdentity(
        roles.Select(role => new Claim(ClaimTypes.Role, role)).Append(new Claim(ClaimTypes.Email, "someone@example.test")),
        "Test"));

    private static string Texts(IEnumerable<TopBarLink> links) => string.Join(", ", links.Select(link => link.Text));

    [Fact]
    public void Links_ShouldDependOnWhoIsSignedIn()
    {
        Assert.Equal("To do, Admin, Registry, Help", Texts(TopBar.Links(Signed(AccessControlService.RoleAdmin), "/")));
        Assert.Equal("Registry, Admin, Help", Texts(TopBar.Links(Signed(AccessControlService.RoleClusterAdmin), "/")));
        foreach (var reader in new[]
        {
            AccessControlService.RoleNormalUser, AccessControlService.RoleMarketSurveillanceAuthority, AccessControlService.RoleNotifiedBody,
            AccessControlService.RoleCommission, AccessControlService.RoleLiRepair, AccessControlService.RoleLiRecycling, AccessControlService.RoleLiTreatment,
        })
        {
            Assert.Equal("Registry, Help", Texts(TopBar.Links(Signed(reader), "/")));
        }

        Assert.Equal("Help", Texts(TopBar.Links(new ClaimsPrincipal(new ClaimsIdentity()), "/")));
    }

    [Fact]
    public void Links_ShouldLeadWhereEachNameSays()
    {
        var team = TopBar.Links(Signed(AccessControlService.RoleAdmin), "/").ToDictionary(link => link.Text, link => link.Href);
        Assert.Equal("/admin/start", team["To do"]);
        Assert.Equal("/admin/batteries", team["Admin"]);
        Assert.Equal("/registry", team["Registry"]);
        Assert.Equal("/help", team["Help"]);
        Assert.Equal("/cluster-admin", TopBar.Links(Signed(AccessControlService.RoleClusterAdmin), "/").Single(link => link.Text == "Admin").Href);
        Assert.True(TopBar.Links(Signed(AccessControlService.RoleClusterAdmin), "/").Single(link => link.Text == "Registry").CountsWaiting);
    }

    [Theory]
    [InlineData("/admin/start", "To do")]
    [InlineData("/admin/batteries", "Admin")]
    [InlineData("/admin/batteries/pack-1", "Admin")]
    [InlineData("/admin/battery-ids", "Admin")]
    [InlineData("/admin/families", "Admin")]
    [InlineData("/registry", "Registry")]
    [InlineData("/help", "Help")]
    [InlineData("/help/api", "Help")]
    [InlineData("/", null)]
    [InlineData("/dpp/some-id", null)]
    [InlineData("/administration", null)]
    public void TheCurrentPagesLink_ShouldBeMarked_ForTheTeam(string path, string? current) =>
        Assert.Equal(current, TopBar.Links(Signed(AccessControlService.RoleAdmin), path).SingleOrDefault(link => link.Current)?.Text);

    [Theory]
    [InlineData("/cluster-admin", "Admin")]
    [InlineData("/cluster-admin/users", "Admin")]
    [InlineData("/cluster-admin/batteries/pack-1", "Admin")]
    [InlineData("/registry", "Registry")]
    public void TheCurrentPagesLink_ShouldBeMarked_ForACustomersAdmin(string path, string current) =>
        Assert.Equal(current, TopBar.Links(Signed(AccessControlService.RoleClusterAdmin), path).Single(link => link.Current).Text);

    [Theory]
    [InlineData("Demo Administrator", "DA")]
    [InlineData("dana nordin", "DN")]
    [InlineData("Ann", "A")]
    [InlineData("Anna Maria Berg", "AM")]
    [InlineData("north.admin@example.test", "N")]
    [InlineData("", "?")]
    public void Initials_ShouldBeTheFirstLettersOfTheFirstTwoWords(string name, string initials) =>
        Assert.Equal(initials, TopBar.Initials(name));

    [Fact]
    public void TheRoleWord_ShouldSayCustomer_ForACustomersPeople()
    {
        Assert.Equal("Customer admin", TopBar.RoleWord(Signed(AccessControlService.RoleClusterAdmin)));
        Assert.Equal("Customer user", TopBar.RoleWord(Signed(AccessControlService.RoleNormalUser)));
        Assert.Equal("Global Admin", TopBar.RoleWord(Signed(AccessControlService.RoleAdmin))); // as in the approved picture
        Assert.Equal("Market Surveillance Authorities", TopBar.RoleWord(Signed(AccessControlService.RoleMarketSurveillanceAuthority)));
    }

    [Fact]
    public void TheLayout_ShouldHaveOneBar_WithTheAccountMenu()
    {
        var header = Layout[Layout.IndexOf("<header", StringComparison.Ordinal)..(Layout.IndexOf("</header>", StringComparison.Ordinal) + 9)];

        Assert.Equal(1, Layout.Split("<header").Length - 1);
        Assert.Contains("<header class=\"bp-topbar@(account is null ? \" is-visitor\" : string.Empty)\">", header, StringComparison.Ordinal);
        Assert.Contains("<details class=\"bp-account\" data-account-menu>", header, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Account menu", header, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"@(link.Current ? \"page\" : null)\"", header, StringComparison.Ordinal);
        Assert.Contains(">Your account</a>", header, StringComparison.Ordinal);
        Assert.Contains(">Log out</button>", header, StringComparison.Ordinal);
        Assert.Contains(">Log in</a>", header, StringComparison.Ordinal);
        Assert.Contains("Dark mode", header, StringComparison.Ordinal);
        Assert.Contains("<script src=\"~/js/bp-topbar.js\" asp-append-version=\"true\" defer></script>", header, StringComparison.Ordinal);
        Assert.DoesNotContain("Logout", Layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Open Registry", Layout, StringComparison.Ordinal);
        Assert.DoesNotContain("style=\"", header, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", header, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMenuScript_ShouldCloseOnEscape_AndGiveFocusBack()
    {
        Assert.Contains("'Escape'", Script, StringComparison.Ordinal);
        Assert.Contains(".focus()", Script, StringComparison.Ordinal);
        Assert.Contains("aria-expanded", Script, StringComparison.Ordinal);
    }
}
