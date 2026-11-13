using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding W9 (decisions 29 and 33): a cluster token reads, or reads and sends live values, nothing more. The bulk button
/// makes only those; a token that already has more is listed with what it can still do, and the team may replace it.
/// Nothing is changed or deleted by itself.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterTokenKindsTests(MongoFixture mongo)
{
    private static ExternalApiRepository Tokens(AccountRig rig) => rig.Services.GetRequiredService<ExternalApiRepository>();

    private static async Task<List<BsonDocument>> AllAsync(AccountRig rig) =>
        (await Tokens(rig).ListTokensAsync()).ToList();

    [Fact]
    public async Task TheBulkButton_ShouldMakeOnlyAReadAndAReadWriteTokenForEachCluster()
    {
        using var rig = await AccountRig.StartAsync(mongo);

        await rig.TeamMember().GenerateClusterTokens(CancellationToken.None);

        var tokens = await AllAsync(rig);
        Assert.Equal(4, tokens.Count); // two clusters, two each
        Assert.Equal(["read", "read", "readWrite", "readWrite"], tokens.Select(token => token["accessMode"].AsString).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(tokens, token => token["accessMode"].AsString.Contains("Sign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TheBulkButton_ShouldLeaveAnExistingReadWriteSignTokenAsItIs()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var (old, _) = await Tokens(rig).CreateTokenAsync("North - readwritesign", ExternalTokenAccessMode.Lifecycle, ["north"], false, false, "team", autoClusterId: "north");

        await rig.TeamMember().GenerateClusterTokens(CancellationToken.None);

        var stored = (await Tokens(rig).GetTokenByIdAsync(old["tokenId"].AsString))!;
        Assert.Equal("readWriteSign", stored["accessMode"].AsString);
        Assert.True(stored["isActive"].AsBoolean);
    }

    [Theory]
    [InlineData("readWriteSign", false, true)]
    [InlineData("sign", false, true)]
    [InlineData("readWriteSign", true, false)]
    [InlineData("sign", true, false)]
    [InlineData("readWrite", false, false)]
    [InlineData("read", false, false)]
    public void ATokenHasMoreThanAClusterToken_OnlyWhenItSignsWithoutBeingForAllBatteries(string mode, bool global, bool more)
    {
        Assert.Equal(more, ClusterTokenRules.HasMoreThanAClusterToken(mode, global));
    }

    [Fact]
    public void TheTokenList_ShouldSayWhatSuchATokenCanStillDo_AndOfferTheTeamAReplacement()
    {
        var list = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_ApiTokenList.cshtml"));

        Assert.Contains("ClusterTokenRules.HasMoreThanAClusterToken", list, StringComparison.Ordinal);
        Assert.Contains("ClusterTokenRules.StillDoes", list, StringComparison.Ordinal);
        Assert.Contains("@Model.ActionBase/replace", list, StringComparison.Ordinal);
        Assert.Contains("Model.CanSetActive", list, StringComparison.Ordinal); // the team's page only
    }

    [Fact]
    public void WhatSuchATokenCanStillDo_ShouldDependOnWhetherItWrites()
    {
        Assert.Contains("live values", ClusterTokenRules.StillDoes("readWriteSign"), StringComparison.Ordinal);
        Assert.DoesNotContain("live values", ClusterTokenRules.StillDoes("sign"), StringComparison.Ordinal);
        Assert.Contains("can't create or publish", ClusterTokenRules.StillDoes("sign"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Replacing_ShouldMakeAReadWriteTokenForTheSameClustersAndSwitchTheOldOneOff()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var (old, oldValue) = await Tokens(rig).CreateTokenAsync("Customer North - readwritesign", ExternalTokenAccessMode.Lifecycle, ["north"], false, false, "team", autoClusterId: "north");
        var team = AccountRig.Form(rig.TeamMember(), ("tokenId", old["tokenId"].AsString));

        await team.ReplaceApiToken(CancellationToken.None);

        var tokens = await AllAsync(rig);
        var replacement = Assert.Single(tokens, token => token["tokenId"].AsString != old["tokenId"].AsString);
        Assert.Equal("readWrite", replacement["accessMode"].AsString);
        Assert.Equal("Customer North - readwrite", replacement["name"].AsString);
        Assert.Equal(["north"], replacement["clusterIds"].AsBsonArray.Select(id => id.AsString));
        Assert.False(replacement["globalAccess"].AsBoolean);
        Assert.False((await Tokens(rig).GetTokenByIdAsync(old["tokenId"].AsString))!["isActive"].AsBoolean);
        Assert.False((await Tokens(rig).ValidateTokenAsync(oldValue, ExternalTokenRequirement.Read)).Success);
        Assert.False(string.IsNullOrWhiteSpace(team.TempData["GeneratedCredential"] as string));
        Assert.True((await Tokens(rig).ValidateTokenAsync((string)team.TempData["GeneratedCredential"]!, ExternalTokenRequirement.Write)).Success);
        Assert.False((await Tokens(rig).ValidateTokenAsync((string)team.TempData["GeneratedCredential"]!, ExternalTokenRequirement.Sign)).Success);
    }

    [Fact]
    public async Task ASignOnlyClusterToken_ShouldBeReplacedByAReadToken()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var (old, _) = await Tokens(rig).CreateTokenAsync("Lifecycle", ExternalTokenAccessMode.Sign, ["north"], false, false, "team");

        await AccountRig.Form(rig.TeamMember(), ("tokenId", old["tokenId"].AsString)).ReplaceApiToken(CancellationToken.None);

        Assert.Contains(await AllAsync(rig), token => token["accessMode"].AsString == "read" && token["tokenId"].AsString != old["tokenId"].AsString);
    }

    [Fact]
    public async Task Replacing_ShouldRefuseATokenThatHasNoMoreThanAClusterToken()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var (fine, _) = await Tokens(rig).CreateTokenAsync("Fine", ExternalTokenAccessMode.ReadWrite, ["north"], false, false, "team");
        var (global, _) = await Tokens(rig).CreateTokenAsync("Team", ExternalTokenAccessMode.Lifecycle, [], true, true, "team");

        foreach (var id in new[] { fine["tokenId"].AsString, global["tokenId"].AsString, "no-such-token" })
        {
            var team = AccountRig.Form(rig.TeamMember(), ("tokenId", id));
            await team.ReplaceApiToken(CancellationToken.None);
            Assert.False(string.IsNullOrWhiteSpace(team.TempData["ErrorMessage"] as string));
        }

        Assert.Equal(2, (await AllAsync(rig)).Count);
        Assert.All(await AllAsync(rig), token => Assert.True(token["isActive"].AsBoolean));
    }

    [Theory]
    [InlineData("sign")]
    [InlineData("readWriteSign")]
    public async Task TheTeamsCreateForm_ShouldNotMakeASigningTokenForClusters(string mode)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = rig.TeamMember();
        team.HttpContext.Request.Form = new Microsoft.AspNetCore.Http.FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["name"] = "Too much",
            ["accessMode"] = mode,
            ["clusterIds"] = new Microsoft.Extensions.Primitives.StringValues(["north"]),
        });

        await team.CreateApiToken(CancellationToken.None);

        Assert.Equal(ClusterTokenRules.OnlyForAllBatteries, team.TempData["ErrorMessage"]);
        Assert.Empty(await AllAsync(rig));
    }

    [Fact]
    public async Task TheTeamsCreateForm_ShouldStillMakeASigningTokenForAllBatteries()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(), ("name", "Team token"), ("accessMode", "readWriteSign"), ("globalAccess", "on"));

        await team.CreateApiToken(CancellationToken.None);

        Assert.Equal("readWriteSign", Assert.Single(await AllAsync(rig))["accessMode"].AsString);
    }
}
