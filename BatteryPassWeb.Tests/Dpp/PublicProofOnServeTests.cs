using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// C3 round two, point 1: the public proof is checked against the public data being served, on every answer. When the
/// who-sees-what list changes after publication, the stored proof no longer matches what is served, and the answer says
/// <c>"proof": null</c>; the data is always cut afresh, never a stored signed copy.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PublicProofOnServeTests(MongoFixture mongo)
{
    [Fact]
    public async Task AProofOverOtherPublicValues_ShouldNotBeServed()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        // As if the public part had been cut by another list at publication: one more value, signed by the app's own key.
        var then = PublicPassport.SignedPart(PublicPassport.Cut(version.StaticCore.DeepClone().AsObject()));
        then["batteryPassportData"]!.AsObject()["identifiersAndProductData"]!.AsObject()["noLongerPublic"] = "a value the public may no longer see";
        await StorePublicViewAsync(rig, version, then, SigningTesting.Keys.SignData);

        var data = await PublicPassportDataTests.DataAsync(PublicPassportDataTests.Controller(rig).SummaryJson(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.True(data.ContainsKey(PublicPassport.ProofProperty));
        Assert.Null(data[PublicPassport.ProofProperty]);
        Assert.DoesNotContain("noLongerPublic", data.ToJsonString(), StringComparison.Ordinal); // never the signed copy
        Assert.NotNull(PublicPassportDataTests.Find(data, AttributeCatalog.ByNumber(6)));
    }

    [Fact]
    public async Task AProofByAKeyTheSiteDoesntKnow_ShouldNotBeServed()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var part = PublicPassport.SignedPart(PublicPassport.Cut(version.StaticCore.DeepClone().AsObject()));
        await StorePublicViewAsync(rig, version, part, bytes => stranger.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation), stranger);

        var data = await PublicPassportDataTests.DataAsync(PublicPassportDataTests.Controller(rig).SummaryJson(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.Null(data[PublicPassport.ProofProperty]);
    }

    [Theory]
    [InlineData("PublicKeyX")]
    [InlineData("ProofValue")]
    [InlineData("Hash")]
    public async Task ADamagedPublicProof_ShouldNotBeServed(string part)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", version.Id),
            Builders<BsonDocument>.Update.Unset($"Proof.PublicView.{part}"));

        var data = await PublicPassportDataTests.DataAsync(PublicPassportDataTests.Controller(rig).SummaryJson(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.Null(data[PublicPassport.ProofProperty]);
    }

    [Fact]
    public async Task AProofThatHolds_ShouldBeServedAsStored()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var stored = (await rig.Store.GetActiveVersionAsync(rig.PackId))!.Proof.PublicView!;

        var data = await PublicPassportDataTests.DataAsync(PublicPassportDataTests.Controller(rig).SummaryJson(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.Equal(PublicPassport.ProofJson(stored).ToJsonString(), data[PublicPassport.ProofProperty]!.ToJsonString());
    }

    /// <summary>Replaces the stored public proof with a signature over <paramref name="part"/>, by the given key.</summary>
    private static async Task StorePublicViewAsync(PublicationRig rig, PassportVersion version, JsonObject part, Func<byte[], byte[]> sign, ECDsa? key = null)
    {
        var update = Builders<BsonDocument>.Update
            .Set("Proof.PublicView.Hash", CanonicalJson.Hash(part))
            .Set("Proof.PublicView.ProofValue", Base64Url.Encode(sign(Encoding.UTF8.GetBytes(CanonicalJson.Serialize(part)))));
        if (key is not null)
        {
            var point = key.ExportParameters(includePrivateParameters: false).Q;
            update = update.Set("Proof.PublicView.PublicKeyX", Base64Url.Encode(point.X!)).Set("Proof.PublicView.PublicKeyY", Base64Url.Encode(point.Y!));
        }

        await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", version.Id), update);
    }
}
