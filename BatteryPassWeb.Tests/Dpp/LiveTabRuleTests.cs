using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's item 11 and the managing session's rule: the Live values tab was left out whenever the last
/// 7 days had no readings (demo batteries without a week of readings, or a demo reset days ago). It now shows to any
/// reader allowed to see a live value whenever the battery has one: the latest value of each with its date, even older
/// than 7 days; the charts where the week has readings; a calm note when the week had none.
/// </summary>
public sealed class LiveTabRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static ShownValue Latest(int number, string text, DateTime at) =>
        new(AttributeCatalog.ByNumber(number), [new ValueLine(null, text)], new ValueOrigin(at, "feed", Measured: true));

    private static LiveHistoryEntry Reading(int hoursAgo, decimal percent) =>
        new(AttributeCatalog.ByNumber(63), new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = percent }, Now.AddHours(-hoursAgo), Now.AddHours(-hoursAgo), "feed");

    [Fact]
    public void AValueWithNoReadingThisWeek_ShouldShowItsLatestValueAndDate()
    {
        var old = Now.AddDays(-12);

        var tile = Assert.Single(LiveTrends.ForTab([Latest(63, "61 %", old)], [], Now, _ => string.Empty));

        Assert.Equal("61 %", tile.Shown);
        Assert.Equal(0, tile.Readings);
        Assert.Null(tile.Line(300, 70));
        Assert.Equal($"Measured {ValueWords.Time(old)} · {PassportPages.SourceWord("feed")}", tile.Note);
    }

    [Fact]
    public void AValueWithAWeekOfReadings_ShouldShowItsLine_AndTheLatestValue()
    {
        var tile = Assert.Single(LiveTrends.ForTab([Latest(63, "61 %", Now)], [Reading(0, 61), Reading(48, 80), Reading(400, 20)], Now, _ => string.Empty));

        Assert.Equal("61 %", tile.Shown);
        Assert.Equal(2, tile.Readings); // the reading 400 hours ago is outside the week
        Assert.NotNull(tile.Line(300, 70));
    }

    [Fact]
    public void OnlyLiveValues_ShouldBeTiles() =>
        Assert.Empty(LiveTrends.ForTab([Latest(21, "320 kg", Now)], [], Now, _ => string.Empty)); // #21 battery mass is fixed

    [Fact]
    public void TheTab_ShouldShowWheneverTheReaderMaySeeALiveValue_OnTheLatestPassport()
    {
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml"));
        var live = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_LiveHistory.cshtml"));
        var controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "PassportController.cs"));

        Assert.Contains("var live = Model.Current is null && liveValues.Length > 0", tabs, StringComparison.Ordinal);
        Assert.Contains("Model.Audiences.SelectMany(section => section.Values).Where(value => value.Attribute.Change == BatteryPassWeb.Dpp.Catalog.ChangeRule.Live)", tabs, StringComparison.Ordinal); // only what this reader may see
        Assert.Contains("0 => \"No reading in the last 7 days\"", live, StringComparison.Ordinal);
        Assert.Contains("if (liveHistory is not null && page.Current is null)", controller, StringComparison.Ordinal);
    }
}
