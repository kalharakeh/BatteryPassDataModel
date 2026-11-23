using System.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;

namespace BatteryPassWeb.Dpp.RegistryToken;

/// <summary>
/// The EU's token-signing keys (R3, decision 94; the guide's §5.3): fetched from the configured key set only, never from
/// an address a token names, and kept as long as the key set's own cache headers say (Cache-Control max-age, else
/// Expires, less Age). A known key ID never waits for a fetch: once the keys are due, they keep serving while one refresh
/// runs in the background. Only an unknown key ID, or no keys at all, waits for a fetch. Never more than one fetch a
/// minute, whatever asks. When the EU can't be reached the keys already held keep working; without them a token is
/// refused and the fetch is tried again a minute later (the guide's §5.4 and §11). The keys are also fetched once in the
/// background when the app starts, so a wrong or unreachable address shows in the start log.
/// </summary>
public sealed class RegistryTokenKeys(IHttpClientFactory http, RegistryTokenSettings settings, TimeProvider clock, ILogger<RegistryTokenKeys> log)
{
    /// <summary>The app's own HttpClient for the key set.</summary>
    public const string HttpClientName = "eu-registry-keys";

    /// <summary>At most one fetch in this time, the first one included.</summary>
    public static readonly TimeSpan FetchGap = TimeSpan.FromMinutes(1);

    /// <summary>How long the keys are kept when the key set's answer doesn't say; what it says is kept to 1 minute to 1 day.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan LongestLifetime = TimeSpan.FromDays(1);

    /// <summary>A key set larger than this isn't read.</summary>
    public const int MaxBytes = 64 * 1024;

    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The time a fetch may take; <see cref="FetchTimeout"/> unless a test shortens it.</summary>
    public TimeSpan Timeout { get; init; } = FetchTimeout;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyDictionary<string, JsonWebKey> _keys = new Dictionary<string, JsonWebKey>(StringComparer.Ordinal);
    private DateTimeOffset? _lastFetch;
    private DateTimeOffset _keepUntil = DateTimeOffset.MinValue;
    private int _refreshing;

    /// <summary>
    /// The P-384 signing key with this key ID, or null when the EU's key set has none. A key already held is returned at
    /// once; an unknown one waits for the one fetch it may make.
    /// </summary>
    public async Task<JsonWebKey?> FindAsync(string keyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyId);
        if (!settings.IsOn)
        {
            return null;
        }

        if (_keys.TryGetValue(keyId, out var key))
        {
            if (clock.GetUtcNow() >= _keepUntil)
            {
                RefreshInTheBackground(); // the keys held keep serving meanwhile
            }

            return key;
        }

        // An unknown key ID, or no keys yet: the EU may have added a key. Fetch, unless a fetch was made in the last minute.
        await FetchAsync(cancellationToken);
        return _keys.TryGetValue(keyId, out key) ? key : null;
    }

    /// <summary>The fetch at start (in the background): a key set that can't be read gives one warning naming its address.</summary>
    public async Task WarmUpAsync()
    {
        if (settings.IsOn)
        {
            await FetchAsync(CancellationToken.None);
        }
    }

    private void RefreshInTheBackground()
    {
        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
        {
            return; // one refresh at a time
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await FetchAsync(CancellationToken.None);
            }
            finally
            {
                Volatile.Write(ref _refreshing, 0);
            }
        });
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
            using var timeout = new CancellationTokenSource(Timeout); // not the request's: the keys serve every request
            try
            {
                var client = http.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(HttpMethod.Get, settings.JwksUrl);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jwk-set+json"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    log.LogWarning("The EU registry's keys couldn't be fetched from {Address}: it answered {Status}. Tokens are checked with the keys held so far, if any.", settings.JwksUrl, (int)response.StatusCode);
                    return;
                }

                var text = await ReadAsync(response.Content, timeout.Token);
                if (text is null)
                {
                    log.LogWarning("The EU registry's keys couldn't be fetched from {Address}: the key set is larger than {MaxBytes} bytes. Tokens are checked with the keys held so far, if any.", settings.JwksUrl, MaxBytes);
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
                _keepUntil = now + Lifetime(response, now);
                if (keys.Count == 0)
                {
                    log.LogWarning("The EU registry's key set at {Address} holds no P-384 signing key: every token will be refused.", settings.JwksUrl);
                }
                else
                {
                    log.LogInformation("The EU registry's keys were fetched from {Address}: {Count} signing keys.", settings.JwksUrl, keys.Count);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException || timeout.IsCancellationRequested)
            {
                // Only the kind of failure: an answer's text never reaches the log.
                log.LogWarning("The EU registry's keys couldn't be fetched from {Address} ({Failure}). Tokens are checked with the keys held so far, if any.", settings.JwksUrl, timeout.IsCancellationRequested ? "no answer in time" : exception.GetType().Name);
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

    /// <summary>
    /// How long the answer stays fresh (RFC 9111 §4.2): Cache-Control max-age, else Expires less Date (or now), less Age;
    /// no-store or no-cache: a minute; nothing said: an hour. Kept to 1 minute to 1 day.
    /// </summary>
    private static TimeSpan Lifetime(HttpResponseMessage response, DateTimeOffset now)
    {
        var cache = response.Headers.CacheControl;
        if (cache is { NoStore: true } or { NoCache: true })
        {
            return FetchGap;
        }

        TimeSpan? fresh = cache?.MaxAge is { } maxAge
            ? maxAge
            : response.Content.Headers.Expires is { } expires ? expires - (response.Headers.Date ?? now) : null;
        if (fresh is not { } lifetime)
        {
            return DefaultLifetime;
        }

        lifetime -= response.Headers.Age ?? TimeSpan.Zero;
        return TimeSpan.FromTicks(Math.Clamp(lifetime.Ticks, FetchGap.Ticks, LongestLifetime.Ticks));
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
