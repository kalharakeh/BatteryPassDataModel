using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.RegistryToken;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Dpp.RegistryTokenTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (decision 94): the EU registry's read at a passport's data address, over real HTTP requests to the app's controllers
/// on a free port, wired as <c>Program.cs</c> wires them (the scheme, then the read limit). Without a token: the public
/// part, as before. With a valid token: the restricted parts too, never cached by a shared cache, never redirected. A bad
/// token: 401; a token that may not read: 403; never the public part instead, and never the token in the log.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RegistryTokenHttpTests(MongoFixture mongo) : IDisposable
{
    private const string Browser = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
    private readonly ECDsa _key = NewKey();

    public void Dispose() => _key.Dispose();

    private string ValidToken(Action<JsonObject>? claims = null)
    {
        var body = Claims(DateTimeOffset.UtcNow.AddSeconds(-2));
        claims?.Invoke(body);
        return Token(_key, Header(), body);
    }

    [Fact]
    public async Task WithoutAToken_TheAddressShouldAnswerThePublicPart_AsBefore()
    {
        await using var site = await SiteAsync();

        using var answer = await site.GetAsync($"/dpp/{site.Id}", "application/json");

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        var data = JsonNode.Parse(await answer.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(PublicPaths(site), Paths(data));
        Assert.Equal(["Accept", "Authorization"], answer.Headers.Vary);
        Assert.True(answer.Headers.CacheControl is { NoCache: true, NoStore: true, Private: false });
        Assert.Equal(0, site.Keys.Fetches); // an anonymous read never touches the EU's keys
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData(Browser)] // a token gets the data, never the page
    [InlineData("")]
    public async Task AValidToken_ShouldGetTheRestrictedPartsToo_Directly_AndNotForSharedCaches(string accept)
    {
        await using var site = await SiteAsync();

        using var answer = await site.GetAsync($"/dpp/{site.Id}", accept, $"Bearer {ValidToken()}");

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal("application/json", answer.Content.Headers.ContentType?.MediaType);
        Assert.True(answer.Headers.CacheControl is { Private: true, NoStore: true, Public: false }, $"{answer.Headers.CacheControl}");
        Assert.Equal(["Accept", "Authorization"], answer.Headers.Vary);
        var data = JsonNode.Parse(await answer.Content.ReadAsStringAsync())!.AsObject();
        var paths = Paths(data);
        Assert.All(PublicPaths(site), path => Assert.Contains(path, paths)); // the public part and more
        foreach (var audience in new[] { BatteryPassWeb.Dpp.Catalog.Audience.Shared, BatteryPassWeb.Dpp.Catalog.Audience.Authorities, BatteryPassWeb.Dpp.Catalog.Audience.Purchaser })
        {
            Assert.Contains(AttributeCatalog.All.Where(attribute => attribute.Audience == audience), attribute => paths.Contains(attribute.Path));
        }

        Assert.Equal(PassportPaths(site), paths); // the whole passport, as the reader "EU registry" sees it
    }

    [Fact]
    public async Task AValidToken_ShouldBeAnsweredAtEveryDataAddress_WithoutARedirect()
    {
        await using var site = await SiteAsync();

        foreach (var path in new[] { $"/dpp/{site.Id}.json", $"/dpp/{site.Id}/v1", $"/dpp/{site.Id}/v1.json" })
        {
            using var answer = await site.GetAsync(path, "application/json", $"Bearer {ValidToken()}");

            Assert.False((int)answer.StatusCode is >= 300 and < 400, path);
            Assert.Equal("application/json", answer.Content.Headers.ContentType?.MediaType);
            Assert.True(answer.Headers.CacheControl is { Private: true, NoStore: true }, path);
        }
    }

    public static TheoryData<string, string> RefusedCredentials() => new()
    {
        { "expired", "expired" },
        { "garbage", "garbage" },
        { "wrong audience", "audience" },
        { "another environment's issuer", "issuer" },
        { "Basic", "basic" },
        { "Bearer without a token", "bearer-empty" },
    };

    [Theory]
    [MemberData(nameof(RefusedCredentials))]
    public async Task AnInvalidCredential_ShouldBe401_WithoutThePublicPart(string why, string kind)
    {
        await using var site = await SiteAsync();
        var authorization = kind switch
        {
            "expired" => $"Bearer {Token(_key, Header(), Claims(DateTimeOffset.UtcNow.AddMinutes(-5)))}",
            "garbage" => "Bearer not-a-token",
            "audience" => $"Bearer {ValidToken(claims => claims["aud"] = "https://another.example.com")}",
            "issuer" => $"Bearer {ValidToken(claims => claims["iss"] = "https://auth.product-passport.ec.europa.eu")}",
            "basic" => "Basic dXNlcjpwYXNz",
            _ => "Bearer ",
        };

        foreach (var accept in new[] { "application/json", Browser })
        {
            using var answer = await site.GetAsync($"/dpp/{site.Id}", accept, authorization);

            Assert.True(answer.StatusCode == HttpStatusCode.Unauthorized, $"{why}, {accept}: {answer.StatusCode}");
            Assert.Equal("Bearer", answer.Headers.WwwAuthenticate.Single().Scheme);
            Assert.Equal(kind is "basic" or "bearer-empty" ? "error=\"invalid_request\"" : "error=\"invalid_token\"", answer.Headers.WwwAuthenticate.Single().Parameter);
            await AssertNoPassportAsync(site, answer);
        }
    }

    [Fact]
    public async Task AValidTokenForAnotherOrganisation_ShouldBe403_WithoutDataOrTheExpectedOrganisation()
    {
        await using var site = await SiteAsync();

        using var answer = await site.GetAsync($"/dpp/{site.Id}", "application/json", $"Bearer {ValidToken(claims => claims["resource_requestor_identifiers"]![0]!["value"] = "ORG-SOMEONE-ELSE")}");

        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
        Assert.Empty(answer.Headers.WwwAuthenticate);
        var body = await AssertNoPassportAsync(site, answer);
        Assert.Equal(RegistryTokenAnswers.NotAuthorised, body["detail"]!.GetValue<string>());
        Assert.DoesNotContain(Organisation, body.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AValidTokenWithTheWrongScope_ShouldBe403_InsufficientScope()
    {
        await using var site = await SiteAsync();

        using var answer = await site.GetAsync($"/dpp/{site.Id}.json", "application/json", $"Bearer {ValidToken(claims => claims["scope"] = "dpp:registry:write")}");

        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
        Assert.Equal("error=\"insufficient_scope\", scope=\"dpp:registry:read\"", answer.Headers.WwwAuthenticate.Single().Parameter);
        Assert.Equal(RegistryTokenAnswers.WrongScope, (await AssertNoPassportAsync(site, answer))["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task ATokenInTheQueryOrACookie_ShouldBe401_EvenAValidOne()
    {
        await using var site = await SiteAsync();
        var token = ValidToken();

        using var query = await site.GetAsync($"/dpp/{site.Id}?access_token={token}", "application/json");
        using var queryAndHeader = await site.GetAsync($"/dpp/{site.Id}.json?access_token={token}", "application/json", $"Bearer {token}");
        using var cookie = await site.GetAsync($"/dpp/{site.Id}", Browser, cookie: $"access_token={token}");

        foreach (var answer in new[] { query, queryAndHeader, cookie })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, answer.StatusCode);
            await AssertNoPassportAsync(site, answer);
        }
    }

    [Fact]
    public async Task TwoAuthorizationHeaders_ShouldBe401()
    {
        await using var site = await SiteAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/dpp/{site.Id}");
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("Authorization", [$"Bearer {ValidToken()}", $"Bearer {ValidToken()}"]);

        using var answer = await site.Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, answer.StatusCode);
    }

    [Fact]
    public async Task WhileTheCheckIsOff_AValidToken_ShouldStillBe401_NeverThePublicPart()
    {
        await using var site = await SiteAsync(RegistryTokenSettings.Off);

        using var answer = await site.GetAsync($"/dpp/{site.Id}", "application/json", $"Bearer {ValidToken()}");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.StatusCode);
        await AssertNoPassportAsync(site, answer);
        Assert.Equal(0, site.Keys.Fetches);
    }

    [Fact]
    public async Task TheBatteryIdsAddress_ShouldRedirectWhateverTheHeaders_AndTheWholeChainStayWithinOneRedirect()
    {
        await using var site = await SiteAsync();
        const string item = "UN778474481-VBC-92921"; // the test passports have URN IDs, which have no /25S/ address

        using var withToken = await site.GetAsync($"/25S/{item}", "application/json", $"Bearer {ValidToken()}");
        using var garbage = await site.GetAsync($"/25S/{item}", "application/json", "Bearer not-a-token");
        using var anonymous = await site.GetAsync($"/25S/{item}", "application/json");

        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, withToken.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, garbage.StatusCode);
        Assert.Equal($"/dpp/{item}", anonymous.Headers.Location!.ToString());
        Assert.Equal(anonymous.Headers.Location, withToken.Headers.Location);
        Assert.Equal(0, site.Keys.Fetches); // the resolver never checks a token
        using var target = await site.GetAsync(anonymous.Headers.Location!.ToString(), "application/json");
        using var registry = await site.GetAsync(anonymous.Headers.Location!.ToString(), "application/json", $"Bearer {ValidToken()}");
        Assert.False((int)target.StatusCode is >= 300 and < 400); // the anonymous discovery ends there: one redirect in all
        Assert.False((int)registry.StatusCode is >= 300 and < 400); // and the token's read at the same address: none
        Assert.Equal("application/json", registry.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TheLog_ShouldHoldNoToken_OnlyTheResultAndTheOrganisation()
    {
        await using var site = await SiteAsync();
        var tokens = new[]
        {
            ValidToken(),
            ValidToken(claims => claims["aud"] = "https://another.example.com"),
            ValidToken(claims => claims["resource_requestor_identifiers"]![0]!["value"] = "ORG-SOMEONE-ELSE"),
            Token(_key, Header(), Claims(DateTimeOffset.UtcNow.AddMinutes(-5))),
        };

        foreach (var token in tokens)
        {
            using var answer = await site.GetAsync($"/dpp/{site.Id}", "application/json", $"Bearer {token}");
        }

        using (await site.GetAsync($"/dpp/{site.Id}?access_token={tokens[0]}", "application/json"))
        using (await site.GetAsync($"/dpp/{site.Id}", Browser, cookie: $"access_token={tokens[0]}"))
        using (await site.GetAsync("/dpp/X%0A%20%20%20%20%20%20EU%20registry%20read%20of%20Y:%20200.json", "application/json", "Bearer forged"))
        {
        }

        var log = string.Join("\n", site.Log);
        Assert.DoesNotContain(site.Log, line => line.Contains("\n      EU registry read of Y", StringComparison.Ordinal)); // no line of its own
        Assert.Contains("EU registry read of X?      EU registry read of Y: 200: refused, 401", log, StringComparison.Ordinal);
        Assert.Contains($"200, organisation {Organisation}", log, StringComparison.Ordinal);
        Assert.Contains("refused, 401 audience", log, StringComparison.Ordinal);
        Assert.Contains("refused, 403 requestor-other", log, StringComparison.Ordinal);
        foreach (var part in tokens.SelectMany(token => token.Split('.')).Distinct())
        {
            Assert.DoesNotContain(part, log, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("ORG-SOMEONE-ELSE", log, StringComparison.Ordinal); // another organisation's ID is the token's, not ours
        Assert.DoesNotContain("Bearer", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AValidRead_ShouldGoIntoTheAccessLog_AsTheEuRegistry()
    {
        await using var site = await SiteAsync();

        using var first = await site.GetAsync($"/dpp/{site.Id}", "application/json", $"Bearer {ValidToken()}");
        using var second = await site.GetAsync($"/dpp/{site.Id}.json", "application/json", $"Bearer {ValidToken()}");
        using var refused = await site.GetAsync($"/dpp/{site.Id}", "application/json", "Bearer not-a-token");

        var entries = await site.Rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", RestrictedReads.EventType)).ToListAsync();
        var entry = Assert.Single(entries); // H1's log: the first read of the battery per reader and day
        Assert.Equal($"eu-registry:{Organisation}".ToLowerInvariant(), entry["actor"].AsString.ToLowerInvariant());
        Assert.Equal(RegistryTokenRequest.RoleLabel, entry["actorRole"].AsString);
        Assert.Equal(site.Rig.PackId, entry["entityId"].AsString);
        Assert.Equal(RestrictedReads.EuRegistryRead, entry["metadata"]["page"].AsString);
        Assert.Equal(["authorities", "purchaser", "shared"], entry["metadata"]["sections"].AsBsonArray.Select(section => section.AsString).Order());
    }

    [Fact]
    public async Task TheApisOwnTokens_ShouldBeUnchanged()
    {
        await using var site = await SiteAsync();

        using var basic = await site.GetAsync("/api/external/v1/clusters", "application/json", "Basic dXNlcjpwYXNz");
        using var bearer = await site.GetAsync("/api/external/v1/clusters", "application/json", $"Bearer {ValidToken()}");

        foreach (var answer in new[] { basic, bearer })
        {
            Assert.NotEqual(RegistryTokenAnswers.ProblemType, answer.Content.Headers.ContentType?.MediaType); // the API's own answer
            Assert.DoesNotContain("invalid_token", answer.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
        }

        Assert.Equal(0, site.Keys.Fetches);
    }

    private static async Task<JsonObject> AssertNoPassportAsync(Site site, HttpResponseMessage answer)
    {
        Assert.Equal(RegistryTokenAnswers.ProblemType, answer.Content.Headers.ContentType?.MediaType);
        Assert.Equal(["Accept", "Authorization"], answer.Headers.Vary);
        Assert.True(answer.Headers.CacheControl is { NoStore: true }, $"{answer.Headers.CacheControl}");
        var text = await answer.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Uri.UnescapeDataString(site.Id), text, StringComparison.Ordinal);
        Assert.DoesNotContain("batteryPassportData", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dppHeader", text, StringComparison.Ordinal);
        var body = JsonNode.Parse(text)!.AsObject();
        Assert.Equal(["correlationId", "detail", "status", "title"], body.Select(property => property.Key).Order());
        return body;
    }

    private static string[] PublicPaths(Site site) => site.Published(Reader.Anonymous);

    private static string[] PassportPaths(Site site) => site.Published(DppReaders.EuRegistry(Organisation));

    private static string[] Paths(JsonObject data) =>
        AttributeCatalog.All.Where(attribute => Holds(data, attribute.Path)).Select(attribute => attribute.Path).Order().ToArray();

    private static bool Holds(JsonObject data, string path)
    {
        JsonNode? node = data;
        foreach (var name in path.Split('.'))
        {
            if (node is not JsonObject parent || !parent.TryGetPropertyValue(name, out node))
            {
                return false;
            }
        }

        return true;
    }

    private Task<Site> SiteAsync(RegistryTokenSettings? settings = null, int readLimit = 1000) => StartAsync(mongo, _key, settings, readLimit);

    /// <summary>A site with a published passport, the EU's key set serving <paramref name="key"/>, and the read limit.</summary>
    internal static async Task<Site> StartAsync(MongoFixture mongo, ECDsa key, RegistryTokenSettings? settings = null, int readLimit = 1000)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var keys = KeySet.With((key, KeyId));
        var log = new ConcurrentQueue<string>();
        var app = Build(rig, settings ?? Settings, keys, log, readLimit);
        try
        {
            using var starting = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await app.StartAsync(starting.Token);
            var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            var published = (await SigningTesting.Passports(rig.Store).CurrentAsync(rig.PackId))!.Passport;
            var clusters = await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId);
            return new Site(app, rig, keys, log, published, clusters, new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(60) });
        }
        catch
        {
            await Site.StopAsync(app);
            throw;
        }
    }

    /// <summary>The app's controllers on the test database, wired as Program.cs wires them: routing, cookies and the EU registry's scheme, the token check, the read limit.</summary>
    internal static WebApplication Build(PublicationRig rig, RegistryTokenSettings settings, KeySet keys, ConcurrentQueue<string> log, int readLimit)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PassportController).Assembly.GetName().Name,
            ContentRootPath = Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Program.cs")),
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning); // as the app's appsettings say (pinned below)
        builder.Logging.AddProvider(new ListLogger(log));
        builder.Configuration["DPP_PASSPORT_CHECK"] = "off";
        var options = new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl, DemoAdminEmail = string.Empty, ExternalApiEncryptionKey = "http-test-key-for-the-token-hashes-0000" };
        var services = builder.Services;
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(rig.Context);
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
        services.AddControllersWithViews().AddApplicationPart(typeof(PassportController).Assembly);
        services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme).AddCookie().AddRegistryToken();
        services.AddRegistryToken(settings);
        services.AddSingleton<IHttpClientFactory>(keys); // the EU's key set, served by the test
        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
            authorization.AddPolicy("ClusterAdminOrAdmin", policy => policy.RequireRole("clusterAdmin", "admin"));
        });
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = TooManyRequests.WriteAsync;
            PublicDataRateLimit.Add(limiter, readLimit, 60);
            limiter.AddPolicy(SecurityRateLimitPolicyNames.ExternalApiRead, _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("none"));
        });

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseRegistryToken();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }

    internal sealed class Site(WebApplication app, PublicationRig rig, KeySet keys, ConcurrentQueue<string> log, JsonObject published, IReadOnlyCollection<string> clusters, HttpClient http) : IAsyncDisposable
    {
        public PublicationRig Rig => rig;

        public KeySet Keys => keys;

        public IEnumerable<string> Log => log;

        public HttpClient Http => http;

        public string Id { get; } = Uri.EscapeDataString(PassportPagesTests.Identifier(rig));

        public string[] Published(Reader reader) => Paths(PassportFilter.Cut(published, reader, clusters));

        public Task<HttpResponseMessage> GetAsync(string path, string accept, string? authorization = null, string? cookie = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (accept.Length > 0)
            {
                request.Headers.TryAddWithoutValidation("Accept", accept);
            }

            if (authorization is not null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
            }

            if (cookie is not null)
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
            }

            return http.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            http.Dispose();
            await StopAsync(app);
        }

        public static async Task StopAsync(WebApplication app)
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await app.StopAsync(stopping.Token);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.WriteLine($"The HTTP test host didn't stop cleanly: {exception.GetType().Name}: {exception.Message}");
            }

            try
            {
                await app.DisposeAsync();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.WriteLine($"The HTTP test host didn't dispose cleanly: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    /// <summary>Every log line, at every level, with its exception and scopes written out.</summary>
    private sealed class ListLogger(ConcurrentQueue<string> lines) : ILoggerProvider, ILogger
    {
        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            lines.Enqueue($"scope: {state}");
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue($"{logLevel}: {formatter(state, exception)} {exception}");

        public void Dispose()
        {
        }
    }

    private sealed class NoMail : IEmailSender
    {
        public bool IsConfigured => false;

        public Task SendTemporaryPasswordAsync(string recipientEmail, string temporaryPassword, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
