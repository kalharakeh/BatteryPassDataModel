using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class PassportSignerTests
{
    private static readonly DateTime Created = new(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void SignedCore_ShouldVerify()
    {
        var core = SeedCore();

        var proof = new PassportSigner(SigningTesting.Keys).Sign(core, Created);

        Assert.True(PassportSigner.Verify(core, proof));
        Assert.Equal(PassportSigner.ProofType, proof.Type);
        Assert.Equal(PassportSigner.Cryptosuite, proof.Cryptosuite);
        Assert.Equal(CanonicalJson.Hash(core), proof.Hash);
        Assert.Equal(Created, proof.CreatedUtc);
    }

    [Fact]
    public void ChangedCore_ShouldNotVerify()
    {
        var core = SeedCore();
        var proof = new PassportSigner(SigningTesting.Keys).Sign(core, Created);

        core["batteryPassportData"]!["identifiersAndProductData"]!["batterySerialNumber"] = "VPC8C05-6789";

        Assert.False(PassportSigner.Verify(core, proof));
    }

    [Fact]
    public void Verification_ShouldUseTheKeyStoredInTheProof()
    {
        var core = SeedCore();
        var proof = new PassportSigner(SigningTesting.Keys).Sign(core, Created);
        using var otherKey = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        var otherPublic = otherKey.ExportPublicParameters();
        var swapped = new SignatureProof
        {
            Type = proof.Type,
            Cryptosuite = proof.Cryptosuite,
            CreatedUtc = proof.CreatedUtc,
            Issuer = proof.Issuer,
            VerificationMethod = proof.VerificationMethod,
            Hash = proof.Hash,
            ProofValue = proof.ProofValue,
            PublicKeyX = Base64Url.Encode(otherPublic.Q.X!),
            PublicKeyY = Base64Url.Encode(otherPublic.Q.Y!),
        };

        Assert.True(PassportSigner.Verify(core, proof)); // still true after the app's key changes: the proof carries its key
        Assert.False(PassportSigner.Verify(core, swapped));
    }

    [Fact]
    public void MalformedProof_ShouldNotVerifyOrThrow()
    {
        var core = SeedCore();
        var proof = new PassportSigner(SigningTesting.Keys).Sign(core, Created);
        var broken = new SignatureProof
        {
            Type = proof.Type,
            Cryptosuite = proof.Cryptosuite,
            CreatedUtc = proof.CreatedUtc,
            Issuer = proof.Issuer,
            VerificationMethod = proof.VerificationMethod,
            Hash = proof.Hash,
            ProofValue = "not base64url!",
            PublicKeyX = proof.PublicKeyX,
            PublicKeyY = proof.PublicKeyY,
        };

        Assert.False(PassportSigner.Verify(core, broken));
    }

    private static JsonObject SeedCore()
    {
        var seed = DppTestSeed.Load();
        return PassportStaticCore.Extract(PassportAssembler.Assemble(seed.Sources, seed.Header));
    }
}
