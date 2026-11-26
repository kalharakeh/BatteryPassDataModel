using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class PublicationServiceTests(MongoFixture mongo)
{
    [Fact]
    public async Task Team_ShouldPublishADraftAsSignedVersion1()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.Null(result.Problem);
        var version = result.Version!;
        Assert.Equal(1, version.Number);
        Assert.Equal(DppStatuses.Active, version.DppStatus);
        Assert.StartsWith(TestIdentifierScheme.Prefix + "DPP-", version.DigitalProductPassportId, StringComparison.Ordinal);
        Assert.Equal(DppStoreTesting.Now.UtcDateTime, version.PublishedAtUtc);
        Assert.Equal(DppStoreTesting.Now.UtcDateTime, version.LastUpdatedUtc);
        Assert.Equal("team@example.com", version.PublishedBy);
        var stored = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        Assert.True(PassportSigner.Verify(stored.StaticCore, stored.Proof));
        Assert.Equal(version.DigitalProductPassportId, version.StaticCore["dppHeader"]!["digitalProductPassportId"]!.GetValue<string>());
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Contains(events, e => e["eventType"].AsString == PublicationService.PublishedEvent && e["entityId"].AsString == rig.PackId);
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldPublish()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Publication.PublishAsync(ChangeSender.ClusterAdmin("admin@example.com", "cluster-test"), rig.PackId);

        Assert.Contains("only the team", result.Problem, StringComparison.Ordinal);
        Assert.Empty(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task IncompletePack_ShouldNotBePublished()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await RemoveStoredValueAsync(rig, "thirdPartyServiceProvider");

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.Null(result.Version);
        Assert.Contains(result.Report!.Blocking, item => item.StartsWith("#13 ", StringComparison.Ordinal));
        Assert.Empty(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task MissingPack_ShouldBeReported()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, "no-such-pack");

        Assert.Contains("not found", result.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishingAgainWithoutChanges_ShouldNotCreateAVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var again = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.True(again.Unchanged);
        Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task VersionNumber_ShouldFollowTheHighestStoredOne()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var core = PassportStaticCore.Extract(PassportAssembler.Assemble(rig.Seed.Sources, rig.Seed.Header));
        await rig.Store.InsertAsync(new PassportVersion
        {
            Id = "version-earlier",
            PackId = rig.PackId,
            Number = 2,
            DigitalProductPassportId = "urn:x-battery-pass:TEST-DPP-earlier",
            DppStatus = DppStatuses.Archived,
            PublishedAtUtc = DppStoreTesting.Now.UtcDateTime,
            LastUpdatedUtc = DppStoreTesting.Now.UtcDateTime,
            PublishedBy = "team@example.com",
            StaticCore = core,
            Proof = new PassportSigner(SigningTesting.Keys).Sign(core, DppStoreTesting.Now.UtcDateTime),
        });

        var result = await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        Assert.Equal(3, result.Version?.Number);
    }

    internal static Task RemoveStoredValueAsync(PublicationRig rig, string property) =>
        rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Packs).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", rig.PackId),
            Builders<BsonDocument>.Update.Unset($"Values.{property}"));
}
