using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Services;
using MongoDB.Bson.Serialization.Attributes;

namespace BatteryPassWeb.Dpp.Signing;

/// <summary>
/// The signature of one passport version's static core, with the public key that made it, so a version stays
/// verifiable after the signing key changes (same proof shape as today's trust services).
/// </summary>
public sealed class SignatureProof
{
    public required string Type { get; init; }

    public required string Cryptosuite { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime CreatedUtc { get; init; }

    public required string Issuer { get; init; }

    public required string VerificationMethod { get; init; }

    /// <summary>"sha256:" and the hex SHA-256 of the canonical static core.</summary>
    public required string Hash { get; init; }

    /// <summary>The ECDSA P-256 signature (IEEE P1363), base64url.</summary>
    public required string ProofValue { get; init; }

    /// <summary>The signing key's public point, base64url.</summary>
    public required string PublicKeyX { get; init; }

    public required string PublicKeyY { get; init; }
}

/// <summary>Signs and verifies a version's static core (design 4.3) with the app's existing ECDSA key.</summary>
public sealed class PassportSigner(DemoSigningKeyService keys)
{
    public const string ProofType = "DataIntegrityProof";
    public const string Cryptosuite = "ecdsa-p256-sha256-dpp-canonical-2026";

    public SignatureProof Sign(JsonNode staticCore, DateTime createdUtc)
    {
        ArgumentNullException.ThrowIfNull(staticCore);
        var canonical = CanonicalJson.Serialize(staticCore);
        var signature = keys.SignData(Encoding.UTF8.GetBytes(canonical));
        var publicKey = keys.ExportPublicParameters();
        return new SignatureProof
        {
            Type = ProofType,
            Cryptosuite = Cryptosuite,
            CreatedUtc = createdUtc,
            Issuer = keys.Issuer,
            VerificationMethod = keys.VerificationMethod,
            Hash = CanonicalJson.Hash(staticCore),
            ProofValue = Base64Url.Encode(signature),
            PublicKeyX = Base64Url.Encode(publicKey.Q.X ?? throw new CryptographicException("The public key has no X coordinate.")),
            PublicKeyY = Base64Url.Encode(publicKey.Q.Y ?? throw new CryptographicException("The public key has no Y coordinate.")),
        };
    }

    /// <summary>
    /// True when the core is exactly what was signed. Uses the public key stored in the proof, not today's key.
    /// Whether that key belongs to the issuer is the trust pages' question, not this check's.
    /// </summary>
    public static bool Verify(JsonNode staticCore, SignatureProof proof)
    {
        ArgumentNullException.ThrowIfNull(staticCore);
        ArgumentNullException.ThrowIfNull(proof);
        try
        {
            if (!string.Equals(proof.Hash, CanonicalJson.Hash(staticCore), StringComparison.Ordinal))
            {
                return false;
            }

            using var key = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = Base64Url.Decode(proof.PublicKeyX), Y = Base64Url.Decode(proof.PublicKeyY) },
            });
            return key.VerifyData(
                Encoding.UTF8.GetBytes(CanonicalJson.Serialize(staticCore)),
                Base64Url.Decode(proof.ProofValue),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}
