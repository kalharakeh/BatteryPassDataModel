using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
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

    /// <summary>
    /// The proof over the version's public part (finding R07), made at publication with the same key, which the public
    /// data carries so anyone can check it, but only while it still holds for the data being served (checked on every
    /// answer). Null for a version published before this existed.
    /// </summary>
    public SignatureProof? PublicView { get; init; }
}

/// <summary>
/// What a signature check found (finding R23): "Verified" only when the data matches the signature and the key that made
/// it is one the app trusts; otherwise a plain reason.
/// </summary>
public sealed record SignatureCheck(bool Holds, string? Problem)
{
    public const string NoMatch = "the data doesn't match its signature";
    public const string KeyNotTrusted = "signed with a key this site doesn't know";

    public const string NotChecked = "the signature couldn't be checked";

    /// <summary>A stored proof with a missing or unreadable part.</summary>
    public const string Unreadable = "the signature can't be read";

    /// <summary>A version stored with no proof at all (C3 round three, point 4).</summary>
    public const string NotSigned = "not signed";

    public static SignatureCheck Verified { get; } = new(true, null);

    /// <summary>For a page built without the check: never "Verified" by default.</summary>
    public static SignatureCheck Unchecked { get; } = new(false, NotChecked);

    /// <summary>The signature line in words: "Verified", or "Not verified: " and the reason.</summary>
    public string Words => Holds ? "Verified" : $"Not verified: {Problem}";

    /// <summary>The reason as a sentence of its own ("The data doesn't match its signature."); null when it holds.</summary>
    public string? Reason => Problem is null ? null : $"{char.ToUpperInvariant(Problem[0])}{Problem[1..]}.";

    /// <summary>For the team only: what to do when the key is one the site doesn't know; null otherwise.</summary>
    public string? TeamAdvice => Problem == KeyNotTrusted
        ? $"If it was signed with this site's earlier key, list that public key in {DemoSigningKeyService.TrustedKeysSetting}; the runbook says how."
        : null;
}

/// <summary>Signs and verifies a version's static core (design 4.3) with the app's existing ECDSA key.</summary>
public sealed class PassportSigner(DemoSigningKeyService keys)
{
    /// <summary>
    /// The full check behind "Verified": the core is exactly what was signed, and the proof's key is one the app trusts
    /// (its current key or an older one listed as trusted). Uses only what the app holds; it never calls out.
    /// </summary>
    public SignatureCheck Check(JsonNode staticCore, SignatureProof? proof)
    {
        if (proof is null)
        {
            return new SignatureCheck(false, SignatureCheck.NotSigned);
        }

        if (!Readable(proof))
        {
            return new SignatureCheck(false, SignatureCheck.Unreadable);
        }

        if (!Verify(staticCore, proof))
        {
            return new SignatureCheck(false, SignatureCheck.NoMatch);
        }

        return keys.Trusts(Base64Url.Decode(proof.PublicKeyX), Base64Url.Decode(proof.PublicKeyY))
            ? SignatureCheck.Verified
            : new SignatureCheck(false, SignatureCheck.KeyNotTrusted);
    }

    /// <summary>
    /// True when a readable proof names the site's issuer and the key ID did.json lists for its key, so an outsider who
    /// looks the key up by that ID finds it (C3 round three, point 2). Issuer and key ID aren't in the signed text.
    /// </summary>
    public bool Listed(SignatureProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        return Readable(proof)
            && string.Equals(proof.Issuer, keys.Issuer, StringComparison.Ordinal)
            && keys.Lists(proof.VerificationMethod, Base64Url.Decode(proof.PublicKeyX), Base64Url.Decode(proof.PublicKeyY));
    }

    public const string ProofType = "DataIntegrityProof";
    public const string Cryptosuite = "ecdsa-p256-sha256-dpp-canonical-2026";

    /// <summary>Signs a version's static core and, separately, its public part (<see cref="SignatureProof.PublicView"/>).</summary>
    public SignatureProof Sign(JsonNode staticCore, DateTime createdUtc)
    {
        ArgumentNullException.ThrowIfNull(staticCore);
        var publicView = staticCore is JsonObject core ? SignPart(PublicPassport.SignedPart(PublicPassport.Cut(core)), createdUtc, null) : null;
        return SignPart(staticCore, createdUtc, publicView);
    }

    private SignatureProof SignPart(JsonNode staticCore, DateTime createdUtc, SignatureProof? publicView)
    {
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
            PublicView = publicView,
        };
    }

    /// <summary>
    /// True when the core is exactly what was signed, with the public key stored in the proof. This alone is not
    /// "Verified": <see cref="Check"/> also asks whether the app trusts that key.
    /// </summary>
    public static bool Verify(JsonNode staticCore, SignatureProof proof)
    {
        ArgumentNullException.ThrowIfNull(staticCore);
        ArgumentNullException.ThrowIfNull(proof);
        if (!Readable(proof))
        {
            return false;
        }

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

    /// <summary>
    /// True when every part of a stored proof is there and has the right form: a "sha256:" hash, a 64-byte signature and
    /// a P-256 point of two 32-byte coordinates, all base64url. A proof read from the database may lack any of them.
    /// </summary>
    public static bool Readable(SignatureProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        return proof.Hash is { } hash && hash.StartsWith("sha256:", StringComparison.Ordinal)
            && Length(proof.ProofValue) == 64
            && Length(proof.PublicKeyX) == 32
            && Length(proof.PublicKeyY) == 32;

        static int Length(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            try
            {
                return Base64Url.Decode(text).Length;
            }
            catch (FormatException)
            {
                return -1;
            }
        }
    }
}
