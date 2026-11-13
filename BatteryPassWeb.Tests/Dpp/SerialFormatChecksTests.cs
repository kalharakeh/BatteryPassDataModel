using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 81 where it bites: releasing IDs for a model, creating a battery and swapping a draft's ID refuse a serial
/// number that doesn't fit the model's format, naming the model and the format. Core800 (<c>VPC8C05-#####</c>) and
/// Core800s (<c>VPC8C05s-#####</c>) can't be mixed up, either way; a Core700 serial can't be put on a Core800. A model
/// without a format accepts anything; test IDs and a battery's new passport are free of the format.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SerialFormatChecksTests(MongoFixture mongo)
{
    private const string Core800 = "VPC8C05-#####";
    private const string Core800s = "VPC8C05s-#####";

    /// <summary>Two real models with the owner's formats (the demo's Compact 7M as Core800s, its Compact 13M as Core800) and one without.</summary>
    private sealed record Models(DemoRig Rig, BatteryIdPool Pool, BatteryPack LikeS, BatteryModel S, BatteryPack Like800, BatteryModel M800, BatteryPack LikeFree, BatteryModel Free)
    {
        public PackCreationService Creation => Rig.Services.GetRequiredService<PackCreationService>();
    }

    private static async Task<Models> StartAsync(MongoFixture mongo)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        var c13 = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C13M-0001")).First(pack => pack.SuccessorOf is null); // it has a later passport too
        var core = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-CORE-0001")).First(pack => pack.SuccessorOf is null);
        var s = (await rig.Store.GetAsync<ProductionBatch>(c7.BatchId))!.ModelId;
        var m800 = await MakeRealAsync(rig, c13);
        var free = await MakeRealAsync(rig, core);
        Assert.True(await rig.Store.SetModelSerialFormatAsync(s, Core800s));
        Assert.True(await rig.Store.SetModelSerialFormatAsync(m800, Core800));
        return new Models(rig, pool, c7, (await rig.Store.GetAsync<BatteryModel>(s))!, c13, (await rig.Store.GetAsync<BatteryModel>(m800))!, core, (await rig.Store.GetAsync<BatteryModel>(free))!);
    }

    private static ReleaseRequest For(BatteryModel? model, string reference, params string[] serials) => new(reference, model?.Id, [], string.Join('\n', serials));

    private static async Task<string?> SerialOf(DemoRig rig, string packId) => Text((await rig.Store.GetAsync<BatteryPack>(packId))!, "batterySerialNumber");

    [Fact]
    public async Task ReleasingForAModel_ShouldAcceptSerialsThatFitItsFormat_InAnyLetters()
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;

        var range = await models.Pool.ReleaseAsync(Who, new ReleaseRequest("PO Core800s", models.S.Id, [new IdRange("VPC8C05s-00001", "VPC8C05s-00003", "3")], null));
        var capital = await models.Pool.ReleaseAsync(Who, For(models.S, "PO capital", "VPC8C05S-00004")); // the same format, in capitals
        var plain = await models.Pool.ReleaseAsync(Who, For(models.M800, "PO Core800", "VPC8C05-00001"));

        Assert.Equal((3, 1, 1), (range.Released, capital.Released, plain.Released));
        Assert.Equal(["VPC8C05s-00001", "VPC8C05s-00002", "VPC8C05s-00003"], (await models.Pool.ListAsync(new ReleasedIdFilter(Reference: range.ReleaseId))).Select(item => item.SerialNumber));
    }

    [Theory]
    [InlineData("VPC7C05-00001")] // Core700's
    [InlineData("VPC8C05-00001")] // Core800's: one letter short
    [InlineData("VPC8C05s-0001")]
    [InlineData("VPC8C05s00001")]
    public async Task ReleasingForAModel_ShouldRefuseASerialThatDoesNotFit_NamingTheFormat_AndReleaseNothing(string serial)
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;

        var result = await models.Pool.ReleaseAsync(Who, For(models.S, "PO mixed", "VPC8C05s-00001", serial, "VPC8C05s-00002"));

        Assert.Equal(0, result.Released); // all or nothing
        var problem = Assert.Single(result.Problems);
        Assert.Equal((serial, $"it doesn't fit the serial number format of model {models.S.Name}, {Core800s}"), (problem.What, problem.Reason));
        Assert.Equal("1 serial number or range can't be released", result.Problem);
        Assert.Null(await models.Pool.GetAsync("VPC8C05s-00001"));
    }

    [Fact]
    public async Task TheMixUpOfCore800AndCore800s_ShouldBeRefusedAtRelease_BothWays()
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;

        var onPlain = await models.Pool.ReleaseAsync(Who, For(models.M800, "PO Core800", "VPC8C05s-00004", "VPC8C05S-00005"));
        var onS = await models.Pool.ReleaseAsync(Who, For(models.S, "PO Core800s", "VPC8C05-00004"));

        Assert.False(onPlain.Done);
        Assert.Equal(["VPC8C05s-00004", "VPC8C05S-00005"], onPlain.Problems.Select(problem => problem.What));
        Assert.All(onPlain.Problems, problem => Assert.Equal($"it doesn't fit the serial number format of model {models.M800.Name}, {Core800}", problem.Reason));
        Assert.False(onS.Done);
        Assert.Equal($"it doesn't fit the serial number format of model {models.S.Name}, {Core800s}", Assert.Single(onS.Problems).Reason);
    }

    [Fact]
    public async Task ManySerialsThatDoNotFit_ShouldNameTheFirstOnes_AndCountTheRest()
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;

        var result = await models.Pool.ReleaseAsync(Who, new ReleaseRequest("PO Core700", models.S.Id, [new IdRange("VPC7C05-00001", "VPC7C05-00020", "20")], "VPC8C05s-00001"));

        Assert.False(result.Done);
        Assert.Equal(["VPC7C05-00001", "VPC7C05-00002", "VPC7C05-00003", "VPC7C05-00004", "VPC7C05-00005", "15 more serial numbers"], result.Problems.Select(problem => problem.What));
        Assert.Equal($"they don't fit the format {Core800s} either", result.Problems[^1].Reason);
        Assert.Equal("20 serial numbers or ranges can't be released", result.Problem);
    }

    [Fact]
    public async Task ReleasedForAnyModel_AndTestIds_ShouldNotBeCheckedAtRelease_NorAModelWithoutAFormat()
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;

        var any = await models.Pool.ReleaseAsync(Who, For(null, "PO any", "VPC7C05-00001", "WHATEVER-1"));
        var free = await models.Pool.ReleaseAsync(Who, For(models.Free, "PO free", "ANY-0001"));
        var test = await models.Pool.ReleaseAsync(Who, For(null, "Trial", "TRY-0001") with { IsDemo = true });

        Assert.Equal((2, 1, 1), (any.Released, free.Released, test.Released));
        Assert.Null(models.Free.SerialFormat); // as every model before decision 81
    }

    [Fact]
    public async Task ACore700Serial_ShouldNotBePutOnACore800Battery_AtCreation()
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;
        await models.Pool.ReleaseAsync(Who, For(null, "PO any", "VPC7C05-00001", "VPC8C05-00001", "VPC8C05s-00001", "VPC8C05S-00002"));

        var core700 = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.Like800, "VPC7C05-00001"));
        var sOn800 = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.Like800, "VPC8C05s-00001"));
        var capitalSOn800 = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.Like800, "vpc8c05s-00002")); // typed in small letters
        var plainOnS = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.LikeS, "VPC8C05-00001"));

        Assert.Equal((null, SerialFormats.Misfit("VPC7C05-00001", models.M800.Name, Core800)), (core700.Pack, core700.Problem));
        Assert.Equal((null, SerialFormats.Misfit("VPC8C05s-00001", models.M800.Name, Core800)), (sOn800.Pack, sOn800.Problem));
        Assert.Equal((null, SerialFormats.Misfit("VPC8C05S-00002", models.M800.Name, Core800)), (capitalSOn800.Pack, capitalSOn800.Problem)); // named as released
        Assert.Equal((null, SerialFormats.Misfit("VPC8C05-00001", models.S.Name, Core800s)), (plainOnS.Pack, plainOnS.Problem));
        foreach (var serial in new[] { "VPC7C05-00001", "VPC8C05-00001", "VPC8C05s-00001", "VPC8C05S-00002" })
        {
            Assert.Equal((ReleasedIdStatuses.Available, (string?)null), ((await models.Pool.GetAsync(serial))!.Status, (await models.Pool.GetAsync(serial))!.PackId)); // nothing was taken
        }

        // Each on its own model is fine, and so is anything on the model without a format.
        var plain = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.Like800, "VPC8C05-00001"));
        var s = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.LikeS, "vpc8c05s-00002"));
        var free = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.LikeFree, "VPC7C05-00001"));
        Assert.Equal(("VPC8C05-00001", "VPC8C05S-00002", "VPC7C05-00001"), (Text(plain.Pack!, "batterySerialNumber"), Text(s.Pack!, "batterySerialNumber"), Text(free.Pack!, "batterySerialNumber")));
    }

    [Fact]
    public async Task ABatteryMadeWithoutThePool_ShouldBeHeldToItsModelsFormatToo()
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;

        var wrong = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.Like800, "VPC7C05-00009") with { FromPool = false });
        var right = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.Like800, "VPC8C05-00009") with { FromPool = false });

        Assert.Equal((null, SerialFormats.Misfit("VPC7C05-00009", models.M800.Name, Core800)), (wrong.Pack, wrong.Problem));
        Assert.NotNull(right.Pack);
    }

    [Fact]
    public async Task ADraft_ShouldNotSwapToASerialThatDoesNotFitItsModel()
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;
        await models.Pool.ReleaseAsync(Who, For(null, "PO any", "VPC8C05-00001", "VPC8C05-00002", "VPC7C05-00001", "VPC8C05s-00001", "VPC8C05S-00002"));
        var draft = (await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.Like800, "VPC8C05-00001"))).Pack!;

        Assert.Equal(SerialFormats.Misfit("VPC7C05-00001", models.M800.Name, Core800), await models.Pool.SwapAsync(Who, draft.Id, "VPC7C05-00001"));
        Assert.Equal(SerialFormats.Misfit("VPC8C05s-00001", models.M800.Name, Core800), await models.Pool.SwapAsync(Who, draft.Id, "VPC8C05s-00001"));
        Assert.Equal(SerialFormats.Misfit("VPC8C05S-00002", models.M800.Name, Core800), await models.Pool.SwapAsync(Who, draft.Id, "vpc8c05s-00002"));

        // Nothing moved: the draft keeps its ID, and the refused ones stay available.
        Assert.Equal("VPC8C05-00001", await SerialOf(models.Rig, draft.Id));
        Assert.Equal((ReleasedIdStatuses.Used, draft.Id), ((await models.Pool.GetAsync("VPC8C05-00001"))!.Status, (await models.Pool.GetAsync("VPC8C05-00001"))!.PackId));
        Assert.All(new[] { "VPC7C05-00001", "VPC8C05s-00001", "VPC8C05S-00002" }, serial => Assert.Equal(ReleasedIdStatuses.Available, models.Pool.GetAsync(serial).GetAwaiter().GetResult()!.Status));

        Assert.Null(await models.Pool.SwapAsync(Who, draft.Id, "VPC8C05-00002")); // one that fits
        Assert.Equal("VPC8C05-00002", await SerialOf(models.Rig, draft.Id));

        // The other way round: a Core800s draft doesn't take a Core800 serial.
        var s = (await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.LikeS, "VPC8C05s-00001"))).Pack!;
        Assert.Equal(SerialFormats.Misfit("VPC8C05-00001", models.S.Name, Core800s), await models.Pool.SwapAsync(Who, s.Id, "VPC8C05-00001"));
    }

    [Fact]
    public async Task TestIds_ShouldBeFreeOfTheFormat_AtCreationAndAtASwap()
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;
        await models.Pool.ReleaseAsync(Who, For(null, "Trial", "TRY-0001", "ANYTHING-2") with { IsDemo = true });

        var created = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.Like800, "TRY-0001"));

        Assert.True(created.Pack is not null, created.Problem);
        Assert.True(created.Pack!.IsDemo);
        Assert.Null(await models.Pool.SwapAsync(Who, created.Pack.Id, "ANYTHING-2"));
        Assert.Equal("ANYTHING-2", await SerialOf(models.Rig, created.Pack.Id));
    }

    [Fact]
    public async Task AFormatSetLater_ShouldNotTouchABatteryThatExists_NorItsNewPassport()
    {
        var models = await StartAsync(mongo);
        using var __ = models.Rig;
        await models.Pool.ReleaseAsync(Who, For(null, "PO any", "OLD-0001"));
        var old = (await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.LikeFree, "OLD-0001"))).Pack!;

        Assert.True(await models.Rig.Store.SetModelSerialFormatAsync(models.Free.Id, "VPC7C05-#####"));

        // The battery is as it was, and its new linked passport keeps its serial number (design 7.5): never checked.
        var same = (await models.Rig.Store.GetAsync<BatteryPack>(old.Id))!;
        Assert.Equal(old.Values["uniqueProductIdentifier"].Value!.GetValue<string>(), same.Values["uniqueProductIdentifier"].Value!.GetValue<string>());
        var next = await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.LikeFree, "OLD-0001") with { PredecessorPackId = old.Id });
        Assert.True(next.Pack is not null, next.Problem);
        Assert.EndsWith("/25S/UN778474481-OLD-0001-R1", Text(next.Pack!, "uniqueProductIdentifier"), StringComparison.Ordinal);

        // A new battery of the model is held to the format from now on.
        await models.Pool.ReleaseAsync(Who, For(null, "PO any 2", "OLD-0002"));
        Assert.Equal(SerialFormats.Misfit("OLD-0002", models.Free.Name, "VPC7C05-#####"), (await models.Creation.CreateAsync(ChangeSender.Team(Who), NewBattery(models.LikeFree, "OLD-0002"))).Problem);
    }
}
