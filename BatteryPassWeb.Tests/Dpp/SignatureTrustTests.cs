using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R23 (the owner's decision, 10 Nov 2026): the issuer is the passport's own web address, the app publishes its
/// key there, and "Verified" means the data matches the signature and the key is one the app trusts.
/// </summary>
public sealed class SignatureTrustTests
{
    private static readonly DateTime Created = new(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("https://www.sib-scania-eubp.eu", "did:web:www.sib-scania-eubp.eu")]
    [InlineData("https://www.sib-scania-eubp.eu/", "did:web:www.sib-scania-eubp.eu")]
    [InlineData("https://WWW.Example.TEST/ids", "did:web:www.example.test")]
    [InlineData("http://localhost:5423", "did:web:localhost%3A5423")]
    public void Issuer_ShouldBeTheBatteryIdsSite(string idBaseUrl, string issuer)
    {
        using var keys = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), idBaseUrl);

        Assert.Equal(issuer, keys.Issuer);
        Assert.Equal($"{issuer}#{keys.KeyId}", keys.VerificationMethod);
    }

    [Fact]
    public void Issuer_ShouldNeverBeThePlaceholder()
    {
        using var keys = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256));

        Assert.DoesNotContain("local.battery.pass", keys.Issuer, StringComparison.Ordinal);
        Assert.DoesNotContain("acme-p256-1", keys.VerificationMethod, StringComparison.Ordinal);
        Assert.StartsWith("did:web:", keys.Issuer, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyId_ShouldBeMadeFromTheKey_SoANewKeyGetsANewId()
    {
        using var first = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), "https://www.example.test");
        using var second = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), "https://www.example.test");
        var point = first.ExportPublicParameters().Q;

        // The JWK thumbprint (RFC 7638): SHA-256 of the key's required members in order, base64url.
        var thumbprint = Base64Url.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{{\"crv\":\"P-256\",\"kty\":\"EC\",\"x\":\"{Base64Url.Encode(point.X!)}\",\"y\":\"{Base64Url.Encode(point.Y!)}\"}}")));

        Assert.Equal(thumbprint, first.KeyId);
        Assert.NotEqual(first.KeyId, second.KeyId);
    }

    [Fact]
    public void ANewProof_ShouldNameTheIssuerAndKeyFromTheBatteryIdsSite()
    {
        using var keys = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), "https://www.sib-scania-eubp.eu");

        var proof = new PassportSigner(keys).Sign(SeedCore(), Created);

        Assert.Equal("did:web:www.sib-scania-eubp.eu", proof.Issuer);
        Assert.Equal($"did:web:www.sib-scania-eubp.eu#{keys.KeyId}", proof.VerificationMethod);
    }

    [Fact]
    public void Check_ShouldHold_WhenTheDataMatchesAndTheKeyIsTheAppsOwn()
    {
        var core = SeedCore();
        var signer = new PassportSigner(SigningTesting.Keys);

        var check = signer.Check(core, signer.Sign(core, Created));

        Assert.True(check.Holds);
        Assert.Null(check.Problem);
        Assert.Equal("Verified", check.Words);
    }

    [Fact]
    public void Check_ShouldNotHold_WhenTheDataChanged()
    {
        var core = SeedCore();
        var signer = new PassportSigner(SigningTesting.Keys);
        var proof = signer.Sign(core, Created);
        core["batteryPassportData"]!["identifiersAndProductData"]!["batterySerialNumber"] = "VPC8C05-6789";

        var check = signer.Check(core, proof);

        Assert.False(check.Holds);
        Assert.Equal(SignatureCheck.DataChanged, check.Problem);
    }

    [Fact]
    public void Check_ShouldNotHold_WhenTheProofWasMadeWithAKeyTheAppDoesNotTrust()
    {
        var core = SeedCore();
        using var stranger = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), "https://www.example.test");
        var foreign = new PassportSigner(stranger).Sign(core, Created); // a sound signature, made with someone else's key

        var check = new PassportSigner(SigningTesting.Keys).Check(core, foreign);

        Assert.True(PassportSigner.Verify(core, foreign)); // the data matches its own key...
        Assert.False(check.Holds);                         // ...but the key isn't the app's
        Assert.Equal(SignatureCheck.KeyNotTrusted, check.Problem);
        Assert.Equal($"Not verified: {SignatureCheck.KeyNotTrusted}", check.Words);
    }

    [Fact]
    public void Check_ShouldNotHold_WhenTheStoredProofIsGivenAnotherKey()
    {
        var core = SeedCore();
        var signer = new PassportSigner(SigningTesting.Keys);
        var proof = signer.Sign(core, Created);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var resigned = Resign(proof, core, other);

        Assert.False(signer.Check(core, resigned).Holds);
    }

    [Fact]
    public void Check_ShouldHold_ForAnOlderKeyListedAsTrusted_EvenWithThePlaceholderIssuer()
    {
        var core = SeedCore();
        using var oldKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var oldProof = Placeholder(Resign(new PassportSigner(SigningTesting.Keys).Sign(core, Created), core, oldKey));
        using var today = new DemoSigningKeyService(
            ECDsa.Create(ECCurve.NamedCurves.nistP256),
            "https://www.example.test",
            Convert.ToBase64String(oldKey.ExportSubjectPublicKeyInfo()));
        using var withoutTheList = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), "https://www.example.test");

        Assert.True(new PassportSigner(today).Check(core, oldProof).Holds);
        Assert.False(new PassportSigner(withoutTheList).Check(core, oldProof).Holds);
        Assert.Equal("did:web:local.battery.pass:issuer", oldProof.Issuer); // a signature keeps the name it was made with
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void TrustedKeysSetting_ShouldBeOptional(string? setting)
    {
        Assert.Null(DemoSigningKeyService.UnreadableTrustedKeys(setting));
        using var keys = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), "https://www.example.test", setting);
        Assert.Single(keys.TrustedKeys);
    }

    [Fact]
    public void TrustedKeysSetting_ShouldTakeSeveralKeys_AndSayWhichEntryCantBeRead()
    {
        using var a = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var b = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var good = $"{Convert.ToBase64String(a.ExportSubjectPublicKeyInfo())}, {Convert.ToBase64String(b.ExportSubjectPublicKeyInfo())}";

        Assert.Null(DemoSigningKeyService.UnreadableTrustedKeys(good));
        using var keys = new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256), "https://www.example.test", good);
        Assert.Equal(3, keys.TrustedKeys.Count);

        var message = DemoSigningKeyService.UnreadableTrustedKeys($"{Convert.ToBase64String(a.ExportSubjectPublicKeyInfo())}, not-a-key");
        Assert.NotNull(message);
        Assert.Contains(DemoSigningKeyService.TrustedKeysSetting, message, StringComparison.Ordinal);
        Assert.Contains("2", message, StringComparison.Ordinal);
        Assert.DoesNotContain("not-a-key", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TrustedKeysSetting_ShouldRefuseAPrivateKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.NotNull(DemoSigningKeyService.UnreadableTrustedKeys(Convert.ToBase64String(key.ExportPkcs8PrivateKey())));
    }

    [Fact]
    public void DidDocument_ShouldPublishTheKeys_UnderTheIssuersName()
    {
        using var oldKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var keys = new DemoSigningKeyService(
            ECDsa.Create(ECCurve.NamedCurves.nistP256),
            "https://www.sib-scania-eubp.eu",
            Convert.ToBase64String(oldKey.ExportSubjectPublicKeyInfo()));

        var document = keys.DidDocument();

        Assert.Equal("did:web:www.sib-scania-eubp.eu", document["id"]!.GetValue<string>());
        var methods = document["verificationMethod"]!.AsArray();
        Assert.Equal(2, methods.Count);
        var current = methods[0]!;
        Assert.Equal(keys.VerificationMethod, current["id"]!.GetValue<string>());
        Assert.Equal("JsonWebKey2020", current["type"]!.GetValue<string>());
        Assert.Equal(keys.Issuer, current["controller"]!.GetValue<string>());
        var point = keys.ExportPublicParameters().Q;
        Assert.Equal(Base64Url.Encode(point.X!), current["publicKeyJwk"]!["x"]!.GetValue<string>());
        Assert.Equal(Base64Url.Encode(point.Y!), current["publicKeyJwk"]!["y"]!.GetValue<string>());
        Assert.Null(current["publicKeyJwk"]!["d"]); // never the private part
        Assert.Equal(
            methods.Select(method => method!["id"]!.GetValue<string>()),
            document["assertionMethod"]!.AsArray().Select(id => id!.GetValue<string>()));
    }

    [Fact]
    public void Verification_ShouldNeverCallOutToTheInternet()
    {
        // The check uses only the proof's own key and the keys the app holds: nothing in signing can make a request.
        var root = Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Dpp", "Signing", "PassportSigner.cs"))!;
        var sources = Directory.GetFiles(root, "*.cs").Append(DppTestFiles.RepoFile("web", "Services", "DemoSigningKeyService.cs"));
        foreach (var source in sources)
        {
            var text = File.ReadAllText(source);
            Assert.DoesNotContain("HttpClient", text, StringComparison.Ordinal);
            Assert.DoesNotContain("WebRequest", text, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Net", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheAppsStart_ShouldNameTheIssuerFromTheBatteryIdsSite_AndCheckTheTrustedKeys()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs")).Replace("\r\n", "\n");

        Assert.Contains("builder.Services.AddSingleton(_ => new DemoSigningKeyService(idBaseUrlSetting, appBaseUrlSetting, trustedSigningKeysSetting));", program, StringComparison.Ordinal);
        Assert.Contains(
            "if (DemoSigningKeyService.UnreadableTrustedKeys(trustedSigningKeysSetting) is { } unreadableTrustedKeys && builder.Environment.IsProduction())\n{\n    throw new InvalidOperationException(unreadableTrustedKeys);\n}",
            program,
            StringComparison.Ordinal);
        Assert.Contains("app.Logger.LogWarning(trustedKeysWarning);", program, StringComparison.Ordinal);
        Assert.DoesNotContain("BATTERY_PASS_SIGNING_ISSUER", File.ReadAllText(DppTestFiles.RepoFile("web", "Services", "DemoSigningKeyService.cs")), StringComparison.Ordinal);
    }

    private static SignatureProof Resign(SignatureProof proof, JsonNode core, ECDsa key)
    {
        var point = key.ExportParameters(includePrivateParameters: false).Q;
        var signature = key.SignData(Encoding.UTF8.GetBytes(CanonicalJson.Serialize(core)), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new SignatureProof
        {
            Type = proof.Type,
            Cryptosuite = proof.Cryptosuite,
            CreatedUtc = proof.CreatedUtc,
            Issuer = proof.Issuer,
            VerificationMethod = proof.VerificationMethod,
            Hash = proof.Hash,
            ProofValue = Base64Url.Encode(signature),
            PublicKeyX = Base64Url.Encode(point.X!),
            PublicKeyY = Base64Url.Encode(point.Y!),
        };
    }

    private static SignatureProof Placeholder(SignatureProof proof) => new()
    {
        Type = proof.Type,
        Cryptosuite = proof.Cryptosuite,
        CreatedUtc = proof.CreatedUtc,
        Issuer = "did:web:local.battery.pass:issuer",
        VerificationMethod = "did:web:local.battery.pass:issuer#acme-p256-1",
        Hash = proof.Hash,
        ProofValue = proof.ProofValue,
        PublicKeyX = proof.PublicKeyX,
        PublicKeyY = proof.PublicKeyY,
    };

    private static JsonObject SeedCore()
    {
        var seed = DppTestSeed.Load();
        return PassportStaticCore.Extract(PassportAssembler.Assemble(seed.Sources, seed.Header));
    }
}
