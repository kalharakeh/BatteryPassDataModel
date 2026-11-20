using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R3 on the passport pages: the passport's status for everyone, the battery's (#22, purchaser data) only for
/// its readers, and an earlier or newer passport as a calm chip and line (the owner's answer to Q4).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class StatusBadgePagesTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheBatteryStatus_ShouldBeShownOnlyToItsReaders()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var id = PassportPagesTests.Identifier(rig);
        var pages = PassportPagesTests.Pages(rig);

        var anyone = (await pages.SummaryAsync(Reader.Anonymous, id))!;
        var itsCluster = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), id))!;
        var surveillance = (await pages.DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), id))!;
        var recycler = (await pages.DetailAsync(Reader.With("recycler@example.com", ReaderRole.LiRecycling), id))!;

        Assert.Null(StatusBadge.Battery(anyone.BatteryStatus));
        Assert.Equal("Original", StatusBadge.Battery(itsCluster.BatteryStatus)!.Words);
        Assert.Equal("Original", StatusBadge.Battery(surveillance.BatteryStatus)!.Words);
        Assert.Null(StatusBadge.Battery(recycler.BatteryStatus));
    }

    [Fact]
    public async Task AnyoneWhoMayNotSeeTheBatteryStatus_ShouldNotReadItInTheNoticesEither()
    {
        // #22 is purchaser data, and "repurposed" is its value: the public reads the facts without it (manager, 2 Nov 2026).
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryPack>(rig.PackId))!;
        var successor = (await rig.Store.GetAsync<BatteryPassWeb.Dpp.Store.BatteryPack>(old.Succession!.SuccessorPackId!))!;
        var pages = PassportPagesTests.Pages(rig);
        string Id(BatteryPassWeb.Dpp.Store.BatteryPack pack) => pack.Values["uniqueProductIdentifier"].Value!.ToString();
        var day = ValueWords.Day(old.Succession.StartedAtUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        string[] statusWords = ["original", "re-used", "repurposed", "remanufactured", "waste"];

        var notices = new[]
        {
            (await pages.SummaryAsync(Reader.Anonymous, Id(old)))!.Notice!,
            (await pages.SummaryAsync(Reader.Anonymous, Id(successor)))!.Notice!,
            (await pages.SummaryAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), Id(successor)))!.Notice!, // the summary is public for everyone
            (await pages.DetailAsync(Reader.With("recycler@example.com", ReaderRole.LiRecycling), Id(successor)))!.Notice!,
        };
        var itsCluster = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), Id(successor)))!.Notice!;

        Assert.All(notices, notice => Assert.DoesNotContain(statusWords, word => notice.Text.Contains(word, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal($"Replaces an earlier passport of this battery (since {day}).", notices[1].Text);
        Assert.Equal($"Repurposed on {day}. Its earlier passport is inactive and stays readable.", itsCluster.Text);
    }

    [Fact]
    public void ATestPassport_ShouldWearATagThatExplainsItselfOnATap()
    {
        // A tag beside the badges, not a full-width banner (owner's point 7); a tap or a hover explains it (manager, 2 Nov 2026).
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var tag = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_TestTag.cshtml"));

        Assert.Contains("@await Html.PartialAsync(\"_TestTag\", \"Test passport\")", hero, StringComparison.Ordinal);
        Assert.Contains("<p class=\"bp-test-note\">Test identifiers:", hero, StringComparison.Ordinal); // the detail page says it in words
        Assert.DoesNotContain("<p class=\"bp-warning\">Test passport", hero, StringComparison.Ordinal);
        Assert.Contains("<button type=\"button\" class=\"bp-test-tag\" popovertarget=\"test-help\"", tag, StringComparison.Ordinal);
        Assert.Contains("<div id=\"test-help\" class=\"bp-status-help bp-test-help\" popover>", tag, StringComparison.Ordinal);
        Assert.Contains("@if (!Context.Items.ContainsKey(\"test-help\"))", tag, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHero_ShouldShowBothBadgesAndFoldItsNoticeIntoThem()
    {
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));

        Assert.Contains("@if (StatusBadge.Battery(Model.BatteryStatus) is { } battery)", hero, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_StatusHelp\")", hero, StringComparison.Ordinal);
        Assert.Contains("@if (Model.Notice is { Calm: true, Href: { } lineageHref } lineage)", hero, StringComparison.Ordinal);
        Assert.Contains("<a class=\"bp-chip\" href=\"@lineageHref\">", hero, StringComparison.Ordinal);
        Assert.Contains("<p class=\"bp-lineage\" data-passport-notice>", hero, StringComparison.Ordinal);
        Assert.Contains("<p class=\"bp-status-notice mt-2\" data-passport-notice>", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("<p class=\"bp-warning\" data-passport-notice>", hero, StringComparison.Ordinal); // no banner above the page (point 7)
        Assert.DoesNotContain("bp-status-pill", hero, StringComparison.Ordinal);
    }
}
