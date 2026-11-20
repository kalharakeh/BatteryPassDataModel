using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// FU5: a real serial number whose key (letters and digits only, capitals) starts with TEST makes an ID the test IDs
/// could collide with, so it is refused like "TEST-1" is, at release and at creation. And a sender who may not see a
/// battery is never told its serial number when its ID is taken.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class TestSerialKeyTests(MongoFixture mongo)
{
    private const string RealSerial = "T-EST-DEMO-SPARE-0001";

    private static ChangeSender DemoTeam => ChangeSender.Team("api-token:demo") with { DemoOnly = true };

    [Theory]
    [InlineData("T-EST-1")]
    [InlineData("t-est-1")]
    [InlineData("TE-ST1")]
    [InlineData("TES-T-0001")]
    [InlineData("TEST-1")]
    public async Task ARealSerialWhoseKeyStartsWithTest_ShouldBeRefusedAtRelease(string serial)
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var _ = rig;

        var result = await pool.ReleaseAsync(Who, List("PO test key", serial));

        Assert.False(result.Done);
        Assert.Equal(DunsIdentifierScheme.StartsWithTest, Assert.Single(result.Problems).Reason);
    }

    [Theory]
    [InlineData("T-EST-1")]
    [InlineData("tes-t1")]
    public async Task ARealSerialWhoseKeyStartsWithTest_ShouldBeRefusedAtCreation(string serial)
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var _ = rig;

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, serial) with { FromPool = false });

        Assert.Null(result.Pack);
        Assert.Contains(DunsIdentifierScheme.StartsWithTest, result.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TES-1")]
    [InlineData("VBC-TEST-1")]
    [InlineData("TEAT-1")]
    public async Task ASerialThatOnlyContainsOrAlmostStartsWithTest_ShouldStillBeReleased(string serial)
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var _ = rig;

        Assert.True((await pool.ReleaseAsync(Who, List("PO fine", serial))).Done);
    }

    [Fact]
    public async Task ADemoSenderCreatingFromATakenTestId_ShouldNotBeToldTheOtherBatterysSerialNumber()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var _ = rig;
        await MakeHolderAsync(rig, c7);
        var demoLike = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-CORE-0001")).Single(); // demo product data: a demo sender reaches it

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(DemoTeam, NewBattery(demoLike, DemoDataService.SpareSerials[0]));

        Assert.Null(result.Pack);
        Assert.DoesNotContain(RealSerial, result.Problem, StringComparison.Ordinal);
        Assert.Equal(DunsIdentifierScheme.TakenByAnother("UN778474481-TEST-DEMO-SPARE-0001"), result.Problem);
    }

    [Fact]
    public async Task ADemoSender_ShouldNotBeToldTheSerialNumberOfADemoIdBatteryOnRealProductData()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var _ = rig;
        await MakeHolderAsync(rig, c7, demoMarked: true); // real product data under a demo mark: real to a demo sender
        var demoLike = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-CORE-0001")).Single();

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(DemoTeam, NewBattery(demoLike, DemoDataService.SpareSerials[0]));

        Assert.DoesNotContain(RealSerial, result.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTeam_ShouldKeepTodaysSentenceWithTheOtherBatterysSerialNumber()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var _ = rig;
        await MakeHolderAsync(rig, c7);

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, DemoDataService.SpareSerials[0]));

        Assert.Null(result.Pack);
        Assert.Contains(RealSerial, result.Problem, StringComparison.Ordinal);
    }

    private static AttributeValue Same(AttributeValue old, string text) =>
        new() { Value = JsonValue.Create(text), GeneratedAtUtc = old.GeneratedAtUtc, Source = old.Source, Who = old.Who };

    /// <summary>A real battery whose ID has the key of the demo spare's test ID (such a serial can no longer be released; one from before can exist).</summary>
    private static async Task MakeHolderAsync(DemoRig rig, BatteryPack like, bool demoMarked = false, string? clusterId = null)
    {
        var pool = rig.Services.GetRequiredService<BatteryIdPool>();
        Assert.True((await pool.ReleaseAsync(Who, List("PO real", "FU5-REAL-1"))).Done);
        var created = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(like, "FU5-REAL-1"));
        Assert.True(created.Pack is not null, created.Problem);
        var real = (await rig.Store.GetAsync<BatteryPack>(created.Pack!.Id))!;
        real.Values["batterySerialNumber"] = Same(real.Values["batterySerialNumber"], RealSerial);
        real.Values["uniqueProductIdentifier"] = Same(real.Values["uniqueProductIdentifier"], $"{Base}/25S/UN778474481-{RealSerial}");
        real.ItemKey = IdentifierForms.ItemKey($"UN778474481-{RealSerial}");
        real.IsDemo = demoMarked;
        real.ClusterId = clusterId;
        await rig.Database.GetCollection<BatteryPack>(DppStore.Packs).ReplaceOneAsync(pack => pack.Id == real.Id, real);
    }

    [Fact]
    public async Task AReleasedRealIdWhoseKeyStartsWithTest_WrittenStraightIntoThePool_ShouldBeRefusedAtCreationAndNeverPicked()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var _ = rig;
        await rig.Store.ReleasedIds.InsertOneAsync(new ReleasedBatteryId
        {
            Id = IdentifierForms.Clean("T-EST-1"),
            SerialNumber = "T-EST-1",
            Identifier = $"{Base}/25S/UN778474481-T-EST-1",
            ReleaseId = "release-before-fu5",
            Reference = "PO before FU5",
            Status = ReleasedIdStatuses.Available,
            ReleasedBy = Who,
            ReleasedAtUtc = DateTime.UtcNow,
        });

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "T-EST-1"));

        Assert.Null(result.Pack);
        Assert.Contains(DunsIdentifierScheme.StartsWithTest, result.Problem, StringComparison.Ordinal);
        Assert.Null(await pool.PickAsync("T-EST-1", "pack-x", null)); // nor by a swap, which picks the same way
        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("T-EST-1"))!.Status);
    }

    [Fact]
    public async Task AClusterAdmin_ShouldNeverGetFarEnoughToLearnASerialNumber_OnlyTheTeamCreatesBatteries()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var _ = rig;
        await MakeHolderAsync(rig, c7, clusterId: "cluster-a");

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.ClusterAdmin("b@example.test", "cluster-b"), NewBattery(c7, DemoDataService.SpareSerials[0]) with { ClusterId = "cluster-b" });

        Assert.Null(result.Pack);
        Assert.Equal("only the team creates packs", result.Problem);
    }

    [Fact]
    public async Task ADemoSender_ShouldBeToldTheSerialNumberOfADemoBatteryInADemoCluster()
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        using var _ = rig;
        await rig.Demo.SeedAsync(Who);
        var holder = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single(); // in a demo cluster, on demo product data
        holder.Values["batterySerialNumber"] = Same(holder.Values["batterySerialNumber"], "DEMO-HOLDER-1");
        holder.Values["uniqueProductIdentifier"] = Same(holder.Values["uniqueProductIdentifier"], $"{Base}/25S/UN778474481-TEST-DEMO-SPARE-0001");
        holder.ItemKey = IdentifierForms.ItemKey("UN778474481-TEST-DEMO-SPARE-0001");
        await rig.Database.GetCollection<BatteryPack>(DppStore.Packs).ReplaceOneAsync(pack => pack.Id == holder.Id, holder);
        var like = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-CORE-0001")).Single();

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(DemoTeam, NewBattery(like, DemoDataService.SpareSerials[0]));

        Assert.Null(result.Pack);
        Assert.Contains("DEMO-HOLDER-1", result.Problem, StringComparison.Ordinal);
    }
}
