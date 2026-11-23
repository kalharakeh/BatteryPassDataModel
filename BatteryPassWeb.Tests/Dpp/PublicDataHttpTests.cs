using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Store;
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

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// C3 round two, point 9: the public address's choice of data or page, which route wins, the caching headers, the read
/// limit and a damaged proof, over real HTTP requests to the app's controllers and views on a free port (as
/// <see cref="DemoSweepTests"/> does), not by calling the methods.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PublicDataHttpTests(MongoFixture mongo)
{
    private const string Browser = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

    [Theory]
    [InlineData("application/json", true)]
    [InlineData("application/json, text/html;q=0.5", true)]
    [InlineData("application/json, */*;q=0.1", true)]
    [InlineData(Browser, false)]
    [InlineData("application/json;q=0.5, text/*", false)]
    [InlineData("application/json, */*", true)] // R3, decision 6: the page only when it is named and wanted as much as JSON
    [InlineData("application/ld+json", true)]
    [InlineData("*/*", true)]
    [InlineData("", true)]
    [InlineData("text/html, application/json", false)]
    public async Task ThePublicAddress_ShouldAnswerDataOrPage_ByTheAcceptHeader(string accept, bool data)
    {
        await using var site = await SiteAsync();

        using var answer = await site.GetAsync($"/dpp/{site.Id}", accept);

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Equal(data ? "application/json" : "text/html", answer.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Accept", answer.Headers.Vary);
    }

    [Fact]
    public async Task TheDataAddresses_ShouldWin_OverThePagesRoutes()
    {
        await using var site = await SiteAsync();

        using var byAddress = await site.GetAsync($"/dpp/{site.Id}.json", Browser);
        using var byAccept = await site.GetAsync($"/dpp/{site.Id}", "application/json");
        using var version = await site.GetAsync($"/dpp/{site.Id}/v1.json", Browser);
        using var versionByAccept = await site.GetAsync($"/dpp/{site.Id}/v1", "application/json");
        using var versionPage = await site.GetAsync($"/dpp/{site.Id}/v1", Browser);

        Assert.Equal("application/json", byAddress.Content.Headers.ContentType?.MediaType); // {id}.json, not {id} = "….json"
        var text = await byAddress.Content.ReadAsStringAsync();
        Assert.Equal(text, await byAccept.Content.ReadAsStringAsync());
        Assert.NotNull(JsonNode.Parse(text)!["proof"]);
        // The test passports have URN IDs, which have no version address, so these find nothing; the route still shows in
        // the answer: the data routes answer a JSON 404, the page route its HTML one.
        Assert.Equal("application/json", version.Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/json", versionByAccept.Content.Headers.ContentType?.MediaType);
        Assert.Equal("text/html", versionPage.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TheDataAndThePage_ShouldNotBeCached_TheirNotFoundIncluded()
    {
        await using var site = await SiteAsync();

        foreach (var (path, accept) in new[]
        {
            ($"/dpp/{site.Id}", Browser),
            ($"/dpp/{site.Id}", "application/json"),
            ($"/dpp/{site.Id}.json", Browser),
            ("/dpp/VPC8C05-0000", Browser),
            ("/dpp/VPC8C05-0000", "application/json"),
            ("/dpp/VPC8C05-0000.json", Browser),
        })
        {
            using var answer = await site.GetAsync(path, accept);
            Assert.True(answer.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"{path}: {answer.StatusCode}");
            Assert.True(answer.Headers.CacheControl is { NoCache: true, NoStore: true } cache && !cache.Public && !cache.Private && cache.MaxAge is null, $"{path}: {answer.Headers.CacheControl}");
            Assert.Contains("no-cache", answer.Headers.Pragma.Select(value => value.ToString()));
        }
    }

    [Fact]
    public async Task TheReadLimit_ShouldCountTheCallersAddress_WhateverItsAuthorizationHeader()
    {
        await using var site = await SiteAsync(readLimit: 3);

        var statuses = new List<HttpStatusCode>();
        foreach (var (path, accept) in new[]
        {
            ($"/dpp/{site.Id}.json", Browser),
            ($"/dpp/{site.Id}", "application/json"),
            ($"/dpp/{site.Id}.json", "application/json"),
            ($"/dpp/{site.Id}.json", Browser),
        })
        {
            using var answer = await site.GetAsync(path, accept, authorization: $"Bearer {Guid.NewGuid():N}");
            statuses.Add(answer.StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
        using var page = await site.GetAsync($"/dpp/{site.Id}", Browser);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode); // the page itself has no limit, as before
    }

    [Fact]
    public async Task ADamagedProof_ShouldBeNotVerified_NeverAnError()
    {
        await using var site = await SiteAsync();
        await site.Rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions).UpdateManyAsync(
            Builders<BsonDocument>.Filter.Eq("PackId", site.Rig.PackId),
            Builders<BsonDocument>.Update.Unset("Proof.PublicKeyX").Unset("Proof.PublicView.PublicKeyX"));

        using var page = await site.GetAsync($"/dpp/{site.Id}", Browser);
        using var data = await site.GetAsync($"/dpp/{site.Id}.json", Browser);
        using var verify = await site.GetAsync($"/api/passports/{site.Id}/verify", "application/json");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(HttpStatusCode.OK, data.StatusCode);
        Assert.Null(JsonNode.Parse(await data.Content.ReadAsStringAsync())!["proof"]);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var answer = JsonNode.Parse(await verify.Content.ReadAsStringAsync())!;
        Assert.False(answer["signatureVerified"]!.GetValue<bool>());
        Assert.Equal("The signature can't be read.", answer["signatureReason"]!.GetValue<string>());
    }

    [Fact]
    public async Task AHostThatFailsToStop_ShouldNotFailTheTest()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddHostedService<FailsToStop>();
        var app = builder.Build();
        await app.StartAsync();
        var raw = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        raw.WebHost.UseUrls("http://127.0.0.1:0");
        raw.Logging.ClearProviders();
        raw.Services.AddHostedService<FailsToStop>();
        await using var plain = raw.Build();
        await plain.StartAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => plain.StopAsync()); // the host's own stop does throw...
        await Site.StopAsync(app); // ...and this throws nothing
    }

    private async Task<Site> SiteAsync(int readLimit = 1000)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var app = Build(rig, readLimit);
        try
        {
            using var starting = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await app.StartAsync(starting.Token);
            var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            return new Site(app, rig, new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(60) });
        }
        catch
        {
            // A host that didn't start, or whose address can't be read, is stopped here rather than left listening.
            await Site.StopAsync(app);
            throw;
        }
    }

    /// <summary>The app's controllers and views on the test database, with the public data's read limit, anonymous.</summary>
    private static WebApplication Build(PublicationRig rig, int readLimit)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PassportController).Assembly.GetName().Name,
            ContentRootPath = Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Program.cs")),
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Configuration["DPP_PASSPORT_CHECK"] = "off"; // the hourly check never runs in a test host (round seven, 5)
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
        services.AddAuthentication().AddCookie();
        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
            authorization.AddPolicy("ClusterAdminOrAdmin", policy => policy.RequireRole("clusterAdmin", "admin"));
        });
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            PublicDataRateLimit.Add(limiter, readLimit, 60);
            limiter.AddPolicy(SecurityRateLimitPolicyNames.ExternalApiRead, _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("none"));
        });

        var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }

    private sealed class Site(WebApplication app, PublicationRig rig, HttpClient http) : IAsyncDisposable
    {
        public PublicationRig Rig => rig;

        public string Id { get; } = Uri.EscapeDataString(PassportPagesTests.Identifier(rig));

        public Task<HttpResponseMessage> GetAsync(string path, string accept, string? authorization = null)
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

            return http.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            http.Dispose();
            await StopAsync(app);
        }

        /// <summary>
        /// Stops and disposes the host, waiting at most 10 seconds for open requests (C3 round three, point 5). Stopping late
        /// or with an error is logged, never a test failure: the test has already passed or failed on its own (round four,
        /// point 2).
        /// </summary>
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

    /// <summary>A service whose stop fails, as a host stopping late on a slow machine does.</summary>
    private sealed class FailsToStop : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => throw new OperationCanceledException("stopping took too long");
    }

    private sealed class NoMail : IEmailSender
    {
        public bool IsConfigured => false;

        public Task SendTemporaryPasswordAsync(string recipientEmail, string temporaryPassword, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
