using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The owner's real serial numbers (13 Nov 2026) with made-up numbers, for the tests of decisions 80 to 82: Core800
/// <c>VPC8C05-XXXXX</c>, Core800s <c>VPC8C05s-XXXXX</c> (a small s) and Core700 <c>VPC7C05-XXXXX</c>.
/// </summary>
internal static class SmallLetterSerialTesting
{
    public const string Who = DemoRig.Who;
    public const string Base = "https://www.sib-scania-eubp.eu";

    /// <summary>A Core800s serial number as it is released and shown.</summary>
    public const string Serial = "VPC8C05s-00001";

    /// <summary>Its battery ID's item: the serial in capitals.</summary>
    public const string Item = "UN778474481-VPC8C05S-00001";

    /// <summary>The same item typed in capitals, in small letters and mixed.</summary>
    public static readonly string[] ItemSpellings = [Item, "un778474481-vpc8c05s-00001", "Un778474481-vPc8C05s-00001"];

    /// <summary>The same serial number typed in capitals, in small letters, mixed and as released.</summary>
    public static readonly string[] SerialSpellings = ["VPC8C05S-00001", "vpc8c05s-00001", "vPc8C05s-00001", Serial];

    public static TheoryData<string> Items() => new(ItemSpellings);

    public static TheoryData<string> Serials() => new(SerialSpellings);

    /// <summary>The demo data on the owner's ID settings, with the Compact 7M's batch, model and operator made real product data.</summary>
    public static async Task<(DemoRig Rig, BatteryIdPool Pool, BatteryPack C7)> SeededAsync(MongoFixture mongo)
    {
        var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(Who);
        var c7 = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single();
        await MakeRealAsync(rig, c7);
        return (rig, rig.Services.GetRequiredService<BatteryIdPool>(), c7);
    }

    /// <summary>Makes a demo battery's batch, model and operator real product data, so that real IDs may be used on them; the model's ID.</summary>
    public static async Task<string> MakeRealAsync(DemoRig rig, BatteryPack pack)
    {
        var batch = (await rig.Store.GetAsync<ProductionBatch>(pack.BatchId))!;
        foreach (var (collection, id) in new[] { (DppStore.Batches, batch.Id), (DppStore.Models, batch.ModelId), (DppStore.Operators, pack.OperatorId) })
        {
            await rig.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Set("IsDemo", false));
        }

        return batch.ModelId;
    }

    public static ReleaseRequest List(string reference, params string[] serials) => new(reference, null, [], string.Join('\n', serials));

    /// <summary>A new battery with every value a passport needs, taking a released serial number; no customer.</summary>
    public static NewPack NewBattery(BatteryPack like, string serial)
    {
        var values = DemoCatalogue.Load().Packs.First(pack => pack.Publish).Values.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone(), StringComparer.Ordinal);
        values["batterySerialNumber"] = serial;
        return new NewPack(like.BatchId, like.OperatorId, null, values, FromPool: true);
    }

    /// <summary>Releases <paramref name="serial"/>, makes its battery and publishes it.</summary>
    public static async Task<BatteryPack> PublishedAsync(DemoRig rig, BatteryPack like, string serial = Serial)
    {
        var pool = rig.Services.GetRequiredService<BatteryIdPool>();
        var released = await pool.ReleaseAsync(Who, List($"PO {serial}", serial));
        Assert.True(released.Done, released.Problem + string.Join("; ", released.Problems.Select(problem => problem.Reason)));
        var created = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(like, serial));
        Assert.True(created.Pack is not null, created.Problem + string.Join("; ", created.Keys.Select(key => key.Message)));
        await ExampleValuesTests.ReplaceExamplesAsync(rig, created.Pack!.Id);
        var published = await rig.Services.GetRequiredService<PublicationService>().PublishAsync(ChangeSender.Team(Who), created.Pack.Id);
        Assert.True(published.Version is not null, string.Join(" | ", published.Report?.Blocking ?? [published.Problem ?? string.Empty]));
        return (await rig.Store.GetAsync<BatteryPack>(created.Pack.Id))!;
    }

    public static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

/// <summary>A page's messages for the next request, kept nowhere: a test reads them from the controller.</summary>
internal sealed class NoTempDataKept : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>();

    public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values)
    {
    }
}
