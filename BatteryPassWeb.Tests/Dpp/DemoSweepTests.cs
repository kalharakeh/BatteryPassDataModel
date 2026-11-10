using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit.Abstractions;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R03, the manager's condition 2: a sweep over every route of the running app (from its route table, so a new
/// route falls in without anyone adding it). Each route is called, with the real battery's IDs wherever it takes one and
/// in a generic query, form and body, as each demo identity and as its non-demo twin. A demo identity must never see a
/// restricted value of the real battery, never change the database, and (a demo token) never be answered on the real
/// battery; the twin shows the requests do reach data and make changes, so a quiet sweep means something.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DemoSweepTests(MongoFixture mongo, ITestOutputHelper output)
{
    private const string Header = "X-Sweep-User";
    private const string RealCluster = "cluster-test";
    private const string OtherRealCluster = "south";
    private const string DemoCluster = "demo-x";
    private const string Password = "sweep-password-123456";

    private sealed record Identity(string Name, string? Email, string? Token);

    private sealed record Pair(Identity Demo, Identity Twin, bool Api);

    [Fact]
    public async Task EveryRoute_ShouldKeepDemoIdentitiesOutOfTheRealBattery()
    {
        var (store, context, seed, planted) = await SeededWithMarkersAsync(mongo);
        await using var app = Build(context);
        await app.StartAsync();
        var services = app.Services;
        var database = context.Database!;

        // The real battery, published, in a real cluster, with a restricted document and a waiting hand-over.
        var clusters = services.GetRequiredService<ClusterRepository>();
        await clusters.UpsertClusterAsync(RealCluster, "Real customer");
        await clusters.UpsertClusterAsync(OtherRealCluster, "Other real customer");
        await clusters.UpsertClusterAsync(DemoCluster, "Demo customer");
        await database.GetCollection<BsonDocument>("clusters").UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("clusterId", DemoCluster), Builders<BsonDocument>.Update.Set(ClusterRepository.DemoMark, true));
        var team = PublicationTesting.Team;
        var pack = seed.Sources.Pack;
        await store.SetPackClusterAsync(pack.Id, RealCluster);
        Assert.Null((await services.GetRequiredService<PublicationService>().PublishAsync(team, pack.Id)).Problem);
        var document = (await services.GetRequiredService<DocumentService>().UploadAsync(team, TargetKind.Model, seed.Sources.Model.Id, "dismantlingInformation",
            new DocumentUpload("sweep.pdf", DocumentTesting.Pdf("sweep"), "SWEEPSECRETDOCUMENT"))).Document!;
        var handOver = await services.GetRequiredService<HandOverService>().AskAsync(ChangeSender.ClusterAdmin("real.admin@example.test", RealCluster), pack.Id, OtherRealCluster, confirm: true);
        Assert.Null(handOver.Problem);
        var handOverId = (await database.GetCollection<BsonDocument>(DppStore.HandOvers).Find(FilterDefinition<BsonDocument>.Empty).FirstAsync())["_id"].ToString()!;
        var version = (await store.GetActiveVersionAsync(pack.Id))!;
        var batteryId = pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>();
        var serial = pack.Values["batterySerialNumber"].Value!.GetValue<string>();
        var markers = new HashSet<string>(planted, StringComparer.Ordinal) { "SWEEPSECRETDOCUMENT" };
        Assert.True(planted.Count >= 1, "no restricted text to plant markers in");

        // The identities: each demo one beside its real twin. A twin's request may change them (the team's "Clear demo
        // data" removes the demo accounts and tokens, a token may be regenerated), so they are put back after it.
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        var setups = new List<Func<Task>>();
        Identity Account(string email, string role, bool demo, params (string Cluster, string Role)[] memberships)
        {
            setups.Add(async () =>
            {
                if (await clusters.GetUserByEmailAsync(email) is null)
                {
                    await clusters.UpsertUserAsync(email, email, [role], BCrypt.Net.BCrypt.HashPassword(Password, 4));
                }

                await database.GetCollection<BsonDocument>("users").UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("email", email),
                    Builders<BsonDocument>.Update.Set("roles", new BsonArray { role }).Set(ClusterRepository.DemoMark, demo));
                foreach (var (cluster, clusterRole) in memberships)
                {
                    await clusters.UpsertClusterMembershipAsync(email, cluster, clusterRole);
                }
            });
            return new Identity(email, email, null);
        }

        Identity Token(string name, ExternalTokenAccessMode mode, string[] clusterIds, bool global, bool demo)
        {
            setups.Add(async () =>
            {
                var repository = services.GetRequiredService<ExternalApiRepository>();
                if (tokens.TryGetValue(name, out var value) && (await repository.ValidateTokenAsync(value, ExternalTokenRequirement.Read)).Success)
                {
                    return;
                }

                var (made, fresh) = await repository.CreateTokenAsync(name, mode, clusterIds, false, global, "sweep");
                if (demo)
                {
                    await repository.MarkDemoAsync(made["_id"]);
                }

                tokens[name] = fresh;
            });
            return new Identity(name, null, name);
        }

        async Task PutBackAsync()
        {
            foreach (var (cluster, name, demo) in new[] { (RealCluster, "Real customer", false), (OtherRealCluster, "Other real customer", false), (DemoCluster, "Demo customer", true) })
            {
                await clusters.UpsertClusterAsync(cluster, name);
                await database.GetCollection<BsonDocument>("clusters").UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("clusterId", cluster), Builders<BsonDocument>.Update.Set(ClusterRepository.DemoMark, demo));
            }

            foreach (var setup in setups)
            {
                await setup();
            }

            await store.SetPackClusterAsync(pack.Id, RealCluster);
            if (await store.PassportStatusAsync(pack.Id) != DppStatuses.Active)
            {
                await services.GetRequiredService<PassportMarks>().UndoAsync(team, pack.Id);
            }
        }

        var pairs = new List<Pair>
        {
            new(Account("demo.msa@example.test", AccessControlService.RoleMarketSurveillanceAuthority, true), Account("msa@example.test", AccessControlService.RoleMarketSurveillanceAuthority, false), false),
            new(Account("demo.repair@example.test", AccessControlService.RoleLiRepair, true), Account("repair@example.test", AccessControlService.RoleLiRepair, false), false),
            new(Account("demo.nb@example.test", AccessControlService.RoleNotifiedBody, true), Account("nb@example.test", AccessControlService.RoleNotifiedBody, false), false),
            new(
                Account("demo.admin@example.test", AccessControlService.RoleNormalUser, true, (RealCluster, AccessControlService.RoleClusterAdmin), (DemoCluster, AccessControlService.RoleClusterAdmin)),
                Account("admin@example.test", AccessControlService.RoleNormalUser, false, (RealCluster, AccessControlService.RoleClusterAdmin)),
                false),
            new(Account("demo.team@example.test", AccessControlService.RoleAdmin, true), Account("team@example.test", AccessControlService.RoleAdmin, false), false),
            new(Token("demo team", ExternalTokenAccessMode.Lifecycle, [], true, true), Token("real team", ExternalTokenAccessMode.Lifecycle, [], true, false), true),
            new(Token("demo feed", ExternalTokenAccessMode.ReadWrite, [RealCluster], false, true), Token("real feed", ExternalTokenAccessMode.ReadWrite, [RealCluster], false, false), true),
        };
        await PutBackAsync();

        // Every route the app has, the reads first.
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"]).Select(method => (Method: method, endpoint.RoutePattern)))
            .Distinct()
            .OrderBy(route => route.Method == "GET" ? 0 : 1)
            .ThenBy(route => route.RoutePattern.RawText, StringComparer.Ordinal)
            .ToList();
        Assert.True(routes.Count > 50, $"only {routes.Count} routes found");

        var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(60) };

        Identity Current(Identity who) => who.Token is { } name ? who with { Token = tokens[name] } : who;
        var problems = new List<string>();
        var twinSaw = new HashSet<string>(StringComparer.Ordinal);
        var twinChanged = new HashSet<string>(StringComparer.Ordinal);
        var requests = 0;
        var values = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["pack"] = [pack.Id, batteryId, version.DigitalProductPassportId, serial],
        };

        foreach (var (method, pattern) in routes)
        {
            var api = pattern.RawText!.StartsWith("api/external", StringComparison.OrdinalIgnoreCase);
            var idParameters = pattern.Parameters.Where(parameter => !parameter.Name.Contains("cluster", StringComparison.OrdinalIgnoreCase)).ToList();
            var candidates = idParameters.Count == 0 ? [pack.Id] : values["pack"];
            foreach (var candidate in candidates)
            {
                var path = Path(pattern, name =>
                    name.Contains("cluster", StringComparison.OrdinalIgnoreCase) ? RealCluster
                    : name.Contains("document", StringComparison.OrdinalIgnoreCase) || name.Contains("file", StringComparison.OrdinalIgnoreCase) ? document.Id
                    : name.Contains("handover", StringComparison.OrdinalIgnoreCase) ? handOverId
                    : name.Contains("section", StringComparison.OrdinalIgnoreCase) ? "circularity"
                    : candidate);
                var query = $"?id={Uri.EscapeDataString(candidate)}&packId={Uri.EscapeDataString(pack.Id)}&q={Uri.EscapeDataString(serial)}&hours=24";
                foreach (var pair in pairs.Where(pair => pair.Api == api))
                {
                    var key = $"{method} /{pattern.RawText} [{candidate}] {pair.Twin.Name}";
                    var demo = await CallAsync(http, database, method, path + query, Current(pair.Demo), candidate, pack.Id, serial, seed);
                    var twin = await CallAsync(http, database, method, path + query, Current(pair.Twin), candidate, pack.Id, serial, seed);
                    if (twin.Changed || demo.Changed)
                    {
                        await PutBackAsync();
                    }
                    requests += 2;

                    if (markers.FirstOrDefault(marker => demo.Body.Contains(marker, StringComparison.Ordinal)) is { } seen)
                    {
                        problems.Add($"{key}: the demo identity saw a restricted value ({seen[..Math.Min(20, seen.Length)]}…), status {(int)demo.Status}");
                    }

                    // A change a visitor makes too (asking for a temporary password) is no demo identity's doing.
                    if (demo.Changed && (await CallAsync(http, database, method, path + query, new Identity("visitor", null, null), candidate, pack.Id, serial, seed)).Changed is false)
                    {
                        problems.Add($"{key}: the demo identity changed the database, status {(int)demo.Status}");
                    }

                    if (pair.Api && idParameters.Count > 0 && (int)demo.Status is >= 200 and < 300)
                    {
                        problems.Add($"{key}: a demo token was answered {(int)demo.Status} on the real battery");
                    }

                    if (idParameters.Count == 0 && method == "GET" && demo.Body.Contains(batteryId, StringComparison.Ordinal))
                    {
                        problems.Add($"{key}: a list showed the demo identity the real battery");
                    }

                    if (markers.Any(marker => twin.Body.Contains(marker, StringComparison.Ordinal)))
                    {
                        twinSaw.Add($"{method} /{pattern.RawText}");
                    }

                    if (twin.Changed)
                    {
                        twinChanged.Add($"{method} /{pattern.RawText}");
                    }
                }
            }
        }

        output.WriteLine($"{routes.Count} routes, {requests} requests. Twins saw restricted values on {twinSaw.Count} routes and made changes on {twinChanged.Count}.");
        output.WriteLine("Twins saw: " + string.Join(", ", twinSaw.Order()));
        output.WriteLine("Twins changed: " + string.Join(", ", twinChanged.Order()));
        foreach (var problem in problems)
        {
            output.WriteLine(problem);
        }

        // The control: the same requests do reach the role's data and make changes, so the sweep would see a leak.
        Assert.Contains(twinSaw, route => route.Contains("/detail", StringComparison.Ordinal));
        Assert.Contains(twinSaw, route => route.StartsWith("GET /api/external/v1/batteries/{id}", StringComparison.Ordinal));
        Assert.Contains(twinChanged, route => route.StartsWith("POST ", StringComparison.Ordinal));
        Assert.True(problems.Count == 0, $"{problems.Count} problems:\n" + string.Join("\n", problems.Take(40)));
    }

    private sealed record Answer(HttpStatusCode Status, string Body, bool Changed);

    private static async Task<Answer> CallAsync(HttpClient http, IMongoDatabase database, string method, string url, Identity who, string id, string packId, string serial, SeedPassport seed)
    {
        var writes = method != "GET" && method != "HEAD";
        var before = writes ? await SnapshotAsync(database) : null;
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (who.Email is { } email)
        {
            request.Headers.Add(Header, email);
        }

        if (who.Token is { } token)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(token + ":")));
        }

        if (writes)
        {
            if (url.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                var live = BatteryPassWeb.Dpp.Api.BatteryApi.LiveSet[0].Property;
                var body = new JsonObject
                {
                    ["batchId"] = seed.Sources.Batch.Id,
                    ["operatorId"] = seed.Sources.Operator.Id,
                    ["clusterId"] = RealCluster,
                    ["values"] = new JsonObject { [live] = 50, ["batterySerialNumber"] = "SWEEP-NEW-1" },
                    ["batteries"] = new JsonArray(new JsonObject { ["batteryId"] = id, ["values"] = new JsonObject { [live] = 51 } }),
                    ["batteryId"] = id,
                    ["serialNumber"] = serial,
                };
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }
            else
            {
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["id"] = id, ["packId"] = packId, ["batteryId"] = id, ["reason"] = "Sweep reason", ["note"] = "Sweep note",
                    ["clusterId"] = OtherRealCluster, ["toClusterId"] = OtherRealCluster, ["accept"] = "true", ["confirm"] = "true",
                    ["what"] = "repaired", ["name"] = "Sweep", ["email"] = "sweep.new@example.test", ["role"] = "member",
                    ["password"] = Password, ["passwordConfirmation"] = Password, ["systemRole"] = "member", ["text"] = "Sweep text",
                });
            }
        }

        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var changed = writes && await SnapshotAsync(database) != before;
        return new Answer(response.StatusCode, text, changed);
    }

    /// <summary>A fingerprint of everything stored but the logs and the tokens' last use.</summary>
    private static async Task<string> SnapshotAsync(IMongoDatabase database)
    {
        var text = new StringBuilder();
        foreach (var name in (await (await database.ListCollectionNamesAsync()).ToListAsync()).Order(StringComparer.Ordinal))
        {
            if (name.Contains("audit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var record in await database.GetCollection<BsonDocument>(name).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
            {
                record.Remove("lastUsedAt");
                record.Remove("lastUsedIp");
                if (name == "apiTokens")
                {
                    record.Remove("updatedAt"); // a token's use stamps it
                }
                text.Append(name).Append(record.ToJson());
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>
    /// The test seed (as <see cref="DppStoreTesting.SeededStoreAsync"/> stores it) with distinct markers in its model's
    /// restricted free text (#43 materials, Shared with repair and recycling): what no demo identity may ever see.
    /// </summary>
    private static async Task<(DppStore Store, MongoContext Context, SeedPassport Seed, List<string> Planted)> SeededWithMarkersAsync(MongoFixture mongo)
    {
        var context = mongo.NewContext();
        var store = new DppStore(context);
        await store.EnsureIndexesAsync();
        var seed = DppTestSeed.Load();
        var planted = new List<string>();
        foreach (var property in new[] { "materialsUsedInCathodeAnodeAndElectrolyte" })
        {
            if (seed.Sources.Model.Values.TryGetValue(property, out var stored) && stored.Value is { } value)
            {
                seed.Sources.Model.Values[property] = new BatteryPassWeb.Dpp.Values.AttributeValue
                {
                    Value = Plant(value.DeepClone(), property, planted),
                    GeneratedAtUtc = stored.GeneratedAtUtc,
                    Source = stored.Source,
                    Who = stored.Who,
                };
            }
        }

        await store.InsertAsync(new BatteryFamily { Id = seed.Sources.Model.FamilyId, Name = "Test battery family" });
        await store.InsertAsync(seed.Sources.Model);
        await store.InsertAsync(seed.Sources.Site);
        await store.InsertAsync(seed.Sources.Batch);
        await store.InsertAsync(seed.Sources.Operator);
        await store.InsertAsync(seed.Sources.Pack);
        return (store, context, seed, planted);

        static JsonNode Plant(JsonNode node, string property, List<string> planted)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var key in obj.Select(pair => pair.Key).ToArray())
                    {
                        obj[key] = obj[key] is { } child ? Plant(child, property, planted) : null;
                    }

                    return obj;
                case JsonArray array:
                    for (var index = 0; index < array.Count; index++)
                    {
                        array[index] = array[index] is { } child ? Plant(child, property, planted) : null;
                    }

                    return array;
                case JsonValue value when value.TryGetValue<string>(out _):
                    var marker = $"SWEEPSECRET{property.Length}X{planted.Count}";
                    planted.Add(marker);
                    return JsonValue.Create(marker)!;
                default:
                    return node;
            }
        }
    }

    private static string Path(RoutePattern pattern, Func<string, string> value)
    {
        var path = new StringBuilder();
        foreach (var segment in pattern.PathSegments)
        {
            path.Append('/');
            foreach (var part in segment.Parts)
            {
                path.Append(part switch
                {
                    RoutePatternLiteralPart literal => literal.Content,
                    RoutePatternParameterPart parameter => Uri.EscapeDataString(value(parameter.Name)),
                    RoutePatternSeparatorPart separator => separator.Content,
                    _ => string.Empty,
                });
            }
        }

        return path.Length == 0 ? "/" : path.ToString();
    }

    /// <summary>The app's controllers and services on the test database, with a header sign-in and no antiforgery check.</summary>
    private static WebApplication Build(MongoContext context)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(DppController).Assembly.GetName().Name,
            ContentRootPath = System.IO.Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Program.cs")),
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var options = new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl, DemoAdminEmail = string.Empty, ExternalApiEncryptionKey = "sweep-test-key-for-the-token-hashes-0000" };
        var services = builder.Services;
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(context);
        services.AddSingleton(DemoDataSwitch.On);
        services.AddSingleton(SigningTesting.Keys);
        services.AddSingleton<ClusterRepository>();
        services.AddSingleton<ApplicationAuditService>();
        services.AddSingleton<ApplicationSettingsService>();
        services.AddSingleton<AuthenticationSessionService>();
        services.AddSingleton<AccessControlService>();
        services.AddSingleton<PassportQrCodeService>();
        services.AddSingleton<IEmailSender, NoMail>();
        services.AddSingleton<AuthService>();
        services.AddSingleton(new ExternalApiSecurityService(options.ExternalApiEncryptionKey));
        services.AddSingleton<ExternalApiRepository>();
        services.AddDpp();
        services.AddSingleton<DemoDataService>();
        services.AddControllersWithViews().AddApplicationPart(typeof(DppController).Assembly);
        services.Replace(ServiceDescriptor.Singleton<IAntiforgery, NoAntiforgery>());
        services.AddAuthentication(SweepAuthentication.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SweepAuthentication>(SweepAuthentication.SchemeName, _ => { })
            .AddCookie();
        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
            authorization.AddPolicy("ClusterAdminOrAdmin", policy => policy.RequireRole("clusterAdmin", "admin"));
        });
        services.AddRateLimiter(_ => { });

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }

    /// <summary>Signs in the account the request names, as the app's sign-in would make it (a demo account is never the team).</summary>
    private sealed class SweepAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, AuthService auth)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Sweep";

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var email = Request.Headers[Header].ToString();
            if (email.Length == 0)
            {
                return AuthenticateResult.NoResult();
            }

            var principal = await auth.CreatePrincipalForUserAsync(email);
            return principal is null
                ? AuthenticateResult.Fail("no such account")
                : AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(principal.Claims, SchemeName)), SchemeName));
        }
    }

    private sealed class NoAntiforgery : IAntiforgery
    {
        private static readonly AntiforgeryTokenSet Tokens = new("sweep", "sweep", "__RequestVerificationToken", "RequestVerificationToken");

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => Tokens;

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => Tokens;

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);

        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;

        public void SetCookieTokenAndHeader(HttpContext httpContext)
        {
        }
    }

    private sealed class NoMail : IEmailSender
    {
        public bool IsConfigured => false;

        public Task SendTemporaryPasswordAsync(string recipientEmail, string temporaryPassword, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
