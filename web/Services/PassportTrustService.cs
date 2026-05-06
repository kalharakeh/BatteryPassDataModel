using System.Security.Cryptography;
using System.Text;
using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class PassportTrustService
{
    public const string ProofType = "DataIntegrityProof";
    public const string Cryptosuite = "ecdsa-p256-sha256-jcs-2026";

    private readonly CanonicalPassportSnapshotService _snapshotService;
    private readonly DemoSigningKeyService _signingKeyService;

    public PassportTrustService(
        CanonicalPassportSnapshotService snapshotService,
        DemoSigningKeyService signingKeyService)
    {
        _snapshotService = snapshotService;
        _signingKeyService = signingKeyService;
    }

    public PassportSignatureResult Sign(BsonDocument passport, string actor)
    {
        var snapshot = _snapshotService.BuildSnapshot(passport);
        var canonicalJson = _snapshotService.Canonicalize(snapshot);
        var hash = _snapshotService.Sha256Hex(canonicalJson);
        var signedAt = DateTimeOffset.UtcNow.ToString("O");
        var signature = _signingKeyService.SignData(Encoding.UTF8.GetBytes(canonicalJson));

        var proof = new BsonDocument
        {
            ["type"] = ProofType,
            ["cryptosuite"] = Cryptosuite,
            ["created"] = signedAt,
            ["verificationMethod"] = _signingKeyService.VerificationMethod,
            ["proofPurpose"] = "assertionMethod",
            ["proofValue"] = Base64Url.Encode(signature),
            ["hash"] = $"sha256:{hash}",
            ["issuer"] = _signingKeyService.Issuer,
            ["publicKeyJwk"] = BuildPublicJwk(_signingKeyService.ExportPublicParameters())
        };

        if (!string.IsNullOrWhiteSpace(actor))
        {
            proof["actor"] = actor;
        }

        return new PassportSignatureResult
        {
            Snapshot = snapshot,
            CanonicalJson = canonicalJson,
            Hash = hash,
            Proof = proof,
            SignedAt = signedAt
        };
    }

    public PassportVerificationResult Verify(BsonDocument passport)
    {
        var proof = BsonHelpers.GetValue(passport, "trust", "latestProof") as BsonDocument;
        if (proof == null || proof.ElementCount == 0)
        {
            return new PassportVerificationResult
            {
                IsValid = false,
                State = TrustState.Unvalidated,
                Message = "No signature proof is available for this passport."
            };
        }

        var expectedHash = BsonHelpers.GetString(passport, "trust", "latestHash");
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            expectedHash = BsonHelpers.GetString(proof, "hash");
        }

        return Verify(passport, expectedHash, proof);
    }

    public PassportVerificationResult Verify(BsonDocument passport, string expectedHash, BsonDocument proof)
    {
        var snapshot = _snapshotService.BuildSnapshot(passport);
        var canonicalJson = _snapshotService.Canonicalize(snapshot);
        var currentHash = _snapshotService.Sha256Hex(canonicalJson);
        var normalizedExpectedHash = NormalizeHash(expectedHash);
        var issuer = BsonHelpers.GetString(proof, "issuer");
        var verificationMethod = BsonHelpers.GetString(proof, "verificationMethod");
        var signedAt = BsonHelpers.GetString(proof, "created");

        if (string.IsNullOrWhiteSpace(normalizedExpectedHash))
        {
            return Invalid(
                "Signature verification requires an expected hash.",
                currentHash,
                normalizedExpectedHash,
                issuer,
                verificationMethod,
                signedAt);
        }

        var proofHash = NormalizeHash(BsonHelpers.GetString(proof, "hash"));
        if (!string.IsNullOrWhiteSpace(proofHash)
            && !string.Equals(proofHash, normalizedExpectedHash, StringComparison.Ordinal))
        {
            return Invalid(
                "Signature proof hash does not match the expected hash.",
                currentHash,
                normalizedExpectedHash,
                issuer,
                verificationMethod,
                signedAt);
        }

        if (!string.Equals(currentHash, normalizedExpectedHash, StringComparison.Ordinal))
        {
            return Invalid(
                "Signature hash mismatch: the current canonical passport core no longer matches the signed snapshot.",
                currentHash,
                normalizedExpectedHash,
                issuer,
                verificationMethod,
                signedAt);
        }

        if (!string.Equals(BsonHelpers.GetString(proof, "type"), ProofType, StringComparison.Ordinal)
            || !string.Equals(BsonHelpers.GetString(proof, "cryptosuite"), Cryptosuite, StringComparison.Ordinal))
        {
            return Invalid(
                "Signature proof metadata is not supported by this verifier.",
                currentHash,
                normalizedExpectedHash,
                issuer,
                verificationMethod,
                signedAt);
        }

        try
        {
            var proofValue = BsonHelpers.GetString(proof, "proofValue");
            var publicKey = BsonHelpers.GetValue(proof, "publicKeyJwk") as BsonDocument;
            if (string.IsNullOrWhiteSpace(proofValue) || publicKey == null)
            {
                return Invalid(
                    "Signature proof is missing its proof value or public key.",
                    currentHash,
                    normalizedExpectedHash,
                    issuer,
                    verificationMethod,
                    signedAt);
            }

            using var key = ECDsa.Create(ReadPublicParameters(publicKey));
            var signature = Base64Url.Decode(proofValue);
            var isValid = key.VerifyData(
                Encoding.UTF8.GetBytes(canonicalJson),
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            return isValid
                ? new PassportVerificationResult
                {
                    IsValid = true,
                    State = TrustState.Signed,
                    Message = "Signature verified against the current canonical passport core.",
                    CurrentHash = currentHash,
                    ExpectedHash = normalizedExpectedHash,
                    Issuer = issuer,
                    VerificationMethod = verificationMethod,
                    SignedAt = signedAt
                }
                : Invalid(
                    "Invalid signature: proof value could not be verified for the current canonical passport core.",
                    currentHash,
                    normalizedExpectedHash,
                    issuer,
                    verificationMethod,
                    signedAt);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return Invalid(
                "Invalid signature proof: proof value or public key could not be decoded.",
                currentHash,
                normalizedExpectedHash,
                issuer,
                verificationMethod,
                signedAt);
        }
    }

    private static PassportVerificationResult Invalid(
        string message,
        string currentHash,
        string expectedHash,
        string issuer,
        string verificationMethod,
        string signedAt)
    {
        return new PassportVerificationResult
        {
            IsValid = false,
            State = TrustState.SignatureInvalid,
            Message = message,
            CurrentHash = currentHash,
            ExpectedHash = expectedHash,
            Issuer = issuer,
            VerificationMethod = verificationMethod,
            SignedAt = signedAt
        };
    }

    private static BsonDocument BuildPublicJwk(ECParameters publicParameters)
    {
        return new BsonDocument
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64Url.Encode(publicParameters.Q.X ?? throw new CryptographicException("Public key is missing X coordinate.")),
            ["y"] = Base64Url.Encode(publicParameters.Q.Y ?? throw new CryptographicException("Public key is missing Y coordinate."))
        };
    }

    private static ECParameters ReadPublicParameters(BsonDocument jwk)
    {
        var curve = BsonHelpers.GetString(jwk, "crv");
        if (!string.Equals(curve, "P-256", StringComparison.Ordinal))
        {
            throw new CryptographicException("Only P-256 public keys are supported.");
        }

        return new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Base64Url.Decode(BsonHelpers.GetString(jwk, "x")),
                Y = Base64Url.Decode(BsonHelpers.GetString(jwk, "y"))
            }
        };
    }

    private static string NormalizeHash(string hash)
    {
        const string prefix = "sha256:";
        return hash.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? hash[prefix.Length..]
            : hash;
    }
}
