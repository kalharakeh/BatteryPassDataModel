using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.RegistryToken;
using static BatteryPassWeb.Tests.Dpp.RegistryTokenTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (decision 94, G05): the EU registry's token is checked as the EU's Self-Hosted Authentication Guide says, every row
/// of its minimum conformance matrix (§12.1) that a passport site answers, and the checks of §6 and §7.1 behind them. A bad
/// token is 401; a good token with the wrong scope or organisation is 403.
/// </summary>
public sealed class RegistryTokenCheckTests : IDisposable
{
    private readonly ECDsa _key = NewKey();
    private readonly TestClock _clock = new(Start);

    public void Dispose() => _key.Dispose();

    private KeySet EuKeys() => KeySet.With((_key, KeyId));

    private Task<RegistryTokenResult> CheckAsync(string token, KeySet? keys = null, RegistryTokenSettings? settings = null) =>
        Check(keys ?? EuKeys(), _clock, settings).CheckAsync(token);

    private string Valid(Action<JsonObject>? claims = null, Action<JsonObject>? header = null)
    {
        var body = Claims(_clock.Now.AddSeconds(-5));
        claims?.Invoke(body);
        var head = Header();
        header?.Invoke(head);
        return Token(_key, head, body);
    }

    [Fact]
    public async Task AValidToken_ShouldBeAuthorised_ForTheSitesOrganisation()
    {
        var result = await CheckAsync(Valid());

        Assert.Equal(RegistryTokenVerdict.Authorised, result.Verdict);
        Assert.Equal(200, result.Status);
        Assert.Equal(Organisation, result.OrganisationId);
    }

    [Theory]
    [InlineData("at+jwt")]
    [InlineData("application/at+jwt")] // RFC 9068's long form of the same type
    public async Task TheTokenType_ShouldBeAccepted_InEitherOfItsForms(string type)
    {
        Assert.True((await CheckAsync(Valid(header: head => head["typ"] = type))).Authorised);
    }

    [Fact]
    public async Task TheIssuersClosingSlash_ShouldNotCount_OnEitherSide()
    {
        var withSlash = await CheckAsync(Valid(claims => claims["iss"] = Issuer + "/"));
        var settingWithSlash = await CheckAsync(Valid(), settings: Settings with { Issuer = Issuer + "/" });

        Assert.True(withSlash.Authorised);
        Assert.True(settingWithSlash.Authorised);
    }

    [Fact]
    public async Task AnAudienceListOfTheSitesOriginAlone_ShouldBeAccepted()
    {
        Assert.True((await CheckAsync(Valid(claims => claims["aud"] = new JsonArray(Audience)))).Authorised);
    }

    // The guide's §12.1, the rows answered 401.
    public static TheoryData<string, Action<JsonObject>?, Action<JsonObject>?> InvalidTokens() => new()
    {
        { "wrong typ", null, head => head["typ"] = "JWT" },
        { "no typ", null, head => head.Remove("typ") },
        { "ES256 named", null, head => head["alg"] = "ES256" },
        { "alg none", null, head => head["alg"] = "none" },
        { "no kid", null, head => head.Remove("kid") },
        { "crit", null, head => head["crit"] = new JsonArray("exp") },
        { "wrong issuer (a production token at an acceptance site)", claims => claims["iss"] = "https://auth.product-passport.ec.europa.eu", null },
        { "issuer with a path", claims => claims["iss"] = Issuer + "/oauth2", null },
        { "issuer with two closing slashes", claims => claims["iss"] = Issuer + "//", null },
        { "no sub", claims => claims.Remove("sub"), null },
        { "empty client_id", claims => claims["client_id"] = "", null },
        { "no jti", claims => claims.Remove("jti"), null },
        { "principal_type absent", claims => claims.Remove("principal_type"), null },
        { "principal_type USER", claims => claims["principal_type"] = "USER", null },
        { "principal_type lower case", claims => claims["principal_type"] = "system", null },
        { "lifetime above 120 s", claims => claims["exp"] = claims["iat"]!.GetValue<long>() + 121, null },
        { "exp before iat", claims => claims["exp"] = claims["iat"]!.GetValue<long>() - 1, null },
        { "no exp", claims => claims.Remove("exp"), null },
        { "no iat", claims => claims.Remove("iat"), null },
        { "exp as text", claims => claims["exp"] = claims["exp"]!.ToString(), null },
        { "nbf 31 s ahead", claims => claims["nbf"] = Start.AddSeconds(31).ToUnixTimeSeconds(), null },
        { "nbf as text", claims => claims["nbf"] = Start.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), null },
        { "wrong audience", claims => claims["aud"] = "https://another.example.com", null },
        { "audience with a closing slash", claims => claims["aud"] = Audience + "/", null },
        { "audience with a path", claims => claims["aud"] = Audience + "/25S", null },
        { "audience list of two", claims => claims["aud"] = new JsonArray(Audience, "https://another.example.com"), null },
        { "no audience", claims => claims.Remove("aud"), null },
        { "wrong audience and wrong scope: 401 first", claims => { claims["aud"] = "https://another.example.com"; claims["scope"] = "dpp:registry:write"; }, null },
    };

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task AnInvalidToken_ShouldBe401(string why, Action<JsonObject>? claims, Action<JsonObject>? header)
    {
        var result = await CheckAsync(Valid(claims, header));

        Assert.True(result.Status == 401, $"{why}: {result.Verdict} {result.Reason}");
        Assert.Null(result.OrganisationId);
    }

    [Fact]
    public async Task AnExpiredToken_ShouldBe401_AfterTheSmallClockSkew()
    {
        var token = Valid();
        _clock.Now = Start.AddSeconds(115 + 29); // 120 s after issue, plus 29 s of skew
        Assert.True((await CheckAsync(token)).Authorised);

        _clock.Now = Start.AddSeconds(115 + 31);
        var result = await CheckAsync(token);

        Assert.Equal((401, "expired"), (result.Status, result.Reason));
    }

    [Fact]
    public async Task AnNbfWithinTheSkew_ShouldBeAccepted_AndOneBeyondIt401()
    {
        var soon = await CheckAsync(Valid(claims => claims["nbf"] = Start.AddSeconds(29).ToUnixTimeSeconds()));
        var later = await CheckAsync(Valid(claims => claims["nbf"] = Start.AddSeconds(31).ToUnixTimeSeconds()));

        Assert.True(soon.Authorised);
        Assert.Equal((401, "not-yet-valid"), (later.Status, later.Reason));
    }

    [Fact]
    public async Task ANameWrittenTwiceInTheHeader_ShouldBe401()
    {
        var claims = Claims(Start).ToJsonString();
        var twice = RawToken(_key, $"{{\"typ\":\"at+jwt\",\"alg\":\"HS256\",\"alg\":\"ES384\",\"kid\":\"{KeyId}\"}}", claims);
        var once = RawToken(_key, $"{{\"typ\":\"at+jwt\",\"alg\":\"ES384\",\"kid\":\"{KeyId}\"}}", claims);

        Assert.True((await CheckAsync(once)).Authorised);
        Assert.Equal((401, "malformed"), ((await CheckAsync(twice)).Status, (await CheckAsync(twice)).Reason));
    }

    [Fact]
    public async Task ANameWrittenTwiceInTheRequestor_ShouldBe403()
    {
        var claims = Claims(Start);
        claims.Remove("resource_requestor_identifiers");
        var text = claims.ToJsonString().TrimEnd('}')
            + $",\"resource_requestor_identifiers\":[{{\"type\":\"ORG_IDENTIFIER\",\"value\":\"ORG-SOMEONE-ELSE\",\"value\":\"{Organisation}\"}}]}}";

        var result = await CheckAsync(RawToken(_key, Header().ToJsonString(), text));

        Assert.Equal((403, "requestor-unreadable"), (result.Status, result.Reason));
    }

    [Fact]
    public async Task ATokenIssuedInTheFuture_ShouldBe401_BeyondTheSkew()
    {
        var soon = Token(_key, Header(), Claims(Start.AddSeconds(25)));
        var later = Token(_key, Header(), Claims(Start.AddSeconds(40)));

        Assert.True((await CheckAsync(soon)).Authorised);
        Assert.Equal(401, (await CheckAsync(later)).Status);
    }

    [Fact]
    public async Task AnInvalidSignature_ShouldBe401()
    {
        using var other = NewKey();
        var forged = Token(other, Header(), Claims(Start)); // same kid, another key
        var valid = Valid();
        var parts = valid.Split('.');
        var claims = Claims(Start);
        claims["resource_requestor_identifiers"]![0]!["value"] = "ORG-SOMEONE-ELSE";
        var tampered = $"{parts[0]}.{Part(claims)}.{parts[2]}";

        Assert.Equal((401, "signature"), ((await CheckAsync(forged)).Status, (await CheckAsync(forged)).Reason));
        Assert.Equal((401, "signature"), ((await CheckAsync(tampered)).Status, (await CheckAsync(tampered)).Reason));
    }

    [Fact]
    public async Task AnHs256TokenSignedWithThePublicKey_ShouldBe401()
    {
        foreach (var secret in new[] { _key.ExportSubjectPublicKeyInfo(), System.Text.Encoding.UTF8.GetBytes(Jwk(_key, KeyId).ToJsonString()) })
        {
            var result = await CheckAsync(HmacToken(secret, Claims(Start)));

            Assert.Equal((401, "algorithm"), (result.Status, result.Reason));
        }
    }

    [Fact]
    public async Task AnEs256Token_ShouldBe401_EvenWithAP256KeyInTheKeySet()
    {
        using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keys = EuKeys();
        keys.Keys.Add(Jwk(p256, "p256", curve: "P-256"));
        var header = new JsonObject { ["typ"] = "at+jwt", ["alg"] = "ES256", ["kid"] = "p256" };
        var signed = $"{Part(header)}.{Part(Claims(Start))}";
        var token = $"{signed}.{Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(p256.SignData(System.Text.Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256))}";

        Assert.Equal(401, (await CheckAsync(token, keys)).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData("a..c")]
    [InlineData("e30.e30.e30")] // three parts, but {} for a header
    [InlineData("eyJhbGciOiJFUzM4NCJ9.e30.!!")]
    public async Task AMalformedToken_ShouldBe401(string token)
    {
        Assert.Equal(401, (await CheckAsync(token)).Status);
    }

    [Fact]
    public async Task ATooLongToken_ShouldBe401_BeforeAnyKeyIsFetched()
    {
        var keys = EuKeys();
        var result = await CheckAsync(Valid(claims => claims["padding"] = new string('x', RegistryTokenCheck.MaxTokenLength)), keys);

        Assert.Equal((401, "malformed"), (result.Status, result.Reason));
        Assert.Equal(0, keys.Fetches);
    }

    [Fact]
    public async Task AClaimWrittenTwice_ShouldBe401()
    {
        var valid = Valid().Split('.');
        var payload = System.Text.Encoding.UTF8.GetString(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(valid[1]));
        var twice = payload.Replace("\"iss\":", "\"iss\":\"https://evil.example\",\"iss\":", StringComparison.Ordinal);
        var signed = $"{valid[0]}.{Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(System.Text.Encoding.UTF8.GetBytes(twice))}";
        var token = $"{signed}.{Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(_key.SignData(System.Text.Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA384))}";

        Assert.Equal(401, (await CheckAsync(token)).Status);
    }

    [Fact]
    public async Task WhileTheCheckIsOff_EveryToken_ShouldBe401()
    {
        foreach (var settings in new[] { RegistryTokenSettings.Off, Settings with { OrganisationId = null }, Settings with { JwksUrl = null } })
        {
            var keys = EuKeys();
            var result = await CheckAsync(Valid(), keys, settings);

            Assert.Equal((401, "off"), (result.Status, result.Reason));
            Assert.Equal(0, keys.Fetches);
        }
    }

    // The guide's §12.1, the rows answered 403.
    public static TheoryData<string, Action<JsonObject>> ForbiddenTokens() => new()
    {
        { "scope wider", claims => claims["scope"] = "dpp:registry:read dpp:registry:write" },
        { "scope other", claims => claims["scope"] = "dpp:registry:write" },
        { "scope with a space", claims => claims["scope"] = "dpp:registry:read " },
        { "scope absent", claims => claims.Remove("scope") },
        { "scope as a list", claims => claims["scope"] = new JsonArray("dpp:registry:read") },
        { "another organisation", claims => claims["resource_requestor_identifiers"]![0]!["value"] = "ORG-SOMEONE-ELSE" },
        { "the organisation in small letters", claims => claims["resource_requestor_identifiers"]![0]!["value"] = Organisation.ToLowerInvariant() },
        { "the organisation with a space", claims => claims["resource_requestor_identifiers"]![0]!["value"] = Organisation + " " },
        { "the organisation as a suffix", claims => claims["resource_requestor_identifiers"]![0]!["value"] = "X" + Organisation },
        { "two requestor IDs", claims => claims["resource_requestor_identifiers"]!.AsArray().Add(new JsonObject { ["type"] = "ORG_IDENTIFIER", ["value"] = Organisation }) },
        { "no requestor ID", claims => claims["resource_requestor_identifiers"] = new JsonArray() },
        { "requestor claim absent", claims => claims.Remove("resource_requestor_identifiers") },
        { "requestor as an object", claims => claims["resource_requestor_identifiers"] = new JsonObject { ["type"] = "ORG_IDENTIFIER", ["value"] = Organisation } },
        { "requestor of type UOI", claims => claims["resource_requestor_identifiers"]![0]!["type"] = "UOI" },
        { "requestor without a type", claims => claims["resource_requestor_identifiers"]![0]!.AsObject().Remove("type") },
        { "requestor with an empty value", claims => claims["resource_requestor_identifiers"]![0]!["value"] = "" },
    };

    [Theory]
    [MemberData(nameof(ForbiddenTokens))]
    public async Task AValidTokenThatMayNotRead_ShouldBe403(string why, Action<JsonObject> claims)
    {
        var result = await CheckAsync(Valid(claims));

        Assert.True(result.Status == 403, $"{why}: {result.Verdict} {result.Reason}");
        Assert.Null(result.OrganisationId);
    }

    [Fact]
    public async Task TheScope_ShouldBeCheckedBeforeTheRequestor_AsTheGuideOrdersThem()
    {
        var result = await CheckAsync(Valid(claims =>
        {
            claims["scope"] = "dpp:registry:write";
            claims["resource_requestor_identifiers"]![0]!["value"] = "ORG-SOMEONE-ELSE";
        }));

        Assert.Equal(RegistryTokenVerdict.InsufficientScope, result.Verdict);
    }

    [Fact]
    public async Task AKeyInTheToken_ShouldBeIgnored_EvenUnderTheEusOwnKeyId()
    {
        using var attacker = NewKey();
        var keys = EuKeys();
        var header = Header(); // the EU's own kid, so the check reaches the signature
        header["jku"] = "https://attacker.example/jwks";
        header["x5u"] = "https://attacker.example/cert";
        header["jwk"] = Jwk(attacker, KeyId);

        var result = await CheckAsync(Token(attacker, header, Claims(Start)), keys);

        Assert.Equal((401, "signature"), (result.Status, result.Reason));
        Assert.All(keys.Asked, address => Assert.Equal(JwksUrl, address));
        Assert.True((await CheckAsync(Token(_key, header, Claims(Start)), keys)).Authorised); // the same header, signed by the EU's key
    }

    [Fact]
    public async Task AKeyAddressInTheToken_ShouldBeIgnored()
    {
        using var attacker = NewKey();
        var keys = EuKeys();
        var header = Header("attacker-key");
        header["jku"] = "https://attacker.example/jwks";
        header["x5u"] = "https://attacker.example/cert";
        header["jwk"] = Jwk(attacker, "attacker-key");

        var result = await CheckAsync(Token(attacker, header, Claims(Start)), keys);

        Assert.Equal((401, "unknown-key"), (result.Status, result.Reason));
        Assert.All(keys.Asked, address => Assert.Equal(JwksUrl, address));
    }
}
