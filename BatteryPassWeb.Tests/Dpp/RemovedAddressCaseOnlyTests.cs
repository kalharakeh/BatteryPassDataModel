using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using Microsoft.Extensions.DependencyInjection;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S1 round three, point 1: two questions, two matches. "Was the passport at this address removed?" (every serving
/// route, uploads, the API's write target) matches a removal marker's IDs ignoring capitals only, so a live battery whose
/// ID differs from a removed one's in its dashes is served. "Is this ID taken?" (release, creation, swap, a new passport's
/// R-number) keeps the key: capitals and dashes ignored.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RemovedAddressCaseOnlyTests(MongoFixture mongo)
{
    private static RemovedPassport Marker(string item) => new()
    {
        Id = $"pack-gone-{Guid.NewGuid():N}",
        BatteryId = $"{Base}/25S/{item}",
        Ids = [$"pack-gone", $"{Base}/25S/{item}", item, $"{Base}/dpp/{item}/v1", $"{item}/v1"],
        RemovedAtUtc = DateTime.UtcNow,
        RemovedBy = Who,
    };

    [Fact]
    public async Task ALiveBattery_WhoseIdDiffersFromARemovedOnesInItsDashes_ShouldBeServed_NotGone()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        // Removed before round two: …-VBC-1R1. Live (round one let dash variants through): …-VBC-1-R1.
        await rig.Store.AddRemovedAsync(Marker("UN778474481-VBC-1R1"));
        var live = new BatteryPack { Id = "pack-live", BatchId = c7.BatchId, OperatorId = c7.OperatorId };
        live.Values["uniqueProductIdentifier"] = new AttributeValue { Value = JsonValue.Create($"{Base}/25S/UN778474481-VBC-1-R1"), GeneratedAtUtc = DateTime.UtcNow, Source = "system" };
        await rig.Store.InsertAsync(live);
        var removal = rig.Services.GetRequiredService<PassportRemoval>();

        foreach (var address in new[] { "UN778474481-VBC-1-R1", "un778474481-vbc-1-r1", $"{Base}/25S/UN778474481-VBC-1-R1", $"{Base}/dpp/UN778474481-VBC-1-R1/v1", "UN778474481-VBC-1-R1/v1" })
        {
            Assert.Null(await removal.FindAsync(address)); // served
        }

        foreach (var address in new[] { "UN778474481-VBC-1R1", "un778474481-vbc-1r1", "Un778474481-vBc-1r1", $"{Base}/25S/UN778474481-VBC-1R1", $"{Base}/25s/un778474481-vbc-1r1", $"{Base}/dpp/UN778474481-VBC-1R1/v1", "un778474481-vbc-1r1/V1" })
        {
            Assert.NotNull(await removal.FindAsync(address)); // the removed passport's own ID, in any capitals: 410
        }
    }

    [Fact]
    public async Task TheSurvivorOfAPairTheLogNamed_ShouldStillBeServed_AfterTheOtherIsRemoved()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        // The log named …-VBC-2-R1 and …-VBC2R1 as a pair; the team removed the second.
        await rig.Store.AddRemovedAsync(Marker("UN778474481-VBC2R1"));

        Assert.Null(await rig.Services.GetRequiredService<PassportRemoval>().FindAsync("UN778474481-VBC-2-R1"));
        Assert.NotNull(await rig.Services.GetRequiredService<PassportRemoval>().FindAsync("un778474481-vbc2r1"));
    }

    [Fact]
    public async Task IsThisIdTaken_ShouldStillIgnoreDashesAndCapitals_ForARemovedPassport()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await rig.Store.AddRemovedAsync(Marker("UN778474481-VBC-3R1"));

        foreach (var serial in new[] { "VBC-3R1", "VBC-3-r1", "vbc3r1" })
        {
            Assert.Equal(BatteryIdPool.RemovedId, Assert.Single((await pool.ReleaseAsync(Who, List("PO", serial))).Problems).Reason);
        }

        Assert.True(await rig.Store.IsItemKeyRemovedAsync("UN778474481VBC3R1"));
        Assert.False(await rig.Store.IsItemKeyRemovedAsync("UN778474481VBC3R2"));
    }
}
