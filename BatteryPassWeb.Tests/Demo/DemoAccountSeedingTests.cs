using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Finding R03, the manager's points 6 and 7: seeding never touches a real account that has a catalogue address (in any
/// capitals) and says so; the demo accounts it makes carry the creator mark of their demo cluster.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoAccountSeedingTests(MongoFixture mongo)
{
    private static FilterDefinition<BsonDocument> Like(string email) =>
        Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(email)}$", "i"));

    [Fact]
    public async Task ARealAccountWithACatalogueAddress_ShouldStayAsItIs_InAnyCapitals()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var hash = BCrypt.Net.BCrypt.HashPassword("a-real-person-password-1", 4);
        await rig.Database.GetCollection<BsonDocument>("users").InsertOneAsync(new BsonDocument
        {
            ["email"] = "Demo.MSA@Example.test",
            ["name"] = "A real person",
            ["roles"] = new BsonArray { AccessControlService.RoleNormalUser },
            ["passwordHash"] = hash,
        });

        var seeded = await rig.Demo.ResetAsync(DemoRig.Who);
        await rig.Demo.ResetAsync(DemoRig.Who);

        var accounts = await rig.All("users", Like("demo.msa@example.test"));
        var account = Assert.Single(accounts);
        Assert.Equal("A real person", account["name"].AsString);
        Assert.Equal(hash, account["passwordHash"].AsString);
        Assert.Equal(new BsonArray { AccessControlService.RoleNormalUser }, account["roles"].AsBsonArray);
        Assert.False(account.GetValue("isDemo", false).ToBoolean());
        Assert.Contains(seeded.Notes, note => note.Contains("demo.msa@example.test", StringComparison.OrdinalIgnoreCase) && note.Contains("stays as it is", StringComparison.Ordinal));
    }

    /// <summary>Finding R03, point 6b: the seeded hand-over and readings file never act under a real account's address.</summary>
    [Fact]
    public async Task TheSeededHandOverAndReadings_ShouldActUnderTheFixedDemoName_NotARealAccountsAddress()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        string[] admins = ["demo.north.admin@example.test", "demo.south.admin@example.test", "demo.fleet.admin@example.test"];
        foreach (var email in admins)
        {
            await rig.Database.GetCollection<BsonDocument>("users").InsertOneAsync(new BsonDocument
            {
                ["email"] = email,
                ["name"] = "A real person",
                ["roles"] = new BsonArray { AccessControlService.RoleNormalUser },
                ["passwordHash"] = "hash",
            });
        }

        await rig.Demo.SeedAsync(DemoRig.Who);

        var askedBy = (await rig.All(DppStore.HandOvers)).Select(handOver => handOver["AskedBy"].AsString).ToList();
        var fileFrom = (await rig.All(DppStore.Uploads)).Where(upload => upload["Channel"].AsString == "page").Select(upload => upload["Who"].AsString).ToList();
        Assert.NotEmpty(askedBy);
        Assert.NotEmpty(fileFrom);
        Assert.All(askedBy.Concat(fileFrom), who => Assert.Equal(DemoDataService.SeedActor, who));
    }

    [Fact]
    public async Task TheDemoClusterAccounts_ShouldCarryTheirClustersCreatorMark()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var user = Assert.Single(await rig.All("users", Like("demo.north.user@example.test")));
        var msa = Assert.Single(await rig.All("users", Like("demo.msa@example.test")));

        Assert.Equal("demo-north", user.GetValue(ClusterRepository.CreatedInClusterField, BsonNull.Value).ToString());
        Assert.False(msa.Contains(ClusterRepository.CreatedInClusterField));
    }
}
