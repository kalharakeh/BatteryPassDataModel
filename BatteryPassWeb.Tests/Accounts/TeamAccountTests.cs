using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding S04: the team's "Add user" refuses an address that has an account (it is changed from its row), the last team
/// account keeps its access and its account, and nobody lowers or deletes their own team account.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class TeamAccountTests(MongoFixture mongo)
{
    [Theory]
    [InlineData(AccountRig.NorthUser, AccessControlService.RoleNormalUser, "North User")]
    [InlineData(AccountRig.Team2, AccessControlService.RoleAdmin, "Team Two")]
    public async Task AddUser_ShouldRefuseAnAddressThatHasAnAccount(string email, string role, string name)
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(),
            ("email", email.ToUpperInvariant()), ("name", "Changed"), ("systemRole", "member"), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed));

        await team.SaveUser(CancellationToken.None);

        Assert.Equal(AdminController.AlreadyHasAnAccount, team.TempData["ErrorMessage"]);
        Assert.Equal([role], await rig.RolesOfAsync(email));
        Assert.Equal(name, await rig.NameOfAsync(email));
        Assert.True(await rig.SignsInAsync(email, AccountRig.Password));
    }

    [Fact]
    public async Task TheRows_ShouldStillChangeAnAccount()
    {
        using var rig = await AccountRig.StartAsync(mongo);

        await AccountRig.Form(rig.TeamMember(), ("change", "access"), ("email", AccountRig.Team2), ("name", "Former Team"), ("systemRole", "member")).SaveUser(CancellationToken.None);
        await AccountRig.Form(rig.TeamMember(), ("change", "password"), ("email", AccountRig.NorthUser), ("password", AccountRig.Typed), ("passwordConfirmation", AccountRig.Typed)).SaveUser(CancellationToken.None);

        Assert.Equal([AccessControlService.RoleNormalUser], await rig.RolesOfAsync(AccountRig.Team2));
        Assert.Equal("Former Team", await rig.NameOfAsync(AccountRig.Team2));
        Assert.True(await rig.SignsInAsync(AccountRig.NorthUser, AccountRig.Typed));
    }

    [Fact]
    public async Task YourOwnTeamAccount_ShouldNotBeLowered()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var team = AccountRig.Form(rig.TeamMember(AccountRig.Team), ("change", "access"), ("email", AccountRig.Team), ("name", "Team"), ("systemRole", "member"));

        await team.SaveUser(CancellationToken.None);

        Assert.Equal(AdminController.NotYourOwnAccess, team.TempData["ErrorMessage"]);
        Assert.Equal([AccessControlService.RoleAdmin], await rig.RolesOfAsync(AccountRig.Team));
    }

    [Fact]
    public async Task YourOwnTeamAccount_ShouldStillTakeANewNameAndPassword()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await AccountRig.Form(rig.TeamMember(AccountRig.Team), ("change", "access"), ("email", AccountRig.Team), ("name", "Renamed"), ("systemRole", "admin")).SaveUser(CancellationToken.None);

        Assert.Equal("Renamed", await rig.NameOfAsync(AccountRig.Team));
        Assert.Equal([AccessControlService.RoleAdmin], await rig.RolesOfAsync(AccountRig.Team));
    }

    [Fact]
    public async Task TheLastTeamAccount_ShouldKeepItsAccess_EvenForTheFallbackAdmin()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.Clusters.DeleteUserAsync(AccountRig.Team2);
        var fallback = AccountRig.With(rig.TeamMember(), AccountRig.Person(AccountRig.FallbackEmail, AccessControlService.RoleAdmin));
        AccountRig.Form(fallback, ("change", "access"), ("email", AccountRig.Team), ("name", "Team"), ("systemRole", AccessControlService.RoleMarketSurveillanceAuthority));

        await fallback.SaveUser(CancellationToken.None);

        Assert.Equal(AdminController.LastTeamAccount, fallback.TempData["ErrorMessage"]);
        Assert.Equal([AccessControlService.RoleAdmin], await rig.RolesOfAsync(AccountRig.Team));
    }

    [Fact]
    public async Task ADemoAccountWithTheTeamRole_ShouldNotCountAsATeamAccount()
    {
        // A demo account never acts as the team (finding R03), so it can't keep the team reachable either.
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.Database.GetCollection<MongoDB.Bson.BsonDocument>("users").UpdateOneAsync(
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("email", AccountRig.Team2),
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set(ClusterRepository.DemoMark, true));
        var fallback = AccountRig.With(rig.TeamMember(), AccountRig.Person(AccountRig.FallbackEmail, AccessControlService.RoleAdmin));
        AccountRig.Form(fallback, ("change", "access"), ("email", AccountRig.Team), ("name", "Team"), ("systemRole", "member"));

        await fallback.SaveUser(CancellationToken.None);

        Assert.Equal(AdminController.LastTeamAccount, fallback.TempData["ErrorMessage"]);
        Assert.Equal([AccessControlService.RoleAdmin], await rig.RolesOfAsync(AccountRig.Team));
    }

    [Fact]
    public async Task TheLastTeamAccount_ShouldNotBeDeleted_EvenByTheFallbackAdmin()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        await rig.Clusters.DeleteUserAsync(AccountRig.Team2);
        var fallback = AccountRig.With(rig.TeamMember(), AccountRig.Person(AccountRig.FallbackEmail, AccessControlService.RoleAdmin));
        AccountRig.Form(fallback, ("email", AccountRig.Team));

        await fallback.DeleteAccount(CancellationToken.None);

        Assert.NotNull(await rig.Clusters.GetUserByEmailAsync(AccountRig.Team));
    }

    [Fact]
    public void TheUsersTab_ShouldSayWhatEachFormChanges()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

        Assert.Contains("<input type=\"hidden\" name=\"change\" value=\"access\" />", page, StringComparison.Ordinal);
        Assert.Contains("<input type=\"hidden\" name=\"change\" value=\"password\" />", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Typing the email of an existing user updates that user.", page, StringComparison.Ordinal);
    }
}
