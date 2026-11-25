using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>Package R1, round two (review L3): the workbook sets #42's names apart with ";" only; a comma stays inside a name.</summary>
[Collection(MongoCollection.Name)]
public sealed class CommaInWorkbookNamesTests(MongoFixture mongo)
{
    [Fact]
    public async Task AWorkbookCellWithACommaInAName_ShouldGiveTwoNames()
    {
        var rig = await EmptyAsync(mongo);

        var loaded = await rig.LoadAsync(Core().Set(ModelSheet, "model|criticalRawMaterials", "Core800", "Rare earth elements (Nd, Dy); Lithium"));

        Assert.True(loaded.Loaded, loaded.Message);
        var model = (await rig.Store.ListAsync<BatteryModel>()).Single(item => item.Name == "Core800");
        var stored = model.Values[AttributeCatalog.CriticalRawMaterials].Value;
        Assert.True(JsonNode.DeepEquals(new JsonArray(EuCodeLists.RawMaterialBase + "lithium", "Rare earth elements (Nd, Dy)"), stored), stored.ToJsonString());
    }
}
