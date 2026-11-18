using System.Net;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Finding U05: the team's "To do" page as the browser gets it, and who may open it (the role row: the team only).</summary>
[Collection(MongoCollection.Name)]
public sealed class ToDoPageViewTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheTeam_ShouldGetTheToDoPage_WithItsSixBoxesAndTheListOneClickAway()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());
        await site.Services.GetRequiredService<DemoDataService>().ResetAsync(DemoRig.Who);
        await AddTeamAsync(site);

        var (status, body, _) = await site.GetAsync("/admin/start", Team);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<h1>To do</h1>", body, StringComparison.Ordinal);
        Assert.Contains("<title>To do - Battery Passport</title>", body, StringComparison.Ordinal);
        Assert.Equal(6, body.Split("data-box=\"").Length - 1);
        Assert.Contains("href=\"/admin/batteries\">Open the battery list</a>", body, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\">To do</a>", body, StringComparison.Ordinal);
        Assert.Contains("Every saved change is public.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("style=\"", body[body.IndexOf("<main", StringComparison.Ordinal)..body.IndexOf("</main>", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("demo.north.admin@example.test")]
    [InlineData("demo.north.user@example.test")]
    [InlineData("demo.msa@example.test")]
    [InlineData("demo.notified-body@example.test")]
    [InlineData("demo.commission@example.test")]
    [InlineData("demo.repair@example.test")]
    [InlineData("demo.recycling@example.test")]
    [InlineData("demo.treatment@example.test")]
    public async Task EveryoneButTheTeam_ShouldBeKeptOut(string email)
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());
        await site.Services.GetRequiredService<DemoDataService>().ResetAsync(DemoRig.Who);

        var (status, _, location) = await site.GetAsync("/admin/start", email);

        Assert.Equal(HttpStatusCode.Redirect, status);
        Assert.Equal("/not-allowed", location);
    }

    [Fact]
    public async Task AVisitor_ShouldBeAskedToLogIn()
    {
        await using var site = await ShellSite.StartAsync(mongo.NewContext());

        var (status, _, location) = await site.GetAsync("/admin/start");

        Assert.Equal(HttpStatusCode.Redirect, status);
        Assert.StartsWith("/login", location, StringComparison.Ordinal);
    }

    internal const string Team = "team.person@example.test";

    /// <summary>A real team account (the demo has none: the team signs in with its own).</summary>
    internal static async Task AddTeamAsync(ShellSite site) =>
        await site.Services.GetRequiredService<BatteryPassWeb.Services.ClusterRepository>()
            .UpsertUserAsync(Team, "Tess Team", [BatteryPassWeb.Services.AccessControlService.RoleAdmin], BCrypt.Net.BCrypt.HashPassword("team-password-123456", 4));
}
