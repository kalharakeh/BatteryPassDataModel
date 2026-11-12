using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Saves after publication keep the passport's versions current (design 4.2).</summary>
[Collection(MongoCollection.Name)]
public sealed class VersioningTests(MongoFixture mongo)
{
    [Fact]
    public async Task StaticChange_ShouldPublishANewVersionAndArchiveTheOld()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddHours(1);

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, PackChange(rig, ("thirdPartyServiceProvider", "\"New Hosting AB\"")));

        var versions = await rig.Store.ListVersionsAsync(rig.PackId);
        Assert.Equal([DppStatuses.Archived, DppStatuses.Active], versions.Select(v => v.DppStatus));
        var current = versions[1];
        Assert.Equal(2, current.Number);
        Assert.Equal("New Hosting AB", current.StaticCore["batteryPassportData"]!["identifiersAndProductData"]!["thirdPartyServiceProvider"]!.GetValue<string>());
        Assert.Equal(rig.Clock.Now.UtcDateTime, current.PublishedAtUtc);
        Assert.NotEqual(versions[0].DigitalProductPassportId, current.DigitalProductPassportId);
        Assert.True(PassportSigner.Verify(current.StaticCore, current.Proof));
    }

    [Fact]
    public async Task LiveChange_ShouldOnlyMoveTheLastUpdateToTheSaveTime()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddHours(2);
        var measured = rig.Clock.Now.UtcDateTime.AddMinutes(-30);
        var change = new ChangeSet(TargetKind.Pack, rig.PackId, Values(("stateOfChargeSoC", "55")), "feed", measured);

        await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), change);

        var version = Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId));
        Assert.Equal(rig.Clock.Now.UtcDateTime, version.LastUpdatedUtc); // #4: made available now
        Assert.Equal(measured, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"].GeneratedAtUtc); // when it was measured
    }

    [Theory]
    [InlineData(TargetKind.Model, "model-test", "ratedCapacity", "210")]
    [InlineData(TargetKind.Batch, "batch-test-2026", "carbonFootprintPerformanceClass", "\"Class A\"")]
    [InlineData(TargetKind.Site, "site-test", "manufacturingPlace", "\"New plant, Stockholm, Sweden\"")]
    [InlineData(TargetKind.Operator, "operator-test", "economicOperatorId", "\"urn:test:operator:renamed\"")]
    public async Task SharedRecordChange_ShouldRepublishItsPublishedPacks(TargetKind target, string id, string key, string json)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(target, id, Values((key, json)), "form"));

        Assert.True(result.Saved);
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Fact]
    public async Task ChangeToADraft_ShouldNotPublishIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, PackChange(rig, ("thirdPartyServiceProvider", "\"Draft Hosting AB\"")));

        Assert.Empty(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task ClearingAValue_ShouldKeepTheActiveVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, PackChange(rig, ("thirdPartyServiceProvider", "\"\"")));

        Assert.Equal(KeyOutcome.KeptEmpty, Assert.Single(result.Keys).Outcome);
        Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task BlockedRepublish_ShouldKeepTheActiveVersionAndBeLogged()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await PublicationServiceTests.RemoveStoredValueAsync(rig, "thirdPartyServiceProvider"); // e.g. data damaged outside the app

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, "model-test", Values(("ratedCapacity", "210")), "form"));

        Assert.True(result.Saved); // the save itself stands
        var version = Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId));
        Assert.Equal(DppStatuses.Active, version.DppStatus);
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var blocked = Assert.Single(events, e => e["eventType"].AsString == PublicationService.BlockedEvent);
        Assert.Contains("#13", blocked["metadata"]["blocking"][0].AsString, StringComparison.Ordinal);
    }

    private static ChangeSet PackChange(PublicationRig rig, params (string Key, string Json)[] values) =>
        new(TargetKind.Pack, rig.PackId, Values(values), "form");

    private static Dictionary<string, JsonNode?> Values(params (string Key, string Json)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonNode.Parse(v.Json), StringComparer.Ordinal);
}
