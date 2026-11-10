using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding R03, the manager's point 2: a demo cluster admin's user forms reach only demo clusters, and an account they
/// make is a demo account (cleared with the demo data). They don't pull real accounts into the demo either.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoClusterAdminTests(MongoFixture mongo)
{
    private const string DemoAdmin = "demo.admin@example.test";

    /// <summary>A demo cluster with a demo admin, whom a real admin has also made admin of the real cluster north.</summary>
    private static async Task<AccountRig> RigAsync(MongoFixture mongo)
    {
        var rig = await AccountRig.StartAsync(mongo);
        await rig.Clusters.UpsertClusterAsync("demo-x", "Demo X");
        await rig.Database.GetCollection<BsonDocument>("clusters").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("clusterId", "demo-x"), Builders<BsonDocument>.Update.Set(DemoDataService.AccountMark, true));
        await rig.AccountAsync(DemoAdmin, "Demo admin", AccessControlService.RoleNormalUser, "demo-x", AccessControlService.RoleClusterAdmin);
        await rig.Clusters.UpsertClusterMembershipAsync(DemoAdmin, "north", AccessControlService.RoleClusterAdmin);
        await rig.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("email", DemoAdmin), Builders<BsonDocument>.Update.Set(DemoDataService.AccountMark, true));
        return rig;
    }

    [Fact]
    public async Task ADemoClusterAdmin_ShouldAdministerOnlyDemoClusters()
    {
        using var rig = await RigAsync(mongo);

        Assert.Equal(["demo-x"], await rig.Access.GetAdministeredClusterIdsForUserAsync(AccountRig.Person(DemoAdmin, AccessControlService.RoleClusterAdmin)));
        var intoNorth = AccountRig.Form(rig.ClusterAdmin(DemoAdmin),
            ("email", "someone.new@example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north"));
        Assert.IsType<NotFoundResult>(await intoNorth.SaveUser(CancellationToken.None));
        Assert.Null(await rig.Clusters.GetUserByEmailAsync("someone.new@example.test"));
    }

    [Fact]
    public async Task AnAccountADemoClusterAdminMakes_ShouldBeADemoAccount()
    {
        using var rig = await RigAsync(mongo);

        await AccountRig.Form(rig.ClusterAdmin(DemoAdmin),
            ("email", "tester.made@example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "demo-x")).SaveUser(CancellationToken.None);

        var made = await rig.Clusters.GetUserByEmailAsync("tester.made@example.test");
        Assert.True(made!.GetValue(DemoDataService.AccountMark, false).ToBoolean());
        var real = await AccountRig.Form(rig.ClusterAdmin(AccountRig.NorthAdmin),
            ("email", "real.made@example.test"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed), ("clusterId", "north")).SaveUser(CancellationToken.None);
        Assert.False((await rig.Clusters.GetUserByEmailAsync("real.made@example.test"))!.GetValue(DemoDataService.AccountMark, false).ToBoolean());
    }

    [Fact]
    public async Task ATokenADemoClusterAdminMakes_ShouldBeADemoToken()
    {
        using var rig = await RigAsync(mongo);
        var controller = rig.ClusterAdmin(DemoAdmin);
        controller.HttpContext.Request.Form = new Microsoft.AspNetCore.Http.FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["clusterIds"] = "demo-x",
            ["name"] = "Demo X feed",
            ["accessMode"] = "read",
        });

        await controller.CreateClusterApiToken(CancellationToken.None);

        var token = Assert.Single(await rig.Database.GetCollection<BsonDocument>("apiTokens").Find(Builders<BsonDocument>.Filter.Eq("name", "Demo X feed")).ToListAsync());
        Assert.True(token.GetValue(DemoDataService.AccountMark, false).ToBoolean());
    }

    [Fact]
    public async Task ADemoClusterAdmin_ShouldNotPullARealAccountIntoTheDemo()
    {
        using var rig = await RigAsync(mongo);
        var join = AccountRig.Form(rig.ClusterAdmin(DemoAdmin), ("email", AccountRig.SouthUser), ("clusterId", "demo-x"));

        await join.SaveUser(CancellationToken.None);

        Assert.Equal(AccessControlService.CantChangeThisAccount, join.TempData["ErrorMessage"]);
        Assert.Equal(["south"], await rig.ClustersOfAsync(AccountRig.SouthUser));
    }
}
