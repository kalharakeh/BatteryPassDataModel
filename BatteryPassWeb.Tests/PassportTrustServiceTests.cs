using System.Security.Cryptography;
using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportTrustServiceTests
{
    [Fact]
    public void Sign_ShouldCreateDataIntegrityProofWithHashAndPublicKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var service = BuildService(key);

        var result = service.Sign(BuildPassport(), "admin@example.test");

        Assert.False(string.IsNullOrWhiteSpace(result.Hash));
        Assert.False(string.IsNullOrWhiteSpace(result.CanonicalJson));
        Assert.False(string.IsNullOrWhiteSpace(result.SignedAt));
        Assert.True(result.Snapshot.Contains("passportId"));
        Assert.Equal("DataIntegrityProof", result.Proof["type"].AsString);
        Assert.Equal("ecdsa-p256-sha256-jcs-2026", result.Proof["cryptosuite"].AsString);
        Assert.Equal("assertionMethod", result.Proof["proofPurpose"].AsString);
        Assert.Equal($"sha256:{result.Hash}", result.Proof["hash"].AsString);
        Assert.False(string.IsNullOrWhiteSpace(result.Proof["verificationMethod"].AsString));
        Assert.False(string.IsNullOrWhiteSpace(result.Proof["proofValue"].AsString));
        Assert.Equal("EC", result.Proof["publicKeyJwk"]["kty"].AsString);
        Assert.Equal("P-256", result.Proof["publicKeyJwk"]["crv"].AsString);
        Assert.False(string.IsNullOrWhiteSpace(result.Proof["publicKeyJwk"]["x"].AsString));
        Assert.False(string.IsNullOrWhiteSpace(result.Proof["publicKeyJwk"]["y"].AsString));
    }

    [Fact]
    public void Verify_ShouldAcceptUntamperedSignedPassport()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var service = BuildService(key);
        var passport = BuildPassport();
        var signature = service.Sign(passport, "admin@example.test");

        var verification = service.Verify(passport, signature.Hash, signature.Proof);

        Assert.True(verification.IsValid, verification.Message);
        Assert.Equal(TrustState.Signed, verification.State);
        Assert.Equal(signature.Hash, verification.ExpectedHash);
        Assert.Equal(signature.Hash, verification.CurrentHash);
        Assert.Equal(signature.Proof["issuer"].AsString, verification.Issuer);
        Assert.Equal(signature.Proof["verificationMethod"].AsString, verification.VerificationMethod);
        Assert.Equal(signature.SignedAt, verification.SignedAt);
    }

    [Fact]
    public void Verify_ShouldRejectTamperedCanonicalPayload()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var service = BuildService(key);
        var passport = BuildPassport();
        var signature = service.Sign(passport, "admin@example.test");
        passport["aspects"]["generalProductInformation"]["payload"]["batteryMass"] = 99.9;

        var verification = service.Verify(passport, signature.Hash, signature.Proof);

        Assert.False(verification.IsValid);
        Assert.Equal(TrustState.SignatureInvalid, verification.State);
        Assert.NotEqual(signature.Hash, verification.CurrentHash);
        Assert.Contains("hash", verification.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verify_ShouldRejectMissingProof()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var service = BuildService(key);

        var verification = service.Verify(BuildPassport());

        Assert.False(verification.IsValid);
        Assert.Equal(TrustState.Unvalidated, verification.State);
        Assert.Contains("proof", verification.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PassportTrustService BuildService(ECDsa key)
    {
        return new PassportTrustService(
            new CanonicalPassportSnapshotService(new SchemaRegistryService()),
            new DemoSigningKeyService(key));
    }

    private static BsonDocument BuildPassport()
    {
        return new BsonDocument
        {
            ["passportId"] = "did:web:acme.battery.pass:test-001",
            ["registryInfo"] = new BsonDocument
            {
                ["registryId"] = "registry-001",
                ["status"] = "draft"
            },
            ["app"] = new BsonDocument
            {
                ["documents"] = new BsonDocument
                {
                    ["sustainabilityReport"] = new BsonDocument
                    {
                        ["url"] = "https://example.test/report.pdf",
                        ["fileId"] = "file-001",
                        ["sha256"] = "abc123",
                        ["visibility"] = "public"
                    }
                },
                ["operations"] = new BsonDocument
                {
                    ["latestTelemetry"] = new BsonDocument
                    {
                        ["currentVoltageV"] = 800
                    }
                }
            },
            ["aspects"] = new BsonDocument
            {
                ["generalProductInformation"] = new BsonDocument
                {
                    ["visibility"] = "public",
                    ["payload"] = new BsonDocument
                    {
                        ["batteryMass"] = 12.5,
                        ["batteryStatus"] = "Original"
                    }
                }
            }
        };
    }
}
