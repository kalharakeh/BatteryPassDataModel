using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03, the manager's points 1 and 4: a demo account's or demo token's roles and memberships count only on demo
/// batteries; on a real battery it is a visitor. One rule (<see cref="DemoScope"/>) where the reader is built and met
/// with a battery; this table runs it over every demo role and both kinds of battery.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoScopeTests(MongoFixture mongo)
{
    public static TheoryData<ReaderRole> EveryRole() => new(Enum.GetValues<ReaderRole>());

    private static Reader Demo(ReaderRole role) => Reader.With("demo.someone@example.test", role, "demo-north") with { DemoOnly = true };

    [Theory]
    [MemberData(nameof(EveryRole))]
    public void OnARealBattery_ADemoReaderShouldSeeWhatAVisitorSees(ReaderRole role)
    {
        var onReal = DemoScope.On(Demo(role), batteryIsDemo: false);

        Assert.Empty(onReal.Roles);
        Assert.Empty(onReal.ClusterIds);
        foreach (var attribute in AttributeCatalog.All)
        {
            Assert.Equal(AudienceRules.MaySee(Reader.Anonymous, attribute, ["demo-north"]), AudienceRules.MaySee(onReal, attribute, ["demo-north"]));
        }

        Assert.False(AudienceRules.MaySeePurchaserData(onReal, ["demo-north"]));
        Assert.False(DemoScope.Reaches(Demo(role), batteryIsDemo: false));
    }

    [Theory]
    [MemberData(nameof(EveryRole))]
    public void OnADemoBattery_ADemoReaderShouldKeepItsRole(ReaderRole role)
    {
        var real = Reader.With("someone@example.test", role, "demo-north");
        var onDemo = DemoScope.On(Demo(role), batteryIsDemo: true);

        Assert.True(DemoScope.Reaches(Demo(role), batteryIsDemo: true));
        foreach (var attribute in AttributeCatalog.All)
        {
            Assert.Equal(AudienceRules.MaySee(real, attribute, ["demo-north"]), AudienceRules.MaySee(onDemo, attribute, ["demo-north"]));
        }
    }

    [Theory]
    [MemberData(nameof(EveryRole))]
    public void AReaderThatIsNotDemo_ShouldKeepItsRoleOnEveryBattery(ReaderRole role)
    {
        var real = Reader.With("someone@example.test", role, "north");

        Assert.Same(real, DemoScope.On(real, batteryIsDemo: false));
        Assert.Same(real, DemoScope.On(real, batteryIsDemo: true));
    }

    [Fact]
    public async Task ADemoAccountsReader_ShouldBeDemoOnly_AndARealOnesNot()
    {
        var context = mongo.NewContext();
        var clusters = new ClusterRepository(context);
        await clusters.UpsertUserAsync("demo.msa@example.test", "Demo", [AccessControlService.RoleMarketSurveillanceAuthority], "hash");
        await context.Database!.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("email", "demo.msa@example.test"), Builders<BsonDocument>.Update.Set(ClusterRepository.DemoMark, true));
        await clusters.UpsertUserAsync("msa@example.test", "Real", [AccessControlService.RoleMarketSurveillanceAuthority], "hash");
        var readers = new DppReaders(clusters);

        var demo = await readers.ForAsync(Accounts.AccountRig.Person("demo.msa@example.test", AccessControlService.RoleMarketSurveillanceAuthority));
        var real = await readers.ForAsync(Accounts.AccountRig.Person("msa@example.test", AccessControlService.RoleMarketSurveillanceAuthority));

        Assert.True(demo.DemoOnly);
        Assert.False(real.DemoOnly);
        Assert.Contains(ReaderRole.MarketSurveillance, demo.Roles);
    }

    [Fact]
    public void ADemoTokensReader_ShouldBeDemoOnly()
    {
        var demoTeam = DppReaders.For(new ExternalApiTokenContext { TokenId = "t1", GlobalAccess = true, IsDemo = true });
        var realTeam = DppReaders.For(new ExternalApiTokenContext { TokenId = "t2", GlobalAccess = true });

        Assert.True(demoTeam.DemoOnly);
        Assert.False(realTeam.DemoOnly);
    }
}
