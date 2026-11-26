using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.RegistryToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static BatteryPassWeb.Tests.Dpp.RegistryTokenTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3: the app's own HttpClient for the EU's key set, as <see cref="RegistryTokenAuthentication.AddRegistryToken(IServiceCollection, RegistryTokenSettings)"/>
/// sets it up: no redirect is followed (the key set's host can't send the fetch elsewhere), no cookies, a 10-second
/// timeout. Checked on the real client against a local server of the test's own, no network.
/// </summary>
public sealed class RegistryTokenHttpClientTests : IDisposable
{
    private readonly ECDsa _key = NewKey();

    public void Dispose() => _key.Dispose();

    private static ServiceProvider Services(RegistryTokenSettings settings) =>
        new ServiceCollection().AddLogging().AddRegistryToken(settings).BuildServiceProvider();

    [Fact]
    public void TheKeySetsClient_ShouldFollowNoRedirect_SendNoCookie_AndTimeOutAfterTenSeconds()
    {
        using var services = Services(Settings);
        var factory = services.GetRequiredService<IHttpMessageHandlerFactory>();

        HttpMessageHandler handler = factory.CreateHandler(RegistryTokenKeys.HttpClientName);
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        var sockets = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(sockets.AllowAutoRedirect);
        Assert.False(sockets.UseCookies);
        Assert.Equal(TimeSpan.FromSeconds(10), services.GetRequiredService<IHttpClientFactory>().CreateClient(RegistryTokenKeys.HttpClientName).Timeout);
    }

    [Fact]
    public async Task AKeySetThatRedirects_ShouldNotBeFollowed()
    {
        var other = 0;
        var jwks = new JsonObject { ["keys"] = new JsonArray(Jwk(_key, KeyId)) }.ToJsonString();
        await using var server = await ServerAsync(app =>
        {
            app.MapGet("/jwks", () => Results.Redirect("/elsewhere"));
            app.MapGet("/elsewhere", () =>
            {
                Interlocked.Increment(ref other);
                return Results.Text(jwks, "application/json");
            });
            app.MapGet("/direct", () => Results.Text(jwks, "application/json"));
        });

        var redirected = await CheckWithTheRealClientAsync($"{server.Address}/jwks");
        var direct = await CheckWithTheRealClientAsync($"{server.Address}/direct");

        Assert.Equal((401, "unknown-key"), (redirected.Status, redirected.Reason));
        Assert.Equal(0, other);
        Assert.True(direct.Authorised); // the same keys, served without a redirect, are used
    }

    private async Task<RegistryTokenResult> CheckWithTheRealClientAsync(string jwksUrl)
    {
        var settings = Settings with { JwksUrl = jwksUrl };
        using var services = Services(settings);
        var clock = new TestClock(Start);
        var keys = new RegistryTokenKeys(services.GetRequiredService<IHttpClientFactory>(), settings, clock, NullLogger<RegistryTokenKeys>.Instance);
        return await new RegistryTokenCheck(settings, keys, clock).CheckAsync(Token(_key, Header(), Claims(Start)));
    }

    internal static async Task<LocalServer> ServerAsync(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        map(app);
        await app.StartAsync();
        return new LocalServer(app, app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First());
    }

    internal sealed class LocalServer(WebApplication app, string address) : IAsyncDisposable
    {
        public string Address => address;

        public async ValueTask DisposeAsync()
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await app.StopAsync(stopping.Token);
            }
            catch (OperationCanceledException)
            {
            }

            await app.DisposeAsync();
        }
    }
}
