using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Accounts;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>
/// Finding R03, the manager's point 5: ENABLE_DEMO_DATA. Outside Production it is on unless set to false; in Production
/// it is off unless set to true. Off: no reset, no demo sign-in, no demo token; clearing still works.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoSwitchTests(MongoFixture mongo)
{
    [Theory]
    [InlineData(null, false, true)]
    [InlineData("false", false, false)]
    [InlineData("no", false, false)]
    [InlineData(null, true, false)]
    [InlineData("false", true, false)]
    [InlineData("true", true, true)]
    public void TheSetting_ShouldBeOnOutsideProductionAndOffInProduction_UnlessSet(string? value, bool isProduction, bool on) =>
        Assert.Equal(on, DemoDataSwitch.Parse(value, isProduction));

    [Fact]
    public void ProgramCs_ShouldReadTheSetting()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("\"ENABLE_DEMO_DATA\"", program, StringComparison.Ordinal);
        Assert.Contains("DemoDataSwitch.Parse(", program, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Off_TheResetShouldRefuse_AndClearingShouldStillWork()
    {
        using var on = await DemoRig.StartAsync(mongo);
        await on.Demo.SeedAsync(DemoRig.Who);
        using var off = DemoRig.Over(on, demoData: false);

        var reset = await off.Demo.ResetAsync(DemoRig.Who);
        Assert.Equal([DemoDataSwitch.Off], reset.Notes);
        Assert.True(await off.Demo.HasDemoDataAsync());

        await off.Demo.ClearAsync(DemoRig.Who);
        Assert.False(await off.Demo.HasDemoDataAsync());
        Assert.Equal([DemoDataSwitch.Off], (await off.Demo.SeedAsync(DemoRig.Who)).Notes);
    }

    [Fact]
    public async Task Off_TheResetActionShouldRefuse_AndTheCardShouldSaySo()
    {
        using var on = await DemoRig.StartAsync(mongo);
        using var off = DemoRig.Over(on, demoData: false);
        var controller = DemoCardTests.Controller(off);

        await controller.ResetDemoData();

        Assert.Equal(DemoDataSwitch.Off, controller.TempData["ErrorMessage"]);
        Assert.Equal(0, await off.Count(DppStore.Packs));
        var card = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        Assert.Contains("DemoDataSwitch.Off", card, StringComparison.Ordinal);
        Assert.Contains("ViewData[DemoDataSwitch.ViewDataKey]", card, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Off_ADemoAccountShouldNotSignIn_NorKeepItsSession()
    {
        using var on = await AccountRig.StartAsync(mongo);
        await on.AccountAsync("demo.someone@example.test", "Demo someone", AccessControlService.RoleMarketSurveillanceAuthority);
        await on.Database.GetCollection<BsonDocument>("users").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("email", "demo.someone@example.test"), Builders<BsonDocument>.Update.Set(DemoDataService.AccountMark, true));
        var session = (await on.Auth.AuthenticateLoginAsync("demo.someone@example.test", AccountRig.Password)).Principal!;
        Assert.True(await on.SignsInAsync(AccountRig.NorthUser, AccountRig.Password));

        var off = new AuthService(on.Services.GetRequiredService<MongoContext>(), on.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions>>(),
            on.Services.GetRequiredService<IEmailSender>(), new DemoDataSwitch(false));

        Assert.Equal(LoginAuthenticationStatus.Invalid, (await off.AuthenticateLoginAsync("demo.someone@example.test", AccountRig.Password)).Status);
        Assert.True((await off.CheckSessionAsync(session)).SignOut);
        Assert.Equal(LoginAuthenticationStatus.Authenticated, (await off.AuthenticateLoginAsync(AccountRig.NorthUser, AccountRig.Password)).Status);
    }

    [Fact]
    public async Task Off_ADemoTokenShouldBeRefused()
    {
        using var on = await DemoRig.StartAsync(mongo);
        var tokens = (await on.Demo.SeedAsync(DemoRig.Who)).Tokens;
        var team = tokens.Single(token => token.Label == "Team, all clusters").Value;
        using var off = DemoRig.Over(on, demoData: false);

        var accepted = await on.Services.GetRequiredService<ExternalApiRepository>().ValidateTokenAsync(team, ExternalTokenRequirement.Read);
        var refused = await off.Services.GetRequiredService<ExternalApiRepository>().ValidateTokenAsync(team, ExternalTokenRequirement.Read);

        Assert.True(accepted.Success);
        Assert.True(accepted.Context!.IsDemo);
        Assert.False(refused.Success);
        Assert.Equal(DemoDataSwitch.TokenOff, refused.Message);
    }
}
