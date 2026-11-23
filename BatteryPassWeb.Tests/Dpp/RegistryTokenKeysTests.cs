using System.Security.Cryptography;
using System.Text.Json.Nodes;
using static BatteryPassWeb.Tests.Dpp.RegistryTokenTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (decision 94; the guide's §5.3 and §5.4): the EU's keys are fetched from the configured key set, kept, fetched again
/// once on an unknown key ID and never more than once a minute; while the EU can't be reached, the keys held keep working,
/// and without them a token is refused.
/// </summary>
public sealed class RegistryTokenKeysTests : IDisposable
{
    private readonly ECDsa _key = NewKey();
    private readonly TestClock _clock = new(Start);

    public void Dispose() => _key.Dispose();

    private string TokenNow(ECDsa? key = null, string keyId = KeyId) => Token(key ?? _key, Header(keyId), Claims(_clock.Now));

    [Fact]
    public async Task TheKeys_ShouldBeFetchedOnce_AndKept()
    {
        var keys = KeySet.With((_key, KeyId));
        var check = Check(keys, _clock);

        for (var i = 0; i < 5; i++)
        {
            Assert.True((await check.CheckAsync(TokenNow())).Authorised);
            _clock.Now = _clock.Now.AddMinutes(5);
        }

        Assert.Equal(1, keys.Fetches); // kept an hour when the key set doesn't say
        Assert.Equal([JwksUrl], keys.Asked);
    }

    [Fact]
    public async Task AnUnknownKeyId_ShouldFetchTheKeysOnce_AndFindANewKey()
    {
        using var next = NewKey();
        var keys = KeySet.With((_key, KeyId));
        var check = Check(keys, _clock);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);

        _clock.Now = _clock.Now.AddMinutes(2);
        keys.Keys.Add(Jwk(next, "next-key")); // the EU adds a key while rotating
        var rotated = await check.CheckAsync(TokenNow(next, "next-key"));

        Assert.True(rotated.Authorised);
        Assert.Equal(2, keys.Fetches);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised); // the old key, still listed, still works
        Assert.Equal(2, keys.Fetches);
    }

    [Fact]
    public async Task AnUnknownKeyId_ShouldBe401_AfterOneFetch_AndNotFetchAgainWithinAMinute()
    {
        using var stranger = NewKey();
        var keys = KeySet.With((_key, KeyId));
        var check = Check(keys, _clock);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        _clock.Now = _clock.Now.AddMinutes(2);

        var first = await check.CheckAsync(TokenNow(stranger, "unknown"));
        Assert.Equal((401, "unknown-key"), (first.Status, first.Reason));
        Assert.Equal(2, keys.Fetches); // fetched once for it

        for (var i = 0; i < 10; i++)
        {
            _clock.Now = _clock.Now.AddSeconds(5);
            Assert.Equal(401, (await check.CheckAsync(TokenNow(stranger, $"unknown-{i}"))).Status);
        }

        Assert.Equal(2, keys.Fetches); // never more than one fetch a minute, whatever the key IDs

        _clock.Now = _clock.Now.AddSeconds(15);
        Assert.Equal(401, (await check.CheckAsync(TokenNow(stranger, "unknown"))).Status);
        Assert.Equal(3, keys.Fetches);
    }

    [Fact]
    public async Task TheKeySetsOwnCachingTime_ShouldBeHonoured()
    {
        var keys = KeySet.With((_key, KeyId));
        keys.CacheControl = "public, max-age=300";
        var check = Check(keys, _clock);

        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        _clock.Now = _clock.Now.AddSeconds(299);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        Assert.Equal(1, keys.Fetches);
        _clock.Now = _clock.Now.AddSeconds(2);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        await WaitForAsync(() => keys.Fetches == 2); // refreshed in the background
    }

    [Fact]
    public async Task WhileTheEuIsDown_TheKeysHeld_ShouldKeepWorking()
    {
        var keys = KeySet.With((_key, KeyId));
        keys.CacheControl = "max-age=60";
        var check = Check(keys, _clock);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);

        keys.Down = true;
        _clock.Now = _clock.Now.AddMinutes(10);

        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        await WaitForAsync(() => keys.Fetches == 2); // it tried, in the background
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
    }

    [Fact]
    public async Task WhileTheEuIsDown_WithoutKeys_ATokenShouldBe401_UntilTheKeySetAnswersAgain()
    {
        var keys = KeySet.With((_key, KeyId));
        keys.Down = true;
        var check = Check(keys, _clock);

        Assert.Equal((401, "unknown-key"), ((await check.CheckAsync(TokenNow())).Status, (await check.CheckAsync(TokenNow())).Reason));
        Assert.Equal(1, keys.Fetches);

        keys.Down = false;
        _clock.Now = _clock.Now.AddSeconds(61);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised); // back once the EU is
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"keys\": 3}")]
    [InlineData("[]")]
    public async Task AKeySetThatCantBeRead_ShouldGiveNoKeys(string body)
    {
        var keys = KeySet.With((_key, KeyId));
        keys.Body = body;

        Assert.Equal(401, (await Check(keys, _clock).CheckAsync(TokenNow())).Status);
    }

    [Fact]
    public async Task ATooLargeKeySetWithoutALength_ShouldNotBeRead()
    {
        var keys = KeySet.With((_key, KeyId));
        keys.Unsized = true;
        var check = Check(keys, _clock);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised); // the same answer, small enough, is read

        var large = KeySet.With((_key, KeyId));
        large.Unsized = true;
        large.Body = new JsonObject { ["keys"] = new JsonArray(Jwk(_key, KeyId)), ["padding"] = new string('x', BatteryPassWeb.Dpp.RegistryToken.RegistryTokenKeys.MaxBytes) }.ToJsonString();

        Assert.Equal(401, (await Check(large, _clock).CheckAsync(TokenNow())).Status);
    }

    [Fact]
    public async Task ATooLargeKeySet_ShouldNotBeRead()
    {
        var keys = KeySet.With((_key, KeyId));
        keys.Body = new JsonObject { ["keys"] = new JsonArray(Jwk(_key, KeyId)), ["padding"] = new string('x', BatteryPassWeb.Dpp.RegistryToken.RegistryTokenKeys.MaxBytes) }.ToJsonString();

        Assert.Equal(401, (await Check(keys, _clock).CheckAsync(TokenNow())).Status);
    }

    [Theory]
    [InlineData("P-256")]
    [InlineData("P-521")]
    public async Task AKeyOnAnotherCurve_ShouldNotBeUsed(string curve)
    {
        var keys = new KeySet();
        keys.Keys.Add(Jwk(_key, KeyId, curve: curve));

        Assert.Equal(401, (await Check(keys, _clock).CheckAsync(TokenNow())).Status);
    }

    [Fact]
    public async Task AKeyForEncryption_ShouldNotBeUsed()
    {
        var keys = new KeySet();
        keys.Keys.Add(Jwk(_key, KeyId, use: "enc"));

        Assert.Equal(401, (await Check(keys, _clock).CheckAsync(TokenNow())).Status);
    }

    [Fact]
    public async Task AKnownKey_ShouldNeverWaitForAFetch_WhileTheKeySetHangs()
    {
        var keys = KeySet.With((_key, KeyId));
        keys.CacheControl = "max-age=60";
        var check = Check(keys, _clock);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        _clock.Now = _clock.Now.AddMinutes(2); // the keys are due
        keys.Hang = true; // and the EU's key set doesn't answer
        var token = TokenNow();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => check.CheckAsync(token))));
        watch.Stop();

        Assert.All(results, result => Assert.True(result.Authorised));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"the reads waited {watch.Elapsed}");
        await WaitForAsync(() => keys.Fetches == 2); // one refresh, in the background
        Assert.Equal(2, keys.Fetches);
    }

    [Fact]
    public async Task AnUnknownKey_MayWaitForTheFetch_ButNoLongerThanItsTimeout()
    {
        using var stranger = NewKey();
        var keys = KeySet.With((_key, KeyId));
        var settings = Settings;
        var keyCache = new BatteryPassWeb.Dpp.RegistryToken.RegistryTokenKeys(keys, settings, _clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<BatteryPassWeb.Dpp.RegistryToken.RegistryTokenKeys>.Instance) { Timeout = TimeSpan.FromSeconds(1) };
        var check = new BatteryPassWeb.Dpp.RegistryToken.RegistryTokenCheck(settings, keyCache, _clock);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        _clock.Now = _clock.Now.AddMinutes(2);
        keys.Hang = true;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var unknown = await check.CheckAsync(TokenNow(stranger, "unknown"));
        watch.Stop();

        Assert.Equal((401, "unknown-key"), (unknown.Status, unknown.Reason));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"waited {watch.Elapsed}");
        Assert.True((await check.CheckAsync(TokenNow())).Authorised); // the known key still answers at once
    }

    [Fact]
    public async Task TheKeySetsExpires_ShouldBeHonoured_WithoutMaxAge()
    {
        var keys = KeySet.With((_key, KeyId));
        keys.Date = Start;
        keys.Expires = Start.AddMinutes(5);
        var check = Check(keys, _clock);

        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        _clock.Now = Start.AddSeconds(299);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        Assert.Equal(1, keys.Fetches);
        _clock.Now = Start.AddSeconds(301);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        await WaitForAsync(() => keys.Fetches == 2);
    }

    [Theory]
    [InlineData("max-age=600", null, 300, 299, 301)] // 600 s less an Age of 300
    [InlineData(null, 600, 300, 299, 301)] // Expires 600 s after Date, less an Age of 300
    [InlineData(null, -60, null, 59, 61)] // Expires already past: a minute, never less
    public async Task TheAge_ShouldBeTakenOff_AndThePastKeptToAMinute(string? cacheControl, int? expiresInSeconds, int? ageSeconds, int stillKept, int refreshed)
    {
        var keys = KeySet.With((_key, KeyId));
        keys.CacheControl = cacheControl;
        keys.Date = Start;
        keys.Expires = expiresInSeconds is { } seconds ? Start.AddSeconds(seconds) : null;
        keys.Age = ageSeconds is { } age ? TimeSpan.FromSeconds(age) : null;
        var check = Check(keys, _clock);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);

        _clock.Now = Start.AddSeconds(stillKept);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        Assert.Equal(1, keys.Fetches);
        _clock.Now = Start.AddSeconds(refreshed);
        Assert.True((await check.CheckAsync(TokenNow())).Authorised);
        await WaitForAsync(() => keys.Fetches == 2);
    }

    [Fact]
    public async Task TheFetch_ShouldAskForAKeySet()
    {
        var keys = KeySet.With((_key, KeyId));

        Assert.True((await Check(keys, _clock).CheckAsync(TokenNow())).Authorised);

        Assert.Contains("application/jwk-set+json", Assert.Single(keys.Accepts), StringComparison.Ordinal);
    }

    /// <summary>A refresh runs in the background: waits a little for it.</summary>
    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task ManyRequestsAtOnce_ShouldFetchTheKeysOnce()
    {
        var keys = KeySet.With((_key, KeyId));
        var check = Check(keys, _clock);
        var token = TokenNow();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => check.CheckAsync(token))));

        Assert.All(results, result => Assert.True(result.Authorised));
        Assert.Equal(1, keys.Fetches);
    }
}
