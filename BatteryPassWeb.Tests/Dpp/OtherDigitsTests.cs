using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Uploads;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round four, 6: digits of other alphabets (Arabic-Indic, full-width) in a warranty or an export's readings are refused
/// or left out like any other bad value, never a failure, through the API, an upload or the feed.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class OtherDigitsTests(MongoFixture mongo)
{
    [Theory]
    [InlineData("P٣Y")]
    [InlineData("P１２M")]
    public async Task AWarrantyInOtherDigits_ShouldBeRefusedInAnUpload(string warranty)
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var report = await UploadServiceTests.Run(rig, PublicationTesting.Team, UploadServiceTests.File(UploadServiceTests.Entry(UploadServiceTests.Serial(rig), $$"""{ "warrantyPeriod": "{{warranty}}" }""")));

        Assert.Equal(KeyOutcome.Rejected, Assert.Single(Assert.Single(report.Batteries).Values).Outcome);
    }

    [Fact]
    public void AnExportsReadingsInOtherDigits_ShouldBeLeftOutWithANote()
    {
        var export = BmsExportUploadTests.Export("VPC9-0300");
        export["values"]!["stateOfCharge"] = new JsonObject { ["raw"] = 1, ["formatted"] = "٢٧ %" };
        export["values"]!["remainingCapacityAh"] = new JsonObject { ["raw"] = 1, ["formatted"] = "１２６ Ah" };

        var entry = Assert.Single(new BmsExportFormat().Read(export).Entries);

        Assert.False(entry.Values.ContainsKey("stateOfChargeSoC"));
        Assert.Null(entry.RemainingCapacityAh);
        Assert.Contains(entry.Notes!, note => note.Contains("isn't a percentage", StringComparison.Ordinal));
    }
}
