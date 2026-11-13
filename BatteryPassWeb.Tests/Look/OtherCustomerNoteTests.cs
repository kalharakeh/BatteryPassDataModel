using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Finding W10: on another customer's battery the summary said "Sign in with another account to see more", though most
/// people have one account. It now says whose battery it is and what they see, with nothing to click.
/// </summary>
public sealed class OtherCustomerNoteTests
{
    private static readonly string Hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

    [Fact]
    public void AnotherCustomersBattery_ShouldSaySo()
    {
        var branch = Hero[Hero.IndexOf("else if (signedIn)\n", StringComparison.Ordinal)..Hero.IndexOf("                else\n", Hero.IndexOf("else if (signedIn)\n", StringComparison.Ordinal), StringComparison.Ordinal)];

        Assert.Contains("<p class=\"bp-report-hero-cta bp-report-hero-cta-warning mb-0\">This battery belongs to another customer; you see its public data.</p>", branch, StringComparison.Ordinal);
        Assert.DoesNotContain("switchAccount", branch, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign in with another account", Hero, StringComparison.Ordinal);
    }
}
