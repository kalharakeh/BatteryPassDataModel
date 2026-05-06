using System.Security.Cryptography;
using System.Text;
using BatteryPassWeb.Configuration;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Services;

public sealed class ExternalApiSecurityService
{
    private static readonly char[] TokenAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
    private readonly byte[] _encryptionKey;

    public ExternalApiSecurityService(IOptions<BatteryPassOptions> options)
        : this(options.Value.ExternalApiEncryptionKey)
    {
    }

    public ExternalApiSecurityService(string encryptionKey)
    {
        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            throw new InvalidOperationException("External API encryption key is missing.");
        }

        _encryptionKey = NormalizeKey(encryptionKey);
    }

    public string CreateToken(int length = 32)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than zero.");
        }

        Span<byte> random = stackalloc byte[length];
        RandomNumberGenerator.Fill(random);

        var chars = new char[length];
        for (var i = 0; i < random.Length; i++)
        {
            chars[i] = TokenAlphabet[random[i] % TokenAlphabet.Length];
        }

        return new string(chars);
    }

    public string HashSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret is required.", nameof(secret));
        }

        Span<byte> salt = stackalloc byte[16];
        RandomNumberGenerator.Fill(salt);

        var hashBytes = HashWithSalt(secret, salt);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hashBytes)}";
    }

    public bool VerifySecret(string secret, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!TryBase64(parts[0], out var salt) || !TryBase64(parts[1], out var expectedHash))
        {
            return false;
        }

        var computedHash = HashWithSalt(secret, salt);
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    public string Encrypt(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var plaintext = Encoding.UTF8.GetBytes(value);
        var nonce = new byte[12];
        var tag = new byte[16];
        var cipher = new byte[plaintext.Length];

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_encryptionKey, tag.Length);
        aes.Encrypt(nonce, plaintext, cipher, tag);

        var packed = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, packed, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, packed, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, packed, nonce.Length + tag.Length, cipher.Length);

        return Convert.ToBase64String(packed);
    }

    public string Decrypt(string encryptedValue)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue))
        {
            throw new ArgumentException("Encrypted value is required.", nameof(encryptedValue));
        }

        if (!TryBase64(encryptedValue, out var packed))
        {
            throw new InvalidOperationException("Encrypted value format is invalid.");
        }

        if (packed.Length < 12 + 16)
        {
            throw new InvalidOperationException("Encrypted value is too short.");
        }

        var nonce = packed[..12];
        var tag = packed[12..28];
        var cipher = packed[28..];
        var plaintext = new byte[cipher.Length];

        using var aes = new AesGcm(_encryptionKey, tag.Length);
        aes.Decrypt(nonce, cipher, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] NormalizeKey(string rawKey)
    {
        if (TryBase64(rawKey, out var decodedKey) && decodedKey.Length == 32)
        {
            return decodedKey;
        }

        var utf8 = Encoding.UTF8.GetBytes(rawKey.Trim());
        if (utf8.Length == 32)
        {
            return utf8;
        }

        return SHA256.HashData(utf8);
    }

    private static byte[] HashWithSalt(string secret, ReadOnlySpan<byte> salt)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var combined = new byte[salt.Length + secretBytes.Length];
        salt.CopyTo(combined);
        Buffer.BlockCopy(secretBytes, 0, combined, salt.Length, secretBytes.Length);
        return SHA256.HashData(combined);
    }

    private static bool TryBase64(string value, out byte[] data)
    {
        try
        {
            data = Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            data = [];
            return false;
        }
    }
}
