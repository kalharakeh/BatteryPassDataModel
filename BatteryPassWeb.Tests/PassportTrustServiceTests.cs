using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using System.Security.Cryptography;

namespace BatteryPassWeb.Tests;

public sealed class PassportTrustServiceTests
{
    [Fact]
    public void Sign_ShouldReturnDataIntegrityProofWithHashAndPublicKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var service = CreateService(key);

        var result = service.Sign(BuildPassport(), "admin@example.test");

        Assert.False(string.IsNullOrWhiteSpace(result.Hash));
        Assert.False(string.IsNullOrWhiteSpace(result.CanonicalJson));
        Assert.Equal("DataIntegrityProof", result.Proof["type"].AsString);
        Assert.Equal("ecdsa-p256-sha256-jcs-2026", result.Proof["cryptosuite"].AsString);
        Assert.Equal("assertionMethod", result.Proof["proofPurpose"].AsString);
        Assert.Equal("did:web:local.battery.pass:issuer", result.Proof["issuer"].AsString);
        Assert.Equal("did:web:local.battery.pass:issuer#acme-p256-key-1", result.Proof["verificationMethod"].AsString);
        Assert.False(string.IsNullOrWhiteSpace(result.Proof["proofValue"].AsString));
        Assert.Equal("EC", result.Proof["publicKeyJwk"]["kty"].AsString);
        Assert.Equal("P-256", result.Proof["publicKeyJwk"]["crv"].AsString);
    }

    [Fact]
    public void Verify_ShouldPassForSignedPassportSnapshot()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var service = CreateService(key);
        var passport = BuildPassport();
        var signature = service.Sign(passport, "admin@example.test");

        var verification = service.Verify(passport, signature.Hash, signature.Proof);

        Assert.True(verification.IsValid);
        Assert.Equal(TrustState.Signed, verification.State);
        Assert.Equal(signature.Hash, verification.ExpectedHash);
        Assert.Equal(signature.Hash, verification.CurrentHash);
    }

    [Fact]
    public void Verify_ShouldFailWhenSignedCoreIsTampered()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var service = CreateService(key);
        var passport = BuildPassport();
        var signature = service.Sign(passport, "admin@example.test");
        passport["aspects"]["generalProductInformation"]["payload"]["batteryMass"] = 99;

        var verification = service.Verify(passport, signature.Hash, signature.Proof);

        Assert.False(verification.IsValid);
        Assert.Equal(TrustState.SignatureInvalid, verification.State);
        Assert.NotEqual(signature.Hash, verification.CurrentHash);
        Assert.Contains("hash", verification.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PassportTrustService CreateService(ECDsa key)
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
                ["documents"] = new BsonDocument()
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
