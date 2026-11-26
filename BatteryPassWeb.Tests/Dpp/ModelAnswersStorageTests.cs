using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R01: a model's answers are stored on the model. A model saved before the questions existed reads as "not answered
/// yet", and saving its answers keeps everything else it has.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ModelAnswersStorageTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Fact]
    public async Task AModelStoredWithoutAnswers_ShouldReadAsNotAnswered_AndKeepEverythingWhenTheyAreSaved()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetModelModuleDescriptionAsync(ModelId, "Seven modules");
        var models = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Models);
        await models.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", ModelId),
            Builders<BsonDocument>.Update.Unset("HasCommercialWarranty").Unset("MadeForCyclingUse").Unset("CadmiumOrLeadAboveLimit"));
        var before = await models.Find(Builders<BsonDocument>.Filter.Eq("_id", ModelId)).SingleAsync();
        Assert.False(before.Contains("MadeForCyclingUse")); // as an earlier version stored it

        var old = (await rig.Store.GetAsync<BatteryModel>(ModelId))!;
        Assert.Equal(ModelAnswers.NotAnswered, old.Answers());

        Assert.True(await rig.Store.SetModelAnswersAsync(ModelId, true, false, null));

        var after = (await rig.Store.GetAsync<BatteryModel>(ModelId))!;
        Assert.Equal(new ModelAnswers(true, false, null), after.Answers());
        Assert.Equal(old.Name, after.Name);
        Assert.Equal("Seven modules", after.ModuleDescription);
        Assert.Equal(old.Values.Keys.Order(), after.Values.Keys.Order());
        Assert.Equal(old.Values["batteryMass"].Value!.ToJsonString(), after.Values["batteryMass"].Value!.ToJsonString());
        var stored = await models.Find(Builders<BsonDocument>.Filter.Eq("_id", ModelId)).SingleAsync();
        Assert.Equal(before.Names.Order().Concat([]), stored.Names.Except(["HasCommercialWarranty", "MadeForCyclingUse", "CadmiumOrLeadAboveLimit"]).Order());
    }

    [Fact]
    public async Task AnAnswer_ShouldNotBeSavedOnAModelThatDoesntExist()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        Assert.False(await rig.Store.SetModelAnswersAsync("model-missing", true, true, true));
    }
}
