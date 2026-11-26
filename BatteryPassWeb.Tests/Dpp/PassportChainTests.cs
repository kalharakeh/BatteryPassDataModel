using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's item 12 ("Team + MSA full, admin limited"): a battery placed on the market again shows
/// "Passports of this battery", oldest first, each with its battery ID, status, from–to dates and a link. The team sees
/// every passport and its history; the detail page shows it to the team and market surveillance; the current cluster
/// admin sees public facts only; public readers keep "replaces an earlier passport".
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportChainTests(MongoFixture mongo)
{
    [Fact]
    public async Task ABatteryPlacedOnTheMarketAgain_ShouldListItsPassportsInOrder()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var old = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var successor = (await rig.Store.GetAsync<BatteryPack>(old.Succession!.SuccessorPackId!))!;

        var fromNew = await PassportChains.ForAsync(rig.Store, successor);
        var fromOld = await PassportChains.ForAsync(rig.Store, old);

        Assert.Equal([rig.PackId, successor.Id], fromNew.Select(link => link.PackId));
        Assert.Equal([1, 2], fromNew.Select(link => link.Number));
        Assert.Equal((DppStatuses.Inactive, false, false), (fromNew[0].Status, fromNew[0].IsThis, fromNew[0].IsNewest));
        Assert.NotNull(fromNew[0].ToUtc); // replaced: it has an end date
        Assert.Equal((DppStatuses.Active, true, true), (fromNew[1].Status, fromNew[1].IsThis, fromNew[1].IsNewest));
        Assert.Null(fromNew[1].ToUtc);
        Assert.True(fromOld[0].IsThis);
        var single = await PublicationTesting.PublishedRigAsync(mongo);
        Assert.Empty(await PassportChains.ForAsync(single.Store, (await single.Store.GetAsync<BatteryPack>(single.PackId))!)); // one passport: no list
    }

    [Fact]
    public async Task ADraftNewPassport_ShouldShowOnlyToTheTeam()
    {
        var rig = await new SuccessionTests(mongo).RepurposedAsync();
        var (successorId, _) = await SuccessionTests.Service(rig).CreateSuccessorAsync(PublicationTesting.Team, rig.PackId, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.OperatorId);
        var links = await PassportChains.ForAsync(rig.Store, (await rig.Store.GetAsync<BatteryPack>(successorId!))!);

        Assert.Equal(2, new ChainView(links, ChainReader.Team).Shown.Count);
        Assert.Single(new ChainView(links, ChainReader.ClusterAdmin).Shown); // a draft has no public facts
        Assert.Single(new ChainView(links, ChainReader.Detail).Shown);
    }

    [Fact]
    public async Task TheDetailPage_ShouldShowTheChainToTheTeamAndMarketSurveillanceOnly()
    {
        var rig = await new SuccessionTests(mongo).InactiveAsync();
        var newId = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Succession!.SuccessorPackId!;
        var id = (await rig.Store.GetAsync<BatteryPack>(newId))!.Values["uniqueProductIdentifier"].Value!.GetValue<string>();
        var pages = PassportPagesTests.Pages(rig);

        var team = (await pages.DetailAsync(Reader.With("team@example.com", ReaderRole.Team), id))!;
        var surveillance = (await pages.DetailAsync(Reader.With("msa@example.com", ReaderRole.MarketSurveillance), id))!;
        var member = (await pages.DetailAsync(Reader.With("user", ReaderRole.ClusterUser, BatteryTesting.North), id))!;
        var anyone = (await pages.SummaryAsync(Reader.Anonymous, id))!;

        Assert.Equal(2, team.Chain.Count);
        Assert.Equal(2, surveillance.Chain.Count);
        Assert.Empty(member.Chain);
        Assert.Empty(anyone.Chain); // public readers keep "replaces an earlier passport"
    }

    [Fact]
    public void EachReader_ShouldGetItsOwnLinks_AndAClusterAdminOnlyPublicFacts()
    {
        var chain = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_PassportChain.cshtml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var clusterPart = chain[chain.IndexOf("default:", StringComparison.Ordinal)..chain.IndexOf("}\n                    </span>", StringComparison.Ordinal)];

        Assert.Contains("<a href=\"@PassportPages.SummaryHref(link.Identifier)\">Public page</a>", clusterPart, StringComparison.Ordinal);
        Assert.DoesNotContain("audit", clusterPart, StringComparison.Ordinal);
        Assert.DoesNotContain("BatteryHref", clusterPart, StringComparison.Ordinal);
        Assert.DoesNotContain("Cluster", typeof(ChainLink).GetProperties().Select(property => property.Name).Aggregate((a, b) => a + b), StringComparison.Ordinal); // a link knows no cluster at all
        Assert.Contains("@await Html.PartialAsync(\"_PassportChain\", new ChainView(Model.Chain, ChainReader.ClusterAdmin))", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml")), StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_PassportChain\", new ChainView(Model.Chain, ChainReader.Team))", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml")), StringComparison.Ordinal);
        Assert.Contains("new ChainView(Model.Chain, ChainReader.Detail)", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml")), StringComparison.Ordinal);
    }
}
