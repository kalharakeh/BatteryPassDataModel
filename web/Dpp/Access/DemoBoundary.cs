using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>
/// Reads what <see cref="DemoScope.Inside"/> needs (the pack, its batch, model and operator, and its cluster) and answers
/// for demo readers and senders only, so a real reader or sender costs nothing extra (finding R03).
/// </summary>
public sealed class DemoBoundary(DppStore store, ClusterRepository clusters)
{
    /// <summary>Whether the battery is inside the demo.</summary>
    public async Task<bool> InsideAsync(BatteryPack pack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (!pack.IsDemo)
        {
            return false;
        }

        var batch = await store.GetAsync<ProductionBatch>(pack.BatchId, cancellationToken);
        var model = batch is null ? null : await store.GetAsync<BatteryModel>(batch.ModelId, cancellationToken);
        var economicOperator = await store.GetAsync<EconomicOperator>(pack.OperatorId, cancellationToken);
        return DemoScope.Inside(pack, batch, model, economicOperator, await ClusterIsDemoOrNoneAsync(pack.ClusterId, cancellationToken));
    }

    /// <summary>Whether the cluster is a demo cluster, or there is none.</summary>
    public async Task<bool> ClusterIsDemoOrNoneAsync(string? clusterId, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(clusterId) || ClusterRepository.IsDemo(await clusters.GetClusterByIdAsync(clusterId, cancellationToken));

    /// <summary>The reader as it counts on this battery (<see cref="DemoScope.On"/>).</summary>
    public async Task<Reader> OnAsync(Reader reader, BatteryPack pack, CancellationToken cancellationToken = default) =>
        reader.DemoOnly ? DemoScope.On(reader, await InsideAsync(pack, cancellationToken)) : reader;

    public async Task<Reader> OnAsync(Reader reader, string packId, CancellationToken cancellationToken = default) =>
        !reader.DemoOnly ? reader
        : await store.GetAsync<BatteryPack>(packId, cancellationToken) is { } pack ? await OnAsync(reader, pack, cancellationToken)
        : DemoScope.On(reader, false);

    /// <summary>Whether the reader's roles count on this battery (<see cref="DemoScope.Reaches(Reader, bool)"/>).</summary>
    public async Task<bool> ReachesAsync(Reader reader, BatteryPack pack, CancellationToken cancellationToken = default) =>
        !reader.DemoOnly || await InsideAsync(pack, cancellationToken);

    /// <summary>Whether the sender may change this battery at all (<see cref="DemoScope.Reaches(ChangeSender, bool)"/>).</summary>
    public async Task<bool> ReachesAsync(ChangeSender sender, BatteryPack pack, CancellationToken cancellationToken = default) =>
        !sender.DemoOnly || await InsideAsync(pack, cancellationToken);

    public async Task<bool> ReachesAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default) =>
        !sender.DemoOnly || (await store.GetAsync<BatteryPack>(packId, cancellationToken) is { } pack && await InsideAsync(pack, cancellationToken));
}
