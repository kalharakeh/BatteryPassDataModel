using System.Security.Cryptography;
using System.Text.Json;
using BatteryPassWeb.Dpp.Signing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// C3 round two, point 4: a failed check says only what is known, the APIs give the plain reason next to
/// signatureVerified, and the team alone is told what to do about a key the site doesn't know.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SignatureReasonTests(MongoFixture mongo)
{
    [Fact]
    public void AFailedMatch_ShouldSayTheDataDoesntMatchItsSignature()
    {
        Assert.Equal("Not verified: the data doesn't match its signature", new SignatureCheck(false, SignatureCheck.NoMatch).Words);
        Assert.Equal("The data doesn't match its signature.", new SignatureCheck(false, SignatureCheck.NoMatch).Reason);
    }

    [Fact]
    public void AWrongSignatureOverUnchangedData_ShouldNotClaimTheDataChanged()
    {
        var core = new System.Text.Json.Nodes.JsonObject { ["a"] = 1 };
        var signer = new PassportSigner(SigningTesting.Keys);
        var proof = signer.Sign(core, DateTime.UtcNow);
        var forged = proof.ProofValue[..^4] + (proof.ProofValue.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        var check = signer.Check(core, new SignatureProof
        {
            Type = proof.Type,
            Cryptosuite = proof.Cryptosuite,
            CreatedUtc = proof.CreatedUtc,
            Issuer = proof.Issuer,
            VerificationMethod = proof.VerificationMethod,
            Hash = proof.Hash,
            ProofValue = forged,
            PublicKeyX = proof.PublicKeyX,
            PublicKeyY = proof.PublicKeyY,
        });

        Assert.Equal(SignatureCheck.NoMatch, check.Problem);
        Assert.DoesNotContain("changed", check.Words, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheApis_ShouldGiveTheReason_NullWhenVerified()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        var id = PassportPagesTests.Identifier(rig);

        var answers = await AnswersAsync(rig, tokens["global"], id);

        Assert.All(answers, answer =>
        {
            Assert.True(answer.GetProperty("signatureVerified").GetBoolean());
            Assert.Equal(JsonValueKind.Null, answer.GetProperty("signatureReason").ValueKind);
        });
    }

    [Fact]
    public async Task TheApis_ShouldGiveThePlainReason_WhenNotVerified()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var tokens = await TokensAsync(rig);
        var id = PassportPagesTests.Identifier(rig);
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await SignatureOnPagesTests.GiveTheProofAnotherKeyAsync(rig, (await rig.Store.GetActiveVersionAsync(rig.PackId))!, stranger);

        var answers = await AnswersAsync(rig, tokens["global"], id);

        Assert.Equal(4, answers.Count);
        Assert.All(answers, answer =>
        {
            Assert.False(answer.GetProperty("signatureVerified").GetBoolean());
            Assert.Equal("Signed with a key this site doesn't know.", answer.GetProperty("signatureReason").GetString());
            Assert.DoesNotContain(BatteryPassWeb.Services.DemoSigningKeyService.TrustedKeysSetting, answer.GetRawText(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void WhatToDoAboutAnUnknownKey_ShouldBeForTheTeamOnly()
    {
        var unknown = new SignatureCheck(false, SignatureCheck.KeyNotTrusted);
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_VersionsCard.cshtml"));

        Assert.Contains(BatteryPassWeb.Services.DemoSigningKeyService.TrustedKeysSetting, unknown.TeamAdvice, StringComparison.Ordinal);
        Assert.Contains("runbook", unknown.TeamAdvice, StringComparison.Ordinal);
        Assert.Null(new SignatureCheck(false, SignatureCheck.NoMatch).TeamAdvice);
        Assert.Null(SignatureCheck.Verified.TeamAdvice);
        Assert.Contains("@if (Model.ForTeam && row.Signature.TeamAdvice is { } advice)", card, StringComparison.Ordinal);
        Assert.DoesNotContain("TeamAdvice", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportHero.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheClusterAdminsPage_ShouldNotBeTheTeams()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var team = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!;
        var cluster = (await ClusterBatteryPagesTests.Pages(rig).BatteryAsync(BatteryPassWeb.Dpp.Editing.ChangeSender.ClusterAdmin("north-admin@example.com", BatteryTesting.North), rig.PackId))!;

        Assert.True(team.Cards.ForTeam);
        Assert.False(cluster.Cards.ForTeam);
    }

    /// <summary>The external API's battery and versions answers, the passports API's detail and its public /verify.</summary>
    private static async Task<IReadOnlyList<JsonElement>> AnswersAsync(PublicationRig rig, string token, string id)
    {
        var battery = Data(await DppApiControllerTests.Controller(rig, token).Battery(id, CancellationToken.None));
        var versions = Data(await DppApiControllerTests.Controller(rig, token).Passports(id, CancellationToken.None)).GetProperty("passports").EnumerateArray().ToArray();
        var passports = DppApiControllerTests.PassportsController(rig);
        var detail = DppApiControllerTests.Json(Assert.IsType<OkObjectResult>(await passports.Detail(id, CancellationToken.None)));
        var verify = DppApiControllerTests.Json(Assert.IsType<OkObjectResult>(await passports.Verify(id, CancellationToken.None)));
        return [battery, Assert.Single(versions), detail, verify];
    }

    private static async Task<Dictionary<string, string>> TokensAsync(PublicationRig rig) => new()
    {
        ["global"] = (await DppApiControllerTests.Repository(rig).CreateTokenAsync("global", BatteryPassWeb.Services.ExternalTokenAccessMode.Read, [], false, true, "test")).PlainToken,
    };

    private static JsonElement Data(IActionResult result)
    {
        var answer = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, answer.StatusCode);
        return DppApiControllerTests.Json(answer).GetProperty("data");
    }
}
