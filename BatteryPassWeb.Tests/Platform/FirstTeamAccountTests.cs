using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Accounts;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Platform;

/// <summary>
/// Decision 78: on a database with no team account at all, FIRST_TEAM_ACCOUNT_EMAIL makes one team account for that
/// address, with no usable password and one audit entry; the person then uses "Forgot password". With a team account, or
/// an empty setting, nothing happens. Two instances starting together make one account.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class FirstTeamAccountTests(MongoFixture mongo)
{
    private const string Address = "first.person@example.test";

    private static FirstTeamAccount StartUp(MongoContext context) => new(context, new ApplicationAuditService(context));

    private static IMongoCollection<BsonDocument> Users(MongoContext context) => context.Database!.GetCollection<BsonDocument>("users");

    private static Task<List<BsonDocument>> Audit(MongoContext context) =>
        context.Database!.GetCollection<BsonDocument>("applicationAuditEvents").Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();

    private static async Task<long> UserCount(MongoContext context) =>
        await Users(context).CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty);

    private static AuthService AuthOn(MongoContext context, AccountRig.CapturingEmailSender mail) =>
        new(context, Options.Create(new BatteryPassOptions()), mail);

    [Fact]
    public async Task EmptyDatabase_ShouldGetOneTeamAccountWithNoPassword_AndOneAuditEntry()
    {
        var context = mongo.NewContext();

        var result = await StartUp(context).RunAsync($"  {Address.ToUpperInvariant()} ");

        Assert.Equal(FirstTeamAccountOutcome.Created, result.Outcome);
        var users = await Users(context).Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();
        var user = Assert.Single(users);
        Assert.Equal(Address, user["email"].AsString);
        Assert.Equal([AccessControlService.RoleAdmin], user["roles"].AsBsonArray.Select(role => role.AsString));
        Assert.False(user.Contains("passwordHash"));
        var audit = Assert.Single(await Audit(context));
        Assert.Equal("account.firstTeamAccountCreated", audit["eventType"].AsString);
        Assert.Equal(Address, audit["entityId"].AsString);
        Assert.DoesNotContain("hash", audit.ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheNewAccount_ShouldHaveNoPasswordThatWorks_AndForgotPasswordShouldMailATemporaryOne()
    {
        var context = mongo.NewContext();
        await StartUp(context).RunAsync(Address);
        var mail = new AccountRig.CapturingEmailSender();
        var auth = AuthOn(context, mail);

        foreach (var guess in new[] { "password", "an empty hash must not match", "x" })
        {
            Assert.Equal(LoginAuthenticationStatus.Invalid, (await auth.AuthenticateLoginAsync(Address, guess)).Status);
        }

        await auth.CreatePasswordResetAsync(Address, "127.0.0.1", string.Empty);

        Assert.NotEmpty(mail.LastTemporaryPassword);
        var signIn = await auth.AuthenticateLoginAsync(Address, mail.LastTemporaryPassword);
        Assert.Equal(LoginAuthenticationStatus.RequiresTemporaryPasswordChange, signIn.Status);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    public async Task ATeamAccount_ShouldMakeTheSettingDoNothing(string role)
    {
        var context = mongo.NewContext();
        await Users(context).InsertOneAsync(new BsonDocument { ["email"] = "team@example.test", ["name"] = "Team", ["roles"] = new BsonArray { role } });

        var result = await StartUp(context).RunAsync(Address);

        Assert.Equal(FirstTeamAccountOutcome.TeamAccountExists, result.Outcome);
        Assert.Equal(1, await UserCount(context));
        Assert.Empty(await Audit(context));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not an address")]
    [InlineData("two@example.test, three@example.test")]
    [InlineData("Name <someone@example.test>")]
    public async Task AnEmptyOrUnusableSetting_ShouldCreateNothing(string? setting)
    {
        var context = mongo.NewContext();

        var result = await StartUp(context).RunAsync(setting);

        Assert.True(result.Outcome is FirstTeamAccountOutcome.NotSet or FirstTeamAccountOutcome.NotAnAddress);
        Assert.Equal(0, await UserCount(context));
        Assert.Empty(await Audit(context));
    }

    [Fact]
    public async Task AnotherStart_ShouldDoNothingMore_WhateverTheSettingStillHolds()
    {
        var context = mongo.NewContext();
        await StartUp(context).RunAsync(Address);

        var again = await StartUp(context).RunAsync(Address);
        var other = await StartUp(context).RunAsync("someone.else@example.test");

        Assert.Equal(FirstTeamAccountOutcome.TeamAccountExists, again.Outcome);
        Assert.Equal(FirstTeamAccountOutcome.TeamAccountExists, other.Outcome);
        Assert.Equal(1, await UserCount(context));
        Assert.Single(await Audit(context));
    }

    [Fact]
    public async Task AnAccountThatIsNotOnTheTeam_ShouldKeepItsAccess_WhenItsAddressIsTheSetting()
    {
        var context = mongo.NewContext();
        await Users(context).InsertOneAsync(new BsonDocument
        {
            ["email"] = Address, ["name"] = "Member", ["roles"] = new BsonArray { AccessControlService.RoleNormalUser }, ["passwordHash"] = "kept",
        });

        var result = await StartUp(context).RunAsync(Address.ToUpperInvariant());

        Assert.Equal(FirstTeamAccountOutcome.AddressInUse, result.Outcome);
        var user = Assert.Single(await Users(context).Find(Builders<BsonDocument>.Filter.Empty).ToListAsync());
        Assert.Equal([AccessControlService.RoleNormalUser], user["roles"].AsBsonArray.Select(role => role.AsString));
        Assert.Equal("kept", user["passwordHash"].AsString);
        Assert.Empty(await Audit(context));
    }

    [Fact]
    public async Task InstancesStartingAtTheSameMoment_ShouldMakeOneAccountAndOneAuditEntry()
    {
        for (var round = 0; round < 5; round++)
        {
            var context = mongo.NewContext();
            var instances = Enumerable.Range(0, 8).Select(_ => StartUp(context)).ToList();
            var gate = new TaskCompletionSource();

            var runs = instances.Select(async instance =>
            {
                await gate.Task;
                return await instance.RunAsync(Address);
            }).ToList();
            gate.SetResult();
            var results = await Task.WhenAll(runs);

            Assert.Equal(1, results.Count(result => result.Outcome == FirstTeamAccountOutcome.Created));
            Assert.All(
                results.Where(result => result.Outcome != FirstTeamAccountOutcome.Created),
                result => Assert.Equal(FirstTeamAccountOutcome.TeamAccountExists, result.Outcome));
            Assert.Equal(1, await UserCount(context));
            Assert.Single(await Audit(context));
        }
    }

    [Fact]
    public void TheLogLines_ShouldNameTheAddressOnly_AndSayNothingWhenThereIsNothingToSay()
    {
        var created = new FirstTeamAccountResult(FirstTeamAccountOutcome.Created, Address).LogLine;
        var ignored = new FirstTeamAccountResult(FirstTeamAccountOutcome.TeamAccountExists, Address).LogLine;

        Assert.Equal($"The first team account was created for {Address}. It has no password: the person uses \"Forgot password\" on the sign-in page.", created);
        Assert.Equal("FIRST_TEAM_ACCOUNT_EMAIL is ignored: a team account exists.", ignored);
        Assert.Null(new FirstTeamAccountResult(FirstTeamAccountOutcome.NotSet, string.Empty).LogLine);
        Assert.NotNull(new FirstTeamAccountResult(FirstTeamAccountOutcome.NotAnAddress, string.Empty).LogLine);
        Assert.Contains("Use another address, or have that account deleted first.", new FirstTeamAccountResult(FirstTeamAccountOutcome.AddressInUse, Address).LogLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramCs_ShouldRunTheStepAtStartUp_FromTheSetting()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("FirstTeamAccount.Setting", program, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<FirstTeamAccount>", program, StringComparison.Ordinal);
        Assert.Contains(".RunAsync(", program[program.IndexOf("GetRequiredService<FirstTeamAccount>()", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }
}
