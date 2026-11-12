using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R05 (the owner's decision, 11 Nov 2026: "Yes, as the article"): notified bodies see a model's restricted data,
/// #43 and #45 to #48, through the one who-sees-what rule; nothing changes for any other reader or for the public data.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class NotifiedBodyRestrictedDataTests(MongoFixture mongo)
{
    private static readonly int[] Restricted = [43, 45, 46, 47, 48];

    [Fact]
    public async Task TheDetailPage_ShouldShowANotifiedBodyTheModelsRestrictedData_OnADemoBattery()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pages = rig.Services.GetRequiredService<PassportPages>();
        var reader = rig.Services.GetRequiredService<PassportReader>();
        var notifiedBody = Reader.With("demo.notified-body@example.test", ReaderRole.NotifiedBody);

        foreach (var version in await rig.Store.ListAllActiveVersionsAsync())
        {
            var passport = (await reader.CurrentAsync(version.PackId))!.Passport;
            var page = (await pages.DetailAsync(notifiedBody, version.DigitalProductPassportId))!;
            var shared = Assert.Single(page.Audiences, section => section.Key == "shared");
            Assert.Equal(
                Restricted.Where(number => PublicPassportDataTests.Find(passport, AttributeCatalog.ByNumber(number)) is not null),
                shared.Values.Select(value => value.Attribute.Number).Order());
            Assert.Contains("notified bodies", shared.Note, StringComparison.Ordinal);
            Assert.DoesNotContain(page.Audiences, section => section.Key == "purchaser"); // still not the purchaser's data
        }
    }

    [Fact]
    public async Task ThePublicData_ShouldStayThePublicPartOnly_WithItsProof_ForEveryPublishedDemoBattery()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pages = rig.Services.GetRequiredService<PassportPages>();
        var reader = rig.Services.GetRequiredService<PassportReader>();
        var versions = await rig.Store.ListAllActiveVersionsAsync();
        Assert.NotEmpty(versions);

        foreach (var version in versions)
        {
            var data = (await pages.SummaryAsync(Reader.Anonymous, version.DigitalProductPassportId))!.PublicData!;

            // The bytes signed at publication are the bytes served: the public proof still holds for them.
            Assert.NotNull(data[PublicPassport.ProofProperty]);
            var values = data.DeepClone().AsObject();
            values.Remove(PublicPassport.ProofProperty);
            var passport = (await reader.CurrentAsync(version.PackId))!.Passport;
            Assert.Equal(CanonicalJson.Serialize(OnlyPublic(passport)), CanonicalJson.Serialize(values));
            foreach (var number in Restricted)
            {
                Assert.Null(PublicPassportDataTests.Find(data, AttributeCatalog.ByNumber(number)));
            }
        }
    }

    /// <summary>The public data of every published demo battery as the rule before R05 gave it (round two, 4).</summary>
    private static readonly string Snapshot = Path.Combine(Path.GetDirectoryName(DppTestFiles.RepoFile("BatteryPassWeb.Tests", "BatteryPassWeb.Tests.csproj"))!, "Dpp", "Snapshots", "r05-public-data-before.txt");

    [Fact]
    public async Task ThePublicData_ShouldBeTheBytesFromBeforeTheChange_WithAProofThatHolds()
    {
        using var rig = await DemoRig.StartAsync(mongo, dunsIds: true); // the real ID scheme: the same IDs at every run
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pages = rig.Services.GetRequiredService<PassportPages>();
        var text = new System.Text.StringBuilder();
        foreach (var version in (await rig.Store.ListAllActiveVersionsAsync()).OrderBy(version => version.DigitalProductPassportId, StringComparer.Ordinal))
        {
            var data = (await pages.SummaryAsync(Reader.Anonymous, version.DigitalProductPassportId))!.PublicData!;

            // The proof, checked as a reader would: the hash of the signed part, and the signature with the proof's key.
            var proof = data[PublicPassport.ProofProperty]!;
            var part = PublicPassport.SignedPart(data);
            Assert.Equal(proof["hash"]!.GetValue<string>(), CanonicalJson.Hash(part));
            using var key = System.Security.Cryptography.ECDsa.Create(new System.Security.Cryptography.ECParameters
            {
                Curve = System.Security.Cryptography.ECCurve.NamedCurves.nistP256,
                Q = new System.Security.Cryptography.ECPoint { X = Base64Url.Decode(proof["publicKeyX"]!.GetValue<string>()), Y = Base64Url.Decode(proof["publicKeyY"]!.GetValue<string>()) },
            });
            Assert.True(key.VerifyData(
                System.Text.Encoding.UTF8.GetBytes(CanonicalJson.Serialize(part)),
                Base64Url.Decode(proof["proofValue"]!.GetValue<string>()),
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

            // The bytes, without the proof (its signature differs at every signing) and with the seed's moments in time
            // written as placeholders (the demo is seeded at the test's own time).
            var values = data.DeepClone().AsObject();
            values.Remove(PublicPassport.ProofProperty);
            text.Append(version.DigitalProductPassportId).Append('\n').Append(Steady(CanonicalJson.Serialize(values))).Append('\n');
        }

        if (Environment.GetEnvironmentVariable("BP_WRITE_R05_SNAPSHOT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Snapshot)!);
            File.WriteAllText(Snapshot, text.ToString()); // written once, with the rule as it was before R05
        }

        Assert.Equal(File.ReadAllText(Snapshot), text.ToString());
    }

    /// <summary>The seed's moments in time (timestamps and days) as placeholders, so the bytes compare across runs.</summary>
    private static string Steady(string json) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(json, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z", "<time>"),
            @"\d{4}-\d{2}-\d{2}",
            "<day>");

    [Fact]
    public void TheEditorsWords_ShouldNameNotifiedBodies_ForTheRestrictedData()
    {
        foreach (var number in Restricted)
        {
            var attribute = AttributeCatalog.ByNumber(number);
            var field = new EditorField(AttributeFormsTests.Forms.For(attribute), new Dictionary<string, string>(), 1, null, null, false);

            Assert.Contains("the battery's cluster, notified bodies, market surveillance, the Commission and your team", field.SeenBy, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The public part worked out without <see cref="AudienceRules"/>: every value whose audience isn't public removed by its
    /// path, as the rule did before this change for an anonymous reader. Sections left empty are dropped.
    /// </summary>
    private static JsonObject OnlyPublic(JsonObject passport)
    {
        var copy = passport.DeepClone().AsObject();
        foreach (var attribute in AttributeCatalog.All.Where(a => a.Audience != Audience.Public))
        {
            var parts = attribute.Path.Split('.');
            var parent = copy as JsonNode;
            foreach (var part in parts[..^1])
            {
                parent = parent?[part];
            }

            (parent as JsonObject)?.Remove(parts[^1]);
        }

        return copy;
    }
}
