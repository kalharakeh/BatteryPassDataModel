using System.Security.Claims;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// The account pages' services on a fresh throwaway database: two customers (north and south), each with an admin and a
/// user, a market surveillance account and a team account. Every account's password is <see cref="Password"/>.
/// </summary>
internal sealed class AccountRig(ServiceProvider services, IMongoDatabase database) : IDisposable
{
    public const string Password = "the-old-password-1";
    public const string Typed = "typed-by-someone-else-1";
    public const string FallbackEmail = "fallback.admin@example.test";
    public const string FallbackPassword = "fallback-password-for-tests-1";

    public const string NorthAdmin = "north.admin@example.test";
    public const string NorthUser = "north.user@example.test";
    public const string SouthAdmin = "south.admin@example.test";
    public const string SouthUser = "south.user@example.test";
    public const string Msa = "msa@example.test";
    public const string Team = "team@example.test";
    public const string Team2 = "team2@example.test";

    public ServiceProvider Services { get; } = services;
    public IMongoDatabase Database { get; } = database;
    public ClusterRepository Clusters => Services.GetRequiredService<ClusterRepository>();
    public AuthService Auth => Services.GetRequiredService<AuthService>();
    public AccessControlService Access => Services.GetRequiredService<AccessControlService>();

    public static async Task<AccountRig> StartAsync(MongoFixture mongo, Action<BatteryPassOptions>? configure = null)
    {
        var options = new BatteryPassOptions
        {
            DemoAdminEmail = FallbackEmail,
            DemoAdminPassword = FallbackPassword,
            EnableDemoAdminFallback = true,
        };
        configure?.Invoke(options);
        var context = mongo.NewContext();
        var services = new ServiceCollection()
            .AddSingleton(Options.Create(options))
            .AddSingleton(context)
            .AddSingleton<ApplicationAuditService>()
            .AddSingleton<ApplicationSettingsService>()
            .AddSingleton<ClusterRepository>()
            .AddSingleton<AccessControlService>()
            .AddSingleton<IEmailSender, NoEmailSender>()
            .AddSingleton<AuthService>()
            .AddSingleton<AuthenticationSessionService>()
            .AddSingleton(SigningTesting.Keys)
            .AddSingleton(new ExternalApiSecurityService("account-test-key-for-the-token-hashes-00"))
            .AddSingleton<ExternalApiRepository>()
            .AddDpp()
            .AddSingleton<DemoDataService>()
            .BuildServiceProvider();
        await services.GetRequiredService<DppStore>().EnsureIndexesAsync();
        await services.GetRequiredService<ClusterRepository>().EnsureIndexesAsync();
        var rig = new AccountRig(services, context.Database!);
        await rig.SeedAsync();
        return rig;
    }

    private async Task SeedAsync()
    {
        await Clusters.UpsertClusterAsync("north", "Customer North");
        await Clusters.UpsertClusterAsync("south", "Customer South");
        await AccountAsync(NorthAdmin, "North Admin", AccessControlService.RoleNormalUser, "north", AccessControlService.RoleClusterAdmin);
        await AccountAsync(NorthUser, "North User", AccessControlService.RoleNormalUser, "north", AccessControlService.RoleNormalUser);
        await AccountAsync(SouthAdmin, "South Admin", AccessControlService.RoleNormalUser, "south", AccessControlService.RoleClusterAdmin);
        await AccountAsync(SouthUser, "South User", AccessControlService.RoleNormalUser, "south", AccessControlService.RoleNormalUser);
        await AccountAsync(Msa, "Market Surveillance", AccessControlService.RoleMarketSurveillanceAuthority);
        await AccountAsync(Team, "Team", AccessControlService.RoleAdmin);
        await AccountAsync(Team2, "Team Two", AccessControlService.RoleAdmin);
    }

    public async Task AccountAsync(string email, string name, string role, string? cluster = null, string? clusterRole = null, string password = Password)
    {
        await Clusters.UpsertUserAsync(email, name, [role], BCrypt.Net.BCrypt.HashPassword(password, 4));
        if (cluster is not null)
        {
            await Clusters.UpsertClusterMembershipAsync(email, cluster, clusterRole ?? AccessControlService.RoleNormalUser);
        }
    }

    public async Task<bool> SignsInAsync(string email, string password) =>
        (await Auth.AuthenticateLoginAsync(email, password)).Status == LoginAuthenticationStatus.Authenticated;

    public async Task<string> NameOfAsync(string email) =>
        BsonHelpers.GetString((await Clusters.GetUserByEmailAsync(email))!, "name");

    public async Task<IReadOnlyList<string>> RolesOfAsync(string email) =>
        ((await Clusters.GetUserByEmailAsync(email))!["roles"].AsBsonArray).Select(role => role.AsString).ToList();

    public async Task<IReadOnlyList<string>> ClustersOfAsync(string email) =>
        (await Clusters.GetClusterMembershipsForUserAsync(email)).Select(membership => BsonHelpers.GetString(membership, "clusterId")).ToList();

    public static ClaimsPrincipal Person(string email, params string[] roles) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), .. roles.Select(role => new Claim(ClaimTypes.Role, role))], "test"));

    /// <summary>The cluster admin's Users page, as <paramref name="email"/> (a cluster admin's sign-in gives both roles).</summary>
    public ClusterAdminController ClusterAdmin(string email) =>
        With(new ClusterAdminController(
            Access,
            Services.GetRequiredService<ApplicationAuditService>(),
            Clusters,
            Services.GetRequiredService<ExternalApiRepository>()),
            Person(email, AccessControlService.RoleNormalUser, AccessControlService.RoleClusterAdmin));

    /// <summary>The team's Users tab, as <paramref name="email"/>.</summary>
    public AdminController TeamMember(string email = Team) =>
        With(new AdminController(
            Clusters,
            Services.GetRequiredService<ExternalApiRepository>(),
            Services.GetRequiredService<ApplicationAuditService>(),
            Services.GetRequiredService<ApplicationSettingsService>(),
            Services.GetRequiredService<DppStore>(),
            Services.GetRequiredService<DemoDataService>()),
            Person(email, AccessControlService.RoleAdmin));

    public static T With<T>(T controller, ClaimsPrincipal user) where T : Controller
    {
        var http = new DefaultHttpContext { User = user, RequestServices = new ServiceCollection().BuildServiceProvider() };
        http.Request.Form = new FormCollection(new Dictionary<string, StringValues>());
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new NoTempDataStorage());
        return controller;
    }

    /// <summary>Sets the form the next action reads.</summary>
    public static T Form<T>(T controller, params (string Key, string Value)[] fields) where T : Controller
    {
        controller.HttpContext.Request.Form = new FormCollection(fields.ToDictionary(field => field.Key, field => new StringValues(field.Value)));
        return controller;
    }

    public void Dispose() => Services.Dispose();

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class NoEmailSender : IEmailSender
    {
        public bool IsConfigured => false;

        public Task SendTemporaryPasswordAsync(string recipientEmail, string temporaryPassword, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
