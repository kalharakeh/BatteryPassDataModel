using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// The app's pages as a browser gets them (package N1): its controllers and views on a test database, with the app's
/// error pages, signed in by a header. "X-Test-User" signs in an account of the database as the sign-in page would;
/// "X-Test-Claims" ("e-mail|name|role") makes a session without the database, for the error page.
/// </summary>
internal sealed class ShellSite : IAsyncDisposable
{
    public const string UserHeader = "X-Test-User";
    public const string ClaimsHeader = "X-Test-Claims";

    private readonly WebApplication _app;
    private readonly HttpClient _http;

    private ShellSite(WebApplication app, HttpClient http)
    {
        _app = app;
        _http = http;
    }

    public IServiceProvider Services => _app.Services;

    /// <summary>A site on <paramref name="context"/>, in Production (the error page is on) unless said otherwise.</summary>
    /// <param name="throwOn">An address that throws, to show the error page.</param>
    /// <param name="readLimit">The public read limit per minute, as the app sets it with rate limiting on; none when null.</param>
    public static async Task<ShellSite> StartAsync(MongoContext context, bool demoData = true, string environment = "Production", string? throwOn = null, int? readLimit = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(HomeController).Assembly.GetName().Name,
            ContentRootPath = Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Program.cs")),
            EnvironmentName = environment,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Configuration["DPP_PASSPORT_CHECK"] = "off";
        var options = new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl, DemoAdminEmail = string.Empty, DemoUsersPassword = DemoRig.Password, ExternalApiEncryptionKey = "shell-test-key-for-the-token-hashes-0000" };
        var services = builder.Services;
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(context);
        services.AddSingleton(new DemoDataSwitch(demoData));
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
        services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly);
        services.Replace(ServiceDescriptor.Singleton<IAntiforgery, NoAntiforgery>());
        services.AddAuthentication(HeaderSignIn.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderSignIn>(HeaderSignIn.SchemeName, _ => { });
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = TooManyRequests.WriteAsync;
            PublicDataRateLimit.Add(limiter, readLimit ?? 100000, 60);
        });
        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
            authorization.AddPolicy("ClusterAdminOrAdmin", policy => policy.RequireRole("clusterAdmin", "admin"));
        });

        var app = builder.Build();
        app.UseErrorPages();
        if (throwOn is not null)
        {
            app.Use(async (http, next) =>
            {
                if (http.Request.Path.Equals(throwOn, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("SHELLSECRETDETAIL a forced error");
                }

                await next();
            });
        }

        app.UseStaticFiles();
        app.UseRouting();
        if (readLimit is not null)
        {
            app.UseRateLimiter();
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapNotFoundPage();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new ShellSite(app, new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(60) });
    }

    /// <summary>
    /// A request as a browser makes it: following one redirect with the cookies the first answer set (the search hands its
    /// near matches to the home page that way).
    /// </summary>
    public async Task<(HttpStatusCode Status, string Body, string? Location)> FollowAsync(string path, string? email = null)
    {
        using var first = new HttpRequestMessage(HttpMethod.Get, path);
        if (email is not null)
        {
            first.Headers.Add(UserHeader, email);
        }

        using var answer = await _http.SendAsync(first);
        if (answer.StatusCode != HttpStatusCode.Redirect || answer.Headers.Location is null)
        {
            return (answer.StatusCode, await answer.Content.ReadAsStringAsync(), answer.Headers.Location?.OriginalString);
        }

        using var second = new HttpRequestMessage(HttpMethod.Get, answer.Headers.Location);
        if (email is not null)
        {
            second.Headers.Add(UserHeader, email);
        }

        if (answer.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            second.Headers.Add("Cookie", string.Join("; ", cookies.Select(cookie => cookie.Split(';')[0])));
        }

        using var response = await _http.SendAsync(second);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.Location?.OriginalString);
    }

    /// <summary>A GET as <paramref name="email"/> (an account in the database), or as a visitor.</summary>
    public Task<(HttpStatusCode Status, string Body, string? Location)> GetAsync(string path, string? email = null, string? claims = null, string? accept = null) =>
        SendAsync(HttpMethod.Get, path, email, claims, accept);

    /// <summary>A request with <paramref name="method"/>, as <paramref name="email"/> or as a visitor, with no body.</summary>
    public async Task<(HttpStatusCode Status, string Body, string? Location)> SendAsync(HttpMethod method, string path, string? email = null, string? claims = null, string? accept = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        if (email is not null)
        {
            request.Headers.Add(UserHeader, email);
        }

        if (claims is not null)
        {
            request.Headers.Add(ClaimsHeader, claims);
        }

        using var response = await _http.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.Location?.OriginalString);
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        try
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _app.StopAsync(stopping.Token);
        }
        catch (Exception exception) when (exception is OperationCanceledException or AggregateException or ObjectDisposedException)
        {
            // a host that doesn't stop in time is left to the test run's end
        }

        await _app.DisposeAsync();
    }

    /// <summary>Signs in by a request header, as the sign-in page would make the session.</summary>
    private sealed class HeaderSignIn(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, AuthService auth)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestHeader";

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers[ClaimsHeader].ToString() is { Length: > 0 } claims)
            {
                var parts = claims.Split('|');
                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.Email, parts[0]), new Claim(ClaimTypes.Name, parts[1]), new Claim(ClaimTypes.Role, parts[2])],
                    SchemeName);
                return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
            }

            var email = Request.Headers[UserHeader].ToString();
            if (email.Length == 0)
            {
                return AuthenticateResult.NoResult();
            }

            var principal = await auth.CreatePrincipalForUserAsync(email);
            return principal is null
                ? AuthenticateResult.Fail("no such account")
                : AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(principal.Claims, SchemeName)), SchemeName));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Redirect($"/login?ReturnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.Redirect("/not-allowed");
            return Task.CompletedTask;
        }
    }

    private sealed class NoAntiforgery : IAntiforgery
    {
        private static readonly AntiforgeryTokenSet Tokens = new("shell", "shell", "__RequestVerificationToken", "RequestVerificationToken");

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
