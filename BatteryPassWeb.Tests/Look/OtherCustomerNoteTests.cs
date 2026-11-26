using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Finding W10: on another customer's battery the summary said "Sign in with another account to see more", though most
/// people have one account. It now says whose battery it is and what they see, with nothing to click.
/// </summary>
public sealed class OtherCustomerNoteTests
{
    private static readonly string Hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml")).ReplaceLineEndings("\n"); // whatever the checkout's line endings

    [Fact]
    public void AnotherCustomersBattery_ShouldSaySo()
    {
        // Only when the battery has a customer and the reader another one (review 1, point 2).
        var start = Hero.IndexOf("else if (signedIn && Model.AnotherCustomers)\n", StringComparison.Ordinal);
        var branch = Hero[start..Hero.IndexOf("else if (signedIn)\n", start, StringComparison.Ordinal)];

        Assert.Contains("<p class=\"bp-report-hero-cta bp-report-hero-cta-warning mb-0\">This battery belongs to another customer; you see its public data.</p>", branch, StringComparison.Ordinal);
        Assert.DoesNotContain("switchAccount", branch, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign in with another account", Hero, StringComparison.Ordinal);
    }
}
