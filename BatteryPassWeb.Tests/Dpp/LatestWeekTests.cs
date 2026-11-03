using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R7, the owner's item 1 ("live values have no plots anymore") and choice "Latest week with data": a battery
/// with no reading in the last 7 days showed no line. Its charts and overview lines now show the 7 days ending at its
/// last reading, titled with that week's dates; a battery that reports keeps "The last 7 days". The last reading's time
/// comes from the battery's stored values (no history scan), and one query loads that week, with the same rule of who
/// sees what.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class LatestWeekTests(MongoFixture mongo)
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void TheWeek_ShouldEndNow_UnlessTheLastReadingIsOlderThan7Days()
    {
        var older = Now.AddDays(-10);

        Assert.Equal(Now, LiveTrends.WeekEnd(Now.AddDays(-2), Now));
        Assert.Equal(Now, LiveTrends.WeekEnd(null, Now));
        Assert.Equal(older, LiveTrends.WeekEnd(older, Now));
        Assert.Equal("The last 7 days", LiveTrends.WeekTitle(Now, Now));
        Assert.Equal("Last week of readings: 17–24 Oct", LiveTrends.WeekTitle(older, Now));
        Assert.Equal("28 Oct – 4 Nov", LiveTrends.WeekDates(new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void TheTab_ShouldDrawTheOlderWeek_OnItsOwnAxis()
    {
        var end = Now.AddDays(-10);
        var soc = AttributeCatalog.ByNumber(63);
        LiveHistoryEntry Reading(int hoursBefore, decimal percent) =>
            new(soc, new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = percent }, end.AddHours(-hoursBefore), end.AddHours(-hoursBefore), "feed");
        var latest = new ShownValue(soc, [new ValueLine(null, "61 %")], new ValueOrigin(end, "feed", Measured: true));

        var tile = Assert.Single(LiveTrends.ForTab([latest], [Reading(0, 61), Reading(48, 80), Reading(200, 20)], Now, _ => string.Empty, end));

        Assert.Equal(2, tile.Readings); // the reading 200 hours before the end is outside that week
        Assert.Equal((end.AddHours(-LiveTrends.Hours), end), (tile.FromUtc!.Value, tile.ToUtc!.Value));
        Assert.Equal("17–24 Oct", tile.Period);
        Assert.Equal("From 80 to 61 over 17–24 Oct", tile.TrendWords);
        Assert.NotNull(tile.Line(300, 70));
    }

    [Fact]
    public async Task TheOverviewLines_ShouldShowTheLastWeekOfReadings()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var api = BatteryApiTests.Api(rig);
        var now = rig.Clock.GetUtcNow().UtcDateTime;
        foreach (var (daysAgo, level) in new[] { (12, 30), (11, 45), (10, 61) })
        {
            var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
            Assert.True((await api.WriteLiveValuesAsync(PublicationTesting.Team, pack, new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["stateOfChargeSoC"] = level }, now.AddDays(-daysAgo), preview: false)).Saved);
        }

        // The battery stopped reporting 10 days ago: every live value it holds was last measured then.
        foreach (var property in new[] { "numberOfFullChargingAndDischargingCycles", "temperatureInformation" })
        {
            await rig.Context.Database!.GetCollection<MongoDB.Bson.BsonDocument>(DppStore.Packs).UpdateOneAsync(
                MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", rig.PackId),
                MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set($"Values.{property}.GeneratedAtUtc", now.AddDays(-10)));
        }

        var sources = (await rig.Store.LoadSourcesAsync(rig.PackId))!;
        var charge = (await new LiveTrends(new ApplicationAuditService(rig.Context), rig.Clock).TilesAsync(sources.Pack, sources.Model)).Single(tile => tile.Property == "stateOfChargeSoC");

        Assert.Equal([30m, 45m, 61m], charge.Points.Select(point => point.Value));
        Assert.Equal(LiveTrends.WeekDates(now.AddDays(-10)), charge.Period);
        Assert.Equal($"From 30 to 61 over {LiveTrends.WeekDates(now.AddDays(-10))}", charge.TrendWords);
    }

    [Fact]
    public async Task TheDetailPagesWeek_ShouldEndAtTheLastReading_ForThoseWhoMaySeeIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var api = BatteryApiTests.Api(rig);
        var now = rig.Clock.GetUtcNow().UtcDateTime;
        foreach (var hoursAgo in new[] { 300, 260, 240 })
        {
            var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
            Assert.True((await api.WriteLiveValuesAsync(PublicationTesting.Team, pack, new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["stateOfChargeSoC"] = 50 }, now.AddHours(-hoursAgo), preview: false)).Saved);
        }

        var last = now.AddHours(-240);
        var (week, end) = await api.LatestWeekAsync(Reader.With("team", ReaderRole.Team), rig.PackId, last);
        var (recent, recentEnd) = await api.LatestWeekAsync(Reader.With("team", ReaderRole.Team), rig.PackId, now.AddHours(-1));
        var (other, _) = await api.LatestWeekAsync(Reader.With("south", ReaderRole.ClusterUser, BatteryTesting.South), rig.PackId, last);

        Assert.Equal(last, end);
        Assert.Equal([last, now.AddHours(-260), now.AddHours(-300)], week.Where(entry => entry.Attribute.Property == "stateOfChargeSoC").Select(entry => entry.MeasuredAtUtc));
        Assert.Equal(now, recentEnd);
        Assert.DoesNotContain(recent, entry => entry.Attribute.Property == "stateOfChargeSoC"); // the last 7 days had none
        Assert.Empty(other); // another cluster's user sees no purchaser values, as before
    }

    [Fact]
    public void ThePages_ShouldNameTheWeek_AndFindTheLastReadingFromTheStoredValues()
    {
        var tab = Read("web", "Views", "Passport", "_LiveHistory.cshtml");
        var tiles = Read("web", "Views", "Batteries", "_LiveTiles.cshtml");
        var controller = Read("web", "Controllers", "PassportController.cs");

        Assert.Contains("<h2 class=\"h5 mb-2\">@LiveTrends.WeekTitle(end, now)</h2>", tab, StringComparison.Ordinal);
        Assert.Contains("so the charts show its last week of readings.", tab, StringComparison.Ordinal);
        Assert.Contains("No reading in the last 7 days: the lines show the last week of readings, @Model[0].Period.", tiles, StringComparison.Ordinal);
        Assert.Contains("await liveHistory.LatestWeekAsync(reader, page.Version.PackId, lastReading, cancellationToken);", controller, StringComparison.Ordinal);
        Assert.Contains("value.Attribute.Property != BatteryPassWeb.Dpp.Documents.DocumentService.Accidents", controller, StringComparison.Ordinal); // the accident reports' page isn't a reading
    }
}
