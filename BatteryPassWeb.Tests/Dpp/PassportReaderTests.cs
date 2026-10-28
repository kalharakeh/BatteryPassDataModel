using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class PassportReaderTests(MongoFixture mongo)
{
    [Fact]
    public async Task Current_ShouldBeTheSignedCoreWithTheHeaderAndTheLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var current = await new PassportReader(rig.Store).CurrentAsync(rig.PackId);

        Assert.NotNull(current);
        var passport = current.Passport;
        Assert.Equal("active", passport["dppHeader"]!["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Equal("2026-10-29T08:00:00Z", passport["dppHeader"]!["lastUpdated"]!.GetValue<string>());
        Assert.Equal(76, passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]!["percentageValue"]!.GetValue<int>());
        Assert.Empty(DppStoreTesting.Schemas.CheckPassport(passport, feb2027Variant: false));
    }

    [Fact]
    public async Task Current_ShouldShowTheLatestLiveValue()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        rig.Clock.Now = rig.Clock.Now.AddMinutes(10);
        await rig.Pipeline.SaveAsync(
            ChangeSender.Feed("bms-feed", "cluster-test"),
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 55 }, "feed"));

        var passport = (await new PassportReader(rig.Store).CurrentAsync(rig.PackId))!.Passport;

        Assert.Equal(55, passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]!["percentageValue"]!.GetValue<int>());
        Assert.Equal("2026-10-29T08:10:00Z", passport["dppHeader"]!["lastUpdated"]!.GetValue<string>());
    }

    [Fact]
    public async Task ArchivedVersion_ShouldStayReadableByItsPassportIdWithoutLiveValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var first = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["thirdPartyServiceProvider"] = "New Hosting AB" }, "form"));

        var archived = await new PassportReader(rig.Store).VersionAsync(first.DigitalProductPassportId);

        Assert.NotNull(archived);
        Assert.Equal("archived", archived.Passport["dppHeader"]!["dppStatus"]!["dppStatusValue"]!.GetValue<string>());
        Assert.Equal("Example Passport Hosting AB", archived.Passport["batteryPassportData"]!["identifiersAndProductData"]!["thirdPartyServiceProvider"]!.GetValue<string>());
        Assert.Null(archived.Passport["batteryPassportData"]!["performanceAndDurability"]!["stateOfChargeSoC"]);
        Assert.True(PassportReader.Verify(archived.Version));
    }

    [Fact]
    public async Task ChangedStoredCore_ShouldFailVerification()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", version.Id),
            Builders<BsonDocument>.Update.Set("StaticCore.batteryPassportData.identifiersAndProductData.batterySerialNumber", "TAMPERED"));

        var reloaded = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;

        Assert.False(PassportReader.Verify(reloaded));
    }

    [Fact]
    public async Task UnknownPackOrPassportId_ShouldReadAsNothing()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var reader = new PassportReader(rig.Store);

        Assert.Null(await reader.CurrentAsync(rig.PackId)); // a draft has no published passport
        Assert.Null(await reader.VersionAsync("urn:x-battery-pass:TEST-DPP-none"));
    }
}
