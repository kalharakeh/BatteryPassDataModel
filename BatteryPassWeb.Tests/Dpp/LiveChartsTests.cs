using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, the owner's items 7 and 8: on the detail page the last 7 days of live values are small charts (the latest
/// value, a line on a day axis, its low and high), with every reading folded under them; Live values and Modules are
/// tabs of their own, each only for a reader it has something for.
/// </summary>
public sealed class LiveChartsTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 19, 50, 0, DateTimeKind.Utc);

    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(["web", .. parts])).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static LiveHistoryEntry Entry(int number, JsonNode value, int hoursAgo, string source = "feed") =>
        new(AttributeCatalog.ByNumber(number), value, Now.AddHours(-hoursAgo), Now.AddHours(-hoursAgo), source);

    private static JsonNode Percent(decimal value) => new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = value };

    [Fact]
    public void EachValue_ShouldBeOneChart_WithItsLatestReadingLowHighAndDays()
    {
        var history = new[]
        {
            Entry(63, Percent(61), 0, "form"), Entry(83, JsonValue.Create(411), 2), Entry(63, Percent(85), 24), Entry(63, Percent(25), 72),
        };

        var charts = LiveTrends.Charts(history, Now, entry => entry.Value.ToJsonString());

        Assert.Equal(["State of Charge (SoC) (%)", "Number of full charging and discharging cycles"], charts.Select(chart => chart.Title)); // the catalogue's order, units in the title
        var charge = charts[0];
        Assert.Equal("61 %", charge.Shown);
        Assert.Equal((25m, 85m, 3), (charge.Low!.Value, charge.High!.Value, charge.Readings));
        Assert.Equal($"Measured {ValueWords.Time(Now)} · {PassportPages.SourceWord("form")}", charge.Note);
        Assert.NotNull(charge.Line(300, 70));
        Assert.Equal(7, charge.Days(300).Count); // the midnights of the last 7 days
        Assert.Equal("27 Oct", charge.Days(300)[0].Label);
        Assert.Null(charts[1].Line(300, 70)); // one reading: no line, "One reading this week"
        Assert.Equal(1, charts[1].Readings);
    }

    [Fact]
    public void TheLine_ShouldSpanTheWholeWeek()
    {
        var chart = LiveTrends.Charts([Entry(63, Percent(50), 84), Entry(63, Percent(60), 0)], Now, _ => string.Empty)[0];

        var points = chart.Line(300, 70)!.Split(' ');

        Assert.Equal("150", points[0].Split(',')[0]); // half a week ago sits half way across
        Assert.Equal("300", points[1].Split(',')[0]);
    }

    [Fact]
    public void AValueThatIsNotANumber_ShouldShowItsLatestReadingInWords()
    {
        var report = JsonValue.Create("https://example.com/accidents");
        var chart = LiveTrends.Charts([Entry(92, report!, 3)], Now, _ => "Accident page")[0];

        Assert.Equal("Accident page", chart.Shown);
        Assert.Null(chart.Line(300, 70));
    }

    [Fact]
    public void LiveValuesAndModules_ShouldBeTabs_NotCardsUnderThem()
    {
        var tabs = Read("Views", "Passport", "_PassportTabs.cshtml");
        var detail = Read("Views", "Passport", "Detail.cshtml");
        var live = Read("Views", "Passport", "_LiveHistory.cshtml");

        Assert.Contains("<div class=\"tab-pane fade@(buttons[0].Key == \"live\" ? \" show active\" : string.Empty)\" id=\"tab-live\" role=\"tabpanel\">", tabs, StringComparison.Ordinal);
        Assert.Contains("id=\"tab-modules\" role=\"tabpanel\">", tabs, StringComparison.Ordinal);
        Assert.Contains("var modules = Model.Modules is { Count: > 0 } listed ? listed : null;", tabs, StringComparison.Ordinal); // only for its readers
        Assert.DoesNotContain("bp-passport-module-list", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("<partial name=\"_LiveHistory\"", detail, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_PassportTabs\" model=\"Model\" view-data=\"ViewData\" />", detail, StringComparison.Ordinal);
        Assert.Contains("<summary>All readings (@Model.History.Count)</summary>", live, StringComparison.Ordinal); // the tab's own model since R6
        Assert.Contains("var charts = LiveTrends.ForTab(Model.Latest, Model.History, Clock.GetUtcNow().UtcDateTime, Reading);", live, StringComparison.Ordinal);
        Assert.Contains("a[href^=\"#tab-\"]", Read("wwwroot", "js", "bp-passport-links.js"), StringComparison.Ordinal);
    }
}
