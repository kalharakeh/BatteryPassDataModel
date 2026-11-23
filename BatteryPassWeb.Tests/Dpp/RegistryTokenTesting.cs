using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.RegistryToken;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3: an EU registry of the test's own. An ES384 key pair made in the test, its key set served by a fake handler that
/// counts the fetches (no network), a clock the test moves, and tokens written by hand so every header and claim can be
/// wrong on purpose.
/// </summary>
internal static class RegistryTokenTesting
{
    public const string Issuer = "https://auth.acc.product-passport.ec.europa.eu";
    public const string JwksUrl = "https://auth.acc.product-passport.ec.europa.eu/oauth2/jwks";
    public const string Audience = "https://www.sib-scania-eubp.eu";
    public const string Organisation = "ORG-7Q2-TEST";
    public const string KeyId = "dpp-as-ec-p384-test";

    public static RegistryTokenSettings Settings { get; } = new(Issuer, JwksUrl, Audience, Organisation);

    public static DateTimeOffset Start { get; } = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A fresh P-384 key.</summary>
    public static ECDsa NewKey() => ECDsa.Create(ECCurve.NamedCurves.nistP384);

    /// <summary>The public half of a key as a JWK, as the EU's key set lists it.</summary>
    public static JsonObject Jwk(ECDsa key, string keyId, string curve = "P-384", string? use = "sig")
    {
        var parameters = key.ExportParameters(false);
        var jwk = new JsonObject
        {
            ["kty"] = "EC",
            ["crv"] = curve,
            ["kid"] = keyId,
            ["x"] = Base64UrlEncoder.Encode(parameters.Q.X!),
            ["y"] = Base64UrlEncoder.Encode(parameters.Q.Y!),
            ["alg"] = "ES384",
        };
        if (use is not null)
        {
            jwk["use"] = use;
        }

        return jwk;
    }

    /// <summary>The header of a token as the EU writes it.</summary>
    public static JsonObject Header(string keyId = KeyId) => new() { ["typ"] = "at+jwt", ["alg"] = "ES384", ["kid"] = keyId };

    /// <summary>The claims of a valid token issued at <paramref name="issuedAt"/>, for 120 seconds.</summary>
    public static JsonObject Claims(DateTimeOffset issuedAt, int lifetimeSeconds = 120) => new()
    {
        ["iss"] = Issuer,
        ["sub"] = "dpp-registry",
        ["client_id"] = "dpp-registry-acc",
        ["principal_type"] = "SYSTEM",
        ["aud"] = Audience,
        ["scope"] = "dpp:registry:read",
        ["resource_requestor_identifiers"] = new JsonArray(new JsonObject { ["type"] = "ORG_IDENTIFIER", ["value"] = Organisation }),
        ["iat"] = issuedAt.ToUnixTimeSeconds(),
        ["exp"] = issuedAt.ToUnixTimeSeconds() + lifetimeSeconds,
        ["jti"] = $"at_{Guid.NewGuid():N}",
    };

    /// <summary>A compact token signed with ES384 by <paramref name="key"/>.</summary>
    public static string Token(ECDsa key, JsonObject header, JsonObject claims)
    {
        var signed = $"{Part(header)}.{Part(claims)}";
        return $"{signed}.{Base64UrlEncoder.Encode(key.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA384))}";
    }

    /// <summary>A compact token from header and claims written as raw JSON text (to write a name twice), signed with ES384.</summary>
    public static string RawToken(ECDsa key, string header, string claims)
    {
        var signed = $"{Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(header))}.{Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(claims))}";
        return $"{signed}.{Base64UrlEncoder.Encode(key.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA384))}";
    }

    /// <summary>A token signed with HMAC-SHA256, its secret the public key: the old trick of a verifier that trusts the token's alg.</summary>
    public static string HmacToken(byte[] secret, JsonObject claims)
    {
        var header = new JsonObject { ["typ"] = "at+jwt", ["alg"] = "HS256", ["kid"] = KeyId };
        var signed = $"{Part(header)}.{Part(claims)}";
        return $"{signed}.{Base64UrlEncoder.Encode(HMACSHA256.HashData(secret, Encoding.ASCII.GetBytes(signed)))}";
    }

    public static string Part(JsonNode node) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(node.ToJsonString()));

    public static RegistryTokenCheck Check(KeySet keySet, TestClock clock, RegistryTokenSettings? settings = null)
    {
        var used = settings ?? Settings;
        return new RegistryTokenCheck(used, new RegistryTokenKeys(keySet, used, clock, NullLogger<RegistryTokenKeys>.Instance), clock);
    }

    /// <summary>Content whose length isn't known before it is read: no Content-Length header.</summary>
    public sealed class UnsizedContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new OneWayStream(bytes));
    }

    /// <summary>A stream that can only be read forward, in small pieces, and has no length.</summary>
    private sealed class OneWayStream(byte[] bytes) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var take = Math.Min(Math.Min(count, 4096), bytes.Length - _position);
            Array.Copy(bytes, _position, buffer, offset, take);
            _position += take;
            return take;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A clock the test sets.</summary>
    public sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>
    /// The EU's key set at <see cref="JwksUrl"/>, served by a fake handler as an <see cref="IHttpClientFactory"/>: the keys
    /// it lists, its Cache-Control, whether it answers, and every address asked.
    /// </summary>
    public sealed class KeySet : HttpMessageHandler, IHttpClientFactory
    {
        public List<JsonObject> Keys { get; } = [];

        public List<string> Asked { get; } = [];

        public int Fetches
        {
            get
            {
                lock (Asked)
                {
                    return Asked.Count;
                }
            }
        }

        public bool Down { get; set; }

        public string? CacheControl { get; set; }

        /// <summary>Text to serve in place of the key set, when set.</summary>
        public string? Body { get; set; }

        /// <summary>Serve the body without a Content-Length, as a chunked answer would come.</summary>
        public bool Unsized { get; set; }

        /// <summary>Never answer: the fetch waits until it is cancelled.</summary>
        public bool Hang { get; set; }

        /// <summary>The answer's Date, Expires and Age headers, when set.</summary>
        public DateTimeOffset? Date { get; set; }

        public DateTimeOffset? Expires { get; set; }

        public TimeSpan? Age { get; set; }

        /// <summary>The Accept header of every fetch.</summary>
        public List<string> Accepts { get; } = [];

        public static KeySet With(params (ECDsa Key, string KeyId)[] keys)
        {
            var set = new KeySet();
            foreach (var (key, keyId) in keys)
            {
                set.Keys.Add(Jwk(key, keyId));
            }

            return set;
        }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Asked)
            {
                Asked.Add(request.RequestUri!.ToString());
                Accepts.Add(request.Headers.Accept.ToString());
            }

            if (Hang)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (Down)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            var body = Body ?? new JsonObject { ["keys"] = new JsonArray(Keys.Select(key => (JsonNode)key.DeepClone()).ToArray()) }.ToJsonString();
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = Unsized ? new UnsizedContent(Encoding.UTF8.GetBytes(body)) : new StringContent(body, Encoding.UTF8, "application/json") };
            if (CacheControl is not null)
            {
                response.Headers.TryAddWithoutValidation("Cache-Control", CacheControl);
            }

            response.Headers.Date = Date;
            response.Headers.Age = Age;
            response.Content.Headers.Expires = Expires;
            return response;
        }
    }
}
