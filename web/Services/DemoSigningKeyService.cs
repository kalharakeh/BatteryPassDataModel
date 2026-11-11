using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Services;

/// <summary>
/// The app's signing key and the name it signs under (finding R23, the owner's decision of 10 Nov 2026). The issuer is the
/// passport's own web address: <c>did:web:</c> and the host of the battery IDs' address (DPP_ID_BASE_URL). The key ID is
/// made from the key itself, so a new key gets a new ID. The keys the app trusts are its current key and the older
/// public keys listed in <see cref="TrustedKeysSetting"/>, so that passports signed before a key change still verify.
/// </summary>
public sealed class DemoSigningKeyService : IDisposable
{
    /// <summary>Older public keys the app still trusts: base64 SubjectPublicKeyInfo (DER), separated by commas.</summary>
    public const string TrustedKeysSetting = "BATTERY_PASS_SIGNING_TRUSTED_PUBLIC_KEYS";

    /// <summary>Where the app publishes its keys for the issuer's name (the did:web rule for a name without a path).</summary>
    public const string DidDocumentPath = "/.well-known/did.json";

    /// <summary>The issuer when no address is set at all, as on a developer's PC.</summary>
    private const string LocalHost = "localhost";

    private readonly ECDsa _key;
    private readonly bool _ownsKey;

    /// <summary>The app's own key: BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64, else the local test key it makes for itself.</summary>
    public DemoSigningKeyService(string? idBaseUrl, string? appBaseUrl, string? trustedPublicKeys)
        : this(LoadOrCreateKey(), ownsKey: true, Host(idBaseUrl) ?? Host(appBaseUrl), trustedPublicKeys)
    {
    }

    /// <summary>A given key, for tests: the issuer comes from <paramref name="idBaseUrl"/>.</summary>
    public DemoSigningKeyService(ECDsa key, string? idBaseUrl = null, string? trustedPublicKeys = null)
        : this(key, ownsKey: false, Host(idBaseUrl), trustedPublicKeys)
    {
    }

    private DemoSigningKeyService(ECDsa key, bool ownsKey, string? host, string? trustedPublicKeys)
    {
        _key = key;
        _ownsKey = ownsKey;
        Issuer = $"did:web:{(host ?? LocalHost).Replace(":", "%3A", StringComparison.Ordinal)}";
        var own = key.ExportParameters(includePrivateParameters: false).Q;
        KeyId = Thumbprint(own);
        TrustedKeys = new[] { own }
            .Concat(ReadKeys(trustedPublicKeys).Where(key => key is not null).Select(key => key!.Value))
            .DistinctBy(Thumbprint)
            .ToArray();
    }

    /// <summary>The issuer's name: <c>did:web:www.sib-scania-eubp.eu</c> for https://www.sib-scania-eubp.eu.</summary>
    public string Issuer { get; }

    /// <summary>The key's JWK thumbprint (RFC 7638), base64url.</summary>
    public string KeyId { get; }

    public string VerificationMethod => $"{Issuer}#{KeyId}";

    /// <summary>The public points of the keys the app trusts: its own first, then the listed older ones.</summary>
    public IReadOnlyList<ECPoint> TrustedKeys { get; }

    /// <summary>True when the public point is one the app trusts.</summary>
    public bool Trusts(byte[] x, byte[] y) =>
        TrustedKeys.Any(key => key.X is not null && key.Y is not null && key.X.AsSpan().SequenceEqual(x) && key.Y.AsSpan().SequenceEqual(y));

    /// <summary>
    /// The issuer's DID document, served at <see cref="DidDocumentPath"/>: each trusted key as a JSON Web Key under the
    /// issuer's name, its ID made from the key. Public keys only.
    /// </summary>
    public JsonObject DidDocument()
    {
        var methods = new JsonArray();
        var assertions = new JsonArray();
        foreach (var key in TrustedKeys)
        {
            var id = $"{Issuer}#{Thumbprint(key)}";
            methods.Add(new JsonObject
            {
                ["id"] = id,
                ["type"] = "JsonWebKey2020",
                ["controller"] = Issuer,
                ["publicKeyJwk"] = new JsonObject
                {
                    ["kty"] = "EC",
                    ["crv"] = "P-256",
                    ["x"] = Base64Url.Encode(key.X!),
                    ["y"] = Base64Url.Encode(key.Y!),
                },
            });
            assertions.Add(id);
        }

        return new JsonObject
        {
            ["@context"] = new JsonArray("https://www.w3.org/ns/did/v1", "https://w3id.org/security/suites/jws-2020/v1"),
            ["id"] = Issuer,
            ["verificationMethod"] = methods,
            ["assertionMethod"] = assertions,
        };
    }

    /// <summary>The current key's public key as base64 SubjectPublicKeyInfo: what goes into the setting after a key change.</summary>
    public string PublicKeyText() => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    /// <summary>Why the trusted keys setting can't be read, or null. Names the entry by its place, never its text.</summary>
    public static string? UnreadableTrustedKeys(string? setting)
    {
        var keys = ReadKeys(setting);
        var bad = keys.Select((key, index) => (key, index)).Where(entry => entry.key is null).Select(entry => (entry.index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return bad.Length == 0
            ? null
            : $"{TrustedKeysSetting} has an entry that isn't a P-256 public key (entry {string.Join(", ", bad)}). List each older public key as base64 SubjectPublicKeyInfo, separated by commas.";
    }

    public byte[] SignData(byte[] data)
    {
        return _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    public ECParameters ExportPublicParameters()
    {
        return _key.ExportParameters(includePrivateParameters: false);
    }

    public void Dispose()
    {
        if (_ownsKey)
        {
            _key.Dispose();
        }
    }

    /// <summary>The JWK thumbprint (RFC 7638) of a P-256 public point.</summary>
    private static string Thumbprint(ECPoint point) =>
        Base64Url.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{{\"crv\":\"P-256\",\"kty\":\"EC\",\"x\":\"{Base64Url.Encode(point.X!)}\",\"y\":\"{Base64Url.Encode(point.Y!)}\"}}")));

    /// <summary>The host (and a port other than the scheme's) of an address, lowercase; null when there is none.</summary>
    private static string? Host(string? address) =>
        Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.IdnHost.Length > 0
            ? (uri.IsDefaultPort ? uri.IdnHost : $"{uri.IdnHost}:{uri.Port}").ToLowerInvariant()
            : null;

    /// <summary>Each entry of the setting as a P-256 public point, or null where an entry can't be read as one.</summary>
    private static IReadOnlyList<ECPoint?> ReadKeys(string? setting)
    {
        var entries = (setting ?? string.Empty).Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var keys = new List<ECPoint?>();
        foreach (var entry in entries)
        {
            try
            {
                using var key = ECDsa.Create();
                key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(entry), out var read);
                var parameters = key.ExportParameters(includePrivateParameters: false);
                keys.Add(read == Convert.FromBase64String(entry).Length && parameters.Curve.Oid.Value == ECCurve.NamedCurves.nistP256.Oid.Value
                    ? parameters.Q
                    : null);
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                keys.Add(null);
            }
        }

        return keys;
    }

    private static ECDsa LoadOrCreateKey()
    {
        var configuredKey = Environment.GetEnvironmentVariable("BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64");
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            return ImportPrivateKey(Convert.FromBase64String(configuredKey));
        }

        var keyPath = GetLocalKeyPath();
        if (File.Exists(keyPath))
        {
            return ImportPrivateKey(Convert.FromBase64String(File.ReadAllText(keyPath)));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(keyPath, Convert.ToBase64String(key.ExportPkcs8PrivateKey()));
        return key;
    }

    private static ECDsa ImportPrivateKey(byte[] pkcs8PrivateKey)
    {
        var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(pkcs8PrivateKey, out _);
        return key;
    }

    private static string GetLocalKeyPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = AppContext.BaseDirectory;
        }

        return Path.Combine(localAppData, "BatteryPassDemo", "signing-key-p256.pkcs8");
    }
}
