using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// C3 round two, point 3: a stored proof with a missing or unreadable part is "Not verified" with a reason of its own on
/// every page and API answer, never an error.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DamagedProofTests(MongoFixture mongo)
{
    [Theory]
    [InlineData("PublicKeyX", null)]
    [InlineData("PublicKeyY", "AAAA")]
    [InlineData("ProofValue", "not base64 at all!")]
    [InlineData("ProofValue", null)]
    [InlineData("Hash", null)]
    [InlineData("Proof", null)]
    public async Task AProofThatCantBeRead_ShouldBeNotVerified_Everywhere(string part, string? value)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        var path = part == "Proof" ? "Proof" : $"Proof.{part}";
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", version.Id);
        await versions.UpdateOneAsync(filter, value is null ? Builders<BsonDocument>.Update.Unset(path) : Builders<BsonDocument>.Update.Set(path, value));
        var id = PassportPagesTests.Identifier(rig);
        var team = Reader.With("team@example.com", ReaderRole.Team);

        var detail = (await PassportPagesTests.Pages(rig).DetailAsync(team, id))!;
        var summary = (await PassportPagesTests.Pages(rig).SummaryAsync(Reader.Anonymous, id))!;
        var batteryRow = Assert.Single((await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Versions);
        var clusterRow = Assert.Single((await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(BatteryPassWeb.Dpp.Editing.ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId))!.Versions);
        var api = new BatteryPassWeb.Dpp.Api.PassportApi(rig.Store, TestDemo.For(rig), SigningTesting.Passports(rig.Store), PassportPagesTests.Pages(rig), BatteryTesting.Pages(rig));
        var read = (await api.ReadAsync(team, id))!;

        Assert.Equal($"Not verified: {SignatureCheck.Unreadable}", detail.Signature.Words);
        Assert.False(summary.SignatureHolds);
        Assert.Equal("The signature can't be read.", batteryRow.Signature.Reason);
        Assert.Equal("The signature can't be read.", clusterRow.Signature.Reason);
        Assert.False(read.SignatureVerified);
        Assert.False(api.Verified(read.Version));
    }

    [Fact]
    public void AProofWithoutParts_ShouldNotThrowFromTheSigner()
    {
        var proof = new SignatureProof
        {
            Type = PassportSigner.ProofType,
            Cryptosuite = PassportSigner.Cryptosuite,
            CreatedUtc = DateTime.UtcNow,
            Issuer = null!,
            VerificationMethod = null!,
            Hash = null!,
            ProofValue = null!,
            PublicKeyX = null!,
            PublicKeyY = null!,
        };
        var core = new System.Text.Json.Nodes.JsonObject { ["a"] = 1 };

        Assert.False(PassportSigner.Verify(core, proof));
        Assert.Equal(SignatureCheck.Unreadable, new PassportSigner(SigningTesting.Keys).Check(core, proof).Problem);
        Assert.Equal(SignatureCheck.Unreadable, new PassportSigner(SigningTesting.Keys).Check(core, null).Problem);
    }
}
