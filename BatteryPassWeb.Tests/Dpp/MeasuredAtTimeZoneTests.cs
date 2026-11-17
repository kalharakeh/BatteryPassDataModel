using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// H1 round two, point 3: a "measured at" never depends on the server's time zone. The pages send the browser's offset
/// with the typed time and the server converts with it (UTC without one); the API and uploads read a time without an
/// offset as UTC and honour one that has it. These tests run on a PC whose own zone isn't UTC (CET here), so the old
/// reading in the server's zone would fail them.
/// </summary>
public sealed class MeasuredAtTimeZoneTests
{
    private static readonly DateTime Expected = new(2026, 11, 13, 13, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("2026-11-13T14:00", "60")]   // Stockholm in winter
    [InlineData("2026-11-13T08:00", "-300")] // New York
    [InlineData("2026-11-13T13:00", "0")]
    [InlineData("2026-11-13T13:00", null)]   // no offset sent: read as UTC
    [InlineData("2026-11-13T13:00", "")]
    [InlineData("2026-11-13T13:00", "garbage")]
    [InlineData("2026-11-13T13:00", "900")]  // beyond 14 hours: not an offset, read as UTC
    public void ATypedTime_ShouldBeReadWithTheBrowsersOffset_OrAsUtc(string typed, string? offset)
    {
        Assert.Equal(Expected, BatteriesController.MeasuredAt(typed, offset));
    }

    [Fact]
    public void ABlankOrUnreadableTime_ShouldStillMeanNow()
    {
        Assert.Null(BatteriesController.MeasuredAt("", "60"));
        Assert.Null(BatteriesController.MeasuredAt("not a time", "60"));
    }

    [Fact]
    public void TheServersZone_ShouldNotBeUsedForATypedTime()
    {
        var controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "BatteriesController.cs"));

        Assert.DoesNotContain("TimeZoneInfo.Local", controller, StringComparison.Ordinal);
        Assert.Contains("form[MeasuredAtOffsetField]", controller, StringComparison.Ordinal);
        Assert.Contains("form[BatteriesController.MeasuredAtOffsetField]", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "ClusterBatteriesController.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void TheLiveValuesForm_ShouldSendTheOffset_AndSayHowATimeIsReadWithoutIt()
    {
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_LiveValuesCard.cshtml"));
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-battery-page.js"));

        Assert.Contains("name=\"@BatteriesController.MeasuredAtOffsetField\"", card, StringComparison.Ordinal);
        Assert.Contains("Read as UTC", card, StringComparison.Ordinal);
        Assert.Contains("measuredAtOffset", script, StringComparison.Ordinal);
        Assert.Contains("getTimezoneOffset", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-11-13T13:00:00")]
    [InlineData("2026-11-13T13:00:00Z")]
    [InlineData("2026-11-13T14:00:00+01:00")]
    [InlineData("2026-11-13T08:00:00-05:00")]
    public void ASentTime_ShouldBeUtcWithoutAnOffset_AndHonourOneWithIt(string sent)
    {
        Assert.True(MeasuredAtRule.TryReadSent(sent, out var utc));
        Assert.Equal(Expected, utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void AnUploadFile_ShouldReadATimeWithoutAnOffsetAsUtc()
    {
        var file = new JsonObject
        {
            ["format"] = NativeUploadFormat.Format,
            ["batteries"] = new JsonArray(new JsonObject { ["batterySerialNumber"] = "DEMO-C7M-0001", ["measuredAt"] = "2026-11-13T13:00:00", ["values"] = new JsonObject { ["stateOfChargeSoC"] = 50 } }),
        };

        var entry = new NativeUploadFormat().Read(file).Entries.Single();

        Assert.Equal(Expected, entry.MeasuredAtUtc);
    }

    [Fact]
    public void TheApiAndTheBatterySystemsExport_ShouldReadSentTimesWithTheOneRule()
    {
        foreach (var file in new[] { new[] { "web", "Controllers", "DppApiController.cs" }, new[] { "web", "Dpp", "Uploads", "UploadFile.cs" }, new[] { "web", "Dpp", "Uploads", "BmsExportFormat.cs" } })
        {
            var source = File.ReadAllText(DppTestFiles.RepoFile(file));
            Assert.Contains("MeasuredAtRule.TryReadSent(", source, StringComparison.Ordinal);
        }
    }
}
