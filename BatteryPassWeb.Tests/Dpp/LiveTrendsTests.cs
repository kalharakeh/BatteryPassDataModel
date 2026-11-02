using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R4, the owner's improvement 4: the battery overview's tiles (state of charge, cycles, temperature, capacity
/// fade), each with a small line of its last 7 days, read from the change log like the live history.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class LiveTrendsTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public async Task EachTile_ShouldShowItsValueAndItsLast7Days()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var api = BatteryApiTests.Api(rig);
        var now = rig.Clock.GetUtcNow().UtcDateTime;
        foreach (var (hoursAgo, level) in new[] { (200, 10), (48, 40), (24, 55), (1, 61) })
        {
            var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
            var written = await api.WriteLiveValuesAsync(PublicationTesting.Team, pack, new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["stateOfChargeSoC"] = level }, now.AddHours(-hoursAgo), preview: false);
            Assert.True(written.Saved, $"{hoursAgo}h: {string.Join("; ", written.Keys.Select(key => $"{key.Key}:{key.Outcome} {key.Message}"))}");
        }

        var sources = (await rig.Store.LoadSourcesAsync(rig.PackId))!;
        var tiles = await new LiveTrends(new ApplicationAuditService(rig.Context), rig.Clock).TilesAsync(sources.Pack, sources.Model);
        var charge = tiles.Single(tile => tile.Property == "stateOfChargeSoC");
        var fade = tiles.Single(tile => tile.Property == "capacityFade");

        Assert.Equal(["State of charge", "Full cycles", "Temperature", "Capacity fade"], tiles.Select(tile => tile.Title));
        Assert.Equal("61 %", charge.Shown);
        Assert.Equal([40m, 55m, 61m], charge.Points.Select(point => point.Value)); // the reading of 200 hours ago is older than 7 days
        Assert.Equal("From 40 to 61 over the last 7 days", charge.TrendWords);
        Assert.StartsWith("0,", charge.Line(140, 36), StringComparison.Ordinal);
        Assert.Equal("Re-stated at each status change", fade.Note);
        Assert.NotNull(fade.Value); // the model's value until the battery re-states it
        Assert.Null(fade.Line(140, 36));
        Assert.Equal("No change in the last 7 days", fade.TrendWords);
    }

    [Fact]
    public void TheLine_ShouldRunFromTheOldestOnTheLeftWithTheHighestOnTop()
    {
        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var tile = new LiveTile("State of charge", "stateOfChargeSoC", 80m, "%", "Measured …", [new(start, 20m), new(start.AddHours(12), 80m), new(start.AddHours(24), 50m)]);

        Assert.Equal("0,34 70,2 140,18", tile.Line(140, 36));
        Assert.Equal("0,18 140,18", new LiveTile("Full cycles", "x", 5m, "", "", [new(start, 5m), new(start.AddHours(1), 5m)]).Line(140, 36)); // flat in the middle
    }

    [Fact]
    public void TheOverview_ShouldShowTheTilesThenWhatNeedsDoingOnceInClearWords()
    {
        var team = Read("web", "Views", "Batteries", "Battery.cshtml");
        var overview = team[team.IndexOf("<div data-section=\"overview\"", StringComparison.Ordinal)..team.IndexOf("<div data-section=\"values\"", StringComparison.Ordinal)];
        var tiles = Read("web", "Views", "Batteries", "_LiveTiles.cshtml");

        Assert.Contains("@await Html.PartialAsync(\"~/Views/Batteries/_LiveTiles.cshtml\", Model.Tiles)", overview, StringComparison.Ordinal);
        Assert.True(overview.IndexOf("_LiveTiles", StringComparison.Ordinal) < overview.IndexOf("Nothing needs you.", StringComparison.Ordinal));
        Assert.DoesNotContain("<dt><svg class=\"bp-i\" aria-hidden=\"true\"><use href=\"#i-swap\" /></svg>Battery status</dt>", overview, StringComparison.Ordinal); // the header's badge says it
        Assert.Contains("Documents: @publication.Files.Count, all unchanged since they were uploaded", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("Uploaded files:", overview, StringComparison.Ordinal);
        Assert.Contains("<svg class=\"bp-spark\" viewBox=\"0 0 140 36\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"@tile.TrendWords\"><polyline points=\"@line\" /></svg>", tiles, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"~/Views/Batteries/_LiveTiles.cshtml\", Model.Tiles)", Read("web", "Views", "ClusterAdmin", "Battery.cshtml"), StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<LiveTrends>();", Read("web", "Dpp", "DppServiceCollectionExtensions.cs"), StringComparison.Ordinal);
    }
}
