using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The status change (design 7.4, 7.6 and 7.9, decisions 15, 19 and 34; step 4b): what happened sets the battery status
/// (#22), the fades and lifetime are re-stated with it, a repair joins the battery's repair history, and the passport
/// gets a new version. All or nothing.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class StatusChangeTests(MongoFixture mongo)
{
    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    [Fact]
    public void TheRestatedValues_ShouldBeTheFadesAndLifetimeWithRoundTripFadeOnlyWhereTheModelHasIt()
    {
        var with = new BatteryModel { Id = "m1", FamilyId = "f", Name = "With" };
        with.Values["energyRoundTripEfficiencyFade"] = new BatteryPassWeb.Dpp.Values.AttributeValue { Value = JsonNode.Parse("{\"percentageValue\":0,\"percentUnit\":\"%\"}")!, GeneratedAtUtc = DateTime.UtcNow, Source = "form" };
        var without = new BatteryModel { Id = "m2", FamilyId = "f", Name = "Without" };

        Assert.Equal([59, 69, 74, 79, 81], StatusChangeService.RestatedAttributes(with).Select(attribute => attribute.Number));
        Assert.Equal([59, 69, 79, 81], StatusChangeService.RestatedAttributes(without).Select(attribute => attribute.Number));
    }

    [Theory]
    [InlineData("original", StatusEvent.Repaired, "re-used")]
    [InlineData("re-used", StatusEvent.Repaired, "re-used")]
    [InlineData("remanufactured", StatusEvent.Repaired, "remanufactured")]
    [InlineData("repurposed", StatusEvent.Repaired, "repurposed")]
    [InlineData("original", StatusEvent.Waste, "waste")]
    [InlineData("re-used", StatusEvent.Waste, "waste")]
    [InlineData("repurposed", StatusEvent.Waste, "waste")]
    [InlineData("waste", StatusEvent.Repaired, null)]
    [InlineData("waste", StatusEvent.Waste, null)]
    [InlineData("unknown", StatusEvent.Repaired, null)]
    [InlineData("original", StatusEvent.Corrected, "original")]
    [InlineData("re-used", StatusEvent.Corrected, "re-used")]
    [InlineData("waste", StatusEvent.Corrected, "waste")]
    public void WhatHappened_ShouldSetTheStatusTheDesignSays(string current, StatusEvent what, string? expected)
    {
        Assert.Equal(expected, StatusChangeService.NewStatus(current, what));
    }

    [Fact]
    public async Task ARepair_ShouldMakeTheBatteryReUsedRecordItAndPublishANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        rig.Clock.Now = rig.Clock.Now.AddHours(1);

        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));

        Assert.Null(result.Problem);
        Assert.Equal(2, result.Version!.Number);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("re-used", Status(pack));
        Assert.Equal(6, pack.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Equal(12, pack.Values["expectedLifetimeInCalendarYears"].Value!.GetValue<int>()); // re-stated as it was
        var repair = Assert.Single(pack.Repairs);
        Assert.Equal("2026-10-20", repair.Date);
        Assert.Equal("Module 4 replaced after a coolant leak.", repair.Description);
        Assert.Equal("team@example.com", repair.RecordedBy);
        Assert.Equal(rig.Clock.Now.UtcDateTime, repair.RecordedAtUtc);
        Assert.Equal("re-used", result.Version.StaticCore["batteryPassportData"]!["identifiersAndProductData"]!["batteryStatus"]!["batteryStatusValues"]!.GetValue<string>());
        Assert.Equal(DppStatuses.Active, result.Version.DppStatus);
    }

    [Fact]
    public async Task ASecondRepair_ShouldKeepTheStatusAndAddToTheHistory()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));
        rig.Clock.Now = rig.Clock.Now.AddDays(1);

        var result = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "7", description: "Cooling plate resealed."));

        Assert.Null(result.Problem);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("re-used", Status(pack));
        Assert.Equal(["Module 4 replaced after a coolant leak.", "Cooling plate resealed."], pack.Repairs.Select(entry => entry.Description));
        Assert.Equal(3, result.Version!.Number);
    }

    [Fact]
    public async Task Waste_ShouldBeFinal()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        var waste = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await CurrentAsync(rig)));
        var after = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));

        Assert.Null(waste.Problem);
        Assert.Equal(DppStatuses.Active, waste.Version!.DppStatus); // the passport stays active (7.6)
        Assert.Equal(StatusChangeService.WasteIsFinal, after.Problem);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("waste", Status(pack));
        Assert.Empty(pack.Repairs);
        Assert.Equal(2, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task TheTeamsCorrection_ShouldReStateTheValuesKeepTheStatusAndAddNoHistory()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var values = await CurrentAsync(rig);
        values["powerFade"] = JsonValue.Create(3);

        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Corrected, values));

        Assert.Null(result.Problem);
        Assert.Equal(2, result.Version!.Number);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("original", Status(pack));
        Assert.Equal(3, pack.Values["powerFade"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Empty(pack.Repairs);
        var changed = await LastAsync(rig, StatusChangeService.ChangedEvent);
        Assert.Equal("Corrected", changed["metadata"]["what"].AsString);
        Assert.Contains("values corrected", changed["message"].AsString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACorrection_ShouldBeTheTeamsAndAlsoFixAWasteBattery()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Waste, await CurrentAsync(rig)));

        var byAdmin = await service.ChangeAsync(NorthAdmin, rig.PackId, new StatusChange(StatusEvent.Corrected, await CurrentAsync(rig)));
        var values = await CurrentAsync(rig);
        values["expectedLifetimeInCalendarYears"] = JsonValue.Create(0);
        var byTeam = await service.ChangeAsync(PublicationTesting.Team, rig.PackId, new StatusChange(StatusEvent.Corrected, values));

        Assert.Equal(StatusChangeService.OnlyTheTeamCorrects, byAdmin.Problem);
        Assert.Null(byTeam.Problem);
        Assert.Equal("waste", byTeam.NewStatus);
        Assert.Equal(3, byTeam.Version!.Number);
    }

    [Fact]
    public async Task ANote_ShouldJoinTheHistoryAsACorrectionWithoutANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);
        await service.ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));
        rig.Clock.Now = rig.Clock.Now.AddDays(1);

        var problem = await service.AddNoteAsync(NorthAdmin, rig.PackId, " The repair was on module 5, not module 4. ");

        Assert.Null(problem);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal([false, true], pack.Repairs.Select(entry => entry.IsCorrection));
        var note = pack.Repairs[1];
        Assert.Equal("The repair was on module 5, not module 4.", note.Description);
        Assert.Equal("north-admin@example.com", note.RecordedBy);
        Assert.Equal(nameof(SenderKind.ClusterAdmin), note.RecordedAs);
        Assert.Equal(rig.Clock.Now.UtcDateTime, note.RecordedAtUtc);
        Assert.Equal("2026-10-30", note.Date);
        Assert.Equal(2, await rig.Store.GetLatestVersionNumberAsync(rig.PackId)); // the repair's version, none for the note
        Assert.Equal("north-admin@example.com", (await LastAsync(rig, StatusChangeService.NoteAddedEvent))["actor"].AsString);
    }

    [Fact]
    public async Task ANote_ShouldNeedTextAPublishedPassportAndTheRightSender()
    {
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var service = Service(rig);

        Assert.Equal(StatusChangeService.NoteNeedsText, await service.AddNoteAsync(PublicationTesting.Team, rig.PackId, "  "));
        Assert.Equal(StatusChangeService.NotYours, await service.AddNoteAsync(ChangeSender.ClusterAdmin("south@example.com", BatteryTesting.South), rig.PackId, "A note."));
        Assert.Equal(StatusChangeService.OnlyPublished, await Service(draft).AddNoteAsync(PublicationTesting.Team, draft.PackId, "A note."));
        Assert.Equal(StatusChangeService.NoBattery, await service.AddNoteAsync(PublicationTesting.Team, "pack-unknown", "A note."));
        Assert.Empty((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs);
    }

    [Fact]
    public async Task TheBatterysClusterAdmin_ShouldChangeItsStatusAndThePublicationLogNameThem()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await Service(rig).ChangeAsync(NorthAdmin, rig.PackId, await RepairAsync(rig, capacityFade: "6"));

        Assert.Null(result.Problem);
        Assert.Equal("north-admin@example.com", Assert.Single((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs).RecordedBy);
        var published = await LastAsync(rig, BatteryPassWeb.Dpp.Publication.PublicationService.PublishedEvent);
        Assert.Equal("north-admin@example.com", published["actor"].AsString);
        Assert.Equal(nameof(SenderKind.ClusterAdmin), published["actorRole"].AsString);
        var changed = await LastAsync(rig, StatusChangeService.ChangedEvent);
        Assert.Equal("original", changed["metadata"]["from"].AsString);
        Assert.Equal("re-used", changed["metadata"]["to"].AsString);
        Assert.Equal("Repaired", changed["metadata"]["what"].AsString);
    }

    [Fact]
    public async Task AnotherClustersAdmin_ShouldChangeNothing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await Service(rig).ChangeAsync(ChangeSender.ClusterAdmin("south@example.com", BatteryTesting.South), rig.PackId, await RepairAsync(rig, capacityFade: "6"));

        Assert.Equal(StatusChangeService.NotYours, result.Problem);
        await AssertUnchangedAsync(rig);
    }

    [Fact]
    public async Task ADraft_ShouldNotChangeStatus()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade: "6"));

        Assert.Equal(StatusChangeService.OnlyPublished, result.Problem);
        Assert.Empty((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Repairs);
    }

    [Fact]
    public async Task AnUnknownBattery_ShouldNotBeFound()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        Assert.False((await Service(rig).ChangeAsync(PublicationTesting.Team, "pack-unknown", await RepairAsync(rig, capacityFade: "6"))).Found);
    }

    [Theory]
    [InlineData("140")]
    [InlineData("-3")]
    [InlineData("a lot")]
    [InlineData("")]
    public async Task ARefusedOrMissingRestatedValue_ShouldSaveNothing(string capacityFade)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, await RepairAsync(rig, capacityFade));

        Assert.Equal(StatusChangeService.ValuesRefused, result.Problem);
        Assert.Contains(result.Keys, key => key.Key == "capacityFade" && key.Outcome != KeyOutcome.Saved);
        await AssertUnchangedAsync(rig);
    }

    [Theory]
    [InlineData(null, "Module 4 replaced.", StatusChangeService.RepairNeedsDate)]
    [InlineData("2026-13-01", "Module 4 replaced.", StatusChangeService.RepairNeedsDate)]
    [InlineData("2099-01-01", "Module 4 replaced.", StatusChangeService.RepairNotInTheFuture)]
    [InlineData("2026-10-20", " ", StatusChangeService.RepairNeedsDescription)]
    public async Task ARepair_ShouldNeedItsDateAndWhatWasRepaired(string? date, string description, string problem)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var change = (await RepairAsync(rig, capacityFade: "6")) with { RepairDate = date, RepairDescription = description };

        Assert.Equal(problem, (await Service(rig).ChangeAsync(PublicationTesting.Team, rig.PackId, change)).Problem);
        await AssertUnchangedAsync(rig);
    }

    [Fact]
    public async Task TheSavePipeline_ShouldStillRefuseTheStatusAndFadesOutsideTheStatusChange()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(
            TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["batteryStatus"] = "waste", ["capacityFade"] = 9 }, "form"));

        Assert.All(result.Keys, key => Assert.Equal(KeyOutcome.Rejected, key.Outcome));
        await AssertUnchangedAsync(rig);
    }

    internal static StatusChangeService Service(PublicationRig rig) =>
        new(rig.Store, TestDemo.For(rig), rig.Pipeline, rig.Publication, new ApplicationAuditService(rig.Context), rig.Clock);

    internal static BatteryPassWeb.Dpp.Pages.StatusPages Pages(PublicationRig rig) =>
        new(rig.Store, PageTesting.Editor(rig), Service(rig), SuccessionTests.Service(rig), rig.Clock);

    /// <summary>The battery's current re-stated values as the window fills them in, as text.</summary>
    internal static async Task<Dictionary<string, JsonNode?>> CurrentAsync(PublicationRig rig)
    {
        var sources = (await rig.Store.LoadSourcesAsync(rig.PackId))!;
        return StatusChangeService.RestatedAttributes(sources.Model).ToDictionary(
            attribute => attribute.Property,
            attribute => (sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values[attribute.Property]).Value?.DeepClone(),
            StringComparer.Ordinal);
    }

    /// <summary>A repair with the window's values and one capacity fade typed (no blocking wait: xUnit's context has few threads).</summary>
    private static async Task<StatusChange> RepairAsync(PublicationRig rig, string capacityFade, string description = "Module 4 replaced after a coolant leak.")
    {
        var values = await CurrentAsync(rig);
        values["capacityFade"] = capacityFade.Length == 0 ? null : (JsonNode?)JsonValue.Create(capacityFade);
        if (decimal.TryParse(capacityFade, out var number))
        {
            values["capacityFade"] = JsonValue.Create(number);
        }

        return new StatusChange(StatusEvent.Repaired, values, "2026-10-20", description);
    }

    private static string Status(BatteryPack pack) => pack.Values["batteryStatus"].Value!["batteryStatusValues"]!.GetValue<string>();

    private static async Task AssertUnchangedAsync(PublicationRig rig)
    {
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        Assert.Equal("original", Status(pack));
        Assert.Equal(4, pack.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>());
        Assert.Empty(pack.Repairs);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
