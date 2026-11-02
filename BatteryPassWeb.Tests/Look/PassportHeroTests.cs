using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// The owner's item 10 (redesign R3): on the public summary and the detail page, the picture sits inside the hero card,
/// top right, the facts use the card's full width, and "View more" is in the card. Contained to the hero's own markup
/// and rules scoped to it; site.css is unchanged.
/// </summary>
public sealed class PassportHeroTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void ThePicture_ShouldSitInsideTheCardWithTheButtonsUnderTheFacts()
    {
        var hero = Read("web", "Views", "Passport", "_PassportHero.cshtml");
        var card = hero[hero.IndexOf("<article class=\"bp-report-hero-info", StringComparison.Ordinal)..hero.IndexOf("</article>", StringComparison.Ordinal)];

        Assert.Contains("<section class=\"bp-report-hero-card bp-hero-compact\">", hero, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-report-hero-media bp-hero-picture\">", card, StringComparison.Ordinal);
        Assert.True(card.IndexOf("bp-hero-picture", StringComparison.Ordinal) < card.IndexOf("<dl class=\"bp-field-grid", StringComparison.Ordinal));
        Assert.True(card.IndexOf("</dl>", StringComparison.Ordinal) < card.IndexOf("<div class=\"bp-hero-ctas\">", StringComparison.Ordinal));
        Assert.Contains("View more about this passport", card, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside", hero, StringComparison.Ordinal); // no empty picture column beside the card
    }

    [Fact]
    public void TheFacts_ShouldNotRepeatThePassportStatusTheBadgeShows()
    {
        // The Passport badge shows #2 to everyone, so the facts leave it out (owner, 2 Nov 2026); nothing is hidden.
        var hero = Read("web", "Views", "Passport", "_PassportHero.cshtml");
        var facts = hero[hero.IndexOf("<dl class=\"bp-field-grid", StringComparison.Ordinal)..hero.IndexOf("</dl>", StringComparison.Ordinal)];

        Assert.DoesNotContain("Passport status", facts, StringComparison.Ordinal);
        Assert.DoesNotContain("Model.Text(2)", facts, StringComparison.Ordinal);
        Assert.Contains("StatusBadge.Passport(", hero[hero.IndexOf("data-passport-badges", StringComparison.Ordinal)..hero.IndexOf("<dl class=\"bp-field-grid", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public void TheLayout_ShouldBeScopedToTheHeroAndGiveTheFactsTheFullWidth()
    {
        var css = Read("web", "wwwroot", "css", "bp-status.css");
        var hero = css[css.IndexOf(".bp-hero-compact .bp-report-hero-layout {", StringComparison.Ordinal)..css.IndexOf("/* ---- Dark", StringComparison.Ordinal)];

        Assert.Contains("\"battery ids qr picture\"\n    \"fields fields fields fields\"\n    \"cta cta cta cta\";", hero, StringComparison.Ordinal);
        Assert.Contains("    \"battery picture\"\n      \"ids ids\"", hero, StringComparison.Ordinal); // a phone: small, beside the name
        Assert.Contains("  height: 160px;\n  max-width: 220px;", hero, StringComparison.Ordinal);
        Assert.All(hero.Split('\n').Where(line => line.TrimEnd().EndsWith('{') && !line.TrimStart().StartsWith('@')), rule =>
            Assert.Contains(".bp-hero-compact", rule, StringComparison.Ordinal)); // every rule is the hero's own
    }
}
