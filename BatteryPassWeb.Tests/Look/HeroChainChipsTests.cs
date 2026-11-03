using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Redesign R6b (the owner's rule: no duplicates): on the team's battery page, "Passports of this battery" lists every
/// passport of a battery placed on the market again, so the hero's "Earlier passport" and "New passport" chips, which
/// led to the same passports, show only while there is no list. The public page has no list and keeps its link.
/// </summary>
public sealed class HeroChainChipsTests
{
    [Fact]
    public void TheTeamsHero_ShouldOfferThePassportChipsOnlyWithoutTheList()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var pages = File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Pages", "PassportPages.cs"));

        Assert.Contains("@if (Model.Chain.Count == 0 && Model.Status?.Predecessor is { } earlier)", page, StringComparison.Ordinal);
        Assert.Contains("@if (Model.Chain.Count == 0 && Model.Status?.Succession is { Linked: true } next && (next.SuccessorHref ?? next.OtherPassport) is { } newPassport)", page, StringComparison.Ordinal);
        Assert.Contains("\"Earlier passport\") { Calm = true }", pages, StringComparison.Ordinal); // the public page keeps it
    }
}
