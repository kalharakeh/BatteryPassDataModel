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
        Assert.Equal(2, keys.Fetches);
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
        Assert.Equal(2, keys.Fetches); // it tried
    }

    [Fact]
    public async Task WhileTheEuIsDown_WithoutKeys_ATokenShouldBe401_AndThePublicPartStaysOpen()
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
