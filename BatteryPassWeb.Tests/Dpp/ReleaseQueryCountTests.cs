using System.Diagnostics;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// S1 round three, point 3: a release checks the keys of all its serial numbers against the removal markers and the
/// batteries in a fixed number of queries, whatever its size; a new passport's R-number reads the taken numbers once.
/// Counted by the server's own operation counters (the tests that use the database run one at a time).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ReleaseQueryCountTests(MongoFixture mongo, ITestOutputHelper output)
{
    /// <summary>The commands the server has run (finds, inserts, updates, …): one insert of 5,000 documents is one command.</summary>
    private static async Task<long> OperationsAsync(BatteryPassWeb.Tests.Demo.DemoRig rig)
    {
        var status = await rig.Database.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("serverStatus", 1));
        return status["metrics"]["commands"].AsBsonDocument.Elements
            .Where(command => command.Value.IsBsonDocument && command.Value.AsBsonDocument.Contains("total"))
            .Sum(command => command.Value["total"].ToInt64());
    }

    [Fact]
    public async Task AReleaseOf1And5000Serials_ShouldMakeTheSameFixedNumberOfQueries()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        for (var n = 0; n < 50; n++)
        {
            await rig.Store.AddRemovedAsync(new RemovedPassport { Id = $"pack-gone-{n}", BatteryId = $"{Base}/25S/UN778474481-GONE-{n}", Ids = [$"pack-gone-{n}", $"{Base}/25S/UN778474481-GONE-{n}", $"UN778474481-GONE-{n}"], RemovedAtUtc = DateTime.UtcNow, RemovedBy = Who });
        }

        var before = await OperationsAsync(rig);
        Assert.True((await pool.ReleaseAsync(Who, new ReleaseRequest("PO one", null, [new IdRange("ONE-00001", "ONE-00001", "1")], null))).Done);
        var one = await OperationsAsync(rig) - before - 1; // minus the counter's own command

        var watch = Stopwatch.StartNew();
        before = await OperationsAsync(rig);
        var big = await pool.ReleaseAsync(Who, new ReleaseRequest("PO many", null, [new IdRange("MANY-00001", "MANY-05000", "5000")], null));
        var many = await OperationsAsync(rig) - before - 1;
        watch.Stop();
        output.WriteLine($"release of 1: {one} operations; release of 5,000: {many} operations, {watch.ElapsedMilliseconds} ms");

        Assert.Equal(5000, big.Released);
        Assert.InRange(many, one, one + 3); // the insert of 5,000 may take a few more batches; never one per serial
        Assert.True(one <= 12, $"{one}");
    }

    [Fact]
    public async Task ANewPassportsRNumber_ShouldReadTheTakenNumbersOnce()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO", "VBC-9", "VBC-9-R2", "VBC-9-R4"));
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        var old = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VBC-9"))).Pack!;
        foreach (var n in new[] { 1, 3, 5, 6 })
        {
            await rig.Store.AddRemovedAsync(new RemovedPassport { Id = $"pack-r{n}", BatteryId = $"{Base}/25S/UN778474481-VBC-9-R{n}", Ids = [$"{Base}/25S/UN778474481-VBC-9-R{n}"], RemovedAtUtc = DateTime.UtcNow, RemovedBy = Who });
        }

        var taken = await rig.Store.TakenItemKeysAsync("UN778474481-VBC-9");
        var before = await OperationsAsync(rig);
        var scheme = rig.Services.GetRequiredService<IIdentifierScheme>();
        var next = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(old, "VBC-9") with { FromPool = false, PredecessorPackId = old.Id });
        var used = await OperationsAsync(rig) - before - 1;
        output.WriteLine($"a new passport after 6 taken numbers: {used} operations");

        Assert.Contains("UN778474481VBC9R1", taken);
        Assert.EndsWith("/25S/UN778474481-VBC-9-R7", Text(next.Pack!, "uniqueProductIdentifier"), StringComparison.Ordinal); // R1–R6 taken: removed, released, removed, released, removed, removed
        Assert.NotNull(scheme);
    }
}
