using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the review of the built app (3 Nov 2026): the cluster admin's History and the team's audit log read
/// well. 28 demo live readings filled the History, so a run of live readings is one line; the History shows its latest
/// 10 lines and folds the others; the battery status reads "Re-used" as on the pages, not the code; the accident reports'
/// link reads as words, not an internal address; no stray space before a colon.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class HistoryReadsWellTests(MongoFixture mongo)
{
    private static BsonDocument Save(string actor, params (string Key, string Value)[] keys) => new()
    {
        ["eventId"] = Guid.NewGuid().ToString("N"),
        ["eventType"] = SavePipeline.AuditEventType,
        ["actor"] = actor,
        ["actorRole"] = "Team",
        ["source"] = "form",
        ["message"] = $"{keys.Length} value(s) saved on Pack pack-1",
        ["entityType"] = "dpp-pack",
        ["entityId"] = "pack-1",
        ["createdAt"] = "2026-11-03T12:00:00Z",
        ["metadata"] = new BsonDocument
        {
            ["keys"] = new BsonArray(keys.Select(key => new BsonDocument { ["key"] = key.Key, ["outcome"] = "Saved", ["message"] = string.Empty, ["previous"] = BsonNull.Value, ["value"] = key.Value })),
        },
    };

    private static (BsonDocument Event, AuditLine? Own) Item(BsonDocument document) => (document, null);

    [Fact]
    public void ARunOfLiveReadings_ShouldBeOneLine_AndOtherChangesBreakIt()
    {
        const string Soc = "{\"percentageValue\":61,\"percentUnit\":\"%\"}";
        var items = new[]
        {
            Item(Save("team", ("stateOfChargeSoC", Soc))),
            Item(Save("team", ("stateOfChargeSoC", Soc), ("temperatureInformation", "{\"value\":22}"))),
            Item(Save("team", ("stateOfChargeSoC", Soc))),
            Item(Save("team", ("capacityFade", "{\"percentageValue\":6,\"percentUnit\":\"%\"}"))), // not a live value
            Item(Save("team", ("stateOfChargeSoC", Soc))),
            Item(Save("feed", ("stateOfChargeSoC", Soc))), // another sender
        };

        var grouped = ClusterHistory.Grouped(items).ToArray();

        Assert.Equal([3, 1, 1, 1], grouped.Select(item => item.Readings));
        Assert.Same(items[0].Item1, grouped[0].Event); // the newest of the run stands for it
    }

    [Fact]
    public async Task ALinesValues_ShouldSayTheStatusInWords_AndTheAccidentPageByName()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var lines = new AuditLines(rig.Store, new DocumentStore(rig.Context), AttributeFormsTests.Forms, rig.Context);

        var line = Assert.Single(await lines.DescribeAsync([Save("team@example.com",
            ("batteryStatus", "{\"batteryStatusValues\":\"re-used\"}"),
            (DocumentService.Accidents, "{\"url\":\"http://localhost/dpp/packs/pack-1/accidents\"}"))]));

        Assert.Contains(line.Values, row => row.Name.StartsWith("Battery status", StringComparison.Ordinal) && row.After == "Re-used");
        Assert.Contains(line.Values, row => row.Name.StartsWith("Information on accidents", StringComparison.Ordinal) && row.After == "the accident reports page");
        Assert.DoesNotContain(line.Values, row => row.After.Contains("pack-1", StringComparison.Ordinal));
    }

    [Fact]
    public void TheHistory_ShouldShowItsLatestTenLines_AndFoldTheOthers_WithNoSpaceBeforeAColon()
    {
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "_HistoryCard.cshtml"));
        var auditLine = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_AuditLine.cshtml"));

        Assert.Equal(10, ClusterHistory.ShownOpen);
        Assert.Contains("Model.Take(ClusterHistory.ShownOpen)", card, StringComparison.Ordinal);
        Assert.Contains("<summary>Show @Words.Count(Model.Count - ClusterHistory.ShownOpen, \"earlier event\")</summary>", card, StringComparison.Ordinal);
        // The sentence's words on one line: a line break after </strong> showed as a space ("team : Battery").
        Assert.Contains("@foreach (var word in item.Sentence) { if (word.Strong) {<strong>@word.Text</strong>} else {@word.Text} }", card, StringComparison.Ordinal);
        Assert.Contains("@foreach (var word in Model.Sentence) { if (word.Strong) {<b>@word.Text</b>} else {@word.Text} }", auditLine, StringComparison.Ordinal);
    }
}
