using System.Security.Cryptography;
using System.Text;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R23: everywhere a signature shows, "Verified" needs the data to match and the key to be one the app trusts; a
/// passport whose stored proof is given another key shows as not verified, with the reason.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SignatureOnPagesTests(MongoFixture mongo)
{
    [Fact]
    public async Task APassportSignedWithAnotherKey_ShouldShowAsNotVerified_Everywhere()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await GiveTheProofAnotherKeyAsync(rig, version, stranger);
        var id = PassportPagesTests.Identifier(rig);
        var team = Reader.With("team@example.com", ReaderRole.Team);

        var detail = (await PassportPagesTests.Pages(rig).DetailAsync(team, id))!;
        var batteryRow = Assert.Single((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Versions);
        var clusterRow = Assert.Single((await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(BatteryPassWeb.Dpp.Editing.ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId))!.Versions);
        var api = (await new BatteryPassWeb.Dpp.Api.PassportApi(rig.Store, TestDemo.For(rig), SigningTesting.Passports(rig.Store), PassportPagesTests.Pages(rig), BatteryTesting.Pages(rig)).ReadAsync(team, id))!;

        Assert.False(detail.SignatureHolds);
        Assert.Equal($"Not verified: {SignatureCheck.KeyNotTrusted}", detail.Signature.Words);
        Assert.False(batteryRow.SignatureHolds);
        Assert.Equal("Signed with a key this site doesn't know.", batteryRow.Signature.Reason);
        Assert.False(clusterRow.SignatureHolds);
        Assert.False(api.SignatureVerified);
    }

    [Fact]
    public async Task APassportSignedWithAnOlderTrustedKey_ShouldVerify()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        using var oldKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await GiveTheProofAnotherKeyAsync(rig, version, oldKey);
        var reloaded = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        using var keysAfterAChange = new DemoSigningKeyService(
            ECDsa.Create(ECCurve.NamedCurves.nistP256),
            "https://www.example.test",
            Convert.ToBase64String(oldKey.ExportSubjectPublicKeyInfo()));

        Assert.True(new PassportReader(rig.Store, new PassportSigner(keysAfterAChange)).Check(reloaded).Holds);
        Assert.False(SigningTesting.Passports(rig.Store).Check(reloaded).Holds);
    }

    [Fact]
    public async Task ABatteryPageBuiltWithoutTheCheck_ShouldNeverSayVerified()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pages = new BatteryPages(
            rig.Store,
            PageTesting.Editor(rig),
            BatteryTesting.Creation(rig),
            rig.Publication,
            new BatteryPassWeb.Dpp.Documents.DocumentStore(rig.Context),
            DocumentTesting.Links,
            new ClusterRepository(rig.Context),
            new ApplicationAuditService(rig.Context),
            StatusChangeTests.Pages(rig));

        var row = Assert.Single((await pages.BatteryAsync(rig.PackId))!.Versions);

        Assert.False(row.SignatureHolds);
        Assert.Equal("The signature couldn't be checked.", row.Signature.Reason);
    }

    [Fact]
    public void TheWordsForAReader_ShouldBePlain()
    {
        Assert.Equal("Not verified: the data doesn't match its signature", new SignatureCheck(false, SignatureCheck.NoMatch).Words);
        Assert.Equal("Signed with a key this site doesn't know.", new SignatureCheck(false, SignatureCheck.KeyNotTrusted).Reason);
        Assert.Equal("Verified", SignatureCheck.Verified.Words);
    }

    [Fact]
    public void ThePages_ShouldShowTheReason()
    {
        var hero = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_VersionsCard.cshtml"));

        Assert.Contains("@Model.Signature.Words", hero, StringComparison.Ordinal);
        Assert.Contains("Not verified", card, StringComparison.Ordinal);
        Assert.Contains("@row.Signature.Reason", card, StringComparison.Ordinal);
    }

    /// <summary>Re-signs the stored core with another key: a sound signature, but not the app's.</summary>
    internal static async Task GiveTheProofAnotherKeyAsync(PublicationRig rig, PassportVersion version, ECDsa key)
    {
        var point = key.ExportParameters(includePrivateParameters: false).Q;
        var signature = key.SignData(Encoding.UTF8.GetBytes(CanonicalJson.Serialize(version.StaticCore)), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", version.Id),
            Builders<BsonDocument>.Update
                .Set("Proof.ProofValue", Base64Url.Encode(signature))
                .Set("Proof.PublicKeyX", Base64Url.Encode(point.X!))
                .Set("Proof.PublicKeyY", Base64Url.Encode(point.Y!)));
    }
}
