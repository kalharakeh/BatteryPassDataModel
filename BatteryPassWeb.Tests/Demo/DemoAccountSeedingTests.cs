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
