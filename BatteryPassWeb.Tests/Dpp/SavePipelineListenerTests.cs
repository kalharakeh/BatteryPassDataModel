using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class SavePipelineListenerTests(MongoFixture mongo)
{
    private const string PackId = "pack-VPC8C05-6788";

    [Fact]
    public async Task SavedValues_ShouldReachTheListeners()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var listener = new RecordingListener();

        await SavePipelineTesting.Pipeline(store, context, listener).SaveAsync(
            ChangeSender.Team("team@example.com"),
            Change(("thirdPartyServiceProvider", "\"Heard AB\""), ("ratedCapacity", "200")));

        var call = Assert.Single(listener.Calls);
        Assert.Equal((TargetKind.Pack, PackId, "team@example.com"), (call.Target, call.TargetId, call.Who));
        Assert.Equal(["thirdPartyServiceProvider"], call.Keys); // only what was saved
    }

    [Fact]
    public async Task PreviewsAndRejectedChanges_ShouldNotReachTheListeners()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var listener = new RecordingListener();
        var pipeline = SavePipelineTesting.Pipeline(store, context, listener);

        await pipeline.SaveAsync(ChangeSender.Team("team@example.com"), Change(("thirdPartyServiceProvider", "\"Preview AB\"")), preview: true);
        await pipeline.SaveAsync(ChangeSender.Team("team@example.com"), Change(("ratedCapacity", "200")));

        Assert.Empty(listener.Calls);
    }

    private static ChangeSet Change(params (string Key, string Json)[] values) =>
        new(TargetKind.Pack, PackId, values.ToDictionary(v => v.Key, v => JsonNode.Parse(v.Json), StringComparer.Ordinal), "form");

    private sealed class RecordingListener : IPackChangeListener
    {
        public List<(TargetKind Target, string TargetId, string[] Keys, string Who)> Calls { get; } = [];

        public Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, ChangeSender sender, CancellationToken cancellationToken)
        {
            Calls.Add((target, targetId, keys.ToArray(), sender.Who));
            return Task.CompletedTask;
        }
    }
}
