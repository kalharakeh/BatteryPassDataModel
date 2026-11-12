using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class EditRightsTests
{
    private const string OwnCluster = "cluster-test";

    [Theory]
    [InlineData("team", 11, null, true)] // pack data at creation
    [InlineData("team", 43, null, true)] // shared data: the team only (decision 10)
    [InlineData("team", 63, OwnCluster, true)]
    [InlineData("team", 6, OwnCluster, false)] // issued by the app
    [InlineData("cluster-admin", 63, OwnCluster, true)] // purchaser data (role rule)
    [InlineData("cluster-admin", 92, OwnCluster, true)]
    [InlineData("cluster-admin", 22, OwnCluster, true)]
    [InlineData("cluster-admin", 59, OwnCluster, true)]
    [InlineData("cluster-admin", 11, OwnCluster, false)] // not purchaser data
    [InlineData("cluster-admin", 43, OwnCluster, false)]
    [InlineData("cluster-admin", 63, "another-cluster", false)] // not their battery
    [InlineData("cluster-admin", 59, null, false)] // a model record has no cluster
    [InlineData("feed", 63, OwnCluster, true)] // live values only (J-10)
    [InlineData("feed", 83, OwnCluster, true)]
    [InlineData("feed", 87, OwnCluster, true)]
    [InlineData("feed", 92, OwnCluster, false)]
    [InlineData("feed", 22, OwnCluster, false)]
    [InlineData("feed", 63, "another-cluster", false)]
    public void MayEdit_ShouldFollowTheCatalogueAndTheBatterysCluster(string sender, int number, string? packClusterId, bool expected)
    {
        var who = sender switch
        {
            "team" => ChangeSender.Team("team@example.com"),
            "cluster-admin" => ChangeSender.ClusterAdmin("admin@example.com", OwnCluster),
            _ => ChangeSender.Feed("bms-feed", OwnCluster),
        };

        Assert.Equal(expected, EditRights.MayEdit(who, AttributeCatalog.ByNumber(number), packClusterId));
    }
}
