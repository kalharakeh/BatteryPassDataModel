using System.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;

namespace BatteryPassWeb.Dpp.RegistryToken;

/// <summary>
/// The EU's token-signing keys (R3, decision 94; the guide's §5.3): fetched from the configured key set only, never from
/// an address a token names, and kept. The keys are fetched when first needed and again when the key set's own caching
/// time is up; an unknown key ID fetches them again once. Never more than one fetch a minute, whatever asks. When the EU
/// can't be reached the keys already held keep working; without them a token is refused (the guide's §5.4).
/// </summary>
public sealed class RegistryTokenKeys(IHttpClientFactory http, RegistryTokenSettings settings, TimeProvider clock, ILogger<RegistryTokenKeys> log)
{
    /// <summary>The app's own HttpClient for the key set.</summary>
    public const string HttpClientName = "eu-registry-keys";

    /// <summary>At most one fetch in this time, the first one included.</summary>
    public static readonly TimeSpan FetchGap = TimeSpan.FromMinutes(1);

    /// <summary>How long the keys are kept when the key set's answer doesn't say; its own max-age is kept to 1 minute to 1 day.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan LongestLifetime = TimeSpan.FromDays(1);

    /// <summary>A key set larger than this isn't read.</summary>
    public const int MaxBytes = 64 * 1024;

    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyDictionary<string, JsonWebKey> _keys = new Dictionary<string, JsonWebKey>(StringComparer.Ordinal);
    private DateTimeOffset? _lastFetch;
    private DateTimeOffset _keepUntil = DateTimeOffset.MinValue;

    /// <summary>The P-384 signing key with this key ID, or null when the EU's key set has none (after the one fetch it may make).</summary>
    public async Task<JsonWebKey?> FindAsync(string keyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyId);
        if (!settings.IsOn)
        {
            return null;
        }

        if (clock.GetUtcNow() >= _keepUntil)
        {
            await FetchAsync(cancellationToken);
        }

        if (_keys.TryGetValue(keyId, out var key))
        {
            return key;
        }

        // An unknown key ID: the EU may have added a key. Fetch once more, unless a fetch was made in the last minute.
        await FetchAsync(cancellationToken);
        return _keys.TryGetValue(keyId, out key) ? key : null;
    }

    private async Task FetchAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            if (_lastFetch is { } last && now - last < FetchGap)
            {
                return; // one fetch a minute, whoever asks; a fetch that just ran for another request counts too
            }

            _lastFetch = now;
            using var timeout = new CancellationTokenSource(FetchTimeout); // not the request's: the keys serve every request
            try
            {
                var client = http.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(HttpMethod.Get, settings.JwksUrl);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    log.LogWarning("The EU registry's keys couldn't be fetched: the key set answered {Status}. The keys held so far are kept.", (int)response.StatusCode);
                    return;
                }

                var text = await ReadAsync(response.Content, timeout.Token);
                if (text is null)
                {
                    log.LogWarning("The EU registry's keys couldn't be fetched: the key set is larger than {MaxBytes} bytes. The keys held so far are kept.", MaxBytes);
                    return;
                }

                var keys = new Dictionary<string, JsonWebKey>(StringComparer.Ordinal);
                foreach (var key in new JsonWebKeySet(text).Keys)
                {
                    if (Usable(key) && !keys.ContainsKey(key.Kid))
                    {
                        keys[key.Kid] = key;
                    }
                }

                _keys = keys;
                _keepUntil = now + Lifetime(response.Headers.CacheControl);
                log.LogInformation("The EU registry's keys were fetched: {Count} signing keys.", keys.Count);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || timeout.IsCancellationRequested)
            {
                // Only the kind of failure: an answer's text never reaches the log.
                log.LogWarning("The EU registry's keys couldn't be fetched ({Failure}). The keys held so far are kept.", exception.GetType().Name);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>An EC P-384 key for signatures, with a key ID, and no private part.</summary>
    private static bool Usable(JsonWebKey key) =>
        !string.IsNullOrEmpty(key.Kid)
        && key.Kty == JsonWebAlgorithmsKeyTypes.EllipticCurve
        && key.Crv == JsonWebKeyECTypes.P384
        && !string.IsNullOrEmpty(key.X)
        && !string.IsNullOrEmpty(key.Y)
        && string.IsNullOrEmpty(key.D)
        && (string.IsNullOrEmpty(key.Use) || key.Use == JsonWebKeyUseNames.Sig)
        && (string.IsNullOrEmpty(key.Alg) || key.Alg == SecurityAlgorithms.EcdsaSha384);

    private static TimeSpan Lifetime(CacheControlHeaderValue? cache)
    {
        if (cache is null || cache.NoStore || cache.NoCache)
        {
            return cache is null ? DefaultLifetime : FetchGap;
        }

        return cache.MaxAge is { } maxAge
            ? TimeSpan.FromTicks(Math.Clamp(maxAge.Ticks, FetchGap.Ticks, LongestLifetime.Ticks))
            : DefaultLifetime;
    }

    private static async Task<string?> ReadAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaxBytes)
        {
            return null;
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxBytes + 1];
        var total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxBytes)
            {
                return null;
            }
        }

        return System.Text.Encoding.UTF8.GetString(buffer, 0, total);
    }
}
