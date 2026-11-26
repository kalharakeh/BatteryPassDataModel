using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6 (found building the owner's item 12): a battery placed on the market a second time couldn't get its third
/// passport ("another battery already has this serial number"), because only a passport's direct neighbours could share
/// its serial number. Every passport of the chain may share it; an unrelated battery still may not.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ThirdPassportTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    [Fact]
    public async Task ABatteryPlacedOnTheMarketTwice_ShouldGetItsThirdPassport()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var second = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;
        Assert.Null((await StatusChangeTests.Service(rig).ChangeAsync(Team, second, new StatusChange(StatusEvent.Remanufactured, await StatusChangeTests.CurrentAsync(rig)))).Problem);

        var (third, problem) = await SuccessionTests.Service(rig).CreateSuccessorAsync(Team, second, (await rig.Store.GetAsync<BatteryPack>(second))!.OperatorId);

        Assert.Null(problem);
        Assert.NotNull(third);
        Assert.Equal([third!, second, rig.PackId], (await rig.Store.ChainPackIdsAsync(third)).ToArray());
        Assert.Equal([rig.PackId, second, third], (await rig.Store.ChainPackIdsAsync(rig.PackId)).ToArray());
    }

    [Fact]
    public async Task AnUnrelatedBattery_ShouldStillNotTakeTheSerialNumber()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var serial = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["batterySerialNumber"].Value!.GetValue<string>();
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var other = await BatteryTesting.Creation(rig).CreateAsync(Team, new NewPack(pack.BatchId, pack.OperatorId, null, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }));

        Assert.Null(other.Pack);
        Assert.Contains(other.Keys, key => key.Message == SavePipeline.SerialNumberTaken);
    }
}
