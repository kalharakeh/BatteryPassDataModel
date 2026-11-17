using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace BatteryPassWeb.Services;

/// <summary>
/// Encrypts the sign-in keys before they go into the database (PL round two, point 1): AES-GCM with the secret made from
/// the passport signing key (<see cref="DemoSigningKeyService.DataProtectionSecret"/>), which is not in the database. Someone
/// who can only read the database gets the keys in a form they can't use to make a sign-in cookie.
/// </summary>
public sealed class KeyRingCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public KeyRingCipher(byte[] secret)
    {
        if (secret.Length != 32)
        {
            throw new ArgumentException("The sign-in keys' secret is 32 bytes.", nameof(secret));
        }

        _key = secret.ToArray();
        Id = DataProtectionSetup.CipherId(secret);
    }

    /// <summary>A short name of the secret (not the secret): which signing key a stored sign-in key was encrypted for.</summary>
    public string Id { get; }

    public string Encrypt(string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var sealedBytes = new byte[NonceSize + plain.Length + TagSize];
        var nonce = sealedBytes.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, sealedBytes.AsSpan(NonceSize, plain.Length), sealedBytes.AsSpan(NonceSize + plain.Length), AssociatedData());
        CryptographicOperations.ZeroMemory(plain);
        return Convert.ToBase64String(sealedBytes);
    }

    public string Decrypt(string sealedText)
    {
        var sealedBytes = Convert.FromBase64String(sealedText);
        if (sealedBytes.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("A stored sign-in key is damaged.");
        }

        var length = sealedBytes.Length - NonceSize - TagSize;
        var plain = new byte[length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(sealedBytes.AsSpan(0, NonceSize), sealedBytes.AsSpan(NonceSize, length), sealedBytes.AsSpan(NonceSize + length), plain, AssociatedData());
        try
        {
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private byte[] AssociatedData() => Encoding.UTF8.GetBytes($"BatteryPass sign-in key v1 {Id}");
}

/// <summary>Wraps a sign-in key's secret part before it is stored; the framework calls it for every new key.</summary>
public sealed class SigningKeyXmlEncryptor(KeyRingCipher cipher) : IXmlEncryptor
{
    public const string ElementName = "signingKeyEncrypted";

    public EncryptedXmlInfo Encrypt(XElement plaintextElement) =>
        new(
            new XElement(
                ElementName,
                new XAttribute("version", "1"),
                new XAttribute("cipher", cipher.Id),
                new XElement("data", cipher.Encrypt(plaintextElement.ToString(SaveOptions.DisableFormatting)))),
            typeof(SigningKeyXmlDecryptor));
}

/// <summary>
/// Unwraps a stored sign-in key. The framework makes it from the type name written in the stored key, through its own
/// activator with the app's services: keep the name and the constructor as they are, or stored keys can't be read.
/// </summary>
public sealed class SigningKeyXmlDecryptor : IXmlDecryptor
{
    private readonly KeyRingCipher? _cipher;

    public SigningKeyXmlDecryptor(IServiceProvider services)
    {
        _cipher = services.GetService(typeof(KeyRingCipher)) as KeyRingCipher;
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        if (_cipher is null)
        {
            throw new CryptographicException("This sign-in key is encrypted, and the app has no signing key to read it.");
        }

        if (encryptedElement.Name.LocalName != SigningKeyXmlEncryptor.ElementName
            || (string?)encryptedElement.Attribute("version") != "1"
            || (string?)encryptedElement.Attribute("cipher") != _cipher.Id)
        {
            throw new CryptographicException("This sign-in key was encrypted for another signing key.");
        }

        return XElement.Parse(_cipher.Decrypt((string?)encryptedElement.Element("data") ?? string.Empty));
    }
}
