using System.Security.Cryptography;

namespace BatteryPassWeb.Services;

public sealed class DemoSigningKeyService : IDisposable
{
    public const string DefaultIssuer = "did:web:local.battery.pass:issuer";
    public const string DefaultKeyId = "acme-p256-1";

    private readonly ECDsa _key;
    private readonly bool _ownsKey;

    public DemoSigningKeyService()
        : this(LoadOrCreateKey(), ownsKey: true)
    {
    }

    public DemoSigningKeyService(ECDsa key)
        : this(key, ownsKey: false)
    {
    }

    private DemoSigningKeyService(ECDsa key, bool ownsKey)
    {
        _key = key;
        _ownsKey = ownsKey;
    }

    public string Issuer => Environment.GetEnvironmentVariable("BATTERY_PASS_SIGNING_ISSUER") ?? DefaultIssuer;

    public string VerificationMethod =>
        Environment.GetEnvironmentVariable("BATTERY_PASS_SIGNING_VERIFICATION_METHOD") ?? $"{Issuer}#{DefaultKeyId}";

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
