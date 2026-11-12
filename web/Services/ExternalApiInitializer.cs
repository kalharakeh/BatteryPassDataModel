namespace BatteryPassWeb.Services;

/// <summary>
/// Prepares the API tokens at startup: their indexes, the clusters' indexes, and each registered cluster's default read
/// and read-write tokens (a cluster token neither creates nor publishes, so it gets no "Full access" token). The old
/// model's sample passport, sample tokens and data repairs went with step 3d-1.
/// </summary>
public sealed class ExternalApiInitializer
{
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public ExternalApiInitializer(ExternalApiRepository externalApiRepository, ClusterRepository clusterRepository)
    {
        _externalApiRepository = externalApiRepository;
        _clusterRepository = clusterRepository;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _externalApiRepository.EnsureIndexesAsync(cancellationToken);
            await _clusterRepository.EnsureIndexesAsync(cancellationToken);
            await EnsureClusterTokensAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureClusterTokensAsync(CancellationToken cancellationToken)
    {
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        if (clusters.Count == 0)
        {
            return;
        }

        var existingTokens = await _externalApiRepository.ListTokensAsync(cancellationToken);
        foreach (var cluster in clusters)
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            var clusterName = BsonHelpers.GetString(cluster, "name");
            if (string.IsNullOrWhiteSpace(clusterId))
            {
                continue;
            }

            var hasReadToken = existingTokens.Any(token =>
                BsonHelpers.GetString(token, "autoClusterId").Equals(clusterId, StringComparison.OrdinalIgnoreCase)
                && BsonHelpers.GetString(token, "accessMode").Equals("read", StringComparison.OrdinalIgnoreCase));
            if (!hasReadToken)
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - read",
                    ExternalTokenAccessMode.Read,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: "system",
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
            }

            var hasReadWriteToken = existingTokens.Any(token =>
                BsonHelpers.GetString(token, "autoClusterId").Equals(clusterId, StringComparison.OrdinalIgnoreCase)
                && BsonHelpers.GetString(token, "accessMode").Equals("readWrite", StringComparison.OrdinalIgnoreCase));
            if (!hasReadWriteToken)
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - readwrite",
                    ExternalTokenAccessMode.ReadWrite,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: "system",
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
