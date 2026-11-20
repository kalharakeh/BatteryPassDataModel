using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BatteryPassWeb.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Platform;

/// <summary>
/// FU4: the upload limit counts each signed-in person on their own, and a caller who isn't signed in by address. Two people
/// behind one office address each get the whole budget; one person is still limited. The limiter has to run after
/// authentication to know the person, which <c>Program.cs</c> is read for here.
/// </summary>
public sealed class UploadRateLimitTests
{
    private const int Budget = 3;

    [Fact]
    public async Task TwoSignedInPeopleFromOneAddress_ShouldEachGetTheWholeUploadBudget()
    {
        await using var site = await StartAsync();

        var ann = await site.PostManyAsync("ann@example.test", Budget + 1);
        var bob = await site.PostManyAsync("bob@example.test", Budget + 1);

        Assert.Equal([200, 200, 200, 429], ann);
        Assert.Equal([200, 200, 200, 429], bob);
    }

    [Fact]
    public async Task OneSignedInPerson_ShouldStillBeLimited_WhateverTheirCapitals()
    {
        await using var site = await StartAsync();

        var first = await site.PostManyAsync("ann@example.test", Budget);
        var then = await site.PostManyAsync("ann@example.test", 1);

        Assert.All(first, status => Assert.Equal(200, status));
        Assert.Equal([429], then);
    }

    [Fact]
    public async Task CallersWhoAreNotSignedIn_ShouldShareTheirAddressesBudget_ApartFromThePeopleSignedIn()
    {
        await using var site = await StartAsync();

        var anonymous = await site.PostManyAsync(null, Budget + 1);
        var person = await site.PostManyAsync("ann@example.test", 1);

        Assert.Equal([200, 200, 200, 429], anonymous);
        Assert.Equal([200], person);
    }

    [Fact]
    public void TheKey_ShouldNameThePersonWhenSignedIn_AndTheAddressOtherwise()
    {
        var anonymous = new DefaultHttpContext();
        anonymous.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");
        var ann = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ann@example.test")], "Test")) };
        ann.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");

        Assert.Equal("upload-ip:203.0.113.9", UploadRateLimit.PartitionKey(anonymous));
        Assert.StartsWith("upload-user:", UploadRateLimit.PartitionKey(ann), StringComparison.Ordinal);
        Assert.DoesNotContain("ann@example.test", UploadRateLimit.PartitionKey(ann), StringComparison.Ordinal);
    }

    [Fact]
    public void Program_ShouldLimitAfterItKnowsThePerson_AndKeepTheHeadersFirst()
    {
        var program = File.ReadAllText(BatteryPassWeb.Tests.Dpp.DppTestFiles.RepoFile("web", "Program.cs"));

        var headers = program.IndexOf("app.UseSecurityHeaders();", StringComparison.Ordinal);
        var routing = program.IndexOf("app.UseRouting();", StringComparison.Ordinal);
        var authentication = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var limiter = program.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal);
        var authorization = program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);

        Assert.True(headers >= 0 && headers < routing, "the security headers come first");
        Assert.True(routing < authentication, "routing comes before authentication");
        Assert.True(authentication < limiter, "the limiter runs after authentication, so it knows the person");
        Assert.True(limiter < authorization, "the limiter runs before authorization");
        Assert.Contains("UploadRateLimit.Add(", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_ShouldKeepTheLoginLimitPerAddress()
    {
        var program = File.ReadAllText(BatteryPassWeb.Tests.Dpp.DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("$\"login:{RemoteIpPartitionKey(context)}\"", program, StringComparison.Ordinal);
        Assert.Contains("external-token:", program, StringComparison.Ordinal);
    }

    private static async Task<Site> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderUser>("Test", null);
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            UploadRateLimit.Add(options, Budget, 600);
        });
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapPost("/upload", () => "ok").RequireRateLimiting(SecurityRateLimitPolicyNames.FileUpload);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new Site(app, new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(60) });
    }

    private sealed class Site(WebApplication app, HttpClient http) : IAsyncDisposable
    {
        public async Task<int[]> PostManyAsync(string? who, int count)
        {
            var statuses = new List<int>();
            for (var turn = 0; turn < count; turn++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/upload");
                if (who is not null)
                {
                    request.Headers.Add(HeaderUser.Header, turn % 2 == 0 ? who : who.ToUpperInvariant());
                }

                using var response = await http.SendAsync(request);
                statuses.Add((int)response.StatusCode);
            }

            return [.. statuses];
        }

        public async ValueTask DisposeAsync()
        {
            http.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    /// <summary>Signs the caller in as the person named in a header, so that no sign-in page is needed.</summary>
    private sealed class HeaderUser(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Header = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var who) || string.IsNullOrWhiteSpace(who))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, who.ToString().ToLowerInvariant())], "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}
