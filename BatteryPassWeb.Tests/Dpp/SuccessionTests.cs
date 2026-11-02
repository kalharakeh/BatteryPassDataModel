using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The new linked passport (design 7.5, decisions 13, 19 and 34; step 4c-1): placed on the market again, the old passport
/// waits for its successor; Scania's is a new draft made by the team, another operator's is linked by its ID or address;
/// once the new one is published or linked, the old one is inactive and takes no more changes.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SuccessionTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    private static readonly ChangeSender NorthAdmin = ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North);

    [Theory]
    [InlineData(StatusEvent.PlacedOnMarketAgain, "re-used")]
    [InlineData(StatusEvent.Remanufactured, "remanufactured")]
    [InlineData(StatusEvent.Repurposed, "repurposed")]
    public async Task PlacedOnTheMarketAgain_ShouldSetTheStatusAndWaitForTheNewPassport(StatusEvent what, string status)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var result = await StatusChangeTests.Service(rig).ChangeAsync(NorthAdmin, rig.PackId, new StatusChange(what, await StatusChangeTests.CurrentAsync(rig)));

        Assert.Null(result.Problem);
        Assert.Equal(status, result.NewStatus);
        Assert.Equal(DppStatuses.Active, result.Version!.DppStatus); // J-13: still active until the new one comes
        var succession = (await PackAsync(rig)).Succession!;
        Assert.Equal("original", succession.StatusBefore);
        Assert.Equal(status, succession.Status);
        Assert.Null(succession.SuccessorPackId);
        Assert.Null(succession.LinkedAtUtc);
    }

    [Fact]
    public async Task AWaitingPassport_ShouldTakeNoOtherStatusChangeButTheTeamsCorrection()
    {
        var rig = await RepurposedAsync();
        var service = StatusChangeTests.Service(rig);

        var repair = await service.ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Repaired, await StatusChangeTests.CurrentAsync(rig), "2026-10-20", "Fan."));
        var correction = await service.ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Corrected, await StatusChangeTests.CurrentAsync(rig)));

        Assert.Equal(StatusChangeService.WaitingForSuccessor, repair.Problem);
        Assert.Null(correction.Problem);
        Assert.Equal("repurposed", correction.NewStatus);
    }

    [Fact]
    public async Task TheTeam_ShouldMakeTheNewPassportAsADraftThatStartsFromTheOldBattery()
    {
        var rig = await RepurposedAsync();
        var old = await PackAsync(rig);

        var (successorId, problem) = await Service(rig).CreateSuccessorAsync(Team, rig.PackId, old.OperatorId);

        Assert.Null(problem);
        var successor = (await rig.Store.GetAsync<BatteryPack>(successorId!))!;
        Assert.NotEqual(Text(old, "uniqueProductIdentifier"), Text(successor, "uniqueProductIdentifier")); // a new #6 (J-2)
        Assert.Equal(Text(old, "batterySerialNumber"), Text(successor, "batterySerialNumber"));
        Assert.Equal(Text(old, "thirdPartyServiceProvider"), Text(successor, "thirdPartyServiceProvider"));
        Assert.Equal(Text(old, "manufacturingDate"), Text(successor, "manufacturingDate"));
        Assert.False(successor.Values.ContainsKey("warrantyPeriod")); // the team types the new warranty
        Assert.Equal("repurposed", successor.Values["batteryStatus"].Value!["batteryStatusValues"]!.GetValue<string>());
        Assert.Equal(4, successor.Values["capacityFade"].Value!["percentageValue"]!.GetValue<int>()); // the re-stated values
        Assert.Equal(old.Values["stateOfChargeSoC"].Value!.ToJsonString(), successor.Values["stateOfChargeSoC"].Value!.ToJsonString()); // the last measured values
        Assert.Equal(old.Values["stateOfChargeSoC"].GeneratedAtUtc, successor.Values["stateOfChargeSoC"].GeneratedAtUtc);
        Assert.NotEqual(old.Values["informationOnAccidents"].Value!.ToJsonString(), successor.Values["informationOnAccidents"].Value!.ToJsonString()); // its own accident page
        Assert.Equal(old.BatchId, successor.BatchId);
        Assert.Equal(old.ClusterId, successor.ClusterId);
        Assert.Equal(rig.PackId, successor.SuccessorOf);
        Assert.Empty(successor.Repairs);
        Assert.Equal(successorId, (await PackAsync(rig)).Succession!.SuccessorPackId);
        Assert.Equal(0, await rig.Store.GetLatestVersionNumberAsync(successorId!)); // a draft
        Assert.NotNull(await rig.Store.GetActiveVersionAsync(rig.PackId)); // the old one stays active (J-13)
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldMakeScaniasNewPassportAndOnlyWhenOneIsAwaited()
    {
        var waiting = await RepurposedAsync();
        var plain = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var old = await PackAsync(waiting);

        Assert.Equal(SuccessionService.TeamMakesIt, (await Service(waiting).CreateSuccessorAsync(NorthAdmin, waiting.PackId, old.OperatorId)).Problem);
        Assert.Equal(SuccessionService.NotAwaited, (await Service(plain).CreateSuccessorAsync(Team, plain.PackId, old.OperatorId)).Problem);
        Assert.Equal(SuccessionService.NoOperator, (await Service(waiting).CreateSuccessorAsync(Team, waiting.PackId, "operator-unknown")).Problem);
        var made = await Service(waiting).CreateSuccessorAsync(Team, waiting.PackId, old.OperatorId);
        Assert.Equal(SuccessionService.AlreadyMade, (await Service(waiting).CreateSuccessorAsync(Team, waiting.PackId, old.OperatorId)).Problem);
        Assert.Null(made.Problem);
    }

    [Fact]
    public async Task PublishingTheNewPassport_ShouldMakeTheOldOneInactive()
    {
        var rig = await RepurposedAsync();
        var (successorId, _) = await Service(rig).CreateSuccessorAsync(Team, rig.PackId, (await PackAsync(rig)).OperatorId);
        await TypeWarrantyAsync(rig, successorId!);
        rig.Clock.Now = rig.Clock.Now.AddHours(1);

        var published = await rig.Publication.PublishAsync(Team, successorId!);

        Assert.Null(published.Problem);
        Assert.Null(await rig.Store.GetActiveVersionAsync(rig.PackId));
        var last = (await rig.Store.GetLatestVersionAsync(rig.PackId))!;
        Assert.Equal(DppStatuses.Inactive, last.DppStatus);
        Assert.Equal(rig.Clock.Now.UtcDateTime, last.LastUpdatedUtc); // #4: the status was made available now
        Assert.Equal(rig.Clock.Now.UtcDateTime, (await PackAsync(rig)).Succession!.LinkedAtUtc);
        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
        var entry = await LastAsync(rig, PublicationService.InactiveEvent);
        Assert.Equal(rig.PackId, entry["entityId"].AsString);
    }

    [Fact]
    public async Task AnInactivePassport_ShouldTakeNoMoreChanges()
    {
        var rig = await InactiveAsync();

        var live = await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms", BatteryTesting.North), new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 40 }, "feed"));
        var status = await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Corrected, await StatusChangeTests.CurrentAsync(rig)));
        var note = await StatusChangeTests.Service(rig).AddNoteAsync(Team, rig.PackId, "A note.");
        var publish = await rig.Publication.PublishAsync(Team, rig.PackId);

        Assert.False(live.Saved);
        Assert.All(live.Keys, key => Assert.Equal(KeyOutcome.Rejected, key.Outcome));
        Assert.Contains("inactive", Assert.Single(live.Keys).Message, StringComparison.Ordinal);
        Assert.Equal(StatusChangeService.NoLongerActive("inactive"), status.Problem);
        Assert.Equal(StatusChangeService.NoLongerActive("inactive"), note);
        Assert.Equal(PublicationService.NoNewVersion("inactive"), publish.Problem);
        Assert.Equal(DppStatuses.Inactive, (await rig.Store.GetLatestVersionAsync(rig.PackId))!.DppStatus);
    }

    [Fact]
    public async Task TheSerialNumber_ShouldFindTheBatteryThatIsNotInactiveAndTheOldIdItsOwnPassport()
    {
        var rig = await InactiveAsync();
        var old = await PackAsync(rig);
        var serial = Text(old, "batterySerialNumber")!;
        var successorId = old.Succession!.SuccessorPackId!;

        var bySerial = await PassportPagesTests.Pages(rig).FindAsync(serial);
        var byOldId = await PassportPagesTests.Pages(rig).FindAsync(Text(old, "uniqueProductIdentifier"));
        var feedTarget = await BatteryApiTests.Api(rig).FindAsync(ChangeSender.Feed("bms", BatteryTesting.North), serial);
        var teamTarget = await BatteryApiTests.Api(rig).FindAsync(Team, serial);

        Assert.Equal(successorId, bySerial!.PackId);
        Assert.Equal(rig.PackId, byOldId!.PackId);
        Assert.Equal(DppStatuses.Inactive, byOldId.DppStatus);
        Assert.Equal(successorId, feedTarget.Pack?.Id); // feeds by serial number reach the battery that isn't inactive
        Assert.Equal(successorId, teamTarget.Pack?.Id);
    }

    [Fact]
    public async Task AnotherOperator_ShouldBeSavedFirstAndLinkedOnceItsPassportIsKnown()
    {
        var rig = await RepurposedAsync();
        var service = Service(rig);
        var details = new OtherOperator("urn:test:operator:reman-ab", "Example Remanufacturing AB, Box 1, 111 11 Stockholm", null);

        var saved = await service.SaveOtherOperatorAsync(NorthAdmin, rig.PackId, details, link: false);
        var tooEarly = await service.SaveOtherOperatorAsync(NorthAdmin, rig.PackId, details, link: true);
        var otherCluster = await service.SaveOtherOperatorAsync(ChangeSender.ClusterAdmin("south@example.com", BatteryTesting.South), rig.PackId, details, link: false);
        Assert.NotNull(await rig.Store.GetActiveVersionAsync(rig.PackId));
        var linked = await service.SaveOtherOperatorAsync(NorthAdmin, rig.PackId, details with { Passport = "https://example.com/passports/reman-1" }, link: true);

        Assert.Null(saved);
        Assert.Equal(SuccessionService.LinkNeedsPassport, tooEarly);
        Assert.Equal("urn:test:operator:reman-ab", (await PackAsync(rig)).Succession!.OtherOperatorId); // kept for the next try
        Assert.Equal(SuccessionService.NotYours, otherCluster);
        Assert.Null(linked);
        var succession = (await PackAsync(rig)).Succession!;
        Assert.Equal("Example Remanufacturing AB, Box 1, 111 11 Stockholm", succession.OtherOperatorInfo);
        Assert.Equal("https://example.com/passports/reman-1", succession.OtherPassport);
        Assert.NotNull(succession.LinkedAtUtc);
        Assert.Equal(DppStatuses.Inactive, await rig.Store.PassportStatusAsync(rig.PackId));
    }

    [Fact]
    public async Task TheTeam_ShouldCancelTheNewPassportAndPutTheOldStatusBack()
    {
        var rig = await RepurposedAsync();
        var service = Service(rig);
        var (successorId, _) = await service.CreateSuccessorAsync(Team, rig.PackId, (await PackAsync(rig)).OperatorId);

        Assert.Equal(SuccessionService.TeamCancels, await service.CancelAsync(NorthAdmin, rig.PackId));
        var cancelled = await service.CancelAsync(Team, rig.PackId);

        Assert.Null(cancelled);
        var pack = await PackAsync(rig);
        Assert.Null(pack.Succession);
        Assert.Equal("original", pack.Values["batteryStatus"].Value!["batteryStatusValues"]!.GetValue<string>());
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(successorId!));
        Assert.Equal(3, (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Number); // published, repurposed, put back
        Assert.Equal(rig.PackId, (await LastAsync(rig, SuccessionService.CancelledEvent))["entityId"].AsString);
    }

    [Fact]
    public async Task AnInactivePassport_ShouldNotBeCancelled()
    {
        var rig = await InactiveAsync();

        Assert.Equal(SuccessionService.NotAwaited, await Service(rig).CancelAsync(Team, rig.PackId));
    }

    [Fact]
    public async Task TheNewPassport_ShouldKeepItsSerialNumberWhileADraft()
    {
        var rig = await RepurposedAsync();
        var (successorId, _) = await Service(rig).CreateSuccessorAsync(Team, rig.PackId, (await PackAsync(rig)).OperatorId);
        var serial = Text(await PackAsync(rig), "batterySerialNumber")!;

        var same = await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, successorId!, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = $" {serial.ToLowerInvariant()} " }, "form"));
        var another = await new PackCreationService(rig.Store, rig.Pipeline, new BatteryPassWeb.Dpp.Identifiers.TestIdentifierScheme(), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock)
            .CreateAsync(Team, new NewPack(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, BatteryTesting.North, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, "form"));

        Assert.DoesNotContain(same.Keys, key => key.Message == SavePipeline.SerialNumberTaken);
        Assert.Null(another.Pack); // an unrelated battery still can't take it
    }

    internal static SuccessionService Service(PublicationRig rig) =>
        new(rig.Store, BatteryTesting.Creation(rig), rig.Pipeline, rig.Publication, new ApplicationAuditService(rig.Context), rig.Clock);

    /// <summary>The seed pack, published and then repurposed by the team: waiting for its new passport.</summary>
    internal async Task<PublicationRig> RepurposedAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var result = await StatusChangeTests.Service(rig).ChangeAsync(Team, rig.PackId, new StatusChange(StatusEvent.Repurposed, await StatusChangeTests.CurrentAsync(rig)));
        Assert.Null(result.Problem);
        return rig;
    }

    /// <summary>Repurposed, with Scania's new passport published: the old passport is inactive.</summary>
    internal async Task<PublicationRig> InactiveAsync()
    {
        var rig = await RepurposedAsync();
        var (successorId, problem) = await Service(rig).CreateSuccessorAsync(Team, rig.PackId, (await PackAsync(rig)).OperatorId);
        Assert.Null(problem);
        await TypeWarrantyAsync(rig, successorId!);
        Assert.Null((await rig.Publication.PublishAsync(Team, successorId!)).Problem);
        return rig;
    }

    private static async Task TypeWarrantyAsync(PublicationRig rig, string packId) =>
        Assert.True((await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, packId, new Dictionary<string, JsonNode?> { ["warrantyPeriod"] = "P5Y" }, "form"))).Saved);

    private static Task<BatteryPack> PackAsync(PublicationRig rig) => rig.Store.GetAsync<BatteryPack>(rig.PackId)!;

    private static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static async Task<BsonDocument> LastAsync(PublicationRig rig, string eventType) =>
        await rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", eventType))
            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
            .FirstAsync();
}
