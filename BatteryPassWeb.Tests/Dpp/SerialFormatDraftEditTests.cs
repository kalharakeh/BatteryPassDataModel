using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decisions 80 and 81 on the third way to a battery's serial number: #11 corrected on a draft's page or over the API.
/// It is held to the model's serial number format and to the one rule for serial numbers, as creating a battery and
/// swapping its ID are, so a Core700 serial can't be typed onto a Core800 battery either. A serial number that doesn't
/// change is never checked, so a battery from before a format was set can still be saved.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SerialFormatDraftEditTests(MongoFixture mongo)
{
    private const string Core800 = "VPC8C05-#####";

    private static Task<SaveResult> SetSerialAsync(DemoRig rig, string packId, string serial, string source = "form") =>
        rig.Services.GetRequiredService<SavePipeline>().SaveAsync(
            ChangeSender.Team(Who),
            new ChangeSet(TargetKind.Pack, packId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = serial }, source));

    private static async Task<(DemoRig Rig, BatteryIdPool Pool, BatteryPack Like, BatteryModel Model)> StartAsync(MongoFixture mongo)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        var modelId = (await rig.Store.GetAsync<ProductionBatch>(c7.BatchId))!.ModelId;
        Assert.True(await rig.Store.SetModelSerialFormatAsync(modelId, Core800));
        return (rig, pool, c7, (await rig.Store.GetAsync<BatteryModel>(modelId))!);
    }

    private static async Task<string?> SerialOf(DemoRig rig, string packId) => Text((await rig.Store.GetAsync<BatteryPack>(packId))!, "batterySerialNumber");

    [Theory]
    [InlineData("VPC7C05-00001", "form")] // Core700's, typed on the page
    [InlineData("VPC7C05-00001", "api")] // or sent over the API
    [InlineData("VPC8C05s-00001", "form")] // Core800s's: one more letter
    [InlineData("VPC8C0500002", "form")] // without the dash the format has
    public async Task ADraftsSerialNumber_ShouldNotBeChangedToOneThatDoesNotFitItsModelsFormat(string typed, string source)
    {
        var (rig, pool, like, model) = await StartAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO Core800", "VPC8C05-00001"));
        var draft = (await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(like, "VPC8C05-00001"))).Pack!;

        var result = await SetSerialAsync(rig, draft.Id, typed, source);

        var key = Assert.Single(result.Keys);
        Assert.Equal((KeyOutcome.Rejected, SerialFormats.Misfit(typed, model.Name, Core800)), (key.Outcome, key.Message));
        Assert.False(result.Saved);
        Assert.Equal("VPC8C05-00001", await SerialOf(rig, draft.Id));
    }

    [Fact]
    public async Task ADraftsSerialNumber_ShouldNotBeChangedToAnotherBatterys_InAnyLetters()
    {
        var (rig, pool, like, _) = await StartAsync(mongo);
        using var __ = rig;
        await rig.Store.SetModelSerialFormatAsync((await rig.Store.GetAsync<ProductionBatch>(like.BatchId))!.ModelId, "VPC8C05s-#####");
        await pool.ReleaseAsync(Who, List("PO Core800s", "VPC8C05s-00001", "VPC8C05s-00002"));
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        var one = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(like, "VPC8C05s-00001"))).Pack!;
        var other = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(like, "VPC8C05s-00002"))).Pack!;

        foreach (var typed in new[] { "VPC8C05s-00002", "VPC8C05S-00002", "vpc8c05s-00002" }) // the other battery's, however it is typed
        {
            var result = await SetSerialAsync(rig, one.Id, typed);
            Assert.Equal((KeyOutcome.Rejected, SavePipeline.SerialNumberTaken), (Assert.Single(result.Keys).Outcome, result.Keys[0].Message));
        }

        Assert.Equal(("VPC8C05s-00001", "VPC8C05s-00002"), (await SerialOf(rig, one.Id), await SerialOf(rig, other.Id)));
    }

    [Fact]
    public async Task ASerialNumberThatDoesNotChange_ShouldNeverBeCheckedAgainstAFormatSetLater()
    {
        var (rig, pool, like, model) = await StartAsync(mongo);
        using var __ = rig;
        await rig.Store.SetModelSerialFormatAsync(model.Id, null);
        await pool.ReleaseAsync(Who, List("PO old", "OLD-0001"));
        var old = (await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(like, "OLD-0001"))).Pack!;
        await rig.Store.SetModelSerialFormatAsync(model.Id, Core800); // a format the battery doesn't fit, set afterwards

        // Its own serial number sent again, the very same text, is no change (S1 round two: other letters are a change).
        foreach (var same in new[] { "OLD-0001" })
        {
            var again = await rig.Services.GetRequiredService<SavePipeline>().SaveAsync(
                ChangeSender.Team(Who),
                new ChangeSet(TargetKind.Pack, old.Id, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = same }, "form"));
            Assert.DoesNotContain(again.Keys, key => key.Outcome == KeyOutcome.Rejected);
        }

        Assert.Equal(SerialFormats.Misfit("old-0001", model.Name, Core800), Assert.Single((await SetSerialAsync(rig, old.Id, "old-0001")).Keys).Message);

        // A change to another serial number is held to the format from now on.
        var changed = await SetSerialAsync(rig, old.Id, "OLD-0002");
        Assert.Equal(SerialFormats.Misfit("OLD-0002", model.Name, Core800), Assert.Single(changed.Keys).Message);
        Assert.Equal(KeyOutcome.Saved, Assert.Single((await SetSerialAsync(rig, old.Id, "VPC8C05-00009")).Keys).Outcome); // one that fits
    }

    [Fact]
    public async Task ATestBatterysSerialNumber_AndAModelWithoutAFormat_ShouldStayFree()
    {
        var (rig, pool, like, model) = await StartAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, new ReleaseRequest("Trial", null, [], "TRY-0001", IsDemo: true));
        await pool.ReleaseAsync(Who, List("PO Core800", "VPC8C05-00001"));
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        var trial = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(like, "TRY-0001"))).Pack!;
        var real = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(like, "VPC8C05-00001"))).Pack!;

        Assert.Equal(KeyOutcome.Saved, Assert.Single((await SetSerialAsync(rig, trial.Id, "TRY-0002")).Keys).Outcome); // test IDs are free of the format

        await rig.Store.SetModelSerialFormatAsync(model.Id, null);
        Assert.Equal(KeyOutcome.Saved, Assert.Single((await SetSerialAsync(rig, real.Id, "ANYTHING-7")).Keys).Outcome); // no format: anything, as before
    }
}
