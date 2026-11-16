using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding W8: deleting a cluster revokes its tokens (they answer as an unknown token from the next request, whatever
/// is made later with the same cluster ID) and cancels the hand-overs waiting for it or from it, in the same action,
/// and the audit entry says how many of each.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ClusterDeletionTests(MongoFixture mongo)
{
    private static async Task<(string Plain, string Id)> TokenAsync(AccountRig rig, string name, params string[] clusterIds)
    {
        var (document, plain) = await rig.Services.GetRequiredService<ExternalApiRepository>()
            .CreateTokenAsync(name, ExternalTokenAccessMode.ReadWrite, clusterIds, false, false, "team");
        return (plain, document["tokenId"].AsString);
    }

    private static async Task<bool> WorksAsync(AccountRig rig, string plain) =>
        (await rig.Services.GetRequiredService<ExternalApiRepository>().ValidateTokenAsync(plain, ExternalTokenRequirement.Read)).Success;

    private static async Task<HandOver> HandOverAsync(AccountRig rig, string from, string to)
    {
        var handOver = new HandOver
        {
            Id = $"handover-{Guid.NewGuid():N}",
            PackId = "pack-" + Guid.NewGuid().ToString("N"),
            FromClusterId = from,
            ToClusterId = to,
            State = HandOverStates.Waiting,
            AskedBy = "north.admin@example.test",
            AskedAtUtc = DateTime.UtcNow,
        };
        await rig.Services.GetRequiredService<DppStore>().InsertAsync(handOver);
        return handOver;
    }

    private static async Task<string> StateAsync(AccountRig rig, HandOver handOver) =>
        (await rig.Services.GetRequiredService<DppStore>().GetAsync<HandOver>(handOver.Id))!.State;

    private static async Task<BsonDocument> DeletedEntryAsync(AccountRig rig, string clusterId) =>
        await rig.Database.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", "cluster.deleted") & Builders<BsonDocument>.Filter.Eq("entityId", clusterId))
            .SingleAsync();

    private static async Task MakeEmptyClusterAsync(AccountRig rig, string clusterId) =>
        await rig.Clusters.UpsertClusterAsync(clusterId, "Customer " + clusterId);

    [Fact]
    public async Task Deleting_ShouldRevokeTheTokensAtOnce_AndCancelTheWaitingHandOvers_AndCountBothInTheAuditEntry()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await MakeEmptyClusterAsync(rig, "gone");
        var (onlyGone, _) = await TokenAsync(rig, "Gone reader", "gone");
        var (both, bothId) = await TokenAsync(rig, "Gone and north", "Gone", "north");
        var (northOnly, _) = await TokenAsync(rig, "North reader", "north");
        var toGone = await HandOverAsync(rig, "north", "gone");
        var fromGone = await HandOverAsync(rig, "GONE", "north");
        var unrelated = await HandOverAsync(rig, "north", "south");
        Assert.True(await WorksAsync(rig, onlyGone));

        await AccountRig.Form(rig.TeamMember(), ("clusterId", "gone")).DeleteCluster(CancellationToken.None);

        Assert.False(await WorksAsync(rig, onlyGone));
        Assert.True(await WorksAsync(rig, both)); // it still has another cluster
        Assert.True(await WorksAsync(rig, northOnly));
        Assert.Equal(["north"], (await rig.Services.GetRequiredService<ExternalApiRepository>().GetTokenByIdAsync(bothId))!["clusterIds"].AsBsonArray.Select(id => id.AsString));
        Assert.Equal(HandOverStates.Cancelled, await StateAsync(rig, toGone));
        Assert.Equal(HandOverStates.Cancelled, await StateAsync(rig, fromGone));
        Assert.Equal(HandOverStates.Waiting, await StateAsync(rig, unrelated));
        var metadata = (await DeletedEntryAsync(rig, "gone"))["metadata"].AsBsonDocument;
        Assert.Equal(1, metadata["tokensRevoked"].ToInt32());
        Assert.Equal(1, metadata["tokensNarrowed"].ToInt32());
        Assert.Equal(2, metadata["handOversCancelled"].ToInt32());
    }

    [Fact]
    public async Task ARevokedToken_ShouldNotWorkAgain_WhenTheClusterIdIsUsedAgain()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await MakeEmptyClusterAsync(rig, "gone");
        var (plain, tokenId) = await TokenAsync(rig, "Gone reader", "gone");
        await AccountRig.Form(rig.TeamMember(), ("clusterId", "gone")).DeleteCluster(CancellationToken.None);

        await MakeEmptyClusterAsync(rig, "gone");
        // Even switched on again by hand, it is gone: it was deleted, not just switched off.
        await rig.Services.GetRequiredService<ExternalApiRepository>().SetTokenActiveAsync(tokenId, true, "team");

        Assert.False(await WorksAsync(rig, plain));
        var answer = await rig.Services.GetRequiredService<ExternalApiRepository>().ValidateTokenAsync(plain, ExternalTokenRequirement.Read);
        Assert.Equal("Token is invalid.", answer.Message);
    }

    [Fact]
    public async Task ForceDeleting_ShouldDoTheSame()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await MakeEmptyClusterAsync(rig, "gone");
        var (plain, _) = await TokenAsync(rig, "Gone reader", "gone");
        var toGone = await HandOverAsync(rig, "north", "gone");

        await AccountRig.Form(rig.TeamMember(), ("clusterId", "gone"), ("confirmation", "I understand this deletes linked cluster data")).ForceDeleteClusterAsync(CancellationToken.None);

        Assert.False(await WorksAsync(rig, plain));
        Assert.Equal(HandOverStates.Cancelled, await StateAsync(rig, toGone));
        var metadata = (await DeletedEntryAsync(rig, "gone"))["metadata"].AsBsonDocument;
        Assert.Equal(1, metadata["tokensRevoked"].ToInt32());
        Assert.Equal(1, metadata["handOversCancelled"].ToInt32());
    }

    [Fact]
    public async Task ARefusedDelete_ShouldTouchNeitherTokensNorHandOvers()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var (plain, _) = await TokenAsync(rig, "North reader", "north"); // north has members, so a plain delete is refused
        var toNorth = await HandOverAsync(rig, "south", "north");

        await AccountRig.Form(rig.TeamMember(), ("clusterId", "north")).DeleteCluster(CancellationToken.None);

        Assert.True(await WorksAsync(rig, plain));
        Assert.Equal(HandOverStates.Waiting, await StateAsync(rig, toNorth));
    }

    [Fact]
    public void TheDeleteButtons_ShouldSayWhatDeletingDoesBeforeAsking()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("API tokens stop working at once", page, StringComparison.Ordinal);
        Assert.Contains("hand-overs waiting for it or from it are cancelled", page, StringComparison.Ordinal);
    }
}
