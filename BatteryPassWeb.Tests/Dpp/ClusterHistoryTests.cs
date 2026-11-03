using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R6, the owner's choice 9 (3 Nov 2026) with the managing session's privacy rules: the cluster admin's battery
/// page gets a read-only History of what happened while the battery was in their cluster, and its moves in and out. It
/// never shows more than their page does: no other cluster's name or ID, no team member's name, no value they may not
/// read (the authorities'), no mark reason. One battery moves from Customer North to Customer South to prove it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterHistoryTests(MongoFixture mongo)
{
    private const string NorthAdmin = "north-admin@example.com";
    private const string SouthAdmin = "south-admin@example.com";
    private const string Reason = "Demo: declared values don't match the test report";

    private static ClusterHistory History(PublicationRig rig) =>
        new(new ApplicationAuditService(rig.Context), new AuditLines(rig.Store, new DocumentStore(rig.Context), AttributeFormsTests.Forms, rig.Context));

    private static string Text(IReadOnlyList<AuditLine> lines) => string.Join("\n", lines.Select(line =>
        string.Concat(line.Sentence.Select(word => word.Text)) + (line.Change is null ? string.Empty : $" · {line.Change}")
        + string.Concat(line.Values.Select(row => $" | {row.Name}: {row.Before} -> {row.After}"))));

    private static async Task SaveAsync(PublicationRig rig, ChangeSender sender, string property, JsonNode value) =>
        Assert.True((await rig.Pipeline.SaveAsync(sender, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { [property] = value }, "form"))).Saved);

    [Fact]
    public async Task ABatteryMovedBetweenTwoClusters_ShouldShowEachClusterOnlyItsOwnTime()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var north = ChangeSender.ClusterAdmin(NorthAdmin, BatteryTesting.North);
        var south = ChangeSender.ClusterAdmin(SouthAdmin, BatteryTesting.South);
        var team = PublicationTesting.Team;

        await SaveAsync(rig, north, "stateOfChargeSoC", new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = 71 });
        await SaveAsync(rig, team, "thirdPartyServiceProvider", "Authorities-only provider name"); // #13, the authorities' value
        var handOvers = HandOverTests.Service(rig);
        Assert.Null((await handOvers.AskAsync(north, rig.PackId, BatteryTesting.South, confirm: true)).Problem);
        var waiting = (await rig.Store.FindWaitingHandOverAsync(rig.PackId))!;
        Assert.Null(await handOvers.AnswerAsync(south, waiting.Id, accept: true));
        await SaveAsync(rig, south, "stateOfChargeSoC", new JsonObject { ["percentUnit"] = "%", ["percentageValue"] = 42 });
        var marks = PassportMarkTests.Service(rig);
        Assert.Null(await marks.MarkInvalidAsync(team, rig.PackId, Reason));
        Assert.Null(await marks.UndoAsync(team, rig.PackId));

        var pack = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!;
        var forNorth = Text(await History(rig).ForAsync(pack, new HashSet<string> { BatteryTesting.North }));
        var forSouth = Text(await History(rig).ForAsync(pack, new HashSet<string> { BatteryTesting.South }));

        // North: its own change and the hand-over out; nothing after, nothing of South's.
        Assert.Contains(NorthAdmin, forNorth, StringComparison.Ordinal);
        Assert.Contains("71", forNorth, StringComparison.Ordinal);
        Assert.Contains("asked another cluster to accept the battery", forNorth, StringComparison.Ordinal);
        Assert.Contains("Another cluster accepted the battery's hand-over: it moved to another cluster", forNorth, StringComparison.Ordinal);
        Assert.DoesNotContain("42", forNorth, StringComparison.Ordinal);
        Assert.DoesNotContain("marked the passport invalid", forNorth, StringComparison.Ordinal);

        // South: the hand-over in, its own change, the mark and its undoing as facts; nothing before, nothing of North's.
        Assert.Contains("Another cluster asked your cluster to accept the battery", forSouth, StringComparison.Ordinal);
        Assert.Contains($"{SouthAdmin} accepted the battery's hand-over to your cluster", forSouth, StringComparison.Ordinal);
        Assert.Contains("42", forSouth, StringComparison.Ordinal);
        Assert.Contains("The manufacturer's team marked the passport invalid", forSouth, StringComparison.Ordinal);
        Assert.Contains("The manufacturer's team undid the passport's mark", forSouth, StringComparison.Ordinal);
        Assert.DoesNotContain("71", forSouth, StringComparison.Ordinal);

        foreach (var (history, other, otherAdmin) in new[] { (forNorth, BatteryTesting.South, SouthAdmin), (forSouth, BatteryTesting.North, NorthAdmin) })
        {
            Assert.DoesNotContain(other, history, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Customer North", history, StringComparison.Ordinal);
            Assert.DoesNotContain("Customer South", history, StringComparison.Ordinal);
            Assert.DoesNotContain(otherAdmin, history, StringComparison.Ordinal);
            Assert.DoesNotContain(team.Who, history, StringComparison.Ordinal); // the team by role, never by name
            Assert.DoesNotContain(Reason, history, StringComparison.Ordinal); // a mark's reason is the team's and market surveillance's
            Assert.DoesNotContain("Authorities-only provider name", history, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Others_ShouldBeNamedByTheirRole()
    {
        Assert.Equal("a@north", ClusterHistory.Who("a@north", nameof(SenderKind.ClusterAdmin)));
        Assert.Equal("Your battery system", ClusterHistory.Who("token north", nameof(SenderKind.BatterySystemFeed)));
        Assert.Equal("The manufacturer's team", ClusterHistory.Who("team@example.com", nameof(SenderKind.Team)));
        Assert.Equal("Market surveillance", ClusterHistory.Who("msa@example.com", nameof(SenderKind.MarketSurveillance)));
        Assert.Equal("The app", ClusterHistory.Who("system", string.Empty));
    }

    [Fact]
    public void TheClusterAdminsPage_ShouldHaveAHistorySection()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "Battery.cshtml"));
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ClusterAdmin", "_HistoryCard.cshtml"));

        Assert.Contains("@await Html.PartialAsync(\"~/Views/ClusterAdmin/_HistoryCard.cshtml\", Model.History)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("line.Facts", card, StringComparison.Ordinal); // never the team's details
        Assert.DoesNotContain("line.Technical", card, StringComparison.Ordinal);
    }
}
