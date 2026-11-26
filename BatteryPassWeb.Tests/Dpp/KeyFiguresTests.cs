using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R5, owner's item 4 (decision 37): "Key figures" replaces the summary's "Summary report" card and shows on
/// the detail page under the hero too. Public values only, the same card for every reader; nothing is worked out.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class KeyFiguresTests(MongoFixture mongo)
{
    private static string View(string name) => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", name)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static readonly Dictionary<int, JsonNode?> Compact7M = new()
    {
        [57] = JsonNode.Parse("""{"amperehourMiliamperehourValue":140}"""),
        [64] = JsonNode.Parse("""{"voltValue":320}"""),
        [65] = JsonNode.Parse("""{"voltValue":460}"""),
        [66] = JsonNode.Parse("""{"voltValue":400}"""),
        [67] = JsonNode.Parse("""{"wattValueAt80PercentStateOfCharge":171000,"wattValueAt20PercentStateOfCharge":126000}"""),
        [70] = JsonNode.Parse("""{"wattValue":180000}"""),
        [82] = JsonValue.Create(3000),
        [85] = JsonNode.Parse("""{"amperePerAmpereHourValue":0.5}"""),
        [71] = JsonNode.Parse("""{"percentageValue":93}"""),
        [72] = JsonNode.Parse("""{"percentageValue":90}"""),
        [30] = JsonNode.Parse("""{"kgCO2EquivalentPerKilowattHourValue":64}"""),
        [35] = JsonValue.Create("Class B"),
        [53] = JsonNode.Parse("""{"percentageValue":38}"""),
        [42] = JsonNode.Parse("""["Cobalt","Lithium"]"""),
        [88] = JsonNode.Parse("""{"celsiusValue":-20}"""),
        [89] = JsonNode.Parse("""{"celsiusValue":45}"""),
        [26] = JsonNode.Parse("""{"agentFireClass":"B","extinguishingAgent":"Water"}"""),
    };

    private static string? Words(int number) => number switch { 19 => "8 years", 41 => "Li-ion NMC", _ => null };

    [Fact]
    public void TheFigures_ShouldReadTheFilesValues_InWordsAReaderKnows()
    {
        var figures = KeyFigures.From(number => Compact7M.GetValueOrDefault(number), Words, [], [new ChartPart("Nickel", 30, "%")]);

        Assert.Equal(["Capacity and voltage", "Power", "Expected lifetime", "Round-trip efficiency"], figures.Using.Select(figure => figure.Label));
        Assert.Equal(new KeyFigure("battery", "Capacity and voltage", "140 Ah", "at 400 V", "Works between 320 and 460 V", 57), figures.Using[0]);
        Assert.Equal(new KeyFigure("bolt", "Power", "171 kW", "at 80 % charge", "126 kW at 20 % charge · at most 180 kW", 67), figures.Using[1]); // the stored W, in kW
        Assert.Equal(new KeyFigure("reuse", "Expected lifetime", "3,000", "cycles", "Tested at 0.5 C · warranty 8 years", 82), figures.Using[2]);
        Assert.Equal("90 % at half its cycle life", figures.Using[3].Under);
        Assert.Equal(("64", "Class B"), (figures.Carbon!.Value, figures.CarbonClass));
        Assert.Equal(38m, figures.Renewable);
        Assert.Equal(("value-71", "value-30", 49), (figures.Using[3].Anchor, figures.Carbon.Anchor, figures.RecycledNumber)); // where the detail page's links land
        Assert.Equal(new KeyFigure("flask", "Chemistry", "Li-ion NMC", null, "Critical raw materials: cobalt, lithium", 41), figures.Safety[0]);
        Assert.Equal(new KeyFigure("thermo", "Storage and fire", "−20 to 45 °C", "when idle", "In a fire: water (fire class B)", 88), figures.Safety[1]);
    }

    [Fact]
    public void AFigureWithoutItsValue_ShouldNotShow()
    {
        var figures = KeyFigures.From(number => number == 57 ? Compact7M[57] : null, _ => null, [], []);

        Assert.Equal(new KeyFigure("battery", "Capacity and voltage", "140 Ah", null, null, 57), Assert.Single(figures.Using));
        Assert.Null(figures.Carbon);
        Assert.Empty(figures.Safety);
        Assert.True(figures.Any);
        Assert.False(KeyFigures.From(_ => null, _ => null, [], []).Any);
    }

    [Fact]
    public async Task TheCard_ShouldBeTheSameForEveryReader_OnBothPages()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = PassportPagesTests.Pages(rig);
        var id = PassportPagesTests.Identifier(rig);

        var summary = (await pages.SummaryAsync(Reader.Anonymous, id))!.KeyFigures!;
        var purchaser = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), id))!.KeyFigures!;

        Assert.True(summary.Any);
        Assert.Equal(summary.Using, purchaser.Using);
        Assert.Equal(summary.Safety, purchaser.Safety);
        Assert.Equal(summary.Carbon, purchaser.Carbon);
        Assert.Equal(summary.Recycled, purchaser.Recycled);
        Assert.DoesNotContain(summary.Using.Concat(summary.Safety), figure => figure.Label.Contains("charge", StringComparison.OrdinalIgnoreCase) && figure.Label != "Power"); // no state of charge (#63, purchaser data)
    }

    [Fact]
    public async Task EveryFiguresLink_ShouldLandOnAnEntryOfTheDetailPage()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var page = (await PassportPagesTests.Pages(rig).DetailAsync(Reader.With("team@example.com", ReaderRole.Team), PassportPagesTests.Identifier(rig)))!;
        var figures = page.KeyFigures!;
        var entries = page.Sections.Concat(page.Audiences).SelectMany(section => section.Values).Select(value => KeyFigures.Anchor(value.Attribute.Number)).ToHashSet();

        var anchors = figures.Using.Concat(figures.Safety).Append(figures.Carbon).OfType<KeyFigure>().Select(figure => figure.Anchor).Append(KeyFigures.Anchor(figures.RecycledNumber)).ToArray();

        Assert.NotEmpty(anchors);
        Assert.All(anchors, anchor => Assert.Contains(anchor, entries));
    }

    [Fact]
    public void OnTheDetailPage_EachTitleShouldLinkDown_AndTheLinkOpenItsTab()
    {
        var label = View("_KeyFigureLabel.cshtml");
        var tabs = View("_PassportTabs.cshtml");
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-passport-links.js"));

        Assert.Contains("@if (ViewData[\"Detail\"] is true)", label, StringComparison.Ordinal); // the summary's card has no links
        Assert.Contains("<a href=\"#@Model.Anchor\" data-key-figure-link>", label, StringComparison.Ordinal);
        Assert.Contains("<div id=\"@KeyFigures.Anchor(value.Attribute.Number)\">", tabs, StringComparison.Ordinal);
        Assert.Contains("<script src=\"~/js/bp-passport-links.js\" asp-append-version=\"true\"></script>", View("Detail.cshtml"), StringComparison.Ordinal);
        Assert.Contains("window.bootstrap.Tab.getOrCreateInstance(button).show();", script, StringComparison.Ordinal);
        Assert.Contains("folded.open = true;", script, StringComparison.Ordinal);
        Assert.Contains("entry.classList.add('is-landed');", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BothPages_ShouldShowTheCard_AndTheOldSummaryReportIsGone()
    {
        var summary = View("Summary.cshtml");
        var detail = View("Detail.cshtml");
        var card = View("_KeyFigures.cshtml");

        Assert.Contains("<partial name=\"_KeyFigures\" model=\"keyFigures\" />", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Summary report", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("bp-pie", summary, StringComparison.Ordinal);
        Assert.True(detail.IndexOf("_PassportHero", StringComparison.Ordinal) < detail.IndexOf("_KeyFigures", StringComparison.Ordinal)
            && detail.IndexOf("_KeyFigures", StringComparison.Ordinal) < detail.IndexOf("_PassportTabs", StringComparison.Ordinal)); // under the hero
        Assert.Contains("<span class=\"bp-summary-title\" id=\"key-figures-title\">Key figures</span>", card, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-kf-stack\" role=\"img\"", card, StringComparison.Ordinal); // one bar instead of the pie
    }
}
