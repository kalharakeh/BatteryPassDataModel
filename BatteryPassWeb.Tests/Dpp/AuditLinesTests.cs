using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The audit log in plain English: one sentence per event, its details, and the events grouped by day.</summary>
[Collection(MongoCollection.Name)]
public sealed class AuditLinesTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    private static AuditLines Lines(PublicationRig rig) =>
        new(rig.Store, new DocumentStore(rig.Context), AttributeFormsTests.Forms, rig.Context);

    private static async Task<IReadOnlyList<AuditLine>> LinesAsync(PublicationRig rig, string? eventType = null)
    {
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var lines = await Lines(rig).DescribeAsync(events);
        return eventType is null ? lines : lines.Where(line => line.EventType == eventType).ToArray();
    }

    private static Task<SaveResult> SaveAsync(PublicationRig rig, params (string Key, string Json)[] values) =>
        rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(
            TargetKind.Model,
            "model-test",
            values.ToDictionary(value => value.Key, value => JsonNode.Parse(value.Json)),
            "form"));

    [Fact]
    public async Task AChange_ShouldReadAsOneSentenceWithItsValueBeforeAndAfter()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;

        await SaveAsync(rig, ("batteryMass", """{"gramKilogramValue":600,"gramKilogramUnit":"kg"}"""));

        var line = Assert.Single(await LinesAsync(rig, SavePipeline.AuditEventType));
        Assert.Equal($"{Who} changed the battery mass of model {model.Name}", line.Text);
        Assert.EndsWith("→ 600 kg", line.Change);
        Assert.Equal(AuditIcon.Edit, line.Icon);
        Assert.Equal(["Battery model", "Team page"], line.Tags);
        var row = Assert.Single(line.Values);
        Assert.Equal(("Battery mass (#21)", "600 kg", "Saved"), (row.Name, row.After, row.Result));
        Assert.Contains(line.Facts, fact => fact.Term == "Record" && fact.Href == "/admin/families/family-test?model=model-test#editor");
    }

    [Fact]
    public async Task AnOlderChange_ShouldSayItsNewValueWasNotRecorded()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var key = new BsonDocument { ["key"] = "batteryMass", ["outcome"] = "Saved", ["message"] = "", ["previous"] = """{"gramKilogramValue":500,"gramKilogramUnit":"kg"}""" };
        await new ApplicationAuditService(rig.Context).AppendApplicationAuditEventAsync(
            SavePipeline.AuditEventType, Who, "Team", "form", "1 value(s) saved", new BsonDocument { ["keys"] = new BsonArray { key } }, "dpp-model", "model-test");

        var line = Assert.Single(await LinesAsync(rig, SavePipeline.AuditEventType));

        Assert.Null(line.Change);
        Assert.Equal(("500 kg", "Not recorded"), (line.Values[0].Before, line.Values[0].After));
    }

    [Fact]
    public async Task ARemoval_ShouldSayWhatWasRemovedAndWhatItWas()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(
            TargetKind.Model, "model-test", new Dictionary<string, JsonNode?>(), "form", Removals: ["batteryMass"]));

        var line = Assert.Single(await LinesAsync(rig, SavePipeline.AuditEventType));
        Assert.Equal($"{Who} removed the battery mass of model {model.Name}", line.Text);
        Assert.StartsWith("was ", line.Change);
        Assert.EndsWith(" kg", line.Change);
        Assert.Equal(AuditIcon.Remove, line.Icon);
    }

    [Fact]
    public async Task SeveralValues_ShouldBeCountedWithTheRefusedOnesInTheDetails()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;

        await SaveAsync(
            rig,
            ("batteryMass", """{"gramKilogramValue":600,"gramKilogramUnit":"kg"}"""),
            ("ratedCapacity", """{"amperehourMiliamperehourValue":210,"ampereHourMiliamperehourUnit":"Ah"}"""),
            ("capacityFade", """{"percentageValue":120,"percentUnit":"%"}"""));

        var line = Assert.Single(await LinesAsync(rig, SavePipeline.AuditEventType));
        Assert.Equal($"{Who} saved 2 values of model {model.Name}; 1 was refused", line.Text);
        Assert.Null(line.Change);
        Assert.Equal(3, line.Values.Count);
        var refused = Assert.Single(line.Values, row => row.Refused);
        Assert.StartsWith("Capacity fade (#59)", refused.Name);
        Assert.StartsWith("Refused: ", refused.Result);
    }

    [Fact]
    public async Task RecordEvents_ShouldNameTheRecordEvenOnceItIsGone()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var familyId = await pages.CreateFamilyAsync(Who, "Audit family");
        await pages.RenameAsync<ManufacturingSite>(Who, "site-test", "Example plant", "Plant B");
        await pages.RemoveAsync(Who, RecordKind.Family, familyId);

        var lines = await LinesAsync(rig);

        Assert.Contains(lines, line => line.Text == $"{Who} added battery family Audit family" && line.Icon == AuditIcon.Add);
        Assert.Contains(lines, line => line.Text == $"{Who} renamed manufacturing site Example plant to Plant B");
        Assert.Contains(lines, line => line.Text == $"{Who} removed battery family Audit family" && line.Icon == AuditIcon.Remove);
    }

    [Fact]
    public async Task APersonWithAnAccount_ShouldBeNamed()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Context.Database!.GetCollection<BsonDocument>("users").InsertOneAsync(new BsonDocument { ["email"] = Who, ["name"] = "Kassem" });

        await SaveAsync(rig, ("batteryMass", """{"gramKilogramValue":600,"gramKilogramUnit":"kg"}"""));

        var line = Assert.Single(await LinesAsync(rig, SavePipeline.AuditEventType));
        Assert.StartsWith("Kassem changed the battery mass", line.Text);
        Assert.Contains(line.Facts, fact => fact.Term == "Who" && fact.Detail.StartsWith(Who, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OtherEvents_ShouldStillReadPlainly()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await new ApplicationAuditService(rig.Context).AppendApplicationAuditEventAsync(
            "auth.login-failed", "someone@example.com", "user", "admin-ui", "Sign-in refused: wrong password", entityType: "account");

        var line = Assert.Single(await LinesAsync(rig, "auth.login-failed"));

        Assert.Equal("someone@example.com: Sign-in refused: wrong password", line.Text);
        Assert.Contains("Account", line.Tags);
    }

    [Fact]
    public void Days_ShouldBeNewestFirstWithTodayAndYesterdayNamed()
    {
        var now = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        AuditLine At(string id, DateTimeOffset when) => new(id, "x", when, AuditIcon.Other, false, [new AuditWord(id)], null, [], [], [], "{}");

        var days = AuditLines.ByDay(
            [At("a", now.AddHours(-1)), At("b", now.AddHours(-2)), At("c", now.AddDays(-1)), At("d", now.AddDays(-3))],
            TimeZoneInfo.Utc,
            now);

        Assert.Equal(["Today · Thursday 29 October 2026", "Yesterday · Wednesday 28 October 2026", "Monday 26 October 2026"], days.Select(day => day.Title));
        Assert.Equal(["a", "b"], days[0].Lines.Select(line => line.Id));
    }
}
