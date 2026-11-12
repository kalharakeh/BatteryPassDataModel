using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round two, F: the correction that adds a missing warranty writes only while the battery still has none, so two
/// corrections at the same moment can't both add one. The check and the write are one database step.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class WarrantyAddedOnceTests(MongoFixture mongo)
{
    private const string Warranty = "warrantyPeriod";

    [Fact]
    public async Task TheWrite_ShouldOnlyHappenWhileTheBatteryHasNoWarranty()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), [Warranty]);

        var first = await rig.Store.SetValuesIfMissingAsync<BatteryPack>(rig.PackId, Value("P8Y"), Warranty);
        var second = await rig.Store.SetValuesIfMissingAsync<BatteryPack>(rig.PackId, Value("P2Y"), Warranty);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal("P8Y", (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values[Warranty].Value!.GetValue<string>());
    }

    private static Dictionary<string, AttributeValue> Value(string period) => new(StringComparer.Ordinal)
    {
        [Warranty] = new() { Value = JsonValue.Create(period), GeneratedAtUtc = DateTime.UtcNow, Source = "form" },
    };
}
