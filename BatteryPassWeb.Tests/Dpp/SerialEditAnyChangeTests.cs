using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S1 round two, point 2: whenever a draft's serial number (#11) changes at all, the serial rule and the model's format
/// apply; "unchanged" means the very same text, so a change of dashes or separators alone is checked too. Letters that
/// .NET upper-cases into A–Z (<c>ſ</c>, <c>ı</c>) are refused wherever a serial number is typed.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SerialEditAnyChangeTests(MongoFixture mongo)
{
    private const string Core800s = "VPC8C05s-#####";

    private static Task<SaveResult> SetSerialAsync(DemoRig rig, string packId, string serial) =>
        rig.Services.GetRequiredService<SavePipeline>().SaveAsync(
            ChangeSender.Team(Who),
            new ChangeSet(TargetKind.Pack, packId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, "api"));

    private static async Task<(DemoRig Rig, BatteryIdPool Pool, BatteryPack Draft, BatteryModel Model)> DraftAsync(MongoFixture mongo)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        var modelId = (await rig.Store.GetAsync<ProductionBatch>(c7.BatchId))!.ModelId;
        await rig.Store.SetModelSerialFormatAsync(modelId, Core800s);
        await pool.ReleaseAsync(Who, List("PO", "VPC8C05s-00001"));
        var draft = (await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC8C05s-00001"))).Pack!;
        return (rig, pool, draft, (await rig.Store.GetAsync<BatteryModel>(modelId))!);
    }

    [Theory]
    [InlineData("VPC8C05s00001")] // the dash taken out: the format has it
    [InlineData("VPC8C05s 00001")]
    [InlineData("VPC8C05s_00001")]
    [InlineData("VPC8C05s--00001")]
    [InlineData("VPC8C05s-00001 ")]
    public async Task ADraftsSerialNumber_ChangedOnlyInItsSeparators_ShouldBeChecked_AndRefused(string typed)
    {
        var (rig, _, draft, model) = await DraftAsync(mongo);
        using var __ = rig;

        var result = await SetSerialAsync(rig, draft.Id, typed);

        var key = Assert.Single(result.Keys);
        Assert.Equal(KeyOutcome.Rejected, key.Outcome);
        Assert.True(key.Message == IdentifierForms.SerialRule || key.Message == SerialFormats.Misfit(typed, model.Name, Core800s), key.Message);
        Assert.Equal("VPC8C05s-00001", Text((await rig.Store.GetAsync<BatteryPack>(draft.Id))!, "batterySerialNumber"));
    }

    [Theory]
    [InlineData("VPC8C05ſ-00001")] // ſ upper-cases to S
    [InlineData("VPC8C05s-0000ı")]
    public async Task LettersThatUpperCaseIntoAToZ_ShouldBeRefused_WhereverASerialNumberIsTyped(string typed)
    {
        var (rig, pool, draft, _) = await DraftAsync(mongo);
        using var __ = rig;

        Assert.False(IdentifierForms.IsSerial(typed));
        Assert.Equal(string.Empty, IdentifierForms.Clean(typed)); // no key: never the same as anything
        Assert.False(SerialFormats.Fits(Core800s, typed));
        Assert.False(IdentifierForms.SameSerial(typed, "VPC8C05s-00001"));
        Assert.Equal(IdentifierForms.SerialRule, Assert.Single((await pool.ReleaseAsync(Who, List("PO", typed))).Problems).Reason);
        Assert.Equal(IdentifierForms.SerialRule, Assert.Single((await SetSerialAsync(rig, draft.Id, typed)).Keys).Message);
        Assert.Null((await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(draft, typed))).Pack);
        Assert.Empty(await rig.Store.FindPacksBySerialNumberAsync(typed));
        Assert.False(await rig.Store.SerialNumberTakenAsync(typed)); // no key, and never folded into s or i by the database
        Assert.Null(await rig.Store.FindPackByUniqueProductIdentifierAsync($"{Base}/25S/UN778474481-{typed}"));
    }

    [Fact]
    public async Task AnUnchangedSerialNumber_ShouldNeverBeChecked_SoABatteryWithAnOldSerialStillSaves()
    {
        var (rig, _, draft, _) = await DraftAsync(mongo);
        using var __ = rig;
        // A battery from before 6d-5 (no dash) and before the rule (a space): the API sends #11 again with another value.
        await rig.Database.GetCollection<BsonDocument>(DppStore.Packs).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", draft.Id),
            Builders<BsonDocument>.Update.Set("Values.batterySerialNumber.Value", "VPC 12221"));

        var result = await rig.Services.GetRequiredService<SavePipeline>().SaveAsync(
            ChangeSender.Team(Who),
            new ChangeSet(TargetKind.Pack, draft.Id, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "VPC 12221", ["thirdPartyServiceProvider"] = "Scania AB" }, "api"));

        Assert.DoesNotContain(result.Keys, key => key.Outcome == KeyOutcome.Rejected);
        Assert.Equal("Scania AB", Text((await rig.Store.GetAsync<BatteryPack>(draft.Id))!, "thirdPartyServiceProvider"));
        Assert.Equal(IdentifierForms.SerialRule, Assert.Single((await SetSerialAsync(rig, draft.Id, "VPC 12222")).Keys).Message); // a change is checked
    }
}
