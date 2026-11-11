using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R07 (the owner's decision, 10 Nov 2026): a published passport's public address also answers data, without a
/// token: the public part only, cut by the same rule as the public page, with a proof anyone can check.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PublicPassportDataTests(MongoFixture mongo)
{
    [Theory]
    [InlineData("application/json", true)]
    [InlineData("application/json; charset=utf-8", true)]
    [InlineData("application/json;q=1, text/html;q=0.5", true)]
    [InlineData("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8", false)]
    [InlineData("text/html, application/json", false)]
    [InlineData("*/*", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("application/json;q=0", false)]
    public void TheAddress_ShouldAnswerData_OnlyWhenAskedForJson(string? accept, bool data) =>
        Assert.Equal(data, PrefersJsonAttribute.PrefersJson(accept));

    [Fact]
    public async Task ThePublicAddress_ShouldAnswerThePublicPartAsData_WithoutAToken()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var id = PassportPagesTests.Identifier(rig);

        var byAccept = await DataAsync(Controller(rig).SummaryData(Uri.EscapeDataString(id), CancellationToken.None));
        var byAddress = await DataAsync(Controller(rig).SummaryJson(Uri.EscapeDataString(id), CancellationToken.None));

        Assert.Equal(byAccept.ToJsonString(), byAddress.ToJsonString());
        var published = (await SigningTesting.Passports(rig.Store).CurrentAsync(rig.PackId))!;
        var expected = PassportFilter.Cut(published.Passport, Reader.Anonymous, []);
        var withoutProof = byAccept.DeepClone().AsObject();
        withoutProof.Remove("proof");
        Assert.Equal(CanonicalJson.Serialize(expected), CanonicalJson.Serialize(withoutProof));
        Assert.NotNull(Find(byAccept, AttributeCatalog.ByNumber(6)));
    }

    [Fact]
    public async Task TheData_ShouldLeaveOutWhatThePublicMayNotSee()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var full = (await SigningTesting.Passports(rig.Store).CurrentAsync(rig.PackId))!.Passport;
        Assert.NotNull(Find(full, AttributeCatalog.ByNumber(13))); // the seed has an authorities-only value...
        Assert.NotNull(Find(full, AttributeCatalog.ByNumber(22))); // ...and a purchaser-only one

        var data = await DataAsync(Controller(rig).SummaryJson(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.Null(Find(data, AttributeCatalog.ByNumber(13)));
        Assert.Null(Find(data, AttributeCatalog.ByNumber(22)));
        foreach (var attribute in AttributeCatalog.All.Where(a => a.Audience != Audience.Public))
        {
            Assert.Null(Find(data, attribute));
        }
    }

    [Fact]
    public async Task TheDataAndThePage_ShouldShowTheSameValues()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var id = PassportPagesTests.Identifier(rig);
        var page = (await PassportPagesTests.Pages(rig).SummaryAsync(Reader.Anonymous, id))!;

        var data = await DataAsync(Controller(rig).SummaryJson(id, CancellationToken.None));

        var shown = page.Sections.SelectMany(section => section.Values).Select(value => value.Attribute).ToArray();
        Assert.NotEmpty(shown);
        foreach (var attribute in shown)
        {
            Assert.NotNull(Find(data, attribute)); // every value on the page is in the data...
        }

        foreach (var attribute in AttributeCatalog.All.Where(a => Find(data, a) is not null))
        {
            Assert.Equal(Audience.Public, attribute.Audience); // ...and the data holds public values only
            var lines = ValueWords.Lines(AttributeFormsTests.Forms.For(attribute), Find(data, attribute));
            Assert.True(lines.Count == 0 || shown.Contains(attribute), $"#{attribute.Number} is in the data but not on the page");
        }
    }

    [Fact]
    public async Task TheDatasProof_ShouldHold_ForAnyoneWhoChecksIt()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var text = await TextAsync(Controller(rig).SummaryJson(PassportPagesTests.Identifier(rig), CancellationToken.None));

        // What a reader does: parse the text, take the signed part, check hash and signature with the proof's key.
        var data = JsonNode.Parse(text)!.AsObject();
        var proof = data["proof"]!;
        var part = PublicPassport.SignedPart(data);
        Assert.Equal(proof["hash"]!.GetValue<string>(), CanonicalJson.Hash(part));
        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Base64Url.Decode(proof["publicKeyX"]!.GetValue<string>()), Y = Base64Url.Decode(proof["publicKeyY"]!.GetValue<string>()) },
        });
        Assert.True(key.VerifyData(
            Encoding.UTF8.GetBytes(CanonicalJson.Serialize(part)),
            Base64Url.Decode(proof["proofValue"]!.GetValue<string>()),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.Equal(SigningTesting.Keys.Issuer, proof["issuer"]!.GetValue<string>());
        Assert.Equal(SigningTesting.Keys.VerificationMethod, proof["verificationMethod"]!.GetValue<string>());
        Assert.Equal(PassportSigner.ProofType, proof["type"]!.GetValue<string>());

        // Changing a public value breaks it.
        data["batteryPassportData"]!["identifiersAndProductData"]!["batterySerialNumber"] = "VPC8C05-6789";
        Assert.NotEqual(proof["hash"]!.GetValue<string>(), CanonicalJson.Hash(PublicPassport.SignedPart(data)));
    }

    [Fact]
    public async Task TheDatasProof_ShouldStillHold_AfterALiveValueChanges()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        await BatteryApiTests.Api(rig).WriteLiveValuesAsync(PublicationTesting.Team, pack, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(41) }, null, preview: false);

        var data = JsonNode.Parse(await TextAsync(Controller(rig).SummaryJson(PassportPagesTests.Identifier(rig), CancellationToken.None)))!.AsObject();

        Assert.Equal(data["proof"]!["hash"]!.GetValue<string>(), CanonicalJson.Hash(PublicPassport.SignedPart(data)));
    }

    [Fact]
    public async Task AVersionPublishedBeforeThePublicProof_ShouldSaySoPlainly()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var version = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        await rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", version.Id),
            Builders<BsonDocument>.Update.Unset("Proof.PublicView"));

        var data = await DataAsync(Controller(rig).SummaryJson(PassportPagesTests.Identifier(rig), CancellationToken.None));

        Assert.True(data.ContainsKey("proof"));
        Assert.Null(data["proof"]);
        var stored = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        Assert.Null(stored.Proof.PublicView);
        Assert.True(SigningTesting.Passports(rig.Store).Check(stored).Holds); // the version itself verifies as before
        Assert.NotNull(Find(data, AttributeCatalog.ByNumber(6)));
    }

    [Fact]
    public async Task ADraftOrAnUnknownId_ShouldAnswer404ForData_AsThePageDoes()
    {
        var draft = await PublicationTesting.RigAsync(mongo);

        foreach (var id in new[] { PassportPagesTests.Identifier(draft), "VPC8C05-0000" })
        {
            Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ViewResult>(await Controller(draft).Summary(id, CancellationToken.None)).StatusCode);
            foreach (var result in new[] { await Controller(draft).SummaryData(id, CancellationToken.None), await Controller(draft).SummaryJson(id, CancellationToken.None) })
            {
                var answer = Assert.IsAssignableFrom<ObjectResult>(result);
                Assert.Equal(StatusCodes.Status404NotFound, answer.StatusCode);
                Assert.DoesNotContain("batteryPassportData", JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(answer.Value))!.ToJsonString(), StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void TheDataAddresses_ShouldBeOpen_WithThePublicReadLimit()
    {
        var type = typeof(PassportController);
        foreach (var name in new[] { nameof(PassportController.SummaryData), nameof(PassportController.SummaryJson), nameof(PassportController.VersionData), nameof(PassportController.VersionJson) })
        {
            var method = type.GetMethod(name)!;
            Assert.Equal(SecurityRateLimitPolicyNames.ExternalApiRead, method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        }

        Assert.Equal("{id}", type.GetMethod(nameof(PassportController.SummaryData))!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.NotNull(type.GetMethod(nameof(PassportController.SummaryData))!.GetCustomAttribute<PrefersJsonAttribute>());
        Assert.Equal("{id}.json", type.GetMethod(nameof(PassportController.SummaryJson))!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal("{id}/v{version:int}", type.GetMethod(nameof(PassportController.VersionData))!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.NotNull(type.GetMethod(nameof(PassportController.VersionData))!.GetCustomAttribute<PrefersJsonAttribute>());
        Assert.Equal("{id}/v{version:int}.json", type.GetMethod(nameof(PassportController.VersionJson))!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Null(type.GetMethod(nameof(PassportController.Summary))!.GetCustomAttribute<EnableRateLimitingAttribute>()); // the page itself is as before
    }

    [Fact]
    public async Task ThePublicPage_ShouldLinkToItsData()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var id = PassportPagesTests.Identifier(rig);
        var controller = Controller(rig);

        var view = Assert.IsType<ViewResult>(await controller.Summary(id, CancellationToken.None));

        var page = Assert.IsType<PassportPage>(view.Model);
        Assert.Equal($"/dpp/{BatteryPassWeb.Dpp.Identifiers.IdentifierForms.PagePath(page.Address)}.json", page.DataHref);
        Assert.Equal($"<{page.DataHref}>; rel=\"alternate\"; type=\"application/json\"", controller.Response.Headers["Link"].ToString());
        Assert.Contains("Accept", controller.Response.Headers["Vary"].ToString(), StringComparison.Ordinal);
        var summary = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        Assert.Contains("<link rel=\"alternate\" type=\"application/json\" href=\"@Model.DataHref\" />", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TheApiHelp_ShouldDescribeThePublicData()
    {
        var help = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("data-api-help-detail-target=\"api-detail-public-data\"", help, StringComparison.Ordinal);
        Assert.Contains("<article id=\"api-detail-public-data\"", help, StringComparison.Ordinal);
        Assert.Contains("Accept: application/json", help, StringComparison.Ordinal);
        Assert.Contains("won't pass the schema as a full passport", help, StringComparison.Ordinal);
        Assert.Contains("/.well-known/did.json", help, StringComparison.Ordinal);
    }

    internal static PassportController Controller(PublicationRig rig) =>
        new(new DppReaders(new ClusterRepository(rig.Context)), PassportPagesTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } },
        };

    internal static async Task<string> TextAsync(Task<IActionResult> action)
    {
        var content = Assert.IsType<ContentResult>(await action);
        Assert.Equal("application/json", content.ContentType);
        return content.Content!;
    }

    internal static async Task<JsonObject> DataAsync(Task<IActionResult> action) => JsonNode.Parse(await TextAsync(action))!.AsObject();

    internal static JsonNode? Find(JsonObject passport, AttributeDefinition attribute)
    {
        JsonNode? node = passport;
        foreach (var name in attribute.Path.Split('.'))
        {
            node = (node as JsonObject)?[name];
        }

        return node;
    }
}
